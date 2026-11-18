# BOT-2.4 – QR vật thể + vị trí trực tiếp tài xế – thay đổi API cho dev FE

Chỉ **thêm** tham số / trường (không đổi hành vi cũ, không bảng / cột mới – sau `git pull` chỉ cần build lại API).

| API | Thêm | Ghi chú |
|---|---|---|
| `GET api/ProductionBatch/GetBoard` | tham số `LineCode` (≤ 50) | Chỉ mẻ của dây chuyền (`ProductionLine.Code` của lệnh SX) hoặc bàn nấu (`KitchenWorkstation.Code` của mẻ) có mã này. Bỏ trống = như cũ. |
| `GET api/InventoryBalance/GetByLot` | tham số `storageLocationCode` (≤ 50) | Chỉ tồn tại vị trí kho (`StorageLocation.Code`). Bỏ trống = như cũ. |
| `GET api/DeliveryRoute/GetMine` | `data.listKitchen[] { code, latitude, longitude, radiusMeters }` | Bàn / trạm bếp có tọa độ (radiusMeters = 0 → dùng bán kính mặc định). Chỉ GetMine (tài xế) điền; GetBoard trả rỗng. |
| Chi tiết chuyến (`GetMine`, `GetBoard`, `GetDetail`) | `listStop[].latitude`, `listStop[].longitude` | Tọa độ điểm giao (`CustomerDeliveryLocation`) – có thể dùng hiện bản đồ. |

**QR in / dán** (ảnh QR do FE sinh từ link, BE không vẽ QR): `https://t.me/<bot VH>?start=LINE_<mã dây chuyền hoặc bàn nấu>`, `VEH_<mã xe hoặc biển số bỏ dấu>`, `LOC_<mã vị trí kho>`, `CLEAN` (khu vệ sinh hộp khay). Mã chỉ gồm chữ / số / `-` / `_`, ≤ 50 ký tự.
