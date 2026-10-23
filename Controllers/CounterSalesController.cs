using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MovieGoApp.Data;
using MovieGoApp.Models;

namespace MovieGoApp.Controllers;

[Authorize(Roles = "Admin")]
public sealed class CounterSalesController(
    AdminStore store,
    ILogger<CounterSalesController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await store.GetCounterSalePageAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> AvailableSeats(string showtimeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(showtimeId))
            return BadRequest("Cần chọn suất chiếu.");
        var seats = await store.GetAvailableSeatsAsync(showtimeId, cancellationToken);
        return Json(seats.Select(seat => new
        {
            seat.Id,
            Label = $"{seat.Row}{seat.Number}",
            seat.Row,
            seat.Number,
            seat.Type,
            seat.Price,
            seat.IsAvailable,
            seat.IsRestricted
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CounterSaleForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn suất chiếu, nhân viên, hình thức thanh toán và ít nhất một ghế.");
            return await InvalidFormAsync(form, cancellationToken);
        }

        try
        {
            var invoiceId = await store.CreateCounterSaleAsync(form, cancellationToken);
            TempData["Success"] = $"Đã bán vé tại quầy. Mã hóa đơn: {invoiceId}.";
            return RedirectToAction(nameof(Index));
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Could not create MovieGo counter sale for showtime {ShowtimeId}", form.ShowtimeId);
            ModelState.AddModelError(string.Empty, "Không thể hoàn tất bán vé. Suất chiếu, ghế hoặc nhân viên có thể vừa thay đổi; hãy tải lại và thử lại.");
            return await InvalidFormAsync(form, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await InvalidFormAsync(form, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await InvalidFormAsync(form, cancellationToken);
        }
    }

    private async Task<IActionResult> InvalidFormAsync(CounterSaleForm form, CancellationToken cancellationToken)
    {
        ViewData["SelectedShowtime"] = form.ShowtimeId;
        ViewData["SelectedCustomer"] = form.CustomerId;
        ViewData["SelectedEmployee"] = form.EmployeeId;
        ViewData["SelectedPaymentMethod"] = form.PaymentMethodId;
        ViewData["SelectedSeats"] = form.SeatIds;
        ViewData["AcceptSeparatedSeats"] = form.AcceptSeparatedSeats;
        return View(await store.GetCounterSalePageAsync(cancellationToken));
    }
}
