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

        // First, collect foreign key columns for this table
        var fkColumns = await GetForeignKeyColumnsAsync(schema, table, ct);

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
            var columnName = reader.GetString(0);
            fkColumns.TryGetValue(columnName, out var referencedTable);

            columns.Add(new ColumnInfo(
                columnName,
                reader.GetString(1),
                reader.GetString(2) == "YES",
                reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
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
                    ccu_fk.COLUMN_NAME,
                    ccu_pk.TABLE_SCHEMA + '.' + ccu_pk.TABLE_NAME
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ccu_fk
                    ON rc.CONSTRAINT_NAME = ccu_fk.CONSTRAINT_NAME
                    AND rc.CONSTRAINT_SCHEMA = ccu_fk.CONSTRAINT_SCHEMA
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ccu_pk
                    ON rc.UNIQUE_CONSTRAINT_NAME = ccu_pk.CONSTRAINT_NAME
                    AND rc.UNIQUE_CONSTRAINT_SCHEMA = ccu_pk.CONSTRAINT_SCHEMA
                WHERE ccu_fk.TABLE_SCHEMA = @Schema AND ccu_fk.TABLE_NAME = @TableName";

            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@Schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);
        }
        else
        {
            command.CommandText = @"
                SELECT
                    ccu_fk.COLUMN_NAME,
                    ccu_pk.TABLE_SCHEMA + '.' + ccu_pk.TABLE_NAME
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ccu_fk
                    ON rc.CONSTRAINT_NAME = ccu_fk.CONSTRAINT_NAME
                    AND rc.CONSTRAINT_SCHEMA = ccu_fk.CONSTRAINT_SCHEMA
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ccu_pk
                    ON rc.UNIQUE_CONSTRAINT_NAME = ccu_pk.CONSTRAINT_NAME
                    AND rc.UNIQUE_CONSTRAINT_SCHEMA = ccu_pk.CONSTRAINT_SCHEMA
                WHERE ccu_fk.TABLE_NAME = @TableName";
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

    public async Task<List<MigrationRecord>> GetAllMigrationsAsync(CancellationToken ct = default)
    {
        var migrations = new List<MigrationRecord>();

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT MigrationId, ProductVersion
            FROM __EFMigrationsHistory
            ORDER BY MigrationId";

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
