using DataDesensitization.Models;

namespace DataDesensitization.Services;

public interface IReportSerializer
{
    string Serialize(ExecutionReport report);
    ExecutionReport Deserialize(string json);
    Task ExportToFileAsync(ExecutionReport report, string filePath);
    Task<ExecutionReport> ImportFromFileAsync(string filePath);
}
