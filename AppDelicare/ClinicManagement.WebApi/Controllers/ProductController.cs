using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers
{
    public class ProductController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
