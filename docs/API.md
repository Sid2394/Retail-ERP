# MiniERP API reference

Base URL (development): `http://localhost:5080`. All endpoints except `POST /api/auth/login` and `/health` require `Authorization: Bearer <jwt>`.
A machine-readable OpenAPI document is served at `/openapi/v1.json` in Development.

## Conventions

* **Lists** accept `page`, `pageSize`, `search`, `sortBy`, `sortDir` (`asc|desc`), `from`, `to`, `status`, `type`, `productId`, `warehouseId`, `partyId` and return `{ items, total, page, pageSize, summary? }` (`summary` = column totals for report endpoints).
* **Documents** (requisitions, orders, receipts, invoices, returns, transfers, deliveries, material issues) use a common `DocumentDto` with `partyId/partyName`, `warehouseId`, `refId` (source document), `lines[]`, totals and `status`.
* **Errors** are `{ "message": "..." }` with status 400 (validation/business rule), 401, 403 (missing permission), 404, 409 (duplicate / concurrency / wrong status).
* Stock and ledger changes happen only inside database transactions through the inventory/accounting services; repeated posting is rejected (status checks, unique journal reference keys, payment `idempotencyKey`).

## Generic master-data CRUD

Each supports `GET /api/{res}`, `GET /api/{res}/{id}`, `POST`, `PUT /{id}`, `DELETE /{id}` (soft delete).

| Resource | View / manage permission |
|---|---|
| `/api/categories` | inventory.view / inventory.manage |
| `/api/units` | inventory.view / inventory.manage |
| `/api/warehouses` | inventory.view / inventory.manage |
| `/api/suppliers` | procurement.view / procurement.manage |
| `/api/customers` | sales.view / sales.manage |
| `/api/accounts` | finance.view / finance.manage |

## Endpoints

| Method | Path | Permission |
|---|---|---|
| POST | `api/auth/login` | anonymous |
| POST | `api/auth/logout` | authenticated |
| GET | `api/auth/me` | authenticated |
| GET | `api/users` | admin.manage |
| POST | `api/users` | admin.manage |
| PUT | `api/users/{id}` | admin.manage |
| DELETE | `api/users/{id}` | admin.manage |
| GET | `api/roles` | admin.manage |
| POST | `api/roles` | admin.manage |
| PUT | `api/roles/{id}` | admin.manage |
| DELETE | `api/roles/{id}` | admin.manage |
| GET | `api/permissions` | admin.manage |
| GET | `api/audit-logs` | admin.manage |
| GET | `api/settings` | admin.manage |
| PUT | `api/settings` | admin.manage |
| GET | `api/dashboard` | authenticated |
| GET | `api/notifications` | authenticated |
| POST | `api/notifications/{id}/read` | authenticated |
| POST | `api/notifications/read-all` | authenticated |
| GET | `api/settings/public` | authenticated |
| GET | `api/products` | inventory.view |
| GET | `api/products/{id}` | inventory.view |
| POST | `api/products` | inventory.manage |
| PUT | `api/products/{id}` | inventory.manage |
| DELETE | `api/products/{id}` | inventory.manage |
| GET | `api/stock/balances` | inventory.view |
| GET | `api/stock/transactions` | inventory.view |
| POST | `api/stock/movement` | inventory.manage |
| GET | `api/stock-transfers` | inventory.view |
| GET | `api/stock-transfers/{id}` | inventory.view |
| POST | `api/stock-transfers` | inventory.manage |
| POST | `api/stock-transfers/{id}/complete` | inventory.manage |
| GET | `api/purchase-requisitions` | procurement.view |
| GET | `api/purchase-requisitions/{id}` | procurement.view |
| POST | `api/purchase-requisitions` | procurement.manage |
| PUT | `api/purchase-requisitions/{id}` | procurement.manage |
| POST | `api/purchase-requisitions/{id}/submit` | procurement.manage |
| POST | `api/purchase-requisitions/{id}/approve` | procurement.approve |
| POST | `api/purchase-requisitions/{id}/reject` | procurement.approve |
| POST | `api/purchase-requisitions/{id}/convert` | procurement.manage |
| GET | `api/purchase-orders` | procurement.view |
| GET | `api/purchase-orders/{id}` | procurement.view |
| POST | `api/purchase-orders` | procurement.manage |
| PUT | `api/purchase-orders/{id}` | procurement.manage |
| POST | `api/purchase-orders/{id}/approve` | procurement.approve |
| POST | `api/purchase-orders/{id}/cancel` | procurement.manage |
| POST | `api/purchase-orders/{id}/close` | procurement.manage |
| GET | `api/goods-receipts` | procurement.view |
| GET | `api/goods-receipts/{id}` | procurement.view |
| POST | `api/goods-receipts` | procurement.manage |
| POST | `api/goods-receipts/{id}/approve` | procurement.manage |
| POST | `api/goods-receipts/{id}/reject` | procurement.manage |
| GET | `api/purchase-invoices` | procurement.view |
| GET | `api/purchase-invoices/{id}` | procurement.view |
| POST | `api/purchase-invoices` | procurement.manage |
| GET | `api/purchase-returns` | procurement.view |
| POST | `api/purchase-returns` | procurement.manage |
| GET | `api/sales-quotations` | sales.view |
| POST | `api/sales-quotations` | sales.manage |
| PUT | `api/sales-quotations/{id}` | sales.manage |
| POST | `api/sales-quotations/{id}/send` | sales.manage |
| POST | `api/sales-quotations/{id}/accept` | sales.manage |
| POST | `api/sales-quotations/{id}/reject` | sales.manage |
| POST | `api/sales-quotations/{id}/convert` | sales.manage |
| GET | `api/sales-orders` | sales.view |
| GET | `api/sales-orders/{id}` | sales.view |
| POST | `api/sales-orders` | sales.manage |
| PUT | `api/sales-orders/{id}` | sales.manage |
| POST | `api/sales-orders/{id}/confirm` | sales.manage |
| POST | `api/sales-orders/{id}/cancel` | sales.manage |
| POST | `api/sales-orders/{id}/close` | sales.manage |
| GET | `api/deliveries` | sales.view |
| GET | `api/deliveries/{id}` | sales.view |
| POST | `api/deliveries` | sales.manage |
| POST | `api/deliveries/{id}/post` | sales.manage |
| GET | `api/sales-invoices` | sales.view |
| GET | `api/sales-invoices/{id}` | sales.view |
| POST | `api/sales-invoices` | sales.manage |
| GET | `api/sales-returns` | sales.view |
| POST | `api/sales-returns` | sales.manage |
| GET | `api/boms` | production.view |
| POST | `api/boms` | production.manage |
| PUT | `api/boms/{id}` | production.manage |
| DELETE | `api/boms/{id}` | production.manage |
| POST | `api/production/plan` | production.view |
| POST | `api/production/plan/requisition` | production.manage |
| GET | `api/production-orders` | production.view |
| POST | `api/production-orders` | production.manage |
| POST | `api/production-orders/{id}/release` | production.manage |
| POST | `api/production-orders/{id}/issue-materials` | production.manage |
| POST | `api/production-orders/{id}/complete` | production.manage |
| POST | `api/production-orders/{id}/cancel` | production.manage |
| GET | `api/material-issues` | production.view |
| GET | `api/production-receipts` | production.view |
| GET | `api/payments` | finance.view |
| POST | `api/payments` | finance.manage |
| GET | `api/payables` | finance.view |
| GET | `api/receivables` | finance.view |
| GET | `api/expenses` | finance.view |
| POST | `api/expenses` | finance.manage |
| GET | `api/incomes` | finance.view |
| POST | `api/incomes` | finance.manage |
| GET | `api/journal-entries` | finance.view |
| POST | `api/journal-entries` | finance.manage |
| POST | `api/journal-entries/{id}/post` | finance.manage |
| POST | `api/journal-entries/{id}/reverse` | finance.manage |
| GET | `api/finance/trial-balance` | finance.view |
| GET | `api/finance/general-ledger` | finance.view |
| GET | `api/finance/profit-loss` | finance.view |
| GET | `api/finance/balance-sheet` | finance.view |
| GET | `api/sales` | authenticated |
| GET | `api/purchases` | authenticated |
| GET | `api/inventory` | authenticated |
| GET | `api/stock-valuation` | authenticated |
| GET | `api/suppliers` | authenticated |
| GET | `api/customers` | authenticated |
| GET | `api/production` | authenticated |
| GET | `api/expenses` | authenticated |
| GET | `api/revenue` | authenticated |
| GET | `api/receivables` | authenticated |
| GET | `api/payables` | authenticated |
| GET | `api/analytics/{kind}` | authenticated |

## Key workflow calls

1. `POST /api/purchase-requisitions` → `/submit` → `/approve` → `/convert {supplierId, warehouseId}` creates a draft PO.
2. `POST /api/purchase-orders/{id}/approve`, then `POST /api/goods-receipts {refId: poId, lines:[{refLineId, quantity, rejectedQuantity}]}` and `POST /api/goods-receipts/{id}/approve` (QC passed → stock + GL).
3. `POST /api/purchase-invoices {refId: grnId, reference}` → AP; `POST /api/payments {type:"SupplierPayment", invoiceId, amount, method, idempotencyKey}`.
4. `POST /api/sales-orders/{id}/confirm` (stock validation) → `POST /api/deliveries {refId: soId, lines}` → `/post` (stock out + COGS) → `POST /api/sales-invoices {refId: deliveryId}` → `POST /api/payments {type:"CustomerReceipt", ...}`.
5. `POST /api/production/plan` (MRP) → `POST /api/production-orders` → `/release` → `/issue-materials` → `/complete {quantityProduced, qualityStatus, laborCost, overheadCost}`.
