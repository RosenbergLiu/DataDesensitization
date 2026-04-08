using DataDesensitization.Models;
using DataDesensitization.Services;

namespace DataDesensitization.Tests;

public class SmokeTest
{
    [Fact]
    public void Models_CanBeInstantiated()
    {
        var connResult = new ConnectionResult(true, "TestDb", "localhost", null);
        Assert.True(connResult.Success);

        var table = new TableInfo("dbo", "Users");
        Assert.Equal("Users", table.TableName);

        var column = new ColumnInfo("Email", "nvarchar", true, 255);
        Assert.Equal("Email", column.ColumnName);

        var migration = new MigrationRecord("20240101_Init", "8.0.0");
        Assert.Equal("20240101_Init", migration.MigrationId);

        var parameters = new StrategyParameters { MinLength = 5, MaxLength = 10 };
        Assert.Equal(5, parameters.MinLength);

        var rule = new DesensitizationRule("Users", "Email", DesensitizationStrategyType.Masking, parameters);
        Assert.Equal("Users", rule.TableName);

        var validation = new ValidationResult(true, null);
        Assert.True(validation.IsValid);

        var profile = new Profile { Name = "Test", Rules = [rule] };
        Assert.Single(profile.Rules);

        var loadResult = new ProfileLoadResult([rule], []);
        Assert.Single(loadResult.MatchedRules);

        var importResult = new ProfileImportResult(true, null, loadResult);
        Assert.True(importResult.Success);

        var progress = new ProgressInfo("Users", 50, 100, TimeSpan.FromSeconds(10));
        Assert.Equal(50, progress.RowsProcessed);

        var tableResult = new TableExecutionResult("Users", 100, TimeSpan.FromSeconds(5), null);
        Assert.Equal(100, tableResult.RowsUpdated);

        var report = new ExecutionReport
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            TotalElapsed = TimeSpan.FromSeconds(5),
            TableResults = [tableResult]
        };
        Assert.Equal(100, report.TotalRowsUpdated);
        Assert.Equal(0, report.TotalErrors);

        var previewRow = new PreviewRow(
            new Dictionary<string, object?> { ["Email"] = "test@example.com" },
            new Dictionary<string, object?> { ["Email"] = "****@example.com" });
        Assert.Single(previewRow.OriginalValues);

        var preview = new PreviewResult("Users", [previewRow]);
        Assert.Single(preview.Rows);
    }

    [Fact]
    public void Enums_HaveExpectedValues()
    {
        Assert.Equal(2, Enum.GetValues<DatabaseProvider>().Length);
        Assert.Equal(4, Enum.GetValues<ConnectionStatus>().Length);
        Assert.Equal(5, Enum.GetValues<DesensitizationStrategyType>().Length);
    }
}
