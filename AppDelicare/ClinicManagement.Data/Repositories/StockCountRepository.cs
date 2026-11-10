using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class StockCountRepository(IUnitOfWork unitOfWork) : Repository<StockCount>(unitOfWork), IStockCountRepository
	{
	}
}
