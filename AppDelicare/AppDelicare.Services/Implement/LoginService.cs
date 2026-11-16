using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.Response;
using AppDelicare.Services.Interface;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace AppDelicare.Services.Implement
{
    public class LoginService(ITokenFactoryService tokenFactory, IJwtFactoryService jwtFactory, IOptions<JwtIssuerOptions> jwtOptions
        , IUnitOfWork unitOfWork, IOptions<AppSettingHelper> options, IUserRoleService userRoleService, ICurrentActor currentActor
        , Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor) : ILoginService
    {
        private readonly ITokenFactoryService _tokenFactory = tokenFactory;
        private readonly IJwtFactoryService _jwtFactory = jwtFactory;
        private readonly JwtIssuerOptions _jwtOptions = jwtOptions.Value;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly AppSettingHelper _appSettingHelper = options.Value;
        private readonly IUserRoleService _userRoleService = userRoleService;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        public async Task<Response<AuthenticateResponse>> Authenticate(LoginRequest request)
        {
            List<Claim> claims = [];
            request.UserName = request.UserName?.ToLower();
            // T48.0 (D-29): tìm theo tên đăng nhập rồi kiểm mật khẩu (PBKDF2 hoặc MD5 cũ) – không so chuỗi hash trong SQL nữa (salt ngẫu nhiên).
            // Nhiều bản ghi trùng tên (đã xóa mềm + đang dùng): ưu tiên bản chưa xóa.
            var candidates = await _unitOfWork.UserRepository.GetAll().Where(x => x.UserName == request.UserName)
                .OrderBy(x => x.IsDeleted).ToListAsync();
            var user = candidates.FirstOrDefault(x => EncriptFunctionsHelper.VerifyPassword(request.PassWord ?? string.Empty, x.PassWord, x.UserName, out _));
            if (user == null)
            {
                return new Response<AuthenticateResponse>
                {
                    Code = Convert.ToInt32(HttpStatusCode.BadRequest),
                    Message = ConstantHelper.Message.UserInActiveMessage
                };
            }
            if (user.IsDeleted)
            {
                return new Response<AuthenticateResponse>
                {
                    Code = Convert.ToInt32(HttpStatusCode.InternalServerError),
                    Message = ConstantHelper.Message.AccountDeleted
                };
            }
            if (!user.IsActive)
            {
                return new Response<AuthenticateResponse>
                {
                    Code = Convert.ToInt32(HttpStatusCode.InternalServerError),
                    Message = ConstantHelper.Message.AccountInActive
                };
            }
            // T48.0: hash MD5 cũ đúng mật khẩu → nâng lên PBKDF2 ngay (user không phải đổi mật khẩu). Không log mật khẩu / hash.
            EncriptFunctionsHelper.VerifyPassword(request.PassWord ?? string.Empty, user.PassWord, user.UserName, out bool needsUpgrade);
            if (needsUpgrade)
            {
                using (_currentActor.BeginScope(ActorInfo.ForUser(user.Id)))
                {
                    user.PassWord = EncriptFunctionsHelper.GeneratePassword(request.PassWord ?? string.Empty);
                    _unitOfWork.UserRepository.Update(user);
                    await _unitOfWork.UserRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                Serilog.Log.Information("Nâng hash mật khẩu lên PBKDF2 cho user {UserId}", user.Id);
            }
            var id = user.Id;
            var userName = user.UserName ?? string.Empty;
            var person = await _unitOfWork.PersonRepository.GetSingleAsync(x => x.Id == user.PersonId);
            // T48.1: user chưa có hồ sơ Person (vd. admin của DB mới) không được làm lỗi đăng nhập.
            var fullName = person?.FullName ?? string.Empty;
            var avatar = !string.IsNullOrEmpty(person?.Avatar) ? Path.Combine(_appSettingHelper.DomainFile ?? string.Empty, ConstantHelper.Import, ConstantHelper.FolderName.PERSON_IMAGE, person!.Avatar!) : string.Empty;
            List<string> listUserGroupId = [.. _unitOfWork.UserClaimRepository.GetAll().Where(x => x.UserId == id && !x.IsDeleted).Select(x => x.UserGroupId.ToString())];
            var lstUserGroupId = listUserGroupId.Count != 0 ? string.Join(ConstantHelper.Comma, listUserGroupId) : string.Empty;
            List<string> listUserGroupName = [.. _unitOfWork.UserGroupRepository.GetAll().Where(x => listUserGroupId.Contains(x.Id.ToString()) && !x.IsDeleted).Select(x => x.Name)];
            var userGroupName = listUserGroupName.Count != 0 ? string.Join(ConstantHelper.CommaSpace, listUserGroupName) : string.Empty;
            var pcName = Dns.GetHostName();
            // T48.1 (CODE_AUDIT A20): trước đây gọi api.iplocation.net mỗi lần đăng nhập (lấy IP công khai của SERVER, gửi request ra
            // Internet, lỗi khi không có mạng). Nay ghi IP của CLIENT gửi request.
            var iPv4 = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? ConstantHelper.NotFound;
            var isAdmin = user.IsAdmin;
            var listMenu = (await _userRoleService.GetByUserGroupId(lstUserGroupId, false)).Data?.ListMenu;
            ClaimsIdentity identity = await GetClaimsIdentity(request);
            claims.Add(new Claim(ClaimTypes.Role, userName));
            claims.Add(new Claim(ClaimTypes.Role, fullName));
            claims.Add(new Claim(ClaimTypes.Role, avatar));
            claims.Add(new Claim(ClaimTypes.Role, lstUserGroupId));
            claims.Add(new Claim(ClaimTypes.Role, userGroupName));
            claims.Add(new Claim(ClaimTypes.Role, pcName));
            claims.Add(new Claim(ClaimTypes.Role, iPv4 ?? ConstantHelper.NotFound));
            claims.Add(new Claim(ClaimTypes.Role, isAdmin.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, string.Join(ConstantHelper.Comma, listMenu?.Select(x => x.ObjectMenuName).ToList() ?? [])));
            var jwt = await _tokenFactory.GenerateJwt(identity,
                                                      _jwtFactory,
                                                      _jwtOptions,
                                                      new JsonSerializerSettings { Formatting = Formatting.Indented },
                                                      id,
                                                      userName,
                                                      fullName,
                                                      avatar,
                                                      userGroupName,
                                                      isAdmin,
                                                      listMenu ?? [],
                                                      claims);
            var jwtObject = JsonConvert.DeserializeObject<AuthenticateResponse>(jwt);
            LocationTracking insertLocationTracking = new()
            {
                IPv4 = iPv4,
                PcName = pcName,
                CreatedBy = id,
                CreatedDate = DateTime.Now
            };
            // T02: request đăng nhập chưa có token → người thao tác là chính user vừa xác thực.
            using (_currentActor.BeginScope(ActorInfo.ForUser(id)))
            {
                _unitOfWork.LocationTrackingRepository.Add(insertLocationTracking);
                _unitOfWork.LocationTrackingRepository.Commit();
            }
            return new Response<AuthenticateResponse>
            {
                Code = Convert.ToInt32(HttpStatusCode.OK),
                Message = ConstantHelper.Message.Successfully,
                Data = jwtObject
            };
        }

        private async Task<ClaimsIdentity> GetClaimsIdentity(LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.PassWord))
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                return await Task.FromResult<ClaimsIdentity>(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            }

            // get the user to verifty
            var userToVerify = _unitOfWork.UserRepository.FindBy(x => x.UserName == request.UserName && x.IsActive == true && !x.IsDeleted).ToList();

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            if (userToVerify.Count == 0)
            {
                return await Task.FromResult<ClaimsIdentity>(null);
            }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

            // check the credentials
            if (EncriptFunctionsHelper.VerifyPassword(request.PassWord, userToVerify.FirstOrDefault()?.PassWord, request.UserName, out _))
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                return await Task.FromResult(_jwtFactory.GenerateClaimsIdentity(request.UserName, userToVerify.FirstOrDefault().Id));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }

            // Credentials are invalid, or account doesn't exist
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            return await Task.FromResult<ClaimsIdentity>(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }
    }
}
