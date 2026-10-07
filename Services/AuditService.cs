using System.Security.Claims;
using SmartSales.Data;
using SmartSales.Models;

namespace SmartSales.Services;

/// <summary>
/// Records security / workflow events that are not plain entity changes (login, lockout, role change, conversion...).
/// Entity create/update/delete is captured automatically by <see cref="AppDbContext.SaveChangesAsync"/>.
/// Never pass passwords, hashes or tokens to this service.
/// </summary>
public class AuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(string action, string entityName, string? recordId = null, string? oldValue = null,
        string? newValue = null, string result = "Success", string? userId = null, string? userName = null)
    {
        var ctx = _http.HttpContext;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = userName ?? ctx?.User.Identity?.Name,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = oldValue,
            NewValue = newValue,
            Result = result,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
        });
        await _db.SaveChangesAsync();
    }
}
