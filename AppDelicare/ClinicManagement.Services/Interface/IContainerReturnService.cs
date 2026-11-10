using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IContainerReturnService
    {
        public Task<IResults<ContainerReturnViewModel>> GetAll(ContainerReturnSearchRequest request);
        public Task<IResult<bool>> Save(ContainerReturnRequest request, string typeSave);
        public IResults<ContainerReturn> GetAllData();
        public Task<IResult<ContainerReturn>> GetById(Guid id);
    }
}
