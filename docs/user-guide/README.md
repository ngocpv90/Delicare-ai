# Hướng dẫn sử dụng DELI-CARE

Bộ hướng dẫn cho người dùng cuối, viết theo **code hiện tại** (rà soát 19/11/2026): web quản trị `/#/quan-tri/<screen-id>`, bot Telegram **Vận hành** và **Điều hành**. Chỗ code chưa có hoặc khác tài liệu ghi riêng ở `90_CHENH_LECH.md` (mã C-xx được nhắc trong từng phần).

## 1. Đọc gì trước

1. Mọi người: **`00_CHUNG.md`** (đăng nhập, bố cục, danh sách, ngăn chi tiết, tìm kiếm, in, lỗi chung) và **`20_BOT_TELEGRAM.md`**.
2. Sau đó: file của vai trò mình (bảng 2).

## 2. Vai trò → file

| File | Vai trò | Nhóm quyền |
|---|---|---|
| `01_QUAN_TRI.md` | Quản trị hệ thống (IT) | FIN-IT |
| `02_KINH_DOANH.md` | Kinh doanh, hợp đồng, đơn hàng | SAL-SALES, SAL-CON |
| `03_KE_HOACH.md` | Kế hoạch: món, công thức, thực đơn, kế hoạch SX, giá thành | OPS-PLAN |
| `04_MUA_HANG.md` | Mua hàng: MRP, đề nghị mua, đơn mua, NCC | OPS-SCM |
| `05_KHO.md` | Kho: nhận hàng, tồn, lô, xuất, kiểm kê | OPS-SCM |
| `06_QC_ATTP.md` | QC / ATTP: QC đầu vào, checklist, nhiệt độ, QC mẻ, vệ sinh khay | QA, OPS-KIT (QA bếp) |
| `07_BEP_SAN_XUAT.md` | Bếp / sản xuất: mẻ nấu, lệnh SX, hao hụt | OPS-KIT |
| `08_DONG_GOI.md` | Chia suất / đóng gói | OPS-KIT (tổ đóng gói), OPS-LOG |
| `09_GIAO_VAN.md` | Giao vận: chuyến, xuất bến, POD, thu hồi khay | OPS-LOG |
| `10_DIEM_PHAT.md` | Điểm phát, CSKH: đối soát suất, phản ánh | OPS-POS, SAL-CS |
| `11_NHAN_SU.md` | Nhân sự: phân ca, chấm công, nghỉ, đổi ca | FIN-HR (+ mọi nhân viên) |
| `12_QUAN_LY_DUYET.md` | Người duyệt: hộp phê duyệt | CEO, COO, FIN-HR, OPS-SCM, FIN-ACC |
| `13_BAN_DIEU_HANH.md` | Ban điều hành, kế toán: dashboard, KPI | CEO, CEO-PMO, COO, FIN-ACC |
| `20_BOT_TELEGRAM.md` | Bot Vận hành / Điều hành | Mọi người |
| `90_CHENH_LECH.md` | Chênh lệch tài liệu – code (đầu việc UG-1) | Đội phát triển |
| `91_PHU_LUC.md` | Trạng thái, số chứng từ, ngưỡng, lịch job, thuật ngữ | Mọi người |

## 3. Cấu trúc mỗi file vai trò

1. **Bạn là ai** – nhóm quyền, bot dùng, việc chính.
2. **Màn hình được dùng** – screen-id, đường menu, dùng để làm gì.
3. **Việc trong ngày** – theo thứ tự luồng nghiệp vụ.
4. **Thao tác từng bước** – từng màn, từng nút, từng ô nhập.
5. **Trạng thái** chứng từ.
6. **Lỗi / cảnh báo thường gặp** – nguyên văn thông báo (… là phần thay đổi) và cách xử lý.
7. **Trên bot Telegram**.

## 4. Ma trận vai trò × màn hình chính (quyền theo seed)

S = sửa / thao tác · X = chỉ xem · D = có mục duyệt · – = không vào được

| Màn | KD | KH | MH/Kho | QA | Bếp | GV | ĐP | CSKH | NS | IT | CEO | COO | KT |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `crm-*` (khách, hợp đồng, đơn, dự báo, đối soát) | S | X | X | X | X | X | S | X | – | X | X·D | X | X |
| `menu-*` (món, công thức, thực đơn, đổi món) | X | S | X | X | X | – | X | X | – | X | X·D | X·D | X |
| `prod-*` (kế hoạch, lệnh, mẻ, hao hụt) | X | S | X | X | S | X | – | – | – | X | X·D | X·D | X |
| `cost-*` (giá thành) | X | S | X | X | S | X | – | – | – | X | X | X | X |
| `scm-*` (MRP, PR, PO, kho, lô, kiểm kê) | – | X | S·D | S | X | – | – | – | – | X | X·D | X·D | X·D |
| `qc-*` | – | X | X | S·D | S | X | X | X | – | X | X | X | – |
| `log-*` (đóng gói, chuyến, giao, khay, phản ánh) | X | X | – | X | X | S | S | S | – | X | X | X | X |
| `hr-*` | – | – | – | – | X | – | – | – | S·D | X | X·D | X·D | X |
| `wf-inbox` | – | – | – | – | X | – | – | – | S·D | X | X·D | X·D | X |
| `adm-*` | – | – | – | – | – | – | – | – | X | S | X | – | – |
| `dash-exec`, `kpi-*` | X | X | X | X | X | X | X | X | X | X | X | X | X |

KD = SAL-SALES/SAL-CON · KH = OPS-PLAN · MH/Kho = OPS-SCM · QA = QA · Bếp = OPS-KIT · GV = OPS-LOG · ĐP = OPS-POS · CSKH = SAL-CS · NS = FIN-HR · IT = FIN-IT · KT = FIN-ACC. Chi tiết từng mục duyệt: `12_QUAN_LY_DUYET.md` §3. Các ô "–" ở `hr-*` / `wf-inbox` là chênh lệch C-18. Bảng tính theo `UserRole_seed.sql`: một nhóm có thể chỉ được một phần màn trong cụm; quyền thật xem ở `adm-permission` → **Quyền hiệu lực**.

## 5. Quy ước

- Tên nút / ô / thông báo viết **đậm** hoặc *nghiêng* đúng như trên màn hình; `…` là phần thay đổi theo dữ liệu.
- Dấu `*` sau tên ô = bắt buộc.
- "D-1" = ngày trước ngày ăn.
- Khi C-xx được sửa, cập nhật đoạn liên quan và đánh dấu ✅ trong `90_CHENH_LECH.md`.
