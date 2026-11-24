namespace MiniERP.Api.Services;

public class SalesService(ErpDbContext db, ICurrentUser user, NumberingService num, InventoryService inv, AccountingService acct) : ServiceBase
{
    public IQueryable<SalesQuotation> Quotations => db.SalesQuotations.Include(x => x.Customer).Include(x => x.Items).ThenInclude(i => i.Product);
    public IQueryable<SalesOrder> Orders => db.SalesOrders.Include(x => x.Customer).Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(i => i.Product);
    public IQueryable<Delivery> Deliveries => db.Deliveries.Include(x => x.Customer).Include(x => x.Warehouse).Include(x => x.SalesOrder).Include(x => x.Items).ThenInclude(i => i.Product);
    public IQueryable<SalesInvoice> Invoices => db.SalesInvoices.Include(x => x.Customer).Include(x => x.Delivery).Include(x => x.Items).ThenInclude(i => i.Product);
    public IQueryable<SalesReturn> Returns => db.SalesReturns.Include(x => x.Customer).Include(x => x.Warehouse).Include(x => x.SalesInvoice).Include(x => x.Items).ThenInclude(i => i.Product);

    async Task<Dictionary<int, Product>> Products(IEnumerable<int> ids)
    {
        var list = ids.Distinct().ToList();
        var d = await db.Products.Where(p => list.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var id in list) if (!d.ContainsKey(id)) throw new BusinessException($"Product {id} not found.");
        return d;
    }
    async Task<DocumentDto> LoadQt(int id) => (await Quotations.AsNoTracking().FirstAsync(x => x.Id == id)).ToDto();
    async Task<DocumentDto> LoadSo(int id) => (await Orders.AsNoTracking().FirstAsync(x => x.Id == id)).ToDto();
    async Task<DocumentDto> LoadDn(int id) => (await Deliveries.AsNoTracking().FirstAsync(x => x.Id == id)).ToDto();
    async Task<DocumentDto> LoadInv(int id) => (await Invoices.AsNoTracking().FirstAsync(x => x.Id == id)).ToDto();
    async Task CheckCustomer(int? id) { if (id == null || !await db.Customers.AnyAsync(c => c.Id == id)) throw new BusinessException("A valid customer is required."); }

    // ================= Quotations =================
    public Task<DocumentDto> CreateQuotation(DocumentDto d) => db.InTx(async () =>
    {
        CheckLines(d.Lines, true); await Products(d.Lines.Select(l => l.ProductId)); await CheckCustomer(d.PartyId);
        var q = new SalesQuotation { Number = await num.Next(Prefix.QT), Date = Day(d.Date), CustomerId = d.PartyId!.Value, ValidUntil = d.DueDate, Notes = d.Notes, CreatedBy = user.UserName,
            Items = d.Lines.Select(l => new SalesQuotationItem { ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxRate = l.TaxRate, Notes = l.Notes }).ToList() };
        Recalc(q, q.Items); db.SalesQuotations.Add(q); await db.SaveChangesAsync();
        return await LoadQt(q.Id);
    });
    public Task<DocumentDto> UpdateQuotation(int id, DocumentDto d) => db.InTx(async () =>
    {
        var q = await db.SalesQuotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Quotation");
        Require(q.Status, "Quotation", St.Draft);
        CheckLines(d.Lines, true); await Products(d.Lines.Select(l => l.ProductId)); await CheckCustomer(d.PartyId);
        db.RemoveRange(q.Items.ToList());
        q.Items = d.Lines.Select(l => new SalesQuotationItem { ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxRate = l.TaxRate, Notes = l.Notes }).ToList();
        q.CustomerId = d.PartyId!.Value; q.Date = Day(d.Date); q.ValidUntil = d.DueDate; q.Notes = d.Notes;
        Recalc(q, q.Items); await db.SaveChangesAsync(); return await LoadQt(id);
    });
    public Task<DocumentDto> SetQuotationStatus(int id, string action) => db.InTx(async () =>
    {
        var q = await db.SalesQuotations.FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Quotation");
        switch (action)
        {
            case "send": Require(q.Status, "Quotation", St.Draft); q.Status = St.Sent; break;
            case "accept": Require(q.Status, "Quotation", St.Draft, St.Sent); q.Status = St.Accepted; break;
            case "reject": Require(q.Status, "Quotation", St.Draft, St.Sent); q.Status = St.Rejected; break;
            default: throw new BusinessException("Unknown action.");
        }
        await db.SaveChangesAsync(); return await LoadQt(id);
    });
    public Task<DocumentDto> ConvertQuotation(int id, ConvertRequest c) => db.InTx(async () =>
    {
        var q = await db.SalesQuotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Quotation");
        Require(q.Status, "Quotation", St.Accepted);
        if (c.WarehouseId == null || !await db.Warehouses.AnyAsync(w => w.Id == c.WarehouseId)) throw new BusinessException("A valid warehouse is required.");
        var so = new SalesOrder { Number = await num.Next(Prefix.SO), Date = DateTime.UtcNow.Date, CustomerId = q.CustomerId, WarehouseId = c.WarehouseId.Value, QuotationId = q.Id, Notes = $"From quotation {q.Number}", CreatedBy = user.UserName,
            Items = q.Items.Select(i => new SalesOrderItem { ProductId = i.ProductId, Quantity = i.Quantity, UnitPrice = i.UnitPrice, TaxRate = i.TaxRate }).ToList() };
        Recalc(so, so.Items); db.SalesOrders.Add(so); await db.SaveChangesAsync();
        q.Status = St.Converted; await db.SaveChangesAsync();
        return await LoadSo(so.Id);
    });

    // ================= Sales orders =================
    public Task<DocumentDto> CreateOrder(DocumentDto d) => db.InTx(async () =>
    {
        CheckLines(d.Lines, true); await Products(d.Lines.Select(l => l.ProductId)); await CheckCustomer(d.PartyId);
        if (d.WarehouseId == null || !await db.Warehouses.AnyAsync(w => w.Id == d.WarehouseId)) throw new BusinessException("A valid dispatch warehouse is required.");
        var so = new SalesOrder { Number = await num.Next(Prefix.SO), Date = Day(d.Date), CustomerId = d.PartyId!.Value, WarehouseId = d.WarehouseId.Value, DeliveryDate = d.DueDate, Notes = d.Notes, CreatedBy = user.UserName,
            Items = d.Lines.Select(l => new SalesOrderItem { ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxRate = l.TaxRate, Notes = l.Notes }).ToList() };
        Recalc(so, so.Items); db.SalesOrders.Add(so); await db.SaveChangesAsync();
        return await LoadSo(so.Id);
    });
    public Task<DocumentDto> UpdateOrder(int id, DocumentDto d) => db.InTx(async () =>
    {
        var so = await db.SalesOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Sales order");
        Require(so.Status, "Sales order", St.Draft);
        CheckLines(d.Lines, true); await Products(d.Lines.Select(l => l.ProductId)); await CheckCustomer(d.PartyId);
        db.RemoveRange(so.Items.ToList());
        so.Items = d.Lines.Select(l => new SalesOrderItem { ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxRate = l.TaxRate, Notes = l.Notes }).ToList();
        so.CustomerId = d.PartyId!.Value; so.WarehouseId = d.WarehouseId ?? so.WarehouseId; so.Date = Day(d.Date); so.DeliveryDate = d.DueDate; so.Notes = d.Notes;
        Recalc(so, so.Items); await db.SaveChangesAsync(); return await LoadSo(id);
    });
    /// <summary>Confirms the order after validating stock availability in the dispatch warehouse.</summary>
    public Task<DocumentDto> SetOrderStatus(int id, string action) => db.InTx(async () =>
    {
        var so = await db.SalesOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Sales order");
        switch (action)
        {
            case "confirm":
                Require(so.Status, "Sales order", St.Draft);
                var prods = await Products(so.Items.Select(i => i.ProductId));
                var shortages = new List<string>();
                foreach (var g in so.Items.GroupBy(i => i.ProductId))
                {
                    var need = g.Sum(i => i.Quantity); var avail = await inv.Available(g.Key, so.WarehouseId);
                    if (avail < need) shortages.Add($"{prods[g.Key].Sku}: required {need}, available {avail}");
                }
                if (shortages.Count > 0) throw new BusinessException("Insufficient stock - " + string.Join("; ", shortages));
                so.Status = St.Confirmed; break;
            case "cancel":
                Require(so.Status, "Sales order", St.Draft, St.Confirmed);
                if (so.Items.Any(i => i.DeliveredQuantity > 0)) throw new BusinessException("Cannot cancel an order that has deliveries.");
                so.Status = St.Cancelled; break;
            case "close": Require(so.Status, "Sales order", St.Confirmed, St.PartiallyDelivered, St.Delivered); so.Status = St.Closed; break;
            default: throw new BusinessException("Unknown action.");
        }
        await db.SaveChangesAsync(); return await LoadSo(id);
    });

    // ================= Deliveries =================
    public Task<DocumentDto> CreateDelivery(DocumentDto d) => db.InTx(async () =>
    {
        var so = await db.SalesOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == d.RefId) ?? throw NF("Sales order");
        Require(so.Status, "Sales order", St.Confirmed, St.PartiallyDelivered);
        var items = new List<DeliveryItem>();
        foreach (var l in (d.Lines ?? new()).Where(l => l.Quantity > 0))
        {
            var soi = so.Items.FirstOrDefault(i => i.Id == l.RefLineId) ?? throw new BusinessException("A line does not belong to the selected sales order.");
            var remaining = soi.Quantity - soi.DeliveredQuantity;
            if (l.Quantity > remaining) throw new BusinessException($"Delivery quantity ({l.Quantity}) exceeds the remaining quantity ({remaining}).");
            items.Add(new DeliveryItem { ProductId = soi.ProductId, SalesOrderItemId = soi.Id, Quantity = l.Quantity, UnitPrice = soi.UnitPrice, TaxRate = soi.TaxRate, LineTotal = Math.Round(l.Quantity * soi.UnitPrice, 4) });
        }
        if (items.Count == 0) throw new BusinessException("Enter a delivery quantity for at least one line.");
        var dn = new Delivery { Number = await num.Next(Prefix.DN), Date = Day(d.Date), SalesOrderId = so.Id, CustomerId = so.CustomerId, WarehouseId = d.WarehouseId ?? so.WarehouseId, Notes = d.Notes, CreatedBy = user.UserName, Items = items };
        db.Deliveries.Add(dn); await db.SaveChangesAsync();
        return await LoadDn(dn.Id);
    });
    /// <summary>Posting a delivery validates and deducts stock and books COGS (Dr COGS / Cr Inventory).</summary>
    public Task<DocumentDto> PostDelivery(int id) => db.InTx(async () =>
    {
        var dn = await db.Deliveries.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Delivery");
        Require(dn.Status, "Delivery", St.Draft);
        var so = await db.SalesOrders.Include(x => x.Items).FirstAsync(x => x.Id == dn.SalesOrderId);
        Require(so.Status, "Sales order", St.Confirmed, St.PartiallyDelivered);
        decimal cogs = 0;
        foreach (var it in dn.Items)
        {
            var soi = so.Items.First(i => i.Id == it.SalesOrderItemId);
            if (soi.DeliveredQuantity + it.Quantity > soi.Quantity) throw new BusinessException("Delivery exceeds the ordered quantity.");
            var tx = await inv.Move(it.ProductId, dn.WarehouseId, -it.Quantity, TxType.SalesDelivery, null, "Delivery", dn.Id, dn.Number, date: dn.Date);
            it.UnitCost = tx.UnitCost; cogs += Math.Round(it.Quantity * tx.UnitCost, 4); soi.DeliveredQuantity += it.Quantity;
        }
        dn.Status = St.Posted;
        so.Status = so.Items.All(i => i.DeliveredQuantity >= i.Quantity) ? St.Delivered : St.PartiallyDelivered;
        await acct.Post(dn.Date, $"Delivery {dn.Number} - cost of goods sold", "Delivery", dn.Id, $"DN:{dn.Id}", AccountingService.Dr(Acc.COGS, cogs), AccountingService.Cr(Acc.Inventory, cogs));
        await db.SaveChangesAsync();
        return await LoadDn(id);
    });

    // ================= Invoices =================
    public Task<DocumentDto> CreateInvoice(DocumentDto d) => db.InTx(async () =>
    {
        var dn = await db.Deliveries.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == d.RefId) ?? throw NF("Delivery");
        if (dn.Status != St.Posted) throw new BusinessException("Only posted deliveries can be invoiced.");
        if (dn.IsInvoiced) throw new BusinessException("This delivery has already been invoiced.", 409);
        var cust = await db.Customers.FirstAsync(c => c.Id == dn.CustomerId);
        var so = await db.SalesOrders.Include(x => x.Items).FirstAsync(x => x.Id == dn.SalesOrderId);
        var items = dn.Items.Select(i => new SalesInvoiceItem { ProductId = i.ProductId, Quantity = i.Quantity, UnitPrice = i.UnitPrice, TaxRate = i.TaxRate }).ToList();
        var date = Day(d.Date);
        var si = new SalesInvoice { Number = await num.Next(Prefix.SINV), Date = date, DueDate = d.DueDate ?? date.AddDays(cust.PaymentTermsDays), CustomerId = dn.CustomerId, DeliveryId = dn.Id, SalesOrderId = dn.SalesOrderId, Status = St.Unpaid, Notes = d.Notes, CreatedBy = user.UserName, Items = items };
        Recalc(si, items); dn.IsInvoiced = true;
        foreach (var it in dn.Items) so.Items.First(x => x.Id == it.SalesOrderItemId).InvoicedQuantity += it.Quantity;
        db.SalesInvoices.Add(si); await db.SaveChangesAsync();
        await acct.Post(date, $"Sales invoice {si.Number} - {cust.Name}", "SalesInvoice", si.Id, $"SINV:{si.Id}",
            AccountingService.Dr(Acc.AR, si.Total), AccountingService.Cr(Acc.Sales, si.Subtotal), AccountingService.Cr(Acc.OutputTax, si.TaxAmount));
        return await LoadInv(si.Id);
    });

    // ================= Returns =================
    public Task<DocumentDto> CreateReturn(DocumentDto d) => db.InTx(async () =>
    {
        var si = await db.SalesInvoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == d.RefId) ?? throw NF("Sales invoice");
        var wh = d.WarehouseId ?? throw new BusinessException("The warehouse receiving the returned goods is required.");
        CheckLines(d.Lines, false);
        var prior = (await db.SalesReturnItems.Where(i => i.SalesReturn!.SalesInvoiceId == si.Id).Select(i => new { i.ProductId, i.Quantity }).ToListAsync())
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var items = new List<SalesReturnItem>();
        foreach (var l in d.Lines)
        {
            var sii = si.Items.FirstOrDefault(i => i.ProductId == l.ProductId) ?? throw new BusinessException("Product is not on the original invoice.");
            var allowed = sii.Quantity - prior.GetValueOrDefault(l.ProductId);
            if (l.Quantity > allowed) throw new BusinessException($"Return quantity exceeds the returnable quantity ({allowed}).");
            items.Add(new SalesReturnItem { ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = sii.UnitPrice, TaxRate = sii.TaxRate });
        }
        var ret = new SalesReturn { Number = await num.Next(Prefix.SRET), Date = Day(d.Date), CustomerId = si.CustomerId, SalesInvoiceId = si.Id, WarehouseId = wh, Reason = d.Reference, Status = St.Posted, Notes = d.Notes, CreatedBy = user.UserName, Items = items };
        Recalc(ret, items); db.SalesReturns.Add(ret); await db.SaveChangesAsync();
        decimal cost = 0;
        foreach (var i in items) { var tx = await inv.Move(i.ProductId, wh, i.Quantity, TxType.SalesReturn, null, "SalesReturn", ret.Id, ret.Number, date: ret.Date); cost += Math.Round(i.Quantity * tx.UnitCost, 4); }
        await acct.Post(ret.Date, $"Sales return {ret.Number}", "SalesReturn", ret.Id, $"SRET:{ret.Id}",
            AccountingService.Dr(Acc.SalesReturns, ret.Subtotal), AccountingService.Dr(Acc.OutputTax, ret.TaxAmount), AccountingService.Cr(Acc.AR, ret.Total),
            AccountingService.Dr(Acc.Inventory, cost), AccountingService.Cr(Acc.COGS, cost));
        si.CreditedAmount += ret.Total; si.Status = Settle(si.Total, si.PaidAmount, si.CreditedAmount);
        await db.SaveChangesAsync();
        return (await Returns.AsNoTracking().FirstAsync(x => x.Id == ret.Id)).ToDto();
    });
}
