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
    public interface IUomConversionService
    {
        public Task<IResults<UomConversionViewModel>> GetAll(UomConversionSearchRequest request);
        public Task<IResult<bool>> Save(UomConversionRequest request, string typeSave);
        public IResults<UomConversion> GetAllData();
        public Task<IResult<UomConversion>> GetById(Guid id);
        // <summary>T45R: quy đổi (null nếu không có đường quy đổi).</summary>
        public Task<decimal?> ConvertAsync(decimal quantity, Guid fromUnitId, Guid toUnitId, DateTime? on = null);
        public Task<IResult<decimal>> ConvertQuantity(UomConvertRequest request);
    }
}
