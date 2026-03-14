using System.Data;
using System.Text.Json;
using AppMobileCPM.Models;
using Microsoft.Data.SqlClient;

namespace AppMobileCPM.Services;

public sealed class SqlMarketplaceRepository : IMarketplaceRepository
{
    private const string TablePrefix = "cpm_web_";

    private readonly string _connectionString;
    private readonly ILogger<SqlMarketplaceRepository> _logger;
    private readonly InMemoryMarketplaceRepository _seedRepository = new();
    private readonly object _initLock = new();
    private bool _initialized;

    public SqlMarketplaceRepository(IConfiguration configuration, ILogger<SqlMarketplaceRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao encontrada.");
        _logger = logger;
    }

    public IReadOnlyList<ServiceCategory> GetCategories()
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT Id, Name, IconClass
FROM dbo.{TablePrefix}service_categories
WHERE IsActive = 1
ORDER BY SortOrder, Name;
""";

        var items = new List<ServiceCategory>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new ServiceCategory
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                IconClass = reader.GetString(2)
            });
        }

        return items;
    }

    public ServiceCategory? GetCategoryById(string categoryId)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) Id, Name, IconClass
FROM dbo.{TablePrefix}service_categories
WHERE IsActive = 1 AND Id = @id;
""";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80) { Value = categoryId });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ServiceCategory
        {
            Id = reader.GetString(0),
            Name = reader.GetString(1),
            IconClass = reader.GetString(2)
        };
    }

    public IReadOnlyList<string> GetProfessionOptions()
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT Name
FROM dbo.{TablePrefix}profession_options
WHERE IsActive = 1
ORDER BY SortOrder, Name;
""";

        var items = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(reader.GetString(0));
        }

        return items;
    }

    public IReadOnlyList<Professional> GetProfessionals(string? searchTerm = null)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT Id, Name, Profession, Description, Rating, Reviews, Distance, ServicesJson, ServicePhotoUrlsJson, Verified, ImageUrl, WhatsappUrl
FROM dbo.{TablePrefix}professionals
WHERE IsActive = 1
  AND (
    @searchTerm = ''
    OR Name LIKE @searchLike
    OR Profession LIKE @searchLike
    OR Description LIKE @searchLike
    OR ServicesJson LIKE @searchLike
  )
ORDER BY SortOrder, Rating DESC, Reviews DESC, Name;
""";
        var term = searchTerm?.Trim() ?? string.Empty;
        command.Parameters.Add(new SqlParameter("@searchTerm", SqlDbType.NVarChar, 200) { Value = term });
        command.Parameters.Add(new SqlParameter("@searchLike", SqlDbType.NVarChar, 260) { Value = $"%{term}%" });

        var items = new List<Professional>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new Professional
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Profession = reader.GetString(2),
                Description = reader.GetString(3),
                Rating = reader.GetDouble(4),
                Reviews = reader.GetInt32(5),
                Distance = reader.GetString(6),
                Services = DeserializeStringList(reader.GetString(7)),
                ServicePhotoUrls = DeserializeStringList(reader.GetString(8)),
                Verified = reader.GetBoolean(9),
                ImageUrl = reader.GetString(10),
                WhatsappUrl = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
            });
        }

        return items;
    }

    public IReadOnlyList<string> GetSupportCategoryOptions()
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT Name
FROM dbo.{TablePrefix}support_categories
WHERE IsActive = 1
ORDER BY SortOrder, Name;
""";

        var items = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(reader.GetString(0));
        }

        return items;
    }

    public IReadOnlyList<FaqItem> GetSupportFaqItems()
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT Question, Answer
FROM dbo.{TablePrefix}support_faq_items
WHERE IsActive = 1
ORDER BY SortOrder, Question;
""";

        var items = new List<FaqItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new FaqItem
            {
                Question = reader.GetString(0),
                Answer = reader.GetString(1)
            });
        }

        return items;
    }

    public IReadOnlyDictionary<string, string> GetSiteContents()
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT [Key], [Value]
FROM dbo.{TablePrefix}site_contents
WHERE IsActive = 1;
""";

        var items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items[reader.GetString(0)] = reader.GetString(1);
        }

        return items;
    }

    public string? GetSiteContent(string key)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT TOP (1) [Value]
FROM dbo.{TablePrefix}site_contents
WHERE [Key] = @key AND IsActive = 1;
""";
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 120) { Value = key });
        var value = command.ExecuteScalar();
        return value as string;
    }

    public void AddServiceRequest(ServiceRequest request)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}service_requests
(CategoryId, CategoryName, Description, Location, Name, Phone, IsWhatsapp, SubmittedAt)
VALUES
(@categoryId, @categoryName, @description, @location, @name, @phone, @isWhatsapp, @submittedAt);
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@categoryId", SqlDbType.NVarChar, 80) { Value = request.CategoryId },
            new SqlParameter("@categoryName", SqlDbType.NVarChar, 120) { Value = request.CategoryName },
            new SqlParameter("@description", SqlDbType.NVarChar, -1) { Value = request.Description },
            new SqlParameter("@location", SqlDbType.NVarChar, 120) { Value = request.Location },
            new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = request.Name },
            new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = request.Phone },
            new SqlParameter("@isWhatsapp", SqlDbType.Bit) { Value = request.IsWhatsapp },
            new SqlParameter("@submittedAt", SqlDbType.DateTimeOffset) { Value = request.SubmittedAt }
        ]);
        command.ExecuteNonQuery();
    }

    public void AddProfessionalRegistration(ProfessionalRegistration registration)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}professional_registrations
(Name, Profession, Services, PostalCode, Phone, IsWhatsapp, Experience, SubmittedAt)
VALUES
(@name, @profession, @services, @postalCode, @phone, @isWhatsapp, @experience, @submittedAt);
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = registration.Name },
            new SqlParameter("@profession", SqlDbType.NVarChar, 140) { Value = registration.Profession },
            new SqlParameter("@services", SqlDbType.NVarChar, -1) { Value = registration.Services },
            new SqlParameter("@postalCode", SqlDbType.NVarChar, 9) { Value = registration.PostalCode },
            new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = registration.Phone },
            new SqlParameter("@isWhatsapp", SqlDbType.Bit) { Value = registration.IsWhatsapp },
            new SqlParameter("@experience", SqlDbType.NVarChar, -1) { Value = registration.Experience },
            new SqlParameter("@submittedAt", SqlDbType.DateTimeOffset) { Value = registration.SubmittedAt }
        ]);
        command.ExecuteNonQuery();
    }

    public void AddSupportRequest(SupportRequest request)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
INSERT INTO dbo.{TablePrefix}support_requests
(Name, Email, Phone, Category, Subject, Message, SubmittedAt)
VALUES
(@name, @email, @phone, @category, @subject, @message, @submittedAt);
""";
        command.Parameters.AddRange(
        [
            new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = request.Name },
            new SqlParameter("@email", SqlDbType.NVarChar, 180) { Value = request.Email },
            new SqlParameter("@phone", SqlDbType.NVarChar, 30) { Value = request.Phone },
            new SqlParameter("@category", SqlDbType.NVarChar, 120) { Value = request.Category },
            new SqlParameter("@subject", SqlDbType.NVarChar, 180) { Value = request.Subject },
            new SqlParameter("@message", SqlDbType.NVarChar, -1) { Value = request.Message },
            new SqlParameter("@submittedAt", SqlDbType.DateTimeOffset) { Value = request.SubmittedAt }
        ]);
        command.ExecuteNonQuery();
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

            CreateTables(connection, transaction);
            SeedData(connection, transaction);

            transaction.Commit();
            _initialized = true;
            _logger.LogInformation("Tabelas {Prefix} inicializadas no banco.", TablePrefix);
        }
    }

    private static void CreateTables(SqlConnection connection, SqlTransaction transaction)
    {
        var sql = $"""
IF OBJECT_ID('dbo.{TablePrefix}service_categories', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}service_categories
(
    Id NVARCHAR(80) NOT NULL PRIMARY KEY,
    Name NVARCHAR(120) NOT NULL,
    IconClass NVARCHAR(80) NOT NULL,
    IsActive BIT NOT NULL DEFAULT(1),
    SortOrder INT NOT NULL DEFAULT(0),
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL
);

IF OBJECT_ID('dbo.{TablePrefix}profession_options', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}profession_options
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(140) NOT NULL UNIQUE,
    IsActive BIT NOT NULL DEFAULT(1),
    SortOrder INT NOT NULL DEFAULT(0),
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL
);

IF OBJECT_ID('dbo.{TablePrefix}professionals', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}professionals
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(140) NOT NULL,
    Profession NVARCHAR(140) NOT NULL,
    Description NVARCHAR(600) NOT NULL,
    Rating FLOAT NOT NULL,
    Reviews INT NOT NULL,
    Distance NVARCHAR(40) NOT NULL,
    ServicesJson NVARCHAR(MAX) NOT NULL,
    ServicePhotoUrlsJson NVARCHAR(MAX) NOT NULL,
    Verified BIT NOT NULL,
    ImageUrl NVARCHAR(500) NOT NULL,
    WhatsappUrl NVARCHAR(300) NULL,
    IsActive BIT NOT NULL DEFAULT(1),
    SortOrder INT NOT NULL DEFAULT(0),
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL
);

IF OBJECT_ID('dbo.{TablePrefix}service_requests', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}service_requests
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CategoryId NVARCHAR(80) NOT NULL,
    CategoryName NVARCHAR(120) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    Location NVARCHAR(120) NOT NULL,
    Name NVARCHAR(140) NOT NULL,
    Phone NVARCHAR(30) NOT NULL,
    IsWhatsapp BIT NOT NULL DEFAULT(0),
    SubmittedAt DATETIMEOFFSET NOT NULL
);

IF OBJECT_ID('dbo.{TablePrefix}professional_registrations', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}professional_registrations
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(140) NOT NULL,
    Profession NVARCHAR(140) NOT NULL,
    Services NVARCHAR(MAX) NOT NULL,
    PostalCode NVARCHAR(9) NOT NULL,
    Phone NVARCHAR(30) NOT NULL,
    IsWhatsapp BIT NOT NULL DEFAULT(0),
    Experience NVARCHAR(MAX) NOT NULL,
    SubmittedAt DATETIMEOFFSET NOT NULL
);

IF OBJECT_ID('dbo.{TablePrefix}support_requests', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}support_requests
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(140) NOT NULL,
    Email NVARCHAR(180) NOT NULL,
    Phone NVARCHAR(30) NULL,
    Category NVARCHAR(120) NOT NULL,
    Subject NVARCHAR(180) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    SubmittedAt DATETIMEOFFSET NOT NULL
);

IF OBJECT_ID('dbo.{TablePrefix}support_categories', 'U') IS NULL
CREATE TABLE dbo.{TablePrefix}support_categories
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(120) NOT NULL UNIQUE,
    IsActive BIT NOT NULL DEFAULT(1),
    SortOrder INT NOT NULL DEFAULT(0),
    CreatedAt DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIME2 NULL
);

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

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void SeedData(SqlConnection connection, SqlTransaction transaction)
    {
        SeedServiceCategories(connection, transaction);
        SeedProfessionOptions(connection, transaction);
        SeedProfessionals(connection, transaction);
        SeedSupportCategories(connection, transaction);
        SeedSupportFaqItems(connection, transaction);
        SeedSiteContents(connection, transaction);
    }

    private void SeedServiceCategories(SqlConnection connection, SqlTransaction transaction)
    {
        if (TableHasRows(connection, transaction, $"{TablePrefix}service_categories"))
        {
            return;
        }

        var categories = _seedRepository.GetCategories();
        for (var i = 0; i < categories.Count; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"""
INSERT INTO dbo.{TablePrefix}service_categories (Id, Name, IconClass, SortOrder)
VALUES (@id, @name, @iconClass, @sortOrder);
""";
            cmd.Parameters.AddRange(
            [
                new SqlParameter("@id", SqlDbType.NVarChar, 80) { Value = categories[i].Id },
                new SqlParameter("@name", SqlDbType.NVarChar, 120) { Value = categories[i].Name },
                new SqlParameter("@iconClass", SqlDbType.NVarChar, 80) { Value = categories[i].IconClass },
                new SqlParameter("@sortOrder", SqlDbType.Int) { Value = i + 1 }
            ]);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedProfessionOptions(SqlConnection connection, SqlTransaction transaction)
    {
        if (TableHasRows(connection, transaction, $"{TablePrefix}profession_options"))
        {
            return;
        }

        var options = _seedRepository.GetProfessionOptions();
        for (var i = 0; i < options.Count; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"""
INSERT INTO dbo.{TablePrefix}profession_options (Name, SortOrder)
VALUES (@name, @sortOrder);
""";
            cmd.Parameters.AddRange(
            [
                new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = options[i] },
                new SqlParameter("@sortOrder", SqlDbType.Int) { Value = i + 1 }
            ]);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedProfessionals(SqlConnection connection, SqlTransaction transaction)
    {
        if (TableHasRows(connection, transaction, $"{TablePrefix}professionals"))
        {
            return;
        }

        var professionals = _seedRepository.GetProfessionals();
        for (var i = 0; i < professionals.Count; i++)
        {
            var professional = professionals[i];
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"""
INSERT INTO dbo.{TablePrefix}professionals
(Name, Profession, Description, Rating, Reviews, Distance, ServicesJson, ServicePhotoUrlsJson, Verified, ImageUrl, WhatsappUrl, SortOrder)
VALUES
(@name, @profession, @description, @rating, @reviews, @distance, @services, @photos, @verified, @imageUrl, @whatsappUrl, @sortOrder);
""";
            cmd.Parameters.AddRange(
            [
                new SqlParameter("@name", SqlDbType.NVarChar, 140) { Value = professional.Name },
                new SqlParameter("@profession", SqlDbType.NVarChar, 140) { Value = professional.Profession },
                new SqlParameter("@description", SqlDbType.NVarChar, 600) { Value = professional.Description },
                new SqlParameter("@rating", SqlDbType.Float) { Value = professional.Rating },
                new SqlParameter("@reviews", SqlDbType.Int) { Value = professional.Reviews },
                new SqlParameter("@distance", SqlDbType.NVarChar, 40) { Value = professional.Distance },
                new SqlParameter("@services", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(professional.Services) },
                new SqlParameter("@photos", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(professional.ServicePhotoUrls) },
                new SqlParameter("@verified", SqlDbType.Bit) { Value = professional.Verified },
                new SqlParameter("@imageUrl", SqlDbType.NVarChar, 500) { Value = professional.ImageUrl },
                new SqlParameter("@whatsappUrl", SqlDbType.NVarChar, 300) { Value = professional.WhatsappUrl },
                new SqlParameter("@sortOrder", SqlDbType.Int) { Value = i + 1 }
            ]);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedSupportCategories(SqlConnection connection, SqlTransaction transaction)
    {
        if (TableHasRows(connection, transaction, $"{TablePrefix}support_categories"))
        {
            return;
        }

        var categories = _seedRepository.GetSupportCategoryOptions();
        for (var i = 0; i < categories.Count; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"""
INSERT INTO dbo.{TablePrefix}support_categories (Name, SortOrder)
VALUES (@name, @sortOrder);
""";
            cmd.Parameters.AddRange(
            [
                new SqlParameter("@name", SqlDbType.NVarChar, 120) { Value = categories[i] },
                new SqlParameter("@sortOrder", SqlDbType.Int) { Value = i + 1 }
            ]);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedSupportFaqItems(SqlConnection connection, SqlTransaction transaction)
    {
        if (TableHasRows(connection, transaction, $"{TablePrefix}support_faq_items"))
        {
            return;
        }

        var items = _seedRepository.GetSupportFaqItems();
        for (var i = 0; i < items.Count; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"""
INSERT INTO dbo.{TablePrefix}support_faq_items (Question, Answer, SortOrder)
VALUES (@question, @answer, @sortOrder);
""";
            cmd.Parameters.AddRange(
            [
                new SqlParameter("@question", SqlDbType.NVarChar, 300) { Value = items[i].Question },
                new SqlParameter("@answer", SqlDbType.NVarChar, -1) { Value = items[i].Answer },
                new SqlParameter("@sortOrder", SqlDbType.Int) { Value = i + 1 }
            ]);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedSiteContents(SqlConnection connection, SqlTransaction transaction)
    {
        foreach (var item in _seedRepository.GetSiteContents())
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"""
IF NOT EXISTS (SELECT 1 FROM dbo.{TablePrefix}site_contents WHERE [Key] = @key)
BEGIN
    INSERT INTO dbo.{TablePrefix}site_contents ([Key], [Value], Description)
    VALUES (@key, @value, @description);
END;
""";
            cmd.Parameters.AddRange(
            [
                new SqlParameter("@key", SqlDbType.NVarChar, 120) { Value = item.Key },
                new SqlParameter("@value", SqlDbType.NVarChar, -1) { Value = item.Value },
                new SqlParameter("@description", SqlDbType.NVarChar, 260) { Value = SiteContentLabelHelper.BuildFriendlyName(item.Key, null) }
            ]);
            cmd.ExecuteNonQuery();
        }
    }

    private static bool TableHasRows(SqlConnection connection, SqlTransaction transaction, string tableName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.{tableName}) THEN 1 ELSE 0 END;
""";
        return command.ExecuteScalar() is 1;
    }

    private static IReadOnlyList<string> DeserializeStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json);
            return values ?? [];
        }
        catch
        {
            return [];
        }
    }
}
