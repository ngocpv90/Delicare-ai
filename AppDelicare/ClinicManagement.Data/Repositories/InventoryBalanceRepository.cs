using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class InventoryBalanceRepository(IUnitOfWork unitOfWork) : Repository<InventoryBalance>(unitOfWork), IInventoryBalanceRepository
	{
	}
}
