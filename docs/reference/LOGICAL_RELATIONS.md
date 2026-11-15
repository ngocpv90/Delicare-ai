# Quan hệ logic giữa các bảng (không có FK vật lý)

Dùng cho: kiểm tra tồn tại khi ghi, `ReferenceGuard` khi xóa mềm (T07), JOIN trong query, sinh dropdown ở Angular.

## 224 quan hệ đã xác định

| Bảng nguồn | Cột | → Bảng đích | Cột đích | Căn cứ | Mức |
|---|---|---|---|---|---|
| ActualCost | ProductionOrderId | ProductionOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ActualCosts_Legacy_20261113 | ProductionOrderId | ProductionOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| AdministrativeDistrict | AdministrativeProvinceId | AdministrativeProvince | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| AdministrativeDistrict | AdministrativeUnitId | AdministrativeUnit | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| AdministrativeProvince | AdministrativeRegionId | AdministrativeRegion | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| AdministrativeProvince | AdministrativeUnitId | AdministrativeUnit | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| AdministrativeWard | AdministrativeDistrictId | AdministrativeDistrict | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| AdministrativeWard | AdministrativeUnitId | AdministrativeUnit | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ApprovalDecision | ApprovalRequestId | ApprovalRequest | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ApprovalDecision | ApprovalStepId | ApprovalStep | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ApprovalItem | ApprovalRequestId | ApprovalRequest | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ApprovalStep | ApprovalRequestId | ApprovalRequest | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| AttendanceEvent | EmployeeId | Employee | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| AttendanceEvent | ShiftAssignmentId | ShiftAssignment | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| BotConfiguration | IntegrationChannelId | IntegrationChannel | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| CleaningBatche | ReturnInspectionId | ReturnInspection | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ContainerReturn | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ContainerReturn | DeliveryOrderId | DeliveryOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Contract | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ContractMeal | ContractId | Contract | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Customer | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| CustomerComplaint | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| CustomerComplaint | DeliveryOrderId | DeliveryOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| CustomerComplaint | DeliveryRouteId | DeliveryRoute | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| CustomerComplaint | OrderId | Order | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| CustomerContact | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| CustomerDeliveryLocation | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryEvent | DeliveryOrderId | DeliveryOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryEvent | DeliveryRouteId | DeliveryRoute | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryEvent | DeliveryStopId | DeliveryStop | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryExecution | DeliveryOrderId | DeliveryOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryExecution | DeliveryRouteId | DeliveryRoute | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryLoading | DeliveryOrderId | DeliveryOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryLoading | DeliveryRouteId | DeliveryRoute | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryLoading | EmployeeId | Employee | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryOrder | CustomerDeliveryLocationId | CustomerDeliveryLocation | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryOrder | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryOrder | DeliveryRouteId | DeliveryRoute | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryOrder | OrderId | Order | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryRoute | VehicleId | Vehicle | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryStop | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryStop | DeliveryOrderId | DeliveryOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DeliveryStop | DeliveryRouteId | DeliveryRoute | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| DocumentFile | TestDemoId | TestDemo | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Employee | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Employee | OrganizationUnitId | OrganizationUnit | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Employee | PositionId | Position | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| FoodSafetyChecklistResult | ShiftId | Shift | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Forecast | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Forecast | ShiftId | Shift | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| GoodsReceipt | PurchaseOrderId | PurchaseOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| GoodsReceipt | SupplierId | Supplier | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| GoodsReceipt | WarehouseId | Warehouse | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| GoodsReceiptLine | GoodsReceiptId | GoodsReceipt | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| GoodsReceiptLine | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InboundMessage | IntegrationChannelId | IntegrationChannel | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Ingredient | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| IntegrationLog | IntegrationChannelId | IntegrationChannel | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryBalance | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryBalance | MaterialLotId | MaterialLot | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryBalance | StorageLocationId | StorageLocation | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryBalance | WarehouseId | Warehouse | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryPolicie | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryPolicie | WarehouseId | Warehouse | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryTransaction | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryTransaction | MaterialLotId | MaterialLot | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryTransaction | StorageLocationId | StorageLocation | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryTransaction | WarehouseId | Warehouse | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| KitchenWorkstation | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| KitchenWorkstation | OrganizationUnitId | OrganizationUnit | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| LeaveRequest | ApprovalRequestId | ApprovalRequest | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| LeaveRequest | EmployeeId | Employee | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialConsumptionVariance | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialConsumptionVariance | ProductionOrderId | ProductionOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialIssue | ProductionOrderId | ProductionOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialIssue | WarehouseId | Warehouse | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialIssueLine | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialIssueLine | MaterialIssueId | MaterialIssue | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialIssueLine | MaterialLotId | MaterialLot | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialLot | GoodsReceiptLineId | GoodsReceiptLine | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialLot | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialLot | SupplierId | Supplier | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialRequirement | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialRequirement | ProductionPlanId | ProductionPlan | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialReservation | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MaterialReservation | ProductionOrderId | ProductionOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MealCost | OrderId | Order | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MealCost | OrderLineId | OrderLine | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MealWasteRecord | ProductionOrderId | ProductionOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MealWasteRecord | ShiftId | Shift | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MedicineWarehouse | SupplierId | Supplier | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MedicineWarehouse | UnitMeasureId | UnitMeasure | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Menu | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MenuAssignment | ContractId | Contract | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MenuAssignment | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MenuAssignment | MenuId | Menu | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MenuChangeRequest | MenuId | Menu | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MenuChangeRequest | OrderId | Order | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MenuChangeRequest | ShiftId | Shift | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| MenuLine | MenuId | Menu | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Order | ContractId | Contract | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Order | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| OrderLine | ContractMealId | ContractMeal | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| OrderLine | MenuId | Menu | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| OrderLine | OrderId | Order | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| OrderLine | ShiftId | Shift | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| OrderReconciliation | OrderId | Order | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| OrganizationUnit | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| OutboundMessage | IntegrationChannelId | IntegrationChannel | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PackagingLedger | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PackagingLedger | DeliveryRouteId | DeliveryRoute | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PackagingLedger | OrderId | Order | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PackingOrder | OrderId | Order | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PackingResult | PackingOrderId | PackingOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PackingResult | ProductionOutputId | ProductionOutput | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Patient | AdministrativeDistrictId | AdministrativeDistrict | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Patient | AdministrativeProvinceId | AdministrativeProvince | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Patient | AdministrativeWardId | AdministrativeWard | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PatientDetail | MedicalExaminationId | MedicalExamination | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PatientDetail | PatientId | Patient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PatientDetail | PersonId | Person | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Position | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PricePolicie | ContractId | Contract | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PricePolicie | CustomerId | Customer | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionBatche | KitchenWorkstationId | KitchenWorkstation | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionBatche | ProductionOrderId | ProductionOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionBatche | RecipeId | Recipe | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionLine | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionLine | OrganizationUnitId | OrganizationUnit | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionOrder | ProductionLineId | ProductionLine | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionOrder | ProductionPlanId | ProductionPlan | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionOrder | RecipeId | Recipe | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionOrder | ShiftId | Shift | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionPlan | OrganizationUnitId | OrganizationUnit | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionPlan | ShiftId | Shift | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionPlanLine | OrderLineId | OrderLine | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionPlanLine | ProductionPlanId | ProductionPlan | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionPlanLine | ShiftId | Shift | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ProofOfDeliverie | DeliveryOrderId | DeliveryOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PurchaseOrder | PurchaseRequestId | PurchaseRequest | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PurchaseOrder | SupplierId | Supplier | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PurchaseOrderLine | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PurchaseOrderLine | PurchaseOrderId | PurchaseOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PurchaseRequestLine | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| PurchaseRequestLine | PurchaseRequestId | PurchaseRequest | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| QCInspection | GoodsReceiptId | GoodsReceipt | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| QCInspectionLine | GoodsReceiptLineId | GoodsReceiptLine | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| QCInspectionLine | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| QCInspectionLine | QCInspectionId | QCInspection | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| RecipeLine | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| RecipeLine | RecipeId | Recipe | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ReturnInspection | ContainerReturnId | ContainerReturn | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Shift | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ShiftAssignment | EmployeeId | Employee | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ShiftAssignment | ShiftId | Shift | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ShiftSwapRequest | ApprovalRequestId | ApprovalRequest | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| StandardCost | RecipeId | Recipe | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| StockCount | WarehouseId | Warehouse | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| StockCountLine | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| StockCountLine | MaterialLotId | MaterialLot | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| StockCountLine | StockCountId | StockCount | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| StorageLocation | StorageZoneId | StorageZone | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| StorageLocation | WarehouseId | Warehouse | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| StorageZone | WarehouseId | Warehouse | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Supplier | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| SupplierIssue | GoodsReceiptId | GoodsReceipt | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| SupplierIssue | PurchaseOrderId | PurchaseOrder | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| SupplierIssue | SupplierId | Supplier | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| SupplierMaterial | IngredientId | Ingredient | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| SupplierMaterial | SupplierId | Supplier | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| TemperatureLog | MaterialLotId | MaterialLot | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| TemperatureLog | StorageLocationId | StorageLocation | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| TemperatureLog | StorageZoneId | StorageZone | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| User | PersonId | Person | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| UserClaim | UserGroupId | UserGroup | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| UserClaim | UserId | User | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| UserExternalIdentity | EmployeeId | Employee | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| UserExternalIdentity | IntegrationChannelId | IntegrationChannel | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| UserExternalIdentity | UserId | User | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| UserRole | ObjectMenuId | ObjectMenu | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| UserRole | UserGroupId | UserGroup | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Warehouse | CompanyId | Company | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| Warehouse | OrganizationUnitId | OrganizationUnit | Id | Dictionary 20261113 (sheet 04) | ĐỀ XUẤT – chờ BA xác nhận |
| ActualCost | ProductionBatchId | ProductionBatche | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| ActualCosts_Legacy_20261113 | ProductionBatchId | ProductionBatche | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| CleaningChecklistResult | CleaningBatchId | CleaningBatche | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| ContractMeal | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| Dishe | DefaultUnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| Dishe | DishCategoryId | DishCategorie | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| FoodSafetyChecklistResult | ProductionBatchId | ProductionBatche | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| GoodsReceiptLine | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| Ingredient | BaseUnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| Ingredient | IngredientCategoryId | IngredientCategorie | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| InventoryBalance | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| InventoryTransaction | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| MaterialIssueLine | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| MaterialRequirement | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| MaterialReservation | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| MealCost | DishId | Dishe | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| MealWasteRecord | DishId | Dishe | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| MealWasteRecord | ProductionBatchId | ProductionBatche | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| MenuLine | DishId | Dishe | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| OrderReconciliation | ProofOfDeliveryId | ProofOfDeliverie | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| PackingOrder | ProductionBatchId | ProductionBatche | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionBatche | DishId | Dishe | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionOrder | DishId | Dishe | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionOutput | DishId | Dishe | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionOutput | ProductionBatchId | ProductionBatche | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| ProductionOutput | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| ProductionPlanLine | DishId | Dishe | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| PurchaseOrderLine | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| PurchaseRequestLine | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| Recipe | DishId | Dishe | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| Recipe | YieldUnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| RecipeLine | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| StandardCost | DishId | Dishe | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| StockCountLine | UnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| TemperatureLog | ProductionBatchId | ProductionBatche | Id | Khớp tên sau khi xét tên bảng legacy số nhiều | ĐỀ XUẤT – chờ BA xác nhận |
| UomConversion | FromUnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| UomConversion | ToUnitId | UnitMeasure | Id | PO xác nhận 14/11/2026: UnitId → UnitMeasure | PO XÁC NHẬN |
| Customer | BranchId | OrganizationUnit | Id | PO xác nhận 14/11/2026: OrganizationUnit là nguồn Chi nhánh/Phòng ban | PO XÁC NHẬN |
| Customer | DepartmentId | OrganizationUnit | Id | PO xác nhận 14/11/2026: OrganizationUnit là nguồn Chi nhánh/Phòng ban | PO XÁC NHẬN |
| KpiDailySnapshot | BranchId | OrganizationUnit | Id | PO xác nhận 14/11/2026: OrganizationUnit là nguồn Chi nhánh/Phòng ban | PO XÁC NHẬN |
| KpiMonthlySnapshot | BranchId | OrganizationUnit | Id | PO xác nhận 14/11/2026: OrganizationUnit là nguồn Chi nhánh/Phòng ban | PO XÁC NHẬN |

## 80 cột tham chiếu chưa xác định (không tự suy đoán – ghi GAP nếu cần dùng)
| Bảng | Cột | Lý do | Đề xuất |
|---|---|---|---|
| AnalyticsRefreshLog | RunId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| ApprovalItem | ItemId | Không đủ căn cứ xác định bảng đích | Đa hình theo ItemType |
| ApprovalRequest | BusinessId | Không đủ căn cứ xác định bảng đích | Đa hình theo BusinessType |
| ApprovalRequest | CorrelationId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| ApprovalStep | ApproverUserId | Không đủ căn cứ xác định bảng đích | User |
| AttendanceEvent | ExternalEventId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| CleaningBatche | ResponsibleEmployeeId | Không đủ căn cứ xác định bảng đích | Employee |
| CleaningChecklistResult | ChecklistId | Không đủ căn cứ xác định bảng đích | FoodSafetyChecklist |
| CleaningChecklistResult | ChecklistItemId | Không đủ căn cứ xác định bảng đích | FoodSafetyChecklistItem |
| CleaningChecklistResult | WorkstationId | Không đủ căn cứ xác định bảng đích | KitchenWorkstation |
| ComplaintHistory | ActorId | Không đủ căn cứ xác định bảng đích | Employee / User |
| ComplaintHistory | ComplaintId | Không đủ căn cứ xác định bảng đích | CustomerComplaint |
| CoreDataMergeConflict | RecordId | Không đủ căn cứ xác định bảng đích | Đa hình theo EntityName |
| Customer | AreaId | Không đủ căn cứ xác định bảng đích |  |
| Customer | RegionId | Không đủ căn cứ xác định bảng đích |  |
| Customer | UserAssignId | Không đủ căn cứ xác định bảng đích | User / Employee |
| DeliveryEvent | ExternalEventId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| DeliveryOrder | DeliveryLocationId | Không đủ căn cứ xác định bảng đích | CustomerDeliveryLocation |
| DeliveryRoute | DriverId | Không đủ căn cứ xác định bảng đích | Employee |
| DeliveryStop | DeliveryLocationId | Không đủ căn cứ xác định bảng đích | CustomerDeliveryLocation |
| DishCategorie | ParentId | Không đủ căn cứ xác định bảng đích | (chính bảng đó – cha/con) |
| Dishe | CategoryId | Không đủ căn cứ xác định bảng đích | IngredientCategorie / DishCategorie |
| FoodSafetyChecklistItem | ChecklistId | Không đủ căn cứ xác định bảng đích | FoodSafetyChecklist |
| FoodSafetyChecklistResult | ChecklistId | Không đủ căn cứ xác định bảng đích | FoodSafetyChecklist |
| FoodSafetyChecklistResult | ChecklistItemId | Không đủ căn cứ xác định bảng đích | FoodSafetyChecklistItem |
| FoodSafetyChecklistResult | WorkstationId | Không đủ căn cứ xác định bảng đích | KitchenWorkstation |
| Forecast | DeliveryLocationId | Không đủ căn cứ xác định bảng đích | CustomerDeliveryLocation |
| GoodsReceiptLine | LotId | Không đủ căn cứ xác định bảng đích | MaterialLot |
| GoodsReceiptLine | UomId | Cột UomId: deprecated (đã có cột UnitId tương ứng) |  |
| InboundMessage | CorrelationId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| InboundMessage | ExternalConversationId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| InboundMessage | ExternalMessageId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| InboundMessage | ExternalUserId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| Ingredient | BaseUomId | Cột UomId: deprecated (đã có cột UnitId tương ứng) |  |
| Ingredient | CategoryId | Không đủ căn cứ xác định bảng đích | IngredientCategorie / DishCategorie |
| Ingredient | IssueUomId | Cột UomId: không có cột UnitId tương ứng – cần BA/DEV quyết định | UnitMeasure |
| Ingredient | PurchaseUomId | Cột UomId: không có cột UnitId tương ứng – cần BA/DEV quyết định | UnitMeasure |
| IngredientCategorie | ParentId | Không đủ căn cứ xác định bảng đích | (chính bảng đó – cha/con) |
| IntegrationChannel | OwnerUserId | Không đủ căn cứ xác định bảng đích | User |
| IntegrationLog | CorrelationId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| InventoryBalance | LotId | Không đủ căn cứ xác định bảng đích | MaterialLot |
| InventoryTransaction | LotId | Không đủ căn cứ xác định bảng đích | MaterialLot |
| InventoryTransaction | ReferenceId | Không đủ căn cứ xác định bảng đích | Đa hình theo ReferenceType |
| InventoryTransaction | UomId | Cột UomId: deprecated (đã có cột UnitId tương ứng) |  |
| MaterialIssueLine | LotId | Không đủ căn cứ xác định bảng đích | MaterialLot |
| MaterialIssueLine | UomId | Cột UomId: deprecated (đã có cột UnitId tương ứng) |  |
| MenuAssignment | DeliveryLocationId | Không đủ căn cứ xác định bảng đích | CustomerDeliveryLocation |
| MenuChangeRequest | OriginalDishId | Không đủ căn cứ xác định bảng đích | Dishe |
| MenuChangeRequest | RequestedDishId | Không đủ căn cứ xác định bảng đích | Dishe |
| ObjectMenu | ParentId | Không đủ căn cứ xác định bảng đích | (chính bảng đó – cha/con) |
| Order | DeliveryLocationId | Không đủ căn cứ xác định bảng đích | CustomerDeliveryLocation |
| OrganizationUnit | ManagerId | Không đủ căn cứ xác định bảng đích | Employee |
| OrganizationUnit | ParentId | Không đủ căn cứ xác định bảng đích | (chính bảng đó – cha/con) |
| OutboundMessage | CorrelationId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| OutboundMessage | ExternalConversationId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| OutboundMessage | ProviderMessageId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| OutboundMessage | RecipientExternalId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |
| PackagingLedger | DeliveryLocationId | Không đủ căn cứ xác định bảng đích | CustomerDeliveryLocation |
| ProductionOrder | ResponsibleEmployeeId | Không đủ căn cứ xác định bảng đích | Employee |
| PurchaseOrderLine | UomId | Cột UomId: deprecated (đã có cột UnitId tương ứng) |  |
| PurchaseRequest | RequestedByEmployeeId | Không đủ căn cứ xác định bảng đích | Employee |
| PurchaseRequest | RequesterId | Không đủ căn cứ xác định bảng đích | Employee / User |
| PurchaseRequestLine | UomId | Cột UomId: deprecated (đã có cột UnitId tương ứng) |  |
| QCInspection | InspectorEmployeeId | Không đủ căn cứ xác định bảng đích | Employee |
| QCInspection | InspectorId | Không đủ căn cứ xác định bảng đích | Employee / User |
| RecipeLine | UomId | Cột UomId: deprecated (đã có cột UnitId tương ứng) |  |
| ReturnInspection | InspectorEmployeeId | Không đủ căn cứ xác định bảng đích | Employee |
| ShiftSwapRequest | CurrentShiftAssignmentId | Không đủ căn cứ xác định bảng đích | ShiftAssignment |
| ShiftSwapRequest | ProposedShiftAssignmentId | Không đủ căn cứ xác định bảng đích | ShiftAssignment |
| ShiftSwapRequest | RequesterEmployeeId | Không đủ căn cứ xác định bảng đích | Employee |
| ShiftSwapRequest | TargetEmployeeId | Không đủ căn cứ xác định bảng đích | Employee |
| StockCount | ResponsibleEmployeeId | Không đủ căn cứ xác định bảng đích | Employee |
| StockCountLine | LotId | Không đủ căn cứ xác định bảng đích | MaterialLot |
| StorageLocation | ZoneId | Không đủ căn cứ xác định bảng đích | StorageZone |
| SupplierIssue | OwnerId | Không đủ căn cứ xác định bảng đích | Employee / User |
| SupplierMaterial | PurchaseUomId | Cột UomId: không có cột UnitId tương ứng – cần BA/DEV quyết định | UnitMeasure |
| TestDemo | FieldDropdownId | Không đủ căn cứ xác định bảng đích |  |
| UomConversion | FromUomId | Cột UomId: deprecated (đã có cột UnitId tương ứng) |  |
| UomConversion | ToUomId | Cột UomId: deprecated (đã có cột UnitId tương ứng) |  |
| UserExternalIdentity | ExternalUserId | Mã định danh hệ thống ngoài/kỹ thuật – không phải tham chiếu bảng nội bộ | Không áp dụng |