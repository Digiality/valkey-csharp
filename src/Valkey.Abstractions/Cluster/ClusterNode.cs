using System.Net;

namespace Valkey.Abstractions.Cluster;

/// <summary>
/// Represents a node in a Valkey cluster.
/// </summary>
public sealed class ClusterNode
{
    /// <summary>
    /// Gets the unique node ID.
    /// </summary>
    public string NodeId { get; }

    /// <summary>
    /// Gets the endpoint of the node.
    /// </summary>
    public EndPoint EndPoint { get; }

    /// <summary>
    /// Gets the flags associated with the node.
    /// </summary>
    public ClusterNodeFlags Flags { get; }

    /// <summary>
    /// Gets the master node ID if this is a replica.
    /// </summary>
    public string? MasterNodeId { get; }

    /// <summary>
    /// Gets the ping sent timestamp.
    /// </summary>
    public long PingSent { get; }

    /// <summary>
    /// Gets the pong received timestamp.
    /// </summary>
    public long PongReceived { get; }

    /// <summary>
    /// Gets the configuration epoch.
    /// </summary>
    public long ConfigEpoch { get; }

    /// <summary>
    /// Gets the link state (connected/disconnected).
    /// </summary>
    public string LinkState { get; }

    /// <summary>
    /// Gets the hash slots served by this node (for master nodes).
    /// </summary>
    public HashSlotRange[] Slots { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClusterNode"/> class.
    /// </summary>
    public ClusterNode(
        string nodeId,
        EndPoint endPoint,
        ClusterNodeFlags flags,
        string? masterNodeId,
        long pingSent,
        long pongReceived,
        long configEpoch,
        string linkState,
        HashSlotRange[] slots)
    {
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
        Flags = flags;
        MasterNodeId = masterNodeId;
        PingSent = pingSent;
        PongReceived = pongReceived;
        ConfigEpoch = configEpoch;
        LinkState = linkState ?? throw new ArgumentNullException(nameof(linkState));
        Slots = slots ?? Array.Empty<HashSlotRange>();
    }

    /// <summary>
    /// Gets whether this node is a master.
    /// </summary>
    public bool IsMaster => Flags.HasFlag(ClusterNodeFlags.Master);

    /// <summary>
    /// Gets whether this node is a replica.
    /// </summary>
    public bool IsReplica => Flags.HasFlag(ClusterNodeFlags.Replica);

    /// <summary>
    /// Gets whether this node is the one we're currently connected to.
    /// </summary>
    public bool IsMyself => Flags.HasFlag(ClusterNodeFlags.Myself);

    /// <summary>
    /// Checks if this node serves the specified hash slot.
    /// </summary>
    public bool ServesSlot(int slot)
    {
        foreach (var range in Slots)
        {
            if (slot >= range.Start && slot <= range.End)
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Flags associated with a cluster node.
/// </summary>
[Flags]
public enum ClusterNodeFlags
{
    /// <summary>
    /// No flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// The node is a master.
    /// </summary>
    Master = 1 << 0,

    /// <summary>
    /// The node is a replica.
    /// </summary>
    Replica = 1 << 1,

    /// <summary>
    /// This is the current node.
    /// </summary>
    Myself = 1 << 2,

    /// <summary>
    /// The node is in a PFAIL state (possibly failing).
    /// </summary>
    PFail = 1 << 3,

    /// <summary>
    /// The node is in a FAIL state.
    /// </summary>
    Fail = 1 << 4,

    /// <summary>
    /// The node sent a PONG recently.
    /// </summary>
    Handshake = 1 << 5,

    /// <summary>
    /// The node has no assigned slots.
    /// </summary>
    NoAddr = 1 << 6,

    /// <summary>
    /// The node has no flags.
    /// </summary>
    NoFlags = 1 << 7
}

/// <summary>
/// Represents a range of hash slots.
/// </summary>
public readonly struct HashSlotRange
{
    /// <summary>
    /// Gets the start of the slot range (inclusive).
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the end of the slot range (inclusive).
    /// </summary>
    public int End { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HashSlotRange"/> struct.
    /// </summary>
    public HashSlotRange(int start, int end)
    {
        if (start < 0 || start > 16383)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Slot must be between 0 and 16383");
        }
        if (end < 0 || end > 16383)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Slot must be between 0 and 16383");
        }
        if (end < start)
        {
            throw new ArgumentException("End slot must be >= start slot");
        }

        Start = start;
        End = end;
    }

    /// <summary>
    /// Creates a single-slot range.
    /// </summary>
    public static HashSlotRange Single(int slot) => new(slot, slot);

    /// <summary>
    /// Gets the number of slots in this range.
    /// </summary>
    public int Count => End - Start + 1;

    /// <inheritdoc/>
    public override string ToString() => Start == End ? $"{Start}" : $"{Start}-{End}";
}
