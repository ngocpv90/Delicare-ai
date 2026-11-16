using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IForecastService
    {
        public Task<IResults<ForecastViewModel>> GetAll(ForecastSearchRequest request);
        public Task<IResult<bool>> Save(ForecastRequest request, string typeSave);
        public IResults<Forecast> GetAllData();
        public Task<IResult<Forecast>> GetById(Guid id);
    }
}
