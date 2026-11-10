using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IReturnInspectionService
    {
        public Task<IResults<ReturnInspectionViewModel>> GetAll(ReturnInspectionSearchRequest request);
        public Task<IResult<bool>> Save(ReturnInspectionRequest request, string typeSave);
        public IResults<ReturnInspection> GetAllData();
        public Task<IResult<ReturnInspection>> GetById(Guid id);
    }
}
