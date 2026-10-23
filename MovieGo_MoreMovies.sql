/*
    Adds six real-film sample records and upcoming demo showtimes.
    Safe to run more than once. Run against an existing MovieGo database.
    Movie metadata is based on the linked public film references in README.md.
    Age ratings and showtimes are illustrative local demo values.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.PHIM', N'U') IS NULL
    THROW 51050, N'Không tìm thấy bảng PHIM. Hãy chạy MovieGo.sql hoặc chọn đúng cơ sở dữ liệu MovieGo.', 1;
IF OBJECT_ID(N'dbo.GIAVE_SUAT_HANG', N'U') IS NULL
    THROW 51051, N'Chưa có bảng giá vé theo hạng ghế. Hãy chạy MovieGo_SeatRules_Migration.sql trước.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.PHONGCHIEU WHERE MaPhong IN ('R001', 'R002') AND TrangThai = N'Hoạt động')
    THROW 51052, N'Cần có ít nhất một trong hai phòng mẫu R001 hoặc R002 đang hoạt động để tạo lịch chiếu mẫu.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1
        FROM (VALUES
            ('MG001', N'Avatar: Lửa và Tro Tàn'),
            ('MG002', N'Phi Vụ Động Trời 2'),
            ('MG003', N'Minecraft: Phim Điện Ảnh'),
            ('MG004', N'Bí Kíp Luyện Rồng'),
            ('MG005', N'Superman'),
            ('MG006', N'F1: Phim Điện Ảnh')
        ) AS expected(MaPhim, TenPhim)
        JOIN dbo.PHIM AS existing ON existing.MaPhim = expected.MaPhim
        WHERE existing.TenPhim <> expected.TenPhim
    )
        THROW 51053, N'Mã phim MG001-MG006 đã được dùng cho phim khác.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM (VALUES
            ('TL004', N'Hành động'),
            ('TL005', N'Hoạt hình'),
            ('TL006', N'Hài'),
            ('TL007', N'Giả tưởng'),
            ('TL008', N'Siêu anh hùng'),
            ('TL009', N'Tâm lý'),
            ('TL010', N'Thể thao')
        ) AS expected(MaTheLoai, TenTheLoai)
        JOIN dbo.THELOAI AS existing ON existing.MaTheLoai = expected.MaTheLoai
        WHERE existing.TenTheLoai <> expected.TenTheLoai
    )
        THROW 51054, N'Mã thể loại TL004-TL010 đã được dùng cho thể loại khác.', 1;

    INSERT INTO dbo.THELOAI (MaTheLoai, TenTheLoai, MoTa)
    SELECT source.MaTheLoai, source.TenTheLoai, source.MoTa
    FROM (VALUES
        ('TL004', N'Hành động', N'Phim có các tình tiết hành động'),
        ('TL005', N'Hoạt hình', N'Phim hoạt hình'),
        ('TL006', N'Hài', N'Phim có yếu tố hài hước'),
        ('TL007', N'Giả tưởng', N'Phim có yếu tố giả tưởng'),
        ('TL008', N'Siêu anh hùng', N'Phim về các nhân vật siêu anh hùng'),
        ('TL009', N'Tâm lý', N'Phim khai thác tâm lý nhân vật'),
        ('TL010', N'Thể thao', N'Phim xoay quanh thể thao')
    ) AS source(MaTheLoai, TenTheLoai, MoTa)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.THELOAI AS existing
        WHERE existing.MaTheLoai = source.MaTheLoai
    );

    INSERT INTO dbo.PHIM
        (MaPhim, TenPhim, ThoiLuong, NgonNgu, GioiHanTuoi, NgayKhoiChieu, TrangThai)
    SELECT source.MaPhim, source.TenPhim, source.ThoiLuong, source.NgonNgu,
           source.GioiHanTuoi, source.NgayKhoiChieu, N'Đang chiếu'
    FROM (VALUES
        ('MG001', N'Avatar: Lửa và Tro Tàn', 197, N'Tiếng Anh, phụ đề Việt', 'T13', CONVERT(date, '2025-12-19')),
        ('MG002', N'Phi Vụ Động Trời 2', 108, N'Tiếng Anh, phụ đề Việt', 'P', CONVERT(date, '2025-11-26')),
        ('MG003', N'Minecraft: Phim Điện Ảnh', 101, N'Tiếng Anh, phụ đề Việt', 'P', CONVERT(date, '2025-04-04')),
        ('MG004', N'Bí Kíp Luyện Rồng', 125, N'Tiếng Anh, phụ đề Việt', 'P', CONVERT(date, '2025-06-13')),
        ('MG005', N'Superman', 129, N'Tiếng Anh, phụ đề Việt', 'T13', CONVERT(date, '2025-07-11')),
        ('MG006', N'F1: Phim Điện Ảnh', 155, N'Tiếng Anh, phụ đề Việt', 'T13', CONVERT(date, '2025-06-27'))
    ) AS source(MaPhim, TenPhim, ThoiLuong, NgonNgu, GioiHanTuoi, NgayKhoiChieu)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.PHIM AS existing
        WHERE existing.MaPhim = source.MaPhim
    );

    INSERT INTO dbo.PHIM_THELOAI (MaPhim, MaTheLoai)
    SELECT source.MaPhim, source.MaTheLoai
    FROM (VALUES
        ('MG001', 'TL001'), ('MG001', 'TL002'), ('MG001', 'TL004'),
        ('MG002', 'TL002'), ('MG002', 'TL005'), ('MG002', 'TL006'),
        ('MG003', 'TL002'), ('MG003', 'TL006'), ('MG003', 'TL007'),
        ('MG004', 'TL002'), ('MG004', 'TL004'), ('MG004', 'TL007'),
        ('MG005', 'TL001'), ('MG005', 'TL004'), ('MG005', 'TL008'),
        ('MG006', 'TL004'), ('MG006', 'TL009'), ('MG006', 'TL010')
    ) AS source(MaPhim, MaTheLoai)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.PHIM_THELOAI AS existing
        WHERE existing.MaPhim = source.MaPhim
          AND existing.MaTheLoai = source.MaTheLoai
    );

    DECLARE @PhimMoi TABLE
    (
        MaSuatChieu varchar(10) NOT NULL,
        MaPhim varchar(10) NOT NULL,
        MaPhong varchar(10) NOT NULL,
        ThoiLuong smallint NOT NULL,
        GiaVeCoBan decimal(12, 2) NOT NULL
    );

    INSERT INTO @PhimMoi (MaSuatChieu, MaPhim, MaPhong, ThoiLuong, GiaVeCoBan)
    VALUES
        ('SC101', 'MG001', 'R001', 197, 140000),
        ('SC102', 'MG002', 'R002', 108, 120000),
        ('SC103', 'MG003', 'R001', 101, 100000),
        ('SC104', 'MG004', 'R002', 125, 110000),
        ('SC105', 'MG005', 'R001', 129, 130000),
        ('SC106', 'MG006', 'R002', 155, 125000);

    DECLARE @MaSuatMoi varchar(10);
    DECLARE @MaPhimMoi varchar(10);
    DECLARE @MaPhongMoi varchar(10);
    DECLARE @ThoiLuongMoi smallint;
    DECLARE @GiaVeMoi decimal(12, 2);
    DECLARE @NgayChieuMoi int;
    DECLARE @BatDauMoi datetime2(0);

    DECLARE SuatPhimMoi CURSOR LOCAL FAST_FORWARD FOR
        SELECT source.MaSuatChieu, source.MaPhim, source.MaPhong, source.ThoiLuong, source.GiaVeCoBan
        FROM @PhimMoi AS source
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.SUATCHIEU AS existing
            WHERE existing.MaSuatChieu = source.MaSuatChieu
        )
        ORDER BY source.MaSuatChieu;

    OPEN SuatPhimMoi;
    FETCH NEXT FROM SuatPhimMoi
    INTO @MaSuatMoi, @MaPhimMoi, @MaPhongMoi, @ThoiLuongMoi, @GiaVeMoi;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF NOT EXISTS
        (
            SELECT 1 FROM dbo.PHONGCHIEU
            WHERE MaPhong = @MaPhongMoi AND TrangThai = N'Hoạt động'
        )
            THROW 51055, N'Phòng mẫu cần tạo lịch chiếu không hoạt động.', 1;

        IF EXISTS (SELECT 1 FROM dbo.SUATCHIEU WHERE MaSuatChieu = @MaSuatMoi)
            THROW 51056, N'Mã suất chiếu SC101-SC106 đã được dùng cho suất khác.', 1;

        SET @NgayChieuMoi = 1;
        SET @BatDauMoi = DATEADD(hour, 13, CAST(DATEADD(day, @NgayChieuMoi, CONVERT(date, GETDATE())) AS datetime2(0)));

        WHILE EXISTS
        (
            SELECT 1
            FROM dbo.SUATCHIEU
            WHERE MaPhong = @MaPhongMoi
              AND ThoiGianBatDau < DATEADD(minute, @ThoiLuongMoi, @BatDauMoi)
              AND @BatDauMoi < ThoiGianKetThuc
        )
        BEGIN
            SET @NgayChieuMoi += 1;
            SET @BatDauMoi = DATEADD(hour, 13, CAST(DATEADD(day, @NgayChieuMoi, CONVERT(date, GETDATE())) AS datetime2(0)));
        END;

        INSERT INTO dbo.SUATCHIEU
            (MaSuatChieu, MaPhim, MaPhong, ThoiGianBatDau, ThoiGianKetThuc, GiaVeCoBan, TrangThai)
        VALUES
            (@MaSuatMoi, @MaPhimMoi, @MaPhongMoi, @BatDauMoi, DATEADD(minute, @ThoiLuongMoi, @BatDauMoi), @GiaVeMoi, N'Sắp chiếu');

        FETCH NEXT FROM SuatPhimMoi
        INTO @MaSuatMoi, @MaPhimMoi, @MaPhongMoi, @ThoiLuongMoi, @GiaVeMoi;
    END;

    CLOSE SuatPhimMoi;
    DEALLOCATE SuatPhimMoi;

    INSERT INTO dbo.GIAVE_SUAT_HANG (MaSuatChieu, LoaiGhe, GiaVe)
    SELECT source.MaSuatChieu, tiers.LoaiGhe,
           CASE tiers.LoaiGhe WHEN N'Thường' THEN source.GiaVeCoBan
                              WHEN N'VIP' THEN ROUND(source.GiaVeCoBan * 1.25, 0)
                              ELSE ROUND(source.GiaVeCoBan * 1.50, 0) END
    FROM @PhimMoi AS source
    JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = source.MaSuatChieu
    CROSS JOIN (VALUES (N'Thường'), (N'VIP'), (N'Đôi')) AS tiers(LoaiGhe)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.GIAVE_SUAT_HANG AS existing
        WHERE existing.MaSuatChieu = source.MaSuatChieu
          AND existing.LoaiGhe = tiers.LoaiGhe
    );

    COMMIT TRANSACTION;
    PRINT N'Đã thêm tối đa 6 phim và lịch chiếu mẫu MovieGo.';
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'SuatPhimMoi') >= 0
        CLOSE SuatPhimMoi;
    IF CURSOR_STATUS('local', 'SuatPhimMoi') > -3
        DEALLOCATE SuatPhimMoi;
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
