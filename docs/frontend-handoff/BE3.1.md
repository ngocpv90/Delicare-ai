# Bàn giao frontend – BE3.1: biểu đồ + sự kiện giao (đợt BE-3, D-57)

Quy ước chung xem `P0-P2.md` mục 1:
- đăng nhập;
- `Result` / `Results`;
- lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt;
- quyền theo `listMenu`.

Thử nhanh: `docs/api-tests/be3.1-charts.http` (dữ liệu `[DEMO]` có sẵn trên DB dev, đã chạy 22 / 22). **Không có SP, seed hay mục menu mới.**

Ngày giờ theo giờ Việt Nam. Ngày gửi dạng `yyyy-MM-dd`; ngày giờ gửi dạng `yyyy-MM-ddTHH:mm`.

## Quyền

Các action dưới đây thuộc màn hình đã có (ScreenMap).

| Action | Màn | Quyền |
|---|---|---|
| `OrderReconciliation/GetList` | `#/crm-reconciliation` | Xem |
| `MealWasteRecord/GetSummary` | `#/prod-waste` | Xem |
| `TemperatureLog/GetSeries` | `#/qc-temperature` | Xem |
| `DeliveryOrder/GetEvents` | `#/log-delivery-order` | Xem |
| `DeliveryEvent/GetVehiclePositions` | `#/log-event` | Xem |
| `DeliveryEvent/RecordPosition` | `#/log-event` | **Sửa**: nhóm tài xế cần quyền Sửa màn này (PROPOSED D-57 m) |

## 1. Đối soát + biểu đồ Đặt – Giao – Nhận (Catalog 100)

`GET api/OrderReconciliation/GetList`

**Tham số (query):**

| Tham số | Ý nghĩa |
|---|---|
| `From`, `To` | Ngày ăn. Trống = 30 ngày gần nhất; tối đa 366 ngày |
| `Status` | `Open` / `Matched` / `Variance` / `Closed` |
| `CustomerId` | Lọc theo khách |
| `SearchText` | Tìm số đơn hoặc tên khách |
| `Page`, `PageSize` | Mặc định 1 và 50, `PageSize` tối đa 500 |

**`data` trả về:**
```json
{
  "from": "2026-11-01T00:00:00", "to": "2026-12-31T00:00:00", "totalRecords": 12,
  "statusCounts": { "Closed": 12 },
  "chart": [ { "date": "2026-11-23T00:00:00", "ordered": 1000, "delivered": 1000, "accepted": 990 }, … ],
  "listItem": [ { "id", "orderId", "orderNumber", "mealDate", "customerId", "customerName", "reconciliationDate",
                  "orderedQuantity", "deliveredQuantity", "acceptedQuantity", "varianceQty", "variancePercent", "finalAmount",
                  "status", "statusName" } ]
}
```

| Trường | Cách tính |
|---|---|
| `chart` | Đủ **mọi ngày** trong khoảng, ngày không có đối soát = 0. Theo đúng bộ lọc (kể cả trạng thái), **không** phân trang. Vẽ cột / đường 3 chuỗi Đặt – Giao – Nhận |
| `statusCounts` | Tính **trước** khi lọc trạng thái, dùng cho chip lọc nhanh |
| `listItem` | Ngày ăn mới nhất trước |

## 2. Tổng hợp hao hụt (Catalog 157)

`GET api/MealWasteRecord/GetSummary`

**Tham số (query):**

| Tham số | Ý nghĩa |
|---|---|
| `From`, `To` | Theo thời điểm hao hụt. Trống = 30 ngày gần nhất |
| `GroupBy` | `wasteType` (mặc định) / `dish` / `shift` / `date`. Giá trị khác → 400 |

**`data` trả về:**
```json
{ "from", "to", "groupBy": "wasteType", "totalMeals": 33, "totalKg": 22.5, "totalCost": 66000, "recordsWithoutCost": 0,
  "listItem": [ { "key": "<mã loại>", "label": "Nấu dư", "quantityMeals": 33, "quantityKg": 0, "cost": 66000, "recordCount": 2 } ] }
```

| Trường | Ý nghĩa |
|---|---|
| `cost` | Số suất × giá thành chuẩn / suất của món (bản hiệu lực tại ngày hao hụt). `null` khi không tính được: không gắn món hoặc chưa có giá thành chuẩn (PROPOSED D-57 k) |
| `recordsWithoutCost` | Số dòng có suất hao hụt nhưng không tính được giá |
| `listItem` | Nhiều suất trước; riêng `GroupBy=date` xếp theo ngày |
| `key` rỗng | Nhóm "(Không gắn món)" / "(Không gắn ca)" / "(Không ghi loại)" |

## 3. Chuỗi nhiệt độ khu vực kho (Catalog 163)

`GET api/TemperatureLog/GetSeries`

**Tham số (query):**

| Tham số | Ý nghĩa |
|---|---|
| `StorageZoneId` | Bắt buộc |
| `From`, `To` | Ngày giờ. Trống = 48 giờ gần nhất; tối đa 31 ngày |

**`data` trả về:**
```json
{ "storageZoneId", "zoneCode", "zoneName", "temperatureMin": 0, "temperatureMax": 5, "from", "to",
  "measurementCount": 120, "outOfRangeCount": 1, "bucketMinutes": 0,
  "points": [ { "measuredAt": "…", "temperatureC": 3.2, "minC": null, "maxC": null, "isWithinRange": true, "count": 1 } ] }
```

- Kẻ 2 đường ngưỡng `temperatureMin` / `temperatureMax`, và tô đỏ điểm có `isWithinRange = false`.
- **Gộp điểm:** khi quá 2 000 lần đo, `bucketMinutes > 0`. Mỗi điểm khi đó là:
  - `temperatureC` = trung bình của khoảng;
  - `minC` / `maxC` = thấp nhất / cao nhất trong khoảng;
  - `count` = số lần đo trong khoảng;
  - `isWithinRange = false` nếu **có ít nhất 1** lần đo vượt ngưỡng, nên điểm vượt ngưỡng không bị mất khi gộp.
- Chỉ tính lần đo gắn **khu vực kho** (`StorageZoneId`). Nhiệt độ mẻ nấu / giao hàng không gắn khu vực nên không có trong chuỗi.

## 4. Timeline sự kiện đơn giao (Catalog 192)

`GET api/DeliveryOrder/GetEvents?id=<deliveryOrderId>` → `Results` (`listData`), theo thời gian tăng dần.

**Gồm:**
- sự kiện gắn đơn giao;
- sự kiện tại điểm dừng của đơn (đến điểm, đo nhiệt độ bàn giao, bàn giao, giao không thành công);
- sự kiện **cấp chuyến**: xếp hàng, xuất phát, đo nhiệt độ xuất phát, vị trí xe.

**Không gồm:** sự kiện của đơn giao khác trên cùng chuyến (PROPOSED D-57 l).

**Mỗi dòng (`DeliveryEventItemModel`):**
```json
{ "id", "deliveryRouteId", "routeNumber": "CX-…", "vehiclePlate": "51C-…", "driverName", "deliveryStopId", "stopSequenceNo": 1,
  "deliveryOrderId", "eventType": "ARRIVED", "eventTypeName": "Đến điểm giao", "eventAt", "latitude", "longitude",
  "temperatureC", "photoUrl", "description", "recordedByName", "sourceChannel" }
```

**`eventType`:**

| Mã | Nhãn |
|---|---|
| `LOADED` | Xếp hàng lên xe |
| `DISPATCHED` | Xuất phát |
| `TEMP_CHECK` | Đo nhiệt độ |
| `ARRIVED` | Đến điểm giao |
| `DELIVERED` | Bàn giao |
| `FAILED` | Giao không thành công |
| `POSITION` | Vị trí xe |

## 5. Vị trí xe theo sự kiện (Catalog 193, GAP-F-05)

Không có GPS liên tục. Vị trí lấy từ **sự kiện có tọa độ**: đến điểm hoặc giao không thành công (khi app gửi `latitude` / `longitude`), và vị trí do tài xế ghi.

**Đọc vị trí** – `GET api/DeliveryEvent/GetVehiclePositions?DeliveryDate=2026-12-01&RouteId=` (`RouteId` tùy chọn). `data` trả về:

| Trường | Nội dung |
|---|---|
| `deliveryDate` | Ngày giao |
| `listPosition` | Mọi điểm có tọa độ của các chuyến trong ngày, theo thời gian; dùng vẽ hành trình |
| `listLatest` | Điểm mới nhất của mỗi chuyến; dùng vẽ xe trên bản đồ |

**Tài xế ghi vị trí** – `POST api/DeliveryEvent/RecordPosition` (form: `routeId`, `latitude`, `longitude`, `note`):
- Chỉ khi chuyến **Đang xếp** hoặc **Đang giao**.
- Vĩ độ -90…90, kinh độ -180…180.
- Giờ = giờ máy chủ; người ghi = người gọi.
- Trả về 1 `DeliveryEventItemModel`.
- Gợi ý: app tài xế có nút "Gửi vị trí" (lấy GPS của điện thoại), hoặc tự gửi mỗi 5–10 phút khi đang giao.
