# UAT_CHECKLIST – kịch bản thử bằng API

Thử trên **staging** (deploy/README.md). Công cụ: Swagger (`/swagger`) hoặc VS Code REST Client (`docs/api-tests/*.http`).
Đánh dấu [x] khi đạt; ghi lỗi / góp ý vào cột "Ghi chú" hoặc báo Claude Code.

## TRANG CHI TIẾT TOÀN MÀN – MUA HÀNG / KHO (UI-6.2 nhóm B, D-80 – 2026-11-23) · test: `DetailPages/ScmOverviewTests.cs` · HDSD `04_MUA_HANG.md` mục 4.6, `05_KHO.md` mục 4.12

| # | Kịch bản | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Mở trang | Ở Đơn mua / Phiếu nhập / Đề nghị mua / Lô / Nguyên liệu bấm 1 dòng | Mở trang toàn màn `#/scm-…-detail?id=…` (không còn ngăn trượt); F5 vẫn đúng bản ghi; dán đường dẫn sang trình duyệt khác (cùng quyền) mở được | [ ] | |
| 2 | Quay lại | Lọc + sang trang 2 + cuộn xuống → mở 1 dòng → **← Danh sách** | Về đúng bộ lọc, trang, vị trí cuộn | [ ] | |
| 3 | Tab mới | Ctrl + bấm (hoặc chuột giữa) 1 dòng PO | Trang chi tiết mở ở tab mới, danh sách giữ nguyên | [ ] | |
| 4 | PO đang nhận | Mở PO Nhận một phần | Tiến trình tô tới "Nhận một phần"; thẻ Đã nhận % khớp tab Dòng hàng; tab Phiếu nhập & QC có phiếu + kết quả QC; bấm số phiếu → trang phiếu nhập; nút **Nhận hàng** mở đúng hộp nhận hàng của danh sách | [ ] | |
| 5 | PO Nháp → duyệt | PO Nháp → **Gửi duyệt** trên trang | Trang tải lại: trạng thái Chờ duyệt, có tab **Duyệt** (các cấp, hạn), ⋯ Mở màn Phê duyệt | [ ] | |
| 6 | Phiếu nhập | Mở phiếu Đạt một phần → **Ghi sổ nhập kho** | Ghi sổ xong trang hiện Đã ghi sổ, khối **Lô đã tạo** có số lô (bấm → trang lô); thẻ Lệch nhiệt độ đếm đúng dòng vùng có điều kiện / loại | [ ] | |
| 7 | Đề nghị mua | Đề nghị Đã duyệt → **Lập PO** | Báo danh sách PO; đề nghị → Đã lập PO; tab Đơn mua đã lập có PO mới | [ ] | |
| 8 | Lô | Mở lô từ trang phiếu nhập → tab **Truy xuất** | Chuỗi NCC → đơn mua → phiếu nhập → lô → phiếu xuất → … hiện ngay trên trang; QA có quyền thấy **Tạm giữ** → giữ lô → trạng thái Tạm giữ + khung "Không xuất được" | [ ] | kho không thấy nút Tạm giữ |
| 9 | Nguyên liệu | Mở 1 nguyên liệu có tồn | 4 thẻ: tồn khả dụng (đỏ nếu dưới tối thiểu), lô còn tồn / sắp hết hạn, **giá mua gần nhất** = PO đã duyệt mới nhất (không lấy PO Nháp), số NCC; tab Giá & nhà cung cấp có bảng giá + lịch sử giá; **Sửa** mở hộp sửa cũ | [ ] | |
| 10 | Quyền | Tài khoản chỉ xem Đơn mua mở trang Lô (dán đường dẫn) | Báo không có quyền (API 403); tài khoản xem Lô mở được | [ ] | |
| 11 | Id sai / rỗng | Sửa id trên đường dẫn thành id không có; mở PO / phiếu nhập chưa có dòng | Id sai: "Không tìm thấy bản ghi" (không trắng trang); chứng từ rỗng: trang mở, bảng "Chưa có dữ liệu" | [ ] | |
| 12 | Điện thoại | Mở trang PO trên điện thoại | Chỉ nút chính + ⋯; thẻ chỉ số 2 cột; bảng cuộn ngang trong khung | [ ] | |
## TRANG CHI TIẾT – NHÓM KINH DOANH (UI-6 6.2 nhóm A, D-80 – 2026-11-23) · API: `GET api/Order|Contract|CustomerComplaint|CustomerDeliveryLocation/GetOverview?id=` · test: `DetailPages/SalesOverviewTests.cs` · HDSD `02_KINH_DOANH.md` 4.2 / 4.3 / 4.5, `09_GIAO_VAN.md` 4.7
- [ ] **Mua hàng liền mạch (UI-6.18):** form Đề nghị mua có bảng nguyên liệu (tồn, NCC gợi ý · giá, tổng), không có ô trạng thái / số; duyệt 2 cấp xong → hệ thống tự lập đơn mua (NCC ưu tiên + dưới 50 tr → tự duyệt); dòng không có NCC đủ điều kiện hiện ở Đơn mua hàng › Chọn nhà cung cấp (bấm thẻ NCC, Theo gợi ý, Lập đơn mua); Chờ gửi NCC → Gửi / Gửi tất cả; NCC ưu tiên tăng giá ≥ 10% có NCC rẻ hơn đúng hẹn → gợi ý NCC rẻ hơn.
- [ ] **Bảng giá NCC (UI-6.19):** màn so sánh nguyên liệu × NCC theo ngày (giá quy về ĐVT kho, viền ưu tiên, chấm rẻ nhất, chip việc cần làm, gia hạn hàng loạt, nhập báo giá Excel xem trước → áp dụng); form giá: giá theo "thùng 10 kg" hiện "= X đ / kg", lịch sử + so với NCC khác; thêm giá mới tự kết thúc giá cũ; sửa giá đã áp dụng → chỉ được "Lưu giá mới từ ngày"; đơn vị không quy đổi được bị chặn.
- [ ] **Đơn vị tính (UI-6.22):** sidebar không còn "Quy đổi đơn vị"; màn Đơn vị tính lọc Gốc / Đóng gói, cột "= N đơn vị gốc" + "Đang dùng ở"; tạo "thùng 10 kg" → có quy đổi 10 kg; tạo lại cùng kích cỡ → chọn lại đơn vị cũ; đơn vị đã dùng đổi kích cỡ → bị chặn; form Nguyên liệu có "+ Tạo đơn vị đóng gói", ô Đơn vị mua chỉ hiện đơn vị quy đổi được ("= N kg").
- [ ] **Nhóm trong màn (UI-6.21):** sidebar không còn Nhóm nguyên liệu / Nhóm món ăn (mọi vai trò); màn Nguyên liệu / Món ăn có cột nhóm: lọc, + Thêm nhóm, đổi tên tại chỗ, ngừng dùng bị khóa khi còn bản ghi, trùng tên bị chặn; chọn nhóm trong form nguyên liệu → gợi ý bảo quản + đơn vị; link cũ `#/scm-ingredient-cat?id=` mở màn Nguyên liệu lọc đúng nhóm.
- [ ] **Nguyên liệu (UI-6.20):** form 5 nhóm; nhóm + đơn vị kho + loại bảo quản bắt buộc; Đơn vị mua / xuất chỉ liệt kê đơn vị quy đổi được ("= N kg"); Mát / Đông lạnh tự bật Kiểm tra khi nhận (người không phải QA không tắt được); bật hạn dùng → bắt số ngày, tự bật lô; Ngừng dùng nguyên liệu trong công thức đang dùng → báo "Lưu ý … công thức"; nhận hàng thử nguyên liệu Mát → có kiểm nhiệt độ.
- [ ] **Món ăn (UI-6.17):** form Thêm / Sửa món chỉ còn Tên, Nhóm món (bắt buộc chọn, + Thêm nhóm món), Định lượng 1 suất + đơn vị, Mô tả, Trạng thái; mã + giá thành chuẩn chỉ đọc; Thêm món theo 3 lựa chọn công thức (chép từ món khác → V1 Nháp có sẵn định lượng; nhập mới → V1 trống; để sau → nhãn Chưa có công thức); tab Công thức trên trang món sửa định lượng / hao hụt, thêm nguyên liệu bằng ô gõ tên, Lưu nháp / Gửi duyệt / Duyệt (người khác) / Khóa / Tạo phiên bản mới; màn Công thức chế biến không đổi (có link Mở trang món).
- [ ] **Điểm giao (UI-6.16):** sidebar không còn "Điểm giao hàng" với mọi vai trò; trang Khách hàng có + Điểm giao / ✎ sửa (chỉ người có quyền Sửa Khách hàng); ngừng điểm còn đơn chưa giao → báo số đơn; `#/crm-location` mở danh sách Khách hàng, `#/crm-location?id=` mở trang điểm giao; OPS-LOG xem được Khách hàng + tìm điểm giao ở Chuyến giao hàng.
- [ ] **Đơn hàng theo ngày (UI-6.15):** menu Kinh doanh hiện "Đơn hàng theo ngày"; Hôm nay / Ngày mai / ◀ ▶ đổi ngày; 4 bước + 4 thẻ khớp bảng; điểm chưa có đơn hiện dự báo mờ + "Chưa có số" → **Nhập số** mở Đăng ký suất tuần đúng tuần + điểm; đơn Nháp → **Xác nhận** tại dòng; tab Cần xử lý / Đơn phát sinh / Đã hủy; **Thêm đơn phát sinh** vào điểm đã có đơn tuần → cảnh báo, tạo được, trước 20:00 Đã xác nhận, sau 20:00 gửi duyệt; **⋯ Chốt đơn theo ngày** chỉ hiện với người có quyền Sửa + duyệt KD; Xuất Excel đúng bảng; không còn Kanban / chữ kỹ thuật.
- [ ] Đơn đặt suất: bấm dòng ở Đơn hàng theo ngày → mở `#/crm-order-detail?id=` (không còn ngăn trượt); **← Đơn đặt suất** về đúng ngày ăn / dạng xem / bộ lọc / vị trí cuộn; Ctrl + bấm hoặc chuột giữa → tab mới; F5 trên trang vẫn mở đúng đơn.
- [ ] Đơn Nháp trước giờ chốt: nút **Xác nhận** (hộp hiện % lệch dự báo) → trạng thái + thanh bước đổi ngay; **Sửa số suất** → lưu → số / tiền / thẻ Tổng suất cập nhật; đơn Đã xác nhận: **Chốt số lượng**; ⋯ **Hủy đơn** bắt nhập lý do.
- [ ] Đơn sau giờ chốt: chỉ còn **Yêu cầu sửa sau chốt** → gửi → tab **Duyệt** hiện yêu cầu + các cấp; đơn có lệch suất chờ duyệt có tag "Chờ duyệt PD-…".
- [ ] Đơn đã giao: thẻ **Giao hàng** = số phiếu giao, khách nhận khớp biên bản giao (POD); khối **Đối soát** khớp màn Đối soát; tab **Phản ánh** liệt kê phản ánh của đơn, bấm mở trang phản ánh.
- [ ] Hợp đồng: bấm dòng (danh sách / hồ sơ khách 360) → `#/crm-contract-detail?id=`; Nháp: **Gửi duyệt** / **Sửa**; Chờ duyệt: **Duyệt** / **Từ chối** + tab Duyệt; Hiệu lực: ⋯ **Thanh lý hợp đồng** (bắt lý do); **Đổi** cách tính tiền đối soát → trang tải lại đúng giá trị mới.
- [ ] Hợp đồng: thẻ **Đơn hàng** / **Đã đối soát** khớp tổng ở màn Đơn đặt suất / Đối soát (lọc theo hợp đồng); "Còn N ngày" đúng ngày hiệu lực đến; hợp đồng không thời hạn hiện "Không thời hạn".
- [ ] Phản ánh: bấm dòng → `#/log-complaint-detail?id=`; nút **→ bước tiếp theo** chuyển Mới → Đang xử lý → Đã xử lý → Đóng (bắt nhập nội dung), ⋯ **Từ chối phản ánh**; thẻ hạn xử lý: còn / quá hạn (đỏ) / đã xử lý trong … giờ.
- [ ] Phản ánh Thiếu suất / Hỏng / Nhiệt độ có đơn gốc, đang mở, chưa có đơn bù: nút **Tạo đơn bù** (chỉ người có Thêm phản ánh + Thêm đơn đặt suất) → tạo xong nút ẩn, khối **Đơn bù** có đơn mới; **Thêm ghi chú** → tab **Diễn biến xử lý** có dòng mới kèm tên người ghi.
- [ ] Điểm giao: bấm dòng ở màn Điểm giao hàng (hoặc tên điểm giao trong trang đơn) → `#/crm-location-detail?id=`; thẻ Suất TB / ngày, Đơn 30 ngày, **Giao đúng giờ** (đơn giao trễ > 15 phút không tính đúng giờ), Phản ánh đang mở; **Bản đồ** mở đúng tọa độ; **Sửa** mở form chung, lưu xong trang cập nhật.
- [ ] Quyền: tài khoản chỉ có quyền xem `crm-order` mở được trang đơn nhưng không thấy nút thao tác; không có quyền màn gốc → API trả 403 / trang báo không có quyền; tab Giao hàng / Phản ánh ẩn khi không có quyền xem màn tương ứng. Id sai → "Không tìm thấy bản ghi". Chế độ DEMO vẫn mở ngăn trượt.

## TÀI KHOẢN ↔ CON NGƯỜI ↔ NHÂN VIÊN (HR-ACC-1, D-82 – 2026-11-24) · migration `DB/migrations/20261123_02_hr_acc_person_link.sql` · script `DB/scripts/D82_link_employee_person.sql` · test: `HrAccount/HrAccountTests.cs`, `HrAccount/D82LinkScriptSqlTests.cs` · ảnh: `docs/frontend-handoff/assets/hr-acc-1/` · HDSD `01_QUAN_TRI.md` 4.1c–4.2, `11_NHAN_SU.md` 4.0 / 4.0b, `12_QUAN_LY_DUYET.md`

Chạy **sau** khi đã backup → migration → deploy API → script D82 (DryRun rồi chạy thật).

| # | Kịch bản | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Script gắn | `sqlcmd … -v DryRun=1 -i DB/scripts/D82_link_employee_person.sql` rồi `DryRun=0`, chạy lại lần 2 | Báo cáo 3 nhóm Khớp chắc / Nhiều ứng viên / Không khớp + danh sách; lần 2 "Đã gắn 0" | | |
| 2 | Đối chiếu | NS mở **Nhân viên › Đối chiếu tài khoản**, lọc Nhiều ứng viên → **Gắn** đúng người; thử gắn người đó cho NV thứ 2 | Gắn được; lần 2 báo "Người này đã gắn nhân viên …" | | |
| 3 | Tuyển mới | NS **Thêm** nhân viên, tích **Cấp tài khoản web** → IT mở **Phê duyệt**, đổi nhóm, **Duyệt** | Tài khoản = mã NV viết thường, đúng nhóm IT chọn; mật khẩu tạm qua bot hoặc hộp 1 lần; đăng nhập phải đổi mật khẩu | | |
| 4 | Không trùng | NS bấm **Đề nghị cấp tài khoản** lần 2 khi yêu cầu còn mở | "Đã có yêu cầu duyệt đang chờ…" | | |
| 5 | Đổi tên / mã / SĐT | Đổi tên đăng nhập (màn Tài khoản), đổi SĐT nhân viên → nhân viên chấm công web / bot | Vẫn chấm công đúng người; **Việc của tôi** không báo "chưa gắn" | | |
| 6 | Nghỉ việc | NS đổi trạng thái *Nghỉ việc* khi người đó đang đăng nhập và đang giữ 1 yêu cầu duyệt | Thao tác kế tiếp: "Tài khoản đã bị khóa"; đăng nhập lại không được; bot ngừng; COO nhận tin "Người duyệt … đã nghỉ việc"; màn Tài khoản: cảnh báo "đã nghỉ còn mở" = 0 | | |
| 7 | Quay lại | Đổi lại *Đang làm* → IT duyệt **Mở lại tài khoản** | Đăng nhập lại được | | |
| 8 | Đổi chức danh | (Sau khi IT điền `BusinessRules:PositionDefaultGroups`) đổi chức danh sang chức danh có nhóm gợi ý khác | Có yêu cầu **Đổi nhóm quyền**; trong lúc chờ giữ quyền cũ; duyệt → nhóm mới | | |
| 9 | Tự duyệt | Người duyệt đơn nghỉ là chính nhân viên được NS lập đơn hộ | "Không duyệt yêu cầu về chính mình" | | |
| 10 | Đồng bộ | Sửa SĐT ở hồ sơ nhân viên; sửa họ tên ở hồ sơ cá nhân (menu tài khoản) | Hai nơi cùng đổi | | |
| 11 | Tài khoản khách | (Test tự động) tài khoản CUSTOMER kể cả IsAdmin | Đăng nhập bị từ chối; API nội bộ 403 | | |

## PHÊ DUYỆT TỔNG (APR-UI-1, D-81 – 2026-11-23) · test: `Approval/ApprovalHubTests.cs` · ảnh: `docs/frontend-handoff/assets/apr-ui-1/screens/` · HDSD `12_QUAN_LY_DUYET.md`

**Đo (D-81 k):** COO + trưởng mua hàng dùng màn Phê duyệt 1 ngày; ghi số lần bấm / yêu cầu bình thường (mục tiêu **≤ 2**) và thời gian / yêu cầu có cảnh báo (mục tiêu **≤ 30 giây**). Cuối ngày gọi `GET api/ApprovalRequest/GetStats` (admin / người xem hộp chung) – số yêu cầu, % duyệt nguyên trạng, thời gian chờ TB theo loại – dán vào cột Ghi chú.

| # | Kịch bản | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Sidebar | Đăng nhập COO / OPS-SCM-HEAD / FIN-HR / nhân viên vừa gửi đơn nghỉ | 1 mục **Phê duyệt** dưới "Việc của tôi", số = chờ tôi duyệt; không còn mục "Phê duyệt › …"; người không có quyền duyệt và chưa gửi gì không thấy mục | [ ] | |
| 2 | Màn tổng | Mở Phê duyệt | 4 thẻ (Chờ tôi duyệt · Quá hạn · Hết hạn trong 8 giờ · Tôi gửi đang chờ); lọc phân hệ có số; xếp quá hạn → sắp hết hạn → cũ nhất; mỗi dòng có Vì sao cần duyệt + cảnh báo / Bình thường; không có "dbo.", mã loại thô, hộp GAP | [ ] | |
| 3 | Duyệt ≤ 2 bấm | Bấm 1 đơn nghỉ bình thường → **Duyệt & sang việc tiếp** | Duyệt xong, tự sang việc kế; số trên sidebar giảm | [ ] | đếm số bấm |
| 4 | Cảnh báo PO | Lập PO tay giá cao hơn lần trước ≥ 10 % → gửi duyệt | Dòng PO có "⚠ … cảnh báo: Giá … cao hơn lần mua trước …", ô giá tô màu ở xem nhanh; không tick được để duyệt nhiều | [ ] | đo thời gian |
| 5 | Từ chối | Bấm Từ chối không nhập ý kiến | Báo "Nhập lý do từ chối"; nhập lý do → từ chối được | [ ] | |
| 6 | Duyệt nhiều | Tick 2 đơn nghỉ / đổi ca bình thường → Duyệt các mục đã chọn | "Đã duyệt 2/2"; dòng có cảnh báo không tick được | [ ] | |
| 7 | Kế hoạch giữ Nháp | Sau chốt đơn hệ thống giữ Nháp kế hoạch (thiếu NL) → COO mở Phê duyệt | Kế hoạch hiện trong Chờ tôi duyệt (phân hệ Sản xuất) với lý do giữ; Duyệt → kế hoạch Đã duyệt; không có nút Từ chối (có Sửa kế hoạch) | [ ] | |
| 8 | Tôi đã gửi + Nhắc | Người gửi mở tab Tôi đã gửi → **Nhắc qua bot**, bấm lại ngay | Lần 1: người duyệt nhận tin "🔔 Người gửi nhắc"; lần 2: "Đã nhắc lúc … – nhắc lại được sau …" | [ ] | |
| 9 | Người duyệt nghỉ | FIN-HR (người duyệt duy nhất đơn nghỉ) có đơn nghỉ Đã duyệt hôm nay; nhân viên gửi đơn nghỉ | COO nhận ngay tin bot "Người duyệt cấp này đang nghỉ hôm nay – chuyển bạn xử lý (bậc 2)"; yêu cầu có cảnh báo "Người duyệt cấp này đang nghỉ" | [ ] | |
| 10 | Bot | Bot Điều hành → ⏳ Chờ tôi duyệt → mở 1 thẻ | Thẻ: 🔴/⚠️/🟢 + hạn, loại + số, tóm tắt, vì sao, ≤ 3 cảnh báo, người gửi; nút Duyệt / Từ chối / 📄 Xem chi tiết; sau 18 giờ nhận 📋 Tổng hợp cuối ngày | [ ] | |
| 11 | Điện thoại | Mở Phê duyệt trên điện thoại | Thẻ thay bảng, lọc cuộn ngang, bấm thẻ → toàn màn, nút Từ chối / Duyệt · việc tiếp cố định cuối (cao ≥ 44px) | [ ] | |
| 12 | Phân quyền | Admin mở Phân quyền 1 nhóm | Khối **Quyền duyệt** riêng (tên loại + cấp, 1 ô / loại); tick → nhóm duyệt được loại đó | [ ] | |

## TRANG CHI TIẾT – SẢN XUẤT / THỰC ĐƠN / GIAO HÀNG (UI-6.2 nhóm C, D-80 – 2026-11-23) · test: `DetailPages/ProdOverviewTests.cs` · HDSD `03_KE_HOACH.md` (món ăn, kế hoạch, lệnh SX), `07_BEP_SAN_XUAT.md` (mẻ), `09_GIAO_VAN.md` (chuyến)

Chế độ dữ liệu thật (API). Chế độ DEMO giữ ngăn trượt như cũ.

| # | Kịch bản | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Mẻ nấu – mở trang | Bảng điều độ mẻ → bấm 1 thẻ mẻ (Ctrl + bấm → tab mới) | Mở `#/prod-batch-detail?id=…`: tên "Mẻ ME-…", thanh bước Kế hoạch → Đang nấu → Chờ QC → Đạt → Đã đóng gói, 4 chỉ số (sản lượng, nhiệt độ lõi, công đoạn, hao hụt); "← Danh sách" về đúng ngày đang xem | [ ] | |
| 2 | Mẻ nấu – thao tác | Mẻ Kế hoạch → Bắt đầu nấu; Đang nấu → Kết thúc nấu (nhập sản lượng + nhiệt độ lõi); Chờ QC → QC: Đạt / Tạm giữ / Loại mẻ (QA); ⋯ → Ghi hao hụt / In phiếu sản xuất | Hộp nhập giống ngăn chi tiết cũ; xong trang tự tải lại, bước + chỉ số đổi; người không có quyền Duyệt "Mẻ nấu" không thấy nút QC | [ ] | |
| 3 | Mẻ nấu – tab | Tab QC & an toàn / Công đoạn / Nguyên liệu & xuất kho / Chứng từ liên quan / Tài liệu / Lịch sử | Bảng nhiệt độ lõi (đạt / không đạt + khắc phục), lưu mẫu; công đoạn có nút Bắt đầu / Xong; nguyên liệu theo định lượng + phiếu xuất | [ ] | |
| 4 | Lệnh SX | Màn Lệnh sản xuất → bấm 1 dòng | Trang `#/prod-order-detail`: chỉ số mẻ đạt, suất đạt, giữ chỗ thiếu, giá thành / phần (đỏ khi > chuẩn); bảng mẻ bấm được → trang mẻ; nút Đóng lệnh / Tính giá thành / Hủy lệnh theo trạng thái (gọi đúng hộp xác nhận cũ) | [ ] | |
| 5 | Kế hoạch SX | Màn Kế hoạch sản xuất → bấm 1 dòng | Trang `#/prod-plan-detail`: tổng suất, ca đã phát hành, lệnh SX, giữ chỗ thiếu; khối Theo ca + Món & suất; tab Lệnh sản xuất / Mẻ / Giữ chỗ nguyên liệu; Nháp → Duyệt kế hoạch (người có quyền Duyệt), Đã duyệt → Ra lệnh SX | [ ] | |
| 6 | Kế hoạch giữ chờ COO | Kế hoạch Nháp do hệ thống giữ sau chốt đơn ("chờ COO duyệt") | Tag "Chờ COO duyệt", dòng lý do ở Thông tin kế hoạch, tab **Duyệt** hiện cấp "Kế hoạch sản xuất (COO)"; ⋯ → Mở màn Phê duyệt | [ ] | |
| 7 | Món ăn | Màn Món ăn → bấm 1 dòng | Trang `#/menu-dish-detail`: giá vốn NVL / suất (cam khi thiếu giá, đỏ khi > giá thành chuẩn), số nguyên liệu định mức, số menu dùng món, % mẻ đạt QC; tab Định mức (các phiên bản công thức) / Menu / Lịch sử nấu (bấm mẻ → trang mẻ); Sửa mở đúng biểu mẫu món | [ ] | |
| 8 | Chuyến giao | Màn Tuyến giao → Chi tiết 1 chuyến | Trang `#/log-route-detail`: điểm đã giao / tổng, suất đã giao, tải xe %, phản ánh; dòng thời gian điểm giao; tab Điểm giao & POD / Sự kiện & nhiệt độ / Mẻ phục vụ / Phản ánh; nút Xếp hàng lên xe (Kế hoạch) / Xuất bến (Đang xếp – hỏi nhiệt độ) / Sắp điểm dừng / In phiếu giao / Biên bản giao hàng | [ ] | |
| 9 | Quyền | Tài khoản chỉ xem Mẻ nấu mở link trang lệnh SX / chuyến giao | Không mở được (403 / "không có quyền"); tài khoản có quyền xem màn gốc mở được | [ ] | |
| 10 | Id sai / rỗng | Sửa `?id=` thành mã khác | "Không tìm thấy bản ghi"; bản ghi chưa có dữ liệu con (mẻ chưa đo, món chưa công thức, chuyến chưa điểm giao) vẫn mở được | [ ] | |

## PHÂN QUYỀN THEO TỪNG MÀN + MENU THEO VAI TRÒ (MENU-5, D-79 – 2026-11-23) · script: `DB/scripts/D79_menu_regroup.sql`, `DB/scripts/D79_permission_matrix.sql` · test: `Security/Menu5PermissionTests.cs`, `Security/D79MatrixSqlTests.cs` · ma trận: `docs/claude/MENU5_MATRIX.md`

Trước khi thử: deploy API → seed → `D79_menu_regroup.sql` → `D79_permission_matrix.sql` (DryRun trước, sau backup); tạo 1 tài khoản thử cho mỗi nhóm (hoặc chuyển người sang SAL-HEAD / OPS-SCM-HEAD / OPS-WH), đăng nhập lại.

| # | Vai trò | Menu thấy gì | 1 thao tác được | 1 thao tác bị chặn | Đạt | Ghi chú |
|---|---|---|---|---|---|---|
| 1 | CEO | Việc của tôi, Phê duyệt (Hợp đồng, Đơn mua ≥ 50 triệu, Tất cả yêu cầu duyệt), mọi nhóm nghiệp vụ chỉ xem; không có Quản trị, Nhật ký & Tích hợp (trừ Tin nhắn đi) | Duyệt hợp đồng trong Phê duyệt › Hợp đồng | Mở Thực đơn tuần → không có nút sửa; không thấy mục duyệt Công thức | [ ] | |
| 2 | COO | Như CEO + Phê duyệt việc vận hành (công thức, thực đơn, đổi món, kế hoạch SX, điều chỉnh tồn, đơn mua, ĐN mua cấp 2, kiểm kê, đơn nghỉ / đổi ca, sửa đơn sau chốt) | Duyệt kế hoạch sản xuất | Sửa đơn đặt suất → không có nút Sửa (chỉ xem) | [ ] | |
| 3 | OPS-PLAN | Thực đơn & Món ăn, Sản xuất (kế hoạch / lệnh), Giá thành (chuẩn S, thực tế / giá vốn xem + in), Chất lượng (xem), Kho (tồn / lô xem) | Tạo công thức, gửi duyệt | Không tự duyệt công thức của mình; không vào Hợp đồng | [ ] | |
| 4 | OPS-SCM | Mua hàng (NCC, nguyên liệu Q; tính nhu cầu, ĐN mua, đơn mua S); Kho chỉ xem | Lập đề nghị mua, gửi duyệt | Phiếu nhập kho chỉ xem (không ghi sổ nhập); không thấy Phê duyệt › ĐN mua – cấp 1 | [ ] | |
| 5 | OPS-WH | Kho (phiếu nhập / xuất, giữ chỗ, sổ kho, kiểm kê, lô S; danh mục kho Q); Mua hàng xem đơn mua / NCC / nguyên liệu | Ghi phiếu nhập kho | Lô hàng: không có nút Tạm giữ / Mở giữ; API MaterialLot/Release → 403 | [ ] | |
| 6 | OPS-SCM-HEAD | Mua hàng + Kho (như OPS-SCM ∪ OPS-WH) + Điều hành; Phê duyệt › ĐN mua – cấp 1, Phiếu kiểm kê – cấp 1 | Duyệt ĐN mua cấp 1 của người khác | ĐN mua do chính mình gửi: nhãn "Bạn gửi – chờ người khác duyệt", không có nút Duyệt | [ ] | |
| 7 | OPS-KIT | Sản xuất (mẻ nấu, hao hụt S; kế hoạch / lệnh xem), Đóng gói (lệnh / kết quả S), Chất lượng (kiểm tra theo ca, nhiệt độ, vệ sinh S), Nhân sự xem ca / chấm công | Bắt đầu / kết thúc mẻ, ghi nhiệt độ lõi | Mẻ Chờ QC: không có nút QC PASS / HOLD / REJECT ("Chờ QA kết luận"); không thấy Giá thành | [ ] | |
| 8 | OPS-LOG | Đóng gói & Giao hàng (đơn giao, chuyến, xếp xe, theo dõi, xác nhận, thu hồi S; xe Q) | Tạo chuyến giao | Không thấy Chính sách giá / Hợp đồng | [ ] | |
| 9 | OPS-POS | Kinh doanh (đối soát S; khách / đơn xem), Phản ánh S, Giao hàng (xác nhận đã giao, thu hồi S) | Ghi đối soát suất ăn | Hợp đồng: không thấy; PUT Contract/Update → 403 | [ ] | |
| 10 | OPS-MNT | Sản xuất › Dây chuyền bếp, Trạm bếp (S), Mẻ nấu xem, Nhật ký nhiệt độ xem | Sửa trạm bếp | Không vào kế hoạch sản xuất | [ ] | |
| 11 | SAL-SALES | Kinh doanh (khách, điểm giao, giá, hợp đồng, dự báo, đơn, đăng ký tuần S; bảng kê xem + in), Thực đơn xem, Giao hàng xem | Tạo đơn đặt suất | Không xóa khách hàng; không duyệt sửa đơn sau chốt | [ ] | |
| 12 | SAL-CON | Như SAL-SALES nhưng Q + in / xuất trên dữ liệu khách hàng / hợp đồng / đơn | Xóa đơn nháp | Không còn mục Phê duyệt › Đơn đặt suất (sửa sau chốt) | [ ] | |
| 13 | SAL-HEAD | Như SAL-CON + Phản ánh S + Giá vốn / Giá thành chuẩn / Điều hành xem; Phê duyệt › Đơn đặt suất (sửa sau chốt) | Tạo đơn bù từ phản ánh thiếu suất; duyệt sửa đơn sau chốt của người khác | Không tự duyệt yêu cầu sửa đơn do mình gửi | [ ] | |
| 14 | SAL-CS | Phản ánh khách hàng, Yêu cầu đổi món (S); khách / đơn / theo dõi chuyến xem | Ghi phản ánh | Phản ánh thiếu suất: không có nút Tạo đơn bù; API CreateCompensationOrder → 403; không sửa chuyến | [ ] | |
| 15 | FIN-ACC | Xem + in / xuất: khách, hợp đồng, giá, đơn, đối soát, bảng kê, mua hàng, kho, giá thành, hao hụt, khay hộp, chấm công, chỉ số; Phê duyệt › Phiếu kiểm kê – cấp 2 | Duyệt kiểm kê cấp 2 | Không có nút Thêm / Sửa ở mọi màn; không mở được Hộp phê duyệt chung | [ ] | |
| 16 | FIN-HR | Nhân sự (nhân viên, phân ca, chấm công, đơn nghỉ / đổi ca S; ca làm việc, chức vụ Q); Phê duyệt › Đơn xin nghỉ, Đơn đổi ca | Thêm chức vụ; duyệt đơn nghỉ của người khác | Đơn nghỉ do chính mình tạo: không có nút Duyệt | [ ] | |
| 17 | FIN-IT | Quản trị hệ thống + Nhật ký & Tích hợp (Job tự động, nhật ký, bot…) + xem mọi màn nghiệp vụ + Hộp phê duyệt chung | Chạy tay 1 job ở Job tự động | Không có mục Phê duyệt › …; không sửa đơn đặt suất | [ ] | |
| 18 | QA | Chất lượng & ATTP (Q / S), Phản ánh S, kiểm tra khay hộp thu hồi S; NCC / nguyên liệu / phiếu nhập / lô / mẻ xem; Phê duyệt › Kiểm tra hàng nhập, Mẻ nấu, Giữ / thả lô | Kết luận phiếu QC đầu vào; Tạm giữ lô; Loại mẻ | Không lập đơn mua (POST PurchaseOrder/Insert → 403); không thấy Hợp đồng / Chính sách giá | [ ] | |
| 19 | Chung | Sidebar mọi vai trò: **Việc của tôi** dòng đầu (bấm thẳng), **Phê duyệt** ngay sau; không còn nhóm chỉ có 1 mục lạc chỗ; không thấy "Bot Telegram – …" | Mở Người liên hệ cũ (`#/crm-contact`) → về Khách hàng | Người gửi bấm Duyệt yêu cầu của mình (bot / API) → "Bạn là người gửi yêu cầu này – cần người khác duyệt" | [ ] | |
| 20 | ĐN mua 2 cấp | Tài khoản OPS-SCM gửi ĐN mua → OPS-SCM-HEAD duyệt cấp 1 → COO duyệt cấp 2 | Đề nghị chuyển Đã duyệt | Một người không duyệt được cả 2 cấp | [ ] | |

## DỌN MENU (MENU-4, D-75 d, e, f – 2026-11-23) · script: `DB/scripts/MENU4_cleanup_menu.sql` · test: `AppDelicare.Tests/Admin/Menu4Tests.cs`, `Menu4SqlServerTests.cs` · ảnh: `docs/frontend-handoff/assets/menu-4/`

| # | Kịch bản | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Menu thật | Đăng nhập admin, mở từng nhóm menu | Không còn mục "Chi tiết …", Loại suất & đơn giá, Tiêu chí kiểm tra, Điểm dừng, Diễn biến giao hàng, Lịch sử xử lý phản ánh, Các bước / Lịch sử duyệt, Phiếu chuyển kho, Phiếu điều chỉnh tồn, Thông tin cá nhân, Hộp phê duyệt ở nhóm Nhân sự | [ ] | |
| 2 | Quyền màn cha | Tài khoản Kinh doanh (chỉ quyền Hợp đồng) tạo hợp đồng Nháp 2 loại suất, gửi duyệt, xem tab Loại suất | Chạy như cũ (không 403) | [ ] | |
| 3 | Dòng chứng từ đã khóa | Gọi `OrderLine` / `PurchaseOrderLine` / `GoodsReceiptLine` / `MaterialIssueLine` / `StockCountLine` / `QcInspectionLine` Insert / Update / Delete | responseCode 400 – chỉ ghi qua action của màn cha (kiểm trạng thái) | [ ] | |
| 4 | Tiêu chí ATTP | Bộ kiểm tra ATTP → mở bộ → tab Tiêu chí: thêm (để trống mã), ↑ ↓, sửa tiêu chí đã có kết quả | Mã tự cấp TC-nn; thứ tự lưu; sửa nội dung / xóa tiêu chí đã dùng bị chặn với thông báo rõ | [ ] | |
| 5 | Danh mục dùng chung | Quản trị › Danh mục dùng chung | 14 danh mục tên tiếng Việt, số giá trị, màn / cột đang dùng; bấm xem mã + tên; không có nút thêm / sửa | [ ] | |
| 6 | Lọc theo nguyên liệu | Đơn mua / Phiếu nhập / Phiếu xuất / Đề nghị mua → chọn nguyên liệu | Chỉ còn chứng từ có nguyên liệu + cột SL nguyên liệu | [ ] | |
| 7 | Màn chỉ xem | Sản lượng, Xếp hàng lên xe, Xác nhận đã giao, Liên kết tài khoản chat | Không có nút Tạo mới / Sửa; nghiệp vụ (kết thúc mẻ, Load chuyến, xác nhận giao, Liên kết / Gỡ) vẫn chạy | [ ] | |
| 8 | Link cũ | Mở `#/crm-contract-meal`, `#/scm-adjustment` | Tự chuyển về Hợp đồng / Tồn kho | [ ] | |
| 9 | Script DB thật | `MENU4_cleanup_menu.sql` DryRun=1 rồi DryRun=0 (sau backup, sau deploy API) | In 21 mục; số dòng UserRole trước = sau; chạy lại không đổi gì | [ ] | **Anh chạy** |

## R1.0 – Nền móng (2026-11-14) · file thử: `docs/api-tests/r1.0-foundation.http`, `docs/api-tests/r1.1-admin-masterdata.http` (T45R)

| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Sức khỏe hệ thống | `GET /health` | `Healthy`, database `Healthy`, đúng environment | [ ] | |
| 2 | Swagger | mở `/swagger` | Nhóm theo controller (`Shift`, `InventoryTransaction`, `AuditChange`, `UserRole`…); không còn route `/api/v1` (D-30) | [ ] | |
| 3 | Đăng nhập đúng / sai | `POST /api/Authentication/Login` | Đúng → token; sai mật khẩu → 400 "Tài khoản hoặc mật khẩu không đúng" | [ ] | |
| 4 | Chặn khi chưa đăng nhập | gọi API bất kỳ không token | 401 (v1: envelope `UNAUTHORIZED`) | [ ] | |
| 5 | Cơ cấu tổ chức | `GET /api/OrganizationUnit/GetAll` | 33 đơn vị đúng sơ đồ (DLC → CEO → 5 khối + Ban cố vấn; OPS-KIT có 6 tổ) | [ ] | Anh soát tên / mã |
| 6 | Nhóm quyền | `GET /api/UserGroup/GetAll` | 16 nhóm: CEO, CEO-PMO, COO, OPS-PLAN, OPS-SCM, OPS-KIT, OPS-LOG, OPS-POS, OPS-MNT, SAL-SALES, SAL-CON, SAL-CS, FIN-ACC, FIN-HR, FIN-IT, QA | [ ] | |
| 7 | Ma trận quyền | `GET /api/UserRole/GetByUserGroupId` cho từng nhóm | Đúng ma trận ORG_STRUCTURE mục 2 (PROPOSED, D-25) | [ ] | **Anh duyệt** |
| 8 | Phân quyền thật | tạo user, gán vào 1 nhóm (vd. OPS-SCM), đăng nhập user đó | Xem được màn được phép; thêm / sửa màn không được phép → 403 | [ ] | |
| 9 | Duyệt | (khi có API duyệt) user OPS-SCM duyệt kế hoạch sản xuất | Bị từ chối; COO / CEO được | [ ] | Nhóm duyệt: appsettings `Permissions:ApproverGroups` |
| 10 | Ca làm việc | `POST api/Shift/Insert` (form) | Tạo ca qua đêm được; trùng mã / giờ bắt đầu = kết thúc → responseCode 400 kèm thông báo; gửi 2 lần cùng Idempotency-Key chỉ tạo 1 | [ ] | |
| 11 | Xóa bản ghi đang dùng | `DELETE api/Shift/Delete?id=` với ca đang có phân ca | responseCode 400 "Không thể xóa dữ liệu… đang được sử dụng" | [ ] | |
| 12 | Sổ kho chỉ ghi thêm | gọi `Update` / `Delete` sổ kho; `POST api/InventoryTransaction/Reverse` 2 lần | Không có Update / Delete; đảo lần 2 → responseCode 400 "đã được đảo" | [ ] | |
| 13 | Lịch sử xử lý | `GET /api/AuditChange/GetAll?EntityName=<Entity>&IdChange=<Id>` sau khi sửa 1 bản ghi | Có dòng: thời điểm, người thao tác, nội dung thay đổi | [ ] | |
| 14 | Bảo mật | xem response đăng nhập / danh sách user | Không bao giờ thấy trường mật khẩu | [ ] | |

## R1.1 – Danh mục và quản trị (T45R + T45B.5, 2026-11-15) · file thử: `docs/api-tests/r1.1-admin-masterdata.http` · tài liệu: `docs/frontend-handoff/P0-P2.md`

| # | Kịch bản | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Ma trận quyền | `UserRole/GetMatrix` 1 nhóm → bật quyền 1 menu bằng `UserRole/Update` → user nhóm đó gọi lại ngay | Quyền áp dụng ngay (không chờ 5′) | [ ] | |
| 2 | Quyền duyệt | Nhóm OPS-PLAN không có mục "Duyệt – Công thức" → `Recipe/Approve` | 403; cấp `allowEdit` trên mục đó → duyệt được | [ ] | |
| 3 | Nhóm mới | `UserGroup/Insert` rồi xem ma trận | Mọi cờ = false (PROPOSED) | [ ] | |
| 4 | Khóa tài khoản | `User/SaveIsActive` user khác; thử tự khóa mình | User bị khóa → 403 ngay; tự khóa → responseCode 400 | [ ] | |
| 5 | Menu | `ObjectMenu/GetTree`, `UpdateOrder` tạo vòng lặp | Cây đúng; vòng lặp → responseCode 400 | [ ] | |
| 6 | Khách 360° | `Customer/GetOverview` | Liên hệ, điểm giao, hợp đồng + suất, 15 đơn gần nhất, KPI | [ ] | |
| 7 | Điểm giao | Ngừng điểm giao còn đơn chưa giao | responseCode 400 | [ ] | |
| 8 | Giá NCC | 2 NCC cùng nguyên liệu bật ưu tiên; `SupplierMaterial/GetAll?EffectiveOn=` | Chỉ còn 1 ưu tiên; lọc theo ngày đúng | [ ] | |
| 9 | Quy đổi | `UomConversion/Convert` kg → g, g → kg, bao → g qua kg | 1000 / 0,001 / qua trung gian đúng; không có quy đổi → 400 | [ ] | |
| 10 | Công thức | Tạo phiên bản → dòng → gửi duyệt → duyệt → khóa; tạo V2 sao chép → khóa | V1 Obsolete, EffectiveTo = ngày trước V2; giá thành chuẩn mới; sửa dòng khi không Draft → 400 | [ ] | |
| 11 | Giá vốn | `Recipe/CalculateCost?mealCount=50` | Đúng theo giá NCC ưu tiên + quy đổi; dòng thiếu giá có cảnh báo | [ ] | |
| 12 | Chính sách giá | `PricePolicy/Resolve`; thêm chính sách chồng hiệu lực | Thứ tự hợp đồng → khách → ContractMeal; chồng → 400 | [ ] | |
| 13 | Trạng thái / tham chiếu | `MetaData/GetStatuses`, `MetaData/GetReferenceLists` | 47 bộ trạng thái; MealType SANG / TRUA / TOI / DEM (PROPOSED) | [ ] | |
| 14 | Nhập Excel – mẫu | Tải mẫu Customer / Ingredient / Dish / RecipeLine / SupplierMaterial, điền vài dòng (cố ý 1 dòng thiếu cột *) rồi `ImportExcel/Import` | Dòng đúng được nạp; dòng lỗi báo đúng số dòng + lý do; nạp lại không trùng | [ ] | |
| 15 | Nhập Excel – file thật | Anh tự nạp `Rule Menu.xlsx` (RuleMenu_DishBank rồi RuleMenu_DishDetail) và `Tool_Tong hop bao gia` (BaoGia_Summary) | Món / nhóm món / nguyên liệu / NCC / bảng giá đúng; ghi chú GAP-IMP-01 (không có định lượng), GAP-IMP-02 (VAT) | [ ] | Chỉ anh nạp dữ liệu thật |

## PILOT-FE – Giao diện (thử tay trên trình duyệt) · FE-0, FE-1 (2026-11-16)

| # | Màn hình | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Khung (FE-0) | Đăng nhập; tải lại trang; đổi mật khẩu; đăng xuất; để token hết hạn | Menu theo quyền; tải lại vẫn đúng menu; đổi mật khẩu xong đăng nhập bằng mật khẩu mới; 401 → về đăng nhập | [ ] | |
| 2 | Khung | Sidebar, ô chọn chi nhánh, màn `scm-uom`, máy tính bảng | Nhóm cấp 1 → màn cấp 2; chi nhánh lấy từ OrganizationUnit; chữ / nút đủ lớn; không còn chữ "phòng khám" / "W3CRM" | [ ] | |
| 3 | adm-user | Tạo user 2 nhóm + chọn hồ sơ → đăng nhập (mật khẩu = tên); sửa nhóm; khóa / mở; tự khóa mình; reset mật khẩu (admin) | Đúng; các trường khác không mất sau khi sửa; tự khóa → thông báo lỗi; nút reset chỉ admin thấy | [ ] | |
| 4 | adm-usergroup | Thêm / sửa / xóa; xóa nhóm đang dùng; nút "Phân quyền" | Xóa nhóm đang dùng → thông báo; "Phân quyền" mở adm-permission đúng nhóm | [ ] | |
| 5 | adm-permission | Tick Sửa → tự bật Truy cập + nhóm cha; bỏ Truy cập → xóa dòng; Lưu; Cấp / Thu hồi tất cả; Sao chép từ nhóm; Xem trước (admin); dòng "Duyệt" | Tải lại thấy đúng cờ; user nhóm đó thấy / mất màn hình sau khi đăng nhập lại; dòng duyệt lưu được | [ ] | |
| 6 | adm-objectmenu | Kéo thả / lên xuống; ẩn / hiện; thêm con; sửa; xóa | Thứ tự sidebar khớp sau khi tải lại | [ ] | |
| 7 | adm-orgunit | Cây; thêm con; đổi quản lý; đặt cha là con của mình; trùng mã | Vòng lặp / trùng mã → thông báo; đơn vị BRANCH hiện ở ô chọn chi nhánh | [ ] | |
| 8 | adm-employee, adm-position | Thêm / sửa (ngày không lệch 1 ngày, trường khác giữ nguyên) / lọc / xóa | Đúng | [ ] | |
| 9 | Mọi màn | User thường không có quyền Thêm / Sửa / Xóa | Nút ẩn đúng; không có quyền xem → trang "không tìm thấy" | [ ] | |
### T45R.8 – Hoàn thiện quản trị người dùng (backend, 2026-11-16) · file thử: `docs/api-tests/t45r8-admin-users.http` · tài liệu: `docs/frontend-handoff/P0-P2.md` mục 6
Trước khi thử trên staging: chạy `DB/seed/ObjectMenu_disable_legacy.sql` (hoặc `deploy/db/Deploy-Database.ps1` – đã có trong danh sách seed) và `DB/sp/uspUser_GetAll.sql`.

| # | Kịch bản | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Tắt menu cũ (D-37) | Chạy script 2 lần; đăng nhập xem `listMenu` | Lần 1 tắt 26 mục (21 cấp 2 + 5 cấp 1), lần 2 tắt 0, không lỗi; không còn "Quản lý…", "Khám bệnh", "Kho thuốc"… | [ ] | Không xóa, không đổi UserRole |
| 2 | Tạo người dùng 1 bước (D-38) | `Person/Insert` có Họ tên + `UserName` + `ListUserGroupId` + `IsActive=true` | Đúng 1 Person + 1 User + nhóm; response có `tempPassword` 12 ký tự | [ ] | |
| 3 | Chặn tài khoản thứ 2 / trùng tên | `User/Insert` cho hồ sơ vừa tạo; `Person/Insert` với tên đăng nhập trùng (khác hoa thường) | responseCode 400, không tạo hồ sơ thừa | [ ] | |
| 4 | Mật khẩu tạm (D-39) | Admin `User/ResetPassword` → đăng nhập bằng mật khẩu tạm → gọi 1 màn bất kỳ → `User/ChangePassword` → gọi lại | Login có `mustChangePassword = true`; API khác 403 "Cần đổi mật khẩu"; đổi xong dùng bình thường; mật khẩu tạm không có trong `AuditChange` (tab Lịch sử của User chỉ ghi "PassWord: đã thay đổi") | [ ] | |
| 5 | Mật khẩu tạm hết hạn | Đăng nhập bằng mật khẩu tạm sau 24 giờ | "Mật khẩu tạm đã hết hạn, liên hệ quản trị" | [ ] | Có test tự động (TestClock) |
| 6 | FE-1 follow-ups | `User/GetAll?IsActive=false`; `UserRole/SaveAccessAll`; `ObjectMenu/Insert` dưới mục cấp 2 | Chỉ user bị khóa; nhóm menu cấp 1 cũng được cấp; menu cấp 3 → 400 | [ ] | |

## TOÀN LUỒNG R1 bằng API (BE2.9, 2026-11-17) · file thử: `docs/api-tests/full-flow.http` · test tự động: `AppDelicare.Tests/Pilot/FullFlowSqlServerTests.cs`
**Thay phạm vi PILOT bên dưới (D-44: không chạy Pilot – kiểm theo toàn luồng).** Thử trên **staging** (`http://localhost:5200`, deploy/README.md mục 9 – khởi động lại theo mục 9 bước 4) hoặc dev. Chạy `full-flow.http` **từ trên xuống**; mỗi lần đổi `@key`, `@day` (ngày ăn tương lai chưa có kế hoạch SX, ≤ hôm nay + 20 ngày), `@dow`; trên staging đổi `@base` + `@password`. Lỗi nghiệp vụ = HTTP 200 + `responseCode` 400 + thông báo tiếng Việt. Dòng `# @expect` = số liệu cần thấy.
**Vai trò:** lần 1 chạy bằng `admin`; lần 2 đăng nhập đúng nhóm từng bước (cột "Vai trò") – thiếu quyền màn → HTTP 403, thiếu mục duyệt → 400 "không có quyền duyệt". Kết quả chạy thật: dev 216 / 216 (22 chốt an toàn, 60 @expect – key B29F2 / B29F3), staging 216 / 216 (key B29S, ngày 01/12/2026).

| # | Phân hệ – kịch bản (mục trong file) | Vai trò (nhóm gợi ý) | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Danh mục tối thiểu (0): đơn vị, kho, NL bò (MÁT) / gạo (KHÔ), NCC + bảng giá, 2 món + công thức (CreateVersion → dòng → Submit → Approve → Lock), khách + điểm giao 11:00, xe, dây chuyền 1000 suất / mẻ | OPS-PLAN, OPS-SCM, SAL-SALES | Mọi bước 200; công thức Đã khóa | [ ] | |
| 2 | **Chốt – lô bị chặn** (0.10–0.11): lô bò hết hạn 50 kg + lô `KHONG_DAT` 30 kg qua `InventoryTransaction/Adjust` → `ApprovalRequest/Decide` | OPS-SCM; duyệt: `scm-adjustment` | `pendingApproval = true`, duyệt xong mới ghi sổ; các bước sau không dùng 2 lô này | [ ] | |
| 3 | Hợp đồng (1): `Contract/Create` (kèm suất TRƯA 35.000) → `Submit` → `Approve` → `GetDetail` | SAL-SALES; duyệt: `crm-contract` | Số `HD-YYMMDD-NNN` Nháp → Hiệu lực; thêm loại suất khi đã Hiệu lực → 400 | [ ] | |
| 4 | Dự báo (2): `Forecast/Import` 1000 suất → `Publish` → `GetMatrix` | SAL-SALES / OPS-PLAN | Ma trận 1000 (đã công bố) | [ ] | |
| 5 | Thực đơn + đơn (3): menu → món → `GetCalendar` → `Approve` → phân công; `Order/GetNewTemplate` → `Create` 1000 → `Submit` → `Confirm` → `Lock` | OPS-PLAN; SAL-CS | Gợi ý `suggestedQuantity = 1000`; đơn `PH-YYMMDD-NNNN` 35.000.000; lệch dự báo 0% (không cảnh báo); Đã chốt | [ ] | |
| 6 | Hàng sắp về (4): đề nghị mua tay gạo 50 kg → Submit → duyệt 2 cấp → PO 1.000.000 (1 cấp) → gửi NCC | OPS-SCM; duyệt `scm-pr-l1`, `scm-pr-l2`, `scm-po` | PO Đã gửi, chưa nhận | [ ] | |
| 7 | MRP (5): `MaterialRequirement/Calculate` → `ExportPreProduction` | OPS-PLAN | Bò cần 200, bị chặn 80, mua 200; gạo cần 120, **hàng sắp về 50**, mua 70; KH- Nháp + đề nghị Nháp | [ ] | |
| 8 | Đề nghị 2 cấp + PO Giám đốc (6): Convert khi Nháp → 400; Submit → cấp 1 → `GetDetail` (`stepNo 2`) → cấp 2 → Convert → PO 61.400.000 → Submit; hộp `scm-po` **không** có, `scm-po-director` có → GĐ duyệt → Send → in PO | OPS-SCM; COO; CEO (GĐ) | `needsDirectorApproval = true`; người chỉ có `scm-po` duyệt → 400 | [ ] | |
| 9 | Nhận hàng + QC (7): `GoodsReceipt/Create` → Post trước QC → 400; bò 9 °C DAT → 400; QC 3,5 °C → Post; nhận PO định kỳ gạo 50 (hạn +20 ngày); in phiếu nhập xlsx / docx, pdf → 400 | OPS-SCM (kho), QA | Mỗi dòng 1 lô `NL-YYMMDD-<NCC>-NN` khác số; TemperatureLog RECEIVING; PO Đã nhận | [ ] | PDF: D-56 b |
| 10 | Kho (8): `InventoryBalance/GetSummary`; xuất tay đích danh lô hết hạn → 400 | OPS-SCM | Chặn lô hết hạn | [ ] | |
| 11 | Kế hoạch + giữ chỗ (9): `ProductionPlan/Approve` → `Release` → `ProductionOrder/GetDetail` | OPS-PLAN; duyệt kế hoạch | 2 mẻ `ME-YYMMDD-TRUA-NN` (Bò kho trước), giữ chỗ bò 200 (`listReservation`) | [ ] | |
| 12 | **Sản xuất theo tổ** (10): NAU trước SO_CHE → 400; SO_CHE → xuất NL FEFO (không lô -HH / -KD) → NAU (= bắt đầu mẻ) → Finish trước xong NAU → 400 → lõi 65 °C thiếu khắc phục → 400 → 65 + khắc phục → Finish / sản lượng 1000 → Release → 400 (lõi < 70) → 75,5 °C → lưu mẫu → CHIA_SUAT trước Đạt → 400 → Release → CHIA_SUAT → `GetStageBoard` | OPS-KIT (tổ sơ chế / nấu / chia), QA | Mọi chốt 400 đúng; bảng tiến độ theo tổ | [ ] | |
| 13 | Đóng gói (11.1–11.3): `PackingOrder/GenerateFromBatch` → `ProductionBatch/Pack` → 400 → xong CHIA_SUAT → `Start` → `RecordResult` 1000 | OPS-KIT | Mẻ Đã đóng gói, 3 công đoạn Đã xong | [ ] | |
| 14 | **Checklist lỗi trọng yếu** (11.4–11.9): mẻ Cơm xuất FEFO 2 lô (lô định kỳ 50 trước) → nấu 82 °C → Đạt → QA_FG Cảm quan FAIL → `Release` → 400 → kiểm lại ĐẠT → Release → Pack (chưa lưu mẫu); `MealCost/Calculate` khi chưa giao → 400 | QA, OPS-KIT | `listCriticalFailure` ≥ 1; kế hoạch Completed | [ ] | |
| 15 | **Giao hàng** (12): chuyến `CX-` → 55 °C → Dispatch 400 → 65 °C nhưng mẻ chưa lưu mẫu → 400 (liệt kê `ME-`) → lưu mẫu → chưa xếp hàng → 400 → `Load` → Dispatch → Arrive → bàn giao 58 °C → 400 → 63 °C giao 1000 / nhận 998 | OPS-LOG (tài xế) | Chuyến Hoàn thành; POD; sổ bao bì OUT 998; phản ánh tự động (2 suất từ chối) | [ ] | |
| 16 | In / tệp giao hàng (12.11–12.13): biên bản giao chuyến, phiếu giao, POD; tải lên PDF bằng chứng POD → danh sách → tải xuống; .exe → 400 | OPS-LOG | Tệp lưu ngoài wwwroot (`App_Data/DocumentFiles`, tên SHA-256) | [ ] | |
| 17 | Hộp / khay (13.1–13.5): thu hồi 990 (hỏng 5) → kiểm tra DIRTY → mẻ vệ sinh tự lập → khử khuẩn thiếu hóa chất → 400 → 6 bước → PASS → số dư | OPS-LOG, QA | OUT 998 / IN 990 / còn 8; AVAILABLE 985 | [ ] | |
| 18 | Đối soát + phản ánh (13.6–13.8): `Reconcile` 990 / trả 5 / hủy 3 → `Close` → in biên bản; phản ánh High → Assign → Resolve → Close | SAL-CS / FIN-ACC; QA | 34.650.000; đơn Reconciled; khách có 2 phản ánh | [ ] | |
| 19 | Giá thành (14): đóng 2 lệnh SX (đóng lại → 400) → `ActualCost/GetList` → chênh lệch tiêu hao → `MealCost/Calculate` → `GetList` | OPS-PLAN / FIN-ACC | NVL 62.400.000 (bò 60.000 đ / phần); 990 suất nhận, giá vốn 62.400.000, doanh thu 35.000.000 | [ ] | Giá bán [DEMO] < giá vốn do số thử |
| 20 | Kiểm điểm dùng chung (15): phiếu xuất, in hàng loạt theo chuyến, thống kê trạng thái, xuất xlsx / csv, lịch sử PO + POD (có tệp), truy xuất ngược mẻ | Mọi nhóm có quyền In / Xuất / Xem | Tệp xlsx / docx; lịch sử có quyết định duyệt + tệp; truy xuất tới khách | [ ] | |
| 21 | Lô hết hạn (16): `MaterialLot/ProcessExpired` Preview → xử lý → chạy lại | OPS-SCM | Lô -HH Hết hạn + WASTE 50; lần 2 `expiredLotCount = 0` | [ ] | |
| 22 | Nhân sự smoke (17): nhân viên + trạm bếp có tọa độ → phân ca hôm nay → lưới ca → chấm hộ PROXY thiếu lý do → 400 → vào ca GPS ~22 m → bảng công | FIN-HR / tổ trưởng | Vào ca hợp lệ, không trả tọa độ | [ ] | Đủ kịch bản HR: `be2.6-hr.http` |
| 23 | Bảng điều hành thiếu ngày (lỗi BE2.9 đã sửa): `DeliveryExecution/GetBoard`, `DeliveryRoute/GetBoard`, `GetReadyOrders`, `ProductionBatch/GetBoard`, `GetStageBoard`, `Forecast/GetMatrix` không truyền ngày | mọi nhóm | 400 "Chọn ngày giao" / "Chọn ngày sản xuất" / "Khoảng ngày không hợp lệ" (trước: HTTP 500) | [ ] | |

Kịch bản chi tiết từng phân hệ (nhiều nhánh / chốt hơn): `be2.1-flow-gaps.http` (đóng gói, xếp xe, đơn giao), `be2.2-contract-forecast.http` (hợp đồng, dự báo, đổi món, sửa đơn sau giờ chốt), `be2.3-multilevel-approval.http` (duyệt nhiều cấp, kiểm kê), `be2.4-inventory-production.http` (giữ chỗ, hủy lệnh, công đoạn, giá thành), `be2.5-foodsafety-packaging.http` (ATTP, vệ sinh, hộp / khay, sự cố NCC), `be2.6-hr.http` (nhân sự), `be2.7-common-print.http` (in / xuất / tệp / lịch sử).

## DASHBOARD ĐIỀU HÀNH + HÀNH TRÌNH ĐƠN (BE3.5, 2026-11-17) · file thử: `docs/api-tests/be3.5-dashboard.http` · test: `AppDelicare.Tests/Dashboard/DashboardTests.cs`
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Số liệu khớp danh sách | `Dashboard/GetExecutive?date=2026-12-02` so với `Order/GetBoard`, `ProductionBatch/GetBoard`, `DeliveryExecution/GetBoard`, chuông | Số đơn / trạng thái, suất, tiến độ, giao, tồn, phản ánh trùng khớp | [ ] | mẻ Hủy không tính (D-57 ae) |
| 2 | Lọc ca / chi nhánh | Chọn 1 ca, 1 chi nhánh | Suất / đơn / mẻ / ca làm chỉ của ca / chi nhánh (gồm đơn vị con); giao không lọc ca | [ ] | |
| 3 | Cache 60 giây | Tạo 1 đơn mới cho ngày đang xem, tải lại ngay rồi sau 60 giây | Lần đầu số cũ, sau 60 giây số mới (`generatedAt` đổi) | [ ] | |
| 4 | Cảnh báo theo người | Mở dashboard bằng admin rồi bằng người chỉ có quyền Xem dashboard | Thẻ số giống nhau; khối cảnh báo của người thứ 2 chỉ theo quyền của họ | [ ] | |
| 5 | Chỉ số theo ngày | Bấm KPI Refresh (BE3.4) cho 1 ngày rồi mở dashboard ngày đó | 4 chỉ tiêu có giá trị / mục tiêu / đạt (chi phí càng thấp càng tốt) | [ ] | chưa tính → trống |
| 6 | Hành trình đơn | Bấm 1 đơn đã đối soát → `Dashboard/GetOrderJourney` | 8 chặng, số KH / lệnh SX / mẻ / đóng gói / đơn giao / chuyến / POD / đối soát / phản ánh đúng; chặng hiện tại đúng | [ ] | KH- dùng chung cả ngày |
| 7 | Quyền hành trình | Người chỉ Xem đơn hàng mở hành trình | Thấy trạng thái mọi chặng nhưng chỉ chứng từ đơn; người không có quyền → 403 | [ ] | |

## CHUÔNG CẢNH BÁO + TÌM KIẾM + LIÊN QUAN (BE3.3, 2026-11-17) · file thử: `docs/api-tests/be3.3-alerts-search.http` · test: `AppDelicare.Tests/Common/AlertsSearchRelatedTests.cs`
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Chuông theo quyền | Đăng nhập thủ kho (chỉ Xem kho / lô) → `Common/GetAlerts` | Chỉ cảnh báo tồn / lô; không thấy phản ánh / nhiệt độ | [ ] | |
| 2 | Chuông theo chi nhánh | Chọn chi nhánh trên header | Số tồn / lô / phiếu nhập / phản ánh chỉ của chi nhánh (dữ liệu chưa gắn chi nhánh vẫn hiện) | [ ] | |
| 3 | Nhiệt độ IoT | Gửi 1 lần đo vượt ngưỡng qua `IotTemperature/Receive`, đợi ≤ 60 giây | `TEMPERATURE_OUT_OF_RANGE` tăng 1 | [ ] | cache 60 giây |
| 4 | Duyệt | Gửi duyệt 1 công thức; người có quyền Duyệt mở chuông | `APPROVAL_PENDING` = 1, bấm → `#/wf-inbox` | [ ] | |
| 5 | Tìm kiếm | Gõ "don hang", mã khách, số PO | Thấy màn hình đúng quyền; bản ghi ≤ 5 / loại; không có tên bảng | [ ] | |
| 6 | Liên quan | Mở khách hàng / lô → tab Liên quan | Đơn / hợp đồng / phản ánh; lô → phiếu nhập, phiếu xuất, mẻ dùng lô | [ ] | |
## KPI (BE3.4, 2026-11-17) · file thử: `docs/api-tests/be3.4-kpi.http` · test: `AppDelicare.Tests/Kpi/KpiTests.cs`
Cần: đã chạy `DB/seed/KpiMetricDefinition_seed.sql`; tài khoản admin để bấm làm mới.
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Danh sách chỉ tiêu | `#/kpi-definition` | 11 chỉ tiêu, có mục tiêu + chiều (≥ / ≤); công thức + mục tiêu đúng ý BA / PO (PROPOSED D-57 y) | [ ] | |
| 2 | Phiên bản | Sửa mục tiêu 1 chỉ tiêu | Có phiên bản mới hiệu lực từ hôm nay, bản cũ đóng hôm qua, số KPI cũ giữ mục tiêu cũ; sửa bản cũ → báo lỗi | [ ] | |
| 3 | Làm mới Chỉ số theo ngày | Admin bấm làm mới 1 ngày có đơn giao / mẻ | Số khớp tay (vd. giao đúng giờ = đơn trễ ≤ 15′ ÷ đơn đã giao + thất bại); bấm lại không nhân đôi | [ ] | |
| 4 | Thiếu dữ liệu | Làm mới ngày không có dữ liệu | Chỉ tiêu hiện "Thiếu dữ liệu" (INCOMPLETE), không đỏ / xanh | [ ] | |
| 5 | Đạt / không đạt | Chỉ số theo ngày / tháng | Hao hụt, thất thoát, food cost: thấp hơn mục tiêu = đạt; giao đúng giờ, lưu mẫu: cao hơn = đạt | [ ] | |
| 6 | Chỉ số theo tháng | Làm mới tháng này | Số tính trên cả tháng tới hôm nay | [ ] | |
| 7 | Quyền | User không phải admin | Không bấm được làm mới (403); xem KPI theo quyền Xem màn | [ ] | |
| 8 | Nhật ký | `#/kpi-refresh` | Mỗi lần bấm 1 dòng Thành công, người chạy, số dòng; đang chạy trùng → "Đang chạy ở nơi khác" | [ ] | |

## IOT NHIỆT ĐỘ (BE3.2, 2026-11-17) · file thử: `docs/api-tests/be3.2-iot.http` · test: `AppDelicare.Tests/Iot/IotTemperatureTests.cs`
Cần: `Iot__ApiKeys__0` + 1 thiết bị `Iot__Devices__<mã>__StorageZoneCode` (biến môi trường của API – deploy/README.md).
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Khóa API | Gửi không khóa / sai khóa | HTTP 401 | [ ] | |
| 2 | Nhận lô | Gửi 1 lô (đạt, vượt ngưỡng, thiết bị lạ) | Đạt + vượt ngưỡng được ghi (IOT, người ghi system); thiết bị lạ trong `rejected` | [ ] | |
| 3 | Gửi lại | Gửi lại cùng lô | `accepted = 0`, `duplicate` = số đã ghi | [ ] | |
| 4 | Biểu đồ | `TemperatureLog/GetSeries` khu vực của thiết bị | Thấy lần đo IoT, lần vượt ngưỡng đỏ | [ ] | |

## BIỂU ĐỒ + SỰ KIỆN GIAO (BE3.1, 2026-11-17) · file thử: `docs/api-tests/be3.1-charts.http` · test: `AppDelicare.Tests/Charts/ChartTests.cs`
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Biểu đồ đối soát | `OrderReconciliation/GetList` tháng có đơn | `chart` đủ mọi ngày; tổng Đặt / Giao / Nhận khớp màn đối soát từng đơn | [ ] | |
| 2 | Hao hụt theo loại / món / ca / ngày | `MealWasteRecord/GetSummary` 4 kiểu groupBy | Tổng suất bằng nhau ở 4 kiểu; giá trị = suất × giá thành chuẩn | [ ] | |
| 3 | Biểu đồ nhiệt độ kho | `TemperatureLog/GetSeries` khu vực MÁT | Ngưỡng = khu vực; lần đo vượt ngưỡng hiện đỏ | [ ] | |
| 4 | Timeline đơn giao | `DeliveryOrder/GetEvents` 1 đơn đã giao | Xếp hàng → xuất phát → đo nhiệt → đến điểm → bàn giao, đúng thứ tự giờ | [ ] | |
| 5 | Vị trí xe | Tài xế `DeliveryEvent/RecordPosition` khi đang giao → `GetVehiclePositions` | Điểm mới nhất của chuyến = vị trí vừa ghi; chuyến chưa xuất phát → 400 | [ ] | |

## JOB TỰ ĐỘNG (T23, 2026-11-17) · file thử: `docs/api-tests/t23-jobs.http` · test tự động: `AppDelicare.Tests/Jobs/JobTests.cs`
Chạy `AppDelicare.Job` cạnh API (deploy/README.md mục "Job tự động"). Kiểm bằng admin; giờ theo giờ Việt Nam.

| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Danh sách job | `dotnet AppDelicare.Job.dll --list` và `GET api/Job/GetAll` | 5 job, giờ 20:00 / 20:30 / 00:15 / 00:30 / 08:00, lần chạy kế tiếp đúng giờ VN | [ ] | |
| 2 | Khóa đơn 20:00 | Có đơn Đã xác nhận ngày mai → chờ 20:00 (hoặc `Job/Run` LockOrders sau 20:00) | Đơn → Đã khóa; `lastRun.message` liệt kê đơn chưa xác nhận / chờ duyệt | [ ] | Trước 20:00 → Skipped "chưa đến giờ chốt" |
| 3 | MRP 20:30 | Sau bước 2 | Kế hoạch KH- ngày mai, đề nghị mua Nháp cho nguyên liệu thiếu | [ ] | |
| 4 | Lô hết hạn 00:15 | Có lô Khả dụng hạn hôm qua | Lô → Hết hạn, phiếu hao hụt PH- | [ ] | |
| 5 | Hợp đồng hết hạn 00:30 | Hợp đồng Hiệu lực hết hiệu lực hôm qua (`Contract/ExpireOverdue` preview trước) | Hợp đồng → Hết hạn, ghi chú "Hết hạn ngày …" | [ ] | |
| 6 | Chốt vắng 08:00 | Ca hôm qua không chấm vào | Phân ca → Vắng | [ ] | |
| 7 | Lỡ giờ | Tắt Job qua 20:00, bật lại trước 24:00 | Chạy bù LockOrders / Mrp ngay khi bật | [ ] | |
| 8 | Chống chạy trùng | Chạy 2 bản Job cùng lúc / bấm `Job/Run` khi job đang chạy | Bản thứ 2: Locked "đang chạy ở nơi khác" | [ ] | |
| 9 | Quyền | User thường gọi `api/Job/GetAll` | HTTP 403 | [ ] | |
| 10 | Người ghi | Xem lịch sử bản ghi (`Common/GetHistory`) của đơn do job khóa | Người thao tác = `system` | [ ] | |

## PILOT – Luồng P3 → P8 bằng API (T50R1, 2026-11-17) – **ĐÃ THAY bằng mục TOÀN LUỒNG R1 ở trên (BE2.9)**, giữ để đối chiếu · file thử: `docs/api-tests/pilot-flow.http` · test tự động: `AppDelicare.Tests/Pilot/PilotFlowSqlServerTests.cs`
Thử trên **staging** (`http://localhost:5200`, deploy/README.md mục 9) hoặc dev. Chạy `pilot-flow.http` **từ trên xuống** (đổi `@key`, `@day`, `@dow` mỗi lần). Lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + thông báo tiếng Việt; không có quyền màn hình = HTTP 403.
**Vai trò / token:** chạy lần 1 bằng `admin` (mọi quyền) để thấy luồng thông suốt; lần 2 đăng nhập **đúng người** từng bước (cột "Vai trò") – quyền = ma trận `UserRole` × mục menu (D-31), nhóm gợi ý theo ORG_STRUCTURE (PROPOSED – anh chỉnh trên màn phân quyền). Quyền duyệt = `AllowEdit` trên mục "Duyệt – …" (`#/wf-inbox?type=<màn>`).

| # | Bước (thứ tự request) | Vai trò (nhóm gợi ý) | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Danh mục: đơn vị, kho, NL (bò MÁT, gạo KHÔ), NCC + bảng giá, món + công thức (CreateVersion → RecipeLine → Submit → Approve → Lock), khách + điểm giao (giao 11:00), hợp đồng Active + suất TRUA 35.000, xe, dây chuyền 1000 suất / mẻ (`pilot-flow.http` mục 0) | OPS-SCM, OPS-PLAN, SAL-SALES; duyệt công thức: mục "Duyệt – Công thức" | Mỗi Insert `responseCode 200`; công thức Locked; danh sách `GetAll` không lọc vẫn thấy dòng | [ ] | |
| 2 | **Chốt an toàn – lô bị chặn**: lô bò hết hạn hôm qua 50 kg + lô QC `KHONG_DAT` 30 kg (`MaterialLot/Insert` + `InventoryTransaction/Adjust`) | OPS-SCM | Ghi được tồn; các bước sau **không** dùng 2 lô này | [ ] | |
| 3 | P3 Thực đơn tuần: `Menu/Insert` → `MenuLine/Insert` ×2 → `Menu/GetCalendar` (FC%) → `Menu/Approve` → `MenuAssignment/Insert` | OPS-PLAN; duyệt: mục "Duyệt – Thực đơn tuần" | Lịch có giá vốn / FC%; thực đơn Approved; `MenuAssignment/Resolve` ra thực đơn | [ ] | |
| 4 | P3 Đơn hàng: `Order/GetNewTemplate` → `Order/Create` 1000 suất → `Submit` → `Confirm` → `Lock` | SAL-CS | Số `PH-YYMMDD-NNNN`, tổng 35.000.000; trạng thái Draft → Submitted → Confirmed → Locked. Sau 20:00 ngày trước ngày ăn: tạo / sửa / xác nhận → 400 (giờ chốt) | [ ] | |
| 5 | P5 MRP: `MaterialRequirement/Calculate` (ngày ăn, kho bếp) → `ExportPreProduction` | OPS-PLAN | Kế hoạch `KH-YYMMDD` Nháp + đề nghị mua `PH-` Nháp; bò cần 200 kg, `blockedQuantity` 80 (lô hết hạn + KHONG_DAT không tính), mua 200; gạo 120; file Phiếu trước SX mở được | [ ] | |
| 6 | P6 PO: `PurchaseRequest/ConvertToPurchaseOrder` → `PurchaseOrder/GetDetail` → `Submit` | OPS-SCM | PO `PO-YYMMDD-NNNN` 62.400.000, `needsDirectorApproval = true`, `approvalScreenId = scm-po-director`; Submit → Chờ duyệt (PO ≥ 50 triệu không tự duyệt) | [ ] | |
| 7 | **Chốt an toàn – Giám đốc duyệt PO ≥ 50 triệu**: `ApprovalRequest/GetMyPending?type=scm-po` và `?type=scm-po-director`; người chỉ có quyền "Duyệt – Đơn mua hàng" gọi `PurchaseOrder/Approve` | COO (scm-po) rồi CEO (scm-po-director) | Hộp `scm-po` **không** có PO; hộp `scm-po-director` có; người chỉ có `scm-po` → 400 "không có quyền"; CEO duyệt → Approved + `ApprovalDecision`; `Send` → Đã gửi NCC; `ExportOrder` có cột VAT | [ ] | |
| 8 | P6 Nhận hàng: `GoodsReceipt/Create` (nhận đủ) → `GoodsReceipt/Post` trước QC | OPS-SCM (kho) | Phiếu `PH-` Chờ QC; Post trước QC → 400 | [ ] | |
| 9 | **Chốt an toàn – nhiệt độ nhận 3 vùng**: `QcInspection/Conclude` bò DAT 9 °C → 400; (thêm) 7 °C DAT không `remark` → 400, có `remark` → nhận có điều kiện; đông lạnh −16 °C thiếu `noThawConfirmed` → 400 | QA | Mát ≤ 5 nhận / 5–8 có điều kiện (bắt buộc lý do, lô "ưu tiên dùng trước", sự cố NCC) / > 8 loại (chỉ KHONG_DAT); đông lạnh ≤ −18 / −18…−15 / > −15; hàng khô không kiểm | [ ] | |
| 10 | P6 QC đạt 3,5 °C → `GoodsReceipt/Post` | QA → OPS-SCM | Mỗi dòng 1 lô **khác số** `NL-YYMMDD-<mã NCC>-01/02`; nhập kho phần đạt; TemperatureLog RECEIVING; PO → Đã nhận | [ ] | Lỗi trùng số lô đã sửa ở T50R1 |
| 11 | P7 `ProductionPlan/Approve` → `Release` (ShiftId trống = mọi ca) → `ExportProductionSheet` | OPS-PLAN; duyệt: mục "Duyệt – Kế hoạch sản xuất" | Lệnh SX `PH-` theo món + mẻ `ME-YYMMDD-TRUA-NN`; giao 11:00 → xuất bếp 10:00 → mẻ 08:30–09:30; đơn → Đang sản xuất | [ ] | |
| 12 | P7 `ProductionBatch/CreateMaterialIssue` (Post=true) từng mẻ → `Start` | OPS-KIT (bếp) / OPS-SCM (kho) | Phiếu xuất `PH-` chỉ lấy lô `NL-` vừa nhập (FEFO), **không** lấy lô hết hạn / KHONG_DAT; thiếu tồn → 400, không ghi gì | [ ] | |
| 13 | **Chốt an toàn – nhiệt độ lõi < 70 °C**: `RecordCoreTemperature` 65 không khắc phục → 400; 65 + khắc phục → lưu; `Finish` → `RecordOutput` → `Release` → 400; đo lại 75,5 → `RecordSampleRetained` → `Release` → `Pack` | OPS-KIT, QA | Dưới 70 °C chặn đạt QC và chặn đóng gói (lần đo gần nhất); đạt → Passed → Packed; lệnh SX / kế hoạch tự Hoàn thành khi mọi mẻ đóng gói | [ ] | |
| 14 | Mẻ 2: lõi 82 °C → đạt **khi chưa lưu mẫu** → đóng gói | OPS-KIT | Đạt kèm cảnh báo "chưa lưu mẫu" (D-42); `GetDetail` có `recordSampleRetained` trong `allowedActions` | [ ] | |
| 15 | P8 `DeliveryRoute/GetReadyOrders` → `Create` | OPS-LOG | Chuyến `CX-YYMMDD-<mã xe>-NN` + đơn giao `PH-` + điểm dừng; xuất phát kế hoạch 10:00 | [ ] | |
| 16 | **Chốt an toàn – nhiệt độ giao < 60 °C**: `RecordTemperature` 55 + khắc phục → `Dispatch` | OPS-LOG | 400 "… 55 °C dưới ngưỡng 60 °C"; lần đo vẫn lưu sổ (TemperatureLog DELIVERY) | [ ] | |
| 17 | **Chốt an toàn – lưu mẫu trước khi giao (D-42)**: `Dispatch` kèm 65 °C khi mẻ 2 chưa lưu mẫu → 400 liệt kê `ME-…-02`; `RecordSampleRetained` mẻ 2 → `Dispatch` | OPS-LOG, OPS-KIT | Sau khi lưu mẫu: Đang giao. Mẻ của ngày / ca chưa đóng gói cũng chặn xuất phát | [ ] | |
| 18 | P8 `DeliveryStop/Arrive` → `Deliver` 58 °C → 400 → `Deliver` 63 °C, giao 1000 / nhận 998 → `ExportDeliveryNote` | OPS-LOG (tài xế) | 58 °C chặn bàn giao; 63 °C → POD, đơn Đã giao, chuyến Hoàn thành; 2 suất từ chối → phản ánh `PA-` tự động; biên bản mở được | [ ] | |
| 19 | P8 Đối soát: `OrderReconciliation/GetPendingOrders` → `Reconcile` (phục vụ 990, trả 5, hủy 3) → `Close` | SAL-CS / FIN-ACC | Chênh −10 (−1%), giá trị 34.650.000 (= 35.000.000 × 990 ÷ 1000); phục vụ > nhận → 400; chênh > 1% đóng phải có ghi chú; đơn → Đã đối soát | [ ] | |
| 20 | P8 Phản ánh: `CustomerComplaint/Create` (High) → `Assign` → `Resolve` (bắt buộc nội dung) → `Close` → `GetDetail` | SAL-CS, QA | Số `PA-YYMMDD-NNNN`, hạn SLA +4 giờ; lịch sử CREATED / ASSIGNED / RESOLVED / CLOSED; Resolve khi Mới → 400 | [ ] | |
| 21 | Phân quyền | Đăng nhập user nhóm không có quyền màn (vd. OPS-LOG gọi `PurchaseOrder/Submit`) | HTTP 403; user không có mục duyệt gọi `ApprovalRequest/Decide` → 400 "không có quyền duyệt" | [ ] | |
| 22 | Danh sách không lọc | `MaterialLot/GetAll`, `Order/GetAll`, `Recipe/GetAll`, `ProductionBatch/GetAll` không truyền Id cha | Trả đủ dòng (trước T50R1 luôn rỗng) | [ ] | Lỗi đã sửa ở T50R1 |

Kết quả chạy thật trên DB dev (2026-11-17): luồng PowerShell 88/88 bước (key P9B), chính file `pilot-flow.http` 117 request (key P9C) – 8 chốt an toàn trả 400 đúng; số chứng từ ở PROGRESS mục T50R1.

### PILOT-FE – thao tác trên giao diện (để sau)
Theo D-40, các kịch bản **trên giao diện** của luồng Pilot (FE-P2 hộp duyệt, FE-P3 thực đơn / đơn hàng, FE-P4 kho, FE-P5 mua hàng – nhận hàng – QC, FE-P7 mẻ sản xuất, FE-P8 giao – đối soát – phản ánh) **viết và thử sau khi làm xong đợt FE** (thứ tự FE-3 → FE của T45R.8 → FE-P2 … FE-P8 → UAT-PILOT trên giao diện). Hiện staging đã có bản build Angular (FE-0, FE-1, FE-2, FE-3 WIP) – chỉ thử được các màn ở bảng PILOT-FE phía trên.

## KÊNH TIN NHẮN – LÕI (BOT-1.1, 2026-11-18) · file thử: `docs/api-tests/bot-1.1-channel.http` · test: `AppDelicare.Tests/Notifications/BotChannel*Tests.cs`
- [ ] Không có / sai `X-Client-Key` → `api/BotChannel/*` trả HTTP 401; từ internet qua nginx → 404.
- [ ] Gửi 2 lần cùng update (bot + update_id) → 1 dòng `InboundMessage`, lần 2 `duplicate = true`.
- [ ] ClaimInbound → Đang xử lý; CompleteInbound Đã xử lý; kết thúc lần 2 → 400; màn Tin nhắn đến: Xử lý lại chỉ với Lỗi / Bỏ qua.
- [ ] Màn Tin nhắn đi: gửi tay cho người đã liên kết → Chờ gửi; người chưa liên kết → báo lỗi; gửi lỗi → tự thử lại sau 1′; nút Gửi lại; người dùng chặn bot → Blocked, định danh Blocked.
- [ ] Màn Kênh: ConfigJson không lộ token / secret / key (hiện `***`); sửa kênh không làm mất bí mật.

## LIÊN KẾT TELEGRAM (BOT-1.2, 2026-11-18) · file thử: `docs/api-tests/bot-1.2-identity.http` · test: `AppDelicare.Tests/Notifications/BotIdentityTests.cs`
- [ ] Nhân viên quét QR → chia sẻ số → khớp đúng 1 hồ sơ → liên kết; số không khớp / trùng nhiều → bot báo liên hệ Nhân sự.
- [ ] Trưởng ca / người có quyền duyệt chỉ chia sẻ số → dùng bot nhưng không duyệt được; lấy mã trên web (hồ sơ cá nhân) → /start <mã> → duyệt được.
- [ ] HR cấp mã cho nhân viên (hạn 72 giờ) → nhân viên /start <mã> → liên kết; dùng lại mã → từ chối; mã quá hạn → từ chối.
- [ ] Khóa tài khoản trên web → bot ngừng cấp token (log TOKEN Failed). Admin gỡ liên kết → bot coi như chưa liên kết.
- [ ] Nhân viên không có tài khoản web: xem lịch ca, ca hôm nay, vào / ra ca (đúng quy tắc ca + bán kính), xem đơn của mình.

## BOT TELEGRAM – CONTAINER MESSAGING (BOT-1.3, 2026-11-18) · thử: `tools/bot-smoke/` (Telegram giả) hoặc bộ bot DEV thật · test: `AppDelicare.Tests/Messaging/MessagingHostTests.cs`
- [ ] Quét QR cổng (`/start CHAMCONG`) khi chưa liên kết → bot hiện nút "📱 Xác nhận số điện thoại".
- [ ] Chia sẻ số → liên kết, bot chào theo tên + 3 nút; người có quyền duyệt được nhắc lấy mã web; người có quyền bot DH nhận nút mở bot Điều hành.
- [ ] "⚡ Ca của tôi" hiện ca hôm nay; `/huylienket` gỡ liên kết.
- [ ] Gửi tay trên màn Tin nhắn đi → tin tới Telegram trong vài giây (tin thường không rung chuông).
- [ ] Production: webhook sai secret → 401; tắt api → Telegram tự gửi lại, không mất tin; `docker compose ps` messaging healthy.

## BOT – MENU, CHẤM CÔNG, DUYỆT (BOT-1.4, 2026-11-18) · thử: `tools/bot-smoke/flow-experience.mjs` · test: `AppDelicare.Tests/Messaging/MessagingExperienceTests.cs`
- [ ] Người ít chức năng (≤ 6) thấy các nút chức năng ngay; người nhiều chức năng / admin chỉ 3 nút, 📂 Chức năng → phân hệ → chức năng (≤ 2 lần bấm, sửa cùng tin, có ⬅️).
- [ ] Danh sách lệnh "/" của mỗi người chỉ gồm lệnh được dùng (người không duyệt không thấy /cho).
- [ ] 📍 Chấm công: gửi vị trí → vào / ra ca; vị trí chuyển tiếp hoặc cũ > 2 phút bị từ chối.
- [ ] ⏳ Chờ tôi duyệt chỉ có việc mình đúng là người duyệt; đơn nghỉ / đổi ca: 1 chạm + Hoàn tác 10″ (hoàn tác → không duyệt); PO / PR / hợp đồng: xác nhận 2 bước; Từ chối bắt buộc lý do.
- [ ] Bấm Duyệt 2 lần chỉ ghi 1 quyết định; người không có quyền / chưa nhập mã web → bot báo rõ.
- [ ] Gõ mã chứng từ (ME-…, CX-…, PO-…) → kết quả tra cứu theo quyền.

## QR PHIẾU IN + NHÓM CHAT + NHIỆT ĐỘ GIAO (BOT-1.7d, 2026-11-18) · test: `MessagingDocGroupTests`, `GroupAndTempTests`, `PrintTests`
- [ ] Nhập BotUsername cho bot Vận hành → phiếu in có dòng "Mở phiếu trên Telegram"; quét QR (FE vẽ) → bot hiện đúng phiếu; người không có quyền xem màn phiếu → báo không có quyền.
- [ ] Cấu hình nhóm "Bếp – ATTP" cho FOODSAFETY_CRITICAL / CORE_TEMP_LOW → cảnh báo vào nhóm (không gửi riêng); bấm 🙋 trong nhóm → tin của nhóm hiện tên người nhận; tin duyệt không bao giờ vào nhóm.
- [ ] Đo nhiệt độ xuất phát / bàn giao < 60 °C (có khắc phục) → điều phối nhận 🌡 có nút Nhận xử lý; đo đạt → không báo.

## QR VẬT THỂ + VỊ TRÍ TRỰC TIẾP (BOT-2.4, 2026-11-18) · test: `MessagingQrLiveTests`, `QrLiveApiTests`
- [ ] In QR `?start=LINE_<mã bàn>` dán ở bàn nấu: quét → mẻ đang chạy tại bàn, bấm ▶️ Bắt đầu / ✅ Xong mẻ ghi như web.
- [ ] QR trên xe (`VEH_<biển số>`) → chuyến hôm nay của xe, 🚀 Xuất phát; QR kệ (`LOC_<mã>`) → tồn + lô cận date; QR khu vệ sinh (`CLEAN`) → lô vệ sinh hôm nay; người chưa liên kết quét → được hướng dẫn liên kết.
- [ ] Tài xế bật chia sẻ vị trí trực tiếp tại bếp: rời bếp → bot ghi xuất phát (hoặc hỏi nhiệt độ nếu chưa đo); tới gần điểm giao (≤ 150 m) → bot tự ghi Đã đến 1 lần và gửi thẻ Giao / Không giao được; tắt chia sẻ → nút 📍 Đã đến vẫn dùng được.

## GIỜ YÊN LẶNG + TIN THEO LỊCH (BOT-2.3, 2026-11-18) · test: `Notifications/ScheduledNotificationTests`
- [ ] Nhân viên ca chiều không nhận tin 🟡 buổi sáng – tin tới lúc 60′ trước ca; người văn phòng không nhận tin 🟡 sau 21:00 (tới 07:00 sáng sau); tin 🔴 vẫn tới người đang trực.
- [ ] 07:00 / 17:00 lãnh đạo (Dashboard điều hành, bot Điều hành) nhận bản tin sáng / báo cáo cuối ngày khớp số liệu Dashboard; 19:00 NV KD nhận danh sách khách chưa chốt đơn ngày mai; 21:00 thủ kho nhận nguyên liệu ngày mai.
- [ ] Xe quá giờ xuất phát 10′ → điều phối nhận 🚚 có 🙋 Nhận xử lý; mẻ trễ 15′ → bếp nhận; ca bắt đầu 10′ còn người chưa vào → chấm công nhận danh sách; phản ánh sắp quá hạn / quá hạn, PO trễ, duyệt quá hạn → đúng người; không nhận trùng.
- [ ] Đổi giờ / tắt từng bản tin trong `Jobs` (appsettings) → `api/Job/GetAll` hiện đúng giờ mới sau khi khởi động lại.

## SỰ VIỆC LIÊN PHÒNG + LEO THANG (BOT-2.2, 2026-11-18) · test: `IncidentServiceTests`, `MessagingIncidentTests`
- [ ] Lỗi trọng yếu ATTP → QA nhận tin có 🙋 Nhận xử lý; người bấm trước được ghi, người khác thấy "X đã nhận xử lý"; ghi chú / ✅ hiện trên tin của mọi người.
- [ ] Không ai nhận 10′ → COO nhận "Sự việc chưa ai nhận" ở bot Điều hành; nhận xong → không leo thang tiếp.
- [ ] Người duyệt có đơn nghỉ từ ngày mai còn việc chờ duyệt → 16:00 nhận nhắc.

## THẺ CA / THẺ NGÀY (BOT-2.1, 2026-11-18) · test: `MessagingShiftCardTests`, `ShiftCardServiceTests`
- [ ] 15′ trước ca, nhân viên (đã liên kết bot) nhận 1 Thẻ ca được ghim; vào ca → thẻ tự đổi "Đã vào ca" (không có tin mới), nút đổi theo giai đoạn ca.
- [ ] ⚡ Ca của tôi làm mới thẻ ghim; bấm nút trên thẻ mở chức năng thành tin mới (thẻ vẫn còn).
- [ ] 07:00 quản lý (có Dashboard điều hành, bot Điều hành) nhận Thẻ ngày ghim: chờ tôi duyệt, cảnh báo, số liệu hôm nay.

## THÔNG BÁO NGHIỆP VỤ QUA BOT (BOT-1.7, 2026-11-18) · test: `Notifications/BusinessNotificationTests`
- [ ] Gửi duyệt đề nghị mua / PO / đơn nghỉ → người duyệt cấp hiện tại nhận tin ⏳ kèm nút Xem & duyệt (admin không nhận); duyệt cấp 1 → cấp 2 nhận; xong → người gửi nhận ✅ / ❌.
- [ ] Xác nhận phân ca → mỗi nhân viên (đã liên kết bot) nhận 1 tin 📅 liệt kê ca; xác nhận lại không gửi trùng.
- [ ] Checklist ATTP lỗi trọng yếu / nhiệt độ lõi < 70 °C / IoT ngoài ngưỡng → QA + trưởng ca nhận tin có chuông.
- [ ] Xe xuất phát → điểm phát nhận 🚚; phản ánh mới → CSKH nhận 📣 (mức cao ở bot Điều hành); công bố thực đơn, QC đầu vào không đạt, khóa đơn 20h, MRP thiếu NL, job lỗi → đúng người nhận.

## BOT ĐIỀU HÀNH (BOT-1.6, 2026-11-18) · thử: `tools/bot-smoke/flow-dieuhanh.mjs` · test: `MessagingDieuHanhTests`
- [ ] CEO / COO (có mục #/bot-dieu-hanh) liên kết bot DH; bot VH gửi nút mở bot DH.
- [ ] 📊 Tổng quan khớp màn Dashboard điều hành; 🎯 KPI / 📅 Chỉ số theo tháng khớp màn KPI; 🚨 Việc đang mở khớp chuông cảnh báo trên web.
- [ ] ⏳ Chờ tôi duyệt chỉ hiện việc mình đúng là người duyệt; bot DH không có phân hệ Bếp / Giao vận… (chỉ ở bot VH).

## BOT VẬN HÀNH – PHẦN CÒN LẠI (BOT-1.5c, 2026-11-18) · thử: `tools/bot-smoke/flow-15c.mjs` · test: `MessagingSwapTests`, `MessagingPhanAnhNhanSuTests`, `MessagingXemNhanhTests`
- [ ] 🔁 Đổi ca: chọn ca → thấy ca đồng nghiệp cùng đơn vị → đổi chéo / nhường ca → lý do → web màn Đơn đổi ca có đơn chờ duyệt; duyệt xong ca đổi đúng.
- [ ] CSKH: 📋 Phản ánh đang mở khớp màn phản ánh; 🙋 Nhận xử lý / ✅ Đã xử lý → lịch sử phản ánh trên web; 📣 Ghi phản ánh (tìm khách → mức độ → mô tả) → phản ánh mới có hạn xử lý.
- [ ] Nhân sự: 👥 Ai chưa vào ca đúng người (không tính người nghỉ phép / chưa tới giờ); ✍️ Chấm hộ có lý do → web màn chấm công ghi "Chấm hộ", hình thức PROXY.
- [ ] Kho / Kế hoạch / Kinh doanh / Kế toán / QA: mỗi lệnh xem khớp số liệu màn web tương ứng; người không có quyền màn không thấy chức năng; /hethong chỉ admin.

## BOT VẬN HÀNH – GIAO VẬN, ĐIỂM PHÁT (BOT-1.5b, 2026-11-18) · thử: `tools/bot-smoke/flow-giaovan.mjs` · test: `AppDelicare.Tests/Messaging/MessagingGiaoVanTests.cs`
- [ ] Tài xế (nhân viên gắn với tài khoản, được phân chuyến hôm nay): 🚚 Chuyến của tôi → thẻ chuyến đúng xe / giờ / điểm; ⛔ điều kiện xuất phát còn thiếu giống web.
- [ ] 🚀 Xuất phát → chọn nhiệt độ → web màn chuyến xe: Đang giao, có lần đo nhiệt độ; đã đo đạt trước đó → nút xuất phát 1 chạm.
- [ ] Tại điểm: 📍 Đã đến → ✅ Giao (số suất điền sẵn = đã xếp) → nhiệt độ → tên người nhận → web: điểm Đã giao, có biên bản giao (POD); ❌ Không giao được → lý do hiện trên web.
- [ ] 📥 Nhận hộp về → gõ số → web màn thu hồi hộp khay có phiếu mới.
- [ ] Điều phối: 🗺 Giao vận hôm nay khớp màn chuyến xe. Điểm phát: 🚚 Xe đến hôm nay; 🧮 Đối soát suất (điền sẵn số khách nhận) → web màn đối soát cập nhật.
- [ ] Người không có quyền màn chuyến / điểm dừng / đối soát không thấy các chức năng này.

## BOT VẬN HÀNH – CHUNG, BẾP, ATTP (BOT-1.5a, 2026-11-18) · thử: `tools/bot-smoke/flow-vanhanh.mjs` · test: `AppDelicare.Tests/Messaging/MessagingVanHanhTests.cs`
- [ ] Quét QR cổng khi đã liên kết → bot xin vị trí chấm công ngay.
- [ ] 📅 Lịch ca 7 ngày đúng như màn phân ca; 🏖 Xin nghỉ bằng nút (loại → ngày → số ngày → lý do) → đơn hiện trên web màn Đơn xin nghỉ, chờ duyệt.
- [ ] Tổ trưởng bếp: 🍳 Mẻ cần làm → Bắt đầu → Nhiệt độ lõi (nút / gõ; < 70 °C bị báo) → Lưu mẫu → Xong mẻ (số suất điền sẵn) → trên web mẻ đổi trạng thái đúng.
- [ ] QA: 🧽 Checklist trong 1 tin → Hoàn tất → web màn kết quả checklist có đủ dòng; lỗi trọng yếu → mẻ bị giữ / cảnh báo như web.
- [ ] Người không có quyền màn Bếp / ATTP không thấy các chức năng này trên bot.

## UI-6.23 – Kế hoạch sản xuất: 7 ngày tới, một kế hoạch / ngày (D-104)
- [ ] Mở Sản xuất › Kế hoạch sản xuất → mặc định tab **7 ngày tới**, kỳ từ hôm nay; ◀ ▶ đổi 7 ngày; dải 4 bước + 4 chỉ số đúng số.
- [ ] Không còn nút **Tạo mới**, hộp **Lập từ đơn hàng**, ô **Đơn vị**, dòng "GAP-C-04" (cả tab Danh sách kế hoạch).
- [ ] Ngày chưa có kế hoạch nhưng có đơn chốt → **Lập trước**: còn đơn chưa chốt → kế hoạch Nháp (thông báo "Còn N suất chưa chốt…"), mở trang chi tiết kế hoạch.
- [ ] Ngày mọi đơn đã chốt → **Tính lại**: đủ công thức + nguyên liệu → tự duyệt + ra lệnh (thông báo số lệnh); thiếu → Bị giữ lại kèm lý do.
- [ ] Ngày **Bị giữ lại** → **Xem lý do**: bảng nguyên liệu Cần / Giữ được / Thiếu / Đang về; **Vẫn duyệt** (COO) → Đã duyệt → **Ra lệnh n ca**.
- [ ] Gọi Generate 2 lần cho cùng ngày (kể cả gửi Đơn vị) → vẫn 1 kế hoạch.
- [ ] Vai trò: OPS-PLAN thấy Lập / Tính lại / Ra lệnh, không có Vẫn duyệt; COO có Vẫn duyệt; OPS-KIT chỉ xem; Kinh doanh / Kế toán không mở được (403).
- [ ] Điện thoại 390: bảng cuộn ngang, cột tên dòng đứng yên.

## UI-6.24 – Sản xuất liền mạch: Bảng sản xuất ngày, lệnh tự đóng, menu Sản xuất 4 mục (D-105)
- [ ] Menu Sản xuất chỉ còn: Kế hoạch sản xuất · Mẻ nấu · Hao hụt · Dây chuyền & trạm bếp. Link cũ `#/prod-order?id=` → trang lệnh; `#/prod-order` → Kế hoạch; `#/prod-output` → Mẻ nấu tab Sản lượng; `#/prod-workstation` → Dây chuyền tab Trạm bếp.
- [ ] Trang kế hoạch = Bảng sản xuất ngày: 4 chỉ số đúng; ma trận Món × Ca (nhóm món gập được, dòng tổng, Cả ngày); màu ô theo trạng thái mẻ; bấm ô → hộp lệnh (mẻ, giữ chỗ, phiếu xuất; nút Giữ chỗ bổ sung / Tính giá thành / Hủy lệnh / Mở trang lệnh theo trạng thái).
- [ ] Mẻ cuối của lệnh Đóng gói (hoặc Loại) → thông báo "hệ thống tự đóng"; lệnh = Đóng, có chênh lệch + giá thành, giữ chỗ dư = Đã giải phóng.
- [ ] Màn Job tự động: "Đóng lệnh sản xuất đã xong" 22:00; Chạy ngay → đóng lệnh còn sót, chạy lại = 0. Có chênh lệch vượt ngưỡng → 1 tin cho Bếp trưởng + COO.
- [ ] Trang lệnh: không có Tạo mới / Sửa chung; **Sửa** cạnh Phụ trách / Ghi chú lưu được (ô chọn nhân viên có tìm).
- [ ] Thêm mới lệnh / mẻ bằng API → câu tiếng Việt "…Vào Kế hoạch sản xuất → chọn ngày → Ra lệnh."
- [ ] Vai trò: OPS-PLAN thao tác lệnh được; OPS-KIT / COO / QA chỉ xem bảng & lệnh; Kinh doanh không mở được.

## UI-6.26 – Dây chuyền bếp gắn theo nhóm món (D-107)
- [ ] Màn Dây chuyền & trạm bếp → tab Dây chuyền: cột Công suất, Phút / mẻ, Nhóm món đang gán, Số món, Trạng thái; còn nhóm chưa gán → cảnh báo + **Gán nhanh** lưu được nhiều nhóm một lần.
- [ ] Thêm dây chuyền (Tên, Công suất, Thời gian 1 mẻ, tick nhóm món) → mã DC-NNN tự sinh, nhóm được gán; bỏ tick → nhóm về "chưa gán". Dây chuyền Ngừng hoạt động không gán được.
- [ ] Màn Món ăn: cột nhóm hiện dây chuyền của từng nhóm (cam = chưa gán); ⋯ → Gán dây chuyền; đổi tên nhóm không mất dây chuyền.
- [ ] Ra lệnh kế hoạch có món của 2 nhóm / 2 dây chuyền: mỗi lệnh đúng dây chuyền, số mẻ = suất ÷ công suất (làm tròn lên), giờ mỗi mẻ theo phút / mẻ; món nhóm chưa gán → 1 mẻ + cảnh báo. Bảng mẻ không còn cột "Chưa gán dây chuyền" khi mọi nhóm đã gán.
- [ ] Tab Bàn nấu: không còn tọa độ / bán kính; In QR bàn nấu vẫn in được.

## UI-6.25 – Mẻ nấu 1 chạm, chế độ Dây chuyền / Bếp trưởng, lưu mẫu theo món × ca (D-106)
- [ ] Màn Mẻ nấu: một ngày (◀ ▶, chọn ngày), 4 thẻ ca (giờ xuất bếp, x/y mẻ xong, trễ, tiến độ); công tắc Dây chuyền / Bếp trưởng nhớ sau khi tải lại.
- [ ] Dây chuyền: chọn dây chuyền → Bắt đầu nấu → **Xong mẻ**: nhập lõi bằng bàn phím số (Đạt / Dưới ngưỡng hiện ngay; dưới ngưỡng bắt nhập khắc phục), số suất −/+, tích "Đã lưu mẫu" (không tích sẵn), hao hụt chip → thông báo "tự Đạt QC" hoặc "Chờ QC: …".
- [ ] Mẻ thứ 2 cùng món trong ca: hộp Xong mẻ hiện "Đã lưu ở mẻ trước" → tự Đạt không cần lưu lại. Món khác chưa lưu → Chờ QC "Chưa lưu mẫu"; bấm **Đã lưu mẫu** ở khối Chờ QC.
- [ ] **Xong & bắt đầu <món kế tiếp>** chỉ hiện khi có mẻ Chờ nấu cùng dây chuyền; bấm → mẻ kế tiếp Đang nấu.
- [ ] Bếp trưởng: 4 chỉ số; Cần làm ngay + **Bắt đầu cả n mẻ**; tab Theo giờ (thanh mẻ, viền đỏ trễ, vạch Bây giờ / Hạn xong nấu); tab Sản lượng & hao hụt ca.
- [ ] Xuất chuyến: mẻ của món × ca đã lưu mẫu (ở mẻ khác) không còn bị chặn "chưa lưu mẫu".
- [ ] Vai trò: OPS-KIT thao tác được; QA thấy Đạt / Tạm giữ / Loại ở mẻ Chờ QC; COO chỉ xem; Kinh doanh không mở được.

## UI-6.27 – Giá vốn & lãi gộp (D-108)
- [ ] Menu Điều hành có "Giá vốn & lãi gộp"; không còn nhóm Giá thành. Link cũ `#/cost-standard`, `#/cost-actual` mở màn mới tab Theo món.
- [ ] Tháng ◀ ▶; 4 chỉ số (NL / suất so tháng trước, tỷ lệ NL / DT có màu theo ngưỡng, Lãi sau nguyên liệu, Chi phí lỗi); dòng "chỉ gồm nguyên liệu"; dòng "N đơn tạm tính" khi lệnh SX chưa đóng.
- [ ] Tab Theo khách hàng: tỷ lệ cao nhất trên đầu, dòng Cả tháng = tổng; bấm dòng → theo ngày → đơn; vượt ngưỡng có nút Lập phụ lục điều chỉnh giá.
- [ ] Tab Theo món: chuẩn vs thực tế, chênh lệch màu, nguyên nhân; bấm món mở Bảng sản xuất ngày. Tab Xu hướng: biểu đồ + đường ngưỡng + Xem bảng số.
- [ ] Thêm / sửa giá thành chuẩn bằng API → báo "do hệ thống tự tính…".
- [ ] Thực đơn tuần: ngày có món thiếu giá chuẩn hiện "thiếu giá N món"; ngày đỏ bấm → gợi ý món rẻ hơn cùng nhóm trước.
- [ ] Quyền: CEO, COO, SAL-HEAD, FIN-ACC xem; OPS-KIT không thấy mục.

## UI-6.28 – Chỉ số kinh doanh, thứ tự nhóm Điều hành (D-109)
- [ ] Sidebar Điều hành: Bảng điều hành · Chỉ số kinh doanh · Giá vốn & lãi gộp · Phê duyệt (admin, CEO, COO, SAL-HEAD, FIN-ACC); vai trò chỉ có quyền duyệt (vd OPS-WH) thấy đúng 1 mục Phê duyệt.
- [ ] Chỉ số kinh doanh: công tắc Theo ngày | Theo tháng đổi đúng dữ liệu, tải lại trang vẫn nhớ; Làm mới số liệu (admin) chạy job ngày / tháng theo chế độ.
- [ ] Link cũ `#/kpi-monthly` mở Chỉ số kinh doanh chế độ tháng.

## UI-6.29 – Kho 5 mục, Nhận hàng & kiểm tra, Sổ kiểm tra hàng nhập (D-110)
- [ ] Sidebar Kho (uat.ops-wh): Nhận hàng · Xuất kho sản xuất · Tồn kho · Kiểm kê · Kho & vị trí; Chất lượng: "Sổ kiểm tra hàng nhập".
- [ ] Nhận hàng: khối đơn mua hẹn giao hôm nay + quá hẹn (◀ ▶ đổi ngày), không có nút "Tạo mới"; ô "Đơn mua khác" mở form.
- [ ] Thủ kho nhận đơn chỉ có nguyên liệu khô không yêu cầu kiểm tra → báo "đã nhập kho", phiếu Đã ghi sổ, có lô, đơn mua cập nhật.
- [ ] Thủ kho nhận hàng cần kiểm tra → "Gửi QA" → phiếu Chờ QC; uat.qa mở Phê duyệt › Kiểm tra hàng nhập → form điền sẵn → "Kết luận & nhập kho" → Đã ghi sổ.
- [ ] Nhập nhiệt độ hàng mát 7 °C → vùng "Nhận có điều kiện"; 9 °C → gợi ý Không đạt; Không đạt bắt buộc nhận xét; đông lạnh có điều kiện bắt buộc tick không rã đông.
- [ ] Tồn kho 4 tab (Theo nguyên liệu / Theo lô & hạn dùng / Sổ kho / Định mức theo kho); Chuyển kho / Điều chỉnh tồn chỉ ở tab đầu.
- [ ] Kho & vị trí 3 tab; tab Vị trí kệ có "In QR vị trí kho". Xuất kho sản xuất tab Giữ chỗ nguyên liệu – thủ kho bấm được Giữ chỗ bổ sung.
- [ ] Link cũ #/scm-lot, #/scm-inv-txn, #/scm-inv-policy, #/scm-zone, #/scm-location, #/scm-reservation, #/scm-consumption-var mở đúng màn / tab.
- [ ] Sổ kiểm tra hàng nhập: gom theo ngày, lọc kỳ / kết quả / bảo quản / NCC / chỉ nhiệt độ ngoài ngưỡng, 4 chỉ số, ngăn chi tiết + "Lô này đã đi đâu", tab Theo nhà cung cấp, In sổ kiểm thực bước 1, Xuất Excel; không có nút Sửa / Xóa / Kết luận. Điện thoại: thẻ gọn.

## UI-6.30 – Kiểm tra theo ca / mẻ (D-111)
- [ ] Hôm nay: ◀ ▶ đổi ngày; 4 thẻ; ma trận Bộ × Ca đúng màu (quá hạn đỏ theo giờ xuất bếp), cột ca đang chạy tô nổi; không có bộ công đoạn / lưu mẫu / vệ sinh khay.
- [ ] Bấm ô → form: mặc định Đạt, "Đạt tất cả" ghi một lượt; Không đạt bắt buộc khắc phục; ô giá trị đo chỉ ở tiêu chí có chuẩn.
- [ ] Tiêu chí trọng yếu Không đạt: không chọn mẻ / không lý do → báo lỗi; chọn 2 mẻ → thông báo "Mẻ bị giữ"; mẻ Đạt chuyển Chờ QC; mẻ Đã đóng gói không xuất chuyến được; kiểm lại Đạt → mở chặn.
- [ ] Bản ghi mới không gắn trạm bếp cố định (trạm theo mẻ hoặc trống).
- [ ] Sổ kiểm tra: gom theo ngày, lọc kỳ / bộ / ca / kết quả / chỉ lỗi trọng yếu, nhãn "Kiểm lại", In sổ kiểm thực bước 2, Xuất Excel; không Sửa / Xóa. Tỷ lệ đạt: biểu đồ 30 ngày + tiêu chí hay Không đạt.
- [ ] Quyền: uat.qa, uat.ops-kit ghi được; uat.coo chỉ xem (ô không bấm được); uat.ops-wh / uat.sal-sales không vào được. Bot gửi "⏰ Kiểm tra ATTP quá hạn" 1 tin / ca.

## UI-6.31 – Nhật ký nhiệt độ (D-112)
- [ ] Hôm nay: ◀ ▶, nhãn ca đang chạy, 4 thẻ, khối "Cần xử lý ngay", thẻ mỗi khu (Cảm biến / Ghi tay, đường 24 giờ, màu trạng thái); bấm thẻ → Biểu đồ của khu.
- [ ] Ghi một lượt: nhập là tô xanh / đỏ; ngoài ngưỡng bắt buộc khắc phục; lưu một lần; ngoài ngưỡng → bot báo trưởng ca + QA.
- [ ] Đo lại & ghi khắc phục ghi lần đo mới; lần ngoài ngưỡng cũ chuyển "đã khắc phục".
- [ ] Khu ghi tay quá 60′ đầu ca chưa đo → "Đến giờ chưa đo" + 1 tin bot / ca; cảm biến im quá 30′ → "Cảm biến mất tín hiệu", có số đo lại → hết.
- [ ] Sổ nhiệt độ: gom theo ngày, lọc khu / nguồn / chỉ ngoài ngưỡng / gồm lõi – giao – nhận, In sổ, Xuất Excel, không Sửa / Xóa. Biểu đồ: chỉ một biểu đồ, 48 giờ / 7 / 30 ngày.
- [ ] Quyền: uat.qa, uat.ops-kit, uat.coo vào được; uat.ops-wh, uat.sal-sales không (theo ma trận).

## UI-6.32 – Vệ sinh khay hộp (D-113)
- [ ] Sidebar Chất lượng: không còn "Kiểm tra vệ sinh"; Đóng gói & Giao hàng: không còn "Kiểm tra khay hộp thu hồi"; link cũ mở Vệ sinh khay hộp (tab Sổ / Hôm nay).
- [ ] Thanh "Khay sạch cho ngày mai" khớp sổ bao bì và suất đã chốt ngày mai (báo thiếu đỏ).
- [ ] Kiểm tra: chọn kết quả một chạm, gợi ý Thiếu / Hỏng đúng, người kiểm = tài khoản đăng nhập, khác Đủ bắt buộc ghi chú → tự lập mẻ.
- [ ] Ghi bước: mặc định Đạt, hóa chất điền sẵn; "Đạt tất cả & kết luận" xong mẻ một chạm; mẻ có bước Không đạt thì nút bị khóa; Vệ sinh lại lập đúng mẻ mới.
- [ ] Sổ vệ sinh: gom theo ngày, lọc, bấm mẻ xem 6 bước, In / Excel, không Sửa / Xóa. Điện thoại: 3 cột xếp dọc.
- [ ] Quyền: uat.qa, uat.ops-kit, uat.coo vào được (theo ma trận OPS-LOG / OPS-POS không có mục này).

## UI-6.33 – Sự cố nhà cung cấp, Bộ kiểm tra ATTP (D-114)
- [ ] Sự cố NCC: chip trạng thái có số đếm, mặc định chưa đóng, quá hạn lên đầu; lọc NCC / loại / mức độ / kỳ; 4 chỉ số.
- [ ] Lập sự cố: chỉ NCC, Loại (danh mục), Mức độ, Mô tả, chứng từ của NCC – không có ô trạng thái / cách xử lý / ngày.
- [ ] Nhận xử lý → Đã xử lý (bắt buộc hướng xử lý + ghi chú) → Đóng; Đã xử lý không sửa; không có nút xóa (xóa qua API bị chặn).
- [ ] Bộ kiểm tra: tab trong Kiểm tra theo ca / mẻ, sidebar không còn "Bộ kiểm tra ATTP", link cũ mở tab; bộ hệ thống có nhãn, không xóa / đổi mã / ngừng; Tạo phiên bản mới chép tiêu chí; tạo bộ mới mã tự sinh; bộ tự tạo hạn = hết ca.
- [ ] Quyền: uat.qa, uat.ops-scm (sự cố), uat.coo; không quyền không vào được.

## UI-6.34 – Chia suất & đóng gói + Điều phối giao hàng (D-115)
- [ ] Sidebar Đóng gói & Giao hàng: "Chia suất & đóng gói", "Điều phối giao hàng" (không còn Kết quả đóng gói, Đơn giao hàng, Xếp hàng lên xe); link cũ mở đúng màn.
- [ ] Chia suất: món chưa đủ mẻ → ô chờ, không bấm được; đủ mẻ → "Xong món này" ghi đủ mọi điểm; bấm ô ghi hỏng / thiếu (lý do bắt buộc); "Xong điểm giao"; điểm đủ mọi món → "Đủ thực đơn"; không có nút "Lập lệnh đóng gói".
- [ ] Mẻ nấu thêm chưa đạt → điểm còn thiếu phần quay lại "chờ".
- [ ] Điều phối: chuyến chưa đủ → "Xếp lên xe" mở hộp thiếu điểm / món; xếp có điều kiện cần lý do (quyền duyệt); đủ → xếp; "Xuất bến" nhập nhiệt độ; "Chuyển xe" chuyển điểm.
- [ ] Chốt đơn → tự lập chuyến cho ngày giao; thiếu xe → "Chưa gán chuyến" + tin bot. Bot "Chuyến đủ hàng" 1 tin / chuyến.
- [ ] Quyền: uat.ops-log đủ 2 màn; uat.ops-kit chỉ Chia suất; uat.coo đủ; sales / QA xem Điều phối.

## UI-6.35 – Theo dõi giao hàng + Khay hộp theo khách (D-116)
- [ ] Sidebar: "Theo dõi giao hàng", "Khay hộp theo khách" (không còn Vị trí xe, Xác nhận đã giao, Thu hồi hộp / khay); link cũ mở đúng tab.
- [ ] Điều phối: 4 thẻ, mỗi chuyến một dòng + dải điểm đúng màu; điểm quá giờ hẹn viền cam; bot báo sắp trễ (≤ 15 phút) / đã trễ; Sổ giao nhận in được phiếu; Bản đồ hiện vị trí theo sự kiện.
- [ ] Tài xế (tài khoản lái xe, hoặc "Mở chuyến" ở Việc của tôi): chỉ chuyến của mình; Đã đến (có / không định vị); Bàn giao điền sẵn người nhận + khay còn ở khách; thu vượt số còn ở khách → báo lỗi, không ghi gì.
- [ ] Bàn giao có thu khay → phiếu Chờ thu hồi cũ nhất chuyển Đã thu hồi, sổ bao bì có dòng IN tốt / hỏng; "Còn ở khách" giảm đúng.
- [ ] Giao thất bại (chọn lý do) → điểm ở cột "Cần giao lại" của Điều phối; chuyển sang chuyến khác giao lại được.
- [ ] Khay hộp theo khách: Quá hạn thu đúng (> 3 ngày); chỉ "Ghi thất thoát" (ghi tay OUT / IN bị chặn).
- [ ] Quyền: uat.ops-log, uat.coo đủ; uat.ops-kit không thấy; QA xem Theo dõi giao hàng.

## UI-6.36 – Nhân viên (D-117)
- [ ] Danh sách: 4 thẻ bấm lọc đúng; mặc định chỉ Đang làm việc; chip trạng thái có số; lọc đơn vị cha gồm đơn vị con; tìm theo SĐT; cột Tài khoản / Bot đúng; không còn cột Công ty, Ngày sinh.
- [ ] Chọn nhiều → Đề nghị cấp tài khoản (báo số bỏ qua), Tạo mã liên kết bot (mã + QR, in được), Xuất Excel.
- [ ] Thêm nhân viên: SĐT trùng người đang làm báo ngay + khóa Lưu; trùng người đã nghỉ thì cho; không có ô Mã / Trạng thái / Công ty; giới tính ô chọn; Lưu & mở hồ sơ; tích Cấp tài khoản → yêu cầu cho IT; tích Tạo mã bot → hiện mã / QR.
- [ ] Hồ sơ người mới: dải Việc cần làm (tài khoản, bot, phân ca, giấy khám sức khỏe) tự tích khi xong, ẩn khi đủ.
- [ ] Cho nghỉ việc hôm nay: hộp báo trước hệ quả; xác nhận → Đã nghỉ việc, tài khoản khóa, ca sau ngày nghỉ bị hủy, lịch sử có lý do. Ngày tương lai: vẫn đang làm, "Nghỉ từ …", đúng ngày tự khóa.
- [ ] Tạm nghỉ / Quay lại làm; form Sửa không đổi được trạng thái. Nhập Excel dòng trùng SĐT → báo lỗi dòng.
- [ ] Quyền: COO / NS đủ; OPS-KIT, OPS-LOG, QA, sales không vào được danh sách nhân viên.

## UI-6.37 – Phân ca tuần (D-118)
- [ ] Lưới luôn Thứ 2 – CN, mặc định tuần hiện tại; ngày đã qua mờ, không bấm được; Tuần sau / Tuần này.
- [ ] Tô ca: chọn ca rồi kéo qua ~20 ô → lưu một lần; ô phép / đã chấm công trả lại như cũ kèm lý do; Bỏ ca tô được.
- [ ] Sao chép tuần trước chỉ điền ô trống, liệt kê ô bỏ qua; bấm lại không tạo thêm.
- [ ] Phân ca hàng loạt: chọn nhanh cả tổ / chức vụ, ô tìm.
- [ ] Chốt & gửi lịch → nhân viên nhận lịch qua bot; sửa ô đã chốt → viền cam, nút "Gửi N thay đổi" → mỗi người 1 tin ca cũ → ca mới / đã hủy; bấm lại không gửi trùng.
- [ ] Cấu hình định biên (Hr:ShiftMinStaff) → dòng tổng tổ đỏ khi thiếu, thẻ Ca thiếu người đúng; cột Tuần cảnh báo ≥ 6 ca / không ngày nghỉ.
- [ ] Thứ 6 sau 15:00: tuần sau còn nháp → trưởng đơn vị + người phân ca nhận 1 tin.
- [ ] Điện thoại: xem theo ngày, mỗi ca một khối.

## UI-6.38 – Chấm công (D-119)
- [ ] Tab Hôm nay: chọn ca; 4 cột đúng; Chấm hộ có giờ thực tế (trống = bây giờ) + lý do; Báo vắng khi đã quá giờ; Cần xử lý: Bổ sung giờ (thiếu ra ca) và Xác nhận hợp lệ (ngoài bán kính, lý do bắt buộc).
- [ ] Bổ sung giờ: ngoài khung ca / ca quá 3 ngày / đã có lần ra → báo lỗi; hợp lệ → ca Hoàn thành, giờ công đúng; sổ có dòng "[BỔ SUNG] …".
- [ ] Bảng công tháng: ký hiệu đúng, dải Cần xử lý lọc được, bấm ô xem chi tiết; tổng khớp bảng công cũ; Xuất bảng công mở Excel đúng tiếng Việt.
- [ ] Sổ chấm công chỉ xem. Không còn nút Chốt vắng; 23:30 tự chuyển Vắng (chạy lại không đổi thêm).
- [ ] Bot: quá giờ còn người chưa vào ca → trưởng đơn vị nhận 1 tin / ca.
- [ ] Điện thoại: 4 cột xếp dọc, nút to.

## UI-6.39 – Nghỉ & đổi ca (D-120)
- [ ] Sidebar Nhân sự: Nhân viên · Phân ca · Chấm công · Nghỉ & đổi ca · Ca làm việc · Chức vụ (không còn Đơn đổi ca); link cũ #/hr-swap mở màn lọc Đổi ca; Phê duyệt › Nghỉ & đổi ca hiện cả 2 loại theo quyền duyệt từng loại.
- [ ] Tạo đơn Xin nghỉ: hiện phép năm còn / sau, ca bị bỏ, người nghỉ trùng. Đổi ca: 2 lần chọn thẻ + lý do. Nhường ca: chọn người đang trống.
- [ ] Đơn đổi / nhường ca mới "Chờ người nhận đồng ý"; người nhận Đồng ý (bot / Việc của tôi) → có yêu cầu duyệt; Từ chối → đơn Từ chối, người xin được báo; 24 giờ không trả lời → tự hủy.
- [ ] Hộp duyệt đơn nghỉ: phép năm, tổ còn x / cần y, người có thể thay; "Duyệt xong mở Phân ca".
- [ ] Lịch nghỉ 2 tuần; ngày tổ thiếu viền đỏ (cần cấu hình định biên).
- [ ] Thẻ Ca trống do nghỉ bấm mở Phân ca.

## UI-6.41 – Công ty & đơn vị (D-122)
- [ ] Sidebar Quản trị: "Công ty & đơn vị" (không còn Công ty, Sơ đồ tổ chức); link cũ #/adm-company mở chọn sẵn công ty.
- [ ] Cây gốc là công ty, nhãn "chưa có trưởng", số nhân viên gồm con; tìm mở nhánh; thẻ Chưa có trưởng lọc cây.
- [ ] Chọn công ty: thông tin + Sửa (người không có quyền mục Công ty không thấy nút); mã số thuế sai → báo lỗi; không xóa được công ty.
- [ ] Chọn đơn vị: loại tiếng Việt (không còn BRANCH / TEAM…); chọn ngay trưởng (chỉ người đang làm); ＋ Đơn vị con; Chuyển (không vào nhánh con; sang công ty khác cả nhánh đổi); Ngừng hoạt động khi còn người → báo số người.

## UI-6.40 – Danh mục hệ thống (D-121)
- [ ] Sidebar: nhóm "Danh mục hệ thống" ngay trên Quản trị hệ thống – Tổng quan danh mục + 10 mục đúng thứ tự; các nhóm cũ không còn các mục này.
- [ ] Quyền mỗi vai trò không đổi (tài khoản chỉ có quyền Xe giao hàng thấy nhóm với Tổng quan + Xe giao hàng).
- [ ] Tổng quan danh mục: số bản ghi khớp màn danh sách; cảnh báo đỏ bấm mở đúng màn; link cũ (kể cả #/adm-company) mở đúng.

## UI-6.42 – Tài khoản (D-123)
- [ ] Danh sách: cột Người dùng / Nhóm quyền / Truy cập gần nhất / Trạng thái; không còn nút biểu tượng không chữ, Sửa, Xóa; "Thao tác ▾" đủ mục theo quyền.
- [ ] Chọn nhiều: thêm / bỏ nhóm (tài khoản hết nhóm bị bỏ qua có báo), khóa (không khóa chính mình).
- [ ] Thẻ Nghỉ việc còn mở → Khóa ngay; thẻ 60 ngày lọc đúng.
- [ ] Tab Chờ IT xử lý: Duyệt & tạo (mật khẩu tạm hiện 1 lần nếu chưa liên kết bot), Đổi nhóm & duyệt, Từ chối – kết quả như Phê duyệt › Tài khoản.
- [ ] Tạo tài khoản cho nhân viên: tên = mã NV, nhóm gợi ý theo chức vụ; tài khoản hệ thống chỉ quản trị; người không phải quản trị tạo trực tiếp bị chặn.

## UI-6.43 – Nhóm quyền (D-124)
- [ ] Danh sách theo khối: tên tiếng Việt + mã, mô tả, số tài khoản / màn hình / loại duyệt; 4 chỉ số bấm được; tìm theo tên hoặc mã.
- [ ] Tạo nhóm: thiếu tên / mã trùng / khối sai → báo lỗi tiếng Việt; mã tự viết hoa.
- [ ] Sửa nhóm đang là người duyệt (vd. COO): đổi tên được, ô mã khóa.
- [ ] Phân quyền: đổi vài màn → thanh "N thay đổi chưa lưu" → Xem lại & lưu → người trong nhóm đăng nhập lại thấy đúng menu; Lịch sử có dòng Trước → Sau.
- [ ] Mục Phê duyệt: chọn Được duyệt → người trong nhóm duyệt được loại đó; Không → mất quyền duyệt.
- [ ] Xem trước menu tính cả thay đổi chưa lưu; So sánh 2 nhóm ra đúng các màn khác nhau.
- [ ] Tab Tài khoản: thêm nhiều tài khoản; bỏ tài khoản có 1 nhóm → không cho.
- [ ] Ngừng dùng nhóm còn tài khoản / đang duyệt → bị chặn có lý do.
- [ ] Link cũ #/adm-permission mở Nhóm quyền; menu không còn mục Phân quyền; luồng duyệt (đề nghị mua, nghỉ phép…) vẫn tới đúng người sau khi nhóm đổi tên tiếng Việt.

## UI-6 – Kịch bản đầu – cuối sau toàn đợt (UI-6.48 / UI-6.3)
Chạy trên DB thử với tài khoản `uat.<vai trò>` (mỗi bước đăng nhập đúng vai trò ghi trong ngoặc).
- [ ] **Đơn hàng** (SAL-SALES): Đơn hàng theo ngày → nhập suất ngày mai cho 1 khách có hợp đồng → lưu.
- [ ] **Chốt 20:00** (tự động / COO bấm chốt): đơn Đã chốt; kế hoạch sản xuất tự lập + phát hành; chuyến giao tự lập theo khu vực (chuông / bot báo).
- [ ] **Bảng sản xuất ngày** (OPS-KIT): mẻ của món × ca → Bắt đầu → Xong mẻ (nhiệt độ lõi, lưu mẫu) → tự Đạt; trên bot: ✅ Xong mẻ, Checklist ATTP (lỗi ❗ phải chọn mẻ).
- [ ] **Chia suất & đóng gói** (OPS-KIT): ghi kết quả theo món / điểm giao; điểm đủ thực đơn → chuyến "đủ hàng".
- [ ] **Điều phối giao hàng** (OPS-LOG): chuyến chưa đủ hàng sát giờ → chuông "Chuyến sắp xuất bến chưa đủ hàng"; xếp xe → xuất bến.
- [ ] **Theo dõi giao hàng** (tài xế – Việc của tôi › Mở chuyến): đến điểm → giao + thu khay → điểm thất bại vào "Cần giao lại".
- [ ] **Khay hộp** (OPS-LOG / QA): khay thu về vào sổ; Vệ sinh khay hộp → Đạt tất cả (web hoặc bot ✅ Đạt); chuông "Khay sạch thiếu cho ngày mai" nếu thiếu.
- [ ] **22:00** đóng lệnh SX → 22:30 KPI ngày → 01:00 giá vốn (Job tự động: xem lần chạy gần nhất).
- [ ] **Nhân sự** (FIN-HR / nhân viên): phân ca tuần → chốt; nhân viên xin đổi ca → người nhận Đồng ý (bot) → người duyệt nhóm Nghỉ & đổi ca duyệt; 23:30 tự chuyển Vắng; chuông "Ca thiếu người".
- [ ] **Tài khoản & nhóm quyền** (FIN-IT): nhân viên nghỉ việc → chuông "Tài khoản người đã nghỉ việc còn mở" → khóa; đổi quyền nhóm ở Nhóm quyền (Xem lại & lưu) → người trong nhóm thấy menu mới.
- [ ] **Quyền theo vai trò:** mỗi `uat.<vai trò>` thấy đúng menu như `docs/frontend-handoff/assets/ui-6/menu-roles/README.md`; link cũ (#/hr-swap, #/adm-permission, #/scm-lot, #/log-pod…) mở màn mới.

## ACC – Kết nối kế toán MISA AMIS (D-131, ACC-1 … ACC-8) – kịch bản tháng 08/2026

> Cần: tệp xuất danh mục AMIS + mẫu "Thêm bằng Excel" (Delicare-Share\MISA\), mã kho AMIS (`Integration:Misa:WarehouseMap`), số liệu tháng 08/2026 trên DB (hiện chưa có), sổ AMIS tháng 08/2026.

- [ ] **ACC-1 Khớp mã AMIS** (uat.fin-acc): nhập 5 tệp danh mục → thẻ chỉ số đúng; lọc Cần xử lý; ghép 1 nguyên liệu, 1 khách, 1 NCC theo mã AMIS; xuất mã cần tạo.
- [ ] **ACC-2 Chứng từ kế toán**: tháng 08/2026 – có Bán hàng (theo khách × loại suất), Nhập mua, Xuất NVL (theo buổi), Xuất bao bì, Nhập / xuất thành phẩm, Kho khác, Trả lại hàng mua, Bảng công; dòng Lỗi có nút sửa; tải Excel mẫu AMIS → nhập thử vào AMIS → Đánh dấu đã lập.
- [ ] **ACC-3 Bảng kê xác nhận**: khách đủ điều kiện → Ghi nhận xác nhận → phiếu bán tháng Sẵn sàng; Phản hồi lệch (bắt buộc ghi chú); Mở lại đối soát đã chốt → uat.coo duyệt ở Phê duyệt › Mở lại đối soát.
- [ ] **ACC-4 Khóa kỳ**: 5 điều kiện; khóa 08/2026 khi đủ; thử hủy phiếu nhận / đảo bút toán ngày trong tháng 8 → bị chặn; Xin mở khóa → uat.coo duyệt → mở; bot 08:00 nhắc từ ngày 5.
- [ ] **ACC-5 Trả hàng NCC** (uat.ops-wh, uat.ops-scm): lập phiếu từ lô còn tồn (lô Tạm giữ) → Mua hàng NCC đồng ý → Thủ kho ghi sổ → tồn giảm, chứng từ Trả lại hàng mua; thử NCC không nhận / Hủy (lô bỏ giữ); màn 390px.
- [ ] **ACC-6 Định mức bao bì** (uat.ops-plan): nhập định mức 5 loại suất → Gửi duyệt → duyệt; Bếp ghi kết quả đóng gói ca trưa → phiếu xuất bao bì Nháp đúng số; Thủ kho sửa 1 dòng; sau giờ hết ca job ghi sổ; thiếu tồn → bot Mua hàng.
- [ ] **ACC-8 Đối chiếu với AMIS**: tải Excel mẫu → điền số từ sổ AMIS 08/2026 → nhập; kiểm 8 chỉ tiêu, dòng lệch có nút đúng chỗ; xuất NVL giá trị lệch nhẹ = "Lệch do giá xuất".
- [ ] Quét UI-6.10 (chữ tiếng Việt, không mã kỹ thuật trên màn, không số tài khoản trên màn kho), ảnh 1440 / 390 ở `docs/frontend-handoff/assets/acc-misa/`.

## UI-6.50 – Nhận hàng theo nhà cung cấp (D-132)

- [ ] uat.ops-wh: Kho › Nhận hàng – thẻ NCC hẹn giao hôm nay, quá hẹn đỏ, đã nhận đủ làm mờ + Xem phiếu; ô tìm theo tên NCC / số đơn / số hóa đơn NCC.
- [ ] Form gộp 1 NCC 3 đơn: bỏ tick 1 đơn; "Nhận đủ – Đạt tất cả"; sửa 1 dòng thiếu; nhiệt độ thùng xe; lô / hạn chỉ ở nguyên liệu quản lý lô → lưu: 3 phiếu (mỗi đơn 1), tự đạt nhập kho, cần QA chờ QA.
- [ ] QA tại cửa kho (uat.qa): "Kết luận & nhập kho" – Đạt một phần trừ đơn mới nhất trước; sự cố NCC lập.
- [ ] Excel phiếu giao: tải mẫu → điền → tải lên tự điền form; dòng sai báo không khớp.
- [ ] In đơn mua PDF có QR → quét bằng camera iPhone / Android → bot mở nhận hàng NCC → Nhận đủ (bot hỏi nhiệt độ xe). Người không có quyền nhận hàng → chỉ xem đơn.
- [ ] Kế toán › Chứng từ kế toán: mỗi phiếu nhập từ form gộp = 1 chứng từ nhập mua gắn 1 đơn mua.
- [ ] Điện thoại 390: thẻ NCC → form dạng thẻ, nút to.

## AI-ĐỢT-1 – Trợ lý AI chỉ đọc + Kho tri thức (D-133, AI-1 … AI-8)

Chuẩn bị (IT): API + ToolGateway (`toolgateway`, chỉ mạng nội bộ) + Hermes (profile `hermes`) trên server; khóa `AGENT_KEY_VIVI / WH / KITCHEN / QA / KNOW`, `ANTHROPIC_API_KEY`, token bot Trợ lý ở `.env`; `Agent:AskingGroups` = nhóm đợt 1; DB: `DB/seed/Agent_seed.sql`, `D79_menu_regroup.sql`, `AI6_agent_knowledge_rights.sql` (DryRun trước).

- [ ] `node tools/agent-eval/run.mjs` (5 agent) trên server: **tất cả ĐÚNG** (số khớp màn web cùng quyền).
- [ ] uat.ceo: Trợ lý AI › Trung tâm agent – 5 / 13 agent đang bật, thẻ không báo "máy chủ chưa có khóa"; Chi tiết WH: công cụ, thư mục 40 / 45 / 80, lịch 05:30.
- [ ] Tắt WH trên màn → hỏi bot "tồn thịt heo còn bao nhiêu" → bot không dùng được WH (nhật ký: Bị từ chối "Agent WH đang tắt"); bật lại → trả lời đúng số màn Tồn kho.
- [ ] Bot Trợ lý: CEO đã liên kết hỏi "hôm nay vận hành thế nào" → số khớp Bảng điều hành; người **chưa liên kết** nhắn bot → không có số liệu (nhật ký: "chưa liên kết DELI-CARE"); thủ kho (không thuộc nhóm đợt 1) → "chỉ dành cho Ban Giám đốc và trưởng bộ phận".
- [ ] Hỏi thủ kho (đã thêm vào nhóm) "lương của Nguyễn Văn A" / số liệu màn không được cấp → agent nói không có quyền; không lộ số điện thoại / email trong câu trả lời.
- [ ] 07:00 và 17:00 (2 tuần liền): bot Trợ lý gửi bản tin cho Ban GĐ, số khớp Bảng điều hành; kho tri thức › 10 Ban GĐ › Bản tin có ghi chú cùng ngày (Sáng / Chiều); 96 Nhật ký › VIVI có dòng "đã gửi bản tin".
- [ ] Kho tri thức: uat.qa thấy 30 / 40 / 45 / 50 / 55 / 80 / 99, **không** thấy 60 / 70 / BOD; uat.ceo thấy BOD; admin **không** thấy BOD.
- [ ] uat.qa soạn SOP trong 45 ở trạng thái *chờ duyệt* (lưu được), thử *hiệu lực* (bị chặn), thêm số điện thoại (bị chặn); hai trình duyệt cùng sửa 1 ghi chú → người lưu sau nhận báo xung đột.
- [ ] Tìm "thit heo" ra ghi chú "thịt heo"; `[[liên kết]]` và mã chứng từ bấm được; ngưỡng `{{BusinessRules:…}}` hiện giá trị cấu hình.
- [ ] Trợ lý AI › Nhật ký agent: lọc WH / Bị từ chối thấy các lần thử ở trên; Lịch chạy: lần chạy gần nhất có giờ sau 1 ngày chạy Hermes.
- [ ] Chi phí AI trong ngân sách (anh xác nhận theo hóa đơn Anthropic) – **cửa qua đợt 1**.
