namespace SmartSales.Models;

public enum CustomerStatus { Active, Prospect, Inactive }
public enum LeadStatus { New, Contacted, Qualified, Unqualified, Converted, Lost }
public enum LeadPriority { Low, Medium, High }
public enum OpportunityStage { Qualification, Proposal, Negotiation, Won, Lost }
public enum FollowUpStatus { Planned, Completed, Missed, Cancelled }
public enum FollowUpType { Call, Meeting, Email, Visit }
public enum ActivityType { Call, Meeting, Email, Task }
public enum ActivityStatus { Planned, Completed, Cancelled }

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Sales = "SalesExecutive";
    public const string AdminOrManager = Admin + "," + Manager;
    public static readonly string[] All = { Admin, Manager, Sales };
}

public static class LeadSources
{
    public static readonly string[] All = { "Website", "Referral", "Email Campaign", "Cold Call", "Social Media", "Event", "Other" };
}

/// <summary>Allowed lead status transitions (workflow). Converted is only reachable through the Convert action.</summary>
public static class LeadWorkflow
{
    private static readonly Dictionary<LeadStatus, LeadStatus[]> Allowed = new()
    {
        [LeadStatus.New] = new[] { LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Contacted] = new[] { LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Qualified] = new[] { LeadStatus.Contacted, LeadStatus.Lost },
        [LeadStatus.Unqualified] = new[] { LeadStatus.Contacted, LeadStatus.Lost },
        [LeadStatus.Converted] = Array.Empty<LeadStatus>(),
        [LeadStatus.Lost] = Array.Empty<LeadStatus>(),
    };

    public static bool CanMove(LeadStatus from, LeadStatus to) => from == to || Allowed[from].Contains(to);

    public static IEnumerable<LeadStatus> NextStatuses(LeadStatus from) => new[] { from }.Concat(Allowed[from]);

    public static bool IsOpen(LeadStatus s) => s is not (LeadStatus.Converted or LeadStatus.Lost or LeadStatus.Unqualified);
}
