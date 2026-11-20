using AutoMapper;
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
using AppDelicare.Data.Share;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement
{
    public class MaterialIssueService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IInventoryLedgerService inventoryLedgerService, IDocumentNumberService documentNumberService
        , IClock clock, IOptions<BusinessRulesOptions> rules) : IMaterialIssueService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IInventoryLedgerService _inventoryLedgerService = inventoryLedgerService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<MaterialIssueViewModel>> GetAll(MaterialIssueSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ProductionOrderId", request.ProductionOrderId == Guid.Empty ? null : request.ProductionOrderId);
                dynamicParameters.Add("@WarehouseId", request.WarehouseId == Guid.Empty ? null : request.WarehouseId);
                //dynamicParameters.Add("@IssueNumber", request.IssueNumber);
                dynamicParameters.Add("@IssueDate", request.IssueDate);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MaterialIssueViewModel>("uspMaterialIssue_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<MaterialIssueViewModel>
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

        public IResults<MaterialIssue> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MaterialIssueRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<MaterialIssue>
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

        public async Task<IResult<bool>> Save(MaterialIssueRequest request, string typeSave)
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

        /// <summary>T16: tạo phiếu xuất chỉ qua MaterialIssue/Create (số PH-, kiểm lô hạn dùng / QC / FEFO, tồn khả dụng) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(MaterialIssueRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.IssueInsertBlocked);

        /// <summary>T16: CRUD Update chỉ sửa ghi chú / ngày xuất của phiếu Nháp; trạng thái, số, kho, lệnh SX chỉ đổi qua action nghiệp vụ.</summary>
        private async Task<string> Update(MaterialIssueRequest request, string typeSave)
        {
            var exist = _unitOfWork.MaterialIssueRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status != MaterialIssueStatus.Draft)
            {
                return InvalidStatus(exist, "sửa phiếu xuất", MaterialIssueStatus.Draft);
            }
            exist.Note = request.Note;
            if (request.IssueDate != default)
            {
                exist.IssueDate = request.IssueDate;
            }
            _unitOfWork.MaterialIssueRepository.Update(exist);
            await _unitOfWork.MaterialIssueRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T16: chỉ xóa (mềm) phiếu Nháp, kèm dòng; phiếu đã xuất dùng Cancel (ghi bút toán đảo).</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.MaterialIssueRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status != MaterialIssueStatus.Draft)
            {
                return InvalidStatus(delete, "xóa phiếu xuất", MaterialIssueStatus.Draft);
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số); dòng của chính phiếu xóa cùng phiếu.
            if (await _auditChangeService.IsReferencedAsync(nameof(MaterialIssue), (Guid)id, nameof(MaterialIssueLine)))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            foreach (var line in _unitOfWork.MaterialIssueLineRepository.GetAll().Where(l => l.MaterialIssueId == delete.Id && !l.IsDeleted).ToList())
            {
                _unitOfWork.MaterialIssueLineRepository.Update(line);
            }
            _unitOfWork.MaterialIssueRepository.Update(delete);
            await _unitOfWork.MaterialIssueRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<MaterialIssue>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MaterialIssueRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<MaterialIssue>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<MaterialIssue>
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

        private static string InvalidStatus(MaterialIssue issue, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "phiếu xuất", string.Join(" / ", expected.Select(MaterialIssueStatus.Label)), MaterialIssueStatus.Label(issue.Status));

        private static string Qty(decimal value) => value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// T16 (spec 7.7 suggest): gợi ý lô xuất cho 1 nguyên liệu tại 1 kho theo FEFO / FIFO (InventoryPolicie.UseFefo, mặc định FEFO – D-24).
        /// Bỏ qua lô hết hạn, QC KHONG_DAT / REJECT / HOLD, lô tạm giữ (trả riêng trong ListBlocked). ShortQuantity &gt; 0 = không đủ tồn xuất được.
        /// </summary>
        public async Task<IResult<LotSuggestionModel>> SuggestLots(MaterialIssueSuggestRequest request)
        {
            try
            {
                if (request.Quantity <= 0)
                {
                    return Result<LotSuggestionModel>.Fail(ConstantHelper.Message.QuantityPositive);
                }
                var db = _unitOfWork.DbContext;
                bool useFefo = await InventoryLotRules.UseFefoAsync(db, request.WarehouseId, request.IngredientId);
                var rows = await InventoryLotRules.LoadAsync(db, _clock.Today, _rules.ExpiringLotDays, request.WarehouseId, request.IngredientId);
                var suggestion = InventoryLotRules.Suggest(rows, request.WarehouseId, request.IngredientId, request.Quantity, useFefo);
                return Result<LotSuggestionModel>.Success(suggestion, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SuggestLots: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// T16 (spec 7.7 material-issues): tạo phiếu xuất Nháp trong 1 transaction – số PH- (T11, ghi cả IssueNumber / IssueNo), dòng kèm lô.
        /// Dòng chọn lô: chặn lô hết hạn / QC không đạt / tạm giữ, chặn vượt tồn khả dụng; còn lô nên xuất trước → cảnh báo (không chặn).
        /// Dòng không chọn lô: tự phân bổ FEFO / FIFO (tách nhiều dòng theo lô), thiếu tồn xuất được → lỗi.
        /// Chưa ghi sổ – ghi sổ bằng Post. Không có lệnh SX → ProductionOrderId = Guid.Empty (GAP-INV-02, PROPOSED).
        /// </summary>
        public async Task<IResult<MaterialIssueDetailModel>> Create(MaterialIssueCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var warnings = new List<string>();
                var (issue, lines, error) = await BuildIssueAsync(request, warnings);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<MaterialIssueDetailModel>.Fail(error);
                }
                string number = await _documentNumberService.NextAsync(DocumentKind.MaterialIssue, _clock.Today);
                issue!.IssueNumber = number;
                issue.IssueNo = number;
                _unitOfWork.MaterialIssueRepository.Add(issue);
                foreach (var line in lines!)
                {
                    _unitOfWork.MaterialIssueLineRepository.Add(line);
                }
                await _unitOfWork.MaterialIssueRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(issue.Id);
                detail!.ListWarning.AddRange(warnings);
                return Result<MaterialIssueDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<(MaterialIssue? Issue, List<MaterialIssueLine>? Lines, string? Error)> BuildIssueAsync(MaterialIssueCreateRequest request, List<string> warnings)
        {
            var db = _unitOfWork.DbContext;
            DateTime today = _clock.Today;
            string? error = await InventoryLotRules.CheckPlaceAsync(db, request.WarehouseId, null);
            if (error != null)
            {
                return (null, null, error);
            }
            if (request.ProductionOrderId is { } productionOrderId && productionOrderId != Guid.Empty
                && !await db.ProductionOrder.AsNoTracking().AnyAsync(p => p.Id == productionOrderId && !p.IsDeleted))
            {
                return (null, null, ConstantHelper.Message.ProductionOrderNotFound);
            }
            if (request.ListLine.Count == 0)
            {
                return (null, null, ConstantHelper.Message.IssueNeedsLine);
            }
            if (request.ListLine.Any(l => l.Quantity <= 0))
            {
                return (null, null, ConstantHelper.Message.QuantityPositive);
            }

            var issue = new MaterialIssue
            {
                Id = Guid.NewGuid(),
                WarehouseId = request.WarehouseId,
                ProductionOrderId = request.ProductionOrderId ?? Guid.Empty,
                IssueDate = request.IssueDate ?? _clock.Now,
                Status = MaterialIssueStatus.Draft,
                Note = request.Note,
            };
            var lines = new List<MaterialIssueLine>();
            // Tồn + thứ tự xuất theo nguyên liệu; lượng đã phân bổ trong phiếu theo (nguyên liệu, lô).
            var stock = new Dictionary<Guid, (List<InventoryLotBalanceModel> Rows, bool UseFefo)>();
            var allocated = new Dictionary<Guid, Dictionary<Guid, decimal>>();
            var chosen = new List<(Guid IngredientId, InventoryLotBalanceModel Row)>();

            foreach (var item in request.ListLine)
            {
                var (ingredient, lot, lotError) = await InventoryLotRules.CheckIngredientLotAsync(db, item.IngredientId, item.MaterialLotId);
                if (lotError != null)
                {
                    return (null, null, lotError);
                }
                if (!stock.TryGetValue(item.IngredientId, out var s))
                {
                    s = (await InventoryLotRules.LoadAsync(db, today, _rules.ExpiringLotDays, request.WarehouseId, item.IngredientId),
                        await InventoryLotRules.UseFefoAsync(db, request.WarehouseId, item.IngredientId));
                    // BE2.4: xuất cho lệnh SX đã giữ chỗ → được dùng phần lệnh đã giữ (khả dụng = tồn − giữ chỗ của lệnh khác).
                    var own = await ReservationRules.AvailableForOrderAsync(db, [item.IngredientId], today, request.ProductionOrderId);
                    if (own != null)
                    {
                        foreach (var row in s.Rows.Where(r => own.ContainsKey(r.BalanceId)))
                        {
                            row.AvailableQuantity = own[row.BalanceId];
                            row.ReservedQuantity = row.OnHandQuantity - row.AvailableQuantity;
                        }
                    }
                    stock[item.IngredientId] = s;
                    allocated[item.IngredientId] = [];
                }
                var used = allocated[item.IngredientId];
                if (lot != null)
                {
                    string? block = InventoryLotRules.BlockReason(lot, today);
                    if (block != null)
                    {
                        return (null, null, block);
                    }
                    var lotRows = s.Rows.Where(r => r.MaterialLotId == lot.Id).ToList();
                    decimal available = lotRows.Sum(r => r.AvailableQuantity) - used.GetValueOrDefault(lot.Id);
                    if (available < item.Quantity)
                    {
                        return (null, null, string.Format(ConstantHelper.Message.InsufficientAvailable,
                            $"{ingredient!.Name} – lô {lot.LotNumber ?? lot.LotNo}", Qty(Math.Max(available, 0)), Qty(item.Quantity)));
                    }
                    Guid unitId = lotRows[0].UnitId;
                    if (item.UnitId != null && item.UnitId != unitId)
                    {
                        return (null, null, string.Format(ConstantHelper.Message.UnitMismatch, lotRows[0].UnitName));
                    }
                    used[lot.Id] = used.GetValueOrDefault(lot.Id) + item.Quantity;
                    lines.Add(NewLine(issue.Id, item.IngredientId, lot.Id, item.Quantity, unitId, item.Note));
                    chosen.Add((item.IngredientId, lotRows[0]));
                    continue;
                }
                var suggestion = InventoryLotRules.Suggest(s.Rows, request.WarehouseId, item.IngredientId, item.Quantity, s.UseFefo, used);
                if (suggestion.ShortQuantity > 0)
                {
                    return (null, null, string.Format(ConstantHelper.Message.InsufficientAvailable, ingredient!.Name,
                        Qty(suggestion.PickedQuantity), Qty(item.Quantity)));
                }
                foreach (var pick in suggestion.ListPick)
                {
                    if (item.UnitId != null && item.UnitId != pick.UnitId)
                    {
                        string? unitName = s.Rows.FirstOrDefault(r => r.UnitId == pick.UnitId)?.UnitName;
                        return (null, null, string.Format(ConstantHelper.Message.UnitMismatch, unitName));
                    }
                    used[pick.MaterialLotId ?? Guid.Empty] = used.GetValueOrDefault(pick.MaterialLotId ?? Guid.Empty) + pick.Quantity;
                    lines.Add(NewLine(issue.Id, item.IngredientId, pick.MaterialLotId, pick.Quantity, pick.UnitId, item.Note));
                }
            }

            // Cảnh báo FEFO / FIFO (không chặn): còn lô xuất được, hạn / ngày nhận sớm hơn lô đã chọn, chưa dùng hết trong phiếu.
            foreach (var (ingredientId, row) in chosen.DistinctBy(c => c.Row.MaterialLotId))
            {
                var (rows, useFefo) = stock[ingredientId];
                var used = allocated[ingredientId];
                var earlier = InventoryLotRules.LotsInPickOrder(rows, useFefo)
                    .FirstOrDefault(l => l.Row.MaterialLotId != row.MaterialLotId && InventoryLotRules.IsEarlier(l.Row, row, useFefo)
                        && l.Available - used.GetValueOrDefault(l.Row.MaterialLotId ?? Guid.Empty) > 0);
                if (earlier.Row != null)
                {
                    DateTime? date = useFefo ? earlier.Row.ExpiryDate : earlier.Row.ReceivedDate;
                    warnings.Add(string.Format(ConstantHelper.Message.LotNotFirstWarning, earlier.Row.LotNumber,
                        (useFefo ? "HSD " : "nhận ") + date?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture),
                        row.LotNumber, useFefo ? "FEFO" : "FIFO"));
                }
            }
            return (issue, lines, null);
        }

        private static MaterialIssueLine NewLine(Guid issueId, Guid ingredientId, Guid? lotId, decimal quantity, Guid unitId, string? note) => new()
        {
            Id = Guid.NewGuid(),
            MaterialIssueId = issueId,
            IngredientId = ingredientId,
            MaterialLotId = lotId,
            LotId = lotId,
            Quantity = quantity,
            UnitId = unitId,
            UomId = unitId,
            Note = note,
        };

        /// <summary>
        /// T16 (spec 7.7): ghi sổ phiếu xuất Draft → Issued trong 1 transaction. Kiểm lại toàn bộ trước khi ghi (lô hết hạn / QC / tạm giữ tại hôm nay,
        /// tồn khả dụng theo lô, đơn vị) rồi mỗi dòng ghi bút toán ISSUE (OUT) qua InventoryLedgerService – phân bổ trên các vị trí của lô;
        /// <c>TransactionNumber</c> = số phiếu, <c>ReferenceType = MaterialIssue</c>, <c>ReferenceId</c> = Id phiếu. Lỗi bất kỳ → không ghi gì.
        /// </summary>
        public async Task<IResult<MaterialIssueDetailModel>> Post(InventoryDocumentActionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                await DbLocks.DocumentAsync(db, "MaterialIssue", request.Id); // Rà soát BE: 2 lần ghi sổ / hủy cùng lúc chạy lần lượt.
                var issue = _unitOfWork.MaterialIssueRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                if (issue == null || issue.Status != MaterialIssueStatus.Draft)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<MaterialIssueDetailModel>.Fail(issue == null ? ConstantHelper.Message.RecordNotFoundMessage : InvalidStatus(issue, "ghi sổ phiếu xuất", MaterialIssueStatus.Draft));
                }
                var lines = await db.MaterialIssueLine.AsNoTracking().Where(l => l.MaterialIssueId == issue.Id && !l.IsDeleted && l.Quantity > 0).ToListAsync();
                if (lines.Count == 0)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<MaterialIssueDetailModel>.Fail(ConstantHelper.Message.IssueNeedsLine);
                }
                DateTime today = _clock.Today;
                // BE2.4: phiếu của lệnh SX đã giữ chỗ → khả dụng = tồn − giữ chỗ của lệnh khác (phần lệnh đã giữ được xuất).
                var own = await ReservationRules.AvailableForOrderAsync(db, [.. lines.Select(l => l.IngredientId).Distinct()], today, issue.ProductionOrderId);
                // 1) Kiểm toàn bộ trước khi ghi: lô, tồn khả dụng theo (nguyên liệu, lô) cộng dồn các dòng, đơn vị.
                var plan = new List<(MaterialIssueLine Line, MaterialLot? Lot, List<(Guid? StorageLocationId, Guid UnitId, decimal Quantity)> Chunks)>();
                foreach (var group in lines.GroupBy(l => (l.IngredientId, l.MaterialLotId)))
                {
                    var lot = group.Key.MaterialLotId == null ? null : await db.MaterialLot.AsNoTracking().FirstOrDefaultAsync(x => x.Id == group.Key.MaterialLotId);
                    string? error = group.Key.MaterialLotId != null && lot == null ? ConstantHelper.Message.LotNotFound : InventoryLotRules.BlockReason(lot, today);
                    var balances = await db.InventoryBalance.AsNoTracking()
                        .Where(b => !b.IsDeleted && b.WarehouseId == issue.WarehouseId && b.IngredientId == group.Key.IngredientId && b.MaterialLotId == group.Key.MaterialLotId)
                        .ToListAsync();
                    if (own != null)
                    {
                        foreach (var b in balances.Where(b => own.ContainsKey(b.Id)))
                        {
                            b.AvailableQuantity = own[b.Id];
                        }
                    }
                    decimal need = group.Sum(l => l.Quantity);
                    decimal available = balances.Sum(b => Math.Max(b.AvailableQuantity, 0));
                    if (error == null && available < need)
                    {
                        string name = await db.Ingredient.AsNoTracking().Where(i => i.Id == group.Key.IngredientId).Select(i => i.Name).FirstOrDefaultAsync() ?? string.Empty;
                        error = string.Format(ConstantHelper.Message.InsufficientAvailable,
                            lot == null ? name : $"{name} – lô {lot.LotNumber ?? lot.LotNo}", Qty(available), Qty(need));
                    }
                    if (error == null && balances.Any(b => b.AvailableQuantity > 0 && group.Any(l => l.UnitId != b.UnitId)))
                    {
                        error = string.Format(ConstantHelper.Message.UnitMismatch, balances[0].UnitId);
                    }
                    if (error != null)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<MaterialIssueDetailModel>.Fail(error);
                    }
                    // Phân bổ lần lượt từng dòng trên các vị trí (khả dụng còn lại sau dòng trước).
                    var remaining = balances.ToDictionary(b => b.Id, b => Math.Max(b.AvailableQuantity, 0));
                    foreach (var line in group)
                    {
                        var chunks = InventoryLotRules.AllocateOverLocations(
                            balances.Select(b => new InventoryBalance { Id = b.Id, StorageLocationId = b.StorageLocationId, UnitId = b.UnitId, AvailableQuantity = remaining[b.Id] }),
                            line.Quantity);
                        foreach (var chunk in chunks)
                        {
                            var b = balances.First(x => x.StorageLocationId == chunk.StorageLocationId && remaining[x.Id] >= chunk.Quantity);
                            remaining[b.Id] -= chunk.Quantity;
                        }
                        plan.Add((line, lot, chunks));
                    }
                }
                // 2) Ghi sổ.
                string number = issue.IssueNumber ?? issue.IssueNo ?? issue.Id.ToString("N")[..8];
                foreach (var (line, lot, chunks) in plan)
                {
                    foreach (var chunk in chunks)
                    {
                        await _inventoryLedgerService.PostAsync(new InventoryPostRequest
                        {
                            TransactionType = "ISSUE",
                            TransactionNumber = number,
                            TransactionDate = _clock.Now,
                            WarehouseId = issue.WarehouseId,
                            StorageLocationId = chunk.StorageLocationId,
                            IngredientId = line.IngredientId,
                            MaterialLotId = line.MaterialLotId,
                            UnitId = chunk.UnitId,
                            QuantityOut = chunk.Quantity,
                            UnitCost = lot?.UnitCost,
                            ReferenceType = InventoryReferenceType.MaterialIssue,
                            ReferenceId = issue.Id,
                            Note = string.IsNullOrWhiteSpace(line.Note) ? $"Xuất kho {number}" : line.Note,
                        });
                    }
                }
                issue.Status = MaterialIssueStatus.Issued;
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    issue.Note = string.IsNullOrWhiteSpace(issue.Note) ? request.Note.Trim() : $"{issue.Note}\n{request.Note.Trim()}";
                }
                _unitOfWork.MaterialIssueRepository.Update(issue);
                // BE2.4 (spec 7.7 material-issues): giảm giữ chỗ của lệnh theo số đã xuất → Đã xuất / Đã xuất một phần, rồi phân bổ lại tồn giữ chỗ.
                var consumed = issue.ProductionOrderId == Guid.Empty ? []
                    : await ReservationRules.ConsumeAsync(db, issue.ProductionOrderId, lines.Select(l => (l.IngredientId, l.UnitId, l.Quantity)));
                await _unitOfWork.MaterialIssueRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                if (consumed.Count != 0)
                {
                    await ReservationRules.ReallocateAsync(db, consumed, today);
                }
                _unitOfWork.CommitTransaction();
                return Result<MaterialIssueDetailModel>.Success(await DetailAsync(issue.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (BusinessRuleException ex)
            {
                _unitOfWork.RollbackTransaction();
                return Result<MaterialIssueDetailModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Post: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T16: hủy phiếu xuất, lý do bắt buộc (Note "Hủy: …"). Draft → Cancelled. Issued → ghi bút toán đảo (REVERSAL) cho mọi bút toán ISSUE của phiếu
        /// (tồn trở lại như trước) rồi Cancelled – 1 transaction (PROPOSED – BA/PO CONFIRM: hủy phiếu đã xuất khi NL chưa dùng).
        /// </summary>
        public async Task<IResult<MaterialIssueDetailModel>> Cancel(MaterialIssueCancelRequest request)
        {
            string? reason = request.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < 3)
            {
                return Result<MaterialIssueDetailModel>.Fail(ConstantHelper.Message.IssueCancelReasonRequired);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                await DbLocks.DocumentAsync(db, "MaterialIssue", request.Id); // Rà soát BE: 2 lần ghi sổ / hủy cùng lúc chạy lần lượt.
                var issue = _unitOfWork.MaterialIssueRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                if (issue == null || (issue.Status != MaterialIssueStatus.Draft && issue.Status != MaterialIssueStatus.Issued))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<MaterialIssueDetailModel>.Fail(issue == null ? ConstantHelper.Message.RecordNotFoundMessage
                        : InvalidStatus(issue, "hủy phiếu xuất", MaterialIssueStatus.Draft, MaterialIssueStatus.Issued));
                }
                if (issue.Status == MaterialIssueStatus.Issued)
                {
                    var posted = await db.InventoryTransaction.AsNoTracking()
                        .Where(t => !t.IsDeleted && t.ReferenceType == InventoryReferenceType.MaterialIssue && t.ReferenceId == issue.Id)
                        .Select(t => t.Id).ToListAsync();
                    foreach (Guid txnId in posted)
                    {
                        await _inventoryLedgerService.ReverseAsync(txnId, $"Hủy phiếu xuất {issue.IssueNumber ?? issue.IssueNo}: {reason}");
                    }
                }
                issue.Status = MaterialIssueStatus.Cancelled;
                issue.Note = string.IsNullOrWhiteSpace(issue.Note) ? "Hủy: " + reason : $"{issue.Note}\nHủy: {reason}";
                _unitOfWork.MaterialIssueRepository.Update(issue);
                await _unitOfWork.MaterialIssueRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<MaterialIssueDetailModel>.Success(await DetailAsync(issue.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (BusinessRuleException ex)
            {
                _unitOfWork.RollbackTransaction();
                return Result<MaterialIssueDetailModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>T16: chi tiết phiếu xuất (dòng + lô + bút toán đã ghi + thao tác được phép).</summary>
        public async Task<IResult<MaterialIssueDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<MaterialIssueDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<MaterialIssueDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<MaterialIssueDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var issue = await db.MaterialIssue.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (issue == null)
            {
                return null;
            }
            var lines = await db.MaterialIssueLine.AsNoTracking().Where(l => l.MaterialIssueId == id && !l.IsDeleted).ToListAsync();
            var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            var lotIds = lines.Where(l => l.MaterialLotId != null).Select(l => l.MaterialLotId!.Value).Distinct().ToList();
            var unitIds = lines.Select(l => l.UnitId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var units = await db.UnitMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
            var txns = await db.InventoryTransaction.AsNoTracking()
                .Where(t => !t.IsDeleted && t.ReferenceType == InventoryReferenceType.MaterialIssue && t.ReferenceId == id).ToListAsync();
            var txnIds = txns.Select(t => t.Id).ToList();
            txns.AddRange(await db.InventoryTransaction.AsNoTracking()
                .Where(t => !t.IsDeleted && t.ReferenceType == InventoryLedgerService.ReversalReferenceType && t.ReferenceId != null && txnIds.Contains(t.ReferenceId.Value)).ToListAsync());
            return new MaterialIssueDetailModel
            {
                MaterialIssue = issue,
                StatusName = MaterialIssueStatus.Label(issue.Status),
                WarehouseName = await db.Warehouse.AsNoTracking().Where(w => w.Id == issue.WarehouseId).Select(w => w.Name).FirstOrDefaultAsync(),
                // T23: cùng nguyên liệu → theo hạn lô (FEFO, lô không hạn cuối) rồi số lô – trước đây thứ tự lô phụ thuộc DB trả về (test toàn luồng chập chờn).
                ListLine = [.. lines
                    .OrderBy(l => ingredients.GetValueOrDefault(l.IngredientId)?.Name)
                    .ThenBy(l => (l.MaterialLotId != null ? lots.GetValueOrDefault(l.MaterialLotId.Value)?.ExpiryDate : null) ?? DateTime.MaxValue)
                    .ThenBy(l => l.MaterialLotId != null ? lots.GetValueOrDefault(l.MaterialLotId.Value)?.LotNumber : null, StringComparer.Ordinal)
                    .Select(l =>
                {
                    var lot = l.MaterialLotId != null ? lots.GetValueOrDefault(l.MaterialLotId.Value) : null;
                    return new MaterialIssueLineDetailModel
                    {
                        Id = l.Id,
                        IngredientId = l.IngredientId,
                        IngredientCode = ingredients.GetValueOrDefault(l.IngredientId)?.Code,
                        IngredientName = ingredients.GetValueOrDefault(l.IngredientId)?.Name,
                        MaterialLotId = l.MaterialLotId,
                        LotNumber = lot?.LotNumber ?? lot?.LotNo,
                        ExpiryDate = lot?.ExpiryDate,
                        QcStatus = lot?.QcStatus ?? lot?.QualityStatus,
                        Quantity = l.Quantity,
                        UnitId = l.UnitId,
                        UnitName = units.GetValueOrDefault(l.UnitId),
                        Note = l.Note,
                    };
                })],
                AllowedActions = issue.Status switch
                {
                    MaterialIssueStatus.Draft => ["post", "cancel"],
                    MaterialIssueStatus.Issued => ["cancel"],
                    _ => [],
                },
                ListTransaction = [.. txns.OrderBy(t => t.CreatedDate)],
            };
        }
    }
}
