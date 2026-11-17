using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IContainerReturnService
    {
        public Task<IResults<ContainerReturnViewModel>> GetAll(ContainerReturnSearchRequest request);
        public Task<IResult<bool>> Save(ContainerReturnRequest request, string typeSave);
        public IResults<ContainerReturn> GetAllData();
        public Task<IResult<ContainerReturn>> GetById(Guid id);

        /// <summary>BE2.5 (spec 10.4): chi tiết phiếu thu hồi.</summary>
        public Task<IResult<ContainerReturnDetailModel>> GetDetail(Guid id);
        /// <summary>BE2.5 (spec 10.4): danh sách phiếu thu hồi + đếm theo trạng thái.</summary>
        public Task<IResult<ContainerReturnListModel>> GetList(ContainerReturnListRequest request);
        /// <summary>BE2.5 (Catalog 194): ghi thu hồi khay / hộp + sổ bao bì IN.</summary>
        public Task<IResult<ContainerReturnDetailModel>> Record(ContainerReturnRecordRequest request);
        /// <summary>BE2.5 (Catalog 195): kiểm tra vật dụng thu hồi + tự lập mẻ vệ sinh.</summary>
        public Task<IResult<ContainerReturnDetailModel>> Inspect(ContainerReturnInspectRequest request);
    }
}
