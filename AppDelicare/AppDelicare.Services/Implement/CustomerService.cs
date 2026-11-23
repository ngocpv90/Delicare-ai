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
using AppDelicare.Model.Option;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement
{
    public partial class CustomerService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IClock clock, IDocumentNumberService documentNumberService, ICurrentActor currentActor
        , IPackagingLedgerService packagingLedgerService, IOptions<BusinessRulesOptions> rules, IPermissionService permissionService) : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        /// <summary>MA-1.2: mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công (D-63 j).</summary>
        private string? _createdCode;
        /// <summary>UI-5.8: bản ghi tạo kèm khách mới (liên hệ chính, điểm giao) – nối vào thông báo thành công.</summary>
        private readonly List<string> _createdExtras = [];

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
                        : _createdCode != null ? string.Join(" · ", new[] { string.Format(ConstantHelper.Message.SaveSuccessWithCode, "khách hàng", _createdCode) }.Concat(_createdExtras))
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
            // UI-5.8: kiểm điểm giao đầu tiên TRƯỚC khi ghi – lỗi → không tạo khách (cùng transaction của Save).
            var first = request.FirstLocation is { IsEmpty: false } fl ? fl : null;
            if (first != null)
            {
                invalid = CheckFirstLocation(first);
                if (!string.IsNullOrEmpty(invalid))
                {
                    return invalid;
                }
            }
            var insert = _mapper.Map<Customer>(request);
            insert.Code = await _documentNumberService.NextCodeAsync(CodeKind(request.CustomerType));
            insert.CIF = insert.Code;
            insert.CompanyId = await CreatorCompanyAsync() ?? request.CompanyId;
            _unitOfWork.CustomerRepository.Add(insert);
            await _unitOfWork.CustomerRepository.CommitAsync(typeSave);
            _createdCode = insert.Code;

            // UI-5.8 (D-63 r): khách doanh nghiệp → người liên hệ chính từ Người liên hệ + SĐT + email người liên hệ.
            if (request.CustomerType == CustomerTypes.Business && request.ContactPersonName != null)
            {
                _unitOfWork.CustomerContactRepository.Add(new CustomerContact
                {
                    CustomerId = insert.Id, Name = request.ContactPersonName, FullName = request.ContactPersonName,
                    Phone = request.ContactPersonPhone, Email = request.ContactPersonEmail, IsPrimary = true, Status = "Active",
                });
                await _unitOfWork.CustomerContactRepository.CommitAsync(typeSave);
                _createdExtras.Add(ConstantHelper.Message.CustomerCreatedPrimaryContact);
            }
            if (first != null)
            {
                var location = new CustomerDeliveryLocation
                {
                    CustomerId = insert.Id, Code = await _documentNumberService.NextCodeAsync(DocumentKind.DeliveryLocation),
                    Name = first.Name!.Trim(), Address = first.Address!.Trim(), DeliveryWindowFrom = first.DeliveryWindowFrom, DeliveryWindowTo = first.DeliveryWindowTo,
                    ContactName = request.ContactPersonName ?? request.Name, ContactPhone = request.ContactPersonPhone ?? request.PhoneNumber, Status = "Active",
                };
                _unitOfWork.CustomerDeliveryLocationRepository.Add(location);
                await _unitOfWork.CustomerDeliveryLocationRepository.CommitAsync(typeSave);
                _createdExtras.Add(string.Format(ConstantHelper.Message.CustomerCreatedLocation, location.Code));
            }
            return string.Empty;
        }

        /// <summary>UI-5.8: điểm giao đầu tiên cần tên + địa chỉ; giờ nhận từ &lt; đến khi nhập đủ hai đầu.</summary>
        private static string CheckFirstLocation(CustomerFirstLocationRequest first)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(first.Name)) missing.Add("tên điểm giao");
            if (string.IsNullOrWhiteSpace(first.Address)) missing.Add("địa chỉ");
            if (missing.Count > 0)
            {
                return string.Format(ConstantHelper.Message.CustomerFirstLocationRequired, string.Join(", ", missing));
            }
            if (first.DeliveryWindowFrom is { } from && first.DeliveryWindowTo is { } to && from >= to)
            {
                return ConstantHelper.Message.CustomerFirstLocationWindowInvalid;
            }
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
                request.ContactPersonName = request.ContactPersonPhone = request.ContactPersonEmail = request.Industry = request.CompanySize = null;
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

            // UI-5.3 (D-63 r): Ngành / Quy mô / Giới tính / Nguồn chỉ nhận mã trong danh mục (giá trị cũ ngoài danh mục giữ được khi không sửa).
            string? catalogError;
            (request.Industry, catalogError) = CustomerCatalogs.Check("Industry", request.Industry, exist?.Industry);
            if (catalogError != null) return catalogError;
            (request.CompanySize, catalogError) = CustomerCatalogs.Check("CompanySize", request.CompanySize, exist?.CompanySize);
            if (catalogError != null) return catalogError;
            (request.Gender, catalogError) = CustomerCatalogs.Check("Gender", request.Gender, exist?.Gender);
            if (catalogError != null) return catalogError;
            (request.Source, catalogError) = CustomerCatalogs.Check("Source", request.Source, exist?.Source);
            if (catalogError != null) return catalogError;

            // UI-5.1: NV phụ trách = User.Id của tài khoản đang hoạt động (giữ nguyên người cũ nếu không đổi).
            if (request.UserAssignId is { } assignee && assignee != exist?.UserAssignId
                && !await _unitOfWork.DbContext.User.AsNoTracking().AnyAsync(x => x.Id == assignee && !x.IsDeleted && x.IsActive))
            {
                return ConstantHelper.Message.CustomerAssigneeNotFound;
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

        /// <summary>
        /// UI-2 (D-66 b, d, f1): tên chi nhánh / NV phụ trách, hạn hợp đồng gần nhất, phản ánh quá hạn, khay ở khách + thất thoát, khối "Cần chú ý".
        /// Ngưỡng: hợp đồng còn ≤ 30 ngày; thất thoát khay theo ngưỡng sổ bao bì; chưa chốt đơn ngày mai từ (giờ chốt – 1) giờ (mặc định 19:00).
        /// </summary>
        private async Task FillProfileAsync(CustomerOverviewModel model, Customer customer, List<Contract> contracts,
            List<(string? Status, DateTime? SlaDueAt)> complaints, DateTime today, DateTime now)
        {
            var db = _unitOfWork.DbContext;
            if (customer.BranchId is { } branchId)
            {
                model.BranchName = await db.OrganizationUnit.AsNoTracking().Where(x => x.Id == branchId).Select(x => x.Name).FirstOrDefaultAsync();
            }
            if (customer.UserAssignId is { } userId)
            {
                var user = await db.User.AsNoTracking().Where(x => x.Id == userId).Select(x => new { x.UserName, x.PersonId }).FirstOrDefaultAsync();
                string? person = user?.PersonId is { } pid ? await db.Person.AsNoTracking().Where(p => p.Id == pid).Select(p => p.FullName).FirstOrDefaultAsync() : null;
                model.UserAssignName = person ?? user?.UserName;
            }
            var active = contracts.Where(c => IsActiveContract(c, today)).ToList();
            model.NearestContractEndDate = active.Where(c => c.EffectiveTo != null).Select(c => c.EffectiveTo!.Value.Date).OrderBy(d => d).Cast<DateTime?>().FirstOrDefault();
            model.DaysToContractEnd = model.NearestContractEndDate is { } end ? (int)(end - today).TotalDays : null;
            model.OverdueComplaintCount = complaints.Count(c => !ComplaintStatus.Done.Contains(c.Status) && c.SlaDueAt != null && c.SlaDueAt < now);

            var balance = await packagingLedgerService.GetBalance(new PackagingBalanceRequest { CustomerId = customer.Id });
            var items = balance.Data?.ListItem ?? [];
            model.ContainersAtCustomer = items.Sum(i => i.Outstanding);
            model.PackagingLossRatePct = items.Where(i => i.LossRatePct != null).Select(i => i.LossRatePct).DefaultIfEmpty(null).Max();
            model.PackagingLossThresholdPct = items.Select(i => i.LossThresholdPct).DefaultIfEmpty(0m).Max();

            var alerts = model.ListAlert;
            foreach (var c in active.Where(c => c.EffectiveTo != null && (c.EffectiveTo.Value.Date - today).TotalDays <= 30).OrderBy(c => c.EffectiveTo))
            {
                int days = (int)(c.EffectiveTo!.Value.Date - today).TotalDays;
                alerts.Add(new CustomerAlertModel
                {
                    Code = "CONTRACT_EXPIRING", Severity = days <= 7 ? "HIGH" : "MEDIUM", Screen = "crm-contract",
                    Message = string.Format(ConstantHelper.Message.CustomerAlertContractExpiring, c.ContractNumber ?? c.ContractNo, days),
                });
            }
            if (model.OverdueComplaintCount > 0)
            {
                alerts.Add(new CustomerAlertModel { Code = "COMPLAINT_OVERDUE", Severity = "HIGH", Screen = "log-complaint",
                    Message = string.Format(ConstantHelper.Message.CustomerAlertComplaintOverdue, model.OverdueComplaintCount) });
            }
            if (items.Any(i => i.IsOverThreshold))
            {
                alerts.Add(new CustomerAlertModel { Code = "PACKAGING_LOSS", Severity = "MEDIUM", Screen = "log-container-return",
                    Message = string.Format(ConstantHelper.Message.CustomerAlertPackagingLoss, model.PackagingLossRatePct, model.PackagingLossThresholdPct) });
            }
            if (active.Count > 0 && now.Hour >= Math.Max(0, _rules.OrderCutoffHourPreviousDay - 1))
            {
                DateTime tomorrow = today.AddDays(1);
                var tomorrowOrders = await db.Order.AsNoTracking()
                    .Where(o => !o.IsDeleted && o.CustomerId == customer.Id && o.MealDate >= tomorrow && o.MealDate < tomorrow.AddDays(1) && o.Status != OrderStatus.Cancelled)
                    .Select(o => o.Status).ToListAsync();
                if (tomorrowOrders.Count == 0 || tomorrowOrders.Any(s => s is OrderStatus.Draft or OrderStatus.Submitted))
                {
                    alerts.Add(new CustomerAlertModel { Code = "ORDER_NOT_LOCKED", Severity = "HIGH", Screen = "crm-order",
                        Message = string.Format(tomorrowOrders.Count == 0 ? ConstantHelper.Message.CustomerAlertNoOrderTomorrow : ConstantHelper.Message.CustomerAlertOrderNotConfirmed,
                            tomorrow.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture), _rules.OrderCutoffHourPreviousDay) });
                }
            }
            if (model.ListLocation.Count == 0)
            {
                alerts.Add(new CustomerAlertModel { Code = "NO_DELIVERY_LOCATION", Severity = "MEDIUM", Screen = "crm-location", Message = ConstantHelper.Message.CustomerAlertNoLocation });
            }
            if (!model.ListContact.Any(x => x.IsPrimary == true))
            {
                alerts.Add(new CustomerAlertModel { Code = "NO_PRIMARY_CONTACT", Severity = "LOW", Screen = "crm-contact", Message = ConstantHelper.Message.CustomerAlertNoPrimaryContact });
            }
        }

        /// <summary>UI-2 (D-66 f2): suất đặt / giao / nhận / phục vụ theo ngày ăn + cơ cấu loại suất.</summary>
        public async Task<IResult<CustomerTrendModel>> GetTrend(Guid id, DateTime? from, DateTime? to)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                if (!await db.Customer.AsNoTracking().AnyAsync(x => x.Id == id && !x.IsDeleted))
                {
                    return Result<CustomerTrendModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                DateTime end = (to ?? clock.Today).Date, start = (from ?? end.AddDays(-29)).Date;
                if (start > end || (end - start).TotalDays > 366)
                {
                    return Result<CustomerTrendModel>.Fail(ConstantHelper.Message.CustomerTrendRangeInvalid);
                }
                DateTime endExcl = end.AddDays(1);
                var orders = await db.Order.AsNoTracking()
                    .Where(o => !o.IsDeleted && o.CustomerId == id && o.MealDate >= start && o.MealDate < endExcl && o.Status != OrderStatus.Cancelled)
                    .Select(o => new { o.Id, o.MealDate }).ToListAsync();
                var orderIds = orders.Select(o => o.Id).ToList();
                var dayOf = orders.ToDictionary(o => o.Id, o => o.MealDate.Date);
                var lines = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && orderIds.Contains(l.OrderId))
                    .Select(l => new { l.OrderId, l.MealType, l.Quantity }).ToListAsync();
                var pods = await (from p in db.ProofOfDelivery.AsNoTracking()
                                  join d in db.DeliveryOrder.AsNoTracking() on p.DeliveryOrderId equals d.Id
                                  where !p.IsDeleted && !d.IsDeleted && orderIds.Contains(d.OrderId)
                                  select new { d.OrderId, Delivered = p.DeliveredQuantity ?? p.DeliveredQty ?? 0m, Accepted = p.AcceptedQuantity ?? p.AcceptedQty ?? 0m }).ToListAsync();
                var recs = await db.OrderReconciliation.AsNoTracking().Where(r => !r.IsDeleted && orderIds.Contains(r.OrderId))
                    .Select(r => new { r.OrderId, r.AcceptedQuantity }).ToListAsync();
                var model = new CustomerTrendModel { From = start, To = end };
                for (DateTime d = start; d <= end; d = d.AddDays(1))
                {
                    var ids = dayOf.Where(x => x.Value == d).Select(x => x.Key).ToHashSet();
                    model.ListDay.Add(new CustomerTrendDayModel
                    {
                        Date = d,
                        Ordered = lines.Where(l => ids.Contains(l.OrderId)).Sum(l => l.Quantity),
                        Delivered = pods.Where(p => ids.Contains(p.OrderId)).Sum(p => p.Delivered),
                        Accepted = pods.Where(p => ids.Contains(p.OrderId)).Sum(p => p.Accepted),
                        Served = recs.Where(r => ids.Contains(r.OrderId)).Sum(r => r.AcceptedQuantity),
                    });
                }
                model.ListMealType = lines.GroupBy(l => string.IsNullOrWhiteSpace(l.MealType) ? "(khác)" : l.MealType!)
                    .Select(g => new CustomerTrendMealTypeModel { MealType = g.Key, Quantity = g.Sum(x => x.Quantity) })
                    .OrderByDescending(x => x.Quantity).ToList();
                return Result<CustomerTrendModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTrend: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Hợp đồng hiệu lực tại ngày: Active và trong khoảng hiệu lực.</summary>
        private static bool IsActiveContract(Contract c, DateTime day)
            => c.Status == ContractStatus.Active && c.EffectiveFrom.Date <= day && (c.EffectiveTo == null || c.EffectiveTo.Value.Date >= day);

        /// <summary>UI-1.2 (D-64): 4 thẻ của danh sách khách hàng – lọc giống danh sách (chi nhánh, loại, tìm theo mã / tên / SĐT / MST).</summary>
        public async Task<IResult<CustomerSummaryModel>> GetSummary(Guid? branchId, string? customerType, string? searchText)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var q = db.Customer.AsNoTracking().Where(x => !x.IsDeleted);
                if (branchId is { } b && b != Guid.Empty)
                {
                    q = q.Where(x => x.BranchId == b);
                }
                if (!string.IsNullOrWhiteSpace(customerType))
                {
                    string t = customerType.Trim();
                    q = q.Where(x => x.CustomerType == t);
                }
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    string s = searchText.Trim();
                    q = q.Where(x => x.Code!.Contains(s) || x.Name!.Contains(s) || x.DisplayName!.Contains(s) || x.PhoneNumber!.Contains(s) || x.TaxCode!.Contains(s));
                }
                var rows = await q.Select(x => new { x.Id, x.Status }).ToListAsync();
                var idList = rows.Select(x => x.Id).ToList();
                DateTime today = clock.Today, since = today.AddDays(-7);
                var contracts = await db.Contract.AsNoTracking().Where(c => !c.IsDeleted && idList.Contains(c.CustomerId) && c.Status == ContractStatus.Active).ToListAsync();
                decimal meals = await db.Order.AsNoTracking()
                    .Where(o => !o.IsDeleted && idList.Contains(o.CustomerId) && o.MealDate >= since && o.MealDate < today && CompletedOrderStatuses.Contains(o.Status!))
                    .SumAsync(o => o.TotalQuantity) ?? 0m;
                return Result<CustomerSummaryModel>.Success(new CustomerSummaryModel
                {
                    TotalCount = rows.Count,
                    ActiveCount = rows.Count(x => x.Status == "Active"),
                    ActiveContractCount = contracts.Count(c => IsActiveContract(c, today)),
                    AvgMealsPerDay7d = Math.Round(meals / 7m, 1),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSummary: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>UI-5 (D-63 r): chi nhánh (chỉ loại BRANCH), NV phụ trách (tài khoản đang hoạt động, tên từ Person), danh mục, cờ chi nhánh bắt buộc.</summary>
        public async Task<IResult<CustomerFormOptionsModel>> GetFormOptions()
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var branches = await db.OrganizationUnit.AsNoTracking()
                    .Where(x => !x.IsDeleted && x.UnitType == OrganizationUnitService.BranchType)
                    .OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync();
                var users = await db.User.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive)
                    .Select(x => new { x.Id, x.UserName, x.PersonId }).ToListAsync();
                var personIds = users.Where(x => x.PersonId != null).Select(x => x.PersonId!.Value).Distinct().ToList();
                var names = await db.Person.AsNoTracking().Where(p => personIds.Contains(p.Id)).Select(p => new { p.Id, p.FullName }).ToDictionaryAsync(p => p.Id, p => p.FullName);
                static List<CustomerFormOptionItem> Items(IEnumerable<CatalogItem> list) => [.. list.Select(x => new CustomerFormOptionItem { Code = x.Code, Name = x.Name })];
                var model = new CustomerFormOptionsModel
                {
                    BranchRequired = branches.Count > 0,
                    ListBranch = [.. branches.Select(x => new CustomerFormOptionItem { Code = x.Id.ToString(), Name = x.Name ?? x.Code ?? string.Empty, Hint = x.Code })],
                    ListAssignee = [.. users
                        .Select(x => new CustomerFormOptionItem
                        {
                            Code = x.Id.ToString(),
                            Name = x.PersonId is { } pid && names.TryGetValue(pid, out var n) && !string.IsNullOrWhiteSpace(n) ? n! : x.UserName ?? string.Empty,
                            Hint = x.UserName,
                        })
                        .OrderBy(x => x.Name, StringComparer.CurrentCulture)],
                    Catalogs = new Dictionary<string, List<CustomerFormOptionItem>>
                    {
                        ["Industry"] = Items(CustomerCatalogs.Industry),
                        ["CompanySize"] = Items(CustomerCatalogs.CompanySize),
                        ["Gender"] = Items(CustomerCatalogs.Gender),
                        ["Source"] = Items(CustomerCatalogs.Source),
                        ["Status"] = Items(CustomerCatalogs.Status),
                    },
                };
                return Result<CustomerFormOptionsModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetFormOptions: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// UI-2R (D-69 c–d): quyền xem thông tin cá nhân (ngày sinh, CCCD + ngày / nơi cấp của khách cá nhân – chỉ người có quyền Sửa crm-customer;
        /// lọc ở máy chủ), tên người tạo / cập nhật, suất / ngày theo điểm giao (30 ngày đã qua).
        /// </summary>
        private async Task FillInfoPanelAsync(CustomerOverviewModel model, Customer customer, IQueryable<Order> orders, DateTime today)
        {
            var db = _unitOfWork.DbContext;
            model.CanViewPersonal = _currentActor.Actor is not { } actor
                || permissionService.IsAllowed(await permissionService.GetAsync(actor.UserId), "crm-customer", PermissionAction.Edit);
            if (!model.CanViewPersonal)
            {
                // Bản sao tách khỏi DbContext – không đụng bản ghi đang theo dõi.
                db.Entry(customer).State = EntityState.Detached;
                customer.DateOfBirth = null;
                customer.IdentityNumber = null;
                customer.IdentityDate = null;
                customer.IdentityPlace = null;
            }
            var names = await InfoPanelRules.UserNamesAsync(db, [customer.CreatedBy, customer.ModifiedBy]);
            model.CreatedByName = names.NameOf(customer.CreatedBy);
            model.ModifiedByName = names.NameOf(customer.ModifiedBy);
            DateTime since = today.AddDays(-30);
            var perLocation = await orders.Where(o => o.DeliveryLocationId != null && o.MealDate >= since && o.MealDate < today && CompletedOrderStatuses.Contains(o.Status!))
                .GroupBy(o => o.DeliveryLocationId!.Value).Select(g => new { g.Key, Total = g.Sum(o => o.TotalQuantity) ?? 0m }).ToListAsync();
            model.ListLocationStat = [.. perLocation.Select(x => new CustomerLocationStatModel { LocationId = x.Key, AvgMealsPerDay = Math.Round(x.Total / 30m, 0) })];
        }

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
                // UI-1: 7 ngày ĐÃ QUA (không gồm hôm nay / ngày tương lai) – khớp Customer/GetSummary.
                DateTime until = clock.Today;
                decimal meals7d = await orders.Where(o => o.MealDate >= since && o.MealDate < until && CompletedOrderStatuses.Contains(o.Status!)).SumAsync(o => o.TotalQuantity) ?? 0m;
                var model = new CustomerOverviewModel
                {
                    Customer = customer,
                    ListContact = await db.CustomerContact.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).ToListAsync(),
                    ListLocation = await db.CustomerDeliveryLocation.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).ToListAsync(),
                    ListContract = contracts.Select(x => new ContractOverviewModel
                    {
                        Id = x.Id, ContractNumber = x.ContractNumber, Name = x.Name, StartDate = x.StartDate, EndDate = x.EndDate, Status = x.Status,
                        ContractValue = x.ContractValue, PaymentTerm = x.PaymentTerm, ListMeal = meals.Where(m => m.ContractId == x.Id).ToList(),
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
                // UI-1.3 (D-64): thẻ chỉ số hồ sơ 360.
                DateTime today = clock.Today, now = clock.Now;
                model.ActiveContractCount = contracts.Count(c => IsActiveContract(c, today));
                var complaints = await db.CustomerComplaint.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id)
                    .Select(x => new { x.ComplaintStatus, x.SlaDueAt, x.ResolvedAt }).ToListAsync();
                model.OpenComplaintCount = complaints.Count(c => !ComplaintStatus.Done.Contains(c.ComplaintStatus));
                var withSla = complaints.Where(c => c.SlaDueAt != null).ToList();
                model.ComplaintOnTimePct = withSla.Count == 0 ? null : Math.Round(100m * withSla.Count(c =>
                    ComplaintStatus.Done.Contains(c.ComplaintStatus) ? c.ResolvedAt == null || c.ResolvedAt <= c.SlaDueAt : c.SlaDueAt >= now) / withSla.Count, 1);
                model.ReconciledOrderCount = await (from r in db.OrderReconciliation.AsNoTracking()
                                                    join o in db.Order.AsNoTracking() on r.OrderId equals o.Id
                                                    where !r.IsDeleted && !o.IsDeleted && o.CustomerId == id
                                                    select r.OrderId).Distinct().CountAsync();
                await FillProfileAsync(model, customer, contracts, complaints.Select(c => (c.ComplaintStatus, c.SlaDueAt)).ToList(), today, now);
                await FillInfoPanelAsync(model, customer, orders, today);
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
