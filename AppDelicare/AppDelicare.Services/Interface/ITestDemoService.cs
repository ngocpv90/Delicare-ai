using GemBox.Document;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.Response;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface ITestDemoService
    {
        public Task<IResults<TestDemoViewModel>> GetAll(TestDemoSearchRequest request);
        public Task<IResult<bool>> Save(TestDemoCustomRequest request, string typeSave);
        public IResults<TestDemo> GetAllData();
        public Task<IResult<TestDemoModel>> GetById(Guid id);
        public Task<ExportToExcelResponse> ExportToExcel(TestDemoSearchRequest request);
        public ExportToExcelResponse DownloadToWord(DocumentModel document);
    }
}
