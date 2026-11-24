using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IIngredientCategoryService
    {
        public Task<IResults<IngredientCategoryViewModel>> GetAll(IngredientCategorySearchRequest request);
        public Task<IResult<bool>> Save(IngredientCategoryRequest request, string typeSave);
        public IResults<IngredientCategory> GetAllData();
        public Task<IResult<IngredientCategory>> GetById(Guid id);
        /// <summary>UI-6.21 (D-102): nhóm + số bản ghi đang dùng (cột nhóm bên trái).</summary>
        public Task<IResult<AppDelicare.Model.Model.CategoryCountListModel>> GetWithCount();
    }
}
