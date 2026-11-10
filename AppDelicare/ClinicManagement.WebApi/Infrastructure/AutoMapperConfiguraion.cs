using AutoMapper;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;

namespace AppDelicare.WebApi.Infrastructure
{
	public class AutoMapperConfiguraion : Profile
    {
		public AutoMapperConfiguraion()
        {
            CreateMap<ObjectMenuRequest, ObjectMenu>();
            CreateMap<UserGroupRequest, UserGroup>();
            CreateMap<UserRequest, User>();
            CreateMap<UserRoleRequest, UserRole>();
            CreateMap<LocationTrackingRequest, LocationTracking>();
            CreateMap<AuditChangeRequest, AuditChange>();
            CreateMap<UserClaimRequest, UserClaim>();
			//Custom
			CreateMap<PatientDetailRequest, PatientDetail>();
			CreateMap<MedicalExaminationRequest, MedicalExamination>();
			CreateMap<MedicineWarehouseRequest, MedicineWarehouse>();
			CreateMap<SupplierRequest, Supplier>();
			CreateMap<UnitMeasureRequest, UnitMeasure>();
			CreateMap<AdministrativeDistrictRequest, AdministrativeDistrict>();
			CreateMap<AdministrativeProvinceRequest, AdministrativeProvince>();
			CreateMap<AdministrativeRegionRequest, AdministrativeRegion>();
			CreateMap<AdministrativeUnitRequest, AdministrativeUnit>();
			CreateMap<AdministrativeWardRequest, AdministrativeWard>();
			CreateMap<PersonRequest, Person>();
			CreateMap<TestDemoRequest, TestDemo>();
			CreateMap<PatientRequest, Patient>();
			//App
            CreateMap<AppConfigRequest, AppConfig>();
            CreateMap<CustomerRequest, Customer>();
            CreateMap<RegionRequest, Region>();
            CreateMap<AreaRequest, Area>();
            CreateMap<BranchRequest, Branch>();
            CreateMap<DepartmentRequest, Department>();
            CreateMap<EmployeeRequest, Employee>();
            CreateMap<ProductRequest, Products>();
            CreateMap<LeadRequest, Leads>();
            CreateMap<OpportunityRequest, Opportunity>();
            //AppKitchen
            CreateMap<CompanyRequest, Company>();
            CreateMap<OrganizationUnitRequest, OrganizationUnit>();
            CreateMap<PositionRequest, Position>();
            CreateMap<ShiftRequest, Shift>();
            CreateMap<EmployeeRequest, Employee>();
            CreateMap<CustomerRequest, Customer>();
            CreateMap<CustomerContactRequest, CustomerContact>();
            CreateMap<CustomerDeliveryLocationRequest, CustomerDeliveryLocation>();
            CreateMap<SupplierRequest, Supplier>();
            CreateMap<IngredientCategoryRequest, IngredientCategory>();
            CreateMap<UnitOfMeasureRequest, UnitOfMeasure>();
            CreateMap<UomConversionRequest, UomConversion>();
            CreateMap<IngredientRequest, Ingredient>();
            CreateMap<SupplierMaterialRequest, SupplierMaterial>();
            CreateMap<MaterialLotRequest, MaterialLot>();
            CreateMap<DishCategoryRequest, DishCategory>();
            CreateMap<DishRequest, Dish>();
            CreateMap<RecipeRequest, Recipe>();
            CreateMap<RecipeLineRequest, RecipeLine>();
            CreateMap<MenuRequest, Menu>();
            CreateMap<MenuLineRequest, MenuLine>();
            CreateMap<MenuAssignmentRequest, MenuAssignment>();
            CreateMap<ContractRequest, Contract>();
            CreateMap<ContractMealRequest, ContractMeals>();
            CreateMap<PricePolicyRequest, PricePolicy>();
            CreateMap<ForecastRequest, Forecast>();
            CreateMap<OrderRequest, Order>();
            CreateMap<OrderLineRequest, OrderLine>();
            CreateMap<MaterialRequirementRequest, MaterialRequirement>();
            CreateMap<PurchaseRequestRequest, PurchaseRequest>();
            CreateMap<PurchaseRequestLineRequest, PurchaseRequestLine>();
            CreateMap<PurchaseOrderRequest, PurchaseOrder>();
            CreateMap<PurchaseOrderLineRequest, PurchaseOrderLine>();
            CreateMap<GoodsReceiptRequest, GoodsReceipt>();
            CreateMap<GoodsReceiptLineRequest, GoodsReceiptLine>();
            CreateMap<QcInspectionRequest, QcInspection>();
            CreateMap<QcInspectionLineRequest, QcInspectionLine>();
            CreateMap<WarehouseRequest, Warehouse>();
            CreateMap<StorageZoneRequest, StorageZone>();
            CreateMap<StorageLocationRequest, StorageLocation>();
            CreateMap<InventoryPolicyRequest, InventoryPolicy>();
            CreateMap<InventoryTransactionRequest, InventoryTransaction>();
            CreateMap<InventoryBalanceRequest, InventoryBalance>();
            CreateMap<StockCountRequest, StockCount>();
            CreateMap<StockCountLineRequest, StockCountLine>();
            CreateMap<ProductionPlanRequest, ProductionPlan>();
            CreateMap<ProductionPlanLineRequest, ProductionPlanLine>();
            CreateMap<ProductionOrderRequest, ProductionOrder>();
            CreateMap<MaterialReservationRequest, MaterialReservation>();
            CreateMap<MaterialIssueRequest, MaterialIssue>();
            CreateMap<MaterialIssueLineRequest, MaterialIssueLine>();
            CreateMap<ProductionBatchRequest, ProductionBatch>();
            CreateMap<ProductionOutputRequest, ProductionOutput>();
            CreateMap<PackingOrderRequest, PackingOrder>();
            CreateMap<PackingResultRequest, PackingResult>();
            CreateMap<DeliveryRouteRequest, DeliveryRoute>();
            CreateMap<DeliveryOrderRequest, DeliveryOrder>();
            CreateMap<DeliveryLoadingRequest, DeliveryLoading>();
            CreateMap<DeliveryExecutionRequest, DeliveryExecution>();
            CreateMap<ProofOfDeliveryRequest, ProofOfDelivery>();
            CreateMap<ContainerReturnRequest, ContainerReturn>();
            CreateMap<ReturnInspectionRequest, ReturnInspection>();
            CreateMap<CleaningBatchRequest, CleaningBatch>();
            CreateMap<OrderReconciliationRequest, OrderReconciliation>();
            CreateMap<MaterialConsumptionVarianceRequest, MaterialConsumptionVariance>();
            CreateMap<StandardCostRequest, StandardCost>();
            CreateMap<ActualCostRequest, ActualCost>();
            CreateMap<MealCostRequest, MealCost>();
        }
    }
}
