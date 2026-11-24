using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Interface
{
    /// <summary>UI-6.26 (D-107): khai dây chuyền + gán nhóm món (màn "Dây chuyền & trạm bếp").</summary>
    public interface IProductionLineSetupService
    {
        Task<IResult<ProductionLineOverviewModel>> GetOverview();
        Task<IResult<ProductionLineSetupModel>> SaveWithCategories(ProductionLineSaveRequest request);
        Task<IResult<int>> AssignProductionLine(DishCategoryAssignLineRequest request);
    }
}

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.26 (D-107, GAP-PRD-10): dây chuyền gắn theo nhóm món (<c>DishCategorie.ProductionLineId</c>) – ra lệnh chia mẻ đúng dây chuyền (ProductionLineRules).
    /// GetOverview: dây chuyền + nhóm món gán + số món + nhóm chưa gán; SaveWithCategories: form dây chuyền lưu kèm nhóm món (một giao dịch, mã trống → DC-NNN);
    /// AssignProductionLine: Gán nhanh nhiều nhóm (một giao dịch). Dây chuyền Ngừng hoạt động không gán được cho nhóm.
    /// </summary>
    public class ProductionLineSetupService(IUnitOfWork unitOfWork, ICurrentActor currentActor) : Interface.IProductionLineSetupService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ICurrentActor _currentActor = currentActor;

        public async Task<IResult<ProductionLineOverviewModel>> GetOverview()
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var lines = await db.ProductionLine.AsNoTracking().Where(l => !l.IsDeleted).ToListAsync();
                var cats = await db.DishCategory.AsNoTracking().Where(c => !c.IsDeleted && (c.Status == null || c.Status != "Inactive")).Select(c => new { c.Id, c.Name, c.ProductionLineId }).ToListAsync();
                var dishCount = (await db.Dish.AsNoTracking().Where(d => !d.IsDeleted && (d.Status == null || d.Status == "Active") && d.DishCategoryId != null)
                    .GroupBy(d => d.DishCategoryId!.Value).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).ToDictionary(x => x.Key, x => x.N);
                var byId = lines.ToDictionary(l => l.Id);
                var model = new ProductionLineOverviewModel
                {
                    ListCategory = [.. cats.Select(c =>
                    {
                        var line = c.ProductionLineId is { } l ? byId.GetValueOrDefault(l) : null;
                        return new DishCategoryLineModel
                        {
                            Id = c.Id, Name = c.Name, ProductionLineId = c.ProductionLineId, ProductionLineName = line?.Name ?? line?.Code,
                            LineInactive = c.ProductionLineId != null && !ProductionLineRules.IsActive(line), DishCount = dishCount.GetValueOrDefault(c.Id),
                        };
                    }).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)],
                };
                var unassigned = model.ListCategory.Where(c => c.ProductionLineId == null || c.LineInactive).ToList();
                model.UnassignedCategoryCount = unassigned.Count;
                model.UnassignedDishCount = unassigned.Sum(c => c.DishCount);
                model.ListLine = [.. lines.Select(l => new ProductionLineSetupModel
                {
                    Id = l.Id, Code = l.Code, Name = l.Name, CapacityMealPerBatch = l.CapacityMealPerBatch, MinutesPerBatch = l.MinutesPerBatch, Status = l.Status ?? "Active", Note = l.Note,
                    ListCategory = [.. model.ListCategory.Where(c => c.ProductionLineId == l.Id)],
                    DishCount = model.ListCategory.Where(c => c.ProductionLineId == l.Id).Sum(c => c.DishCount),
                }).OrderBy(l => ProductionLineRules.IsActive(byId[l.Id]) ? 0 : 1).ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)];
                return Result<ProductionLineOverviewModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<ProductionLineSetupModel>> SaveWithCategories(ProductionLineSaveRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Fail(ConstantHelper.Message.ProductionLineNameRequired);
                }
                if (!(request.CapacityMealPerBatch > 0) || !(request.MinutesPerBatch > 0))
                {
                    return Fail(ConstantHelper.Message.ProductionLineCapacityInvalid);
                }
                ProductionLine? line = request.Id is { } id && id != Guid.Empty ? _unitOfWork.ProductionLineRepository.GetSingle(l => l.Id == id && !l.IsDeleted) : null;
                if (request.Id is { } rid && rid != Guid.Empty && line == null)
                {
                    return Fail(ConstantHelper.Message.ProductionLineNotFound);
                }
                string name = request.Name.Trim();
                string lower = name.ToLower();
                Guid self = line?.Id ?? Guid.Empty;
                if (await db.ProductionLine.AsNoTracking().AnyAsync(l => !l.IsDeleted && l.Id != self && l.Name != null && l.Name.ToLower() == lower))
                {
                    return Fail(string.Format(ConstantHelper.Message.GroupNameDuplicate, name));
                }
                string status = string.IsNullOrWhiteSpace(request.Status) ? line?.Status ?? "Active" : request.Status.Trim();
                bool isNew = line == null;
                if (isNew)
                {
                    string? code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim().ToUpperInvariant();
                    if (code == null)
                    {
                        var used = await db.ProductionLine.AsNoTracking().Where(l => l.Code != null && l.Code.StartsWith("DC-")).Select(l => l.Code!).ToListAsync();
                        int max = used.Select(c => int.TryParse(c[3..], out int n) ? n : 0).DefaultIfEmpty(0).Max();
                        code = $"DC-{max + 1:000}";
                    }
                    else if (await db.ProductionLine.AsNoTracking().AnyAsync(l => !l.IsDeleted && l.Code == code))
                    {
                        return Fail(string.Format(ConstantHelper.Message.CodeExist, code));
                    }
                    line = new ProductionLine { Id = Guid.NewGuid(), Code = code };
                    _unitOfWork.ProductionLineRepository.Add(line);
                }
                else
                {
                    _unitOfWork.ProductionLineRepository.Update(line!);
                }
                line!.Name = name;
                line.CapacityMealPerBatch = request.CapacityMealPerBatch;
                line.MinutesPerBatch = request.MinutesPerBatch;
                line.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
                line.Status = status;
                await _unitOfWork.ProductionLineRepository.CommitAsync(isNew ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update);

                var wanted = request.ListDishCategoryId.Where(x => x != Guid.Empty).Distinct().ToList();
                if (wanted.Count != 0 && !ProductionLineRules.IsActive(line))
                {
                    return Fail(string.Format(ConstantHelper.Message.ProductionLineInactiveAssign, line.Name));
                }
                var cats = _unitOfWork.DishCategoryRepository.GetAll().Where(c => !c.IsDeleted && (wanted.Contains(c.Id) || c.ProductionLineId == line.Id)).ToList();
                var missing = wanted.Where(w => cats.All(c => c.Id != w)).ToList();
                if (missing.Count != 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.DishCategoryNotFoundIds, missing.Count));
                }
                bool changed = false;
                foreach (var c in cats)
                {
                    Guid? target = wanted.Contains(c.Id) ? line.Id : null;
                    if (c.ProductionLineId != target)
                    {
                        c.ProductionLineId = target;
                        _unitOfWork.DishCategoryRepository.Update(c);
                        changed = true;
                    }
                }
                if (changed)
                {
                    await _unitOfWork.DishCategoryRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                _unitOfWork.CommitTransaction();
                var overview = (await GetOverview()).Data!;
                return Result<ProductionLineSetupModel>.Success(overview.ListLine.First(l => l.Id == line.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveWithCategories: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ProductionLineSetupModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ProductionLineSetupModel>.Fail(message);
            }
        }

        public async Task<IResult<int>> AssignProductionLine(DishCategoryAssignLineRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var items = request.ListItem.Where(i => i.DishCategoryId != Guid.Empty).GroupBy(i => i.DishCategoryId).Select(g => g.Last()).ToList();
                var catIds = items.Select(i => i.DishCategoryId).ToList();
                var cats = _unitOfWork.DishCategoryRepository.GetAll().Where(c => !c.IsDeleted && catIds.Contains(c.Id)).ToList();
                if (cats.Count != catIds.Count)
                {
                    return Fail(string.Format(ConstantHelper.Message.DishCategoryNotFoundIds, catIds.Count - cats.Count));
                }
                var lineIds = items.Where(i => i.ProductionLineId is { } l && l != Guid.Empty).Select(i => i.ProductionLineId!.Value).Distinct().ToList();
                var lines = await db.ProductionLine.AsNoTracking().Where(l => lineIds.Contains(l.Id) && !l.IsDeleted).ToDictionaryAsync(l => l.Id);
                int changed = 0;
                foreach (var item in items)
                {
                    var cat = cats.First(c => c.Id == item.DishCategoryId);
                    Guid? target = item.ProductionLineId is { } l && l != Guid.Empty ? l : null;
                    if (target is { } t && t != cat.ProductionLineId)
                    {
                        var line = lines.GetValueOrDefault(t);
                        if (line == null)
                        {
                            return Fail(ConstantHelper.Message.ProductionLineNotFound);
                        }
                        if (!ProductionLineRules.IsActive(line))
                        {
                            return Fail(string.Format(ConstantHelper.Message.ProductionLineInactiveAssign, line.Name ?? line.Code));
                        }
                    }
                    if (cat.ProductionLineId != target)
                    {
                        cat.ProductionLineId = target;
                        _unitOfWork.DishCategoryRepository.Update(cat);
                        changed++;
                    }
                }
                if (changed != 0)
                {
                    await _unitOfWork.DishCategoryRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                _unitOfWork.CommitTransaction();
                return Result<int>.Success(changed, string.Format(ConstantHelper.Message.DishCategoryLineAssigned, changed));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " AssignProductionLine: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<int> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<int>.Fail(message);
            }
        }
    }
}
