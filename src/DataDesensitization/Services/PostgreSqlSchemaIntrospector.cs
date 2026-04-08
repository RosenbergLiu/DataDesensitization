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
            columns.Add(new ColumnInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2) == "YES",
                reader.IsDBNull(3) ? null : reader.GetInt32(3)));
        }

        return columns;
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
}
