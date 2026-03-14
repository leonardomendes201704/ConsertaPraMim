using System.Text;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Web.Admin.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;

namespace ConsertaPraMim.Web.Admin.Services;

public sealed class AdminDatabaseSchemaService : IAdminDatabaseSchemaService
{
    private const string SchemaCacheKey = "admin:database-schema:view-model";
    private static readonly TimeSpan SchemaCacheDuration = TimeSpan.FromSeconds(30);
    private const string DesignTimeConnectionString = "Server=(localdb)\\mssqllocaldb;Database=ConsertaPraMimSchemaPreview;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private static readonly (string Prefix, string Domain)[] DomainRules =
    [
        ("Admin", "Administracao"),
        ("Api", "Monitoramento API"),
        ("Landing", "Landing"),
        ("SupportTicket", "Suporte"),
        ("Chatbot", "Chatbot"),
        ("Chat", "Conversas"),
        ("ServiceDispute", "Disputas"),
        ("ServiceAppointment", "Agendamentos"),
        ("ServiceScopeChange", "Escopo e Alteracoes"),
        ("ServiceWarranty", "Garantias"),
        ("ServicePayment", "Pagamentos"),
        ("ServiceChecklist", "Checklists"),
        ("ServiceCategory", "Catalogo de Servicos"),
        ("Service", "Pedidos e Servicos"),
        ("ProviderPlan", "Planos de Prestadores"),
        ("ProviderCredit", "Creditos de Prestadores"),
        ("Provider", "Prestadores"),
        ("Proposal", "Propostas"),
        ("User", "Usuarios e Termos"),
        ("LegalTerms", "Usuarios e Termos"),
        ("SystemSetting", "Configuracoes"),
        ("NoShow", "No-show"),
        ("MobilePush", "Notificacoes"),
        ("PjRecurring", "Contratos PJ"),
        ("Review", "Avaliacoes")
    ];

    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminDatabaseSchemaService> _logger;

    public AdminDatabaseSchemaService(
        IMemoryCache memoryCache,
        ILogger<AdminDatabaseSchemaService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<AdminDatabaseSchemaViewModel> BuildViewModelAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_memoryCache.TryGetValue(SchemaCacheKey, out AdminDatabaseSchemaViewModel? cached) && cached != null)
        {
            return Task.FromResult(cached);
        }

        AdminDatabaseSchemaViewModel viewModel;
        try
        {
            viewModel = BuildViewModel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao montar visao de schema do banco para o Portal Admin.");
            viewModel = new AdminDatabaseSchemaViewModel
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                MermaidSource = "flowchart TB",
                DiagramOptions =
                [
                    new AdminDatabaseSchemaDiagramOptionViewModel
                    {
                        Key = "overview",
                        Name = "Visao geral",
                        MermaidSource = "flowchart TB"
                    }
                ]
            };
        }

        _memoryCache.Set(SchemaCacheKey, viewModel, SchemaCacheDuration);
        return Task.FromResult(viewModel);
    }

    private static AdminDatabaseSchemaViewModel BuildViewModel()
    {
        using var dbContext = CreateSchemaContext();
        var tableMap = BuildTableCatalog(dbContext.Model);
        var relationships = BuildRelationshipCatalog(dbContext.Model, tableMap.Keys);

        var tables = tableMap.Values
            .Select(table => new AdminDatabaseSchemaTableViewModel
            {
                Schema = table.Schema,
                Name = table.Name,
                FullName = BuildTableKey(table.Schema, table.Name),
                DomainName = ResolveDomainName(table.Name),
                TotalColumns = table.Columns.Count,
                Columns = table.Columns
                    .OrderByDescending(column => column.IsPrimaryKey)
                    .ThenByDescending(column => column.IsForeignKey)
                    .ThenBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(column => new AdminDatabaseSchemaColumnViewModel
                    {
                        Name = column.Name,
                        StoreType = column.StoreType,
                        IsPrimaryKey = column.IsPrimaryKey,
                        IsForeignKey = column.IsForeignKey,
                        IsNullable = column.IsNullable
                    })
                    .ToArray(),
                PrimaryKeyColumns = table.Columns
                    .Where(column => column.IsPrimaryKey)
                    .Select(column => column.Name)
                    .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ForeignKeyColumns = table.Columns
                    .Where(column => column.IsForeignKey)
                    .Select(column => column.Name)
                    .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .OrderBy(table => table.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var relationshipModels = relationships
            .Select(relationship => new AdminDatabaseSchemaRelationshipViewModel
            {
                ConstraintName = relationship.ConstraintName,
                PrincipalTable = relationship.PrincipalTable,
                DependentTable = relationship.DependentTable,
                PrincipalColumns = relationship.PrincipalColumns,
                DependentColumns = relationship.DependentColumns,
                IsRequired = relationship.IsRequired,
                IsUnique = relationship.IsUnique,
                DeleteBehavior = relationship.DeleteBehavior
            })
            .OrderBy(relationship => relationship.PrincipalTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(relationship => relationship.DependentTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(relationship => relationship.ConstraintName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var domainByTable = tables.ToDictionary(
            table => table.FullName,
            table => table.DomainName,
            StringComparer.OrdinalIgnoreCase);

        var diagramOptions = BuildDiagramOptions(tables, relationshipModels, domainByTable);
        var defaultDiagram = diagramOptions.First();

        return new AdminDatabaseSchemaViewModel
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            TotalTables = tables.Length,
            TotalRelationships = relationshipModels.Length,
            MermaidSource = defaultDiagram.MermaidSource,
            DiagramOptions = diagramOptions,
            Tables = tables,
            Relationships = relationshipModels
        };
    }

    private static IReadOnlyList<AdminDatabaseSchemaDiagramOptionViewModel> BuildDiagramOptions(
        IReadOnlyList<AdminDatabaseSchemaTableViewModel> tables,
        IReadOnlyList<AdminDatabaseSchemaRelationshipViewModel> relationships,
        IReadOnlyDictionary<string, string> domainByTable)
    {
        var options = new List<AdminDatabaseSchemaDiagramOptionViewModel>();

        options.Add(new AdminDatabaseSchemaDiagramOptionViewModel
        {
            Key = "overview-domains",
            Name = "Visao macro por dominios",
            ScopeKind = "domain-overview",
            SupportsErTableLayout = false,
            TotalTables = tables.Count,
            TotalRelationships = relationships.Count,
            MermaidSource = BuildDomainOverviewSource(
                tables,
                relationships,
                domainByTable)
        });

        options.Add(new AdminDatabaseSchemaDiagramOptionViewModel
        {
            Key = "overview",
            Name = "Visao geral (todos os dominios)",
            ScopeKind = "overview",
            SupportsErTableLayout = true,
            TotalTables = tables.Count,
            TotalRelationships = relationships.Count,
            MermaidSource = BuildFlowchartSource(
                tables,
                relationships,
                domainByTable,
                    includeDomainSubgraphs: true)
        });

        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "overview-domains",
            "overview"
        };
        var domains = tables
            .GroupBy(table => domainByTable[table.FullName], StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var domainGroup in domains)
        {
            var domainTables = domainGroup
                .OrderBy(table => table.FullName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (domainTables.Length == 0)
            {
                continue;
            }

            var domainTableSet = domainTables
                .Select(table => table.FullName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var domainRelationships = relationships
                .Where(relationship =>
                    domainTableSet.Contains(relationship.PrincipalTable) &&
                    domainTableSet.Contains(relationship.DependentTable))
                .ToArray();

            var keyBase = BuildDomainKey(domainGroup.Key);
            var key = keyBase;
            var suffix = 2;
            while (!usedKeys.Add(key))
            {
                key = $"{keyBase}-{suffix}";
                suffix++;
            }

            options.Add(new AdminDatabaseSchemaDiagramOptionViewModel
            {
                Key = key,
                Name = $"{domainGroup.Key} ({domainTables.Length} tabelas)",
                ScopeKind = "domain",
                DomainName = domainGroup.Key,
                SupportsErTableLayout = true,
                TotalTables = domainTables.Length,
                TotalRelationships = domainRelationships.Length,
                MermaidSource = BuildFlowchartSource(
                    domainTables,
                    domainRelationships,
                    domainByTable,
                    includeDomainSubgraphs: false)
            });
        }

        return options;
    }

    private static ConsertaPraMimDbContext CreateSchemaContext()
    {
        var options = new DbContextOptionsBuilder<ConsertaPraMimDbContext>()
            .UseSqlServer(DesignTimeConnectionString)
            .Options;

        return new ConsertaPraMimDbContext(options);
    }

    private static Dictionary<string, MutableTableDescriptor> BuildTableCatalog(IModel model)
    {
        var tableMap = new Dictionary<string, MutableTableDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            var storeSchema = entityType.GetSchema();
            var schema = storeSchema ?? "dbo";
            var tableKey = BuildTableKey(schema, tableName);
            if (!tableMap.TryGetValue(tableKey, out var table))
            {
                table = new MutableTableDescriptor(schema, tableName);
                tableMap[tableKey] = table;
            }

            var tableIdentifier = StoreObjectIdentifier.Table(tableName, storeSchema);
            var primaryKeyColumns = entityType.FindPrimaryKey()?.Properties
                .Select(property => property.GetColumnName(tableIdentifier) ?? property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var foreignKeyColumns = entityType.GetForeignKeys()
                .SelectMany(foreignKey => foreignKey.Properties)
                .Select(property => property.GetColumnName(tableIdentifier))
                .Where(columnName => !string.IsNullOrWhiteSpace(columnName))
                .Select(columnName => columnName!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(tableIdentifier);
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    continue;
                }

                var storeType = ResolveStoreType(property, tableIdentifier);
                table.RegisterColumn(
                    columnName,
                    storeType,
                    property.IsNullable,
                    primaryKeyColumns.Contains(columnName),
                    foreignKeyColumns.Contains(columnName));
            }
        }

        return tableMap;
    }

    private static IReadOnlyList<RelationshipDescriptor> BuildRelationshipCatalog(
        IModel model,
        IReadOnlyCollection<string> tableKeys)
    {
        var relationships = new Dictionary<string, RelationshipDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            var dependentTableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(dependentTableName))
            {
                continue;
            }

            var dependentStoreSchema = entityType.GetSchema();
            var dependentSchema = dependentStoreSchema ?? "dbo";
            var dependentTableKey = BuildTableKey(dependentSchema, dependentTableName);
            if (!tableKeys.Contains(dependentTableKey, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var dependentTableIdentifier = StoreObjectIdentifier.Table(dependentTableName, dependentStoreSchema);

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                var principalTableName = foreignKey.PrincipalEntityType.GetTableName();
                if (string.IsNullOrWhiteSpace(principalTableName))
                {
                    continue;
                }

                var principalStoreSchema = foreignKey.PrincipalEntityType.GetSchema();
                var principalSchema = principalStoreSchema ?? "dbo";
                var principalTableKey = BuildTableKey(principalSchema, principalTableName);
                if (!tableKeys.Contains(principalTableKey, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var principalTableIdentifier = StoreObjectIdentifier.Table(principalTableName, principalStoreSchema);
                var dependentColumns = DistinctColumns(
                    foreignKey.Properties.Select(property => property.GetColumnName(dependentTableIdentifier) ?? property.Name));
                var principalColumns = DistinctColumns(
                    foreignKey.PrincipalKey.Properties.Select(property => property.GetColumnName(principalTableIdentifier) ?? property.Name));
                var relationshipKey = BuildRelationshipKey(principalTableKey, dependentTableKey, dependentColumns, principalColumns);
                if (relationships.ContainsKey(relationshipKey))
                {
                    continue;
                }

                relationships[relationshipKey] = new RelationshipDescriptor(
                    ConstraintName: foreignKey.GetConstraintName() ?? BuildFallbackConstraintName(dependentTableName, dependentColumns),
                    PrincipalTable: principalTableKey,
                    DependentTable: dependentTableKey,
                    PrincipalColumns: principalColumns,
                    DependentColumns: dependentColumns,
                    IsRequired: foreignKey.IsRequired,
                    IsUnique: foreignKey.IsUnique,
                    DeleteBehavior: foreignKey.DeleteBehavior.ToString());
            }
        }

        return relationships.Values.ToArray();
    }

    private static string BuildDomainOverviewSource(
        IReadOnlyList<AdminDatabaseSchemaTableViewModel> tables,
        IReadOnlyList<AdminDatabaseSchemaRelationshipViewModel> relationships,
        IReadOnlyDictionary<string, string> domainByTable)
    {
        var domainSummaries = tables
            .GroupBy(table => domainByTable[table.FullName], StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Domain = group.Key,
                TableCount = group.Count(),
                InternalRelationshipCount = relationships.Count(relationship =>
                    string.Equals(
                        ResolveDomainForTable(domainByTable, relationship.DependentTable),
                        group.Key,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        ResolveDomainForTable(domainByTable, relationship.PrincipalTable),
                        group.Key,
                        StringComparison.OrdinalIgnoreCase))
            })
            .OrderBy(summary => summary.Domain, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var aliasByDomain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < domainSummaries.Length; i++)
        {
            var summary = domainSummaries[i];
            var baseAlias = BuildMermaidAlias("DOMAIN", summary.Domain);
            var candidate = baseAlias;
            var suffix = 2;

            while (!usedAliases.Add(candidate))
            {
                candidate = $"{baseAlias}_{suffix}";
                suffix++;
            }

            aliasByDomain[summary.Domain] = candidate;
        }

        var crossDomainRelationships = relationships
            .Select(relationship => new
            {
                FromDomain = ResolveDomainForTable(domainByTable, relationship.DependentTable),
                ToDomain = ResolveDomainForTable(domainByTable, relationship.PrincipalTable)
            })
            .Where(edge => !string.Equals(edge.FromDomain, edge.ToDomain, StringComparison.OrdinalIgnoreCase))
            .GroupBy(edge => new { edge.FromDomain, edge.ToDomain })
            .Select(group => new
            {
                group.Key.FromDomain,
                group.Key.ToDomain,
                TotalRelationships = group.Count()
            })
            .OrderBy(edge => edge.FromDomain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.ToDomain, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("flowchart TB");
        builder.AppendLine("    classDef domainNode fill:#eff6ff,stroke:#1d4ed8,stroke-width:1.2px,color:#1e3a8a,font-size:12px;");

        foreach (var summary in domainSummaries)
        {
            var alias = aliasByDomain[summary.Domain];
            var label = $"{summary.Domain}<br/>{summary.TableCount} tabelas<br/>{summary.InternalRelationshipCount} FK internas";

            builder.Append("    ")
                .Append(alias)
                .Append("[\"")
                .Append(EscapeMermaidText(label))
                .AppendLine("\"]");
        }

        foreach (var edge in crossDomainRelationships)
        {
            if (!aliasByDomain.TryGetValue(edge.FromDomain, out var fromAlias) ||
                !aliasByDomain.TryGetValue(edge.ToDomain, out var toAlias))
            {
                continue;
            }

            builder.Append("    ")
                .Append(fromAlias)
                .Append(" -->|\"")
                .Append(edge.TotalRelationships)
                .Append(" FK\"| ")
                .Append(toAlias)
                .AppendLine();
        }

        builder.Append("    class ")
            .Append(string.Join(",", aliasByDomain.Values))
            .AppendLine(" domainNode;");

        return builder.ToString().TrimEnd();
    }

    private static string BuildFlowchartSource(
        IReadOnlyList<AdminDatabaseSchemaTableViewModel> tables,
        IReadOnlyList<AdminDatabaseSchemaRelationshipViewModel> relationships,
        IReadOnlyDictionary<string, string> domainByTable,
        bool includeDomainSubgraphs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("flowchart TB");
        builder.AppendLine("    classDef tableNode fill:#f8fafc,stroke:#334155,stroke-width:1px,color:#0f172a,font-size:11px;");

        var aliasMap = BuildTableAliasMap(tables);

        if (includeDomainSubgraphs)
        {
            var grouped = tables
                .GroupBy(table => domainByTable[table.FullName], StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (var i = 0; i < grouped.Length; i++)
            {
                var group = grouped[i];
                var subgraphId = $"SG_{i + 1:00}";
                builder.Append("    subgraph ")
                    .Append(subgraphId)
                    .Append("[\"")
                    .Append(EscapeMermaidText(group.Key))
                    .AppendLine("\"]");

                foreach (var table in group.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    var alias = aliasMap[table.FullName];
                    builder.Append("        ")
                        .Append(alias)
                        .Append("[\"")
                        .Append(EscapeMermaidText(table.FullName))
                        .AppendLine("\"]");
                }

                builder.AppendLine("    end");
            }
        }
        else
        {
            foreach (var table in tables.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
            {
                var alias = aliasMap[table.FullName];
                builder.Append("    ")
                    .Append(alias)
                    .Append("[\"")
                    .Append(EscapeMermaidText(table.FullName))
                    .AppendLine("\"]");
            }
        }

        foreach (var relationship in relationships
                     .OrderBy(item => item.DependentTable, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.PrincipalTable, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.ConstraintName, StringComparer.OrdinalIgnoreCase))
        {
            if (!aliasMap.TryGetValue(relationship.DependentTable, out var dependentAlias) ||
                !aliasMap.TryGetValue(relationship.PrincipalTable, out var principalAlias))
            {
                continue;
            }

            var edgeLabel = BuildFlowchartEdgeLabel(relationship);
            builder.Append("    ")
                .Append(dependentAlias)
                .Append(" -->|\"")
                .Append(EscapeMermaidText(edgeLabel))
                .Append("\"| ")
                .Append(principalAlias)
                .AppendLine();
        }

        builder.Append("    class ")
            .Append(string.Join(",", aliasMap.Values))
            .AppendLine(" tableNode;");

        return builder.ToString().TrimEnd();
    }

    private static Dictionary<string, string> BuildTableAliasMap(IReadOnlyList<AdminDatabaseSchemaTableViewModel> tables)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            var baseAlias = BuildMermaidAlias(table.Schema, table.Name);
            var candidate = baseAlias;
            var suffix = 2;

            while (!usedAliases.Add(candidate))
            {
                candidate = $"{baseAlias}_{suffix}";
                suffix++;
            }

            aliases[table.FullName] = candidate;
        }

        return aliases;
    }

    private static string BuildMermaidAlias(string schema, string tableName)
    {
        var raw = $"{schema}_{tableName}";
        var builder = new StringBuilder(raw.Length + 4);

        foreach (var character in raw)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_');
        }

        if (builder.Length == 0)
        {
            builder.Append("TABLE");
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, 'T');
        }

        return builder.ToString().Trim('_');
    }

    private static string BuildFlowchartEdgeLabel(AdminDatabaseSchemaRelationshipViewModel relationship)
    {
        var dependentColumnsLabel = relationship.DependentColumns.Count switch
        {
            0 => "FK",
            <= 2 => string.Join(", ", relationship.DependentColumns),
            _ => string.Join(", ", relationship.DependentColumns.Take(2)) + ", ..."
        };

        var cardinality = relationship.IsUnique ? "1:1" : "N:1";
        var required = relationship.IsRequired ? "obrigatorio" : "opcional";

        return $"{dependentColumnsLabel} ({cardinality}, {required})";
    }

    private static string ResolveDomainForTable(
        IReadOnlyDictionary<string, string> domainByTable,
        string tableFullName)
    {
        if (domainByTable.TryGetValue(tableFullName, out var domain) &&
            !string.IsNullOrWhiteSpace(domain))
        {
            return domain;
        }

        return "Outros";
    }

    private static string ResolveDomainName(string tableName)
    {
        foreach (var (prefix, domain) in DomainRules)
        {
            if (tableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return domain;
            }
        }

        return "Outros";
    }

    private static string BuildDomainKey(string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            return "domain-outros";
        }

        var builder = new StringBuilder();
        foreach (var character in domainName.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length == 0 || builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "outros";
        }

        return $"domain-{slug}";
    }

    private static string EscapeMermaidText(string value)
    {
        return value.Replace('"', '\'');
    }

    private static string BuildRelationshipKey(
        string principalTable,
        string dependentTable,
        IReadOnlyList<string> dependentColumns,
        IReadOnlyList<string> principalColumns)
    {
        return string.Join(
            "|",
            principalTable,
            dependentTable,
            string.Join(",", dependentColumns),
            string.Join(",", principalColumns));
    }

    private static string BuildFallbackConstraintName(string dependentTableName, IReadOnlyList<string> dependentColumns)
    {
        var suffix = dependentColumns.Count == 0
            ? "fk"
            : string.Join("_", dependentColumns.Select(column => column.ToLowerInvariant()));

        return $"{dependentTableName}_{suffix}";
    }

    private static string BuildTableKey(string schema, string tableName)
    {
        return $"{schema}.{tableName}";
    }

    private static string ResolveStoreType(IProperty property, StoreObjectIdentifier tableIdentifier)
    {
        var configuredType = property.GetColumnType(tableIdentifier);
        if (!string.IsNullOrWhiteSpace(configuredType))
        {
            return configuredType;
        }

        var mappedType = property.GetRelationalTypeMapping()?.StoreType;
        if (!string.IsNullOrWhiteSpace(mappedType))
        {
            return mappedType;
        }

        return property.ClrType.Name;
    }

    private static IReadOnlyList<string> DistinctColumns(IEnumerable<string> columns)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        foreach (var column in columns)
        {
            if (!unique.Add(column))
            {
                continue;
            }

            ordered.Add(column);
        }

        return ordered;
    }

    private sealed class MutableTableDescriptor
    {
        private readonly Dictionary<string, MutableColumnDescriptor> _columns = new(StringComparer.OrdinalIgnoreCase);

        public MutableTableDescriptor(string schema, string name)
        {
            Schema = schema;
            Name = name;
        }

        public string Schema { get; }
        public string Name { get; }
        public IReadOnlyList<MutableColumnDescriptor> Columns => _columns.Values.ToArray();

        public void RegisterColumn(string name, string storeType, bool isNullable, bool isPrimaryKey, bool isForeignKey)
        {
            if (!_columns.TryGetValue(name, out var column))
            {
                column = new MutableColumnDescriptor(name, storeType, isNullable);
                _columns[name] = column;
            }
            else
            {
                if (string.Equals(column.StoreType, "desconhecido", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(storeType))
                {
                    column.StoreType = storeType;
                }

                if (isNullable)
                {
                    column.IsNullable = true;
                }
            }

            if (isPrimaryKey)
            {
                column.IsPrimaryKey = true;
            }

            if (isForeignKey)
            {
                column.IsForeignKey = true;
            }
        }
    }

    private sealed class MutableColumnDescriptor
    {
        public MutableColumnDescriptor(string name, string storeType, bool isNullable)
        {
            Name = name;
            StoreType = string.IsNullOrWhiteSpace(storeType) ? "desconhecido" : storeType;
            IsNullable = isNullable;
        }

        public string Name { get; }
        public string StoreType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
    }

    private sealed record RelationshipDescriptor(
        string ConstraintName,
        string PrincipalTable,
        string DependentTable,
        IReadOnlyList<string> PrincipalColumns,
        IReadOnlyList<string> DependentColumns,
        bool IsRequired,
        bool IsUnique,
        string DeleteBehavior);
}
