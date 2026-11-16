using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMaterialConsumptionVarianceService
    {
        public Task<IResults<MaterialConsumptionVarianceViewModel>> GetAll(MaterialConsumptionVarianceSearchRequest request);
        public Task<IResult<bool>> Save(MaterialConsumptionVarianceRequest request, string typeSave);
        public IResults<MaterialConsumptionVariance> GetAllData();
        public Task<IResult<MaterialConsumptionVariance>> GetById(Guid id);
    }
}
