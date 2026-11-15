# CODE_AUDIT – Hiện trạng lõi .NET (rà ngày 2026-11-14)

Kết luận: **nâng cấp dần, không viết lại**. Nền móng (.NET 8, Angular 18, JWT, Serilog, ghi AuditChange tự động) dùng được. Có 4 lỗi nghiêm trọng phải sửa trước khi xây thêm: A1, A2, A3, A5.

## A. Lỗi và rủi ro (đánh số để task tham chiếu)

| Mã | Vị trí | Vấn đề | Mức | Task sửa |
|---|---|---|---|---|
| A1 | `ClinicManagement.Model/Entity/*`, `Data/Connection/DatabaseContext.cs` | **Code lệch với lược đồ 124 bảng.** (a) 13 entity sai tên bảng, không có `ToTable` / `[Table]`: `CleaningBatch`→`CleaningBatche`, `Dish`→`Dishe`, `DishCategory`→`DishCategorie`, `IngredientCategory`→`IngredientCategorie`, `InventoryPolicy`→`InventoryPolicie`, `PricePolicy`→`PricePolicie`, `ProductionBatch`→`ProductionBatche`, `ProofOfDelivery`→`ProofOfDeliverie`, `QcInspection`→`QCInspection`, `QcInspectionLine`→`QCInspectionLine`, `ContractMeals`→`ContractMeal`, `UnitOfMeasure`→(bảng đã đổi thành `UnitsOfMeasure_Legacy_20261113`; PO chốt dùng `UnitMeasure`). (b) 9 entity **không có bảng** trong 124 bảng: `AppConfig`, `Area`, `Branch`, `Department`, `Employees`, `Leads`, `Opportunity`, `Products`, `Region`. (c) **35 bảng chưa có code:** `AnalyticsRefreshLog, ApprovalDecision, ApprovalItem, ApprovalRequest, ApprovalStep, AttendanceEvent, BotConfiguration, CleaningChecklistResult, ComplaintHistory, CoreDataMergeConflict, CustomerComplaint, DeliveryEvent, DeliveryStop, FoodSafetyChecklist, FoodSafetyChecklistItem, FoodSafetyChecklistResult, InboundMessage, IntegrationChannel, IntegrationLog, KitchenWorkstation, KpiDailySnapshot, KpiMetricDefinition, KpiMonthlySnapshot, LeaveRequest, MealWasteRecord, MenuChangeRequest, OutboundMessage, PackagingLedger, ProductionLine, ShiftAssignment, ShiftSwapRequest, SupplierIssue, TemperatureLog, UserExternalIdentity, Vehicle` (cùng 2 bảng `_Legacy_20261113` chỉ để đọc). Cột của các entity hiện có cũng có thể lệch, cần so từng cột. | Rất cao | T05, T06 |
| A2 | `ClinicManagement.Data/Share/AuthenticatedService.cs` | `Id` trả về **người dùng đầu tiên trong bảng `User`** khi request không có claim. Job, bot hay agent ghi dữ liệu sẽ bị gán sai người. `UserInfo` ghép chuỗi SQL (`$"... WHERE u.Id = '{Id}'"`). | Rất cao | T02 – **ĐÃ SỬA 2026-11-14** |
| A3 | `WebApi/Infrastructure/ConfigureServices.cs` (khóa ký JWT), `WebApi/Startup.cs` (key ApplicationInsights QuickPulse, license GemBox) | **Bí mật viết cứng trong code.** Phải chuyển sang user-secrets / biến môi trường và **đổi khóa** (anh tự sinh khóa mới). `RequireExpirationTime = false`. | Cao | T01 – **ĐÃ SỬA 2026-11-14** (còn: anh đổi khóa, xem SECRETS_TODO.md) |
| A4 | `WebApi/Controllers/BaseController.cs` + mọi controller | Chỉ có `[Authorize]`. Quyền `UserRole` (AllowAccess/Add/Edit/Delete/Print/Export/Import theo `ObjectMenu`) **không được kiểm tra ở API**. | Cao | T07 |
| A5 | `Services/Implement/InventoryTransactionService.cs` (và service sinh tự động cho các sổ khác) | Sổ kho cho phép Update/Delete như bảng thường, trái nguyên tắc sổ chỉ ghi thêm. | Cao | T08 |
| A6 | Tất cả service | Chỉ có CRUD sinh tự động, chưa có nghiệp vụ: chốt đơn, MRP, FEFO, duyệt, máy trạng thái, mã chứng từ. | Cao | T10–T22 |
| A7 | Controller | Lỗi nghiệp vụ trả **HTTP 200** kèm `ResponseCode=400` bên trong. Exception trả 500 kèm thông báo chung. Insert/Update dùng `[FromForm]`. | Trung bình | T03 |
| A8 | `Startup.cs` | Thứ tự middleware sai: `UseAuthentication` trước `UseRouting`, `CorsMiddleware` sau `UseEndpoints`. CORS `AllowAnyOrigin`. `ServiceLocator.SetLocatorProvider(services.BuildServiceProvider())` là anti-pattern. | Trung bình | T03 |
| A9 | `*.csproj` | Target `net8.0` nhưng gói EF Core, JwtBearer, OpenApi là **7.0.3**, `Microsoft.IdentityModel.Tokens` 6.27. Swashbuckle 6.4. | Trung bình | T04 – **ĐÃ SỬA 2026-11-14** |
| A10 | `Delete` trong service sinh tự động | Kiểm tra tham chiếu bằng SQL động qua `uspSystem_ExecuteQuery` và tải toàn bộ metadata FK mỗi lần xóa. Do DB không có FK vật lý nên kiểm tra này gần như vô hiệu. | Trung bình | T07 (thay bằng `ReferenceGuard` theo quan hệ logic) |
| A11 | `DatabaseContext` | Kế thừa `IdentityDbContext`. Các bảng `AspNet*` **không có** trong 124 bảng, cần xác minh không có code nào dùng Identity store. Ngoài ra dùng `DateTime.Now`. | Thấp | T05 |
| A12 | `ClinicManagement.Generator` | T4 + OrmLite chỉ chạy được trong Visual Studio và đọc schema từ DB live. Claude Code trong VS Code không chạy được. | Thấp | T05 (thay bằng `tools/AppDelicare.CodeGen` đọc file DDL) |
| A14 | `WebApi/Controllers/AuditChangeController.cs:60` → `AuditChangeService.DeleteData()` | **`GET api/AuditChange/DeleteData`**: bất kỳ user đã đăng nhập nào gọi một lệnh GET là **xóa vật lý** mọi bản ghi `IsDeleted = 1` ở **mọi bảng** (kể cả sổ append-only), qua SQL động `uspSystem_ExecuteQuery`. Không kiểm tra quyền, không ghi AuditChange. (T00 phát hiện 2026-11-14; cần xem thêm `LocationTrackingController` `DeleteData`.) | Rất cao | Đề xuất tắt ngay (D-21) |
| A15 | DB dev ↔ code | **74 SP `usp<Entity>_GetAll` code gọi nhưng DB không có** → các route `GetAll` tương ứng trả 500 (đã thử `api/Customer/GetAll`). Danh sách: `DB/sp/_MISSING_FROM_DB.md`. | Cao | T05 |
| A16 | DB dev: `uspSystem_BackupDB` (`DB/sp/uspSystem_BackupDB.sql`) | Tên "Backup" nhưng thực chất **RESTORE … WITH REPLACE** ghi đè DB `AppDelicare` từ một file .bak với đường dẫn máy viết cứng. Code không gọi. Không bao giờ chạy SP này. | Cao | D-21 |
| A17 | `WebApi/*.csproj` | Gói `AutoMapper 13.0.1` có 3 cảnh báo lỗ hổng mức cao (NU1903). | Trung bình | Giữ 13.0.1 – chờ D-22 (bản vá 15.1.4+ là license thương mại) |
| A13 | Angular `app-delicare-client` | Vẫn là template w3crm + màn hình phòng khám (patient, medical…). Chưa có màn hình nghiệp vụ DELICARE. `jwt.interceptor` chỉ xử lý 401/403. Service gọi `.../GetAll?...` theo route cũ. | Trung bình | DEFERRED(FE) – ngoài phạm vi hiện tại (D-20) |

## B. Điểm tốt cần giữ
- `DatabaseContext.SaveChangesAsync(typeSave)` tự điền Created/Modified/Deleted và ghi `AuditChange` cho mọi thay đổi. Đây là nền để truy vết.
- Mẫu Controller → Service → UnitOfWork thống nhất, dễ sinh mã.
- `DapperRepository` + stored procedure `uspXxx_GetAll` phân trang, sắp xếp phía server.
- Serilog ghi file theo ngày tại `wwwroot/Logs`.
- Angular có `generic-filter-popup`, `table-pager`, `modal.service`, `notify.service` dùng lại được.

## C. Ánh xạ tên (entity C# ↔ bảng SQL) – BẮT BUỘC dùng `[Table]`
| Entity (C#) | Bảng SQL |
|---|---|
| Dish | Dishe |
| DishCategory | DishCategorie |
| IngredientCategory | IngredientCategorie |
| InventoryPolicy | InventoryPolicie |
| PricePolicy | PricePolicie |
| ProductionBatch | ProductionBatche |
| CleaningBatch | CleaningBatche |
| ProofOfDelivery | ProofOfDeliverie |
| QcInspection | QCInspection |
| QcInspectionLine | QCInspectionLine |
| ContractMeal | ContractMeal |
| UnitMeasure | UnitMeasure (chuẩn). `UnitsOfMeasure_Legacy_20261113` chỉ để đọc / chuyển đổi |
| ActualCost | ActualCost. `ActualCosts_Legacy_20261113` chỉ để đọc |
