import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, map, shareReplay } from 'rxjs';
import { PortalData, PortalProduct } from './portal.model';
import { RetailApiService, RetailCatalogDto, WeeklyMenuDto } from './retail-api.service';

const list = (v: string[] | string | undefined): string[] =>
  Array.isArray(v) ? v : (v || '').split(';').map((t) => t.trim()).filter(Boolean);

/**
 * Nội dung trang khách lẻ.
 * - Thông tin công ty, chính sách, giới thiệu: file cấu hình assets/portal/portal-data.json.
 * - Sản phẩm + giá (RetailPortal/GetCatalog) và thực đơn tuần đã công bố (RetailPortal/GetWeeklyMenu): máy chủ khi có (RETAIL-1);
 *   chưa có API / lỗi → dùng phần tương ứng trong file cấu hình (catalogSource / menuSource = 'file').
 */
@Injectable({ providedIn: 'root' })
export class PortalDataService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(RetailApiService);

  public readonly data$: Observable<PortalData> = forkJoin([
    this.http.get<PortalData>('assets/portal/portal-data.json'),
    this.api.catalog(),
    this.api.weeklyMenu(),
  ]).pipe(
    map(([file, catalog, menu]) => {
      let d: PortalData = { ...file, catalogSource: 'file', menuSource: 'file' };
      if (catalog) d = this.withCatalog(d, catalog);
      if (menu) d = this.withMenu(d, menu);
      return d;
    }),
    shareReplay(1),
  );

  private withCatalog(file: PortalData, c: RetailCatalogDto): PortalData {
    const products: PortalProduct[] = c.products.map((p, i) => ({
      id: p.id,
      code: p.code,
      categoryId: p.categoryCode,
      name: p.name,
      price: Number(p.price) || 0,
      unit: p.unit,
      description: p.description || '',
      tags: list(p.tags),
      meals: list(p.mealTypes),
      icon: p.icon || 'restaurant',
      imageUrl: p.imageUrl || undefined,
      tone: i % 2 ? 'orange' : 'green',
    }));
    const used = new Set(products.map((p) => p.categoryId));
    const categories = (c.categories?.length ? c.categories.map((x) => ({ id: x.code, name: x.name })) : file.categories).filter((x) => used.has(x.id));
    return { ...file, products, categories, catalogSource: 'api' };
  }

  private withMenu(file: PortalData, m: WeeklyMenuDto): PortalData {
    const fmt = (iso: string) => (iso ? `${iso.slice(8, 10)}/${iso.slice(5, 7)}` : '');
    return {
      ...file,
      menuSource: 'api',
      weeklyMenu: {
        title: m.title || file.weeklyMenu.title,
        note: file.weeklyMenu.note,
        meals: m.meals?.length ? m.meals : file.weeklyMenu.meals,
        days: m.days.map((x) => ({ day: x.date ? `${x.day} ${fmt(x.date)}` : x.day, date: x.date, meals: x.meals })),
      },
    };
  }
}
