using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class StockCountLineRepository(IUnitOfWork unitOfWork) : Repository<StockCountLine>(unitOfWork), IStockCountLineRepository
	{
	}
}
