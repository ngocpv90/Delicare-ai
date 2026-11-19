# UI-3 – Màn chi tiết nhà cung cấp (D-67) · 2026-11-19

Màn FE đã làm: `scm-supplier-360` (`modules/supplier360.js`, khung chung `modules/detail360.js`). Tài liệu này ghi API để soát / dùng lại.

## API mới / bổ sung

| API | Thay đổi | Ghi chú |
|---|---|---|
| `GET api/Supplier/GetOverview?id=` | **Thêm trường** (không đổi trường cũ): `openPoCount`, `openPoAmount`, `overduePoCount`, `receiptCount90d`, `qcPassRate`, `lastReceiptDate`, `issueCount`, `openIssueCount`, `overdueIssueCount`, `issueOnTimePct`, `lotOnHoldCount`, `lotNearExpiryCount`, `lotExpiryDays`, `priceExpiringCount`, `priceExpiryDays`, `listAlert[]` | `listAlert` = `{ code, severity (HIGH/MEDIUM/LOW), message, screen }`, mã: `PO_OVERDUE`, `ISSUE_OVERDUE`, `LOT_ON_HOLD`, `PRICE_EXPIRING`, `LOT_EXPIRING` |
| `GET api/Supplier/GetTrend?id=&from=&to=` | **Mới** | Theo tháng (from / to là ngày bất kỳ trong tháng; mặc định 6 tháng gần nhất; tối đa 24 tháng). `listMonth[] = { month, poAmount, receivedAmount, receiptCount, onTimePct, rejectPct }` |
| `GET api/PurchaseOrder/GetAll` | Thêm `FromOrderDate`, `ToOrderDate` | Gồm cả 2 đầu (SP `uspPurchaseOrder_GetAll`) |
| `GET api/GoodsReceipt/GetAll` | Thêm `FromReceiptDate`, `ToReceiptDate` | Gồm cả 2 đầu (SP `uspGoodsReceipt_GetAll`) |

## Định nghĩa số liệu

- **PO đang mở:** trạng thái khác Đã nhận đủ / Đóng / Hủy. **PO trễ:** Đã duyệt / Đã gửi NCC / Nhận một phần và hẹn giao < hôm nay (PO Nháp không tính trễ).
- **Giao đúng hạn 90 ngày:** phiếu nhập gắn PO có hẹn giao, ngày nhận ≤ ngày hẹn. **Hàng bị loại:** Σ SL loại ÷ Σ SL nhận (90 ngày); **đạt QC** = 100 − tỷ lệ loại.
- **Sự cố đúng hạn:** trên các sự cố có hạn – Đóng trước hạn, hoặc chưa đóng mà chưa tới hạn (hệ thống chỉ ghi giờ Đóng, không ghi giờ "Đã xử lý").
- **Lô tạm giữ / cận date:** lô của NCC còn tồn (InventoryBalance > 0); cận date = khả dụng, hết hạn trong `AlertRules:LotExpiryDays` ngày (mặc định 2).
- **Giá sắp hết hiệu lực:** giá đang hiệu lực, hết hiệu lực trong 15 ngày.
- **Giá trị đã nhận (GetTrend):** phiếu nhập Đã ghi sổ, Σ SL đạt × đơn giá dòng.

Quyền: xem `scm-supplier` (màn `scm-supplier-360` dùng quyền màn cha – `PERM_ALIAS`). Test: `MasterDataServiceTests.Supplier_overview_profile_alerts_and_monthly_trend`, `SearchFilterSqlServerTests.Purchase_order_and_goods_receipt_lists_filter_by_date_range`; `full-flow.http` mục 15c.
