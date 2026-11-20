# 90 – Chênh lệch tài liệu – code

Danh sách chỗ **code chưa có** hoặc **khác tài liệu** phát hiện khi viết hướng dẫn sử dụng (rà soát code ngày 19/11/2026). Mỗi mục là đầu việc cho Claude Code (task **UG-1** trong `docs/claude/ROADMAP.md`).

- **Ưu tiên:** Cao = chặn nghiệp vụ / người dùng làm sai; TB = thiếu chức năng có đường vòng; Thấp = chữ, tài liệu.
- **Ai làm:** CC-code = Claude Code sửa code · CC-doc = Claude Code sửa tài liệu · **Anh** = cần anh quyết định trước (ghi D-63 trở đi).
- Đường dẫn: `UI` = `app-delicare-client/src/assets/delicare/js`, `BE` = `AppDelicare`.
- Khi sửa xong một mục: cập nhật lại đoạn tương ứng trong hướng dẫn (file ghi trong cột "Ảnh hưởng HDSD") và đánh dấu ✅ ở đây.

## 1. Bảng tổng hợp

| # | Ưu tiên | Ai làm | Tóm tắt | Ảnh hưởng HDSD |
|---|---|---|---|---|
| ✅ C-01 | Cao | CC-code | Không có nút "Yêu cầu sửa đơn sau giờ chốt" | 02 §4.6, 12 §3 |
| ✅ C-02 | Cao | CC-code | Web chưa có nơi lấy mã liên kết Telegram | 12 §7, 20 §2.2 |
| ✅ C-03 | TB | CC-code / CC-doc | Màn nhập dữ liệu gốc (`sys-import`) không vào được | 01 |
| ✅ C-04 | Thấp | CC-doc | REVIEW ghi sai về `Common/GetHistory` | – |
| ✅ C-05 | Thấp | CC-code | Không có nút "Chốt đơn theo ngày ăn" (`Order/LockByMealDate`) | 02 |
| ✅ C-06 | Cao | CC-code | Tạo người dùng: không gán được nhóm quyền, không thấy mật khẩu tạm | 01 §4 |
| ✅ C-07 | Cao | CC-code | Bot "✅ Xong mẻ" không ghi sản lượng → mẻ kẹt ở Chờ QC | 07 §7, 20 §9 |
| ✅ C-08 | Cao | CC-code | QC đầu vào: HOLD và hàng đông lạnh nhận có điều kiện không lưu được | 06 §4.1 |
| ✅ C-09 | TB | CC-code | Nhiệt độ lõi: nhãn "≥ 75°C", thiếu ô hành động khắc phục | 07 §4.2 |
| ✅ C-10 | TB | CC-code + CC-doc | Bảng trạng thái giao diện / STATUS_CATALOG thiếu, lệch nhãn | 91 |
| ✅ C-11 | TB | CC-code | Hộp phê duyệt không hiện chứng từ của 4 loại yêu cầu | 12 §6 |
| ✅ C-12 | TB | CC-code | Không thêm dòng PR tay, không sửa dòng PO trên web | 04 |
| ✅ C-13 | Thấp | CC-code / CC-doc | Màn tích hợp `int-*` bị ẩn nhưng cảnh báo vẫn trỏ tới | 01 |
| ✅ C-14 | Thấp | CC-doc | "Gửi NCC" chỉ đổi trạng thái, chưa gửi email / Zalo | 04 |
| ✅ C-15 | TB | CC-code | Công đoạn mẻ có API nhưng không có nút | 07, 08 |
| ✅ C-16 | TB | CC-code | Nút Duyệt / Khóa hiện cho người không có quyền Duyệt | 03, 12 |
| ✅ C-17 | TB | **Anh** | Tổ đóng gói (OPS-KIT) chỉ có quyền xem màn đóng gói | 08 §1 |
| ✅ C-18 | TB | **Anh** | Nhiều nhóm không vào được màn chấm công web | 11 §6.2 |
| ✅ C-19 | TB | **Anh** | Quyền duyệt trong seed khác BOT_TELEGRAM / ORG_STRUCTURE | 12 §3 |
| ✅ C-20 | TB | CC-code | Người dùng thường chuyển được sang dữ liệu DEMO | 00, 13 §4.3 |
| ✅ C-21 | Thấp | CC-code | Chữ trên bot sai (nút Mở bot Điều hành, Trợ giúp, gợi ý Truy xuất) | 13 §7 |
| ✅ C-22 | TB | **Anh** + CC-doc | Nhiều chức năng bot trong BOT_TELEGRAM §3.2/3.3 chưa có | 20 |
| ✅ C-23 | TB | **Anh** | Nhân sự (FIN-HR) chỉ xem hồ sơ nhân viên, không sửa SĐT / tạo mã bot được | 11 §2, 20 §2 |

## 2. Chi tiết

### C-01 – Yêu cầu sửa đơn sau giờ chốt không có nút (Cao, CC-code) · ✅ **Đã sửa 2026-11-19** (nhánh feat/MA-1-code-rules – nút chỉ hiện khi máy chủ cho phép)
- **Tài liệu:** BUSINESS_RULES / REVIEW_BUSINESS_FLOWS: sau 20:00 D-1 KD gửi yêu cầu đổi số lượng, CEO duyệt (ORDER_CHANGE_AFTER_CUTOFF).
- **Code:** API có: `BE/AppDelicare.WebApi/Controllers/OrderController.cs:304` `RequestChange`; `BE/AppDelicare.Services/Implement/OrderService.cs:981` trả `allowedActions` có `requestChange`. Giao diện `UI/modules/sales.js:152` chỉ hiện dòng chữ, không có nút.
- **Đề xuất:** thêm nút **Yêu cầu sửa sau chốt** trong ngăn chi tiết đơn (form: số suất mới từng dòng + lý do bắt buộc) gọi `Order/RequestChange`; báo "đã gửi duyệt XN-…".

### C-02 – Chưa có nơi lấy mã liên kết Telegram trên web (Cao, CC-code) · ✅ **Đã sửa 2026-11-19** (FE-E.1: menu người dùng → Liên kết Telegram – GetMine / Tạo mã + QR / Gỡ; nút Tạo mã bot ở hồ sơ nhân viên)
- **Tài liệu:** BOT_TELEGRAM §2: người duyệt lấy mã ở web "Hồ sơ cá nhân → Liên kết Telegram"; NS tạo mã cho nhân viên không khớp SĐT.
- **Code:** API `BE/AppDelicare.WebApi/Controllers/TelegramLinkController.cs` (`CreateCode`, `CreateCodeForEmployee` – quyền Sửa `adm-employee`, `GetMine`, `Unlink`) – **không có chỗ nào trên giao diện gọi**. Bot vẫn nhắc người dùng vào màn này (`BE/AppDelicare.Messaging/Audiences/Internal/Chung/OnboardingActions.cs:130`, `ApprovalActions.cs:378`).
- **Hệ quả:** không ai duyệt được trên bot; nhân viên sai SĐT không liên kết được.
- **Đề xuất:** (1) menu người dùng (góc phải) thêm **Liên kết Telegram**: hiện trạng thái (`GetMine`), nút **Tạo mã** (hiện mã + hạn 15′ + link `t.me/<bot>?start=<mã>` + QR), **Hủy liên kết**; (2) `adm-employee` thêm nút **Tạo mã liên kết bot** trên dòng (mã 72 giờ).

### C-03 – Màn nhập dữ liệu gốc không có đường vào (TB) · ✅ **Đã sửa 2026-11-19** (màn engine sys-import: file mẫu, nạp file, bảng dòng lỗi; HDSD 01 §4.8b)
- **Tài liệu:** REVIEW_BUSINESS_FLOWS ghi ✅ cho nhập file dữ liệu gốc (sys-import / master-import).
- **Code:** `app-delicare-client/src/app/app.routes.ts` chỉ có `dang-nhap`, `khong-co-quyen-truy-cap` và shell `:screenId`; module Angular cũ (main-routing có sys-import / master-import) không được nạp.
- **Đề xuất:** hoặc nối lại route / đăng ký màn trong engine, hoặc sửa REVIEW thành "chưa dùng được" – **CC hỏi anh** chọn hướng nếu việc nối lại > 1 ngày.

### C-04 – REVIEW ghi sai về lịch sử xử lý (Thấp, CC-doc) · ✅ **Đã sửa 2026-11-19** (REVIEW đã sửa – GetHistory và GetStatusSummary đều đã gọi)
- **Tài liệu:** REVIEW ghi `Common/GetHistory` chưa được gọi.
- **Code:** `UI/core/ui.js:175-178` có gọi (tab **Lịch sử xử lý**). `Common/GetStatusSummary` mới là API chưa gọi.
- **Đề xuất:** sửa REVIEW.

### C-05 – Chốt đơn theo ngày ăn không có nút (Thấp) · ✅ **Đã sửa 2026-11-19** (nút Chốt đơn theo ngày ở crm-order)
- **Code:** `OrderController.cs:246` `LockByMealDate`; giao diện không gọi (chốt tự động bằng job 20:00).
- **Đề xuất:** thêm nút **Chốt đơn ngày …** ở `crm-order` (chỉ quản lý, xác nhận 2 bước) hoặc ghi rõ trong tài liệu là chỉ dùng cho job.

### C-06 – Tạo người dùng thiếu gán nhóm và mật khẩu tạm (Cao, CC-code) · ✅ **Đã sửa 2026-11-19** (nhánh feat/MA-1-code-rules – kèm sửa lỗi Sửa người dùng làm mất nhóm)
- **Code:** `UserService.InsertAccount` nhận `ListUserGroupId` nhưng form `adm-user` không có ô chọn nhóm; mật khẩu tạm sinh ở máy chủ không hiển thị → quản trị phải bấm **Đặt lại mật khẩu** ngay sau khi tạo.
- **Đề xuất:** form tạo người dùng thêm ô **Nhóm quyền** (chọn nhiều); sau khi tạo hiện mật khẩu tạm một lần (có nút sao chép); trên dòng thêm **Gán nhóm**.

### C-07 – Bot "Xong mẻ" không ghi sản lượng (Cao, CC-code) · ✅ **Đã sửa 2026-11-19** (nhánh feat/MA-1-code-rules – bot ghi sản lượng + nút web)
- **Code:** bot gọi `api/ProductionBatch/Finish` (`BE/AppDelicare.Messaging/Audiences/Internal/VanHanh/BepActions.cs:145,175`) – chỉ ghi `ActualQuantity`, không tạo ProductionOutput. Cho Đạt / đóng gói kiểm `SafetyBlockersAsync(... needOutput: true)` (`BE/AppDelicare.Services/Implement/ProductionBatchService.cs:616,697,850`) → *"Chưa ghi sản lượng mẻ"*. Web ở trạng thái Chờ QC không có nút ghi sản lượng → **kẹt**.
- **Đề xuất:** bot gọi thêm `RecordOutput` sau `Finish` (như web `UI/modules/production.js:110`); và thêm nút **Ghi sản lượng** cho mẻ Chờ QC chưa có sản lượng trên web.

### C-08 – QC đầu vào: HOLD và nhận có điều kiện hàng đông lạnh (Cao, CC-code) · ✅ **Đã sửa 2026-11-19** (nhánh feat/MA-1-code-rules – Số lượng đạt + Xác nhận không rã đông)
- **Code:** máy chủ `BE/AppDelicare.Services/Implement/QcInspectionService.cs:268-273`: HOLD = DAT_MOT_PHAN cần `AcceptedQuantity` (> 0 và < số nhận); `:286` hàng đông lạnh trong vùng có điều kiện cần `NoThawConfirmed`. Giao diện `UI/modules/qc.js:42` chỉ gửi `goodsReceiptLineId, result, temperature, weight, appearance, packaging, remark` → HOLD luôn lỗi *"DAT_MOT_PHAN cần số lượng đạt > 0 và < số nhận"*, đông lạnh có điều kiện lỗi *QcNoThawConfirmRequired*.
- **Đề xuất:** hộp kết luận thêm ô **Số lượng đạt** (hiện khi chọn HOLD) và ô tick **Xác nhận không rã đông** (hiện với hàng FROZEN trong vùng −18 … −15 °C).

### C-09 – Nhiệt độ lõi: nhãn sai, thiếu khắc phục (TB, CC-code) · ✅ **Đã sửa 2026-11-19** (ngưỡng từ máy chủ, ô khắc phục web + bot hỏi khắc phục)
- **Tài liệu / cấu hình:** BUSINESS_RULES và `BusinessRules:CoreTempMinC = 70`.
- **Code:** `UI/modules/production.js:110` ghi "(≥ 75°C)", giá trị mặc định 82.5; không có ô **Hành động khắc phục**, trong khi máy chủ đòi (`ConstantHelper.cs:376` *CoreTempCorrectiveRequired*). Bot nút nhiệt độ cũng không hỏi khắc phục.
- **Đề xuất:** nhãn lấy ngưỡng từ máy chủ; bỏ giá trị mặc định; thêm ô khắc phục (hiện khi < ngưỡng) ở web và bước hỏi khắc phục trên bot.

### C-10 – Danh mục trạng thái giao diện và STATUS_CATALOG lệch code (TB) · ✅ **Đã sửa 2026-11-19** (StatusCatalogEffective theo *Status.cs, GetStatuses + giao diện chế độ thật đọc từ máy chủ; 91 §1 cập nhật. STATUS_CATALOG.md (docs/reference) chưa sửa – anh cập nhật)
- **Code giao diện** `UI/data/catalog.js` thiếu: PO **Chờ duyệt**, PR **Hủy**, phiếu nhập **Hủy**, mẻ **Hủy**, lệnh đóng gói **Hủy**, **DeliveryOrder** (không có định nghĩa), **CleaningBatch** (không có), thực hiện giao **CANCELLED**. Lệch nhãn: READY "Sẵn sàng" (máy chủ "Chờ xếp hàng"), DISPATCHED "Đã xuất phát" (máy chủ "Đang giao"), nhãn lệnh đóng gói.
- **Tài liệu** `docs/STATUS_CATALOG.md` cũ (vd phiếu nhập có Nháp nhưng code không có).
- **Đề xuất:** đồng bộ `catalog.js` theo các lớp `BE/AppDelicare.Model/Helper/*Status.cs`; cập nhật STATUS_CATALOG.md; lấy `91_PHU_LUC.md` §1 làm chuẩn (theo máy chủ).

### C-11 – Hộp phê duyệt thiếu 4 loại (TB, CC-code) · ✅ **Đã sửa 2026-11-19** (4 loại + bảng đề xuất sửa đơn)
- **Code:** `UI/modules/hr.js:257` bảng `BT` chỉ có PURCHASE_REQUEST, LEAVE_REQUEST, SHIFT_SWAP, RECIPE_VERSION, STOCK_COUNT, CONTRACT, MENU_CHANGE; thiếu **PURCHASE_ORDER, ORDER_DEVIATION, ORDER_CHANGE_AFTER_CUTOFF, STOCK_ADJUSTMENT** → cột "Bản ghi nghiệp vụ" hiện "–", không có liên kết mở chứng từ.
- **Đề xuất:** bổ sung 4 loại (bảng, cột trạng thái, màn mở: `scm-po`, `crm-order`, `crm-order`, `scm-inv-balance`/điều chỉnh) và hiện nội dung đề xuất (số mới) cho ORDER_CHANGE_AFTER_CUTOFF.

### C-12 – Dòng đề nghị mua / đơn mua (TB, CC-code) · ✅ **Đã sửa 2026-11-19** (Sửa dòng nguyên liệu (PR Nháp); PO Nháp đã có Sửa dòng hàng)
- **Code:** PR tạo tay chỉ tạo đầu phiếu, màn dòng `scm-pr-line` bị ẩn; API sửa dòng PO Nháp có nhưng giao diện không gọi.
- **Đề xuất:** ngăn chi tiết PR Nháp / PO Nháp thêm bảng dòng sửa được (nguyên liệu, số lượng, đơn vị, đơn giá, ngày cần), lưu qua API dòng.

### C-13 – Màn tích hợp bị ẩn (Thấp) · ✅ **Đã sửa 2026-11-19** (int-* hiện lại theo ma trận quyền (FE-E.3); siết quyền – D-71 (làm theo đề xuất, script chờ anh chạy))
- **Code:** `int-channel, int-bot, int-identity, int-inbound, int-outbound, int-log` thuộc HIDDEN_LIVE; cảnh báo OUTBOUND_FAILED vẫn trỏ `#/int-outbound`. REVIEW ghi xem được tin vào / ra.
- **Đề xuất:** hoặc mở `int-outbound`, `int-log` (chỉ xem) cho FIN-IT, hoặc đổi đích cảnh báo sang `adm-log`; sửa REVIEW.

### C-14 – "Gửi NCC" chưa gửi thật (Thấp, CC-doc) · ✅ **Đã sửa 2026-11-19** (REVIEW ghi rõ chỉ đổi trạng thái, gửi email / Zalo là việc sau)
- **Code:** nút chỉ chuyển PO sang **Đã gửi NCC**; gửi email / Zalo (Catalog 113) chưa làm.
- **Đề xuất:** ghi rõ trong REVIEW / ROADMAP là việc sau; HDSD đã ghi "chỉ đánh dấu".

### C-15 – Công đoạn mẻ không có nút (TB, CC-code) · ✅ **Đã sửa 2026-11-19** (tab Công đoạn trên web; nút ▶️ Bắt đầu / ⏹ Xong công đoạn trên thẻ mẻ ở bot `/me`)
- **Code:** `BE/AppDelicare.WebApi/Controllers/ProductionBatchController.cs:395` `StartStage`, `FinishStage`; không có nút trên web / bot. Nếu mẻ có công đoạn bắt buộc, máy chủ chặn "phải xong công đoạn Nấu / Chia suất…".
- **Đề xuất:** tab công đoạn trong ngăn mẻ: mỗi công đoạn nút **Bắt đầu / Xong**; bot `/me` thêm nút công đoạn hiện tại.

### C-16 – Nút duyệt hiện cho người không có quyền (TB, CC-code) · ✅ **Đã sửa 2026-11-19** (Store.canApprove)
- **Code:** `prod-plan` nút **Duyệt** (`ProductionPlan/Approve` cần quyền Duyệt `prod-plan`), `menu-menu` **Duyệt** (`Menu/Approve`), `menu-recipe` **Khóa & áp dụng** (`Recipe/Lock`) hiện cho mọi người có quyền vào màn → OPS-PLAN bấm bị báo lỗi quyền.
- **Đề xuất:** ẩn nút khi `Store.perm(id).approve` false (hoặc theo `allowedActions` từ máy chủ).

### C-17 – Quyền màn đóng gói (TB, Anh quyết định) · ✅ **Đã xử lý 2026-11-19** (D-69 – OPS-KIT Thêm + Sửa 2 màn đóng gói (seed + DB/scripts/UG1_D69_permissions.sql))
- **Seed** `UserRole_seed.sql`: quyền sửa `log-*` chỉ có OPS-LOG, OPS-POS, SAL-CS; OPS-KIT (gồm tổ Chia suất / Đóng gói theo ORG_STRUCTURE) chỉ xem `log-packing-order`, `log-packing-result`.
- **Đề xuất em:** cấp OPS-KIT quyền Truy cập + Thêm + Sửa trên `log-packing-order`, `log-packing-result` (seed bổ sung, không đổi schema). Cần anh đồng ý → ghi D-63.

### C-18 – Nhóm không vào được màn chấm công web (TB, Anh quyết định) · ✅ **Đã xử lý 2026-11-19** (D-69 – tự chấm công / tạo đơn nghỉ / đổi ca trên màn Việc của tôi, không cấp quyền hr-* (quyền Thêm = lập hộ, Truy cập = xem của mọi người))
- **Seed:** OPS-SCM, OPS-LOG, OPS-POS, SAL-SALES, SAL-CON, SAL-CS, QA, OPS-PLAN, OPS-MNT không có `hr-attendance`, `hr-shift-assign`, `hr-leave`, `hr-swap` (một số không có `wf-inbox`).
- **Đề xuất em:** cấp mọi nhóm quyền **Truy cập** `hr-attendance` (tự chấm công), **Truy cập + Thêm** `hr-leave`, `hr-swap` (tự lập đơn) – máy chủ đã giới hạn "chỉ của mình" khi không có quyền Thêm cho người khác. Cần anh đồng ý.

### C-19 – Quyền duyệt seed khác tài liệu (TB, Anh quyết định) · ✅ **Đã xử lý 2026-11-19** (D-69 – giữ seed, sửa BOT_TELEGRAM §3.3)
- **Tài liệu** BOT_TELEGRAM §3.3 / ORG_STRUCTURE: bếp trưởng duyệt kế hoạch SX / nghỉ / đổi ca tổ bếp; TP KD duyệt sửa sau chốt / lệch / hợp đồng; có duyệt "lịch xe"; QA "duyệt giữ / thả lô".
- **Seed:** OPS-KIT không có mục duyệt nào; sửa sau chốt / lệch / hợp đồng chỉ CEO; không có loại duyệt "lịch xe"; giữ / thả lô là thao tác trực tiếp trên `scm-lot`, không qua duyệt.
- **Đề xuất em:** (1) cấp bếp trưởng (cần nhóm riêng OPS-KIT-HEAD hoặc gán CEO… – anh chọn) mục `hr-leave`, `hr-swap`; (2) cấp TP KD (nhóm SAL-HEAD) mục `crm-order`; giữ CEO cho `crm-contract`; (3) sửa tài liệu: bỏ "duyệt lịch xe", đổi "duyệt giữ / thả lô" thành thao tác QA. Cần anh quyết.

### C-20 – Chuyển sang dữ liệu DEMO khi đang dùng thật (TB, CC-code) · ✅ **Đã sửa 2026-11-19** (chuyển DEMO chỉ quản trị)
- **Code:** hộp **Hướng dẫn sử dụng** (`UI/core/app.js:369`) cho mọi người bấm **Chuyển sang dữ liệu DEMO**; `flow-e2e` hiện nút **Bắt đầu demo từ bước 1** (`UI/modules/dashboard.js:139`) cả ở chế độ thật.
- **Đề xuất:** chỉ quản trị thấy nút chuyển DEMO; ẩn nút demo của `flow-e2e` khi `Store.isLive`.

### C-21 – Chữ trên bot (Thấp, CC-code) · ✅ **Đã sửa 2026-11-19** (chữ bot + thông báo xác nhận ca)
- `OnboardingActions.cs:144`: nút **Mở bot Điều hành** dùng `?start=CHAMCONG` (mở chấm công) – nên là `?start=` trống.
- `HelpActions.cs:62`: còn câu cũ "Có thể vào ca (chấm công bằng vị trí – bản tới)" dù chấm công vị trí đã có.
- `DieuHanhActions.cs:31`: gợi ý Truy xuất ghi `DH-…` – số đơn hàng là `PH-…`.
- `ConstantHelper` *HrConfirmDone* ghi "chưa gửi thông báo cho nhân viên – tích hợp bot để sau" – kiểm lại vì đã có thông báo SHIFT_CONFIRMED.

### C-22 – Chức năng bot trong tài liệu chưa có (TB, Anh + CC-doc) · ✅ **Đã làm hết 2026-11-20** (anh chọn cả 4 nhóm – D-72; ĐỢT BOT-3: 27 chức năng bot)
BOT_TELEGRAM §3.2 / §3.3 liệt kê nhưng code chưa có:
- **Bếp:** chia suất / nấu thêm, nguyên liệu của ca, ai chưa vào **tổ tôi**, chấm hộ cho tổ trưởng, truy xuất mẻ, ghi lô vệ sinh (chỉ xem qua QR CLEAN).
- **QA:** truy xuất lô / mẻ.
- **Giao vận:** điều phối chuyến trực tiếp, lịch xe, đổi xe, hộp khay hôm nay.
- **Điểm phát:** nhận hàng / ký nhận trên bot.
- **Kho:** thiếu hụt, truy xuất lô.
- **Kế hoạch:** lập lại kế hoạch, đề xuất đổi món.
- **Kinh doanh:** thực đơn tuần, đổi món, báo thiếu suất, việc tôi đã báo.
- **Kế toán:** công tháng. **Nhân sự:** nhập file máy chấm công, công tháng, đơn đang chờ. **IT:** `/chatid`. **Điều hành:** báo cáo tuần / tháng.
- **Đề xuất:** anh chọn mục nào làm tiếp (đưa vào ĐỢT BOT trong ROADMAP), phần còn lại đánh dấu "kế hoạch" trong BOT_TELEGRAM.md để tài liệu không mô tả như đã có.

### C-23 – Nhân sự không sửa được hồ sơ nhân viên (TB, Anh quyết định) · ✅ **Đã xử lý 2026-11-19** (D-69 – FIN-HR Thêm + Sửa adm-employee)
- **Seed:** FIN-HR chỉ có quyền **xem** `adm-employee`; quyền sửa `adm-*` chỉ FIN-IT. Trong khi đó SĐT hồ sơ là khóa liên kết bot, và `TelegramLink/CreateCodeForEmployee` cần quyền **Sửa** `adm-employee`.
- **Đề xuất em:** cấp FIN-HR Truy cập + Thêm + Sửa trên `adm-employee` (không cấp `adm-user`, `adm-permission`). Cần anh đồng ý.
