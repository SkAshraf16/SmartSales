using System.ComponentModel.DataAnnotations;
using SmartSales.Models;
using SmartSales.Validation;
using V = SmartSales.Validation.ValidationRules;

namespace SmartSales.ViewModels;

public class CustomerFormVm
{
    public int CustomerId { get; set; }

    [Display(Name = "Customer Name")]
    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Customer Name must be 2-100 characters.")]
    [RegularExpression(V.NameRegex, ErrorMessage = V.NameMessage)]
    public string CustomerName { get; set; } = "";

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = V.EmailMessage)]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", ErrorMessage = V.EmailMessage)]
    [StringLength(150)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(V.PhoneRegex, ErrorMessage = V.PhoneMessage)]
    public string Phone { get; set; } = "";

    [Display(Name = "Company Name"), StringLength(150)]
    public string? CompanyName { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(60)]
    public string? City { get; set; }

    [StringLength(60)]
    public string? State { get; set; }

    [Required]
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class LeadFormVm
{
    public int LeadId { get; set; }

    [Display(Name = "Lead Name")]
    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Lead Name must be 2-100 characters.")]
    [RegularExpression(V.NameRegex, ErrorMessage = V.NameMessage)]
    public string LeadName { get; set; } = "";

    [EmailAddress(ErrorMessage = V.EmailMessage)]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", ErrorMessage = V.EmailMessage)]
    [StringLength(150)]
    public string? Email { get; set; }

    [RegularExpression(V.PhoneRegex, ErrorMessage = V.PhoneMessage)]
    public string? Phone { get; set; }

    [Display(Name = "Company Name"), StringLength(150)]
    public string? CompanyName { get; set; }

    [Required(ErrorMessage = "Lead Source is required.")]
    public string Source { get; set; } = "Website";

    [Required(ErrorMessage = "Lead Status is required.")]
    public LeadStatus Status { get; set; } = LeadStatus.New;

    [Required]
    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    [Display(Name = "Expected Value")]
    [Required(ErrorMessage = "Expected Value is required.")]
    [Range(typeof(decimal), "0", "100000000", ErrorMessage = "Expected Value must be between 0 and 10,00,00,000.")]
    public decimal ExpectedValue { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class OpportunityFormVm
{
    public int OpportunityId { get; set; }

    [Display(Name = "Opportunity Name")]
    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Opportunity Name must be 3-150 characters.")]
    public string OpportunityName { get; set; } = "";

    [Display(Name = "Customer")]
    [Required(ErrorMessage = "Customer is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Customer is required.")]
    public int CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [GreaterThan(0, V.AmountMessage)]
    [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Amount is too large.")]
    public decimal Amount { get; set; }

    [Required]
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    [Required(ErrorMessage = "Probability is required.")]
    [Range(0, 100, ErrorMessage = V.ProbabilityMessage)]
    public int Probability { get; set; } = 10;

    [Display(Name = "Expected Close Date")]
    [Required(ErrorMessage = "Expected Close Date is required.")]
    [DataType(DataType.Date)]
    [NotPastDate(V.CloseDateMessage, IgnoreWhenProperty = nameof(Stage), IgnoreValues = new[] { "Won", "Lost" })]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);

    [StringLength(50)]
    public string? Source { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class FollowUpFormVm
{
    public int FollowUpId { get; set; }

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Follow-Up Date")]
    [Required(ErrorMessage = "Follow-Up Date is required.")]
    [DataType(DataType.Date)]
    [NotPastDate(V.FollowUpDateMessage, IgnoreWhenProperty = nameof(Status), IgnoreValues = new[] { "Completed", "Missed", "Cancelled" })]
    public DateTime FollowUpDate { get; set; } = DateTime.Today.AddDays(1);

    [Display(Name = "Type")]
    [Required]
    public FollowUpType FollowUpType { get; set; } = FollowUpType.Call;

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Subject must be 3-150 characters.")]
    public string Subject { get; set; } = "";

    [StringLength(1000)]
    public string? Remarks { get; set; }

    [Required]
    public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class ActivityFormVm
{
    public int ActivityId { get; set; }

    [Display(Name = "Activity Type")]
    [Required]
    public ActivityType ActivityType { get; set; } = ActivityType.Call;

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Subject must be 3-150 characters.")]
    public string Subject { get; set; } = "";

    [StringLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "Activity Date")]
    [Required(ErrorMessage = "Activity Date is required.")]
    [DataType(DataType.Date)]
    public DateTime ActivityDate { get; set; } = DateTime.Today;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Required]
    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class LoginVm
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = V.EmailMessage)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterVm
{
    [Display(Name = "Full Name")]
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2)]
    [RegularExpression(V.NameRegex, ErrorMessage = V.NameMessage)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = V.EmailMessage)]
    [StringLength(150)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(V.PhoneRegex, ErrorMessage = V.PhoneMessage)]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
        ErrorMessage = "Password must contain upper case, lower case, digit and special character.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Confirm Password")]
    [Required(ErrorMessage = "Confirm Password is required.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public class UserFormVm
{
    public string? Id { get; set; }

    [Display(Name = "Full Name")]
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2)]
    [RegularExpression(V.NameRegex, ErrorMessage = V.NameMessage)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = V.EmailMessage)]
    public string Email { get; set; } = "";

    [RegularExpression(V.PhoneRegex, ErrorMessage = V.PhoneMessage)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = Roles.Sales;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Password (new users only)")]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
        ErrorMessage = "Password must be 8+ characters with upper case, lower case, digit and special character.")]
    public string? Password { get; set; }
}

public class ResetPasswordVm
{
    public string Id { get; set; } = "";
    public string? Email { get; set; }

    [Display(Name = "New Password")]
    [Required(ErrorMessage = "New Password is required.")]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
        ErrorMessage = "Password must be 8+ characters with upper case, lower case, digit and special character.")]
    public string NewPassword { get; set; } = "";
}

public class UserListItemVm
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public int FailedCount { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class DashboardVm
{
    public string Range { get; set; } = "all";
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal PipelineValue { get; set; }
    public decimal WeightedPipeline { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }
    public int? TotalUsers { get; set; }
    public int? FailedLoginsToday { get; set; }
    public Dictionary<string, int> LeadStatusChart { get; set; } = new();
    public Dictionary<string, decimal> PipelineChart { get; set; } = new();
    public Dictionary<string, decimal> MonthlySalesChart { get; set; } = new();
    public List<FollowUp> UpcomingFollowUps { get; set; } = new();
    public List<AuditLog> RecentActivity { get; set; } = new();
}
