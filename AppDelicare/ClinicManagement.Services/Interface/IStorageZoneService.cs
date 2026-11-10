using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IStorageZoneService
    {
        public Task<IResults<StorageZoneViewModel>> GetAll(StorageZoneSearchRequest request);
        public Task<IResult<bool>> Save(StorageZoneRequest request, string typeSave);
        public IResults<StorageZone> GetAllData();
        public Task<IResult<StorageZone>> GetById(Guid id);
    }
}
