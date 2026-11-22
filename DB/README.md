# DB – Script SQL của DELICARE

| Thư mục | Nội dung | Ai chạy |
|---|---|---|
| `migrations/` | **Nguồn cấu trúc bảng DUY NHẤT** (2026-11-18), chạy tự động theo thứ tự tên file ở mọi môi trường – xem mục **Migration** bên dưới. `20261113_00_baseline_124_tables.sql` = DDL chuẩn 124 bảng của PO (DB trống); `20261113_restore_structure_db_appdelicare.sql` = script **chính thức của PO** nâng DB cũ 91 bảng (nguồn `D:\Duan\Delicare\SQLScript`). | Bộ chạy migration (Deploy-Database.ps1 / db-init.sh / docker compose) – thay đổi bảng mới cần anh duyệt |
| `sp/` | Stored procedure (`CREATE OR ALTER`), một file mỗi SP: `usp<Entity>_GetAll.sql`… Gồm SP lấy ra từ DB dev (T00) và SP mới do CodeGen sinh. | Claude Code chạy trên DB dev |
| `sp/_MISSING_FROM_DB.md` | Danh sách 74 SP mà code gọi nhưng DB dev chưa có (T00). | – |
| `_ssms_diagrams/` | 7 SP `sp_*diagram*` + `fn_diagramobjects` do SSMS tự tạo khi vẽ sơ đồ. Chỉ lưu tham khảo, **không phải** SP nghiệp vụ, không cần chạy. | – |
| `proposed/` | Script thay đổi lược đồ **đề xuất** (GAP – vd. index), KHÔNG nằm trong `scripts.order`, không tự chạy | Anh / DBA duyệt rồi chạy tay |
| `seed/` | Dữ liệu cấu hình (Shift, ObjectMenu, UserGroup, KpiMetricDefinition…). Dữ liệu demo gắn `[DEMO]`. | Claude Code chạy trên DB dev |

Không có script tạo bảng / cột ngoài `migrations/` đã được PO duyệt.

## Migration – cấu trúc bảng đồng bộ giữa các môi trường (2026-11-18)

Mọi máy (dev backend, dev FE, server test Docker, staging, production) dựng / nâng cấu trúc bảng **chỉ** từ `DB/migrations/*.sql`, chạy theo **thứ tự tên file**, mỗi file **1 lần** trên mỗi DB. Hai bộ chạy cùng quy tắc: `deploy/db/Migrations.ps1` (gọi từ `Deploy-Database.ps1`) và `deploy/db/migrations.sh` (gọi từ `deploy/docker/db-init.sh`, `deploy/db/migrate.sh` – Docker / Linux).

| Loại (dòng đầu file `-- delicare-migration: <loại>`) | Khi nào chạy |
|---|---|
| `baseline` – `20261113_00_baseline_124_tables.sql` | DB **chưa có bảng** → tạo 124 bảng. DB đã có bảng → chỉ **ghi nhận** (ADOPT), không chạy. |
| `legacy-upgrade` – `20261113_restore_structure_db_appdelicare.sql` | DB còn **thiếu bảng** của baseline (DB cũ 91 bảng) → chạy script nâng cấp của PO. Đủ rồi → ghi nhận. |
| `normal` (không khai) – migration về sau | Chạy **1 lần**. |

- **Đã chạy lưu ở đâu:** extended property của database (không thêm bảng theo dõi – luật DB): tên `DelicareMigration:<tên file>`, giá trị `RUN|ADOPT|<giờ UTC>|sha256:<16 ký tự>`. Xem: `SELECT name, value FROM sys.extended_properties WHERE class = 0 AND name LIKE 'DelicareMigration:%'`.
- **Không sửa file đã chạy.** Nội dung đổi (khác mã băm) → bộ chạy CẢNH BÁO, không chạy lại. Thay đổi mới = file mới. Baseline phải giữ **nguyên văn** `docs/reference/20261113_structure_db_appdelicare.sql` (test kiểm).
- **Sau khi chạy migration**, bộ chạy bash so cấu trúc với baseline (thiếu bảng / cột → dừng, trừ khi `DB_ALLOW_SCHEMA_DRIFT=1`).
- **Xem trước / chỉ đồng bộ bảng:** `./deploy/db/Deploy-Database.ps1 -Server … -Database … -ListMigrations` (không đổi gì) · `-MigrationsOnly` (không chạy SP / seed).

**Thêm thay đổi bảng / cột (backend, CHỈ khi anh đã duyệt – luật DB trong CLAUDE.md):**
1. Tạo `DB/migrations/YYYYMMDD_NN_<tên>.sql` (chữ thường, số, gạch dưới; ngày + số thứ tự trong ngày để đứng sau các file cũ), ví dụ `20261120_01_outbound_scheduled_at.sql`.
2. Script **chạy lại được**: `IF COL_LENGTH(N'dbo.X', N'Y') IS NULL ALTER TABLE …`, `IF OBJECT_ID(N'dbo.X', N'U') IS NULL CREATE TABLE …`, `IF NOT EXISTS (… sys.indexes …)`. Không `DROP TABLE / COLUMN`, `TRUNCATE`, `DELETE FROM` (test `MigrationFilesTests` chặn – cần anh duyệt riêng). Cú pháp SQL Server 2022 (D-61). Không `USE` (bộ chạy bỏ qua dòng `USE`).
3. Cập nhật model EF / `docs/reference` theo quy trình CodeGen nếu có, chạy `dotnet test`, ghi vào mục "Kiểm tra lược đồ" bên dưới.
4. Chạy trên DB dev: `./deploy/db/Deploy-Database.ps1 -Server "(localdb)\MSSQLLocalDB" -Database AppDelicare -MigrationsOnly`.

**SQL Server 2022 (D-61, T48.5):** mọi script ở đây (SP, seed, migrations) và SQL inline trong C# **chỉ dùng cú pháp có trong SQL Server 2022** (mức tương thích 160) – danh sách cấm ở CLAUDE.md mục 3.1 quy tắc 0 (kiểu `json` / `vector`, `REGEXP_*`, `JSON_OBJECTAGG` / `JSON_ARRAYAGG` / `JSON_CONTAINS`, `CURRENT_DATE`, `||`…). Test `SqlServer2022CompatibilityTests` quét tự động (bỏ qua comment và chuỗi dữ liệu). Mức tương thích DB do `deploy/db/compat-level.sql` giữ ở 160 khi deploy. Không chuyển DB bằng `.bak` giữa SQL Server 2025 và 2022 – chỉ bằng script.

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
| 7 | `seed/ObjectMenu_seed.sql` (sửa) | T13, T15.1 | + mục "Phê duyệt › Đơn đặt suất ăn" (`#/wf-inbox?type=crm-order`), "Phê duyệt › Đơn mua hàng (Giám đốc)" (`#/wf-inbox?type=scm-po-director`) |
| 8 | `seed/ObjectMenu_disable_legacy.sql` (mới) | T45R.8 (D-37) | Tắt 26 menu app cũ (không xóa) |
| 9 | `seed/UserRole_seed.sql` (sửa) | T13, T15.1 | Quyền duyệt 2 mục trên cho nhóm CEO |
| 10 | `seed/Shift_seed.sql` (không đổi) | T21.1 | 4 ca `SANG_6H` / `TRUA` / `TOI` / `DEM_23H` (Pilot chạy 4 ca – T17.1) |
| 11 | `seed/FoodSafetyChecklist_seed.sql` (mới) | T17 | Bộ `PRD_BATCH`: `SAMPLE_RETAINED` (lưu mẫu – bắt buộc trước khi giao, D-42), `CORE_TEMP` |
| 12 | `seed/ProductionStage_seed.sql` (mới) | BE2.4 | Bộ `PRD_STAGE` "Công đoạn sản xuất theo tổ": `SO_CHE` → `NAU` → `CHIA_SUAT` (PROPOSED – D-53 e). Chỉ thêm khi chưa có (theo Code / ItemCode), không ghi đè |
| 13 | `seed/FoodSafetyQa_seed.sql` (mới) | BE2.5 | Bộ checklist `QA_FG` (thành phẩm), `PREP`, `PREP_AREA_CLEAN`, `BLAST_CHILL`, `KITCHEN_CLEAN` (5S), `VS-KHAY` (vệ sinh khay 6 bước) – PROPOSED D-54. Chỉ thêm khi chưa có (theo Code / ItemCode), không ghi đè |
| 14 | `seed/SystemUser_seed.sql` (mới) | T23 | Tài khoản dịch vụ `system` (Id cố định `061877ad-396c-4386-8868-25d4940bf467` = `SystemUsers:JobUserId`) cho job tự động – không đăng nhập được (mật khẩu không phải hash hợp lệ), không cấp nhóm quyền. Chỉ thêm khi chưa có |
| 15 | `seed/KpiMetricDefinition_seed.sql` (mới) | BE3.4 | 11 chỉ tiêu KPI phiên bản 1 theo spec 11.7 + BUSINESS_RULES §12 (`PROD_ACHIEVEMENT`, `WASTE_RATE`, `QC_REJECT_RATE`, `QC_CORE_TEMP_PASS`, `QC_SAMPLE_RETAINED`, `DEL_ONTIME`, `INV_ACCURACY`, `FIN_COST_MEAL`, `FIN_FOOD_COST`, `PKG_LOSS_RATE`, `CS_COMPLAINT_SLA`); mục tiêu = thẻ `[TARGET >= x]` cuối `FormulaDescription` (GAP-KPI-01). Công thức + mục tiêu PROPOSED – D-57 b, w–ab. Chỉ thêm mã chưa có (mọi phiên bản, kể cả đã xóa mềm), không ghi đè – đổi số qua màn `#/kpi-definition` (tạo phiên bản mới) |

T11, T12, T13 (ngoài seed menu), T14, T15, T15.1, T17, T17.1, T19, T50R1: **không có SP / seed mới** (đọc / ghi qua EF + SP `usp*_GetAll` sẵn có).

**BE2.1 (2026-11-17): không có SP / seed mới** – không cần thêm gì vào `deploy/db/scripts.order` (T48.3). Mục menu của mọi màn hình BE2.1 (`log-packing-order`, `log-loading`, `log-execution`, `log-delivery-order`, `qc-temperature`, `prod-waste`, `scm-lot`, `wf-inbox`…) đã có trong `seed/ObjectMenu_seed.sql`. Thay đổi cấu hình (appsettings, không phải DB): `BusinessRules:DeliveryRequireLoading` (true), `ReferenceLists:WasteType` thêm `PACKING_DAMAGE`, `ReferenceLists:TemperatureMeasurementType` (mới) – môi trường có appsettings riêng (staging / Docker) cần bổ sung 3 khóa này.

**BE2.2 (2026-11-17): không có SP / seed mới** – không thêm dòng nào vào `deploy/db/scripts.order`. Mục duyệt `#/wf-inbox?type=crm-contract`, `menu-change-request`, `crm-order` đã có trong `seed/ObjectMenu_seed.sql` / `UserRole_seed.sql` (nhóm CEO; COO thêm đổi món). Cấu hình mới (appsettings): `BusinessRules:ForecastVariancePctWarning` (10), `BusinessRules:ForecastMaxDays` (62) – có giá trị mặc định trong code.

**BE2.3 (2026-11-17): không có SP / file seed mới** – chỉ **sinh lại** 2 seed đã có trong `deploy/db/scripts.order` (`seed/ObjectMenu_seed.sql`, `seed/UserRole_seed.sql`) bằng `dotnet run --project tools/AppDelicare.CodeGen -- --layers security` (`Seeds.ExtraApprovals`): thêm 3 mục duyệt `#/wf-inbox?type=scm-pr-l1` (OPS-SCM), `scm-pr-l2` (COO), `scm-stockcount-l2` (FIN-ACC) + quyền AllowAccess / AllowEdit (insert-only, idempotent). **Đã chạy 2 seed trên DB dev `AppDelicare`** (`sqlcmd -b -I -f 65001`). Server Docker của dev FE: `db-init` tự chạy lại 2 seed. Cấu hình mới (appsettings, có mặc định trong code): `BusinessRules:ApprovalSteps`, `ApprovalStepDueHours` (48), `ApprovalStepDueHoursByType`, `ApprovalBlockOverdueDecision` (false), `ApprovalRequireDistinctApprovers` (true), `PurchaseOrderDirectorTwoLevel` (false), `StockAdjustmentRequireApproval` (true).

**BE2.5 (2026-11-17): 1 seed mới `seed/FoodSafetyQa_seed.sql`** (mục 13) – đã thêm vào `deploy/db/scripts.order` ngay sau `ProductionStage_seed.sql`; **đã chạy trên DB dev 2 lần** (`sqlcmd -b -I -f 65001`, lần 2 không thêm dòng). Không SP mới, không bảng / cột.

**BE2.4 (2026-11-17): 1 seed mới `seed/ProductionStage_seed.sql`** (mục 12) – đã thêm vào `deploy/db/scripts.order` ngay sau `FoodSafetyChecklist_seed.sql`; **đã chạy trên DB dev 2 lần** (`sqlcmd -f 65001`, lần 2 không thêm dòng). Không SP mới (đọc / ghi qua EF + `usp*_GetAll` sẵn có), không bảng / cột.

**BE2.6 (2026-11-17): không có SP / seed mới** – mục menu / quyền `#/hr-shift-assign`, `#/hr-attendance`, `#/hr-leave`, `#/hr-swap`, `#/wf-inbox?type=hr-leave / hr-swap` đã có trong seed T07.

**BE2.7 (2026-11-17): không có SP / seed mới** – tệp đính kèm lưu ngoài DB (`DocumentStorage:RootPath`, xem `deploy/README.md`); DB chỉ giữ khóa lưu trữ trong `DocumentFile.FileNameHas` (GAP-DOC-01).

**BE2.9 (2026-11-17) – kiểm tra cuối đợt BE-2:** `deploy/db/scripts.order` (140 dòng: 131 SP + `00_Admin_seed.sql` + 8 seed) **khớp đúng** với `DB/sp/*.sql` + `DB/seed/*.sql` (so bằng `diff`, không thiếu / không thừa); không SP / seed mới trong BE2.9. Đã chạy `Deploy-Database.ps1` trên staging `AppDelicare_Staging` (0 lỗi, `Repair-VietnameseText.ps1` chạy thử: 0 giá trị lỗi font).

**T48.5 (2026-11-17): không bảng / cột / SP / seed mới** – thêm `deploy/db/compat-level.sql` (đặt mức tương thích về 160 nếu DB đang > 160), `Deploy-Database.ps1` và `db-init.sh` tự gọi, không cần chạy tay.

**REVIEW-BE-1 (2026-11-17): sửa 7 SP, không bảng / cột / seed mới** – `uspAuditChange_GetAll`, `uspUser_GetAll`, `uspUserClaim_GetAll`, `uspUserGroup_GetAll`, `uspPerson_GetAll`, `uspObjectMenu_GetAll`, `uspLocationTracking_GetAll`: chặn `Page` ≥ 1, `PageSize` 1–1000. Cả 7 đã có trong `scripts.order` (không thêm dòng). **Đã chạy trên DB dev và staging** (`Deploy-Database.ps1`).

**BOT-1.1 (2026-11-18, D-62): 1 seed mới `seed/Integration_Telegram_seed.sql`** – thêm vào cuối nhóm seed của `deploy/db/scripts.order`: kênh + cấu hình bot Telegram (`IntegrationChannel` `TELEGRAM_VH` / `TELEGRAM_DH` / `TELEGRAM_TRO_LY`, `BotConfiguration` VH / DH / TRO_LY – **không chứa token**, `SecretReference` = tên biến môi trường), menu quyền `#/bot-van-hanh`, `#/bot-dieu-hanh` + UserRole PROPOSED. Chỉ thêm, chạy lại không đổi. **Đã chạy DB dev 2 lần.** Không SP mới, không bảng / cột. `proposed/GAP_INT_02_idempotency_index.sql` = đề xuất index (GAP-INT-02) – **không tự chạy**, chờ anh duyệt.

### Kiểm tra lược đồ (2026-11-17)

Toàn bộ lịch sử git: lệnh tạo bảng / thêm cột **chỉ có** trong `migrations/20261113_restore_structure_db_appdelicare.sql` (PO, T00). Mọi task sau đó (T01 … BE-3, T48.x, REVIEW-BE-1) **không** tạo bảng, thêm cột, FK hay index. DB dev và staging: **124 bảng / 2264 cột, khớp từng cột** với `docs/reference/20261113_structure_db_appdelicare.sql` (bảng thứ 125 `sysdiagrams` do SSMS tự tạo, không thuộc ứng dụng).

**2026-11-18 – chuyển sang migration:** DB dev `AppDelicare` và staging `AppDelicare_Staging` đã **ghi nhận** (ADOPT) 2 migration, không đổi bảng (vẫn 124 bảng / 2264 cột). DB tạo mới từ baseline khớp từng cột (tên, kiểu, độ dài, null) với DB dev.

→ **Dev FE / server test sau `git pull`:** chạy lệnh cập nhật DB như cũ – lần đầu sau bản này bộ chạy chỉ ghi nhận 2 migration (không đổi bảng); về sau migration mới (nếu có) tự chạy đúng 1 lần:
- Docker: `docker compose -f deploy/docker/docker-compose.staging.yml --env-file deploy/docker/staging.env run --rm db-init`, rồi `... up -d --build api` (hoặc `bash deploy/docker/update-db.sh`) – xem `deploy/docker/README.md` mục 2.
- SQL Server thường: `./deploy/db/Deploy-Database.ps1 -Server "<server>" -Database <db>` rồi publish lại API (`deploy/README.md` mục 9).

Cả hai chạy lại bao nhiêu lần cũng được: migration đã chạy thì bỏ qua, SP `CREATE OR ALTER`, seed chỉ thêm phần thiếu. Compose gốc (`docker-compose.yml`, dịch vụ `db-sync`): DB đã có bảng mà có migration **mới** chờ → dừng và in danh sách, trừ khi `.env` đặt `DB_AUTO_MIGRATE=1` (backup trước khi chạy).

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

Đã chạy: DB dev `AppDelicare` (2026-11-17, mục 1–4, 6–11 theo thứ tự trên, `-f 65001`) và staging `AppDelicare_Staging` (qua `Deploy-Database.ps1`). Tên tiếng Việt bị lỗi font từ lần chạy seed trước (thiếu `-f 65001`: tên ca, ghi chú đơn vị, tên công ty, mục "Phê duyệt › Đơn đặt suất ăn") đã sửa bằng `deploy/db/Repair-VietnameseText.ps1` (dev 44 giá trị, staging 9).
