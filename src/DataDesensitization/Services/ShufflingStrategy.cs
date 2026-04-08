using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class ShufflingStrategy : IDesensitizationStrategy
{
    public string Name => "Shuffling";

    public bool IsCompatibleWith(ColumnInfo column)
    {
        return true;
    }

    public object? GenerateValue(object? originalValue, ColumnInfo column, StrategyParameters parameters)
    {
        // Shuffling returns the original value per-row.
        // The actual redistribution of values across rows is handled at the engine level.
        return originalValue;
    }
}
