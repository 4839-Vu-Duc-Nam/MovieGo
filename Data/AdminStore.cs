using System.Data;
using Microsoft.Data.SqlClient;
using MovieGoApp.Models;

namespace MovieGoApp.Data;

public sealed class AdminStore(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("MovieGo")
        ?? throw new InvalidOperationException("Connection string 'MovieGo' is missing.");

    public async Task<AdminPageModel> GetPageAsync(CancellationToken cancellationToken)
    {
        var movies = await ReadAsync("""
            SELECT p.MaPhim, p.TenPhim, p.ThoiLuong, p.NgonNgu, p.GioiHanTuoi,
                   p.NgayKhoiChieu, p.TrangThai,
                   COALESCE(STRING_AGG(t.TenTheLoai, N', '), N'Chưa phân loại')
            FROM dbo.PHIM p
            LEFT JOIN dbo.PHIM_THELOAI pt ON pt.MaPhim=p.MaPhim
            LEFT JOIN dbo.THELOAI t ON t.MaTheLoai=pt.MaTheLoai
            GROUP BY p.MaPhim,p.TenPhim,p.ThoiLuong,p.NgonNgu,p.GioiHanTuoi,p.NgayKhoiChieu,p.TrangThai
            ORDER BY p.TenPhim;
            """, r => new MovieRow(r.GetString(0), r.GetString(1), r.GetInt16(2), r.GetString(3),
            r.GetString(4), r.IsDBNull(5) ? null : r.GetDateTime(5), r.GetString(6), r.GetString(7)), cancellationToken);
        var genres = await ReadAsync("SELECT MaTheLoai,TenTheLoai FROM dbo.THELOAI ORDER BY TenTheLoai;",
            r => new LookupOption(r.GetString(0), r.GetString(1)), cancellationToken);
        var rooms = await ReadAsync("""
            SELECT room.MaPhong,room.TenPhong,room.LoaiPhong,room.TrangThai,COUNT(seat.MaGhe)
            FROM dbo.PHONGCHIEU room
            LEFT JOIN dbo.GHE seat ON seat.MaPhong=room.MaPhong
            GROUP BY room.MaPhong,room.TenPhong,room.LoaiPhong,room.TrangThai
            ORDER BY room.TenPhong;
            """, r => new RoomRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4)), cancellationToken);
        var seats = await ReadAsync("""
            SELECT g.MaGhe,p.TenPhong,g.HangGhe,g.SoGhe,g.LoaiGhe,g.TrangThai
            FROM dbo.GHE g JOIN dbo.PHONGCHIEU p ON p.MaPhong=g.MaPhong
            ORDER BY p.TenPhong,g.HangGhe,g.SoGhe;
            """, r => new SeatRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt16(3),
            r.GetString(4), r.GetString(5)), cancellationToken);
        var showtimes = await ReadAsync("""
            SELECT sc.MaSuatChieu,p.TenPhim,pc.TenPhong,sc.ThoiGianBatDau,sc.ThoiGianKetThuc,sc.GiaVeCoBan,
                   COALESCE(vip.GiaVe,sc.GiaVeCoBan),COALESCE(couple.GiaVe,sc.GiaVeCoBan),sc.TrangThai,
                   sc.KhoaGheBienVaHangDau
            FROM dbo.SUATCHIEU sc JOIN dbo.PHIM p ON p.MaPhim=sc.MaPhim
            JOIN dbo.PHONGCHIEU pc ON pc.MaPhong=sc.MaPhong
            LEFT JOIN dbo.GIAVE_SUAT_HANG vip ON vip.MaSuatChieu=sc.MaSuatChieu AND vip.LoaiGhe=N'VIP'
            LEFT JOIN dbo.GIAVE_SUAT_HANG couple ON couple.MaSuatChieu=sc.MaSuatChieu AND couple.LoaiGhe=N'Đôi'
            ORDER BY sc.ThoiGianBatDau DESC;
            """, r => new ShowtimeAdminRow(r.GetString(0), r.GetString(1), r.GetString(2),
            r.GetDateTime(3), r.GetDateTime(4), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7),
            r.GetString(8), r.GetBoolean(9)), cancellationToken);
        var customers = await ReadAsync("SELECT MaKH,HoTen,SoDienThoai,Email,NgaySinh FROM dbo.KHACHHANG ORDER BY HoTen;",
            r => new CustomerRow(r.GetString(0), r.GetString(1), NullableString(r, 2),
            NullableString(r, 3), r.IsDBNull(4) ? null : r.GetDateTime(4)), cancellationToken);
        var employees = await ReadAsync("SELECT MaNV,HoTen,SoDienThoai,Email,ChucVu,TrangThai FROM dbo.NHANVIEN ORDER BY HoTen;",
            r => new EmployeeRow(r.GetString(0), r.GetString(1), NullableString(r, 2),
            NullableString(r, 3), r.GetString(4), r.GetString(5)), cancellationToken);
        return new AdminPageModel(movies, genres, rooms, seats, showtimes, customers, employees);
    }

    public async Task SaveMovieAsync(MovieEditForm form, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var id = CleanId(form.Id, "P");
            const string upsert = """
                IF EXISTS (SELECT 1 FROM dbo.PHIM WHERE MaPhim=@Id)
                    UPDATE dbo.PHIM SET TenPhim=@Title,ThoiLuong=@Duration,NgonNgu=@Language,
                        GioiHanTuoi=@Age,NgayKhoiChieu=@Release,TrangThai=@Status WHERE MaPhim=@Id;
                ELSE
                    INSERT dbo.PHIM(MaPhim,TenPhim,ThoiLuong,NgonNgu,GioiHanTuoi,NgayKhoiChieu,TrangThai)
                    VALUES(@Id,@Title,@Duration,@Language,@Age,@Release,@Status);
                DELETE FROM dbo.PHIM_THELOAI WHERE MaPhim=@Id;
                """;
            await using (var command = new SqlCommand(upsert, connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.VarChar, 10).Value = id;
                command.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = form.Title.Trim();
                command.Parameters.Add("@Duration", SqlDbType.SmallInt).Value = form.DurationMinutes;
                command.Parameters.Add("@Language", SqlDbType.NVarChar, 50).Value = form.Language.Trim();
                command.Parameters.Add("@Age", SqlDbType.VarChar, 10).Value = form.AgeRating.Trim();
                command.Parameters.Add("@Release", SqlDbType.Date).Value = form.ReleaseDate?.Date is DateTime date ? date : DBNull.Value;
                command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = form.Status;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var genreId in form.GenreIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await using var command = new SqlCommand(
                    "INSERT dbo.PHIM_THELOAI(MaPhim,MaTheLoai) VALUES(@MovieId,@GenreId);",
                    connection, transaction);
                command.Parameters.Add("@MovieId", SqlDbType.VarChar, 10).Value = id;
                command.Parameters.Add("@GenreId", SqlDbType.VarChar, 10).Value = genreId;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task SaveGenreAsync(GenreEditForm form, CancellationToken ct) =>
        ExecuteUpsertAsync("""
            IF EXISTS (SELECT 1 FROM dbo.THELOAI WHERE MaTheLoai=@Id)
                UPDATE dbo.THELOAI SET TenTheLoai=@Name,MoTa=@Description WHERE MaTheLoai=@Id;
            ELSE
                INSERT dbo.THELOAI(MaTheLoai,TenTheLoai,MoTa) VALUES(@Id,@Name,@Description);
            """, CleanId(form.Id, "TL"), ct,
            ("@Name", SqlDbType.NVarChar, 100, form.Name.Trim()),
            ("@Description", SqlDbType.NVarChar, 500, DbNull(form.Description)));

    public Task SaveRoomAsync(RoomEditForm form, CancellationToken ct) =>
        ExecuteUpsertAsync("""
            IF EXISTS (SELECT 1 FROM dbo.PHONGCHIEU WHERE MaPhong=@Id)
                UPDATE dbo.PHONGCHIEU SET TenPhong=@Name,LoaiPhong=@Type,TrangThai=@Status WHERE MaPhong=@Id;
            ELSE
                INSERT dbo.PHONGCHIEU(MaPhong,TenPhong,LoaiPhong,TrangThai) VALUES(@Id,@Name,@Type,@Status);
            """, CleanId(form.Id, "R"), ct,
            ("@Name", SqlDbType.NVarChar, 100, form.Name.Trim()),
            ("@Type", SqlDbType.NVarChar, 50, form.Type.Trim()),
            ("@Status", SqlDbType.NVarChar, 20, form.Status));

    public async Task SaveSeatAsync(SeatEditForm form, CancellationToken ct)
    {
        if (form.Type is not ("Thường" or "VIP"))
            throw new ArgumentException("Dùng tạo ghế hàng loạt để cấu hình ghế đôi theo cặp.");
        var row = form.Row.Trim().ToUpperInvariant();
        if (row.Length != 1 || row[0] is < 'A' or > 'Z')
            throw new ArgumentException("Tên hàng ghế phải là một ký tự từ A đến Z.");

        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        try
        {
            const string sql = """
                IF EXISTS (SELECT 1 FROM dbo.GHE WITH (UPDLOCK,HOLDLOCK) WHERE MaGhe=@Id)
                BEGIN
                    IF EXISTS (SELECT 1 FROM dbo.VE WHERE MaGhe=@Id)
                       AND EXISTS (SELECT 1 FROM dbo.GHE WHERE MaGhe=@Id
                                   AND (MaPhong<>@Room OR HangGhe<>@Row OR SoGhe<>@Number OR LoaiGhe<>@Type))
                        THROW 51030, N'Không thể đổi vị trí hoặc hạng ghế đã có lịch sử vé.', 1;
                    IF EXISTS (SELECT 1 FROM dbo.GHE
                               WHERE MaPhong=@Room AND LoaiGhe=N'Đôi' AND @Row>=HangGhe)
                        THROW 51034, N'Ghế thường/VIP không được đặt ở hàng ghế đôi cuối cùng hoặc phía sau.', 1;
                    IF EXISTS (SELECT 1 FROM dbo.GHE WHERE MaGhe=@Id AND MaPhong<>@Room)
                       AND (SELECT COUNT(*) FROM dbo.GHE WITH (UPDLOCK,HOLDLOCK) WHERE MaPhong=@Room)>=150
                        THROW 51033, N'Phòng không được vượt quá 150 ghế.', 1;
                    UPDATE dbo.GHE SET MaPhong=@Room,HangGhe=@Row,SoGhe=@Number,LoaiGhe=@Type,TrangThai=@Status
                    WHERE MaGhe=@Id;
                END
                ELSE
                BEGIN
                    IF (SELECT COUNT(*) FROM dbo.GHE WITH (UPDLOCK,HOLDLOCK) WHERE MaPhong=@Room) >= 150
                        THROW 51033, N'Phòng không được vượt quá 150 ghế.', 1;
                    IF EXISTS (SELECT 1 FROM dbo.GHE
                               WHERE MaPhong=@Room AND LoaiGhe=N'Đôi' AND @Row>=HangGhe)
                        THROW 51034, N'Ghế thường/VIP không được đặt ở hàng ghế đôi cuối cùng hoặc phía sau.', 1;
                    INSERT dbo.GHE(MaGhe,MaPhong,HangGhe,SoGhe,LoaiGhe,TrangThai)
                    VALUES(@Id,@Room,@Row,@Number,@Type,@Status);
                END;
                """;
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@Id", SqlDbType.VarChar, 10).Value = CleanId(form.Id, "G");
            command.Parameters.Add("@Room", SqlDbType.VarChar, 10).Value = form.RoomId;
            command.Parameters.Add("@Row", SqlDbType.VarChar, 5).Value = row;
            command.Parameters.Add("@Number", SqlDbType.SmallInt).Value = form.Number;
            command.Parameters.Add("@Type", SqlDbType.NVarChar, 30).Value = form.Type;
            command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = form.Status;
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> GenerateSeatsAsync(BulkSeatGenerationForm form, CancellationToken cancellationToken)
    {
        var rows = form.Rows.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(row => row.ToUpperInvariant())
            .ToArray();
        if (rows.Length == 0 || rows.Length > 26
            || rows.Any(row => row.Length != 1 || row[0] is < 'A' or > 'Z')
            || rows.Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Length)
            throw new ArgumentException("Nhập các hàng ghế A-Z, không trùng nhau, phân cách bằng dấu phẩy.");
        if (form.Type is not ("Thường" or "VIP" or "Đôi"))
            throw new ArgumentException("Loại ghế phải là Thường, VIP hoặc Đôi.");
        if ((long)rows.Length * form.SeatsPerRow > 150)
            throw new ArgumentException("Mỗi lần chỉ tạo tối đa 150 ghế trong một phòng.");
        if (form.FirstNumber + form.SeatsPerRow - 1 > short.MaxValue)
            throw new ArgumentException("Số ghế vượt giới hạn cho phép.");
        var isCouple = string.Equals(form.Type, "Đôi", StringComparison.OrdinalIgnoreCase);
        if (isCouple && (rows.Length != 1 || form.FirstNumber != 1 || form.SeatsPerRow % 2 != 0))
            throw new ArgumentException("Hàng ghế đôi phải là một hàng duy nhất, bắt đầu từ ghế 1 và có số ghế chẵn để ghép cặp.");

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            string lastRow;
            string lastCoupleRow;
            await using (var roomCommand = new SqlCommand("""
                SELECT
                    (SELECT COUNT(*) FROM dbo.GHE WITH (UPDLOCK,HOLDLOCK) WHERE MaPhong=@Room),
                    COALESCE((SELECT MAX(HangGhe) FROM dbo.GHE WITH (UPDLOCK,HOLDLOCK) WHERE MaPhong=@Room),''),
                    COALESCE((SELECT MAX(HangGhe) FROM dbo.GHE WITH (UPDLOCK,HOLDLOCK)
                              WHERE MaPhong=@Room AND LoaiGhe=N'Đôi'),''),
                    (SELECT COUNT(*) FROM dbo.PHONGCHIEU WITH (UPDLOCK,HOLDLOCK) WHERE MaPhong=@Room);
                """, connection, transaction))
            {
                roomCommand.Parameters.Add("@Room", SqlDbType.VarChar, 10).Value = form.RoomId;
                await using var reader = await roomCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken) || reader.GetInt32(3) != 1)
                    throw new ArgumentException("Phòng chiếu không tồn tại.");
                if (reader.GetInt32(0) > 150)
                    throw new ArgumentException("Phòng hiện đã vượt quá giới hạn 150 ghế.");
                lastRow = reader.GetString(1);
                lastCoupleRow = reader.GetString(2);
            }
            if (isCouple && string.CompareOrdinal(rows[0], lastRow) < 0)
                throw new ArgumentException("Ghế đôi phải nằm ở hàng cuối cùng của phòng.");
            if (isCouple && lastCoupleRow.Length > 0 && rows[0] != lastCoupleRow)
                throw new ArgumentException("Phòng chỉ được có một hàng ghế đôi, ở hàng cuối cùng.");
            if (!isCouple && lastCoupleRow.Length > 0
                          && rows.Any(row => string.CompareOrdinal(row, lastCoupleRow) >= 0))
                throw new ArgumentException("Không thể thêm ghế thường/VIP vào hàng ghế đôi cuối cùng hoặc phía sau hàng đó.");

            var created = 0;
            foreach (var row in rows)
            {
                for (var number = form.FirstNumber; number < form.FirstNumber + form.SeatsPerRow; number++)
                {
                    await using var command = new SqlCommand("""
                        IF NOT EXISTS
                        (
                            SELECT 1 FROM dbo.GHE WITH (UPDLOCK,HOLDLOCK)
                            WHERE MaPhong=@Room AND HangGhe=@Row AND SoGhe=@Number
                        )
                        BEGIN
                            IF (SELECT COUNT(*) FROM dbo.GHE WITH (UPDLOCK,HOLDLOCK) WHERE MaPhong=@Room) >= 150
                            BEGIN
                                SELECT -1;
                                RETURN;
                            END;
                            INSERT dbo.GHE(MaGhe,MaPhong,HangGhe,SoGhe,LoaiGhe,TrangThai)
                            VALUES(@Id,@Room,@Row,@Number,@Type,N'Hoạt động');
                            SELECT 1;
                        END
                        ELSE
                            SELECT 0;
                        """, connection, transaction);
                    command.Parameters.Add("@Id", SqlDbType.VarChar, 10).Value = NewId("G");
                    command.Parameters.Add("@Room", SqlDbType.VarChar, 10).Value = form.RoomId;
                    command.Parameters.Add("@Row", SqlDbType.VarChar, 5).Value = row;
                    command.Parameters.Add("@Number", SqlDbType.SmallInt).Value = number;
                    command.Parameters.Add("@Type", SqlDbType.NVarChar, 30).Value = form.Type.Trim();
                    var inserted = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
                    if (inserted < 0)
                        throw new ArgumentException("Phòng không được vượt quá 150 ghế.");
                    created += inserted;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return created;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SaveShowtimeAsync(ShowtimeEditForm form, CancellationToken ct)
    {
        if (form.VipPrice <= form.Price || form.CouplePrice <= form.VipPrice)
            throw new ArgumentException("Giá phải tăng theo hạng: thường < VIP < ghế đôi.");
        if (form.EndsAt <= form.StartsAt)
            throw new ArgumentException("Thời gian kết thúc phải sau thời gian bắt đầu.");

        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            const string sql = """
                IF EXISTS (SELECT 1 FROM dbo.SUATCHIEU WHERE MaSuatChieu=@Id)
                BEGIN
                    IF EXISTS (SELECT 1 FROM dbo.VE WHERE MaSuatChieu=@Id
                               AND TrangThai IN (N'Đang giữ chỗ',N'Đã bán'))
                       AND @Status=N'Đã hủy'
                        THROW 51031, N'Sử dụng nghiệp vụ hủy suất chiếu để hoàn tất vé đã bán.', 1;
                    IF EXISTS (SELECT 1 FROM dbo.SUATCHIEU WHERE MaSuatChieu=@Id
                               AND EXISTS (SELECT 1 FROM dbo.VE WHERE MaSuatChieu=@Id)
                               AND (MaPhim<>@Movie OR MaPhong<>@Room
                                    OR ThoiGianBatDau<>@Starts OR ThoiGianKetThuc<>@Ends))
                        THROW 51032, N'Không thể đổi phim, phòng hoặc giờ của suất đã có vé.', 1;
                    UPDATE dbo.SUATCHIEU SET MaPhim=@Movie,MaPhong=@Room,ThoiGianBatDau=@Starts,
                        ThoiGianKetThuc=@Ends,GiaVeCoBan=@Price,TrangThai=@Status,
                        KhoaGheBienVaHangDau=@BlockFrontAndCorners WHERE MaSuatChieu=@Id;
                END
                ELSE
                    INSERT dbo.SUATCHIEU(MaSuatChieu,MaPhim,MaPhong,ThoiGianBatDau,ThoiGianKetThuc,GiaVeCoBan,TrangThai,KhoaGheBienVaHangDau)
                    VALUES(@Id,@Movie,@Room,@Starts,@Ends,@Price,@Status,@BlockFrontAndCorners);

                UPDATE dbo.GIAVE_SUAT_HANG SET GiaVe=@Price WHERE MaSuatChieu=@Id AND LoaiGhe=N'Thường';
                IF @@ROWCOUNT=0 INSERT dbo.GIAVE_SUAT_HANG(MaSuatChieu,LoaiGhe,GiaVe) VALUES(@Id,N'Thường',@Price);
                UPDATE dbo.GIAVE_SUAT_HANG SET GiaVe=@VipPrice WHERE MaSuatChieu=@Id AND LoaiGhe=N'VIP';
                IF @@ROWCOUNT=0 INSERT dbo.GIAVE_SUAT_HANG(MaSuatChieu,LoaiGhe,GiaVe) VALUES(@Id,N'VIP',@VipPrice);
                UPDATE dbo.GIAVE_SUAT_HANG SET GiaVe=@CouplePrice WHERE MaSuatChieu=@Id AND LoaiGhe=N'Đôi';
                IF @@ROWCOUNT=0 INSERT dbo.GIAVE_SUAT_HANG(MaSuatChieu,LoaiGhe,GiaVe) VALUES(@Id,N'Đôi',@CouplePrice);
                """;
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@Id", SqlDbType.VarChar, 10).Value = CleanId(form.Id, "SC");
            command.Parameters.Add("@Movie", SqlDbType.VarChar, 10).Value = form.MovieId;
            command.Parameters.Add("@Room", SqlDbType.VarChar, 10).Value = form.RoomId;
            command.Parameters.Add("@Starts", SqlDbType.DateTime2).Value = form.StartsAt;
            command.Parameters.Add("@Ends", SqlDbType.DateTime2).Value = form.EndsAt;
            AddMoney(command, "@Price", form.Price);
            AddMoney(command, "@VipPrice", form.VipPrice);
            AddMoney(command, "@CouplePrice", form.CouplePrice);
            command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = form.Status;
            command.Parameters.Add("@BlockFrontAndCorners", SqlDbType.Bit).Value = form.BlockFrontAndCorners;
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public Task SaveCustomerAsync(CustomerEditForm form, CancellationToken ct) =>
        ExecuteUpsertAsync("""
            IF EXISTS (SELECT 1 FROM dbo.KHACHHANG WHERE MaKH=@Id)
                UPDATE dbo.KHACHHANG SET HoTen=@Name,SoDienThoai=@Phone,Email=@Email,NgaySinh=@Birth WHERE MaKH=@Id;
            ELSE
                INSERT dbo.KHACHHANG(MaKH,HoTen,SoDienThoai,Email,NgaySinh)
                VALUES(@Id,@Name,@Phone,@Email,@Birth);
            """, CleanId(form.Id, "KH"), ct,
            ("@Name", SqlDbType.NVarChar, 150, form.Name.Trim()),
            ("@Phone", SqlDbType.VarChar, 20, DbNull(form.Phone)),
            ("@Email", SqlDbType.VarChar, 254, DbNull(form.Email)),
            ("@Birth", SqlDbType.Date, 0, form.BirthDate?.Date is DateTime date ? date : DBNull.Value));

    public Task SaveEmployeeAsync(EmployeeEditForm form, CancellationToken ct) =>
        ExecuteUpsertAsync("""
            IF EXISTS (SELECT 1 FROM dbo.NHANVIEN WHERE MaNV=@Id)
                UPDATE dbo.NHANVIEN SET HoTen=@Name,SoDienThoai=@Phone,Email=@Email,ChucVu=@Role,TrangThai=@Status WHERE MaNV=@Id;
            ELSE
                INSERT dbo.NHANVIEN(MaNV,HoTen,SoDienThoai,Email,ChucVu,TrangThai)
                VALUES(@Id,@Name,@Phone,@Email,@Role,@Status);
            """, CleanId(form.Id, "NV"), ct,
            ("@Name", SqlDbType.NVarChar, 150, form.Name.Trim()),
            ("@Phone", SqlDbType.VarChar, 20, DbNull(form.Phone)),
            ("@Email", SqlDbType.VarChar, 254, DbNull(form.Email)),
            ("@Role", SqlDbType.NVarChar, 50, form.Role.Trim()),
            ("@Status", SqlDbType.NVarChar, 20, form.Status));

    public async Task CancelShowtimeAsync(
        string showtimeId, string employeeId, string reason, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_HuySuatChieu", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@MaSuatChieu", SqlDbType.VarChar, 10).Value = showtimeId;
        command.Parameters.Add("@MaNV", SqlDbType.VarChar, 10).Value = employeeId;
        command.Parameters.Add("@LyDo", SqlDbType.NVarChar, 300).Value = reason.Trim();
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CounterSalePageModel> GetCounterSalePageAsync(CancellationToken cancellationToken)
    {
        var showtimes = await ReadAsync("""
            SELECT TOP (100) sc.MaSuatChieu,p.TenPhim,pc.TenPhong,sc.ThoiGianBatDau,sc.ThoiGianKetThuc,
                   sc.GiaVeCoBan,sc.TrangThai,
                   (SELECT COUNT(*) FROM dbo.GHE g WHERE g.MaPhong=sc.MaPhong AND g.TrangThai=N'Hoạt động'
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
                    AND NOT EXISTS(SELECT 1 FROM dbo.VE v WHERE v.MaSuatChieu=sc.MaSuatChieu AND v.MaGhe=g.MaGhe
                        AND v.TrangThai IN (N'Đang giữ chỗ',N'Đã bán'))) AvailableSeats
            FROM dbo.SUATCHIEU sc JOIN dbo.PHIM p ON p.MaPhim=sc.MaPhim
            JOIN dbo.PHONGCHIEU pc ON pc.MaPhong=sc.MaPhong
            WHERE sc.TrangThai=N'Sắp chiếu' AND sc.ThoiGianBatDau>SYSDATETIME()
            ORDER BY sc.ThoiGianBatDau;
            """, r => new ShowtimeRow(r.GetString(0), r.GetString(1), r.GetString(2),
            r.GetDateTime(3), r.GetDateTime(4), r.GetDecimal(5), r.GetString(6), r.GetInt32(7)), cancellationToken);
        var methods = await ReadAsync("""
            SELECT p.MaPTTT,p.TenPTTT FROM dbo.PHUONGTHUCTT p
            JOIN dbo.KENH_PTTT kp ON kp.MaPTTT=p.MaPTTT
            WHERE kp.MaKenh='KB001' AND p.TrangThai=N'Đang hoạt động' ORDER BY p.TenPTTT;
            """, r => new LookupOption(r.GetString(0), r.GetString(1)), cancellationToken);
        var customers = await ReadAsync("SELECT MaKH,HoTen FROM dbo.KHACHHANG ORDER BY HoTen;",
            r => new LookupOption(r.GetString(0), r.GetString(1)), cancellationToken);
        var employees = await ReadAsync("SELECT MaNV,HoTen FROM dbo.NHANVIEN WHERE TrangThai=N'Đang làm' ORDER BY HoTen;",
            r => new LookupOption(r.GetString(0), r.GetString(1)), cancellationToken);
        return new CounterSalePageModel(showtimes, methods, customers, employees);
    }

    public async Task<string> CreateCounterSaleAsync(CounterSaleForm form, CancellationToken cancellationToken)
    {
        var seatIds = form.SeatIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (seatIds.Length is 0 or > 8)
            throw new ArgumentException("Mỗi giao dịch được chọn từ 1 đến 8 ghế.");

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            const string showtimeSql = """
                SELECT MaPhong FROM dbo.SUATCHIEU WITH (UPDLOCK,HOLDLOCK)
                WHERE MaSuatChieu=@Showtime AND TrangThai=N'Sắp chiếu' AND ThoiGianBatDau>SYSDATETIME();
                """;
            string? room = null;
            await using (var command = new SqlCommand(showtimeSql, connection, transaction))
            {
                command.Parameters.Add("@Showtime", SqlDbType.VarChar, 10).Value = form.ShowtimeId;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                    room = reader.GetString(0);
            }
            if (room is null)
                throw new InvalidOperationException("Suất chiếu không còn mở bán.");

            const string staffSql = """
                SELECT COUNT(*) FROM dbo.NHANVIEN WITH (UPDLOCK,HOLDLOCK)
                WHERE MaNV=@Employee AND TrangThai=N'Đang làm';
                """;
            await using (var command = new SqlCommand(staffSql, connection, transaction))
            {
                command.Parameters.Add("@Employee", SqlDbType.VarChar, 10).Value = form.EmployeeId;
                if ((int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0) != 1)
                    throw new ArgumentException("Nhân viên không hợp lệ hoặc đã ngừng làm.");
            }

            const string paymentSql = """
                SELECT COUNT(*) FROM dbo.KENH_PTTT kp JOIN dbo.PHUONGTHUCTT p ON p.MaPTTT=kp.MaPTTT
                WHERE kp.MaKenh='KB001' AND kp.MaPTTT=@Payment AND p.TrangThai=N'Đang hoạt động';
                """;
            await using (var command = new SqlCommand(paymentSql, connection, transaction))
            {
                command.Parameters.Add("@Payment", SqlDbType.VarChar, 10).Value = form.PaymentMethodId;
                if ((int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0) != 1)
                    throw new ArgumentException("Hình thức thanh toán không hợp lệ tại quầy.");
            }

            var roomSeats = await GetRoomSeatsForSaleAsync(
                connection, transaction, room, form.ShowtimeId, cancellationToken);
            SeatSelectionRules.Validate(roomSeats, seatIds, form.AcceptSeparatedSeats);
            var selectedSeats = roomSeats
                .Where(seat => seatIds.Contains(seat.Id, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            var invoiceId = NewId("HD");
            const string invoiceSql = """
                INSERT dbo.HOADON(MaHD,MaNV,MaKH,MaKenh,MaPTTT,TrangThai)
                VALUES(@Invoice,@Employee,@Customer,'KB001',@Payment,N'Đã thanh toán');
                """;
            await using (var command = new SqlCommand(invoiceSql, connection, transaction))
            {
                command.Parameters.Add("@Invoice", SqlDbType.VarChar, 10).Value = invoiceId;
                command.Parameters.Add("@Employee", SqlDbType.VarChar, 10).Value = form.EmployeeId;
                command.Parameters.Add("@Customer", SqlDbType.VarChar, 10).Value =
                    string.IsNullOrWhiteSpace(form.CustomerId) ? DBNull.Value : form.CustomerId;
                command.Parameters.Add("@Payment", SqlDbType.VarChar, 10).Value = form.PaymentMethodId;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var seat in selectedSeats)
            {
                await using var command = new SqlCommand("""
                    INSERT dbo.VE(MaVe,MaHD,MaSuatChieu,MaGhe,GiaBan,ThoiGianBan,TrangThai)
                    VALUES(@Ticket,@Invoice,@Showtime,@Seat,@Price,SYSDATETIME(),N'Đã bán');
                    """, connection, transaction);
                command.Parameters.Add("@Ticket", SqlDbType.VarChar, 10).Value = NewId("V");
                command.Parameters.Add("@Invoice", SqlDbType.VarChar, 10).Value = invoiceId;
                command.Parameters.Add("@Showtime", SqlDbType.VarChar, 10).Value = form.ShowtimeId;
                command.Parameters.Add("@Seat", SqlDbType.VarChar, 10).Value = seat.Id;
                var amount = command.Parameters.Add("@Price", SqlDbType.Decimal);
                amount.Precision = 12;
                amount.Scale = 2;
                amount.Value = seat.Price;
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

    public async Task<IReadOnlyList<SeatOption>> GetAvailableSeatsAsync(
        string showtimeId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT g.MaGhe,g.HangGhe,g.SoGhe,g.LoaiGhe,
                   CASE WHEN g.TrangThai=N'Hoạt động' AND NOT EXISTS
                   (
                       SELECT 1 FROM dbo.VE v
                       WHERE v.MaSuatChieu=sc.MaSuatChieu AND v.MaGhe=g.MaGhe
                         AND v.TrangThai IN (N'Đang giữ chỗ',N'Đã bán')
                   )
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
                   THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                   COALESCE(price.GiaVe,sc.GiaVeCoBan),
                   CAST(CASE WHEN sc.KhoaGheBienVaHangDau=1
                              AND (g.HangGhe IN ('A','B')
                                   OR g.SoGhe <= (SELECT MIN(SoGhe)+1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)
                                   OR g.SoGhe >= (SELECT MAX(SoGhe)-1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe))
                             THEN 1 ELSE 0 END AS bit)
            FROM dbo.SUATCHIEU sc JOIN dbo.GHE g ON g.MaPhong=sc.MaPhong
            LEFT JOIN dbo.GIAVE_SUAT_HANG price
              ON price.MaSuatChieu=sc.MaSuatChieu AND price.LoaiGhe=g.LoaiGhe
            WHERE sc.MaSuatChieu=@Showtime AND sc.TrangThai=N'Sắp chiếu'
              AND sc.ThoiGianBatDau>SYSDATETIME()
            ORDER BY g.HangGhe,g.SoGhe;
            """;
        return await ReadAsync(sql, r => new SeatOption(r.GetString(0), r.GetString(1),
            r.GetInt16(2), r.GetString(3), r.GetBoolean(4), r.GetDecimal(5), r.GetBoolean(6)), cancellationToken,
            ("@Showtime", SqlDbType.VarChar, 10, showtimeId));
    }

    private static async Task<List<SeatOption>> GetRoomSeatsForSaleAsync(
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
                               AND NOT EXISTS (SELECT 1 FROM dbo.VE pairTicket WITH (UPDLOCK,HOLDLOCK)
                                               WHERE pairTicket.MaSuatChieu=@Showtime
                                                 AND pairTicket.MaGhe=pair.MaGhe
                                                 AND pairTicket.TrangThai IN (N'Đang giữ chỗ',N'Đã bán'))
                         ))
                   THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                   COALESCE(price.GiaVe,sc.GiaVeCoBan),
                   CAST(CASE WHEN sc.KhoaGheBienVaHangDau=1
                              AND (g.HangGhe IN ('A','B')
                                   OR g.SoGhe <= (SELECT MIN(SoGhe)+1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe)
                                   OR g.SoGhe >= (SELECT MAX(SoGhe)-1 FROM dbo.GHE WHERE MaPhong=g.MaPhong AND HangGhe=g.HangGhe))
                             THEN 1 ELSE 0 END AS bit)
            FROM dbo.GHE g WITH (UPDLOCK,HOLDLOCK)
            JOIN dbo.SUATCHIEU sc ON sc.MaSuatChieu=@Showtime AND sc.MaPhong=g.MaPhong
            LEFT JOIN dbo.GIAVE_SUAT_HANG price
              ON price.MaSuatChieu=sc.MaSuatChieu AND price.LoaiGhe=g.LoaiGhe
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

    private async Task ExecuteUpsertAsync(
        string sql, string id, CancellationToken cancellationToken,
        params (string Name, SqlDbType Type, int Size, object Value)[] values)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.VarChar, 10).Value = id;
        foreach (var item in values)
        {
            var parameter = item.Size == 0
                ? command.Parameters.Add(item.Name, item.Type)
                : command.Parameters.Add(item.Name, item.Type, item.Size);
            if (item.Type == SqlDbType.Decimal)
            {
                parameter.Precision = 12;
                parameter.Scale = 2;
            }
            parameter.Value = item.Value;
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<List<T>> ReadAsync<T>(
        string sql, Func<SqlDataReader, T> map, CancellationToken cancellationToken,
        params (string Name, SqlDbType Type, int Size, object Value)[] parameters)
    {
        var result = new List<T>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        foreach (var item in parameters)
        {
            var parameter = item.Size == 0
                ? command.Parameters.Add(item.Name, item.Type)
                : command.Parameters.Add(item.Name, item.Type, item.Size);
            parameter.Value = item.Value;
        }
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(map(reader));
        return result;
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 12;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private static string CleanId(string id, string prefix) =>
        string.IsNullOrWhiteSpace(id) ? NewId(prefix) : id.Trim();
    private static string NewId(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    private static object DbNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static string? NullableString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
