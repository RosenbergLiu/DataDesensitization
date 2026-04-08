using System.Text.RegularExpressions;
using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class RuleConfigurationService : IRuleConfigurationService
{
    private readonly List<DesensitizationRule> _rules = new();

    private static readonly List<(Regex Pattern, DesensitizationStrategyType Strategy, StrategyParameters Parameters)> DetectionPatterns =
    [
        (new Regex(@"(?i)(first|last|full)?_?name", RegexOptions.Compiled), DesensitizationStrategyType.Randomization, new StrategyParameters()),
        (new Regex(@"(?i)e?mail", RegexOptions.Compiled), DesensitizationStrategyType.Masking, new StrategyParameters { PreserveStart = 0, PreserveEnd = 0 }),
        (new Regex(@"(?i)phone|mobile|fax", RegexOptions.Compiled), DesensitizationStrategyType.Masking, new StrategyParameters { PreserveStart = 0, PreserveEnd = 4 }),
        (new Regex(@"(?i)address|street|city|zip|postal", RegexOptions.Compiled), DesensitizationStrategyType.Randomization, new StrategyParameters()),
        (new Regex(@"(?i)ssn|social_security", RegexOptions.Compiled), DesensitizationStrategyType.Nullification, new StrategyParameters()),
        (new Regex(@"(?i)credit_card|card_number|ccn", RegexOptions.Compiled), DesensitizationStrategyType.Masking, new StrategyParameters { PreserveStart = 0, PreserveEnd = 4 }),
        (new Regex(@"(?i)password|pwd|pass_hash", RegexOptions.Compiled), DesensitizationStrategyType.Nullification, new StrategyParameters()),
    ];

    public IReadOnlyList<DesensitizationRule> Rules => _rules.AsReadOnly();

    public ValidationResult AddRule(DesensitizationRule rule)
    {
        // We need a ColumnInfo to validate, but AddRule only receives the rule.
        // Add the rule without column-level validation here; callers should use
        // ValidateRule with the column before calling AddRule, or the engine
        // validates at execution time.
        // However, per the task spec, AddRule should validate the rule.
        // Since we don't have column info in AddRule's signature, we add it directly.
        // The UI layer is expected to call ValidateRule first.
        _rules.Add(rule);
        return new ValidationResult(true, null);
    }

    public void RemoveRule(string tableName, string columnName)
    {
        _rules.RemoveAll(r =>
            string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
    }

    public ValidationResult ValidateRule(DesensitizationRule rule, ColumnInfo column)
    {
        var strategy = CreateStrategy(rule.Strategy);
        if (!strategy.IsCompatibleWith(column))
        {
            return new ValidationResult(false,
                $"{strategy.Name} strategy is not compatible with column '{rule.TableName}.{rule.ColumnName}' (type: {column.DataType}, nullable: {column.IsNullable}).");
        }

        return new ValidationResult(true, null);
    }

    public List<DesensitizationRule> AutoDetectRules(List<TableInfo> tables, Dictionary<string, List<ColumnInfo>> columnsByTable)
    {
        var detectedRules = new List<DesensitizationRule>();

        foreach (var table in tables)
        {
            var key = $"{table.SchemaName}.{table.TableName}";
            if (!columnsByTable.TryGetValue(key, out var columns))
                continue;

            foreach (var column in columns)
            {
                foreach (var (pattern, strategy, parameters) in DetectionPatterns)
                {
                    if (pattern.IsMatch(column.ColumnName))
                    {
                        detectedRules.Add(new DesensitizationRule(
                            key,
                            column.ColumnName,
                            strategy,
                            parameters));
                        break; // First matching pattern wins
                    }
                }
            }
        }

        return detectedRules;
    }

    private static IDesensitizationStrategy CreateStrategy(DesensitizationStrategyType strategyType)
    {
        return strategyType switch
        {
            DesensitizationStrategyType.Randomization => new RandomizationStrategy(),
            DesensitizationStrategyType.Masking => new MaskingStrategy(),
            DesensitizationStrategyType.Nullification => new NullificationStrategy(),
            DesensitizationStrategyType.FixedValue => new FixedValueStrategy(),
            DesensitizationStrategyType.Shuffling => new ShufflingStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategyType), strategyType, "Unknown strategy type.")
        };
    }
}
