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
  'modules/detail-page.js', // UI-6 (D-80): khung trang chi tiết toàn màn – trước các module đăng ký trang
  'modules/customer360.js',
  'modules/menu.js',
  'modules/scm.js',
  'modules/ingredient-form.js', // UI-6.20 (D-101): form Nguyên liệu – sau scm.js
  'modules/uom.js', // UI-6.22 (D-103): Đơn vị tính gộp quy đổi + đơn vị đóng gói
  'modules/supplier-price.js', // UI-6.19 (D-100): form giá NCC + bảng so sánh NCC × nguyên liệu – sau uom.js
  'modules/purchase-flow.js', // UI-6.18 (D-99): form đề nghị mua có dòng + Chọn nhà cung cấp / Chờ gửi NCC – sau scm.js
  'modules/production.js',
  'modules/plan-days.js', // UI-6.23 (D-104): Kế hoạch sản xuất – 7 ngày tới (sau production.js: dùng APP.prodPlanList)
  'modules/cost-margin.js', // UI-6.27 (D-108): Giá vốn & lãi gộp – sau production.js
  'modules/batch-board.js', // UI-6.25 (D-106): Mẻ nấu 1 chạm – sau production.js, trước prod-menu.js
  'modules/qc.js',
  'modules/qc-clean.js', // UI-6.32 (D-113): Vệ sinh khay hộp – sau qc.js
  'modules/qc-shift.js', // UI-6.30 (D-111): Kiểm tra theo ca / mẻ – sau qc.js
  'modules/qc-issue.js', // UI-6.33 (D-114): Sự cố NCC + form Bộ kiểm tra / phiên bản – sau qc-shift.js
  'modules/supplier360.js',
  'modules/employee360.js',
  'modules/logistics.js',
  'modules/hr.js',
  'modules/approval.js', // APR-UI-1 (D-81): màn Phê duyệt tổng – sau hr.js (giữ màn cũ cho chế độ DEMO)
  // UI-6 (D-80): trang chi tiết toàn màn – mỗi nhóm 1 tệp (khung: modules/detail-page.js).
  'modules/detail-stockcount.js',
  'modules/detail-scm.js', // UI-6B
  'modules/detail-sales.js', // UI-6A: đơn đặt suất, hợp đồng, phản ánh, điểm giao
  'modules/detail-prod.js', // UI-6C
  'modules/prod-board.js', // UI-6.24 (D-105): Bảng sản xuất ngày trên trang kế hoạch (APP.prodBoard)
  'modules/admin.js',
  'modules/integration.js',
  'modules/explorer.js',
  'modules/be3.js',
  'modules/ghep.js',
  'modules/group-panel.js', // UI-6.21 (D-102): cột nhóm màn Nguyên liệu / Món ăn – sau menu.js, scm.js, ghep.js
  'modules/prod-menu.js', // UI-6.24 (D-105): Mẻ nấu + tab Sản lượng, Dây chuyền + tab Trạm bếp – sau ghep.js
  'modules/qc-temp.js', // UI-6.31 (D-112): Nhật ký nhiệt độ 3 tab – sau be3.js (thay biểu đồ trùng)
  'modules/log-dispatch.js', // UI-6.34 (D-115): Chia suất & đóng gói + Điều phối giao hàng – sau logistics.js
  'modules/log-track.js', // UI-6.35 (D-116): Theo dõi giao hàng + Khay hộp theo khách – sau logistics.js
  'modules/hr-employee.js', // UI-6.36 (D-117): Nhân viên – danh sách nghiệp vụ, form Thêm nhân viên, nghỉ việc – sau ghep.js, employee360.js
  'modules/hr-shift.js', // UI-6.37 (D-118): Phân ca tuần Thứ 2 – CN, tô ca, sao chép tuần, thiếu người – sau hr.js, hr-employee.js (unitPicker)
  'modules/hr-attendance.js', // UI-6.38 (D-119): Chấm công – Hôm nay / Bảng công tháng / Sổ, bổ sung giờ, xác nhận ngoài bán kính – sau hr.js, hr-employee.js
  'modules/hr-request.js', // UI-6.39 (D-120): Nghỉ & đổi ca – danh sách gộp, duyệt kèm ảnh hưởng, lịch nghỉ, tạo đơn 3 loại, người nhận đồng ý – sau hr.js
  'modules/admin-org.js', // UI-6.41 (D-122): Công ty & đơn vị – cây trái / chi tiết phải – sau admin.js
  'modules/catalog-home.js', // UI-6.40 (D-121): trang tổng Danh mục hệ thống
  'modules/admin-account.js', // UI-6.42 (D-123): Tài khoản – danh sách, Chờ IT xử lý, tạo tài khoản 3 lựa chọn – sau admin.js
  'modules/admin-group.js', // UI-6.43 (D-124): Nhóm quyền – danh sách theo khối, trang nhóm (phân quyền theo mức, tài khoản, xem trước, so sánh, lịch sử) – sau admin.js
  'modules/kho.js', // UI-6.29 (D-110): Kho 12 → 5 mục, Nhận hàng & kiểm tra, Sổ kiểm tra hàng nhập – sau scm.js, qc.js, approval.js, ghep.js
  'modules/receive.js', // UI-6.50 (D-132): Nhận hàng theo NCC – thẻ NCC, ô tìm, form gộp nhiều đơn mua – sau kho.js
  'modules/acc.js', // ACC-1 (D-131): nhóm Kế toán – thanh chuyển nhanh, Khớp mã AMIS, bọc Bảng kê (crm-billing)
  'modules/ai.js', // AI-6 (D-133): Trợ lý AI (Trung tâm agent, Nhật ký agent, Lịch chạy) + Kho tri thức – CUỐI danh sách
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
