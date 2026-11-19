using AppDelicare.Data.Share;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using System.Text.RegularExpressions;

namespace AppDelicare.Services.Implement
{
    public partial class CustomerService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IClock clock, IDocumentNumberService documentNumberService, ICurrentActor currentActor) : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly ICurrentActor _currentActor = currentActor;

        /// <summary>MA-1.2: mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công (D-63 j).</summary>
        private string? _createdCode;

        public async Task<IResults<CustomerViewModel>> GetAll(CustomerSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CompanyId", request.CompanyId);
                dynamicParameters.Add("@Code", request.Code);
                dynamicParameters.Add("@Name", request.Name);
                dynamicParameters.Add("@TaxCode", request.TaxCode);
                dynamicParameters.Add("@Phone", request.Phone);
                dynamicParameters.Add("@Email", request.Email);
                //dynamicParameters.Add("@Address", request.Address);
                dynamicParameters.Add("@CustomerType", request.CustomerType);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<CustomerViewModel>("uspCustomer_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<CustomerViewModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.FirstOrDefault()?.TotalRow ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                throw;
            }
        }

        public IResults<Customer> GetAllData()
        {
            try
            {
                var list = _unitOfWork.CustomerRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Customer>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Save(CustomerRequest request, string typeSave)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var result = string.Empty;
                switch (typeSave)
                {
                    case ConstantHelper.TypeSave.Insert:
                        result = await Insert(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Update:
                        result = await Update(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Delete:
                        result = await Delete(request.Id, typeSave);
                        break;
                    default:
                        result = "Loại thao tác lưu dữ liệu không hợp lệ.";
                        break;
                }
                _unitOfWork.CommitTransaction();
                if (!string.IsNullOrEmpty(result))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = result,
                        Data = false
                    };
                }
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "khách hàng", _createdCode)
                        : ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// MA-1.2 (D-63 g–h): Code / CIF gửi lên bị bỏ qua – máy chủ sinh <c>KHB-NNNNN</c> / <c>KHC-NNNNN</c> theo loại, CIF = Code;
        /// Name / DisplayName / CompanyId do máy chủ gán; kiểm bắt buộc + định dạng theo loại.
        /// </summary>
        private async Task<string> Insert(CustomerRequest request, string typeSave)
        {
            var invalid = await CheckRules(request, null);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<Customer>(request);
            insert.Code = await _documentNumberService.NextCodeAsync(CodeKind(request.CustomerType));
            insert.CIF = insert.Code;
            insert.CompanyId = await CreatorCompanyAsync() ?? request.CompanyId;
            _unitOfWork.CustomerRepository.Add(insert);
            await _unitOfWork.CustomerRepository.CommitAsync(typeSave);
            _createdCode = insert.Code;
            return string.Empty;
        }

        private static DocumentKind CodeKind(string? type) => type == CustomerTypes.Individual ? DocumentKind.CustomerIndividual : DocumentKind.CustomerBusiness;

        /// <summary>D-63 h: công ty của người tạo = công ty của nhân viên gắn với tài khoản (GAP-HR-02); không tìm được → giá trị gửi lên.</summary>
        private async Task<Guid?> CreatorCompanyAsync()
        {
            if (_currentActor.Actor is not { } actor)
            {
                return null;
            }
            var employee = await HrRules.ResolveEmployeeAsync(_unitOfWork.DbContext, actor.UserId);
            return employee?.CompanyId;
        }

        /// <summary>
        /// MA-1.2: Code, CIF không đổi khi sửa (giá trị gửi lên bị bỏ qua). Đổi loại (D-63 i): khách chưa có hợp đồng / đơn → cấp mã mới theo loại mới
        /// (mã cũ nằm trong lịch sử AuditChange); đã có → báo lỗi. Giá trị loại cũ (HOSPITAL…) tính là Doanh nghiệp.
        /// </summary>
        private async Task<string> Update(CustomerRequest request, string typeSave)
        {
            var exist = request.Id == null ? null : _unitOfWork.CustomerRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var invalid = await CheckRules(request, exist);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            string oldType = CustomerTypes.Effective(exist.CustomerType);
            string? code = exist.Code, cif = exist.CIF;
            Guid? companyId = exist.CompanyId ?? request.CompanyId;
            if (request.CustomerType != oldType)
            {
                var db = _unitOfWork.DbContext;
                Guid id = exist.Id;
                if (await db.Contract.AnyAsync(x => x.CustomerId == id && !x.IsDeleted) || await db.Order.AnyAsync(x => x.CustomerId == id && !x.IsDeleted))
                {
                    return string.Format(ConstantHelper.Message.CustomerTypeLocked, exist.Code);
                }
                code = await _documentNumberService.NextCodeAsync(CodeKind(request.CustomerType));
                cif = code;
                Log.Information(string.Concat("[", GetType().Name, "]", " Update: ", string.Format(ConstantHelper.Message.CustomerTypeChanged, exist.Code, code)));
            }
            var update = _mapper.Map(request, exist);
            update.Code = code;
            update.CIF = cif;
            update.CompanyId = companyId;
            _unitOfWork.CustomerRepository.Update(update);
            await _unitOfWork.CustomerRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Customer), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.CustomerRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.CustomerRepository.Update(delete);
            await _unitOfWork.CustomerRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Customer>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.CustomerRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Customer>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Customer>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// MA-1.2 (D-63 f–h): chuẩn hóa + kiểm theo loại, sửa trực tiếp <paramref name="request"/>: xóa trống trường không thuộc loại,
        /// gán Name / DisplayName, điện thoại về dạng gọn. Kiểm hết ở máy chủ (không dựa vào giao diện). Trả thông báo lỗi đầu tiên.
        /// </summary>
        private async Task<string> CheckRules(CustomerRequest request, Customer? exist)
        {
            string? type = CustomerTypes.Normalize(request.CustomerType);
            if (type == null)
            {
                return ConstantHelper.Message.CustomerTypeInvalid;
            }
            request.CustomerType = type;
            request.Status = string.IsNullOrWhiteSpace(request.Status) ? (exist?.Status ?? "Active") : request.Status.Trim();
            if (request.Status is not ("Active" or "Inactive"))
            {
                return ConstantHelper.Message.CustomerStatusInvalid;
            }
            // Cột Phone cũ của request (không có trong bảng) → PhoneNumber.
            request.PhoneNumber = CleanPhone(Clean(request.PhoneNumber) ?? Clean(request.Phone));
            request.Phone = null;
            request.ContactPersonPhone = CleanPhone(Clean(request.ContactPersonPhone));
            request.Email = Clean(request.Email);
            request.ContactPersonEmail = Clean(request.ContactPersonEmail);
            request.TaxCode = Clean(request.TaxCode);
            request.IdentityNumber = Clean(request.IdentityNumber);
            request.Address = Clean(request.Address);
            string title = CustomerTypes.Title(type);
            var missing = new List<string>();
            void Need(string? value, string label) { if (string.IsNullOrWhiteSpace(value)) missing.Add(label); }
            DateTime today = clock.Today;
            // D-63 g: chi nhánh phục vụ bắt buộc – chỉ khi hệ thống đã khai báo chi nhánh (OrganizationUnit.UnitType = BRANCH).
            // Chưa có chi nhánh nào (1 pháp nhân, 1 bếp) → không bắt buộc (PROPOSED – BA/PO CONFIRM).
            bool branchRequired = await _unitOfWork.DbContext.OrganizationUnit.AsNoTracking()
                .AnyAsync(x => !x.IsDeleted && x.UnitType == OrganizationUnitService.BranchType);

            if (type == CustomerTypes.Business)
            {
                // Trường của cá nhân → xóa trống.
                request.FirstName = request.LastName = request.Gender = request.IdentityNumber = request.IdentityPlace = request.Occupation = null;
                request.DateOfBirth = request.IdentityDate = null;
                request.LegalName = Clean(request.LegalName);
                request.DisplayName = Clean(request.DisplayName);
                request.LegalRepresentativeName = Clean(request.LegalRepresentativeName);
                request.ContactPersonName = Clean(request.ContactPersonName);
                Need(request.LegalName, "Tên doanh nghiệp");
                Need(request.DisplayName, "Tên giao dịch");
                Need(request.TaxCode, "Mã số thuế");
                Need(request.LegalRepresentativeName, "Người đại diện pháp luật");
                Need(request.ContactPersonName, "Người liên hệ");
                Need(request.ContactPersonPhone, "SĐT người liên hệ");
                Need(request.Address, "Địa chỉ");
                Need(request.PhoneNumber, "Điện thoại");
                if (request.BranchId == null && branchRequired) missing.Add("Chi nhánh phục vụ");
                if (missing.Count > 0)
                {
                    return string.Format(ConstantHelper.Message.CustomerFieldRequired, title.ToLowerInvariant(), string.Join(", ", missing));
                }
                if (!BusinessTaxCode().IsMatch(request.TaxCode!))
                {
                    return string.Format(ConstantHelper.Message.CustomerTaxCodeInvalid, request.TaxCode);
                }
                request.BusinessRegistrationNumber = Clean(request.BusinessRegistrationNumber) ?? request.TaxCode;
                if (request.RegistrationDate?.Date > today)
                {
                    return string.Format(ConstantHelper.Message.CustomerDateInFuture, "Ngày cấp ĐKKD");
                }
                string tax = request.TaxCode!;
                var dup = await _unitOfWork.CustomerRepository.GetAll().AsNoTracking()
                    .Where(x => x.TaxCode == tax && x.Id != request.Id && !x.IsDeleted && x.CustomerType != CustomerTypes.Individual)
                    .Select(x => x.Code).FirstOrDefaultAsync();
                if (dup != null)
                {
                    return string.Format(ConstantHelper.Message.CustomerTaxCodeExist, tax, dup);
                }
                request.Name = request.LegalName;
            }
            else
            {
                // Trường của doanh nghiệp → xóa trống.
                request.LegalName = request.BusinessRegistrationNumber = request.RegistrationPlace = request.LegalRepresentativeName = null;
                request.ContactPersonName = request.ContactPersonPhone = request.ContactPersonEmail = request.Industry = request.CompanySize = request.Segment = null;
                request.RegistrationDate = null;
                request.LastName = Clean(request.LastName);
                request.FirstName = Clean(request.FirstName);
                Need(request.LastName, "Họ");
                Need(request.FirstName, "Tên");
                Need(request.Address, "Địa chỉ");
                Need(request.PhoneNumber, "Điện thoại");
                if (request.BranchId == null && branchRequired) missing.Add("Chi nhánh phục vụ");
                if (missing.Count > 0)
                {
                    return string.Format(ConstantHelper.Message.CustomerFieldRequired, title.ToLowerInvariant(), string.Join(", ", missing));
                }
                if (request.DateOfBirth?.Date > today)
                {
                    return string.Format(ConstantHelper.Message.CustomerDateInFuture, "Ngày sinh");
                }
                if (request.IdentityDate is { } idDate && (idDate.Date > today || (request.DateOfBirth is { } dob && idDate.Date < dob.Date)))
                {
                    return ConstantHelper.Message.CustomerIdentityDateInvalid;
                }
                if (request.TaxCode != null && !PersonalTaxCode().IsMatch(request.TaxCode))
                {
                    return string.Format(ConstantHelper.Message.CustomerPersonalTaxCodeInvalid, request.TaxCode);
                }
                if (request.IdentityNumber != null)
                {
                    if (!IdentityNumber().IsMatch(request.IdentityNumber))
                    {
                        return string.Format(ConstantHelper.Message.CustomerIdentityInvalid, request.IdentityNumber);
                    }
                    string idNo = request.IdentityNumber;
                    var dup = await _unitOfWork.CustomerRepository.GetAll().AsNoTracking()
                        .Where(x => x.IdentityNumber == idNo && x.Id != request.Id && !x.IsDeleted && x.CustomerType == CustomerTypes.Individual)
                        .Select(x => x.Code).FirstOrDefaultAsync();
                    if (dup != null)
                    {
                        return string.Format(ConstantHelper.Message.CustomerIdentityExist, idNo, dup);
                    }
                }
                request.Name = request.LastName + " " + request.FirstName;
                request.DisplayName = request.Name;
            }

            foreach (var (label, phone) in new[] { ("Điện thoại", request.PhoneNumber), ("SĐT người liên hệ", request.ContactPersonPhone) })
            {
                if (phone != null && !Phone().IsMatch(phone))
                {
                    return string.Format(ConstantHelper.Message.CustomerPhoneInvalid, label, phone);
                }
            }
            foreach (var (label, email) in new[] { ("Email", request.Email), ("Email người liên hệ", request.ContactPersonEmail) })
            {
                if (email != null && !Email().IsMatch(email))
                {
                    return string.Format(ConstantHelper.Message.CustomerEmailInvalid, label, email);
                }
            }
            if (request.BranchId is not { } branchId)
            {
                return string.Empty;
            }
            if (!await _unitOfWork.DbContext.OrganizationUnit.AsNoTracking().AnyAsync(x => x.Id == branchId && !x.IsDeleted))
            {
                return ConstantHelper.Message.CustomerBranchNotFound;
            }
            return string.Empty;
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>Bỏ khoảng trắng, dấu chấm, gạch, ngoặc trong số điện thoại (0912.345.678 → 0912345678).</summary>
        private static string? CleanPhone(string? phone) => phone == null ? null : PhoneSeparators().Replace(phone, string.Empty);

        [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
        private static partial Regex BusinessTaxCode();
        [GeneratedRegex(@"^\d{10}$")]
        private static partial Regex PersonalTaxCode();
        [GeneratedRegex(@"^\d{12}$")]
        private static partial Regex IdentityNumber();
        [GeneratedRegex(@"^(0\d{9}|\+84\d{9})$")]
        private static partial Regex Phone();
        [GeneratedRegex(@"[\s.\-()]")]
        private static partial Regex PhoneSeparators();
        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
        private static partial Regex Email();

        /// <summary>Đơn đã giao xong – dùng tính số suất trung bình 7 ngày.</summary>
        private static readonly string[] CompletedOrderStatuses = ["Delivered", "Reconciled"];

        /// <summary>T45R (spec 4.1): hồ sơ khách hàng 360° – liên hệ, điểm giao, hợp đồng + suất, 15 đơn gần nhất, KPI.</summary>
        public async Task<IResult<CustomerOverviewModel>> GetOverview(Guid id)
        {
            try
            {
                var customer = await _unitOfWork.CustomerRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (customer == null)
                {
                    return Result<CustomerOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var db = _unitOfWork.DbContext;
                var contracts = await db.Contract.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).OrderByDescending(x => x.StartDate).ToListAsync();
                var contractIds = contracts.Select(x => x.Id).ToList();
                var meals = await db.ContractMeal.AsNoTracking().Where(m => !m.IsDeleted && contractIds.Contains(m.ContractId)).ToListAsync();
                var orders = db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.CustomerId == id);
                DateTime since = clock.Today /* Rà soát BE: giờ nghiệp vụ VN */.AddDays(-7);
                decimal meals7d = await orders.Where(o => o.MealDate >= since && CompletedOrderStatuses.Contains(o.Status!)).SumAsync(o => o.TotalQuantity) ?? 0m;
                var model = new CustomerOverviewModel
                {
                    Customer = customer,
                    ListContact = await db.CustomerContact.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).ToListAsync(),
                    ListLocation = await db.CustomerDeliveryLocation.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).ToListAsync(),
                    ListContract = contracts.Select(x => new ContractOverviewModel
                    {
                        Id = x.Id, ContractNumber = x.ContractNumber, Name = x.Name, StartDate = x.StartDate, EndDate = x.EndDate, Status = x.Status,
                        ContractValue = x.ContractValue, ListMeal = meals.Where(m => m.ContractId == x.Id).ToList(),
                    }).ToList(),
                    ListRecentOrder = await orders.OrderByDescending(o => o.MealDate).Take(15).Select(o => new OrderBriefModel
                    {
                        Id = o.Id, OrderNumber = o.OrderNumber, MealDate = o.MealDate, DeliveryDate = o.DeliveryDate,
                        TotalQuantity = o.TotalQuantity, TotalAmount = o.TotalAmount, Status = o.Status,
                    }).ToListAsync(),
                    OrderCount = await orders.CountAsync(),
                    ReconciledRevenue = await (from r in db.OrderReconciliation.AsNoTracking()
                                               join o in db.Order.AsNoTracking() on r.OrderId equals o.Id
                                               where !r.IsDeleted && !o.IsDeleted && o.CustomerId == id
                                               select r.FinalAmount).SumAsync() ?? 0m,
                    ComplaintCount = await db.CustomerComplaint.AsNoTracking().CountAsync(x => !x.IsDeleted && x.CustomerId == id),
                    AvgMealsPerDay7d = Math.Round(meals7d / 7m, 1),
                };
                return Result<CustomerOverviewModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }
    }
}
