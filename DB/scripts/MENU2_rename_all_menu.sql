/*
  MENU-2 (D-75, anh chốt 2026-11-22) – script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE).
  1) Đổi tên 110 mục menu theo bảng ROADMAP MENU-2 (khóa MetaLink). Chỉ đổi khi Name đang là TÊN CŨ trong bảng hoặc "Duyệt – " + tên cũ
     – tên admin đã tự sửa khác thì GIỮ NGUYÊN (in ra để anh xem). Giữ Id, MetaLink, ShortName, OrderShow, ParentId và quyền (UserRole).
  2) Ẩn nhóm "Database Architecture" (#/group/11) + #/dbx-explorer, #/dbx-gap: IsActive = 0 (D-58), không xóa dòng.
  Ghi ModifiedDate / ModifiedBy (tài khoản system) + AuditChange từng dòng. Chạy lại lần 2 không đổi gì (idempotent).
  Tên bước của yêu cầu duyệt ĐÃ tạo (ApprovalStep) giữ nguyên – không sửa lịch sử. Số dòng UserRole phải không đổi (khác → hủy).

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật).
  CÁCH CHẠY (chỉ sau khi backup):
    Chạy thử (in tên trước / sau, KHÔNG ghi):
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/MENU2_rename_all_menu.sql -s "," -W -o menu2-preview.csv
    Chạy thật:
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/MENU2_rename_all_menu.sql -s "," -W -o menu2-applied.csv
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME()); -- giờ Việt Nam như ứng dụng

BEGIN TRANSACTION;

DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);
DECLARE @Rename TABLE (MetaLink NVARCHAR(500) PRIMARY KEY, OldName NVARCHAR(500), NewName NVARCHAR(500));
INSERT INTO @Rename (MetaLink, OldName, NewName) VALUES
    (N'#/group/01', N'Tổng quan điều hành', N'Điều hành'),
    (N'#/dash-exec', N'Tổng quan điều hành', N'Bảng điều hành'),
    (N'#/group/02', N'Kinh doanh & Đơn hàng', N'Kinh doanh'),
    (N'#/crm-customer', N'Hồ sơ khách hàng 360°', N'Khách hàng'),
    (N'#/crm-contact', N'Đầu mối liên hệ khách hàng', N'Người liên hệ'),
    (N'#/crm-location', N'Điểm giao hàng & khung giờ nhận', N'Điểm giao hàng'),
    (N'#/crm-price-policy', N'Chính sách giá theo KH/HĐ', N'Chính sách giá'),
    (N'#/crm-contract', N'Hợp đồng suất ăn', N'Hợp đồng'),
    (N'#/crm-contract-meal', N'Loại suất theo hợp đồng', N'Loại suất & đơn giá'),
    (N'#/crm-order', N'Đơn đặt suất ăn', N'Đơn đặt suất'),
    (N'#/crm-order-line', N'Số lượng theo ca / loại suất', N'Chi tiết đơn đặt suất'),
    (N'#/crm-reconciliation', N'Đối soát Đặt–SX–Đóng–Giao–Nhận', N'Đối soát suất ăn'),
    (N'#/menu-dish', N'Danh mục món ăn', N'Món ăn'),
    (N'#/menu-recipe', N'Công thức & phiên bản', N'Công thức chế biến'),
    (N'#/menu-recipe-line', N'Định lượng nguyên liệu', N'Chi tiết định lượng'),
    (N'#/menu-menu', N'Thực đơn & phiên bản', N'Thực đơn tuần'),
    (N'#/menu-menu-line', N'Lịch món theo ngày/bữa', N'Chi tiết thực đơn'),
    (N'#/menu-assignment', N'Gán thực đơn cho KH / điểm giao / loại suất', N'Thực đơn riêng cho khách'),
    (N'#/scm-supplier', N'Hồ sơ nhà cung cấp', N'Nhà cung cấp'),
    (N'#/scm-ingredient', N'Danh mục nguyên liệu', N'Nguyên liệu'),
    (N'#/scm-supplier-material', N'Bảng giá & MOQ theo NCC', N'Bảng giá nhà cung cấp'),
    (N'#/scm-warehouse', N'Danh mục kho', N'Kho'),
    (N'#/scm-zone', N'Khu vực bảo quản & ngưỡng nhiệt', N'Khu bảo quản'),
    (N'#/scm-location', N'Vị trí lưu trữ (bin)', N'Vị trí kệ'),
    (N'#/scm-inv-policy', N'Min / Max / Safety / Reorder', N'Định mức tồn kho'),
    (N'#/scm-requirement', N'Tính nhu cầu nguyên liệu (MRP)', N'Tính nhu cầu nguyên liệu'),
    (N'#/scm-pr-line', N'Dòng đề nghị mua', N'Chi tiết đề nghị mua'),
    (N'#/scm-po-line', N'Dòng đơn mua', N'Chi tiết đơn mua'),
    (N'#/scm-gr-line', N'Dòng nhập – lô, HSD', N'Chi tiết phiếu nhập'),
    (N'#/scm-lot', N'Lô & hạn sử dụng (FEFO)', N'Lô hàng & hạn dùng'),
    (N'#/scm-inv-txn', N'Sổ cái kho (ledger)', N'Sổ kho'),
    (N'#/scm-inv-balance', N'Tồn kho theo kho / lô', N'Tồn kho'),
    (N'#/scm-issue', N'Phiếu xuất cho sản xuất', N'Phiếu xuất sản xuất'),
    (N'#/scm-issue-line', N'Dòng xuất theo lô', N'Chi tiết phiếu xuất'),
    (N'#/scm-stockcount-line', N'Dòng kiểm kê & chênh lệch', N'Chi tiết kiểm kê'),
    (N'#/scm-consumption-var', N'Tiêu hao lý thuyết vs thực tế', N'Chênh lệch tiêu hao'),
    (N'#/cost-standard', N'Giá thành chuẩn theo công thức', N'Giá thành chuẩn'),
    (N'#/prod-line', N'Dây chuyền & năng lực', N'Dây chuyền bếp'),
    (N'#/prod-workstation', N'Trạm làm việc bếp', N'Trạm bếp'),
    (N'#/prod-plan', N'Kế hoạch sản xuất theo ngày/ca', N'Kế hoạch sản xuất'),
    (N'#/prod-plan-line', N'Dòng kế hoạch theo món', N'Chi tiết kế hoạch sản xuất'),
    (N'#/prod-batch', N'Bảng điều độ mẻ (ca × dây chuyền)', N'Mẻ nấu'),
    (N'#/prod-output', N'Ghi nhận sản lượng', N'Sản lượng'),
    (N'#/cost-actual', N'Giá thành thực tế theo mẻ/lệnh', N'Giá thành thực tế'),
    (N'#/cost-meal', N'Giá thành mỗi suất & Margin', N'Giá vốn suất ăn'),
    (N'#/prod-waste', N'Ghi nhận hao hụt', N'Hao hụt'),
    (N'#/group/06', N'QC & An toàn thực phẩm', N'Chất lượng & ATTP'),
    (N'#/qc-checklist', N'Bộ checklist ATTP', N'Bộ kiểm tra ATTP'),
    (N'#/qc-checklist-item', N'Tiêu chí checklist', N'Tiêu chí kiểm tra'),
    (N'#/qc-inspection', N'Phiếu QC đầu vào', N'Kiểm tra hàng nhập'),
    (N'#/qc-inspection-line', N'Tiêu chí QC theo dòng nhập', N'Chi tiết kiểm tra hàng nhập'),
    (N'#/qc-checklist-result', N'Thực hiện checklist theo ca/mẻ', N'Kiểm tra theo ca / mẻ'),
    (N'#/qc-temperature', N'Nhật ký nhiệt độ kho/mẻ', N'Nhật ký nhiệt độ'),
    (N'#/qc-cleaning', N'Mẻ vệ sinh khay/hộp', N'Vệ sinh khay hộp'),
    (N'#/qc-cleaning-result', N'Checklist vệ sinh', N'Kiểm tra vệ sinh'),
    (N'#/qc-supplier-issue', N'Sự cố / NCR nhà cung cấp', N'Sự cố nhà cung cấp'),
    (N'#/group/07', N'Đóng gói & Giao nhận', N'Đóng gói & Giao hàng'),
    (N'#/log-vehicle', N'Danh mục phương tiện', N'Xe giao hàng'),
    (N'#/log-packing-order', N'Lệnh đóng suất', N'Lệnh đóng gói'),
    (N'#/log-route', N'Lập tuyến & điều phối', N'Chuyến giao hàng'),
    (N'#/log-stop', N'Thứ tự điểm dừng', N'Điểm dừng'),
    (N'#/log-event', N'Timeline sự kiện giao hàng', N'Diễn biến giao hàng'),
    (N'#/log-pod', N'Xác nhận giao hàng (POD)', N'Xác nhận đã giao'),
    (N'#/log-container-return', N'Thu hồi khay/hộp', N'Thu hồi khay hộp'),
    (N'#/log-return-inspection', N'Kiểm tra vật dụng thu hồi', N'Kiểm tra khay hộp thu hồi'),
    (N'#/log-packaging-ledger', N'Sổ bao bì theo khách hàng', N'Khay hộp theo khách'),
    (N'#/log-packing-result', N'Kết quả đóng suất', N'Kết quả đóng gói'),
    (N'#/log-gps', N'Bản đồ vị trí xe', N'Vị trí xe'),
    (N'#/log-complaint', N'Khiếu nại khách hàng', N'Phản ánh khách hàng'),
    (N'#/log-complaint-history', N'Lịch sử xử lý khiếu nại', N'Lịch sử xử lý phản ánh'),
    (N'#/group/08', N'Nhân sự & Phê duyệt', N'Nhân sự'),
    (N'#/hr-shift', N'Danh mục ca làm việc (4 ca)', N'Ca làm việc'),
    (N'#/hr-shift-assign', N'Lịch phân ca nhân sự', N'Phân ca'),
    (N'#/hr-attendance', N'Chấm công theo ca', N'Chấm công'),
    (N'#/wf-inbox', N'Hộp phê duyệt tập trung', N'Hộp phê duyệt'),
    (N'#/wf-step', N'Bước phê duyệt', N'Các bước duyệt'),
    (N'#/wf-decision', N'Quyết định phê duyệt', N'Lịch sử duyệt'),
    (N'#/wf-item', N'Đối tượng gắn với yêu cầu', N'Chứng từ trong yêu cầu duyệt'),
    (N'#/group/09', N'Tích hợp & KPI', N'Tích hợp & Chỉ số'),
    (N'#/int-channel', N'Kênh Zalo / Telegram / Facebook', N'Kênh nhắn tin'),
    (N'#/int-bot', N'Cấu hình Bot', N'Bot'),
    (N'#/int-identity', N'Liên kết tài khoản Zalo/Telegram', N'Liên kết tài khoản chat'),
    (N'#/int-inbound', N'Tin nhắn vào', N'Tin nhắn đến'),
    (N'#/int-outbound', N'Tin nhắn ra', N'Tin nhắn đi'),
    (N'#/kpi-definition', N'Định nghĩa KPI', N'Danh mục chỉ số'),
    (N'#/kpi-daily', N'KPI ngày', N'Chỉ số theo ngày'),
    (N'#/kpi-monthly', N'KPI tháng', N'Chỉ số theo tháng'),
    (N'#/kpi-refresh', N'Nhật ký làm mới KPI', N'Nhật ký tính chỉ số'),
    (N'#/adm-company', N'Công ty / Pháp nhân', N'Công ty'),
    (N'#/adm-orgunit', N'Cây đơn vị tổ chức', N'Sơ đồ tổ chức'),
    (N'#/adm-employee', N'Hồ sơ nhân viên', N'Nhân viên'),
    (N'#/adm-person', N'Thông tin cá nhân của tài khoản', N'Thông tin cá nhân'),
    (N'#/adm-user', N'Tài khoản người dùng', N'Tài khoản'),
    (N'#/adm-permission', N'Ma trận quyền Nhóm × Chức năng', N'Phân quyền'),
    (N'#/adm-usergroup', N'Nhóm người dùng (Vai trò)', N'Nhóm quyền'),
    (N'#/adm-objectmenu', N'Cấu hình menu chức năng (ObjectMenu)', N'Cấu hình menu'),
    (N'#/adm-position', N'Danh mục chức vụ', N'Chức vụ'),
    (N'#/adm-admin-unit', N'Vùng / Tỉnh / Huyện / Xã', N'Địa giới hành chính'),
    (N'#/adm-document', N'Kho tài liệu đính kèm', N'Tài liệu đính kèm'),
    (N'#/adm-access-log', N'Nhật ký máy truy cập (IP/PC)', N'Nhật ký truy cập'),
    (N'#/adm-merge-conflict', N'Xung đột chuẩn hóa dữ liệu lõi', N'Dữ liệu trùng cần gộp'),
    (N'#/adm-reference', N'Danh mục tham chiếu (Meal Type, lý do, loại…)', N'Danh mục dùng chung'),
    (N'#/wf-inbox?type=menu-recipe', N'Công thức & phiên bản', N'Công thức chế biến'),
    (N'#/wf-inbox?type=crm-price-policy', N'Chính sách giá theo KH/HĐ', N'Chính sách giá'),
    (N'#/wf-inbox?type=crm-contract', N'Hợp đồng suất ăn', N'Hợp đồng'),
    (N'#/wf-inbox?type=menu-menu', N'Thực đơn & phiên bản', N'Thực đơn tuần'),
    (N'#/wf-inbox?type=crm-order', N'Đơn đặt suất ăn', N'Đơn đặt suất'),
    (N'#/wf-inbox?type=prod-plan', N'Kế hoạch sản xuất theo ngày/ca', N'Kế hoạch sản xuất'),
    (N'#/wf-inbox?type=qc-inspection', N'Phiếu QC đầu vào', N'Kiểm tra hàng nhập'),
    (N'#/wf-inbox?type=wf-inbox', N'Hộp phê duyệt tập trung', N'Tất cả yêu cầu duyệt');

DECLARE @Map TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(500), OldName NVARCHAR(500), NewName NVARCHAR(500));
INSERT INTO @Map (Id, MetaLink, OldName, NewName)
SELECT m.Id, m.MetaLink, m.Name, r.NewName
FROM dbo.ObjectMenu AS m JOIN @Rename AS r ON r.MetaLink = m.MetaLink
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.Name IN (r.OldName, N'Duyệt – ' + r.OldName);

DECLARE @Hide TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(500), Name NVARCHAR(500));
INSERT INTO @Hide (Id, MetaLink, Name)
SELECT m.Id, m.MetaLink, m.Name FROM dbo.ObjectMenu AS m
WHERE m.MetaLink IN (N'#/group/11', N'#/dbx-explorer', N'#/dbx-gap') AND ISNULL(m.IsDeleted, 0) = 0 AND m.IsActive = 1;

-- Bảng đối chiếu (in cả khi chạy thử).
SELECT m.MetaLink AS [MetaLink], m.OldName AS [Tên cũ], m.NewName AS [Tên mới] FROM @Map AS m ORDER BY m.MetaLink;
SELECT r.MetaLink AS [MetaLink – giữ tên (admin đã sửa / chưa có)], o.Name AS [Tên hiện tại], r.NewName AS [Tên mới theo bảng]
FROM @Rename AS r LEFT JOIN dbo.ObjectMenu AS o ON o.MetaLink = r.MetaLink AND ISNULL(o.IsDeleted, 0) = 0
WHERE NOT EXISTS (SELECT 1 FROM @Map AS m WHERE m.MetaLink = r.MetaLink) AND ISNULL(o.Name, N'') <> r.NewName
ORDER BY r.MetaLink;
SELECT h.MetaLink AS [Ẩn (IsActive = 0)], h.Name AS [Tên] FROM @Hide AS h ORDER BY h.MetaLink;

IF @DryRun = 1
BEGIN
    ROLLBACK TRANSACTION;
    PRINT N'Chạy thử: không ghi gì. Chạy thật: thêm -v DryRun=0 (sau khi backup).';
    RETURN;
END

UPDATE o SET o.Name = m.NewName, o.ModifiedDate = @Now, o.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS o JOIN @Map AS m ON m.Id = o.Id;

UPDATE o SET o.IsActive = 0, o.ModifiedDate = @Now, o.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS o JOIN @Hide AS h ON h.Id = o.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), m.Id, N'Modified', N'ObjectMenu',
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | MENU-2 đổi tên menu (D-75) <br />' + CHAR(13) + CHAR(10)
       + N'- Name: ''' + m.OldName + N''' → ''' + m.NewName + N''' <br />',
       @Now, @SystemUser, 0
FROM @Map AS m;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), h.Id, N'Modified', N'ObjectMenu',
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | MENU-2 ẩn nhóm Database Architecture (D-58) <br />' + CHAR(13) + CHAR(10)
       + N'- IsActive: ''True'' → ''False'' <br />',
       @Now, @SystemUser, 0
FROM @Hide AS h;

DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
IF @RolesAfter <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – hủy (quyền không được đổi).', 1;

COMMIT TRANSACTION;
SELECT (SELECT COUNT(*) FROM @Map) AS [Đã đổi tên], (SELECT COUNT(*) FROM @Hide) AS [Đã ẩn], @RolesBefore AS [UserRole trước], @RolesAfter AS [UserRole sau];
PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì.';
