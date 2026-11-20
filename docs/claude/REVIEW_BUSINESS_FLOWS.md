# Rà soát luồng nghiệp vụ đã hoàn thành (BE + FE) · 2026-11-19

> Tài liệu rà soát – chỉ ghi nhận hiện trạng, **không phải task**. Việc cần làm tiếp (mục 5) chỉ thành task khi anh duyệt đưa vào ROADMAP.

## 0. Phạm vi và cách rà

| Nguồn | Bản đọc |
|---|---|
| `docs/claude/ROADMAP.md`, `PROGRESS.md`, `UAT_CHECKLIST.md`, `DECISIONS.md` | bản 2026-11-18 22:43 |
| `docs/reference/DELICARE_API_Catalog.xlsx` | cập nhật lần cuối 2026-11-17 17:30 (đã cũ – xem mục 4) |
| `docs/frontend-handoff/README.md` | mục lục bàn giao FE |
| `app-delicare-client/src` – `app.routes.ts`, `delicare/delicare-host.ts`, `assets/delicare/js/core/*`, `assets/delicare/js/modules/*.js` | bản 2026-11-18 22:46 |

Cách đánh giá:
- **Backend**: theo trạng thái task trong ROADMAP / PROGRESS, file `.http` đã chạy và số test tự động.
- **Frontend**: đọc code – mỗi màn (`APP.register('<screen-id>')`) gọi những API nào (`<Controller>/<Action>`). Store chạy 2 chế độ: **api** (mặc định trong Angular, bảng nào có controller thì đọc / ghi qua `GetAllData` / `GetAll` / `Insert` / `Update` / `Delete`) và **demo** (dữ liệu mockup). Chưa chạy thử trên trình duyệt.

Ký hiệu: ✅ có · ⚠️ có nhưng thiếu một phần · ❌ chưa có.

## 1. Tình trạng chung

| Hạng mục | Trạng thái |
|---|---|
| Backend | Xong toàn luồng R1 (đợt BE-2: BE2.1–BE2.7, BE2.9; BE2.8 để sau – D-45), đợt BE-3 (BE3.1–BE3.5; BE3.6 không làm – D-58), job tự động (T23.1), bot Telegram (BOT-1.1 → 1.7d, 2.1 → 2.4). Test tự động **437 / 437**. `full-flow.http` (hợp đồng → đối soát) **216 / 216** trên DB dev và staging. |
| Frontend | Bạn FE dựng giao diện theo mockup, đã gộp vào `main` (PR #1 GHEP-FE). **54 màn**; ở chế độ api **52 / 54** màn gọi API thật (2 màn DB Explorer `dbx-explorer`, `dbx-gap` bỏ theo D-58). |
| Hạ tầng | Docker production (nginx + api + job + messaging – D-60, D-62), SQL Server 2022 (T48.5), DB quản lý bằng migration (DB-MIGRATIONS). **Docker chưa chạy trên server thật.** |
| Chưa làm | UAT trên giao diện; BOT-2.9 (server test + token bot thật); BE2.8 (D-45). |

## 2. Luồng nghiệp vụ theo thứ tự R1

| # | Luồng | Backend | FE: màn hình → API đã gọi |
|---|---|---|---|
| 1 | **Nền tảng, quản trị** – đăng nhập, người dùng, nhóm, phân quyền UserRole × ObjectMenu, sao chép quyền, reset mật khẩu, khóa / mở người dùng, cơ cấu tổ chức, nhật ký thay đổi, xung đột gộp dữ liệu | ✅ T07, T45R, T45R.8, T48.0 | ✅ `adm-user`, `adm-usergroup`, `adm-permission`, `adm-orgunit`, `adm-objectmenu`, `adm-merge-conflict` (`User/ResetPassword`, `User/SaveIsActive`, `UserRole/CopyFromUserGroup`, `UserRole/SaveAccessAll`, `ObjectMenu/GetTree`, `CoreDataMergeConflict/Resolve`…) |
| 2 | **Danh mục** – khách hàng, điểm giao, liên hệ, NCC + nguyên liệu NCC, nguyên liệu, đơn vị tính + quy đổi, kho / khu / vị trí, xe, ca, nhập Excel | ✅ T45B, T49 | ✅ màn danh mục (CRUD chuẩn), `sys-import` (màn engine *Nhập dữ liệu gốc (Excel)* – nối lại 2026-11-19, UG-1 C-03), `crm-customer` (`Customer/GetOverview`), `scm-supplier` (`Supplier/GetOverview`), `scm-warehouse` (`Warehouse/GetLayout`) |
| 3 | **Món, công thức, giá** – công thức có phiên bản, gửi / khóa, tính giá công thức, giá chuẩn, chính sách giá | ✅ T45B.4 | ✅ `menu-dish`, `menu-recipe` (`Recipe/CreateVersion`, `Submit`, `Lock`, `CalculateCost`), `cost-standard` (`StandardCost/Recalculate`), `crm-price-policy` (`PricePolicy/Resolve`) |
| 4 | **Hợp đồng** – kèm loại suất, số HD-, gửi duyệt, chấm dứt, tự hết hạn | ✅ BE2.2, T23 | ✅ `crm-contract` (`Contract/Create`, `Submit`, `Terminate`, `ExpireOverdue`, `GetDetail`) |
| 5 | **Dự báo** – nhập phiên bản, công bố, ma trận, gợi ý số suất | ✅ BE2.2 | ✅ `crm-forecast` (`Forecast/Import`, `Publish`, `GetMatrix`) |
| 6 | **Thực đơn** – lịch tuần, nhân bản, phân công theo khách, xuất thực đơn gửi khách, yêu cầu đổi món | ✅ T13, BE2.2 | ✅ `menu-menu` (`Menu/GetCalendar`, `Clone`, `ExportForCustomer`), `menu-assignment` (`MenuAssignment/Resolve`), `menu-change-request` (`MenuChangeRequest/Create`) |
| 7 | **Đơn hàng** – mẫu đơn, bảng đơn, chốt 20:00 (job), sửa / thêm sau giờ chốt qua duyệt, lệch dự báo | ✅ T13, BE2.2, T23 | ✅ `crm-order` (`Order/GetNewTemplate`, `Create`, `UpdateLines`, `GetBoard`, `GetDetail`) |
| 8 | **Nhu cầu nguyên liệu (MRP)** – tính 20:30 (job), tính bổ sung, dự phòng, trừ hàng đang về, phiếu trước sản xuất | ✅ T14, T14.1, BE2.4 | ✅ `scm-requirement` (`MaterialRequirement/Calculate`, `CalculateTopUp`, `CreatePurchaseRequest`, `ExportPreProduction`) |
| 9 | **Mua hàng** – đề nghị mua 2 cấp, chuyển PO, PO 2 cấp theo ngưỡng (GĐ duyệt ≥ 50 triệu), gửi / đóng / hủy, in đơn đặt | ⚠️ T15, T15.1, BE2.3 – **chưa gửi PO cho NCC qua email / Zalo** (Catalog 113) | ✅ `scm-pr` (`PurchaseRequest/Submit`, `ConvertToPurchaseOrder`, `Cancel`), `scm-po` (`PurchaseOrder/Submit`, `Send`, `Close`, `Cancel`, `ExportOrder`) |
| 10 | **Nhận hàng + QC đầu vào** – nhiệt độ 3 vùng, kết luận QC, sự cố NCC | ✅ T15, BE2.5 | ✅ `scm-gr` (`GoodsReceipt/Create`, `Post`, `Cancel`, `Print`), `qc-inspection` (`QcInspection/Conclude`), `qc-supplier-issue` (`SupplierIssue/Transition`) |
| 11 | **Kho** – sổ kho chỉ ghi thêm, tồn theo lô, xuất FEFO có gợi ý lô, chuyển / điều chỉnh qua duyệt, kiểm kê (lệch > 2% duyệt 2 cấp), giữ / giải phóng lô, xử lý lô hết hạn, truy xuất 2 chiều | ✅ T16, BE2.3, BE2.4 | ✅ `scm-inv-balance`, `scm-lot` (`MaterialLot/Hold`, `Release`, `ProcessExpired`, `Trace`), `scm-issue` (`MaterialIssue/SuggestLots`, `Post`, `Print`), `scm-inv-txn` (`InventoryTransaction/Adjust`, `Transfer`), `scm-stockcount`, `scm-reservation` |
| 12 | **Kế hoạch sản xuất** – sinh theo ca (4 ca / ngày, 1 KH- / ngày), duyệt, phát hành + giữ chỗ nguyên liệu, phiếu sản xuất | ✅ T17, T17.1, BE2.1, BE2.4 | ✅ `prod-plan` (`ProductionPlan/Generate`, `Approve`, `Release`, `ExportProductionSheet`) |
| 13 | **Lệnh và mẻ sản xuất** – xuất nguyên liệu cho mẻ, nhiệt độ lõi, giữ / đạt / loại mẻ, hao hụt, đóng / hủy lệnh, chênh lệch tiêu hao | ✅ T17, BE2.4 | ✅ `prod-order` (`ProductionOrder/Reserve`, `Close`, `Cancel`), `prod-batch` (`ProductionBatch/GetBoard`, `Hold`, `Release`, `Reject`, `RecordWaste`, `CreateMaterialIssue`), `prod-waste`, `scm-consumption-var` |
| 14 | **Giá thành** – thực tế, giá vốn suất | ✅ BE2.4 | ✅ `cost-actual`, `cost-meal` (`MealCost/Calculate`, `ProductionOrder/CalculateCost`) |
| 15 | **ATTP, vệ sinh** – checklist theo ca / mẻ (lỗi trọng yếu giữ mẻ, chặn QC / đóng gói / xuất chuyến), nhật ký nhiệt độ, vệ sinh khay 6 bước | ✅ BE2.5 | ✅ `qc-checklist-result` (`FoodSafetyChecklistResult/GetRun`, `Record`), `qc-temperature` (`TemperatureLog/Record`), `qc-cleaning`, `qc-cleaning-result` (`CleaningBatch/Create`, `RecordResults`, `Complete`) |
| 16 | **Đóng gói** – sinh từ mẻ, bắt đầu, ghi kết quả | ✅ BE2.1 | ✅ `log-packing-order`, `log-packing-result` (`PackingOrder/GenerateFromBatch`, `Start`, `RecordResult`, `Cancel`) |
| 17 | **Giao hàng** – đơn giao (tạo tay / tách / gán / hủy), lập chuyến nhiều xe theo khu vực, xếp xe bắt buộc, xuất phát, đến / giao / thất bại, POD, phiếu giao, bảng thực hiện giao, vị trí xe | ✅ T19, BE2.1, BE3.1 | ✅ `log-delivery-order` (`DeliveryOrder/Create`, `Split`, `AssignRoute`, `Cancel`, `Print`), `log-route` (`DeliveryRoute/Plan`, `Create`, `UpdateStops`, `Load`, `Dispatch`, `ExportDeliveryNote`), `log-execution` (`DeliveryStop/Arrive`, `Deliver`, `Fail`, `ProofOfDelivery/Print`, `DeliveryEvent/RecordPosition`) |
| 18 | **Đối soát suất** – đặt / giao / nhận, chốt, in, biểu đồ | ✅ T19, BE3.1 | ✅ `crm-reconciliation` (`OrderReconciliation/GetPendingOrders`, `Reconcile`, `Close`, `Print`, `GetList`) |
| 19 | **Phản ánh khách, hộp / khay** – phản ánh có SLA, thu hồi + kiểm tra khay, sổ bao bì, tỷ lệ thất thoát | ✅ T19, BE2.5 | ✅ `log-complaint` (`CustomerComplaint/Create`), `log-container-return`, `log-return-inspection` (`ContainerReturn/Record`, `Inspect`), `log-packaging-ledger` (`PackagingLedger/Record`, `GetBalance`) |
| 20 | **Nhân sự** – lưới phân ca, phân ca hàng loạt, xác nhận ca, vào / ra ca GPS / QR / chấm hộ, bảng công, đánh vắng 08:00 (job), đơn nghỉ, đổi ca | ✅ BE2.6, T23 | ✅ `hr-shift-assign` (`ShiftAssignment/GetGrid`, `Assign`, `BulkAssign`, `Confirm`, `MarkAbsent`), `hr-attendance` (`AttendanceEvent/CheckIn`, `CheckOut`, `GetTimesheet`), đơn nghỉ / đổi ca (`LeaveRequest/*`, `ShiftSwapRequest/*`) |
| 21 | **Duyệt nhiều cấp** – hộp duyệt theo cấp, quá hạn, hủy | ✅ T12, BE2.3 | ✅ `wf-inbox` (`ApprovalRequest/GetMyPending`, `Decide`, `Cancel`, `GetDetail`) |
| 22 | **Dùng chung** – chuông cảnh báo, tìm kiếm toàn cục, tab Liên quan, xuất Excel / CSV, tệp đính kèm, in PDF + in hàng loạt | ✅ BE2.7, BE2.7.1, BE3.3 | ⚠️ đã gọi `Common/GetAlerts`, `GlobalSearch`, `GetRelated`, `ExportList`, `PrintBatch`, `DocumentFile/Upload`, `Download`; `Common/GetHistory` (tab **Lịch sử xử lý**, màn khách hàng 360) và `Common/GetStatusSummary` (số trên chip lọc nhanh) – đã gọi (sửa theo UG-1 C-04) |
| 23 | **Dashboard, KPI, IoT** – dashboard điều hành, hành trình đơn, KPI ngày / tháng, cảm biến nhiệt độ | ✅ BE3.1 → BE3.5 | ✅ `dash-exec` (`Dashboard/GetExecutive`), `flow-e2e` (`Dashboard/GetOrderJourney`), `kpi-daily`, `kpi-monthly` (`KpiSnapshot/GetDaily`, `GetMonthly`, `Refresh`), biểu đồ nhiệt độ (`TemperatureLog/GetSeries`). IoT là cổng máy, không có màn. |
| 24 | **Job tự động** – chốt đơn 20:00, MRP 20:30, lô hết hạn 00:15, hợp đồng hết hạn 00:30, đánh vắng 08:00 | ✅ T23.1 | ❌ chưa có màn `adm-job` (`Job/GetAll`, `Job/Run` – handoff `T23.md`) |
| 25 | **Bot Telegram** – 3 bot Vận hành / Điều hành / Trợ lý, liên kết tài khoản, menu theo quyền, chấm công, duyệt theo mức rủi ro, Thẻ ca / Thẻ ngày, sự việc liên phòng + leo thang, giờ yên lặng, tin theo lịch, QR phiếu in / vật thể, vị trí tài xế, nhóm chat | ✅ BOT-1.1 → 1.7d, BOT-2.1 → 2.4 (D-62); container `messaging` | Không làm FE trong đợt BOT (chỉ BE). FE-BOT-1/2/3 ghi nhận bàn giao (DECISIONS mục E). Màn `int-inbound` / `int-outbound` hiện chỉ xem bảng thô. |

## 3. Kiểm chứng đã có

| Loại | Kết quả |
|---|---|
| Test tự động (`dotnet test`) | 437 / 437 (2026-11-18, sau BOT-1.7d) |
| Toàn luồng R1 bằng API (`docs/api-tests/full-flow.http`) | 216 request / 22 chốt / 60 kiểm số liệu – đạt trên DB dev và staging |
| File `.http` từng đợt | `pilot-flow`, `be2.1` … `be2.7`, `t23-jobs`, `be3.1` … `be3.5` – đạt trên DB dev |
| Bot | `tools/bot-smoke/flow-*.mjs` với Telegram giả – đạt; **chưa chạy với bot thật trên server** |
| Giao diện | **Chưa UAT** (UAT_CHECKLIST mục PILOT-FE "để sau") |

## 4. Lệch giữa Catalog API và thực tế

Catalog (cập nhật 2026-11-17 17:30) đang ghi: **210 Đã xong · 5 Một phần · 10 Chưa bắt đầu · 9 Không làm** / 234 dòng. Các dòng đã làm xong nhưng Catalog chưa cập nhật:

| Dòng Catalog | Catalog ghi | Thực tế |
|---|---|---|
| 214–220 (Tích hợp Zalo / Telegram / Facebook) | Chưa bắt đầu | Telegram xong theo D-62 (`BotChannel/*`, container `messaging`); Zalo / Facebook chưa làm (GAP-INT-01 / D-06) |
| 221–225 (KPI) | Một phần / Chưa bắt đầu | Xong (BE3.4) |
| 231 (Dashboard điều hành), 232 (Hành trình đơn) | Chưa bắt đầu | Xong (BE3.5) |
| 113 (PO gửi NCC) | Một phần | Vẫn một phần – nút **Gửi NCC** chỉ đổi trạng thái sang Đã gửi NCC; gửi email / Zalo tự động là việc sau (UG-1 C-14) |

Sau khi cập nhật, chỉ còn **113** là "Một phần" và phần Zalo / Facebook của 214–220 là chưa làm.

## 5. Việc còn lại (đề xuất – chờ anh duyệt mới đưa vào ROADMAP)

| # | Việc | Ai làm | Đề xuất |
|---|---|---|---|
| 1 | UAT toàn luồng R1 trên giao diện (staging) theo `UAT_CHECKLIST` mục TOÀN LUỒNG R1, thao tác trên màn thay vì `.http` | Anh + bạn FE | Làm ngay sau khi bạn FE báo xong đợt sửa hiện tại – đây là chỗ hở lớn nhất |
| 2 | FE: màn `adm-job`; tab Lịch sử (`Common/GetHistory`); thẻ / chip trạng thái (`Common/GetStatusSummary`) | Bạn FE | Ghi vào `docs/frontend-handoff/` (API đã có, handoff `T23.md`, `BE2.7.md`) |
| 3 | BOT-2.9: dựng bot trên server test với token thật | Claude Code + anh (cấp server, tạo bot) | Theo ROADMAP BOT-2.9 |
| 4 | Chạy Docker production trên server thật (T48.4 mới kiểm bằng đọc) | Claude Code + anh | Gộp chung lần dựng server test của BOT-2.9 |
| 5 | Cập nhật Catalog API theo mục 4 | Claude Code | Task nhỏ, không đụng code |
| 6 | Gửi PO cho NCC qua email / Zalo (Catalog 113) | Chưa xếp | Để sau go-live, cùng quyết định kênh Zalo (D-06) |
