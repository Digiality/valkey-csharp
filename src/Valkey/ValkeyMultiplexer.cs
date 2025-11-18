using System.Collections.Concurrent;
using Valkey.Abstractions;
using Valkey.Configuration;

namespace Valkey;

/// <summary>
/// Manages a pool of connections to Valkey servers with connection multiplexing.
/// Similar to StackExchange.Redis ConnectionMultiplexer pattern.
/// </summary>
public sealed class ValkeyMultiplexer : IAsyncDisposable
{
    private readonly ValkeyOptions _options;
    private readonly ConcurrentDictionary<int, ValkeyDatabase> _databases;
    private ValkeyConnection? _connection;
    private readonly SemaphoreSlim _connectionLock;
    private bool _disposed;

    private ValkeyMultiplexer(ValkeyOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _databases = new ConcurrentDictionary<int, ValkeyDatabase>();
        _connectionLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Creates and connects a new multiplexer instance.
    /// </summary>
    /// <param name="configuration">Connection string or endpoint.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A connected multiplexer instance.</returns>
    public static async ValueTask<ValkeyMultiplexer> ConnectAsync(
        string configuration,
        CancellationToken cancellationToken = default)
    {
        var options = ValkeyOptions.Parse(configuration);
        return await ConnectAsync(options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates and connects a new multiplexer instance.
    /// </summary>
    /// <param name="options">Connection options.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A connected multiplexer instance.</returns>
    public static async ValueTask<ValkeyMultiplexer> ConnectAsync(
        ValkeyOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var multiplexer = new ValkeyMultiplexer(options);
        await multiplexer.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return multiplexer;
    }

    /// <summary>
    /// Gets a database instance for the specified database number.
    /// </summary>
    /// <param name="db">The database number (default is 0).</param>
    /// <returns>A database instance.</returns>
    public IValkeyDatabase GetDatabase(int db = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (db < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(db), "Database number must be non-negative");
        }

        return _databases.GetOrAdd(db, dbNum =>
        {
            if (_connection == null)
            {
                throw new InvalidOperationException("Connection is not established");
            }

            return new ValkeyDatabase(_connection, dbNum);
        });
    }

    /// <summary>
    /// Gets the underlying connection (for advanced scenarios).
    /// </summary>
    internal ValkeyConnection Connection
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _connection ?? throw new InvalidOperationException("Connection is not established");
        }
    }

    /// <summary>
    /// Ensures the connection is established.
    /// </summary>
    private async ValueTask EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection != null)
        {
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection != null)
            {
                return;
            }

            if (_options.Endpoints == null || _options.Endpoints.Count == 0)
            {
                throw new InvalidOperationException("No endpoints configured");
            }

            // Connect to the first endpoint (in a real implementation, could support failover)
            var endpoint = _options.Endpoints[0];
            _connection = await ValkeyConnection.ConnectAsync(endpoint, _options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Tests connectivity to the server.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if the connection is healthy, false otherwise.</returns>
    public async ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var db = GetDatabase();
            var result = await db.PingAsync(cancellationToken).ConfigureAwait(false);
            return result == "PONG";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets whether the multiplexer is connected.
    /// </summary>
    public bool IsConnected => _connection != null && !_disposed;

    /// <summary>
    /// Closes all connections and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection != null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _databases.Clear();
        _connectionLock.Dispose();
    }
}
