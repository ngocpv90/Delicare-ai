import { HttpBackend, HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap, timeout } from 'rxjs';
import { URL_API } from '../helpers/constant-helper';
import { TrackOrderDto } from './retail-api.service';

/** Phiên khách lẻ – KHÁC phiên nhân viên (localStorage "authen"); không bao giờ dùng cho API nội bộ. */
const SESSION_KEY = 'dc_retail_session';
const TIMEOUT_MS = 10000;

export interface CustomerProfile {
  fullName: string;
  phoneNumber: string;
  email?: string | null;
  address?: string | null;
  createdDate?: string;
}

interface Session {
  token: string;
  expiresAt: string;
  profile: CustomerProfile;
}

export interface ProgressStep {
  code: string;
  label: string;
  /** done / current / pending / cancelled */
  state: string;
  at?: string | null;
  note?: string | null;
}

export interface RouteInfo {
  routeNumber: string;
  status: string;
  statusLabel: string;
  driverName?: string | null;
  licensePlate?: string | null;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  actualStart?: string | null;
  actualEnd?: string | null;
  stopCount: number;
  stopDoneCount: number;
  lastLatitude?: number | null;
  lastLongitude?: number | null;
  lastPositionAt?: string | null;
  listEvent: { label: string; at: string; note?: string | null; temperatureC?: number | null }[];
}

/** Chi tiết đơn có tiến độ (Đơn của tôi / Tra cứu đơn). */
export interface OrderDetail extends TrackOrderDto {
  address?: string;
  note?: string | null;
  createdDate?: string;
  stageCode: string;
  stageLabel: string;
  listStep: ProgressStep[];
  route?: RouteInfo | null;
  hasTrace?: boolean;
}

export interface MyOrderItem {
  orderNumber: string;
  deliveryDate: string;
  mealType: string;
  orderStatus: string;
  stageLabel: string;
  stageCode: string;
  totalAmount: number;
  lineSummary: string;
  createdDate: string;
  hasRoute: boolean;
}

export interface MyOrderList {
  listItem: MyOrderItem[];
  groupCounts: Record<string, number>;
}

export interface TraceInfo {
  orderNumber: string;
  deliveryDate: string;
  mealType: string;
  inferred: boolean;
  note?: string | null;
  listSupplier: string[];
  listBatch: {
    dishName: string;
    batchNumber: string;
    statusLabel: string;
    qcLabel: string;
    cookStart?: string | null;
    cookEnd?: string | null;
    sampleRetainedAt?: string | null;
    listTemperature: { label: string; temperatureC: number; measuredAt: string; isWithinRange?: boolean | null }[];
    listIngredient: { ingredientName: string; lotNumber?: string | null; supplierName?: string | null; receivedDate?: string | null; expiryDate?: string | null; qcLabel: string }[];
  }[];
}

/** Kết quả gọi API: ok + dữ liệu, hoặc lỗi tiếng Việt (expired = phiên hết hạn → đã đăng xuất). */
export type ApiOutcome<T> = { ok: true; data: T; message?: string } | { ok: false; message: string; expired?: boolean };

interface ApiResult<T> {
  responseCode?: number;
  responseMessage?: string;
  data?: T;
}

/**
 * RETAIL-2: tài khoản khách lẻ (RetailPortal/Register, Login, GetMyOrders…). Đi thẳng HttpBackend như RetailApiService:
 * không qua JwtInterceptor của nhân viên; phiên gửi ở header X-Retail-Token.
 */
@Injectable({ providedIn: 'root' })
export class CustomerAccountService {
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly session = signal<Session | null>(this.load());

  public readonly profile = computed(() => this.session()?.profile ?? null);
  public readonly loggedIn = computed(() => !!this.session());

  public register(body: { fullName: string; phoneNumber: string; password: string; email?: string; address?: string; website?: string }): Observable<ApiOutcome<CustomerProfile>> {
    return this.call<Session>('post', 'Register', body).pipe(map((r) => this.keep(r)));
  }

  public login(phoneNumber: string, password: string): Observable<ApiOutcome<CustomerProfile>> {
    return this.call<Session>('post', 'Login', { phoneNumber, password }).pipe(map((r) => this.keep(r)));
  }

  public logout(): void {
    this.session.set(null);
    try {
      localStorage.removeItem(SESSION_KEY);
    } catch {
      /* trình duyệt chặn lưu trữ */
    }
  }

  public refreshProfile(): Observable<ApiOutcome<CustomerProfile>> {
    return this.call<CustomerProfile>('get', 'GetProfile').pipe(tap((r) => r.ok && this.patchProfile(r.data)));
  }

  public updateProfile(body: { fullName: string; email?: string; address?: string }): Observable<ApiOutcome<CustomerProfile>> {
    return this.call<CustomerProfile>('post', 'UpdateProfile', body).pipe(tap((r) => r.ok && this.patchProfile(r.data)));
  }

  public changePassword(currentPassword: string, newPassword: string): Observable<ApiOutcome<CustomerProfile>> {
    return this.call<Session>('post', 'ChangePassword', { currentPassword, newPassword }).pipe(map((r) => this.keep(r)));
  }

  public myOrders(group: string): Observable<ApiOutcome<MyOrderList>> {
    return this.call<MyOrderList>('get', 'GetMyOrders?group=' + encodeURIComponent(group));
  }

  public myOrder(orderNumber: string): Observable<ApiOutcome<OrderDetail>> {
    return this.call<OrderDetail>('get', 'GetMyOrder?orderNumber=' + encodeURIComponent(orderNumber));
  }

  public trace(orderNumber: string): Observable<ApiOutcome<TraceInfo>> {
    return this.call<TraceInfo>('get', 'GetOrderTrace?orderNumber=' + encodeURIComponent(orderNumber));
  }

  public claim(orderNumber: string): Observable<ApiOutcome<OrderDetail>> {
    return this.call<OrderDetail>('post', 'ClaimOrder', { orderNumber });
  }

  /** Header phiên cho các API công khai khác (gửi đơn khi đã đăng nhập → đơn vào "Đơn của tôi"). */
  public authHeaders(): HttpHeaders | undefined {
    const s = this.session();
    return s ? new HttpHeaders({ 'X-Retail-Token': s.token }) : undefined;
  }

  private call<T>(method: 'get' | 'post', path: string, body?: unknown): Observable<ApiOutcome<T>> {
    const url = URL_API + 'RetailPortal/' + path;
    const opts = { headers: this.authHeaders() };
    const req = method === 'get' ? this.http.get<ApiResult<T>>(url, opts) : this.http.post<ApiResult<T>>(url, body ?? {}, opts);
    return req.pipe(
      timeout(TIMEOUT_MS),
      map((r): ApiOutcome<T> =>
        (r?.responseCode ?? 200) === 200 && r?.data != null
          ? { ok: true, data: r.data, message: r.responseMessage }
          : { ok: false, message: r?.responseMessage || 'Chưa thực hiện được – vui lòng thử lại.' },
      ),
      catchError((e: unknown) => {
        const err = e instanceof HttpErrorResponse ? e : null;
        const msg = (err?.error as ApiResult<unknown> | null)?.responseMessage;
        if (err?.status === 401) {
          this.logout();
          return of<ApiOutcome<T>>({ ok: false, expired: true, message: msg || 'Phiên đăng nhập đã hết hạn – vui lòng đăng nhập lại.' });
        }
        if (err && (err.status === 400 || err.status === 429)) return of<ApiOutcome<T>>({ ok: false, message: msg || 'Thông tin chưa hợp lệ.' });
        return of<ApiOutcome<T>>({ ok: false, message: 'Không kết nối được máy chủ – vui lòng thử lại sau ít phút hoặc gọi hotline.' });
      }),
    );
  }

  private keep(r: ApiOutcome<Session>): ApiOutcome<CustomerProfile> {
    if (!r.ok) return r;
    this.session.set(r.data);
    this.save(r.data);
    return { ok: true, data: r.data.profile, message: r.message };
  }

  private patchProfile(p: CustomerProfile): void {
    const s = this.session();
    if (!s) return;
    const next = { ...s, profile: p };
    this.session.set(next);
    this.save(next);
  }

  private save(s: Session): void {
    try {
      localStorage.setItem(SESSION_KEY, JSON.stringify(s));
    } catch {
      /* chế độ ẩn danh chặn lưu trữ – phiên chỉ còn trong tab */
    }
  }

  private load(): Session | null {
    try {
      const s = JSON.parse(localStorage.getItem(SESSION_KEY) || 'null') as Session | null;
      return s && s.token && (!s.expiresAt || new Date(s.expiresAt).getTime() > Date.now()) ? s : null;
    } catch {
      return null;
    }
  }
}
