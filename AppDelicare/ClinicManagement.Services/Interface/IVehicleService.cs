using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IVehicleService
    {
        public Task<IResults<VehicleViewModel>> GetAll(VehicleSearchRequest request);
        public Task<IResult<bool>> Save(VehicleRequest request, string typeSave);
        public IResults<Vehicle> GetAllData();
        public Task<IResult<Vehicle>> GetById(Guid id);
    }
}
