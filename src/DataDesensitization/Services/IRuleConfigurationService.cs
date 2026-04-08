using DataDesensitization.Models;

namespace DataDesensitization.Services;

public interface IRuleConfigurationService
{
    IReadOnlyList<DesensitizationRule> Rules { get; }
    ValidationResult AddRule(DesensitizationRule rule);
    void RemoveRule(string tableName, string columnName);
    ValidationResult ValidateRule(DesensitizationRule rule, ColumnInfo column);
    List<DesensitizationRule> AutoDetectRules(List<TableInfo> tables, Dictionary<string, List<ColumnInfo>> columnsByTable);
}
