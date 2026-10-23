using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using MovieGoApp.Models;
using System.Data;

namespace MovieGoApp.Data;

public sealed class AuthenticationStore(IConfiguration configuration)
{
    public const string CustomerIdClaim = "MovieGoCustomerId";
    public const string CustomerRole = "Customer";
    public const string AdminRole = "Admin";

    private readonly string _connectionString =
        configuration.GetConnectionString("MovieGo")
        ?? throw new InvalidOperationException("Connection string 'MovieGo' is missing.");

    private readonly PasswordHasher<AccountCredential> _passwordHasher = new();

    public async Task<AuthenticatedAccount?> AuthenticateAsync(
        string email, string password, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT a.AccountId, a.Email, a.Role, a.MaKH, a.PasswordHash, k.HoTen
            FROM dbo.TAIKHOAN AS a
            LEFT JOIN dbo.KHACHHANG AS k ON k.MaKH = a.MaKH
            WHERE a.NormalizedEmail = @NormalizedEmail
              AND a.IsActive = 1;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar, 254).Value = NormalizeEmail(email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var credential = new AccountCredential(reader.GetString(4));
        var verification = _passwordHasher.VerifyHashedPassword(credential, credential.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
            return null;

        return new AuthenticatedAccount(
            reader.GetGuid(0).ToString("D"),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(2) == AdminRole
                ? "Admin"
                : reader.IsDBNull(5) ? reader.GetString(1) : reader.GetString(5));
    }

    public async Task RegisterCustomerAsync(RegisterForm form, CancellationToken cancellationToken)
    {
        var email = form.Email.Trim();
        var normalizedEmail = NormalizeEmail(email);
        var customerId = "KH" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var credential = new AccountCredential("");
        var passwordHash = _passwordHasher.HashPassword(credential, form.Password);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string customerSql = """
                INSERT INTO dbo.KHACHHANG (MaKH, HoTen, SoDienThoai, Email)
                VALUES (@CustomerId, @FullName, @PhoneNumber, @Email);
                """;
            await using (var command = new SqlCommand(customerSql, connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 10).Value = customerId;
                command.Parameters.Add("@FullName", SqlDbType.NVarChar, 150).Value = form.FullName.Trim();
                command.Parameters.Add("@PhoneNumber", SqlDbType.VarChar, 20).Value =
                    string.IsNullOrWhiteSpace(form.PhoneNumber) ? DBNull.Value : form.PhoneNumber.Trim();
                command.Parameters.Add("@Email", SqlDbType.VarChar, 254).Value = email;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            const string accountSql = """
                INSERT INTO dbo.TAIKHOAN
                    (AccountId, Email, NormalizedEmail, PasswordHash, Role, MaKH)
                VALUES
                    (@AccountId, @Email, @NormalizedEmail, @PasswordHash, @Role, @CustomerId);
                """;
            await using (var command = new SqlCommand(accountSql, connection, transaction))
            {
                command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                command.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email;
                command.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar, 254).Value = normalizedEmail;
                command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = passwordHash;
                command.Parameters.Add("@Role", SqlDbType.NVarChar, 20).Value = CustomerRole;
                command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 10).Value = customerId;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ArgumentException("Email này đã được đăng ký.", nameof(form.Email), ex);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task EnsureBootstrapAdminAsync(CancellationToken cancellationToken)
    {
        var emailSetting = configuration["MovieGoAdmin:Email"];
        var password = configuration["MovieGoAdmin:Password"];
        if (string.IsNullOrWhiteSpace(emailSetting) && string.IsNullOrWhiteSpace(password))
            return;
        if (string.IsNullOrWhiteSpace(emailSetting) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Set both MovieGoAdmin__Email and MovieGoAdmin__Password to create the initial administrator.");
        if (password.Length < 12)
            throw new InvalidOperationException(
                "MovieGoAdmin__Password must be at least 12 characters when provisioning the initial administrator.");

        var email = emailSetting.Trim();
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)
            || email.Length > 254)
            throw new InvalidOperationException("MovieGoAdmin__Email must contain a valid email address.");

        const string findSql = """
            SELECT Role FROM dbo.TAIKHOAN
            WHERE NormalizedEmail = @NormalizedEmail;
            """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var command = new SqlCommand(findSql, connection))
        {
            command.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar, 254).Value = NormalizeEmail(email);
            var existingRole = await command.ExecuteScalarAsync(cancellationToken);
            if (existingRole is string role)
            {
                if (role != AdminRole)
                    throw new InvalidOperationException(
                        "The configured bootstrap administrator email already belongs to a customer account.");
                return;
            }
        }

        var credential = new AccountCredential("");
        var passwordHash = _passwordHasher.HashPassword(credential, password);
        const string insertSql = """
            INSERT INTO dbo.TAIKHOAN
                (AccountId, Email, NormalizedEmail, PasswordHash, Role, MaKH)
            VALUES
                (@AccountId, @Email, @NormalizedEmail, @PasswordHash, @Role, NULL);
            """;
        await using var insert = new SqlCommand(insertSql, connection);
        insert.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        insert.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email;
        insert.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar, 254).Value = NormalizeEmail(email);
        insert.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = passwordHash;
        insert.Parameters.Add("@Role", SqlDbType.NVarChar, 20).Value = AdminRole;
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private sealed record AccountCredential(string PasswordHash);
}

public sealed record AuthenticatedAccount(
    string AccountId,
    string Email,
    string Role,
    string? CustomerId,
    string DisplayName);
