using AppDelicare.Data.Interface;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Data.Share
{
    /// <summary>
    /// Giữ để tương thích code cũ. T02: KHÔNG còn fallback "user đầu tiên trong bảng User";
    /// không có người thao tác thì ném <see cref="UnauthorizedActorException"/>.
    /// </summary>
    public class AuthenticatedService(ICurrentActor currentActor, IDapperRepository dapperRepository) : IAuthenticatedService
    {
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly ICurrentActor _currentActor = currentActor;

        public Guid Id => _currentActor.RequireActor().UserId;

        public UserInfo UserInfo => _dapperRepository.QueryMultipleWithParam<UserInfo>(
              "SELECT TOP 1 u.Id, u.UserName, u.PersonId FROM [User] AS u"
            + " WHERE u.Id = @Id AND u.IsActive = 1 AND u.IsDeleted = 0",
              new { Id }).FirstOrDefault() ?? new();
    }
}
