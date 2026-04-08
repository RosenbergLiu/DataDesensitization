namespace DataDesensitization.Models;

public record TableExecutionResult(
    string TableName,
    long RowsUpdated,
    TimeSpan Elapsed,
    string? Error);
