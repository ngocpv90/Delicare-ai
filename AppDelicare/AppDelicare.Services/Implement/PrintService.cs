#nullable enable
using System.Globalization;
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
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.7 (spec 2.5, Catalog 24–30): nội dung các phiếu in – lấy từ GetDetail sẵn có của chứng từ (không tính lại nghiệp vụ), vẽ bằng <see cref="PrintRenderer"/>.
    /// BUSINESS_DOCS.md không có file mẫu thật cho 6 phiếu này → bố cục theo mẫu chứng từ kho phổ biến (01-VT, 02-VT, 05-VT – TT 200/2014/TT-BTC, chỉ tham khảo)
    /// + trường spec yêu cầu; đầu trang = pháp nhân trong bảng Company (PROPOSED – D-56 c). Chỉ đọc.
    /// </summary>
    public class PrintService(IUnitOfWork unitOfWork, ICurrentActor currentActor, IPermissionService permissionService, IOptions<BusinessRulesOptions> rules,
        IOptions<ReferenceListsOptions> referenceLists, IGoodsReceiptService goodsReceiptService, IMaterialIssueService materialIssueService,
        IStockCountService stockCountService, IOrderReconciliationService reconciliationService) : IPrintService
    {
        public const string GoodsReceipt = "GOODS_RECEIPT";
        public const string MaterialIssue = "MATERIAL_ISSUE";
        public const string StockCount = "STOCK_COUNT";
        public const string DeliveryOrder = "DELIVERY_ORDER";
        public const string Pod = "POD";
        public const string Reconciliation = "RECONCILIATION";

        private static readonly Dictionary<string, string> Screens = new(StringComparer.OrdinalIgnoreCase)
        {
            [GoodsReceipt] = "scm-gr", [MaterialIssue] = "scm-issue", [StockCount] = "scm-stockcount",
            [DeliveryOrder] = "log-delivery-order", [Pod] = "log-pod", [Reconciliation] = "crm-reconciliation",
        };

        /// <summary>Tên controller → loại phiếu (client gửi được cả hai).</summary>
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GoodsReceipt"] = GoodsReceipt, ["MaterialIssue"] = MaterialIssue, ["StockCount"] = StockCount,
            ["DeliveryOrder"] = DeliveryOrder, ["ProofOfDelivery"] = Pod, ["OrderReconciliation"] = Reconciliation,
        };

        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly CommonRulesOptions _rules = rules.Value.Common;
        private readonly ReferenceListsOptions _referenceLists = referenceLists.Value;
        private readonly IGoodsReceiptService _goodsReceiptService = goodsReceiptService;
        private readonly IMaterialIssueService _materialIssueService = materialIssueService;
        private readonly IStockCountService _stockCountService = stockCountService;
        private readonly IOrderReconciliationService _reconciliationService = reconciliationService;

        public IReadOnlyDictionary<string, string> TypeScreens => Screens;

        public static string? NormalizeType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return null;
            }
            string t = type.Trim();
            return Screens.ContainsKey(t) ? t.ToUpperInvariant() : Aliases.GetValueOrDefault(t);
        }

        private static string N(decimal? value) => value is { } v ? v.ToString("#,##0.##", Vi) : string.Empty;
        private static string D(DateTime? value) => value is { } v ? v.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;
        private static string DT(DateTime? value) => value is { } v ? v.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : string.Empty;
        private static string Join(params string?[] parts) => string.Join(" – ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        private static PrintInfoRowModel I(string label, string? value) => new() { Label = label, Value = value };

        private async Task<PrintDocumentModel> NewDocAsync(string type, Guid id, string title, string formCode, string? no, DateTime? date)
        {
            var company = await _unitOfWork.DbContext.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();
            return new PrintDocumentModel
            {
                Type = type, Id = id, Title = title, FormCode = formCode, DocumentNo = no, DocumentDate = date,
                CompanyName = company?.Name ?? "DELICARE", CompanyAddress = company?.Address, CompanyPhone = company?.Phone, CompanyTaxCode = company?.TaxCode,
            };
        }

        public async Task<IResult<PrintDocumentModel>> Build(string type, Guid id)
        {
            try
            {
                string? t = NormalizeType(type);
                return t switch
                {
                    GoodsReceipt => await BuildGoodsReceiptAsync(id),
                    MaterialIssue => await BuildMaterialIssueAsync(id),
                    StockCount => await BuildStockCountAsync(id),
                    DeliveryOrder => await BuildDeliveryOrderAsync(id),
                    Pod => await BuildPodAsync(id),
                    Reconciliation => await BuildReconciliationAsync(id),
                    _ => Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintTypeInvalid, type, string.Join(", ", Screens.Keys))),
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Build: ", ex.ToString()));
                throw;
            }
        }

        private static string? Normalize(string? format) => string.IsNullOrWhiteSpace(format) ? null : format.Trim().TrimStart('.').ToLowerInvariant();

        /// <summary>pdf / xlsx / docx; không gửi → <c>BusinessRules:Common:PrintDefaultFormat</c> (cấu hình sai → pdf như spec 2.5) – BE2.7.1, PROPOSED D-56 k.</summary>
        private string? Format(string? format, out string normalized)
        {
            string? fallback = Normalize(_rules.PrintDefaultFormat);
            normalized = Normalize(format) ?? (fallback != null && PrintRenderer.Formats.Contains(fallback) ? fallback : PrintRenderer.Pdf);
            return PrintRenderer.Formats.Contains(normalized) ? null : string.Format(ConstantHelper.Message.CommonFormatInvalid, format, string.Join(", ", PrintRenderer.Formats));
        }

        private static FileContentModel File(IReadOnlyList<PrintDocumentModel> docs, string format, string baseName)
            => new()
            {
                Content = PrintRenderer.Render(docs, format),
                ContentType = PrintRenderer.ContentTypeOf(format),
                FileName = DocumentStorageRules.SafeFileName(baseName + "." + format),
            };

        public async Task<IResult<FileContentModel>> Print(string type, Guid id, string? format)
        {
            try
            {
                if (Format(format, out string f) is { } bad)
                {
                    return Result<FileContentModel>.Fail(bad);
                }
                var built = await Build(type, id);
                if (built.ResponseCode != 200 || built.Data == null)
                {
                    return Result<FileContentModel>.Fail(built.ResponseMessage ?? ConstantHelper.Message.RecordNotFoundMessage);
                }
                var doc = built.Data;
                return Result<FileContentModel>.Success(File([doc], f, $"{doc.Type}_{doc.DocumentNo ?? id.ToString("N")}"), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Print: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<FileContentModel>> PrintBatch(PrintBatchRequest request)
        {
            try
            {
                if (Format(request.Format, out string f) is { } bad)
                {
                    return Result<FileContentModel>.Fail(bad);
                }
                var items = (request.Items ?? []).Select(i => (Type: NormalizeType(i.Type), Raw: i.Type, i.Id)).ToList();
                var invalid = items.FirstOrDefault(i => i.Type == null);
                if (items.Count > 0 && invalid != default)
                {
                    return Result<FileContentModel>.Fail(string.Format(ConstantHelper.Message.PrintTypeInvalid, invalid.Raw, string.Join(", ", Screens.Keys)));
                }
                if (request.DeliveryRouteId is { } routeId)
                {
                    var db = _unitOfWork.DbContext;
                    var stops = await db.DeliveryStop.AsNoTracking().Where(s => s.DeliveryRouteId == routeId && !s.IsDeleted && s.DeliveryOrderId != null)
                        .OrderBy(s => s.SequenceNo).Select(s => s.DeliveryOrderId!.Value).ToListAsync();
                    var others = await db.DeliveryOrder.AsNoTracking().Where(o => o.DeliveryRouteId == routeId && !o.IsDeleted && o.Status != DeliveryOrderStatus.Cancelled)
                        .OrderBy(o => o.PlannedDeliveryTime).Select(o => o.Id).ToListAsync();
                    foreach (var doId in stops.Concat(others).Distinct())
                    {
                        items.Add((DeliveryOrder, DeliveryOrder, doId));
                    }
                }
                items = items.DistinctBy(i => (i.Type, i.Id)).ToList();
                if (items.Count == 0)
                {
                    return Result<FileContentModel>.Fail(ConstantHelper.Message.PrintBatchEmpty);
                }
                int max = Math.Max(1, _rules.PrintBatchMaxItems);
                if (items.Count > max)
                {
                    return Result<FileContentModel>.Fail(string.Format(ConstantHelper.Message.PrintBatchTooMany, max, items.Count));
                }
                // Quyền In theo từng loại phiếu (spec: "Quyền: theo từng phiếu").
                foreach (var screen in items.Select(i => Screens[i.Type!]).Distinct())
                {
                    if (await CommonAccess.DenyAsync(_permissionService, _currentActor, screen, PermissionAction.Print) is { } deny)
                    {
                        return new Result<FileContentModel> { ResponseCode = CommonAccess.Forbidden, ResponseMessage = deny };
                    }
                }
                var docs = new List<PrintDocumentModel>();
                var errors = new List<string>();
                foreach (var item in items)
                {
                    var built = await Build(item.Type!, item.Id);
                    if (built.ResponseCode == 200 && built.Data != null)
                    {
                        docs.Add(built.Data);
                    }
                    else
                    {
                        errors.Add(built.ResponseMessage ?? item.Id.ToString());
                    }
                }
                if (errors.Count > 0)
                {
                    return Result<FileContentModel>.Fail(string.Format(ConstantHelper.Message.PrintBatchFailed, string.Join("; ", errors.Distinct())));
                }
                return Result<FileContentModel>.Success(File(docs, f, $"In_hang_loat_{docs.Count}_phieu"), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " PrintBatch: ", ex.ToString()));
                throw;
            }
        }

        // ---------------------------------------------------------------- 24. Phiếu nhập kho

        private async Task<IResult<PrintDocumentModel>> BuildGoodsReceiptAsync(Guid id)
        {
            var result = await _goodsReceiptService.GetDetail(id);
            var detail = result.Data;
            if (result.ResponseCode != 200 || detail?.GoodsReceipt == null)
            {
                return Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintNotFound, "phiếu nhập kho"));
            }
            var gr = detail.GoodsReceipt;
            string no = gr.ReceiptNumber ?? gr.ReceiptNo ?? id.ToString("N")[..8];
            // Spec 2.5: chỉ in khi QC đã kết luận.
            if (string.IsNullOrEmpty(detail.QcResult) || string.Equals(gr.Status, GoodsReceiptStatus.QcPending, StringComparison.OrdinalIgnoreCase))
            {
                return Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintGoodsReceiptNoQc, no));
            }
            var doc = await NewDocAsync(GoodsReceipt, id, "PHIẾU NHẬP KHO", "Mẫu số 01 - VT (tham khảo)", no, gr.ReceiptDate);
            doc.ListInfo.AddRange([
                I("Nhà cung cấp", Join(detail.SupplierCode, detail.SupplierName)),
                I("Theo đơn mua", detail.PurchaseOrderNumber),
                I("Nhập tại kho", detail.WarehouseName),
                I("Kết luận QC", Join(detail.InspectionNumber, detail.QcResultName ?? detail.QcResult)),
                I("Trạng thái", detail.StatusName ?? gr.Status),
                I("Ghi chú", gr.Note),
            ]);
            doc.ListHeader.AddRange(["STT", "Mã NL", "Tên nguyên liệu", "ĐVT", "Số lô NCC", "NSX", "HSD", "SL đặt", "SL nhận", "SL đạt", "SL loại", "Đơn giá", "Thành tiền", "QC"]);
            int i = 0;
            decimal total = 0m;
            foreach (var l in detail.ListLine)
            {
                decimal amount = l.AcceptedQuantity * (l.UnitCost ?? 0m);
                total += amount;
                doc.ListRow.Add([(++i).ToString(CultureInfo.InvariantCulture), l.IngredientCode ?? string.Empty, l.IngredientName ?? string.Empty, l.UnitName ?? string.Empty,
                    l.SupplierLotNumber ?? string.Empty, D(l.ManufactureDate), D(l.ExpiryDate), N(l.OrderedQuantity), N(l.ReceivedQuantity), N(l.AcceptedQuantity),
                    N(l.RejectedQuantity), N(l.UnitCost), N(amount), l.QualityStatusName ?? l.QualityStatus ?? string.Empty]);
            }
            doc.ListFooter.Add(["", "", "Cộng", "", "", "", "", N(detail.ListLine.Sum(l => l.OrderedQuantity ?? 0m)), N(detail.ListLine.Sum(l => l.ReceivedQuantity)),
                N(detail.ListLine.Sum(l => l.AcceptedQuantity)), N(detail.ListLine.Sum(l => l.RejectedQuantity)), "", N(total), ""]);
            doc.ListNote.Add("Thành tiền = số lượng đạt × đơn giá (chưa VAT).");
            doc.ListSignature.AddRange(["Người lập phiếu", "Người giao hàng", "Nhân viên QC", "Thủ kho", "Kế toán"]);
            return Result<PrintDocumentModel>.Success(doc, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ---------------------------------------------------------------- 25. Phiếu xuất kho

        private async Task<IResult<PrintDocumentModel>> BuildMaterialIssueAsync(Guid id)
        {
            var result = await _materialIssueService.GetDetail(id);
            var detail = result.Data;
            if (result.ResponseCode != 200 || detail?.MaterialIssue == null)
            {
                return Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintNotFound, "phiếu xuất kho"));
            }
            var mi = detail.MaterialIssue;
            string no = mi.IssueNumber ?? mi.IssueNo ?? id.ToString("N")[..8];
            if (!string.Equals(mi.Status, MaterialIssueStatus.Issued, StringComparison.OrdinalIgnoreCase))
            {
                return Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintIssueNotIssued, no));
            }
            var db = _unitOfWork.DbContext;
            var po = await db.ProductionOrder.AsNoTracking().FirstOrDefaultAsync(o => o.Id == mi.ProductionOrderId);
            string? dish = po == null ? null : await db.Dish.AsNoTracking().Where(x => x.Id == po.DishId).Select(x => x.Name).FirstOrDefaultAsync();
            var doc = await NewDocAsync(MaterialIssue, id, "PHIẾU XUẤT KHO", "Mẫu số 02 - VT (tham khảo)", no, mi.IssueDate);
            doc.ListInfo.AddRange([
                I("Lệnh sản xuất", Join(po?.ProductionOrderNumber ?? po?.ProductionOrderNo, dish)),
                I("Xuất tại kho", detail.WarehouseName),
                I("Lý do xuất", "Xuất nguyên liệu cho sản xuất"),
                I("Trạng thái", detail.StatusName ?? mi.Status),
                I("Ghi chú", mi.Note),
            ]);
            doc.ListHeader.AddRange(["STT", "Mã NL", "Tên nguyên liệu", "ĐVT", "Số lô", "HSD", "Số lượng xuất", "Ghi chú"]);
            int i = 0;
            foreach (var l in detail.ListLine)
            {
                doc.ListRow.Add([(++i).ToString(CultureInfo.InvariantCulture), l.IngredientCode ?? string.Empty, l.IngredientName ?? string.Empty, l.UnitName ?? string.Empty,
                    l.LotNumber ?? string.Empty, D(l.ExpiryDate), N(l.Quantity), l.Note ?? string.Empty]);
            }
            doc.ListFooter.Add(["", "", "Cộng", "", "", "", N(detail.ListLine.Sum(l => l.Quantity)), ""]);
            doc.ListSignature.AddRange(["Người lập phiếu", "Thủ kho", "Người nhận (bếp)", "Kế toán"]);
            return Result<PrintDocumentModel>.Success(doc, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ---------------------------------------------------------------- 26. Biên bản kiểm kê

        private async Task<IResult<PrintDocumentModel>> BuildStockCountAsync(Guid id)
        {
            var result = await _stockCountService.GetDetail(id);
            var detail = result.Data;
            if (result.ResponseCode != 200 || detail?.StockCount == null)
            {
                return Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintNotFound, "phiếu kiểm kê"));
            }
            var sc = detail.StockCount;
            string no = sc.CountNumber ?? sc.CountNo ?? id.ToString("N")[..8];
            var doc = await NewDocAsync(StockCount, id, "BIÊN BẢN KIỂM KÊ KHO", "Mẫu số 05 - VT (tham khảo)", no, sc.CountDate);
            // Spec 2.5: mọi trạng thái; chưa ghi sổ (Posted) → "BẢN NHÁP".
            doc.IsDraft = !string.Equals(sc.Status, StockCountStatus.Posted, StringComparison.OrdinalIgnoreCase);
            var db = _unitOfWork.DbContext;
            string? responsible = sc.ResponsibleEmployeeId is { } e ? await db.Employee.AsNoTracking().Where(x => x.Id == e).Select(x => x.FullName).FirstOrDefaultAsync() : null;
            doc.ListInfo.AddRange([
                I("Kho", detail.WarehouseName),
                I("Loại kiểm kê", sc.CountType),
                I("Người phụ trách", responsible),
                I("Trạng thái", detail.StatusName ?? sc.Status),
                I("Ngưỡng lệch cần duyệt", N(detail.VarianceThresholdPercent) + " %"),
                I("Ghi chú", sc.Note),
            ]);
            doc.ListHeader.AddRange(["STT", "Mã NL", "Tên nguyên liệu", "ĐVT", "Số lô", "HSD", "Tồn sổ", "Thực tế", "Chênh lệch", "% lệch", "Giá trị lệch", "Lý do"]);
            int i = 0;
            foreach (var l in detail.ListLine)
            {
                doc.ListRow.Add([(++i).ToString(CultureInfo.InvariantCulture), l.IngredientCode ?? string.Empty, l.IngredientName ?? string.Empty, l.UnitName ?? string.Empty,
                    l.LotNumber ?? string.Empty, D(l.ExpiryDate), N(l.SystemQuantity), N(l.PhysicalQuantity), N(l.VarianceQuantity), N(l.VariancePercent),
                    N(l.VarianceValue), l.VarianceReason ?? string.Empty]);
            }
            doc.ListFooter.Add(["", "", "Cộng", "", "", "", "", "", "", "", N(detail.TotalVarianceValue), ""]);
            doc.ListSignature.AddRange(["Người kiểm kê", "Thủ kho", "Kế toán", "Người duyệt"]);
            return Result<PrintDocumentModel>.Success(doc, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ---------------------------------------------------------------- 27. Phiếu giao hàng (theo đơn giao)

        private async Task<IResult<PrintDocumentModel>> BuildDeliveryOrderAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var dorder = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
            if (dorder == null)
            {
                return Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintNotFound, "phiếu giao hàng"));
            }
            var (customer, location, order, route, vehicle, driver) = await DeliveryContextAsync(dorder);
            var execution = await db.DeliveryExecution.AsNoTracking().Where(e => e.DeliveryOrderId == id && !e.IsDeleted).OrderByDescending(e => e.CreatedDate).FirstOrDefaultAsync();
            var meals = (await DeliveryRules.DeliveryOrderMealsAsync(db, [dorder])).GetValueOrDefault(dorder.Id);
            var lines = order == null ? [] : await db.OrderLine.AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted).ToListAsync();
            var shiftIds = lines.Where(l => l.ShiftId != null).Select(l => l.ShiftId!.Value).Distinct().ToList();
            var shifts = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
            var mealTypes = _referenceLists.Lists.GetValueOrDefault("MealType") ?? [];

            string no = dorder.DeliveryNumber ?? dorder.DeliveryNo ?? id.ToString("N")[..8];
            var doc = await NewDocAsync(DeliveryOrder, id, "PHIẾU GIAO HÀNG", "Phiếu giao suất ăn", no, dorder.DeliveryDate);
            string window = location?.DeliveryWindowFrom is { } wf ? $"{wf:hh\\:mm}" + (location.DeliveryWindowTo is { } wt ? $" – {wt:hh\\:mm}" : string.Empty) : string.Empty;
            doc.ListInfo.AddRange([
                I("Khách hàng", customer?.Name),
                I("Đơn hàng", order?.OrderNumber ?? order?.OrderNo),
                I("Điểm giao", location?.Name),
                I("Địa chỉ", location?.Address ?? customer?.Address),
                I("Người liên hệ", Join(location?.ContactName, location?.ContactPhone)),
                I("Khung giờ nhận", window),
                I("Giờ giao dự kiến", DT(dorder.PlannedDeliveryTime)),
                I("Chuyến", route?.RouteNo ?? route?.Code),
                I("Xe", Join(vehicle?.Code ?? execution?.VehicleCode, vehicle?.LicensePlate)),
                I("Tài xế", driver ?? execution?.DriverName),
            ]);
            doc.ListHeader.AddRange(["STT", "Bữa", "Ca", "Số suất đặt", "Ghi chú"]);
            int i = 0;
            foreach (var l in lines.OrderBy(l => l.MealType).ThenBy(l => l.CreatedDate))
            {
                string meal = mealTypes.FirstOrDefault(m => m.Code.Equals(l.MealType ?? string.Empty, StringComparison.OrdinalIgnoreCase))?.Label ?? l.MealType ?? string.Empty;
                doc.ListRow.Add([(++i).ToString(CultureInfo.InvariantCulture), meal, l.ShiftId is { } sid ? shifts.GetValueOrDefault(sid) ?? string.Empty : string.Empty,
                    N(l.Quantity), l.Note ?? string.Empty]);
            }
            doc.ListFooter.Add(["", "Tổng suất của đơn", "", N(lines.Sum(l => l.Quantity)), ""]);
            doc.ListFooter.Add(["", "Số suất giao theo phiếu này", "", N(meals), ""]);
            doc.ListNote.Add("Suất nhận / từ chối, nhiệt độ bàn giao và chữ ký người nhận ghi tại điểm giao (biên bản giao nhận – POD).");
            if (location?.DeliveryInstructions is { Length: > 0 } ins)
            {
                doc.ListNote.Add("Hướng dẫn giao: " + ins);
            }
            doc.ListSignature.AddRange(["Người giao (tài xế)", "Người nhận", "Giờ nhận / nhiệt độ (°C)"]);
            return Result<PrintDocumentModel>.Success(doc, ConstantHelper.Message.GetAllSuccessfully);
        }

        private async Task<(Customer? Customer, CustomerDeliveryLocation? Location, Order? Order, DeliveryRoute? Route, Vehicle? Vehicle, string? Driver)> DeliveryContextAsync(AppDelicare.Model.Entity.DeliveryOrder d)
        {
            var db = _unitOfWork.DbContext;
            var customer = await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == d.CustomerId);
            Guid? locationId = d.CustomerDeliveryLocationId ?? d.DeliveryLocationId;
            var location = locationId is { } lid ? await db.CustomerDeliveryLocation.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lid) : null;
            var order = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == d.OrderId);
            var route = d.DeliveryRouteId is { } rid ? await db.DeliveryRoute.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid) : null;
            var vehicle = route?.VehicleId is { } vid ? await db.Vehicle.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid) : null;
            string? driver = route?.DriverId is { } did ? await db.Employee.AsNoTracking().Where(e => e.Id == did).Select(e => e.FullName).FirstOrDefaultAsync() : null;
            return (customer, location, order, route, vehicle, driver);
        }

        // ---------------------------------------------------------------- 28. Biên bản giao nhận (POD)

        private async Task<IResult<PrintDocumentModel>> BuildPodAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var pod = await db.ProofOfDelivery.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            var dorder = pod == null ? null : await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(o => o.Id == pod.DeliveryOrderId);
            if (pod == null || dorder == null)
            {
                return Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintNotFound, "biên bản giao nhận (POD)"));
            }
            var (customer, location, order, route, vehicle, driver) = await DeliveryContextAsync(dorder);
            var stop = await db.DeliveryStop.AsNoTracking().FirstOrDefaultAsync(s => s.DeliveryOrderId == dorder.Id && !s.IsDeleted);
            var handoverTemp = stop == null ? null : await db.DeliveryEvent.AsNoTracking()
                .Where(e => e.DeliveryStopId == stop.Id && !e.IsDeleted && e.TemperatureC != null).OrderByDescending(e => e.EventAt).Select(e => e.TemperatureC).FirstOrDefaultAsync();
            string no = dorder.DeliveryNumber ?? dorder.DeliveryNo ?? id.ToString("N")[..8];
            var doc = await NewDocAsync(Pod, id, "BIÊN BẢN GIAO NHẬN SUẤT ĂN", "POD", no, pod.ReceivedAt ?? pod.ConfirmedAt);
            decimal? delivered = pod.DeliveredQuantity ?? pod.DeliveredQty;
            decimal? accepted = pod.AcceptedQuantity ?? pod.AcceptedQty;
            decimal? rejected = pod.RejectedQuantity ?? pod.RejectedQty;
            doc.ListInfo.AddRange([
                I("Bên giao", doc.CompanyName),
                I("Bên nhận", customer?.Name),
                I("Đơn hàng", order?.OrderNumber ?? order?.OrderNo),
                I("Điểm giao", Join(location?.Name, location?.Address)),
                I("Chuyến / xe", Join(route?.RouteNo ?? route?.Code, vehicle?.Code, vehicle?.LicensePlate)),
                I("Tài xế", driver),
                I("Thời điểm nhận", DT(pod.ReceivedAt)),
                I("Xác nhận lúc", DT(pod.ConfirmedAt)),
                I("Người nhận", Join(pod.ReceiverName, pod.ReceiverPhone)),
                I("Nhiệt độ bàn giao (°C)", N(handoverTemp)),
            ]);
            doc.ListHeader.AddRange(["Nội dung", "Số suất giao", "Số suất nhận", "Số suất từ chối", "Ghi chú"]);
            doc.ListRow.Add(["Suất ăn theo phiếu giao " + no, N(delivered), N(accepted), N(rejected), pod.Remark ?? string.Empty]);
            // Ảnh chữ ký / ảnh giao hàng: in đường dẫn tham chiếu (tệp xem trên hệ thống) – chưa nhúng ảnh (PROPOSED – D-56 d).
            doc.ListNote.Add(string.IsNullOrWhiteSpace(pod.SignatureUrl) ? "Chữ ký điện tử: chưa có – ký tay vào ô bên dưới." : "Chữ ký điện tử: đã lưu trên hệ thống (" + pod.SignatureUrl + ").");
            if (!string.IsNullOrWhiteSpace(pod.PhotoUrl))
            {
                doc.ListNote.Add("Ảnh giao hàng: " + pod.PhotoUrl);
            }
            doc.ListSignature.AddRange(["Đại diện bên giao", "Đại diện bên nhận"]);
            return Result<PrintDocumentModel>.Success(doc, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ---------------------------------------------------------------- 29. Biên bản đối soát

        private async Task<IResult<PrintDocumentModel>> BuildReconciliationAsync(Guid id)
        {
            var result = await _reconciliationService.GetDetail(id);
            var detail = result.Data;
            if (result.ResponseCode != 200 || detail?.Reconciliation == null)
            {
                return Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintNotFound, "đối soát"));
            }
            var r = detail.Reconciliation;
            if (string.Equals(r.Status, ReconciliationStatus.Open, StringComparison.OrdinalIgnoreCase))
            {
                return Result<PrintDocumentModel>.Fail(string.Format(ConstantHelper.Message.PrintReconciliationOpen, detail.OrderNumber));
            }
            var doc = await NewDocAsync(Reconciliation, id, "BIÊN BẢN ĐỐI SOÁT SUẤT ĂN", "Đối soát Đặt – Sản xuất – Đóng gói – Giao – Nhận", detail.OrderNumber, r.ReconciliationDate);
            doc.ListInfo.AddRange([
                I("Khách hàng", detail.CustomerName),
                I("Đơn hàng", detail.OrderNumber),
                I("Ngày ăn", D(detail.MealDate)),
                I("Trạng thái", detail.StatusName ?? r.Status),
                I("Cơ sở tính tiền", detail.BillingBasis),
                I("Số suất tính tiền", N(detail.BillingQuantity)),
            ]);
            doc.ListHeader.AddRange(["Đặt", "Sản xuất", "Đóng gói", "Giao", "Nhận (POD)", "Chênh lệch", "% chênh lệch", "Giá trị chốt"]);
            decimal ordered = r.OrderedQuantity;
            doc.ListRow.Add([N(ordered), N(r.ProducedQty), N(r.PackedQty), N(r.DeliveredQuantity), N(r.AcceptedQuantity),
                N(r.VarianceQty ?? (r.AcceptedQuantity - ordered)), N(r.VariancePercent), N(r.FinalAmount)]);
            if (!string.IsNullOrWhiteSpace(r.Remark ?? r.Note))
            {
                doc.ListNote.Add("Ghi chú: " + (r.Remark ?? r.Note));
            }
            doc.ListNote.Add("Giá trị chốt là căn cứ lập hóa đơn trên phần mềm kế toán (MISA – D-23).");
            doc.ListSignature.AddRange([$"Đại diện {doc.CompanyName}", "Đại diện khách hàng"]);
            return Result<PrintDocumentModel>.Success(doc, ConstantHelper.Message.GetAllSuccessfully);
        }
    }
}
