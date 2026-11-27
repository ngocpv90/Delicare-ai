import { Routes, UrlMatchResult, UrlSegment } from '@angular/router';
import { AuthGuard } from './helpers/auth.guard';
import { DOMAIN_ROUTER } from './helpers/constant-helper';

// FE-0 (D-35): only the DELI-CARE app. The W3CRM demo pages (pages/**, components/**, plugins/**,
// elements/admin-layout) are kept on disk as reference but are no longer routed.
export const routes: Routes = [
  {
    path: 'dang-nhap',
    loadChildren: () =>
      import('./sign-in/sign-in.module').then((m) => m.SignInModule),
  },
  // Giao diện DELICARE theo mockup (docs/mockup): một route duy nhất cho /quan-tri và mọi /quan-tri/<screen-id>,
  // để component không bị tạo lại khi đổi màn hình – engine tự định tuyến theo hash.
  {
    matcher: (segments: UrlSegment[]): UrlMatchResult | null =>
      segments.length && segments[0].path === DOMAIN_ROUTER ? { consumed: segments } : null,
    loadComponent: () =>
      import('./delicare/delicare-shell.component').then((m) => m.DelicareShellComponent),
    canActivate: [AuthGuard],
  },
  {
    path: 'khong-co-quyen-truy-cap',
    loadChildren: () =>
      import('./not-found/not-found.module').then((m) => m.NotFoundModule),
  },
  // Trang khách lẻ công khai (trang chủ, thực đơn tuần, sản phẩm, chính sách, giới thiệu, liên hệ, giỏ hàng) – không cần đăng nhập.
  // Đặt trước ':screenId' để '/thuc-don', '/san-pham'… không bị chuyển vào /quan-tri.
  {
    path: '',
    loadChildren: () => import('./portal/portal.routes').then((m) => m.PORTAL_ROUTES),
  },
  // ObjectMenu.MetaLink pasted as a URL ('#/<screen-id>') -> same screen inside the app
  { path: ':screenId', redirectTo: `/${DOMAIN_ROUTER}/:screenId` },
  { path: '**', redirectTo: '' },
];
