using AutoMapper;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Model.ViewModels.Custom;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class UserService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IOptions<AppSettingHelper> options
        , IImportService importService, IMapper mapper, IUserClaimService userClaimService
        , IUserGroupService userGroupService, IAuditChangeService auditChangeService
        , ICurrentActor currentActor, IPermissionService permissionService, IUserRoleService userRoleService, IClock clock) : IUserService
    {
        /// <summary>T45R.8 (D-38): tên đăng nhập chữ thường không dấu, số, . _ - ; 2–50 ký tự, không khoảng trắng. PROPOSED – BA/PO CONFIRM.</summary>
        private static readonly System.Text.RegularExpressions.Regex UserNamePattern = new("^[a-z0-9][a-z0-9._-]{1,49}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        private readonly IClock _clock = clock;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IImportService _importService = importService;
        private readonly AppSettingHelper _appSettingHelper = options.Value;
        private readonly IMapper _mapper = mapper;
        private readonly IUserClaimService _userClaimService = userClaimService;
        private readonly IUserGroupService _userGroupService = userGroupService;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly IUserRoleService _userRoleService = userRoleService;

        public async Task<IResults<UserViewModel>> GetAll(UserSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@FullName", request.FullName);
                dynamicParameters.Add("@UserName", request.UserName);
                dynamicParameters.Add("@UserGroupId", request.UserGroupId);
                dynamicParameters.Add("@IsActive", request.IsActive); // T45R.8 (FE-1): lọc Đang dùng / Đã khóa
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<UserViewModel>("uspUser_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    List<Guid> listUserId = list.Select(x => x.Id).ToList();
                    List<UserClaim> listUserClaim = _userClaimService.GetAllData().ListData.Where(x => listUserId.Contains(x.UserId ?? Guid.NewGuid())).ToList();
                    List<Guid?> listUserGroupId = listUserClaim.Select(x => x.UserGroupId).ToList();
                    List<UserGroup> listUserGroup = _userGroupService.GetAllData().ListData.Where(x => listUserGroupId.Contains(x.Id)).ToList();
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                        x.UserGroupName = string.Join(ConstantHelper.CommaSpace, listUserGroup.Where(e => listUserClaim.Where(d => d.UserId == x.Id).Select(d => d.UserGroupId).Contains(e.Id)).Select(e => e.Name));
                    });
                }
                return new Results<UserViewModel>
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

        public IResults<User> GetAllData()
        {
            try
            {
                var list = _unitOfWork.UserRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<User>
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

        public async Task<IResult<bool>> Save(UserRequest request, string typeSave)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var result = string.Empty;
                User oldValue = new();
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
                        break;
                }
                _unitOfWork.CommitTransaction();
                if (request.Id is Guid savedUserId)
                {
                    _permissionService.Invalidate(savedUserId); // T45R: đổi nhóm / khóa / xóa áp dụng ngay
                }
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
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess,
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

        private async Task<string> Insert(UserRequest request, string typeSave)
        {
            var (error, _) = await InsertCore(request, typeSave);
            return error;
        }

        /// <summary>
        /// T45R.8 (D-38, D-39): <c>User/Insert</c> – gắn tài khoản cho hồ sơ có sẵn (hoặc tài khoản không hồ sơ).
        /// Chặn khi Person đã có User (1 Person ↔ tối đa 1 User) và khi trùng UserName (không phân biệt hoa thường).
        /// Mật khẩu ban đầu = mật khẩu tạm 24 giờ, trả MỘT LẦN trong <c>Data.TempPassword</c>.
        /// </summary>
        public async Task<IResult<PersonUserCreatedViewModel>> InsertAccount(UserRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var (error, created) = await InsertCore(request, ConstantHelper.TypeSave.Insert);
                if (!string.IsNullOrEmpty(error) || created == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<PersonUserCreatedViewModel>.Fail(error);
                }
                _unitOfWork.CommitTransaction();
                return Result<PersonUserCreatedViewModel>.Success(created, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " InsertAccount: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<(string Error, PersonUserCreatedViewModel? Created)> InsertCore(UserRequest request, string typeSave)
        {
            if (request.PersonId is Guid personId)
            {
                if (!await _unitOfWork.PersonRepository.GetAll().AnyAsync(x => x.Id == personId && !x.IsDeleted))
                {
                    return (ConstantHelper.Message.PersonNotFound, null);
                }
                var existing = await _unitOfWork.UserRepository.GetAll().Where(x => x.PersonId == personId && !x.IsDeleted).Select(x => x.UserName ?? string.Empty).FirstOrDefaultAsync();
                if (existing != null)
                {
                    return (string.Format(ConstantHelper.Message.PersonHasUser, existing), null);
                }
            }
            string error = ValidateNewAccount(request.UserName, null, request.ListUserGroupId, out string userName);
            if (!string.IsNullOrEmpty(error))
            {
                return (error, null);
            }
            request.UserName = userName;
            var insert = _mapper.Map<User>(request);
            insert.Id = Guid.NewGuid();
            insert.IsAdmin = false; // Rà soát BE: quyền quản trị chỉ đổi qua User/SaveIsAdmin (AdminOnly) – không nhận từ form Insert.
            var created = await AddAccount(insert, request.ListUserGroupId, typeSave);
            return (string.Empty, created);
        }

        /// <summary>Ghi User với mật khẩu tạm + nhóm (UserClaim). Gọi trong transaction của nơi gọi.</summary>
        private async Task<PersonUserCreatedViewModel> AddAccount(User insert, string? listUserGroupId, string typeSave)
        {
            string tempPassword = EncriptFunctionsHelper.GenerateTemporaryPassword();
            insert.PassWord = EncriptFunctionsHelper.GenerateTemporaryPasswordHash(tempPassword, _clock.UtcNow.Add(EncriptFunctionsHelper.TemporaryPasswordLifetime));
            _unitOfWork.UserRepository.Add(insert);
            await _unitOfWork.UserRepository.CommitAsync(typeSave);
            await _userClaimService.SaveAfterUser(insert.Id, NormalizeGroupIds(listUserGroupId));
            return new PersonUserCreatedViewModel { PersonId = insert.PersonId, UserId = insert.Id, UserName = insert.UserName, TempPassword = tempPassword };
        }

        private static string? NormalizeGroupIds(string? listUserGroupId)
        {
            var ids = (listUserGroupId ?? string.Empty).Split(ConstantHelper.Comma, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return ids.Length == 0 ? null : string.Join(ConstantHelper.Comma, ids.Select(Guid.Parse).Distinct());
        }

        /// <summary>
        /// T45R.8 (D-38): kiểm tài khoản sắp tạo. <paramref name="userName"/> trống → tự sinh từ <paramref name="fullName"/>
        /// (tên + chữ cái đầu họ / đệm, không dấu, thêm số nếu trùng). Có → chuẩn hóa chữ thường, kiểm định dạng và trùng (không phân biệt hoa thường).
        /// Nhóm quyền phải tồn tại. Trả thông báo lỗi (rỗng = hợp lệ).
        /// </summary>
        public string ValidateNewAccount(string? userName, string? fullName, string? listUserGroupId, out string resolvedUserName)
        {
            resolvedUserName = string.Empty;
            var groupIds = (listUserGroupId ?? string.Empty).Split(ConstantHelper.Comma, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (groupIds.Length != 0)
            {
                List<Guid> parsed = [];
                foreach (var id in groupIds)
                {
                    if (!Guid.TryParse(id, out Guid g))
                    {
                        return ConstantHelper.Message.GroupNotFound;
                    }
                    parsed.Add(g);
                }
                parsed = [.. parsed.Distinct()];
                int found = _unitOfWork.UserGroupRepository.GetAll().Count(x => parsed.Contains(x.Id) && !x.IsDeleted);
                if (found != parsed.Count)
                {
                    return ConstantHelper.Message.GroupNotFound;
                }
            }
            if (string.IsNullOrWhiteSpace(userName))
            {
                string baseName = GenerateUserNameBase(fullName);
                if (baseName.Length < 2)
                {
                    return ConstantHelper.Message.UserNameCannotGenerate;
                }
                for (int i = 0; i < 10_000; i++)
                {
                    string candidate = i == 0 ? baseName : string.Concat(baseName, i);
                    if (!UserNameTaken(candidate))
                    {
                        resolvedUserName = candidate;
                        return string.Empty;
                    }
                }
                return ConstantHelper.Message.UserNameCannotGenerate;
            }
            string normalized = userName.Trim().ToLowerInvariant();
            if (!UserNamePattern.IsMatch(normalized))
            {
                return ConstantHelper.Message.UserNameInvalid;
            }
            if (UserNameTaken(normalized))
            {
                return string.Format(ConstantHelper.Message.UserNameExist, normalized);
            }
            resolvedUserName = normalized;
            return string.Empty;
        }

        private bool UserNameTaken(string lowerUserName)
            => _unitOfWork.UserRepository.GetAll().Any(x => !x.IsDeleted && x.UserName != null && x.UserName.ToLower() == lowerUserName);

        /// <summary>Cách sinh cũ: "Phạm Văn Ngọc" → "ngocpv" (tên + chữ cái đầu của họ / đệm), nay bỏ dấu cả chữ cái đầu (Đ → d) và bỏ ký tự lạ.</summary>
        private static string GenerateUserNameBase(string? fullName)
        {
            var words = CommonHelper.RemoveSign4VietnameseString(fullName ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => new string([.. w.ToLowerInvariant().Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9')]))
                .Where(w => w.Length != 0).ToList();
            if (words.Count == 0)
            {
                return string.Empty;
            }
            string name = string.Concat(words[^1], string.Concat(words.Take(words.Count - 1).Select(w => w[0])));
            return name.Length > 50 ? name[..50] : name;
        }

        private async Task<string> Update(UserRequest request, string typeSave)
        {
            var update = _unitOfWork.UserRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (string.IsNullOrEmpty(request.Id.ToString()) || update == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            update.PersonId = request.PersonId;
            update.IsActive = request.IsActive;
            // Rà soát BE: KHÔNG ghi IsAdmin từ form Update (người có quyền Sửa màn adm-user tự nâng thành admin được) – chỉ qua User/SaveIsAdmin (AdminOnly).
            _unitOfWork.UserRepository.Update(update);
            await _unitOfWork.UserRepository.CommitAsync(typeSave);
            await _userClaimService.SaveAfterUser(update.Id, request.ListUserGroupId);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (string.IsNullOrEmpty(id.ToString()))
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard thay SQL động trên mọi bảng (A10): kiểm tra quan hệ logic tới User; UserClaim bị xóa kèm.
            // Bản ghi do user tạo / sửa (CreatedBy…) không chặn xóa mềm – xóa mềm giữ nguyên tham chiếu audit.
            if (await _auditChangeService.IsReferencedAsync(nameof(User), id.GetValueOrDefault(), nameof(UserClaim)))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.UserRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.UserRepository.Update(delete);
            await _unitOfWork.UserRepository.CommitAsync(typeSave);
            await _userClaimService.SaveAfterUser(id ?? Guid.NewGuid(), string.Empty);
            return string.Empty;
        }

        public async Task<IResult<UserModel>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.UserRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<UserModel>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = new(),
                    };
                }
                var listUserGroupId = string.Join(ConstantHelper.Comma, _userClaimService.GetAllData().ListData.Where(x => x.UserId == data.Id).Select(x => x.UserGroupId.ToString()).ToList());
                UserModel userModel = new()
                {
                    Id = data.Id,
                    UserName = data.UserName,
                    FullName = (await _unitOfWork.PersonRepository.GetSingleAsync(x => x.Id == data.PersonId))?.FullName,
                    PersonId = data.PersonId,
                    IsActive = data.IsActive,
                    IsAdmin = data.IsAdmin,
                    IsDefault = data.IsDefault,
                    ListUserGroupId = !string.IsNullOrEmpty(listUserGroupId) ? listUserGroupId : null,
                };
                return new Result<UserModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = userModel,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// T45R.8 (D-39): đặt lại mật khẩu = mật khẩu tạm 12 ký tự, hạn 24 giờ (hash <c>PBKDF2T$…</c>). <c>Data</c> = mật khẩu tạm, trả MỘT LẦN,
        /// không log, không lưu bản rõ. AuditChange tự ghi "ai (Actor / CreatedBy) đặt lại cho ai (IdChange)" – cột PassWord chỉ ghi "đã thay đổi".
        /// Chỉ admin (PermissionConvention.AdminActions).
        /// </summary>
        public async Task<IResult<string>> ResetPassword(Guid id)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var update = id == Guid.Empty ? null : _unitOfWork.UserRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                if (update == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                string tempPassword = EncriptFunctionsHelper.GenerateTemporaryPassword();
                update.PassWord = EncriptFunctionsHelper.GenerateTemporaryPasswordHash(tempPassword, _clock.UtcNow.Add(EncriptFunctionsHelper.TemporaryPasswordLifetime));
                _unitOfWork.UserRepository.Update(update);
                await _unitOfWork.UserRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                Log.Information("Đặt lại mật khẩu tạm cho user {UserId} bởi {Actor}", id, _currentActor.Actor); // không ghi mật khẩu
                return Result<string>.Success(tempPassword, ConstantHelper.Message.ResetPassword);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ResetPassword: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<bool>> SaveIsActive(Guid id)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                if (string.IsNullOrEmpty(id.ToString()))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = false
                    };
                }
                var update = _unitOfWork.UserRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                if (update == null)
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = false
                    };
                }
                // T45R (spec 3.2): không tự khóa tài khoản của chính mình.
                if (update.IsActive && _currentActor.Actor?.UserId == id)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(ConstantHelper.Message.CannotLockYourself, false);
                }
                update.IsActive = !update.IsActive;
                _unitOfWork.UserRepository.Update(update);
                await _unitOfWork.UserRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                _permissionService.Invalidate(id);
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveIsActive: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<bool>> SaveIsAdmin(Guid id)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                if (string.IsNullOrEmpty(id.ToString()))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = false
                    };
                }
                var update = _unitOfWork.UserRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                if (update == null)
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = false
                    };
                }
                update.IsAdmin = !update.IsAdmin;
                _unitOfWork.UserRepository.Update(update);
                await _unitOfWork.UserRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                _permissionService.Invalidate(id);
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveIsAdmin: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<bool>> ChangePassword(ChangePasswordRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var message = string.Empty;
                // Rà soát BE: chỉ đổi mật khẩu của CHÍNH người đăng nhập (trước đây lấy Id từ form → đoán mật khẩu user khác không giới hạn).
                Guid me = _currentActor.RequireActor().UserId;
                if (request.Id != Guid.Empty && request.Id != me)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail("Chỉ đổi được mật khẩu của chính tài khoản đang đăng nhập.");
                }
                var exist = _unitOfWork.UserRepository.GetSingle(x => x.Id == me && !x.IsDeleted);
                if (exist == null)
                {
                    message += "Tài khoản của bạn không tồn tại trong hệ thống";
                }
                else
                {
                    if (exist.IsActive == false)
                    {
                        if (!string.IsNullOrEmpty(message))
                        {
                            message += ", ";
                        }
                        message += "Tài khoản của bạn đang bị khóa, vui lòng liên hệ với quản trị viên";
                    }
                    if (!EncriptFunctionsHelper.VerifyPassword(request.PassWordOld ?? string.Empty, exist.PassWord, exist.UserName, out _)) // T48.0: kiểm cả PBKDF2 và MD5 cũ
                    {
                        if (!string.IsNullOrEmpty(message))
                        {
                            message += ", ";
                        }
                        message += "Mật khẩu cũ không đúng";
                    }
                    else if (EncriptFunctionsHelper.IsTemporaryExpired(exist.PassWord, _clock.UtcNow)) // T45R.8 (D-39)
                    {
                        if (!string.IsNullOrEmpty(message))
                        {
                            message += ", ";
                        }
                        message += ConstantHelper.Message.TempPasswordExpired;
                    }
                    if ((request.PassWordNew?.Length ?? 0) < 8) // T48.0: đúng thông báo "từ 8 ký tự" (GAP-ADM-02); trước đây đòi ≥ 9
                    {
                        if (!string.IsNullOrEmpty(message))
                        {
                            message += ", ";
                        }
                        message += "Mật khẩu mới từ 8 ký tự trở lên";
                    }
                }
                if (!string.IsNullOrEmpty(message))
                {
                    _unitOfWork.CommitTransaction();
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = message,
                        Data = false
                    };
                }
                if (exist != null)
                {
                    // T48.0: lưu PBKDF2 của mật khẩu mới (trước đây MD5(mật khẩu + tên) – đăng nhập kiểm MD5(mật khẩu) nên user không vào lại được).
                    // T45R.8: luôn là PBKDF2 thường → bỏ dấu mật khẩu tạm (PBKDF2T$).
                    exist.PassWord = EncriptFunctionsHelper.GeneratePassword(request.PassWordNew ?? string.Empty);
                    _unitOfWork.UserRepository.Update(exist);
                    await _unitOfWork.UserRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                _unitOfWork.CommitTransaction();
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = "Thay đổi mật khẩu thành công",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ChangePassword: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>T45R (spec 2.2 /meta/me): tài khoản đang đăng nhập + menu theo quyền hiệu lực.</summary>
        public async Task<IResult<CurrentUserModel>> GetCurrent()
        {
            try
            {
                Guid userId = _currentActor.RequireActor().UserId;
                var user = await GetById(userId);
                if (user.ResponseCode != Convert.ToInt32(HttpStatusCode.OK))
                {
                    return Result<CurrentUserModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var menu = await _userRoleService.GetByUserId(userId);
                return Result<CurrentUserModel>.Success(new CurrentUserModel
                {
                    User = user.Data,
                    ListMenu = menu.Data?.ListMenu ?? [],
                    MustChangePassword = await IsTemporaryPassword(userId),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCurrent: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T45R.8 (D-39): tài khoản đang dùng mật khẩu tạm (chưa đổi). Dùng cho PermissionFilter và GetCurrent.</summary>
        public async Task<bool> IsTemporaryPassword(Guid userId)
        {
            var stored = await _unitOfWork.UserRepository.GetAll().AsNoTracking().Where(x => x.Id == userId && !x.IsDeleted).Select(x => x.PassWord).FirstOrDefaultAsync();
            return EncriptFunctionsHelper.IsTemporary(stored);
        }

        /// <summary>
        /// T45R.8 (D-38): tạo ĐÚNG 1 tài khoản cho Person vừa tạo – trong transaction của nơi gọi (<c>PersonService.Save</c>).
        /// <paramref name="userName"/> trống → tự sinh từ họ tên; nhóm quyền qua <c>UserClaimService.SaveAfterUser</c>; mật khẩu tạm (D-39).
        /// Lỗi nghiệp vụ → <c>ResponseCode = 400</c> (nơi gọi rollback).
        /// </summary>
        public async Task<IResult<PersonUserCreatedViewModel>> InsertUserAfter(Guid personId, string? fullName, string typeSave
            , string? userName = null, string? listUserGroupId = null, bool isActive = false)
        {
            try
            {
                var existing = await _unitOfWork.UserRepository.GetAll().Where(x => x.PersonId == personId && !x.IsDeleted).Select(x => x.UserName ?? string.Empty).FirstOrDefaultAsync();
                if (existing != null)
                {
                    return Result<PersonUserCreatedViewModel>.Fail(string.Format(ConstantHelper.Message.PersonHasUser, existing));
                }
                string error = ValidateNewAccount(userName, fullName, listUserGroupId, out string resolvedUserName);
                if (!string.IsNullOrEmpty(error))
                {
                    return Result<PersonUserCreatedViewModel>.Fail(error);
                }
                var insert = new User { Id = Guid.NewGuid(), UserName = resolvedUserName, PersonId = personId, IsActive = isActive };
                var created = await AddAccount(insert, listUserGroupId, typeSave);
                return Result<PersonUserCreatedViewModel>.Success(created, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " InsertUserAfter: ", ex.ToString()));
                throw;
            }
        }
    }
}
