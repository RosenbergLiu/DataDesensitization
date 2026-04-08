using DataDesensitization.Models;

namespace DataDesensitization.Services;

public interface ISchemaService
{
    Task<List<TableInfo>> GetTablesAsync(CancellationToken ct = default);
    Task<List<ColumnInfo>> GetColumnsAsync(string tableName, CancellationToken ct = default);
    Task<List<TableInfo>> SearchTablesAsync(string filter, CancellationToken ct = default);
}
