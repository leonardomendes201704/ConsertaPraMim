using System.Data;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed class SqlAdminSupportFaqService : IAdminSupportFaqService
{
    private const string TablePrefix = "cpm_web_";

    private readonly string _connectionString;
    private readonly InMemoryMarketplaceRepository _seedRepository = new();
    private readonly object _initLock = new();
    private bool _initialized;

    public SqlAdminSupportFaqService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao encontrada.");
    }

    public IReadOnlyList<AdminSupportFaqRecord> GetAll()
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT Id, Question, Answer, IsActive, SortOrder, CreatedAt, UpdatedAt
FROM dbo.{TablePrefix}support_faq_items
ORDER BY SortOrder, Id;
""";

        var items = new List<AdminSupportFaqRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public AdminSupportFaqRecord? GetById(int id)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) Id, Question, Answer, IsActive, SortOrder, CreatedAt, UpdatedAt
FROM dbo.{TablePrefix}support_faq_items
WHERE Id = @id;
""";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public void Create(AdminSupportFaqUpsertRequest request)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}support_faq_items
(Question, Answer, IsActive, SortOrder, CreatedAt, UpdatedAt)
VALUES
(@question, @answer, @isActive, @sortOrder, SYSUTCDATETIME(), NULL);
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@question", SqlDbType.NVarChar, 300) { Value = request.Question.Trim() },
            new SqlParameter("@answer", SqlDbType.NVarChar, -1) { Value = request.Answer.Trim() },
            new SqlParameter("@isActive", SqlDbType.Bit) { Value = request.IsActive },
            new SqlParameter("@sortOrder", SqlDbType.Int) { Value = request.SortOrder }
        ]);
        command.ExecuteNonQuery();
    }

    public bool Update(int id, AdminSupportFaqUpsertRequest request)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
UPDATE dbo.{TablePrefix}support_faq_items
SET Question = @question,
    Answer = @answer,
    IsActive = @isActive,
    SortOrder = @sortOrder,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @id;
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@id", SqlDbType.Int) { Value = id },
            new SqlParameter("@question", SqlDbType.NVarChar, 300) { Value = request.Question.Trim() },
            new SqlParameter("@answer", SqlDbType.NVarChar, -1) { Value = request.Answer.Trim() },
            new SqlParameter("@isActive", SqlDbType.Bit) { Value = request.IsActive },
            new SqlParameter("@sortOrder", SqlDbType.Int) { Value = request.SortOrder }
        ]);
        return command.ExecuteNonQuery() > 0;
    }

    public bool Delete(int id)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
DELETE FROM dbo.{TablePrefix}support_faq_items
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
IF OBJECT_ID('dbo.{TablePrefix}support_faq_items', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}support_faq_items
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Question NVARCHAR(300) NOT NULL,
    Answer NVARCHAR(MAX) NOT NULL,
    IsActive BIT NOT NULL DEFAULT(1),
    SortOrder INT NOT NULL DEFAULT(0),
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL
);
""";
                createCommand.ExecuteNonQuery();
            }

            using (var countCommand = connection.CreateCommand())
            {
                countCommand.Transaction = transaction;
                countCommand.CommandText = $"""
SELECT COUNT(1)
FROM dbo.{TablePrefix}support_faq_items;
""";
                var count = Convert.ToInt32(countCommand.ExecuteScalar());
                if (count == 0)
                {
                    var faqItems = _seedRepository.GetSupportFaqItems();
                    for (var i = 0; i < faqItems.Count; i++)
                    {
                        using var insertCommand = connection.CreateCommand();
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText = $"""
INSERT INTO dbo.{TablePrefix}support_faq_items
(Question, Answer, IsActive, SortOrder, CreatedAt)
VALUES
(@question, @answer, 1, @sortOrder, SYSUTCDATETIME());
""";
                        insertCommand.Parameters.AddRange(
                        [
                            new SqlParameter("@question", SqlDbType.NVarChar, 300) { Value = faqItems[i].Question },
                            new SqlParameter("@answer", SqlDbType.NVarChar, -1) { Value = faqItems[i].Answer },
                            new SqlParameter("@sortOrder", SqlDbType.Int) { Value = i + 1 }
                        ]);
                        insertCommand.ExecuteNonQuery();
                    }
                }
            }

            transaction.Commit();
            _initialized = true;
        }
    }

    private static AdminSupportFaqRecord Map(SqlDataReader reader)
    {
        return new AdminSupportFaqRecord
        {
            Id = reader.GetInt32(0),
            Question = reader.GetString(1),
            Answer = reader.GetString(2),
            IsActive = reader.GetBoolean(3),
            SortOrder = reader.GetInt32(4),
            CreatedAt = reader.GetDateTime(5),
            UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
        };
    }
}
