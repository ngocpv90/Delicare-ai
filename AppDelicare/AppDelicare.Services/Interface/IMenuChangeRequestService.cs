#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IMenuChangeRequestService
    {
        public Task<IResults<MenuChangeRequestViewModel>> GetAll(MenuChangeRequestSearchRequest request);
        public Task<IResult<bool>> Save(MenuChangeRequestRequest request, string typeSave);
        public IResults<MenuChangeRequest> GetAllData();
        public Task<IResult<MenuChangeRequest>> GetById(Guid id);
        // BE2.2 (spec 6.4, Catalog 104): tạo + gửi duyệt MENU_CHANGE, duyệt / từ chối, chi tiết.
        public Task<IResult<AppDelicare.Model.Model.MenuChangeDetailModel>> Create(AppDelicare.Model.Request.Custom.MenuChangeCreateRequest request);
        public Task<IResult<bool>> Decide(AppDelicare.Model.Request.Custom.MenuChangeActionRequest request, bool approve);
        public Task<IResult<AppDelicare.Model.Model.MenuChangeDetailModel>> GetDetail(Guid id);
        /// <summary>Nội bộ (handler MENU_CHANGE) – chạy trong transaction của engine.</summary>
        public Task<string?> BuildApprovalStateAsync(Guid id);
        public Task<string?> DescribeAsync(Guid id);
        public Task<string?> ApproveFromEngineAsync(Guid id, Guid actorUserId);
        public Task<string?> CloseFromEngineAsync(Guid id, bool cancelled);
        /// <summary>UI-6.13 (D-91): đổi món cho mọi khách trên ô Thực đơn tuần – xem trước ảnh hưởng, gửi (tự duyệt khi đủ điều kiện), danh sách đổi món của tuần.</summary>
        public Task<IResult<AppDelicare.Model.Model.MenuChangeCellPreviewModel>> PreviewCell(AppDelicare.Model.Model.MenuChangeCellRequest request);
        public Task<IResult<AppDelicare.Model.Model.MenuChangeDetailModel>> CreateForCell(AppDelicare.Model.Model.MenuChangeCellRequest request);
        public Task<IResults<AppDelicare.Model.Model.MenuChangeWeekItemModel>> GetWeekChanges(AppDelicare.Model.Model.MenuWeekRequest request);
    }
}
