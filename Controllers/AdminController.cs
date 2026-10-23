using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MovieGoApp.Data;
using MovieGoApp.Models;

namespace MovieGoApp.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminController(
    AdminStore store,
    ILogger<AdminController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await store.GetPageAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Movie(MovieEditForm form, CancellationToken ct) =>
        SaveAsync(() => store.SaveMovieAsync(form, ct), "phim", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Genre(GenreEditForm form, CancellationToken ct) =>
        SaveAsync(() => store.SaveGenreAsync(form, ct), "thể loại", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Room(RoomEditForm form, CancellationToken ct) =>
        SaveAsync(() => store.SaveRoomAsync(form, ct), "phòng chiếu", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Seat(SeatEditForm form, CancellationToken ct) =>
        SaveAsync(() => store.SaveSeatAsync(form, ct), "ghế", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateSeats(BulkSeatGenerationForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin tạo ghế hàng loạt không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var created = await store.GenerateSeatsAsync(form, cancellationToken);
            TempData["Success"] = created > 0
                ? $"Đã tạo {created} ghế mới. Các vị trí đã có sẵn được giữ nguyên."
                : "Các vị trí được chọn đã tồn tại, không có ghế mới được tạo.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Could not generate seats for MovieGo room {RoomId}", form.RoomId);
            TempData["Error"] = "Không thể tạo ghế. Kiểm tra mã phòng và các ràng buộc vị trí.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Showtime(ShowtimeEditForm form, CancellationToken ct) =>
        SaveAsync(() => store.SaveShowtimeAsync(form, ct), "suất chiếu", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Customer(CustomerEditForm form, CancellationToken ct) =>
        SaveAsync(() => store.SaveCustomerAsync(form, ct), "khách hàng", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Employee(EmployeeEditForm form, CancellationToken ct) =>
        SaveAsync(() => store.SaveEmployeeAsync(form, ct), "nhân viên", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelShowtime(
        string showtimeId, string employeeId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(showtimeId)
            || string.IsNullOrWhiteSpace(employeeId)
            || string.IsNullOrWhiteSpace(reason)
            || reason.Trim().Length > 300)
        {
            TempData["Error"] = "Cần chọn suất chiếu, nhân viên và nhập lý do hủy tối đa 300 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await store.CancelShowtimeAsync(showtimeId, employeeId, reason, cancellationToken);
            TempData["Success"] = $"Đã hủy suất chiếu {showtimeId}. Các vé đang giữ/bán được xử lý và ghi lịch sử theo quy định.";
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Could not cancel MovieGo showtime {ShowtimeId}", showtimeId);
            TempData["Error"] = ex.Number == 51013
                ? "Suất chiếu không tồn tại hoặc đã bị hủy."
                : "Không thể hủy suất chiếu. Vui lòng kiểm tra nhân viên và trạng thái suất chiếu.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(
        Func<Task> save, string entity, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error =>
                    $"{FieldLabel(entry.Key)}: {(string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Giá trị không hợp lệ." : error.ErrorMessage)}"))
                .Take(5)
                .ToArray();
            TempData["Error"] = $"Dữ liệu {entity} không hợp lệ. {string.Join(" ", errors)}";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await save();
            TempData["Success"] = $"Đã lưu {entity}.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Could not save MovieGo {Entity}", entity);
            TempData["Error"] = $"Không thể lưu {entity}. Có thể mã đã tồn tại, dữ liệu đang được sử dụng hoặc vi phạm quy tắc nghiệp vụ.";
        }
        return RedirectToAction(nameof(Index));
    }

    private static string FieldLabel(string field) => field switch
    {
        nameof(MovieEditForm.Id) => "Mã phim",
        nameof(MovieEditForm.Title) => "Tên phim",
        nameof(MovieEditForm.DurationMinutes) => "Thời lượng",
        nameof(MovieEditForm.Language) => "Ngôn ngữ",
        nameof(MovieEditForm.AgeRating) => "Giới hạn tuổi",
        nameof(MovieEditForm.ReleaseDate) => "Ngày khởi chiếu",
        nameof(MovieEditForm.Status) => "Trạng thái",
        nameof(MovieEditForm.GenreIds) => "Thể loại",
        _ => field
    };
}
