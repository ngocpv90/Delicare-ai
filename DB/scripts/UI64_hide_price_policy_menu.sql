/*
  UI-6.4 (D-83 – anh chốt 2026-11-23): Chính sách giá → "Phụ lục điều chỉnh giá" trong hợp đồng.
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    (1) Ẩn mục menu "Chính sách giá" (#/crm-price-policy): ObjectMenu.IsActive = 0, GIỮ dòng + UserRole.
    (2) Mở lại mục quyền duyệt "Phê duyệt › Chính sách giá" (#/wf-inbox?type=crm-price-policy) – nay là quyền duyệt phụ lục điều chỉnh giá (CEO – D-79 d).
    (3) Chính sách giá theo KHÁCH (ContractId trống) → gắn vào hợp đồng ĐANG HIỆU LỰC DUY NHẤT của khách đó (in trước / sau).
        Khách có 0 hoặc > 1 hợp đồng hiệu lực → KHÔNG chuyển, liệt kê để anh quyết (trong lúc chờ, tra giá vẫn đọc bản theo khách – có cảnh báo trong log).
  Ghi ModifiedDate / ModifiedBy (tài khoản system) + AuditChange. Chạy lại lần 2 không đổi gì. Số dòng UserRole phải không đổi.

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật). DB dev: chạy thật ngay trong task (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI64_hide_price_policy_menu.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Today DATE = CAST(@Now AS DATE);

BEGIN TRANSACTION;
DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);

DECLARE @Menu TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(500), Name NVARCHAR(500), NewActive BIT);
INSERT INTO @Menu (Id, MetaLink, Name, NewActive)
SELECT m.Id, m.MetaLink, m.Name, CASE m.MetaLink WHEN N'#/crm-price-policy' THEN 0 ELSE 1 END
FROM dbo.ObjectMenu AS m
WHERE ISNULL(m.IsDeleted, 0) = 0
  AND ((m.MetaLink = N'#/crm-price-policy' AND m.IsActive = 1) OR (m.MetaLink = N'#/wf-inbox?type=crm-price-policy' AND m.IsActive = 0));

SELECT MetaLink AS [Mục menu], Name AS [Tên], CASE NewActive WHEN 0 THEN N'Ẩn' ELSE N'Mở lại (quyền duyệt)' END AS [Đổi] FROM @Menu;

-- Hợp đồng đang hiệu lực của từng khách
DECLARE @Active TABLE (CustomerId UNIQUEIDENTIFIER, ContractId UNIQUEIDENTIFIER, Number NVARCHAR(200));
INSERT INTO @Active
SELECT c.CustomerId, c.Id, ISNULL(c.ContractNumber, c.ContractNo) FROM dbo.Contract AS c
WHERE ISNULL(c.IsDeleted, 0) = 0 AND c.Status = N'Active' AND c.EffectiveFrom <= @Today AND (c.EffectiveTo IS NULL OR c.EffectiveTo >= @Today);

DECLARE @Move TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, Code NVARCHAR(100), CustomerId UNIQUEIDENTIFIER, ContractId UNIQUEIDENTIFIER NULL, Number NVARCHAR(200) NULL, Cnt INT);
INSERT INTO @Move (Id, Code, CustomerId, ContractId, Number, Cnt)
SELECT p.Id, p.Code, p.CustomerId,
       CASE WHEN x.Cnt = 1 THEN x.OnlyId END, CASE WHEN x.Cnt = 1 THEN x.OnlyNo END, ISNULL(x.Cnt, 0)
FROM dbo.PricePolicie AS p
OUTER APPLY (SELECT COUNT(*) AS Cnt, MAX(CAST(a.ContractId AS NVARCHAR(36))) AS OnlyIdTxt, MAX(a.Number) AS OnlyNo FROM @Active AS a WHERE a.CustomerId = p.CustomerId) AS y
OUTER APPLY (SELECT y.Cnt, TRY_CAST(y.OnlyIdTxt AS UNIQUEIDENTIFIER) AS OnlyId, y.OnlyNo) AS x
WHERE ISNULL(p.IsDeleted, 0) = 0 AND p.ContractId IS NULL;

SELECT m.Code AS [Chính sách theo khách], c.Code AS [Mã KH], c.DisplayName AS [Khách hàng], p.MealType AS [Loại suất], p.UnitPrice AS [Đơn giá],
       CONVERT(VARCHAR(10), p.EffectiveFrom, 103) AS [Từ], ISNULL(CONVERT(VARCHAR(10), p.EffectiveTo, 103), N'–') AS [Đến],
       CASE WHEN m.ContractId IS NOT NULL THEN N'Chuyển sang HĐ ' + m.Number
            WHEN m.Cnt = 0 THEN N'KHÔNG chuyển – khách không có hợp đồng hiệu lực (chờ anh quyết)'
            ELSE N'KHÔNG chuyển – khách có ' + CAST(m.Cnt AS NVARCHAR(10)) + N' hợp đồng hiệu lực (chờ anh quyết)' END AS [Xử lý]
FROM @Move AS m JOIN dbo.PricePolicie AS p ON p.Id = m.Id JOIN dbo.Customer AS c ON c.Id = m.CustomerId
ORDER BY CASE WHEN m.ContractId IS NULL THEN 1 ELSE 0 END, c.Code;

IF @DryRun = 1
BEGIN
    ROLLBACK TRANSACTION;
    SELECT (SELECT COUNT(*) FROM @Menu) AS [Mục menu đổi], (SELECT COUNT(*) FROM @Move WHERE ContractId IS NOT NULL) AS [Sẽ chuyển sang HĐ],
           (SELECT COUNT(*) FROM @Move WHERE ContractId IS NULL) AS [Chờ anh quyết];
    PRINT N'Chạy thử: không ghi gì. Chạy thật: -v DryRun=0.';
    RETURN;
END

UPDATE o SET o.IsActive = m.NewActive, o.ModifiedDate = @Now, o.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS o JOIN @Menu AS m ON m.Id = o.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), m.Id, N'Modified', N'ObjectMenu',
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | UI-6.4 (D-83) phụ lục điều chỉnh giá trong hợp đồng <br />' + CHAR(13) + CHAR(10)
       + N'- IsActive: ''' + CASE m.NewActive WHEN 0 THEN N'True'' → ''False' ELSE N'False'' → ''True' END + N''' <br />',
       @Now, @SystemUser, 0
FROM @Menu AS m;

UPDATE p SET p.ContractId = m.ContractId, p.ModifiedDate = @Now, p.ModifiedBy = @SystemUser
FROM dbo.PricePolicie AS p JOIN @Move AS m ON m.Id = p.Id WHERE m.ContractId IS NOT NULL;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), m.Id, N'Modified', N'PricePolicy',
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | UI-6.4 (D-83) chuyển chính sách theo khách sang hợp đồng ' + m.Number + N' <br />' + CHAR(13) + CHAR(10)
       + N'- ContractId: '''' → ''' + LOWER(CAST(m.ContractId AS NVARCHAR(36))) + N''' <br />',
       @Now, @SystemUser, 0
FROM @Move AS m WHERE m.ContractId IS NOT NULL;

DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
IF @RolesAfter <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – hủy (quyền không được đổi).', 1;

COMMIT TRANSACTION;
SELECT (SELECT COUNT(*) FROM @Menu) AS [Mục menu đổi], (SELECT COUNT(*) FROM @Move WHERE ContractId IS NOT NULL) AS [Đã chuyển sang HĐ],
       (SELECT COUNT(*) FROM @Move WHERE ContractId IS NULL) AS [Chờ anh quyết], @RolesBefore AS [UserRole trước], @RolesAfter AS [UserRole sau];
PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì (mục chờ anh quyết vẫn được liệt kê).';
