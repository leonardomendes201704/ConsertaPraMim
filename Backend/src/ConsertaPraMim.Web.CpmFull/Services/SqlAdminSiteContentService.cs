using System.Data;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed class SqlAdminSiteContentService : IAdminSiteContentService
{
    private const string TablePrefix = "cpm_web_";

    private readonly string _connectionString;
    private readonly InMemoryMarketplaceRepository _seedRepository = new();
    private readonly object _initLock = new();
    private bool _initialized;

    public SqlAdminSiteContentService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao encontrada.");
    }

    public IReadOnlyList<AdminSiteContentRecord> GetAll()
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT Id, [Key], [Value], Description, IsActive, CreatedAt, UpdatedAt
FROM dbo.{TablePrefix}site_contents
ORDER BY [Key];
""";

        var items = new List<AdminSiteContentRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public AdminSiteContentRecord? GetById(int id)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) Id, [Key], [Value], Description, IsActive, CreatedAt, UpdatedAt
FROM dbo.{TablePrefix}site_contents
WHERE Id = @id;
""";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public void Create(AdminSiteContentUpsertRequest request)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}site_contents
([Key], [Value], Description, IsActive, CreatedAt, UpdatedAt)
VALUES
(@key, @value, @description, @isActive, SYSUTCDATETIME(), NULL);
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@key", SqlDbType.NVarChar, 120) { Value = request.Key.Trim() },
            new SqlParameter("@value", SqlDbType.NVarChar, -1) { Value = request.Value.Trim() },
            new SqlParameter("@description", SqlDbType.NVarChar, 260) { Value = request.Description.Trim() },
            new SqlParameter("@isActive", SqlDbType.Bit) { Value = request.IsActive }
        ]);

        command.ExecuteNonQuery();
    }

    public bool Update(int id, AdminSiteContentUpsertRequest request)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}site_contents
SET [Key] = @key,
    [Value] = @value,
    Description = @description,
    IsActive = @isActive,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @id;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@id", SqlDbType.Int) { Value = id },
            new SqlParameter("@key", SqlDbType.NVarChar, 120) { Value = request.Key.Trim() },
            new SqlParameter("@value", SqlDbType.NVarChar, -1) { Value = request.Value.Trim() },
            new SqlParameter("@description", SqlDbType.NVarChar, 260) { Value = request.Description.Trim() },
            new SqlParameter("@isActive", SqlDbType.Bit) { Value = request.IsActive }
        ]);

        return command.ExecuteNonQuery() > 0;
    }

    public bool Delete(int id)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
DELETE FROM dbo.{TablePrefix}site_contents
WHERE Id = @id;
""";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });
        return command.ExecuteNonQuery() > 0;
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

            using (var createCommand = connection.CreateCommand())
            {
                createCommand.Transaction = transaction;
                createCommand.CommandText = $"""
IF OBJECT_ID('dbo.{TablePrefix}site_contents', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}site_contents
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Key] NVARCHAR(120) NOT NULL UNIQUE,
    [Value] NVARCHAR(MAX) NOT NULL,
    Description NVARCHAR(260) NULL,
    IsActive BIT NOT NULL DEFAULT(1),
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL
);
""";
                createCommand.ExecuteNonQuery();
            }

            foreach (var item in _seedRepository.GetSiteContents())
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = $"""
IF NOT EXISTS (SELECT 1 FROM dbo.{TablePrefix}site_contents WHERE [Key] = @key)
BEGIN
    INSERT INTO dbo.{TablePrefix}site_contents
    ([Key], [Value], Description, IsActive, CreatedAt)
    VALUES
    (@key, @value, @description, 1, SYSUTCDATETIME());
END;
""";
                insertCommand.Parameters.AddRange(
                [
                    new SqlParameter("@key", SqlDbType.NVarChar, 120) { Value = item.Key },
                    new SqlParameter("@value", SqlDbType.NVarChar, -1) { Value = item.Value },
                    new SqlParameter("@description", SqlDbType.NVarChar, 260) { Value = SiteContentLabelHelper.BuildFriendlyName(item.Key, null) }
                ]);
                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
            _initialized = true;
        }
    }

    private static AdminSiteContentRecord Map(SqlDataReader reader)
    {
        return new AdminSiteContentRecord
        {
            Id = reader.GetInt32(0),
            Key = reader.GetString(1),
            Value = reader.GetString(2),
            Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            IsActive = reader.GetBoolean(4),
            CreatedAt = reader.GetDateTime(5),
            UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
        };
    }
}
