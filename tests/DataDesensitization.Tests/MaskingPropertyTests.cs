// Feature: data-desensitization, Property 3: Masking preserves format
// **Validates: Requirements 3.3**

using DataDesensitization.Models;
using DataDesensitization.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DataDesensitization.Tests;

public class MaskingPropertyTests
{
    [Property(MaxTest = 100)]
    public Property Masking_PreservesFormat()
    {
        // Generate a non-empty string, a mask character, and valid preserveStart/preserveEnd
        var gen =
            from original in Arb.Generate<NonEmptyString>().Select(s => s.Get)
            from maskChar in Arb.Generate<char>().Where(c => !char.IsControl(c))
            let len = original.Length
            from preserveStart in Gen.Choose(0, len - 1)
            from preserveEnd in Gen.Choose(0, len - 1 - preserveStart)
            select new { original, maskChar, preserveStart, preserveEnd };

        return Prop.ForAll(gen.ToArbitrary(), data =>
        {
            var column = new ColumnInfo("TestColumn", "nvarchar", false, null);
            var parameters = new StrategyParameters
            {
                MaskCharacter = data.maskChar,
                PreserveStart = data.preserveStart,
                PreserveEnd = data.preserveEnd
            };

            var strategy = new MaskingStrategy();
            var result = strategy.GenerateValue(data.original, column, parameters);
            var resultStr = result as string;

            if (resultStr == null)
                return false.Label("Result should not be null for non-empty input");

            var len = data.original.Length;
            var sameLength = resultStr.Length == len;

            var startPreserved = true;
            for (var i = 0; i < data.preserveStart; i++)
            {
                if (resultStr[i] != data.original[i])
                {
                    startPreserved = false;
                    break;
                }
            }

            var endPreserved = true;
            for (var i = len - data.preserveEnd; i < len; i++)
            {
                if (resultStr[i] != data.original[i])
                {
                    endPreserved = false;
                    break;
                }
            }

            var middleMasked = true;
            for (var i = data.preserveStart; i < len - data.preserveEnd; i++)
            {
                if (resultStr[i] != data.maskChar)
                {
                    middleMasked = false;
                    break;
                }
            }

            return (sameLength && startPreserved && endPreserved && middleMasked)
                .Label($"original='{data.original}', result='{resultStr}', maskChar='{data.maskChar}', preserveStart={data.preserveStart}, preserveEnd={data.preserveEnd}");
        });
    }
}
