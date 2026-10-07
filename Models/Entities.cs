using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace SmartSales.Models;

/// <summary>Entities whose create/update/delete operations are written to the audit log automatically.</summary>
public interface IAuditable { }

/// <summary>Entities owned by a user; Sales Executives only see records assigned to them.</summary>
public interface IAssignable
{
    string? AssignedTo { get; set; }
}

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

public class Customer : IAuditable, IAssignable
{
    public int CustomerId { get; set; }

    [Required, MaxLength(20)]
    public string CustomerCode { get; set; } = "";

    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = "";

    [Required, MaxLength(150)]
    public string Email { get; set; } = "";

    [Required, MaxLength(15)]
    public string Phone { get; set; } = "";

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    [MaxLength(60)]
    public string? City { get; set; }

    [MaxLength(60)]
    public string? State { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}

public class Lead : IAuditable, IAssignable
{
    public int LeadId { get; set; }

    [Required, MaxLength(20)]
    public string LeadCode { get; set; } = "";

    [Required, MaxLength(100)]
    public string LeadName { get; set; } = "";

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(15)]
    public string? Phone { get; set; }

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [Required, MaxLength(50)]
    public string Source { get; set; } = "Website";

    public LeadStatus Status { get; set; } = LeadStatus.New;

    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    public decimal ExpectedValue { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    public int? ConvertedCustomerId { get; set; }
    public DateTime? ConvertedDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ModifiedDate { get; set; }

    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}

public class Opportunity : IAuditable, IAssignable
{
    public int OpportunityId { get; set; }

    [Required, MaxLength(150)]
    public string OpportunityName { get; set; } = "";

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public decimal Amount { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    public int Probability { get; set; }

    public DateTime ExpectedCloseDate { get; set; }

    /// <summary>Open / Won / Lost - derived from Stage.</summary>
    [MaxLength(10)]
    public string Status { get; set; } = "Open";

    [MaxLength(50)]
    public string? Source { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ModifiedDate { get; set; }
    public DateTime? ClosedDate { get; set; }

    [NotMapped]
    public decimal WeightedAmount => Amount * Probability / 100m;

    [NotMapped]
    public bool IsActive => Stage is not (OpportunityStage.Won or OpportunityStage.Lost);

    public static string StatusFor(OpportunityStage stage) => stage switch
    {
        OpportunityStage.Won => "Won",
        OpportunityStage.Lost => "Lost",
        _ => "Open"
    };
}

public class FollowUp : IAuditable, IAssignable
{
    public int FollowUpId { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public DateTime FollowUpDate { get; set; }

    public FollowUpType FollowUpType { get; set; } = FollowUpType.Call;

    [Required, MaxLength(150)]
    public string Subject { get; set; } = "";

    [MaxLength(1000)]
    public string? Remarks { get; set; }

    public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? CompletedDate { get; set; }

    [NotMapped]
    public bool IsOverdue => Status == FollowUpStatus.Planned && FollowUpDate.Date < DateTime.Today;
}

public class Activity : IAuditable, IAssignable
{
    public int ActivityId { get; set; }

    public ActivityType ActivityType { get; set; } = ActivityType.Call;

    [Required, MaxLength(150)]
    public string Subject { get; set; } = "";

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

/// <summary>Append-only audit record. There is no UI or API to edit or delete these rows.</summary>
public class AuditLog
{
    public int AuditLogId { get; set; }

    public string? UserId { get; set; }

    [MaxLength(256)]
    public string? UserName { get; set; }

    [Required, MaxLength(50)]
    public string Action { get; set; } = "";

    [Required, MaxLength(50)]
    public string EntityName { get; set; } = "";

    [MaxLength(50)]
    public string? RecordId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    [MaxLength(20)]
    public string Result { get; set; } = "Success";

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [MaxLength(64)]
    public string? IpAddress { get; set; }
}
