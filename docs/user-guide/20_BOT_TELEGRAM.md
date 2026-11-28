---
screens: []
roles: []
updated: 2026-11-29
---
# 20 – Dùng bot Telegram (bot Vận hành, bot Điều hành)

## 1. Hai bot

| Bot | Dành cho | Nội dung |
|---|---|---|
| **Vận hành** | Mọi nhân viên có số điện thoại trong hồ sơ: bếp, QC, kho, giao vận, điểm phát, CSKH, NS… | Việc cá nhân + việc của phòng ban; cảnh báo trong phạm vi phòng ban |
| **Điều hành** | CEO, CEO-PMO, COO, FIN-ACC (và trưởng các phòng được cấp) | Tổng quan, KPI, cảnh báo liên phòng ban, phê duyệt, leo thang |

Quyền bấm nút = quyền vào màn tương ứng trên web: nút chỉ hiện khi nhóm của bạn có quyền mục menu của chức năng; máy chủ kiểm lại khi ghi.

## 2. Bắt đầu

### 2.1 Nhân viên (liên kết bằng số điện thoại)
1. Mở bot (link / QR do NS gửi) → **Start**.
2. Bot hỏi → bấm **📱 Xác nhận số điện thoại** (nút chia sẻ số của Telegram).
3. Số khớp hồ sơ nhân viên → *liên kết thành công*, hiện bàn phím chính. Không khớp → nhờ NS / IT sửa SĐT trong hồ sơ nhân viên (`adm-employee`) rồi làm lại.

### 2.2 Người có tài khoản web (người duyệt, quản lý)
- Để **duyệt** và dùng chức năng theo quyền web, tài khoản Telegram phải gắn với **tài khoản web**: lấy mã trên web, gõ `/start <mã>` (mã người dùng hết hạn sau 15 phút; mã NS tạo cho nhân viên hết hạn sau 72 giờ).
- **Lấy mã trên web:** bấm tên tài khoản (góc phải) → **Liên kết Telegram** → **Tạo mã liên kết** (hoặc màn *Việc của tôi* → khối Liên kết Telegram). Màn hiện mã + nút **Mở bot Vận hành / Điều hành** (link mở thẳng bot kèm mã); cùng chỗ đó xem bot đã liên kết và **Gỡ** liên kết. Nhân viên không có tài khoản web: Nhân sự mở *Nhân viên* (`adm-employee`) → nút **Tạo mã liên kết bot** trên dòng / trong ngăn chi tiết (mã 72 giờ, cần quyền Sửa).
- `/huylienket` – hủy liên kết.

## 3. Bàn phím và lệnh chung

Bàn phím dưới cố định (mọi người): **[⚡ Ca của tôi] [📂 Chức năng] [📍 Chấm công]**. Danh sách lệnh `/` chỉ có 4 lệnh: `/menu`, `/ca`, `/chamcong`, `/help` – các lệnh khác trong bảng dưới gõ vẫn chạy.

**Bảng điều khiển (chat riêng):** bot chỉ giữ **1 tin có nút** – mọi màn mở ra đều hiện trong chính tin đó, dòng đầu là đường dẫn đủ cấp (vd. *Menu › 🍳 Bếp › 🍳 Mẻ cần làm › 🔥 ME-0123*; dài quá thì rút gọn giữa bằng "…"), hàng cuối **[◀ Quay lại] [🏠 Menu] [✖ Đóng]** – mỗi màn chỉ có **1 nút Quay lại** (ở hàng cuối).
- **Quay lại** mở lại **đúng màn trước** (thẻ mẻ › nhiệt độ › ◀ → về thẻ mẻ, không về danh sách). Quay lại **không bao giờ lặp lại thao tác ghi**; đang chọn số / gõ dở (vd. lý do khác) mà bấm Quay lại → bỏ, không lưu gì.
- Bấm nút nào bot cũng báo 1 dòng nhỏ trên màn hình (*"Đã ghi 78 °C"*, *"Đã mở: ME-0123"*, lỗi → câu lỗi của máy chủ).
- Ghi xong 1 bước: đầu thẻ hiện **✅ kết quả · giờ** và thẻ **giữ nút bước kế tiếp** (vd. ghi nhiệt độ xong vẫn còn 🧪 Lưu mẫu / ✅ Xong mẻ). Chỉ khi chức năng không còn bước nào, bảng mới thành **phiếu kết quả** với **[▶ Tiếp tục]** / **[🏠 Menu]**.
- Gõ `/menu` hoặc bấm phím dưới → bot xóa tin lệnh của bạn và bảng cũ, mở bảng mới ở cuối chat. Bấm nút trên bảng cũ → báo *"Menu này đã cũ"* và mở bảng mới.
- **Màn đầu:** người có ≤ 6 chức năng thấy thẳng các chức năng (không qua phân hệ); người nhiều chức năng thấy phân hệ – phân hệ của công việc chính của bạn đứng đầu – và **1 hàng ⭐ Hay dùng** (chức năng dùng gần nhất) trên cùng. ⚡ Ca của tôi / 📍 Chấm công không lặp lại trong menu (đã có ở phím dưới). Mỗi chức năng chỉ nằm ở 1 phân hệ.
- **Danh sách** tối đa 6 mục / trang, **[◀ Trước] [Sau ▶]**; việc gấp (trễ giờ, cảnh báo, quá hạn) lên đầu. *Mẻ cần làm* mặc định chỉ hiện **ca hiện tại** (đang nấu / chờ QC, mẻ trễ, mẻ bắt đầu trong 2 giờ trước – 4 giờ tới) → **[📋 Xem tất cả]** để xem cả ngày.
- **Không phải gõ:** số (nhiệt độ, số suất, số hộp, số ngày nghỉ) chọn trên **màn số** – giá trị điền sẵn (kế hoạch / lần đo trước) hiện trên bảng, chỉnh bằng **[−10] [−1] [+1] [+10]** hoặc hàng chọn nhanh, rồi **[✅ Xác nhận]**. Lý do / ghi chú (từ chối duyệt, khắc phục nhiệt độ, nấu thêm, không giao được, chấm hộ, xin nghỉ, đổi ca, đổi món, vệ sinh không đạt) chọn **nút lý do hay gặp**; chỉ khi bấm **[✏️ Khác…]** mới phải gõ. Chỉ còn gõ tay cho chữ tự do thật sự: ghi chú phản ánh / sự việc, tên khách / món / tài xế để tìm, tên người nhận khác, số lô để truy xuất.
- Phím dưới **3 phím luôn còn**: bot gửi lại khi gõ `/menu`, `/ca`, `/help` và ở lần bạn nhắn đầu tiên sau khi hệ thống khởi động lại (tin phím cũ được xóa). `/chamcong` chỉ nhắc bấm 📍 – không đổi bàn phím.
- Thẻ việc, Thẻ ca ghim, bản tin… **không bị bảng điều khiển xóa / sửa**. Nhóm chat không có bảng điều khiển.

| Nút | Lệnh | Làm gì |
|---|---|---|
| ⚡ Ca của tôi | `/ca` | Ca hôm nay, giờ, trạng thái vào / ra, việc chính; **⚡** làm mới |
| 📂 Chức năng | `/menu` | Danh sách phân hệ được phép |
| 📍 Chấm công | `/chamcong` | Bấm phím 📍 ở bàn phím dưới để gửi vị trí vào / ra ca (không chuyển tiếp vị trí; vị trí quá 120 giây bị từ chối; bán kính 150 m) |
| 📅 Lịch ca | `/lichca` | Lịch ca 7 ngày |
| 🏖 Xin nghỉ | `/nghi` | Loại nghỉ → ngày bắt đầu (10 ngày tới bấm 1 chạm; Ngày khác… mới gõ) → số ngày (1 / 2 / 3, Khác… = màn số) → lý do (nút lý do, Khác… mới gõ) |
| 🔁 Đổi ca | `/doica` | Chỉ người có tài khoản web |
| 📝 Đơn của tôi | `/donct` | Đơn nghỉ / đổi ca và trạng thái |
| ⏳ Chờ tôi duyệt | `/cho` | Yêu cầu đến lượt bạn duyệt (cần tài khoản web + quyền duyệt) |
| ❓ Trợ giúp | `/help` | Hướng dẫn ngắn |
| | `/start`, `/huylienket` | Bắt đầu / hủy liên kết |

**Tìm nhanh:** gõ số chứng từ dạng `XX-123456…` (PH, XN, PA, PO, DN, NL, ME, CX, KH, VS, HD) để mở thẻ chứng từ; gõ chữ để tìm theo tên.

**Thẻ ca:** 15′ trước ca bot gửi và ghim **Thẻ ca** (ca, việc chính, checklist, việc tồn); thẻ được cập nhật khi có thay đổi (cách nhau ≥ 5′). 07:00 có **thẻ ngày**.

## 4. Bot Vận hành – theo phân hệ

| Phân hệ | Nút (lệnh) | Ai dùng | Chi tiết |
|---|---|---|---|
| 🍳 Bếp | 🍳 Mẻ cần làm (`/me` – mặc định ca hiện tại, 📋 Xem tất cả), 📈 Tiến độ ca (`/tiendo`), 🥬 NL của ca (`/nlca`), 👥 Ai chưa vào (tổ) + ✍️ Chấm hộ (chọn lý do); thẻ mẻ Đang nấu: **hàng nhiệt độ lõi 70° 72° 75° 78° 80° bấm 1 chạm**, 🌡 Nhiệt độ khác (màn số), dưới ngưỡng → chọn hành động khắc phục; ✅ Xong mẻ (số suất điền sẵn kế hoạch), 🍱 Chia suất xong, ♻️ Ghi thừa, ➕ Nấu thêm (màn số + nút lý do) | OPS-KIT | `07_BEP_SAN_XUAT.md` §7 |
| 🧽 ATTP / QC | 🧽 Checklist ATTP (`/checklist`), ⛔ Mẻ đang giữ (`/giu`), 🧪 Lô chờ QC đầu vào (`/chokiem`), 🌡 Cảnh báo ATTP (`/canhbao`), 🔍 Truy xuất lô / mẻ (`/truyxuatlo` – gõ số lô LO-… / số mẻ ME-…; ai có quyền xem lô hoặc mẻ đều thấy), 🧼 Lô vệ sinh hộp khay (`/vesinh` – ✅ Đạt / ❌ Không đạt chọn lý do) | QA, OPS-KIT | `06_QC_ATTP.md` §7 |
| 🚚 Giao vận | 🚚 Chuyến của tôi (`/chuyen` – 1 chuyến thì mở thẳng thẻ chuyến), 🗺 Giao vận hôm nay (`/giaovan`), 📥 Nhận hộp về (`/hop` – số hộp trên màn số, điền sẵn số phải thu), 🧭 Lịch xe ngày mai (`/lichxe`), 📦 Hộp khay hôm nay (`/hopkhay`); 🚀 Xuất phát: nhiệt độ bấm 1 chạm; thẻ điểm: **✅ Giao đủ N · t° 1 chạm** (số đã xếp / kế hoạch, nhiệt độ đo tại điểm hoặc lúc xuất phát, người nhận điền sẵn – 👤 Người nhận khác… để gõ tên), ✏️ Giao số khác (thiếu / thừa), ❌ Không giao được (chọn lý do); thẻ chuyến chưa xuất phát: 🔁 Đổi xe (chọn xe) / 👤 Đổi tài xế (gõ tên để tìm) | OPS-LOG | `09_GIAO_VAN.md` §7 |
| 🍱 Điểm phát | 🚚 Xe đến hôm nay (`/xeden`) + ✅ Nhận <khách> (màn số điền sẵn kế hoạch → ✅ Ký nhận – người nhận là bạn), 🧮 Đối soát suất (`/doisoat` – điền sẵn số khách nhận); **2 giờ sau ký nhận bàn giao** bot tự hỏi với nút **✅ Đã phục vụ đủ** (1 chạm) / **✏️ Nhập số khác** (UI-6.8, D-86 c); ghi phản ánh tại điểm: 📣 Ghi phản ánh ở phân hệ CSKH (hoặc ⭐ / `/ghiphananh`) | OPS-POS | `10_DIEM_PHAT.md` §7 |
| 📣 CSKH | 📋 Phản ánh đang mở (`/phananh`), ⏱ Sắp quá hạn (`/saphan`), 📣 Ghi phản ánh (`/ghiphananh`) | SAL-CS | `10_DIEM_PHAT.md` §7 |
| 📦 Kho & mua hàng | 📦 Tồn dưới mức (`/ton`), ⌛ Cận date (`/candate`), 🥬 NL ngày mai (`/nlmai`), 🧾 PO sắp về / trễ (`/po`), 🚛 Hàng về hôm nay (`/hangve`), ⚠️ Thiếu hụt (`/thieuhut`); truy xuất lô / mẻ: 🔍 Truy xuất ở ATTP / QC (`/truyxuatlo`) | OPS-SCM | `04_MUA_HANG.md`, `05_KHO.md` §7 |
| 📋 Kế hoạch | 📋 Kế hoạch ngày mai (`/kehoach`), 📅 Thực đơn đang áp dụng (`/thucdon`), ♻️ Hao hụt 7 ngày (`/haohut`), 💰 Giá thành suất (`/giathanh`), 🔄 Lập lại kế hoạch ngày mai (`/laplai`, xác nhận trước), ✏️ Đề xuất đổi món (`/doimon` – món cũ → món mới → ngày ăn → lý do) | OPS-PLAN | `03_KE_HOACH.md` §7 |
| 💼 Kinh doanh | 📦 Đơn ngày mai (`/donmai`), ⏰ Khách chưa chốt (`/chuachot`), 📋 Việc tôi đã báo (`/viecdabao`); báo thiếu suất / phản ánh = 📣 Ghi phản ánh (CSKH, `/ghiphananh`), đổi món theo ý khách = ✏️ Đề xuất đổi món (Kế hoạch, `/doimon`), thực đơn = 📅 Thực đơn đang áp dụng (Kế hoạch, `/thucdon`) | SAL-SALES, SAL-CON | `02_KINH_DOANH.md` §7 |
| 🧾 Kế toán | 📦 Hộp khay thất thoát (`/thatthoat`), 🧾 PO đã nhận (`/podanhan`); 🧾 Công tháng ở phân hệ Nhân sự (`/congthang`) | FIN-ACC | |
| 👥 Nhân sự | 👥 Ai chưa vào ca (`/chuavao` – trễ lâu nhất lên đầu) + ✍️ Chấm hộ (chọn lý do), 🧾 Công tháng (`/congthang`, cần quyền xem bảng công), ⏳ Đơn đang chờ (`/donchoduyet`), 📥 Nhập file máy chấm công (`/nhapmcc` – trả lời tin bot bằng file Excel .xlsx) | FIN-HR | `11_NHAN_SU.md` §7 |
| 🖥 Quản trị | 🖥 Tình trạng hệ thống (`/hethong`), 🆔 Mã chat (`/chatid` – trong nhóm: thêm bot vào nhóm rồi gõ `/chatid`, chỉ quản trị được trả lời) | FIN-IT (admin) | `01_QUAN_TRI.md` |
| ⏳ Phê duyệt | ⏳ Chờ tôi duyệt (`/cho`) | Người duyệt | `12_QUAN_LY_DUYET.md` §7 |

**Thao tác nhanh (≤ 3 chạm, không gõ – BOT-UX-2):** ghi nhiệt độ lõi: **⭐ Mẻ cần làm → ME-… → 78°**; giao đủ 1 điểm: **⭐ Chuyến của tôi → điểm → ✅ Giao đủ N**; chấm công: **1 chạm phím 📍** (3 phím dưới vẫn giữ nguyên). Lý do hay gặp chỉnh trong cấu hình `Messaging:Reasons:<luồng>` của container messaging (IT).

### 4b. 💬 Hỏi nhanh (bot Vận hành và Điều hành – không dùng AI)

- Mở: 📂 Chức năng › **💬 Hỏi nhanh** hoặc gõ `/hoinhanh` (cần tài khoản web).
- Có nhiều mảng (Kho, Bếp, ATTP, Nhân sự…) → bấm mảng; mỗi nút là 1 câu hỏi. Chỉ hiện câu thuộc màn bạn có quyền xem trên web.
- Bấm câu → tin kết quả: tiêu đề, tóm tắt, tối đa 10 dòng ("… còn N dòng – xem đủ trên web"), nút **🌐 Mở trên web** (khi IT đã đặt địa chỉ web) và **⬅️ Câu khác**.
- Câu cần số lô / tên nguyên liệu / từ khóa: bot hỏi "Gõ …" – **trả lời vào đúng tin đó** rồi bot gửi kết quả.
- Muốn hỏi tự do hoặc cần phân tích → dùng bot **Trợ lý** hoặc tab Chat trên web (`15_TRO_LY_AI.md` mục 4e).

## 5. Bot Điều hành
Xem `13_BAN_DIEU_HANH.md` §7: 📊 Tổng quan hôm nay, 🚨 Việc đang mở, 🎯 KPI 7 ngày, 📅 Chỉ số theo tháng, 📈 Tiến độ bếp, 🗺 Giao vận trực tiếp, 📦 Tồn & thiếu hụt, 🔍 Truy xuất.

## 6. Mã QR dán tại hiện trường

**In QR trên web:** nút **In QR …** ở đầu các màn *Dây chuyền* (`prod-line`), *Trạm / bàn nấu* (`prod-workstation` – kèm **In QR cổng chấm công**), *Xe* (`log-vehicle`), *Vị trí kho* (`scm-location`), *Vệ sinh khay hộp* (`qc-cleaning`) – cần quyền In của màn. Mở cửa sổ in A4, mỗi mã 1 thẻ (tên, mã, link) để cắt dán. Phiếu in PDF (nhập, xuất, kiểm kê, giao, POD, đối soát) có sẵn ảnh QR mở phiếu trên bot ở cuối phiếu. Cần khai báo username bot Vận hành (màn *Bot*) – chưa khai báo thì không in được QR.

| QR (`?start=…`) | Dán ở | Mở |
|---|---|---|
| `CHAMCONG` | Cổng / bếp | Chấm công |
| `LINE_<mã>` | Dây chuyền / bàn nấu | Mẻ đang nấu / chờ nấu tại đó |
| `VEH_<mã xe>` | Xe | Chuyến hôm nay + 🚀 Xuất phát |
| `LOC_<mã>` | Kho / điểm phát | Thông tin khu / điểm |
| `CLEAN` | Khu vệ sinh khay | Lô vệ sinh hôm nay |
| `DOC_<GR/MI/SC/DO/POD/RC>_<mã>` | In trên phiếu (nhập, xuất, kiểm kê, giao, POD, đối soát) | Thẻ chứng từ |

## 7. Thông báo và sự cố

- Mức: 🔴 khẩn (ATTP, thiếu suất, xe trễ…), 🟡 cần làm, ⚪ thông tin. Tin cá nhân / phòng ban đi bot Vận hành; tin liên phòng ban, phê duyệt, khẩn cấp đi bot Điều hành.
- **Thẻ sự cố:** **🛠 Nhận xử lý** (ghi tên người nhận cho mọi người cùng thấy), **✅ Đã xử lý**, trả lời tin để thêm ghi chú. Chưa ai nhận → leo thang theo bậc thời gian (vd ATTP trọng yếu 10′ → COO, 30′ → CEO-PMO; thiếu suất 15′ → COO, 45′ → CEO; xe chưa rời bếp / điểm trễ 10′ → COO; IoT 30′ → COO).
- **Kiểm tra định kỳ:** mẻ trễ > 15′, chuyến chưa xuất phát > 10′, điểm dừng trễ > 15′, chưa vào ca > 10′, phản ánh còn < 60′ tới hạn, yêu cầu duyệt quá hạn 24 giờ.
- **Giờ yên lặng:** 🟡 / ⚪ gửi khi bạn đang trong ca hoặc 60′ trước ca; ngoài ca hẹn tới 60′ trước ca kế tiếp. Người không có phân ca nhận trong 07:00–21:00. 🔴 gửi ngay cho người đang trực (nếu không ai trực thì gửi tất cả); tin leo thang gửi ngay.
- **Lịch tin định kỳ:** 07:00 thẻ ngày / bản tin sáng · 16:00 nhắc bàn giao người nghỉ ngày mai · 17:00 báo cáo cuối ngày · 19:00 khách chưa chốt đơn · 21:00 nguyên liệu cho tối nay / mai.

## 8. Duyệt trên bot
`12_QUAN_LY_DUYET.md` §7 (APR-UI-1): thẻ duyệt cùng khuôn tóm tắt với web – 🔴 quá hạn / ⚠️ cảnh báo / 🟢 bình thường, vì sao cần duyệt, ≤ 3 cảnh báo, người gửi, hạn; **✅ Duyệt / ❌ Từ chối / 📄 Xem chi tiết**. Rủi ro thấp + không cảnh báo duyệt 1 chạm + **↩️ Hoàn tác** 10 giây, **✅ Duyệt nhanh N mục**; loại khác xác nhận 2 bước; **❌ Từ chối** → chọn lý do hay gặp (bấm = từ chối), **✏️ Khác…** mới phải trả lời tin bằng lý do. **📋 Tổng hợp cuối ngày** lúc 18 giờ (`Messaging:ApprovalDigestHour`). Tin **🔔 Người gửi nhắc** khi người gửi bấm Nhắc trên web.

## 9. Lỗi thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| Bot không nhận số điện thoại | SĐT hồ sơ khác SĐT Telegram – nhờ NS / IT sửa |
| Không thấy nút chức năng | Nhóm quyền chưa có mục menu tương ứng – nhờ IT |
| Chấm công báo vị trí cũ / chuyển tiếp | Bấm lại **📍 Chấm công** và gửi vị trí hiện tại |
| Ngoài bán kính | Đến đúng bếp (≤ 150 m) |
| "Phiên … đã hết hạn – gõ /… để làm lại" | Làm lại lệnh |
| Không duyệt được / không có ⏳ | Chưa liên kết tài khoản web – lấy mã ở menu tài khoản → Liên kết Telegram |
| Mẻ xong trên bot nhưng QA không cho Đạt được (mẻ kết thúc trước 19/11/2026) | Mở mẻ trên web → **Ghi sản lượng** |
