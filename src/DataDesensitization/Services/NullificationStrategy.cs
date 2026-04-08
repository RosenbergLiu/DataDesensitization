using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class NullificationStrategy : IDesensitizationStrategy
{
    public string Name => "Nullification";

    public bool IsCompatibleWith(ColumnInfo column)
    {
        return column.IsNullable;
    }

    public object? GenerateValue(object? originalValue, ColumnInfo column, StrategyParameters parameters)
    {
        return DBNull.Value;
    }
}
