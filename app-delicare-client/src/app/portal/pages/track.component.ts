import { AsyncPipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PortalDataService } from '../portal-data.service';
import { RetailApiService } from '../retail-api.service';
import { OrderDetail } from '../account.service';
import { OrderProgressComponent } from '../order-progress.component';

/** Trạng thái đơn khách lẻ → chữ hiển thị + bước trên thanh tiến trình (RETAIL-1 mục 4). */
const STEPS = [
  { code: 'New', label: 'Đã nhận đơn' },
  { code: 'Confirmed', label: 'Đã xác nhận' },
  { code: 'Delivering', label: 'Đang giao' },
  { code: 'Completed', label: 'Hoàn thành' },
];

/**
 * Tra cứu đơn (không cần tài khoản): mã đơn + SĐT đặt hàng – cả 2 phải khớp (RetailPortal/TrackOrder).
 * Chưa có API → báo gọi điện hỏi tình trạng đơn.
 */
@Component({
  selector: 'app-portal-track',
  standalone: true,
  imports: [AsyncPipe, DecimalPipe, RouterLink, ReactiveFormsModule, OrderProgressComponent],
  template: `
    @if (data$ | async; as d) {
      <section class="pt-page-head">
        <div class="pt-wrap">
          <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / Tra cứu đơn</nav>
          <h1>Tra cứu đơn hàng</h1>
          <p>Nhập mã đơn (dạng BL-…) và số điện thoại đã dùng khi đặt.</p>
        </div>
      </section>
      <section class="pt-section">
        <div class="pt-wrap pt-track">
          <form class="pt-card pt-form pt-track-form" [formGroup]="form" (ngSubmit)="search()" novalidate>
            <label>
              <span>Mã đơn <b>*</b></span>
              <input formControlName="orderNumber" placeholder="BL-261128-0001" autocomplete="off" />
            </label>
            <label>
              <span>Số điện thoại đặt hàng <b>*</b></span>
              <input formControlName="phone" type="tel" autocomplete="tel" placeholder="09xx xxx xxx" />
            </label>
            @if (error(); as e) {
              <div class="pt-alert" role="alert"><i class="material-icons">error_outline</i><span>{{ e }}</span></div>
            }
            <button type="submit" class="pt-btn pt-btn-lg pt-btn-block" [disabled]="loading()">
              <i class="material-icons">search</i>{{ loading() ? 'Đang tra cứu…' : 'Tra cứu' }}
            </button>
            <p class="pt-muted">Cần đổi / hủy đơn: gọi <a [href]="'tel:' + d.company.phoneHref">{{ d.company.phone }}</a> trước giờ chốt.</p>
            <p class="pt-muted"><a routerLink="/tai-khoan/dang-nhap">Đăng nhập</a> để xem mọi đơn của bạn và truy xuất nguồn gốc suất ăn.</p>
          </form>

          @if (result(); as o) {
            <article class="pt-card pt-track-result">
              <header>
                <div>
                  <small class="pt-muted">Mã đơn</small>
                  <h2>{{ o.orderNumber }}</h2>
                </div>
                <span class="pt-track-status" [class.cancel]="o.orderStatus === 'Cancelled'">{{ o.stageLabel || statusLabel(o.orderStatus) }}</span>
              </header>
              @if (o.listStep.length) {
                <app-order-progress [o]="o" />
              } @else if (o.cancelReason) {
                <p class="pt-muted">Lý do hủy: {{ o.cancelReason }}</p>
              }
              <p>Nhận: bữa {{ o.mealType.toLowerCase() }} ngày {{ ddmm(o.deliveryDate) }} · Người nhận: {{ o.customerName }}</p>
              <ul class="pt-track-lines">
                @for (l of o.lines; track $index) {
                  <li><span>{{ l.productName }} × {{ l.quantity }} {{ l.unit }}</span><b>{{ l.lineAmount | number: '1.0-0' : 'vi' }}đ</b></li>
                }
              </ul>
              <div class="pt-cart-total"><span>Tổng tiền</span><strong>{{ o.totalAmount | number: '1.0-0' : 'vi' }}đ</strong></div>
              @if (o.paymentStatus === 'PAID') {
                <p class="pt-muted">Đã thanh toán.</p>
              }
            </article>
          }
        </div>
      </section>
    }
  `,
})
export class PortalTrackComponent {
  public readonly data$ = inject(PortalDataService).data$;
  private readonly api = inject(RetailApiService);
  public readonly steps = STEPS;
  public readonly loading = signal(false);
  public readonly error = signal('');
  public readonly result = signal<OrderDetail | null>(null);

  public readonly form = inject(FormBuilder).nonNullable.group({
    orderNumber: [inject(ActivatedRoute).snapshot.queryParamMap.get('ma') ?? '', Validators.required],
    phone: ['', Validators.required],
  });

  public search(): void {
    if (this.loading()) return;
    const orderNumber = this.form.controls.orderNumber.value.trim().toUpperCase();
    const phone = this.form.controls.phone.value.replace(/[\s.\-]/g, '');
    this.error.set('');
    this.result.set(null);
    if (!/^BL-\d{6}-\d{4}$/.test(orderNumber)) return this.error.set('Mã đơn có dạng BL-YYMMDD-NNNN (ví dụ BL-261128-0001).');
    if (!/^(0|\+84)(3|5|7|8|9)\d{8}$/.test(phone)) return this.error.set('Số điện thoại chưa đúng.');
    this.loading.set(true);
    this.api.track(orderNumber, phone).subscribe((r) => {
      this.loading.set(false);
      if (r.ok) this.result.set(r.data);
      else this.error.set(r.message);
    });
  }

  public statusLabel(s: string): string {
    return s === 'Cancelled' ? 'Đã hủy' : (STEPS.find((x) => x.code === s)?.label ?? 'Đang xử lý');
  }

  public stepIndex(s: string): number {
    return STEPS.findIndex((x) => x.code === s);
  }

  public ddmm(iso: string): string {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso || '');
    return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
  }
}
