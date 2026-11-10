using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class UserRepository(IUnitOfWork unitOfWork) : Repository<User>(unitOfWork), IUserRepository
    {
    }
}
