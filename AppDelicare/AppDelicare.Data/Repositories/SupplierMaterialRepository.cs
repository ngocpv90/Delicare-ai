using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class SupplierMaterialRepository(IUnitOfWork unitOfWork) : Repository<SupplierMaterial>(unitOfWork), ISupplierMaterialRepository
	{
	}
}
