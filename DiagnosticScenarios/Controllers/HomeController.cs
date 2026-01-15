using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiagnosticScenarios.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ToggleScenarios()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
