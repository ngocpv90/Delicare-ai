# Rà ô gõ chữ tự do trong form (UI-6.17 D – D-98 e, CLAUDE.md §4.5 điểm 10)

Luật: trường đã có danh mục / bảng tham chiếu / bộ giá trị cố định thì **bắt buộc chọn**, không gõ chữ tự do; cột chữ tự do trùng nghĩa với một trường chọn → ẩn khỏi giao diện, máy chủ tự ghi. Ô cần **danh mục mới chưa có bảng** → không tự tạo bảng, ghi GAP để anh duyệt.

Cách rà (2026-11-24, script `scratchpad/ui617-review.mjs`): mọi bảng có màn trong menu (`catalog.js`), cột chữ (`nvarchar`) có tên dạng Loại / Nhóm / Đơn vị / Lý do / Kênh / Mức / Ca / Buổi… (bỏ cột tên, mã, số chứng từ, ghi chú, địa chỉ, trạng thái – trạng thái đã có ô chọn chung). Cột **khóa ngoại (…Id)** đã là ô chọn (form tự sinh `UI.form`) nên không liệt kê.

Tổng: **59** ô. Cột "Đã sửa / cần duyệt" cập nhật theo từng task UI-6.x.

| # | Màn | Ô (cột) | Đề xuất danh mục | Đã sửa / cần duyệt |
|---|---|---|---|---|
| 1 | Bảng giá nhà cung cấp | `SupplierMaterial.DeliveryFrequency` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Làm ở UI-6.19 |
| 2 | Chấm công | `AttendanceEvent.EventType` | Bộ giá trị cố định đã có (STATUS_CATALOG) – dùng ô chọn | Cần anh duyệt / task sau |
| 3 | Chấm công | `AttendanceEvent.SourceChannel` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 4 | Chênh lệch tiêu hao | `MaterialConsumptionVariance.Reason` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 5 | Chi tiết đơn đặt suất | `OrderLine.MealType` | Bộ giá trị Buổi (SANG / TRUA / TOI / DEM) | Cần anh duyệt / task sau |
| 6 | Chi tiết kiểm kê | `StockCountLine.Reason` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 7 | Chi tiết kiểm kê | `StockCountLine.VarianceReason` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 8 | Chi tiết kiểm tra hàng nhập | `QCInspectionLine.Result` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 9 | Chi tiết thực đơn | `MenuLine.MealType` | Bộ giá trị Buổi (SANG / TRUA / TOI / DEM) | Cần anh duyệt / task sau |
| 10 | Chính sách giá | `PricePolicie.MealType` | Bộ giá trị Buổi (SANG / TRUA / TOI / DEM) | Cần anh duyệt / task sau |
| 11 | Chứng từ trong yêu cầu duyệt | `ApprovalItem.ItemType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 12 | Diễn biến giao hàng | `DeliveryEvent.EventType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 13 | Diễn biến giao hàng | `DeliveryEvent.SourceChannel` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 14 | Đơn đổi ca | `ShiftSwapRequest.Reason` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Làm ở UI-6.39 |
| 15 | Đơn vị tính | `UnitMeasure.UnitType` | Danh mục Đơn vị tính (UnitMeasure) | Cần anh duyệt / task sau |
| 16 | Đơn vị tính | `UnitMeasure.UomType` | Danh mục Đơn vị tính (UnitMeasure) | Cần anh duyệt / task sau |
| 17 | Đơn xin nghỉ | `LeaveRequest.Reason` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Làm ở UI-6.39 |
| 18 | Dự báo suất ăn | `Forecast.MealType` | Bộ giá trị Buổi (SANG / TRUA / TOI / DEM) | Cần anh duyệt / task sau |
| 19 | Hao hụt | `MealWasteRecord.Reason` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 20 | Hao hụt | `MealWasteRecord.WasteType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 21 | Kết quả đóng gói | `PackingResult.PackagingType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 22 | Khách hàng | `Customer.CustomerType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 23 | Khách hàng | `Customer.Gender` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 24 | Khách hàng | `Customer.Source` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 25 | Khay hộp theo khách | `PackagingLedger.PackagingType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 26 | Khay hộp theo khách | `PackagingLedger.TransactionType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 27 | Kho | `Warehouse.WarehouseType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 28 | Kiểm tra hàng nhập | `QCInspection.Result` | Bộ giá trị cố định đã có (STATUS_CATALOG) – dùng ô chọn | Cần anh duyệt / task sau |
| 29 | Kiểm tra khay hộp thu hồi | `ReturnInspection.InspectionResult` | Bộ giá trị cố định đã có (STATUS_CATALOG) – dùng ô chọn | Cần anh duyệt / task sau |
| 30 | Kiểm tra khay hộp thu hồi | `ReturnInspection.Result` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 31 | Lịch sử xử lý phản ánh | `ComplaintHistory.EventType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 32 | Loại suất & đơn giá | `ContractMeal.MealType` | Bộ giá trị Buổi (SANG / TRUA / TOI / DEM) | Cần anh duyệt / task sau |
| 33 | Loại suất & đơn giá | `ContractMeal.ServiceType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 34 | Món ăn | `Dishe.MealCategory` | Danh mục nhóm tương ứng (nếu có bảng) | Ẩn khỏi form, máy chủ tự ghi = mã nhóm món (UI-6.17) |
| 35 | Nguyên liệu | `Ingredient.StorageCondition` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | **Giữ gõ tự do (UI-6.20):** chỉ là mô tả thêm, loại bảo quản đã là ô chọn |
| 36 | Nguyên liệu | `Ingredient.StorageType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | **Đã sửa UI-6.20:** chọn 1 trong 3 (DRY / CHILLED / FROZEN), máy chủ chặn mã khác |
| 37 | Nhà cung cấp | `Supplier.SupplierType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 38 | Nhân viên | `Employee.Gender` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Làm ở UI-6.36 |
| 39 | Nhật ký nhiệt độ | `TemperatureLog.MeasurementType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 40 | Nhật ký nhiệt độ | `TemperatureLog.SourceChannel` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 41 | Phản ánh khách hàng | `CustomerComplaint.Severity` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 42 | Phản ánh khách hàng | `CustomerComplaint.SourceChannel` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 43 | Phê duyệt | `ApprovalRequest.BusinessType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 44 | Phiếu kiểm kê | `StockCount.CountType` | Bộ giá trị cố định đã có (STATUS_CATALOG) – dùng ô chọn | Cần anh duyệt / task sau |
| 45 | Sơ đồ tổ chức | `OrganizationUnit.UnitType` | Danh mục Đơn vị tính (UnitMeasure) | Cần anh duyệt / task sau |
| 46 | Sổ kho | `InventoryTransaction.Reason` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 47 | Sổ kho | `InventoryTransaction.ReferenceType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 48 | Sổ kho | `InventoryTransaction.TransactionType` | Bộ giá trị cố định đã có (STATUS_CATALOG) – dùng ô chọn | Cần anh duyệt / task sau |
| 49 | Sự cố nhà cung cấp | `SupplierIssue.IssueType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Làm ở UI-6.33 (hướng xử lý chuẩn) |
| 50 | Sự cố nhà cung cấp | `SupplierIssue.Severity` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Làm ở UI-6.33 (hướng xử lý chuẩn) |
| 51 | Tài liệu đính kèm | `DocumentFile.FileType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 52 | Thực đơn riêng cho khách | `MenuAssignment.MealType` | Bộ giá trị Buổi (SANG / TRUA / TOI / DEM) | Cần anh duyệt / task sau |
| 53 | Thực đơn tuần | `Menu.MenuType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 54 | Tin nhắn đến | `InboundMessage.MessageType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 55 | Tin nhắn đi | `OutboundMessage.MessageType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 56 | Vệ sinh khay hộp | `CleaningBatche.QcResult` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 57 | Vị trí kệ | `StorageLocation.LocationType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 58 | Xe giao hàng | `Vehicle.VehicleType` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |
| 59 | Yêu cầu đổi món | `MenuChangeRequest.Reason` | Bộ giá trị cố định trong BUSINESS_RULES (nếu có) – chưa có thì GAP | Cần anh duyệt / task sau |

## Luật chứng từ (UI-6.18 E – CLAUDE.md §4.5 điểm 11)

Kết quả rà chứng từ có luồng trạng thái (không ô chọn trạng thái, số / người lập / ngày lập máy chủ tự sinh, form tạo mới nhập được dòng) ghi tiếp ở mục này khi làm UI-6.18.
