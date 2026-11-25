using Dapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public partial class UserRoleService(IUnitOfWork unitOfWork, IObjectMenuService objectMenuService, IDapperRepository dapperRepository
        , IPermissionService permissionService, IServiceProvider services) : IUserRoleService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IObjectMenuService _objectMenuService = objectMenuService;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly IServiceProvider _services = services;

        public async Task<IResult<bool>> DeleteByUserGroupId(Guid userGroupId)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var list = GetAllData().ListData.Where(x => x.UserGroupId == userGroupId).ToList();
                if (list.Count != 0)
                {
                    foreach (var item in list)
                    {
                        _unitOfWork.UserRoleRepository.Update(item);
                    }
                    await _unitOfWork.UserRoleRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                }
                _unitOfWork.CommitTransaction();
                _permissionService.InvalidateAll();
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " DeleteByUserGroupId: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public IResults<UserRole> GetAllData()
        {
            try
            {
                var list = _unitOfWork.UserRoleRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<UserRole>
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

        public async Task<IResult<UserRoleModel>> GetByUserGroupId(string listUserGroupId, bool isUseManage)
        {
            try
            {
                List<UserRoleViewModel> listMenu = [];
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@ListUserGroupId", listUserGroupId);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<UserRoleViewModel>("uspUserRole_GetByUserGroupId", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    List<UserRoleViewModel> listLevel1 = [.. list.Where(x => x.LevelShow == 1).OrderBy(x => x.OrderShow)];
                    if (!isUseManage)
                    {
                        listLevel1 = listLevel1.Where(x => x.AllowAccess).DistinctBy(x => x.ObjectMenuName).ToList();
                    }
                    List<UserRoleViewModel> listLevel2 = [.. list.Where(x => x.LevelShow == 2).OrderBy(x => x.OrderShow)];
                    if (!isUseManage)
                    {
                        listLevel2 = listLevel2.Where(x => x.AllowAccess).DistinctBy(x => x.ObjectMenuName).ToList();
                    }
                    foreach (var item1 in listLevel1)
                    {
                        List<UserRoleViewModel> listMenuLevel2 = [];
                        foreach (var item2 in listLevel2.Where(x => x.ParentId == item1.ObjectMenuId))
                        {
                            UserRoleViewModel menuLevel2 = new()
                            {
                                Id = item2.Id,
                                ObjectMenuName = item2.ObjectMenuName,
                                ShortName = item2.ShortName,
                                ParentId = item1.Id,
                                MetaLink = item2.MetaLink,
                                IconName = item2.IconName,
                                LevelShow = item2.LevelShow,
                                OrderShow = item2.OrderShow,
                                DataTarget = item2.DataTarget,
                                AllowAccess = item2.AllowAccess,
                                AllowAdd = item2.AllowAdd,
                                AllowEdit = item2.AllowEdit,
                                AllowDelete = item2.AllowDelete,
                                AllowPrint = item2.AllowPrint,
                                AllowExport = item2.AllowExport,
                                AllowImport = item2.AllowImport,
                                Children = []
                            };
                            listMenuLevel2.Add(menuLevel2);
                        }
                        UserRoleViewModel menuLevel1 = new()
                        {
                            Id = item1.Id,
                            ObjectMenuName = item1.ObjectMenuName,
                            ShortName = item1.ShortName,
                            MetaLink = item1.MetaLink,
                            IconName = item1.IconName,
                            LevelShow = item1.LevelShow,
                            OrderShow = item1.OrderShow,
                            DataTarget = item1.DataTarget,
                            AriaControls = item1.AriaControls,
                            AllowAccess = item1.AllowAccess,
                            AllowAdd = item1.AllowAdd,
                            AllowEdit = item1.AllowEdit,
                            AllowDelete = item1.AllowDelete,
                            AllowPrint = item1.AllowPrint,
                            AllowExport = item1.AllowExport,
                            AllowImport = item1.AllowImport,
                            Children = listMenuLevel2
                        };
                        listMenu.Add(menuLevel1);
                    }
                }
                UserRoleModel userRoleModel = new()
                {
                    ListMenu = listMenu,
                };
                return new Result<UserRoleModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = userRoleModel
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByUserGroupId: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Insert(Guid userGroupId)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var listObjectMenu = _objectMenuService.GetAllData().ListData;
                foreach (var item in listObjectMenu)
                {
                    UserRole model = new()
                    {
                        Id = Guid.NewGuid(),
                        UserGroupId = userGroupId,
                        ObjectMenuId = item.Id,
                        // T45R (CODE_AUDIT A22): nhóm mới KHÔNG có quyền nào – trước đây cấp toàn quyền mọi menu. Admin cấp trên ma trận quyền (PROPOSED – BA/PO CONFIRM).
                    };
                    _unitOfWork.UserRoleRepository.Add(model);
                }
                await _unitOfWork.UserRoleRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                _permissionService.InvalidateAll();
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Insert: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// Cập nhật ma trận quyền. T45R: dòng chưa có (Id rỗng / không tồn tại) mà có UserGroupId + ObjectMenuId → tạo mới;
        /// chuẩn hóa cờ (bật cờ bất kỳ → bật AllowAccess; tắt AllowAccess → tắt hết – spec 3.3); xóa cache quyền.
        /// </summary>
        public async Task<IResult<bool>> Update(UserRoleRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var lstRole = JsonConvert.DeserializeObject<List<UserRole>>(request.ListPermission ?? string.Empty) ?? [];
                var ids = lstRole.Select(x => x.Id).Where(x => x != Guid.Empty).ToList();
                var existing = _unitOfWork.UserRoleRepository.GetAll().Where(x => ids.Contains(x.Id) && !x.IsDeleted).ToList();
                var newRows = lstRole.Where(x => !existing.Any(e => e.Id == x.Id)).ToList();
                var groupIds = newRows.Where(x => x.UserGroupId != null).Select(x => x.UserGroupId!.Value).Distinct().ToList();
                var menuIds = newRows.Where(x => x.ObjectMenuId != null).Select(x => x.ObjectMenuId!.Value).Distinct().ToList();
                var knownGroups = _unitOfWork.UserGroupRepository.GetAll().Where(x => groupIds.Contains(x.Id) && !x.IsDeleted).Select(x => x.Id).ToList();
                var knownMenus = _unitOfWork.ObjectMenuRepository.GetAll().Where(x => menuIds.Contains(x.Id) && !x.IsDeleted).Select(x => x.Id).ToList();
                var invalid = newRows.Where(x => x.UserGroupId == null || x.ObjectMenuId == null
                    || !knownGroups.Contains(x.UserGroupId.Value) || !knownMenus.Contains(x.ObjectMenuId.Value)).ToList();
                if (invalid.Count != 0)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(string.Format(ConstantHelper.Message.MenuNotFound, string.Join(ConstantHelper.CommaSpace, invalid.Select(x => x.ObjectMenuId?.ToString() ?? x.Id.ToString()))), false);
                }
                bool anyInsert = false;
                foreach (var item in lstRole)
                {
                    Normalize(item);
                    var row = existing.FirstOrDefault(x => x.Id == item.Id)
                        ?? _unitOfWork.UserRoleRepository.GetSingle(x => x.UserGroupId == item.UserGroupId && x.ObjectMenuId == item.ObjectMenuId && !x.IsDeleted);
                    if (row == null)
                    {
                        if (!item.AllowAccess)
                        {
                            continue;
                        }
                        row = new UserRole { Id = Guid.NewGuid(), UserGroupId = item.UserGroupId, ObjectMenuId = item.ObjectMenuId };
                        _unitOfWork.UserRoleRepository.Add(row);
                        anyInsert = true;
                    }
                    else
                    {
                        _unitOfWork.UserRoleRepository.Update(row);
                    }
                    CopyFlags(item, row);
                }
                await _unitOfWork.UserRoleRepository.CommitAsync(anyInsert ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                _permissionService.InvalidateAll();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Update: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<UserRoleModel>> GetMatrix(Guid userGroupId)
        {
            try
            {
                if (!await _unitOfWork.UserGroupRepository.GetAll().AnyAsync(x => x.Id == userGroupId && !x.IsDeleted))
                {
                    return Result<UserRoleModel>.Fail(ConstantHelper.Message.GroupNotFound);
                }
                var rows = await _unitOfWork.UserRoleRepository.GetAll().AsNoTracking()
                    .Where(x => x.UserGroupId == userGroupId && !x.IsDeleted).ToListAsync();
                var flat = (await ActiveMenusAsync()).Select(m => ToViewModel(m, rows.FirstOrDefault(r => r.ObjectMenuId == m.Id))).ToList();
                return Result<UserRoleModel>.Success(new UserRoleModel { ListMenu = BuildTree(flat) }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMatrix: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<int>> SaveAccessAll(Guid userGroupId, bool allowAccess)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                if (!await _unitOfWork.UserGroupRepository.GetAll().AnyAsync(x => x.Id == userGroupId && !x.IsDeleted))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<int>.Fail(ConstantHelper.Message.GroupNotFound);
                }
                // T45R.8 (FE-1): gồm cả nhóm menu cấp 1 (sidebar cần AllowAccess của nhóm), không chỉ màn hình cấp 2.
                var menuIds = (await ActiveMenusAsync()).Where(m => m.LevelShow == 1 || m.LevelShow == 2).Select(m => m.Id).ToList();
                var existing = _unitOfWork.UserRoleRepository.GetAll()
                    .Where(x => x.UserGroupId == userGroupId && x.ObjectMenuId != null && menuIds.Contains(x.ObjectMenuId.Value) && !x.IsDeleted).ToList();
                int changed = 0;
                foreach (var menuId in menuIds)
                {
                    var row = existing.FirstOrDefault(x => x.ObjectMenuId == menuId);
                    if (row == null)
                    {
                        if (!allowAccess)
                        {
                            continue;
                        }
                        _unitOfWork.UserRoleRepository.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = userGroupId, ObjectMenuId = menuId, AllowAccess = true });
                        changed++;
                    }
                    else if (row.AllowAccess != allowAccess)
                    {
                        row.AllowAccess = allowAccess;
                        Normalize(row);
                        _unitOfWork.UserRoleRepository.Update(row);
                        changed++;
                    }
                }
                await _unitOfWork.UserRoleRepository.CommitAsync(allowAccess ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                _permissionService.InvalidateAll();
                return Result<int>.Success(changed, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveAccessAll: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<int>> CopyFromUserGroup(Guid sourceUserGroupId, Guid targetUserGroupId)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var groups = await _unitOfWork.UserGroupRepository.GetAll()
                    .Where(x => (x.Id == sourceUserGroupId || x.Id == targetUserGroupId) && !x.IsDeleted).Select(x => x.Id).ToListAsync();
                if (sourceUserGroupId == targetUserGroupId || !groups.Contains(sourceUserGroupId) || !groups.Contains(targetUserGroupId))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<int>.Fail(ConstantHelper.Message.GroupNotFound);
                }
                var source = _unitOfWork.UserRoleRepository.GetAll().Where(x => x.UserGroupId == sourceUserGroupId && !x.IsDeleted).ToList();
                var target = _unitOfWork.UserRoleRepository.GetAll().Where(x => x.UserGroupId == targetUserGroupId && !x.IsDeleted).ToList();
                int changed = 0;
                bool anyInsert = false;
                foreach (var row in target.Where(t => !source.Any(s => s.ObjectMenuId == t.ObjectMenuId)))
                {
                    // Menu nhóm nguồn không có → nhóm đích cũng không có quyền.
                    CopyFlags(new UserRole(), row);
                    _unitOfWork.UserRoleRepository.Update(row);
                    changed++;
                }
                foreach (var s in source)
                {
                    var row = target.FirstOrDefault(t => t.ObjectMenuId == s.ObjectMenuId);
                    if (row == null)
                    {
                        row = new UserRole { Id = Guid.NewGuid(), UserGroupId = targetUserGroupId, ObjectMenuId = s.ObjectMenuId };
                        _unitOfWork.UserRoleRepository.Add(row);
                        anyInsert = true;
                    }
                    else
                    {
                        _unitOfWork.UserRoleRepository.Update(row);
                    }
                    CopyFlags(s, row);
                    changed++;
                }
                await _unitOfWork.UserRoleRepository.CommitAsync(anyInsert ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                _permissionService.InvalidateAll();
                return Result<int>.Success(changed, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CopyFromUserGroup: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<UserGroupPreviewModel>> GetPreview(Guid userGroupId)
        {
            try
            {
                var group = await _unitOfWork.UserGroupRepository.GetSingleAsync(x => x.Id == userGroupId && !x.IsDeleted);
                if (group == null)
                {
                    return Result<UserGroupPreviewModel>.Fail(ConstantHelper.Message.GroupNotFound);
                }
                var rows = await _unitOfWork.UserRoleRepository.GetAll().AsNoTracking()
                    .Where(x => x.UserGroupId == userGroupId && x.AllowAccess && !x.IsDeleted).ToListAsync();
                var flat = (await ActiveMenusAsync()).Where(m => rows.Any(r => r.ObjectMenuId == m.Id))
                    .Select(m => ToViewModel(m, rows.First(r => r.ObjectMenuId == m.Id))).ToList();
                var userIds = await _unitOfWork.UserClaimRepository.GetAll()
                    .Where(x => x.UserGroupId == userGroupId && x.UserId != null && !x.IsDeleted).Select(x => x.UserId!.Value).Distinct().ToListAsync();
                var users = await _unitOfWork.UserRepository.GetAll().AsNoTracking().Where(x => userIds.Contains(x.Id) && !x.IsDeleted)
                    .OrderBy(x => x.UserName).Select(x => new UserGroupMemberModel { Id = x.Id, UserName = x.UserName }).ToListAsync();
                return Result<UserGroupPreviewModel>.Success(new UserGroupPreviewModel
                {
                    UserGroupId = group.Id,
                    UserGroupName = group.Name,
                    ScreenCount = flat.Count(x => x.LevelShow == 2),
                    ListMenu = BuildTree(flat),
                    ListUser = users,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPreview: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<UserRoleModel>> GetByUserId(Guid userId)
        {
            try
            {
                var user = await _unitOfWork.UserRepository.GetSingleAsync(x => x.Id == userId && !x.IsDeleted);
                if (user == null)
                {
                    return Result<UserRoleModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var menus = await ActiveMenusAsync();
                List<UserRoleViewModel> flat;
                if (user.IsAdmin)
                {
                    var all = new UserRole { AllowAccess = true, AllowAdd = true, AllowEdit = true, AllowDelete = true, AllowPrint = true, AllowExport = true, AllowImport = true };
                    flat = menus.Select(m => ToViewModel(m, all)).ToList();
                }
                else
                {
                    var groupIds = await (from c in _unitOfWork.UserClaimRepository.GetAll()
                                          join g in _unitOfWork.UserGroupRepository.GetAll() on c.UserGroupId equals g.Id
                                          where c.UserId == userId && !c.IsDeleted && !g.IsDeleted
                                          select g.Id).Distinct().ToListAsync();
                    var rows = await _unitOfWork.UserRoleRepository.GetAll().AsNoTracking()
                        .Where(x => x.UserGroupId != null && groupIds.Contains(x.UserGroupId.Value) && !x.IsDeleted).ToListAsync();
                    flat = [];
                    foreach (var m in menus)
                    {
                        var mine = rows.Where(r => r.ObjectMenuId == m.Id).ToList();
                        var merged = new UserRole
                        {
                            AllowAccess = mine.Any(r => r.AllowAccess), AllowAdd = mine.Any(r => r.AllowAdd), AllowEdit = mine.Any(r => r.AllowEdit),
                            AllowDelete = mine.Any(r => r.AllowDelete), AllowPrint = mine.Any(r => r.AllowPrint), AllowExport = mine.Any(r => r.AllowExport),
                            AllowImport = mine.Any(r => r.AllowImport),
                        };
                        if (merged.AllowAccess)
                        {
                            flat.Add(ToViewModel(m, merged));
                        }
                    }
                    // MENU-5.5 (D-79): nhóm menu cấp 1 (#/group/…) thường KHÔNG có dòng UserRole (seed chỉ cấp quyền màn) → mục con bị rơi thành gốc
                    // và giao diện không dựng được nhóm. Nhóm cha của mục được xem luôn đi kèm (chỉ để hiển thị – quyền vẫn tính trên mục con).
                    var shown = flat.Select(x => x.ObjectMenuId).ToHashSet();
                    foreach (var parent in menus.Where(m => !shown.Contains(m.Id) && flat.Any(x => x.ParentId == m.Id)).ToList())
                    {
                        flat.Add(ToViewModel(parent, new UserRole { AllowAccess = true }));
                    }
                    flat = [.. flat.OrderBy(x => x.LevelShow).ThenBy(x => x.OrderShow).ThenBy(x => x.ObjectMenuName)];
                }
                return Result<UserRoleModel>.Success(new UserRoleModel { ListMenu = BuildTree(flat) }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByUserId: ", ex.ToString()));
                throw;
            }
        }

        private Task<List<ObjectMenu>> ActiveMenusAsync()
            => _unitOfWork.ObjectMenuRepository.GetAll().AsNoTracking().Where(x => !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.LevelShow).ThenBy(x => x.OrderShow).ThenBy(x => x.Name).ToListAsync();

        /// <summary>Spec 3.3: bật cờ bất kỳ → bật AllowAccess; tắt AllowAccess (và không còn cờ nào) → tắt hết.</summary>
        private static void Normalize(UserRole r)
        {
            bool other = r.AllowAdd || r.AllowEdit || r.AllowDelete || r.AllowPrint || r.AllowExport || r.AllowImport;
            if (other && !r.AllowAccess)
            {
                r.AllowAccess = true;
            }
            if (!r.AllowAccess)
            {
                r.AllowAdd = r.AllowEdit = r.AllowDelete = r.AllowPrint = r.AllowExport = r.AllowImport = false;
            }
        }

        private static void CopyFlags(UserRole from, UserRole to)
        {
            to.AllowAccess = from.AllowAccess;
            to.AllowAdd = from.AllowAdd;
            to.AllowEdit = from.AllowEdit;
            to.AllowDelete = from.AllowDelete;
            to.AllowPrint = from.AllowPrint;
            to.AllowExport = from.AllowExport;
            to.AllowImport = from.AllowImport;
        }

        /// <summary>Id = UserRole.Id (Guid.Empty nếu menu chưa có dòng – gửi kèm UserGroupId + ObjectMenuId khi Update để tạo mới).</summary>
        private static UserRoleViewModel ToViewModel(ObjectMenu m, UserRole? r) => new()
        {
            Id = r?.Id ?? Guid.Empty,
            ObjectMenuId = m.Id,
            ObjectMenuName = m.Name,
            ShortName = m.ShortName,
            ParentId = m.ParentId,
            MetaLink = m.MetaLink,
            IconName = m.IconName,
            LevelShow = m.LevelShow,
            OrderShow = m.OrderShow,
            DataTarget = m.DataTarget,
            AriaControls = m.AriaControls,
            AllowAccess = r?.AllowAccess ?? false,
            AllowAdd = r?.AllowAdd ?? false,
            AllowEdit = r?.AllowEdit ?? false,
            AllowDelete = r?.AllowDelete ?? false,
            AllowPrint = r?.AllowPrint ?? false,
            AllowExport = r?.AllowExport ?? false,
            AllowImport = r?.AllowImport ?? false,
            Children = [],
        };

        /// <summary>Cây 2 cấp như <see cref="GetByUserGroupId"/>: menu cha (theo ObjectMenu.ParentId) → con; mục không có cha trong danh sách là gốc.</summary>
        private static List<UserRoleViewModel> BuildTree(List<UserRoleViewModel> flat)
        {
            var roots = new List<UserRoleViewModel>();
            foreach (var item in flat)
            {
                var parent = item.ParentId == null ? null : flat.FirstOrDefault(x => x.ObjectMenuId == item.ParentId && x != item);
                if (parent != null)
                {
                    (parent.Children ??= []).Add(item);
                }
                else
                {
                    roots.Add(item);
                }
            }
            return roots;
        }
    }
}
