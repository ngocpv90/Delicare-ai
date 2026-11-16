# MIGRATION – Google Sheets (42 sổ) → 124 bảng `dbo` (TÙY CHỌN)

> **Sheets chỉ chứa dữ liệu demo (D-16).** Không có cắt chuyển hay chạy song song. Bảng ánh xạ dưới đây dùng cho: (1) **T49**: file mẫu Excel nạp danh mục ban đầu, cột đặt gần với sổ `dm.*` để anh chuẩn bị dữ liệu thật quen tay; (2) nạp dữ liệu demo vào staging cho UAT nếu anh muốn. Phần "Lộ trình chuyển" cuối file **không áp dụng**.

Quyết định: **124 bảng `dbo` là chuẩn** (D-01). Lược đồ 42 bảng `dm/vh/ht/bc` của bộ bàn giao F1 **không dùng** vì vi phạm luật DB. Bộ F1 chỉ dùng làm nguồn đọc dữ liệu.

**Nguồn dữ liệu:**
- Bản sao lưu 21h hằng ngày trên Drive: `99_LUU_TRU/SaoLuu/<ngày>` (xlsx/csv).
- Mô tả cột từng sổ: `BANG_ANH_XA.md` trên Drive (bộ F1).

**Chuyển kiểu khi nạp:**
- Bỏ dấu `'` ở đầu chuỗi giờ.
- `dd/MM/yyyy` → date.
- Dấu phẩy thập phân → dấu chấm.
- Ô trống → NULL.
- Mã nghiệp vụ (Ma_KH, Ma_NL…) → tra ra Id (GUID) qua cột `Code` / `...Number` của bảng đích.

**Idempotent:** khóa tự nhiên là mã chứng từ hoặc mã danh mục. Nạp lại không tạo dòng trùng. Ghi một dòng `IntegrationLog` cho mỗi lô nạp (`OperationCode` = `IMPORT_<SHEET>`, `CorrelationId` = ngày sao lưu).

## Bảng ánh xạ cấp sổ (PROPOSED – T60 sẽ chốt tới cấp cột)

| Sổ Sheets (F1) | Bảng `dbo` đích | Ghi chú |
|---|---|---|
| dm.KhachHang | Customer (+ Contract) | So_hop_dong / Ngay_hieu_luc / Ngay_het_han → Contract. Nguong_that_thoat_hop_pct → **GAP-PKG-01** |
| dm.DiemGiao | CustomerDeliveryLocation | Gio_chot_giao → DeliveryWindowTo (PROPOSED). So_suat_trung_binh → tính lại từ OrderLine |
| dm.BangGia_HopDong | ContractMeal / PricePolicie | |
| dm.DayChuyen | ProductionLine | Suat_moi_me → CapacityMealPerBatch, Phut_moi_me → MinutesPerBatch |
| dm.DinhMuc | Recipe + RecipeLine | Dinh_muc_g_tren_suat → Quantity (UnitId = gram), Hao_hut_che_bien_pct → WastePercent / ExpectedWasteRate |
| dm.MonAn | Dishe (+ DishCategorie) | |
| dm.NCC | Supplier | điểm giá / chất lượng / đúng hẹn → GAP-SUP-01 nếu không có cột |
| dm.NguyenLieu | Ingredient + InventoryPolicie + IngredientCategorie + SupplierMaterial(IsPreferred) | Ton_toi_thieu / toi_da → InventoryPolicie |
| dm.NhanVien | Employee (+ OrganizationUnit, Position) | |
| dm.ViTri_Bep | KitchenWorkstation | Latitude / Longitude / RadiusMeters |
| dm.Xe | Vehicle | |
| ht.NguoiDung | User + UserExternalIdentity (TELEGRAM) + UserRole / UserGroup | Vai_tro → UserGroup |
| ht.DanhSach_Bot | BotConfiguration (+ IntegrationChannel) | token → SecretReference (không lưu token) |
| ht.NhatKy | IntegrationLog | |
| ht.PhieuChoXacNhan | ApprovalRequest + ApprovalItem | Ma_xac_nhan → RequestNo |
| ht.AppSheet_Kho | InboundMessage (kênh APPSHEET) | |
| ht.TaiHeThong | không nạp (chỉ số kỹ thuật) | |
| vh.DonHang | Order + OrderLine | Loai_dong DIEU_CHINH / HUY → xử lý như thay đổi đã duyệt + AuditChange |
| vh.NhapKho | GoodsReceipt + GoodsReceiptLine + MaterialLot + InventoryTransaction(IN) + QCInspection + TemperatureLog(RECEIVING) | |
| vh.XuatKho | MaterialIssue + MaterialIssueLine + InventoryTransaction(OUT) | |
| vh.KiemKe | StockCount + StockCountLine + InventoryTransaction(ADJUSTMENT) | |
| vh.PO | PurchaseOrder + PurchaseOrderLine | |
| vh.LoiNCC | SupplierIssue | |
| vh.KeHoach | ProductionPlan + ProductionPlanLine + ProductionBatche(planned) | Phien_ban → GAP-PRD-02 |
| vh.DinhMucKhoa | Recipe.SnapshotJson / LockedAt / LockedBy | |
| vh.SoMeNau | ProductionBatche + TemperatureLog(CORE) + FoodSafetyChecklistResult(SAMPLE_RETAINED) | |
| vh.ChecklistATTP | FoodSafetyChecklistResult | |
| vh.HaoHutSuat | MealWasteRecord | |
| vh.DoiMon | MenuChangeRequest | |
| vh.ThucDon | Menu + MenuLine + MenuAssignment | |
| vh.KeHoachGiao | DeliveryRoute + DeliveryStop (planned) | |
| vh.ChuyenXe | DeliveryRoute + DeliveryStop + DeliveryExecution + ProofOfDeliverie | |
| vh.SuKienGiao | DeliveryEvent (+ TemperatureLog DELIVERY) | |
| vh.PhanAnh_KH | CustomerComplaint | |
| vh.NhatKyPhanAnh | ComplaintHistory | |
| vh.XuatThuHoi | PackagingLedger (OUT / RETURN / LOSS) | |
| vh.NhanHopVe | ContainerReturn + ReturnInspection + PackagingLedger | |
| vh.NhatKyVeSinh | CleaningBatche + CleaningChecklistResult (6 bước) | |
| vh.ChamCong | AttendanceEvent | |
| vh.PhanCa | ShiftAssignment | |
| vh.DonNghi_DoiCa | LeaveRequest / ShiftSwapRequest | |
| bc.KPI_Ngay | KpiDailySnapshot (+ KpiMetricDefinition) | |

**Lộ trình chuyển** (không dừng vận hành):
1. Nạp danh mục.
2. Đồng bộ một chiều hằng đêm (Sheets → SQL).
3. Báo cáo đọc từ SQL, so khớp với bot trong 2 tuần.
4. Chuyển dần luồng ghi theo thứ tự: danh mục → kho → đơn → sản xuất → giao vận → nhân sự. Mỗi luồng chuyển xong thì gateway cũ gọi API .NET thay vì ghi Sheets.
5. Đổi URL Hermes và webhook bot sang .NET. Sheets chuyển sang chỉ đọc.
