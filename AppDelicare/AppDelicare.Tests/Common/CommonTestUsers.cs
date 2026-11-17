using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Common
{
    /// <summary>BE2.7: user thường thuộc 1 nhóm có quyền theo từng màn hình (V = Xem, A = Thêm, E = Sửa, D = Xóa, P = In, X = Xuất).</summary>
    public static class CommonTestUsers
    {
        public static async Task<Guid> UserAsync(InMemoryApiFactory factory, params (string Screen, string Rights)[] grants)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "u-" + userId.ToString("N")[..8], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "BE27-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (var (screen, rights) in grants)
                {
                    string link = "#/" + screen;
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole
                    {
                        Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id,
                        AllowAccess = rights.Contains('V'), AllowAdd = rights.Contains('A'), AllowEdit = rights.Contains('E'), AllowDelete = rights.Contains('D'),
                        AllowPrint = rights.Contains('P'), AllowExport = rights.Contains('X'),
                    });
                }
            });
            return userId;
        }
    }
}
