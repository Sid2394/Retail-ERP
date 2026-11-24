namespace MiniERP.Api.Services;

public class ManufacturingService(ErpDbContext db, ICurrentUser user, NumberingService num, InventoryService inv, AccountingService acct) : ServiceBase
{
    // ================= BOM =================
    public IQueryable<BillOfMaterial> Boms => db.BillsOfMaterials.Include(b => b.Product).Include(b => b.Items).ThenInclude(i => i.ComponentProduct);
    public static BomDto ToDto(BillOfMaterial b) => new()
    {
        Id = b.Id, Code = b.Code, Name = b.Name, ProductId = b.ProductId, ProductName = b.Product?.Name, OutputQuantity = b.OutputQuantity, Version = b.Version, IsActive = b.IsActive, Notes = b.Notes,
        Items = b.Items.Select(i => new BomItemDto { Id = i.Id, ComponentProductId = i.ComponentProductId, ComponentName = i.ComponentProduct?.Name, Sku = i.ComponentProduct?.Sku, Quantity = i.Quantity, ScrapPercent = i.ScrapPercent }).ToList()
    };
    async Task ValidateBom(BomDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Code) || string.IsNullOrWhiteSpace(d.Name)) throw new BusinessException("BOM code and name are required.");
        if (d.OutputQuantity <= 0) throw new BusinessException("Output quantity must be greater than zero.");
        if (d.Items.Count == 0) throw new BusinessException("A BOM needs at least one component.");
        if (d.Items.Any(i => i.Quantity <= 0 || i.ScrapPercent < 0)) throw new BusinessException("Component quantity must be positive.");
        if (d.Items.Any(i => i.ComponentProductId == d.ProductId)) throw new BusinessException("A product cannot be a component of itself.");
        if (d.Items.GroupBy(i => i.ComponentProductId).Any(g => g.Count() > 1)) throw new BusinessException("Duplicate components are not allowed.");
        if (!await db.Products.AnyAsync(p => p.Id == d.ProductId)) throw NF("Finished product");
        var ids = d.Items.Select(i => i.ComponentProductId).ToList();
        if (await db.Products.CountAsync(p => ids.Contains(p.Id)) != ids.Count) throw NF("Component product");
    }
    public Task<BomDto> SaveBom(int? id, BomDto d) => db.InTx(async () =>
    {
        await ValidateBom(d);
        BillOfMaterial b;
        if (id == null) { b = new BillOfMaterial(); db.BillsOfMaterials.Add(b); }
        else { b = await db.BillsOfMaterials.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Bill of materials"); db.RemoveRange(b.Items.ToList()); }
        b.Code = d.Code.Trim(); b.Name = d.Name.Trim(); b.ProductId = d.ProductId; b.OutputQuantity = d.OutputQuantity; b.Version = d.Version; b.IsActive = d.IsActive; b.Notes = d.Notes;
        b.Items = d.Items.Select(i => new BomItem { ComponentProductId = i.ComponentProductId, Quantity = i.Quantity, ScrapPercent = i.ScrapPercent }).ToList();
        await db.SaveChangesAsync();
        return ToDto(await Boms.AsNoTracking().FirstAsync(x => x.Id == b.Id));
    });
    public async Task DeleteBom(int id)
    {
        var b = await db.BillsOfMaterials.FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Bill of materials");
        if (await db.ProductionOrders.AnyAsync(p => p.BillOfMaterialId == id)) throw new BusinessException("This BOM is used by production orders; deactivate it instead.");
        b.IsDeleted = true; await db.SaveChangesAsync();
    }

    // ================= Material requirement planning =================
    public async Task<object> Plan(PlanRequest r)
    {
        if (r.Quantity <= 0) throw new BusinessException("Quantity must be greater than zero.");
        var bom = await Boms.AsNoTracking().Where(b => b.ProductId == r.ProductId && b.IsActive).OrderByDescending(b => b.Id).FirstOrDefaultAsync() ?? throw new BusinessException("No active BOM exists for this product.");
        var scale = r.Quantity / bom.OutputQuantity;
        var rows = new List<ComponentDto>();
        foreach (var i in bom.Items)
        {
            var required = Math.Round(i.Quantity * scale * (1 + i.ScrapPercent / 100m), 4);
            var avail = r.WarehouseId.HasValue ? await inv.Available(i.ComponentProductId, r.WarehouseId.Value) : await inv.TotalOnHand(i.ComponentProductId);
            var cost = i.ComponentProduct?.AverageCost ?? 0;
            rows.Add(new ComponentDto { ProductId = i.ComponentProductId, Sku = i.ComponentProduct?.Sku, ProductName = i.ComponentProduct?.Name, Required = required, Available = avail, Shortage = Math.Max(0, required - avail), UnitCost = cost, EstimatedCost = Math.Round(required * cost, 4) });
        }
        return new { bomId = bom.Id, bomCode = bom.Code, productId = bom.ProductId, productName = bom.Product?.Name, quantity = r.Quantity, components = rows, estimatedMaterialCost = rows.Sum(x => x.EstimatedCost), canProduce = rows.All(x => x.Shortage == 0) };
    }
    public Task<DocumentDto> RequisitionForShortages(PlanRequest r) => db.InTx(async () =>
    {
        var bom = await Boms.AsNoTracking().Where(b => b.ProductId == r.ProductId && b.IsActive).OrderByDescending(b => b.Id).FirstOrDefaultAsync() ?? throw new BusinessException("No active BOM exists for this product.");
        var scale = r.Quantity / bom.OutputQuantity; var lines = new List<PurchaseRequisitionItem>();
        foreach (var i in bom.Items)
        {
            var required = Math.Round(i.Quantity * scale * (1 + i.ScrapPercent / 100m), 4);
            var avail = r.WarehouseId.HasValue ? await inv.Available(i.ComponentProductId, r.WarehouseId.Value) : await inv.TotalOnHand(i.ComponentProductId);
            if (required > avail) lines.Add(new PurchaseRequisitionItem { ProductId = i.ComponentProductId, Quantity = required - avail, Notes = "MRP shortage" });
        }
        if (lines.Count == 0) throw new BusinessException("No material shortages - a requisition is not needed.");
        var pr = new PurchaseRequisition { Number = await num.Next(Prefix.PR), Date = DateTime.UtcNow.Date, RequestedBy = user.UserName, CreatedBy = user.UserName, Notes = $"MRP for {r.Quantity} x {bom.Product?.Name}", Items = lines };
        db.PurchaseRequisitions.Add(pr); await db.SaveChangesAsync();
        return (await db.PurchaseRequisitions.Include(x => x.Items).ThenInclude(i => i.Product).AsNoTracking().FirstAsync(x => x.Id == pr.Id)).ToDto();
    });

    // ================= Production orders =================
    public IQueryable<ProductionOrder> Orders => db.ProductionOrders.Include(o => o.Product).Include(o => o.BillOfMaterial).Include(o => o.Components).ThenInclude(c => c.Product);
    public async Task<ProductionOrderDto> ToDto(ProductionOrder o)
    {
        var dto = new ProductionOrderDto
        {
            Id = o.Id, Number = o.Number, Date = o.Date, DueDate = o.DueDate, Status = o.Status, QualityStatus = o.QualityStatus, ProductId = o.ProductId, ProductName = o.Product?.Name, BillOfMaterialId = o.BillOfMaterialId, BomCode = o.BillOfMaterial?.Code,
            Quantity = o.Quantity, QuantityProduced = o.QuantityProduced, SourceWarehouseId = o.SourceWarehouseId, TargetWarehouseId = o.TargetWarehouseId, Notes = o.Notes,
            MaterialCost = o.MaterialCost, LaborCost = o.LaborCost, OverheadCost = o.OverheadCost, TotalCost = o.TotalCost, UnitCost = o.UnitCost
        };
        foreach (var c in o.Components)
        {
            var avail = await inv.Available(c.ProductId, o.SourceWarehouseId); var remaining = Math.Max(0, c.RequiredQuantity - c.IssuedQuantity);
            dto.Components.Add(new ComponentDto { ProductId = c.ProductId, Sku = c.Product?.Sku, ProductName = c.Product?.Name, Required = c.RequiredQuantity, Issued = c.IssuedQuantity, Available = avail, Shortage = Math.Max(0, remaining - avail), UnitCost = c.UnitCost, EstimatedCost = Math.Round(c.RequiredQuantity * c.UnitCost, 4) });
        }
        return dto;
    }
    async Task<ProductionOrderDto> Load(int id) => await ToDto(await Orders.AsNoTracking().FirstAsync(x => x.Id == id));

    public Task<ProductionOrderDto> CreateOrder(CreateProductionRequest r) => db.InTx(async () =>
    {
        if (r.Quantity <= 0) throw new BusinessException("Quantity must be greater than zero.");
        if (!await db.Warehouses.AnyAsync(w => w.Id == r.SourceWarehouseId) || !await db.Warehouses.AnyAsync(w => w.Id == r.TargetWarehouseId)) throw new BusinessException("Valid source and target warehouses are required.");
        var bom = await db.BillsOfMaterials.Include(b => b.Items).ThenInclude(i => i.ComponentProduct).Where(b => b.IsActive && (r.BomId != null ? b.Id == r.BomId : b.ProductId == r.ProductId)).OrderByDescending(b => b.Id).FirstOrDefaultAsync()
            ?? throw new BusinessException("No active BOM exists for this product.");
        var scale = r.Quantity / bom.OutputQuantity;
        var o = new ProductionOrder { Number = await num.Next(Prefix.PRD), Date = Day(r.Date ?? default), DueDate = r.DueDate, BillOfMaterialId = bom.Id, ProductId = bom.ProductId, Quantity = r.Quantity, SourceWarehouseId = r.SourceWarehouseId, TargetWarehouseId = r.TargetWarehouseId, Notes = r.Notes, CreatedBy = user.UserName, Status = St.Planned,
            Components = bom.Items.Select(i => new ProductionComponent { ProductId = i.ComponentProductId, RequiredQuantity = Math.Round(i.Quantity * scale * (1 + i.ScrapPercent / 100m), 4), UnitCost = i.ComponentProduct?.AverageCost ?? 0 }).ToList() };
        db.ProductionOrders.Add(o); await db.SaveChangesAsync();
        return await Load(o.Id);
    });
    /// <summary>Release performs the material availability check.</summary>
    public Task<ProductionOrderDto> Release(int id) => db.InTx(async () =>
    {
        var o = await db.ProductionOrders.Include(x => x.Components).ThenInclude(c => c.Product).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Production order");
        Require(o.Status, "Production order", St.Planned);
        var shortages = new List<string>();
        foreach (var c in o.Components) { var a = await inv.Available(c.ProductId, o.SourceWarehouseId); if (a < c.RequiredQuantity) shortages.Add($"{c.Product?.Sku}: required {c.RequiredQuantity}, available {a}"); }
        if (shortages.Count > 0) throw new BusinessException("Material shortage - " + string.Join("; ", shortages));
        o.Status = St.Released; await db.SaveChangesAsync(); return await Load(id);
    });
    public Task<ProductionOrderDto> Cancel(int id) => db.InTx(async () =>
    {
        var o = await db.ProductionOrders.Include(x => x.Components).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Production order");
        Require(o.Status, "Production order", St.Planned, St.Released, St.InProgress);
        if (o.Components.Any(c => c.IssuedQuantity > 0)) throw new BusinessException("Materials were already issued; complete the order instead of cancelling.");
        o.Status = St.Cancelled; await db.SaveChangesAsync(); return await Load(id);
    });
    /// <summary>Issues (consumes) raw materials: stock out + Dr WIP / Cr Inventory.</summary>
    public Task<ProductionOrderDto> IssueMaterials(int id, IssueRequest? req) => db.InTx(async () =>
    {
        var o = await db.ProductionOrders.Include(x => x.Components).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Production order");
        Require(o.Status, "Production order", St.Released, St.InProgress);
        var plan = new List<(ProductionComponent C, decimal Qty)>();
        if (req?.Lines is { Count: > 0 })
            foreach (var l in req.Lines.Where(l => l.Quantity > 0))
            {
                var c = o.Components.FirstOrDefault(x => x.ProductId == l.ProductId) ?? throw new BusinessException("Product is not a component of this order.");
                if (c.IssuedQuantity + l.Quantity > c.RequiredQuantity + 0.0001m) throw new BusinessException("Cannot issue more than the required quantity.");
                plan.Add((c, l.Quantity));
            }
        else foreach (var c in o.Components) { var rem = c.RequiredQuantity - c.IssuedQuantity; if (rem > 0) plan.Add((c, rem)); }
        if (plan.Count == 0) throw new BusinessException("There is nothing left to issue.");
        var mi = new MaterialIssue { Number = await num.Next(Prefix.MI), Date = DateTime.UtcNow.Date, ProductionOrderId = o.Id, WarehouseId = o.SourceWarehouseId, Status = St.Posted, CreatedBy = user.UserName };
        db.MaterialIssues.Add(mi); await db.SaveChangesAsync();
        decimal total = 0;
        foreach (var (c, qty) in plan)
        {
            var tx = await inv.Move(c.ProductId, o.SourceWarehouseId, -qty, TxType.MaterialIssue, null, "MaterialIssue", mi.Id, mi.Number, date: mi.Date);
            var cost = Math.Round(qty * tx.UnitCost, 4); total += cost;
            c.UnitCost = c.IssuedQuantity + qty > 0 ? Math.Round((c.IssuedQuantity * c.UnitCost + cost) / (c.IssuedQuantity + qty), 4) : tx.UnitCost; c.IssuedQuantity += qty;
            db.MaterialIssueItems.Add(new MaterialIssueItem { MaterialIssueId = mi.Id, ProductId = c.ProductId, Quantity = qty, UnitPrice = tx.UnitCost, LineTotal = cost });
        }
        o.MaterialCost += total; o.Status = St.InProgress;
        await acct.Post(mi.Date, $"Material issue {mi.Number} for {o.Number}", "MaterialIssue", mi.Id, $"MI:{mi.Id}", AccountingService.Dr(Acc.WIP, total), AccountingService.Cr(Acc.Inventory, total));
        await db.SaveChangesAsync(); return await Load(id);
    });
    /// <summary>Quality check + finished goods receipt + production costing.</summary>
    public Task<ProductionOrderDto> Complete(int id, CompleteProductionRequest r) => db.InTx(async () =>
    {
        var o = await db.ProductionOrders.Include(x => x.Components).FirstOrDefaultAsync(x => x.Id == id) ?? throw NF("Production order");
        Require(o.Status, "Production order", St.InProgress);
        if (r.LaborCost < 0 || r.OverheadCost < 0) throw new BusinessException("Costs cannot be negative.");
        if (o.Components.Any(c => c.IssuedQuantity + 0.0001m < c.RequiredQuantity)) throw new BusinessException("All required materials must be issued before completion.");
        o.LaborCost = r.LaborCost; o.OverheadCost = r.OverheadCost; o.QualityStatus = r.QualityStatus == St.Failed ? St.Failed : St.Passed;
        if (!string.IsNullOrWhiteSpace(r.Notes)) o.Notes = r.Notes;
        var date = DateTime.UtcNow.Date;
        if (o.QualityStatus == St.Failed)
        {
            o.Status = St.Rejected; o.QuantityProduced = 0; o.TotalCost = o.MaterialCost + o.LaborCost + o.OverheadCost; o.UnitCost = 0;
            await acct.Post(date, $"Production {o.Number} failed quality check - scrapped", "ProductionOrder", o.Id, $"PRDX:{o.Id}", AccountingService.Dr(Acc.InvAdj, o.MaterialCost), AccountingService.Cr(Acc.WIP, o.MaterialCost));
            await db.SaveChangesAsync(); return await Load(id);
        }
        var qty = r.QuantityProduced > 0 ? r.QuantityProduced : o.Quantity;
        o.QuantityProduced = qty; o.TotalCost = o.MaterialCost + o.LaborCost + o.OverheadCost; o.UnitCost = Math.Round(o.TotalCost / qty, 4);
        var rec = new ProductionReceipt { Number = await num.Next(Prefix.FGR), Date = date, ProductionOrderId = o.Id, ProductId = o.ProductId, WarehouseId = o.TargetWarehouseId, QuantityProduced = qty, QualityStatus = St.Passed, UnitCost = o.UnitCost, TotalCost = Math.Round(qty * o.UnitCost, 4), Status = St.Posted, CreatedBy = user.UserName };
        db.ProductionReceipts.Add(rec); await db.SaveChangesAsync();
        await inv.Move(o.ProductId, o.TargetWarehouseId, qty, TxType.ProductionReceipt, o.UnitCost, "ProductionReceipt", rec.Id, rec.Number, date: date);
        var absorbed = rec.TotalCost - o.MaterialCost;
        await acct.Post(date, $"Finished goods receipt {rec.Number} for {o.Number}", "ProductionReceipt", rec.Id, $"FGR:{rec.Id}",
            AccountingService.Dr(Acc.Inventory, rec.TotalCost), AccountingService.Cr(Acc.WIP, o.MaterialCost),
            absorbed >= 0 ? AccountingService.Cr(Acc.Overhead, absorbed) : AccountingService.Dr(Acc.Overhead, -absorbed));
        o.Status = St.Completed; await db.SaveChangesAsync(); return await Load(id);
    });

    public IQueryable<MaterialIssue> Issues => db.MaterialIssues.Include(x => x.Warehouse).Include(x => x.ProductionOrder).Include(x => x.Items).ThenInclude(i => i.Product);
    public IQueryable<ProductionReceipt> Receipts => db.ProductionReceipts.Include(x => x.Product).Include(x => x.Warehouse).Include(x => x.ProductionOrder);
}
