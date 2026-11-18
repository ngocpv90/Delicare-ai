# BOT-1.1 – Lõi kênh tin nhắn (D-62) – API cho dev FE

Màn liên quan (đã có trong menu): `#/int-channel`, `#/int-bot`, `#/int-identity`, `#/int-inbound`, `#/int-outbound`, `#/int-log`.
Khung cũ D-30: `api/<Controller>/<Action>`, form (`[FromForm]`), kết quả `{ data, responseCode, responseMessage }`; `responseCode` 400 = lỗi nghiệp vụ (HTTP vẫn 200).

## API web mới

| API | Quyền | Vào | Ra / ghi chú |
|---|---|---|---|
| `POST api/InboundMessage/Reprocess` (Catalog 216) | Sửa `#/int-inbound` | form `id` | Chỉ tin **Failed / Ignored** → `Received` (bot xử lý lại). Khác → 400 "Chỉ xử lý lại được tin Lỗi / Bỏ qua". |
| `POST api/OutboundMessage/Enqueue` (217) | Sửa `#/int-outbound` | form `BotCode` (VH / DH, trống = VH), `UserId` **hoặc** `EmployeeId`, `Text` (≤ 4000 ký tự) | `data = { queued, messageIds[] }`. Người chưa liên kết bot → 400 "Người nhận chưa liên kết bot VH". |
| `POST api/OutboundMessage/Retry` (218) | Sửa `#/int-outbound` | form `id` | Chỉ tin **Failed / Cancelled** → `Queued`. Tin **Blocked** (người dùng chặn bot) không gửi lại. |
| `GET api/OutboundMessage/GetConversation` (219) | Xem `#/int-outbound` | `IdentityId` hoặc `UserId` hoặc `EmployeeId` (bắt buộc 1 trong 3), `BotCode?`, `From?`, `To?`, `Page`, `PageSize` (≤ 200) | `data = { total, items[] }`, mỗi item `{ id, direction: IN/OUT, at, botCode, externalUserId, messageType, status, payloadJson, correlationId, error }`, mới nhất trước. |

## Thay đổi hành vi API cũ

- `IntegrationChannel/GetAll`, `GetById`, `GetAllData`: `configJson` **đã che** các khóa có tên chứa `secret`, `token`, `key`, `password` → giá trị `"***"`. Khi sửa (`IntegrationChannel/Update`) gửi lại nguyên chuỗi đã che: khóa còn `"***"` **giữ bí mật cũ**; muốn đổi bí mật thì nhập giá trị mới. `secretReference` chỉ là **tên biến môi trường** (vd. `Telegram__Bots__VH__Token`), không phải token.
- `IntegrationLog/Insert` **bỏ** (sổ chỉ ghi thêm do backend ghi) – màn `#/int-log` chỉ xem, ẩn nút Thêm.

## Trạng thái (PROPOSED D-62 p2 – hiển thị nhãn tiếng Việt)

| Bảng.cột | Giá trị |
|---|---|
| `InboundMessage.ProcessingStatus` | `Received` Đã nhận · `Processing` Đang xử lý · `Processed` Đã xử lý · `Failed` Lỗi · `Ignored` Bỏ qua |
| `OutboundMessage.MessageStatus` | `Queued` Chờ gửi · `Sending` Đang gửi · `Sent` Đã gửi · `Failed` Lỗi (tự thử lại sau 1′, 5′, 15′, 60′, 240′) · `Blocked` Người dùng chặn bot · `Cancelled` Đã hủy |
| `UserExternalIdentity.IdentityStatus` | `Active` · `Revoked` · `Blocked` (người dùng chặn bot) |

`OutboundMessage.QueuedAt` = **giờ được phép gửi** (lần đầu = lúc xếp; sau lỗi = giờ thử lại kế tiếp); giờ tạo gốc = `CreatedDate`.

## Không dành cho FE

`api/BotChannel/*` là API nội bộ cho container `messaging` (khóa client `X-Client-Key`, không JWT). nginx production trả 404 từ ngoài. **`app-delicare-client/nginx.conf` (Docker của dev FE) nên thêm** khối tương tự, đặt trước `location /api/`:

```nginx
location ~* ^/api/BotChannel(/|$) { return 404; }
```

Liên kết tài khoản (nút "Liên kết Telegram", "Cấp mã liên kết") có API ở BOT-1.2.
