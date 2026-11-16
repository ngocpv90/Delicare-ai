// The 7 UserRole flags in matrix order (adm-permission, adm-user drawer)
export const PERMISSION_FLAGS: { key: string; label: string }[] = [
  { key: 'allowAccess', label: 'Truy cập' },
  { key: 'allowAdd', label: 'Thêm' },
  { key: 'allowEdit', label: 'Sửa' },
  { key: 'allowDelete', label: 'Xóa' },
  { key: 'allowPrint', label: 'In' },
  { key: 'allowExport', label: 'Xuất' },
  { key: 'allowImport', label: 'Nhập' },
];

// Approval items: MetaLink '#/wf-inbox?type=<screen-id>' – the Edit flag means "Duyệt" (D-31)
export const isApprovalMenu = (metaLink?: string | null): boolean =>
  (metaLink ?? '').replace(/^#?\/?/, '').startsWith('wf-inbox?type=');
