using System.Data;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed class SqlAdminAuthService : IAdminAuthService
{
    private const string TablePrefix = "cpm_web_";

    private readonly string _connectionString;
    private readonly ILogger<SqlAdminAuthService> _logger;
    private readonly object _initLock = new();
    private bool _initialized;

    public SqlAdminAuthService(IConfiguration configuration, ILogger<SqlAdminAuthService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao encontrada.");
        _logger = logger;
    }

    public AdminUser? ValidateCredentials(string username, string password)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) Id, Username, DisplayName, PasswordHash, IsActive
FROM dbo.{TablePrefix}admin_users
WHERE Username = @username;
""";
        command.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 80) { Value = username.Trim() });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var isActive = reader.GetBoolean(4);
        var passwordHash = reader.GetString(3);
        if (!isActive || !AdminPasswordHasher.Verify(password, passwordHash))
        {
            return null;
        }

        var user = new AdminUser
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            DisplayName = reader.GetString(2)
        };

        reader.Close();
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}admin_users
SET LastLoginAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
WHERE Id = @id;
""";
        updateCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = user.Id });
        updateCommand.ExecuteNonQuery();

        return user;
    }

    public bool ChangePassword(int userId, string currentPassword, string newPassword, out string message)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            message = "A nova senha deve ter pelo menos 6 caracteres.";
            return false;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) PasswordHash, IsActive
FROM dbo.{TablePrefix}admin_users
WHERE Id = @id;
""";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = userId });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            message = "Usuario admin nao encontrado.";
            return false;
        }

        var passwordHash = reader.GetString(0);
        var isActive = reader.GetBoolean(1);
        if (!isActive)
        {
            message = "Usuario admin inativo.";
            return false;
        }

        if (!AdminPasswordHasher.Verify(currentPassword, passwordHash))
        {
            message = "Senha atual incorreta.";
            return false;
        }

        reader.Close();
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = $"""
UPDATE dbo.{TablePrefix}admin_users
SET PasswordHash = @passwordHash, UpdatedAt = SYSUTCDATETIME()
WHERE Id = @id;
""";
        updateCommand.Parameters.AddRange(
        [
            new SqlParameter("@passwordHash", SqlDbType.NVarChar, 512) { Value = AdminPasswordHasher.Hash(newPassword) },
            new SqlParameter("@id", SqlDbType.Int) { Value = userId }
        ]);
        updateCommand.ExecuteNonQuery();

        message = "Senha alterada com sucesso.";
        return true;
    }

    public AdminDashboardStats GetDashboardStats()
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT
    CASE WHEN OBJECT_ID('dbo.{TablePrefix}professionals', 'U') IS NULL THEN 0 ELSE (SELECT COUNT(1) FROM dbo.{TablePrefix}professionals) END AS ProfessionalsCount,
    CASE WHEN OBJECT_ID('dbo.{TablePrefix}service_requests', 'U') IS NULL THEN 0 ELSE (SELECT COUNT(1) FROM dbo.{TablePrefix}service_requests) END AS ServiceRequestsCount,
    CASE WHEN OBJECT_ID('dbo.{TablePrefix}professional_registrations', 'U') IS NULL THEN 0 ELSE (SELECT COUNT(1) FROM dbo.{TablePrefix}professional_registrations) END AS ProfessionalRegistrationsCount,
    CASE WHEN OBJECT_ID('dbo.{TablePrefix}support_requests', 'U') IS NULL THEN 0 ELSE (SELECT COUNT(1) FROM dbo.{TablePrefix}support_requests) END AS SupportRequestsCount;
""";

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new AdminDashboardStats();
        }

        return new AdminDashboardStats
        {
            ProfessionalsCount = reader.GetInt32(0),
            ServiceRequestsCount = reader.GetInt32(1),
            ProfessionalRegistrationsCount = reader.GetInt32(2),
            SupportRequestsCount = reader.GetInt32(3)
        };
    }

    private SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();

            using var createCommand = connection.CreateCommand();
            createCommand.Transaction = transaction;
            createCommand.CommandText = $"""
IF OBJECT_ID('dbo.{TablePrefix}admin_users', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}admin_users
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Username NVARCHAR(80) NOT NULL UNIQUE,
    DisplayName NVARCHAR(120) NOT NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    IsActive BIT NOT NULL DEFAULT(1),
    LastLoginAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL
);
""";
            createCommand.ExecuteNonQuery();

            using var countCommand = connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText = $"""
SELECT COUNT(1)
FROM dbo.{TablePrefix}admin_users;
""";

            var hasAnyUser = Convert.ToInt32(countCommand.ExecuteScalar()) > 0;
            if (!hasAnyUser)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = $"""
INSERT INTO dbo.{TablePrefix}admin_users
(Username, DisplayName, PasswordHash, IsActive)
VALUES
(@username, @displayName, @passwordHash, 1);
""";
                insertCommand.Parameters.AddRange(
                [
                    new SqlParameter("@username", SqlDbType.NVarChar, 80) { Value = "admin" },
                    new SqlParameter("@displayName", SqlDbType.NVarChar, 120) { Value = "Administrador" },
                    new SqlParameter("@passwordHash", SqlDbType.NVarChar, 512) { Value = AdminPasswordHasher.Hash("123456") }
                ]);
                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
            _initialized = true;
            _logger.LogInformation("Tabela de administradores inicializada com prefixo {Prefix}.", TablePrefix);
        }
    }
}
