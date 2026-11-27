/*
  RETAIL-1 – [DEMO] 12 sản phẩm bán lẻ lấy từ bản nháp trang khách (app-delicare-client/src/assets/portal/portal-data.json, PROPOSED – chờ anh duyệt giá / món).
  CHỈ chạy trên DB dev / máy thử (KHÔNG có trong deploy/db/scripts.order – production nhập sản phẩm thật ở màn Bán lẻ › Sản phẩm bán lẻ).
  Mô tả gắn [DEMO]. Chỉ THÊM món chưa có (theo Name, chưa xóa); mã SPL-NNNNN tiếp nối mã lớn nhất đang có. Không DELETE / UPDATE. Chạy lại = 0 việc.
    sqlcmd -S <server> -d <db> -E -b -I -f 65001 -i DB/seed/RetailProduct_demo_seed.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME2(0) = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Want TABLE (Seq INT IDENTITY(1, 1), Name NVARCHAR(255), CategoryCode NVARCHAR(50), MealTypes NVARCHAR(100), Unit NVARCHAR(50), Price DECIMAL(18, 2),
    Description NVARCHAR(1000), Tags NVARCHAR(255), Icon NVARCHAR(50), SortOrder INT);
INSERT INTO @Want (Name, CategoryCode, MealTypes, Unit, Price, Description, Tags, Icon, SortOrder) VALUES
    (N'Suất cơm trưa tiêu chuẩn', N'com-van-phong', N'Trưa', N'suất', 40000, N'[DEMO] 2 món mặn theo thực đơn ngày, 1 món rau, canh, cơm trắng.', N'Bán chạy', N'lunch_dining', 10),
    (N'Suất cơm trưa đặc biệt', N'com-van-phong', N'Trưa', N'suất', 55000, N'[DEMO] Thêm món chính đặc biệt trong tuần, rau, canh, tráng miệng.', N'Mới', N'set_meal', 20),
    (N'Suất cơm tối', N'com-van-phong', N'Tối', N'suất', 40000, N'[DEMO] Món mặn, rau xào, canh nóng, cơm trắng – giao trước 18:00.', NULL, N'dinner_dining', 30),
    (N'Xôi – bánh mì buổi sáng', N'an-sang', N'Sáng', N'suất', 20000, N'[DEMO] Chọn 1 trong 3 món sáng của ngày, kèm sữa đậu nành.', N'Bán chạy', N'bakery_dining', 40),
    (N'Phở / bún nóng', N'an-sang', N'Sáng', N'bát', 30000, N'[DEMO] Nước dùng ninh xương, đóng hộp giữ nhiệt riêng phần nước.', NULL, N'ramen_dining', 50),
    (N'Combo văn phòng 5 suất', N'combo', N'Trưa', N'combo', 190000, N'[DEMO] 5 suất cơm trưa tiêu chuẩn, miễn phí giao trong khu vực.', N'Tiết kiệm', N'groups', 60),
    (N'Combo văn phòng 10 suất', N'combo', N'Trưa', N'combo', 370000, N'[DEMO] 10 suất cơm trưa tiêu chuẩn kèm tráng miệng.', N'Tiết kiệm', N'diversity_3', 70),
    (N'Combo gia đình 4 người', N'combo', N'Tối', N'combo', 220000, N'[DEMO] 3 món mặn, 1 rau, 1 canh, cơm cho 4 người – giao bữa tối.', NULL, N'family_restroom', 80),
    (N'Suất ăn ít tinh bột', N'dinh-duong', N'Trưa;Tối', N'suất', 60000, N'[DEMO] Ức gà / cá áp chảo, rau củ hấp, cơm gạo lứt phần nhỏ.', N'Lành mạnh', N'spa', 90),
    (N'Suất chay thanh đạm', N'dinh-duong', N'Trưa;Tối', N'suất', 45000, N'[DEMO] Đậu, nấm, rau củ theo mùa, canh rau, cơm trắng.', NULL, N'eco', 100),
    (N'Tráng miệng trong ngày', N'trang-mieng', N'Sáng;Trưa;Tối', N'phần', 12000, N'[DEMO] Hoa quả theo mùa hoặc chè / sữa chua tự làm.', NULL, N'icecream', 110),
    (N'Nước ép – sữa hạt', N'trang-mieng', N'Sáng;Trưa;Tối', N'chai', 18000, N'[DEMO] Chai 330 ml, ép trong ngày, không thêm đường.', NULL, N'local_drink', 120);

BEGIN TRANSACTION;
DECLARE @MaxNo INT = ISNULL((SELECT MAX(TRY_CAST(SUBSTRING(Code, 5, 10) AS INT)) FROM dbo.RetailProduct WITH (UPDLOCK, HOLDLOCK) WHERE Code LIKE N'SPL-%'), 0);
;WITH NewRows AS (
    SELECT w.*, ROW_NUMBER() OVER (ORDER BY w.Seq) AS Rn
    FROM @Want AS w
    WHERE NOT EXISTS (SELECT 1 FROM dbo.RetailProduct AS p WHERE p.Name = w.Name AND p.IsDeleted = 0)
)
INSERT INTO dbo.RetailProduct (Id, Code, Name, CategoryCode, MealTypes, Unit, Price, Description, Tags, Icon, ImageUrl, SortOrder, Status, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), CONCAT(N'SPL-', RIGHT(CONCAT(N'00000', @MaxNo + Rn), 5)), Name, CategoryCode, MealTypes, Unit, Price, Description, Tags, Icon, NULL, SortOrder, N'Active', @Now, @SystemUser, 0
FROM NewRows;
DECLARE @Added INT = @@ROWCOUNT;
COMMIT TRANSACTION;
SELECT @Added AS N'Sản phẩm [DEMO] thêm';
