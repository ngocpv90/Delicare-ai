import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { DOMAIN_ROUTER, URL_API } from '../helpers/constant-helper';
import { User } from '../helpers/user';

/** Quyền của một màn hình theo đúng khóa engine mockup dùng (Store.perm). */
export interface ScreenPermission {
  access: boolean;
  add?: boolean;
  edit?: boolean;
  del?: boolean;
  exp?: boolean;
  print?: boolean;
  imp?: boolean;
}

/** Cầu nối Angular ↔ engine giao diện DELICARE (assets/delicare/js) – gán vào window.DC_HOST. */
export interface DelicareHost {
  user: User | null;
  hashBase: string;
  get(path: string): Promise<any>;
  send(method: 'POST' | 'PUT' | 'DELETE', path: string, body?: FormData): Promise<any>;
  perm(screenId: string): ScreenPermission;
  errorText(error: any): string;
  logout(): void;
}

// Màn hình điều hướng / tổng quan không gắn bảng: ai đăng nhập cũng xem được.
const OPEN_SCREENS = new Set(['dash-exec', 'flow-e2e']);
const FULL: ScreenPermission = { access: true, add: true, edit: true, del: true, exp: true, print: true, imp: true };

export function createDelicareHost(http: HttpClient, user: User | null, logout: () => void): DelicareHost {
  // listMenu (UserRole × ObjectMenu của nhóm): cấp 1 + cấp 2. MetaLink theo spec = '#/<screen-id>'.
  const menus: any[] = (user?.listMenu ?? []).flatMap((x: any) => [x, ...(x.children ?? [])]);
  const byLink = new Map<string, any>();
  menus.forEach((m) => {
    const link = String(m.metaLink ?? '').trim();
    if (link) {
      byLink.set(link.replace(/^#\//, ''), m);
    }
  });
  const url = (path: string) => URL_API + path;

  return {
    user,
    hashBase: `#/${DOMAIN_ROUTER}/`,
    get: (path) => firstValueFrom(http.get<any>(url(path))),
    send: (method, path, body) => {
      switch (method) {
        case 'POST':
          return firstValueFrom(http.post<any>(url(path), body ?? null));
        case 'PUT':
          return firstValueFrom(http.put<any>(url(path), body ?? null));
        default:
          return firstValueFrom(http.delete<any>(url(path)));
      }
    },
    perm: (screenId) => {
      if (user?.isAdmin) {
        return FULL;
      }
      const m = byLink.get(screenId);
      if (!m) {
        return OPEN_SCREENS.has(screenId) ? { access: true } : { access: false };
      }
      return {
        access: !!m.allowAccess,
        add: !!m.allowAdd,
        edit: !!m.allowEdit,
        del: !!m.allowDelete,
        exp: !!m.allowExport,
        print: !!m.allowPrint,
        imp: !!m.allowImport,
      };
    },
    errorText: (error) => {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 0) return 'Không kết nối được server';
        if (error.status === 401) return 'Hết phiên đăng nhập (401)';
        if (error.status === 403) return 'Tài khoản không có quyền thao tác này (403)';
        if (error.status === 404) return 'API chưa có (404)';
        const body = error.error;
        const text = typeof body === 'string' ? body : body?.responseMessage || body?.title || body?.message;
        return `${text || error.statusText || 'Lỗi API'} (${error.status})`;
      }
      // JwtInterceptor nuốt lỗi 401/403 và trả về chuỗi message thay cho dữ liệu.
      if (typeof error === 'string') return error;
      return error?.message || 'Lỗi không xác định';
    },
    logout,
  };
}
