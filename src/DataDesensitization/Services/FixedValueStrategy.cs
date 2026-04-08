using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class FixedValueStrategy : IDesensitizationStrategy
{
    public string Name => "FixedValue";

    public bool IsCompatibleWith(ColumnInfo column)
    {
        return true;
    }

    public object? GenerateValue(object? originalValue, ColumnInfo column, StrategyParameters parameters)
    {
        return parameters.FixedValue;
    }
}
