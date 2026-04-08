namespace DataDesensitization.Models;

public record ConnectionResult(
    bool Success,
    string? DatabaseName,
    string? ServerAddress,
    string? ErrorMessage);
