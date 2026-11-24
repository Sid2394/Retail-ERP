export interface MenuItem { label: string; path: string; perm?: string }
export interface MenuGroup { label: string; icon: string; perm?: string; items: MenuItem[] }

export const menu: MenuGroup[] = [
  { label: 'Dashboard', icon: '📊', items: [{ label: 'Dashboard', path: '/dashboard' }] },
  { label: 'Products & Inventory', icon: '📦', perm: 'inventory.view', items: [
    { label: 'Products', path: '/inventory/products' }, { label: 'Categories', path: '/inventory/categories' }, { label: 'Units', path: '/inventory/units' },
    { label: 'Stock Management', path: '/inventory/stock' }, { label: 'Inventory Reports', path: '/inventory/reports', perm: 'reports.view' }] },
  { label: 'Purchases', icon: '🛒', perm: 'procurement.view', items: [
    { label: 'Suppliers', path: '/procurement/suppliers' }, { label: 'Purchase Orders', path: '/procurement/orders' },
    { label: 'Goods Received', path: '/procurement/receipts' }, { label: 'Purchase Invoices', path: '/procurement/invoices' }, { label: 'Purchase Returns', path: '/procurement/returns' }] },
  { label: 'Retail Sales', icon: '🧾', perm: 'sales.view', items: [
    { label: 'Customers', path: '/sales/customers' }, { label: 'Sales Orders', path: '/sales/orders' },
    { label: 'Deliveries', path: '/sales/deliveries' }, { label: 'Sales Invoices', path: '/sales/invoices' }, { label: 'Sales Returns', path: '/sales/returns' }] },
  { label: 'Accounts', icon: '🏦', perm: 'finance.view', items: [
    { label: 'Accounts Payable', path: '/finance/payables' }, { label: 'Accounts Receivable', path: '/finance/receivables' },
    { label: 'Payments', path: '/finance/payments' }, { label: 'Expenses', path: '/finance/expenses' }, { label: 'Financial Reports', path: '/finance/reports' }] },
  { label: 'Reports', icon: '📈', perm: 'reports.view', items: [
    { label: 'Sales Analytics', path: '/reports/sales' }, { label: 'Purchase Analytics', path: '/reports/purchases' }, { label: 'Inventory Analytics', path: '/reports/inventory' }, { label: 'Financial Analytics', path: '/reports/financial' }] },
  { label: 'Administration', icon: '⚙️', perm: 'admin.manage', items: [
    { label: 'Users', path: '/admin/users' }, { label: 'Roles', path: '/admin/roles' }, { label: 'Permissions', path: '/admin/permissions' }, { label: 'Audit Log', path: '/admin/audit' }, { label: 'Shop Settings', path: '/admin/settings' }] },
];
