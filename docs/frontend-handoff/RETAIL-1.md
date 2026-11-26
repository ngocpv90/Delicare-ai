# RETAIL-1 – Module Bán lẻ (khách lẻ đặt trên web) – đặc tả cho backend

> Trạng thái: **PROPOSED – BA/PO CONFIRM**. Anh chốt 2026-11-26: tạo **bảng mới riêng** cho khách lẻ, **tách hẳn** đơn doanh nghiệp (`Order` / hợp đồng giữ nguyên).
> Frontend (trang khách lẻ `#/` + màn nội bộ `#/sal-retail-order`, `#/sal-retail-product`) **đã làm**, gọi đúng các API dưới đây; khi API chưa có:
> trang khách lẻ tự quay về đặt qua hotline / Zalo, màn nội bộ báo "Tính năng chưa được nâng cấp…". Liên quan: GAP-POS-01, GAP-PORTAL-01 (tài khoản khách – chưa làm).

## 1. Luồng

```
Khách (web, không đăng nhập) ─ chọn món → giỏ → Gửi đơn ─▶ RetailPortal/SubmitOrder ─▶ RetailOrder (Mới, kênh WEB)
                                                                         └─▶ tin Telegram nhóm Vận hành (bot sẵn có)
Nhân viên bán lẻ (#/sal-retail-order) ─ gọi khách xác nhận → Xác nhận → Đang giao → Hoàn thành (đã thu tiền)
                                       └─ Hủy (kèm lý do) khi khách hủy / không liên lạc được
Nhân viên nhận đơn qua điện thoại / Zalo → nút "Lập đơn" trên cùng màn (kênh PHONE / ZALO)
```

Đơn doanh nghiệp (`Order`, `#/crm-order`, `#/crm-order-week`) **không đổi**, không hiện đơn lẻ. Báo cáo / MRP chưa tính đơn lẻ (đợt sau – mục 8).

## 2. Bảng mới (migration `DB/migrations/YYYYMMDD_NN_retail_order.sql` – cần anh duyệt; CodeGen `--only RetailProduct,RetailOrder,RetailOrderLine`)

Khuôn cột chuẩn như `SupplierIssue` (Id GUID, CreatedDate / CreatedBy / ModifiedDate / ModifiedBy / IsDeleted), giờ Việt Nam, không FK vật lý (kiểm ở Service).

```sql
-- delicare-migration: normal
IF OBJECT_ID(N'dbo.RetailProduct', N'U') IS NULL
CREATE TABLE dbo.RetailProduct (
    Id            uniqueidentifier NOT NULL CONSTRAINT PK_RetailProduct PRIMARY KEY,
    Code          nvarchar(50)     NOT NULL,          -- G1 SPL-NNNNN (máy chủ sinh)
    Name          nvarchar(255)    NOT NULL,
    CategoryCode  nvarchar(50)     NOT NULL,          -- ReferenceLists:RetailCategory (appsettings)
    Unit          nvarchar(50)     NOT NULL,          -- suất / combo / chai…
    Price         decimal(18, 2)   NOT NULL,          -- giá bán lẻ đã gồm VAT
    Description   nvarchar(1000)   NULL,
    Tags          nvarchar(255)    NULL,              -- "Bán chạy;Mới" – nhãn hiển thị
    Icon          nvarchar(50)     NULL,              -- tên biểu tượng Material Icons (tạm thay ảnh)
    ImageUrl      nvarchar(500)    NULL,              -- ảnh món (DocumentStorage) – để sau
    SortOrder     int              NOT NULL CONSTRAINT DF_RetailProduct_SortOrder DEFAULT (0),
    Status        nvarchar(30)     NOT NULL,          -- Active / Inactive (Đang bán / Ngừng bán)
    CreatedDate datetime2(0) NOT NULL, CreatedBy uniqueidentifier NULL,
    ModifiedDate datetime2(0) NULL,    ModifiedBy uniqueidentifier NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_RetailProduct_IsDeleted DEFAULT (0));

IF OBJECT_ID(N'dbo.RetailOrder', N'U') IS NULL
CREATE TABLE dbo.RetailOrder (
    Id            uniqueidentifier NOT NULL CONSTRAINT PK_RetailOrder PRIMARY KEY,
    OrderNumber   nvarchar(50)     NOT NULL,          -- G1 BL-YYMMDD-NNNN
    Channel       nvarchar(20)     NOT NULL,          -- WEB / PHONE / ZALO
    CustomerName  nvarchar(200)    NOT NULL,
    PhoneNumber   nvarchar(20)     NOT NULL,          -- đã chuẩn hóa 0xxxxxxxxx
    Address       nvarchar(500)    NOT NULL,
    DeliveryDate  date             NOT NULL,
    MealType      nvarchar(50)     NOT NULL,          -- mã ReferenceLists:MealType (Sáng / Trưa / Tối)
    Note          nvarchar(1000)   NULL,
    OrderStatus   nvarchar(30)     NOT NULL,          -- mục 4
    SubTotal      decimal(18, 2)   NOT NULL,
    ShippingFee   decimal(18, 2)   NOT NULL CONSTRAINT DF_RetailOrder_ShippingFee DEFAULT (0),
    TotalAmount   decimal(18, 2)   NOT NULL,
    PaymentMethod nvarchar(30)     NULL,              -- COD / BANK
    PaymentStatus nvarchar(30)     NOT NULL,          -- UNPAID / PAID
    ConfirmedAt   datetime2(0)     NULL, ConfirmedBy uniqueidentifier NULL,
    CompletedAt   datetime2(0)     NULL,
    CancelledAt   datetime2(0)     NULL, CancelReason nvarchar(500) NULL,
    ClientIp      nvarchar(64)     NULL,              -- chống spam (đơn WEB) – không hiển thị
    CreatedDate datetime2(0) NOT NULL, CreatedBy uniqueidentifier NULL,   -- đơn WEB: CreatedBy = tài khoản system
    ModifiedDate datetime2(0) NULL,    ModifiedBy uniqueidentifier NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_RetailOrder_IsDeleted DEFAULT (0));

IF OBJECT_ID(N'dbo.RetailOrderLine', N'U') IS NULL
CREATE TABLE dbo.RetailOrderLine (
    Id              uniqueidentifier NOT NULL CONSTRAINT PK_RetailOrderLine PRIMARY KEY,
    RetailOrderId   uniqueidentifier NOT NULL,
    RetailProductId uniqueidentifier NOT NULL,
    ProductName     nvarchar(255)    NOT NULL,        -- chụp lại lúc đặt (giá / tên đổi sau không ảnh hưởng đơn cũ)
    Unit            nvarchar(50)     NOT NULL,
    Quantity        decimal(18, 2)   NOT NULL,
    UnitPrice       decimal(18, 2)   NOT NULL,
    LineAmount      decimal(18, 2)   NOT NULL,
    CreatedDate datetime2(0) NOT NULL, CreatedBy uniqueidentifier NULL,
    ModifiedDate datetime2(0) NULL,    ModifiedBy uniqueidentifier NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_RetailOrderLine_IsDeleted DEFAULT (0));

-- index không unique (ghi DECISIONS)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrder_Status_Date')
    CREATE INDEX IX_RetailOrder_Status_Date ON dbo.RetailOrder (OrderStatus, DeliveryDate) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrder_Phone')
    CREATE INDEX IX_RetailOrder_Phone ON dbo.RetailOrder (PhoneNumber) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrderLine_Order')
    CREATE INDEX IX_RetailOrderLine_Order ON dbo.RetailOrderLine (RetailOrderId) WHERE IsDeleted = 0;
```

Kèm theo:
- BUSINESS_RULES §11: đăng ký G1 `BL-YYMMDD-NNNN` (RetailOrder.OrderNumber), `SPL-NNNNN` (RetailProduct.Code); bảng phân nhóm mã (test chặn).
- `AuditChange` tự ghi (DbContext). Sổ append-only: không – đơn đổi trạng thái tại chỗ theo máy trạng thái mục 4.
- Seed `[DEMO]` 12 sản phẩm từ `app-delicare-client/src/assets/portal/portal-data.json` (script seed riêng).
- `ReferenceLists:RetailCategory` trong appsettings: `com-van-phong` Cơm văn phòng, `an-sang` Món ăn sáng, `combo` Combo nhóm, `dinh-duong` Suất dinh dưỡng, `trang-mieng` Tráng miệng & đồ uống.

## 3. Quy tắc (BusinessRules:Retail – appsettings, không hard-code)

| Khóa | Mặc định | Ý nghĩa |
|---|---|---|
| `OrderCutoffHour` | 20 | Ngày nhận sớm nhất: trước giờ chốt → ngày mai; sau → ngày kia. Máy chủ **kiểm lại**, không tin giao diện. |
| `MaxDaysAhead` | 14 | Đặt trước tối đa 14 ngày. |
| `MaxLines` / `MaxQuantityPerLine` | 20 / 999 | Chặn đơn bất thường. |
| `SubmitLimitPerPhone` / `SubmitLimitPerIp` | 3 / 10 trong 60 phút | Chống spam đơn WEB; vượt → 429 + thông báo tiếng Việt. |
| `NotifyChannel` | Telegram Vận hành | Gửi tin đơn mới (số đơn, tên, SĐT, tổng tiền, ngày / bữa, nút mở `#/sal-retail-order?id=`). |

Giá: **máy chủ tính lại** từ `RetailProduct.Price` (bỏ qua giá gửi lên); sản phẩm Ngừng bán / đã xóa → 400 "Sản phẩm … đã ngừng bán".

## 4. Máy trạng thái `RetailOrder.OrderStatus` (thêm vào STATUS_CATALOG + từ điển `UI.STATUS_VI`)

| Mã | Hiển thị | Chuyển được sang | Ai |
|---|---|---|---|
| `New` | Mới | `Confirmed`, `Cancelled` | (đơn WEB tự tạo) |
| `Confirmed` | Đã xác nhận | `Delivering`, `Cancelled` | Bán lẻ – Sửa |
| `Delivering` | Đang giao | `Completed`, `Cancelled` | Bán lẻ – Sửa |
| `Completed` | Hoàn thành | – (PaymentStatus = PAID) | Bán lẻ – Sửa |
| `Cancelled` | Đã hủy | – (bắt buộc lý do) | Bán lẻ – Sửa |

Sửa dòng / phí giao chỉ khi `New` / `Confirmed`. API trả `allowedTransitions` để giao diện vẽ nút (như `SupplierIssue`).

## 5. API (khung cũ – `api/<Controller>/<Action>`, `Result<T>` / `Results<T>`)

### 5.1 Công khai – `RetailPortalController` (`[AllowAnonymous]` cả controller – **bắt buộc**, nếu không JwtInterceptor sẽ đẩy khách sang trang đăng nhập)

**GET `RetailPortal/GetCatalog`** → `Result<{ categories: [{ code, name }], products: [{ id, code, categoryCode, name, unit, price, description, tags: string[], icon, imageUrl }] }>` – chỉ sản phẩm Active, theo SortOrder.

**POST `RetailPortal/SubmitOrder`** – `[FromBody]` (có danh sách dòng):
```json
{ "customerName": "Nguyễn Văn A", "phoneNumber": "0912345678", "address": "Số 1 Phố Huế, Hà Nội",
  "deliveryDate": "2026-11-28", "mealType": "Trưa", "note": "Ít cay", "website": "",
  "lines": [ { "productId": "<guid>", "productCode": "SPL-00001", "quantity": 2 } ] }
```
- `website` = ô bẫy (honeypot) ẩn: có giá trị → trả 200 giả, không ghi.
- Nhận `productId` **hoặc** `productCode` (trang khách lẻ khi đọc danh mục từ JSON dự phòng chỉ có mã).
- Trả `Result<{ id, orderNumber, totalAmount, deliveryDate, mealType }>`; 400 + `responseMessage` tiếng Việt khi sai (SĐT, ngày qua giờ chốt, sản phẩm ngừng bán…); 429 khi vượt giới hạn.

### 5.2 Nội bộ – `RetailOrderController` (BaseController, ScreenMap `RetailOrder → sal-retail-order`)

| Action | Kiểu | Tham số | Trả |
|---|---|---|---|
| `GetList` | GET | `SearchText` (số đơn / tên / SĐT), `OrderStatus`, `Channel`, `FromDate`, `ToDate` (ngày nhận), `Page`, `PageSize` | `Result<{ listItem: [RetailOrderViewModel], totalRecords, statusCounts: { New: n, … }, todayCount, todayAmount }>` |
| `GetDetail` | GET | `id` | `Result<{ order, lines: [...], allowedTransitions: [] }>` |
| `Create` | POST `[FromBody]` | như SubmitOrder + `channel` (PHONE / ZALO), `shippingFee`, `paymentMethod` | `Result<{ id, orderNumber }>` – trạng thái `Confirmed` (nhân viên đã nói chuyện với khách) |
| `UpdateLines` | PUT `[FromBody]` | `id`, `lines`, `shippingFee`, `note`, `address`, `deliveryDate`, `mealType` | chỉ `New` / `Confirmed` |
| `Transition` | POST `[FromForm]` | `Id`, `To`, `Reason` (bắt buộc khi `Cancelled`), `ShippingFee`, `PaymentMethod` (khi `Confirmed`) | `Result<bool>` – `[Permission("sal-retail-order", Edit)]` |
| `Export` | GET | như GetList | Excel danh sách đơn (tên file tiếng Việt) |

`RetailOrderViewModel`: `id, orderNumber, channel, customerName, phoneNumber, address, deliveryDate, mealType, orderStatus, subTotal, shippingFee, totalAmount, paymentMethod, paymentStatus, note, lineSummary` ("Suất cơm trưa tiêu chuẩn × 2; …"), `createdDate, confirmedAt, cancelReason`.

### 5.3 Danh mục – `RetailProductController` (CRUD mẫu `WarehouseController`, ScreenMap `RetailProduct → sal-retail-product`)
`GetAll` (`SearchText`, `CategoryCode`, `Status`, `Page`, `PageSize`) · `Insert` / `Update` `[FromForm]` (Code bỏ qua – G1) · `Delete` (xóa mềm, ReferenceGuard: đã có dòng đơn → chỉ cho Ngừng bán) · `GetById`.

## 6. Menu / quyền (seed – chỉ thêm)
Nhóm menu cấp 1 mới **"Bán lẻ"** (tách khỏi "Kinh doanh" doanh nghiệp):
- `#/sal-retail-order` "Đơn khách lẻ"
- `#/sal-retail-product` "Sản phẩm bán lẻ"
Quyền đề xuất: nhóm Kinh doanh (SAL / SAL-HEAD) Truy cập + Thêm + Sửa + Xuất; CEO / COO Truy cập. Cập nhật `catalog.js` (CodeGen) + `CatalogMenuTests`.
Giao diện đã khai báo 2 màn với cờ `extra` (`modules/retail.js`) → trước khi có seed, chỉ admin thấy trên sidebar.

## 7. Kiểm thử cần có
- SubmitOrder: giá gửi lên sai bị bỏ qua; ngày qua giờ chốt → 400; sản phẩm Ngừng bán → 400; honeypot; giới hạn 429; không cần token.
- Transition: đúng máy trạng thái, Hủy bắt buộc lý do, người thao tác = `ICurrentActor`.
- Đơn lẻ **không** xuất hiện trong `Order/*`, `crm-order`, MRP.

## 8. Để sau (ngoài RETAIL-1)
Tài khoản khách / tra cứu đơn của tôi (GAP-PORTAL-01) · thanh toán trực tuyến · đưa đơn lẻ vào kế hoạch sản xuất / MRP · ảnh sản phẩm · báo cáo doanh thu bán lẻ · đồng bộ kế toán AMIS.
