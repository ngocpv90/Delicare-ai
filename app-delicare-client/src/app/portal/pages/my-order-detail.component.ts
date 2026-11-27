import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CustomerAccountService, OrderDetail, TraceInfo } from '../account.service';
import { OrderProgressComponent, viDate, viTime } from '../order-progress.component';

/** Tự tải lại khi đơn còn đang chạy (bếp / giao) để khách thấy bước mới mà không cần bấm. */
const REFRESH_MS = 60000;

/**
 * RETAIL-2: chi tiết 1 đơn của khách – tab Tiến độ & giao hàng (bước bếp + chuyến giao) và tab Nguồn gốc
 * (mẻ nấu, nhiệt độ, nguyên liệu theo lô, nhà cung cấp – RetailPortal/GetOrderTrace).
 */
@Component({
  selector: 'app-portal-my-order-detail',
  standalone: true,
  imports: [DecimalPipe, RouterLink, OrderProgressComponent],
  template: `
    <section class="pt-page-head">
      <div class="pt-wrap">
        <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / <a routerLink="/don-cua-toi">Đơn của tôi</a> / {{ no }}</nav>
        <h1>Đơn {{ no }}</h1>
        @if (o(); as d) { <p>Bữa {{ d.mealType.toLowerCase() }} ngày {{ date(d.deliveryDate) }} · đặt lúc {{ time(d.createdDate) }}</p> }
      </div>
    </section>
    <section class="pt-section">
      <div class="pt-wrap">
        @if (error(); as e) { <div class="pt-alert" role="alert"><i class="material-icons">error_outline</i><span>{{ e }}</span></div> }
        @if (o(); as d) {
          <div class="pt-chips pt-detail-tabs">
            <button type="button" class="pt-chip" [class.active]="tab() === 'progress'" (click)="tab.set('progress')"><i class="material-icons">timeline</i>Tiến độ &amp; giao hàng</button>
            <button type="button" class="pt-chip" [class.active]="tab() === 'trace'" (click)="openTrace()"><i class="material-icons">verified</i>Nguồn gốc suất ăn</button>
          </div>
          <div class="pt-detail">
            <div class="pt-card">
              @if (tab() === 'progress') {
                <app-order-progress [o]="d" />
                <p class="pt-muted pt-mt">Cập nhật lúc {{ time(updatedAt()) }}@if (live(d)) { · tự cập nhật mỗi phút }.
                  <a href="javascript:void(0)" (click)="load()">Tải lại</a></p>
              } @else {
                @if (traceLoading()) {
                  <div class="pt-empty"><i class="material-icons">hourglass_top</i><p>Đang tải nguồn gốc…</p></div>
                } @else {
                @if (trace(); as t) {
                  <div class="pt-trace-head">
                    <p>{{ t.note }}</p>
                    @if (t.listSupplier.length) { <p><b>Nhà cung cấp nguyên liệu:</b> {{ t.listSupplier.join(', ') }}</p> }
                  </div>
                  @for (b of t.listBatch; track b.batchNumber + b.dishName) {
                    <article class="pt-trace-batch">
                      <header>
                        <h3>{{ b.dishName || 'Món' }}</h3>
                        <span class="pt-tag">Mẻ {{ b.batchNumber }}</span>
                        <span class="pt-tag" [class.pt-tag-ok]="b.qcLabel === 'Đạt'">Kiểm tra: {{ b.qcLabel }}</span>
                        @if (b.sampleRetainedAt) { <span class="pt-tag pt-tag-ok">Đã lưu mẫu {{ time(b.sampleRetainedAt) }}</span> }
                      </header>
                      <p class="pt-muted">
                        {{ b.statusLabel }}@if (b.cookStart) { · bắt đầu nấu {{ time(b.cookStart) }} }@if (b.cookEnd) { · xong {{ time(b.cookEnd) }} }
                      </p>
                      @if (b.listTemperature.length) {
                        <p class="pt-temps">
                          @for (x of b.listTemperature; track $index) {
                            <span [class.bad]="x.isWithinRange === false"><i class="material-icons">thermostat</i>{{ x.label }} {{ x.temperatureC }}°C · {{ time(x.measuredAt, false) }}</span>
                          }
                        </p>
                      }
                      @if (b.listIngredient.length) {
                        <div class="pt-table-wrap">
                          <table class="pt-table">
                            <thead><tr><th>Nguyên liệu</th><th>Lô</th><th>Nhà cung cấp</th><th>Ngày nhận</th><th>Hạn dùng</th><th>Kiểm tra</th></tr></thead>
                            <tbody>
                              @for (i of b.listIngredient; track $index) {
                                <tr>
                                  <td>{{ i.ingredientName }}</td><td>{{ i.lotNumber || '—' }}</td><td>{{ i.supplierName || '—' }}</td>
                                  <td>{{ date(i.receivedDate) || '—' }}</td><td>{{ date(i.expiryDate) || '—' }}</td><td>{{ i.qcLabel }}</td>
                                </tr>
                              }
                            </tbody>
                          </table>
                        </div>
                      } @else {
                        <p class="pt-muted">Chưa ghi nhận nguyên liệu xuất kho cho mẻ này.</p>
                      }
                    </article>
                  }
                } @else if (traceError()) {
                  <div class="pt-alert"><i class="material-icons">info</i><span>{{ traceError() }}</span></div>
                }
                }
              }
            </div>
            <aside class="pt-card">
              <h3>Thông tin đơn</h3>
              <p><i class="material-icons pt-inline-icon">person</i>{{ d.customerName }}</p>
              @if (d.address) { <p><i class="material-icons pt-inline-icon">place</i>{{ d.address }}</p> }
              @if (d.note) { <p class="pt-muted">Ghi chú: {{ d.note }}</p> }
              <ul class="pt-track-lines">
                @for (l of d.lines; track $index) {
                  <li><span>{{ l.productName }} × {{ l.quantity }} {{ l.unit }}</span><b>{{ l.lineAmount | number: '1.0-0' : 'vi' }}đ</b></li>
                }
              </ul>
              <div class="pt-cart-total"><span>Tổng tiền</span><strong>{{ d.totalAmount | number: '1.0-0' : 'vi' }}đ</strong></div>
              <p class="pt-muted">{{ d.paymentStatus === 'PAID' ? 'Đã thanh toán.' : 'Thanh toán khi nhận hàng.' }}</p>
            </aside>
          </div>
        } @else if (!error()) {
          <div class="pt-empty"><i class="material-icons">hourglass_top</i><p>Đang tải đơn…</p></div>
        }
      </div>
    </section>
  `,
})
export class PortalMyOrderDetailComponent implements OnInit {
  private readonly account = inject(CustomerAccountService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  public readonly no = (inject(ActivatedRoute).snapshot.paramMap.get('so') || '').toUpperCase();
  public readonly o = signal<OrderDetail | null>(null);
  public readonly error = signal('');
  public readonly updatedAt = signal('');
  public readonly tab = signal<'progress' | 'trace'>(inject(ActivatedRoute).snapshot.queryParamMap.get('tab') === 'nguon-goc' ? 'trace' : 'progress');
  public readonly trace = signal<TraceInfo | null>(null);
  public readonly traceLoading = signal(false);
  public readonly traceError = signal('');

  public ngOnInit(): void {
    if (!this.account.loggedIn()) {
      this.router.navigate(['/tai-khoan/dang-nhap'], { queryParams: { ve: '/don-cua-toi/' + this.no }, replaceUrl: true });
      return;
    }
    this.load();
    if (this.tab() === 'trace') this.openTrace();
    const timer = setInterval(() => {
      const d = this.o();
      if (d && this.live(d) && this.tab() === 'progress' && document.visibilityState === 'visible') this.load();
    }, REFRESH_MS);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  public live(d: OrderDetail): boolean {
    return !['DELIVERED', 'CANCELLED', 'RECEIVED'].includes(d.stageCode);
  }

  public load(): void {
    this.account.myOrder(this.no).subscribe((r) => {
      if (r.ok) {
        this.o.set(r.data);
        this.updatedAt.set(this.nowIso());
      } else if (r.expired) this.router.navigate(['/tai-khoan/dang-nhap'], { queryParams: { ve: '/don-cua-toi/' + this.no } });
      else this.error.set(r.message);
    });
  }

  public openTrace(): void {
    this.tab.set('trace');
    if (this.trace() || this.traceLoading()) return;
    this.traceLoading.set(true);
    this.traceError.set('');
    this.account.trace(this.no).subscribe((r) => {
      this.traceLoading.set(false);
      if (r.ok) this.trace.set(r.data);
      else this.traceError.set(r.message);
    });
  }

  public date(iso?: string | null): string {
    return iso ? viDate(iso) : '';
  }

  public time(s?: string | null, withDate = true): string {
    return viTime(s, withDate);
  }

  private nowIso(): string {
    const d = new Date();
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
  }
}
