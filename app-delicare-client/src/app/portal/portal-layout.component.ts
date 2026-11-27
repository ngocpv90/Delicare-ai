import { AsyncPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, ViewEncapsulation, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catchError, filter, map, of, tap } from 'rxjs';
import { ThemeService } from '../delicare/theme.service';
import { AUTHEN, DOMAIN_ROUTER } from '../helpers/constant-helper';
import { CustomerAccountService } from './account.service';
import { CartService } from './cart.service';
import { PortalDataService } from './portal-data.service';
import { PortalData } from './portal.model';

/** Khung trang khách lẻ: thanh trên + menu + chân trang. Toàn bộ CSS cổng nằm ở portal.css, gói trong .pt-root. */
@Component({
  selector: 'app-portal-layout',
  standalone: true,
  imports: [AsyncPipe, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './portal-layout.component.html',
  styleUrls: ['./portal.css'],
  encapsulation: ViewEncapsulation.None,
})
export class PortalLayoutComponent implements OnInit {
  private readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  public readonly cart = inject(CartService);
  /** RETAIL-2: tài khoản khách lẻ (Đăng nhập / Đơn của tôi trên thanh đầu trang). */
  public readonly account = inject(CustomerAccountService);
  public readonly menuOpen = signal(false);
  public readonly onCart = signal(false);
  public readonly year = new Date().getFullYear();

  /** Đang xem trước khi trang khách lẻ chưa bật (portalEnabled = false) – mở bằng #/?xemtruoc=1, giữ trong phiên trình duyệt. */
  public readonly preview = signal(this.readPreview());
  /** Nhân viên đã đăng nhập (còn hạn) – hiện nút "Vào hệ thống". */
  public readonly staffLoggedIn = this.readStaff();

  /** Nội dung + trạng thái lỗi: file cấu hình hỏng / không tải được thì báo lỗi thay vì "Đang tải…" mãi. */
  public readonly vm$ = inject(PortalDataService).data$.pipe(
    tap((d) => {
      // Trang khách lẻ TẮT (nội dung chưa duyệt): giữ hành vi cũ – vào #/ là trang đăng nhập nội bộ.
      if (d.portalEnabled === false && !this.preview()) {
        this.router.navigateByUrl('/dang-nhap', { replaceUrl: true });
        return;
      }
      this.cart.sync(d.products, d.catalogSource === 'api');
    }),
    map((d): { d: PortalData | null; off?: boolean } => (d.portalEnabled === false && !this.preview() ? { d: null, off: true } : { d })),
    catchError(() => of({ d: null, off: false })),
  );

  public readonly nav = [
    { link: '/', label: 'Trang chủ', exact: true },
    { link: '/thuc-don', label: 'Thực đơn tuần', exact: false },
    { link: '/san-pham', label: 'Sản phẩm', exact: false },
    { link: '/tra-cuu-don', label: 'Tra cứu đơn', exact: false },
    { link: '/lien-he', label: 'Liên hệ', exact: false },
    // Mục phụ: máy tính nằm trên thanh xanh phía trên (header chính chỉ giữ việc mua hàng – anh báo menu đè nút Đăng nhập 2026-11-27); menu ☰ vẫn có.
    { link: '/gioi-thieu', label: 'Giới thiệu', exact: false, secondary: true },
    { link: '/chinh-sach', label: 'Chính sách', exact: false, secondary: true },
  ];
  public readonly appLink = '/' + DOMAIN_ROUTER;

  private previousTitle = '';

  public ngOnInit(): void {
    // Cổng công khai không dùng lớp CSS của màn nghiệp vụ (assets/delicare/css/app.css).
    this.theme.use('w3crm');
    this.previousTitle = document.title;
    document.title = 'DELI-CARE – Đặt suất ăn, cơm văn phòng';
    this.destroyRef.onDestroy(() => (document.title = this.previousTitle));

    this.onCart.set(this.router.url.startsWith('/gio-hang'));
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((e) => {
        this.menuOpen.set(false);
        this.onCart.set(e.urlAfterRedirects.startsWith('/gio-hang'));
      });
  }

  private readPreview(): boolean {
    try {
      if (/[?&]xemtruoc=1\b/.test(location.hash)) sessionStorage.setItem('dc_portal_preview', '1');
      return sessionStorage.getItem('dc_portal_preview') === '1';
    } catch {
      return false;
    }
  }

  private readStaff(): boolean {
    try {
      const a = JSON.parse(localStorage.getItem(AUTHEN) || 'null') as { token?: string; expireTime?: number } | null;
      return !!(a && a.token && (!a.expireTime || a.expireTime > Date.now()));
    } catch {
      return false;
    }
  }

  public toggleMenu(): void {
    this.menuOpen.update((v) => !v);
  }

  public reload(): void {
    location.reload();
  }
}
