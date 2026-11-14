using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IOperationalLocationService
    {
        public Task<IResults<OperationalLocationViewModel>> GetAll(OperationalLocationSearchRequest request);
        public Task<IResult<bool>> Save(OperationalLocationRequest request, string typeSave);
        public IResults<OperationalLocation> GetAllData();
        public Task<IResult<OperationalLocation>> GetById(Guid id);
    }
}
