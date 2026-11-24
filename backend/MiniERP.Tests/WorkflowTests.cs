namespace MiniERP.Tests;

public class WorkflowTests
{
    static LineDto L(int product, decimal qty, decimal price = 0, decimal tax = 10) => new() { ProductId = product, Quantity = qty, UnitPrice = price, TaxRate = tax };

    async Task<DocumentDto> ApprovedPo(World w, int product, decimal qty, decimal price, int? wh = null)
    {
        var po = await w.Pur.CreateOrder(new DocumentDto { PartyId = w.Supplier, WarehouseId = wh ?? w.Main, Lines = { L(product, qty, price) } });
        return await w.Pur.SetOrderStatus(po.Id, "approve");
    }
    async Task<DocumentDto> Grn(World w, DocumentDto po, decimal qty, decimal rejected = 0)
        => await w.Pur.CreateReceipt(new DocumentDto { RefId = po.Id, Lines = { new LineDto { RefLineId = po.Lines[0].Id, Quantity = qty, RejectedQuantity = rejected } } });

    [Fact]
    public async Task Requisition_to_purchase_order_workflow()
    {
        var w = await World.Create();
        var r = await w.Pur.CreateRequisition(new DocumentDto { Lines = { L(w.Pen, 50) } });
        await Assert.ThrowsAsync<BusinessException>(() => w.Pur.ConvertRequisition(r.Id, new ConvertRequest { SupplierId = w.Supplier, WarehouseId = w.Main })); // not approved yet
        await w.Pur.SetRequisitionStatus(r.Id, "submit");
        var approved = await w.Pur.SetRequisitionStatus(r.Id, "approve");
        Assert.Equal(St.Approved, approved.Status);
        var po = await w.Pur.ConvertRequisition(r.Id, new ConvertRequest { SupplierId = w.Supplier, WarehouseId = w.Main });
        Assert.Equal(St.Draft, po.Status); Assert.Equal(50, po.Lines.Single().Quantity);
        Assert.Equal(St.Converted, (await w.Pur.Requisitions.AsNoTracking().FirstAsync(x => x.Id == r.Id)).Status);
    }

    [Fact]
    public async Task Goods_receipt_updates_inventory_ledger_partial_receipts_and_po_status()
    {
        var w = await World.Create();
        var po = await ApprovedPo(w, w.Pen, 100, 10);
        Assert.Equal(110, po.Total);   // 1000 + 10% tax

        var g1 = await Grn(w, po, 60, rejected: 10);
        Assert.Equal(0, await w.Qty(w.Pen, w.Main));           // draft receipt does not touch stock
        var approved = await w.Pur.ApproveReceipt(g1.Id);
        Assert.Equal(St.Approved, approved.Status); Assert.Equal(St.Passed, approved.QualityStatus);
        Assert.Equal(50, await w.Qty(w.Pen, w.Main));          // only accepted quantity
        Assert.Equal(10, (await w.Db.Products.FindAsync(w.Pen))!.AverageCost);
        Assert.Equal(St.PartiallyReceived, (await w.Pur.Orders.AsNoTracking().FirstAsync(x => x.Id == po.Id)).Status);
        Assert.Equal(500, await w.Balance(Acc.Inventory)); Assert.Equal(500, await w.Balance(Acc.GRNI, false));

        await Assert.ThrowsAsync<BusinessException>(() => w.Pur.ApproveReceipt(g1.Id));                // duplicate approval cannot post twice
        Assert.Equal(50, await w.Qty(w.Pen, w.Main));
        await Assert.ThrowsAsync<BusinessException>(() => Grn(w, po, 60));                              // exceeds remaining (50)

        var g2 = await Grn(w, po, 50); await w.Pur.ApproveReceipt(g2.Id);
        Assert.Equal(100, await w.Qty(w.Pen, w.Main));
        Assert.Equal(St.Received, (await w.Pur.Orders.AsNoTracking().FirstAsync(x => x.Id == po.Id)).Status);
    }

    [Fact]
    public async Task Rejected_receipt_does_not_change_stock()
    {
        var w = await World.Create(); var po = await ApprovedPo(w, w.Pen, 10, 5);
        var g = await Grn(w, po, 10); var r = await w.Pur.RejectReceipt(g.Id);
        Assert.Equal(St.Failed, r.QualityStatus); Assert.Equal(0, await w.Qty(w.Pen, w.Main));
        await Assert.ThrowsAsync<BusinessException>(() => w.Pur.ApproveReceipt(g.Id));
    }

    [Fact]
    public async Task Purchase_invoice_creates_payable_and_supplier_payment_settles_it_idempotently()
    {
        var w = await World.Create(); var po = await ApprovedPo(w, w.Pen, 100, 10);
        var g = await Grn(w, po, 60, 10); await w.Pur.ApproveReceipt(g.Id);
        var inv = await w.Pur.CreateInvoice(new DocumentDto { RefId = g.Id, Reference = "SUP-77" });
        Assert.Equal(500, inv.Subtotal); Assert.Equal(50, inv.TaxAmount); Assert.Equal(550, inv.Total); Assert.Equal(St.Unpaid, inv.Status);
        Assert.Equal(550, await w.Balance(Acc.AP, false)); Assert.Equal(0, await w.Balance(Acc.GRNI, false)); Assert.Equal(50, await w.Balance(Acc.InputTax));
        await Assert.ThrowsAsync<BusinessException>(() => w.Pur.CreateInvoice(new DocumentDto { RefId = g.Id }));   // already invoiced

        var pay = await w.Fin.RecordPayment(new PaymentRequest { Type = "SupplierPayment", InvoiceId = inv.Id, Amount = 200, Method = "Bank", IdempotencyKey = "k1" });
        var again = await w.Fin.RecordPayment(new PaymentRequest { Type = "SupplierPayment", InvoiceId = inv.Id, Amount = 200, Method = "Bank", IdempotencyKey = "k1" });
        Assert.Equal(pay.Id, again.Id); Assert.Equal(1, await w.Db.Payments.CountAsync());
        var after = await w.Pur.Invoices.AsNoTracking().FirstAsync(x => x.Id == inv.Id);
        Assert.Equal(200, after.PaidAmount); Assert.Equal(St.PartiallyPaid, after.Status);
        Assert.Equal(350, await w.Balance(Acc.AP, false)); Assert.Equal(-200, await w.Balance(Acc.Bank));
        await Assert.ThrowsAsync<BusinessException>(() => w.Fin.RecordPayment(new PaymentRequest { Type = "SupplierPayment", InvoiceId = inv.Id, Amount = 351, Method = "Bank" }));
        await w.Fin.RecordPayment(new PaymentRequest { Type = "SupplierPayment", InvoiceId = inv.Id, Amount = 350, Method = "Cash" });
        Assert.Equal(St.Paid, (await w.Pur.Invoices.AsNoTracking().FirstAsync(x => x.Id == inv.Id)).Status);
        Assert.Equal(0, await w.Balance(Acc.AP, false));
        var payables = await w.Rep.Payables(new PageQuery()); Assert.Equal(0, payables.Total);
    }

    [Fact]
    public async Task Purchase_return_reduces_stock_and_payable()
    {
        var w = await World.Create(); var po = await ApprovedPo(w, w.Pen, 100, 10);
        var g = await Grn(w, po, 100); await w.Pur.ApproveReceipt(g.Id);
        var inv = await w.Pur.CreateInvoice(new DocumentDto { RefId = g.Id });
        var ret = await w.Pur.CreateReturn(new DocumentDto { RefId = inv.Id, WarehouseId = w.Main, Reference = "Faulty", Lines = { new LineDto { ProductId = w.Pen, Quantity = 10 } } });
        Assert.Equal(110, ret.Total); Assert.Equal(90, await w.Qty(w.Pen, w.Main));
        var after = await w.Pur.Invoices.AsNoTracking().FirstAsync(x => x.Id == inv.Id);
        Assert.Equal(110, after.CreditedAmount); Assert.Equal(1100 - 110, await w.Balance(Acc.AP, false));
        await Assert.ThrowsAsync<BusinessException>(() => w.Pur.CreateReturn(new DocumentDto { RefId = inv.Id, WarehouseId = w.Main, Lines = { new LineDto { ProductId = w.Pen, Quantity = 91 } } }));
    }

    [Fact]
    public async Task Sales_workflow_validates_stock_delivers_invoices_and_collects()
    {
        var w = await World.Create(); await w.Open(w.Pen, w.Main, 100, 10);
        var big = await w.Sal.CreateOrder(new DocumentDto { PartyId = w.Customer, WarehouseId = w.Main, Lines = { L(w.Pen, 500, 20) } });
        var ex = await Assert.ThrowsAsync<BusinessException>(() => w.Sal.SetOrderStatus(big.Id, "confirm"));   // stock validation
        Assert.Contains("Insufficient stock", ex.Message);

        var so = await w.Sal.CreateOrder(new DocumentDto { PartyId = w.Customer, WarehouseId = w.Main, Lines = { L(w.Pen, 30, 20) } });
        so = await w.Sal.SetOrderStatus(so.Id, "confirm"); Assert.Equal(St.Confirmed, so.Status);
        await Assert.ThrowsAsync<BusinessException>(() => w.Sal.CreateDelivery(new DocumentDto { RefId = so.Id, Lines = { new LineDto { RefLineId = so.Lines[0].Id, Quantity = 31 } } }));

        var d1 = await w.Sal.CreateDelivery(new DocumentDto { RefId = so.Id, Lines = { new LineDto { RefLineId = so.Lines[0].Id, Quantity = 10 } } });
        Assert.Equal(100, await w.Qty(w.Pen, w.Main));                // draft delivery: no stock effect
        await w.Sal.PostDelivery(d1.Id);
        Assert.Equal(90, await w.Qty(w.Pen, w.Main));
        Assert.Equal(St.PartiallyDelivered, (await w.Sal.Orders.AsNoTracking().FirstAsync(x => x.Id == so.Id)).Status);
        Assert.Equal(100, await w.Balance(Acc.COGS));
        await Assert.ThrowsAsync<BusinessException>(() => w.Sal.PostDelivery(d1.Id));   // double post blocked
        Assert.Equal(90, await w.Qty(w.Pen, w.Main));

        var inv1 = await w.Sal.CreateInvoice(new DocumentDto { RefId = d1.Id });
        Assert.Equal(200, inv1.Subtotal); Assert.Equal(220, inv1.Total);
        await Assert.ThrowsAsync<BusinessException>(() => w.Sal.CreateInvoice(new DocumentDto { RefId = d1.Id }));
        Assert.Equal(220, await w.Balance(Acc.AR)); Assert.Equal(200, await w.Balance(Acc.Sales, false)); Assert.Equal(20, await w.Balance(Acc.OutputTax, false));

        var d2 = await w.Sal.CreateDelivery(new DocumentDto { RefId = so.Id, Lines = { new LineDto { RefLineId = so.Lines[0].Id, Quantity = 20 } } });
        await w.Sal.PostDelivery(d2.Id);
        Assert.Equal(St.Delivered, (await w.Sal.Orders.AsNoTracking().FirstAsync(x => x.Id == so.Id)).Status);
        var inv2 = await w.Sal.CreateInvoice(new DocumentDto { RefId = d2.Id });

        await w.Fin.RecordPayment(new PaymentRequest { Type = "CustomerReceipt", InvoiceId = inv1.Id, Amount = 220, Method = "Bank" });
        Assert.Equal(St.Paid, (await w.Sal.Invoices.AsNoTracking().FirstAsync(x => x.Id == inv1.Id)).Status);
        Assert.Equal(440, await w.Balance(Acc.AR));
        var ret = await w.Sal.CreateReturn(new DocumentDto { RefId = inv2.Id, WarehouseId = w.Main, Lines = { new LineDto { ProductId = w.Pen, Quantity = 5 } } });
        Assert.Equal(110, ret.Total); Assert.Equal(75, await w.Qty(w.Pen, w.Main));
        Assert.Equal(440 - 110, await w.Balance(Acc.AR)); Assert.Equal(250, await w.Balance(Acc.COGS));
        var recv = await w.Rep.Receivables(new PageQuery()); Assert.Equal(1, recv.Total);
    }

    [Fact]
    public async Task Quotation_converts_to_sales_order()
    {
        var w = await World.Create(); await w.Open(w.Pen, w.Main, 10, 5);
        var q = await w.Sal.CreateQuotation(new DocumentDto { PartyId = w.Customer, Lines = { L(w.Pen, 5, 20) } });
        await Assert.ThrowsAsync<BusinessException>(() => w.Sal.ConvertQuotation(q.Id, new ConvertRequest { WarehouseId = w.Main }));
        await w.Sal.SetQuotationStatus(q.Id, "send"); await w.Sal.SetQuotationStatus(q.Id, "accept");
        var so = await w.Sal.ConvertQuotation(q.Id, new ConvertRequest { WarehouseId = w.Main });
        Assert.Equal(q.Total, so.Total); Assert.Equal(q.Id, so.RefId);
    }

    [Fact]
    public async Task Stock_transfer_moves_stock_between_warehouses_without_changing_value()
    {
        var w = await World.Create(); await w.Open(w.Pen, w.Main, 100, 10);
        var t = await w.Inv.CreateTransfer(new DocumentDto { WarehouseId = w.Main, ToWarehouseId = w.Other, Lines = { new LineDto { ProductId = w.Pen, Quantity = 40 } } });
        await w.Inv.CompleteTransfer(t.Id);
        Assert.Equal(60, await w.Qty(w.Pen, w.Main)); Assert.Equal(40, await w.Qty(w.Pen, w.Other)); Assert.Equal(100, await w.Inv.TotalOnHand(w.Pen));
        Assert.Equal(1000, await w.Balance(Acc.Inventory));
        await Assert.ThrowsAsync<BusinessException>(() => w.Inv.CompleteTransfer(t.Id));
        var t2 = await w.Inv.CreateTransfer(new DocumentDto { WarehouseId = w.Other, ToWarehouseId = w.Main, Lines = { new LineDto { ProductId = w.Pen, Quantity = 41 } } });
        await Assert.ThrowsAsync<BusinessException>(() => w.Inv.CompleteTransfer(t2.Id));   // insufficient stock
    }

    [Fact]
    public async Task Manual_stock_adjustments_are_logged_and_cannot_go_negative()
    {
        var w = await World.Create(); await w.Open(w.Pen, w.Main, 10, 4);
        await w.Inv.Movement(new MovementRequest { ProductId = w.Pen, WarehouseId = w.Main, Type = TxType.AdjustmentOut, Quantity = 3, Notes = "damaged" });
        Assert.Equal(7, await w.Qty(w.Pen, w.Main));
        await Assert.ThrowsAsync<BusinessException>(() => w.Inv.Movement(new MovementRequest { ProductId = w.Pen, WarehouseId = w.Main, Type = TxType.StockOut, Quantity = 8 }));
        Assert.Equal(28, await w.Balance(Acc.Inventory)); Assert.Equal(12, await w.Balance(Acc.InvAdj));
    }

    async Task<(BomDto Bom, World W)> NotebookWorld()
    {
        var w = await World.Create();
        await w.Open(w.Paper, w.Rm, 5000, 0.5m); await w.Open(w.Cover, w.Rm, 500, 8); await w.Open(w.Binding, w.Rm, 500, 2);
        var bom = await w.Man.SaveBom(null, new BomDto { Code = "B1", Name = "Notebook", ProductId = w.Notebook, OutputQuantity = 1, Items = { new BomItemDto { ComponentProductId = w.Paper, Quantity = 10 }, new BomItemDto { ComponentProductId = w.Cover, Quantity = 1 }, new BomItemDto { ComponentProductId = w.Binding, Quantity = 1 } } });
        return (bom, w);
    }

    [Fact]
    public async Task Mrp_calculates_raw_material_requirements_for_100_notebooks()
    {
        var (_, w) = await NotebookWorld();
        dynamic plan = await w.Man.Plan(new PlanRequest { ProductId = w.Notebook, Quantity = 100, WarehouseId = w.Rm });
        var comps = ((IEnumerable<ComponentDto>)plan.components).ToDictionary(c => c.ProductId);
        Assert.Equal(1000, comps[w.Paper].Required); Assert.Equal(100, comps[w.Cover].Required); Assert.Equal(100, comps[w.Binding].Required);
        Assert.True((bool)plan.canProduce); Assert.Equal(1500m, (decimal)plan.estimatedMaterialCost);
        dynamic big = await w.Man.Plan(new PlanRequest { ProductId = w.Notebook, Quantity = 1000, WarehouseId = w.Rm });
        Assert.False((bool)big.canProduce);
        var pr = await w.Man.RequisitionForShortages(new PlanRequest { ProductId = w.Notebook, Quantity = 1000, WarehouseId = w.Rm });
        Assert.Contains(pr.Lines, l => l.ProductId == w.Paper && l.Quantity == 5000);   // 10000 needed - 5000 on hand
    }

    [Fact]
    public async Task Production_workflow_consumes_materials_and_receives_finished_goods_with_costing()
    {
        var (bom, w) = await NotebookWorld();
        var big = await w.Man.CreateOrder(new CreateProductionRequest { ProductId = w.Notebook, Quantity = 100000, SourceWarehouseId = w.Rm, TargetWarehouseId = w.Main });
        var ex = await Assert.ThrowsAsync<BusinessException>(() => w.Man.Release(big.Id)); Assert.Contains("Material shortage", ex.Message);

        var o = await w.Man.CreateOrder(new CreateProductionRequest { ProductId = w.Notebook, BomId = bom.Id, Quantity = 100, SourceWarehouseId = w.Rm, TargetWarehouseId = w.Main });
        Assert.Equal(St.Planned, o.Status);
        await Assert.ThrowsAsync<BusinessException>(() => w.Man.IssueMaterials(o.Id, null));    // not released
        await w.Man.Release(o.Id);
        var issued = await w.Man.IssueMaterials(o.Id, null);
        Assert.Equal(St.InProgress, issued.Status); Assert.Equal(1500, issued.MaterialCost);
        Assert.Equal(4000, await w.Qty(w.Paper, w.Rm)); Assert.Equal(400, await w.Qty(w.Cover, w.Rm)); Assert.Equal(400, await w.Qty(w.Binding, w.Rm));
        Assert.Equal(1500, await w.Balance(Acc.WIP));
        await Assert.ThrowsAsync<BusinessException>(() => w.Man.IssueMaterials(o.Id, null));    // nothing left to issue

        var done = await w.Man.Complete(o.Id, new CompleteProductionRequest { QuantityProduced = 100, LaborCost = 300, OverheadCost = 200, QualityStatus = "Passed" });
        Assert.Equal(St.Completed, done.Status); Assert.Equal(2000, done.TotalCost); Assert.Equal(20, done.UnitCost);
        Assert.Equal(100, await w.Qty(w.Notebook, w.Main)); Assert.Equal(20, (await w.Db.Products.FindAsync(w.Notebook))!.AverageCost);
        Assert.Equal(0, await w.Balance(Acc.WIP)); Assert.Equal(500, await w.Balance(Acc.Overhead, false));
        Assert.Equal(7500 - 1500 + 2000, await w.Balance(Acc.Inventory));
        await Assert.ThrowsAsync<BusinessException>(() => w.Man.Complete(o.Id, new CompleteProductionRequest { QuantityProduced = 100 }));
        Assert.Equal(1, await w.Db.ProductionReceipts.CountAsync());
        Assert.Equal(1, (await w.Rep.Production(new PageQuery())).Total);
    }

    [Fact]
    public async Task Failed_quality_check_scraps_wip_and_adds_no_finished_goods()
    {
        var (bom, w) = await NotebookWorld();
        var o = await w.Man.CreateOrder(new CreateProductionRequest { ProductId = w.Notebook, BomId = bom.Id, Quantity = 10, SourceWarehouseId = w.Rm, TargetWarehouseId = w.Main });
        await w.Man.Release(o.Id); await w.Man.IssueMaterials(o.Id, null);
        var r = await w.Man.Complete(o.Id, new CompleteProductionRequest { QualityStatus = "Failed" });
        Assert.Equal(St.Rejected, r.Status); Assert.Equal(0, await w.Qty(w.Notebook, w.Main)); Assert.Equal(0, await w.Balance(Acc.WIP));
    }

    [Fact]
    public async Task Journals_must_balance_and_duplicates_are_rejected()
    {
        var w = await World.Create(); int a = w.Acc(Acc.Cash), b = w.Acc(Acc.Equity);
        await Assert.ThrowsAsync<BusinessException>(() => w.Acct.CreateManual(new JournalDto { Post = true, Description = "bad", Lines = { new JournalLineDto { AccountId = a, Debit = 100 }, new JournalLineDto { AccountId = b, Credit = 90 } } }));
        var j = await w.Acct.CreateManual(new JournalDto { Post = true, Description = "ok", Lines = { new JournalLineDto { AccountId = a, Debit = 100 }, new JournalLineDto { AccountId = b, Credit = 100 } } });
        Assert.Equal(St.Posted, j.Status); Assert.Equal(100, await w.Balance(Acc.Cash));
        var rev = await w.Acct.Reverse(j.Id); Assert.Equal(0, await w.Balance(Acc.Cash));
        await Assert.ThrowsAsync<BusinessException>(() => w.Acct.Reverse(j.Id));
        await w.Acct.Post(DateTime.UtcNow, "x", "T", 1, "KEY:1", AccountingService.Dr(Acc.Cash, 5), AccountingService.Cr(Acc.Equity, 5));
        await Assert.ThrowsAsync<BusinessException>(() => w.Acct.Post(DateTime.UtcNow, "x", "T", 1, "KEY:1", AccountingService.Dr(Acc.Cash, 5), AccountingService.Cr(Acc.Equity, 5)));
        await Assert.ThrowsAsync<BusinessException>(() => w.Acct.Post(DateTime.UtcNow, "x", "T", 2, "KEY:2", AccountingService.Dr(Acc.Cash, 5), AccountingService.Cr(Acc.Equity, 4)));
    }

    [Fact]
    public async Task Expenses_income_profit_and_loss_and_balance_sheet_reflect_transactions()
    {
        var w = await World.Create(); await w.Open(w.Pen, w.Main, 100, 10);
        await w.Acct.CreateManual(new JournalDto { Post = true, Description = "capital", Lines = { new JournalLineDto { AccountId = w.Acc(Acc.Bank), Debit = 10000 }, new JournalLineDto { AccountId = w.Acc(Acc.Equity), Credit = 10000 } } });
        var so = await w.Sal.CreateOrder(new DocumentDto { PartyId = w.Customer, WarehouseId = w.Main, Lines = { L(w.Pen, 10, 20) } }); await w.Sal.SetOrderStatus(so.Id, "confirm");
        var dn = await w.Sal.CreateDelivery(new DocumentDto { RefId = so.Id, Lines = { new LineDto { RefLineId = so.Lines[0].Id, Quantity = 10 } } }); await w.Sal.PostDelivery(dn.Id);
        await w.Sal.CreateInvoice(new DocumentDto { RefId = dn.Id });
        await w.Fin.CreateExpense(new CashEntryDto { AccountId = w.Acc("5200"), Amount = 30, Method = "Cash", Party = "Landlord" });
        await w.Fin.CreateIncome(new CashEntryDto { AccountId = w.Acc("4200"), Amount = 15, Method = "Bank" });
        await Assert.ThrowsAsync<BusinessException>(() => w.Fin.CreateExpense(new CashEntryDto { AccountId = w.Acc(Acc.Cash), Amount = 5 }));   // not an expense account

        var pl = await w.Acct.ProfitLoss(null, null);
        Assert.Equal(215, pl.TotalRevenue); Assert.Equal(100, pl.TotalCostOfSales); Assert.Equal(115, pl.GrossProfit); Assert.Equal(30, pl.TotalOperatingExpenses); Assert.Equal(85, pl.NetProfit);
        dynamic bs = await w.Acct.BalanceSheet(null); Assert.True((bool)bs.balanced);
        dynamic tb = await w.Acct.TrialBalance(null); Assert.True((bool)tb.balanced);
        Assert.Equal(1, (await w.Rep.Expenses(new PageQuery())).Total);
        Assert.Equal(2, (await w.Rep.Revenue(new PageQuery())).Total);
    }

    [Fact]
    public async Task Ledger_integrity_stock_balances_equal_transaction_history_and_all_journals_balance()
    {
        var w = await World.Create();
        var po = await ApprovedPo(w, w.Pen, 100, 10); var g = await Grn(w, po, 100); await w.Pur.ApproveReceipt(g.Id); await w.Pur.CreateInvoice(new DocumentDto { RefId = g.Id });
        var t = await w.Inv.CreateTransfer(new DocumentDto { WarehouseId = w.Main, ToWarehouseId = w.Other, Lines = { new LineDto { ProductId = w.Pen, Quantity = 25 } } }); await w.Inv.CompleteTransfer(t.Id);
        await w.Inv.Movement(new MovementRequest { ProductId = w.Pen, WarehouseId = w.Other, Type = TxType.StockOut, Quantity = 5 });
        foreach (var b in await w.Db.StockBalances.ToListAsync())
        {
            var sum = await w.Db.StockTransactions.Where(x => x.ProductId == b.ProductId && x.WarehouseId == b.WarehouseId).SumAsync(x => x.Quantity);
            Assert.Equal(sum, b.Quantity);
        }
        foreach (var j in await w.Db.JournalEntries.Include(x => x.Lines).ToListAsync()) { Assert.Equal(j.Lines.Sum(l => l.Debit), j.Lines.Sum(l => l.Credit)); Assert.Equal(j.TotalDebit, j.TotalCredit); }
        var lines = await w.Db.JournalEntryLines.ToListAsync(); Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
        Assert.True((await w.Db.AuditLogs.CountAsync()) > 0);
    }

    [Fact]
    public async Task Dashboard_and_reports_return_database_values()
    {
        var w = await World.Create(); await w.Open(w.Pen, w.Main, 3, 10);
        await w.Db.Products.Where(p => p.Id == w.Pen).ForEachAsync(p => p.ReorderLevel = 5); await w.Db.SaveChangesAsync();
        var dash = new DashboardService(w.Db, w.Rep, w.Acct);
        dynamic d = await dash.Get();
        Assert.Equal(5, (int)d.totals.products); Assert.Equal(30m, (decimal)d.totals.inventoryValue);
        Assert.Single((IEnumerable<object>)d.lowStock);
        var val = await w.Rep.StockValuation(new PageQuery { Search = "PEN" }); Assert.Equal(1, val.Total); Assert.Equal(30m, val.Summary!["value"]);
        var inv = await w.Rep.Inventory(new PageQuery { SortBy = "quantity", SortDir = "asc", PageSize = 1 }); Assert.Single(inv.Items);
    }
}
