using SmartSales.Data;
using SmartSales.Models;
using SmartSales.Services;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Controllers.Api;

// leads, opportunities, follow-ups and pipeline report apis
[ApiController]
[Authorize]
[Route("api")]
[Produces("application/json")]
public class SalesApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CrmService _crm;

    public SalesApiController(AppDbContext db, CrmService crm)
    {
        _db = db;
        _crm = crm;
    }

    [HttpGet("leads")]
    public async Task<IActionResult> GetLeads(string? q, LeadStatus? status)
    {
        var query = _db.Leads.ForUser(User).Include(l => l.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(l => l.LeadName.Contains(q) || (l.CompanyName != null && l.CompanyName.Contains(q)));
        if (status != null) query = query.Where(l => l.Status == status);
        var list = await query.OrderByDescending(l => l.CreatedDate).Take(200).ToListAsync();
        return Ok(list.Select(LeadDto.From));
    }

    [HttpGet("leads/{id:int}")]
    public async Task<IActionResult> GetLead(int id)
    {
        var lead = await _db.Leads.ForUser(User).Include(l => l.AssignedUser).FirstOrDefaultAsync(l => l.LeadId == id);
        return lead == null ? NotFound(new ApiError(404, "Lead not found.")) : Ok(LeadDto.From(lead));
    }

    [HttpPost("leads")]
    public async Task<IActionResult> CreateLead(LeadRequest req)
    {
        if (!LeadSources.All.Contains(req.Source)) return BadRequest(new ApiError(400, "Invalid lead source."));
        var (owner, err) = await _crm.ResolveAssigneeAsync(User, req.AssignedTo);
        if (err != null) return BadRequest(new ApiError(400, err));

        var lead = new Lead
        {
            LeadCode = await _crm.NextLeadCodeAsync(),
            LeadName = req.LeadName.Trim(),
            Email = req.Email?.Trim().ToLowerInvariant(),
            Phone = req.Phone,
            CompanyName = req.CompanyName,
            Source = req.Source,
            Priority = req.Priority,
            ExpectedValue = req.ExpectedValue,
            Status = LeadStatus.New,
            AssignedTo = owner
        };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetLead), new { id = lead.LeadId }, LeadDto.From(lead));
    }

    [HttpGet("opportunities")]
    public async Task<IActionResult> GetOpportunities(string? q, OpportunityStage? stage)
    {
        var query = _db.Opportunities.ForUser(User).Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(o => o.OpportunityName.Contains(q));
        if (stage != null) query = query.Where(o => o.Stage == stage);
        var list = await query.OrderBy(o => o.ExpectedCloseDate).Take(200).ToListAsync();
        return Ok(list.Select(OpportunityDto.From));
    }

    [HttpGet("opportunities/{id:int}")]
    public async Task<IActionResult> GetOpportunity(int id)
    {
        var opp = await _db.Opportunities.ForUser(User).Include(o => o.Customer).Include(o => o.AssignedUser)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);
        return opp == null ? NotFound(new ApiError(404, "Opportunity not found.")) : Ok(OpportunityDto.From(opp));
    }

    [HttpPost("opportunities")]
    public async Task<IActionResult> CreateOpportunity(OpportunityRequest req)
    {
        if (req.Stage is OpportunityStage.Won or OpportunityStage.Lost)
            return BadRequest(new ApiError(400, "New opportunity must be in an open stage."));
        if (!await _crm.CustomerVisibleAsync(User, req.CustomerId))
            return BadRequest(new ApiError(400, "Customer not found or not accessible."));
        var (owner, err) = await _crm.ResolveAssigneeAsync(User, req.AssignedTo);
        if (err != null) return BadRequest(new ApiError(400, err));

        var opp = new Opportunity
        {
            OpportunityName = req.OpportunityName.Trim(),
            CustomerId = req.CustomerId,
            Amount = req.Amount,
            Stage = req.Stage,
            Probability = req.Probability,
            ExpectedCloseDate = req.ExpectedCloseDate.Date,
            Status = "Open",
            AssignedTo = owner
        };
        _db.Opportunities.Add(opp);
        await _db.SaveChangesAsync();
        await _db.Entry(opp).Reference(o => o.Customer).LoadAsync();
        return CreatedAtAction(nameof(GetOpportunity), new { id = opp.OpportunityId }, OpportunityDto.From(opp));
    }

    [HttpGet("followups")]
    public async Task<IActionResult> GetFollowUps(FollowUpStatus? status)
    {
        var query = _db.FollowUps.ForUser(User);
        if (status != null) query = query.Where(f => f.Status == status);
        var list = await query.OrderBy(f => f.FollowUpDate).Take(200).ToListAsync();
        return Ok(list.Select(FollowUpDto.From));
    }

    [HttpPost("followups")]
    public async Task<IActionResult> CreateFollowUp(FollowUpRequest req)
    {
        if (req.CustomerId == null && req.LeadId == null)
            return BadRequest(new ApiError(400, "CustomerId or LeadId is required."));
        if (req.CustomerId != null && !await _crm.CustomerVisibleAsync(User, req.CustomerId.Value))
            return BadRequest(new ApiError(400, "Customer not found or not accessible."));
        if (req.LeadId != null && !await _crm.LeadVisibleAsync(User, req.LeadId.Value))
            return BadRequest(new ApiError(400, "Lead not found or not accessible."));
        var (owner, err) = await _crm.ResolveAssigneeAsync(User, req.AssignedTo);
        if (err != null) return BadRequest(new ApiError(400, err));

        var fu = new FollowUp
        {
            CustomerId = req.CustomerId,
            LeadId = req.LeadId,
            FollowUpDate = req.FollowUpDate.Date,
            FollowUpType = req.FollowUpType,
            Subject = req.Subject.Trim(),
            Remarks = req.Remarks,
            Status = FollowUpStatus.Planned,
            AssignedTo = owner
        };
        _db.FollowUps.Add(fu);
        await _db.SaveChangesAsync();
        return Created($"/api/followups/{fu.FollowUpId}", FollowUpDto.From(fu));
    }

    [HttpGet("reports/pipeline")]
    public async Task<IActionResult> Pipeline()
    {
        var opps = await _db.Opportunities.ForUser(User).Include(o => o.AssignedUser).ToListAsync();
        var byStage = opps.GroupBy(o => o.Stage).Select(g => new
        {
            Stage = g.Key.ToString(),
            Count = g.Count(),
            Amount = g.Sum(o => o.Amount),
            Weighted = g.Sum(o => o.WeightedAmount)
        });
        var byOwner = opps.Where(o => o.IsActive).GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned").Select(g => new
        {
            Owner = g.Key,
            Count = g.Count(),
            Amount = g.Sum(o => o.Amount),
            Weighted = g.Sum(o => o.WeightedAmount)
        });
        return Ok(new { byStage, byOwner });
    }
}
