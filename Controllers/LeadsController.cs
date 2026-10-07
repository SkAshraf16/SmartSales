using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers;

[Authorize]
public class LeadsController : Controller
{
    private readonly AppDbContext _db;
    private readonly CrmService _crm;

    public LeadsController(AppDbContext db, CrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public async Task<IActionResult> Index(string? q, LeadStatus? status, string? assignedTo, string sort = "newest", int page = 1)
    {
        var query = _db.Leads.ForUser(User).Include(l => l.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(l => l.LeadName.Contains(q) || (l.CompanyName != null && l.CompanyName.Contains(q))
                                     || (l.Email != null && l.Email.Contains(q)) || l.LeadCode.Contains(q));
        }
        if (status != null) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrEmpty(assignedTo)) query = query.Where(l => l.AssignedTo == assignedTo);
        query = sort switch
        {
            "name" => query.OrderBy(l => l.LeadName),
            "oldest" => query.OrderBy(l => l.CreatedDate),
            _ => query.OrderByDescending(l => l.CreatedDate)
        };

        ViewBag.Q = q;
        ViewBag.Status = status;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.Sort = sort;
        ViewBag.Users = await _crm.AssignableUsersAsync();
        return View(await query.ToPagedAsync(page));
    }

    public async Task<IActionResult> Details(int id)
    {
        var lead = await _db.Leads.ForUser(User).Include(l => l.AssignedUser).Include(l => l.FollowUps).Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead == null) return NotFound();
        ViewBag.History = await _db.AuditLogs.Where(a => a.EntityName == nameof(Lead) && a.RecordId == id.ToString())
            .OrderByDescending(a => a.CreatedDate).Take(20).ToListAsync();
        return View(lead);
    }

    public async Task<IActionResult> Create()
    {
        await LoadListsAsync(LeadStatus.New, isNew: true);
        return View("Form", new LeadFormVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeadFormVm vm)
    {
        if (vm.Status is not (LeadStatus.New or LeadStatus.Contacted))
            ModelState.AddModelError(nameof(vm.Status), "A new lead must start as New or Contacted.");
        ValidateCommon(vm);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync(LeadStatus.New, isNew: true);
            return View("Form", vm);
        }

        var lead = new Lead { LeadCode = await _crm.NextLeadCodeAsync(), AssignedTo = assignee, Status = vm.Status };
        Map(vm, lead);
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Lead {lead.LeadName} created.";
        return RedirectToAction(nameof(Details), new { id = lead.LeadId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var l = await _db.Leads.ForUser(User).FirstOrDefaultAsync(x => x.LeadId == id);
        if (l == null) return NotFound();
        if (l.Status == LeadStatus.Converted)
        {
            TempData["Warning"] = "Converted leads cannot be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }
        await LoadListsAsync(l.Status, isNew: false);
        return View("Form", new LeadFormVm
        {
            LeadId = l.LeadId, LeadName = l.LeadName, Email = l.Email, Phone = l.Phone, CompanyName = l.CompanyName,
            Source = l.Source, Status = l.Status, Priority = l.Priority, ExpectedValue = l.ExpectedValue, Notes = l.Notes,
            AssignedTo = l.AssignedTo
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LeadFormVm vm)
    {
        if (id != vm.LeadId) return BadRequest();
        var lead = await _db.Leads.ForUser(User).FirstOrDefaultAsync(x => x.LeadId == id);
        if (lead == null) return NotFound();

        // Workflow validation: only configured transitions are allowed.
        if (!LeadWorkflow.CanMove(lead.Status, vm.Status))
            ModelState.AddModelError(nameof(vm.Status), $"Lead status cannot change from {lead.Status} to {vm.Status}.");
        ValidateCommon(vm);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo ?? lead.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync(lead.Status, isNew: false);
            return View("Form", vm);
        }

        Map(vm, lead);
        lead.Status = vm.Status;
        if (!User.IsSalesExecutive()) lead.AssignedTo = assignee;
        lead.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Lead updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await _db.Leads.ForUser(User).FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead == null) return NotFound();

        var (customer, opportunity, error) = await _crm.ConvertLeadAsync(lead, User);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }
        TempData["Success"] = $"Lead converted to customer {customer!.CustomerCode} and a new opportunity.";
        return RedirectToAction("Details", "Opportunities", new { id = opportunity!.OpportunityId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _db.Leads.FindAsync(id);
        if (lead == null) return NotFound();
        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Lead deleted.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateCommon(LeadFormVm vm)
    {
        if (!LeadSources.All.Contains(vm.Source))
            ModelState.AddModelError(nameof(vm.Source), "Select a valid lead source.");
        if (!Enum.IsDefined(vm.Status))
            ModelState.AddModelError(nameof(vm.Status), "Select a valid lead status.");
        if (vm.Status == LeadStatus.Converted)
            ModelState.AddModelError(nameof(vm.Status), "Use the Convert action to convert a lead.");
    }

    private static void Map(LeadFormVm vm, Lead l)
    {
        l.LeadName = vm.LeadName.Trim();
        l.Email = vm.Email?.Trim().ToLowerInvariant();
        l.Phone = vm.Phone?.Trim();
        l.CompanyName = vm.CompanyName?.Trim();
        l.Source = vm.Source;
        l.Priority = vm.Priority;
        l.ExpectedValue = vm.ExpectedValue;
        l.Notes = vm.Notes?.Trim();
    }

    private async Task LoadListsAsync(LeadStatus current, bool isNew)
    {
        ViewBag.Users = await _crm.AssignableUsersAsync();
        ViewBag.Statuses = isNew
            ? new[] { LeadStatus.New, LeadStatus.Contacted }
            : LeadWorkflow.NextStatuses(current).ToArray();
    }
}
