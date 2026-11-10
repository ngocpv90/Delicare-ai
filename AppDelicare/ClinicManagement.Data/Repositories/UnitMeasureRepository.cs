using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class UnitMeasureRepository(IUnitOfWork unitOfWork) : Repository<UnitMeasure>(unitOfWork), IUnitMeasureRepository
    {
	}
}
