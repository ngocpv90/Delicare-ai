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
| Phê duyệt › Nghỉ & đổi ca | `wf-inbox?type=hr-leave` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | Duyệt | — | — |
| Phê duyệt › Tài khoản | `wf-inbox?type=adm-user` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Duyệt | — |
| Phê duyệt › Xếp xe chưa đủ hàng | `wf-inbox?type=log-load-short` | — | — | Duyệt | — | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Mở lại đối soát | `wf-inbox?type=crm-recon-reopen` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Mở khóa kỳ | `wf-inbox?type=acc-period-unlock` | — | — | Duyệt | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Phê duyệt › Lệnh của agent | `wf-inbox?type=ai-agent-cmd` | — | — | Duyệt | Duyệt | Duyệt | Duyệt | Duyệt | Duyệt | Duyệt | Duyệt | — | Duyệt | Duyệt | Duyệt | Duyệt | — | Duyệt | — | Duyệt |
| Phê duyệt › Duyệt SOP | `wf-inbox?type=kb-sop` | Duyệt | — | Duyệt | — | — | Duyệt | Duyệt | Duyệt | Duyệt | — | — | — | — | Duyệt | — | Duyệt | Duyệt | — | Duyệt |
| Điều hành › Bảng điều hành | `dash-exec` | X (IX) | X (IX) | X (IX) | X | — | — | X | — | — | — | — | — | — | X | — | X (IX) | — | X | X |
| Điều hành › Chỉ số kinh doanh | `kpi-daily` | X (IX) | X (IX) | X (IX) | X | — | — | X | — | — | — | — | — | — | X | — | X (IX) | — | X | X |
| Điều hành › Giá vốn & lãi gộp | `cost-meal` | X (IX) | X | X | X (IX) | — | — | — | — | — | — | — | — | — | X | — | X (IX) | — | X | — |
| Điều hành › Kết quả kinh doanh | `dash-pl` | X (IX) | — | X (IX) | — | — | — | — | — | — | — | — | — | — | — | — | Sửa (IX) | — | X (IX) | — |
| Điều hành › Trung tâm Agent | `ai-agents` | S | — | S | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S | — |
| Điều hành › Nhật ký agent | `ai-agent-log` | X | — | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | — |
| Điều hành › Luồng liên phòng | `ai-flow` | X | — | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | — |
| Điều hành › Giao việc | `ai-tasks` | S | — | S | — | S | S | S | S | S | — | — | — | — | S | — | — | S | S | S |
| Điều hành › Lịch chạy | `ai-schedule` | S | — | S | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S | — |
| Điều hành › Sử dụng & chi phí | `ai-cost` | X | — | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | — |
| Điều hành › Bản tin sáng – Ban GĐ | `ai-ban-tin-sang` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Điều hành › Báo cáo cuối ngày – Ban GĐ | `ai-ban-tin-chieu` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Điều hành › Bản tin kho buổi sáng | `ai-ban-tin-kho` | — | — | — | X | — | X | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Điều hành › Bản tin bếp đầu ca | `ai-ban-tin-bep` | — | — | — | — | — | — | — | X | — | — | — | — | — | — | — | — | — | — | — |
| Điều hành › Nhắc chốt suất | `ai-ban-tin-kd` | — | — | — | — | — | — | — | — | — | — | — | X | — | X | — | — | — | — | — |
| Điều hành › Dùng bot Trợ lý | `ai-bot-tro-ly` | X | X | X | X | — | — | X | X | X | — | — | — | — | X | — | X | X | X | X |
| Kinh doanh › Khách hàng | `crm-customer` | X | X | X | X | — | — | — | — | X | X | — | S | Q (IX) | Q (IX) | X | X (IX) | — | X | — |
| Kinh doanh › Hợp đồng | `crm-contract` | X | X | X | — | — | — | — | — | — | — | — | S | Q (IX) | Q (IX) | — | X (IX) | — | X | — |
| Kinh doanh › Đăng ký suất tuần | `crm-order-week` | X | X | X | X | — | — | — | — | — | X | — | S | Q (IX) | Q (IX) | — | — | — | X | — |
| Kinh doanh › Đơn hàng theo ngày | `crm-order` | X | X | X | X | — | — | — | — | X | X | — | S | Q (IX) | Q (IX) | X | X (IX) | — | X | — |
| Kinh doanh › Dự báo suất ăn | `crm-forecast` | X | X | X | X | — | — | — | — | — | — | — | S | Q (IX) | Q (IX) | — | — | — | X | — |
| Kinh doanh › Đối soát suất ăn | `crm-reconciliation` | X | X | X | — | — | — | — | — | — | S | — | X | S | S | X | S (IX) | — | X | — |
| Kinh doanh › Phản ánh khách hàng | `log-complaint` | X | X | X | — | — | — | — | — | — | S | — | X | X | S | S | — | — | X | S (IX) |
| Kế toán › Chứng từ kế toán | `acc-voucher` | X (IX) | X | X (IX) | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — | X | — |
| Kế toán › Bảng kê thanh toán | `crm-billing` | X (IX) | X | X (IX) | — | — | — | — | — | — | — | — | X (IX) | X (IX) | Q (IX) | — | Q (IX) | — | X | — |
| Kế toán › Khớp mã AMIS | `acc-amis-code` | X (IX) | X | X (IX) | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — | X | — |
| Kế toán › Đối chiếu với AMIS | `acc-amis-recon` | X (IX) | X | X (IX) | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — | X | — |
| Kế toán › Khóa kỳ | `acc-period` | X (IX) | X | X (IX) | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — | X | — |
| Bán lẻ › Đơn khách lẻ | `sal-retail-order` | X (IX) | X | X (IX) | X (IX) | — | — | — | X (IX) | Sửa (IX) | S (IX) | — | S (IX) | — | Q (IX) | S (IX) | X (IX) | — | X | — |
| Bán lẻ › Sản phẩm bán lẻ | `sal-retail-product` | X (IX) | X | X (IX) | — | — | — | — | — | — | X | — | X | — | Q (IX) | X | X (IX) | — | X | — |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X | X | X | Q | X | — | X | X | — | X | — | X | X | X | X | — | — | X | X |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | X | X | X | Q | X | — | X | X | — | — | — | — | — | — | — | — | — | X | X |
| Thực đơn & Món ăn › Món ăn | `menu-dish` | X | X | X | Q | — | — | — | X | — | — | — | X | X | X | X | — | — | X | X |
| Mua hàng › Tính nhu cầu nguyên liệu | `scm-requirement` | X | X | X | X | S (IX) | — | S (IX) | — | — | — | — | — | — | — | — | — | — | X | — |
| Mua hàng › Đề nghị mua hàng | `scm-pr` | X | X | X | X | S (IX) | — | S (IX) | — | — | — | — | — | — | — | — | — | — | X | — |
| Mua hàng › Đơn mua hàng | `scm-po` | X | X | X | — | S (IX) | X | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Mua hàng › Nhà cung cấp | `scm-supplier` | X | X | X | — | Q | X | Q | — | — | — | — | — | — | — | — | X | — | X | X |
| Mua hàng › Bảng giá nhà cung cấp | `scm-supplier-material` | X | X | X | — | Q | — | Q | — | — | — | — | — | — | — | — | X | — | X | — |
| Mua hàng › Nguyên liệu | `scm-ingredient` | X | X | X | S | Q | X | Q | — | — | — | — | — | — | — | — | — | — | X | X |
| Kho › Nhận hàng | `scm-gr` | X | X | X | — | X | S (IX) | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | X | X |
| Kho › Trả hàng NCC | `scm-supplier-return` | X | X | X | — | Sửa | S (IX) | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Kho › Xuất kho sản xuất | `scm-issue` | X | X | X | — | X | S (IX) | S (IX) | X | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Kho › Tồn kho | `scm-inv-balance` | X | X | X | X | X | Q (IX) | Q (IX) | X | — | — | — | — | — | — | — | X (IX) | — | X | X |
| Kho › Kiểm kê | `scm-stockcount` | X | X | X | — | — | S (IX) | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | X | X | X | S | X | X | X | X | X | — | — | — | — | — | — | — | — | X | X |
| Sản xuất › Mẻ nấu | `prod-batch` | X | X | X | X | — | — | — | S | — | — | X | — | — | — | — | — | — | X | X |
| Sản xuất › Hao hụt | `prod-waste` | X | X | X | X | — | — | — | S | — | — | — | — | — | — | — | X (IX) | — | X | — |
| Chất lượng & ATTP › Sổ kiểm tra hàng nhập | `qc-inspection` | X | X | X | X | X | — | X | — | — | — | — | — | — | — | — | — | — | X | S (IX) |
| Chất lượng & ATTP › Kiểm tra theo ca / mẻ | `qc-checklist-result` | X | X | X | X | — | — | — | S | — | — | — | — | — | — | — | — | — | X | S (IX) |
| Chất lượng & ATTP › Nhật ký nhiệt độ | `qc-temperature` | X | X | X | X | — | — | — | S | X | — | X | — | — | — | — | — | — | X | S (IX) |
| Chất lượng & ATTP › Vệ sinh khay hộp | `qc-cleaning` | X | X | X | X | — | — | — | S | — | — | — | — | — | — | — | — | — | X | S (IX) |
| Chất lượng & ATTP › Sự cố nhà cung cấp | `qc-supplier-issue` | X | X | X | X | X | — | X | — | — | — | — | — | — | — | — | — | — | X | Q |
| Chất lượng & ATTP › Bộ kiểm tra ATTP | `qc-checklist` | X | X | X | X | — | — | — | X | — | — | — | — | — | — | — | — | — | X | Q |
| Đóng gói & Giao hàng › Chia suất & đóng gói | `log-packing-order` | X | X | X | — | — | — | — | S | X | — | — | — | — | — | — | — | — | X | — |
| Đóng gói & Giao hàng › Điều phối giao hàng | `log-route` | X | X | X | — | — | — | — | — | S | — | — | X | X | X | — | — | — | X | X |
| Đóng gói & Giao hàng › Theo dõi giao hàng | `log-execution` | X | X | X | — | — | — | — | — | S | S | — | X | X | X | X | X (IX) | — | X | X |
| Đóng gói & Giao hàng › Khay hộp theo khách | `log-packaging-ledger` | X | X | X | — | — | — | — | — | S | S | — | X | X | X | — | X (IX) | — | X | — |
| Đóng gói & Giao hàng › Loại hộp / khay | `log-packaging-type` | X | X | X | — | — | — | — | X | S | X | — | X | X | X | — | Sửa (IX) | — | X | — |
| Đóng gói & Giao hàng › Định mức bao bì | `log-packaging-norm` | X | X | X | S | X | X | X | X | X | — | — | — | — | — | — | X (IX) | — | X | — |
| Nhân sự › Nhân viên | `adm-employee` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | X | — |
| Nhân sự › Phân ca | `hr-shift-assign` | X | X | X | — | — | — | — | X | — | — | — | — | — | — | — | — | S (IX) | X | — |
| Nhân sự › Chấm công | `hr-attendance` | X | X | X | — | — | — | — | X | — | — | — | — | — | — | — | X (IX) | S (IX) | X | — |
| Nhân sự › Nghỉ & đổi ca | `hr-leave` | X | X | X | — | — | — | — | X | — | — | — | — | — | — | — | — | S (IX) | X | — |
| Nhân sự › Hộp phê duyệt | `wf-inbox` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | — |
| Danh mục hệ thống › Công ty & đơn vị | `adm-orgunit` | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | Q (IX) | — |
| Danh mục hệ thống › Chức vụ | `adm-position` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | Q | X | — |
| Danh mục hệ thống › Ca làm việc | `hr-shift` | X | X | X | — | — | — | — | X | — | — | — | — | — | — | — | — | Q | X | — |
| Danh mục hệ thống › Đơn vị tính | `scm-uom` | X | X | X | — | Q | — | Q | — | — | — | — | — | — | — | — | — | — | X | — |
| Danh mục hệ thống › Kho & vị trí | `scm-warehouse` | X | X | X | — | X | Q | Q | — | — | — | — | — | — | — | — | — | — | X | — |
| Danh mục hệ thống › Dây chuyền & trạm bếp | `prod-line` | X | X | X | Q | — | — | — | X | — | — | S | — | — | — | — | — | — | X | — |
| Danh mục hệ thống › Xe giao hàng | `log-vehicle` | X | X | X | — | — | — | — | — | Q | — | — | — | — | — | — | — | — | X | — |
| Danh mục hệ thống › Danh mục dùng chung | `adm-reference` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Danh mục hệ thống › Địa giới hành chính | `adm-admin-unit` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Danh mục hệ thống › Danh mục chỉ số | `kpi-definition` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |
| Tri thức › Kho tri thức | `knowledge` | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X |
| Tri thức › 10 Ban GĐ | `knowledge?folder=10` | S | X | S | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Tri thức › 20 Kinh doanh & Hợp đồng | `knowledge?folder=20` | X | X | X | — | — | — | — | — | X | — | — | X | X | S | X | X | — | — | — |
| Tri thức › 30 Thực đơn & Công thức | `knowledge?folder=30` | X | X | X | X | X | — | X | S | — | — | — | — | — | X | — | — | — | — | X |
| Tri thức › 40 Kho & Truy xuất | `knowledge?folder=40` | X | X | X | X | X | S | S | — | — | — | — | — | — | — | — | X | — | — | X |
| Tri thức › 45 ATTP | `knowledge?folder=45` | X | X | X | — | — | X | — | X | X | — | — | — | — | — | — | — | — | — | S |
| Tri thức › 50 Sản xuất | `knowledge?folder=50` | X | X | X | X | — | — | — | S | — | — | — | — | — | — | — | — | — | — | X |
| Tri thức › 55 Giao vận | `knowledge?folder=55` | X | X | X | — | — | — | — | — | S | — | — | — | — | X | X | — | — | — | X |
| Tri thức › 60 Kế toán | `knowledge?folder=60` | X | X | X | — | — | — | X | — | — | — | — | — | — | — | — | S | — | — | — |
| Tri thức › 70 Nhân sự | `knowledge?folder=70` | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | S | X | X |
| Tri thức › 80 Đào tạo | `knowledge?folder=80` | X | X | S | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X |
| Tri thức › 95 Nguồn BOT | `knowledge?folder=95` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S | — |
| Tri thức › 96 Nhật ký | `knowledge?folder=96` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | — |
| Tri thức › 97 Tương tác | `knowledge?folder=97` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | — |
| Tri thức › 98 Công việc | `knowledge?folder=98` | X | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | X | — |
| Tri thức › 99 Mẫu | `knowledge?folder=99` | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | S | X |
| Tri thức › BOD Bảo mật | `knowledge?folder=bod` | S | X | X | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Tri thức › 05 Công văn đến / đi | `knowledge?folder=05` | X (IX) | S (IX) | X (IX) | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Tri thức › 15 Họp & giao ban | `knowledge?folder=15` | S (IX) | X (IX) | S (IX) | — | — | X (IX) | S (IX) | S (IX) | X (IX) | — | — | — | — | S (IX) | — | S (IX) | S (IX) | — | S (IX) |
| Tri thức › 20 › Hợp đồng khách hàng | `knowledge?folder=20-hd` | X (IX) | X (IX) | X (IX) | — | — | — | — | — | — | — | — | — | S (IX) | S (IX) | — | X (IX) | — | — | — |
| Tri thức › 40 › Hợp đồng nhà cung cấp | `knowledge?folder=40-hd` | X (IX) | X (IX) | X (IX) | — | X (IX) | — | S (IX) | — | — | — | — | — | — | — | — | X (IX) | — | — | — |
| Tri thức › 60 › Chứng từ gốc | `knowledge?folder=60-ct` | X (IX) | X (IX) | X (IX) | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — | — | — |
| Tri thức › 70 › Hồ sơ cá nhân | `knowledge?folder=70-hs` | X (IX) | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — | — |
| Tri thức › 80 › Hướng dẫn sử dụng | `knowledge?folder=80-hdsd` | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X | X |
| Quản trị hệ thống › Tài khoản | `adm-user` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Nhóm quyền | `adm-usergroup` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Cấu hình menu | `adm-objectmenu` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
| Quản trị hệ thống › Nhập dữ liệu gốc (Excel) | `sys-import` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | Q (IX) | — |
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
| Nhật ký & Tích hợp › Nhật ký tính chỉ số | `kpi-refresh` | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S (IX) | — |

## Số mục được vào / số ô quyền

```
  CEO           vào 129 mục · ô quyền  182
  CEO-PMO       vào 115 mục · ô quyền  134
  COO           vào 138 mục · ô quyền  199
  OPS-PLAN      vào  51 mục · ô quyền   99
  OPS-SCM       vào  33 mục · ô quyền   75
  OPS-WH        vào  32 mục · ô quyền   88
  OPS-SCM-HEAD  vào  50 mục · ô quyền  153
  OPS-KIT       vào  43 mục · ô quyền   75
  OPS-LOG       vào  33 mục · ô quyền   63
  OPS-POS       vào  21 mục · ô quyền   38
  OPS-MNT       vào  10 mục · ô quyền   14
  SAL-SALES     vào  31 mục · ô quyền   54
  SAL-CON       vào  29 mục · ô quyền   83
  SAL-HEAD      vào  44 mục · ô quyền  127
  SAL-CS        vào  21 mục · ô quyền   30
  FIN-ACC       vào  53 mục · ô quyền  165
  FIN-HR        vào  23 mục · ô quyền   69
  FIN-IT        vào 127 mục · ô quyền  252
  QA            vào  45 mục · ô quyền   95
  Duyệt: crm-contract = CEO; crm-price-policy = CEO; scm-po-director = CEO; crm-order = SAL-HEAD, COO; menu-recipe = COO; menu-menu = COO; menu-change-request = COO; prod-plan = COO; scm-adjustment = COO; scm-po = COO; scm-pr-l1 = OPS-SCM-HEAD; scm-pr-l2 = COO; scm-stockcount = OPS-SCM-HEAD, COO; scm-stockcount-l2 = FIN-ACC; hr-leave = FIN-HR, COO; hr-swap = FIN-HR, COO; qc-inspection = QA; prod-batch = QA; scm-lot = QA; adm-user = FIN-IT; crm-recon-reopen = COO; acc-period-unlock = COO; ai-agent-cmd = OPS-WH, OPS-KIT, QA, OPS-SCM, OPS-SCM-HEAD, SAL-HEAD, OPS-LOG, FIN-HR, COO, OPS-PLAN, OPS-POS, SAL-SALES, SAL-CON, SAL-CS; kb-sop = CEO, COO, OPS-KIT, QA, OPS-WH, OPS-SCM-HEAD, SAL-HEAD, OPS-LOG, FIN-ACC, FIN-HR; log-load-short = COO, OPS-SCM-HEAD
```
