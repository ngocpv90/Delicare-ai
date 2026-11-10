using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class WarehouseRepository(IUnitOfWork unitOfWork) : Repository<Warehouse>(unitOfWork), IWarehouseRepository
	{
	}
}
