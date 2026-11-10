using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ForecastRepository(IUnitOfWork unitOfWork) : Repository<Forecast>(unitOfWork), IForecastRepository
	{
	}
}
