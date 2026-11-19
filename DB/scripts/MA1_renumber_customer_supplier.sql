/*
  MA-1.4 (D-63 e, f) – script DỮ LIỆU một lần (không phải migration cấu trúc: không tạo / sửa bảng, cột; không DELETE).
  1) Customer.CustomerType cũ (HOSPITAL, FACTORY, SCHOOL…) → chuyển giá trị sang Industry (nếu Industry trống), CustomerType = BUSINESS.
  2) Đánh lại mã khách hàng chưa đúng mẫu: KHB-NNNNN (doanh nghiệp) / KHC-NNNNN (cá nhân), theo ngày tạo; CIF = Code.
  3) Đánh lại mã nhà cung cấp chưa đúng mẫu: NC-NNNNN, theo ngày tạo.
  Số tiếp nối số lớn nhất đang có của từng tiền tố (kể cả bản ghi đã xóa mềm) – giống IDocumentNumberService; khóa sp_getapplock cùng khóa với ứng dụng.
  Mỗi bản ghi đổi mã có 1 dòng lịch sử AuditChange (Modified, người thao tác = tài khoản "system").
  Bản ghi đã đúng mẫu thì bỏ qua → chạy lại lần 2 không đổi gì (idempotent).

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật) – không có giá trị mặc định để tránh ghi nhầm.
  CÁCH CHẠY (chỉ sau khi backup; SQL Server 2022, mức tương thích 160):
    Chạy thử (in bảng đối chiếu, KHÔNG ghi):
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/MA1_renumber_customer_supplier.sql -s "," -W -o ma1-preview.csv
    Chạy thật (ghi + in bảng đối chiếu đã áp dụng):
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/MA1_renumber_customer_supplier.sql -s "," -W -o ma1-applied.csv
  Sau khi chạy thật: file import dữ liệu gốc (Excel) đang dùng mã KH / NCC cũ phải đổi sang mã mới theo bảng đối chiếu.
  Số lô cũ (NL-…-<mã NCC cũ>) giữ nguyên – là số chứng từ đã phát sinh (D-63 e).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun BIT = $(DryRun);

DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME()); -- giờ Việt Nam như ứng dụng

BEGIN TRANSACTION;

DECLARE @r INT;
EXEC @r = sp_getapplock @Resource = N'DocNo:KHB', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @r < 0 THROW 50102, N'Không lấy được khóa sinh mã KHB.', 1;
EXEC @r = sp_getapplock @Resource = N'DocNo:KHC', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @r < 0 THROW 50102, N'Không lấy được khóa sinh mã KHC.', 1;
EXEC @r = sp_getapplock @Resource = N'DocNo:NC', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @r < 0 THROW 50102, N'Không lấy được khóa sinh mã NC.', 1;

-- ---------------------------------------------------------------- 1) Loại khách hàng
DECLARE @TypeMap TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, OldType NVARCHAR(20), OldIndustry NVARCHAR(100), NewIndustry NVARCHAR(100));
INSERT INTO @TypeMap (Id, OldType, OldIndustry, NewIndustry)
SELECT c.Id, c.CustomerType, c.Industry, ISNULL(NULLIF(LTRIM(RTRIM(c.Industry)), N''), c.CustomerType)
FROM dbo.Customer AS c
WHERE ISNULL(UPPER(LTRIM(RTRIM(c.CustomerType))), N'') NOT IN (N'BUSINESS', N'INDIVIDUAL');

-- ---------------------------------------------------------------- 2) Mã khách hàng
-- Loại hiệu lực: INDIVIDUAL giữ nguyên, còn lại (kể cả giá trị cũ) = BUSINESS.
DECLARE @CustomerMap TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, Prefix NVARCHAR(3), OldCode NVARCHAR(255), OldCif NVARCHAR(50), NewCode NVARCHAR(255), Name NVARCHAR(MAX));

;WITH typed AS (
    SELECT c.Id, c.Code, c.CIF, c.Name, c.CreatedDate,
           CASE WHEN UPPER(LTRIM(RTRIM(c.CustomerType))) = N'INDIVIDUAL' THEN N'KHC' ELSE N'KHB' END AS Prefix
    FROM dbo.Customer AS c
),
need AS (
    -- Chưa đúng mẫu TIỀNTỐ-NNNNN (5–6 chữ số) của đúng loại.
    SELECT t.*
    FROM typed AS t
    WHERE NOT (t.Code LIKE t.Prefix + N'-[0-9][0-9][0-9][0-9][0-9]' OR t.Code LIKE t.Prefix + N'-[0-9][0-9][0-9][0-9][0-9][0-9]')
       OR t.Code IS NULL
),
maxes AS (
    SELECT p.Prefix, ISNULL(x.MaxNo, 0) AS MaxNo
    FROM (VALUES (N'KHB'), (N'KHC')) AS p(Prefix)
    CROSS APPLY (
        SELECT MAX(TRY_CAST(SUBSTRING(c.Code, 5, 20) AS INT)) AS MaxNo -- KHB- / KHC- dài 4 ký tự
        FROM dbo.Customer AS c WHERE c.Code LIKE p.Prefix + N'-%'
    ) AS x
)
INSERT INTO @CustomerMap (Id, Prefix, OldCode, OldCif, NewCode, Name)
SELECT n.Id, n.Prefix, n.Code, n.CIF,
       n.Prefix + N'-' + RIGHT(N'00000' + CAST(m.MaxNo + ROW_NUMBER() OVER (PARTITION BY n.Prefix ORDER BY n.CreatedDate, n.Id) AS NVARCHAR(10)),
           CASE WHEN m.MaxNo + ROW_NUMBER() OVER (PARTITION BY n.Prefix ORDER BY n.CreatedDate, n.Id) > 99999 THEN 6 ELSE 5 END),
       n.Name
FROM need AS n
JOIN maxes AS m ON m.Prefix = n.Prefix;

-- ---------------------------------------------------------------- 3) Mã nhà cung cấp
DECLARE @SupplierMap TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, OldCode NVARCHAR(255), NewCode NVARCHAR(255), Name NVARCHAR(MAX));
DECLARE @MaxNc INT = ISNULL((SELECT MAX(TRY_CAST(SUBSTRING(s.Code, 4, 20) AS INT)) FROM dbo.Supplier AS s WHERE s.Code LIKE N'NC-%'), 0);
INSERT INTO @SupplierMap (Id, OldCode, NewCode, Name)
SELECT s.Id, s.Code,
       N'NC-' + RIGHT(N'00000' + CAST(@MaxNc + ROW_NUMBER() OVER (ORDER BY s.CreatedDate, s.Id) AS NVARCHAR(10)),
           CASE WHEN @MaxNc + ROW_NUMBER() OVER (ORDER BY s.CreatedDate, s.Id) > 99999 THEN 6 ELSE 5 END),
       s.Name
FROM dbo.Supplier AS s
WHERE s.Code IS NULL
   OR NOT (s.Code LIKE N'NC-[0-9][0-9][0-9][0-9][0-9]' OR s.Code LIKE N'NC-[0-9][0-9][0-9][0-9][0-9][0-9]');

-- ---------------------------------------------------------------- Bảng đối chiếu (in ra – sqlcmd -o <file>.csv)
SELECT CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ' ELSE N'ĐÃ ÁP DỤNG' END AS [CheDo],
       N'Customer' AS [Bang], CAST(m.Id AS NVARCHAR(36)) AS [Id], m.OldCode AS [MaCu], m.NewCode AS [MaMoi], m.OldCif AS [CifCu],
       t.OldType AS [LoaiCu], CASE m.Prefix WHEN N'KHC' THEN N'INDIVIDUAL' ELSE N'BUSINESS' END AS [LoaiMoi], m.Name AS [Ten]
FROM @CustomerMap AS m LEFT JOIN @TypeMap AS t ON t.Id = m.Id
UNION ALL
SELECT CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ' ELSE N'ĐÃ ÁP DỤNG' END, N'Supplier', CAST(m.Id AS NVARCHAR(36)), m.OldCode, m.NewCode, NULL, NULL, NULL, m.Name
FROM @SupplierMap AS m
ORDER BY [Bang], [MaMoi];

SELECT N'Tổng' AS [Muc],
       (SELECT COUNT(*) FROM @TypeMap) AS [DoiLoaiKhach],
       (SELECT COUNT(*) FROM @CustomerMap) AS [DoiMaKhach],
       (SELECT COUNT(*) FROM @SupplierMap) AS [DoiMaNcc];

IF @DryRun = 1
BEGIN
    ROLLBACK TRANSACTION;
    PRINT N'Chạy thử: không ghi gì. Chạy thật: thêm -v DryRun=0 (sau khi backup).';
    RETURN;
END

-- ---------------------------------------------------------------- Ghi
UPDATE c SET c.Industry = t.NewIndustry, c.CustomerType = N'BUSINESS', c.ModifiedDate = @Now, c.ModifiedBy = @SystemUser
FROM dbo.Customer AS c JOIN @TypeMap AS t ON t.Id = c.Id;

UPDATE c SET c.Code = m.NewCode, c.CIF = m.NewCode, c.ModifiedDate = @Now, c.ModifiedBy = @SystemUser
FROM dbo.Customer AS c JOIN @CustomerMap AS m ON m.Id = c.Id;

UPDATE s SET s.Code = m.NewCode, s.ModifiedDate = @Now, s.ModifiedBy = @SystemUser
FROM dbo.Supplier AS s JOIN @SupplierMap AS m ON m.Id = s.Id;

-- Lịch sử (AuditChange chỉ ghi thêm) – cùng định dạng ứng dụng ghi.
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.Id, N'Modified', x.EntityName,
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | MA-1.4 (D-63) đánh lại mã <br />' + CHAR(13) + CHAR(10) + x.Detail,
       @Now, @SystemUser, 0
FROM (
    SELECT c.Id, N'Customer' AS EntityName,
           N'- Code: ''' + ISNULL(c.OldCode, N'') + N''' → ''' + c.NewCode + N''' <br />' + CHAR(13) + CHAR(10)
           + N'- CIF: ''' + ISNULL(c.OldCif, N'') + N''' → ''' + c.NewCode + N''' <br />'
           + ISNULL(CHAR(13) + CHAR(10) + N'- CustomerType: ''' + ISNULL(t.OldType, N'') + N''' → ''BUSINESS'' (Industry: ''' + ISNULL(t.NewIndustry, N'') + N''') <br />', N'') AS Detail
    FROM @CustomerMap AS c LEFT JOIN @TypeMap AS t ON t.Id = c.Id
    UNION ALL
    SELECT t.Id, N'Customer',
           N'- CustomerType: ''' + ISNULL(t.OldType, N'') + N''' → ''BUSINESS'' (Industry: ''' + ISNULL(t.NewIndustry, N'') + N''') <br />'
    FROM @TypeMap AS t WHERE NOT EXISTS (SELECT 1 FROM @CustomerMap AS c WHERE c.Id = t.Id)
    UNION ALL
    SELECT s.Id, N'Supplier', N'- Code: ''' + ISNULL(s.OldCode, N'') + N''' → ''' + s.NewCode + N''' <br />'
    FROM @SupplierMap AS s
) AS x;

COMMIT TRANSACTION;
PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì.';
