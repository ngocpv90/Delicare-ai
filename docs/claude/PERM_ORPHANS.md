# PERM_ORPHANS – quyền lẻ trên mục đã ẩn không có màn cha (UI-6.49 (8), QĐ-71, D-127 g)

> Sinh từ DB dev 2026-11-25 bằng SQL theo `ScreenMap.HiddenScreenParent` (cùng truy vấn kiểm tổng UI-6.44): nhóm có quyền Xem trên mục **đã ẩn** (ObjectMenu.IsActive = 0) mà **không** có quyền Xem ở màn cha.
>
> **Kết luận: 70 dòng – tất cả "không còn màn nào dùng" → để nguyên, không cấp thêm, không xóa (D-127 g).**
> - API của các mục này kiểm quyền theo **màn cha** (`ScreenMap.ParentByClass` – vd DeliveryStop → Điều phối giao hàng, ApprovalStep → Phê duyệt, Person → Tài khoản, CustomerContact → Khách hàng); đã rà code máy chủ + giao diện: **0 chỗ** còn kiểm quyền trực tiếp theo mã các mục này.
> - Vì vậy quyền trên dòng này **không mở thêm** được gì; nhóm không có màn cha là đúng ma trận D-79 (anh chốt). Không có dòng nào "cần cho API màn cha" → không cấp theo luật UI-6.44.
> - Khi dọn dữ liệu quyền sau UAT (nếu anh muốn), có thể thu các dòng này bằng script idempotent – chưa làm (không xóa dữ liệu thật).
>
> - **Rà kèm (lỗi thật đã sửa):** 3 API vẫn kiểm quyền theo mục đã ẩn (nên chỉ quản trị dùng được): `CleaningBatch/RecordResults` (qc-cleaning-result → **Vệ sinh khay hộp**), `ContainerReturn/Record` (log-container-return → **Khay hộp theo khách**), `ContainerReturn` kiểm tra khay thu hồi (log-return-inspection → **Vệ sinh khay hộp**) – nay theo màn cha đã nhận hợp quyền (UI-6.32 / 6.35). Test `PermOrphansUi649Tests` chặn tái phát.
>
> Ký hiệu quyền: Xem · Thêm · Sửa · Xóa · In · Xuất · Nhập.

| Nhóm (mã) | Tên nhóm | Mục ẩn | Màn cha | Quyền đang có | Nguồn ẩn | Xử lý |
|---|---|---|---|---|---|---|
| CEO | Tổng giám đốc | `#/adm-person` | `#/adm-user` | Xem | MENU-4 (Thông tin cá nhân → Tài khoản) | Để nguyên (không còn màn dùng) |
| CEO-PMO | Trợ lý TGĐ / PMO | `#/adm-person` | `#/adm-user` | Xem | MENU-4 (Thông tin cá nhân → Tài khoản) | Để nguyên (không còn màn dùng) |
| FIN-ACC | Kế toán | `#/log-complaint-history` | `#/log-complaint` | Xem · In · Xuất | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| FIN-ACC | Kế toán | `#/log-stop` | `#/log-route` | Xem · In · Xuất | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| FIN-ACC | Kế toán | `#/menu-menu-line` | `#/menu-menu` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| FIN-ACC | Kế toán | `#/menu-recipe-line` | `#/menu-recipe` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| FIN-ACC | Kế toán | `#/prod-plan-line` | `#/prod-plan` | Xem · In · Xuất | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| FIN-ACC | Kế toán | `#/scm-pr-line` | `#/scm-pr` | Xem · In · Xuất | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| FIN-ACC | Kế toán | `#/wf-step` | `#/wf-inbox` | Xem · In · Xuất | MENU-4.3 bảng con duyệt | Để nguyên (không còn màn dùng) |
| FIN-ACC | Kế toán | `#/wf-item` | `#/wf-inbox` | Xem · In · Xuất | MENU-4.3 bảng con duyệt | Để nguyên (không còn màn dùng) |
| FIN-ACC | Kế toán | `#/wf-decision` | `#/wf-inbox` | Xem · In · Xuất | MENU-4.3 bảng con duyệt | Để nguyên (không còn màn dùng) |
| FIN-HR | Nhân sự | `#/wf-item` | `#/wf-inbox` | Xem · Thêm · Sửa · Xóa · In · Xuất · Nhập | MENU-4.3 bảng con duyệt | Để nguyên (không còn màn dùng) |
| FIN-HR | Nhân sự | `#/wf-step` | `#/wf-inbox` | Xem · Thêm · Sửa · Xóa · In · Xuất · Nhập | MENU-4.3 bảng con duyệt | Để nguyên (không còn màn dùng) |
| FIN-HR | Nhân sự | `#/wf-decision` | `#/wf-inbox` | Xem · Thêm · Sửa · Xóa · In · Xuất · Nhập | MENU-4.3 bảng con duyệt | Để nguyên (không còn màn dùng) |
| FIN-HR | Nhân sự | `#/adm-person` | `#/adm-user` | Xem | MENU-4 (Thông tin cá nhân → Tài khoản) | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/crm-contract-meal` | `#/crm-contract` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/crm-contact` | `#/crm-customer` | Xem | MENU-5.4 (D-79) – Người liên hệ trong Khách hàng 360 | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/crm-order-line` | `#/crm-order` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/log-stop` | `#/log-route` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/log-event` | `#/log-execution` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/log-complaint-history` | `#/log-complaint` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/scm-stockcount-line` | `#/scm-stockcount` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/wf-step` | `#/wf-inbox` | Xem | MENU-4.3 bảng con duyệt | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/wf-item` | `#/wf-inbox` | Xem | MENU-4.3 bảng con duyệt | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/wf-decision` | `#/wf-inbox` | Xem | MENU-4.3 bảng con duyệt | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/scm-pr-line` | `#/scm-pr` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/qc-inspection-line` | `#/qc-inspection` | Xem · Thêm · Sửa · Xóa · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/scm-gr-line` | `#/scm-gr` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-KIT | Bếp | `#/scm-po-line` | `#/scm-po` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-LOG | Điều phối giao hàng | `#/qc-inspection-line` | `#/qc-inspection` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-LOG | Điều phối giao hàng | `#/qc-checklist-item` | `#/qc-checklist` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-LOG | Điều phối giao hàng | `#/log-complaint-history` | `#/log-complaint` | Xem · Thêm · Sửa · Xóa · In · Xuất · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-LOG | Điều phối giao hàng | `#/crm-contract-meal` | `#/crm-contract` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-MNT | Bảo trì | `#/prod-plan-line` | `#/prod-plan` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-MNT | Bảo trì | `#/qc-inspection-line` | `#/qc-inspection` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-MNT | Bảo trì | `#/qc-checklist-item` | `#/qc-checklist` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-PLAN | Kế hoạch vận hành | `#/scm-gr-line` | `#/scm-gr` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-PLAN | Kế hoạch vận hành | `#/scm-issue-line` | `#/scm-issue` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-PLAN | Kế hoạch vận hành | `#/scm-stockcount-line` | `#/scm-stockcount` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-PLAN | Kế hoạch vận hành | `#/scm-po-line` | `#/scm-po` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-PLAN | Kế hoạch vận hành | `#/log-stop` | `#/log-route` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-PLAN | Kế hoạch vận hành | `#/log-event` | `#/log-execution` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-PLAN | Kế hoạch vận hành | `#/log-complaint-history` | `#/log-complaint` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-PLAN | Kế hoạch vận hành | `#/crm-contract-meal` | `#/crm-contract` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-POS | Điểm phát suất | `#/crm-contract-meal` | `#/crm-contract` | Xem · Thêm · Sửa · Xóa · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-POS | Điểm phát suất | `#/log-stop` | `#/log-route` | Xem · Thêm · Sửa · Xóa · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-POS | Điểm phát suất | `#/menu-recipe-line` | `#/menu-recipe` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-POS | Điểm phát suất | `#/qc-checklist-item` | `#/qc-checklist` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-POS | Điểm phát suất | `#/qc-inspection-line` | `#/qc-inspection` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-SCM | Mua hàng | `#/qc-checklist-item` | `#/qc-checklist` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-SCM | Mua hàng | `#/scm-stockcount-line` | `#/scm-stockcount` | Xem · Thêm · Sửa · Xóa · In · Xuất · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-SCM | Mua hàng | `#/crm-contract-meal` | `#/crm-contract` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| OPS-SCM | Mua hàng | `#/crm-contact` | `#/crm-customer` | Xem | MENU-5.4 (D-79) – Người liên hệ trong Khách hàng 360 | Để nguyên (không còn màn dùng) |
| OPS-SCM | Mua hàng | `#/crm-order-line` | `#/crm-order` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| QA | QA / ATTP | `#/crm-contact` | `#/crm-customer` | Xem | MENU-5.4 (D-79) – Người liên hệ trong Khách hàng 360 | Để nguyên (không còn màn dùng) |
| QA | QA / ATTP | `#/crm-order-line` | `#/crm-order` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| QA | QA / ATTP | `#/crm-contract-meal` | `#/crm-contract` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| QA | QA / ATTP | `#/scm-stockcount-line` | `#/scm-stockcount` | Xem · Thêm · Sửa · Xóa · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| QA | QA / ATTP | `#/scm-po-line` | `#/scm-po` | Xem · Thêm · Sửa · Xóa · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| QA | QA / ATTP | `#/scm-pr-line` | `#/scm-pr` | Xem · Thêm · Sửa · Xóa · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| QA | QA / ATTP | `#/scm-issue-line` | `#/scm-issue` | Xem · Thêm · Sửa · Xóa · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| SAL-CON | Hợp đồng & đơn hàng | `#/menu-recipe-line` | `#/menu-recipe` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| SAL-CON | Hợp đồng & đơn hàng | `#/prod-plan-line` | `#/prod-plan` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| SAL-CS | Chăm sóc khách hàng | `#/menu-recipe-line` | `#/menu-recipe` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| SAL-CS | Chăm sóc khách hàng | `#/log-stop` | `#/log-route` | Xem · Thêm · Sửa · Xóa · Nhập | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| SAL-CS | Chăm sóc khách hàng | `#/crm-contract-meal` | `#/crm-contract` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| SAL-CS | Chăm sóc khách hàng | `#/qc-checklist-item` | `#/qc-checklist` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| SAL-CS | Chăm sóc khách hàng | `#/qc-inspection-line` | `#/qc-inspection` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| SAL-SALES | Kinh doanh | `#/menu-recipe-line` | `#/menu-recipe` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |
| SAL-SALES | Kinh doanh | `#/prod-plan-line` | `#/prod-plan` | Xem | MENU-4.3 (D-75 e) – bảng con / dòng chứng từ | Để nguyên (không còn màn dùng) |

## Tổng theo mục ẩn

| Mục ẩn | Màn cha | Số nhóm |
|---|---|---|
| `#/crm-contract-meal` | `#/crm-contract` | 7 |
| `#/log-stop` | `#/log-route` | 5 |
| `#/menu-recipe-line` | `#/menu-recipe` | 5 |
| `#/qc-inspection-line` | `#/qc-inspection` | 5 |
| `#/qc-checklist-item` | `#/qc-checklist` | 5 |
| `#/log-complaint-history` | `#/log-complaint` | 4 |
| `#/prod-plan-line` | `#/prod-plan` | 4 |
| `#/scm-stockcount-line` | `#/scm-stockcount` | 4 |
| `#/adm-person` | `#/adm-user` | 3 |
| `#/scm-pr-line` | `#/scm-pr` | 3 |
| `#/wf-step` | `#/wf-inbox` | 3 |
| `#/wf-item` | `#/wf-inbox` | 3 |
| `#/wf-decision` | `#/wf-inbox` | 3 |
| `#/crm-contact` | `#/crm-customer` | 3 |
| `#/crm-order-line` | `#/crm-order` | 3 |
| `#/scm-po-line` | `#/scm-po` | 3 |
| `#/log-event` | `#/log-execution` | 2 |
| `#/scm-gr-line` | `#/scm-gr` | 2 |
| `#/scm-issue-line` | `#/scm-issue` | 2 |
| `#/menu-menu-line` | `#/menu-menu` | 1 |
