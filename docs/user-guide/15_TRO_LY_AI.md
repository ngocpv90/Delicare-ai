# 15 – Trợ lý AI (agent)

Viết theo code AI-1 … AI-8 (D-133, 27/11/2026). Trợ lý AI gồm 13 agent; **đợt 1 bật 5 agent chỉ đọc**: VIVI (điều phối), WH (kho), KITCHEN (bếp), QA (ATTP), KNOW (thủ thư tri thức). Agent trả lời bằng số liệu lấy thẳng từ DELI-CARE (cùng cách tính với màn hình) và kho tri thức; **đợt 1 agent không ghi gì vào nghiệp vụ** (chỉ ghi nhật ký vào kho tri thức 96 / 97 / 98).

## 1. Bạn là ai

- **CEO / COO / FIN-IT**: xem nhóm menu **Trợ lý AI**; **bật / tắt agent** (quyền Sửa Trung tâm agent).
- **Ban GĐ + trưởng bộ phận** (theo cấu hình `Agent:AskingGroups`): hỏi Trợ lý qua bot Telegram **Trợ lý** sau khi đã liên kết tài khoản (như bot Vận hành – `20_BOT_TELEGRAM.md`).
- Người chưa liên kết tài khoản nhắn bot Trợ lý: bot **không trả lời số liệu**.

## 2. Màn hình được dùng

| Menu › Mục | screen-id | Dùng để |
|---|---|---|
| Trợ lý AI › Trung tâm agent | `ai-agents` | 13 thẻ agent: đang bật / tắt, số công cụ, lời gọi hôm nay, bị từ chối; nút **Chi tiết**; công tắc **Bật / Tắt** |
| Trợ lý AI › Trung tâm agent › Chi tiết | `ai-agents?code=WH` | Mục đích, công cụ được dùng (và màn kiểm quyền), thư mục tri thức được đọc, lịch chạy, hoạt động gần đây |
| Trợ lý AI › Nhật ký agent | `ai-agent-log` | Mọi lời gọi công cụ: lúc, agent, công cụ, người hỏi, kết quả (Thành công / Bị từ chối / Lỗi), thời gian xử lý, số dòng |
| Trợ lý AI › Lịch chạy | `ai-schedule` | Việc theo giờ của từng agent (05:30 kho, 07:00 bản tin sáng, 08:30 QA, 10:00 bếp, 17:00 cuối ngày, mỗi 5 phút lấy sự kiện…), lần chạy gần nhất |

## 3. Agent làm gì (đợt 1)

| Agent | Hỏi được gì | Theo lịch |
|---|---|---|
| VIVI | Bảng điều hành trong ngày, KPI 7 ngày, sự cố hôm nay, việc **bạn** đang chờ duyệt, cảnh báo, tiến độ bếp, kế hoạch, phản ánh | 07:00 bản tin sáng, 17:00 báo cáo cuối ngày (đồng thời ghi kho thư mục 10), mỗi 5 phút lấy sự kiện mới |
| WH | Tồn kho, dưới tối thiểu, lô sắp hết hạn, nhu cầu nguyên liệu 7 ngày, truy xuất lô | 05:30 báo cáo kho |
| KITCHEN | Tiến độ bếp theo mẻ / công đoạn, kế hoạch, thực đơn tuần, hao hụt, nhiệt độ | 10:00 tiến độ trước giờ giao trưa |
| QA | Truy xuất lô / mẻ, nhiệt độ ngoài ngưỡng, phản ánh, lô tạm giữ | 08:30 rà nhiệt độ, cảnh báo, phản ánh quá hạn |
| KNOW | Tìm SOP / hướng dẫn / tiền lệ trong kho tri thức (luôn nêu nguồn + ngày hiệu lực) | Thứ Hai 22:00 rà ghi chú quá hạn rà soát |

**Agent chỉ thấy những gì bạn được thấy:** mỗi lời hỏi kiểm **quyền của agent ∩ quyền của bạn** trên màn tương ứng (vd hỏi tồn kho cần quyền Xem Tồn kho). Không có quyền → agent trả lời "không có quyền xem …".

## 4. Bật / tắt agent

1. **Trợ lý AI › Trung tâm agent** → công tắc trên thẻ agent.
2. Tắt: hộp xác nhận → **Tắt agent**. Có hiệu lực **ngay** từ lời gọi sau (bot không trả lời bằng agent đó, lịch của agent dừng). Job lõi (khóa đơn, MRP, bản tin bot Điều hành…) vẫn chạy đúng giờ.
3. Bật lại: gạt công tắc – không cần xác nhận.
4. Thẻ ghi "máy chủ chưa có khóa agent": IT cần đặt biến môi trường `AGENT_KEY_<MÃ>` trên server (`agents/README.md`).

## 5. Đọc nhật ký agent

- Lọc theo agent, kết quả, công cụ, khoảng ngày → **Lọc**; **Xem thêm** để tải trang sau.
- **Bị từ chối**: sai / thiếu khóa, agent đang tắt, vượt giới hạn lời gọi / phút, người hỏi chưa liên kết hoặc không có quyền màn – cột Ghi chú nói lý do.
- **Lỗi**: lỗi khi chạy công cụ – báo IT kèm thời điểm.

## 6. An toàn

- Agent **không có** quyền ghi nghiệp vụ ở đợt 1; từ đợt 2 mọi đề xuất ghi đều cần người xác nhận.
- Agent **không bao giờ** đọc thư mục **BOD Bảo mật**; kết quả trả cho agent đã che số điện thoại, email, CCCD, số tài khoản.
- Nội dung khách viết (phản ánh…) là **dữ liệu** – agent không làm theo chỉ dẫn trong đó.
- Tắt toàn bộ Trợ lý: IT đặt `Agent:Enabled = false` (hoặc tắt từng agent trên màn).

## 7. Lỗi thường gặp

| Hiện tượng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Bot Trợ lý không trả lời số liệu | Tài khoản Telegram chưa liên kết / không thuộc nhóm được hỏi đợt này | Liên kết như bot Vận hành; hỏi IT danh sách nhóm `Agent:AskingGroups` |
| Agent nói "không có quyền xem màn …" | Bạn hoặc agent chưa được cấp màn đó | Xin quyền Xem màn tương ứng |
| Lịch chạy "chưa chạy" | Hermes chưa chạy / chưa nạp lịch | IT chạy `tools/agent-eval/gen-cron.mjs` (agents/README.md) |
