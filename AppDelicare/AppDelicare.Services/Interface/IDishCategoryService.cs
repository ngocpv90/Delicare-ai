using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IDishCategoryService
    {
        public Task<IResults<DishCategoryViewModel>> GetAll(DishCategorySearchRequest request);
        public Task<IResult<bool>> Save(DishCategoryRequest request, string typeSave);
        public IResults<DishCategory> GetAllData();
        public Task<IResult<DishCategory>> GetById(Guid id);
        /// <summary>UI-6.21 (D-102): nhóm + số bản ghi đang dùng (cột nhóm bên trái).</summary>
        public Task<IResult<AppDelicare.Model.Model.CategoryCountListModel>> GetWithCount();
    }
}
