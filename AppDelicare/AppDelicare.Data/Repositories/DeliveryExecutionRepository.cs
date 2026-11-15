using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class DeliveryExecutionRepository(IUnitOfWork unitOfWork) : Repository<DeliveryExecution>(unitOfWork), IDeliveryExecutionRepository
	{
	}
}
