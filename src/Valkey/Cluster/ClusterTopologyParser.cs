using System.Net;
using Valkey.Abstractions.Cluster;

namespace Valkey.Cluster;

/// <summary>
/// Parses the output of CLUSTER NODES command.
/// </summary>
public static class ClusterTopologyParser
{
    /// <summary>
    /// Parses CLUSTER NODES output into a list of ClusterNode objects.
    /// </summary>
    /// <param name="clusterNodesOutput">The raw output from CLUSTER NODES command.</param>
    /// <returns>List of parsed cluster nodes.</returns>
    public static List<ClusterNode> ParseClusterNodes(string clusterNodesOutput)
    {
        if (string.IsNullOrWhiteSpace(clusterNodesOutput))
        {
            return new List<ClusterNode>();
        }

        var nodes = new List<ClusterNode>();
        var lines = clusterNodesOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var node = ParseNodeLine(line);
            if (node != null)
            {
                nodes.Add(node);
            }
        }

        return nodes;
    }

    /// <summary>
    /// Parses a single line from CLUSTER NODES output.
    /// Format: node-id ip:port@cport flags master ping pong epoch link-state slots
    /// Example: 07c37dfeb235213a872192d90877d0cd55635b91 127.0.0.1:30004@31004 slave e7d1eecce10fd6bb5eb35b9f99a514335d9ba9ca 0 1426238317239 4 connected
    /// </summary>
    private static ClusterNode? ParseNodeLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 8)
        {
            return null; // Invalid format
        }

        try
        {
            // Parse node ID
            var nodeId = parts[0];

            // Parse endpoint (format: ip:port@cport or ip:port)
            var endpointStr = parts[1];
            var endpoint = ParseEndPoint(endpointStr);

            // Parse flags
            var flags = ParseFlags(parts[2]);

            // Parse master node ID (or "-" if this is a master)
            var masterNodeId = parts[3] == "-" ? null : parts[3];

            // Parse ping/pong timestamps
            var pingSent = long.TryParse(parts[4], out var ping) ? ping : 0;
            var pongReceived = long.TryParse(parts[5], out var pong) ? pong : 0;

            // Parse config epoch
            var configEpoch = long.TryParse(parts[6], out var epoch) ? epoch : 0;

            // Parse link state
            var linkState = parts[7];

            // Parse slots (if this is a master)
            var slots = ParseSlots(parts, 8);

            return new ClusterNode(
                nodeId,
                endpoint,
                flags,
                masterNodeId,
                pingSent,
                pongReceived,
                configEpoch,
                linkState,
                slots);
        }
        catch
        {
            // If parsing fails, return null
            return null;
        }
    }

    /// <summary>
    /// Parses an endpoint string (format: ip:port@cport or ip:port).
    /// </summary>
    private static EndPoint ParseEndPoint(string endpointStr)
    {
        // Remove cluster port if present (e.g., "127.0.0.1:6379@16379" -> "127.0.0.1:6379")
        var atIndex = endpointStr.IndexOf('@');
        if (atIndex > 0)
        {
            endpointStr = endpointStr.Substring(0, atIndex);
        }

        // Parse IP and port
        var colonIndex = endpointStr.LastIndexOf(':');
        if (colonIndex > 0)
        {
            var ipStr = endpointStr.Substring(0, colonIndex);
            var portStr = endpointStr.Substring(colonIndex + 1);

            if (int.TryParse(portStr, out var port))
            {
                // Try to parse as IP address
                if (IPAddress.TryParse(ipStr, out var ipAddress))
                {
                    return new IPEndPoint(ipAddress, port);
                }

                // Fallback to DNS endpoint
                return new DnsEndPoint(ipStr, port);
            }
        }

        throw new FormatException($"Invalid endpoint format: {endpointStr}");
    }

    /// <summary>
    /// Parses node flags from a comma-separated string.
    /// </summary>
    private static ClusterNodeFlags ParseFlags(string flagsStr)
    {
        var flags = ClusterNodeFlags.None;
        var flagParts = flagsStr.Split(',');

        foreach (var flag in flagParts)
        {
            flags |= flag.ToLowerInvariant() switch
            {
                "master" => ClusterNodeFlags.Master,
                "slave" or "replica" => ClusterNodeFlags.Replica,
                "myself" => ClusterNodeFlags.Myself,
                "fail?" or "pfail" => ClusterNodeFlags.PFail,
                "fail" => ClusterNodeFlags.Fail,
                "handshake" => ClusterNodeFlags.Handshake,
                "noaddr" => ClusterNodeFlags.NoAddr,
                "noflags" => ClusterNodeFlags.NoFlags,
                _ => ClusterNodeFlags.None
            };
        }

        return flags;
    }

    /// <summary>
    /// Parses slot ranges from the remaining parts of the line.
    /// Slots can be single values (e.g., "0") or ranges (e.g., "0-5460").
    /// </summary>
    private static HashSlotRange[] ParseSlots(string[] parts, int startIndex)
    {
        if (startIndex >= parts.Length)
        {
            return Array.Empty<HashSlotRange>();
        }

        var slots = new List<HashSlotRange>();

        for (int i = startIndex; i < parts.Length; i++)
        {
            var slotStr = parts[i];

            // Skip importing/migrating markers (e.g., "[1234-<-node-id]")
            if (slotStr.StartsWith('['))
            {
                continue;
            }

            // Check if it's a range (e.g., "0-5460")
            var dashIndex = slotStr.IndexOf('-');
            if (dashIndex > 0)
            {
                var startStr = slotStr.Substring(0, dashIndex);
                var endStr = slotStr.Substring(dashIndex + 1);

                if (int.TryParse(startStr, out var start) && int.TryParse(endStr, out var end))
                {
                    slots.Add(new HashSlotRange(start, end));
                }
            }
            else
            {
                // Single slot
                if (int.TryParse(slotStr, out var slot))
                {
                    slots.Add(HashSlotRange.Single(slot));
                }
            }
        }

        return slots.ToArray();
    }
}
