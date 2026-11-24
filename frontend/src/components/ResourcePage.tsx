import { ReactNode, useCallback, useEffect, useState } from 'react';
import { api, clearLookups, downloadCsv, errMsg, fmtDate, isoDate, lookup, money, num, today } from '../api';
import { useAuth } from '../auth';
import { Empty, ErrorBox, LookupSelect, Modal, PageHeader, Pagination, Spinner, StatusBadge, useToast } from '../ui';
import type { ActionDef, ColumnDef, FieldDef, LineCol, ResourceConfig } from '../types';

export function cell(c: ColumnDef, r: any): ReactNode {
  if (c.render) return c.render(r);
  const v = r[c.key];
  switch (c.type) {
    case 'money': return money(v);
    case 'number': return num(v);
    case 'date': return fmtDate(v);
    case 'status': return <StatusBadge value={v} />;
    case 'bool': return v ? '✓' : '—';
    default: return v ?? '';
  }
}
const right = (c: ColumnDef) => c.type === 'money' || c.type === 'number';

function FieldInput({ f, form, set, editing }: { f: FieldDef; form: any; set: (n: string, v: any, row?: any) => void; editing: boolean }) {
  const v = form[f.name]; const dis = editing && f.disabledOnEdit;
  if (f.type === 'checkbox') return <label className="flex items-center gap-2 pt-5 text-sm"><input type="checkbox" checked={!!v} onChange={(e) => set(f.name, e.target.checked)} />{f.label}</label>;
  let input: ReactNode;
  if (f.lookup) input = <LookupSelect endpoint={f.lookup.endpoint} params={f.lookup.params} value={v} valueKey={f.lookup.value ?? 'id'} label={f.lookup.label} filter={f.lookup.filter} disabled={dis} onChange={(val, row) => set(f.name, val, row)} />;
  else if (f.type === 'select') input = <select className="input" disabled={dis} value={v ?? ''} onChange={(e) => set(f.name, e.target.value)}>{!f.default && <option value="">Select…</option>}{f.options?.map((o) => <option key={String(o.value)} value={o.value}>{o.label}</option>)}</select>;
  else if (f.type === 'textarea') input = <textarea className="input" rows={2} value={v ?? ''} onChange={(e) => set(f.name, e.target.value)} />;
  else if (f.type === 'number') input = <input className="input" type="number" step={f.step ?? 'any'} disabled={dis} value={v ?? ''} onChange={(e) => set(f.name, e.target.value === '' ? '' : Number(e.target.value))} />;
  else input = <input className="input" type={f.type ?? 'text'} disabled={dis} value={v ?? ''} autoComplete={f.type === 'password' ? 'new-password' : undefined} onChange={(e) => set(f.name, e.target.value)} />;
  return <div className={f.full || f.type === 'textarea' ? 'sm:col-span-2' : ''}><label className="label">{f.label}{f.required === true && <span className="text-red-500"> *</span>}</label>{input}{f.help && <div className="mt-0.5 text-xs text-slate-400">{f.help}</div>}</div>;
}

export function LineEditor({ cfg, lines, onChange }: { cfg: NonNullable<ResourceConfig['lines']>; lines: any[]; onChange: (l: any[]) => void }) {
  const [products, setProducts] = useState<any[]>([]); const [accounts, setAccounts] = useState<any[]>([]);
  useEffect(() => { lookup('/products').then(setProducts).catch(() => {}); if (cfg.cols.some((c) => c.type === 'account')) lookup('/accounts').then(setAccounts).catch(() => {}); }, []);
  const upd = (i: number, patch: any) => onChange(lines.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));
  const pick = (i: number, col: LineCol, id: number | null) => {
    const p = products.find((x) => x.id === id); const patch: any = { [col.name]: id };
    if (p && cfg.priceFrom) { patch.unitPrice = p[cfg.priceFrom] ?? 0; patch.taxRate = p.taxRate ?? 0; }
    upd(i, patch);
  };
  const hasPrice = cfg.cols.some((c) => c.name === 'unitPrice');
  const total = lines.reduce((s, l) => s + (Number(l.quantity) || 0) * (Number(l.unitPrice) || 0) * (1 + (Number(l.taxRate) || 0) / 100), 0);
  const td = lines.reduce((s, l) => s + (Number(l.debit) || 0), 0); const tc = lines.reduce((s, l) => s + (Number(l.credit) || 0), 0);
  const isJournal = cfg.cols.some((c) => c.name === 'debit');
  return (
    <div className="mt-4">
      <div className="mb-1 flex items-center justify-between"><span className="text-sm font-semibold">Line items</span>{!cfg.fixed && <button type="button" className="btn btn-sm" onClick={() => onChange([...lines, cfg.newLine ? cfg.newLine() : { productId: null, quantity: 1, unitPrice: 0, taxRate: 0 }])}>+ Add line</button>}</div>
      <div className="overflow-x-auto rounded-md border border-slate-200">
        <table className="w-full"><thead><tr>{cfg.cols.map((c) => <th key={c.name} className="th">{c.label}</th>)}{!cfg.fixed && <th className="th" />}</tr></thead>
          <tbody>
            {lines.map((l, i) => (
              <tr key={i}>
                {cfg.cols.map((c) => (
                  <td key={c.name} className="td min-w-[110px]">
                    {c.type === 'product' ? (cfg.fixed || c.readOnly ? <span className="text-sm">{l.sku ? `${l.sku} · ` : ''}{l[c.nameKey ?? 'productName']}</span> :
                      <select className="input" value={l[c.name] ?? ''} onChange={(e) => pick(i, c, e.target.value ? Number(e.target.value) : null)}><option value="">Select product…</option>{products.map((p) => <option key={p.id} value={p.id}>{p.sku} · {p.name}</option>)}</select>) :
                      c.type === 'account' ? <select className="input" value={l[c.name] ?? ''} onChange={(e) => upd(i, { [c.name]: e.target.value ? Number(e.target.value) : null })}><option value="">Select account…</option>{accounts.map((a) => <option key={a.id} value={a.id}>{a.code} · {a.name}</option>)}</select> :
                      c.type === 'text' ? <input className="input" value={l[c.name] ?? ''} onChange={(e) => upd(i, { [c.name]: e.target.value })} /> :
                      c.readOnly ? <span className="text-sm">{num(l[c.name])}</span> :
                      <input className="input text-right" type="number" step={c.step ?? 'any'} min={0} value={l[c.name] ?? ''} onChange={(e) => upd(i, { [c.name]: e.target.value === '' ? '' : Number(e.target.value) })} />}
                  </td>
                ))}
                {!cfg.fixed && <td className="td"><button type="button" className="text-red-500" title="Remove" onClick={() => onChange(lines.filter((_, idx) => idx !== i))}>✕</button></td>}
              </tr>
            ))}
            {lines.length === 0 && <tr><td className="td text-center text-slate-400" colSpan={cfg.cols.length + 1}>No lines yet.</td></tr>}
          </tbody></table>
      </div>
      {hasPrice && <div className="mt-2 text-right text-sm">Total incl. tax: <b>{money(total)}</b></div>}
      {isJournal && <div className={`mt-2 text-right text-sm ${Math.abs(td - tc) < 0.0001 ? 'text-green-700' : 'text-red-600'}`}>Debit {money(td)} · Credit {money(tc)} {Math.abs(td - tc) < 0.0001 ? '(balanced)' : '(not balanced)'}</div>}
    </div>
  );
}

export function PromptModal({ title, fields, onClose, onSubmit }: { title: string; fields: FieldDef[]; onClose: () => void; onSubmit: (v: any) => Promise<void> }) {
  const [form, setForm] = useState<any>(() => Object.fromEntries(fields.map((f) => [f.name, f.default ?? (f.type === 'date' ? today() : '')]))); const [busy, setBusy] = useState(false);
  const { toast } = useToast();
  return (
    <Modal title={title} onClose={onClose} footer={<><button className="btn" onClick={onClose}>Cancel</button><button className="btn btn-primary" disabled={busy} onClick={async () => {
      const missing = fields.find((f) => f.required && (form[f.name] === '' || form[f.name] == null)); if (missing) return toast('error', `${missing.label} is required.`);
      setBusy(true); try { await onSubmit(form); } finally { setBusy(false); }
    }}>{busy ? 'Working…' : 'Confirm'}</button></>}>
      <div className="grid gap-3 sm:grid-cols-2">{fields.map((f) => <FieldInput key={f.name} f={f} form={form} editing={false} set={(n, v) => setForm((s: any) => ({ ...s, [n]: v }))} />)}</div>
    </Modal>
  );
}

export default function ResourcePage({ cfg, extraHeader }: { cfg: ResourceConfig; extraHeader?: ReactNode }) {
  const { can } = useAuth(); const { toast } = useToast();
  const linesKey = cfg.lines?.key ?? 'lines';
  const [data, setData] = useState<{ items: any[]; total: number }>({ items: [], total: 0 });
  const [loading, setLoading] = useState(true); const [error, setError] = useState('');
  const [page, setPage] = useState(1); const pageSize = 15; const [search, setSearch] = useState(''); const [q, setQ] = useState('');
  const [sort, setSort] = useState(cfg.defaultSort ?? { by: '', dir: 'desc' as 'asc' | 'desc' });
  const [status, setStatus] = useState(''); const [type, setType] = useState(''); const [from, setFrom] = useState(''); const [to, setTo] = useState('');
  const [tick, setTick] = useState(0);
  const [form, setForm] = useState<any | null>(null); const [saving, setSaving] = useState(false);
  const [view, setView] = useState<any | null>(null); const [prompt, setPrompt] = useState<{ a: ActionDef; row: any } | null>(null);
  const canManage = cfg.manage ? can(cfg.manage) : false;

  useEffect(() => { const t = setTimeout(() => { setQ(search); setPage(1); }, 300); return () => clearTimeout(t); }, [search]);
  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const params: any = { page, pageSize, search: q || undefined, sortBy: sort.by || undefined, sortDir: sort.dir, status: status || undefined, from: from || undefined, to: to || undefined };
      if (cfg.typeFilter && type) params[cfg.typeFilter.param] = type;
      const { data } = await api.get(cfg.endpoint, { params }); setData({ items: data.items ?? data, total: data.total ?? (data.items ?? data).length });
    } catch (e) { setError(errMsg(e)); } finally { setLoading(false); }
  }, [cfg.endpoint, page, q, sort.by, sort.dir, status, type, from, to, tick]);
  useEffect(() => { load(); }, [load]);
  const refresh = () => { clearLookups(); setTick((t) => t + 1); };

  const blank = () => {
    const f: any = {}; cfg.fields?.forEach((x) => { f[x.name] = x.default ?? (x.type === 'date' ? today() : x.type === 'checkbox' ? false : ''); });
    if (cfg.lines) f[linesKey] = cfg.lines.fixed ? [] : [cfg.lines.newLine ? cfg.lines.newLine() : { productId: null, quantity: 1, unitPrice: 0, taxRate: 0 }];
    return f;
  };
  const edit = (r: any) => {
    const f: any = { ...r }; cfg.fields?.forEach((x) => { if (x.type === 'date') f[x.name] = isoDate(r[x.name]); if (x.type === 'password') f[x.name] = ''; });
    if (cfg.lines) f[linesKey] = (r[linesKey] ?? []).map((l: any) => ({ ...l })); f._id = r.id; return f;
  };
  const setField = async (name: string, value: any) => {
    setForm((s: any) => ({ ...s, [name]: value }));
    if (cfg.prefill && name === cfg.prefill.field && value) {
      try { const { data: doc } = await api.get(`${cfg.prefill.endpoint}/${value}`); setForm((s: any) => ({ ...s, ...cfg.prefill!.apply(doc, s) })); } catch (e) { toast('error', errMsg(e)); }
    }
  };
  const save = async () => {
    if (!form) return;
    const missing = cfg.fields?.find((f) => (f.required === true || (f.required === 'create' && !form._id)) && (form[f.name] === '' || form[f.name] == null));
    if (missing) return toast('error', `${missing.label} is required.`);
    let payload: any = { ...form }; delete payload._id;
    cfg.fields?.forEach((x) => { if ((x.type === 'date' || x.lookup || x.type === 'select') && payload[x.name] === '') payload[x.name] = null; });
    if (cfg.lines) {
      const numCols = cfg.lines.cols.filter((c) => !c.type || c.type === 'number').map((c) => c.name);
      payload[linesKey] = (payload[linesKey] ?? []).map((l: any) => ({ ...l, ...Object.fromEntries(numCols.map((n) => [n, Number(l[n]) || 0])) }));
      if (cfg.lines.dropZero) payload[linesKey] = payload[linesKey].filter((l: any) => Number(l.quantity) > 0);
    }
    if (cfg.mapSave) payload = cfg.mapSave(payload);
    setSaving(true);
    try {
      if (form._id) await api.put(`${cfg.endpoint}/${form._id}`, payload); else await api.post(cfg.createEndpoint ?? cfg.endpoint, payload);
      toast('success', form._id ? 'Saved.' : 'Created.'); setForm(null); refresh();
    } catch (e) { toast('error', errMsg(e)); } finally { setSaving(false); }
  };
  const remove = async (r: any) => {
    if (!confirm('Delete this record?')) return;
    try { await api.delete(`${cfg.endpoint}/${r.id}`); toast('success', 'Deleted.'); setView(null); refresh(); } catch (e) { toast('error', errMsg(e)); }
  };
  const run = async (a: ActionDef, r: any, body?: any) => {
    try { await api.post(a.path(r), body ?? {}); toast('success', `${a.label}: done.`); setPrompt(null); setView(null); refresh(); } catch (e) { toast('error', errMsg(e)); }
  };
  const click = (a: ActionDef, r: any) => { if (a.prompt) setPrompt({ a, row: r }); else if (!a.confirm || confirm(a.confirm)) run(a, r); };
  const exportCsv = async () => {
    try {
      const params: any = { page: 1, pageSize: 100000, search: q || undefined, status: status || undefined, from: from || undefined, to: to || undefined };
      const { data } = await api.get(cfg.endpoint, { params }); const rows = (data.items ?? data).map((r: any) => r);
      downloadCsv(cfg.csvName ?? cfg.title.toLowerCase().replace(/\s+/g, '-'), rows, cfg.columns.filter((c) => !c.render || c.key).map((c) => ({ key: c.key, label: c.label })));
    } catch (e) { toast('error', errMsg(e)); }
  };

  const rowActions = (r: any) => (cfg.actions ?? []).filter((a) => a.show(r) && (!a.perm || can(a.perm)) && (canManage || a.perm));
  const toggleSort = (c: ColumnDef) => c.sortable && setSort((s) => ({ by: c.key, dir: s.by === c.key && s.dir === 'asc' ? 'desc' : 'asc' }));
  const lc = cfg.lines; const lines: any[] = view ? view[linesKey] ?? [] : [];

  return (
    <div>
      <PageHeader title={cfg.title} subtitle={cfg.subtitle}>
        {extraHeader}
        <button className="btn" onClick={exportCsv}>⬇ CSV</button>
        {canManage && cfg.fields && <button className="btn btn-primary" onClick={() => setForm(blank())}>+ {cfg.createLabel ?? 'New'}</button>}
      </PageHeader>
      <div className="card">
        <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 p-3">
          {!cfg.hideSearch && <input className="input max-w-xs" placeholder="Search…" value={search} onChange={(e) => setSearch(e.target.value)} />}
          {cfg.statuses && <select className="input w-44" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}><option value="">All statuses</option>{cfg.statuses.map((s) => <option key={s} value={s}>{s.replace(/([a-z])([A-Z])/g, '$1 $2')}</option>)}</select>}
          {cfg.typeFilter && <select className="input w-48" value={type} onChange={(e) => { setType(e.target.value); setPage(1); }}><option value="">{cfg.typeFilter.label}</option>{cfg.typeFilter.options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select>}
          {cfg.dates && <><input type="date" className="input w-40" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1); }} /><span className="text-xs text-slate-400">to</span><input type="date" className="input w-40" value={to} onChange={(e) => { setTo(e.target.value); setPage(1); }} /></>}
        </div>
        {loading && data.items.length === 0 ? <Spinner /> : error ? <ErrorBox text={error} onRetry={load} /> : data.items.length === 0 ? <Empty hint={canManage && cfg.fields ? 'Use the New button to create one.' : undefined} /> : (
          <div className="overflow-x-auto"><table className="w-full"><thead><tr>
            {cfg.columns.map((c) => <th key={c.key + c.label} className={`th ${right(c) ? 'text-right' : ''} ${c.sortable ? 'cursor-pointer select-none hover:text-slate-800' : ''}`} onClick={() => toggleSort(c)}>{c.label}{sort.by === c.key ? (sort.dir === 'asc' ? ' ▲' : ' ▼') : ''}</th>)}
          </tr></thead><tbody className={loading ? 'opacity-50' : ''}>
              {data.items.map((r, i) => (
                <tr key={r.id ?? i} className={cfg.rowClick === false ? '' : 'cursor-pointer hover:bg-brand-50'} onClick={() => cfg.rowClick !== false && setView(r)}>
                  {cfg.columns.map((c) => <td key={c.key + c.label} className={`td ${right(c) ? 'text-right tabular-nums' : ''}`}>{cell(c, r)}</td>)}
                </tr>))}
            </tbody></table></div>
        )}
        <Pagination page={page} pageSize={pageSize} total={data.total} onPage={setPage} />
      </div>

      {view && (
        <Modal wide title={`${cfg.title.replace(/s$/, '')} ${view.number ?? view.code ?? view.name ?? view.username ?? ''}`} onClose={() => setView(null)} footer={<>
          {rowActions(view).map((a) => <button key={a.label} className={`btn ${a.tone === 'danger' ? 'btn-danger' : a.tone === 'default' ? '' : 'btn-primary'}`} onClick={() => click(a, view)}>{a.label}</button>)}
          {canManage && cfg.fields && cfg.canEdit?.(view) ? <button className="btn" onClick={() => { setForm(edit(view)); setView(null); }}>Edit</button> : null}
          {canManage && cfg.canDelete && (typeof cfg.canDelete === 'function' ? cfg.canDelete(view) : true) && <button className="btn btn-danger" onClick={() => remove(view)}>Delete</button>}
          <button className="btn" onClick={() => setView(null)}>Close</button></>}>
          {cfg.detail ? cfg.detail(view) : (
            <>
              <dl className="grid gap-x-6 gap-y-2 sm:grid-cols-3">{cfg.columns.map((c) => <div key={c.key + c.label}><dt className="text-xs text-slate-500">{c.label}</dt><dd className="text-sm font-medium">{cell(c, view) || '—'}</dd></div>)}
                {view.notes && <div className="sm:col-span-3"><dt className="text-xs text-slate-500">Notes</dt><dd className="text-sm">{view.notes}</dd></div>}</dl>
              {lc && lines.length > 0 && (
                <div className="mt-4 overflow-x-auto rounded-md border border-slate-200"><table className="w-full"><thead><tr>
                  <th className="th">Product</th><th className="th text-right">Qty</th>{lines.some((l) => l.unitPrice) && <><th className="th text-right">Unit price</th><th className="th text-right">Tax %</th><th className="th text-right">Line total</th></>}
                  {lines.some((l) => l.doneQuantity) && <th className="th text-right">{lc.doneLabel ?? 'Done'}</th>}{lines.some((l) => l.rejectedQuantity) && <th className="th text-right">Rejected</th>}</tr></thead>
                  <tbody>{lines.map((l, i) => <tr key={i}><td className="td">{l.sku ? `${l.sku} · ` : ''}{l.productName ?? l.componentName}</td><td className="td text-right">{num(l.quantity)}</td>
                    {lines.some((x) => x.unitPrice) && <><td className="td text-right">{money(l.unitPrice)}</td><td className="td text-right">{num(l.taxRate)}</td><td className="td text-right">{money(l.lineTotal)}</td></>}
                    {lines.some((x) => x.doneQuantity) && <td className="td text-right">{num(l.doneQuantity)}</td>}{lines.some((x) => x.rejectedQuantity) && <td className="td text-right">{num(l.rejectedQuantity)}</td>}</tr>)}</tbody></table></div>
              )}
              {view.total !== undefined && view.subtotal !== undefined && <div className="mt-3 text-right text-sm">Subtotal {money(view.subtotal)} · Tax {money(view.taxAmount)} · <b>Total {money(view.total)}</b></div>}
            </>)}
        </Modal>
      )}

      {form && cfg.fields && (
        <Modal wide={!!cfg.lines || cfg.wide} title={`${form._id ? 'Edit' : 'New'} ${cfg.title.replace(/s$/, '')}`} onClose={() => setForm(null)} footer={<><button className="btn" onClick={() => setForm(null)}>Cancel</button><button className="btn btn-primary" disabled={saving} onClick={save}>{saving ? 'Saving…' : 'Save'}</button></>}>
          <div className="grid gap-3 sm:grid-cols-2">{cfg.fields.map((f) => <FieldInput key={f.name} f={f} form={form} editing={!!form._id} set={(n, v) => setField(n, v)} />)}</div>
          {cfg.lines && cfg.lines.cols.length > 0 && <LineEditor cfg={cfg.lines} lines={form[linesKey]} onChange={(l) => setForm((s: any) => ({ ...s, [linesKey]: l }))} />}
        </Modal>
      )}
      {prompt && <PromptModal title={prompt.a.promptTitle ?? prompt.a.label} fields={prompt.a.prompt!} onClose={() => setPrompt(null)} onSubmit={(v) => run(prompt.a, prompt.row, Object.fromEntries(Object.entries(v).map(([k, x]) => [k, x === '' ? null : x])))} />}
    </div>
  );
}
