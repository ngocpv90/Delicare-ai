using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IQcInspectionService
    {
        public Task<IResults<QcInspectionViewModel>> GetAll(QcInspectionSearchRequest request);
        public Task<IResult<bool>> Save(QcInspectionRequest request, string typeSave);
        public IResults<QcInspection> GetAllData();
        public Task<IResult<QcInspection>> GetById(Guid id);
    }
}
