# BOT_TELEGRAM – Tính năng, trải nghiệm và thông báo của bot Telegram nội bộ

> **Trạng thái:** ✅ **anh chốt 2026-11-18 (D-62).** Kiến trúc: `docs/claude/BOT_SOURCE_ARCHITECTURE.md`. Task: ROADMAP mục "ĐỢT BOT".
> Mục ghi **PROPOSED** là giá trị mặc định em đề xuất (cấu hình được) – Claude Code làm theo, BA/PO chỉnh khi UAT.
> Nguồn tham khảo: bản Apps Script v2.11 (48 chức năng của 3 bot VH / KHO / NS) – giữ lệnh và nhãn quen thuộc, đổi cách chia bot và thêm các cơ chế ở §1.

## 0. Ba bot

| Mã | Bot | Ai dùng | Nội dung |
|---|---|---|---|
| `VH` | **Delicare Vận hành** | **Mọi nhân viên** (kể cả trưởng ca, tổ trưởng, trưởng phòng) | Việc tay theo quyền; thông báo **cá nhân** và **trong phòng** |
| `DH` | **Delicare Điều hành** | Trưởng phòng trở lên, Ban GĐ, COO, kế toán trưởng | Việc **liên phòng**, **mọi việc chờ mình duyệt**, xem nhanh mọi mảng trong quyền, bản tin, cảnh báo nghiêm trọng; có phần tự phục vụ nhân sự |
| `TRO_LY` | **Delicare Trợ lý** (Hermes) | Ban GĐ, trưởng phòng được cấp | Hỏi đáp tự do, chỉ đọc (đợt agent: đề xuất ghi, xác nhận trên bot DH). **Không chạy trong `messaging`** |

- Quyền vào bot = mục ObjectMenu `#/bot-van-hanh`, `#/bot-dieu-hanh` (seed). Chức năng trong bot = quyền ObjectMenu của màn tương ứng → admin chỉnh quyền trên web là bot đổi theo.
- Số bot mỗi người: nhân viên **1** (VH) · trưởng phòng **2** (VH + DH) · CEO / COO **1–2** (DH, + Trợ lý).
- **Một sự kiện chỉ tới một bot với mỗi người:** cá nhân / trong phòng → VH; liên phòng / duyệt / nghiêm trọng → DH; người không có bot đích → bot người đó có.

## 1. Bốn thứ người dùng cần nhớ

Thứ tự ưu tiên cách thao tác (ít công nhất trước): **hệ thống tự ghi → Thẻ việc → QR → Thẻ ca → gõ mã → Menu**.

### 1.1 Thẻ ca (Thẻ ngày cho quản lý) – BOT-2.1
- **Một tin / người / ca, ghim đầu chat** (`pinChatMessage`), cập nhật bằng sửa tin (không gửi tin mới).
- **15′ trước giờ vào ca** (PROPOSED, `Messaging:ShiftCard:LeadMinutes`): ca, vị trí, việc chính theo vai trò (mẻ phụ trách / chuyến / hàng về), checklist và giờ hạn, việc tồn từ ca trước.
- **Trong ca:** tiến độ (cập nhật khi có sự kiện, tối đa 1 lần / 5′ – PROPOSED) + **nút đúng giai đoạn**: trước ca → Kế hoạch ca · Ai chưa vào; trong ca → Mẻ cần làm · Checklist; cuối ca → Chia suất · Hao hụt.
- **Cuối ca (sau go-live, BOT-3.1):** thành bàn giao ca soạn sẵn → trưởng ca thêm ghi chú → [Gửi bàn giao] → hiện trong Thẻ ca của trưởng ca sau.
- **Thẻ ngày** (quản lý không theo ca, bot DH): việc chờ duyệt, sự việc đang mở, số liệu chính hôm nay; gửi 07:00 (gộp bản tin sáng).
- Nút ⚡ **Ca của tôi** trên bàn phím = hiện lại / làm mới Thẻ ca.

### 1.2 Thẻ việc – mọi thông báo đều có nút làm luôn (BOT-1.4, BOT-1.7)
- **Mức tin:** 🔴 **KHẨN** – gửi ngay, có chuông · 🟡 **THƯỜNG** – gửi ngay, `disable_notification = true` · ⚪ **TỔNG HỢP** – không gửi lẻ, gom vào bản tin / Thẻ ca.
- **Nút trên thẻ:** VD mẻ sắp đến giờ [▶️ Bắt đầu]; checklist đến hạn [Mở checklist]; xe chưa rời bếp [🔁 Đổi xe] (số điện thoại tài xế in trong thẻ để bấm gọi); lô cận date [Xem lô] [Xuất trước].
- **Đổi trạng thái đồng loạt:** làm xong / đã duyệt / đã nhận → thẻ ở **mọi người nhận** (cùng `CorrelationId`) đổi thành "✅ … bởi <tên> – HH:mm", nút biến mất.
- **Nhận xử lý (thẻ liên phòng, BOT-2.2):** [🛠 Nhận xử lý] – người bấm trước được ghi; người sau thấy "Anh Nam đã nhận – 10:42". [✅ Đã xử lý xong] đóng sự việc.
- **Ghi chú chung (BOT-2.2):** trả lời (reply) vào thẻ sự việc → nội dung nối vào thẻ của mọi người liên quan, có tên và giờ. Phản ánh khách → ghi `ComplaintHistory`; sự việc khác → `PayloadJson` tin gốc (GAP-INT-04), khi T50 có thì lưu vào Obsidian `20_Su_co` lúc đóng.
- **Leo thang (BOT-2.2):** 🔴 chưa ai nhận sau N phút → bậc kế tiếp (bảng §5.2).
- **Nhập liệu không phải gõ (BOT-1.4):** điền sẵn số hệ thống đã biết (IoT, kế hoạch, FEFO, số hộp đã giao) → [✅ Đúng]; nút số đặt sẵn (nhiệt độ lõi [70] [72] [75] [78] [80] [Khác]); bước [−10] [−1] [+1] [+10]; checklist trong **một tin**, bấm từng mục ✅ / ❌ rồi [Hoàn tất]. Chỉ lý do từ chối, nội dung phản ánh, ghi chú mới phải gõ.

### 1.3 QR – quét để làm
| QR dán / in ở | Link | Mở ra |
|---|---|---|
| Cổng bếp (BOT-1.5) | `t.me/<bot VH>?start=CHAMCONG` | Chấm công; **người chưa liên kết → đăng ký** (§2) |
| Phiếu in: PO, phiếu xuất, phiếu giao (BOT-1.7) | `?start=DOC_<loại>_<id>` | Đúng phiếu đó: xem / ký nhận / duyệt theo quyền |
| Dây chuyền / bàn nấu (BOT-2.4) | `?start=LINE_<mã>` | Mẻ đang chạy tại đó: [▶️ Bắt đầu] [✅ Xong mẻ] |
| Xe (BOT-2.4) | `?start=VEH_<mã>` | Chuyến hôm nay của xe: [🚚 Xuất phát] |
| Kệ / vị trí kho (BOT-2.4) | `?start=LOC_<mã>` | Tồn tại vị trí, lô cận date |
| Khu vệ sinh hộp khay (BOT-2.4) | `?start=CLEAN` | Lô vệ sinh của ca |
- Tham số `start` ≤ 64 ký tự `[A-Za-z0-9_-]`; dùng **mã** (không dùng Guid khi có mã ngắn). Ảnh QR: FE sinh ở màn in hoặc in sẵn từ link – **BE không thêm thư viện vẽ QR** (nếu cần → hỏi gói mới).

### 1.4 Menu + điều hướng – bảng điều khiển duy nhất (BOT-1.4, BOT-UX-1, BOT-UX-2)
> **BOT-UX-2 – `TODO` (anh chốt 2026-11-23, sau khi test thật):** mục tiêu việc hằng ngày **≤ 3 chạm, không gõ tay** (tổ trưởng bếp ghi nhiệt độ lõi: ⭐ Mẻ cần làm → ME → 78; tài xế giao đủ 1 điểm: ⭐ Chuyến của tôi → điểm → ✅ Giao đủ N; chấm công: 1 chạm 📍). Khi xong, các điểm dưới thay phần tương ứng bên dưới:
> - **Một nút Quay lại / màn** (hàng điều hướng), về **đúng màn trước** (thẻ mẻ › nhiệt độ › ◀ = thẻ mẻ); không phát lại thao tác ghi. Đường dẫn đủ cấp (*Menu › Bếp › Mẻ cần làm › ME-0123*).
> - **3 phím luôn có:** gửi lại khi /menu, /ca, /help và lần đầu sau khi bot khởi động; bỏ bàn phím vị trí dùng 1 lần – phím 📍 đã gửi vị trí.
> - **Nhập số bằng nút:** giá trị điền sẵn + `−10 −1 +1 +10` + chọn nhanh + [✅ Xác nhận]; lý do / ghi chú = nút lý do hay gặp, [Khác…] mới gõ.
> - **Phản hồi:** mọi nút có thông báo nổi ngắn; ghi xong → dòng `✅ … · HH:mm` ở đầu thẻ, **thẻ giữ nút bước kế tiếp**; chỉ khi hết bước mới thành phiếu kết quả.
> - **Danh sách:** 6 mục / trang [◀ Trước] [Sau ▶]; mặc định lọc ca này / tổ tôi / chuyến của tôi, việc gấp lên đầu, [Xem tất cả].
> - **Menu theo vai trò:** ≤ 6 chức năng → màn đầu phẳng; bỏ ô "·"; mỗi chức năng 1 phân hệ chính; phân hệ của vai trò chính xếp đầu.
- **Bàn phím dưới cố định 3 phím** (mọi người, `is_persistent`): [⚡ Ca của tôi] [📂 Chức năng] [📍 Chấm công] – gửi khi liên kết tài khoản và khi /start.
- **Chỉ 4 lệnh "/"** cho mọi chat: `/menu` `/ca` `/chamcong` `/help` – lệnh nào cũng mở cùng bảng điều khiển. Lệnh / mã chức năng khác (`/nlca`, `/cho`, `/truyxuatlo`…) và gõ mã chứng từ **vẫn chạy** nhưng không liệt kê.
- **Bảng điều khiển (panel) – chat riêng:** mỗi chat **tối đa 1 tin có nút của bot**. Mọi điều hướng / chức năng **sửa chính panel** (không gửi tin mới). Dòng đầu là đường dẫn (vd. *Menu › 🍳 Bếp › 🌡 Ghi nhiệt độ lõi*); hàng nút cuối mọi màn: **[◀ Quay lại] [🏠 Menu] [✖ Đóng]** (màn đầu chỉ có ✖ Đóng).
  - **Quay lại:** lùi 1 bước (chức năng → phân hệ → màn đầu; từ màn con trong chức năng → màn đầu của chức năng). Đang nhập dở → hủy nháp, xóa câu hỏi, **không ghi gì**.
  - **Menu:** về màn chọn phân hệ. **Đóng:** xóa panel (quá 48 giờ – Telegram không cho xóa – thì sửa thành "Menu đã đóng" + bỏ nút).
  - **Màn đầu:** hàng **⭐ Hay dùng** (4 chức năng dùng gần nhất của người đó – bộ nhớ, khởi động lại thì mất) + lưới phân hệ.
  - **Xong thao tác:** nội dung bắt đầu bằng ✅ (không có nút) → panel thành **phiếu kết quả** (nội dung, mã chứng từ, giờ) + **[▶ Tiếp tục] [🏠 Menu]**; Tiếp tục về đúng màn đầu của chức năng vừa dùng.
  - **Gõ /menu, /ca, /chamcong hoặc phím bàn phím dưới:** xóa tin lệnh của người dùng, xóa panel cũ, gửi **panel mới ở cuối chat**.
  - **Bấm nút trên panel cũ** → thông báo nhỏ "Menu này đã cũ", xóa tin đó, mở panel mới ở màn đầu.
  - **Nhập tự do:** ưu tiên nút chọn sẵn; bắt buộc gõ → panel hiện bước "✏️ …" + câu hỏi ForceReply ngay dưới; trả lời xong bot **xóa câu hỏi + câu trả lời** và cập nhật panel.
  - **Tin hệ thống** (thẻ việc, Thẻ ca ghim, bản tin, thông báo – OutboundPump) **không bao giờ bị panel xóa / sửa**; nút trên thẻ việc chạy như cũ. Nút ⚡ chạy chức năng trên Thẻ ca → kết quả lên panel mới ở cuối.
  - **Nhóm chat giữ hành vi cũ** (không panel). Tắt panel: `Messaging:Panel:Enabled = false` (mỗi màn 1 tin như trước).
- **📂 Chức năng:** lưới phân hệ người đó có quyền → chức năng. Tối đa 2 lần bấm tới bất kỳ chức năng nào (phân hệ chỉ có 1 → hiện thẳng chức năng).
- **Bàn phím theo việc** (chỉ bàn phím dưới mới gửi được vị trí / mở camera): VD giao hàng [📍 Đến điểm] [📷 Ảnh ký nhận] [✅ Xong điểm] [⬅️ Thoát] – trả về bàn phím thường khi xong.
- **Gõ mã → mở thẳng:** theo tiền tố mã chứng từ đang sinh (`DocumentNumberService`, BUSINESS_RULES – VD lô, mẻ `ME-`, chuyến `CX-`, `PO-`, phản ánh…). Bảng tiền tố → màn chi tiết để ở cấu hình `Messaging:CodePrefixes`, **lấy đúng tiền tố trong code hiện tại**, không đặt mới. Gõ chữ (VD "tồn gạo") → tìm nguyên liệu / món theo tên. Không dùng AI.
- **Danh sách lệnh "/"** (`setMyCommands` scope chat): từ BOT-UX-1 chỉ 4 lệnh /menu /ca /chamcong /help; lệnh cũ (`/me`, `/tiendo`, `/chuyen`, `/ton`, `/candate`, `/thieuhut`, `/lichca`, `/nghi`, `/doica`, `/cho`, `/truyxuatlo`…) gõ vẫn chạy.
- Nút [💬 Góp ý / báo lỗi] ở cuối menu (sau go-live, BOT-3.2).

## 2. Đăng ký và liên kết (BOT-1.2)

1. **Nhân viên (mặc định):** quét QR cổng → bot: "Bấm để xác nhận số điện thoại" (nút `request_contact`) → bot nhận liên hệ, **kiểm `contact.user_id` = người gửi** (Telegram xác thực số thuộc tài khoản đó) → `api` chuẩn hóa số VN, khớp **duy nhất** `Employee.Phone` (và / hoặc `Person.PhoneNumber` → `User`) → ghi `UserExternalIdentity` (UserId / EmployeeId) → bot chào theo tên + hướng dẫn 3 nút.
2. **Người có quyền duyệt hoặc quản trị** (có mục quyền A hoặc IsAdmin): sau bước 1 phải **xác nhận thêm một lần bằng mã** lấy trên web (`TelegramLink/CreateCode`, hạn 15′) – tránh chiếm quyền duyệt chỉ bằng số điện thoại.
3. **Không khớp / khớp nhiều hồ sơ / hồ sơ không có số:** bot báo "liên hệ phòng Nhân sự"; HR cấp mã cho nhân viên (`TelegramLink/CreateCodeForEmployee`, hạn 72 giờ) → nhân viên gửi `/start <mã>` hoặc quét QR mã.
4. **Bot DH:** người có quyền `#/bot-dieu-hanh` được bot VH gửi nút mở bot DH sau khi liên kết (cần `/start` bot DH một lần – Telegram bắt buộc).
5. **Gỡ:** `/huylienket`, admin `UserExternalIdentity/Unlink`, User bị khóa → bot ngừng phục vụ (kiểm mỗi lần cấp token). Người dùng chặn bot → `IdentityStatus = Blocked`.
6. **Chỉ có EmployeeId (không có tài khoản web):** không cấp JWT; dùng API hẹp `BotChannel/Employee*` (lịch ca, chấm công, đơn nghỉ / đổi ca của chính mình), `api` tự lấy EmployeeId từ định danh, ghi dưới user `system` kèm ghi chú nguồn.

## 3. Tính năng theo bot và người dùng

Ký hiệu mức tin: 🔴 KHẨN · 🟡 THƯỜNG · ⚪ TỔNG HỢP. API: dùng action đã có; thiếu API "của tôi" (mẻ của ca tôi, chuyến của tôi…) thì **thêm action vào controller cũ** theo D-30 (không thêm bảng).

### 3.1 Phần chung (VH và DH)
| Chức năng | Lệnh | API chính |
|---|---|---|
| Liên kết | `/start` | BotChannel/LinkByPhone, LinkByCode |
| 📍 Chấm công vào / ra (vị trí; chặn vị trí chuyển tiếp, vị trí cũ > 120 giây) | `/chamcong` | AttendanceEvent/CheckIn, CheckOut (hoặc BotChannel/EmployeeCheckIn) |
| 📅 Lịch ca của tôi (7 ngày) | `/lichca` | ShiftAssignment (của tôi) |
| 🏖 Xin nghỉ · 🔁 Đổi ca (hỏi từng bước bằng nút) | `/nghi` `/doica` | LeaveRequest/Create, ShiftSwapRequest/Create |
| 📝 Đơn của tôi (trạng thái, hủy khi còn chờ) | `/donct` | LeaveRequest/Cancel, ShiftSwapRequest/Cancel |
| ⏳ Chờ tôi duyệt (nếu có quyền) | `/cho` | ApprovalRequest/GetMyPending, GetDetail, Decide |
| Menu, trợ giúp, gỡ liên kết | `/menu` `/huylienket` | – |
Thông báo cá nhân: 🟡 phân ca đã xác nhận · 🟡 nhắc ca (nằm trong Thẻ ca) · 🟡 kết quả đơn nghỉ / đổi ca · 🟡 quên chấm ra.

### 3.2 Bot Vận hành – theo nhóm (phân hệ trong 📂 Chức năng)
> Mọi chức năng trong bảng đã có trên bot (ĐỢT BOT-3, D-72 – 2026-11-20). Quyền như web: nút chỉ hiện khi tài khoản có mục menu tương ứng, máy chủ kiểm lại khi ghi.

| Nhóm (UserGroup) | Chức năng | Thông báo nhận trên VH |
|---|---|---|
| Công nhân các tổ bếp | Phần chung | Cá nhân |
| Tổ trưởng / trưởng ca bếp (OPS-KIT) | 🍳 Mẻ cần làm (bắt đầu / xong: nhiệt độ lõi, lưu mẫu) · 📈 Tiến độ ca · 🧽 Checklist ATTP · 🍱 Chia suất xong / ➕ nấu thêm / ♻️ thừa (nút trên thẻ mẻ) · 🥬 NL của ca · 👥 Ai chưa vào (tổ) · ✍️ Chấm hộ · 🔍 Truy xuất mẻ · 🧼 Lô vệ sinh hộp khay | 🔴 mẻ trễ · 🔴 lõi < 70°C / chưa lưu mẫu · 🟡 checklist sắp hạn · 🟡 người tổ chưa vào ca · 🟡 kế hoạch ca đã duyệt |
| QA / QC (OPS-KIT-QA, QA) | 🧽 Checklist / kiểm thành phẩm · 🧪 Lô chờ QC đầu vào · ⛔ Lô / mẻ đang giữ · 🌡 Nhiệt độ · 🔍 Truy xuất | 🔴 lỗi trọng yếu · 🔴 nhiệt độ ngoài ngưỡng · 🟡 hàng về chờ kiểm · 🟡 phản ánh về món ăn |
| Điều phối giao nhận (OPS-LOG) | 🗺 Giao vận hôm nay · 🚚 Chuyến trực tiếp (= 🗺 Giao vận hôm nay → thẻ chuyến) · 🧭 Lịch xe · 🔁 Đổi xe / tài xế (nút trên thẻ chuyến) · 📦 Hộp khay hôm nay | 🔴 xe chưa rời bếp · 🔴 điểm sắp / đã trễ · 🔴 nhiệt độ giao < 60°C · 🔴 giao thiếu |
| Tài xế | 🚚 Chuyến của tôi: xuất phát, 📍 đến điểm, số suất + nhiệt độ giao, 📷 ảnh ký nhận · 📥 Nhận hộp về | 🟡 được phân / đổi chuyến · 🟡 nhắc giờ xuất phát |
| Điểm phát suất (OPS-POS) | ✅ Nhận hàng / ký nhận · 🧮 Đối soát suất thực tế · 📣 Ghi phản ánh tại điểm | 🟡 xe đã xuất phát / sắp đến |
| Thủ kho / mua hàng (OPS-SCM) | 📦 Tồn · ⌛ Cận date · ⚠️ Thiếu hụt · 🥬 NL ngày mai · 🧾 PO (sắp về, trễ) · 🚛 Hàng về hôm nay · 🔍 Truy xuất lô | 🟡 thiếu hụt sau MRP · 🟡 dưới tồn tối thiểu · 🟡 cận / hết hạn · 🟡 PO trễ · 🔴 lô QC không đạt |
| Kế hoạch vận hành & Cost (OPS-PLAN) | 📋 Kế hoạch ngày mai · 🔄 Lập lại · 📅 Thực đơn tuần · ✏️ Đề xuất đổi món · ♻️ Hao hụt · 💰 Giá thành suất | 🟡 khóa đơn → kế hoạch đã lập · 🟡 thiếu NL · 🟡 kết quả duyệt thực đơn / đổi món |
| Kinh doanh / hợp đồng (SAL-SALES, SAL-CON) | 📦 Đơn ngày mai (khách phụ trách) · ⏰ Khách chưa chốt · 📅 Thực đơn tuần · 🔄 Đổi món – ý kiến khách · 📣 Báo thiếu suất / phản ánh thay khách · 📋 Việc tôi đã báo | 🔴 19:00 khách chưa chốt · 🟡 kết quả sửa đơn sau 20h · 🟡 phản ánh khách mình đã xử lý · 🟡 việc mình báo đã có người nhận |
| CSKH (SAL-CS) | 📣 Ghi phản ánh · 📋 Phản ánh đang mở (nhận / xong) · ⏱ Sắp quá hạn | 🔴 phản ánh mức cao · 🔴 sắp quá hạn · 🟡 phản ánh mới |
| NV kế toán (FIN-ACC) | 📦 Hộp khay thất thoát · 🧾 PO đã nhận · 🧾 Công tháng (chỉ xem) | ⚪ thất thoát vượt ngưỡng |
| NV nhân sự (FIN-HR) | 👥 Ai chưa vào ca (toàn công ty) · ✍️ Chấm hộ · 📥 Nhập file máy chấm công (trả lời tin bot bằng file Excel) · 🧾 Công tháng · ⏳ Đơn đang chờ (xem) | 🟡 chưa vào ca > 10′ · 🟡 chấm ngoài bán kính |
| IT / quản trị (FIN-IT) | 🖥 Tình trạng hệ thống (job, tin lỗi) · `/chatid` nhóm | 🔴 job lỗi · 🔴 tin gửi thất bại nhiều lần |

### 3.3 Bot Điều hành
Menu chung (lọc theo quyền): ⏳ Chờ tôi duyệt · 🚨 Việc liên phòng đang mở · 📊 Báo cáo hôm nay / tuần / tháng (cấp LD / QL) · 📈 Tiến độ bếp · 🗺 Giao vận trực tiếp · 📦 Tồn & thiếu hụt · 🎯 KPI · 🔍 Truy xuất · phần chung.

| Người dùng | Duyệt trên DH | Tin liên phòng |
|---|---|---|
| CEO | PO ≥ 50 triệu, các mục CEO có quyền A | 🔴 sự cố cấp công ty · ⚪ bản tin 7h, báo cáo 17h, báo cáo tuần (thứ Hai) / tháng (ngày 1) 07:30 |
| PMO | – | ⚪ bản tin · 🟡 việc liên phòng quá hạn chưa ai nhận |
| COO | PR cấp 2, PO dưới ngưỡng, kế hoạch SX, công thức / thực đơn / đổi món, điều chỉnh tồn, kiểm kê, nghỉ / đổi ca | 🔴 mọi sự cố vận hành liên phòng · 🟡 thiếu hụt NL · ⚪ bản tin |
| Bếp trưởng | – (D-69: chưa có quyền duyệt; kế hoạch SX do COO / CEO, nghỉ / đổi ca do Nhân sự / COO duyệt) | 🔴 thiếu suất cần nấu bù · 🔴 phản ánh món ăn · 🟡 thiếu NL ảnh hưởng ca · 🟡 thiếu người · 🟡 đổi món chờ |
| Trưởng mua hàng – kho | PR cấp 1 | 🟡 thiếu hụt MRP · 🔴 NCC trễ / QC không đạt ảnh hưởng SX · 🟡 PO chờ GĐ |
| Trưởng điều phối | – (D-69: không có loại duyệt lịch xe) | 🔴 giao trễ · 🔴 thiếu suất tại điểm · 🔴 nhiệt độ giao thấp · 🟡 phản ánh giờ giao |
| TP kinh doanh | – (D-69: sửa đơn sau 20h, lệch > 30%, hợp đồng do CEO duyệt) | 🔴 thiếu suất · 🔴 phản ánh mức cao · 🟡 hộp thất thoát · 🟡 đổi món cần khách chấp thuận |
| Trưởng QA | Kết quả QC đầu vào (giữ / thả lô là thao tác QA trực tiếp trên `scm-lot`, không qua duyệt – D-69) | 🔴 sự cố ATTP · 🔴 lô không đạt · 🔴 phản ánh món ăn |
| Kế toán trưởng | Kiểm kê lệch cấp 2; (R2) phí thất thoát hộp | ⚪ doanh thu, NL / doanh thu, giá thành |
| Trưởng nhân sự | Nghỉ / đổi ca theo phân cấp | 🟡 thiếu người ảnh hưởng SX · ⚪ công tháng |

Người duyệt / người nhận cụ thể lấy theo `ApprovalStep` (người / nhóm của cấp hiện tại) và quyền ObjectMenu – **"Chờ tôi duyệt" chỉ gồm bước mà người đó đúng là người duyệt**, không phải mọi thứ người đó có quyền xem (admin không bị ngập).

### 3.4 Bot Trợ lý (Hermes)
26 công cụ đọc hiện có (LEGACY_GATEWAY.md), 6 kỹ năng (báo cáo điều hành, phân tích nguyên nhân, so sánh kỳ, sự cố ATTP, mua hàng – tồn kho, sức khỏe khách hàng), tri thức vault (sau T50). Đợt agent: đề xuất ghi → xác nhận trên bot DH. Không thuộc đợt BOT.

## 4. Duyệt theo mức rủi ro (BOT-1.4) – PROPOSED, cấu hình `Messaging:ApprovalRisk`

Mã loại lấy từ hằng `BusinessType` của các `Services/Implement/Approval/*ApprovalHandler.cs` hiện có (đã thấy `LEAVE_REQUEST`, `SHIFT_SWAP`, `MENU_CHANGE`, `ORDER_DEVIATION`, `PURCHASE_ORDER`, `CONTRACT`; các handler còn lại dùng đúng hằng trong code, không đặt mã mới).

| Mức | Loại | Cách duyệt |
|---|---|---|
| **Thấp** | LeaveRequest, ShiftSwap, MenuChange; duyệt kế hoạch sản xuất (`ProductionPlan/Approve`, không qua ApprovalRequest – GAP-PRD-06) khi không có cảnh báo vượt định mức | **1 chạm** → "Đang duyệt… [↩️ Hoàn tác]" 10 giây → gọi API duyệt. Cho **duyệt hàng loạt** (có danh sách xem trước) |
| **Cao** | PurchaseOrder, PurchaseRequest, OrderChangeAfterCutoff, OrderDeviation, StockCount, StockAdjustment, Contract, Recipe; mọi duyệt có cảnh báo vượt định mức / ngưỡng | **Xác nhận 2 bước** ("Xác nhận duyệt PO-… 62,4 triệu?") |
| **Mọi lệnh từ chối** | – | 2 bước + **bắt buộc lý do** (ForceReply) |
- Loại chưa có trong bảng → coi là **Cao**. Server luôn kiểm quyền lại khi bấm; bấm 2 lần chỉ ghi 1 quyết định; thẻ cũ đổi trạng thái ở mọi người cùng cấp.

## 5. Danh mục thông báo (`NotificationCatalog`) – BOT-1.7, BOT-2.x

### 5.1 Sự kiện → phạm vi, mức, người nhận
| Mã mẫu | Sự kiện (nguồn) | Phạm vi | Mức | VH nhận | DH nhận |
|---|---|---|---|---|---|
| `SHIFT_CONFIRMED` | Phân ca xác nhận (ShiftAssignment/Confirm, Catalog 204) | Cá nhân | 🟡 | Nhân viên được phân | – |
| `LEAVE_RESULT`, `SWAP_RESULT` | Đơn nghỉ / đổi ca được duyệt / từ chối | Cá nhân | 🟡 | Người gửi | – |
| `APPROVAL_PENDING` | Cấp duyệt mới (ApprovalRequest Submit / bước kế) | Duyệt | 🟡 (🔴 nếu `PriorityCode` cao) | Trưởng phòng duyệt việc trong phòng | Cấp trên trưởng phòng / việc liên phòng |
| `APPROVAL_RESULT` | Kết quả duyệt | Cá nhân | 🟡 | Người gửi | – |
| `BATCH_LATE` | Mẻ trễ > 15′ (ProgressWatch) | Phòng | 🔴 | Trưởng ca | – |
| `CORE_TEMP_LOW` | Lõi < 70°C / chưa lưu mẫu | Liên phòng | 🔴 | Trưởng ca của mẻ | Trưởng QA, bếp trưởng, COO |
| `FOODSAFETY_CRITICAL` | Lỗi trọng yếu checklist (D-54 d, Catalog 162) | Liên phòng | 🔴 | QA, trưởng ca | Trưởng QA, bếp trưởng, COO |
| `IOT_OUT_OF_RANGE` | Nhiệt độ kho / khu ngoài ngưỡng (IoT) | Phòng | 🔴 | QA, thủ kho khu đó | Trưởng QA nếu kéo dài > 30′ |
| `CHECKLIST_DUE` | Checklist sắp đến hạn | Phòng | 🟡 | Trưởng ca | – |
| `PLAN_READY` | Khóa đơn 20h → kế hoạch chờ duyệt (LockOrders) | Liên phòng | 🟡 | Kế hoạch vận hành | Bếp trưởng, trưởng điều phối, COO |
| `SHORTAGE_MRP` | Thiếu NL sau MRP (Mrp) | Liên phòng | 🟡 | Mua hàng, thủ kho | Trưởng mua hàng – kho, bếp trưởng, COO |
| `STOCK_LOW`, `LOT_EXPIRING` | Dưới tồn tối thiểu / cận hạn (LotExpiry) | Phòng | 🟡 | Thủ kho | – |
| `PO_LATE`, `QC_INCOMING_FAIL` | PO trễ / QC đầu vào không đạt | Liên phòng | 🟡 / 🔴 | Thủ kho, QA đầu vào | Trưởng mua hàng – kho, bếp trưởng, trưởng QA |
| `TRIP_NOT_DEPARTED` | Xe chưa rời bếp quá giờ | Phòng | 🔴 | Điều phối | COO nếu leo thang |
| `STOP_LATE`, `DELIVERY_TEMP_LOW` | Điểm trễ / nhiệt độ giao < 60°C | Liên phòng | 🔴 | Điều phối | Trưởng điều phối, TP KD |
| `TRIP_DEPARTED` | Xe xuất phát (DeliveryRoute/Dispatch, Catalog 181) | Phòng | 🟡 | Điểm phát suất, NV KD phụ trách khách | – |
| `SHORTAGE_REPORTED` | Báo thiếu suất (NV KD / điểm phát / tài xế) | Liên phòng | 🔴 | Người báo (theo dõi) | Bếp trưởng, trưởng điều phối, TP KD – **[Nhận xử lý]** |
| `COMPLAINT_NEW`, `COMPLAINT_SLA` | Phản ánh mới / sắp quá hạn | Phòng → liên phòng nếu mức cao | 🟡 / 🔴 | CSKH | TP KD; món ăn → trưởng QA, bếp trưởng |
| `CONTAINER_LOSS` | Thất thoát hộp vượt ngưỡng | Liên phòng | ⚪ / 🟡 | NV kế toán (⚪) | TP KD, kế toán trưởng |
| `NOT_CHECKED_IN` | Chưa vào ca > 10′ | Phòng | 🟡 | Trưởng ca, nhân sự | Bếp trưởng nếu ảnh hưởng sản xuất |
| `ORDERS_UNCONFIRMED` | 19:00 khách chưa chốt (RemindUnconfirmedOrders) | Phòng | 🔴 | NV KD | TP KD (⚪ trong Thẻ ngày) |
| `MENU_PUBLISHED` | Thực đơn công bố (Menu/Publish, Catalog 85) | Phòng | ⚪ | Kế hoạch, KD, bếp | – |
| `MORNING_BRIEF`, `DAILY_REPORT`, `PERIOD_REPORT` | 07:00 / 17:00 / tuần / tháng | Liên phòng | ⚪ | – | CEO, COO, PMO, trưởng phòng (theo cấp) |
| `SYSTEM_JOB_FAILED`, `OUTBOUND_FAILING` | Job lỗi / tin gửi lỗi nhiều lần | Phòng | 🔴 | IT | – |
| `INCIDENT_UNCLAIMED` | Sự việc liên phòng quá hạn chưa ai nhận | Liên phòng | 🔴 | – | COO, PMO |
- **Nhóm chat** (tùy chọn, cấu hình `ConfigJson.groups` của kênh): BGĐ · Bếp – ATTP · Kinh doanh · Giao vận · Kho · Nhân sự. Chỉ tin **không chứa nút duyệt / dữ liệu cá nhân** được gửi vào nhóm; chưa cấu hình nhóm → gửi riêng.
- `CorrelationId` = mã sự việc (VD `SHORTAGE:<orderId>:<date>`), `IdempotencyKey` = mã mẫu + mã sự việc + người nhận (+ ngày) → chạy lại job không gửi trùng.

### 5.2 Leo thang (BOT-2.2) – PROPOSED, cấu hình `Messaging:Escalation`
| Sự kiện 🔴 | Bậc 1 | Sau | Bậc 2 | Sau | Bậc 3 |
|---|---|---|---|---|---|
| ATTP (lõi thấp, lỗi trọng yếu) | Trưởng ca / QA | 10′ | Bếp trưởng, trưởng QA | 20′ | COO |
| Xe chưa rời / giao trễ / nhiệt độ giao | Điều phối | 10′ | Trưởng điều phối | 20′ | COO |
| Thiếu suất | Bếp trưởng, trưởng điều phối, TP KD | 15′ | COO | 30′ | CEO |
| Phản ánh mức cao | CSKH | 30′ | TP KD | 60′ | COO |
| Duyệt quá hạn (`DueAt`) | Người duyệt | hạn | Cấp trên trực tiếp | +1 ngày | COO |
- Người duyệt có đơn nghỉ đã duyệt: **trước ngày nghỉ 1 ngày** bot nhắc "Anh nghỉ … còn N việc chờ anh duyệt" (BOT-2.2). Ủy quyền duyệt thực sự = quy định nghiệp vụ, **chưa làm** (chờ BA).

### 5.3 Giờ yên lặng theo ca (BOT-2.3) – PROPOSED, cấu hình `Messaging:QuietHours` (chi tiết DECISIONS p37)
- 🟡 / ⚪ chỉ gửi khi người nhận **đang trong ca hoặc trong 60′ trước ca**; ngoài ca → `notBefore` = đầu ca kế tiếp (hoặc 07:00 với người không có phân ca). 🔴 gửi ngay cho người **đang trực**; người ngoài ca chỉ nhận 🔴 khi là bậc leo thang.
- Tạm lưu `notBefore` trong `PayloadJson` (GAP-INT-03 đề xuất cột `ScheduledAt`).

## 6. Lệnh, callback, mã

- `callback_data` = `<loại>:<hành động>:<id>` ≤ 64 byte, Guid dạng `N`. Loại: `apv` duyệt · `inc` sự việc · `bat` mẻ · `stp` điểm giao · `chk` checklist · `menu` điều hướng · `nav` điều hướng panel (back / home / close / cont – BOT-UX-1) · `num` nhập số · `undo` hoàn tác.
- Nút nào cũng **kiểm lại quyền ở API** khi bấm; nút hết hạn (thẻ đã xử lý) → trả lời "Việc này đã được xử lý bởi …".
- Vị trí: chặn vị trí chuyển tiếp; vị trí cũ hơn 120 giây bị coi là cũ (như Apps Script); ảnh gửi muộn vẫn lấy giờ chụp trong giới hạn 30′ (PROPOSED).
- **Vị trí trực tiếp tài xế (BOT-2.4):** tài xế bật chia sẻ một lần đầu chuyến; `messaging` chỉ gửi về `api` khi **vào / ra bán kính 150 m** (PROPOSED) của điểm giao / bếp → tự ghi xuất phát / đến điểm, gửi thẻ [📷 Ảnh ký nhận] [Giao thiếu]. **Nút 📍 Đến điểm luôn còn** trên bàn phím giao hàng (khi không bật / mất tín hiệu). Không lưu từng điểm vị trí vào DB.

## 7. Bot không làm
- Máy tính bảng dùng chung ở bếp → **màn web** (FE), không dùng bot (Telegram gắn tài khoản cá nhân).
- Nhập phiếu số lượng lớn (nhập / xuất / kiểm kê nhiều dòng) → web; bot chỉ xem, duyệt, ghi nhanh từng dòng.
- Tin tự do không hiểu → gợi ý menu; hiểu câu tự nhiên = Hermes (bot Trợ lý).
- Ra lệnh bằng giọng nói → R2 (cùng Hermes); R1 chỉ nhận ghi âm đính kèm phản ánh.

## 8. Phân pha
| Giai đoạn | Nội dung | Task |
|---|---|---|
| **R1 cốt lõi** | Đăng ký QR + SĐT · Thẻ việc (nút, 3 mức, điền sẵn, duyệt theo rủi ro) · 3 nút + bàn phím theo việc · Menu phân hệ + gõ mã + lệnh "/" theo chat · Chức năng VH / DH · QR cổng + phiếu in | BOT-1.1 → BOT-1.7 |
| **R1 hoàn thiện** | Thẻ ca / Thẻ ngày · Nhận xử lý + ghi chú chung + leo thang + nhắc trước ngày nghỉ · Giờ yên lặng theo ca + bản tin / lịch · QR vật thể + vị trí trực tiếp tài xế | BOT-2.1 → BOT-2.4, BOT-2.9 |
| **Sau go-live** | Bàn giao ca · Công tuần + góp ý · Đo số liệu (thời gian nhận cảnh báo, số lần bấm, chức năng không dùng) · Mini App (FE) cho biểu mẫu dài / bảng điều hành | BOT-3.x |
