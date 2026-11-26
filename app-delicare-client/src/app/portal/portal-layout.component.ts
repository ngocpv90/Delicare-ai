import { AsyncPipe } from '@angular/common';
import { Component, OnInit, ViewEncapsulation, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { ThemeService } from '../delicare/theme.service';
import { CartService } from './cart.service';
import { PortalDataService } from './portal-data.service';

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
  public readonly cart = inject(CartService);
  public readonly data$ = inject(PortalDataService).data$;
  public readonly menuOpen = signal(false);
  public readonly onCart = signal(false);
  public readonly year = new Date().getFullYear();

  public readonly nav = [
    { link: '/', label: 'Trang chủ', exact: true },
    { link: '/thuc-don', label: 'Thực đơn tuần', exact: false },
    { link: '/san-pham', label: 'Sản phẩm', exact: false },
    { link: '/chinh-sach', label: 'Chính sách', exact: false },
    { link: '/gioi-thieu', label: 'Giới thiệu', exact: false },
    { link: '/lien-he', label: 'Liên hệ', exact: false },
  ];

  public ngOnInit(): void {
    // Cổng công khai không dùng lớp CSS của màn nghiệp vụ (assets/delicare/css/app.css).
    this.theme.use('w3crm');
    document.title = 'DELI-CARE – Đặt suất ăn, cơm văn phòng';
    this.onCart.set(this.router.url.startsWith('/gio-hang'));
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe((e) => {
      this.menuOpen.set(false);
      this.onCart.set((e as NavigationEnd).urlAfterRedirects.startsWith('/gio-hang'));
    });
  }

  public toggleMenu(): void {
    this.menuOpen.update((v) => !v);
  }
}
