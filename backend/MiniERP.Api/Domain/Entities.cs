using System.ComponentModel.DataAnnotations;
namespace MiniERP.Api.Domain;

public interface ISoftDelete { bool IsDeleted { get; set; } }
public abstract class BaseEntity { public int Id { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; public DateTime? UpdatedAt { get; set; } }
public abstract class MasterEntity : BaseEntity, ISoftDelete { public bool IsDeleted { get; set; } }
public abstract class DocEntity : BaseEntity
{
    [MaxLength(40)] public string Number { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;
    [MaxLength(30)] public string Status { get; set; } = St.Draft;
    [MaxLength(1000)] public string? Notes { get; set; }
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public byte[]? RowVersion { get; set; }
}
public abstract class PricedDoc : DocEntity { public decimal Subtotal { get; set; } public decimal TaxAmount { get; set; } public decimal Total { get; set; } }
public abstract class ItemBase
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public static class St
{
    public const string Draft = "Draft", Submitted = "Submitted", Approved = "Approved", Rejected = "Rejected", Converted = "Converted",
        Cancelled = "Cancelled", PartiallyReceived = "PartiallyReceived", Received = "Received", Closed = "Closed", Posted = "Posted",
        Pending = "Pending", Passed = "Passed", Failed = "Failed", Sent = "Sent", Accepted = "Accepted", Confirmed = "Confirmed",
        PartiallyDelivered = "PartiallyDelivered", Delivered = "Delivered", Unpaid = "Unpaid", PartiallyPaid = "PartiallyPaid", Paid = "Paid",
        Planned = "Planned", Released = "Released", InProgress = "InProgress", Completed = "Completed", Reversed = "Reversed";
}
public static class ProductTypes { public const string RawMaterial = "RawMaterial", FinishedGood = "FinishedGood", TradingProduct = "TradingProduct"; }
public static class Prefix
{
    public const string PR = "PR", PO = "PO", GRN = "GRN", PINV = "PI", PRET = "PRT", QT = "QT", SO = "SO", DN = "DN", SINV = "INV", SRET = "SRT",
        TRF = "TRF", PRD = "PRD", MI = "MI", FGR = "FGR", PAY = "PAY", REC = "RCT", EXP = "EXP", INC = "INC", JV = "JV";
}
public static class Perms
{
    public static readonly (string Code, string Description)[] All =
    {
        ("inventory.view","View inventory"),("inventory.manage","Manage inventory"),
        ("procurement.view","View procurement"),("procurement.manage","Manage procurement"),("procurement.approve","Approve purchase documents"),
        ("sales.view","View sales"),("sales.manage","Manage sales"),
        ("production.view","View production"),("production.manage","Manage production"),
        ("finance.view","View finance"),("finance.manage","Manage finance"),
        ("reports.view","View reports"),("admin.manage","Administration")
    };
}

// ---------- Auth ----------
public class User : MasterEntity { [MaxLength(60)] public string Username { get; set; } = ""; public string PasswordHash { get; set; } = ""; [MaxLength(150)] public string FullName { get; set; } = ""; [MaxLength(150)] public string? Email { get; set; } public bool IsActive { get; set; } = true; public int RoleId { get; set; } public Role? Role { get; set; } }
public class Role : BaseEntity { [MaxLength(60)] public string Name { get; set; } = ""; public string? Description { get; set; } public List<RolePermission> RolePermissions { get; set; } = new(); }
public class Permission : BaseEntity { [MaxLength(60)] public string Code { get; set; } = ""; public string? Description { get; set; } }
public class RolePermission { public int RoleId { get; set; } public Role? Role { get; set; } public int PermissionId { get; set; } public Permission? Permission { get; set; } }

// ---------- Masters ----------
public class Category : MasterEntity { [MaxLength(100)] public string Name { get; set; } = ""; public string? Description { get; set; } }
public class Unit : MasterEntity { [MaxLength(60)] public string Name { get; set; } = ""; [MaxLength(10)] public string Symbol { get; set; } = ""; }
public class Warehouse : MasterEntity { [MaxLength(20)] public string Code { get; set; } = ""; [MaxLength(100)] public string Name { get; set; } = ""; public string? Location { get; set; } public bool IsActive { get; set; } = true; }
public class Product : MasterEntity
{
    [MaxLength(40)] public string Sku { get; set; } = ""; [MaxLength(200)] public string Name { get; set; } = ""; public string? Description { get; set; }
    public int CategoryId { get; set; } public Category? Category { get; set; }
    public int UnitId { get; set; } public Unit? Unit { get; set; }
    [MaxLength(20)] public string ProductType { get; set; } = ProductTypes.TradingProduct;
    public decimal AverageCost { get; set; } public decimal SalePrice { get; set; } public decimal TaxRate { get; set; } public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
}
public class Supplier : MasterEntity { [MaxLength(20)] public string Code { get; set; } = ""; [MaxLength(200)] public string Name { get; set; } = ""; public string? ContactPerson { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } public string? TaxNumber { get; set; } public int PaymentTermsDays { get; set; } = 30; public bool IsActive { get; set; } = true; }
public class Customer : MasterEntity { [MaxLength(20)] public string Code { get; set; } = ""; [MaxLength(200)] public string Name { get; set; } = ""; public string? ContactPerson { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } public string? TaxNumber { get; set; } public int PaymentTermsDays { get; set; } = 30; public decimal CreditLimit { get; set; } public bool IsActive { get; set; } = true; }

// ---------- Inventory ----------
public class StockBalance { public int Id { get; set; } public int ProductId { get; set; } public Product? Product { get; set; } public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; } public decimal Quantity { get; set; } public DateTime? UpdatedAt { get; set; } }
public class StockTransaction
{
    public int Id { get; set; } public DateTime Date { get; set; } = DateTime.UtcNow;
    public int ProductId { get; set; } public Product? Product { get; set; } public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; }
    [MaxLength(30)] public string TransactionType { get; set; } = ""; public decimal Quantity { get; set; } public decimal UnitCost { get; set; }
    [MaxLength(30)] public string? ReferenceType { get; set; } public int? ReferenceId { get; set; } [MaxLength(40)] public string? ReferenceNo { get; set; }
    public string? Notes { get; set; } [MaxLength(100)] public string? CreatedBy { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class StockTransfer : DocEntity { public int FromWarehouseId { get; set; } public Warehouse? FromWarehouse { get; set; } public int ToWarehouseId { get; set; } public Warehouse? ToWarehouse { get; set; } public List<StockTransferItem> Items { get; set; } = new(); }
public class StockTransferItem : ItemBase { public int StockTransferId { get; set; } }

// ---------- Procurement ----------
public class PurchaseRequisition : DocEntity { public string? RequestedBy { get; set; } public string? ApprovedBy { get; set; } public DateTime? ApprovedAt { get; set; } public int? PurchaseOrderId { get; set; } public List<PurchaseRequisitionItem> Items { get; set; } = new(); }
public class PurchaseRequisitionItem : ItemBase { public int PurchaseRequisitionId { get; set; } }
public class PurchaseOrder : PricedDoc
{
    public int SupplierId { get; set; } public Supplier? Supplier { get; set; } public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; }
    public DateTime? ExpectedDate { get; set; } public int? RequisitionId { get; set; } public string? ApprovedBy { get; set; } public List<PurchaseOrderItem> Items { get; set; } = new();
}
public class PurchaseOrderItem : ItemBase { public int PurchaseOrderId { get; set; } public decimal ReceivedQuantity { get; set; } }
public class GoodsReceipt : DocEntity
{
    public int PurchaseOrderId { get; set; } public PurchaseOrder? PurchaseOrder { get; set; } public int SupplierId { get; set; } public Supplier? Supplier { get; set; }
    public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; } [MaxLength(20)] public string QualityStatus { get; set; } = St.Pending; public string? InspectionNotes { get; set; }
    public bool IsInvoiced { get; set; } public List<GoodsReceiptItem> Items { get; set; } = new();
}
public class GoodsReceiptItem : ItemBase { public int GoodsReceiptId { get; set; } public int PurchaseOrderItemId { get; set; } public PurchaseOrderItem? PurchaseOrderItem { get; set; } public decimal RejectedQuantity { get; set; } }
public class PurchaseInvoice : PricedDoc
{
    public int SupplierId { get; set; } public Supplier? Supplier { get; set; } public int? GoodsReceiptId { get; set; } public GoodsReceipt? GoodsReceipt { get; set; } public int? PurchaseOrderId { get; set; }
    [MaxLength(60)] public string? SupplierInvoiceNo { get; set; } public DateTime? DueDate { get; set; } public decimal PaidAmount { get; set; } public decimal CreditedAmount { get; set; } public List<PurchaseInvoiceItem> Items { get; set; } = new();
}
public class PurchaseInvoiceItem : ItemBase { public int PurchaseInvoiceId { get; set; } }
public class PurchaseReturn : PricedDoc { public int SupplierId { get; set; } public Supplier? Supplier { get; set; } public int PurchaseInvoiceId { get; set; } public PurchaseInvoice? PurchaseInvoice { get; set; } public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; } public string? Reason { get; set; } public List<PurchaseReturnItem> Items { get; set; } = new(); }
public class PurchaseReturnItem : ItemBase { public int PurchaseReturnId { get; set; } public PurchaseReturn? PurchaseReturn { get; set; } }

// ---------- Sales ----------
public class SalesQuotation : PricedDoc { public int CustomerId { get; set; } public Customer? Customer { get; set; } public DateTime? ValidUntil { get; set; } public List<SalesQuotationItem> Items { get; set; } = new(); }
public class SalesQuotationItem : ItemBase { public int SalesQuotationId { get; set; } }
public class SalesOrder : PricedDoc { public int CustomerId { get; set; } public Customer? Customer { get; set; } public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; } public int? QuotationId { get; set; } public DateTime? DeliveryDate { get; set; } public List<SalesOrderItem> Items { get; set; } = new(); }
public class SalesOrderItem : ItemBase { public int SalesOrderId { get; set; } public decimal DeliveredQuantity { get; set; } public decimal InvoicedQuantity { get; set; } }
public class Delivery : DocEntity { public int SalesOrderId { get; set; } public SalesOrder? SalesOrder { get; set; } public int CustomerId { get; set; } public Customer? Customer { get; set; } public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; } public bool IsInvoiced { get; set; } public List<DeliveryItem> Items { get; set; } = new(); }
public class DeliveryItem : ItemBase { public int DeliveryId { get; set; } public int SalesOrderItemId { get; set; } public SalesOrderItem? SalesOrderItem { get; set; } public decimal UnitCost { get; set; } }
public class SalesInvoice : PricedDoc
{
    public int CustomerId { get; set; } public Customer? Customer { get; set; } public int? DeliveryId { get; set; } public Delivery? Delivery { get; set; } public int? SalesOrderId { get; set; }
    public DateTime? DueDate { get; set; } public decimal PaidAmount { get; set; } public decimal CreditedAmount { get; set; } public List<SalesInvoiceItem> Items { get; set; } = new();
}
public class SalesInvoiceItem : ItemBase { public int SalesInvoiceId { get; set; } }
public class SalesReturn : PricedDoc { public int CustomerId { get; set; } public Customer? Customer { get; set; } public int SalesInvoiceId { get; set; } public SalesInvoice? SalesInvoice { get; set; } public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; } public string? Reason { get; set; } public List<SalesReturnItem> Items { get; set; } = new(); }
public class SalesReturnItem : ItemBase { public int SalesReturnId { get; set; } public SalesReturn? SalesReturn { get; set; } }

// ---------- Manufacturing ----------
public class BillOfMaterial : MasterEntity
{
    [MaxLength(30)] public string Code { get; set; } = ""; [MaxLength(200)] public string Name { get; set; } = ""; public int ProductId { get; set; } public Product? Product { get; set; }
    public decimal OutputQuantity { get; set; } = 1; [MaxLength(20)] public string Version { get; set; } = "1.0"; public bool IsActive { get; set; } = true; public string? Notes { get; set; }
    public List<BomItem> Items { get; set; } = new();
}
public class BomItem { public int Id { get; set; } public int BillOfMaterialId { get; set; } public int ComponentProductId { get; set; } public Product? ComponentProduct { get; set; } public decimal Quantity { get; set; } public decimal ScrapPercent { get; set; } }
public class ProductionOrder : DocEntity
{
    public int BillOfMaterialId { get; set; } public BillOfMaterial? BillOfMaterial { get; set; } public int ProductId { get; set; } public Product? Product { get; set; }
    public decimal Quantity { get; set; } public decimal QuantityProduced { get; set; }
    public int SourceWarehouseId { get; set; } public Warehouse? SourceWarehouse { get; set; } public int TargetWarehouseId { get; set; } public Warehouse? TargetWarehouse { get; set; }
    public DateTime? DueDate { get; set; } [MaxLength(20)] public string QualityStatus { get; set; } = St.Pending;
    public decimal MaterialCost { get; set; } public decimal LaborCost { get; set; } public decimal OverheadCost { get; set; } public decimal TotalCost { get; set; } public decimal UnitCost { get; set; }
    public List<ProductionComponent> Components { get; set; } = new();
}
public class ProductionComponent { public int Id { get; set; } public int ProductionOrderId { get; set; } public int ProductId { get; set; } public Product? Product { get; set; } public decimal RequiredQuantity { get; set; } public decimal IssuedQuantity { get; set; } public decimal UnitCost { get; set; } }
public class MaterialIssue : DocEntity { public int ProductionOrderId { get; set; } public ProductionOrder? ProductionOrder { get; set; } public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; } public List<MaterialIssueItem> Items { get; set; } = new(); }
public class MaterialIssueItem : ItemBase { public int MaterialIssueId { get; set; } }
public class ProductionReceipt : DocEntity
{
    public int ProductionOrderId { get; set; } public ProductionOrder? ProductionOrder { get; set; } public int ProductId { get; set; } public Product? Product { get; set; }
    public int WarehouseId { get; set; } public Warehouse? Warehouse { get; set; } public decimal QuantityProduced { get; set; } public string QualityStatus { get; set; } = St.Passed; public decimal UnitCost { get; set; } public decimal TotalCost { get; set; }
}

// ---------- Finance ----------
public static class AccountTypes { public const string Asset = "Asset", Liability = "Liability", Equity = "Equity", Revenue = "Revenue", Expense = "Expense"; }
public class Account : MasterEntity { [MaxLength(20)] public string Code { get; set; } = ""; [MaxLength(150)] public string Name { get; set; } = ""; [MaxLength(20)] public string AccountType { get; set; } = AccountTypes.Asset; public int? ParentId { get; set; } public bool IsSystem { get; set; } public bool IsActive { get; set; } = true; }
public class JournalEntry : DocEntity
{
    public string Description { get; set; } = ""; [MaxLength(40)] public string? ReferenceType { get; set; } public int? ReferenceId { get; set; } [MaxLength(80)] public string? ReferenceKey { get; set; }
    public decimal TotalDebit { get; set; } public decimal TotalCredit { get; set; } public List<JournalEntryLine> Lines { get; set; } = new();
}
public class JournalEntryLine { public int Id { get; set; } public int JournalEntryId { get; set; } public JournalEntry? JournalEntry { get; set; } public int AccountId { get; set; } public Account? Account { get; set; } public decimal Debit { get; set; } public decimal Credit { get; set; } public string? Memo { get; set; } }
public class Payment : DocEntity
{
    [MaxLength(20)] public string PaymentType { get; set; } = "SupplierPayment"; // SupplierPayment | CustomerReceipt
    public int? SupplierId { get; set; } public Supplier? Supplier { get; set; } public int? CustomerId { get; set; } public Customer? Customer { get; set; }
    public int? PurchaseInvoiceId { get; set; } public PurchaseInvoice? PurchaseInvoice { get; set; } public int? SalesInvoiceId { get; set; } public SalesInvoice? SalesInvoice { get; set; }
    public decimal Amount { get; set; } [MaxLength(20)] public string Method { get; set; } = "Bank"; [MaxLength(80)] public string? Reference { get; set; } [MaxLength(80)] public string? IdempotencyKey { get; set; }
}
public class Expense : DocEntity { public int AccountId { get; set; } public Account? Account { get; set; } public decimal Amount { get; set; } [MaxLength(20)] public string Method { get; set; } = "Cash"; public string? Payee { get; set; } public string? Description { get; set; } }
public class Income : DocEntity { public int AccountId { get; set; } public Account? Account { get; set; } public decimal Amount { get; set; } [MaxLength(20)] public string Method { get; set; } = "Bank"; public string? Source { get; set; } public string? Description { get; set; } }

// ---------- System ----------
public class AuditLog { public int Id { get; set; } public DateTime Timestamp { get; set; } = DateTime.UtcNow; [MaxLength(100)] public string? UserName { get; set; } [MaxLength(20)] public string Action { get; set; } = ""; [MaxLength(80)] public string EntityName { get; set; } = ""; public int EntityId { get; set; } public string? Details { get; set; } }
public class Notification { public int Id { get; set; } public string Message { get; set; } = ""; [MaxLength(30)] public string Type { get; set; } = "Info"; [MaxLength(80)] public string? Key { get; set; } public string? Link { get; set; } public bool IsRead { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; }
public class DocumentSequence { public int Id { get; set; } [MaxLength(10)] public string Prefix { get; set; } = ""; public int LastNumber { get; set; } public byte[]? RowVersion { get; set; } }
public class Setting : BaseEntity { [MaxLength(80)] public string Key { get; set; } = ""; public string? Value { get; set; } }
