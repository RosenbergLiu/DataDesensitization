namespace DataDesensitization.Models;

public record ExecutionReport
{
    public DateTime StartedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public TimeSpan TotalElapsed { get; init; }
    public List<TableExecutionResult> TableResults { get; init; } = new();
    public int TotalRowsUpdated => TableResults.Where(r => r.Error == null).Sum(r => (int)r.RowsUpdated);
    public int TotalErrors => TableResults.Count(r => r.Error != null);
}
