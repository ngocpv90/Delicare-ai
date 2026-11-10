using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class PackingOrderRepository(IUnitOfWork unitOfWork) : Repository<PackingOrder>(unitOfWork), IPackingOrderRepository
	{
	}
}
