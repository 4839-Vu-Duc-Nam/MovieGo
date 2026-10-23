/*
    MovieGo - SQL Server schema and sample data
    Run this script once in SQL Server Management Studio or sqlcmd.
    Business rules that span several tables are enforced with triggers
    and stored procedures below.
*/

IF DB_ID(N'MovieGo') IS NULL
    EXEC(N'CREATE DATABASE MovieGo');
GO

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

CREATE TABLE dbo.PHIM
(
    MaPhim          varchar(10)     NOT NULL CONSTRAINT PK_PHIM PRIMARY KEY,
    TenPhim         nvarchar(200)   NOT NULL,
    ThoiLuong       smallint        NOT NULL,
    NgonNgu         nvarchar(50)    NOT NULL,
    GioiHanTuoi     varchar(10)     NOT NULL,
    NgayKhoiChieu   date            NULL,
    TrangThai       nvarchar(20)    NOT NULL,
    CONSTRAINT CK_PHIM_ThoiLuong CHECK (ThoiLuong > 0),
    CONSTRAINT CK_PHIM_TrangThai CHECK (TrangThai IN (N'Sắp chiếu', N'Đang chiếu', N'Ngừng chiếu'))
);
GO

CREATE TABLE dbo.THELOAI
(
    MaTheLoai  varchar(10)     NOT NULL CONSTRAINT PK_THELOAI PRIMARY KEY,
    TenTheLoai nvarchar(100)   NOT NULL,
    MoTa       nvarchar(500)   NULL
);
GO

CREATE TABLE dbo.PHIM_THELOAI
(
    MaPhim     varchar(10) NOT NULL,
    MaTheLoai  varchar(10) NOT NULL,
    CONSTRAINT PK_PHIM_THELOAI PRIMARY KEY (MaPhim, MaTheLoai),
    CONSTRAINT FK_PHIM_THELOAI_PHIM FOREIGN KEY (MaPhim) REFERENCES dbo.PHIM(MaPhim),
    CONSTRAINT FK_PHIM_THELOAI_THELOAI FOREIGN KEY (MaTheLoai) REFERENCES dbo.THELOAI(MaTheLoai)
);
GO

CREATE TABLE dbo.PHONGCHIEU
(
    MaPhong    varchar(10)     NOT NULL CONSTRAINT PK_PHONGCHIEU PRIMARY KEY,
    TenPhong   nvarchar(100)   NOT NULL,
    LoaiPhong  nvarchar(50)    NOT NULL,
    TrangThai  nvarchar(20)    NOT NULL,
    CONSTRAINT CK_PHONGCHIEU_TrangThai CHECK (TrangThai IN (N'Hoạt động', N'Bảo trì', N'Ngừng hoạt động'))
);
GO

CREATE TABLE dbo.GHE
(
    MaGhe     varchar(10)     NOT NULL CONSTRAINT PK_GHE PRIMARY KEY,
    MaPhong   varchar(10)     NOT NULL,
    HangGhe   varchar(5)      NOT NULL,
    SoGhe     smallint        NOT NULL,
    LoaiGhe   nvarchar(30)    NOT NULL,
    TrangThai nvarchar(20)    NOT NULL,
    CONSTRAINT UQ_GHE_ViTri UNIQUE (MaPhong, HangGhe, SoGhe),
    CONSTRAINT FK_GHE_PHONG FOREIGN KEY (MaPhong) REFERENCES dbo.PHONGCHIEU(MaPhong),
    CONSTRAINT CK_GHE_SoGhe CHECK (SoGhe > 0),
    CONSTRAINT CK_GHE_TrangThai CHECK (TrangThai IN (N'Hoạt động', N'Ngừng hoạt động')),
    CONSTRAINT CK_GHE_LoaiGhe CHECK (LoaiGhe IN (N'Thường', N'VIP', N'Đôi'))
);
GO

CREATE TABLE dbo.NHANVIEN
(
    MaNV         varchar(10)     NOT NULL CONSTRAINT PK_NHANVIEN PRIMARY KEY,
    HoTen        nvarchar(150)   NOT NULL,
    SoDienThoai  varchar(20)     NULL,
    Email        varchar(254)    NULL,
    ChucVu       nvarchar(50)    NOT NULL,
    TrangThai    nvarchar(20)    NOT NULL,
    CONSTRAINT CK_NHANVIEN_TrangThai CHECK (TrangThai IN (N'Đang làm', N'Ngừng làm'))
);
GO

CREATE TABLE dbo.KHACHHANG
(
    MaKH         varchar(10)     NOT NULL CONSTRAINT PK_KHACHHANG PRIMARY KEY,
    HoTen        nvarchar(150)   NOT NULL,
    SoDienThoai  varchar(20)     NULL,
    Email        varchar(254)    NULL,
    NgaySinh     date            NULL
);
GO

CREATE TABLE dbo.TAIKHOAN
(
    AccountId       uniqueidentifier NOT NULL CONSTRAINT PK_TAIKHOAN PRIMARY KEY,
    Email           nvarchar(254)    NOT NULL,
    NormalizedEmail nvarchar(254)    NOT NULL,
    PasswordHash    nvarchar(500)    NOT NULL,
    Role            nvarchar(20)     NOT NULL,
    MaKH            varchar(10)      NULL,
    IsActive        bit              NOT NULL CONSTRAINT DF_TAIKHOAN_IsActive DEFAULT (1),
    CreatedAt       datetime2(0)     NOT NULL CONSTRAINT DF_TAIKHOAN_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_TAIKHOAN_NormalizedEmail UNIQUE (NormalizedEmail),
    CONSTRAINT UQ_TAIKHOAN_MaKH UNIQUE (MaKH),
    CONSTRAINT FK_TAIKHOAN_KHACHHANG FOREIGN KEY (MaKH) REFERENCES dbo.KHACHHANG(MaKH),
    CONSTRAINT CK_TAIKHOAN_Role CHECK
        ((Role = N'Customer' AND MaKH IS NOT NULL) OR (Role = N'Admin' AND MaKH IS NULL))
);
GO

CREATE TABLE dbo.PHUONGTHUCTT
(
    MaPTTT    varchar(10)     NOT NULL CONSTRAINT PK_PHUONGTHUCTT PRIMARY KEY,
    TenPTTT   nvarchar(100)   NOT NULL,
    TrangThai nvarchar(20)    NOT NULL,
    CONSTRAINT CK_PHUONGTHUCTT_TrangThai CHECK (TrangThai IN (N'Đang hoạt động', N'Ngừng hoạt động'))
);
GO

CREATE TABLE dbo.KENHBAN
(
    MaKenh    varchar(10)     NOT NULL CONSTRAINT PK_KENHBAN PRIMARY KEY,
    TenKenh   nvarchar(100)   NOT NULL,
    LoaiKenh  varchar(10)     NOT NULL,
    MoTa      nvarchar(500)   NULL,
    TrangThai nvarchar(20)    NOT NULL,
    CONSTRAINT UQ_KENHBAN_MaKenh_LoaiKenh UNIQUE (MaKenh, LoaiKenh),
    CONSTRAINT CK_KENHBAN_LoaiKenh CHECK (LoaiKenh IN ('QUAY', 'ONLINE')),
    CONSTRAINT CK_KENHBAN_TrangThai CHECK (TrangThai IN (N'Đang hoạt động', N'Ngừng hoạt động'))
);
GO

CREATE TABLE dbo.KENH_PTTT
(
    MaKenh  varchar(10) NOT NULL,
    MaPTTT  varchar(10) NOT NULL,
    CONSTRAINT PK_KENH_PTTT PRIMARY KEY (MaKenh, MaPTTT),
    CONSTRAINT FK_KENH_PTTT_KENHBAN FOREIGN KEY (MaKenh) REFERENCES dbo.KENHBAN(MaKenh),
    CONSTRAINT FK_KENH_PTTT_PTTT FOREIGN KEY (MaPTTT) REFERENCES dbo.PHUONGTHUCTT(MaPTTT)
);
GO

CREATE TABLE dbo.SUATCHIEU
(
    MaSuatChieu       varchar(10)     NOT NULL CONSTRAINT PK_SUATCHIEU PRIMARY KEY,
    MaPhim            varchar(10)     NOT NULL,
    MaPhong           varchar(10)     NOT NULL,
    ThoiGianBatDau    datetime2(0)    NOT NULL,
    ThoiGianKetThuc   datetime2(0)    NOT NULL,
    GiaVeCoBan        decimal(12, 2)  NOT NULL,
    KhoaGheBienVaHangDau bit          NOT NULL CONSTRAINT DF_SUATCHIEU_KhoaGheBienVaHangDau DEFAULT (0),
    TrangThai         nvarchar(20)    NOT NULL,
    CONSTRAINT FK_SUATCHIEU_PHIM FOREIGN KEY (MaPhim) REFERENCES dbo.PHIM(MaPhim),
    CONSTRAINT FK_SUATCHIEU_PHONG FOREIGN KEY (MaPhong) REFERENCES dbo.PHONGCHIEU(MaPhong),
    CONSTRAINT CK_SUATCHIEU_ThoiGian CHECK (ThoiGianKetThuc > ThoiGianBatDau),
    CONSTRAINT CK_SUATCHIEU_Gia CHECK (GiaVeCoBan >= 0),
    CONSTRAINT CK_SUATCHIEU_TrangThai CHECK (TrangThai IN (N'Sắp chiếu', N'Đang chiếu', N'Đã chiếu', N'Đã hủy'))
);
GO

CREATE INDEX IX_SUATCHIEU_Phong_ThoiGian
    ON dbo.SUATCHIEU (MaPhong, ThoiGianBatDau, ThoiGianKetThuc);
GO

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
GO

CREATE TABLE dbo.HOADON
(
    MaHD         varchar(10)     NOT NULL CONSTRAINT PK_HOADON PRIMARY KEY,
    MaNV         varchar(10)     NULL,
    MaKH         varchar(10)     NULL,
    MaKenh       varchar(10)     NOT NULL,
    MaPTTT       varchar(10)     NOT NULL,
    ThoiGianLap  datetime2(0)    NOT NULL CONSTRAINT DF_HOADON_ThoiGianLap DEFAULT (SYSDATETIME()),
    TrangThai    nvarchar(20)    NOT NULL,
    CONSTRAINT FK_HOADON_NHANVIEN FOREIGN KEY (MaNV) REFERENCES dbo.NHANVIEN(MaNV),
    CONSTRAINT FK_HOADON_KHACHHANG FOREIGN KEY (MaKH) REFERENCES dbo.KHACHHANG(MaKH),
    CONSTRAINT FK_HOADON_KENH_PTTT FOREIGN KEY (MaKenh, MaPTTT) REFERENCES dbo.KENH_PTTT(MaKenh, MaPTTT),
    CONSTRAINT CK_HOADON_TrangThai CHECK (TrangThai IN (N'Chờ thanh toán', N'Đã thanh toán', N'Đã hủy'))
);
GO

CREATE TABLE dbo.VE
(
    MaVe          varchar(10)     NOT NULL CONSTRAINT PK_VE PRIMARY KEY,
    MaHD          varchar(10)     NOT NULL,
    MaSuatChieu   varchar(10)     NOT NULL,
    MaGhe         varchar(10)     NOT NULL,
    GiaBan        decimal(12, 2)  NOT NULL,
    ThoiGianTao   datetime2(0)    NOT NULL CONSTRAINT DF_VE_ThoiGianTao DEFAULT (SYSDATETIME()),
    ThoiGianBan   datetime2(0)    NULL,
    TrangThai     nvarchar(20)    NOT NULL,
    CONSTRAINT FK_VE_HOADON FOREIGN KEY (MaHD) REFERENCES dbo.HOADON(MaHD),
    CONSTRAINT FK_VE_SUATCHIEU FOREIGN KEY (MaSuatChieu) REFERENCES dbo.SUATCHIEU(MaSuatChieu),
    CONSTRAINT FK_VE_GHE FOREIGN KEY (MaGhe) REFERENCES dbo.GHE(MaGhe),
    CONSTRAINT CK_VE_GiaBan CHECK (GiaBan >= 0),
    CONSTRAINT CK_VE_TrangThai CHECK (TrangThai IN (N'Đang giữ chỗ', N'Đã bán', N'Đã hủy', N'Đã trả', N'Đã đổi')),
    CONSTRAINT CK_VE_ThoiGianBan CHECK (TrangThai <> N'Đã bán' OR ThoiGianBan IS NOT NULL)
);
GO

CREATE UNIQUE INDEX UX_VE_Ghe_ConHieuLuc
    ON dbo.VE (MaSuatChieu, MaGhe)
    WHERE TrangThai IN (N'Đang giữ chỗ', N'Đã bán');
GO

CREATE TABLE dbo.XULYVE
(
    MaXL          int             IDENTITY(1, 1) NOT NULL CONSTRAINT PK_XULYVE PRIMARY KEY,
    LoaiXL        nvarchar(10)    NOT NULL,
    MaVeGoc       varchar(10)     NOT NULL,
    MaVeMoi       varchar(10)     NULL,
    MaNV          varchar(10)     NULL,
    ThoiGianXL    datetime2(0)    NOT NULL CONSTRAINT DF_XULYVE_ThoiGianXL DEFAULT (SYSDATETIME()),
    LyDo          nvarchar(300)   NOT NULL,
    PhiXuLy       decimal(12, 2)  NOT NULL CONSTRAINT DF_XULYVE_PhiXuLy DEFAULT (0),
    SoTienHoan    decimal(12, 2)  NOT NULL CONSTRAINT DF_XULYVE_SoTienHoan DEFAULT (0),
    MaPTTT        varchar(10)     NULL,
    CONSTRAINT UQ_XULYVE_MaVeGoc UNIQUE (MaVeGoc),
    CONSTRAINT FK_XULYVE_VE_GOC FOREIGN KEY (MaVeGoc) REFERENCES dbo.VE(MaVe),
    CONSTRAINT FK_XULYVE_VE_MOI FOREIGN KEY (MaVeMoi) REFERENCES dbo.VE(MaVe),
    CONSTRAINT FK_XULYVE_NHANVIEN FOREIGN KEY (MaNV) REFERENCES dbo.NHANVIEN(MaNV),
    CONSTRAINT FK_XULYVE_PTTT FOREIGN KEY (MaPTTT) REFERENCES dbo.PHUONGTHUCTT(MaPTTT),
    CONSTRAINT CK_XULYVE_Loai CHECK (LoaiXL IN (N'Hủy', N'Trả', N'Đổi')),
    CONSTRAINT CK_XULYVE_Phi CHECK (PhiXuLy >= 0),
    CONSTRAINT CK_XULYVE_Hoan CHECK (SoTienHoan >= 0),
    CONSTRAINT CK_XULYVE_VeMoi CHECK
        ((LoaiXL = N'Đổi' AND MaVeMoi IS NOT NULL) OR (LoaiXL IN (N'Hủy', N'Trả') AND MaVeMoi IS NULL))
);
GO

CREATE UNIQUE INDEX UX_XULYVE_MaVeMoi
    ON dbo.XULYVE (MaVeMoi)
    WHERE MaVeMoi IS NOT NULL;
GO

CREATE INDEX IX_VE_HoaDon_TrangThai ON dbo.VE (MaHD, TrangThai);
CREATE INDEX IX_VE_SuatChieu_TrangThai ON dbo.VE (MaSuatChieu, TrangThai);
GO

CREATE TRIGGER dbo.TR_SUATCHIEU_KhongChongLich
ON dbo.SUATCHIEU
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        JOIN dbo.SUATCHIEU AS s WITH (UPDLOCK, HOLDLOCK)
          ON s.MaPhong = i.MaPhong
         AND s.MaSuatChieu <> i.MaSuatChieu
         AND i.ThoiGianBatDau < s.ThoiGianKetThuc
         AND s.ThoiGianBatDau < i.ThoiGianKetThuc
    )
       OR EXISTS
    (
        SELECT 1
        FROM inserted AS i
        JOIN inserted AS j
          ON j.MaPhong = i.MaPhong
         AND j.MaSuatChieu > i.MaSuatChieu
         AND i.ThoiGianBatDau < j.ThoiGianKetThuc
         AND j.ThoiGianBatDau < i.ThoiGianKetThuc
    )
    BEGIN
        ;THROW 51001, N'Lịch chiếu trong cùng phòng không được chồng lấn.', 1;
    END;
END;
GO

CREATE TRIGGER dbo.TR_HOADON_KenhNhanVien
ON dbo.HOADON
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        JOIN dbo.KENHBAN AS k ON k.MaKenh = i.MaKenh
        WHERE (k.LoaiKenh = 'QUAY' AND i.MaNV IS NULL)
           OR (k.LoaiKenh = 'ONLINE' AND i.MaNV IS NOT NULL)
    )
    BEGIN
        ;THROW 51002, N'Hóa đơn tại quầy phải có nhân viên; hóa đơn online không gắn nhân viên.', 1;
    END;
END;
GO

CREATE TRIGGER dbo.TR_VE_KiemTraGheVaHoaDon
ON dbo.VE
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = i.MaSuatChieu
        JOIN dbo.GHE AS g ON g.MaGhe = i.MaGhe
        WHERE sc.MaPhong <> g.MaPhong
    )
    BEGIN
        ;THROW 51003, N'Ghế phải thuộc phòng chiếu của suất chiếu được chọn.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        JOIN dbo.HOADON AS h ON h.MaHD = i.MaHD
        JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = i.MaSuatChieu
        WHERE (i.TrangThai = N'Đang giữ chỗ' AND h.TrangThai <> N'Chờ thanh toán')
           OR (i.TrangThai = N'Đã bán' AND h.TrangThai <> N'Đã thanh toán')
           OR (i.TrangThai IN (N'Đang giữ chỗ', N'Đã bán') AND sc.TrangThai = N'Đã hủy')
    )
    BEGIN
        ;THROW 51004, N'Trạng thái vé phải phù hợp với hóa đơn và suất chiếu.', 1;
    END;
END;
GO

CREATE TRIGGER dbo.TR_XULYVE_KiemTraNghiepVu
ON dbo.XULYVE
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS x
        JOIN dbo.VE AS vg ON vg.MaVe = x.MaVeGoc
        JOIN dbo.HOADON AS h ON h.MaHD = vg.MaHD
        JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = vg.MaSuatChieu
        WHERE x.ThoiGianXL > CONVERT(datetime2(0), SYSDATETIME())
           OR x.SoTienHoan > vg.GiaBan
           OR
           (
               x.LoaiXL = N'Hủy'
               AND
               (
                   (vg.TrangThai = N'Đã bán'
                    AND sc.TrangThai = N'Đã hủy'
                    AND (x.PhiXuLy <> 0
                         OR x.SoTienHoan <> vg.GiaBan
                         OR x.MaPTTT IS NULL
                         OR x.MaPTTT <> h.MaPTTT))
                   OR
                   ((vg.TrangThai <> N'Đã bán' OR sc.TrangThai <> N'Đã hủy')
                    AND (vg.TrangThai <> N'Đang giữ chỗ'
                         OR h.TrangThai <> N'Chờ thanh toán'
                         OR x.PhiXuLy <> 0
                         OR x.SoTienHoan <> 0
                         OR x.MaPTTT IS NOT NULL))
               )
           )
           OR
           (
               x.LoaiXL = N'Trả'
               AND
               (
                   vg.TrangThai <> N'Đã bán'
                   OR h.TrangThai <> N'Đã thanh toán'
                   OR x.ThoiGianXL > DATEADD(hour, -2, sc.ThoiGianBatDau)
                   OR x.PhiXuLy <> ROUND(vg.GiaBan * 0.10, 2)
                   OR x.SoTienHoan <> vg.GiaBan - ROUND(vg.GiaBan * 0.10, 2)
                   OR x.MaPTTT IS NULL
                   OR x.MaPTTT <> h.MaPTTT
               )
           )
           OR
           (
               x.LoaiXL = N'Đổi'
               AND
               (
                   vg.TrangThai <> N'Đã bán'
                   OR h.TrangThai <> N'Đã thanh toán'
                   OR x.ThoiGianXL > DATEADD(hour, -2, sc.ThoiGianBatDau)
                   OR x.SoTienHoan <> 0
                   OR x.PhiXuLy <> 0
                   OR EXISTS (SELECT 1 FROM dbo.XULYVE AS oldx WHERE oldx.MaVeMoi = vg.MaVe)
                   OR NOT EXISTS
                   (
                       SELECT 1
                       FROM dbo.VE AS vm
                       JOIN dbo.SUATCHIEU AS scm ON scm.MaSuatChieu = vm.MaSuatChieu
                       WHERE vm.MaVe = x.MaVeMoi
                         AND vm.MaHD = vg.MaHD
                         AND vm.TrangThai = N'Đã bán'
                         AND vm.MaSuatChieu <> vg.MaSuatChieu
                         AND scm.ThoiGianBatDau > x.ThoiGianXL
                         AND vm.GiaBan =
                             CASE WHEN scm.GiaVeCoBan > vg.GiaBan
                                  THEN scm.GiaVeCoBan ELSE vg.GiaBan END
                   )
                   OR
                   (
                       EXISTS
                       (
                           SELECT 1
                           FROM dbo.VE AS vm
                           JOIN dbo.SUATCHIEU AS scm ON scm.MaSuatChieu = vm.MaSuatChieu
                           WHERE vm.MaVe = x.MaVeMoi
                             AND vm.GiaBan > vg.GiaBan
                       )
                       AND x.MaPTTT IS NULL
                   )
                   OR
                   (
                       EXISTS
                       (
                           SELECT 1
                           FROM dbo.VE AS vm
                           WHERE vm.MaVe = x.MaVeMoi
                             AND vm.GiaBan > vg.GiaBan
                       )
                       AND NOT EXISTS
                       (
                           SELECT 1
                           FROM dbo.HOADON AS h
                           JOIN dbo.KENH_PTTT AS kp
                             ON kp.MaKenh = h.MaKenh
                            AND kp.MaPTTT = x.MaPTTT
                           WHERE h.MaHD = vg.MaHD
                       )
                   )
                   OR
                   (
                       EXISTS
                       (
                           SELECT 1
                           FROM dbo.VE AS vm
                           WHERE vm.MaVe = x.MaVeMoi
                             AND vm.GiaBan = vg.GiaBan
                       )
                       AND x.MaPTTT IS NOT NULL
                   )
               )
           )
    )
    BEGIN
        ;THROW 51005, N'Nghiệp vụ hủy, trả hoặc đổi vé không thỏa điều kiện MovieGo.', 1;
    END;

    UPDATE v
       SET TrangThai = CASE x.LoaiXL
                           WHEN N'Hủy' THEN N'Đã hủy'
                           WHEN N'Trả' THEN N'Đã trả'
                           WHEN N'Đổi' THEN N'Đã đổi'
                       END
    FROM dbo.VE AS v
    JOIN inserted AS x ON x.MaVeGoc = v.MaVe;
END;
GO

CREATE TRIGGER dbo.TR_XULYVE_BaoVeLichSu
ON dbo.XULYVE
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    ;THROW 51006, N'Lịch sử xử lý vé là dữ liệu chỉ ghi thêm, không được sửa hoặc xóa.', 1;
END;
GO

CREATE PROCEDURE dbo.usp_ThanhToanHoaDon
    @MaHD varchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.HOADON WITH (UPDLOCK, HOLDLOCK)
            WHERE MaHD = @MaHD AND TrangThai = N'Chờ thanh toán'
        )
        BEGIN
            ;THROW 51010, N'Hóa đơn không tồn tại hoặc không ở trạng thái chờ thanh toán.', 1;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM dbo.VE WITH (UPDLOCK, HOLDLOCK)
            WHERE MaHD = @MaHD
              AND TrangThai = N'Đang giữ chỗ'
              AND ThoiGianTao <= DATEADD(minute, -10, SYSDATETIME())
        )
        BEGIN
            ;THROW 51015, N'Vé giữ chỗ đã hết hạn; hãy hủy giữ chỗ trước khi thanh toán.', 1;
        END;

        IF NOT EXISTS
        (
            SELECT 1 FROM dbo.VE WITH (UPDLOCK, HOLDLOCK)
            WHERE MaHD = @MaHD AND TrangThai = N'Đang giữ chỗ'
        )
        BEGIN
            ;THROW 51011, N'Hóa đơn không có vé đang giữ chỗ để thanh toán.', 1;
        END;

        UPDATE dbo.HOADON SET TrangThai = N'Đã thanh toán' WHERE MaHD = @MaHD;

        UPDATE dbo.VE
           SET TrangThai = N'Đã bán',
               ThoiGianBan = SYSDATETIME()
         WHERE MaHD = @MaHD AND TrangThai = N'Đang giữ chỗ';

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE PROCEDURE dbo.usp_HuyHoaDonChoThanhToan
    @MaHD varchar(10),
    @MaNV varchar(10) = NULL,
    @LyDo nvarchar(300) = N'Khách hủy'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.HOADON WITH (UPDLOCK, HOLDLOCK)
            WHERE MaHD = @MaHD AND TrangThai = N'Chờ thanh toán'
        )
        BEGIN
            ;THROW 51016, N'Hóa đơn không tồn tại hoặc không còn chờ thanh toán.', 1;
        END;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.VE WITH (UPDLOCK, HOLDLOCK)
            WHERE MaHD = @MaHD AND TrangThai = N'Đang giữ chỗ'
        )
        BEGIN
            ;THROW 51017, N'Hóa đơn không còn vé đang giữ chỗ để hủy.', 1;
        END;

        INSERT INTO dbo.XULYVE
            (LoaiXL, MaVeGoc, MaNV, ThoiGianXL, LyDo, PhiXuLy, SoTienHoan)
        SELECT N'Hủy', MaVe, @MaNV, SYSDATETIME(), @LyDo, 0, 0
        FROM dbo.VE WITH (UPDLOCK, HOLDLOCK)
        WHERE MaHD = @MaHD AND TrangThai = N'Đang giữ chỗ';

        UPDATE dbo.HOADON SET TrangThai = N'Đã hủy' WHERE MaHD = @MaHD;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE PROCEDURE dbo.usp_TraVe
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
        FROM dbo.VE AS v WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.GHE AS g ON g.MaGhe=v.MaGhe
        WHERE v.MaVe=@MaVe;

        IF @LoaiGhe=N'Đôi'
            THROW 51035, N'Ghế đôi chỉ được trả theo cả cặp.', 1;

        INSERT INTO dbo.XULYVE
            (LoaiXL, MaVeGoc, MaNV, ThoiGianXL, LyDo, PhiXuLy, SoTienHoan, MaPTTT)
        SELECT N'Trả',
               v.MaVe,
               @MaNV,
               SYSDATETIME(),
               @LyDo,
               ROUND(v.GiaBan * 0.10, 2),
               v.GiaBan - ROUND(v.GiaBan * 0.10, 2),
               h.MaPTTT
        FROM dbo.VE AS v WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.HOADON AS h ON h.MaHD = v.MaHD
        WHERE v.MaVe = @MaVe;

        IF @@ROWCOUNT = 0
        BEGIN
            ;THROW 51012, N'Không tìm thấy vé cần trả.', 1;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE PROCEDURE dbo.usp_DoiVe
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

    DECLARE @MaHD varchar(10);
    DECLARE @MaKenh varchar(10);
    DECLARE @MaSuatChieuCu varchar(10);
    DECLARE @GiaVeCu decimal(12, 2);
    DECLARE @GiaVeMoi decimal(12, 2);
    DECLARE @GiaBanMoi decimal(12, 2);
    DECLARE @MaPhongMoi varchar(10);
    DECLARE @GheDoiGoc bit = 0;
    DECLARE @GheDoiMoi bit = 0;
    DECLARE @ThoiGianBatDauCu datetime2(0);
    DECLARE @ThoiGianBatDauMoi datetime2(0);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @MaHD = v.MaHD,
               @MaSuatChieuCu = v.MaSuatChieu,
               @GiaVeCu = v.GiaBan,
               @GheDoiGoc = CASE WHEN g.LoaiGhe = N'Đôi' THEN 1 ELSE 0 END,
               @ThoiGianBatDauCu = sc.ThoiGianBatDau
        FROM dbo.VE AS v WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.HOADON AS h WITH (UPDLOCK, HOLDLOCK) ON h.MaHD = v.MaHD
        JOIN dbo.SUATCHIEU AS sc ON sc.MaSuatChieu = v.MaSuatChieu
        JOIN dbo.GHE AS g ON g.MaGhe = v.MaGhe
        WHERE v.MaVe = @MaVeGoc
          AND v.TrangThai = N'Đã bán'
          AND h.TrangThai = N'Đã thanh toán';

        IF @MaHD IS NULL
        BEGIN
            ;THROW 51020, N'Vé gốc không tồn tại, chưa thanh toán hoặc không còn ở trạng thái đã bán.', 1;
        END;

        IF @GheDoiGoc = 1
        BEGIN
            ;THROW 51033, N'Vé ghế đôi phải được đổi theo cả cặp.', 1;
        END;

        IF EXISTS (SELECT 1 FROM dbo.XULYVE WITH (UPDLOCK, HOLDLOCK) WHERE MaVeGoc = @MaVeGoc)
           OR EXISTS (SELECT 1 FROM dbo.XULYVE WITH (UPDLOCK, HOLDLOCK) WHERE MaVeMoi = @MaVeGoc)
        BEGIN
            ;THROW 51021, N'Vé đã được xử lý hoặc là vé phát sinh từ lần đổi trước.', 1;
        END;

        IF SYSDATETIME() > DATEADD(hour, -2, @ThoiGianBatDauCu)
        BEGIN
            ;THROW 51022, N'Chỉ được đổi vé trước giờ chiếu ít nhất 2 giờ.', 1;
        END;

        SELECT @MaKenh = h.MaKenh
        FROM dbo.HOADON AS h
        WHERE h.MaHD = @MaHD;

        SELECT @MaPhongMoi = sc.MaPhong,
               @GiaVeMoi = COALESCE(gv.GiaVe, sc.GiaVeCoBan),
               @GheDoiMoi = CASE WHEN g.LoaiGhe = N'Đôi' THEN 1 ELSE 0 END,
               @ThoiGianBatDauMoi = sc.ThoiGianBatDau
        FROM dbo.SUATCHIEU AS sc WITH (UPDLOCK, HOLDLOCK)
        LEFT JOIN dbo.GHE AS g ON g.MaGhe = @MaGheMoi
        LEFT JOIN dbo.GIAVE_SUAT_HANG AS gv
          ON gv.MaSuatChieu = sc.MaSuatChieu AND gv.LoaiGhe = g.LoaiGhe
        WHERE sc.MaSuatChieu = @MaSuatChieuMoi
          AND sc.TrangThai = N'Sắp chiếu';

        IF @MaPhongMoi IS NULL
        BEGIN
            ;THROW 51023, N'Suất chiếu mới không tồn tại hoặc không còn mở bán.', 1;
        END;

        IF @GheDoiMoi = 1
        BEGIN
            ;THROW 51034, N'Đổi vé sang ghế đôi hiện cần đổi cả cặp vé.', 1;
        END;

        IF @MaSuatChieuMoi = @MaSuatChieuCu
           OR @ThoiGianBatDauMoi <= SYSDATETIME()
        BEGIN
            ;THROW 51024, N'Suất chiếu mới phải khác suất cũ và chưa bắt đầu.', 1;
        END;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.GHE AS g WITH (UPDLOCK, HOLDLOCK)
            WHERE g.MaGhe = @MaGheMoi
              AND g.MaPhong = @MaPhongMoi
              AND g.TrangThai = N'Hoạt động'
        )
        BEGIN
            ;THROW 51025, N'Ghế mới không tồn tại, không hoạt động hoặc không thuộc phòng của suất chiếu mới.', 1;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM dbo.VE WITH (UPDLOCK, HOLDLOCK)
            WHERE MaSuatChieu = @MaSuatChieuMoi
              AND MaGhe = @MaGheMoi
              AND TrangThai IN (N'Đang giữ chỗ', N'Đã bán')
        )
        BEGIN
            ;THROW 51026, N'Ghế đã được giữ chỗ hoặc bán cho suất chiếu mới.', 1;
        END;

        SET @GiaBanMoi = CASE WHEN @GiaVeMoi > @GiaVeCu THEN @GiaVeMoi ELSE @GiaVeCu END;

        IF @GiaBanMoi > @GiaVeCu
        BEGIN
            IF @MaPTTTChenhlech IS NULL
               OR NOT EXISTS
               (
                   SELECT 1
                   FROM dbo.KENH_PTTT
                   WHERE MaKenh = @MaKenh
                     AND MaPTTT = @MaPTTTChenhlech
               )
            BEGIN
                ;THROW 51027, N'Phải chọn hình thức thanh toán hợp lệ cho khoản chênh lệch.', 1;
            END;
        END
        ELSE IF @MaPTTTChenhlech IS NOT NULL
        BEGIN
            ;THROW 51028, N'Không nhập hình thức thanh toán chênh lệch khi không phát sinh khoản thu thêm.', 1;
        END;

        INSERT INTO dbo.VE
            (MaVe, MaHD, MaSuatChieu, MaGhe, GiaBan, ThoiGianBan, TrangThai)
        VALUES
            (@MaVeMoi, @MaHD, @MaSuatChieuMoi, @MaGheMoi, @GiaBanMoi, SYSDATETIME(), N'Đã bán');

        INSERT INTO dbo.XULYVE
            (LoaiXL, MaVeGoc, MaVeMoi, MaNV, ThoiGianXL, LyDo, PhiXuLy, SoTienHoan, MaPTTT)
        VALUES
            (N'Đổi', @MaVeGoc, @MaVeMoi, @MaNV, SYSDATETIME(), @LyDo, 0, 0, @MaPTTTChenhlech);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE PROCEDURE dbo.usp_HuySuatChieu
    @MaSuatChieu varchar(10),
    @MaNV varchar(10) = NULL,
    @LyDo nvarchar(300) = N'Rạp hủy suất chiếu'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.SUATCHIEU WITH (UPDLOCK, HOLDLOCK)
            WHERE MaSuatChieu = @MaSuatChieu AND TrangThai <> N'Đã hủy'
        )
        BEGIN
            ;THROW 51013, N'Suất chiếu không tồn tại hoặc đã bị hủy.', 1;
        END;

        UPDATE dbo.SUATCHIEU
           SET TrangThai = N'Đã hủy'
         WHERE MaSuatChieu = @MaSuatChieu;

        INSERT INTO dbo.XULYVE
            (LoaiXL, MaVeGoc, MaNV, ThoiGianXL, LyDo, PhiXuLy, SoTienHoan, MaPTTT)
        SELECT N'Hủy',
               v.MaVe,
               @MaNV,
               SYSDATETIME(),
               CASE WHEN v.TrangThai = N'Đã bán' THEN @LyDo
                    ELSE N'Suất chiếu bị hủy khi đang giữ chỗ' END,
               0,
               CASE WHEN v.TrangThai = N'Đã bán' THEN v.GiaBan ELSE 0 END,
               CASE WHEN v.TrangThai = N'Đã bán' THEN h.MaPTTT ELSE NULL END
        FROM dbo.VE AS v WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.HOADON AS h ON h.MaHD = v.MaHD
        WHERE v.MaSuatChieu = @MaSuatChieu
          AND v.TrangThai IN (N'Đang giữ chỗ', N'Đã bán');

        UPDATE h
           SET TrangThai = N'Đã hủy'
        FROM dbo.HOADON AS h
        WHERE h.TrangThai = N'Chờ thanh toán'
          AND EXISTS
              (SELECT 1 FROM dbo.VE AS v WHERE v.MaHD = h.MaHD AND v.MaSuatChieu = @MaSuatChieu)
          AND NOT EXISTS
              (SELECT 1 FROM dbo.VE AS v
               WHERE v.MaHD = h.MaHD AND v.TrangThai IN (N'Đang giữ chỗ', N'Đã bán'));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE PROCEDURE dbo.usp_HuyGiuChoQuaHan
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @HoaDonHetHan TABLE (MaHD varchar(10) PRIMARY KEY);

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO @HoaDonHetHan (MaHD)
        SELECT DISTINCT v.MaHD
        FROM dbo.VE AS v WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.HOADON AS h WITH (UPDLOCK, HOLDLOCK) ON h.MaHD = v.MaHD
        WHERE v.TrangThai = N'Đang giữ chỗ'
          AND h.TrangThai = N'Chờ thanh toán'
          AND v.ThoiGianTao <= DATEADD(minute, -10, SYSDATETIME());

        INSERT INTO dbo.XULYVE
            (LoaiXL, MaVeGoc, ThoiGianXL, LyDo, PhiXuLy, SoTienHoan)
        SELECT N'Hủy', v.MaVe, SYSDATETIME(), N'Hết hạn giữ chỗ', 0, 0
        FROM dbo.VE AS v
        JOIN @HoaDonHetHan AS e ON e.MaHD = v.MaHD
        WHERE v.TrangThai = N'Đang giữ chỗ';

        UPDATE h
           SET TrangThai = N'Đã hủy'
        FROM dbo.HOADON AS h
        JOIN @HoaDonHetHan AS e ON e.MaHD = h.MaHD
        WHERE h.TrangThai = N'Chờ thanh toán';

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE PROCEDURE dbo.usp_DoanhThuTheoNgay
    @TuNgay date,
    @DenNgay date
AS
BEGIN
    SET NOCOUNT ON;

    IF @DenNgay < @TuNgay
    BEGIN
        ;THROW 51014, N'Ngày kết thúc không được trước ngày bắt đầu.', 1;
    END;

    SELECT CAST(NgayGhiNhan AS date) AS Ngay,
           SUM(DoanhThuVe) AS DoanhThuVe,
           SUM(PhiXuLy) AS PhiXuLy,
           SUM(DoanhThuVe + PhiXuLy) AS DoanhThuThuan
    FROM
    (
        SELECT v.ThoiGianBan AS NgayGhiNhan,
               v.GiaBan AS DoanhThuVe,
               CAST(0 AS decimal(12, 2)) AS PhiXuLy
        FROM dbo.VE AS v
        WHERE v.TrangThai = N'Đã bán'
          AND v.ThoiGianBan >= @TuNgay
          AND v.ThoiGianBan < DATEADD(day, 1, @DenNgay)

        UNION ALL

        SELECT x.ThoiGianXL,
               CAST(0 AS decimal(12, 2)),
               x.PhiXuLy
        FROM dbo.XULYVE AS x
        WHERE x.PhiXuLy > 0
          AND x.ThoiGianXL >= @TuNgay
          AND x.ThoiGianXL < DATEADD(day, 1, @DenNgay)
    ) AS DoanhThu
    GROUP BY CAST(NgayGhiNhan AS date)
    ORDER BY Ngay;
END;
GO

INSERT INTO dbo.PHIM (MaPhim, TenPhim, ThoiLuong, NgonNgu, GioiHanTuoi, NgayKhoiChieu, TrangThai)
VALUES
    ('P001', N'Hành Trình Ánh Sao', 120, N'Tiếng Việt', 'P13', DATEADD(day, -10, CONVERT(date, GETDATE())), N'Đang chiếu'),
    ('P002', N'Bí Ẩn Thành Phố', 105, N'Tiếng Việt', 'P16', DATEADD(day, -3, CONVERT(date, GETDATE())), N'Đang chiếu');

INSERT INTO dbo.PHIM (MaPhim, TenPhim, ThoiLuong, NgonNgu, GioiHanTuoi, NgayKhoiChieu, TrangThai)
VALUES
    ('MG001', N'Avatar: Lửa và Tro Tàn', 197, N'Tiếng Anh, phụ đề Việt', 'T13', '2025-12-19', N'Đang chiếu'),
    ('MG002', N'Phi Vụ Động Trời 2', 108, N'Tiếng Anh, phụ đề Việt', 'P', '2025-11-26', N'Đang chiếu'),
    ('MG003', N'Minecraft: Phim Điện Ảnh', 101, N'Tiếng Anh, phụ đề Việt', 'P', '2025-04-04', N'Đang chiếu'),
    ('MG004', N'Bí Kíp Luyện Rồng', 125, N'Tiếng Anh, phụ đề Việt', 'P', '2025-06-13', N'Đang chiếu'),
    ('MG005', N'Superman', 129, N'Tiếng Anh, phụ đề Việt', 'T13', '2025-07-11', N'Đang chiếu'),
    ('MG006', N'F1: Phim Điện Ảnh', 155, N'Tiếng Anh, phụ đề Việt', 'T13', '2025-06-27', N'Đang chiếu');

INSERT INTO dbo.THELOAI (MaTheLoai, TenTheLoai, MoTa)
VALUES
    ('TL001', N'Khoa học viễn tưởng', N'Phim có yếu tố khoa học và công nghệ'),
    ('TL002', N'Phiêu lưu', N'Phim về hành trình và khám phá'),
    ('TL003', N'Trinh thám', N'Phim tập trung vào điều tra và giải đố');

INSERT INTO dbo.THELOAI (MaTheLoai, TenTheLoai, MoTa)
VALUES
    ('TL004', N'Hành động', N'Phim có các tình tiết hành động'),
    ('TL005', N'Hoạt hình', N'Phim hoạt hình'),
    ('TL006', N'Hài', N'Phim có yếu tố hài hước'),
    ('TL007', N'Giả tưởng', N'Phim có yếu tố giả tưởng'),
    ('TL008', N'Siêu anh hùng', N'Phim về các nhân vật siêu anh hùng'),
    ('TL009', N'Tâm lý', N'Phim khai thác tâm lý nhân vật'),
    ('TL010', N'Thể thao', N'Phim xoay quanh thể thao');

INSERT INTO dbo.PHIM_THELOAI (MaPhim, MaTheLoai)
VALUES ('P001', 'TL001'), ('P001', 'TL002'), ('P002', 'TL003');

INSERT INTO dbo.PHIM_THELOAI (MaPhim, MaTheLoai)
VALUES
    ('MG001', 'TL001'), ('MG001', 'TL002'), ('MG001', 'TL004'),
    ('MG002', 'TL002'), ('MG002', 'TL005'), ('MG002', 'TL006'),
    ('MG003', 'TL002'), ('MG003', 'TL006'), ('MG003', 'TL007'),
    ('MG004', 'TL002'), ('MG004', 'TL004'), ('MG004', 'TL007'),
    ('MG005', 'TL001'), ('MG005', 'TL004'), ('MG005', 'TL008'),
    ('MG006', 'TL004'), ('MG006', 'TL009'), ('MG006', 'TL010');

INSERT INTO dbo.PHONGCHIEU (MaPhong, TenPhong, LoaiPhong, TrangThai)
VALUES ('R001', N'Phòng 1', N'2D', N'Hoạt động'),
       ('R002', N'Phòng 2', N'3D', N'Hoạt động');

INSERT INTO dbo.GHE (MaGhe, MaPhong, HangGhe, SoGhe, LoaiGhe, TrangThai)
VALUES
    ('G001', 'R001', 'A', 1, N'Thường', N'Hoạt động'),
    ('G002', 'R001', 'A', 2, N'Thường', N'Hoạt động'),
    ('G003', 'R001', 'A', 3, N'VIP', N'Hoạt động'),
    ('G004', 'R002', 'A', 1, N'Thường', N'Hoạt động'),
    ('G005', 'R002', 'A', 2, N'VIP', N'Hoạt động');

;WITH RowDefinitions AS
(
    SELECT RoomId, SeatRow
    FROM (VALUES
        ('R001', 'A'), ('R001', 'B'), ('R001', 'C'),
        ('R001', 'D'), ('R001', 'E'), ('R001', 'F'), ('R001', 'G'), ('R001', 'H'), ('R001', 'I'), ('R001', 'J'),
        ('R002', 'A'), ('R002', 'B'), ('R002', 'C'), ('R002', 'D'), ('R002', 'E'),
        ('R002', 'F'), ('R002', 'G'), ('R002', 'H'), ('R002', 'I'), ('R002', 'J')
    ) AS rows(RoomId, SeatRow)
),
SeatNumbers AS
(
    SELECT SeatNumber
    FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS numbers(SeatNumber)
),
MissingSeats AS
(
    SELECT rows.RoomId, rows.SeatRow, numbers.SeatNumber
    FROM RowDefinitions AS rows
    CROSS JOIN SeatNumbers AS numbers
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.GHE AS existing
        WHERE existing.MaPhong = rows.RoomId
          AND existing.HangGhe = rows.SeatRow
          AND existing.SoGhe = numbers.SeatNumber
    )
),
NumberedSeats AS
(
    SELECT RoomId, SeatRow, SeatNumber,
           ROW_NUMBER() OVER (ORDER BY RoomId, SeatRow, SeatNumber) + 5 AS SeatIdNumber
    FROM MissingSeats
)
INSERT INTO dbo.GHE (MaGhe, MaPhong, HangGhe, SoGhe, LoaiGhe, TrangThai)
SELECT 'G' + RIGHT('000' + CONVERT(varchar(3), SeatIdNumber), 3),
       RoomId, SeatRow, SeatNumber,
       CASE WHEN SeatRow = 'J' THEN N'Đôi'
            WHEN SeatRow IN ('E', 'F', 'G', 'H', 'I') THEN N'VIP'
            ELSE N'Thường' END,
       N'Hoạt động'
FROM NumberedSeats;

UPDATE dbo.GHE
SET LoaiGhe = CASE WHEN HangGhe = 'J' THEN N'Đôi'
                   WHEN HangGhe IN ('E', 'F', 'G', 'H', 'I') THEN N'VIP'
                   ELSE N'Thường' END
WHERE MaPhong IN ('R001', 'R002');

INSERT INTO dbo.NHANVIEN (MaNV, HoTen, SoDienThoai, Email, ChucVu, TrangThai)
VALUES ('NV001', N'Nguyễn Minh Anh', '0900000001', 'minhanh@example.com', N'Thu ngân', N'Đang làm');

INSERT INTO dbo.KHACHHANG (MaKH, HoTen, SoDienThoai, Email, NgaySinh)
VALUES ('KH001', N'Trần Gia Huy', '0900000002', 'giahuy@example.com', '2002-04-15');

INSERT INTO dbo.PHUONGTHUCTT (MaPTTT, TenPTTT, TrangThai)
VALUES
    ('PT001', N'Tiền mặt', N'Đang hoạt động'),
    ('PT002', N'Thẻ ngân hàng', N'Đang hoạt động'),
    ('PT003', N'Chuyển khoản QR', N'Đang hoạt động'),
    ('PT004', N'Ví điện tử', N'Đang hoạt động');

INSERT INTO dbo.KENHBAN (MaKenh, TenKenh, LoaiKenh, MoTa, TrangThai)
VALUES
    ('KB001', N'Quầy vé tại rạp', 'QUAY', N'Bán vé trực tiếp tại quầy', N'Đang hoạt động'),
    ('KB002', N'Website/Ứng dụng MovieGo', 'ONLINE', N'Bán vé qua kênh trực tuyến chính thức', N'Đang hoạt động');

INSERT INTO dbo.KENH_PTTT (MaKenh, MaPTTT)
VALUES
    ('KB001', 'PT001'),
    ('KB001', 'PT002'),
    ('KB002', 'PT002'),
    ('KB002', 'PT003'),
    ('KB002', 'PT004');

DECLARE @NgayMai date = DATEADD(day, 1, CONVERT(date, GETDATE()));
DECLARE @NgayKia date = DATEADD(day, 2, CONVERT(date, GETDATE()));

INSERT INTO dbo.SUATCHIEU
    (MaSuatChieu, MaPhim, MaPhong, ThoiGianBatDau, ThoiGianKetThuc, GiaVeCoBan, TrangThai)
VALUES
    ('SC001', 'P001', 'R001', DATEADD(hour, 18, CAST(@NgayMai AS datetime2)), DATEADD(hour, 20, CAST(@NgayMai AS datetime2)), 100000, N'Sắp chiếu'),
    ('SC002', 'P002', 'R001', DATEADD(hour, 18, CAST(@NgayKia AS datetime2)), DATEADD(hour, 20, CAST(@NgayKia AS datetime2)), 90000, N'Sắp chiếu'),
    ('SC003', 'P001', 'R002', DATEADD(hour, 18, CAST(@NgayMai AS datetime2)), DATEADD(hour, 20, CAST(@NgayMai AS datetime2)), 120000, N'Sắp chiếu');

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

DECLARE @SuatPhimMoi cursor;
DECLARE @MaSuatMoi varchar(10);
DECLARE @MaPhimMoi varchar(10);
DECLARE @MaPhongMoi varchar(10);
DECLARE @ThoiLuongMoi smallint;
DECLARE @GiaVeMoi decimal(12, 2);
DECLARE @NgayChieuMoi int;
DECLARE @BatDauMoi datetime2(0);

SET @SuatPhimMoi = CURSOR LOCAL FAST_FORWARD FOR
    SELECT MaSuatChieu, MaPhim, MaPhong, ThoiLuong, GiaVeCoBan
    FROM @PhimMoi
    ORDER BY MaSuatChieu;

OPEN @SuatPhimMoi;
FETCH NEXT FROM @SuatPhimMoi
INTO @MaSuatMoi, @MaPhimMoi, @MaPhongMoi, @ThoiLuongMoi, @GiaVeMoi;

WHILE @@FETCH_STATUS = 0
BEGIN
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

    FETCH NEXT FROM @SuatPhimMoi
    INTO @MaSuatMoi, @MaPhimMoi, @MaPhongMoi, @ThoiLuongMoi, @GiaVeMoi;
END;

CLOSE @SuatPhimMoi;
DEALLOCATE @SuatPhimMoi;

INSERT INTO dbo.GIAVE_SUAT_HANG (MaSuatChieu, LoaiGhe, GiaVe)
SELECT sc.MaSuatChieu, tiers.LoaiGhe,
       CASE tiers.LoaiGhe WHEN N'Thường' THEN sc.GiaVeCoBan
                          WHEN N'VIP' THEN ROUND(sc.GiaVeCoBan * 1.25, 0)
                          ELSE ROUND(sc.GiaVeCoBan * 1.50, 0) END
FROM dbo.SUATCHIEU AS sc
CROSS JOIN (VALUES (N'Thường'), (N'VIP'), (N'Đôi')) AS tiers(LoaiGhe);

INSERT INTO dbo.HOADON (MaHD, MaNV, MaKH, MaKenh, MaPTTT, TrangThai)
VALUES
    ('HD001', 'NV001', 'KH001', 'KB001', 'PT001', N'Đã thanh toán'),
    ('HD002', NULL, 'KH001', 'KB002', 'PT004', N'Chờ thanh toán');

INSERT INTO dbo.VE (MaVe, MaHD, MaSuatChieu, MaGhe, GiaBan, ThoiGianBan, TrangThai)
VALUES
    ('V001', 'HD001', 'SC001', 'G001', 100000, SYSDATETIME(), N'Đã bán');

INSERT INTO dbo.VE (MaVe, MaHD, MaSuatChieu, MaGhe, GiaBan, TrangThai)
VALUES
    ('V002', 'HD002', 'SC001', 'G002', 100000, N'Đang giữ chỗ');

PRINT N'Đã tạo cơ sở dữ liệu MovieGo, các bảng, ràng buộc, thủ tục và dữ liệu mẫu.';
GO
