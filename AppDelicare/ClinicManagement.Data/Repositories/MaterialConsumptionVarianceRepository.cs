using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MaterialConsumptionVarianceRepository(IUnitOfWork unitOfWork) : Repository<MaterialConsumptionVariance>(unitOfWork), IMaterialConsumptionVarianceRepository
	{
	}
}
