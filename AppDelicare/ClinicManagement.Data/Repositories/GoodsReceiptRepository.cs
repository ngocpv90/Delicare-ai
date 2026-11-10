using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class GoodsReceiptRepository(IUnitOfWork unitOfWork) : Repository<GoodsReceipt>(unitOfWork), IGoodsReceiptRepository
	{
	}
}
