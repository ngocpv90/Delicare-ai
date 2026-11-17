# Bàn giao frontend – BE3.4: KPI (đợt BE-3, D-57, spec 11.7)

Quy ước chung xem `P0-P2.md` mục 1:
- đăng nhập;
- `Result` / `Results`;
- lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt;
- quyền theo `listMenu`.

Thử nhanh: `docs/api-tests/be3.4-kpi.http` (đã chạy 33 / 33 trên DB dev). Seed mới: `DB/seed/KpiMetricDefinition_seed.sql` (11 chỉ tiêu phiên bản 1). Mục menu đã có sẵn (`#/kpi-definition`, `#/kpi-daily`, `#/kpi-monthly`, `#/kpi-refresh` – nhóm "Tích hợp & KPI"), **không thêm menu**.

Ngày gửi dạng `yyyy-MM-dd`; tháng gửi dạng `yyyy-MM`. Giá trị % là số 0–100 (vd. `95.5` = 95,5 %).

## Quyền

| Action | Màn | Quyền |
|---|---|---|
| `KpiMetricDefinition/GetAll`, `GetById`, `GetAllData` | `#/kpi-definition` | Xem |
| `KpiMetricDefinition/Insert` / `Update` / `Delete` | `#/kpi-definition` | Thêm / Sửa / Xóa |
| `KpiSnapshot/GetDaily` | `#/kpi-daily` | Xem |
| `KpiSnapshot/GetMonthly` | `#/kpi-monthly` | Xem |
| `KpiSnapshot/Refresh` | – | **Chỉ admin** (PROPOSED D-57 z) – nút "Làm mới" chỉ hiện khi `isAdmin` |
| `AnalyticsRefreshLog/GetList` | `#/kpi-refresh` | Xem |

**Không dùng** các action ghi cũ `KpiDailySnapshot/Insert|Update|Delete`, `KpiMonthlySnapshot/Insert|Update|Delete`, `AnalyticsRefreshLog/Insert|Update|Delete`: số KPI và nhật ký chỉ do `KpiSnapshot/Refresh` ghi (D-57 ac).

## 1. Định nghĩa chỉ tiêu + phiên bản (Catalog 221) – `#/kpi-definition`

CRUD cũ, form (`[FromForm]`). Thêm 2 trường so với bảng:

| Trường | Ý nghĩa |
|---|---|
| `MetricCode` | Mã (tự IN HOA). Không đổi được sau khi tạo |
| `MetricName`, `DomainCode` (PROD / QA / DEL / INV / FIN / PKG / CS), `UnitCode` (`%`, `VND`…), `AggregationCode` (`RATIO`) | Như bảng |
| `FormulaDescription` | Công thức (chữ) |
| `TargetValue` | **Mới** – mục tiêu (cùng đơn vị với giá trị). Trống = không có mục tiêu (`met` = null) |
| `TargetDirection` | **Mới** – `HIGHER` (≥ mục tiêu là đạt) / `LOWER` (≤). Trống = theo mã (WASTE, REJECT, EXPIRY, COST, LOSS → LOWER) |
| `EffectiveFrom`, `EffectiveTo`, `IsActive` | Hiệu lực |

Mục tiêu được lưu thành thẻ `[TARGET >= 95]` cuối `FormulaDescription` (bảng không có cột mục tiêu – GAP-KPI-01). FE hiển thị `formula` / `targetValue` / `targetDirectionName`, **không** hiển thị `formulaDescription` thô.

**Phiên bản (D-57 x):**
- `Update` đổi công thức / mục tiêu / chiều / đơn vị / cách tổng hợp → hệ thống **tạo phiên bản mới** (hiệu lực từ `EffectiveFrom` gửi lên nếu sau bản đang dùng, không thì từ hôm nay) và **đóng** bản cũ (`EffectiveTo` = ngày trước). `responseMessage` báo "Đã tạo phiên bản N…".
- Đổi tên / nhóm / đang dùng / ngày hết hiệu lực → sửa tại chỗ.
- Chỉ sửa / xóa được **phiên bản mới nhất** (bản cũ → 400). Xóa chỉ khi chưa có số KPI (có → 400, hãy bỏ chọn "Đang dùng").
- Insert mã đã có → 400 (sửa để tạo phiên bản mới).

`GET api/KpiMetricDefinition/GetAll` (SearchText, Page, PageSize, SortField, SortOrder như cũ) – mỗi dòng thêm `formula`, `targetValue`, `targetDirection`, `targetDirectionName`, `isCurrent` (đang hiệu lực hôm nay). Mọi phiên bản đều có trong danh sách – gợi ý: nhóm theo `metricCode`, sắp `versionNo` giảm dần.

`GET api/KpiMetricDefinition/GetById?id=` → `data`: `{ id, metricCode, metricName, domainCode, unitCode, formula, aggregationCode, versionNo, effectiveFrom, effectiveTo, isActive, targetValue, targetDirection, targetDirectionName, hasCalculator }` (`hasCalculator = false` → hệ thống chưa có công thức tính cho mã này, số KPI sẽ là INCOMPLETE).

## 2. KPI ngày (Catalog 222) – `#/kpi-daily`

`GET api/KpiSnapshot/GetDaily`

| Tham số | Ý nghĩa |
|---|---|
| `From`, `To` | Ngày. Trống = 30 ngày gần nhất; tối đa 366 ngày |
| `BranchId` | Trống = toàn công ty |
| `DomainCode` | Lọc nhóm chỉ tiêu |

**`data`:**
```json
{
  "period": "DAILY", "from": "2026-11-01T00:00:00", "to": "2026-11-30T00:00:00", "branchId": null, "domainCode": null,
  "metrics": [{
    "definition": { "metricCode": "DEL_ONTIME", "metricName": "Giao đúng giờ", "domainCode": "DEL", "unitCode": "%", "formula": "…",
                    "versionNo": 1, "targetValue": 95, "targetDirection": "HIGHER", "targetDirectionName": "Càng cao càng tốt (≥ mục tiêu)", "hasCalculator": true, "…": "…" },
    "latest": { "date": "2026-11-16T00:00:00", "value": 33.3333, "target": 95, "variance": -61.6667, "met": false,
                "numerator": 1, "denominator": 3, "metricVersion": 1, "dataQualityStatus": "OK", "calculatedAt": "…" },
    "series": [{ "date": "2026-11-16T00:00:00", "value": 33.3333, "target": 95, "met": false, "dataQualityStatus": "OK" }]
  }]
}
```
- `metrics` = mọi chỉ tiêu đang dùng (phiên bản mới nhất), kể cả chưa có số (`latest = null`, `series = []`).
- `latest` = ngày gần nhất **có số** trong khoảng. `value = null` + `dataQualityStatus = INCOMPLETE` → hiện "Thiếu dữ liệu" (không vẽ điểm). INCOMPLETE nhưng có `value` → hiện số kèm cảnh báo "chưa đủ dữ liệu".
- `met`: `true` xanh / `false` đỏ / `null` không tô (chưa có mục tiêu hoặc số).
- `series` chỉ có ngày đã làm mới – FE tự để trống ngày không có.

## 3. KPI tháng (Catalog 223) – `#/kpi-monthly`

`GET api/KpiSnapshot/GetMonthly?FromMonth=2026-01&ToMonth=2026-12&BranchId=&DomainCode=` – trống = 12 tháng gần nhất, tối đa 36 tháng. `data` như trên, `period = "MONTHLY"`, `date` = ngày 01 của tháng. Tháng đang chạy là số **tới ngày làm mới**.

## 4. Làm mới (Catalog 224) – nút trên `#/kpi-daily`, `#/kpi-monthly`, `#/kpi-refresh` (chỉ admin)

`POST api/KpiSnapshot/Refresh` (form)

| Trường | Ý nghĩa |
|---|---|
| `jobCode` | `KPI_DAILY` / `KPI_MONTHLY` (`INVENTORY_BALANCE` → 400, R1 không làm) |
| `date` | KPI_DAILY: ngày, trống = hôm nay; không nhận ngày tương lai |
| `month` | KPI_MONTHLY: `yyyy-MM`, trống = tháng này |
| `branchId` | Trống = toàn công ty |

**`data`:** `{ runId, jobCode, runStatus: "Succeeded", periodDate, from, to, branchId, rowsProcessed, incompleteCount, sourceWatermark, metrics: [{ metricCode, metricName, metricVersion, value, numerator, denominator, target, variance, met, dataQualityStatus, note }] }`. `note` = lý do thiếu dữ liệu (vd. "3 đơn chưa tính giá vốn suất") – chỉ có ở đây, nên hiện trong thông báo / bảng kết quả sau khi bấm.

Bấm lại cùng ngày / tháng **ghi đè** số cũ (không nhân đôi). Đang có lần làm mới khác cùng loại → 400 "Đang có lần làm mới … – thử lại sau". Sau khi bấm: gọi lại `GetDaily` / `GetMonthly`.

## 5. Nhật ký làm mới (Catalog 225) – `#/kpi-refresh`

`GET api/AnalyticsRefreshLog/GetList?JobCode=&Status=&From=&To=&Page=1&PageSize=50` – theo giờ bắt đầu (trống = 30 ngày gần nhất, tối đa 366), mới nhất trước. Gồm cả lần chạy job T23 (`LockOrders`, `Mrp`…) – lọc `JobCode = KPI_DAILY / KPI_MONTHLY` cho màn KPI.

`listData[]`: `{ stt, id, jobCode, runId, startedAt, completedAt, durationSeconds, runStatus, runStatusName, rowsProcessed, sourceWatermark, trigger (MANUAL / SCHEDULE), targetDate, message, createdBy, createdByName }`; `totalRecords`.
`runStatus`: `Running` (Đang chạy), `Succeeded` (Thành công), `Failed` (Lỗi), `Skipped` (Bỏ qua), `Locked` (Đang chạy ở nơi khác).

## Chỉ tiêu (seed, PROPOSED D-57 y)

| Mã | Nhóm | Đơn vị | Mục tiêu | Nguồn |
|---|---|---|---|---|
| PROD_ACHIEVEMENT | PROD | % | ≥ 98 | Sản lượng đạt ÷ kế hoạch mẻ |
| WASTE_RATE | PROD | % | ≤ 2 | Hao hụt suất ÷ sản lượng |
| QC_REJECT_RATE | QA | % | ≤ 1 | Mẻ QC từ chối ÷ mẻ có kết luận |
| QC_CORE_TEMP_PASS | QA | % | ≥ 100 | Mẻ đạt nhiệt độ lõi |
| QC_SAMPLE_RETAINED | QA | % | ≥ 100 | Mẻ đã lưu mẫu |
| DEL_ONTIME | DEL | % | ≥ 95 | Đơn giao đúng giờ (trễ ≤ 15′) |
| INV_ACCURACY | INV | % | ≥ 98 | Kiểm kê đã ghi sổ |
| FIN_COST_MEAL | FIN | VND | – | Giá vốn / suất (MealCost) |
| FIN_FOOD_COST | FIN | % | ≤ 55 | Giá vốn / doanh thu |
| PKG_LOSS_RATE | PKG | % | ≤ 3 | Hộp thất thoát ÷ hộp giao |
| CS_COMPLAINT_SLA | CS | % | ≥ 95 | Phản ánh xử lý đúng hạn |
