USE MovieGo;
GO

SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF COL_LENGTH(N'dbo.SUATCHIEU', N'KhoaGheBienVaHangDau') IS NULL
BEGIN
    ALTER TABLE dbo.SUATCHIEU
    ADD KhoaGheBienVaHangDau bit NOT NULL
        CONSTRAINT DF_SUATCHIEU_KhoaGheBienVaHangDau DEFAULT (0);
END;
GO

IF OBJECT_ID(N'dbo.GIAVE_SUAT_HANG', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GIAVE_SUAT_HANG
    (
        MaSuatChieu varchar(10) NOT NULL,
        LoaiGhe nvarchar(30) NOT NULL,
        GiaVe decimal(12, 2) NOT NULL,
        CONSTRAINT PK_GIAVE_SUAT_HANG PRIMARY KEY (MaSuatChieu, LoaiGhe),
        CONSTRAINT FK_GIAVE_SUAT_HANG_SUATCHIEU FOREIGN KEY (MaSuatChieu) REFERENCES dbo.SUATCHIEU(MaSuatChieu),
        CONSTRAINT CK_GIAVE_SUAT_HANG_Loai CHECK (LoaiGhe IN (N'Thường', N'VIP', N'Đôi')),
        CONSTRAINT CK_GIAVE_SUAT_HANG_Gia CHECK (GiaVe >= 0)
    );
END;
GO

UPDATE dbo.GHE
SET LoaiGhe = CASE
    WHEN LoaiGhe IN (N'VIP', N'Đôi') THEN LoaiGhe
    ELSE N'Thường'
END
WHERE LoaiGhe NOT IN (N'Thường', N'VIP', N'Đôi');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_GHE_LoaiGhe')
BEGIN
    ALTER TABLE dbo.GHE WITH CHECK
    ADD CONSTRAINT CK_GHE_LoaiGhe CHECK (LoaiGhe IN (N'Thường', N'VIP', N'Đôi'));
END;
GO

;WITH Tiers AS
(
    SELECT LoaiGhe, PriceFactor
    FROM (VALUES
        (N'Thường', CONVERT(decimal(5,2), 1.00)),
        (N'VIP', CONVERT(decimal(5,2), 1.25)),
        (N'Đôi', CONVERT(decimal(5,2), 1.50))
    ) AS tier(LoaiGhe, PriceFactor)
)
INSERT dbo.GIAVE_SUAT_HANG(MaSuatChieu, LoaiGhe, GiaVe)
SELECT sc.MaSuatChieu, tier.LoaiGhe, ROUND(sc.GiaVeCoBan * tier.PriceFactor, 0)
FROM dbo.SUATCHIEU AS sc
CROSS JOIN Tiers AS tier
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.GIAVE_SUAT_HANG AS existing
    WHERE existing.MaSuatChieu = sc.MaSuatChieu
      AND existing.LoaiGhe = tier.LoaiGhe
);
GO

;WITH RowDefinitions AS
(
    SELECT RoomId, SeatRow
    FROM (VALUES
        ('R001','A'),('R001','B'),('R001','C'),('R001','D'),('R001','E'),
        ('R001','F'),('R001','G'),('R001','H'),('R001','I'),('R001','J'),
        ('R002','A'),('R002','B'),('R002','C'),('R002','D'),('R002','E'),
        ('R002','F'),('R002','G'),('R002','H'),('R002','I'),('R002','J')
    ) AS rows(RoomId, SeatRow)
),
SeatNumbers AS
(
    SELECT SeatNumber
    FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS numbers(SeatNumber)
),
Candidates AS
(
    SELECT rows.RoomId, rows.SeatRow, numbers.SeatNumber,
           CASE WHEN rows.SeatRow = 'J' THEN N'Đôi'
                WHEN rows.SeatRow IN ('E','F','G','H','I') THEN N'VIP'
                ELSE N'Thường' END AS SeatType,
           ROW_NUMBER() OVER (PARTITION BY rows.RoomId ORDER BY rows.SeatRow, numbers.SeatNumber) AS SequenceNumber
    FROM RowDefinitions AS rows
    CROSS JOIN SeatNumbers AS numbers
    WHERE EXISTS (SELECT 1 FROM dbo.PHONGCHIEU room WHERE room.MaPhong = rows.RoomId)
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.GHE AS existing
          WHERE existing.MaPhong = rows.RoomId
            AND existing.HangGhe = rows.SeatRow
            AND existing.SoGhe = numbers.SeatNumber
      )
),
RequiredSeats AS
(
    SELECT candidate.*
    FROM Candidates AS candidate
    JOIN
    (
        SELECT MaPhong, COUNT(*) AS CurrentSeats
        FROM dbo.GHE
        GROUP BY MaPhong
    ) AS capacity ON capacity.MaPhong = candidate.RoomId
    WHERE candidate.SequenceNumber <= 100 - capacity.CurrentSeats
),
NumberedSeats AS
(
    SELECT RoomId, SeatRow, SeatNumber, SeatType,
           COALESCE((SELECT MAX(TRY_CONVERT(int, SUBSTRING(MaGhe, 2, 9)))
                     FROM dbo.GHE), 0)
           + ROW_NUMBER() OVER (ORDER BY RoomId, SeatRow, SeatNumber) AS SeatIdNumber
    FROM RequiredSeats
)
INSERT dbo.GHE(MaGhe, MaPhong, HangGhe, SoGhe, LoaiGhe, TrangThai)
SELECT 'G' + RIGHT('000000000' + CONVERT(varchar(9), SeatIdNumber), 9),
       RoomId, SeatRow, SeatNumber, SeatType, N'Hoạt động'
FROM NumberedSeats;
GO

UPDATE seat
SET LoaiGhe = CASE WHEN seat.HangGhe = 'J' THEN N'Đôi'
                   WHEN seat.HangGhe IN ('E','F','G','H','I') THEN N'VIP'
                   ELSE N'Thường' END
FROM dbo.GHE AS seat
WHERE seat.MaPhong IN ('R001','R002')
  AND NOT EXISTS (SELECT 1 FROM dbo.VE AS ticket WHERE ticket.MaGhe = seat.MaGhe);
GO

CREATE OR ALTER PROCEDURE dbo.usp_TraVe
    @MaVe varchar(10),
    @MaNV varchar(10) = NULL,
    @LyDo nvarchar(300) = N'Khách yêu cầu trả vé'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @LoaiGhe nvarchar(30);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @LoaiGhe=g.LoaiGhe
        FROM dbo.VE AS v WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.GHE AS g ON g.MaGhe=v.MaGhe
        WHERE v.MaVe=@MaVe;

        IF @LoaiGhe=N'Đôi'
            THROW 51035, N'Ghế đôi chỉ được trả theo cả cặp.', 1;

        INSERT dbo.XULYVE(LoaiXL,MaVeGoc,MaNV,ThoiGianXL,LyDo,PhiXuLy,SoTienHoan,MaPTTT)
        SELECT N'Trả',v.MaVe,@MaNV,SYSDATETIME(),@LyDo,
               ROUND(v.GiaBan*0.10,2),v.GiaBan-ROUND(v.GiaBan*0.10,2),h.MaPTTT
        FROM dbo.VE AS v WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.HOADON AS h ON h.MaHD=v.MaHD
        WHERE v.MaVe=@MaVe;

        IF @@ROWCOUNT=0
            THROW 51012, N'Không tìm thấy vé cần trả.', 1;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_DoiVe
    @MaVeGoc varchar(10),
    @MaSuatChieuMoi varchar(10),
    @MaGheMoi varchar(10),
    @MaVeMoi varchar(10),
    @MaPTTTChenhlech varchar(10) = NULL,
    @MaNV varchar(10) = NULL,
    @LyDo nvarchar(300) = N'Khách yêu cầu đổi suất chiếu'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @MaHD varchar(10), @MaKenh varchar(10), @MaSuatChieuCu varchar(10);
    DECLARE @GiaVeCu decimal(12,2), @GiaVeMoi decimal(12,2), @GiaBanMoi decimal(12,2);
    DECLARE @MaPhongMoi varchar(10), @ThoiGianBatDauCu datetime2(0), @ThoiGianBatDauMoi datetime2(0);
    DECLARE @GheDoiGoc bit = 0, @GheDoiMoi bit = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @MaHD=v.MaHD, @MaSuatChieuCu=v.MaSuatChieu, @GiaVeCu=v.GiaBan,
               @GheDoiGoc=CASE WHEN g.LoaiGhe=N'Đôi' THEN 1 ELSE 0 END,
               @ThoiGianBatDauCu=sc.ThoiGianBatDau
        FROM dbo.VE AS v WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.HOADON AS h WITH (UPDLOCK,HOLDLOCK) ON h.MaHD=v.MaHD
        JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu=v.MaSuatChieu
        JOIN dbo.GHE AS g ON g.MaGhe=v.MaGhe
        WHERE v.MaVe=@MaVeGoc AND v.TrangThai=N'Đã bán' AND h.TrangThai=N'Đã thanh toán';

        IF @MaHD IS NULL THROW 51020, N'Vé gốc không tồn tại, chưa thanh toán hoặc không còn ở trạng thái đã bán.', 1;
        IF EXISTS (SELECT 1 FROM dbo.XULYVE WITH (UPDLOCK,HOLDLOCK) WHERE MaVeGoc=@MaVeGoc OR MaVeMoi=@MaVeGoc)
            THROW 51021, N'Vé đã được xử lý hoặc là vé phát sinh từ lần đổi trước.', 1;
        IF @GheDoiGoc=1 THROW 51033, N'Vé ghế đôi phải được đổi theo cả cặp.', 1;
        IF SYSDATETIME()>DATEADD(hour,-2,@ThoiGianBatDauCu)
            THROW 51022, N'Chỉ được đổi vé trước giờ chiếu ít nhất 2 giờ.', 1;

        SELECT @MaKenh=h.MaKenh FROM dbo.HOADON AS h WHERE h.MaHD=@MaHD;

        SELECT @MaPhongMoi=sc.MaPhong,
               @GiaVeMoi=COALESCE(gv.GiaVe,sc.GiaVeCoBan),
               @GheDoiMoi=CASE WHEN g.LoaiGhe=N'Đôi' THEN 1 ELSE 0 END,
               @ThoiGianBatDauMoi=sc.ThoiGianBatDau
        FROM dbo.SUATCHIEU AS sc WITH (UPDLOCK,HOLDLOCK)
        LEFT JOIN dbo.GHE AS g ON g.MaGhe=@MaGheMoi
        LEFT JOIN dbo.GIAVE_SUAT_HANG AS gv ON gv.MaSuatChieu=sc.MaSuatChieu AND gv.LoaiGhe=g.LoaiGhe
        WHERE sc.MaSuatChieu=@MaSuatChieuMoi AND sc.TrangThai=N'Sắp chiếu';
        IF @MaPhongMoi IS NULL THROW 51023, N'Suất chiếu mới không tồn tại hoặc không còn mở bán.', 1;
        IF @MaSuatChieuMoi=@MaSuatChieuCu OR @ThoiGianBatDauMoi<=SYSDATETIME()
            THROW 51024, N'Suất chiếu mới phải khác suất cũ và chưa bắt đầu.', 1;
        IF @GheDoiMoi=1 THROW 51034, N'Đổi vé sang ghế đôi hiện cần đổi cả cặp vé.', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM dbo.GHE AS g WITH (UPDLOCK,HOLDLOCK)
            WHERE g.MaGhe=@MaGheMoi AND g.MaPhong=@MaPhongMoi AND g.TrangThai=N'Hoạt động'
        ) THROW 51025, N'Ghế mới không tồn tại, không hoạt động hoặc không thuộc phòng của suất chiếu mới.', 1;

        IF EXISTS
        (
            SELECT 1 FROM dbo.VE WITH (UPDLOCK,HOLDLOCK)
            WHERE MaSuatChieu=@MaSuatChieuMoi AND MaGhe=@MaGheMoi
              AND TrangThai IN (N'Đang giữ chỗ',N'Đã bán')
        ) THROW 51026, N'Ghế đã được giữ chỗ hoặc bán cho suất chiếu mới.', 1;

        SET @GiaBanMoi=CASE WHEN @GiaVeMoi>@GiaVeCu THEN @GiaVeMoi ELSE @GiaVeCu END;
        IF @GiaBanMoi>@GiaVeCu
        BEGIN
            IF @MaPTTTChenhlech IS NULL OR NOT EXISTS
               (SELECT 1 FROM dbo.KENH_PTTT WHERE MaKenh=@MaKenh AND MaPTTT=@MaPTTTChenhlech)
                THROW 51027, N'Phải chọn hình thức thanh toán hợp lệ cho khoản chênh lệch.', 1;
        END
        ELSE IF @MaPTTTChenhlech IS NOT NULL
            THROW 51028, N'Không nhập hình thức thanh toán chênh lệch khi không phát sinh khoản thu thêm.', 1;

        INSERT dbo.VE(MaVe,MaHD,MaSuatChieu,MaGhe,GiaBan,ThoiGianBan,TrangThai)
        VALUES(@MaVeMoi,@MaHD,@MaSuatChieuMoi,@MaGheMoi,@GiaBanMoi,SYSDATETIME(),N'Đã bán');
        INSERT dbo.XULYVE(LoaiXL,MaVeGoc,MaVeMoi,MaNV,ThoiGianXL,LyDo,PhiXuLy,SoTienHoan,MaPTTT)
        VALUES(N'Đổi',@MaVeGoc,@MaVeMoi,@MaNV,SYSDATETIME(),@LyDo,0,0,@MaPTTTChenhlech);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

PRINT N'Đã nâng cấp MovieGo: hạng ghế, giá theo suất, 100 ghế/phòng và quy trình đổi vé hỗ trợ giá hạng.';
GO
