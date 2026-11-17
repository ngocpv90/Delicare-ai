# Rà soát backend 2026-11-17 – thay đổi hành vi API (cho dev FE)

| API | Thay đổi | FE cần làm |
|---|---|---|
| `POST api/User/Insert`, `PUT api/User/Update` | Trường `IsAdmin` trong form **bị bỏ qua**. | Đổi quyền quản trị bằng nút gọi `User/SaveIsAdmin` (đã có, AdminOnly). |
| `PUT api/User/ChangePassword` | Chỉ đổi mật khẩu **của tài khoản đang đăng nhập**; `Id` khác → `responseCode` 400 "Chỉ đổi được mật khẩu của chính tài khoản đang đăng nhập." | Gửi `Id` = user hiện tại (hoặc bỏ trống). Admin đổi cho người khác: `User/ResetPassword`. |
| `PUT api/Person/UpdateProfile` | Chỉ sửa hồ sơ của chính mình (`Id` khác → 400). Ảnh: **chỉ PNG / JPEG ≤ 5 MB**, nội dung phải đúng định dạng; sai → "Tải tệp thất bại". Tên tệp lưu do server sinh. Thay đổi hồ sơ nay **được lưu** (trước đây bị rollback). | `accept="image/png,image/jpeg"`, kiểm 5 MB phía client. |
| `POST api/Authentication/Login` | Sai ≥ 10 lần / tên đăng nhập (hoặc ≥ 50 / IP) trong 15 phút → **HTTP 429**, body `{ code: 429, message: "Đăng nhập sai quá nhiều lần – vui lòng thử lại sau 15 phút." }`. | Hiện `message` khi HTTP 429. |
| `POST api/ProductionBatch/RecordCoreTemperature`, nhiệt độ chuyến (`DeliveryRoute` – xuất phát / điểm giao) | `MeasuredAt` chỉ nhận trong **24 giờ trước → 5 phút sau** giờ hiện tại; ngoài khoảng → 400. Bỏ trống = giờ hiện tại. | Mặc định để trống; nếu cho chọn giờ thì giới hạn khoảng trên. |
| `GET api/MealCost/GetList` | Không có `OrderId` → bắt buộc khoảng ngày ăn: mặc định 31 ngày gần nhất, tối đa 92 ngày (quá → 400). | Màn giá vốn suất luôn gửi `From` / `To`. |
| SP danh sách `User`, `UserGroup`, `UserClaim`, `Person`, `ObjectMenu`, `AuditChange`, `LocationTracking` (`GetAll`) | `PageSize` tối đa 1000. | Không gửi PageSize > 1000. |
