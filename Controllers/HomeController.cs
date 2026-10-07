using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Role-scoped dashboard. All numbers are computed on the server from records the user may see.</summary>
    public async Task<IActionResult> Index(string range = "all", DateTime? from = null, DateTime? to = null)
    {
        (DateTime? start, DateTime? end) = range switch
        {
            "today" => (DateTime.Today, DateTime.Today.AddDays(1)),
            "week" => (DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek), DateTime.Today.AddDays(1)),
            "month" => (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today.AddDays(1)),
            "custom" => (from?.Date, to?.Date.AddDays(1)),
            _ => ((DateTime?)null, (DateTime?)null)
        };

        var customers = _db.Customers.ForUser(User);
        var leads = _db.Leads.ForUser(User);
        var opps = _db.Opportunities.ForUser(User);
        if (start != null) { customers = customers.Where(c => c.CreatedDate >= start); leads = leads.Where(l => l.CreatedDate >= start); opps = opps.Where(o => o.CreatedDate >= start); }
        if (end != null) { customers = customers.Where(c => c.CreatedDate < end); leads = leads.Where(l => l.CreatedDate < end); opps = opps.Where(o => o.CreatedDate < end); }

        // SQLite cannot aggregate decimals server-side, so amounts are summed in memory (data is already scoped).
        var oppList = await opps.Select(o => new { o.Stage, o.Status, o.Amount, o.Probability, o.ClosedDate, o.ExpectedCloseDate }).ToListAsync();
        var leadStatusCounts = await leads.GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var followUps = _db.FollowUps.ForUser(User).Where(f => f.Status == FollowUpStatus.Planned);

        var vm = new DashboardVm
        {
            Range = range,
            TotalCustomers = await customers.CountAsync(),
            TotalLeads = leadStatusCounts.Sum(x => x.Count),
            OpenLeads = leadStatusCounts.Where(x => LeadWorkflow.IsOpen(x.Key)).Sum(x => x.Count),
            TotalOpportunities = oppList.Count,
            OpenOpportunities = oppList.Count(o => o.Status == "Open"),
            WonOpportunities = oppList.Count(o => o.Status == "Won"),
            LostOpportunities = oppList.Count(o => o.Status == "Lost"),
            PipelineValue = oppList.Where(o => o.Status == "Open").Sum(o => o.Amount),
            WeightedPipeline = oppList.Where(o => o.Status == "Open").Sum(o => o.Amount * o.Probability / 100m),
            PendingFollowUps = await followUps.CountAsync(),
            OverdueFollowUps = await followUps.CountAsync(f => f.FollowUpDate < DateTime.Today),
            UpcomingFollowUps = await followUps.Include(f => f.Customer).Include(f => f.Lead)
                .OrderBy(f => f.FollowUpDate).Take(6).ToListAsync(),
        };

        foreach (var s in new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Lost, LeadStatus.Converted })
            vm.LeadStatusChart[s.ToString()] = leadStatusCounts.FirstOrDefault(x => x.Key == s)?.Count ?? 0;

        foreach (var s in Enum.GetValues<OpportunityStage>())
            vm.PipelineChart[s.ToString()] = oppList.Where(o => o.Stage == s).Sum(o => o.Amount);

        var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        for (var m = firstMonth; m <= DateTime.Today; m = m.AddMonths(1))
        {
            var month = m;
            vm.MonthlySalesChart[month.ToString("MMM yy")] = oppList
                .Where(o => o.Status == "Won")
                .Where(o => (o.ClosedDate ?? o.ExpectedCloseDate).Year == month.Year && (o.ClosedDate ?? o.ExpectedCloseDate).Month == month.Month)
                .Sum(o => o.Amount);
        }

        if (User.IsInRole(Roles.Admin))
        {
            vm.TotalUsers = await _db.Users.CountAsync();
            vm.FailedLoginsToday = await _db.AuditLogs.CountAsync(a => a.Action == "FailedLogin" && a.CreatedDate >= DateTime.Today);
            vm.RecentActivity = await _db.AuditLogs.OrderByDescending(a => a.AuditLogId).Take(6).ToListAsync();
        }

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
