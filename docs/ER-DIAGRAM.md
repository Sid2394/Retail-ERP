# MiniERP – Entity Relationship Diagram

Rendered by GitHub, VS Code (Markdown Preview Mermaid Support) and https://mermaid.live. Column lists are abbreviated to keys and the most important fields; see `docs/DATABASE.md` for conventions that apply to every table.

```mermaid
erDiagram
    ROLE ||--o{ USER : has
    ROLE ||--o{ ROLE_PERMISSION : grants
    PERMISSION ||--o{ ROLE_PERMISSION : in

    CATEGORY ||--o{ PRODUCT : classifies
    UNIT ||--o{ PRODUCT : measures
    PRODUCT ||--o{ STOCK_BALANCE : "held as"
    WAREHOUSE ||--o{ STOCK_BALANCE : stores
    PRODUCT ||--o{ STOCK_TRANSACTION : moves
    WAREHOUSE ||--o{ STOCK_TRANSACTION : at
    WAREHOUSE ||--o{ STOCK_TRANSFER : "from/to"
    STOCK_TRANSFER ||--|{ STOCK_TRANSFER_ITEM : lines

    SUPPLIER ||--o{ PURCHASE_ORDER : receives
    PURCHASE_REQUISITION ||--|{ PURCHASE_REQUISITION_ITEM : lines
    PURCHASE_ORDER ||--|{ PURCHASE_ORDER_ITEM : lines
    PURCHASE_ORDER ||--o{ GOODS_RECEIPT : "received by"
    GOODS_RECEIPT ||--|{ GOODS_RECEIPT_ITEM : lines
    PURCHASE_ORDER_ITEM ||--o{ GOODS_RECEIPT_ITEM : "fulfilled by"
    GOODS_RECEIPT ||--o| PURCHASE_INVOICE : "invoiced as"
    SUPPLIER ||--o{ PURCHASE_INVOICE : bills
    PURCHASE_INVOICE ||--|{ PURCHASE_INVOICE_ITEM : lines
    PURCHASE_INVOICE ||--o{ PURCHASE_RETURN : "credited by"
    PURCHASE_RETURN ||--|{ PURCHASE_RETURN_ITEM : lines

    CUSTOMER ||--o{ SALES_QUOTATION : receives
    SALES_QUOTATION ||--|{ SALES_QUOTATION_ITEM : lines
    CUSTOMER ||--o{ SALES_ORDER : places
    SALES_ORDER ||--|{ SALES_ORDER_ITEM : lines
    SALES_ORDER ||--o{ DELIVERY : "shipped by"
    DELIVERY ||--|{ DELIVERY_ITEM : lines
    SALES_ORDER_ITEM ||--o{ DELIVERY_ITEM : "fulfilled by"
    DELIVERY ||--o| SALES_INVOICE : "invoiced as"
    CUSTOMER ||--o{ SALES_INVOICE : billed
    SALES_INVOICE ||--|{ SALES_INVOICE_ITEM : lines
    SALES_INVOICE ||--o{ SALES_RETURN : "credited by"
    SALES_RETURN ||--|{ SALES_RETURN_ITEM : lines

    PRODUCT ||--o{ BILL_OF_MATERIAL : "is made by"
    BILL_OF_MATERIAL ||--|{ BOM_ITEM : components
    PRODUCT ||--o{ BOM_ITEM : "used in"
    BILL_OF_MATERIAL ||--o{ PRODUCTION_ORDER : "planned from"
    PRODUCTION_ORDER ||--|{ PRODUCTION_COMPONENT : requires
    PRODUCTION_ORDER ||--o{ MATERIAL_ISSUE : consumes
    MATERIAL_ISSUE ||--|{ MATERIAL_ISSUE_ITEM : lines
    PRODUCTION_ORDER ||--o{ PRODUCTION_RECEIPT : yields

    ACCOUNT ||--o{ JOURNAL_ENTRY_LINE : "posted to"
    JOURNAL_ENTRY ||--|{ JOURNAL_ENTRY_LINE : lines
    PURCHASE_INVOICE ||--o{ PAYMENT : "paid by"
    SALES_INVOICE ||--o{ PAYMENT : "collected by"
    ACCOUNT ||--o{ EXPENSE : categorises
    ACCOUNT ||--o{ INCOME : categorises

    PRODUCT { int Id PK; string Sku UK; string ProductType; decimal AverageCost; decimal SalePrice; decimal ReorderLevel; bool IsDeleted }
    STOCK_BALANCE { int Id PK; int ProductId FK; int WarehouseId FK; decimal Quantity }
    STOCK_TRANSACTION { int Id PK; int ProductId FK; int WarehouseId FK; string TransactionType; decimal Quantity; decimal UnitCost; string ReferenceType; int ReferenceId }
    JOURNAL_ENTRY { int Id PK; string Number UK; string ReferenceKey UK; decimal TotalDebit; decimal TotalCredit; string Status }
    PAYMENT { int Id PK; string Number UK; string IdempotencyKey UK; decimal Amount; string PaymentType }
```

Not drawn (no business relationships): `AuditLog`, `Notification`, `DocumentSequence`, `Setting`.
