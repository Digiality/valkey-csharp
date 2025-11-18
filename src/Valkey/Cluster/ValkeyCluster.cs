using System.Net;
using Valkey.Abstractions;
using Valkey.Abstractions.Cluster;
using Valkey.Configuration;

namespace Valkey.Cluster;

/// <summary>
/// Client for interacting with a Valkey/Redis cluster.
/// Handles topology discovery, connection pooling, and automatic redirections.
/// </summary>
public sealed class ValkeyCluster : IValkeyCluster
{
    private readonly EndPoint[] _seedEndpoints;
    private readonly ValkeyClusterOptions _options;
    private readonly ClusterConnectionPool _connectionPool;
    private readonly ClusterSlotMap _slotMap;
    private bool _disposed;

    private ValkeyCluster(IEnumerable<EndPoint> seedEndpoints, ValkeyClusterOptions? options)
    {
        _seedEndpoints = seedEndpoints?.ToArray() ?? throw new ArgumentNullException(nameof(seedEndpoints));

        if (_seedEndpoints.Length == 0)
        {
            throw new ArgumentException("At least one seed endpoint must be provided", nameof(seedEndpoints));
        }

        _options = options ?? new ValkeyClusterOptions();
        _connectionPool = new ClusterConnectionPool(_options.ConnectionOptions);
        _slotMap = new ClusterSlotMap();
    }

    /// <summary>
    /// Creates and connects to a Valkey cluster.
    /// </summary>
    /// <param name="seedEndpoints">One or more cluster node endpoints for initial connection.</param>
    /// <param name="options">Cluster-specific options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connected cluster client.</returns>
    public static async Task<ValkeyCluster> ConnectAsync(
        IEnumerable<EndPoint> seedEndpoints,
        ValkeyClusterOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var cluster = new ValkeyCluster(seedEndpoints, options);
        await cluster.RefreshTopologyAsync(cancellationToken).ConfigureAwait(false);
        return cluster;
    }

    /// <summary>
    /// Creates and connects to a Valkey cluster using a single endpoint string.
    /// </summary>
    /// <param name="endpoint">Endpoint in format "host:port".</param>
    /// <param name="options">Cluster-specific options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<ValkeyCluster> ConnectAsync(
        string endpoint,
        ValkeyClusterOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var endPoint = ParseEndpoint(endpoint);
        return await ConnectAsync(new[] { endPoint }, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IValkeyDatabase GetDatabase()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ValkeyCluster));
        }

        return new ValkeyClusterDatabase(this, _connectionPool, _slotMap, _options);
    }

    /// <inheritdoc/>
    public async Task RefreshTopologyAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ValkeyCluster));
        }

        Exception? lastException = null;

        // Try each seed endpoint until we get topology
        foreach (var endpoint in _seedEndpoints)
        {
            try
            {
                var connection = await _connectionPool.GetOrCreateConnectionAsync(endpoint, cancellationToken)
                    .ConfigureAwait(false);

                var response = await connection.ExecuteCommandAsync(
                    "CLUSTER"u8.ToArray(),
                    new ReadOnlyMemory<byte>[] { "NODES"u8.ToArray().AsMemory() },
                    1,
                    cancellationToken).ConfigureAwait(false);

                if (response.IsNull)
                {
                    throw new ClusterTopologyException("CLUSTER NODES returned null");
                }

                var clusterNodesOutput = response.AsString();
                var nodes = ClusterTopologyParser.ParseClusterNodes(clusterNodesOutput);

                if (nodes.Count == 0)
                {
                    throw new ClusterTopologyException("CLUSTER NODES returned no nodes");
                }

                _slotMap.Update(nodes);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                // Try next endpoint
            }
        }

        throw new ClusterTopologyException(
            $"Failed to refresh cluster topology from any seed endpoint. Last error: {lastException?.Message}",
            lastException!);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ClusterNode> GetNodes()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ValkeyCluster));
        }

        return _slotMap.GetAllNodes();
    }

    /// <inheritdoc/>
    public ClusterNode? GetNodeForKey(string key)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ValkeyCluster));
        }

        return _slotMap.GetNodeForKey(key);
    }

    /// <inheritdoc/>
    public ClusterNode? GetNodeForSlot(int slot)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ValkeyCluster));
        }

        return _slotMap.GetNodeForSlot(slot);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<EndPoint> GetActiveConnections()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ValkeyCluster));
        }

        return _connectionPool.GetActiveEndpoints();
    }

    /// <summary>
    /// Gets a connection for a specific key (internal use).
    /// </summary>
    internal async ValueTask<ValkeyConnection> GetConnectionForKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var node = _slotMap.GetNodeForKey(key)
            ?? throw new NoNodeAvailableException($"No node found for key '{key}'. Cluster topology may not be initialized.");

        return await _connectionPool.GetOrCreateConnectionAsync(node.EndPoint, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a connection to a specific endpoint (internal use for redirections).
    /// </summary>
    internal async ValueTask<ValkeyConnection> GetConnectionAsync(
        EndPoint endPoint,
        CancellationToken cancellationToken = default)
    {
        return await _connectionPool.GetOrCreateConnectionAsync(endPoint, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the cluster slot map (internal use).
    /// </summary>
    internal ClusterSlotMap SlotMap => _slotMap;

    private static EndPoint ParseEndpoint(string endpoint)
    {
        var parts = endpoint.Split(':');
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid endpoint format: '{endpoint}'. Expected format: 'host:port'");
        }

        if (!int.TryParse(parts[1], out var port))
        {
            throw new ArgumentException($"Invalid port in endpoint: '{endpoint}'");
        }

        return new DnsEndPoint(parts[0], port);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionPool.Dispose();
        _slotMap.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _connectionPool.DisposeAsync().ConfigureAwait(false);
        _slotMap.Dispose();
    }
}
