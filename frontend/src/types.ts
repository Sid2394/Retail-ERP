import type { ReactNode } from 'react';

export type Opt = { value: any; label: string };
export interface Lookup { endpoint: string; value?: string; label: (r: any) => string; filter?: (r: any) => boolean; params?: Record<string, any> }
export interface FieldDef {
  name: string; label: string; type?: 'text' | 'number' | 'date' | 'select' | 'textarea' | 'checkbox' | 'password'; options?: Opt[]; lookup?: Lookup;
  required?: boolean | 'create'; default?: any; step?: string; help?: string; disabledOnEdit?: boolean; full?: boolean;
}
export interface ColumnDef { key: string; label: string; type?: 'text' | 'money' | 'number' | 'date' | 'status' | 'bool'; render?: (r: any) => ReactNode; sortable?: boolean }
export interface LineCol { name: string; label: string; type?: 'product' | 'number' | 'text' | 'account'; step?: string; readOnly?: boolean; nameKey?: string }
export interface ActionDef { label: string; path: (r: any) => string; show: (r: any) => boolean; confirm?: string; prompt?: FieldDef[]; perm?: string; tone?: 'primary' | 'danger' | 'default'; promptTitle?: string }
export interface ResourceConfig {
  title: string; subtitle?: string; endpoint: string; view: string; manage?: string;
  columns: ColumnDef[]; fields?: FieldDef[];
  lines?: { key?: string; cols: LineCol[]; priceFrom?: 'averageCost' | 'salePrice'; fixed?: boolean; dropZero?: boolean; doneLabel?: string; newLine?: () => any };
  actions?: ActionDef[]; canEdit?: (r: any) => boolean; canDelete?: boolean | ((r: any) => boolean);
  createLabel?: string; statuses?: string[]; dates?: boolean; typeFilter?: { param: string; label: string; options: Opt[] };
  prefill?: { field: string; endpoint: string; apply: (doc: any, form: any) => any };
  mapSave?: (form: any) => any; createEndpoint?: string; rowClick?: boolean; defaultSort?: { by: string; dir: 'asc' | 'desc' };
  detail?: (row: any) => ReactNode; wide?: boolean; csvName?: string; hideSearch?: boolean;
}
