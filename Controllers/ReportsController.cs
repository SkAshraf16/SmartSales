using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers;

// sales exec gets only own data in every report (ForUser)
[Authorize]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> Customers(DateTime? from, DateTime? to, CustomerStatus? status, bool csv = false)
    {
        var q = _db.Customers.ForUser(User).Include(c => c.AssignedUser).AsQueryable();
        if (from != null) q = q.Where(c => c.CreatedDate >= from.Value.Date);
        if (to != null) q = q.Where(c => c.CreatedDate < to.Value.Date.AddDays(1));
        if (status != null) q = q.Where(c => c.Status == status);
        var rows = await q.OrderBy(c => c.CustomerName).ToListAsync();

        if (csv)
            return File(Csv.Build(new[] { "Code", "Name", "Email", "Phone", "Status", "Owner", "Created" },
                rows.Select(c => new object?[] { c.CustomerCode, c.CustomerName, c.Email, c.Phone, c.Status, c.AssignedUser?.FullName, c.CreatedDate })),
                "text/csv", "customer-report.csv");
        ViewBag.From = from; ViewBag.To = to; ViewBag.Status = status;
        return View(rows);
    }

    public async Task<IActionResult> Leads(DateTime? from, DateTime? to, LeadStatus? status, bool csv = false)
    {
        var q = _db.Leads.ForUser(User).Include(l => l.AssignedUser).AsQueryable();
        if (from != null) q = q.Where(l => l.CreatedDate >= from.Value.Date);
        if (to != null) q = q.Where(l => l.CreatedDate < to.Value.Date.AddDays(1));
        if (status != null) q = q.Where(l => l.Status == status);
        var rows = await q.OrderByDescending(l => l.CreatedDate).ToListAsync();

        if (csv)
            return File(Csv.Build(new[] { "Code", "Name", "Source", "Status", "Priority", "Expected Value", "Owner", "Converted On" },
                rows.Select(l => new object?[] { l.LeadCode, l.LeadName, l.Source, l.Status, l.Priority, l.ExpectedValue, l.AssignedUser?.FullName, l.ConvertedDate })),
                "text/csv", "lead-report.csv");
        ViewBag.From = from; ViewBag.To = to; ViewBag.Status = status;
        return View(rows);
    }

    public async Task<IActionResult> FollowUps(DateTime? from, DateTime? to, FollowUpStatus? status, bool csv = false)
    {
        var q = _db.FollowUps.ForUser(User).Include(f => f.Customer).Include(f => f.Lead).Include(f => f.AssignedUser).AsQueryable();
        if (from != null) q = q.Where(f => f.FollowUpDate >= from.Value.Date);
        if (to != null) q = q.Where(f => f.FollowUpDate < to.Value.Date.AddDays(1));
        if (status != null) q = q.Where(f => f.Status == status);
        var rows = await q.OrderBy(f => f.FollowUpDate).ToListAsync();

        if (csv)
            return File(Csv.Build(new[] { "Date", "Subject", "Related To", "Status", "Overdue", "Owner" },
                rows.Select(f => new object?[] { f.FollowUpDate, f.Subject, f.Customer?.CustomerName ?? f.Lead?.LeadName, f.Status, f.IsOverdue ? "Yes" : "No", f.AssignedUser?.FullName })),
                "text/csv", "followup-report.csv");
        ViewBag.From = from; ViewBag.To = to; ViewBag.Status = status;
        return View(rows);
    }

    public async Task<IActionResult> Opportunities(DateTime? from, DateTime? to, OpportunityStage? stage, bool csv = false)
    {
        var q = _db.Opportunities.ForUser(User).Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();
        if (from != null) q = q.Where(o => o.ExpectedCloseDate >= from.Value.Date);
        if (to != null) q = q.Where(o => o.ExpectedCloseDate < to.Value.Date.AddDays(1));
        if (stage != null) q = q.Where(o => o.Stage == stage);
        var rows = await q.OrderBy(o => o.ExpectedCloseDate).ToListAsync();

        if (csv)
            return File(Csv.Build(new[] { "Name", "Customer", "Stage", "Amount", "Probability", "Weighted", "Expected Close", "Owner" },
                rows.Select(o => new object?[] { o.OpportunityName, o.Customer?.CustomerName, o.Stage, o.Amount, o.Probability, o.WeightedAmount, o.ExpectedCloseDate, o.AssignedUser?.FullName })),
                "text/csv", "opportunity-report.csv");
        ViewBag.From = from; ViewBag.To = to; ViewBag.Stage = stage;
        return View(rows);
    }

    // stage wise and owner wise pipeline, weighted = amount * prob / 100
    public async Task<IActionResult> Pipeline(bool csv = false)
    {
        var opps = await _db.Opportunities.ForUser(User).Include(o => o.AssignedUser).ToListAsync();
        var byStage = opps.GroupBy(o => o.Stage).OrderBy(g => g.Key)
            .Select(g => new object?[] { g.Key.ToString(), g.Count(), g.Sum(o => o.Amount), g.Sum(o => o.WeightedAmount) }).ToList();
        var byOwner = opps.Where(o => o.IsActive).GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned")
            .OrderByDescending(g => g.Sum(o => o.Amount))
            .Select(g => new object?[] { g.Key, g.Count(), g.Sum(o => o.Amount), g.Sum(o => o.WeightedAmount) }).ToList();

        if (csv)
            return File(Csv.Build(new[] { "Owner", "Open Opportunities", "Amount", "Weighted" }, byOwner), "text/csv", "pipeline-report.csv");
        ViewBag.ByStage = byStage;
        ViewBag.ByOwner = byOwner;
        return View();
    }

    public async Task<IActionResult> Conversion()
    {
        var leads = await _db.Leads.ForUser(User).ToListAsync();
        var opps = await _db.Opportunities.ForUser(User).ToListAsync();
        ViewBag.TotalLeads = leads.Count;
        ViewBag.Converted = leads.Count(l => l.Status == LeadStatus.Converted);
        ViewBag.LostLeads = leads.Count(l => l.Status == LeadStatus.Lost);
        ViewBag.Won = opps.Count(o => o.Status == "Won");
        ViewBag.LostOpps = opps.Count(o => o.Status == "Lost");
        ViewBag.WonValue = opps.Where(o => o.Status == "Won").Sum(o => o.Amount);
        ViewBag.BySource = leads.GroupBy(l => l.Source).OrderByDescending(g => g.Count())
            .Select(g => new object?[] { g.Key, g.Count(), g.Count(l => l.Status == LeadStatus.Converted) }).ToList();
        return View();
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> UserActivity(DateTime? from, DateTime? to, bool csv = false)
    {
        var q = _db.AuditLogs.Where(a => a.UserName != null);
        if (from != null) q = q.Where(a => a.CreatedDate >= from.Value.Date);
        if (to != null) q = q.Where(a => a.CreatedDate < to.Value.Date.AddDays(1));
        var rows = (await q.GroupBy(a => a.UserName).Select(g => new
        {
            User = g.Key,
            Total = g.Count(),
            Logins = g.Count(a => a.Action == "Login"),
            Creates = g.Count(a => a.Action == "Create"),
            Updates = g.Count(a => a.Action == "Update"),
            Deletes = g.Count(a => a.Action == "Delete"),
        }).ToListAsync())
            .OrderByDescending(r => r.Total)
            .Select(r => new object?[] { r.User, r.Total, r.Logins, r.Creates, r.Updates, r.Deletes }).ToList();

        if (csv)
            return File(Csv.Build(new[] { "User", "Total", "Logins", "Creates", "Updates", "Deletes" }, rows), "text/csv", "user-activity.csv");
        ViewBag.From = from; ViewBag.To = to;
        return View(rows);
    }
}
