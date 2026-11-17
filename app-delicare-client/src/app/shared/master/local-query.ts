// Client-side search / filter / sort / paging for the FE-2 master-data lists (FE-2).
// Why: several legacy GetAll actions cannot serve the mockup list – UnitMeasure / Supplier SPs return only
// Id + Name, many services ignore Status, and the child tables (CustomerContact, CustomerDeliveryLocation,
// Contract, SupplierMaterial, InventoryPolicy, StorageZone) bind the parent filter as a non-nullable Guid, so a
// call without that filter returns 0 rows. Master data is small, so the screens load <Entity>/GetAllData once
// and query it here. Switch back to server paging once the GetAll actions are fixed (see PROGRESS FE-2).

export interface LocalQueryOptions {
  searchText?: string | null;
  searchFields: string[];
  // exact-match filters, empty values are ignored
  filters?: Record<string, any>;
  sortField?: string;
  sortOrder?: string;
  page: number;
  pageSize: number;
}

// Lower-case, no Vietnamese diacritics – "Rau cải" matches "rau cai"
export const normalizeText = (value: any): string =>
  String(value ?? '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .trim();

export const isEmptyFilter = (value: any): boolean =>
  value === null || value === undefined || value === '';

const compareValues = (a: any, b: any): number => {
  const emptyA = a === null || a === undefined || a === '';
  const emptyB = b === null || b === undefined || b === '';
  if (emptyA || emptyB) {
    return emptyA === emptyB ? 0 : emptyA ? 1 : -1; // empty values last
  }
  if (typeof a === 'number' && typeof b === 'number') {
    return a - b;
  }
  if (typeof a === 'boolean' && typeof b === 'boolean') {
    return a === b ? 0 : a ? -1 : 1;
  }
  return String(a).localeCompare(String(b), 'vi', { numeric: true, sensitivity: 'base' });
};

export function localQuery(rows: any[], options: LocalQueryOptions): { listData: any[]; total: number } {
  const text = normalizeText(options.searchText);
  const filters = Object.entries(options.filters ?? {}).filter(([, v]) => !isEmptyFilter(v));
  let result = rows.filter(
    (row) =>
      (!text || options.searchFields.some((f) => normalizeText(row?.[f]).includes(text))) &&
      filters.every(([key, value]) =>
        typeof value === 'boolean'
          ? !!row?.[key] === value
          : String(row?.[key] ?? '').toLowerCase() === String(value).toLowerCase()
      )
  );
  if (options.sortField && options.sortOrder) {
    const field = options.sortField;
    const sign = options.sortOrder === 'descend' ? -1 : 1;
    result = [...result].sort((a, b) => sign * compareValues(a?.[field], b?.[field]));
  }
  const total = result.length;
  const start = (options.page - 1) * options.pageSize;
  const listData = result
    .slice(start, start + options.pageSize)
    .map((row, i) => ({ ...row, stt: start + i + 1 }));
  return { listData, total };
}

// id -> label lookup for foreign-key columns
export const labelMap = (list: any[], label: (x: any) => string = (x) => x?.name ?? ''): Map<string, string> =>
  new Map((list ?? []).map((x) => [String(x?.id ?? '').toLowerCase(), label(x)]));

export const labelFrom = (map: Map<string, string>, id: any): string =>
  id ? map.get(String(id).toLowerCase()) ?? '' : '';
