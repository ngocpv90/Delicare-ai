using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMenuAssignmentService
    {
        public Task<IResults<MenuAssignmentViewModel>> GetAll(MenuAssignmentSearchRequest request);
        public Task<IResult<bool>> Save(MenuAssignmentRequest request, string typeSave);
        public IResults<MenuAssignment> GetAllData();
        public Task<IResult<MenuAssignment>> GetById(Guid id);
        /// <summary>T13 (spec 6.3): thực đơn áp dụng cho (khách, điểm giao, loại suất, ngày) – cấp điểm giao trước, rồi cấp khách.</summary>
        public Task<IResult<AppDelicare.Model.Model.MenuResolveModel>> Resolve(AppDelicare.Model.Request.Custom.MenuResolveRequest request);
        /// <summary>T13: như Resolve, dùng nội bộ (OrderService); null = chưa gán.</summary>
        public Task<AppDelicare.Model.Model.MenuResolveModel?> ResolveAsync(Guid customerId, Guid? deliveryLocationId, string? mealType, DateTime date);
    }
}
