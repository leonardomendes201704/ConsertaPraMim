using System.Data;
using AppMobileCPM.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ConsertaPraMim.Tests.Unit.Services;

public sealed class SqlAdminKanbanServiceChatwootPersistenceTests
{
    [Fact(DisplayName = "EnsureInitialized deve criar colunas e indice de Chatwoot no kanban")]
    public void EnsureInitialized_DeveCriarColunasEIndiceChatwoot()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);

        _ = service.GetStages(AdminKanbanBoardTypes.Clients);

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = """
SELECT c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.type = 'U' AND o.name = 'cpm_web_kanban_leads'
ORDER BY c.column_id;
""";

        var columnNames = new List<string>();
        using (var reader = columnsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                columnNames.Add(reader.GetString(0));
            }
        }

        Assert.Contains("ChatwootContactId", columnNames);
        Assert.Contains("ChatwootConversationId", columnNames);
        Assert.Contains("ChatwootInboxId", columnNames);
        Assert.Contains("ChatwootSyncStatus", columnNames);
        Assert.Contains("ChatwootLastSyncAt", columnNames);
        Assert.Contains("ChatwootLastError", columnNames);

        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
SELECT COUNT(1)
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.cpm_web_kanban_leads')
  AND name = 'IX_cpm_web_kanban_leads_chatwoot_conversation';
""";

        Assert.Equal(1, Convert.ToInt32(indexCommand.ExecuteScalar()));
    }

    [Fact(DisplayName = "UpdateLeadChatwootSync deve persistir e ler vinculo do Chatwoot no lead")]
    public void UpdateLeadChatwootSync_DevePersistirELerVinculoDoChatwoot()
    {
        using var database = new LocalDbKanbanDatabaseScope();
        if (!database.IsAvailable)
        {
            return;
        }

        var service = CreateService(database.ConnectionString);

        var leadId = service.CreateLead(new AdminKanbanLeadUpsertRequest
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StageId = 0,
            Name = "Lead Integracao Chatwoot",
            Phone = "(13) 99999-0000",
            Email = "lead.chatwoot@teste.com",
            ServiceCategory = "Encanador",
            Source = "Teste automatizado",
            Priority = "alta",
            StatusNote = "Lead criado para validar persistencia Chatwoot.",
            InternalNotes = "Nao remover",
            LastContactAt = new DateTime(2026, 3, 13, 12, 0, 0, DateTimeKind.Utc)
        });

        var firstSyncAt = new DateTime(2026, 3, 13, 13, 30, 0, DateTimeKind.Utc);
        var synced = service.UpdateLeadChatwootSync(leadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootContactId = 101,
            ChatwootConversationId = 202,
            ChatwootInboxId = 1,
            ChatwootSyncStatus = "synced",
            ChatwootLastSyncAt = firstSyncAt,
            ChatwootLastError = "Erro antigo ja tratado"
        });

        Assert.True(synced);

        var secondSyncAt = new DateTime(2026, 3, 13, 14, 15, 0, DateTimeKind.Utc);
        var updated = service.UpdateLeadChatwootSync(leadId, new AdminKanbanLeadChatwootSyncUpdateRequest
        {
            ChatwootSyncStatus = "failed",
            ChatwootLastSyncAt = secondSyncAt,
            ClearChatwootLastError = true
        });

        Assert.True(updated);

        var details = service.GetLeadDetails(leadId);

        Assert.NotNull(details);
        Assert.Equal(101, details!.Chatwoot.ContactId);
        Assert.Equal(202, details.Chatwoot.ConversationId);
        Assert.Equal(1, details.Chatwoot.InboxId);
        Assert.Equal("failed", details.Chatwoot.SyncStatus);
        Assert.Equal(secondSyncAt, details.Chatwoot.LastSyncAt);
        Assert.Equal(string.Empty, details.Chatwoot.LastError);

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT ChatwootContactId, ChatwootConversationId, ChatwootInboxId, ChatwootSyncStatus, ChatwootLastSyncAt, ChatwootLastError
FROM dbo.cpm_web_kanban_leads
WHERE Id = @leadId;
""";
        command.Parameters.Add(new SqlParameter("@leadId", SqlDbType.Int) { Value = leadId });

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(101L, reader.GetInt64(0));
        Assert.Equal(202L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal("failed", reader.GetString(3));
        Assert.Equal(secondSyncAt, reader.GetDateTime(4));
        Assert.True(reader.IsDBNull(5));
    }

    private static SqlAdminKanbanService CreateService(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

        return new SqlAdminKanbanService(configuration);
    }

    private sealed class LocalDbKanbanDatabaseScope : IDisposable
    {
        private const string DefaultMasterConnectionString = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Encrypt=False;TrustServerCertificate=True;";
        private bool _disposed;
        private bool _databaseCreated;
        private readonly string _masterConnectionString;

        public LocalDbKanbanDatabaseScope()
        {
            DatabaseName = $"CpmFullChatwoot_{Guid.NewGuid():N}";
            _masterConnectionString = Environment.GetEnvironmentVariable("CPMFULL_SQLSERVER_TEST_MASTER_CONNECTION")
                ?? DefaultMasterConnectionString;
            ConnectionString = BuildDatabaseConnectionString(_masterConnectionString, DatabaseName);

            try
            {
                using var connection = new SqlConnection(_masterConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{DatabaseName}];";
                command.ExecuteNonQuery();
                _databaseCreated = true;
                IsAvailable = true;
            }
            catch (Exception ex) when (ShouldBypassForUnavailableSqlServer(ex))
            {
                IsAvailable = false;
            }
        }

        public string DatabaseName { get; }

        public string ConnectionString { get; }

        public bool IsAvailable { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!_databaseCreated)
            {
                return;
            }

            using var connection = new SqlConnection(_masterConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"""
IF DB_ID('{DatabaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{DatabaseName}];
END;
""";
            command.ExecuteNonQuery();
        }

        private static string BuildDatabaseConnectionString(string masterConnectionString, string databaseName)
        {
            var builder = new SqlConnectionStringBuilder(masterConnectionString)
            {
                InitialCatalog = databaseName
            };

            return builder.ConnectionString;
        }

        private static bool ShouldBypassForUnavailableSqlServer(Exception ex)
        {
            return ex switch
            {
                SqlException => true,
                InvalidOperationException => true,
                _ when ex.InnerException is not null => ShouldBypassForUnavailableSqlServer(ex.InnerException),
                _ => false
            };
        }
    }
}
