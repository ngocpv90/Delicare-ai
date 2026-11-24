/*
  HR-ACC-1.2 (D-82 d – anh chốt 2026-11-23): gắn Employee.PersonId cho dữ liệu cũ theo 3 cách đoán cũ (D-55 b / HrRules.ResolveEmployeeAsync):
    (1) liên kết bot: UserExternalIdentity (chưa thu hồi) có EmployeeId = nhân viên và UserId → User.PersonId;
    (2) tên đăng nhập = mã nhân viên (Employee.Code hoặc EmployeeCode cũ) → User.PersonId;
    (3) số điện thoại: Person.PhoneNumber = Employee.Phone sau khi chuẩn hóa như PhoneMatcher (+84 / 0 / 0084, ≥ 10 số).
  Gom MỌI ứng viên Person của 3 cách cho từng nhân viên CHƯA có PersonId:
    - KHỚP CHẮC   = đúng 1 Person, Person đó chưa gắn nhân viên khác và không là ứng viên chắc của nhân viên khác → điền PersonId;
    - NHIỀU ỨNG VIÊN = ≥ 2 Person (các cách cho kết quả khác nhau / nhiều người trùng SĐT) hoặc Person đã / sẽ gắn nhân viên khác → KHÔNG điền;
    - KHÔNG KHỚP  = không có ứng viên (chưa có tài khoản / tài khoản chưa có Person) → KHÔNG điền. Person chỉ tạo khi cấp tài khoản (D-82 d).
  Nhóm 2 và 3: NS xử lý tay ở màn Nhân viên › tab Đối chiếu (API Employee/LinkPerson). In báo cáo 3 nhóm + danh sách.
  Không tạo / sửa bảng, cột; không DELETE. Ghi AuditChange (người sửa = tài khoản "system"). Chạy lại lần 2: 0 việc (chỉ xét nhân viên còn PersonId NULL).

  CẦN migration DB/migrations/20261123_02_hr_acc_person_link.sql trước khi chạy THẬT. DryRun chạy được cả khi chưa có cột (coi mọi PersonId = NULL).
  Bắt buộc truyền -v DryRun=1 (chạy thử, in báo cáo) hoặc -v DryRun=0 (chạy thật).
  THỨ TỰ (chỉ sau khi backup): migration → deploy API HR-ACC-1 → script này DryRun=1 → DryRun=0 → NS xử lý tab Đối chiếu.
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/D82_link_employee_person.sql -W -s "|"
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/D82_link_employee_person.sql -W -s "|"
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
-- FOR XML / chỉ mục lọc UX_Employee_PersonId cần các SET này (sqlcmd mặc định QUOTED_IDENTIFIER OFF).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ANSI_PADDING ON;

DECLARE @DryRun BIT = $(DryRun);
DECLARE @HasCol BIT = CASE WHEN COL_LENGTH(N'dbo.Employee', N'PersonId') IS NULL THEN 0 ELSE 1 END;
IF @DryRun = 0 AND @HasCol = 0
    THROW 50201, N'Chưa có cột Employee.PersonId – chạy migration 20261123_02_hr_acc_person_link.sql trước.', 1;
DECLARE @By UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @By IS NULL
    THROW 50202, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME()); -- giờ Việt Nam như ứng dụng

-- Bảng tạm của lần chạy trước trong cùng phiên (chạy lại ngay) – chỉ bảng #tạm.
DROP TABLE IF EXISTS #Res, #Cand, #Person, #Emp;
-- Nhân viên (đọc PersonId bằng SQL động: DryRun trước migration vẫn chạy được).
CREATE TABLE #Emp (Id UNIQUEIDENTIFIER PRIMARY KEY, Code NVARCHAR(255) NULL, OldCode NVARCHAR(255) NULL, FullName NVARCHAR(500) NULL,
                   Phone NVARCHAR(100) NULL, Digits NVARCHAR(100) NULL, Status NVARCHAR(50) NULL, PersonId UNIQUEIDENTIFIER NULL);
IF @HasCol = 1
    EXEC (N'INSERT INTO #Emp (Id, Code, OldCode, FullName, Phone, Status, PersonId)
            SELECT Id, LTRIM(RTRIM(Code)), LTRIM(RTRIM(EmployeeCode)), FullName, Phone, Status, PersonId FROM dbo.Employee WHERE IsDeleted = 0;');
ELSE
    INSERT INTO #Emp (Id, Code, OldCode, FullName, Phone, Status, PersonId)
    SELECT Id, LTRIM(RTRIM(Code)), LTRIM(RTRIM(EmployeeCode)), FullName, Phone, Status, NULL FROM dbo.Employee WHERE IsDeleted = 0;
-- SĐT chuẩn hóa như PhoneMatcher.Normalize (C#): chỉ giữ chữ số; 0084… → 84…; 0… → 84…; 9 số không đầu 84 → 84 + số; < 10 số = bỏ.
UPDATE #Emp SET Digits = REPLACE(TRANSLATE(ISNULL(Phone, N''), N'.-+()/', N'      '), N' ', N'');
UPDATE #Emp SET Digits = CASE WHEN LEFT(Digits, 4) = N'0084' THEN SUBSTRING(Digits, 3, 100) WHEN LEFT(Digits, 1) = N'0' THEN N'84' + SUBSTRING(Digits, 2, 100)
                              WHEN LEN(Digits) = 9 AND LEFT(Digits, 2) <> N'84' THEN N'84' + Digits ELSE Digits END;

CREATE TABLE #Person (Id UNIQUEIDENTIFIER PRIMARY KEY, FullName NVARCHAR(500) NULL, Digits NVARCHAR(100) NULL);
INSERT INTO #Person (Id, FullName, Digits)
SELECT Id, FullName, REPLACE(TRANSLATE(ISNULL(PhoneNumber, N''), N'.-+()/', N'      '), N' ', N'') FROM dbo.Person WHERE IsDeleted = 0;
UPDATE #Person SET Digits = CASE WHEN LEFT(Digits, 4) = N'0084' THEN SUBSTRING(Digits, 3, 100) WHEN LEFT(Digits, 1) = N'0' THEN N'84' + SUBSTRING(Digits, 2, 100)
                                 WHEN LEN(Digits) = 9 AND LEFT(Digits, 2) <> N'84' THEN N'84' + Digits ELSE Digits END;

-- Ứng viên (nhân viên chưa có PersonId).
CREATE TABLE #Cand (EmployeeId UNIQUEIDENTIFIER NOT NULL, PersonId UNIQUEIDENTIFIER NOT NULL, Source NVARCHAR(20) NOT NULL);
INSERT INTO #Cand (EmployeeId, PersonId, Source)
SELECT DISTINCT e.Id, u.PersonId, N'BOT'
FROM #Emp e
JOIN dbo.UserExternalIdentity x ON x.EmployeeId = e.Id AND x.IsDeleted = 0 AND x.UserId IS NOT NULL
     AND (x.IdentityStatus IS NULL OR x.IdentityStatus IN (N'Active', N'PendingCode'))
JOIN dbo.[User] u ON u.Id = x.UserId AND u.IsDeleted = 0 AND u.PersonId IS NOT NULL
JOIN #Person p ON p.Id = u.PersonId
WHERE e.PersonId IS NULL;
INSERT INTO #Cand (EmployeeId, PersonId, Source)
SELECT DISTINCT e.Id, u.PersonId, N'USERNAME'
FROM #Emp e
JOIN dbo.[User] u ON u.IsDeleted = 0 AND u.PersonId IS NOT NULL AND LTRIM(RTRIM(u.UserName)) <> N''
     AND (LTRIM(RTRIM(u.UserName)) = e.Code OR LTRIM(RTRIM(u.UserName)) = e.OldCode)
JOIN #Person p ON p.Id = u.PersonId
WHERE e.PersonId IS NULL;
INSERT INTO #Cand (EmployeeId, PersonId, Source)
SELECT DISTINCT e.Id, p.Id, N'PHONE'
FROM #Emp e
JOIN #Person p ON p.Digits = e.Digits
WHERE e.PersonId IS NULL AND LEN(e.Digits) >= 10;

CREATE TABLE #Res (EmployeeId UNIQUEIDENTIFIER PRIMARY KEY, Grp NVARCHAR(20) NOT NULL, PersonId UNIQUEIDENTIFIER NULL, Note NVARCHAR(400) NULL);
INSERT INTO #Res (EmployeeId, Grp, PersonId, Note)
SELECT e.Id,
       CASE WHEN c.N IS NULL THEN N'NONE' WHEN c.N = 1 THEN N'SURE' ELSE N'MULTI' END,
       CASE WHEN c.N = 1 THEN c.OnlyPerson END,
       CASE WHEN c.N IS NULL THEN N'Không có tài khoản / Person nào khớp' WHEN c.N > 1 THEN N'Các cách đoán cho ' + CAST(c.N AS NVARCHAR(10)) + N' người khác nhau' END
FROM #Emp e
LEFT JOIN (SELECT EmployeeId, COUNT(DISTINCT PersonId) AS N,
                  CAST(MIN(CAST(PersonId AS NVARCHAR(36))) AS UNIQUEIDENTIFIER) AS OnlyPerson
           FROM #Cand GROUP BY EmployeeId) c ON c.EmployeeId = e.Id
WHERE e.PersonId IS NULL;
-- Person đã gắn nhân viên khác, hoặc là ứng viên chắc của ≥ 2 nhân viên → hạ xuống "nhiều ứng viên".
UPDATE r SET Grp = N'MULTI', Note = N'Người này đã gắn nhân viên khác', PersonId = NULL
FROM #Res r WHERE r.Grp = N'SURE' AND EXISTS (SELECT 1 FROM #Emp o WHERE o.PersonId = r.PersonId);
UPDATE r SET Grp = N'MULTI', Note = N'Cùng 1 người khớp nhiều nhân viên (trùng SĐT / mã)', PersonId = NULL
FROM #Res r WHERE r.Grp = N'SURE' AND (SELECT COUNT(*) FROM #Res o WHERE o.Grp = N'SURE' AND o.PersonId = r.PersonId) > 1;

-- Báo cáo.
SELECT (SELECT COUNT(*) FROM #Emp) AS [Nhân viên], (SELECT COUNT(*) FROM #Emp WHERE PersonId IS NOT NULL) AS [Đã gắn từ trước],
       (SELECT COUNT(*) FROM #Res WHERE Grp = N'SURE') AS [Khớp chắc], (SELECT COUNT(*) FROM #Res WHERE Grp = N'MULTI') AS [Nhiều ứng viên],
       (SELECT COUNT(*) FROM #Res WHERE Grp = N'NONE') AS [Không khớp];
SELECT CASE r.Grp WHEN N'SURE' THEN N'Khớp chắc' WHEN N'MULTI' THEN N'Nhiều ứng viên' ELSE N'Không khớp' END AS [Nhóm],
       ISNULL(e.Code, e.OldCode) AS [Mã NV], e.FullName AS [Họ tên], e.Status AS [Trạng thái], e.Phone AS [SĐT],
       ISNULL(p.FullName, N'') AS [Người được gắn],
       ISNULL(STUFF((SELECT N', ' + c.Source + N':' + ISNULL(pp.FullName, LOWER(CAST(c.PersonId AS NVARCHAR(36))))
                     FROM #Cand c LEFT JOIN #Person pp ON pp.Id = c.PersonId WHERE c.EmployeeId = r.EmployeeId FOR XML PATH(N''), TYPE).value(N'.', N'NVARCHAR(MAX)'), 1, 2, N''), N'') AS [Ứng viên],
       ISNULL(r.Note, N'') AS [Ghi chú]
FROM #Res r JOIN #Emp e ON e.Id = r.EmployeeId LEFT JOIN #Person p ON p.Id = r.PersonId
ORDER BY CASE r.Grp WHEN N'SURE' THEN 1 WHEN N'MULTI' THEN 2 ELSE 3 END, ISNULL(e.Code, e.OldCode);

IF @DryRun = 1
BEGIN
    PRINT N'Chạy thử (DryRun=1): không ghi gì.' + CASE WHEN @HasCol = 0 THEN N' DB chưa có cột Employee.PersonId (chưa chạy migration) – báo cáo coi mọi PersonId = NULL.' ELSE N'' END
        + N' Chạy thật: -v DryRun=0 (sau backup, sau migration + deploy API HR-ACC-1).';
    RETURN;
END

BEGIN TRANSACTION;
DECLARE @Linked INT;
EXEC sp_executesql N'UPDATE e SET e.PersonId = r.PersonId, e.ModifiedDate = @Now, e.ModifiedBy = @By
                       FROM dbo.Employee e JOIN #Res r ON r.EmployeeId = e.Id
                       WHERE r.Grp = N''SURE'' AND e.PersonId IS NULL;
                     SET @Linked = @@ROWCOUNT;',
     N'@Now DATETIME, @By UNIQUEIDENTIFIER, @Linked INT OUTPUT', @Now = @Now, @By = @By, @Linked = @Linked OUTPUT;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), r.EmployeeId, N'Modified', N'Employee',
       N'- Actor: SYSTEM:' + LOWER(CAST(@By AS NVARCHAR(36))) + N' | HR-ACC-1.2 gắn nhân viên với con người (D-82 d – khớp chắc: '
       + STUFF((SELECT N', ' + c.Source FROM #Cand c WHERE c.EmployeeId = r.EmployeeId FOR XML PATH(N''), TYPE).value(N'.', N'NVARCHAR(MAX)'), 1, 2, N'') + N') <br />' + CHAR(13) + CHAR(10)
       + N'- PersonId: '''' → ''' + LOWER(CAST(r.PersonId AS NVARCHAR(36))) + N''' <br />',
       @Now, @By, 0
FROM #Res r WHERE r.Grp = N'SURE';
IF @Linked <> (SELECT COUNT(*) FROM #Res WHERE Grp = N'SURE')
    THROW 50203, N'Số nhân viên được gắn khác số khớp chắc – hủy.', 1;
COMMIT TRANSACTION;
PRINT N'Đã gắn ' + CAST(@Linked AS NVARCHAR(10)) + N' nhân viên (khớp chắc). Chạy lại lần 2 sẽ không đổi gì. Nhóm nhiều ứng viên / không khớp: NS xử lý ở màn Nhân viên › Đối chiếu.';
