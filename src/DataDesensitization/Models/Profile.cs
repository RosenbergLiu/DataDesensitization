namespace DataDesensitization.Models;

public record Profile
{
    public string Name { get; init; } = string.Empty;
    public string? ConnectionString { get; init; }
    public MigrationRecord? MigrationRecord { get; init; }
    public List<DesensitizationRule> Rules { get; init; } = new();
}
