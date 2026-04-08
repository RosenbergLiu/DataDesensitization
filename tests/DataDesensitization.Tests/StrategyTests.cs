using DataDesensitization.Models;
using DataDesensitization.Services;

namespace DataDesensitization.Tests;

/// <summary>
/// Unit tests for all desensitization strategies.
/// Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5
/// </summary>
public class RandomizationStrategyTests
{
    private readonly RandomizationStrategy _strategy = new();

    [Fact]
    public void GenerateValue_TextColumn_ReturnsStringOfCorrectLength()
    {
        var column = new ColumnInfo("Name", "nvarchar", true, 100);
        var parameters = new StrategyParameters { MinLength = 5, MaxLength = 10 };

        var result = _strategy.GenerateValue("original", column, parameters);

        Assert.IsType<string>(result);
        var str = (string)result;
        Assert.InRange(str.Length, 5, 10);
    }

    [Fact]
    public void GenerateValue_NumericColumn_ReturnsNumericValue()
    {
        var column = new ColumnInfo("Age", "int", true, null);
        var parameters = new StrategyParameters();

        var result = _strategy.GenerateValue(42, column, parameters);

        Assert.IsType<int>(result);
    }

    [Theory]
    [InlineData("datetime")]
    [InlineData("bit")]
    [InlineData("uniqueidentifier")]
    public void IsCompatibleWith_UnsupportedType_ReturnsFalse(string dataType)
    {
        var column = new ColumnInfo("Col", dataType, true, null);

        Assert.False(_strategy.IsCompatibleWith(column));
    }

    [Theory]
    [InlineData("nvarchar")]
    [InlineData("varchar")]
    [InlineData("int")]
    [InlineData("bigint")]
    [InlineData("decimal")]
    public void IsCompatibleWith_SupportedType_ReturnsTrue(string dataType)
    {
        var column = new ColumnInfo("Col", dataType, true, null);

        Assert.True(_strategy.IsCompatibleWith(column));
    }
}


public class MaskingStrategyTests
{
    private readonly MaskingStrategy _strategy = new();

    [Fact]
    public void GenerateValue_MasksMiddleCharacters()
    {
        var column = new ColumnInfo("Email", "nvarchar", true, 100);
        var parameters = new StrategyParameters
        {
            MaskCharacter = '*',
            PreserveStart = 2,
            PreserveEnd = 2
        };

        var result = _strategy.GenerateValue("Hello!", column, parameters);

        Assert.Equal("He**o!", result);
    }

    [Fact]
    public void GenerateValue_PreservesStartAndEndCharacters()
    {
        var column = new ColumnInfo("Name", "varchar", true, 50);
        var parameters = new StrategyParameters
        {
            MaskCharacter = '#',
            PreserveStart = 3,
            PreserveEnd = 1
        };

        var result = _strategy.GenerateValue("JohnDoe", column, parameters);

        var str = (string)result!;
        Assert.Equal("Joh", str[..3]);
        Assert.Equal("e", str[^1..]);
        Assert.True(str[3..^1].All(c => c == '#'));
    }

    [Fact]
    public void GenerateValue_PreserveExceedsLength_ReturnsOriginal()
    {
        var column = new ColumnInfo("Code", "varchar", true, 10);
        var parameters = new StrategyParameters
        {
            MaskCharacter = '*',
            PreserveStart = 3,
            PreserveEnd = 3
        };

        var result = _strategy.GenerateValue("Hi", column, parameters);

        Assert.Equal("Hi", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GenerateValue_NullOrEmpty_ReturnsAsIs(string? input)
    {
        var column = new ColumnInfo("Col", "nvarchar", true, 50);
        var parameters = new StrategyParameters { MaskCharacter = '*', PreserveStart = 1, PreserveEnd = 1 };

        object? boxed = input;
        var result = _strategy.GenerateValue(boxed, column, parameters);

        Assert.Equal(input, result);
    }

    [Fact]
    public void GenerateValue_DBNull_ReturnsDBNull()
    {
        var column = new ColumnInfo("Col", "nvarchar", true, 50);
        var parameters = new StrategyParameters { MaskCharacter = '*' };

        var result = _strategy.GenerateValue(DBNull.Value, column, parameters);

        Assert.Same(DBNull.Value, result);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("datetime")]
    [InlineData("bit")]
    public void IsCompatibleWith_NonTextType_ReturnsFalse(string dataType)
    {
        var column = new ColumnInfo("Col", dataType, true, null);

        Assert.False(_strategy.IsCompatibleWith(column));
    }

    [Theory]
    [InlineData("nvarchar")]
    [InlineData("varchar")]
    [InlineData("text")]
    [InlineData("char")]
    public void IsCompatibleWith_TextType_ReturnsTrue(string dataType)
    {
        var column = new ColumnInfo("Col", dataType, true, null);

        Assert.True(_strategy.IsCompatibleWith(column));
    }
}

public class NullificationStrategyTests
{
    private readonly NullificationStrategy _strategy = new();

    [Fact]
    public void GenerateValue_ReturnsDBNullValue()
    {
        var column = new ColumnInfo("Name", "nvarchar", true, 50);
        var parameters = new StrategyParameters();

        var result = _strategy.GenerateValue("anything", column, parameters);

        Assert.Same(DBNull.Value, result);
    }

    [Fact]
    public void IsCompatibleWith_NonNullableColumn_ReturnsFalse()
    {
        var column = new ColumnInfo("Id", "int", false, null);

        Assert.False(_strategy.IsCompatibleWith(column));
    }

    [Fact]
    public void IsCompatibleWith_NullableColumn_ReturnsTrue()
    {
        var column = new ColumnInfo("Name", "nvarchar", true, 50);

        Assert.True(_strategy.IsCompatibleWith(column));
    }
}

public class FixedValueStrategyTests
{
    private readonly FixedValueStrategy _strategy = new();

    [Fact]
    public void GenerateValue_ReturnsFixedValueFromParameters()
    {
        var column = new ColumnInfo("Status", "nvarchar", true, 50);
        var parameters = new StrategyParameters { FixedValue = "REDACTED" };

        var result = _strategy.GenerateValue("original", column, parameters);

        Assert.Equal("REDACTED", result);
    }

    [Fact]
    public void GenerateValue_NullFixedValue_ReturnsNull()
    {
        var column = new ColumnInfo("Col", "nvarchar", true, 50);
        var parameters = new StrategyParameters { FixedValue = null };

        var result = _strategy.GenerateValue("original", column, parameters);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("nvarchar", true)]
    [InlineData("int", false)]
    [InlineData("datetime", true)]
    [InlineData("bit", false)]
    public void IsCompatibleWith_AnyColumn_ReturnsTrue(string dataType, bool isNullable)
    {
        var column = new ColumnInfo("Col", dataType, isNullable, null);

        Assert.True(_strategy.IsCompatibleWith(column));
    }
}

public class ShufflingStrategyTests
{
    private readonly ShufflingStrategy _strategy = new();

    [Fact]
    public void GenerateValue_ReturnsOriginalValue()
    {
        var column = new ColumnInfo("Name", "nvarchar", true, 50);
        var parameters = new StrategyParameters();

        var result = _strategy.GenerateValue("TestValue", column, parameters);

        Assert.Equal("TestValue", result);
    }

    [Fact]
    public void GenerateValue_NullOriginal_ReturnsNull()
    {
        var column = new ColumnInfo("Col", "nvarchar", true, 50);
        var parameters = new StrategyParameters();

        var result = _strategy.GenerateValue(null, column, parameters);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("nvarchar", true)]
    [InlineData("int", false)]
    [InlineData("datetime", true)]
    [InlineData("bit", false)]
    public void IsCompatibleWith_AnyColumn_ReturnsTrue(string dataType, bool isNullable)
    {
        var column = new ColumnInfo("Col", dataType, isNullable, null);

        Assert.True(_strategy.IsCompatibleWith(column));
    }
}
