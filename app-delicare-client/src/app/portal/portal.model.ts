// Kiểu dữ liệu của trang khách lẻ (cổng công khai) – đọc từ assets/portal/portal-data.json.
export interface PortalCompany {
  brand: string;
  legalName: string;
  slogan: string;
  address: string;
  phone: string;
  phoneHref: string;
  zalo: string;
  email: string;
  website: string;
  workingHours: string;
  /** Giờ chốt đơn cho ngày hôm sau (mặc định 20). */
  orderCutoffHour?: number;
}

export interface PortalIconText {
  icon?: string;
  title: string;
  text: string;
}

export interface PortalCategory {
  id: string;
  name: string;
}

export interface PortalProduct {
  /** Id máy chủ (GUID) khi danh mục lấy từ API; mã trong file cấu hình khi dùng file dự phòng. */
  id: string;
  /** Mã sản phẩm máy chủ (SPL-NNNNN). */
  code?: string;
  categoryId: string;
  name: string;
  price: number;
  unit: string;
  description: string;
  tags: string[];
  icon: string;
  tone: 'green' | 'orange';
}

export interface PortalMenuDay {
  day: string;
  meals: Partial<Record<string, string[]>>;
}

export interface PortalPolicy {
  slug: string;
  title: string;
  icon: string;
  sections: { heading: string; items: string[] }[];
}

export interface PortalData {
  company: PortalCompany;
  hero: { eyebrow: string; title: string; subtitle: string };
  highlights: PortalIconText[];
  steps: PortalIconText[];
  categories: PortalCategory[];
  products: PortalProduct[];
  weeklyMenu: { title: string; note: string; meals: string[]; days: PortalMenuDay[] };
  policies: PortalPolicy[];
  about: { title: string; paragraphs: string[]; values: PortalIconText[] };
  /** Nguồn danh mục sản phẩm: 'api' (máy chủ) hoặc 'file' (file cấu hình – chưa có API). */
  catalogSource?: 'api' | 'file';
}

export interface CartLine {
  productId: string;
  productCode?: string;
  name: string;
  price: number;
  unit: string;
  quantity: number;
}
