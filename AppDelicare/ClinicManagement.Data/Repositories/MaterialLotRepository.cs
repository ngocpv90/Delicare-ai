using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MaterialLotRepository(IUnitOfWork unitOfWork) : Repository<MaterialLot>(unitOfWork), IMaterialLotRepository
	{
	}
}
