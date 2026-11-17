# Bàn giao frontend – BE2.7: API dùng chung + mẫu in

Quy ước chung (đăng nhập, `Result` / `Results`, lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt, quyền theo `listMenu`): `P0-P2.md` mục 1.
Thử nhanh (dữ liệu `[DEMO]` có sẵn trên DB dev, đã chạy 43 / 43): `docs/api-tests/be2.7-common-print.http`. **Không có SP / seed / mục menu mới.**

**Mới ở BE2.7:** API dùng chung kiểm quyền **theo màn của danh sách** truyền vào (`resource` / loại phiếu). Không có quyền → **HTTP 403** kèm
`{ responseCode: 403, responseMessage: "Bạn không có quyền Xuất trên màn hình #/crm-order" }` (khác các API khác: 403 không body). FE hiện `responseMessage`.

## Quyền
| Action | Quyền |
|---|---|
| `Common/GetResources` | Đăng nhập; chỉ trả danh sách có quyền **Xem** |
| `Common/GetStatusSummary`, `Common/GetHistory` | **Xem** màn của `resource` (vd. `Order` → `#/crm-order`) |
| `Common/ExportList` | **Xuất** (`AllowExport`) màn của `resource` |
| `DocumentFile/Upload` | Gắn chứng từ: **Sửa** màn của chứng từ (`ProofOfDelivery` → `#/log-pod`); tài liệu chung: **Thêm** `#/adm-document` |
| `DocumentFile/GetList`, `Download` | **Xem** màn của chứng từ; danh sách mọi tệp (không truyền Resource): **Xem** `#/adm-document` |
| `DocumentFile/Delete` (cũ) | Xóa `#/adm-document` (không đổi) |
| `GoodsReceipt/Print`, `MaterialIssue/Print`, `StockCount/Print`, `DeliveryOrder/Print`, `ProofOfDelivery/Print`, `OrderReconciliation/Print` | **In** (`AllowPrint`) màn của chứng từ (`scm-gr`, `scm-issue`, `scm-stockcount`, `log-delivery-order`, `log-pod`, `crm-reconciliation`) – 403 không body (PermissionFilter) |
| `Common/PrintBatch` | **In** trên màn của **từng loại phiếu** có trong lô (thiếu 1 loại → 403 nêu màn) |

## 1. Danh sách được hỗ trợ (danh sách trắng)
`GET Common/GetResources` → `listData[] { resource, title, screenId, statusColumn, dateColumn, listSearchColumn[], listExportColumn[] (tiêu đề cột Excel), attachable }`.
`resource` = tên controller cũ (`Order`, `GoodsReceipt`, `PurchaseOrder`, `DeliveryOrder`, `ProofOfDelivery`, `CustomerComplaint`, `LeaveRequest`…, 35 danh sách).
Tên khác (`User`, `UserRole`, `AuditChange`, chữ lạ…) → 400 "Danh sách '…' không hỗ trợ chức năng này" – client **không** chọn được bảng / cột tùy ý.

## 2. Thẻ KPI / chip "Lọc nhanh" (Catalog 2)
`GET Common/GetStatusSummary?Resource=Order&SearchText=&FromDate=&ToDate=` (cùng bộ lọc với danh sách: tìm chữ trên cột mã / số / tên, khoảng ngày trên `dateColumn`)
→ `data { resource, statusColumn, total, listByStatus[] { status, statusName, count } }` – **đủ mọi mã của STATUS_CATALOG (kể cả count 0)**, rồi mã lạ trong dữ liệu,
`status = null` = "(Chưa có trạng thái)". Danh sách không có cột trạng thái (`ProofOfDelivery`) → 400.

## 3. Nút "Xuất Excel" (Catalog 8)
`GET Common/ExportList?Resource=&SearchText=&Status=&FromDate=&ToDate=&Format=xlsx|csv` → **tệp** (`Content-Disposition: attachment`, tên `<Resource>_yyyyMMdd_HHmm.xlsx`).
Lỗi → JSON Result (kiểm `content-type` trước khi lưu tệp). Cột cố định theo danh sách (`listExportColumn`), trạng thái → nhãn tiếng Việt, khóa ngoại → tên / số chứng từ.
Tối đa `BusinessRules:Common:ExportMaxRows` (10 000) dòng – nhiều hơn thì cắt + dòng ghi chú đỏ cuối file. CSV: UTF-8 có BOM; ô bắt đầu `= + - @` thêm `'` (chống chèn công thức).

## 4. Tệp đính kèm (Catalog 19–21) – màn `#/adm-document` + tab "Tài liệu" của chứng từ
| Chức năng | Action | Input | Kết quả |
|---|---|---|---|
| Tải lên | `POST DocumentFile/Upload` (**multipart/form-data**) | `File`, `Category?` (`signature` / `photo` / `evidence` / `contract` / `other` – mặc định other), `Resource?` + `RecordId?` (cả hai hoặc không) | `data { id, fileName, fileType, contentType, fileSize, category, resource, recordId, sha256, createdDate, createdBy, createdByName, url, isDuplicate }` |
| Danh sách | `GET DocumentFile/GetList` | `Resource?, RecordId?, SearchText?, FileType? (pdf), Category?, FromDate?, ToDate?, CreatedBy?, Page, PageSize (≤ 200)` | `listData[]` như trên, `totalRecords` |
| Tải xuống | `GET DocumentFile/Download?id=` | | tệp (attachment, `X-Content-Type-Options: nosniff`); lỗi → JSON Result. Mỗi lượt tải ghi AuditChange "Download" |

- Nhận: **.pdf, .jpg/.jpeg, .png, .xlsx, .docx**, ≤ **20 MB** (`DocumentStorage:MaxFileSizeMb`). Kiểm cả **nội dung** (đổi đuôi .exe → .pdf bị chặn), Office có macro bị chặn;
  .exe / .js / .html / .svg… luôn bị chặn. Lỗi → 400 kèm lý do.
- Tải lại **cùng nội dung lên cùng chứng từ + loại** → trả tệp cũ, `isDuplicate = true` (không thêm dòng).
- `url` là đường dẫn API nội bộ (cần token + quyền) – **không** phải link công khai: FE tải bằng `HttpClient` (blob) rồi mở / lưu, không gắn thẳng vào `<img src>`.
- Ảnh chữ ký / ảnh giao hàng POD: tải lên `Resource=ProofOfDelivery&RecordId=<podId>&Category=signature|photo`, rồi gửi `url` vào `SignatureUrl` / `PhotoUrl` của `DeliveryStop/Deliver` như cũ.
- Danh sách chứng từ gắn được tệp = `GetResources` có `attachable = true` (đơn, hợp đồng, khách, NCC, PO, đề nghị mua, phiếu nhập / xuất, kiểm kê, mẻ, chuyến, đơn giao, POD, đối soát, phản ánh, sự cố NCC).
- Tệp cũ lưu kiểu wwwroot (TestDemo) không tải qua `Download` (400).

## 5. Tab "Lịch sử xử lý" (Catalog 15)
`GET Common/GetHistory?resource=Order&id=` → `listData[]` mới nhất trước (tối đa 500):
`{ at, by, byName ("Họ tên (tên đăng nhập)"), source (AUDIT / APPROVAL / COMPLAINT / FILE), action, actionName ("Tạo mới", "Cập nhật", "Xóa", "Gửi duyệt", "Duyệt (cấp 1)", "Từ chối (cấp 2)", "Đính kèm tệp"…), entityName (bảng – gồm dòng con như OrderLine), entityId, comment, referenceNo (số yêu cầu duyệt), listChange[] { field, from, to, masked } }`.
Gộp: AuditChange của bản ghi **và dòng con** (dòng đơn, dòng phiếu, dòng công thức…), yêu cầu duyệt + quyết định từng cấp (ApprovalDecision), ComplaintHistory (phản ánh), tệp đính kèm.
Mật khẩu / token / khóa bí mật / tọa độ GPS → `to = "(đã ẩn)"`, `masked = true`. Cột hệ thống (CreatedDate, ModifiedBy…) không hiện. Bản ghi đã xóa mềm vẫn xem được lịch sử.

## 6. Mẫu in (Catalog 24–29) – nút "In" trên chi tiết chứng từ
`GET <Controller>/Print?id=&format=xlsx|docx` → **tệp** (mặc định **Excel .xlsx**, khổ A4; `format=docx` = Word, mỗi phiếu 1 trang). `format=pdf` → **400** (chưa có thư viện PDF chạy được – D-56 b).
| Phiếu | Action | Điều kiện | Nội dung |
|---|---|---|---|
| Phiếu nhập kho (Mẫu 01-VT tham khảo) | `GoodsReceipt/Print` | QC đã kết luận | NCC, đơn mua, kho, kết luận QC; dòng: mã / tên NL, ĐVT, số lô NCC, NSX, HSD, SL đặt / nhận / đạt / loại, đơn giá, thành tiền (đạt × giá), QC; ký: lập phiếu, giao hàng, QC, thủ kho, kế toán |
| Phiếu xuất kho (02-VT) | `MaterialIssue/Print` | Đã xuất (Issued) | lệnh SX + món, kho; dòng theo lô (số lô, HSD, SL); ký: lập phiếu, thủ kho, người nhận (bếp), kế toán |
| Biên bản kiểm kê (05-VT) | `StockCount/Print` | mọi trạng thái; **chưa ghi sổ → chữ "BẢN NHÁP"** | tồn sổ, thực tế, chênh lệch, %, giá trị, lý do; tổng giá trị lệch |
| Phiếu giao hàng | `DeliveryOrder/Print` | không | khách, đơn, điểm giao + địa chỉ + liên hệ, khung giờ, giờ dự kiến, chuyến, xe, tài xế; suất theo bữa / ca; số suất của phiếu giao; ô ký nhận + giờ / nhiệt độ |
| Biên bản giao nhận (POD) | `ProofOfDelivery/Print` (id = POD) | POD đã có | bên giao / nhận, thời điểm, người nhận, nhiệt độ bàn giao, suất giao / nhận / từ chối; chữ ký điện tử in dạng tham chiếu (chưa nhúng ảnh – D-56 d) |
| Biên bản đối soát | `OrderReconciliation/Print` | khác **Mở** | Đặt – Sản xuất – Đóng gói – Giao – Nhận, chênh lệch, %, giá trị chốt, cơ sở tính tiền |
Đầu trang = pháp nhân trong bảng `Company` (tên, địa chỉ, ĐT, MST). Lỗi / chưa đủ điều kiện → JSON Result 400.

## 7. In hàng loạt (Catalog 30)
`POST Common/PrintBatch` (**JSON**): `{ "items": [ { "type": "DELIVERY_ORDER", "id": "…" }, … ], "deliveryRouteId": "…"?, "format": "xlsx" | "docx" }`
- `type`: `GOODS_RECEIPT`, `MATERIAL_ISSUE`, `STOCK_COUNT`, `DELIVERY_ORDER`, `POD`, `RECONCILIATION` (nhận cả tên controller `GoodsReceipt`…).
- `deliveryRouteId` → thêm **mọi phiếu giao của chuyến** theo thứ tự điểm dừng (in phiếu giao theo tuyến). Trùng bị bỏ.
- Tối đa 100 phiếu (`BusinessRules:Common:PrintBatchMaxItems`); 1 phiếu lỗi (không tìm thấy / chưa đủ điều kiện) → 400 liệt kê, không in nửa chừng.
- Kết quả 1 tệp: Excel mỗi phiếu 1 sheet (tên sheet = số phiếu), Word mỗi phiếu 1 trang.

## ⚠ Thay đổi hành vi
1. Không đổi API cũ. `DocumentFileService` nhận thêm cấu hình `DocumentStorage` (không ảnh hưởng TestDemo).
2. API dùng chung trả **HTTP 403 có body Result** khi thiếu quyền (mục đầu file).
3. Mẫu in trả **Excel / Word**, chưa có PDF (spec 2.5 ghi PDF) – PROPOSED D-56 b; FE hiển thị nút "In (Excel)" / "In (Word)".
