using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class AreaRepository(IUnitOfWork unitOfWork) : Repository<Area>(unitOfWork), IAreaRepository
    {
    }
}
