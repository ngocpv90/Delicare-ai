# GAP Register (bản markdown để tra nhanh; bản gốc: DELICARE_Database_Gap_Register.xlsx)

## func

- **GAP-F-01** – Địa điểm vận hành (Operational Location) · BRD có object Operational Location; SQL không có bảng tương ứng. · Warehouse, KitchenWorkstation, OrganizationUnit.Address · adm-orgunit · BA xác nhận có cần bảng riêng hay dùng OrganizationUnit.UnitType
- **GAP-F-02** – SLA · BRD liệt kê SLA (Configuration) nhưng chưa có object/field; SQL chỉ có CustomerComplaint.SlaDueAt. · CustomerComplaint.SlaDueAt · log-complaint, crm-contract · Đặc tả SLA giao hàng/khiếu nại; quyết định lưu theo Contract hay bảng riêng
- **GAP-F-03** – Phiếu chuyển kho (Stock Transfer) · BRD có Stock Transfer; SQL chỉ có dòng ledger TRANSFER_IN/TRANSFER_OUT, không có header phiếu, kho nguồn/đích, phê duyệt. · InventoryTransaction.TransactionType, ReferenceType, ReferenceId · scm-transfer (nhãn GAP) · Bổ sung bảng phiếu chuyển kho hoặc chuẩn hóa ReferenceType
- **GAP-F-04** – Phiếu điều chỉnh tồn (Stock Adjustment) · BRD yêu cầu lý do và phê duyệt; SQL chỉ có ledger ADJUSTMENT + Reason. · InventoryTransaction.Reason, ApprovalRequest.BusinessType · scm-adjustment (nhãn GAP) · Dùng ApprovalRequest với BusinessType=STOCK_ADJUSTMENT hoặc bổ sung bảng
- **GAP-F-05** – GPS / theo dõi vị trí xe liên tục · LocationTracking chỉ có IPv4, PcName (nhật ký truy cập). DeliveryEvent có Latitude/Longitude theo sự kiện, không tracking liên tục. · LocationTracking, DeliveryEvent.Latitude/Longitude · log-gps (nhãn GAP) · Xác nhận nhu cầu GPS realtime; nguồn dữ liệu thiết bị
- **GAP-F-06** – Danh mục tham chiếu dùng chung · 12/15 danh mục tham chiếu BRD không có bảng (xem sheet 08); giá trị lưu text tự do. · MealType, WasteType, Reason, CustomerType, SupplierType, WarehouseType, StorageType… · adm-reference · Chốt danh sách giá trị cố định trong cấu hình ứng dụng hoặc bổ sung bảng
- **GAP-F-07** – Giờ chốt số lượng đơn hàng · Không có cột/cấu hình giờ chốt (cut-off) theo hợp đồng/ca. · Order, ContractMeal, Shift · crm-order · Xác định cut-off theo Contract/ContractMeal/Shift
- **GAP-F-08** – Mẫu quy trình phê duyệt · ApprovalStep lưu theo từng yêu cầu; không có bảng định nghĩa quy trình, cấp duyệt, hạn mức theo BusinessType. · ApprovalRequest, ApprovalStep · wf-inbox · Định nghĩa ma trận phê duyệt; quyết định lưu cấu hình
- **GAP-F-09** – Đính kèm tài liệu cho chứng từ · DocumentFile chỉ có TestDemoId – không liên kết được với chứng từ nghiệp vụ (PO, phiếu nhập, POD…). · DocumentFile.TestDemoId · adm-document · Bổ sung cột liên kết đối tượng hoặc dùng các cột *Url hiện có
- **GAP-F-10** – QR & truy xuất nguồn gốc · Menu HTML gốc có "QR & Truy xuất nguồn gốc"; SQL không có cột mã QR/nhãn. Truy xuất chỉ dựng được qua chuỗi quan hệ lô → mẻ → đơn. · MaterialLot, MaterialIssueLine, ProductionBatche, PackingOrder, DeliveryOrder · (gộp vào scm-lot – màn hình truy xuất theo lô) · Xác nhận phạm vi QR; truy xuất demo bằng chuỗi quan hệ hiện có
- **GAP-F-11** – Ngưỡng cảnh báo chênh lệch · Không có nơi cấu hình ngưỡng chênh lệch đối soát/tiêu hao/hao hụt. · OrderReconciliation.VariancePercent, MaterialConsumptionVariance.VariancePercent · crm-reconciliation, scm-consumption-var · Chốt ngưỡng và nơi lưu
- **GAP-F-12** – Định biên nhân sự theo ca/trạm · Không có bảng định biên (số người yêu cầu theo ca/trạm/dây chuyền). · Shift, ShiftAssignment, KitchenWorkstation · hr-shift-assign · Xác nhận nhu cầu định biên
- **GAP-F-13** – Hóa đơn / công nợ (Kế toán) · Prompt có vai trò Kế toán; SQL không có bảng hóa đơn, thanh toán, công nợ. Chỉ có OrderReconciliation.FinalAmount, MealCost.Revenue. · OrderReconciliation, MealCost · crm-reconciliation · Xác nhận phạm vi kế toán (tích hợp phần mềm kế toán?)
- **GAP-F-14** – Thông báo trong ứng dụng · Không có bảng thông báo nội bộ; chỉ có OutboundMessage cho kênh ngoài. · OutboundMessage · Header – chuông thông báo · Dùng OutboundMessage hay bổ sung bảng thông báo
- **GAP-F-15** – Cấu hình hệ thống · Menu HTML gốc có "Cấu hình hệ thống"; SQL không có bảng tham số hệ thống. · — · (không đưa vào menu) · Xác nhận nhu cầu
- **GAP-F-16** – Cổng khách hàng · Khách hàng là một vai trò; không có bảng tài khoản khách hàng riêng (User/Person không liên kết Customer). · User, Person, CustomerContact, UserExternalIdentity · crm-customer · Xác nhận khách hàng đăng nhập Web hay chỉ qua Bot

## col

- **GAP-C-01** – Giá thành thực tế · BRD gồm Packaging, Logistics, Utility; SQL ActualCost chỉ có MaterialCost, LaborCost, OverheadCost, WasteCost, OtherCost. · ActualCost · cost-actual · Tạm gộp vào OtherCost/OverheadCost – BA xác nhận
- **GAP-C-02** – Điểm đầu/cuối tuyến · BRD: Delivery Route có điểm đầu/cuối; SQL không có cột. · DeliveryRoute · log-route · Dùng DeliveryStop SequenceNo đầu/cuối hoặc bổ sung cột
- **GAP-C-03** – Xếp xe ↔ đóng gói ↔ xe · BRD: Loading liên kết Packing và Vehicle; DeliveryLoading không có PackingOrderId, xe chỉ là VehicleCode text. · DeliveryLoading.VehicleCode · log-loading · Suy xe qua DeliveryRoute.VehicleId; BA xác nhận
- **GAP-C-04** – Kế hoạch theo dây chuyền/menu · BRD lập kế hoạch theo ngày, ca, line, menu; ProductionPlan không có ProductionLineId/MenuId. · ProductionPlan, ProductionOrder.ProductionLineId · prod-plan · Line xác định ở cấp ProductionOrder
- **GAP-C-05** – Số lượng "bẩn" khi kiểm tra thu hồi · BRD có kết quả bẩn; ReturnInspection chỉ có DamagedQty, MissingQty. · ReturnInspection · log-return-inspection · Ghi nhận qua InspectionResult=DIRTY; BA xác nhận
- **GAP-C-06** – Variance % kiểm kê · BRD yêu cầu Variance Qty, Value, %; StockCountLine không có cột %. · StockCountLine · scm-stockcount-line · Tính trên UI = VarianceQuantity / SystemQuantity
- **GAP-C-07** – Đơn vị mua/xuất không có cột UnitId · Ingredient.PurchaseUomId, Ingredient.IssueUomId, SupplierMaterial.PurchaseUomId không có cột Unit… tương ứng (trái với quy tắc dùng UnitId). · Ingredient, SupplierMaterial · scm-ingredient, scm-supplier-material · Giữ cột Uom… và tham chiếu UnitMeasure – DEV xác nhận
- **GAP-C-08** – Khu vực sản xuất · Chỉ có KitchenWorkstation.AreaName dạng text; không có bảng khu vực. · KitchenWorkstation.AreaName · prod-workstation, prod-batch · Dùng AreaName làm nhóm hiển thị
- **GAP-C-09** – Vùng/khu vực khách hàng · Customer.RegionId, AreaId không có bảng đích (AdministrativeRegion là vùng hành chính, chưa xác nhận). · Customer.RegionId, Customer.AreaId · crm-customer · BA xác nhận dùng AdministrativeRegion/Province hay bỏ
- **GAP-C-10** – Liên kết Person – Employee · Không có cột liên kết Person với Employee; User → Person. · Person, Employee, User · adm-user, adm-employee · Xác nhận mô hình tài khoản – nhân viên

## dq

- **GAP-Q-01** – 88 bảng có DeleteddBy; 68 bảng có cả DeletedBy và DeleteddBy; 20 bảng chỉ có DeleteddBy (User, Customer, UserRole…). · 88 · Audit xóa mềm không nhất quán · Giữ nguyên tên theo SQL (không tự đổi); DEV quyết định migration · Open
- **GAP-Q-02** – Cột cũ/mới tồn tại song song (…No/…Number, …Qty/…Quantity, UomId/UnitId, LotId/MaterialLotId…). · 141 · Nguy cơ ghi 2 nơi · PO xác nhận dùng cột mới; danh sách trong Mapping sheet 05 · Open
- **GAP-Q-03** – Cột Default trong Dictionary 20261113 trống cho toàn bộ ràng buộc DEFAULT trong SQL. · 323 · Dev/BA không thấy giá trị mặc định · Mapping sheet 03 lấy DEFAULT trực tiếp từ SQL · Open
- **GAP-Q-04** – CreatedDate/ModifiedDate: 90 bảng datetime, 33 bảng datetime2(0); nhiều cột ngày khác lẫn datetime/datetime2/date. · 123 · Sai lệch timezone/độ chính xác · Chốt quy ước UTC & kiểu chuẩn · Open
- **GAP-Q-05** – Khoảng 35 bảng nghiệp vụ mới không có DeletedDate/DeletedBy; CoreDataMergeConflict không có cột audit. · 54 · Xóa mềm không thống nhất · Xác nhận chính sách xóa mềm · Open
- **GAP-Q-06** – ObjectMenu bị xếp "Thực đơn & kế hoạch" (PO xác nhận là menu hệ thống – P0). · 1 · Hiểu sai nghiệp vụ · Đã điều chỉnh trong Mapping · Open
- **GAP-Q-07** – UserGroup/UserRole dùng mô tả User; DeliveryOrder, PackingOrder, OrderReconciliation dùng mô tả Order; MealCost, MenuLine, ContractMeal, DeliveryLoading, DeliveryExecution… mô tả chung chung. · 11 · Hiểu sai bảng · Đã thay bằng mô tả BRD/PO trong Mapping sheet 02 · Open
- **GAP-Q-08** – Cấu trúc chỉ có IPv4, PcName. · 1 · Hiểu nhầm là GPS · Đổi mô tả thành nhật ký truy cập (P0) · Open
- **GAP-Q-09** – MedicineWarehouse, ActualCosts_Legacy_20261113 được xếp Business; PO quyết định bỏ qua nhóm bệnh viện. · 2 · — · Xếp Legacy, chỉ hiện ở DB Explorer · Open
- **GAP-Q-10** – Suppliers_Legacy_20261113 có trong kế hoạch nhưng không có trong SQL. · 1 · — · Không đưa vào mockup · Open
- **GAP-Q-11** – CoreDataMergeConflict.Id là bigint (123 bảng khác dùng uniqueidentifier). · 1 · — · Ghi nhận · Open
- **GAP-Q-12** – SQL dùng PascalCase (Pending, Open), BRD dùng UPPERCASE (PASS, READY). · Lọc/so khớp trạng thái · PO đồng ý: giữ đúng cách viết theo nguồn; đề xuất mới viết PascalCase · Open

## ref

- **GAP-RD-01** – — · ContractMeal.MealType, OrderLine.MealType, MenuLine.MealType, MenuAssignment.MealType, PricePolicie.MealType, Forecast.MealType · BREAKFAST / LUNCH / DINNER / NIGHT / DIET (bệnh viện) – PROPOSED · BA/PO · Open
- **GAP-RD-02** – Shift · — · Ca 1 Sáng sớm, Ca 2 Buổi sáng, Ca 3 Buổi chiều, Ca 4 Ban đêm (theo HTML gốc) · — · Có bảng
- **GAP-RD-03** – — · Order.Status · Xem sheet 07 · BA/PO · Open
- **GAP-RD-04** – — · ProductionOrder.Status, ProductionBatche.Status · Xem sheet 07 · BA/PO · Open
- **GAP-RD-05** – — · DeliveryExecution.Status, DeliveryRoute.Status, DeliveryStop.StopStatus · Xem sheet 07 · BA/PO · Open
- **GAP-RD-06** – — · QCInspection.Result, MaterialLot.QcStatus, ProductionBatche.QcStatus · PASS / HOLD / REJECT (BRD) · — · Theo BRD
- **GAP-RD-07** – — · Warehouse.WarehouseType · RAW (nguyên liệu) / FINISHED (thành phẩm) / PACKAGING / COLD – PROPOSED · BA/PO · Open
- **GAP-RD-08** – — · Ingredient.StorageType, StorageLocation.LocationType · AMBIENT / CHILLED / FROZEN / DRY – PROPOSED · BA/PO · Open
- **GAP-RD-09** – — · MealWasteRecord.WasteType, MealWasteRecord.Reason · OVERPRODUCTION / QC_REJECT / SPOILAGE / PREP_LOSS / RETURNED – PROPOSED · BA/PO · Open
- **GAP-RD-10** – — · InventoryTransaction.Reason, StockCountLine.VarianceReason · COUNT_VARIANCE / DAMAGED / EXPIRED / LOST – PROPOSED · BA/PO · Open
- **GAP-RD-11** – — · ReturnInspection.InspectionResult · OK / SHORTAGE / DAMAGED / DIRTY / REJECTED (BRD) · — · Theo BRD
- **GAP-RD-12** – — · Customer.CustomerType (nvarchar(20)) · HOSPITAL / SCHOOL / FACTORY / CONSTRUCTION / OTHER – PROPOSED · BA/PO · Open
- **GAP-RD-13** – — · Supplier.SupplierType · MEAT / VEGETABLE / DRY_GOODS / PACKAGING / OTHER – PROPOSED · BA/PO · Open
- **GAP-RD-14** – DishCategorie · — · Có bảng · — · Có bảng
- **GAP-RD-15** – IngredientCategorie · — · Có bảng · — · Có bảng
