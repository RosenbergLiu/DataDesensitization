// Feature: data-desensitization, Property 1: Schema filter returns only matching items
// **Validates: Requirements 2.4**

using DataDesensitization.Models;
using DataDesensitization.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DataDesensitization.Tests;

/// <summary>
/// Fake ISchemaIntrospector that returns a preconfigured list of tables.
/// </summary>
internal class FakeSchemaIntrospector : ISchemaIntrospector
{
    private readonly List<TableInfo> _tables;

    public FakeSchemaIntrospector(List<TableInfo> tables)
    {
        _tables = tables;
    }

    public Task<List<TableInfo>> GetTablesAsync(CancellationToken ct = default)
        => Task.FromResult(_tables);

    public Task<List<ColumnInfo>> GetColumnsAsync(string tableName, CancellationToken ct = default)
        => Task.FromResult(new List<ColumnInfo>());

    public Task<MigrationRecord?> GetNewestMigrationAsync(CancellationToken ct = default)
        => Task.FromResult<MigrationRecord?>(null);

    public Task<List<MigrationRecord>> GetAllMigrationsAsync(CancellationToken ct = default)
        => Task.FromResult(new List<MigrationRecord>());
}

public class SchemaFilterPropertyTests
{
    [Property(MaxTest = 100)]
    public Property SchemaFilter_ReturnsExactlyMatchingTables(NonEmptyString filter)
    {
        var filterStr = filter.Get;

        return Prop.ForAll(
            Arb.From<List<string>>(),
            tableNames =>
            {
                var tables = tableNames
                    .Where(n => n != null)
                    .Select(n => new TableInfo("dbo", n))
                    .ToList();

                var introspector = new FakeSchemaIntrospector(tables);
                var service = new SchemaService(introspector);

                var result = service.SearchTablesAsync(filterStr).GetAwaiter().GetResult();

                var expected = tables
                    .Where(t => t.TableName.Contains(filterStr, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Result contains exactly those tables whose name matches (no extras)
                var noExtras = result.All(r =>
                    r.TableName.Contains(filterStr, StringComparison.OrdinalIgnoreCase));

                // No matching items are excluded
                var noneExcluded = expected.All(e => result.Contains(e));

                // Counts match
                var countMatches = result.Count == expected.Count;

                return noExtras && noneExcluded && countMatches;
            });
    }
}
