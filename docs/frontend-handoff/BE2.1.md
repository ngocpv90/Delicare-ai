# Bàn giao frontend – BE2.1: lấp chỗ thiếu của luồng (đóng gói, xếp xe, đơn giao tay, lập chuyến nhiều xe, thực hiện giao, kế hoạch theo ca, đề nghị mua có dự phòng, nhiệt độ tự do, hao hụt, bảng đơn, thực đơn, phản ánh, duyệt, truy xuất)

Quy ước chung (đăng nhập, `Result` / `Results`, lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt, quyền theo `listMenu`): `P0-P2.md` mục 1.
Form: số `62.5`, ngày `yyyy-MM-dd`, ngày giờ `yyyy-MM-ddTHH:mm:ss`. Action có danh sách lồng nhau nhận **JSON** (ghi rõ bên dưới), còn lại **form** (`application/x-www-form-urlencoded`).
Thử nhanh cả luồng (1 bộ `[DEMO]` tự đủ, đã chạy trên DB dev): `docs/api-tests/be2.1-flow-gaps.http`. Luồng Pilot đã cập nhật (đóng gói theo lệnh + xếp xe trước khi xuất phát): `docs/api-tests/pilot-flow.http`.

Quyền: theo `PermissionConvention` – `Get*` = Xem, `Create*` = Thêm, còn lại = Sửa trên màn hình của controller (ScreenMap); ngoại lệ ghi ở cột "Quyền". Mục menu của mọi màn hình dưới đây **đã có** trong `ObjectMenu` (không thêm seed).

## ⚠ Thay đổi hành vi so với T17 / T19 (FE đang làm theo P7 / P8 cần sửa)
1. **Xuất phát bắt buộc xếp hàng lên xe** (`BusinessRules:DeliveryRequireLoading = true`): chuyến Kế hoạch → `DeliveryRoute/Load` → Đang xếp → `Dispatch`. `allowedActions` của chuyến: Kế hoạch = `recordTemperature, load, updateStops, update, delete, exportDeliveryNote` (**không còn `dispatch`**); Đang xếp = `recordTemperature, load, dispatch, exportDeliveryNote`. `listBlocker` có thêm "Chưa xếp hàng lên xe (DeliveryRoute/Load): PH-…".
2. `listStop[]` của chi tiết chuyến có thêm `loadedQty` (null = chưa xếp) và `executionStatus` (READY / LOADED / DISPATCHED / ARRIVED / DELIVERED / FAILED).
3. 1 đơn hàng có thể có **nhiều đơn giao** (lập tay / tách). `GetReadyOrders` trả đơn còn suất chưa lập đơn giao, `mealQuantity` = **số suất còn lại**. Đơn hàng → Đã giao khi mọi suất đã nằm trong đơn giao và mọi đơn giao còn hiệu lực đã giao.
4. `ProductionBatch/Pack` bị chặn khi mẻ còn **lệnh đóng gói chưa xong** (Kế hoạch / Đang đóng gói) – hoàn tất bằng `PackingOrder/RecordResult` (mẻ tự Đã đóng gói khi chia hết). Mẻ chưa có lệnh đóng gói vẫn `Pack` thẳng như T17.
5. CRUD `Insert` bị chặn (trả 400 chỉ đường) cho: `PackingOrder`, `PackingResult`, `DeliveryLoading`, `DeliveryExecution` (cả Update / Delete), `DeliveryOrder` (Insert), `MealWasteRecord` (Insert – dùng `Record`). `TemperatureLog/Insert` vẫn dùng được nhưng ngưỡng / `isWithinRange` / người ghi **tính ở server** (bỏ giá trị client gửi).

## 1. `log-packing-order` / `log-packing-result` – Đóng gói (Catalog 171–175)

| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Danh sách lệnh | `GET PackingOrder/GetList?PackingDate=&Status=&OrderId=&ProductionBatchId=` | Xem | `listData[]` = mục 1.1 |
| Lệnh của 1 mẻ | `GET PackingOrder/GetByBatch?id=<mẻ>` | Xem | `data` = mục 1.2 |
| Sinh lệnh từ mẻ | `POST PackingOrder/GenerateFromBatch` (form `ProductionBatchId, PackingDate?, Note?`) | Thêm | Mẻ **Đạt QC** (`Passed`, QcStatus PASS), nhiệt độ lõi gần nhất ≥ 70 °C, đã ghi sản lượng. Số phần đạt **còn lại** chia cho các đơn **Đã chốt / Đang sản xuất** của ngày + ca của mẻ theo tỉ lệ suất món (GAP-PCK-01); mỗi đơn 1 lệnh `PH-YYMMDD-NNNN` Kế hoạch. Đã chia hết → 400 |
| Lập lệnh tay | `POST PackingOrder/Create` (form `ProductionBatchId, OrderId, PlannedQty, PackingDate?, Note?`) | Thêm | Σ số phần các lệnh còn hiệu lực ≤ số phần đạt của mẻ (vượt → 400); đơn Đã chốt / Đang SX |
| Bắt đầu | `POST PackingOrder/Start` (form `Id`) | Sửa | Kế hoạch → Đang đóng gói |
| Ghi kết quả | `POST PackingOrder/RecordResult` (form `Id, PackedQuantity, DamagedQty?, RejectedQuantity?, PackagingType?, QualityStatus?, ProductionOutputId?, Note?`) | Sửa | `PackedQuantity + DamagedQty + RejectedQuantity` **= số kế hoạch** của lệnh. Ghi `PackingResult` (acceptedQty = packed); hỏng + loại → hao hụt `PACKING_DAMAGE`. Mẻ chia hết + mọi lệnh Đã đóng gói → **mẻ Đã đóng gói** (`listWarning` "Mẻ … đã đóng gói xong") |
| Hủy | `POST PackingOrder/Cancel` (form `Id, Reason`) | Sửa | Chỉ lệnh **Kế hoạch**, lý do bắt buộc; số phần trả về mẻ |

Trạng thái lệnh: `Planned` Kế hoạch → `Packing` Đang đóng gói → `Packed` Đã đóng gói; `Cancelled` Hủy (PROPOSED).

### 1.1 Lệnh đóng gói (`PackingOrderDetailModel`)
`packingOrder` (`id, packingOrderNumber, productionBatchId, orderId, plannedQty, packingDate, status, note`), `statusName, batchNumber, batchStatus, dishName, orderNumber, customerName`,
`listResult[]` (`plannedQty, packedQuantity, damagedQty, rejectedQuantity, acceptedQty, packagingType, qualityStatus, remark`), `allowedActions[]` (Kế hoạch: `start, recordResult, cancel`; Đang đóng gói: `recordResult`).

### 1.2 Mẻ + lệnh (`PackingBatchModel`) – kết quả của mọi thao tác ghi
`productionBatchId, batchNumber, batchStatus, acceptedQuantity` (số phần đạt), `allocatedQuantity` (Σ lệnh còn hiệu lực), `remainingQuantity`, `listPackingOrder[]` (mục 1.1), `listWarning[]`.

## 2. `log-route` – Chuyến xe (bổ sung Catalog 177–180)

| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Lập chuyến nhiều xe theo khu vực | `POST DeliveryRoute/Plan` (**JSON**) | Thêm | Mục 2.1 |
| Lập 1 chuyến (đã có) | `POST DeliveryRoute/Create` (**JSON**) | Thêm | Thêm `listDeliveryOrderId: []` = đơn giao lập tay / tách (Kế hoạch, chưa gán chuyến, cùng ngày). Đơn trong `listOrderId` → đơn giao mới cho **phần suất còn lại**. Tài xế đã có chuyến trùng khung giờ (`plannedStart`–`plannedEnd`) cùng ngày → 400 |
| Đổi thứ tự / giờ điểm dừng | `PUT DeliveryRoute/UpdateStops` (**JSON** `{ "id": "<chuyến>", "listStop": [ { "deliveryOrderId": "…", "sequenceNo": 1, "plannedArrival": null } ] }`) | Sửa | Chỉ chuyến **Kế hoạch**; số ≥ 1, không trùng (điểm không gửi giữ số cũ) |
| Xếp hàng lên xe | `POST DeliveryRoute/Load` (**JSON** `{ "id": "<chuyến>", "employeeId": null, "listItem": [ { "deliveryOrderId": "…", "loadedQty": 34 } ], "note": null }`) | Sửa | Chuyến Kế hoạch / Đang xếp → **Đang xếp**. `listItem` trống = các đơn giao **chưa xếp** (số = suất kế hoạch); có danh sách = xếp / xếp lại đúng các đơn đó. Xếp khác kế hoạch → cảnh báo. Trả `data`: `route` (chi tiết chuyến), `listLoading[]` (`deliveryOrderId, loadedQty, loadingTime, vehicleCode, employeeId, status = Loaded`), `listExecution[]` (`deliveryOrderId, status = LOADED, driverName, vehicleCode`) |
| Xuất phát (đã có) | `POST DeliveryRoute/Dispatch` | Sửa | Thêm điều kiện: mọi đơn giao của chuyến đã xếp lên xe. Thực hiện giao → DISPATCHED (`dispatchedAt, departedAt`) |
| Sửa (đã có) | `PUT DeliveryRoute/Update` | Sửa | Thêm kiểm tài xế trùng khung giờ |

### 2.1 `POST DeliveryRoute/Plan` (JSON)
```json
{ "deliveryDate": "2026-12-30", "shiftId": null, "strategy": "byArea",
  "listVehicle": [ { "vehicleId": "<xe 1>", "driverId": "<nhân viên – tùy chọn>" }, { "vehicleId": "<xe 2>" } ] }
```
- Hàng cần xếp = đơn **Đang sản xuất** của ngày (dòng thuộc ca) còn suất chưa lập đơn giao + đơn giao Kế hoạch chưa gán chuyến.
- **Khu vực** (GAP-DLV-09, PROPOSED): `Customer.AreaId` nếu có; không có → phần "Quận / Huyện / Thị xã" (rồi "TP.") trong địa chỉ điểm giao; không có → phần cuối địa chỉ.
- Khu vực lớn xếp trước, cả khu vào xe còn trống nhiều nhất chứa vừa; không vừa → chia theo điểm (đơn lớn trước); không xe nào vừa → xe trống nhiều nhất + cảnh báo vượt tải. Xe không khai `Capacity` = không giới hạn. Không tự tách đơn.
- Kết quả `data`: `deliveryDate`, `listRoute[]` (chi tiết chuyến như P8 mục 1.1, mỗi xe có hàng 1 chuyến `CX-`), `listArea[]` (`area, mealQuantity, stopCount, listVehicleCode[]`), `listWarning[]`.

## 3. `log-delivery-order` – Đơn giao (Catalog 183–187)

| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Danh sách | `GET DeliveryOrder/GetList?DeliveryDate=&OrderId=&CustomerId=&DeliveryRouteId=&Status=&Unassigned=true` | Xem | `listData[]` = mục 3.1. Không lọc `Status` → bỏ đơn giao Hủy |
| Lập đơn giao tay | `POST DeliveryOrder/Create` (**JSON** `{ "orderId": "…", "customerDeliveryLocationId": null, "plannedDeliveryTime": null, "mealQuantity": 100, "listLine": [ { "orderLineId": "…", "quantity": 60 } ], "note": null }`) | Thêm | Đơn hàng **Đã chốt / Đang SX**; điểm giao thuộc khách (trống = điểm của đơn); số suất = Σ `listLine` / `mealQuantity` / trống = **toàn bộ suất còn lại**; vượt suất còn lại → 400. Số `PH-` theo ngày giao. Trả `data` = mục 3.2 |
| Tách | `POST DeliveryOrder/Split` (**JSON** `{ "id": "…", "listPart": [ { "mealQuantity": 60, "plannedDeliveryTime": "…T11:45:00", "customerDeliveryLocationId": null }, { "mealQuantity": 40 } ] }`) | Sửa | Chỉ đơn giao **Kế hoạch chưa xếp lên xe**; ≥ 2 phần, mỗi phần > 0, Σ = số suất gốc. Phần đầu giữ đơn gốc (và điểm dừng nếu đã gán chuyến), phần sau = đơn giao mới chưa gán chuyến |
| Gán / chuyển chuyến | `POST DeliveryOrder/AssignRoute` (form `Id, DeliveryRouteId, SequenceNo?`) | Sửa | Chuyến đích **Kế hoạch**, cùng ngày; chuyển từ chuyến khác thì chuyến cũ cũng phải Kế hoạch. Trống `SequenceNo` = cuối chuyến; các điểm khác tự đánh số lại. Vượt tải → cảnh báo |
| Hủy | `POST DeliveryOrder/Cancel` (form `Id, Reason`) | Sửa | Chỉ đơn giao **Kế hoạch chưa xếp lên xe**, lý do bắt buộc; gỡ điểm dừng khỏi chuyến; suất trả lại đơn hàng |

Trạng thái đơn giao: `Planned` Kế hoạch → `InTransit` Đang giao → `Delivered` Đã giao / `Failed` Thất bại; `Cancelled` Hủy (PROPOSED).

### 3.1 `DeliveryOrderItemModel`
`deliveryOrder` (bản ghi; `note` có tag `[SUAT:n]` ở đầu – dùng `noteText` để hiển thị), `statusName, mealQuantity, orderNumber, orderStatus, customerName, locationName, address, area, deliveryRouteId, routeNo, routeStatus, deliveryStopId, sequenceNo, loadedQty, isLoaded, noteText`, `allowedActions[]` (`split, assignRoute, cancel` khi Kế hoạch chưa xếp).

### 3.2 Kết quả thao tác (`DeliveryOrderActionModel`)
`listDeliveryOrder[]` (mục 3.1 – các đơn giao liên quan), `listRoute[]` (chi tiết các chuyến bị ảnh hưởng, không có `listBatch`), `listWarning[]`.

## 4. `log-execution` – Thực hiện giao (Catalog 188)
`GET DeliveryExecution/GetBoard?DeliveryDate=yyyy-MM-dd&Status=&DeliveryRouteId=` (Xem). `data`: `deliveryDate`, `statusCounts` (`{ "READY": n, "LOADED": n, "DISPATCHED": n, … }` – đếm trước khi lọc `Status`), `listItem[]`:
`deliveryOrderId, deliveryNumber, deliveryOrderStatus, mealQuantity, orderId, orderNumber, customerId, customerName, deliveryLocationId, locationName, address, deliveryRouteId, routeNo, routeStatus, vehicleCode, driverName, deliveryStopId, sequenceNo, stopStatus, plannedArrival, actualArrival, loadedQty, status, statusName, execution` (bản ghi `DeliveryExecution`: `dispatchedAt, departedAt, arrivedAt, deliveredAt, remark`), `pod`.
Trạng thái: `READY` Chờ xếp hàng → `LOADED` Đã xếp lên xe → `DISPATCHED` Đang giao → `ARRIVED` Đã đến → `DELIVERED` Đã giao / `FAILED` Thất bại. Đến điểm / bàn giao / thất bại vẫn gọi `DeliveryStop/Arrive / Deliver / Fail` (P8 mục 2) – trạng thái thực hiện giao tự cập nhật.

## 5. `prod-plan` – Sinh kế hoạch theo ca (Catalog 137)
`POST ProductionPlan/Generate` (form `ProductionDate, OrganizationUnitId?, Note?`) – quyền **Thêm** `prod-plan`.
- Theo quyết định của anh (GAP-MRP-01 / GAP-PRD-01): **1 kế hoạch `KH-YYMMDD` / ngày** (spec ghi mỗi ca 1 kế hoạch – PROPOSED giữ quyết định của anh); số phần từng ca ở `listLine[].listShift` + `listShift` của chi tiết (như `ProductionPlan/GetDetail`, P7); `listLine[].shiftId` có khi món thuộc đúng 1 ca. Phát hành từng ca bằng `Release` như T17.1.
- Nguồn: đơn **Đã xác nhận / Đã chốt** của ngày × thực đơn (như MRP). Chưa có kế hoạch → tạo Nháp; Nháp chưa tính nhu cầu NL → cập nhật dòng; đã Duyệt trở đi / đã tính nhu cầu NL → 400.
- Trả chi tiết kế hoạch (P7) + `listWarning` (dòng đơn không có thực đơn, đơn Nháp / Đã gửi chưa tính).

## 6. `scm-requirement` – Đề nghị mua chọn dòng + dự phòng (Catalog 107)
`POST MaterialRequirement/CreatePurchaseRequest` (**JSON** `{ "productionPlanId": "…", "listRequirementId": [], "bufferPercent": 5, "note": null }`) – quyền Sửa `scm-requirement`.
- `listRequirementId` trống = mọi dòng thiếu (số mua > 0); dòng không thuộc kế hoạch → 400; không có dòng thiếu nào được chọn → 400. `bufferPercent` 0–100.
- Số đề nghị = số mua × (1 + bufferPercent / 100), làm tròn lên 4 số lẻ. Đề nghị Nháp của kế hoạch được cập nhật tại chỗ (dòng không chọn xóa mềm, dòng nhu cầu về "Thiếu"); đề nghị đã Chờ duyệt / Duyệt / Lập PO → 400 (dùng `CalculateTopUp`).
- Trả kết quả nhu cầu như `MaterialRequirement/GetDetail` (P5). Duyệt đề nghị mua 2 cấp → BE2.3.

## 7. `qc-temperature` – Nhiệt độ tự do (Catalog 165)
`POST TemperatureLog/Record` (form `StorageZoneId? | StorageLocationId? | MaterialLotId? | ProductionBatchId?, MeasurementType, TemperatureC, MeasuredAt?, DeviceCode?, SourceChannel?, CorrectiveAction?, EvidenceUrl?`) – quyền **Thêm** `qc-temperature`.
- Ít nhất 1 đối tượng đo. Loại đo theo `MetaData/GetReferenceLists?name=TemperatureMeasurementType` (STORAGE, HOT_HOLDING, BLAST_CHILL, PACKING_AREA, AMBIENT). `CORE` / `DELIVERY` / `RECEIVING` → 400 (ghi qua `ProductionBatch/RecordCoreTemperature`, `DeliveryRoute/RecordTemperature`, nhận hàng).
- Ngưỡng: khu kho `TemperatureMin…Max` (vị trí → khu của vị trí); `HOT_HOLDING` ≥ 60 °C; còn lại không ngưỡng (`isWithinRange` null). Ngoài ngưỡng mà không ghi khắc phục → **vẫn lưu** + `listWarning`.
- `data`: `log` (bản ghi `TemperatureLog`: `minAllowedC, maxAllowedC, isWithinRange, recordedBy, measuredAt`), `listWarning[]`. Danh sách: `GET TemperatureLog/GetAll` (CRUD).

## 8. `prod-waste` – Hao hụt (Catalog 156)
| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Ghi hao hụt | `POST MealWasteRecord/Record` (form `ProductionBatchId? | ProductionOrderId? | DishId, ShiftId?, WasteType, QuantityMeals?, QuantityKg?, Reason?, Disposition?, OccurredAt?`) | Thêm | Loại theo `ReferenceLists WasteType` (thêm `PACKING_DAMAGE`); số suất hoặc kg > 0; gắn mẻ → món / ca / lệnh SX lấy từ mẻ, `wasteQty` của mẻ cộng dồn (mẻ phải đã bắt đầu nấu). `data`: `record, wasteTypeName, dishName, shiftCode, batchNumber, productionOrderNumber, recordedByName` |
| Danh sách | `GET MealWasteRecord/GetList?From=&To=&DishId=&ShiftId=&ProductionBatchId=&WasteType=` | Xem | `data`: `listItem[]`, `totalMeals, totalKg, mealsByType` (biểu đồ tổng hợp 157 → để sau) |
| Ghi theo mẻ (đã có) | `POST ProductionBatch/RecordWaste` | Sửa | Giữ như T17 |

## 9. `crm-order` – Bảng đơn (Catalog 90)
`GET Order/GetBoard?MealDate=&MealDateFrom=&MealDateTo=&Status=&CustomerId=&BranchId=&SearchText=` (Xem). Không lọc ngày → 30 ngày trước tới 7 ngày tới.
`data`: `listColumn[]` theo thứ tự `Draft, Submitted, Confirmed, Locked, InProduction, Delivered, Reconciled, Cancelled` (`status, statusName, count, totalQuantity, listItem[]` = `id, orderNumber, customerId, customerName, deliveryLocationId, locationName, mealDate, deliveryDate, totalQuantity, totalAmount, status`), `statusCounts`, `totalRecords`. `BranchId` lọc theo `Customer.BranchId`.

## 10. `menu-menu` – Danh sách thực đơn (Catalog 82)
`GET Menu/GetList?Status=&EffectiveOn=&SearchText=` (Xem). `listData[]`: `menu` (bản ghi), `statusName, lineCount, dishCount, assignedCustomerCount` (khách khác nhau có phân công còn hiệu lực tại `EffectiveOn`, không Inactive), `orderedMeals` (Σ suất dòng đơn có thực đơn này, đơn không Hủy).

## 11. `log-complaint` – Danh sách phản ánh (Catalog 197)
`GET CustomerComplaint/GetList?Status=&Severity=&CustomerId=&Overdue=true&From=&To=&SearchText=&Page=1&PageSize=50` (Xem).
`data`: `listItem[]` (`complaint, statusName, severityName, customerName, orderNumber, deliveryNumber, routeNo, assignedToName, isOverdue, hoursToDue` – âm = quá hạn), `totalRecords, statusCounts, overdueCount`. Quá hạn xếp trước, rồi hạn SLA gần nhất. `isOverdue` = `slaDueAt` < bây giờ và chưa Đã xử lý / Đóng / Từ chối.

## 12. `wf-inbox` – Chi tiết + hủy yêu cầu duyệt (Catalog 211, 213)
| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Chi tiết | `GET ApprovalRequest/GetDetail?id=` | Đăng nhập (service kiểm: người gửi / người có quyền Duyệt mục của yêu cầu / Xem `wf-inbox` / admin) | `data`: `request, statusName` (Chờ duyệt / Đã duyệt / Từ chối / Đã hủy), `requestedByName, screenId, title, listStep[], listDecision[]` (`decision` + `decidedByName`), `listItem[]`, `business` (`table` = PurchaseOrder / Recipe / Order, `id, record, lines`), `proposalJson`, `canDecide, canCancel` |
| Hủy | `POST ApprovalRequest/Cancel` (form `Id, Reason`) | Đăng nhập (service kiểm người gửi / admin) | Chỉ khi **Chờ duyệt**, lý do bắt buộc. Bản ghi nghiệp vụ về **Nháp** (PO / công thức; đơn lệch suất: giữ số cũ), yêu cầu → `Cancelled`, bước → `Cancelled`, ghi `ApprovalDecision` `CANCEL` |

## 13. `scm-lot` / `prod-batch` – Lô + truy xuất 2 chiều (Catalog 118, 119)
| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Danh sách lô | `GET MaterialLot/GetList?IngredientId=&SupplierId=&Status=&QcStatus=&ExpiringWithinDays=&SearchText=&InStock=true` | Xem | `listData[]`: `lot` (bản ghi, dùng `lotNumber`, `qcStatus`), `ingredientCode, ingredientName, supplierName, onHandQuantity, availableQuantity, daysToExpiry, isExpired, isBlocked`. Sắp xếp hạn dùng gần nhất trước |
| Truy xuất xuôi (lô → khách) | `GET MaterialLot/Trace?id=<lô>` | Xem | `TraceModel` (dưới) |
| Truy xuất ngược (mẻ → NCC) | `GET ProductionBatch/Trace?id=<mẻ>` | Xem `prod-batch` | `TraceModel` |

`TraceModel`: `direction` (FORWARD / BACKWARD), `root`, các danh sách nút `listSupplier, listPurchaseOrder, listGoodsReceipt, listLot, listIssue, listProductionOrder, listBatch, listPackingOrder, listOrder, listDeliveryOrder, listCustomer, listTemperature` – mỗi nút: `id, type, number, name, status, date, quantity, note, inferred`; `listWarning[]`.
`inferred = true` = liên kết **suy ra** (mẻ đóng gói thẳng không có lệnh đóng gói → đơn / khách theo ngày + ca + món; số mẻ không có trong ghi chú phiếu xuất → mọi mẻ của lệnh SX) – hiển thị nghiêng / chú thích "suy luận" (GAP-TRC-01). Chưa có mã QR (spec GAP-F-10).
