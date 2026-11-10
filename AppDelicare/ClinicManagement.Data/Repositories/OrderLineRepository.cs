using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class OrderLineRepository(IUnitOfWork unitOfWork) : Repository<OrderLine>(unitOfWork), IOrderLineRepository
	{
	}
}
