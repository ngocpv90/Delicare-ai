using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class PackingResultRepository(IUnitOfWork unitOfWork) : Repository<PackingResult>(unitOfWork), IPackingResultRepository
	{
	}
}
