using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class InventoryPolicyRepository(IUnitOfWork unitOfWork) : Repository<InventoryPolicy>(unitOfWork), IInventoryPolicyRepository
	{
	}
}
