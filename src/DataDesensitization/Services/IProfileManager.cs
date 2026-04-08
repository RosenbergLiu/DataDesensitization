using DataDesensitization.Models;

namespace DataDesensitization.Services;

public interface IProfileManager
{
    Task<byte[]> ExportProfileAsync(string name, IReadOnlyList<DesensitizationRule> rules);
    Task<ProfileImportResult> ImportProfileAsync(byte[] jsonBytes);
}
