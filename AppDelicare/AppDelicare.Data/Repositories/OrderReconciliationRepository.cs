using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class OrderReconciliationRepository(IUnitOfWork unitOfWork) : Repository<OrderReconciliation>(unitOfWork), IOrderReconciliationRepository
	{
	}
}
