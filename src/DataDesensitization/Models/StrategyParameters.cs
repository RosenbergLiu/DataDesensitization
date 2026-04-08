namespace DataDesensitization.Models;

public record StrategyParameters
{
    // Randomization
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }

    // Masking
    public char MaskCharacter { get; init; } = '*';
    public int PreserveStart { get; init; }
    public int PreserveEnd { get; init; }

    // Fixed Value
    public string? FixedValue { get; init; }
}
