using System.Data.Common;
using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class ConnectionManager : IConnectionManager
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(30);

    private readonly IDbProviderFactory _sqlServerFactory;
    private readonly IDbProviderFactory _postgreSqlFactory;

    private DbConnection? _currentConnection;
    private ConnectionStatus _status = ConnectionStatus.Disconnected;

    public ConnectionManager(
        SqlServerProviderFactory sqlServerFactory,
        PostgreSqlProviderFactory postgreSqlFactory)
    {
        _sqlServerFactory = sqlServerFactory;
        _postgreSqlFactory = postgreSqlFactory;
    }

    /// <summary>
    /// Internal constructor for unit testing with mock factories.
    /// </summary>
    internal ConnectionManager(
        IDbProviderFactory sqlServerFactory,
        IDbProviderFactory postgreSqlFactory)
    {
        _sqlServerFactory = sqlServerFactory;
        _postgreSqlFactory = postgreSqlFactory;
    }

    public DbConnection? CurrentConnection => _currentConnection;

    public ConnectionStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                StatusChanged?.Invoke(_status);
            }
        }
    }

    public event Action<ConnectionStatus>? StatusChanged;

    public async Task<ConnectionResult> ConnectAsync(
        string connectionString,
        DatabaseProvider provider,
        CancellationToken ct = default)
    {
        Status = ConnectionStatus.Connecting;

        try
        {
            var factory = provider switch
            {
                DatabaseProvider.SqlServer => _sqlServerFactory,
                DatabaseProvider.PostgreSql => _postgreSqlFactory,
                _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.")
            };

            var connection = factory.CreateConnection(connectionString);

            using var timeoutCts = new CancellationTokenSource(ConnectionTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);

            try
            {
                await connection.OpenAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                await DisposeConnectionAsync(connection);
                Status = ConnectionStatus.Error;
                return new ConnectionResult(false, null, null, "Connection timed out after 30 seconds.");
            }
            catch (OperationCanceledException)
            {
                await DisposeConnectionAsync(connection);
                Status = ConnectionStatus.Disconnected;
                return new ConnectionResult(false, null, null, "Connection attempt was cancelled.");
            }

            var databaseName = connection.Database;
            var serverAddress = connection.DataSource;

            // Close any existing connection before storing the new one
            await CloseCurrentConnectionAsync();

            _currentConnection = connection;
            Status = ConnectionStatus.Connected;

            return new ConnectionResult(true, databaseName, serverAddress, null);
        }
        catch (OperationCanceledException)
        {
            Status = ConnectionStatus.Disconnected;
            return new ConnectionResult(false, null, null, "Connection attempt was cancelled.");
        }
        catch (Exception ex)
        {
            Status = ConnectionStatus.Error;
            return new ConnectionResult(false, null, null, ex.Message);
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            await CloseCurrentConnectionAsync();
        }
        catch
        {
            // On disconnect failure, force-set status to Disconnected per design
        }
        finally
        {
            _currentConnection = null;
            Status = ConnectionStatus.Disconnected;
        }
    }

    private async Task CloseCurrentConnectionAsync()
    {
        if (_currentConnection is not null)
        {
            if (_currentConnection.State != System.Data.ConnectionState.Closed)
            {
                await _currentConnection.CloseAsync();
            }

            await _currentConnection.DisposeAsync();
            _currentConnection = null;
        }
    }

    private static async Task DisposeConnectionAsync(DbConnection connection)
    {
        try
        {
            if (connection.State != System.Data.ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }

            await connection.DisposeAsync();
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
