using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MiniERP.Api.Data;

public class ErpDbContext(DbContextOptions<ErpDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<PurchaseRequisitionItem> PurchaseRequisitionItems => Set<PurchaseRequisitionItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();
    public DbSet<SalesQuotation> SalesQuotations => Set<SalesQuotation>();
    public DbSet<SalesQuotationItem> SalesQuotationItems => Set<SalesQuotationItem>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();
    public DbSet<BillOfMaterial> BillsOfMaterials => Set<BillOfMaterial>();
    public DbSet<BomItem> BomItems => Set<BomItem>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionComponent> ProductionComponents => Set<ProductionComponent>();
    public DbSet<MaterialIssue> MaterialIssues => Set<MaterialIssue>();
    public DbSet<MaterialIssueItem> MaterialIssueItems => Set<MaterialIssueItem>();
    public DbSet<ProductionReceipt> ProductionReceipts => Set<ProductionReceipt>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder cb) => cb.Properties<decimal>().HavePrecision(18, 4);

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        b.Entity<User>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Permission>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        b.Entity<Product>().HasIndex(x => x.ProductType);
        b.Entity<Warehouse>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Supplier>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Account>().HasIndex(x => x.Code).IsUnique();
        b.Entity<BillOfMaterial>().HasIndex(x => x.Code).IsUnique();
        b.Entity<StockBalance>().HasIndex(x => new { x.ProductId, x.WarehouseId }).IsUnique();
        b.Entity<StockTransaction>().HasIndex(x => new { x.ProductId, x.WarehouseId, x.Date });
        b.Entity<StockTransaction>().HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        b.Entity<DocumentSequence>().HasIndex(x => x.Prefix).IsUnique();
        b.Entity<Setting>().HasIndex(x => x.Key).IsUnique();
        b.Entity<AuditLog>().HasIndex(x => x.Timestamp);
        
        // Duplicate-posting guards (PostgreSQL filtered unique indexes)
b.Entity<JournalEntry>().HasIndex(x => x.ReferenceKey)
    .IsUnique().HasFilter("\"ReferenceKey\" IS NOT NULL");

b.Entity<Payment>().HasIndex(x => x.IdempotencyKey)
    .IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");

b.Entity<PurchaseInvoice>().HasIndex(x => x.GoodsReceiptId)
    .IsUnique().HasFilter("\"GoodsReceiptId\" IS NOT NULL");

b.Entity<SalesInvoice>().HasIndex(x => x.DeliveryId)
    .IsUnique().HasFilter("\"DeliveryId\" IS NOT NULL");

        foreach (var t in b.Model.GetEntityTypes().ToList())
        {
            var clr = t.ClrType;
            if (typeof(ISoftDelete).IsAssignableFrom(clr))
            {
                var p = Expression.Parameter(clr, "e");
                var body = Expression.Equal(Expression.Property(p, nameof(ISoftDelete.IsDeleted)), Expression.Constant(false));
                b.Entity(clr).HasQueryFilter(Expression.Lambda(body, p));
            }
            if (typeof(DocEntity).IsAssignableFrom(clr))
            {
                b.Entity(clr).HasIndex("Number").IsUnique();
                b.Entity(clr).HasIndex("Status");
                b.Entity(clr).HasIndex("Date");
                b.Entity(clr).Property<uint>("RowVersion").IsRowVersion();
            }
        }
        b.Entity<DocumentSequence>().Property(x => x.RowVersion).IsRowVersion();
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            var child = fk.DeclaringEntityType.ClrType; var parent = fk.PrincipalEntityType.ClrType;
            var isLine = typeof(ItemBase).IsAssignableFrom(child) || child == typeof(BomItem) || child == typeof(ProductionComponent) || child == typeof(JournalEntryLine) || child == typeof(RolePermission);
            var isHeader = typeof(DocEntity).IsAssignableFrom(parent) || parent == typeof(BillOfMaterial) || parent == typeof(Role) || parent == typeof(Permission);
            fk.DeleteBehavior = isLine && isHeader ? DeleteBehavior.Cascade : DeleteBehavior.Restrict;
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var pending = new List<(object Entity, string Action, string Name, string? Details)>();
        foreach (var e in ChangeTracker.Entries<BaseEntity>().ToList())
        {
            if (e.State == EntityState.Added) { e.Entity.CreatedAt = now; pending.Add((e.Entity, "Create", e.Metadata.ClrType.Name, null)); }
            else if (e.State == EntityState.Modified)
            {
                e.Entity.UpdatedAt = now;
                var isDel = e.Entity is ISoftDelete sd && sd.IsDeleted && e.Property("IsDeleted").IsModified;
                var changed = string.Join(", ", e.Properties.Where(p => p.IsModified && p.Metadata.Name != "UpdatedAt" && p.Metadata.Name != "RowVersion").Select(p => p.Metadata.Name));
                pending.Add((e.Entity, isDel ? "Delete" : "Update", e.Metadata.ClrType.Name, changed));
            }
        }
        var result = await base.SaveChangesAsync(ct);
        if (pending.Count > 0)
        {
            foreach (var p in pending)
                AuditLogs.Add(new AuditLog { Timestamp = now, UserName = currentUser.UserName, Action = p.Action, EntityName = p.Name, EntityId = ((BaseEntity)p.Entity).Id, Details = p.Details });
            await base.SaveChangesAsync(ct);
        }
        return result;
    }
}

public static class DbExtensions
{
    /// <summary>Runs work in a DB transaction (re-uses an ambient one if present).</summary>
    public static async Task<T> InTx<T>(this ErpDbContext db, Func<Task<T>> work)
    {
        if (db.Database.CurrentTransaction != null) return await work();
        await using var tx = await db.Database.BeginTransactionAsync();
        try { var r = await work(); await tx.CommitAsync(); return r; }
        catch { await tx.RollbackAsync(); db.ChangeTracker.Clear(); throw; }
    }
    public static Task InTx(this ErpDbContext db, Func<Task> work) => db.InTx<bool>(async () => { await work(); return true; });
}
