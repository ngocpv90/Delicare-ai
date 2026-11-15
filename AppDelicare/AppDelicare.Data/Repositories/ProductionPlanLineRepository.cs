using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ProductionPlanLineRepository(IUnitOfWork unitOfWork) : Repository<ProductionPlanLine>(unitOfWork), IProductionPlanLineRepository
	{
	}
}
