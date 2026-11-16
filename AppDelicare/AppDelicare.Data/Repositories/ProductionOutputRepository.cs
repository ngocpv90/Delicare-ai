using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ProductionOutputRepository(IUnitOfWork unitOfWork) : Repository<ProductionOutput>(unitOfWork), IProductionOutputRepository
	{
	}
}
