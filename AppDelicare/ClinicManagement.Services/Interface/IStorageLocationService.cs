using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IStorageLocationService
    {
        public Task<IResults<StorageLocationViewModel>> GetAll(StorageLocationSearchRequest request);
        public Task<IResult<bool>> Save(StorageLocationRequest request, string typeSave);
        public IResults<StorageLocation> GetAllData();
        public Task<IResult<StorageLocation>> GetById(Guid id);
    }
}
