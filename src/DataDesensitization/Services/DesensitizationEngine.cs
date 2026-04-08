using System.Data;
using System.Data.Common;
using System.Diagnostics;
using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class DesensitizationEngine : IDesensitizationEngine
{
    private readonly IConnectionManager _connectionManager;
    private readonly ISchemaIntrospector _schemaIntrospector;

    public DesensitizationEngine(IConnectionManager connectionManager, ISchemaIntrospector schemaIntrospector)
    {
        _connectionManager = connectionManager;
        _schemaIntrospector = schemaIntrospector;
    }

    public event Action<ProgressInfo>? ProgressChanged;

    public async Task<ExecutionReport> ExecuteAsync(
        IReadOnlyList<DesensitizationRule> rules,
        CancellationToken ct = default)
    {
        if (rules.Count == 0)
            throw new InvalidOperationException("At least one desensitization rule must be configured before execution.");

        var connection = _connectionManager.CurrentConnection
            ?? throw new InvalidOperationException("No active database connection.");

        var startedAt = DateTime.UtcNow;
        var tableResults = new List<TableExecutionResult>();
        var rulesByTable = rules.GroupBy(r => r.TableName).ToList();

        foreach (var tableGroup in rulesByTable)
        {
            if (ct.IsCancellationRequested)
                break;

            var tableName = tableGroup.Key;
            var tableRules = tableGroup.ToList();
            var tableStopwatch = Stopwatch.StartNew();

            // Get column metadata before starting the transaction so the
            // introspector commands don't conflict with the pending transaction.
            var columns = await _schemaIntrospector.GetColumnsAsync(tableName, ct);
            var columnLookup = columns.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

            DbTransaction? transaction = null;
            try
            {
                transaction = await connection.BeginTransactionAsync(ct);

                // Build column names list from rules
                var columnNames = tableRules.Select(r => r.ColumnName).ToList();

                // Get primary key columns for reliable row identification in UPDATE WHERE clauses
                var pkColumns = await GetPrimaryKeyColumnsAsync(connection, tableName, transaction, ct);

                // Count total rows for progress reporting
                var totalRows = await CountRowsAsync(connection, tableName, transaction, ct);

                // Read all rows
                var rows = await ReadRowsAsync(connection, tableName, columnNames, transaction, ct);

                // For shuffling strategies, collect and shuffle values
                var shuffledValues = PrepareShuffledValues(tableRules, rows);

                // Apply strategies and update each row
                long rowsProcessed = 0;
                var progressStopwatch = Stopwatch.StartNew();

                foreach (var row in rows)
                {
                    ct.ThrowIfCancellationRequested();

                    var updatedValues = new Dictionary<string, object?>();

                    for (var i = 0; i < tableRules.Count; i++)
                    {
                        var rule = tableRules[i];
                        var columnName = rule.ColumnName;
                        var originalValue = row.ContainsKey(columnName) ? row[columnName] : null;

                        if (rule.Strategy == DesensitizationStrategyType.Shuffling
                            && shuffledValues.TryGetValue(columnName, out var shuffled))
                        {
                            updatedValues[columnName] = shuffled[(int)rowsProcessed];
                        }
                        else
                        {
                            var strategy = CreateStrategy(rule.Strategy);
                            var columnInfo = columnLookup.GetValueOrDefault(columnName)
                                ?? new ColumnInfo(columnName, "nvarchar", true, null);
                            updatedValues[columnName] = strategy.GenerateValue(originalValue, columnInfo, rule.Parameters);
                        }
                    }

                    await UpdateRowAsync(connection, tableName, row, updatedValues, pkColumns, transaction, ct);

                    rowsProcessed++;

                    // Fire progress event
                    var elapsed = progressStopwatch.Elapsed;
                    var estimatedRemaining = rowsProcessed > 0
                        ? TimeSpan.FromTicks(elapsed.Ticks * (totalRows - rowsProcessed) / rowsProcessed)
                        : (TimeSpan?)null;

                    ProgressChanged?.Invoke(new ProgressInfo(
                        tableName,
                        rowsProcessed,
                        totalRows,
                        estimatedRemaining));
                }

                await transaction.CommitAsync(ct);
                tableStopwatch.Stop();

                tableResults.Add(new TableExecutionResult(
                    tableName,
                    rowsProcessed,
                    tableStopwatch.Elapsed,
                    null));
            }
            catch (OperationCanceledException)
            {
                // Cancellation: rollback and return partial report
                if (transaction is not null)
                {
                    try { await transaction.RollbackAsync(); }
                    catch { /* best-effort rollback */ }
                }

                tableStopwatch.Stop();
                break;
            }
            catch (Exception ex)
            {
                // Error: rollback this table, capture error, continue to next
                if (transaction is not null)
                {
                    try { await transaction.RollbackAsync(); }
                    catch { /* best-effort rollback */ }
                }

                tableStopwatch.Stop();
                tableResults.Add(new TableExecutionResult(
                    tableName,
                    0,
                    tableStopwatch.Elapsed,
                    ex.Message));
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        var completedAt = DateTime.UtcNow;

        return new ExecutionReport
        {
            StartedAt = startedAt,
            CompletedAt = completedAt,
            TotalElapsed = completedAt - startedAt,
            TableResults = tableResults
        };
    }

    public async Task<PreviewResult> PreviewAsync(
        string tableName,
        IReadOnlyList<DesensitizationRule> tableRules,
        int maxRows = 10,
        CancellationToken ct = default)
    {
        var connection = _connectionManager.CurrentConnection
            ?? throw new InvalidOperationException("No active database connection.");

        // Get column metadata for strategy creation
        var columns = await _schemaIntrospector.GetColumnsAsync(tableName, ct);
        var columnLookup = columns.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

        // Read up to maxRows — read-only, no transaction needed
        var ruleColumnNames = tableRules.Select(r => r.ColumnName).ToList();
        var rows = await ReadPreviewRowsAsync(connection, tableName, ruleColumnNames, maxRows, ct);

        // For shuffling strategies, collect and shuffle values from the preview set
        var shuffledValues = PrepareShuffledValues(tableRules.ToList(), rows);

        var previewRows = new List<PreviewRow>();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            ct.ThrowIfCancellationRequested();

            var row = rows[rowIndex];
            var originalValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var desensitizedValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < tableRules.Count; i++)
            {
                var rule = tableRules[i];
                var columnName = rule.ColumnName;
                var originalValue = row.GetValueOrDefault(columnName);

                originalValues[columnName] = originalValue;

                if (rule.Strategy == DesensitizationStrategyType.Shuffling
                    && shuffledValues.TryGetValue(columnName, out var shuffled))
                {
                    desensitizedValues[columnName] = shuffled[rowIndex];
                }
                else
                {
                    var strategy = CreateStrategy(rule.Strategy);
                    var columnInfo = columnLookup.GetValueOrDefault(columnName)
                        ?? new ColumnInfo(columnName, "nvarchar", true, null);
                    desensitizedValues[columnName] = strategy.GenerateValue(originalValue, columnInfo, rule.Parameters);
                }
            }

            previewRows.Add(new PreviewRow(originalValues, desensitizedValues));
        }

        return new PreviewResult(tableName, previewRows);
    }

    private static IDesensitizationStrategy CreateStrategy(DesensitizationStrategyType strategyType)
    {
        return strategyType switch
        {
            DesensitizationStrategyType.Randomization => new RandomizationStrategy(),
            DesensitizationStrategyType.Masking => new MaskingStrategy(),
            DesensitizationStrategyType.Nullification => new NullificationStrategy(),
            DesensitizationStrategyType.FixedValue => new FixedValueStrategy(),
            DesensitizationStrategyType.Shuffling => new ShufflingStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategyType), strategyType, "Unknown strategy type.")
        };
    }

    private static async Task<long> CountRowsAsync(
        DbConnection connection, string tableName,
        DbTransaction transaction, CancellationToken ct)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"SELECT COUNT(*) FROM {QuoteIdentifier(tableName)}";
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Retrieves the primary key column names for the given table.
    /// Works for both SQL Server and PostgreSQL.
    /// </summary>
    private static async Task<List<string>> GetPrimaryKeyColumnsAsync(
        DbConnection connection, string tableName,
        DbTransaction transaction, CancellationToken ct)
    {
        var parts = tableName.Split('.', 2);
        var schema = parts.Length > 1 ? parts[0] : null;
        var table = parts.Length > 1 ? parts[1] : parts[0];

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;

        // ANSI INFORMATION_SCHEMA query that works on both SQL Server and PostgreSQL
        if (schema is not null)
        {
            cmd.CommandText = @"
                SELECT kcu.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                  ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                 AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                 AND tc.TABLE_NAME = kcu.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                  AND tc.TABLE_SCHEMA = @Schema
                  AND tc.TABLE_NAME = @TableName
                ORDER BY kcu.ORDINAL_POSITION";

            var schemaParam = cmd.CreateParameter();
            schemaParam.ParameterName = "@Schema";
            schemaParam.Value = schema;
            cmd.Parameters.Add(schemaParam);
        }
        else
        {
            cmd.CommandText = @"
                SELECT kcu.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                  ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                 AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                 AND tc.TABLE_NAME = kcu.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                  AND tc.TABLE_NAME = @TableName
                ORDER BY kcu.ORDINAL_POSITION";
        }

        var tableParam = cmd.CreateParameter();
        tableParam.ParameterName = "@TableName";
        tableParam.Value = table;
        cmd.Parameters.Add(tableParam);

        var pkColumns = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            pkColumns.Add(reader.GetString(0));
        }

        return pkColumns;
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(
        DbConnection connection, string tableName,
        List<string> columnNames, DbTransaction transaction,
        CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;

        cmd.CommandText = $"SELECT * FROM {QuoteIdentifier(tableName)}";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[name] = value;
            }
            rows.Add(row);
        }

        return rows;
    }

    private static Dictionary<string, List<object?>> PrepareShuffledValues(
        List<DesensitizationRule> tableRules,
        List<Dictionary<string, object?>> rows)
    {
        var shuffled = new Dictionary<string, List<object?>>(StringComparer.OrdinalIgnoreCase);
        var rng = new Random();

        foreach (var rule in tableRules.Where(r => r.Strategy == DesensitizationStrategyType.Shuffling))
        {
            var values = rows
                .Select(r => r.GetValueOrDefault(rule.ColumnName))
                .OrderBy(_ => rng.Next())
                .ToList();
            shuffled[rule.ColumnName] = values;
        }

        return shuffled;
    }

    private static async Task UpdateRowAsync(
        DbConnection connection, string tableName,
        Dictionary<string, object?> originalRow,
        Dictionary<string, object?> updatedValues,
        List<string> pkColumns,
        DbTransaction transaction, CancellationToken ct)
    {
        if (updatedValues.Count == 0)
            return;

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;

        // Build SET clause
        var setClauses = new List<string>();
        var paramIndex = 0;

        foreach (var (columnName, newValue) in updatedValues)
        {
            var paramName = $"@p{paramIndex}";
            setClauses.Add($"{QuoteIdentifier(columnName)} = {paramName}");

            var param = cmd.CreateParameter();
            param.ParameterName = paramName;
            param.Value = newValue ?? DBNull.Value;
            cmd.Parameters.Add(param);
            paramIndex++;
        }

        // Build WHERE clause using primary key columns when available,
        // otherwise fall back to all columns (excluding non-comparable types).
        var whereColumns = pkColumns.Count > 0
            ? pkColumns
            : originalRow.Keys.ToList();

        var whereClauses = new List<string>();
        foreach (var columnName in whereColumns)
        {
            if (!originalRow.TryGetValue(columnName, out var originalValue))
                continue;

            var paramName = $"@w{paramIndex}";
            if (originalValue is null)
            {
                whereClauses.Add($"{QuoteIdentifier(columnName)} IS NULL");
            }
            else if (originalValue is byte[])
            {
                // Skip binary columns — they can't be reliably compared with =
                continue;
            }
            else
            {
                whereClauses.Add($"{QuoteIdentifier(columnName)} = {paramName}");
                var param = cmd.CreateParameter();
                param.ParameterName = paramName;
                param.Value = originalValue;
                cmd.Parameters.Add(param);
            }
            paramIndex++;
        }

        if (whereClauses.Count == 0)
            return;

        cmd.CommandText = $"UPDATE {QuoteIdentifier(tableName)} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}";

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<Dictionary<string, object?>>> ReadPreviewRowsAsync(
        DbConnection connection, string tableName,
        List<string> columnNames, int maxRows,
        CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();

        using var cmd = connection.CreateCommand();

        var quotedColumns = columnNames.Select(QuoteIdentifier);
        var columnList = string.Join(", ", quotedColumns);

        // Use standard SQL FETCH FIRST which works across providers;
        // fall back to a simple TOP-style if needed. ANSI SQL FETCH FIRST is widely supported.
        cmd.CommandText = $"SELECT {columnList} FROM {QuoteIdentifier(tableName)} ORDER BY 1 OFFSET 0 ROWS FETCH NEXT {maxRows} ROWS ONLY";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[name] = value;
            }
            rows.Add(row);
        }

        return rows;
    }

    private static string QuoteIdentifier(string identifier)
    {
        // Handle schema-qualified names like "dbo.Users" → "dbo"."Users"
        var parts = identifier.Split('.', 2);
        if (parts.Length == 2)
        {
            return $"\"{parts[0].Replace("\"", "\"\"")}\".\"{parts[1].Replace("\"", "\"\"")}\"";
        }

        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
