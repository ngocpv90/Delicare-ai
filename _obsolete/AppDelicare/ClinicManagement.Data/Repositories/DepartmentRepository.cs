using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Data.Repositories
{
    public class DepartmentRepository(IUnitOfWork unitOfWork) : Repository<Department>(unitOfWork), IDepartmentRepository
    {
    }
}
