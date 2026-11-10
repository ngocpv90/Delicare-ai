using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface ISupplierMaterialService
    {
        public Task<IResults<SupplierMaterialViewModel>> GetAll(SupplierMaterialSearchRequest request);
        public Task<IResult<bool>> Save(SupplierMaterialRequest request, string typeSave);
        public IResults<SupplierMaterial> GetAllData();
        public Task<IResult<SupplierMaterial>> GetById(Guid id);
    }
}
