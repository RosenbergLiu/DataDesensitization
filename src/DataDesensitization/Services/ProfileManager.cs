using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class ProfileManager : IProfileManager
{
    private readonly ISchemaIntrospector _introspector;
    private static readonly byte[] HashKey = Encoding.UTF8.GetBytes("123456789");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProfileManager(ISchemaIntrospector introspector)
    {
        _introspector = introspector;
    }

    public async Task<byte[]> ExportProfileAsync(string name, IReadOnlyList<DesensitizationRule> rules)
    {
        var migrations = await _introspector.GetAllMigrationsAsync();
        var hash = ComputeMigrationHash(migrations);

        var profile = new Profile
        {
            Name = name,
            MigrationHistoryHash = hash,
            Rules = rules.ToList()
        };

        return JsonSerializer.SerializeToUtf8Bytes(profile, JsonOptions);
    }

    public async Task<ProfileImportResult> ImportProfileAsync(byte[] jsonBytes)
    {
        var profile = JsonSerializer.Deserialize<Profile>(jsonBytes, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize profile.");

        var currentMigrations = await _introspector.GetAllMigrationsAsync();

        // Support both hashed and legacy raw migration history
        if (profile.MigrationHistoryHash is not null)
        {
            var currentHash = ComputeMigrationHash(currentMigrations);
            if (!string.Equals(profile.MigrationHistoryHash, currentHash, StringComparison.Ordinal))
            {
                return new ProfileImportResult(
                    false,
                    "Migration history mismatch. The hashed migration history in the profile does not match the current database.",
                    null);
            }
        }
        else if (!MigrationHistoriesMatch(profile.MigrationHistory, currentMigrations))
        {
            var expected = profile.MigrationHistory.Count > 0
                ? $"{profile.MigrationHistory.Count} migration(s), latest: {profile.MigrationHistory[^1].MigrationId}"
                : "none";
            var actual = currentMigrations.Count > 0
                ? $"{currentMigrations.Count} migration(s), latest: {currentMigrations[^1].MigrationId}"
                : "none";

            return new ProfileImportResult(
                false,
                $"Migration history mismatch. Profile: {expected}. Database: {actual}.",
                null);
        }

        // Match rules against current schema
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
                matched.Add(rule);
            else
                unmatched.Add(rule);
        }

        var loadResult = new ProfileLoadResult(matched, unmatched);
        return new ProfileImportResult(true, null, loadResult);
    }

    private static string ComputeMigrationHash(List<MigrationRecord> migrations)
    {
        var payload = JsonSerializer.Serialize(
            migrations.Select(m => new { m.MigrationId, m.ProductVersion }),
            JsonOptions);

        using var hmac = new HMACSHA256(HashKey);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes);
    }

    private static bool MigrationHistoriesMatch(List<MigrationRecord> exported, List<MigrationRecord> current)
    {
        if (exported.Count == 0 && current.Count == 0) return true;
        if (exported.Count != current.Count) return false;

        for (int i = 0; i < exported.Count; i++)
        {
            if (exported[i].MigrationId != current[i].MigrationId ||
                exported[i].ProductVersion != current[i].ProductVersion)
                return false;
        }

        return true;
    }
}
