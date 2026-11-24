import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { api, errMsg, fmtDate, money, num } from '../api';
import { ErrorBox, PageHeader, Spinner } from '../ui';

const COLORS = ['#2563eb', '#16a34a', '#f59e0b', '#9333ea', '#ef4444', '#0891b2', '#64748b'];
const compact = (v: number) => (Math.abs(v) >= 100000 ? `${(v / 100000).toFixed(1)}L` : Math.abs(v) >= 1000 ? `${(v / 1000).toFixed(0)}k` : String(v));

function Card({ label, value, sub, to, tone }: { label: string; value: string | number; sub?: string; to?: string; tone?: string }) {
  const body = <div className="card p-4 transition hover:shadow-md"><div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div><div className={`mt-1 text-2xl font-semibold ${tone ?? 'text-slate-900'}`}>{value}</div>{sub && <div className="text-xs text-slate-400">{sub}</div>}</div>;
  return to ? <Link to={to}>{body}</Link> : body;
}
const ChartCard = ({ title, children }: { title: string; children: React.ReactNode }) => <div className="card p-4"><h3 className="mb-3 text-sm font-semibold">{title}</h3><div className="h-64">{children}</div></div>;

export default function Dashboard() {
  const [d, setD] = useState<any>(null); const [err, setErr] = useState('');
  const load = () => api.get('/dashboard').then((r) => setD(r.data)).catch((e) => setErr(errMsg(e)));
  useEffect(() => { load(); }, []);
  if (err) return <ErrorBox text={err} onRetry={load} />;
  if (!d) return <Spinner />;
  const t = d.totals;
  return (
    <div>
      <PageHeader title="Dashboard" subtitle="Live overview of your single-shop retail business" />
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Card label="Products" value={t.products} to="/inventory/products" /><Card label="Suppliers" value={t.suppliers} to="/procurement/suppliers" />
        <Card label="Customers" value={t.customers} to="/sales/customers" /><Card label="Purchase orders" value={t.purchaseOrders} sub={`${d.pending.purchaseOrders} pending`} to="/procurement/orders" />
        <Card label="Sales orders" value={t.salesOrders} sub={`${d.pending.salesOrders} pending`} to="/sales/orders" /><Card label="Inventory value" value={money(t.inventoryValue)} to="/inventory/reports" />
        <Card label="Sales revenue" value={money(t.salesRevenue)} sub="net of returns" /><Card label="Purchase value" value={money(t.purchaseValue)} sub="net of returns" />
        <Card label="Total expenses" value={money(t.expenses)} to="/finance/expenses" /><Card label="Net profit" value={money(t.netProfit)} sub="from the general ledger" tone={t.netProfit >= 0 ? 'text-green-600' : 'text-red-600'} to="/finance/reports" />
        <Card label="Low-stock items" value={d.lowStock.length} tone={d.lowStock.length ? 'text-red-600' : undefined} to="/inventory/stock" />
      </div>
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <ChartCard title="Monthly sales"><ResponsiveContainer><BarChart data={d.monthlySales}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="month" fontSize={11} /><YAxis fontSize={11} tickFormatter={compact} /><Tooltip formatter={(v: any) => money(v)} /><Bar dataKey="value" name="Sales" fill="#2563eb" radius={[3, 3, 0, 0]} /></BarChart></ResponsiveContainer></ChartCard>
        <ChartCard title="Monthly purchases"><ResponsiveContainer><BarChart data={d.monthlyPurchases}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="month" fontSize={11} /><YAxis fontSize={11} tickFormatter={compact} /><Tooltip formatter={(v: any) => money(v)} /><Bar dataKey="value" name="Purchases" fill="#f59e0b" radius={[3, 3, 0, 0]} /></BarChart></ResponsiveContainer></ChartCard>
        <ChartCard title="Inventory distribution (by value)">{d.inventoryDistribution.length === 0 ? <div className="p-8 text-center text-sm text-slate-400">No stock yet.</div> :
          <ResponsiveContainer><PieChart><Pie data={d.inventoryDistribution} dataKey="value" nameKey="name" outerRadius={85} label={(e: any) => e.name}>{d.inventoryDistribution.map((_: any, i: number) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}</Pie><Tooltip formatter={(v: any) => money(v)} /></PieChart></ResponsiveContainer>}</ChartCard>
        <ChartCard title="Revenue vs expenses"><ResponsiveContainer><LineChart data={d.revenueVsExpenses}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="month" fontSize={11} /><YAxis fontSize={11} tickFormatter={compact} /><Tooltip formatter={(v: any) => money(v)} /><Legend /><Line type="monotone" dataKey="revenue" name="Revenue" stroke="#16a34a" strokeWidth={2} /><Line type="monotone" dataKey="expenses" name="Expenses" stroke="#ef4444" strokeWidth={2} /></LineChart></ResponsiveContainer></ChartCard>
      </div>
      <div className="mt-4 grid gap-4 lg:grid-cols-3">
        <div className="card lg:col-span-1"><h3 className="border-b border-slate-200 px-4 py-3 text-sm font-semibold">⚠ Low-stock alerts</h3>
          {d.lowStock.length === 0 ? <div className="p-4 text-sm text-slate-400">All items are above their reorder level.</div> : <ul>{d.lowStock.map((p: any) => <li key={p.id} className="flex justify-between border-b border-slate-100 px-4 py-2 text-sm"><span>{p.sku} · {p.name}</span><span className="font-semibold text-red-600">{num(p.onHand)} / {num(p.reorderLevel)}</span></li>)}</ul>}</div>
        <div className="card lg:col-span-1"><h3 className="border-b border-slate-200 px-4 py-3 text-sm font-semibold">Recent transactions</h3>
          {d.recentTransactions.length === 0 ? <div className="p-4 text-sm text-slate-400">No transactions.</div> : <ul>{d.recentTransactions.map((j: any) => <li key={j.number} className="border-b border-slate-100 px-4 py-2 text-sm"><div className="flex justify-between"><span className="font-medium">{j.number}</span><span>{money(j.amount)}</span></div><div className="text-xs text-slate-500">{fmtDate(j.date)} · {j.description}</div></li>)}</ul>}</div>
        <div className="card lg:col-span-1"><h3 className="border-b border-slate-200 px-4 py-3 text-sm font-semibold">🔔 Notifications</h3>
          {d.notifications.length === 0 ? <div className="p-4 text-sm text-slate-400">Nothing new.</div> : <ul>{d.notifications.map((n: any) => <li key={n.id} className="border-b border-slate-100 px-4 py-2 text-sm">{n.message}</li>)}</ul>}</div>
      </div>
    </div>
  );
}
