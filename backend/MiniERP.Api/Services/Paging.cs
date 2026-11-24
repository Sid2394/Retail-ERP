using System.Reflection;

namespace MiniERP.Api.Services;

public class BusinessException(string message, int status = 400) : Exception(message) { public int Status { get; } = status; }

public static class DtoMapper
{
    public static TDto ToDto<TE, TDto>(TE e) where TDto : new()
    {
        var d = new TDto();
        foreach (var p in typeof(TDto).GetProperties())
        {
            var sp = typeof(TE).GetProperty(p.Name);
            if (sp != null && p.CanWrite && sp.PropertyType == p.PropertyType) p.SetValue(d, sp.GetValue(e));
        }
        return d;
    }
    public static void Apply<TE, TDto>(TDto d, TE e)
    {
        foreach (var p in typeof(TDto).GetProperties())
        {
            if (p.Name == "Id") continue;
            var tp = typeof(TE).GetProperty(p.Name);
            if (tp != null && tp.CanWrite && tp.PropertyType == p.PropertyType) tp.SetValue(e, p.GetValue(d));
        }
    }
}

public static class Paging
{
    static int Clamp(PageQuery pq) { if (pq.Page < 1) pq.Page = 1; if (pq.PageSize < 1) pq.PageSize = 20; if (pq.PageSize > 100000) pq.PageSize = 100000; return (pq.Page - 1) * pq.PageSize; }

    public static async Task<PagedResult<TO>> Docs<T, TO>(IQueryable<T> q, PageQuery pq, Func<T, TO> map) where T : DocEntity
    {
        if (!string.IsNullOrWhiteSpace(pq.Search)) { var s = pq.Search.Trim(); q = q.Where(x => x.Number.Contains(s) || (x.Notes != null && x.Notes.Contains(s))); }
        if (!string.IsNullOrWhiteSpace(pq.Status)) q = q.Where(x => x.Status == pq.Status);
        if (pq.From.HasValue) q = q.Where(x => x.Date >= pq.From.Value.Date);
        if (pq.To.HasValue) q = q.Where(x => x.Date <= pq.To.Value.Date);
        var total = await q.CountAsync();
        var asc = string.Equals(pq.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<T> o = (pq.SortBy ?? "").ToLowerInvariant() switch
        {
            "number" => asc ? q.OrderBy(x => x.Number) : q.OrderByDescending(x => x.Number),
            "status" => asc ? q.OrderBy(x => x.Status) : q.OrderByDescending(x => x.Status),
            _ => asc ? q.OrderBy(x => x.Date).ThenBy(x => x.Id) : q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
        };
        var skip = Clamp(pq);
        var items = await o.Skip(skip).Take(pq.PageSize).ToListAsync();
        return new PagedResult<TO> { Items = items.Select(map).ToList(), Total = total, Page = pq.Page, PageSize = pq.PageSize };
    }

    public static async Task<TO> One<T, TO>(IQueryable<T> q, int id, Func<T, TO> map) where T : BaseEntity
    {
        var e = await q.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new BusinessException("Record not found", 404);
        return map(e);
    }

    /// <summary>In-memory search/sort/page for report rows (works with anonymous types). Adds a summary of decimal columns.</summary>
    public static PagedResult<T> Rows<T>(IEnumerable<T> rows, PageQuery pq)
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var list = rows.ToList();
        if (!string.IsNullOrWhiteSpace(pq.Search))
        {
            var s = pq.Search.Trim();
            list = list.Where(r => props.Any(p => (p.GetValue(r)?.ToString() ?? "").Contains(s, StringComparison.OrdinalIgnoreCase))).ToList();
        }
        var summary = new Dictionary<string, decimal>();
        foreach (var p in props.Where(p => p.PropertyType == typeof(decimal))) summary[p.Name] = list.Sum(r => (decimal)p.GetValue(r)!);
        var sort = props.FirstOrDefault(p => string.Equals(p.Name, pq.SortBy, StringComparison.OrdinalIgnoreCase));
        if (sort != null)
            list = (string.Equals(pq.SortDir, "desc", StringComparison.OrdinalIgnoreCase) ? list.OrderByDescending(r => sort.GetValue(r)) : list.OrderBy(r => sort.GetValue(r))).ToList();
        var skip = Clamp(pq);
        return new PagedResult<T> { Items = list.Skip(skip).Take(pq.PageSize).ToList(), Total = list.Count, Page = pq.Page, PageSize = pq.PageSize, Summary = summary };
    }
}
