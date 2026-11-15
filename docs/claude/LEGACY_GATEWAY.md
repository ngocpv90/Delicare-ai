# LEGACY_GATEWAY – Hợp đồng của bản demo Apps Script v2.11 (THAM KHẢO)

> **Bản Apps Script là demo, chưa vận hành thật (D-16).** Không có người dùng hay bot thật nào phụ thuộc vào hợp đồng này. File này chỉ dùng để: (1) tham khảo nghiệp vụ / quyền theo vai trò; (2) đặt tên và tham số cho công cụ đọc của agent ở Release 2, để Hermes đang thử nghiệm dùng lại được. Controller tương thích `POST /api/gateway` (T35) là **tùy chọn**, chỉ làm nếu anh muốn Hermes demo trỏ sang .NET.

Nguồn: `openapi.yaml` và `HUONG_DAN_BAN_GIAO_NET.md` trên Google Drive (thư mục `NHAMAY/00_HE_THONG/DELICARE_CLAUDE`, bộ bàn giao F1).

- **Hợp đồng cũ:** `POST /exec?k=GATEWAY_KEY`, thân `{fn, args, telegram_id}`, luôn trả HTTP 200 `{ok: bool, error?: string, ...}`.
- **Mã lỗi:** `UNAUTHORIZED`, `NGUOI_DUNG_KHONG_HOP_LE`, `KHONG_CO_QUYEN`, `HAM_KHONG_TON_TAI: <fn>`, hoặc thông báo nghiệp vụ.
- **Lõi .NET cung cấp:** `POST /api/gateway` (cùng thân, cùng kiểu trả). Khóa `k` thay bằng header `X-Gateway-Key`, nhưng vẫn nhận query `k` trong giai đoạn chuyển.
- `telegram_id` → `UserExternalIdentity` (kênh TELEGRAM) → `User` → `UserGroup`.
- **Vai trò cũ → UserGroup** (seed `UserGroup.Name` = mã vai trò): `GIAM_DOC, TP_KINH_DOANH, KINH_DOANH, TRUONG_BEP, TP_VAN_HANH, TRUONG_CA, TO_TRUONG, THU_KHO, MUA_HANG, KE_TOAN, NHAN_SU, TRUONG_BO_PHAN, DIEU_PHOI_GV, TAI_XE, ADMIN`.
- **Lưu ý:** trong lõi mới, phiếu ghi qua gateway cũ cũng phải đi qua ApprovalService / máy trạng thái giống REST.

## 34 hàm

| fn | Nhóm | Đọc/Ghi | Tham số (args) | Vai trò được gọi | Ánh xạ sang lõi .NET (Application) |
|---|---|---|---|---|---|
| nguoi_dung_hien_tai | Hệ thống | R | – | mọi người dùng hoạt động | `CurrentActorQuery` |
| get_ton_kho | Kho | R | ma_nl, kho (KHO_KHO/KHO_LANH/KHO_DONG) | THU_KHO, MUA_HANG, TRUONG_BEP, TP_VAN_HANH, GIAM_DOC, KE_TOAN, ADMIN | `Inventory.StockOnHandQuery` |
| get_lo_can_date | Kho | R | so_ngay=7 | như trên | `Inventory.ExpiringLotsQuery` |
| du_bao_thieu_hut | Kho | R | so_ngay=3 | như trên | `Planning.ShortageForecastQuery` |
| get_don_hang_ngay | Kinh doanh | R | ngay | KINH_DOANH, TP_KINH_DOANH, TRUONG_BEP, TP_VAN_HANH, THU_KHO, MUA_HANG, GIAM_DOC, ADMIN | `Orders.DailyOrdersQuery` |
| don_hang_khoang | Kinh doanh | R | tu_ngay, den_ngay (≤62 ngày) | như trên | `Orders.OrdersRangeQuery` |
| tinh_nguyen_lieu | Kho | R | ngay, ca | như trên | `Planning.MaterialRequirementQuery` |
| truy_xuat_lo | Truy xuất | R | ma_lo | TRUONG_BEP, TP_VAN_HANH, THU_KHO, MUA_HANG, GIAM_DOC, ADMIN | `Quality.TraceLotForwardQuery` |
| truy_xuat_me | Truy xuất | R | ma_me | như trên | `Quality.TraceBatchBackwardQuery` |
| bao_cao_ngay | Báo cáo | R | ngay | GIAM_DOC, TP_KINH_DOANH, TRUONG_BEP, TP_VAN_HANH, KE_TOAN, ADMIN | `Kpi.DailyReportQuery` |
| bao_cao_dieu_hanh | Báo cáo | R | ngay | như trên | `Kpi.ExecutiveBriefQuery` |
| kpi_khoang | Báo cáo | R | tu_ngay, den_ngay (≤31) | như trên | `Kpi.KpiRangeQuery` |
| su_co_ngay | Báo cáo | R | ngay | như trên | `Kpi.IncidentsQuery` |
| ke_hoach_ngay | Sản xuất | R | ngay (mặc định mai) | TO_TRUONG, TRUONG_CA, TRUONG_BEP, TP_VAN_HANH, KINH_DOANH, TP_KINH_DOANH, GIAM_DOC, KE_TOAN, ADMIN | `Production.DailyPlanQuery` |
| tien_do_bep | Sản xuất | R | – | như trên | `Production.KitchenProgressSummaryQuery` |
| tien_do_bep_chi_tiet | Sản xuất | R | – | như trên | `Production.KitchenProgressDetailQuery` |
| hao_hut_khoang | Sản xuất | R | tu_ngay, den_ngay | GIAM_DOC, TP_KINH_DOANH, TRUONG_BEP, TP_VAN_HANH, KE_TOAN, ADMIN | `Production.WasteRangeQuery` |
| thuc_don_tuan | Sản xuất | R | ngay | như trên | `Menu.WeeklyMenuQuery` |
| giao_van_ngay | Giao vận | R | ngay | như trên | `Logistics.DailyDeliveryQuery` |
| giao_van_truc_tiep | Giao vận | R | – (12 giờ tới) | DIEU_PHOI_GV, TP_VAN_HANH, TRUONG_BEP, GIAM_DOC, ADMIN, TRUONG_CA, KINH_DOANH, TP_KINH_DOANH, KE_TOAN | `Logistics.LiveDeliveryQuery` |
| phan_anh_khach | Giao vận | R | tu_ngay, den_ngay, ma_kh, chi_chua_xu_ly | GIAM_DOC, TP_KINH_DOANH, TRUONG_BEP, TP_VAN_HANH, KE_TOAN, ADMIN | `Logistics.ComplaintsQuery` |
| hop_khay | Giao vận | R | tu_ngay, den_ngay | như trên | `Packaging.ContainerBalanceQuery` |
| cham_cong_ngay | Nhân sự | R | ngay | TRUONG_BO_PHAN, TRUONG_BEP, TP_VAN_HANH, TRUONG_CA, NHAN_SU, GIAM_DOC, ADMIN | `HR.DailyAttendanceQuery` |
| chua_vao_ca | Nhân sự | R | – | mọi người (theo phạm vi) | `HR.NotCheckedInQuery` |
| cong_thang | Nhân sự | R | thang (YYYY-MM) | mọi người (theo phạm vi) | `HR.MonthlyTimesheetQuery` |
| danh_muc | Danh mục | R | loai (khach_hang/diem_giao/mon_an/nguyen_lieu/nha_cung_cap), tu_khoa | GIAM_DOC, TP_KINH_DOANH, TRUONG_BEP, TP_VAN_HANH, KE_TOAN, ADMIN | `Common.LookupQuery` (không trả SĐT/MST/email) |
| kiem_tra_phieu | Phiếu | R | loai (DonHang/NhapKho/XuatKho/KiemKe), rows[] | theo ROLES_SUBMIT | `Vouchers.ValidateCommand` (dry-run) |
| tao_phieu | Phiếu | W | loai, rows[] | theo ROLES_SUBMIT | `Vouchers.CreatePendingCommand` → ApprovalRequest(`AGENT_WRITE`/`VOUCHER`), trả `ma_xac_nhan` (XN-…) |
| xac_nhan_phieu | Phiếu | W | ma_xac_nhan | người gửi | `Vouchers.ConfirmCommand`: ghi sổ; dòng vượt ngưỡng chuyển chờ duyệt |
| huy_phieu | Phiếu | W | ma_xac_nhan | người gửi | `Vouchers.CancelCommand` |
| duyet_phieu | Phiếu | W | ma_xac_nhan, dong_y, ghi_chu | theo ROLES_APPROVE | `Approval.DecideCommand` |
| ds_cho_duyet | Phiếu | R | – | mọi người | `Approval.MyPendingQuery` |
| doi_mon_khoang | Sản xuất | R | tu_ngay, den_ngay | GIAM_DOC, TP_KINH_DOANH, TRUONG_BEP, TP_VAN_HANH, KE_TOAN, ADMIN | `Menu.ChangeRequestsQuery` |
| xu_ly_appsheet_kho | Hệ thống | W | – | ADMIN | `Integration.ProcessAppSheetQueueCommand` (InboundMessage kênh APPSHEET) |

**Cột của phiếu cũ:**
| Phiếu | Cột |
|---|---|
| DonHang | Ma_phieu, Ngay_giao, Ca, Ma_KH, Ma_diem_giao, Ma_mon, So_suat, Don_gia_ban, Thanh_tien, Gio_chot_giao, Loai_dong, Ma_phieu_goc, Trang_thai, Nguoi_gui, Nguoi_duyet, Thoi_diem_ghi, Link_file_goc, Ghi_chu |
| NhapKho | Ma_phieu, Ngay_nhap, Gio_nhap, Ma_lo, Ma_NL, Ma_NCC, So_chung_tu, So_luong, DVT, Don_gia, Thanh_tien, NSX, HSD, Nhiet_do_nhan_C, Ket_qua_kiem_tra, SL_tra_lai, Ly_do_tra, Kho, Vi_tri, Nguoi_nhan, Link_chung_tu, Loai_dong, Ma_phieu_goc, … |
| XuatKho | Ma_phieu, Ngay_xuat, Gio_xuat, Ma_lo, Ma_NL, So_luong, DVT, Kho, Muc_dich, Ma_me_nau, Ca, Bo_phan_nhan, Nguoi_xuat, Nguoi_nhan, Loai_dong, Ma_phieu_goc, … |
| KiemKe | Ma_phieu, Ngay_kiem, Dot_kiem_ke, Kho, Ma_lo, Ma_NL, DVT, SL_so_sach, SL_thuc_te, Chenh_lech, Ty_le_chenh_lech_pct, Ly_do, Nguoi_kiem, Nguoi_chung_kien, Trang_thai_xu_ly, Nguoi_duyet, Loai_dong, … |

Adapter phải nhận các tên cột tiếng Việt này và chuyển sang DTO của Application. Bảng ánh xạ cột chi tiết nằm trong MIGRATION_SHEETS_TO_DBO.md.

**Hermes hiện có 26 công cụ chỉ đọc**, gọi đúng các hàm R ở trên. Khi chuyển sang MCP thì giữ nguyên tên và tham số.
