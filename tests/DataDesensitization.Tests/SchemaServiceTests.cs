using DataDesensitization.Models;
using DataDesensitization.Services;

namespace DataDesensitization.Tests;

/// <summary>
/// Fake ISchemaIntrospector that supports configurable tables and columns.
/// </summary>
internal class ConfigurableFakeSchemaIntrospector : ISchemaIntrospector
{
    private readonly List<TableInfo> _tables;
    private readonly Dictionary<string, List<ColumnInfo>> _columns;

    public ConfigurableFakeSchemaIntrospector(
        List<TableInfo> tables,
        Dictionary<string, List<ColumnInfo>>? columns = null)
    {
        _tables = tables;
        _columns = columns ?? new Dictionary<string, List<ColumnInfo>>();
    }

    public Task<List<TableInfo>> GetTablesAsync(CancellationToken ct = default)
        => Task.FromResult(_tables);

    public Task<List<ColumnInfo>> GetColumnsAsync(string tableName, CancellationToken ct = default)
        => Task.FromResult(_columns.TryGetValue(tableName, out var cols) ? cols : new List<ColumnInfo>());

    public Task<MigrationRecord?> GetNewestMigrationAsync(CancellationToken ct = default)
        => Task.FromResult<MigrationRecord?>(null);

    public Task<List<MigrationRecord>> GetAllMigrationsAsync(CancellationToken ct = default)
        => Task.FromResult(new List<MigrationRecord>());
}

public class SchemaServiceTests
{
    [Fact]
    public async Task GetTablesAsync_ReturnsTablesFromIntrospector()
    {
        var tables = new List<TableInfo>
        {
            new("dbo", "Users"),
            new("dbo", "Orders"),
            new("sales", "Products")
        };
        var introspector = new FakeSchemaIntrospector(tables);
        var service = new SchemaService(introspector);

        var result = await service.GetTablesAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal(tables, result);
    }

    [Fact]
    public async Task GetColumnsAsync_ReturnsColumnsFromIntrospector()
    {
        var columns = new Dictionary<string, List<ColumnInfo>>
        {
            ["Users"] = new List<ColumnInfo>
            {
                new("Id", "int", false, null),
                new("Name", "nvarchar", true, 100),
                new("Email", "nvarchar", true, 255)
            }
        };
        var introspector = new ConfigurableFakeSchemaIntrospector(new List<TableInfo>(), columns);
        var service = new SchemaService(introspector);

        var result = await service.GetColumnsAsync("Users");

        Assert.Equal(3, result.Count);
        Assert.Equal("Id", result[0].ColumnName);
        Assert.Equal("Name", result[1].ColumnName);
        Assert.Equal("Email", result[2].ColumnName);
    }

    [Fact]
    public async Task SearchTablesAsync_WithMatchingFilter_ReturnsMatchingTables()
    {
        var tables = new List<TableInfo>
        {
            new("dbo", "Users"),
            new("dbo", "UserRoles"),
            new("dbo", "Orders"),
            new("sales", "Products")
        };
        var introspector = new FakeSchemaIntrospector(tables);
        var service = new SchemaService(introspector);

        var result = await service.SearchTablesAsync("User");

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Contains("User", t.TableName));
    }

    [Fact]
    public async Task SearchTablesAsync_WithNonMatchingFilter_ReturnsEmptyList()
    {
        var tables = new List<TableInfo>
        {
            new("dbo", "Users"),
            new("dbo", "Orders")
        };
        var introspector = new FakeSchemaIntrospector(tables);
        var service = new SchemaService(introspector);

        var result = await service.SearchTablesAsync("Inventory");

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SearchTablesAsync_WithEmptyOrNullFilter_ReturnsAllTables(string? filter)
    {
        var tables = new List<TableInfo>
        {
            new("dbo", "Users"),
            new("dbo", "Orders"),
            new("sales", "Products")
        };
        var introspector = new FakeSchemaIntrospector(tables);
        var service = new SchemaService(introspector);

        var result = await service.SearchTablesAsync(filter!);

        Assert.Equal(3, result.Count);
        Assert.Equal(tables, result);
    }

    [Fact]
    public async Task SearchTablesAsync_IsCaseInsensitive()
    {
        var tables = new List<TableInfo>
        {
            new("dbo", "Users"),
            new("dbo", "USERS_ARCHIVE"),
            new("dbo", "Orders")
        };
        var introspector = new FakeSchemaIntrospector(tables);
        var service = new SchemaService(introspector);

        var result = await service.SearchTablesAsync("users");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.TableName == "Users");
        Assert.Contains(result, t => t.TableName == "USERS_ARCHIVE");
    }
}
