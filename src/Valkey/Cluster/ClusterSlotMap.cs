using System.Net;
using Valkey.Abstractions.Cluster;

namespace Valkey.Cluster;

/// <summary>
/// Thread-safe map of hash slots to cluster nodes.
/// </summary>
internal sealed class ClusterSlotMap
{
    private const int TotalSlots = 16384;

    private readonly ReaderWriterLockSlim _lock = new();
    private ClusterNode?[] _slotToNode = new ClusterNode?[TotalSlots];
    private List<ClusterNode> _allNodes = new();

    /// <summary>
    /// Updates the slot map with new cluster topology.
    /// </summary>
    public void Update(IEnumerable<ClusterNode> nodes)
    {
        var nodeList = nodes.ToList();
        var newSlotMap = new ClusterNode?[TotalSlots];

        // Build new slot map
        foreach (var node in nodeList)
        {
            // Only master nodes serve slots
            if (node.IsMaster)
            {
                foreach (var slotRange in node.Slots)
                {
                    for (int slot = slotRange.Start; slot <= slotRange.End; slot++)
                    {
                        newSlotMap[slot] = node;
                    }
                }
            }
        }

        // Atomically update
        _lock.EnterWriteLock();
        try
        {
            _slotToNode = newSlotMap;
            _allNodes = nodeList;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets the master node responsible for a given slot.
    /// </summary>
    public ClusterNode? GetNodeForSlot(int slot)
    {
        if (slot < 0 || slot >= TotalSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), $"Slot must be between 0 and {TotalSlots - 1}");
        }

        _lock.EnterReadLock();
        try
        {
            return _slotToNode[slot];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets the master node responsible for a given key.
    /// </summary>
    public ClusterNode? GetNodeForKey(string key)
    {
        var slot = HashSlotCalculator.CalculateSlot(key);
        return GetNodeForSlot(slot);
    }

    /// <summary>
    /// Gets all cluster nodes (masters and replicas).
    /// </summary>
    public IReadOnlyList<ClusterNode> GetAllNodes()
    {
        _lock.EnterReadLock();
        try
        {
            return _allNodes.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all master nodes.
    /// </summary>
    public IReadOnlyList<ClusterNode> GetMasterNodes()
    {
        _lock.EnterReadLock();
        try
        {
            return _allNodes.Where(n => n.IsMaster).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a random master node (useful for topology discovery).
    /// </summary>
    public ClusterNode? GetRandomMasterNode()
    {
        _lock.EnterReadLock();
        try
        {
            var masters = _allNodes.Where(n => n.IsMaster).ToList();
            return masters.Count > 0 ? masters[Random.Shared.Next(masters.Count)] : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
