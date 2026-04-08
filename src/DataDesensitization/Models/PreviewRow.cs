namespace DataDesensitization.Models;

public record PreviewRow(
    Dictionary<string, object?> OriginalValues,
    Dictionary<string, object?> DesensitizedValues);
