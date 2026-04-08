using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using DataDesensitization.Models;
using DataDesensitization.Services;

namespace DataDesensitization.Tests;

#region Test Doubles

/// <summary>
/// Fake DbConnection that allows controlling Open/Close behavior for testing.
/// </summary>
internal class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;
    private readonly Func<CancellationToken, Task>? _onOpenAsync;
    private readonly Action? _onClose;
    private readonly string _database;
    private readonly string _dataSource;

    public FakeDbConnection(
        string database = "TestDb",
        string dataSource = "localhost",
        Func<CancellationToken, Task>? onOpenAsync = null,
        Action? onClose = null)
    {
        _database = database;
        _dataSource = dataSource;
        _onOpenAsync = onOpenAsync;
        _onClose = onClose;
    }

    [AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => _database;
    public override string DataSource => _dataSource;
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) { }

    public override void Close()
    {
        _onClose?.Invoke();
        _state = ConnectionState.Closed;
    }

    public override void Open() => _state = ConnectionState.Open;

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        if (_onOpenAsync != null)
            await _onOpenAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        _state = ConnectionState.Open;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => throw new NotImplementedException();

    protected override DbCommand CreateDbCommand()
        => throw new NotImplementedException();
}

/// <summary>
/// Fake IDbProviderFactory that returns a preconfigured FakeDbConnection.
/// </summary>
internal class FakeDbProviderFactory : IDbProviderFactory
{
    private readonly Func<string, FakeDbConnection> _factory;

    public FakeDbProviderFactory(FakeDbConnection connection)
    {
        _factory = _ => connection;
    }

    public FakeDbProviderFactory(Func<string, FakeDbConnection> factory)
    {
        _factory = factory;
    }

    public DbConnection CreateConnection(string connectionString) => _factory(connectionString);
}

#endregion

public class ConnectionManagerTests
{
    private static ConnectionManager CreateManager(
        FakeDbConnection? sqlConn = null,
        FakeDbConnection? pgConn = null)
    {
        var sqlFactory = new FakeDbProviderFactory(sqlConn ?? new FakeDbConnection());
        var pgFactory = new FakeDbProviderFactory(pgConn ?? new FakeDbConnection());
        return new ConnectionManager(sqlFactory, pgFactory);
    }

    #region 1. Successful connection returns ConnectionResult with Success=true, DatabaseName, ServerAddress

    [Fact]
    public async Task ConnectAsync_Success_ReturnsSuccessResultWithDatabaseInfo()
    {
        var conn = new FakeDbConnection(database: "MyDatabase", dataSource: "myserver.local");
        var manager = CreateManager(sqlConn: conn);

        var result = await manager.ConnectAsync("Server=myserver.local;Database=MyDatabase", DatabaseProvider.SqlServer);

        Assert.True(result.Success);
        Assert.Equal("MyDatabase", result.DatabaseName);
        Assert.Equal("myserver.local", result.ServerAddress);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ConnectAsync_PostgreSql_Success_ReturnsSuccessResult()
    {
        var conn = new FakeDbConnection(database: "PgDb", dataSource: "pghost");
        var manager = CreateManager(pgConn: conn);

        var result = await manager.ConnectAsync("Host=pghost;Database=PgDb", DatabaseProvider.PostgreSql);

        Assert.True(result.Success);
        Assert.Equal("PgDb", result.DatabaseName);
        Assert.Equal("pghost", result.ServerAddress);
    }

    #endregion

    #region 2. Failed connection returns ConnectionResult with Success=false and descriptive error

    [Fact]
    public async Task ConnectAsync_OpenThrows_ReturnsFailureWithErrorMessage()
    {
        var conn = new FakeDbConnection(onOpenAsync: _ =>
            throw new InvalidOperationException("Invalid credentials"));
        var manager = CreateManager(sqlConn: conn);

        var result = await manager.ConnectAsync("bad-conn-string", DatabaseProvider.SqlServer);

        Assert.False(result.Success);
        Assert.Contains("Invalid credentials", result.ErrorMessage);
        Assert.Null(result.DatabaseName);
        Assert.Null(result.ServerAddress);
    }

    [Fact]
    public async Task ConnectAsync_OpenThrowsGenericException_ReturnsFailure()
    {
        var conn = new FakeDbConnection(onOpenAsync: _ =>
            throw new Exception("Unreachable host"));
        var manager = CreateManager(sqlConn: conn);

        var result = await manager.ConnectAsync("conn", DatabaseProvider.SqlServer);

        Assert.False(result.Success);
        Assert.Equal("Unreachable host", result.ErrorMessage);
    }

    #endregion

    #region 3. Timeout enforcement (30 seconds)

    [Fact]
    public async Task ConnectAsync_Timeout_ReturnsTimeoutError()
    {
        // Simulate a connection that hangs until cancellation
        var conn = new FakeDbConnection(onOpenAsync: async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
        });
        var manager = CreateManager(sqlConn: conn);

        var result = await manager.ConnectAsync("conn", DatabaseProvider.SqlServer);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConnectAsync_ExternalCancellation_ReturnsCancelledMessage()
    {
        var cts = new CancellationTokenSource();
        var conn = new FakeDbConnection(onOpenAsync: async ct =>
        {
            await cts.CancelAsync();
            ct.ThrowIfCancellationRequested();
        });
        var manager = CreateManager(sqlConn: conn);

        var result = await manager.ConnectAsync("conn", DatabaseProvider.SqlServer, cts.Token);

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 4. Status transitions

    [Fact]
    public async Task ConnectAsync_Success_TransitionsDisconnectedToConnectingToConnected()
    {
        var transitions = new List<ConnectionStatus>();
        var conn = new FakeDbConnection();
        var manager = CreateManager(sqlConn: conn);
        manager.StatusChanged += s => transitions.Add(s);

        Assert.Equal(ConnectionStatus.Disconnected, manager.Status);

        await manager.ConnectAsync("conn", DatabaseProvider.SqlServer);

        Assert.Equal(ConnectionStatus.Connected, manager.Status);
        Assert.Contains(ConnectionStatus.Connecting, transitions);
        Assert.Contains(ConnectionStatus.Connected, transitions);
        // Connecting must come before Connected
        Assert.True(transitions.IndexOf(ConnectionStatus.Connecting) < transitions.IndexOf(ConnectionStatus.Connected));
    }

    [Fact]
    public async Task ConnectAsync_Failure_TransitionsDisconnectedToConnectingToError()
    {
        var transitions = new List<ConnectionStatus>();
        var conn = new FakeDbConnection(onOpenAsync: _ => throw new Exception("fail"));
        var manager = CreateManager(sqlConn: conn);
        manager.StatusChanged += s => transitions.Add(s);

        await manager.ConnectAsync("conn", DatabaseProvider.SqlServer);

        Assert.Equal(ConnectionStatus.Error, manager.Status);
        Assert.Contains(ConnectionStatus.Connecting, transitions);
        Assert.Contains(ConnectionStatus.Error, transitions);
        Assert.True(transitions.IndexOf(ConnectionStatus.Connecting) < transitions.IndexOf(ConnectionStatus.Error));
    }

    #endregion

    #region 5. StatusChanged event fires on transitions

    [Fact]
    public async Task StatusChanged_FiresForEachTransition()
    {
        var events = new List<ConnectionStatus>();
        var conn = new FakeDbConnection();
        var manager = CreateManager(sqlConn: conn);
        manager.StatusChanged += s => events.Add(s);

        await manager.ConnectAsync("conn", DatabaseProvider.SqlServer);

        Assert.True(events.Count >= 2); // At least Connecting + Connected
        Assert.Equal(ConnectionStatus.Connecting, events[0]);
    }

    [Fact]
    public async Task StatusChanged_DoesNotFireWhenStatusUnchanged()
    {
        var events = new List<ConnectionStatus>();
        var manager = CreateManager();
        manager.StatusChanged += s => events.Add(s);

        // Disconnect when already disconnected should not fire
        await manager.DisconnectAsync();

        // Status was already Disconnected, so no event should fire
        Assert.DoesNotContain(ConnectionStatus.Disconnected, events);
    }

    #endregion

    #region 6. DisconnectAsync closes connection and sets status to Disconnected

    [Fact]
    public async Task DisconnectAsync_ClosesConnectionAndSetsDisconnected()
    {
        var closed = false;
        var conn = new FakeDbConnection(onClose: () => closed = true);
        var manager = CreateManager(sqlConn: conn);

        await manager.ConnectAsync("conn", DatabaseProvider.SqlServer);
        Assert.Equal(ConnectionStatus.Connected, manager.Status);
        Assert.NotNull(manager.CurrentConnection);

        await manager.DisconnectAsync();

        Assert.Equal(ConnectionStatus.Disconnected, manager.Status);
        Assert.Null(manager.CurrentConnection);
        Assert.True(closed);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_StaysDisconnected()
    {
        var manager = CreateManager();

        await manager.DisconnectAsync();

        Assert.Equal(ConnectionStatus.Disconnected, manager.Status);
        Assert.Null(manager.CurrentConnection);
    }

    #endregion

    #region 7. DisconnectAsync handles errors gracefully (force-sets Disconnected)

    [Fact]
    public async Task DisconnectAsync_CloseThrows_ForcesSetsDisconnected()
    {
        var conn = new FakeDbConnection(onClose: () => throw new Exception("close error"));
        var manager = CreateManager(sqlConn: conn);

        await manager.ConnectAsync("conn", DatabaseProvider.SqlServer);
        Assert.Equal(ConnectionStatus.Connected, manager.Status);

        // Should not throw, and should force status to Disconnected
        await manager.DisconnectAsync();

        Assert.Equal(ConnectionStatus.Disconnected, manager.Status);
        Assert.Null(manager.CurrentConnection);
    }

    #endregion
}
