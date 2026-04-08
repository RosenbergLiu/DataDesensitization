using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class MaskingStrategy : IDesensitizationStrategy
{
    private static readonly HashSet<string> TextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "nvarchar", "varchar", "text", "char", "nchar", "ntext", "character varying"
    };

    public string Name => "Masking";

    public bool IsCompatibleWith(ColumnInfo column)
    {
        return TextTypes.Contains(column.DataType);
    }

    public object? GenerateValue(object? originalValue, ColumnInfo column, StrategyParameters parameters)
    {
        if (originalValue is null or DBNull)
            return originalValue;

        var original = originalValue.ToString();
        if (string.IsNullOrEmpty(original))
            return original;

        var preserveStart = parameters.PreserveStart;
        var preserveEnd = parameters.PreserveEnd;
        var maskChar = parameters.MaskCharacter;

        if (preserveStart + preserveEnd >= original.Length)
            return original;

        var masked = new char[original.Length];

        for (var i = 0; i < preserveStart; i++)
            masked[i] = original[i];

        for (var i = preserveStart; i < original.Length - preserveEnd; i++)
            masked[i] = maskChar;

        for (var i = original.Length - preserveEnd; i < original.Length; i++)
            masked[i] = original[i];

        return new string(masked);
    }
}
