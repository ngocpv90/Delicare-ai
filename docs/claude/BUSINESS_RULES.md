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
- **ORD-OPT-1 (D-74, anh chốt 2026-11-22) – khách đăng ký số suất theo từng lựa chọn:**
  - Lựa chọn = **set món** = nhóm `MenuLine` cùng `SequenceNo` (trống = 1) của (thực đơn, loại suất, thứ). Buổi 1 lựa chọn (trưa / tối hiện nay) = 1 set.
  - Dòng đơn lưu thêm `OrderLine.MenuOptionNo` (1–9, cột mới duy nhất – migration `20261122_01_orderline_menu_option.sql`); mỗi dòng = (suất hợp đồng / loại suất, ca, lựa chọn); NULL = dòng cũ không theo lựa chọn. Lựa chọn phải có trong thực đơn áp dụng của ngày ăn; không trùng (loại suất, ca, lựa chọn) trong một đơn; buổi chỉ 1 lựa chọn mà không gửi → tự = 1.
  - **Giá:** mọi lựa chọn cùng buổi cùng giá = đơn giá loại suất (hợp đồng / chính sách giá) – không giá theo món / lựa chọn.
  - **Thực đơn chung:** `Menu.MenuType = COMMON` (danh mục `ReferenceLists:MenuType`: COMMON / CUSTOMER). **UI-6.12 (D-90): không áp dụng thực đơn riêng cho từng khách** – mỗi tuần một thực đơn chung; tra thực đơn áp dụng (mẫu tạo đơn, đăng ký suất tuần, phiếu đăng ký, tính nhu cầu, kế hoạch SX, bot) **chỉ lấy thực đơn chung Đã công bố hiệu lực tại ngày ăn** (có món cho loại suất; cùng ngày hiệu lực → bản tạo sau). Dữ liệu phân công cũ (`MenuAssignment`) giữ nguyên, không dùng; mục menu "Thực đơn riêng cho khách" ẩn (giữ quyền). Không cho công bố 2 thực đơn chung chồng hiệu lực.
  - **Đăng ký suất tuần** (`#/crm-order-week`, `Order/GetWeek` / `SaveWeek` / `CopyPreviousWeek`): mỗi ngày 1 đơn cho điểm giao; Lưu nháp = đơn Nháp; Xác nhận cả tuần = Đã gửi → Đã xác nhận; ngày quá giờ chốt / đơn đã chốt / đang chờ duyệt bị khóa (sửa qua "Yêu cầu thay đổi sau giờ chốt"). Sửa đơn Đã xác nhận lệch > 30% vẫn đi duyệt `ORDER_DEVIATION`; lệch dự báo > 10% chỉ cảnh báo (dự báo của buổi tính 1 lần cho Σ các lựa chọn).
  - **Phiếu đăng ký suất** (`Order/ExportWeekSlip`): Excel theo khách + điểm giao + tuần, ô Số suất dưới mỗi lựa chọn, mã ẩn (A1, hàng 2, cột J) → `Order/ImportWeekSlip` đọc lại vào lưới (chưa lưu; sai mẫu / tuần / lựa chọn không còn → báo dòng lỗi).
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
- Định mức tính theo **gram/suất** cộng **% hao hụt chế biến** (`RecipeLine`). Nhu cầu = Σ(số suất × g/suất tinh ÷ (1 − % hao hụt chế biến) ÷ (1 − % hao hụt sơ chế)) – **BIZ-1.1 (D-77 a, anh chốt 2026-11-23)**, quy đổi đơn vị qua `UomConversion` sang `UnitMeasure` (PO chốt dùng `UnitId` → `UnitMeasure`).
- Kế hoạch đã duyệt thì **khóa định mức**. **Không tạo RecipeVersion.** Dùng cột có sẵn của `Recipe`: `SnapshotJson` (ảnh chụp các dòng định mức), `LockedAt`, `LockedBy`, `VersionNo` / `VersionCode`, `EffectiveFrom` / `EffectiveTo`.
- Thiếu hụt = Nhu cầu − (tồn khả dụng − đã giữ chỗ `MaterialReservation`). Thiếu thì sinh `PurchaseRequest` nháp lúc 21:00, gợi ý NCC từ `SupplierMaterial` (ưu tiên, giá, lead time).
- **APR-UI-1 (D-81, anh chốt 2026-11-23) – màn Phê duyệt tổng:** khuôn tóm tắt chung do máy chủ tính (`IApprovalHandler.GetSummaryAsync`); cảnh báo: PO giá > lần mua trước cùng NCC ≥ `ApprovalFlagPricePct` (10 %), NCC không ưu tiên, PO lập tay; ĐN mua vượt định mức tối đa; sửa đơn sau chốt khi kế hoạch đã duyệt / phát hành, lệch > `PortionDeviationPctNeedApproval`; kiểm kê dòng lệch > `ApprovalFlagStockCountLinePct` (5 %); đơn nghỉ có ca đã phân; hợp đồng giá suất < giá vốn chuẩn bình quân (PROPOSED); người gửi bị từ chối loại này ≥ `ApprovalFlagRejectCount` (2) lần / `ApprovalFlagRejectDays` (30) ngày. Duyệt nhiều chỉ Bình thường + loại trong `ApprovalBulkTypes` (đơn nghỉ, đổi ca; PO chỉ khi < `PurchaseOrderAutoApproveMaxAmount` > 0); không từ chối hàng loạt. Sắp hết hạn < `ApprovalDueSoonHours` (8). Nhắc qua bot ≤ 1 lần / `ApprovalRemindHours` (4) giờ / yêu cầu. Người duyệt của cấp đều nghỉ (đơn nghỉ Đã duyệt hôm nay) → báo ngay COO (COO nghỉ → CEO). Kế hoạch SX giữ Nháp chờ COO vào cùng hộp. Tổng hợp cuối ngày `Messaging:ApprovalDigestHour` (18).
- **MENU-5 (D-79, anh chốt 2026-11-23) – quyền duyệt:** (1) **người gửi không duyệt bất kỳ cấp nào** của yêu cầu mình (`BusinessRules:ApprovalBlockSelfApproval` = true; admin miễn; thay D-52 d) – áp cho hộp duyệt, `ApprovalRequest/Decide`, bot; vẫn hủy được; (2) thao tác kiểm soát cần quyền **Duyệt** (Xem + Sửa mục *Phê duyệt › …*): Đạt / Tạm giữ / Loại mẻ làm tay (`prod-batch`), Giữ / thả lô (`scm-lot`), Kết luận kiểm tra hàng nhập (`qc-inspection`) – QA; mẻ hệ thống tự Đạt (BIZ-2.7) và tự ghi sổ nhập sau kết luận (BIZ-2.6) không đổi; (3) tạo đơn bù cần Thêm Phản ánh **và** Thêm Đơn đặt suất; (4) người duyệt từng loại: ORG_STRUCTURE §2 / `docs/claude/MENU5_MATRIX.md`.
- **D-78 (anh chốt 2026-11-23):** (1) đơn Đã chốt sau khi kế hoạch ngày đã duyệt / phát hành (duyệt sau giờ chốt, đơn bù) → tự sinh lệnh SX + mẻ bổ sung gắn kế hoạch đó, `[BO_SUNG:<đơn>]`, nhãn 🔴, ưu tiên (không kịp lịch ngược → bắt đầu ngay), giữ chỗ + phiếu xuất Nháp; vẫn 1 KH / ngày. (2) `Contract.ConfigJson.billingBasis` (RECEIVED / SERVED / ORDERED / DELIVERED / ACCEPTED) ưu tiên hơn `ReconciliationBillingBasis`. (3) giá vốn đơn bù giá 0 = Chi phí lỗi, tách khỏi giá vốn / lãi gộp suất bán. (6) màn kỹ thuật chỉ admin / FIN-IT; đọc danh mục chỉ số bằng quyền xem KPI / bảng điều hành.
- **BIZ-3 (D-77 – đợt 3, anh chốt 2026-11-23) – hoàn thiện luồng:** (3.1) `CustomerComplaint/CreateCompensationOrder`: phản ánh SHORTAGE / DAMAGED / TEMPERATURE có đơn gốc → đơn bù Đã chốt (tự duyệt), cùng ngày ăn / điểm giao / loại suất (chia lựa chọn theo đơn gốc), số = POD từ chối (sửa được), giá 0 (lỗi Delicare) hoặc giá dòng gốc (ChargeCustomer), Note `[DON_BU:<đơn gốc>|<PA>]`, 1 đơn bù / phản ánh, phản ánh → Đang xử lý, bot 🔴 Kế hoạch + điều phối; **chưa tự sinh kế hoạch / lệnh bổ sung** (dùng Nấu thêm). (3.2) `BillingStatement/GetStatement` / `ExportStatement` (xlsx) / `PrintStatement` (pdf), màn `crm-billing`: đơn đối soát Đã chốt, suất tính tiền + đơn giá cố định + thành tiền = FinalAmount chia theo dòng; phí khay = (mất − giao × ngưỡng %) × `ContainerLossFee`[loại] (mặc định 0); VAT `SalesVatPct` 8 %; đơn chưa chốt → chờ đối soát; đơn bù giá 0 bỏ. (3.3) `StandardCostAutoOnSupplierPrice`: lưu SupplierMaterial → StandardCost/Recalculate các món khóa dùng NL + FC % thực đơn (giá bán = đơn giá suất thấp nhất hợp đồng hiệu lực – PROPOSED) > `KpiIngredientToRevenueMaxPct` → bot FOOD_COST_OVER. (3.4) job MenuExpiry 00:30, MenuWeekReminder 09:00 (`MenuReminderWeekday` 3), OrderWeekReminder 16:00 (`OrderWeekReminderWeekday` 5). (3.5) KPI_DAILY 22:00, KPI_MONTHLY 01:00 (tháng của hôm qua) bật. (3.6) `Customer.ExternalData.containerLossMaxPct` trên form, ghi gộp khóa; kiểu tính tiền theo hợp đồng chờ GAP-BIZ-01. (3.7) ẩn `#/wf-inbox?type=qc-inspection`.
- **BIZ-2 (D-77 – đợt 2, anh chốt 2026-11-23) – bớt bước, tự động hóa** (mọi bước tự động ghi "Hệ thống tự …", lỗi bước tự động không hỏng thao tác gốc; tắt từng bước bằng cờ BusinessRules): (2.1) đơn nhân viên nhập Nháp → **Xác nhận** thẳng (bỏ Gửi; Đã gửi cho đơn khách tự đăng ký); trước giờ chốt sửa số **không đi duyệt** – lệch > `PortionDeviationPctNeedApproval` (30 %) so **trung bình 4 tuần cùng thứ của điểm giao** chỉ cảnh báo + Note `[LECH_TB_4_TUAN]` (ORDER_DEVIATION đang chờ vẫn xử lý); sau giờ chốt giữ ORDER_CHANGE_AFTER_CUTOFF, người duyệt seed = SAL-CON (Trưởng KD) + COO (bỏ CEO – `DB/scripts/BIZ2_order_approvers.sql`); tạo đơn mặc định ngày mai. (2.2) `PurchaseOrderAutoApproveFromRequest`: PO lập từ đề nghị Đã duyệt tự Đã duyệt khi NCC gợi ý + giá bảng giá + không cảnh báo đơn vị + tổng < `PurchaseOrderDirectorApprovalAmount`; sửa dòng PO tự duyệt (chưa nhập) → Nháp. (2.3) công thức: ngày áp dụng chọn khi gửi; duyệt cấp cuối = Lock. (2.4) `ProductionAutoReleaseAfterLock`: sau `Order/LockByMealDate` (job + nút) → Generate → Approve → Release; giữ Nháp + bot 🔴 Kế hoạch / COO khi sửa tay / thiếu công thức khóa / thiếu NL sau giữ chỗ / mẻ bắt đầu đã qua (phát hành thử trong transaction CSDL). (2.5) `ProductionAutoDraftIssueOnRelease`: phiếu xuất Nháp mỗi lệnh, thẻ ca `[XUAT_CA:<KH>|<ca>]` (GAP-BIZ-02 – không bảng phiếu ca), `MaterialIssue/GetShiftGroups`, `MaterialIssue/PostShift`. (2.6) `GoodsReceiptAutoPostAfterQc`: QC Đạt / Đạt một phần → GoodsReceipt/Post ngay. (2.7) `BatchAutoPass`: mẻ Chờ QC không Tạm giữ, đủ lõi + sản lượng + lưu mẫu + không FAIL trọng yếu → Đạt; Tạm giữ được cả mẻ Đạt. (2.8) `PackingAutoGenerateOnPass`: mẻ Đạt → PackingOrder/GenerateFromBatch. (2.9) `ReconciliationAutoCreate` / `ReconciliationAutoCloseHours` (24) / `MealCostAutoOnReconcileClose`: đơn Đã giao → đối soát (phục vụ = nhận); Khớp + không phản ánh mở + quá 24 giờ → chốt (job 20:00 + mở màn); chốt → MealCost/Calculate.
- **FC-1 (D-76 – anh chốt 2026-11-23): dự báo hệ thống tự gợi ý** (`Forecast/Suggest`, form; `Preview` = chỉ tính, `Overwrite` = ghi đè Nháp đã sửa): khóa (khách, điểm giao đang hoạt động, ngày, loại suất trong hợp đồng hiệu lực tại ngày), ca trống (theo buổi – Σ lựa chọn). Số gợi ý = ⌈trung bình suất thực nhận cùng thứ trong `ForecastLookbackWeeks` (4) tuần gần nhất trước hôm nay⌉; thực nhận của đơn Đã chốt / Đang SX / Đã giao / Đã đối soát = suất tính tiền trong đối soát (BIZ-1.4; đối soát cũ: suất phục vụ) → Σ suất nhận POD → số đặt, chia loại suất theo tỷ lệ dòng đơn; tuần không có đơn không tính; < 2 tuần → Thiếu dữ liệu. Ghi bản Nháp, Note `[GOI_Y=<số>] TB 4 tuần cùng thứ: …` (GAP-FC-01 – không thêm cột); Nháp có số ≠ số gợi ý hoặc không có thẻ = người đã sửa → giữ. Tối đa `ForecastSuggestMaxDays` (14) ngày. **Độ chính xác** (`Forecast/GetAccuracy`): dự báo Đã công bố mới nhất so thực nhận, sai số = Σ|F − A| / ΣA × 100 theo khách / tuần (tính khi xem).
- **UI-6.9 (D-87 – anh chốt 2026-11-23): bảng kê tháng mọi khách** (`BillingStatement/GetMonthOverview` / `ExportStatementBatch` / `PrintStatementBatch` (JSON) / `GetExportHistory` + `GetStatement` / `ExportStatement` / `PrintStatement` có sẵn): khách = có đơn chưa hủy trong tháng ∪ hợp đồng Hiệu lực giao tháng (lọc chi nhánh); mỗi khách tính như BIZ-3.2 (`BuildAsync`); trạng thái READY (không còn đơn chờ đối soát, có dòng / phí khay) / PENDING / NONE; ⚠ khi |tiền suất − Σ `FinalAmount` đối soát tháng trước| / tháng trước > `BillingMonthDeviationPct` (20). Xuất lô: xlsx 1 tệp (sheet Tổng hợp + mỗi khách 1 sheet, `PrintRenderer`), pdf = zip mỗi khách 1 PDF `BangKe_<MaKH>_<YYYYMM>`. Mỗi lần xuất / in (từng khách hoặc lô) ghi `AuditChange` (EntityName `BillingStatement`, IdChange = khách, Action Export / Print). Job `BillingMonthReady` 08:00 ngày `BillingMonthNoticeDay` (1) → FIN-ACC + SAL-HEAD (bot Điều hành). "Gửi khách xác nhận" chưa làm (D-87 c). Không thêm bảng / cột.
- **UI-6.8 (D-86 b, c – anh chốt 2026-11-23): Đối soát xác nhận hàng loạt** (`OrderReconciliation/GetBoard` / `ReconcileMany` (JSON) / `CloseMany` (JSON) / `ConfirmServedFull` (form `orderId`)): bảng = đơn Đã giao / Đã đối soát có ngày ăn trong kỳ (lọc khách, chi nhánh); tab PENDING (chưa người / bot xác nhận – đối soát tự tạo BIZ-2.9 coi là chưa xác nhận) / CONFIRMED / VARIANCE (nhận ≠ đặt hoặc có lỗi Delicare) / CLOSED; nguồn số nhận POD / BOT (ghi chú bắt đầu "Telegram") / WEB / AUTO. Tính tiền hiển thị = `BillingQuantity` đã lưu, chưa lưu thì nhận − lỗi; thành tiền = `FinalAmount` hoặc giá trị đơn × tính tiền ÷ đặt. Xác nhận / chốt theo lô dùng lại `Reconcile` / `Close` (mỗi đơn một transaction, dòng lỗi báo riêng); phục vụ trống = khách nhận. Bot điểm phát: sau ký nhận bàn giao `BusinessRules:ReconcilePromptMinutes` (120, 0 = tắt) gửi người ký nhận (bot Vận hành) nút "✅ Đã phục vụ đủ" (`rcn:ok`) / "✏️ Nhập số khác" (`rcn:o`). Không thêm bảng / cột.
- **UI-6.6 (D-85 – anh chốt 2026-11-23): đăng ký suất tuần theo mẫu chuẩn** (`Order/GetWeekOverview` / `ConfirmWeekPoints` (JSON) / `FillWeekFromForecast` / `ImportWeekSlips` (form, nhiều `files`) + `GetWeek` / `SaveWeek` / `CopyPreviousWeek` / `ExportWeekSlip` có sẵn): tổng quan = khách hợp đồng hiệu lực giao tuần × điểm giao đang hoạt động × 7 ngày; ô NONE (kèm dự báo Đã công bố) / DRAFT / SUBMITTED (khách gửi – chờ xác nhận) / CONFIRMED / LOCKED (Đã chốt trở đi) / NOCONTRACT. Lệch dự báo = (Σ đăng ký − Σ dự báo) / Σ dự báo trên ô có cả hai. Xác nhận nhiều điểm = Gửi + Xác nhận từng đơn Nháp / Đã gửi trước giờ chốt (đơn lỗi báo riêng). Lấy số dự báo: chỉ buổi còn trống (mọi lựa chọn = 0, ngày chưa khóa); chia lựa chọn theo Σ suất đặt cùng thứ 4 tuần (không có → lựa chọn đầu), phần lẻ vào lựa chọn lớn nhất; chỉ trả về. Tải nhiều phiếu: mã ẩn A1 → khách / điểm / tuần; khác tuần đang mở → lỗi phiếu đó; đọc được → `SaveWeek` Nháp. Ô lưới có `Average4Weeks` (TB cùng thứ 4 tuần, tuần có đơn) để cảnh báo ⚠ > 30 %. **D-85 c:** điểm chưa có số lúc chốt **không tự tạo đơn**; job `OrderMissingReminder` 18:00 (KD phụ trách) + `OrderMissingAtCutoff` 20:05 (🔴 KD phụ trách + SAL-HEAD + COO), bot Điều hành. Không thêm bảng / cột.
- **UI-6.5 (D-84 – anh chốt 2026-11-23): dự báo theo tuần** (`Forecast/GetWeek` / `SaveCell` (JSON) / `SuggestWeek` / `PublishWeek` (form)): khách có hợp đồng hiệu lực trong tuần tự hiện (kể cả chưa có dự báo); dòng = điểm giao đang hoạt động × loại suất hợp đồng; ô = PUBLISHED / DRAFT / REGISTERED (Σ dòng đơn Đã xác nhận trở lên – chỉ đọc, dùng số đăng ký) / MISSING (< 2 tuần có đơn) / LOCKED (ngoài hiệu lực hợp đồng) / EMPTY. Sửa ô = upsert bản Nháp (trống = xóa Nháp); chặn ngày đã qua và ô đã đăng ký. Công bố tuần bỏ qua ô đã đăng ký (giữ Nháp). Sai số 4 tuần = `GetAccuracy`. **Vòng tự động** (bot Điều hành → `Customer.UserAssignId` + SAL-HEAD): `ForecastAutoSuggest` T4 22:00 → `ForecastReadyNotice` T5 08:00 → `ForecastPublishReminder` T6 12:00 → `ForecastAutoPublish` T6 16:00 (Note "Hệ thống tự công bố", ô thiếu dữ liệu để trống + báo danh sách). Cờ `BusinessRules:ForecastAutoSuggest` / `ForecastAutoPublish` (true), thứ `ForecastSuggestWeekday` 3 / `ForecastNoticeWeekday` 4 / `ForecastPublishWeekday` 5; giờ ở `Jobs:<Code>`. Quyền: SAL-HEAD / SAL-SALES xem + sửa; OPS-PLAN chỉ xem.
- **BIZ-1.2 / 1.3 (D-77 b, c – anh chốt 2026-11-23):** chỉ giữ **một** lớp dự phòng = tồn an toàn theo nguyên liệu; "Dự phòng thêm %" khi lập đề nghị mua mặc định 0; 15 % theo lựa chọn chỉ cho dòng đơn cũ không theo lựa chọn. **Kế hoạch mua nhiều ngày** (`MaterialRequirement/GetHorizon`, `PurchaseHorizonDays` = 7): nguồn từng ngày đơn Đã chốt → đăng ký / đơn Đã xác nhận (+ Đã gửi) → dự báo Đã công bố; trừ tồn lũy kế; ngày phải đặt = ngày cần − lead time NCC ưu tiên − `PurchaseOrderLeadBufferDays` (1); đề nghị mua theo ngày cần hàng (`CreateHorizonPurchaseRequest`). **Kế hoạch SX chính thức (`ProductionPlan/Generate`) và tính nhu cầu chính thức chỉ lấy đơn Đã chốt** (A9).
- **Anh chốt 2026-11-16 (T14.1):** công thức hao hụt ~~số lượng × (1 + hao hụt %)~~ → **thay bằng công thức chia 2 tầng (BIZ-1.1, D-77 a, 2026-11-23)**: lượng mua = tinh ÷ (1 − % chế biến `ExpectedWasteRate`) ÷ (1 − % sơ chế `WastePercent`, trống = `Ingredient.StandardWastePercent`) – một chỗ tính `WasteRules` dùng cho MRP, nguyên liệu của mẻ, giữ chỗ / phiếu xuất theo định mức, giá vốn công thức; kiểm chứng Thịt hầm đậu đỏ 70 g / 35 % / 5 % → 113,4 g; **chỉ đơn Đã chốt (Locked)** được tính, đơn Confirmed chỉ cảnh báo; kế hoạch nháp `KH-` theo **ngày**. Thực đơn có ≥ 2 lựa chọn cho một loại suất: số suất chia đều cho các lựa chọn rồi **cộng dự phòng `BusinessRules:MrpOptionBufferPct` = 15% mỗi lựa chọn** (0 = tắt; ví dụ 100 suất, 2 lựa chọn → mỗi lựa chọn 57,5 suất). Đề nghị mua của ngày đã gửi duyệt / duyệt / lập PO → không tính lại, dùng **tính bổ sung** (`MaterialRequirement/CalculateTopUp`): đề nghị mua bổ sung Nháp chỉ cho phần thiếu thêm, phần thừa chỉ cảnh báo.
- **ORD-OPT-1.6 (D-74 e, anh chốt 2026-11-22):** dòng đơn **có `MenuOptionNo`** → số phần món = **số suất của lựa chọn × `MenuLine.PortionQty` (trống = 1) của các món trong set** – không chia đều; dự phòng `BusinessRules:MrpOptionQtyBufferPct` (mặc định **0**). Món có trong nhiều lựa chọn (sữa, cơm) cộng dồn. Dòng cũ NULL giữ cách trên (chia đều + `MrpOptionBufferPct`). Lựa chọn không còn trong thực đơn → cảnh báo + tính cách cũ. Một chỗ tính: `ProductionRules.LinePortions` – dùng cho MRP (Calculate / TopUp), kế hoạch SX (Generate), chia ca (Release), đóng gói (GenerateFromBatch), giá vốn bữa, truy xuất. Đổi món đã duyệt áp như cũ.
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
- **BE2.5 (PROPOSED – D-54, GAP-PKG-01…03, GAP-QA-03 / 04):** bàn giao (`DeliveryStop/Deliver`) ghi sổ bao bì **OUT** = suất khách nhận × `PackagingUnitsPerMeal` (loại = loại của kết quả đóng gói, không có → `PackagingDefaultType` KHAY; `PackagingQuantity = 0` = hộp dùng 1 lần) + phiếu thu hồi Chờ thu hồi; thu hồi (`ContainerReturn/Record`) ghi **IN** tốt / hỏng, không vượt số còn ở khách; kiểm tra (`ContainerReturn/Inspect`, OK / SHORTAGE / DAMAGED / DIRTY / REJECTED, khác OK bắt buộc ghi chú) tự lập mẻ vệ sinh `VS-YYMMDD-<CA>` cho khay dùng được; 6 bước (checklist `VS-KHAY`) **đi lần lượt**, khử khuẩn đạt bắt buộc ghi hóa chất; kết luận PASS (đủ 6 bước đạt) → sổ **AVAILABLE**, REJECT (lý do) → vệ sinh lại mẻ mới. Thất thoát (**LOSS**) ghi tay có lý do; còn ở khách = OUT − IN − LOSS; tỷ lệ thất thoát = LOSS ÷ OUT trong kỳ, ngưỡng = `Customer.ExternalData` `containerLossMaxPct` hoặc `KpiContainerLossMaxPct` 3% → cờ + cảnh báo báo khách / kế toán (không job).
- **BE2.5 – checklist ATTP theo ca / mẻ (§6, PROPOSED D-54 c, d):** mỗi lần ghi 1 dòng mới (dòng mới nhất = kết quả hiện tại), tiêu chí bắt buộc phải có kết quả, KHÔNG ĐẠT bắt buộc khắc phục; hạn = giờ xuất bếp của ca + `ChecklistOffsetsMinutes` (cờ quá hạn khi xem). Tiêu chí **trọng yếu** KHÔNG ĐẠT gắn mẻ → mẻ Đạt về Chờ QC (HOLD), chặn đạt QC / đóng gói / xuất chuyến tới khi kiểm lại ĐẠT (`FoodSafetyCriticalFailHoldsBatch`). Sự cố NCC: Mới → Đang xử lý → Đã xử lý (bắt buộc cách xử lý) → Đóng.

## 8b. Điểm phát suất / căng tin tại khách (mô hình a – D-21)
Điểm bán hiện tại = điểm phục vụ suất theo hợp đồng tại khách hàng. Nhân viên điểm nhận hàng (POD), ghi suất thực tế phục vụ / thừa / thiếu → `OrderReconciliation`; dữ liệu này là cơ sở dự báo nhu cầu kỳ sau (bước 1) và đối soát doanh thu. Không có thu tiền / POS trong R1.
- **BIZ-1.4 (D-77 d, anh chốt 2026-11-23):** tiền đối soát mặc định = suất **khách ký nhận (POD) − suất trả lại / hủy do lỗi Delicare** (thiếu, hỏng, không đạt nhiệt độ, sai món, chất lượng – lưu số + lý do trong JSON `OrderReconciliation.Note`); khách không dùng hết không giảm tiền; suất phục vụ thực tế chỉ để tham khảo / dự báo. `ReconciliationBillingBasis` = `RECEIVED` (vẫn chọn được SERVED / ORDERED / DELIVERED / ACCEPTED toàn hệ thống). Kiểu tính **theo từng hợp đồng chưa làm**: bảng `Contract` không có cột dữ liệu mở rộng → **GAP-BIZ-01** (chờ duyệt thêm cột hoặc chỗ lưu). Đối soát đã chốt trước đây giữ nguyên.
- **BIZ-1.5 (D-77 e):** giá **cố định lúc chốt** – Chốt theo ngày (job 20:00 / nút), Chốt từng đơn, duyệt sửa sau giờ chốt đều tra `PricePolicy/Resolve` tại ngày ăn, ghi đơn giá + thành tiền vào dòng đơn, tính lại tổng đơn; đối soát / giá vốn dùng giá đã ghi. Phụ lục giá duyệt sau khi chốt không đổi đơn đã chốt. **Thứ tự tra giá (UI-6.4, D-83):** phụ lục điều chỉnh giá **Hiệu lực** của hợp đồng tại ngày ăn (dòng `PricePolicy` có `ContractId`) → giá gốc loại suất trong hợp đồng (`ContractMeal`). Bỏ cấp theo khách; bản cũ theo khách (không hợp đồng) chỉ còn đọc cho khách chưa chuyển được (0 / > 1 hợp đồng hiệu lực – có cảnh báo log). Phụ lục: hợp đồng Hiệu lực, từ ≥ ngày mai trong hiệu lực hợp đồng, không chồng thời gian cùng loại suất, gửi duyệt bắt buộc có file phụ lục đã ký, CEO duyệt (`#/wf-inbox?type=crm-price-policy`). Dòng không tra được giá giữ giá cũ.

## 9. Phản ánh khách
Mức độ HIGH / MEDIUM / LOW, hạn xử lý 4 / 24 / 72 giờ (`CustomerComplaint`, lịch sử ở `ComplaintHistory`). Quá hạn thì báo lên cấp trên.
- **Bản Pilot đã cài (T19, GAP-DLV-06):** `CustomerComplaint/Create` (số `PA-YYMMDD-NNNN`, `SlaDueAt` = lúc nhận + 4 / 24 / 72 giờ, mức độ lưu High / Medium / Low) → `Assign` (Đang xử lý) → `Resolve` (Đã xử lý) → `Close`; `Reject`; `AddComment`; lịch sử ở `ComplaintHistory`. `isOverdue` tính khi xem; cảnh báo / leo thang tự động → sau Pilot (T23).

## 10. Nhân sự
Chấm công hợp lệ khi nằm trong bán kính bếp (GPS), hoặc quét QR tại bếp, hoặc từ máy chấm công, hoặc chấm hộ (phải ghi rõ hình thức). Lưu ở `AttendanceEvent` (`IsValid`, `SourceChannel` = GPS / QR / MACHINE / PROXY, `DistanceMeters`). Tọa độ và bán kính bếp lấy từ `KitchenWorkstation.Latitude` / `Longitude` / `RadiusMeters`. Có đơn nghỉ (`LeaveRequest`) và đổi ca (`ShiftSwapRequest`).
- **BE2.6 đã cài (PROPOSED – D-55, GAP-HR-02…05; ngưỡng ở `BusinessRules:Hr`):** lưới ca 1 nhân viên 1 ca / ngày, không phân ca ngày đã qua / ngày nghỉ phép Đã duyệt / ca đã chấm công (dùng đơn đổi ca); bỏ ca = xóa mềm, phân lại dùng lại dòng cũ (UNIQUE tính cả dòng xóa). Xác nhận lịch Kế hoạch → Xác nhận, chưa gửi Telegram. Vào ca từ 120′ trước giờ ca tới hết ca; GPS hợp lệ khi cách trạm bếp Hoạt động gần nhất ≤ `RadiusMeters` (trống = 150 m) – ngoài bán kính vẫn ghi `IsValid = false` + cảnh báo (`RejectOutsideRadius` = true thì chặn); đi muộn / về sớm > 5′ ghi Note; tự chấm chỉ GPS / QR, chấm cho người khác cần quyền Thêm `#/hr-attendance` + ghi rõ hình thức (PROXY bắt buộc lý do). Vắng = ca hết giờ chưa vào ca (bảng công tự tính; `MarkAbsent` chốt trạng thái – không job). Đơn nghỉ `DN-` (≤ 30 ngày, lùi ≤ 7 ngày, lý do bắt buộc, không chồng đơn) và đổi / nhường ca `PH-` duyệt 1 cấp qua engine (mục `hr-leave` / `hr-swap`); duyệt nghỉ bỏ ca Kế hoạch / Xác nhận trong khoảng nghỉ + cảnh báo ca thiếu người; duyệt đổi ca hoán đổi nhân viên của 2 ca (kiểm lại chưa chấm công, không 2 ca / ngày, không nghỉ phép).

## 11. Mã chứng từ và mã danh mục (D-63 – sinh tự động, không trùng, an toàn khi chạy đồng thời)

**Luật chung**
1. Chỉ chữ hoa không dấu, số và `-`.
2. Tiền tố: 2 chữ = đối tượng, chữ thứ 3 (tùy chọn) = phân loại (`KH` + `B` / `C`). Mỗi tiền tố duy nhất, không là phần đầu của tiền tố khác; đăng ký ở một chỗ (`DocumentNumberService.Specs` + bảng dưới).
3. **Danh mục:** `TIỀNTỐ-NNNNN` – 5 chữ số, tăng liên tục, không quay về 1; quá 99.999 tự sang 6 chữ số.
4. **Chứng từ:** `TIỀNTỐ-YYMMDD-NNNN` – YYMMDD là ngày lập, số quay về 1 mỗi ngày; có hậu tố: `TIỀNTỐ-YYMMDD-<HẬU TỐ>-NN` (hậu tố bỏ dấu, bỏ ký tự khác A–Z 0–9, tối đa 20 ký tự).
5. Máy chủ sinh khi lưu lần đầu, trong transaction; người dùng không nhập / không sửa; không đổi sau khi cấp; không cấp lại số đã dùng.
6. Chứng từ phát sinh trước D-63 giữ số cũ; tìm kiếm, bot, in nhận cả mẫu cũ (cột "Trước D-63").

**Danh mục (dạng A)**

| Tiền tố | Đối tượng | Ghi vào cột | Ghi chú |
|---|---|---|---|
| `KHB` | Khách hàng doanh nghiệp (`CustomerType = BUSINESS`) | Customer.Code (+ CIF = Code) | khách cũ đánh lại một lần |
| `KHC` | Khách hàng cá nhân (`CustomerType = INDIVIDUAL`) | Customer.Code (+ CIF = Code) | khách cũ đánh lại một lần |
| `NC` | Nhà cung cấp | Supplier.Code | NCC cũ đánh lại một lần |
| `NL` | Nguyên liệu | Ingredient.Code | bản ghi cũ giữ mã |
| `MA` | Món ăn | Dishe.Code | bản ghi cũ giữ mã |
| `KO` | Kho | Warehouse.Code | bản ghi cũ giữ mã (QR `LOC_`) |
| `XE` | Phương tiện | Vehicle.Code | bản ghi cũ giữ mã (QR `VEH_`) |
| `NV` | Nhân viên | Employee.EmployeeCode / Code (= tên đăng nhập) | chỉ nhân viên mới |

**Chứng từ (dạng B)**

| Tiền tố | Mẫu | Chứng từ | Ghi vào cột | Trước D-63 |
|---|---|---|---|---|
| `DH` | `DH-YYMMDD-NNNN` | Đơn hàng | Order.OrderNumber / OrderNo | PH |
| `HD` | `HD-YYMMDD-NNN` | Hợp đồng | Contract.ContractNumber / ContractNo | HD |
| `NU` | `NU-YYMMDD-NNN` (ngày bắt đầu áp dụng) | Thực đơn | Menu.Code | – |
| `DM` | `DM-YYMMDD-NNNN` | Đề xuất đổi món | MenuChangeRequest.RequestNo | PH |
| `MH` | `MH-YYMMDD-NNNN` | Đề nghị mua | PurchaseRequest.RequestNumber / RequestNo | PH |
| `PO` | `PO-YYMMDD-NNNN` | Đơn mua NCC | PurchaseOrder | PO |
| `PN` | `PN-YYMMDD-NNNN` | Phiếu nhập | GoodsReceipt.ReceiptNumber / ReceiptNo | PH |
| `PX` | `PX-YYMMDD-NNNN` | Phiếu xuất | MaterialIssue.IssueNumber / IssueNo | PH |
| `KK` | `KK-YYMMDD-NNNN` | Kiểm kê | StockCount.CountNumber / CountNo | PH |
| `GD` | `GD-YYMMDD-NNNN` | Giao dịch kho tay (điều chỉnh, chuyển kho) | InventoryTransaction | PH |
| `LO` | `LO-YYMMDD-<NCC>-NN` | Lô nguyên liệu | MaterialLot.LotNumber / LotNo | NL |
| `KS` | `KS-YYMMDD` (+ `-2`, `-3`…) | Kế hoạch sản xuất | ProductionPlan.PlanNumber / PlanNo | KH |
| `LS` | `LS-YYMMDD-NNNN` | Lệnh sản xuất | ProductionOrder | PH |
| `ME` | `ME-YYMMDD-<CA>-NN` | Mẻ nấu | ProductionBatche | ME |
| `DG` | `DG-YYMMDD-NNNN` | Lệnh đóng gói | PackingOrder | PH |
| `GH` | `GH-YYMMDD-NNNN` | Đơn giao hàng | DeliveryOrder | PH |
| `CX` | `CX-YYMMDD-<XE>-NN` | Chuyến xe | DeliveryRoute.RouteNo | CX |
| `VS` | `VS-YYMMDD-<CA>` | Mẻ vệ sinh | CleaningBatche | VS |
| `PA` | `PA-YYMMDD-NNNN` | Phản ánh khách hàng | CustomerComplaint | PA |
| `XN` | `XN-YYMMDD-NNN` | Yêu cầu duyệt | ApprovalRequest.RequestNo | XN |
| `DN` | `DN-YYMMDD-NNNN` | Đơn nghỉ | LeaveRequest | DN |
| `DC` | `DC-YYMMDD-NNNN` | Đơn đổi ca | ShiftSwapRequest.RequestNo | PH |
| `CC` | `CC-YYMMDD-NNNN` | Chấm công | AttendanceEvent – **chưa có cột số** (GAP, giữ như trước D-63, chưa sinh) | CC |

Cách cài: `IDocumentNumberService.Next(kind, date, suffix)`. Dùng `sp_getapplock` theo khóa mã gốc (`TIỀNTỐ` cho danh mục, `TIỀNTỐ-YYMMDD[-HẬU TỐ]` cho chứng từ), đọc MAX số hiện có trong cột đích (cả cột cũ), rồi +1. **Không tạo bảng đếm và không tạo SEQUENCE** (luật DB).

**11.3 Phân nhóm mọi cột mã (D-63 k)** – mỗi cột mã định danh thuộc đúng 1 nhóm; bảng / cột mới phải xếp nhóm (test chặn).

| Nhóm | Quy tắc | Bảng.cột |
|---|---|---|
| **G1 – tự sinh, danh mục (dạng A)** | Máy chủ sinh, giao diện chỉ đọc | Customer.Code (`KHB` / `KHC`) · Supplier.Code (`NC`) · Ingredient.Code (`NL`) · Dishe.Code (`MA`) · Warehouse.Code (`KO`) · Vehicle.Code (`XE`) · Employee.EmployeeCode / Code (`NV`, chỉ người mới) · CustomerDeliveryLocation.Code (`DP` – điểm phát / giao) · PricePolicie.Code (`BG` – bảng giá) |
| **G1 – tự sinh, chứng từ (dạng B)** | Máy chủ sinh, giao diện chỉ đọc | Bảng chứng từ ở §11 (Order, Contract, Menu `NU`, MenuChangeRequest, PurchaseRequest, PurchaseOrder, GoodsReceipt, MaterialIssue, StockCount, InventoryTransaction, MaterialLot, ProductionPlan, ProductionOrder, ProductionBatche, PackingOrder, DeliveryOrder, DeliveryRoute.RouteNo, CleaningBatche, CustomerComplaint, ApprovalRequest, LeaveRequest, ShiftSwapRequest) · QCInspection.InspectionNumber (`QC-YYMMDD-NNNN`) · MaterialRequirement.RequirementNo (`MR-YYMMDD-NNN`) |
| **G2 – mã nghiệp vụ người đặt** | Nhập tay; chữ hoa không dấu, A–Z 0–9 `-` `_`; không trùng; không sửa khi đã được dùng (QR, hậu tố, công thức, cấu hình) | Company · OrganizationUnit · Position · Shift (`CA1`… – hậu tố mẻ / vệ sinh) · UnitMeasure (`KG`…) · DishCategorie · IngredientCategorie · ProductionLine (QR `LINE_`) · KitchenWorkstation · StorageZone · StorageLocation (QR `LOC_`) · FoodSafetyChecklist.Code · FoodSafetyChecklistItem.ItemCode · SupplierMaterial.SupplierMaterialCode (mã hàng của NCC) · BotConfiguration.BotCode · IntegrationChannel.ChannelCode · KpiMetricDefinition.MetricCode · AdministrativeProvince / District / Ward.Code (mã hành chính nhà nước) |
| **G3 – không phải mã định danh** | Không áp quy tắc | Cột mã loại / trạng thái / tiền tệ / đơn vị (`…TypeCode`, `CurrencyCode`, `UnitCode`, `CategoryCode`, `ConditionCode`, `PriorityCode`, `ResultCode`, `DecisionCode`, `ActionCode`, `ApproverRoleCode`, `DomainCode`, `AggregationCode`, `FrequencyCode`, `AreaCode`, `DirectionCode`, `OperationCode`, `ErrorCode`, `ProviderCode`, `EnvironmentCode`, `JobCode`, `MigrationCode`, `DeviceCode`) · cột sao chép / tham chiếu (`DeliveryExecution` / `DeliveryLoading.VehicleCode`, `PackagingLedger.ReferenceNo`, `GoodsReceiptLine.LotNumber`, `KpiDaily` / `MonthlySnapshot.MetricCode`, `DeliveryRoute.Code`) · số thứ tự (`StepNo`, `SequenceNo`, `CurrentStepNo`) · phiên bản (`Menu` / `Recipe.VersionCode`) · bảng cũ / ngoài phạm vi (`*_Legacy_*`, `TestDemo`, `Patient`) |

**Khách hàng theo loại (D-63 f–i):** danh mục `CustomerType` = `BUSINESS` / `INDIVIDUAL`; trường bắt buộc / tùy chọn theo loại, CIF = Code và không hiển thị, khóa đổi loại – xem DECISIONS D-63.

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

## 13. Nhãn hiển thị – tiếng Việt một cách gọi duy nhất (D-88 c, UI-6.10)

Mọi chữ người dùng thấy (giao diện, chip lọc, thẻ chỉ số, file CSV / Excel / PDF, thông báo, bot) dùng **nhãn tiếng Việt** dưới đây khi bảng / cột chưa có nhãn riêng trong danh mục trạng thái (`StatusCatalogEffective` – lớp `*Status.cs` hoặc `STATUS_CATALOG.md`). Nguồn code: máy chủ `AppDelicare.Model/Helper/StatusLabels.cs`, giao diện `UI.STATUS_VI` (`core/ui.js`) – hai bảng phải giống nhau. Mã trạng thái mới: **thêm vào cả hai trước khi lên giao diện**; ở máy dev, mã chưa dịch hiện "(chưa dịch: X)" để quét tự động bắt được.

**Active / Inactive = Đang hoạt động / Ngừng hoạt động ở mọi bảng** (bỏ biến thể "Hoạt động", "Đang áp dụng", "Ngừng", "Không hoạt động"). Trạng thái chứng từ có nhãn nghiệp vụ riêng giữ nhãn riêng (vd hợp đồng *Hiệu lực*, đơn *Đã chốt*, lô *Khả dụng*).

| Mã | Nhãn | Mã | Nhãn | Mã | Nhãn |
|---|---|---|---|---|---|
| Active | Đang hoạt động | Inactive | Ngừng hoạt động | Draft | Nháp |
| Submitted | Đã gửi | Pending | Chờ xử lý | PendingApproval | Chờ duyệt |
| Approved | Đã duyệt | Rejected | Từ chối | Cancelled / Canceled | Đã hủy |
| Confirmed | Đã xác nhận | Locked | Đã chốt | Published | Đã công bố |
| Superseded | Bị thay thế | Expired | Hết hiệu lực | Open | Đang mở |
| Closed | Đã đóng | InProgress | Đang thực hiện | Completed | Hoàn thành |
| Delivered | Đã giao | Failed | Thất bại | OnHold / Hold | Tạm giữ |
| Passed / Pass | Đạt | Fail | Không đạt | Success / Succeeded | Thành công |
| Running / Started | Đang chạy | Queued | Chờ gửi | Sent | Đã gửi |
| Received | Đã nhận | Processed | Đã xử lý | Ignored | Bỏ qua |
| Linked | Đã liên kết | Revoked | Đã thu hồi | Unlinked | Đã gỡ liên kết |
| Resigned | Nghỉ việc | Suspended | Tạm ngừng | New | Mới |
| Planned | Kế hoạch | Released | Đã phát hành | Available | Khả dụng |
| Reserved | Đã giữ chỗ | Consumed | Đã dùng hết | Returned | Đã trả |
| Packed | Đã đóng gói | Paid | Đã thanh toán | Unpaid | Chưa thanh toán |
| Valid | Hợp lệ | Invalid | Không hợp lệ | Error | Lỗi |
| Warning | Cảnh báo | Info | Thông tin | Terminated | Chấm dứt |
| Obsolete | Hết dùng | Posted | Đã ghi sổ | Issued | Đã xuất |
| Matched | Khớp | Variance | Chênh lệch | Inbound / Outbound | Đến / Đi |
| true / false | Có / Không | | | | |

Giữ tiếng Anh chỉ cho: mã chứng từ / mã định danh (DH-…, KHB-…), đơn vị chuẩn (kg, °C), tên riêng (Zalo, Telegram, Excel, PDF). Tiêu đề cột CSV chỉ có nhãn tiếng Việt (không kèm tên cột kỹ thuật); tên file xuất = tên màn không dấu + ngày (vd `Diem-giao-hang_2026-11-24.csv`).
