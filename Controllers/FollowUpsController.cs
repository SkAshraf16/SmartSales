using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using SmartSales.Validation;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers;

[Authorize]
public class FollowUpsController : Controller
{
    private readonly AppDbContext _db;
    private readonly CrmService _crm;

    public FollowUpsController(AppDbContext db, CrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    /// <param name="view">all | pending | overdue | upcoming</param>
    public async Task<IActionResult> Index(string view = "pending", FollowUpStatus? status = null, DateTime? date = null,
        string? assignedTo = null, string? q = null, int page = 1)
    {
        var query = _db.FollowUps.ForUser(User).Include(f => f.Customer).Include(f => f.Lead).Include(f => f.AssignedUser).AsQueryable();
        query = view switch
        {
            "pending" => query.Where(f => f.Status == FollowUpStatus.Planned),
            "overdue" => query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < DateTime.Today),
            "upcoming" => query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= DateTime.Today),
            _ => query
        };
        if (status != null) query = query.Where(f => f.Status == status);
        if (date != null) query = query.Where(f => f.FollowUpDate >= date.Value.Date && f.FollowUpDate < date.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(assignedTo)) query = query.Where(f => f.AssignedTo == assignedTo);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(f => f.Subject.Contains(q) || (f.Customer != null && f.Customer.CustomerName.Contains(q))
                                     || (f.Lead != null && f.Lead.LeadName.Contains(q)));

        ViewBag.View = view;
        ViewBag.Status = status;
        ViewBag.Date = date;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.Q = q;
        ViewBag.Users = await _crm.AssignableUsersAsync();
        return View(await query.OrderBy(f => f.FollowUpDate).ToPagedAsync(page));
    }

    public async Task<IActionResult> Create(int? customerId, int? leadId)
    {
        await LoadListsAsync();
        return View("Form", new FollowUpFormVm { CustomerId = customerId, LeadId = leadId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUpFormVm vm)
    {
        vm.Status = FollowUpStatus.Planned;
        ModelState.Remove(nameof(vm.Status));
        await ValidateAsync(vm, isNew: true);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", vm);
        }

        var f = new FollowUp { AssignedTo = assignee, Status = FollowUpStatus.Planned };
        Map(vm, f);
        _db.FollowUps.Add(f);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Follow-up scheduled.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var f = await _db.FollowUps.ForUser(User).FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f == null) return NotFound();
        await LoadListsAsync();
        return View("Form", new FollowUpFormVm
        {
            FollowUpId = f.FollowUpId, CustomerId = f.CustomerId, LeadId = f.LeadId, FollowUpDate = f.FollowUpDate,
            FollowUpType = f.FollowUpType, Subject = f.Subject, Remarks = f.Remarks, Status = f.Status, AssignedTo = f.AssignedTo
        });
    }

    /// <summary>Edit / reschedule. Rescheduling a planned follow-up still requires a date from today onward.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUpFormVm vm)
    {
        if (id != vm.FollowUpId) return BadRequest();
        var f = await _db.FollowUps.ForUser(User).FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f == null) return NotFound();

        // An unchanged historical date on a planned follow-up may be kept (it will show as overdue).
        if (vm.Status == FollowUpStatus.Planned && vm.FollowUpDate.Date == f.FollowUpDate.Date)
            ModelState.Remove(nameof(vm.FollowUpDate));
        await ValidateAsync(vm, isNew: false, originalDate: f.FollowUpDate);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo ?? f.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", vm);
        }

        Map(vm, f);
        if (f.Status != vm.Status && vm.Status == FollowUpStatus.Completed) f.CompletedDate = DateTime.Now;
        f.Status = vm.Status;
        if (!User.IsSalesExecutive()) f.AssignedTo = assignee;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Follow-up updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, FollowUpStatus status)
    {
        var f = await _db.FollowUps.ForUser(User).FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f == null) return NotFound();
        if (f.Status != FollowUpStatus.Planned)
        {
            TempData["Error"] = "Only planned follow-ups can be updated.";
        }
        else
        {
            f.Status = status;
            if (status == FollowUpStatus.Completed) f.CompletedDate = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Follow-up marked as {status}.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f == null) return NotFound();
        _db.FollowUps.Remove(f);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Follow-up deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(FollowUpFormVm vm, bool isNew, DateTime? originalDate = null)
    {
        if (vm.CustomerId == null && vm.LeadId == null)
            ModelState.AddModelError("", "Select a customer or a lead for this follow-up.");
        if (vm.CustomerId != null && !await _crm.CustomerVisibleAsync(User, vm.CustomerId.Value))
            ModelState.AddModelError(nameof(vm.CustomerId), "Selected customer does not exist or is not accessible.");
        if (vm.LeadId != null && !await _crm.LeadVisibleAsync(User, vm.LeadId.Value))
            ModelState.AddModelError(nameof(vm.LeadId), "Selected lead does not exist or is not accessible.");
        var dateChanged = isNew || originalDate?.Date != vm.FollowUpDate.Date;
        if (vm.Status == FollowUpStatus.Planned && dateChanged && vm.FollowUpDate.Date < DateTime.Today)
            ModelState.AddModelError(nameof(vm.FollowUpDate), ValidationRules.FollowUpDateMessage);
    }

    private static void Map(FollowUpFormVm vm, FollowUp f)
    {
        f.CustomerId = vm.CustomerId;
        f.LeadId = vm.LeadId;
        f.FollowUpDate = vm.FollowUpDate.Date;
        f.FollowUpType = vm.FollowUpType;
        f.Subject = vm.Subject.Trim();
        f.Remarks = vm.Remarks?.Trim();
    }

    private async Task LoadListsAsync()
    {
        ViewBag.Users = await _crm.AssignableUsersAsync();
        ViewBag.Customers = await _crm.CustomerOptionsAsync(User);
        ViewBag.Leads = await _crm.LeadOptionsAsync(User);
    }
}
