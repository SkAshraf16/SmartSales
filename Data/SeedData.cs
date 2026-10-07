using SmartSales.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Data;

/// <summary>Creates roles, demo users and sample CRM data on first run. Demo credentials are listed in README.md.</summary>
public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await EnsureUser(userManager, "admin@smartsales.in", "System Admin", "9000000001", "Admin@123", Roles.Admin);
        var manager = await EnsureUser(userManager, "manager@smartsales.in", "Meera Manager", "9000000002", "Manager@123", Roles.Manager);
        var sales1 = await EnsureUser(userManager, "sales1@smartsales.in", "Ravi Kumar", "9000000003", "Sales@123", Roles.Sales);
        var sales2 = await EnsureUser(userManager, "sales2@smartsales.in", "Priya Sharma", "9000000004", "Sales@123", Roles.Sales);

        if (await db.Customers.AnyAsync()) return;

        var rnd = new Random(42);
        var owners = new[] { sales1.Id, sales2.Id };
        var companies = new[] { "Tata Digital", "Infosys", "Wipro", "HCL Tech", "Reliance Retail", "Mahindra Logistics",
            "Zoho", "Freshworks", "Byju's", "Swiggy", "Zomato", "Paytm" };
        var names = new[] { "Arjun Mehta", "Sneha Reddy", "Vikram Singh", "Anjali Gupta", "Rahul Verma", "Kavya Nair",
            "Suresh Iyer", "Pooja Patel", "Aditya Rao", "Neha Joshi", "Karan Malhotra", "Divya Menon" };
        var cities = new[] { ("Hyderabad", "Telangana"), ("Bengaluru", "Karnataka"), ("Chennai", "Tamil Nadu"),
            ("Mumbai", "Maharashtra"), ("Pune", "Maharashtra"), ("Delhi", "Delhi") };

        var customers = new List<Customer>();
        for (var i = 0; i < 12; i++)
        {
            var (city, state) = cities[i % cities.Length];
            customers.Add(new Customer
            {
                CustomerCode = $"CUST-{i + 1:D4}",
                CustomerName = names[i],
                Email = names[i].ToLower().Replace(" ", ".") + "@example.com",
                Phone = $"98{rnd.Next(10000000, 99999999)}",
                CompanyName = companies[i],
                Address = $"{rnd.Next(1, 200)}, MG Road",
                City = city,
                State = state,
                Status = i % 5 == 4 ? CustomerStatus.Prospect : CustomerStatus.Active,
                AssignedTo = owners[i % 2],
                CreatedBy = admin.Email,
                CreatedDate = DateTime.Today.AddDays(-rnd.Next(5, 300)),
            });
        }
        db.Customers.AddRange(customers);

        var leadStatuses = new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Lost,
            LeadStatus.Converted, LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.New };
        var leadNames = new[] { "Rohit Bansal", "Isha Kapoor", "Manoj Pillai", "Ritu Saxena", "Amit Chawla",
            "Nisha Agarwal", "Gaurav Sinha", "Swati Desai", "Harish Kulkarni", "Tanvi Bhatt" };
        for (var i = 0; i < leadNames.Length; i++)
        {
            db.Leads.Add(new Lead
            {
                LeadCode = $"LEAD-{i + 1:D4}",
                LeadName = leadNames[i],
                Email = leadNames[i].ToLower().Replace(" ", ".") + "@prospect.in",
                Phone = $"97{rnd.Next(10000000, 99999999)}",
                CompanyName = $"{leadNames[i].Split(' ')[1]} Enterprises",
                Source = LeadSources.All[i % LeadSources.All.Length],
                Status = leadStatuses[i],
                Priority = (LeadPriority)(i % 3),
                ExpectedValue = rnd.Next(50, 900) * 1000,
                AssignedTo = owners[i % 2],
                CreatedDate = DateTime.Today.AddDays(-rnd.Next(1, 120)),
            });
        }
        await db.SaveChangesAsync();

        var stages = new[] { OpportunityStage.Qualification, OpportunityStage.Proposal, OpportunityStage.Negotiation,
            OpportunityStage.Won, OpportunityStage.Lost };
        var probabilities = new Dictionary<OpportunityStage, int>
        {
            [OpportunityStage.Qualification] = 20, [OpportunityStage.Proposal] = 50, [OpportunityStage.Negotiation] = 75,
            [OpportunityStage.Won] = 100, [OpportunityStage.Lost] = 0
        };
        for (var i = 0; i < 18; i++)
        {
            var stage = stages[i % stages.Length];
            var closed = stage is OpportunityStage.Won or OpportunityStage.Lost;
            var customer = customers[i % customers.Count];
            db.Opportunities.Add(new Opportunity
            {
                OpportunityName = $"{customer.CompanyName} - {(i % 2 == 0 ? "CRM Licences" : "Data Services")}",
                CustomerId = customer.CustomerId,
                Amount = rnd.Next(100, 2500) * 1000,
                Stage = stage,
                Probability = probabilities[stage],
                Status = Opportunity.StatusFor(stage),
                ExpectedCloseDate = closed ? DateTime.Today.AddDays(-rnd.Next(5, 300)) : DateTime.Today.AddDays(rnd.Next(10, 120)),
                ClosedDate = closed ? DateTime.Today.AddDays(-rnd.Next(5, 300)) : null,
                Source = LeadSources.All[i % LeadSources.All.Length],
                AssignedTo = customer.AssignedTo,
                CreatedDate = DateTime.Today.AddDays(-rnd.Next(30, 320)),
            });
        }

        var subjects = new[] { "Product demo", "Pricing discussion", "Contract review", "Requirement call", "Quarterly check-in" };
        for (var i = 0; i < 10; i++)
        {
            var customer = customers[i];
            db.FollowUps.Add(new FollowUp
            {
                CustomerId = customer.CustomerId,
                FollowUpDate = DateTime.Today.AddDays(i - 3),
                FollowUpType = (FollowUpType)(i % 4),
                Subject = subjects[i % subjects.Length],
                Remarks = "Seed follow-up",
                Status = i < 2 ? FollowUpStatus.Completed : FollowUpStatus.Planned,
                CompletedDate = i < 2 ? DateTime.Today.AddDays(i - 3) : null,
                AssignedTo = customer.AssignedTo,
            });
            db.Activities.Add(new Activity
            {
                ActivityType = (ActivityType)(i % 4),
                Subject = subjects[(i + 2) % subjects.Length],
                Description = "Seed activity",
                ActivityDate = DateTime.Today.AddDays(-i),
                CustomerId = customer.CustomerId,
                AssignedTo = customer.AssignedTo,
                Status = i % 3 == 0 ? ActivityStatus.Planned : ActivityStatus.Completed,
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUser(UserManager<ApplicationUser> users, string email, string name,
        string phone, string password, string role)
    {
        var user = await users.FindByEmailAsync(email);
        if (user != null) return user;

        user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = name, PhoneNumber = phone, EmailConfirmed = true, IsActive = true
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
        return user;
    }
}
