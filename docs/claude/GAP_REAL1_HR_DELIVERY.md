# GAP – Dữ liệu nhân sự & đội giao vận chưa nạp vào DB (REAL-1 bổ sung, 2026-11-26)

Nguồn: `DB/data/real1/HR/DSNS_20261126.xlsx`, `DB/data/real1/Delivery/Đội Giao vận.xlsx` (bản chép từ `Delicare-Share`). Script: `tools/real1/09-hr-delivery.mjs`.
**Đã nạp:** 53 chức vụ, 139 nhân viên (đơn vị, chức vụ), SĐT 18 lái xe / phụ xe, địa chỉ 16 người, 9 xe (biển số, loại, tải trọng, ghi chú, lái xe mặc định 4 xe).
Dưới đây là phần **chưa nạp** hoặc **em tự quyết tạm** – để anh trao đổi giải pháp. Chưa sửa code / DB cho các mục này.

## 1. Hình thức làm việc – chưa có chỗ lưu

| Hình thức | Số người |
|---|---|
| Fulltime | 89 |
| Partime | 43 |
| Khoán | 6 |
| Remote/online | 1 |

- `Employee` không có cột hình thức làm việc.
- Ảnh hưởng: chưa lọc / báo cáo theo hình thức; phân ca, tính công (part-time theo giờ, khoán theo việc) chưa phân biệt.
- Phương án gợi ý: (a) thêm cột `Employee.EmploymentType` + danh mục `ReferenceLists:EmploymentType` (cần anh duyệt thêm cột); (b) tạm lưu vào chức vụ / ghi chú (không lọc được chuẩn); (c) dùng nhóm nhân viên riêng.

## 2. Bảng tuyến giao theo buổi – chưa có mô hình tuyến cố định

Tệp có bảng tuyến theo buổi (và khoảng cách):

| Buổi | Tuyến (khoảng cách) |
|---|---|
| Sáng | Cổ Loa (A5) + KTX + Vinalpha (>20 km) · Gia Lâm (<20 km) · Công trường 1,2,3 + Km 13,14,15 (>20 km) · Km 17 + Trí Quả (<20 km) |
| Trưa | KTX + Vinalpha (>20 km) · Cổ Loa (A5) (>20 km) · Km 13,14,15 – SGC (<20 km) · Gia Lâm 1 (inox + 1 lần) (<20 km) · Công trường 1,2,3 SGC (>20 km) · Trí Quả + Km 17 + Km 23 (<20 km) |
| Chiều | KTX + Vinalpha (>20 km) · Cổ Loa (A5) (>20 km) · Km 13,14,15 – SGC (<20 km) · Gia Lâm 1 (inox + 1 lần) (<20 km) · Công trường 1,2,3 SGC (>20 km) · Trí Quả + Km 17 + Km 23 (<20 km) |
| Tối | Công trường + Km 13,14 (<20 km) · Km 17 (<20 km) |

- Hiện chuyến giao (`DeliveryRoute`) lập **theo từng ngày** (tự lập sau chốt đơn, gán xe / tài xế theo chuyến gần nhất – UI-6.49 (10)); chưa có "tuyến mẫu" cố định theo buổi, chưa có trường khoảng cách.
- Cần thống nhất: tên tuyến ↔ điểm giao của khách nào (KTX, Vinalpha, Cổ Loa A5, Gia Lâm, Công trường 1–3, Km 13/14/15/17/23, Trí Quả, SGC); buổi "Chiều" có phải ca riêng không (hệ thống đang có Sáng / Trưa / Tối / Đêm).
- Phương án gợi ý: (a) bảng / danh mục tuyến mẫu (tuyến × buổi × điểm giao × xe / lái xe mặc định) – cần bảng mới; (b) tạm dùng ghi chú xe + nhóm điểm giao theo tên tuyến (không bảng mới) để tự lập chuyến đúng tuyến.

## 3. Phân công lái xe – mới ghi chú, chưa thành dữ liệu

- **Lái xe mặc định của xe:** đã gán 4 xe (30M-48196 – Chu Văn Mãi; 30B-08940 – Dương Văn Mã; 30B-09031 – Chu Văn Lợi; 30B-31381 – Nguyễn Văn Đức). Còn trống vì tệp ghi nhiều người / theo buổi:
  - 30B-05637: "Hiển – chiều, Mãi sáng"
  - 30B-98742: "Minh sáng trưa – chiều, Đức ca đêm"
  - 30M-47638 (dự phòng), 30B-48197 (Gia Lâm – chưa ghi lái xe), 30M-67145 (xe riêng)
  - Xe chỉ có **1** lái xe mặc định; phân lái xe **theo buổi** cần chỗ lưu mới (gắn với mục 2).
- **Chuyên môn / tuyến quen của lái xe** (chưa lưu – chỉ có trong tệp): Tùng – lái xe sếp + ca trưa, test lái xe mới; Tuyến – leader Gia Lâm; Hiển – leader Cổ Loa; Mãi – Trí Quả + Km 17, 3 chuyến / ngày; Minh – Công trường + Km 13,14 + SCG; Trường – ca tối, đêm; Quang, Mã – trưa, tối; Quân – đêm.
- **Tên trên xe không có trong danh sách lái xe của tệp Giao vận:** "Lợi" (đã khớp Chu Văn Lợi trong danh sách nhân sự).

## 4. Xe – thông tin chưa có cột

- **Hạn bảo hiểm TNDS** (8 xe, 21/03/2027 → 23/10/2027): mới nằm trong ghi chú xe → chưa nhắc hạn tự động. Phương án: cột hạn bảo hiểm / đăng kiểm hoặc tài liệu đính kèm có hạn.
- **Hãng xe** (SRM): trong ghi chú.
- **Xe riêng 30M-67145** ("Xe riêng ms Hiện"): chưa có loại / tải trọng – có cần đưa vào điều phối giao hàng không?
- Loại xe đang giữ nguyên chữ trong tệp ("xe tải thùng kín – xe mát") – chưa chuẩn hóa thành danh mục loại xe.

## 5. Em tự quyết tạm – anh xác nhận

| Mục | Đang làm | Cần anh |
|---|---|---|
| Nhân viên "Pháp chế" (Luật sư / Pháp chế) | Đơn vị FIN – Khối Tài chính – Hỗ trợ (chưa có đơn vị Pháp chế) | Đơn vị đúng? |
| Nguyễn Đức Thắng (59894) | Không có chức vụ / khối / tổ → để cấp công ty (DLC), không chức vụ | Bộ phận, chức vụ? |
| 2 người "Nguyễn Đức Minh" không mã NV (Phụ kho; Học việc bếp) | Coi là 2 người, mã tạm `NV-SCM-NGUYENDUCMINH`, `NV-COOK-NGUYENDUCMINH` | 1 hay 2 người? mã NV thật? |
| Nguyễn Thị Mão (không mã NV) | Mã tạm `NV-PREP-NGUYENTHIMAO` | Mã NV thật? |
| Nấu cơm / Chia canh (thuộc Bếp / Tổ chia suất trong tệp) | Đơn vị OPS-KIT-RICE (Tổ Cơm / Canh) | Đúng tổ? |
| Tạp vụ tại Vin Cổ Loa / Vin Gia Lâm | Đơn vị OPS-KIT-CLEAN (tổ tạp vụ bếp) | Có thuộc điểm phát (OPS-POS) không? |
| 53 chức vụ giữ nguyên tên trong tệp (vd. "Nhân viên chia suất ca sáng / ca ngày / ca đêm", "Lái xe – Tăng cường") | Mỗi tên 1 chức vụ | Gộp chức vụ + ca riêng? |
| Mã nhân viên dạng số (vd. 79357, 6, 58…) | Giữ nguyên làm mã NV | Có quy tắc mã chuẩn không? |
| SĐT | Chỉ có 18 người giao vận (thêm số 0 đầu) | Bổ sung SĐT 121 người còn lại (cần để liên kết bot / chấm công) |

## 6. Chưa làm theo phạm vi

- **Tài khoản đăng nhập / liên kết bot:** chưa tạo (cấp ở Nhân sự › Nhân viên › Đề nghị cấp tài khoản – IT duyệt); `BusinessRules:PositionDefaultGroups` (chức vụ → nhóm quyền gợi ý) còn trống – cần anh / IT điền theo 53 chức vụ mới.
- **Ngày vào làm, ngày sinh, giới tính, email:** tệp không có.
