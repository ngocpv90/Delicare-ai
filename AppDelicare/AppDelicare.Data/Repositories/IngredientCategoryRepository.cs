using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class IngredientCategoryRepository(IUnitOfWork unitOfWork) : Repository<IngredientCategory>(unitOfWork), IIngredientCategoryRepository
	{
	}
}
