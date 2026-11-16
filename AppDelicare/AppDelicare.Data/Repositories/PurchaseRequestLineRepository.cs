using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class PurchaseRequestLineRepository(IUnitOfWork unitOfWork) : Repository<PurchaseRequestLine>(unitOfWork), IPurchaseRequestLineRepository
	{
	}
}
