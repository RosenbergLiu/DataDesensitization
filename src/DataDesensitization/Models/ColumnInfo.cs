namespace DataDesensitization.Models;

public record ColumnInfo(
    string ColumnName,
    string DataType,
    bool IsNullable,
    int? MaxLength,
    bool IsForeignKey = false,
    string? ReferencedTable = null);
