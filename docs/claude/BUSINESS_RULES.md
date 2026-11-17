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
- **Bản Pilot đã cài (T13, PROPOSED – DECISIONS GAP-ORD-01…03):** 1 đơn = 1 điểm giao; sau giờ chốt chặn tạo / sửa số suất / gửi / xác nhận / hủy đơn Confirmed, vẫn cho Lock và hủy Draft / Submitted; nút `Order/LockByMealDate` thay job 20:00 (chỉ chạy khi đã tới giờ chốt); lệch suất = sửa số suất đơn Confirmed, dòng bất kỳ lệch > 30% so với số đã xác nhận → `ApprovalRequest` `ORDER_DEVIATION`. `ORDER_CHANGE_AFTER_CUTOFF` và so lịch sử 4 tuần → sau Pilot. Ca mặc định theo loại suất: `BusinessRules:MealTypeDefaultShift` (SANG → SANG_6H, TRUA → TRUA, TOI → TOI, DEM → DEM_23H).
- **BE2.2 (PROPOSED – D-51):** sau giờ chốt thêm / sửa đơn qua duyệt `ORDER_CHANGE_AFTER_CUTOFF` (`Order/RequestChange`; thêm đơn = `Order/Create` + `afterCutoffReason`), duyệt xong đơn → Đã chốt; quyền mục `#/wf-inbox?type=crm-order`. Xác nhận đơn lệch dự báo > `ForecastVariancePctWarning` (10%) chỉ cảnh báo + ghi Note `[LECH_DU_BAO]`. Số gợi ý mẫu tạo đơn = dự báo Đã công bố mới nhất. Hợp đồng: Nháp → Chờ duyệt (CONTRACT, 1 cấp, mục `crm-contract`) → Hiệu lực → Chấm dứt (chặn khi còn đơn chưa giao sau ngày chấm dứt); số `HD-YYMMDD-NNN`. Đổi món đã duyệt (`MENU_CHANGE`) áp vào kế hoạch Nháp và mọi lần tính lại (MRP / sinh kế hoạch / chia mẻ).

## 3. Định mức và MRP
- Định mức tính theo **gram/suất** cộng **% hao hụt chế biến** (`RecipeLine`). Nhu cầu = Σ(số suất × g/suất × (1 + hao hụt%)), quy đổi đơn vị qua `UomConversion` sang `UnitMeasure` (PO chốt dùng `UnitId` → `UnitMeasure`).
- Kế hoạch đã duyệt thì **khóa định mức**. **Không tạo RecipeVersion.** Dùng cột có sẵn của `Recipe`: `SnapshotJson` (ảnh chụp các dòng định mức), `LockedAt`, `LockedBy`, `VersionNo` / `VersionCode`, `EffectiveFrom` / `EffectiveTo`.
- Thiếu hụt = Nhu cầu − (tồn khả dụng − đã giữ chỗ `MaterialReservation`). Thiếu thì sinh `PurchaseRequest` nháp lúc 21:00, gợi ý NCC từ `SupplierMaterial` (ưu tiên, giá, lead time).
- **Anh chốt 2026-11-16 (T14.1):** công thức hao hụt giữ như trên (số lượng × (1 + hao hụt %)); **chỉ đơn Đã chốt (Locked)** được tính, đơn Confirmed chỉ cảnh báo; kế hoạch nháp `KH-` theo **ngày**. Thực đơn có ≥ 2 lựa chọn cho một loại suất: số suất chia đều cho các lựa chọn rồi **cộng dự phòng `BusinessRules:MrpOptionBufferPct` = 15% mỗi lựa chọn** (0 = tắt; ví dụ 100 suất, 2 lựa chọn → mỗi lựa chọn 57,5 suất). Đề nghị mua của ngày đã gửi duyệt / duyệt / lập PO → không tính lại, dùng **tính bổ sung** (`MaterialRequirement/CalculateTopUp`): đề nghị mua bổ sung Nháp chỉ cho phần thiếu thêm, phần thừa chỉ cảnh báo.
- Đổi món hoặc đổi nguyên liệu ảnh hưởng tới khách thì tạo `MenuChangeRequest` và **xin từng khách chấp thuận** (G1). NCC chính không giao được thì đặt PO bù cho NCC dự phòng (G2).

## 4. Mua hàng – nhập kho – QC
- Kết quả kiểm nhận lô: `DAT` (đạt), `DAT_MOT_PHAN` (đạt một phần), `KHONG_DAT` (không đạt). Lưu ở `QCInspection.Result`, `GoodsReceiptLine.QualityStatus` (+ `AcceptedQuantity` / `RejectedQuantity`) và `MaterialLot.QcStatus` (cột chuẩn, xem DEPRECATED_COLUMNS.md). Nếu BRD dùng PASS/HOLD/REJECT thì ánh xạ DAT=PASS, DAT_MOT_PHAN=HOLD (phần đạt được nhập), KHONG_DAT=REJECT – PROPOSED.
- Lô `KHONG_DAT`: **chặn xuất cho sản xuất**, chỉ được trả NCC hoặc hủy, FEFO bỏ qua lô này, ghi `SupplierIssue`. Lô `DAT_MOT_PHAN`: xuất được phần đạt, cũng ghi `SupplierIssue`.
- Kiểm nhiệt độ khi nhận so với ngưỡng bảo quản của nguyên liệu. Ghi `TemperatureLog` (MeasurementType=`RECEIVING`).
- **Bản Pilot đã cài (T15 + T15.1 – anh chốt 2026-11-16, DECISIONS mục "Anh chốt 2026-11-16 (T15 mua hàng → T15.1)", GAP-PUR-01…10, D-11):**
  - **Duyệt PO bắt buộc**, 1 cấp: `BusinessRules:PurchaseOrderAutoApproveMaxAmount` = 0 (mọi PO phải duyệt). PO **≥ `PurchaseOrderDirectorApprovalAmount` = 50 000 000** đ chỉ **Giám đốc** duyệt (mục quyền `#/wf-inbox?type=scm-po-director`, seed nhóm CEO); dưới ngưỡng duyệt trên `#/wf-inbox?type=scm-po`. PO lớn không tự duyệt.
  - Nhận hàng khi PO **Đã duyệt** / Đã gửi NCC / Nhận một phần; nhận vượt tối đa `GoodsReceiptOverReceivePct` = **10%** mỗi dòng.
  - **Nhiệt độ nhận 3 vùng** (`BusinessRules:ReceivingTemp` theo `Ingredient.StorageType`):

    | Kiểu | Nhận | Nhận có điều kiện | Loại |
    |---|---|---|---|
    | CHILLED | ≤ 5 °C | 5 < t ≤ 8 °C | > 8 °C |
    | FROZEN | ≤ −18 °C | −18 < t ≤ −15 °C + xác nhận "không có dấu hiệu rã đông" | > −15 °C |
    | DRY / AMBIENT | không kiểm | – | – |

    Có điều kiện → bắt buộc lý do, lô "ưu tiên dùng trước" (`[UU_TIEN_DUNG_TRUOC]` trong Note – GAP-PUR-10), luôn ghi `SupplierIssue` TEMPERATURE. Vùng loại → chỉ KHONG_DAT (có lý do cũng không nhận). Hàng mát / đông lạnh chưa đo → không kết luận đạt được.
  - HSD trước ngày nhận → chỉ KHONG_DAT; ghi sổ **chỉ nhập phần đạt**, lô KHONG_DAT = Tạm giữ không có tồn; `SupplierIssue` hạn xử lý `SupplierIssueDueDays` = 2 ngày.
  - **VAT (phương án A):** chỉ tính khi in Đơn đặt hàng / xem chi tiết PO theo `BusinessRules:PurchaseVat` (`DefaultPct` 8%, `ByIngredientCategoryCode` theo mã nhóm nguyên liệu – PROPOSED, đang trống); không lưu DB (cột VAT → GAP-PUR-09, R2).

## 5. Kho (sổ chỉ ghi thêm)
- Tồn lô = nhập − trả lại − xuất + điều chỉnh kiểm kê đã duyệt. Tất cả đi qua `InventoryTransaction` (`QuantityIn` / `QuantityOut`, `ReferenceType` / `ReferenceId`). `InventoryBalance` chỉ là bảng tổng hợp, tính lại từ sổ.
- Xuất theo **FEFO** (mặc định) hoặc **FIFO** theo `InventoryPolicie.UseFefo` của nguyên liệu (D-24; sơ đồ vận hành của nhà máy ghi FIFO). **Chặn** xuất lô đã hết hạn. Còn lô cùng nguyên liệu hạn gần hơn thì chỉ **cảnh báo**, không chặn.
- Kiểm kê: chênh lệch > **2%** thì cần duyệt rồi mới ghi điều chỉnh.
- **BE2.3 (PROPOSED – D-52):** kiểm kê lệch > 2% → `StockCount/Submit` → duyệt STOCK_COUNT 2 cấp (Quản lý kho `scm-stockcount` → Kế toán `scm-stockcount-l2`, spec 2.3) → `Post` (admin không ghi sổ thẳng nữa); điều chỉnh tồn tay → duyệt STOCK_ADJUSTMENT 1 cấp (`scm-adjustment`) rồi mới ghi bút toán; đề nghị mua (kể cả MRP lập) duyệt 2 cấp `scm-pr-l1` → `scm-pr-l2` (D-46) rồi mới lập PO. Mỗi cấp hạn 48 giờ (cờ quá hạn, không job); một người không duyệt 2 cấp của cùng yêu cầu (admin miễn). Số cấp theo `BusinessRules:ApprovalSteps`.
- Sửa hoặc hủy bút toán: ghi bút toán đảo (`TransactionType` = `REVERSAL`, hoặc ADJUSTMENT kèm `ReferenceId` trỏ về dòng gốc, có `Reason`). Không Update/Delete.
- **BE2.4 (PROPOSED – D-53 a, b, d):** phát hành kế hoạch **giữ chỗ** nguyên liệu (`MaterialReservation` theo lệnh × nguyên liệu, `ProductionReserveOnRelease`); `InventoryBalance.ReservedQuantity` = tổng giữ chỗ còn hiệu lực phân bổ FEFO lên lô xuất được (tính lại sau mỗi bút toán), khả dụng = tồn − giữ chỗ; xuất cho chính lệnh dùng phần đã giữ, xuất khác chỉ lấy phần chưa giữ; hủy / đóng lệnh giải phóng. MRP trừ **hàng sắp về** từ PO Đã duyệt / Đã gửi / Nhận một phần có ngày giao dự kiến ≤ ngày SX (trừ phần đã nhận; PO chưa có ngày giao không tính). **Lô hết hạn** xử lý bằng `MaterialLot/ProcessExpired` (thay job 00:15 – D-45): lô Khả dụng hạn < hôm nay → Hết hạn + WASTE toàn bộ tồn; lô tạm giữ bỏ qua.

## 6. Sản xuất
- Lập kế hoạch **ngược từ giờ xuất bếp**: giờ xuất bếp = giờ chốt giao của điểm giao − 60 phút vận chuyển. Số mẻ tính theo năng lực dây chuyền (`ProductionLine.CapacityMealPerBatch`, `MinutesPerBatch`).
- Mỗi lần lập lại kế hoạch thì tăng phiên bản. `ProductionPlan` không có cột phiên bản → GAP-PRD-02. Tạm thời giữ phiên bản trong `Note` dạng `v=<n>` và trong `ApprovalRequest.PayloadJson` lúc duyệt.
- Nhiệt độ lõi khi nấu xong ≥ **70°C**, ghi `TemperatureLog` (MeasurementType=`CORE`, `ProductionBatchId`). Không đạt thì chặn đóng gói và yêu cầu hành động khắc phục.
- **Mỗi mẻ phải lưu mẫu thức ăn.** `ProductionBatche` không có cột riêng. Ghi bằng `FoodSafetyChecklistResult` (ChecklistItem mã `SAMPLE_RETAINED`, `ProductionBatchId`, `CheckedBy`, `CheckedAt`, `EvidenceUrl`). Seed checklist item này (PROPOSED).
- Chia suất, nấu thêm, suất thừa ghi vào `MealWasteRecord` / `ProductionOutput`.
- Checklist ATTP theo ca, hạn tính từ giờ xuất bếp: sơ chế −150′, vệ sinh khu sơ chế −90′, xả nguội −20′, vệ sinh bếp +90′.
- **Bản Pilot đã cài (T17, PROPOSED – DECISIONS GAP-PRD-01, 03…08):** kế hoạch KH- theo ngày (MRP) → `ProductionPlan/Approve` → `Release` gán 1 ca (`BusinessRules:ProductionDefaultShiftCode` = TRUA), lịch ngược: giờ xuất bếp = giờ giao sớm nhất của điểm giao (`DeliveryWindowFrom`, không có → giờ bắt đầu ca) − `TransportMinutesBeforeCutoff` 60′, mẻ cuối mỗi món xong trước xuất bếp `ProductionPackingMinutes` = 30′, mỗi mẻ `MinutesPerBatch` (trống → `ProductionDefaultMinutesPerBatch` = 60′), số mẻ theo `CapacityMealPerBatch` của dây chuyền chọn; mã `ME-YYMMDD-<CA>-NN`. Nhiệt độ lõi: lần đo gần nhất < `CoreTempMinC` (70 °C) hoặc chưa đo → chặn đạt QC và chặn đóng gói; dưới ngưỡng bắt buộc hành động khắc phục. Lưu mẫu bắt buộc trước khi đạt QC (`ProductionRequireSampleBeforeRelease` = true; seed `DB/seed/FoodSafetyChecklist_seed.sql`). Công đoạn theo tổ → sau Pilot.
- **Anh chốt 2026-11-16 (D-42):** lưu mẫu bắt buộc **trước khi giao**, không phải trước khi mẻ đạt QC → `ProductionRequireSampleBeforeRelease` = false (đạt QC chỉ cảnh báo), chặn ở `DeliveryRoute/Dispatch` (T19).
- **Anh chốt 2026-11-16 (T17 → T17.1, DECISIONS "Anh chốt 2026-11-16 (T17)"):** Pilot chạy **4 ca / ngày** (SANG_6H / TRUA / TOI / DEM_23H); kế hoạch MRP vẫn 1 `KH-` / ngày; `Release` tách theo ca của đơn Đã chốt (`OrderLine.ShiftId`; dòng chưa gán ca → `ProductionDefaultShiftCode`): mỗi ca lệnh SX `PH-` theo món + mẻ `ME-YYMMDD-<CA>-NN`, lịch ngược riêng (giờ giao sớm nhất của điểm giao thuộc ca − 60′, không có → giờ bắt đầu ca − 60′; đóng gói 30′; các món song song). Có thể phát hành từng ca (`ShiftId`); kế hoạch Đã phát hành khi mọi ca có nhu cầu đã phát hành (PROPOSED). **Chia mẻ đều:** N = ⌈phần ÷ năng lực⌉, các mẻ chênh tối đa 1 phần (250 / 100 → 84, 83, 83). Duyệt kế hoạch 1 cấp trực tiếp; phát hành → đơn Đang sản xuất; lệnh SX mã `PH-`. Nhiệt độ lõi 70 °C giữ nguyên.
- **BE2.1 (PROPOSED – GAP-PCK-01, D-50):** đóng gói theo **lệnh đóng gói** `PH-` sinh từ mẻ Đạt QC (`PackingOrder/GenerateFromBatch`: số phần đạt chia cho đơn Đã chốt / Đang SX của ngày + ca theo tỉ lệ suất món, phần nguyên theo phần dư lớn nhất) hoặc lập tay (Σ ≤ số đạt); kết quả: đóng gói + hỏng + loại = kế hoạch, hỏng / loại → `MealWasteRecord` `PACKING_DAMAGE`; nhiệt độ lõi gần nhất ≥ 70 °C kiểm lại ở mọi bước; mẻ chia hết + mọi lệnh xong → mẻ Đã đóng gói. Mẻ chưa có lệnh đóng gói vẫn `ProductionBatch/Pack` trực tiếp. `ProductionPlan/Generate`: kế hoạch Nháp 1 `KH-` / ngày từ đơn Đã xác nhận / Đã chốt (giữ GAP-MRP-01). Hao hụt chung `MealWasteRecord/Record` (loại theo `ReferenceLists:WasteType`). Nhiệt độ tự do `TemperatureLog/Record` (ngưỡng khu kho; `HOT_HOLDING` ≥ `DeliveryTempMinC`; CORE / DELIVERY / RECEIVING chỉ qua nghiệp vụ – GAP-QA-02).
- **BE2.4 (PROPOSED – D-53 c, e–h):** **công đoạn theo tổ** Sơ chế → Nấu → Chia suất (checklist `PRD_STAGE`, `ProductionBatch/StartStage` / `FinishStage`, tổ = OrganizationUnit TEAM mặc định `ProductionStageTeams`, người làm = Employee): không nhảy công đoạn; bắt đầu Nấu = bắt đầu mẻ; Chia suất chỉ khi mẻ Đạt QC (≥ 70 °C); mẻ ghi công đoạn thì nấu xong cần Nấu xong, đóng gói cần Chia suất xong. **Hủy lệnh SX** chỉ khi Phát hành, chưa nấu, chưa xuất kho (mẻ → Hủy, giải phóng giữ chỗ). **Đóng lệnh** (Hoàn thành → Đóng) sinh chênh lệch tiêu hao (chuẩn = định lượng × phần nấu được, cờ vượt `ConsumptionVariancePctWarning` 5%) + giá thành thực tế (NVL theo đơn giá lô; nhân công `ProductionLaborCostPerHour` × giờ công đoạn hoặc `RecipeLaborCostPerServing` × phần; chung `RecipeOverheadCostPerServing` × phần; hao hụt chỉ phân tích). **Giá vốn suất** (`MealCost/Calculate`, đơn Đã giao / Đã đối soát) = giá thành lệnh SX phân bổ theo phần món của ngày / ca ÷ suất nhận.

## 7. Giao vận
- Nhiệt độ khi giao ≥ **60°C** (`DeliveryEvent` + `TemperatureLog` MeasurementType=`DELIVERY`).
- Trễ, thiếu hay thừa suất ghi theo từng điểm dừng (`DeliveryStop`). Có ảnh xác nhận (`ProofOfDeliverie`).
- Khách báo thiếu suất → **bếp** nấu bù → **giao vận** giao bù (luồng liên phòng, xem AGENTS.md).
- **Bản Pilot đã cài (T19, PROPOSED – DECISIONS GAP-DLV-01…07, D-42):** `DeliveryRoute/Create` lập chuyến `CX-YYMMDD-<XE>-NN` (đơn Đang sản xuất của ngày / ca chưa có chuyến; mỗi đơn 1 đơn giao `PH-` + 1 điểm dừng, thứ tự theo giờ giao). **Xuất phát bị chặn** khi nhiệt độ xuất phát đo gần nhất < `DeliveryTempMinC` (60 °C) hoặc chưa đo, còn mẻ của ngày / ca chưa Đã đóng gói (`DeliveryRequirePackedBatches`), có mẻ **chưa lưu mẫu** (`DeliveryRequireSampleRetained` – anh chốt 2026-11-16: lưu mẫu bắt buộc trước khi giao). **Bàn giao bị chặn** khi nhiệt độ tại điểm gần nhất < 60 °C / chưa đo. Dưới ngưỡng bắt buộc ghi hành động khắc phục; lần đo vẫn được lưu. POD → đơn hàng Đã giao; suất bị từ chối → phản ánh `PA-` tự động. Đối soát: suất phục vụ (khách xác nhận) → `OrderReconciliation`, giá trị = giá trị đơn × suất tính tiền ÷ suất đặt (`ReconciliationBillingBasis` = SERVED); đóng → đơn Đã đối soát.
- **BE2.1 (PROPOSED – GAP-DLV-02 cập nhật, GAP-DLV-08 / 09, D-50):** **xếp hàng lên xe bắt buộc** trước khi xuất phát (`DeliveryRoute/Load` → `DeliveryLoading` Loaded + `DeliveryExecution` LOADED, chuyến Đang xếp; `BusinessRules:DeliveryRequireLoading` = true). Thực hiện giao READY → LOADED → DISPATCHED → ARRIVED → DELIVERED / FAILED. 1 đơn hàng nhiều đơn giao (lập tay / tách, Σ suất ≤ suất đơn; số suất ở tag `[SUAT:n]` của `DeliveryOrder.Note`); gán / chuyển chuyến / hủy chỉ khi đơn giao Kế hoạch chưa xếp lên xe và chuyến Kế hoạch. Lập chuyến nhiều xe theo khu vực (`DeliveryRoute/Plan`: khu vực = `Customer.AreaId` hoặc quận / huyện trong địa chỉ, khu lớn trước, vừa tải xe trống nhiều nhất). Tài xế không có 2 chuyến trùng khung giờ kế hoạch cùng ngày.

## 8. Hộp / khay và vệ sinh
- `PackagingLedger` là sổ chỉ ghi thêm: OUT (xuất theo chuyến), RETURN (thu về), LOSS (thất thoát), DAMAGED.
- Tỷ lệ thất thoát ≤ **3%**, hoặc theo ngưỡng riêng của khách (cột ngưỡng trên `Customer` / `Contract` nếu có; không có thì GAP-PKG-01).
- Thất thoát vượt ngưỡng: báo **khách** rồi chuyển **kế toán** tính phí.
- Lô vệ sinh 6 bước (`CleaningBatche` + `CleaningChecklistResult`): 1 nhận/quét QR → 2 phân loại → 3 rửa sơ → 4 rửa máy → 5 khử khuẩn → 6 kiểm tra và nhập kho sạch.

## 8b. Điểm phát suất / căng tin tại khách (mô hình a – D-21)
Điểm bán hiện tại = điểm phục vụ suất theo hợp đồng tại khách hàng. Nhân viên điểm nhận hàng (POD), ghi suất thực tế phục vụ / thừa / thiếu → `OrderReconciliation`; dữ liệu này là cơ sở dự báo nhu cầu kỳ sau (bước 1) và đối soát doanh thu. Không có thu tiền / POS trong R1.

## 9. Phản ánh khách
Mức độ HIGH / MEDIUM / LOW, hạn xử lý 4 / 24 / 72 giờ (`CustomerComplaint`, lịch sử ở `ComplaintHistory`). Quá hạn thì báo lên cấp trên.
- **Bản Pilot đã cài (T19, GAP-DLV-06):** `CustomerComplaint/Create` (số `PA-YYMMDD-NNNN`, `SlaDueAt` = lúc nhận + 4 / 24 / 72 giờ, mức độ lưu High / Medium / Low) → `Assign` (Đang xử lý) → `Resolve` (Đã xử lý) → `Close`; `Reject`; `AddComment`; lịch sử ở `ComplaintHistory`. `isOverdue` tính khi xem; cảnh báo / leo thang tự động → sau Pilot (T23).

## 10. Nhân sự
Chấm công hợp lệ khi nằm trong bán kính bếp (GPS), hoặc quét QR tại bếp, hoặc từ máy chấm công, hoặc chấm hộ (phải ghi rõ hình thức). Lưu ở `AttendanceEvent` (`IsValid`, `SourceChannel` = GPS / QR / MACHINE / PROXY, `DistanceMeters`). Tọa độ và bán kính bếp lấy từ `KitchenWorkstation.Latitude` / `Longitude` / `RadiusMeters`. Có đơn nghỉ (`LeaveRequest`) và đổi ca (`ShiftSwapRequest`).
- **BE2.6 đã cài (PROPOSED – D-55, GAP-HR-02…05; ngưỡng ở `BusinessRules:Hr`):** lưới ca 1 nhân viên 1 ca / ngày, không phân ca ngày đã qua / ngày nghỉ phép Đã duyệt / ca đã chấm công (dùng đơn đổi ca); bỏ ca = xóa mềm, phân lại dùng lại dòng cũ (UNIQUE tính cả dòng xóa). Xác nhận lịch Kế hoạch → Xác nhận, chưa gửi Telegram. Vào ca từ 120′ trước giờ ca tới hết ca; GPS hợp lệ khi cách trạm bếp Hoạt động gần nhất ≤ `RadiusMeters` (trống = 150 m) – ngoài bán kính vẫn ghi `IsValid = false` + cảnh báo (`RejectOutsideRadius` = true thì chặn); đi muộn / về sớm > 5′ ghi Note; tự chấm chỉ GPS / QR, chấm cho người khác cần quyền Thêm `#/hr-attendance` + ghi rõ hình thức (PROXY bắt buộc lý do). Vắng = ca hết giờ chưa vào ca (bảng công tự tính; `MarkAbsent` chốt trạng thái – không job). Đơn nghỉ `DN-` (≤ 30 ngày, lùi ≤ 7 ngày, lý do bắt buộc, không chồng đơn) và đổi / nhường ca `PH-` duyệt 1 cấp qua engine (mục `hr-leave` / `hr-swap`); duyệt nghỉ bỏ ca Kế hoạch / Xác nhận trong khoảng nghỉ + cảnh báo ca thiếu người; duyệt đổi ca hoán đổi nhân viên của 2 ca (kiểm lại chưa chấm công, không 2 ca / ngày, không nghỉ phép).

## 11. Mã chứng từ (sinh theo ngày, không trùng, an toàn khi chạy đồng thời)
| Loại | Mẫu | Ghi vào cột |
|---|---|---|
| Phiếu (đơn / nhập / xuất / kiểm kê) | `PH-YYMMDD-NNNN` | OrderNumber / ReceiptNo / IssueNo / CountNo… |
| Hợp đồng (BE2.2, PROPOSED – D-51) | `HD-YYMMDD-NNN` | Contract.ContractNumber / ContractNo |
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
