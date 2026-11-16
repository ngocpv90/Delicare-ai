import { Routes } from '@angular/router';
import { AuthGuard } from './helpers/auth.guard';
import { DOMAIN_ROUTER } from './helpers/constant-helper';

// FE-0 (D-35): only the DELI-CARE app. The W3CRM demo pages (pages/**, components/**, plugins/**,
// elements/admin-layout) are kept on disk as reference but are no longer routed.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: '/dang-nhap' },
  {
    path: 'dang-nhap',
    loadChildren: () =>
      import('./sign-in/sign-in.module').then((m) => m.SignInModule),
  },
  {
    path: DOMAIN_ROUTER,
    loadChildren: () => import('./main/main.module').then((m) => m.MainModule),
    canActivate: [AuthGuard],
  },
  {
    path: 'khong-co-quyen-truy-cap',
    loadChildren: () =>
      import('./not-found/not-found.module').then((m) => m.NotFoundModule),
  },
  // ObjectMenu.MetaLink pasted as a URL ('#/<screen-id>') -> same screen inside the app
  { path: ':screenId', redirectTo: `/${DOMAIN_ROUTER}/:screenId` },
  { path: '**', redirectTo: '' },
];
