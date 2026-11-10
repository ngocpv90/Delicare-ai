using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IQcInspectionLineService
    {
        public Task<IResults<QcInspectionLineViewModel>> GetAll(QcInspectionLineSearchRequest request);
        public Task<IResult<bool>> Save(QcInspectionLineRequest request, string typeSave);
        public IResults<QcInspectionLine> GetAllData();
        public Task<IResult<QcInspectionLine>> GetById(Guid id);
    }
}
