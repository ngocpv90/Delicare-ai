# RETAIL-1 – Module Bán lẻ (khách lẻ đặt trên web) – đặc tả + trạng thái

> **2026-11-27 – BACKEND ĐÃ LÀM** (anh giao làm cả backend): migration `DB/migrations/20261127_01_retail_order.sql` · `RetailPortalController` (công khai) · `RetailOrderController` · `RetailProductController` · service ở `AppDelicare.Services/Implement/Retail/` · test `AppDelicare.Tests/Sales/RetailTests.cs`. Kiểm đầu-cuối trên LocalDB: 28/28 lời gọi API đạt + đặt đơn thật trên giao diện trang khách. Lệch so với đặc tả: xem mục 9.

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

## 2. Bảng mới – FILE MIGRATION SẴN SÀNG ở `DB/proposed/20261127_01_retail_order.sql` (anh duyệt bảng mới 2026-11-26)

> **Backend:** chuyển file sang `DB/migrations/` trong CÙNG commit có Entity / Service / API. Đưa vào `DB/migrations/` sớm hơn thì server có `DB_AUTO_MIGRATE=0` sẽ dừng `db-sync` → API không lên.

File đã viết theo khuôn `SupplierIssue`, chạy lại được, đã thử 2 lần trên DB tạm (LocalDB): 3 bảng + 8 chỉ mục, lần 2 không đổi gì. **Chưa chạy trên DB dev / production** – chạy cùng lúc backend có Entity / Service (CodeGen `--only RetailProduct,RetailOrder,RetailOrderLine`).

| Bảng | Cột chính |
|---|---|
| `RetailProduct` | `Code` (G1 SPL-NNNNN), `Name`, `CategoryCode`, **`MealTypes`** (bữa được bán, nối `;` – vd `Trưa;Tối`), `Unit`, `Price` (gồm VAT), `Description`, `Tags` (nối `;`), `Icon`, `ImageUrl`, `SortOrder`, `Status` (Active / Inactive) |
| `RetailOrder` | `OrderNumber` (G1 BL-YYMMDD-NNNN), `Channel` (WEB / PHONE / ZALO), `CustomerName`, `PhoneNumber`, `Address`, `DeliveryDate` (date), `MealType`, `Note`, `OrderStatus`, `SubTotal`, `ShippingFee`, `TotalAmount`, `PaymentMethod` (COD / BANK), `PaymentStatus` (UNPAID / PAID), `ConfirmedAt/By`, `DeliveringAt`, `CompletedAt`, `CancelledAt`, `CancelReason`, `ClientIp`, **`ClientRequestId`** |
| `RetailOrderLine` | `RetailOrderId`, `RetailProductId`, `ProductName` / `Unit` / `UnitPrice` (chụp lúc đặt), `Quantity`, `LineAmount` |

Menu + quyền: `DB/scripts/RETAIL1_menu.sql` (nhóm "Bán lẻ" `#/group/02c` + 2 mục + quyền theo nhóm – xem đầu file; chạy thử trên DB dev: 1 nhóm, 2 mục, 21 dòng quyền). **Lưu ý DB dev:** `UserGroup.Code` đang trống ở mọi nhóm (mã nằm ở `Name`) → script khớp `Code`, rơi về `Name` khi `Code` trống; các script ACC* chỉ khớp `Code` nên đã cấp 0 dòng quyền trên DB dev.

Kèm theo:
- BUSINESS_RULES §11: đăng ký G1 `BL-YYMMDD-NNNN` (RetailOrder.OrderNumber), `SPL-NNNNN` (RetailProduct.Code); bảng phân nhóm mã (test chặn).
- `AuditChange` tự ghi (DbContext). Sổ append-only: không – đơn đổi trạng thái tại chỗ theo máy trạng thái mục 4.
- Seed `[DEMO]` 12 sản phẩm từ `app-delicare-client/src/assets/portal/portal-data.json` (script seed riêng).
- `ReferenceLists:MealType` dùng chung (Sáng / Trưa / Tối) cho `RetailProduct.MealTypes` và `RetailOrder.MealType`.
- `ReferenceLists:RetailCategory` trong appsettings: `com-van-phong` Cơm văn phòng, `an-sang` Món ăn sáng, `combo` Combo nhóm, `dinh-duong` Suất dinh dưỡng, `trang-mieng` Tráng miệng & đồ uống.

## 3. Quy tắc (BusinessRules:Retail – appsettings, không hard-code)

| Khóa | Mặc định | Ý nghĩa |
|---|---|---|
| `OrderCutoffHour` | 20 | Ngày nhận sớm nhất: trước giờ chốt → ngày mai; sau → ngày kia. Máy chủ **kiểm lại**, không tin giao diện. |
| `MaxDaysAhead` | 14 | Đặt trước tối đa 14 ngày. |
| `MaxLines` / `MaxQuantityPerLine` | 20 / 999 | Chặn đơn bất thường. |
| `SubmitLimitPerPhone` / `SubmitLimitPerIp` | 3 / 10 trong 60 phút | Chống spam đơn WEB; vượt → 429 + thông báo tiếng Việt. |
| `NotifyChannel` | Telegram Vận hành | Gửi tin đơn mới (số đơn, tên, SĐT, tổng tiền, ngày / bữa, nút mở `#/sal-retail-order?id=`). |

Bữa: mọi dòng phải có `RetailProduct.MealTypes` chứa `MealType` của đơn → sai: 400 "Bữa … không bán: …".
Chống trùng: đơn có `ClientRequestId` đã tồn tại (≤ 24 giờ) → trả lại **đơn cũ** (200, cùng `orderNumber`), không tạo đơn mới.
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
  "deliveryDate": "2026-11-28", "mealType": "Trưa", "note": "Ít cay", "website": "", "clientRequestId": "<uuid>",
  "lines": [ { "productId": "<guid>", "productCode": "SPL-00001", "quantity": 2 } ] }
```
- `website` = ô bẫy (honeypot) ẩn: có giá trị → trả 200 giả, không ghi.
- Nhận `productId` (GUID, ưu tiên) **hoặc** `productCode`. Trang khách chỉ gọi SubmitOrder khi danh mục lấy từ máy chủ (danh mục file dự phòng → đặt qua điện thoại).
- Trang khách chờ tối đa **15 giây**; hết giờ / 5xx → coi là "không rõ đã lưu", khách được nhắc gọi xác nhận, gửi lại dùng cùng `clientRequestId`.
- Trả `Result<{ id, orderNumber, totalAmount, deliveryDate, mealType }>`; 400 + `responseMessage` tiếng Việt khi sai (SĐT, ngày qua giờ chốt, sản phẩm ngừng bán…); 429 khi vượt giới hạn.

**GET `RetailPortal/GetWeeklyMenu`** → `Result<{ title, meals: ["Sáng","Trưa","Tối"], days: [{ date: "2026-11-30", day: "Thứ Hai", meals: { "Trưa": ["Thịt kho trứng", …] } }] }>` – thực đơn tuần **đã công bố** (`Menu` Published, tuần chứa hôm nay; sau Thứ Bảy → tuần sau), tên món theo buổi, không lộ giá vốn / FC. Không có → trang dùng thực đơn trong file.

**GET `RetailPortal/TrackOrder?orderNumber=&phoneNumber=`** → `Result<{ orderNumber, orderStatus, deliveryDate, mealType, customerName, totalAmount, paymentStatus, cancelReason, lines: [{ productName, quantity, unit, lineAmount }] }>` – **cả 2 phải khớp** (sai → 400 "Không tìm thấy đơn…", không phân biệt sai mã hay sai SĐT); giới hạn 20 lần / giờ / IP (429). Không trả địa chỉ.

### 5.2 Nội bộ – `RetailOrderController` (BaseController, ScreenMap `RetailOrder → sal-retail-order`)

| Action | Kiểu | Tham số | Trả |
|---|---|---|---|
| `GetList` | GET | `SearchText` (số đơn / tên / SĐT), `OrderStatus` (**một hoặc nhiều, cách dấu phẩy** – vd `New,Confirmed,Delivering`; lọc ở máy chủ trước khi cắt trang), `Channel`, `FromDate`, `ToDate` (ngày nhận), `Page`, `PageSize` | `Result<{ listItem: [RetailOrderViewModel], totalRecords, statusCounts: { New: n, … }, todayCount, todayAmount }>` |
| `GetDetail` | GET | `id` | `Result<{ order, lines: [{ id, retailProductId, productName, unit, quantity, unitPrice, lineAmount }], allowedTransitions: [] }>` |
| `GetPrepSummary` | GET | `date` | `Result<{ meals: [{ mealType, orderCount, items: [{ productName, unit, quantity }] }] }>` – đơn `Confirmed` + `Delivering` của ngày nhận, cộng theo món × bữa (tổng hợp cho bếp; quyền In hoặc Truy cập) |
| `Create` | POST `[FromBody]` | như SubmitOrder + `channel` (PHONE / ZALO), `shippingFee`, `paymentMethod` | `Result<{ id, orderNumber }>` – trạng thái `Confirmed` (nhân viên đã nói chuyện với khách) |
| `UpdateLines` | PUT `[FromBody]` | `id`, `lines`, `shippingFee`, `note`, `address`, `deliveryDate`, `mealType` | chỉ `New` / `Confirmed` |
| `Transition` | POST `[FromForm]` | `Id`, `To`, `Reason` (bắt buộc khi `Cancelled`), `ShippingFee`, `PaymentMethod` (khi `Confirmed`) | `Result<bool>` – `[Permission("sal-retail-order", Edit)]` |
| `Export` | GET | như GetList | Excel danh sách đơn (tên file tiếng Việt) |

`totalRecords` = tổng số đơn khớp lọc (giao diện báo khi danh sách bị cắt ở 500).

`RetailOrderViewModel`: `id, orderNumber, channel, customerName, phoneNumber, address, deliveryDate, mealType, orderStatus, subTotal, shippingFee, totalAmount, paymentMethod, paymentStatus, note, lineSummary` ("Suất cơm trưa tiêu chuẩn × 2; …"), `createdDate, confirmedAt, cancelReason`.

### 5.3 Danh mục – `RetailProductController` (CRUD mẫu `WarehouseController`, ScreenMap `RetailProduct → sal-retail-product`)
`GetAll` (`SearchText`, `CategoryCode`, `Status`, `Page`, `PageSize`; trả `Results` với `listData` gồm `mealTypes`) · `Insert` / `Update` `[FromForm]` (Code bỏ qua – G1; `MealTypes` bắt buộc, nối `;`) · `Delete` (xóa mềm, ReferenceGuard: đã có dòng đơn → chỉ cho Ngừng bán) · `GetById`.

## 6. Menu / quyền (seed – chỉ thêm)
Nhóm menu cấp 1 mới **"Bán lẻ"** (tách khỏi "Kinh doanh" doanh nghiệp):
- `#/sal-retail-order` "Đơn khách lẻ"
- `#/sal-retail-product` "Sản phẩm bán lẻ"
Quyền đề xuất: nhóm Kinh doanh (SAL / SAL-HEAD) Truy cập + Thêm + Sửa + Xuất; CEO / COO Truy cập. Cập nhật `catalog.js` (CodeGen) + `CatalogMenuTests`.
Giao diện đã khai báo 2 màn với cờ `extra` (`modules/retail.js`) → trước khi có seed, chỉ admin thấy trên sidebar.

## 6b. Bật trang khách lẻ
`assets/portal/portal-data.json` → `portalEnabled`: **true** (anh bật lại 2026-11-27). Đặt `false` thì vào `#/` là trang đăng nhập như cũ; xem trước bằng `#/?xemtruoc=1`. Nội dung chính sách / giới thiệu trong file vẫn là bản nháp chờ anh duyệt.

## 7. Kiểm thử cần có
- SubmitOrder: gửi lại cùng `clientRequestId` → cùng đơn; món sai bữa → 400; giá gửi lên sai bị bỏ qua; ngày qua giờ chốt → 400; sản phẩm Ngừng bán → 400; honeypot; giới hạn 429; không cần token.
- Transition: đúng máy trạng thái, Hủy bắt buộc lý do, người thao tác = `ICurrentActor`.
- TrackOrder: sai SĐT → 400 giống sai mã; không trả địa chỉ.
- Đơn lẻ **không** xuất hiện trong `Order/*`, `crm-order`, MRP.

## 8. Để sau (ngoài RETAIL-1)
Tài khoản khách / tra cứu đơn của tôi (GAP-PORTAL-01) · thanh toán trực tuyến · đưa đơn lẻ vào kế hoạch sản xuất / MRP · ảnh sản phẩm · báo cáo doanh thu bán lẻ · đồng bộ kế toán AMIS.

## 9. Ghi chú khi cài backend (2026-11-27)
- **Bữa** lưu NHÃN (`Trưa`) ở `RetailOrder.MealType` / `RetailProduct.MealTypes` vì giao diện bán lẻ + trang khách dùng nhãn; máy chủ nhận cả mã (`TRUA`) lẫn nhãn và chuẩn hóa về nhãn. PROPOSED – BA/PO CONFIRM.
- **Thực đơn tuần công khai** (`GetWeeklyMenu`) chỉ lấy thực đơn CHUNG (`Menu.MenuType = COMMON`) đã công bố – không lộ thực đơn riêng của khách doanh nghiệp. Chưa có → trang dùng thực đơn trong file.
- **Không có CRUD chung** cho `RetailOrder` / `RetailOrderLine` (đơn đi theo máy trạng thái; dòng chỉ sửa qua `UpdateLines` để tổng tiền không lệch).
- **IP khách** (giới hạn 429): đọc header `X-Real-IP` do nginx đặt, chỉ khi kết nối đến từ mạng nội bộ / loopback.
- **Tin bot** `RETAIL_ORDER_NEW` gửi người có quyền Thêm ở `#/sal-retail-order` (đã liên kết bot); không có nút mở web (bot chưa có link về web).
- **Seed** `DB/seed/RetailProduct_demo_seed.sql` = 12 món [DEMO] từ portal-data.json – CHỈ máy dev (không nằm trong `deploy/db/scripts.order`).
- **Chưa làm** (mục 8 giữ nguyên) + STATUS_CATALOG (docs/reference – chỉ đọc) chưa có dòng `RetailOrder.OrderStatus`: máy trạng thái ở `Model/Helper/RetailOrderStatus.cs`.

## 10. RETAIL-2 – tài khoản khách lẻ, Đơn của tôi, tiến độ, chuyến giao, nguồn gốc (D-136, 2026-11-27)

**DB:** `DB/migrations/20261127_02_retail_customer.sql` – bảng RetailCustomer + RetailOrder.RetailCustomerId / DeliveryRouteId (PROPOSED – anh duyệt).

**API công khai – `RetailPortal`** (phiên khách gửi ở header `X-Retail-Token`, hết phiên / sai → HTTP 401; vượt giới hạn → 429):

| Action | Kiểu | Ghi chú |
|---|---|---|
| `Register` | POST JSON `{fullName, phoneNumber, password, email?, address?, website}` | trả `{token, expiresAt, profile}`; SĐT không trùng; ô bẫy `website` |
| `Login` | POST JSON `{phoneNumber, password}` | sai SĐT / mật khẩu báo như nhau; khóa tạm sau `LoginFailLimit` lần |
| `GetProfile` / `UpdateProfile` / `ChangePassword` | GET / POST JSON | đổi mật khẩu trả phiên mới, phiên cũ hết hiệu lực |
| `GetMyOrders?group=active|done|cancelled|all` | GET | danh sách + `groupCounts`; mỗi đơn có `stageLabel` |
| `GetMyOrder?orderNumber=` | GET | chi tiết + `listStep` (tiến độ) + `route` (chuyến) + địa chỉ |
| `GetOrderTrace?orderNumber=` | GET | mẻ nấu (QC, nhiệt độ lõi, `sampleRetainedAt`), lô nguyên liệu, NCC |
| `ClaimOrder` | POST JSON `{orderNumber}` | gắn đơn đặt khi chưa đăng nhập – SĐT đơn phải trùng SĐT tài khoản |
| `SubmitOrder` | (cũ) | có phiên → đơn gắn vào tài khoản |
| `TrackOrder` | (cũ) | thêm tiến độ + chuyến; **không** trả địa chỉ / ghi chú |

**API nội bộ – `RetailOrder`:** `GetDetail` thêm `deliveryRouteId`, `route`, `listRouteOption` (chuyến cùng ngày nhận), `stageLabel`, `listStep`, `hasAccount`; `AssignRoute` (form `Id`, `DeliveryRouteId` – trống = bỏ gán; quyền Sửa `sal-retail-order`); `Transition` sang Delivering nhận thêm `DeliveryRouteId`.

**Trang khách (Angular `src/app/portal`):** `/tai-khoan/dang-nhap` (đăng nhập / đăng ký), `/tai-khoan` (hồ sơ, đổi mật khẩu), `/don-cua-toi` (3 nhóm), `/don-cua-toi/:so` (tab Tiến độ & giao hàng – tự tải lại mỗi phút khi đơn còn chạy; tab Nguồn gốc suất ăn). Giỏ hàng điền sẵn tên / SĐT / địa chỉ khi đã đăng nhập. **Màn nội bộ** `#/sal-retail-order`: chi tiết đơn có khối "Chuyến giao" (chọn + Lưu chuyến) và tab "Tiến độ".

**Kiểm thử:** `Sales/RetailAccountTests` (5 test: đăng ký / đăng nhập không lộ mật khẩu, SĐT không trùng, khóa sau N lần sai, phiên bị sửa → 401, đổi mật khẩu hủy phiên cũ, Đơn của tôi chỉ thấy đơn mình, gắn đơn phải trùng SĐT, phiên khách không vào API nội bộ).

**Để sau:** OTP xác thực SĐT (cần cổng SMS / Zalo ZNS), quên mật khẩu (cần OTP), bản đồ vị trí xe trực tiếp.
