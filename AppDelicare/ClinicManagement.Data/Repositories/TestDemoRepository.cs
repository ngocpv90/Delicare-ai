using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class TestDemoRepository(IUnitOfWork unitOfWork) : Repository<TestDemo>(unitOfWork), ITestDemoRepository
    {
	}
}
