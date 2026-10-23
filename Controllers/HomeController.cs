using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MovieGoApp.Data;
using MovieGoApp.Models;

namespace MovieGoApp.Controllers;

public class HomeController(MovieGoStore store) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await store.GetDashboardAsync(cancellationToken);
        return View(dashboard);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
