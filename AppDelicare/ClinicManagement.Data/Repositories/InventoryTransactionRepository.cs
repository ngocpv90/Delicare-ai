using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class InventoryTransactionRepository(IUnitOfWork unitOfWork) : Repository<InventoryTransaction>(unitOfWork), IInventoryTransactionRepository
	{
	}
}
