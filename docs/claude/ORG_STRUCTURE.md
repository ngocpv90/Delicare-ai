# ORG_STRUCTURE – Sơ đồ tổ chức và luồng vận hành thật của nhà máy Delicare

Nguồn: ảnh "Sơ đồ tổ chức tổng thể Delicare" anh cung cấp ngày 2026-11-15 (`docs/reference/org/so_do_to_chuc_delicare.jpg`). Đây là **mô hình đang vận hành**, dùng để seed `OrganizationUnit`, `UserGroup`, `Position` và ma trận quyền (T07).

Nguyên tắc vận hành ghi trên sơ đồ:
- Lấy khách hàng làm trung tâm; an toàn thực phẩm là ưu tiên số 1.
- Đúng chất lượng, đúng số lượng, đúng thời gian, đúng chi phí.
- Minh bạch, đo lường được, cải tiến liên tục.

## 1. Cây tổ chức → `OrganizationUnit` (ParentId, Code, Name, UnitType)

Seed bằng `DB/seed/OrganizationUnit_seed.sql`, idempotent theo `Code`. `UnitType`: `COMPANY` · `BOARD` · `DIVISION` (khối) · `DEPARTMENT` (bộ phận) · `TEAM` (tổ). Mã đề xuất (PROPOSED):

| Code | Tên | UnitType | Cha |
|---|---|---|---|
| DLC | Công ty CP TM DV Delicare VN | COMPANY | – |
| CEO | Tổng giám đốc | BOARD | DLC |
| CEO-PMO | Trợ lý TGĐ / PMO (điều phối, báo cáo, theo dõi dự án) | DEPARTMENT | CEO |
| OPS | 1. Khối Vận hành (COO) | DIVISION | CEO |
| OPS-PLAN | 1.1 Kế hoạch vận hành và Cost | DEPARTMENT | OPS |
| OPS-SCM | 1.2 Mua hàng và Kho | DEPARTMENT | OPS |
| OPS-KIT | 1.3 Bếp trung tâm (Nhà máy cơm) | DEPARTMENT | OPS |
| OPS-KIT-PREP | Tổ Sơ chế | TEAM | OPS-KIT |
| OPS-KIT-COOK | Tổ Nấu | TEAM | OPS-KIT |
| OPS-KIT-RICE | Tổ Cơm / Canh | TEAM | OPS-KIT |
| OPS-KIT-PORT | Tổ Chia suất / Đóng gói | TEAM | OPS-KIT |
| OPS-KIT-CLEAN | Tổ Vệ sinh | TEAM | OPS-KIT |
| OPS-KIT-QA | QA/QC bếp | TEAM | OPS-KIT |
| OPS-LOG | 1.4 Điều phối giao nhận | DEPARTMENT | OPS |
| OPS-POS | 1.5 Quản lý điểm bán / điểm phát suất | DEPARTMENT | OPS |
| OPS-MNT | 1.6 Thiết bị / Bảo trì | DEPARTMENT | OPS |
| SAL | 2. Khối Kinh doanh | DIVISION | CEO |
| SAL-DEV | 2.1 Phát triển khách hàng (Marketing) | DEPARTMENT | SAL |
| SAL-SALES | 2.2 Bán hàng (tư vấn, báo giá, chốt đơn) | DEPARTMENT | SAL |
| SAL-CON | 2.3 Quản lý hợp đồng / Đơn hàng | DEPARTMENT | SAL |
| SAL-CS | 2.4 Chăm sóc khách hàng | DEPARTMENT | SAL |
| FIN | 3. Khối Tài chính – Hỗ trợ | DIVISION | CEO |
| FIN-ACC | 3.1 Kế toán | DEPARTMENT | FIN |
| FIN-HR | 3.2 Hành chính – Nhân sự | DEPARTMENT | FIN |
| FIN-TRN | 3.3 Tuyển dụng và Đào tạo | DEPARTMENT | FIN |
| FIN-IT | 3.4 IT / Hệ thống và Dữ liệu | DEPARTMENT | FIN |
| QA | 4. Khối QA/QC và ATTP | DIVISION | CEO |
| QA-IN | 4.1 Kiểm soát nguyên liệu đầu vào | DEPARTMENT | QA |
| QA-PROC | 4.2 Kiểm soát quy trình bếp | DEPARTMENT | QA |
| QA-FG | 4.3 Kiểm soát thành phẩm | DEPARTMENT | QA |
| QA-5S | 4.4 Vệ sinh – 5S – ATTP | DEPARTMENT | QA |
| QA-IMP | 4.5 Báo cáo và cải tiến chất lượng | DEPARTMENT | QA |
| ADV | 5. Ban cố vấn (chiến lược, tài chính, vận hành, ATTP) | BOARD | CEO |

Các tổ trong bếp **đồng thời** là `KitchenWorkstation` (Code trùng với mã tổ, `OrganizationUnitId` trỏ về tổ) để ghi nhận công đoạn sản xuất (T17).

## 2. Nhóm quyền `UserGroup` và ma trận quyền – THEO TỪNG MÀN (D-79, anh chốt 2026-11-23)

**UI-6.43 (D-124):** mỗi nhóm có **Mã** (`UserGroup.Code` – các mã dưới đây, dùng cho luồng duyệt / bot / cấu hình) và **Tên** tiếng Việt (vd. OPS-WH = "Thủ kho", FIN-IT = "Quản trị hệ thống"), Mô tả, Khối (BOARD · OPS · SAL · FIN · QA). Dữ liệu cũ điền bằng `DB/scripts/UI643_usergroup_code.sql` (chỉ đổi tên khi tên còn đúng bằng mã).

**MENU-5 (D-79) thay bảng theo nhóm màn cũ** (GAP-PERM-02: E = Thêm + Sửa + Xóa + Nhập áp cho cả nhóm màn → điểm phát / CSKH / QA / bếp có quyền ghi quá rộng). Nay cấp quyền **theo từng màn**:
- **Nguồn duy nhất:** `tools/AppDelicare.CodeGen/Seeds.Menu5.cs` (ScreenMatrix + Approvers5) – bảng do người đọc: ROADMAP **MENU-5.1** (bản chốt) và `docs/claude/MENU5_MATRIX.md` (bản sinh, 19 nhóm × mọi mục menu). Sinh `DB/seed/UserRole_seed.sql` (chỉ thêm dòng thiếu) và `DB/scripts/D79_permission_matrix.sql` (đặt lại cho DB đang chạy, DryRun bắt buộc).
- **Ký hiệu:** Q = Xem + Thêm + Sửa + Xóa + Nhập (chỉ nhóm chủ dữ liệu) · S = Xem + Thêm + Sửa · X = Xem · (IX) = In + Xuất · Duyệt = Xem + Sửa trên mục "Phê duyệt › …".
- **Thao tác kiểm soát = quyền Duyệt** (D-79 a3): Đạt / Tạm giữ / Loại mẻ làm tay (`#/wf-inbox?type=prod-batch`), Giữ / thả lô (`#/wf-inbox?type=scm-lot`), Kết luận kiểm tra hàng nhập (`#/wf-inbox?type=qc-inspection`) – QA. Tạo đơn bù cần Thêm Phản ánh **và** Thêm Đơn đặt suất.
- **Người gửi không duyệt bất kỳ cấp nào** của yêu cầu mình (D-79 a5, `BusinessRules:ApprovalBlockSelfApproval`; admin miễn).
- **Người duyệt** (D-79 d): Hợp đồng · Chính sách giá · Đơn mua ≥ 50 tr: CEO · Sửa đơn sau chốt: SAL-HEAD, COO · Công thức · Thực đơn · Đổi món · Kế hoạch SX · Điều chỉnh tồn · Đơn mua < 50 tr: COO · ĐN mua cấp 1 / 2: OPS-SCM-HEAD / COO · Kiểm kê cấp 1 / 2: OPS-SCM-HEAD, COO / FIN-ACC · Đơn nghỉ · Đổi ca: FIN-HR, COO · Kiểm tra hàng nhập · Mẻ nấu · Giữ / thả lô: QA · Tất cả yêu cầu duyệt: CEO, CEO-PMO, COO chỉ Xem. Hộp phê duyệt chung (`#/wf-inbox`, xem chi tiết mọi yêu cầu): CEO, CEO-PMO, COO, FIN-IT.

**19 nhóm chuẩn** (UserGroup.Name = mã; đổi tên nhóm trên màn quản trị không làm hỏng quyền – quyền nằm ở UserRole):

| Nhóm | Vai trò |
|---|---|
| CEO · CEO-PMO · COO | Ban điều hành – chỉ xem (CEO duyệt việc lớn, COO duyệt việc vận hành) |
| OPS-PLAN | Kế hoạch – thực đơn, công thức, kế hoạch / lệnh SX |
| OPS-SCM | Mua hàng – NCC, nguyên liệu, đề nghị mua, đơn mua (không ghi sổ nhập) |
| **OPS-WH** (mới) | Thủ kho – phiếu nhập / xuất, giữ chỗ, sổ kho, kiểm kê, danh mục kho |
| **OPS-SCM-HEAD** (mới) | Trưởng mua hàng & kho = OPS-SCM ∪ OPS-WH + chỉ số; duyệt ĐN mua cấp 1, kiểm kê cấp 1 |
| OPS-KIT | Bếp – mẻ nấu (không Đạt / Giữ / Loại tay), hao hụt, đóng gói, kiểm tra theo ca |
| OPS-LOG · OPS-POS · OPS-MNT | Giao vận · Điểm phát suất · Bảo trì |
| SAL-SALES · SAL-CON · SAL-CS | Kinh doanh · Hợp đồng (chủ dữ liệu khách hàng / hợp đồng / đơn) · CSKH (phản ánh, không tạo đơn bù) |
| **SAL-HEAD** (mới) | Trưởng kinh doanh = SAL-CON + tạo đơn bù + xem giá vốn / giá thành chuẩn / chỉ số; duyệt sửa đơn sau chốt |
| FIN-ACC · FIN-HR · FIN-IT · QA | Kế toán (xem + in / xuất) · Nhân sự (+ Chức vụ) · Quản trị hệ thống · QA / ATTP |

Ghi vào `UserRole` (AllowAccess / Add / Edit / Delete / Print / Export / Import). Seed chỉ thêm phần thiếu, không ghi đè chỉnh sửa của admin; sau go-live admin đổi quyền trên màn ma trận quyền cũ (`UserRole/GetByUserGroupId`, `UserRole/Update`). Nhóm cũ không xóa; admin chuyển người sang 3 nhóm mới trên màn Tài khoản.

**MENU-4 (D-75 e):** mục bảng con / màn chưa làm / trùng ẩn (`ObjectMenu.IsActive = 0`, giữ dòng UserRole); API bảng con kiểm quyền theo **màn cha** (`ScreenMap.ParentByClass`; MENU-5 thêm Người liên hệ → Khách hàng). Mục `#/wf-inbox` giữ ở DB (quyền của API duyệt), giao diện bỏ khỏi sidebar.

Ngưỡng duyệt cụ thể (cutoff 20h, lệch > 2%, > 30%, giá trị PO…) theo BUSINESS_RULES.md và DECISIONS.md. `ApprovalStep.ApproverRoleCode` dùng **mã UserGroup** ở trên.

## 3. Luồng vận hành 9 bước ↔ hệ thống

| Bước | Bộ phận chính | Màn hình / API | Ghi chú triển khai |
|---|---|---|---|
| 1. Dự báo và tiếp nhận nhu cầu | SAL, OPS-POS | P3: Forecast, Order (khách → địa điểm → vị trí), chốt 20h | Điểm phát suất tổng hợp nhu cầu → đơn |
| 2. Kế hoạch vận hành | OPS-PLAN | P3 thực đơn, P5 kế hoạch sản xuất, P8 phân ca | Kế hoạch thiết bị = GAP-MNT-01 (R2) |
| 3. Mua hàng và kho | OPS-SCM | P4 | FIFO / FEFO theo `InventoryPolicie.UseFefo` (D-24) |
| 4. Kiểm soát nguyên liệu (QA) | QA-IN | P6 QC đầu vào, COA qua DocumentFile | Đạt → nhập; không đạt → trả / cách ly |
| 5. Sản xuất tại bếp trung tâm | OPS-KIT (5 tổ) + QA-PROC | P5 mẻ, công đoạn theo tổ, TemperatureLog, checklist | Sơ chế → Nấu → Cơm/Canh → Chia suất/Đóng gói |
| 6. Kiểm soát thành phẩm (QA) | QA-FG | P6 checklist: cảm quan, trọng lượng, lưu mẫu + ký | |
| 7. Giao nhận | OPS-LOG | P7 | Đúng giờ, đúng số lượng, nhiệt độ ≥ 60°C |
| 8. Điểm bán / khách hàng | OPS-POS, SAL-CS | P7 POD + P3 đối soát suất thực tế (`OrderReconciliation`) + phản ánh | **Mô hình a** (D-21); bán lẻ = R2 |
| 9. Phản hồi và cải tiến | SAL-CS, QA-IMP | CustomerComplaint / ComplaintHistory, báo cáo NCR tổng hợp, KPI | Sổ CAPA riêng = GAP-QA-01 (R2) |

## 4. Agent (Release 2) theo tổ chức
VIVI ↔ CEO-PMO · SALES ↔ SAL-SALES / SAL-CON · CSKH ↔ SAL-CS · PLAN ↔ OPS-PLAN · PROC + WH ↔ OPS-SCM · KITCHEN + PKG ↔ OPS-KIT · LOG ↔ OPS-LOG · QA ↔ QA · HR ↔ FIN-HR / FIN-TRN · ACC ↔ FIN-ACC.

Bổ sung ở R2: **POS** (điểm phát suất / bán lẻ) và **MNT** (bảo trì). Vault Obsidian thêm thư mục `57 Điểm bán` và `58 Thiết bị – Bảo trì` (PROPOSED).
