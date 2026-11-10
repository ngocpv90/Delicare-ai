using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MedicineWarehouseRepository(IUnitOfWork unitOfWork) : Repository<MedicineWarehouse>(unitOfWork), IMedicineWarehouseRepository
    {
	}
}
