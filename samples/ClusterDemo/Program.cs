using Valkey.Abstractions.Cluster;
using Valkey.Cluster;

namespace ClusterDemo;

/// <summary>
/// Demonstrates cluster support features including:
/// - Hash slot calculation with CRC16
/// - Hash tag support for multi-key operations
/// - Cluster topology parsing
/// </summary>
public class Program
{
    public static void Main()
    {
        Console.WriteLine("=== Valkey.NET Cluster Support Demo ===\n");

        DemoHashSlotCalculation();
        Console.WriteLine();

        DemoHashTags();
        Console.WriteLine();

        DemoClusterTopology();
        Console.WriteLine();

        DemoKeyRouting();
    }

    /// <summary>
    /// Demonstrates hash slot calculation for cluster key routing.
    /// </summary>
    private static void DemoHashSlotCalculation()
    {
        Console.WriteLine("1. Hash Slot Calculation");
        Console.WriteLine("------------------------");
        Console.WriteLine("Redis/Valkey clusters use 16,384 hash slots.");
        Console.WriteLine("Keys are mapped to slots using CRC16:\n");

        var keys = new[]
        {
            "user:1000",
            "user:1001",
            "session:abc123",
            "cart:user1000"
        };

        foreach (var key in keys)
        {
            var slot = HashSlotCalculator.CalculateSlot(key);
            Console.WriteLine($"  {key,-20} → Slot {slot,5}");
        }
    }

    /// <summary>
    /// Demonstrates hash tags for ensuring keys route to the same slot.
    /// </summary>
    private static void DemoHashTags()
    {
        Console.WriteLine("2. Hash Tags for Multi-Key Operations");
        Console.WriteLine("--------------------------------------");
        Console.WriteLine("Hash tags (content between {}) ensure keys route to the same slot.\n");
        Console.WriteLine("This enables multi-key operations in cluster mode:\n");

        var keysWithHashTags = new[]
        {
            "{user1000}.profile",
            "{user1000}.following",
            "{user1000}.followers",
            "{user1000}.posts"
        };

        Console.WriteLine("All these keys use hash tag 'user1000':\n");

        foreach (var key in keysWithHashTags)
        {
            var slot = HashSlotCalculator.CalculateSlot(key);
            Console.WriteLine($"  {key,-25} → Slot {slot,5}");
        }

        var allSameSlot = HashSlotCalculator.AreKeysInSameSlot(keysWithHashTags);
        Console.WriteLine($"\n  ✓ All keys in same slot: {allSameSlot}");

        Console.WriteLine("\n  This allows atomic multi-key operations like:");
        Console.WriteLine("    - MGET {user1000}.profile {user1000}.following");
        Console.WriteLine("    - MULTI/EXEC with multiple {user1000}.* keys");
    }

    /// <summary>
    /// Demonstrates parsing cluster topology from CLUSTER NODES output.
    /// </summary>
    private static void DemoClusterTopology()
    {
        Console.WriteLine("3. Cluster Topology Parsing");
        Console.WriteLine("----------------------------");
        Console.WriteLine("Parse CLUSTER NODES output to understand cluster layout:\n");

        // Simulated CLUSTER NODES output (simplified for demo)
        var clusterNodesOutput = @"
07c37dfeb235213a872192d90877d0cd55635b91 127.0.0.1:7000@17000 myself,master - 0 1426238316232 0 connected 0-5460
67ed2db8d677e59ec4a4cefb06858cf2a1a89fa1 127.0.0.1:7001@17001 master - 0 1426238316232 0 connected 5461-10922
292f8b365bb7edb5e285caf0b7e6ddc7265d2f4f 127.0.0.1:7002@17002 master - 0 1426238316232 0 connected 10923-16383
";

        try
        {
            var nodes = ClusterTopologyParser.ParseClusterNodes(clusterNodesOutput);

            Console.WriteLine($"  Parsed {nodes.Count} cluster nodes:\n");

            foreach (var node in nodes)
            {
                var slotCount = node.Slots.Sum(range => range.Count);
                var role = node.IsMaster ? "Master" : "Replica";

                Console.WriteLine($"  Node: {node.EndPoint}");
                Console.WriteLine($"    Role: {role}");
                Console.WriteLine($"    ID: {node.NodeId[..8]}...");
                Console.WriteLine($"    Slots: {slotCount} slots");

                if (node.Slots.Length > 0)
                {
                    var firstRange = node.Slots[0];
                    var lastRange = node.Slots[^1];
                    Console.WriteLine($"    Range: {firstRange.Start}-{lastRange.End}");
                }

                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error parsing topology: {ex.Message}");
        }
    }

    /// <summary>
    /// Demonstrates grouping keys by their hash slots for routing.
    /// </summary>
    private static void DemoKeyRouting()
    {
        Console.WriteLine("4. Key Routing by Hash Slot");
        Console.WriteLine("----------------------------");
        Console.WriteLine("Group keys by slot to route to correct nodes:\n");

        var mixedKeys = new[]
        {
            "user:1000",
            "user:1001",
            "{cart:user1000}.items",
            "{cart:user1000}.total",
            "session:abc",
            "session:xyz"
        };

        var groupedBySlot = HashSlotCalculator.GroupKeysBySlot(mixedKeys);

        Console.WriteLine($"  Keys grouped into {groupedBySlot.Count} slots:\n");

        foreach (var (slot, keys) in groupedBySlot.OrderBy(kvp => kvp.Key))
        {
            Console.WriteLine($"  Slot {slot,5}:");
            foreach (var key in keys)
            {
                Console.WriteLine($"    - {key}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("  In a cluster client, each slot group would be");
        Console.WriteLine("  sent to the appropriate master node.");
    }
}
