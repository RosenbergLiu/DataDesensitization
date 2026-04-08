using DataDesensitization.Models;
using DataDesensitization.Services;

namespace DataDesensitization.Tests;

public class RuleConfigurationServiceTests
{
    private readonly RuleConfigurationService _service = new();

    private static DesensitizationRule MakeRule(
        string table = "dbo.Users", string column = "Email",
        DesensitizationStrategyType strategy = DesensitizationStrategyType.Masking) =>
        new(table, column, strategy, new StrategyParameters());

    #region AddRule

    [Fact]
    public void AddRule_AddsRuleAndReturnsValid()
    {
        var rule = MakeRule();

        var result = _service.AddRule(rule);

        Assert.True(result.IsValid);
        Assert.Single(_service.Rules);
        Assert.Equal(rule, _service.Rules[0]);
    }

    [Fact]
    public void AddRule_MultipleRules_AllStored()
    {
        _service.AddRule(MakeRule(column: "Email"));
        _service.AddRule(MakeRule(column: "Phone"));

        Assert.Equal(2, _service.Rules.Count);
    }

    #endregion

    #region RemoveRule

    [Fact]
    public void RemoveRule_ExistingRule_RemovesIt()
    {
        _service.AddRule(MakeRule(column: "Email"));
        _service.AddRule(MakeRule(column: "Phone"));

        _service.RemoveRule("dbo.Users", "Email");

        Assert.Single(_service.Rules);
        Assert.Equal("Phone", _service.Rules[0].ColumnName);
    }

    [Fact]
    public void RemoveRule_CaseInsensitive()
    {
        _service.AddRule(MakeRule(table: "dbo.Users", column: "Email"));

        _service.RemoveRule("DBO.USERS", "EMAIL");

        Assert.Empty(_service.Rules);
    }

    [Fact]
    public void RemoveRule_NonExistent_NoEffect()
    {
        _service.AddRule(MakeRule(column: "Email"));

        _service.RemoveRule("dbo.Users", "NonExistent");

        Assert.Single(_service.Rules);
    }

    #endregion

    #region ValidateRule

    [Fact]
    public void ValidateRule_ForeignKeyColumn_ReturnsInvalid()
    {
        var rule = MakeRule(column: "OrderId");
        var column = new ColumnInfo("OrderId", "int", false, null, IsForeignKey: true, ReferencedTable: "Orders");

        var result = _service.ValidateRule(rule, column);

        Assert.False(result.IsValid);
        Assert.Contains("foreign key", result.ErrorMessage);
        Assert.Contains("Orders", result.ErrorMessage);
    }

    [Fact]
    public void ValidateRule_ForeignKeyColumn_NoReferencedTable_StillInvalid()
    {
        var rule = MakeRule(column: "OrderId");
        var column = new ColumnInfo("OrderId", "int", false, null, IsForeignKey: true);

        var result = _service.ValidateRule(rule, column);

        Assert.False(result.IsValid);
        Assert.Contains("foreign key", result.ErrorMessage);
    }

    [Fact]
    public void ValidateRule_IncompatibleStrategy_ReturnsInvalid()
    {
        var rule = MakeRule(column: "Age", strategy: DesensitizationStrategyType.Masking);
        var column = new ColumnInfo("Age", "int", true, null);

        var result = _service.ValidateRule(rule, column);

        Assert.False(result.IsValid);
        Assert.Contains("not compatible", result.ErrorMessage);
    }

    [Fact]
    public void ValidateRule_NullificationOnNonNullable_ReturnsInvalid()
    {
        var rule = MakeRule(column: "Id", strategy: DesensitizationStrategyType.Nullification);
        var column = new ColumnInfo("Id", "int", false, null);

        var result = _service.ValidateRule(rule, column);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRule_CompatibleRule_ReturnsValid()
    {
        var rule = MakeRule(column: "Email", strategy: DesensitizationStrategyType.Masking);
        var column = new ColumnInfo("Email", "nvarchar", true, 255);

        var result = _service.ValidateRule(rule, column);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    #endregion

    #region AutoDetectRules

    [Fact]
    public void AutoDetectRules_DetectsSensitiveColumns()
    {
        var tables = new List<TableInfo> { new("dbo", "Users") };
        var columns = new Dictionary<string, List<ColumnInfo>>
        {
            ["dbo.Users"] =
            [
                new("Id", "int", false, null),
                new("first_name", "nvarchar", true, 100),
                new("email", "nvarchar", true, 255),
                new("phone", "nvarchar", true, 20),
                new("ssn", "nvarchar", true, 11),
                new("credit_card", "nvarchar", true, 20),
                new("password", "nvarchar", true, 100),
            ]
        };

        var rules = _service.AutoDetectRules(tables, columns);

        Assert.Equal(6, rules.Count); // All except Id
        Assert.Contains(rules, r => r.ColumnName == "first_name" && r.Strategy == DesensitizationStrategyType.Randomization);
        Assert.Contains(rules, r => r.ColumnName == "email" && r.Strategy == DesensitizationStrategyType.Masking);
        Assert.Contains(rules, r => r.ColumnName == "ssn" && r.Strategy == DesensitizationStrategyType.Nullification);
        Assert.Contains(rules, r => r.ColumnName == "password" && r.Strategy == DesensitizationStrategyType.Nullification);
    }

    [Fact]
    public void AutoDetectRules_SkipsForeignKeyColumns()
    {
        var tables = new List<TableInfo> { new("dbo", "Orders") };
        var columns = new Dictionary<string, List<ColumnInfo>>
        {
            ["dbo.Orders"] =
            [
                new("Id", "int", false, null),
                new("customer_name", "nvarchar", true, 100, IsForeignKey: true),
            ]
        };

        var rules = _service.AutoDetectRules(tables, columns);

        Assert.Empty(rules);
    }

    [Fact]
    public void AutoDetectRules_NoSensitiveColumns_ReturnsEmpty()
    {
        var tables = new List<TableInfo> { new("dbo", "Config") };
        var columns = new Dictionary<string, List<ColumnInfo>>
        {
            ["dbo.Config"] =
            [
                new("Id", "int", false, null),
                new("SettingKey", "nvarchar", true, 100),
                new("SettingValue", "nvarchar", true, 500),
            ]
        };

        var rules = _service.AutoDetectRules(tables, columns);

        Assert.Empty(rules);
    }

    [Fact]
    public void AutoDetectRules_TableNotInColumnsDictionary_Skipped()
    {
        var tables = new List<TableInfo> { new("dbo", "Missing") };
        var columns = new Dictionary<string, List<ColumnInfo>>();

        var rules = _service.AutoDetectRules(tables, columns);

        Assert.Empty(rules);
    }

    #endregion
}
