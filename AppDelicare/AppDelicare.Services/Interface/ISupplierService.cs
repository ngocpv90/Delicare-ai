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
    public interface ISupplierService
    {
        public Task<IResults<SupplierViewModel>> GetAll(SupplierSearchRequest request);
        public Task<IResult<bool>> Save(SupplierRequest request, string typeSave);
        public IResults<Supplier> GetAllData();
        public Task<IResult<Supplier>> GetById(Guid id);
        // <summary>T45R: tổng quan NCC.</summary>
        public Task<IResult<SupplierOverviewModel>> GetOverview(Guid id);
    }
}
