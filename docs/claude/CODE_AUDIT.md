# CODE_AUDIT – Hiện trạng lõi .NET (rà ngày 2026-11-14)

Kết luận: **nâng cấp dần, không viết lại**. Nền móng (.NET 8, Angular 18, JWT, Serilog, ghi AuditChange tự động) dùng được. Có 4 lỗi nghiêm trọng phải sửa trước khi xây thêm: A1, A2, A3, A5.

## A. Lỗi và rủi ro (đánh số để task tham chiếu)

| Mã | Vị trí | Vấn đề | Mức | Task sửa |
|---|---|---|---|---|
| A1 | `ClinicManagement.Model/Entity/*`, `Data/Connection/DatabaseContext.cs` | **Code lệch với lược đồ 124 bảng.** (a) 13 entity sai tên bảng, không có `ToTable` / `[Table]`: `CleaningBatch`→`CleaningBatche`, `Dish`→`Dishe`, `DishCategory`→`DishCategorie`, `IngredientCategory`→`IngredientCategorie`, `InventoryPolicy`→`InventoryPolicie`, `PricePolicy`→`PricePolicie`, `ProductionBatch`→`ProductionBatche`, `ProofOfDelivery`→`ProofOfDeliverie`, `QcInspection`→`QCInspection`, `QcInspectionLine`→`QCInspectionLine`, `ContractMeals`→`ContractMeal`, `UnitOfMeasure`→(bảng đã đổi thành `UnitsOfMeasure_Legacy_20261113`; PO chốt dùng `UnitMeasure`). (b) 9 entity **không có bảng** trong 124 bảng: `AppConfig`, `Area`, `Branch`, `Department`, `Employees`, `Leads`, `Opportunity`, `Products`, `Region`. (c) **35 bảng chưa có code:** `AnalyticsRefreshLog, ApprovalDecision, ApprovalItem, ApprovalRequest, ApprovalStep, AttendanceEvent, BotConfiguration, CleaningChecklistResult, ComplaintHistory, CoreDataMergeConflict, CustomerComplaint, DeliveryEvent, DeliveryStop, FoodSafetyChecklist, FoodSafetyChecklistItem, FoodSafetyChecklistResult, InboundMessage, IntegrationChannel, IntegrationLog, KitchenWorkstation, KpiDailySnapshot, KpiMetricDefinition, KpiMonthlySnapshot, LeaveRequest, MealWasteRecord, MenuChangeRequest, OutboundMessage, PackagingLedger, ProductionLine, ShiftAssignment, ShiftSwapRequest, SupplierIssue, TemperatureLog, UserExternalIdentity, Vehicle` (cùng 2 bảng `_Legacy_20261113` chỉ để đọc). Cột của các entity hiện có cũng có thể lệch, cần so từng cột. | Rất cao | T05, T06 |
| A2 | `ClinicManagement.Data/Share/AuthenticatedService.cs` | `Id` trả về **người dùng đầu tiên trong bảng `User`** khi request không có claim. Job, bot hay agent ghi dữ liệu sẽ bị gán sai người. `UserInfo` ghép chuỗi SQL (`$"... WHERE u.Id = '{Id}'"`). | Rất cao | T02 |
| A3 | `WebApi/Infrastructure/ConfigureServices.cs` (khóa ký JWT), `WebApi/Startup.cs` (key ApplicationInsights QuickPulse, license GemBox) | **Bí mật viết cứng trong code.** Phải chuyển sang user-secrets / biến môi trường và **đổi khóa** (anh tự sinh khóa mới). `RequireExpirationTime = false`. | Cao | T01 |
| A4 | `WebApi/Controllers/BaseController.cs` + mọi controller | Chỉ có `[Authorize]`. Quyền `UserRole` (AllowAccess/Add/Edit/Delete/Print/Export/Import theo `ObjectMenu`) **không được kiểm tra ở API**. | Cao | T07 |
| A5 | `Services/Implement/InventoryTransactionService.cs` (và service sinh tự động cho các sổ khác) | Sổ kho cho phép Update/Delete như bảng thường, trái nguyên tắc sổ chỉ ghi thêm. | Cao | T08 |
| A6 | Tất cả service | Chỉ có CRUD sinh tự động, chưa có nghiệp vụ: chốt đơn, MRP, FEFO, duyệt, máy trạng thái, mã chứng từ. | Cao | T10–T22 |
| A7 | Controller | Lỗi nghiệp vụ trả **HTTP 200** kèm `ResponseCode=400` bên trong. Exception trả 500 kèm thông báo chung. Insert/Update dùng `[FromForm]`. | Trung bình | T03 |
| A8 | `Startup.cs` | Thứ tự middleware sai: `UseAuthentication` trước `UseRouting`, `CorsMiddleware` sau `UseEndpoints`. CORS `AllowAnyOrigin`. `ServiceLocator.SetLocatorProvider(services.BuildServiceProvider())` là anti-pattern. | Trung bình | T03 |
| A9 | `*.csproj` | Target `net8.0` nhưng gói EF Core, JwtBearer, OpenApi là **7.0.3**, `Microsoft.IdentityModel.Tokens` 6.27. Swashbuckle 6.4. | Trung bình | T04 |
| A10 | `Delete` trong service sinh tự động | Kiểm tra tham chiếu bằng SQL động qua `uspSystem_ExecuteQuery` và tải toàn bộ metadata FK mỗi lần xóa. Do DB không có FK vật lý nên kiểm tra này gần như vô hiệu. | Trung bình | T07 (thay bằng `ReferenceGuard` theo quan hệ logic) |
| A11 | `DatabaseContext` | Kế thừa `IdentityDbContext`. Các bảng `AspNet*` **không có** trong 124 bảng, cần xác minh không có code nào dùng Identity store. Ngoài ra dùng `DateTime.Now`. | Thấp | T05 |
| A12 | `ClinicManagement.Generator` | T4 + OrmLite chỉ chạy được trong Visual Studio và đọc schema từ DB live. Claude Code trong VS Code không chạy được. | Thấp | T05 (thay bằng `tools/AppDelicare.CodeGen` đọc file DDL) |
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
