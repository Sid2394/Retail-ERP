import { useEffect, useState } from 'react';
import { api, errMsg, fmtDate, money, today } from '../api';
import { useAuth } from '../auth';
import { Empty, ErrorBox, LookupSelect, Modal, PageHeader, Spinner, StatusBadge, useToast } from '../ui';
import { Tabs } from '../components/ReportPage';
import ReportPage from '../components/ReportPage';
import ResourcePage from '../components/ResourcePage';
import { defs } from './Reports';
import { expenses, incomes } from './resources';

/** Accounts payable / receivable: outstanding invoices with a Pay / Receive action. */
export function OutstandingPage({ kind }: { kind: 'payables' | 'receivables' }) {
  const { can } = useAuth(); const { toast } = useToast(); const pay = kind === 'payables';
  const [pk, setPk] = useState(0);
  const [pending, setPending] = useState<{ row: any; form: any } | null>(null); const [busy, setBusy] = useState(false);
  const def = { ...defs[kind], id: kind, title: pay ? 'Outstanding supplier invoices' : 'Outstanding customer invoices', columns: [...defs[kind].columns, { key: 'action', label: '', render: (r: any) => can('finance.manage') ? <button className="btn btn-sm btn-primary" onClick={() => setPending({ row: r, form: { amount: r.outstanding, method: 'Bank', reference: '', date: today(), key: crypto.randomUUID() } })}>{pay ? 'Pay' : 'Receive'}</button> : null }] };
  const submit = async () => {
    if (!pending) return; const { row, form } = pending; setBusy(true);
    try { await api.post('/payments', { type: pay ? 'SupplierPayment' : 'CustomerReceipt', invoiceId: row.id, amount: Number(form.amount), method: form.method, reference: form.reference || null, date: form.date, idempotencyKey: form.key }); toast('success', pay ? 'Payment recorded.' : 'Receipt recorded.'); setPending(null); setPk((x) => x + 1); }
    catch (e) { toast('error', errMsg(e)); } finally { setBusy(false); }
  };
  return (<div><PageHeader title={pay ? 'Accounts Payable' : 'Accounts Receivable'} subtitle={pay ? 'Supplier invoices awaiting payment' : 'Customer invoices awaiting collection'} />
    <ReportPage key={pk} def={def} embedded />
    {pending && <Modal title={`${pay ? 'Pay' : 'Receive payment for'} ${pending.row.number}`} onClose={() => setPending(null)} footer={<><button className="btn" onClick={() => setPending(null)}>Cancel</button><button className="btn btn-primary" disabled={busy} onClick={submit}>{busy ? 'Saving…' : 'Confirm'}</button></>}>
      <p className="mb-3 text-sm text-slate-600">{pending.row.party} · outstanding <b>{money(pending.row.outstanding)}</b></p>
      <div className="grid gap-3 sm:grid-cols-2">
        <div><label className="label">Amount</label><input className="input" type="number" step="any" value={pending.form.amount} onChange={(e) => setPending({ ...pending, form: { ...pending.form, amount: e.target.value } })} /></div>
        <div><label className="label">Method</label><select className="input" value={pending.form.method} onChange={(e) => setPending({ ...pending, form: { ...pending.form, method: e.target.value } })}><option>Bank</option><option>Cash</option></select></div>
        <div><label className="label">Date</label><input className="input" type="date" value={pending.form.date} onChange={(e) => setPending({ ...pending, form: { ...pending.form, date: e.target.value } })} /></div>
        <div><label className="label">Reference</label><input className="input" value={pending.form.reference} onChange={(e) => setPending({ ...pending, form: { ...pending.form, reference: e.target.value } })} /></div>
      </div></Modal>}
  </div>);
}

export const ExpensesPage = () => <Tabs tabs={[{ label: 'Expenses', node: <ResourcePage cfg={expenses} /> }, { label: 'Other income', node: <ResourcePage cfg={incomes} /> }]} />;

const Section = ({ title, rows, total }: { title: string; rows: any[]; total?: number }) => (<>
  <tr><td colSpan={3} className="bg-slate-50 px-3 py-1.5 text-xs font-bold uppercase text-slate-500">{title}</td></tr>
  {rows.map((r) => <tr key={r.code}><td className="td w-24 text-slate-500">{r.code}</td><td className="td">{r.name}</td><td className="td text-right">{money(r.amount)}</td></tr>)}
  {rows.length === 0 && <tr><td colSpan={3} className="td text-slate-400">—</td></tr>}
  {total !== undefined && <tr><td className="td" /><td className="td font-semibold">Total {title.toLowerCase()}</td><td className="td text-right font-semibold">{money(total)}</td></tr>}
</>);

export function FinancialReportsPage() {
  const [tab, setTab] = useState('tb'); const [asOf, setAsOf] = useState(today()); const [from, setFrom] = useState(''); const [to, setTo] = useState(today()); const [acc, setAcc] = useState<number | null>(null);
  const [data, setData] = useState<any>(null); const [err, setErr] = useState(''); const [loading, setLoading] = useState(false);
  const run = async () => {
    setErr(''); setData(null); setLoading(true);
    try {
      const p = tab === 'tb' ? ['/finance/trial-balance', { asOf }] : tab === 'bs' ? ['/finance/balance-sheet', { asOf }] : tab === 'pl' ? ['/finance/profit-loss', { from: from || undefined, to: to || undefined }] : ['/finance/general-ledger', { accountId: acc, from: from || undefined, to: to || undefined }];
      if (tab === 'gl' && !acc) { setLoading(false); return; }
      const r = await api.get(p[0] as string, { params: p[1] }); setData(r.data);
    } catch (e) { setErr(errMsg(e)); } finally { setLoading(false); }
  };
  useEffect(() => { run(); }, [tab]);
  const tabs = [['tb', 'Trial Balance'], ['gl', 'General Ledger'], ['pl', 'Profit & Loss'], ['bs', 'Balance Sheet']];
  const asOfTab = tab === 'tb' || tab === 'bs';
  return (<div><PageHeader title="Financial Reports" subtitle="Generated from posted double-entry journals" />
    <div className="mb-3 flex gap-1 border-b border-slate-200">{tabs.map(([k, l]) => <button key={k} onClick={() => setTab(k)} className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium ${tab === k ? 'border-brand-500 text-brand-700' : 'border-transparent text-slate-500'}`}>{l}</button>)}</div>
    <div className="card mb-3 flex flex-wrap items-end gap-3 p-3">
      {tab === 'gl' && <div className="w-72"><label className="label">Account</label><LookupSelect endpoint="/accounts" label={(r) => `${r.code} · ${r.name}`} value={acc} onChange={setAcc} /></div>}
      {asOfTab ? <div><label className="label">As of</label><input className="input" type="date" value={asOf} onChange={(e) => setAsOf(e.target.value)} /></div> : <><div><label className="label">From</label><input className="input" type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></div><div><label className="label">To</label><input className="input" type="date" value={to} onChange={(e) => setTo(e.target.value)} /></div></>}
      <button className="btn btn-primary" onClick={run}>Run report</button>
    </div>
    {loading ? <Spinner /> : err ? <ErrorBox text={err} /> : !data ? <Empty text={tab === 'gl' ? 'Select an account to view its ledger.' : 'Run the report.'} /> : (
      <div className="card overflow-x-auto">
        {tab === 'tb' && <table className="w-full"><thead><tr><th className="th">Code</th><th className="th">Account</th><th className="th">Type</th><th className="th text-right">Debit</th><th className="th text-right">Credit</th></tr></thead><tbody>
          {data.rows.map((r: any) => <tr key={r.code}><td className="td">{r.code}</td><td className="td">{r.name}</td><td className="td">{r.type}</td><td className="td text-right">{r.debit ? money(r.debit) : ''}</td><td className="td text-right">{r.credit ? money(r.credit) : ''}</td></tr>)}
          <tr><td className="td font-bold" colSpan={3}>Total {data.balanced ? <span className="ml-2 text-xs font-normal text-green-600">✓ balanced</span> : <span className="ml-2 text-xs font-normal text-red-600">✗ not balanced</span>}</td><td className="td text-right font-bold">{money(data.totalDebit)}</td><td className="td text-right font-bold">{money(data.totalCredit)}</td></tr></tbody></table>}
        {tab === 'gl' && <><div className="border-b border-slate-200 px-4 py-2 text-sm"><b>{data.account.code} · {data.account.name}</b> ({data.account.type}) — opening balance {money(data.opening)}</div><table className="w-full"><thead><tr><th className="th">Date</th><th className="th">Journal</th><th className="th">Description</th><th className="th text-right">Debit</th><th className="th text-right">Credit</th><th className="th text-right">Balance</th></tr></thead><tbody>
          {data.rows.map((r: any, i: number) => <tr key={i}><td className="td">{fmtDate(r.date)}</td><td className="td">{r.number}</td><td className="td">{r.description}</td><td className="td text-right">{r.debit ? money(r.debit) : ''}</td><td className="td text-right">{r.credit ? money(r.credit) : ''}</td><td className="td text-right">{money(r.balance)}</td></tr>)}
          {data.rows.length === 0 && <tr><td className="td text-slate-400" colSpan={6}>No postings in this period.</td></tr>}<tr><td className="td font-bold" colSpan={5}>Closing balance</td><td className="td text-right font-bold">{money(data.closing)}</td></tr></tbody></table></>}
        {tab === 'pl' && <table className="w-full"><tbody><Section title="Revenue" rows={data.revenue} total={data.totalRevenue} /><Section title="Cost of sales" rows={data.costOfSales} total={data.totalCostOfSales} />
          <tr><td className="td" /><td className="td font-bold">Gross profit</td><td className="td text-right font-bold">{money(data.grossProfit)}</td></tr><Section title="Operating expenses" rows={data.operatingExpenses} total={data.totalOperatingExpenses} />
          <tr className="bg-brand-50"><td className="td" /><td className="td text-base font-bold">Net profit</td><td className={`td text-right text-base font-bold ${data.netProfit >= 0 ? 'text-green-700' : 'text-red-600'}`}>{money(data.netProfit)}</td></tr></tbody></table>}
        {tab === 'bs' && <table className="w-full"><tbody><Section title="Assets" rows={data.assets} total={data.totalAssets} /><Section title="Liabilities" rows={data.liabilities} total={data.totalLiabilities} />
          <Section title="Equity" rows={[...data.equity, { code: '', name: 'Current period earnings', amount: data.currentEarnings }]} total={data.totalEquity} />
          <tr className="bg-brand-50"><td className="td" colSpan={3}>{data.balanced ? <span className="text-green-700">✓ Assets = Liabilities + Equity</span> : <span className="text-red-600">✗ Out of balance</span>}</td></tr></tbody></table>}
      </div>)}
  </div>);
}
