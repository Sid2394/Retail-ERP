import { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { api } from './api';
import { useAuth } from './auth';
import { menu } from './menu';

export default function Layout() {
  const { user, logout, can } = useAuth(); const nav = useNavigate(); const loc = useLocation();
  const [collapsed, setCollapsed] = useState(false); const [mobile, setMobile] = useState(false);
  const [open, setOpen] = useState<Record<string, boolean>>({}); const [notes, setNotes] = useState<any[]>([]); const [showN, setShowN] = useState(false);
  useEffect(() => { api.get('/notifications').then((r) => setNotes(r.data)).catch(() => {}); }, [loc.pathname]);
  useEffect(() => { setMobile(false); }, [loc.pathname]);
  const unread = notes.filter((n) => !n.isRead).length;
  const groups = menu.filter((g) => can(g.perm)).map((g) => ({ ...g, items: g.items.filter((i) => can(i.perm)) })).filter((g) => g.items.length);
  const isOpen = (label: string, items: any[]) => open[label] ?? items.some((i) => loc.pathname.startsWith(i.path));

  return (
    <div className="flex min-h-screen">
      {mobile && <div className="fixed inset-0 z-30 bg-slate-900/40 lg:hidden" onClick={() => setMobile(false)} />}
      <aside className={`fixed z-40 h-full overflow-y-auto border-r border-slate-200 bg-white transition-all lg:static ${collapsed ? 'lg:w-16' : 'lg:w-64'} ${mobile ? 'w-64' : 'w-0 -translate-x-full lg:translate-x-0'}`}>
        <div className="flex h-14 items-center gap-2 border-b border-slate-200 px-4"><span className="text-xl">🏪</span>{!collapsed && <div><div className="text-sm font-bold leading-tight">Retail ERP</div><div className="text-[10px] uppercase tracking-wide text-slate-400">JAI BADRI KEDAR TRADERS</div></div>}</div>
        <nav className="p-2">
          {groups.map((g) => g.items.length === 1 && g.label === 'Dashboard' ? (
            <NavLink key={g.label} to={g.items[0].path} className={({ isActive }) => `mb-1 flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium ${isActive ? 'bg-brand-50 text-brand-700' : 'text-slate-700 hover:bg-slate-100'}`}><span>{g.icon}</span>{!collapsed && g.label}</NavLink>
          ) : (
            <div key={g.label} className="mb-1">
              <button className="flex w-full items-center gap-2 rounded-md px-3 py-2 text-left text-sm font-semibold text-slate-700 hover:bg-slate-100" onClick={() => setOpen((s) => ({ ...s, [g.label]: !isOpen(g.label, g.items) }))} title={g.label}>
                <span>{g.icon}</span>{!collapsed && <><span className="flex-1">{g.label}</span><span className="text-xs text-slate-400">{isOpen(g.label, g.items) ? '▾' : '▸'}</span></>}
              </button>
              {!collapsed && isOpen(g.label, g.items) && <div className="ml-4 border-l border-slate-200 pl-2">{g.items.map((i) => (
                <NavLink key={i.path} to={i.path} className={({ isActive }) => `block rounded-md px-3 py-1.5 text-sm ${isActive ? 'bg-brand-50 font-medium text-brand-700' : 'text-slate-600 hover:bg-slate-100'}`}>{i.label}</NavLink>))}</div>}
            </div>
          ))}
        </nav>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-14 items-center justify-between border-b border-slate-200 bg-white px-4">
          <div className="flex items-center gap-2">
            <button className="btn btn-sm hidden lg:inline-flex" onClick={() => setCollapsed(!collapsed)} title="Collapse sidebar">☰</button>
            <button className="btn btn-sm lg:hidden" onClick={() => setMobile(true)}>☰</button>
            <span className="hidden text-sm text-slate-500 sm:inline">JAI BADRI KEDAR TRADERS</span>
          </div>
          <div className="relative flex items-center gap-3">
            <button className="relative text-xl" onClick={() => setShowN(!showN)} title="Notifications">🔔{unread > 0 && <span className="absolute -right-2 -top-1 rounded-full bg-red-500 px-1.5 text-[10px] font-bold text-white">{unread}</span>}</button>
            {showN && (
              <div className="card absolute right-0 top-9 z-50 max-h-96 w-80 overflow-y-auto">
                <div className="flex items-center justify-between border-b border-slate-200 px-3 py-2 text-sm font-semibold">Notifications<button className="text-xs font-normal text-brand-600" onClick={async () => { await api.post('/notifications/read-all'); setNotes(notes.map((n) => ({ ...n, isRead: true }))); }}>Mark all read</button></div>
                {notes.length === 0 ? <div className="p-4 text-sm text-slate-500">No notifications.</div> : notes.map((n) => <div key={n.id} className={`border-b border-slate-100 px-3 py-2 text-sm ${n.isRead ? 'text-slate-400' : ''}`}>{n.message}</div>)}
              </div>)}
            <div className="text-right leading-tight"><div className="text-sm font-medium">{user?.fullName}</div><div className="text-xs text-slate-500">{user?.role}</div></div>
            <button className="btn btn-sm" onClick={() => { logout(); nav('/login'); }}>Logout</button>
          </div>
        </header>
        <main className="flex-1 p-4 lg:p-6"><Outlet /></main>
      </div>
    </div>
  );
}
