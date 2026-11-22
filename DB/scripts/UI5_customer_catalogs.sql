/*
  UI-5.3 (D-63 r) – script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE).
  Chuẩn hóa 4 trường danh mục của dbo.Customer về MÃ (giao diện hiển thị tiếng Việt, máy chủ chỉ nhận mã trong danh mục – CustomerCatalogs.cs):
    Industry    : HOSPITAL · FACTORY · CONSTRUCTION · SCHOOL · OFFICE · OTHER
    CompanySize : LT200 · 200_500 · 500_1000 · GT1000   (suất / ngày)
    Gender      : MALE · FEMALE · OTHER
    Source      : REFERRAL · TENDER · SELF · OTHER
  Quy tắc: đúng mã (khác hoa thường / dấu cách) → mã chuẩn; chữ tiếng Việt / tiếng Anh thường gặp → mã gần nhất;
  CompanySize là số → xếp khoảng; không khớp → OTHER (CompanySize: để trống) và GIỮ chữ cũ trong Ghi chú ("[UI-5] Ngành cũ: …").
  Mỗi khách đổi có 1 dòng lịch sử AuditChange (Modified, người thao tác = tài khoản "system").
  Giá trị đã là mã đúng thì bỏ qua → chạy lại lần 2 không đổi gì (idempotent).

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật).
  CÁCH CHẠY (chỉ sau khi backup):
    Chạy thử (in bảng đối chiếu, KHÔNG ghi):
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI5_customer_catalogs.sql -s "," -W -o ui5-preview.csv
    Chạy thật:
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/UI5_customer_catalogs.sql -s "," -W -o ui5-applied.csv
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun BIT = $(DryRun);

DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME()); -- giờ Việt Nam như ứng dụng

BEGIN TRANSACTION;

-- Field: tên cột · OldValue: giá trị đang lưu · NewValue: mã mới (NULL = để trống) · KeepNote = 1 → ghi chữ cũ vào Ghi chú.
DECLARE @Map TABLE (Id UNIQUEIDENTIFIER, Code NVARCHAR(255), Field NVARCHAR(20), OldValue NVARCHAR(100), NewValue NVARCHAR(50), KeepNote BIT, PRIMARY KEY (Id, Field));

-- ---------------------------------------------------------------- Ngành
INSERT INTO @Map (Id, Code, Field, OldValue, NewValue, KeepNote)
SELECT x.Id, x.Code, N'Industry', x.Industry, ISNULL(x.Hit, N'OTHER'), CASE WHEN x.Hit IS NULL THEN 1 ELSE 0 END
FROM (
    SELECT c.Id, c.Code, c.Industry, v.Val,
           CASE
               WHEN v.Val IN (N'HOSPITAL', N'BENH_VIEN', N'BV') OR v.Val LIKE N'%BỆNH VIỆN%' OR v.Val LIKE N'%BENH VIEN%' OR v.Val LIKE N'%Y TẾ%' THEN N'HOSPITAL'
               WHEN v.Val IN (N'FACTORY', N'KCN') OR v.Val LIKE N'%NHÀ MÁY%' OR v.Val LIKE N'%NHA MAY%' OR v.Val LIKE N'%XƯỞNG%' OR v.Val LIKE N'%KHU CÔNG NGHIỆP%' OR v.Val LIKE N'%SẢN XUẤT%' THEN N'FACTORY'
               WHEN v.Val IN (N'CONSTRUCTION') OR v.Val LIKE N'%CÔNG TRƯỜNG%' OR v.Val LIKE N'%CONG TRUONG%' OR v.Val LIKE N'%XÂY DỰNG%' THEN N'CONSTRUCTION'
               WHEN v.Val IN (N'SCHOOL') OR v.Val LIKE N'%TRƯỜNG%' OR v.Val LIKE N'%TRUONG HOC%' OR v.Val LIKE N'%MẦM NON%' OR v.Val LIKE N'%HỌC%' THEN N'SCHOOL'
               WHEN v.Val IN (N'OFFICE') OR v.Val LIKE N'%VĂN PHÒNG%' OR v.Val LIKE N'%VAN PHONG%' OR v.Val LIKE N'%TÒA NHÀ%' THEN N'OFFICE'
               WHEN v.Val IN (N'OTHER', N'KHÁC', N'KHAC') THEN N'OTHER'
           END AS Hit
    FROM dbo.Customer AS c
    CROSS APPLY (SELECT UPPER(LTRIM(RTRIM(c.Industry))) AS Val) AS v
    WHERE NULLIF(LTRIM(RTRIM(c.Industry)), N'') IS NOT NULL
) AS x
WHERE x.Industry COLLATE Latin1_General_BIN NOT IN (N'HOSPITAL', N'FACTORY', N'CONSTRUCTION', N'SCHOOL', N'OFFICE', N'OTHER');

-- ---------------------------------------------------------------- Quy mô (suất / ngày)
INSERT INTO @Map (Id, Code, Field, OldValue, NewValue, KeepNote)
SELECT x.Id, x.Code, N'CompanySize', x.CompanySize, x.Hit, CASE WHEN x.Hit IS NULL THEN 1 ELSE 0 END
FROM (
    SELECT c.Id, c.Code, c.CompanySize,
           CASE
               WHEN v.Val IN (N'LT200', N'200_500', N'500_1000', N'GT1000') THEN v.Val
               WHEN n.Num IS NOT NULL AND n.Num < 200 THEN N'LT200'
               WHEN n.Num IS NOT NULL AND n.Num <= 500 THEN N'200_500'
               WHEN n.Num IS NOT NULL AND n.Num <= 1000 THEN N'500_1000'
               WHEN n.Num IS NOT NULL THEN N'GT1000'
               WHEN v.Val LIKE N'<%200%' OR v.Val LIKE N'%DƯỚI 200%' THEN N'LT200'
               WHEN v.Val LIKE N'%200%500%' THEN N'200_500'
               WHEN v.Val LIKE N'%500%1000%' OR v.Val LIKE N'%500%1.000%' THEN N'500_1000'
               WHEN v.Val LIKE N'>%1000%' OR v.Val LIKE N'%TRÊN 1000%' OR v.Val LIKE N'%TRÊN 1.000%' THEN N'GT1000'
           END AS Hit
    FROM dbo.Customer AS c
    CROSS APPLY (SELECT UPPER(LTRIM(RTRIM(c.CompanySize))) AS Val) AS v
    CROSS APPLY (SELECT TRY_CAST(REPLACE(REPLACE(REPLACE(v.Val, N'.', N''), N',', N''), N' ', N'') AS INT) AS Num) AS n
    WHERE NULLIF(LTRIM(RTRIM(c.CompanySize)), N'') IS NOT NULL
) AS x
WHERE x.CompanySize COLLATE Latin1_General_BIN NOT IN (N'LT200', N'200_500', N'500_1000', N'GT1000');

-- ---------------------------------------------------------------- Giới tính
INSERT INTO @Map (Id, Code, Field, OldValue, NewValue, KeepNote)
SELECT x.Id, x.Code, N'Gender', x.Gender, ISNULL(x.Hit, N'OTHER'), CASE WHEN x.Hit IS NULL THEN 1 ELSE 0 END
FROM (
    SELECT c.Id, c.Code, c.Gender,
           CASE
               WHEN v.Val IN (N'MALE', N'M', N'NAM', N'1') THEN N'MALE'
               WHEN v.Val IN (N'FEMALE', N'F', N'NỮ', N'NU', N'0') THEN N'FEMALE'
               WHEN v.Val IN (N'OTHER', N'KHÁC', N'KHAC') THEN N'OTHER'
           END AS Hit
    FROM dbo.Customer AS c
    CROSS APPLY (SELECT UPPER(LTRIM(RTRIM(c.Gender))) AS Val) AS v
    WHERE NULLIF(LTRIM(RTRIM(c.Gender)), N'') IS NOT NULL
) AS x
WHERE x.Gender COLLATE Latin1_General_BIN NOT IN (N'MALE', N'FEMALE', N'OTHER');

-- ---------------------------------------------------------------- Nguồn
INSERT INTO @Map (Id, Code, Field, OldValue, NewValue, KeepNote)
SELECT x.Id, x.Code, N'Source', x.Source, ISNULL(x.Hit, N'OTHER'), CASE WHEN x.Hit IS NULL THEN 1 ELSE 0 END
FROM (
    SELECT c.Id, c.Code, c.Source,
           CASE
               WHEN v.Val IN (N'REFERRAL') OR v.Val LIKE N'%GIỚI THIỆU%' OR v.Val LIKE N'%GIOI THIEU%' THEN N'REFERRAL'
               WHEN v.Val IN (N'TENDER') OR v.Val LIKE N'%ĐẤU THẦU%' OR v.Val LIKE N'%DAU THAU%' OR v.Val LIKE N'%THẦU%' THEN N'TENDER'
               WHEN v.Val IN (N'SELF') OR v.Val LIKE N'%TỰ TÌM%' OR v.Val LIKE N'%TU TIM%' THEN N'SELF'
               WHEN v.Val IN (N'OTHER', N'KHÁC', N'KHAC') THEN N'OTHER'
           END AS Hit
    FROM dbo.Customer AS c
    CROSS APPLY (SELECT UPPER(LTRIM(RTRIM(c.Source))) AS Val) AS v
    WHERE NULLIF(LTRIM(RTRIM(c.Source)), N'') IS NOT NULL
) AS x
WHERE x.Source COLLATE Latin1_General_BIN NOT IN (N'REFERRAL', N'TENDER', N'SELF', N'OTHER');

-- ---------------------------------------------------------------- Bảng đối chiếu (in cả khi chạy thử)
SELECT m.Code AS [Mã KH], m.Field AS [Trường], m.OldValue AS [Giá trị cũ], ISNULL(m.NewValue, N'(để trống)') AS [Mã mới],
       CASE WHEN m.KeepNote = 1 THEN N'Ghi chữ cũ vào Ghi chú' ELSE N'' END AS [Ghi chú]
FROM @Map AS m
ORDER BY m.Code, m.Field;

IF @DryRun = 1
BEGIN
    ROLLBACK TRANSACTION;
    PRINT N'Chạy thử: không ghi gì. Chạy thật: thêm -v DryRun=0 (sau khi backup).';
    RETURN;
END

-- ---------------------------------------------------------------- Ghi
DECLARE @Label TABLE (Field NVARCHAR(20) PRIMARY KEY, Label NVARCHAR(30));
INSERT INTO @Label VALUES (N'Industry', N'Ngành'), (N'CompanySize', N'Quy mô'), (N'Gender', N'Giới tính'), (N'Source', N'Nguồn');

UPDATE c SET
    c.Industry    = ISNULL((SELECT m.NewValue FROM @Map AS m WHERE m.Id = c.Id AND m.Field = N'Industry'), c.Industry),
    c.CompanySize = CASE WHEN EXISTS (SELECT 1 FROM @Map AS m WHERE m.Id = c.Id AND m.Field = N'CompanySize')
                         THEN (SELECT m.NewValue FROM @Map AS m WHERE m.Id = c.Id AND m.Field = N'CompanySize') ELSE c.CompanySize END,
    c.Gender      = ISNULL((SELECT m.NewValue FROM @Map AS m WHERE m.Id = c.Id AND m.Field = N'Gender'), c.Gender),
    c.Source      = ISNULL((SELECT m.NewValue FROM @Map AS m WHERE m.Id = c.Id AND m.Field = N'Source'), c.Source),
    c.Note        = CASE WHEN n.Extra IS NULL THEN c.Note ELSE ISNULL(NULLIF(c.Note, N'') + CHAR(13) + CHAR(10), N'') + n.Extra END,
    c.ModifiedDate = @Now, c.ModifiedBy = @SystemUser
FROM dbo.Customer AS c
OUTER APPLY (
    SELECT STRING_AGG(N'[UI-5] ' + l.Label + N' cũ: ' + m.OldValue, N'; ') AS Extra
    FROM @Map AS m JOIN @Label AS l ON l.Field = m.Field
    WHERE m.Id = c.Id AND m.KeepNote = 1
) AS n
WHERE c.Id IN (SELECT Id FROM @Map);

-- Lịch sử (AuditChange chỉ ghi thêm) – cùng định dạng ứng dụng ghi.
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), g.Id, N'Modified', N'Customer',
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | UI-5.3 (D-63 r) chuẩn hóa danh mục <br />' + CHAR(13) + CHAR(10)
       + STRING_AGG(N'- ' + m.Field + N': ''' + ISNULL(m.OldValue, N'') + N''' → ''' + ISNULL(m.NewValue, N'') + N''' <br />', CHAR(13) + CHAR(10)),
       @Now, @SystemUser, 0
FROM (SELECT DISTINCT Id FROM @Map) AS g
JOIN @Map AS m ON m.Id = g.Id
GROUP BY g.Id;

COMMIT TRANSACTION;
PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì.';
