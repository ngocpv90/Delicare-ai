# Bàn giao frontend – BE3.3: chuông cảnh báo, tìm kiếm toàn cục, tab "Liên quan" (đợt BE-3, D-57 h / i, D-58)

Quy ước chung xem `P0-P2.md` mục 1:
- đăng nhập;
- `Result` / `Results`;
- lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt;
- quyền theo `listMenu`.

Thử nhanh: `docs/api-tests/be3.3-alerts-search.http` (dữ liệu có sẵn trên DB dev, đã chạy 31 / 31). **Chỉ đọc. Không có SP, seed hay mục menu mới.**

Cả 3 action nằm trong `CommonController` (API dùng chung BE2.7), là `[SkipPermission]`: server tự lọc theo quyền **Xem** của từng màn hình (cơ chế quyền cũ UserRole × ObjectMenu, admin thấy hết). **Không API nào trả tên bảng / cột** (D-58).

## Quyền

| Action | Quyền |
|---|---|
| `Common/GetAlerts` | Mỗi cảnh báo chỉ trả khi người dùng có quyền Xem màn của nó (bảng mục 1). Cảnh báo duyệt = hộp duyệt của chính người dùng (quyền Duyệt). Không có quyền gì → danh sách rỗng. |
| `Common/GlobalSearch` | Màn hình = mục menu người dùng được Xem; bản ghi = chỉ loại có quyền Xem màn danh sách. |
| `Common/GetRelated` | Xem màn của bản ghi nguồn (không có → **HTTP 403** kèm Result `responseCode 403`); nhóm không có quyền Xem bị bỏ. |

## 1. Chuông cảnh báo (Catalog 18, spec 12.1)

`GET api/Common/GetAlerts?branchId=`

- `branchId` (tùy chọn): Id chi nhánh đang chọn trên header (`api/OrganizationUnit/GetBranches`). Lọc gồm cả đơn vị con. Id không có → 400.
- Trả `Results` (`listData`), **chỉ cảnh báo có `count > 0`**, cao trước (HIGH → MEDIUM → LOW).
- **FE hỏi định kỳ 60 giây** (không có push / SignalR – D-57 h). Server cache 60 giây theo (người, chi nhánh) – gọi nhanh hơn cũng nhận lại kết quả cũ.
- Badge chuông = tổng `count` (các mã không trùng nhau). Bấm 1 dòng → điều hướng `route`.

```json
{
  "totalRecords": 3,
  "listData": [
    { "code": "COMPLAINT_OVERDUE", "severity": "HIGH", "title": "Phản ánh quá hạn xử lý", "detail": "9 phản ánh đã quá hạn SLA", "route": "#/log-complaint", "count": 9 },
    { "code": "TEMPERATURE_OUT_OF_RANGE", "severity": "HIGH", "title": "Nhiệt độ ngoài ngưỡng", "detail": "40 lần đo ngoài ngưỡng trong 48 giờ qua (kể cả cảm biến IoT)", "route": "#/qc-temperature", "count": 40 },
    { "code": "COMPLAINT_OPEN", "severity": "LOW", "title": "Phản ánh đang mở", "detail": "15 phản ánh mới / đang xử lý (chưa quá hạn)", "route": "#/log-complaint", "count": 15 }
  ],
  "responseCode": 200
}
```

| `code` | Mức | Route (quyền Xem) | Cách tính | Lọc chi nhánh |
|---|---|---|---|---|
| `STOCK_BELOW_MIN` | HIGH | `#/scm-inv-balance` | Số nguyên liệu: Σ tồn khả dụng của kho < `MinimumStock` của chính sách tồn (kho × NL); NL không có chính sách thì so tổng các kho với mức tối thiểu của nguyên liệu | Kho |
| `APPROVAL_PENDING` | HIGH nếu có quá hạn, còn lại MEDIUM | `#/wf-inbox` (không cần quyền Xem) | Yêu cầu đang chờ **bạn** duyệt – đúng như `ApprovalRequest/GetMyPending` | Không |
| `QC_INCOMING_PENDING` | MEDIUM | `#/qc-inspection` | Phiếu nhập Chờ QC (chưa kết luận), `detail` kèm số dòng | Kho |
| `LOT_ON_HOLD` | MEDIUM | `#/scm-lot` | Lô còn tồn đang Tạm giữ / QC HOLD | Kho có tồn |
| `TEMPERATURE_OUT_OF_RANGE` | HIGH | `#/qc-temperature` | Lần đo ngoài ngưỡng trong 48 giờ (cả IoT) | Kho của khu vực / vị trí |
| `COMPLAINT_OVERDUE` | HIGH | `#/log-complaint` | Phản ánh Mới / Đang xử lý đã quá hạn SLA | Chi nhánh của khách |
| `COMPLAINT_OPEN` | LOW | `#/log-complaint` | Phản ánh Mới / Đang xử lý chưa quá hạn | Chi nhánh của khách |
| `OUTBOUND_FAILED` | MEDIUM | `#/int-outbound` | Tin nhắn gửi ra ngoài lỗi | Không |
| `LOT_EXPIRING` | MEDIUM | `#/scm-lot` | Lô Khả dụng còn tồn hết hạn trong 2 ngày tới (tính cả hôm nay) | Kho có tồn |

Dữ liệu không xác định được chi nhánh (kho chưa gắn đơn vị, khách chưa gắn chi nhánh, lần đo của mẻ nấu / xe giao) **vẫn tính** ở mọi chi nhánh (PROPOSED D-57 q). Gợi ý: biểu tượng theo `code`, màu theo `severity` (HIGH đỏ, MEDIUM cam, LOW xám).

## 2. Tìm kiếm toàn cục (Catalog 17)

`GET api/Common/GlobalSearch?q=`

- `q` 2…100 ký tự (sau khi bỏ khoảng trắng hai đầu); sai → 400. FE nên debounce ~300 ms và chỉ gọi khi đủ 2 ký tự.
- `screens`: mục menu (đang hoạt động, người dùng được Xem) có tên khớp – **không phân biệt hoa thường / dấu** ("don hang" khớp "Đơn hàng"); tối đa 10.
- `records`: chỉ 8 loại – `Customer`, `Order`, `Ingredient`, `Dish`, `Supplier`, `MaterialLot`, `Employee`, `PurchaseOrder`; tìm theo mã / tên / số chứng từ; **mỗi loại ≤ 5**, theo thứ tự trên. `type` = tên danh sách của API dùng chung (giống `Common/GetResources`) – dùng tiếp cho `Common/GetRelated` / `Common/GetHistory`.

```json
{
  "data": {
    "q": "PH-",
    "screens": [],
    "records": [
      { "type": "Order", "typeTitle": "Đơn đặt suất ăn", "route": "#/crm-order", "id": "7c7c78d7-…", "code": "PH-261117-0003",
        "label": "[DEMO] Khách Pilot P9A – 25/11/2026", "status": "Reconciled", "statusName": "Đã đối soát" }
    ]
  },
  "responseCode": 200
}
```

Gợi ý hiển thị: nhóm `records` theo `typeTitle`; bấm → mở `route` rồi mở chi tiết theo `id` (ngăn kéo bên phải).

## 3. Tab "Liên quan" (Catalog 16)

`GET api/Common/GetRelated?resource=<tên danh sách>&id=<Id>`

- `resource` chỉ nhận: `Customer`, `Contract`, `Order`, `Supplier`, `Ingredient`, `PurchaseOrder`, `MaterialLot`, `Dish`, `Employee` (không phân biệt hoa thường). Tên khác → 400; bản ghi không có → 400.
- Trả `Results` (`listData`) – mỗi nhóm = 1 màn hình: `key`, `title`, `route`, `count` (tổng), `items` (≤ 8 dòng mới nhất: `id`, `label`, `status`, `statusName`). Nhóm có `count = 0` vẫn trả (FE có thể ẩn).

| Nguồn | Nhóm (thứ tự) |
|---|---|
| Customer | Hợp đồng, Đơn đặt suất ăn, Đơn giao, Phản ánh khách hàng |
| Contract | Đơn đặt suất ăn |
| Order | Đơn giao, Đối soát suất ăn, Phản ánh khách hàng, Yêu cầu đổi món |
| Supplier | Đơn mua hàng, Phiếu nhập kho, Nguyên liệu nhà cung cấp, Lô nguyên liệu, Sự cố nhà cung cấp |
| Ingredient | Lô nguyên liệu, Nguyên liệu nhà cung cấp, Công thức (có dùng nguyên liệu) |
| PurchaseOrder | Phiếu nhập kho, Đề nghị mua, Sự cố nhà cung cấp |
| MaterialLot | Phiếu nhập kho, Phiếu xuất kho, Mẻ sản xuất (mẻ của lệnh có phiếu xuất lấy lô – truy xuất xuôi) |
| Dish | Công thức, Giá thành chuẩn |
| Employee | Phân ca, Đơn nghỉ |

```json
{
  "listData": [
    { "key": "Order", "title": "Đơn đặt suất ăn", "route": "#/crm-order", "count": 10,
      "items": [ { "id": "…", "label": "PH-261117-0003 – 25/11/2026", "status": "Confirmed", "statusName": "Đã xác nhận" } ] }
  ],
  "responseCode": 200
}
```

Gợi ý: hiện "Xem tất cả (count)" khi `count > items.length` → mở `route` (lọc theo bản ghi nguồn nếu màn đó có bộ lọc).

## Cấu hình (PROPOSED – `BusinessRules:Alerts`)

`CacheSeconds` 60, `TemperatureHours` 48, `LotExpiryDays` 2, `SearchMinLength` 2, `SearchMaxLength` 100, `SearchMaxPerType` 5, `SearchMaxScreens` 10, `RelatedMaxItems` 8.
