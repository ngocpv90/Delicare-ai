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
        <div class="pt-wrap">
          <!-- Thanh tra cứu trải ngang (anh báo 2026-11-27: form bé, khoảng trắng bên phải to) -->
          <form class="pt-card pt-form pt-track-search" [formGroup]="form" (ngSubmit)="search()" novalidate>
            <div class="pt-track-search-row">
              <label>
                <span>Mã đơn <b>*</b></span>
                <span class="pt-input-icon"><i class="material-icons">receipt_long</i><input formControlName="orderNumber" placeholder="BL-261128-0001" autocomplete="off" /></span>
              </label>
              <label>
                <span>Số điện thoại đặt hàng <b>*</b></span>
                <span class="pt-input-icon"><i class="material-icons">call</i><input formControlName="phone" type="tel" autocomplete="tel" placeholder="09xx xxx xxx" /></span>
              </label>
              <button type="submit" class="pt-btn pt-btn-lg" [disabled]="loading()">
                <i class="material-icons">search</i>{{ loading() ? 'Đang tra cứu…' : 'Tra cứu' }}
              </button>
            </div>
            @if (error(); as e) {
              <div class="pt-alert" role="alert"><i class="material-icons">error_outline</i><span>{{ e }}</span></div>
            }
            <p class="pt-muted pt-track-note">
              <i class="material-icons pt-inline-icon">info</i>Cần đổi / hủy đơn: gọi <a [href]="'tel:' + d.company.phoneHref">{{ d.company.phone }}</a> trước giờ chốt.
              <span class="pt-sep">·</span><a routerLink="/tai-khoan/dang-nhap">Đăng nhập</a> để xem mọi đơn và truy xuất nguồn gốc suất ăn.
            </p>
          </form>

          @if (result(); as o) {
            <article class="pt-card pt-track-result">
              <header>
                <div>
                  <small class="pt-muted">Mã đơn</small>
                  <h2>{{ o.orderNumber }}</h2>
                  <p class="pt-muted">Bữa {{ o.mealType.toLowerCase() }} ngày {{ ddmm(o.deliveryDate) }} · Người nhận: {{ o.customerName }}</p>
                </div>
                <span class="pt-track-status" [class.cancel]="o.orderStatus === 'Cancelled'">{{ o.stageLabel || statusLabel(o.orderStatus) }}</span>
              </header>
              <div class="pt-track-result-grid">
                <div>
                  @if (o.listStep.length) {
                    <app-order-progress [o]="o" />
                  } @else if (o.cancelReason) {
                    <p class="pt-muted">Lý do hủy: {{ o.cancelReason }}</p>
                  }
                </div>
                <aside class="pt-track-summary">
                  <h3>Món đã đặt</h3>
                  <ul class="pt-track-lines">
                    @for (l of o.lines; track $index) {
                      <li><span>{{ l.productName }} × {{ l.quantity }} {{ l.unit }}</span><b>{{ l.lineAmount | number: '1.0-0' : 'vi' }}đ</b></li>
                    }
                  </ul>
                  <div class="pt-cart-total"><span>Tổng tiền</span><strong>{{ o.totalAmount | number: '1.0-0' : 'vi' }}đ</strong></div>
                  <p class="pt-muted">{{ o.paymentStatus === 'PAID' ? 'Đã thanh toán.' : 'Thanh toán khi nhận hàng.' }}</p>
                </aside>
              </div>
            </article>
          } @else {
            <!-- Chưa tra cứu: hướng dẫn thay cho khoảng trắng -->
            <div class="pt-track-guide">
              <div class="pt-card pt-track-flow">
                <h3>Đơn của bạn sẽ đi qua các bước</h3>
                <ol>
                  @for (g of guide; track g.label) {
                    <li><span class="dot"><i class="material-icons">{{ g.icon }}</i></span><b>{{ g.label }}</b><small>{{ g.note }}</small></li>
                  }
                </ol>
              </div>
              <div class="pt-card pt-track-tip">
                <i class="material-icons">receipt_long</i>
                <div><h3>Mã đơn ở đâu?</h3><p class="pt-muted">Mã đơn dạng <b>BL-…</b> hiện ngay sau khi bạn gửi đơn và trong tin nhân viên gọi / nhắn xác nhận.</p></div>
              </div>
              <div class="pt-card pt-track-tip">
                <i class="material-icons">account_circle</i>
                <div><h3>Không cần nhớ mã đơn</h3><p class="pt-muted">Có tài khoản thì mọi đơn nằm trong <b>Đơn của tôi</b>, kèm chuyến giao và nguồn gốc suất ăn.</p>
                  <a class="pt-btn pt-btn-sm pt-btn-ghost" routerLink="/tai-khoan/dang-nhap" [queryParams]="{ tao: 1 }">Tạo tài khoản</a></div>
              </div>
            </div>
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
  /** Hướng dẫn khi chưa tra cứu – cùng các bước khách thấy trong tiến độ đơn (RETAIL-2). */
  public readonly guide = [
    { icon: 'receipt_long', label: 'Nhận đơn', note: 'Đơn vào hệ thống' },
    { icon: 'task_alt', label: 'Xác nhận', note: 'Nhân viên gọi xác nhận' },
    { icon: 'content_cut', label: 'Sơ chế', note: 'Nguyên liệu đã kiểm tra' },
    { icon: 'soup_kitchen', label: 'Nấu', note: 'Đo nhiệt độ lõi món' },
    { icon: 'inventory_2', label: 'Đóng gói', note: 'Lưu mẫu, niêm phong' },
    { icon: 'local_shipping', label: 'Giao hàng', note: 'Xe giữ nhiệt' },
    { icon: 'check_circle', label: 'Đã giao', note: 'Thanh toán khi nhận' },
  ];
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
