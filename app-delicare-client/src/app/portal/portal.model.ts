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
  id: string;
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
}

export interface CartLine {
  productId: string;
  name: string;
  price: number;
  unit: string;
  quantity: number;
}
