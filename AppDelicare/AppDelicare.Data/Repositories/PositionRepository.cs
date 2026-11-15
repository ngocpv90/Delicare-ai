using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class PositionRepository(IUnitOfWork unitOfWork) : Repository<Position>(unitOfWork), IPositionRepository
	{
	}
}
