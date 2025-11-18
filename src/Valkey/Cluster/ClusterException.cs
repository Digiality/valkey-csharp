using System.Net;

namespace Valkey.Cluster;

/// <summary>
/// Base exception for cluster-related errors.
/// </summary>
public class ClusterException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClusterException"/> class.
    /// </summary>
    public ClusterException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClusterException"/> class.
    /// </summary>
    public ClusterException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a MOVED redirect is received.
/// </summary>
public sealed class MovedRedirectException : ClusterException
{
    /// <summary>
    /// Gets the hash slot that was moved.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    /// Gets the endpoint to redirect to.
    /// </summary>
    public EndPoint TargetEndPoint { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MovedRedirectException"/> class.
    /// </summary>
    public MovedRedirectException(int slot, EndPoint targetEndPoint)
        : base($"MOVED redirect to {targetEndPoint} for slot {slot}")
    {
        Slot = slot;
        TargetEndPoint = targetEndPoint;
    }
}

/// <summary>
/// Exception thrown when an ASK redirect is received.
/// </summary>
public sealed class AskRedirectException : ClusterException
{
    /// <summary>
    /// Gets the hash slot being migrated.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    /// Gets the endpoint to ask.
    /// </summary>
    public EndPoint TargetEndPoint { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AskRedirectException"/> class.
    /// </summary>
    public AskRedirectException(int slot, EndPoint targetEndPoint)
        : base($"ASK redirect to {targetEndPoint} for slot {slot}")
    {
        Slot = slot;
        TargetEndPoint = targetEndPoint;
    }
}

/// <summary>
/// Exception thrown when a cross-slot operation is attempted.
/// </summary>
public sealed class CrossSlotException : ClusterException
{
    /// <summary>
    /// Gets the keys involved in the cross-slot operation.
    /// </summary>
    public string[] Keys { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrossSlotException"/> class.
    /// </summary>
    public CrossSlotException(params string[] keys)
        : base($"Keys belong to different slots: {string.Join(", ", keys)}")
    {
        Keys = keys;
    }
}

/// <summary>
/// Exception thrown when the cluster topology cannot be loaded.
/// </summary>
public sealed class ClusterTopologyException : ClusterException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClusterTopologyException"/> class.
    /// </summary>
    public ClusterTopologyException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClusterTopologyException"/> class.
    /// </summary>
    public ClusterTopologyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when no node is available to handle a request.
/// </summary>
public sealed class NoNodeAvailableException : ClusterException
{
    /// <summary>
    /// Gets the slot that no node is available for.
    /// </summary>
    public int? Slot { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoNodeAvailableException"/> class.
    /// </summary>
    public NoNodeAvailableException(int slot)
        : base($"No node available for slot {slot}")
    {
        Slot = slot;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoNodeAvailableException"/> class.
    /// </summary>
    public NoNodeAvailableException(string message)
        : base(message)
    {
        Slot = null;
    }
}
