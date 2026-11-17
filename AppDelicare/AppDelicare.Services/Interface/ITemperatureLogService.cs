#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface ITemperatureLogService
    {
        public Task<IResults<TemperatureLogViewModel>> GetAll(TemperatureLogSearchRequest request);
        public Task<IResult<bool>> Save(TemperatureLogRequest request, string typeSave);
        public IResults<TemperatureLog> GetAllData();
        public Task<IResult<TemperatureLog>> GetById(Guid id);

        // BE2.1 (spec 9.3 POST temperature-logs) – ghi nhiệt độ tự do.
        public Task<IResult<TemperatureLogRecordModel>> Record(TemperatureRecordRequest request);
    }
}
