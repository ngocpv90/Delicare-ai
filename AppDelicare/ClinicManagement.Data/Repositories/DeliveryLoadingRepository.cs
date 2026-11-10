using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class DeliveryLoadingRepository(IUnitOfWork unitOfWork) : Repository<DeliveryLoading>(unitOfWork), IDeliveryLoadingRepository
	{
	}
}
