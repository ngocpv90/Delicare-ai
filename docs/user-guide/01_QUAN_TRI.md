# 01 – Quản trị hệ thống (IT / quản trị)

## 1. Bạn là ai

- Nhóm quyền: **FIN-IT** (3.4 IT / Hệ thống và Dữ liệu). Tài khoản có cờ **Quản trị** (IsAdmin) có mọi quyền và thấy thêm một số nút chỉ dành cho quản trị.
- Bot dùng: **Vận hành** (mục 🖥 Quản trị → 🖥 Tình trạng hệ thống – chỉ tài khoản quản trị).
- Việc chính: tạo / khóa tài khoản, phân quyền theo nhóm, sắp xếp menu, cây tổ chức, kho tài liệu, theo dõi job và nhật ký.

## 2. Màn hình được dùng

Menu **Quản trị hệ thống** (FIN-IT: xem + sửa + xuất):

| screen-id | Mục menu | Dùng để |
|---|---|---|
| `adm-user` | Người dùng | Tài khoản: tạo, khóa / mở, đặt lại mật khẩu, xem quyền hiệu lực |
| `adm-usergroup` | Nhóm người dùng | Nhóm quyền (vai trò) |
| `adm-permission` | Vai trò & phân quyền | Phân quyền |
| `adm-objectmenu` | Menu chức năng hệ thống | Thứ tự, nhóm của mục menu |
| `adm-orgunit` | Chi nhánh, đơn vị tổ chức | Cây đơn vị |
| `adm-employee` | Nhân sự | Nhân viên (danh mục chung với Nhân sự) |
| `adm-position` | Chức vụ | Chức vụ |
| `adm-company` | Công ty | Pháp nhân |
| `adm-admin-unit` | Đơn vị hành chính | Vùng / tỉnh / huyện / xã |
| `adm-document` | Quản lý tài liệu | Tải lên / tải xuống tệp đính kèm |
| `adm-audit` | Audit log | Nhật ký thay đổi dữ liệu |
| `adm-access-log` | Nhật ký truy cập | Nhật ký máy truy cập (IP / tên máy) |
| `adm-merge-conflict` | Xung đột hợp nhất dữ liệu | Đánh dấu đã xử lý xung đột chuẩn hóa dữ liệu |
| `sys-import` | Nhập dữ liệu gốc (Excel) | Nạp danh mục từ file Excel (khách hàng, điểm giao, NCC, nguyên liệu, kho, nhân viên, xe, món, định mức, bảng giá NCC…) |

| `adm-reference` | Danh mục dùng chung | **Chỉ xem** các giá trị chọn sẵn (loại suất, loại kho, kênh phản ánh…) và màn / cột đang dùng. Thêm / sửa giá trị = sửa cấu hình máy chủ (liên hệ quản trị hệ thống) |

Các màn `dbx-explorer`, `dbx-gap` **đang ẩn** ở chế độ dữ liệu thật. Nhóm **Tích hợp** (`int-*`) hiện theo ma trận quyền.

**Dọn menu (MENU-4, 2026-11-23):** bảng con không còn mục menu riêng – nhập / xem trong màn chính, quyền theo màn chính (ma trận quyền không còn hiện các mục này):

| Mục đã ẩn | Nhập / xem ở |
|---|---|
| Loại suất & đơn giá | Form + tab chi tiết **Hợp đồng** |
| Chi tiết đơn đặt suất | **Đơn đặt suất** / Đăng ký suất tuần |
| Chi tiết định lượng · Chi tiết thực đơn | **Công thức chế biến** · **Thực đơn tuần** |
| Chi tiết đề nghị mua / đơn mua / phiếu nhập / phiếu xuất / kiểm kê | Màn chứng từ tương ứng (danh sách có **Lọc theo nguyên liệu**) |
| Chi tiết kế hoạch sản xuất · Chi tiết kiểm tra hàng nhập | **Kế hoạch sản xuất** · **Kiểm tra hàng nhập** |
| Tiêu chí kiểm tra | **Bộ kiểm tra ATTP** → tab **Tiêu chí** |
| Điểm dừng · Diễn biến giao hàng · Lịch sử xử lý phản ánh | **Chuyến giao hàng** · **Theo dõi chuyến giao** · **Phản ánh khách hàng** |
| Các bước duyệt · Lịch sử duyệt · Chứng từ trong yêu cầu duyệt | Chi tiết yêu cầu trong **Hộp phê duyệt** |
| Phiếu chuyển kho · Phiếu điều chỉnh tồn | Nút **Chuyển kho** / **Điều chỉnh tồn** trên màn **Tồn kho** (duyệt điều chỉnh vẫn ở Phê duyệt › Phiếu điều chỉnh tồn) |
| Thông tin cá nhân | **Tài khoản** / **Nhân viên** |
| Hộp phê duyệt (nhóm Nhân sự) | **Phê duyệt › Tất cả yêu cầu duyệt** (cùng màn; dòng quyền `#/wf-inbox` giữ nguyên – vẫn cấp ở ma trận quyền) |

Chỉ xem (bỏ Thêm / Sửa tay – dữ liệu do nghiệp vụ ghi): **Sản lượng** (ghi khi kết thúc mẻ nấu), **Xếp hàng lên xe** và **Xác nhận đã giao** (Theo dõi chuyến / bot), **Liên kết tài khoản chat** (mã liên kết; nút Liên kết / Gỡ liên kết).

## 3. Việc theo thứ tự

| Khi nào | Việc |
|---|---|
| Trước go-live | Cây tổ chức → nhóm quyền → ma trận quyền → tài khoản → kiểm quyền hiệu lực từng vai |
| Nhân viên mới | Tạo tài khoản, lấy mật khẩu tạm, gửi cho người dùng |
| Nhân viên nghỉ / mất máy | Khóa tài khoản |
| Quên mật khẩu | Đặt lại mật khẩu |
| Hằng ngày | Xem bot 🖥 Tình trạng hệ thống (job lỗi, tin gửi lỗi); xử lý xung đột dữ liệu |
| Khi cần | Làm mới KPI (màn Chỉ số theo ngày / tháng – xem `13_BAN_DIEU_HANH.md`) |

## 4. Thao tác từng bước

### 4.1 Tạo tài khoản – `adm-user`
1. Mở **Quản trị hệ thống › Người dùng** → **Tạo người dùng**.
2. Nhập **Tên đăng nhập**, chọn **Hồ sơ người** có sẵn – hoặc bấm **Lấy từ nhân viên** (gõ mã / tên để tìm): hệ thống tạo hồ sơ mới theo họ tên, SĐT, ngày sinh, giới tính, địa chỉ của nhân viên và gợi ý tên đăng nhập (vd. *an.nguyen*) → **Tạo người dùng**.
   - Tên đăng nhập chỉ gồm chữ thường không dấu, số, `.` `_` `-`, 3–50 ký tự.
   - Mỗi hồ sơ cá nhân chỉ có 1 tài khoản.
3. Tạo xong hiện **mật khẩu tạm** một lần (bấm **Sao chép**). Người dùng đăng nhập bằng mật khẩu tạm sẽ gặp ngay hộp **Đổi mật khẩu** bắt buộc – chưa đổi thì không dùng được màn nào (chỉ có nút Đăng xuất).
4. Gán nhóm quyền: màn **Người dùng** (`adm-user`) → **Tạo người dùng** có ô **Nhóm quyền** (chọn nhiều); tạo xong hiện **mật khẩu tạm** một lần (nút **Sao chép**, hết hạn 24 giờ, người dùng phải đổi khi đăng nhập). Tài khoản đã có: nút **Gán nhóm** trên dòng (chọn lại danh sách nhóm). Sửa tài khoản bằng nút Sửa thông thường không làm mất nhóm.

**4.1b Đặt lại mật khẩu (chỉ tài khoản quản trị):**
1. Trên dòng tài khoản bấm nút hình chìa khóa (**Đặt lại mật khẩu**) → xác nhận *"Đặt lại mật khẩu của tài khoản …? Mật khẩu hiện tại sẽ không dùng được nữa."*
2. Hộp **Mật khẩu tạm** hiện mật khẩu – bấm **Sao chép**. Mật khẩu chỉ hiện **một lần**, hết hạn sau **24 giờ**, người dùng phải đổi khi đăng nhập.

### 4.2 Khóa / mở khóa tài khoản – `adm-user`
1. Trên dòng tài khoản bấm **Khóa** (hoặc **Mở khóa**) → xác nhận.
2. Người bị khóa không đăng nhập được; bot Telegram cũng ngừng phục vụ người đó.
3. Không tự khóa được tài khoản đang đăng nhập: *Không thể tự khóa tài khoản của chính mình*.

### 4.3 Xem quyền hiệu lực của một tài khoản – `adm-user`
Bấm nút hình khiên (**Quyền hiệu lực của tài khoản**) trên dòng → bảng cây menu × 7 quyền (Truy cập, Thêm, Sửa, Xóa, In, Xuất, Nhập), cộng dồn mọi nhóm của tài khoản. Tài khoản quản trị: "có mọi quyền".

### 4.4 Nhóm quyền – `adm-usergroup`
- **Tạo mới** nhóm: tên nhóm = mã vai trò (CEO, COO, OPS-PLAN, OPS-SCM, OPS-KIT, OPS-LOG, OPS-POS, OPS-MNT, SAL-SALES, SAL-CON, SAL-CS, FIN-ACC, FIN-HR, FIN-IT, QA, CEO-PMO).
- Nút **Phân quyền** trên dòng → sang ma trận quyền của nhóm.
- Nút hình khiên **Xem trước quyền của nhóm** (quản trị) → danh sách màn được truy cập + các tài khoản thuộc nhóm.

### 4.5 Phân quyền – `adm-permission`
1. Cột trái: chọn nhóm (hiện số chức năng được truy cập, số tài khoản).
2. Bảng phải: mỗi mục menu cấp 2 × 7 ô quyền. Tick / bỏ tick → lưu ngay ("Cấp … / Thu hồi …").
   - Tick bất kỳ quyền nào khác → tự bật **Truy cập**.
   - Bỏ **Truy cập** → tắt hết quyền còn lại của mục đó.
3. **Cấp toàn bộ truy cập** / **Thu hồi toàn bộ** cho cả nhóm (thu hồi tắt cả Thêm / Sửa / Xóa / In / Xuất / Nhập) → xác nhận.
4. **Sao chép quyền từ nhóm khác**: chọn nhóm nguồn → **Sao chép** → xác nhận *"Thay toàn bộ quyền của nhóm … bằng quyền của nhóm …?"*.
5. **Xem trước quyền của nhóm** (quản trị): kiểm lại kết quả.

**Quyền Duyệt** không phải cột riêng: mỗi loại duyệt là một mục menu **"Phê duyệt › …"** (nhóm Phê duyệt). Cấp **Truy cập + Sửa** trên mục đó = được duyệt loại đó. Danh sách mục duyệt:

| Mục quyền | Duyệt gì |
|---|---|
| Phê duyệt › Hợp đồng (`crm-contract`) | Hợp đồng |
| Phê duyệt › Đơn đặt suất (`crm-order`) | Lệch suất > 30% và sửa / thêm đơn sau giờ chốt |
| Phê duyệt › Chính sách giá (`crm-price-policy`) | (có mục quyền) |
| Phê duyệt › Công thức chế biến (`menu-recipe`) | Phiên bản công thức |
| Phê duyệt › Thực đơn tuần (`menu-menu`) | (có mục quyền) |
| Phê duyệt › Yêu cầu đổi món (`menu-change-request`) | Đổi món |
| Phê duyệt › Đề nghị mua hàng cấp 1 / cấp 2 (`scm-pr-l1`, `scm-pr-l2`) | Đề nghị mua 2 cấp |
| Phê duyệt › Đơn mua hàng (`scm-po`) / (Giám đốc) (`scm-po-director`) | PO dưới / từ 50 triệu |
| Phê duyệt › Phiếu kiểm kê / (cấp 2 – Kế toán) | Kiểm kê lệch > 2% |
| Phê duyệt › Phiếu điều chỉnh tồn (`scm-adjustment`) | Điều chỉnh tồn tay |
| Phê duyệt › Kế hoạch sản xuất (`prod-plan`) | Nút **Duyệt** kế hoạch SX |
| Phê duyệt › Kiểm tra hàng nhập (`qc-inspection`) | (có mục quyền) |
| Phê duyệt › Đơn xin nghỉ / Đơn đổi ca | Nghỉ, đổi ca |
| Phê duyệt › Tất cả yêu cầu duyệt (`wf-inbox`) | Xem hộp duyệt |

Quyền dùng bot: mục **Bot Telegram – Vận hành** (`#/bot-van-hanh`) và **Bot Telegram – Điều hành** (`#/bot-dieu-hanh`). Chức năng trong bot đi theo quyền của màn web tương ứng.

### 4.6 Menu chức năng – `adm-objectmenu`
1. Khối **Cây menu chức năng** hiện menu 2 cấp.
2. Nút ↑ / ↓ trên mỗi mục để đổi thứ tự trong cùng nhóm.
3. **Chuyển nhóm** (mục cấp 2) → chọn nhóm đích → **Chuyển**.
4. Thay đổi có hiệu lực với người dùng **từ lần đăng nhập sau**.
- Lỗi: *Menu chỉ có 2 cấp…*, *Mục menu đang có mục con nên phải là mục cấp 1*, *Sắp xếp tạo vòng lặp cha – con*.

### 4.7 Sơ đồ tổ chức – `adm-orgunit`
- Khối trên: cây đơn vị (tên, loại, mã, quản lý, số nhân viên, số kho). Bấm tên để mở chi tiết.
- Thêm / sửa đơn vị bằng **Tạo mới** / **Sửa** ở bảng dưới. Lỗi: *Đơn vị cha không được là chính nó hoặc đơn vị con của nó*.
- Đơn vị loại **BRANCH** xuất hiện trong hộp **Đơn vị làm việc** ở đầu trang.

### 4.8 Kho tài liệu – `adm-document`
1. **Tải tệp lên** → chọn tệp (pdf, jpg, jpeg, png, xlsx, docx; tối đa 20 MB).
2. Nút tải xuống trên mỗi dòng.
- Lỗi: *Chỉ nhận tệp …*, *Tệp vượt quá 20 MB*, *Nội dung tệp không khớp đuôi*, *Tệp không còn trên máy chủ – báo quản trị kiểm tra thư mục DocumentStorage:RootPath*.

### 4.8b Nhập dữ liệu gốc từ Excel – `sys-import`
Menu **Quản trị hệ thống → Nhập dữ liệu gốc (Excel)**. Mỗi người chỉ thấy loại file mà mình có quyền **Nhập (Import)** ở màn danh mục tương ứng (vd. Khách hàng cần quyền Nhập ở `crm-customer`).
1. Bấm **File mẫu** ở dòng danh mục → mở file, xóa dòng ví dụ `[DEMO]`, điền dữ liệu. Cột có dấu `*` là bắt buộc; rê chuột lên tên cột để xem gợi ý.
2. Bấm **Nạp file** → chọn file `.xlsx` / `.xlsm` → xác nhận.
3. Hộp **Kết quả nạp**: số dòng đọc, thêm mới, cập nhật, bỏ qua, và bảng **Dòng lỗi** (số dòng Excel + lý do). Dòng lỗi không được ghi – sửa trong file rồi nạp lại.
- Nạp lại cùng file không tạo trùng (khớp theo mã). Ô mã để trống → hệ thống tự cấp mã theo quy tắc (`91_PHU_LUC.md` §2).
- Lỗi: *Chỉ nhận tệp .xlsx / .xlsm*, *Tài khoản không có quyền thao tác này (403)* (thiếu quyền Nhập ở màn danh mục).

### 4.8c Danh mục chọn sẵn (loại suất, loại kho, loại khay…)
Các trường dưới đây **chỉ chọn trong danh sách**, không gõ chữ tự do – trên mọi form, khi nhập Excel và khi gọi API:

| Danh mục | Dùng ở | Giá trị |
|---|---|---|
| Loại suất | Hợp đồng, dự báo, thực đơn, đơn đặt suất, chính sách giá | Sáng · Trưa · Tối · Đêm |
| Loại kho | Kho | Nguyên liệu · Thành phẩm · Bao bì · Kho lạnh · Kho bếp |
| Kiểu bảo quản | Nguyên liệu | Nhiệt độ thường · Mát · Đông lạnh · Khô |
| Loại nhà cung cấp | Nhà cung cấp | Thịt · Rau củ · Hàng khô · Bao bì · Khác |
| Loại khay / hộp | Kết quả đóng gói, thu hồi khay, sổ bao bì | Khay inox · Hộp giữ nhiệt · Thùng giữ nhiệt |
| Kênh tiếp nhận / Phân loại | Phản ánh khách hàng | Điện thoại · Zalo · Email · Trực tiếp · Telegram · Biên bản giao nhận / Chất lượng món · Thiếu suất · Giao trễ · Vệ sinh – ATTP · Bao bì · Thái độ phục vụ · Khác |
| Loại hủy, loại đo nhiệt độ, loại nghỉ, hình thức chấm công, loại phát sinh bao bì | Màn tương ứng | Theo danh sách trên màn hình |

- Thêm / đổi giá trị: **bộ phận IT** sửa mục `ReferenceLists` trong cấu hình máy chủ (`appsettings.json`) rồi khởi động lại – không cần sửa phần mềm. Nhập Excel / API nhận cả **mã** lẫn **tên** (vd. "Kho lạnh" = COLD), lưu mã.
- Bản ghi cũ có giá trị ngoài danh sách: ô chọn hiện "(giá trị cũ)"; vẫn sửa được các trường khác, chỉ không đổi sang giá trị lạ mới. IT chuẩn hóa một lần bằng script `DB/scripts/REF1_normalize_catalog_codes.sql` (chạy thử trước, sau khi sao lưu).
- Lỗi: *Loại kho «…» không có trong danh mục (RAW – Nguyên liệu, …)* → chọn lại trong danh sách.

### 4.9 Xung đột dữ liệu – `adm-merge-conflict`
1. Dòng chưa xử lý có nút **Xử lý** → nhập **Cách xử lý** (bắt buộc, ≤ 1000 ký tự) → **Đã xử lý**.
2. Sau khi lưu không sửa lại được. Lỗi: *Xung đột này đã được xử lý*.

### 4.10 Nhật ký – `adm-audit`, `adm-access-log`
Chỉ xem / lọc / xuất. Audit log ghi mọi thêm / sửa / xóa; tab **Lịch sử xử lý** của từng chứng từ đọc từ đây.

## 5. Trạng thái

| Đối tượng | Trạng thái |
|---|---|
| Tài khoản | Đang hoạt động / Bị khóa (nút Khóa / Mở khóa) |
| Liên kết Telegram | Hoạt động / Thu hồi / Bị chặn (người dùng chặn bot) |
| Mục menu | Đang bật / Đang tắt |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| UserName … đã tồn tại | Chọn tên đăng nhập khác |
| Tên đăng nhập chỉ gồm chữ thường không dấu… | Sửa tên đăng nhập |
| Hồ sơ này đã có tài khoản đăng nhập (…) | Mỗi hồ sơ 1 tài khoản – dùng tài khoản cũ |
| Không tìm thấy nhóm người dùng | Nhóm đã xóa – tải lại trang |
| Đường dẫn … đã tồn tại | MetaLink menu trùng – sửa đường dẫn |
| Người dùng báo "không có quyền truy cập màn hình này" | Cấp **Truy cập** cho nhóm của họ ở `adm-permission`; người dùng tải lại trang |
| Người dùng có quyền mà nút vẫn mờ | Thiếu quyền Thêm / Sửa / Xuất của màn đó |
| Người dùng không duyệt được | Cấp mục **Phê duyệt › …** đúng loại (Truy cập + Sửa) |
| … «…» không có trong danh mục (…) | Giá trị phải chọn trong danh sách – xem 4.8c; cần giá trị mới → IT bổ sung danh mục |

## 7. Trên bot Telegram

- 🖥 **Tình trạng hệ thống** (`/hethong`, chỉ quản trị): job tự động + tin gửi lỗi.
- Nhận cảnh báo 🔴 job lỗi, 🔴 tin gửi thất bại nhiều lần.
- Gỡ liên kết Telegram của người dùng: hiện chưa có màn web (màn `int-identity` đang ẩn) – người dùng tự gửi `/huylienket` trên bot, hoặc IT gọi API.
