import { ReactElement } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth';
import Layout from './Layout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import ResourcePage from './components/ResourcePage';
import * as R from './pages/resources';
import { AnalyticsPage, InventoryReportsPage } from './pages/Reports';
import { ExpensesPage, FinancialReportsPage, OutstandingPage } from './pages/Finance';
import { PermissionsPage, RolesPage, SettingsPage, StockPage } from './pages/Misc';
import type { ResourceConfig } from './types';

function Guard({ perm, children }: { perm?: string; children: ReactElement }) {
  const { user, can } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  if (!can(perm)) return <div className="card m-4 p-8 text-center"><div className="text-3xl">🔒</div><h2 className="mt-2 text-lg font-semibold">Access denied</h2><p className="text-sm text-slate-500">Your role does not include the <code>{perm}</code> permission.</p></div>;
  return children;
}
const res = (cfg: ResourceConfig) => <Guard perm={cfg.view}><ResourcePage key={cfg.endpoint} cfg={cfg} /></Guard>;
const g = (perm: string, el: ReactElement) => <Guard perm={perm}>{el}</Guard>;

export default function App() {
  const { user } = useAuth();
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={user ? <Layout /> : <Navigate to="/login" replace />}>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/inventory/products" element={res(R.products)} /><Route path="/inventory/categories" element={res(R.categories)} /><Route path="/inventory/units" element={res(R.units)} />
        <Route path="/inventory/stock" element={g('inventory.view', <StockPage />)} />
        <Route path="/inventory/reports" element={g('reports.view', <InventoryReportsPage />)} />
        <Route path="/procurement/suppliers" element={res(R.suppliers)} /><Route path="/procurement/orders" element={res(R.purchaseOrders)} />
        <Route path="/procurement/receipts" element={res(R.goodsReceipts)} /><Route path="/procurement/invoices" element={res(R.purchaseInvoices)} /><Route path="/procurement/returns" element={res(R.purchaseReturns)} />
        <Route path="/sales/customers" element={res(R.customers)} /><Route path="/sales/quotations" element={res(R.quotations)} /><Route path="/sales/orders" element={res(R.salesOrders)} />
        <Route path="/sales/deliveries" element={res(R.deliveries)} /><Route path="/sales/invoices" element={res(R.salesInvoices)} /><Route path="/sales/returns" element={res(R.salesReturns)} />
        <Route path="/finance/accounts" element={res(R.accounts)} /><Route path="/finance/payables" element={g('finance.view', <OutstandingPage kind="payables" />)} /><Route path="/finance/receivables" element={g('finance.view', <OutstandingPage kind="receivables" />)} />
        <Route path="/finance/payments" element={res(R.payments)} /><Route path="/finance/expenses" element={g('finance.view', <ExpensesPage />)} /><Route path="/finance/journals" element={res(R.journals)} />
        <Route path="/finance/reports" element={g('finance.view', <FinancialReportsPage />)} />
        {['sales', 'purchases', 'inventory', 'financial'].map((k) => <Route key={k} path={`/reports/${k}`} element={g('reports.view', <AnalyticsPage kind={k} />)} />)}
        <Route path="/admin/users" element={res(R.users)} /><Route path="/admin/roles" element={g('admin.manage', <RolesPage />)} /><Route path="/admin/permissions" element={g('admin.manage', <PermissionsPage />)} />
        <Route path="/admin/audit" element={res(R.auditLogs)} /><Route path="/admin/settings" element={g('admin.manage', <SettingsPage />)} />
        <Route path="*" element={<div className="p-8 text-center text-slate-500">Page not found.</div>} />
      </Route>
    </Routes>
  );
}
