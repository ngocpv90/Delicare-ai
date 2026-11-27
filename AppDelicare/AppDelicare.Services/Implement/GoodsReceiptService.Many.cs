using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppDelicare.Services.Implement
{
    public partial class GoodsReceiptService
    {
        private readonly IServiceProvider _services = services;

        private sealed record Part(Guid PoId, string? PoNo, int PoOrder, PurchaseOrderLine Line, decimal Remaining)
        {
            public decimal Qty { get; set; }
            public string? Result { get; set; }
            public decimal? Accepted { get; set; }
        }

        /// <summary>
        /// UI-6.50 (D-132): nhận gộp nhiều đơn mua của 1 NCC. Mỗi dòng gộp (nguyên liệu) chia số nhận về từng dòng đơn mua – <b>đơn cũ trước</b>, không vượt "còn phải nhận";
        /// phần dư dồn vào đơn mới nhất (kiểm "nhận vượt" như GoodsReceipt/Create). Mỗi đơn mua <b>một</b> phiếu nhập (luật Create giữ nguyên) trong <b>một giao dịch</b> – lỗi bất kỳ
        /// → không lập phiếu nào. Nhiệt độ dòng trống + hàng mát / đông → nhiệt độ thùng xe. Lô / hạn dùng chỉ nhận khi nguyên liệu quản lý lô / hạn (khác → lô nội bộ, hạn tự tính).
        /// Số hóa đơn NCC → ghi chú "[HĐ NCC] số". Sau khi lưu: phiếu tự đạt → tự ghi sổ (UI-6.29); <c>Conclude</c> + quyền Duyệt Kiểm tra hàng nhập → kết luận ngay
        /// (Đạt một phần / Không đạt: số không đạt trừ vào <b>đơn mới nhất trước</b>); còn lại chờ QA.
        /// </summary>
        public async Task<IResult<ReceivingManyResultModel>> CreateMany(GoodsReceiptCreateManyRequest request)
        {
            var created = new List<(Guid ReceiptId, bool AllAuto, List<Part> Parts)>();
            var warnings = new List<string>();
            using (var tran = await _unitOfWork.OpenTransaction())
            {
                try
                {
                    var db = _unitOfWork.DbContext;
                    var inputs = (request.ListLine ?? []).Where(l => l.ReceivedQuantity != 0).ToList();
                    if (inputs.Count == 0 || inputs.Any(l => l.ReceivedQuantity < 0 || l.ListPurchaseOrderLineId.Count == 0))
                    {
                        return Fail(ConstantHelper.Message.GoodsReceiptNeedsLine);
                    }
                    var lineIds = inputs.SelectMany(l => l.ListPurchaseOrderLineId).Distinct().ToList();
                    if (lineIds.Count != inputs.Sum(l => l.ListPurchaseOrderLineId.Distinct().Count()))
                    {
                        return Fail(Msg.LineTwice);
                    }
                    var poLines = await db.PurchaseOrderLine.AsNoTracking().Where(l => lineIds.Contains(l.Id) && !l.IsDeleted).ToDictionaryAsync(l => l.Id);
                    if (poLines.Count != lineIds.Count)
                    {
                        return Fail(Msg.LineNotFound);
                    }
                    var poIds = poLines.Values.Select(l => l.PurchaseOrderId).Distinct().ToList();
                    var pos = await db.PurchaseOrder.AsNoTracking().Where(p => poIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
                    if (pos.Values.Select(p => p.SupplierId).Distinct().Count() > 1)
                    {
                        return Fail(Msg.OneSupplier);
                    }
                    var orderIdx = pos.Values.OrderBy(p => p.OrderDate).ThenBy(p => p.CreatedDate).ThenBy(p => p.PurchaseOrderNumber).Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i);
                    var remaining = (await ReceivingService.OpenLinesAsync(db, poIds)).ToDictionary(l => l.LineId, l => l.Remaining);
                    var ingIds = inputs.Select(i => i.IngredientId).Distinct().ToList();
                    var ings = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                    DateTime receiptDate = request.ReceiptDate ?? _clock.Now;
                    var perPo = new Dictionary<Guid, List<(Part Part, ReceivingLineInput Input)>>();
                    foreach (var input in inputs)
                    {
                        var ing = ings.GetValueOrDefault(input.IngredientId);
                        string name = ing?.Name ?? input.IngredientId.ToString();
                        if (input.ListPurchaseOrderLineId.Any(id => poLines[id].IngredientId != input.IngredientId))
                        {
                            return Fail(string.Format(Msg.WrongIngredient, name));
                        }
                        var parts = input.ListPurchaseOrderLineId.Select(id => poLines[id])
                            .Select(l => new Part(l.PurchaseOrderId, pos[l.PurchaseOrderId].PurchaseOrderNumber ?? pos[l.PurchaseOrderId].PONo, orderIdx[l.PurchaseOrderId], l, remaining.GetValueOrDefault(l.Id)))
                            .OrderBy(p => p.PoOrder).ToList();
                        // Chia số nhận: đơn cũ trước, không vượt còn phải nhận; dư → đơn mới nhất (Create kiểm nhận vượt).
                        decimal left = input.ReceivedQuantity;
                        foreach (var p in parts)
                        {
                            p.Qty = Math.Min(left, p.Remaining);
                            left -= p.Qty;
                        }
                        if (left > 0)
                        {
                            parts[^1].Qty += left;
                        }
                        // Kết luận: Không đạt cả dòng / Đạt một phần – số không đạt trừ đơn mới nhất trước.
                        string result = IncomingQcResult.Normalize(input.Result) ?? IncomingQcResult.Dat;
                        if (request.Conclude && result == IncomingQcResult.DatMotPhan)
                        {
                            if (input.AcceptedQuantity is not { } acc || acc <= 0 || acc >= input.ReceivedQuantity)
                            {
                                return Fail(string.Format(Msg.PartialQty, name, PurchasingRules.Qty(input.ReceivedQuantity)));
                            }
                            decimal reject = input.ReceivedQuantity - acc;
                            foreach (var p in parts.Where(p => p.Qty > 0).OrderByDescending(p => p.PoOrder))
                            {
                                decimal r = Math.Min(reject, p.Qty);
                                reject -= r;
                                (p.Result, p.Accepted) = r == 0 ? (IncomingQcResult.Dat, (decimal?)null) : r == p.Qty ? (IncomingQcResult.KhongDat, (decimal?)null) : (IncomingQcResult.DatMotPhan, (decimal?)(p.Qty - r));
                            }
                        }
                        else
                        {
                            parts.ForEach(p => p.Result = result);
                        }
                        foreach (var p in parts.Where(p => p.Qty > 0))
                        {
                            (perPo.TryGetValue(p.PoId, out var l) ? l : perPo[p.PoId] = []).Add((p, input));
                        }
                        if ((ing?.ExpiryControl == true || ing?.IsExpiryManaged == true) && input.ExpiryDate == null)
                        {
                            warnings.Add(string.Format(Msg.ExpiryAuto, name));
                        }
                    }
                    string invoice = string.IsNullOrWhiteSpace(request.SupplierInvoiceNo) ? string.Empty : $"{ReceivingService.InvoiceTag} {request.SupplierInvoiceNo.Trim()}";
                    string merged = perPo.Count > 1 ? "Nhận gộp " + perPo.Count + " đơn: " + string.Join(", ", perPo.Keys.OrderBy(k => orderIdx[k]).Select(k => pos[k].PurchaseOrderNumber ?? pos[k].PONo)) : string.Empty;
                    var requests = new List<(Guid PoId, GoodsReceiptCreateRequest Req, List<(Part Part, ReceivingLineInput Input)> Rows)>();
                    foreach (var (poId, rows) in perPo.OrderBy(k => orderIdx[k.Key]))
                    {
                        var req = new GoodsReceiptCreateRequest
                        {
                            PurchaseOrderId = poId, WarehouseId = request.WarehouseId, ReceiptDate = receiptDate,
                            Note = string.Join(" · ", new[] { invoice, merged, request.Note?.Trim() }.Where(x => !string.IsNullOrEmpty(x))),
                            ListLine = [.. rows.Select(x =>
                            {
                                var ing = ings.GetValueOrDefault(x.Input.IngredientId);
                                bool lot = ing?.LotControl == true, exp = ing?.ExpiryControl == true || ing?.IsExpiryManaged == true;
                                bool cold = PurchasingRules.ReceivingTempRuleOf(_rules, ing?.StorageType) != null;
                                return new GoodsReceiptLineInput
                                {
                                    PurchaseOrderLineId = x.Part.Line.Id, ReceivedQuantity = x.Part.Qty,
                                    SupplierLotNumber = lot ? x.Input.SupplierLotNumber : null, ManufactureDate = lot || exp ? x.Input.ManufactureDate : null, ExpiryDate = exp ? x.Input.ExpiryDate : null,
                                    TemperatureC = x.Input.TemperatureC ?? (cold ? request.VehicleTemperatureC : null),
                                };
                            })],
                        };
                        if (req.Note == string.Empty)
                        {
                            req.Note = null;
                        }
                        requests.Add((poId, req, rows));
                    }
                    // Kiểm MỌI đơn trước khi ghi đơn nào (lỗi bất kỳ → không lập phiếu nào), rồi mới lập.
                    foreach (var (poId, req, _) in requests)
                    {
                        var (_, _, check) = await CreateCoreAsync(req, [], validateOnly: true);
                        if (check != null)
                        {
                            return Fail((perPo.Count > 1 ? $"Đơn mua {pos[poId].PurchaseOrderNumber ?? pos[poId].PONo}: " : string.Empty) + check);
                        }
                    }
                    foreach (var (poId, req, rows) in requests)
                    {
                        var (id, allAuto, error) = await CreateCoreAsync(req, warnings);
                        if (error != null)
                        {
                            return Fail((perPo.Count > 1 ? $"Đơn mua {pos[poId].PurchaseOrderNumber ?? pos[poId].PONo}: " : string.Empty) + error);
                        }
                        created.Add((id, allAuto, [.. rows.Select(r => r.Part)]));
                    }
                    _unitOfWork.CommitTransaction();
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat("[", GetType().Name, "]", " CreateMany: ", ex.ToString()));
                    _unitOfWork.RollbackTransaction();
                    throw;
                }
            }

            // Sau khi lưu (đã commit): tự đạt → ghi sổ; QA tại cửa kho → kết luận ngay; còn lại chờ QA.
            var model = new ReceivingManyResultModel();
            bool canConclude = request.Conclude && await CommonAccess.DenyAsync(_services.GetRequiredService<IPermissionService>(), _currentActor, "qc-inspection", PermissionAction.Approve) == null;
            if (request.Conclude && !canConclude)
            {
                warnings.Add(Msg.NoConcludeRight);
            }
            var inputsByIng = (request.ListLine ?? []).GroupBy(l => l.IngredientId).ToDictionary(g => g.Key, g => g.First());
            foreach (var (id, allAuto, parts) in created)
            {
                IResult<GoodsReceiptDetailModel> r;
                if (allAuto || !canConclude)
                {
                    r = await AfterCreateAsync(id, allAuto, []);
                }
                else
                {
                    var db = _unitOfWork.DbContext;
                    var inspectionId = await db.QcInspection.AsNoTracking().Where(q => q.GoodsReceiptId == id && !q.IsDeleted).Select(q => q.Id).FirstAsync();
                    var grLines = await db.GoodsReceiptLine.AsNoTracking().Where(l => l.GoodsReceiptId == id && !l.IsDeleted).OrderBy(l => l.CreatedDate).ToListAsync();
                    var qcTemps = await db.QcInspectionLine.AsNoTracking().Where(q => q.QCInspectionId == inspectionId && !q.IsDeleted).ToDictionaryAsync(q => q.GoodsReceiptLineId ?? Guid.Empty, q => q.Temperature);
                    var used = new HashSet<Part>();
                    var concludeLines = new List<QcInspectionLineInput>();
                    foreach (var gl in grLines)
                    {
                        var p = parts.First(x => !used.Contains(x) && x.Line.IngredientId == gl.IngredientId);
                        used.Add(p);
                        var input = inputsByIng[gl.IngredientId];
                        concludeLines.Add(new QcInspectionLineInput
                        {
                            GoodsReceiptLineId = gl.Id, Result = p.Result ?? IncomingQcResult.Dat, AcceptedQuantity = p.Accepted, Temperature = qcTemps.GetValueOrDefault(gl.Id),
                            Appearance = input.Appearance, Packaging = input.Packaging, Remark = input.Remark, NoThawConfirmed = input.NoThawConfirmed,
                        });
                    }
                    r = await _services.GetRequiredService<IQcInspectionService>().Conclude(new QcInspectionConcludeRequest { Id = inspectionId, ListLine = concludeLines });
                    if (r.ResponseCode != 200 || r.Data == null)
                    {
                        warnings.Add(string.Format(Msg.ConcludeFailed, r.ResponseMessage));
                        r = await AfterCreateAsync(id, false, []);
                    }
                }
                if (r.Data != null)
                {
                    model.ListReceipt.Add(r.Data);
                    warnings.AddRange(r.Data.ListWarning);
                }
            }
            model.PostedCount = model.ListReceipt.Count(x => x.GoodsReceipt.Status == GoodsReceiptStatus.Posted);
            model.QcPendingCount = model.ListReceipt.Count(x => x.GoodsReceipt.Status == GoodsReceiptStatus.QcPending);
            model.IssueCount = model.ListReceipt.Count(x => x.SupplierIssueId != null);
            model.ListWarning = [.. warnings.Distinct()];
            string msg = string.Format(Msg.Saved, model.ListReceipt.Count, model.PostedCount, model.QcPendingCount);
            return Result<ReceivingManyResultModel>.Success(model, msg);

            Result<ReceivingManyResultModel> Fail(string m)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ReceivingManyResultModel>.Fail(m);
            }
        }

        private static class Msg
        {
            public const string LineTwice = "Một dòng đơn mua chỉ được nằm trong một dòng nhận";
            public const string LineNotFound = "Có dòng đơn mua không còn – tải lại form nhận hàng";
            public const string OneSupplier = "Chỉ gộp các đơn mua của cùng một nhà cung cấp";
            public const string WrongIngredient = "{0}: dòng đơn mua không đúng nguyên liệu";
            public const string PartialQty = "{0}: số đạt phải lớn hơn 0 và nhỏ hơn số nhận ({1})";
            public const string ExpiryAuto = "{0}: chưa nhập hạn dùng – hệ thống tự tính = ngày nhận + hạn bảo quản";
            public const string NoConcludeRight = "Bạn không có quyền kết luận kiểm tra – phiếu đã gửi QA";
            public const string ConcludeFailed = "Đã lập phiếu nhưng chưa kết luận được ({0}) – phiếu chờ ở Phê duyệt › Kiểm tra hàng nhập";
            public const string Saved = "Đã lập {0} phiếu nhập: {1} đã nhập kho, {2} chờ QA";
        }
    }
}
