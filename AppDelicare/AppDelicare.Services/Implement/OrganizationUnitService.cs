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
using Microsoft.EntityFrameworkCore;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Implement
{
    public class OrganizationUnitService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IOrganizationUnitService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<OrganizationUnitViewModel>> GetAll(OrganizationUnitSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CompanyId", request.CompanyId);
                dynamicParameters.Add("@ParentId", request.ParentId);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<OrganizationUnitViewModel>("uspOrganizationUnit_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<OrganizationUnitViewModel>
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

        public IResults<OrganizationUnit> GetAllData()
        {
            try
            {
                var list = _unitOfWork.OrganizationUnitRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<OrganizationUnit>
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

        public async Task<IResult<bool>> Save(OrganizationUnitRequest request, string typeSave)
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

        private async Task<string> Insert(OrganizationUnitRequest request, string typeSave)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return "Mã không được để trống.";
            }
            var checkDuplicate = _unitOfWork.OrganizationUnitRepository.GetSingle(x => x.Code == request.Code && !x.IsDeleted);
            if (checkDuplicate != null)
            {
                return string.Format(ConstantHelper.Message.CodeExist, request.Code);
            }
            var insert = _mapper.Map<OrganizationUnit>(request);
            _unitOfWork.OrganizationUnitRepository.Add(insert);
            await _unitOfWork.OrganizationUnitRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(OrganizationUnitRequest request, string typeSave)
        {
            var exist = _unitOfWork.OrganizationUnitRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return "Mã không được để trống.";
            }
            var checkDuplicate = _unitOfWork.OrganizationUnitRepository.GetSingle(x => x.Id != request.Id && x.Code == request.Code && !x.IsDeleted);
            if (checkDuplicate != null)
            {
                return string.Format(ConstantHelper.Message.CodeExist, request.Code);
            }
            if (CreatesCycle(request.Id!.Value, request.ParentId))
            {
                return ConstantHelper.Message.ParentCycle;
            }
            var update = _mapper.Map(request, exist);
            _unitOfWork.OrganizationUnitRepository.Update(update);
            await _unitOfWork.OrganizationUnitRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(OrganizationUnit), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.OrganizationUnitRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.OrganizationUnitRepository.Update(delete);
            await _unitOfWork.OrganizationUnitRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<OrganizationUnit>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.OrganizationUnitRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<OrganizationUnit>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<OrganizationUnit>
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

        /// <summary>T45R (spec 3.5): đơn vị cha không được là chính nó hoặc đơn vị con của nó.</summary>
        private bool CreatesCycle(Guid id, Guid? parentId)
        {
            var seen = new HashSet<Guid>();
            Guid? cursor = parentId;
            while (cursor != null)
            {
                if (cursor == id)
                {
                    return true;
                }
                if (!seen.Add(cursor.Value))
                {
                    return false;
                }
                Guid current = cursor.Value;
                cursor = _unitOfWork.OrganizationUnitRepository.GetAll().Where(x => x.Id == current).Select(x => x.ParentId).FirstOrDefault();
            }
            return false;
        }

        public async Task<IResult<List<OrganizationUnitTreeModel>>> GetTree(Guid? companyId)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var units = await db.OrganizationUnit.AsNoTracking().Where(x => !x.IsDeleted && (companyId == null || x.CompanyId == companyId))
                    .OrderBy(x => x.Code).ToListAsync();
                var unitIds = units.Select(x => x.Id).ToList();
                var employees = await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && e.OrganizationUnitId != null && unitIds.Contains(e.OrganizationUnitId.Value))
                    .GroupBy(e => e.OrganizationUnitId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
                var warehouses = await db.Warehouse.AsNoTracking().Where(w => !w.IsDeleted && w.OrganizationUnitId != null && unitIds.Contains(w.OrganizationUnitId.Value))
                    .GroupBy(w => w.OrganizationUnitId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
                var managerIds = units.Where(x => x.ManagerId != null).Select(x => x.ManagerId!.Value).Distinct().ToList();
                var managers = await db.Employee.AsNoTracking().Where(e => managerIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName);
                var nodes = units.ToDictionary(x => x.Id, x => new OrganizationUnitTreeModel
                {
                    Id = x.Id, Code = x.Code, Name = x.Name, UnitType = x.UnitType, ParentId = x.ParentId, ManagerId = x.ManagerId,
                    ManagerName = x.ManagerId is { } m ? managers.GetValueOrDefault(m) : null,
                    EmployeeCount = employees.GetValueOrDefault(x.Id), WarehouseCount = warehouses.GetValueOrDefault(x.Id), Status = x.Status,
                });
                var roots = new List<OrganizationUnitTreeModel>();
                foreach (var x in units)
                {
                    if (x.ParentId is { } p && p != x.Id && nodes.TryGetValue(p, out var parent))
                    {
                        parent.Children.Add(nodes[x.Id]);
                    }
                    else
                    {
                        roots.Add(nodes[x.Id]);
                    }
                }
                return Result<List<OrganizationUnitTreeModel>>.Success(roots, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTree: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T45R (spec 3.5): đơn vị UnitType = BRANCH cho bộ chọn đơn vị trên header.</summary>
        public IResults<OrganizationUnit> GetBranches()
        {
            try
            {
                var list = _unitOfWork.OrganizationUnitRepository.GetAll().Where(x => !x.IsDeleted && x.UnitType == BranchType).OrderBy(x => x.Code).ToList();
                return new Results<OrganizationUnit>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBranches: ", ex.ToString()));
                throw;
            }
        }

        public const string BranchType = "BRANCH";
    }
}
