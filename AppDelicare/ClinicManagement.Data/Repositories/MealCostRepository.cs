using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MealCostRepository(IUnitOfWork unitOfWork) : Repository<MealCost>(unitOfWork), IMealCostRepository
	{
	}
}
