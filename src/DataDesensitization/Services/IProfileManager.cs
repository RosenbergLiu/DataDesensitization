using DataDesensitization.Models;

namespace DataDesensitization.Services;

public interface IProfileManager
{
    Task SaveProfileAsync(string name, IReadOnlyList<DesensitizationRule> rules);
    Task<ProfileLoadResult> LoadProfileAsync(string name);
    Task ExportProfileAsync(string filePath, IReadOnlyList<DesensitizationRule> rules, string connectionString);
    Task<ProfileImportResult> ImportProfileAsync(string filePath);
}
