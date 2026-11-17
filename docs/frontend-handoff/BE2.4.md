# Bàn giao frontend – BE2.4: kho & sản xuất mở rộng

Quy ước chung (đăng nhập, `Result` / `Results`, lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt, quyền theo `listMenu`): `P0-P2.md` mục 1.
Form: số `62.5`, ngày `yyyy-MM-dd`, danh sách lặp khóa (`EmployeeIds=<id>&EmployeeIds=<id>`). Mọi action mới nhận **form**, đọc = GET query.
Thử nhanh cả luồng (1 bộ `[DEMO]` tự đủ, đã chạy trên DB dev): `docs/api-tests/be2.4-inventory-production.http`; giá vốn suất sau đối soát: `pilot-flow.http` mục P9.

Quyền: `PermissionConvention` (`Get*` = Xem, còn lại = Sửa trên màn hình của controller). Không thêm mục menu / quyền mới – màn hình đã có trong seed:
`#/prod-order`, `#/prod-batch`, `#/scm-reservation`, `#/scm-consumption-var`, `#/cost-actual`, `#/cost-meal`, `#/scm-lot`.

Seed mới (chạy qua `deploy/db/scripts.order`): `DB/seed/ProductionStage_seed.sql` – checklist `PRD_STAGE` gồm 3 công đoạn `SO_CHE` (Sơ chế) → `NAU` (Nấu) → `CHIA_SUAT` (Chia suất / Đóng gói).

## ⚠ Thay đổi hành vi (FE đang làm theo P4 / P5 / P7 cần biết)
1. **Phát hành kế hoạch giữ chỗ nguyên liệu** (`ProductionPlan/Release`, cấu hình `ProductionReserveOnRelease` = true): mỗi (lệnh SX, nguyên liệu) 1 dòng `MaterialReservation`.
   `InventoryBalance.reservedQuantity` / `availableQuantity` **không còn luôn = 0 / = tồn** – phần đã giữ phân bổ FEFO lên dòng tồn. Thiếu tồn → vẫn phát hành, `listWarning` có "… giữ chỗ X được a / b kg – thiếu c".
   Chi tiết kế hoạch có thêm `listReservation`.
2. **Xuất kho tay / cho lệnh khác không lấy được phần đã giữ** (`MaterialIssue/Create`, `Post` → 400 "Không đủ tồn khả dụng … còn …"). Xuất cho chính lệnh (`ProductionBatch/CreateMaterialIssue`, `MaterialIssue/Create` có `productionOrderId`) dùng phần đã giữ; ghi sổ xong giữ chỗ → `PartiallyIssued` / `Issued`.
3. **MRP trừ hàng sắp về từ PO** (`MaterialRequirement/Calculate` / `CalculateTopUp`): `expectedIncomingQuantity` = Σ dòng PO Đã duyệt / Đã gửi / Nhận một phần có `expectedDeliveryDate` ≤ ngày SX, trừ phần đã nhận đạt. PO chưa có ngày giao → không tính + cảnh báo. Số mua có thể nhỏ hơn trước.
4. Mẻ có trạng thái mới **`Cancelled` ("Hủy")** (khi hủy lệnh). Lệnh SX có **`Closed` ("Đóng")** sau khi đóng lệnh. Giữ chỗ: `Reserved` / `PartiallyIssued` / `Issued` / `Released` (Đã giữ / Đã xuất một phần / Đã xuất / Đã giải phóng).
5. **Công đoạn theo tổ**: khi mẻ đã ghi công đoạn (hoặc bật `ProductionRequireStages`), `ProductionBatch/Finish` cần công đoạn Nấu xong, `ProductionBatch/Pack` cần Chia suất xong (400). Mẻ không ghi công đoạn chạy như cũ.
6. CRUD `ActualCost`, `MealCost`, `MaterialConsumptionVariance`, `MaterialReservation` **Insert / Update → 400** (chỉ sinh qua nghiệp vụ). `GetAll` / `GetById` giữ nguyên.

## 1. Lệnh sản xuất `#/prod-order` (Catalog 142, 144, 145, 153)

| Chức năng | Action | Input | Ghi chú |
|---|---|---|---|
| Chi tiết | `GET ProductionOrder/GetDetail?id=` | | + `listReservation`, `listVariance`, `actualCost`, `allowedActions`, `listWarning` |
| Giữ chỗ bổ sung | `POST ProductionOrder/Reserve` | `Id` | Lệnh Phát hành / Đang SX; dòng giữ chưa xuất được giữ thêm tới đủ (sau khi hàng về) |
| Hủy | `POST ProductionOrder/Cancel` | `Id, Reason` (≥ 3 ký tự) | Chỉ lệnh **Phát hành** có mọi mẻ Kế hoạch, chưa có phiếu xuất Đã xuất (hủy phiếu bằng `MaterialIssue/Cancel` trước). Phiếu Nháp → Hủy, mẻ → Hủy, giải phóng giữ chỗ. Ca không còn lệnh → kế hoạch về **Đã duyệt** (phát hành lại ca đó được) |
| Tính giá thành | `POST ProductionOrder/CalculateCost` | `Id` | Lệnh Đang SX / Hoàn thành / Đóng → `ActualCostListModel` { `breakdown` (tổng lệnh), `listData` (từng mẻ) } |
| Đóng lệnh | `POST ProductionOrder/Close` | `Id, Note?` | Chỉ **Hoàn thành** → Đóng: sinh chênh lệch tiêu hao + giá thành, giải phóng giữ chỗ dư → `{ detail, actualCost, listVariance }` |

`allowedActions`: Phát hành → `reserve`, `cancel` (bỏ `cancel` khi có mẻ đã bắt đầu); Đang SX → `reserve`, `calculateCost`; Hoàn thành → `calculateCost`, `close`; Đóng → `calculateCost`.

```json
"listReservation": [ { "id": "…", "productionOrderNumber": "PH-261117-0161", "ingredientName": "Thịt gà", "unitName": "kg",
  "requiredQuantity": 97.5, "reservedQuantity": 97.5, "shortQuantity": 0, "status": "Reserved", "statusName": "Đã giữ" } ],
"actualCost": { "materialCost": 9100000, "laborCost": 0, "overheadCost": 0, "wasteCost": 10000, "otherCost": 0, "totalCost": 9100000,
  "acceptedPortions": 645, "costPerPortion": 14108.5271, "standardCostPerPortion": 2000, "calculatedAt": "…" }
```
`wasteCost` = suất hao hụt × giá chuẩn / phần – **chỉ để phân tích, không cộng vào `totalCost`** (NVL của phần hao hụt đã nằm trong `materialCost`) – PROPOSED D-53 (f).

## 2. Giữ chỗ `#/scm-reservation` (Catalog 126)
`GET MaterialReservation/GetAll?ProductionOrderId=&IngredientId=&Status=&Page=&PageSize=` (SP cũ): `reservedQuantity` = số **còn đang giữ**, `requiredQty` = số cần lúc giữ.
Kế hoạch: `GET ProductionPlan/GetDetail` → `listReservation` (mọi lệnh của kế hoạch).

## 3. Mẻ – công đoạn theo tổ `#/prod-batch`

| Chức năng | Action | Input |
|---|---|---|
| Bắt đầu công đoạn | `POST ProductionBatch/StartStage` | `Id, StageCode (SO_CHE / NAU / CHIA_SUAT), TeamId?, KitchenWorkstationId?, EmployeeIds?(≤ 10), At?` |
| Xong công đoạn | `POST ProductionBatch/FinishStage` | `Id, StageCode, EmployeeIds?, At?` |
| Tiến độ bếp theo tổ | `GET ProductionBatch/GetStageBoard?ProductionDate=&ShiftId=&TeamId=` | |

Quy tắc (PROPOSED D-53 e): không bắt đầu công đoạn khi công đoạn trước (bắt buộc / đã bắt đầu) chưa xong. Sơ chế: mẻ Kế hoạch / Đang nấu. **Bắt đầu Nấu = bắt đầu mẻ** (Kế hoạch → Đang nấu, lệnh → Đang SX). Chia suất: chỉ mẻ **Đạt** QC (nhiệt độ lõi ≥ 70 °C đã kiểm). Tổ trống = tổ mặc định (`BusinessRules:ProductionStageTeams`: SO_CHE → OPS-KIT-PREP, NAU → OPS-KIT-COOK, CHIA_SUAT → OPS-KIT-PORT; món cơm / canh: gửi `TeamId` = tổ OPS-KIT-RICE). Nhân viên không thuộc tổ → cảnh báo.

`ProductionBatch/GetDetail` có thêm `listStage` + `allowedActions` `startStage` / `finishStage`:
```json
"listStage": [ { "stageCode": "SO_CHE", "stageName": "Sơ chế", "sortOrder": 1, "isRequired": true, "status": "Done", "statusName": "Đã xong",
  "teamId": "…", "teamCode": "OPS-KIT-PREP", "teamName": "Tổ Sơ chế", "kitchenWorkstationId": null, "employeeIds": ["…"], "employeeNames": ["Nguyễn Sơ Chế"],
  "startedAt": "2026-12-08T07:10:00", "startedBy": "…", "finishedAt": "2026-12-08T07:40:00", "finishedBy": "…", "minutes": 30 } ]
```
Trạng thái công đoạn: `NotStarted` / `InProgress` / `Done` (Chưa làm / Đang làm / Đã xong).

`GetStageBoard` → `{ productionDate, listTeam: [ { stageCode, stageName, teamId, teamCode, teamName, waiting, inProgress, done, inProgressPortions, donePortions } ],
listBatch: [ { batch (như GetBoard), listStage, currentStageCode } ], listWarning }`. `waiting` = mẻ có công đoạn hiện tại là công đoạn này, chưa bắt đầu.

## 4. Chênh lệch tiêu hao `#/scm-consumption-var` (Catalog 135)
`GET MaterialConsumptionVariance/GetList?ProductionOrderId=&From=&To=&IngredientId=&OverThresholdOnly=` (chỉ đọc, sinh khi đóng lệnh) →
`[ { productionOrderNumber, productionDate, dishName, ingredientName, unitName, standardQuantity, actualQuantity, varianceQuantity, variancePercent, varianceCost, isOverThreshold, reason } ]`.
Chuẩn = định lượng × **số phần nấu được** ÷ suất chuẩn × (1 + hao hụt %), thực tế = Σ phiếu xuất Đã xuất của lệnh; đơn vị = đơn vị xuất kho; `isOverThreshold` khi |%| > `ConsumptionVariancePctWarning` (5).

## 5. Giá thành thực tế `#/cost-actual` (Catalog 152)
`GET ActualCost/GetList?From=&To=&DishId=&ProductionOrderId=` → `{ listData: [ActualCostModel theo lệnh], breakdown: { materialCost, laborCost, overheadCost, wasteCost, otherCost, totalCost, acceptedPortions, costPerPortion, standardCostPerPortion } }` (biểu đồ cơ cấu). Chỉ lệnh đã tính giá (`CalculateCost` / `Close`).

## 6. Giá vốn suất `#/cost-meal` (Catalog 154)

| Chức năng | Action | Input |
|---|---|---|
| Tính | `POST MealCost/Calculate` | `OrderId` – đơn **Đã giao / Đã đối soát** |
| Danh sách | `GET MealCost/GetList?OrderId=&From=&To=` (ngày ăn) | |

```json
{ "orderId": "…", "orderNumber": "PH-…", "mealDate": "…", "orderedMeals": 1000, "acceptedMeals": 990, "acceptedSource": "RECONCILIATION",
  "totalActualCost": 62400000, "costPerMeal": 63030.3, "revenue": 35000000, "grossMargin": -27400000, "grossMarginPercent": -78.29,
  "listLine": [ { "orderLineId": "…", "mealType": "TRUA", "orderedMeals": 1000, "acceptedMeals": 990, "totalActualCost": …, "costPerMeal": …, "revenue": …, "grossMargin": …,
    "listDish": [ { "dishName": "Bò kho", "portions": 1000, "totalActualCost": 60000000, "costPerPortion": 60000, "costSource": "ACTUAL", "revenue": …, "grossMargin": … } ] } ],
  "listWarning": [] }
```
Suất nhận: đối soát → POD → suất đặt (`acceptedSource` RECONCILIATION / POD / ORDERED). Giá / phần món = giá thành thực tế các lệnh SX của món / ca ÷ phần món của mọi đơn trong ngày / ca; lệnh chưa tính giá → giá chuẩn (`costSource` STANDARD + cảnh báo). `costSource` ở `GetList` = `STORED`.

## 7. Lô hết hạn `#/scm-lot` (Catalog 121 – thay LotExpiryJob, D-45)
`POST MaterialLot/ProcessExpired` (`WarehouseId?, Preview=true|false, Note?`) → `{ preview, today, transactionNumber, expiredLotCount, totalWasteValue,
listLot: [ { materialLotId, lotNumber, ingredientName, expiryDate, lotStatus, wasteQuantity, unitName, wasteValue, transactionCount, skippedReason } ], listExpiring: [...], listWarning }`.
Lô Khả dụng hạn < hôm nay → **Hết hạn** + bút toán WASTE toàn bộ tồn (`referenceType = LotExpiry`); lô đang tạm giữ bỏ qua (`skippedReason`). `Preview = true` chỉ liệt kê. Nút gợi ý: "Xử lý lô hết hạn" trên màn lô (xem trước → xác nhận).
