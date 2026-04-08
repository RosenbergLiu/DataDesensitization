using DataDesensitization.Models;

namespace DataDesensitization.Services;

public interface IDesensitizationEngine
{
    Task<ExecutionReport> ExecuteAsync(IReadOnlyList<DesensitizationRule> rules, CancellationToken ct = default);
    Task<PreviewResult> PreviewAsync(string tableName, IReadOnlyList<DesensitizationRule> tableRules, int maxRows = 10, CancellationToken ct = default);
    event Action<ProgressInfo>? ProgressChanged;
}
