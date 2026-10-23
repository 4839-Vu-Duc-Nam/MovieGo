using Microsoft.AspNetCore.Mvc;
using MovieGoApp.Data;

namespace MovieGoApp.Controllers;

public sealed class MoviesController(MovieGoStore store) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var movies = await store.GetMoviesAsync(cancellationToken);
        return View(movies);
    }
}
