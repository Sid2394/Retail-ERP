using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiniERP.Api.Controllers;

[Authorize]
public abstract class CrudController<TEntity, TDto>(ErpDbContext db, string viewPerm, string managePerm) : ControllerBase
    where TEntity : BaseEntity, new() where TDto : class, new()
{
    protected bool Can(string p) => User.HasClaim("perm", p);
    protected virtual IQueryable<TEntity> Search(IQueryable<TEntity> q, string s) => q;
    protected virtual Task Validate(TEntity e, bool isNew) => Task.CompletedTask;
    protected virtual Task<string?> CanDelete(TEntity e) => Task.FromResult<string?>(null);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery pq)
    {
        if (!Can(viewPerm)) return Forbid();
        IQueryable<TEntity> q = db.Set<TEntity>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(pq.Search)) q = Search(q, pq.Search.Trim());
        var total = await q.CountAsync();
        var prop = typeof(TEntity).GetProperty(pq.SortBy ?? "", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var asc = string.Equals(pq.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        if (prop != null && (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(decimal) || prop.PropertyType == typeof(DateTime)))
            q = asc ? q.OrderBy(e => EF.Property<object>(e, prop.Name)) : q.OrderByDescending(e => EF.Property<object>(e, prop.Name));
        else q = q.OrderByDescending(e => e.Id);
        var size = Math.Clamp(pq.PageSize, 1, 100000); var page = Math.Max(pq.Page, 1);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return Ok(new PagedResult<TDto> { Items = items.Select(DtoMapper.ToDto<TEntity, TDto>).ToList(), Total = total, Page = page, PageSize = size });
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        if (!Can(viewPerm)) return Forbid();
        var e = await db.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return e == null ? NotFound(new { message = "Record not found." }) : Ok(DtoMapper.ToDto<TEntity, TDto>(e));
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TDto dto)
    {
        if (!Can(managePerm)) return Forbid();
        var e = new TEntity(); DtoMapper.Apply(dto, e); await Validate(e, true);
        db.Set<TEntity>().Add(e); await db.SaveChangesAsync();
        return Ok(DtoMapper.ToDto<TEntity, TDto>(e));
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TDto dto)
    {
        if (!Can(managePerm)) return Forbid();
        var e = await db.Set<TEntity>().FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound(new { message = "Record not found." });
        DtoMapper.Apply(dto, e); await Validate(e, false); await db.SaveChangesAsync();
        return Ok(DtoMapper.ToDto<TEntity, TDto>(e));
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!Can(managePerm)) return Forbid();
        var e = await db.Set<TEntity>().FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound(new { message = "Record not found." });
        var why = await CanDelete(e); if (why != null) throw new BusinessException(why);
        db.Remove(e);

await db.SaveChangesAsync();
return NoContent();
    }
}

[ApiController, Route("api/categories")]
public class CategoriesController(ErpDbContext db) : CrudController<Category, CategoryDto>(db, "inventory.view", "inventory.manage")
{
    protected override IQueryable<Category> Search(IQueryable<Category> q, string s) => q.Where(x => x.Name.Contains(s));
    protected override Task Validate(Category e, bool n) => string.IsNullOrWhiteSpace(e.Name) ? throw new BusinessException("Name is required.") : Task.CompletedTask;
}
[ApiController, Route("api/units")]
public class UnitsController(ErpDbContext db) : CrudController<Unit, UnitDto>(db, "inventory.view", "inventory.manage")
{
    protected override IQueryable<Unit> Search(IQueryable<Unit> q, string s) => q.Where(x => x.Name.Contains(s) || x.Symbol.Contains(s));
    protected override Task Validate(Unit e, bool n) => string.IsNullOrWhiteSpace(e.Name) || string.IsNullOrWhiteSpace(e.Symbol) ? throw new BusinessException("Name and symbol are required.") : Task.CompletedTask;
}
[ApiController, Route("api/warehouses")]
public class WarehousesController(ErpDbContext db) : CrudController<Warehouse, WarehouseDto>(db, "inventory.view", "inventory.manage")
{
    protected override IQueryable<Warehouse> Search(IQueryable<Warehouse> q, string s) => q.Where(x => x.Name.Contains(s) || x.Code.Contains(s));
    protected override async Task Validate(Warehouse e, bool n)
    {
        if (string.IsNullOrWhiteSpace(e.Code) || string.IsNullOrWhiteSpace(e.Name)) throw new BusinessException("Code and name are required.");
        if (await db.Warehouses.AnyAsync(x => x.Code == e.Code && x.Id != e.Id)) throw new BusinessException("Warehouse code already exists.", 409);
    }
    protected override async Task<string?> CanDelete(Warehouse e) => await db.StockBalances.AnyAsync(b => b.WarehouseId == e.Id && b.Quantity != 0) ? "Warehouse still holds stock." : null;
}
[ApiController, Route("api/suppliers")]
public class SuppliersController(ErpDbContext db) : CrudController<Supplier, SupplierDto>(db, "procurement.view", "procurement.manage")
{
    protected override IQueryable<Supplier> Search(IQueryable<Supplier> q, string s) => q.Where(x => x.Name.Contains(s) || x.Code.Contains(s) || (x.Email != null && x.Email.Contains(s)));
    protected override async Task Validate(Supplier e, bool n)
    {
        if (string.IsNullOrWhiteSpace(e.Code) || string.IsNullOrWhiteSpace(e.Name)) throw new BusinessException("Code and name are required.");
        if (await db.Suppliers.AnyAsync(x => x.Code == e.Code && x.Id != e.Id)) throw new BusinessException("Supplier code already exists.", 409);
    }
    protected override async Task<string?> CanDelete(Supplier e) => await db.PurchaseInvoices.AnyAsync(i => i.SupplierId == e.Id && i.Total - i.PaidAmount - i.CreditedAmount > 0) ? "Supplier has outstanding invoices." : null;
}
[ApiController, Route("api/customers")]
public class CustomersController(ErpDbContext db) : CrudController<Customer, CustomerDto>(db, "sales.view", "sales.manage")
{
    protected override IQueryable<Customer> Search(IQueryable<Customer> q, string s) => q.Where(x => x.Name.Contains(s) || x.Code.Contains(s) || (x.Email != null && x.Email.Contains(s)));
    protected override async Task Validate(Customer e, bool n)
    {
        if (string.IsNullOrWhiteSpace(e.Code) || string.IsNullOrWhiteSpace(e.Name)) throw new BusinessException("Code and name are required.");
        if (await db.Customers.AnyAsync(x => x.Code == e.Code && x.Id != e.Id)) throw new BusinessException("Customer code already exists.", 409);
    }
    protected override async Task<string?> CanDelete(Customer e) => await db.SalesInvoices.AnyAsync(i => i.CustomerId == e.Id && i.Total - i.PaidAmount - i.CreditedAmount > 0) ? "Customer has outstanding invoices." : null;
}
[ApiController, Route("api/accounts")]
public class AccountsController(ErpDbContext db) : CrudController<Account, AccountDto>(db, "finance.view", "finance.manage")
{
    static readonly string[] Types = { "Asset", "Liability", "Equity", "Revenue", "Expense" };
    protected override IQueryable<Account> Search(IQueryable<Account> q, string s) => q.Where(x => x.Name.Contains(s) || x.Code.Contains(s));
    protected override async Task Validate(Account e, bool n)
    {
        if (string.IsNullOrWhiteSpace(e.Code) || string.IsNullOrWhiteSpace(e.Name)) throw new BusinessException("Code and name are required.");
        if (!Types.Contains(e.AccountType)) throw new BusinessException("Invalid account type.");
        if (await db.Accounts.AnyAsync(x => x.Code == e.Code && x.Id != e.Id)) throw new BusinessException("Account code already exists.", 409);
    }
    protected override async Task<string?> CanDelete(Account e) => e.IsSystem ? "System accounts cannot be deleted." : await db.JournalEntryLines.AnyAsync(l => l.AccountId == e.Id) ? "Account has journal activity." : null;
}
