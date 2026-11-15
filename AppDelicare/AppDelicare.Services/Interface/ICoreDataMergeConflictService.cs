using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface ICoreDataMergeConflictService
    {
        public Task<IResults<CoreDataMergeConflictViewModel>> GetAll(CoreDataMergeConflictSearchRequest request);
        public Task<IResult<CoreDataMergeConflict>> GetById(long id);
        /// <summary>Đánh dấu đã xử lý (ResolvedAt = giờ hiện tại, ResolutionNote). Đã xử lý rồi → lỗi.</summary>
        public Task<IResult<bool>> Resolve(CoreDataMergeConflictResolveRequest request);
    }
}
