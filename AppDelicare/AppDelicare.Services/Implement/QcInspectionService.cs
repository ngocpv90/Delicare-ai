using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using System.Net;

using AppDelicare.Services.Implement.Messaging;

namespace AppDelicare.Services.Implement
{
    public class QcInspectionService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IGoodsReceiptService goodsReceiptService, IClock clock, IOptions<BusinessRulesOptions> rules, INotificationService notifications) : IQcInspectionService
    {
        private readonly INotificationService _notifications = notifications; // BOT-1.7
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IGoodsReceiptService _goodsReceiptService = goodsReceiptService;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<QcInspectionViewModel>> GetAll(QcInspectionSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@GoodsReceiptId", request.GoodsReceiptId);
                dynamicParameters.Add("@InspectionNumber", request.InspectionNumber);
                dynamicParameters.Add("@InspectionDate", request.InspectionDate);
                dynamicParameters.Add("@InspectorEmployeeId", request.InspectorEmployeeId);
                dynamicParameters.Add("@Result", request.Result);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<QcInspectionViewModel>("uspQcInspection_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<QcInspectionViewModel>
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

        public IResults<QcInspection> GetAllData()
        {
            try
            {
                var list = _unitOfWork.QcInspectionRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<QcInspection>
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

        public async Task<IResult<bool>> Save(QcInspectionRequest request, string typeSave)
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

        /// <summary>T15: phiếu QC đầu vào chỉ tạo cùng phiếu nhập (<c>GoodsReceipt/Create</c>) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(QcInspectionRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.QcInspectionCrudBlocked);

        /// <summary>T15: CRUD Update chỉ sửa ghi chú / nhận xét chung; kết quả, số, phiếu nhập chỉ đổi qua <c>QcInspection/Conclude</c>.</summary>
        private async Task<string> Update(QcInspectionRequest request, string typeSave)
        {
            var exist = _unitOfWork.QcInspectionRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            exist.Note = request.Note;
            exist.Remark = request.Remark;
            _unitOfWork.QcInspectionRepository.Update(exist);
            await _unitOfWork.QcInspectionRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T15: không xóa phiếu QC (hồ sơ ATTP / truy xuất).</summary>
        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(ConstantHelper.Message.QcInspectionCrudBlocked);

        public async Task<IResult<QcInspection>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.QcInspectionRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<QcInspection>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<QcInspection>
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

        // ============================================================ T15 – QC đầu vào (spec 9.1, BUSINESS_RULES §4, Pilot P6)

        /// <summary>Chi tiết phiếu QC = chi tiết phiếu nhập (dòng, nhiệt độ + ngưỡng, kết quả gợi ý, lô).</summary>
        public async Task<IResult<GoodsReceiptDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var inspection = await _unitOfWork.DbContext.QcInspection.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);
                var detail = inspection == null ? null : await _goodsReceiptService.DetailAsync(inspection.GoodsReceiptId);
                return detail == null
                    ? Result<GoodsReceiptDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<GoodsReceiptDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// T15 (spec 9.1 conclude, PROPOSED – GAP-PUR-08), 1 transaction – phiếu nhập Chờ QC hoặc đã kết luận nhưng chưa ghi sổ (cho kết luận lại).
        /// Mọi dòng phải có kết quả DAT / DAT_MOT_PHAN / KHONG_DAT (nhận cả PASS / HOLD / REJECT):
        /// DAT → đạt = nhận; DAT_MOT_PHAN → 0 &lt; đạt &lt; nhận, bắt buộc nhận xét; KHONG_DAT → đạt = 0, bắt buộc nhận xét.
        /// Hạn dùng trước ngày nhận → chỉ được KHONG_DAT (chặn lô hết hạn). T15.1 nhiệt độ 3 vùng (BusinessRules:ReceivingTemp): hàng mát / đông lạnh chưa đo → lỗi;
        /// vùng loại → chỉ KHONG_DAT; vùng có điều kiện → bắt buộc lý do (đông lạnh: thêm noThawConfirmed), dòng QC đánh dấu "ưu tiên dùng trước".
        /// Ghi: QCInspectionLine, GoodsReceiptLine (QualityStatus, Accepted / Rejected), QCInspection.Result (tất cả DAT → DAT; tất cả KHONG_DAT → KHONG_DAT; còn lại
        /// DAT_MOT_PHAN), phiếu nhập → Đạt / Đạt một phần / Không đạt. Lô, nhập kho, sự cố NCC ghi khi <c>GoodsReceipt/Post</c>.
        /// </summary>
        public async Task<IResult<GoodsReceiptDetailModel>> Conclude(QcInspectionConcludeRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var inspection = _unitOfWork.QcInspectionRepository.GetSingle(q => q.Id == request.Id && !q.IsDeleted);
                var receipt = inspection == null ? null : _unitOfWork.GoodsReceiptRepository.GetSingle(r => r.Id == inspection.GoodsReceiptId && !r.IsDeleted);
                string? error = inspection == null || receipt == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : receipt.Status != GoodsReceiptStatus.QcPending && !GoodsReceiptStatus.Concluded.Contains(receipt.Status)
                        ? string.Format(ConstantHelper.Message.StatusActionInvalid, "kết luận QC", "phiếu nhập",
                            string.Join(" / ", new[] { GoodsReceiptStatus.QcPending, GoodsReceiptStatus.Accepted, GoodsReceiptStatus.PartiallyAccepted, GoodsReceiptStatus.Rejected }.Select(GoodsReceiptStatus.Label)),
                            GoodsReceiptStatus.Label(receipt.Status))
                    : request.InspectorEmployeeId is { } employeeId && !await db.Employee.AsNoTracking().AnyAsync(e => e.Id == employeeId && !e.IsDeleted)
                        ? "Không tìm thấy nhân viên kiểm tra"
                    : null;
                var lines = receipt == null ? [] : _unitOfWork.GoodsReceiptLineRepository.GetAll().Where(l => l.GoodsReceiptId == receipt.Id && !l.IsDeleted).ToList();
                var qcLines = inspection == null ? [] : _unitOfWork.QcInspectionLineRepository.GetAll().Where(q => q.QCInspectionId == inspection.Id && !q.IsDeleted).ToList();
                var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
                var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                var inputs = (request.ListLine ?? []).GroupBy(l => l.GoodsReceiptLineId).ToDictionary(g => g.Key, g => g.Last());
                var decided = new List<(GoodsReceiptLine Line, QcInspectionLineInput Input, string Result, decimal Accepted, decimal? Temperature, string? TempNote)>();
                if (error == null)
                {
                    var foreign = inputs.Keys.FirstOrDefault(k => lines.All(l => l.Id != k));
                    var missing = lines.Where(l => !inputs.ContainsKey(l.Id)).Select(l => ingredients.GetValueOrDefault(l.IngredientId)?.Name ?? l.Id.ToString()).ToList();
                    error = foreign != Guid.Empty ? $"Dòng {foreign} không thuộc phiếu nhập này"
                        : missing.Count != 0 ? string.Format(ConstantHelper.Message.QcLineMissing, string.Join(", ", missing))
                        : null;
                }
                foreach (var line in error == null ? lines.OrderBy(l => l.CreatedDate) : Enumerable.Empty<GoodsReceiptLine>())
                {
                    var input = inputs[line.Id];
                    ingredients.TryGetValue(line.IngredientId, out var ingredient);
                    string name = ingredient?.Name ?? line.IngredientId.ToString();
                    string? result = IncomingQcResult.Normalize(input.Result);
                    string? remark = input.Remark?.Trim();
                    decimal? temperature = input.Temperature ?? qcLines.FirstOrDefault(q => q.GoodsReceiptLineId == line.Id)?.Temperature;
                    var tempRule = PurchasingRules.ReceivingTempRuleOf(_rules, ingredient?.StorageType);
                    string zone = PurchasingRules.TempZone(tempRule, temperature);
                    bool accepting = result != null && result != IncomingQcResult.KhongDat;
                    decimal accepted = result switch
                    {
                        IncomingQcResult.Dat => line.ReceivedQuantity,
                        IncomingQcResult.DatMotPhan => input.AcceptedQuantity ?? 0m,
                        _ => 0m,
                    };
                    error = result == null ? string.Format(ConstantHelper.Message.QcResultInvalid, name)
                        : result == IncomingQcResult.DatMotPhan && (accepted <= 0 || accepted >= line.ReceivedQuantity)
                            ? string.Format(ConstantHelper.Message.QcPartialQuantityInvalid, name, PurchasingRules.Qty(line.ReceivedQuantity))
                        : result != IncomingQcResult.Dat && string.IsNullOrEmpty(remark) ? string.Format(ConstantHelper.Message.QcRemarkRequired, name, result)
                        : result != IncomingQcResult.KhongDat && line.ExpiryDate != null && line.ExpiryDate.Value.Date < receipt!.ReceiptDate.Date
                            ? string.Format(ConstantHelper.Message.QcExpiredLot, name, line.ExpiryDate.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))
                        // T15.1 (anh chốt 2026-11-16): nhiệt độ nhận 3 vùng – chỉ áp khi kết luận nhận (DAT / DAT_MOT_PHAN); KHONG_DAT luôn được.
                        : accepting && zone == ReceivingTempZone.Missing
                            ? string.Format(ConstantHelper.Message.QcTemperatureMissing, name, ingredient?.StorageType)
                        : accepting && zone == ReceivingTempZone.Reject
                            ? string.Format(ConstantHelper.Message.QcTemperatureRejectZone, name, PurchasingRules.Qty(temperature!.Value), PurchasingRules.Qty(tempRule!.ConditionalMax))
                        : accepting && zone == ReceivingTempZone.Conditional && string.IsNullOrEmpty(remark)
                            ? string.Format(ConstantHelper.Message.QcTemperatureRemarkRequired, name, PurchasingRules.Qty(temperature!.Value), PurchasingRules.Qty(tempRule!.AcceptMax),
                                PurchasingRules.Qty(tempRule.ConditionalMax))
                        : accepting && zone == ReceivingTempZone.Conditional && tempRule!.RequireNoThawConfirmation && input.NoThawConfirmed != true
                            ? string.Format(ConstantHelper.Message.QcNoThawConfirmRequired, name, PurchasingRules.Qty(temperature!.Value))
                        : null;
                    if (error != null)
                    {
                        break;
                    }
                    // Nhận có điều kiện → đánh dấu "ưu tiên dùng trước" trong QCInspectionLine.Note (GAP-PUR-10); GoodsReceipt/Post chép sang lô + tạo sự cố NCC.
                    string? tempNote = accepting && zone == ReceivingTempZone.Conditional
                        ? PurchasingRules.ConditionalNote(tempRule!, temperature!.Value, input.NoThawConfirmed == true)
                        : null;
                    decided.Add((line, input, result!, accepted, temperature, tempNote));
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<GoodsReceiptDetailModel>.Fail(error);
                }
                foreach (var (line, input, result, accepted, temperature, tempNote) in decided)
                {
                    line.QualityStatus = result;
                    line.AcceptedQuantity = accepted;
                    line.RejectedQuantity = line.ReceivedQuantity - accepted;
                    _unitOfWork.GoodsReceiptLineRepository.Update(line);
                    var qc = qcLines.FirstOrDefault(q => q.GoodsReceiptLineId == line.Id);
                    if (qc == null)
                    {
                        qc = new QcInspectionLine { Id = Guid.NewGuid(), QCInspectionId = inspection!.Id, IngredientId = line.IngredientId, GoodsReceiptLineId = line.Id };
                        _unitOfWork.QcInspectionLineRepository.Add(qc);
                    }
                    else
                    {
                        _unitOfWork.QcInspectionLineRepository.Update(qc);
                    }
                    qc.Result = result;
                    qc.Temperature = temperature;
                    qc.Weight = input.Weight ?? qc.Weight;
                    qc.Appearance = input.Appearance ?? qc.Appearance;
                    qc.Packaging = input.Packaging ?? qc.Packaging;
                    qc.Remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim();
                    qc.ExpiryDate = line.ExpiryDate;
                    qc.Note = tempNote; // T15.1: kết luận lại → ghi đè (Note của dòng QC chỉ dùng cho cờ nhận có điều kiện)
                }
                var results = decided.Select(d => d.Result).ToList();
                string overall = results.All(r => r == IncomingQcResult.Dat) ? IncomingQcResult.Dat
                    : results.All(r => r == IncomingQcResult.KhongDat) ? IncomingQcResult.KhongDat
                    : IncomingQcResult.DatMotPhan;
                inspection!.Result = overall;
                inspection.InspectionDate = _clock.Now;
                if (request.InspectorEmployeeId != null)
                {
                    inspection.InspectorEmployeeId = inspection.InspectorId = request.InspectorEmployeeId;
                }
                inspection.Note = PurchasingRules.AppendNote(inspection.Note, request.Note);
                _unitOfWork.QcInspectionRepository.Update(inspection);
                receipt!.Status = overall switch
                {
                    IncomingQcResult.Dat => GoodsReceiptStatus.Accepted,
                    IncomingQcResult.KhongDat => GoodsReceiptStatus.Rejected,
                    _ => GoodsReceiptStatus.PartiallyAccepted,
                };
                _unitOfWork.GoodsReceiptRepository.Update(receipt);
                await _unitOfWork.QcInspectionRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                if (overall != IncomingQcResult.Dat)
                {
                    // BOT-1.7: QC_INCOMING_FAIL → thủ kho, mua hàng, QA đầu vào.
                    await BusinessNotifier.ToMenusAsync(_notifications, _unitOfWork.DbContext, NotificationCatalog.QcIncomingFail, ["#/scm-gr", "#/scm-po", "#/qc-inspection"],
                        new { title = "Phiếu nhập " + receipt.ReceiptNumber, detail = overall == IncomingQcResult.KhongDat ? "QC không đạt – từ chối cả phiếu" : "QC đạt một phần", lines = new[] { inspection.Note } },
                        $"{NotificationCatalog.QcIncomingFail}:{receipt.Id:N}");
                }
                _unitOfWork.CommitTransaction();
                return Result<GoodsReceiptDetailModel>.Success(await _goodsReceiptService.DetailAsync(receipt.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Conclude: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
