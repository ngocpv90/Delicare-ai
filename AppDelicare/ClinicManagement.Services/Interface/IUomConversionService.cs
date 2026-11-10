using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IUomConversionService
    {
        public Task<IResults<UomConversionViewModel>> GetAll(UomConversionSearchRequest request);
        public Task<IResult<bool>> Save(UomConversionRequest request, string typeSave);
        public IResults<UomConversion> GetAllData();
        public Task<IResult<UomConversion>> GetById(Guid id);
    }
}
