#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Model.ViewModels.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IKpiMetricDefinitionService
    {
        public Task<IResults<KpiMetricDefinitionCustomViewModel>> GetAll(KpiMetricDefinitionSearchRequest request);
        public Task<IResult<bool>> Save(KpiMetricDefinitionCustomRequest request, string typeSave);
        public IResults<KpiMetricDefinition> GetAllData();
        public Task<IResult<KpiDefinitionModel>> GetById(Guid id);
    }
}
