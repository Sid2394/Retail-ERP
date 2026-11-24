global using Xunit;
global using Microsoft.EntityFrameworkCore;
global using MiniERP.Api.Domain;
global using MiniERP.Api.Data;
global using MiniERP.Api.Dtos;
global using MiniERP.Api.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MiniERP.Tests;

/// <summary>Builds an isolated in-memory ERP with reference data (chart of accounts, roles) and a small product catalogue.</summary>
public class World
{
    public ServiceProvider Sp; public ErpDbContext Db;
    public ProcurementService Pur; public SalesService Sal; public ManufacturingService Man; public FinanceService Fin; public InventoryService Inv; public AccountingService Acct; public ReportService Rep; public AuthService Auth;
    public int Main, Rm, Other, Supplier, Customer, Paper, Cover, Binding, Notebook, Pen, CatId, UnitId;

    public static async Task<World> Create()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ErpDbContext>(o => o.UseInMemoryDatabase(dbName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<ICurrentUser, SystemUser>();
        foreach (var t in new[] { typeof(NumberingService), typeof(AccountingService), typeof(InventoryService), typeof(ProcurementService), typeof(SalesService), typeof(ManufacturingService), typeof(FinanceService), typeof(ReportService), typeof(AuthService) }) services.AddScoped(t);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "unit-test-key-unit-test-key-unit-test-key", ["Jwt:Issuer"] = "t", ["Jwt:Audience"] = "t" }).Build();
        services.AddSingleton<IConfiguration>(cfg);
        var sp = services.BuildServiceProvider(); var scope = sp.CreateScope(); var s = scope.ServiceProvider;
        var w = new World { Sp = sp, Db = s.GetRequiredService<ErpDbContext>() };
        w.Pur = s.GetRequiredService<ProcurementService>(); w.Sal = s.GetRequiredService<SalesService>(); w.Man = s.GetRequiredService<ManufacturingService>(); w.Fin = s.GetRequiredService<FinanceService>();
        w.Inv = s.GetRequiredService<InventoryService>(); w.Acct = s.GetRequiredService<AccountingService>(); w.Rep = s.GetRequiredService<ReportService>(); w.Auth = s.GetRequiredService<AuthService>();
        await Seeder.EnsureReferenceData(w.Db);
        var cat = new Category { Name = "Test" }; var unit = new Unit { Name = "Piece", Symbol = "pcs" }; w.Db.AddRange(cat, unit);
        var main = new Warehouse { Code = "M", Name = "Main" }; var rm = new Warehouse { Code = "R", Name = "RM" }; var oth = new Warehouse { Code = "O", Name = "Other" };
        w.Db.AddRange(main, rm, oth);
        w.Db.Suppliers.Add(new Supplier { Code = "S1", Name = "Supp" }); w.Db.Customers.Add(new Customer { Code = "C1", Name = "Cust" });
        await w.Db.SaveChangesAsync();
        Product P(string sku, string type, decimal price = 0) => new() { Sku = sku, Name = sku, CategoryId = cat.Id, UnitId = unit.Id, ProductType = type, SalePrice = price, TaxRate = 10 };
        var paper = P("PAPER", ProductTypes.RawMaterial); var cover = P("COVER", ProductTypes.RawMaterial); var bind = P("BIND", ProductTypes.RawMaterial); var nb = P("NB", ProductTypes.FinishedGood, 150); var pen = P("PEN", ProductTypes.TradingProduct, 20);
        w.Db.Products.AddRange(paper, cover, bind, nb, pen); await w.Db.SaveChangesAsync();
        w.Main = main.Id; w.Rm = rm.Id; w.Other = oth.Id; w.Supplier = w.Db.Suppliers.First().Id; w.Customer = w.Db.Customers.First().Id;
        w.Paper = paper.Id; w.Cover = cover.Id; w.Binding = bind.Id; w.Notebook = nb.Id; w.Pen = pen.Id; w.CatId = cat.Id; w.UnitId = unit.Id;
        return w;
    }
    public async Task<decimal> Qty(int product, int wh) => await Inv.Available(product, wh);
    public int Acc(string code) => Db.Accounts.First(a => a.Code == code).Id;
    public async Task<decimal> Balance(string code, bool debitNormal = true)
    {
        var id = Acc(code); var lines = await Db.JournalEntryLines.Where(l => l.AccountId == id && l.JournalEntry!.Status == St.Posted).ToListAsync();
        return debitNormal ? lines.Sum(l => l.Debit - l.Credit) : lines.Sum(l => l.Credit - l.Debit);
    }
    public Task Open(int product, int wh, decimal qty, decimal cost) => Inv.Movement(new MovementRequest { ProductId = product, WarehouseId = wh, Type = TxType.Opening, Quantity = qty, UnitCost = cost });
}
public static class TestHelpers
{
    public static T WithPerms<T>(this T c, params string[] perms) where T : ControllerBase
    {
        var id = new ClaimsIdentity(perms.Select(p => new Claim("perm", p)), "test", "name", "role");
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(id) } };
        return c;
    }
}
