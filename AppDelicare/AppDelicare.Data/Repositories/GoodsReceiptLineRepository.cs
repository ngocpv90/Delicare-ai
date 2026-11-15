using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class GoodsReceiptLineRepository(IUnitOfWork unitOfWork) : Repository<GoodsReceiptLine>(unitOfWork), IGoodsReceiptLineRepository
	{
	}
}
