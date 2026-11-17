# Bàn giao frontend – BE3.5: dashboard điều hành + hành trình đơn hàng (đợt BE-3, D-57, D-58)

Quy ước chung xem `P0-P2.md` mục 1:
- đăng nhập;
- `Result` / `Results`;
- lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt;
- quyền theo `listMenu`.

Thử nhanh: `docs/api-tests/be3.5-dashboard.http` (dữ liệu [DEMO] có sẵn trên DB dev, đã chạy 13 / 13). **Chỉ đọc. Không có bảng, cột, SP, seed hay mục menu mới.**

Cả 2 action nằm trong `DashboardController` (controller cũ không gắn bảng, màn `#/dash-exec`). **Không API nào trả tên bảng / cột** (D-58) – chỉ tên màn hình (`route`) và số chứng từ.

## Quyền

| Action | Quyền |
|---|---|
| `Dashboard/GetExecutive` | **Xem** `#/dash-exec` (không có → HTTP 403). Khối `alerts` lọc thêm theo quyền Xem từng màn như chuông (BE3.3). |
| `Dashboard/GetOrderJourney` | **Xem** 1 trong các màn `#/flow-e2e`, `#/dash-exec`, `#/crm-order` (không có → **HTTP 403** kèm Result `responseCode 403`). Trạng thái mọi chặng luôn trả; **chứng từ** của chặng chỉ trả khi người dùng được Xem màn của chứng từ đó (`documentCount` vẫn là tổng, `canView = false`). |

Lưu ý: mục menu `#/flow-e2e` chưa có trong `ObjectMenu` (màn điều hướng tĩnh của mockup) – hiện admin hoặc người được Xem `#/dash-exec` / `#/crm-order` dùng được (PROPOSED D-57 ai).

## 1. Dashboard điều hành (Catalog 231, spec 12.1) – màn `#/dash-exec`

`GET api/Dashboard/GetExecutive?date=2026-12-02&shiftId=&branchId=`

- `date` (tùy chọn, `yyyy-MM-dd`): trống = hôm nay (giờ máy chủ). Năm ngoài 2000…2100 → 400.
- `shiftId` (tùy chọn): ca (`api/Shift/GetAllData`). Không có → 400.
- `branchId` (tùy chọn): chi nhánh trên header (`api/OrganizationUnit/GetBranches`), **gồm đơn vị con**. Không có → 400.
- Server **cache 60 giây** phần số liệu chung theo (ngày, ca, chi nhánh) – `generatedAt` cho biết lúc tính. `alerts` lấy riêng theo người đăng nhập (cache 60 giây của chuông) → người khác nhau không thấy cảnh báo của nhau. FE có thể tự làm mới mỗi 60 giây.

```json
{
  "responseCode": 200,
  "data": {
    "date": "2026-12-02T00:00:00", "shiftId": null, "branchId": null, "generatedAt": "2026-11-17T15:02:11",
    "kpi": {
      "plannedMeals": 1650,
      "orders": { "total": 4, "byStatus": { "Locked": 3, "Reconciled": 1 },
                  "listStatus": [ { "status": "Locked", "statusName": "Đã chốt", "count": 3 }, { "status": "Reconciled", "statusName": "Đã đối soát", "count": 1 } ] },
      "productionProgress": { "percent": 100, "donePortions": 2000, "plannedPortions": 2000, "batchCount": 2 },
      "delivery": { "delivered": 1, "total": 1, "byStatus": { "DELIVERED": 1 } },
      "stockAlerts": 0,
      "qcHoldReject": 0, "qcDetail": { "batchHold": 0, "batchReject": 0, "lotOnHold": 0 },
      "staffing": { "checkedIn": 0, "assigned": 0 },
      "openComplaints": 24
    },
    "chart": { "meals7d": [ { "date": "2026-11-26T00:00:00", "planned": 1000, "accepted": 998 }, "… 7 điểm, điểm cuối = ngày chọn" ] },
    "shiftProgress": [ { "shiftId": "…", "code": "SANG_6H", "name": "Sáng", "batchCount": 0, "percent": null, "donePortions": 0, "plannedPortions": 0 } ],
    "kpiSnapshot": [
      { "metricCode": "PROD_ACHIEVEMENT", "name": "Hoàn thành kế hoạch sản xuất", "unitCode": "%", "value": null, "target": 98, "met": null, "lowerIsBetter": false, "dataQualityStatus": "INCOMPLETE", "calculatedAt": "…" },
      { "metricCode": "DEL_ONTIME", "…": "…" }, { "metricCode": "INV_ACCURACY", "…": "…" }, { "metricCode": "FIN_FOOD_COST", "lowerIsBetter": true, "…": "…" }
    ],
    "orders": [
      { "id": "…", "orderNumber": "PH-261117-0021", "customerId": "…", "customerName": "…", "totalQuantity": 1000, "status": "Reconciled", "statusName": "Đã đối soát",
        "route": "#/crm-order", "listDelivery": [ { "deliveryOrderId": "…", "deliveryNumber": "PH-261202-0001", "status": "DELIVERED", "statusName": "Đã giao" } ] }
    ],
    "ordersTotal": 4,
    "alerts": [ { "code": "COMPLAINT_OVERDUE", "severity": "HIGH", "title": "…", "detail": "…", "route": "#/log-complaint", "count": 9 } ]
  }
}
```

### Cách tính từng khối (để hiển thị chú thích / tooltip)

| Khối (thẻ mockup) | Cách tính | Bấm mở |
|---|---|---|
| `kpi.plannedMeals` – Suất ăn kế hoạch | Σ suất **dòng đơn** của đơn có ngày ăn = ngày, trạng thái ≠ Nháp / Hủy; lọc ca theo ca của dòng đơn, chi nhánh theo khách | `#/crm-order` |
| `kpi.orders` – Đơn hàng trong ngày | Đếm đơn của ngày ăn theo trạng thái (mọi trạng thái); lọc ca = đơn có dòng thuộc ca. Khớp `Order/GetBoard?MealDate=` | `#/crm-order` |
| `kpi.productionProgress` – Tiến độ sản xuất | Mẻ (≠ Hủy) của lệnh SX thuộc kế hoạch có ngày SX = ngày: `donePortions` = Σ số đạt của mẻ Đạt / Đã đóng gói, `plannedPortions` = Σ số kế hoạch, `percent` (1 số lẻ, `null` khi không có mẻ). Khớp `ProductionBatch/GetBoard` (`passedPortions`; bảng mẻ tính cả mẻ Hủy vào kế hoạch) | `#/prod-batch` |
| `kpi.delivery` – Giao hàng hoàn tất | `total` = đơn giao (≠ Hủy) có ngày giao = ngày; `delivered` = trạng thái thực hiện `DELIVERED`. Khớp `DeliveryExecution/GetBoard` (`statusCounts`). Không lọc ca | `#/log-execution` |
| `kpi.stockAlerts` – Cảnh báo tồn kho | Số nguyên liệu dưới tồn tối thiểu – **cùng quy tắc chuông** `STOCK_BELOW_MIN` (không phụ thuộc quyền) | `#/scm-inv-balance` |
| `kpi.qcHoldReject` – QC tạm giữ / loại | Mẻ trong ngày QC `HOLD` + `REJECT` + lô còn tồn đang tạm giữ (= chuông `LOT_ON_HOLD`); chi tiết ở `qcDetail` | `#/prod-batch`, `#/scm-lot` |
| `kpi.staffing` – Nhân sự theo ca | Ca được phân của ngày: `checkedIn` = Đã vào ca + Hoàn thành, `assigned` = tổng; chi nhánh theo đơn vị của nhân viên | `#/hr-shift-assign` |
| `kpi.openComplaints` – Khiếu nại đang mở | Phản ánh Mới / Đang xử lý (= chuông `COMPLAINT_OVERDUE` + `COMPLAINT_OPEN`) | `#/log-complaint` |
| `chart.meals7d` | 7 ngày tới ngày chọn: `planned` = như `plannedMeals` theo ngày ăn; `accepted` = Σ suất khách nhận (POD) theo ngày giao (không lọc ca) | – |
| `shiftProgress` | Mỗi ca đang dùng (lọc ca → 1 dòng): mẻ theo ca của lệnh SX, cùng công thức tiến độ | `#/prod-batch` |
| `kpiSnapshot` | 4 chỉ tiêu PROD_ACHIEVEMENT, DEL_ONTIME, INV_ACCURACY, FIN_FOOD_COST từ snapshot KPI ngày (BE3.4 tính). Không chọn chi nhánh = số toàn công ty; chọn = đúng chi nhánh. Chưa tính → `value` / `met` = `null`. `met` theo chiều chỉ tiêu (`lowerIsBetter`: chi phí càng thấp càng tốt) | `#/kpi-daily` |
| `orders` | 6 đơn đầu của ngày (theo số đơn) + trạng thái thực hiện giao từng đơn giao (≠ Hủy); `listDelivery` rỗng = "Chưa giao"; `ordersTotal` để hiện "Xem tất cả →" | `#/crm-order` (bấm 1 đơn → hành trình, mục 2) |
| `alerts` | = `Common/GetAlerts?branchId=` của người đăng nhập (BE3.3.md) – có thể dùng chung với chuông | `route` |

## 2. Hành trình đơn hàng (Catalog 232, spec 12.2) – màn `#/flow-e2e` / ngăn kéo đơn hàng

`GET api/Dashboard/GetOrderJourney?orderId=`

- `orderId` trống → 400 "Chưa chọn đơn hàng"; không có → 400 "Không tìm thấy đơn hàng".
- 8 chặng **cố định theo thứ tự**; `currentStage` = chặng đầu tiên chưa xong (`null` khi đơn Hủy hoặc mọi chặng xong).
- `status` của chặng (chung): `NotStarted` (Chưa bắt đầu), `InProgress` (Đang thực hiện), `Done` (Hoàn tất), `Issue` (Có sự cố), `Cancelled` (Đã hủy – đơn hủy), `None` (Không có – chỉ chặng phản ánh). `done = true` khi `Done` / `None`. Mỗi chứng từ giữ trạng thái thật (`status` + `statusName`).

| `stage` | Tiêu đề | Chứng từ (`route`) | Xong khi |
|---|---|---|---|
| `ORDER` | Đơn hàng | đơn (`#/crm-order`) | đơn đã xác nhận trở đi (Nháp / Đã gửi = Đang thực hiện) |
| `PRODUCTION_PLAN` | Kế hoạch sản xuất | KH- của ngày ăn (`#/prod-plan`) – **gộp theo ngày, dùng chung mọi đơn** (`note`) | mọi KH Đã phát hành / Hoàn thành |
| `PRODUCTION` | Sản xuất (lệnh SX / mẻ nấu) | lệnh SX thuộc ca của dòng đơn (`#/prod-order`) + mẻ (`#/prod-batch`) | mọi mẻ (≠ Hủy) Đã đóng gói / Không đạt; tất cả không đạt = `Issue`; `note` = số mẻ không đạt |
| `PACKING` | Đóng gói | lệnh đóng gói gắn đơn, ≠ Hủy (`#/log-packing-order`) | mọi lệnh Đã đóng gói |
| `DELIVERY` | Giao hàng (đơn giao / chuyến xe) | đơn giao ≠ Hủy (`#/log-delivery-order`, trạng thái thực hiện READY…DELIVERED / FAILED) + chuyến (`#/log-route`) | mọi đơn giao còn hiệu lực `DELIVERED`; chỉ có đơn giao thất bại = `Issue`; `note` = số đơn giao thất bại |
| `POD` | Xác nhận giao nhận (POD) | POD theo đơn giao – số = số đơn giao, `statusName` "Nhận x / giao y suất" (`#/log-pod`) | mọi đơn giao đã giao đều có POD |
| `RECONCILIATION` | Đối soát | bản đối soát – số = số đơn (`#/crm-reconciliation`) | Khớp / Chênh lệch / Đóng |
| `COMPLAINT` | Phản ánh khách hàng | phản ánh gắn đơn hoặc đơn giao của đơn (`#/log-complaint`) | không có (`None`) hoặc mọi phản ánh đã xử lý / đóng / từ chối; còn mở = `Issue` |

```json
{
  "responseCode": 200,
  "data": {
    "orderId": "…", "orderNumber": "PH-261117-0317", "customerName": "…", "contractNumber": "HD-…", "mealDate": "2026-12-01T00:00:00",
    "status": "Reconciled", "statusName": "Đã đối soát", "currentStage": "COMPLAINT",
    "stages": [
      { "stage": "ORDER", "title": "Đơn hàng", "route": "#/crm-order", "status": "Done", "statusName": "Hoàn tất", "at": "…", "done": true,
        "documentCount": 1, "canView": true, "note": null,
        "documents": [ { "id": "…", "number": "PH-261117-0317", "status": "Reconciled", "statusName": "Đã đối soát", "at": "…", "route": "#/crm-order" } ] },
      { "stage": "PRODUCTION", "title": "Sản xuất (lệnh SX / mẻ nấu)", "route": "#/prod-batch", "status": "Done", "documents": [ "PH-261117-0321 (Đóng)", "ME-261201-TRUA-01 (Đã đóng gói)", "…" ] },
      { "stage": "COMPLAINT", "title": "Phản ánh khách hàng", "route": "#/log-complaint", "status": "Issue", "statusName": "Có sự cố", "done": false, "documents": [ "PA-261117-0035 (Mới)", "…" ] }
    ]
  }
}
```

Gợi ý giao diện: stepper 8 bước (màu theo `status`), dưới mỗi bước là danh sách chứng từ (bấm → mở `route` của chứng từ, lọc / mở theo `id`). Chặng `canView = false` hiện "N chứng từ – không có quyền xem".

## PROPOSED – BA/PO CONFIRM (DECISIONS D-57 ad–ai)
- (ad) Ánh xạ trạng thái spec → code: Passed/Packed, HOLD/REJECT (`QcStatus` mẻ), CheckedIn/Completed, DELIVERED (trạng thái thực hiện giao, suy theo đơn giao khi chưa có dòng thực hiện), Open/InProgress; "dòng QC đầu vào HOLD" → lô còn tồn đang tạm giữ (D-57 r).
- (ae) Lọc chi nhánh / ca từng khối; mẻ Hủy không tính; ngày trống = hôm nay.
- (af) Cache 60 giây phần chung, cảnh báo theo người.
- (ag) KPI snapshot: dòng toàn công ty khi không chọn chi nhánh; chiều chỉ tiêu theo thẻ `[TARGET <= / >= x]` rồi theo mã.
- (ah) Liên kết chặng + trạng thái chung của chặng.
- (ai) Quyền hành trình đơn.
