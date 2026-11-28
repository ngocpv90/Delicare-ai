# 01 – Quản trị hệ thống (IT / quản trị)

## 1. Bạn là ai

- Nhóm quyền: **FIN-IT** (3.4 IT / Hệ thống và Dữ liệu). Tài khoản có cờ **Quản trị** (IsAdmin) có mọi quyền và thấy thêm một số nút chỉ dành cho quản trị.
- Bot dùng: **Vận hành** (mục 🖥 Quản trị → 🖥 Tình trạng hệ thống – chỉ tài khoản quản trị).
- Việc chính: tạo / khóa tài khoản, phân quyền theo nhóm, sắp xếp menu, cây tổ chức, kho tài liệu, theo dõi job và nhật ký.

## 2. Màn hình được dùng

Menu **Quản trị hệ thống** (FIN-IT: xem + sửa + xuất):

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Xem mọi màn nghiệp vụ; bảng dưới chỉ liệt kê mục có quyền hơn **Xem** (đủ ma trận: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | FIN-IT |
|---|---|---|
| Phê duyệt › Tài khoản | `wf-inbox?type=adm-user` | Duyệt |
| Điều hành › Kết quả kinh doanh | `dash-pl` | X (IX) |
| Điều hành › Trung tâm Agent | `ai-agents` | S |
| Điều hành › Giao việc | `ai-tasks` | S |
| Điều hành › Lịch chạy | `ai-schedule` | S |
| Danh mục hệ thống › Công ty & đơn vị | `adm-orgunit` | Q (IX) |
| Danh mục hệ thống › Danh mục dùng chung | `adm-reference` | Q (IX) |
| Danh mục hệ thống › Địa giới hành chính | `adm-admin-unit` | Q (IX) |
| Danh mục hệ thống › Danh mục chỉ số | `kpi-definition` | S (IX) |
| Tri thức › 95 Nguồn BOT | `knowledge?folder=95` | S |
| Tri thức › 99 Mẫu | `knowledge?folder=99` | S |
| Quản trị hệ thống › Tài khoản | `adm-user` | Q (IX) |
| Quản trị hệ thống › Nhóm quyền | `adm-usergroup` | Q (IX) |
| Quản trị hệ thống › Cấu hình menu | `adm-objectmenu` | Q (IX) |
| Quản trị hệ thống › Nhập dữ liệu gốc (Excel) | `sys-import` | Q (IX) |
| Quản trị hệ thống › Tài liệu đính kèm | `adm-document` | Q (IX) |
| Nhật ký & Tích hợp › Job tự động | `adm-job` | Q (IX) |
| Nhật ký & Tích hợp › Nhật ký thay đổi dữ liệu | `adm-audit` | S (IX) |
| Nhật ký & Tích hợp › Nhật ký truy cập | `adm-access-log` | Q (IX) |
| Nhật ký & Tích hợp › Dữ liệu trùng cần gộp | `adm-merge-conflict` | S (IX) |
| Nhật ký & Tích hợp › Kênh nhắn tin | `int-channel` | S (IX) |
| Nhật ký & Tích hợp › Bot | `int-bot` | S (IX) |
| Nhật ký & Tích hợp › Liên kết tài khoản chat | `int-identity` | S (IX) |
| Nhật ký & Tích hợp › Tin nhắn đến | `int-inbound` | S (IX) |
| Nhật ký & Tích hợp › Tin nhắn đi | `int-outbound` | S (IX) |
| Nhật ký & Tích hợp › Nhật ký tích hợp | `int-log` | S (IX) |
| Nhật ký & Tích hợp › Nhật ký tính chỉ số | `kpi-refresh` | S (IX) |
<!-- MENU5:END -->

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
| Hộp phê duyệt (nhóm Nhân sự) và 20 mục "Phê duyệt › …" | 1 mục **Phê duyệt** (màn tổng – D-81); các dòng quyền giữ nguyên, cấp ở nhóm **Phê duyệt** của tab Phân quyền, trang Nhóm quyền (§4.5 – Được duyệt / Không) |

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

### 4.1 Tài khoản – `adm-user` (UI-6.42)
- **4 thẻ:** Đang hoạt động (nhân viên · hệ thống) · **Chờ IT xử lý** (bấm mở tab) · **Nghỉ việc nhưng còn mở** (bấm → **Khóa ngay** tất cả) · **Không đăng nhập 60 ngày** (bấm lọc).
- **Tab Tài khoản:** chip Tất cả · Nhân viên · Hệ thống; lọc trạng thái, nhóm quyền, đơn vị; tìm tên đăng nhập / họ tên / mã NV. Cột: Tên đăng nhập · **Người dùng** (họ tên, mã · chức vụ · đơn vị; "đã nghỉ việc" đỏ / "tạm nghỉ" cam; tài khoản hệ thống ghi rõ, kèm "quản trị") · **Nhóm quyền** · **Truy cập gần nhất** (+ số ngày khi lâu không đăng nhập) · Bot · **Trạng thái** (Hoạt động · Phải đổi mật khẩu · Đã khóa · Nghỉ việc – còn mở) · **Thao tác ▾**: Xem quyền hiệu lực · Đổi nhóm quyền · Đặt lại mật khẩu (quản trị) · Lịch sử truy cập · Khóa / Mở khóa. **Không còn Sửa / Xóa** – tài khoản là dấu vết người thao tác, không dùng nữa thì **Khóa**. Chọn nhiều → **Thêm nhóm quyền · Bỏ nhóm quyền · Khóa** (tài khoản sẽ hết nhóm thì giữ nguyên và báo; không khóa chính mình / quản trị cuối cùng). Bấm dòng → **trang chi tiết tài khoản** toàn màn (UI-6.49): Tổng quan · Nhóm & quyền hiệu lực · Truy cập (30 lần gần nhất) · Bot · Lịch sử; nút Đổi nhóm quyền, Khóa / Mở khóa, Đặt lại mật khẩu (quản trị). Ctrl + bấm → tab mới.
- **Tab Chờ IT xử lý:** thẻ yêu cầu từ Nhân sự – **Tuyển mới** (cấp tài khoản) · **Đổi chức vụ** (nhóm mới / cũ) · **Quay lại làm** (mở lại); nhân viên, chức vụ, đơn vị, người gửi, ngày, nhóm gợi ý; chưa liên kết bot thì IT xem mật khẩu tạm 1 lần. Nút **Đổi nhóm & duyệt · Từ chối · Duyệt & tạo / Duyệt** – cùng quy trình với Phê duyệt › Tài khoản (người không có quyền duyệt chỉ xem).
- **Tạo tài khoản:** **Cho nhân viên** – chọn nhân viên đang làm chưa có tài khoản (gõ để lọc); tên đăng nhập = mã NV; nhóm quyền gợi ý theo chức vụ (chọn lại được) → **Tạo & gửi mật khẩu** (gửi bot nếu đã liên kết, không thì hiện mật khẩu tạm một lần – hết hạn 24 giờ, phải đổi khi đăng nhập). **Tài khoản hệ thống** – chỉ quản trị: tên đăng nhập, mục đích, nhóm quyền. **Khách hàng** – giai đoạn sau.
- Tài khoản cho nhân viên chỉ đi qua đường này hoặc Nhân viên › Đề nghị cấp tài khoản (IT duyệt) – không còn hộp tạo người dùng tự gõ tên / lấy từ nhân viên.

### 4.2 Khóa / mở khóa tài khoản – `adm-user`
1. Trên dòng tài khoản bấm **Khóa** (hoặc **Mở khóa**) → xác nhận.
2. Người bị khóa không đăng nhập được; bot Telegram cũng ngừng phục vụ người đó.
3. Không tự khóa được tài khoản đang đăng nhập: *Không thể tự khóa tài khoản của chính mình*.
4. **Khóa có hiệu lực ngay** (D-82 h): người đang đăng nhập bị chặn ở thao tác kế tiếp – *Tài khoản đã bị khóa – liên hệ IT* – và về màn đăng nhập.
5. **Nhân viên nghỉ việc** (NS đổi trạng thái *Nghỉ việc* / *Ngừng*): tài khoản **tự khóa**, liên kết bot bị thu hồi, yêu cầu duyệt người đó đang giữ được **chuyển bậc kế** (COO → CEO), yêu cầu người đó đã gửi thì người duyệt nhận tin báo. Không cần IT xác nhận. Job **Khóa tài khoản nhân viên đã nghỉ** (00:30 hằng ngày) khóa nốt trường hợp đổi trạng thái ngoài màn (nhập Excel…) và báo FIN-IT. Nhân viên **quay lại làm** → yêu cầu **Mở lại tài khoản** cho IT duyệt.

### 4.3 Xem quyền hiệu lực của một tài khoản – `adm-user`
Bấm nút hình khiên (**Quyền hiệu lực của tài khoản**) trên dòng → bảng cây menu × 7 quyền (Truy cập, Thêm, Sửa, Xóa, In, Xuất, Nhập), cộng dồn mọi nhóm của tài khoản. Tài khoản quản trị: "có mọi quyền".

### 4.4 Nhóm quyền – `adm-usergroup` (UI-6.43 – gồm cả Phân quyền)
Mỗi nhóm là một vai trò. Nhóm có **Tên** tiếng Việt (vd. "Thủ kho"), **Mã** (vd. `OPS-WH` – luồng duyệt, bot, nhóm gợi ý theo chức vụ tìm theo mã), **Khối** (Ban điều hành · Vận hành · Kinh doanh · Tài chính – Hỗ trợ · QA / ATTP) và **Mô tả**. 19 nhóm chuẩn: `docs/claude/ORG_STRUCTURE.md` §2; quyền chuẩn từng màn: `docs/claude/MENU5_MATRIX.md`.
- **Danh sách:** 4 chỉ số – Nhóm quyền (số khối) · Tài khoản có nhóm (bấm → Tài khoản; báo số tài khoản chưa có nhóm) · Nhóm chưa có tài khoản (bấm để lọc) · Đổi trong 7 ngày. Tìm theo tên / mã. Nhóm xếp theo khối; mỗi thẻ: tên, mã, mô tả, số tài khoản · màn hình · loại duyệt, sửa gần nhất. Bấm thẻ → trang nhóm.
- **Tạo nhóm:** Tên (bắt buộc, không trùng) · Mã (bắt buộc, chữ hoa không dấu, không trùng) · Khối · Mô tả. Nhóm mới chưa có quyền nào.
- **Sửa:** đổi tên / khối / mô tả thoải mái. **Mã bị khóa** khi đang dùng ở bước duyệt hoặc cấu hình (nhóm gợi ý chức vụ, nhóm nhận cảnh báo duyệt quá hạn).
- **Ngừng dùng:** chỉ khi nhóm không còn tài khoản và không là người duyệt / trong cấu hình (báo rõ lý do). Không xóa cứng.
- **Việc cần làm sau MENU-5 (D-79):** chuyển người sang nhóm mới – Trưởng kinh doanh → **Trưởng kinh doanh** (SAL-HEAD), Trưởng mua hàng & kho → **Trưởng mua hàng & kho** (OPS-SCM-HEAD), thủ kho → **Thủ kho** (OPS-WH) – ở tab **Tài khoản** của trang nhóm hoặc màn Tài khoản.

### 4.5 Trang nhóm – Phân quyền, Tài khoản, Xem trước menu, So sánh, Lịch sử
Mục menu "Phân quyền" đã bỏ (link cũ `#/adm-permission` mở trang Nhóm quyền). Sửa quyền cần quyền **Sửa** màn Nhóm quyền.
1. **Phân quyền:** màn hình xếp theo nhóm menu; mỗi màn chọn **mức**: Không · Xem · Xem, in & xuất · Xem & sửa (thêm, sửa, in, xuất) · Toàn quyền (thêm cả xóa, nhập file). Quyền lẻ từ ma trận cũ hiện **Tùy chỉnh** (kèm các quyền đang có) – chọn mức khác để thay. Mục nhóm **Phê duyệt** chỉ có **Không / Được duyệt** (= Truy cập + Sửa trên mục đó, D-31); "Tất cả yêu cầu duyệt" chỉ để xem. "tất cả Xem" / "bỏ hết" áp cho cả nhóm menu; "Chỉ màn có quyền" để gọn.
   - Đổi bao nhiêu cũng được → thanh **"N thay đổi chưa lưu"** → **Xem lại & lưu** (bảng Trước → Sau) → **Lưu thay đổi**: lưu một lần, có hiệu lực ngay với mọi tài khoản trong nhóm, ghi lịch sử. Nhóm menu tự bật khi có màn được xem, tự tắt khi không còn màn nào. **Bỏ thay đổi** / rời trang sẽ hỏi lại.
2. **Tài khoản:** danh sách tài khoản trong nhóm (số nhóm của từng tài khoản, trạng thái). **Thêm tài khoản** (tìm, chọn nhiều) · **Bỏ khỏi nhóm** – không bỏ được nếu đó là nhóm duy nhất của tài khoản.
3. **Xem trước menu:** menu người trong nhóm sẽ thấy, **tính cả thay đổi chưa lưu** – kiểm trước khi lưu.
4. **So sánh:** chọn nhóm khác → các màn có mức khác nhau (và số màn giống nhau).
5. **Lịch sử:** sửa nhóm, đổi quyền (ai, lúc nào, Trước → Sau).

**Quyền Duyệt** không phải cột riêng: mỗi loại duyệt là một mục menu **"Phê duyệt › …"** (nhóm Phê duyệt). Cấp **Truy cập + Sửa** trên mục đó = được duyệt loại đó. Danh sách mục duyệt:

| Mục quyền | Duyệt gì |
|---|---|
| Phê duyệt › Hợp đồng (`crm-contract`) | Hợp đồng |
| Phê duyệt › Đơn đặt suất (`crm-order`) | Lệch suất > 30% và sửa / thêm đơn sau giờ chốt |
| Phê duyệt › Chính sách giá (`crm-price-policy`) | Duyệt phụ lục điều chỉnh giá trong hợp đồng (UI-6.4, D-83) |
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

### 4.6b Nhóm "Danh mục hệ thống" (UI-6.40)
Các danh mục ít thay đổi gom vào một nhóm menu ngay trên **Quản trị hệ thống** (mã màn, link cũ và quyền từng mục giữ nguyên; nhóm chỉ hiện khi bạn được xem ít nhất một mục):
- **Tổ chức:** Công ty & đơn vị · Chức vụ · Ca làm việc. **Mua hàng & kho:** Đơn vị tính · Kho & vị trí. **Sản xuất:** Dây chuyền & bàn nấu. **Giao hàng:** Xe giao hàng. **Dùng chung:** Danh mục dùng chung · Địa giới hành chính · Danh mục chỉ số.
- **Tổng quan danh mục** (mục đầu nhóm): mỗi danh mục bạn được xem một thẻ – số bản ghi đang dùng, lần sửa gần nhất (ngày, người) và **cảnh báo thiếu dữ liệu** màu đỏ (bấm mở đúng màn): đơn vị chưa có trưởng, nhân viên chưa gắn đơn vị, chức vụ chưa có nhóm quyền gợi ý, tổ chưa có số người tối thiểu cho ca, nguyên liệu dùng đơn vị chưa có quy đổi, khu bảo quản chưa có ngưỡng nhiệt độ, nhóm món chưa gán dây chuyền, xe đang hoạt động chưa có tải trọng. Cảnh báo chưa tính được thì không hiện.

### 4.6c Chuẩn bị dữ liệu trước khi chạy thật (UI-6.49)
Trang **Danh mục hệ thống** (bấm tên nhóm trên menu) → nút **Xuất danh sách cần bổ sung** → tệp Excel: sheet **Tổng hợp** (loại · số dòng · người bổ sung · màn) + mỗi loại một sheet (chỉ loại thuộc màn bạn được xem). **Hệ thống không tự sửa** – người phụ trách bổ sung trên màn tương ứng rồi xuất lại tới khi các sheet trống.

| Sheet | Người bổ sung | Làm ở đâu |
|---|---|---|
| Đơn vị chưa có trưởng | Nhân sự | Công ty & đơn vị → chọn đơn vị → Trưởng đơn vị |
| Nhân viên chưa gắn đơn vị | Nhân sự | Nhân viên → Sửa → Đơn vị |
| Số điện thoại trùng | Nhân sự | Nhân viên → sửa đúng số của từng người |
| Tổ chưa có định biên ca | Nhân sự (IT nhập cấu hình) | Số người tối thiểu mỗi ca (`BusinessRules:Hr:ShiftMinStaff`) |
| Nhóm quyền ngoài chuẩn | IT | Nhóm quyền → chuyển tài khoản sang nhóm chuẩn → Ngừng dùng nhóm (hoặc chọn khối) |
| Nhóm món chưa gán dây chuyền | Bếp trưởng | Dây chuyền & bàn nấu → gán nhóm món |
| Món / Nguyên liệu chưa phân nhóm | Bếp trưởng / Mua hàng | Món ăn, Nguyên liệu → cột trái nhóm |

### 4.7 Công ty & đơn vị – `adm-orgunit` (UI-6.41 – gộp Công ty + Sơ đồ tổ chức)
Mục "Công ty" đã gộp vào đây (link cũ mở màn này, chọn sẵn công ty). Sửa thông tin công ty vẫn theo quyền của mục Công ty.
- **Đầu màn:** ô **Tìm đơn vị** (mở nhánh chứa kết quả) · **Thêm đơn vị**. 4 thẻ: Đơn vị đang hoạt động (số công ty · bếp · tổ / phòng) · Nhân viên (đã gắn đơn vị) · **Chưa có trưởng đơn vị** (bấm để lọc cây) · **Nhân viên chưa gắn đơn vị** (bấm mở màn Nhân viên).
- **Cây bên trái:** gốc là **công ty**, dưới là ban / khối / bếp / phòng / tổ; mỗi nút có biểu tượng theo loại, nhãn đỏ **chưa có trưởng**, số nhân viên (gồm đơn vị con). Điện thoại: cây trên, chi tiết dưới.
- **Chọn công ty:** Mã · Mã số thuế · Địa chỉ · Điện thoại · Email (nút **Sửa** – tên, mã số thuế 10 / 13 số, địa chỉ, điện thoại, email, ghi chú; **không xóa công ty**) – các thông tin này **in ở đầu mọi phiếu in và tệp Excel**; bảng **Đơn vị trực thuộc**.
- **Chọn đơn vị:** đường dẫn cha, tên + **loại** (tiếng Việt: Công ty con · Ban điều hành · Khối · Chi nhánh · Bếp · Phòng ban · Tổ · Kho) + trạng thái; Mã · **Trưởng đơn vị** (chưa có → **chọn ngay**) · Nhân viên · Kho. Nút **＋ Đơn vị con · Sửa · Chuyển sang đơn vị khác** (chuyển sang công ty khác thì cả nhánh đổi công ty; không chuyển vào chính nhánh con) **· Ngừng hoạt động / Hoạt động lại** (chặn khi còn nhân viên đang làm hoặc đơn vị con đang hoạt động; hoạt động lại khi đơn vị cha đang hoạt động). Tab **Nhân viên** (gồm đơn vị con; bấm mở hồ sơ) · **Đơn vị con** · **Kho** · **Lịch sử**.
- **Thêm / Sửa đơn vị:** **Thuộc\*** (điền sẵn nút đang chọn) · Tên\* · Mã\* (chữ hoa không dấu; khóa khi đã dùng) · **Loại\*** · Trưởng đơn vị (chỉ nhân viên đang làm) · Điện thoại · Địa chỉ · Ghi chú. Công ty lấy theo đơn vị cha.
- Trưởng đơn vị nhận bot nhắc lịch ca tuần sau và báo người chưa vào ca – đơn vị chưa có trưởng sẽ không ai nhận.
- Đơn vị loại **Chi nhánh** xuất hiện trong hộp **Đơn vị làm việc** ở đầu trang.

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
| Người dùng báo "không có quyền truy cập màn hình này" | Nhóm quyền › trang nhóm của họ › Phân quyền: chọn mức **Xem** cho màn đó → Xem lại & lưu; người dùng tải lại trang |
| Người dùng có quyền mà nút vẫn mờ | Thiếu quyền Thêm / Sửa / Xuất của màn đó |
| Người dùng không duyệt được | Cấp mục **Phê duyệt › …** đúng loại (Truy cập + Sửa) |
| … «…» không có trong danh mục (…) | Giá trị phải chọn trong danh sách – xem 4.8c; cần giá trị mới → IT bổ sung danh mục |

## 7. Trên bot Telegram

- 🖥 **Tình trạng hệ thống** (`/hethong`, chỉ quản trị): job tự động + tin gửi lỗi.
- Nhận cảnh báo 🔴 job lỗi, 🔴 tin gửi thất bại nhiều lần.
- Gỡ liên kết Telegram của người dùng: hiện chưa có màn web (màn `int-identity` đang ẩn) – người dùng tự gửi `/huylienket` trên bot, hoặc IT gọi API.
