using System.Data.Common;
using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class SqlServerSchemaIntrospector : ISchemaIntrospector
{
    private readonly DbConnection _connection;

    public SqlServerSchemaIntrospector(DbConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<TableInfo>> GetTablesAsync(CancellationToken ct = default)
    {
        var tables = new List<TableInfo>();

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME";

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

        // Support schema-qualified names like "dbo.Users"
        var parts = tableName.Split('.', 2);
        var schema = parts.Length > 1 ? parts[0] : null;
        var table = parts.Length > 1 ? parts[1] : parts[0];

        using var command = _connection.CreateCommand();

        if (schema is not null)
        {
            command.CommandText = @"
                SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";

            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@Schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);
        }
        else
        {
            command.CommandText = @"
                SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";
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
                reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3))));
        }

        return columns;
    }

    public async Task<MigrationRecord?> GetNewestMigrationAsync(CancellationToken ct = default)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT TOP 1 MigrationId, ProductVersion
            FROM __EFMigrationsHistory
            ORDER BY MigrationId DESC";

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
