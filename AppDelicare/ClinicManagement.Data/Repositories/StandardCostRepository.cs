using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class StandardCostRepository(IUnitOfWork unitOfWork) : Repository<StandardCost>(unitOfWork), IStandardCostRepository
	{
	}
}
