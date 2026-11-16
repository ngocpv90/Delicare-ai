// Code lists of the FE-2 master-data screens that are not in api/MetaData (no status set in STATUS_CATALOG,
// no ReferenceLists entry). Values follow the mockup demo data / seeds – PROPOSED – BA/PO CONFIRM.
// Customer.Status and Contract.Status come from api/MetaData/GetStatuses; CustomerType, SupplierType,
// WarehouseType, StorageType, MealType from api/MetaData/GetReferenceLists.
export interface CodeLabel {
  code: string;
  label: string;
}

export const ACTIVE_STATUSES: CodeLabel[] = [
  { code: 'Active', label: 'Đang hoạt động' },
  { code: 'Inactive', label: 'Ngừng hoạt động' },
];

export const VEHICLE_STATUSES: CodeLabel[] = [
  { code: 'Active', label: 'Sẵn sàng' },
  { code: 'Maintenance', label: 'Bảo dưỡng' },
  { code: 'Inactive', label: 'Ngừng sử dụng' },
];

export const UOM_TYPES: CodeLabel[] = [
  { code: 'WEIGHT', label: 'Khối lượng' },
  { code: 'VOLUME', label: 'Thể tích' },
  { code: 'COUNT', label: 'Đếm (cái, hộp, suất…)' },
  { code: 'LENGTH', label: 'Chiều dài' },
];

export const LOCATION_TYPES: CodeLabel[] = [
  { code: 'RACK', label: 'Kệ' },
  { code: 'SHELF', label: 'Ngăn / tầng kệ' },
  { code: 'PALLET', label: 'Pallet' },
  { code: 'FLOOR', label: 'Sàn' },
  { code: 'FREEZER', label: 'Tủ đông / tủ mát' },
];

export const YES_NO: CodeLabel[] = [
  { code: 'true', label: 'Có' },
  { code: 'false', label: 'Không' },
];

export const labelOf = (list: CodeLabel[], code?: string | null): string =>
  list.find((x) => x.code.toLowerCase() === (code ?? '').toLowerCase())?.label ?? code ?? '';

// "08:00:00" | "08:00" -> "08:00" (TimeSpan from the API)
export const timeText = (value?: string | null): string => (value ? String(value).substring(0, 5) : '');

// "08:00" -> "08:00:00" for TimeSpan form binding ('' stays '')
export const toTimeParam = (value?: string | null): string => {
  const v = (value ?? '').trim();
  return /^\d{1,2}:\d{2}$/.test(v) ? `${v}:00` : v;
};

// Numbers from a form field (number | null) -> form value ('' for empty)
export const toNumberParam = (value: any): string =>
  value === null || value === undefined || value === '' || isNaN(Number(value)) ? '' : String(value);
