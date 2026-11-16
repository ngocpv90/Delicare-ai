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

## 2. Nhóm quyền `UserGroup` và ma trận quyền (PROPOSED – anh duyệt khi UAT)

Một `UserGroup` cho mỗi vai trò, đặt tên theo mã. Quyền theo nhóm màn hình của mockup (`catalog.js`, cột group). Ký hiệu:
- **V** = xem
- **E** = thêm / sửa
- **A** = duyệt
- **X** = xuất / in
- **—** = không có quyền

Ghi vào `UserRole` (AllowAccess / Add / Edit / Delete / Print / Export / Import) qua `DB/seed/UserRole_seed.sql`.

**Cột A (duyệt) – D-31:** không cấu hình trong appsettings. Mỗi loại duyệt là một mục `ObjectMenu` cấp 2 dưới nhóm "Phê duyệt" (MetaLink `#/wf-inbox?type=<screen-id>`, 14 màn có loại AP trong catalog.js); vai trò có **A** được seed `UserRole.AllowAccess + AllowEdit` trên mục đó. Seed chỉ thêm phần thiếu, không ghi đè chỉnh sửa của admin. Sau go-live, admin đổi quyền trên màn hình ma trận quyền cũ (`UserRole/GetByUserGroupId`, `UserRole/Update`).

| Nhóm màn hình → / Vai trò ↓ | Quản trị (P0) | KD & Đơn hàng | Thực đơn & Món | Mua hàng & Kho | Sản xuất & Giá thành | QC & ATTP | Đóng gói & Giao nhận | Nhân sự & Duyệt | KPI / Dashboard |
|---|---|---|---|---|---|---|---|---|---|
| CEO | V | V A | V A | V A | V A | V | V | V A | V X |
| CEO-PMO | V | V | V | V | V | V | V | V | V X |
| COO (trưởng OPS) | — | V | V A | V A | V A | V | V A | V A | V X |
| OPS-PLAN | — | V | V E | V | V E X | V | V | — | V |
| OPS-SCM (mua hàng, thủ kho) | — | V | V | V E X | V | V | — | — | V |
| OPS-KIT (bếp trưởng, trưởng ca, tổ trưởng) | — | V | V | V (đề nghị xuất) | V E | V E | V | V (ca) | V |
| OPS-LOG (điều phối, tài xế) | — | V | — | — | V | V (nhiệt độ giao) | V E X | — | V |
| OPS-POS (điểm phát suất) | — | V E (đối soát suất) | V | — | — | V | V E (nhận hàng, POD) | — | V |
| OPS-MNT | — | — | — | — | V | V | — | — | — |
| SAL-SALES / SAL-CON | — | V E X | V | — | V | — | V | — | V |
| SAL-CS | — | V | V | — | — | V | V E (phản ánh) | — | V |
| FIN-ACC | — | V X | V | V X | V X (giá thành) | — | V X (hộp / khay) | V X (chấm công) | V X |
| FIN-HR | V (nhân sự) | — | — | — | — | — | — | V E A X | V |
| FIN-IT (admin) | V E X | V | V | V | V | V | V | V | V |
| QA (4.1–4.5) | — | V | V | V E (QC đầu vào) | V | V E A X | V | — | V |

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
