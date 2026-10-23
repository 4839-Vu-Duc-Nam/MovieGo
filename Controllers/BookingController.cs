using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MovieGoApp.Data;
using MovieGoApp.Models;
using System.Security.Claims;

namespace MovieGoApp.Controllers;

[Authorize(Roles = "Customer,Admin")]
public sealed class BookingController(
    MovieGoStore store,
    ILogger<BookingController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(string id, CancellationToken cancellationToken)
    {
        var model = await store.GetBookingPageAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return await InvalidBookingAsync(form, cancellationToken);

        try
        {
            var invoiceId = await store.CreateBookingAsync(
                form, User.FindFirstValue(AuthenticationStore.CustomerIdClaim), cancellationToken);
            return RedirectToAction(nameof(Invoice), new { id = invoiceId });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await InvalidBookingAsync(form, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await InvalidBookingAsync(form, cancellationToken);
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Could not create MovieGo booking for showtime {ShowtimeId}", form.ShowtimeId);
            ModelState.AddModelError(string.Empty,
                "Không thể giữ ghế. Có thể ghế vừa được người khác đặt; vui lòng tải lại và thử lại.");
            return await InvalidBookingAsync(form, cancellationToken);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Invoice(string id, CancellationToken cancellationToken)
    {
        var invoice = await store.GetInvoiceAsync(
            id, User.FindFirstValue(AuthenticationStore.CustomerIdClaim), cancellationToken);
        return invoice is null ? NotFound() : View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string id, CancellationToken cancellationToken)
    {
        try
        {
            await store.PayInvoiceAsync(
                id, User.FindFirstValue(AuthenticationStore.CustomerIdClaim), cancellationToken);
            TempData["Success"] = "Thanh toán mô phỏng thành công. Không có cổng thanh toán thật được tích hợp.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Could not complete payment for MovieGo invoice {InvoiceId}", id);
            TempData["Error"] = "Không thể thanh toán hóa đơn. Hãy kiểm tra trạng thái hóa đơn và thời hạn giữ ghế.";
        }

        return RedirectToAction(nameof(Invoice), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string id, CancellationToken cancellationToken)
    {
        try
        {
            await store.CancelPendingInvoiceAsync(
                id, User.FindFirstValue(AuthenticationStore.CustomerIdClaim), cancellationToken);
            TempData["Success"] = "Đã hủy hóa đơn và giải phóng các ghế đang giữ.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Could not cancel MovieGo invoice {InvoiceId}", id);
            TempData["Error"] = "Không thể hủy hóa đơn. Hóa đơn có thể đã thanh toán hoặc đã hết hạn.";
        }

        return RedirectToAction(nameof(Invoice), new { id });
    }

    private async Task<IActionResult> InvalidBookingAsync(
        BookingForm form, CancellationToken cancellationToken)
    {
        var page = await store.GetBookingPageAsync(form.ShowtimeId, cancellationToken);
        if (page is null)
            return NotFound();
        ViewData["SelectedSeats"] = form.SeatIds;
        ViewData["SelectedPaymentMethod"] = form.PaymentMethodId;
        ViewData["AcceptSeparatedSeats"] = form.AcceptSeparatedSeats;
        return View("Create", page);
    }
}
