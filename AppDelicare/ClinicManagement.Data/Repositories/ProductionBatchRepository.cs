using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ProductionBatchRepository(IUnitOfWork unitOfWork) : Repository<ProductionBatch>(unitOfWork), IProductionBatchRepository
	{
	}
}
