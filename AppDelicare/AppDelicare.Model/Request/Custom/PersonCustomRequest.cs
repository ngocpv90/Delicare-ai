using Microsoft.AspNetCore.Http;

namespace AppDelicare.Model.Request.Custom
{
    public class PersonCustomRequest : PersonRequest
    {
        public IFormFile? File { get; set; }
    }
}
