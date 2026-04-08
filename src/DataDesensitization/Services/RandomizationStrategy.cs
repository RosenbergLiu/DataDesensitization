using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class RandomizationStrategy : IDesensitizationStrategy
{
    private const int DefaultMinLength = 1;
    private const int DefaultMaxLength = 10;

    private static readonly HashSet<string> TextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "nvarchar", "varchar", "text", "char", "nchar", "ntext", "character varying"
    };

    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "bigint", "smallint", "tinyint", "decimal", "numeric",
        "float", "real", "double precision", "integer"
    };

    private readonly Random _random;

    public RandomizationStrategy()
        : this(new Random())
    {
    }

    internal RandomizationStrategy(Random random)
    {
        _random = random;
    }

    public string Name => "Randomization";

    public bool IsCompatibleWith(ColumnInfo column)
    {
        return TextTypes.Contains(column.DataType) || NumericTypes.Contains(column.DataType);
    }

    public object? GenerateValue(object? originalValue, ColumnInfo column, StrategyParameters parameters)
    {
        if (TextTypes.Contains(column.DataType))
        {
            return GenerateRandomString(parameters);
        }

        if (NumericTypes.Contains(column.DataType))
        {
            return GenerateRandomNumber(column.DataType);
        }

        throw new InvalidOperationException(
            $"Randomization strategy is not compatible with column data type '{column.DataType}'.");
    }

    private string GenerateRandomString(StrategyParameters parameters)
    {
        var minLength = parameters.MinLength ?? DefaultMinLength;
        var maxLength = parameters.MaxLength ?? DefaultMaxLength;

        if (minLength < 0) minLength = DefaultMinLength;
        if (maxLength < minLength) maxLength = minLength;

        var length = _random.Next(minLength, maxLength + 1);
        return new string(Enumerable.Range(0, length)
            .Select(_ => (char)_random.Next('a', 'z' + 1))
            .ToArray());
    }

    private object GenerateRandomNumber(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "int" or "integer" => _random.Next(),
            "bigint" => (long)_random.Next(),
            "smallint" => (short)_random.Next(short.MinValue, short.MaxValue + 1),
            "tinyint" => (byte)_random.Next(0, 256),
            "decimal" or "numeric" => (decimal)Math.Round(_random.NextDouble() * 10000, 2),
            "float" or "double precision" => _random.NextDouble() * 10000,
            "real" => (float)(_random.NextDouble() * 10000),
            _ => _random.Next()
        };
    }
}
