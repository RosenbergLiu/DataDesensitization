using System.Data.Common;
using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class PostgreSqlSchemaIntrospector : ISchemaIntrospector
{
    private readonly DbConnection _connection;

    public PostgreSqlSchemaIntrospector(DbConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<TableInfo>> GetTablesAsync(CancellationToken ct = default)
    {
        var tables = new List<TableInfo>();

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
              AND table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY table_schema, table_name";

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tables.Add(new TableInfo(
                reader.GetString(0),
                reader.GetString(1)));
        }

        return tables;
    }

    public async Task<List<ColumnInfo>> GetColumnsAsync(string tableName, CancellationToken ct = default)
    {
        var columns = new List<ColumnInfo>();

        // Support schema-qualified names like "public.users"
        var parts = tableName.Split('.', 2);
        var schema = parts.Length > 1 ? parts[0] : null;
        var table = parts.Length > 1 ? parts[1] : parts[0];

        // First, collect foreign key columns for this table
        var fkColumns = await GetForeignKeyColumnsAsync(schema, table, ct);

        using var command = _connection.CreateCommand();

        if (schema is not null)
        {
            command.CommandText = @"
                SELECT column_name, data_type, is_nullable, character_maximum_length
                FROM information_schema.columns
                WHERE table_schema = @Schema AND table_name = @TableName
                ORDER BY ordinal_position";

            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@Schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);
        }
        else
        {
            command.CommandText = @"
                SELECT column_name, data_type, is_nullable, character_maximum_length
                FROM information_schema.columns
                WHERE table_name = @TableName
                ORDER BY ordinal_position";
        }

        var param = command.CreateParameter();
        param.ParameterName = "@TableName";
        param.Value = table;
        command.Parameters.Add(param);

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var columnName = reader.GetString(0);
            fkColumns.TryGetValue(columnName, out var referencedTable);

            columns.Add(new ColumnInfo(
                columnName,
                reader.GetString(1),
                reader.GetString(2) == "YES",
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                IsForeignKey: referencedTable is not null,
                ReferencedTable: referencedTable));
        }

        return columns;
    }

    private async Task<Dictionary<string, string>> GetForeignKeyColumnsAsync(string? schema, string table, CancellationToken ct)
    {
        var fkColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var command = _connection.CreateCommand();

        if (schema is not null)
        {
            command.CommandText = @"
                SELECT
                    kcu.column_name,
                    ccu.table_schema || '.' || ccu.table_name
                FROM information_schema.table_constraints tc
                INNER JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name
                    AND tc.table_schema = kcu.table_schema
                INNER JOIN information_schema.constraint_column_usage ccu
                    ON tc.constraint_name = ccu.constraint_name
                    AND tc.table_schema = ccu.table_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                    AND tc.table_schema = @Schema
                    AND tc.table_name = @TableName";

            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@Schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);
        }
        else
        {
            command.CommandText = @"
                SELECT
                    kcu.column_name,
                    ccu.table_schema || '.' || ccu.table_name
                FROM information_schema.table_constraints tc
                INNER JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name
                    AND tc.table_schema = kcu.table_schema
                INNER JOIN information_schema.constraint_column_usage ccu
                    ON tc.constraint_name = ccu.constraint_name
                    AND tc.table_schema = ccu.table_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                    AND tc.table_name = @TableName";
        }

        var param = command.CreateParameter();
        param.ParameterName = "@TableName";
        param.Value = table;
        command.Parameters.Add(param);

        try
        {
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var columnName = reader.GetString(0);
                var referencedTable = reader.GetString(1);
                fkColumns.TryAdd(columnName, referencedTable);
            }
        }
        catch (DbException)
        {
            // If FK metadata query fails, proceed without FK info
        }

        return fkColumns;
    }

    public async Task<MigrationRecord?> GetNewestMigrationAsync(CancellationToken ct = default)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT ""MigrationId"", ""ProductVersion""
            FROM ""__EFMigrationsHistory""
            ORDER BY ""MigrationId"" DESC
            LIMIT 1";

        try
        {
            using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new MigrationRecord(
                    reader.GetString(0),
                    reader.GetString(1));
            }

            return null;
        }
        catch (DbException)
        {
            // Table may not exist if EF Core migrations are not used
            return null;
        }
    }

    public async Task<List<MigrationRecord>> GetAllMigrationsAsync(CancellationToken ct = default)
    {
        var migrations = new List<MigrationRecord>();

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT ""MigrationId"", ""ProductVersion""
            FROM ""__EFMigrationsHistory""
            ORDER BY ""MigrationId""";

        try
        {
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                migrations.Add(new MigrationRecord(
                    reader.GetString(0),
                    reader.GetString(1)));
            }
        }
        catch (DbException)
        {
            // Table may not exist if EF Core migrations are not used
        }

        return migrations;
    }
}
