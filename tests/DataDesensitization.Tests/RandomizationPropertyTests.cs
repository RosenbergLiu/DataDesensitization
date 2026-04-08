// Feature: data-desensitization, Property 2: Randomization respects length bounds
// **Validates: Requirements 3.2**

using DataDesensitization.Models;
using DataDesensitization.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DataDesensitization.Tests;

public class RandomizationPropertyTests
{
    [Property(MaxTest = 100)]
    public Property Randomization_RespectsLengthBounds()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 1000).Select(n => n)),
            Arb.From(Gen.Choose(0, 1000).Select(n => n)),
            (minLength, extraRange) =>
            {
                var maxLength = minLength + extraRange;

                var column = new ColumnInfo("TestColumn", "nvarchar", false, maxLength);
                var parameters = new StrategyParameters
                {
                    MinLength = minLength,
                    MaxLength = maxLength
                };

                var strategy = new RandomizationStrategy();
                var result = strategy.GenerateValue(null, column, parameters);

                var resultStr = result as string;
                return (resultStr != null &&
                        resultStr.Length >= minLength &&
                        resultStr.Length <= maxLength)
                    .Label($"Expected length in [{minLength}, {maxLength}], got {resultStr?.Length}");
            });
    }
}
