# DELICARE – Đặc tả luồng nghiệp vụ End-to-End

**Phiên bản:** Giai đoạn 2 – Solution Design (bản nháp chờ BA/PO duyệt), 14/11/2026

**Nguồn căn cứ:**
- `20261113_structure_db_appdelicare.sql`: cấu trúc vật lý, gồm 124 bảng và 2.264 cột.
- `20261113_Dictionary_DB_AppDelicare.xlsx`: nhóm phân loại và các quan hệ đề xuất.
- `DeliCare_Database_Dictionary.xlsx`: 75 Business Object và 12 luồng quan hệ từ BRD.
- Các quyết định của PO ngày 14/11/2026.

## Quy ước

| Ký hiệu | Ý nghĩa |
|---|---|
| **[SQL]** | Giá trị lấy từ DEFAULT trong SQL |
| **[BRD]** | Giá trị lấy từ tài liệu BRD |
| **[PROPOSED – BA/PO CONFIRM]** | Đề xuất của đội thiết kế, PO đã đồng ý ở mức đề xuất. BA cần chốt lại trong SRS |
| `Table.Column` | Tên đúng như trong SQL nguồn. Cột *deprecated* không được dùng (xem Mapping.xlsx, sheet `05_Cot_Deprecated`) |

**Nguyên tắc chung**
- Không có FK vật lý. Mọi kiểm tra quan hệ do lớp ứng dụng .NET thực hiện.
- Cặp cột trùng nghĩa: dùng cột mới (`…Number`, `…Quantity`, `UnitId`, `MaterialLotId`, `…EmployeeId`, `StorageZoneId`, `CustomerDeliveryLocationId`, `IngredientCategoryId`/`DishCategoryId`, `QcStatus`).
- Mã trạng thái: giữ đúng cách viết của nguồn (SQL hoặc BRD). Mã do đội thiết kế đề xuất viết theo kiểu PascalCase.
- Mọi phê duyệt đi qua `ApprovalRequest` → `ApprovalStep` → `ApprovalDecision`. `ApprovalRequest.BusinessType` cho biết loại nghiệp vụ, `ApprovalRequest.BusinessId` trỏ tới Id của bản ghi nghiệp vụ.

---

## Luồng 1 – Quản lý khách hàng và đơn hàng

**Chuỗi bước:** Khách hàng → Hợp đồng → Điểm giao → Kế hoạch suất ăn → Nhận số lượng theo ngày/ca → Đối soát → Chốt đơn hàng

| Hạng mục | Nội dung |
|---|---|
| Người thực hiện | Nhân viên kinh doanh, quản lý kinh doanh (phê duyệt hợp đồng), khách hàng (gửi số lượng qua Zalo/Telegram – P8), kế toán (đối soát) |
| Điều kiện đầu vào | `Customer.Status` = Active. Hợp đồng có hiệu lực (`Contract.EffectiveFrom` ≤ ngày ăn ≤ `Contract.EffectiveTo`) |
| Dữ liệu đầu vào | Hồ sơ khách hàng, người liên hệ, điểm giao (`CustomerDeliveryLocation.DeliveryWindowFrom/To`), điều khoản suất ăn (`ContractMeal.MealType`, `UnitPrice`), chính sách giá (`PricePolicie`), dự báo (`Forecast`) |
| Màn hình | `crm-customer`, `crm-contact`, `crm-location`, `crm-contract`, `crm-contract-meal`, `crm-price-policy`, `crm-forecast`, `crm-order`, `crm-order-line`, `crm-reconciliation` |
| Kết quả | Đơn hàng ở trạng thái Locked (đã chốt số lượng), làm đầu vào cho Luồng 2 và Luồng 5 |
| Bảng DB | `Customer`, `CustomerContact`, `CustomerDeliveryLocation`, `Contract`, `ContractMeal`, `PricePolicie`, `Forecast`, `Order`, `OrderLine`, `OrderReconciliation`, `ApprovalRequest` |

**Trạng thái**

| Đối tượng | Luồng trạng thái | Nguồn |
|---|---|---|
| `Contract.Status` | Draft → PendingApproval → Active → Expired / Terminated | PROPOSED |
| `Forecast.Status` | Draft → Published → Superseded (version mới qua `Forecast.VersionNo`) | PROPOSED |
| `Order.Status` | Draft → Submitted → Confirmed → Locked → InProduction → Delivered → Reconciled; hoặc Cancelled | PROPOSED |
| `OrderReconciliation.Status` | Open → Matched / Variance → Closed | PROPOSED |

**Điều kiện chuyển bước**
1. Draft → Submitted: đơn có ít nhất một `OrderLine` với `Quantity` > 0. Mỗi dòng có `ShiftId` và `MealType`.
2. Submitted → Confirmed: kinh doanh kiểm tra số lượng so với `Forecast` và `ContractMeal`. Theo BRD, **chỉ đơn Confirmed mới được đưa vào kế hoạch sản xuất**.
3. Confirmed → Locked: đến giờ chốt số lượng. Sau thời điểm này, mọi thay đổi phải đi qua `MenuChangeRequest` hoặc yêu cầu điều chỉnh. *(Chưa có cột lưu giờ chốt – GAP-F-07)*
4. Delivered → Reconciled: có `ProofOfDeliverie`, `OrderReconciliation` được tính từ OrderedQuantity / `ProducedQty` / `PackedQty` / DeliveredQuantity / AcceptedQuantity.

**Ngoại lệ**
- Khách hàng tăng hoặc giảm suất sau khi đã chốt: tạo `MenuChangeRequest` hoặc yêu cầu phê duyệt (`ApprovalRequest.BusinessType` = 'ORDER_CHANGE' – PROPOSED).
- Hợp đồng hết hiệu lực: chặn tạo đơn mới và cảnh báo.
- Chênh lệch đối soát vượt ngưỡng (`OrderReconciliation.VariancePercent`): chuyển kế toán xử lý. *(Ngưỡng chưa có nơi cấu hình – GAP)*

---

## Luồng 2 – Lập thực đơn và tính nhu cầu nguyên liệu

**Chuỗi bước:** Thực đơn → Món ăn → Công thức → Định lượng → Số lượng suất ăn → Tính nhu cầu nguyên liệu → So sánh tồn kho → Xác định thiếu hụt

| Hạng mục | Nội dung |
|---|---|
| Người thực hiện | Bếp trưởng (món ăn, công thức), chuyên viên dinh dưỡng / quản lý bếp (thực đơn), quản lý bếp (phê duyệt), nhân viên kế hoạch (MRP) |
| Điều kiện đầu vào | `Dishe.Status` = Active. Có `Recipe` với `IsActive` = 1, Status = Approved/Locked, và ngày sản xuất nằm trong khoảng `EffectiveFrom`–`EffectiveTo`. `Menu` đã Published và được gán qua `MenuAssignment` |
| Dữ liệu đầu vào | `RecipeLine.Quantity`, `UnitId`, `Recipe.YieldPercent`, `YieldQuantity`, `Ingredient.YieldPercent`, `StandardWastePercent`; số suất theo `ProductionPlanLine.PlannedQuantity`; tồn khả dụng `InventoryBalance.AvailableQuantity`; hàng đang về; `InventoryPolicie.SafetyStock` |
| Màn hình | `menu-dish`, `menu-recipe`, `menu-recipe-line`, `menu-menu`, `menu-menu-line`, `menu-assignment`, `scm-requirement` |
| Kết quả | `MaterialRequirement` theo từng nguyên liệu, gồm RequiredQuantity, AvailableQuantity, ExpectedIncomingQuantity, PurchaseQuantity. Thiếu hụt là đầu vào cho Luồng 3 |
| Bảng DB | `DishCategorie`, `Dishe`, `Recipe`, `RecipeLine`, `Ingredient`, `UomConversion`, `Menu`, `MenuLine`, `MenuAssignment`, `ProductionPlan`, `ProductionPlanLine`, `MaterialRequirement`, `InventoryBalance`, `InventoryPolicie`, `PurchaseOrderLine` |

**Quản lý phiên bản công thức** (không tạo bảng RecipeVersion)
- Mỗi phiên bản là một dòng `Recipe` mới, cùng `DishId` và `VersionNo` tăng dần.
- Khi duyệt, ghi `ApprovedBy` và `ApprovedAt`. Khi khóa, ghi `LockedAt`, `LockedBy` và lưu `SnapshotJson`.
- `ProductionOrder.RecipeSnapshotJson` lưu bản chụp công thức tại thời điểm ra lệnh sản xuất.

**Trạng thái**

| Đối tượng | Luồng trạng thái | Nguồn |
|---|---|---|
| `Recipe.Status` | Draft → PendingApproval → Approved → Locked → Obsolete | PROPOSED |
| `Menu.Status` | Draft → Approved → Published → Expired | PROPOSED |
| `MaterialRequirement.Status` | Calculated → Shortage / Sufficient → Requested (đã sinh đề nghị mua) | PROPOSED |

**Công thức tính nhu cầu** (PROPOSED – BA/PO CONFIRM):

- Nhu cầu gộp = Σ (số suất × `RecipeLine.Quantity` quy đổi về `Ingredient.BaseUnitId`) ÷ (`Ingredient.YieldPercent` / 100)
- Thiếu hụt = Nhu cầu gộp + `InventoryPolicie.SafetyStock` − AvailableQuantity − ExpectedIncomingQuantity
- Nếu Thiếu hụt > 0 thì ghi vào PurchaseQuantity.

**Ngoại lệ**
- Món không có công thức hiệu lực: chặn lập kế hoạch và cảnh báo bếp trưởng.
- Thiếu quy đổi đơn vị trong `UomConversion`: dòng nhu cầu bị đánh dấu lỗi.
- Khách hàng yêu cầu đổi món: tạo `MenuChangeRequest` (RequestStatus mặc định Pending **[SQL]**).

---

## Luồng 3 – Mua hàng

**Chuỗi bước:** Nhu cầu nguyên liệu → Đề nghị mua → Phê duyệt → Đơn mua → Nhà cung cấp giao hàng → Kiểm tra đầu vào → Nhập kho

| Hạng mục | Nội dung |
|---|---|
| Người thực hiện | Nhân viên kế hoạch hoặc kho (lập đề nghị), quản lý kho hoặc giám đốc (phê duyệt), nhân viên mua hàng (đơn mua), nhân viên kho (nhận hàng), QC (kiểm tra đầu vào) |
| Điều kiện đầu vào | `MaterialRequirement.PurchaseQuantity` > 0, hoặc đề nghị mua thủ công. NCC có `SupplierMaterial.Status` = Active; ưu tiên NCC có `IsPreferred` = 1 |
| Dữ liệu đầu vào | `SupplierMaterial.UnitPrice`, `MinimumOrderQuantity`, `LeadTimeDays`; `PurchaseRequestLine`; `PurchaseOrderLine` |
| Màn hình | `scm-requirement`, `scm-pr`, `wf-inbox`, `scm-po`, `scm-gr`, `qc-inspection`, `scm-lot` |
| Kết quả | `GoodsReceipt` được ghi sổ. Các lô `MaterialLot` có QcStatus = PASS, kèm `InventoryTransaction` loại RECEIPT |
| Bảng DB | `MaterialRequirement`, `PurchaseRequest`, `PurchaseRequestLine`, `ApprovalRequest`, `ApprovalStep`, `ApprovalDecision`, `PurchaseOrder`, `PurchaseOrderLine`, `Supplier`, `SupplierMaterial`, `GoodsReceipt`, `GoodsReceiptLine`, `QCInspection`, `QCInspectionLine`, `MaterialLot`, `InventoryTransaction`, `SupplierIssue` |

**Trạng thái**

| Đối tượng | Luồng trạng thái | Nguồn |
|---|---|---|
| `PurchaseRequest.Status` | Draft → PendingApproval → Approved / Rejected → Converted | PROPOSED |
| `ApprovalRequest.RequestStatus` | Pending **[SQL]** → InProgress → Approved / Rejected / Cancelled | PROPOSED |
| `ApprovalStep.DecisionStatus` | Waiting **[SQL]** → Approved / Rejected / Skipped | PROPOSED |
| `PurchaseOrder.Status` | Draft → Approved → Sent → PartiallyReceived → Received → Closed; hoặc Cancelled | PROPOSED |
| `GoodsReceipt.Status` | Draft → QCPending → Accepted / PartiallyAccepted / Rejected → Posted | PROPOSED |
| `QCInspection.Result` | PASS / HOLD / REJECT | **[BRD]** |

**Điều kiện chuyển bước**
1. Đề nghị mua chỉ chuyển sang Approved khi mọi `ApprovalStep` đều Approved.
2. Phiếu nhập chỉ được Posted khi `QCInspection.Result` = PASS cho từng dòng. Với từng dòng: `GoodsReceiptLine.AcceptedQuantity` + `RejectedQuantity` = `ReceivedQuantity`.
3. Khi Posted, hệ thống sinh `MaterialLot` (LotNumber, `ManufactureDate`, `ExpiryDate`, `UnitCost`, QcStatus) và `InventoryTransaction` (TransactionType = RECEIPT, `QuantityIn`).

**Ngoại lệ**
- QC kết luận HOLD: lô bị giữ (`MaterialLot.Status` = OnHold), chưa được xuất kho.
- QC kết luận REJECT: tạo `SupplierIssue` (IssueStatus mặc định Open **[SQL]**) và trả hàng (InventoryTransaction loại RETURN).
- Giao thiếu hoặc giao trễ: PO chuyển PartiallyReceived và ghi nhận sự cố nhà cung cấp.
- Từ chối phê duyệt: đề nghị mua về Rejected, người lập chỉnh sửa và gửi lại.

---

## Luồng 4 – Quản lý kho

**Chuỗi bước:** Nhập kho → Quản lý lô và hạn sử dụng → Tồn kho → Giữ chỗ nguyên liệu → Xuất kho cho sản xuất → Kiểm kê → Đối soát chênh lệch

| Hạng mục | Nội dung |
|---|---|
| Người thực hiện | Nhân viên kho, quản lý kho (phê duyệt kiểm kê và điều chỉnh), kế toán kho |
| Điều kiện đầu vào | Có lô `MaterialLot` với QcStatus = PASS. `InventoryPolicie` đã cấu hình (`UseFefo`, `ExpiryWarningDays`, `MinimumStock`, `MaximumStock`, `ReorderPoint`) |
| Màn hình | `scm-lot`, `scm-inv-balance`, `scm-inv-txn`, `scm-reservation`, `scm-issue`, `scm-issue-line`, `scm-stockcount`, `scm-stockcount-line`, `qc-temperature` |
| Kết quả | Tồn kho chính xác theo kho, vị trí và lô. Chênh lệch kiểm kê được ghi sổ bằng ADJUSTMENT |
| Bảng DB | `Warehouse`, `StorageZone`, `StorageLocation`, `MaterialLot`, `InventoryTransaction`, `InventoryBalance`, `InventoryPolicie`, `MaterialReservation`, `MaterialIssue`, `MaterialIssueLine`, `StockCount`, `StockCountLine`, `TemperatureLog` |

**Quy tắc từ BRD**
- `InventoryBalance` được tính từ sổ cái `InventoryTransaction` và **không được sửa trực tiếp**. Mockup không có nút Sửa trên màn hình tồn kho.
- Loại giao dịch kho **[BRD]**: RECEIPT, ISSUE, TRANSFER_IN, TRANSFER_OUT, ADJUSTMENT, WASTE, RETURN.
- Xuất kho theo FEFO khi `InventoryPolicie.UseFefo` = 1.

**Trạng thái**

| Đối tượng | Luồng trạng thái | Nguồn |
|---|---|---|
| `MaterialLot.Status` | Available / OnHold / Expired / Consumed | PROPOSED |
| `MaterialReservation.Status` | Reserved → PartiallyIssued → Issued → Released | PROPOSED |
| `MaterialIssue.Status` | Draft → Issued → Cancelled | PROPOSED |
| `StockCount.Status` | Draft → Counting → PendingApproval → Posted | PROPOSED |
| `StockCount.CountType` | DAILY / WEEKLY / MONTHLY / ADHOC | **[BRD]** |

**Điều kiện chuyển bước**
1. Giữ chỗ: tổng `MaterialReservation.ReservedQuantity` không được vượt `InventoryBalance.AvailableQuantity`. Khi giữ chỗ, ReservedQuantity của số dư tăng tương ứng.
2. Xuất kho: mỗi `MaterialIssueLine` phải chỉ rõ `MaterialLotId`. Hệ thống sinh `InventoryTransaction` loại ISSUE.
3. Ghi sổ kiểm kê: chênh lệch `StockCountLine.VarianceQuantity` ≠ 0 bắt buộc có `VarianceReason`. Phiếu phải được phê duyệt (`StockCount.ApprovedBy`) trước khi sinh ADJUSTMENT.

**Ngoại lệ**
- Lô sắp hết hạn (trong `ExpiryWarningDays`): cảnh báo. Lô đã hết hạn: tự chuyển Expired, ghi WASTE.
- Nhiệt độ ngoài ngưỡng (`TemperatureLog.IsWithinRange` = 0): cảnh báo, bắt buộc nhập `CorrectiveAction`.
- Chuyển kho và điều chỉnh tồn không có bảng phiếu riêng (GAP-F-03, GAP-F-04). Mockup minh họa bằng dòng sổ cái và gắn nhãn GAP.

---

## Luồng 5 – Sản xuất

**Chuỗi bước:** Kế hoạch sản xuất → Lệnh sản xuất → Phân ca → Cấp phát nguyên liệu → Chế biến → Ghi nhận sản lượng → Ghi nhận hao hụt → Tính giá thành

| Hạng mục | Nội dung |
|---|---|
| Người thực hiện | Quản lý bếp (kế hoạch, phê duyệt), bếp trưởng (lệnh sản xuất, điều độ mẻ), nhân viên chế biến, QC, kế toán giá thành |
| Điều kiện đầu vào | Đơn hàng đã Confirmed hoặc Locked. Có công thức hiệu lực. Nguyên liệu đã giữ chỗ đủ. Nhân sự đã được phân ca (`ShiftAssignment`) |
| Màn hình | `prod-plan`, `prod-plan-line`, `prod-order`, `hr-shift-assign`, `scm-reservation`, `scm-issue`, `prod-batch` (bảng điều độ theo ca × dây chuyền), `prod-output`, `prod-waste`, `cost-actual`, `cost-meal`, `scm-consumption-var` |
| Kết quả | Thành phẩm được chấp nhận (`ProductionOutput.AcceptedQuantity`), giá thành thực tế (`ActualCost`), giá thành mỗi suất (`MealCost.CostPerMeal`, `GrossMargin`) |
| Bảng DB | `ProductionPlan`, `ProductionPlanLine`, `ProductionOrder`, `ProductionLine`, `KitchenWorkstation`, `Shift`, `ShiftAssignment`, `MaterialReservation`, `MaterialIssue`, `ProductionBatche`, `ProductionOutput`, `MealWasteRecord`, `ActualCost`, `StandardCost`, `MealCost`, `MaterialConsumptionVariance` |

**Trạng thái**

| Đối tượng | Luồng trạng thái | Nguồn |
|---|---|---|
| `ProductionPlan.Status` | Draft → Approved → Released → Completed | PROPOSED |
| `ProductionOrder.Status` | Planned → Released → InProgress → Completed → Closed; hoặc Cancelled | PROPOSED |
| `ProductionBatche.Status` | Planned → Cooking → QCPending → Passed / Failed → Packed | PROPOSED |
| `ProductionBatche.QcStatus` | PASS / HOLD / REJECT | **[BRD]** |

**Điều kiện chuyển bước**
1. Released: đã giữ chỗ đủ nguyên liệu cho lệnh, và `ProductionLine.Status` = Active **[SQL]**.
2. Cooking → QCPending: đã ghi `ProductionBatche.ActualQuantity` và `EndDate`.
3. Passed: thành phẩm được chuyển sang Luồng 7 (`PackingOrder` sinh từ `ProductionBatche` – **[BRD]**).
4. Tính giá thành khi đóng lệnh: `ActualCost` lấy từ `MaterialCost` (theo ISSUE × `UnitCost`), `LaborCost`, `OverheadCost`, `WasteCost` và `OtherCost`. `MealCost.CostPerMeal` = `TotalActualCost` ÷ `AcceptedMealQuantity` **[BRD]**.

**Ngoại lệ**
- Mẻ không đạt QC (REJECT): ghi `MealWasteRecord` (WasteType, `Reason`, `Disposition`) và sinh mẻ bổ sung.
- Tiêu hao vượt định mức: `MaterialConsumptionVariance.VariancePercent` > ngưỡng thì cảnh báo. *(Ngưỡng chưa có cột – GAP)*
- Thiếu chi phí bao bì, logistics, điện nước trong `ActualCost` (GAP-C-01).

---

## Luồng 6 – QC và ATTP

**Chuỗi bước:** Kiểm tra nguyên liệu đầu vào → Kiểm tra trong sản xuất → Kiểm tra thành phẩm → Kiểm tra nhiệt độ → Checklist ATTP → Xử lý không đạt → Cho phép hoặc từ chối chuyển bước

| Hạng mục | Nội dung |
|---|---|
| Người thực hiện | Nhân viên QC/ATTP, trưởng QC, bếp trưởng |
| Điều kiện đầu vào | Bộ checklist `FoodSafetyChecklist` có Status = Active **[SQL]**, kèm các tiêu chí `FoodSafetyChecklistItem` |
| Màn hình | `qc-inspection`, `qc-inspection-line`, `qc-checklist`, `qc-checklist-item`, `qc-checklist-result`, `qc-temperature`, `qc-cleaning`, `qc-cleaning-result`, `qc-supplier-issue` |
| Kết quả | Quyết định cho phép hoặc chặn bước kế tiếp (nhập kho, chuyển mẻ sang đóng gói, đưa khay vào sử dụng lại) |
| Bảng DB | `QCInspection`, `QCInspectionLine`, `FoodSafetyChecklist`, `FoodSafetyChecklistItem`, `FoodSafetyChecklistResult`, `TemperatureLog`, `CleaningBatche`, `CleaningChecklistResult`, `SupplierIssue`, `MaterialLot`, `ProductionBatche` |

**Điểm kiểm soát (gate)**

| Điểm kiểm soát | Bảng / cột | Kết quả chặn |
|---|---|---|
| Đầu vào | `QCInspection.Result`, `QCInspectionLine.Temperature`, `Weight`, `Appearance`, `Packaging`, `ExpiryDate` | REJECT hoặc HOLD thì không được nhập kho |
| Trong sản xuất | `FoodSafetyChecklistResult.ResultCode` (theo ca `ShiftId` và mẻ `ProductionBatchId`) | Không đạt thì mẻ chuyển QCPending và bắt buộc có `CorrectiveAction` |
| Thành phẩm | `ProductionBatche.QcStatus` | REJECT thì không tạo `PackingOrder` |
| Nhiệt độ | `TemperatureLog.TemperatureC` so với `MinAllowedC` / `MaxAllowedC` | `IsWithinRange` = 0 thì cảnh báo và cần `EvidenceUrl` |
| Vệ sinh | `CleaningBatche.QcResult`, `CleaningChecklistResult` | Không đạt thì khay chưa được sử dụng lại |

**Ngoại lệ**
- Lỗi do nhà cung cấp: tạo `SupplierIssue`.
- `FoodSafetyChecklistResult.ChecklistId` và `ChecklistItemId` chưa có quan hệ xác nhận (GAP-R).

---

## Luồng 7 – Đóng gói và giao hàng

**Chuỗi bước:** Kết quả sản xuất → Đóng gói → Xếp hàng lên xe → Lập tuyến → Giao hàng → Xác nhận giao → POD → Thu hồi khay/hộp → Kiểm tra hàng trả về

| Hạng mục | Nội dung |
|---|---|
| Người thực hiện | Nhân viên đóng gói, điều phối giao hàng, tài xế / nhân viên giao nhận, người nhận phía khách hàng, nhân viên rửa khay |
| Điều kiện đầu vào | `ProductionBatche.QcStatus` = PASS. Có `DeliveryOrder` cho đơn hàng (một Order có thể có nhiều DeliveryOrder – **[BRD]**) |
| Màn hình | `log-packing-order`, `log-packing-result`, `log-delivery-order`, `log-route`, `log-stop`, `log-loading`, `log-execution`, `log-event`, `log-pod`, `log-container-return`, `log-return-inspection`, `qc-cleaning`, `log-packaging-ledger`, `log-complaint` |
| Kết quả | POD được xác nhận, khay được thu hồi và vệ sinh, sổ bao bì cập nhật, đối soát đơn hàng (quay lại Luồng 1) |
| Bảng DB | `PackingOrder`, `PackingResult`, `DeliveryOrder`, `DeliveryRoute`, `DeliveryStop`, `DeliveryLoading`, `DeliveryExecution`, `DeliveryEvent`, `ProofOfDeliverie`, `ContainerReturn`, `ReturnInspection`, `CleaningBatche`, `PackagingLedger`, `CustomerComplaint`, `ComplaintHistory`, `Vehicle` |

**Trạng thái**

| Đối tượng | Luồng trạng thái | Nguồn |
|---|---|---|
| `PackingOrder.Status` | Planned → Packing → Packed | PROPOSED |
| `DeliveryRoute.Status` | Planned → Loading → InTransit → Completed | PROPOSED |
| `DeliveryStop.StopStatus` | Planned **[SQL]** → Arrived → Delivered / Failed | PROPOSED |
| `DeliveryExecution.Status` | READY → LOADED → DISPATCHED → ARRIVED → DELIVERED / FAILED | **[BRD]** |
| `ContainerReturn.Status` | Expected → Returned → Inspected | PROPOSED |
| `ReturnInspection.InspectionResult` | đủ / thiếu / hỏng / bẩn / không đạt | **[BRD]** – SQL chưa có cột số lượng "bẩn" |
| `CustomerComplaint.ComplaintStatus` | Open **[SQL]** → InProgress → Resolved → Closed; hoặc Rejected | PROPOSED |

**Điều kiện chuyển bước**
1. Packed: `PackingResult.PackedQuantity` + `DamagedQty` + RejectedQuantity = `PlannedQty`.
2. DISPATCHED: mọi `DeliveryLoading` của tuyến ở trạng thái Loaded, `DeliveryRoute.ActualStart` đã được ghi.
3. DELIVERED: có `ProofOfDeliverie` (`ReceiverName`, `SignatureUrl` hoặc `PhotoUrl`, AcceptedQuantity). Mỗi sự kiện ghi một dòng `DeliveryEvent` (`Latitude`, `Longitude`, `TemperatureC`).
4. Thu hồi: `ContainerReturn.ExpectedQuantity`, `ReturnedQuantity`, `DamagedQuantity` → `ReturnInspection` → `CleaningBatche` → QC → sẵn sàng (**[BRD]**).

**Ngoại lệ**
- Giao thiếu: `DeliveryStop.ShortageQty` > 0 thì lập `CustomerComplaint` (Severity mặc định Medium **[SQL]**, `SlaDueAt`).
- Giao thất bại (FAILED): ghi `DeliveryEvent` và điều phối lại.
- Không có GPS liên tục (GAP-F-05). Tuyến không có điểm đầu/cuối (GAP-C). Xếp xe không liên kết `PackingOrder` (GAP-C).

---

## Luồng 8 – Nhân sự và ca làm việc

**Chuỗi bước:** Lập ca → Phân công nhân sự → Chấm công → Xin nghỉ → Đổi ca → Phê duyệt → Theo dõi nhân sự theo ca

| Hạng mục | Nội dung |
|---|---|
| Người thực hiện | Nhân sự, quản lý ca hoặc bếp trưởng, nhân viên (tự đăng ký qua Web, Mobile hoặc Bot) |
| Điều kiện đầu vào | Có 4 ca trong `Shift` (`StartTime`/`EndTime` kiểu `time`). `Employee.Status` = Active |
| Màn hình | `hr-shift`, `hr-shift-assign`, `hr-attendance`, `hr-leave`, `hr-swap`, `wf-inbox` |
| Kết quả | Lịch ca đã xác nhận, chấm công theo ca, bảng theo dõi nhân sự có mặt theo ca, đối chiếu với `ProductionPlan` |
| Bảng DB | `Shift`, `ShiftAssignment`, `AttendanceEvent`, `LeaveRequest`, `ShiftSwapRequest`, `ApprovalRequest`, `ApprovalStep`, `ApprovalDecision`, `Employee`, `KitchenWorkstation` |

**Trạng thái**

| Đối tượng | Luồng trạng thái | Nguồn |
|---|---|---|
| `ShiftAssignment.AssignmentStatus` | Planned **[SQL]** → Confirmed → CheckedIn → Completed / Absent | PROPOSED |
| `LeaveRequest.RequestStatus` | Pending **[SQL]** → Approved / Rejected / Cancelled | PROPOSED |
| `ShiftSwapRequest.RequestStatus` | Pending **[SQL]** → Approved / Rejected / Cancelled | PROPOSED |
| `AttendanceEvent.EventType` | CHECK_IN / CHECK_OUT | PROPOSED |

**Điều kiện chuyển bước**
1. Không phân trùng ca: ràng buộc UNIQUE `UQ_ShiftAssignment_Employee_Shift_Date` trên (EmployeeId, ShiftId, WorkDate) **[SQL]**.
2. Đơn nghỉ hoặc đổi ca được duyệt: cập nhật các `ShiftAssignment` liên quan (`CurrentShiftAssignmentId` ↔ `ProposedShiftAssignmentId`).
3. Chấm công gắn với `ShiftAssignmentId`. Đi muộn hoặc về sớm được tính so với `Shift.StartTime` / `EndTime`.

**Ngoại lệ**
- Ca thiếu người so với định biên: cảnh báo. *(Không có bảng định biên theo ca – GAP)*
- Chấm công qua thiết bị hoặc Bot: `AttendanceEvent.ExternalEventId` (mã hệ thống ngoài).

---

## Luồng 9 – Điều hành và báo cáo

**Chuỗi bước:** Dữ liệu đơn hàng + kho + sản xuất + QC + giao hàng + nhân sự → Tổng hợp KPI → Dashboard điều hành

| Hạng mục | Nội dung |
|---|---|
| Người thực hiện | Ban giám đốc, các trưởng bộ phận; job tổng hợp KPI (hệ thống) |
| Điều kiện đầu vào | `KpiMetricDefinition` có `IsActive` = 1 **[SQL]** và đang trong hiệu lực (`EffectiveFrom`/`EffectiveTo`) |
| Màn hình | `dash-exec`, `kpi-definition`, `kpi-daily`, `kpi-monthly`, `kpi-refresh` |
| Kết quả | `KpiDailySnapshot` / `KpiMonthlySnapshot` theo `BranchId` (→ `OrganizationUnit`, PO xác nhận) và `MetricCode`, có `TargetValue` và `VarianceValue` |
| Bảng DB | `KpiMetricDefinition`, `KpiDailySnapshot`, `KpiMonthlySnapshot`, `AnalyticsRefreshLog` và các bảng nguồn của Luồng 1–8 |

**Bộ KPI theo BRD** (`KpiMetricDefinition.DomainCode`)

| Nhóm | Chỉ tiêu |
|---|---|
| PRODUCTION | Production Achievement, Yield, Waste, Productivity, Delay |
| INVENTORY | Inventory Accuracy, Stockout, Expiry, Turnover, Days of Inventory |
| DELIVERY | On-time Delivery, Delivery Accuracy, Failed Delivery, Average Delay |
| SUPPLIER | On-time, Quality, Reject Rate, Price Variance, Lead Time |
| FINANCIAL | Cost/Meal, Food Cost %, Waste Cost, Margin/Meal, Margin % |

**Ngoại lệ**
- Job làm mới thất bại: `AnalyticsRefreshLog.RunStatus` = Failed và hiển thị cảnh báo dữ liệu cũ (`SourceWatermark`).
- `KpiDailySnapshot.DataQualityStatus` khác OK: dashboard hiển thị cờ chất lượng dữ liệu.

---

## Phụ lục – Điểm chờ BA/PO xác nhận trong các luồng

1. Toàn bộ trạng thái gắn nhãn PROPOSED.
2. Giờ chốt số lượng đơn hàng (Luồng 1). Ngưỡng chênh lệch đối soát và tiêu hao (Luồng 1, Luồng 5).
3. Công thức MRP (Luồng 2) và phương pháp phân bổ chi phí nhân công, chi phí chung (Luồng 5).
4. Số cấp phê duyệt theo `BusinessType` và hạn mức (Luồng 3, 4, 8). Chưa có bảng mẫu quy trình phê duyệt.
5. Các GAP chức năng: chuyển kho, điều chỉnh tồn, GPS, SLA, địa điểm vận hành, định biên ca. Xem Gap Register.
