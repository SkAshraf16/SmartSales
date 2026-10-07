using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly AppDbContext _db;
    private readonly CrmService _crm;

    public CustomersController(AppDbContext db, CrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    public async Task<IActionResult> Index(string? q, CustomerStatus? status, string sort = "newest", int page = 1)
    {
        var query = _db.Customers.ForUser(User).Include(c => c.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(c => c.CustomerName.Contains(q) || c.Email.Contains(q) || c.Phone.Contains(q)
                                     || (c.CompanyName != null && c.CompanyName.Contains(q)) || c.CustomerCode.Contains(q));
        }
        if (status != null) query = query.Where(c => c.Status == status);
        query = sort switch
        {
            "name" => query.OrderBy(c => c.CustomerName),
            "oldest" => query.OrderBy(c => c.CreatedDate),
            _ => query.OrderByDescending(c => c.CreatedDate)
        };

        ViewBag.Q = q;
        ViewBag.Status = status;
        ViewBag.Sort = sort;
        return View(await query.ToPagedAsync(page));
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await _db.Customers.ForUser(User)
            .Include(c => c.AssignedUser)
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer == null) return NotFound();

        ViewBag.History = await _db.AuditLogs
            .Where(a => a.EntityName == nameof(Customer) && a.RecordId == id.ToString())
            .OrderByDescending(a => a.CreatedDate).Take(20).ToListAsync();
        return View(customer);
    }

    public async Task<IActionResult> Create()
    {
        await LoadListsAsync();
        return View("Form", new CustomerFormVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormVm vm)
    {
        await ValidateAsync(vm);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", vm);
        }

        var customer = new Customer
        {
            CustomerCode = await _crm.NextCustomerCodeAsync(),
            CreatedBy = User.Identity?.Name,
            AssignedTo = assignee,
        };
        Map(vm, customer);
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Customer {customer.CustomerName} created.";
        return RedirectToAction(nameof(Details), new { id = customer.CustomerId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var c = await _db.Customers.ForUser(User).FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return NotFound();
        await LoadListsAsync();
        return View("Form", new CustomerFormVm
        {
            CustomerId = c.CustomerId, CustomerName = c.CustomerName, Email = c.Email, Phone = c.Phone,
            CompanyName = c.CompanyName, Address = c.Address, City = c.City, State = c.State, Status = c.Status,
            Notes = c.Notes, AssignedTo = c.AssignedTo
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerFormVm vm)
    {
        if (id != vm.CustomerId) return BadRequest();
        // Ownership check on the server: a tampered id for someone else's record returns 404.
        var customer = await _db.Customers.ForUser(User).FirstOrDefaultAsync(x => x.CustomerId == id);
        if (customer == null) return NotFound();

        await ValidateAsync(vm);
        var (assignee, assignError) = await _crm.ResolveAssigneeAsync(User, vm.AssignedTo ?? customer.AssignedTo);
        if (assignError != null) ModelState.AddModelError(nameof(vm.AssignedTo), assignError);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", vm);
        }

        Map(vm, customer);
        if (!User.IsSalesExecutive()) customer.AssignedTo = assignee;
        customer.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Customer updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound();
        if (await _db.Opportunities.AnyAsync(o => o.CustomerId == id))
        {
            // Customers with sales history are deactivated instead of deleted.
            customer.Status = CustomerStatus.Inactive;
            customer.ModifiedDate = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Warning"] = "Customer has opportunities, so it was deactivated instead of deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Customer deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(CustomerFormVm vm)
    {
        if (!ModelState.IsValid) return;
        foreach (var (field, message) in await _crm.CheckCustomerDuplicatesAsync(vm.Email, vm.Phone, vm.CustomerId))
            ModelState.AddModelError(field, message);
    }

    private static void Map(CustomerFormVm vm, Customer c)
    {
        c.CustomerName = vm.CustomerName.Trim();
        c.Email = vm.Email.Trim().ToLowerInvariant();
        c.Phone = vm.Phone.Trim();
        c.CompanyName = vm.CompanyName?.Trim();
        c.Address = vm.Address?.Trim();
        c.City = vm.City?.Trim();
        c.State = vm.State?.Trim();
        c.Status = vm.Status;
        c.Notes = vm.Notes?.Trim();
    }

    private async Task LoadListsAsync() => ViewBag.Users = await _crm.AssignableUsersAsync();
}
