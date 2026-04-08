namespace DataDesensitization.Models;

public record DesensitizationRule(
    string TableName,
    string ColumnName,
    DesensitizationStrategyType Strategy,
    StrategyParameters Parameters);
