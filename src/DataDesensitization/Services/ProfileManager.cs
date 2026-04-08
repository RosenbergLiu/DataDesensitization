using System.Text.Json;
using System.Text.Json.Serialization;
using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class ProfileManager : IProfileManager
{
    private readonly ISchemaIntrospector _introspector;
    private readonly IConnectionManager _connectionManager;
    private readonly string _storageDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProfileManager(
        ISchemaIntrospector introspector,
        IConnectionManager connectionManager,
        string? storageDir = null)
    {
        _introspector = introspector;
        _connectionManager = connectionManager;
        _storageDir = storageDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles");
    }

    public async Task SaveProfileAsync(string name, IReadOnlyList<DesensitizationRule> rules)
    {
        var profile = new Profile
        {
            Name = name,
            Rules = rules.ToList()
        };

        var json = JsonSerializer.Serialize(profile, JsonOptions);

        Directory.CreateDirectory(_storageDir);
        var filePath = Path.Combine(_storageDir, $"{name}.json");
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<ProfileLoadResult> LoadProfileAsync(string name)
    {
        var filePath = Path.Combine(_storageDir, $"{name}.json");
        var json = await File.ReadAllTextAsync(filePath);
        var profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize profile.");

        var tables = await _introspector.GetTablesAsync();

        var schemaColumns = new HashSet<(string Table, string Column)>();
        foreach (var table in tables)
        {
            var fullTableName = string.IsNullOrEmpty(table.SchemaName)
                ? table.TableName
                : $"{table.SchemaName}.{table.TableName}";

            var columns = await _introspector.GetColumnsAsync(fullTableName);
            foreach (var col in columns)
            {
                schemaColumns.Add((fullTableName, col.ColumnName));
            }
        }

        var matched = new List<DesensitizationRule>();
        var unmatched = new List<DesensitizationRule>();

        foreach (var rule in profile.Rules)
        {
            if (schemaColumns.Contains((rule.TableName, rule.ColumnName)))
            {
                matched.Add(rule);
            }
            else
            {
                unmatched.Add(rule);
            }
        }

        return new ProfileLoadResult(matched, unmatched);
    }

    public async Task ExportProfileAsync(string filePath, IReadOnlyList<DesensitizationRule> rules, string connectionString)
    {
        var migration = await _introspector.GetNewestMigrationAsync();

        var profile = new Profile
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            ConnectionString = connectionString,
            MigrationRecord = migration,
            Rules = rules.ToList()
        };

        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<ProfileImportResult> ImportProfileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize profile.");

        var currentMigration = await _introspector.GetNewestMigrationAsync();

        if (!MigrationRecordsMatch(profile.MigrationRecord, currentMigration))
        {
            var expected = profile.MigrationRecord is not null
                ? $"{profile.MigrationRecord.MigrationId} ({profile.MigrationRecord.ProductVersion})"
                : "none";
            var actual = currentMigration is not null
                ? $"{currentMigration.MigrationId} ({currentMigration.ProductVersion})"
                : "none";

            return new ProfileImportResult(
                false,
                $"Schema version mismatch. Profile migration: {expected}, target database migration: {actual}.",
                null);
        }

        // Match rules against current schema (same logic as LoadProfileAsync)
        var tables = await _introspector.GetTablesAsync();

        var schemaColumns = new HashSet<(string Table, string Column)>();
        foreach (var table in tables)
        {
            var fullTableName = string.IsNullOrEmpty(table.SchemaName)
                ? table.TableName
                : $"{table.SchemaName}.{table.TableName}";

            var columns = await _introspector.GetColumnsAsync(fullTableName);
            foreach (var col in columns)
            {
                schemaColumns.Add((fullTableName, col.ColumnName));
            }
        }

        var matched = new List<DesensitizationRule>();
        var unmatched = new List<DesensitizationRule>();

        foreach (var rule in profile.Rules)
        {
            if (schemaColumns.Contains((rule.TableName, rule.ColumnName)))
            {
                matched.Add(rule);
            }
            else
            {
                unmatched.Add(rule);
            }
        }

        var loadResult = new ProfileLoadResult(matched, unmatched);
        return new ProfileImportResult(true, null, loadResult);
    }

    private static bool MigrationRecordsMatch(MigrationRecord? a, MigrationRecord? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.MigrationId == b.MigrationId && a.ProductVersion == b.ProductVersion;
    }
}
