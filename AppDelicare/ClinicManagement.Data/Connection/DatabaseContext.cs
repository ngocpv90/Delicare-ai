using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using System.Text;

namespace AppDelicare.Data.Connection
{
	public class DatabaseContext(DbContextOptions<DatabaseContext> options, IAuthenticatedService authenticatedService) : IdentityDbContext(options)
    {
        private readonly IAuthenticatedService _authenticatedService = authenticatedService;

		public virtual DbSet<User> User { get; set; }
        public virtual DbSet<ObjectMenu> ObjectMenu { get; set; }
        public virtual DbSet<UserGroup> UserGroup { get; set; }
        public virtual DbSet<UserRole> UserRole { get; set; }
        public virtual DbSet<DocumentFile> DocumentFile { get; set; }
        public virtual DbSet<LocationTracking> LocationTracking { get; set; }
        public virtual DbSet<AuditChange> AuditChange { get; set; }
        public virtual DbSet<UserClaim> UserClaim { get; set; }
		//Custom
		public virtual DbSet<PatientDetail> PatientDetail { get; set; }
		public virtual DbSet<MedicalExamination> MedicalExamination { get; set; }
		public virtual DbSet<MedicineWarehouse> MedicineWarehouse { get; set; }
		public virtual DbSet<Supplier> Supplier { get; set; }
		public virtual DbSet<UnitMeasure> UnitMeasure { get; set; }
		public virtual DbSet<AdministrativeDistrict> AdministrativeDistrict { get; set; }
		public virtual DbSet<AdministrativeProvince> AdministrativeProvince { get; set; }
		public virtual DbSet<AdministrativeRegion> AdministrativeRegion { get; set; }
		public virtual DbSet<AdministrativeUnit> AdministrativeUnit { get; set; }
		public virtual DbSet<AdministrativeWard> AdministrativeWard { get; set; }
		public virtual DbSet<Person> Person { get; set; }
		public virtual DbSet<TestDemo> TestDemo { get; set; }
		public virtual DbSet<Patient> Patient { get; set; }
        //App
        public virtual DbSet<AppConfig> AppConfig { get; set; }
        public virtual DbSet<Customer> Customer { get; set; }
        public virtual DbSet<Region> Region { get; set; }
        public virtual DbSet<Area> Area { get; set; }
        public virtual DbSet<Branch> Branch { get; set; }
        public virtual DbSet<Department> Department { get; set; }
        public virtual DbSet<Employee> Employee { get; set; }
        public virtual DbSet<Products> Products { get; set; }
        public virtual DbSet<Leads> Leads { get; set; }
        public virtual DbSet<Opportunity> Opportunity { get; set; }
        public virtual DbSet<Company> Company { get; set; }
        public virtual DbSet<OrganizationUnit> OrganizationUnit { get; set; }
        public virtual DbSet<Position> Position { get; set; }
        public virtual DbSet<Shift> Shift { get; set; }
        public virtual DbSet<CustomerContact> CustomerContact { get; set; }
        public virtual DbSet<CustomerDeliveryLocation> CustomerDeliveryLocation { get; set; }
        public virtual DbSet<IngredientCategory> IngredientCategory { get; set; }
        public virtual DbSet<UnitOfMeasure> UnitOfMeasure { get; set; }
        public virtual DbSet<UomConversion> UomConversion { get; set; }
        public virtual DbSet<Ingredient> Ingredient { get; set; }
        public virtual DbSet<SupplierMaterial> SupplierMaterial { get; set; }
        public virtual DbSet<MaterialLot> MaterialLot { get; set; }
        public virtual DbSet<DishCategory> DishCategory { get; set; }
        public virtual DbSet<Dish> Dish { get; set; }
        public virtual DbSet<Recipe> Recipe { get; set; }
        public virtual DbSet<RecipeLine> RecipeLine { get; set; }
        public virtual DbSet<Menu> Menu { get; set; }
        public virtual DbSet<MenuLine> MenuLine { get; set; }
        public virtual DbSet<MenuAssignment> MenuAssignment { get; set; }
        public virtual DbSet<Contract> Contract { get; set; }
        public virtual DbSet<ContractMeals> ContractMeal { get; set; }
        public virtual DbSet<PricePolicy> PricePolicy { get; set; }
        public virtual DbSet<Forecast> Forecast { get; set; }
        public virtual DbSet<Order> Order { get; set; }
        public virtual DbSet<OrderLine> OrderLine { get; set; }
        public virtual DbSet<MaterialRequirement> MaterialRequirement { get; set; }
        public virtual DbSet<PurchaseRequest> PurchaseRequest { get; set; }
        public virtual DbSet<PurchaseRequestLine> PurchaseRequestLine { get; set; }
        public virtual DbSet<PurchaseOrder> PurchaseOrder { get; set; }
        public virtual DbSet<PurchaseOrderLine> PurchaseOrderLine { get; set; }
        public virtual DbSet<GoodsReceipt> GoodsReceipt { get; set; }
        public virtual DbSet<GoodsReceiptLine> GoodsReceiptLine { get; set; }
        public virtual DbSet<QcInspection> QcInspection { get; set; }
        public virtual DbSet<QcInspectionLine> QcInspectionLine { get; set; }
        public virtual DbSet<Warehouse> Warehouse { get; set; }
        public virtual DbSet<StorageZone> StorageZone { get; set; }
        public virtual DbSet<StorageLocation> StorageLocation { get; set; }
        public virtual DbSet<InventoryPolicy> InventoryPolicy { get; set; }
        public virtual DbSet<InventoryTransaction> InventoryTransaction { get; set; }
        public virtual DbSet<InventoryBalance> InventoryBalance { get; set; }
        public virtual DbSet<StockCount> StockCount { get; set; }
        public virtual DbSet<StockCountLine> StockCountLine { get; set; }
        public virtual DbSet<ProductionPlan> ProductionPlan { get; set; }
        public virtual DbSet<ProductionPlanLine> ProductionPlanLine { get; set; }
        public virtual DbSet<ProductionOrder> ProductionOrder { get; set; }
        public virtual DbSet<MaterialReservation> MaterialReservation { get; set; }
        public virtual DbSet<MaterialIssue> MaterialIssue { get; set; }
        public virtual DbSet<MaterialIssueLine> MaterialIssueLine { get; set; }
        public virtual DbSet<ProductionBatch> ProductionBatch { get; set; }
        public virtual DbSet<ProductionOutput> ProductionOutput { get; set; }
        public virtual DbSet<PackingOrder> PackingOrder { get; set; }
        public virtual DbSet<PackingResult> PackingResult { get; set; }
        public virtual DbSet<DeliveryRoute> DeliveryRoute { get; set; }
        public virtual DbSet<DeliveryOrder> DeliveryOrder { get; set; }
        public virtual DbSet<DeliveryLoading> DeliveryLoading { get; set; }
        public virtual DbSet<DeliveryExecution> DeliveryExecution { get; set; }
        public virtual DbSet<ProofOfDelivery> ProofOfDelivery { get; set; }
        public virtual DbSet<ContainerReturn> ContainerReturn { get; set; }
        public virtual DbSet<ReturnInspection> ReturnInspection { get; set; }
        public virtual DbSet<CleaningBatch> CleaningBatch { get; set; }
        public virtual DbSet<OrderReconciliation> OrderReconciliation { get; set; }
        public virtual DbSet<MaterialConsumptionVariance> MaterialConsumptionVariance { get; set; }
        public virtual DbSet<StandardCost> StandardCost { get; set; }
        public virtual DbSet<ActualCost> ActualCost { get; set; }
        public virtual DbSet<MealCost> MealCost { get; set; }
        public async Task<int> SaveChangesAsync(string typeSave, CancellationToken cancellationToken = new CancellationToken())
        {
            var user = _authenticatedService.Id;
            var date = DateTime.Now;
            foreach (var entry in ChangeTracker.Entries<BaseEntity>().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedDate = date;
                        entry.Entity.CreatedBy = user;
                        entry.Entity.IsDeleted = false;
                        break;
                    case EntityState.Modified:
                        if (typeSave == ConstantHelper.TypeSave.Update || typeSave == ConstantHelper.TypeSave.UpdateProfile)
                        {
                            entry.Entity.ModifiedDate = date;
                            entry.Entity.ModifiedBy = user;
                        }
                        if (typeSave == ConstantHelper.TypeSave.Delete)
                        {
                            entry.Entity.DeletedDate = date;
                            entry.Entity.DeleteddBy = user;
                            entry.Entity.IsDeleted = true;
                        }
                        break;
                    default:
                        break;
                }
            }
            var modifiedEntities = ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified).ToList();
            foreach (var modifiedEntity in modifiedEntities)
            {
                AuditChange auditLog = new()
                {
                    IdChange = modifiedEntity.Entity.Id,
                    EntityName = modifiedEntity.Entity.GetType().Name,
                    Action = modifiedEntity.State.ToString(),
                    CreatedDate = date,
                    CreatedBy = user,
                    Changes = GetChanges(modifiedEntity, modifiedEntity.State),
                    IsDeleted = false,
                };
                AuditChange.Add(auditLog);
            }
            return await base.SaveChangesAsync(cancellationToken);
        }

        private static string GetChanges(EntityEntry entity, EntityState state)
        {
            StringBuilder changes = new();
            foreach (var property in entity.OriginalValues.Properties)
            {
                var originalValue = state == EntityState.Modified ? entity.OriginalValues[property] : ConstantHelper.Space;
                var currentValue = entity.CurrentValues[property];
                if (!Equals(originalValue, currentValue))
                {
                    if (state == EntityState.Added && !string.IsNullOrEmpty(currentValue?.ToString()?.Trim()))
                    {
                        changes.AppendLine($"- {property.Name}: '{currentValue}' {ConstantHelper.Break}");
                    }
                    if (state == EntityState.Modified)
                    {
                        changes.AppendLine($"- {property.Name}: From '{originalValue}' to '{currentValue}' {ConstantHelper.Break}");
                    }
                }
            }
            return changes.ToString();
        }
    }
}
