using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class ObjectMenuRepository(IUnitOfWork unitOfWork) : Repository<ObjectMenu>(unitOfWork), IObjectMenuRepository
    {
    }
}
