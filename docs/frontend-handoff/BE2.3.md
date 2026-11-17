# Bàn giao frontend – BE2.3: duyệt nhiều cấp + áp vào nghiệp vụ

Quy ước chung (đăng nhập, `Result` / `Results`, lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt, quyền theo `listMenu`): `P0-P2.md` mục 1.
Form: số `62.5`, ngày `yyyy-MM-dd`. Mọi action mới nhận **form** (`application/x-www-form-urlencoded`).
Thử nhanh cả luồng (1 bộ `[DEMO]` tự đủ, đã chạy trên DB dev): `docs/api-tests/be2.3-multilevel-approval.http`.

Quyền: `PermissionConvention` (`Get*` = Xem, còn lại = Sửa trên màn hình của controller). Các action `Approve` / `Reject` gắn `[SkipPermission]` – **engine kiểm quyền theo cấp đang chờ** (không có quyền → 400 "Bạn không có quyền duyệt loại yêu cầu này").

**Mục menu duyệt mới** (seed `DB/seed/ObjectMenu_seed.sql` + `UserRole_seed.sql`, nhóm PROPOSED – admin sửa trên ma trận quyền):

| MetaLink | Tên | Nhóm seed |
|---|---|---|
| `#/wf-inbox?type=scm-pr-l1` | Duyệt – Đề nghị mua hàng (cấp 1 – Trưởng bộ phận Mua hàng) | OPS-SCM |
| `#/wf-inbox?type=scm-pr-l2` | Duyệt – Đề nghị mua hàng (cấp 2 – Giám đốc vận hành) | COO |
| `#/wf-inbox?type=scm-stockcount-l2` | Duyệt – Phiếu kiểm kê (cấp 2 – Kế toán) | FIN-ACC |

Mục cũ dùng tiếp: `scm-stockcount` (kiểm kê cấp 1), `scm-adjustment` (điều chỉnh tồn), `scm-po` / `scm-po-director` (PO). Mục cũ **`#/wf-inbox?type=scm-pr` không còn yêu cầu nào** (đề nghị mua dùng `scm-pr-l1` / `scm-pr-l2`) – sidebar nên ẩn / admin tắt mục này.

## ⚠ Thay đổi hành vi (FE đang làm theo P4 / P5 / wf-inbox cần sửa)
1. **Đề nghị mua phải duyệt 2 cấp trước khi lập PO (D-46).** `PurchaseRequest/ConvertToPurchaseOrder` chỉ nhận đề nghị **Đã duyệt** (Nháp → 400). Đề nghị do MRP lập (`MaterialRequirement/Calculate` / `CalculateTopUp` / `CreatePurchaseRequest`) cũng vậy. `PurchaseRequest/Cancel` chỉ Nháp / Đã duyệt (đang chờ duyệt: người gửi hủy yêu cầu duyệt bằng `ApprovalRequest/Cancel` → về Nháp).
2. **Điều chỉnh tồn tay (`InventoryTransaction/Adjust`) không ghi sổ ngay** mà gửi duyệt `STOCK_ADJUSTMENT` (cấu hình `BusinessRules:StockAdjustmentRequireApproval` = true). `data` đổi từ bút toán sang `InventoryAdjustResultModel` (mục 3).
3. **Kiểm kê lệch > 2%**: `StockCount/Post` trả 400 "… gửi duyệt (StockCount/Submit) …" – **kể cả admin** (trước đây admin ghi sổ thẳng). Luồng mới: `Submit` → duyệt 2 cấp → `Post`.
4. **Yêu cầu duyệt có trạng thái mới `InProgress` ("Đang duyệt")** khi đã qua ít nhất 1 cấp. Trạng thái còn mở = `Pending` hoặc `InProgress`. Bước (`ApprovalStep.DecisionStatus`): `Waiting` / `Approved` / `Rejected` / `Skipped` (cấp sau khi bị từ chối / hủy) / `Cancelled`.
5. `ApprovalRequest/Decide` khi còn cấp sau trả message "Đã duyệt cấp 1/2 – chuyển cấp 2 (…)" (`data = true`); nghiệp vụ chỉ đổi ở cấp cuối.
6. **Một người không quyết định 2 cấp của cùng yêu cầu** (admin được miễn): người đã duyệt cấp 1 không thấy yêu cầu ở hộp cấp 2, `canDecide = false`, gọi duyệt → 400 "Bạn đã quyết định cấp 1 …".
7. Từ chối ở hệ nhiều cấp: lý do ghi vào bản ghi nghiệp vụ dạng `Từ chối: [Cấp 2/2] <lý do>`.
8. Hạn mỗi cấp 48 giờ (`BusinessRules:ApprovalStepDueHours`), **chỉ là cờ** `isOverdue` (không job – D-45); vẫn duyệt được (bước ghi `[QUÁ HẠN] …`) trừ khi bật `ApprovalBlockOverdueDecision`.

## 1. Hộp duyệt `#/wf-inbox` (Catalog 210–213)

| Chức năng | Action | Ghi chú |
|---|---|---|
| Chờ tôi duyệt | `GET ApprovalRequest/GetMyPending?type=<mục>` | Chỉ trả yêu cầu mà **cấp đang chờ** dùng đúng mục `type` và người đăng nhập có quyền Duyệt mục đó. Không `type` = mọi mục người đó duyệt được |
| Chi tiết | `GET ApprovalRequest/GetDetail?id=` | mục 1.2 |
| Duyệt / từ chối | `POST ApprovalRequest/Decide` (form `Id, Approve=true|false, Note`) | Từ chối bắt buộc `Note`. Hoặc dùng action của từng nghiệp vụ (`PurchaseRequest/Approve`…) |
| Hủy (người gửi / admin) | `POST ApprovalRequest/Cancel` (form `Id, Reason`) | Ở bất kỳ cấp nào; bản ghi nghiệp vụ về Nháp, cấp sau `Skipped` |

### 1.1 `ApprovalPendingModel` (thêm field)
```json
{ "id": "…", "requestNo": "XN-261117-012", "businessType": "PURCHASE_REQUEST", "businessId": "…", "screenId": "scm-pr-l2",
  "title": "Đề nghị mua PH-261117-0042 – cần ngày 20/11/2026 – 1 dòng – ước tính 3,000,000 đ",
  "requestedAt": "2026-11-17T09:00:00", "requestedByName": "…",
  "requestStatus": "InProgress", "stepNo": 2, "stepCount": 2, "stepName": "Duyệt – Đề nghị mua hàng (cấp 2 – Giám đốc vận hành)",
  "dueAt": "2026-11-19T10:15:00", "isOverdue": false }
```
`screenId` = mục quyền của cấp đang chờ (không phải màn nghiệp vụ) – mở chi tiết theo `businessType` (PURCHASE_REQUEST → `scm-pr`, STOCK_COUNT → `scm-stockcount`, STOCK_ADJUSTMENT → `scm-adjustment` / `scm-inv-txn`).

### 1.2 `ApprovalDetailModel` (thêm field)
`currentStepNo` (0 khi đã kết thúc), `stepCount`, `dueAt`, `isOverdue`, `listStepInfo[]`:
```json
{ "stepNo": 1, "screenId": "scm-pr-l1", "stepName": "Duyệt – Đề nghị mua hàng (cấp 1 – …)", "decisionStatus": "Approved", "decisionStatusName": "Đã duyệt",
  "isCurrent": false, "approverUserId": "…", "approverName": "…", "assignedAt": "…", "dueAt": "…", "isOverdue": false, "decidedAt": "…", "decisionNote": "Đúng nhu cầu" }
```
`statusName`: Chờ duyệt / **Đang duyệt** / Đã duyệt / Từ chối / Đã hủy. `canDecide` = còn mở + có quyền mục của cấp đang chờ + chưa quyết định cấp khác. `business.table`: thêm `PurchaseRequest` (record + lines), `StockCount` (record + dòng lệch), `InventoryTransaction` (STOCK_ADJUSTMENT: `lines` = bút toán đã ghi sau duyệt; nội dung đề nghị ở `proposalJson`). `listItem[0]` = dòng `ApprovalItem` lưu quy trình (`payloadJson = {"steps":["scm-pr-l1","scm-pr-l2"]}`).

## 2. `scm-pr` – Đề nghị mua 2 cấp (Catalog 110, 111)

| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Gửi duyệt | `POST PurchaseRequest/Submit` (form `Id, Note?`) | Sửa `scm-pr` | Nháp, ≥ 1 dòng số lượng > 0 → Chờ duyệt + `XN-` PURCHASE_REQUEST; message "Đã gửi duyệt đề nghị mua (yêu cầu XN-…, 2 cấp)" |
| Duyệt cấp đang chờ | `POST PurchaseRequest/Approve` (form `Id, Note?`) | Duyệt mục `scm-pr-l1` (cấp 1) / `scm-pr-l2` (cấp 2) – engine | Cấp 1 → chờ cấp 2 (đề nghị vẫn Chờ duyệt); cấp 2 → **Đã duyệt**. Nội dung sửa sau khi gửi → 400 "đã thay đổi" |
| Từ chối | `POST PurchaseRequest/Reject` (form `Id, Note` bắt buộc) | như trên | Ở cấp nào cũng → **Nháp**, Note "Từ chối: [Cấp n/2] …"; sửa rồi gửi lại = yêu cầu mới |
| Lập PO | `POST PurchaseRequest/ConvertToPurchaseOrder` (cũ) | Sửa | **Chỉ Đã duyệt** |

`GetDetail` thêm `pendingApproval` (`ApprovalProgressModel`: `id, requestNo, requestStatus, stepNo, stepCount, screenId, stepName, dueAt, isOverdue`) khi Chờ duyệt. `allowedActions`: Nháp `update, updateLines, submit, cancel`; Chờ duyệt `approve, reject, cancelApproval` (hiện nút theo quyền cấp đang chờ – `pendingApproval.screenId`); Đã duyệt `convertToPurchaseOrder, cancel`.
Trạng thái: `Draft` Nháp → `PendingApproval` Chờ duyệt → `Approved` Đã duyệt → `Converted` Đã lập PO; `Cancelled` Hủy.

## 3. `scm-inv-txn` / `scm-adjustment` – Điều chỉnh tồn qua duyệt (GAP-F-04)

`POST InventoryTransaction/Adjust` (form cũ: `WarehouseId, StorageLocationId?, IngredientId, MaterialLotId?, UnitId?, Quantity (có dấu), UnitCost?, Reason, Note?`) – quyền Sửa `scm-inv-txn`.
```json
{ "transaction": null, "pendingApproval": true, "approvalRequestId": "…", "approvalRequestNo": "XN-261117-005", "referenceId": "…" }
```
Message "Đã gửi duyệt điều chỉnh tồn (yêu cầu XN-…) – chỉ ghi sổ khi được duyệt". Giảm quá tồn hiện có → 400 ngay. Duyệt (`ApprovalRequest/Decide`, quyền mục `scm-adjustment`, 1 cấp) → bút toán `ADJUSTMENT`, `ReferenceType = ManualAdjustment`, `ReferenceId = referenceId`, số `PH-` lấy lúc duyệt (không đủ tồn lúc duyệt → 400). Từ chối / hủy → không ghi gì. Khi tắt duyệt (`StockAdjustmentRequireApproval = false`): `transaction` = bút toán, `pendingApproval = false`.

## 4. `scm-stockcount` – Kiểm kê lệch > 2% (Catalog 131, 132)

| Chức năng | Action | Quyền | Ghi chú |
|---|---|---|---|
| Gửi duyệt | `POST StockCount/Submit` (form `Id, Note?`) | Sửa | Nháp / Đang kiểm; mọi dòng lệch có lý do; có dòng > `StockCountVariancePctNeedApproval` (2%) → Chờ duyệt + STOCK_COUNT **2 cấp** (`scm-stockcount` Quản lý kho → `scm-stockcount-l2` Kế toán). Không dòng nào > 2% → 400 "không cần duyệt, ghi sổ trực tiếp" |
| Duyệt / từ chối | `POST StockCount/Approve` / `Reject` (form `Id, Note`) | engine | Cấp cuối duyệt → `approvedBy / approvedAt` = người duyệt, phiếu **giữ Chờ duyệt** ("Đã duyệt – chờ ghi sổ"); từ chối → **Đang kiểm** (đếm / sửa lý do rồi gửi lại) |
| Ghi sổ | `POST StockCount/Post` (cũ) | Sửa | Lệch ≤ 2%: từ Nháp / Đang kiểm như cũ. Lệch > 2%: chỉ khi đã duyệt đủ cấp; chưa duyệt → 400 |

`GetDetail` thêm `isApproved`, `pendingApproval`; `allowedActions`: Nháp / Đang kiểm `recordCounts` + (`submit` nếu `needsApproval`, ngược lại `post`); Chờ duyệt `approve, reject, cancelApproval`; đã duyệt `post`.

## 5. PO nhiều cấp theo ngưỡng, hợp đồng (Catalog 79)
- PO: mặc định **giữ T15.1 / D-11** (dưới 50 triệu `scm-po`, ≥ 50 triệu 1 cấp `scm-po-director`). Bật `BusinessRules:PurchaseOrderDirectorTwoLevel` → PO ≥ ngưỡng 2 cấp `scm-po` → `scm-po-director`. `PurchaseOrder/GetDetail` thêm `pendingApproval` (dùng `pendingApproval.screenId` thay `approvalScreenId` khi đang chờ).
- Hợp đồng: mặc định 1 cấp `crm-contract` (spec "1 cấp Ban giám đốc"); số cấp đổi bằng cấu hình `BusinessRules:ApprovalSteps:CONTRACT` (vd. `["crm-contract-l1","crm-contract"]` – cần thêm mục menu `crm-contract-l1`). FE không phải đổi gì: luôn đọc `pendingApproval` / `listStepInfo`.
- Mọi loại khác (công thức, đơn lệch, sửa đơn sau giờ chốt, đổi món) vẫn 1 cấp; cấu hình được như hợp đồng.
