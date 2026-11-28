# 13 – Ban điều hành (CEO, COO, PMO, Kế toán)

## 1. Bạn là ai

- Nhóm quyền: **CEO**, **CEO-PMO**, **COO**, **FIN-ACC** (Kế toán). Theo seed hiện tại các nhóm này **chỉ xem** màn nghiệp vụ (không sửa), CEO / COO có quyền **Duyệt** (`12_QUAN_LY_DUYET.md`), FIN-ACC duyệt kiểm kê cấp 2.
- Bot dùng: **Điều hành** (📊) – tổng quan, việc mở, KPI, tiến độ bếp, giao vận trực tiếp, tồn & thiếu hụt, truy xuất; nhận cảnh báo 🔴 liên phòng ban và leo thang.
- Việc chính: theo dõi số liệu ngày / ca, duyệt, xử lý sự cố leo thang, xem Chỉ số theo ngày / tháng, giá thành suất.

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Xem mọi màn nghiệp vụ; bảng dưới chỉ liệt kê mục có quyền hơn **Xem** (đủ ma trận: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | CEO | CEO-PMO | COO | FIN-ACC |
|---|---|---|---|---|---|
| Phê duyệt › Hợp đồng | `wf-inbox?type=crm-contract` | Duyệt | — | — | — |
| Phê duyệt › Chính sách giá | `wf-inbox?type=crm-price-policy` | Duyệt | — | — | — |
| Phê duyệt › Đơn đặt suất (sửa sau chốt) | `wf-inbox?type=crm-order` | — | — | Duyệt | — |
| Phê duyệt › Công thức chế biến | `wf-inbox?type=menu-recipe` | — | — | Duyệt | — |
| Phê duyệt › Thực đơn tuần | `wf-inbox?type=menu-menu` | — | — | Duyệt | — |
| Phê duyệt › Yêu cầu đổi món | `wf-inbox?type=menu-change-request` | — | — | Duyệt | — |
| Phê duyệt › Đề nghị mua – cấp 2 | `wf-inbox?type=scm-pr-l2` | — | — | Duyệt | — |
| Phê duyệt › Đơn mua hàng | `wf-inbox?type=scm-po` | — | — | Duyệt | — |
| Phê duyệt › Đơn mua hàng ≥ 50 triệu | `wf-inbox?type=scm-po-director` | Duyệt | — | — | — |
| Phê duyệt › Phiếu kiểm kê – cấp 1 | `wf-inbox?type=scm-stockcount` | — | — | Duyệt | — |
| Phê duyệt › Phiếu kiểm kê – cấp 2 | `wf-inbox?type=scm-stockcount-l2` | — | — | — | Duyệt |
| Phê duyệt › Phiếu điều chỉnh tồn | `wf-inbox?type=scm-adjustment` | — | — | Duyệt | — |
| Phê duyệt › Kế hoạch sản xuất | `wf-inbox?type=prod-plan` | — | — | Duyệt | — |
| Phê duyệt › Nghỉ & đổi ca | `wf-inbox?type=hr-leave` | — | — | Duyệt | — |
| Phê duyệt › Xếp xe chưa đủ hàng | `wf-inbox?type=log-load-short` | — | — | Duyệt | — |
| Phê duyệt › Mở lại đối soát | `wf-inbox?type=crm-recon-reopen` | — | — | Duyệt | — |
| Phê duyệt › Mở khóa kỳ | `wf-inbox?type=acc-period-unlock` | — | — | Duyệt | — |
| Phê duyệt › Lệnh của agent | `wf-inbox?type=ai-agent-cmd` | — | — | Duyệt | — |
| Phê duyệt › Duyệt SOP | `wf-inbox?type=kb-sop` | Duyệt | — | Duyệt | Duyệt |
| Điều hành › Bảng điều hành | `dash-exec` | X (IX) | X (IX) | X (IX) | X (IX) |
| Điều hành › Chỉ số kinh doanh | `kpi-daily` | X (IX) | X (IX) | X (IX) | X (IX) |
| Điều hành › Giá vốn & lãi gộp | `cost-meal` | X (IX) | X | X | X (IX) |
| Điều hành › Kết quả kinh doanh | `dash-pl` | X (IX) | — | X (IX) | Sửa (IX) |
| Điều hành › Trung tâm Agent | `ai-agents` | S | — | S | — |
| Điều hành › Giao việc | `ai-tasks` | S | — | S | — |
| Điều hành › Lịch chạy | `ai-schedule` | S | — | S | — |
| Kinh doanh › Khách hàng | `crm-customer` | X | X | X | X (IX) |
| Kinh doanh › Hợp đồng | `crm-contract` | X | X | X | X (IX) |
| Kinh doanh › Đơn hàng theo ngày | `crm-order` | X | X | X | X (IX) |
| Kinh doanh › Đối soát suất ăn | `crm-reconciliation` | X | X | X | S (IX) |
| Kế toán › Chứng từ kế toán | `acc-voucher` | X (IX) | X | X (IX) | Q (IX) |
| Kế toán › Bảng kê thanh toán | `crm-billing` | X (IX) | X | X (IX) | Q (IX) |
| Kế toán › Khớp mã AMIS | `acc-amis-code` | X (IX) | X | X (IX) | Q (IX) |
| Kế toán › Đối chiếu với AMIS | `acc-amis-recon` | X (IX) | X | X (IX) | Q (IX) |
| Kế toán › Khóa kỳ | `acc-period` | X (IX) | X | X (IX) | Q (IX) |
| Bán lẻ › Đơn khách lẻ | `sal-retail-order` | X (IX) | X | X (IX) | X (IX) |
| Bán lẻ › Sản phẩm bán lẻ | `sal-retail-product` | X (IX) | X | X (IX) | X (IX) |
| Mua hàng › Đơn mua hàng | `scm-po` | X | X | X | X (IX) |
| Kho › Nhận hàng | `scm-gr` | X | X | X | X (IX) |
| Kho › Trả hàng NCC | `scm-supplier-return` | X | X | X | X (IX) |
| Kho › Xuất kho sản xuất | `scm-issue` | X | X | X | X (IX) |
| Kho › Tồn kho | `scm-inv-balance` | X | X | X | X (IX) |
| Kho › Kiểm kê | `scm-stockcount` | X | X | X | X (IX) |
| Sản xuất › Hao hụt | `prod-waste` | X | X | X | X (IX) |
| Đóng gói & Giao hàng › Theo dõi giao hàng | `log-execution` | X | X | X | X (IX) |
| Đóng gói & Giao hàng › Khay hộp theo khách | `log-packaging-ledger` | X | X | X | X (IX) |
| Đóng gói & Giao hàng › Loại hộp / khay | `log-packaging-type` | X | X | X | Sửa (IX) |
| Đóng gói & Giao hàng › Định mức bao bì | `log-packaging-norm` | X | X | X | X (IX) |
| Nhân sự › Chấm công | `hr-attendance` | X | X | X | X (IX) |
| Tri thức › 10 Ban GĐ | `knowledge?folder=10` | S | X | S | — |
| Tri thức › 60 Kế toán | `knowledge?folder=60` | X | X | X | S |
| Tri thức › 80 Đào tạo | `knowledge?folder=80` | X | X | S | X |
| Tri thức › BOD Bảo mật | `knowledge?folder=bod` | S | X | X | — |
<!-- MENU5:END -->

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
1. **Bảng điều hành có 3 tab** (UI-6.52, D-139): **Tổng quan · Chỉ số kinh doanh · Giá vốn & lãi gộp** – tab hiện theo quyền. Menu Điều hành chỉ còn **Bảng điều hành · Phê duyệt · Trung tâm Agent**; link cũ `#/kpi-daily`, `#/kpi-monthly`, `#/cost-meal` tự mở đúng tab.
   **Bộ lọc chung** (một thanh cho cả 3 tab): ô **ngày** – bấm mở lịch, chọn mọi ngày đã qua, tối đa **hôm nay + 7**; nút **◀ ▶** lùi / tiến 1 ngày; **Hôm nay**; **Ca** (chỉ tab Tổng quan – 2 tab kia ô Ca mờ, "Không chia theo ca"); **Chi nhánh** = ô Đơn vị làm việc trên thanh trên cùng. **Mở từ menu luôn là hôm nay**; chuyển tab giữ ngày / ca đang chọn; link có `?date=` (cảnh báo, bot) mở đúng ngày. Số liệu do máy chủ tính, tự làm mới sau 60 giây.
   - Tab **Chỉ số kinh doanh**: công tắc Theo ngày (30 ngày **kết thúc tại ngày chọn**) / Theo tháng (12 tháng kết thúc tại tháng của ngày chọn); lọc nhóm chỉ tiêu.
   - Tab **Giá vốn & lãi gộp**: tháng = **tháng của ngày chọn** (không còn ◀ ▶ tháng riêng); lọc theo chi nhánh (khách thuộc chi nhánh, gồm đơn vị con); 3 tab con giữ nguyên; "Xu hướng theo ngày" đánh dấu ◆ ngày đang chọn.
   - Người chỉ có quyền Chỉ số / Giá vốn (không có Tổng quan): mục **Bảng điều hành** vẫn hiện, mở thẳng tab được phép.
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


## Giá vốn & lãi gộp (UI-6.27, D-108) – `cost-meal`
Mục **Điều hành › Giá vốn & lãi gộp** thay 3 màn cũ (Giá thành chuẩn · Giá thành thực tế · Giá vốn suất ăn – link cũ mở màn này tab Theo món). Đọc theo **tháng** (◀ ▶; tháng hiện tại tính tới hôm nay), **Xuất Excel**.
- 4 chỉ số: **Giá vốn nguyên liệu / suất** (so tháng trước) · **Tỷ lệ nguyên liệu / doanh thu** (xanh ≤ ngưỡng − 5 %, cam sát ngưỡng, đỏ vượt ngưỡng `KpiIngredientToRevenueMaxPct` – mặc định 55 %) · **Lãi sau nguyên liệu** · **Chi phí lỗi** (đơn bù / loại – tách khỏi suất bán).
- Dòng báo: "Giá vốn hiện chỉ gồm nguyên liệu" (chưa có định mức nhân công / chi phí chung – khi Kế toán khai, nhãn đổi thành "Lãi gộp"); "N đơn (x % suất) đang tạm tính theo giá thành chuẩn" – lệnh sản xuất của món chưa đóng, hệ thống tự tính lại khi lệnh đóng (job 22:00 và ngày 1 hằng tháng).
- Tab **Theo khách hàng / hợp đồng** (mặc định, tỷ lệ cao nhất lên đầu, có dòng Cả tháng): bấm dòng → theo **ngày ăn** (suất, NL / suất, tỷ lệ, nguồn giá Thực tế / Tạm tính) → bấm ngày → đơn và giá vốn từng món. Vượt ngưỡng → nút **Lập phụ lục điều chỉnh giá** (mở hợp đồng).
- Tab **Theo món**: giá chuẩn / phần, thực tế / phần, chênh lệch % (> 5 % đỏ, > 2 % cam), tiền chênh, nguyên nhân chính (nguyên liệu vượt định mức, mẻ QC loại); bấm món → Bảng sản xuất ngày gần nhất.
- Tab **Xu hướng theo ngày**: biểu đồ tỷ lệ NL / DT theo ngày + đường ngưỡng (Xem bảng số dưới biểu đồ).
- Giá thành chuẩn **chỉ do hệ thống tự tính** (khóa công thức, đổi giá NCC, Tính lại) – không thêm / sửa tay.


## Chỉ số kinh doanh (UI-6.28, D-109) – `kpi-daily`
Nhóm **Điều hành** nay gồm: **Bảng điều hành · Chỉ số kinh doanh · Giá vốn & lãi gộp · Phê duyệt** (Phê duyệt cuối nhóm, vẫn hiện số việc chờ duyệt). "Chỉ số theo ngày" và "Chỉ số theo tháng" gộp thành **Chỉ số kinh doanh** với công tắc **Theo ngày | Theo tháng** ở đầu màn (máy nhớ lựa chọn): Theo ngày = 30 ngày gần nhất, Theo tháng = 12 tháng (tháng đang chạy tính đến hôm nay). Chip lọc mảng, thẻ từng chỉ tiêu (số mới nhất, Đạt / Chưa đạt / Thiếu dữ liệu, biểu đồ nhỏ có vạch mục tiêu) giữ nguyên; nút **Làm mới số liệu** (admin) chạy đúng job theo chế độ đang xem. Chỉ tiêu tài chính hiện "Tỷ lệ nguyên liệu / doanh thu", "Giá vốn nguyên liệu / suất" (chưa có định mức nhân công / chung). Link cũ `#/kpi-monthly` mở chế độ tháng.

## Kết quả kinh doanh (RPT-1, D-140) – `dash-exec?tab=pl`

Tab thứ 4 của **Bảng điều hành**, chỉ **CEO, COO, FIN-IT, Kế toán (FIN-ACC)** thấy (mục quyền ẩn `dash-pl`; người khác không có tab, API bị từ chối). Tháng = **tháng của ngày đang chọn** ở bộ lọc chung; ô Ca mờ.

- **Hai lớp số:** **Tạm tính** (Delicare tự tính, cập nhật hằng ngày khi kỳ còn mở) và **Chính thức · AMIS** (có sau khi Kế toán nhập sổ AMIS của tháng – RPT-1.2). Mỗi thẻ ghi rõ lớp; thẻ AMIS chưa có số ghi **"Chờ số AMIS"**.
- **Thẻ chỉ số** (so tháng trước + cùng kỳ năm trước, ▲ / ▼ màu theo tốt / xấu): Doanh thu · Số suất · Nguyên vật liệu · Bao bì · % NVL / doanh thu · Lãi gộp theo NVL (Tạm tính) · Lợi nhuận gộp · Chi phí bán hàng & quản lý · Lợi nhuận trước thuế (Chính thức). Doanh thu, NVL, % NVL **trùng số tab Giá vốn & lãi gộp**. Bao bì = phiếu xuất bao bì theo định mức (ACC-6).
- **Doanh thu theo khách × bữa** (Sáng / Trưa / Tối / Đêm): khách chưa xác nhận bảng kê tháng gắn nhãn **"chưa xác nhận"** (số từ đơn × đơn giá hợp đồng). Hiện theo khách – chưa gộp nhóm khách (SGC, Vinalpha, Vin) vì dữ liệu khách chưa có quan hệ cha – con (GAP-RPT-01 chờ duyệt).
- **Nguyên vật liệu theo nhóm**: xuất kho × giá lô theo nhóm nguyên liệu (không gồm bao bì), tháng này so tháng trước; **Pareto** (% lũy kế – dòng đậm là nhóm trong 80 % chi phí); bảng **12 tháng**. Hiện 12 nhóm lớn nhất + dòng "N nhóm khác".
- Đơn vị **triệu đồng (trđ, 1 chữ số thập phân)** – rê chuột lên số xem số đầy đủ.

### Nhập sổ AMIS – số Chính thức (RPT-1.2)

- **Ai:** Kế toán (FIN-ACC – quyền Nhập + Sửa mục Kết quả kinh doanh). CEO / COO / FIN-IT chỉ xem.
- Tab **Kết quả kinh doanh** → **Nhập sổ AMIS** → chọn **cùng lúc** các tệp Excel xuất từ AMIS: *Sổ chi tiết các tài khoản* 511, 621, 622, 627, 641, 642, 711, 811 (mỗi tài khoản 1 tệp, giữ nguyên mẫu AMIS) + *Báo cáo tài chính* (sheet **B01-DN**) → **Nhập**. Tháng lấy từ dòng "Tháng 8 năm 2026" trong sổ; chứng từ khác tháng → báo lỗi, không ghi.
- Cách tính (khớp báo cáo kế toán): Doanh thu = PS Có 511 · Giá vốn hàng bán = PS Nợ 621 + 622 + 627 · CP bán hàng & quản lý = PS Nợ 641 + 642 · Thu nhập / chi phí khác = 711 / 811 · bỏ bút toán kết chuyển (911, 154). Lợi nhuận gộp = DT − GVHB; Lợi nhuận thuần = LN gộp − CP bán hàng & quản lý; **Lợi nhuận trước thuế** = LN thuần + TN khác − CP khác. ROA / ROE **theo tháng** = LN trước thuế ÷ bình quân (đầu + cuối kỳ) tổng tài sản (mã 280) / vốn chủ sở hữu (mã 400).
- **Nhập lại** cùng tháng = ghi đè số của các tài khoản có trong lần nhập (không nhân đôi); có thể nhập riêng 1 sổ để sửa.
- **Số AMIS tháng**: xem các chỉ tiêu, chi tiết khoản mục từng tài khoản, **dòng chưa phân loại** (đang tính vào "khác").
- **Quy tắc phân loại**: bảng *Tài khoản + từ khóa trong diễn giải (không dấu, nguyên từ, cách nhau |) hoặc TK đối ứng → khoản mục*; dòng sổ lấy quy tắc đầu tiên khớp. Kế toán thêm / sửa / xóa dòng → **Lưu quy tắc** → nhập lại sổ để phân loại lại.
- Lớp **Tạm tính** được chụp hằng đêm 23:30 (job *Kết quả kinh doanh – số tạm tính*) để có lịch sử khi chưa có số AMIS.

### Báo cáo kết quả kinh doanh đầy đủ (RPT-1.3)

- Ngay dưới các thẻ chỉ số: khung **Báo cáo kết quả kinh doanh** – công tắc **Tháng | Lũy kế năm** và **Chính thức · AMIS | Tạm tính**.
- **Chính thức**: bảng KQKD theo bố cục báo cáo kế toán – Doanh thu bán hàng (theo bữa) · Giá vốn hàng bán (Nguyên vật liệu trực tiếp theo nhóm, Nhân công trực tiếp, Chi phí sản xuất chung theo khoản mục) · **Lợi nhuận gộp** · **Chi phí bán hàng & quản lý** (bán hàng, quản lý doanh nghiệp) · **Lợi nhuận thuần** · Thu nhập / chi phí khác · **Lợi nhuận trước thuế** · ROA / ROE theo tháng; cột % doanh thu, tháng trước. Bảng **theo quý** (quý chưa đủ 3 tháng ghi số tháng). **12 biểu đồ xu hướng** 12 tháng (doanh thu, giá vốn, lợi nhuận gộp, chi phí, lợi nhuận thuần / trước thuế, % giá vốn / doanh thu, các biên lợi nhuận, ROA, ROE). **Cơ cấu giá vốn** tháng (biểu đồ tròn + bảng).
- **Tạm tính**: biểu đồ 12 tháng các số Delicare tự tính (doanh thu, nguyên vật liệu, lãi gộp theo NVL, % NVL / DT, bao bì, số suất) – chụp hằng đêm.
- Lịch sử 01 – 08/2026 đã nạp sẵn từ báo cáo kế toán (công cụ `tools/rpt1`, IT chạy một lần); từ tháng 9 Kế toán **Nhập sổ AMIS** mỗi tháng.
