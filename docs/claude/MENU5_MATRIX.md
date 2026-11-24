<!-- auto-generated: tools/AppDelicare.CodeGen (--layers security) từ Seeds.Menu5.cs – không sửa tay. -->
# MENU-5 – Ma trận quyền theo từng màn (D-79)

Nguồn duy nhất: `tools/AppDelicare.CodeGen/Seeds.Menu5.cs` (ScreenMatrix + Approvers5) → `DB/seed/UserRole_seed.sql` (chỉ thêm dòng thiếu) và `DB/scripts/D79_permission_matrix.sql` (DB đang chạy). Thay bảng theo nhóm màn của ORG_STRUCTURE §2 (GAP-PERM-02).

**Ký hiệu:** Q = Xem + Thêm + Sửa + Xóa + Nhập · S = Xem + Thêm + Sửa · X = Xem · (IX) = có In + Xuất · Duyệt = Xem + Sửa trên mục "Phê duyệt › …" · — = không có. Mục "Việc của tôi" mọi nhóm Xem. Mục quyền vào bot (`#/bot-van-hanh`, `#/bot-dieu-hanh` – D-62) không thuộc ma trận này.

| Menu › Mục | screen-id | CEO | CEO-PMO | COO | OPS-PLAN | OPS-SCM | OPS-WH | OPS-SCM-HEAD | OPS-KIT | OPS-LOG | OPS-POS | OPS-MNT | SAL-SALES | SAL-CON | SAL-HEAD | SAL-CS | FIN-ACC | FIN-HR | FIN-IT | QA |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X |
| Phê duyệt › Tất cả yêu cầu duyệt | `wf-inbox?type=wf-inbox` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Hợp đồng | `wf-inbox?type=crm-contract` | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Chính sách giá | `wf-inbox?type=crm-price-policy` | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Đơn đặt suất (sửa sau chốt) | `wf-inbox?type=crm-order` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | Duyệt | — | — | — | — | — |
| Phê duyệt › Công thức chế biến | `wf-inbox?type=menu-recipe` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Thực đơn tuần | `wf-inbox?type=menu-menu` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Yêu cầu đổi món | `wf-inbox?type=menu-change-request` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Đề nghị mua – cấp 1 | `wf-inbox?type=scm-pr-l1` | — | — | — | — | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Đề nghị mua – cấp 2 | `wf-inbox?type=scm-pr-l2` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Đơn mua hàng | `wf-inbox?type=scm-po` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Đơn mua hàng ≥ 50 triệu | `wf-inbox?type=scm-po-director` | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Phiếu kiểm kê – cấp 1 | `wf-inbox?type=scm-stockcount` | — | — | Duyệt | — | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Phiếu kiểm kê – cấp 2 | `wf-inbox?type=scm-stockcount-l2` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Duyệt | — | — | — |
| Phê duyệt › Phiếu điều chỉnh tồn | `wf-inbox?type=scm-adjustment` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Kế hoạch sản xuất | `wf-inbox?type=prod-plan` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Kiểm tra hàng nhập | `wf-inbox?type=qc-inspection` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Duyệt |
| Phê duyệt › Mẻ nấu | `wf-inbox?type=prod-batch` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Duyệt |
| Phê duyệt › Giữ / thả lô | `wf-inbox?type=scm-lot` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Duyệt |
| Phê duyệt › Đơn xin nghỉ | `wf-inbox?type=hr-leave` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | Duyệt | — | — |
| Phê duyệt › Đơn đổi ca | `wf-inbox?type=hr-swap` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | Duyệt | — | — |
| Phê duyệt › Tài khoản | `wf-inbox?type=adm-user` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Duyệt | — |
| Điều hành › Bảng điều hành | `dash-exec` | X (IX) | X (IX) | X (IX) | X | — | — | X | — | — | — | — | — | — | X | — | X (IX) | — | X | X |
| Điều hành › Chỉ số theo ngày | `kpi-daily` | X (IX) | X (IX) | X (IX) | X | — | — | X | — | — | — | — | — | — | X | — | X (IX) | — | X | X |
| Điều hành › Chỉ số theo tháng | `kpi-monthly` | X (IX) | X (IX) | X (IX) | X | — | — | X | — | — | — | — | — | — | X | — | X (IX) | — | X | X |
| Kinh doanh › Khách hàng | `crm-customer` | X | X | X | X | — | — | — | — | — | X | — | S | Q (IX) | Q (IX) | X | X (IX) | — | X | — |
| Kinh doanh › Hợp đồng | `crm-contract` | X | X | X | — | — | — | — | — | — | — | — | S | Q (IX) | Q (IX) | — | X (IX) | — | X | — |
| Kinh doanh › Đăng ký suất tuần | `crm-order-week` | X | X | X | X | — | — | — | — | — | X | — | S | Q (IX) | Q (IX) | — | — | — | X | — |
| Kinh doanh › Đơn đặt suất | `crm-order` | X | X | X | X | — | — | — | — | X | X | — | S | Q (IX) | Q (IX) | X | X (IX) | — | X | — |
| Kinh doanh › Dự báo suất ăn | `crm-forecast` | X | X | X | X | — | — | — | — | — | — | — | S | Q (IX) | Q (IX) | — | — | — | X | — |
| Kinh doanh › Đối soát suất ăn | `crm-reconciliation` | X | X | X | — | — | — | — | — | — | S | — | X | S | S | X | X (IX) | — | X | — |
| Kinh doanh › Bảng kê thanh toán | `crm-billing` | X (IX) | X | X | — | — | — | — | — | — | — | — | X (IX) | X (IX) | X (IX) | — | X (IX) | — | X | — |
| Kinh doanh › Phản ánh khách hàng | `log-complaint` | X | X | X | — | — | — | — | — | — | S | — | X | X | S | S | — | — | X | S (IX) |
| Kinh doanh › Điểm giao hàng | `crm-location` | X | X | X | X | — | — | — | — | X | X | — | S | Q (IX) | Q (IX) | X | — | — | X | — |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X | X | X | Q | X | — | X | X | — | X | — | X | X | X | X | — | — | X | X |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | X | X | X | Q | X | — | X | X | — | — | — | — | — | — | — | — | — | X | X |
| Thực đơn & Món ăn › Món ăn | `menu-dish` | X | X | X | Q | — | — | — | X | — | — | — | X | X | X | X | — | — | X | X |
| Thực đơn & Món ăn › Nhóm món ăn | `menu-dish-cat` | X | X | X | Q | — | — | — | — | — | — | — | — | — | — | — | — | — | X | — |
| Mua hàng › Tính nhu cầu nguyên liệu | `scm-requirement` | X | X | X | X | S (IX) | — | S (IX) | — | — | — | — | — | — | — | — | — | — | X | — |
| Mua hàng › Đề nghị mua hàng | `scm-pr` | X | X | X | X | S (IX) | — | S (IX) | — | — | — | — | — | — | — | — | — | — | X | — |
| Mua hàng › Đơn mua hàng | `scm-po` | X | X | X | — | S (IX) | X | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Mua hàng › Nhà cung cấp | `scm-supplier` | X | X | X | — | Q | X | Q | — | — | — | — | — | — | — | — | X | — | X | X |
| Mua hàng › Bảng giá nhà cung cấp | `scm-supplier-material` | X | X | X | — | Q | — | Q | — | — | — | — | — | — | — | — | X | — | X | — |
| Mua hàng › Nguyên liệu | `scm-ingredient` | X | X | X | S | Q | X | Q | — | — | — | — | — | — | — | — | — | — | X | X |
| Mua hàng › Nhóm nguyên liệu | `scm-ingredient-cat` | X | X | X | — | Q | — | Q | — | — | — | — | — | — | — | — | — | — | X | — |
| Mua hàng › Đơn vị tính | `scm-uom` | X | X | X | — | Q | — | Q | — | — | — | — | — | — | — | — | — | — | X | — |
| Mua hàng › Quy đổi đơn vị | `scm-uom-conversion` | X | X | X | — | Q | — | Q | — | — | — | — | — | — | — | — | — | — | X | — |
| Kho › Phiếu nhập kho | `scm-gr` | X | X | X | — | X | S (IX) | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | X | X |
| Kho › Phiếu xuất sản xuất | `scm-issue` | X | X | X | — | X | S (IX) | S (IX) | X | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Kho › Tồn kho | `scm-inv-balance` | X | X | X | X | X | X | X | X | — | — | — | — | — | — | — | X (IX) | — | X | X |
| Kho › Lô hàng & hạn dùng | `scm-lot` | X | X | X | X | X | S (IX) | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | X | X |
| Kho › Sổ kho | `scm-inv-txn` | X | X | X | — | X | S (IX) | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Kho › Giữ chỗ nguyên liệu | `scm-reservation` | X | X | X | — | — | S (IX) | S (IX) | — | — | — | — | — | — | — | — | — | — | X | — |
| Kho › Phiếu kiểm kê | `scm-stockcount` | X | X | X | — | — | S (IX) | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Kho › Chênh lệch tiêu hao | `scm-consumption-var` | X | X | X | — | — | X | X | — | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Kho › Kho | `scm-warehouse` | X | X | X | — | X | Q | Q | — | — | — | — | — | — | — | — | — | — | X | — |
| Kho › Khu bảo quản | `scm-zone` | X | X | X | — | — | Q | Q | — | — | — | — | — | — | — | — | — | — | X | — |
| Kho › Vị trí kệ | `scm-location` | X | X | X | — | — | Q | Q | — | — | — | — | — | — | — | — | — | — | X | — |
| Kho › Định mức tồn kho | `scm-inv-policy` | X | X | X | — | — | Q | Q | — | — | — | — | — | — | — | — | — | — | X | — |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | X | X | X | S | X | X | X | X | X | — | — | — | — | — | — | — | — | X | X |
| Sản xuất › Lệnh sản xuất | `prod-order` | X | X | X | S | — | X | X | X | — | — | — | — | — | — | — | — | — | X | — |
| Sản xuất › Mẻ nấu | `prod-batch` | X | X | X | X | — | — | — | S | — | — | X | — | — | — | — | — | — | X | X |
| Sản xuất › Sản lượng | `prod-output` | X | X | X | X | — | — | — | X | — | — | — | — | — | — | — | — | — | X | X |
| Sản xuất › Hao hụt | `prod-waste` | X | X | X | X | — | — | — | S | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Sản xuất › Dây chuyền bếp | `prod-line` | X | X | X | Q | — | — | — | X | — | — | S | — | — | — | — | — | — | X | — |
| Sản xuất › Trạm bếp | `prod-workstation` | X | X | X | Q | — | — | — | X | — | — | S | — | — | — | — | — | — | X | — |
| Giá thành › Giá thành chuẩn | `cost-standard` | X | X | X | S | — | — | — | — | — | — | — | — | — | X | — | X (IX) | — | X | — |
| Giá thành › Giá thành thực tế | `cost-actual` | X | X | X | X (IX) | — | — | — | — | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Giá thành › Giá vốn suất ăn | `cost-meal` | X (IX) | X | X | X (IX) | — | — | — | — | — | — | — | — | — | X | — | X (IX) | — | X | — |
| Chất lượng & ATTP › Kiểm tra hàng nhập | `qc-inspection` | X | X | X | X | X | — | X | — | — | — | — | — | — | — | — | — | — | X | S (IX) |
| Chất lượng & ATTP › Kiểm tra theo ca / mẻ | `qc-checklist-result` | X | X | X | X | — | — | — | S | — | — | — | — | — | — | — | — | — | X | S (IX) |
| Chất lượng & ATTP › Nhật ký nhiệt độ | `qc-temperature` | X | X | X | X | — | — | — | S | X | — | X | — | — | — | — | — | — | X | S (IX) |
| Chất lượng & ATTP › Vệ sinh khay hộp | `qc-cleaning` | X | X | X | X | — | — | — | S | — | — | — | — | — | — | — | — | — | X | S (IX) |
| Chất lượng & ATTP › Kiểm tra vệ sinh | `qc-cleaning-result` | X | X | X | X | — | — | — | S | — | — | — | — | — | — | — | — | — | X | S (IX) |
| Chất lượng & ATTP › Sự cố nhà cung cấp | `qc-supplier-issue` | X | X | X | X | X | — | X | — | — | — | — | — | — | — | — | — | — | X | Q |
| Chất lượng & ATTP › Bộ kiểm tra ATTP | `qc-checklist` | X | X | X | X | — | — | — | X | — | — | — | — | — | — | — | — | — | X | Q |
| Đóng gói & Giao hàng › Lệnh đóng gói | `log-packing-order` | X | X | X | — | — | — | — | S | X | — | — | — | — | — | — | — | — | X | — |
| Đóng gói & Giao hàng › Kết quả đóng gói | `log-packing-result` | X | X | X | — | — | — | — | S | X | — | — | — | — | — | — | — | — | X | — |
| Đóng gói & Giao hàng › Đơn giao hàng | `log-delivery-order` | X | X | X | — | — | — | — | — | S | — | — | X | X | X | — | — | — | X | X |
| Đóng gói & Giao hàng › Chuyến giao hàng | `log-route` | X | X | X | — | — | — | — | — | S | — | — | — | — | — | — | — | — | X | — |
| Đóng gói & Giao hàng › Xếp hàng lên xe | `log-loading` | X | X | X | — | — | — | — | — | S | — | — | — | — | — | — | — | — | X | — |
| Đóng gói & Giao hàng › Theo dõi chuyến giao | `log-execution` | X | X | X | — | — | — | — | — | S | X | — | X | X | X | X | — | — | X | X |
| Đóng gói & Giao hàng › Vị trí xe | `log-gps` | X | X | X | — | — | — | — | — | X | — | — | — | — | — | — | — | — | X | — |
| Đóng gói & Giao hàng › Xác nhận đã giao | `log-pod` | X | X | X | — | — | — | — | — | S | S | — | X | X | X | X | X (IX) | — | X | — |
| Đóng gói & Giao hàng › Thu hồi khay hộp | `log-container-return` | X | X | X | — | — | — | — | — | S | S | — | — | — | — | — | — | — | X | — |
| Đóng gói & Giao hàng › Kiểm tra khay hộp thu hồi | `log-return-inspection` | X | X | X | — | — | — | — | S | — | — | — | — | — | — | — | — | — | X | S (IX) |
| Đóng gói & Giao hàng › Khay hộp theo khách | `log-packaging-ledger` | X | X | X | — | — | — | — | — | X | X | — | X | X | X | — | X (IX) | — | X | — |
| Đóng gói & Giao hàng › Xe giao hàng | `log-vehicle` | X | X | X | — | — | — | — | — | Q | — | — | — | — | — | — | — | — | X | — |
| Nhân sự › Nhân viên | `adm-employee` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | X | — |
| Nhân sự › Phân ca | `hr-shift-assign` | X | X | X | — | — | — | — | X | — | — | — | — | — | — | — | — | S (IX) | X | — |
| Nhân sự › Chấm công | `hr-attendance` | X | X | X | — | — | — | — | X | — | — | — | — | — | — | — | X (IX) | S (IX) | X | — |
| Nhân sự › Đơn xin nghỉ | `hr-leave` | X | X | X | — | — | — | — | X | — | — | — | — | — | — | — | — | S (IX) | X | — |
| Nhân sự › Đơn đổi ca | `hr-swap` | X | X | X | — | — | — | — | X | — | — | — | — | — | — | — | — | S (IX) | X | — |
| Nhân sự › Ca làm việc | `hr-shift` | X | X | X | — | — | — | — | X | — | — | — | — | — | — | — | — | Q | X | — |
| Nhân sự › Chức vụ | `adm-position` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | Q | X | — |
| Nhân sự › Hộp phê duyệt | `wf-inbox` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | — |
| Quản trị hệ thống › Tài khoản | `adm-user` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Nhóm quyền | `adm-usergroup` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Phân quyền | `adm-permission` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Cấu hình menu | `adm-objectmenu` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Sơ đồ tổ chức | `adm-orgunit` | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | Q (IX) | — |
| Quản trị hệ thống › Công ty | `adm-company` | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Nhập dữ liệu gốc (Excel) | `sys-import` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Danh mục dùng chung | `adm-reference` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Địa giới hành chính | `adm-admin-unit` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Tài liệu đính kèm | `adm-document` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Nhật ký & Tích hợp › Job tự động | `adm-job` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Nhật ký & Tích hợp › Nhật ký thay đổi dữ liệu | `adm-audit` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |
| Nhật ký & Tích hợp › Nhật ký truy cập | `adm-access-log` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Nhật ký & Tích hợp › Dữ liệu trùng cần gộp | `adm-merge-conflict` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |
| Nhật ký & Tích hợp › Kênh nhắn tin | `int-channel` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |
| Nhật ký & Tích hợp › Bot | `int-bot` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |
| Nhật ký & Tích hợp › Liên kết tài khoản chat | `int-identity` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | S (IX) | — |
| Nhật ký & Tích hợp › Tin nhắn đến | `int-inbound` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |
| Nhật ký & Tích hợp › Tin nhắn đi | `int-outbound` | X | — | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |
| Nhật ký & Tích hợp › Nhật ký tích hợp | `int-log` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |
| Nhật ký & Tích hợp › Danh mục chỉ số | `kpi-definition` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |
| Nhật ký & Tích hợp › Nhật ký tính chỉ số | `kpi-refresh` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |

## Số mục được vào / số ô quyền

```
  CEO           vào  85 mục · ô quyền   98
  CEO-PMO       vào  80 mục · ô quyền   86
  COO           vào  91 mục · ô quyền  108
  OPS-PLAN      vào  38 mục · ô quyền   82
  OPS-SCM       vào  21 mục · ô quyền   57
  OPS-WH        vào  18 mục · ô quyền   58
  OPS-SCM-HEAD  vào  33 mục · ô quyền  111
  OPS-KIT       vào  26 mục · ô quyền   44
  OPS-LOG       vào  16 mục · ô quyền   32
  OPS-POS       vào  12 mục · ô quyền   20
  OPS-MNT       vào   5 mục · ô quyền    9
  SAL-SALES     vào  19 mục · ô quyền   37
  SAL-CON       vào  19 mục · ô quyền   67
  SAL-HEAD      vào  25 mục · ô quyền   76
  SAL-CS        vào  11 mục · ô quyền   15
  FIN-ACC       vào  28 mục · ô quyền   77
  FIN-HR        vào  12 mục · ô quyền   42
  FIN-IT        vào 101 mục · ô quyền  214
  QA            vào  29 mục · ô quyền   68
  Duyệt: crm-contract = CEO; crm-price-policy = CEO; scm-po-director = CEO; crm-order = SAL-HEAD, COO; menu-recipe = COO; menu-menu = COO; menu-change-request = COO; prod-plan = COO; scm-adjustment = COO; scm-po = COO; scm-pr-l1 = OPS-SCM-HEAD; scm-pr-l2 = COO; scm-stockcount = OPS-SCM-HEAD, COO; scm-stockcount-l2 = FIN-ACC; hr-leave = FIN-HR, COO; hr-swap = FIN-HR, COO; qc-inspection = QA; prod-batch = QA; scm-lot = QA; adm-user = FIN-IT
```
