# 11 – Nhân sự: phân ca, chấm công, nghỉ phép, đổi ca

## 1. Bạn là ai

- Nhóm quyền: **FIN-HR** (3.2 Nhân sự – trưởng phòng / chuyên viên). Nhóm này có quyền sửa mọi màn `hr-*`, `wf-*` và được cấp **quyền duyệt **Đơn xin nghỉ**, **Đơn đổi ca** (D-79) – duyệt ở màn **Phê duyệt** (sidebar, D-81) hoặc ngay trên `hr-leave` / `hr-swap`.
- Mọi nhân viên có tài khoản web tự chấm công và lập đơn nghỉ / đổi ca ở màn **Việc của tôi** (`hr-my`, không cần quyền màn nào – xem 6.2).
- Bot: **Vận hành** (👥 Nhân sự: Ai chưa vào ca, chấm hộ; 👤 Cá nhân: chấm công, lịch ca, xin nghỉ, đổi ca). Trưởng phòng NS được cấp thêm bot **Điều hành**.

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | FIN-HR |
|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X |
| Phê duyệt › Đơn xin nghỉ | `wf-inbox?type=hr-leave` | Duyệt |
| Phê duyệt › Đơn đổi ca | `wf-inbox?type=hr-swap` | Duyệt |
| Nhân sự › Nhân viên | `adm-employee` | S (IX) |
| Nhân sự › Phân ca | `hr-shift-assign` | S (IX) |
| Nhân sự › Chấm công | `hr-attendance` | S (IX) |
| Nhân sự › Đơn xin nghỉ | `hr-leave` | S (IX) |
| Nhân sự › Đơn đổi ca | `hr-swap` | S (IX) |
| Nhân sự › Ca làm việc | `hr-shift` | Q |
| Nhân sự › Chức vụ | `adm-position` | Q |
| Quản trị hệ thống › Sơ đồ tổ chức | `adm-orgunit` | X |
| Nhật ký & Tích hợp › Liên kết tài khoản chat | `int-identity` | X |
<!-- MENU5:END -->

## 3. Việc trong tuần / ngày

| Khi nào | Việc | Màn |
|---|---|---|
| Cuối tuần trước | Phân ca tuần tới (hàng loạt + chỉnh từng ô), **Xác nhận lịch** | `hr-shift-assign` |
| 07:00 hằng ngày | Bot gửi thẻ ngày; 15′ trước ca gửi **Thẻ ca** cho từng người | bot |
| Đầu ca | Theo dõi **Ai chưa vào ca**, chấm hộ khi có lý do | bot `/chuavao` / `hr-attendance` |
| 08:00 | Hệ thống tự **chốt vắng** các ca hôm qua không vào ca | tự động |
| Khi có đơn | Duyệt đơn nghỉ / đổi ca; phân người thay khi thiếu | `wf-inbox`, `hr-shift-assign` |
| 16:00 | Bot nhắc bàn giao cho người nghỉ ngày mai | bot |
| Cuối kỳ | Xem **Bảng công** (≤ 62 ngày), chi tiết từng người | `hr-attendance` |

## 4. Thao tác từng bước

### 4.0a Danh sách Nhân viên – `adm-employee` (UI-6.36)
- Nút đầu màn: **Nhập từ Excel** (tải file mẫu → điền → nạp; dòng lỗi như thiếu họ tên, SĐT sai / trùng người đang làm được bỏ qua và liệt kê) · **Thêm nhân viên**.
- **4 thẻ, bấm để lọc:** Đang làm việc (trên tổng hồ sơ) · Vào làm tháng này · **Chưa có tài khoản** (dòng phụ: số đang chờ IT duyệt) · **Chưa liên kết bot**.
- **Lọc:** chip trạng thái có số đếm (**Đang làm việc** mặc định · Tạm nghỉ · Đã nghỉ việc · Tất cả); **Đơn vị** gõ để tìm, hiện theo cây (chọn đơn vị cha gồm cả đơn vị con); Chức vụ; ô tìm tên / mã / SĐT.
- **Cột:** Mã · Họ tên (SĐT) · Đơn vị · Chức vụ · Ngày vào làm · **Tài khoản** (Có / Chờ IT duyệt / Chưa có / Đã khóa) · **Bot** · Trạng thái (người đã hẹn nghỉ: "Nghỉ từ dd/mm"). Bấm dòng → hồ sơ. Điện thoại: dòng gọn họ tên, SĐT, mã · chức vụ · đơn vị, tài khoản.
- **Chọn nhiều** (ô đầu dòng) → **Đề nghị cấp tài khoản** (mỗi người một yêu cầu cho IT, nhóm quyền gợi ý theo chức vụ; bỏ qua người đã có / đang chờ – báo số bỏ qua) · **Tạo mã liên kết bot** (hiện mã + QR từng người, hạn 72 giờ, **In danh sách mã**; bỏ qua người đã liên kết) · **Xuất Excel**.
- Tab **Đối chiếu tài khoản · n** chỉ hiện khi còn nhân viên chưa gắn tài khoản / hồ sơ người.

### 4.0c Thêm / sửa nhân viên (UI-6.36)
- **Thông tin cá nhân:** Họ tên\* · **Điện thoại\*** – gõ xong hệ thống kiểm ngay: sai định dạng (10 số, bắt đầu 0) hoặc **trùng người đang làm** ("Số này đang dùng cho … (mã)…") → nút Lưu khóa · Giới tính (Nam / Nữ / Khác) · Ngày sinh · Email · Địa chỉ.
- **Công việc:** Đơn vị\* (gõ để tìm, cây cha → con) · Chức vụ\* · Ngày vào làm\* (mặc định hôm nay). Không có ô Mã (hệ thống cấp), Trạng thái, Công ty (lấy theo đơn vị).
- **Tài khoản & bot** (chỉ khi thêm): tích **Cấp tài khoản web** → hiện nhóm quyền gợi ý theo chức vụ (IT đổi được khi duyệt); **Tạo mã liên kết bot** (mặc định tích) → lưu xong hiện mã / QR.
- **Lưu & mở hồ sơ** → mở hồ sơ người mới. **Sửa** (hồ sơ) dùng cùng form, không có nhóm Tài khoản & bot; đổi trạng thái chỉ bằng nút trên hồ sơ.

### 4.0 Nhân viên – `adm-employee` › bấm dòng nhân viên
Mở **hồ sơ nhân viên** cả trang (← về danh sách). Ai xem được danh sách nhân viên thì mở được hồ sơ; **ngày sinh, giới tính, địa chỉ** chỉ hiện với người có quyền **Sửa** hồ sơ nhân viên (NS, quản trị) – máy chủ không gửi các trường này cho người khác.
- **Việc cần làm cho người mới** (UI-6.36 – vào làm ≤ 30 ngày, tự ẩn khi xong hết): Đề nghị cấp tài khoản (xong khi đã có tài khoản / đang chờ IT) · Liên kết bot (**Tạo mã + QR**) · Phân ca tuần này (**Phân ca**) · Giấy khám sức khỏe (**Tải lên** – lưu ở tab Tài liệu, loại "Giấy khám sức khỏe").
- **Phần đầu:** họ tên, mã, chức danh, đơn vị, ngày vào làm + thâm niên, trạng thái, nhãn **Đã / Chưa liên kết bot**, SĐT, email. Nút **Sửa**, **Phân ca** (mở màn phân ca), **Chấm công hộ** (chọn sẵn nhân viên), ⋯ **Tạo mã liên kết bot** (hạn 72 giờ), **Tạo đơn nghỉ thay**, **In hồ sơ**, **Cho nghỉ việc** – nút hiện theo quyền.
- **4 thẻ chỉ số:** Ca tháng này (giờ công) · Đúng giờ (tháng) · Ngày nghỉ năm nay (số đơn chờ duyệt) · Vắng / muộn (tháng) – cùng cách tính với **Bảng công**.
- **Tab Tổng quan (UI-2R, D-69):** hai cột – **trái 1/3 "Thông tin nhân viên"**: chức danh + đơn vị · điện thoại · email · ngày vào làm + thâm niên · ca thường làm (ca được phân nhiều nhất 30 ngày qua) · liên kết bot + tài khoản web; nhóm **Cá nhân** (ngày sinh, giới tính, địa chỉ – biểu tượng khóa; **chỉ hiện với người có quyền Sửa hồ sơ nhân viên**, máy chủ không trả cho người khác), **Công việc** (chức danh, đơn vị, nhóm quyền, ngày vào làm), **Giấy tờ** (giấy khám sức khỏe, hợp đồng lao động – lưu ở tab Tài liệu), **Hệ thống**. **Phải 2/3**: **Cần chú ý** (ca hôm nay đã tới giờ mà chưa vào ca, ≥ 2 lần chấm ngoài bán kính trong 7 ngày, đơn chờ duyệt, chưa liên kết bot, thiếu SĐT) → **Ca 7 ngày tới** (ngày nghỉ tô cam, ✓ đã vào ca, dòng "Hôm nay"; nút Phân ca) cạnh **Nghỉ phép và đơn từ** (đơn chờ duyệt lên đầu, đơn gần đây, số ngày đã nghỉ năm nay theo loại) → **Giờ công 30 ngày** → **Chấm công gần đây** (ngày, ca, vào – ra, hình thức, kết quả Hợp lệ / Muộn / Về sớm / Ngoài bán kính; Xem bảng công). Tạo mã liên kết bot: menu ⋯ ở phần đầu hoặc tab Tài khoản và bot.
  - **Khối thông tin bên trái (UI-2R, dùng chung 3 màn chi tiết):** 6 dòng chính luôn hiện (điện thoại / email bấm để gọi / gửi thư); bên dưới là các **nhóm gập** – bấm tiêu đề nhóm (hoặc Tab + Enter) để mở / gập, dòng xám dưới tiêu đề là tóm tắt; trình duyệt **nhớ nhóm đang mở** cho lần sau. Ô trống không hiện; nhóm chưa có gì ghi "Chưa có thông tin · Bổ sung" (bấm Bổ sung = Sửa, chỉ khi có quyền sửa). Nút sao chép cạnh MST / CCCD. Màn rộng: khối này đứng yên khi cuộn; màn < 1200px: xếp trên cùng.
- **Các tab khác** (theo quyền xem màn `hr-*`): **Lịch ca** (lưới tuần, ‹ › đổi tuần, bấm ngày mở màn phân ca) · **Chấm công** (lọc từ – đến) · **Nghỉ phép** (tổng ngày nghỉ năm theo loại; bấm đơn → tab **Xử lý đơn**: Duyệt / Từ chối / Hủy theo quyền; nút **Tạo đơn nghỉ thay**) · **Đổi ca** (đơn nhân viên đề nghị hoặc được nhận ca) · **Công việc** (chuyến xe, mẻ… gắn nhân viên) · **Tài khoản và bot** · **Tài liệu** · **Lịch sử**.
- **Tab Tài khoản và bot (HR-ACC-1, D-82):** trạng thái tài khoản **Chưa có / Đang chờ IT duyệt / Hoạt động / Đã khóa**, tên đăng nhập, nhóm quyền, nhóm gợi ý theo chức danh, yêu cầu đang chờ. Nút theo quyền: NS **Đề nghị cấp tài khoản** (chọn nhóm đề xuất + lý do → IT duyệt) / **Đề nghị mở lại**; IT **Tạo ngay**, **Đặt lại mật khẩu**, **Khóa**. Tài khoản gắn với nhân viên qua **hồ sơ người** – đổi mã nhân viên / SĐT / tên đăng nhập không mất liên kết. Lần đăng nhập cuối chưa có chỗ lưu (GAP-ACC-01). Phép năm còn lại và giấy khám sức khỏe chưa có dữ liệu (GAP-HR-06, GAP-HR-07).

### 4.0b Tuyển mới, nghỉ việc, đối chiếu tài khoản (HR-ACC-1, D-82)
- **Tuyển mới:** **Thêm nhân viên** → tích **Cấp tài khoản web** → **Lưu & mở hồ sơ**. Yêu cầu **Cấp tài khoản** tự gửi IT (nhóm quyền gợi ý theo chức danh, IT chỉnh được). IT duyệt xong: tài khoản tên = mã nhân viên viết thường, mật khẩu tạm gửi qua bot (nếu nhân viên đã liên kết) hoặc IT báo trực tiếp. NS **không** tự cấp quyền.
- **Đổi chức danh:** nhóm gợi ý của chức danh mới khác nhóm hiện tại → tự có yêu cầu **Đổi nhóm quyền** cho IT (người đó giữ quyền cũ trong lúc chờ).
- **Cho nghỉ việc** (UI-6.36 – hồ sơ › ⋯): **Ngày nghỉ việc\*** (mặc định hôm nay, chọn được ngày tương lai) · **Lý do\*** (Xin nghỉ / Hết hợp đồng / Kỷ luật / Khác) · Ghi chú. Hộp **Hệ thống sẽ** liệt kê trước: khóa tài khoản, gỡ liên kết bot, hủy các ca đã phân **sau** ngày nghỉ (kèm ngày – báo người phân ca qua bot), chuyển yêu cầu duyệt đang giữ. **Xác nhận nghỉ việc**: ngày nghỉ hôm nay → làm ngay; ngày tương lai → hồ sơ ghi "Nghỉ từ dd/mm", tài khoản **tự khóa đúng ngày** (job 00:30), nút **Hủy hẹn nghỉ việc**. Ngày + lý do ghi ở tab Lịch sử.
- **Tạm nghỉ / Quay lại làm** (hồ sơ › ⋯): tạm nghỉ khóa tài khoản + bot; quay lại → yêu cầu **Mở lại tài khoản** cho IT. Form Sửa **không** đổi trạng thái.
- **Sửa họ tên / SĐT / ngày sinh / giới tính / địa chỉ** ở màn nào (Nhân viên hay hồ sơ cá nhân của tài khoản) cũng ghi cả hai nơi.
- **Tab Đối chiếu tài khoản** (màn **Nhân viên**, tab thứ 2 – NS + IT): nhân viên **chưa gắn** người, chia 3 nhóm như script chuyển dữ liệu: **Khớp chắc** (script tự gắn khi chạy thật) · **Nhiều ứng viên** (vd. 2 người trùng SĐT, các cách đoán ra người khác nhau) · **Không khớp** (chưa có tài khoản). Mỗi ứng viên ghi vì sao khớp (liên kết bot / tên đăng nhập = mã NV / trùng SĐT) → **Gắn**; hoặc **Chọn tài khoản…** (tài khoản nội bộ chưa gắn ai); nhân viên chưa có tài khoản → **Hồ sơ** › **Đề nghị cấp tài khoản**. Người đã gắn nhân viên khác không gắn lần 2. Cần quyền **Sửa** màn Nhân viên **và** **Sửa** màn Tài khoản.
- Trong thời gian chuyển đổi (1 bản phát hành) tài khoản chưa gắn vẫn được **đoán tạm** theo cách cũ để không gián đoạn chấm công; người dùng thấy ở **Việc của tôi** thông báo *Tài khoản chưa gắn với nhân viên* → NS gắn ở tab Đối chiếu.

### 4.1 Phân ca – `hr-shift-assign` (UI-6.37 – tuần Thứ 2 – CN)
- **Đầu màn:** ◀ **Tuần dd/mm – dd/mm** ▶ (mặc định tuần hiện tại; nút **Tuần này**, **Tuần sau**). Ngày đã qua mờ và khóa. Nhãn trạng thái tuần: *Chưa phân ca* / *Nháp · N ca chưa chốt* / *Có N thay đổi chưa gửi* / *Đã chốt & gửi*.
- **4 thẻ:** Ca đã phân (tuần) · **Ca thiếu người** (số cặp ngày × ca × tổ dưới định biên – bấm để cuộn tới dòng tổng đỏ) · Người chưa có ca (từ hôm nay, không nghỉ phép) · Ca chưa chốt.
- **Lọc:** tổ / đơn vị (gõ để tìm, cây cha → con), chức vụ, tìm tên / mã. Mở từ hồ sơ nhân viên (**Phân ca**) → chỉ hiện người đó (chip **Chỉ một nhân viên ✕**).
- **Lưới:** dòng = nhân viên nhóm theo tổ; 7 cột ngày; cột **Tuần** = số ca / giờ công + cảnh báo cam khi ≥ 6 ca hoặc không có ngày nghỉ (chỉ cảnh báo). Ô: **nháp** viền đứt · **đã chốt** nền xanh · **✓** đã vào ca · **vắng** đỏ · **Phép** cam · **…** đơn nghỉ chờ duyệt · **⇄** đang có đơn đổi ca · viền cam = **đổi sau khi chốt** (chưa gửi). Rê chuột xem chi tiết.
- **Dòng "Đã phân / cần"** dưới mỗi tổ: mỗi ngày từng ca "S 8/10" – đỏ khi thiếu, xanh khi đủ; tổ chưa cấu hình định biên chỉ hiện số đã phân.
- **Tô ca:** chọn một nút ca (mã + giờ) hoặc **Bỏ ca**, rồi **bấm hoặc kéo** qua nhiều ô – mỗi ô một chạm, hệ thống lưu theo lô khi thả chuột / sau 1 giây; ô không đổi được (ngày đã qua, nghỉ phép đã duyệt, đã chấm công, đang đổi ca) trở lại như cũ và báo lý do. **Thôi tô** → bấm ô mở hộp chọn ca như trước.
- **Sao chép tuần trước:** chép ca theo thứ sang tuần đang xem, **chỉ điền ô trống**; bỏ qua ngày đã qua, nghỉ phép, người đã nghỉ việc (liệt kê ô bỏ qua). Chạy lại không tạo thêm.
- **Phân ca hàng loạt:** chọn ca, khoảng ngày (mặc định tuần đang xem từ hôm nay), thứ không phân; chọn người nhanh **Cả tổ …** / **Tất cả <chức vụ>** + ô tìm. Ô đã có ca / ngày phép giữ nguyên.
- **Chốt & gửi lịch:** ca nháp → đã chốt, mỗi người nhận lịch qua bot. Sau khi chốt mà sửa / bỏ ô → ô viền cam, nút đổi thành **Gửi N thay đổi**: mỗi người một tin "🔁 Lịch ca của bạn thay đổi" (ca cũ → ca mới / đã hủy ca); bấm lại không gửi trùng.
- **Thứ 6 15:00:** tuần sau còn ca nháp / thiếu người / chưa phân → bot nhắc trưởng đơn vị + người phân ca (1 tin / tuần).
- **Điện thoại:** xem **theo ngày** (◀ ngày ▶): mỗi ca một khối "giờ · n/cần người" + danh sách người.

### 4.2 Danh mục ca – `hr-shift`
**Tạo mới**: Mã, Tên, Giờ bắt đầu, Giờ kết thúc (khác nhau, trong 00:00–23:59; ca qua đêm kết thúc ngày hôm sau). Ca ngừng dùng không phân được.

### 4.3 Tự chấm công – **Việc của tôi** (`hr-my`) › **Chấm công hôm nay** (hoặc `hr-attendance` › **Ca hôm nay của tôi** nếu có quyền vào màn)
1. Khối hiển thị tên, mã NV và bảng ca của bạn (Ngày, Ca, Giờ kế hoạch, Vào ca, Ra ca, Trạng thái – có nhãn hợp lệ / **Ngoài bán kính**, "muộn N′").
2. Bấm **Vào ca** (hiện từ 120′ trước giờ ca) → trình duyệt hỏi quyền vị trí → cho phép → *"Đã vào ca …"*.
3. Hết ca bấm **Ra ca**.
- Vị trí phải trong **150 m** quanh trạm bếp; ngoài bán kính vẫn ghi nhưng đánh dấu chưa hợp lệ.
- *Tài khoản chưa gắn với nhân viên…* → nhờ NS đặt mã nhân viên = tên đăng nhập (hoặc liên kết tài khoản).

### 4.4 Chấm công hộ / chốt vắng / bảng công – `hr-attendance`
- **Chấm công hộ** (cần quyền Thêm): **Nhân viên***, **Loại*** (Vào ca / Ra ca), **Lý do chấm hộ*** → **Ghi nhận**. Ghi thêm một lần chấm – không sửa, không xóa được.
- **Chốt vắng** (cần quyền sửa Phân ca): chọn **Ngày*** → **Đánh vắng** → mọi ca đã hết giờ mà không vào ca chuyển **Vắng** (không hoàn tác). Hằng ngày 08:00 hệ thống tự làm cho ngày hôm qua.
- **Bảng công**: chọn từ ngày → đến ngày (≤ 62 ngày), đơn vị → bảng theo nhân viên: Mã NV, Nhân viên, Đơn vị, Ca được phân, Có mặt, Đi muộn, Về sớm, Vắng, Nghỉ phép, Giờ công… → bấm dòng → **Bảng công chi tiết** từng ngày (giờ vào / ra, hình thức GPS / QR / Máy chấm công / Chấm hộ, hợp lệ).
- Danh sách sự kiện chấm công bên dưới chỉ xem (không sửa / xóa).

### 4.5 Đơn nghỉ phép – `hr-leave`
1. **Tạo đơn** → Nhân viên (trống = chính bạn; cho người khác cần quyền Thêm), **Từ ngày**, **Đến ngày** (≥ từ ngày, ≤ 30 ngày / đơn, lập lùi ≤ 7 ngày), **Loại nghỉ** (Nghỉ phép năm / Nghỉ ốm / Việc riêng có lương / Nghỉ không lương / Thai sản / Khác), **Lý do*** → lưu → *"Đã tạo đơn và gửi duyệt"* (số `DN-…`, Chờ duyệt).
2. Dòng **Chờ duyệt**: **Hủy đơn** (lý do hủy). Bấm dòng → ngăn chi tiết mở thẳng tab **Xử lý đơn**: số đơn, trạng thái, nhân viên, loại, thời gian (số ngày), lý do, cấp duyệt, **Ca trong khoảng nghỉ**, cảnh báo (*Duyệt đơn sẽ bỏ ca … đã phân*), nút **Hủy đơn / Từ chối / Duyệt** theo quyền.
3. Duyệt → ca trong khoảng nghỉ **tự bỏ**; nếu ca thiếu người → cảnh báo *"Ca … thiếu người … – cần phân người thay"*.
- Chỉ xóa được đơn Từ chối / Đã hủy; không sửa đơn.

### 4.6 Đơn đổi ca – `hr-swap`
1. **Tạo đơn** → **Người nhận** (Target), **Ca hiện tại** (ca của người xin), **Ca đề xuất** (ca của người nhận – để trống = **Nhường ca**; chọn = **Hoán đổi 2 ca**), **Lý do*** → lưu (Chờ duyệt).
2. Chi tiết: Hình thức, Người xin đổi, Người nhận, Ca của người xin, Ca đề xuất đổi, cấp duyệt; nút như đơn nghỉ.
3. Duyệt → hệ thống đổi người trên ca; nếu không thực hiện được → *"Duyệt sẽ không thực hiện được: …"*.

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| Ca được phân | Kế hoạch → Xác nhận → Đã vào ca → Hoàn thành; Vắng |
| Đơn nghỉ / đổi ca | Chờ duyệt → Đã duyệt / Từ chối; Đã hủy |
| Chấm công (hình thức) | GPS · QR · Máy chấm công · Chấm hộ |

## 6. Lỗi / cảnh báo thường gặp

### 6.1 Thông báo
| Thông báo | Cách xử lý |
|---|---|
| Không phân ca cho ngày đã qua | Chỉ phân từ hôm nay |
| Ca … đã … / đã có chấm công – không đổi / bỏ được, dùng đơn đổi ca | Lập đơn đổi ca |
| … nghỉ phép ngày … (đơn … đã duyệt) – không phân ca | Phân người khác |
| Ca … đang có đơn đổi ca chờ duyệt – xử lý đơn trước | Duyệt / từ chối đơn trước |
| Khoảng ngày không hợp lệ … tối đa N ngày | Phân ca ≤ 31 ngày; bảng công ≤ 62 ngày |
| Ngoài bán kính bếp: cách trạm … m (cho phép 150 m) | Đến đúng bếp rồi chấm; hoặc nhờ chấm hộ |
| Không có ca được phân để vào ca lúc này … – ca gần nhất …, vào ca được từ … | Chờ tới giờ, hoặc nhờ NS phân ca |
| … đang ở ca … (đã vào ca, chưa ra ca) | Ra ca trước |
| … chưa vào ca nào – không ra ca được | Vào ca trước |
| Chấm hộ phải ghi lý do | Nhập lý do |
| Không có quyền … cho nhân viên khác (cần quyền Thêm trên màn …) | Xin quyền / tự chấm |
| Đơn nghỉ chỉ được lập lùi tối đa 7 ngày / … tối đa 30 ngày / đơn | Sửa ngày |
| … đã có đơn nghỉ … trùng khoảng ngày | Hủy đơn cũ trước |
| … đã chấm công / đã vào ca … – không xin nghỉ ngày đó | Bỏ ngày đó khỏi đơn |
| … đã có ca khác ngày … – không nhận thêm ca | Chọn người nhận khác / hoán đổi |
| Người nhận phải khác người xin đổi ca / Hai ca giống nhau… | Chọn lại |
| Đơn … không còn ở trạng thái Chờ duyệt | Đơn đã được xử lý |
| Không lấy được vị trí – hãy cho phép trình duyệt dùng định vị rồi thử lại | Bật định vị |

### 6.2 Ai tự chấm công được trên web?
Mọi tài khoản đã gắn với nhân viên: menu **Nhân sự › Việc của tôi** (`hr-my`) có **Chấm công hôm nay** (Vào ca / Ra ca), **Tạo đơn nghỉ**, và nút **Đổi ca** trên từng ca của mình – không cần quyền màn `hr-attendance` / `hr-leave` / `hr-swap` (D-69). Các màn `hr-*` (xem bảng công, đơn của mọi người, lập hộ) vẫn theo ma trận quyền. Ngoài web vẫn chấm được bằng bot (📍 Chấm công).

## 7. Trên bot Telegram

- **📍 Chấm công** (`/chamcong`, nút cố định) – bot xin vị trí → bấm nút **gửi vị trí** của Telegram (không chuyển tiếp vị trí cũ; vị trí quá 120 giây bị từ chối). QR tại bếp (`?start=CHAMCONG`) cũng mở chấm công.
- **⚡ Ca của tôi** – ca hôm nay, giờ, trạng thái vào / ra.
- 📅 **Lịch ca** (`/lichca`) – lịch 7 ngày tới.
- 🏖 **Xin nghỉ** (`/nghi`) – chọn loại nghỉ → ngày bắt đầu (**Hôm nay / Ngày mai / Ngày kia / Ngày khác…** gõ dd/MM) → số ngày → gõ lý do → đơn gửi duyệt.
- 🔁 **Đổi ca** (`/doica`) – chỉ cho người có tài khoản web.
- 📝 **Đơn của tôi** (`/donct`) – đơn nghỉ / đổi ca của tôi và trạng thái.
- 👥 **Ai chưa vào ca** (`/chuavao`) – NS / trưởng đơn vị: danh sách chưa vào ca; nút **✍️ Chấm hộ** từng người (gõ lý do).
- Thông báo: 🟡 ca đã xác nhận, 🟡 kết quả duyệt đơn, 🟡 người chưa vào ca sau 10′, 16:00 nhắc bàn giao người nghỉ ngày mai.
