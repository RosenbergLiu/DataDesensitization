using System.Data.Common;
using DataDesensitization.Models;

namespace DataDesensitization.Services;

public interface IConnectionManager
{
    Task<ConnectionResult> ConnectAsync(string connectionString, DatabaseProvider provider, CancellationToken ct = default);
    Task DisconnectAsync();
    DbConnection? CurrentConnection { get; }
    ConnectionStatus Status { get; }
    event Action<ConnectionStatus>? StatusChanged;
}
