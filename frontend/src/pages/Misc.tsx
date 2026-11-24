import { useEffect, useState } from 'react';
import { api, errMsg, money, num } from '../api';
import { useAuth } from '../auth';
import { Empty, ErrorBox, LookupSelect, Modal, PageHeader, Spinner, useToast } from '../ui';
import ResourcePage, { PromptModal } from '../components/ResourcePage';
import { Tabs } from '../components/ReportPage';
import { stockBalances, stockHistory } from './resources';

export const StockPage = () => <Tabs tabs={[{ label: 'Stock balances', node: <ResourcePage cfg={stockBalances} /> }, { label: 'Movement history', node: <ResourcePage cfg={stockHistory} /> }]} />;

export function PlanningPage() {
  const { can } = useAuth(); const { toast } = useToast();
  const [f, setF] = useState<any>({ productId: null, quantity: 100, warehouseId: null }); const [plan, setPlan] = useState<any>(null); const [busy, setBusy] = useState(false); const [ask, setAsk] = useState(false);
  const run = async () => {
    if (!f.productId || !f.quantity) return toast('error', 'Select a finished product and a quantity.');
    setBusy(true); try { const { data } = await api.post('/production/plan', { productId: f.productId, quantity: Number(f.quantity), warehouseId: f.warehouseId }); setPlan(data); } catch (e) { setPlan(null); toast('error', errMsg(e)); } finally { setBusy(false); }
  };
  const req = async () => { try { const { data } = await api.post('/production/plan/requisition', { productId: f.productId, quantity: Number(f.quantity), warehouseId: f.warehouseId }); toast('success', `Requisition ${data.number} created for the shortages.`); } catch (e) { toast('error', errMsg(e)); } };
  return (<div><PageHeader title="Production Planning" subtitle="Material requirement planning: explode the BOM, compare with stock, see shortages" />
    <div className="card mb-4 grid gap-3 p-4 sm:grid-cols-4">
      <div className="sm:col-span-2"><label className="label">Finished product</label><LookupSelect endpoint="/products" label={(r) => `${r.sku} · ${r.name}`} filter={(r) => r.productType === 'FinishedGood'} value={f.productId} onChange={(v) => setF({ ...f, productId: v })} /></div>
      <div><label className="label">Quantity</label><input className="input" type="number" value={f.quantity} onChange={(e) => setF({ ...f, quantity: e.target.value })} /></div>
      <div><label className="label">Raw material warehouse</label><LookupSelect endpoint="/warehouses" label={(r) => r.name} value={f.warehouseId} placeholder="All warehouses" onChange={(v) => setF({ ...f, warehouseId: v })} /></div>
      <div className="sm:col-span-4"><button className="btn btn-primary" disabled={busy} onClick={run}>{busy ? 'Calculating…' : 'Calculate requirements'}</button></div>
    </div>
    {plan && <div className="card"><div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-200 p-4"><div><div className="font-semibold">{plan.quantity} × {plan.productName} <span className="text-sm font-normal text-slate-500">({plan.bomCode})</span></div><div className="text-sm text-slate-500">Estimated material cost {money(plan.estimatedMaterialCost)}</div></div>
      <div className="flex items-center gap-2"><span className={`rounded-full px-3 py-1 text-xs font-semibold ${plan.canProduce ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`}>{plan.canProduce ? 'Materials available' : 'Material shortage'}</span>
        {!plan.canProduce && can('production.manage') && <button className="btn" onClick={req}>Create purchase requisition</button>}{can('production.manage') && <button className="btn btn-primary" onClick={() => setAsk(true)}>Create production order</button>}</div></div>
      <div className="overflow-x-auto"><table className="w-full"><thead><tr><th className="th">Material</th><th className="th text-right">Required</th><th className="th text-right">Available</th><th className="th text-right">Shortage</th><th className="th text-right">Unit cost</th><th className="th text-right">Est. cost</th></tr></thead>
        <tbody>{plan.components.map((c: any) => <tr key={c.productId}><td className="td">{c.sku} · {c.productName}</td><td className="td text-right">{num(c.required)}</td><td className="td text-right">{num(c.available)}</td><td className={`td text-right ${c.shortage > 0 ? 'font-semibold text-red-600' : ''}`}>{num(c.shortage)}</td><td className="td text-right">{money(c.unitCost)}</td><td className="td text-right">{money(c.estimatedCost)}</td></tr>)}</tbody></table></div></div>}
    {ask && <PromptModal title="Create production order" fields={[{ name: 'sourceWarehouseId', label: 'Raw material warehouse', lookup: { endpoint: '/warehouses', label: (r: any) => r.name }, required: true, default: f.warehouseId ?? '' }, { name: 'targetWarehouseId', label: 'Finished goods warehouse', lookup: { endpoint: '/warehouses', label: (r: any) => r.name }, required: true }, { name: 'dueDate', label: 'Due date', type: 'date' }]}
      onClose={() => setAsk(false)} onSubmit={async (v) => { try { await api.post('/production-orders', { productId: f.productId, quantity: Number(f.quantity), sourceWarehouseId: v.sourceWarehouseId, targetWarehouseId: v.targetWarehouseId, dueDate: v.dueDate || null }); toast('success', 'Production order created. Continue under Production Orders.'); setAsk(false); } catch (e) { toast('error', errMsg(e)); } }} />}
  </div>);
}

export function RolesPage() {
  const [roles, setRoles] = useState<any[]>([]); const [perms, setPerms] = useState<any[]>([]); const [edit, setEdit] = useState<any>(null); const [err, setErr] = useState(''); const [loading, setLoading] = useState(true); const { toast } = useToast();
  const load = async () => { try { const [r, p] = await Promise.all([api.get('/roles'), api.get('/permissions')]); setRoles(r.data.items); setPerms(p.data); } catch (e) { setErr(errMsg(e)); } finally { setLoading(false); } };
  useEffect(() => { load(); }, []);
  const save = async () => { try { if (edit.id) await api.put(`/roles/${edit.id}`, edit); else await api.post('/roles', edit); toast('success', 'Role saved.'); setEdit(null); load(); } catch (e) { toast('error', errMsg(e)); } };
  const del = async (r: any) => { if (!confirm(`Delete role ${r.name}?`)) return; try { await api.delete(`/roles/${r.id}`); load(); } catch (e) { toast('error', errMsg(e)); } };
  if (loading) return <Spinner />; if (err) return <ErrorBox text={err} onRetry={load} />;
  return (<div><PageHeader title="Roles" subtitle="Roles group permissions; users get the permissions of their role"><button className="btn btn-primary" onClick={() => setEdit({ name: '', description: '', permissions: [] })}>+ New role</button></PageHeader>
    <div className="grid gap-3 md:grid-cols-2">{roles.map((r) => <div key={r.id} className="card p-4"><div className="flex items-start justify-between"><div><div className="font-semibold">{r.name}</div><div className="text-xs text-slate-500">{r.description}</div></div>
      <div className="flex gap-1"><button className="btn btn-sm" onClick={() => setEdit({ ...r })}>Edit</button><button className="btn btn-sm" onClick={() => del(r)}>Delete</button></div></div>
      <div className="mt-3 flex flex-wrap gap-1">{r.permissions.map((p: string) => <span key={p} className="rounded bg-brand-50 px-2 py-0.5 text-xs text-brand-700">{p}</span>)}</div></div>)}</div>
    {edit && <Modal title={edit.id ? `Edit role ${edit.name}` : 'New role'} onClose={() => setEdit(null)} footer={<><button className="btn" onClick={() => setEdit(null)}>Cancel</button><button className="btn btn-primary" onClick={save}>Save</button></>}>
      <label className="label">Name</label><input className="input mb-3" value={edit.name} onChange={(e) => setEdit({ ...edit, name: e.target.value })} />
      <label className="label">Description</label><input className="input mb-3" value={edit.description ?? ''} onChange={(e) => setEdit({ ...edit, description: e.target.value })} />
      <label className="label">Permissions</label><div className="grid gap-1 sm:grid-cols-2">{perms.map((p) => <label key={p.code} className="flex items-center gap-2 text-sm"><input type="checkbox" checked={edit.permissions.includes(p.code)} onChange={(e) => setEdit({ ...edit, permissions: e.target.checked ? [...edit.permissions, p.code] : edit.permissions.filter((x: string) => x !== p.code) })} />{p.code}<span className="text-xs text-slate-400">{p.description}</span></label>)}</div></Modal>}
  </div>);
}

export function PermissionsPage() {
  const [roles, setRoles] = useState<any[]>([]); const [perms, setPerms] = useState<any[]>([]); const [err, setErr] = useState('');
  useEffect(() => { Promise.all([api.get('/roles'), api.get('/permissions')]).then(([r, p]) => { setRoles(r.data.items); setPerms(p.data); }).catch((e) => setErr(errMsg(e))); }, []);
  if (err) return <ErrorBox text={err} />; if (!perms.length) return <Spinner />;
  return (<div><PageHeader title="Permissions" subtitle="Permission matrix (edit assignments under Roles)" /><div className="card overflow-x-auto"><table className="w-full"><thead><tr><th className="th">Permission</th>{roles.map((r) => <th key={r.id} className="th text-center">{r.name}</th>)}</tr></thead>
    <tbody>{perms.map((p) => <tr key={p.code}><td className="td"><div className="font-medium">{p.code}</div><div className="text-xs text-slate-400">{p.description}</div></td>{roles.map((r) => <td key={r.id} className="td text-center">{r.permissions.includes(p.code) ? '✅' : '—'}</td>)}</tr>)}</tbody></table></div></div>);
}

export function SettingsPage() {
  const [s, setS] = useState<any>(null); const { toast } = useToast(); const [err, setErr] = useState('');
  useEffect(() => { api.get('/settings').then((r) => setS(r.data)).catch((e) => setErr(errMsg(e))); }, []);
  if (err) return <ErrorBox text={err} />; if (!s) return <Spinner />;
  const labels: Record<string, string> = { CompanyName: 'Company name', CompanyAddress: 'Company address', CurrencySymbol: 'Currency symbol', DefaultTaxRate: 'Default tax rate %' };
  return (<div><PageHeader title="Settings" subtitle="Company information" /><div className="card max-w-xl p-5">{Object.keys(labels).map((k) => <div key={k} className="mb-3"><label className="label">{labels[k]}</label><input className="input" value={s[k] ?? ''} onChange={(e) => setS({ ...s, [k]: e.target.value })} /></div>)}
    <button className="btn btn-primary" onClick={async () => { try { await api.put('/settings', s); toast('success', 'Settings saved.'); } catch (e) { toast('error', errMsg(e)); } }}>Save settings</button>
    <p className="mt-3 text-xs text-slate-400">Note: currency display in the UI is fixed to ₹ in this version.</p></div></div>);
}
