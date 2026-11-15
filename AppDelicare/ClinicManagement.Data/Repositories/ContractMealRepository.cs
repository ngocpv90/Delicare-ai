using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ContractMealRepository(IUnitOfWork unitOfWork) : Repository<ContractMeal>(unitOfWork), IContractMealRepository
	{
	}
}
