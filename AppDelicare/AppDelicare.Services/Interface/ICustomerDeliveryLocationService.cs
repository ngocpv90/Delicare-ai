using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface ICustomerDeliveryLocationService
    {
        public Task<IResults<CustomerDeliveryLocationViewModel>> GetAll(CustomerDeliveryLocationSearchRequest request);
        public Task<IResult<bool>> Save(CustomerDeliveryLocationRequest request, string typeSave);
        public IResults<CustomerDeliveryLocation> GetAllData();
        public Task<IResult<CustomerDeliveryLocation>> GetById(Guid id);
        /// <summary>UI-6 (6.2 nhóm A): trang chi tiết toàn màn điểm giao – khách, suất / giao đúng giờ 30 ngày, đơn / đơn giao gần đây, phản ánh.</summary>
        public Task<IResult<AppDelicare.Model.Model.DeliveryLocationPageModel>> GetOverview(Guid id);
    }
}
