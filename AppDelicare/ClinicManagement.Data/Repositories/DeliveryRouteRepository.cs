using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class DeliveryRouteRepository(IUnitOfWork unitOfWork) : Repository<DeliveryRoute>(unitOfWork), IDeliveryRouteRepository
	{
	}
}
