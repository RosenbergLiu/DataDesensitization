// Feature: data-desensitization, Property 4: Strategy-type compatibility is correctly validated
// **Validates: Requirements 3.5**

using DataDesensitization.Models;
using DataDesensitization.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DataDesensitization.Tests;

public class StrategyCompatibilityPropertyTests
{
    private static readonly string[] TextTypes =
        { "nvarchar", "varchar", "text", "char", "nchar", "ntext", "character varying" };

    private static readonly string[] NumericTypes =
        { "int", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real", "double precision", "integer" };

    private static readonly string[] NonTextTypes =
        { "int", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real",
          "double precision", "integer", "bit", "date", "datetime", "uniqueidentifier", "binary" };

    private static readonly string[] AllKnownTypes =
        TextTypes.Concat(NonTextTypes).ToArray();

    private static Gen<ColumnInfo> GenColumnInfo()
    {
        return from name in Arb.Generate<NonEmptyString>().Select(s => s.Get)
               from dataType in Gen.Elements(AllKnownTypes)
               from isNullable in Arb.Generate<bool>()
               from maxLength in Gen.OneOf(Gen.Constant((int?)null), Gen.Choose(1, 4000).Select(n => (int?)n))
               select new ColumnInfo(name, dataType, isNullable, maxLength);
    }

    [Property(MaxTest = 100)]
    public Property Nullification_IsCompatible_OnlyWhenColumnIsNullable()
    {
        return Prop.ForAll(GenColumnInfo().ToArbitrary(), column =>
        {
            var strategy = new NullificationStrategy();
            var result = strategy.IsCompatibleWith(column);

            return (result == column.IsNullable)
                .Label($"Column IsNullable={column.IsNullable}, IsCompatibleWith returned {result}");
        });
    }

    [Property(MaxTest = 100)]
    public Property Masking_IsCompatible_OnlyWithTextTypes()
    {
        return Prop.ForAll(GenColumnInfo().ToArbitrary(), column =>
        {
            var strategy = new MaskingStrategy();
            var result = strategy.IsCompatibleWith(column);
            var isTextType = TextTypes.Contains(column.DataType, StringComparer.OrdinalIgnoreCase);

            return (result == isTextType)
                .Label($"Column DataType='{column.DataType}', IsText={isTextType}, IsCompatibleWith returned {result}");
        });
    }

    [Property(MaxTest = 100)]
    public Property Randomization_IsCompatible_OnlyWithTextAndNumericTypes()
    {
        return Prop.ForAll(GenColumnInfo().ToArbitrary(), column =>
        {
            var strategy = new RandomizationStrategy();
            var result = strategy.IsCompatibleWith(column);
            var isTextType = TextTypes.Contains(column.DataType, StringComparer.OrdinalIgnoreCase);
            var isNumericType = NumericTypes.Contains(column.DataType, StringComparer.OrdinalIgnoreCase);

            return (result == (isTextType || isNumericType))
                .Label($"Column DataType='{column.DataType}', IsText={isTextType}, IsNumeric={isNumericType}, IsCompatibleWith returned {result}");
        });
    }
}
