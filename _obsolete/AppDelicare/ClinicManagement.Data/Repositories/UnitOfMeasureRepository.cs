using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class UnitOfMeasureRepository(IUnitOfWork unitOfWork) : Repository<UnitOfMeasure>(unitOfWork), IUnitOfMeasureRepository
	{
	}
}
