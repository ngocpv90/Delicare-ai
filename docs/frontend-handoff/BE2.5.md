# Bàn giao frontend – BE2.5: ATTP, vệ sinh, hộp / khay

Quy ước chung (đăng nhập, `Result` / `Results`, lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt, quyền theo `listMenu`): `P0-P2.md` mục 1.
Form: số `62.5`, ngày `yyyy-MM-dd`. Action có **danh sách lồng** nhận **JSON** (`FoodSafetyChecklistResult/Record`, `CleaningBatch/RecordResults`); còn lại nhận **form**, đọc = GET query.
Thử nhanh cả luồng (1 bộ `[DEMO]` tự đủ, đã chạy trên DB dev): `docs/api-tests/be2.5-foodsafety-packaging.http`; thu hồi hộp sau bàn giao trong luồng đầy đủ: `pilot-flow.http` mục P10.

Quyền (không thêm mục menu / quyền – màn hình đã có trong seed): `#/qc-checklist-result`, `#/qc-cleaning`, `#/qc-cleaning-result`, `#/qc-supplier-issue`,
`#/log-container-return`, `#/log-return-inspection`, `#/log-packaging-ledger`. `Get*` = Xem; action ghi = Sửa trên màn hình của controller, trừ các action ghi rõ "quyền Thêm" dưới đây.

Seed mới (chạy qua `deploy/db/scripts.order`): `DB/seed/FoodSafetyQa_seed.sql` – bộ checklist:
`QA_FG` Kiểm soát thành phẩm (Cảm quan ★, Trọng lượng suất ★, Lưu mẫu + người ký) · `PREP` Kiểm tra sơ chế · `PREP_AREA_CLEAN` Vệ sinh khu sơ chế · `BLAST_CHILL` Xả nguội ·
`KITCHEN_CLEAN` Vệ sinh bếp – 5S · `VS-KHAY` Vệ sinh khay / hộp 6 bước (★ = trọng yếu `isCritical`). QA sửa / thêm tiêu chí trên màn `#/qc-checklist`, `#/qc-checklist-item` (CRUD cũ).

## ⚠ Thay đổi hành vi (FE đang làm theo P6 / P7 / P8 cần biết)
1. **Bàn giao ghi sổ bao bì** (`DeliveryStop/Deliver`, Catalog 190): form có thêm `PackagingQuantity?`, `PackagingType?`. Trống → tự ghi `PackagingLedger` **OUT** = suất khách nhận × `PackagingUnitsPerMeal` (1),
   loại = `packagingType` của kết quả đóng gói của đơn (`PackingOrder/RecordResult`), không có → `KHAY`; đồng thời sinh phiếu thu hồi `ContainerReturn` **Chờ thu hồi** (`Expected`).
   `PackagingQuantity = 0` = hộp dùng 1 lần (không ghi). `listWarning` có "Sổ bao bì: giao N … cho khách (OUT), chờ thu hồi".
2. **Tiêu chí ATTP trọng yếu KHÔNG ĐẠT gắn mẻ giữ mẻ** (`FoodSafetyCriticalFailHoldsBatch` = true): mẻ Đạt → **Chờ QC** + `qcStatus` HOLD; mẻ đang nấu / chờ QC → HOLD; mẻ Đã đóng gói giữ trạng thái.
   Tới khi tiêu chí đó được **kiểm lại ĐẠT**: `ProductionBatch/Release`, `Pack`, `PackingOrder/*` → 400 "Tiêu chí ATTP trọng yếu chưa đạt…"; `DeliveryRoute/Dispatch` → 400 "Mẻ có tiêu chí ATTP trọng yếu chưa đạt: ME-… (…)".
   Chi tiết mẻ (`listBlocker`) và mẻ của chuyến (`openCriticalFailures[]` trong danh sách mẻ của chi tiết chuyến) hiện lý do.
3. **CRUD bị chặn (400, chỉ ghi qua nghiệp vụ):** `FoodSafetyChecklistResult` Insert / Update / Delete; `CleaningChecklistResult` Insert / Update / Delete; `ReturnInspection` Insert / Update / Delete;
   `CleaningBatch` Insert / Update (Delete chỉ mẻ Đang vệ sinh chưa ghi bước); `ContainerReturn` Insert / Update (Delete chỉ phiếu Chờ thu hồi); `PackagingLedger` Insert.
   `SupplierIssue` Insert luôn **Mới** (bỏ `IssueStatus` / `Resolution` / `ClosedAt` gửi lên), `ReportedAt` trống = bây giờ, `DueAt` trống = +2 ngày, `Severity` High / Medium / Low;
   Update giữ nguyên trạng thái / cách xử lý, sự cố Đóng → 400. Đổi trạng thái chỉ qua `SupplierIssue/Transition`. `GetAll` / `GetById` giữ nguyên.

## 1. Thực hiện checklist theo ca / mẻ `#/qc-checklist-result` (Catalog 161, 162)

| Chức năng | Action | Input | Ghi chú |
|---|---|---|---|
| Mở lần chạy | `GET FoodSafetyChecklistResult/GetRun` | `ChecklistId, CheckDate?, ShiftId?, ProductionBatchId?` | Trống ngày = hôm nay; có mẻ mà trống ca → ca của lệnh SX. → `ChecklistRunModel` |
| Ghi kết quả | `POST FoodSafetyChecklistResult/Record` (**JSON**, quyền Thêm) | `{ checklistId, checkDate?, shiftId?, productionBatchId?, workstationId?, listItem: [{ checklistItemId? \| itemCode, resultCode: PASS\|FAIL, actualValue?, correctiveAction?, evidenceUrl? }] }` | → `{ saved, listCriticalFailure[], run, listWarning[] }` |

`ChecklistRunModel`: `checklist`, `checkDate`, `shiftId/Code/Name`, `productionBatchId`, `batchNumber`, `dueAt` + `dueSource` (bộ có độ lệch: giờ xuất bếp của ca + `ChecklistOffsetsMinutes` – PREP −150′, PREP_AREA_CLEAN −90′, BLAST_CHILL −20′, KITCHEN_CLEAN +90′),
`isOverdue` (quá hạn mà chưa đủ tiêu chí bắt buộc), `listItem[]` { `item` (FoodSafetyChecklistItem: itemCode, itemName, sortOrder, isCritical, isRequired, expectedValue, unitCode), `result` (kết quả hiện tại hoặc null), `resultName`, `checkedByName`, `historyCount` },
`requiredCount`, `doneCount`, `passCount`, `failCount`, `isComplete`, `listCriticalFailure[]` (tên tiêu chí trọng yếu đang KHÔNG ĐẠT), `listPassRate7d[]` { `date`, `total`, `pass`, `rate` (% hoặc null) }.

Quy tắc ghi: mỗi tiêu chí = **1 dòng mới** (giữ lịch sử – kiểm lại sau khắc phục là dòng mới, dòng mới nhất là kết quả hiện tại); tiêu chí bắt buộc phải có kết quả (đã ghi trước hoặc trong lần này – nên lần đầu gửi đủ, lần kiểm lại chỉ gửi tiêu chí sửa);
FAIL bắt buộc `correctiveAction`; bộ phải Đang áp dụng ngày kiểm. **Không ghi qua API này:** `PRD_STAGE` (dùng `ProductionBatch/StartStage – FinishStage`), `PRD_BATCH` (lưu mẫu `RecordSampleRetained`, nhiệt độ lõi `RecordCoreTemperature`), `VS-KHAY` (`CleaningBatch/RecordResults`).
`listCriticalFailure[]` { `itemCode, itemName, actualValue, correctiveAction, batchNumber, batchAction` ("Đạt → Chờ QC, tạm giữ" / "Đã đóng gói – chặn xuất chuyến") }. Cảnh báo Telegram cho QA → Release 2 (chỉ hiện trong danh sách).

## 2. Vệ sinh khay / hộp `#/qc-cleaning`, `#/qc-cleaning-result` (Catalog 167–169)

| Chức năng | Action | Input | Ghi chú |
|---|---|---|---|
| Danh sách | `GET CleaningBatch/GetAll` (cũ) | `Status, StartDate, EndDate…` | |
| Chi tiết | `GET CleaningBatch/GetDetail?id=` | | → `CleaningBatchDetailModel` |
| Lập mẻ | `POST CleaningBatch/Create` | `ReturnInspectionId, ContainerQuantity?, ResponsibleEmployeeId?, ShiftId?` | Thường **tự lập** khi kiểm tra thu hồi (mục 3). Lập tay khi vệ sinh lại sau mẻ Không đạt. Số khay ≤ dùng được còn lại (trả về − hỏng, trừ khay đã vào mẻ chưa bị loại). Số `VS-YYMMDD-<CA>`, lần sau trong ca `-2`, `-3` |
| Ghi bước | `POST CleaningBatch/RecordResults` (**JSON**, quyền Thêm `#/qc-cleaning-result`) | `{ id, checklistId?, workstationId?, listItem: [{ itemCode \| checklistItemId, resultCode: PASS\|FAIL, actualValue?, chemicalUsed?, correctiveAction?, evidenceUrl? }] }` | Mẻ **Đang vệ sinh**. **Đi lần lượt**: bước chỉ ghi được khi các bước bắt buộc đứng trước đã ĐẠT (gửi nhiều bước 1 lần được). FAIL bắt buộc khắc phục; `KHU_KHUAN` ĐẠT bắt buộc `chemicalUsed` |
| Kết luận | `POST CleaningBatch/Complete` | `Id, QcResult: PASS\|REJECT, Note?` | PASS: mọi bước ĐẠT → **Hoàn tất / PASS**, sổ bao bì **AVAILABLE** (số khay sạch). REJECT: `Note` bắt buộc → Hoàn tất / REJECT (ghi KHÔNG ĐẠT ở bước 6), khay chờ lập mẻ mới |

6 bước (`VS-KHAY`): `NHAN_QR` → `PHAN_LOAI` → `RUA_SO` → `RUA_MAY` → `KHU_KHUAN` → `KIEM_TRA_NHAP_KHO`.
`CleaningBatchDetailModel`: `batch` (cleaningBatchNumber, containerQuantity, status, qcResult, startDate, endDate, responsibleEmployeeId), `statusName`, `qcResultName`, `packagingType`, `responsibleName`, `inspection`, `containerReturn`, `customerName`, `deliveryNumber`,
`listStep[]` { `item`, `result` (CleaningChecklistResult mới nhất), `resultName`, `checkedByName`, `historyCount`, `requiresChemical` }, `stepCount`, `passedStepCount`, `canPass`, `allowedActions` (`recordResults`, `complete` khi Đang vệ sinh), `listWarning`.
Trạng thái mẻ (STATUS_CATALOG chưa có – PROPOSED D-54): `InProgress` Đang vệ sinh → `Completed` Hoàn tất; `qcResult` `PASS` Đạt – sẵn sàng dùng lại / `REJECT` Không đạt – vệ sinh lại.

## 3. Thu hồi + kiểm tra khay / hộp `#/log-container-return`, `#/log-return-inspection` (Catalog 194, 195)

| Chức năng | Action | Input | Ghi chú |
|---|---|---|---|
| Danh sách | `GET ContainerReturn/GetList` | `Status?, CustomerId?, From?, To?, Page, PageSize` | → `{ listItem[] { return, statusName, customerName, deliveryNumber, packagingType, inspectionResult, inspectionResultName }, totalRecords, statusCounts }` |
| Chi tiết | `GET ContainerReturn/GetDetail?id=` | | → `{ return, statusName, customerName, deliveryNumber, packagingType, outstandingOfDelivery, outstandingOfCustomer, listInspection[], listCleaningBatch[], listLedger[], allowedActions, listWarning }` |
| Ghi thu hồi | `POST ContainerReturn/Record` (quyền Thêm) | `DeliveryOrderId, ReturnedQuantity, DamagedQuantity?, ExpectedQuantity?, ReturnDate?, PackagingType?, Note?` | Đơn giao **Đã giao**. Loại trống = loại đã giao (OUT) của đơn giao (nhiều loại → bắt buộc chọn). Không thu quá số khay loại đó còn ở khách. Phiếu Chờ thu hồi → **Đã thu hồi** (không có thì lập phiếu mới). Sổ bao bì **IN**: phần tốt (GOOD) + phần hỏng (DAMAGED). Còn khay ở khách → cảnh báo |
| Kiểm tra | `POST ContainerReturn/Inspect` (quyền Thêm `#/log-return-inspection`) | `Id, InspectionResult: OK\|SHORTAGE\|DAMAGED\|DIRTY\|REJECTED, DamagedQty?, MissingQty?, InspectorEmployeeId?, ResponsibleEmployeeId?, Note?` | Phiếu **Đã thu hồi** → **Đã kiểm tra** + `ReturnInspection`. Khác OK bắt buộc `Note`. Hỏng trống = hỏng lúc thu; thiếu trống = phải thu − trả về. Khay dùng được (trả về − hỏng; REJECTED = 0) → **tự lập mẻ vệ sinh** (`listCleaningBatch`) |

Trạng thái phiếu (STATUS_CATALOG): `Expected` Chờ thu hồi → `Returned` Đã thu hồi → `Inspected` Đã kiểm tra. Loại bao bì lưu ở tag `[BAO_BI:<loại>]` đầu `return.note` (GAP-PKG-02) – FE hiển thị `packagingType`, không tự tách tag.

## 4. Sổ bao bì theo khách `#/log-packaging-ledger` (Catalog 196)

| Chức năng | Action | Input | Ghi chú |
|---|---|---|---|
| Sổ (dòng) | `GET PackagingLedger/GetAll` (cũ) | `CustomerId, OrderId, DeliveryRouteId, TransactionType, PackagingType…` | Sổ chỉ ghi thêm |
| Số dư | `GET PackagingLedger/GetBalance` | `CustomerId?, PackagingType?, From?, To?, OverThresholdOnly?` | → `{ from, to, listItem[], totalOutstanding, overThresholdCount }` |
| Ghi tay | `POST PackagingLedger/Record` (quyền Thêm) | `CustomerId, TransactionType: OUT\|IN\|LOSS, PackagingType, Quantity, DeliveryOrderId?, ConditionCode?, Reason` | Tồn đầu kỳ ở khách (OUT), khách trả ngoài phiếu (IN), thất thoát (LOSS). IN / LOSS không vượt số còn ở khách. Sửa sai = ghi dòng ngược lại. → `{ ledger, balance, listWarning }` (vượt ngưỡng thất thoát → cảnh báo "báo khách và chuyển kế toán tính phí") |

`listItem[]` (theo khách × loại): `customerId, customerCode, customerName, packagingType, outQuantity, inQuantity, damagedInQuantity, lossQuantity, outstanding` (= OUT − IN − LOSS, lũy kế tới `To`),
`periodOutQuantity, periodLossQuantity, lossRatePct` (= thất thoát ÷ giao trong kỳ × 100), `lossThresholdPct` (khách: `Customer.ExternalData` `{"containerLossMaxPct": n}`, không có → 3%), `isOverThreshold`.
Loại phát sinh (`ReferenceLists:PackagingTransactionType`): `OUT` Giao cho khách, `IN` Khách trả, `LOSS` Thất thoát, `AVAILABLE` Sẵn sàng dùng lại (mẻ vệ sinh đạt, không gắn khách). Loại bao bì gợi ý: `ReferenceLists:PackagingType` (chữ tự do vẫn nhận).

## 5. Sự cố nhà cung cấp `#/qc-supplier-issue` (Catalog 170)

| Chức năng | Action | Input | Ghi chú |
|---|---|---|---|
| Danh sách | `GET SupplierIssue/GetList` | `Status?, SupplierId?, Severity?, Overdue?, From?, To?, SearchText?, Page, PageSize` | → `{ listItem[] { issue, statusName, severityName, supplierName, goodsReceiptNumber, purchaseOrderNumber, ownerName, isOverdue, hoursToDue, allowedTransitions[] }, totalRecords, statusCounts, overdueCount }` – quá hạn lên đầu |
| Thêm / sửa | `POST SupplierIssue/Insert`, `PUT SupplierIssue/Update` (cũ) | | Xem "Thay đổi hành vi" 3 |
| Chuyển trạng thái | `POST SupplierIssue/Transition` | `Id, To: InProgress\|Resolved\|Closed, Resolution?, OwnerId?` | → 1 `listItem` |

Máy trạng thái (PROPOSED D-54 e): `Open` Mới → `InProgress` Đang xử lý (người phụ trách = `OwnerId` = User.Id, trống = người thao tác) / → `Resolved`; `InProgress` → `Resolved` (bắt buộc `Resolution`);
`Resolved` → `Closed` (ghi `closedAt`) hoặc mở lại `InProgress`. `isOverdue` = `dueAt` < bây giờ và chưa Đã xử lý / Đóng (tính khi xem – không job). Sự cố tự sinh khi QC nhận hàng không đạt (T15) giữ như cũ.
