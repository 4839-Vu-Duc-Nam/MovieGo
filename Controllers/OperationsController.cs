using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MovieGoApp.Data;
using MovieGoApp.Models;

namespace MovieGoApp.Controllers;

[Authorize(Roles = "Admin")]
public sealed class OperationsController(
    MovieGoStore store,
    ILogger<OperationsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Tickets(CancellationToken cancellationToken)
    {
        var tickets = await store.GetSoldTicketsAsync(cancellationToken);
        return View(tickets);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(string ticketId, string? employeeId, CancellationToken cancellationToken)
    {
        try
        {
            await store.ReturnTicketAsync(ticketId, employeeId, cancellationToken);
            TempData["Success"] = $"Đã trả vé {ticketId}. Phí xử lý và số tiền hoàn được ghi vào lịch sử.";
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Could not return MovieGo ticket {TicketId}", ticketId);
            TempData["Error"] = BusinessError(ex.Number,
                "Không thể trả vé. Vé phải đã thanh toán và còn ít nhất 2 giờ trước giờ chiếu.");
        }

        return RedirectToAction(nameof(Tickets));
    }

    [HttpGet]
    public async Task<IActionResult> Exchange(string id, CancellationToken cancellationToken)
    {
        var page = await store.GetExchangePageAsync(id, cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Exchange(ExchangeTicketForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return await InvalidExchangeAsync(form, cancellationToken);

        try
        {
            await store.ExchangeTicketAsync(form, cancellationToken);
            TempData["Success"] = $"Đã đổi vé {form.OriginalTicketId}. Vé mới đã được ghi vào cùng hóa đơn.";
            return RedirectToAction(nameof(Tickets));
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Could not exchange MovieGo ticket {TicketId}", form.OriginalTicketId);
            ModelState.AddModelError(string.Empty, BusinessError(ex.Number,
                "Không thể đổi vé. Hãy kiểm tra suất chiếu, ghế trống và hình thức thanh toán."));
            return await InvalidExchangeAsync(form, cancellationToken);
        }
    }

    [HttpGet]
    public async Task<IActionResult> AvailableSeats(string showtimeId, CancellationToken cancellationToken)
    {
        var seats = await store.GetAvailableSeatsAsync(showtimeId, cancellationToken);
        return Json(seats.Select(seat => new
        {
            seat.Id,
            Label = $"{seat.Row}{seat.Number}",
            seat.Row,
            seat.Number,
            seat.Type,
            seat.Price,
            seat.IsAvailable
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Reports(
        DateTime? from, DateTime? to, string groupBy = "date", CancellationToken cancellationToken = default)
    {
        var start = from?.Date ?? DateTime.Today;
        var end = to?.Date ?? DateTime.Today;
        try
        {
            var report = await store.GetRevenueReportAsync(start, end, groupBy, cancellationToken);
            return View(report);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var report = await store.GetRevenueReportAsync(start, start, "date", cancellationToken);
            return View(report);
        }
    }

    private async Task<IActionResult> InvalidExchangeAsync(
        ExchangeTicketForm form, CancellationToken cancellationToken)
    {
        var page = await store.GetExchangePageAsync(form.OriginalTicketId, cancellationToken);
        if (page is null)
            return NotFound();

        ViewData["SelectedShowtime"] = form.NewShowtimeId;
        ViewData["SelectedSeat"] = form.NewSeatId;
        ViewData["SelectedPaymentMethod"] = form.DifferencePaymentMethodId;
        ViewData["SelectedEmployee"] = form.EmployeeId;
        return View("Exchange", page);
    }

    private static string BusinessError(int number, string fallback) => number switch
    {
        51005 => "Vé này không còn đủ điều kiện xử lý.",
        51020 => "Vé gốc không còn ở trạng thái đã bán hoặc hóa đơn chưa thanh toán.",
        51021 => "Vé đã được xử lý trước đó hoặc là vé phát sinh từ lần đổi khác.",
        51022 => "Chỉ được đổi vé trước giờ chiếu ít nhất 2 giờ.",
        51023 or 51024 => "Suất chiếu mới không hợp lệ hoặc đã bắt đầu.",
        51025 or 51026 => "Ghế mới không khả dụng cho suất chiếu đã chọn.",
        51027 or 51028 => "Hình thức thanh toán chênh lệch chưa hợp lệ.",
        51035 => "Ghế đôi chỉ được trả theo cả cặp; thao tác trả một vé riêng không được phép.",
        _ => fallback
    };
}
