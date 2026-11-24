import { ReactNode, useCallback, useEffect, useState } from 'react';
import { api, downloadCsv, errMsg, money, num } from '../api';
import { cell } from './ResourcePage';
import { Empty, ErrorBox, LookupSelect, Pagination, Spinner, useToast } from '../ui';
import type { ColumnDef, Lookup } from '../types';

export interface ReportDef { id: string; title: string; endpoint: string; columns: ColumnDef[]; dates?: boolean; party?: Lookup; partyLabel?: string; status?: string[] }

export default function ReportPage({ def, embedded }: { def: ReportDef; embedded?: boolean }) {
  const { toast } = useToast();
  const [data, setData] = useState<any>({ items: [], total: 0, summary: {} }); const [loading, setLoading] = useState(true); const [err, setErr] = useState('');
  const [page, setPage] = useState(1); const [search, setSearch] = useState(''); const [q, setQ] = useState(''); const [from, setFrom] = useState(''); const [to, setTo] = useState('');
  const [party, setParty] = useState<any>(null); const [sort, setSort] = useState({ by: '', dir: 'asc' }); const pageSize = 15;
  useEffect(() => { const t = setTimeout(() => { setQ(search); setPage(1); }, 300); return () => clearTimeout(t); }, [search]);
  const params = (size: number, p: number) => ({ page: p, pageSize: size, search: q || undefined, from: from || undefined, to: to || undefined, partyId: party || undefined, sortBy: sort.by || undefined, sortDir: sort.dir });
  const load = useCallback(async () => {
    setLoading(true); setErr('');
    try { const { data } = await api.get(def.endpoint, { params: params(pageSize, page) }); setData(data); } catch (e) { setErr(errMsg(e)); } finally { setLoading(false); }
  }, [def.endpoint, page, q, from, to, party, sort.by, sort.dir]);
  useEffect(() => { load(); }, [load]);
  const exportCsv = async () => { try { const { data } = await api.get(def.endpoint, { params: params(100000, 1) }); downloadCsv(def.id, data.items, def.columns.map((c) => ({ key: c.key, label: c.label }))); } catch (e) { toast('error', errMsg(e)); } };
  const right = (c: ColumnDef) => c.type === 'money' || c.type === 'number';
  return (
    <div className="card">
      <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 p-3">
        {!embedded && <h2 className="mr-2 text-base font-semibold">{def.title}</h2>}
        <input className="input max-w-[14rem]" placeholder="Search…" value={search} onChange={(e) => setSearch(e.target.value)} />
        {def.dates && <><input type="date" className="input w-40" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1); }} /><span className="text-xs text-slate-400">to</span><input type="date" className="input w-40" value={to} onChange={(e) => { setTo(e.target.value); setPage(1); }} /></>}
        {def.party && <div className="w-56"><LookupSelect endpoint={def.party.endpoint} label={def.party.label} value={party} placeholder={def.partyLabel ?? 'All'} onChange={(v) => { setParty(v); setPage(1); }} /></div>}
        <div className="flex-1" /><button className="btn" onClick={exportCsv}>⬇ Export CSV</button>
      </div>
      {loading && data.items.length === 0 ? <Spinner /> : err ? <ErrorBox text={err} onRetry={load} /> : data.items.length === 0 ? <Empty text="No data for the selected filters." /> : (
        <div className="overflow-x-auto"><table className="w-full"><thead><tr>{def.columns.map((c) => <th key={c.key} className={`th cursor-pointer select-none hover:text-slate-800 ${right(c) ? 'text-right' : ''}`} onClick={() => setSort((s) => ({ by: c.key, dir: s.by === c.key && s.dir === 'asc' ? 'desc' : 'asc' }))}>{c.label}{sort.by === c.key ? (sort.dir === 'asc' ? ' ▲' : ' ▼') : ''}</th>)}</tr></thead>
          <tbody className={loading ? 'opacity-50' : ''}>{data.items.map((r: any, i: number) => <tr key={i}>{def.columns.map((c) => <td key={c.key} className={`td ${right(c) ? 'text-right tabular-nums' : ''}`}>{cell(c, r)}</td>)}</tr>)}</tbody>
          <tfoot><tr>{def.columns.map((c, i) => <td key={c.key} className={`bg-slate-50 px-3 py-2 text-sm font-semibold ${right(c) ? 'text-right' : ''}`}>{i === 0 ? 'Totals (all rows)' : right(c) && data.summary?.[c.key] !== undefined ? (c.type === 'money' ? money(data.summary[c.key]) : num(data.summary[c.key])) : ''}</td>)}</tr></tfoot></table></div>
      )}
      <Pagination page={page} pageSize={pageSize} total={data.total} onPage={setPage} />
    </div>
  );
}

export function ReportTabs({ defs }: { defs: ReportDef[] }) {
  const [i, setI] = useState(0);
  return (<div><div className="mb-3 flex flex-wrap gap-1 border-b border-slate-200">{defs.map((d, idx) => <button key={d.id} onClick={() => setI(idx)} className={`-mb-px border-b-2 px-3 py-2 text-sm font-medium ${i === idx ? 'border-brand-500 text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-800'}`}>{d.title}</button>)}</div><ReportPage key={defs[i].id} def={defs[i]} embedded /></div>);
}
export const Tabs = ({ tabs }: { tabs: { label: string; node: ReactNode }[] }) => {
  const [i, setI] = useState(0);
  return (<div><div className="mb-4 flex gap-1 border-b border-slate-200">{tabs.map((t, idx) => <button key={t.label} onClick={() => setI(idx)} className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium ${i === idx ? 'border-brand-500 text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-800'}`}>{t.label}</button>)}</div><div key={tabs[i].label}>{tabs[i].node}</div></div>);
};
