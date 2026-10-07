using System.Security.Claims;
using SmartSales.Data;
using SmartSales.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Services;

/// <summary>Business rules shared by the MVC controllers and the REST API.</summary>
public class CrmService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;

    public CrmService(AppDbContext db, UserManager<ApplicationUser> users, AuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<string> NextCustomerCodeAsync() =>
        $"CUST-{(await _db.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 0) + 1:D4}";

    public async Task<string> NextLeadCodeAsync() =>
        $"LEAD-{(await _db.Leads.MaxAsync(l => (int?)l.LeadId) ?? 0) + 1:D4}";

    /// <summary>Duplicate-customer rule: email and phone must both be unique.</summary>
    public async Task<Dictionary<string, string>> CheckCustomerDuplicatesAsync(string email, string phone, int excludeId = 0)
    {
        var errors = new Dictionary<string, string>();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await _db.Customers.AnyAsync(c => c.Email == normalizedEmail && c.CustomerId != excludeId))
            errors["Email"] = "A customer with this email already exists.";
        if (await _db.Customers.AnyAsync(c => c.Phone == phone.Trim() && c.CustomerId != excludeId))
            errors["Phone"] = "A customer with this phone number already exists.";
        return errors;
    }

    /// <summary>
    /// Sales Executives can only assign records to themselves. Admin/Manager may assign to any active user;
    /// the referenced user is validated (foreign-key validation).
    /// </summary>
    public async Task<(string? UserId, string? Error)> ResolveAssigneeAsync(ClaimsPrincipal user, string? requested)
    {
        if (user.IsSalesExecutive() || string.IsNullOrWhiteSpace(requested)) return (user.UserId(), null);
        var target = await _users.FindByIdAsync(requested);
        if (target == null || !target.IsActive) return (null, "Selected user does not exist or is inactive.");
        return (target.Id, null);
    }

    public async Task<List<SelectListItem>> AssignableUsersAsync()
    {
        var sales = await _users.GetUsersInRoleAsync(Roles.Sales);
        var managers = await _users.GetUsersInRoleAsync(Roles.Manager);
        return sales.Concat(managers).Where(u => u.IsActive).DistinctBy(u => u.Id).OrderBy(u => u.FullName)
            .Select(u => new SelectListItem(u.FullName, u.Id)).ToList();
    }

    public async Task<List<SelectListItem>> CustomerOptionsAsync(ClaimsPrincipal user) =>
        await _db.Customers.ForUser(user).OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem(c.CustomerName + " (" + c.CustomerCode + ")", c.CustomerId.ToString()))
            .ToListAsync();

    public async Task<List<SelectListItem>> LeadOptionsAsync(ClaimsPrincipal user) =>
        await _db.Leads.ForUser(user).Where(l => l.Status != LeadStatus.Converted).OrderBy(l => l.LeadName)
            .Select(l => new SelectListItem(l.LeadName + " (" + l.LeadCode + ")", l.LeadId.ToString()))
            .ToListAsync();

    /// <summary>Verifies the referenced customer exists and is visible to the user.</summary>
    public Task<bool> CustomerVisibleAsync(ClaimsPrincipal user, int customerId) =>
        _db.Customers.ForUser(user).AnyAsync(c => c.CustomerId == customerId);

    public Task<bool> LeadVisibleAsync(ClaimsPrincipal user, int leadId) =>
        _db.Leads.ForUser(user).AnyAsync(l => l.LeadId == leadId);

    /// <summary>
    /// Lead-to-Customer workflow: a Qualified lead becomes a Customer (or is linked to the existing customer with the
    /// same email/phone) plus an Opportunity in the Qualification stage. The conversion is audited.
    /// </summary>
    public async Task<(Customer? Customer, Opportunity? Opportunity, string? Error)> ConvertLeadAsync(Lead lead, ClaimsPrincipal user)
    {
        if (lead.Status != LeadStatus.Qualified)
            return (null, null, "Only Qualified leads can be converted.");
        if (string.IsNullOrWhiteSpace(lead.Email) || string.IsNullOrWhiteSpace(lead.Phone))
            return (null, null, "Lead must have an email and phone number before conversion.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var email = lead.Email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email || c.Phone == lead.Phone);
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerCode = await NextCustomerCodeAsync(),
                CustomerName = lead.LeadName,
                Email = email,
                Phone = lead.Phone.Trim(),
                CompanyName = lead.CompanyName,
                Status = CustomerStatus.Active,
                AssignedTo = lead.AssignedTo,
                CreatedBy = user.Identity?.Name,
                Notes = $"Converted from lead {lead.LeadCode}",
            };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }

        var opportunity = new Opportunity
        {
            OpportunityName = $"{lead.CompanyName ?? lead.LeadName} - {lead.Source}",
            CustomerId = customer.CustomerId,
            LeadId = lead.LeadId,
            Amount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 1,
            Stage = OpportunityStage.Qualification,
            Probability = 20,
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            Status = "Open",
            Source = lead.Source,
            AssignedTo = lead.AssignedTo,
        };
        _db.Opportunities.Add(opportunity);

        lead.Status = LeadStatus.Converted;
        lead.ConvertedCustomerId = customer.CustomerId;
        lead.ConvertedDate = DateTime.Now;
        lead.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Convert", nameof(Lead), lead.LeadId.ToString(),
            newValue: $"{{\"CustomerId\":\"{customer.CustomerId}\",\"OpportunityId\":\"{opportunity.OpportunityId}\"}}");
        await tx.CommitAsync();
        return (customer, opportunity, null);
    }
}
