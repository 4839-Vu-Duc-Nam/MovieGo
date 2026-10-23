using Microsoft.AspNetCore.Mvc;
using MovieGoApp.Data;

namespace MovieGoApp.Controllers;

public sealed class ShowtimesController(MovieGoStore store) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var showtimes = await store.GetShowtimesAsync(100, cancellationToken);
        return View(showtimes);
    }
}
