using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ShiftRepository(IUnitOfWork unitOfWork) : Repository<Shift>(unitOfWork), IShiftRepository
	{
	}
}
