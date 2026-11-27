using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement.Retail
{
    /// <summary>
    /// RETAIL-2 (D-136 – PROPOSED – BA/PO CONFIRM): tài khoản khách lẻ (bảng RetailCustomer, tách khỏi tài khoản nhân viên dbo.[User]) + Đơn của tôi.
    /// Ghi (đăng ký / sửa hồ sơ / đổi mật khẩu / gắn đơn) dưới tài khoản system (SystemUsers:JobUserId) – người thao tác thật là khách, ghi ở log.
    /// Chống dò: sai SĐT hay sai mật khẩu báo như nhau; khóa tạm theo SĐT sau N lần sai; giới hạn tạo tài khoản theo IP.
    /// Chưa có xác thực SĐT bằng mã OTP (chưa có cổng SMS) → đơn cũ đặt khi chưa đăng nhập KHÔNG tự gắn, khách tự thêm bằng số đơn (D-136 c).
    /// </summary>
    public partial class RetailAccountService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules,
        IOptions<ReferenceListsOptions> referenceLists, IConfiguration configuration, IMemoryCache cache) : IRetailAccountService
    {
        public const int Unauthorized = 401;
        public const string GroupActive = "active";
        public const string GroupDone = "done";
        public const string GroupCancelled = "cancelled";
        private const string StatusActive = "Active";
        /// <summary>D-136 j: loại tài khoản khách lẻ.</summary>
        public const string TypeIndividual = "INDIVIDUAL";
        public const string TypeBusiness = "BUSINESS";

        private readonly RetailRulesOptions _rules = rules.Value.Retail;
        private readonly ReferenceListsOptions _lists = referenceLists.Value;
        private readonly byte[]? _key = RetailAccountRules.DeriveKey(configuration["Jwt:SigningKey"]);

        public async Task<IResult<RetailSessionModel>> Register(RetailRegisterRequest request, string? clientIp)
        {
            if (!string.IsNullOrWhiteSpace(request.Website))
            {
                Log.Warning("[RetailAccount] Bỏ đăng ký có ô bẫy từ {Ip}", clientIp);
                return Result<RetailSessionModel>.Fail(RetailMessage.RegisterTooMany);
            }
            string? name = RetailRules.Clean(request.FullName, 200);
            if (name == null || name.Length < 2)
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.NameRequired.Replace("người nhận", "họ tên"));
            }
            string? phone = RetailRules.NormalizePhone(request.PhoneNumber);
            if (phone == null)
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.PhoneInvalid);
            }
            if ((request.Password ?? string.Empty).Length < _rules.PasswordMinLength || request.Password!.Length > 100)
            {
                return Result<RetailSessionModel>.Fail(string.Format(RetailMessage.PasswordTooShort, _rules.PasswordMinLength));
            }
            string? email = RetailRules.Clean(request.Email, 255);
            if (email != null && !EmailPattern().IsMatch(email))
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.EmailInvalid);
            }
            // D-136 j: cá nhân – SĐT (đã kiểm ở trên); doanh nghiệp – thêm tên DN + mã số thuế + email.
            string? type = NormalizeType(request.AccountType);
            if (type == null)
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.AccountTypeInvalid);
            }
            var (company, taxCode, bizError) = Business(type, request.CompanyName, request.TaxCode, email);
            if (bizError != null)
            {
                return Result<RetailSessionModel>.Fail(bizError);
            }
            string ipKey = "retail-register:" + (clientIp ?? "-");
            int count = cache.TryGetValue(ipKey, out int c) ? c : 0;
            if (count >= _rules.RegisterLimitPerIp)
            {
                return new Result<RetailSessionModel> { ResponseCode = RetailPortalService.TooManyRequests, ResponseMessage = RetailMessage.RegisterTooMany };
            }
            if (_key == null || SystemUser() is not { } system)
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.AccountUnavailable);
            }
            var db = unitOfWork.DbContext;
            if (await db.RetailCustomer.AsNoTracking().AnyAsync(x => x.PhoneNumber == phone && !x.IsDeleted))
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.PhoneTaken);
            }
            if (taxCode != null && await db.RetailCustomer.AsNoTracking().AnyAsync(x => x.TaxCode == taxCode && !x.IsDeleted))
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.TaxCodeTaken);
            }
            DateTime now = clock.Now;
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                using var tran = await unitOfWork.OpenTransaction();
                try
                {
                    var entity = new RetailCustomer
                    {
                        Id = Guid.NewGuid(), PhoneNumber = phone, FullName = name, Email = email, Address = RetailRules.Clean(request.Address, 500),
                        AccountType = type, CompanyName = company, TaxCode = taxCode,
                        PasswordHash = RetailAccountRules.HashPassword(request.Password), PasswordChangedAt = now, LastLoginAt = now, Status = StatusActive,
                    };
                    unitOfWork.RetailCustomerRepository.Add(entity);
                    await unitOfWork.RetailCustomerRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    unitOfWork.CommitTransaction();
                    cache.Set(ipKey, count + 1, TimeSpan.FromMinutes(_rules.LimitWindowMinutes));
                    Log.Information("[RetailAccount] Khách lẻ đăng ký tài khoản {Id}", entity.Id);
                    return Result<RetailSessionModel>.Success(Session(entity, now), RetailMessage.Registered);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat("[", GetType().Name, "]", " Register: ", ex.ToString()));
                    unitOfWork.RollbackTransaction();
                    throw;
                }
            }
        }

        public async Task<IResult<RetailSessionModel>> Login(RetailLoginRequest request, string? clientIp)
        {
            string? phone = RetailRules.NormalizePhone(request.PhoneNumber);
            if (phone == null || string.IsNullOrEmpty(request.Password))
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.LoginFailed);
            }
            string failKey = "retail-login-fail:" + phone;
            int fails = cache.TryGetValue(failKey, out int f) ? f : 0;
            if (fails >= _rules.LoginFailLimit)
            {
                return new Result<RetailSessionModel> { ResponseCode = RetailPortalService.TooManyRequests, ResponseMessage = string.Format(RetailMessage.LoginLocked, _rules.LoginLockMinutes) };
            }
            if (_key == null || SystemUser() is not { } system)
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.AccountUnavailable);
            }
            var db = unitOfWork.DbContext;
            var customer = await db.RetailCustomer.FirstOrDefaultAsync(x => x.PhoneNumber == phone && !x.IsDeleted);
            if (customer == null || !RetailAccountRules.VerifyPassword(request.Password, customer.PasswordHash))
            {
                cache.Set(failKey, fails + 1, TimeSpan.FromMinutes(_rules.LoginLockMinutes));
                Log.Warning("[RetailAccount] Đăng nhập sai (lần {Count}) từ {Ip}", fails + 1, clientIp);
                return Result<RetailSessionModel>.Fail(RetailMessage.LoginFailed);
            }
            if (customer.Status != StatusActive)
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.AccountInactive);
            }
            cache.Remove(failKey);
            DateTime now = clock.Now;
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                using var tran = await unitOfWork.OpenTransaction();
                try
                {
                    customer.LastLoginAt = now;
                    unitOfWork.RetailCustomerRepository.Update(customer);
                    await unitOfWork.RetailCustomerRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    unitOfWork.CommitTransaction();
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat("[", GetType().Name, "]", " Login: ", ex.ToString()));
                    unitOfWork.RollbackTransaction();
                    throw;
                }
            }
            return Result<RetailSessionModel>.Success(Session(customer, now), RetailMessage.LoggedIn);
        }

        public async Task<RetailCustomer?> Authenticate(string? token)
        {
            if (_key == null || RetailAccountRules.ReadToken(_key, token, clock.Now) is not { } t)
            {
                return null;
            }
            var customer = await unitOfWork.DbContext.RetailCustomer.AsNoTracking().FirstOrDefaultAsync(x => x.Id == t.CustomerId && !x.IsDeleted);
            return customer != null && customer.Status == StatusActive && RetailAccountRules.Stamp(_key, customer.PasswordHash) == t.Stamp ? customer : null;
        }

        public async Task<IResult<RetailCustomerProfileModel>> GetProfile(string? token)
        {
            var me = await Authenticate(token);
            return me == null ? Denied<RetailCustomerProfileModel>() : Result<RetailCustomerProfileModel>.Success(Profile(me), ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResult<RetailCustomerProfileModel>> UpdateProfile(string? token, RetailProfileRequest request)
        {
            var me = await Authenticate(token);
            if (me == null)
            {
                return Denied<RetailCustomerProfileModel>();
            }
            string? name = RetailRules.Clean(request.FullName, 200);
            if (name == null || name.Length < 2)
            {
                return Result<RetailCustomerProfileModel>.Fail(RetailMessage.NameRequired.Replace("người nhận", "họ tên"));
            }
            string? email = RetailRules.Clean(request.Email, 255);
            if (email != null && !EmailPattern().IsMatch(email))
            {
                return Result<RetailCustomerProfileModel>.Fail(RetailMessage.EmailInvalid);
            }
            // Loại tài khoản không đổi; doanh nghiệp luôn phải đủ tên DN + mã số thuế + email.
            string type = NormalizeType(me.AccountType) ?? TypeIndividual;
            var (company, taxCode, bizError) = Business(type, request.CompanyName ?? me.CompanyName, request.TaxCode ?? me.TaxCode, email);
            if (bizError != null)
            {
                return Result<RetailCustomerProfileModel>.Fail(bizError);
            }
            if (taxCode != null && taxCode != me.TaxCode
                && await unitOfWork.DbContext.RetailCustomer.AsNoTracking().AnyAsync(x => x.TaxCode == taxCode && x.Id != me.Id && !x.IsDeleted))
            {
                return Result<RetailCustomerProfileModel>.Fail(RetailMessage.TaxCodeTaken);
            }
            var saved = await SaveAsync(me.Id, c =>
            {
                c.FullName = name;
                c.Email = email;
                c.Address = RetailRules.Clean(request.Address, 500);
                c.CompanyName = company;
                c.TaxCode = taxCode;
            }, nameof(UpdateProfile));
            return saved == null ? Result<RetailCustomerProfileModel>.Fail(RetailMessage.AccountUnavailable)
                : Result<RetailCustomerProfileModel>.Success(Profile(saved), RetailMessage.ProfileSaved);
        }

        public async Task<IResult<RetailSessionModel>> ChangePassword(string? token, RetailChangePasswordRequest request)
        {
            var me = await Authenticate(token);
            if (me == null)
            {
                return Denied<RetailSessionModel>();
            }
            if (!RetailAccountRules.VerifyPassword(request.CurrentPassword, me.PasswordHash))
            {
                return Result<RetailSessionModel>.Fail(RetailMessage.CurrentPasswordWrong);
            }
            if ((request.NewPassword ?? string.Empty).Length < _rules.PasswordMinLength || request.NewPassword!.Length > 100)
            {
                return Result<RetailSessionModel>.Fail(string.Format(RetailMessage.PasswordTooShort, _rules.PasswordMinLength));
            }
            DateTime now = clock.Now;
            var saved = await SaveAsync(me.Id, c =>
            {
                c.PasswordHash = RetailAccountRules.HashPassword(request.NewPassword);
                c.PasswordChangedAt = now;
            }, nameof(ChangePassword));
            return saved == null ? Result<RetailSessionModel>.Fail(RetailMessage.AccountUnavailable)
                : Result<RetailSessionModel>.Success(Session(saved, now), RetailMessage.PasswordChanged);
        }

        public async Task<IResult<RetailMyOrderListModel>> GetMyOrders(string? token, string? group)
        {
            var me = await Authenticate(token);
            if (me == null)
            {
                return Denied<RetailMyOrderListModel>();
            }
            try
            {
                var db = unitOfWork.DbContext;
                var orders = await db.RetailOrder.AsNoTracking().Where(o => o.RetailCustomerId == me.Id && !o.IsDeleted)
                    .OrderByDescending(o => o.DeliveryDate).ThenByDescending(o => o.CreatedDate).Take(200).ToListAsync();
                string GroupOf(RetailOrder o) => o.OrderStatus == RetailOrderStatus.Completed ? GroupDone : o.OrderStatus == RetailOrderStatus.Cancelled ? GroupCancelled : GroupActive;
                string g = (group ?? GroupActive).Trim().ToLowerInvariant();
                var picked = g == "all" ? orders : orders.Where(o => GroupOf(o) == g).ToList();
                var stages = await RetailProgress.StagesAsync(db, picked, _lists);
                var ids = picked.Select(o => o.Id).ToList();
                var lines = await db.RetailOrderLine.AsNoTracking().Where(l => ids.Contains(l.RetailOrderId) && !l.IsDeleted).ToListAsync();
                var model = new RetailMyOrderListModel
                {
                    GroupCounts = new Dictionary<string, int>
                    {
                        [GroupActive] = orders.Count(o => GroupOf(o) == GroupActive),
                        [GroupDone] = orders.Count(o => GroupOf(o) == GroupDone),
                        [GroupCancelled] = orders.Count(o => GroupOf(o) == GroupCancelled),
                    },
                    ListItem = picked.Select(o => new RetailMyOrderItemModel
                    {
                        OrderNumber = o.OrderNumber ?? string.Empty, DeliveryDate = RetailRules.Iso(o.DeliveryDate), MealType = o.MealType ?? string.Empty,
                        OrderStatus = o.OrderStatus ?? string.Empty, StageCode = stages[o.Id], StageLabel = RetailProgress.StageLabel(stages[o.Id]),
                        TotalAmount = o.TotalAmount, CreatedDate = o.CreatedDate, HasRoute = o.DeliveryRouteId != null,
                        LineSummary = string.Join(", ", lines.Where(l => l.RetailOrderId == o.Id).OrderBy(l => l.CreatedDate).Select(l => $"{l.ProductName} × {l.Quantity:0.##}")),
                    }).ToList(),
                };
                // Đang xử lý: ngày nhận gần nhất lên trước.
                if (g == GroupActive)
                {
                    model.ListItem = model.ListItem.OrderBy(i => i.DeliveryDate, StringComparer.Ordinal).ThenBy(i => i.CreatedDate).ToList();
                }
                return Result<RetailMyOrderListModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMyOrders: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<RetailMyOrderDetailModel>> GetMyOrder(string? token, string? orderNumber)
        {
            var me = await Authenticate(token);
            if (me == null)
            {
                return Denied<RetailMyOrderDetailModel>();
            }
            var o = await MineAsync(me, orderNumber);
            if (o == null)
            {
                return Result<RetailMyOrderDetailModel>.Fail(RetailMessage.OrderNotFound);
            }
            try
            {
                var model = await RetailProgress.DetailAsync(unitOfWork.DbContext, o, _lists);
                model.Address = o.Address ?? string.Empty;
                model.Note = o.Note;
                return Result<RetailMyOrderDetailModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMyOrder: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<RetailTraceModel>> GetOrderTrace(string? token, string? orderNumber)
        {
            var me = await Authenticate(token);
            if (me == null)
            {
                return Denied<RetailTraceModel>();
            }
            var o = await MineAsync(me, orderNumber);
            if (o == null)
            {
                return Result<RetailTraceModel>.Fail(RetailMessage.OrderNotFound);
            }
            try
            {
                return Result<RetailTraceModel>.Success(await RetailProgress.TraceAsync(unitOfWork.DbContext, o, _lists), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOrderTrace: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<RetailMyOrderDetailModel>> ClaimOrder(string? token, RetailClaimOrderRequest request)
        {
            var me = await Authenticate(token);
            if (me == null)
            {
                return Denied<RetailMyOrderDetailModel>();
            }
            string? number = RetailRules.Clean(request.OrderNumber, 50)?.ToUpperInvariant();
            var db = unitOfWork.DbContext;
            var o = number == null ? null : await db.RetailOrder.FirstOrDefaultAsync(x => x.OrderNumber == number && x.PhoneNumber == me.PhoneNumber && !x.IsDeleted);
            if (o == null)
            {
                return Result<RetailMyOrderDetailModel>.Fail(RetailMessage.ClaimNotFound);
            }
            if (o.RetailCustomerId != null && o.RetailCustomerId != me.Id)
            {
                return Result<RetailMyOrderDetailModel>.Fail(RetailMessage.ClaimOtherAccount);
            }
            if (o.RetailCustomerId == null)
            {
                if (SystemUser() is not { } system)
                {
                    return Result<RetailMyOrderDetailModel>.Fail(RetailMessage.AccountUnavailable);
                }
                using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
                {
                    using var tran = await unitOfWork.OpenTransaction();
                    try
                    {
                        o.RetailCustomerId = me.Id;
                        unitOfWork.RetailOrderRepository.Update(o);
                        await unitOfWork.RetailOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                        unitOfWork.CommitTransaction();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Concat("[", GetType().Name, "]", " ClaimOrder: ", ex.ToString()));
                        unitOfWork.RollbackTransaction();
                        throw;
                    }
                }
            }
            var model = await RetailProgress.DetailAsync(db, o, _lists);
            model.Address = o.Address ?? string.Empty;
            model.Note = o.Note;
            return Result<RetailMyOrderDetailModel>.Success(model, RetailMessage.Claimed);
        }

        private async Task<RetailOrder?> MineAsync(RetailCustomer me, string? orderNumber)
        {
            string? number = RetailRules.Clean(orderNumber, 50)?.ToUpperInvariant();
            return number == null ? null
                : await unitOfWork.DbContext.RetailOrder.AsNoTracking().FirstOrDefaultAsync(o => o.OrderNumber == number && o.RetailCustomerId == me.Id && !o.IsDeleted);
        }

        /// <summary>Sửa hồ sơ khách trong 1 transaction dưới tài khoản system; trả bản đã lưu.</summary>
        private async Task<RetailCustomer?> SaveAsync(Guid id, Action<RetailCustomer> change, string action)
        {
            if (SystemUser() is not { } system)
            {
                return null;
            }
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                using var tran = await unitOfWork.OpenTransaction();
                try
                {
                    var c = await unitOfWork.DbContext.RetailCustomer.FirstAsync(x => x.Id == id);
                    change(c);
                    unitOfWork.RetailCustomerRepository.Update(c);
                    await unitOfWork.RetailCustomerRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    unitOfWork.CommitTransaction();
                    return c;
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat("[", GetType().Name, "]", " ", action, ": ", ex.ToString()));
                    unitOfWork.RollbackTransaction();
                    throw;
                }
            }
        }

        private Guid? SystemUser()
            => Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid g) && g != Guid.Empty ? g : null;

        private RetailSessionModel Session(RetailCustomer c, DateTime now)
        {
            DateTime exp = now.AddDays(_rules.SessionDays);
            return new RetailSessionModel { Token = RetailAccountRules.IssueToken(_key!, c.Id, c.PasswordHash, exp), ExpiresAt = exp, Profile = Profile(c) };
        }

        /// <summary>Trống = cá nhân (tài khoản cũ); chỉ nhận INDIVIDUAL / BUSINESS, sai → null.</summary>
        private static string? NormalizeType(string? type)
        {
            string t = (type ?? string.Empty).Trim().ToUpperInvariant();
            return t.Length == 0 ? TypeIndividual : t is TypeIndividual or TypeBusiness ? t : null;
        }

        /// <summary>
        /// D-136 j: doanh nghiệp bắt buộc tên DN + mã số thuế (10 số hoặc 10-3 số; bỏ khoảng trắng / dấu chấm) + email; cá nhân không lưu 2 trường DN.
        /// </summary>
        private static (string? Company, string? TaxCode, string? Error) Business(string type, string? companyName, string? taxCode, string? email)
        {
            if (type != TypeBusiness)
            {
                return (null, null, null);
            }
            string? company = RetailRules.Clean(companyName, 255);
            if (company == null || company.Length < 2)
            {
                return (null, null, RetailMessage.CompanyNameRequired);
            }
            string tax = (taxCode ?? string.Empty).Replace(" ", string.Empty).Replace(".", string.Empty).Trim();
            if (!TaxCodePattern().IsMatch(tax))
            {
                return (null, null, RetailMessage.TaxCodeInvalid);
            }
            if (email == null)
            {
                return (null, null, RetailMessage.BusinessEmailRequired);
            }
            return (company, tax, null);
        }

        private static RetailCustomerProfileModel Profile(RetailCustomer c) => new()
        {
            AccountType = NormalizeType(c.AccountType) ?? TypeIndividual, CompanyName = c.CompanyName, TaxCode = c.TaxCode,
            FullName = c.FullName ?? string.Empty, PhoneNumber = c.PhoneNumber ?? string.Empty, Email = c.Email, Address = c.Address, CreatedDate = c.CreatedDate,
        };

        private static Result<T> Denied<T>() => new() { ResponseCode = Unauthorized, ResponseMessage = RetailMessage.SessionExpired };

        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
        private static partial Regex EmailPattern();

        /// <summary>Mã số thuế: 10 số, hoặc 10 số - 3 số (chi nhánh) – cùng quy tắc CustomerService.</summary>
        [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
        private static partial Regex TaxCodePattern();
    }
}
