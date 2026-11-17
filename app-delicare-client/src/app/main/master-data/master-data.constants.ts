export interface MdOption {
  id: string;
  name: string;
}

// Giá trị lưu DB là mã (cột id), giao diện hiển thị nhãn (cột name).
export const MD_OPTIONS: Record<string, MdOption[]> = {
  status: [
    { id: 'Active', name: 'Đang hoạt động' },
    { id: 'Inactive', name: 'Ngừng hoạt động' },
  ],
  employeeStatus: [
    { id: 'Active', name: 'Đang làm việc' },
    { id: 'Inactive', name: 'Đã nghỉ việc' },
  ],
  unitType: [
    { id: 'SALES', name: 'Kinh doanh' },
    { id: 'PURCHASING', name: 'Mua hàng' },
    { id: 'WAREHOUSE', name: 'Kho' },
    { id: 'PRODUCTION', name: 'Sản xuất' },
    { id: 'QC', name: 'QC' },
    { id: 'LOGISTICS', name: 'Logistics' },
    { id: 'FINANCE', name: 'Tài chính' },
    { id: 'OTHER', name: 'Khác' },
  ],
  operationalLocationType: [
    { id: 'CENTRAL_KITCHEN', name: 'Bếp trung tâm' },
    { id: 'SATELLITE_KITCHEN', name: 'Bếp vệ tinh' },
    { id: 'WAREHOUSE', name: 'Kho bãi' },
    { id: 'DISTRIBUTION_HUB', name: 'Điểm trung chuyển' },
    { id: 'OFFICE', name: 'Văn phòng' },
    { id: 'OTHER', name: 'Khác' },
  ],
  customerType: [
    { id: 'ENTERPRISE', name: 'Doanh nghiệp' },
    { id: 'FACTORY', name: 'Nhà máy / KCN' },
    { id: 'SCHOOL', name: 'Trường học' },
    { id: 'HOSPITAL', name: 'Bệnh viện' },
    { id: 'INDIVIDUAL', name: 'Cá nhân' },
    { id: 'OTHER', name: 'Khác' },
  ],
  warehouseType: [
    { id: 'RAW_MATERIAL', name: 'Kho nguyên liệu' },
    { id: 'FINISHED_GOODS', name: 'Kho thành phẩm' },
    { id: 'PACKAGING', name: 'Kho bao bì / vật tư' },
    { id: 'TOOLS', name: 'Kho dụng cụ' },
    { id: 'OTHER', name: 'Kho khác' },
  ],
  storageLocationType: [
    { id: 'SHELF', name: 'Kệ' },
    { id: 'BIN', name: 'Ô / ngăn' },
    { id: 'PALLET', name: 'Pallet' },
    { id: 'FLOOR', name: 'Sàn' },
    { id: 'COLD_ROOM', name: 'Phòng lạnh' },
  ],
  storageCondition: [
    { id: 'AMBIENT', name: 'Nhiệt độ thường (khô)' },
    { id: 'CHILLED', name: 'Mát (0 - 5°C)' },
    { id: 'FROZEN', name: 'Đông lạnh (≤ -18°C)' },
  ],
  vehicleType: [
    { id: 'REFRIGERATED_TRUCK', name: 'Xe tải lạnh' },
    { id: 'TRUCK', name: 'Xe tải thường' },
    { id: 'VAN', name: 'Xe van' },
    { id: 'MOTORBIKE', name: 'Xe máy' },
    { id: 'OTHER', name: 'Khác' },
  ],
  uomType: [
    { id: 'WEIGHT', name: 'Khối lượng' },
    { id: 'VOLUME', name: 'Thể tích' },
    { id: 'COUNT', name: 'Số lượng / đếm' },
    { id: 'PACKAGE', name: 'Đóng gói' },
    { id: 'PORTION', name: 'Suất' },
  ],
  gender: [
    { id: 'MALE', name: 'Nam' },
    { id: 'FEMALE', name: 'Nữ' },
    { id: 'OTHER', name: 'Khác' },
  ],
};

export function mdLabel(key: string, value: any): string {
  if (value === null || value === undefined || value === '') {
    return '';
  }
  return MD_OPTIONS[key]?.find((x) => x.id === value)?.name ?? String(value);
}

export function mdSameId(a: any, b: any): boolean {
  return !!a && !!b && String(a).toLowerCase() === String(b).toLowerCase();
}

const pad = (n: number) => n.toString().padStart(2, '0');

// FormData for [FromForm] requests: null -> '' (binds to null), Date -> yyyy-MM-dd, bool -> 'true'/'false'.
export function toMasterFormData(value: Record<string, any>): FormData {
  const formData = new FormData();
  Object.keys(value).forEach((key) => {
    const v = value[key];
    let text: string;
    if (v === null || v === undefined) {
      text = '';
    } else if (v instanceof Date) {
      text = `${v.getFullYear()}-${pad(v.getMonth() + 1)}-${pad(v.getDate())}`;
    } else if (typeof v === 'string') {
      text = v.trim();
    } else {
      text = String(v);
    }
    formData.append(key, text);
  });
  return formData;
}
