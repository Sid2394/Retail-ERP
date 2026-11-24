import { useEffect, useState } from 'react';
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { api, errMsg, money } from '../api';
import { ErrorBox, PageHeader, Spinner } from '../ui';
import ReportPage, { ReportDef, ReportTabs } from '../components/ReportPage';

const c = (key: string, label: string, type?: any) => ({ key, label, type });
export const defs: Record<string, ReportDef> = {
  sales: { id: 'sales-report', title: 'Sales Report', endpoint: '/reports/sales', dates: true, party: { endpoint: '/customers', label: (r) => r.name }, partyLabel: 'All customers', columns: [c('date', 'Date', 'date'), c('number', 'Invoice'), c('customer', 'Customer'), c('subtotal', 'Subtotal', 'money'), c('tax', 'Tax', 'money'), c('total', 'Total', 'money'), c('paid', 'Received', 'money'), c('credited', 'Returns', 'money'), c('balance', 'Balance', 'money'), c('status', 'Status', 'status')] },
  purchases: { id: 'purchase-report', title: 'Purchase Report', endpoint: '/reports/purchases', dates: true, party: { endpoint: '/suppliers', label: (r) => r.name }, partyLabel: 'All suppliers', columns: [c('date', 'Date', 'date'), c('number', 'Invoice'), c('supplierInvoice', 'Supplier inv #'), c('supplier', 'Supplier'), c('subtotal', 'Subtotal', 'money'), c('tax', 'Tax', 'money'), c('total', 'Total', 'money'), c('paid', 'Paid', 'money'), c('credited', 'Returns', 'money'), c('balance', 'Balance', 'money'), c('status', 'Status', 'status')] },
  inventory: { id: 'inventory-report', title: 'Inventory Report', endpoint: '/reports/inventory', party: { endpoint: '/warehouses', label: (r) => r.name }, partyLabel: 'All warehouses', columns: [c('sku', 'SKU'), c('product', 'Product'), c('category', 'Category'), c('type', 'Type'), c('warehouse', 'Warehouse'), c('quantity', 'Qty', 'number'), c('avgCost', 'Avg cost', 'money'), c('value', 'Value', 'money'), c('reorderLevel', 'Reorder', 'number'), c('low', 'Level', 'status')] },
  valuation: { id: 'stock-valuation', title: 'Stock Valuation', endpoint: '/reports/stock-valuation', columns: [c('sku', 'SKU'), c('product', 'Product'), c('category', 'Category'), c('type', 'Type'), c('quantity', 'Total qty', 'number'), c('avgCost', 'Avg cost', 'money'), c('value', 'Value', 'money')] },
  suppliers: { id: 'supplier-report', title: 'Supplier Report', endpoint: '/reports/suppliers', columns: [c('code', 'Code'), c('supplier', 'Supplier'), c('phone', 'Phone'), c('invoices', 'Invoices', 'number'), c('invoiced', 'Invoiced', 'money'), c('paid', 'Paid', 'money'), c('credited', 'Returns', 'money'), c('outstanding', 'Outstanding', 'money')] },
  customers: { id: 'customer-report', title: 'Customer Report', endpoint: '/reports/customers', columns: [c('code', 'Code'), c('customer', 'Customer'), c('phone', 'Phone'), c('invoices', 'Invoices', 'number'), c('invoiced', 'Invoiced', 'money'), c('received', 'Received', 'money'), c('credited', 'Returns', 'money'), c('outstanding', 'Outstanding', 'money'), c('creditLimit', 'Credit limit', 'money')] },
  expenses: { id: 'expense-report', title: 'Expense Report', endpoint: '/reports/expenses', dates: true, columns: [c('date', 'Date', 'date'), c('number', 'Number'), c('account', 'Account'), c('payee', 'Payee'), c('description', 'Description'), c('method', 'Method'), c('amount', 'Amount', 'money')] },
  revenue: { id: 'revenue-report', title: 'Revenue Report', endpoint: '/reports/revenue', dates: true, columns: [c('date', 'Date', 'date'), c('source', 'Source'), c('reference', 'Reference'), c('amount', 'Amount', 'money')] },
  receivables: { id: 'outstanding-receivables', title: 'Outstanding Receivables', endpoint: '/reports/receivables', columns: [c('party', 'Customer'), c('number', 'Invoice'), c('date', 'Date', 'date'), c('dueDate', 'Due', 'date'), c('total', 'Total', 'money'), c('paid', 'Received', 'money'), c('credited', 'Returns', 'money'), c('outstanding', 'Outstanding', 'money'), c('daysOverdue', 'Days overdue', 'number')] },
  payables: { id: 'outstanding-payables', title: 'Outstanding Payables', endpoint: '/reports/payables', columns: [c('party', 'Supplier'), c('number', 'Invoice'), c('date', 'Date', 'date'), c('dueDate', 'Due', 'date'), c('total', 'Total', 'money'), c('paid', 'Paid', 'money'), c('credited', 'Returns', 'money'), c('outstanding', 'Outstanding', 'money'), c('daysOverdue', 'Days overdue', 'number')] },
};
const compact = (v: number) => (Math.abs(v) >= 100000 ? `${(v / 100000).toFixed(1)}L` : Math.abs(v) >= 1000 ? `${(v / 1000).toFixed(0)}k` : String(v));
const Chart = ({ title, data, keys }: { title: string; data: any[]; keys: { k: string; name: string; fill: string }[] }) => (
  <div className="card p-4"><h3 className="mb-3 text-sm font-semibold">{title}</h3><div className="h-64">{data.length === 0 ? <div className="p-8 text-center text-sm text-slate-400">No data.</div> :
    <ResponsiveContainer><BarChart data={data}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey={data[0].month ? 'month' : 'name'} fontSize={11} /><YAxis fontSize={11} tickFormatter={compact} /><Tooltip formatter={(v: any) => (typeof v === 'number' ? money(v) : v)} />{keys.length > 1 && <Legend />}{keys.map((x) => <Bar key={x.k} dataKey={x.k} name={x.name} fill={x.fill} radius={[3, 3, 0, 0]} />)}</BarChart></ResponsiveContainer>}</div></div>
);

const kinds: Record<string, { title: string; charts: (d: any) => React.ReactNode; tabs: ReportDef[] }> = {
  sales: { title: 'Sales Analytics', tabs: [defs.sales, defs.customers, defs.receivables], charts: (d) => <><Chart title="Monthly sales" data={d.monthly} keys={[{ k: 'value', name: 'Sales', fill: '#2563eb' }]} /><Chart title="Top customers" data={d.top} keys={[{ k: 'value', name: 'Sales', fill: '#16a34a' }]} /><Chart title="Top products" data={d.topProducts} keys={[{ k: 'value', name: 'Sales', fill: '#9333ea' }]} /></> },
  purchases: { title: 'Purchase Analytics', tabs: [defs.purchases, defs.suppliers, defs.payables], charts: (d) => <><Chart title="Monthly purchases" data={d.monthly} keys={[{ k: 'value', name: 'Purchases', fill: '#f59e0b' }]} /><Chart title="Top suppliers" data={d.top} keys={[{ k: 'value', name: 'Purchases', fill: '#2563eb' }]} /><Chart title="Top purchased products" data={d.topProducts} keys={[{ k: 'value', name: 'Purchases', fill: '#0891b2' }]} /></> },
  inventory: { title: 'Inventory Analytics', tabs: [defs.inventory, defs.valuation], charts: (d) => <><Chart title="Inventory value by category" data={d.top} keys={[{ k: 'value', name: 'Value', fill: '#2563eb' }]} /><Chart title="Inventory value by product type" data={d.topProducts} keys={[{ k: 'value', name: 'Value', fill: '#16a34a' }]} /></> },
  financial: { title: 'Financial Analytics', tabs: [defs.revenue, defs.expenses], charts: (d) => <Chart title="Revenue vs expenses (general ledger)" data={d.monthly} keys={[{ k: 'revenue', name: 'Revenue', fill: '#16a34a' }, { k: 'expenses', name: 'Expenses', fill: '#ef4444' }]} /> },
};

export function AnalyticsPage({ kind }: { kind: string }) {
  const k = kinds[kind]; const [d, setD] = useState<any>(null); const [err, setErr] = useState('');
  useEffect(() => { setD(null); api.get(`/reports/analytics/${kind}`).then((r) => setD(r.data)).catch((e) => setErr(errMsg(e))); }, [kind]);
  return (<div><PageHeader title={k.title} subtitle="Charts and detailed reports with date filters, search, sorting, pagination and CSV export" />
    {err ? <ErrorBox text={err} /> : !d ? <Spinner /> : <div className="mb-4 grid gap-4 lg:grid-cols-2">{k.charts(d)}</div>}
    <ReportTabs defs={k.tabs} /></div>);
}
export const InventoryReportsPage = () => (<div><PageHeader title="Inventory Reports" subtitle="Stock by warehouse and valuation" /><ReportTabs defs={[defs.inventory, defs.valuation]} /></div>);
