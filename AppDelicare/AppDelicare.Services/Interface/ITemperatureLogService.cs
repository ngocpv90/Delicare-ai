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
        /// <summary>BE3.1 (Catalog 163): chuỗi nhiệt độ 1 khu vực kho cho biểu đồ.</summary>
        public Task<IResult<AppDelicare.Model.Model.TemperatureSeriesModel>> GetSeries(AppDelicare.Model.Request.Custom.TemperatureSeriesRequest request);
        /// <summary>UI-6.31 (D-112): bảng "Hôm nay" theo khu.</summary>
        public Task<IResult<AppDelicare.Model.Model.TemperatureDayBoardModel>> GetDayBoard(DateTime? date);
        /// <summary>UI-6.31: ghi một lượt nhiều khu – một giao dịch.</summary>
        public Task<IResult<AppDelicare.Model.Model.TemperatureRoundResultModel>> RecordRound(AppDelicare.Model.Model.TemperatureRoundRequest request);
        /// <summary>UI-6.31: Sổ nhiệt độ – chỉ xem.</summary>
        public Task<IResult<AppDelicare.Model.Model.TemperatureBookModel>> GetBook(AppDelicare.Model.Model.TemperatureBookRequest request);
        /// <summary>UI-6.31: tin bot "đến giờ chưa đo" / "cảm biến mất tín hiệu" (BotWatch).</summary>
        public Task<int> RemindAsync(DateTime now);
    }
}
