using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class SupplierRepository(IUnitOfWork unitOfWork) : Repository<Supplier>(unitOfWork), ISupplierRepository
	{
	}
}
