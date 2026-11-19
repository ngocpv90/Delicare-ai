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
        /// <summary>UI-3 (D-67 f2): giá trị PO / đã nhận, % đúng hạn, % bị loại theo tháng (mặc định 6 tháng gần nhất).</summary>
        public Task<IResult<SupplierTrendModel>> GetTrend(Guid id, DateTime? from, DateTime? to);
    }
}
