import { AsyncPipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CartService } from '../cart.service';
import { PortalDataService } from '../portal-data.service';
import { PortalCompany } from '../portal.model';

const VN_PHONE = /^(0|\+84)(3|5|7|8|9)\d{8}$/;

/**
 * Giỏ hàng + thông tin nhận hàng.
 * Chưa có API nhận đơn bán lẻ (GAP-POS-01, R2 – PROPOSED – BA/PO CONFIRM): bấm "Gửi đơn" chỉ kiểm tra thông tin,
 * dựng nội dung đơn để khách gọi / nhắn Zalo – KHÔNG lưu đơn lên máy chủ, nói rõ với khách.
 */
@Component({
  selector: 'app-portal-cart',
  standalone: true,
  imports: [AsyncPipe, DecimalPipe, RouterLink, ReactiveFormsModule],
  template: `
    @if (data$ | async; as d) {
      <section class="pt-page-head">
        <div class="pt-wrap">
          <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / Giỏ hàng</nav>
          <h1>Giỏ hàng</h1>
        </div>
      </section>

      <section class="pt-section">
        <div class="pt-wrap">
          @if (!cart.items().length) {
            <div class="pt-empty">
              <i class="material-icons">remove_shopping_cart</i>
              <p>Giỏ hàng đang trống.</p>
              <a class="pt-btn" routerLink="/san-pham">Chọn món</a>
            </div>
          } @else {
            <div class="pt-cart-layout">
              <div class="pt-card pt-cart-list">
                @for (l of cart.items(); track l.productId) {
                  <div class="pt-cart-line">
                    <div class="pt-cart-name">
                      <strong>{{ l.name }}</strong>
                      <small>{{ l.price | number: '1.0-0' : 'vi' }}đ / {{ l.unit }}</small>
                    </div>
                    <div class="pt-qty">
                      <button type="button" (click)="cart.setQuantity(l.productId, l.quantity - 1)" aria-label="Giảm">
                        <i class="material-icons">remove</i>
                      </button>
                      <input type="number" min="1" max="999" [value]="l.quantity" (change)="cart.setQuantity(l.productId, $any($event.target).value)" aria-label="Số lượng" />
                      <button type="button" (click)="cart.setQuantity(l.productId, l.quantity + 1)" aria-label="Tăng">
                        <i class="material-icons">add</i>
                      </button>
                    </div>
                    <div class="pt-cart-sum">{{ l.price * l.quantity | number: '1.0-0' : 'vi' }}đ</div>
                    <button type="button" class="pt-icon-btn" (click)="cart.remove(l.productId)" aria-label="Bỏ khỏi giỏ">
                      <i class="material-icons">delete_outline</i>
                    </button>
                  </div>
                }
                <div class="pt-cart-total">
                  <span>Tạm tính</span>
                  <strong>{{ cart.total() | number: '1.0-0' : 'vi' }}đ</strong>
                </div>
                <p class="pt-muted">Phí giao (nếu có) được báo khi nhân viên xác nhận đơn. <a routerLink="/chinh-sach/giao-hang">Chính sách giao hàng</a></p>
              </div>

              <form class="pt-card pt-form" [formGroup]="form" (ngSubmit)="submit(d.company)" novalidate>
                <h2>Thông tin nhận hàng</h2>
                <label>
                  <span>Họ tên <b>*</b></span>
                  <input formControlName="name" autocomplete="name" />
                  @if (invalid('name')) { <em>Vui lòng nhập họ tên.</em> }
                </label>
                <label>
                  <span>Số điện thoại <b>*</b></span>
                  <input formControlName="phone" type="tel" autocomplete="tel" placeholder="09xx xxx xxx" />
                  @if (invalid('phone')) { <em>Số điện thoại chưa đúng.</em> }
                </label>
                <label>
                  <span>Địa chỉ giao <b>*</b></span>
                  <input formControlName="address" autocomplete="street-address" />
                  @if (invalid('address')) { <em>Vui lòng nhập địa chỉ giao.</em> }
                </label>
                <div class="pt-form-row">
                  <label>
                    <span>Ngày nhận <b>*</b></span>
                    <input formControlName="date" type="date" [min]="minDate" />
                    @if (invalid('date')) { <em>Chọn ngày nhận.</em> }
                  </label>
                  <label>
                    <span>Bữa <b>*</b></span>
                    <select formControlName="meal">
                      @for (m of d.weeklyMenu.meals; track m) {
                        <option [value]="m">Bữa {{ m.toLowerCase() }}</option>
                      }
                    </select>
                  </label>
                </div>
                <label>
                  <span>Ghi chú</span>
                  <textarea formControlName="note" rows="3" placeholder="Ít cay, giao tới lễ tân…"></textarea>
                </label>
                <button type="submit" class="pt-btn pt-btn-lg pt-btn-block"><i class="material-icons">send</i>Gửi đơn</button>
                <p class="pt-muted">Bằng việc gửi đơn, bạn đồng ý với <a routerLink="/chinh-sach/dat-hang">chính sách đặt hàng</a> và <a routerLink="/chinh-sach/bao-mat">bảo mật thông tin</a>.</p>
              </form>
            </div>
          }
        </div>
      </section>

      @if (summary(); as s) {
        <div class="pt-modal-backdrop" (click)="summary.set('')">
          <div class="pt-card pt-modal" role="dialog" aria-modal="true" aria-labelledby="pt-order-title" (click)="$event.stopPropagation()">
            <h2 id="pt-order-title"><i class="material-icons">receipt_long</i>Hoàn tất đặt hàng qua hotline</h2>
            <p>Đặt hàng trực tuyến đang được nâng cấp – đơn <b>chưa được gửi tự động</b>. Vui lòng gọi hoặc nhắn Zalo và đọc / dán nội dung dưới đây, nhân viên sẽ xác nhận ngay.</p>
            <pre class="pt-order-text">{{ s }}</pre>
            <div class="pt-modal-actions">
              <button type="button" class="pt-btn pt-btn-ghost" (click)="copy(s)">
                <i class="material-icons">{{ copied() ? 'check' : 'content_copy' }}</i>{{ copied() ? 'Đã sao chép' : 'Sao chép đơn' }}
              </button>
              @if (d.company.zalo) {
                <a class="pt-btn pt-btn-ghost" [href]="d.company.zalo" target="_blank" rel="noopener"><i class="material-icons">chat</i>Nhắn Zalo</a>
              }
              <a class="pt-btn" [href]="'tel:' + d.company.phoneHref"><i class="material-icons">call</i>Gọi {{ d.company.phone }}</a>
            </div>
            <button type="button" class="pt-modal-close" (click)="summary.set('')" aria-label="Đóng"><i class="material-icons">close</i></button>
          </div>
        </div>
      }
    }
  `,
})
export class PortalCartComponent {
  public readonly data$ = inject(PortalDataService).data$;
  public readonly cart = inject(CartService);
  public readonly summary = signal('');
  public readonly copied = signal(false);
  public readonly minDate = this.isoDate(new Date());

  public readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.required, Validators.pattern(VN_PHONE)]],
    address: ['', [Validators.required, Validators.maxLength(250)]],
    date: [this.isoDate(new Date(Date.now() + 864e5)), Validators.required],
    meal: ['Trưa', Validators.required],
    note: ['', Validators.maxLength(500)],
  });

  public invalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  public submit(company: PortalCompany): void {
    // Chuẩn hóa SĐT trước khi kiểm (bỏ khoảng trắng, dấu chấm).
    const phone = this.form.controls.phone;
    phone.setValue(phone.value.replace(/[\s.\-]/g, ''));
    if (this.form.invalid || !this.cart.items().length) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const fmt = (n: number) => n.toLocaleString('vi-VN') + 'đ';
    const [y, m, dd] = v.date.split('-');
    const lines = this.cart.items().map((l) => `- ${l.name} x ${l.quantity} ${l.unit} = ${fmt(l.price * l.quantity)}`);
    this.copied.set(false);
    this.summary.set(
      [
        `ĐƠN ĐẶT HÀNG ${company.brand}`,
        ...lines,
        `Tạm tính: ${fmt(this.cart.total())}`,
        `Người nhận: ${v.name.trim()} – ${v.phone}`,
        `Địa chỉ: ${v.address.trim()}`,
        `Nhận: bữa ${v.meal.toLowerCase()} ngày ${dd}/${m}/${y}`,
        v.note.trim() ? `Ghi chú: ${v.note.trim()}` : '',
      ]
        .filter(Boolean)
        .join('\n'),
    );
  }

  public copy(text: string): void {
    navigator.clipboard?.writeText(text).then(
      () => this.copied.set(true),
      () => this.copied.set(false),
    );
  }

  private isoDate(d: Date): string {
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
  }
}
