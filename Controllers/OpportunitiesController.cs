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
public class OpportunitiesController : Controller
{
    private readonly AppDbContext _db;
    private readonly CrmService _crm;

    public OpportunitiesController(AppDbContext db, CrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public async Task<IActionResult> Index(string? q, OpportunityStage? stage, string? status, int? customerId, string sort = "newest", int page = 1)
    {
        var query = _db.Opportunities.ForUser(User).Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(o => o.OpportunityName.Contains(q) || o.Customer!.CustomerName.Contains(q));
        }
        if (stage != null) query = query.Where(o => o.Stage == stage);
        if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
        if (customerId != null) query = query.Where(o => o.CustomerId == customerId);
        query = sort switch
        {
            "close" => query.OrderBy(o => o.ExpectedCloseDate),
            "name" => query.OrderBy(o => o.OpportunityName),
            _ => query.OrderByDescending(o => o.CreatedDate)
        };

        ViewBag.Q = q;
        ViewBag.Stage = stage;
        ViewBag.StatusFilter = status;
        ViewBag.Sort = sort;
        return View(await query.ToPagedAsync(page));
    }

    /// <summary>Kanban-style sales pipeline grouped by stage.</summary>
    public async Task<IActionResult> Pipeline()
    {
        var opps = await _db.Opportunities.ForUser(User).Include(o => o.Customer).Include(o => o.AssignedUser)
            .OrderBy(o => o.ExpectedCloseDate).ToListAsync();
        return View(opps);
    }

    public async Task<IActionResult> Details(int id)
    {
        var o = await _db.Opportunities.ForUser(User).Include(x => x.Customer).Include(x => x.Lead).Include(x => x.AssignedUser)
            .FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o == null) return NotFound();
        ViewBag.History = await _db.AuditLogs.Where(a => a.EntityName == nameof(Opportunity) && a.RecordId == id.ToString())
            .OrderByDescending(a => a.CreatedDate).Take(20).ToListAsync();
        return View(o);
    }

    public async Task<IActionResult> Create(int? customerId)
    {
        await LoadListsAsync();
        return View("Form", new OpportunityFormVm { CustomerId = customerId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OpportunityFormVm vm)
    {
        await ValidateBusinessRulesAsync(vm, isNew: true);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", vm);
        }

        var opp = new Opportunity { AssignedTo = assignee };
        Map(vm, opp);
        _db.Opportunities.Add(opp);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Opportunity created.";
        return RedirectToAction(nameof(Details), new { id = opp.OpportunityId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var o = await _db.Opportunities.ForUser(User).FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o == null) return NotFound();
        await LoadListsAsync();
        return View("Form", new OpportunityFormVm
        {
            OpportunityId = o.OpportunityId, OpportunityName = o.OpportunityName, CustomerId = o.CustomerId, LeadId = o.LeadId,
            Amount = o.Amount, Stage = o.Stage, Probability = o.Probability, ExpectedCloseDate = o.ExpectedCloseDate,
            Source = o.Source, Notes = o.Notes, AssignedTo = o.AssignedTo
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OpportunityFormVm vm)
    {
        if (id != vm.OpportunityId) return BadRequest();
        var opp = await _db.Opportunities.ForUser(User).FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (opp == null) return NotFound();

        if (!opp.IsActive && vm.Stage != opp.Stage)
            ModelState.AddModelError(nameof(vm.Stage), "Closed (Won/Lost) opportunities cannot be reopened.");
        await ValidateBusinessRulesAsync(vm, isNew: false);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo ?? opp.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", vm);
        }

        Map(vm, opp);
        if (!User.IsSalesExecutive()) opp.AssignedTo = assignee;
        opp.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Opportunity updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Quick stage change from the pipeline board / details page.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveStage(int id, OpportunityStage stage)
    {
        var opp = await _db.Opportunities.ForUser(User).FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (opp == null) return NotFound();
        if (!opp.IsActive)
        {
            TempData["Error"] = "Closed opportunities cannot change stage.";
        }
        else if (stage is not (OpportunityStage.Won or OpportunityStage.Lost) && opp.ExpectedCloseDate.Date < DateTime.Today)
        {
            TempData["Error"] = "Expected Close Date is in the past. Edit the opportunity and set a valid date first.";
        }
        else
        {
            opp.Stage = stage;
            opp.Status = Opportunity.StatusFor(stage);
            if (stage == OpportunityStage.Won) opp.Probability = 100;
            if (stage == OpportunityStage.Lost) opp.Probability = 0;
            if (!opp.IsActive) opp.ClosedDate = DateTime.Now;
            opp.ModifiedDate = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Moved to {stage}.";
        }
        return Redirect(Request.Headers.Referer.FirstOrDefault() is { } r && Url.IsLocalUrl(new Uri(r).PathAndQuery)
            ? new Uri(r).PathAndQuery
            : Url.Action(nameof(Pipeline))!);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var opp = await _db.Opportunities.FindAsync(id);
        if (opp == null) return NotFound();
        _db.Opportunities.Remove(opp);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Opportunity deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Server-side business validation (repeated even though the browser validates too).</summary>
    private async Task ValidateBusinessRulesAsync(OpportunityFormVm vm, bool isNew)
    {
        var active = vm.Stage is not (OpportunityStage.Won or OpportunityStage.Lost);
        if (vm.Amount <= 0)
            ModelState.AddModelError(nameof(vm.Amount), ValidationRules.AmountMessage);
        if (vm.Probability is < 0 or > 100)
            ModelState.AddModelError(nameof(vm.Probability), ValidationRules.ProbabilityMessage);
        if (active && vm.ExpectedCloseDate.Date < DateTime.Today)
            ModelState.AddModelError(nameof(vm.ExpectedCloseDate), ValidationRules.CloseDateMessage);
        if (isNew && !active)
            ModelState.AddModelError(nameof(vm.Stage), "A new opportunity must start in an open stage.");
        if (vm.CustomerId > 0 && !await _crm.CustomerVisibleAsync(User, vm.CustomerId))
            ModelState.AddModelError(nameof(vm.CustomerId), "Selected customer does not exist or is not accessible.");
    }

    private static void Map(OpportunityFormVm vm, Opportunity o)
    {
        o.OpportunityName = vm.OpportunityName.Trim();
        o.CustomerId = vm.CustomerId;
        o.Amount = vm.Amount;
        if (o.Stage != vm.Stage && vm.Stage is OpportunityStage.Won or OpportunityStage.Lost) o.ClosedDate = DateTime.Now;
        o.Stage = vm.Stage;
        o.Status = Opportunity.StatusFor(vm.Stage);
        o.Probability = vm.Stage switch { OpportunityStage.Won => 100, OpportunityStage.Lost => 0, _ => vm.Probability };
        o.ExpectedCloseDate = vm.ExpectedCloseDate.Date;
        o.Source = vm.Source?.Trim();
        o.Notes = vm.Notes?.Trim();
    }

    private async Task LoadListsAsync()
    {
        ViewBag.Users = await _crm.AssignableUsersAsync();
        ViewBag.Customers = await _crm.CustomerOptionsAsync(User);
    }
}
