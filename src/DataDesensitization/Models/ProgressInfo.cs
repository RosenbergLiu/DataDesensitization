namespace DataDesensitization.Models;

public record ProgressInfo(
    string CurrentTable,
    long RowsProcessed,
    long TotalRows,
    TimeSpan? EstimatedTimeRemaining);
