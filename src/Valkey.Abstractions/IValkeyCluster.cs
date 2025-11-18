using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Valkey.Abstractions.Cluster;

namespace Valkey.Abstractions;

/// <summary>
/// Represents a client for interacting with a Valkey/Redis cluster.
/// </summary>
public interface IValkeyCluster : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets a database instance for executing commands against the cluster.
    /// Cluster mode does not support multiple databases (always uses database 0).
    /// </summary>
    public IValkeyDatabase GetDatabase();

    /// <summary>
    /// Refreshes the cluster topology by querying CLUSTER NODES.
    /// </summary>
    public Task RefreshTopologyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current cluster nodes.
    /// </summary>
    public IReadOnlyList<ClusterNode> GetNodes();

    /// <summary>
    /// Gets the master node responsible for a given key.
    /// </summary>
    public ClusterNode? GetNodeForKey(string key);

    /// <summary>
    /// Gets the master node responsible for a given slot.
    /// </summary>
    public ClusterNode? GetNodeForSlot(int slot);

    /// <summary>
    /// Gets all active connection endpoints.
    /// </summary>
    public IReadOnlyCollection<EndPoint> GetActiveConnections();
}
