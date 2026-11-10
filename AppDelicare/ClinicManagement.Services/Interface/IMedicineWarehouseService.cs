using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.Response;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using GemBox.Document;

namespace AppDelicare.Services.Interface
{
    public interface IMedicineWarehouseService
    {
        public Task<IResults<MedicineWarehouseViewModel>> GetAll(MedicineWarehouseSearchRequest request);
        public Task<IResult<bool>> Save(MedicineWarehouseRequest request, string typeSave);
        public IResults<MedicineWarehouse> GetAllData();
        public Task<IResult<MedicineWarehouse>> GetById(Guid id);
        public Task<IResults<MedicineWarehouseViewModel>> GetExpiry(MedicineWarehouseSearchRequest request);
        public Task<ExportToExcelResponse> DownloadExpiry(MedicineWarehouseSearchRequest request, DocumentModel document);
    }
}
