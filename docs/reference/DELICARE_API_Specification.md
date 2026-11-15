# DELICARE – Đặc tả API cho đội phát triển (Angular + .NET)

Nov 14, 2026 · @MrPham

## 1. Tổng quan và quy ước chung

Backend là ASP.NET Core Web API (.NET 8) cung cấp REST/JSON tại `/api/v1` cho frontend Angular. Mỗi màn hình trong mockup (Screen ID `#/xxx`) ứng với một nhóm endpoint trong tài liệu này. Database là SQL Server, 124 bảng thuộc schema `dbo`, **không có FK vật lý**. Vì vậy mọi kiểm tra quan hệ đều đặt ở tầng Service.

Nguồn căn cứ: `20261113_structure_db_appdelicare.sql`, `DELICARE_Module_Function_Mapping.xlsx` (119 chức năng), `DELICARE_Database_Gap_Register.xlsx`, `DELICARE_Business_Workflow.md` và mockup HTML.

### 1.1 Kiến trúc lớp đề xuất (.NET)

| Lớp | Trách nhiệm | Ghi chú |
| --- | --- | --- |
| `Delicare.Api` | Controller, xác thực, phân quyền, envelope, Swagger | Mỗi module một Area: Sales, Menu, Scm, Production, Qc, Logistics, Hr, Workflow, Integration, Kpi, Admin, Meta |
| `Delicare.Application` | Use case (CQRS: Command/Query), validator (FluentValidation), state machine | Logic nghiệp vụ nằm tại đây |
| `Delicare.Domain` | Entity map 1–1 với bảng SQL, enum trạng thái | Giữ đúng tên legacy: `Dishe`, `ProductionBatche`, `ProofOfDeliverie`… |
| `Delicare.Infrastructure` | EF Core (Database-First), Dapper cho báo cáo, ghi AuditChange, job nền (Hangfire/Quartz) | Không migrate thêm bảng/cột |
| `Delicare.Integration` | Webhook Zalo/Telegram/Facebook, gửi tin ra | Dùng các bảng Integration\* |

&#91;embedded content: kiến trúc backend · 5 lớp, 2 nguồn gọi vào\]

Webhook từ Zalo/Telegram/Facebook đi vào cùng lớp Api, sau đó được xử lý bất đồng bộ ở Integration. Jobs chạy theo lịch và gọi thẳng xuống Infrastructure.

### 1.2 Quy ước chung cho mọi API

| Chủ đề | Quy ước |
| --- | --- |
| Base URL | `/api/v1/{module}/{resource}`, tên resource dạng kebab-case số nhiều, ví dụ `/api/v1/sales/orders` |
| Xác thực | JWT Bearer. Claims: `uid` = `User.Id`, `groups` = danh sách `UserGroup.Id` (qua `UserClaim`), `pid` = `User.PersonId` |
| Ngữ cảnh đơn vị | Header `X-Branch-Id` = `OrganizationUnit.Id` (UnitType = BRANCH). Không gửi = tất cả đơn vị được phép |
| Phân quyền | Mỗi endpoint gắn `[Permission("<screen-id>", Action)]`. Server tra `ObjectMenu.MetaLink = '#/<screen-id>'` → `UserRole` theo nhóm của user → cờ `AllowAccess/AllowAdd/AllowEdit/AllowDelete/AllowPrint/AllowExport/AllowImport`. Thiếu quyền → 403 |
| Tên trường JSON | camelCase theo đúng tên cột SQL (`orderNumber`, `mealDate`). **Không trả và không nhận cột deprecated** (141 cột, xem Mapping sheet 05). Cột `PassWord` không bao giờ được trả về |
| Envelope | `{ "success": true, "data": …, "message": null, "errors": [], "traceId": "…" }` |
| Danh sách | `data = { items: [...], total, page, pageSize }`. Query: `page` (mặc định 1), `pageSize` (mặc định 20, tối đa 200), `q` (tìm kiếm), `sort=field:asc,field2:desc`, lọc theo tên cột, ví dụ `status=Confirmed&mealDateFrom=2026-11-14` |
| Ngày giờ | ISO 8601. Cột `date` → `YYYY-MM-DD`, cột `time` → `HH:mm:ss`. Quy ước múi giờ chờ chốt (GAP-Q-04); đề xuất lưu UTC, hiển thị Asia/Ho\_Chi\_Minh |
| Audit | Insert: `CreatedDate`, `CreatedBy` = uid. Update: `ModifiedDate`, `ModifiedBy`. Mọi thao tác ghi thêm 1 dòng `AuditChange` (`IdChange` = Id bản ghi, `Action` = INSERT/UPDATE/SOFT\_DELETE/TRANSITION, `EntityName` = tên bảng, `Changes` = JSON trước/sau) |
| Xóa | Chỉ xóa mềm: `IsDeleted = 1`, `DeletedDate`, `DeletedBy`. Bảng chỉ có `DeleteddBy` (20 bảng, ví dụ `User`, `Customer`) thì ghi vào `DeleteddBy`. Mọi truy vấn mặc định lọc `IsDeleted = 0` |
| Kiểm tra tham chiếu | Không có FK nên với mỗi cột `…Id` nằm trong 224 quan hệ logic (Mapping sheet 04), Service kiểm tra bản ghi đích tồn tại và `IsDeleted = 0`. Sai → 422 `REF_NOT_FOUND`. Với 80 tham chiếu chưa rõ (Gap sheet 03): chỉ kiểm tra định dạng GUID |
| Đồng thời | Không có cột rowversion → dùng `ModifiedDate` làm ETag. Client gửi `If-Match`, lệch → 409 `CONCURRENCY_CONFLICT` |
| Trạng thái | Chuyển trạng thái chỉ qua endpoint lệnh (`POST …/{id}/confirm`), không cho PUT trực tiếp cột Status. Sai luồng → 409 `INVALID_TRANSITION` |
| Đánh số chứng từ | Server sinh số (`OrderNumber`, `RequestNumber`…) theo mẫu `PREFIX-yyMMdd-NNN` (PROPOSED), dùng bảng khóa hoặc sequence trong transaction |
| Lệnh nhiều bảng | Bọc trong 1 transaction DB. POST lệnh nhận header `Idempotency-Key` để chống bấm trùng |
| Mã lỗi HTTP | 400 dữ liệu sai, 401 chưa đăng nhập, 403 thiếu quyền, 404 không thấy, 409 xung đột/sai trạng thái, 422 vi phạm nghiệp vụ, 500 lỗi hệ thống (chi tiết ở Phụ lục) |

Các giá trị trạng thái có nhãn **PROPOSED** đang chờ BA/PO chốt. Backend nên định nghĩa chúng trong file cấu hình hoặc enum dùng chung để đổi được mà không phải sửa logic.

## 2. API dùng chung

Khung CRUD chuẩn dưới đây áp dụng cho mọi bảng có màn hình danh mục (khoảng 75 màn hình dùng khung chung trong mockup). Các mục 3–12 chỉ ghi phần **khác hoặc bổ sung** so với khung này. Nên cài đặt bằng một `GenericCrudController<TEntity, TDto>` và ghi đè khi cần.

### 2.1 CRUD chuẩn cho một resource

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/{resource}` | Query: `page`, `pageSize`, `q`, `sort`, lọc theo cột (`status`, `customerId`, `…From`/`…To` cho ngày) | `{items, total, page, pageSize}`; mỗi item = DTO danh sách + `{field}Label` cho cột tham chiếu (ví dụ `customerIdLabel`) | Lọc `IsDeleted = 0`, lọc theo `X-Branch-Id` nếu bảng có cột đơn vị. `q` tìm trên cột mã/số/tên |
| GET | `/{resource}/summary` | Cùng bộ lọc | `{ total, byStatus: [{ status, count }] }` | Phục vụ thẻ KPI và chip "Lọc nhanh" trên mọi màn hình danh sách |
| GET | `/{resource}/{id}` | `id` (GUID) | DTO chi tiết đầy đủ cột (trừ deprecated, mật khẩu) + label tham chiếu + `etag` | 404 nếu không có hoặc đã xóa mềm |
| POST | `/{resource}` | DTO tạo mới | DTO vừa tạo (201) | Validate: cột NOT NULL không có DEFAULT là bắt buộc, độ dài theo `nvarchar(n)`, UNIQUE (18 ràng buộc), tham chiếu tồn tại. Gán DEFAULT của SQL nếu client không gửi. Ghi audit |
| PUT | `/{resource}/{id}` | DTO sửa + `If-Match` | DTO sau sửa | Không cho sửa cột trạng thái, số chứng từ, khóa. Ghi `AuditChange` với danh sách cột thay đổi |
| DELETE | `/{resource}/{id}` | `id` | 204 | Xóa mềm. Chặn 409 `IN_USE` nếu có bản ghi khác tham chiếu đến (kiểm tra theo quan hệ logic đi vào). Cần quyền `AllowDelete` |
| POST | `/{resource}/{id}/transition` | `{ to, note }` | DTO sau chuyển | Dùng cho màn hình chung có nút "→ trạng thái kế". Kiểm tra theo state machine của bảng (Phụ lục B). Ghi audit action `TRANSITION` |
| GET | `/{resource}/export` | Cùng bộ lọc + `format=xlsx\|csv` | File | Cần `AllowExport`. Tiêu đề cột gồm nhãn tiếng Việt và tên cột SQL |
| POST | `/{resource}/import` | File xlsx theo mẫu | `{ inserted, updated, errors: [{ row, field, message }] }` | Cần `AllowImport`. Chạy validate như POST, all-or-nothing |

### 2.2 API hỗ trợ giao diện

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/meta/lookups/{table}` | `q`, `take` (mặc định 50), `ids` (để lấy label theo Id) | `[{ id, label, code, status }]` | Dùng cho mọi dropdown tham chiếu. Label theo thứ tự ưu tiên: DisplayName → Name → FullName → số chứng từ → Code. Chỉ lấy bản ghi `Status = Active` khi bảng có Status |
| GET | `/api/v1/meta/statuses` | – | `{ "Order.Status": { codes:[…], labels:[…], source: "PROPOSED" }, … }` | 47 bộ trạng thái (Gap sheet 07). Angular dùng để vẽ badge và bộ lọc |
| GET | `/api/v1/meta/reference-lists` | – | `{ MealType:[…], WasteType:[…], CustomerType:[…], … }` | 12 danh mục tham chiếu chưa có bảng (GAP-F-06), đọc từ file cấu hình |
| GET | `/api/v1/meta/menu` | – | Cây `ObjectMenu` (LevelShow, OrderShow, MetaLink, IconName) kèm cờ quyền | Chỉ trả mục có `AllowAccess = 1` với nhóm của user. Angular dựng sidebar và ẩn/hiện nút Thêm/Sửa/Xóa/Xuất |
| GET | `/api/v1/meta/me` | – | `{ user, person, groups, branches, permissions }` | Gọi một lần khi đăng nhập |
| GET | `/api/v1/audit/{table}/{id}/history` | – | `[{ at, by, action, changes }]` | Tab "Lịch sử xử lý" trong drawer chi tiết. Gộp `AuditChange` + `ApprovalDecision` (nếu bản ghi có phiếu duyệt) + `ComplaintHistory` (với khiếu nại), sắp xếp mới nhất trước |
| GET | `/api/v1/meta/related/{table}/{id}` | – | `[{ table, column, count, items:[{ id, label, status }] }]` | Tab "Liên quan": duyệt quan hệ logic đi vào bảng, tối đa 8 dòng mỗi bảng |
| GET | `/api/v1/meta/global-search` | `q` (≥ 2 ký tự) | `[{ type: "screen\|table\|record", label, route, table, id }]` | Ô tìm kiếm trên header: tìm chức năng, tên bảng, Customer, Order, Ingredient, Dishe, Supplier, MaterialLot, Employee, PurchaseOrder |
| GET | `/api/v1/notifications/alerts` | `X-Branch-Id` | `[{ severity, title, detail, route, count }]` | Chuông cảnh báo. Quy tắc tính ở mục 12.1 |

### 2.3 Gửi phê duyệt (dùng chung cho mọi nghiệp vụ)

Mọi màn hình có nút "Gửi duyệt" gọi một service nội bộ `ApprovalService.Submit(businessType, businessId, steps)`. Service này không expose riêng; mỗi nghiệp vụ có endpoint `POST …/{id}/submit`. Logic:

1. Kiểm tra bản ghi đang ở trạng thái cho phép gửi (ví dụ Draft). Không có yêu cầu nào đang mở cho cùng `BusinessId`.
2. Tạo `ApprovalRequest`: `RequestNo` sinh tự động, `BusinessType`, `BusinessId`, `RequestedBy` = uid, `RequestStatus = Pending`, `CurrentStepNo = 1`, `DueAt`, `PayloadJson` = snapshot bản ghi.
3. Tạo các `ApprovalStep` (`StepNo`, `ApproverRoleCode`, `ApproverUserId` nếu xác định được, `DecisionStatus = Waiting`) theo cấu hình mẫu quy trình trong file cấu hình (GAP-F-08 – chưa có bảng).
4. Tạo `ApprovalItem` (`ItemType` = BusinessType, `ItemId` = BusinessId, `ActionCode = APPROVE`).
5. Cập nhật trạng thái bản ghi nghiệp vụ sang `PendingApproval` (hoặc tương đương), ghi audit, đẩy `OutboundMessage` thông báo người duyệt qua Telegram (nếu người duyệt có `UserExternalIdentity`).

Cấu hình quy trình đề xuất (PROPOSED):

| BusinessType | Bảng | Các bước duyệt | Khi duyệt xong | Khi từ chối |
| --- | --- | --- | --- | --- |
| PURCHASE\_REQUEST | PurchaseRequest | 1. Quản lý kho → 2. Ban giám đốc | Status = Approved | Status = Rejected |
| CONTRACT | Contract | 1. Ban giám đốc | Status = Active | Status = Draft |
| RECIPE\_VERSION | Recipe | 1. Quản lý bếp | Status = Approved, ghi ApprovedBy/ApprovedAt | Status = Draft |
| MENU\_CHANGE | MenuChangeRequest | 1. Bếp trưởng | RequestStatus = Approved, ghi ApprovedBy/ApprovedAt | RequestStatus = Rejected |
| STOCK\_COUNT | StockCount | 1. Quản lý kho → 2. Kế toán | ghi ApprovedBy/ApprovedAt, cho phép ghi sổ | Status = Draft |
| LEAVE\_REQUEST | LeaveRequest | 1. Quản lý bếp | RequestStatus = Approved | RequestStatus = Rejected |
| SHIFT\_SWAP | ShiftSwapRequest | 1. Bếp trưởng | RequestStatus = Approved + hoán đổi ShiftAssignment | RequestStatus = Rejected |
| STOCK\_ADJUSTMENT | (GAP-F-04) | 1. Quản lý kho | Ghi InventoryTransaction ADJUSTMENT | – |

Quyết định duyệt/từ chối dùng chung endpoint ở mục 11.5 (Hộp phê duyệt).

### 2.4 Tài liệu đính kèm

Chưa có bảng liên kết tài liệu với chứng từ (GAP-F-09: `DocumentFile` chỉ có `TestDemoId`). Trong khi chờ BA/PO quyết định, dùng endpoint tải file chung `POST /api/v1/files` (multipart) trả về URL. URL này ghi vào các cột `…Url` có sẵn: `ProofOfDeliverie.SignatureUrl/PhotoUrl`, `TemperatureLog.EvidenceUrl`, `FoodSafetyChecklistResult.EvidenceUrl`, `CleaningChecklistResult.EvidenceUrl`, `DeliveryEvent.PhotoUrl`.

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/files` | multipart: `file`, `category` (signature, photo, evidence, contract, other) | `{ fileId, url, fileName, fileType, fileSize }` | Giới hạn 20 MB; chỉ nhận pdf, jpg, png, xlsx, docx. Lưu file vào object storage, ghi `DocumentFile` (`FileName`, `FileType`, `FileSize`, `FileNameHas` = hash SHA-256 để phát hiện trùng). `url` là đường dẫn nội bộ có ký, không công khai |
| GET | `/api/v1/files` | `q`, `fileType`, `from`, `to`, `createdBy` | Danh sách `DocumentFile` | Màn hình `#/adm-document`. Chưa lọc được theo chứng từ vì thiếu cột liên kết (GAP-F-09) |
| GET | `/api/v1/files/{id}/download` | – | Luồng file (`Content-Disposition`) | Kiểm tra quyền truy cập màn hình chứa chứng từ; ghi audit lượt tải |
| DELETE | `/api/v1/files/{id}` | – | 204 | Xóa mềm `DocumentFile`. Chặn 409 `IN_USE` nếu URL của file đang nằm trong cột `…Url` của bản ghi đã chốt (POD đã xác nhận, phiếu QC đã kết luận) |

### 2.5 In phiếu (PDF)

Mọi endpoint in yêu cầu quyền `AllowPrint` trên màn hình tương ứng, trả `application/pdf`, ghi `AuditChange` với `Action = PRINT`. Mẫu in dùng thư viện (QuestPDF hoặc tương đương), logo và thông tin pháp nhân lấy từ `Company`.

| Endpoint | Quyền theo màn hình | Nội dung phiếu | Điều kiện in |
| --- | --- | --- | --- |
| `GET /scm/purchase-orders/{id}/print` | `scm-po` | Header PO, NCC, dòng hàng (nguyên liệu, SL, đơn giá, thành tiền), tổng tiền, ngày giao dự kiến | Status ≠ Draft |
| `GET /scm/goods-receipts/{id}/print` | `scm-gr` | Phiếu nhập: NCC, kho, dòng nhận (số lô, NSX, HSD, SL nhận/đạt/loại), kết luận QC | QC đã kết luận |
| `GET /scm/material-issues/{id}/print` | `scm-issue` | Phiếu xuất: lệnh SX, dòng xuất theo lô, chữ ký kho / bếp | Status = Issued |
| `GET /scm/stock-counts/{id}/print` | `scm-stockcount` | Biên bản kiểm kê: tồn sổ, tồn thực tế, chênh lệch, lý do | Mọi trạng thái; bản chưa Posted in chữ "BẢN NHÁP" |
| `GET /logistics/delivery-orders/{id}/print` | `log-delivery-order` | Phiếu giao hàng: khách, điểm giao, khung giờ, số suất theo loại, xe, tài xế, ô ký nhận | Không giới hạn |
| `GET /logistics/pods/{id}/print` | `log-pod` | Biên bản giao nhận: SL giao/nhận/từ chối, người nhận, ảnh chữ ký (`SignatureUrl`), thời điểm | POD đã tạo |
| `GET /sales/reconciliations/{id}/print` | `crm-reconciliation` | Bảng đối soát Đặt – Sản xuất – Đóng gói – Giao – Nhận, giá trị chốt | Status ≠ Open |
| `POST /print/batch` | theo từng phiếu | `{ items: [{ type, id }] }` → một file PDF gộp | Dùng in hàng loạt phiếu giao theo tuyến; tối đa 100 phiếu |

## 3. P0 – Nền tảng và quản trị (module `admin`)

P0 gồm 19 bảng. Các danh mục `Company`, `Position`, `Administrative*`, `DocumentFile`, `CoreDataMergeConflict` dùng CRUD chuẩn (mục 2.1). Phần dưới chỉ ghi các endpoint có logic riêng.

### 3.1 Xác thực – `User`, `Person`, `UserClaim`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/auth/login` | `{ userName, password }` | `{ accessToken, refreshToken, expiresIn }` | Tìm `User` theo `UserName`, `IsActive = 1`, `IsDeleted = 0`. So khớp hash của `PassWord` (đề xuất BCrypt; cột là `nvarchar(max)`). Ghi `LocationTracking` (`IPv4`, `PcName` từ User-Agent) cho mỗi lần đăng nhập. Sai 5 lần → khóa tạm (cấu hình) |
| POST | `/api/v1/auth/refresh` | `{ refreshToken }` | Token mới | Refresh token lưu cache/Redis (không có bảng) |
| POST | `/api/v1/auth/change-password` | `{ oldPassword, newPassword }` | 204 | Cập nhật `PassWord` (hash), ghi audit không kèm giá trị mật khẩu |

### 3.2 Người dùng – `#/adm-user`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/admin/users` | Lọc `isActive`, `groupId`, `q` | Danh sách `{ id, userName, isActive, isAdmin, person: { fullName, phoneNumber }, groups: [{ id, name }] }` | Join `Person` qua `PersonId`, `UserGroup` qua `UserClaim`. Không trả `PassWord` |
| POST | `/admin/users` | `{ userName, password, person: { fullName, phoneNumber, sex, address, dateOfBirth }, groupIds: [] }` | User vừa tạo | Transaction: tạo `Person` → tạo `User` (`PersonId`, `IsActive = 1`) → tạo `UserClaim` cho từng nhóm. `UserName` không được trùng (kiểm tra ở service vì không có UNIQUE) |
| PUT | `/admin/users/{id}/groups` | `{ groupIds: [] }` | Danh sách nhóm mới | So sánh với `UserClaim` hiện tại: thêm mới, xóa mềm bản ghi bị bỏ |
| POST | `/admin/users/{id}/lock` · `/unlock` | `{ reason }` | 204 | Đặt `IsActive` = 0 / 1 |
| POST | `/admin/users/{id}/reset-password` | – | `{ temporaryPassword }` | Chỉ nhóm Quản trị hệ thống |

GAP-C-10: chưa có cột liên kết `Person` ↔ `Employee`. Trong lúc chờ, liên kết user với nhân viên qua `UserExternalIdentity.EmployeeId` hoặc cấu hình.

### 3.3 Nhóm người dùng (Vai trò) và ma trận quyền – `#/adm-usergroup`, `#/adm-permission`

PO đã xác nhận: Vai trò = `UserGroup`; quyền = `UserRole` (UserGroup × ObjectMenu).

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/admin/user-groups` | – | `[{ id, name, isDefault, menuCount, userCount }]` | Đếm `UserRole` có `AllowAccess = 1` và `UserClaim` |
| GET | `/admin/user-groups/{id}/permissions` | – | Cây ObjectMenu: `[{ menuId, name, metaLink, parentId, allowAccess, allowAdd, allowEdit, allowDelete, allowPrint, allowExport, allowImport }]` | Left join `ObjectMenu` với `UserRole` của nhóm; menu chưa có dòng → mọi cờ = false |
| PUT | `/admin/user-groups/{id}/permissions` | `[{ menuId, allowAccess, …, allowImport }]` (chỉ các dòng thay đổi) | Ma trận sau cập nhật | Upsert `UserRole`. Quy tắc: bật bất kỳ cờ nào → tự bật `AllowAccess`; tắt `AllowAccess` → tắt toàn bộ cờ. Ghi audit cho từng dòng. Xóa cache quyền của các user trong nhóm |
| POST | `/admin/user-groups/{id}/permissions/bulk` | `{ allowAccess: true\|false }` | 204 | Cấp/thu hồi truy cập toàn bộ menu cấp 2 |

**Xem trước quyền theo vai trò** (nút "Xem mockup với vai trò này"). Bản thật không đăng nhập thay người khác; chỉ cho admin xem menu và cờ quyền mà một nhóm sẽ nhận được.

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/admin/user-groups/{id}/preview` | – | `{ group, menu: [cây ObjectMenu chỉ gồm mục AllowAccess = 1, kèm cờ quyền], screenCount, users: [{ id, userName }] }` | Chỉ nhóm Quản trị hệ thống. Cùng logic với `/meta/menu` nhưng tính cho nhóm được chọn thay vì user đang đăng nhập. Angular hiển thị sidebar chế độ xem trước, mọi nút thao tác bị khóa |
| GET | `/admin/users/{id}/effective-permissions` | – | `[{ menuId, metaLink, allowAccess, allowAdd, allowEdit, allowDelete, allowPrint, allowExport, allowImport, grantedBy: [groupName] }]` | Quyền hiệu lực của một user có nhiều nhóm = OR các cờ trên mọi `UserClaim` (PROPOSED). `grantedBy` cho biết nhóm nào cấp quyền đó |

### 3.4 Menu chức năng – `#/adm-objectmenu`

CRUD chuẩn trên `ObjectMenu`, bổ sung: `GET /admin/object-menus/tree` trả cây theo `ParentId`, `LevelShow`, `OrderShow`; `PUT /admin/object-menus/reorder` nhận `[{ id, parentId, orderShow }]`. `MetaLink` phải duy nhất và trùng với route Angular (kiểm tra ở service). Quan hệ `ParentId → ObjectMenu` là đề xuất, chờ BA xác nhận.

### 3.5 Cây đơn vị và nhân viên – `#/adm-orgunit`, `#/adm-employee`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/admin/org-units/tree` | `companyId` | Cây `[{ id, code, name, unitType, managerId, managerName, employeeCount, warehouseCount, children: [] }]` | Dựng theo `OrganizationUnit.ParentId`. Đếm `Employee.OrganizationUnitId`, `Warehouse.OrganizationUnitId` |
| GET | `/admin/branches` | – | `[{ id, code, name }]` | `OrganizationUnit` có `UnitType = BRANCH` (PO xác nhận). Dùng cho bộ chọn đơn vị trên header |
| POST/PUT | `/admin/org-units` | DTO OrganizationUnit | DTO | Chặn vòng lặp cha–con khi đổi `ParentId`. `ManagerId` kiểm tra tồn tại trong `Employee` (đề xuất) |
| GET | `/admin/employees` | `organizationUnitId`, `positionId`, `status`, `q` | Danh sách nhân viên | Dùng cột `Code` (bỏ `EmployeeCode` deprecated) |

### 3.6 Nhật ký – `#/adm-audit`, `#/adm-access-log`

- `GET /admin/audit-changes?entityName=&action=&from=&to=&createdBy=&q=` trả danh sách `AuditChange` và `Changes` đã parse JSON. Chỉ đọc, không sửa/xóa.
- `GET /admin/access-logs?from=&to=&q=` trả `LocationTracking` (IPv4, PcName, CreatedDate, CreatedBy). Bảng này là nhật ký truy cập, **không phải GPS**.
- `GET /admin/merge-conflicts?resolved=false` và `POST /admin/merge-conflicts/{id}/resolve` `{ resolutionNote }` (ghi `ResolvedAt`, `ResolutionNote`) cho `CoreDataMergeConflict`. Id là `bigint`, không phải GUID.

## 4. P1 – Master Data (module `sales`, `scm`, `hr`, `logistics`)

Từ mục này, đường dẫn trong bảng đã bỏ tiền tố `/api/v1`. Các danh mục chỉ cần CRUD chuẩn: `CustomerContact` (`/sales/customer-contacts`), `IngredientCategorie` (`/scm/ingredient-categories`), `UnitMeasure` (`/scm/units`), `StorageZone` (`/scm/storage-zones`), `StorageLocation` (`/scm/storage-locations`), `Shift` (`/hr/shifts`), `Vehicle` (`/logistics/vehicles`).

### 4.1 Khách hàng 360° – `#/crm-customer` (`Customer`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/sales/customers` | `customerType`, `status`, `branchId`, `q` | `[{ id, code, displayName, customerType, branchId, branchName, activeContracts: [{ contractNumber, status }], locationCount, avgMealsPerDay7d, status }]` | `avgMealsPerDay7d` = Σ `Order.TotalQuantity` 7 ngày gần nhất (đơn Reconciled/Delivered) ÷ số ngày có đơn. Lọc `X-Branch-Id` theo `Customer.BranchId` (→ OrganizationUnit, PO xác nhận) |
| GET | `/sales/customers/{id}/overview` | – | `{ customer, contacts[], locations[], contracts: [{ …, meals: [ContractMeal] }], recentOrders[15], kpi: { orderCount, reconciledRevenue, complaintCount } }` | Một lần gọi cho drawer 360°. `reconciledRevenue` = Σ `OrderReconciliation.FinalAmount` |
| POST | `/sales/customers` | DTO Customer | DTO | Bắt buộc: `CustomerType`, `Status`, `CIF`, `DisplayName` (NOT NULL). Kiểm tra trùng `Code`, `CIF` ở service. `RegionId`, `AreaId`, `UserAssignId` chưa có bảng đích → chỉ lưu GUID (GAP-C-09) |

### 4.2 Điểm giao hàng – `#/crm-location` (`CustomerDeliveryLocation`)

CRUD chuẩn tại `/sales/delivery-locations?customerId=`. Validate: `DeliveryWindowFrom < DeliveryWindowTo`; `Latitude` trong \[-90, 90\] và `Longitude` trong \[-180, 180\] nếu có. Không cho ngừng hoạt động (`Status = Inactive`) khi còn `DeliveryOrder` chưa giao đang trỏ đến điểm đó.

### 4.3 Nhà cung cấp và bảng giá – `#/scm-supplier`, `#/scm-supplier-material`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/scm/suppliers/{id}/overview` | – | `{ supplier, materials[], openPurchaseOrders[], issues[], kpi: { onTimeRate, rejectRate } }` | `rejectRate` = Σ `GoodsReceiptLine.RejectedQuantity` ÷ Σ `ReceivedQuantity` 90 ngày |
| GET | `/scm/supplier-materials` | `supplierId`, `ingredientId`, `isPreferred`, `effectiveOn` | Danh sách giá | `effectiveOn` lọc `EffectiveFrom ≤ ngày ≤ EffectiveTo` |
| POST/PUT | `/scm/supplier-materials` | DTO (`SupplierId`, `IngredientId`, `UnitPrice`, `MinimumOrderQuantity`, `LeadTimeDays`, `PurchaseUomId`, `IsPreferred`, …) | DTO | Mỗi nguyên liệu chỉ một dòng `IsPreferred = 1` đang hiệu lực: khi đặt dòng mới là ưu tiên thì tắt dòng cũ. Dùng `MinimumOrderQuantity` (bỏ `MinimumOrderQty`). `PurchaseUomId` chưa có cột `UnitId` tương ứng (GAP-C-07), tạm kiểm tra theo `UnitMeasure` |

### 4.4 Nguyên liệu – `#/scm-ingredient` (`Ingredient`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/scm/ingredients` | `ingredientCategoryId`, `storageType`, `status`, `q` | Danh sách + `availableQuantity` (Σ InventoryBalance) + `belowMin` (bool) | Dùng `IngredientCategoryId`, `BaseUnitId` (bỏ `CategoryId`, `BaseUomId`) |
| POST/PUT | `/scm/ingredients` | DTO | DTO | `YieldPercent` trong (0, 100\]; `StandardWastePercent` trong \[0, 100); `ShelfLifeDays` > 0 khi `IsExpiryManaged = 1` |

### 4.5 Quy đổi đơn vị – `#/scm-uom-conversion` (`UomConversion`)

CRUD chuẩn tại `/scm/uom-conversions`. Dùng `FromUnitId`, `ToUnitId` (PO xác nhận `UnitId → UnitMeasure`). Validate `ConversionFactor > 0`, `FromUnitId ≠ ToUnitId`, không trùng cặp đang hiệu lực. Bổ sung service nội bộ `UomService.Convert(qty, fromUnitId, toUnitId)` (tìm chiều xuôi, chiều ngược thì chia hệ số). MRP và xuất kho dùng service này. Không tìm được quy đổi → 422 `UOM_CONVERSION_MISSING`.

### 4.6 Kho và chính sách tồn – `#/scm-warehouse`, `#/scm-inv-policy`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/scm/warehouses/{id}/layout` | – | `{ warehouse, zones: [{ zone, temperatureMin, temperatureMax, locations: [] }] }` | Dựng từ `StorageZone` + `StorageLocation` (dùng `StorageZoneId`, bỏ `ZoneId`) |
| GET | `/scm/inventory-policies` | `warehouseId`, `ingredientId` | Danh sách chính sách | Dùng `MinimumStock`/`MaximumStock` (bỏ `MinStock`/`MaxStock`) |
| POST/PUT | `/scm/inventory-policies` | DTO | DTO | Validate `MinimumStock ≤ SafetyStock ≤ ReorderPoint ≤ MaximumStock`. Một dòng duy nhất cho cặp (`IngredientId`, `WarehouseId`) |

### 4.7 Ca làm việc và phương tiện

`/hr/shifts`: `StartTime`, `EndTime` kiểu `time`. Cho phép ca qua đêm (`EndTime < StartTime`, ví dụ Ca 4: 18:00–02:00). `/logistics/vehicles`: không cho gán xe `Status = Maintenance` vào tuyến (kiểm tra ở mục 10.2).

## 5. P2 – Món ăn, công thức và chính sách (module `menu`, `production`, `qc`)

CRUD chuẩn: `DishCategorie` (`/menu/dish-categories`), `ProductionLine` (`/production/lines`), `KitchenWorkstation` (`/production/workstations`), `FoodSafetyChecklist` + `FoodSafetyChecklistItem` (`/qc/checklists`, `/qc/checklists/{id}/items`). Riêng `FoodSafetyChecklist` có UNIQUE (`Code`, `VersionNo`); `FoodSafetyChecklistItem` có UNIQUE (`ChecklistId`, `ItemCode`).

### 5.1 Món ăn – `#/menu-dish` (`Dishe`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/menu/dishes` | `dishCategoryId`, `status`, `q` | `[{ id, code, name, dishCategoryId, standardCost, activeRecipe: { id, versionCode, status }, estimatedMaterialCost, status }]` | `activeRecipe` = `Recipe` cùng `DishId` có `IsActive = 1`. `estimatedMaterialCost` tính như mục 5.2 (bước 6). Dùng `DishCategoryId` (bỏ `CategoryId`) |
| POST/PUT | `/menu/dishes` | DTO | DTO | `DefaultUnitId` → UnitMeasure (PO xác nhận) |

### 5.2 Công thức và phiên bản – `#/menu-recipe` (`Recipe`, `RecipeLine`)

Không tạo bảng RecipeVersion. Mỗi phiên bản là một dòng `Recipe` cùng `DishId`, phân biệt bằng `VersionNo`.

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/menu/dishes/{dishId}/recipes` | – | Danh sách phiên bản, mới nhất trước, kèm `lines[]` và `costPerMeal` | Sắp theo `VersionNo` giảm dần |
| POST | `/menu/dishes/{dishId}/recipes` | `{ copyFromRecipeId? }` | Recipe nháp mới | `VersionNo` = max + 1, `VersionCode` = `{Dishe.Code}-V{n}`, `Status = Draft`, `IsActive = 0`. Sao chép `RecipeLine` từ phiên bản nguồn. Chặn nếu món đã có bản Draft/PendingApproval |
| POST | `/menu/recipes/{id}/lines` | `{ ingredientId, quantity, unitId, wastePercent, sequenceNo, preparationNote }` | RecipeLine | Chỉ khi Recipe ở Draft. `quantity` > 0, là định lượng cho 100 suất (theo `StandardOutputQty`). Dùng `UnitId` (bỏ `UomId`) |
| PUT/DELETE | `/menu/recipe-lines/{id}` | DTO | DTO / 204 | Chỉ khi Recipe ở Draft |
| POST | `/menu/recipes/{id}/submit` | – | Recipe | Phải có ≥ 1 dòng. Gọi ApprovalService (RECIPE\_VERSION). Status → PendingApproval |
| POST | `/menu/recipes/{id}/lock` | `{ effectiveFrom }` | Recipe | Chỉ khi Status = Approved. Transaction (bước 1–5 bên dưới) |
| GET | `/menu/recipes/{id}/cost` | `mealCount` (mặc định 100) | `{ lines: [{ ingredientId, quantity, unitPrice, amount }], totalMaterialCost, costPerMeal }` | Bước 6 bên dưới |

Xử lý khóa phiên bản (`lock`):

1. Các Recipe cùng `DishId` đang `IsActive = 1` → `IsActive = 0`, `Status = Obsolete`, `EffectiveTo` = `effectiveFrom` − 1 ngày.
2. Recipe hiện tại → `Status = Locked`, `IsActive = 1`, `EffectiveFrom`, `LockedAt` = now, `LockedBy` = uid.
3. `SnapshotJson` = JSON toàn bộ RecipeLine (ingredientId, quantity, unitId, wastePercent) để truy vết.
4. Tạo/cập nhật `StandardCost` cho (`DishId`, `RecipeId`): `MaterialCost` theo bước 6, `LaborCost` và `OverheadCost` theo tham số cấu hình (PROPOSED), `TotalCost`, `CostPerServing`; dòng StandardCost cũ → `Status = Inactive`.
5. Cập nhật `Dishe.StandardCost` = `CostPerServing`.
6. Chi phí NVL/suất = Σ (`RecipeLine.Quantity` × giá NCC ưu tiên `SupplierMaterial.UnitPrice` đang hiệu lực) ÷ 100. Khác đơn vị → quy đổi qua UomService.

### 5.3 Giá thành chuẩn – `#/cost-standard` (`StandardCost`)

Chỉ đọc và tính lại: `GET /production/standard-costs?dishId=&status=`, `POST /production/standard-costs/recalculate` `{ dishIds?: [] }` chạy lại bước 4–5 với giá NCC hiện hành. Không cho sửa tay.

### 5.4 Chính sách giá – `#/crm-price-policy` (`PricePolicie`)

CRUD chuẩn tại `/sales/price-policies`. Validate: không chồng hiệu lực cùng (`CustomerId`, `ContractId`, `MealType`); `UnitPrice` > 0. Bổ sung `GET /sales/price-policies/resolve?customerId=&contractId=&mealType=&date=` trả đơn giá áp dụng, dùng khi tạo dòng đơn hàng. Thứ tự ưu tiên (PROPOSED): PricePolicie theo hợp đồng → PricePolicie theo khách hàng → `ContractMeal.UnitPrice`.

## 6. P3 – Hợp đồng, thực đơn và đơn hàng (module `sales`, `menu`)

CRUD chuẩn: `ContractMeal` (`/sales/contracts/{id}/meals`), `MenuLine` (`/menu/menus/{id}/lines`). Dùng `ContractNumber`, `OrderNumber`, `LineAmount` (bỏ `ContractNo`, `OrderNo`, `Amount`).

### 6.1 Hợp đồng – `#/crm-contract` (`Contract`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/sales/contracts` | `{ customerId, name, signedDate, effectiveFrom, effectiveTo, paymentTerm, contractValue, meals: [{ mealType, mealName, serviceType, unitPrice, unitId }] }` | Contract + meals | Transaction tạo `Contract` (`Status = Draft`, số `ContractNumber` tự sinh) và các `ContractMeal`. `EffectiveFrom ≤ EffectiveTo`. Mỗi `MealType` xuất hiện 1 lần |
| POST | `/sales/contracts/{id}/submit` | – | Contract | Chỉ Draft. ApprovalService (CONTRACT, 1 cấp Ban giám đốc). Status → PendingApproval |
| POST | `/sales/contracts/{id}/terminate` | `{ reason, effectiveTo }` | Contract | Chỉ Active. Status → Terminated. Chặn nếu còn đơn hàng chưa giao sau `effectiveTo` |
| Job | `ContractExpiryJob` (hằng ngày 00:30) | – | – | Active có `EffectiveTo` < hôm nay → Expired. Gửi cảnh báo trước 30 ngày |

### 6.2 Thực đơn dạng lịch tuần – `#/menu-menu` (`Menu`, `MenuLine`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/menu/menus` | `status`, `effectiveOn` | Danh sách + `lineCount`, `assignedCustomerCount`, `orderedMeals` | `orderedMeals` = Σ `OrderLine.Quantity` có `MenuId` này |
| GET | `/menu/menus/{id}/calendar` | `mealType` | `{ days: [1..7], categories: [{ dishCategoryId, name, cells: { "1": [{ menuLineId, dishId, dishName }], … } }] }` | Pivot `MenuLine` theo `DayOfWeek` (1 = Thứ 2 … 7 = CN) × `Dishe.DishCategoryId` |
| POST | `/menu/menus/{id}/lines` | `{ dishId, mealType, dayOfWeek, sequenceNo, portionQty, isRequired }` | MenuLine | Chỉ khi Menu ở Draft hoặc Approved. `dayOfWeek` 1–7. Món phải có công thức hiệu lực, không có → cảnh báo (không chặn) |
| POST | `/menu/menus/{id}/approve` · `/publish` · `/expire` | – | Menu | Draft → Approved → Published → Expired. Publish: kiểm tra mỗi ngày trong hiệu lực có ≥ 1 món cho mỗi `MealType` đã được gán; đẩy tin qua Zalo cho khách được gán |
| POST | `/menu/menus/{id}/clone` | `{ effectiveFrom, effectiveTo, code }` | Menu nháp mới | Sao chép toàn bộ MenuLine, `VersionNo` = 1 |

### 6.3 Phân công thực đơn – `#/menu-assignment` (`MenuAssignment`)

CRUD chuẩn tại `/menu/assignments`. Quy tắc BRD: thực đơn **chỉ áp dụng khi được gán**. Validate: hợp đồng thuộc đúng khách hàng; `MealType` có trong `ContractMeal` của hợp đồng; không chồng hiệu lực cùng (`CustomerId`, `DeliveryLocationId`, `MealType`). Bổ sung `GET /menu/assignments/resolve?customerId=&deliveryLocationId=&mealType=&date=` trả `MenuId` áp dụng (ưu tiên cấp điểm giao, sau đó cấp khách hàng). "Thực đơn theo đối tượng" thể hiện qua `MealType` (ObjectMenu không dùng cho việc này).

### 6.4 Yêu cầu đổi món – `#/menu-change-request` (`MenuChangeRequest`)

`POST /menu/change-requests` `{ orderId, menuId, originalDishId, requestedDishId, mealDate, shiftId, quantity, reason }` → `RequestNo` tự sinh, `RequestStatus = Pending`, `RequestedBy` = uid, gọi ApprovalService (MENU\_CHANGE). Khi duyệt: ghi `ApprovedBy/ApprovedAt`; nếu đã có `ProductionPlan` cho ngày/ca đó ở Draft thì cập nhật `ProductionPlanLine` (bớt món cũ, thêm món mới theo `quantity`); nếu đã Released thì chỉ cảnh báo bếp trưởng. `OriginalDishId`/`RequestedDishId` → Dishe là quan hệ chưa xác nhận.

### 6.5 Dự báo – `#/crm-forecast` (`Forecast`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/sales/forecasts/matrix` | `from`, `to`, `branchId` | `{ dates: [], rows: [{ customerId, customerName, values: { "2026-11-15": 3010, … } }], totals: {} }` | Σ `ForecastQuantity` theo khách × ngày, chỉ bản Published với `VersionNo` lớn nhất |
| POST | `/sales/forecasts/bulk` | `[{ customerId, deliveryLocationId, forecastDate, shiftId, mealType, forecastQuantity }]` | `{ created, versionNo }` | Dự báo mới cho cùng (khách, điểm, ngày, ca, loại suất) → `VersionNo` + 1, bản cũ → Superseded. Dùng `ForecastQuantity` (bỏ `Quantity`) |
| POST | `/sales/forecasts/publish` | `{ from, to, customerIds? }` | `{ published }` | Draft → Published |

### 6.6 Đơn hàng – `#/crm-order` (`Order`, `OrderLine`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/sales/orders` | `mealDate`, `mealDateFrom/To`, `status`, `customerId`, `branchId`, `q`, `view=board\|list` | List; với `view=board` trả `{ columns: [{ status, items[] }] }` + `statusCounts` | Kanban theo 8 trạng thái. Lọc đơn vị theo `Customer.BranchId` |
| GET | `/sales/orders/{id}` | – | `{ order, customer, contract, lines: [{ …, forecastQuantity, locationName }], deliveryOrders[], reconciliation, allowedActions: ["submit","cancel"] }` | `allowedActions` tính theo state machine + quyền. `forecastQuantity` lấy từ Forecast cùng khách/ngày/loại suất |
| GET | `/sales/orders/new-template` | `customerId`, `mealDate` | `{ contract, lines: [{ contractMealId, mealType, mealName, deliveryLocationId, locationName, shiftId, unitPrice, suggestedQuantity }] }` | Dựng form tạo đơn: hợp đồng Active hiệu lực tại `mealDate` (không có → 422 `NO_ACTIVE_CONTRACT`), `ContractMeal` × điểm giao, ca mặc định theo loại suất (cấu hình), đơn giá qua price-policy resolve, số lượng gợi ý = dự báo chia theo điểm |
| POST | `/sales/orders` | `{ customerId, contractId, deliveryLocationId, mealDate, deliveryDate, note, lines: [{ contractMealId, mealType, shiftId, quantity, deliveryLocationId }] }` | Order (201) | Transaction: sinh `OrderNumber` = `DH-yyMMdd-NN`, `Status = Draft`, `OrderDate` = now. Mỗi dòng: `MenuId` = assignments/resolve, `UnitPrice` = price resolve, `LineAmount` = Quantity × UnitPrice. Tổng hợp `TotalQuantity`, `TotalAmount`. Bỏ dòng quantity = 0 |
| PUT | `/sales/orders/{id}/lines` | `[{ id, quantity }]` | Order | Chỉ Draft/Submitted. Tính lại LineAmount và tổng |
| POST | `/sales/orders/{id}/submit` | – | Order | Draft → Submitted. Điều kiện: ≥ 1 dòng, mọi dòng có `ShiftId` và `MealType`, `Quantity` > 0 |
| POST | `/sales/orders/{id}/confirm` | `{ note }` | Order + `forecastVariancePercent` | Submitted → Confirmed. Ghi nhận lệch dự báo > 10% vào audit (cảnh báo, không chặn). BRD: chỉ đơn Confirmed mới vào kế hoạch sản xuất |
| POST | `/sales/orders/{id}/lock` | – | Order | Confirmed → Locked. GAP-F-07: chưa có cột giờ chốt; tạm cấu hình giờ chốt theo ca. Job `OrderCutoffJob` tự khóa đơn Confirmed khi đến giờ |
| POST | `/sales/orders/{id}/cancel` | `{ reason }` (bắt buộc) | Order | Chỉ Draft/Submitted/Confirmed. Ghi `Note`. Locked trở đi phải qua yêu cầu đổi món/phê duyệt |
| Event | (nội bộ) | – | – | `ProductionPlan` Released → đơn liên quan Locked → InProduction. Mọi `DeliveryOrder` của đơn DELIVERED → Order Delivered. Đối soát đóng → Reconciled |

### 6.7 Đối soát đơn hàng – `#/crm-reconciliation` (`OrderReconciliation`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/sales/reconciliations` | `from`, `to`, `status`, `customerId` | Danh sách + `chart: [{ date, ordered, delivered, accepted }]` | Biểu đồ Đặt – Giao – Nhận theo ngày ăn |
| POST | `/sales/orders/{id}/reconcile` | – | OrderReconciliation | Chỉ khi Order Delivered. Tính: `OrderedQuantity` = Order.TotalQuantity; `ProducedQty` = Σ suất sản xuất phân bổ (PROPOSED); `PackedQty` = Σ `PackingResult.PackedQuantity`; `DeliveredQuantity`, `AcceptedQuantity` = Σ `ProofOfDeliverie`; `VarianceQty` = Accepted − Ordered; `VariancePercent`; `FinalAmount` = TotalAmount × Accepted ÷ Ordered (PROPOSED); `ProofOfDeliveryId` = POD đầu tiên. `Status = Matched` nếu VarianceQty = 0, ngược lại `Variance` |
| POST | `/sales/reconciliations/{id}/close` | `{ remark }` | OrderReconciliation | Matched/Variance → Closed; Order → Reconciled. Variance vượt ngưỡng (GAP-F-11, tạm 1%) cần `remark` |

## 7. P4 – Mua hàng và kho (module `scm`)

Nguyên tắc BRD: `InventoryBalance` được tính từ sổ cái `InventoryTransaction`, **không có API sửa trực tiếp tồn**. Mọi biến động kho đi qua service nội bộ `InventoryLedgerService.Post(txn)`. Service này trong cùng transaction: (1) ghi `InventoryTransaction` (`TransactionNumber`, `TransactionType`, `ReferenceType`/`ReferenceId`, `Quantity` có dấu, `QuantityIn`/`QuantityOut`, `MaterialLotId`, `UnitId`, `UnitCost`); (2) upsert `InventoryBalance` theo (`WarehouseId`, `StorageLocationId`, `IngredientId`, `MaterialLotId`): cập nhật `OnHandQuantity`, `AvailableQuantity` = OnHand − Reserved, `LastCalculatedDate`; (3) chặn tồn âm → 422 `INSUFFICIENT_STOCK`. Loại giao dịch theo BRD: RECEIPT, ISSUE, TRANSFER\_IN, TRANSFER\_OUT, ADJUSTMENT, WASTE, RETURN.

### 7.1 Nhu cầu vật tư (MRP) – `#/scm-requirement` (`MaterialRequirement`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/scm/material-requirements` | `productionPlanId` (bắt buộc) | `{ plan, dishCount, totalMeals, items: [{ ingredientId, requiredQuantity, availableQuantity, expectedIncomingQuantity, purchaseQuantity, unitId, coveragePercent, status }] }` | `coveragePercent` = min(100, Available ÷ Required × 100) |
| POST | `/scm/material-requirements/calculate` | `{ productionPlanId }` | Kết quả như trên | Thuật toán bên dưới. Upsert theo (`ProductionPlanId`, `IngredientId`); dòng đã `Requested` giữ nguyên trạng thái |
| POST | `/scm/material-requirements/create-purchase-request` | `{ productionPlanId, requirementIds?: [], bufferPercent: 5 }` | PurchaseRequest | Lấy các dòng `Status = Shortage`. Tạo `PurchaseRequest` (`RequestNumber` tự sinh, `RequiredDate` = ngày sản xuất, `RequestedByEmployeeId` = nhân viên của user) + `PurchaseRequestLine` (`Quantity` = ceil(PurchaseQuantity × (1 + buffer))). Dòng nhu cầu → `Requested`. Gọi ApprovalService (PURCHASE\_REQUEST, 2 cấp) |

Thuật toán tính nhu cầu (PROPOSED – BA/PO CONFIRM):

1. Lấy `ProductionPlanLine` của kế hoạch (`DishId`, `PlannedQuantity`).
2. Với mỗi món, lấy `Recipe` có `IsActive = 1` và hiệu lực tại `ProductionDate`. Không có → trả cảnh báo `NO_ACTIVE_RECIPE` kèm tên món, bỏ qua món đó.
3. Nhu cầu gộp theo nguyên liệu = Σ (RecipeLine.Quantity × PlannedQuantity ÷ Recipe.StandardOutputQty) ÷ (Ingredient.YieldPercent ÷ 100), quy đổi về `Ingredient.BaseUnitId`.
4. `AvailableQuantity` = Σ `InventoryBalance.AvailableQuantity` của các lô `MaterialLot.QcStatus = PASS`, `Status = Available`, chưa hết hạn tại ngày sản xuất.
5. `ExpectedIncomingQuantity` = Σ `PurchaseOrderLine.Quantity` của PO Approved/Sent/PartiallyReceived có `ExpectedDeliveryDate` ≤ ngày sản xuất, trừ phần đã nhận.
6. `PurchaseQuantity` = max(0, Nhu cầu + `InventoryPolicie.SafetyStock` − Available − Incoming). `Status` = Shortage nếu > 0, ngược lại Sufficient.

### 7.2 Đề nghị mua – `#/scm-pr` (`PurchaseRequest`, `PurchaseRequestLine`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/scm/purchase-requests` | `status`, `from`, `to`, `q` | Danh sách + `lineCount`, `estimatedAmount` | `estimatedAmount` = Σ Quantity × giá NCC ưu tiên |
| POST | `/scm/purchase-requests` | `{ requiredDate, note, lines: [{ ingredientId, quantity, unitId, requiredDate }] }` | PR (Draft) | Dùng `RequestNumber`, `RequestedByEmployeeId` (bỏ `RequestNo`, `RequesterId`) |
| POST | `/scm/purchase-requests/{id}/submit` | – | PR | Draft → PendingApproval, ApprovalService 2 cấp |
| POST | `/scm/purchase-requests/{id}/convert-to-po` | `{ groupBy: "preferredSupplier" }` | `[PurchaseOrder]` | Chỉ Approved. Nhóm dòng theo `SupplierMaterial.IsPreferred = 1`; mỗi NCC một `PurchaseOrder` (Draft, `PurchaseRequestId`, `ExpectedDeliveryDate` = RequiredDate − LeadTimeDays). `UnitPrice` từ SupplierMaterial, `LineAmount`, `TotalAmount`. Cảnh báo nếu Quantity < `MinimumOrderQuantity`. Nguyên liệu chưa có NCC ưu tiên → 422 kèm danh sách. PR → Converted |

### 7.3 Đơn mua – `#/scm-po` (`PurchaseOrder`, `PurchaseOrderLine`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/scm/purchase-orders/{id}/approve` | – | PO | Draft → Approved (hạn mức duyệt PO chờ BA chốt). Dùng `PurchaseOrderNumber`, `ExpectedDeliveryDate` (bỏ `PONo`, `ExpectedDate`) |
| POST | `/scm/purchase-orders/{id}/send` | `{ channel: "email\|zalo" }` | PO | Approved → Sent. Xuất PDF, gửi NCC (ghi `OutboundMessage` nếu qua Bot) |
| POST | `/scm/purchase-orders/{id}/receive` | `{ warehouseId, receiptDate, lines: [{ purchaseOrderLineId, receivedQuantity, lotNumber, manufactureDate, expiryDate }] }` | `{ goodsReceipt, qcInspection }` | Chỉ Sent/PartiallyReceived. Transaction: tạo `GoodsReceipt` (`Status = QCPending`, `ReceiptNumber` tự sinh) + `GoodsReceiptLine` (`ReceivedQuantity`, Accepted/Rejected = 0, `UnitCost` = PO UnitPrice, `ExpiryDate` mặc định = nhận + ShelfLifeDays) + `QCInspection` + `QCInspectionLine` cho từng dòng (Result rỗng). PO → PartiallyReceived, hoặc Received khi nhận đủ. Nguyên liệu `QcRequired = 0` → tự đặt PASS |
| POST | `/scm/purchase-orders/{id}/close` · `/cancel` | `{ reason }` | PO | Close khi đã nhận; Cancel khi chưa nhận dòng nào |

### 7.4 Nhập kho có cổng QC – `#/scm-gr` (`GoodsReceipt`, `GoodsReceiptLine`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/scm/goods-receipts` | `status`, `supplierId`, `from`, `to` | Danh sách + `qcResult` (từ QCInspection) | – |
| POST | `/scm/goods-receipts/{id}/post` | – | `{ goodsReceipt, lots: [], transactions: [] }` | **Cổng QC**: `QCInspection.Result` rỗng → 409 `QC_NOT_CONCLUDED`; = REJECT → 422 `QC_REJECTED`. Chỉ các dòng có `QCInspectionLine.Result = PASS` được nhập. Với mỗi dòng: `AcceptedQuantity` = ReceivedQuantity; tạo `MaterialLot` (`LotNumber`, `IngredientId`, `SupplierId`, `GoodsReceiptLineId`, `ManufactureDate`, `ExpiryDate`, `ReceivedDate`, `UnitCost`, `QcStatus = PASS`, `Status = Available`); `InventoryLedgerService.Post(RECEIPT)` vào vị trí theo `Ingredient.StorageType`. Dòng HOLD: tạo lô `QcStatus = HOLD`, `Status = OnHold`, không tính vào khả dụng. GR → Posted. Dùng `ReceiptNumber` (bỏ `ReceiptNo`) |

### 7.5 Lô và truy xuất nguồn gốc – `#/scm-lot` (`MaterialLot`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/scm/lots` | `ingredientId`, `status`, `qcStatus`, `expiringWithinDays` | Danh sách | Dùng `LotNumber`, `QcStatus` (bỏ `LotNo`, `QualityStatus`) |
| GET | `/scm/lots/{id}/trace` | – | `{ supplier, purchaseOrder, goodsReceipt, qcLine, lot, issues[], productionOrders[], batches[], packingOrders[], deliveryOrders[], customers[] }` | Chuỗi theo quan hệ logic: Lot → GoodsReceiptLine → GoodsReceipt → PurchaseOrder → Supplier; Lot → MaterialIssueLine → MaterialIssue → ProductionOrder → ProductionBatche → PackingOrder → Order → DeliveryOrder → Customer. GAP-F-10: chưa có mã QR |
| POST | `/scm/lots/{id}/hold` · `/release` | `{ reason }` | Lot | Available ⇄ OnHold; khi giữ, `AvailableQuantity` của lô về 0 |
| Job | `LotExpiryJob` (hằng ngày 00:15) | – | – | Lô Available có `ExpiryDate` < hôm nay → Expired, ledger WASTE toàn bộ tồn với `Reason` = hết hạn. Lô còn ≤ `ExpiryWarningDays` → cảnh báo |

### 7.6 Tồn kho – `#/scm-inv-balance`, `#/scm-inv-txn`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/scm/inventory/summary` | `warehouseId`, `ingredientCategoryId`, `belowMin=true` | `[{ ingredientId, code, name, onHand, reserved, available, unitId, minimumStock, safetyStock, reorderPoint, lotCount, expiringLots, level: "critical\|reorder\|ok" }]` + KPI | Group `InventoryBalance` theo nguyên liệu, join `InventoryPolicie`. Chỉ đọc |
| GET | `/scm/inventory/by-lot` | `ingredientId` | `[{ lot, warehouse, location, onHand, reserved, available, expiryDate, qcStatus, fefoRank }]` | Sắp theo `ExpiryDate` tăng dần (FEFO) |
| GET | `/scm/inventory-transactions` | `transactionType`, `ingredientId`, `warehouseId`, `referenceType`, `from`, `to` | Sổ cái | Dùng `TransactionNumber`, `MaterialLotId`, `UnitId`. Chỉ đọc |
| POST | `/scm/inventory/rebuild-balance` | `{ warehouseId? }` | `{ rows }` | Quản trị: tính lại toàn bộ `InventoryBalance` từ sổ cái, ghi `AnalyticsRefreshLog` |

GAP-F-03 và GAP-F-04: chưa có bảng phiếu chuyển kho và phiếu điều chỉnh. Nếu BA chấp nhận phương án không thêm bảng: `POST /scm/inventory/transfer` `{ fromWarehouseId, fromLocationId, toWarehouseId, toLocationId, lines: [{ materialLotId, quantity }], reason }` ghi cặp TRANSFER\_OUT/TRANSFER\_IN cùng `ReferenceType = 'Transfer'`, `ReferenceId` = GUID mới; `POST /scm/inventory/adjust` ghi ADJUSTMENT sau khi phê duyệt STOCK\_ADJUSTMENT.

### 7.7 Đặt trước và xuất kho FEFO – `#/scm-reservation`, `#/scm-issue`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/scm/reservations` | `productionOrderId`, `status` | Danh sách | Dùng `ReservedQuantity` (bỏ `ReservedQty`) |
| GET | `/scm/material-issues/suggest` | `productionOrderId` | `[{ reservationId, ingredientId, reservedQuantity, picks: [{ materialLotId, lotNumber, expiryDate, quantity }], shortQuantity }]` | FEFO khi `InventoryPolicie.UseFefo = 1`: lấy lô PASS/Available theo `ExpiryDate` tăng dần cho đến đủ số lượng |
| POST | `/scm/material-issues` | `{ productionOrderId, warehouseId, lines: [{ ingredientId, materialLotId, quantity, unitId }] }` | MaterialIssue | Transaction: tạo `MaterialIssue` (`IssueNumber`, `Status = Issued`) + `MaterialIssueLine`; ledger ISSUE từng dòng; giảm `ReservedQuantity` trên balance; `MaterialReservation` → Issued/PartiallyIssued. Lệnh SX Released → InProgress. Xuất lô không phải FEFO đầu tiên → cảnh báo, yêu cầu `note` |

### 7.8 Kiểm kê – `#/scm-stockcount` (`StockCount`, `StockCountLine`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/scm/stock-counts` | `{ warehouseId, countType: DAILY\|WEEKLY\|MONTHLY\|ADHOC, countDate, zoneId?, responsibleEmployeeId }` | StockCount + lines | Phiếu Draft, sinh `StockCountLine` từ `InventoryBalance` hiện tại (`SystemQuantity` = OnHand, theo lô). Dùng `CountNumber` |
| PUT | `/scm/stock-counts/{id}/lines` | `[{ id, physicalQuantity, varianceReason }]` | Lines | Status → Counting. `VarianceQuantity` = Physical − System; `VarianceValue` = Variance × UnitCost của lô. % chênh lệch tính khi trả về (GAP-C-06: không có cột) |
| POST | `/scm/stock-counts/{id}/submit` | – | StockCount | Mọi dòng có Variance ≠ 0 phải có `VarianceReason`. Status → PendingApproval, ApprovalService (STOCK\_COUNT) |
| POST | `/scm/stock-counts/{id}/post` | – | `{ transactions: [] }` | Chỉ khi đã có `ApprovedBy`. Mỗi dòng lệch → ledger ADJUSTMENT (`ReferenceType = 'StockCount'`, `Reason` = VarianceReason). Status → Posted |

### 7.9 Chênh lệch tiêu hao – `#/scm-consumption-var` (`MaterialConsumptionVariance`)

Chỉ đọc tại `GET /scm/consumption-variances?productionOrderId=&from=&to=`. Dữ liệu sinh khi đóng lệnh sản xuất (mục 8.3): `StandardQuantity` = định mức theo công thức × sản lượng thực tế, `ActualQuantity` = Σ MaterialIssueLine, `VarianceQuantity`, `VariancePercent`, `VarianceCost`. Dùng cột mới (bỏ `ActualQty`, `VarianceQty`).

## 8. P5 – Kế hoạch sản xuất và giá thành (module `production`)

Số lượng ở kế hoạch, lệnh và mẻ tính theo **phần món** (suất × số món trong suất), không phải số suất. Dùng cột mới: `PlanNumber`, `PlannedQuantity`, `ProductionOrderNumber`, `BatchNumber`, `ActualQuantity`, `StartDate`/`EndDate`, `QcStatus`, `ProducedQuantity`.

### 8.1 Kế hoạch sản xuất – `#/prod-plan` (`ProductionPlan`, `ProductionPlanLine`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/production/plans` | `productionDate`, `from`, `to`, `shiftId`, `status`, `organizationUnitId` | Danh sách + `dishCount`, `totalPortions`, `orderCount` | – |
| POST | `/production/plans/generate` | `{ productionDate, shiftIds?: [], organizationUnitId }` | `[ProductionPlan]` | Mỗi ca một kế hoạch Draft. Nguồn: `OrderLine` của đơn **Confirmed/Locked** có `MealDate` = ngày, `ShiftId` = ca. Với mỗi dòng đơn, lấy danh sách món từ `MenuLine` (MenuId của dòng, `DayOfWeek` của ngày, `MealType` của dòng) → cộng dồn `PlannedQuantity` theo món. Ghi `ProductionPlanLine` (`DishId`, `ShiftId`, `PlannedQuantity`, `OrderLineId` = dòng đơn đầu tiên, `Status = Draft`). Đã có kế hoạch Draft cùng ngày/ca → tính lại; đã Approved trở đi → 409. Áp dụng `MenuChangeRequest` Approved của ngày đó |
| PUT | `/production/plans/{id}/lines` | `[{ id?, dishId, plannedQuantity }]` | Plan | Chỉ Draft. Cho phép cộng thêm số dự phòng |
| POST | `/production/plans/{id}/approve` | – | Plan | Draft → Approved. Tự gọi MRP calculate (mục 7.1) |
| POST | `/production/plans/{id}/release` | – | `{ plan, productionOrders[], batches[], reservations[] }` | Chỉ Approved và chưa có lệnh. Thuật toán bên dưới. Plan → Released; đơn hàng nguồn Locked → InProduction |

Thuật toán phát hành (PROPOSED):

1. Mỗi `ProductionPlanLine` → một `ProductionOrder`: `ProductionOrderNumber` tự sinh, `RecipeId` = công thức `IsActive` (không có → 422 `NO_ACTIVE_RECIPE`), `ShiftId`, `PlannedQty`, `Status = Released`, `PlannedStartDate` = ngày + giờ bắt đầu ca, `RecipeSnapshotJson` = `Recipe.SnapshotJson`.
2. `ProductionLineId`: ánh xạ theo nhóm món (cấu hình DishCategorie → ProductionLine). GAP-C-04: header kế hoạch không có dây chuyền.
3. Chia mẻ: số mẻ = ceil(PlannedQty ÷ công suất mẻ). Công suất = `ProductionLine.CapacityMealPerBatch` (hệ số quy đổi phần món/mẻ chờ BA xác nhận). Mỗi mẻ một `ProductionBatche` (`BatchNumber`, `PlannedQuantity`, `RecipeId`, `DishId`, `KitchenWorkstationId` theo cấu hình, `Status = Planned`).
4. Giữ chỗ: với mỗi `RecipeLine`, `RequiredQty` = Quantity × PlannedQty ÷ StandardOutputQty ÷ Yield. Tạo `MaterialReservation` (`ReservedQuantity` = min(Required, khả dụng), `Status = Reserved`) và tăng `InventoryBalance.ReservedQuantity` theo FEFO. Thiếu → vẫn phát hành, trả danh sách thiếu để cảnh báo.

### 8.2 Lệnh sản xuất – `#/prod-order` (`ProductionOrder`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/production/orders` | `productionDate`, `shiftId`, `productionLineId`, `status` | Danh sách + `batchCount`, `doneQuantity`, `reservationStatus` | – |
| GET | `/production/orders/{id}` | – | `{ order, recipeSnapshot, batches[], reservations[], issues[], outputs[], actualCost }` | – |
| POST | `/production/orders/{id}/complete` | – | Order | InProgress → Completed khi mọi mẻ Passed/Packed/Failed. `ActualQty` = Σ `ProductionBatche.ActualQuantity` mẻ đạt; `ActualEndDate` = now |
| POST | `/production/orders/{id}/close` | – | `{ order, actualCost, variances[] }` | Completed → Closed. Sinh `MaterialConsumptionVariance` (mục 7.9) và `ActualCost` (mục 8.5). Giải phóng giữ chỗ còn dư (Status Released) |
| POST | `/production/orders/{id}/cancel` | `{ reason }` | Order | Chỉ Released (chưa xuất kho). Giải phóng giữ chỗ, mẻ → hủy |

### 8.3 Bảng điều độ mẻ – `#/prod-batch` (`ProductionBatche`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/production/batch-board` | `productionDate` (bắt buộc), `organizationUnitId` | `{ shifts: [], lines: [], cells: [{ shiftId, productionLineId, batches: [{ id, batchNumber, dishName, plannedQuantity, actualQuantity, status, qcStatus }] }], statusCounts, kpi: { batchCount, plannedPortions, passedPortions, holdOrRejectCount } }` | Lưới ca × dây chuyền. Mẻ thuộc ô theo `ProductionOrder.ShiftId` và `ProductionOrder.ProductionLineId` |
| GET | `/production/batches/{id}` | – | `{ batch, productionOrder, recipe, temperatureLogs[], checklistResults[], outputs[] }` | Drawer chi tiết mẻ |
| POST | `/production/batches/{id}/start` | – | Batch | Planned → Cooking, `StartDate` = now. Lệnh Released → InProgress. Cảnh báo nếu chưa xuất kho cho lệnh |
| POST | `/production/batches/{id}/finish` | `{ actualQuantity, coreTemperatureC, deviceCode }` | Batch + TemperatureLog | Cooking → QCPending. Ghi `ActualQuantity`, `EndDate`. Tạo `TemperatureLog` (`MeasurementType = CORE_TEMP`, `MinAllowedC = 75`, `IsWithinRange`). < 75°C → vẫn lưu, cảnh báo QC |
| POST | `/production/batches/{id}/qc` | `{ result: PASS\|HOLD\|REJECT, note }` | `{ batch, output, wasteRecord? }` | Chỉ QCPending. PASS bị chặn (422 `CORE_TEMP_OUT_OF_RANGE`) nếu có TemperatureLog CORE\_TEMP ngoài ngưỡng chưa đo lại đạt. PASS → Passed, `AcceptedQty` = Actual. HOLD → giữ QCPending, `note` bắt buộc. REJECT → Failed, `RejectedQty` = Actual, tạo `MealWasteRecord` (`WasteType = QC_REJECT`). Mọi trường hợp tạo `ProductionOutput` (`ProducedQuantity`, `AcceptedQuantity`, `RejectedQuantity`, `QualityStatus`) |
| POST | `/production/batches/{id}/create-packing-order` | `{ orderId?, plannedQty? }` | PackingOrder | Chỉ Passed. BRD: PackingOrder sinh từ mẻ. Batch → Packed |

### 8.4 Kết quả sản xuất và hao hụt – `#/prod-output`, `#/prod-waste`

- `GET /production/outputs?productionDate=&dishId=` – chỉ đọc; dữ liệu sinh từ bước QC mẻ.
- `POST /production/waste-records` `{ productionBatchId?, productionOrderId?, dishId, shiftId, wasteType, quantityMeals, quantityKg, reason, disposition }` → `OccurredAt` = now, `RecordedBy` = uid. `WasteType` lấy từ reference-lists (GAP-F-06). Cập nhật `ProductionBatche.WasteQty` nếu có mẻ.
- `GET /production/waste-records/summary?from=&to=&groupBy=wasteType|dish|shift` cho biểu đồ hao hụt.

### 8.5 Giá thành thực tế và giá thành/suất – `#/cost-actual`, `#/cost-meal`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/production/actual-costs` | `from`, `to`, `dishId` | Danh sách + `breakdown: { material, labor, overhead, waste, other }`, `costPerMeal`, `standardCostPerMeal` | Biểu đồ cơ cấu giá thành |
| POST | `/production/orders/{id}/calculate-cost` | – | ActualCost | Gọi khi đóng lệnh. `MaterialCost` = Σ (MaterialIssueLine.Quantity × MaterialLot.UnitCost). `LaborCost` = giờ công ca × đơn giá (PROPOSED, chưa có bảng lương). `OverheadCost` = hệ số cấu hình × sản lượng. `WasteCost` = Σ MealWasteRecord × đơn giá chuẩn. `TotalCost` = tổng. GAP-C-01: Packaging, Logistics, Utility tạm gộp `OtherCost` |
| POST | `/production/meal-costs/calculate` | `{ orderId }` | `[MealCost]` | Sau đối soát. Theo từng `OrderLine`: `AcceptedMealQuantity` từ POD, `TotalActualCost` = phân bổ ActualCost của ngày theo tỷ lệ suất (PROPOSED), `CostPerMeal` = Total ÷ Accepted (BRD), `Revenue` = LineAmount, `GrossMargin` = Revenue − TotalActualCost |

## 9. P6 – QC, an toàn thực phẩm và vệ sinh (module `qc`)

Kết quả QC theo BRD: **PASS / HOLD / REJECT**. Dùng `InspectorEmployeeId`, `InspectionNumber`, `CleaningBatchNumber` (bỏ `InspectorId`, `CleaningNo`).

### 9.1 QC đầu vào – `#/qc-inspection` (`QCInspection`, `QCInspectionLine`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/qc/inspections` | `result` (rỗng = chưa kết luận), `from`, `to`, `supplierId` | Danh sách + `lineCounts: { PASS, HOLD, REJECT, pending }` | – |
| GET | `/qc/inspections/{id}` | – | `{ inspection, goodsReceipt, supplier, lines: [{ …, ingredient: { name, storageType, storageCondition }, suggestedResult }] }` | `suggestedResult` theo quy tắc nhiệt độ bên dưới |
| POST | `/qc/inspections/{id}/conclude` | `{ inspectorEmployeeId, lines: [{ id, temperature, weight, appearance, packaging, result, remark }], note }` | `{ inspection, goodsReceiptStatus, supplierIssue? }` | Mọi dòng phải có `result`. Dòng HOLD/REJECT bắt buộc `remark`. Cập nhật `QCInspectionLine`; cập nhật `GoodsReceiptLine.QualityStatus`, `AcceptedQuantity`, `RejectedQuantity`. Kết luận phiếu: tất cả PASS → PASS; tất cả REJECT → REJECT; có PASS → PASS (nhập một phần); còn lại → HOLD. GoodsReceipt → Accepted / PartiallyAccepted / Rejected. Có dòng HOLD/REJECT → tự tạo `SupplierIssue` (`IssueType = QUALITY` hoặc `TEMPERATURE`, `Severity` = High nếu có REJECT, `IssueStatus = Open`, `DueAt` = +2 ngày) |

Quy tắc gợi ý kết quả (PROPOSED – BA/PO CONFIRM): hàng `StorageType = CHILLED` có nhiệt độ > 5°C → HOLD; `FROZEN` > -18°C → REJECT; `ExpiryDate` còn < 50% `ShelfLifeDays` → HOLD. Người kiểm tra vẫn có quyền sửa kết quả.

### 9.2 Thực hiện checklist ATTP – `#/qc-checklist-result` (`FoodSafetyChecklistResult`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/qc/checklists/{id}/run` | `checkDate`, `shiftId`, `productionBatchId?` | `{ checklist, items: [{ item, result? }], passRate7d: [{ date, rate }] }` | Ghép `FoodSafetyChecklistItem` (theo `SortOrder`) với kết quả đã ghi cùng ngày/ca |
| POST | `/qc/checklists/{id}/results` | `{ checkDate, shiftId, productionBatchId?, workstationId?, items: [{ checklistItemId, resultCode: PASS\|FAIL, actualValue, correctiveAction, evidenceUrl }] }` | `{ saved, criticalFailures: [] }` | Upsert theo (`ChecklistId`, `ChecklistItemId`, `CheckDate`, `ShiftId`). Tiêu chí `IsRequired = 1` phải có kết quả. FAIL bắt buộc `correctiveAction`. FAIL ở tiêu chí `IsCritical = 1` kèm `productionBatchId` → mẻ đó về QCPending và gửi cảnh báo Telegram. `CheckedBy` = uid, `CheckedAt` = now. `ChecklistId`, `ChecklistItemId`, `WorkstationId` là quan hệ đề xuất (chưa xác nhận) |

### 9.3 Nhật ký nhiệt độ – `#/qc-temperature` (`TemperatureLog`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/qc/temperature-logs/series` | `storageZoneId`, `from`, `to` (mặc định 48 giờ) | `{ zone: { temperatureMin, temperatureMax }, points: [{ measuredAt, temperatureC, isWithinRange }] }` | Dữ liệu biểu đồ |
| GET | `/qc/temperature-logs` | `isWithinRange=false`, `measurementType`, `from`, `to` | Danh sách | – |
| POST | `/qc/temperature-logs` | `{ storageZoneId? \| storageLocationId? \| materialLotId? \| productionBatchId?, measurementType, temperatureC, deviceCode, sourceChannel, correctiveAction?, evidenceUrl? }` | TemperatureLog | Phải có ít nhất một đối tượng đo. `MinAllowedC`/`MaxAllowedC` lấy từ `StorageZone.TemperatureMin/Max` (hoặc 75°C với CORE\_TEMP). `IsWithinRange` tính ở server. Ngoài ngưỡng mà thiếu `correctiveAction` → vẫn lưu, tạo cảnh báo |
| POST | `/integration/iot/temperature` | `[{ deviceCode, measuredAt, temperatureC }]` | `{ accepted }` | Endpoint cho cảm biến (API key riêng). `DeviceCode` ánh xạ sang khu vực theo cấu hình. `SourceChannel = IOT` |

### 9.4 Vệ sinh khay hộp – `#/qc-cleaning`, `#/qc-cleaning-result` (`CleaningBatche`, `CleaningChecklistResult`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/qc/cleaning-batches` | `{ returnInspectionId, containerQuantity, responsibleEmployeeId }` | CleaningBatche | `CleaningBatchNumber` tự sinh, `StartDate` = now, `Status = InProgress`. Chuỗi BRD: thu hồi → kiểm tra → vệ sinh → QC → sẵn sàng |
| POST | `/qc/cleaning-batches/{id}/results` | `{ checklistId, items: [{ checklistItemId, resultCode, actualValue, chemicalUsed, correctiveAction }] }` | `[CleaningChecklistResult]` | Giống mục 9.2, dùng checklist `VS-KHAY` |
| POST | `/qc/cleaning-batches/{id}/complete` | `{ qcResult: PASS\|REJECT }` | CleaningBatche | Phải có đủ kết quả checklist. `EndDate` = now, `QcResult`, `Status = Completed`. PASS → ghi `PackagingLedger` (`TransactionType = AVAILABLE`, PROPOSED) |

### 9.5 Sự cố nhà cung cấp – `#/qc-supplier-issue` (`SupplierIssue`)

CRUD chuẩn tại `/qc/supplier-issues` + `POST /qc/supplier-issues/{id}/transition` `{ to: InProgress\|Resolved\|Closed, resolution }`. Resolved bắt buộc `resolution`; Closed ghi `ClosedAt`. `OwnerId` là tham chiếu chưa rõ (Employee hay User). Job hằng giờ cảnh báo sự cố quá `DueAt`.

## 10. P7 – Đóng gói, giao hàng và phản hồi (module `logistics`)

Trạng thái thực hiện giao theo BRD: **READY → LOADED → DISPATCHED → ARRIVED → DELIVERED / FAILED**. Dùng `CustomerDeliveryLocationId`, `DeliveryNumber`, `PackingOrderNumber`, `PackedQuantity`, `DeliveredQuantity`/`AcceptedQuantity`/`RejectedQuantity` (bỏ cột cũ tương ứng). CRUD chuẩn: `PackagingLedger` (chỉ đọc + ghi từ nghiệp vụ), `DeliveryEvent` (chỉ đọc).

### 10.1 Đóng gói – `#/log-packing-order`, `#/log-packing-result`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/logistics/packing-orders` | `packingDate`, `status`, `orderId` | Danh sách | – |
| POST | `/logistics/packing-orders/{id}/result` | `{ productionOutputId, plannedQty, packedQuantity, damagedQty, rejectedQuantity, packagingType, qualityStatus, note }` | PackingResult | `packedQuantity + damagedQty + rejectedQuantity = plannedQty`. `AcceptedQty` = Packed. PackingOrder → Packed. Không cho đóng từ mẻ chưa `QcStatus = PASS` |

**Tạo lệnh đóng gói thủ công** (khi một mẻ chia cho nhiều đơn, hoặc đóng bù sau khi khách báo thiếu):

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/logistics/packing-orders` | `{ productionBatchId, orderId, plannedQty, packingDate, note }` | PackingOrder | Mẻ phải có `QcStatus = PASS`. Σ `PlannedQty` của mọi lệnh đóng từ mẻ ≤ `ProductionBatche.AcceptedQty` → vượt thì 422 `BUSINESS_RULE`. Đơn hàng phải ở Locked hoặc InProduction. `PackingOrderNumber` tự sinh, `Status = Planned`. Khi mẻ đã chia hết → mẻ Packed |
| POST | `/logistics/packing-orders/{id}/start` | – | PackingOrder | Planned → Packing |
| POST | `/logistics/packing-orders/{id}/cancel` | `{ reason }` | PackingOrder | Chỉ Planned. Trả số lượng về mẻ |

### 10.2 Tuyến giao hàng – `#/log-route` (`DeliveryRoute`, `DeliveryStop`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/logistics/routes` | `deliveryDate` (bắt buộc) | `[{ route, vehicle, driver, stops: [{ sequenceNo, locationName, plannedMealQty, plannedArrival, stopStatus }], loadMeals, capacity, loadPercent }]` | `loadPercent` = Σ PlannedMealQty ÷ `Vehicle.Capacity` |
| POST | `/logistics/routes/plan` | `{ deliveryDate, vehicleIds: [], strategy: "byArea" }` | `[DeliveryRoute]` | Tạo `DeliveryOrder` cho mỗi (Order × điểm giao) của đơn InProduction/Locked trong ngày (BRD: một Order có thể nhiều DeliveryOrder). Gán vào tuyến theo khu vực, sắp `SequenceNo` theo khung giờ nhận. Không dùng xe `Status = Maintenance`. Thông tin vượt tải trả về cảnh báo |
| POST/PUT | `/logistics/routes` | `{ routeNo?, name, deliveryDate, vehicleId, driverId, plannedStart, plannedEnd }` | DeliveryRoute | `DriverId` chưa có bảng đích xác nhận (đề xuất Employee chức vụ Lái xe). Một xe/tài xế không trùng khung giờ cùng ngày |
| PUT | `/logistics/routes/{id}/stops` | `[{ deliveryOrderId, sequenceNo, plannedArrival }]` | Stops | UNIQUE (`DeliveryRouteId`, `SequenceNo`). Chỉ khi tuyến Planned. GAP-C-02: chưa có cột điểm đầu/cuối; điểm cuối = SequenceNo lớn nhất |
| POST | `/logistics/routes/{id}/load` | `{ employeeId, items?: [{ deliveryOrderId, loadedQty }] }` | `{ loadings[], executions[] }` | Planned → Loading. Mỗi DeliveryOrder: tạo `DeliveryLoading` (`LoadedQty`, `LoadingTime`, `VehicleCode` = Vehicle.Code, `Status = Loaded`) và `DeliveryExecution` (`Status = LOADED`, `DriverName`, `VehicleCode`). Event LOADED. GAP-C-03: chưa liên kết PackingOrder |
| POST | `/logistics/routes/{id}/dispatch` | – | Route | Loading → InTransit, `ActualStart` = now. Chặn nếu còn DeliveryLoading chưa Loaded. Execution LOADED → DISPATCHED (`DispatchedAt`, `DepartedAt`). Event DISPATCHED kèm tọa độ bếp. Gửi tin Zalo cho khách: xe đã xuất phát |
| POST | `/logistics/routes/{id}/complete` | – | Route | InTransit → Completed khi mọi điểm Delivered/Failed. `ActualEnd` = now |

**Đơn giao hàng thủ công – `#/log-delivery-order`** (bổ sung cho việc sinh tự động từ `routes/plan`). BRD: một Order có thể có nhiều DeliveryOrder.

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/logistics/delivery-orders` | `deliveryDate`, `orderId`, `customerId`, `routeId`, `status`, `unassigned=true` | Danh sách + `mealQuantity`, `routeName` | `unassigned` = chưa có `DeliveryRouteId` |
| POST | `/logistics/delivery-orders` | `{ orderId, customerDeliveryLocationId, plannedDeliveryTime, lines: [{ orderLineId, quantity }] }` | DeliveryOrder | Đơn hàng ở Locked/InProduction. Điểm giao phải thuộc khách của đơn. Σ số suất của mọi DeliveryOrder theo từng `OrderLine` ≤ `OrderLine.Quantity`. `DeliveryNumber` tự sinh, `CustomerId` lấy từ Order, `Status = Planned`. Chưa có bảng dòng đơn giao: số suất mỗi đơn giao lưu ở `DeliveryStop.PlannedMealQty` khi gán tuyến (PROPOSED – BA xác nhận) |
| POST | `/logistics/delivery-orders/{id}/split` | `{ parts: [{ customerDeliveryLocationId?, plannedDeliveryTime, mealQuantity }] }` | `[DeliveryOrder]` | Chỉ khi chưa LOADED. Tách thành nhiều đơn giao (ví dụ giao 2 chuyến). Σ `mealQuantity` = số suất gốc. Đơn gốc giữ phần đầu, phần còn lại tạo mới, chưa gán tuyến |
| PUT | `/logistics/delivery-orders/{id}/route` | `{ deliveryRouteId, sequenceNo }` | DeliveryOrder + DeliveryStop | Gán hoặc chuyển tuyến khi tuyến đích còn Planned. Tạo/cập nhật `DeliveryStop`; kiểm tra tải trọng xe |
| POST | `/logistics/delivery-orders/{id}/cancel` | `{ reason }` | DeliveryOrder | Chỉ khi chưa LOADED. Xóa `DeliveryStop` liên quan |

### 10.3 Thực hiện giao hàng và POD – `#/log-execution`, `#/log-pod` (`DeliveryExecution`, `DeliveryEvent`, `ProofOfDeliverie`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/logistics/executions` | `deliveryDate`, `status`, `routeId` | `[{ deliveryOrder, location, route, execution, pod? }]` + `statusCounts` | – |
| POST | `/logistics/executions/{id}/arrive` | `{ latitude, longitude, temperatureC?, photoUrl? }` | Execution | DISPATCHED → ARRIVED, `ArrivedAt`. Tạo `DeliveryEvent` (ARRIVED, tọa độ, nhiệt độ hộp giữ nhiệt). `DeliveryStop` → Arrived, `ActualArrival`. Gọi từ app di động tài xế |
| POST | `/logistics/executions/{id}/pod` | `{ deliveredQuantity, acceptedQuantity, receiverName, receiverPhone, signatureUrl, photoUrl, remark }` | `{ pod, execution, complaint? }` | Chỉ ARRIVED. `receiverName` bắt buộc, `acceptedQuantity ≤ deliveredQuantity`. Transaction: tạo `ProofOfDeliverie` (`RejectedQuantity` = Delivered − Accepted, `ReceivedAt`, `ConfirmedAt`); Execution → DELIVERED (`DeliveredAt`); `DeliveryOrder.Status` = DELIVERED, `ActualDeliveryTime`; `DeliveryStop` → Delivered, `DeliveredMealQty`, `ShortageQty`; Event DELIVERED. Nếu mọi DeliveryOrder của đơn đã giao → Order Delivered. Có suất bị từ chối → tự tạo `CustomerComplaint` (`SourceChannel = POD`, `CategoryCode = SHORTAGE`). Ghi `PackagingLedger` OUT nếu đóng khay |
| POST | `/logistics/executions/{id}/fail` | `{ reason, latitude?, longitude? }` | Execution | DISPATCHED/ARRIVED → FAILED, `Remark`. Event FAILED. Cảnh báo điều phối |
| GET | `/logistics/delivery-orders/{id}/events` | – | Timeline `DeliveryEvent` theo `EventAt` | Drawer "Sự kiện" |
| GET | `/logistics/vehicle-positions` | `deliveryDate` | `[{ eventType, latitude, longitude, eventAt, description }]` | **GAP-F-05**: chỉ có tọa độ theo sự kiện trong `DeliveryEvent`, không phải GPS liên tục. `LocationTracking` là nhật ký IP, không dùng cho việc này |

### 10.4 Thu hồi khay hộp – `#/log-container-return`, `#/log-return-inspection`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/logistics/container-returns` | `{ deliveryOrderId, returnDate, expectedQuantity, returnedQuantity, damagedQuantity, note }` | ContainerReturn | `CustomerId` lấy từ DeliveryOrder. `Status = Returned`. Ghi `PackagingLedger` IN (`ConditionCode` = DAMAGED nếu có hỏng) |
| POST | `/logistics/container-returns/{id}/inspect` | `{ inspectionResult: OK\|SHORTAGE\|DAMAGED\|DIRTY\|REJECTED, damagedQty, missingQty, inspectorEmployeeId, note }` | `{ returnInspection, cleaningBatch? }` | Tạo `ReturnInspection` (`InspectionDate` = now). ContainerReturn → Inspected. Khay dùng được → tự tạo `CleaningBatche` (mục 9.4). GAP-C-05: chưa có cột số lượng "bẩn"; ghi qua `InspectionResult = DIRTY` |
| GET | `/logistics/packaging-ledger/balance` | `customerId` | `[{ packagingType, out, in, outstanding }]` | Số khay còn nằm ở khách = Σ OUT − Σ IN |

### 10.5 Khiếu nại – `#/log-complaint`, `#/log-complaint-history`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/logistics/complaints` | `status`, `severity`, `customerId`, `overdue=true` | Danh sách + `isOverdue` | `isOverdue` = `SlaDueAt` < now và chưa Resolved/Closed |
| POST | `/logistics/complaints` | `{ customerId, orderId?, deliveryOrderId?, deliveryRouteId?, sourceChannel, categoryCode, severity, description }` | CustomerComplaint | `ComplaintNo` tự sinh (UNIQUE), `ReceivedAt` = now, `ComplaintStatus = Open`, `Severity` mặc định Medium. `SlaDueAt` = now + SLA theo mức độ (cấu hình; GAP-F-02). Tạo `ComplaintHistory` CREATED |
| POST | `/logistics/complaints/{id}/transition` | `{ to: InProgress\|Resolved\|Closed\|Rejected, comment, assignedTo? }` | `{ complaint, history }` | Theo state machine. `comment` bắt buộc. Resolved ghi `ResolvedAt`, `Resolution` = comment. Mọi lần chuyển ghi `ComplaintHistory` (`OldStatus`, `NewStatus`, `ActorId` = uid, `EventAt`) |
| GET | `/logistics/complaints/{id}/history` | – | `[ComplaintHistory]` | – |

## 11. P8 – Nhân sự, phê duyệt, Bot và KPI (module `hr`, `workflow`, `integration`, `kpi`)

### 11.1 Phân ca – `#/hr-shift-assign` (`ShiftAssignment`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/hr/shift-grid` | `from` (7 ngày), `organizationUnitId?` | `{ days: [], employees: [{ id, fullName, position, cells: { "2026-11-14": { assignmentId, shiftCode, status, onLeave } } }], kpiToday: [{ shiftId, assigned, checkedIn }] }` | Pivot nhân viên × ngày. `onLeave` = có `LeaveRequest` Approved bao ngày đó |
| PUT | `/hr/shift-assignments` | `{ employeeId, workDate, shiftId \| null }` | ShiftAssignment hoặc 204 | Upsert theo (`EmployeeId`, `WorkDate`). UNIQUE (`EmployeeId`, `ShiftId`, `WorkDate`). Chặn 409 nếu đã có `AttendanceEvent` (phải dùng đơn đổi ca) hoặc nhân viên đang nghỉ phép. `PlannedStart/End` từ giờ ca, `AssignmentStatus = Planned`, `AssignedBy` = uid |
| POST | `/hr/shift-assignments/bulk` | `{ employeeIds, shiftId, from, to, skipWeekdays: [] }` | `{ created, skipped }` | Phân ca hàng loạt |
| POST | `/hr/shift-assignments/confirm` | `{ workDate, organizationUnitId? }` | `{ confirmed }` | Planned → Confirmed, gửi tin Telegram cho nhân viên có `UserExternalIdentity` |

### 11.2 Chấm công – `#/hr-attendance` (`AttendanceEvent`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/hr/attendance/check-in` · `/check-out` | `{ latitude, longitude, sourceChannel: MOBILE\|ZALO\|KIOSK, externalEventId? }` | AttendanceEvent | Tìm `ShiftAssignment` hôm nay của nhân viên. `DistanceMeters` = khoảng cách đến `KitchenWorkstation` gần nhất; `IsValid` = Distance ≤ `RadiusMeters`. Check-in → Assignment CheckedIn; check-out → Completed. Đi muộn/về sớm tính so với `Shift.StartTime/EndTime`, ghi `Note`. Chống trùng bằng `ExternalEventId` |
| GET | `/hr/attendance/report` | `from`, `to`, `organizationUnitId` | `[{ employee, assignedShifts, present, late, absent, earlyLeave }]` | Job cuối ngày đánh Absent cho ca Confirmed không có check-in |

### 11.3 Xin nghỉ và đổi ca – `#/hr-leave`, `#/hr-swap`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/hr/leave-requests` | `{ employeeId, fromDate, toDate, leaveTypeCode, reason }` | LeaveRequest | `RequestNo` tự sinh (UNIQUE), `ToDate ≥ FromDate`, không chồng đơn khác. `RequestStatus = Pending`. ApprovalService (LEAVE\_REQUEST), ghi `ApprovalRequestId`. Khi duyệt: xóa mềm các `ShiftAssignment` Planned trong khoảng nghỉ, cảnh báo ca thiếu người |
| POST | `/hr/shift-swap-requests` | `{ requesterEmployeeId, targetEmployeeId, currentShiftAssignmentId, proposedShiftAssignmentId, reason }` | ShiftSwapRequest | Hai ca phải thuộc đúng hai nhân viên và chưa có chấm công. ApprovalService (SHIFT\_SWAP). Khi duyệt: hoán đổi `EmployeeId` giữa hai `ShiftAssignment` trong transaction. Các cột `…EmployeeId`, `…ShiftAssignmentId` là tham chiếu chưa xác nhận: kiểm tra theo đề xuất |
| POST | `/hr/leave-requests/{id}/cancel` · `/hr/shift-swap-requests/{id}/cancel` | – | – | Chỉ Pending, người tạo. Hủy luôn ApprovalRequest |

### 11.4 Ma trận trạng thái ca

`ShiftAssignment.AssignmentStatus`: Planned (mặc định SQL) → Confirmed → CheckedIn → Completed / Absent (PROPOSED).

### 11.5 Hộp phê duyệt tập trung – `#/wf-inbox` (`ApprovalRequest`, `ApprovalStep`, `ApprovalDecision`, `ApprovalItem`)

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/workflow/approvals` | `tab=mine\|pending\|all`, `businessType`, `from`, `to` | `[{ request, business: { table, id, label }, steps: [{ stepNo, approverRoleCode, decisionStatus }], canDecide }]` + KPI (chờ tôi, đang chờ, đã duyệt, từ chối) | `mine` = bước Waiting đầu tiên có `ApproverUserId` = uid hoặc `ApproverRoleCode` thuộc nhóm của user. `business.label` tra theo `BusinessType` → bảng (mục 2.3) |
| GET | `/workflow/approvals/{id}` | – | `{ request, steps[], decisions[], items[], business: { table, record } }` | `record` = DTO bản ghi nghiệp vụ (với PR kèm dòng) |
| POST | `/workflow/approvals/{id}/decide` | `{ decision: APPROVE\|REJECT, comment }` | `{ request, businessStatus }` | Xử lý bên dưới |
| POST | `/workflow/approvals/{id}/cancel` | `{ reason }` | Request | Người gửi hủy khi còn Pending. Trả bản ghi nghiệp vụ về Draft |

Xử lý `decide` (một transaction):

1. Lấy bước `DecisionStatus = Waiting` có `StepNo` nhỏ nhất. Không có → 409. User không đúng người/vai trò duyệt → 403 `NOT_APPROVER`.
2. REJECT bắt buộc `comment`.
3. Cập nhật `ApprovalStep` (`DecisionStatus`, `DecidedAt`, `DecisionNote`). Tạo `ApprovalDecision` (`DecisionCode`, `DecidedBy`, `DecidedAt`, `Comment`, `PayloadHash` = hash `PayloadJson` để chống sửa dữ liệu sau khi duyệt).
4. APPROVE và còn bước → `RequestStatus = InProgress`, `CurrentStepNo` + 1, thông báo người duyệt kế. APPROVE ở bước cuối → Approved, `CompletedAt`. REJECT → Rejected, `CompletedAt`.
5. Khi kết thúc: cập nhật `ApprovalItem.ItemStatus` và gọi handler theo `BusinessType` (bảng mục 2.3) để đổi trạng thái bản ghi nghiệp vụ. Ghi audit cả hai phía.
6. Gửi thông báo kết quả cho người yêu cầu qua `OutboundMessage`.

### 11.6 Tích hợp Zalo / Telegram / Facebook – `#/int-channel`, `#/int-bot`, `#/int-identity`, `#/int-inbound`, `#/int-outbound`, `#/int-log`

CRUD chuẩn: `IntegrationChannel` (UNIQUE `ChannelCode`), `BotConfiguration` (UNIQUE `BotCode`), `UserExternalIdentity` (UNIQUE `IntegrationChannelId` + `ExternalUserId`). Không bao giờ trả về giá trị bí mật; `SecretReference` chỉ là khóa tham chiếu đến kho bí mật (Key Vault).

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| POST | `/integration/webhooks/{channelCode}` | Payload gốc của nhà cung cấp | 200 nhanh | Không dùng JWT; xác thực chữ ký theo từng provider. Ghi `InboundMessage` (`ExternalMessageId`, `ExternalConversationId`, `ExternalUserId`, `MessageType`, `PayloadJson`, `ProcessingStatus = Received`, `IdempotencyKey` = provider + message id, bỏ trùng) + `IntegrationLog` (IN). Đẩy vào hàng đợi xử lý bất đồng bộ |
| Worker | `InboundMessageProcessor` | – | – | Nhận dạng ý định: báo số suất → cập nhật `Forecast` hoặc dòng đơn Draft của khách (map `ExternalUserId` → khách qua cấu hình, GAP-F-16); phản ánh → tạo `CustomerComplaint`; nhân viên chấm công → mục 11.2. Thành công → Processed; lỗi → Failed + `ErrorMessage`. Trả lời xác nhận qua `OutboundMessage` |
| POST | `/integration/inbound-messages/{id}/reprocess` | – | Message | Chạy lại worker cho tin Failed/Received |
| POST | `/integration/outbound-messages` | `{ integrationChannelId, recipientExternalId, externalConversationId, messageType, payload }` | OutboundMessage | `MessageStatus = Queued`. Worker `OutboundSender` gửi, cập nhật Sent/Delivered/Failed, `ProviderMessageId`, `RetryCount`, `LastError`; ghi `IntegrationLog` (OUT, `HttpStatus`) |
| POST | `/integration/outbound-messages/{id}/retry` | – | Message | Chỉ Failed: Queued, `RetryCount` + 1 (tối đa theo cấu hình) |
| GET | `/integration/conversations` | `channelId`, `from`, `to` | Timeline gộp Inbound + Outbound theo thời gian | Màn hình hộp thư Bot |
| POST | `/integration/identities/link` | `{ integrationChannelId, externalUserId, userId?, employeeId? }` | UserExternalIdentity | Liên kết tài khoản chat với user/nhân viên; `LinkedAt` = now |

### 11.7 KPI – `#/kpi-definition`, `#/kpi-daily`, `#/kpi-monthly`, `#/kpi-refresh`

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| CRUD | `/kpi/definitions` | DTO `KpiMetricDefinition` | DTO | UNIQUE (`MetricCode`, `VersionNo`). Đổi công thức → tạo phiên bản mới, không sửa phiên bản cũ |
| GET | `/kpi/daily` | `from`, `to`, `branchId`, `domainCode` | `{ metrics: [{ definition, latest: { value, target, variance, met }, series: [{ date, value }] }] }` | Đọc `KpiDailySnapshot`. `met` = value ≥ target (hoặc ≤ với chỉ tiêu càng thấp càng tốt: WASTE, REJECT, EXPIRY, COST) |
| GET | `/kpi/monthly` | `fromMonth`, `toMonth`, `branchId`, `domainCode` | Như trên | `KpiMonthlySnapshot` |
| POST | `/kpi/refresh` | `{ jobCode: KPI_DAILY\|KPI_MONTHLY\|INVENTORY_BALANCE, date? }` | `{ runId }` | Khởi chạy job, ghi `AnalyticsRefreshLog` (`RunStatus` Running → Succeeded/Failed, `RowsProcessed`, `SourceWatermark`) |
| GET | `/kpi/refresh-logs` | `jobCode`, `status` | Danh sách | – |

Job `KpiDailyJob` (hằng ngày 17:00 và 23:30): với mỗi `KpiMetricDefinition` đang hiệu lực, mỗi chi nhánh, tính `Numerator`, `Denominator`, `MetricValue`, `TargetValue`, `VarianceValue`, `DataQualityStatus` rồi upsert theo UNIQUE (`SnapshotDate`, `BranchId`, `MetricCode`, `MetricVersion`). Công thức đề xuất: PROD\_ACHIEVEMENT = Σ AcceptedQuantity ÷ Σ PlannedQuantity; DEL\_ONTIME = số DeliveryOrder giao trong khung giờ ÷ tổng; INV\_ACCURACY = 1 − |Σ VarianceValue| ÷ giá trị tồn; FIN\_COST\_MEAL = Σ TotalActualCost ÷ Σ AcceptedMealQuantity; các chỉ tiêu còn lại chờ BA xác nhận công thức. `BranchId` → OrganizationUnit (PO xác nhận).

## 12. Dashboard điều hành, luồng E2E và Database Explorer

### 12.1 Dashboard điều hành – `#/dash-exec`

Một endpoint tổng hợp cho toàn màn hình, đọc trực tiếp bảng giao dịch (không chờ snapshot). Cache 60 giây theo (ngày, ca, chi nhánh).

`GET /dashboard/executive?date=2026-11-14&shiftId=&branchId=`

| Khối output | Trường | Cách tính |
| --- | --- | --- |
| `kpi.plannedMeals` | số suất | Σ `OrderLine.Quantity` của đơn có `MealDate` = ngày, Status ∉ {Draft, Cancelled}, lọc ca/chi nhánh |
| `kpi.orders` | `{ total, byStatus }` | Đếm `Order` theo Status |
| `kpi.productionProgress` | `{ percent, donePortions, plannedPortions, batchCount }` | Σ `ActualQuantity` mẻ Passed/Packed ÷ Σ `PlannedQuantity` các mẻ thuộc kế hoạch của ngày |
| `kpi.delivery` | `{ delivered, total }` | `DeliveryExecution.Status = DELIVERED` / số `DeliveryOrder` có `DeliveryDate` = ngày |
| `kpi.stockAlerts` | số nguyên liệu | Nguyên liệu có Σ `AvailableQuantity` < `InventoryPolicie.MinimumStock` |
| `kpi.qcHoldReject` | số | Mẻ trong ngày có `QcStatus` ∈ {HOLD, REJECT} + `QCInspectionLine.Result = HOLD` chưa xử lý |
| `kpi.staffing` | `{ checkedIn, assigned }` | `ShiftAssignment` của ngày: CheckedIn/Completed so với tổng |
| `kpi.openComplaints` | số | `CustomerComplaint.ComplaintStatus` ∈ {Open, InProgress} |
| `chart.meals7d` | `[{ date, planned, accepted }]` | 7 ngày đến `date`: Σ OrderLine.Quantity và Σ `ProofOfDeliverie.AcceptedQuantity` theo ngày giao |
| `shiftProgress` | `[{ shiftId, name, batchCount, percent }]` | Tiến độ mẻ theo `ProductionOrder.ShiftId` |
| `kpiSnapshot` | `[{ metricCode, name, value, target, met }]` | 4 chỉ tiêu từ `KpiDailySnapshot` của ngày: PROD\_ACHIEVEMENT, DEL\_ONTIME, INV\_ACCURACY, FIN\_FOOD\_COST |
| `orders` | 6 đơn đầu + trạng thái giao | Đơn trong ngày kèm danh sách `DeliveryExecution.Status` |
| `alerts` | giống `/notifications/alerts` | Xem quy tắc dưới |

Quy tắc cảnh báo (`/notifications/alerts`, dùng chung cho chuông trên header): nguyên liệu dưới tồn tối thiểu; yêu cầu phê duyệt Pending/InProgress; dòng QC đầu vào HOLD; `TemperatureLog.IsWithinRange = 0` trong 48 giờ; khiếu nại đang mở; `OutboundMessage.MessageStatus = Failed`; lô Available hết hạn trong 2 ngày. Mỗi cảnh báo trả `route` để Angular điều hướng.

### 12.2 Luồng nghiệp vụ E2E – `#/flow-e2e`

Màn hình điều hướng tĩnh, không cần API nghiệp vụ. Nếu muốn hiển thị tiến độ thật của một đơn hàng xuyên luồng, dùng `GET /dashboard/order-journey/{orderId}` trả trạng thái từng chặng: Order → ProductionPlan → ProductionOrder/Batche → PackingOrder → DeliveryOrder/Execution → ProofOfDeliverie → OrderReconciliation.

### 12.3 Database Architecture Explorer – `#/dbx-explorer`

Màn hình nội bộ cho BA/Dev, chỉ nhóm Quản trị hệ thống. Không đọc trực tiếp `sys.*` trên môi trường chạy; dùng file metadata sinh từ SQL + Dictionary khi build (giống `schema.js` trong mockup), nạp vào bộ nhớ.

| Method | Endpoint | Input | Output | Xử lý |
| --- | --- | --- | --- | --- |
| GET | `/meta/db/summary` | – | `{ tables: 124, columns: 2264, defaults: 323, deprecated: 141, physicalFk: 0, relations: 224, poConfirmed: 21, unresolved: 80 }` | – |
| GET | `/meta/db/tables` | `q`, `phase`, `classification` (Master Data / Transaction / Policy / Workflow / System / Integration / Analytics / Legacy), `module` | `[{ name, phase, classification, module, columnCount, description }]` | `q` tìm theo tên bảng, mô tả hoặc tên cột chính xác |
| GET | `/meta/db/tables/{name}` | – | `{ table, description, descriptionSource, pk, uniques[], columns: [{ ord, name, type, nullable, default, isPk, usage, note, description, basis }], relationsOut[], relationsIn[], unresolved[], screens[] }` | Chuỗi truy vết Module → Function → Screen → Table → Column |
| GET | `/meta/db/tables/{name}/sample` | `take` (≤ 20) | Dữ liệu mẫu | Chỉ môi trường Dev/UAT; che cột nhạy cảm |
| GET | `/meta/db/relations` | `level=PO\|PROPOSED`, `table` | Danh sách quan hệ logic | – |

### 12.4 GAP / BA Confirmation – `#/dbx-gap`

`GET /meta/gaps?group=func|col|ref|desc|dq|status|reference|partial` trả nội dung Gap Register; `PUT /meta/gaps/{code}` `{ decision, status }` cho BA ghi quyết định. Chưa có bảng lưu GAP trong SQL; đề xuất lưu file JSON cấu hình hoặc quản lý ngoài hệ thống (Excel).

## 13. Phụ lục

### A. Mã lỗi nghiệp vụ

| HTTP | `errors[].code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Thiếu trường NOT NULL, sai độ dài, sai định dạng. `errors[]` gồm `{ field, code, message }` |
| 403 | `PERMISSION_DENIED` · `NOT_APPROVER` | Thiếu cờ UserRole · không đúng người/vai trò duyệt |
| 404 | `NOT_FOUND` | Không có hoặc đã xóa mềm |
| 409 | `INVALID_TRANSITION` | Chuyển trạng thái sai luồng |
| 409 | `CONCURRENCY_CONFLICT` | `If-Match` lệch `ModifiedDate` |
| 409 | `DUPLICATE` | Trùng UNIQUE hoặc trùng mã kiểm tra ở service |
| 409 | `IN_USE` | Xóa bản ghi đang được tham chiếu |
| 409 | `QC_NOT_CONCLUDED` | Ghi sổ nhập kho khi chưa kết luận QC |
| 422 | `REF_NOT_FOUND` | Id tham chiếu không tồn tại (thay FK) |
| 422 | `NO_ACTIVE_CONTRACT` · `NO_ACTIVE_RECIPE` | Tạo đơn không có hợp đồng hiệu lực · món không có công thức hiệu lực |
| 422 | `INSUFFICIENT_STOCK` | Xuất/giữ chỗ vượt khả dụng |
| 422 | `QC_REJECTED` · `CORE_TEMP_OUT_OF_RANGE` | Ghi sổ phiếu bị REJECT · PASS mẻ khi nhiệt độ lõi chưa đạt |
| 422 | `UOM_CONVERSION_MISSING` | Thiếu quy đổi đơn vị |
| 422 | `BUSINESS_RULE` | Vi phạm quy tắc khác, `message` mô tả cụ thể |

### B. State machine cần cài đặt

Mã trạng thái giữ đúng cách viết của nguồn (SQL hoặc BRD); mã tự đề xuất viết PascalCase. Đầy đủ 47 bộ nằm ở Gap Register sheet 07 và `GET /meta/statuses`.

| Bảng.Cột | Luồng | Endpoint gây chuyển | Nguồn |
| --- | --- | --- | --- |
| `Order.Status` | Draft → Submitted → Confirmed → Locked → InProduction → Delivered → Reconciled; Cancelled | submit, confirm, lock, cancel; event release/POD/close | PROPOSED |
| `Contract.Status` | Draft → PendingApproval → Active → Expired / Terminated | submit, approval, terminate, job | PROPOSED |
| `Recipe.Status` | Draft → PendingApproval → Approved → Locked → Obsolete | submit, approval, lock | PROPOSED |
| `Menu.Status` | Draft → Approved → Published → Expired | approve, publish, expire | PROPOSED |
| `PurchaseRequest.Status` | Draft → PendingApproval → Approved / Rejected → Converted | submit, approval, convert-to-po | PROPOSED |
| `PurchaseOrder.Status` | Draft → Approved → Sent → PartiallyReceived → Received → Closed; Cancelled | approve, send, receive, close, cancel | PROPOSED |
| `GoodsReceipt.Status` | QCPending → Accepted / PartiallyAccepted / Rejected → Posted | receive, conclude, post | PROPOSED |
| `QCInspection.Result`, `MaterialLot.QcStatus`, `ProductionBatche.QcStatus` | PASS / HOLD / REJECT | conclude, batch qc | BRD |
| `MaterialLot.Status` | Available ⇄ OnHold; Expired; Consumed | post, hold/release, job | PROPOSED |
| `ProductionPlan.Status` | Draft → Approved → Released → Completed | generate, approve, release | PROPOSED |
| `ProductionOrder.Status` | Released → InProgress → Completed → Closed; Cancelled | release, issue/start, complete, close | PROPOSED |
| `ProductionBatche.Status` | Planned → Cooking → QCPending → Passed / Failed → Packed | start, finish, qc, create-packing-order | PROPOSED |
| `DeliveryRoute.Status` | Planned → Loading → InTransit → Completed | load, dispatch, complete | PROPOSED |
| `DeliveryExecution.Status` | READY → LOADED → DISPATCHED → ARRIVED → DELIVERED / FAILED | load, dispatch, arrive, pod, fail | BRD |
| `DeliveryStop.StopStatus` | Planned → Arrived → Delivered / Failed | arrive, pod, fail | SQL + PROPOSED |
| `CustomerComplaint.ComplaintStatus` | Open → InProgress → Resolved → Closed; Rejected | transition | SQL + PROPOSED |
| `ApprovalRequest.RequestStatus` / `ApprovalStep.DecisionStatus` | Pending → InProgress → Approved / Rejected / Cancelled · Waiting → Approved / Rejected | decide, cancel | SQL + PROPOSED |
| `LeaveRequest`, `ShiftSwapRequest`, `MenuChangeRequest`.RequestStatus | Pending → Approved / Rejected / Cancelled | approval, cancel | SQL + PROPOSED |
| `ShiftAssignment.AssignmentStatus` | Planned → Confirmed → CheckedIn → Completed / Absent | confirm, check-in/out, job | SQL + PROPOSED |
| `StockCount.Status` | Draft → Counting → PendingApproval → Posted | lines, submit, post | PROPOSED |
| `InboundMessage.ProcessingStatus` / `OutboundMessage.MessageStatus` | Received → Processed / Failed · Queued → Sent → Delivered / Failed | worker, reprocess, retry | SQL + PROPOSED |

### C. GAP ảnh hưởng trực tiếp đến API

| Mã | Ảnh hưởng | Cách xử lý tạm trong API |
| --- | --- | --- |
| GAP-F-03 / F-04 | Không có phiếu chuyển kho, điều chỉnh tồn | Ghi thẳng sổ cái với `ReferenceType` riêng (mục 7.6) – chờ BA |
| GAP-F-05 | Không có GPS liên tục | Chỉ trả tọa độ từ `DeliveryEvent` |
| GAP-F-06 | 12 danh mục tham chiếu không có bảng | File cấu hình + `/meta/reference-lists` |
| GAP-F-07 | Không có giờ chốt đơn | Cấu hình theo ca + `OrderCutoffJob` |
| GAP-F-08 | Không có mẫu quy trình duyệt | Cấu hình cố định (mục 2.3) |
| GAP-F-09 | Không liên kết tài liệu với chứng từ | Ghi URL vào cột `…Url` có sẵn |
| GAP-F-16 | Không có tài khoản khách hàng | Khách tương tác qua Bot; map `ExternalUserId` → Customer bằng cấu hình |
| GAP-C-01 | ActualCost thiếu Packaging/Logistics/Utility | Gộp `OtherCost` |
| GAP-C-07 | `PurchaseUomId`, `IssueUomId` không có cột `UnitId` | Vẫn dùng, tham chiếu `UnitMeasure` |
| GAP-C-10 | Không liên kết Person – Employee | Xác định nhân viên của user qua `UserExternalIdentity` / cấu hình |
| 80 tham chiếu chưa rõ | Không kiểm tra tồn tại được | Chỉ kiểm tra định dạng GUID; ghi log cảnh báo |

GAP mới phát hiện khi viết API tách đơn giao (mục 10.2): SQL không có bảng dòng đơn giao, nên không lưu được mỗi `DeliveryOrder` giao bao nhiêu suất của từng `OrderLine`. Tạm lưu tổng số suất ở `DeliveryStop.PlannedMealQty`; cần BA/PO xác nhận và bổ sung vào Gap Register.

### D. Thứ tự triển khai API đề xuất

| Sprint | Phạm vi API | Phụ thuộc |
| --- | --- | --- |
| 1 | Mục 1–2 (khung chung, auth, meta, audit), P0 | – |
| 2 | P1 master data, P2 món ăn & công thức | Sprint 1 |
| 3 | P3 hợp đồng, thực đơn, đơn hàng; hộp phê duyệt (11.5) | Sprint 2 |
| 4 | P4 MRP, mua hàng, nhập kho, sổ cái kho, QC đầu vào (9.1) | Sprint 3 |
| 5 | P5 sản xuất & giá thành, P6 checklist & nhiệt độ | Sprint 4 |
| 6 | P7 giao hàng, POD, thu hồi, khiếu nại; đối soát (6.7) | Sprint 5 |
| 7 | P8 nhân sự, tích hợp Bot, KPI, Dashboard, Explorer | Sprint 1–6 |

Mỗi API cần Swagger (OpenAPI 3) sinh từ controller, kèm ví dụ request/response lấy từ dữ liệu demo của mockup.

### E. Ví dụ request/response cho các lệnh chính

Giá trị lấy từ dữ liệu DEMO của mockup. GUID rút gọn dạng `…` chỉ để dễ đọc; API thật dùng GUID đầy đủ. Mọi response bọc trong envelope ở mục 1.2.

**E.1 Tạo đơn hàng** – `POST /api/v1/sales/orders` (header `Idempotency-Key: 6f1c…`)

```json
{
  "customerId": "3a1e…-KH002",
  "contractId": "9b70…-HD002",
  "deliveryLocationId": "c41d…-KH002-L1",
  "mealDate": "2026-11-18",
  "deliveryDate": "2026-11-18T10:30:00",
  "note": "Đơn tuần 47",
  "lines": [
    { "contractMealId": "e2a9…-LUNCH", "mealType": "LUNCH", "shiftId": "7d3c…-CA2", "quantity": 1240, "deliveryLocationId": "c41d…-KH002-L1" }
  ]
}
```

```json
{
  "success": true,
  "data": {
    "id": "0b8f…",
    "orderNumber": "DH-261118-01",
    "status": "Draft",
    "mealDate": "2026-11-18",
    "totalQuantity": 1240,
    "totalAmount": 39680000,
    "lines": [
      { "id": "51c2…", "mealType": "LUNCH", "shiftId": "7d3c…-CA2", "menuId": "a8e0…-TD-2026-W47", "quantity": 1240, "unitPrice": 32000, "lineAmount": 39680000 }
    ],
    "allowedActions": ["submit", "cancel"]
  },
  "message": null,
  "errors": [],
  "traceId": "00-4bf9…-01"
}
```

**E.2 Kết luận QC đầu vào** – `POST /api/v1/qc/inspections/{id}/conclude`

```json
{
  "inspectorEmployeeId": "88aa…-NV0027",
  "note": "Lô thịt bò nhiệt độ 7,2°C",
  "lines": [
    { "id": "q1…", "temperature": 3.8, "weight": 120, "appearance": "Đạt", "packaging": "Nguyên vẹn", "result": "PASS" },
    { "id": "q2…", "temperature": 7.2, "weight": 45, "appearance": "Đạt", "packaging": "Rách 2 túi", "result": "HOLD", "remark": "Nhiệt độ vượt 5°C – chờ trưởng QC" }
  ]
}
```

```json
{
  "success": true,
  "data": {
    "inspection": { "id": "…", "inspectionNumber": "QC-IN-031", "result": "PASS" },
    "goodsReceiptStatus": "PartiallyAccepted",
    "supplierIssue": { "id": "…", "issueType": "TEMPERATURE", "severity": "Medium", "issueStatus": "Open", "dueAt": "2026-11-16T17:00:00" }
  }
}
```

**E.3 Ghi sổ nhập kho** – `POST /api/v1/scm/goods-receipts/{id}/post` (body rỗng)

```json
{
  "success": true,
  "data": {
    "goodsReceipt": { "id": "…", "receiptNumber": "PN-2611-031", "status": "Posted" },
    "lots": [
      { "id": "…", "lotNumber": "LOT-261114-NL001", "ingredientId": "…-NL001", "expiryDate": "2026-11-17", "qcStatus": "PASS", "status": "Available" },
      { "id": "…", "lotNumber": "LOT-261114-NL002", "ingredientId": "…-NL002", "qcStatus": "HOLD", "status": "OnHold" }
    ],
    "transactions": [
      { "transactionNumber": "GD-PN-2611-031-NL001", "transactionType": "RECEIPT", "quantity": 120, "quantityIn": 120, "unitCost": 118000 }
    ]
  }
}
```

Lỗi khi QC chưa kết luận (HTTP 409):

```json
{
  "success": false,
  "data": null,
  "message": "Phiếu nhập chưa có kết luận QC",
  "errors": [ { "code": "QC_NOT_CONCLUDED", "field": null, "message": "QCInspection.Result đang rỗng" } ],
  "traceId": "00-91ac…-01"
}
```

**E.4 Ghi POD** – `POST /api/v1/logistics/executions/{id}/pod`

```json
{
  "deliveredQuantity": 1250,
  "acceptedQuantity": 1238,
  "receiverName": "Nguyễn Thị Lan",
  "receiverPhone": "0912345678",
  "signatureUrl": "/files/7c1e…/signature.png",
  "photoUrl": "/files/7c1f…/handover.jpg",
  "remark": "12 suất nguội, khách từ chối"
}
```

```json
{
  "success": true,
  "data": {
    "pod": { "id": "…", "deliveredQuantity": 1250, "acceptedQuantity": 1238, "rejectedQuantity": 12, "receivedAt": "2026-11-14T10:50:00" },
    "execution": { "id": "…", "status": "DELIVERED", "deliveredAt": "2026-11-14T10:50:00" },
    "complaint": { "id": "…", "complaintNo": "KN-2611-005", "sourceChannel": "POD", "categoryCode": "SHORTAGE", "complaintStatus": "Open" }
  }
}
```

**E.5 Duyệt một bước phê duyệt** – `POST /api/v1/workflow/approvals/{id}/decide`

```json
{ "decision": "APPROVE", "comment": "Đồng ý mua bổ sung cho kế hoạch 15/11" }
```

```json
{
  "success": true,
  "data": {
    "request": { "id": "…", "requestNo": "PD-2611-001", "businessType": "PURCHASE_REQUEST", "requestStatus": "Approved", "currentStepNo": 2, "completedAt": "2026-11-14T15:20:00" },
    "businessStatus": { "table": "PurchaseRequest", "id": "…", "column": "Status", "value": "Approved" }
  }
}
```

Lỗi khi user không phải người duyệt bước hiện tại (HTTP 403): `errors[0].code = "NOT_APPROVER"`, `message` nêu vai trò cần có, ví dụ "Bước 2 cần vai trò Ban giám đốc".
