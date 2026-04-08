using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class SchemaService : ISchemaService
{
    private readonly ISchemaIntrospector _introspector;

    public SchemaService(ISchemaIntrospector introspector)
    {
        _introspector = introspector;
    }

    public Task<List<TableInfo>> GetTablesAsync(CancellationToken ct = default)
        => _introspector.GetTablesAsync(ct);

    public Task<List<ColumnInfo>> GetColumnsAsync(string tableName, CancellationToken ct = default)
        => _introspector.GetColumnsAsync(tableName, ct);

    public async Task<List<TableInfo>> SearchTablesAsync(string filter, CancellationToken ct = default)
    {
        var tables = await _introspector.GetTablesAsync(ct);

        if (string.IsNullOrEmpty(filter))
            return tables;

        return tables
            .Where(t => t.TableName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
