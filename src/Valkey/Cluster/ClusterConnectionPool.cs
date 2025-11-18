using System.Collections.Concurrent;
using System.Net;
using Valkey.Configuration;

namespace Valkey.Cluster;

/// <summary>
/// Manages connections to cluster nodes with pooling.
/// </summary>
internal sealed class ClusterConnectionPool : IDisposable, IAsyncDisposable
{
    private readonly ValkeyOptions _connectionOptions;
    private readonly ConcurrentDictionary<EndPoint, ValkeyConnection> _connections = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    public ClusterConnectionPool(ValkeyOptions connectionOptions)
    {
        _connectionOptions = connectionOptions ?? throw new ArgumentNullException(nameof(connectionOptions));
    }

    /// <summary>
    /// Gets or creates a connection to the specified endpoint.
    /// </summary>
    public async ValueTask<ValkeyConnection> GetOrCreateConnectionAsync(
        EndPoint endPoint,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClusterConnectionPool));
        }

        // Fast path: connection already exists
        if (_connections.TryGetValue(endPoint, out var existingConnection))
        {
            return existingConnection;
        }

        // Slow path: create new connection with lock
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_connections.TryGetValue(endPoint, out existingConnection))
            {
                return existingConnection;
            }

            // Create and connect
            var connection = new ValkeyConnection(endPoint, _connectionOptions);
            await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

            // Add to pool
            if (_connections.TryAdd(endPoint, connection))
            {
                return connection;
            }

            // Race condition: another thread added it, dispose ours and use theirs
            await connection.DisposeAsync().ConfigureAwait(false);
            return _connections[endPoint];
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Removes and disposes a connection from the pool.
    /// </summary>
    public async ValueTask RemoveConnectionAsync(EndPoint endPoint)
    {
        if (_connections.TryRemove(endPoint, out var connection))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets all active connections.
    /// </summary>
    public IReadOnlyCollection<EndPoint> GetActiveEndpoints()
    {
        return _connections.Keys.ToList();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionLock.Dispose();

        foreach (var connection in _connections.Values)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        _connections.Clear();
    }
}
