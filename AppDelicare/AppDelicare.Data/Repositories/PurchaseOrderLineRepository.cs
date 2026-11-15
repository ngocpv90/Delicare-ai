using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class PurchaseOrderLineRepository(IUnitOfWork unitOfWork) : Repository<PurchaseOrderLine>(unitOfWork), IPurchaseOrderLineRepository
	{
	}
}
