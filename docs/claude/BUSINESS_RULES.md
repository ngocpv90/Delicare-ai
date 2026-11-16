# BUSINESS_RULES – Quy tắc nghiệp vụ phải cài vào lõi

Nguồn: bản demo Apps Script v2.11 (do anh thiết kế; `HUONG_DAN_BAN_GIAO_NET.md`), BRD và mockup. Khi các nguồn mâu thuẫn thì theo bản demo Apps Script (D-10), trừ khi DECISIONS.md ghi khác. Các quy tắc này vẫn mang nhãn PROPOSED cho tới khi người dùng xác nhận qua UAT.

Mọi giá trị số đặt trong `appsettings.json` → section `BusinessRules`, đọc qua `IOptions<BusinessRulesOptions>`. Đổi ngưỡng **không cần sửa code**. Lưu cấu hình trong DB là GAP-CFG-01 (chưa có bảng phù hợp).

```json
"BusinessRules": {
  "TimeZone": "Asia/Ho_Chi_Minh",
  "OrderCutoffHourPreviousDay": 20,
  "OrderLockJobTime": "20:00",
  "MrpJobTime": "20:30",
  "PurchaseProposalJobTime": "21:00",
  "StockCountVariancePctNeedApproval": 2.0,
  "PortionDeviationPctNeedApproval": 30.0,
  "TransportMinutesBeforeCutoff": 60,
  "CoreTempMinC": 70.0,
  "DeliveryTempMinC": 60.0,
  "KpiOnTimeDeliveryTargetPct": 95.0,
  "KpiIngredientToRevenueMaxPct": 55.0,
  "KpiContainerLossMaxPct": 3.0,
  "ComplaintSlaHours": { "HIGH": 4, "MEDIUM": 24, "LOW": 72 },
  "ChecklistOffsetsMinutes": { "PREP": -150, "PREP_AREA_CLEAN": -90, "BLAST_CHILL": -20, "KITCHEN_CLEAN": 90 },
  "ExpiringLotDays": 7,
  "ShortageForecastDays": 3
}
```

## 0. Buổi ăn và lựa chọn món (theo file vận hành thật – BUSINESS_DOCS.md)
- Buổi ăn: **SÁNG, TRƯA, TỐI, ĐÊM**. Ánh xạ sang ca giao: SÁNG ↔ `SANG_6H`, TRƯA ↔ `TRUA`, TỐI ↔ `TOI`, ĐÊM ↔ `DEM_23H` (PROPOSED – D-19).
- Mỗi buổi có **Lựa chọn 1 / 2 / 3** (nhiều set món), mỗi lựa chọn có số suất dự kiến và thực tế. Lưu bằng `MenuLine` (lựa chọn = nhóm dòng; cột phân biệt xem DB_SCHEMA_124.md, thiếu thì GAP) và `OrderLine` theo `MenuId` / `MealType` / `ShiftId`.
- **FC% (food cost)** = giá vốn suất / giá bán chưa VAT, tính theo buổi / lựa chọn / ngày; cảnh báo khi vượt mục tiêu (KPI nguyên liệu / doanh thu ≤ 55%).
- Hao hụt hai tầng: % hao hụt **sơ chế** theo nguyên liệu (`RecipeLine.WastePercent`) × % hao hụt theo **phương pháp chế biến** (`ExpectedWasteRate`). Lượng trước sơ chế = định lượng tinh / (1 − hao hụt sơ chế) / (1 − hao hụt chế biến) – PROPOSED, đối chiếu công thức trong `Phieu_Truoc sx.xlsx`.

## 1. Ca làm việc / ca giao
`TRUA` (trưa) · `TOI` (tối) · `DEM_23H` (đêm 23h) · `SANG_6H` (sáng 6h). Seed vào bảng `Shift` (`Code`, `Name`, `StartTime`, `EndTime`) bằng script `DB/seed/`. Mockup cũ dùng CA1–CA4; đổi theo bộ mã này.

## 2. Đơn hàng
- Đơn cho ngày giao D phải chốt trước **20:00 ngày D-1**. Job 20:00 chuyển các đơn `Confirmed` của ngày D+1 sang `Locked`.
- Thêm hay sửa đơn sau giờ chốt: tạo `ApprovalRequest` (BusinessType=`ORDER_CHANGE_AFTER_CUTOFF`). Chỉ ghi khi đã được duyệt.
- Số suất lệch > **30%** so với trung bình của điểm giao (tính trên lịch sử `OrderLine` của `DeliveryLocationId`, cửa sổ 4 tuần cùng thứ – PROPOSED) cũng cần duyệt.
- Trạng thái: theo `STATUS_CATALOG.md` (Order: Draft → Submitted → Confirmed → Locked → InProduction → Delivered → Reconciled; Cancelled).

## 3. Định mức và MRP
- Định mức tính theo **gram/suất** cộng **% hao hụt chế biến** (`RecipeLine`). Nhu cầu = Σ(số suất × g/suất × (1 + hao hụt%)), quy đổi đơn vị qua `UomConversion` sang `UnitMeasure` (PO chốt dùng `UnitId` → `UnitMeasure`).
- Kế hoạch đã duyệt thì **khóa định mức**. **Không tạo RecipeVersion.** Dùng cột có sẵn của `Recipe`: `SnapshotJson` (ảnh chụp các dòng định mức), `LockedAt`, `LockedBy`, `VersionNo` / `VersionCode`, `EffectiveFrom` / `EffectiveTo`.
- Thiếu hụt = Nhu cầu − (tồn khả dụng − đã giữ chỗ `MaterialReservation`). Thiếu thì sinh `PurchaseRequest` nháp lúc 21:00, gợi ý NCC từ `SupplierMaterial` (ưu tiên, giá, lead time).
- Đổi món hoặc đổi nguyên liệu ảnh hưởng tới khách thì tạo `MenuChangeRequest` và **xin từng khách chấp thuận** (G1). NCC chính không giao được thì đặt PO bù cho NCC dự phòng (G2).

## 4. Mua hàng – nhập kho – QC
- Kết quả kiểm nhận lô: `DAT` (đạt), `DAT_MOT_PHAN` (đạt một phần), `KHONG_DAT` (không đạt). Lưu ở `QCInspection.Result`, `GoodsReceiptLine.QualityStatus` (+ `AcceptedQuantity` / `RejectedQuantity`) và `MaterialLot.QcStatus` (cột chuẩn, xem DEPRECATED_COLUMNS.md). Nếu BRD dùng PASS/HOLD/REJECT thì ánh xạ DAT=PASS, DAT_MOT_PHAN=HOLD (phần đạt được nhập), KHONG_DAT=REJECT – PROPOSED.
- Lô `KHONG_DAT`: **chặn xuất cho sản xuất**, chỉ được trả NCC hoặc hủy, FEFO bỏ qua lô này, ghi `SupplierIssue`. Lô `DAT_MOT_PHAN`: xuất được phần đạt, cũng ghi `SupplierIssue`.
- Kiểm nhiệt độ khi nhận so với ngưỡng bảo quản của nguyên liệu. Ghi `TemperatureLog` (MeasurementType=`RECEIVING`).

## 5. Kho (sổ chỉ ghi thêm)
- Tồn lô = nhập − trả lại − xuất + điều chỉnh kiểm kê đã duyệt. Tất cả đi qua `InventoryTransaction` (`QuantityIn` / `QuantityOut`, `ReferenceType` / `ReferenceId`). `InventoryBalance` chỉ là bảng tổng hợp, tính lại từ sổ.
- Xuất theo **FEFO** (mặc định) hoặc **FIFO** theo `InventoryPolicie.UseFefo` của nguyên liệu (D-24; sơ đồ vận hành của nhà máy ghi FIFO). **Chặn** xuất lô đã hết hạn. Còn lô cùng nguyên liệu hạn gần hơn thì chỉ **cảnh báo**, không chặn.
- Kiểm kê: chênh lệch > **2%** thì cần duyệt rồi mới ghi điều chỉnh.
- Sửa hoặc hủy bút toán: ghi bút toán đảo (`TransactionType` = `REVERSAL`, hoặc ADJUSTMENT kèm `ReferenceId` trỏ về dòng gốc, có `Reason`). Không Update/Delete.

## 6. Sản xuất
- Lập kế hoạch **ngược từ giờ xuất bếp**: giờ xuất bếp = giờ chốt giao của điểm giao − 60 phút vận chuyển. Số mẻ tính theo năng lực dây chuyền (`ProductionLine.CapacityMealPerBatch`, `MinutesPerBatch`).
- Mỗi lần lập lại kế hoạch thì tăng phiên bản. `ProductionPlan` không có cột phiên bản → GAP-PRD-02. Tạm thời giữ phiên bản trong `Note` dạng `v=<n>` và trong `ApprovalRequest.PayloadJson` lúc duyệt.
- Nhiệt độ lõi khi nấu xong ≥ **70°C**, ghi `TemperatureLog` (MeasurementType=`CORE`, `ProductionBatchId`). Không đạt thì chặn đóng gói và yêu cầu hành động khắc phục.
- **Mỗi mẻ phải lưu mẫu thức ăn.** `ProductionBatche` không có cột riêng. Ghi bằng `FoodSafetyChecklistResult` (ChecklistItem mã `SAMPLE_RETAINED`, `ProductionBatchId`, `CheckedBy`, `CheckedAt`, `EvidenceUrl`). Seed checklist item này (PROPOSED).
- Chia suất, nấu thêm, suất thừa ghi vào `MealWasteRecord` / `ProductionOutput`.
- Checklist ATTP theo ca, hạn tính từ giờ xuất bếp: sơ chế −150′, vệ sinh khu sơ chế −90′, xả nguội −20′, vệ sinh bếp +90′.

## 7. Giao vận
- Nhiệt độ khi giao ≥ **60°C** (`DeliveryEvent` + `TemperatureLog` MeasurementType=`DELIVERY`).
- Trễ, thiếu hay thừa suất ghi theo từng điểm dừng (`DeliveryStop`). Có ảnh xác nhận (`ProofOfDeliverie`).
- Khách báo thiếu suất → **bếp** nấu bù → **giao vận** giao bù (luồng liên phòng, xem AGENTS.md).

## 8. Hộp / khay và vệ sinh
- `PackagingLedger` là sổ chỉ ghi thêm: OUT (xuất theo chuyến), RETURN (thu về), LOSS (thất thoát), DAMAGED.
- Tỷ lệ thất thoát ≤ **3%**, hoặc theo ngưỡng riêng của khách (cột ngưỡng trên `Customer` / `Contract` nếu có; không có thì GAP-PKG-01).
- Thất thoát vượt ngưỡng: báo **khách** rồi chuyển **kế toán** tính phí.
- Lô vệ sinh 6 bước (`CleaningBatche` + `CleaningChecklistResult`): 1 nhận/quét QR → 2 phân loại → 3 rửa sơ → 4 rửa máy → 5 khử khuẩn → 6 kiểm tra và nhập kho sạch.

## 8b. Điểm phát suất / căng tin tại khách (mô hình a – D-21)
Điểm bán hiện tại = điểm phục vụ suất theo hợp đồng tại khách hàng. Nhân viên điểm nhận hàng (POD), ghi suất thực tế phục vụ / thừa / thiếu → `OrderReconciliation`; dữ liệu này là cơ sở dự báo nhu cầu kỳ sau (bước 1) và đối soát doanh thu. Không có thu tiền / POS trong R1.

## 9. Phản ánh khách
Mức độ HIGH / MEDIUM / LOW, hạn xử lý 4 / 24 / 72 giờ (`CustomerComplaint`, lịch sử ở `ComplaintHistory`). Quá hạn thì báo lên cấp trên.

## 10. Nhân sự
Chấm công hợp lệ khi nằm trong bán kính bếp (GPS), hoặc quét QR tại bếp, hoặc từ máy chấm công, hoặc chấm hộ (phải ghi rõ hình thức). Lưu ở `AttendanceEvent` (`IsValid`, `SourceChannel` = GPS / QR / MACHINE / PROXY, `DistanceMeters`). Tọa độ và bán kính bếp lấy từ `KitchenWorkstation.Latitude` / `Longitude` / `RadiusMeters`. Có đơn nghỉ (`LeaveRequest`) và đổi ca (`ShiftSwapRequest`).

## 11. Mã chứng từ (sinh theo ngày, không trùng, an toàn khi chạy đồng thời)
| Loại | Mẫu | Ghi vào cột |
|---|---|---|
| Phiếu (đơn / nhập / xuất / kiểm kê) | `PH-YYMMDD-NNNN` | OrderNumber / ReceiptNo / IssueNo / CountNo… |
| Xác nhận | `XN-YYMMDD-NNN` | ApprovalRequest.RequestNo |
| Phản ánh | `PA-YYMMDD-NNNN` | CustomerComplaint |
| PO NCC | `PO-YYMMDD-NNNN` | PurchaseOrder |
| Chấm công | `CC-YYMMDD-NNNN` | AttendanceEvent |
| Đơn nghỉ | `DN-YYMMDD-NNNN` | LeaveRequest |
| Lô nhập | `NL-YYMMDD-<NCC>-NN` | MaterialLot |
| Mẻ nấu | `ME-YYMMDD-<CA>-NN` | ProductionBatche |
| Chuyến xe | `CX-YYMMDD-<XE>-NN` | DeliveryRoute |
| Kế hoạch | `KH-YYMMDD` (+ phiên bản) | ProductionPlan |
| Lô vệ sinh | `VS-YYMMDD-<CA>` | CleaningBatche |

Cách cài: `IDocumentNumberService.Next(kind, date, suffix)`. Dùng `sp_getapplock` theo khóa `kind+date+suffix`, đọc MAX số hiện có trong cột đích, rồi +1. **Không tạo bảng đếm và không tạo SEQUENCE** (luật DB).

## 12. Mục tiêu KPI (seed vào `KpiMetricDefinition`)
| Mã | Chỉ số | Mục tiêu |
|---|---|---|
| ON_TIME_DELIVERY_PCT | Giao đúng giờ | ≥ 95% |
| INGREDIENT_COST_TO_REVENUE_PCT | Nguyên liệu / doanh thu | ≤ 55% |
| CONTAINER_LOSS_PCT | Thất thoát hộp | ≤ 3% |
| COMPLAINT_SLA_MET_PCT | Phản ánh xử lý đúng hạn | PROPOSED ≥ 95% |
| CORE_TEMP_PASS_PCT | Mẻ đạt nhiệt độ lõi | PROPOSED 100% |
| SAMPLE_RETAINED_PCT | Mẻ đã lưu mẫu | PROPOSED 100% |

Ảnh chụp KPI theo ngày ghi vào `KpiDailySnapshot` (job 22:00), theo tháng ghi vào `KpiMonthlySnapshot`.
