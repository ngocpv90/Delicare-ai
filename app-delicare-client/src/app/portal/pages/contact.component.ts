import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PortalDataService } from '../portal-data.service';

@Component({
  selector: 'app-portal-contact',
  standalone: true,
  imports: [AsyncPipe, RouterLink],
  template: `
    @if (data$ | async; as d) {
      <section class="pt-page-head">
        <div class="pt-wrap">
          <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / Liên hệ</nav>
          <h1>Liên hệ đặt hàng</h1>
          <p>{{ d.company.workingHours }}</p>
        </div>
      </section>

      <section class="pt-section">
        <div class="pt-wrap pt-grid-3">
          <a class="pt-card pt-contact-tile" [href]="'tel:' + d.company.phoneHref">
            <span class="pt-feature-icon"><i class="material-icons">call</i></span>
            <h3>Gọi điện đặt hàng</h3>
            <p class="pt-big">{{ d.company.phone }}</p>
            <span class="pt-link">Gọi ngay <i class="material-icons">arrow_forward</i></span>
          </a>
          @if (d.company.zalo) {
            <a class="pt-card pt-contact-tile" [href]="d.company.zalo" target="_blank" rel="noopener">
              <span class="pt-feature-icon"><i class="material-icons">chat</i></span>
              <h3>Nhắn Zalo</h3>
              <p>Gửi món, số suất, địa chỉ và giờ nhận – nhân viên trả lời trong giờ làm việc.</p>
              <span class="pt-link">Mở Zalo <i class="material-icons">arrow_forward</i></span>
            </a>
          }
          <div class="pt-card pt-contact-tile">
            <span class="pt-feature-icon"><i class="material-icons">place</i></span>
            <h3>Bếp trung tâm</h3>
            <p>{{ d.company.address }}</p>
            @if (d.company.email) {
              <p><a [href]="'mailto:' + d.company.email">{{ d.company.email }}</a></p>
            }
          </div>
        </div>

        <div class="pt-wrap pt-mt">
          <div class="pt-card pt-b2b">
            <div>
              <h2>Khách hàng doanh nghiệp</h2>
              <p>Nhà máy, văn phòng, trường học đặt suất ăn hằng ngày theo hợp đồng: thực đơn theo tuần, giao theo ca, đối soát và xuất hóa đơn theo kỳ.</p>
            </div>
            <a class="pt-btn pt-btn-lg" [href]="'tel:' + d.company.phoneHref"><i class="material-icons">call</i>Nhận báo giá</a>
          </div>
          <p class="pt-muted pt-mt">Đặt lẻ nhanh hơn? <a routerLink="/san-pham">Chọn món tại trang Sản phẩm</a>.</p>
        </div>
      </section>
    }
  `,
})
export class PortalContactComponent {
  public readonly data$ = inject(PortalDataService).data$;
}
