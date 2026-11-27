import { Routes } from '@angular/router';
import { PortalLayoutComponent } from './portal-layout.component';

// Trang khách lẻ – công khai, không cần đăng nhập. Đường dẫn không trùng screen-id của màn nghiệp vụ.
export const PORTAL_ROUTES: Routes = [
  {
    path: '',
    component: PortalLayoutComponent,
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./pages/home.component').then((m) => m.PortalHomeComponent) },
      { path: 'thuc-don', loadComponent: () => import('./pages/weekly-menu.component').then((m) => m.PortalWeeklyMenuComponent) },
      { path: 'san-pham', loadComponent: () => import('./pages/products.component').then((m) => m.PortalProductsComponent) },
      { path: 'chinh-sach', loadComponent: () => import('./pages/policies.component').then((m) => m.PortalPoliciesComponent) },
      { path: 'chinh-sach/:slug', loadComponent: () => import('./pages/policies.component').then((m) => m.PortalPoliciesComponent) },
      { path: 'gioi-thieu', loadComponent: () => import('./pages/about.component').then((m) => m.PortalAboutComponent) },
      { path: 'lien-he', loadComponent: () => import('./pages/contact.component').then((m) => m.PortalContactComponent) },
      { path: 'tra-cuu-don', loadComponent: () => import('./pages/track.component').then((m) => m.PortalTrackComponent) },
      { path: 'gio-hang', loadComponent: () => import('./pages/cart.component').then((m) => m.PortalCartComponent) },
      // RETAIL-2: tài khoản khách lẻ (khác /dang-nhap của nhân viên).
      { path: 'tai-khoan/dang-nhap', loadComponent: () => import('./pages/account-login.component').then((m) => m.PortalAccountLoginComponent) },
      { path: 'tai-khoan', loadComponent: () => import('./pages/account-profile.component').then((m) => m.PortalAccountProfileComponent) },
      { path: 'don-cua-toi', loadComponent: () => import('./pages/my-orders.component').then((m) => m.PortalMyOrdersComponent) },
      { path: 'don-cua-toi/:so', loadComponent: () => import('./pages/my-order-detail.component').then((m) => m.PortalMyOrderDetailComponent) },
    ],
  },
];
