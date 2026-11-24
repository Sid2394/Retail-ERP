# MiniERP – Database documentation (SQL Server, EF Core Code First)

Schema is defined by the entity classes in `backend/MiniERP.Api/Domain/Entities.cs` and configured in `Data/ErpDbContext.cs`. Generate the SQL with `dotnet ef migrations script` after creating the initial migration.

## Conventions
| Concern | Implementation |
|---|---|
| Keys | Integer identity `Id` on every table (`RolePermission` has a composite key). |
| Money / quantities | `decimal(18,4)` globally (`ConfigureConventions`). |
| Timestamps | `CreatedAt` / `UpdatedAt` maintained in `SaveChangesAsync`. |
| Soft delete | `IsDeleted` + global query filter on master data: User, Category, Unit, Warehouse, Product, Supplier, Customer, Account, BillOfMaterial. Transaction documents are never deleted; they move through statuses (e.g. Cancelled). |
| Status | `Status` string column (indexed) on every document. |
| Audit trail | `AuditLogs` row for each insert/update/delete of any `BaseEntity` (user, action, entity, id, changed fields). |
| Concurrency | `RowVersion` (`rowversion`) on every document table and on `DocumentSequences`; conflicts return HTTP 409. |
| Document numbers | `DocumentSequences` per prefix, formatted `PREFIX-YYYY-00001`; unique index on `Number` for every document table. |
| Referential integrity | All FKs `Restrict` except line items → header, BOM items → BOM, journal lines → entry and role permissions, which cascade. |

## Integrity guards against duplicate transactions
* Filtered unique index `JournalEntries(ReferenceKey)` – each business event (e.g. `GRN:12`, `PINV:7`, `DN:3`, `PAY:9`, `STK:41`) can post to the ledger once.
* Filtered unique index `Payments(IdempotencyKey)` + service-level lookup – retried payment requests return the original payment.
* Filtered unique indexes `PurchaseInvoices(GoodsReceiptId)` and `SalesInvoices(DeliveryId)` – one invoice per receipt/delivery.
* Status state-machine checks (`Draft → Approved/Posted`) executed inside a transaction, with `rowversion` as the concurrency backstop.
* Stock balances change **only** in `InventoryService.Move`, which always inserts a `StockTransactions` row; a test asserts `balance = Σ transactions`.

## Tables
**Security** – Users, Roles, Permissions, RolePermissions
**Master data** – Categories, Units, Warehouses, Products, Suppliers, Customers, Accounts, BillsOfMaterials, BomItems
**Inventory** – StockBalances (unique Product+Warehouse), StockTransactions, StockTransfers, StockTransferItems
**Procurement** – PurchaseRequisitions(+Items), PurchaseOrders(+Items), GoodsReceipts(+Items), PurchaseInvoices(+Items), PurchaseReturns(+Items)
**Sales** – SalesQuotations(+Items), SalesOrders(+Items), Deliveries(+Items), SalesInvoices(+Items), SalesReturns(+Items)
**Manufacturing** – ProductionOrders, ProductionComponents, MaterialIssues(+Items), ProductionReceipts
**Finance** – JournalEntries, JournalEntryLines, Payments, Expenses, Incomes
**System** – AuditLogs, Notifications, DocumentSequences, Settings

## Chart of accounts (seeded)
1000 Cash · 1010 Bank · 1100 Accounts Receivable · 1300 Inventory · 1400 Work in Progress · 1500 Input Tax Receivable · 2000 Accounts Payable · 2100 Goods Received Not Invoiced · 2200 Output Tax Payable · 3000 Owner's Equity · 4000 Sales Revenue · 4100 Sales Returns · 4200 Other Income · 5000 Cost of Goods Sold · 5100 Production Overhead Absorbed · 5150 Inventory Adjustments · 5200–5900 operating expenses.

## Automatic postings
| Event | Inventory | Journal |
|---|---|---|
| GRN approved | + accepted qty at PO price (moving-average cost) | Dr Inventory / Cr GR-IR (2100) |
| Purchase invoice | – | Dr GR-IR + Dr Input tax / Cr Accounts Payable |
| Supplier payment | – | Dr Accounts Payable / Cr Cash or Bank |
| Purchase return | – qty | Dr AP / Cr Inventory + Input tax (variance → 5150) |
| Delivery posted | – qty at average cost | Dr COGS / Cr Inventory |
| Sales invoice | – | Dr AR / Cr Sales + Output tax |
| Customer receipt | – | Dr Cash or Bank / Cr AR |
| Sales return | + qty | Dr Sales Returns + Output tax / Cr AR; Dr Inventory / Cr COGS |
| Material issue | – raw material | Dr WIP / Cr Inventory |
| Production complete (QC pass) | + finished goods at (material+labor+overhead)/qty | Dr Inventory / Cr WIP + Overhead absorbed |
| Production QC failed | – | Dr Inventory Adjustments / Cr WIP |
| Stock transfer | – source, + target | none (same inventory account) |
| Opening stock / stock in-out / adjustments | ± | Opening: vs Equity (3000); others: vs Inventory Adjustments (5150) |
| Expense / Other income | – | Dr Expense / Cr Cash-Bank; Dr Cash-Bank / Cr Income |
