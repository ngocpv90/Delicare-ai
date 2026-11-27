import { AsyncPipe, DecimalPipe } from '@angular/common';
import { Component, HostListener, Injector, afterNextRender, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CartService } from '../cart.service';
import { CustomerAccountService } from '../account.service';
import { RetailApiService } from '../retail-api.service';
import { PortalDataService } from '../portal-data.service';
import { PortalCompany, PortalData } from '../portal.model';

const VN_PHONE = /^(0|\+84)(3|5|7|8|9)\d{8}$/;
const DEFAULT_CUTOFF_HOUR = 20;

/** Bỏ khoảng trắng / dấu chấm / gạch – khách hay gõ "0912 345 678". */
const normalizePhone = (s: string) => s.replace(/[\s.\-]/g, '');

/** Kiểm SĐT di động VN sau khi chuẩn hóa (không báo lỗi khi khách gõ có dấu cách). */
function vnPhone(c: AbstractControl): ValidationErrors | null {
  const v = normalizePhone(String(c.value ?? ''));
  return !v || VN_PHONE.test(v) ? null : { phone: true };
}

/** Ngày nhận không sớm hơn ngày nhận sớm nhất (chặn cả khi gõ tay qua thuộc tính min). */
function notBefore(c: AbstractControl, min: () => string): ValidationErrors | null {
  return c.value && c.value < min() ? { tooEarly: true } : null;
}

/** Mã ngẫu nhiên cho mỗi lần đặt (crypto.randomUUID chỉ có trên https / localhost → dự phòng cho server http). */
function newRequestId(): string {
  try {
    if (crypto && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  } catch {
    /* bỏ qua */
  }
  return Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 12) + Math.random().toString(36).slice(2, 12);
}

/**
 * Giỏ hàng + thông tin nhận hàng + gửi đơn (RETAIL-1 – docs/frontend-handoff/RETAIL-1.md mục 5.1).
 * Máy chủ chưa có API → khách đặt qua điện thoại / Zalo (trang nói rõ đơn chưa được gửi).
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
          @if (done(); as ok) {
            <div class="pt-card pt-success">
              <span class="pt-success-icon"><i class="material-icons">check_circle</i></span>
              <h2>Đặt hàng thành công</h2>
              <p>Mã đơn của bạn: <b class="pt-order-no">{{ ok.orderNumber }}</b></p>
              <p>Nhân viên sẽ gọi số <b>{{ ok.phone }}</b> để xác nhận đơn và giờ giao (bữa {{ ok.meal.toLowerCase() }} ngày {{ ok.date }}).</p>
              @if (ok.totalAmount) {
                <p>Tổng tiền: <b>{{ ok.totalAmount | number: '1.0-0' : 'vi' }}đ</b> – phí giao (nếu có) báo khi xác nhận.</p>
              }
              @if (account.loggedIn()) {
                <p><a class="pt-btn pt-btn-sm" [routerLink]="['/don-cua-toi', ok.orderNumber]"><i class="material-icons">timeline</i>Theo dõi đơn trong Đơn của tôi</a></p>
              } @else {
                <p class="pt-muted"><a routerLink="/tai-khoan/dang-nhap" [queryParams]="{ tao: 1 }">Tạo tài khoản</a> để theo dõi đơn, chuyến giao và nguồn gốc suất ăn (thêm đơn này bằng số đơn).</p>
              }
              <p class="pt-muted">Xem tình trạng đơn tại <a [routerLink]="['/tra-cuu-don']" [queryParams]="{ ma: ok.orderNumber }">Tra cứu đơn</a>. Cần đổi hoặc hủy đơn, vui lòng gọi <a [href]="'tel:' + d.company.phoneHref">{{ d.company.phone }}</a> và đọc mã đơn trước giờ chốt.</p>
              <div class="pt-hero-cta pt-center">
                <a class="pt-btn" routerLink="/san-pham" (click)="done.set(null)"><i class="material-icons">restaurant</i>Đặt thêm món</a>
                <a class="pt-btn pt-btn-ghost" routerLink="/">Về trang chủ</a>
              </div>
            </div>
          } @else if (!cart.items().length) {
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
                      <input type="number" min="1" max="999" [value]="l.quantity" (change)="changeQuantity(l.productId, $any($event.target))" aria-label="Số lượng" />
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

              <form class="pt-card pt-form" [formGroup]="form" (ngSubmit)="submit(d)" novalidate>
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
                    @if (invalid('date')) {
                      <em>{{ form.controls.date.hasError('tooEarly') ? 'Đã qua giờ chốt cho ngày này – chọn ngày sau.' : 'Chọn ngày nhận.' }}</em>
                    }
                  </label>
                  <label>
                    <span>Bữa <b>*</b></span>
                    <select formControlName="meal">
                      @for (m of d.weeklyMenu.meals; track m) {
                        <option [value]="m" [disabled]="!mealAllowed(d, m)">Bữa {{ m.toLowerCase() }}{{ mealAllowed(d, m) ? '' : ' (không có món đã chọn)' }}</option>
                      }
                    </select>
                  </label>
                </div>
                @if (mealConflicts(d); as bad) {
                  @if (bad.length) {
                    <div class="pt-alert" role="alert">
                      <i class="material-icons">info</i>
                      <span>Bữa {{ form.controls.meal.value.toLowerCase() }} không có: {{ bad.join(', ') }}. Chọn bữa khác hoặc bỏ món đó khỏi giỏ.</span>
                    </div>
                  }
                }
                <label>
                  <span>Ghi chú</span>
                  <textarea formControlName="note" rows="3" placeholder="Ít cay, giao tới lễ tân…"></textarea>
                </label>
                <!-- Ô bẫy chống máy gửi tự động (RETAIL-1 mục 5.1): người dùng không thấy, không điền. -->
                <div class="pt-hp" aria-hidden="true">
                  <label>Website<input formControlName="website" tabindex="-1" autocomplete="off" /></label>
                </div>
                @if (serverError(); as err) {
                  <div class="pt-alert" role="alert"><i class="material-icons">error_outline</i><span>{{ err }}</span></div>
                }
                <button type="submit" class="pt-btn pt-btn-lg pt-btn-block" [disabled]="sending()">
                  <i class="material-icons">{{ sending() ? 'hourglass_top' : 'send' }}</i>{{ sending() ? 'Đang gửi đơn…' : 'Gửi đơn' }}
                </button>
                <p class="pt-muted">Bằng việc gửi đơn, bạn đồng ý với <a routerLink="/chinh-sach/dat-hang">chính sách đặt hàng</a> và <a routerLink="/chinh-sach/bao-mat">bảo mật thông tin</a>.</p>
              </form>
            </div>
          }
        </div>
      </section>

      @if (summary(); as s) {
        <div class="pt-modal-backdrop" (click)="summary.set('')">
          <div id="pt-order-dialog" tabindex="-1" class="pt-card pt-modal" role="dialog" aria-modal="true" aria-labelledby="pt-order-title" (click)="$event.stopPropagation()">
            @if (uncertain()) {
              <h2 id="pt-order-title"><i class="material-icons">help_outline</i>Vui lòng gọi xác nhận đơn</h2>
              <p>Kết nối bị gián đoạn nên chưa xác định được đơn đã được ghi nhận hay chưa. <b>Đừng gửi lại</b> – hãy gọi hoặc nhắn Zalo kèm nội dung dưới đây để nhân viên kiểm tra, tránh đặt trùng.</p>
            } @else {
              <h2 id="pt-order-title"><i class="material-icons">receipt_long</i>Hoàn tất đặt hàng qua điện thoại</h2>
              <p>Đặt hàng trực tuyến tạm thời chưa sẵn sàng – đơn <b>chưa được gửi</b>. Vui lòng gọi hoặc nhắn Zalo và đọc / dán nội dung dưới đây, nhân viên sẽ xác nhận ngay.</p>
            }
            <pre class="pt-order-text">{{ s }}</pre>
            <div class="pt-modal-actions">
              <button type="button" class="pt-btn pt-btn-ghost" (click)="copy(s)">
                <i class="material-icons">{{ copied() === 'ok' ? 'check' : 'content_copy' }}</i>{{ copied() === 'ok' ? 'Đã sao chép' : 'Sao chép đơn' }}
              </button>
              @if (copied() === 'fail') {
                <p class="pt-muted pt-copy-fail">Trình duyệt không cho sao chép tự động – vui lòng bôi đen nội dung đơn ở trên để sao chép.</p>
              }
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
  private readonly injector = inject(Injector);
  private readonly api = inject(RetailApiService);
  /** RETAIL-2: khách đã đăng nhập → điền sẵn thông tin nhận hàng, đơn vào "Đơn của tôi". */
  public readonly account = inject(CustomerAccountService);
  public readonly summary = signal('');
  public readonly copied = signal<'' | 'ok' | 'fail'>('');
  public readonly sending = signal(false);
  public readonly serverError = signal('');
  /** Hộp thoại gọi điện mở vì không rõ đơn đã lưu chưa (khác với "chắc chắn chưa gửi"). */
  public readonly uncertain = signal(false);
  /** Mã lần gửi – giữ nguyên khi khách bấm gửi lại, đổi sau khi đặt thành công (máy chủ chống tạo đơn trùng). */
  private requestId = '';
  public readonly done = signal<{ orderNumber: string; totalAmount?: number; phone: string; date: string; meal: string } | null>(null);
  /** Ngày nhận sớm nhất: đơn chốt trước giờ chốt hôm trước (mặc định 20:00) → trước giờ chốt: ngày mai, sau giờ chốt: ngày kia. */
  public minDate = this.earliestDate(DEFAULT_CUTOFF_HOUR);

  public readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.required, vnPhone]],
    address: ['', [Validators.required, Validators.maxLength(250)]],
    date: [this.minDate, [Validators.required, (c: AbstractControl) => notBefore(c, () => this.minDate)]],
    meal: ['Trưa', Validators.required],
    note: ['', Validators.maxLength(500)],
    website: [''],
  });

  constructor() {
    const p = this.account.profile();
    if (p) this.form.patchValue({ name: p.fullName ?? '', phone: p.phoneNumber ?? '', address: p.address ?? '' });
    // Giờ chốt lấy từ cấu hình portal-data.json (company.orderCutoffHour) khi có.
    this.data$.pipe(takeUntilDestroyed()).subscribe((d) => {
      const hour = d.company.orderCutoffHour ?? DEFAULT_CUTOFF_HOUR;
      const min = this.earliestDate(hour);
      if (min !== this.minDate) {
        const c = this.form.controls.date;
        if (!c.dirty || c.value < min) c.setValue(min);
        this.minDate = min;
        c.updateValueAndValidity();
      }
    });
  }

  @HostListener('document:keydown.escape')
  public closeSummary(): void {
    this.summary.set('');
  }

  public invalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  /** Ô số lượng: ghi số đã chuẩn hóa trở lại ô (vd gõ 1500 → 999, gõ chữ → giữ số cũ). */
  public changeQuantity(productId: string, input: HTMLInputElement): void {
    const q = Math.floor(Number(input.value));
    if (Number.isFinite(q) && q >= 0) {
      this.cart.setQuantity(productId, q);
    }
    input.value = String(this.cart.quantityOf(productId) || '');
  }

  /** Bữa m có bán được mọi món đang trong giỏ không (món không khai bữa = bán mọi bữa). */
  public mealAllowed(d: PortalData, meal: string): boolean {
    return this.cart.items().every((l) => {
      const p = d.products.find((x) => x.id === l.productId);
      return !p || !p.meals?.length || p.meals.includes(meal);
    });
  }

  /** Tên các món trong giỏ không bán ở bữa đang chọn. */
  public mealConflicts(d: PortalData): string[] {
    const meal = this.form.controls.meal.value;
    return this.cart.items()
      .filter((l) => {
        const p = d.products.find((x) => x.id === l.productId);
        return p && p.meals?.length && !p.meals.includes(meal);
      })
      .map((l) => l.name);
  }

  /**
   * Gửi đơn lên máy chủ (RetailPortal/SubmitOrder – RETAIL-1). Máy chủ tính lại giá, kiểm giờ chốt, bữa của món.
   * - Thành công: hiện mã đơn, xóa giỏ.
   * - Máy chủ từ chối (400 / 429): hiện lý do trên form, giữ giỏ.
   * - Chắc chắn chưa lưu (chưa có API, không kết nối được, hoặc danh mục đang lấy từ file dự phòng – mã món không khớp máy chủ):
   *   hiện nội dung đơn để khách gọi / nhắn Zalo.
   * - Không rõ đã lưu chưa (hết giờ chờ / lỗi máy chủ sau khi gửi): nhắc gọi xác nhận, KHÔNG gửi lại. Gửi lại vẫn an toàn nhờ
   *   clientRequestId (máy chủ trả lại đơn cũ thay vì tạo đơn mới).
   */
  public submit(d: PortalData): void {
    if (this.sending()) return;
    this.serverError.set('');
    if (this.form.invalid || !this.cart.items().length) {
      this.form.markAllAsTouched();
      return;
    }
    if (this.mealConflicts(d).length) return;
    if (d.catalogSource !== 'api') {
      this.showHotlineSummary(d.company, false);
      return;
    }
    const v = this.form.getRawValue();
    const phone = normalizePhone(v.phone);
    const [y, m, dd] = v.date.split('-');
    this.requestId ||= newRequestId();
    this.sending.set(true);
    this.api
      .submit({
        clientRequestId: this.requestId,
        customerName: v.name.trim(),
        phoneNumber: phone,
        address: v.address.trim(),
        deliveryDate: v.date,
        mealType: v.meal,
        note: v.note.trim(),
        website: v.website,
        lines: this.cart.items().map((l) => ({ productId: l.productId, productCode: l.productCode || '', quantity: l.quantity })),
      })
      .subscribe((r) => {
        this.sending.set(false);
        if (r.kind === 'ok') {
          this.requestId = '';
          this.done.set({ orderNumber: r.orderNumber, totalAmount: r.totalAmount, phone, date: `${dd}/${m}/${y}`, meal: v.meal });
          this.cart.clear();
          this.form.reset();
          const p = this.account.profile();
          if (p) this.form.patchValue({ name: p.fullName ?? '', phone: p.phoneNumber ?? '', address: p.address ?? '' });
          window.scrollTo({ top: 0, behavior: 'smooth' });
        } else if (r.kind === 'rejected') {
          this.serverError.set(r.message);
        } else {
          this.showHotlineSummary(d.company, r.kind === 'uncertain');
        }
      });
  }

  /** Dự phòng khi chưa gửi được / không rõ đã gửi được chưa: dựng nội dung đơn để khách gọi / nhắn Zalo. */
  private showHotlineSummary(company: PortalCompany, uncertain: boolean): void {
    this.uncertain.set(uncertain);
    const v = this.form.getRawValue();
    const fmt = (n: number) => n.toLocaleString('vi-VN') + 'đ';
    const [y, m, dd] = v.date.split('-');
    const lines = this.cart.items().map((l) => `- ${l.name} x ${l.quantity} ${l.unit} = ${fmt(l.price * l.quantity)}`);
    this.copied.set('');
    // eventCoalescing: giao diện vẽ lại ở khung hình sau → chờ vẽ xong mới đưa con trỏ vào hộp thoại.
    afterNextRender(() => document.getElementById('pt-order-dialog')?.focus(), { injector: this.injector });
    this.summary.set(
      [
        `ĐƠN ĐẶT HÀNG ${company.brand}`,
        ...lines,
        `Tạm tính: ${fmt(this.cart.total())}`,
        `Người nhận: ${v.name.trim()} – ${normalizePhone(v.phone)}`,
        `Địa chỉ: ${v.address.trim()}`,
        `Nhận: bữa ${v.meal.toLowerCase()} ngày ${dd}/${m}/${y}`,
        v.note.trim() ? `Ghi chú: ${v.note.trim()}` : '',
      ]
        .filter(Boolean)
        .join('\n'),
    );
  }

  /**
   * Sao chép nội dung đơn. navigator.clipboard chỉ có trên https / localhost – server chạy http (vd http://<ip>:4444)
   * thì dùng cách cũ (textarea + execCommand); vẫn không được thì báo khách tự bôi đen.
   */
  public copy(text: string): void {
    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).then(
        () => this.copied.set('ok'),
        () => this.copied.set(this.legacyCopy(text) ? 'ok' : 'fail'),
      );
      return;
    }
    this.copied.set(this.legacyCopy(text) ? 'ok' : 'fail');
  }

  private legacyCopy(text: string): boolean {
    const ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('readonly', '');
    ta.style.position = 'fixed';
    ta.style.opacity = '0';
    document.body.appendChild(ta);
    ta.select();
    let ok = false;
    try {
      ok = document.execCommand('copy');
    } catch {
      ok = false;
    }
    document.body.removeChild(ta);
    return ok;
  }

  private earliestDate(cutoffHour: number): string {
    const now = new Date();
    const days = now.getHours() < cutoffHour ? 1 : 2;
    return this.isoDate(new Date(now.getFullYear(), now.getMonth(), now.getDate() + days));
  }

  private isoDate(d: Date): string {
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
  }
}
