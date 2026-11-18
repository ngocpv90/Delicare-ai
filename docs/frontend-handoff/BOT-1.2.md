# BOT-1.2 – Liên kết Telegram (D-62, BOT_TELEGRAM §2) – API cho dev FE

Việc FE: **FE-BOT-1** (DECISIONS khối E) – nút "Liên kết Telegram" ở hồ sơ cá nhân, "Cấp mã liên kết" ở hồ sơ nhân viên; màn `#/int-identity` thêm nút Liên kết / Gỡ (admin).
Khung cũ D-30: form, kết quả `{ data, responseCode, responseMessage }`; `responseCode` 400 = lỗi nghiệp vụ (HTTP vẫn 200).

## API web

| API | Quyền | Vào | Ra / ghi chú |
|---|---|---|---|
| `POST api/TelegramLink/CreateCode` | Chỉ cần đăng nhập (cho chính mình) | – | `data = { code, expiresAt, deepLinks: { VH: "https://t.me/<bot>?start=<mã>", DH: … } }`. Hạn **15 phút**, dùng 1 lần. Hiện mã + nút mở bot (FE tự vẽ ảnh QR từ link – D-62 p12). |
| `POST api/TelegramLink/CreateCodeForEmployee` | Sửa `#/adm-employee` (HR) | form `EmployeeId` | Như trên, hạn **72 giờ** – cho nhân viên không khớp được số điện thoại. Không quyền → HTTP 403. |
| `GET api/TelegramLink/GetMine` | Chỉ cần đăng nhập | – | `listData[] = { id, botCode, externalUsername, displayName, identityStatus, linkedAt, lastSeenAt }`. |
| `POST api/TelegramLink/Unlink` | Chỉ cần đăng nhập (liên kết của chính mình) | form `id` | Gỡ → `Revoked`, bot ngừng phục vụ. |
| `POST api/UserExternalIdentity/Link` (Catalog 220) | **Admin** | form `BotCode` (VH / DH), `UserId` và / hoặc `EmployeeId`, `ExternalUserId` (Telegram user id), `ExternalUsername?`, `DisplayName?` | Liên kết tay (đã xác minh trực tiếp) → `Active`. |
| `POST api/UserExternalIdentity/Unlink` (220) | **Admin** | form `id` | → `Revoked`. |

## Trạng thái định danh (`UserExternalIdentity.IdentityStatus`)

| Mã | Nhãn | Ý nghĩa |
|---|---|---|
| `Active` | Đang dùng | |
| `PendingCode` | Chờ xác nhận mã (PROPOSED D-62 p17) | Người có quyền duyệt / admin mới khớp số điện thoại: dùng bot nhưng **chưa duyệt / quản trị được** tới khi nhập mã lấy bằng `CreateCode`. Gợi ý trên web: "Lấy mã xác nhận Telegram". |
| `Blocked` | Đã chặn bot | Người dùng chặn bot trên Telegram. Tự về lại khi họ /start lại bot. |
| `Revoked` | Đã gỡ | |

## Không dành cho FE

`api/BotChannel/*` (ResolveIdentity, LinkByPhone, LinkByCode, TouchIdentity, IssueUserToken, Employee*) – API nội bộ cho container `messaging` (khóa client).
