using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ContractRepository(IUnitOfWork unitOfWork) : Repository<Contract>(unitOfWork), IContractRepository
	{
	}
}
