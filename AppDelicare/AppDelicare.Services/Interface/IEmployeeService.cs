using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IEmployeeService
    {
        /// <summary>UI-4 (D-68 g1): hồ sơ nhân viên cho màn chi tiết – thông tin cá nhân chỉ khi người xem có quyền Sửa adm-employee.</summary>
        public Task<IResult<AppDelicare.Model.Model.EmployeeOverviewModel>> GetOverview(Guid id);
        public Task<IResults<EmployeeViewModel>> GetAll(EmployeeSearchRequest request);
        public Task<IResult<bool>> Save(EmployeeRequest request, string typeSave);
        public IResults<Employee> GetAllData();
        public Task<IResult<Employee>> GetById(Guid id);
        // UI-6.36 (D-117)
        public Task<IResult<AppDelicare.Model.Model.EmployeeListModel>> GetList(AppDelicare.Model.Model.EmployeeListRequest request);
        public Task<IResult<AppDelicare.Model.Model.EmployeePhoneCheckModel>> CheckPhone(string? phone, Guid? exceptId);
        public Task<IResult<AppDelicare.Model.Model.ResignPreviewModel>> PreviewResign(Guid id, DateTime? resignDate);
        public Task<IResult<AppDelicare.Model.Model.ResignPreviewModel>> Resign(AppDelicare.Model.Model.EmployeeResignRequest request);
        public Task<IResult<bool>> SetStatus(AppDelicare.Model.Model.EmployeeSetStatusRequest request);
        public Task<IResult<AppDelicare.Model.Model.EmployeeBulkResultModel>> RequestAccountMany(AppDelicare.Model.Model.EmployeeIdsRequest request);
        public Task<IResult<AppDelicare.Model.Model.EmployeeBulkResultModel>> CreateBotCodes(AppDelicare.Model.Model.EmployeeIdsRequest request);
        /// <summary>Job hằng ngày: hẹn nghỉ việc tới ngày → Đã nghỉ việc + khóa tài khoản (idempotent).</summary>
        public Task<int> ApplyScheduledResignationsAsync();
    }
}
