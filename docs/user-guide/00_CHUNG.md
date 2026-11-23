# 00 – Thao tác chung trên web DELICARE

> Áp dụng cho mọi vai trò. Đọc phần này trước rồi mới đọc file của vai trò mình.
> Viết theo code ngày 2026-11-19 (giao diện `app-delicare-client`, chế độ dữ liệu thật). Chỗ code chưa có hoặc khác tài liệu: xem `90_CHENH_LECH.md`.

## 1. Đăng nhập, đổi mật khẩu, đăng xuất

**Đăng nhập** – địa chỉ web của công ty, trang `/dang-nhap`.

1. Nhập **Tên đăng nhập**, **Mật khẩu**.
2. Nhập **Mã xác nhận**: gõ lại 4 ký tự hiện ở ô bên cạnh.
3. Bấm **Đăng nhập** → vào trang chủ (Dashboard điều hành nếu có quyền).

| Thông báo | Nguyên nhân | Cách xử lý |
|---|---|---|
| Vui lòng nhập tên đăng nhập / mật khẩu / mã xác nhận | Bỏ trống ô | Nhập đủ 3 ô |
| Mã xác nhận không đúng | Gõ sai mã 4 ký tự – mã mới được sinh, form bị xóa | Nhập lại cả 3 ô với mã mới |
| Tài khoản hoặc mật khẩu không đúng | Sai tên / mật khẩu | Nhập lại; quên mật khẩu → nhờ quản trị **Đặt lại mật khẩu** |
| Tài khoản của bạn đang bị khóa, vui lòng liên hệ với quản trị viên | Quản trị đã **Khóa** tài khoản | Liên hệ quản trị (IT) |
| Mật khẩu tạm đã hết hạn, liên hệ quản trị | Mật khẩu tạm quá 24 giờ chưa dùng | Nhờ quản trị đặt lại lần nữa |
| Không thể kết nối với server… | Mất mạng / máy chủ dừng | Kiểm tra mạng; báo IT |

**Đổi mật khẩu** – bấm tên / ảnh đại diện góc phải → hộp **Tài khoản** → **Đổi mật khẩu**.

1. Nhập **Mật khẩu hiện tại**, **Mật khẩu mới (từ 9 ký tự)**, **Nhập lại mật khẩu mới**.
2. Bấm **Lưu** → "Đã đổi mật khẩu".
3. Lỗi tại chỗ: *Mật khẩu mới không được trùng mật khẩu hiện tại*, *Mật khẩu nhập lại chưa khớp*, *Mật khẩu mới phải từ 9 ký tự*.

**Đăng xuất** – hộp **Tài khoản** → **Đăng xuất**.

## 2. Bố cục màn hình

| Vùng | Nội dung |
|---|---|
| Thanh menu trái | Nhóm chức năng (Bảng điều hành, Kinh doanh, Thực đơn & Món ăn, Mua hàng & Kho, Sản xuất & Giá thành, Chất lượng & ATTP, Đóng gói & Giao hàng, Nhân sự, Tích hợp & Chỉ số, Quản trị hệ thống). **Chỉ hiện mục bạn có quyền Truy cập.** Nút thu gọn menu ở đầu thanh; trên điện thoại bấm ☰. |
| Ô tìm kiếm trên cùng | Tìm chức năng + chứng từ (xem mục 4) |
| **Đơn vị làm việc** (góc trên) | Chọn chi nhánh / đơn vị → các màn lọc số liệu theo đơn vị đó. "Tất cả đơn vị" = không lọc |
| Chuông 🔔 | Cảnh báo & công việc (mục 5) |
| ❓ / **Hướng dẫn sử dụng** | Hộp trợ giúp ngắn. Nút **Chuyển sang dữ liệu DEMO** (dữ liệu mẫu trong trình duyệt, chỉ để trình diễn) chỉ hiện với tài khoản quản trị; lỡ bấm thì mở lại hộp và bấm **Chuyển về dữ liệu thật (API)** |
| Tên tài khoản (góc phải) | Thông tin, Đổi mật khẩu, Đăng xuất |

Mỗi màn có địa chỉ riêng `…/#/quan-tri/<screen-id>` (vd. `#/quan-tri/crm-order`). Có thể đánh dấu trang để mở nhanh. Màn bạn không có quyền sẽ báo *"Vai trò … không có quyền truy cập màn hình này"*.

## 3. Màn danh sách chung (dùng cho mọi danh mục và nhiều chứng từ)

Phần lớn màn có dạng danh sách giống nhau:

1. **Thẻ số liệu** đầu trang: Tổng bản ghi + 3 trạng thái nhiều nhất.
2. **Lọc nhanh:** các chip trạng thái (vd. "Nháp · 3") – bấm để lọc, **Tất cả** để bỏ lọc.
3. **Thanh công cụ bảng:** ô **Tìm trong N dòng…** (tìm theo mọi cột đang hiện), ô chọn lọc theo một cột tham chiếu (vd. Khách hàng: Tất cả).
4. Bấm tiêu đề cột để sắp xếp; cuối bảng chọn **5 / 10 / 20 / 50 dòng/trang** và chuyển trang.
5. Cột thao tác trên mỗi dòng: 👁 **Xem** (mở ngăn chi tiết), ✎ **Sửa** (bị mờ nếu không có quyền Sửa), 🗑 **Xóa** (chỉ hiện khi có quyền Xóa), cộng các nút nghiệp vụ riêng của màn (Gửi duyệt, Ghi sổ, In…).
6. Nút đầu trang:
   - **Tạo mới** – mở form nhập (mờ nếu không có quyền Thêm). Nhiều màn chứng từ **không có Tạo mới** vì chứng từ chỉ sinh qua nghiệp vụ (vd. phiếu nhập, mẻ, đơn giao) – xem file vai trò.
   - **Xuất CSV** – tải các dòng đang có trên màn (bỏ qua bộ lọc tìm kiếm). Cần quyền Xuất.
   - **Xuất Excel** – chỉ hiện với danh sách máy chủ hỗ trợ; xuất đủ dòng từ máy chủ, đúng quyền.

**Form Tạo mới / Chỉnh sửa:** ô có dấu * là bắt buộc; ô chọn danh mục liệt kê tối đa 400 dòng đầu. Bấm **Tạo mới** / **Lưu thay đổi**; **Hủy** để đóng. Lỗi trùng mã / tên: *Mã … đã tồn tại, vui lòng nhập mã khác*, *Tên … đã tồn tại…*. Xóa danh mục đang dùng: *Không thể xóa dữ liệu danh mục này do dữ liệu đang được sử dụng*.

**Ngăn chi tiết (bấm vào một dòng):** mở bên phải, gồm các tab:

| Tab | Nội dung |
|---|---|
| Thông tin | Toàn bộ trường của bản ghi (+ khối riêng của từng màn) |
| (tab riêng) | Tùy màn: Chi tiết dòng, Loại suất & đơn giá, Tiêu chí (bộ kiểm tra ATTP), Hành trình (đơn hàng), Sự kiện giao (đơn giao), Nguyên liệu & an toàn (mẻ)… |
| Liên quan | Bản ghi liên quan theo nhóm (khách hàng, hợp đồng, đơn hàng, NCC, nguyên liệu, PO, lô, món, nhân viên); bấm để mở, "Xem tất cả →" để sang màn danh sách |
| Lịch sử xử lý | Dòng thời gian: tạo, sửa, gửi duyệt, quyết định duyệt, lịch sử phản ánh, tệp đính kèm |

Chân ngăn: nút nghiệp vụ của màn + **Chỉnh sửa** (nếu được sửa) + **Đóng**. Phím `Esc` đóng hộp / ngăn đang mở.

**Sổ chỉ ghi thêm:** sổ kho, nhật ký nhiệt độ, chấm công, sổ bao bì, kết quả vệ sinh – **không có Sửa / Xóa**. Ghi sai thì ghi bút toán / dòng ngược lại hoặc hủy chứng từ gốc.

## 4. Tìm kiếm toàn cục

1. Gõ vào ô tìm kiếm trên cùng (không cần dấu: "don hang" tìm được "Đơn hàng").
2. Từ 1 ký tự: hiện **chức năng** khớp tên. Từ 2 ký tự: thêm **chứng từ / bản ghi** (khách hàng, hợp đồng, đơn hàng, NCC, nguyên liệu, PO, lô, món… – mỗi loại tối đa 5, theo quyền Xem).
3. Bấm một kết quả → mở màn và ngăn chi tiết của bản ghi. `Esc` xóa ô tìm.

Lỗi: *Từ khóa tìm kiếm phải từ … đến … ký tự*.

## 5. Chuông cảnh báo

Bấm 🔔 → ngăn **Cảnh báo & công việc**, cập nhật mỗi 60 giây, theo quyền Xem và đơn vị đang chọn. Mỗi dòng có **Xử lý →** để mở màn liên quan. Số trên mục menu **Yêu cầu phê duyệt** = số việc chờ chính bạn duyệt.

| Cảnh báo | Mở màn |
|---|---|
| Nguyên liệu dưới tồn tối thiểu | Số dư tồn kho `scm-inv-balance` |
| Yêu cầu chờ bạn duyệt (… đã quá hạn) | Yêu cầu phê duyệt `wf-inbox` |
| Phiếu nhập chờ kết luận QC | Phiếu kiểm tra QC `qc-inspection` |
| Lô nguyên liệu đang tạm giữ | Lô nguyên liệu `scm-lot` |
| Nhiệt độ ngoài ngưỡng (kể cả cảm biến IoT) | Nhật ký nhiệt độ `qc-temperature` |
| Phản ánh quá hạn xử lý / Phản ánh đang mở | Phản ánh khách hàng `log-complaint` |
| Lô sắp hết hạn | Lô nguyên liệu `scm-lot` |
| Tin nhắn gửi lỗi | (màn tin nhắn ra đang ẩn – báo IT) |

## 6. Duyệt và hủy yêu cầu duyệt (dùng chung)

Mọi chứng từ cần duyệt đều đi qua **Yêu cầu phê duyệt** (`wf-inbox`) – xem `12_QUAN_LY_DUYET.md`. Người gửi:
- Theo dõi ở tab **Lịch sử xử lý** của chứng từ hoặc tab **Tất cả** của hộp duyệt.
- **Hủy yêu cầu**: chỉ người gửi, khi còn chờ duyệt, bắt buộc ghi lý do.
- Sửa chứng từ sau khi đã gửi duyệt → *Nội dung chứng từ đã thay đổi sau khi gửi duyệt – gửi duyệt lại*.

## 7. In phiếu, tải tệp

- Nút **In** / **In phiếu** tải tệp PDF (hoặc Excel với biên bản giao hàng, thực đơn gửi khách, phiếu sản xuất…). Một số phiếu chỉ in được ở trạng thái nhất định: *Phiếu nhập … chưa có kết luận QC – chưa in được*, *Phiếu xuất … chưa ở trạng thái Đã xuất*, *Đối soát … còn Mở – chốt đối soát rồi mới in biên bản*.
- In hàng loạt (phiếu giao của cả chuyến): tối đa theo cấu hình – *In hàng loạt tối đa … phiếu / lần*.
- Phiếu in có **mã QR** mở đúng phiếu đó trên bot Telegram (xem `20_BOT_TELEGRAM.md`).

## 8. Mốc giờ tự động của hệ thống (giờ Việt Nam)

| Giờ | Việc hệ thống tự làm | Ảnh hưởng |
|---|---|---|
| 00:15 | Lô quá hạn dùng → **Hết hạn**, ghi hủy tồn | Kho, QC |
| 00:30 | Hợp đồng Hiệu lực quá ngày hết hiệu lực → **Hết hạn** | Kinh doanh |
| 07:00 | Bản tin sáng + Thẻ ngày (bot Điều hành) | Quản lý |
| 08:00 | Chốt **Vắng** các ca hôm qua không vào ca | Nhân sự |
| 16:00 | Nhắc người duyệt sắp nghỉ còn việc chờ duyệt (bot) | Người duyệt |
| 17:00 | Báo cáo cuối ngày (bot Điều hành) | Quản lý |
| 19:00 | Nhắc nhân viên KD: đơn ngày mai chưa xác nhận (bot) | Kinh doanh |
| **20:00** | **Chốt đơn**: đơn Đã xác nhận của ngày mai → **Đã chốt**; sau giờ này mọi thay đổi đơn ngày mai phải qua duyệt | Kinh doanh, Kế hoạch |
| 20:30 | Tính **nhu cầu nguyên liệu (MRP)** ngày mai + lập đề nghị mua nháp | Kế hoạch, Mua hàng |
| 21:00 | Gửi thủ kho nguyên liệu cần cho ngày mai (bot) | Kho |

Chỉ số theo ngày / tháng hiện **không chạy tự động** – quản trị bấm **Làm mới số liệu** trên màn KPI.

## 9. Mã chứng từ

Hệ thống tự sinh số, dạng `<tiền tố>-<YYMMDD>-<số>`:

| Tiền tố | Chứng từ |
|---|---|
| DH- / PN- / PX- / KK- / MH- / LS- / DG- / GH- / DM- / DC- / GD- | Đơn hàng / phiếu nhập / phiếu xuất / kiểm kê / đề nghị mua / lệnh SX / lệnh đóng gói / đơn giao / đổi món / đổi ca / giao dịch kho tay (trước 19/11/2026 dùng chung `PH-`) – bảng đủ: 91_PHU_LUC.md §2 |
| HD- | Hợp đồng |
| KS- | Kế hoạch sản xuất (1 kế hoạch / ngày; trước 19/11/2026: `KH-`) |
| PO- | Đơn mua hàng |
| LO- | Lô nguyên liệu (kèm mã NCC; trước 19/11/2026: `NL-`) |
| ME- | Mẻ nấu |
| CX- | Chuyến xe (kèm mã xe) |
| VS- | Mẻ vệ sinh khay / hộp |
| PA- | Phản ánh khách hàng |
| DN- | Đơn nghỉ |
| XN- | Yêu cầu phê duyệt |

## 10. Lỗi chung thường gặp

| Thông báo | Ý nghĩa / cách xử lý |
|---|---|
| Bạn không có quyền {thao tác} trên màn hình {màn} | Thiếu quyền Thêm / Sửa / Xóa / In / Xuất – nhờ quản trị cấp trên ma trận quyền |
| Bạn không có quyền duyệt loại yêu cầu này | Thiếu mục "Phê duyệt › …" tương ứng |
| Không tìm thấy bản ghi (hoặc đã xóa) | Dữ liệu đã đổi – tải lại trang (F5) |
| Chỉ {thao tác} khi {chứng từ} ở trạng thái {A} (hiện tại: {B}) | Thao tác không hợp với trạng thái hiện tại – xem bảng trạng thái của màn |
| … chỉ tạo / đổi trạng thái qua thao tác nghiệp vụ (…) | Không sửa trực tiếp được – dùng nút nghiệp vụ trên màn |
| Lý do là bắt buộc (3–1000 ký tự) / Nhập lý do hủy (tối thiểu 3 ký tự) | Mọi thao tác Hủy / Từ chối / Thanh lý đều phải ghi lý do |
| Tệp vượt quá … MB / Không nhận tệp … | Tệp đính kèm tối đa 20 MB, chỉ pdf, jpg, png, xlsx, docx |
| Màn hình trắng / "Không tải được dữ liệu" | Mạng chập chờn – F5; còn lỗi thì chụp màn hình gửi IT |
