using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NamClient.Web.Services;

namespace NamClient.Web.Controllers;

public class MaintenanceController : Controller
{
    private readonly IDataMaintenanceService _maintenance;
    private readonly IWebHostEnvironment _environment;

    public MaintenanceController(IDataMaintenanceService maintenance, IWebHostEnvironment environment)
    {
        _maintenance = maintenance;
        _environment = environment;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!_environment.IsDevelopment())
        {
            context.Result = NotFound();
        }
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAll() =>
        Json(new { message = await _maintenance.DeleteAllAsync(CancellationToken.None) });
}
