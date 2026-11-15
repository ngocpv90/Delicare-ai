using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MenuRepository(IUnitOfWork unitOfWork) : Repository<Menu>(unitOfWork), IMenuRepository
	{
	}
}
