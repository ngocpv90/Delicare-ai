using Dapper;
using Microsoft.Extensions.Configuration;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Common;
using AppDelicare.Model.Helper;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace AppDelicare.Data.Repositories
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>")]
	public class UnitOfWork : IUnitOfWork
    {
        protected readonly IConfiguration _config;
        protected string _cnStr = ConfigurationCommon.Instance.GetConfig<string>(ConstantHelper.ConnectionStrings, ConstantHelper.MainConnectionString);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public UnitOfWork(IConfiguration config, DatabaseContext context)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            _config = config;
            _cnStr = _config.GetConnectionString(ConnectionHelper.DatabaseName.MainConnectionString.ToString()) ?? string.Empty;
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            _cn = new SqlConnection(_cnStr);
            _context = context;
        }

        public string ConnectionString
        {
            get => _cnStr;
        }

        protected IDbConnection _cn;
        public IDbConnection Connection
        {
            get => _cn;
        }

        protected DatabaseContext _context;
        public DatabaseContext DbContext
        {
            get => _context;
        }

        protected IDbTransaction? _trans = null;
        public IDbTransaction Transaction
        {
#pragma warning disable CS8603 // Possible null reference return.
            get => _trans;
#pragma warning restore CS8603 // Possible null reference return.
        }

        public async Task<IDbTransaction> OpenTransaction()
        {
            if (_trans != null)
            {
                throw new Exception("A transaction is already open, you need to use a new DbContext for parallel job.");
            }
            if (_cn.State == ConnectionState.Closed)
            {
                if (_cn is not DbConnection)
                {
                    throw new Exception("Connection object does not support OpenAsync.");
                }
                await ((DbConnection)_cn).OpenAsync();
            }
            _trans = _cn.BeginTransaction();
            return _trans;
        }

        public async Task<IDbTransaction> OpenTransaction(IsolationLevel level)
        {
            if (_trans != null)
            {
                throw new Exception("A transaction is already open, you need to use a new DbContext for parallel job.");
            }
            if (_cn.State == ConnectionState.Closed)
            {
                if (_cn is not DbConnection)
                {
                    throw new Exception("Connection object does not support OpenAsync.");
                }
                await ((DbConnection)_cn).OpenAsync();
            }
            _trans = _cn.BeginTransaction(level);
            return _trans;
        }

        public void CommitTransaction(bool disposeTrans = true)
        {
            if (_trans == null)
            {
                throw new Exception("DB Transaction is not present.");
            }
            _trans.Commit();
            if (disposeTrans)
            {
                _trans.Dispose();
            }
            if (disposeTrans)
            {
                _trans = null;
            }
        }

        public void RollbackTransaction(bool disposeTrans = true)
        {
            if (_trans == null)
            {
                throw new Exception("DB Transaction is not present.");
            }
            _trans.Rollback();
            if (disposeTrans)
            {
                _trans.Dispose();
            }
            if (disposeTrans)
            {
                _trans = null;
            }
        }

        public void Dispose()
        {
            _context.Dispose();
            _trans?.Dispose();
            _cn?.Close();
            _cn?.Dispose();
        }

        public virtual void BeginTransaction() => _context.Database.BeginTransaction();

        public virtual void Commit() => _context.Database.CommitTransaction();

        public virtual void Rollback() => _context.Database.RollbackTransaction();

        public virtual int SaveChanges() => _context.SaveChanges();

        public virtual Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        private IUserRepository? _userRepository;
        public IUserRepository UserRepository
        {
            get
            {
                return _userRepository ??= new UserRepository(this);
            }
        }

        private IDocumentFileRepository? _documentFileRepository;
        public IDocumentFileRepository DocumentFileRepository
        {
            get
            {
                return _documentFileRepository ??= new DocumentFileRepository(this);
            }
        }

        private IObjectMenuRepository _objectMenuRepository;
        public IObjectMenuRepository ObjectMenuRepository
        {
            get
            {
                return _objectMenuRepository ??= new ObjectMenuRepository(this);
            }
        }

        private IUserGroupRepository _userGroupRepository;
        public IUserGroupRepository UserGroupRepository
        {
            get
            {
                return _userGroupRepository ??= new UserGroupRepository(this);
            }
        }

        private IUserRoleRepository _userRoleRepository;
        public IUserRoleRepository UserRoleRepository
        {
            get
            {
                return _userRoleRepository ??= new UserRoleRepository(this);
            }
        }

        private ILocationTrackingRepository _locationTrackingRepository;
        public ILocationTrackingRepository LocationTrackingRepository
        {
            get
            {
                return _locationTrackingRepository ??= new LocationTrackingRepository(this);
            }
        }

        private IAuditChangeRepository _auditChangeRepository;
        public IAuditChangeRepository AuditChangeRepository
        {
            get
            {
                return _auditChangeRepository ??= new AuditChangeRepository(this);
            }
        }

        private IUserClaimRepository _userClaimRepository;
        public IUserClaimRepository UserClaimRepository
        {
            get
            {
                return _userClaimRepository ??= new UserClaimRepository(this);
            }
        }

		//Custom
	 
        private IPatientDetailRepository _PatientDetailRepository;
        public IPatientDetailRepository PatientDetailRepository
        {
            get
            {
                return _PatientDetailRepository ??= new PatientDetailRepository(this);
            }
        }
	 
        private IMedicalExaminationRepository _MedicalExaminationRepository;
        public IMedicalExaminationRepository MedicalExaminationRepository
        {
            get
            {
                return _MedicalExaminationRepository ??= new MedicalExaminationRepository(this);
            }
        }
	 
        private IMedicineWarehouseRepository _MedicineWarehouseRepository;
        public IMedicineWarehouseRepository MedicineWarehouseRepository
        {
            get
            {
                return _MedicineWarehouseRepository ??= new MedicineWarehouseRepository(this);
            }
        }
	 
        private ISupplierRepository _SupplierRepository;
        public ISupplierRepository SupplierRepository
        {
            get
            {
                return _SupplierRepository ??= new SupplierRepository(this);
            }
        }
	 
        private IUnitMeasureRepository _UnitMeasureRepository;
        public IUnitMeasureRepository UnitMeasureRepository
        {
            get
            {
                return _UnitMeasureRepository ??= new UnitMeasureRepository(this);
            }
        }
	 
        private IAdministrativeDistrictRepository _AdministrativeDistrictRepository;
        public IAdministrativeDistrictRepository AdministrativeDistrictRepository
        {
            get
            {
                return _AdministrativeDistrictRepository ??= new AdministrativeDistrictRepository(this);
            }
        }
	 
        private IAdministrativeProvinceRepository _AdministrativeProvinceRepository;
        public IAdministrativeProvinceRepository AdministrativeProvinceRepository
        {
            get
            {
                return _AdministrativeProvinceRepository ??= new AdministrativeProvinceRepository(this);
            }
        }
	 
        private IAdministrativeRegionRepository _AdministrativeRegionRepository;
        public IAdministrativeRegionRepository AdministrativeRegionRepository
        {
            get
            {
                return _AdministrativeRegionRepository ??= new AdministrativeRegionRepository(this);
            }
        }
	 
        private IAdministrativeUnitRepository _AdministrativeUnitRepository;
        public IAdministrativeUnitRepository AdministrativeUnitRepository
        {
            get
            {
                return _AdministrativeUnitRepository ??= new AdministrativeUnitRepository(this);
            }
        }
	 
        private IAdministrativeWardRepository _AdministrativeWardRepository;
        public IAdministrativeWardRepository AdministrativeWardRepository
        {
            get
            {
                return _AdministrativeWardRepository ??= new AdministrativeWardRepository(this);
            }
        }
	 
        private IPersonRepository _PersonRepository;
        public IPersonRepository PersonRepository
        {
            get
            {
                return _PersonRepository ??= new PersonRepository(this);
            }
        }
	 
        private ITestDemoRepository _TestDemoRepository;
        public ITestDemoRepository TestDemoRepository
        {
            get
            {
                return _TestDemoRepository ??= new TestDemoRepository(this);
            }
        }
	 
        private IPatientRepository _PatientRepository;
        public IPatientRepository PatientRepository
        {
            get
            {
                return _PatientRepository ??= new PatientRepository(this);
            }
        }
        private IAppConfigRepository _AppConfigRepository;
        public IAppConfigRepository AppConfigRepository
        {
            get
            {
                return _AppConfigRepository ??= new AppConfigRepository(this);
            }
        }
        private ICustomerRepository _CustomerRepository;
        public ICustomerRepository CustomerRepository
        {
            get
            {
                return _CustomerRepository ??= new CustomerRepository(this);
            }
        }
        private IRegionRepository _RegionRepository;
        public IRegionRepository RegionRepository
        {
            get
            {
                return _RegionRepository ??= new RegionRepository(this);
            }
        }
        private IAreaRepository _AreaRepository;
        public IAreaRepository AreaRepository
        {
            get
            {
                return _AreaRepository ??= new AreaRepository(this);
            }
        }
        private IBranchRepository _BranchRepository;
        public IBranchRepository BranchRepository
        {
            get
            {
                return _BranchRepository ??= new BranchRepository(this);
            }
        }
        private IDepartmentRepository _DepartmentRepository;
        public IDepartmentRepository DepartmentRepository
        {
            get
            {
                return _DepartmentRepository ??= new DepartmentRepository(this);
            }
        }
        private IEmployeeRepository _EmployeeRepository;
        public IEmployeeRepository EmployeeRepository
        {
            get
            {
                return _EmployeeRepository ??= new EmployeeRepository(this);
            }
        }
        private IProductRepository _ProductRepository;
        public IProductRepository ProductRepository
        {
            get
            {
                return _ProductRepository ??= new ProductRepository(this);
            }
        }
        private ILeadRepository _LeadRepository;
        public ILeadRepository LeadRepository
        {
            get
            {
                return _LeadRepository ??= new LeadRepository(this);
            }
        }
        private IOpportunityRepository _OpportunityRepository;
        public IOpportunityRepository OpportunityRepository
        {
            get
            {
                return _OpportunityRepository ??= new OpportunityRepository(this);
            }
        }
        private ICompanyRepository _CompanyRepository;
        public ICompanyRepository CompanyRepository
        {
            get
            {
                return _CompanyRepository ??= new CompanyRepository(this);
            }
        }

        private IOrganizationUnitRepository _OrganizationUnitRepository;
        public IOrganizationUnitRepository OrganizationUnitRepository
        {
            get
            {
                return _OrganizationUnitRepository ??= new OrganizationUnitRepository(this);
            }
        }

        private IPositionRepository _PositionRepository;
        public IPositionRepository PositionRepository
        {
            get
            {
                return _PositionRepository ??= new PositionRepository(this);
            }
        }

        private IShiftRepository _ShiftRepository;
        public IShiftRepository ShiftRepository
        {
            get
            {
                return _ShiftRepository ??= new ShiftRepository(this);
            }
        }

        private ICustomerContactRepository _CustomerContactRepository;
        public ICustomerContactRepository CustomerContactRepository
        {
            get
            {
                return _CustomerContactRepository ??= new CustomerContactRepository(this);
            }
        }

        private ICustomerDeliveryLocationRepository _CustomerDeliveryLocationRepository;
        public ICustomerDeliveryLocationRepository CustomerDeliveryLocationRepository
        {
            get
            {
                return _CustomerDeliveryLocationRepository ??= new CustomerDeliveryLocationRepository(this);
            }
        }

        private IIngredientCategoryRepository _IngredientCategoryRepository;
        public IIngredientCategoryRepository IngredientCategoryRepository
        {
            get
            {
                return _IngredientCategoryRepository ??= new IngredientCategoryRepository(this);
            }
        }

        private IUnitOfMeasureRepository _UnitOfMeasureRepository;
        public IUnitOfMeasureRepository UnitOfMeasureRepository
        {
            get
            {
                return _UnitOfMeasureRepository ??= new UnitOfMeasureRepository(this);
            }
        }

        private IUomConversionRepository _UomConversionRepository;
        public IUomConversionRepository UomConversionRepository
        {
            get
            {
                return _UomConversionRepository ??= new UomConversionRepository(this);
            }
        }

        private IIngredientRepository _IngredientRepository;
        public IIngredientRepository IngredientRepository
        {
            get
            {
                return _IngredientRepository ??= new IngredientRepository(this);
            }
        }

        private ISupplierMaterialRepository _SupplierMaterialRepository;
        public ISupplierMaterialRepository SupplierMaterialRepository
        {
            get
            {
                return _SupplierMaterialRepository ??= new SupplierMaterialRepository(this);
            }
        }

        private IMaterialLotRepository _MaterialLotRepository;
        public IMaterialLotRepository MaterialLotRepository
        {
            get
            {
                return _MaterialLotRepository ??= new MaterialLotRepository(this);
            }
        }

        private IDishCategoryRepository _DishCategoryRepository;
        public IDishCategoryRepository DishCategoryRepository
        {
            get
            {
                return _DishCategoryRepository ??= new DishCategoryRepository(this);
            }
        }

        private IDishRepository _DishRepository;
        public IDishRepository DishRepository
        {
            get
            {
                return _DishRepository ??= new DishRepository(this);
            }
        }

        private IRecipeRepository _RecipeRepository;
        public IRecipeRepository RecipeRepository
        {
            get
            {
                return _RecipeRepository ??= new RecipeRepository(this);
            }
        }

        private IRecipeLineRepository _RecipeLineRepository;
        public IRecipeLineRepository RecipeLineRepository
        {
            get
            {
                return _RecipeLineRepository ??= new RecipeLineRepository(this);
            }
        }

        private IMenuRepository _MenuRepository;
        public IMenuRepository MenuRepository
        {
            get
            {
                return _MenuRepository ??= new MenuRepository(this);
            }
        }

        private IMenuLineRepository _MenuLineRepository;
        public IMenuLineRepository MenuLineRepository
        {
            get
            {
                return _MenuLineRepository ??= new MenuLineRepository(this);
            }
        }

        private IMenuAssignmentRepository _MenuAssignmentRepository;
        public IMenuAssignmentRepository MenuAssignmentRepository
        {
            get
            {
                return _MenuAssignmentRepository ??= new MenuAssignmentRepository(this);
            }
        }

        private IContractRepository _ContractRepository;
        public IContractRepository ContractRepository
        {
            get
            {
                return _ContractRepository ??= new ContractRepository(this);
            }
        }

        private IContractMealRepository _ContractMealRepository;
        public IContractMealRepository ContractMealRepository
        {
            get
            {
                return _ContractMealRepository ??= new ContractMealRepository(this);
            }
        }

        private IPricePolicyRepository _PricePolicyRepository;
        public IPricePolicyRepository PricePolicyRepository
        {
            get
            {
                return _PricePolicyRepository ??= new PricePolicyRepository(this);
            }
        }

        private IForecastRepository _ForecastRepository;
        public IForecastRepository ForecastRepository
        {
            get
            {
                return _ForecastRepository ??= new ForecastRepository(this);
            }
        }

        private IOrderRepository _OrderRepository;
        public IOrderRepository OrderRepository
        {
            get
            {
                return _OrderRepository ??= new OrderRepository(this);
            }
        }

        private IOrderLineRepository _OrderLineRepository;
        public IOrderLineRepository OrderLineRepository
        {
            get
            {
                return _OrderLineRepository ??= new OrderLineRepository(this);
            }
        }

        private IMaterialRequirementRepository _MaterialRequirementRepository;
        public IMaterialRequirementRepository MaterialRequirementRepository
        {
            get
            {
                return _MaterialRequirementRepository ??= new MaterialRequirementRepository(this);
            }
        }

        private IPurchaseRequestRepository _PurchaseRequestRepository;
        public IPurchaseRequestRepository PurchaseRequestRepository
        {
            get
            {
                return _PurchaseRequestRepository ??= new PurchaseRequestRepository(this);
            }
        }

        private IPurchaseRequestLineRepository _PurchaseRequestLineRepository;
        public IPurchaseRequestLineRepository PurchaseRequestLineRepository
        {
            get
            {
                return _PurchaseRequestLineRepository ??= new PurchaseRequestLineRepository(this);
            }
        }

        private IPurchaseOrderRepository _PurchaseOrderRepository;
        public IPurchaseOrderRepository PurchaseOrderRepository
        {
            get
            {
                return _PurchaseOrderRepository ??= new PurchaseOrderRepository(this);
            }
        }

        private IPurchaseOrderLineRepository _PurchaseOrderLineRepository;
        public IPurchaseOrderLineRepository PurchaseOrderLineRepository
        {
            get
            {
                return _PurchaseOrderLineRepository ??= new PurchaseOrderLineRepository(this);
            }
        }

        private IGoodsReceiptRepository _GoodsReceiptRepository;
        public IGoodsReceiptRepository GoodsReceiptRepository
        {
            get
            {
                return _GoodsReceiptRepository ??= new GoodsReceiptRepository(this);
            }
        }

        private IGoodsReceiptLineRepository _GoodsReceiptLineRepository;
        public IGoodsReceiptLineRepository GoodsReceiptLineRepository
        {
            get
            {
                return _GoodsReceiptLineRepository ??= new GoodsReceiptLineRepository(this);
            }
        }

        private IQcInspectionRepository _QcInspectionRepository;
        public IQcInspectionRepository QcInspectionRepository
        {
            get
            {
                return _QcInspectionRepository ??= new QcInspectionRepository(this);
            }
        }

        private IQcInspectionLineRepository _QcInspectionLineRepository;
        public IQcInspectionLineRepository QcInspectionLineRepository
        {
            get
            {
                return _QcInspectionLineRepository ??= new QcInspectionLineRepository(this);
            }
        }

        private IWarehouseRepository _WarehouseRepository;
        public IWarehouseRepository WarehouseRepository
        {
            get
            {
                return _WarehouseRepository ??= new WarehouseRepository(this);
            }
        }

        private IStorageZoneRepository _StorageZoneRepository;
        public IStorageZoneRepository StorageZoneRepository
        {
            get
            {
                return _StorageZoneRepository ??= new StorageZoneRepository(this);
            }
        }

        private IStorageLocationRepository _StorageLocationRepository;
        public IStorageLocationRepository StorageLocationRepository
        {
            get
            {
                return _StorageLocationRepository ??= new StorageLocationRepository(this);
            }
        }

        private IInventoryPolicyRepository _InventoryPolicyRepository;
        public IInventoryPolicyRepository InventoryPolicyRepository
        {
            get
            {
                return _InventoryPolicyRepository ??= new InventoryPolicyRepository(this);
            }
        }

        private IInventoryTransactionRepository _InventoryTransactionRepository;
        public IInventoryTransactionRepository InventoryTransactionRepository
        {
            get
            {
                return _InventoryTransactionRepository ??= new InventoryTransactionRepository(this);
            }
        }

        private IInventoryBalanceRepository _InventoryBalanceRepository;
        public IInventoryBalanceRepository InventoryBalanceRepository
        {
            get
            {
                return _InventoryBalanceRepository ??= new InventoryBalanceRepository(this);
            }
        }

        private IStockCountRepository _StockCountRepository;
        public IStockCountRepository StockCountRepository
        {
            get
            {
                return _StockCountRepository ??= new StockCountRepository(this);
            }
        }

        private IStockCountLineRepository _StockCountLineRepository;
        public IStockCountLineRepository StockCountLineRepository
        {
            get
            {
                return _StockCountLineRepository ??= new StockCountLineRepository(this);
            }
        }

        private IProductionPlanRepository _ProductionPlanRepository;
        public IProductionPlanRepository ProductionPlanRepository
        {
            get
            {
                return _ProductionPlanRepository ??= new ProductionPlanRepository(this);
            }
        }

        private IProductionPlanLineRepository _ProductionPlanLineRepository;
        public IProductionPlanLineRepository ProductionPlanLineRepository
        {
            get
            {
                return _ProductionPlanLineRepository ??= new ProductionPlanLineRepository(this);
            }
        }

        private IProductionOrderRepository _ProductionOrderRepository;
        public IProductionOrderRepository ProductionOrderRepository
        {
            get
            {
                return _ProductionOrderRepository ??= new ProductionOrderRepository(this);
            }
        }

        private IMaterialReservationRepository _MaterialReservationRepository;
        public IMaterialReservationRepository MaterialReservationRepository
        {
            get
            {
                return _MaterialReservationRepository ??= new MaterialReservationRepository(this);
            }
        }

        private IMaterialIssueRepository _MaterialIssueRepository;
        public IMaterialIssueRepository MaterialIssueRepository
        {
            get
            {
                return _MaterialIssueRepository ??= new MaterialIssueRepository(this);
            }
        }

        private IMaterialIssueLineRepository _MaterialIssueLineRepository;
        public IMaterialIssueLineRepository MaterialIssueLineRepository
        {
            get
            {
                return _MaterialIssueLineRepository ??= new MaterialIssueLineRepository(this);
            }
        }

        private IProductionBatchRepository _ProductionBatchRepository;
        public IProductionBatchRepository ProductionBatchRepository
        {
            get
            {
                return _ProductionBatchRepository ??= new ProductionBatchRepository(this);
            }
        }

        private IProductionOutputRepository _ProductionOutputRepository;
        public IProductionOutputRepository ProductionOutputRepository
        {
            get
            {
                return _ProductionOutputRepository ??= new ProductionOutputRepository(this);
            }
        }

        private IPackingOrderRepository _PackingOrderRepository;
        public IPackingOrderRepository PackingOrderRepository
        {
            get
            {
                return _PackingOrderRepository ??= new PackingOrderRepository(this);
            }
        }

        private IPackingResultRepository _PackingResultRepository;
        public IPackingResultRepository PackingResultRepository
        {
            get
            {
                return _PackingResultRepository ??= new PackingResultRepository(this);
            }
        }

        private IDeliveryRouteRepository _DeliveryRouteRepository;
        public IDeliveryRouteRepository DeliveryRouteRepository
        {
            get
            {
                return _DeliveryRouteRepository ??= new DeliveryRouteRepository(this);
            }
        }

        private IDeliveryOrderRepository _DeliveryOrderRepository;
        public IDeliveryOrderRepository DeliveryOrderRepository
        {
            get
            {
                return _DeliveryOrderRepository ??= new DeliveryOrderRepository(this);
            }
        }

        private IDeliveryLoadingRepository _DeliveryLoadingRepository;
        public IDeliveryLoadingRepository DeliveryLoadingRepository
        {
            get
            {
                return _DeliveryLoadingRepository ??= new DeliveryLoadingRepository(this);
            }
        }

        private IDeliveryExecutionRepository _DeliveryExecutionRepository;
        public IDeliveryExecutionRepository DeliveryExecutionRepository
        {
            get
            {
                return _DeliveryExecutionRepository ??= new DeliveryExecutionRepository(this);
            }
        }

        private IProofOfDeliveryRepository _ProofOfDeliveryRepository;
        public IProofOfDeliveryRepository ProofOfDeliveryRepository
        {
            get
            {
                return _ProofOfDeliveryRepository ??= new ProofOfDeliveryRepository(this);
            }
        }

        private IContainerReturnRepository _ContainerReturnRepository;
        public IContainerReturnRepository ContainerReturnRepository
        {
            get
            {
                return _ContainerReturnRepository ??= new ContainerReturnRepository(this);
            }
        }

        private IReturnInspectionRepository _ReturnInspectionRepository;
        public IReturnInspectionRepository ReturnInspectionRepository
        {
            get
            {
                return _ReturnInspectionRepository ??= new ReturnInspectionRepository(this);
            }
        }

        private ICleaningBatchRepository _CleaningBatchRepository;
        public ICleaningBatchRepository CleaningBatchRepository
        {
            get
            {
                return _CleaningBatchRepository ??= new CleaningBatchRepository(this);
            }
        }

        private IOrderReconciliationRepository _OrderReconciliationRepository;
        public IOrderReconciliationRepository OrderReconciliationRepository
        {
            get
            {
                return _OrderReconciliationRepository ??= new OrderReconciliationRepository(this);
            }
        }

        private IMaterialConsumptionVarianceRepository _MaterialConsumptionVarianceRepository;
        public IMaterialConsumptionVarianceRepository MaterialConsumptionVarianceRepository
        {
            get
            {
                return _MaterialConsumptionVarianceRepository ??= new MaterialConsumptionVarianceRepository(this);
            }
        }

        private IStandardCostRepository _StandardCostRepository;
        public IStandardCostRepository StandardCostRepository
        {
            get
            {
                return _StandardCostRepository ??= new StandardCostRepository(this);
            }
        }

        private IActualCostRepository _ActualCostRepository;
        public IActualCostRepository ActualCostRepository
        {
            get
            {
                return _ActualCostRepository ??= new ActualCostRepository(this);
            }
        }

        private IMealCostRepository _MealCostRepository;
        public IMealCostRepository MealCostRepository
        {
            get
            {
                return _MealCostRepository ??= new MealCostRepository(this);
            }
        }
    }
}
