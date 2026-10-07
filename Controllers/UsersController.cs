using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers;

/// <summary>User &amp; Role management. Admin has full control; Manager can only view the user list.</summary>
[Authorize(Roles = Roles.AdminOrManager)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly AuditService _audit;
    private readonly AppDbContext _db;

    public UsersController(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, AuditService audit, AppDbContext db)
    {
        _users = users;
        _roles = roles;
        _audit = audit;
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, string? role)
    {
        var query = _users.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(u => u.FullName.Contains(q) || u.Email!.Contains(q));
        var list = new List<UserListItemVm>();
        foreach (var u in await query.OrderBy(u => u.FullName).ToListAsync())
        {
            var userRole = (await _users.GetRolesAsync(u)).FirstOrDefault() ?? "-";
            if (!string.IsNullOrEmpty(role) && userRole != role) continue;
            list.Add(new UserListItemVm
            {
                Id = u.Id, FullName = u.FullName, Email = u.Email!, Role = userRole, IsActive = u.IsActive,
                IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
                FailedCount = u.AccessFailedCount, CreatedDate = u.CreatedDate
            });
        }
        ViewBag.Q = q;
        ViewBag.Role = role;
        return View(list);
    }

    [Authorize(Roles = Roles.Admin)]
    public IActionResult Create() => View("Form", new UserFormVm());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(UserFormVm vm)
    {
        if (string.IsNullOrEmpty(vm.Password)) ModelState.AddModelError(nameof(vm.Password), "Password is required for new users.");
        if (!Roles.All.Contains(vm.Role)) ModelState.AddModelError(nameof(vm.Role), "Select a valid role.");
        if (!ModelState.IsValid) return View("Form", vm);

        var user = new ApplicationUser
        {
            UserName = vm.Email.Trim(), Email = vm.Email.Trim(), FullName = vm.FullName.Trim(), PhoneNumber = vm.Phone,
            IsActive = vm.IsActive, EmailConfirmed = true
        };
        var result = await _users.CreateAsync(user, vm.Password!);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View("Form", vm);
        }
        await _users.AddToRoleAsync(user, vm.Role);
        await _audit.LogAsync("Create", "User", user.Id, newValue: $"{{\"Email\":\"{user.Email}\",\"Role\":\"{vm.Role}\",\"IsActive\":\"{vm.IsActive}\"}}");
        TempData["Success"] = $"User {user.Email} created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u == null) return NotFound();
        return View("Form", new UserFormVm
        {
            Id = u.Id, FullName = u.FullName, Email = u.Email!, Phone = u.PhoneNumber, IsActive = u.IsActive,
            Role = (await _users.GetRolesAsync(u)).FirstOrDefault() ?? Roles.Sales
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(string id, UserFormVm vm)
    {
        var u = await _users.FindByIdAsync(id);
        if (u == null) return NotFound();
        ModelState.Remove(nameof(vm.Password));
        if (!Roles.All.Contains(vm.Role)) ModelState.AddModelError(nameof(vm.Role), "Select a valid role.");
        if (u.Id == User.UserId() && (!vm.IsActive || vm.Role != Roles.Admin))
            ModelState.AddModelError("", "You cannot deactivate yourself or remove your own Admin role.");
        if (!ModelState.IsValid) return View("Form", vm);

        var oldRole = (await _users.GetRolesAsync(u)).FirstOrDefault();
        var oldActive = u.IsActive;
        u.FullName = vm.FullName.Trim();
        u.Email = vm.Email.Trim();
        u.UserName = vm.Email.Trim();
        u.PhoneNumber = vm.Phone;
        u.IsActive = vm.IsActive;
        var result = await _users.UpdateAsync(u);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View("Form", vm);
        }

        if (oldRole != vm.Role)
        {
            if (oldRole != null) await _users.RemoveFromRoleAsync(u, oldRole);
            await _users.AddToRoleAsync(u, vm.Role);
            await _users.UpdateSecurityStampAsync(u); // forces re-login with new role
            await _audit.LogAsync("RoleChange", "User", u.Id, oldRole, vm.Role);
        }
        if (oldActive != vm.IsActive)
            await _audit.LogAsync(vm.IsActive ? "Activate" : "Deactivate", "User", u.Id, oldActive.ToString(), vm.IsActive.ToString());
        await _audit.LogAsync("Update", "User", u.Id, newValue: $"{{\"FullName\":\"{u.FullName}\",\"Email\":\"{u.Email}\"}}");

        TempData["Success"] = "User updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u == null) return NotFound();
        if (u.Id == User.UserId())
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }
        u.IsActive = !u.IsActive;
        await _users.UpdateAsync(u);
        await _users.UpdateSecurityStampAsync(u);
        await _audit.LogAsync(u.IsActive ? "Activate" : "Deactivate", "User", u.Id, (!u.IsActive).ToString(), u.IsActive.ToString());
        TempData["Success"] = $"{u.Email} is now {(u.IsActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Unlock(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u == null) return NotFound();
        await _users.SetLockoutEndDateAsync(u, null);
        await _users.ResetAccessFailedCountAsync(u);
        await _audit.LogAsync("Unlock", "User", u.Id);
        TempData["Success"] = $"{u.Email} unlocked.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u == null) return NotFound();
        return View(new ResetPasswordVm { Id = u.Id, Email = u.Email });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
    {
        var u = await _users.FindByIdAsync(vm.Id);
        if (u == null) return NotFound();
        if (!ModelState.IsValid) return View(vm);

        var token = await _users.GeneratePasswordResetTokenAsync(u);
        var result = await _users.ResetPasswordAsync(u, token, vm.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }
        await _users.SetLockoutEndDateAsync(u, null);
        await _audit.LogAsync("PasswordReset", "User", u.Id); // never log the password itself
        TempData["Success"] = $"Password reset for {u.Email}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Roles with user counts.</summary>
    public async Task<IActionResult> RoleList()
    {
        var model = new List<(string Role, int Count)>();
        foreach (var r in await _roles.Roles.OrderBy(r => r.Name).ToListAsync())
            model.Add((r.Name!, (await _users.GetUsersInRoleAsync(r.Name!)).Count));
        return View(model);
    }

    /// <summary>Permission matrix enforced by [Authorize] attributes and record scoping.</summary>
    public IActionResult Permissions() => View();
}
