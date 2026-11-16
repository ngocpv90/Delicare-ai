using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IInventoryPolicyService
    {
        public Task<IResults<InventoryPolicyViewModel>> GetAll(InventoryPolicySearchRequest request);
        public Task<IResult<bool>> Save(InventoryPolicyRequest request, string typeSave);
        public IResults<InventoryPolicy> GetAllData();
        public Task<IResult<InventoryPolicy>> GetById(Guid id);
    }
}
