using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ProductionPlanRepository(IUnitOfWork unitOfWork) : Repository<ProductionPlan>(unitOfWork), IProductionPlanRepository
	{
	}
}
