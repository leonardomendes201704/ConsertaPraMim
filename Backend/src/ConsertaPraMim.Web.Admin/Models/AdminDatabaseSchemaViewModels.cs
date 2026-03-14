namespace ConsertaPraMim.Web.Admin.Models;

public sealed class AdminDatabaseSchemaViewModel
{
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public int TotalTables { get; init; }
    public int TotalRelationships { get; init; }
    public string MermaidSource { get; init; } = string.Empty;
    public IReadOnlyList<AdminDatabaseSchemaDiagramOptionViewModel> DiagramOptions { get; init; } = Array.Empty<AdminDatabaseSchemaDiagramOptionViewModel>();
    public IReadOnlyList<AdminDatabaseSchemaTableViewModel> Tables { get; init; } = Array.Empty<AdminDatabaseSchemaTableViewModel>();
    public IReadOnlyList<AdminDatabaseSchemaRelationshipViewModel> Relationships { get; init; } = Array.Empty<AdminDatabaseSchemaRelationshipViewModel>();
}

public sealed class AdminDatabaseSchemaDiagramOptionViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ScopeKind { get; init; } = string.Empty;
    public string DomainName { get; init; } = string.Empty;
    public bool SupportsErTableLayout { get; init; }
    public int TotalTables { get; init; }
    public int TotalRelationships { get; init; }
    public string MermaidSource { get; init; } = string.Empty;
}

public sealed class AdminDatabaseSchemaTableViewModel
{
    public string Schema { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string DomainName { get; init; } = string.Empty;
    public int TotalColumns { get; init; }
    public IReadOnlyList<AdminDatabaseSchemaColumnViewModel> Columns { get; init; } = Array.Empty<AdminDatabaseSchemaColumnViewModel>();
    public IReadOnlyList<string> PrimaryKeyColumns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ForeignKeyColumns { get; init; } = Array.Empty<string>();
}

public sealed class AdminDatabaseSchemaColumnViewModel
{
    public string Name { get; init; } = string.Empty;
    public string StoreType { get; init; } = string.Empty;
    public bool IsPrimaryKey { get; init; }
    public bool IsForeignKey { get; init; }
    public bool IsNullable { get; init; }
}

public sealed class AdminDatabaseSchemaRelationshipViewModel
{
    public string ConstraintName { get; init; } = string.Empty;
    public string PrincipalTable { get; init; } = string.Empty;
    public string DependentTable { get; init; } = string.Empty;
    public IReadOnlyList<string> PrincipalColumns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DependentColumns { get; init; } = Array.Empty<string>();
    public bool IsRequired { get; init; }
    public bool IsUnique { get; init; }
    public string DeleteBehavior { get; init; } = string.Empty;
}
