import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CustomerAccountService, MyOrderItem } from '../account.service';
import { viDate } from '../order-progress.component';

const GROUPS = [
  { code: 'active', label: 'Đang xử lý / đang giao' },
  { code: 'done', label: 'Đã giao' },
  { code: 'cancelled', label: 'Đã hủy' },
];

/** RETAIL-2: Đơn của tôi – nhóm Đang xử lý (gồm đang giao) / Đã giao / Đã hủy; thêm đơn đặt trước khi có tài khoản bằng số đơn. */
@Component({
  selector: 'app-portal-my-orders',
  standalone: true,
  imports: [DecimalPipe, FormsModule, RouterLink],
  template: `
    <section class="pt-page-head">
      <div class="pt-wrap">
        <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / Đơn của tôi</nav>
        <h1>Đơn của tôi</h1>
        @if (account.profile(); as p) {
          <p>Xin chào {{ p.fullName }} · {{ p.phoneNumber }} · <a routerLink="/tai-khoan">Thông tin tài khoản</a></p>
        }
      </div>
    </section>
    <section class="pt-section">
      <div class="pt-wrap">
        <div class="pt-toolbar">
          <div class="pt-chips">
            @for (g of groups; track g.code) {
              <button type="button" class="pt-chip" [class.active]="group() === g.code" (click)="pick(g.code)">
                {{ g.label }}@if (counts()[g.code] != null) { <span class="pt-chip-count">{{ counts()[g.code] }}</span> }
              </button>
            }
          </div>
          <a class="pt-btn pt-btn-sm" routerLink="/san-pham"><i class="material-icons">add_shopping_cart</i>Đặt món</a>
        </div>

        @if (error(); as e) { <div class="pt-alert" role="alert"><i class="material-icons">error_outline</i><span>{{ e }}</span></div> }

        @if (loading()) {
          <div class="pt-empty"><i class="material-icons">hourglass_top</i><p>Đang tải đơn…</p></div>
        } @else if (!items().length) {
          <div class="pt-empty">
            <i class="material-icons">receipt_long</i>
            <p>{{ group() === 'active' ? 'Chưa có đơn nào đang xử lý.' : group() === 'done' ? 'Chưa có đơn đã giao.' : 'Không có đơn đã hủy.' }}</p>
          </div>
        } @else {
          <div class="pt-orders">
            @for (o of items(); track o.orderNumber) {
              <a class="pt-card pt-order-card" [routerLink]="['/don-cua-toi', o.orderNumber]">
                <header>
                  <b>{{ o.orderNumber }}</b>
                  <span class="pt-stage-chip" [class]="'s-' + o.stageCode.toLowerCase()">{{ o.stageLabel }}</span>
                </header>
                <p><i class="material-icons">event</i>Bữa {{ o.mealType.toLowerCase() }} · {{ date(o.deliveryDate) }}</p>
                <p class="pt-muted">{{ o.lineSummary }}</p>
                <footer>
                  <strong>{{ o.totalAmount | number: '1.0-0' : 'vi' }}đ</strong>
                  <span class="pt-link">@if (o.hasRoute && o.stageCode === 'DELIVERING') { Theo dõi chuyến } @else { Xem chi tiết }<i class="material-icons">chevron_right</i></span>
                </footer>
              </a>
            }
          </div>
        }

        <details class="pt-card pt-claim">
          <summary><i class="material-icons">playlist_add</i>Đơn đặt trước khi có tài khoản? Thêm vào Đơn của tôi</summary>
          <p class="pt-muted">Nhập số đơn (BL-…) đã đặt bằng số điện thoại {{ account.profile()?.phoneNumber }}.</p>
          <div class="pt-claim-row">
            <input [(ngModel)]="claimNo" placeholder="BL-261128-0001" autocomplete="off" />
            <button type="button" class="pt-btn" [disabled]="claiming()" (click)="claim()"><i class="material-icons">add</i>Thêm</button>
          </div>
          @if (claimMsg(); as m) { <p [class.pt-ok-text]="claimOk()" [class.pt-err-text]="!claimOk()">{{ m }}</p> }
        </details>
      </div>
    </section>
  `,
})
export class PortalMyOrdersComponent implements OnInit {
  public readonly account = inject(CustomerAccountService);
  private readonly router = inject(Router);
  public readonly groups = GROUPS;
  public readonly group = signal('active');
  public readonly items = signal<MyOrderItem[]>([]);
  public readonly counts = signal<Record<string, number>>({});
  public readonly loading = signal(true);
  public readonly error = signal('');
  public claimNo = '';
  public readonly claiming = signal(false);
  public readonly claimMsg = signal('');
  public readonly claimOk = signal(false);

  public ngOnInit(): void {
    if (!this.account.loggedIn()) {
      this.router.navigate(['/tai-khoan/dang-nhap'], { queryParams: { ve: '/don-cua-toi' }, replaceUrl: true });
      return;
    }
    this.load();
  }

  public pick(g: string): void {
    this.group.set(g);
    this.load();
  }

  public date(iso: string): string {
    return viDate(iso);
  }

  public claim(): void {
    const no = this.claimNo.trim().toUpperCase();
    if (!/^BL-\d{6}-\d{4}$/.test(no)) {
      this.claimOk.set(false);
      this.claimMsg.set('Số đơn có dạng BL-YYMMDD-NNNN.');
      return;
    }
    this.claiming.set(true);
    this.account.claim(no).subscribe((r) => {
      this.claiming.set(false);
      this.claimOk.set(r.ok);
      this.claimMsg.set(r.ok ? 'Đã thêm đơn ' + no + '.' : r.message);
      if (r.ok) {
        this.claimNo = '';
        this.load();
      } else if (r.expired) this.toLogin();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');
    this.account.myOrders(this.group()).subscribe((r) => {
      this.loading.set(false);
      if (r.ok) {
        this.items.set(r.data.listItem);
        this.counts.set(r.data.groupCounts);
      } else if (r.expired) this.toLogin();
      else this.error.set(r.message);
    });
  }

  private toLogin(): void {
    this.router.navigate(['/tai-khoan/dang-nhap'], { queryParams: { ve: '/don-cua-toi' } });
  }
}
