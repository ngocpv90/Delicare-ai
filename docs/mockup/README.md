# DELICARE Enterprise Mockup – Database-Aligned Interactive Prototype

> **Toàn bộ số liệu trong mockup là DỮ LIỆU DEMO, không phải dữ liệu thật.**
> Dữ liệu được lưu trong bộ nhớ trình duyệt và sẽ mất khi tải lại trang. Muốn đưa về trạng thái ban đầu: vào **Hướng dẫn trình diễn → Khôi phục dữ liệu demo**.

## 1. Cách chạy

1. Giải nén thư mục `DELICARE_Enterprise_Mockup`.
2. Mở file `index.html` bằng Chrome hoặc Edge (nhấp đúp là được).
3. Không cần backend, SQL Server, Internet hay CDN. Mockup dùng HTML5, CSS3 và JavaScript thuần; biểu đồ vẽ bằng SVG.

## 2. Cấu trúc thư mục

```
DELICARE_Enterprise_Mockup/
├── index.html                 Khung ứng dụng (sidebar, header, router)
├── css/app.css                Theme kế thừa deli_care_admin_demo.html (#91AF42 / #F07825), responsive
├── js/data/                   SINH TỰ ĐỘNG – không sửa tay
│   ├── schema.js              124 bảng · 2.264 cột · PK/NULL/DEFAULT · 224 quan hệ logic · 80 tham chiếu chưa rõ · 141 cột deprecated
│   ├── catalog.js             119 chức năng P0–P8, 47 bộ trạng thái, Gap Register, 9 luồng nghiệp vụ
│   └── demo-data.js           7.888 dòng DEMO cho 117 bảng (chỉ dùng tên bảng/cột có trong SQL)
├── js/core/                   util · store (dữ liệu + AuditChange + phân quyền) · ui (bảng, form, modal, drawer, truy vết, biểu đồ) · app (router, màn hình chung)
├── js/modules/                Màn hình chuyên biệt theo module (dashboard, sales, menu, scm, production, qc, logistics, hr, admin, integration, explorer)
└── docs/                      DELICARE_Module_Function_Mapping.xlsx · DELICARE_Database_Gap_Register.xlsx · DELICARE_Business_Workflow.md
```

## 3. Nguồn dữ liệu và nguyên tắc

| Nguồn | Dùng cho |
|---|---|
| `20261113_structure_db_appdelicare.sql` | Tên bảng/cột, kiểu, NULL, DEFAULT, PK. Đây là nguồn chuẩn duy nhất |
| `20261113_Dictionary_DB_AppDelicare.xlsx` | Nhóm phân loại, 183 quan hệ đề xuất, 121 tham chiếu chưa rõ, mô tả cột |
| `DeliCare_Database_Dictionary.xlsx` (BRD) | 75 Business Object, mô tả bảng, danh sách trạng thái QC / Delivery / Inventory |
| Quyết định PO (14/11/2026) | UnitId → UnitMeasure; ObjectMenu là menu hệ thống (P0); Vai trò = UserGroup; OrganizationUnit là nguồn Chi nhánh/Phòng ban; bỏ qua nhóm bệnh viện; dùng cột mới trong các cặp trùng nghĩa |

Các nguyên tắc đã tuân thủ:
- Mọi bảng thuộc schema `dbo`.
- Không có FK vật lý, không tạo bảng hay cột mới, không có bảng `RecipeVersion`.
- Giữ nguyên tên legacy: `Dishe`, `ProductionBatche`, `ProofOfDeliverie`…
- Cột `DeleteddBy` vẫn giữ nguyên như trong SQL.
- Chức năng chưa có bảng hỗ trợ được gắn nhãn **GAP**. Trạng thái tự đề xuất được gắn nhãn **PROPOSED – BA/PO CONFIRM**.

## 4. Phạm vi đã xây dựng

- **119 chức năng P0–P8 + 1 màn hình Luồng E2E**, chia thành 11 nhóm menu.
- **40 màn hình chuyên biệt**, mỗi màn hình có giao diện riêng theo nghiệp vụ:
  - Dashboard điều hành (lọc ngày / ca / đơn vị).
  - 9 luồng E2E.
  - Đơn hàng: Kanban, tạo đơn theo hợp đồng và dự báo, Gửi → Xác nhận → Chốt.
  - Khách hàng 360°, Đối soát, Dự báo dạng ma trận.
  - Công thức nhiều phiên bản (tạo / gửi duyệt / khóa trên bảng `Recipe`).
  - Thực đơn dạng lịch tuần.
  - MRP: tính nhu cầu và thiếu hụt, sinh đề nghị mua.
  - Đề nghị mua, Đơn mua, Nhập kho có cổng QC, QC đầu vào PASS/HOLD/REJECT.
  - Tồn kho (không cho sửa trực tiếp), Lô & truy xuất nguồn gốc, Xuất kho theo FEFO, Kiểm kê.
  - Kế hoạch SX → phát hành lệnh, Bảng điều độ mẻ (ca × dây chuyền), Giá thành.
  - Checklist ATTP theo ca, Nhật ký nhiệt độ.
  - Tuyến giao, Thực hiện giao (READY → DELIVERED) và POD, Khiếu nại.
  - Lưới phân ca, Hộp phê duyệt nhiều cấp, Xin nghỉ / Đổi ca.
  - Ma trận phân quyền UserGroup × ObjectMenu, Cây đơn vị.
  - Hộp thư Bot, KPI ngày / tháng.
  - Database Explorer, GAP Register.
- **4 màn hình GAP**: Chuyển kho, Điều chỉnh tồn, GPS, Danh mục tham chiếu. Các màn hình này chỉ minh họa dữ liệu đang có và đề xuất xử lý, không giả lập bảng mới.
- **75 màn hình danh mục / chi tiết dạng phụ** dùng chung khung sinh từ schema. Mỗi màn hình có KPI theo trạng thái, lọc nhanh, tìm kiếm, sắp xếp, phân trang, xuất CSV, form Thêm/Sửa sinh từ cột SQL (bắt buộc = NOT NULL không có DEFAULT), chuyển trạng thái, xóa mềm, drawer chi tiết (Thông tin / Liên quan / Lịch sử / Truy vết DB).
- **Phân quyền thật trên mockup**: bấm avatar để đổi vai trò. Menu và các nút Thêm / Sửa / Xóa / Xuất được lọc theo `UserRole`.
- **Lịch sử xử lý**: mọi thao tác ghi vào `AuditChange`. Lịch sử phê duyệt lấy từ `ApprovalDecision`, khiếu nại lấy từ `ComplaintHistory`.

## 5. Kịch bản demo gợi ý (khoảng 15 phút)

1. **Tổng quan điều hành**: đổi ngày, ca và đơn vị (header) để thấy KPI thay đổi. Bấm chuông để xem cảnh báo.
2. **Luồng nghiệp vụ E2E**: chọn luồng 3 "Mua hàng" → Bắt đầu demo.
3. **Nhu cầu vật tư**: Tính lại → Tạo đề nghị mua. Sau đó vào **Hộp phê duyệt** duyệt 2 cấp → **Đề nghị mua**: Lập PO → **Đơn mua**: chuyển "Đã gửi" → Nhận hàng → kết luận QC → **Nhập kho**: Ghi sổ. Lô và giao dịch kho được sinh tự động.
4. **Bảng điều độ mẻ**: bấm thẻ mẻ "Kế hoạch" → Nấu → nhập nhiệt độ lõi → QC PASS → Tạo lệnh đóng gói.
5. **Thực hiện giao hàng**: Đã đến → Bàn giao & POD. Nếu khách từ chối suất, hệ thống tự lập khiếu nại.
6. **Phân quyền**: chọn "Bếp trưởng", bỏ tick vài quyền → "Xem mockup với vai trò này" → menu thay đổi theo.
7. **Database Architecture Explorer**: chọn bảng → tab Sơ đồ quan hệ / Dữ liệu demo. Tiếp theo mở **GAP / BA Confirmation**.

## 6. Kiểm thử đã thực hiện (Chrome headless)

- Mở đủ 120 màn hình: 0 lỗi console.
- 18 kịch bản thao tác đều đạt: đơn hàng, MRP → PR → duyệt 2 cấp → PO → nhận hàng → QC → ghi sổ, cổng QC chặn ghi sổ, vòng đời mẻ, giao hàng & POD, xếp xe / xuất bến, phân ca, nghỉ phép, phân quyền & đổi vai trò, form tạo / sửa có kiểm tra bắt buộc, khiếu nại, Explorer, xuất CSV, bộ lọc dashboard, tìm kiếm, checklist, phiên bản công thức, xuất kho FEFO, truy xuất lô.
- Kiểm tra tĩnh 430 lời gọi dữ liệu: 0 bảng hoặc cột ngoài SQL, 0 lần ghi vào cột deprecated.
- Responsive: desktop 1440, tablet 900, mobile 390.

## 7. Giới hạn đã biết

- Dữ liệu DEMO chỉ nằm trong bộ nhớ, không lưu xuống ổ đĩa.
- Bộ trạng thái và quy tắc chuyển bước có nhãn PROPOSED đang chờ BA/PO chốt (xem Gap Register, sheet 07).
- Phân phase P0–P8 là đề xuất, vì không có file Implementation Plan.
- Màn hình phụ dùng khung sinh từ schema. Khi lên Angular cần thiết kế chi tiết thêm theo SRS.
