namespace MiniERP.Api.Services;

public static class Seeder
{
    static readonly (string Name, string Desc, string[] Perms)[] Roles =
    {
        ("Admin", "Full access", Perms.All.Select(p => p.Code).ToArray()),
        ("Purchase Manager", "Procurement and suppliers", new[] { "inventory.view", "procurement.view", "procurement.manage", "procurement.approve", "reports.view" }),
        ("Sales Manager", "Sales and customers", new[] { "inventory.view", "sales.view", "sales.manage", "reports.view" }),
                ("Accountant", "Finance and accounting", new[] { "finance.view", "finance.manage", "inventory.view", "procurement.view", "sales.view", "reports.view" }),
        ("Viewer", "Read-only", new[] { "inventory.view", "procurement.view", "sales.view", "finance.view", "reports.view" }),
    };
    static readonly (string Code, string Name, string Type)[] Chart =
    {
        ("1000","Cash","Asset"),("1010","Bank","Asset"),("1100","Accounts Receivable","Asset"),("1300","Inventory","Asset"),("1500","Input Tax Receivable","Asset"),
        ("2000","Accounts Payable","Liability"),("2100","Goods Received Not Invoiced","Liability"),("2200","Output Tax Payable","Liability"),
        ("3000","Owner's Equity / Opening Balance","Equity"),
        ("4000","Sales Revenue","Revenue"),("4100","Sales Returns","Revenue"),("4200","Other Income","Revenue"),
        ("5000","Cost of Goods Sold","Expense"),("5150","Inventory Adjustments","Expense"),
        ("5200","Rent","Expense"),("5300","Utilities","Expense"),("5400","Salaries & Wages","Expense"),("5500","Office Supplies","Expense"),("5600","Transport & Freight","Expense"),("5900","Miscellaneous Expense","Expense"),
    };

    /// <summary>Roles, permissions, chart of accounts, default settings. Idempotent; safe in every environment.</summary>
    public static async Task EnsureReferenceData(ErpDbContext db)
    {
        foreach (var (code, desc) in Perms.All) if (!await db.Permissions.AnyAsync(p => p.Code == code)) db.Permissions.Add(new Permission { Code = code, Description = desc });
        await db.SaveChangesAsync();
        var perms = await db.Permissions.ToDictionaryAsync(p => p.Code);
        foreach (var (name, desc, ps) in Roles)
        {
            if (await db.Roles.AnyAsync(r => r.Name == name)) continue;
            db.Roles.Add(new Role { Name = name, Description = desc, RolePermissions = ps.Select(c => new RolePermission { Permission = perms[c] }).ToList() });
        }
        foreach (var (code, name, type) in Chart) if (!await db.Accounts.AnyAsync(a => a.Code == code)) db.Accounts.Add(new Account { Code = code, Name = name, AccountType = type, IsSystem = true });
        foreach (var (k, v) in new[] { ("CompanyName", "My Retail Shop"), ("CompanyAddress", ""), ("CurrencySymbol", "₹"), ("DefaultTaxRate", "18") })
            if (!await db.Settings.AnyAsync(s => s.Key == k)) db.Settings.Add(new Setting { Key = k, Value = v });
        await db.SaveChangesAsync();
    }

    public static async Task SeedDemo(IServiceProvider sp, string? defaultPassword)
{
    var db = sp.GetRequiredService<ErpDbContext>();

    if (string.IsNullOrWhiteSpace(defaultPassword))
        throw new InvalidOperationException(
            "Seed:DefaultPassword must be configured when creating the initial Admin account.");

    if (!await db.Users.IgnoreQueryFilters().AnyAsync())
    {
        var adminRole = await db.Roles.SingleAsync(r => r.Name == "Admin");

        db.Users.Add(new User
        {
            Username = "admin",
            FullName = "Shop Administrator",
            Email = "admin@retail.local",
            RoleId = adminRole.Id,
            PasswordHash = PasswordHasher.Hash(defaultPassword)
        });

        await db.SaveChangesAsync();
    }
}

    static async Task SeedRetailData(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ErpDbContext>();
        var inventory = sp.GetRequiredService<InventoryService>();
        var units = new[] { new Unit { Name = "Piece", Symbol = "pcs" }, new Unit { Name = "Pack", Symbol = "pack" }, new Unit { Name = "Kilogram", Symbol = "kg" }, new Unit { Name = "Bottle", Symbol = "btl" } };
        var categories = new[] { "Grocery", "Personal Care", "Household", "Stationery" }.Select(name => new Category { Name = name }).ToArray();
        var shop = new Warehouse { Code = "SHOP-01", Name = "Main Shop", Location = "My shop" };
        db.Units.AddRange(units); db.Categories.AddRange(categories); db.Warehouses.Add(shop);
        db.Suppliers.Add(new Supplier { Code = "SUP-001", Name = "Sample Supplier", Phone = "", Address = "" });
        db.Customers.Add(new Customer { Code = "CUS-WALKIN", Name = "Walk-in Customer", Phone = "", Address = "" });
        await db.SaveChangesAsync();
        Unit U(string symbol) => units.Single(x => x.Symbol == symbol);
        Category C(string name) => categories.Single(x => x.Name == name);
        var products = new[]
        {
            new Product { Sku = "GRO-001", Name = "Rice 1 kg", CategoryId = C("Grocery").Id, UnitId = U("kg").Id, ProductType = ProductTypes.TradingProduct, AverageCost = 52, SalePrice = 65, TaxRate = 0, ReorderLevel = 10 },
            new Product { Sku = "GRO-002", Name = "Cooking Oil 1 L", CategoryId = C("Grocery").Id, UnitId = U("btl").Id, ProductType = ProductTypes.TradingProduct, AverageCost = 110, SalePrice = 135, TaxRate = 5, ReorderLevel = 8 },
            new Product { Sku = "PER-001", Name = "Bath Soap", CategoryId = C("Personal Care").Id, UnitId = U("pcs").Id, ProductType = ProductTypes.TradingProduct, AverageCost = 28, SalePrice = 35, TaxRate = 18, ReorderLevel = 12 },
            new Product { Sku = "HOU-001", Name = "Dishwash Liquid", CategoryId = C("Household").Id, UnitId = U("btl").Id, ProductType = ProductTypes.TradingProduct, AverageCost = 65, SalePrice = 85, TaxRate = 18, ReorderLevel = 6 },
            new Product { Sku = "STA-001", Name = "Ball Pen Pack", CategoryId = C("Stationery").Id, UnitId = U("pack").Id, ProductType = ProductTypes.TradingProduct, AverageCost = 30, SalePrice = 45, TaxRate = 12, ReorderLevel = 5 }
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        var opening = new[] { 40m, 25m, 60m, 20m, 30m };
        for (var i = 0; i < products.Length; i++)
            await inventory.Movement(new MovementRequest { ProductId = products[i].Id, WarehouseId = shop.Id, Type = TxType.Opening, Quantity = opening[i], UnitCost = products[i].AverageCost, Notes = "Sample opening stock – replace with your actual stock" });
    }
}
