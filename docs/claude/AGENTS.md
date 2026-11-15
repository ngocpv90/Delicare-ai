# AGENTS – Hermes và các agent phòng ban

**Nguyên tắc:**
1. Agent không truy cập DB, Sheets hay Obsidian trực tiếp. Mọi việc đi qua ToolGateway (MCP).
2. Agent **đọc** thì trả lời ngay. Agent **ghi** chỉ qua `propose_*`, có người xác nhận mới `commit`.
3. **Hạn chót do job của lõi chạy.** Agent chỉ nhắc, tóm tắt, giải thích và xin xác nhận. Nếu agent lỗi thì nghiệp vụ vẫn đúng giờ.
4. Mỗi agent có tài khoản dịch vụ riêng (`User` + `UserGroup` = `AGENT_<CODE>`) và danh sách công cụ riêng.
5. Câu trả lời cho người dùng: tiếng Việt, ngắn, có số liệu và mã chứng từ, có link Mission Control khi cần duyệt.

## 1. Danh sách agent

| Mã | Agent | Phục vụ | Công cụ đọc (ví dụ) | Công cụ ghi (propose → ai xác nhận) |
|---|---|---|---|---|
| VIVI | Điều phối trung tâm | Ban GĐ, mọi người | `bao_cao_dieu_hanh`, `kpi_khoang`, `su_co_ngay`, `ds_cho_duyet` + định tuyến câu hỏi | không ghi trực tiếp; chuyển cho agent phòng ban |
| SALES | Đơn hàng / Kinh doanh | KD, TP KD | `get_don_hang_ngay`, `don_hang_khoang`, `danh_muc`, hợp đồng | `propose_order_change` → TP KD (sau 20h thì thêm duyệt cutoff) |
| PLAN | Kế hoạch sản xuất | TP vận hành, trưởng bếp | `ke_hoach_ngay`, `tinh_nguyen_lieu`, năng lực dây chuyền | `propose_production_plan` → trưởng bếp |
| PROC | Thu mua | Mua hàng | `du_bao_thieu_hut`, NCC, giá | `propose_purchase_order` → TP vận hành / GĐ theo giá trị |
| WH | Kho | Thủ kho | `get_ton_kho`, `get_lo_can_date`, `truy_xuat_lo` | `propose_goods_receipt`, `propose_issue`, `propose_stock_count` → thủ kho / duyệt khi lệch > 2% |
| KITCHEN | Bếp | Trưởng ca, tổ trưởng | `tien_do_bep`, `tien_do_bep_chi_tiet`, `thuc_don_tuan` | `propose_batch_event` (bắt đầu / xong / nhiệt độ / lưu mẫu) → trưởng ca |
| QA | ATTP và Truy xuất | QA, trưởng bếp | `truy_xuat_lo`, `truy_xuat_me`, checklist, nhiệt độ | `propose_qc_result`, `propose_hold_lot` → QA lead |
| LOG | Giao vận | Điều phối GV, tài xế | `giao_van_ngay`, `giao_van_truc_tiep` | `propose_route`, `propose_delivery_event` → điều phối GV |
| PKG | Hộp / khay và Vệ sinh | Tổ vệ sinh, KT | `hop_khay`, lô vệ sinh | `propose_container_return`, `propose_cleaning_step` → tổ trưởng VS |
| HR | Nhân sự | NS, trưởng bộ phận | `cham_cong_ngay`, `chua_vao_ca`, `cong_thang` | `propose_leave`, `propose_shift_swap`, `propose_attendance_fix` → trưởng bộ phận |
| ACC | Kế toán | Kế toán | doanh thu, chi phí NL, giá thành suất, phí thất thoát hộp | `propose_container_loss_charge` → KT trưởng |
| CSKH | CSKH – Zalo | Khách hàng | đơn và thực đơn **của chính khách đó** | `propose_portion_confirm`, `propose_shortage_report`, `propose_complaint` → KD / điều phối (khách không tự commit) |

**Bổ sung R2 (theo sơ đồ tổ chức – ORG_STRUCTURE.md):** POS (điểm phát suất / bán lẻ – bộ phận 1.5) và MNT (thiết bị / bảo trì – 1.6), khi có bảng tương ứng (GAP-POS-01, GAP-MNT-01). ACC chỉ đọc số liệu hệ thống và xuất cho MISA, không thay MISA (D-23).

Tên công cụ đọc giữ theo hàm cũ để Hermes không phải học lại (LEGACY_GATEWAY.md). Công cụ mới đặt tên `snake_case` tiếng Anh.

## 2. Lịch chạy

| Giờ | Job lõi (Hangfire – quyết định) | Agent (thông báo / xin xác nhận) |
|---|---|---|
| 19:00 | `RemindUnconfirmedOrdersJob`: lập danh sách khách chưa chốt cho D+1 | CSKH nhắn Zalo cho khách; SALES nhắn Telegram cho nhân viên KD phụ trách |
| 20:00 | `LockOrdersJob`: khóa đơn D+1 (Confirmed → Locked) | SALES báo số đơn đã khóa và đơn chưa chốt |
| 20:30 | `MrpJob`: tính MaterialRequirement + thiếu hụt | PLAN / PROC tóm tắt thiếu hụt |
| 21:00 | `PurchaseProposalJob`: tạo PurchaseRequest nháp + gợi ý NCC | PROC gửi đề xuất PO kèm nút xác nhận |
| 05:30 | `StockMorningSnapshotJob`: tồn, cận date, lô bị chặn | WH gửi báo cáo kho |
| 07:00 | `MorningBriefJob`: số liệu bản tin sáng | VIVI gửi bản tin sáng cho Ban GĐ; ghi vào Obsidian 10 và 96 |
| 17:00 | `EndOfDayJob`: tổng hợp cuối ngày | VIVI gửi báo cáo cuối ngày |
| 22:00 | `KpiDailySnapshotJob` + Obsidian export | – |
| mỗi 5′ | `ComplaintSlaWatchJob`, `TemperatureAlertJob`, `ApprovalExpiryJob`, `OutboundMessageWorker` | agent liên quan nhận sự kiện |

## 3. Luồng liên phòng (theo sự kiện)
1. **Chốt đơn → kế hoạch → thu mua / kho:** `OrdersLocked` → MrpJob → `ShortageDetected` → PROC đề xuất PO → duyệt → `PurchaseOrderApproved` → WH chờ nhận hàng.
2. **Khách báo thiếu suất → bếp → giao vận:** CSKH `propose_shortage_report` → KD xác nhận → `ShortageReported` → KITCHEN được yêu cầu nấu bù (tạo ProductionOrder bổ sung) → LOG xếp chuyến bù → `DeliveryCompleted` → CSKH báo khách.
3. **Hộp thất thoát → khách → kế toán:** `PackagingLossExceeded` (từ PackagingLedger) → CSKH thông báo khách → ACC `propose_container_loss_charge` → KT trưởng duyệt.
4. **Sự cố ATTP:** nhiệt độ lõi < 70°C hoặc giao < 60°C → `FoodSafetyAlert` → QA giữ lô / mẻ → VIVI báo Ban GĐ.

## 4. Cấu hình agent trong repo
Thư mục `agents/` (tạo ở T43): mỗi agent một file `agents/<CODE>.md` gồm vai trò, phạm vi, giọng văn, danh sách tool được dùng, những việc **không** được làm, và mẫu câu trả lời. Có thêm `agents/hermes.config.example.yaml` để anh nạp vào Hermes (`~/.hermes`). Không chứa token.
