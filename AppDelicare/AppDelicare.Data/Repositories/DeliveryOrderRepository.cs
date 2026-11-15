using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class DeliveryOrderRepository(IUnitOfWork unitOfWork) : Repository<DeliveryOrder>(unitOfWork), IDeliveryOrderRepository
	{
	}
}
