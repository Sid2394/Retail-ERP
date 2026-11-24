namespace MiniERP.Api.Services;

public class ProcurementService(ErpDbContext db, ICurrentUser user, NumberingService num, InventoryService inv, AccountingService acct) : ServiceBase
{
    public IQueryable<PurchaseRequisition> Requisitions => db.PurchaseRequisitions.Include(x => x.Items).ThenInclude(i => i.Product);
    public IQueryable<PurchaseOrder> Orders => db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(i => i.Product);
    public IQueryable<GoodsReceipt> Receipts => db.GoodsReceipts.Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.PurchaseOrder).Include(x => x.Items).ThenInclude(i => i.Product);
    public IQueryable<PurchaseInvoice> Invoices => db.PurchaseInvoices.Include(x => x.Supplier).Include(x => x.GoodsReceipt).Include(x => x.Items).ThenInclude(i => i.Product);
    public IQueryable<PurchaseReturn> Returns => db.PurchaseReturns.Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.PurchaseInvoice).Include(x => x.Items).ThenInclude(i => i.Product);

    async Task<Dictionary<int, Product>> Products(IEnumerable<int> ids)
    {
        var list = ids.Distinct().ToList();
        var d = await db.Products.Where(p => list.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var id in list) if (!d.ContainsKey(id)) throw new BusinessException($"Product {id} not found.");
        return d;
    }
    async Task<DocumentDto> LoadReq(int id) => (await Requisitions.AsNoTracking().FirstAsync(x => x.Id == id)).ToDto();
    async Task<DocumentDto> LoadPo(int id) => (await Orders.AsNoTracking().FirstAsync(x => x.Id == id)).ToDto();
    async Task<DocumentDto> LoadGrn(int id) => (await Receipts.AsNoTracking().FirstAsync(x => x.Id == id)).ToDto();
    async Task<DocumentDto> LoadInv(int id) => (await Invoices.AsNoTracking().FirstAsync(x => x.Id == id)).ToDto();

    // ================= Requisitions =================
    public Task<DocumentDto> CreateRequisition(DocumentDto d) => db.InTx(async () =>
    {
        CheckLines(d.Lines, false); await Products(d.Lines.Select(l => l.ProductId));
        var r = new PurchaseRequisition { Number = await num.Next(Prefix.PR), Date = Day(d.Date), Notes = d.Notes, RequestedBy = user.UserName, CreatedBy = user.UserName,
            Items = d.Lines.Select(l => new PurchaseRequisitionItem { ProductId = l.ProductId, Quantity = l.Quantity, Notes = l.Notes }).ToList() };
        db.PurchaseRequisitions.Add(r); await db.SaveChangesAsync();
        return await LoadReq(r.Id);
    });
    public Task<DocumentDto> UpdateRequisition(int id, DocumentDto d) => db.InTx(async () =>
    {
        var r = await db.PurchaseRequisitions.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Purchase requisition");
        Require(r.Status, "Requisition", St.Draft, St.Rejected);
        CheckLines(d.Lines, false); await Products(d.Lines.Select(l => l.ProductId));
        db.RemoveRange(r.Items.ToList());
        r.Items = d.Lines.Select(l => new PurchaseRequisitionItem { ProductId = l.ProductId, Quantity = l.Quantity, Notes = l.Notes }).ToList();
        r.Notes = d.Notes; r.Date = Day(d.Date); r.Status = St.Draft;
        await db.SaveChangesAsync(); return await LoadReq(id);
    });
    public Task<DocumentDto> SetRequisitionStatus(int id, string action) => db.InTx(async () =>
    {
        var r = await db.PurchaseRequisitions.FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Purchase requisition");
        switch (action)
        {
            case "submit": Require(r.Status, "Requisition", St.Draft); r.Status = St.Submitted; break;
            case "approve": Require(r.Status, "Requisition", St.Submitted); r.Status = St.Approved; r.ApprovedBy = user.UserName; r.ApprovedAt = DateTime.UtcNow; break;
            case "reject": Require(r.Status, "Requisition", St.Submitted); r.Status = St.Rejected; r.ApprovedBy = user.UserName; r.ApprovedAt = DateTime.UtcNow; break;
            default: throw new BusinessException("Unknown action.");
        }
        await db.SaveChangesAsync(); return await LoadReq(id);
    });
    public Task<DocumentDto> ConvertRequisition(int id, ConvertRequest c) => db.InTx(async () =>
    {
        var r = await db.PurchaseRequisitions.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Purchase requisition");
        Require(r.Status, "Requisition", St.Approved);
        if (c.SupplierId == null || c.WarehouseId == null) throw new BusinessException("Supplier and warehouse are required to create the purchase order.");
        if (!await db.Suppliers.AnyAsync(s => s.Id == c.SupplierId)) throw NF("Supplier");
        var prods = await Products(r.Items.Select(i => i.ProductId));
        var po = new PurchaseOrder { Number = await num.Next(Prefix.PO), Date = DateTime.UtcNow.Date, SupplierId = c.SupplierId.Value, WarehouseId = c.WarehouseId.Value, RequisitionId = r.Id, CreatedBy = user.UserName, Notes = $"From requisition {r.Number}",
            Items = r.Items.Select(i => new PurchaseOrderItem { ProductId = i.ProductId, Quantity = i.Quantity, UnitPrice = prods[i.ProductId].AverageCost, TaxRate = prods[i.ProductId].TaxRate }).ToList() };
        Recalc(po, po.Items); db.PurchaseOrders.Add(po); await db.SaveChangesAsync();
        r.Status = St.Converted; r.PurchaseOrderId = po.Id; await db.SaveChangesAsync();
        return await LoadPo(po.Id);
    });

    // ================= Purchase orders =================
    public Task<DocumentDto> CreateOrder(DocumentDto d) => db.InTx(async () =>
    {
        CheckLines(d.Lines, true); await Products(d.Lines.Select(l => l.ProductId));
        if (d.PartyId == null || !await db.Suppliers.AnyAsync(s => s.Id == d.PartyId)) throw new BusinessException("A valid supplier is required.");
        if (d.WarehouseId == null || !await db.Warehouses.AnyAsync(w => w.Id == d.WarehouseId)) throw new BusinessException("A valid receiving warehouse is required.");
        var po = new PurchaseOrder { Number = await num.Next(Prefix.PO), Date = Day(d.Date), SupplierId = d.PartyId.Value, WarehouseId = d.WarehouseId.Value, ExpectedDate = d.DueDate, Notes = d.Notes, CreatedBy = user.UserName,
            Items = d.Lines.Select(l => new PurchaseOrderItem { ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxRate = l.TaxRate, Notes = l.Notes }).ToList() };
        Recalc(po, po.Items); db.PurchaseOrders.Add(po); await db.SaveChangesAsync();
        return await LoadPo(po.Id);
    });
    public Task<DocumentDto> UpdateOrder(int id, DocumentDto d) => db.InTx(async () =>
    {
        var po = await db.PurchaseOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Purchase order");
        Require(po.Status, "Purchase order", St.Draft);
        CheckLines(d.Lines, true); await Products(d.Lines.Select(l => l.ProductId));
        if (d.PartyId == null || !await db.Suppliers.AnyAsync(s => s.Id == d.PartyId)) throw new BusinessException("A valid supplier is required.");
        db.RemoveRange(po.Items.ToList());
        po.Items = d.Lines.Select(l => new PurchaseOrderItem { ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxRate = l.TaxRate, Notes = l.Notes }).ToList();
        po.SupplierId = d.PartyId.Value; po.WarehouseId = d.WarehouseId ?? po.WarehouseId; po.Date = Day(d.Date); po.ExpectedDate = d.DueDate; po.Notes = d.Notes;
        Recalc(po, po.Items); await db.SaveChangesAsync(); return await LoadPo(id);
    });
    public Task<DocumentDto> SetOrderStatus(int id, string action) => db.InTx(async () =>
    {
        var po = await db.PurchaseOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Purchase order");
        switch (action)
        {
            case "approve": Require(po.Status, "Purchase order", St.Draft); po.Status = St.Approved; po.ApprovedBy = user.UserName; break;
            case "cancel":
                Require(po.Status, "Purchase order", St.Draft, St.Approved);
                if (po.Items.Any(i => i.ReceivedQuantity > 0)) throw new BusinessException("Cannot cancel an order that already has receipts.");
                po.Status = St.Cancelled; break;
            case "close": Require(po.Status, "Purchase order", St.Approved, St.PartiallyReceived, St.Received); po.Status = St.Closed; break;
            default: throw new BusinessException("Unknown action.");
        }
        await db.SaveChangesAsync(); return await LoadPo(id);
    });

    // ================= Goods receipts =================
    public Task<DocumentDto> CreateReceipt(DocumentDto d) => db.InTx(async () =>
    {
        var po = await db.PurchaseOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == d.RefId) ?? throw NF("Purchase order");
        Require(po.Status, "Purchase order", St.Approved, St.PartiallyReceived);
        var items = new List<GoodsReceiptItem>();
        foreach (var l in (d.Lines ?? new()).Where(l => l.Quantity > 0))
        {
            var pi = po.Items.FirstOrDefault(i => i.Id == l.RefLineId) ?? throw new BusinessException("A line does not belong to the selected purchase order.");
            var remaining = pi.Quantity - pi.ReceivedQuantity;
            if (l.Quantity > remaining) throw new BusinessException($"Received quantity ({l.Quantity}) exceeds the remaining ordered quantity ({remaining}).");
            if (l.RejectedQuantity < 0 || l.RejectedQuantity > l.Quantity) throw new BusinessException("Rejected quantity must be between 0 and the received quantity.");
            items.Add(new GoodsReceiptItem { ProductId = pi.ProductId, PurchaseOrderItemId = pi.Id, Quantity = l.Quantity, RejectedQuantity = l.RejectedQuantity, UnitPrice = pi.UnitPrice, TaxRate = pi.TaxRate, LineTotal = Math.Round(l.Quantity * pi.UnitPrice, 4), Notes = l.Notes });
        }
        if (items.Count == 0) throw new BusinessException("Enter a received quantity for at least one line.");
        var g = new GoodsReceipt { Number = await num.Next(Prefix.GRN), Date = Day(d.Date), PurchaseOrderId = po.Id, SupplierId = po.SupplierId, WarehouseId = d.WarehouseId ?? po.WarehouseId, Notes = d.Notes, CreatedBy = user.UserName, Items = items };
        db.GoodsReceipts.Add(g); await db.SaveChangesAsync();
        return await LoadGrn(g.Id);
    });
    /// <summary>Quality inspection passed: posts stock into the warehouse and the GL (Dr Inventory / Cr GR-IR).</summary>
    public Task<DocumentDto> ApproveReceipt(int id) => db.InTx(async () =>
    {
        var g = await db.GoodsReceipts.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Goods receipt");
        Require(g.Status, "Goods receipt", St.Draft);
        var po = await db.PurchaseOrders.Include(x => x.Items).FirstAsync(x => x.Id == g.PurchaseOrderId);
        if (po.Status is St.Cancelled or St.Closed) throw new BusinessException($"Purchase order is {po.Status}.");
        decimal value = 0;
        foreach (var it in g.Items)
        {
            var acc = it.Quantity - it.RejectedQuantity; if (acc <= 0) continue;
            var poi = po.Items.First(i => i.Id == it.PurchaseOrderItemId);
            if (poi.ReceivedQuantity + acc > poi.Quantity) throw new BusinessException("Accepted quantity exceeds the remaining ordered quantity.");
            await inv.Move(it.ProductId, g.WarehouseId, acc, TxType.PurchaseReceipt, it.UnitPrice, "GRN", g.Id, g.Number, date: g.Date);
            poi.ReceivedQuantity += acc; value += Math.Round(acc * it.UnitPrice, 4);
        }
        g.Status = St.Approved; g.QualityStatus = St.Passed;
        po.Status = po.Items.All(i => i.ReceivedQuantity >= i.Quantity) ? St.Received : St.PartiallyReceived;
        await acct.Post(g.Date, $"Goods receipt {g.Number}", "GRN", g.Id, $"GRN:{g.Id}", AccountingService.Dr(Acc.Inventory, value), AccountingService.Cr(Acc.GRNI, value));
        await db.SaveChangesAsync();
        return await LoadGrn(id);
    });
    public Task<DocumentDto> RejectReceipt(int id) => db.InTx(async () =>
    {
        var g = await db.GoodsReceipts.FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Goods receipt");
        Require(g.Status, "Goods receipt", St.Draft);
        g.Status = St.Rejected; g.QualityStatus = St.Failed; await db.SaveChangesAsync();
        return await LoadGrn(id);
    });

    // ================= Purchase invoices =================
    public Task<DocumentDto> CreateInvoice(DocumentDto d) => db.InTx(async () =>
    {
        var g = await db.GoodsReceipts.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == d.RefId) ?? throw NF("Goods receipt");
        if (g.Status != St.Approved) throw new BusinessException("Only approved goods receipts can be invoiced.");
        if (g.IsInvoiced) throw new BusinessException("This goods receipt has already been invoiced.", 409);
        var sup = await db.Suppliers.FirstAsync(x => x.Id == g.SupplierId);
        var items = g.Items.Where(i => i.Quantity - i.RejectedQuantity > 0).Select(i => new PurchaseInvoiceItem { ProductId = i.ProductId, Quantity = i.Quantity - i.RejectedQuantity, UnitPrice = i.UnitPrice, TaxRate = i.TaxRate }).ToList();
        if (items.Count == 0) throw new BusinessException("Nothing to invoice: all quantities were rejected.");
        var date = Day(d.Date);
        var pi = new PurchaseInvoice { Number = await num.Next(Prefix.PINV), Date = date, DueDate = d.DueDate ?? date.AddDays(sup.PaymentTermsDays), SupplierId = g.SupplierId, GoodsReceiptId = g.Id, PurchaseOrderId = g.PurchaseOrderId,
            SupplierInvoiceNo = d.Reference, Status = St.Unpaid, Notes = d.Notes, CreatedBy = user.UserName, Items = items };
        Recalc(pi, items); g.IsInvoiced = true; db.PurchaseInvoices.Add(pi); await db.SaveChangesAsync();
        await acct.Post(date, $"Purchase invoice {pi.Number} - {sup.Name}", "PurchaseInvoice", pi.Id, $"PINV:{pi.Id}",
            AccountingService.Dr(Acc.GRNI, pi.Subtotal), AccountingService.Dr(Acc.InputTax, pi.TaxAmount), AccountingService.Cr(Acc.AP, pi.Total));
        return await LoadInv(pi.Id);
    });

    // ================= Purchase returns =================
    public Task<DocumentDto> CreateReturn(DocumentDto d) => db.InTx(async () =>
    {
        var pinv = await db.PurchaseInvoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == d.RefId) ?? throw NF("Purchase invoice");
        var wh = d.WarehouseId ?? throw new BusinessException("The warehouse the goods are returned from is required.");
        CheckLines(d.Lines, false);
        var prior = (await db.PurchaseReturnItems.Where(i => i.PurchaseReturn!.PurchaseInvoiceId == pinv.Id).Select(i => new { i.ProductId, i.Quantity }).ToListAsync())
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var items = new List<PurchaseReturnItem>();
        foreach (var l in d.Lines)
        {
            var pii = pinv.Items.FirstOrDefault(i => i.ProductId == l.ProductId) ?? throw new BusinessException("Product is not on the original invoice.");
            var allowed = pii.Quantity - prior.GetValueOrDefault(l.ProductId);
            if (l.Quantity > allowed) throw new BusinessException($"Return quantity exceeds the returnable quantity ({allowed}).");
            items.Add(new PurchaseReturnItem { ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = pii.UnitPrice, TaxRate = pii.TaxRate });
        }
        var ret = new PurchaseReturn { Number = await num.Next(Prefix.PRET), Date = Day(d.Date), SupplierId = pinv.SupplierId, PurchaseInvoiceId = pinv.Id, WarehouseId = wh, Reason = d.Reference, Status = St.Posted, Notes = d.Notes, CreatedBy = user.UserName, Items = items };
        Recalc(ret, items); db.PurchaseReturns.Add(ret); await db.SaveChangesAsync();
        decimal invCost = 0;
        foreach (var i in items) { var tx = await inv.Move(i.ProductId, wh, -i.Quantity, TxType.PurchaseReturn, null, "PurchaseReturn", ret.Id, ret.Number, date: ret.Date); invCost += Math.Round(i.Quantity * tx.UnitCost, 4); }
        var variance = ret.Subtotal - invCost;
        await acct.Post(ret.Date, $"Purchase return {ret.Number}", "PurchaseReturn", ret.Id, $"PRET:{ret.Id}",
            AccountingService.Dr(Acc.AP, ret.Total), AccountingService.Cr(Acc.Inventory, invCost), AccountingService.Cr(Acc.InputTax, ret.TaxAmount),
            variance > 0 ? AccountingService.Cr(Acc.InvAdj, variance) : AccountingService.Dr(Acc.InvAdj, -variance));
        pinv.CreditedAmount += ret.Total; pinv.Status = Settle(pinv.Total, pinv.PaidAmount, pinv.CreditedAmount);
        await db.SaveChangesAsync();
        return (await Returns.AsNoTracking().FirstAsync(x => x.Id == ret.Id)).ToDto();
    });
}
