import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CustomerAccountService } from '../account.service';

/** RETAIL-2: thông tin tài khoản khách lẻ – sửa họ tên / địa chỉ / email, đổi mật khẩu, đăng xuất. SĐT (tên đăng nhập) chỉ xem. */
@Component({
  selector: 'app-portal-account-profile',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="pt-page-head">
      <div class="pt-wrap">
        <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / <a routerLink="/don-cua-toi">Đơn của tôi</a> / Tài khoản</nav>
        <h1>Thông tin tài khoản</h1>
      </div>
    </section>
    <section class="pt-section">
      <div class="pt-wrap pt-auth">
        <form class="pt-card pt-form" [formGroup]="profileForm" (ngSubmit)="save()" novalidate>
          <h2>Thông tin nhận hàng</h2>
          <label><span>Loại tài khoản</span><input [value]="biz() ? 'Doanh nghiệp' : 'Cá nhân'" readonly /></label>
          <label><span>Số điện thoại (đăng nhập)</span><input [value]="account.profile()?.phoneNumber || ''" readonly /></label>
          @if (biz()) {
            <label><span>Tên doanh nghiệp <b>*</b></span><input formControlName="companyName" autocomplete="organization" /></label>
            <label><span>Mã số thuế <b>*</b></span><input formControlName="taxCode" inputmode="numeric" autocomplete="off" /></label>
          }
          <label><span>{{ biz() ? 'Người liên hệ' : 'Họ tên' }} <b>*</b></span><input formControlName="fullName" autocomplete="name" /></label>
          <label><span>Địa chỉ giao thường dùng</span><input formControlName="address" autocomplete="street-address" /></label>
          <label><span>Email @if (biz()) { <b>*</b> }</span><input formControlName="email" type="email" autocomplete="email" /></label>
          @if (msg(); as m) { <p [class.pt-ok-text]="ok()" [class.pt-err-text]="!ok()">{{ m }}</p> }
          <button type="submit" class="pt-btn" [disabled]="busy()"><i class="material-icons">save</i>Lưu thông tin</button>
        </form>
        <div>
          <form class="pt-card pt-form" [formGroup]="pwForm" (ngSubmit)="changePassword()" novalidate>
            <h2>Đổi mật khẩu</h2>
            <label><span>Mật khẩu hiện tại <b>*</b></span><input formControlName="current" type="password" autocomplete="current-password" /></label>
            <label><span>Mật khẩu mới <b>*</b></span><input formControlName="next" type="password" autocomplete="new-password" /></label>
            <label><span>Nhập lại mật khẩu mới <b>*</b></span><input formControlName="confirm" type="password" autocomplete="new-password" /></label>
            @if (pwMsg(); as m) { <p [class.pt-ok-text]="pwOk()" [class.pt-err-text]="!pwOk()">{{ m }}</p> }
            <button type="submit" class="pt-btn" [disabled]="busy()"><i class="material-icons">key</i>Đổi mật khẩu</button>
            <p class="pt-muted">Đổi mật khẩu sẽ đăng xuất các thiết bị khác.</p>
          </form>
          <button type="button" class="pt-btn pt-btn-ghost pt-btn-block pt-mt" (click)="logout()"><i class="material-icons">logout</i>Đăng xuất</button>
        </div>
      </div>
    </section>
  `,
})
export class PortalAccountProfileComponent implements OnInit {
  public readonly account = inject(CustomerAccountService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder).nonNullable;
  public readonly busy = signal(false);
  public readonly msg = signal('');
  public readonly ok = signal(false);
  public readonly pwMsg = signal('');
  public readonly pwOk = signal(false);
  public readonly profileForm = this.fb.group({ fullName: ['', Validators.required], address: [''], email: [''], companyName: [''], taxCode: [''] });
  /** D-136 j: tài khoản doanh nghiệp (loại không đổi được sau khi tạo). */
  public readonly biz = computed(() => this.account.profile()?.accountType === 'BUSINESS');
  public readonly pwForm = this.fb.group({ current: [''], next: [''], confirm: [''] });

  public ngOnInit(): void {
    if (!this.account.loggedIn()) {
      this.router.navigate(['/tai-khoan/dang-nhap'], { queryParams: { ve: '/tai-khoan' }, replaceUrl: true });
      return;
    }
    this.fill();
    this.account.refreshProfile().subscribe((r) => {
      if (r.ok) this.fill();
      else if (r.expired) this.router.navigate(['/tai-khoan/dang-nhap'], { queryParams: { ve: '/tai-khoan' } });
    });
  }

  public save(): void {
    const v = this.profileForm.getRawValue();
    if (v.fullName.trim().length < 2) {
      this.ok.set(false);
      this.msg.set('Vui lòng nhập họ tên.');
      return;
    }
    this.busy.set(true);
    this.account.updateProfile({ fullName: v.fullName.trim(), address: v.address.trim(), email: v.email.trim(), ...(this.biz() ? { companyName: v.companyName.trim(), taxCode: v.taxCode.replace(/[\s.]/g, '') } : {}) }).subscribe((r) => {
      this.busy.set(false);
      this.ok.set(r.ok);
      this.msg.set(r.ok ? 'Đã lưu thông tin.' : r.message);
    });
  }

  public changePassword(): void {
    const v = this.pwForm.getRawValue();
    this.pwOk.set(false);
    if (!v.current) return this.pwMsg.set('Vui lòng nhập mật khẩu hiện tại.');
    if (v.next.length < 6) return this.pwMsg.set('Mật khẩu mới cần tối thiểu 6 ký tự.');
    if (v.next !== v.confirm) return this.pwMsg.set('Hai lần nhập mật khẩu mới chưa khớp.');
    this.busy.set(true);
    this.account.changePassword(v.current, v.next).subscribe((r) => {
      this.busy.set(false);
      this.pwOk.set(r.ok);
      this.pwMsg.set(r.ok ? 'Đã đổi mật khẩu.' : r.message);
      if (r.ok) this.pwForm.reset();
    });
  }

  public logout(): void {
    this.account.logout();
    this.router.navigateByUrl('/');
  }

  private fill(): void {
    const p = this.account.profile();
    if (p) this.profileForm.setValue({ fullName: p.fullName ?? '', address: p.address ?? '', email: p.email ?? '', companyName: p.companyName ?? '', taxCode: p.taxCode ?? '' });
  }
}
