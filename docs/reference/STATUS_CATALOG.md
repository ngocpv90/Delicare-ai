# Danh mục trạng thái (đã được PO đồng ý dùng làm đề xuất – vẫn đánh dấu PROPOSED cho tới khi BA chốt)

| Bảng | Cột | Giá trị (code) | Nghĩa tiếng Việt | Nguồn |
|---|---|---|---|---|
| Customer | Status | Active / Inactive | Đang hoạt động / Ngừng | PROPOSED – BA/PO CONFIRM |
| Contract | Status | Draft → PendingApproval → Active → Expired / Terminated | Nháp → Chờ duyệt → Hiệu lực → Hết hạn / Chấm dứt | PROPOSED – BA/PO CONFIRM |
| Forecast | Status | Draft → Published → Superseded | Nháp → Đã công bố → Bị thay thế | PROPOSED – BA/PO CONFIRM |
| Order | Status | Draft → Submitted → Confirmed → Locked → InProduction → Delivered → Reconciled; Cancelled | Nháp → Đã gửi → Đã xác nhận → Đã chốt → Đang sản xuất → Đã giao → Đã đối soát; Hủy | PROPOSED – BA/PO CONFIRM |
| OrderReconciliation | Status | Open → Matched / Variance → Closed | Mở → Khớp / Chênh lệch → Đóng | PROPOSED – BA/PO CONFIRM |
| Recipe | Status | Draft → PendingApproval → Approved → Locked → Obsolete | Nháp → Chờ duyệt → Đã duyệt → Đã khóa → Hết dùng | PROPOSED – BA/PO CONFIRM |
| Menu | Status | Draft → Approved → Published → Expired | Nháp → Đã duyệt → Đã công bố → Hết hiệu lực | PROPOSED – BA/PO CONFIRM |
| MenuChangeRequest | RequestStatus | Pending → Approved / Rejected / Cancelled | Chờ duyệt → Duyệt / Từ chối / Hủy | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| MaterialRequirement | Status | Calculated → Shortage / Sufficient → Requested | Đã tính → Thiếu / Đủ → Đã đề nghị mua | PROPOSED – BA/PO CONFIRM |
| PurchaseRequest | Status | Draft → PendingApproval → Approved / Rejected → Converted | Nháp → Chờ duyệt → Duyệt / Từ chối → Đã lập PO | PROPOSED – BA/PO CONFIRM |
| PurchaseOrder | Status | Draft → Approved → Sent → PartiallyReceived → Received → Closed; Cancelled | Nháp → Đã duyệt → Đã gửi NCC → Nhận một phần → Đã nhận đủ → Đóng; Hủy | PROPOSED – BA/PO CONFIRM |
| GoodsReceipt | Status | Draft → QCPending → Accepted / PartiallyAccepted / Rejected → Posted | Nháp → Chờ QC → Đạt / Đạt một phần / Từ chối → Đã ghi sổ | PROPOSED – BA/PO CONFIRM |
| QCInspection | Result | PASS / HOLD / REJECT | Đạt / Tạm giữ / Từ chối | Theo nguồn – không đổi |
| MaterialLot | QcStatus | PASS / HOLD / REJECT | Đạt / Tạm giữ / Từ chối | Theo nguồn – không đổi |
| MaterialLot | Status | Available / OnHold / Expired / Consumed | Khả dụng / Tạm giữ / Hết hạn / Đã dùng hết | PROPOSED – BA/PO CONFIRM |
| InventoryTransaction | TransactionType | RECEIPT, ISSUE, TRANSFER_IN, TRANSFER_OUT, ADJUSTMENT, WASTE, RETURN | Nhập, Xuất, Chuyển đến, Chuyển đi, Điều chỉnh, Hủy/hao hụt, Trả lại | Theo nguồn – không đổi |
| MaterialReservation | Status | Reserved → PartiallyIssued → Issued → Released | Đã giữ → Đã xuất một phần → Đã xuất → Đã giải phóng | PROPOSED – BA/PO CONFIRM |
| MaterialIssue | Status | Draft → Issued → Cancelled | Nháp → Đã xuất → Hủy | PROPOSED – BA/PO CONFIRM |
| StockCount | CountType | DAILY / WEEKLY / MONTHLY / ADHOC | Ngày / Tuần / Tháng / Đột xuất | Theo nguồn – không đổi |
| StockCount | Status | Draft → Counting → PendingApproval → Posted | Nháp → Đang kiểm → Chờ duyệt → Đã ghi sổ | PROPOSED – BA/PO CONFIRM |
| ProductionPlan | Status | Draft → Approved → Released → Completed | Nháp → Đã duyệt → Đã phát hành → Hoàn thành | PROPOSED – BA/PO CONFIRM |
| ProductionOrder | Status | Planned → Released → InProgress → Completed → Closed; Cancelled | Kế hoạch → Phát hành → Đang SX → Hoàn thành → Đóng; Hủy | PROPOSED – BA/PO CONFIRM |
| ProductionBatche | Status | Planned → Cooking → QCPending → Passed / Failed → Packed | Kế hoạch → Đang nấu → Chờ QC → Đạt / Không đạt → Đã đóng gói | PROPOSED – BA/PO CONFIRM |
| ProductionBatche | QcStatus | PASS / HOLD / REJECT | Đạt / Tạm giữ / Từ chối | Theo nguồn – không đổi |
| FoodSafetyChecklist | Status | Active / Inactive | Đang áp dụng / Ngừng | Theo nguồn – không đổi |
| ProductionLine | Status | Active / Inactive | Hoạt động / Ngừng | Theo nguồn – không đổi |
| KitchenWorkstation | Status | Active / Inactive | Hoạt động / Ngừng | Theo nguồn – không đổi |
| PackingOrder | Status | Planned → Packing → Packed | Kế hoạch → Đang đóng → Đã đóng | PROPOSED – BA/PO CONFIRM |
| DeliveryRoute | Status | Planned → Loading → InTransit → Completed | Kế hoạch → Đang xếp → Đang giao → Hoàn thành | PROPOSED – BA/PO CONFIRM |
| DeliveryStop | StopStatus | Planned → Arrived → Delivered / Failed | Kế hoạch → Đã đến → Đã giao / Thất bại | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| DeliveryExecution | Status | READY → LOADED → DISPATCHED → ARRIVED → DELIVERED / FAILED | Sẵn sàng → Đã xếp → Đã xuất phát → Đã đến → Đã giao / Thất bại | Theo nguồn – không đổi |
| ContainerReturn | Status | Expected → Returned → Inspected | Chờ thu hồi → Đã thu hồi → Đã kiểm tra | PROPOSED – BA/PO CONFIRM |
| ReturnInspection | InspectionResult | OK / SHORTAGE / DAMAGED / DIRTY / REJECTED | Đủ / Thiếu / Hỏng / Bẩn / Không đạt | Theo nguồn – không đổi |
| CustomerComplaint | ComplaintStatus | Open → InProgress → Resolved → Closed; Rejected | Mới → Đang xử lý → Đã xử lý → Đóng; Từ chối | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| SupplierIssue | IssueStatus | Open → InProgress → Resolved → Closed | Mới → Đang xử lý → Đã xử lý → Đóng | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| ApprovalRequest | RequestStatus | Pending → InProgress → Approved / Rejected / Cancelled | Chờ duyệt → Đang duyệt → Duyệt / Từ chối / Hủy | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| ApprovalStep | DecisionStatus | Waiting → Approved / Rejected / Skipped | Chờ → Duyệt / Từ chối / Bỏ qua | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| LeaveRequest | RequestStatus | Pending → Approved / Rejected / Cancelled | Chờ duyệt → Duyệt / Từ chối / Hủy | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| ShiftSwapRequest | RequestStatus | Pending → Approved / Rejected / Cancelled | Chờ duyệt → Duyệt / Từ chối / Hủy | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| ShiftAssignment | AssignmentStatus | Planned → Confirmed → CheckedIn → Completed / Absent | Kế hoạch → Xác nhận → Đã vào ca → Hoàn thành / Vắng | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| AttendanceEvent | EventType | CHECK_IN / CHECK_OUT | Vào ca / Ra ca | PROPOSED – BA/PO CONFIRM |
| IntegrationChannel | ChannelStatus | Inactive / Active | Ngừng / Hoạt động | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| BotConfiguration | BotStatus | Inactive / Active | Ngừng / Hoạt động | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| UserExternalIdentity | IdentityStatus | Active / Revoked | Hoạt động / Thu hồi | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| InboundMessage | ProcessingStatus | Received → Processed / Failed | Đã nhận → Đã xử lý / Lỗi | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| OutboundMessage | MessageStatus | Queued → Sent → Delivered / Failed | Chờ gửi → Đã gửi → Đã nhận / Lỗi | Mặc định SQL giữ nguyên; giá trị còn lại PROPOSED |
| AnalyticsRefreshLog | RunStatus | Running → Succeeded / Failed | Đang chạy → Thành công / Lỗi | PROPOSED – BA/PO CONFIRM |