namespace MiniERP.Api.Services;

public interface ICurrentUser { string UserName { get; } int? UserId { get; } }
public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserName => accessor.HttpContext?.User.Identity?.Name ?? "system";
    public int? UserId => int.TryParse(accessor.HttpContext?.User.FindFirst("uid")?.Value, out var i) ? i : null;
}
public class SystemUser : ICurrentUser { public string UserName => "system"; public int? UserId => null; }

public abstract class ServiceBase
{
    protected static BusinessException NF(string what) => new($"{what} not found.", 404);
    protected static DateTime Day(DateTime d) => d == default ? DateTime.UtcNow.Date : d.Date;
    protected static void CheckLines(List<LineDto>? lines, bool needPrice)
    {
        if (lines == null || lines.Count == 0) throw new BusinessException("At least one line item is required.");
        foreach (var l in lines)
        {
            if (l.Quantity <= 0) throw new BusinessException("Line quantity must be greater than zero.");
            if (needPrice && l.UnitPrice < 0) throw new BusinessException("Unit price cannot be negative.");
        }
    }
    protected static void Recalc(PricedDoc d, IEnumerable<ItemBase> items)
    {
        var list = items.ToList();
        foreach (var i in list) i.LineTotal = Math.Round(i.Quantity * i.UnitPrice, 4);
        d.Subtotal = list.Sum(i => i.LineTotal);
        d.TaxAmount = Math.Round(list.Sum(i => i.LineTotal * i.TaxRate / 100m), 4);
        d.Total = d.Subtotal + d.TaxAmount;
    }
    protected static void Require(string actual, string what, params string[] allowed)
    {
        if (!allowed.Contains(actual)) throw new BusinessException($"{what} is {actual}; allowed status: {string.Join("/", allowed)}.", 409);
    }
    /// <summary>Recalculates invoice status from paid/credited amounts.</summary>
    protected static string Settle(decimal total, decimal paid, decimal credited)
    {
        var bal = total - paid - credited;
        return bal <= 0.0001m ? St.Paid : (paid + credited > 0 ? St.PartiallyPaid : St.Unpaid);
    }
}

public class NumberingService(ErpDbContext db)
{
    public async Task<string> Next(string prefix)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var seq = await db.DocumentSequences.FirstOrDefaultAsync(s => s.Prefix == prefix);
            if (seq == null) { seq = new DocumentSequence { Prefix = prefix }; db.DocumentSequences.Add(seq); }
            seq.LastNumber++;
            try { await db.SaveChangesAsync(); return $"{prefix}-{DateTime.UtcNow:yyyy}-{seq.LastNumber:D5}"; }
            catch (DbUpdateConcurrencyException ex) { foreach (var e in ex.Entries) await e.ReloadAsync(); }
        }
        throw new BusinessException("Could not allocate a document number; please retry.", 409);
    }
}

public static class Acc
{
    public const string Cash = "1000", Bank = "1010", AR = "1100", Inventory = "1300", WIP = "1400", InputTax = "1500", AP = "2000", GRNI = "2100", OutputTax = "2200",
        Equity = "3000", Sales = "4000", SalesReturns = "4100", OtherIncome = "4200", COGS = "5000", Overhead = "5100", InvAdj = "5150";
    public static string ForMethod(string? method) => string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase) ? Cash : Bank;
}
public record JLine(string Account, decimal Debit, decimal Credit, string? Memo = null);

public class AccountingService(ErpDbContext db, ICurrentUser user, NumberingService num) : ServiceBase
{
    public static JLine Dr(string a, decimal v, string? memo = null) => new(a, Math.Round(v, 4), 0, memo);
    public static JLine Cr(string a, decimal v, string? memo = null) => new(a, 0, Math.Round(v, 4), memo);

    /// <summary>Posts a balanced double-entry journal. The refKey makes posting idempotent (duplicate = rejected).</summary>
    public async Task<JournalEntry?> Post(DateTime date, string description, string refType, int? refId, string refKey, params JLine[] lines)
    {
        var ls = lines.Where(l => l.Debit != 0 || l.Credit != 0).ToArray();
        if (ls.Length == 0) return null;
        if (ls.Any(l => l.Debit < 0 || l.Credit < 0)) throw new BusinessException("Journal amounts cannot be negative.");
        var d = ls.Sum(l => l.Debit); var c = ls.Sum(l => l.Credit);
        if (Math.Abs(d - c) > 0.0001m) throw new BusinessException($"Journal is not balanced (Dr {d} / Cr {c}).");
        if (await db.JournalEntries.AnyAsync(j => j.ReferenceKey == refKey)) throw new BusinessException($"Duplicate posting prevented ({refKey}).", 409);
        var codes = ls.Select(l => l.Account).Distinct().ToList();
        var accts = await db.Accounts.Where(a => codes.Contains(a.Code)).ToDictionaryAsync(a => a.Code);
        foreach (var code in codes) if (!accts.ContainsKey(code)) throw new BusinessException($"Account {code} is missing from the chart of accounts.");
        var je = new JournalEntry
        {
            Number = await num.Next(Prefix.JV), Date = date.Date, Description = description, ReferenceType = refType, ReferenceId = refId, ReferenceKey = refKey,
            Status = St.Posted, TotalDebit = d, TotalCredit = c, CreatedBy = user.UserName,
            Lines = ls.Select(l => new JournalEntryLine { AccountId = accts[l.Account].Id, Debit = l.Debit, Credit = l.Credit, Memo = l.Memo }).ToList()
        };
        db.JournalEntries.Add(je);
        await db.SaveChangesAsync();
        return je;
    }

    // ---------- manual journals ----------
    public Task<JournalDto> CreateManual(JournalDto d) => db.InTx(async () =>
    {
        if (d.Lines.Count < 2) throw new BusinessException("A journal needs at least two lines.");
        foreach (var l in d.Lines)
        {
            if (l.Debit < 0 || l.Credit < 0 || (l.Debit > 0 && l.Credit > 0) || (l.Debit == 0 && l.Credit == 0)) throw new BusinessException("Each line must have either a debit or a credit amount.");
        }
        var ids = d.Lines.Select(l => l.AccountId).Distinct().ToList();
        if (await db.Accounts.CountAsync(a => ids.Contains(a.Id)) != ids.Count) throw new BusinessException("One or more accounts do not exist.");
        var td = d.Lines.Sum(l => l.Debit); var tc = d.Lines.Sum(l => l.Credit);
        if (d.Post && Math.Abs(td - tc) > 0.0001m) throw new BusinessException($"Posted journals must balance (Dr {td} / Cr {tc}).");
        var je = new JournalEntry
        {
            Number = await num.Next(Prefix.JV), Date = Day(d.Date), Description = d.Description, ReferenceType = "Manual", Status = d.Post ? St.Posted : St.Draft,
            ReferenceKey = d.Post ? $"MJ:{Guid.NewGuid():N}" : null, TotalDebit = td, TotalCredit = tc, CreatedBy = user.UserName,
            Lines = d.Lines.Select(l => new JournalEntryLine { AccountId = l.AccountId, Debit = l.Debit, Credit = l.Credit, Memo = l.Memo }).ToList()
        };
        db.JournalEntries.Add(je); await db.SaveChangesAsync();
        return await Load(je.Id);
    });
    public Task<JournalDto> PostDraft(int id) => db.InTx(async () =>
    {
        var j = await db.JournalEntries.FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Journal entry");
        Require(j.Status, "Journal entry", St.Draft);
        if (Math.Abs(j.TotalDebit - j.TotalCredit) > 0.0001m) throw new BusinessException("Journal is not balanced.");
        j.Status = St.Posted; j.ReferenceKey = $"MJ:{Guid.NewGuid():N}"; await db.SaveChangesAsync();
        return await Load(id);
    });
    public Task<JournalDto> Reverse(int id) => db.InTx(async () =>
    {
        var j = await db.JournalEntries.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Journal entry");
        Require(j.Status, "Journal entry", St.Posted);
        var rev = new JournalEntry
        {
            Number = await num.Next(Prefix.JV), Date = DateTime.UtcNow.Date, Description = $"Reversal of {j.Number}", ReferenceType = "Reversal", ReferenceId = j.Id, ReferenceKey = $"REV:{j.Id}",
            Status = St.Posted, TotalDebit = j.TotalCredit, TotalCredit = j.TotalDebit, CreatedBy = user.UserName,
            Lines = j.Lines.Select(l => new JournalEntryLine { AccountId = l.AccountId, Debit = l.Credit, Credit = l.Debit, Memo = l.Memo }).ToList()
        };
        j.Status = St.Reversed; db.JournalEntries.Add(rev); await db.SaveChangesAsync();
        return await Load(rev.Id);
    });
    public async Task<JournalDto> Load(int id) => (await db.JournalEntries.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.Account).FirstAsync(x => x.Id == id)).ToDto();
    public IQueryable<JournalEntry> Journals => db.JournalEntries.Include(x => x.Lines).ThenInclude(l => l.Account);

    // ---------- reporting ----------
    IQueryable<JournalEntryLine> PostedLines => db.JournalEntryLines.Where(l => l.JournalEntry!.Status == St.Posted);

    public async Task<object> TrialBalance(DateTime? asOf)
    {
        var q = PostedLines; if (asOf.HasValue) q = q.Where(l => l.JournalEntry!.Date <= asOf.Value.Date);
        var sums = await q.GroupBy(l => l.AccountId).Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }).ToListAsync();
        var accts = await db.Accounts.ToListAsync();
        var rows = accts.OrderBy(a => a.Code).Select(a =>
        {
            var s = sums.FirstOrDefault(x => x.AccountId == a.Id); var net = (s?.Debit ?? 0) - (s?.Credit ?? 0);
            return new { a.Code, a.Name, Type = a.AccountType, Debit = net > 0 ? net : 0m, Credit = net < 0 ? -net : 0m };
        }).Where(r => r.Debit != 0 || r.Credit != 0).ToList();
        var td = rows.Sum(r => r.Debit); var tc = rows.Sum(r => r.Credit);
        return new { rows, totalDebit = td, totalCredit = tc, balanced = Math.Abs(td - tc) < 0.001m };
    }

    public async Task<object> GeneralLedger(int accountId, DateTime? from, DateTime? to)
    {
        var a = await db.Accounts.FirstOrDefaultAsync(x => x.Id == accountId) ?? throw NF("Account");
        var debitNormal = a.AccountType is AccountTypes.Asset or AccountTypes.Expense;
        var q = PostedLines.Where(l => l.AccountId == accountId);
        decimal opening = 0;
        if (from.HasValue)
        {
            var pre = await q.Where(l => l.JournalEntry!.Date < from.Value.Date).Select(l => new { l.Debit, l.Credit }).ToListAsync();
            opening = pre.Sum(x => debitNormal ? x.Debit - x.Credit : x.Credit - x.Debit);
            q = q.Where(l => l.JournalEntry!.Date >= from.Value.Date);
        }
        if (to.HasValue) q = q.Where(l => l.JournalEntry!.Date <= to.Value.Date);
        var lines = await q.Select(l => new { l.JournalEntry!.Date, l.JournalEntry.Number, l.JournalEntry.Description, l.Debit, l.Credit, l.JournalEntryId, l.Id })
            .OrderBy(l => l.Date).ThenBy(l => l.JournalEntryId).ThenBy(l => l.Id).ToListAsync();
        var bal = opening;
        var rows = lines.Select(l => { bal += debitNormal ? l.Debit - l.Credit : l.Credit - l.Debit; return new { l.Date, l.Number, l.Description, l.Debit, l.Credit, Balance = bal }; }).ToList();
        return new { account = new { a.Id, a.Code, a.Name, Type = a.AccountType }, opening, rows, closing = bal };
    }

    async Task<Dictionary<int, decimal>> NetByAccount(DateTime? from, DateTime? to, DateTime? asOf = null)
    {
        var q = PostedLines;
        if (from.HasValue) q = q.Where(l => l.JournalEntry!.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(l => l.JournalEntry!.Date <= to.Value.Date);
        if (asOf.HasValue) q = q.Where(l => l.JournalEntry!.Date <= asOf.Value.Date);
        var s = await q.GroupBy(l => l.AccountId).Select(g => new { g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }).ToListAsync();
        return s.ToDictionary(x => x.Key, x => x.Debit - x.Credit); // debit-positive
    }

    public async Task<ProfitLossResult> ProfitLoss(DateTime? from, DateTime? to)
    {
        var net = await NetByAccount(from, to);
        var accts = await db.Accounts.ToListAsync();
        var revenue = accts.Where(a => a.AccountType == AccountTypes.Revenue).Select(a => new PlRow(a.Code, a.Name, -(net.GetValueOrDefault(a.Id)))).Where(r => r.Amount != 0).OrderBy(r => r.Code).ToList();
        var cogs = accts.Where(a => a.Code == Acc.COGS || a.Code == Acc.Overhead).Select(a => new PlRow(a.Code, a.Name, net.GetValueOrDefault(a.Id))).Where(r => r.Amount != 0).OrderBy(r => r.Code).ToList();
        var opex = accts.Where(a => a.AccountType == AccountTypes.Expense && a.Code != Acc.COGS && a.Code != Acc.Overhead).Select(a => new PlRow(a.Code, a.Name, net.GetValueOrDefault(a.Id))).Where(r => r.Amount != 0).OrderBy(r => r.Code).ToList();
        var tr = revenue.Sum(r => r.Amount); var tc = cogs.Sum(r => r.Amount); var to2 = opex.Sum(r => r.Amount);
        return new ProfitLossResult(revenue, cogs, opex, tr, tc, tr - tc, to2, tr - tc - to2);
    }
    public async Task<object> BalanceSheet(DateTime? asOf)
    {
        var net = await NetByAccount(null, null, asOf);
        var accts = await db.Accounts.ToListAsync();
        List<PlRow> Section(string type, int sign) => accts.Where(a => a.AccountType == type).Select(a => new PlRow(a.Code, a.Name, sign * net.GetValueOrDefault(a.Id))).Where(r => r.Amount != 0).OrderBy(r => r.Code).ToList();
        var assets = Section(AccountTypes.Asset, 1); var liab = Section(AccountTypes.Liability, -1); var eq = Section(AccountTypes.Equity, -1);
        var earnings = accts.Where(a => a.AccountType == AccountTypes.Revenue).Sum(a => -net.GetValueOrDefault(a.Id)) - accts.Where(a => a.AccountType == AccountTypes.Expense).Sum(a => net.GetValueOrDefault(a.Id));
        var ta = assets.Sum(r => r.Amount); var tl = liab.Sum(r => r.Amount); var te = eq.Sum(r => r.Amount) + earnings;
        return new { assets, liabilities = liab, equity = eq, currentEarnings = earnings, totalAssets = ta, totalLiabilities = tl, totalEquity = te, balanced = Math.Abs(ta - tl - te) < 0.001m };
    }
}
public record PlRow(string Code, string Name, decimal Amount);
public record ProfitLossResult(List<PlRow> Revenue, List<PlRow> CostOfSales, List<PlRow> OperatingExpenses, decimal TotalRevenue, decimal TotalCostOfSales, decimal GrossProfit, decimal TotalOperatingExpenses, decimal NetProfit);

public static class TxType
{
    public const string Opening = "OpeningStock", PurchaseReceipt = "PurchaseReceipt", SalesDelivery = "SalesDelivery", TransferIn = "TransferIn", TransferOut = "TransferOut",
        AdjustmentIn = "AdjustmentIn", AdjustmentOut = "AdjustmentOut", StockIn = "StockIn", StockOut = "StockOut", MaterialIssue = "MaterialIssue",
        ProductionReceipt = "ProductionReceipt", PurchaseReturn = "PurchaseReturn", SalesReturn = "SalesReturn";
}

public class InventoryService(ErpDbContext db, ICurrentUser user, AccountingService acct, NumberingService num) : ServiceBase
{
    public async Task<decimal> Available(int productId, int warehouseId) =>
        await db.StockBalances.Where(b => b.ProductId == productId && b.WarehouseId == warehouseId).Select(b => b.Quantity).FirstOrDefaultAsync();
    public async Task<decimal> TotalOnHand(int productId) => await db.StockBalances.Where(b => b.ProductId == productId).SumAsync(b => b.Quantity);

    /// <summary>The ONLY place stock balances change. Every change writes a StockTransaction. Caller owns the DB transaction.</summary>
    public async Task<StockTransaction> Move(int productId, int warehouseId, decimal qty, string type, decimal? unitCost, string refType, int? refId, string? refNo, string? notes = null, DateTime? date = null)
    {
        if (qty == 0) throw new BusinessException("Quantity cannot be zero.");
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId) ?? throw NF("Product");
        if (!await db.Warehouses.AnyAsync(w => w.Id == warehouseId)) throw NF("Warehouse");
        var bal = await db.StockBalances.FirstOrDefaultAsync(b => b.ProductId == productId && b.WarehouseId == warehouseId);
        if (bal == null) { bal = new StockBalance { ProductId = productId, WarehouseId = warehouseId }; db.StockBalances.Add(bal); }
        if (qty < 0 && bal.Quantity + qty < 0) throw new BusinessException($"Insufficient stock for {product.Sku}: available {bal.Quantity}, required {-qty}.");
        var totalBefore = await db.StockBalances.Where(b => b.ProductId == productId).SumAsync(b => b.Quantity);
        decimal cost;
        if (qty > 0)
        {
            cost = unitCost ?? product.AverageCost;
            var newTotal = totalBefore + qty;
            if (newTotal > 0) product.AverageCost = Math.Round((totalBefore * product.AverageCost + qty * cost) / newTotal, 4);
        }
        else cost = product.AverageCost;
        bal.Quantity += qty; bal.UpdatedAt = DateTime.UtcNow;
        var tx = new StockTransaction { Date = date ?? DateTime.UtcNow, ProductId = productId, WarehouseId = warehouseId, TransactionType = type, Quantity = qty, UnitCost = cost, ReferenceType = refType, ReferenceId = refId, ReferenceNo = refNo, Notes = notes, CreatedBy = user.UserName };
        db.StockTransactions.Add(tx);
        await db.SaveChangesAsync();
        if (qty < 0 && product.ReorderLevel > 0)
        {
            var total = await TotalOnHand(productId);
            if (total <= product.ReorderLevel)
            {
                var key = $"LOW:{productId}";
                if (!await db.Notifications.AnyAsync(n => n.Key == key && !n.IsRead))
                {
                    db.Notifications.Add(new Notification { Key = key, Type = "LowStock", Message = $"Low stock: {product.Sku} {product.Name} has {total} units (reorder level {product.ReorderLevel}).", Link = "/inventory/stock" });
                    await db.SaveChangesAsync();
                }
            }
        }
        return tx;
    }

    /// <summary>Opening stock, stock in/out and adjustments. Posts the matching GL entry.</summary>
    public Task<StockTransaction> Movement(MovementRequest r) => db.InTx(async () =>
    {
        if (r.Quantity <= 0) throw new BusinessException("Quantity must be greater than zero.");
        var inbound = r.Type is TxType.Opening or TxType.StockIn or TxType.AdjustmentIn;
        var outbound = r.Type is TxType.StockOut or TxType.AdjustmentOut;
        if (!inbound && !outbound) throw new BusinessException("Invalid movement type.");
        if (r.Type == TxType.Opening && (r.UnitCost == null || r.UnitCost < 0)) throw new BusinessException("Opening stock requires a unit cost.");
        var date = r.Date ?? DateTime.UtcNow;
        var tx = await Move(r.ProductId, r.WarehouseId, inbound ? r.Quantity : -r.Quantity, r.Type, r.UnitCost, "StockMovement", null, null, r.Notes, date);
        var value = Math.Round(r.Quantity * tx.UnitCost, 4);
        var other = r.Type == TxType.Opening ? Acc.Equity : Acc.InvAdj;
        await acct.Post(date, $"{r.Type} {r.Quantity} @ {tx.UnitCost}", "StockTransaction", tx.Id, $"STK:{tx.Id}",
            inbound ? AccountingService.Dr(Acc.Inventory, value) : AccountingService.Dr(other, value),
            inbound ? AccountingService.Cr(other, value) : AccountingService.Cr(Acc.Inventory, value));
        return tx;
    });

    // ---------- transfers ----------
    public IQueryable<StockTransfer> Transfers => db.StockTransfers.Include(x => x.FromWarehouse).Include(x => x.ToWarehouse).Include(x => x.Items).ThenInclude(i => i.Product);
    public Task<DocumentDto> CreateTransfer(DocumentDto d) => db.InTx(async () =>
    {
        CheckLines(d.Lines, false);
        if (d.WarehouseId == null || d.ToWarehouseId == null) throw new BusinessException("Source and destination warehouses are required.");
        if (d.WarehouseId == d.ToWarehouseId) throw new BusinessException("Source and destination warehouses must differ.");
        var t = new StockTransfer { Number = await num.Next(Prefix.TRF), Date = Day(d.Date), FromWarehouseId = d.WarehouseId.Value, ToWarehouseId = d.ToWarehouseId.Value, Notes = d.Notes, CreatedBy = user.UserName,
            Items = d.Lines.Select(l => new StockTransferItem { ProductId = l.ProductId, Quantity = l.Quantity, Notes = l.Notes }).ToList() };
        db.StockTransfers.Add(t); await db.SaveChangesAsync();
        return (await Transfers.AsNoTracking().FirstAsync(x => x.Id == t.Id)).ToDto();
    });
    public Task<DocumentDto> CompleteTransfer(int id) => db.InTx(async () =>
    {
        var t = await db.StockTransfers.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Stock transfer");
        Require(t.Status, "Stock transfer", St.Draft);
        foreach (var i in t.Items)
        {
            var outTx = await Move(i.ProductId, t.FromWarehouseId, -i.Quantity, TxType.TransferOut, null, "Transfer", t.Id, t.Number, date: t.Date);
            await Move(i.ProductId, t.ToWarehouseId, i.Quantity, TxType.TransferIn, outTx.UnitCost, "Transfer", t.Id, t.Number, date: t.Date);
        }
        t.Status = St.Completed; await db.SaveChangesAsync();
        return (await Transfers.AsNoTracking().FirstAsync(x => x.Id == id)).ToDto();
    });
}
