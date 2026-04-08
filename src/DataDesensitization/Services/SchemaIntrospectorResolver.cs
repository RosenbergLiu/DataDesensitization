using System.Data.Common;
using DataDesensitization.Models;
using Npgsql;

namespace DataDesensitization.Services;

/// <summary>
/// Resolves the correct ISchemaIntrospector implementation based on the
/// current database connection type managed by IConnectionManager.
/// </summary>
public class SchemaIntrospectorResolver : ISchemaIntrospector
{
    private readonly IConnectionManager _connectionManager;

    public SchemaIntrospectorResolver(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public Task<List<TableInfo>> GetTablesAsync(CancellationToken ct = default)
        => GetIntrospector().GetTablesAsync(ct);

    public Task<List<ColumnInfo>> GetColumnsAsync(string tableName, CancellationToken ct = default)
        => GetIntrospector().GetColumnsAsync(tableName, ct);

    public Task<MigrationRecord?> GetNewestMigrationAsync(CancellationToken ct = default)
        => GetIntrospector().GetNewestMigrationAsync(ct);

    public Task<List<MigrationRecord>> GetAllMigrationsAsync(CancellationToken ct = default)
        => GetIntrospector().GetAllMigrationsAsync(ct);

    private ISchemaIntrospector GetIntrospector()
    {
        var connection = _connectionManager.CurrentConnection
            ?? throw new InvalidOperationException("No active database connection. Connect to a database first.");

        return connection switch
        {
            NpgsqlConnection => new PostgreSqlSchemaIntrospector(connection),
            _ => new SqlServerSchemaIntrospector(connection)
        };
    }
}
