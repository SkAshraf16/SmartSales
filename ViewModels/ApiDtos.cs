using System.ComponentModel.DataAnnotations;
using SmartSales.Models;
using SmartSales.Validation;
using V = SmartSales.Validation.ValidationRules;

namespace SmartSales.ViewModels;

// Response DTOs - never expose EF entities or security fields directly.

public record CustomerDto(int CustomerId, string CustomerCode, string CustomerName, string Email, string Phone,
    string? CompanyName, string? Address, string? City, string? State, string Status, string? AssignedTo,
    string? AssignedToName, DateTime CreatedDate)
{
    public static CustomerDto From(Customer c) => new(c.CustomerId, c.CustomerCode, c.CustomerName, c.Email, c.Phone,
        c.CompanyName, c.Address, c.City, c.State, c.Status.ToString(), c.AssignedTo, c.AssignedUser?.FullName, c.CreatedDate);
}

public record LeadDto(int LeadId, string LeadCode, string LeadName, string? Email, string? Phone, string? CompanyName,
    string Source, string Status, string Priority, decimal ExpectedValue, string? AssignedTo, string? AssignedToName,
    DateTime CreatedDate)
{
    public static LeadDto From(Lead l) => new(l.LeadId, l.LeadCode, l.LeadName, l.Email, l.Phone, l.CompanyName,
        l.Source, l.Status.ToString(), l.Priority.ToString(), l.ExpectedValue, l.AssignedTo, l.AssignedUser?.FullName, l.CreatedDate);
}

public record OpportunityDto(int OpportunityId, string OpportunityName, int CustomerId, string? CustomerName,
    decimal Amount, string Stage, int Probability, decimal WeightedAmount, DateTime ExpectedCloseDate, string Status,
    string? AssignedTo, string? AssignedToName, DateTime CreatedDate)
{
    public static OpportunityDto From(Opportunity o) => new(o.OpportunityId, o.OpportunityName, o.CustomerId,
        o.Customer?.CustomerName, o.Amount, o.Stage.ToString(), o.Probability, o.WeightedAmount, o.ExpectedCloseDate,
        o.Status, o.AssignedTo, o.AssignedUser?.FullName, o.CreatedDate);
}

public record FollowUpDto(int FollowUpId, int? CustomerId, int? LeadId, DateTime FollowUpDate, string FollowUpType,
    string Subject, string? Remarks, string Status, bool IsOverdue, string? AssignedTo)
{
    public static FollowUpDto From(FollowUp f) => new(f.FollowUpId, f.CustomerId, f.LeadId, f.FollowUpDate,
        f.FollowUpType.ToString(), f.Subject, f.Remarks, f.Status.ToString(), f.IsOverdue, f.AssignedTo);
}

public record ApiError(int Status, string Message, IDictionary<string, string[]>? Errors = null);

// Request DTOs (validated at the API boundary).

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class CustomerRequest
{
    [Required, StringLength(100, MinimumLength = 2), RegularExpression(V.NameRegex, ErrorMessage = V.NameMessage)]
    public string CustomerName { get; set; } = "";

    [Required, EmailAddress(ErrorMessage = V.EmailMessage), StringLength(150)]
    public string Email { get; set; } = "";

    [Required, RegularExpression(V.PhoneRegex, ErrorMessage = V.PhoneMessage)]
    public string Phone { get; set; } = "";

    [StringLength(150)] public string? CompanyName { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(60)] public string? City { get; set; }
    [StringLength(60)] public string? State { get; set; }

    [EnumDataType(typeof(CustomerStatus))]
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    public string? AssignedTo { get; set; }
}

public class LeadRequest
{
    [Required, StringLength(100, MinimumLength = 2), RegularExpression(V.NameRegex, ErrorMessage = V.NameMessage)]
    public string LeadName { get; set; } = "";

    [EmailAddress(ErrorMessage = V.EmailMessage), StringLength(150)]
    public string? Email { get; set; }

    [RegularExpression(V.PhoneRegex, ErrorMessage = V.PhoneMessage)]
    public string? Phone { get; set; }

    [StringLength(150)] public string? CompanyName { get; set; }

    [Required] public string Source { get; set; } = "Website";

    [EnumDataType(typeof(LeadPriority))]
    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    [Range(typeof(decimal), "0", "100000000", ErrorMessage = "Expected Value must be between 0 and 10,00,00,000.")]
    public decimal ExpectedValue { get; set; }

    public string? AssignedTo { get; set; }
}

public class OpportunityRequest
{
    [Required, StringLength(150, MinimumLength = 3)]
    public string OpportunityName { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Customer is required.")]
    public int CustomerId { get; set; }

    [GreaterThan(0, V.AmountMessage)]
    public decimal Amount { get; set; }

    [EnumDataType(typeof(OpportunityStage))]
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    [Range(0, 100, ErrorMessage = V.ProbabilityMessage)]
    public int Probability { get; set; }

    [Required]
    [NotPastDate(V.CloseDateMessage, IgnoreWhenProperty = nameof(Stage), IgnoreValues = new[] { "Won", "Lost" })]
    public DateTime ExpectedCloseDate { get; set; }

    public string? AssignedTo { get; set; }
}

public class FollowUpRequest
{
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }

    [Required, NotPastDate(V.FollowUpDateMessage)]
    public DateTime FollowUpDate { get; set; }

    [EnumDataType(typeof(FollowUpType))]
    public FollowUpType FollowUpType { get; set; } = FollowUpType.Call;

    [Required, StringLength(150, MinimumLength = 3)]
    public string Subject { get; set; } = "";

    [StringLength(1000)] public string? Remarks { get; set; }

    public string? AssignedTo { get; set; }
}
