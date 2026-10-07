using SmartSales.Models;
using SmartSales.Services;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace SmartSales.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;

    public AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, AuditService audit)
    {
        _signIn = signIn;
        _users = users;
        _audit = audit;
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        const string invalid = "Invalid email or password.";
        var user = await _users.FindByEmailAsync(vm.Email.Trim());
        if (user == null)
        {
            await _audit.LogAsync("FailedLogin", "Account", result: "Failure", userName: vm.Email);
            ModelState.AddModelError("", invalid);
            return View(vm);
        }
        if (!user.IsActive)
        {
            await _audit.LogAsync("FailedLogin", "Account", user.Id, newValue: "Inactive account", result: "Failure", userId: user.Id, userName: user.Email);
            ModelState.AddModelError("", "Your account is inactive. Contact the administrator.");
            return View(vm);
        }

        var result = await _signIn.PasswordSignInAsync(user, vm.Password, vm.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await _audit.LogAsync("Login", "Account", user.Id, userId: user.Id, userName: user.Email);
            return LocalRedirect(Url.IsLocalUrl(vm.ReturnUrl) ? vm.ReturnUrl! : "/");
        }
        if (result.IsLockedOut)
        {
            await _audit.LogAsync("Lockout", "Account", user.Id, result: "Failure", userId: user.Id, userName: user.Email);
            ModelState.AddModelError("", "Account locked due to repeated failed login attempts. Try again in 15 minutes.");
            return View(vm);
        }

        await _audit.LogAsync("FailedLogin", "Account", user.Id, result: "Failure", userId: user.Id, userName: user.Email);
        var remaining = _users.Options.Lockout.MaxFailedAccessAttempts - await _users.GetAccessFailedCountAsync(user);
        ModelState.AddModelError("", $"{invalid} {remaining} attempt(s) left before lockout.");
        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new RegisterVm());
    }

    /// <summary>Self-registration creates a Sales Executive. Admins assign other roles.</summary>
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = new ApplicationUser
        {
            UserName = vm.Email.Trim(), Email = vm.Email.Trim(), FullName = vm.FullName.Trim(),
            PhoneNumber = vm.Phone, IsActive = true
        };
        var result = await _users.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }
        await _users.AddToRoleAsync(user, Roles.Sales);
        await _audit.LogAsync("Register", "User", user.Id, newValue: $"{{\"Email\":\"{user.Email}\",\"Role\":\"{Roles.Sales}\"}}",
            userId: user.Id, userName: user.Email);

        await _signIn.SignInAsync(user, isPersistent: false);
        await _audit.LogAsync("Login", "Account", user.Id, userId: user.Id, userName: user.Email);
        TempData["Success"] = "Welcome to SmartSales! Your account has been created.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("Logout", "Account", User.UserId());
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
