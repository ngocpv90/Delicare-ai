# Cặp cột trùng nghĩa (PO đã chốt: DÙNG CỘT MỚI, cột cũ chỉ đọc / ghi song song để tương thích)

## DeleteddBy (lỗi chính tả) → DeletedBy
68 bảng có cả hai cột: ActualCost, ActualCosts_Legacy_20261113, CleaningBatche, Company, ContainerReturn, Contract, ContractMeal, CustomerContact, CustomerDeliveryLocation, DeliveryExecution, DeliveryLoading, DeliveryOrder, DeliveryRoute, DishCategorie, Dishe, Employee, Forecast, GoodsReceipt, GoodsReceiptLine, Ingredient, IngredientCategorie, InventoryBalance, InventoryPolicie, InventoryTransaction, MaterialConsumptionVariance, MaterialIssue, MaterialIssueLine, MaterialLot, MaterialRequirement, MaterialReservation, MealCost, Menu, MenuAssignment, MenuLine, Order, OrderLine, OrderReconciliation, OrganizationUnit, PackingOrder, PackingResult, Position, PricePolicie, ProductionBatche, ProductionOrder, ProductionOutput, ProductionPlan, ProductionPlanLine, ProofOfDeliverie, PurchaseOrder, PurchaseOrderLine, PurchaseRequest, PurchaseRequestLine, QCInspection, QCInspectionLine, Recipe, RecipeLine, ReturnInspection, Shift, StandardCost, StockCount, StockCountLine, StorageLocation, StorageZone, SupplierMaterial, UnitMeasure, UnitsOfMeasure_Legacy_20261113, UomConversion, Warehouse

Cách xử lý: BaseEntity giữ `DeleteddBy` (để code cũ chạy) và thêm `DeletedBy`. Khi xóa mềm thì ghi CẢ HAI ở bảng có cả hai cột. Bảng chỉ có một cột thì map cột đó (cấu hình EF theo từng entity, `Ignore` thuộc tính không có cột). Sinh mã phải đọc DDL để biết bảng nào có cột nào.

## Các cặp khác (ghi cột mới; nếu cột cũ NOT NULL thì ghi cả hai cùng giá trị)
| Bảng | Cột cũ (deprecated) | Dùng cột |
|---|---|---|
| CleaningBatche | StartTime | StartDate |
| CleaningBatche | EndTime | EndDate |
| CleaningBatche | CleaningNo | CleaningBatchNumber |
| Contract | ContractNo | ContractNumber |
| DeliveryOrder | DeliveryNo | DeliveryNumber |
| DeliveryOrder | DeliveryLocationId | CustomerDeliveryLocationId |
| Dishe | CategoryId | DishCategoryId |
| Employee | EmployeeCode | Code |
| Forecast | Quantity | ForecastQuantity |
| GoodsReceipt | ReceiptNo | ReceiptNumber |
| GoodsReceiptLine | UomId | UnitId |
| Ingredient | BaseUomId | BaseUnitId |
| Ingredient | CategoryId | IngredientCategoryId |
| InventoryBalance | ReservedQty | ReservedQuantity |
| InventoryBalance | OnHandQty | OnHandQuantity |
| InventoryBalance | AvailableQty | AvailableQuantity |
| InventoryBalance | LotId | MaterialLotId |
| InventoryTransaction | TransactionNo | TransactionNumber |
| InventoryTransaction | UomId | UnitId |
| InventoryTransaction | LotId | MaterialLotId |
| MaterialConsumptionVariance | ActualQty | ActualQuantity |
| MaterialConsumptionVariance | VarianceQty | VarianceQuantity |
| MaterialIssue | IssueNo | IssueNumber |
| MaterialIssueLine | UomId | UnitId |
| MaterialIssueLine | LotId | MaterialLotId |
| MaterialLot | LotNo | LotNumber |
| MaterialLot | QualityStatus | QcStatus |
| MaterialRequirement | PurchaseQty | PurchaseQuantity |
| MaterialRequirement | RequiredQty | RequiredQuantity |
| MaterialRequirement | ExpectedIncomingQty | ExpectedIncomingQuantity |
| MaterialRequirement | AvailableQty | AvailableQuantity |
| MaterialReservation | ReservedQty | ReservedQuantity |
| Order | OrderNo | OrderNumber |
| OrderLine | Amount | LineAmount |
| OrderReconciliation | AcceptedQty | AcceptedQuantity |
| OrderReconciliation | DeliveredQty | DeliveredQuantity |
| OrderReconciliation | OrderedQty | OrderedQuantity |
| PackingOrder | PackingNo | PackingOrderNumber |
| PackingResult | PackedQty | PackedQuantity |
| PackingResult | RejectedQty | RejectedQuantity |
| ProductionBatche | StartTime | StartDate |
| ProductionBatche | EndTime | EndDate |
| ProductionBatche | PlannedQty | PlannedQuantity |
| ProductionBatche | ActualQty | ActualQuantity |
| ProductionBatche | BatchNo | BatchNumber |
| ProductionBatche | QualityStatus | QcStatus |
| ProductionOrder | ProductionOrderNo | ProductionOrderNumber |
| ProductionOutput | AcceptedQty | AcceptedQuantity |
| ProductionOutput | RejectedQty | RejectedQuantity |
| ProductionOutput | OutputQty | ProducedQuantity |
| ProductionPlan | PlanNo | PlanNumber |
| ProductionPlanLine | PlannedQty | PlannedQuantity |
| ProofOfDeliverie | AcceptedQty | AcceptedQuantity |
| ProofOfDeliverie | RejectedQty | RejectedQuantity |
| ProofOfDeliverie | DeliveredQty | DeliveredQuantity |
| PurchaseOrder | PONo | PurchaseOrderNumber |
| PurchaseOrder | ExpectedDate | ExpectedDeliveryDate |
| PurchaseOrderLine | UomId | UnitId |
| PurchaseOrderLine | Amount | LineAmount |
| PurchaseRequest | RequestNo | RequestNumber |
| PurchaseRequest | RequesterId | RequestedByEmployeeId |
| PurchaseRequestLine | UomId | UnitId |
| QCInspection | InspectorId | InspectorEmployeeId |
| RecipeLine | UomId | UnitId |
| StockCount | CountNo | CountNumber |
| StockCountLine | SystemQty | SystemQuantity |
| StockCountLine | VarianceQty | VarianceQuantity |
| StockCountLine | PhysicalQty | PhysicalQuantity |
| StockCountLine | LotId | MaterialLotId |
| StorageLocation | ZoneId | StorageZoneId |
| SupplierMaterial | MinimumOrderQty | MinimumOrderQuantity |
| UomConversion | FromUomId | FromUnitId |
| UomConversion | ToUomId | ToUnitId |