using AppDelicare.Model.Request;
using AppDelicare.Model.Response;

namespace AppDelicare.Services.Interface
{
    public interface ILoginService
    {
        public Task<Response<AuthenticateResponse>> Authenticate(LoginRequest request);
    }
}
