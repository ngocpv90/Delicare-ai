using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>UI-6.20 (D-101): dữ liệu cho form Nguyên liệu – 3 loại bảo quản kèm ngưỡng nhận hàng (BusinessRules:ReceivingTemp), quy đổi đơn vị chung
    /// (để chỉ cho chọn đơn vị mua / xuất quy đổi được về đơn vị kho), định mức tồn riêng theo kho của nguyên liệu (chỉ xem).</summary>
    public partial class IngredientService
    {
        /// <summary>UI-6.21 (D-102): chọn nhóm → gợi ý loại bảo quản + đơn vị kho theo đa số nguyên liệu đang dùng cùng nhóm (tính khi gọi, không lưu).</summary>
        public async Task<IResult<IngredientCategoryDefaultsModel>> GetCategoryDefaults(Guid categoryId)
        {
            try
            {
                var rows = await _unitOfWork.DbContext.Ingredient.AsNoTracking()
                    .Where(x => !x.IsDeleted && (x.Status == null || x.Status == "Active") && (x.IngredientCategoryId == categoryId || (x.IngredientCategoryId == null && x.CategoryId == categoryId)))
                    .Select(x => new { x.StorageType, Unit = x.BaseUnitId ?? x.BaseUomId }).ToListAsync();
                var model = new IngredientCategoryDefaultsModel { SampleCount = rows.Count };
                var st = rows.Where(r => !string.IsNullOrEmpty(r.StorageType)).GroupBy(r => r.StorageType!.ToUpperInvariant() == "AMBIENT" ? "DRY" : r.StorageType!.ToUpperInvariant())
                    .OrderByDescending(g => g.Count()).FirstOrDefault();
                var un = rows.Where(r => r.Unit != null).GroupBy(r => r.Unit).OrderByDescending(g => g.Count()).FirstOrDefault();
                model.StorageType = st?.Key;
                model.StorageCount = st?.Count() ?? 0;
                model.BaseUnitId = un?.Key;
                model.UnitCount = un?.Count() ?? 0;
                return Result<IngredientCategoryDefaultsModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCategoryDefaults: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<IngredientFormOptionsModel>> GetFormOptions(Guid? id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var model = new IngredientFormOptionsModel();
                foreach (var (code, label) in new[] { ("DRY", "Khô / thường"), ("CHILLED", "Mát 0–5 °C"), ("FROZEN", "Đông lạnh ≤ −18 °C") })
                {
                    var rule = _rules.ReceivingTemp.GetValueOrDefault(code);
                    model.ListStorageType.Add(new IngredientStorageOptionModel
                    {
                        Code = code, Label = label, AcceptMax = rule?.AcceptMax, ConditionalMax = rule?.ConditionalMax,
                        Note = rule == null ? "Không kiểm nhiệt độ khi nhận hàng" : $"Nhận hàng: ≤ {rule.AcceptMax:0.#} °C nhận; ≤ {rule.ConditionalMax:0.#} °C nhận có điều kiện; cao hơn loại",
                    });
                }
                DateTime today = _clock.Today;
                model.ListConversion = await db.UomConversion.AsNoTracking()
                    .Where(x => !x.IsDeleted && x.FromUnitId != Guid.Empty && x.ToUnitId != Guid.Empty && x.ConversionFactor > 0
                        && (x.EffectiveFrom == null || x.EffectiveFrom <= today) && (x.EffectiveTo == null || x.EffectiveTo >= today))
                    .Select(x => new UomEdgeModel { FromUnitId = x.FromUnitId, ToUnitId = x.ToUnitId, Factor = x.ConversionFactor }).ToListAsync();
                if (id is { } iid)
                {
                    model.ListWarehousePolicy = await (from p in db.InventoryPolicy.AsNoTracking()
                                                       join w in db.Warehouse.AsNoTracking() on p.WarehouseId equals w.Id
                                                       where p.IngredientId == iid && !p.IsDeleted && (p.Status == null || p.Status == "Active")
                                                       select new IngredientWarehousePolicyModel { WarehouseName = w.Name ?? w.Code, MinStock = p.MinStock ?? p.MinimumStock, SafetyStock = p.SafetyStock }).ToListAsync();
                }
                return Result<IngredientFormOptionsModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetFormOptions: ", ex.ToString()));
                throw;
            }
        }
    }
}
