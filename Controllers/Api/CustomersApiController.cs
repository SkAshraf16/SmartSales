using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/customers")]
[Produces("application/json")]
public class CustomersApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CrmService _crm;

    public CustomersApiController(AppDbContext db, CrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string? q, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Customers.ForUser(User).Include(c => c.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.CustomerName.Contains(q) || c.Email.Contains(q) || c.Phone.Contains(q));
        Response.Headers["X-Total-Count"] = (await query.CountAsync()).ToString();
        var list = await query.OrderBy(c => c.CustomerId).Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(list.Select(CustomerDto.From));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cust = await _db.Customers.ForUser(User).Include(x => x.AssignedUser).FirstOrDefaultAsync(x => x.CustomerId == id);
        return cust == null ? NotFound(new ApiError(404, "Customer not found.")) : Ok(CustomerDto.From(cust));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CustomerRequest req)
    {
        var dup = await _crm.CheckCustomerDuplicatesAsync(req.Email, req.Phone);
        if (dup.Count > 0)
            return Conflict(new ApiError(409, "Customer already exists.", dup.ToDictionary(d => d.Key, d => new[] { d.Value })));
        var (owner, err) = await _crm.ResolveAssigneeAsync(User, req.AssignedTo);
        if (err != null) return BadRequest(new ApiError(400, err));

        var cust = new Customer
        {
            CustomerCode = await _crm.NextCustomerCodeAsync(),
            CustomerName = req.CustomerName.Trim(),
            Email = req.Email.Trim().ToLowerInvariant(),
            Phone = req.Phone.Trim(),
            CompanyName = req.CompanyName,
            Address = req.Address,
            City = req.City,
            State = req.State,
            Status = req.Status,
            AssignedTo = owner,
            CreatedBy = User.Identity?.Name
        };
        _db.Customers.Add(cust);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = cust.CustomerId }, CustomerDto.From(cust));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CustomerRequest req)
    {
        var cust = await _db.Customers.ForUser(User).FirstOrDefaultAsync(x => x.CustomerId == id);
        if (cust == null) return NotFound(new ApiError(404, "Customer not found."));
        var dup = await _crm.CheckCustomerDuplicatesAsync(req.Email, req.Phone, id);
        if (dup.Count > 0)
            return Conflict(new ApiError(409, "Customer already exists.", dup.ToDictionary(d => d.Key, d => new[] { d.Value })));

        if (!User.IsSalesExecutive() && !string.IsNullOrEmpty(req.AssignedTo))
        {
            var (owner, err) = await _crm.ResolveAssigneeAsync(User, req.AssignedTo);
            if (err != null) return BadRequest(new ApiError(400, err));
            cust.AssignedTo = owner;
        }
        cust.CustomerName = req.CustomerName.Trim();
        cust.Email = req.Email.Trim().ToLowerInvariant();
        cust.Phone = req.Phone.Trim();
        cust.CompanyName = req.CompanyName;
        cust.Address = req.Address;
        cust.City = req.City;
        cust.State = req.State;
        cust.Status = req.Status;
        cust.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(CustomerDto.From(cust));
    }

    // if customer has opportunities we only make it inactive
    [HttpDelete("{id:int}"), Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var cust = await _db.Customers.FindAsync(id);
        if (cust == null) return NotFound(new ApiError(404, "Customer not found."));
        if (await _db.Opportunities.AnyAsync(o => o.CustomerId == id))
        {
            cust.Status = CustomerStatus.Inactive;
            cust.ModifiedDate = DateTime.Now;
        }
        else _db.Customers.Remove(cust);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
