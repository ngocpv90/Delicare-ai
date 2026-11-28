---
screens: []
roles: []
updated: 2026-11-28
---
# 91 – Phụ lục: trạng thái, số chứng từ, ngưỡng, thuật ngữ

## 1. Trạng thái chứng từ (theo máy chủ)

| Chứng từ | Trạng thái (theo thứ tự luồng) | Màn |
|---|---|---|
| Đơn hàng | Nháp → Đã gửi → Đã xác nhận → Đã chốt → Đang sản xuất → Đã giao → Đã đối soát; Hủy | `crm-order` |
| Hợp đồng | Nháp → Chờ duyệt → Hiệu lực → Hết hạn / Chấm dứt | `crm-contract` |
| Dự báo | Nháp → Đã công bố; Bị thay thế | `crm-forecast` |
| Thực đơn | Nháp → Đã duyệt → Đã công bố → Hết hiệu lực | `menu-menu` |
| Yêu cầu đổi thực đơn | Chờ duyệt → Đã duyệt / Từ chối; Đã hủy | `menu-change-request` |
| Đối soát | Mở → Khớp / Chênh lệch → Đóng | `crm-reconciliation` |
| Phản ánh khách hàng | Mới → Đang xử lý → Đã xử lý → Đóng; Từ chối | `log-complaint` |
| Đề nghị mua (PR) | Nháp → Chờ duyệt → Đã duyệt → Đã lập PO; Từ chối; Hủy | `scm-pr` |
| Đơn mua (PO) | Nháp → Chờ duyệt → Đã duyệt → Đã gửi NCC → Nhận một phần → Đã nhận đủ → Đóng; Hủy | `scm-po` |
| Phiếu nhập | Chờ QC → Đạt / Đạt một phần / Không đạt → Đã ghi sổ; Hủy | `scm-gr` |
| Nhu cầu nguyên liệu | Đã tính → Thiếu / Đủ → Đã đề nghị mua | `scm-requirement` |
| Giữ hàng | Đã giữ → Đã xuất một phần → Đã xuất; Đã giải phóng | `scm-reservation` |
| Lô nguyên liệu | Khả dụng · Tạm giữ · Hết hạn · Đã dùng hết | `scm-lot` |
| Phiếu xuất | Nháp → Đã xuất; Hủy | `scm-issue` |
| Kiểm kê | Nháp → Đang kiểm → Chờ duyệt → Đã ghi sổ | `scm-stockcount` |
| Sự cố NCC | Mới → Đang xử lý → Đã xử lý → Đóng | `qc-supplier-issue` |
| Kế hoạch SX | Nháp → Đã duyệt → Đã phát hành → Hoàn thành | `prod-plan` |
| Lệnh SX | Kế hoạch → Phát hành → Đang SX → Hoàn thành → Đóng; Hủy | `prod-order` |
| Mẻ nấu | Kế hoạch → Đang nấu → Chờ QC → Đạt / Không đạt → Đã đóng gói; Hủy · QC: Đạt / Tạm giữ / Từ chối | `prod-batch` |
| Lệnh đóng gói | Kế hoạch → Đang đóng gói → Đã đóng gói; Hủy | `log-packing-order` |
| Chuyến xe | Kế hoạch → Đang xếp → Đang giao → Hoàn thành | `log-route` |
| Điểm dừng | Kế hoạch → Đã đến → Đã giao / Thất bại | `log-route` |
| Đơn giao | Kế hoạch → Đang giao → Đã giao / Thất bại; Hủy | `log-delivery-order` |
| Thực hiện giao | Chờ xếp hàng → Đã xếp lên xe → Đang giao → Đã đến → Đã giao / Thất bại; Hủy | `log-execution` |
| Xếp hàng lên xe | Đã xếp lên xe; Hủy | `log-route` |
| Thu hồi khay | Chờ thu hồi → Đã thu hồi → Đã kiểm tra · Kết quả: Đủ / Thiếu / Hỏng / Bẩn / Không đạt | `log-packaging-ledger` (tab Phiếu thu hồi) |
| Sổ bao bì | Giao cho khách (OUT) · Khách trả lại (IN) · Thất thoát (LOSS) · Sẵn sàng dùng lại | `log-packaging-ledger` |
| Mẻ vệ sinh | Đang vệ sinh → Hoàn tất (Đạt – sẵn sàng dùng lại / Không đạt) | `qc-cleaning` |
| Ca được phân | Kế hoạch → Xác nhận → Đã vào ca → Hoàn thành; Vắng | `hr-shift-assign` |
| Đơn nghỉ / đổi ca | Chờ duyệt → Đã duyệt / Từ chối; Đã hủy | `hr-leave`, `hr-swap` |
| Yêu cầu duyệt | Chờ duyệt → Đang duyệt → Duyệt / Từ chối; Hủy | `wf-inbox` |

Nhãn trạng thái trên mọi màn (ở chế độ dữ liệu thật) lấy từ máy chủ (`MetaData/GetStatuses`) nên luôn khớp bảng này (C-10).

## 2. Mã danh mục và số chứng từ (D-63)

Mã và số chứng từ do **máy chủ tự sinh khi lưu** – trên form ô Mã hiện *(tự sinh khi lưu)*, không gõ được, không đổi sau khi cấp. Lưu xong, thông báo hiện mã mới (vd. *Đã tạo khách hàng – mã KHB-00012*).

**Danh mục** – dạng `TIỀNTỐ-NNNNN` (5 chữ số, tăng liên tục):

| Tiền tố | Danh mục |
|---|---|
| `KHB-` / `KHC-` | Khách hàng doanh nghiệp / cá nhân |
| `NC-` | Nhà cung cấp |
| `NL-` | Nguyên liệu |
| `MA-` | Món ăn |
| `KO-` | Kho |
| `XE-` | Phương tiện |
| `NV-` | Nhân viên (người mới; mã = tên đăng nhập) |
| `DP-` | Điểm giao / điểm phát |
| `BG-` | Bảng giá |
| `NM-` | Nhóm món ăn (từ 22/11/2026; nhóm tạo trước đó giữ mã cũ) |
| `NN-` | Nhóm nguyên liệu (từ 22/11/2026; nhóm tạo trước đó giữ mã cũ) |

**Chứng từ** – dạng `TIỀNTỐ-YYMMDD-NNNN` (YYMMDD = ngày lập, số đếm lại mỗi ngày):

| Tiền tố | Chứng từ |
|---|---|
| `DH-` | Đơn hàng |
| `HD-` | Hợp đồng (số hợp đồng giấy của khách ghi ở Ghi chú) |
| `NU-` | Thực đơn (ngày = ngày bắt đầu áp dụng) |
| `DM-` | Đề xuất đổi món |
| `MH-` | Đề nghị mua |
| `PO-` | Đơn mua |
| `PN-` / `PX-` | Phiếu nhập / phiếu xuất |
| `KK-` | Kiểm kê |
| `GD-` | Giao dịch kho tay (điều chỉnh, chuyển kho, hủy lô hết hạn) |
| `QC-` | Phiếu kiểm tra QC đầu vào |
| `LO-YYMMDD-<mã NCC>-NN` | Lô nguyên liệu (lô nhập tay không có NCC: hậu tố `TDK`) |
| `MR-` | Lần tính nhu cầu nguyên liệu |
| `KS-YYMMDD` | Kế hoạch sản xuất (lần sau: `-2`, `-3`…) |
| `LS-` | Lệnh sản xuất |
| `ME-YYMMDD-<ca>-NN` | Mẻ nấu |
| `DG-` | Lệnh đóng gói |
| `GH-` | Đơn giao hàng |
| `CX-YYMMDD-<mã xe>-NN` | Chuyến xe |
| `VS-YYMMDD-<ca>` | Mẻ vệ sinh |
| `PA-` | Phản ánh khách hàng |
| `XN-` | Yêu cầu duyệt |
| `DN-` / `DC-` | Đơn nghỉ / đơn đổi ca |

Chứng từ lập **trước 19/11/2026** giữ số cũ (`PH-…`, `KH-…`, `NL-…-<NCC>-NN`) – vẫn tìm, in, mở trên bot bình thường.

**Mã do người đặt** (ca `CA1`, đơn vị tính `KG`, khu / vị trí kho, dây chuyền…): tự chuyển chữ hoa không dấu, chỉ gồm A–Z, 0–9, `-`, `_`; mã đã được dùng (đã có phân ca, mẻ, tồn kho…) thì không sửa được.

Gõ mã / số trên bot hoặc ô tìm kiếm toàn cục để mở nhanh.

## 3. Ngưỡng và giờ mặc định

| Nội dung | Giá trị |
|---|---|
| Giờ chốt đơn | 20:00 ngày D-1 (job tự chốt) |
| Tính nhu cầu nguyên liệu | 20:30 |
| Đơn lệch cần duyệt | > 30% |
| Dự báo: cảnh báo lệch / tối đa | 10% / 62 ngày |
| Kiểm kê cần duyệt 2 cấp | chênh > 2% |
| PO cần thêm cấp giám đốc | ≥ 50.000.000 đ |
| Nhận vượt PO | ≤ 10% |
| Nhận hàng mát | ≤ 5 °C nhận · ≤ 8 °C có điều kiện |
| Nhận hàng đông lạnh | ≤ −18 °C nhận · ≤ −15 °C có điều kiện (xác nhận không rã đông) |
| Nhiệt độ lõi | ≥ 70 °C |
| Nhiệt độ xuất / giao | ≥ 60 °C |
| Hạn xử lý phản ánh | Cao 4 giờ · Trung bình 24 giờ · Thấp 72 giờ |
| Đối soát phải ghi chú | chênh > 1% |
| Chênh lệch tiêu hao cảnh báo | > 5% |
| Thất thoát khay hộp cảnh báo | > 3% |
| Hạn mỗi cấp duyệt | 48 giờ |
| Chấm công | bán kính 150 m · vào ca sớm tối đa 120′ |
| Phân ca hàng loạt / bảng công | ≤ 31 ngày / ≤ 62 ngày |
| Đơn nghỉ | ≤ 30 ngày / đơn · lập lùi ≤ 7 ngày |
| Loại khay mặc định đóng gói | KHAY |

## 4. Lịch job tự động

| Giờ | Việc |
|---|---|
| 00:15 | Quét lô hết hạn |
| 00:30 | Quét hợp đồng hết hạn |
| 07:00 | Thẻ ngày, bản tin sáng (bot) |
| 08:00 | Chốt vắng ca hôm qua |
| 16:00 | Nhắc bàn giao người nghỉ ngày mai |
| 17:00 | Báo cáo cuối ngày |
| 19:00 | Nhắc khách chưa chốt đơn |
| 20:00 | Chốt đơn ngày mai |
| 20:30 | MRP |
| 21:00 | Nguyên liệu cho tối nay / mai |
| (tắt) | Chỉ số theo ngày / tháng – quản trị bấm **Làm mới số liệu** |

## 5. Thuật ngữ

| Từ | Nghĩa |
|---|---|
| Suất / phần món | Một suất ăn giao khách / một phần của một món trong mẻ |
| Mẻ | Một lần nấu một món trên một dây chuyền, theo lệnh SX |
| MRP | Tính nhu cầu nguyên liệu từ kế hoạch SX trừ tồn |
| POD | Biên bản giao nhận (Proof of Delivery) – số giao, số nhận, người nhận, nhiệt độ |
| Lưu mẫu | Giữ mẫu thức ăn của mẻ theo quy định ATTP (bắt buộc trước khi xe chạy) |
| Tiêu chí trọng yếu | Tiêu chí kiểm tra ATTP mà FAIL thì chặn đạt QC / đóng gói / xuất chuyến |
| Sổ chỉ ghi thêm | Sổ không sửa / xóa; sai thì ghi dòng ngược lại |
| Chấm hộ | Người có quyền ghi vào / ra ca thay nhân viên, bắt buộc lý do |
| Leo thang | Sự cố chưa ai nhận sau N phút được gửi lên cấp cao hơn |
| Giờ yên lặng | Khung giờ bot hoãn tin không khẩn |
