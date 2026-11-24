import axios from 'axios';

export const api = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5080/api' });
api.interceptors.request.use((c) => { const t = localStorage.getItem('token'); if (t) c.headers.Authorization = `Bearer ${t}`; return c; });
api.interceptors.response.use((r) => r, (e) => {
  if (e.response?.status === 401 && !location.pathname.startsWith('/login')) { localStorage.removeItem('token'); localStorage.removeItem('user'); location.href = '/login'; }
  return Promise.reject(e);
});

export const errMsg = (e: any): string => e?.response?.data?.message ?? e?.response?.data?.title ?? (e?.response?.status === 403 ? 'You do not have permission for this action.' : e?.message) ?? 'Request failed';

const nf = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
export const money = (n: any) => (n === null || n === undefined || n === '' ? '' : '₹' + nf.format(Number(n)));
export const num = (n: any) => (n === null || n === undefined || n === '' ? '' : new Intl.NumberFormat('en-IN', { maximumFractionDigits: 4 }).format(Number(n)));
export const fmtDate = (d: any) => (d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '');
export const isoDate = (d?: any) => (d ? String(d).substring(0, 10) : '');
export const today = () => new Date().toISOString().substring(0, 10);

export function downloadCsv(name: string, rows: any[], cols: { key: string; label: string }[]) {
  const esc = (v: any) => { const s = v === null || v === undefined ? '' : String(v); return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s; };
  const csv = [cols.map((c) => esc(c.label)).join(','), ...rows.map((r) => cols.map((c) => esc(r[c.key])).join(','))].join('\n');
  const url = URL.createObjectURL(new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8;' }));
  const a = document.createElement('a'); a.href = url; a.download = `${name}-${today()}.csv`; a.click(); URL.revokeObjectURL(url);
}

const cache = new Map<string, Promise<any[]>>();
export function lookup(endpoint: string, params?: Record<string, any>): Promise<any[]> {
  const key = endpoint + JSON.stringify(params ?? {});
  if (!cache.has(key)) cache.set(key, api.get(endpoint, { params: { pageSize: 1000, ...params } }).then((r) => r.data.items ?? r.data).catch((e) => { cache.delete(key); throw e; }));
  return cache.get(key)!;
}
export const clearLookups = () => cache.clear();
