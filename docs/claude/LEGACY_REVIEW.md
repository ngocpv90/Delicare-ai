# LEGACY_REVIEW – Code cũ đã có gì, còn thiếu gì (T45R, cập nhật mỗi task)

Khung chuẩn = code cũ (D-30). File này là bước "rà soát trước, bổ sung sau" (CLAUDE.md mục 4.1). Mỗi task sau T45R cập nhật dòng của màn hình mình làm.

Ký hiệu: **Có** = code cũ đã đủ · **Thiếu** = T45R bổ sung (ghi file / method) · **Lỗi** = code cũ sai, T45R sửa.
Route cũ: `api/<Controller>/<Action>`. Mọi controller trong bảng (a) đều có sẵn `GetAll / Insert / Update / Delete / GetById / GetAllData` + SP `usp<Entity>_GetAll`, trừ chỗ ghi khác.

**Trạng thái sau T45R (2026-11-15): mọi mục "Thiếu / Lỗi" của bảng (a) đã bổ sung đúng file / method ghi trong cột cuối, trừ `adm-document`, `adm-reference` (để T45B.5 / T49). Mọi dòng bảng (b) đã chuyển; lớp v1 nằm ở `_obsolete/v1/`.** Kiểm chứng: test `Admin/AdminServiceTests`, `MasterData/MasterDataServiceTests`, `Menu/RecipeServiceTests`, `Menu/RecipeSqlServerTests`, `Security/PermissionTests`, `Ledger/AppendOnlyAndLedgerTests`.

Thêm khi làm: DTO cũ thiếu cột của DDL 124 bảng (CODE_AUDIT A23) → `tools/AppDelicare.CodeGen --merge` bổ sung vào 144 file Request / ViewModel; SP `GetAll` của bảng có hiệu lực theo ngày nhận thêm `@EffectiveOn` (10 SP).

## (a) Màn hình P0–P2 → code cũ

| Màn hình | Bảng → Controller / Service cũ | Đánh giá | Bổ sung / sửa trong T45R |
|---|---|---|---|
| adm-company | Company | Có | – |
| adm-orgunit | OrganizationUnit | Thiếu | `OrganizationUnitService.GetTree(companyId)` (cây kèm số nhân viên, số kho, tên quản lý), `GetBranches()` (UnitType = BRANCH); chặn vòng lặp cha – con trong `Update` |
| adm-employee | Employee | Có | – |
| adm-person | Person (+ GetProfile / UpdateProfile / GetAllDataByKey) | Có | Thêm Person tự tạo User (`UserService.InsertUserAfter`) – đây là luồng "tạo user kèm hồ sơ" |
| adm-user | User (+ ResetPassword / SaveIsActive / SaveIsAdmin / ChangePassword) | Thiếu / Lỗi | `SaveIsActive`: chặn tự khóa chính mình. `ResetPassword` chỉ admin (spec 3.2). `User/GetCurrent` (user + nhóm + menu theo quyền). Xóa cache quyền sau Save / Delete / SaveIsActive / SaveIsAdmin. Ghi chú: mật khẩu tạo mới / reset = tên đăng nhập (MD5) – giữ hành vi cũ, xử lý cùng D-29 |
| adm-permission | UserRole (`GetByUserGroupId`, `Update`) | Thiếu / Lỗi | `GetMatrix(userGroupId)` (mọi menu đang hoạt động, kể cả menu chưa có dòng UserRole); `Update` nhận thêm dòng chưa có Id (tạo mới), chuẩn hóa cờ (bật cờ bất kỳ → bật AllowAccess; tắt AllowAccess → tắt hết); `SaveAccessAll` (cấp / thu hồi mọi menu cấp 2); `GetPreview` (chỉ admin); `CopyFromUserGroup`; `GetByUserId` (quyền hiệu lực của 1 user, OR các nhóm). Xóa cache quyền sau mọi thay đổi |
| adm-usergroup | UserGroup | Lỗi | **Tạo nhóm mới đang cấp TOÀN QUYỀN mọi menu** (`UserRoleService.Insert` đặt mọi cờ = true) → đổi thành tạo dòng với mọi cờ = false, admin cấp quyền sau (PROPOSED – BA/PO CONFIRM). Xóa cache quyền khi xóa nhóm |
| adm-objectmenu | ObjectMenu (+ SaveIsActive) | Thiếu | `GetTree(includeInactive)`, `UpdateOrder` (đổi cha / thứ tự, chặn vòng lặp); MetaLink duy nhất trong Insert / Update; xóa cache quyền sau Save / SaveIsActive |
| adm-position | Position | Có | – |
| adm-admin-unit | AdministrativeProvince / District / Ward / Unit / Region | Có | – |
| adm-document | DocumentFile (chỉ `Delete`) | Thiếu (ngoài T45R) | Danh sách / tải lên theo R1 sau (T49 / T46B) |
| adm-audit | AuditChange (`GetAll` lọc EntityName + IdChange, GetAllEntityName, GetAllAction) | Có | Lịch sử 1 bản ghi = `AuditChange/GetAll?EntityName=&IdChange=`. `DeleteData` chờ D-26 (đang AdminOnly) |
| adm-access-log | LocationTracking | Có | `DeleteData` chờ D-26 (đang AdminOnly) |
| adm-merge-conflict | CoreDataMergeConflict (Id bigint – CodeGen không sinh) | Thiếu | **Tạo mới theo mẫu**: `CoreDataMergeConflictController` / `Service`: `GetAll` (SP mới `uspCoreDataMergeConflict_GetAll`, lọc đã / chưa xử lý), `GetById(long)`, `Resolve` (ResolvedAt, ResolutionNote) |
| adm-reference | – (STATUS_CATALOG, loại suất…) | Thiếu | T45B.5 |
| crm-customer | Customer | Thiếu | `CustomerService.GetOverview(id)` (liên hệ, điểm giao, hợp đồng + suất, 15 đơn gần nhất, KPI); CIF không trùng trong Insert / Update |
| crm-contact | CustomerContact | Có | – |
| crm-location | CustomerDeliveryLocation | Thiếu | Insert / Update: giờ giao từ < đến, vĩ độ / kinh độ hợp lệ, chặn chuyển Inactive khi còn đơn giao chưa xong |
| scm-supplier | Supplier | Thiếu | `SupplierService.GetOverview(id)` (bảng giá, PO đang mở, sự cố, tỷ lệ đúng hạn / trả hàng 90 ngày) |
| scm-ingredient | Ingredient | Thiếu | Insert / Update: YieldPercent ∈ (0,100], StandardWastePercent ∈ [0,100), ShelfLifeDays > 0 khi quản lý hạn. `GetStock(id)` (tồn khả dụng theo kho, dưới mức tối thiểu) |
| scm-ingredient-cat | IngredientCategory | Có | – |
| scm-supplier-material | SupplierMaterial | Thiếu | Lọc `EffectiveOn` trong SP GetAll; bật IsPreferred → bỏ ưu tiên các NCC khác cùng nguyên liệu |
| scm-uom | UnitMeasure | Có | – |
| scm-uom-conversion | UomConversion | Thiếu | `UomConversionService.ConvertAsync` / action `UomConversion/Convert` (xuôi, ngược, qua đơn vị trung gian, chỉ quy đổi đang hiệu lực); Insert / Update: hệ số > 0, 2 đơn vị khác nhau, không trùng cặp |
| scm-warehouse | Warehouse | Thiếu | `WarehouseService.GetLayout(id)` (khu → vị trí) |
| scm-zone / scm-location | StorageZone / StorageLocation | Có | – |
| scm-inv-policy | InventoryPolicy | Thiếu | Insert / Update: Min ≤ Safety ≤ Reorder ≤ Max; 1 dòng cho (nguyên liệu, kho) |
| hr-shift | Shift | Thiếu | Insert / Update: giờ bắt đầu ≠ kết thúc, công ty tồn tại, mã không trùng; Delete kiểm tham chiếu (đã có mẫu) |
| log-vehicle | Vehicle | Có | – |
| menu-dish-cat | DishCategory | Có | – |
| menu-dish | Dish | Thiếu | `DishService.GetActiveRecipe(id)` (công thức hiệu lực + giá vốn NVL / suất) |
| menu-recipe | Recipe | Thiếu / Lỗi | `RecipeService.GetByDishId`, `CreateVersion`, `Submit`, `Approve`, `Reject`, `Lock`, `CalculateCost`. Sửa: Insert luôn tạo bản Draft (VersionNo tự tăng), Update / Delete chỉ khi Draft, không đổi Status qua Update |
| menu-recipe-line | RecipeLine | Lỗi | Insert / Update / Delete chỉ khi công thức Draft; nguyên liệu / đơn vị phải tồn tại; Quantity > 0; ghi song song UomId |
| cost-standard | StandardCost | Thiếu | `StandardCostService.Recalculate(dishIds)`; Insert / Update tay vẫn giữ (PROPOSED: chỉ đọc khi UAT-1 xác nhận) |
| crm-price-policy | PricePolicy | Thiếu | `PricePolicyService.Resolve` (hợp đồng → khách → ContractMeal); Insert / Update: đơn giá > 0, không chồng hiệu lực |
| qc-checklist | FoodSafetyChecklist | Thiếu | Insert / Update: (Code, VersionNo) không trùng |
| qc-checklist-item / prod-line / prod-workstation | FoodSafetyChecklistItem / ProductionLine / KitchenWorkstation | Có | – |
| dbx-explorer / dbx-gap | – | Ngoài T45R | Màn tài liệu, không cần API riêng ở R1 |

Hạ tầng cũ phát hiện khi rà:
- **Lỗi – `UnitOfWork.OpenTransaction`** mở transaction trên kết nối Dapper riêng, nên các lần ghi EF (`CommitAsync`) **không nằm trong transaction** (mỗi lần lưu commit ngay; rollback không có tác dụng). T45R sửa: transaction mở trên chính kết nối của DbContext (Dapper dùng chung), service lồng nhau dùng chung transaction ngoài cùng; provider InMemory (test) bỏ qua transaction. Ghi CODE_AUDIT A21.

## (b) Lớp v1 (T03, T10, T45B.1–.4) → chuyển sang

| Endpoint / thành phần v1 | Nghiệp vụ | Chuyển sang (khung cũ) |
|---|---|---|
| `/api/v1/{module}/{resource}` 39 resource (`CrudResources.Generated.cs`, `CrudService`, `ResourceRegistry`, `TableMetadata.Generated.cs`) | Danh sách / chi tiết / thêm / sửa / xóa mềm | `GetAll / GetById / Insert / Update / Delete / GetAllData` của 39 controller cũ (bảng (a)). Luật riêng của registry chuyển vào `Insert / Update` của service tương ứng (bảng (a)) |
| `GET /meta/resources`, `GET /meta/schema/{m}/{r}` | Mô tả resource cho frontend | Bỏ. Thay bằng `docs/frontend-handoff/P0-P2.md` (T45B.5) |
| `GET /meta/lookups/{table}` | Dropdown | `<Entity>/GetAllData` |
| `POST /auth/login` | Đăng nhập | `Authentication/Login` (đã có, `LoginService`) |
| `POST /auth/change-password` | Đổi mật khẩu | `User/ChangePassword` (đã có) |
| `GET /meta/menu` | Menu theo quyền | `Authentication/Login` trả `listMenu`; gọi lại: `User/GetCurrent` |
| `GET /meta/me` | User + nhóm + đơn vị + quyền | `User/GetCurrent` + `OrganizationUnit/GetBranches` |
| `GET/POST/PUT /admin/users…` (list, get, create, update) | Người dùng | `User/GetAll, GetById, Insert, Update` (đã có; `ListUserGroupId` gán nhóm) |
| `PUT /admin/users/{id}/groups` | Gán nhóm | `User/Update` (`ListUserGroupId` → `UserClaimService.SaveAfterUser`) |
| `POST /admin/users/{id}/lock`, `/unlock` | Khóa / mở | `User/SaveIsActive` (+ chặn tự khóa) |
| `POST /admin/users/{id}/reset-password` | Đặt lại mật khẩu | `User/ResetPassword` (chỉ admin) |
| `GET /admin/users/{id}/effective-permissions` | Quyền hiệu lực | `UserRole/GetByUserId` |
| `GET/PUT /admin/user-groups/{id}/permissions` | Ma trận quyền | `UserRole/GetMatrix`, `UserRole/Update` |
| `POST /admin/user-groups/{id}/permissions/bulk` | Cấp / thu hồi hết | `UserRole/SaveAccessAll` |
| `GET /admin/user-groups/{id}/preview` | Xem trước | `UserRole/GetPreview` (chỉ admin) |
| `GET /admin/object-menus/tree`, `PUT …/reorder` | Cây menu, sắp xếp | `ObjectMenu/GetTree`, `ObjectMenu/UpdateOrder` |
| `GET /admin/org-units/tree`, `GET /admin/branches` | Cây đơn vị, chi nhánh | `OrganizationUnit/GetTree`, `OrganizationUnit/GetBranches` |
| `GET /admin/merge-conflicts`, `POST …/{id}/resolve` | Xung đột dữ liệu | `CoreDataMergeConflict/GetAll`, `CoreDataMergeConflict/Resolve` |
| `GET /audit/{table}/{id}/history` | Lịch sử bản ghi | `AuditChange/GetAll?EntityName=&IdChange=` |
| `/api/v1/hr/shifts` (5 action) | Ca | `Shift/*` + luật ở `ShiftService` |
| `POST /scm/inventory-transactions/{id}/reverse` | Đảo bút toán | `InventoryTransaction/Reverse` → `InventoryLedgerService.ReverseAsync` (đã có) |
| `GET /sales/customers/{id}/overview` | Khách 360° | `Customer/GetOverview` |
| `GET /scm/suppliers/{id}/overview` | NCC tổng quan | `Supplier/GetOverview` |
| `GET /scm/warehouses/{id}/layout` | Sơ đồ kho | `Warehouse/GetLayout` |
| `GET /scm/uom-conversions/convert` | Quy đổi | `UomConversion/Convert` |
| Danh sách khách / nguyên liệu kèm trường tính toán (Enrich) | activeContracts, locationCount, tồn khả dụng… | `Customer/GetOverview`, `Ingredient/GetStock` (danh sách giữ SP cũ) |
| Lọc `effectiveOn` giá NCC | | Tham số `EffectiveOn` của `SupplierMaterial/GetAll` (SP) |
| `GET/POST /menu/dishes/{dishId}/recipes` | Phiên bản công thức | `Recipe/GetByDishId`, `Recipe/CreateVersion` |
| `POST /menu/recipes/{id}/lines`, `PUT/DELETE /menu/recipe-lines/{id}` | Dòng định lượng | `RecipeLine/Insert, Update, Delete` (+ chỉ khi Draft) |
| `POST /menu/recipes/{id}/submit · approve · reject · lock` | Duyệt, khóa | `Recipe/Submit, Approve, Reject, Lock` |
| `GET /menu/recipes/{id}/cost` | Giá vốn | `Recipe/CalculateCost` |
| `POST /production/standard-costs/recalculate` | Tính lại giá chuẩn | `StandardCost/Recalculate` |
| `GET /sales/price-policies/resolve` | Tra đơn giá | `PricePolicy/Resolve` |
| `IClock`, `BusinessRulesOptions` (Application) | Giờ VN, ngưỡng | `AppDelicare.Data/Share/Clock.cs`, `AppDelicare.Model/Option/BusinessRulesOptions.cs` |
| `IUomService`, `IRecipeCostCalculator` | Quy đổi, giá vốn | `UomConversionService.ConvertAsync`, `RecipeService.CalculateCostAsync` |
| `PasswordHasher` | Băm mật khẩu | `EncriptFunctionsHelper` (đã có, D-29) |
| `IDispatcher`, Command / Handler / Validator, domain event `PermissionsChanged` | | Bỏ. Service gọi `IPermissionService.Invalidate*` trực tiếp |
| `Permissions:ApproverGroups` (appsettings), `ScreenMap.MatrixGroup` cho duyệt | Quyền duyệt | Mục ObjectMenu "Duyệt …" (MetaLink `#/wf-inbox?type=<screen-id>`) dưới nhóm "Phê duyệt" + UserRole (`AllowEdit`) – D-31 |
| `Api/*` (ApiControllerBase, ApiEnvelope, EnvelopeResultFilter, ExceptionToErrorMiddleware, IdempotencyFilter, BranchContext / BranchHeaderMiddleware, PageQuery, ApiJson, ApiException) | Quy ước spec 1.2 | Bỏ → `_obsolete/v1/` |
| `PermissionService`, `IPermissionService` (WebApi/Security) | Kiểm quyền | `AppDelicare.Services` (giữ logic, bỏ ApproverGroups). `PermissionFilter`, `PermissionConvention` (bỏ nhánh CRUD v1), `ScreenMap` giữ ở WebApi |

## (c) Pilot P3 – thực đơn tuần, phân công thực đơn, đơn hàng (T13 + T46B.1, 2026-11-16)

Rà trước khi viết: cả 7 bảng đã có Controller / Service / Repository / Request / ViewModel / SP `usp<Entity>_GetAll` sinh mã (CRUD chuẩn), **chưa có nghiệp vụ nào**. Không có service / controller thứ hai được tạo; mọi bổ sung nằm trong file cũ.

| Màn hình | Bảng → Controller / Service cũ | Đánh giá trước T13 | Bổ sung / sửa trong T13 |
|---|---|---|---|
| menu-menu | Menu | Thiếu / Lỗi: CRUD nhận `Status` tùy ý (đổi trạng thái "chui"), không lịch tuần, không duyệt / công bố | `MenuService.GetCalendar` (pivot thứ × loại suất / lựa chọn / nhóm món + giá vốn công thức khóa + FC%), `Approve`, `Publish` (đủ món mỗi thứ × loại suất đã gán), `Expire`, `Clone`, `ExportForCustomer` (T46B.1, ClosedXML). Insert luôn Draft / VersionNo 1; Update chỉ Draft / Approved, giữ Status; Delete chỉ Draft |
| menu-menu-line | MenuLine | Lỗi: thêm / sửa không kiểm gì | `MenuLineService` Insert / Update / Delete: thực đơn Draft / Approved, DayOfWeek 1–7, SequenceNo (lựa chọn) 1–9, MealType bắt buộc, món tồn tại; món chưa có công thức khóa → cảnh báo trong `responseMessage` (không chặn) |
| menu-assignment | MenuAssignment | Thiếu: không kiểm chồng hiệu lực, không tra thực đơn áp dụng | `MenuAssignmentService.CheckRules` (hiệu lực, hợp đồng thuộc khách + có MealType, điểm giao thuộc khách, không chồng hiệu lực Active cùng khách / điểm / loại suất); `Resolve` / `ResolveAsync` (điểm giao trước, cấp khách sau; chỉ thực đơn Approved / Published) |
| crm-contract / crm-contract-meal | Contract / ContractMeal | Có (CRUD) | Không sửa trong Pilot (Submit / Terminate hợp đồng, ContractExpiryJob → sau Pilot). Đơn hàng chỉ đọc hợp đồng `Status = Active` hiệu lực tại ngày ăn |
| crm-order | Order | Thiếu / Lỗi: CRUD Insert / Update đổi được Status, số, tổng tiền; không máy trạng thái | `OrderService.GetNewTemplate`, `GetDetail`, `Create` (số PH- qua `IDocumentNumberService`, giá qua `PricePolicyService.Resolve`, thực đơn qua `MenuAssignmentService.ResolveAsync`, ghi song song OrderNo / OrderNumber, Amount / LineAmount), `UpdateLines` (lệch > 30% → ApprovalRequest ORDER_DEVIATION), `Submit`, `Confirm`, `Lock`, `LockByMealDate`, `Cancel`; giờ chốt 20h D-1 qua `IClock` + `BusinessRulesOptions`. CRUD: Insert → chuyển sang `Create`; Update chỉ Note / DeliveryDate của đơn Draft; Delete chỉ Draft (xóa kèm dòng) |
| crm-order-line | OrderLine | Lỗi: CRUD ghi dòng không tính lại tiền / tổng, bỏ qua giờ chốt | `OrderLineService` Insert / Update / Delete → lỗi nghiệp vụ, chỉ ghi qua `Order/Create`, `Order/UpdateLines` (PROPOSED) |
| wf-inbox (crm-order) | ApprovalRequest | Thiếu: engine T12 chỉ lưu nội dung, không lưu đề xuất | `IApprovalHandler.ApproveAsync(…, proposalJson)` (mặc định gọi bản cũ), `ApprovalRequestService.CreateAsync(…, proposalJson)` – PayloadJson `{"state":"…","proposal":"…"}`, hash chỉ trên state; không có đề xuất thì giữ định dạng T12. Handler mới `OrderDeviationApprovalHandler`. Seed mục menu `#/wf-inbox?type=crm-order` (CodeGen `Seeds.ExtraApprovable`) |

## (d) Pilot P4 – sổ kho, lô, xuất kho FEFO, kiểm kê (T16, 2026-11-16)

Rà trước khi viết: 9 bảng (InventoryTransaction, InventoryBalance, MaterialLot, MaterialIssue / Line, StockCount / Line, MaterialReservation, InventoryPolicie) + Warehouse / StorageLocation đều có Controller / Service / Repository / Request / ViewModel / SP `usp<Entity>_GetAll` sinh mã (CRUD chuẩn). Nghiệp vụ có sẵn duy nhất: `InventoryLedgerService` (T08: `PostAsync`, `ReverseAsync`, cập nhật InventoryBalance, chặn tồn âm, ghi song song cột cũ) và `InventoryTransaction/Reverse` (T45R). `DatabaseContext` đã chặn Update / Delete `InventoryTransaction` (append-only). Không tạo service / controller thứ hai; mọi bổ sung nằm trong file cũ (+ lớp tĩnh `InventoryLotRules` dùng chung cho quy tắc lô).

| Màn hình | Bảng → Controller / Service cũ | Đánh giá trước T16 | Bổ sung / sửa trong T16 |
|---|---|---|---|
| scm-inv-txn | InventoryTransaction | Lỗi: `Insert` CRUD ghi thẳng vào sổ (không cập nhật tồn, không kiểm lô / đơn vị); `GetAll` chỉ lọc đúng 1 ngày | CRUD Insert / Update / Delete → lỗi nghiệp vụ. Mới: `GetLedger` (SP `uspInventoryTransaction_GetLedger`, lọc khoảng ngày / loại / chứng từ nguồn / lô), `Adjust` (ADJUSTMENT có lý do, số PH-), `Transfer` (cặp TRANSFER_OUT / IN). `Reverse` giữ nguyên |
| scm-inv-balance | InventoryBalance | Lỗi: CRUD Insert / Update / Delete sửa thẳng tồn (lệch sổ cái) | CRUD → lỗi nghiệp vụ. Mới: `GetSummary` (kho × nguyên liệu: tồn / giữ chỗ / khả dụng / **xuất được** / bị chặn / hết hạn / lô sắp hết hạn / mức tồn theo InventoryPolicie), `GetByLot` (theo lô + vị trí, thứ tự FEFO / FIFO, lý do chặn), `RebuildBalance` (chỉ admin, tính lại từ sổ qua `InventoryLedgerService.RebuildBalancesAsync`). Sổ cái: thêm chặn khác đơn vị dòng tồn |
| scm-lot | MaterialLot | Thiếu / Lỗi: Insert không ghi cột cũ LotNo / QualityStatus, nhận Status tùy ý; Update đổi được QC KHONG_DAT → DAT, đổi trạng thái, đổi nguyên liệu của lô đã có giao dịch; Delete lô đang có tồn | Insert: số lô bắt buộc (trống + có NCC → sinh `NL-…` qua T11), Status chỉ Available / OnHold, ghi song song cột cũ. Update: giữ Status, chặn sửa QC không đạt, chặn đổi nguyên liệu khi có giao dịch. Delete: chặn khi có giao dịch / tồn. Mới: `Hold`, `Release` |
| scm-issue | MaterialIssue | Thiếu: CRUD tạo phiếu không số, không dòng, không ghi sổ, đổi trạng thái tùy ý | CRUD Insert → `Create`; Update chỉ Note / IssueDate phiếu Nháp; Delete chỉ Nháp (kèm dòng). Mới: `SuggestLots` (FEFO / FIFO), `Create` (JSON, số PH-, chặn lô hết hạn / QC / tạm giữ / vượt tồn, cảnh báo lô nên xuất trước, tự phân bổ khi không chọn lô), `Post` (Draft → Issued, bút toán ISSUE qua sổ cái, 1 transaction, kiểm lại toàn bộ trước khi ghi), `Cancel` (Nháp → Hủy; Đã xuất → bút toán đảo), `GetDetail` |
| scm-issue-line | MaterialIssueLine | Lỗi: CRUD ghi dòng không kiểm lô / tồn | Insert / Update / Delete → lỗi nghiệp vụ (chỉ qua `MaterialIssue/Create`) |
| scm-stockcount | StockCount | Thiếu: CRUD tạo phiếu không dòng, đổi trạng thái tùy ý, không ghi điều chỉnh | CRUD Insert → `Create`; Update chỉ Note / người phụ trách khi Draft / Counting; Delete chỉ Draft / Counting. Mới: `Create` (dòng từ tồn hệ thống, số PH-), `RecordCounts` (JSON), `Post` (ADJUSTMENT; lệch > 2% chỉ admin – duyệt để sau Pilot), `GetDetail` (% lệch) |
| scm-stockcount-line | StockCountLine | Lỗi: CRUD ghi số lệch tùy ý | Insert / Update / Delete → lỗi nghiệp vụ (chỉ qua `StockCount/Create`, `RecordCounts`) |
| scm-reservation | MaterialReservation | Có CRUD; không đụng tồn (ReservedQuantity của InventoryBalance không đổi) | Không sửa – **DEFERRED(PILOT)** (giữ chỗ theo lệnh SX làm cùng T14 / T17). Tồn khả dụng = OnHand − ReservedQuantity vẫn được tôn trọng khi xuất |
| scm-inv-policy | InventoryPolicie | Có (T45B: chuỗi Min ≤ Safety ≤ Reorder ≤ Max, 1 dòng / (nguyên liệu, kho)) | Không sửa; `UseFefo`, `ExpiryWarningDays`, mức tồn được đọc trong gợi ý lô và `GetSummary` |
| scm-warehouse / storage-location | Warehouse / StorageLocation | Có (CRUD, `Warehouse/GetLayout`) | Không sửa; dùng để kiểm kho / vị trí thuộc kho khi ghi sổ |

## (e) Pilot P5 – tính nhu cầu NL (MRP) + đề nghị mua nháp (T14, 2026-11-16)

Rà trước khi viết: MaterialRequirement, ProductionPlan / ProductionPlanLine, PurchaseRequest / PurchaseRequestLine, SupplierMaterial đều chỉ có CRUD sinh mã (Controller / Service / Repository / Request / ViewModel / SP `usp<Entity>_GetAll`), chưa có nghiệp vụ. Dùng lại: `IDocumentNumberService` (KH-, PH-), `IUomConversionService.ConvertAsync`, `InventoryLotRules.LoadAsync` (T16, tồn xuất được / bị chặn), `MenuService.MenuDayOf`, quy ước công thức khóa của `RecipeService.Lock` (SnapshotJson). Không tạo service / controller thứ hai.

| Màn hình | Bảng → Controller / Service cũ | Đánh giá trước T14 | Bổ sung / sửa trong T14 |
|---|---|---|---|
| scm-requirement | MaterialRequirement | Thiếu / Lỗi: CRUD ghi số nhu cầu tùy ý, không có tính toán | Mới: `Calculate` (form), `GetByDate`, `GetDetail` trong `MaterialRequirementService` / `MaterialRequirementController`. CRUD Insert / Update / Delete → lỗi nghiệp vụ (chỉ ghi qua Calculate). `GetAll` giữ nguyên |
| prod-plan / prod-plan-line | ProductionPlan / ProductionPlanLine | Có CRUD | Không sửa service; Calculate tạo / cập nhật kế hoạch Draft neo theo ngày (GAP-MRP-01). Lập kế hoạch theo ca, duyệt, phát hành → T17 |
| scm-pr / scm-pr-line | PurchaseRequest / PurchaseRequestLine | Có CRUD (đổi được Status tùy ý) | Không sửa trong T14; Calculate sinh PR Draft + dòng (PH-, ghi song song RequestNo / UomId). Submit / duyệt / chuyển PO và chặn CRUD đổi trạng thái → T15 |
| scm-supplier-material | SupplierMaterial | Có (T45B: một NCC ưu tiên / nguyên liệu) | Không sửa; đọc NCC gợi ý. FE-2: lọc `SupplierId` / `IngredientId` trống = không lọc |

FE-2 follow-up (cùng nhánh, commit riêng): 7 SearchRequest lọc cha đổi `Guid` → `Guid?` (CustomerContact / CustomerDeliveryLocation / Contract: CustomerId; ContractMeal: ContractId; SupplierMaterial: SupplierId, IngredientId; InventoryPolicy: IngredientId; StorageZone: WarehouseId) – service đổi `Guid.Empty` thành NULL, SP đã xử lý NULL sẵn. `uspSupplier_GetAll` / `uspUnitMeasure_GetAll` viết lại theo mẫu CodeGen: đủ cột ViewModel, lọc `@Status` (+ `@CompanyId` cho NCC), tham số mới có mặc định. **Quy ước mới:** form `[FromForm]` luôn đọc theo InvariantCulture (`InvariantCultureValueProviderFactory`, GAP-FORM-01) – query string vốn đã invariant.

## (f) Pilot P6 – mua hàng, nhận hàng, QC đầu vào (T15, 2026-11-16)

Rà trước khi viết: PurchaseRequest(+Line), PurchaseOrder(+Line), GoodsReceipt(+Line), QCInspection(+Line) (entity `QcInspection*`), SupplierIssue, TemperatureLog đều chỉ có CRUD sinh mã; CRUD đổi được trạng thái / số / tổng tiền tùy ý, dòng sửa tùy ý, không ghi sổ kho. `GoodsReceiptSearchRequest.SupplierId` / `PurchaseOrderSearchRequest.SupplierId` kiểu `Guid` → lọc trống thành Guid rỗng, danh sách luôn rỗng (lỗi). Dùng lại: engine duyệt T12 (`IApprovalHandler`, `ApprovalRequestService.CreateAsync / Decide`), `IDocumentNumberService` (PO-, PH-, NL-), `InventoryLedgerService.PostAsync` (T08 / T16), `InventoryLotRules` (chặn lô QC không đạt – T16), `IUomConversionService`, kết quả MRP (T14). Không tạo service / controller mới; thêm 1 lớp tĩnh `PurchasingRules` (như `InventoryLotRules`) và 1 handler duyệt.

| Màn hình | Bảng → Controller / Service cũ | Đánh giá trước T15 | Bổ sung / sửa trong T15 |
|---|---|---|---|
| scm-pr / scm-pr-line | PurchaseRequest / PurchaseRequestLine | CRUD đổi Status tùy ý, không lập PO | Insert: luôn Draft, số PH-; Update / Delete chỉ Draft (không đổi trạng thái / số). Dòng: chỉ khi đề nghị Draft, ghi song song UomId. Mới: `GetDetail`, `ConvertToPurchaseOrder`, `Cancel` |
| scm-po / scm-po-line | PurchaseOrder / PurchaseOrderLine | CRUD đổi Status / tổng tiền tùy ý, không duyệt | Insert: Draft, số PO-, tổng 0, kiểm NCC; Update chỉ Draft (ngày, ghi chú, NCC khi chưa có dòng); Delete chỉ Draft lập tay. Mới: `GetDetail`, `UpdateLines` (JSON), `Submit`, `Approve` / `Reject` (engine T12, `[Permission(scm-po, Approve)]`), `Send`, `Cancel`, `Close`, `ExportOrder` (.xlsx). `PurchaseOrderLine` CRUD ghi → lỗi nghiệp vụ. GetAll: lọc Status / NCC / đề nghị (NCC trống = không lọc) |
| wf-inbox?type=scm-po | ApprovalRequest | Seed mục menu "Duyệt – Đơn mua hàng" đã có (CEO, COO) | Mới `Services/Implement/Approval/PurchaseOrderApprovalHandler` (BusinessType `PURCHASE_ORDER`) – đăng ký tự động theo assembly scan |
| scm-gr / scm-gr-line | GoodsReceipt / GoodsReceiptLine | CRUD tạo phiếu không số, không PO, không ghi sổ | Insert / Delete → lỗi nghiệp vụ (Create / Cancel); Update chỉ Note. Mới: `GetDetail`, `Create` (JSON, theo PO đã duyệt, PH-, phiếu QC), `Post` (lô NL-, ghi sổ phần đạt, TemperatureLog, SupplierIssue, trạng thái PO), `Cancel`. `GoodsReceiptLine` CRUD ghi → lỗi nghiệp vụ. GetAll: NCC / PO / kho trống = không lọc |
| qc-inspection / qc-inspection-line | QCInspection / QCInspectionLine | CRUD ghi kết quả tùy ý | Insert / Delete → lỗi nghiệp vụ; Update chỉ Note / Remark. Mới: `GetDetail`, `Conclude` (JSON). `QcInspectionLine` CRUD ghi → lỗi nghiệp vụ |
| scm-requirement | MaterialRequirement | (T14) | Mới: `ExportPreProduction` (.xlsx Phiếu trước sản xuất) |
| qc-supplier-issue / qc-temperature | SupplierIssue / TemperatureLog | CRUD (TemperatureLog chỉ ghi thêm – T08) | Không sửa; T15 ghi SupplierIssue + TemperatureLog RECEIVING khi ghi sổ phiếu nhập |

## (g) Pilot P7 – sản xuất theo mẻ (T17, 2026-11-16)

Rà trước khi viết: ProductionPlan(+Line), ProductionOrder, ProductionBatche (entity `ProductionBatch`), ProductionOutput, MealWasteRecord, TemperatureLog, FoodSafetyChecklist(+Item, +Result), ProductionLine, Shift đều chỉ có CRUD sinh mã: CRUD tạo lệnh / mẻ không số, đổi `Status` / `QcStatus` / số lượng tùy ý (mẻ có thể "đóng gói" mà không đo nhiệt độ, không lưu mẫu); `ProductionPlanLine` sửa được cả khi kế hoạch đã duyệt; `ProductionOutput` không gắn số đạt của mẻ. Kế hoạch `KH-` + dòng món do T14 (`MaterialRequirementService.Calculate`) tạo – **không sửa** service này (T17 chỉ đọc). Phiếu xuất (T16) gắn `ProductionOrderId` rỗng (GAP-INV-02). DB dev chưa có `ProductionLine`, chưa có checklist `SAMPLE_RETAINED` (→ seed mới).

| Màn hình | Bảng → Controller / Service cũ | Đánh giá trước T17 | Bổ sung / sửa trong T17 |
|---|---|---|---|
| prod-plan | ProductionPlan | CRUD đổi Status tùy ý | Insert / Update → lỗi nghiệp vụ (tạo bằng MRP Calculate; Note là JSON MRP); Delete chỉ Nháp. Mới: `GetDetail`, `Approve` (`[Permission(prod-plan, Approve)]`), `Release` (lệnh SX PH- + mẻ ME-, lịch ngược, đơn → InProduction), `ExportProductionSheet` (.xlsx) |
| prod-plan-line | ProductionPlanLine | CRUD ghi tùy ý | Insert / Update / Delete chỉ khi kế hoạch Nháp, 1 món 1 dòng, số phần > 0, ghi song song PlannedQty |
| prod-order | ProductionOrder | CRUD tạo lệnh không số, đổi trạng thái | Insert / Delete → lỗi nghiệp vụ; Update chỉ Note / ResponsibleEmployeeId. Mới: `GetDetail` (ảnh chụp định lượng, mẻ, phiếu xuất). Trạng thái tự đổi theo mẻ |
| prod-batch | ProductionBatche | CRUD tạo / đổi trạng thái tùy ý | Insert / Delete → lỗi nghiệp vụ; Update chỉ trạm bếp (Kế hoạch / Đang nấu). Mới: `GetBoard`, `GetDetail`, `CreateMaterialIssue` (gọi `MaterialIssueService.Create` / `Post` – FEFO, chặn lô), `Start`, `Finish`, `RecordCoreTemperature`, `RecordSampleRetained`, `RecordOutput`, `RecordWaste`, `Release`, `Hold`, `Reject`, `Pack`, `ExportProductionSheet` |
| prod-output | ProductionOutput | CRUD ghi tùy ý | Insert / Update / Delete → lỗi nghiệp vụ (ghi qua `ProductionBatch/RecordOutput` / `Reject`) |
| prod-waste | MealWasteRecord | CRUD | Dòng gắn mẻ chỉ ghi qua `ProductionBatch/RecordWaste` (Insert / Update / Delete dòng có `ProductionBatchId` → lỗi); dòng không gắn mẻ giữ CRUD. Gỡ header auto-generated |
| qc-temperature / qc-checklist-result | TemperatureLog / FoodSafetyChecklistResult | CRUD (TemperatureLog chỉ ghi thêm – T08) | Không sửa; T17 ghi CORE + SAMPLE_RETAINED qua action của mẻ |
| (dùng chung) | – | – | Lớp tĩnh `Services/Implement/ProductionRules` (chọn công thức như MRP, lịch ngược, nguyên liệu của mẻ, điều kiện ATTP, mẫu in Phiếu sản xuất) – không đăng ký DI, không phải service thứ hai |

## (h) Pilot P8 – giao vận, đối soát suất, phản ánh (T19, 2026-11-16)

Rà trước khi viết: DeliveryRoute, DeliveryStop, DeliveryOrder, DeliveryEvent, ProofOfDeliverie (entity `ProofOfDelivery`), OrderReconciliation, CustomerComplaint, ComplaintHistory, Vehicle, TemperatureLog đều chỉ có CRUD sinh mã: chuyến / đơn giao tạo không số, đổi `Status` / `StopStatus` tùy ý, POD / đối soát nhập tay mọi con số, `DeliveryEvent/Insert` ghi được nhiệt độ "đạt" giả, phản ánh tạo không số / không hạn SLA. Không có kiểm nhiệt độ giao 60 °C. `DocumentNumberService` (T11) đã có CX / PA / PH (DeliveryOrder). DeliveryExecution / DeliveryLoading (xếp hàng) không dùng trong Pilot (GAP-DLV-02).

| Màn hình | Bảng → Controller / Service cũ | Đánh giá trước T19 | Bổ sung / sửa trong T19 |
|---|---|---|---|
| log-route | DeliveryRoute | CRUD tạo chuyến không số, đổi trạng thái | Insert → lỗi nghiệp vụ (dùng `Create`); Update chỉ tên / mô tả / tài xế / giờ kế hoạch khi Kế hoạch; Delete chỉ Kế hoạch chưa có sự kiện (xóa kèm điểm dừng + đơn giao). Mới: `GetBoard`, `GetReadyOrders`, `GetDetail`, `Create` (JSON), `RecordTemperature`, `Dispatch`, `ExportDeliveryNote` (.xlsx) |
| log-stop | DeliveryStop | CRUD | Insert / Delete → lỗi; Update chỉ thứ tự / giờ dự kiến / ghi chú khi chuyến + điểm Kế hoạch. Mới: `Arrive`, `Deliver` (POD), `Fail`. Gỡ header auto-generated |
| log-delivery-order | DeliveryOrder | CRUD | Insert / Delete → lỗi (sinh khi lập chuyến); Update chỉ ghi chú |
| log-event | DeliveryEvent (chỉ ghi thêm – T08) | CRUD Insert ghi tùy ý | Insert → lỗi (sự kiện / nhiệt độ chỉ qua action chuyến / điểm). Gỡ header auto-generated |
| log-pod | ProofOfDeliverie | CRUD | Insert / Update / Delete → lỗi (ghi qua `DeliveryStop/Deliver`, không sửa) |
| crm-reconciliation | OrderReconciliation | CRUD nhập tay số liệu | Insert / Update / Delete → lỗi. Mới: `GetPendingOrders`, `GetDetail`, `GetByOrder`, `Reconcile`, `Close` |
| log-complaint | CustomerComplaint | CRUD tạo không số / SLA, đổi trạng thái | Insert / Delete → lỗi; Update chỉ phân loại / kênh / mô tả / mức độ (tính lại SLA) khi Mới / Đang xử lý. Mới: `GetDetail`, `Create`, `Assign`, `Resolve`, `Close`, `Reject`, `AddComment`. Gỡ header auto-generated |
| log-complaint-history | ComplaintHistory (chỉ ghi thêm) | CRUD Insert | Insert → lỗi (ghi qua action phản ánh). Gỡ header auto-generated |
| qc-temperature | TemperatureLog | CRUD (chỉ ghi thêm) | Không sửa; T19 ghi DELIVERY qua action (căn cứ chặn là `DeliveryEvent` TEMP_CHECK) |
| prod-batch | ProductionBatche | (T17) | D-42: `RecordSampleRetained` nhận thêm mẻ Đã đóng gói; `Release` chỉ cảnh báo khi chưa lưu mẫu (`ProductionRequireSampleBeforeRelease` = false) |
| (dùng chung) | – | – | Lớp tĩnh `Services/Implement/DeliveryRules` (nhiệt độ giao, điều kiện xuất phát, chi tiết chuyến, tạo phản ánh PA-, mẫu in) – không đăng ký DI, không phải service thứ hai |
