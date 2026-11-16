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
    public interface IWarehouseService
    {
        public Task<IResults<WarehouseViewModel>> GetAll(WarehouseSearchRequest request);
        public Task<IResult<bool>> Save(WarehouseRequest request, string typeSave);
        public IResults<Warehouse> GetAllData();
        public Task<IResult<Warehouse>> GetById(Guid id);
        // <summary>T45R: sơ đồ kho.</summary>
        public Task<IResult<WarehouseLayoutModel>> GetLayout(Guid id);
    }
}
