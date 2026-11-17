# Bàn giao frontend – BE2.2: hợp đồng, dự báo, đổi món, sửa đơn sau giờ chốt

Quy ước chung (đăng nhập, `Result` / `Results`, lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt, quyền theo `listMenu`): `P0-P2.md` mục 1.
Form: số `62.5`, ngày `yyyy-MM-dd`. Action có danh sách lồng nhau nhận **JSON** (ghi rõ bên dưới), còn lại **form** (`application/x-www-form-urlencoded`).
Thử nhanh cả luồng (1 bộ `[DEMO]` tự đủ, đã chạy trên DB dev): `docs/api-tests/be2.2-contract-forecast.http`.

Quyền: theo `PermissionConvention` – `Get*` = Xem, `Create*` = Thêm, `Import*` = Nhập, còn lại = Sửa trên màn hình của controller; ngoại lệ ghi ở cột "Quyền".
Mục menu **đã có** trong `ObjectMenu` (không thêm seed): `crm-contract`, `crm-forecast`, `menu-change-request`, `crm-order` và các mục duyệt
`#/wf-inbox?type=crm-contract` (Duyệt – Hợp đồng suất ăn), `#/wf-inbox?type=menu-change-request` (Duyệt – Yêu cầu đổi món), `#/wf-inbox?type=crm-order` (Duyệt – Đơn đặt suất ăn: lệch suất **và** sửa / thêm đơn sau giờ chốt).
Quyền duyệt = `AllowEdit` trên mục duyệt (D-31); các action `Approve` / `Reject` gắn `[SkipPermission]` – engine kiểm (người không quyền nhận 400 "Bạn không có quyền duyệt…").

## ⚠ Thay đổi hành vi (FE đang làm theo P3 cần sửa)
1. **Hợp đồng luôn bắt đầu ở Nháp.** `Contract/Insert` bỏ qua `Status` gửi lên (luôn `Draft`), `ContractNumber` trống → tự sinh `HD-YYMMDD-NNN`. Hợp đồng chỉ Hiệu lực sau `Contract/Submit` → duyệt. `Contract/Update` / `Delete` chỉ khi Nháp (Update không đổi `Status`).
2. **Loại suất (`ContractMeal`) chỉ Thêm / Sửa / Xóa khi hợp đồng Nháp**; `MealType` bắt buộc, không trùng trong hợp đồng (không phân biệt hoa thường), `UnitPrice` (nếu có) > 0; Update không chuyển được sang hợp đồng khác.
3. **Dự báo**: `Forecast/Insert` bị chặn (400 – dùng `Forecast/Import`); `Forecast/Update` / `Delete` chỉ bản Nháp (Update chỉ đổi `ForecastQuantity`, `Note`).
4. **Yêu cầu đổi món**: `MenuChangeRequest/Insert` / `Update` bị chặn (dùng `Create`, duyệt qua hộp duyệt); `Delete` chỉ khi Từ chối / Đã hủy.
5. **Đơn hàng**: `GetNewTemplate` → `suggestedQuantity` = dự báo (trước đây luôn 0); `GetDetail` / kết quả các thao tác có thêm `forecastQuantity`, `forecastVariancePercent`, `forecastVarianceExceeded`, `pendingApprovalType`, `listLine[].forecastQuantity`; sau giờ chốt `allowedActions` có thêm **`requestChange`** (Nháp / Đã gửi / Đã xác nhận / Đã chốt, khi không có yêu cầu duyệt đang chờ). `Order/Confirm` có thể trả `responseMessage` kèm "Lệch dự báo …" và `listWarning` (không chặn). `Order/Create` sau giờ chốt **có `afterCutoffReason`** → tạo đơn Nháp + gửi duyệt (trước đây luôn 400).
6. Hợp đồng **Chấm dứt** vẫn nhận đơn tới hết `effectiveTo` (mẫu tạo đơn tìm hợp đồng Hiệu lực hoặc Chấm dứt còn hiệu lực ở ngày ăn).
7. Kế hoạch sinh tự động / MRP / chia mẻ đóng gói **áp đổi món đã duyệt** (suất món cũ chuyển sang món mới); MRP có `listWarning` "Đã áp N yêu cầu đổi món…".

## 1. `crm-contract` – Hợp đồng (Catalog 78–80)

| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Danh sách | `GET Contract/GetAll` (cũ) | Xem | |
| Chi tiết | `GET Contract/GetDetail?id=` | Xem | mục 1.1 |
| Tạo kèm suất | `POST Contract/Create` (**JSON**) | Thêm | `{ customerId, contractNumber?, name, signedDate?, effectiveFrom, effectiveTo?, paymentTerm?, contractValue?, note?, listMeal: [{ mealType, mealName?, serviceType?, unitPrice?, unitId? }] }`. Kiểm: khách tồn tại, từ ≤ đến, số không trùng, ≥ 1 suất, mỗi `mealType` 1 lần, đơn giá > 0. → Nháp, số `HD-YYMMDD-NNN` nếu trống |
| Gửi duyệt | `POST Contract/Submit` (form `Id, Note?`) | Sửa | Nháp → Chờ duyệt (`PendingApproval`) + yêu cầu `XN-` loại `CONTRACT`; message "Đã gửi duyệt hợp đồng (yêu cầu XN-…)" |
| Duyệt | `POST Contract/Approve` (form `Id, Note?`) | Duyệt mục `crm-contract` (engine) | → Hiệu lực. Nội dung (đầu HĐ + suất) bị sửa sau khi gửi → 400 "đã thay đổi"; HĐ đã quá `effectiveTo` → 400 |
| Từ chối | `POST Contract/Reject` (form `Id, Note` bắt buộc) | như trên | → Nháp, Note "Từ chối: …". Người gửi hủy: `ApprovalRequest/Cancel` → Nháp |
| Chấm dứt | `POST Contract/Terminate` (form `Id, Reason, EffectiveTo?`) | Sửa (PROPOSED) | Chỉ Hiệu lực; lý do ≥ 3 ký tự; `EffectiveTo` trống = hôm nay, trong [từ, đến cũ]. Còn đơn Nháp … Đang SX của HĐ có ngày ăn **sau** ngày chấm dứt → 400 liệt kê số đơn. → `Terminated`, `effectiveTo` = ngày chấm dứt |

Có thể duyệt chung qua `ApprovalRequest/GetMyPending?type=crm-contract` + `ApprovalRequest/Decide` / `GetDetail` (business.table = `Contract`, lines = loại suất).

Trạng thái: `Draft` Nháp → `PendingApproval` Chờ duyệt → `Active` Hiệu lực → `Expired` Hết hạn / `Terminated` Chấm dứt (Hết hạn tự động = job, để sau – D-45; `GetDetail.listWarning` báo HĐ Hiệu lực đã quá ngày).

### 1.1 `ContractDetailModel`
`contract` (bảng Contract), `statusName, customerName, listMeal[]` (ContractMeal, theo `mealType`), `allowedActions[]` (Nháp: `update, submit, delete`; Chờ duyệt: `approve, reject`; Hiệu lực: `terminate`), `pendingApprovalId, pendingApprovalNo, openOrderCount, listWarning[]`.

```json
{ "responseCode": 200, "data": { "contract": { "id": "…", "contractNumber": "HD-261117-001", "status": "Draft", "effectiveFrom": "2026-11-16T00:00:00" },
  "statusName": "Nháp", "listMeal": [ { "id": "…", "mealType": "SANG", "unitPrice": 20000 }, { "id": "…", "mealType": "TRUA", "unitPrice": 40000 } ],
  "allowedActions": ["update", "submit", "delete"], "openOrderCount": 0 } }
```

## 2. `crm-forecast` – Dự báo (Catalog 87–89)

Khóa dự báo = (khách, điểm giao (trống = cấp khách), ngày, ca (trống = mọi ca), loại suất). Mỗi lần nhập tạo **phiên bản mới** (`versionNo` + 1) ở Nháp.

| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Ma trận | `GET Forecast/GetMatrix?From=&To=&BranchId=&CustomerId=&MealType=` | Xem | ≤ 62 ngày (`BusinessRules:ForecastMaxDays`). `dates[]`, `rows[]` = `{ customerId, customerCode, customerName, values: { "yyyy-MM-dd": n }, draftValues: {…}, total }`, `totals: { "yyyy-MM-dd": n }`, `grandTotal`. `values` = bản **Đã công bố** mới nhất mỗi khóa; `draftValues` = bản Nháp chưa công bố |
| Nhập hàng loạt | `POST Forecast/Import` (**JSON**) | **Nhập** | `{ note?, listItem: [{ customerId, deliveryLocationId?, forecastDate, shiftId?, mealType, forecastQuantity }] }` → `{ created, versionNo, superseded, listItem[] }`. Lỗi theo dòng "Dòng n: …" (khách / điểm giao không thuộc khách / ca / số âm / trùng khóa trong lô). Bản Nháp cũ cùng khóa → Bị thay thế ngay; bản **Đã công bố** cũ giữ tới khi bản mới được công bố (PROPOSED D-51) |
| Công bố | `POST Forecast/Publish` (form `From, To, ListCustomerId` lặp lại – trống = mọi khách) | Sửa | Nháp → Đã công bố; bản đã công bố cũ cùng khóa → Bị thay thế. → `{ published, superseded }`; không có Nháp → 400 |
| CRUD | `Forecast/GetAll`, `Update` (Nháp), `Delete` (Nháp) | | `Insert` → 400 |

Trạng thái: `Draft` Nháp → `Published` Đã công bố → `Superseded` Bị thay thế.

## 3. `crm-order` – Gợi ý số suất, lệch dự báo, sửa / thêm đơn sau giờ chốt

| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Mẫu tạo đơn | `GET Order/GetNewTemplate` (cũ) | Xem | `listLocation[].listLine[].suggestedQuantity` = dự báo Đã công bố của (điểm giao, loại suất, ca); điểm chưa có dự báo riêng nhận phần chia đều của dự báo cấp khách (phần nguyên, điểm đầu nhận dư); không có = 0 (Catalog 92) |
| Xác nhận | `POST Order/Confirm` (cũ) | Sửa | Lệch = (đặt − dự báo) / dự báo × 100 trên các dòng có dự báo; \|lệch\| > `ForecastVariancePctWarning` (10) → message "Đã lưu … – Lệch dự báo +12.5% (đặt 135 / dự báo 120 suất) vượt ngưỡng 10% – đã ghi nhận", `listWarning`, Note đơn thêm `[LECH_DU_BAO] …`. **Không chặn** (Catalog 96) |
| Xin sửa sau giờ chốt | `POST Order/RequestChange` (**JSON**) | Sửa | `{ id, reason, listLine: [{ id, quantity }], listNewLine: [{ contractMealId, shiftId?, quantity, note? }] }`. Chỉ khi **đã tới giờ chốt** (trước giờ chốt → 400 "dùng UpdateLines"); đơn Nháp / Đã gửi / Đã xác nhận / Đã chốt; lý do bắt buộc; đơn Đã xác nhận / Đã chốt phải có ≥ 1 thay đổi. **Không ghi ngay** – tạo yêu cầu `ORDER_CHANGE_AFTER_CUTOFF` (`approvalRequested = true`). Duyệt (mục `crm-order`) → ghi số mới / dòng mới (giá + thực đơn tra lúc duyệt), đơn → **Đã chốt**, Note `[SAU_GIO_CHOT] …` (+ nhắc tính NL bổ sung nếu kế hoạch đã lập). Từ chối → không đổi |
| Thêm đơn sau giờ chốt | `POST Order/Create` (cũ, **JSON**) + `afterCutoffReason` | Thêm | Sau giờ chốt: không có lý do → 400 như cũ; có lý do → đơn Nháp + yêu cầu `ORDER_CHANGE_AFTER_CUTOFF` (`approvalRequested = true`). Duyệt → Đã chốt; từ chối / người gửi hủy → Hủy |
| Duyệt | `ApprovalRequest/GetMyPending?type=crm-order` + `ApprovalRequest/Decide` (form `Id, Approve, Note`) | Duyệt mục `crm-order` | `pendingApprovalId` lấy từ `Order/GetDetail`. Nút chốt 20h (`LockByMealDate`) chạy trong lúc chờ **không** làm hỏng yêu cầu (Đã xác nhận ↔ Đã chốt cùng giai đoạn); sửa số / hủy / vào sản xuất → yêu cầu không duyệt được |

`OrderDetailModel` thêm: `pendingApprovalType` (`ORDER_DEVIATION` / `ORDER_CHANGE_AFTER_CUTOFF`), `forecastQuantity`, `forecastVariancePercent`, `forecastVarianceExceeded`, `listLine[].forecastQuantity`. `Order/UpdateLines` đang có yêu cầu duyệt (bất kỳ loại) → 400 "Đơn đang có yêu cầu duyệt XN-…".

## 4. `menu-change-request` – Yêu cầu đổi món (Catalog 104)

| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Danh sách | `GET MenuChangeRequest/GetAll` (cũ) | Xem | |
| Chi tiết | `GET MenuChangeRequest/GetDetail?id=` | Xem | mục 4.1 |
| Tạo + gửi duyệt | `POST MenuChangeRequest/Create` (form `OrderId?, MenuId?, OriginalDishId, RequestedDishId, MealDate?, ShiftId?, Quantity?, Reason`) | Thêm | Lý do ≥ 3 ký tự; 2 món khác nhau, đang dùng. Có đơn: đơn chưa giao / chưa hủy, ngày ăn = ngày ăn của đơn, món cũ phải có trong thực đơn của đơn ở thứ đó; `Quantity` trống = Σ suất dòng đơn có thực đơn đó; `ShiftId` trống = ca của dòng (nếu 1 ca). Không đơn: `MealDate` + `Quantity` bắt buộc (áp cho mọi đơn của ngày / ca). Số `PH-YYMMDD-NNNN`, `requestStatus = Pending`, yêu cầu `XN-` loại `MENU_CHANGE` |
| Duyệt | `POST MenuChangeRequest/Approve` (form `Id, Note?`) | Duyệt mục `menu-change-request` (engine) | → Đã duyệt + `approvedBy / approvedAt`. Kế hoạch SX của ngày còn **Nháp** → dòng kế hoạch: món cũ − số suất, món mới + số suất (cùng ca). Kế hoạch đã Duyệt / Phát hành → không sửa, `GetDetail.planNeedsManualAction = true` + cảnh báo "bếp trưởng xử lý tay" |
| Từ chối | `POST MenuChangeRequest/Reject` (form `Id, Note` bắt buộc) | như trên | → Từ chối. Người gửi hủy: `ApprovalRequest/Cancel` → Đã hủy |
| Xóa | `DELETE MenuChangeRequest/Delete?id=` | Xóa | Chỉ Từ chối / Đã hủy |

Trạng thái: `Pending` Chờ duyệt → `Approved` Đã duyệt / `Rejected` Từ chối / `Cancelled` Đã hủy. Xin khách chấp thuận (BUSINESS_RULES §3 G1) làm ngoài hệ thống (PROPOSED D-51).

### 4.1 `MenuChangeDetailModel`
`request` (bảng MenuChangeRequest), `statusName, orderNumber, customerName, menuCode, originalDishName, requestedDishName, shiftCode, requestedByName, approvalRequestId, approvalRequestNo, productionPlanId, productionPlanNumber, productionPlanStatus, planNeedsManualAction, listWarning[]`.
