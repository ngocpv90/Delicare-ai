import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, map, shareReplay } from 'rxjs';
import { PortalData, PortalProduct } from './portal.model';
import { RetailApiService, RetailCatalogDto } from './retail-api.service';

/**
 * Nội dung trang khách lẻ.
 * - Thông tin công ty, thực đơn tuần, chính sách, giới thiệu: file cấu hình assets/portal/portal-data.json.
 * - Danh mục sản phẩm + giá: máy chủ (RetailPortal/GetCatalog – RETAIL-1) khi có; chưa có API thì dùng sản phẩm trong file cấu hình.
 */
@Injectable({ providedIn: 'root' })
export class PortalDataService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(RetailApiService);

  public readonly data$: Observable<PortalData> = forkJoin([
    this.http.get<PortalData>('assets/portal/portal-data.json'),
    this.api.catalog(),
  ]).pipe(
    map(([file, catalog]) => (catalog ? this.withCatalog(file, catalog) : { ...file, catalogSource: 'file' as const })),
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
      tags: Array.isArray(p.tags) ? p.tags : (p.tags || '').split(';').map((t) => t.trim()).filter(Boolean),
      icon: p.icon || 'restaurant',
      tone: i % 2 ? 'orange' : 'green',
    }));
    const used = new Set(products.map((p) => p.categoryId));
    const categories = (c.categories?.length ? c.categories.map((x) => ({ id: x.code, name: x.name })) : file.categories).filter((x) => used.has(x.id));
    return { ...file, products, categories, catalogSource: 'api' };
  }
}
