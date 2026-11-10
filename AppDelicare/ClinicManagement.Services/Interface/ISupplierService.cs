using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface ISupplierService
    {
        public Task<IResults<SupplierViewModel>> GetAll(SupplierSearchRequest request);
        public Task<IResult<bool>> Save(SupplierRequest request, string typeSave);
        public IResults<Supplier> GetAllData();
        public Task<IResult<Supplier>> GetById(Guid id);
    }
}
