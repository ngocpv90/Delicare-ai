import { HttpClient } from '@angular/common/http';
import { AfterViewInit, Component, NgZone, OnDestroy, ViewEncapsulation } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LEGAL_NAME, URL_API } from '../helpers/constant-helper';
import { User } from '../helpers/user';
import { AuthenticationService } from '../services/authentication.service';
import { createDelicareHost } from './delicare-host';
import { ThemeService } from './theme.service';

/** Thứ tự nạp giống docs/mockup/index.html. store.js đọc window.DC_HOST nên host phải gán trước. */
const SCRIPTS = [
  'data/schema.js',
  'data/catalog.js',
  'data/demo-data.js',
  'core/util.js',
  'core/store.js',
  'core/ui.js',
  'core/app.js',
  'modules/dashboard.js',
  'modules/sales.js',
  'modules/orderweek.js',
  'modules/detail360.js',
  'modules/customer360.js',
  'modules/menu.js',
  'modules/scm.js',
  'modules/production.js',
  'modules/qc.js',
  'modules/supplier360.js',
  'modules/employee360.js',
  'modules/logistics.js',
  'modules/hr.js',
  'modules/admin.js',
  'modules/integration.js',
  'modules/explorer.js',
  'modules/be3.js',
  'modules/ghep.js',
];
const SCRIPT_BASE = 'assets/delicare/js/';
const SCRIPT_ATTR = 'data-delicare';

/**
 * Khung ứng dụng DELICARE trên theme W3CRM (nav-header, header, deznav, content-body – như main.component):
 * chức năng theo mockup đã duyệt (docs/mockup), giao diện theo theme W3CRM + màu DELI-CARE (clinic.css, D-36).
 * Toàn bộ màn hình nghiệp vụ do engine của mockup vẽ (assets/delicare/js); dữ liệu đọc / ghi qua API cũ
 * (api/<Controller>/GetAllData · Insert · Update · Delete), bảng chưa có API dùng dữ liệu DEMO.
 */
@Component({
  selector: 'app-delicare-shell',
  standalone: true,
  encapsulation: ViewEncapsulation.None,
  templateUrl: './delicare-shell.component.html',
})
export class DelicareShellComponent implements AfterViewInit, OnDestroy {
  public loading = true;
  public loadError = '';
  public legalName = LEGAL_NAME;
  public currentYear = new Date().getFullYear();
  private destroyed = false;

  constructor(
    private http: HttpClient,
    private auth: AuthenticationService,
    private theme: ThemeService,
    private zone: NgZone
  ) {
    this.theme.use('delicare');
  }

  public ngAfterViewInit(): void {
    // Menu + quyền lấy mới từ database mỗi lần mở khung (api/User/GetCurrent): đổi cấu hình ở màn "Menu chức năng" / ma trận quyền
    // là có hiệu lực sau khi tải lại trang. Lỗi mạng → dùng menu đã lưu lúc đăng nhập.
    const stored = this.auth.userValue;
    firstValueFrom(this.http.get<any>(`${URL_API}User/GetCurrent`))
      .then((res) => (Array.isArray(res?.data?.listMenu) && stored ? { ...stored, listMenu: res.data.listMenu, isAdmin: res.data.user?.isAdmin ?? stored.isAdmin, mustChangePassword: res.data.mustChangePassword ?? stored.mustChangePassword } : stored))
      .catch(() => stored)
      .then((user) => this.startEngine(user));
  }

  private startEngine(user: User | null): void {
    if (this.destroyed) return;
    const w = window as any;
    w.DC_HOST = createDelicareHost(this.http, user, () => this.logout());
    // Engine thao tác DOM trực tiếp: chạy ngoài Angular zone để không kích change detection liên tục.
    this.zone.runOutsideAngular(() => {
      this.loadScripts()
        .then(() => {
          if (this.destroyed) return;
          this.zone.run(() => (this.loading = false));
          w.APP.start();
        })
        .catch((e) => this.zone.run(() => {
          this.loading = false;
          this.loadError = String(e?.message ?? e);
        }));
    });
  }

  public ngOnDestroy(): void {
    this.destroyed = true;
    const w = window as any;
    w.APP?.stop?.();
  }

  public logout(): void {
    (window as any).APP?.stop?.();
    // Tải lại trang sau khi đăng xuất để engine (biến toàn cục, dữ liệu đã nạp) được khởi tạo sạch cho tài khoản sau.
    this.zone.run(() => this.auth.logout());
    setTimeout(() => window.location.reload(), 50);
  }

  // Engine dùng biến toàn cục (window.Store, window.APP…) tạo lúc nạp script: luôn nạp lại để store.js
  // đọc DC_HOST của phiên hiện tại.
  private async loadScripts(): Promise<void> {
    document.querySelectorAll(`script[${SCRIPT_ATTR}]`).forEach((s) => s.remove());
    for (const file of SCRIPTS) {
      if (this.destroyed) return;
      await new Promise<void>((resolve, reject) => {
        const s = document.createElement('script');
        s.src = SCRIPT_BASE + file;
        s.async = false;
        s.setAttribute(SCRIPT_ATTR, file);
        s.onload = () => resolve();
        s.onerror = () => reject(new Error('Không tải được ' + s.src));
        document.body.appendChild(s);
      });
    }
  }
}
