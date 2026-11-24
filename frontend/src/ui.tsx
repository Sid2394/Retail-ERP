import { createContext, ReactNode, useCallback, useContext, useEffect, useState } from 'react';
import { lookup } from './api';

// ---------- toasts ----------
type Toast = { id: number; kind: 'success' | 'error' | 'info'; text: string };
const ToastCtx = createContext<{ toast: (kind: Toast['kind'], text: string) => void }>({ toast: () => {} });
export const useToast = () => useContext(ToastCtx);
export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<Toast[]>([]);
  const toast = useCallback((kind: Toast['kind'], text: string) => {
    const id = Date.now() + Math.random(); setItems((s) => [...s, { id, kind, text }]); setTimeout(() => setItems((s) => s.filter((t) => t.id !== id)), 4500);
  }, []);
  return (
    <ToastCtx.Provider value={{ toast }}>
      {children}
      <div className="fixed right-4 top-4 z-[100] flex w-80 flex-col gap-2">
        {items.map((t) => (
          <div key={t.id} className={`rounded-md border px-3 py-2 text-sm shadow-lg ${t.kind === 'success' ? 'border-green-200 bg-green-50 text-green-800' : t.kind === 'error' ? 'border-red-200 bg-red-50 text-red-800' : 'border-blue-200 bg-blue-50 text-blue-800'}`}>{t.text}</div>
        ))}
      </div>
    </ToastCtx.Provider>
  );
}

// ---------- primitives ----------
export const Spinner = ({ label = 'Loading…' }: { label?: string }) => (
  <div className="flex items-center justify-center gap-2 p-8 text-sm text-slate-500"><span className="h-4 w-4 animate-spin rounded-full border-2 border-brand-500 border-t-transparent" />{label}</div>
);
export const Empty = ({ text = 'No records found.', hint }: { text?: string; hint?: string }) => (
  <div className="p-10 text-center"><div className="text-3xl">📭</div><div className="mt-2 text-sm font-medium text-slate-600">{text}</div>{hint && <div className="text-xs text-slate-400">{hint}</div>}</div>
);
export const ErrorBox = ({ text, onRetry }: { text: string; onRetry?: () => void }) => (
  <div className="m-4 rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700"><b>Something went wrong.</b> {text} {onRetry && <button className="ml-2 underline" onClick={onRetry}>Retry</button>}</div>
);

const tone: Record<string, string> = {
  Draft: 'bg-slate-100 text-slate-700', Submitted: 'bg-blue-100 text-blue-700', Approved: 'bg-green-100 text-green-700', Posted: 'bg-green-100 text-green-700', Completed: 'bg-green-100 text-green-700',
  Paid: 'bg-green-100 text-green-700', Received: 'bg-green-100 text-green-700', Delivered: 'bg-green-100 text-green-700', Passed: 'bg-green-100 text-green-700', Accepted: 'bg-green-100 text-green-700', Converted: 'bg-indigo-100 text-indigo-700',
  Confirmed: 'bg-blue-100 text-blue-700', Released: 'bg-blue-100 text-blue-700', Sent: 'bg-blue-100 text-blue-700', InProgress: 'bg-amber-100 text-amber-700', Planned: 'bg-slate-100 text-slate-700',
  PartiallyReceived: 'bg-amber-100 text-amber-700', PartiallyDelivered: 'bg-amber-100 text-amber-700', PartiallyPaid: 'bg-amber-100 text-amber-700', Unpaid: 'bg-orange-100 text-orange-700', Pending: 'bg-amber-100 text-amber-700',
  Rejected: 'bg-red-100 text-red-700', Cancelled: 'bg-red-100 text-red-700', Failed: 'bg-red-100 text-red-700', Reversed: 'bg-red-100 text-red-700', Closed: 'bg-slate-200 text-slate-600', Low: 'bg-red-100 text-red-700', OK: 'bg-green-100 text-green-700',
};
export const StatusBadge = ({ value }: { value?: string }) => value ? <span className={`inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${tone[value] ?? 'bg-slate-100 text-slate-700'}`}>{value.replace(/([a-z])([A-Z])/g, '$1 $2')}</span> : null;

export function Modal({ title, onClose, children, wide, footer }: { title: string; onClose: () => void; children: ReactNode; wide?: boolean; footer?: ReactNode }) {
  useEffect(() => { const h = (e: KeyboardEvent) => e.key === 'Escape' && onClose(); window.addEventListener('keydown', h); return () => window.removeEventListener('keydown', h); }, [onClose]);
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-slate-900/40 p-4 pt-12">
      <div className={`card w-full ${wide ? 'max-w-5xl' : 'max-w-xl'} shadow-xl`}>
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-3"><h3 className="text-base font-semibold">{title}</h3><button className="text-xl leading-none text-slate-400 hover:text-slate-700" onClick={onClose}>×</button></div>
        <div className="p-5">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-slate-200 bg-slate-50 px-5 py-3">{footer}</div>}
      </div>
    </div>
  );
}

export function Pagination({ page, pageSize, total, onPage }: { page: number; pageSize: number; total: number; onPage: (p: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="flex items-center justify-between border-t border-slate-200 px-3 py-2 text-xs text-slate-500">
      <span>{total === 0 ? '0 records' : `${(page - 1) * pageSize + 1}–${Math.min(page * pageSize, total)} of ${total}`}</span>
      <div className="flex items-center gap-1">
        <button className="btn btn-sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>Prev</button>
        <span className="px-2">Page {page} / {pages}</span>
        <button className="btn btn-sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</button>
      </div>
    </div>
  );
}

export function PageHeader({ title, subtitle, children }: { title: string; subtitle?: string; children?: ReactNode }) {
  return <div className="mb-4 flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-xl font-semibold text-slate-900">{title}</h1>{subtitle && <p className="text-sm text-slate-500">{subtitle}</p>}</div><div className="flex flex-wrap items-center gap-2">{children}</div></div>;
}

/** Select populated from an API list. */
export function LookupSelect({ endpoint, value, onChange, label, valueKey = 'id', filter, params, disabled, placeholder = 'Select…' }: {
  endpoint: string; value: any; onChange: (v: any, row?: any) => void; label: (r: any) => string; valueKey?: string; filter?: (r: any) => boolean; params?: Record<string, any>; disabled?: boolean; placeholder?: string;
}) {
  const [rows, setRows] = useState<any[]>([]);
  useEffect(() => { let on = true; lookup(endpoint, params).then((r) => on && setRows(r)).catch(() => {}); return () => { on = false; }; }, [endpoint, JSON.stringify(params)]);
  const list = filter ? rows.filter(filter) : rows;
  return (
    <select className="input" disabled={disabled} value={value ?? ''} onChange={(e) => { const v = e.target.value === '' ? null : (isNaN(Number(e.target.value)) ? e.target.value : Number(e.target.value)); onChange(v, rows.find((r) => String(r[valueKey]) === e.target.value)); }}>
      <option value="">{placeholder}</option>
      {list.map((r) => <option key={r[valueKey]} value={r[valueKey]}>{label(r)}</option>)}
    </select>
  );
}
