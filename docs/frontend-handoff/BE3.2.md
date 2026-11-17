# Bàn giao – BE3.2: IoT nhiệt độ (Catalog 166, đợt BE-3, D-57 f)

API này cho **cảm biến / cổng IoT** gọi, không cho người dùng. Frontend không gọi trực tiếp. Lần đo IoT hiện trên:
- biểu đồ nhiệt độ khu vực kho (`TemperatureLog/GetSeries` – BE3.1);
- danh sách nhật ký nhiệt độ (`TemperatureLog/GetAll`, `sourceChannel = IOT`);
- chuông cảnh báo khi vượt ngưỡng (BE3.3).

Thử: `docs/api-tests/be3.2-iot.http`, đã chạy 3 / 3 trên DB dev với khóa đặt lúc chạy.

## Gọi API

`POST api/IotTemperature/Receive`
- Header: `X-Api-Key: <khóa>`, `Content-Type: application/json`.
- **Không** cần token đăng nhập; token người dùng cũng **không** thay được khóa.

```json
[ { "deviceCode": "SENSOR-KHO-MAT-01", "measuredAt": "2026-11-17T08:00:00", "temperatureC": 3.4 }, … ]
```

| Trường | Quy tắc |
|---|---|
| `deviceCode` | Phải khai báo trong cấu hình `Iot:Devices`; không phân biệt hoa thường |
| `measuredAt` | Không ghi múi giờ = giờ Việt Nam. Có `Z` hoặc `+hh:mm` → đổi về giờ Việt Nam. Bỏ phần lẻ giây. Nhận lần đo trong **7 ngày** gần nhất, ở tương lai tối đa **5 phút** |
| `temperatureC` | Trong khoảng -60 … 150 °C; ngoài khoảng coi là lỗi cảm biến |
| Số lần đo | Tối đa **500 lần đo / lần gửi** |

**Kết quả** (`Result`, HTTP 200):
```json
{ "responseCode": 200, "responseMessage": "Đã ghi 3 lần đo, trùng 1, từ chối 2",
  "data": { "received": 6, "accepted": 3, "duplicate": 1, "outOfRange": 1,
            "rejected": [ { "index": 4, "deviceCode": "THIET-BI-LA", "reason": "Thiết bị chưa khai báo trong cấu hình Iot:Devices" } ] } }
```

| Trường | Ý nghĩa |
|---|---|
| `accepted` | Số lần đo đã ghi |
| `duplicate` | Cùng (`deviceCode`, `measuredAt`) đã có hoặc lặp trong lô → bỏ qua. Gửi lại cả lô sau khi mất mạng là **an toàn** |
| `outOfRange` | Số lần đã ghi nhưng vượt ngưỡng khu vực. Ngưỡng = `StorageZone.TemperatureMin / Max`, tính ở server |
| `rejected` | Từng lần đo bị từ chối: vị trí trong mảng (từ 0) + lý do. Các lần đo còn lại **vẫn được ghi** |

**Lỗi:**

| HTTP | Trường hợp |
|---|---|
| **401** | Thiếu khóa, sai khóa, hoặc máy chủ chưa cấu hình khóa (API đang tắt) |
| **429** | Quá **60 request / phút / khóa** |
| 200 + `responseCode` 400 | Lô rỗng hoặc quá 500 lần đo |

## Ghi vào đâu

Bảng có sẵn `TemperatureLog`, không tạo bảng mới:

| Cột | Giá trị |
|---|---|
| `SourceChannel` | `IOT` |
| `DeviceCode` | Mã thiết bị (chữ hoa) |
| `StorageZoneId` / `StorageLocationId` | Theo cấu hình thiết bị |
| `MeasurementType` | `STORAGE` (hoặc theo thiết bị, ví dụ `BLAST_CHILL`) |
| `MinAllowedC` / `MaxAllowedC` / `IsWithinRange` | Ngưỡng của khu vực |
| `RecordedBy` / `CreatedBy` | Tài khoản `system` |

## Cấu hình máy chủ (anh / IT)

- **Khóa API là bí mật:** đặt `Iot__ApiKeys__0`, `Iot__ApiKeys__1`… bằng biến môi trường hoặc user-secrets, **không** ghi vào `appsettings.json` trong repo.
  - Mỗi cổng IoT nên có 1 khóa riêng.
  - Đổi khóa: thêm khóa mới, cập nhật thiết bị, rồi xóa khóa cũ.
- **Thiết bị:** khai báo trong `Iot:Devices`, ví dụ:
  ```json
  "Iot": { "Devices": { "SENSOR-KHO-MAT-01": { "Name": "Cảm biến kho mát 1", "StorageZoneCode": "MAT" } } }
  ```
  hoặc bằng biến môi trường `Iot__Devices__SENSOR-KHO-MAT-01__StorageZoneCode=MAT`.
  - Có thể dùng `StorageZoneId` thay cho mã. Mã khu vực trùng ở nhiều kho thì phải dùng `StorageZoneId`.
  - Tùy chọn: `StorageLocationId`, `MeasurementType`, `Enabled: false` (tạm ngưng thiết bị).
- **Giới hạn** (`Iot:*`, PROPOSED D-57 f): `MaxItemsPerRequest` 500, `RateLimitPerMinute` 60, `MaxAgeDays` 7, `MaxFutureMinutes` 5, `MinTemperatureC` / `MaxTemperatureC` -60 / 150.
