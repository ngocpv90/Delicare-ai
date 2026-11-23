# 13 – Ban điều hành (CEO, COO, PMO, Kế toán)

## 1. Bạn là ai

- Nhóm quyền: **CEO**, **CEO-PMO**, **COO**, **FIN-ACC** (Kế toán). Theo seed hiện tại các nhóm này **chỉ xem** màn nghiệp vụ (không sửa), CEO / COO có quyền **Duyệt** (`12_QUAN_LY_DUYET.md`), FIN-ACC duyệt kiểm kê cấp 2.
- Bot dùng: **Điều hành** (📊) – tổng quan, việc mở, KPI, tiến độ bếp, giao vận trực tiếp, tồn & thiếu hụt, truy xuất; nhận cảnh báo 🔴 liên phòng ban và leo thang.
- Việc chính: theo dõi số liệu ngày / ca, duyệt, xử lý sự cố leo thang, xem Chỉ số theo ngày / tháng, giá thành suất.

## 2. Màn hình được dùng

| screen-id | Menu › Mục | Dùng để |
|---|---|---|
| `dash-exec` | Bảng điều hành › Dashboard điều hành (trang chủ) | Số liệu ngày / ca, cảnh báo |
| `kpi-daily` | … › Chỉ số theo ngày | Chỉ tiêu 30 ngày gần nhất theo nhóm |
| `kpi-monthly` | … › Chỉ số theo tháng | Chỉ tiêu 12 tháng |
| `flow-e2e` | … › 9 luồng nghiệp vụ | Sơ đồ luồng, mở nhanh màn từng bước |
| `wf-inbox` | Nhân sự › Hộp phê duyệt | Duyệt |
| `cost-meal`, `cost-actual` | Sản xuất & Giá thành | Giá thành suất, giá thành thực tế |
| `crm-reconciliation` | Kinh doanh › Đối soát đơn hàng | Kế toán xem / chốt đối soát (`10_DIEM_PHAT.md`) |
| Mọi màn khác | | Xem (không sửa) |

## 3. Việc trong ngày

| Thời điểm | Việc | Ở đâu |
|---|---|---|
| 07:00 | Đọc **bản tin sáng** (bot) | bot Điều hành |
| Trong ngày | Theo dõi dashboard, nhận cảnh báo 🔴, nhận xử lý sự cố leo thang | `dash-exec` / bot |
| Khi có yêu cầu | Duyệt PR, PO, hợp đồng, công thức, kiểm kê, điều chỉnh tồn, kế hoạch SX, thực đơn, đơn nghỉ | `wf-inbox`, `prod-plan`, `menu-menu` / bot |
| 17:00 | Đọc **báo cáo cuối ngày** (bot) | bot |
| 19:00 | Bot báo khách chưa chốt đơn ngày mai | bot |
| Cuối tháng | Chỉ số theo tháng, giá thành suất | `kpi-monthly`, `cost-meal` |

## 4. Thao tác từng bước

### 4.1 Dashboard điều hành – `dash-exec`
1. Bộ lọc đầu trang: **Ngày** (6 ngày trước → 3 ngày tới, "Hôm nay" đánh dấu), **Ca** (Tất cả ca / từng ca), đơn vị làm việc (ô đầu trang). Số liệu do máy chủ tính, tự làm mới sau 60 giây.
2. **8 thẻ số liệu** (bấm thẻ để mở màn gốc). Mỗi thẻ: biểu tượng bên trái, tên + số chính, số phụ ngắn ngay sau số chính (vd "123 đơn", "350 / 1.000 phần"):

| Thẻ | Ý nghĩa | Mở màn |
|---|---|---|
| Suất ăn kế hoạch | Tổng suất của đơn hợp lệ ngày ăn | `crm-order` |
| Đơn hàng trong ngày | Số đơn theo trạng thái | `crm-order` |
| Tiến độ sản xuất | % phần món đạt / kế hoạch, số mẻ | `prod-batch` |
| Giao hàng hoàn tất | Đã giao / tổng đơn giao | `log-execution` |
| Cảnh báo tồn kho | Nguyên liệu dưới tồn tối thiểu | `scm-inv-balance` |
| QC tạm giữ / loại | Mẻ giữ, mẻ loại, lô tạm giữ | `prod-batch` |
| Nhân sự theo ca | Đã vào ca / được phân ca | `hr-shift-assign` |
| Khiếu nại đang mở | Phản ánh mới / đang xử lý | `log-complaint` |

3. Biểu đồ **Sản lượng suất ăn 7 ngày** (Kế hoạch vs Thực nhận), **Tiến độ theo ca sản xuất** (thanh %), khối **Chỉ số theo ngày** (giá trị, mục tiêu, đạt / chưa đạt).
4. Bảng **Tình hình đơn hàng & giao nhận** (Mã đơn, Khách hàng, Suất ăn, Đơn hàng, Giao hàng) → bấm dòng mở đơn; **Xem tất cả →**.
5. Khối **Cảnh báo & công việc** (theo quyền tài khoản): mỗi việc có **Mở** → tới màn xử lý.

### 4.2 Chỉ số theo ngày / tháng – `kpi-daily`, `kpi-monthly`
**Tự tính (BIZ-3.5):** KPI ngày tự tính lúc **22:00**; KPI tháng tính lúc **01:00** cho tháng của hôm qua (ngày 1 chốt số tháng trước). Job lỗi → bot báo IT. Nút làm mới tay vẫn dùng được.
1. Chip nhóm: **Tất cả**, Sản xuất, Chất lượng, Giao hàng, Kho, Tài chính, Bao bì, Khách hàng.
2. Mỗi chỉ tiêu: nhóm, tên, nhãn **Đạt / Chưa đạt / Thiếu dữ liệu / Không có mục tiêu / Chưa có số**, giá trị gần nhất, mục tiêu (≥ / ≤), ngày / tháng, biểu đồ đường có vạch mục tiêu.
3. **Làm mới số liệu** – chỉ quản trị hệ thống thấy; tính lại và báo *"Đã làm mới N chỉ tiêu · M chỉ tiêu thiếu dữ liệu"*. Lịch tự tính KPI hằng ngày / tháng hiện **đang tắt** → số liệu chỉ có sau khi quản trị bấm làm mới.

### 4.3 Chín luồng nghiệp vụ – `flow-e2e`
Danh sách 9 luồng (người tham gia, các bước); mỗi bước bấm **Mở →** tới màn tương ứng. Nút **Tải đặc tả .md**. Nút **Bắt đầu demo từ bước 1** chỉ hiện ở chế độ dữ liệu demo.

### 4.4 Giá thành – `cost-meal`, `cost-actual`
Xem `03_KE_HOACH.md` mục 4.10–4.11 (giá thành suất theo khách / ngày, giá thành thực tế theo lệnh SX, chênh lệch so định mức).

## 5. Trạng thái
Không có chứng từ riêng; xem trạng thái chứng từ ở `91_PHU_LUC.md`.

## 6. Lỗi / cảnh báo thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| Không tải được số liệu điều hành … | Kiểm mạng; bấm lại sau 60 giây; báo IT nếu kéo dài |
| KPI "Chưa có số" / "Thiếu dữ liệu" | Chưa làm mới hoặc thiếu dữ liệu nguồn – nhờ IT **Làm mới số liệu** |
| Bấm nút sửa báo không có quyền | Ban điều hành chỉ xem; thao tác giao cho phòng ban |
| Bấm **Duyệt** kế hoạch / thực đơn báo lỗi quyền | Chỉ CEO / COO có quyền Duyệt; kiểm tài khoản đúng nhóm |

## 7. Trên bot Điều hành

| Nút | Lệnh | Nội dung |
|---|---|---|
| 📊 Tổng quan hôm nay | `/tongquan` | Suất kế hoạch, đơn, tiến độ SX, giao, QC, nhân sự, phản ánh |
| 🚨 Việc đang mở | `/viecmo` | Sự cố / cảnh báo chưa xử lý, người nhận |
| 🎯 KPI 7 ngày | `/kpi` | Chỉ tiêu 7 ngày |
| 📅 Chỉ số theo tháng | `/kpithang` | Chỉ tiêu tháng |
| 📈 Tiến độ bếp | `/tiendobep` | Mẻ theo trạng thái, suất đạt / kế hoạch |
| 🗺 Giao vận trực tiếp | `/giaovantt` | Chuyến đang chạy, điểm trễ |
| 📦 Tồn & thiếu hụt | `/tonthieu` | Tồn dưới mức, thiếu hụt MRP |
| 🔍 Truy xuất | `/truyxuat` | Gõ số chứng từ (`DH-…`, `LO-…`, `ME-…`, `PO-…`, `CX-…`, mã `KHB-…`… – số cũ `PH-…` / `NL-…` vẫn tra được) hoặc tên để tra. Gợi ý trên bot ghi `DH-…` nhưng số đơn hàng là `PH-…` (C-21) |

- **Thông báo:** 07:00 bản tin sáng, 17:00 báo cáo cuối ngày, cảnh báo 🔴 (ATTP trọng yếu, thiếu suất, xe chưa rời bếp, điểm trễ, nhiệt độ IoT), yêu cầu duyệt quá hạn, job hệ thống lỗi.
- **Thẻ sự cố:** **🛠 Nhận xử lý**, **✅ Đã xử lý**, trả lời tin để ghi chú. Chưa ai nhận → leo thang theo bậc (vd ATTP trọng yếu 10′ → COO, 30′ → CEO-PMO; thiếu suất 15′ → COO, 45′ → CEO).
- **Giờ yên lặng:** người không có phân ca (văn phòng, quản lý) nhận tin 🟡 / ⚪ trong 07:00–21:00, ngoài khung thì hẹn tới 07:00 hôm sau; tin 🔴 và tin leo thang gửi ngay.
- Duyệt trên bot: `12_QUAN_LY_DUYET.md` mục 7.
