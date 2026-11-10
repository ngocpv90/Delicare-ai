using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class RecipeLineRepository(IUnitOfWork unitOfWork) : Repository<RecipeLine>(unitOfWork), IRecipeLineRepository
	{
	}
}
