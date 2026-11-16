using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ActualCostRepository(IUnitOfWork unitOfWork) : Repository<ActualCost>(unitOfWork), IActualCostRepository
	{
	}
}
