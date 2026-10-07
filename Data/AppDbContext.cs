using System.Security.Claims;
using System.Text.Json;
using SmartSales.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SmartSales.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IHttpContextAccessor _http;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor http) : base(options)
    {
        _http = http;
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Store enums as readable text.
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Email).IsUnique();
            e.HasIndex(c => c.Phone).IsUnique();
            e.HasIndex(c => c.CustomerCode).IsUnique();
            e.HasOne(c => c.AssignedUser).WithMany().HasForeignKey(c => c.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Lead>(e =>
        {
            e.HasIndex(l => l.LeadCode).IsUnique();
            e.Property(l => l.ExpectedValue).HasPrecision(18, 2);
            e.HasOne(l => l.AssignedUser).WithMany().HasForeignKey(l => l.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Opportunity>(e =>
        {
            e.Property(o => o.Amount).HasPrecision(18, 2);
            e.HasOne(o => o.Customer).WithMany(c => c.Opportunities).HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.Lead).WithMany().HasForeignKey(o => o.LeadId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.AssignedUser).WithMany().HasForeignKey(o => o.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<FollowUp>(e =>
        {
            e.HasOne(f => f.Customer).WithMany(c => c.FollowUps).HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Lead).WithMany(l => l.FollowUps).HasForeignKey(f => f.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.AssignedUser).WithMany().HasForeignKey(f => f.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Activity>(e =>
        {
            e.HasOne(a => a.Customer).WithMany(c => c.Activities).HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Lead).WithMany(l => l.Activities).HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.AssignedUser).WithMany().HasForeignKey(a => a.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedDate);
            e.HasIndex(a => a.EntityName);
        });
    }

    /// <summary>
    /// Writes an AuditLog row for every create / update / delete of an <see cref="IAuditable"/> entity,
    /// including old and new values of the changed fields.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var pending = new List<PendingAudit>();
        foreach (var entry in ChangeTracker.Entries().Where(e => e.Entity is IAuditable))
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    pending.Add(new PendingAudit(entry, "Create"));
                    break;
                case EntityState.Modified:
                    var changed = entry.Properties
                        .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue) && p.Metadata.Name != "ModifiedDate")
                        .ToList();
                    if (changed.Count == 0) break;
                    pending.Add(new PendingAudit(entry, "Update")
                    {
                        RecordId = KeyOf(entry),
                        OldValue = Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)),
                        NewValue = Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)),
                    });
                    break;
                case EntityState.Deleted:
                    pending.Add(new PendingAudit(entry, "Delete")
                    {
                        RecordId = KeyOf(entry),
                        OldValue = Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)),
                    });
                    break;
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);
        if (pending.Count == 0) return result;

        var http = _http.HttpContext;
        foreach (var p in pending)
        {
            if (p.Action == "Create")
            {
                p.RecordId = KeyOf(p.Entry);
                p.NewValue = Serialize(p.Entry.Properties.ToDictionary(x => x.Metadata.Name, x => x.CurrentValue));
            }

            AuditLogs.Add(new AuditLog
            {
                UserId = http?.User.FindFirstValue(ClaimTypes.NameIdentifier),
                UserName = http?.User.Identity?.Name,
                Action = p.Action,
                EntityName = p.EntityName,
                RecordId = p.RecordId,
                OldValue = p.OldValue,
                NewValue = p.NewValue,
                IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            });
        }
        await base.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static string? KeyOf(EntityEntry entry) =>
        entry.Metadata.FindPrimaryKey()?.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()).FirstOrDefault();

    private static string Serialize(Dictionary<string, object?> values) =>
        JsonSerializer.Serialize(values.ToDictionary(k => k.Key, k => k.Value?.ToString()));

    private sealed class PendingAudit
    {
        public PendingAudit(EntityEntry entry, string action)
        {
            Entry = entry;
            Action = action;
            EntityName = entry.Metadata.ClrType.Name;
        }

        public EntityEntry Entry { get; }
        public string Action { get; }
        public string EntityName { get; }
        public string? RecordId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }
}
