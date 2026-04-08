namespace DataDesensitization.Models;

public record ProfileImportResult(
    bool Success,
    string? ErrorMessage,
    ProfileLoadResult? LoadResult);
