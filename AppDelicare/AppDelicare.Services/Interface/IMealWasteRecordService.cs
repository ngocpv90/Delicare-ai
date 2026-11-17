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
    public interface IMealWasteRecordService
    {
        public Task<IResults<MealWasteRecordViewModel>> GetAll(MealWasteRecordSearchRequest request);
        public Task<IResult<bool>> Save(MealWasteRecordRequest request, string typeSave);
        public IResults<MealWasteRecord> GetAllData();
        public Task<IResult<MealWasteRecord>> GetById(Guid id);

        // BE2.1 (spec 8.4 waste-records).
        public Task<IResult<MealWasteItemModel>> Record(MealWasteRecordCreateRequest request);
        public Task<IResult<MealWasteListModel>> GetList(MealWasteListRequest request);
    }
}
