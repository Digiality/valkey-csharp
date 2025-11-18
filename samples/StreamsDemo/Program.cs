using System.Net;
using Valkey;
using Valkey.Abstractions;
using Valkey.Abstractions.Streams;
using Valkey.Configuration;

Console.WriteLine("=== Valkey Streams Demo ===\n");

var options = new ValkeyOptions
{
    Endpoints = { new DnsEndPoint("localhost", 6379) },
    PreferResp3 = true
};

await using var connection = await ValkeyConnection.ConnectAsync(
    options.Endpoints[0],
    options);
var db = connection.GetDatabase();

try
{
    // Demo 1: Basic Stream Operations
    await BasicStreamOperations(db);

    // Demo 2: Reading Stream Range
    await ReadStreamRange(db);

    // Demo 3: Consumer Groups
    await ConsumerGroupDemo(db);

    // Demo 4: Event Sourcing Pattern
    await EventSourcingPattern(db);

    // Demo 5: Stream Trimming
    await StreamTrimmingDemo(db);
}
finally
{
    // Cleanup
    Console.WriteLine("\n=== Cleanup ===");
    await db.KeyDeleteAsync("events:orders");
    await db.KeyDeleteAsync("events:user-actions");
    await db.KeyDeleteAsync("events:notifications");
    await db.KeyDeleteAsync("stream:trimming");
    Console.WriteLine("Demo keys deleted");
}

static async Task BasicStreamOperations(ValkeyDatabase db)
{
    Console.WriteLine("=== Demo 1: Basic Stream Operations ===");

    // Add entries to stream
    var id1 = await db.StreamAddAsync(
        "events:orders",
        new Dictionary<string, string>
        {
            { "order_id", "ORD-1001" },
            { "customer", "Alice" },
            { "amount", "99.99" },
            { "status", "pending" }
        }
    );
    Console.WriteLine($"Added order event: {id1}");

    var id2 = await db.StreamAddAsync(
        "events:orders",
        new Dictionary<string, string>
        {
            { "order_id", "ORD-1002" },
            { "customer", "Bob" },
            { "amount", "149.99" },
            { "status", "pending" }
        }
    );
    Console.WriteLine($"Added order event: {id2}");

    // Get stream length
    var length = await db.StreamLengthAsync("events:orders");
    Console.WriteLine($"Stream length: {length}");

    // Read all entries
    var entries = await db.StreamReadAsync("events:orders", "0");
    Console.WriteLine($"\nAll entries in stream:");
    foreach (var entry in entries)
    {
        Console.WriteLine($"  ID: {entry.Id}");
        foreach (var field in entry.Fields)
        {
            Console.WriteLine($"    {field.Key}: {field.Value}");
        }
    }
    Console.WriteLine();
}

static async Task ReadStreamRange(ValkeyDatabase db)
{
    Console.WriteLine("=== Demo 2: Reading Stream Range ===");

    // Add timestamped events
    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    await db.StreamAddAsync(
        "events:user-actions",
        new Dictionary<string, string>
        {
            { "user", "user123" },
            { "action", "login" },
            { "timestamp", now.ToString() }
        }
    );

    await Task.Delay(10); // Small delay to ensure different timestamps

    await db.StreamAddAsync(
        "events:user-actions",
        new Dictionary<string, string>
        {
            { "user", "user123" },
            { "action", "view_product" },
            { "product_id", "PROD-500" }
        }
    );

    await db.StreamAddAsync(
        "events:user-actions",
        new Dictionary<string, string>
        {
            { "user", "user123" },
            { "action", "add_to_cart" },
            { "product_id", "PROD-500" }
        }
    );

    // Read range of entries
    var rangeEntries = await db.StreamRangeAsync("events:user-actions", "-", "+", 10);
    Console.WriteLine("User actions timeline:");
    foreach (var entry in rangeEntries)
    {
        var action = entry.Fields.GetValueOrDefault("action", "unknown");
        Console.WriteLine($"  [{entry.Id}] Action: {action}");
        if (entry.Fields.ContainsKey("product_id"))
        {
            Console.WriteLine($"    Product: {entry.Fields["product_id"]}");
        }
    }
    Console.WriteLine();
}

static async Task ConsumerGroupDemo(ValkeyDatabase db)
{
    Console.WriteLine("=== Demo 3: Consumer Groups ===");

    // Add some notifications
    await db.StreamAddAsync(
        "events:notifications",
        new Dictionary<string, string>
        {
            { "type", "email" },
            { "recipient", "alice@example.com" },
            { "subject", "Order Confirmation" }
        }
    );

    await db.StreamAddAsync(
        "events:notifications",
        new Dictionary<string, string>
        {
            { "type", "sms" },
            { "recipient", "+1234567890" },
            { "message", "Your order has shipped" }
        }
    );

    await db.StreamAddAsync(
        "events:notifications",
        new Dictionary<string, string>
        {
            { "type", "push" },
            { "recipient", "user123" },
            { "title", "Delivery Update" },
            { "body", "Your package will arrive tomorrow" }
        }
    );

    // Create consumer group
    try
    {
        await db.StreamGroupCreateAsync("events:notifications", "notification-processors", "0");
        Console.WriteLine("Created consumer group: notification-processors");
    }
    catch
    {
        // Group might already exist from previous run
        Console.WriteLine("Consumer group already exists");
    }

    // Consumer 1: Email processor
    var emailMessages = await db.StreamReadGroupAsync(
        "events:notifications",
        "notification-processors",
        "email-worker",
        ">",
        count: 10
    );

    Console.WriteLine("\nEmail Worker Processing:");
    foreach (var entry in emailMessages)
    {
        var type = entry.Fields.GetValueOrDefault("type", "");
        if (type == "email")
        {
            Console.WriteLine($"  Processing email to {entry.Fields["recipient"]}");
            // Acknowledge message
            await db.StreamAckAsync("events:notifications", "notification-processors", new[] { entry.Id });
            Console.WriteLine($"  Acknowledged: {entry.Id}");
        }
    }

    // Consumer 2: SMS processor
    var smsMessages = await db.StreamReadGroupAsync(
        "events:notifications",
        "notification-processors",
        "sms-worker",
        ">",
        count: 10
    );

    Console.WriteLine("\nSMS Worker Processing:");
    foreach (var entry in smsMessages)
    {
        var type = entry.Fields.GetValueOrDefault("type", "");
        if (type == "sms")
        {
            Console.WriteLine($"  Processing SMS to {entry.Fields["recipient"]}");
            await db.StreamAckAsync("events:notifications", "notification-processors", new[] { entry.Id });
            Console.WriteLine($"  Acknowledged: {entry.Id}");
        }
    }

    // Consumer 3: Push notification processor
    var pushMessages = await db.StreamReadGroupAsync(
        "events:notifications",
        "notification-processors",
        "push-worker",
        ">",
        count: 10
    );

    Console.WriteLine("\nPush Worker Processing:");
    foreach (var entry in pushMessages)
    {
        var type = entry.Fields.GetValueOrDefault("type", "");
        if (type == "push")
        {
            Console.WriteLine($"  Processing push to {entry.Fields["recipient"]}");
            await db.StreamAckAsync("events:notifications", "notification-processors", new[] { entry.Id });
            Console.WriteLine($"  Acknowledged: {entry.Id}");
        }
    }
    Console.WriteLine();
}

static async Task EventSourcingPattern(ValkeyDatabase db)
{
    Console.WriteLine("=== Demo 4: Event Sourcing Pattern ===");

    // Simulate order lifecycle events
    var orderId = "ORD-2001";

    // Event 1: Order Created
    await db.StreamAddAsync(
        "events:orders",
        new Dictionary<string, string>
        {
            { "event_type", "OrderCreated" },
            { "order_id", orderId },
            { "customer", "Charlie" },
            { "total_amount", "299.99" }
        }
    );

    // Event 2: Payment Received
    await db.StreamAddAsync(
        "events:orders",
        new Dictionary<string, string>
        {
            { "event_type", "PaymentReceived" },
            { "order_id", orderId },
            { "amount", "299.99" },
            { "payment_method", "credit_card" }
        }
    );

    // Event 3: Order Shipped
    await db.StreamAddAsync(
        "events:orders",
        new Dictionary<string, string>
        {
            { "event_type", "OrderShipped" },
            { "order_id", orderId },
            { "tracking_number", "TRACK123456" },
            { "carrier", "FedEx" }
        }
    );

    // Reconstruct order state from events
    Console.WriteLine($"Reconstructing order {orderId} from event stream:");
    var orderEvents = await db.StreamReadAsync("events:orders", "0");

    string? customer = null;
    string? amount = null;
    string? trackingNumber = null;

    foreach (var entry in orderEvents.Where(e => e.Fields.GetValueOrDefault("order_id") == orderId))
    {
        var eventType = entry.Fields["event_type"];
        Console.WriteLine($"  [{entry.Id}] {eventType}");

        switch (eventType)
        {
            case "OrderCreated":
                customer = entry.Fields["customer"];
                amount = entry.Fields["total_amount"];
                break;
            case "PaymentReceived":
                Console.WriteLine($"    Payment: ${entry.Fields["amount"]} via {entry.Fields["payment_method"]}");
                break;
            case "OrderShipped":
                trackingNumber = entry.Fields["tracking_number"];
                Console.WriteLine($"    Tracking: {trackingNumber}");
                break;
        }
    }

    Console.WriteLine($"\nFinal Order State:");
    Console.WriteLine($"  Customer: {customer}");
    Console.WriteLine($"  Amount: ${amount}");
    Console.WriteLine($"  Tracking: {trackingNumber}");
    Console.WriteLine("  Status: Shipped");
    Console.WriteLine();
}

static async Task StreamTrimmingDemo(ValkeyDatabase db)
{
    Console.WriteLine("=== Demo 5: Stream Trimming ===");

    // Add entries with max length constraint
    for (int i = 1; i <= 10; i++)
    {
        await db.StreamAddAsync(
            "stream:trimming",
            new Dictionary<string, string>
            {
                { "sequence", i.ToString() },
                { "data", $"Entry {i}" }
            },
            maxLength: 5  // Keep only last 5 entries
        );
    }

    var length = await db.StreamLengthAsync("stream:trimming");
    Console.WriteLine($"Stream length after adding 10 entries with maxLength=5: {length}");

    var entries = await db.StreamReadAsync("stream:trimming", "0");
    Console.WriteLine("Remaining entries:");
    foreach (var entry in entries)
    {
        Console.WriteLine($"  Sequence: {entry.Fields["sequence"]}, Data: {entry.Fields["data"]}");
    }

    // Manual trim
    var trimmed = await db.StreamTrimAsync("stream:trimming", 3);
    Console.WriteLine($"\nTrimmed to 3 entries, removed: {trimmed}");

    length = await db.StreamLengthAsync("stream:trimming");
    Console.WriteLine($"Final stream length: {length}");
    Console.WriteLine();
}
