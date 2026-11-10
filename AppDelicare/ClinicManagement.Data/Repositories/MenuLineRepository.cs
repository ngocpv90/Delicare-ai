using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MenuLineRepository(IUnitOfWork unitOfWork) : Repository<MenuLine>(unitOfWork), IMenuLineRepository
	{
	}
}
