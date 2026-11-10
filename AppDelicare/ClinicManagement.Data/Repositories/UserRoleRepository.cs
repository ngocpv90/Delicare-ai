using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class UserRoleRepository(IUnitOfWork unitOfWork) : Repository<UserRole>(unitOfWork), IUserRoleRepository
    {
    }
}
