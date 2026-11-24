import type { ActionDef, ColumnDef, FieldDef, Lookup, ResourceConfig } from '../types';
import { money, num, fmtDate } from '../api';
import { StatusBadge } from '../ui';

// ---------- lookups ----------
const sup: Lookup = { endpoint: '/suppliers', label: (r) => `${r.code} · ${r.name}` };
const cus: Lookup = { endpoint: '/customers', label: (r) => `${r.code} · ${r.name}` };
const wh: Lookup = { endpoint: '/warehouses', label: (r) => r.name };
const cat: Lookup = { endpoint: '/categories', label: (r) => r.name };
const unit: Lookup = { endpoint: '/units', label: (r) => `${r.name} (${r.symbol})` };
const prod: Lookup = { endpoint: '/products', label: (r) => `${r.sku} · ${r.name}` };
const acct = (type: string): Lookup => ({ endpoint: '/accounts', label: (r) => `${r.code} · ${r.name}`, filter: (r) => r.accountType === type });
const role: Lookup = { endpoint: '/roles', label: (r) => r.name };
const typeOpts = [{ value: 'TradingProduct', label: 'Retail Product' }];

// ---------- helpers ----------
const f = (name: string, label: string, extra: Partial<FieldDef> = {}): FieldDef => ({ name, label, ...extra });
const date = (label = 'Date'): FieldDef => ({ name: 'date', label, type: 'date', required: true });
const col = (key: string, label: string, extra: Partial<ColumnDef> = {}): ColumnDef => ({ key, label, ...extra });
const docCols = (party: string, extra: ColumnDef[] = []): ColumnDef[] => [col('number', 'Number', { sortable: true }), col('date', 'Date', { type: 'date', sortable: true }), col('partyName', party), ...extra, col('status', 'Status', { type: 'status', sortable: true })];
const pricedLines = (priceFrom: 'averageCost' | 'salePrice') => ({ priceFrom, cols: [{ name: 'productId', label: 'Product', type: 'product' as const }, { name: 'quantity', label: 'Quantity', type: 'number' as const }, { name: 'unitPrice', label: 'Unit price', type: 'number' as const }, { name: 'taxRate', label: 'Tax %', type: 'number' as const }] });
const qtyLines = { cols: [{ name: 'productId', label: 'Product', type: 'product' as const }, { name: 'quantity', label: 'Quantity', type: 'number' as const }] };
const act = (label: string, path: string, statuses: string[], extra: Partial<ActionDef> = {}): ActionDef => ({ label, path: (r) => `${path.replace(':id', r.id)}`, show: (r) => statuses.includes(r.status), ...extra });
const mapLine = (l: any, extra: any = {}) => ({ refLineId: l.id, productId: l.productId, productName: l.productName, sku: l.sku, ...extra });
const yes = () => true;

// ---------- Inventory ----------
export const products: ResourceConfig = {
  title: 'Products', subtitle: 'Finished goods purchased from suppliers and sold to retail customers', endpoint: '/products', view: 'inventory.view', manage: 'inventory.manage', createLabel: 'New product', csvName: 'products',
  typeFilter: { param: 'type', label: 'All types', options: typeOpts }, canEdit: yes, canDelete: true, defaultSort: { by: 'sku', dir: 'asc' },
  columns: [col('sku', 'SKU', { sortable: true }), col('name', 'Name', { sortable: true }), col('categoryName', 'Category'), col('unitName', 'Unit'),
    col('onHand', 'On hand', { type: 'number' }), col('averageCost', 'Avg cost', { type: 'money' }), col('salePrice', 'Sale price', { type: 'money', sortable: true }), col('reorderLevel', 'Reorder lvl', { type: 'number' }), col('isActive', 'Active', { type: 'bool' })],
  fields: [f('sku', 'SKU', { required: true }), f('name', 'Name', { required: true }), f('categoryId', 'Category', { lookup: cat, required: true }), f('unitId', 'Unit of measure', { lookup: unit, required: true }),
    f('productType', 'Product type', { type: 'select', options: typeOpts, default: 'TradingProduct', required: true }), f('salePrice', 'Sale price', { type: 'number', default: 0 }), f('taxRate', 'Tax rate %', { type: 'number', default: 18 }),
    f('reorderLevel', 'Reorder level', { type: 'number', default: 0, help: 'Low-stock alert fires at or below this quantity.' }), f('description', 'Description', { type: 'textarea' }), f('isActive', 'Active', { type: 'checkbox', default: true })],
};
export const categories: ResourceConfig = { title: 'Categories', endpoint: '/categories', view: 'inventory.view', manage: 'inventory.manage', canEdit: yes, canDelete: true, columns: [col('name', 'Name', { sortable: true }), col('description', 'Description')], fields: [f('name', 'Name', { required: true }), f('description', 'Description', { type: 'textarea' })] };
export const units: ResourceConfig = { title: 'Units', subtitle: 'Units of measurement', endpoint: '/units', view: 'inventory.view', manage: 'inventory.manage', canEdit: yes, canDelete: true, columns: [col('name', 'Name', { sortable: true }), col('symbol', 'Symbol')], fields: [f('name', 'Name', { required: true }), f('symbol', 'Symbol', { required: true })] };
export const warehouses: ResourceConfig = {
  title: 'Warehouses', endpoint: '/warehouses', view: 'inventory.view', manage: 'inventory.manage', canEdit: yes, canDelete: true,
  columns: [col('code', 'Code', { sortable: true }), col('name', 'Name', { sortable: true }), col('location', 'Location'), col('isActive', 'Active', { type: 'bool' })],
  fields: [f('code', 'Code', { required: true }), f('name', 'Name', { required: true }), f('location', 'Location'), f('isActive', 'Active', { type: 'checkbox', default: true })],
};
export const stockBalances: ResourceConfig = {
  title: 'Stock Balances', subtitle: 'On-hand quantity by warehouse. Changes are only made through movements.', endpoint: '/stock/balances', createEndpoint: '/stock/movement', view: 'inventory.view', manage: 'inventory.manage', createLabel: 'New movement', rowClick: false, csvName: 'stock-balances',
  columns: [col('sku', 'SKU'), col('product', 'Product'), col('warehouse', 'Warehouse'), col('quantity', 'Quantity', { type: 'number' }), col('avgCost', 'Avg cost', { type: 'money' }), col('value', 'Value', { type: 'money' }), col('reorderLevel', 'Reorder lvl', { type: 'number' }), col('status', 'Status', { type: 'status' })],
  fields: [f('type', 'Movement type', { type: 'select', required: true, default: 'StockIn', options: [{ value: 'OpeningStock', label: 'Opening stock' }, { value: 'StockIn', label: 'Stock in' }, { value: 'StockOut', label: 'Stock out' }, { value: 'AdjustmentIn', label: 'Adjustment (increase)' }, { value: 'AdjustmentOut', label: 'Adjustment (decrease)' }] }),
    f('productId', 'Product', { lookup: prod, required: true }), f('warehouseId', 'Warehouse', { lookup: wh, required: true }), f('quantity', 'Quantity', { type: 'number', required: true }),
    f('unitCost', 'Unit cost', { type: 'number', help: 'Required for opening stock; optional for stock in (defaults to average cost).' }), f('notes', 'Reason / notes', { type: 'textarea' })],
  mapSave: (x) => ({ ...x, unitCost: x.unitCost === '' ? null : x.unitCost }),
};
export const stockHistory: ResourceConfig = {
  title: 'Stock Movement History', endpoint: '/stock/transactions', view: 'inventory.view', rowClick: false, dates: true, csvName: 'stock-movements',
  columns: [col('date', 'Date', { type: 'date' }), col('sku', 'SKU'), col('product', 'Product'), col('warehouse', 'Warehouse'), col('type', 'Type'), col('quantity', 'Qty', { type: 'number' }), col('unitCost', 'Unit cost', { type: 'money' }), col('reference', 'Reference'), col('user', 'User')],
};
export const transfers: ResourceConfig = {
  title: 'Stock Transfers', subtitle: 'Move stock between warehouses', endpoint: '/stock-transfers', view: 'inventory.view', manage: 'inventory.manage', createLabel: 'New transfer', statuses: ['Draft', 'Completed'], dates: true,
  columns: [col('number', 'Number', { sortable: true }), col('date', 'Date', { type: 'date', sortable: true }), col('warehouseName', 'From'), col('toWarehouseName', 'To'), col('status', 'Status', { type: 'status' })],
  fields: [date(), f('warehouseId', 'From warehouse', { lookup: wh, required: true }), f('toWarehouseId', 'To warehouse', { lookup: wh, required: true }), f('notes', 'Notes', { type: 'textarea' })], lines: qtyLines,
  actions: [act('Complete transfer', '/stock-transfers/:id/complete', ['Draft'], { confirm: 'Move the stock now?' })],
};

// ---------- Procurement ----------
const party = (label: string) => [f('code', 'Code', { required: true }), f('name', label, { required: true }), f('contactPerson', 'Contact person'), f('email', 'Email'), f('phone', 'Phone'), f('taxNumber', 'Tax number'), f('paymentTermsDays', 'Payment terms (days)', { type: 'number', default: 30 })];
export const suppliers: ResourceConfig = {
  title: 'Suppliers', endpoint: '/suppliers', view: 'procurement.view', manage: 'procurement.manage', canEdit: yes, canDelete: true, createLabel: 'New supplier',
  columns: [col('code', 'Code', { sortable: true }), col('name', 'Name', { sortable: true }), col('contactPerson', 'Contact'), col('email', 'Email'), col('phone', 'Phone'), col('paymentTermsDays', 'Terms (days)', { type: 'number' }), col('isActive', 'Active', { type: 'bool' })],
  fields: [...party('Supplier name'), f('address', 'Address', { type: 'textarea' }), f('isActive', 'Active', { type: 'checkbox', default: true })],
};
export const requisitions: ResourceConfig = {
  title: 'Purchase Requisitions', subtitle: 'Internal requests for materials, approved before ordering', endpoint: '/purchase-requisitions', view: 'procurement.view', manage: 'procurement.manage', createLabel: 'New requisition',
  statuses: ['Draft', 'Submitted', 'Approved', 'Rejected', 'Converted'], dates: true, columns: docCols('Requested by'), fields: [date(), f('notes', 'Notes / justification', { type: 'textarea' })], lines: qtyLines,
  canEdit: (r) => ['Draft', 'Rejected'].includes(r.status),
  actions: [act('Submit for approval', '/purchase-requisitions/:id/submit', ['Draft']), act('Approve', '/purchase-requisitions/:id/approve', ['Submitted'], { perm: 'procurement.approve' }),
    act('Reject', '/purchase-requisitions/:id/reject', ['Submitted'], { perm: 'procurement.approve', tone: 'danger', confirm: 'Reject this requisition?' }),
    act('Convert to PO', '/purchase-requisitions/:id/convert', ['Approved'], { promptTitle: 'Create purchase order', prompt: [f('supplierId', 'Supplier', { lookup: sup, required: true }), f('warehouseId', 'Receiving warehouse', { lookup: wh, required: true })] })],
};
export const purchaseOrders: ResourceConfig = {
  title: 'Purchase Orders', endpoint: '/purchase-orders', view: 'procurement.view', manage: 'procurement.manage', createLabel: 'New purchase order', statuses: ['Draft', 'Approved', 'PartiallyReceived', 'Received', 'Closed', 'Cancelled'], dates: true,
  columns: docCols('Supplier', [col('warehouseName', 'Deliver to'), col('total', 'Total', { type: 'money' })]),
  fields: [f('partyId', 'Supplier', { lookup: sup, required: true }), f('warehouseId', 'Receiving warehouse', { lookup: wh, required: true }), date('Order date'), f('dueDate', 'Expected date', { type: 'date' }), f('notes', 'Notes', { type: 'textarea' })],
  lines: { ...pricedLines('averageCost'), doneLabel: 'Received' }, canEdit: (r) => r.status === 'Draft',
  actions: [act('Approve', '/purchase-orders/:id/approve', ['Draft'], { perm: 'procurement.approve' }), act('Cancel', '/purchase-orders/:id/cancel', ['Draft', 'Approved'], { tone: 'danger', confirm: 'Cancel this order?' }), act('Close', '/purchase-orders/:id/close', ['Approved', 'PartiallyReceived', 'Received'], { tone: 'default', confirm: 'Close this order?' })],
};
export const goodsReceipts: ResourceConfig = {
  title: 'Goods Receipts', subtitle: 'Receive goods against purchase orders. Approval (quality passed) updates inventory.', endpoint: '/goods-receipts', view: 'procurement.view', manage: 'procurement.manage', createLabel: 'New goods receipt', statuses: ['Draft', 'Approved', 'Rejected'], dates: true,
  columns: docCols('Supplier', [col('refNumber', 'Purchase order'), col('warehouseName', 'Warehouse'), col('qualityStatus', 'Quality', { type: 'status' })]),
  fields: [f('refId', 'Purchase order', { lookup: { endpoint: '/purchase-orders', label: (r) => `${r.number} · ${r.partyName}`, filter: (r) => ['Approved', 'PartiallyReceived'].includes(r.status) }, required: true }), date('Receipt date'), f('notes', 'Notes', { type: 'textarea' })],
  prefill: { field: 'refId', endpoint: '/purchase-orders', apply: (po) => ({ warehouseId: po.warehouseId, lines: po.lines.filter((l: any) => l.quantity - l.doneQuantity > 0).map((l: any) => mapLine(l, { quantity: l.quantity - l.doneQuantity, rejectedQuantity: 0 })) }) },
  lines: { fixed: true, dropZero: true, cols: [{ name: 'productId', label: 'Product', type: 'product' }, { name: 'quantity', label: 'Received qty', type: 'number' }, { name: 'rejectedQuantity', label: 'Rejected qty (QC)', type: 'number' }] },
  actions: [act('Approve (QC passed) & post to stock', '/goods-receipts/:id/approve', ['Draft'], { confirm: 'Approve this receipt and add accepted quantities to stock?' }), act('Reject (QC failed)', '/goods-receipts/:id/reject', ['Draft'], { tone: 'danger', confirm: 'Reject this receipt? No stock will be added.' })],
};
export const purchaseInvoices: ResourceConfig = {
  title: 'Purchase Invoices', subtitle: 'Invoice approved goods receipts → creates accounts payable', endpoint: '/purchase-invoices', view: 'procurement.view', manage: 'procurement.manage', createLabel: 'New invoice', statuses: ['Unpaid', 'PartiallyPaid', 'Paid'], dates: true,
  columns: docCols('Supplier', [col('reference', 'Supplier inv #'), col('refNumber', 'GRN'), col('dueDate', 'Due', { type: 'date' }), col('total', 'Total', { type: 'money' }), col('paidAmount', 'Paid', { type: 'money' }), col('balance', 'Balance', { type: 'money' })]),
  fields: [f('refId', 'Goods receipt', { lookup: { endpoint: '/goods-receipts', label: (r) => `${r.number} · ${r.partyName}`, filter: (r) => r.status === 'Approved' && !r.reference }, required: true }), f('reference', 'Supplier invoice number'), date('Invoice date'), f('dueDate', 'Due date', { type: 'date', help: 'Defaults to supplier payment terms.' }), f('notes', 'Notes', { type: 'textarea' })],
  lines: { cols: [], fixed: true },
  mapSave: (x) => ({ ...x, lines: [], dueDate: x.dueDate || null }),
};
export const purchaseReturns: ResourceConfig = {
  title: 'Purchase Returns', subtitle: 'Return goods to the supplier; reduces stock and the payable', endpoint: '/purchase-returns', view: 'procurement.view', manage: 'procurement.manage', createLabel: 'New return', dates: true,
  columns: docCols('Supplier', [col('refNumber', 'Invoice'), col('warehouseName', 'From warehouse'), col('reference', 'Reason'), col('total', 'Total', { type: 'money' })]),
  fields: [f('refId', 'Purchase invoice', { lookup: { endpoint: '/purchase-invoices', label: (r) => `${r.number} · ${r.partyName}` }, required: true }), f('warehouseId', 'Return from warehouse', { lookup: wh, required: true }), date('Return date'), f('reference', 'Reason', { type: 'text' })],
  prefill: { field: 'refId', endpoint: '/purchase-invoices', apply: (inv) => ({ lines: inv.lines.map((l: any) => mapLine(l, { quantity: 0 })) }) },
  lines: { fixed: true, dropZero: true, cols: [{ name: 'productId', label: 'Product', type: 'product' }, { name: 'quantity', label: 'Return qty', type: 'number' }] },
};

// ---------- Sales ----------
export const customers: ResourceConfig = {
  title: 'Customers', endpoint: '/customers', view: 'sales.view', manage: 'sales.manage', canEdit: yes, canDelete: true, createLabel: 'New customer',
  columns: [col('code', 'Code', { sortable: true }), col('name', 'Name', { sortable: true }), col('contactPerson', 'Contact'), col('email', 'Email'), col('phone', 'Phone'), col('creditLimit', 'Credit limit', { type: 'money' }), col('isActive', 'Active', { type: 'bool' })],
  fields: [...party('Customer name'), f('creditLimit', 'Credit limit', { type: 'number', default: 0 }), f('address', 'Address', { type: 'textarea' }), f('isActive', 'Active', { type: 'checkbox', default: true })],
};
export const quotations: ResourceConfig = {
  title: 'Quotations', endpoint: '/sales-quotations', view: 'sales.view', manage: 'sales.manage', createLabel: 'New quotation', statuses: ['Draft', 'Sent', 'Accepted', 'Rejected', 'Converted'], dates: true,
  columns: docCols('Customer', [col('dueDate', 'Valid until', { type: 'date' }), col('total', 'Total', { type: 'money' })]),
  fields: [f('partyId', 'Customer', { lookup: cus, required: true }), date('Quotation date'), f('dueDate', 'Valid until', { type: 'date' }), f('notes', 'Notes', { type: 'textarea' })], lines: pricedLines('salePrice'), canEdit: (r) => r.status === 'Draft',
  actions: [act('Mark as sent', '/sales-quotations/:id/send', ['Draft']), act('Accept', '/sales-quotations/:id/accept', ['Draft', 'Sent']), act('Reject', '/sales-quotations/:id/reject', ['Draft', 'Sent'], { tone: 'danger' }),
    act('Convert to sales order', '/sales-quotations/:id/convert', ['Accepted'], { prompt: [f('warehouseId', 'Dispatch warehouse', { lookup: wh, required: true })] })],
};
export const salesOrders: ResourceConfig = {
  title: 'Sales Orders', subtitle: 'Confirming an order validates stock availability', endpoint: '/sales-orders', view: 'sales.view', manage: 'sales.manage', createLabel: 'New sales order', statuses: ['Draft', 'Confirmed', 'PartiallyDelivered', 'Delivered', 'Closed', 'Cancelled'], dates: true,
  columns: docCols('Customer', [col('warehouseName', 'Dispatch from'), col('total', 'Total', { type: 'money' })]),
  fields: [f('partyId', 'Customer', { lookup: cus, required: true }), f('warehouseId', 'Dispatch warehouse', { lookup: wh, required: true }), date('Order date'), f('dueDate', 'Delivery date', { type: 'date' }), f('notes', 'Notes', { type: 'textarea' })],
  lines: { ...pricedLines('salePrice'), doneLabel: 'Delivered' }, canEdit: (r) => r.status === 'Draft',
  actions: [act('Confirm (validate stock)', '/sales-orders/:id/confirm', ['Draft']), act('Cancel', '/sales-orders/:id/cancel', ['Draft', 'Confirmed'], { tone: 'danger', confirm: 'Cancel this order?' }), act('Close', '/sales-orders/:id/close', ['Confirmed', 'PartiallyDelivered', 'Delivered'], { tone: 'default', confirm: 'Close this order?' })],
};
export const deliveries: ResourceConfig = {
  title: 'Deliveries', subtitle: 'Posting a delivery deducts inventory and books cost of goods sold', endpoint: '/deliveries', view: 'sales.view', manage: 'sales.manage', createLabel: 'New delivery', statuses: ['Draft', 'Posted'], dates: true,
  columns: docCols('Customer', [col('refNumber', 'Sales order'), col('warehouseName', 'Warehouse')]),
  fields: [f('refId', 'Sales order', { lookup: { endpoint: '/sales-orders', label: (r) => `${r.number} · ${r.partyName}`, filter: (r) => ['Confirmed', 'PartiallyDelivered'].includes(r.status) }, required: true }), date('Delivery date'), f('notes', 'Notes', { type: 'textarea' })],
  prefill: { field: 'refId', endpoint: '/sales-orders', apply: (so) => ({ warehouseId: so.warehouseId, lines: so.lines.filter((l: any) => l.quantity - l.doneQuantity > 0).map((l: any) => mapLine(l, { quantity: l.quantity - l.doneQuantity })) }) },
  lines: { fixed: true, dropZero: true, cols: [{ name: 'productId', label: 'Product', type: 'product' }, { name: 'quantity', label: 'Deliver qty', type: 'number' }] },
  actions: [act('Post delivery (deduct stock)', '/deliveries/:id/post', ['Draft'], { confirm: 'Post this delivery and deduct stock?' })],
};
export const salesInvoices: ResourceConfig = {
  title: 'Sales Invoices', subtitle: 'Invoice posted deliveries → creates accounts receivable', endpoint: '/sales-invoices', view: 'sales.view', manage: 'sales.manage', createLabel: 'New invoice', statuses: ['Unpaid', 'PartiallyPaid', 'Paid'], dates: true,
  columns: docCols('Customer', [col('refNumber', 'Delivery'), col('dueDate', 'Due', { type: 'date' }), col('total', 'Total', { type: 'money' }), col('paidAmount', 'Received', { type: 'money' }), col('balance', 'Balance', { type: 'money' })]),
  fields: [f('refId', 'Delivery', { lookup: { endpoint: '/deliveries', label: (r) => `${r.number} · ${r.partyName}`, filter: (r) => r.status === 'Posted' && !r.reference }, required: true }), date('Invoice date'), f('dueDate', 'Due date', { type: 'date', help: 'Defaults to customer payment terms.' }), f('notes', 'Notes', { type: 'textarea' })],
  lines: { cols: [], fixed: true }, mapSave: (x) => ({ ...x, lines: [], dueDate: x.dueDate || null }),
};
export const salesReturns: ResourceConfig = {
  title: 'Sales Returns', subtitle: 'Customer returns restock goods and reduce the receivable', endpoint: '/sales-returns', view: 'sales.view', manage: 'sales.manage', createLabel: 'New return', dates: true,
  columns: docCols('Customer', [col('refNumber', 'Invoice'), col('warehouseName', 'Restock to'), col('reference', 'Reason'), col('total', 'Total', { type: 'money' })]),
  fields: [f('refId', 'Sales invoice', { lookup: { endpoint: '/sales-invoices', label: (r) => `${r.number} · ${r.partyName}` }, required: true }), f('warehouseId', 'Restock warehouse', { lookup: wh, required: true }), date('Return date'), f('reference', 'Reason')],
  prefill: { field: 'refId', endpoint: '/sales-invoices', apply: (inv) => ({ lines: inv.lines.map((l: any) => mapLine(l, { quantity: 0 })) }) },
  lines: { fixed: true, dropZero: true, cols: [{ name: 'productId', label: 'Product', type: 'product' }, { name: 'quantity', label: 'Return qty', type: 'number' }] },
};

// ---------- Manufacturing ----------
export const boms: ResourceConfig = {
  title: 'Bills of Materials', subtitle: 'Recipe of raw materials needed per unit of finished product', endpoint: '/boms', view: 'production.view', manage: 'production.manage', createLabel: 'New BOM', canEdit: yes, canDelete: true,
  columns: [col('code', 'Code', { sortable: true }), col('name', 'Name'), col('productName', 'Finished product'), col('outputQuantity', 'Output qty', { type: 'number' }), col('version', 'Version'), col('items', 'Components', { render: (r) => r.items.length }), col('isActive', 'Active', { type: 'bool' })],
  fields: [f('code', 'BOM code', { required: true }), f('name', 'Name', { required: true }), f('productId', 'Finished product', { lookup: prod, required: true }), f('outputQuantity', 'Output quantity', { type: 'number', default: 1, required: true }),
    f('version', 'Version', { default: '1.0' }), f('isActive', 'Active', { type: 'checkbox', default: true }), f('notes', 'Notes', { type: 'textarea' })],
  lines: { key: 'items', cols: [{ name: 'componentProductId', label: 'Component', type: 'product', nameKey: 'componentName' }, { name: 'quantity', label: 'Qty per output', type: 'number' }, { name: 'scrapPercent', label: 'Scrap %', type: 'number' }], newLine: () => ({ componentProductId: null, quantity: 1, scrapPercent: 0 }) },
  detail: (r) => (<div><div className="mb-3 text-sm text-slate-600">{r.productName} · output {num(r.outputQuantity)} · v{r.version} {r.notes && `· ${r.notes}`}</div>
    <table className="w-full"><thead><tr><th className="th">Component</th><th className="th text-right">Qty per output</th><th className="th text-right">Scrap %</th></tr></thead><tbody>{r.items.map((i: any) => <tr key={i.id}><td className="td">{i.sku} · {i.componentName}</td><td className="td text-right">{num(i.quantity)}</td><td className="td text-right">{num(i.scrapPercent)}</td></tr>)}</tbody></table></div>),
};
const componentTable = (comps: any[]) => (
  <table className="w-full"><thead><tr><th className="th">Material</th><th className="th text-right">Required</th><th className="th text-right">Issued</th><th className="th text-right">Available</th><th className="th text-right">Shortage</th><th className="th text-right">Unit cost</th></tr></thead>
    <tbody>{comps.map((c) => <tr key={c.productId}><td className="td">{c.sku} · {c.productName}</td><td className="td text-right">{num(c.required)}</td><td className="td text-right">{num(c.issued)}</td><td className="td text-right">{num(c.available)}</td><td className={`td text-right ${c.shortage > 0 ? 'font-semibold text-red-600' : ''}`}>{num(c.shortage)}</td><td className="td text-right">{money(c.unitCost)}</td></tr>)}</tbody></table>
);
export const productionOrders: ResourceConfig = {
  title: 'Production Orders', subtitle: 'Release (availability check) → issue materials → complete with quality check', endpoint: '/production-orders', view: 'production.view', manage: 'production.manage', createLabel: 'New production order', statuses: ['Planned', 'Released', 'InProgress', 'Completed', 'Rejected', 'Cancelled'], dates: true,
  columns: [col('number', 'Number', { sortable: true }), col('date', 'Date', { type: 'date', sortable: true }), col('productName', 'Product'), col('quantity', 'Planned', { type: 'number' }), col('quantityProduced', 'Produced', { type: 'number' }), col('totalCost', 'Total cost', { type: 'money' }), col('unitCost', 'Unit cost', { type: 'money' }), col('qualityStatus', 'Quality', { type: 'status' }), col('status', 'Status', { type: 'status', sortable: true })],
  fields: [f('productId', 'Finished product (needs an active BOM)', { lookup: prod, required: true }), f('quantity', 'Quantity to produce', { type: 'number', required: true }), f('sourceWarehouseId', 'Raw material warehouse', { lookup: wh, required: true }), f('targetWarehouseId', 'Finished goods warehouse', { lookup: wh, required: true }), date('Order date'), f('dueDate', 'Due date', { type: 'date' }), f('notes', 'Notes', { type: 'textarea' })],
  mapSave: (x) => ({ ...x, dueDate: x.dueDate || null }),
  detail: (r) => (<div><dl className="mb-4 grid gap-3 sm:grid-cols-4">
    {[['Product', r.productName], ['BOM', r.bomCode], ['Planned qty', num(r.quantity)], ['Produced qty', num(r.quantityProduced)], ['Material cost', money(r.materialCost)], ['Labor cost', money(r.laborCost)], ['Overhead', money(r.overheadCost)], ['Unit cost', money(r.unitCost)]].map(([k, v]) => <div key={k as string}><dt className="text-xs text-slate-500">{k}</dt><dd className="text-sm font-medium">{v as any}</dd></div>)}
    <div><dt className="text-xs text-slate-500">Status</dt><dd><StatusBadge value={r.status} /></dd></div><div><dt className="text-xs text-slate-500">Quality</dt><dd><StatusBadge value={r.qualityStatus} /></dd></div></dl>
    <div className="mb-1 text-sm font-semibold">Material requirements</div>{componentTable(r.components)}{r.notes && <p className="mt-3 text-sm text-slate-500">{r.notes}</p>}</div>),
  actions: [act('Release (check materials)', '/production-orders/:id/release', ['Planned']), act('Issue all materials', '/production-orders/:id/issue-materials', ['Released', 'InProgress'], { confirm: 'Issue all remaining materials from the warehouse to production?' }),
    act('Complete & receive goods', '/production-orders/:id/complete', ['InProgress'], { promptTitle: 'Quality check & finished goods receipt', prompt: [f('quantityProduced', 'Quantity produced', { type: 'number', required: true }), f('qualityStatus', 'Quality check', { type: 'select', default: 'Passed', options: [{ value: 'Passed', label: 'Passed' }, { value: 'Failed', label: 'Failed (scrap)' }] }), f('laborCost', 'Labor cost', { type: 'number', default: 0 }), f('overheadCost', 'Overhead cost', { type: 'number', default: 0 })] }),
    act('Cancel', '/production-orders/:id/cancel', ['Planned', 'Released', 'InProgress'], { tone: 'danger', confirm: 'Cancel this production order?' })],
};
export const materialIssues: ResourceConfig = {
  title: 'Material Issues', subtitle: 'Raw materials consumed by production orders', endpoint: '/material-issues', view: 'production.view', dates: true, lines: { cols: [] },
  columns: [col('number', 'Number', { sortable: true }), col('date', 'Date', { type: 'date', sortable: true }), col('refNumber', 'Production order'), col('warehouseName', 'Issued from'), col('status', 'Status', { type: 'status' })],
};
export const finishedGoods: ResourceConfig = {
  title: 'Finished Goods Receipts', subtitle: 'Output of completed production orders', endpoint: '/production-receipts', view: 'production.view', dates: true, rowClick: false,
  columns: [col('number', 'Number', { sortable: true }), col('date', 'Date', { type: 'date', sortable: true }), col('product', 'Product'), col('order', 'Production order'), col('warehouse', 'Warehouse'), col('quantity', 'Qty', { type: 'number' }), col('unitCost', 'Unit cost', { type: 'money' }), col('totalCost', 'Total cost', { type: 'money' }), col('quality', 'Quality', { type: 'status' })],
};

// ---------- Finance ----------
export const accounts: ResourceConfig = {
  title: 'Chart of Accounts', endpoint: '/accounts', view: 'finance.view', manage: 'finance.manage', createLabel: 'New account', canEdit: (r) => !r.isSystem, canDelete: (r) => !r.isSystem, defaultSort: { by: 'code', dir: 'asc' },
  columns: [col('code', 'Code', { sortable: true }), col('name', 'Name', { sortable: true }), col('accountType', 'Type', { sortable: true }), col('isSystem', 'System', { type: 'bool' }), col('isActive', 'Active', { type: 'bool' })],
  fields: [f('code', 'Account code', { required: true }), f('name', 'Account name', { required: true }), f('accountType', 'Type', { type: 'select', required: true, options: ['Asset', 'Liability', 'Equity', 'Revenue', 'Expense'].map((x) => ({ value: x, label: x })) }), f('isActive', 'Active', { type: 'checkbox', default: true })],
};
export const payments: ResourceConfig = {
  title: 'Payments', subtitle: 'Supplier payments and customer receipts (record them from Accounts Payable / Receivable)', endpoint: '/payments', view: 'finance.view', rowClick: false, dates: true,
  typeFilter: { param: 'type', label: 'All types', options: [{ value: 'SupplierPayment', label: 'Supplier payments' }, { value: 'CustomerReceipt', label: 'Customer receipts' }] },
  columns: [col('number', 'Number', { sortable: true }), col('date', 'Date', { type: 'date', sortable: true }), col('type', 'Type'), col('party', 'Party'), col('invoice', 'Invoice'), col('amount', 'Amount', { type: 'money' }), col('method', 'Method'), col('reference', 'Reference')],
};
const methodField = f('method', 'Paid via', { type: 'select', default: 'Cash', options: [{ value: 'Cash', label: 'Cash' }, { value: 'Bank', label: 'Bank' }] });
export const expenses: ResourceConfig = {
  title: 'Expenses', subtitle: 'Posts Dr expense / Cr cash or bank', endpoint: '/expenses', view: 'finance.view', manage: 'finance.manage', createLabel: 'New expense', dates: true, rowClick: false,
  columns: [col('number', 'Number', { sortable: true }), col('date', 'Date', { type: 'date', sortable: true }), col('account', 'Account'), col('payee', 'Payee'), col('description', 'Description'), col('method', 'Method'), col('amount', 'Amount', { type: 'money' })],
  fields: [date(), f('accountId', 'Expense account', { lookup: acct('Expense'), required: true }), f('amount', 'Amount', { type: 'number', required: true }), methodField, f('party', 'Payee'), f('description', 'Description', { type: 'textarea' })],
};
export const incomes: ResourceConfig = {
  title: 'Other Income', subtitle: 'Posts Dr cash or bank / Cr income', endpoint: '/incomes', view: 'finance.view', manage: 'finance.manage', createLabel: 'New income', dates: true, rowClick: false,
  columns: [col('number', 'Number', { sortable: true }), col('date', 'Date', { type: 'date', sortable: true }), col('account', 'Account'), col('source', 'Source'), col('description', 'Description'), col('method', 'Method'), col('amount', 'Amount', { type: 'money' })],
  fields: [date(), f('accountId', 'Income account', { lookup: acct('Revenue'), required: true }), f('amount', 'Amount', { type: 'number', required: true }), { ...methodField, default: 'Bank' }, f('party', 'Source'), f('description', 'Description', { type: 'textarea' })],
};
export const journals: ResourceConfig = {
  title: 'Journal Entries', subtitle: 'All postings are double-entry. Manual journals must balance to be posted.', endpoint: '/journal-entries', view: 'finance.view', manage: 'finance.manage', createLabel: 'New journal', statuses: ['Draft', 'Posted', 'Reversed'], dates: true,
  columns: [col('number', 'Number', { sortable: true }), col('date', 'Date', { type: 'date', sortable: true }), col('description', 'Description'), col('referenceType', 'Source'), col('totalDebit', 'Debit', { type: 'money' }), col('totalCredit', 'Credit', { type: 'money' }), col('status', 'Status', { type: 'status', sortable: true })],
  fields: [date(), f('description', 'Description', { required: true, full: true }), f('post', 'Post immediately', { type: 'checkbox', default: true })],
  lines: { cols: [{ name: 'accountId', label: 'Account', type: 'account' }, { name: 'debit', label: 'Debit', type: 'number' }, { name: 'credit', label: 'Credit', type: 'number' }, { name: 'memo', label: 'Memo', type: 'text' }], newLine: () => ({ accountId: null, debit: 0, credit: 0, memo: '' }) },
  detail: (r) => (<div><p className="mb-3 text-sm">{r.description}</p><table className="w-full"><thead><tr><th className="th">Account</th><th className="th text-right">Debit</th><th className="th text-right">Credit</th><th className="th">Memo</th></tr></thead>
    <tbody>{r.lines.map((l: any) => <tr key={l.id}><td className="td">{l.accountCode} · {l.accountName}</td><td className="td text-right">{l.debit ? money(l.debit) : ''}</td><td className="td text-right">{l.credit ? money(l.credit) : ''}</td><td className="td">{l.memo}</td></tr>)}
      <tr><td className="td font-semibold">Totals</td><td className="td text-right font-semibold">{money(r.totalDebit)}</td><td className="td text-right font-semibold">{money(r.totalCredit)}</td><td className="td" /></tr></tbody></table></div>),
  actions: [act('Post', '/journal-entries/:id/post', ['Draft']), { label: 'Reverse', path: (r) => `/journal-entries/${r.id}/reverse`, show: (r) => r.status === 'Posted' && r.referenceType === 'Manual', tone: 'danger', confirm: 'Create a reversing entry?' }],
};

// ---------- Administration ----------
export const users: ResourceConfig = {
  title: 'Users', endpoint: '/users', view: 'admin.manage', manage: 'admin.manage', canEdit: yes, canDelete: true, createLabel: 'New user',
  columns: [col('username', 'Username', { sortable: true }), col('fullName', 'Full name'), col('email', 'Email'), col('roleName', 'Role'), col('isActive', 'Active', { type: 'bool' })],
  fields: [f('username', 'Username', { required: true, disabledOnEdit: true }), f('fullName', 'Full name', { required: true }), f('email', 'Email'), f('roleId', 'Role', { lookup: role, required: true }), f('password', 'Password', { type: 'password', required: 'create', help: 'Min 8 characters. On edit, leave blank to keep the current password.' }), f('isActive', 'Active', { type: 'checkbox', default: true })],
};
export const auditLogs: ResourceConfig = {
  title: 'Audit Log', subtitle: 'Every create / update / delete of business records', endpoint: '/audit-logs', view: 'admin.manage', rowClick: false,
  columns: [col('timestamp', 'When', { render: (r) => new Date(r.timestamp).toLocaleString() }), col('userName', 'User'), col('action', 'Action'), col('entityName', 'Entity'), col('entityId', 'Id'), col('details', 'Changed fields')],
};
