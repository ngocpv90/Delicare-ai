using System.Globalization;
using System.Text;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.22 (D-103): Đơn vị tính gộp quy đổi – đơn vị **gốc** (kg, g, lít…) và đơn vị **đóng gói có kích cỡ** ("thùng 10 kg", "bao 25 kg"…: tên tự sinh =
    /// loại bao bì + số + đơn vị gốc, lưu cùng quy đổi chung trong một giao dịch; trùng kích cỡ → dùng lại đơn vị có sẵn). Quy đổi không gắn nguyên liệu
    /// (GAP-UOM-01 PROPOSED). Đổi hệ số đơn vị đã dùng → chặn (tạo đơn vị mới). Ngày hiệu lực quy đổi để trống (luôn hiệu lực). Không thêm bảng / cột:
    /// loại đơn vị ghi ở UnitMeasure.UomType = BASE / PACK (trống = gốc), loại bao bì ở UnitMeasure.UnitType.
    /// </summary>
    public partial class UnitMeasureService
    {
        public const string KindBase = "BASE", KindPack = "PACK";
        public static readonly string[] PackTypes = ["thùng", "bao", "khay", "hộp", "chai", "can", "túi", "kiện", "bình", "lốc"];

        private static string Slug(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.Trim().ToUpperInvariant().Replace('Đ', 'D').Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            }
            return string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>Đơn vị có đang được dùng (nguyên liệu, giá NCC, công thức, chứng từ, sổ kho).</summary>
        private async Task<int> UsageCountAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            int n = await db.Ingredient.CountAsync(x => !x.IsDeleted && (x.BaseUnitId == id || x.BaseUomId == id || x.PurchaseUomId == id || x.IssueUomId == id));
            n += await db.SupplierMaterial.CountAsync(x => !x.IsDeleted && x.PurchaseUomId == id);
            n += await db.RecipeLine.CountAsync(x => !x.IsDeleted && (x.UnitId == id || x.UomId == id));
            n += await db.PurchaseRequestLine.CountAsync(x => !x.IsDeleted && (x.UnitId == id || x.UomId == id));
            n += await db.PurchaseOrderLine.CountAsync(x => !x.IsDeleted && (x.UnitId == id || x.UomId == id));
            n += await db.GoodsReceiptLine.CountAsync(x => !x.IsDeleted && (x.UnitId == id || x.UomId == id));
            n += await db.MaterialIssueLine.CountAsync(x => !x.IsDeleted && (x.UnitId == id || x.UomId == id));
            return n;
        }

        public async Task<IResult<UnitSavedModel>> SaveWithConversion(UnitPackRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                string kind = (request.Kind ?? KindBase).Trim().ToUpperInvariant();
                UnitMeasure? exist = request.Id is { } eid ? await db.UnitMeasure.FirstOrDefaultAsync(x => x.Id == eid && !x.IsDeleted) : null;
                if (request.Id != null && exist == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<UnitSavedModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                string name, code;
                UomConversion? conv = null;
                decimal factor = 0;
                Guid? baseId = null;
                if (kind == KindPack)
                {
                    string pack = (request.PackType ?? string.Empty).Trim().ToLowerInvariant();
                    if (!PackTypes.Contains(pack))
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<UnitSavedModel>.Fail(ConstantHelper.Message.UomPackTypeInvalid);
                    }
                    if (!(request.Quantity > 0))
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<UnitSavedModel>.Fail(ConstantHelper.Message.UomPackQuantityInvalid);
                    }
                    var bas = request.BaseUnitId is { } bid ? await db.UnitMeasure.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bid && !x.IsDeleted) : null;
                    if (bas == null || bas.UomType == KindPack)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<UnitSavedModel>.Fail(ConstantHelper.Message.UomPackBaseInvalid);
                    }
                    factor = request.Quantity!.Value;
                    baseId = bas.Id;
                    string baseCode = bas.Symbol ?? bas.Code ?? bas.Name ?? string.Empty;
                    name = $"{pack} {factor.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',')} {baseCode}";
                    // Trùng kích cỡ (cùng loại bao bì, cùng đơn vị gốc, cùng hệ số) → dùng lại đơn vị có sẵn.
                    if (exist == null)
                    {
                        Guid b = bas.Id;
                        decimal f = factor;
                        var dup = await (from u in db.UnitMeasure.AsNoTracking()
                                         join c in db.UomConversion.AsNoTracking() on u.Id equals c.FromUnitId
                                         where !u.IsDeleted && !c.IsDeleted && u.UomType == KindPack && u.UnitType == pack && c.ToUnitId == b && c.ConversionFactor == f
                                         select new { u.Id, u.Code, u.Name }).FirstOrDefaultAsync();
                        if (dup != null)
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<UnitSavedModel>.Success(new UnitSavedModel { Id = dup.Id, Code = dup.Code, Name = dup.Name, Reused = true }, string.Format(ConstantHelper.Message.UomPackReused, dup.Name));
                        }
                    }
                    code = exist?.Code ?? Slug(name);
                    request.UnitType = pack;
                }
                else
                {
                    kind = KindBase;
                    name = (request.Name ?? string.Empty).Trim();
                    code = (exist?.Code ?? request.Code ?? string.Empty).Trim();
                    if (name.Length == 0 || code.Length == 0)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<UnitSavedModel>.Fail(ConstantHelper.Message.UomBaseNameRequired);
                    }
                }
                Guid self = exist?.Id ?? Guid.Empty;
                if (await db.UnitMeasure.AsNoTracking().AnyAsync(x => !x.IsDeleted && x.Id != self && x.Code == code))
                {
                    if (kind == KindPack && exist == null)
                    {
                        code = code + "-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
                    }
                    else
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<UnitSavedModel>.Fail(string.Format(ConstantHelper.Message.UomCodeDuplicate, code));
                    }
                }
                var unit = exist ?? new UnitMeasure { Id = Guid.NewGuid() };
                if (exist != null && kind == KindPack)
                {
                    conv = await db.UomConversion.FirstOrDefaultAsync(c => !c.IsDeleted && c.FromUnitId == exist.Id);
                    bool changed = conv == null || conv.ToUnitId != baseId || conv.ConversionFactor != factor;
                    if (changed && await UsageCountAsync(exist.Id) > 0)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<UnitSavedModel>.Fail(ConstantHelper.Message.UomFactorInUse);
                    }
                }
                unit.Code = code;
                unit.Name = name;
                unit.Symbol = kind == KindBase ? (string.IsNullOrWhiteSpace(request.Symbol) ? code : request.Symbol.Trim()) : name;
                unit.UomType = kind;
                unit.UnitType = kind == KindPack ? request.UnitType : unit.UnitType;
                unit.Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status;
                unit.DecimalPrecision ??= kind == KindPack ? 0 : 3;
                if (exist == null)
                {
                    _unitOfWork.UnitMeasureRepository.Add(unit);
                }
                else
                {
                    _unitOfWork.UnitMeasureRepository.Update(unit);
                }
                if (kind == KindPack)
                {
                    if (conv == null)
                    {
                        _unitOfWork.UomConversionRepository.Add(new UomConversion
                        {
                            Id = Guid.NewGuid(), FromUnitId = unit.Id, ToUnitId = baseId!.Value, FromUomId = unit.Id, ToUomId = baseId.Value, ConversionFactor = factor,
                            IsActive = true, Status = "Active",
                        });
                    }
                    else
                    {
                        conv.ToUnitId = baseId!.Value;
                        conv.ToUomId = baseId.Value;
                        conv.ConversionFactor = factor;
                        conv.EffectiveFrom = null;
                        conv.EffectiveTo = null;
                        _unitOfWork.UomConversionRepository.Update(conv);
                    }
                }
                await _unitOfWork.UnitMeasureRepository.CommitAsync(exist == null ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<UnitSavedModel>.Success(new UnitSavedModel { Id = unit.Id, Code = unit.Code, Name = unit.Name }, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveWithConversion: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Đơn vị quy đổi được về đơn vị kho của nguyên liệu (hoặc về đơn vị gốc truyền vào) + hệ số (1 đơn vị = N đơn vị kho).</summary>
        public async Task<IResult<UnitChoiceListModel>> GetForIngredient(Guid? ingredientId, Guid? baseUnitId)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                Guid? baseId = baseUnitId;
                if (ingredientId is { } iid)
                {
                    baseId = await db.Ingredient.AsNoTracking().Where(x => x.Id == iid).Select(x => x.BaseUnitId ?? x.BaseUomId).FirstOrDefaultAsync();
                }
                var model = new UnitChoiceListModel { BaseUnitId = baseId };
                if (baseId == null)
                {
                    return Result<UnitChoiceListModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
                }
                var units = await db.UnitMeasure.AsNoTracking().Where(x => !x.IsDeleted && (x.Status == null || x.Status == "Active")).Select(x => new { x.Id, x.Code, x.Name, x.UomType }).ToListAsync();
                foreach (var u in units)
                {
                    decimal? f = await _uom.ConvertAsync(1m, u.Id, baseId.Value);
                    if (f != null)
                    {
                        model.ListUnit.Add(new UnitChoiceModel { Id = u.Id, Code = u.Code, Name = u.Name, Kind = u.UomType ?? KindBase, Factor = f.Value, IsBase = u.Id == baseId });
                    }
                }
                model.ListUnit = [.. model.ListUnit.OrderByDescending(u => u.IsBase).ThenBy(u => u.Factor)];
                return Result<UnitChoiceListModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetForIngredient: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Danh sách đơn vị cho màn Đơn vị tính: loại, "= bao nhiêu đơn vị gốc", số chỗ đang dùng.</summary>
        public async Task<IResults<UnitUsageModel>> GetUsage()
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var units = await db.UnitMeasure.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync();
                var convs = await db.UomConversion.AsNoTracking().Where(x => !x.IsDeleted && x.ConversionFactor > 0).ToListAsync();
                var ing = await db.Ingredient.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.BaseUnitId, x.BaseUomId, x.PurchaseUomId, x.IssueUomId }).ToListAsync();
                var sm = await db.SupplierMaterial.AsNoTracking().Where(x => !x.IsDeleted && x.PurchaseUomId != null).GroupBy(x => x.PurchaseUomId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
                var rl = await db.RecipeLine.AsNoTracking().Where(x => !x.IsDeleted).GroupBy(x => (Guid?)(x.UnitId != Guid.Empty ? x.UnitId : x.UomId)).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
                var docs = new Dictionary<Guid, int>();
                void Add(IEnumerable<(Guid? K, int N)> rows)
                {
                    foreach (var (k, n) in rows.Where(r => r.K != null))
                    {
                        docs[k!.Value] = docs.GetValueOrDefault(k.Value) + n;
                    }
                }
                Add((await db.PurchaseRequestLine.AsNoTracking().Where(x => !x.IsDeleted).GroupBy(x => (Guid?)(x.UnitId != Guid.Empty ? x.UnitId : x.UomId)).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).Select(x => (x.Key, x.N)));
                Add((await db.PurchaseOrderLine.AsNoTracking().Where(x => !x.IsDeleted).GroupBy(x => (Guid?)(x.UnitId != Guid.Empty ? x.UnitId : x.UomId)).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).Select(x => (x.Key, x.N)));
                Add((await db.GoodsReceiptLine.AsNoTracking().Where(x => !x.IsDeleted).GroupBy(x => (Guid?)(x.UnitId != Guid.Empty ? x.UnitId : x.UomId)).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).Select(x => (x.Key, x.N)));
                Add((await db.MaterialIssueLine.AsNoTracking().Where(x => !x.IsDeleted).GroupBy(x => (Guid?)(x.UnitId != Guid.Empty ? x.UnitId : x.UomId)).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).Select(x => (x.Key, x.N)));
                var list = units.Select(u =>
                {
                    var c = convs.FirstOrDefault(x => x.FromUnitId == u.Id) ?? convs.FirstOrDefault(x => x.ToUnitId == u.Id && u.UomType == KindPack);
                    var to = c == null ? null : units.FirstOrDefault(x => x.Id == (c.FromUnitId == u.Id ? c.ToUnitId : c.FromUnitId));
                    decimal? f = c == null ? null : c.FromUnitId == u.Id ? c.ConversionFactor : 1 / c.ConversionFactor;
                    return new UnitUsageModel
                    {
                        Id = u.Id, Code = u.Code, Name = u.Name, Symbol = u.Symbol, Kind = u.UomType ?? KindBase, PackType = u.UnitType, Status = u.Status ?? "Active",
                        Factor = f, ToUnitId = to?.Id, ToUnitCode = to?.Symbol ?? to?.Code,
                        IngredientCount = ing.Count(i => i.BaseUnitId == u.Id || i.BaseUomId == u.Id || i.PurchaseUomId == u.Id || i.IssueUomId == u.Id),
                        SupplierPriceCount = sm.Where(x => x.Key == u.Id).Sum(x => x.N),
                        RecipeLineCount = rl.Where(x => x.Key == u.Id).Sum(x => x.N),
                        DocumentLineCount = docs.GetValueOrDefault(u.Id),
                        Locked = (u.Code is "kg" or "g" or "l" or "ml" or "lít") && u.UomType != KindPack,
                    };
                }).OrderBy(x => x.Kind == KindPack ? 1 : 0).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                return new Results<UnitUsageModel>(list, list.Count) { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.GetAllSuccessfully };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetUsage: ", ex.ToString()));
                throw;
            }
        }
    }
}
