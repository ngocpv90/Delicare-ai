using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class PurchaseRequestRepository(IUnitOfWork unitOfWork) : Repository<PurchaseRequest>(unitOfWork), IPurchaseRequestRepository
	{
	}
}
