using DataDesensitization.Models;

namespace DataDesensitization.Services;

public interface ISchemaIntrospector
{
    Task<List<TableInfo>> GetTablesAsync(CancellationToken ct = default);
    Task<List<ColumnInfo>> GetColumnsAsync(string tableName, CancellationToken ct = default);
    Task<MigrationRecord?> GetNewestMigrationAsync(CancellationToken ct = default);
    Task<List<MigrationRecord>> GetAllMigrationsAsync(CancellationToken ct = default);
}
