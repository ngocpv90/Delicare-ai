using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
	public interface IUnitMeasureService
    {
		public Task<IResults<UnitMeasureViewModel>> GetAll(UnitMeasureSearchRequest request);
        public Task<IResult<bool>> Save(UnitMeasureRequest request, string typeSave);
        public IResults<UnitMeasure> GetAllData();
        public Task<IResult<UnitMeasure>> GetById(Guid id);
        /// <summary>UI-6.22 (D-103): đơn vị gốc / đóng gói + quy đổi một giao dịch; đơn vị quy đổi được cho nguyên liệu; danh sách kèm chỗ đang dùng.</summary>
        public Task<IResult<AppDelicare.Model.Model.UnitSavedModel>> SaveWithConversion(AppDelicare.Model.Model.UnitPackRequest request);
        public Task<IResult<AppDelicare.Model.Model.UnitChoiceListModel>> GetForIngredient(Guid? ingredientId, Guid? baseUnitId);
        public Task<IResults<AppDelicare.Model.Model.UnitUsageModel>> GetUsage();
	}
}
