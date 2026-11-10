using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class UserGroupRepository(IUnitOfWork unitOfWork) : Repository<UserGroup>(unitOfWork), IUserGroupRepository
    {
    }
}
