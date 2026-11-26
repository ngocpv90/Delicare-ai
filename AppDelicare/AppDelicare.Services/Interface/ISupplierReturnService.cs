using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>ACC-5 (D-131 i): Kho › Trả hàng NCC – Thủ kho lập, Mua hàng xác nhận NCC, Thủ kho ghi sổ xuất trả.</summary>
    public interface ISupplierReturnService
    {
        Task<IResult<SupplierReturnListModel>> GetList(string? state, string? search, string? month);
        Task<IResult<SupplierReturnModel>> GetDetail(Guid id);
        Task<IResult<List<SupplierReturnLotModel>>> GetLots(string? search, Guid? supplierId);
        Task<IResult<SupplierReturnModel>> Create(SupplierReturnCreateRequest request);
        Task<IResult<SupplierReturnModel>> Confirm(SupplierReturnActionRequest request);
        Task<IResult<SupplierReturnModel>> Post(SupplierReturnActionRequest request);
        Task<IResult<SupplierReturnModel>> Cancel(SupplierReturnActionRequest request);
    }
}
