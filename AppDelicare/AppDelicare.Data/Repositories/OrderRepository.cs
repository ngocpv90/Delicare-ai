using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class OrderRepository(IUnitOfWork unitOfWork) : Repository<Order>(unitOfWork), IOrderRepository
	{
	}
}
