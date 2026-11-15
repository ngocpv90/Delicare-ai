using Microsoft.AspNetCore.Http;

namespace AppDelicare.Model.Request.Custom
{
    public class TestDemoCustomRequest : TestDemoRequest
    {
        public IFormFile? File { get; set; }
        public List<IFormFile>? Files { get; set; }
    }
}
