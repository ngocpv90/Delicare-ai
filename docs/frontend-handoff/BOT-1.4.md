# BOT-1.4 – Bot: menu, chấm công, duyệt (D-62) – thay đổi API cho dev FE

| API | Thay đổi | FE cần làm |
|---|---|---|
| `GET api/ApprovalRequest/GetMyPending` | Thêm tham số tùy chọn `explicitOnly` (mặc định `false` – web **không đổi**). `true` = chỉ bước có quyền Duyệt được cấp rõ qua nhóm, không tính quyền admin (bot dùng). | Không cần. |

Hướng dẫn dùng bot cho người dùng (màn trợ giúp / hồ sơ cá nhân, nếu FE muốn hiện): 3 nút cố định [⚡ Ca của tôi] [📂 Chức năng] [📍 Chấm công]; gõ mã chứng từ để tra; duyệt đơn nghỉ / đổi ca 1 chạm + Hoàn tác 10 giây; duyệt PO / PR / hợp đồng xác nhận 2 bước; từ chối cần lý do.
