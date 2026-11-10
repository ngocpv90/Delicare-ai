using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Data.Repositories
{
    public class ProductRepository(IUnitOfWork unitOfWork) : Repository<Products>(unitOfWork), IProductRepository
    {
    }
}
