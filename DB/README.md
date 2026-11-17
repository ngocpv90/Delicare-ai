# DB – Script SQL của DELICARE

| Thư mục | Nội dung | Ai chạy |
|---|---|---|
| `migrations/` | `20261113_restore_structure_db_appdelicare.sql`: script **chính thức của PO**, nâng DB cũ (91 bảng) lên lược đồ chuẩn 124 bảng `dbo`. Nguồn: `D:\Duan\Delicare\SQLScript`. | Anh duyệt; chỉ chạy trên DB dev / UAT **sau khi backup** |
| `sp/` | Stored procedure (`CREATE OR ALTER`), một file mỗi SP: `usp<Entity>_GetAll.sql`… Gồm SP lấy ra từ DB dev (T00) và SP mới do CodeGen sinh. | Claude Code chạy trên DB dev |
| `sp/_MISSING_FROM_DB.md` | Danh sách 74 SP mà code gọi nhưng DB dev chưa có (T00). | – |
| `_ssms_diagrams/` | 7 SP `sp_*diagram*` + `fn_diagramobjects` do SSMS tự tạo khi vẽ sơ đồ. Chỉ lưu tham khảo, **không phải** SP nghiệp vụ, không cần chạy. | – |
| `seed/` | Dữ liệu cấu hình (Shift, ObjectMenu, UserGroup, KpiMetricDefinition…). Dữ liệu demo gắn `[DEMO]`. | Claude Code chạy trên DB dev |

Không có script tạo bảng / cột ngoài `migrations/` đã được PO duyệt.

## Pilot (T11 … T19, T45R.8, T48.0, T14 / T15.1 / T17 / T17.1) – script SQL thêm / sửa và thứ tự chạy

So với commit `9d03b40` (trước đợt Pilot). Chạy **đúng thứ tự** dưới đây, luôn bằng `sqlcmd ... -b -I -f 65001` (**`-f 65001` bắt buộc** – thiếu cờ này tên tiếng Việt bị lỗi font khi ghi vào DB). Mọi script chạy lại được nhiều lần (SP `CREATE OR ALTER`; seed chỉ **thêm** phần chưa có, không ghi đè chỉnh sửa trên màn hình). Seed cần tài khoản `admin` đã có (ghi `CreatedBy`).

**Thứ tự chạy chính thức = `deploy/db/scripts.order`** (T48.3 – nguồn **duy nhất**; `deploy/db/Deploy-Database.ps1` và `deploy/docker/db-init.sh` cùng đọc file này: 131 SP `DB/sp` → `00_Admin_seed.sql` → 8 seed dưới đây). **SP / seed mới phải thêm 1 dòng vào cuối nhóm tương ứng trong `scripts.order`** (và 1 dòng vào bảng dưới). SP quên ghi vẫn chạy (sau cùng) kèm cảnh báo; seed quên ghi **không chạy**. Dùng 2 script này cho staging / DB mới / server Docker của dev FE (`deploy/docker/README.md`).

| # | Script | Task | Nội dung |
|---|---|---|---|
| 1 | `sp/uspInventoryTransaction_GetLedger.sql` (mới) | T16 | Sổ cái kho lọc khoảng ngày (`InventoryTransaction/GetLedger`) |
| 2 | `sp/uspSupplier_GetAll.sql` (sửa) | T14 (FE-2) | Đủ cột ViewModel + lọc `Status` |
| 3 | `sp/uspUnitMeasure_GetAll.sql` (sửa) | T14 (FE-2) | Đủ cột ViewModel + lọc `Status` |
| 4 | `sp/uspUser_GetAll.sql` (sửa) | T45R.8 | Thêm `@IsActive` |
| 5 | `seed/00_Admin_seed.sql` (sửa) | T48.0 | Tạo `admin` nếu chưa có – nhận hash **PBKDF2** (biến sqlcmd `AdminUserName`, `AdminPasswordHash` – truyền bằng `:setvar`, không dùng `-v`, T48.3); chỉ chạy qua `Deploy-Database.ps1 -AdminPassword` hoặc `db-init.sh` (`ADMIN_PASSWORD`, khi chưa có admin) |
| 6 | `seed/OrganizationUnit_seed.sql` (không đổi) | T07 | Cơ cấu tổ chức (chạy trước để các seed sau có công ty) |
| 7 | `seed/ObjectMenu_seed.sql` (sửa) | T13, T15.1 | + mục "Duyệt – Đơn đặt suất ăn" (`#/wf-inbox?type=crm-order`), "Duyệt – Đơn mua hàng (Giám đốc)" (`#/wf-inbox?type=scm-po-director`) |
| 8 | `seed/ObjectMenu_disable_legacy.sql` (mới) | T45R.8 (D-37) | Tắt 26 menu app cũ (không xóa) |
| 9 | `seed/UserRole_seed.sql` (sửa) | T13, T15.1 | Quyền duyệt 2 mục trên cho nhóm CEO |
| 10 | `seed/Shift_seed.sql` (không đổi) | T21.1 | 4 ca `SANG_6H` / `TRUA` / `TOI` / `DEM_23H` (Pilot chạy 4 ca – T17.1) |
| 11 | `seed/FoodSafetyChecklist_seed.sql` (mới) | T17 | Bộ `PRD_BATCH`: `SAMPLE_RETAINED` (lưu mẫu – bắt buộc trước khi giao, D-42), `CORE_TEMP` |
| 12 | `seed/ProductionStage_seed.sql` (mới) | BE2.4 | Bộ `PRD_STAGE` "Công đoạn sản xuất theo tổ": `SO_CHE` → `NAU` → `CHIA_SUAT` (PROPOSED – D-53 e). Chỉ thêm khi chưa có (theo Code / ItemCode), không ghi đè |
| 13 | `seed/FoodSafetyQa_seed.sql` (mới) | BE2.5 | Bộ checklist `QA_FG` (thành phẩm), `PREP`, `PREP_AREA_CLEAN`, `BLAST_CHILL`, `KITCHEN_CLEAN` (5S), `VS-KHAY` (vệ sinh khay 6 bước) – PROPOSED D-54. Chỉ thêm khi chưa có (theo Code / ItemCode), không ghi đè |
| 14 | `seed/SystemUser_seed.sql` (mới) | T23 | Tài khoản dịch vụ `system` (Id cố định `061877ad-396c-4386-8868-25d4940bf467` = `SystemUsers:JobUserId`) cho job tự động – không đăng nhập được (mật khẩu không phải hash hợp lệ), không cấp nhóm quyền. Chỉ thêm khi chưa có |

T11, T12, T13 (ngoài seed menu), T14, T15, T15.1, T17, T17.1, T19, T50R1: **không có SP / seed mới** (đọc / ghi qua EF + SP `usp*_GetAll` sẵn có).

**BE2.1 (2026-11-17): không có SP / seed mới** – không cần thêm gì vào `deploy/db/scripts.order` (T48.3). Mục menu của mọi màn hình BE2.1 (`log-packing-order`, `log-loading`, `log-execution`, `log-delivery-order`, `qc-temperature`, `prod-waste`, `scm-lot`, `wf-inbox`…) đã có trong `seed/ObjectMenu_seed.sql`. Thay đổi cấu hình (appsettings, không phải DB): `BusinessRules:DeliveryRequireLoading` (true), `ReferenceLists:WasteType` thêm `PACKING_DAMAGE`, `ReferenceLists:TemperatureMeasurementType` (mới) – môi trường có appsettings riêng (staging / Docker) cần bổ sung 3 khóa này.

**BE2.2 (2026-11-17): không có SP / seed mới** – không thêm dòng nào vào `deploy/db/scripts.order`. Mục duyệt `#/wf-inbox?type=crm-contract`, `menu-change-request`, `crm-order` đã có trong `seed/ObjectMenu_seed.sql` / `UserRole_seed.sql` (nhóm CEO; COO thêm đổi món). Cấu hình mới (appsettings): `BusinessRules:ForecastVariancePctWarning` (10), `BusinessRules:ForecastMaxDays` (62) – có giá trị mặc định trong code.

**BE2.3 (2026-11-17): không có SP / file seed mới** – chỉ **sinh lại** 2 seed đã có trong `deploy/db/scripts.order` (`seed/ObjectMenu_seed.sql`, `seed/UserRole_seed.sql`) bằng `dotnet run --project tools/AppDelicare.CodeGen -- --layers security` (`Seeds.ExtraApprovals`): thêm 3 mục duyệt `#/wf-inbox?type=scm-pr-l1` (OPS-SCM), `scm-pr-l2` (COO), `scm-stockcount-l2` (FIN-ACC) + quyền AllowAccess / AllowEdit (insert-only, idempotent). **Đã chạy 2 seed trên DB dev `AppDelicare`** (`sqlcmd -b -I -f 65001`). Server Docker của dev FE: `db-init` tự chạy lại 2 seed. Cấu hình mới (appsettings, có mặc định trong code): `BusinessRules:ApprovalSteps`, `ApprovalStepDueHours` (48), `ApprovalStepDueHoursByType`, `ApprovalBlockOverdueDecision` (false), `ApprovalRequireDistinctApprovers` (true), `PurchaseOrderDirectorTwoLevel` (false), `StockAdjustmentRequireApproval` (true).

**BE2.5 (2026-11-17): 1 seed mới `seed/FoodSafetyQa_seed.sql`** (mục 13) – đã thêm vào `deploy/db/scripts.order` ngay sau `ProductionStage_seed.sql`; **đã chạy trên DB dev 2 lần** (`sqlcmd -b -I -f 65001`, lần 2 không thêm dòng). Không SP mới, không bảng / cột.

**BE2.4 (2026-11-17): 1 seed mới `seed/ProductionStage_seed.sql`** (mục 12) – đã thêm vào `deploy/db/scripts.order` ngay sau `FoodSafetyChecklist_seed.sql`; **đã chạy trên DB dev 2 lần** (`sqlcmd -f 65001`, lần 2 không thêm dòng). Không SP mới (đọc / ghi qua EF + `usp*_GetAll` sẵn có), không bảng / cột.

**BE2.6 (2026-11-17): không có SP / seed mới** – mục menu / quyền `#/hr-shift-assign`, `#/hr-attendance`, `#/hr-leave`, `#/hr-swap`, `#/wf-inbox?type=hr-leave / hr-swap` đã có trong seed T07.

**BE2.7 (2026-11-17): không có SP / seed mới** – tệp đính kèm lưu ngoài DB (`DocumentStorage:RootPath`, xem `deploy/README.md`); DB chỉ giữ khóa lưu trữ trong `DocumentFile.FileNameHas` (GAP-DOC-01).

**BE2.9 (2026-11-17) – kiểm tra cuối đợt BE-2:** `deploy/db/scripts.order` (140 dòng: 131 SP + `00_Admin_seed.sql` + 8 seed) **khớp đúng** với `DB/sp/*.sql` + `DB/seed/*.sql` (so bằng `diff`, không thiếu / không thừa); không SP / seed mới trong BE2.9. Đã chạy `Deploy-Database.ps1` trên staging `AppDelicare_Staging` (0 lỗi, `Repair-VietnameseText.ps1` chạy thử: 0 giá trị lỗi font).

### Khóa cấu hình mới của đợt BE-2 (appsettings.json – có giá trị mặc định, PROPOSED; staging / Docker kế thừa từ appsettings.json khi publish)

| Khóa | Mặc định | Task | Ý nghĩa ngắn |
|---|---|---|---|
| `BusinessRules:DeliveryRequireLoading` | true | BE2.1 | Xuất phát chỉ khi mọi đơn giao đã xếp lên xe |
| `ReferenceLists:WasteType` + `PACKING_DAMAGE`; `ReferenceLists:TemperatureMeasurementType` | – | BE2.1 | Loại hao hụt khi đóng gói; loại đo nhiệt độ tự do |
| `BusinessRules:ForecastVariancePctWarning`, `ForecastMaxDays` | 10, 62 | BE2.2 | Cảnh báo lệch dự báo khi xác nhận; khoảng ngày ma trận / công bố |
| `BusinessRules:ApprovalSteps`, `ApprovalStepDueHours`, `ApprovalStepDueHoursByType`, `ApprovalBlockOverdueDecision`, `ApprovalRequireDistinctApprovers`, `PurchaseOrderDirectorTwoLevel`, `StockAdjustmentRequireApproval` | {}, 48, {}, false, true, false, true | BE2.3 | Duyệt nhiều cấp |
| `BusinessRules:ProductionReserveOnRelease`, `MrpIncludeIncomingPurchaseOrders`, `ProductionRequireStages`, `ProductionStageTeams`, `ProductionLaborCostPerHour`, `ConsumptionVariancePctWarning` | true, true, false, {SO_CHE: OPS-KIT-PREP, NAU: OPS-KIT-COOK, COM_CANH: OPS-KIT-RICE, CHIA_SUAT: OPS-KIT-PORT}, 0, 5 | BE2.4 | Giữ chỗ, hàng sắp về, công đoạn theo tổ, giá thành |
| `BusinessRules:FoodSafetyCriticalFailHoldsBatch`, `CleaningChecklistCode`, `CleaningChemicalRequiredItems`, `CleaningAutoCreateOnInspect`, `PackagingOutOnDelivery`, `PackagingDefaultType`, `PackagingUnitsPerMeal`; `ReferenceLists:PackagingType`, `PackagingTransactionType` | true, VS-KHAY, [KHU_KHUAN], true, true, KHAY, 1 | BE2.5 | ATTP, vệ sinh khay, sổ bao bì |
| `BusinessRules:Hr:*` (`CheckInEarlyMinutes` 120, `CheckInLateMinutes` 0, `LateGraceMinutes` 5, `EarlyLeaveGraceMinutes` 5, `RejectOutsideRadius` false, `DefaultRadiusMeters` 150, `BulkAssignMaxDays` 31, `TimesheetMaxDays` 62, `LeaveMaxBackdateDays` 7, `LeaveMaxDays` 30, `GridDays` 7); `ReferenceLists:LeaveType`, `AttendanceSource` | – | BE2.6 | Nhân sự |
| `BusinessRules:Common:ExportMaxRows`, `PrintBatchMaxItems`, `HistoryMaxItems` | 10000, 100, 500 | BE2.7 | Xuất danh sách / in hàng loạt / lịch sử |
| `DocumentStorage:RootPath`, `MaxFileSizeMb`, `AllowedExtensions`, `Categories` | `App_Data/DocumentFiles`, 20, pdf/jpg/jpeg/png/xlsx/docx, signature/photo/evidence/contract/other | BE2.7 | Tệp đính kèm ngoài wwwroot (biến môi trường `DocumentStorage__RootPath` để đổi chỗ) |
| `Licenses:GemBox` (bí mật – user-secrets / biến môi trường `Licenses__GemBox`) | – | BE2.7 | Mẫu in Word đủ trang (thiếu → bản dùng thử) |

Đã chạy: DB dev `AppDelicare` (2026-11-17, mục 1–4, 6–11 theo thứ tự trên, `-f 65001`) và staging `AppDelicare_Staging` (qua `Deploy-Database.ps1`). Tên tiếng Việt bị lỗi font từ lần chạy seed trước (thiếu `-f 65001`: tên ca, ghi chú đơn vị, tên công ty, mục "Duyệt – Đơn đặt suất ăn") đã sửa bằng `deploy/db/Repair-VietnameseText.ps1` (dev 44 giá trị, staging 9).
