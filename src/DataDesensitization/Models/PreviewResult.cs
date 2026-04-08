namespace DataDesensitization.Models;

public record PreviewResult(
    string TableName,
    List<PreviewRow> Rows);
