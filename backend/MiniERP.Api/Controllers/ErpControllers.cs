using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiniERP.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AuthService auth, ErpDbContext db) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous] public async Task<LoginResponse> Login(LoginRequest r) => await auth.Login(r);
    [HttpPost("logout"), Authorize] public IActionResult Logout() => Ok(new { message = "Logged out. Discard the token on the client." });
    [HttpGet("me"), Authorize]
    public async Task<AuthUser> Me() => new()
    {
        Id = int.Parse(User.FindFirst("uid")!.Value), Username = User.Identity!.Name!, FullName = User.FindFirst("fullName")?.Value ?? "", Role = User.FindFirst("role")?.Value ?? "",
        Permissions = User.FindAll("perm").Select(c => c.Value).OrderBy(x => x).ToList()
    };
}

[ApiController, Route("api"), Authorize(Policy = "admin.manage")]
public class AdminController(AuthService auth, ErpDbContext db, ICurrentUser me) : ControllerBase
{
    [HttpGet("users")] public Task<PagedResult<UserDto>> Users([FromQuery] PageQuery q) => auth.ListUsers(q);
    [HttpPost("users")] public Task<UserDto> CreateUser(UserDto d) => auth.SaveUser(null, d);
    [HttpPut("users/{id:int}")] public Task<UserDto> UpdateUser(int id, UserDto d) => auth.SaveUser(id, d);
    [HttpDelete("users/{id:int}")] public async Task<IActionResult> DeleteUser(int id) { await auth.DeleteUser(id, me.UserId); return NoContent(); }
    [HttpGet("roles")] public async Task<IActionResult> Roles() => Ok(new PagedResult<RoleDto> { Items = await auth.Roles(), Total = (await auth.Roles()).Count, Page = 1, PageSize = 1000 });
    [HttpPost("roles")] public Task<RoleDto> CreateRole(RoleDto d) => auth.SaveRole(null, d);
    [HttpPut("roles/{id:int}")] public Task<RoleDto> UpdateRole(int id, RoleDto d) => auth.SaveRole(id, d);
    [HttpDelete("roles/{id:int}")] public async Task<IActionResult> DeleteRole(int id) { await auth.DeleteRole(id); return NoContent(); }
    [HttpGet("permissions")] public async Task<IActionResult> Permissions() => Ok(await db.Permissions.AsNoTracking().OrderBy(p => p.Code).Select(p => new { p.Id, p.Code, p.Description }).ToListAsync());
    [HttpGet("audit-logs")]
    public async Task<PagedResult<AuditLog>> Audit([FromQuery] PageQuery q)
    {
        var x = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search)) x = x.Where(a => a.EntityName.Contains(q.Search) || (a.UserName != null && a.UserName.Contains(q.Search)) || a.Action.Contains(q.Search));
        var total = await x.CountAsync(); var size = Math.Clamp(q.PageSize, 1, 1000);
        return new PagedResult<AuditLog> { Items = await x.OrderByDescending(a => a.Id).Skip((Math.Max(q.Page, 1) - 1) * size).Take(size).ToListAsync(), Total = total, Page = q.Page, PageSize = size };
    }
    [HttpGet("settings"), Authorize]
    public async Task<Dictionary<string, string?>> Settings() => await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
    [HttpPut("settings")]
    public async Task<Dictionary<string, string?>> SaveSettings(Dictionary<string, string?> d)
    {
        foreach (var (k, v) in d)
        {
            var s = await db.Settings.FirstOrDefaultAsync(x => x.Key == k);
            if (s == null) db.Settings.Add(new Setting { Key = k, Value = v }); else s.Value = v;
        }
        await db.SaveChangesAsync(); return await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
    }
}

[ApiController, Route("api"), Authorize]
public class CommonController(ErpDbContext db, DashboardService dash) : ControllerBase
{
    [HttpGet("dashboard")] public async Task<object> Dashboard() => await dash.Get();
    [HttpGet("notifications")] public async Task<object> Notifications() => await db.Notifications.AsNoTracking().OrderByDescending(n => n.CreatedAt).Take(30).ToListAsync();
    [HttpPost("notifications/{id:int}/read")] public async Task<IActionResult> Read(int id) { var n = await db.Notifications.FindAsync(id); if (n != null) { n.IsRead = true; await db.SaveChangesAsync(); } return NoContent(); }
    [HttpPost("notifications/read-all")] public async Task<IActionResult> ReadAll() { await db.Notifications.Where(n => !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true)); return NoContent(); }
    [HttpGet("settings/public")] public async Task<Dictionary<string, string?>> PublicSettings() => await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
}

[ApiController, Route("api"), Authorize]
public class InventoryController(ErpDbContext db, InventoryService inv) : ControllerBase
{
    static ProductDto P(Product p, decimal onHand) => new() { Id = p.Id, Sku = p.Sku, Name = p.Name, Description = p.Description, CategoryId = p.CategoryId, CategoryName = p.Category?.Name, UnitId = p.UnitId, UnitName = p.Unit?.Symbol, ProductType = p.ProductType, AverageCost = p.AverageCost, SalePrice = p.SalePrice, TaxRate = p.TaxRate, ReorderLevel = p.ReorderLevel, IsActive = p.IsActive, OnHand = onHand };

    [HttpGet("products"), Authorize("inventory.view")]
    public async Task<PagedResult<ProductDto>> Products([FromQuery] PageQuery q)
    {
        var x = db.Products.Include(p => p.Category).Include(p => p.Unit).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search)) x = x.Where(p => p.Name.Contains(q.Search) || p.Sku.Contains(q.Search));
        if (!string.IsNullOrWhiteSpace(q.Type)) x = x.Where(p => p.ProductType == q.Type);
        var total = await x.CountAsync(); var asc = string.Equals(q.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        x = (q.SortBy?.ToLower()) switch { "name" => asc ? x.OrderBy(p => p.Name) : x.OrderByDescending(p => p.Name), "sku" => asc ? x.OrderBy(p => p.Sku) : x.OrderByDescending(p => p.Sku), "salePrice" or "saleprice" => asc ? x.OrderBy(p => p.SalePrice) : x.OrderByDescending(p => p.SalePrice), _ => x.OrderBy(p => p.Sku) };
        var size = Math.Clamp(q.PageSize, 1, 100000);
        var items = await x.Skip((Math.Max(q.Page, 1) - 1) * size).Take(size).ToListAsync();
        var ids = items.Select(p => p.Id).ToList();
        var on = await db.StockBalances.Where(b => ids.Contains(b.ProductId)).GroupBy(b => b.ProductId).Select(g => new { g.Key, Q = g.Sum(b => b.Quantity) }).ToDictionaryAsync(a => a.Key, a => a.Q);
        return new PagedResult<ProductDto> { Items = items.Select(p => P(p, on.GetValueOrDefault(p.Id))).ToList(), Total = total, Page = q.Page, PageSize = size };
    }
    [HttpGet("products/{id:int}"), Authorize("inventory.view")]
    public async Task<ProductDto> Product(int id) { var p = await db.Products.Include(x => x.Category).Include(x => x.Unit).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new BusinessException("Product not found.", 404); return P(p, await inv.TotalOnHand(id)); }
    async Task Check(ProductDto d, int id)
    {
        if (string.IsNullOrWhiteSpace(d.Sku) || string.IsNullOrWhiteSpace(d.Name)) throw new BusinessException("SKU and name are required.");
        if (!new[] { ProductTypes.RawMaterial, ProductTypes.FinishedGood, ProductTypes.TradingProduct }.Contains(d.ProductType)) throw new BusinessException("Invalid product type.");
        if (d.SalePrice < 0 || d.ReorderLevel < 0 || d.TaxRate < 0) throw new BusinessException("Prices, tax and reorder level cannot be negative.");
        if (await db.Products.AnyAsync(p => p.Sku == d.Sku && p.Id != id)) throw new BusinessException("SKU already exists.", 409);
        if (!await db.Categories.AnyAsync(c => c.Id == d.CategoryId)) throw new BusinessException("Select a valid category.");
        if (!await db.Units.AnyAsync(u => u.Id == d.UnitId)) throw new BusinessException("Select a valid unit.");
    }
    [HttpPost("products"), Authorize("inventory.manage")]
    public async Task<ProductDto> CreateProduct(ProductDto d)
    {
        await Check(d, 0);
        var p = new Product { Sku = d.Sku.Trim(), Name = d.Name.Trim(), Description = d.Description, CategoryId = d.CategoryId, UnitId = d.UnitId, ProductType = d.ProductType, SalePrice = d.SalePrice, TaxRate = d.TaxRate, ReorderLevel = d.ReorderLevel, IsActive = d.IsActive };
        db.Products.Add(p); await db.SaveChangesAsync(); return await Product(p.Id);
    }
    [HttpPut("products/{id:int}"), Authorize("inventory.manage")]
    public async Task<ProductDto> UpdateProduct(int id, ProductDto d)
    {
        await Check(d, id);
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BusinessException("Product not found.", 404);
        p.Sku = d.Sku.Trim(); p.Name = d.Name.Trim(); p.Description = d.Description; p.CategoryId = d.CategoryId; p.UnitId = d.UnitId; p.ProductType = d.ProductType; p.SalePrice = d.SalePrice; p.TaxRate = d.TaxRate; p.ReorderLevel = d.ReorderLevel; p.IsActive = d.IsActive;
        await db.SaveChangesAsync(); return await Product(id);   // AverageCost is only changed by stock movements
    }
    [HttpDelete("products/{id:int}"), Authorize("inventory.manage")]
    

[HttpDelete("products/{id:int}"), Authorize("inventory.manage")]
public async Task<IActionResult> DeleteProduct(int id)
{
    // Find the product, including previously soft-deleted products.
    var p = await db.Products
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == id)
        ?? throw new BusinessException("Product not found.", 404);

    // A product with stock cannot be deleted.
    if (await inv.TotalOnHand(id) != 0)
        throw new BusinessException(
            "Product still has stock on hand. Remove its stock before deleting."
        );

    // Check whether the product is used in any business records.
    var isReferenced =
        await db.StockTransactions.AnyAsync(x => x.ProductId == id) ||
        await db.StockTransferItems.AnyAsync(x => x.ProductId == id) ||

        await db.PurchaseRequisitionItems.AnyAsync(x => x.ProductId == id) ||
        await db.PurchaseOrderItems.AnyAsync(x => x.ProductId == id) ||
        await db.GoodsReceiptItems.AnyAsync(x => x.ProductId == id) ||
        await db.PurchaseInvoiceItems.AnyAsync(x => x.ProductId == id) ||
        await db.PurchaseReturnItems.AnyAsync(x => x.ProductId == id) ||

        await db.SalesQuotationItems.AnyAsync(x => x.ProductId == id) ||
        await db.SalesOrderItems.AnyAsync(x => x.ProductId == id) ||
        await db.DeliveryItems.AnyAsync(x => x.ProductId == id) ||
        await db.SalesInvoiceItems.AnyAsync(x => x.ProductId == id) ||
        await db.SalesReturnItems.AnyAsync(x => x.ProductId == id) ||

        await db.BillsOfMaterials.AnyAsync(x => x.ProductId == id) ||
        await db.BomItems.AnyAsync(x => x.ComponentProductId == id) ||
        await db.ProductionOrders.AnyAsync(x => x.ProductId == id) ||
        await db.ProductionComponents.AnyAsync(x => x.ProductId == id) ||
        await db.MaterialIssueItems.AnyAsync(x => x.ProductId == id) ||
        await db.ProductionReceipts.AnyAsync(x => x.ProductId == id);

    if (isReferenced)
        throw new BusinessException(
            "This product is used in historical transactions or documents. " +
            "It cannot be permanently deleted."
        );

    // Remove zero-quantity stock balance records.
    var balances = await db.StockBalances
        .Where(x => x.ProductId == id)
        .ToListAsync();

    db.StockBalances.RemoveRange(balances);

    // Permanently delete the product from SQL Server.
    db.Products.Remove(p);

    await db.SaveChangesAsync();

    return NoContent();
}


    [HttpGet("stock/balances"), Authorize("inventory.view")]
    public async Task<PagedResult<object>> Balances([FromQuery] PageQuery q) => PagingObjects.Rows((await db.StockBalances.Include(b => b.Product).Include(b => b.Warehouse).AsNoTracking()
        .Where(b => (q.WarehouseId == null || b.WarehouseId == q.WarehouseId) && (q.ProductId == null || b.ProductId == q.ProductId)).ToListAsync())
        .OrderBy(b => b.Product!.Sku).Select(b => (object)new { id = b.Id, sku = b.Product!.Sku, product = b.Product.Name, warehouse = b.Warehouse!.Name, quantity = b.Quantity, avgCost = b.Product.AverageCost, value = Math.Round(b.Quantity * b.Product.AverageCost, 2), reorderLevel = b.Product.ReorderLevel, status = b.Product.ReorderLevel > 0 && b.Quantity <= b.Product.ReorderLevel ? "Low" : "OK" }), q);
    [HttpGet("stock/transactions"), Authorize("inventory.view")]
    public async Task<PagedResult<object>> Transactions([FromQuery] PageQuery q)
    {
        var x = db.StockTransactions.Include(t => t.Product).Include(t => t.Warehouse).AsNoTracking().AsQueryable();
        if (q.ProductId != null) x = x.Where(t => t.ProductId == q.ProductId); if (q.WarehouseId != null) x = x.Where(t => t.WarehouseId == q.WarehouseId);
        if (!string.IsNullOrWhiteSpace(q.Type)) x = x.Where(t => t.TransactionType == q.Type);
        if (q.From != null) x = x.Where(t => t.Date >= q.From.Value.Date); if (q.To != null) x = x.Where(t => t.Date < q.To.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Search)) x = x.Where(t => t.Product!.Name.Contains(q.Search) || t.Product.Sku.Contains(q.Search) || (t.ReferenceNo != null && t.ReferenceNo.Contains(q.Search)));
        var total = await x.CountAsync(); var size = Math.Clamp(q.PageSize, 1, 100000);
        var rows = await x.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).Skip((Math.Max(q.Page, 1) - 1) * size).Take(size).ToListAsync();
        return new PagedResult<object> { Items = rows.Select(t => (object)new { id = t.Id, date = t.Date, sku = t.Product!.Sku, product = t.Product.Name, warehouse = t.Warehouse!.Name, type = t.TransactionType, quantity = t.Quantity, unitCost = t.UnitCost, reference = t.ReferenceNo ?? t.ReferenceType, notes = t.Notes, user = t.CreatedBy }).ToList(), Total = total, Page = q.Page, PageSize = size };
    }
    [HttpPost("stock/movement"), Authorize("inventory.manage")]
    public async Task<object> Movement(MovementRequest r) { var t = await inv.Movement(r); return new { t.Id, t.TransactionType, t.Quantity, t.UnitCost }; }

    [HttpGet("stock-transfers"), Authorize("inventory.view")] public Task<PagedResult<DocumentDto>> Transfers([FromQuery] PageQuery q) => Paging.Docs(inv.Transfers.AsNoTracking(), q, x => x.ToDto());
    [HttpGet("stock-transfers/{id:int}"), Authorize("inventory.view")] public Task<DocumentDto> Transfer(int id) => Paging.One(inv.Transfers, id, x => x.ToDto());
    [HttpPost("stock-transfers"), Authorize("inventory.manage")] public Task<DocumentDto> CreateTransfer(DocumentDto d) => inv.CreateTransfer(d);
    [HttpPost("stock-transfers/{id:int}/complete"), Authorize("inventory.manage")] public Task<DocumentDto> CompleteTransfer(int id) => inv.CompleteTransfer(id);
}

[ApiController, Route("api"), Authorize]
public class ProcurementController(ProcurementService s, FinanceService fin) : ControllerBase
{
    [HttpGet("purchase-requisitions"), Authorize("procurement.view")] public Task<PagedResult<DocumentDto>> Reqs([FromQuery] PageQuery q) => Paging.Docs(s.Requisitions.AsNoTracking(), q, x => x.ToDto());
    [HttpGet("purchase-requisitions/{id:int}"), Authorize("procurement.view")] public Task<DocumentDto> Req(int id) => Paging.One(s.Requisitions, id, x => x.ToDto());
    [HttpPost("purchase-requisitions"), Authorize("procurement.manage")] public Task<DocumentDto> CreateReq(DocumentDto d) => s.CreateRequisition(d);
    [HttpPut("purchase-requisitions/{id:int}"), Authorize("procurement.manage")] public Task<DocumentDto> UpdateReq(int id, DocumentDto d) => s.UpdateRequisition(id, d);
    [HttpPost("purchase-requisitions/{id:int}/submit"), Authorize("procurement.manage")] public Task<DocumentDto> Submit(int id) => s.SetRequisitionStatus(id, "submit");
    [HttpPost("purchase-requisitions/{id:int}/approve"), Authorize("procurement.approve")] public Task<DocumentDto> ApproveReq(int id) => s.SetRequisitionStatus(id, "approve");
    [HttpPost("purchase-requisitions/{id:int}/reject"), Authorize("procurement.approve")] public Task<DocumentDto> RejectReq(int id) => s.SetRequisitionStatus(id, "reject");
    [HttpPost("purchase-requisitions/{id:int}/convert"), Authorize("procurement.manage")] public Task<DocumentDto> Convert(int id, ConvertRequest c) => s.ConvertRequisition(id, c);

    [HttpGet("purchase-orders"), Authorize("procurement.view")] public Task<PagedResult<DocumentDto>> Orders([FromQuery] PageQuery q) => Paging.Docs(s.Orders.AsNoTracking(), q, x => x.ToDto());
    [HttpGet("purchase-orders/{id:int}"), Authorize("procurement.view")] public Task<DocumentDto> Order(int id) => Paging.One(s.Orders, id, x => x.ToDto());
    [HttpPost("purchase-orders"), Authorize("procurement.manage")] public Task<DocumentDto> CreateOrder(DocumentDto d) => s.CreateOrder(d);
    [HttpPut("purchase-orders/{id:int}"), Authorize("procurement.manage")] public Task<DocumentDto> UpdateOrder(int id, DocumentDto d) => s.UpdateOrder(id, d);
    [HttpPost("purchase-orders/{id:int}/approve"), Authorize("procurement.approve")] public Task<DocumentDto> ApproveOrder(int id) => s.SetOrderStatus(id, "approve");
    [HttpPost("purchase-orders/{id:int}/cancel"), Authorize("procurement.manage")] public Task<DocumentDto> CancelOrder(int id) => s.SetOrderStatus(id, "cancel");
    [HttpPost("purchase-orders/{id:int}/close"), Authorize("procurement.manage")] public Task<DocumentDto> CloseOrder(int id) => s.SetOrderStatus(id, "close");

    [HttpGet("goods-receipts"), Authorize("procurement.view")] public Task<PagedResult<DocumentDto>> Receipts([FromQuery] PageQuery q) => Paging.Docs(s.Receipts.AsNoTracking(), q, x => x.ToDto());
    [HttpGet("goods-receipts/{id:int}"), Authorize("procurement.view")] public Task<DocumentDto> Receipt(int id) => Paging.One(s.Receipts, id, x => x.ToDto());
    [HttpPost("goods-receipts"), Authorize("procurement.manage")] public Task<DocumentDto> CreateReceipt(DocumentDto d) => s.CreateReceipt(d);
    [HttpPost("goods-receipts/{id:int}/approve"), Authorize("procurement.manage")] public Task<DocumentDto> ApproveReceipt(int id) => s.ApproveReceipt(id);
    [HttpPost("goods-receipts/{id:int}/reject"), Authorize("procurement.manage")] public Task<DocumentDto> RejectReceipt(int id) => s.RejectReceipt(id);

    [HttpGet("purchase-invoices"), Authorize("procurement.view")] public Task<PagedResult<DocumentDto>> Invoices([FromQuery] PageQuery q) => Paging.Docs(s.Invoices.AsNoTracking(), q, x => x.ToDto());
    [HttpGet("purchase-invoices/{id:int}"), Authorize("procurement.view")] public Task<DocumentDto> Invoice(int id) => Paging.One(s.Invoices, id, x => x.ToDto());
    [HttpPost("purchase-invoices"), Authorize("procurement.manage")] public Task<DocumentDto> CreateInvoice(DocumentDto d) => s.CreateInvoice(d);

    [HttpGet("purchase-returns"), Authorize("procurement.view")] public Task<PagedResult<DocumentDto>> Returns([FromQuery] PageQuery q) => Paging.Docs(s.Returns.AsNoTracking(), q, x => x.ToDto());
    [HttpPost("purchase-returns"), Authorize("procurement.manage")] public Task<DocumentDto> CreateReturn(DocumentDto d) => s.CreateReturn(d);
}

[ApiController, Route("api"), Authorize]
public class SalesController(SalesService s) : ControllerBase
{
    [HttpGet("sales-quotations"), Authorize("sales.view")] public Task<PagedResult<DocumentDto>> Quotes([FromQuery] PageQuery q) => Paging.Docs(s.Quotations.AsNoTracking(), q, x => x.ToDto());
    [HttpPost("sales-quotations"), Authorize("sales.manage")] public Task<DocumentDto> CreateQuote(DocumentDto d) => s.CreateQuotation(d);
    [HttpPut("sales-quotations/{id:int}"), Authorize("sales.manage")] public Task<DocumentDto> UpdateQuote(int id, DocumentDto d) => s.UpdateQuotation(id, d);
    [HttpPost("sales-quotations/{id:int}/send"), Authorize("sales.manage")] public Task<DocumentDto> Send(int id) => s.SetQuotationStatus(id, "send");
    [HttpPost("sales-quotations/{id:int}/accept"), Authorize("sales.manage")] public Task<DocumentDto> Accept(int id) => s.SetQuotationStatus(id, "accept");
    [HttpPost("sales-quotations/{id:int}/reject"), Authorize("sales.manage")] public Task<DocumentDto> Reject(int id) => s.SetQuotationStatus(id, "reject");
    [HttpPost("sales-quotations/{id:int}/convert"), Authorize("sales.manage")] public Task<DocumentDto> Convert(int id, ConvertRequest c) => s.ConvertQuotation(id, c);

    [HttpGet("sales-orders"), Authorize("sales.view")] public Task<PagedResult<DocumentDto>> Orders([FromQuery] PageQuery q) => Paging.Docs(s.Orders.AsNoTracking(), q, x => x.ToDto());
    [HttpGet("sales-orders/{id:int}"), Authorize("sales.view")] public Task<DocumentDto> Order(int id) => Paging.One(s.Orders, id, x => x.ToDto());
    [HttpPost("sales-orders"), Authorize("sales.manage")] public Task<DocumentDto> CreateOrder(DocumentDto d) => s.CreateOrder(d);
    [HttpPut("sales-orders/{id:int}"), Authorize("sales.manage")] public Task<DocumentDto> UpdateOrder(int id, DocumentDto d) => s.UpdateOrder(id, d);
    [HttpPost("sales-orders/{id:int}/confirm"), Authorize("sales.manage")] public Task<DocumentDto> ConfirmOrder(int id) => s.SetOrderStatus(id, "confirm");
    [HttpPost("sales-orders/{id:int}/cancel"), Authorize("sales.manage")] public Task<DocumentDto> CancelOrder(int id) => s.SetOrderStatus(id, "cancel");
    [HttpPost("sales-orders/{id:int}/close"), Authorize("sales.manage")] public Task<DocumentDto> CloseOrder(int id) => s.SetOrderStatus(id, "close");

    [HttpGet("deliveries"), Authorize("sales.view")] public Task<PagedResult<DocumentDto>> Deliveries([FromQuery] PageQuery q) => Paging.Docs(s.Deliveries.AsNoTracking(), q, x => x.ToDto());
    [HttpGet("deliveries/{id:int}"), Authorize("sales.view")] public Task<DocumentDto> Delivery(int id) => Paging.One(s.Deliveries, id, x => x.ToDto());
    [HttpPost("deliveries"), Authorize("sales.manage")] public Task<DocumentDto> CreateDelivery(DocumentDto d) => s.CreateDelivery(d);
    [HttpPost("deliveries/{id:int}/post"), Authorize("sales.manage")] public Task<DocumentDto> PostDelivery(int id) => s.PostDelivery(id);

    [HttpGet("sales-invoices"), Authorize("sales.view")] public Task<PagedResult<DocumentDto>> Invoices([FromQuery] PageQuery q) => Paging.Docs(s.Invoices.AsNoTracking(), q, x => x.ToDto());
    [HttpGet("sales-invoices/{id:int}"), Authorize("sales.view")] public Task<DocumentDto> Invoice(int id) => Paging.One(s.Invoices, id, x => x.ToDto());
    [HttpPost("sales-invoices"), Authorize("sales.manage")] public Task<DocumentDto> CreateInvoice(DocumentDto d) => s.CreateInvoice(d);

    [HttpGet("sales-returns"), Authorize("sales.view")] public Task<PagedResult<DocumentDto>> Returns([FromQuery] PageQuery q) => Paging.Docs(s.Returns.AsNoTracking(), q, x => x.ToDto());
    [HttpPost("sales-returns"), Authorize("sales.manage")] public Task<DocumentDto> CreateReturn(DocumentDto d) => s.CreateReturn(d);
}

[ApiController, Route("api"), Authorize]
public class ManufacturingController(ManufacturingService m, ErpDbContext db) : ControllerBase
{
    [HttpGet("boms"), Authorize("production.view")]
    public async Task<PagedResult<BomDto>> Boms([FromQuery] PageQuery q)
    {
        var x = m.Boms.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search)) x = x.Where(b => b.Code.Contains(q.Search) || b.Name.Contains(q.Search));
        var total = await x.CountAsync(); var size = Math.Clamp(q.PageSize, 1, 1000);
        var items = await x.OrderByDescending(b => b.Id).Skip((Math.Max(q.Page, 1) - 1) * size).Take(size).ToListAsync();
        return new PagedResult<BomDto> { Items = items.Select(ManufacturingService.ToDto).ToList(), Total = total, Page = q.Page, PageSize = size };
    }
    [HttpPost("boms"), Authorize("production.manage")] public Task<BomDto> CreateBom(BomDto d) => m.SaveBom(null, d);
    [HttpPut("boms/{id:int}"), Authorize("production.manage")] public Task<BomDto> UpdateBom(int id, BomDto d) => m.SaveBom(id, d);
    [HttpDelete("boms/{id:int}"), Authorize("production.manage")] public async Task<IActionResult> DeleteBom(int id) { await m.DeleteBom(id); return NoContent(); }

    [HttpPost("production/plan"), Authorize("production.view")] public Task<object> Plan(PlanRequest r) => m.Plan(r);
    [HttpPost("production/plan/requisition"), Authorize("production.manage")] public Task<DocumentDto> PlanReq(PlanRequest r) => m.RequisitionForShortages(r);

    [HttpGet("production-orders"), Authorize("production.view")]
    public async Task<PagedResult<ProductionOrderDto>> Orders([FromQuery] PageQuery q)
    {
        var r = await Paging.Docs(m.Orders.AsNoTracking(), q, x => x);
        var list = new List<ProductionOrderDto>(); foreach (var o in r.Items) list.Add(await m.ToDto(o));
        return new PagedResult<ProductionOrderDto> { Items = list, Total = r.Total, Page = r.Page, PageSize = r.PageSize };
    }
    [HttpPost("production-orders"), Authorize("production.manage")] public Task<ProductionOrderDto> Create(CreateProductionRequest r) => m.CreateOrder(r);
    [HttpPost("production-orders/{id:int}/release"), Authorize("production.manage")] public Task<ProductionOrderDto> Release(int id) => m.Release(id);
    [HttpPost("production-orders/{id:int}/issue-materials"), Authorize("production.manage")] public Task<ProductionOrderDto> Issue(int id, IssueRequest? r) => m.IssueMaterials(id, r);
    [HttpPost("production-orders/{id:int}/complete"), Authorize("production.manage")] public Task<ProductionOrderDto> Complete(int id, CompleteProductionRequest r) => m.Complete(id, r);
    [HttpPost("production-orders/{id:int}/cancel"), Authorize("production.manage")] public Task<ProductionOrderDto> Cancel(int id) => m.Cancel(id);
    [HttpGet("material-issues"), Authorize("production.view")] public Task<PagedResult<DocumentDto>> Issues([FromQuery] PageQuery q) => Paging.Docs(m.Issues.AsNoTracking(), q, x => x.ToDto());
    [HttpGet("production-receipts"), Authorize("production.view")]
    public Task<PagedResult<object>> Receipts([FromQuery] PageQuery q) => Paging.Docs(m.Receipts.AsNoTracking(), q, x => (object)new { x.Id, x.Number, x.Date, product = x.Product!.Name, order = x.ProductionOrder!.Number, warehouse = x.Warehouse!.Name, quantity = x.QuantityProduced, x.UnitCost, x.TotalCost, quality = x.QualityStatus, x.Status });
}

[ApiController, Route("api"), Authorize]
public class FinanceController(FinanceService f, AccountingService a, ReportService r) : ControllerBase
{
    [HttpGet("payments"), Authorize("finance.view")]
    public Task<PagedResult<object>> Payments([FromQuery] PageQuery q) { var x = f.Payments.AsNoTracking(); if (!string.IsNullOrWhiteSpace(q.Type)) x = x.Where(p => p.PaymentType == q.Type); return Paging.Docs(x, q, FinanceService.PaymentRow); }
    [HttpPost("payments"), Authorize("finance.manage")] public async Task<object> Pay(PaymentRequest r) => FinanceService.PaymentRow((await f.RecordPayment(r)));
    [HttpGet("payables"), Authorize("finance.view")] public Task<PagedResult<object>> Payables([FromQuery] PageQuery q) => r.Payables(q);
    [HttpGet("receivables"), Authorize("finance.view")] public Task<PagedResult<object>> Receivables([FromQuery] PageQuery q) => r.Receivables(q);

    [HttpGet("expenses"), Authorize("finance.view")]
    public Task<PagedResult<object>> Expenses([FromQuery] PageQuery q) => Paging.Docs(f.Expenses.AsNoTracking(), q, e => (object)new { e.Id, e.Number, e.Date, account = e.Account!.Name, e.Amount, e.Method, payee = e.Payee, e.Description });
    [HttpPost("expenses"), Authorize("finance.manage")] public async Task<object> CreateExpense(CashEntryDto d) { var e = await f.CreateExpense(d); return new { e.Id, e.Number }; }
    [HttpGet("incomes"), Authorize("finance.view")]
    public Task<PagedResult<object>> Incomes([FromQuery] PageQuery q) => Paging.Docs(f.Incomes.AsNoTracking(), q, e => (object)new { e.Id, e.Number, e.Date, account = e.Account!.Name, e.Amount, e.Method, source = e.Source, e.Description });
    [HttpPost("incomes"), Authorize("finance.manage")] public async Task<object> CreateIncome(CashEntryDto d) { var e = await f.CreateIncome(d); return new { e.Id, e.Number }; }

    [HttpGet("journal-entries"), Authorize("finance.view")] public Task<PagedResult<JournalDto>> Journals([FromQuery] PageQuery q) => Paging.Docs(a.Journals.AsNoTracking(), q, x => x.ToDto());
    [HttpPost("journal-entries"), Authorize("finance.manage")] public Task<JournalDto> CreateJournal(JournalDto d) => a.CreateManual(d);
    [HttpPost("journal-entries/{id:int}/post"), Authorize("finance.manage")] public Task<JournalDto> PostJournal(int id) => a.PostDraft(id);
    [HttpPost("journal-entries/{id:int}/reverse"), Authorize("finance.manage")] public Task<JournalDto> Reverse(int id) => a.Reverse(id);

    [HttpGet("finance/trial-balance"), Authorize("finance.view")] public Task<object> Tb([FromQuery] DateTime? asOf) => a.TrialBalance(asOf);
    [HttpGet("finance/general-ledger"), Authorize("finance.view")] public Task<object> Gl([FromQuery] int accountId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) => a.GeneralLedger(accountId, from, to);
    [HttpGet("finance/profit-loss"), Authorize("finance.view")] public async Task<object> Pl([FromQuery] DateTime? from, [FromQuery] DateTime? to) => await a.ProfitLoss(from, to);
    [HttpGet("finance/balance-sheet"), Authorize("finance.view")] public Task<object> Bs([FromQuery] DateTime? asOf) => a.BalanceSheet(asOf);
}

[ApiController, Route("api/reports"), Authorize("reports.view")]
public class ReportsController(ReportService r) : ControllerBase
{
    [HttpGet("sales")] public Task<PagedResult<object>> Sales([FromQuery] PageQuery q) => r.Sales(q);
    [HttpGet("purchases")] public Task<PagedResult<object>> Purchases([FromQuery] PageQuery q) => r.Purchases(q);
    [HttpGet("inventory")] public Task<PagedResult<object>> Inventory([FromQuery] PageQuery q) => r.Inventory(q);
    [HttpGet("stock-valuation")] public Task<PagedResult<object>> Valuation([FromQuery] PageQuery q) => r.StockValuation(q);
    [HttpGet("suppliers")] public Task<PagedResult<object>> Suppliers([FromQuery] PageQuery q) => r.Suppliers(q);
    [HttpGet("customers")] public Task<PagedResult<object>> Customers([FromQuery] PageQuery q) => r.Customers(q);
    [HttpGet("production")] public Task<PagedResult<object>> Production([FromQuery] PageQuery q) => r.Production(q);
    [HttpGet("expenses")] public Task<PagedResult<object>> Expenses([FromQuery] PageQuery q) => r.Expenses(q);
    [HttpGet("revenue")] public Task<PagedResult<object>> Revenue([FromQuery] PageQuery q) => r.Revenue(q);
    [HttpGet("receivables")] public Task<PagedResult<object>> Receivables([FromQuery] PageQuery q) => r.Receivables(q);
    [HttpGet("payables")] public Task<PagedResult<object>> Payables([FromQuery] PageQuery q) => r.Payables(q);
    [HttpGet("analytics/{kind}")] public Task<object> Analytics(string kind) => r.Analytics(kind);
}
