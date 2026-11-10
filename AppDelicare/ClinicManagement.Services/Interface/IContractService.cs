using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IContractService
    {
        public Task<IResults<ContractViewModel>> GetAll(ContractSearchRequest request);
        public Task<IResult<bool>> Save(ContractRequest request, string typeSave);
        public IResults<Contract> GetAllData();
        public Task<IResult<Contract>> GetById(Guid id);
    }
}
