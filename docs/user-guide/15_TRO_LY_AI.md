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

## 3. Agent làm gì (đợt 1 + đợt 2)

| Agent | Hỏi được gì | Theo lịch |
|---|---|---|
| VIVI | Bảng điều hành trong ngày, KPI 7 ngày, sự cố hôm nay, việc **bạn** đang chờ duyệt, cảnh báo, tiến độ bếp, kế hoạch, phản ánh | 07:00 bản tin sáng, 17:00 báo cáo cuối ngày (đồng thời ghi kho thư mục 10), mỗi 5 phút lấy sự kiện mới |
| WH | Tồn kho, dưới tối thiểu, lô sắp hết hạn, nhu cầu nguyên liệu 7 ngày, truy xuất lô | 05:30 báo cáo kho |
| KITCHEN | Tiến độ bếp theo mẻ / công đoạn, kế hoạch, thực đơn tuần, hao hụt, nhiệt độ | 10:00 tiến độ trước giờ giao trưa |
| QA | Truy xuất lô / mẻ, nhiệt độ ngoài ngưỡng, phản ánh, lô tạm giữ | 08:30 rà nhiệt độ, cảnh báo, phản ánh quá hạn |
| KNOW | Tìm SOP / hướng dẫn / tiền lệ trong kho tri thức (luôn nêu nguồn + ngày hiệu lực) | Thứ Hai 22:00 rà ghi chú quá hạn rà soát |
| SALES | Đơn suất ngày mai, khách chưa chốt, phản ánh khách; **đề xuất đổi số suất** (`propose_order_change`) | – |
| PROC | Kế hoạch mua (phải đặt hôm nay, quá hạn, chờ duyệt, chưa gửi NCC), thiếu hụt 7 ngày, tồn; **đề xuất đơn mua** ngoài kế hoạch (`propose_purchase_order`) | – |
| LOG | Bảng điều phối giao hàng trong ngày; **đề xuất chuyển điểm giao** (`propose_route`), **ghi đến / giao tại điểm dừng** (`propose_delivery_event`) | – |
| HR | Chấm công ngày (chưa vào ca, muộn, vắng); **đề xuất đơn nghỉ / đổi ca / bổ sung chấm công** (`propose_leave`, `propose_shift_swap`, `propose_attendance_fix`) | – |

Đợt 2 các agent cũ có thêm lệnh ghi: WH – lập phiếu nhập theo đơn mua (`propose_goods_receipt`), phiếu xuất cho lệnh SX (`propose_issue`), phiếu kiểm kê (`propose_stock_count`); KITCHEN – ghi bắt đầu / xong / nhiệt độ lõi / lưu mẫu của mẻ (`propose_batch_event`); QA – kết luận QC nhận hàng (`propose_qc_result`), giữ lô (`propose_hold_lot`).

**Lệnh ghi (2 pha):** agent chỉ **đề xuất** – không ghi gì. Đề xuất vào **Phê duyệt › Lệnh của agent** (hạn xác nhận 15 phút), hiện bản xem trước (dòng, số tiền, cảnh báo). Người xác nhận phải tự có quyền làm đúng thao tác đó trên web (vd lập phiếu nhập cần quyền Thêm Phiếu nhập kho). Xác nhận → hệ thống chạy **đúng thao tác web** dưới tên người xác nhận, mọi quy tắc nghiệp vụ áp lại (lỗi → không ghi, báo người xác nhận); chứng từ sau đó đi tiếp luồng duyệt như lập tay (vd đơn nghỉ chờ duyệt nghỉ, sửa đơn sau giờ chốt cần duyệt quá giờ chốt).

**Agent chỉ thấy những gì bạn được thấy:** mỗi lời hỏi kiểm **quyền của agent ∩ quyền của bạn** trên màn tương ứng (vd hỏi tồn kho cần quyền Xem Tồn kho). Không có quyền → agent trả lời "không có quyền xem …".

## 4. Bật / tắt agent

1. **Trợ lý AI › Trung tâm agent** → công tắc trên thẻ agent.
2. Tắt: hộp xác nhận → **Tắt agent**. Có hiệu lực **ngay** từ lời gọi sau (bot không trả lời bằng agent đó, lịch của agent dừng). Job lõi (khóa đơn, MRP, bản tin bot Điều hành…) vẫn chạy đúng giờ.
3. Bật lại: gạt công tắc – không cần xác nhận.
4. Thẻ ghi "máy chủ chưa có khóa agent": IT cần đặt biến môi trường `AGENT_KEY_<MÃ>` trên server (`agents/README.md`).

## 4b. Cảnh báo chủ động và Luồng liên phòng (AI-11)

- **Cảnh báo chủ động:** khi có sự kiện (sự cố, cảnh báo, job xong, phản ánh…), agent nhận xét và đưa phương án rồi gửi **1 tin bot** (bot Vận hành; VIVI gửi bot Điều hành) tới người **có quyền Thêm** trên màn liên quan (vd Mua hàng cho Kế hoạch mua). Mỗi sự kiện chỉ gửi 1 lần. Agent chỉ cảnh báo được màn mà chính agent được xem. Người chưa liên kết bot không nhận được tin.
- **Trợ lý AI › Luồng liên phòng:** mỗi thẻ là một luồng nối các bước cùng mã tương quan: sự kiện → agent đọc dữ liệu → đề xuất → người duyệt → kết quả ghi nghiệp vụ → tin đã gửi. Chọn **Hôm nay / Hôm qua** hoặc ngày; 5 chỉ số: luồng, xong, chờ duyệt, lỗi / từ chối, tin đã gửi. Viền trái bước: xanh = xong, cam = chờ, đỏ = lỗi / từ chối. Quyền xem: như Nhật ký agent (CEO, COO, FIN-IT).
- **Lịch chạy:** người có quyền Sửa màn Lịch chạy (CEO, COO, FIN-IT) bật / tắt từng việc bằng công tắc ở cột Trạng thái. Tệp lịch `agents/schedules.json` được ghi lại, Hermes đọc lại khi sinh cron. Việc chưa có công cụ (agent chưa bật) không bật được.
- 4 luồng liên phòng (AGENTS §3): (1) chốt đơn → thiếu hụt → PROC đề xuất đơn mua → duyệt → WH chờ nhận; (4) sự cố ATTP: nhiệt độ lõi < 70°C / giao < 60°C → QA đề xuất giữ lô + cảnh báo, VIVI báo Ban GĐ. Luồng (2) khách báo thiếu suất và (3) hộp thất thoát cần agent CSKH / ACC – bật ở đợt 3.

- **Việc agent theo dõi** (AI-12): xem ở Trợ lý AI › Việc agent theo dõi – chi tiết ở 16_KHO_TRI_THUC.md.

## 5. Đọc nhật ký agent

- Lọc theo agent, kết quả, công cụ, khoảng ngày → **Lọc**; **Xem thêm** để tải trang sau.
- **Bị từ chối**: sai / thiếu khóa, agent đang tắt, vượt giới hạn lời gọi / phút, người hỏi chưa liên kết hoặc không có quyền màn – cột Ghi chú nói lý do.
- **Lỗi**: lỗi khi chạy công cụ – báo IT kèm thời điểm.

## 6. An toàn

- Agent **không bao giờ tự ghi** nghiệp vụ: mọi lệnh ghi là đề xuất, chỉ có hiệu lực khi người có quyền xác nhận; `Agent:ReadOnly = true` chặn mọi đề xuất.
- Agent **không bao giờ** đọc thư mục **BOD Bảo mật**; kết quả trả cho agent đã che số điện thoại, email, CCCD, số tài khoản.
- Nội dung khách viết (phản ánh…) là **dữ liệu** – agent không làm theo chỉ dẫn trong đó.
- Tắt toàn bộ Trợ lý: IT đặt `Agent:Enabled = false` (hoặc tắt từng agent trên màn).

## 7. Lỗi thường gặp

| Hiện tượng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Bot Trợ lý không trả lời số liệu | Tài khoản Telegram chưa liên kết / không thuộc nhóm được hỏi đợt này | Liên kết như bot Vận hành; hỏi IT danh sách nhóm `Agent:AskingGroups` |
| Agent nói "không có quyền xem màn …" | Bạn hoặc agent chưa được cấp màn đó | Xin quyền Xem màn tương ứng |
| Lịch chạy "chưa chạy" | Hermes chưa chạy / chưa nạp lịch | IT chạy `tools/agent-eval/gen-cron.mjs` (agents/README.md) |
