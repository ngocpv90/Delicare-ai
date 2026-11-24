# Lược đồ chuẩn – 124 bảng `dbo` (nguồn: 20261113_structure_db_appdelicare.sql + Dictionary 20261113)

**Không thêm bảng/cột ngoài danh sách này** (CLAUDE.md – Luật DB). Không có FK vật lý; cột "→ Bảng" là **quan hệ logic** (kiểm ở tầng Service).
Cột chuẩn BaseEntity (`Id`, `CreatedDate`, `CreatedBy`, `ModifiedDate`, `ModifiedBy`, `DeletedDate`, `DeleteddBy`, `IsDeleted`, `DeletedBy`) chỉ liệt kê khi bảng THIẾU. "Deprecated → X" = dùng cột X (DEPRECATED_COLUMNS.md).

## ActualCost
_Sản xuất & giá thành · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu chi phí thực tế phát sinh của hoạt động/mẻ sản xuất; có thể gồm nguyên vật liệu, nhân công, chi phí chung, hao hụt và chi phí khác.
BRD: **Actual Cost** (Costing/Result) – Giá thành thực tế gồm Actual Material, Labor, Packaging, Logistics, Utility và Waste.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ProductionBatchId | uniqueidentifier | N |  | ProductionBatche | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MaterialCost | decimal(18, 2) | Y |  |  | Chi phí nguyên vật liệu |
| LaborCost | decimal(18, 2) | Y |  |  | Chi phí nhân công |
| OverheadCost | decimal(18, 2) | Y |  |  | Chi phí chung |
| WasteCost | decimal(18, 2) | Y |  |  | Chi phí hao hụt |
| TotalCost | decimal(18, 2) | Y |  |  | Tổng chi phí |
| ProductionOrderId | uniqueidentifier | Y |  | ProductionOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OtherCost | decimal(18, 2) | N | (0) |  | Chi phí khác |
| CurrencyCode | nvarchar(10) | Y |  |  | Mã tiền tệ |
| CalculatedAt | datetime2(7) | N | sysutcdatetime() |  | Thời điểm tính toán |

## ActualCosts_Legacy_20261113
_Sản xuất & giá thành · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu chi phí thực tế phát sinh của hoạt động/mẻ sản xuất; có thể gồm nguyên vật liệu, nhân công, chi phí chung, hao hụt và chi phí khác.
Ghi chú: Bảng legacy; không phát triển CRUD mới, chỉ phục vụ đối soát/tương thích.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ProductionBatchId | uniqueidentifier | N |  | ProductionBatche | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MaterialCost | decimal(18, 2) | Y |  |  | Chi phí nguyên vật liệu |
| LaborCost | decimal(18, 2) | Y |  |  | Chi phí nhân công |
| OverheadCost | decimal(18, 2) | Y |  |  | Chi phí chung |
| WasteCost | decimal(18, 2) | Y |  |  | Chi phí hao hụt |
| TotalCost | decimal(18, 2) | Y |  |  | Tổng chi phí |
| ProductionOrderId | uniqueidentifier | Y |  | ProductionOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OtherCost | decimal(18, 2) | N | (0) |  | Chi phí khác |
| CurrencyCode | nvarchar(10) | Y |  |  | Mã tiền tệ |
| CalculatedAt | datetime2(7) | N | sysutcdatetime() |  | Thời điểm tính toán |

## AdministrativeDistrict
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Danh mục quận/huyện hoặc đơn vị hành chính cấp tương ứng, liên kết với tỉnh/thành.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(max) | Y |  |  | Mã nhận diện đối tượng. |
| CodeName | nvarchar(max) | Y |  |  | Mã định danh dạng tên |
| NameVN | nvarchar(max) | Y |  |  | Tên tiếng Việt |
| NameEN | nvarchar(max) | Y |  |  | Tên tiếng Anh |
| FullNameVN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| FullNameEN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| AdministrativeProvinceId | uniqueidentifier | Y |  | AdministrativeProvince | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| AdministrativeUnitId | uniqueidentifier | Y |  | AdministrativeUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IsDefault | bit | N | (0) |  | Cờ xác định giá trị mặc định |

## AdministrativeProvince
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Danh mục tỉnh/thành phố hành chính, phục vụ chuẩn hóa địa chỉ.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(max) | Y |  |  | Mã nhận diện đối tượng. |
| CodeName | nvarchar(max) | Y |  |  | Mã định danh dạng tên |
| NameVN | nvarchar(max) | Y |  |  | Tên tiếng Việt |
| NameEN | nvarchar(max) | Y |  |  | Tên tiếng Anh |
| FullNameVN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| FullNameEN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| AdministrativeUnitId | uniqueidentifier | Y |  | AdministrativeUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| AdministrativeRegionId | uniqueidentifier | Y |  | AdministrativeRegion | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IsDefault | bit | N | (0) |  | Cờ xác định giá trị mặc định |

## AdministrativeRegion
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Danh mục vùng/khu vực hành chính.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CodeVN | nvarchar(max) | Y |  |  | Mã nghiệp vụ/mã định danh |
| CodeEN | nvarchar(max) | Y |  |  | Mã nghiệp vụ/mã định danh |
| NameVN | nvarchar(max) | Y |  |  | Tên tiếng Việt |
| NameEN | nvarchar(max) | Y |  |  | Tên tiếng Anh |
| IsDefault | bit | N | (0) |  | Cờ xác định giá trị mặc định |

## AdministrativeUnit
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Danh mục loại/đơn vị hành chính.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CodeNameVN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| CodeNameEN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| FullNameVN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| FullNameEN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| ShortNameVN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| ShortNameEN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| IsDefault | bit | N | (0) |  | Cờ xác định giá trị mặc định |

## AdministrativeWard
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Danh mục phường/xã, liên kết với đơn vị hành chính cấp trên.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(max) | Y |  |  | Mã nhận diện đối tượng. |
| CodeName | nvarchar(max) | Y |  |  | Mã định danh dạng tên |
| NameVN | nvarchar(max) | Y |  |  | Tên tiếng Việt |
| NameEN | nvarchar(max) | Y |  |  | Tên tiếng Anh |
| FullNameVN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| FullNameEN | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| AdministrativeDistrictId | uniqueidentifier | Y |  | AdministrativeDistrict | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| AdministrativeUnitId | uniqueidentifier | Y |  | AdministrativeUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IsDefault | bit | N | (0) |  | Cờ xác định giá trị mặc định |

## AnalyticsRefreshLog
_Audit / Document · HỆ THỐNG / AUDIT / TÀI LIỆU_ – Nhật ký các lần làm mới/tổng hợp dữ liệu phân tích và trạng thái thực thi.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| JobCode | nvarchar(100) | N |  |  | Thông tin JobCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| RunId | uniqueidentifier | N |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StartedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm bắt đầu chạy tác vụ. |
| CompletedAt | datetime2(0) | Y |  |  | Thời điểm/ngày Completed của nghiệp vụ. |
| RunStatus | nvarchar(30) | N |  |  | Thông tin RunStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| RowsProcessed | bigint | Y |  |  | Thông tin RowsProcessed của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| SourceWatermark | nvarchar(500) | Y |  |  | Thông tin SourceWatermark của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ErrorMessage | nvarchar(2000) | Y |  |  | Nội dung thông báo lỗi nếu xử lý thất bại. |

## ApprovalDecision
_Approval · WORKFLOW / PHÊ DUYỆT_ – Ý kiến/quyết định của người phê duyệt tại từng bước.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ApprovalRequestId | uniqueidentifier | N |  | ApprovalRequest | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ApprovalStepId | uniqueidentifier | N |  | ApprovalStep | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DecisionCode | nvarchar(30) | N |  |  | Thông tin DecisionCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DecidedBy | uniqueidentifier | N |  |  | Thông tin DecidedBy của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DecidedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Decided của nghiệp vụ. |
| Comment | nvarchar(2000) | Y |  |  | Thông tin Comment của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| PayloadHash | nvarchar(128) | Y |  |  | Thông tin PayloadHash của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## ApprovalItem
_Approval · WORKFLOW / PHÊ DUYỆT_ – Các đối tượng hoặc dòng dữ liệu nghiệp vụ được gắn với yêu cầu phê duyệt.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ApprovalRequestId | uniqueidentifier | N |  | ApprovalRequest | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ItemType | nvarchar(100) | N |  |  | Thông tin ItemType của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ItemId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ActionCode | nvarchar(50) | N |  |  | Thông tin ActionCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| PayloadJson | nvarchar(max) | Y |  |  | Nội dung dữ liệu gốc/trao đổi được lưu dưới dạng JSON. |
| ItemStatus | nvarchar(30) | Y |  |  | Thông tin ItemStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## ApprovalRequest
_Approval · WORKFLOW / PHÊ DUYỆT_ – Hồ sơ yêu cầu phê duyệt cho một nghiệp vụ hoặc đối tượng nghiệp vụ.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| RequestNo | nvarchar(50) | N |  |  | Thông tin RequestNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| BusinessType | nvarchar(100) | N |  |  | Thông tin BusinessType của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| BusinessId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RequestedBy | uniqueidentifier | Y |  |  | Mã người tạo yêu cầu. |
| RequestedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm tạo/gửi yêu cầu. |
| RequestStatus | nvarchar(30) | N | N'Pending' |  | Trạng thái xử lý yêu cầu. |
| CurrentStepNo | int | Y |  |  | Thông tin CurrentStepNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| PriorityCode | nvarchar(30) | Y |  |  | Thông tin PriorityCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DueAt | datetime2(0) | Y |  |  | Thời điểm/ngày Due của nghiệp vụ. |
| PayloadJson | nvarchar(max) | Y |  |  | Nội dung dữ liệu gốc/trao đổi được lưu dưới dạng JSON. |
| CorrelationId | nvarchar(100) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| CompletedAt | datetime2(0) | Y |  |  | Thời điểm/ngày Completed của nghiệp vụ. |

## ApprovalStep
_Approval · WORKFLOW / PHÊ DUYỆT_ – Các bước phê duyệt trong luồng xử lý của một yêu cầu.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ApprovalRequestId | uniqueidentifier | N |  | ApprovalRequest | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StepNo | int | N |  |  | Thông tin StepNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ApproverUserId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ApproverRoleCode | nvarchar(100) | Y |  |  | Thông tin ApproverRoleCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DecisionStatus | nvarchar(30) | N | N'Waiting' |  | Thông tin DecisionStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| AssignedAt | datetime2(0) | Y |  |  | Thời điểm/ngày Assigned của nghiệp vụ. |
| DueAt | datetime2(0) | Y |  |  | Thời điểm/ngày Due của nghiệp vụ. |
| DecidedAt | datetime2(0) | Y |  |  | Thời điểm/ngày Decided của nghiệp vụ. |
| DecisionNote | nvarchar(2000) | Y |  |  | Thông tin DecisionNote của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## AttendanceEvent
_Nhân sự vận hành · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Ghi nhận sự kiện chấm công vào/ra hoặc các mốc hiện diện của nhân viên.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| EmployeeId | uniqueidentifier | N |  | Employee | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ShiftAssignmentId | uniqueidentifier | Y |  | ShiftAssignment | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| EventType | nvarchar(30) | N |  |  | Loại sự kiện được ghi nhận. |
| EventAt | datetime2(0) | N |  |  | Thời điểm sự kiện xảy ra. |
| Latitude | decimal(10, 7) | Y |  |  | Vĩ độ địa lý. |
| Longitude | decimal(10, 7) | Y |  |  | Kinh độ địa lý. |
| DistanceMeters | decimal(10, 2) | Y |  |  | Thông tin DistanceMeters của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| IsValid | bit | Y |  |  | Cờ Boolean cho biết trạng thái Valid. |
| SourceChannel | nvarchar(50) | Y |  |  | Thông tin SourceChannel của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ExternalEventId | nvarchar(200) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Note | nvarchar(1000) | Y |  |  | Ghi chú. |

## AuditChange
_Audit / Document · HỆ THỐNG / AUDIT / TÀI LIỆU_ – Lưu vết thay đổi/thao tác để phục vụ kiểm tra và truy xuất lịch sử.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| IdChange | uniqueidentifier | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Action | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EntityName | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| Changes | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## BotConfiguration
_Policy · CẤU HÌNH / CHÍNH SÁCH_ – Cấu hình bot theo kênh, môi trường, phạm vi hoạt động và tham chiếu thông tin bí mật.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| IntegrationChannelId | uniqueidentifier | N |  | IntegrationChannel | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| BotCode | nvarchar(50) | N |  |  | Thông tin BotCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| BotName | nvarchar(255) | N |  |  | Thông tin BotName của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| BotUsername | nvarchar(255) | Y |  |  | Thông tin BotUsername của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| TargetAudience | nvarchar(100) | Y |  |  | Thông tin TargetAudience của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| WebhookPath | nvarchar(500) | Y |  |  | Thông tin WebhookPath của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| BotStatus | nvarchar(30) | N | N'Inactive' |  | Thông tin BotStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| LastTokenRotatedAt | datetime2(0) | Y |  |  | Thời điểm/ngày LastTokenRotated của nghiệp vụ. |
| SecretReference | nvarchar(500) | Y |  |  | Tham chiếu đến nơi lưu bí mật/token; không lưu token dạng rõ trong bảng. |
| Note | nvarchar(1000) | Y |  |  | Ghi chú. |

## CleaningBatche
_Vệ sinh · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu nghiệp vụ vệ sinh/rửa khay hoặc dụng cụ sau khi thu hồi.
BRD: **Cleaning Batch** (Return & Reverse Logistics/Transaction) – Quản lý lô vệ sinh/rửa; theo dõi từ vật dụng thu hồi qua vệ sinh, QC đến trạng thái sẵn sàng.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CleaningNo | nvarchar(255) | Y |  |  | **Deprecated → CleaningBatchNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ReturnInspectionId | uniqueidentifier | N |  | ReturnInspection | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StartTime | datetime | Y |  |  | **Deprecated → StartDate**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EndTime | datetime | Y |  |  | **Deprecated → EndDate**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Quantity | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| QcResult | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| CleaningBatchNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| StartDate | datetime2(7) | N | sysutcdatetime() |  | Ngày bắt đầu |
| EndDate | datetime2(7) | Y |  |  | Ngày kết thúc |
| ContainerQuantity | int | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ResponsibleEmployeeId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| QualityStatus | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## CleaningChecklistResult
_Vệ sinh · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Kết quả thực hiện checklist vệ sinh tại khu vực, thiết bị hoặc ca làm việc.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CleaningBatchId | uniqueidentifier | Y |  | CleaningBatche | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| WorkstationId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ChecklistId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ChecklistItemId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ResultCode | nvarchar(30) | N |  |  | Thông tin ResultCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ActualValue | nvarchar(500) | Y |  |  | Giá trị thực tế của Value trong nghiệp vụ. |
| CheckedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Checked của nghiệp vụ. |
| CheckedBy | uniqueidentifier | Y |  |  | Thông tin CheckedBy của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ChemicalUsed | nvarchar(255) | Y |  |  | Thông tin ChemicalUsed của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CorrectiveAction | nvarchar(2000) | Y |  |  | Thông tin CorrectiveAction của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EvidenceUrl | nvarchar(1000) | Y |  |  | Thông tin EvidenceUrl của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## Company
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Lưu thông tin pháp nhân/công ty hoặc đơn vị vận hành.
BRD: **Company** (Organization/Master Data) – Đại diện pháp nhân/doanh nghiệp vận hành hệ thống; lưu mã công ty, tên, mã số thuế, địa chỉ, điện thoại, email và trạng thái.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| TaxCode | nvarchar(100) | Y |  |  | Mã số thuế |
| Address | nvarchar(max) | Y |  |  | Địa chỉ |
| Phone | nvarchar(100) | Y |  |  | Số điện thoại |
| Email | nvarchar(255) | Y |  |  | Địa chỉ thư điện tử |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## ComplaintHistory
_Khiếu nại khách hàng · BẢNG NGHIỆP VỤ (Transaction)_ – Lịch sử xử lý và thay đổi trạng thái của một khiếu nại.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ComplaintId | uniqueidentifier | N |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| EventType | nvarchar(50) | N |  |  | Loại sự kiện được ghi nhận. |
| OldStatus | nvarchar(30) | Y |  |  | Thông tin OldStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| NewStatus | nvarchar(30) | Y |  |  | Thông tin NewStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Comment | nvarchar(2000) | Y |  |  | Thông tin Comment của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EventAt | datetime2(0) | N | sysdatetime() |  | Thời điểm sự kiện xảy ra. |
| ActorId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## ContainerReturn
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin khay/hộp chứa và quá trình cấp phát, thu hồi.
BRD: **Container Return** (Return & Reverse Logistics/Transaction) – Ghi nhận thu hồi hộp, khay, thùng và dụng cụ sau giao hàng.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| DeliveryOrderId | uniqueidentifier | N |  | DeliveryOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ReturnDate | datetime | N |  |  | Ngày/giờ liên quan đến return |
| Quantity | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| CustomerId | uniqueidentifier | Y |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ExpectedQuantity | int | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ReturnedQuantity | int | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DamagedQuantity | int | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## Contract
_Hợp đồng & đơn hàng · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin hợp đồng và các điều khoản giao dịch với khách hàng/đối tác.
BRD: **Contract** (Customer & Commercial/Configuration) – Quản lý hợp đồng giữa công ty và khách hàng; liên kết Customer, Order và chính sách áp dụng.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CustomerId | uniqueidentifier | N |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ContractNo | nvarchar(255) | Y |  |  | **Deprecated → ContractNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| StartDate | datetime | Y |  |  | Ngày bắt đầu |
| EndDate | datetime | Y |  |  | Ngày kết thúc |
| PaymentTerm | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| ContractNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| SignedDate | datetime2(7) | Y |  |  | Ngày/giờ liên quan đến signed |
| EffectiveFrom | datetime2(7) | N | sysutcdatetime() |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveTo | datetime2(7) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ContractValue | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## ContractMeal
_Hợp đồng & đơn hàng · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin hợp đồng và các điều khoản giao dịch với khách hàng/đối tác.
BRD: **Contract Meal** (Customer & Commercial/Configuration) – Quy định loại suất ăn/dịch vụ suất ăn thuộc hợp đồng.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ContractId | uniqueidentifier | N |  | Contract | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MealType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ServiceType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| MealName | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| UnitPrice | decimal(18, 4) | Y |  |  | Đơn giá |
| UnitId | uniqueidentifier | Y |  | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## CoreDataMergeConflict
_Audit / Document · HỆ THỐNG / AUDIT / TÀI LIỆU_ – Ghi nhận xung đột dữ liệu khi hợp nhất các bảng danh mục lõi trong quá trình chuẩn hóa.
_Thiếu cột chuẩn: CreatedDate, CreatedBy, ModifiedDate, ModifiedBy, DeletedDate, DeleteddBy, IsDeleted_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| MigrationCode | nvarchar(50) | N |  |  | Thông tin MigrationCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EntityName | sysname | N |  |  | Thông tin EntityName của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| RecordId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| FieldName | sysname | N |  |  | Thông tin FieldName của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CanonicalValue | nvarchar(max) | Y |  |  | Thông tin CanonicalValue của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| LegacyValue | nvarchar(max) | Y |  |  | Thông tin LegacyValue của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DetectedAt | datetime2(7) | N | sysutcdatetime() |  | Thời điểm/ngày Detected của nghiệp vụ. |
| ResolvedAt | datetime2(7) | Y |  |  | Thời điểm hoàn tất xử lý. |
| ResolutionNote | nvarchar(1000) | Y |  |  | Thông tin ResolutionNote của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## Customer
_Khách hàng & nhà cung cấp · DANH MỤC (Master Data)_ – Lưu hồ sơ khách hàng/đối tác sử dụng dịch vụ suất ăn và thông tin định danh liên quan.
BRD: **Customer** (Customer/Master Data) – Quản lý khách hàng sử dụng dịch vụ suất ăn: mã, tên, loại khách hàng, thông tin liên hệ, địa điểm giao hàng và trạng thái.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| CustomerType | nvarchar(20) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | N |  |  | Trạng thái bản ghi/đối tượng. |
| CIF | nvarchar(50) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DisplayName | nvarchar(255) | N |  |  | Tên hiển thị của đối tượng |
| PhoneNumber | nvarchar(50) | Y |  |  | Thông tin số điện thoại |
| Email | nvarchar(255) | Y |  |  | Địa chỉ thư điện tử |
| Address | nvarchar(500) | Y |  |  | Địa chỉ |
| TaxCode | nvarchar(100) | Y |  |  | Mã số thuế |
| FirstName | nvarchar(100) | Y |  |  | Tên hiển thị của đối tượng |
| LastName | nvarchar(100) | Y |  |  | Tên hiển thị của đối tượng |
| Gender | nvarchar(20) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DateOfBirth | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| IdentityNumber | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| IdentityDate | datetime | Y |  |  | Ngày/giờ liên quan đến identity |
| IdentityPlace | nvarchar(255) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Occupation | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| LegalName | nvarchar(255) | Y |  |  | Tên hiển thị của đối tượng |
| BusinessRegistrationNumber | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RegistrationDate | datetime | Y |  |  | Ngày/giờ liên quan đến registration |
| RegistrationPlace | nvarchar(255) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| CompanySize | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Industry | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| LegalRepresentativeName | nvarchar(255) | Y |  |  | Tên hiển thị của đối tượng |
| ContactPersonName | nvarchar(255) | Y |  |  | Tên hiển thị của đối tượng |
| ContactPersonPhone | nvarchar(50) | Y |  |  | Thông tin số điện thoại |
| ContactPersonEmail | nvarchar(255) | Y |  |  | Thông tin địa chỉ email |
| Segment | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Source | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RegionId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| AreaId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| BranchId | uniqueidentifier | Y |  | OrganizationUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DepartmentId | uniqueidentifier | Y |  | OrganizationUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| UserAssignId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| ExternalData | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ExternalValue | nvarchar(500) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## CustomerComplaint
_Khiếu nại khách hàng · BẢNG NGHIỆP VỤ (Transaction)_ – Hồ sơ khiếu nại/phản ánh của khách hàng về suất ăn, giao hàng hoặc dịch vụ.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ComplaintNo | nvarchar(50) | N |  |  | Thông tin ComplaintNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CustomerId | uniqueidentifier | N |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrderId | uniqueidentifier | Y |  | Order | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryOrderId | uniqueidentifier | Y |  | DeliveryOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryRouteId | uniqueidentifier | Y |  | DeliveryRoute | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| SourceChannel | nvarchar(50) | Y |  |  | Thông tin SourceChannel của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CategoryCode | nvarchar(50) | Y |  |  | Thông tin CategoryCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Severity | nvarchar(30) | N | N'Medium' |  | Thông tin Severity của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Description | nvarchar(2000) | N |  |  | Thông tin mô tả. |
| ReceivedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm tiếp nhận khiếu nại/tin nhắn. |
| SlaDueAt | datetime2(0) | Y |  |  | Thời điểm/ngày SlaDue của nghiệp vụ. |
| ComplaintStatus | nvarchar(30) | N | N'Open' |  | Trạng thái xử lý khiếu nại. |
| AssignedTo | uniqueidentifier | Y |  |  | Thông tin AssignedTo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ResolvedAt | datetime2(0) | Y |  |  | Thời điểm hoàn tất xử lý. |
| Resolution | nvarchar(2000) | Y |  |  | Thông tin Resolution của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## CustomerContact
_Khách hàng & nhà cung cấp · DANH MỤC (Master Data)_ – Lưu hồ sơ khách hàng/đối tác sử dụng dịch vụ suất ăn và thông tin định danh liên quan.
BRD: **Customer Contact** (Customer/Master Data) – Quản lý các đầu mối liên hệ của khách hàng.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CustomerId | uniqueidentifier | N |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Name | nvarchar(500) | Y |  |  | Tên đối tượng. |
| Position | nvarchar(255) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Phone | nvarchar(100) | Y |  |  | Số điện thoại |
| Email | nvarchar(255) | Y |  |  | Địa chỉ thư điện tử |
| IsPrimary | bit | Y |  |  | Cờ logic xác định primary (thường 1: có/đúng, 0: không/sai) |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| FullName | nvarchar(max) | Y |  |  | Tên đầy đủ |

## CustomerDeliveryLocation
_Khách hàng & nhà cung cấp · DANH MỤC (Master Data)_ – Lưu hồ sơ khách hàng/đối tác sử dụng dịch vụ suất ăn và thông tin định danh liên quan.
BRD: **Delivery Location** (Customer/Master Data) – Quản lý địa điểm giao suất ăn của khách hàng; là thành phần liên kết trong phân bổ Menu và đơn hàng.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CustomerId | uniqueidentifier | N |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(500) | Y |  |  | Tên đối tượng. |
| Address | nvarchar(max) | Y |  |  | Địa chỉ |
| ContactName | nvarchar(500) | Y |  |  | Tên hiển thị của đối tượng |
| ContactPhone | nvarchar(100) | Y |  |  | Thông tin số điện thoại |
| Latitude | decimal(18, 10) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Longitude | decimal(18, 10) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DeliveryWindowFrom | time(7) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DeliveryWindowTo | time(7) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| DeliveryInstructions | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## DeliveryEvent
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Lịch sử sự kiện phát sinh trong quá trình giao hàng như xuất phát, đến điểm, giao thành công/thất bại.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| DeliveryRouteId | uniqueidentifier | N |  | DeliveryRoute | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryStopId | uniqueidentifier | Y |  | DeliveryStop | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryOrderId | uniqueidentifier | Y |  | DeliveryOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| EventType | nvarchar(50) | N |  |  | Loại sự kiện được ghi nhận. |
| EventAt | datetime2(0) | N | sysdatetime() |  | Thời điểm sự kiện xảy ra. |
| Latitude | decimal(10, 7) | Y |  |  | Vĩ độ địa lý. |
| Longitude | decimal(10, 7) | Y |  |  | Kinh độ địa lý. |
| TemperatureC | decimal(6, 2) | Y |  |  | Thông tin TemperatureC của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| PhotoUrl | nvarchar(1000) | Y |  |  | Thông tin PhotoUrl của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Description | nvarchar(2000) | Y |  |  | Thông tin mô tả. |
| RecordedBy | uniqueidentifier | Y |  |  | Thông tin RecordedBy của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| SourceChannel | nvarchar(50) | Y |  |  | Thông tin SourceChannel của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ExternalEventId | nvarchar(200) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## DeliveryExecution
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu dữ liệu giao hàng, điều phối tuyến và thực hiện giao suất ăn.
BRD: **Delivery Execution** (Logistics/Transaction) – Theo dõi quá trình giao với các trạng thái READY, LOADED, DISPATCHED, ARRIVED, DELIVERED hoặc FAILED.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| DeliveryOrderId | uniqueidentifier | N |  | DeliveryOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryRouteId | uniqueidentifier | Y |  | DeliveryRoute | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| DispatchedAt | datetime | Y |  |  | Ngày/giờ liên quan đến dispatched |
| ArrivedAt | datetime | Y |  |  | Ngày/giờ liên quan đến arrived |
| DeliveredAt | datetime | Y |  |  | Ngày/giờ liên quan đến delivered |
| Remark | nvarchar(max) | Y |  |  | Diễn giải/ghi chú |
| DepartedAt | datetime2(7) | Y |  |  | Ngày/giờ liên quan đến departed |
| DriverName | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| VehicleCode | nvarchar(max) | Y |  |  | Mã nghiệp vụ/mã định danh |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## DeliveryLoading
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu dữ liệu giao hàng, điều phối tuyến và thực hiện giao suất ăn.
BRD: **Delivery Loading** (Logistics/Transaction) – Xác nhận hàng được xếp lên xe; liên kết Packing và Vehicle.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| DeliveryRouteId | uniqueidentifier | N |  | DeliveryRoute | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryOrderId | uniqueidentifier | N |  | DeliveryOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LoadedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| LoadingTime | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| EmployeeId | uniqueidentifier | Y |  | Employee | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| VehicleCode | nvarchar(max) | Y |  |  | Mã nghiệp vụ/mã định danh |
| LoadingDate | datetime2(7) | N | sysutcdatetime() |  | Ngày/giờ liên quan đến loading |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## DeliveryOrder
_Hợp đồng & đơn hàng · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin đơn hàng/yêu cầu cung cấp suất ăn hoặc hàng hóa theo kỳ/ngày.
BRD: **Delivery Order** (Logistics/Transaction) – Đơn giao hàng liên kết với Order; một Order có thể được giao thành nhiều Delivery nếu nghiệp vụ cho phép.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| DeliveryNo | nvarchar(255) | Y |  |  | **Deprecated → DeliveryNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| OrderId | uniqueidentifier | N |  | Order | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| CustomerId | uniqueidentifier | N |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryLocationId | uniqueidentifier | Y |  |  | **Deprecated → CustomerDeliveryLocationId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryDate | datetime | N |  |  | Ngày giao hàng |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| CustomerDeliveryLocationId | uniqueidentifier | Y |  | CustomerDeliveryLocation | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryRouteId | uniqueidentifier | Y |  | DeliveryRoute | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PlannedDeliveryTime | datetime2(7) | N | sysutcdatetime() |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ActualDeliveryTime | datetime2(7) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## DeliveryRoute
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu dữ liệu giao hàng, điều phối tuyến và thực hiện giao suất ăn.
BRD: **Delivery Route** (Logistics/Transaction) – Tuyến giao hàng; gồm ngày, xe, tài xế, điểm đầu/cuối, thời gian dự kiến và trạng thái.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| RouteNo | nvarchar(255) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DeliveryDate | datetime | N |  |  | Ngày giao hàng |
| VehicleId | uniqueidentifier | Y |  | Vehicle | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DriverId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PlannedStart | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PlannedEnd | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ActualStart | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ActualEnd | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Code | nvarchar(max) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| Description | nvarchar(max) | Y |  |  | Thông tin mô tả. |

## DeliveryStop
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Điểm giao hàng thuộc một tuyến giao, phục vụ điều phối nhiều điểm giao.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| DeliveryRouteId | uniqueidentifier | N |  | DeliveryRoute | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryOrderId | uniqueidentifier | Y |  | DeliveryOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| CustomerId | uniqueidentifier | Y |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryLocationId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| SequenceNo | int | N |  |  | Thông tin SequenceNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| PlannedArrival | datetime2(0) | Y |  |  | Thông tin PlannedArrival của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ActualArrival | datetime2(0) | Y |  |  | Giá trị thực tế của Arrival trong nghiệp vụ. |
| PlannedMealQty | decimal(18, 3) | Y |  |  | Thông tin PlannedMealQty của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DeliveredMealQty | decimal(18, 3) | Y |  |  | Thông tin DeliveredMealQty của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ShortageQty | decimal(18, 3) | Y |  |  | Thông tin ShortageQty của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| StopStatus | nvarchar(30) | N | N'Planned' |  | Thông tin StopStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Note | nvarchar(1000) | Y |  |  | Ghi chú. |

## DishCategorie
_Món ăn & công thức · DANH MỤC (Master Data)_ – Danh mục món ăn
BRD: **Dish Category** (Product/Reference Data) – Danh mục nhóm món ăn.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| ParentId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Description | nvarchar(max) | Y |  |  | Thông tin mô tả. |
| ProductionLineId | uniqueidentifier | Y |  | ProductionLine | **UI-6.26 (D-107, GAP-PRD-10 anh duyệt 2026-11-24)** – dây chuyền nấu các món của nhóm; ra lệnh chia mẻ theo công suất / phút mỗi mẻ của dây chuyền. Không FK vật lý. |

## Dishe
_Món ăn & công thức · DANH MỤC (Master Data)_ – Món ăn
BRD: **Dish** (Product/Master Data) – Danh mục món ăn được chế biến.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| CategoryId | uniqueidentifier | Y |  |  | **Deprecated → DishCategoryId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MealCategory | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Description | nvarchar(max) | Y |  |  | Thông tin mô tả. |
| StandardPortion | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| StandardCost | decimal(18, 2) | Y |  |  | Giá trị tiền/chi phí/đơn giá |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| DishCategoryId | uniqueidentifier | Y |  | DishCategorie | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DefaultUnitId | uniqueidentifier | Y |  | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StandardServingSize | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## DocumentFile
_Audit / Document · HỆ THỐNG / AUDIT / TÀI LIỆU_ – Chưa có mô tả nghiệp vụ đủ căn cứ trong nguồn đầu vào; cần BA xác nhận.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| TestDemoId | uniqueidentifier | Y |  | TestDemo | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| FileName | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| FileSize | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| FileType | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| FileNameHas | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |

## Employee
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Lưu hồ sơ nhân sự/nhân viên tham gia vận hành.
BRD: **Employee** (Resource/Master Data) – Quản lý nhân viên tham gia vận hành.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| EmployeeCode | nvarchar(255) | Y |  |  | **Deprecated → Code**. Mã nghiệp vụ/mã định danh |
| FullName | nvarchar(500) | Y |  |  | Tên đầy đủ |
| OrganizationUnitId | uniqueidentifier | Y |  | OrganizationUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PositionId | uniqueidentifier | Y |  | Position | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Phone | nvarchar(100) | Y |  |  | Số điện thoại |
| Email | nvarchar(255) | Y |  |  | Địa chỉ thư điện tử |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Code | nvarchar(max) | Y |  |  | Mã nhận diện đối tượng. |
| DateOfBirth | datetime2(7) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Gender | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Address | nvarchar(max) | Y |  |  | Địa chỉ |
| HireDate | datetime2(7) | Y |  |  | Ngày/giờ liên quan đến hire |

## FoodSafetyChecklist
_An toàn thực phẩm · DANH MỤC (Master Data)_ – Danh mục bộ checklist kiểm soát an toàn thực phẩm theo quy trình/đối tượng áp dụng.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(50) | N |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(255) | N |  |  | Tên đối tượng. |
| VersionNo | int | N | (1) |  | Thông tin VersionNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| AreaCode | nvarchar(50) | Y |  |  | Thông tin AreaCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| FrequencyCode | nvarchar(30) | Y |  |  | Thông tin FrequencyCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EffectiveFrom | date | Y |  |  | Thông tin EffectiveFrom của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EffectiveTo | date | Y |  |  | Thông tin EffectiveTo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Status | nvarchar(30) | N | N'Active' |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(1000) | Y |  |  | Ghi chú. |

## FoodSafetyChecklistItem
_An toàn thực phẩm · DANH MỤC (Master Data)_ – Các tiêu chí/câu hỏi thành phần của một checklist an toàn thực phẩm.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ChecklistId | uniqueidentifier | N |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ItemCode | nvarchar(50) | N |  |  | Thông tin ItemCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ItemName | nvarchar(500) | N |  |  | Thông tin ItemName của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Description | nvarchar(1000) | Y |  |  | Thông tin mô tả. |
| SortOrder | int | N | (0) |  | Thông tin SortOrder của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| IsCritical | bit | N | (0) |  | Cờ Boolean cho biết trạng thái Critical. |
| ExpectedValue | nvarchar(500) | Y |  |  | Giá trị dự kiến của Value trong nghiệp vụ. |
| UnitCode | nvarchar(30) | Y |  |  | Thông tin UnitCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| IsRequired | bit | N | (1) |  | Cờ Boolean cho biết trạng thái Required. |

## FoodSafetyChecklistResult
_QC & an toàn thực phẩm · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Kết quả thực hiện checklist theo ngày, ca, người thực hiện và trạng thái đạt/không đạt.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ChecklistId | uniqueidentifier | N |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ChecklistItemId | uniqueidentifier | N |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| CheckDate | date | N |  |  | Ngày thực hiện kiểm tra. |
| ShiftId | uniqueidentifier | Y |  | Shift | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ProductionBatchId | uniqueidentifier | Y |  | ProductionBatche | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| WorkstationId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ResultCode | nvarchar(30) | N |  |  | Thông tin ResultCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ActualValue | nvarchar(500) | Y |  |  | Giá trị thực tế của Value trong nghiệp vụ. |
| CheckedBy | uniqueidentifier | Y |  |  | Thông tin CheckedBy của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CheckedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Checked của nghiệp vụ. |
| CorrectiveAction | nvarchar(2000) | Y |  |  | Thông tin CorrectiveAction của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EvidenceUrl | nvarchar(1000) | Y |  |  | Thông tin EvidenceUrl của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## Forecast
_Thực đơn & kế hoạch · BẢNG NGHIỆP VỤ (Transaction)_ – Dự báo
BRD: **Forecast** (Demand & Order/Transaction) – Dự báo nhu cầu suất ăn theo Customer, Delivery Location, Date, Shift, Meal Type, Quantity, Version và Status.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CustomerId | uniqueidentifier | N |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryLocationId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ForecastDate | datetime | N |  |  | Ngày/giờ liên quan đến forecast |
| ShiftId | uniqueidentifier | Y |  | Shift | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MealType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Quantity | decimal(18, 6) | N |  |  | **Deprecated → ForecastQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VersionNo | int | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| ForecastQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## GoodsReceipt
_Mua hàng & nhập kho · BẢNG NGHIỆP VỤ (Transaction)_ – Nhập kho hàng hóa
BRD: **Goods Receipt** (Receiving & Quality/Transaction) – Phiếu nhập hàng, phát sinh từ Purchase Order.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ReceiptNo | nvarchar(255) | Y |  |  | **Deprecated → ReceiptNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PurchaseOrderId | uniqueidentifier | Y |  | PurchaseOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| SupplierId | uniqueidentifier | N |  | Supplier | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| WarehouseId | uniqueidentifier | N |  | Warehouse | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ReceiptDate | datetime | N |  |  | Ngày/giờ liên quan đến receipt |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| ReceiptNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## GoodsReceiptLine
_Mua hàng & nhập kho · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Dòng chi tiết nhập kho hàng hóa
BRD: **Goods Receipt Line** (Receiving & Quality/Transaction) – Chi tiết nhập gồm Ingredient, Quantity, UOM, Lot, Manufacturing Date, Expiry Date và Unit Cost.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| GoodsReceiptId | uniqueidentifier | N |  | GoodsReceipt | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Quantity | decimal(18, 6) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UomId | uniqueidentifier | N |  |  | **Deprecated → UnitId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| UnitCost | decimal(18, 2) | Y |  |  | Giá trị tiền/chi phí/đơn giá |
| LotId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ReceivedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AcceptedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RejectedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LotNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ManufactureDate | datetime2(7) | Y |  |  | Ngày sản xuất |
| ExpiryDate | datetime2(7) | Y |  |  | Ngày hết hạn |
| QualityStatus | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## InboundMessage
_Message · TÍCH HỢP / MESSAGE_ – Lưu tin nhắn/sự kiện nhận vào từ bot hoặc nền tảng tích hợp để xử lý.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| IntegrationChannelId | uniqueidentifier | N |  | IntegrationChannel | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ExternalMessageId | nvarchar(255) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ExternalConversationId | nvarchar(255) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ExternalUserId | nvarchar(255) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MessageType | nvarchar(50) | Y |  |  | Thông tin MessageType của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| PayloadJson | nvarchar(max) | Y |  |  | Nội dung dữ liệu gốc/trao đổi được lưu dưới dạng JSON. |
| ReceivedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm tiếp nhận khiếu nại/tin nhắn. |
| ProcessingStatus | nvarchar(30) | N | N'Received' |  | Trạng thái tiếp nhận/xử lý thông điệp. |
| IdempotencyKey | nvarchar(200) | Y |  |  | Thông tin IdempotencyKey của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CorrelationId | nvarchar(100) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ErrorMessage | nvarchar(2000) | Y |  |  | Nội dung thông báo lỗi nếu xử lý thất bại. |

## Ingredient
_Nguyên liệu & đơn vị · DANH MỤC (Master Data)_ – Nguyên liệu
BRD: **Ingredient** (Material/Master Data) – Danh mục nguyên liệu sử dụng trong chế biến; liên quan nhóm nguyên liệu, UOM, chính sách bảo quản, hạn sử dụng và chính sách chất lượng.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| CategoryId | uniqueidentifier | Y |  |  | **Deprecated → IngredientCategoryId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| BaseUomId | uniqueidentifier | Y |  |  | **Deprecated → BaseUnitId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PurchaseUomId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IssueUomId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StorageType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ShelfLifeDays | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| YieldPercent | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| StandardWastePercent | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| LotControl | bit | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ExpiryControl | bit | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| QcRequired | bit | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientCategoryId | uniqueidentifier | Y |  | IngredientCategorie | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| BaseUnitId | uniqueidentifier | Y |  | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Specification | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| StorageCondition | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| IsExpiryManaged | bit | N | (0) |  | Cờ logic xác định expiry managed (thường 1: có/đúng, 0: không/sai) |
| MinimumStock | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| SafetyStock | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## IngredientCategorie
_Nguyên liệu & đơn vị · DANH MỤC (Master Data)_ – Danh mục nguyên liệu
BRD: **Ingredient Category** (Material/Reference Data) – Danh mục nhóm/phân loại nguyên liệu.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| ParentId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| Description | nvarchar(max) | Y |  |  | Thông tin mô tả. |

## IntegrationChannel
_Tích hợp & KPI · DANH MỤC (Master Data)_ – Danh mục kênh tích hợp như Zalo, Telegram, Facebook hoặc các kênh hệ thống khác.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ChannelCode | nvarchar(50) | N |  |  | Thông tin ChannelCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ChannelName | nvarchar(255) | N |  |  | Thông tin ChannelName của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ProviderCode | nvarchar(50) | N |  |  | Thông tin ProviderCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EnvironmentCode | nvarchar(30) | N | N'Production' |  | Thông tin EnvironmentCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ChannelStatus | nvarchar(30) | N | N'Inactive' |  | Thông tin ChannelStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ConfigJson | nvarchar(max) | Y |  |  | Thông tin ConfigJson của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| SecretReference | nvarchar(500) | Y |  |  | Tham chiếu đến nơi lưu bí mật/token; không lưu token dạng rõ trong bảng. |
| OwnerUserId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Note | nvarchar(1000) | Y |  |  | Ghi chú. |

## IntegrationLog
_Message · TÍCH HỢP / MESSAGE_ – Nhật ký giao tiếp tích hợp, phục vụ truy vết request/response và lỗi.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| IntegrationChannelId | uniqueidentifier | Y |  | IntegrationChannel | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DirectionCode | nvarchar(20) | N |  |  | Thông tin DirectionCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| OperationCode | nvarchar(100) | N |  |  | Thông tin OperationCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CorrelationId | nvarchar(100) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IdempotencyKey | nvarchar(200) | Y |  |  | Thông tin IdempotencyKey của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| RequestAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Request của nghiệp vụ. |
| ResponseAt | datetime2(0) | Y |  |  | Thời điểm/ngày Response của nghiệp vụ. |
| HttpStatus | int | Y |  |  | Thông tin HttpStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| LogStatus | nvarchar(30) | N |  |  | Thông tin LogStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ErrorCode | nvarchar(100) | Y |  |  | Thông tin ErrorCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ErrorMessage | nvarchar(2000) | Y |  |  | Nội dung thông báo lỗi nếu xử lý thất bại. |
| PayloadHash | nvarchar(128) | Y |  |  | Thông tin PayloadHash của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## InventoryBalance
_Kho & cấp phát · BẢNG NGHIỆP VỤ (Transaction)_ – Số dư tồn kho
BRD: **Inventory Balance** (Lot & Inventory/Result) – Số tồn kho được tính từ Inventory Transaction; không phải nguồn ghi nhận biến động gốc.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| WarehouseId | uniqueidentifier | N |  | Warehouse | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StorageLocationId | uniqueidentifier | Y |  | StorageLocation | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LotId | uniqueidentifier | Y |  |  | **Deprecated → MaterialLotId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OnHandQty | decimal(18, 6) | N |  |  | **Deprecated → OnHandQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ReservedQty | decimal(18, 6) | N |  |  | **Deprecated → ReservedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AvailableQty | decimal(18, 6) | N |  |  | **Deprecated → AvailableQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UpdatedDate | datetime | N |  |  | Ngày/giờ liên quan đến updated |
| MaterialLotId | uniqueidentifier | Y |  | MaterialLot | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OnHandQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ReservedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AvailableQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LastCalculatedDate | datetime2(7) | N | sysutcdatetime() |  | Ngày/giờ liên quan đến last calculated |

## InventoryPolicie
_Nguyên liệu & đơn vị · DANH MỤC (Master Data)_ – Chính sách tồn kho
BRD: **Inventory Policy** (Warehouse/Configuration) – Cấu hình chính sách tồn kho như Safety Stock, Reorder Point và các ngưỡng quản lý tồn.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| WarehouseId | uniqueidentifier | N |  | Warehouse | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MinStock | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| MaxStock | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| SafetyStock | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ReorderPoint | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| CriticalLevel | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| LeadTimeDays | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| MinimumStock | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| MaximumStock | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UseFefo | bit | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ExpiryWarningDays | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## InventoryTransaction
_Kho & cấp phát · BẢNG NGHIỆP VỤ (Transaction)_ – Giao dịch tồn kho
BRD: **Inventory Transaction** (Lot & Inventory/Transaction) – Sổ ghi nhận biến động kho (ledger): RECEIPT, ISSUE, TRANSFER_IN, TRANSFER_OUT, ADJUSTMENT, WASTE và RETURN.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| TransactionNo | nvarchar(255) | Y |  |  | **Deprecated → TransactionNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| TransactionType | nvarchar(50) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| WarehouseId | uniqueidentifier | N |  | Warehouse | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StorageLocationId | uniqueidentifier | Y |  | StorageLocation | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LotId | uniqueidentifier | Y |  |  | **Deprecated → MaterialLotId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ReferenceType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ReferenceId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Quantity | decimal(18, 6) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UomId | uniqueidentifier | N |  |  | **Deprecated → UnitId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| UnitCost | decimal(18, 2) | Y |  |  | Giá trị tiền/chi phí/đơn giá |
| TransactionDate | datetime | N |  |  | Ngày/giờ liên quan đến transaction |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| MaterialLotId | uniqueidentifier | Y |  | MaterialLot | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| TransactionNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| QuantityIn | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| QuantityOut | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Reason | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## KitchenWorkstation
_Vận hành bếp & phương tiện · DANH MỤC (Master Data)_ – Danh mục vị trí/trạm làm việc trong bếp; có thể định vị bằng tọa độ và bán kính.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(50) | N |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(255) | N |  |  | Tên đối tượng. |
| AreaName | nvarchar(255) | Y |  |  | Tên khu vực/vị trí trong bếp. |
| Latitude | decimal(10, 7) | Y |  |  | Vĩ độ địa lý. |
| Longitude | decimal(10, 7) | Y |  |  | Kinh độ địa lý. |
| RadiusMeters | decimal(10, 2) | Y |  |  | Bán kính tính bằng mét dùng để xác định vùng hợp lệ quanh vị trí. |
| Status | nvarchar(30) | N | N'Active' |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(1000) | Y |  |  | Ghi chú. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrganizationUnitId | uniqueidentifier | Y |  | OrganizationUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## KpiDailySnapshot
_KPI Snapshot · BÁO CÁO / KPI SNAPSHOT_ – Ảnh chụp số liệu KPI theo ngày tại thời điểm tổng hợp.
BRD: **Financial KPI** (KPI & Analytics/Result) – Theo dõi Cost/Meal, Food Cost %, Waste Cost, Margin/Meal và Margin %.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| SnapshotDate | date | N |  |  | Ngày chốt số liệu KPI. |
| BranchId | uniqueidentifier | Y |  | OrganizationUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MetricCode | nvarchar(100) | N |  |  | Mã định danh duy nhất của chỉ tiêu KPI. |
| MetricVersion | int | N |  |  | Thông tin MetricVersion của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| MetricValue | decimal(28, 6) | Y |  |  | Giá trị KPI được tổng hợp. |
| Numerator | decimal(28, 6) | Y |  |  | Thông tin Numerator của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Denominator | decimal(28, 6) | Y |  |  | Thông tin Denominator của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| TargetValue | decimal(28, 6) | Y |  |  | Thông tin TargetValue của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| VarianceValue | decimal(28, 6) | Y |  |  | Thông tin VarianceValue của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CalculationVersion | nvarchar(50) | N |  |  | Thông tin CalculationVersion của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CalculatedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Calculated của nghiệp vụ. |
| SourceWatermark | nvarchar(500) | Y |  |  | Thông tin SourceWatermark của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DataQualityStatus | nvarchar(30) | Y |  |  | Thông tin DataQualityStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## KpiMetricDefinition
_Tích hợp & KPI · DANH MỤC (Master Data)_ – Định nghĩa chỉ tiêu KPI, mã chỉ tiêu, đơn vị tính, công thức/tham số cấu hình.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| MetricCode | nvarchar(100) | N |  |  | Mã định danh duy nhất của chỉ tiêu KPI. |
| MetricName | nvarchar(255) | N |  |  | Tên hiển thị của chỉ tiêu KPI. |
| DomainCode | nvarchar(50) | N |  |  | Thông tin DomainCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| UnitCode | nvarchar(30) | Y |  |  | Thông tin UnitCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| FormulaDescription | nvarchar(2000) | N |  |  | Thông tin FormulaDescription của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| AggregationCode | nvarchar(30) | Y |  |  | Thông tin AggregationCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| VersionNo | int | N | (1) |  | Thông tin VersionNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EffectiveFrom | date | N |  |  | Thông tin EffectiveFrom của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EffectiveTo | date | Y |  |  | Thông tin EffectiveTo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| IsActive | bit | N | (1) |  | Cờ trạng thái hoạt động. |

## KpiMonthlySnapshot
_KPI Snapshot · BÁO CÁO / KPI SNAPSHOT_ – Ảnh chụp số liệu KPI theo tháng tại thời điểm tổng hợp.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| SnapshotMonth | date | N |  |  | Tháng chốt số liệu KPI. |
| BranchId | uniqueidentifier | Y |  | OrganizationUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MetricCode | nvarchar(100) | N |  |  | Mã định danh duy nhất của chỉ tiêu KPI. |
| MetricVersion | int | N |  |  | Thông tin MetricVersion của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| MetricValue | decimal(28, 6) | Y |  |  | Giá trị KPI được tổng hợp. |
| Numerator | decimal(28, 6) | Y |  |  | Thông tin Numerator của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Denominator | decimal(28, 6) | Y |  |  | Thông tin Denominator của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| TargetValue | decimal(28, 6) | Y |  |  | Thông tin TargetValue của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| VarianceValue | decimal(28, 6) | Y |  |  | Thông tin VarianceValue của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CalculationVersion | nvarchar(50) | N |  |  | Thông tin CalculationVersion của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CalculatedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Calculated của nghiệp vụ. |
| SourceWatermark | nvarchar(500) | Y |  |  | Thông tin SourceWatermark của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DataQualityStatus | nvarchar(30) | Y |  |  | Thông tin DataQualityStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## LeaveRequest
_Nhân sự vận hành · BẢNG NGHIỆP VỤ (Transaction)_ – Yêu cầu nghỉ phép/nghỉ ca của nhân viên và trạng thái phê duyệt.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| RequestNo | nvarchar(50) | N |  |  | Thông tin RequestNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EmployeeId | uniqueidentifier | N |  | Employee | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| FromDate | date | N |  |  | Thời điểm/ngày From của nghiệp vụ. |
| ToDate | date | N |  |  | Thời điểm/ngày To của nghiệp vụ. |
| LeaveTypeCode | nvarchar(50) | N |  |  | Thông tin LeaveTypeCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Reason | nvarchar(1000) | Y |  |  | Lý do phát sinh yêu cầu/sự kiện. |
| RequestStatus | nvarchar(30) | N | N'Pending' |  | Trạng thái xử lý yêu cầu. |
| ApprovalRequestId | uniqueidentifier | Y |  | ApprovalRequest | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| SubmittedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Submitted của nghiệp vụ. |

## LocationTracking
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ (Transaction)_ – Theo dõi vị trí
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| IPv4 | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PcName | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |

## MaterialConsumptionVariance
_Kho & cấp phát · BẢNG NGHIỆP VỤ (Transaction)_ – Chênh lệch tiêu hao vật tư
BRD: **Material Consumption Variance** (Reconciliation/Result) – Đối soát tiêu hao nguyên liệu lý thuyết với tiêu hao thực tế.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ProductionOrderId | uniqueidentifier | N |  | ProductionOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| TheoreticalQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ActualQty | decimal(18, 6) | Y |  |  | **Deprecated → ActualQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VarianceQty | decimal(18, 6) | Y |  |  | **Deprecated → VarianceQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VariancePercent | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VarianceValue | decimal(18, 2) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Reason | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| StandardQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ActualQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VarianceQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VarianceCost | decimal(18, 4) | Y |  |  | Giá trị tiền/chi phí/đơn giá |

## MaterialIssue
_Kho & cấp phát · BẢNG NGHIỆP VỤ (Transaction)_ – Xuất vật tư
BRD: **Material Issue** (Production/Transaction) – Xuất nguyên liệu thực tế cho sản xuất; liên kết Production Order và Inventory Transaction.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| IssueNo | nvarchar(255) | Y |  |  | **Deprecated → IssueNumber**. Cờ logic xác định sue no (thường 1: có/đúng, 0: không/sai) |
| ProductionOrderId | uniqueidentifier | N |  | ProductionOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| WarehouseId | uniqueidentifier | N |  | Warehouse | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IssueDate | datetime | N |  |  | Cờ logic xác định sue date (thường 1: có/đúng, 0: không/sai) |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| IssueNumber | nvarchar(max) | Y |  |  | Cờ logic xác định sue number (thường 1: có/đúng, 0: không/sai) |

## MaterialIssueLine
_Kho & cấp phát · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Dòng chi tiết xuất vật tư
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| MaterialIssueId | uniqueidentifier | N |  | MaterialIssue | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LotId | uniqueidentifier | Y |  |  | **Deprecated → MaterialLotId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Quantity | decimal(18, 6) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UomId | uniqueidentifier | N |  |  | **Deprecated → UnitId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MaterialLotId | uniqueidentifier | Y |  | MaterialLot | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## MaterialLot
_Mua hàng & nhập kho · BẢNG NGHIỆP VỤ (Transaction)_ – Lô vật tư
BRD: **Material Lot** (Lot & Inventory/Master Data / Traceability) – Quản lý LOT nguyên liệu; gồm Ingredient, Lot Number, Supplier, Manufacturing Date, Expiry Date, Received Date, Unit Cost và QC Status.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| LotNo | nvarchar(255) | N |  |  | **Deprecated → LotNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| SupplierId | uniqueidentifier | Y |  | Supplier | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ManufactureDate | datetime | Y |  |  | Ngày sản xuất |
| ExpiryDate | datetime | Y |  |  | Ngày hết hạn |
| ReceivedDate | datetime | Y |  |  | Ngày/giờ liên quan đến received |
| UnitCost | decimal(18, 2) | Y |  |  | Giá trị tiền/chi phí/đơn giá |
| QcStatus | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| GoodsReceiptLineId | uniqueidentifier | Y |  | GoodsReceiptLine | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LotNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| QualityStatus | nvarchar(max) | Y |  |  | **Deprecated → QcStatus**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## MaterialRequirement
_Kho & cấp phát · BẢNG NGHIỆP VỤ (Transaction)_ – Nhu cầu vật tư
BRD: **Material Requirement** (Procurement/Transaction) – Nhu cầu nguyên liệu tính từ kế hoạch sản xuất, Recipe, Yield, Safety Stock, tồn khả dụng và hàng dự kiến nhập.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| RequirementNo | nvarchar(255) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ProductionPlanId | uniqueidentifier | N |  | ProductionPlan | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RequiredQty | decimal(18, 6) | N |  |  | **Deprecated → RequiredQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AvailableQty | decimal(18, 6) | Y |  |  | **Deprecated → AvailableQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ExpectedIncomingQty | decimal(18, 6) | Y |  |  | **Deprecated → ExpectedIncomingQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PurchaseQty | decimal(18, 6) | Y |  |  | **Deprecated → PurchaseQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| RequiredQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AvailableQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ExpectedIncomingQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PurchaseQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## MaterialReservation
_Kho & cấp phát · BẢNG NGHIỆP VỤ (Transaction)_ – Đặt trước vật tư
BRD: **Production Material Reservation** (Production/Transaction) – Đặt giữ nguyên liệu cho Production Order để tránh nhiều lệnh cùng sử dụng một lượng tồn kho đã cam kết.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ProductionOrderId | uniqueidentifier | N |  | ProductionOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RequiredQty | decimal(18, 6) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ReservedQty | decimal(18, 6) | N |  |  | **Deprecated → ReservedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| ReservedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## MealCost
_Thực đơn & kế hoạch · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin bữa ăn/loại suất ăn và các thuộc tính liên quan.
BRD: **Margin** (Costing/Result) – Lợi nhuận gộp; Revenue trừ Actual Cost.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| OrderId | uniqueidentifier | N |  | Order | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DishId | uniqueidentifier | N |  | Dishe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MealQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| TotalCost | decimal(18, 2) | Y |  |  | Tổng chi phí |
| CostPerMeal | decimal(18, 2) | Y |  |  | Giá trị tiền/chi phí/đơn giá |
| OrderLineId | uniqueidentifier | Y |  | OrderLine | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| AcceptedMealQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| TotalActualCost | decimal(18, 4) | N | (0) |  | Giá trị tổng hợp của total actual cost |
| Revenue | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| GrossMargin | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| CalculatedAt | datetime2(7) | N | sysutcdatetime() |  | Thời điểm tính toán |

## MealWasteRecord
_Sản xuất & giá thành · BẢNG NGHIỆP VỤ (Transaction)_ – Ghi nhận suất ăn hoặc thực phẩm hao hụt, hủy bỏ, dư thừa theo nguyên nhân và ca/ngày.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ProductionBatchId | uniqueidentifier | Y |  | ProductionBatche | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ProductionOrderId | uniqueidentifier | Y |  | ProductionOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DishId | uniqueidentifier | Y |  | Dishe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ShiftId | uniqueidentifier | Y |  | Shift | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| WasteType | nvarchar(50) | N |  |  | Thông tin WasteType của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| QuantityMeals | decimal(18, 3) | Y |  |  | Thông tin QuantityMeals của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| QuantityKg | decimal(18, 3) | Y |  |  | Thông tin QuantityKg của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Reason | nvarchar(1000) | Y |  |  | Lý do phát sinh yêu cầu/sự kiện. |
| Disposition | nvarchar(500) | Y |  |  | Thông tin Disposition của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| OccurredAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Occurred của nghiệp vụ. |
| RecordedBy | uniqueidentifier | Y |  |  | Thông tin RecordedBy của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## MedicalExamination
_cần xác nhận · LEGACY – cần rà soát_ – Khám bệnh
Ghi chú: Ứng viên ngừng sử dụng; xác minh dependency trước khi loại bỏ.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| Price | decimal(18, 0) | N | (0) |  | Đơn giá/giá |
| NormalIndex | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## MedicineWarehouse
_Kho & cấp phát · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu dữ liệu kho và nghiệp vụ quản lý tồn kho, nhập, xuất hoặc điều chuyển.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| Quantity | int | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| SupplierId | uniqueidentifier | Y |  | Supplier | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| UnitMeasureId | uniqueidentifier | Y |  | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PriceInput | decimal(18, 0) | N | (0) |  | Giá trị tiền/chi phí/đơn giá |
| DateInput | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Price | decimal(18, 0) | N | (0) |  | Đơn giá/giá |
| Expiry | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PriceTotal | decimal(18, 0) | N | (0) |  | Giá trị tiền/chi phí/đơn giá |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| IsMain | bit | N | (0) |  | Cờ logic xác định main (thường 1: có/đúng, 0: không/sai) |

## Menu
_Thực đơn & kế hoạch · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin thực đơn được lập và áp dụng theo ngày/tuần/tháng hoặc bữa ăn.
BRD: **Menu** (Menu/Configuration) – Quản lý thực đơn theo tuần/tháng/ngày hoặc khoảng thời gian.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| VersionNo | int | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveFrom | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveTo | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MenuType | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VersionCode | nvarchar(max) | Y |  |  | Mã nghiệp vụ/mã định danh |

## MenuAssignment
_Thực đơn & kế hoạch · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin thực đơn được lập và áp dụng theo ngày/tuần/tháng hoặc bữa ăn.
BRD: **Menu Assignment** (Menu/Configuration) – Phân bổ Menu theo Customer, Delivery Location, Meal Type và ngày/khoảng thời gian; Menu không tự động áp dụng cho mọi khách hàng.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CustomerId | uniqueidentifier | N |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryLocationId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MenuId | uniqueidentifier | N |  | Menu | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MealType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveFrom | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveTo | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| ContractId | uniqueidentifier | Y |  | Contract | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## MenuChangeRequest
_Thực đơn & kế hoạch · BẢNG NGHIỆP VỤ (Transaction)_ – Yêu cầu thay đổi thực đơn đã lập/đã duyệt, kèm lý do và trạng thái xử lý.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| RequestNo | nvarchar(50) | N |  |  | Thông tin RequestNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| OrderId | uniqueidentifier | Y |  | Order | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MenuId | uniqueidentifier | Y |  | Menu | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OriginalDishId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RequestedDishId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MealDate | date | N |  |  | Thời điểm/ngày Meal của nghiệp vụ. |
| ShiftId | uniqueidentifier | Y |  | Shift | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Quantity | decimal(18, 3) | Y |  |  | Số lượng phát sinh. |
| Reason | nvarchar(1000) | N |  |  | Lý do phát sinh yêu cầu/sự kiện. |
| RequestedBy | uniqueidentifier | Y |  |  | Mã người tạo yêu cầu. |
| RequestStatus | nvarchar(30) | N | N'Pending' |  | Trạng thái xử lý yêu cầu. |
| ApprovedBy | uniqueidentifier | Y |  |  | Mã người phê duyệt. |
| ApprovedAt | datetime2(0) | Y |  |  | Thời điểm phê duyệt. |

## MenuLine
_Thực đơn & kế hoạch · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Lưu thông tin thực đơn được lập và áp dụng theo ngày/tuần/tháng hoặc bữa ăn.
BRD: **Menu Line** (Menu/Configuration) – Chi tiết các món ăn thuộc Menu; tham chiếu Dish.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| MenuId | uniqueidentifier | N |  | Menu | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DishId | uniqueidentifier | N |  | Dishe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MealType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| SequenceNo | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PortionQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| IsRequired | bit | Y |  |  | Cờ bắt buộc |
| DayOfWeek | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| SortOrder | int | N | (0) |  | Thứ tự sắp xếp |
| StandardQuantity | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## ObjectMenu
_Thực đơn & kế hoạch · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin thực đơn được lập và áp dụng theo ngày/tuần/tháng hoặc bữa ăn.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| ShortName | nvarchar(max) | Y |  |  | Tên viết tắt |
| ParentId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MetaLink | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| IconName | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| LevelShow | int | Y | (1) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| OrderShow | int | Y | (1) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DataTarget | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AriaControls | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| IsActive | bit | N | (0) |  | Cờ trạng thái hoạt động. |
| IsDefault | bit | N | (0) |  | Cờ xác định giá trị mặc định |

## Order
_Hợp đồng & đơn hàng · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin đơn hàng/yêu cầu cung cấp suất ăn hoặc hàng hóa theo kỳ/ngày.
BRD: **Order** (Demand & Order/Transaction) – Đơn đặt suất ăn; Header lưu Customer, Contract, Delivery Location, Order Date, Meal Date và Status.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| OrderNo | nvarchar(255) | Y |  |  | **Deprecated → OrderNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| CustomerId | uniqueidentifier | N |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ContractId | uniqueidentifier | Y |  | Contract | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryLocationId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrderDate | datetime | N |  |  | Ngày/giờ liên quan đến order |
| MealDate | datetime | N |  |  | Ngày/giờ liên quan đến meal |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| TotalQuantity | decimal(18, 6) | Y |  |  | Giá trị tổng hợp của total quantity |
| TotalAmount | decimal(18, 2) | Y |  |  | Tổng tiền |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| OrderNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DeliveryDate | datetime2(7) | N | sysutcdatetime() |  | Ngày giao hàng |

## OrderLine
_Hợp đồng & đơn hàng · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Lưu thông tin đơn hàng/yêu cầu cung cấp suất ăn hoặc hàng hóa theo kỳ/ngày.
BRD: **Order Line** (Demand & Order/Transaction) – Chi tiết đơn hàng; tham chiếu Menu, Meal Type, Shift, Date, Quantity, Unit Price và Amount.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| OrderId | uniqueidentifier | N |  | Order | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MenuId | uniqueidentifier | Y |  | Menu | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MealType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ShiftId | uniqueidentifier | Y |  | Shift | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Quantity | decimal(18, 6) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitPrice | decimal(18, 2) | Y |  |  | Đơn giá |
| Amount | decimal(18, 2) | Y |  |  | **Deprecated → LineAmount**. Số tiền/giá trị |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| ContractMealId | uniqueidentifier | Y |  | ContractMeal | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LineAmount | decimal(18, 4) | N | (0) |  | Giá trị tiền/chi phí/đơn giá |

## OrderReconciliation
_Hợp đồng & đơn hàng · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin đơn hàng/yêu cầu cung cấp suất ăn hoặc hàng hóa theo kỳ/ngày.
BRD: **Order Reconciliation** (Reconciliation/Result) – Đối soát Ordered, Produced, Packed, Delivered và Accepted.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| OrderId | uniqueidentifier | N |  | Order | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrderedQty | decimal(18, 6) | Y |  |  | **Deprecated → OrderedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ProducedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PackedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DeliveredQty | decimal(18, 6) | Y |  |  | **Deprecated → DeliveredQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AcceptedQty | decimal(18, 6) | Y |  |  | **Deprecated → AcceptedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VarianceQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VariancePercent | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Remark | nvarchar(max) | Y |  |  | Diễn giải/ghi chú |
| ProofOfDeliveryId | uniqueidentifier | Y |  | ProofOfDeliverie | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ReconciliationDate | datetime2(7) | N | sysutcdatetime() |  | Ngày/giờ liên quan đến reconciliation |
| OrderedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DeliveredQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AcceptedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| FinalAmount | decimal(18, 4) | Y |  |  | Giá trị tiền/chi phí/đơn giá |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## OrganizationUnit
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Đơn vị tổ chức
BRD: **Organization Unit** (Organization/Master Data) – Quản lý đơn vị/bộ phận trong doanh nghiệp như Kinh doanh, Mua hàng, Kho, Sản xuất, QC, Logistics và Tài chính; hỗ trợ cấu trúc cha-con.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CompanyId | uniqueidentifier | N |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ParentId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| UnitType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ManagerId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| Address | nvarchar(max) | Y |  |  | Địa chỉ |
| Phone | nvarchar(max) | Y |  |  | Số điện thoại |

## OutboundMessage
_Message · TÍCH HỢP / MESSAGE_ – Lưu tin nhắn cần gửi hoặc đã gửi ra ngoài qua các kênh tích hợp.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| IntegrationChannelId | uniqueidentifier | N |  | IntegrationChannel | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ExternalConversationId | nvarchar(255) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RecipientExternalId | nvarchar(255) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MessageType | nvarchar(50) | Y |  |  | Thông tin MessageType của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| PayloadJson | nvarchar(max) | N |  |  | Nội dung dữ liệu gốc/trao đổi được lưu dưới dạng JSON. |
| QueuedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm đưa thông điệp vào hàng đợi gửi. |
| SentAt | datetime2(0) | Y |  |  | Thời điểm gửi thông điệp. |
| MessageStatus | nvarchar(30) | N | N'Queued' |  | Trạng thái gửi thông điệp. |
| RetryCount | int | N | (0) |  | Thông tin RetryCount của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| IdempotencyKey | nvarchar(200) | Y |  |  | Thông tin IdempotencyKey của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CorrelationId | nvarchar(100) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ProviderMessageId | nvarchar(255) | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LastError | nvarchar(2000) | Y |  |  | Thông tin LastError của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## PackagingLedger
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Sổ phát sinh tăng/giảm/điều chuyển/thu hồi bao bì hoặc vật dụng giao suất ăn.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CustomerId | uniqueidentifier | Y |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryLocationId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrderId | uniqueidentifier | Y |  | Order | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveryRouteId | uniqueidentifier | Y |  | DeliveryRoute | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| TransactionType | nvarchar(30) | N |  |  | Loại nghiệp vụ phát sinh tăng/giảm/điều chuyển/thu hồi. |
| PackagingType | nvarchar(100) | N |  |  | Loại bao bì/vật dụng được quản lý. |
| Quantity | decimal(18, 3) | N |  |  | Số lượng phát sinh. |
| TransactionAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Transaction của nghiệp vụ. |
| ReferenceNo | nvarchar(100) | Y |  |  | Thông tin ReferenceNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ConditionCode | nvarchar(30) | Y |  |  | Thông tin ConditionCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| RecordedBy | uniqueidentifier | Y |  |  | Thông tin RecordedBy của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Note | nvarchar(1000) | Y |  |  | Ghi chú. |

## PackingOrder
_Hợp đồng & đơn hàng · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin đơn hàng/yêu cầu cung cấp suất ăn hoặc hàng hóa theo kỳ/ngày.
BRD: **Packing Order** (Packing/Transaction) – Lệnh đóng suất được tạo từ Production Batch.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| PackingNo | nvarchar(255) | Y |  |  | **Deprecated → PackingOrderNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ProductionBatchId | uniqueidentifier | N |  | ProductionBatche | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrderId | uniqueidentifier | Y |  | Order | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PlannedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| PackingOrderNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PackingDate | datetime2(7) | N | sysutcdatetime() |  | Ngày/giờ liên quan đến packing |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## PackingResult
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Kết quả đóng gói
BRD: **Packing Result** (Packing/Transaction) – Kết quả đóng suất: Planned, Packed, Damaged, Rejected và Accepted.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| PackingOrderId | uniqueidentifier | N |  | PackingOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PlannedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PackedQty | decimal(18, 6) | Y |  |  | **Deprecated → PackedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DamagedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RejectedQty | decimal(18, 6) | Y |  |  | **Deprecated → RejectedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AcceptedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Remark | nvarchar(max) | Y |  |  | Diễn giải/ghi chú |
| ProductionOutputId | uniqueidentifier | Y |  | ProductionOutput | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PackedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RejectedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PackagingType | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| QualityStatus | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## Patient
_cần xác nhận · LEGACY – cần rà soát_ – Bệnh nhân
Ghi chú: Ứng viên ngừng sử dụng; xác minh dependency trước khi loại bỏ.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CouponCode | bigint | N | (1) |  | Mã nghiệp vụ/mã định danh |
| FullName | nvarchar(max) | Y |  |  | Tên đầy đủ |
| DateOfBirth | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Sex | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PhoneNumber | nvarchar(max) | Y |  |  | Thông tin số điện thoại |
| AdministrativeProvinceId | uniqueidentifier | Y |  | AdministrativeProvince | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| AdministrativeDistrictId | uniqueidentifier | Y |  | AdministrativeDistrict | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| AdministrativeWardId | uniqueidentifier | Y |  | AdministrativeWard | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Address | nvarchar(max) | Y |  |  | Địa chỉ |
| Diagnosis | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Result | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Visit | int | N | (1) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| TotalPrice | decimal(18, 0) | N | (0) |  | Giá trị tổng hợp của total price |

## PatientDetail
_cần xác nhận · LEGACY – cần rà soát_ – Thông tin chi tiết bệnh nhân
Ghi chú: Ứng viên ngừng sử dụng; xác minh dependency trước khi loại bỏ.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| PatientId | uniqueidentifier | Y |  | Patient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MedicalExaminationId | uniqueidentifier | Y |  | MedicalExamination | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PersonId | uniqueidentifier | Y |  | Person | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Price | decimal(18, 0) | N | (0) |  | Đơn giá/giá |
| Quantity | int | N | (1) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| TotalPrice | decimal(18, 0) | N | (0) |  | Giá trị tổng hợp của total price |
| Result | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## Person
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Cá nhân
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| FullName | nvarchar(max) | Y |  |  | Tên đầy đủ |
| Avatar | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PhoneNumber | nvarchar(max) | Y |  |  | Thông tin số điện thoại |
| DateOfBirth | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Sex | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Address | nvarchar(max) | Y |  |  | Địa chỉ |

## Position
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Chức vụ
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Description | nvarchar(max) | Y |  |  | Thông tin mô tả. |

## PricePolicie
_Policy · CẤU HÌNH / CHÍNH SÁCH_ – Chính sách giá
BRD: **Price Policy** (Customer & Commercial/Configuration) – Quản lý chính sách giá áp dụng theo hợp đồng/khách hàng và thời gian hiệu lực.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CustomerId | uniqueidentifier | N |  | Customer | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ContractId | uniqueidentifier | Y |  | Contract | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MealType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitPrice | decimal(18, 2) | N |  |  | Đơn giá |
| EffectiveFrom | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveTo | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Code | nvarchar(max) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| CurrencyCode | nvarchar(max) | Y |  |  | Mã tiền tệ |

## ProductionBatche
_Sản xuất & giá thành · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu dữ liệu kế hoạch/lệnh/mẻ sản xuất, phục vụ điều phối chế biến và theo dõi sản lượng.
BRD: **Production Batch** (Production/Transaction) – Quản lý mẻ sản xuất; liên kết Production Order, Dish, Recipe, thời gian, sản lượng và trạng thái QC.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| BatchNo | nvarchar(255) | Y |  |  | **Deprecated → BatchNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ProductionOrderId | uniqueidentifier | N |  | ProductionOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DishId | uniqueidentifier | N |  | Dishe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RecipeId | uniqueidentifier | N |  | Recipe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StartTime | datetime | Y |  |  | **Deprecated → StartDate**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EndTime | datetime | Y |  |  | **Deprecated → EndDate**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PlannedQty | decimal(18, 6) | Y |  |  | **Deprecated → PlannedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ActualQty | decimal(18, 6) | Y |  |  | **Deprecated → ActualQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AcceptedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RejectedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| WasteQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| QcStatus | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| BatchNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| StartDate | datetime2(7) | N | sysutcdatetime() |  | Ngày bắt đầu |
| EndDate | datetime2(7) | Y |  |  | Ngày kết thúc |
| PlannedQuantity | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ActualQuantity | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| QualityStatus | nvarchar(max) | Y |  |  | **Deprecated → QcStatus**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| KitchenWorkstationId | uniqueidentifier | Y |  | KitchenWorkstation | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## ProductionLine
_Vận hành bếp & phương tiện · DANH MỤC (Master Data)_ – Danh mục dây chuyền/khu vực sản xuất, năng lực và trạng thái vận hành.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(50) | N |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(255) | N |  |  | Tên đối tượng. |
| CapacityMealPerBatch | decimal(18, 2) | Y |  |  | Công suất suất ăn tối đa dự kiến của một mẻ trên dây chuyền. |
| MinutesPerBatch | decimal(10, 2) | Y |  |  | Thời lượng tiêu chuẩn để hoàn thành một mẻ sản xuất. |
| Status | nvarchar(30) | N | N'Active' |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(1000) | Y |  |  | Ghi chú. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrganizationUnitId | uniqueidentifier | Y |  | OrganizationUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## ProductionOrder
_Sản xuất & giá thành · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin đơn hàng/yêu cầu cung cấp suất ăn hoặc hàng hóa theo kỳ/ngày.
BRD: **Production Order** (Production/Transaction) – Lệnh sản xuất; gồm ngày sản xuất, ca, line, Dish, Recipe, Planned Quantity, Actual Quantity và Status.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ProductionOrderNo | nvarchar(255) | Y |  |  | **Deprecated → ProductionOrderNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ProductionPlanId | uniqueidentifier | N |  | ProductionPlan | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DishId | uniqueidentifier | N |  | Dishe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RecipeId | uniqueidentifier | N |  | Recipe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ShiftId | uniqueidentifier | Y |  | Shift | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PlannedQty | decimal(18, 6) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ActualQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| StartTime | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EndTime | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| ProductionOrderNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PlannedStartDate | datetime2(7) | N | sysutcdatetime() |  | Ngày/giờ liên quan đến planned start |
| ActualStartDate | datetime2(7) | Y |  |  | Ngày/giờ liên quan đến actual start |
| ActualEndDate | datetime2(7) | Y |  |  | Ngày/giờ liên quan đến actual end |
| ResponsibleEmployeeId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ProductionLineId | uniqueidentifier | Y |  | ProductionLine | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RecipeSnapshotJson | nvarchar(max) | Y |  |  | Bản chụp công thức được áp dụng cho lệnh sản xuất tại thời điểm tạo/khóa lệnh. |

## ProductionOutput
_Sản xuất & giá thành · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu dữ liệu kế hoạch/lệnh/mẻ sản xuất, phục vụ điều phối chế biến và theo dõi sản lượng.
BRD: **Production Output** (Production/Transaction) – Ghi nhận kết quả sản xuất: Planned, Actual, Rejected, Waste và Accepted.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ProductionBatchId | uniqueidentifier | N |  | ProductionBatche | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OutputQty | decimal(18, 6) | Y |  |  | **Deprecated → ProducedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AcceptedQty | decimal(18, 6) | Y |  |  | **Deprecated → AcceptedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RejectedQty | decimal(18, 6) | Y |  |  | **Deprecated → RejectedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| WasteQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Remark | nvarchar(max) | Y |  |  | Diễn giải/ghi chú |
| DishId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | Dishe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ProducedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AcceptedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RejectedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitId | uniqueidentifier | Y |  | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| QualityStatus | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## ProductionPlan
_Thực đơn & kế hoạch · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu dữ liệu kế hoạch/lệnh/mẻ sản xuất, phục vụ điều phối chế biến và theo dõi sản lượng.
BRD: **Production Plan** (Production/Transaction) – Kế hoạch sản xuất từ Confirmed Order; lập theo ngày, ca, line, menu, dish và quantity.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| PlanNo | nvarchar(255) | Y |  |  | **Deprecated → PlanNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ProductionDate | datetime | N |  |  | Ngày sản xuất |
| ShiftId | uniqueidentifier | Y |  | Shift | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| PlanNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PlanDate | datetime2(7) | N | sysutcdatetime() |  | Ngày/giờ liên quan đến plan |
| OrganizationUnitId | uniqueidentifier | Y |  | OrganizationUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## ProductionPlanLine
_Thực đơn & kế hoạch · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Lưu dữ liệu kế hoạch/lệnh/mẻ sản xuất, phục vụ điều phối chế biến và theo dõi sản lượng.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ProductionPlanId | uniqueidentifier | N |  | ProductionPlan | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrderLineId | uniqueidentifier | Y |  | OrderLine | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DishId | uniqueidentifier | N |  | Dishe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PlannedQty | decimal(18, 6) | N |  |  | **Deprecated → PlannedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PlannedQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ActualQuantity | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ShiftId | uniqueidentifier | Y |  | Shift | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Status | nvarchar(max) | Y |  |  | Trạng thái bản ghi/đối tượng. |

## ProofOfDeliverie
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ (Transaction)_ – Chứng từ giao hàng
BRD: **Proof of Delivery (POD)** (Logistics/Transaction) – Bằng chứng giao hàng; gồm Delivered/Accepted/Rejected Quantity, Delivery Time, Receiver, Signature, Photo và Remark.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| DeliveryOrderId | uniqueidentifier | N |  | DeliveryOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| DeliveredQty | decimal(18, 6) | Y |  |  | **Deprecated → DeliveredQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AcceptedQty | decimal(18, 6) | Y |  |  | **Deprecated → AcceptedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RejectedQty | decimal(18, 6) | Y |  |  | **Deprecated → RejectedQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ReceiverName | nvarchar(500) | Y |  |  | Tên hiển thị của đối tượng |
| ReceiverPhone | nvarchar(100) | Y |  |  | Thông tin số điện thoại |
| ReceivedAt | datetime | Y |  |  | Ngày/giờ liên quan đến received |
| SignatureUrl | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PhotoUrl | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Remark | nvarchar(max) | Y |  |  | Diễn giải/ghi chú |
| ConfirmedAt | datetime2(7) | N | sysutcdatetime() |  | Ngày/giờ liên quan đến confirmed |
| DeliveredQuantity | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AcceptedQuantity | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RejectedQuantity | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## PurchaseOrder
_Mua hàng & nhập kho · BẢNG NGHIỆP VỤ (Transaction)_ – Lưu thông tin đơn hàng/yêu cầu cung cấp suất ăn hoặc hàng hóa theo kỳ/ngày.
BRD: **Purchase Order** (Procurement/Transaction) – Đơn mua hàng gửi Supplier.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| PONo | nvarchar(255) | Y |  |  | **Deprecated → PurchaseOrderNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| SupplierId | uniqueidentifier | N |  | Supplier | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PurchaseRequestId | uniqueidentifier | Y |  | PurchaseRequest | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrderDate | datetime | N |  |  | Ngày/giờ liên quan đến order |
| ExpectedDate | datetime | Y |  |  | **Deprecated → ExpectedDeliveryDate**. Ngày/giờ liên quan đến expected |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| TotalAmount | decimal(18, 2) | Y |  |  | Tổng tiền |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| PurchaseOrderNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ExpectedDeliveryDate | datetime2(7) | Y |  |  | Ngày/giờ liên quan đến expected delivery |

## PurchaseOrderLine
_Mua hàng & nhập kho · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Lưu thông tin đơn hàng/yêu cầu cung cấp suất ăn hoặc hàng hóa theo kỳ/ngày.
BRD: **Purchase Order Line** (Procurement/Transaction) – Chi tiết nguyên liệu mua trong Purchase Order.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| PurchaseOrderId | uniqueidentifier | N |  | PurchaseOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Quantity | decimal(18, 6) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UomId | uniqueidentifier | N |  |  | **Deprecated → UnitId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| UnitPrice | decimal(18, 2) | N |  |  | Đơn giá |
| Amount | decimal(18, 2) | Y |  |  | **Deprecated → LineAmount**. Số tiền/giá trị |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LineAmount | decimal(18, 4) | N | (0) |  | Giá trị tiền/chi phí/đơn giá |

## PurchaseRequest
_Mua hàng & nhập kho · BẢNG NGHIỆP VỤ (Transaction)_ – Yêu cầu mua hàng
BRD: **Purchase Request** (Procurement/Transaction) – Đề nghị mua hàng phát sinh từ nhu cầu nguyên liệu.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| RequestNo | nvarchar(255) | Y |  |  | **Deprecated → RequestNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RequestDate | datetime | N |  |  | Ngày/giờ liên quan đến request |
| RequesterId | uniqueidentifier | Y |  |  | **Deprecated → RequestedByEmployeeId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| RequestNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| RequestedByEmployeeId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RequiredDate | datetime2(7) | Y |  |  | Ngày/giờ liên quan đến required |

## PurchaseRequestLine
_Mua hàng & nhập kho · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Dòng chi tiết yêu cầu mua hàng
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| PurchaseRequestId | uniqueidentifier | N |  | PurchaseRequest | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Quantity | decimal(18, 6) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UomId | uniqueidentifier | N |  |  | **Deprecated → UnitId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RequiredDate | datetime | Y |  |  | Ngày/giờ liên quan đến required |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## QCInspection
_QC & an toàn thực phẩm · BẢNG NGHIỆP VỤ (Transaction)_ – Kiểm tra chất lượng (QC)
BRD: **QC Inspection** (Receiving & Quality/Transaction / Control) – Phiếu kiểm tra chất lượng nguyên liệu đầu vào; trạng thái PASS, HOLD hoặc REJECT.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| GoodsReceiptId | uniqueidentifier | N |  | GoodsReceipt | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| InspectionDate | datetime | N |  |  | Ngày/giờ liên quan đến inspection |
| InspectorId | uniqueidentifier | Y |  |  | **Deprecated → InspectorEmployeeId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Result | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Remark | nvarchar(max) | Y |  |  | Diễn giải/ghi chú |
| InspectionNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| InspectorEmployeeId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## QCInspectionLine
_QC & an toàn thực phẩm · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Dòng chi tiết kiểm tra chất lượng (QC)
BRD: **QC Inspection Line** (Receiving & Quality/Transaction / Control) – Chi tiết kiểm tra ngoại quan, nhiệt độ, bao bì, HSD, khối lượng và tiêu chuẩn chất lượng.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| QCInspectionId | uniqueidentifier | N |  | QCInspection | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Temperature | decimal(10, 2) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Weight | decimal(18, 6) | Y |  |  | Khối lượng |
| Appearance | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Packaging | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ExpiryDate | datetime | Y |  |  | Ngày hết hạn |
| Result | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Remark | nvarchar(max) | Y |  |  | Diễn giải/ghi chú |
| GoodsReceiptLineId | uniqueidentifier | Y |  | GoodsReceiptLine | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Criterion | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## Recipe
_Món ăn & công thức · DANH MỤC (Master Data)_ – Công thức chế biến
BRD: **Recipe** (Product & Recipe/Configuration) – Công thức chế biến món ăn; có thể quản lý phiên bản, trạng thái và hiệu lực.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| DishId | uniqueidentifier | N |  | Dishe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| VersionNo | int | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| YieldPercent | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| StandardOutputQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveFrom | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveTo | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| ApprovedBy | uniqueidentifier | Y |  |  | Người phê duyệt |
| ApprovedAt | datetime | Y |  |  | Ngày/giờ liên quan đến approved |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| VersionCode | nvarchar(max) | Y |  |  | Mã nghiệp vụ/mã định danh |
| YieldQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| YieldUnitId | uniqueidentifier | Y |  | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IsActive | bit | N | (0) |  | Cờ trạng thái hoạt động. |
| Instructions | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| SnapshotJson | nvarchar(max) | Y |  |  | Bản chụp cấu hình/định mức tại thời điểm phát sinh, lưu dạng JSON để bảo toàn dữ liệu lịch sử. |
| LockedAt | datetime2(0) | Y |  |  | Thời điểm khóa phiên bản/công thức, ngăn chỉnh sửa làm thay đổi dữ liệu đã sử dụng. |
| LockedBy | uniqueidentifier | Y |  |  | Mã người dùng thực hiện khóa công thức/phiên bản. |

## RecipeLine
_Món ăn & công thức · DANH MỤC (Master Data)_ – Dòng chi tiết công thức chế biến
BRD: **Recipe Line** (Product & Recipe/Configuration) – Chi tiết nguyên liệu cấu thành Recipe; liên kết Ingredient và định lượng sử dụng.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| RecipeId | uniqueidentifier | N |  | Recipe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Quantity | decimal(18, 6) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UomId | uniqueidentifier | N |  |  | **Deprecated → UnitId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| WastePercent | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| YieldPercent | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| SequenceNo | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ExpectedWasteRate | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PreparationNote | nvarchar(max) | Y |  |  | Thông tin mô tả/ghi chú |
| SortOrder | int | N | (0) |  | Thứ tự sắp xếp |

## ReturnInspection
_Giao hàng & bao bì · BẢNG NGHIỆP VỤ (Transaction)_ – Kiểm tra hàng trả về
BRD: **Return Inspection** (Return & Reverse Logistics/Transaction / Control) – Kiểm tra vật dụng thu hồi: đủ, thiếu, hỏng, bẩn hoặc không đạt.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ContainerReturnId | uniqueidentifier | N |  | ContainerReturn | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| InspectionResult | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DamagedQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| MissingQty | decimal(18, 6) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Remark | nvarchar(max) | Y |  |  | Diễn giải/ghi chú |
| InspectionDate | datetime2(7) | N | sysutcdatetime() |  | Ngày/giờ liên quan đến inspection |
| InspectorEmployeeId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Result | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## Shift
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Ca làm việc
BRD: **Shift** (Resource/Reference Data) – Danh mục ca làm việc; được dùng trong Forecast, Order và Production.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(255) | Y |  |  | Tên đối tượng. |
| StartTime | time(7) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EndTime | time(7) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## ShiftAssignment
_Nhân sự vận hành · BẢNG NGHIỆP VỤ (Transaction)_ – Phân công nhân sự vào ca làm việc, vị trí và ngày làm việc.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| EmployeeId | uniqueidentifier | N |  | Employee | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ShiftId | uniqueidentifier | N |  | Shift | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| WorkDate | date | N |  |  | Thời điểm/ngày Work của nghiệp vụ. |
| PlannedStart | datetime2(0) | Y |  |  | Thông tin PlannedStart của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| PlannedEnd | datetime2(0) | Y |  |  | Thông tin PlannedEnd của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| AssignmentStatus | nvarchar(30) | N | N'Planned' |  | Thông tin AssignmentStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| AssignedBy | uniqueidentifier | Y |  |  | Thông tin AssignedBy của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Note | nvarchar(1000) | Y |  |  | Ghi chú. |

## ShiftSwapRequest
_Nhân sự vận hành · BẢNG NGHIỆP VỤ (Transaction)_ – Yêu cầu đổi ca giữa nhân viên, kèm ca/ngày liên quan và trạng thái xử lý.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| RequestNo | nvarchar(50) | N |  |  | Thông tin RequestNo của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| RequesterEmployeeId | uniqueidentifier | N |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| TargetEmployeeId | uniqueidentifier | N |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| CurrentShiftAssignmentId | uniqueidentifier | N |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ProposedShiftAssignmentId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Reason | nvarchar(1000) | Y |  |  | Lý do phát sinh yêu cầu/sự kiện. |
| RequestStatus | nvarchar(30) | N | N'Pending' |  | Trạng thái xử lý yêu cầu. |
| ApprovalRequestId | uniqueidentifier | Y |  | ApprovalRequest | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| SubmittedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Submitted của nghiệp vụ. |

## StandardCost
_Policy · CẤU HÌNH / CHÍNH SÁCH_ – Chi phí tiêu chuẩn
BRD: **Standard Cost** (Costing/Result) – Giá thành chuẩn từ Recipe, Standard Material Price, Standard Labor và Standard Overhead.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| DishId | uniqueidentifier | N |  | Dishe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| RecipeId | uniqueidentifier | N |  | Recipe | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MaterialCost | decimal(18, 2) | Y |  |  | Chi phí nguyên vật liệu |
| LaborCost | decimal(18, 2) | Y |  |  | Chi phí nhân công |
| OverheadCost | decimal(18, 2) | Y |  |  | Chi phí chung |
| TotalCost | decimal(18, 2) | Y |  |  | Tổng chi phí |
| EffectiveFrom | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveTo | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| CostPerServing | decimal(18, 4) | N | (0) |  | Giá trị tiền/chi phí/đơn giá |
| CurrencyCode | nvarchar(max) | Y |  |  | Mã tiền tệ |

## StockCount
_Kho & cấp phát · BẢNG NGHIỆP VỤ (Transaction)_ – Kiểm kê kho
BRD: **Stock Count** (Stock Count/Transaction) – Phiếu kiểm kê kho theo Daily, Weekly, Monthly hoặc Ad-hoc.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| CountNo | nvarchar(255) | Y |  |  | **Deprecated → CountNumber**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| WarehouseId | uniqueidentifier | N |  | Warehouse | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| CountType | nvarchar(50) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| CountDate | datetime | N |  |  | Ngày/giờ liên quan đến count |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| ApprovedBy | uniqueidentifier | Y |  |  | Người phê duyệt |
| ApprovedAt | datetime | Y |  |  | Ngày/giờ liên quan đến approved |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| CountNumber | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| ResponsibleEmployeeId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## StockCountLine
_Kho & cấp phát · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Dòng chi tiết kiểm kê kho
BRD: **Inventory Variance** (Reconciliation/Result) – Đối soát System Stock với Physical Stock.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| StockCountId | uniqueidentifier | N |  | StockCount | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LotId | uniqueidentifier | Y |  |  | **Deprecated → MaterialLotId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| SystemQty | decimal(18, 6) | Y |  |  | **Deprecated → SystemQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PhysicalQty | decimal(18, 6) | Y |  |  | **Deprecated → PhysicalQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VarianceQty | decimal(18, 6) | Y |  |  | **Deprecated → VarianceQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VarianceValue | decimal(18, 2) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Reason | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| MaterialLotId | uniqueidentifier | Y |  | MaterialLot | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| SystemQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PhysicalQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VarianceQuantity | decimal(18, 4) | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| VarianceReason | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## StorageLocation
_Nguyên liệu & đơn vị · DANH MỤC (Master Data)_ – Địa điểm lưu trữ
BRD: **Storage Location** (Warehouse/Master Data) – Quản lý vị trí lưu trữ cụ thể trong kho/khu vực.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| ZoneId | uniqueidentifier | N |  |  | **Deprecated → StorageZoneId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(255) | Y |  |  | Tên đối tượng. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| WarehouseId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | Warehouse | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StorageZoneId | uniqueidentifier | Y |  | StorageZone | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| LocationType | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## StorageZone
_Nguyên liệu & đơn vị · DANH MỤC (Master Data)_ – Khu vực lưu trữ
BRD: **Storage Zone** (Warehouse/Master Data) – Quản lý khu vực/khoang bảo quản trong kho.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| WarehouseId | uniqueidentifier | N |  | Warehouse | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(255) | Y |  |  | Tên đối tượng. |
| TemperatureMin | decimal(10, 2) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| TemperatureMax | decimal(10, 2) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| TemperatureRange | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## Supplier
_Khách hàng & nhà cung cấp · DANH MỤC (Master Data)_ – Lưu thông tin nhà cung cấp nguyên liệu, hàng hóa hoặc dịch vụ.
BRD: **Supplier** (Supplier/Master Data) – Quản lý nhà cung cấp nguyên liệu.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.
_Thiếu cột chuẩn: DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| SupplierType | nvarchar(100) | Y |  |  | Thông tin SupplierType của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| TaxCode | nvarchar(100) | Y |  |  | Thông tin TaxCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Address | nvarchar(max) | Y |  |  | Thông tin Address của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Phone | nvarchar(100) | Y |  |  | Thông tin Phone của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Email | nvarchar(255) | Y |  |  | Thông tin Email của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ContactName | nvarchar(max) | Y |  |  | Thông tin ContactName của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ContactPhone | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## SupplierIssue
_Mua hàng & nhập kho · BẢNG NGHIỆP VỤ (Transaction)_ – Ghi nhận sự cố/chất lượng không phù hợp liên quan đến nhà cung cấp hoặc lô nguyên liệu.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| SupplierId | uniqueidentifier | N |  | Supplier | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| PurchaseOrderId | uniqueidentifier | Y |  | PurchaseOrder | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| GoodsReceiptId | uniqueidentifier | Y |  | GoodsReceipt | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IssueType | nvarchar(50) | N |  |  | Loại sự cố/vấn đề. |
| Severity | nvarchar(30) | N | N'Medium' |  | Thông tin Severity của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Description | nvarchar(2000) | N |  |  | Thông tin mô tả. |
| ReportedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Reported của nghiệp vụ. |
| DueAt | datetime2(0) | Y |  |  | Thời điểm/ngày Due của nghiệp vụ. |
| IssueStatus | nvarchar(30) | N | N'Open' |  | Trạng thái xử lý sự cố. |
| Resolution | nvarchar(2000) | Y |  |  | Thông tin Resolution của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| ClosedAt | datetime2(0) | Y |  |  | Thời điểm/ngày Closed của nghiệp vụ. |
| OwnerId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## SupplierMaterial
_Khách hàng & nhà cung cấp · DANH MỤC (Master Data)_ – Lưu thông tin nhà cung cấp nguyên liệu, hàng hóa hoặc dịch vụ.
BRD: **Supplier Material** (Supplier/Master Data) – Liên kết nhà cung cấp với nguyên liệu; quản lý thông tin Material, giá, MOQ và Lead Time.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| SupplierId | uniqueidentifier | N |  | Supplier | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IngredientId | uniqueidentifier | N |  | Ingredient | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| SupplierMaterialCode | nvarchar(255) | Y |  |  | Mã nghiệp vụ/mã định danh |
| PurchaseUomId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| UnitPrice | decimal(18, 2) | Y |  |  | Đơn giá |
| MinimumOrderQty | decimal(18, 6) | Y |  |  | **Deprecated → MinimumOrderQuantity**. Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| LeadTimeDays | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DeliveryFrequency | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveFrom | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveTo | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| QualityRequirement | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| MinimumOrderQuantity | decimal(18, 4) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| IsPreferred | bit | N | (0) |  | Cờ logic xác định preferred (thường 1: có/đúng, 0: không/sai) |

## TemperatureLog
_QC & an toàn thực phẩm · BẢNG NGHIỆP VỤ – chi tiết/sự kiện (theo tên bảng)_ – Nhật ký đo nhiệt độ tại thiết bị, khu vực, thực phẩm hoặc công đoạn.
Ghi chú: Dữ liệu phát sinh; cần workflow, trạng thái, audit và transaction.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| StorageZoneId | uniqueidentifier | Y |  | StorageZone | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| StorageLocationId | uniqueidentifier | Y |  | StorageLocation | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MaterialLotId | uniqueidentifier | Y |  | MaterialLot | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ProductionBatchId | uniqueidentifier | Y |  | ProductionBatche | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| MeasurementType | nvarchar(40) | N |  |  | Thông tin MeasurementType của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| TemperatureC | decimal(6, 2) | N |  |  | Thông tin TemperatureC của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| MinAllowedC | decimal(6, 2) | Y |  |  | Thông tin MinAllowedC của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| MaxAllowedC | decimal(6, 2) | Y |  |  | Thông tin MaxAllowedC của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| IsWithinRange | bit | Y |  |  | Cờ Boolean cho biết trạng thái WithinRange. |
| MeasuredAt | datetime2(0) | N | sysdatetime() |  | Thời điểm đo/ghi nhận. |
| DeviceCode | nvarchar(100) | Y |  |  | Thông tin DeviceCode của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| SourceChannel | nvarchar(50) | Y |  |  | Thông tin SourceChannel của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| RecordedBy | uniqueidentifier | Y |  |  | Thông tin RecordedBy của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| CorrectiveAction | nvarchar(2000) | Y |  |  | Thông tin CorrectiveAction của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| EvidenceUrl | nvarchar(1000) | Y |  |  | Thông tin EvidenceUrl của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## TestDemo
_cần xác nhận · LEGACY – cần rà soát_ – Kiểm thử/Demo
Ghi chú: Ứng viên ngừng sử dụng; xác minh dependency trước khi loại bỏ.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| FieldString | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| FieldDateTime | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| FieldDropdownId | uniqueidentifier | Y |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| FieldNumber | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| FieldTextArea | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| FieldImageSingle | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| FieldCheckBox | bit | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| FieldSwitch | bit | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## UnitMeasure
_Nguyên liệu & đơn vị · DANH MỤC (Master Data)_ – Đơn vị tính
BRD: **UOM** (Material/Reference Data) – Danh mục đơn vị tính dùng cho nguyên liệu và giao dịch.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(50) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(255) | Y |  |  | Tên đối tượng. |
| UomType | nvarchar(100) | Y |  |  | Thông tin UomType của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DecimalPrecision | int | Y |  |  | Thông tin DecimalPrecision của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Symbol | nvarchar(max) | Y |  |  | Thông tin Symbol của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| UnitType | nvarchar(max) | Y |  |  | Thông tin UnitType của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |

## UnitsOfMeasure_Legacy_20261113
_cần xác nhận · LEGACY – cần rà soát_ – Chưa có mô tả nghiệp vụ đủ căn cứ trong nguồn đầu vào; cần BA xác nhận.
Ghi chú: Bảng legacy; không phát triển CRUD mới, chỉ phục vụ đối soát/tương thích.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(50) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(255) | Y |  |  | Tên đối tượng. |
| UomType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| DecimalPrecision | int | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Symbol | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| UnitType | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## UomConversion
_Nguyên liệu & đơn vị · DANH MỤC (Master Data)_ – Quy đổi đơn vị tính
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| FromUomId | uniqueidentifier | N |  |  | **Deprecated → FromUnitId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ToUomId | uniqueidentifier | N |  |  | **Deprecated → ToUnitId**. Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ConversionFactor | decimal(28, 10) | N |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveFrom | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| EffectiveTo | datetime | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| FromUnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ToUnitId | uniqueidentifier | N | '00000000-0000-0000-0000-000000000000' | UnitMeasure | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IsActive | bit | N | (0) |  | Cờ trạng thái hoạt động. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## User
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Lưu tài khoản người dùng và thông tin phục vụ đăng nhập, phân quyền.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| UserName | nvarchar(max) | Y |  |  | Tên hiển thị của đối tượng |
| PassWord | nvarchar(max) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| PersonId | uniqueidentifier | Y |  | Person | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IsActive | bit | N | (0) |  | Cờ trạng thái hoạt động. |
| IsAdmin | bit | N | (0) |  | Cờ logic xác định admin (thường 1: có/đúng, 0: không/sai) |
| IsDefault | bit | N | (0) |  | Cờ xác định giá trị mặc định |

## UserClaim
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Lưu tài khoản người dùng và thông tin phục vụ đăng nhập, phân quyền.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| UserId | uniqueidentifier | Y |  | User | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| UserGroupId | uniqueidentifier | Y |  | UserGroup | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |

## UserExternalIdentity
_Message · TÍCH HỢP / MESSAGE_ – Ánh xạ người dùng nội bộ với định danh tài khoản bên ngoài trên từng nền tảng.
_Thiếu cột chuẩn: DeletedDate, DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| UserId | uniqueidentifier | Y |  | User | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| EmployeeId | uniqueidentifier | Y |  | Employee | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| IntegrationChannelId | uniqueidentifier | N |  | IntegrationChannel | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ExternalUserId | nvarchar(255) | N |  |  | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ExternalUsername | nvarchar(255) | Y |  |  | Thông tin ExternalUsername của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| DisplayName | nvarchar(255) | Y |  |  | Thông tin DisplayName của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| LinkedAt | datetime2(0) | N | sysdatetime() |  | Thời điểm/ngày Linked của nghiệp vụ. |
| IdentityStatus | nvarchar(30) | N | N'Active' |  | Thông tin IdentityStatus của bản ghi; cần BA/PO xác nhận quy tắc nghiệp vụ và nguồn dữ liệu. |
| LastSeenAt | datetime2(0) | Y |  |  | Thời điểm/ngày LastSeen của nghiệp vụ. |

## UserGroup
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Lưu tài khoản người dùng và thông tin phục vụ đăng nhập, phân quyền.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| IsDefault | bit | N | (0) |  | Cờ xác định giá trị mặc định |

## UserRole
_Tổ chức & nhân sự · DANH MỤC (Master Data)_ – Lưu tài khoản người dùng và thông tin phục vụ đăng nhập, phân quyền.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| UserGroupId | uniqueidentifier | Y |  | UserGroup | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| ObjectMenuId | uniqueidentifier | Y |  | ObjectMenu | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| AllowAccess | bit | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AllowAdd | bit | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AllowEdit | bit | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AllowDelete | bit | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AllowPrint | bit | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AllowExport | bit | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| AllowImport | bit | N | (0) |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |

## Vehicle
_Vận hành bếp & phương tiện · DANH MỤC (Master Data)_ – Lưu thông tin phương tiện phục vụ vận chuyển.
BRD: **Vehicle** (Resource/Master Data) – Quản lý phương tiện vận chuyển, được tham chiếu trong Delivery Route và Loading.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.
_Thiếu cột chuẩn: DeleteddBy_

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| LicensePlate | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| VehicleType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Capacity | decimal(18, 2) | Y |  |  | Công suất/sức chứa |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |

## Warehouse
_Nguyên liệu & đơn vị · DANH MỤC (Master Data)_ – Lưu dữ liệu kho và nghiệp vụ quản lý tồn kho, nhập, xuất hoặc điều chuyển.
BRD: **Warehouse** (Warehouse/Master Data) – Danh mục kho; phân biệt kho nguyên liệu, kho thành phẩm và các loại kho khác.
Ghi chú: Dữ liệu nền; ưu tiên làm CRUD, import, trạng thái, phân quyền và mã duy nhất.

| Cột | Kiểu | Null | Default | → Bảng (logic) | Mô tả |
|---|---|---|---|---|---|
| Code | nvarchar(255) | Y |  |  | Mã nhận diện đối tượng. |
| Name | nvarchar(max) | Y |  |  | Tên đối tượng. |
| WarehouseType | nvarchar(100) | Y |  |  | Nguồn SQL chỉ cung cấp tên và kiểu dữ liệu; chưa có mô tả nghiệp vụ đủ căn cứ cho cột này. |
| Address | nvarchar(max) | Y |  |  | Địa chỉ |
| Status | nvarchar(50) | Y |  |  | Trạng thái bản ghi/đối tượng. |
| Note | nvarchar(max) | Y |  |  | Ghi chú. |
| CompanyId | uniqueidentifier | Y |  | Company | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
| OrganizationUnitId | uniqueidentifier | Y |  | OrganizationUnit | Định danh tham chiếu logic; bảng đích cần đối chiếu tại sheet 04_Quan_he. |
