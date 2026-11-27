import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CustomerAccountService } from '../account.service';

const PHONE = /^(0|\+84)(3|5|7|8|9)\d{8}$/;
const clean = (p: string) => p.replace(/[\s.\-]/g, '');

/**
 * RETAIL-2: đăng nhập / đăng ký tài khoản khách lẻ (SĐT + mật khẩu). Xong → về trang trước (?ve=) hoặc "Đơn của tôi".
 * Không liên quan đăng nhập nội bộ (/dang-nhap).
 */
@Component({
  selector: 'app-portal-account-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="pt-page-head">
      <div class="pt-wrap">
        <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / Tài khoản</nav>
        <h1>{{ mode() === 'login' ? 'Đăng nhập' : 'Tạo tài khoản' }}</h1>
        <p>Đăng nhập để xem Đơn của tôi, theo dõi chuyến giao và truy xuất nguồn gốc suất ăn.</p>
      </div>
    </section>
    <section class="pt-section">
      <div class="pt-wrap pt-auth">
        <div class="pt-card pt-form">
          <div class="pt-chips pt-auth-tabs" role="tablist">
            <button type="button" class="pt-chip" [class.active]="mode() === 'login'" (click)="switch('login')">Đăng nhập</button>
            <button type="button" class="pt-chip" [class.active]="mode() === 'register'" (click)="switch('register')">Tạo tài khoản</button>
          </div>

          @if (mode() === 'login') {
            <form [formGroup]="loginForm" (ngSubmit)="login()" novalidate>
              <label>
                <span>Số điện thoại <b>*</b></span>
                <input formControlName="phone" type="tel" autocomplete="tel" placeholder="09xx xxx xxx" />
              </label>
              <label>
                <span>Mật khẩu <b>*</b></span>
                <input formControlName="password" type="password" autocomplete="current-password" />
              </label>
              @if (error(); as e) { <div class="pt-alert" role="alert"><i class="material-icons">error_outline</i><span>{{ e }}</span></div> }
              <button type="submit" class="pt-btn pt-btn-lg pt-btn-block" [disabled]="busy()">
                <i class="material-icons">login</i>{{ busy() ? 'Đang đăng nhập…' : 'Đăng nhập' }}
              </button>
              <p class="pt-muted">Quên mật khẩu? Gọi hotline để được cấp lại. Chưa có tài khoản? <a href="javascript:void(0)" (click)="switch('register')">Tạo tài khoản</a>.</p>
            </form>
          } @else {
            <form [formGroup]="registerForm" (ngSubmit)="register()" novalidate>
              <label>
                <span>Họ tên <b>*</b></span>
                <input formControlName="fullName" autocomplete="name" />
              </label>
              <label>
                <span>Số điện thoại <b>*</b></span>
                <input formControlName="phone" type="tel" autocomplete="tel" placeholder="09xx xxx xxx" />
                <em class="pt-hint">Dùng để đăng nhập và để nhân viên gọi xác nhận đơn.</em>
              </label>
              <div class="pt-form-row">
                <label>
                  <span>Mật khẩu <b>*</b></span>
                  <input formControlName="password" type="password" autocomplete="new-password" />
                </label>
                <label>
                  <span>Nhập lại mật khẩu <b>*</b></span>
                  <input formControlName="confirm" type="password" autocomplete="new-password" />
                </label>
              </div>
              <label>
                <span>Địa chỉ giao thường dùng</span>
                <input formControlName="address" autocomplete="street-address" />
              </label>
              <label>
                <span>Email</span>
                <input formControlName="email" type="email" autocomplete="email" />
              </label>
              <input formControlName="website" class="pt-hp" tabindex="-1" autocomplete="off" aria-hidden="true" />
              @if (error(); as e) { <div class="pt-alert" role="alert"><i class="material-icons">error_outline</i><span>{{ e }}</span></div> }
              <button type="submit" class="pt-btn pt-btn-lg pt-btn-block" [disabled]="busy()">
                <i class="material-icons">person_add</i>{{ busy() ? 'Đang tạo…' : 'Tạo tài khoản' }}
              </button>
              <p class="pt-muted">Bằng việc tạo tài khoản, bạn đồng ý với <a routerLink="/chinh-sach">chính sách</a> của DELI-CARE.</p>
            </form>
          }
        </div>
        <aside class="pt-card pt-auth-side">
          <h3>Tài khoản giúp bạn</h3>
          <ul>
            <li><i class="material-icons">receipt_long</i>Xem mọi đơn đã đặt – đang xử lý, đã giao, đã hủy</li>
            <li><i class="material-icons">soup_kitchen</i>Biết đơn đang ở bước nào: sơ chế, nấu, đóng gói, đang giao</li>
            <li><i class="material-icons">local_shipping</i>Theo dõi chuyến giao: xe, tài xế, vị trí gần nhất</li>
            <li><i class="material-icons">verified</i>Truy xuất nguồn gốc: mẻ nấu, nguyên liệu, nhà cung cấp</li>
            <li><i class="material-icons">bolt</i>Đặt nhanh – điền sẵn tên, SĐT, địa chỉ</li>
          </ul>
        </aside>
      </div>
    </section>
  `,
})
export class PortalAccountLoginComponent {
  private readonly account = inject(CustomerAccountService);
  private readonly router = inject(Router);
  private readonly back = inject(ActivatedRoute).snapshot.queryParamMap.get('ve') || '/don-cua-toi';
  public readonly mode = signal<'login' | 'register'>(inject(ActivatedRoute).snapshot.queryParamMap.get('tao') ? 'register' : 'login');
  public readonly busy = signal(false);
  public readonly error = signal('');
  private readonly fb = inject(FormBuilder).nonNullable;

  public readonly loginForm = this.fb.group({ phone: ['', Validators.required], password: ['', Validators.required] });
  public readonly registerForm = this.fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    phone: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(6)]],
    confirm: ['', Validators.required],
    address: ['', Validators.maxLength(500)],
    email: ['', Validators.maxLength(255)],
    website: [''],
  });

  constructor() {
    if (this.account.loggedIn()) this.router.navigateByUrl(this.back, { replaceUrl: true });
  }

  public switch(m: 'login' | 'register'): void {
    this.mode.set(m);
    this.error.set('');
  }

  public login(): void {
    const v = this.loginForm.getRawValue();
    const phone = clean(v.phone);
    if (!PHONE.test(phone)) return this.error.set('Số điện thoại chưa đúng.');
    if (!v.password) return this.error.set('Vui lòng nhập mật khẩu.');
    this.run(this.account.login(phone, v.password));
  }

  public register(): void {
    const v = this.registerForm.getRawValue();
    const phone = clean(v.phone);
    if (v.fullName.trim().length < 2) return this.error.set('Vui lòng nhập họ tên.');
    if (!PHONE.test(phone)) return this.error.set('Số điện thoại chưa đúng (10 số, bắt đầu bằng 03, 05, 07, 08 hoặc 09).');
    if (v.password.length < 6) return this.error.set('Mật khẩu cần tối thiểu 6 ký tự.');
    if (v.password !== v.confirm) return this.error.set('Hai lần nhập mật khẩu chưa khớp.');
    this.run(this.account.register({ fullName: v.fullName.trim(), phoneNumber: phone, password: v.password, address: v.address.trim(), email: v.email.trim(), website: v.website }));
  }

  private run(obs: ReturnType<CustomerAccountService['login']>): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    obs.subscribe((r) => {
      this.busy.set(false);
      if (r.ok) this.router.navigateByUrl(this.back, { replaceUrl: true });
      else this.error.set(r.message);
    });
  }
}
