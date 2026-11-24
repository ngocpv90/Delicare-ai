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
    public interface IIngredientService
    {
        public Task<IResults<IngredientViewModel>> GetAll(IngredientSearchRequest request);
        public Task<IResult<bool>> Save(IngredientRequest request, string typeSave);
        public IResults<Ingredient> GetAllData();
        public Task<IResult<Ingredient>> GetById(Guid id);
        // <summary>T45R: tồn khả dụng + dưới mức tối thiểu.</summary>
        public Task<IResult<IngredientStockModel>> GetStock(Guid id);
        /// <summary>UI-6 (D-80): trang chi tiết toàn màn – tồn theo kho, lô, giá mua gần nhất, bảng giá NCC, lịch sử giá, giao dịch kho.</summary>
        public Task<IResult<IngredientOverviewModel>> GetOverview(Guid id);
        /// <summary>UI-6.20 (D-101): loại bảo quản + ngưỡng nhận hàng, quy đổi đơn vị, định mức theo kho.</summary>
        public Task<IResult<AppDelicare.Model.Model.IngredientFormOptionsModel>> GetFormOptions(Guid? id);
    }
}
