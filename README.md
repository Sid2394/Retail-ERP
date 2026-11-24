# Retail ERP – JAI BADRI KEDAR TRADERS Edition

This is a retail-trading adaptation of MiniERP for **one physical shop**. It is intended for personal use by a shop owner. It manages finished goods purchased from suppliers and sold to walk-in or registered customers. It is not a manufacturing ERP and it is not a stock-market trading application.

## Included areas

- Dashboard: sales, purchases, stock value, expenses, profit and low-stock overview.
- Products and inventory: retail products, categories, units, opening stock, stock-in/out and stock movement history.
- Purchases: suppliers, purchase orders, goods received, supplier invoices and purchase returns.
- Retail sales: customers, sales orders, delivery records, sales invoices and sales returns.
- Accounts: supplier payables, customer receivables, payments, expenses and financial reports.
- Reports: sales, purchases, inventory, stock valuation and financial analytics.
- One default stock location named **Main Shop**. Warehouse transfers and manufacturing screens are removed from the normal navigation.

## Important workflow notes

The current sales workflow is document-based: create a sales order, confirm it, create/post its delivery, then create the sales invoice and record the customer receipt. This ZIP does **not** add a dedicated barcode-based POS checkout screen, thermal-printer integration, or automatic payment-gateway/UPI integration. Payments can be recorded manually using the payment module.

Purchase workflow: create a purchase order, receive the goods, create the supplier invoice, then record the supplier payment. Stock is increased when the goods receipt is approved.

The starter data contains sample products and sample opening stock. Replace these with your actual products, prices, tax rates, suppliers, customers and stock quantities before using the figures for real business decisions. Tax rates are editable per product; confirm applicable GST treatment with your accountant.

## Technology

| Layer | Technology |
|---|---|
| Frontend | React 18, TypeScript, Vite, Tailwind CSS, Axios, Recharts, React Router |
| Backend | C#, ASP.NET Core Web API, Entity Framework Core, JWT |
| Database | Microsoft SQL Server |

## Requirements (Windows)

- .NET SDK matching the target framework in `backend/MiniERP.Api/MiniERP.Api.csproj` (the project targets .NET 10).
- Node.js 20 or later.
- SQL Server Express, SQL Server Developer, or LocalDB.
- Optional: SSMS to inspect the database.

## First-time setup

Open PowerShell in the extracted `MiniERP` folder:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\setup-dev.ps1
```

The setup script installs frontend dependencies, configures a development JWT secret, creates the initial EF migration and database. Then start both applications:

```powershell
.\scripts\run-all.ps1
```

Open the frontend at `http://localhost:5173`. The API is at `http://localhost:5080`; health check: `http://localhost:5080/health`; API documentation: `http://localhost:5080/openapi/v1.json` in Development.

## Development login

If demo seeding is enabled in Development settings:

- Username: `admin`
- Password: `Erp@12345`

Change the password before any use beyond local development. Do not expose the development server to the public internet.

## Existing database warning

This adaptation changes the starter seed data and the intended single-shop navigation. If you have already run the earlier MiniERP version, **back up your database first**. For a clean retail setup, use a new empty database (or drop the old development database only if you are certain you do not need its data), then run the setup script. Existing databases are not automatically converted or cleaned of old manufacturing records.

## Verification status

This ZIP is a source-code adaptation. The project has not been compiled or run against your local SQL Server in this environment. After extracting it, run `dotnet build MiniERP.sln`, `dotnet test MiniERP.sln`, and in `frontend`, `npm install` followed by `npm run build`. Resolve any environment-specific errors before entering live business data.
