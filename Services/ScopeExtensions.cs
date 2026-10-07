using System.Security.Claims;
using SmartSales.Models;
using Microsoft.EntityFrameworkCore;

namespace SmartSales.Services;

public static class ScopeExtensions
{
    public static string? UserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    public static bool IsSalesExecutive(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Sales) && !user.IsInRole(Roles.Admin) && !user.IsInRole(Roles.Manager);

    /// <summary>
    /// Row-level authorization: Admin and Manager see all records (the whole sales team),
    /// Sales Executives see only records assigned to them.
    /// </summary>
    public static IQueryable<T> ForUser<T>(this IQueryable<T> query, ClaimsPrincipal user) where T : class, IAssignable
    {
        if (!user.IsSalesExecutive()) return query;
        var uid = user.UserId();
        return query.Where(x => x.AssignedTo == uid);
    }
}

public class PagedResult<T> : IPaged
{
    public List<T> Items { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public interface IPaged
{
    int Page { get; }
    int TotalPages { get; }
    int TotalCount { get; }
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize = 10)
    {
        page = Math.Max(1, page);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }
}
