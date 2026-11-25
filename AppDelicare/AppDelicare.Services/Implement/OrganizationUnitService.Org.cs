using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.41 (D-122): "Công ty & đơn vị" – cây có công ty làm gốc, 4 chỉ số, chuyển đơn vị (kiểm vòng lặp, đổi công ty cả nhánh), ngừng / hoạt động lại
    /// (chặn khi còn người / đơn vị con đang hoạt động), đặt trưởng tại chỗ; luật Insert / Update (công ty theo cha, loại theo danh mục, trưởng đang làm).
    /// </summary>
    public partial class OrganizationUnitService
    {
        public const string TypeList = "OrganizationUnitType";
        public const string StatusActive = "Active";
        public const string StatusInactive = "Inactive";

        private Dictionary<string, string> TypeLabels()
            => _services.GetRequiredService<IOptions<ReferenceListsOptions>>().Value.Lists.TryGetValue(TypeList, out var l)
                ? l.Where(x => x.Code != null).GroupBy(x => x.Code!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Label ?? g.Key, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private async Task AuditAsync(Guid id, string action, string changes)
        {
            var actor = _services.GetRequiredService<ICurrentActor>().Actor;
            _unitOfWork.DbContext.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = id, EntityName = nameof(OrganizationUnit), Action = action, CreatedDate = _services.GetRequiredService<IClock>().Now,
                CreatedBy = actor?.UserId, IsDeleted = false, Changes = $"- {changes} {ConstantHelper.Break}",
            });
            await Task.CompletedTask;
        }

        /// <summary>Luật chung Insert / Update. Trả lỗi tiếng Việt hoặc null; điền CompanyId theo đơn vị cha.</summary>
        private async Task<string?> RulesAsync(Model.Request.OrganizationUnitRequest request, OrganizationUnit? exist)
        {
            var db = _unitOfWork.DbContext;
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return ConstantHelper.Message.OrgNameRequired;
            }
            var labels = TypeLabels();
            string? type = request.UnitType?.Trim();
            bool unchangedOld = exist != null && string.Equals(exist.UnitType, type, StringComparison.OrdinalIgnoreCase);
            if (labels.Count > 0 && !unchangedOld && (string.IsNullOrEmpty(type) || !labels.ContainsKey(type)))
            {
                return string.Format(ConstantHelper.Message.OrgTypeInvalid, string.Join(", ", labels.Values));
            }
            if (request.ManagerId is { } mid && mid != Guid.Empty)
            {
                var m = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == mid && !e.IsDeleted);
                if (m == null || !EmployeeStatus.IsWorking(m.Status))
                {
                    return ConstantHelper.Message.OrgManagerNotWorking;
                }
            }
            if (request.ParentId is { } pid && pid != Guid.Empty)
            {
                var parent = await db.OrganizationUnit.AsNoTracking().FirstOrDefaultAsync(o => o.Id == pid && !o.IsDeleted);
                if (parent == null)
                {
                    return ConstantHelper.Message.OrgParentNotFound;
                }
                request.CompanyId = parent.CompanyId;
            }
            else
            {
                request.ParentId = null;
                request.CompanyId ??= exist?.CompanyId ?? await db.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Code).Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
            }
            if (request.CompanyId == null || request.CompanyId == Guid.Empty)
            {
                return ConstantHelper.Message.OrgCompanyMissing;
            }
            request.Status = exist?.Status ?? StatusActive; // trạng thái chỉ đổi qua SetStatus
            return null;
        }

        // ================================================================ Cây + chỉ số

        public async Task<IResult<List<OrganizationUnitTreeModel>>> GetOrgTree(Guid? companyId)
        {
            try
            {
                var tree = await GetTree(companyId);
                var roots = tree.Data ?? [];
                var labels = TypeLabels();
                var db = _unitOfWork.DbContext;
                var units = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted).Select(o => new { o.Id, o.CompanyId }).ToDictionaryAsync(o => o.Id, o => o.CompanyId);
                int Fill(OrganizationUnitTreeModel n)
                {
                    n.UnitTypeName = n.UnitType != null && labels.TryGetValue(n.UnitType, out var l) ? l : n.UnitType;
                    n.NoManager = n.ManagerId == null && n.Status != StatusInactive;
                    n.CompanyId = units.GetValueOrDefault(n.Id);
                    n.TotalEmployees = n.EmployeeCount + n.Children.Sum(Fill);
                    return n.TotalEmployees;
                }
                roots.ForEach(r => Fill(r));
                var companies = await db.Company.AsNoTracking().Where(c => !c.IsDeleted && (companyId == null || c.Id == companyId)).OrderBy(c => c.Code).ToListAsync();
                var result = companies.Select(c => new OrganizationUnitTreeModel
                {
                    Id = c.Id, Code = c.Code, Name = c.Name, UnitType = "COMPANY_ROOT", UnitTypeName = "Công ty", IsCompany = true, CompanyId = c.Id, Status = c.Status,
                    Children = [.. roots.Where(r => r.CompanyId == c.Id)],
                }).ToList();
                foreach (var c in result)
                {
                    c.TotalEmployees = c.Children.Sum(x => x.TotalEmployees);
                }
                // Đơn vị gốc không thuộc công ty nào còn hoạt động → gốc riêng.
                result.AddRange(roots.Where(r => !companies.Any(c => c.Id == r.CompanyId)));
                return Result<List<OrganizationUnitTreeModel>>.Success(result, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOrgTree: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<OrgSummaryModel>> GetOrgSummary()
        {
            var db = _unitOfWork.DbContext;
            var units = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted && o.Status != StatusInactive).Select(o => new { o.UnitType, o.ManagerId }).ToListAsync();
            var emps = await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && e.Status != EmployeeStatus.Resigned).Select(e => new { e.OrganizationUnitId }).ToListAsync();
            return Result<OrgSummaryModel>.Success(new OrgSummaryModel
            {
                ActiveUnits = units.Count,
                Companies = await db.Company.AsNoTracking().CountAsync(c => !c.IsDeleted),
                Kitchens = units.Count(u => u.UnitType is "KITCHEN" or "BRANCH"),
                Teams = units.Count(u => u.UnitType is "TEAM" or "DEPARTMENT"),
                Employees = emps.Count(e => e.OrganizationUnitId != null),
                NoManager = units.Count(u => u.ManagerId == null),
                EmployeesNoUnit = emps.Count(e => e.OrganizationUnitId == null),
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ================================================================ Chuyển / trạng thái / trưởng

        public async Task<IResult<bool>> Move(OrgMoveRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var unit = _unitOfWork.OrganizationUnitRepository.GetSingle(o => o.Id == request.Id && !o.IsDeleted);
                if (unit == null)
                {
                    return Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                Guid? newParent = request.NewParentId is { } np && np != Guid.Empty ? np : null;
                Guid? company;
                if (newParent is { } p)
                {
                    if (CreatesCycle(unit.Id, p))
                    {
                        return Fail(ConstantHelper.Message.ParentCycle);
                    }
                    var parent = await db.OrganizationUnit.AsNoTracking().FirstOrDefaultAsync(o => o.Id == p && !o.IsDeleted);
                    if (parent == null)
                    {
                        return Fail(ConstantHelper.Message.OrgParentNotFound);
                    }
                    company = parent.CompanyId;
                }
                else
                {
                    company = request.CompanyId ?? unit.CompanyId;
                    if (!await db.Company.AsNoTracking().AnyAsync(c => c.Id == company && !c.IsDeleted))
                    {
                        return Fail(ConstantHelper.Message.OrgCompanyMissing);
                    }
                }
                string from = unit.ParentId?.ToString() ?? "gốc";
                unit.ParentId = newParent;
                int moved = 0;
                if (company is { } cid && cid != unit.CompanyId)
                {
                    // Đổi công ty cả nhánh.
                    var all = _unitOfWork.OrganizationUnitRepository.GetAll().Where(o => !o.IsDeleted).ToList();
                    var branch = new HashSet<Guid> { unit.Id };
                    for (bool grew = true; grew;)
                    {
                        grew = false;
                        foreach (var o in all.Where(o => o.ParentId != null && branch.Contains(o.ParentId.Value) && !branch.Contains(o.Id)))
                        {
                            branch.Add(o.Id);
                            grew = true;
                        }
                    }
                    foreach (var o in all.Where(o => branch.Contains(o.Id)))
                    {
                        o.CompanyId = cid;
                        _unitOfWork.OrganizationUnitRepository.Update(o);
                        moved++;
                    }
                }
                _unitOfWork.OrganizationUnitRepository.Update(unit);
                await AuditAsync(unit.Id, "Move", $"ParentId: {from} → {(newParent?.ToString() ?? "gốc")}{(moved > 0 ? $" | Đổi công ty {moved} đơn vị" : string.Empty)}");
                await _unitOfWork.OrganizationUnitRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.OrgMoved);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Move: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<bool> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<bool>.Fail(message);
            }
        }

        public async Task<IResult<bool>> SetStatus(OrgSetStatusRequest request)
        {
            var db = _unitOfWork.DbContext;
            var unit = _unitOfWork.OrganizationUnitRepository.GetSingle(o => o.Id == request.Id && !o.IsDeleted);
            if (unit == null)
            {
                return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            string status = request.Status?.Trim() ?? string.Empty;
            if (status is not (StatusActive or StatusInactive))
            {
                return Result<bool>.Fail(ConstantHelper.Message.OrgStatusInvalid);
            }
            if (status == StatusInactive)
            {
                int people = await db.Employee.CountAsync(e => !e.IsDeleted && e.OrganizationUnitId == unit.Id && e.Status != EmployeeStatus.Resigned);
                int children = await db.OrganizationUnit.CountAsync(o => !o.IsDeleted && o.ParentId == unit.Id && o.Status != StatusInactive);
                if (people > 0 || children > 0)
                {
                    return Result<bool>.Fail(string.Format(ConstantHelper.Message.OrgDeactivateBlocked, people, children));
                }
            }
            else if (unit.ParentId is { } p && await db.OrganizationUnit.AnyAsync(o => o.Id == p && o.Status == StatusInactive))
            {
                return Result<bool>.Fail(ConstantHelper.Message.OrgReactivateParentInactive);
            }
            string old = unit.Status ?? StatusActive;
            unit.Status = status;
            _unitOfWork.OrganizationUnitRepository.Update(unit);
            await _unitOfWork.OrganizationUnitRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, status == StatusInactive ? ConstantHelper.Message.OrgDeactivated : ConstantHelper.Message.OrgReactivated);
        }

        public async Task<IResult<bool>> SetManager(OrgSetManagerRequest request)
        {
            var db = _unitOfWork.DbContext;
            var unit = _unitOfWork.OrganizationUnitRepository.GetSingle(o => o.Id == request.Id && !o.IsDeleted);
            if (unit == null)
            {
                return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            if (request.EmployeeId is { } eid && eid != Guid.Empty)
            {
                var e = await db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == eid && !x.IsDeleted);
                if (e == null || !EmployeeStatus.IsWorking(e.Status))
                {
                    return Result<bool>.Fail(ConstantHelper.Message.OrgManagerNotWorking);
                }
                unit.ManagerId = eid;
            }
            else
            {
                unit.ManagerId = null;
            }
            _unitOfWork.OrganizationUnitRepository.Update(unit);
            await _unitOfWork.OrganizationUnitRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, ConstantHelper.Message.OrgManagerSet);
        }
    }
}
