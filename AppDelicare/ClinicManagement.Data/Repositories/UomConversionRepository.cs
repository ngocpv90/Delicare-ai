using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class UomConversionRepository(IUnitOfWork unitOfWork) : Repository<UomConversion>(unitOfWork), IUomConversionRepository
	{
	}
}
