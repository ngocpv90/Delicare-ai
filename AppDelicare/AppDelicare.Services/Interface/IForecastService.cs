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
        // BE2.2 (spec 6.5): ma trận, nhập hàng loạt (phiên bản), công bố.
        public Task<IResult<AppDelicare.Model.Model.ForecastMatrixModel>> GetMatrix(AppDelicare.Model.Request.Custom.ForecastMatrixRequest request);
        public Task<IResult<AppDelicare.Model.Model.ForecastImportResultModel>> Import(AppDelicare.Model.Request.Custom.ForecastImportRequest request);
        public Task<IResult<AppDelicare.Model.Model.ForecastPublishResultModel>> Publish(AppDelicare.Model.Request.Custom.ForecastPublishRequest request);
        /// <summary>FC-1.1 / 1.2 (D-76): tạo dự báo gợi ý → bản Nháp.</summary>
        public Task<IResult<AppDelicare.Model.Model.ForecastSuggestResultModel>> Suggest(AppDelicare.Model.Request.Custom.ForecastSuggestRequest request);
        /// <summary>FC-1.5: sai số dự báo theo khách / tuần.</summary>
        public Task<IResult<AppDelicare.Model.Model.ForecastAccuracyModel>> GetAccuracy(AppDelicare.Model.Request.Custom.ForecastAccuracyRequest request);
        // UI-6.5 (D-84): dự báo theo tuần
        public Task<IResult<AppDelicare.Model.Model.ForecastWeekModel>> GetWeek(AppDelicare.Model.Model.ForecastWeekRequest request);
        public Task<IResult<bool>> SaveCell(AppDelicare.Model.Model.ForecastCellRequest request);
        public Task<IResult<AppDelicare.Model.Model.ForecastSuggestResultModel>> SuggestWeek(AppDelicare.Model.Model.ForecastWeekActionRequest request);
        public Task<IResult<AppDelicare.Model.Model.ForecastWeekPublishResultModel>> PublishWeek(AppDelicare.Model.Model.ForecastWeekActionRequest request, bool auto = false);
    }
}
