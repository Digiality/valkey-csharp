namespace Valkey.Configuration;

/// <summary>
/// Configuration options for Valkey cluster connections.
/// </summary>
public sealed class ValkeyClusterOptions
{
    /// <summary>
    /// Gets or sets the base connection options.
    /// </summary>
    public ValkeyOptions ConnectionOptions { get; set; } = new ValkeyOptions();

    /// <summary>
    /// Gets or sets the maximum number of redirects to follow for MOVED/ASK responses.
    /// Default is 5.
    /// </summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>
    /// Gets or sets the interval for refreshing the cluster topology.
    /// Default is 5 minutes. Set to TimeSpan.Zero to disable automatic refresh.
    /// </summary>
    public TimeSpan TopologyRefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether to read from replica nodes.
    /// Default is false (read from master only).
    /// </summary>
    public bool AllowReadFromReplicas { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically handle MOVED redirects.
    /// Default is true.
    /// </summary>
    public bool AutoHandleMovedRedirects { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically handle ASK redirects.
    /// Default is true.
    /// </summary>
    public bool AutoHandleAskRedirects { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to throw an exception when all nodes are unavailable.
    /// Default is true.
    /// </summary>
    public bool ThrowOnAllNodesUnavailable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate multi-key operations are on the same slot.
    /// Default is true.
    /// </summary>
    public bool ValidateCrossSlotOperations { get; set; } = true;
}
