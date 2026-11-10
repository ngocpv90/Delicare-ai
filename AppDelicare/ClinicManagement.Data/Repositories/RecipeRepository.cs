using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class RecipeRepository(IUnitOfWork unitOfWork) : Repository<Recipe>(unitOfWork), IRecipeRepository
	{
	}
}
