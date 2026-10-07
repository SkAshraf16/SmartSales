using System.Globalization;

namespace SmartSales.Services;

public static class Fmt
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    public static string Inr(decimal amt) => amt.ToString("C0", India);

    public static string Dt(DateTime d) => d.ToString("dd-MMM-yyyy");

    public static string Dt(DateTime? d) => d == null ? "-" : d.Value.ToString("dd-MMM-yyyy");

    public static string DtTm(DateTime d) => d.ToString("dd-MMM-yyyy hh:mm tt");

    public static string Badge(string status) => status switch
    {
        "Active" or "Won" or "Completed" or "Converted" or "Qualified" => "bg-success",
        "Lost" or "Inactive" or "Missed" or "Cancelled" or "Unqualified" or "Failure" => "bg-danger",
        "New" or "Planned" or "Qualification" => "bg-primary",
        "Contacted" or "Proposal" or "Prospect" => "bg-info text-dark",
        "Negotiation" or "High" => "bg-warning text-dark",
        _ => "bg-secondary"
    };
}
