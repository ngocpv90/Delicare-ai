import { HttpBackend, HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { URL_API } from '../helpers/constant-helper';

/** Kết quả gửi đơn: ok = đã lưu; rejected = máy chủ từ chối (sai thông tin – hiện lý do); unavailable = chưa có API / lỗi mạng → đặt qua hotline. */
export type SubmitResult =
  | { kind: 'ok'; orderNumber: string; totalAmount?: number }
  | { kind: 'rejected'; message: string }
  | { kind: 'unavailable' };

export interface RetailCatalogDto {
  categories: { code: string; name: string }[];
  products: {
    id: string;
    code: string;
    categoryCode: string;
    name: string;
    unit: string;
    price: number;
    description?: string;
    tags?: string[] | string;
    icon?: string;
  }[];
}

export interface SubmitOrderRequest {
  customerName: string;
  phoneNumber: string;
  address: string;
  deliveryDate: string;
  mealType: string;
  note: string;
  website: string;
  lines: { productId: string | null; productCode: string; quantity: number }[];
}

interface ApiResult<T> {
  responseCode?: number;
  responseMessage?: string;
  data?: T;
}

/**
 * API công khai của module Bán lẻ (docs/frontend-handoff/RETAIL-1.md mục 5.1).
 * Dùng HttpClient đi thẳng HttpBackend – KHÔNG qua JwtInterceptor: khách lẻ không có token, và lỗi 401 không được đẩy khách sang trang đăng nhập nội bộ.
 */
@Injectable({ providedIn: 'root' })
export class RetailApiService {
  private readonly http = new HttpClient(inject(HttpBackend));

  /** Danh mục sản phẩm từ máy chủ; null khi API chưa có / lỗi → trang dùng file cấu hình. */
  public catalog(): Observable<RetailCatalogDto | null> {
    return this.http.get<ApiResult<RetailCatalogDto>>(URL_API + 'RetailPortal/GetCatalog').pipe(
      map((r) => (r && (r.responseCode ?? 200) === 200 && r.data && Array.isArray(r.data.products) && r.data.products.length ? r.data : null)),
      catchError(() => of(null)),
    );
  }

  public submit(req: SubmitOrderRequest): Observable<SubmitResult> {
    return this.http.post<ApiResult<{ orderNumber: string; totalAmount?: number }>>(URL_API + 'RetailPortal/SubmitOrder', req).pipe(
      map((r): SubmitResult => {
        const code = r?.responseCode ?? 200;
        if (code === 200 && r?.data?.orderNumber) return { kind: 'ok', orderNumber: r.data.orderNumber, totalAmount: r.data.totalAmount };
        if (code >= 400 && code < 500) return { kind: 'rejected', message: r?.responseMessage || 'Thông tin đơn chưa hợp lệ.' };
        return { kind: 'unavailable' };
      }),
      catchError((e: HttpErrorResponse) => {
        // 400 / 429 có thông báo tiếng Việt từ máy chủ → hiện cho khách; 404 (chưa có API), 0 (mất mạng), 5xx → đặt qua hotline.
        if (e.status === 400 || e.status === 429) {
          const body = e.error as ApiResult<unknown> | string | null;
          const msg = typeof body === 'string' ? body : body?.responseMessage;
          return of<SubmitResult>({
            kind: 'rejected',
            message: msg || (e.status === 429 ? 'Bạn đã gửi nhiều đơn trong thời gian ngắn – vui lòng thử lại sau hoặc gọi hotline.' : 'Thông tin đơn chưa hợp lệ.'),
          });
        }
        return of<SubmitResult>({ kind: 'unavailable' });
      }),
    );
  }
}
