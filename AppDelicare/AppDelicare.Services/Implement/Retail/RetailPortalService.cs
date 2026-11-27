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
    /// RETAIL-1: trang khách lẻ (không đăng nhập). Đơn WEB ghi dưới tài khoản <c>system</c> (SystemUsers:JobUserId – như bot),
    /// IP + mã lần gửi lưu trên đơn để chống spam / chống trùng. Không lộ giá vốn, không trả địa chỉ khi tra cứu.
    /// </summary>
    public class RetailPortalService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules,
        IOptions<ReferenceListsOptions> referenceLists, IConfiguration configuration, IMemoryCache cache, IDocumentNumberService documentNumbers,
        INotificationService notifications) : IRetailPortalService
    {
        public const int TooManyRequests = 429;
        private readonly RetailRulesOptions _rules = rules.Value.Retail;
        private readonly ReferenceListsOptions _lists = referenceLists.Value;

        public async Task<IResult<RetailCatalogModel>> GetCatalog()
        {
            try
            {
                var db = unitOfWork.DbContext;
                var products = await db.RetailProduct.AsNoTracking().Where(p => !p.IsDeleted && p.Status == RetailCodes.ProductActive)
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync();
                var cats = RetailRules.Categories(_lists);
                return Result<RetailCatalogModel>.Success(new RetailCatalogModel
                {
                    Categories = cats.Where(c => products.Any(p => p.CategoryCode == c.Code)).Select(c => new RetailCategoryModel { Code = c.Code, Name = c.Label }).ToList(),
                    Products = products.Select(p => new RetailCatalogProductModel
                    {
                        Id = p.Id, Code = p.Code ?? string.Empty, CategoryCode = p.CategoryCode ?? string.Empty, Name = p.Name ?? string.Empty, Unit = p.Unit ?? string.Empty,
                        Price = p.Price, Description = p.Description, Tags = RetailRules.Tags(p.Tags), MealTypes = RetailRules.MealsOf(p.MealTypes, _lists),
                        Icon = p.Icon, ImageUrl = p.ImageUrl,
                    }).ToList(),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCatalog: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Thực đơn CHUNG (Menu.MenuType = COMMON) đã công bố của tuần chứa hôm nay (Chủ Nhật → tuần sau). Chỉ tên món theo bữa.
        /// Không có → Data = null (trang dùng thực đơn trong file cấu hình). PROPOSED – BA/PO CONFIRM.
        /// </summary>
        public async Task<IResult<RetailWeeklyMenuModel>> GetWeeklyMenu()
        {
            try
            {
                var db = unitOfWork.DbContext;
                DateTime today = clock.Today;
                DateTime monday = today.DayOfWeek == DayOfWeek.Sunday ? today.AddDays(1) : today.AddDays(-((int)today.DayOfWeek - 1));
                DateTime sunday = monday.AddDays(6);
                var menu = await db.Menu.AsNoTracking()
                    .Where(m => !m.IsDeleted && m.Status == MenuStatus.Published && m.MenuType == MenuScope.Common
                        && (m.EffectiveFrom == null || m.EffectiveFrom <= sunday) && (m.EffectiveTo == null || m.EffectiveTo >= monday))
                    .OrderByDescending(m => m.EffectiveFrom).ThenByDescending(m => m.VersionNo).FirstOrDefaultAsync();
                if (menu == null)
                {
                    return Result<RetailWeeklyMenuModel>.Success(null, ConstantHelper.Message.GetAllSuccessfully);
                }
                var lines = await (from l in db.MenuLine.AsNoTracking()
                                   join d in db.Dish.AsNoTracking() on l.DishId equals d.Id
                                   where l.MenuId == menu.Id && !l.IsDeleted && !d.IsDeleted && l.DayOfWeek >= 1 && l.DayOfWeek <= 7
                                   orderby l.DayOfWeek, l.SequenceNo, l.SortOrder
                                   select new { l.DayOfWeek, l.MealType, d.Name }).ToListAsync();
                var mealOrder = RetailRules.Meals(_lists).Select(m => m.Label).ToList();
                var days = new List<RetailWeeklyMenuDayModel>();
                for (int i = 0; i < 7; i++)
                {
                    DateTime date = monday.AddDays(i);
                    if (menu.EffectiveFrom is { } f && date < f.Date || menu.EffectiveTo is { } t && date > t.Date)
                    {
                        continue;
                    }
                    var meals = lines.Where(l => l.DayOfWeek == i + 1 && !string.IsNullOrWhiteSpace(l.Name))
                        .GroupBy(l => RetailRules.ResolveMeal(l.MealType, _lists) ?? l.MealType ?? string.Empty)
                        .Where(g => g.Key.Length > 0)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).Distinct().ToList());
                    if (meals.Count > 0)
                    {
                        days.Add(new RetailWeeklyMenuDayModel { Date = RetailRules.Iso(date), Day = RetailRules.Day(date), Meals = meals });
                    }
                }
                if (days.Count == 0)
                {
                    return Result<RetailWeeklyMenuModel>.Success(null, ConstantHelper.Message.GetAllSuccessfully);
                }
                var used = days.SelectMany(d => d.Meals.Keys).Distinct().ToList();
                return Result<RetailWeeklyMenuModel>.Success(new RetailWeeklyMenuModel
                {
                    Title = $"Thực đơn tuần {monday:dd/MM} – {sunday:dd/MM/yyyy}",
                    Meals = [.. mealOrder.Where(used.Contains), .. used.Where(u => !mealOrder.Contains(u))],
                    Days = days,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWeeklyMenu: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<RetailSubmitResultModel>> SubmitOrder(RetailSubmitOrderRequest request, string? clientIp, Guid? retailCustomerId = null)
        {
            // Ô bẫy có giá trị = máy gửi tự động → trả "thành công" giả, không ghi gì.
            if (!string.IsNullOrWhiteSpace(request.Website))
            {
                Log.Warning("[RetailPortal] Bỏ đơn có ô bẫy từ {Ip}", clientIp);
                return Result<RetailSubmitResultModel>.Success(new RetailSubmitResultModel(), RetailMessage.OrderSubmitted);
            }
            var db = unitOfWork.DbContext;
            string? requestId = RetailRules.Clean(request.ClientRequestId, 64);
            DateTime now = clock.Now;
            if (requestId != null)
            {
                DateTime since = now.AddHours(-_rules.DuplicateRequestHours);
                var dup = await db.RetailOrder.AsNoTracking()
                    .Where(o => o.ClientRequestId == requestId && o.CreatedDate >= since && !o.IsDeleted).FirstOrDefaultAsync();
                if (dup != null)
                {
                    return Result<RetailSubmitResultModel>.Success(ToSubmitResult(dup), RetailMessage.OrderSubmitted);
                }
            }
            var (order, error) = await BuildAsync(request, now);
            if (order == null)
            {
                return Result<RetailSubmitResultModel>.Fail(error!);
            }
            string ip = RetailRules.Clean(clientIp, 64) ?? "-";
            DateTime window = now.AddMinutes(-_rules.LimitWindowMinutes);
            int byPhone = await db.RetailOrder.AsNoTracking().CountAsync(o => o.PhoneNumber == order.Value.Phone && o.Channel == RetailCodes.ChannelWeb && o.CreatedDate >= window);
            int byIp = ip == "-" ? 0 : await db.RetailOrder.AsNoTracking().CountAsync(o => o.ClientIp == ip && o.Channel == RetailCodes.ChannelWeb && o.CreatedDate >= window);
            if (byPhone >= _rules.SubmitLimitPerPhone || byIp >= _rules.SubmitLimitPerIp)
            {
                Log.Warning("[RetailPortal] Vượt giới hạn gửi đơn: SĐT {Count1}, IP {Ip} {Count2}", byPhone, ip, byIp);
                return new Result<RetailSubmitResultModel> { ResponseCode = TooManyRequests, ResponseMessage = RetailMessage.TooManyRequests };
            }
            if (!Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid system) || system == Guid.Empty)
            {
                Log.Error("[RetailPortal] Chưa cấu hình SystemUsers:JobUserId – không nhận được đơn trên trang web");
                return Result<RetailSubmitResultModel>.Fail(RetailMessage.JobUserMissing);
            }
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                using var tran = await unitOfWork.OpenTransaction();
                try
                {
                    var o = order.Value;
                    var entity = new RetailOrder
                    {
                        Id = Guid.NewGuid(),
                        OrderNumber = await documentNumbers.NextAsync(DocumentKind.RetailOrder, now),
                        Channel = RetailCodes.ChannelWeb,
                        CustomerName = o.Name, PhoneNumber = o.Phone, Address = o.Address, DeliveryDate = o.Date, MealType = o.Meal,
                        Note = RetailRules.Clean(request.Note, 1000),
                        OrderStatus = RetailOrderStatus.New,
                        SubTotal = o.Lines.Sum(l => l.LineAmount), ShippingFee = 0, TotalAmount = o.Lines.Sum(l => l.LineAmount),
                        PaymentMethod = RetailCodes.PayCod, PaymentStatus = RetailCodes.Unpaid,
                        ClientIp = ip, ClientRequestId = requestId,
                        RetailCustomerId = retailCustomerId, // RETAIL-2: khách đã đăng nhập → Đơn của tôi
                    };
                    unitOfWork.RetailOrderRepository.Add(entity);
                    foreach (var l in o.Lines)
                    {
                        unitOfWork.RetailOrderLineRepository.Add(RetailRules.ToEntity(l, entity.Id));
                    }
                    await unitOfWork.RetailOrderRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    if (_rules.NotifyNewOrder)
                    {
                        await RetailNotify.NewOrderAsync(notifications, db, entity, o.Lines);
                    }
                    unitOfWork.CommitTransaction();
                    Log.Information("[RetailPortal] Đơn khách lẻ mới {OrderNumber} ({Total})", entity.OrderNumber, entity.TotalAmount);
                    return Result<RetailSubmitResultModel>.Success(ToSubmitResult(entity), RetailMessage.OrderSubmitted);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat("[", GetType().Name, "]", " SubmitOrder: ", ex.ToString()));
                    unitOfWork.RollbackTransaction();
                    throw;
                }
            }
        }

        public async Task<IResult<RetailMyOrderDetailModel>> TrackOrder(string? orderNumber, string? phoneNumber, string? clientIp)
        {
            string key = "retail-track:" + (clientIp ?? "-");
            int count = cache.TryGetValue(key, out int c) ? c : 0;
            if (count >= _rules.TrackLimitPerIp)
            {
                return new Result<RetailMyOrderDetailModel> { ResponseCode = TooManyRequests, ResponseMessage = RetailMessage.TrackTooMany };
            }
            cache.Set(key, count + 1, TimeSpan.FromMinutes(_rules.LimitWindowMinutes));
            string? number = RetailRules.Clean(orderNumber, 50)?.ToUpperInvariant();
            string? phone = RetailRules.NormalizePhone(phoneNumber);
            if (number == null || phone == null)
            {
                return Result<RetailMyOrderDetailModel>.Fail(RetailMessage.TrackNotFound);
            }
            try
            {
                var db = unitOfWork.DbContext;
                // Sai mã hay sai SĐT đều báo như nhau (không cho dò số đơn của người khác).
                var o = await db.RetailOrder.AsNoTracking().FirstOrDefaultAsync(x => x.OrderNumber == number && x.PhoneNumber == phone && !x.IsDeleted);
                if (o == null)
                {
                    return Result<RetailMyOrderDetailModel>.Fail(RetailMessage.TrackNotFound);
                }
                // RETAIL-2: thêm tiến độ + chuyến giao; KHÔNG trả địa chỉ / ghi chú (tra cứu công khai chỉ cần số đơn + SĐT).
                return Result<RetailMyOrderDetailModel>.Success(await RetailProgress.DetailAsync(db, o, _lists), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " TrackOrder: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Kiểm thông tin đơn WEB (tên, SĐT, địa chỉ, ngày, bữa, dòng) – trả bản đã chuẩn hóa hoặc lỗi.</summary>
        private async Task<((string Name, string Phone, string Address, DateTime Date, string Meal, List<RetailRules.PricedLine> Lines)? Order, string? Error)> BuildAsync(
            RetailSubmitOrderRequest request, DateTime now)
        {
            string? name = RetailRules.Clean(request.CustomerName, 200);
            if (name == null || name.Length < 2)
            {
                return (null, RetailMessage.NameRequired);
            }
            string? phone = RetailRules.NormalizePhone(request.PhoneNumber);
            if (phone == null)
            {
                return (null, RetailMessage.PhoneInvalid);
            }
            string? address = RetailRules.Clean(request.Address, 500);
            if (address == null || address.Length < 5)
            {
                return (null, RetailMessage.AddressRequired);
            }
            if (RetailRules.ParseDate(request.DeliveryDate) is not { } date)
            {
                return (null, RetailMessage.DateInvalid);
            }
            if (RetailRules.CheckDate(date, now, _rules) is { } dateError)
            {
                return (null, dateError);
            }
            string? meal = RetailRules.ResolveMeal(request.MealType, _lists);
            if (meal == null)
            {
                return (null, string.Format(RetailMessage.MealInvalid, string.Join(", ", RetailRules.Meals(_lists).Select(m => m.Label))));
            }
            var (lines, lineError) = await RetailRules.PriceLinesAsync(unitOfWork.DbContext, request.Lines, meal, _rules, _lists);
            if (lineError != null)
            {
                return (null, lineError);
            }
            return ((name, phone, address, date, meal, lines), null);
        }

        private static RetailSubmitResultModel ToSubmitResult(RetailOrder o) => new()
        {
            Id = o.Id, OrderNumber = o.OrderNumber ?? string.Empty, TotalAmount = o.TotalAmount, DeliveryDate = RetailRules.Iso(o.DeliveryDate), MealType = o.MealType ?? string.Empty,
        };
    }
}
