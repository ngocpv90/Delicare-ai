using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ProductionOrderRepository(IUnitOfWork unitOfWork) : Repository<ProductionOrder>(unitOfWork), IProductionOrderRepository
	{
	}
}
