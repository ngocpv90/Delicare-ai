using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Model.Request.Custom;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMaterialLotService
    {
        public Task<IResults<MaterialLotViewModel>> GetAll(MaterialLotSearchRequest request);
        public Task<IResult<bool>> Save(MaterialLotRequest request, string typeSave);
        public IResults<MaterialLot> GetAllData();
        public Task<IResult<MaterialLot>> GetById(Guid id);

        // BE2.1 (spec 7.5) – danh sách lô + truy xuất xuôi.
        public Task<IResults<AppDelicare.Model.Model.MaterialLotListItemModel>> GetList(AppDelicare.Model.Request.Custom.MaterialLotListRequest request);
        public Task<IResult<AppDelicare.Model.Model.TraceModel>> Trace(Guid id);
        /// <summary>T16: tạm giữ lô (Available → OnHold), lý do bắt buộc.</summary>
        public Task<IResult<MaterialLot>> Hold(MaterialLotHoldRequest request);
        /// <summary>T16: mở lô tạm giữ (OnHold → Available).</summary>
        public Task<IResult<MaterialLot>> Release(MaterialLotHoldRequest request);
    }
}
