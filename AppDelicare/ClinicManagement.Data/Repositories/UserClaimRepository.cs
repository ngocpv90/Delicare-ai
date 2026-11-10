using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class UserClaimRepository(IUnitOfWork unitOfWork) : Repository<UserClaim>(unitOfWork), IUserClaimRepository
    {
	}
}
