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
	}
}
