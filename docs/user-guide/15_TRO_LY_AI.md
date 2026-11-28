---
screens: [ai-agents?tab=overview, ai-agents?tab=tasks, ai-agents?tab=flow, ai-agents?tab=schedule, ai-agents?tab=log, ai-agents?tab=usage]
roles: []
updated: 2026-11-28
---
# 15 – Trợ lý AI (agent)

Viết theo code AI-1 … AI-21 (D-133, D-138, cập nhật 28/11/2026). Trợ lý AI gồm 13 agent; **đợt 1 bật 5 agent chỉ đọc**: VIVI (điều phối), WH (kho), KITCHEN (bếp), QA (ATTP), KNOW (thủ thư tri thức). Agent trả lời bằng số liệu lấy thẳng từ DELI-CARE (cùng cách tính với màn hình) và kho tri thức; **đợt 1 agent không ghi gì vào nghiệp vụ** (chỉ ghi nhật ký vào kho tri thức 96 / 97 / 98).

## 1. Bạn là ai

- **CEO / COO / FIN-IT**: thấy **Điều hành › Trung tâm Agent** đủ 7 tab, kể cả **chi phí AI**; **bật / tắt agent** (quyền Sửa Trung tâm Agent), bật / tắt việc theo lịch.
- **Trưởng bộ phận / người xác nhận lệnh agent** (thủ kho, bếp, QA, mua hàng, KD trưởng, giao vận, nhân sự – quyền mục Giao việc): mục **Trung tâm Agent** chỉ có tab **Giao việc** + **Chat với agent**; thấy việc của màn mình được xem.
- **Ban GĐ + trưởng bộ phận** (theo cấu hình `Agent:AskingGroups`): hỏi Trợ lý qua bot Telegram **Trợ lý** sau khi đã liên kết tài khoản (`20_BOT_TELEGRAM.md`).
- Người chưa liên kết tài khoản nhắn bot Trợ lý: bot **không trả lời số liệu**. Người không có quyền mục nào của Trung tâm Agent: không thấy mục, mở link → "chưa được cấp quyền".

## 2. Trung tâm Agent – một màn 7 tab (AI-21, D-138)

Menu: **Điều hành › Trung tâm Agent** (ngay dưới Phê duyệt). Nhóm sidebar "Trợ lý AI" cũ đã bỏ; Nhật ký agent, Lịch chạy, Luồng liên phòng, Việc agent theo dõi thành **tab** (link cũ `#/ai-agent-log`, `#/ai-schedule`, `#/ai-flow`, `#/ai-tasks` tự chuyển sang tab). Tab hiện theo quyền Xem của mục tương ứng (mục ẩn khỏi sidebar nhưng vẫn phân quyền ở ma trận):

| Tab | Đường dẫn | Quyền (mục) | Dùng để |
|---|---|---|---|
| Tổng quan | `ai-agents?tab=overview` | `ai-agents` (CEO, COO, FIN-IT) | Hàng robot 13 agent (màu theo agent, xám = chưa bật, số lượt hôm nay); 3 nhóm chỉ số: **Cần xử lý hôm nay** (việc đang mở, bước luồng cần chú ý), **Hoạt động agent** (agent đang chạy, lượt hỏi, lịch tự động, kho tri thức), **Chi phí AI · tháng**; danh sách agent (lần gọi gần nhất), thẻ agent đang chọn (hỏi nhanh, Chat, Chi tiết, công tắc Bật / Tắt), chi phí chi tiết, hoạt động gần đây |
| Chat với agent | `ai-agents?tab=chat&agent=WH` | ai vào được màn | Danh sách agent · khung chat (nguồn ghi dưới câu trả lời) · câu hỏi gợi ý · "Agent này làm được" / "Giới hạn" |
| Giao việc | `ai-agents?tab=tasks` | `ai-tasks` | Việc agent phát hiện + việc nên làm + phụ trách + hạn + **điều kiện tự đóng** (mục 4d) |
| Luồng liên phòng | `ai-agents?tab=flow` | `ai-flow` (CEO, COO, FIN-IT) | 12 bước QT-SX-01 của 1 ngày (mục 4b) |
| Lịch chạy | `ai-agents?tab=schedule` | `ai-schedule` | Việc theo giờ của từng agent, lần chạy gần nhất, công tắc bật / tắt (quyền Sửa) |
| Nhật ký agent | `ai-agents?tab=log` | `ai-agent-log` | Mọi lời gọi công cụ: lúc, agent, công cụ, người hỏi, kết quả, thời gian xử lý, số dòng |
| Sử dụng & chi phí | `ai-agents?tab=usage` | `ai-cost` – **chỉ CEO, COO, FIN-IT** | Chi phí tháng (chọn tháng): % ngân sách, dự báo cuối tháng, tháng trước, bình quân / lượt, token; biểu đồ theo ngày + theo agent; bảng theo agent |
| Chi tiết agent | `ai-agents?code=WH` | `ai-agents` | Mục đích, công cụ (màn kiểm quyền), thư mục tri thức, lịch chạy, hoạt động gần đây |

Người không thuộc CEO / COO / FIN-IT **không thấy** nhóm Chi phí AI và tab Sử dụng & chi phí; gọi API chi phí bị từ chối (403). Màn hẹp (điện thoại): hàng robot vuốt ngang, các khung xếp dọc, thanh tab vuốt ngang.

## 3. Agent làm gì (đợt 1 – 4)

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
| HR | Chấm công ngày (chưa vào ca, muộn, vắng); **đề xuất đơn nghỉ / đổi ca / bổ sung chấm công** (`propose_leave`, `propose_shift_swap`, `propose_attendance_fix`) | 07:15 người chưa vào ca |
| PLAN (đợt 3) | Kế hoạch sản xuất 7 ngày, thực đơn tuần, nhu cầu / thiếu hụt nguyên liệu, giá vốn & lãi gộp; **đề xuất tính lại kế hoạch ngày** (`propose_production_plan` – như nút Tính lại kế hoạch), **đề xuất đổi món** trên thực đơn đã công bố (`propose_menu_change` – yêu cầu đổi món đi tiếp như web: tự duyệt khi đủ 4 điều kiện, còn lại chờ duyệt đổi món) | 20:40 tóm tắt thiếu hụt sau MRP |
| PKG (đợt 3) | Hộp / khay khách đang giữ, quá hạn trả, tỷ lệ thất thoát, lô vệ sinh trong ngày; **đề xuất ghi khách trả hộp** (`propose_container_return`), **ghi 1 bước vệ sinh khay** (`propose_cleaning_step`, đúng thứ tự 6 bước VS-KHAY), **ghi hộp thất thoát** (`propose_container_loss_charge`) | 16:00 khách giữ hộp quá hạn / vượt ngưỡng |
| CSKH (đợt 4) | **Khách hàng** hỏi qua Zalo OA DELI-CARE: đơn suất và thực đơn của **chính khách đó**, hướng dẫn chung (ghi chú công khai). Khách xác nhận đơn / báo số suất, báo thiếu suất, phản ánh → **đề xuất** chờ KD / CSKH / điều phối xác nhận; đổi món theo danh sách gợi ý | – |
| ACC (đợt 3) | **Chỉ đọc:** chứng từ kế toán tháng, đối chiếu AMIS, giá vốn & lãi gộp, hộp khay khách giữ. Không lập / sửa / xuất chứng từ, không thay MISA. Đề xuất duy nhất: ghi hộp thất thoát của khách vượt ngưỡng (`propose_container_loss_charge`) – bảng kê thanh toán tự tính phí theo cấu hình | Ngày 2 hằng tháng 08:30 tóm tắt lệch AMIS + khách vượt ngưỡng hộp |

Đợt 2 các agent cũ có thêm lệnh ghi: WH – lập phiếu nhập theo đơn mua (`propose_goods_receipt`), phiếu xuất cho lệnh SX (`propose_issue`), phiếu kiểm kê (`propose_stock_count`); KITCHEN – ghi bắt đầu / xong / nhiệt độ lõi / lưu mẫu của mẻ (`propose_batch_event`); QA – kết luận QC nhận hàng (`propose_qc_result`), giữ lô (`propose_hold_lot`).

Người xác nhận lệnh đợt 3: tính lại kế hoạch – quyền Sửa Kế hoạch sản xuất; đổi món – quyền Sửa Thực đơn tuần; trả hộp / thất thoát – quyền Thêm Sổ hộp khay (giao vận / điểm phát); bước vệ sinh – quyền Thêm màn Vệ sinh hộp / khay (bếp / QA).

**Lệnh ghi (2 pha):** agent chỉ **đề xuất** – không ghi gì. Đề xuất vào **Phê duyệt › Lệnh của agent** (hạn xác nhận 15 phút), hiện bản xem trước (dòng, số tiền, cảnh báo). Người xác nhận phải tự có quyền làm đúng thao tác đó trên web (vd lập phiếu nhập cần quyền Thêm Phiếu nhập kho). Xác nhận → hệ thống chạy **đúng thao tác web** dưới tên người xác nhận, mọi quy tắc nghiệp vụ áp lại (lỗi → không ghi, báo người xác nhận); chứng từ sau đó đi tiếp luồng duyệt như lập tay (vd đơn nghỉ chờ duyệt nghỉ, sửa đơn sau giờ chốt cần duyệt quá giờ chốt).

**Agent chỉ thấy những gì bạn được thấy:** mỗi lời hỏi kiểm **quyền của agent ∩ quyền của bạn** trên màn tương ứng (vd hỏi tồn kho cần quyền Xem Tồn kho). Không có quyền → agent trả lời "không có quyền xem …".

## 4. Bật / tắt agent

1. **Điều hành › Trung tâm Agent** (tab Tổng quan) → bấm robot / dòng agent → công tắc **Bật / Tắt** trên thẻ agent bên phải.
2. Tắt: hộp xác nhận → **Tắt agent**. Có hiệu lực **ngay** từ lời gọi sau (bot không trả lời bằng agent đó, lịch của agent dừng). Job lõi (khóa đơn, MRP, bản tin bot Điều hành…) vẫn chạy đúng giờ.
3. Bật lại: gạt công tắc – không cần xác nhận.
4. Thẻ ghi "máy chủ chưa có khóa agent": IT cần đặt biến môi trường `AGENT_KEY_<MÃ>` trên server (`app-delicare-agents/README.md`).

## 4b. Cảnh báo chủ động và Luồng liên phòng (AI-11, AI-21)

- **Cảnh báo chủ động:** khi có sự kiện (sự cố, cảnh báo, job xong, phản ánh…), agent nhận xét và đưa phương án rồi gửi **1 tin bot** (bot Vận hành; VIVI gửi bot Điều hành) tới người **có quyền Thêm** trên màn liên quan. Mỗi sự kiện chỉ gửi 1 lần. Người chưa liên kết bot không nhận được tin.
- **Tab Luồng liên phòng:** 12 bước quy trình QT-SX-01 của ngày đang chọn (‹ › đổi ngày, nút Hôm nay): 1 Nhận & chốt đơn · 2 Thực đơn · 3 Đổi món chờ khách · 4 Kế hoạch sản xuất · 5 Nguyên liệu & thiếu hụt · 6 Mua hàng (PO) · 7 Sản xuất · 8 An toàn thực phẩm · 9 Giao vận · 10 Thu hồi hộp khay · 11 Phản ánh khách · 12 Báo cáo cuối ngày. Trạng thái **tính từ dữ liệu** lúc mở màn: **Đạt** / **Đang chạy** / **Cần chú ý** / **Có vấn đề** / **Chờ dữ liệu** (vd bước 1 ngày mai chưa có đơn → Có vấn đề; có đơn chưa xác nhận → Cần chú ý; đơn đã khóa → Đạt kèm số suất; bước 4 đơn đã khóa mà chưa có kế hoạch → Có vấn đề).
- Bấm 1 bước → khung bên phải: ghi chú, **chuỗi xử lý** (sự kiện → agent đọc → đề xuất → người duyệt → kết quả → tin bot theo mã tương quan; việc đã giao; lần kiểm tra), nút **Mở màn …** (nếu bạn có quyền màn đó), **Hỏi agent**, **Xem việc đã sinh**.
- Bước **Có vấn đề / Cần chú ý** của hôm nay và ngày mai **tự sinh việc** ở tab Giao việc cho agent phòng đó (trừ bước 5, 8, 11 – đã có việc từ cảnh báo kho / ATTP / phản ánh).
- **Lịch chạy:** người có quyền Sửa Lịch chạy (CEO, COO, FIN-IT) bật / tắt từng việc ở cột Trạng thái; tệp `app-delicare-agents/schedules.json` được ghi lại, Hermes đọc lại khi sinh cron.

## 4d. Giao việc (AI-21, D-138 d)

- Mỗi thẻ việc: tiêu đề + mức (**Cần xử lý ngay** đỏ / **Cần chú ý** vàng), chi tiết, **Việc nên làm (agent đề xuất)**, Nguồn, Phụ trách (theo vai trò), Hạn, **Tự đóng khi …**, nhãn agent; nút **Mở chứng từ ↗** (màn nguồn), **Hỏi agent** (mở Chat đúng agent với câu hỏi sẵn), **Đã xử lý** (quyền Sửa Giao việc).
- Việc đến từ: (1) **cảnh báo đang mở** (nguyên liệu dưới định mức, lô cận hạn, lô tạm giữ, QC đầu vào chờ, nhiệt độ ngoài ngưỡng, phản ánh, ca thiếu người, khay sạch thiếu, chuyến chưa đủ hàng…), (2) **bước luồng kẹt**, (3) việc agent ghi ở kho tri thức 98 Công việc. **Chỉ hiện việc của màn bạn được Xem**; "Chỉ việc tôi phụ trách" = màn bạn có quyền Thêm.
- **Tự đóng:** nguồn hết điều kiện (vd phản ánh đã xử lý, tồn đã đủ) → việc chuyển sang **Đã xử lý hôm nay** với nhãn **Tự đóng**, không cần bấm. Nguồn có lại trong ngày → việc mở lại.
- **Đã xử lý:** ghi chú ngắn (đã làm gì) → lưu vào **kho tri thức › 98 Công việc › _so-viec › <ngày>** (người, giờ, ghi chú). Việc đã bấm Đã xử lý không mở lại trong ngày.
- **Yêu cầu duyệt không nằm ở Giao việc:** thẻ "N yêu cầu chờ anh / chị duyệt" dẫn sang **Phê duyệt**.

## 4c. Chi phí Trợ lý AI và chat trên web (AI-13, AI-14)

- **Chi phí Trợ lý AI tháng** (chỉ CEO / COO / FIN-IT): nhóm **Chi phí AI · tháng MM/yyyy** trên Tổng quan (đã dùng / ngân sách, thanh % có vạch mốc ngày, so tháng trước) + khung chi tiết (dự báo cuối tháng, tháng trước, bình quân / lượt, theo agent) + tab **Sử dụng & chi phí**. Chưa có lượt nào báo chi phí → "Chưa có dữ liệu chi phí" (không phải lỗi). Mỗi lượt trả lời Hermes báo token + chi phí (USD quy VND theo `Agent:UsdToVnd`); cùng một lượt báo 2 lần chỉ tính 1.
- **Ngân sách** `Agent:MonthlyBudget` (VND / tháng, IT đặt). Chạm **`Agent:CostAlertPct` % (mặc định 80 %)** và **100 %** → bot Điều hành gửi 1 tin "💰 Chi phí Trợ lý AI" tới người có quyền Thêm Trung tâm Agent (CEO, COO, FIN-IT) – mỗi ngưỡng mỗi tháng 1 lần. Chưa đặt ngân sách → thẻ ghi "Chưa đặt ngân sách", không cảnh báo.
- **Chat trên web (tab Chat với agent):** chọn agent ở cột trái (agent chưa bật bị mờ, không chọn được), bấm câu hỏi gợi ý hoặc gõ câu hỏi (Enter gửi, Shift+Enter xuống dòng). Nút **Chat với agent / Hỏi agent** ở Tổng quan, Giao việc, Luồng mở đúng agent. Máy chủ Hermes tắt → câu trả lời báo "⚠ …" bằng tiếng Việt. Lịch sử hiển thị giữ trong phiên trình duyệt. Agent dùng **quyền của bạn ∩ quyền agent** như bot; đề xuất ghi vẫn chờ người có quyền xác nhận ở Phê duyệt › Lệnh của agent.
- Máy chủ Hermes chạy 3 container (AI-15): **hermes-vivi** (VIVI, KNOW – bot Trợ lý), **hermes-van-hanh** (KITCHEN, QA, LOG, PKG, WH), **hermes-van-phong** (SALES, PLAN, PROC, HR, ACC, CSKH). Một nhóm lỗi không làm dừng nhóm khác; IT xem `app-delicare-agents/README.md`.

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
| Lịch chạy "chưa chạy" | Hermes chưa chạy / chưa nạp lịch | IT chạy `tools/agent-eval/gen-cron.mjs --group <nhóm>` trong từng container (app-delicare-agents/README.md) |
| Chat web báo "chưa kết nối máy chủ Hermes" | Chưa đặt `Agent:HermesApiUrl` | IT đặt URL API server Hermes (Docker đặt sẵn `http://hermes-vivi:8642/v1`) – trong lúc chờ vẫn hỏi được qua bot Trợ lý |
| Chat web báo "đang bận / không phản hồi" | Container Hermes của agent đó dừng / quá `Agent:ChatTimeoutSeconds` | Thử lại sau ít phút; IT xem `docker compose logs hermes-<nhóm>` |
| Agent trả lời "Phiên chat trên web hết hạn" | Vé người hỏi quá 10 phút (câu trả lời dài, gọi nhiều công cụ) | Đóng ngăn chat, mở lại rồi hỏi lại |
| Không có tin cảnh báo chi phí | Chưa đặt `Agent:MonthlyBudget` hoặc người nhận chưa liên kết bot Điều hành | IT đặt ngân sách; CEO / FIN-IT liên kết bot |

## 8. Bot Zalo cho khách hàng (AI-19)

- **Kinh doanh cấp mã:** Khách 360 › Tổng quan › Người liên hệ → nút **Mã Zalo** (quyền Sửa Khách hàng) → gửi mã cho người liên hệ (email / điện thoại). Khách quan tâm Zalo OA DELI-CARE rồi nhắn mã vào OA (mã dùng 1 lần, hạn `Zalo:LinkCodeHours` = 72 giờ). Cấp mã mới khi khách đổi số Zalo – Zalo cũ tự bị thu hồi.
- **Khách dùng bot (gõ số):** 1 đơn 7 ngày · 2 thực đơn theo đơn · 3 <số đơn> xác nhận (3 <số đơn> TRUA=120 đổi số suất) · 4 <số suất> báo thiếu đơn hôm nay · 5 <nội dung> phản ánh · 6 <số đơn> đổi món (chọn món → chọn món thay trong gợi ý). Câu hỏi tự do → Trợ lý CSKH trả lời theo đơn của khách (khi máy chủ Hermes đã cấu hình).
- **Khách không tự ghi:** xác nhận / đổi số suất, thiếu suất, phản ánh vào **Phê duyệt › Lệnh của agent** (agent CSKH, hạn 12 giờ). Người xác nhận phải có quyền thao tác web tương ứng: đơn – Sửa Đơn hàng (KD); thiếu suất / phản ánh – Thêm Phản ánh (CSKH, điều phối điểm phát, QA). Xác nhận / từ chối → bot nhắn lại khách. Đổi món đi luồng đổi món như web (tự duyệt khi đủ 4 điều kiện, còn lại chờ duyệt).
- Khách chỉ thấy dữ liệu **của chính mình** – số đơn của khách khác báo "không tìm thấy". Tài khoản khách (CUSTOMER) không đăng nhập web được.
- Bật kênh: IT cấu hình Zalo OA (app id, app secret, OA secret key, token làm mới lần đầu ở .env – `app-delicare-agents/README.md`), bật kênh ZALO_OA + bot ZALO_KH ở Tích hợp › Kênh, `ZALO_ENABLED=true` cho messaging.
