using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers;

/// <summary>Calls, meetings, emails and tasks logged against customers or leads.</summary>
[Authorize]
public class ActivitiesController : Controller
{
    private readonly AppDbContext _db;
    private readonly CrmService _crm;

    public ActivitiesController(AppDbContext db, CrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public async Task<IActionResult> Index(ActivityType? type, ActivityStatus? status, DateTime? date, string? assignedTo, int page = 1)
    {
        var query = _db.Activities.ForUser(User).Include(a => a.Customer).Include(a => a.Lead).Include(a => a.AssignedUser).AsQueryable();
        if (type != null) query = query.Where(a => a.ActivityType == type);
        if (status != null) query = query.Where(a => a.Status == status);
        if (date != null) query = query.Where(a => a.ActivityDate >= date.Value.Date && a.ActivityDate < date.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(assignedTo)) query = query.Where(a => a.AssignedTo == assignedTo);

        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.Date = date;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.Users = await _crm.AssignableUsersAsync();
        return View(await query.OrderByDescending(a => a.ActivityDate).ToPagedAsync(page));
    }

    public async Task<IActionResult> Create(ActivityType? type, int? customerId, int? leadId)
    {
        await LoadListsAsync();
        return View("Form", new ActivityFormVm { ActivityType = type ?? ActivityType.Call, CustomerId = customerId, LeadId = leadId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityFormVm vm)
    {
        await ValidateAsync(vm);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", vm);
        }

        var a = new Activity { AssignedTo = assignee };
        Map(vm, a);
        _db.Activities.Add(a);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{a.ActivityType} logged.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var a = await _db.Activities.ForUser(User).FirstOrDefaultAsync(x => x.ActivityId == id);
        if (a == null) return NotFound();
        await LoadListsAsync();
        return View("Form", new ActivityFormVm
        {
            ActivityId = a.ActivityId, ActivityType = a.ActivityType, Subject = a.Subject, Description = a.Description,
            ActivityDate = a.ActivityDate, CustomerId = a.CustomerId, LeadId = a.LeadId, Status = a.Status, AssignedTo = a.AssignedTo
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityFormVm vm)
    {
        if (id != vm.ActivityId) return BadRequest();
        var a = await _db.Activities.ForUser(User).FirstOrDefaultAsync(x => x.ActivityId == id);
        if (a == null) return NotFound();
        await ValidateAsync(vm);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo ?? a.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", vm);
        }

        Map(vm, a);
        if (!User.IsSalesExecutive()) a.AssignedTo = assignee;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Activity updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _db.Activities.FindAsync(id);
        if (a == null) return NotFound();
        _db.Activities.Remove(a);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Activity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(ActivityFormVm vm)
    {
        if (vm.CustomerId == null && vm.LeadId == null)
            ModelState.AddModelError("", "Select a customer or a lead for this activity.");
        if (vm.CustomerId != null && !await _crm.CustomerVisibleAsync(User, vm.CustomerId.Value))
            ModelState.AddModelError(nameof(vm.CustomerId), "Selected customer does not exist or is not accessible.");
        if (vm.LeadId != null && !await _crm.LeadVisibleAsync(User, vm.LeadId.Value))
            ModelState.AddModelError(nameof(vm.LeadId), "Selected lead does not exist or is not accessible.");
        if (vm.Status == ActivityStatus.Planned && vm.ActivityDate.Date < DateTime.Today && vm.ActivityId == 0)
            ModelState.AddModelError(nameof(vm.ActivityDate), "A planned activity cannot be dated in the past.");
    }

    private static void Map(ActivityFormVm vm, Activity a)
    {
        a.ActivityType = vm.ActivityType;
        a.Subject = vm.Subject.Trim();
        a.Description = vm.Description?.Trim();
        a.ActivityDate = vm.ActivityDate.Date;
        a.CustomerId = vm.CustomerId;
        a.LeadId = vm.LeadId;
        a.Status = vm.Status;
    }

    private async Task LoadListsAsync()
    {
        ViewBag.Users = await _crm.AssignableUsersAsync();
        ViewBag.Customers = await _crm.CustomerOptionsAsync(User);
        ViewBag.Leads = await _crm.LeadOptionsAsync(User);
    }
}
