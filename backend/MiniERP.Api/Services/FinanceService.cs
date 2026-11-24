namespace MiniERP.Api.Services;

public class FinanceService(ErpDbContext db, ICurrentUser user, NumberingService num, AccountingService acct) : ServiceBase
{
    public IQueryable<Payment> Payments => db.Payments.Include(p => p.Supplier).Include(p => p.Customer).Include(p => p.PurchaseInvoice).Include(p => p.SalesInvoice);
    public static object PaymentRow(Payment p) => new
    {
        p.Id, p.Number, p.Date, Type = p.PaymentType, Party = p.PaymentType == "SupplierPayment" ? p.Supplier?.Name : p.Customer?.Name,
        Invoice = p.PaymentType == "SupplierPayment" ? p.PurchaseInvoice?.Number : p.SalesInvoice?.Number, p.Amount, p.Method, p.Reference, p.Status, p.Notes
    };

    /// <summary>Records a supplier payment or customer receipt against one invoice. Idempotent on IdempotencyKey.</summary>
    public Task<Payment> RecordPayment(PaymentRequest r) => db.InTx(async () =>
    {
        if (r.Amount <= 0) throw new BusinessException("Amount must be greater than zero.");
        if (!string.IsNullOrWhiteSpace(r.IdempotencyKey))
        {
            var existing = await db.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == r.IdempotencyKey);
            if (existing != null) return existing;
        }
        var date = Day(r.Date ?? default);
        var cashAcc = Acc.ForMethod(r.Method);
        Payment pay;
        if (r.Type == "SupplierPayment")
        {
            var inv = await db.PurchaseInvoices.FirstOrDefaultAsync(i => i.Id == r.InvoiceId) ?? throw NF("Purchase invoice");
            var bal = inv.Total - inv.PaidAmount - inv.CreditedAmount;
            if (r.Amount > bal + 0.0001m) throw new BusinessException($"Payment ({r.Amount}) exceeds the outstanding balance ({bal}).");
            pay = new Payment { Number = await num.Next(Prefix.PAY), Date = date, PaymentType = r.Type, SupplierId = inv.SupplierId, PurchaseInvoiceId = inv.Id, Amount = r.Amount, Method = r.Method, Reference = r.Reference, IdempotencyKey = r.IdempotencyKey, Status = St.Posted, Notes = r.Notes, CreatedBy = user.UserName };
            db.Payments.Add(pay); inv.PaidAmount += r.Amount; inv.Status = Settle(inv.Total, inv.PaidAmount, inv.CreditedAmount);
            await db.SaveChangesAsync();
            await acct.Post(date, $"Supplier payment {pay.Number} for {inv.Number}", "Payment", pay.Id, $"PAY:{pay.Id}", AccountingService.Dr(Acc.AP, r.Amount), AccountingService.Cr(cashAcc, r.Amount));
        }
        else if (r.Type == "CustomerReceipt")
        {
            var inv = await db.SalesInvoices.FirstOrDefaultAsync(i => i.Id == r.InvoiceId) ?? throw NF("Sales invoice");
            var bal = inv.Total - inv.PaidAmount - inv.CreditedAmount;
            if (r.Amount > bal + 0.0001m) throw new BusinessException($"Receipt ({r.Amount}) exceeds the outstanding balance ({bal}).");
            pay = new Payment { Number = await num.Next(Prefix.REC), Date = date, PaymentType = r.Type, CustomerId = inv.CustomerId, SalesInvoiceId = inv.Id, Amount = r.Amount, Method = r.Method, Reference = r.Reference, IdempotencyKey = r.IdempotencyKey, Status = St.Posted, Notes = r.Notes, CreatedBy = user.UserName };
            db.Payments.Add(pay); inv.PaidAmount += r.Amount; inv.Status = Settle(inv.Total, inv.PaidAmount, inv.CreditedAmount);
            await db.SaveChangesAsync();
            await acct.Post(date, $"Customer receipt {pay.Number} for {inv.Number}", "Payment", pay.Id, $"PAY:{pay.Id}", AccountingService.Dr(cashAcc, r.Amount), AccountingService.Cr(Acc.AR, r.Amount));
        }
        else throw new BusinessException("Payment type must be SupplierPayment or CustomerReceipt.");
        return pay;
    });

    // ---------- Expenses & income ----------
    public IQueryable<Expense> Expenses => db.Expenses.Include(e => e.Account);
    public IQueryable<Income> Incomes => db.Incomes.Include(e => e.Account);
    public Task<Expense> CreateExpense(CashEntryDto d) => db.InTx(async () =>
    {
        if (d.Amount <= 0) throw new BusinessException("Amount must be greater than zero.");
        var a = await db.Accounts.FirstOrDefaultAsync(x => x.Id == d.AccountId) ?? throw NF("Expense account");
        if (a.AccountType != AccountTypes.Expense) throw new BusinessException("Select an expense account.");
        var e = new Expense { Number = await num.Next(Prefix.EXP), Date = Day(d.Date), AccountId = a.Id, Amount = d.Amount, Method = d.Method, Payee = d.Party, Description = d.Description, Status = St.Posted, CreatedBy = user.UserName };
        db.Expenses.Add(e); await db.SaveChangesAsync();
        await acct.Post(e.Date, $"Expense {e.Number} - {a.Name}", "Expense", e.Id, $"EXP:{e.Id}", AccountingService.Dr(a.Code, d.Amount), AccountingService.Cr(Acc.ForMethod(d.Method), d.Amount));
        return e;
    });
    public Task<Income> CreateIncome(CashEntryDto d) => db.InTx(async () =>
    {
        if (d.Amount <= 0) throw new BusinessException("Amount must be greater than zero.");
        var a = await db.Accounts.FirstOrDefaultAsync(x => x.Id == d.AccountId) ?? throw NF("Income account");
        if (a.AccountType != AccountTypes.Revenue) throw new BusinessException("Select a revenue account.");
        var e = new Income { Number = await num.Next(Prefix.INC), Date = Day(d.Date), AccountId = a.Id, Amount = d.Amount, Method = d.Method, Source = d.Party, Description = d.Description, Status = St.Posted, CreatedBy = user.UserName };
        db.Incomes.Add(e); await db.SaveChangesAsync();
        await acct.Post(e.Date, $"Income {e.Number} - {a.Name}", "Income", e.Id, $"INC:{e.Id}", AccountingService.Dr(Acc.ForMethod(d.Method), d.Amount), AccountingService.Cr(a.Code, d.Amount));
        return e;
    });
}
