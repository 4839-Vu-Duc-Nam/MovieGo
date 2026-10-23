using System.ComponentModel.DataAnnotations;

namespace MovieGoApp.Models;

public sealed record DashboardModel(
    int MovieCount,
    int TodayShowtimeCount,
    int SoldTicketCount,
    decimal TodayRevenue,
    IReadOnlyList<ShowtimeRow> UpcomingShowtimes);

public sealed record MovieRow(
    string Id,
    string Title,
    int DurationMinutes,
    string Language,
    string AgeRating,
    DateTime? ReleaseDate,
    string Status,
    string Genres);

public sealed record ShowtimeRow(
    string Id,
    string MovieTitle,
    string RoomName,
    DateTime StartsAt,
    DateTime EndsAt,
    decimal Price,
    string Status,
    int AvailableSeats);

public sealed record SeatOption(
    string Id,
    string Row,
    int Number,
    string Type,
    bool IsAvailable,
    decimal Price,
    bool IsRestricted = false);

public sealed class BookingForm
{
    [Required]
    public string ShowtimeId { get; set; } = "";

    [Required]
    public string PaymentMethodId { get; set; } = "";

    public bool AcceptSeparatedSeats { get; set; }

    [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một ghế.")]
    public List<string> SeatIds { get; set; } = [];
}

public sealed class LoginForm
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class RegisterForm
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, Compare(nameof(Password)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed record CustomerInvoiceRow(
    string Id,
    DateTime CreatedAt,
    string Status,
    string MovieTitle,
    DateTime? Showtime,
    decimal Total);

public sealed record LookupOption(string Id, string Name);

public sealed record BookingPageModel(
    ShowtimeRow Showtime,
    IReadOnlyList<SeatOption> Seats,
    IReadOnlyList<LookupOption> PaymentMethods);

public sealed record InvoiceModel(
    string Id,
    DateTime CreatedAt,
    string Status,
    string PaymentMethod,
    string Channel,
    decimal Total,
    DateTime? HoldExpiresAt,
    IReadOnlyList<InvoiceTicket> Tickets);

public sealed record InvoiceTicket(
    string Id,
    string MovieTitle,
    string RoomName,
    DateTime StartsAt,
    string Seat,
    decimal Price,
    string Status);

public sealed record TicketOperationRow(
    string TicketId,
    string InvoiceId,
    string MovieTitle,
    string ShowtimeId,
    DateTime StartsAt,
    string RoomName,
    string Seat,
    decimal Price,
    string Status,
    bool IsCoupleSeat);

public sealed record ExchangePageModel(
    TicketOperationRow Ticket,
    IReadOnlyList<ShowtimeRow> Showtimes,
    IReadOnlyList<LookupOption> PaymentMethods,
    IReadOnlyList<LookupOption> Employees);

public sealed class ExchangeTicketForm
{
    [Required]
    public string OriginalTicketId { get; set; } = "";

    [Required]
    public string NewShowtimeId { get; set; } = "";

    [Required]
    public string NewSeatId { get; set; } = "";

    public string? DifferencePaymentMethodId { get; set; }

    public string? EmployeeId { get; set; }
}

public sealed class ReportFilter
{
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    [Required]
    public string GroupBy { get; set; } = "date";
}

public sealed record ReportRow(
    string Label,
    decimal TicketRevenue,
    decimal ProcessingFees,
    decimal NetRevenue,
    int TicketCount);

public sealed record ReportPageModel(
    DateTime From,
    DateTime To,
    string GroupBy,
    string GroupLabel,
    IReadOnlyList<ReportRow> Rows)
{
    public decimal TotalTicketRevenue => Rows.Sum(row => row.TicketRevenue);
    public decimal TotalProcessingFees => Rows.Sum(row => row.ProcessingFees);
    public decimal TotalNetRevenue => Rows.Sum(row => row.NetRevenue);
    public int TotalTickets => Rows.Sum(row => row.TicketCount);
}

public sealed record AdminPageModel(
    IReadOnlyList<MovieRow> Movies,
    IReadOnlyList<LookupOption> Genres,
    IReadOnlyList<RoomRow> Rooms,
    IReadOnlyList<SeatRow> Seats,
    IReadOnlyList<ShowtimeAdminRow> Showtimes,
    IReadOnlyList<CustomerRow> Customers,
    IReadOnlyList<EmployeeRow> Employees);

public sealed record RoomRow(string Id, string Name, string Type, string Status, int SeatCount);
public sealed record SeatRow(string Id, string RoomName, string Row, int Number, string Type, string Status);
public sealed record ShowtimeAdminRow(string Id, string MovieTitle, string RoomName, DateTime StartsAt, DateTime EndsAt, decimal Price, decimal VipPrice, decimal CouplePrice, string Status, bool BlockFrontAndCorners);
public sealed record CustomerRow(string Id, string Name, string? Phone, string? Email, DateTime? BirthDate);
public sealed record EmployeeRow(string Id, string Name, string? Phone, string? Email, string Role, string Status);

public sealed class MovieEditForm
{
    [StringLength(10)] public string Id { get; set; } = "";
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Range(1, 500)] public int DurationMinutes { get; set; }
    [Required, StringLength(50)] public string Language { get; set; } = "";
    [Required, StringLength(10)] public string AgeRating { get; set; } = "P";
    [DataType(DataType.Date)] public DateTime? ReleaseDate { get; set; }
    [Required] public string Status { get; set; } = "Sắp chiếu";
    public List<string> GenreIds { get; set; } = [];
}

public sealed class GenreEditForm
{
    [StringLength(10)] public string Id { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
}

public sealed class RoomEditForm
{
    [StringLength(10)] public string Id { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(50)] public string Type { get; set; } = "2D";
    [Required] public string Status { get; set; } = "Hoạt động";
}

public sealed class SeatEditForm
{
    [StringLength(10)] public string Id { get; set; } = "";
    [Required, StringLength(10)] public string RoomId { get; set; } = "";
    [Required, StringLength(5)] public string Row { get; set; } = "";
    [Range(1, 500)] public int Number { get; set; }
    [Required, StringLength(30)] public string Type { get; set; } = "Thường";
    [Required] public string Status { get; set; } = "Hoạt động";
}

public sealed class BulkSeatGenerationForm
{
    [Required, StringLength(10)] public string RoomId { get; set; } = "";
    [Required, StringLength(100)] public string Rows { get; set; } = "A,B,C,D,E,F";
    [Range(1, 500)] public int FirstNumber { get; set; } = 1;
    [Range(1, 100)] public int SeatsPerRow { get; set; } = 10;
    [Required, StringLength(30)] public string Type { get; set; } = "Thường";
}

public sealed class ShowtimeEditForm
{
    [StringLength(10)] public string Id { get; set; } = "";
    [Required, StringLength(10)] public string MovieId { get; set; } = "";
    [Required, StringLength(10)] public string RoomId { get; set; } = "";
    [Required] public DateTime StartsAt { get; set; }
    [Required] public DateTime EndsAt { get; set; }
    [Range(typeof(decimal), "0", "9999999999")] public decimal Price { get; set; }
    [Range(typeof(decimal), "0", "9999999999")] public decimal VipPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999")] public decimal CouplePrice { get; set; }
    public bool BlockFrontAndCorners { get; set; }
    [Required] public string Status { get; set; } = "Sắp chiếu";
}

public sealed class CustomerEditForm
{
    [StringLength(10)] public string Id { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Phone, StringLength(20)] public string? Phone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    [DataType(DataType.Date)] public DateTime? BirthDate { get; set; }
}

public sealed class EmployeeEditForm
{
    [StringLength(10)] public string Id { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Phone, StringLength(20)] public string? Phone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    [Required, StringLength(50)] public string Role { get; set; } = "Thu ngân";
    [Required] public string Status { get; set; } = "Đang làm";
}

public sealed class CounterSaleForm
{
    [Required] public string ShowtimeId { get; set; } = "";
    [Required] public string PaymentMethodId { get; set; } = "";
    [Required] public string EmployeeId { get; set; } = "";
    public string? CustomerId { get; set; }
    public bool AcceptSeparatedSeats { get; set; }
    [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một ghế.")]
    public List<string> SeatIds { get; set; } = [];
}

public sealed record CounterSalePageModel(
    IReadOnlyList<ShowtimeRow> Showtimes,
    IReadOnlyList<LookupOption> PaymentMethods,
    IReadOnlyList<LookupOption> Customers,
    IReadOnlyList<LookupOption> Employees);
