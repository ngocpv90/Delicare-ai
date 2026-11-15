using AppDelicare.Model.ViewModels;

namespace AppDelicare.Data.Share
{
    public interface IAuthenticatedService
    {
        public Guid Id { get; }
        public UserInfo UserInfo { get; }
    }
}
