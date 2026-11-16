// Code lists of the HR / organization screens (adm-orgunit, adm-employee, adm-position).
// Not in api/MetaData yet: values = what the seeds use (OrganizationUnit_seed.sql: Status 'Active';
// UnitType COMPANY / BOARD / DIVISION / DEPARTMENT / TEAM; BRANCH = header branch selector). PROPOSED.
export interface CodeLabel {
  code: string;
  label: string;
}

export const UNIT_TYPES: CodeLabel[] = [
  { code: 'COMPANY', label: 'Công ty' },
  { code: 'BOARD', label: 'Ban điều hành' },
  { code: 'DIVISION', label: 'Khối' },
  { code: 'DEPARTMENT', label: 'Phòng ban' },
  { code: 'TEAM', label: 'Tổ / nhóm' },
  { code: 'BRANCH', label: 'Chi nhánh' },
  { code: 'KITCHEN', label: 'Bếp' },
  { code: 'WAREHOUSE', label: 'Kho' },
];

export const RECORD_STATUSES: CodeLabel[] = [
  { code: 'Active', label: 'Đang hoạt động' },
  { code: 'Inactive', label: 'Ngừng hoạt động' },
];

export const EMPLOYEE_STATUSES: CodeLabel[] = [
  { code: 'Active', label: 'Đang làm việc' },
  { code: 'Inactive', label: 'Đã nghỉ việc' },
];

export const GENDERS: CodeLabel[] = [
  { code: 'Nam', label: 'Nam' },
  { code: 'Nữ', label: 'Nữ' },
  { code: 'Khác', label: 'Khác' },
];

export const labelOf = (list: CodeLabel[], code?: string | null): string =>
  list.find((x) => x.code.toLowerCase() === (code ?? '').toLowerCase())?.label ?? code ?? '';
