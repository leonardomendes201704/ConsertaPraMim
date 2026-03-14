using ConsertaPraMim.Web.Admin.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminDatabaseSchemaServiceTests
{
    [Fact(DisplayName = "Admin database schema service | BuildViewModel | Deve listar tabelas, relacionamentos e Mermaid")]
    public async Task BuildViewModelAsync_ShouldReturnTablesRelationshipsAndMermaidSource()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminDatabaseSchemaService(memoryCache, NullLogger<AdminDatabaseSchemaService>.Instance);

        var viewModel = await service.BuildViewModelAsync();

        Assert.True(viewModel.TotalTables > 0);
        Assert.True(viewModel.TotalRelationships > 0);
        Assert.StartsWith("flowchart TB", viewModel.MermaidSource, StringComparison.Ordinal);
        Assert.NotEmpty(viewModel.DiagramOptions);
        Assert.Contains(viewModel.DiagramOptions, option =>
            string.Equals(option.Key, "overview-domains", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(viewModel.DiagramOptions[0].MermaidSource, viewModel.MermaidSource);
        Assert.All(viewModel.Tables, table => Assert.False(string.IsNullOrWhiteSpace(table.DomainName)));
        Assert.Contains(viewModel.DiagramOptions, option =>
            string.Equals(option.ScopeKind, "domain", StringComparison.OrdinalIgnoreCase) &&
            option.SupportsErTableLayout);
        var domainOverview = Assert.Single(
            viewModel.DiagramOptions,
            option => string.Equals(option.Key, "overview-domains", StringComparison.OrdinalIgnoreCase));
        Assert.False(domainOverview.SupportsErTableLayout);
        var usersTable = Assert.Single(
            viewModel.Tables,
            table => table.FullName.EndsWith(".Users", StringComparison.OrdinalIgnoreCase));
        Assert.True(usersTable.TotalColumns > 0);
        Assert.Equal(usersTable.TotalColumns, usersTable.Columns.Count);
        Assert.False(string.IsNullOrWhiteSpace(usersTable.DomainName));
        Assert.Contains(usersTable.Columns, column =>
            !string.IsNullOrWhiteSpace(column.StoreType));
        Assert.Contains(viewModel.Relationships, relationship =>
            relationship.DependentColumns.Count > 0);
    }

    [Fact(DisplayName = "Admin database schema service | BuildViewModel | Deve reaproveitar cache entre chamadas")]
    public async Task BuildViewModelAsync_ShouldUseMemoryCache()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminDatabaseSchemaService(memoryCache, NullLogger<AdminDatabaseSchemaService>.Instance);

        var first = await service.BuildViewModelAsync();
        var second = await service.BuildViewModelAsync();

        Assert.Same(first, second);
    }
}
