using Microsoft.Data.SqlClient;
using MovieGoApp.Models;
using System.Data;

namespace MovieGoApp.Data;

public sealed class MovieGoStore(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("MovieGo")
        ?? throw new InvalidOperationException("Connection string 'MovieGo' is missing.");

    public async Task<DashboardModel> GetDashboardAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM dbo.PHIM WHERE TrangThai <> N'Ngừng chiếu') AS MovieCount,
                (SELECT COUNT(*) FROM dbo.SUATCHIEU
                 WHERE CAST(ThoiGianBatDau AS date) = CAST(SYSDATETIME() AS date)
                   AND TrangThai <> N'Đã hủy') AS TodayShowtimeCount,
                (SELECT COUNT(*) FROM dbo.VE
                 WHERE TrangThai = N'Đã bán'
                   AND CAST(ThoiGianBan AS date) = CAST(SYSDATETIME() AS date)) AS SoldTicketCount,
                (SELECT COALESCE(SUM(GiaBan), 0) FROM dbo.VE
                 WHERE TrangThai = N'Đã bán'
                   AND CAST(ThoiGianBan AS date) = CAST(SYSDATETIME() AS date))
                    + (SELECT COALESCE(SUM(PhiXuLy), 0) FROM dbo.XULYVE
                       WHERE CAST(ThoiGianXL AS date) = CAST(SYSDATETIME() AS date)) AS TodayRevenue;
            """;

        int movies;
        int showtimes;
        int sold;
        decimal revenue;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            movies = reader.GetInt32(0);
            showtimes = reader.GetInt32(1);
            sold = reader.GetInt32(2);
            revenue = reader.GetDecimal(3);
        }

        var upcoming = await GetShowtimesAsync(10, cancellationToken);
        return new DashboardModel(movies, showtimes, sold, revenue, upcoming);
    }

    public async Task<IReadOnlyList<MovieRow>> GetMoviesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.MaPhim, p.TenPhim, p.ThoiLuong, p.NgonNgu, p.GioiHanTuoi,
                   p.NgayKhoiChieu, p.TrangThai,
                   COALESCE(STRING_AGG(t.TenTheLoai, N', '), N'Chưa phân loại') AS TheLoai
            FROM dbo.PHIM AS p
            LEFT JOIN dbo.PHIM_THELOAI AS pt ON pt.MaPhim = p.MaPhim
            LEFT JOIN dbo.THELOAI AS t ON t.MaTheLoai = pt.MaTheLoai
            GROUP BY p.MaPhim, p.TenPhim, p.ThoiLuong, p.NgonNgu, p.GioiHanTuoi,
                     p.NgayKhoiChieu, p.TrangThai
            ORDER BY p.TenPhim;
            """;

        var movies = new List<MovieRow>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            movies.Add(new MovieRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt16(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                reader.GetString(6),
                reader.GetString(7)));
        }
        return movies;
    }

    public async Task<IReadOnlyList<ShowtimeRow>> GetShowtimesAsync(int limit, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@Limit) sc.MaSuatChieu, p.TenPhim, pc.TenPhong,
                   sc.ThoiGianBatDau, sc.ThoiGianKetThuc, sc.GiaVeCoBan, sc.TrangThai,
                   (SELECT COUNT(*)
                    FROM dbo.GHE AS g
                    WHERE g.MaPhong = sc.MaPhong
                      AND g.TrangThai = N'Hoạt động'
                      AND NOT
                      (
                          sc.KhoaGheBienVaHangDau=1
                          AND (g.HangGhe IN ('A','B')
                               OR g.SoGhe <= (SELECT MIN(SoGhe)+1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)
                               OR g.SoGhe >= (SELECT MAX(SoGhe)-1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe))
                      )
                      AND (g.LoaiGhe<>N'Đôi' OR EXISTS
                           (
                               SELECT 1 FROM dbo.GHE pair
                               WHERE pair.MaPhong=g.MaPhong AND pair.HangGhe=g.HangGhe
                                 AND pair.SoGhe=CASE WHEN g.SoGhe%2=1 THEN g.SoGhe+1 ELSE g.SoGhe-1 END
                                 AND pair.TrangThai=N'Hoạt động'
                                 AND NOT EXISTS
                                 (
                                     SELECT 1 FROM dbo.VE pairTicket
                                     WHERE pairTicket.MaSuatChieu=sc.MaSuatChieu
                                       AND pairTicket.MaGhe=pair.MaGhe
                                       AND pairTicket.TrangThai IN (N'Đang giữ chỗ',N'Đã bán')
                                 )
                           ))
                      AND NOT EXISTS
                      (
                          SELECT 1 FROM dbo.VE AS v
                          WHERE v.MaSuatChieu = sc.MaSuatChieu
                            AND v.MaGhe = g.MaGhe
                            AND v.TrangThai IN (N'Đang giữ chỗ', N'Đã bán')
                      )) AS AvailableSeats
            FROM dbo.SUATCHIEU AS sc
            JOIN dbo.PHIM AS p ON p.MaPhim = sc.MaPhim
            JOIN dbo.PHONGCHIEU AS pc ON pc.MaPhong = sc.MaPhong
            WHERE sc.ThoiGianBatDau >= SYSDATETIME()
              AND sc.TrangThai = N'Sắp chiếu'
            ORDER BY sc.ThoiGianBatDau;
            """;

        var rows = new List<ShowtimeRow>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Limit", SqlDbType.Int).Value = Math.Clamp(limit, 1, 100);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadShowtime(reader));
        return rows;
    }

    public async Task<BookingPageModel?> GetBookingPageAsync(string showtimeId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var showtime = await GetShowtimeAsync(connection, showtimeId, cancellationToken);
        if (showtime is null)
            return null;

        const string seatSql = """
            SELECT g.MaGhe, g.HangGhe, g.SoGhe, g.LoaiGhe,
                   CASE WHEN EXISTS
                   (
                       SELECT 1 FROM dbo.VE AS v
                       WHERE v.MaSuatChieu = @ShowtimeId
                         AND v.MaGhe = g.MaGhe
                         AND v.TrangThai IN (N'Đang giữ chỗ', N'Đã bán')
                   ) OR g.TrangThai <> N'Hoạt động'
                     OR (sc.KhoaGheBienVaHangDau=1
                         AND (g.HangGhe IN ('A','B')
                              OR g.SoGhe <= (SELECT MIN(SoGhe)+1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)
                              OR g.SoGhe >= (SELECT MAX(SoGhe)-1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)))
                     OR (g.LoaiGhe=N'Đôi' AND NOT EXISTS
                         (
                             SELECT 1 FROM dbo.GHE pair
                             WHERE pair.MaPhong=g.MaPhong AND pair.HangGhe=g.HangGhe
                               AND pair.SoGhe=CASE WHEN g.SoGhe%2=1 THEN g.SoGhe+1 ELSE g.SoGhe-1 END
                               AND pair.TrangThai=N'Hoạt động'
                               AND NOT EXISTS (SELECT 1 FROM dbo.VE pairTicket
                                               WHERE pairTicket.MaSuatChieu=@ShowtimeId
                                                 AND pairTicket.MaGhe=pair.MaGhe
                                                 AND pairTicket.TrangThai IN (N'Đang giữ chỗ',N'Đã bán'))
                         ))
                   THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS IsAvailable,
                   COALESCE(gh.GiaVe, sc.GiaVeCoBan) AS SeatPrice,
                   CAST(CASE WHEN sc.KhoaGheBienVaHangDau=1
                              AND (g.HangGhe IN ('A','B')
                                   OR g.SoGhe <= (SELECT MIN(SoGhe)+1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)
                                   OR g.SoGhe >= (SELECT MAX(SoGhe)-1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe))
                             THEN 1 ELSE 0 END AS bit) AS IsRestricted
            FROM dbo.GHE AS g
            JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu=@ShowtimeId AND sc.MaPhong=g.MaPhong
            LEFT JOIN dbo.GIAVE_SUAT_HANG AS gh
              ON gh.MaSuatChieu=sc.MaSuatChieu AND gh.LoaiGhe=g.LoaiGhe
            ORDER BY g.HangGhe, g.SoGhe;
            """;

        var seats = new List<SeatOption>();
        await using (var command = new SqlCommand(seatSql, connection))
        {
            command.Parameters.Add("@ShowtimeId", SqlDbType.VarChar, 10).Value = showtimeId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                seats.Add(new SeatOption(reader.GetString(0), reader.GetString(1),
                    reader.GetInt16(2), reader.GetString(3), reader.GetBoolean(4), reader.GetDecimal(5),
                    reader.GetBoolean(6)));
        }

        var methods = await ReadOptionsAsync(connection, """
            SELECT p.MaPTTT, p.TenPTTT
            FROM dbo.PHUONGTHUCTT AS p
            JOIN dbo.KENH_PTTT AS kp ON kp.MaPTTT = p.MaPTTT
            WHERE kp.MaKenh = 'KB002' AND p.TrangThai = N'Đang hoạt động'
            ORDER BY p.TenPTTT;
            """, cancellationToken);
        return new BookingPageModel(showtime, seats, methods);
    }

    public async Task<string> CreateBookingAsync(
        BookingForm form, string? customerId, CancellationToken cancellationToken)
    {
        var seatIds = form.SeatIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (seatIds.Length == 0)
            throw new ArgumentException("Vui lòng chọn ít nhất một ghế.");
        if (seatIds.Length > 8)
            throw new ArgumentException("Mỗi giao dịch được chọn tối đa 8 ghế.");

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        try
        {
            const string showtimeSql = """
                SELECT MaPhong
                FROM dbo.SUATCHIEU WITH (UPDLOCK, HOLDLOCK)
                WHERE MaSuatChieu = @ShowtimeId
                  AND TrangThai = N'Sắp chiếu'
                  AND ThoiGianBatDau > SYSDATETIME();
                """;
            string? roomId = null;
            await using (var command = new SqlCommand(showtimeSql, connection, transaction))
            {
                command.Parameters.Add("@ShowtimeId", SqlDbType.VarChar, 10).Value = form.ShowtimeId;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                    roomId = reader.GetString(0);
            }
            if (roomId is null)
                throw new InvalidOperationException("Suất chiếu không tồn tại, đã bắt đầu hoặc đã ngừng bán.");

            const string paymentSql = """
                SELECT COUNT(*)
                FROM dbo.KENH_PTTT AS kp
                JOIN dbo.PHUONGTHUCTT AS p ON p.MaPTTT = kp.MaPTTT
                WHERE kp.MaKenh = 'KB002' AND kp.MaPTTT = @PaymentMethodId
                  AND p.TrangThai = N'Đang hoạt động';
                """;
            await using (var command = new SqlCommand(paymentSql, connection, transaction))
            {
                command.Parameters.Add("@PaymentMethodId", SqlDbType.VarChar, 10).Value = form.PaymentMethodId;
                if ((int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0) != 1)
                    throw new ArgumentException("Hình thức thanh toán không hợp lệ cho kênh trực tuyến.");
            }

            var roomSeats = await GetSeatOptionsForTransactionAsync(
                connection, transaction, roomId, form.ShowtimeId, cancellationToken);
            SeatSelectionRules.Validate(roomSeats, seatIds, form.AcceptSeparatedSeats);
            var selectedSeats = roomSeats.Where(seat => seatIds.Contains(seat.Id, StringComparer.OrdinalIgnoreCase)).ToArray();

            var invoiceId = CreateId("HD");
            const string invoiceSql = """
                INSERT INTO dbo.HOADON (MaHD, MaKH, MaKenh, MaPTTT, TrangThai)
                VALUES (@InvoiceId, @CustomerId, 'KB002', @PaymentMethodId, N'Chờ thanh toán');
                """;
            await using (var command = new SqlCommand(invoiceSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 10).Value = invoiceId;
                command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 10).Value =
                    string.IsNullOrWhiteSpace(customerId) ? DBNull.Value : customerId;
                command.Parameters.Add("@PaymentMethodId", SqlDbType.VarChar, 10).Value = form.PaymentMethodId;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            const string ticketSql = """
                INSERT INTO dbo.VE (MaVe, MaHD, MaSuatChieu, MaGhe, GiaBan, TrangThai)
                VALUES (@TicketId, @InvoiceId, @ShowtimeId, @SeatId, @Price, N'Đang giữ chỗ');
                """;
            foreach (var seat in selectedSeats)
            {
                await using var command = new SqlCommand(ticketSql, connection, transaction);
                command.Parameters.Add("@TicketId", SqlDbType.VarChar, 10).Value = CreateId("V");
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 10).Value = invoiceId;
                command.Parameters.Add("@ShowtimeId", SqlDbType.VarChar, 10).Value = form.ShowtimeId;
                command.Parameters.Add("@SeatId", SqlDbType.VarChar, 10).Value = seat.Id;
                AddMoney(command, "@Price", seat.Price);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return invoiceId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task PayInvoiceAsync(
        string invoiceId, string? customerId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await EnsureInvoiceAccessAsync(connection, invoiceId, customerId, cancellationToken);
        await using var command = new SqlCommand("dbo.usp_ThanhToanHoaDon", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@MaHD", SqlDbType.VarChar, 10).Value = invoiceId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CancelPendingInvoiceAsync(
        string invoiceId, string? customerId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await EnsureInvoiceAccessAsync(connection, invoiceId, customerId, cancellationToken);
        await using var command = new SqlCommand("dbo.usp_HuyHoaDonChoThanhToan", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@MaHD", SqlDbType.VarChar, 10).Value = invoiceId;
        command.Parameters.Add("@MaNV", SqlDbType.VarChar, 10).Value = DBNull.Value;
        command.Parameters.Add("@LyDo", SqlDbType.NVarChar, 300).Value = "Khách hủy";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ExpireReservationsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_HuyGiuChoQuaHan", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<InvoiceModel?> GetInvoiceAsync(
        string invoiceId, string? customerId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT h.MaHD, h.ThoiGianLap, h.TrangThai, pttt.TenPTTT, k.TenKenh,
                   COALESCE((SELECT SUM(CASE WHEN vt.TrangThai IN (N'Đang giữ chỗ', N'Đã bán')
                                             THEN vt.GiaBan ELSE 0 END)
                             FROM dbo.VE AS vt WHERE vt.MaHD = h.MaHD), 0) AS TongTien,
                   (SELECT MIN(DATEADD(minute, 10, vt.ThoiGianTao))
                    FROM dbo.VE AS vt
                    WHERE vt.MaHD=h.MaHD AND vt.TrangThai=N'Đang giữ chỗ') AS HoldExpiresAt,
                   v.MaVe, p.TenPhim, pc.TenPhong, sc.ThoiGianBatDau,
                   g.HangGhe, g.SoGhe, v.GiaBan, v.TrangThai
            FROM dbo.HOADON AS h
            JOIN dbo.PHUONGTHUCTT AS pttt ON pttt.MaPTTT = h.MaPTTT
            JOIN dbo.KENHBAN AS k ON k.MaKenh = h.MaKenh
            LEFT JOIN dbo.VE AS v ON v.MaHD = h.MaHD
            LEFT JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = v.MaSuatChieu
            LEFT JOIN dbo.PHIM AS p ON p.MaPhim = sc.MaPhim
            LEFT JOIN dbo.PHONGCHIEU AS pc ON pc.MaPhong = sc.MaPhong
            LEFT JOIN dbo.GHE AS g ON g.MaGhe = v.MaGhe
            WHERE h.MaHD = @InvoiceId
              AND (@CustomerId IS NULL OR h.MaKH = @CustomerId)
            ORDER BY v.MaVe;
            """;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 10).Value = invoiceId;
        command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 10).Value =
            string.IsNullOrWhiteSpace(customerId) ? DBNull.Value : customerId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var id = reader.GetString(0);
        var createdAt = reader.GetDateTime(1);
        var status = reader.GetString(2);
        var payment = reader.GetString(3);
        var channel = reader.GetString(4);
        var total = reader.GetDecimal(5);
        DateTime? holdExpiresAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
        var tickets = new List<InvoiceTicket>();
        do
        {
            if (!reader.IsDBNull(7))
            {
                tickets.Add(new InvoiceTicket(
                    reader.GetString(7),
                    reader.IsDBNull(8) ? "" : reader.GetString(8),
                    reader.IsDBNull(9) ? "" : reader.GetString(9),
                    reader.IsDBNull(10) ? default : reader.GetDateTime(10),
                    reader.IsDBNull(11) ? "" : $"{reader.GetString(11)}{reader.GetInt16(12)}",
                    reader.GetDecimal(13),
                    reader.GetString(14)));
            }
        } while (await reader.ReadAsync(cancellationToken));

        return new InvoiceModel(id, createdAt, status, payment, channel, total, holdExpiresAt, tickets);
    }

    public async Task<IReadOnlyList<CustomerInvoiceRow>> GetCustomerInvoicesAsync(
        string customerId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT h.MaHD, h.ThoiGianLap, h.TrangThai,
                   COALESCE(MIN(p.TenPhim), N'Chưa có thông tin phim') AS TenPhim,
                   MIN(sc.ThoiGianBatDau) AS ThoiGianChieu,
                   COALESCE(SUM(CASE WHEN v.TrangThai IN (N'Đang giữ chỗ', N'Đã bán')
                                     THEN v.GiaBan ELSE 0 END), 0) AS TongTien
            FROM dbo.HOADON AS h
            LEFT JOIN dbo.VE AS v ON v.MaHD = h.MaHD
            LEFT JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = v.MaSuatChieu
            LEFT JOIN dbo.PHIM AS p ON p.MaPhim = sc.MaPhim
            WHERE h.MaKH = @CustomerId
            GROUP BY h.MaHD, h.ThoiGianLap, h.TrangThai
            ORDER BY h.ThoiGianLap DESC;
            """;

        var invoices = new List<CustomerInvoiceRow>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 10).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            invoices.Add(new CustomerInvoiceRow(
                reader.GetString(0),
                reader.GetDateTime(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                reader.GetDecimal(5)));
        }
        return invoices;
    }

    private static async Task EnsureInvoiceAccessAsync(
        SqlConnection connection, string invoiceId, string? customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM dbo.HOADON
            WHERE MaHD = @InvoiceId
              AND (@CustomerId IS NULL OR MaKH = @CustomerId);
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 10).Value = invoiceId;
        command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 10).Value =
            string.IsNullOrWhiteSpace(customerId) ? DBNull.Value : customerId;
        if ((int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0) != 1)
            throw new KeyNotFoundException("Không tìm thấy hóa đơn của tài khoản này.");
    }

    public async Task<IReadOnlyList<TicketOperationRow>> GetSoldTicketsAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.MaVe, v.MaHD, p.TenPhim, sc.MaSuatChieu, sc.ThoiGianBatDau,
                   pc.TenPhong, g.HangGhe, g.SoGhe, v.GiaBan, v.TrangThai,
                   CAST(CASE WHEN g.LoaiGhe=N'Đôi' THEN 1 ELSE 0 END AS bit)
            FROM dbo.VE AS v
            JOIN dbo.HOADON AS h ON h.MaHD = v.MaHD
            JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = v.MaSuatChieu
            JOIN dbo.PHIM AS p ON p.MaPhim = sc.MaPhim
            JOIN dbo.PHONGCHIEU AS pc ON pc.MaPhong = sc.MaPhong
            JOIN dbo.GHE AS g ON g.MaGhe = v.MaGhe
            WHERE v.TrangThai = N'Đã bán' AND h.TrangThai = N'Đã thanh toán'
            ORDER BY sc.ThoiGianBatDau, v.MaHD, g.HangGhe, g.SoGhe;
            """;
        var rows = new List<TicketOperationRow>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadTicketOperation(reader));
        return rows;
    }

    public async Task<ExchangePageModel?> GetExchangePageAsync(
        string ticketId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var ticket = await GetSoldTicketAsync(connection, ticketId, cancellationToken);
        if (ticket is null)
            return null;

        var showtimes = await GetShowtimesAsync(100, cancellationToken);
        var paymentMethods = await ReadOptionsAsync(connection, """
            SELECT p.MaPTTT, p.TenPTTT
            FROM dbo.HOADON AS h
            JOIN dbo.KENH_PTTT AS kp ON kp.MaKenh = h.MaKenh
            JOIN dbo.PHUONGTHUCTT AS p ON p.MaPTTT = kp.MaPTTT
            WHERE h.MaHD = @InvoiceId AND p.TrangThai = N'Đang hoạt động'
            ORDER BY p.TenPTTT;
            """, cancellationToken, ("@InvoiceId", ticket.InvoiceId));
        var employees = await ReadOptionsAsync(connection, """
            SELECT MaNV, HoTen
            FROM dbo.NHANVIEN
            WHERE TrangThai = N'Đang làm'
            ORDER BY HoTen;
            """, cancellationToken);
        return new ExchangePageModel(ticket, showtimes, paymentMethods, employees);
    }

    public async Task<IReadOnlyList<SeatOption>> GetAvailableSeatsAsync(
        string showtimeId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT g.MaGhe, g.HangGhe, g.SoGhe, g.LoaiGhe,
                   COALESCE(price.GiaVe,sc.GiaVeCoBan)
            FROM dbo.SUATCHIEU AS sc
            JOIN dbo.GHE AS g ON g.MaPhong = sc.MaPhong
            LEFT JOIN dbo.GIAVE_SUAT_HANG AS price
              ON price.MaSuatChieu=sc.MaSuatChieu AND price.LoaiGhe=g.LoaiGhe
            WHERE sc.MaSuatChieu = @ShowtimeId
              AND sc.TrangThai = N'Sắp chiếu'
              AND sc.ThoiGianBatDau > SYSDATETIME()
              AND g.TrangThai = N'Hoạt động'
              AND NOT (sc.KhoaGheBienVaHangDau=1
                       AND (g.HangGhe IN ('A','B')
                            OR g.SoGhe <= (SELECT MIN(SoGhe)+1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)
                            OR g.SoGhe >= (SELECT MAX(SoGhe)-1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)))
              AND (g.LoaiGhe<>N'Đôi' OR EXISTS
                   (
                       SELECT 1 FROM dbo.GHE pair
                       WHERE pair.MaPhong=g.MaPhong AND pair.HangGhe=g.HangGhe
                         AND pair.SoGhe=CASE WHEN g.SoGhe%2=1 THEN g.SoGhe+1 ELSE g.SoGhe-1 END
                         AND pair.TrangThai=N'Hoạt động'
                         AND NOT EXISTS (SELECT 1 FROM dbo.VE pairTicket
                                         WHERE pairTicket.MaSuatChieu=sc.MaSuatChieu
                                           AND pairTicket.MaGhe=pair.MaGhe
                                           AND pairTicket.TrangThai IN (N'Đang giữ chỗ',N'Đã bán'))
                   ))
              AND NOT EXISTS
              (
                  SELECT 1 FROM dbo.VE AS v
                  WHERE v.MaSuatChieu = sc.MaSuatChieu
                    AND v.MaGhe = g.MaGhe
                    AND v.TrangThai IN (N'Đang giữ chỗ', N'Đã bán')
              )
            ORDER BY g.HangGhe, g.SoGhe;
            """;
        var seats = new List<SeatOption>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ShowtimeId", SqlDbType.VarChar, 10).Value = showtimeId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            seats.Add(new SeatOption(reader.GetString(0), reader.GetString(1),
                reader.GetInt16(2), reader.GetString(3), true, reader.GetDecimal(4)));
        return seats;
    }

    public async Task ReturnTicketAsync(
        string ticketId, string? employeeId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_TraVe", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@MaVe", SqlDbType.VarChar, 10).Value = ticketId;
        command.Parameters.Add("@MaNV", SqlDbType.VarChar, 10).Value =
            string.IsNullOrWhiteSpace(employeeId) ? DBNull.Value : employeeId;
        command.Parameters.Add("@LyDo", SqlDbType.NVarChar, 300).Value = "Khách yêu cầu trả vé";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ExchangeTicketAsync(
        ExchangeTicketForm form, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_DoiVe", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@MaVeGoc", SqlDbType.VarChar, 10).Value = form.OriginalTicketId;
        command.Parameters.Add("@MaSuatChieuMoi", SqlDbType.VarChar, 10).Value = form.NewShowtimeId;
        command.Parameters.Add("@MaGheMoi", SqlDbType.VarChar, 10).Value = form.NewSeatId;
        command.Parameters.Add("@MaVeMoi", SqlDbType.VarChar, 10).Value = CreateId("V");
        command.Parameters.Add("@MaPTTTChenhlech", SqlDbType.VarChar, 10).Value =
            string.IsNullOrWhiteSpace(form.DifferencePaymentMethodId)
                ? DBNull.Value : form.DifferencePaymentMethodId;
        command.Parameters.Add("@MaNV", SqlDbType.VarChar, 10).Value =
            string.IsNullOrWhiteSpace(form.EmployeeId) ? DBNull.Value : form.EmployeeId;
        command.Parameters.Add("@LyDo", SqlDbType.NVarChar, 300).Value = "Khách yêu cầu đổi suất chiếu";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ReportPageModel> GetRevenueReportAsync(
        DateTime from, DateTime to, string groupBy, CancellationToken cancellationToken)
    {
        if (to.Date < from.Date)
            throw new ArgumentException("Ngày kết thúc không được trước ngày bắt đầu.");

        var fromDate = from.Date;
        var toDate = to.Date;
        var dimension = groupBy switch
        {
            "date" => ("CONVERT(nvarchar(10), e.EventTime, 23)", "Ngày"),
            "movie" => ("e.MovieTitle", "Phim"),
            "showtime" => ("e.ShowtimeId + N' · ' + e.MovieTitle", "Suất chiếu · Phim"),
            "employee" => ("e.EmployeeName", "Nhân viên"),
            _ => throw new ArgumentException("Kiểu báo cáo không hợp lệ.")
        };

        var sql = $"""
            WITH Events AS
            (
                SELECT v.ThoiGianBan AS EventTime, p.TenPhim AS MovieTitle,
                       sc.MaSuatChieu AS ShowtimeId, COALESCE(nv.HoTen, N'Online') AS EmployeeName,
                       v.GiaBan AS TicketRevenue, CAST(0 AS decimal(12, 2)) AS ProcessingFees,
                       1 AS TicketCount
                FROM dbo.VE AS v
                JOIN dbo.HOADON AS h ON h.MaHD = v.MaHD
                JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = v.MaSuatChieu
                JOIN dbo.PHIM AS p ON p.MaPhim = sc.MaPhim
                LEFT JOIN dbo.NHANVIEN AS nv ON nv.MaNV = h.MaNV
                WHERE v.TrangThai = N'Đã bán'
                  AND v.ThoiGianBan >= @From
                  AND v.ThoiGianBan < DATEADD(day, 1, @To)

                UNION ALL

                SELECT x.ThoiGianXL, p.TenPhim, sc.MaSuatChieu,
                       COALESCE(nv.HoTen, N'Online'),
                       CAST(0 AS decimal(12, 2)), x.PhiXuLy, 0
                FROM dbo.XULYVE AS x
                JOIN dbo.VE AS v ON v.MaVe = x.MaVeGoc
                JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = v.MaSuatChieu
                JOIN dbo.PHIM AS p ON p.MaPhim = sc.MaPhim
                LEFT JOIN dbo.NHANVIEN AS nv ON nv.MaNV = x.MaNV
                WHERE x.PhiXuLy > 0
                  AND x.ThoiGianXL >= @From
                  AND x.ThoiGianXL < DATEADD(day, 1, @To)
            )
            SELECT {dimension.Item1} AS GroupLabel,
                   SUM(e.TicketRevenue) AS TicketRevenue,
                   SUM(e.ProcessingFees) AS ProcessingFees,
                   SUM(e.TicketRevenue + e.ProcessingFees) AS NetRevenue,
                   SUM(e.TicketCount) AS TicketCount
            FROM Events AS e
            GROUP BY {dimension.Item1}
            ORDER BY NetRevenue DESC, GroupLabel;
            """;

        var rows = new List<ReportRow>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@From", SqlDbType.Date).Value = fromDate;
        command.Parameters.Add("@To", SqlDbType.Date).Value = toDate;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new ReportRow(reader.GetString(0), reader.GetDecimal(1),
                reader.GetDecimal(2), reader.GetDecimal(3), reader.GetInt32(4)));
        return new ReportPageModel(fromDate, toDate, groupBy, dimension.Item2, rows);
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<List<SeatOption>> GetSeatOptionsForTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string roomId,
        string showtimeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT g.MaGhe,g.HangGhe,g.SoGhe,g.LoaiGhe,
                   CASE WHEN g.TrangThai=N'Hoạt động' AND NOT EXISTS
                   (
                       SELECT 1 FROM dbo.VE v WITH (UPDLOCK,HOLDLOCK)
                       WHERE v.MaSuatChieu=@Showtime AND v.MaGhe=g.MaGhe
                         AND v.TrangThai IN (N'Đang giữ chỗ',N'Đã bán')
                   )
                     AND NOT (showtime.KhoaGheBienVaHangDau=1
                              AND (g.HangGhe IN ('A','B')
                                   OR g.SoGhe <= (SELECT MIN(SoGhe)+1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)
                                   OR g.SoGhe >= (SELECT MAX(SoGhe)-1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)))
                     AND (g.LoaiGhe<>N'Đôi' OR EXISTS
                         (
                             SELECT 1 FROM dbo.GHE pair
                             WHERE pair.MaPhong=g.MaPhong AND pair.HangGhe=g.HangGhe
                               AND pair.SoGhe=CASE WHEN g.SoGhe%2=1 THEN g.SoGhe+1 ELSE g.SoGhe-1 END
                               AND pair.TrangThai=N'Hoạt động'
                               AND NOT EXISTS (SELECT 1 FROM dbo.VE pairTicket WITH (UPDLOCK,HOLDLOCK)
                                               WHERE pairTicket.MaSuatChieu=@Showtime
                                                 AND pairTicket.MaGhe=pair.MaGhe
                                                 AND pairTicket.TrangThai IN (N'Đang giữ chỗ',N'Đã bán'))
                         ))
                   THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                   COALESCE(price.GiaVe,showtime.GiaVeCoBan),
                   CAST(CASE WHEN showtime.KhoaGheBienVaHangDau=1
                              AND (g.HangGhe IN ('A','B')
                                   OR g.SoGhe <= (SELECT MIN(SoGhe)+1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)
                                   OR g.SoGhe >= (SELECT MAX(SoGhe)-1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe))
                             THEN 1 ELSE 0 END AS bit)
            FROM dbo.GHE g WITH (UPDLOCK,HOLDLOCK)
            JOIN dbo.SUATCHIEU showtime ON showtime.MaSuatChieu=@Showtime
                                        AND showtime.MaPhong=g.MaPhong
            LEFT JOIN dbo.GIAVE_SUAT_HANG price
              ON price.MaSuatChieu=showtime.MaSuatChieu AND price.LoaiGhe=g.LoaiGhe
            WHERE g.MaPhong=@Room
            ORDER BY g.HangGhe,g.SoGhe;
            """;
        var seats = new List<SeatOption>();
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Room", SqlDbType.VarChar, 10).Value = roomId;
        command.Parameters.Add("@Showtime", SqlDbType.VarChar, 10).Value = showtimeId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            seats.Add(new SeatOption(reader.GetString(0), reader.GetString(1), reader.GetInt16(2),
                reader.GetString(3), reader.GetBoolean(4), reader.GetDecimal(5), reader.GetBoolean(6)));
        return seats;
    }

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 12;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private static async Task<ShowtimeRow?> GetShowtimeAsync(
        SqlConnection connection, string showtimeId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT sc.MaSuatChieu, p.TenPhim, pc.TenPhong,
                   sc.ThoiGianBatDau, sc.ThoiGianKetThuc, sc.GiaVeCoBan, sc.TrangThai,
                   (SELECT COUNT(*)
                    FROM dbo.GHE AS g
                    WHERE g.MaPhong = sc.MaPhong
                      AND g.TrangThai = N'Hoạt động'
                      AND NOT EXISTS
                      (
                          SELECT 1 FROM dbo.VE AS v
                          WHERE v.MaSuatChieu = sc.MaSuatChieu AND v.MaGhe = g.MaGhe
                            AND v.TrangThai IN (N'Đang giữ chỗ', N'Đã bán')
                      )) AS AvailableSeats
            FROM dbo.SUATCHIEU AS sc
            JOIN dbo.PHIM AS p ON p.MaPhim = sc.MaPhim
            JOIN dbo.PHONGCHIEU AS pc ON pc.MaPhong = sc.MaPhong
            WHERE sc.MaSuatChieu = @ShowtimeId
              AND sc.ThoiGianBatDau >= SYSDATETIME()
              AND sc.TrangThai = N'Sắp chiếu';
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ShowtimeId", SqlDbType.VarChar, 10).Value = showtimeId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadShowtime(reader) : null;
    }

    private static async Task<IReadOnlyList<LookupOption>> ReadOptionsAsync(
        SqlConnection connection, string sql, CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var options = new List<LookupOption>();
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            options.Add(new LookupOption(reader.GetString(0), reader.GetString(1)));
        return options;
    }

    private static async Task<TicketOperationRow?> GetSoldTicketAsync(
        SqlConnection connection, string ticketId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.MaVe, v.MaHD, p.TenPhim, sc.MaSuatChieu, sc.ThoiGianBatDau,
                   pc.TenPhong, g.HangGhe, g.SoGhe, v.GiaBan, v.TrangThai,
                   CAST(CASE WHEN g.LoaiGhe=N'Đôi' THEN 1 ELSE 0 END AS bit)
            FROM dbo.VE AS v
            JOIN dbo.HOADON AS h ON h.MaHD = v.MaHD
            JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = v.MaSuatChieu
            JOIN dbo.PHIM AS p ON p.MaPhim = sc.MaPhim
            JOIN dbo.PHONGCHIEU AS pc ON pc.MaPhong = sc.MaPhong
            JOIN dbo.GHE AS g ON g.MaGhe = v.MaGhe
            WHERE v.MaVe = @TicketId AND v.TrangThai = N'Đã bán'
              AND h.TrangThai = N'Đã thanh toán';
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TicketId", SqlDbType.VarChar, 10).Value = ticketId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTicketOperation(reader) : null;
    }

    private static TicketOperationRow ReadTicketOperation(SqlDataReader reader) =>
        new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetDateTime(4), reader.GetString(5),
            $"{reader.GetString(6)}{reader.GetInt16(7)}", reader.GetDecimal(8),
            reader.GetString(9), reader.GetBoolean(10));

    private static ShowtimeRow ReadShowtime(SqlDataReader reader) =>
        new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.GetDateTime(3), reader.GetDateTime(4), reader.GetDecimal(5),
            reader.GetString(6), reader.GetInt32(7));

    private static string CreateId(string prefix) =>
        prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
