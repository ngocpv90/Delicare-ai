using AppDelicare.Model.ViewModels;

namespace AppDelicare.Model.Response
{
    public sealed class AuthenticateResponse : AuthenticateViewModel
    {
        public string? UserId { get; set; }
        public string? Token { get; set; }
        public int ExpireTime { get; set; }
    }
}
