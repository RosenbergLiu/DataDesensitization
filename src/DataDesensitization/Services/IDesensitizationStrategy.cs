using DataDesensitization.Models;

namespace DataDesensitization.Services;

public interface IDesensitizationStrategy
{
    string Name { get; }
    bool IsCompatibleWith(ColumnInfo column);
    object? GenerateValue(object? originalValue, ColumnInfo column, StrategyParameters parameters);
}
