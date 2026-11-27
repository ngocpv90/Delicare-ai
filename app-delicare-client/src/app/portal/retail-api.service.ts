import { HttpBackend, HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, TimeoutError, catchError, map, of, timeout } from 'rxjs';
import { URL_API } from '../helpers/constant-helper';

/**
 * Kết quả gửi đơn:
 * - ok: máy chủ đã lưu (có mã đơn);
 * - rejected: máy chủ từ chối (sai thông tin / quá giờ chốt / gửi quá nhiều) – hiện lý do, khách sửa rồi gửi lại;
 * - unavailable: chắc chắn CHƯA lưu (chưa có API – 404, không kết nối được – 0) → đặt qua điện thoại;
 * - uncertain: có thể đã lưu (hết giờ chờ, lỗi 5xx sau khi gửi) → KHÔNG báo "chưa gửi", nhắc gọi xác nhận để tránh đặt trùng.
 */
export type SubmitResult =
  | { kind: 'ok'; orderNumber: string; totalAmount?: number }
  | { kind: 'rejected'; message: string }
  | { kind: 'unavailable' }
  | { kind: 'uncertain' };

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
    mealTypes?: string[] | string;
    icon?: string;
  }[];
}

export interface WeeklyMenuDto {
  title?: string;
  meals: string[];
  days: { date: string; day: string; meals: Record<string, string[]> }[];
}

export interface TrackOrderDto {
  orderNumber: string;
  orderStatus: string;
  deliveryDate: string;
  mealType: string;
  customerName: string;
  totalAmount: number;
  paymentStatus?: string;
  lines: { productName: string; quantity: number; unit: string; lineAmount: number }[];
  cancelReason?: string;
}

export interface SubmitOrderRequest {
  clientRequestId: string;
  customerName: string;
  phoneNumber: string;
  address: string;
  deliveryDate: string;
  mealType: string;
  note: string;
  website: string;
  lines: { productId: string; productCode: string; quantity: number }[];
}

interface ApiResult<T> {
  responseCode?: number;
  responseMessage?: string;
  data?: T;
}

const SUBMIT_TIMEOUT_MS = 15000;
const READ_TIMEOUT_MS = 8000;

/**
 * API công khai của module Bán lẻ (docs/frontend-handoff/RETAIL-1.md mục 5.1).
 * Dùng HttpClient đi thẳng HttpBackend – KHÔNG qua JwtInterceptor: khách lẻ không có token, và lỗi 401 không được đẩy khách sang trang đăng nhập nội bộ.
 */
@Injectable({ providedIn: 'root' })
export class RetailApiService {
  private readonly http = new HttpClient(inject(HttpBackend));

  /** Danh mục sản phẩm từ máy chủ; null khi API chưa có / lỗi → trang dùng file cấu hình. */
  public catalog(): Observable<RetailCatalogDto | null> {
    return this.read<RetailCatalogDto>('RetailPortal/GetCatalog').pipe(map((d) => (d && Array.isArray(d.products) && d.products.length ? d : null)));
  }

  /** Thực đơn tuần đã công bố trên hệ thống; null → dùng thực đơn trong file cấu hình. */
  public weeklyMenu(): Observable<WeeklyMenuDto | null> {
    return this.read<WeeklyMenuDto>('RetailPortal/GetWeeklyMenu').pipe(map((d) => (d && Array.isArray(d.days) && d.days.length ? d : null)));
  }

  /** Tra cứu đơn theo mã đơn + SĐT đặt hàng (cả 2 phải khớp). */
  public track(orderNumber: string, phoneNumber: string): Observable<{ ok: true; data: TrackOrderDto } | { ok: false; message: string; missing?: boolean }> {
    const q = `?orderNumber=${encodeURIComponent(orderNumber)}&phoneNumber=${encodeURIComponent(phoneNumber)}`;
    return this.http.get<ApiResult<TrackOrderDto>>(URL_API + 'RetailPortal/TrackOrder' + q).pipe(
      timeout(READ_TIMEOUT_MS),
      map((r) =>
        (r?.responseCode ?? 200) === 200 && r?.data
          ? ({ ok: true as const, data: r.data })
          : ({ ok: false as const, message: r?.responseMessage || 'Không tìm thấy đơn – kiểm tra lại mã đơn và số điện thoại.' }),
      ),
      catchError((e: unknown) => {
        const st = e instanceof HttpErrorResponse ? e.status : 0;
        if (st === 400 || st === 429) {
          const b = (e as HttpErrorResponse).error as ApiResult<unknown> | null;
          return of({ ok: false as const, message: b?.responseMessage || 'Không tìm thấy đơn – kiểm tra lại mã đơn và số điện thoại.' });
        }
        return of({ ok: false as const, missing: st === 404, message: 'Chưa tra cứu được lúc này – vui lòng gọi điện để hỏi tình trạng đơn.' });
      }),
    );
  }

  public submit(req: SubmitOrderRequest): Observable<SubmitResult> {
    return this.http.post<ApiResult<{ orderNumber: string; totalAmount?: number }>>(URL_API + 'RetailPortal/SubmitOrder', req).pipe(
      timeout(SUBMIT_TIMEOUT_MS),
      map((r): SubmitResult => {
        const code = r?.responseCode ?? 200;
        if (code === 200 && r?.data?.orderNumber) return { kind: 'ok', orderNumber: r.data.orderNumber, totalAmount: r.data.totalAmount };
        if (code >= 400 && code < 500) return { kind: 'rejected', message: r?.responseMessage || 'Thông tin đơn chưa hợp lệ.' };
        return { kind: 'uncertain' };
      }),
      catchError((e: unknown) => {
        if (e instanceof TimeoutError) return of<SubmitResult>({ kind: 'uncertain' });
        const err = e as HttpErrorResponse;
        // 400 / 429: máy chủ từ chối, có lý do tiếng Việt → hiện cho khách.
        if (err.status === 400 || err.status === 429) {
          const body = err.error as ApiResult<unknown> | string | null;
          const msg = typeof body === 'string' ? body : body?.responseMessage;
          return of<SubmitResult>({
            kind: 'rejected',
            message: msg || (err.status === 429 ? 'Bạn đã gửi nhiều đơn trong thời gian ngắn – vui lòng thử lại sau hoặc gọi điện đặt hàng.' : 'Thông tin đơn chưa hợp lệ.'),
          });
        }
        // 404 (chưa có API) / 0 (không kết nối được): chắc chắn chưa lưu. 5xx: có thể đã lưu.
        return of<SubmitResult>(err.status === 404 || err.status === 0 ? { kind: 'unavailable' } : { kind: 'uncertain' });
      }),
    );
  }

  private read<T>(path: string): Observable<T | null> {
    return this.http.get<ApiResult<T>>(URL_API + path).pipe(
      timeout(READ_TIMEOUT_MS),
      map((r) => (r && (r.responseCode ?? 200) === 200 && r.data ? r.data : null)),
      catchError(() => of(null)),
    );
  }
}
