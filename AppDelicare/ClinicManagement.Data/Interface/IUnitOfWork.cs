using AppDelicare.Data.Connection;
using System.Data;

namespace AppDelicare.Data.Interface
{
	public interface IUnitOfWork : IDisposable
    {
		public IDbConnection Connection { get; }
		public DatabaseContext DbContext { get; }
        public IDbTransaction Transaction { get; }
        public string ConnectionString { get; }
        public Task<IDbTransaction> OpenTransaction();
        public Task<IDbTransaction> OpenTransaction(IsolationLevel level);
        public void CommitTransaction(bool disposeTrans = true);
        public void RollbackTransaction(bool disposeTrans = true);
        public void BeginTransaction();
        public int SaveChanges();
        public void Commit();
        public void Rollback();
        public Task<int> SaveChangesAsync();
        public IUserRepository UserRepository { get; }
        public IDocumentFileRepository DocumentFileRepository { get; }
        public ILocationTrackingRepository LocationTrackingRepository { get; }
        public IObjectMenuRepository ObjectMenuRepository { get; }
        public IUserGroupRepository UserGroupRepository { get; }
        public IUserRoleRepository UserRoleRepository { get; }
        public IAuditChangeRepository AuditChangeRepository { get; }
        public IUserClaimRepository UserClaimRepository { get; }
		//Custom
		public IPatientDetailRepository PatientDetailRepository { get; }
		public IMedicalExaminationRepository MedicalExaminationRepository { get; }
		public IMedicineWarehouseRepository MedicineWarehouseRepository { get; }
		public ISupplierRepository SupplierRepository { get; }
		public IUnitMeasureRepository UnitMeasureRepository { get; }
		public IAdministrativeDistrictRepository AdministrativeDistrictRepository { get; }
		public IAdministrativeProvinceRepository AdministrativeProvinceRepository { get; }
		public IAdministrativeRegionRepository AdministrativeRegionRepository { get; }
		public IAdministrativeUnitRepository AdministrativeUnitRepository { get; }
		public IAdministrativeWardRepository AdministrativeWardRepository { get; }
		public IPersonRepository PersonRepository { get; }
		public ITestDemoRepository TestDemoRepository { get; }
		public IPatientRepository PatientRepository { get; }
        public IAppConfigRepository AppConfigRepository { get; }
        public ICustomerRepository CustomerRepository { get; }
        public IRegionRepository RegionRepository { get; }
        public IAreaRepository AreaRepository { get; }
        public IBranchRepository BranchRepository { get; }
        public IDepartmentRepository DepartmentRepository { get; }
        public IEmployeeRepository EmployeeRepository { get; }
        public IProductRepository ProductRepository { get; }
        public ILeadRepository LeadRepository { get; }
        public IOpportunityRepository OpportunityRepository { get; }

        public ICompanyRepository CompanyRepository { get; }
        public IOrganizationUnitRepository OrganizationUnitRepository { get; }
        public IPositionRepository PositionRepository { get; }
        public IShiftRepository ShiftRepository { get; }
        public ICustomerContactRepository CustomerContactRepository { get; }
        public ICustomerDeliveryLocationRepository CustomerDeliveryLocationRepository { get; }
        public IIngredientCategoryRepository IngredientCategoryRepository { get; }
        public IUnitOfMeasureRepository UnitOfMeasureRepository { get; }
        public IUomConversionRepository UomConversionRepository { get; }
        public IIngredientRepository IngredientRepository { get; }
        public ISupplierMaterialRepository SupplierMaterialRepository { get; }
        public IMaterialLotRepository MaterialLotRepository { get; }
        public IDishCategoryRepository DishCategoryRepository { get; }
        public IDishRepository DishRepository { get; }
        public IRecipeRepository RecipeRepository { get; }
        public IRecipeLineRepository RecipeLineRepository { get; }
        public IMenuRepository MenuRepository { get; }
        public IMenuLineRepository MenuLineRepository { get; }
        public IMenuAssignmentRepository MenuAssignmentRepository { get; }
        public IContractRepository ContractRepository { get; }
        public IContractMealRepository ContractMealRepository { get; }
        public IPricePolicyRepository PricePolicyRepository { get; }
        public IForecastRepository ForecastRepository { get; }
        public IOrderRepository OrderRepository { get; }
        public IOrderLineRepository OrderLineRepository { get; }
        public IMaterialRequirementRepository MaterialRequirementRepository { get; }
        public IPurchaseRequestRepository PurchaseRequestRepository { get; }
        public IPurchaseRequestLineRepository PurchaseRequestLineRepository { get; }
        public IPurchaseOrderRepository PurchaseOrderRepository { get; }
        public IPurchaseOrderLineRepository PurchaseOrderLineRepository { get; }
        public IGoodsReceiptRepository GoodsReceiptRepository { get; }
        public IGoodsReceiptLineRepository GoodsReceiptLineRepository { get; }
        public IQcInspectionRepository QcInspectionRepository { get; }
        public IQcInspectionLineRepository QcInspectionLineRepository { get; }
        public IWarehouseRepository WarehouseRepository { get; }
        public IStorageZoneRepository StorageZoneRepository { get; }
        public IStorageLocationRepository StorageLocationRepository { get; }
        public IInventoryPolicyRepository InventoryPolicyRepository { get; }
        public IInventoryTransactionRepository InventoryTransactionRepository { get; }
        public IInventoryBalanceRepository InventoryBalanceRepository { get; }
        public IStockCountRepository StockCountRepository { get; }
        public IStockCountLineRepository StockCountLineRepository { get; }
        public IProductionPlanRepository ProductionPlanRepository { get; }
        public IProductionPlanLineRepository ProductionPlanLineRepository { get; }
        public IProductionOrderRepository ProductionOrderRepository { get; }
        public IMaterialReservationRepository MaterialReservationRepository { get; }
        public IMaterialIssueRepository MaterialIssueRepository { get; }
        public IMaterialIssueLineRepository MaterialIssueLineRepository { get; }
        public IProductionBatchRepository ProductionBatchRepository { get; }
        public IProductionOutputRepository ProductionOutputRepository { get; }
        public IPackingOrderRepository PackingOrderRepository { get; }
        public IPackingResultRepository PackingResultRepository { get; }
        public IDeliveryRouteRepository DeliveryRouteRepository { get; }
        public IDeliveryOrderRepository DeliveryOrderRepository { get; }
        public IDeliveryLoadingRepository DeliveryLoadingRepository { get; }
        public IDeliveryExecutionRepository DeliveryExecutionRepository { get; }
        public IProofOfDeliveryRepository ProofOfDeliveryRepository { get; }
        public IContainerReturnRepository ContainerReturnRepository { get; }
        public IReturnInspectionRepository ReturnInspectionRepository { get; }
        public ICleaningBatchRepository CleaningBatchRepository { get; }
        public IOrderReconciliationRepository OrderReconciliationRepository { get; }
        public IMaterialConsumptionVarianceRepository MaterialConsumptionVarianceRepository { get; }
        public IStandardCostRepository StandardCostRepository { get; }
        public IActualCostRepository ActualCostRepository { get; }
        public IMealCostRepository MealCostRepository { get; }
    }
}
