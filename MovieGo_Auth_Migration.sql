/*
    Adds password-based customer accounts and administrator accounts
    to an existing MovieGo database. Safe to run more than once.
*/

IF OBJECT_ID(N'dbo.KHACHHANG', N'U') IS NULL
    THROW 51060, N'Không tìm thấy bảng KHACHHANG. Hãy chọn đúng cơ sở dữ liệu MovieGo.', 1;
GO

IF OBJECT_ID(N'dbo.TAIKHOAN', N'U') IS NULL
BEGIN
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
END;
GO

PRINT N'Đã tạo bảng tài khoản MovieGo. Đăng ký tạo tài khoản khách hàng; tạo admin ban đầu bằng MovieGoAdmin__Email và MovieGoAdmin__Password.';
GO
