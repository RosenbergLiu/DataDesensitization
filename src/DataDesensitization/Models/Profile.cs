namespace DataDesensitization.Models;

public record Profile
{
    public string Name { get; init; } = string.Empty;
    public List<MigrationRecord> MigrationHistory { get; init; } = new();
    public List<DesensitizationRule> Rules { get; init; } = new();
}
