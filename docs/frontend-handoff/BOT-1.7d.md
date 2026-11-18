# BOT-1.7d – QR trên phiếu in + nhóm chat + cảnh báo nhiệt độ giao – thay đổi API cho dev FE

Chỉ **thêm** (không đổi hành vi cũ, không bảng / cột mới – sau `git pull` chỉ cần build lại API).

| API | Thêm | Ghi chú |
|---|---|---|
| Phiếu in (`PrintDocumentModel` – Print / PrintBatch / GetPrintDocument) | `botLink` | `https://t.me/<bot VH>?start=DOC_<GR\|MI\|SC\|DO\|POD\|RC>_<Id N>` – **FE vẽ ảnh QR từ link này** trên màn in / bản in. Null khi bot Vận hành chưa có `BotUsername`. Bản in PDF / Word / Excel có thêm dòng ghi chú "Mở phiếu trên Telegram: <link>". |
| `GET api/Common/GetPrintDocument?type=&id=` (mới) | nội dung phiếu dạng JSON (`PrintDocumentModel`) | `type` = GOODS_RECEIPT / MATERIAL_ISSUE / STOCK_COUNT / DELIVERY_ORDER / POD / RECONCILIATION hoặc mã ngắn GR / MI / SC / DO / POD / RC. Quyền **Xem** màn của loại phiếu (403 nếu không có). Dùng được cho xem trước bản in. |

**Nhóm chat** (cấu hình, không cần FE): `IntegrationChannel.ConfigJson` của bot – `{ "groups": [ { "code": "BEP_ATTP", "chatId": "-100…", "templates": ["FOODSAFETY_CRITICAL", "CORE_TEMP_LOW"], "mode": "replace" } ] }`. API lưu kênh tích hợp (`IntegrationChannelRequest.ConfigJson`) đã nhận trường này – màn quản trị kênh chỉ cần có ô sửa ConfigJson (chưa kiểm màn FE).
