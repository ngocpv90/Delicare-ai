using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>T45B.5: dữ liệu hỗ trợ giao diện không gắn bảng (spec 2.2 /meta/statuses, /meta/reference-lists) – khung cũ.</summary>
    public interface IMetaDataService
    {
        /// <summary>Bộ trạng thái; <paramref name="key"/> = "Bảng.Cột" hoặc tên bảng (trống = tất cả 47 bộ).</summary>
        public IResults<StatusSetModel> GetStatuses(string? key);
        /// <summary>Danh mục tham chiếu chưa có bảng; <paramref name="name"/> trống = tất cả.</summary>
        public IResults<ReferenceListModel> GetReferenceLists(string? name);
    }
}
