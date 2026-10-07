using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers;

/// <summary>Read-only audit trail (Admin only). No edit/delete actions exist by design.</summary>
[Authorize(Roles = Roles.Admin)]
public class AuditLogsController : Controller
{
    private readonly AppDbContext _db;

    public AuditLogsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? user, string? entity, string? act, DateTime? from, DateTime? to, int page = 1)
    {
        var query = Filter(user, entity, act, from, to);

        ViewBag.User = user;
        ViewBag.Entity = entity;
        ViewBag.Act = act;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Entities = await _db.AuditLogs.Select(a => a.EntityName).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.Actions = await _db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(x => x).ToListAsync();
        return View(await query.OrderByDescending(a => a.AuditLogId).ToPagedAsync(page, 20));
    }

    public async Task<IActionResult> Details(int id)
    {
        var log = await _db.AuditLogs.FindAsync(id);
        return log == null ? NotFound() : View(log);
    }

    public async Task<IActionResult> Export(string? user, string? entity, string? act, DateTime? from, DateTime? to)
    {
        var rows = await Filter(user, entity, act, from, to).OrderByDescending(a => a.AuditLogId).Take(5000).ToListAsync();
        var csv = Csv.Build(new[] { "Id", "Date", "User", "Action", "Entity", "RecordId", "Result", "IP" },
            rows.Select(r => new object?[] { r.AuditLogId, r.CreatedDate, r.UserName, r.Action, r.EntityName, r.RecordId, r.Result, r.IpAddress }));
        return File(csv, "text/csv", $"audit-log-{DateTime.Now:yyyyMMdd}.csv");
    }

    private IQueryable<AuditLog> Filter(string? user, string? entity, string? act, DateTime? from, DateTime? to)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(user)) query = query.Where(a => a.UserName != null && a.UserName.Contains(user));
        if (!string.IsNullOrEmpty(entity)) query = query.Where(a => a.EntityName == entity);
        if (!string.IsNullOrEmpty(act)) query = query.Where(a => a.Action == act);
        if (from != null) query = query.Where(a => a.CreatedDate >= from.Value.Date);
        if (to != null) query = query.Where(a => a.CreatedDate < to.Value.Date.AddDays(1));
        return query;
    }
}
