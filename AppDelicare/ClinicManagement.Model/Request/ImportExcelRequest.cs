using Microsoft.AspNetCore.Http;

namespace AppDelicare.Model.Request
{
    public class ImportExcelRequest
    {
        public string? TableName { get; set; }
        public IFormFile? File { get; set; }
    }
}
