namespace MiniERP.Api.Services;

public static class Map
{
    public static LineDto Line(ItemBase i, decimal done = 0, decimal rejected = 0, int? refLine = null) => new()
    {
        Id = i.Id, ProductId = i.ProductId, ProductName = i.Product?.Name, Sku = i.Product?.Sku, Quantity = i.Quantity, UnitPrice = i.UnitPrice,
        TaxRate = i.TaxRate, LineTotal = i.LineTotal, DoneQuantity = done, RejectedQuantity = rejected, RefLineId = refLine, Notes = i.Notes
    };
    static DocumentDto Base(DocEntity d) => new() { Id = d.Id, Number = d.Number, Date = d.Date, Status = d.Status, Notes = d.Notes };
    static DocumentDto Priced(PricedDoc d) { var r = Base(d); r.Subtotal = d.Subtotal; r.TaxAmount = d.TaxAmount; r.Total = d.Total; return r; }

    public static DocumentDto ToDto(this PurchaseRequisition r) { var d = Base(r); d.PartyName = r.RequestedBy; d.Lines = r.Items.Select(i => Line(i)).ToList(); return d; }
    public static DocumentDto ToDto(this PurchaseOrder r)
    {
        var d = Priced(r); d.PartyId = r.SupplierId; d.PartyName = r.Supplier?.Name; d.WarehouseId = r.WarehouseId; d.WarehouseName = r.Warehouse?.Name; d.DueDate = r.ExpectedDate; d.RefId = r.RequisitionId;
        d.Lines = r.Items.Select(i => Line(i, i.ReceivedQuantity)).ToList(); return d;
    }
    public static DocumentDto ToDto(this GoodsReceipt r)
    {
        var d = Base(r); d.PartyId = r.SupplierId; d.PartyName = r.Supplier?.Name; d.WarehouseId = r.WarehouseId; d.WarehouseName = r.Warehouse?.Name; d.RefId = r.PurchaseOrderId; d.RefNumber = r.PurchaseOrder?.Number; d.QualityStatus = r.QualityStatus; d.Reference = r.IsInvoiced ? "Invoiced" : null;
        d.Lines = r.Items.Select(i => Line(i, 0, i.RejectedQuantity, i.PurchaseOrderItemId)).ToList(); return d;
    }
    public static DocumentDto ToDto(this PurchaseInvoice r)
    {
        var d = Priced(r); d.PartyId = r.SupplierId; d.PartyName = r.Supplier?.Name; d.DueDate = r.DueDate; d.RefId = r.GoodsReceiptId; d.RefNumber = r.GoodsReceipt?.Number; d.Reference = r.SupplierInvoiceNo;
        d.PaidAmount = r.PaidAmount; d.CreditedAmount = r.CreditedAmount; d.Balance = r.Total - r.PaidAmount - r.CreditedAmount; d.Lines = r.Items.Select(i => Line(i)).ToList(); return d;
    }
    public static DocumentDto ToDto(this PurchaseReturn r)
    {
        var d = Priced(r); d.PartyId = r.SupplierId; d.PartyName = r.Supplier?.Name; d.WarehouseId = r.WarehouseId; d.WarehouseName = r.Warehouse?.Name; d.RefId = r.PurchaseInvoiceId; d.RefNumber = r.PurchaseInvoice?.Number; d.Reference = r.Reason;
        d.Lines = r.Items.Select(i => Line(i)).ToList(); return d;
    }
    public static DocumentDto ToDto(this SalesQuotation r) { var d = Priced(r); d.PartyId = r.CustomerId; d.PartyName = r.Customer?.Name; d.DueDate = r.ValidUntil; d.Lines = r.Items.Select(i => Line(i)).ToList(); return d; }
    public static DocumentDto ToDto(this SalesOrder r)
    {
        var d = Priced(r); d.PartyId = r.CustomerId; d.PartyName = r.Customer?.Name; d.WarehouseId = r.WarehouseId; d.WarehouseName = r.Warehouse?.Name; d.DueDate = r.DeliveryDate; d.RefId = r.QuotationId;
        d.Lines = r.Items.Select(i => Line(i, i.DeliveredQuantity)).ToList(); return d;
    }
    public static DocumentDto ToDto(this Delivery r)
    {
        var d = Base(r); d.PartyId = r.CustomerId; d.PartyName = r.Customer?.Name; d.WarehouseId = r.WarehouseId; d.WarehouseName = r.Warehouse?.Name; d.RefId = r.SalesOrderId; d.RefNumber = r.SalesOrder?.Number; d.Reference = r.IsInvoiced ? "Invoiced" : null;
        d.Lines = r.Items.Select(i => Line(i, 0, 0, i.SalesOrderItemId)).ToList(); return d;
    }
    public static DocumentDto ToDto(this SalesInvoice r)
    {
        var d = Priced(r); d.PartyId = r.CustomerId; d.PartyName = r.Customer?.Name; d.DueDate = r.DueDate; d.RefId = r.DeliveryId; d.RefNumber = r.Delivery?.Number;
        d.PaidAmount = r.PaidAmount; d.CreditedAmount = r.CreditedAmount; d.Balance = r.Total - r.PaidAmount - r.CreditedAmount; d.Lines = r.Items.Select(i => Line(i)).ToList(); return d;
    }
    public static DocumentDto ToDto(this SalesReturn r)
    {
        var d = Priced(r); d.PartyId = r.CustomerId; d.PartyName = r.Customer?.Name; d.WarehouseId = r.WarehouseId; d.WarehouseName = r.Warehouse?.Name; d.RefId = r.SalesInvoiceId; d.RefNumber = r.SalesInvoice?.Number; d.Reference = r.Reason;
        d.Lines = r.Items.Select(i => Line(i)).ToList(); return d;
    }
    public static DocumentDto ToDto(this StockTransfer r)
    {
        var d = Base(r); d.WarehouseId = r.FromWarehouseId; d.WarehouseName = r.FromWarehouse?.Name; d.ToWarehouseId = r.ToWarehouseId; d.ToWarehouseName = r.ToWarehouse?.Name;
        d.Lines = r.Items.Select(i => Line(i)).ToList(); return d;
    }
    public static DocumentDto ToDto(this MaterialIssue r)
    {
        var d = Base(r); d.WarehouseId = r.WarehouseId; d.WarehouseName = r.Warehouse?.Name; d.RefId = r.ProductionOrderId; d.RefNumber = r.ProductionOrder?.Number;
        d.Lines = r.Items.Select(i => Line(i)).ToList(); return d;
    }
    public static JournalDto ToDto(this JournalEntry j) => new()
    {
        Id = j.Id, Number = j.Number, Date = j.Date, Description = j.Description, Status = j.Status, ReferenceType = j.ReferenceType, TotalDebit = j.TotalDebit, TotalCredit = j.TotalCredit,
        Lines = j.Lines.Select(l => new JournalLineDto { Id = l.Id, AccountId = l.AccountId, AccountCode = l.Account?.Code, AccountName = l.Account?.Name, Debit = l.Debit, Credit = l.Credit, Memo = l.Memo }).ToList()
    };
}
