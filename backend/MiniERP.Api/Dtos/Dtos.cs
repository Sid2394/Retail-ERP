namespace MiniERP.Api.Dtos;

public class PageQuery
{
    public int Page { get; set; } = 1; public int PageSize { get; set; } = 20;
    public string? Search { get; set; } public string? SortBy { get; set; } public string? SortDir { get; set; } = "desc";
    public DateTime? From { get; set; } public DateTime? To { get; set; } public string? Status { get; set; }
    public int? ProductId { get; set; } public int? WarehouseId { get; set; } public int? PartyId { get; set; } public string? Type { get; set; }
}
public class PagedResult<T> { public List<T> Items { get; set; } = new(); public int Total { get; set; } public int Page { get; set; } public int PageSize { get; set; } public Dictionary<string, decimal>? Summary { get; set; } }

public class LineDto
{
    public int Id { get; set; } public int? RefLineId { get; set; }
    public int ProductId { get; set; } public string? ProductName { get; set; } public string? Sku { get; set; }
    public decimal Quantity { get; set; } public decimal UnitPrice { get; set; } public decimal TaxRate { get; set; } public decimal LineTotal { get; set; }
    public decimal DoneQuantity { get; set; } public decimal RejectedQuantity { get; set; } public string? Notes { get; set; }
}
public class DocumentDto
{
    public int Id { get; set; } public string? Number { get; set; } public DateTime Date { get; set; } public DateTime? DueDate { get; set; }
    public int? PartyId { get; set; } public string? PartyName { get; set; }
    public int? WarehouseId { get; set; } public string? WarehouseName { get; set; } public int? ToWarehouseId { get; set; } public string? ToWarehouseName { get; set; }
    public int? RefId { get; set; } public string? RefNumber { get; set; } public string? Reference { get; set; }
    public string Status { get; set; } = ""; public string? QualityStatus { get; set; } public string? Notes { get; set; }
    public decimal Subtotal { get; set; } public decimal TaxAmount { get; set; } public decimal Total { get; set; }
    public decimal PaidAmount { get; set; } public decimal CreditedAmount { get; set; } public decimal Balance { get; set; }
    public List<LineDto> Lines { get; set; } = new();
}
public class MovementRequest { public int ProductId { get; set; } public int WarehouseId { get; set; } public string Type { get; set; } = ""; public decimal Quantity { get; set; } public decimal? UnitCost { get; set; } public string? Notes { get; set; } public DateTime? Date { get; set; } }
public class ConvertRequest { public int? SupplierId { get; set; } public int? WarehouseId { get; set; } }
public class PaymentRequest { public string Type { get; set; } = "SupplierPayment"; public int InvoiceId { get; set; } public decimal Amount { get; set; } public string Method { get; set; } = "Bank"; public string? Reference { get; set; } public string? IdempotencyKey { get; set; } public DateTime? Date { get; set; } public string? Notes { get; set; } }
public class CashEntryDto { public int Id { get; set; } public string? Number { get; set; } public DateTime Date { get; set; } public int AccountId { get; set; } public string? AccountName { get; set; } public decimal Amount { get; set; } public string Method { get; set; } = "Cash"; public string? Party { get; set; } public string? Description { get; set; } }
public class JournalLineDto { public int Id { get; set; } public int AccountId { get; set; } public string? AccountCode { get; set; } public string? AccountName { get; set; } public decimal Debit { get; set; } public decimal Credit { get; set; } public string? Memo { get; set; } }
public class JournalDto { public int Id { get; set; } public string? Number { get; set; } public DateTime Date { get; set; } public string Description { get; set; } = ""; public string Status { get; set; } = ""; public string? ReferenceType { get; set; } public decimal TotalDebit { get; set; } public decimal TotalCredit { get; set; } public bool Post { get; set; } public List<JournalLineDto> Lines { get; set; } = new(); }
public class BomItemDto { public int Id { get; set; } public int ComponentProductId { get; set; } public string? ComponentName { get; set; } public string? Sku { get; set; } public decimal Quantity { get; set; } public decimal ScrapPercent { get; set; } }
public class BomDto { public int Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public int ProductId { get; set; } public string? ProductName { get; set; } public decimal OutputQuantity { get; set; } = 1; public string Version { get; set; } = "1.0"; public bool IsActive { get; set; } = true; public string? Notes { get; set; } public List<BomItemDto> Items { get; set; } = new(); }
public class ComponentDto { public int ProductId { get; set; } public string? Sku { get; set; } public string? ProductName { get; set; } public decimal Required { get; set; } public decimal Issued { get; set; } public decimal Available { get; set; } public decimal Shortage { get; set; } public decimal UnitCost { get; set; } public decimal EstimatedCost { get; set; } }
public class ProductionOrderDto
{
    public int Id { get; set; } public string Number { get; set; } = ""; public DateTime Date { get; set; } public DateTime? DueDate { get; set; } public string Status { get; set; } = ""; public string QualityStatus { get; set; } = "";
    public int ProductId { get; set; } public string? ProductName { get; set; } public int BillOfMaterialId { get; set; } public string? BomCode { get; set; }
    public decimal Quantity { get; set; } public decimal QuantityProduced { get; set; } public int SourceWarehouseId { get; set; } public int TargetWarehouseId { get; set; } public string? Notes { get; set; }
    public decimal MaterialCost { get; set; } public decimal LaborCost { get; set; } public decimal OverheadCost { get; set; } public decimal TotalCost { get; set; } public decimal UnitCost { get; set; }
    public List<ComponentDto> Components { get; set; } = new();
}
public class CreateProductionRequest { public int? BomId { get; set; } public int ProductId { get; set; } public decimal Quantity { get; set; } public int SourceWarehouseId { get; set; } public int TargetWarehouseId { get; set; } public DateTime? Date { get; set; } public DateTime? DueDate { get; set; } public string? Notes { get; set; } }
public class IssueRequest { public List<IssueLine>? Lines { get; set; } } public class IssueLine { public int ProductId { get; set; } public decimal Quantity { get; set; } }
public class CompleteProductionRequest { public decimal QuantityProduced { get; set; } public string QualityStatus { get; set; } = "Passed"; public decimal LaborCost { get; set; } public decimal OverheadCost { get; set; } public string? Notes { get; set; } }
public class PlanRequest { public int ProductId { get; set; } public decimal Quantity { get; set; } public int? WarehouseId { get; set; } }
public class LoginRequest { public string Username { get; set; } = ""; public string Password { get; set; } = ""; }
public class AuthUser { public int Id { get; set; } public string Username { get; set; } = ""; public string FullName { get; set; } = ""; public string Role { get; set; } = ""; public List<string> Permissions { get; set; } = new(); }
public class LoginResponse { public string Token { get; set; } = ""; public AuthUser User { get; set; } = new(); }
public class UserDto { public int Id { get; set; } public string Username { get; set; } = ""; public string FullName { get; set; } = ""; public string? Email { get; set; } public int RoleId { get; set; } public string? RoleName { get; set; } public bool IsActive { get; set; } = true; public string? Password { get; set; } }
public class RoleDto { public int Id { get; set; } public string Name { get; set; } = ""; public string? Description { get; set; } public List<string> Permissions { get; set; } = new(); }
public class ProductDto { public int Id { get; set; } public string Sku { get; set; } = ""; public string Name { get; set; } = ""; public string? Description { get; set; } public int CategoryId { get; set; } public string? CategoryName { get; set; } public int UnitId { get; set; } public string? UnitName { get; set; } public string ProductType { get; set; } = ProductTypes.TradingProduct; public decimal AverageCost { get; set; } public decimal SalePrice { get; set; } public decimal TaxRate { get; set; } public decimal ReorderLevel { get; set; } public bool IsActive { get; set; } = true; public decimal OnHand { get; set; } }
public class CategoryDto { public int Id { get; set; } public string Name { get; set; } = ""; public string? Description { get; set; } }
public class UnitDto { public int Id { get; set; } public string Name { get; set; } = ""; public string Symbol { get; set; } = ""; }
public class WarehouseDto { public int Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string? Location { get; set; } public bool IsActive { get; set; } = true; }
public class SupplierDto { public int Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string? ContactPerson { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } public string? TaxNumber { get; set; } public int PaymentTermsDays { get; set; } = 30; public bool IsActive { get; set; } = true; }
public class CustomerDto { public int Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string? ContactPerson { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } public string? TaxNumber { get; set; } public int PaymentTermsDays { get; set; } = 30; public decimal CreditLimit { get; set; } public bool IsActive { get; set; } = true; }
public class AccountDto { public int Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string AccountType { get; set; } = "Asset"; public int? ParentId { get; set; } public bool IsActive { get; set; } = true; public bool IsSystem { get; set; } }
