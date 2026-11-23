/*
  MENU-4 (D-75 d, e, f – anh chốt 2026-11-23) – script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE). Gộp cả MENU-3.
  Ẩn khỏi menu (ObjectMenu.IsActive = 0, giữ dòng + UserRole):
    - Nhóm A – 18 bảng con (nhập / xem trong màn cha): Loại suất & đơn giá, Chi tiết đơn đặt suất, Chi tiết định lượng, Chi tiết thực đơn,
      Chi tiết đề nghị mua / đơn mua / phiếu nhập / phiếu xuất / kiểm kê / kế hoạch sản xuất / kiểm tra hàng nhập, Tiêu chí kiểm tra (MENU-4.5 đã làm),
      Điểm dừng, Diễn biến giao hàng, Lịch sử xử lý phản ánh, Các bước duyệt, Lịch sử duyệt, Chứng từ trong yêu cầu duyệt.
    - Nhóm B – màn chưa làm: Phiếu chuyển kho, Phiếu điều chỉnh tồn (nghiệp vụ thật: nút Chuyển kho / Điều chỉnh tồn trên màn Tồn kho;
      mục "Phê duyệt › Phiếu điều chỉnh tồn" #/wf-inbox?type=scm-adjustment GIỮ NGUYÊN).
    - Nhóm D – Thông tin cá nhân (#/adm-person, trùng Tài khoản / Nhân viên).
  KHÔNG ẩn #/wf-inbox "Hộp phê duyệt" (nhóm Nhân sự): dòng này là quyền của toàn bộ API duyệt – ẩn sẽ làm người dùng mất hộp duyệt.
  Giao diện tự bỏ mục này khỏi sidebar khi đã có "Tất cả yêu cầu duyệt" (core/app.js).
  Quyền API của bảng con đã chuyển sang màn cha trong code (ScreenMap.ParentByClass) – cần deploy API cùng lúc / trước khi chạy script.
  Ghi ModifiedDate / ModifiedBy (tài khoản system) + AuditChange từng dòng. Chạy lại lần 2 không đổi gì (idempotent). Số dòng UserRole phải không đổi.

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật).
  CÁCH CHẠY (chỉ sau khi backup):
    Chạy thử (in danh sách sẽ ẩn, KHÔNG ghi):
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/MENU4_cleanup_menu.sql -s "," -W -o menu4-preview.csv
    Chạy thật:
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/MENU4_cleanup_menu.sql -s "," -W -o menu4-applied.csv
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
DECLARE @Links TABLE (MetaLink NVARCHAR(400) PRIMARY KEY, Grp NVARCHAR(10), OpenAt NVARCHAR(200));
INSERT INTO @Links (MetaLink, Grp, OpenAt) VALUES
    (N'#/crm-contract-meal', N'A', N'Form + tab chi tiết Hợp đồng'),
    (N'#/crm-order-line', N'A', N'Đơn đặt suất / Đăng ký suất tuần'),
    (N'#/menu-recipe-line', N'A', N'Công thức chế biến'),
    (N'#/menu-menu-line', N'A', N'Thực đơn tuần'),
    (N'#/scm-pr-line', N'A', N'Đề nghị mua hàng'),
    (N'#/scm-po-line', N'A', N'Đơn mua hàng'),
    (N'#/scm-gr-line', N'A', N'Phiếu nhập kho'),
    (N'#/scm-issue-line', N'A', N'Phiếu xuất sản xuất'),
    (N'#/scm-stockcount-line', N'A', N'Phiếu kiểm kê'),
    (N'#/prod-plan-line', N'A', N'Kế hoạch sản xuất'),
    (N'#/qc-inspection-line', N'A', N'Kiểm tra hàng nhập'),
    (N'#/qc-checklist-item', N'A', N'Bộ kiểm tra ATTP (tab Tiêu chí)'),
    (N'#/log-stop', N'A', N'Chuyến giao hàng'),
    (N'#/log-event', N'A', N'Theo dõi chuyến giao'),
    (N'#/log-complaint-history', N'A', N'Phản ánh khách hàng'),
    (N'#/wf-step', N'A', N'Chi tiết yêu cầu (Hộp phê duyệt)'),
    (N'#/wf-decision', N'A', N'Chi tiết yêu cầu (Hộp phê duyệt)'),
    (N'#/wf-item', N'A', N'Chi tiết yêu cầu (Hộp phê duyệt)'),
    (N'#/scm-transfer', N'B', N'Nút Chuyển kho trên màn Tồn kho'),
    (N'#/scm-adjustment', N'B', N'Nút Điều chỉnh tồn trên màn Tồn kho'),
    (N'#/adm-person', N'D', N'Tài khoản / Nhân viên');

DECLARE @Hide TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(500), Name NVARCHAR(500), Grp NVARCHAR(10), OpenAt NVARCHAR(200));
INSERT INTO @Hide (Id, MetaLink, Name, Grp, OpenAt)
SELECT m.Id, m.MetaLink, m.Name, l.Grp, l.OpenAt
FROM dbo.ObjectMenu AS m JOIN @Links AS l ON l.MetaLink = m.MetaLink
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.IsActive = 1;

-- In cả khi chạy thử.
SELECT h.Grp AS [Nhóm], h.MetaLink AS [Ẩn (IsActive = 0)], h.Name AS [Tên], h.OpenAt AS [Nhập / xem ở] FROM @Hide AS h ORDER BY h.Grp, h.MetaLink;
SELECT l.MetaLink AS [Đã ẩn / không có – bỏ qua], o.Name AS [Tên], o.IsActive AS [IsActive]
FROM @Links AS l LEFT JOIN dbo.ObjectMenu AS o ON o.MetaLink = l.MetaLink AND ISNULL(o.IsDeleted, 0) = 0
WHERE NOT EXISTS (SELECT 1 FROM @Hide AS h WHERE h.MetaLink = l.MetaLink)
ORDER BY l.MetaLink;

IF @DryRun = 1
BEGIN
    ROLLBACK TRANSACTION;
    PRINT N'Chạy thử: không ghi gì. Chạy thật: thêm -v DryRun=0 (sau khi backup, sau khi deploy API MENU-4).';
    RETURN;
END

UPDATE o SET o.IsActive = 0, o.ModifiedDate = @Now, o.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS o JOIN @Hide AS h ON h.Id = o.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), h.Id, N'Modified', N'ObjectMenu',
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | MENU-4 dọn menu (D-75 e) – nhập / xem ở: ' + h.OpenAt + N' <br />' + CHAR(13) + CHAR(10)
       + N'- IsActive: ''True'' → ''False'' <br />',
       @Now, @SystemUser, 0
FROM @Hide AS h;

DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
IF @RolesAfter <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – hủy (quyền không được đổi).', 1;

COMMIT TRANSACTION;
SELECT (SELECT COUNT(*) FROM @Hide) AS [Đã ẩn], @RolesBefore AS [UserRole trước], @RolesAfter AS [UserRole sau];
PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì.';
