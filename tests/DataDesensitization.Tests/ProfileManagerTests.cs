using DataDesensitization.Models;
using DataDesensitization.Services;

namespace DataDesensitization.Tests;

/// <summary>
/// Full ISchemaIntrospector fake for ProfileManager tests.
/// </summary>
internal class ProfileTestIntrospector : ISchemaIntrospector
{
    public List<MigrationRecord> Migrations { get; set; } = [];
    public List<TableInfo> Tables { get; set; } = [];
    public Dictionary<string, List<ColumnInfo>> Columns { get; set; } = new();

    public Task<List<TableInfo>> GetTablesAsync(CancellationToken ct = default) =>
        Task.FromResult(Tables);

    public Task<List<ColumnInfo>> GetColumnsAsync(string tableName, CancellationToken ct = default) =>
        Task.FromResult(Columns.GetValueOrDefault(tableName) ?? []);

    public Task<MigrationRecord?> GetNewestMigrationAsync(CancellationToken ct = default) =>
        Task.FromResult(Migrations.Count > 0 ? Migrations[^1] : (MigrationRecord?)null);

    public Task<List<MigrationRecord>> GetAllMigrationsAsync(CancellationToken ct = default) =>
        Task.FromResult(Migrations);
}

public class ProfileManagerTests
{
    private static readonly MigrationRecord Migration1 = new("20240101_Init", "8.0.0");
    private static readonly MigrationRecord Migration2 = new("20240201_AddOrders", "8.0.0");

    private static ProfileTestIntrospector MakeIntrospector(
        List<MigrationRecord>? migrations = null,
        List<TableInfo>? tables = null,
        Dictionary<string, List<ColumnInfo>>? columns = null)
    {
        return new ProfileTestIntrospector
        {
            Migrations = migrations ?? [Migration1, Migration2],
            Tables = tables ?? [new("dbo", "Users")],
            Columns = columns ?? new()
            {
                ["dbo.Users"] = [new("Id", "int", false, null), new("Email", "nvarchar", true, 255)]
            }
        };
    }

    #region ExportProfileAsync

    [Fact]
    public async Task ExportProfileAsync_ReturnsNonEmptyBytes()
    {
        var introspector = MakeIntrospector();
        var manager = new ProfileManager(introspector);
        var rules = new List<DesensitizationRule>
        {
            new("dbo.Users", "Email", DesensitizationStrategyType.Masking, new StrategyParameters())
        };

        var bytes = await manager.ExportProfileAsync("TestProfile", rules);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task ExportProfileAsync_ContainsProfileNameAndRules()
    {
        var introspector = MakeIntrospector();
        var manager = new ProfileManager(introspector);
        var rules = new List<DesensitizationRule>
        {
            new("dbo.Users", "Email", DesensitizationStrategyType.Masking, new StrategyParameters())
        };

        var bytes = await manager.ExportProfileAsync("MyProfile", rules);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("MyProfile", json);
        Assert.Contains("Email", json);
    }

    #endregion

    #region ImportProfileAsync — matching migrations

    [Fact]
    public async Task ImportProfileAsync_MatchingMigrations_ReturnsSuccess()
    {
        var introspector = MakeIntrospector();
        var manager = new ProfileManager(introspector);
        var rules = new List<DesensitizationRule>
        {
            new("dbo.Users", "Email", DesensitizationStrategyType.Masking, new StrategyParameters())
        };

        var exported = await manager.ExportProfileAsync("Test", rules);
        var result = await manager.ImportProfileAsync(exported);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.LoadResult);
        Assert.Single(result.LoadResult!.MatchedRules);
        Assert.Empty(result.LoadResult.UnmatchedRules);
    }

    #endregion

    #region ImportProfileAsync — migration mismatch

    [Fact]
    public async Task ImportProfileAsync_MigrationMismatch_ReturnsFailure()
    {
        var exportIntrospector = MakeIntrospector(migrations: [Migration1]);
        var exportManager = new ProfileManager(exportIntrospector);
        var exported = await exportManager.ExportProfileAsync("Test",
            [new("dbo.Users", "Email", DesensitizationStrategyType.Masking, new StrategyParameters())]);

        // Import into a DB with different migrations
        var importIntrospector = MakeIntrospector(migrations: [Migration1, Migration2]);
        var importManager = new ProfileManager(importIntrospector);

        var result = await importManager.ImportProfileAsync(exported);

        Assert.False(result.Success);
        Assert.Contains("Migration history mismatch", result.ErrorMessage);
    }

    #endregion

    #region ImportProfileAsync — unmatched rules

    [Fact]
    public async Task ImportProfileAsync_RuleReferencesRemovedColumn_ReportsUnmatched()
    {
        // Export with Email column present
        var exportIntrospector = MakeIntrospector();
        var exportManager = new ProfileManager(exportIntrospector);
        var exported = await exportManager.ExportProfileAsync("Test",
        [
            new("dbo.Users", "Email", DesensitizationStrategyType.Masking, new StrategyParameters()),
            new("dbo.Users", "Deleted", DesensitizationStrategyType.Nullification, new StrategyParameters())
        ]);

        // Import into DB where "Deleted" column doesn't exist
        var importIntrospector = MakeIntrospector();
        var importManager = new ProfileManager(importIntrospector);

        var result = await importManager.ImportProfileAsync(exported);

        Assert.True(result.Success);
        Assert.Single(result.LoadResult!.MatchedRules);
        Assert.Single(result.LoadResult.UnmatchedRules);
        Assert.Equal("Deleted", result.LoadResult.UnmatchedRules[0].ColumnName);
    }

    #endregion

    #region ImportProfileAsync — empty migrations both sides

    [Fact]
    public async Task ImportProfileAsync_BothEmptyMigrations_Succeeds()
    {
        var introspector = MakeIntrospector(migrations: []);
        var manager = new ProfileManager(introspector);
        var exported = await manager.ExportProfileAsync("Test", []);

        var result = await manager.ImportProfileAsync(exported);

        Assert.True(result.Success);
    }

    #endregion

    #region ImportProfileAsync — invalid data

    [Fact]
    public async Task ImportProfileAsync_InvalidJson_Throws()
    {
        var introspector = MakeIntrospector();
        var manager = new ProfileManager(introspector);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => manager.ImportProfileAsync(System.Text.Encoding.UTF8.GetBytes("not json")));
    }

    #endregion
}
