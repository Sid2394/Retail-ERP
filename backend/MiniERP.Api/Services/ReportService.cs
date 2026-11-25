namespace MiniERP.Api.Services;

public class ReportService(ErpDbContext db, AccountingService acct)
{
    static DateTime? F(PageQuery q) => q.From?.Date;
    static DateTime? T(PageQuery q) => q.To?.Date;

    public async Task<PagedResult<object>> Sales(PageQuery q)
    {
        var inv = db.SalesInvoices.Include(x => x.Customer).AsQueryable();
        if (F(q) != null) inv = inv.Where(x => x.Date >= F(q)); if (T(q) != null) inv = inv.Where(x => x.Date <= T(q)); if (q.PartyId != null) inv = inv.Where(x => x.CustomerId == q.PartyId);
        var rows = (await inv.OrderByDescending(x => x.Date).ToListAsync()).Select(x => (object)new { date = x.Date, number = x.Number, customer = x.Customer!.Name, subtotal = x.Subtotal, tax = x.TaxAmount, total = x.Total, paid = x.PaidAmount, credited = x.CreditedAmount, balance = x.Total - x.PaidAmount - x.CreditedAmount, status = x.Status });
        return Rows(rows, q);
    }
    public async Task<PagedResult<object>> Purchases(PageQuery q)
    {
        var inv = db.PurchaseInvoices.Include(x => x.Supplier).AsQueryable();
        if (F(q) != null) inv = inv.Where(x => x.Date >= F(q)); if (T(q) != null) inv = inv.Where(x => x.Date <= T(q)); if (q.PartyId != null) inv = inv.Where(x => x.SupplierId == q.PartyId);
        var rows = (await inv.OrderByDescending(x => x.Date).ToListAsync()).Select(x => (object)new { date = x.Date, number = x.Number, supplierInvoice = x.SupplierInvoiceNo ?? "", supplier = x.Supplier!.Name, subtotal = x.Subtotal, tax = x.TaxAmount, total = x.Total, paid = x.PaidAmount, credited = x.CreditedAmount, balance = x.Total - x.PaidAmount - x.CreditedAmount, status = x.Status });
        return Rows(rows, q);
    }
    public async Task<PagedResult<object>> Inventory(PageQuery q)
    {
        var b = db.StockBalances.Include(x => x.Product).ThenInclude(p => p!.Category).Include(x => x.Warehouse).AsQueryable();
        if (q.WarehouseId != null) b = b.Where(x => x.WarehouseId == q.WarehouseId); if (q.ProductId != null) b = b.Where(x => x.ProductId == q.ProductId);
        var rows = (await b.ToListAsync()).OrderBy(x => x.Product!.Sku).Select(x => (object)new { sku = x.Product!.Sku, product = x.Product.Name, category = x.Product.Category?.Name ?? "", type = x.Product.ProductType, warehouse = x.Warehouse!.Name, quantity = x.Quantity, avgCost = x.Product.AverageCost, value = Math.Round(x.Quantity * x.Product.AverageCost, 2), reorderLevel = x.Product.ReorderLevel, low = x.Product.ReorderLevel > 0 && x.Quantity <= x.Product.ReorderLevel ? "Low" : "OK" });
        return Rows(rows, q);
    }
    public async Task<PagedResult<object>> StockValuation(PageQuery q)
    {
        var prods = await db.Products.Include(p => p.Category).ToListAsync();
        var bal = await db.StockBalances.GroupBy(x => x.ProductId).Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var rows = prods.OrderBy(p => p.Sku).Select(p => { var qty = bal.GetValueOrDefault(p.Id); return (object)new { sku = p.Sku, product = p.Name, category = p.Category?.Name ?? "", type = p.ProductType, quantity = qty, avgCost = p.AverageCost, value = Math.Round(qty * p.AverageCost, 2) }; });
        return Rows(rows, q);
    }
    public async Task<PagedResult<object>> Suppliers(PageQuery q)
    {
        var sup = await db.Suppliers.ToListAsync(); var inv = await db.PurchaseInvoices.ToListAsync();
        var rows = sup.OrderBy(s => s.Name).Select(s => { var i = inv.Where(x => x.SupplierId == s.Id).ToList(); return (object)new { code = s.Code, supplier = s.Name, phone = s.Phone ?? "", invoices = i.Count, invoiced = i.Sum(x => x.Total), paid = i.Sum(x => x.PaidAmount), credited = i.Sum(x => x.CreditedAmount), outstanding = i.Sum(x => x.Total - x.PaidAmount - x.CreditedAmount) }; });
        return Rows(rows, q);
    }
    public async Task<PagedResult<object>> Customers(PageQuery q)
    {
        var cus = await db.Customers.ToListAsync(); var inv = await db.SalesInvoices.ToListAsync();
        var rows = cus.OrderBy(s => s.Name).Select(s => { var i = inv.Where(x => x.CustomerId == s.Id).ToList(); return (object)new { code = s.Code, customer = s.Name, phone = s.Phone ?? "", invoices = i.Count, invoiced = i.Sum(x => x.Total), received = i.Sum(x => x.PaidAmount), credited = i.Sum(x => x.CreditedAmount), outstanding = i.Sum(x => x.Total - x.PaidAmount - x.CreditedAmount), creditLimit = s.CreditLimit }; });
        return Rows(rows, q);
    }
    public async Task<PagedResult<object>> Production(PageQuery q)
    {
        var o = db.ProductionOrders.Include(x => x.Product).AsQueryable();
        if (F(q) != null) o = o.Where(x => x.Date >= F(q)); if (T(q) != null) o = o.Where(x => x.Date <= T(q)); if (!string.IsNullOrWhiteSpace(q.Status)) o = o.Where(x => x.Status == q.Status);
        var rows = (await o.OrderByDescending(x => x.Date).ToListAsync()).Select(x => (object)new { date = x.Date, number = x.Number, product = x.Product!.Name, planned = x.Quantity, produced = x.QuantityProduced, materialCost = x.MaterialCost, laborCost = x.LaborCost, overheadCost = x.OverheadCost, totalCost = x.TotalCost, unitCost = x.UnitCost, quality = x.QualityStatus, status = x.Status });
        return Rows(rows, q);
    }
    public async Task<PagedResult<object>> Expenses(PageQuery q)
    {
        var e = db.Expenses.Include(x => x.Account).AsQueryable();
        if (F(q) != null) e = e.Where(x => x.Date >= F(q)); if (T(q) != null) e = e.Where(x => x.Date <= T(q));
        var rows = (await e.OrderByDescending(x => x.Date).ToListAsync()).Select(x => (object)new { date = x.Date, number = x.Number, account = x.Account!.Name, payee = x.Payee ?? "", description = x.Description ?? "", method = x.Method, amount = x.Amount });
        return Rows(rows, q);
    }
    public async Task<PagedResult<object>> Revenue(PageQuery q)
    {
        var rows = new List<(DateTime Date, string Source, string Ref, decimal Amount)>();
        var si = db.SalesInvoices.AsQueryable(); var inc = db.Incomes.Include(x => x.Account).AsQueryable();
        if (F(q) != null) { si = si.Where(x => x.Date >= F(q)); inc = inc.Where(x => x.Date >= F(q)); }
        if (T(q) != null) { si = si.Where(x => x.Date <= T(q)); inc = inc.Where(x => x.Date <= T(q)); }
        rows.AddRange((await si.ToListAsync()).Select(x => (x.Date, "Sales", x.Number, x.Subtotal)));
        rows.AddRange((await inc.ToListAsync()).Select(x => (x.Date, x.Account!.Name, x.Number, x.Amount)));
        return Rows(rows.OrderByDescending(r => r.Date).Select(r => (object)new { date = r.Date, source = r.Source, reference = r.Ref, amount = r.Amount }), q);
    }
    public async Task<PagedResult<object>> Receivables(PageQuery q)
    {
        var today = DateTime.UtcNow.Date;
        var inv = await db.SalesInvoices.Include(x => x.Customer).ToListAsync();
        var rows = inv.Where(x => x.Total - x.PaidAmount - x.CreditedAmount > 0.0001m).OrderBy(x => x.DueDate).Select(x => (object)new { id = x.Id, party = x.Customer!.Name, number = x.Number, date = x.Date, dueDate = x.DueDate, total = x.Total, paid = x.PaidAmount, credited = x.CreditedAmount, outstanding = x.Total - x.PaidAmount - x.CreditedAmount, daysOverdue = x.DueDate.HasValue && x.DueDate.Value < today ? (int)(today - x.DueDate.Value).TotalDays : 0, status = x.Status });
        return Rows(rows, q);
    }
    public async Task<PagedResult<object>> Payables(PageQuery q)
    {
        var today = DateTime.UtcNow.Date;
        var inv = await db.PurchaseInvoices.Include(x => x.Supplier).ToListAsync();
        var rows = inv.Where(x => x.Total - x.PaidAmount - x.CreditedAmount > 0.0001m).OrderBy(x => x.DueDate).Select(x => (object)new { id = x.Id, party = x.Supplier!.Name, number = x.Number, date = x.Date, dueDate = x.DueDate, total = x.Total, paid = x.PaidAmount, credited = x.CreditedAmount, outstanding = x.Total - x.PaidAmount - x.CreditedAmount, daysOverdue = x.DueDate.HasValue && x.DueDate.Value < today ? (int)(today - x.DueDate.Value).TotalDays : 0, status = x.Status });
        return Rows(rows, q);
    }
    static PagedResult<object> Rows(IEnumerable<object> rows, PageQuery q) => PagingObjects.Rows(rows, q);

    // ---------- Analytics (chart data) ----------
    static readonly string[] Mn = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    public static List<(int Y, int M, string Label)> Months(int n = 12)
    {
        var now = DateTime.UtcNow; var list = new List<(int, int, string)>();
        for (var i = n - 1; i >= 0; i--) { var d = new DateTime(now.Year, now.Month, 1).AddMonths(-i); list.Add((d.Year, d.Month, $"{Mn[d.Month - 1]} {d:yy}")); }
        return list;
    }
    public async Task<object> Analytics(string kind)
    {
        var months = Months(); var start = new DateTime(months[0].Y, months[0].M, 1);
        List<object> Series(IEnumerable<(DateTime Date, decimal V)> src) => months.Select(m => (object)new { month = m.Label, value = Math.Round(src.Where(x => x.Date.Year == m.Y && x.Date.Month == m.M).Sum(x => x.V), 2) }).ToList();
        switch (kind)
        {
            case "sales":
                {
                    var inv = await db.SalesInvoices.Include(x => x.Customer).Where(x => x.Date >= start).ToListAsync();
                    var items = await db.SalesInvoiceItems.Include(i => i.Product).ToListAsync();
                    return new
                    {
                        monthly = Series(inv.Select(x => (x.Date, x.Subtotal))),
                        top = (await db.SalesInvoices.Include(x => x.Customer).ToListAsync()).GroupBy(x => x.Customer!.Name).Select(g => new { name = g.Key, value = Math.Round(g.Sum(x => x.Subtotal), 2) }).OrderByDescending(x => x.value).Take(5),
                        topProducts = items.GroupBy(i => i.Product!.Name).Select(g => new { name = g.Key, value = Math.Round(g.Sum(i => i.LineTotal), 2) }).OrderByDescending(x => x.value).Take(5)
                    };
                }
            case "purchases":
                {
                    var inv = await db.PurchaseInvoices.Where(x => x.Date >= start).ToListAsync();
                    return new
                    {
                        monthly = Series(inv.Select(x => (x.Date, x.Subtotal))),
                        top = (await db.PurchaseInvoices.Include(x => x.Supplier).ToListAsync()).GroupBy(x => x.Supplier!.Name).Select(g => new { name = g.Key, value = Math.Round(g.Sum(x => x.Subtotal), 2) }).OrderByDescending(x => x.value).Take(5),
                        topProducts = (await db.PurchaseInvoiceItems.Include(i => i.Product).ToListAsync()).GroupBy(i => i.Product!.Name).Select(g => new { name = g.Key, value = Math.Round(g.Sum(i => i.LineTotal), 2) }).OrderByDescending(x => x.value).Take(5)
                    };
                }
            case "inventory":
                {
                    var bal = await db.StockBalances.Include(b => b.Product).ThenInclude(p => p!.Category).ToListAsync();
                    return new
                    {
                        monthly = Array.Empty<object>(),
                        top = bal.GroupBy(b => b.Product!.Category?.Name ?? "Uncategorised").Select(g => new { name = g.Key, value = Math.Round(g.Sum(b => b.Quantity * b.Product!.AverageCost), 2) }).OrderByDescending(x => x.value),
                        topProducts = bal.GroupBy(b => b.Product!.ProductType).Select(g => new { name = g.Key, value = Math.Round(g.Sum(b => b.Quantity * b.Product!.AverageCost), 2) })
                    };
                }
            case "production":
                {
                    var o = await db.ProductionOrders.Include(x => x.Product).Where(x => x.Date >= start && x.Status == St.Completed).ToListAsync();
                    return new { monthly = Series(o.Select(x => (x.Date, x.QuantityProduced))), top = o.GroupBy(x => x.Product!.Name).Select(g => new { name = g.Key, value = g.Sum(x => x.QuantityProduced) }).OrderByDescending(x => x.value).Take(5), topProducts = Series(o.Select(x => (x.Date, x.TotalCost))) };
                }
            case "financial":
                {
                    var rve = await RevenueVsExpenses();
                    return new { monthly = rve, top = Array.Empty<object>(), topProducts = Array.Empty<object>() };
                }
            default: throw new BusinessException("Unknown analytics type.");
        }
    }
    public async Task<List<object>> RevenueVsExpenses()
    {
        var months = Months(); var start = new DateTime(months[0].Y, months[0].M, 1);
        var lines = await db.JournalEntryLines.Where(l => l.JournalEntry!.Status == St.Posted && l.JournalEntry.Date >= start && (l.Account!.AccountType == AccountTypes.Revenue || l.Account.AccountType == AccountTypes.Expense))
            .Select(l => new { l.JournalEntry!.Date, Type = l.Account!.AccountType, l.Debit, l.Credit }).ToListAsync();
        return months.Select(m => (object)new
        {
            month = m.Label,
            revenue = Math.Round(lines.Where(l => l.Type == AccountTypes.Revenue && l.Date.Year == m.Y && l.Date.Month == m.M).Sum(l => l.Credit - l.Debit), 2),
            expenses = Math.Round(lines.Where(l => l.Type == AccountTypes.Expense && l.Date.Year == m.Y && l.Date.Month == m.M).Sum(l => l.Debit - l.Credit), 2)
        }).ToList();
    }
}

public static class PagingObjects
{
    /// <summary>Rows over boxed anonymous objects: reflection uses each runtime type.</summary>
    public static PagedResult<object> Rows(IEnumerable<object> rows, PageQuery pq)
    {
        var list = rows.ToList();
        if (list.Count == 0) return new PagedResult<object> { Page = pq.Page, PageSize = pq.PageSize, Summary = new() };
        var props = list[0].GetType().GetProperties();
        if (!string.IsNullOrWhiteSpace(pq.Search))
        {
            var s = pq.Search.Trim();
            list = list.Where(r => props.Any(p => (p.GetValue(r)?.ToString() ?? "").Contains(s, StringComparison.OrdinalIgnoreCase))).ToList();
        }
        var summary = new Dictionary<string, decimal>();
        foreach (var p in props.Where(p => p.PropertyType == typeof(decimal))) summary[p.Name] = list.Sum(r => (decimal)p.GetValue(r)!);
        var sort = props.FirstOrDefault(p => string.Equals(p.Name, pq.SortBy, StringComparison.OrdinalIgnoreCase));
        if (sort != null) list = (string.Equals(pq.SortDir, "desc", StringComparison.OrdinalIgnoreCase) ? list.OrderByDescending(r => sort.GetValue(r)) : list.OrderBy(r => sort.GetValue(r))).ToList();
        if (pq.Page < 1) pq.Page = 1; if (pq.PageSize < 1) pq.PageSize = 20;
        return new PagedResult<object> { Items = list.Skip((pq.Page - 1) * pq.PageSize).Take(pq.PageSize).ToList(), Total = list.Count, Page = pq.Page, PageSize = pq.PageSize, Summary = summary };
    }
}

public class DashboardService(ErpDbContext db, ReportService reports, AccountingService acct)
{
    public async Task<object> Get()
    {
        var months = ReportService.Months();
var start = new DateTime(
    months[0].Y,
    months[0].M,
    1,
    0,
    0,
    0,
    DateTimeKind.Utc
);
        var products = await db.Products.Include(p => p.Category).ToListAsync();
        var balances = await db.StockBalances.Include(b => b.Product).ThenInclude(p => p!.Category).ToListAsync();
        var onHand = balances.GroupBy(b => b.ProductId).ToDictionary(g => g.Key, g => g.Sum(b => b.Quantity));
        var low = products.Where(p => p.IsActive && p.ReorderLevel > 0 && onHand.GetValueOrDefault(p.Id) <= p.ReorderLevel)
            .Select(p => new { p.Id, p.Sku, p.Name, onHand = onHand.GetValueOrDefault(p.Id), reorderLevel = p.ReorderLevel }).ToList();
        var salesInv = await db.SalesInvoices.Where(x => x.Date >= start).Select(x => new { x.Date, x.Subtotal }).ToListAsync();
        var purInv = await db.PurchaseInvoices.Where(x => x.Date >= start).Select(x => new { x.Date, x.Subtotal }).ToListAsync();
        var pl = await acct.ProfitLoss(null, null);
        var salesTotal = await db.SalesInvoices.SumAsync(x => (decimal?)x.Subtotal) ?? 0; var salesRet = await db.SalesReturns.SumAsync(x => (decimal?)x.Subtotal) ?? 0;
        var purTotal = await db.PurchaseInvoices.SumAsync(x => (decimal?)x.Subtotal) ?? 0; var purRet = await db.PurchaseReturns.SumAsync(x => (decimal?)x.Subtotal) ?? 0;
        var recent = await db.JournalEntries.Where(j => j.Status == St.Posted).OrderByDescending(j => j.Date).ThenByDescending(j => j.Id).Take(10).Select(j => new { j.Number, j.Date, j.Description, amount = j.TotalDebit }).ToListAsync();
        var notes = await db.Notifications.Where(n => !n.IsRead).OrderByDescending(n => n.CreatedAt).Take(8).ToListAsync();
        return new
        {
            totals = new
            {
                products = products.Count, suppliers = await db.Suppliers.CountAsync(), customers = await db.Customers.CountAsync(),
                purchaseOrders = await db.PurchaseOrders.CountAsync(), salesOrders = await db.SalesOrders.CountAsync(),
                inventoryValue = Math.Round(balances.Sum(b => b.Quantity * b.Product!.AverageCost), 2),
                salesRevenue = salesTotal - salesRet, purchaseValue = purTotal - purRet, expenses = await db.Expenses.SumAsync(x => (decimal?)x.Amount) ?? 0, netProfit = pl.NetProfit
            },
            lowStock = low,
            pending = new
            {
                purchaseOrders = await db.PurchaseOrders.CountAsync(x => x.Status == St.Draft || x.Status == St.Approved || x.Status == St.PartiallyReceived),
                salesOrders = await db.SalesOrders.CountAsync(x => x.Status == St.Draft || x.Status == St.Confirmed || x.Status == St.PartiallyDelivered)
            },
            activeProduction = await db.ProductionOrders.CountAsync(x => x.Status == St.Planned || x.Status == St.Released || x.Status == St.InProgress),
            monthlySales = months.Select(m => new { month = m.Label, value = Math.Round(salesInv.Where(x => x.Date.Year == m.Y && x.Date.Month == m.M).Sum(x => x.Subtotal), 2) }),
            monthlyPurchases = months.Select(m => new { month = m.Label, value = Math.Round(purInv.Where(x => x.Date.Year == m.Y && x.Date.Month == m.M).Sum(x => x.Subtotal), 2) }),
            inventoryDistribution = balances.GroupBy(b => b.Product!.Category?.Name ?? "Uncategorised").Select(g => new { name = g.Key, value = Math.Round(g.Sum(b => b.Quantity * b.Product!.AverageCost), 2) }).Where(x => x.value > 0),
            revenueVsExpenses = await reports.RevenueVsExpenses(),
            recentTransactions = recent, notifications = notes.Select(n => new { n.Id, n.Message, n.Type, n.Link, n.CreatedAt })
        };
    }
}
