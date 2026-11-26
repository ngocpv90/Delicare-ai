import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PortalDataService } from '../portal-data.service';
import { PortalProductCardComponent } from '../product-card.component';
import { PortalData, PortalMenuDay } from '../portal.model';

@Component({
  selector: 'app-portal-home',
  standalone: true,
  imports: [AsyncPipe, RouterLink, PortalProductCardComponent],
  template: `
    @if (data$ | async; as d) {
      <section class="pt-hero">
        <div class="pt-wrap pt-hero-in">
          <div class="pt-hero-text">
            <span class="pt-eyebrow">{{ d.hero.eyebrow }}</span>
            <h1>{{ d.hero.title }}</h1>
            <p>{{ d.hero.subtitle }}</p>
            <div class="pt-hero-cta">
              <a class="pt-btn pt-btn-lg" routerLink="/san-pham"><i class="material-icons">restaurant</i>Đặt món ngay</a>
              <a class="pt-btn pt-btn-lg pt-btn-ghost" routerLink="/thuc-don"><i class="material-icons">calendar_month</i>Xem thực đơn tuần</a>
            </div>
          </div>
          @if (today(d); as t) {
            <div class="pt-card pt-today">
              <div class="pt-today-head">
                <i class="material-icons">today</i>
                <div>
                  <small>Thực đơn hôm nay</small>
                  <strong>{{ t.day }}</strong>
                </div>
              </div>
              @for (m of d.weeklyMenu.meals; track m) {
                @if (t.meals[m]; as dishes) {
                  <div class="pt-today-meal">
                    <span class="pt-meal-chip">{{ m }}</span>
                    <span>{{ dishes.join(' · ') }}</span>
                  </div>
                }
              }
              <a class="pt-link" routerLink="/thuc-don">Cả tuần <i class="material-icons">arrow_forward</i></a>
            </div>
          }
        </div>
      </section>

      <section class="pt-section">
        <div class="pt-wrap pt-grid-4">
          @for (h of d.highlights; track h.title) {
            <div class="pt-feature">
              <span class="pt-feature-icon"><i class="material-icons">{{ h.icon }}</i></span>
              <h3>{{ h.title }}</h3>
              <p>{{ h.text }}</p>
            </div>
          }
        </div>
      </section>

      <section class="pt-section pt-section-alt">
        <div class="pt-wrap">
          <div class="pt-section-head">
            <div>
              <h2>Món được đặt nhiều</h2>
              <p>Suất ăn nấu trong ngày, đóng hộp kín, giao theo buổi.</p>
            </div>
            <a class="pt-link" routerLink="/san-pham">Tất cả sản phẩm <i class="material-icons">arrow_forward</i></a>
          </div>
          <div class="pt-grid-products">
            @for (p of featured(d); track p.id) {
              <app-portal-product-card [product]="p" />
            }
          </div>
        </div>
      </section>

      <section class="pt-section">
        <div class="pt-wrap">
          <div class="pt-section-head pt-center">
            <div>
              <h2>Đặt hàng chỉ 4 bước</h2>
              <p>Đơn cho ngày mai chốt trước 20:00 hôm nay.</p>
            </div>
          </div>
          <ol class="pt-steps">
            @for (s of d.steps; track s.title; let i = $index) {
              <li>
                <span class="pt-step-no">{{ i + 1 }}</span>
                <h3>{{ s.title }}</h3>
                <p>{{ s.text }}</p>
              </li>
            }
          </ol>
        </div>
      </section>

      <section class="pt-section pt-section-alt">
        <div class="pt-wrap">
          <div class="pt-section-head">
            <div>
              <h2>Chính sách mua hàng</h2>
              <p>Rõ ràng trước khi đặt – đổi, hủy, giao nhận và thanh toán.</p>
            </div>
          </div>
          <div class="pt-grid-3">
            @for (p of d.policies; track p.slug) {
              <a class="pt-card pt-policy-tile" [routerLink]="['/chinh-sach', p.slug]">
                <i class="material-icons">{{ p.icon }}</i>
                <div>
                  <h3>{{ p.title }}</h3>
                  <p>{{ p.sections.length ? p.sections[0].items[0] : "" }}</p>
                </div>
              </a>
            }
          </div>
        </div>
      </section>

      <section class="pt-cta-band">
        <div class="pt-wrap pt-cta-in">
          <div>
            <h2>Đặt suất ăn cho văn phòng, nhà máy?</h2>
            <p>Gọi cho chúng tôi để nhận báo giá theo số suất và thực đơn riêng.</p>
          </div>
          <div class="pt-hero-cta">
            <a class="pt-btn pt-btn-lg pt-btn-light" [href]="'tel:' + d.company.phoneHref"><i class="material-icons">call</i>{{ d.company.phone }}</a>
            <a class="pt-btn pt-btn-lg pt-btn-outline-light" routerLink="/lien-he">Gửi yêu cầu</a>
          </div>
        </div>
      </section>
    }
  `,
})
export class PortalHomeComponent {
  public readonly data$ = inject(PortalDataService).data$;

  public featured(d: PortalData) {
    const tagged = d.products.filter((p) => p.tags.length);
    return (tagged.length >= 4 ? tagged : d.products).slice(0, 8);
  }

  /** Thực đơn của hôm nay theo thứ trong tuần; Chủ nhật / ngoài danh sách → ngày đầu tuần. */
  public today(d: PortalData): PortalMenuDay | undefined {
    const days = d.weeklyMenu.days;
    const dow = new Date().getDay(); // 0 = Chủ nhật
    return days[dow - 1] ?? days[0];
  }
}
