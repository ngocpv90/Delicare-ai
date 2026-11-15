using AutoMapper;
using Dapper;
using Microsoft.EntityFrameworkCore;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class ObjectMenuService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IPermissionService permissionService) : IObjectMenuService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IPermissionService _permissionService = permissionService;

        public async Task<IResults<ObjectMenuViewModel>> GetAll(ObjectMenuSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ParentId", request.ParentId);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ObjectMenuViewModel>("uspObjectMenu_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ObjectMenuViewModel>
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

        public IResults<ObjectMenu> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ObjectMenuRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ObjectMenu>
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

        public async Task<IResult<ObjectMenu>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ObjectMenuRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ObjectMenu>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = data,
                    };
                }
                return new Result<ObjectMenu>
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

        public async Task<IResult<bool>> Save(ObjectMenuRequest request, string typeSave)
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
                        break;
                }
                _unitOfWork.CommitTransaction();
                _permissionService.InvalidateAll(); // T45R: menu đổi → quyền theo MetaLink đổi
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

        private async Task<string> Insert(ObjectMenuRequest request, string typeSave)
        {
            if (MetaLinkExists(request.MetaLink, null))
            {
                return string.Format(ConstantHelper.Message.MetaLinkExist, request.MetaLink);
            }
            var insert = _mapper.Map<ObjectMenu>(request);
            _unitOfWork.ObjectMenuRepository.Add(insert);
            await _unitOfWork.ObjectMenuRepository.CommitAsync(typeSave);
            var listUserGroup = _unitOfWork.UserGroupRepository.GetAll().Where(x => !x.IsDeleted).ToList();
            if (listUserGroup.Count != 0)
            {
                foreach (var item in listUserGroup)
                {
                    UserRole userRole = new()
                    {
                        Id = Guid.NewGuid(),
                        UserGroupId = item.Id,
                        ObjectMenuId = insert.Id,
                    };
                    _unitOfWork.UserRoleRepository.Add(userRole);
                }
                await _unitOfWork.UserRoleRepository.CommitAsync(typeSave);
            }
            return string.Empty;
        }

        private async Task<string> Update(ObjectMenuRequest request, string typeSave)
        {
            var exist = _unitOfWork.ObjectMenuRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (string.IsNullOrEmpty(request.Id.ToString()) || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (MetaLinkExists(request.MetaLink, request.Id))
            {
                return string.Format(ConstantHelper.Message.MetaLinkExist, request.MetaLink);
            }
            var update = _mapper.Map(request, exist);
            _unitOfWork.ObjectMenuRepository.Update(update);
            await _unitOfWork.ObjectMenuRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (string.IsNullOrEmpty(id.ToString()))
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.ObjectMenuRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.ObjectMenuRepository.Update(delete);
            await _unitOfWork.ObjectMenuRepository.CommitAsync(typeSave);
            var list = _unitOfWork.UserRoleRepository.GetAll().Where(x => x.ObjectMenuId == id && !x.IsDeleted).ToList();
            if (list.Count != 0)
            {
                foreach (var item in list)
                {
                    _unitOfWork.UserRoleRepository.Update(item);
                }
                await _unitOfWork.UserRoleRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
            }
            return string.Empty;
        }

        public async Task<IResult<bool>> SaveIsActive(Guid id)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                if (string.IsNullOrEmpty(id.ToString()))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = false
                    };
                }
                var update = _unitOfWork.ObjectMenuRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                if (update == null)
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = false
                    };
                }
                update.IsActive = !update.IsActive;
                _unitOfWork.ObjectMenuRepository.Update(update);
                await _unitOfWork.ObjectMenuRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                _permissionService.InvalidateAll(); // T45R: menu đổi → quyền theo MetaLink đổi
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveIsActive: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private bool MetaLinkExists(string? metaLink, Guid? currentId)
            => !string.IsNullOrWhiteSpace(metaLink)
                && _unitOfWork.ObjectMenuRepository.GetSingle(x => x.MetaLink == metaLink && x.Id != currentId && !x.IsDeleted) != null;

        public async Task<IResult<List<ObjectMenuTreeModel>>> GetTree(bool includeInactive)
        {
            try
            {
                var menus = await _unitOfWork.ObjectMenuRepository.GetAll().AsNoTracking()
                    .Where(x => !x.IsDeleted && (includeInactive || x.IsActive))
                    .OrderBy(x => x.LevelShow).ThenBy(x => x.OrderShow).ThenBy(x => x.Name)
                    .Select(x => new ObjectMenuTreeModel
                    {
                        Id = x.Id, Name = x.Name, ShortName = x.ShortName, MetaLink = x.MetaLink, ParentId = x.ParentId,
                        LevelShow = x.LevelShow, OrderShow = x.OrderShow, IconName = x.IconName, IsActive = x.IsActive,
                    })
                    .ToListAsync();
                var roots = new List<ObjectMenuTreeModel>();
                foreach (var m in menus)
                {
                    var parent = m.ParentId == null ? null : menus.FirstOrDefault(p => p.Id == m.ParentId && p != m);
                    if (parent != null)
                    {
                        parent.Children.Add(m);
                    }
                    else
                    {
                        roots.Add(m);
                    }
                }
                return Result<List<ObjectMenuTreeModel>>.Success(roots, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTree: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> UpdateOrder(List<ObjectMenuOrderRequest> request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var ids = request.Select(x => x.Id).Concat(request.Where(x => x.ParentId != null).Select(x => x.ParentId!.Value)).Distinct().ToList();
                var menus = _unitOfWork.ObjectMenuRepository.GetAll().Where(x => ids.Contains(x.Id) && !x.IsDeleted).ToList();
                var missing = ids.Where(id => !menus.Any(m => m.Id == id)).ToList();
                if (request.Count == 0 || missing.Count != 0)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(string.Format(ConstantHelper.Message.MenuNotFound, string.Join(ConstantHelper.CommaSpace, missing)), false);
                }
                // Chặn vòng lặp cha – con sau khi áp thay đổi.
                var parents = _unitOfWork.ObjectMenuRepository.GetAll().AsNoTracking().Where(x => !x.IsDeleted).ToDictionary(x => x.Id, x => x.ParentId);
                foreach (var item in request)
                {
                    parents[item.Id] = item.ParentId;
                }
                foreach (var item in request)
                {
                    var seen = new HashSet<Guid> { item.Id };
                    Guid? cursor = item.ParentId;
                    while (cursor != null)
                    {
                        if (!seen.Add(cursor.Value))
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<bool>.Fail(ConstantHelper.Message.MenuCycle, false);
                        }
                        cursor = parents.GetValueOrDefault(cursor.Value);
                    }
                }
                foreach (var item in request)
                {
                    var m = menus.First(x => x.Id == item.Id);
                    m.ParentId = item.ParentId;
                    m.OrderShow = item.OrderShow;
                    m.LevelShow = item.ParentId == null ? 1 : 2;
                    _unitOfWork.ObjectMenuRepository.Update(m);
                }
                await _unitOfWork.ObjectMenuRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                _permissionService.InvalidateAll();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateOrder: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
