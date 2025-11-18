# Getting Started with Valkey.NET

## Quick Start

### Prerequisites
- .NET 9.0 SDK or later
- Valkey 9.0+ server (or Redis 7.x compatible server)

### Building the Library

```bash
# Clone the repository (or use your local copy)
cd valkey-csharp

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Build for release
dotnet build -c Release
```

### Current Status

✅ **Production Ready** for single-node and cluster deployments.

For detailed implementation status and roadmap, see [STATUS.md](STATUS.md).

## Architecture Overview

### Core Components

#### 1. RESP3 Protocol Layer
```csharp
using Valkey.Protocol;

// RESP3 value types
var stringValue = RespValue.BulkString("Hello, Valkey!");
var intValue = RespValue.Integer(42);
var arrayValue = RespValue.Array(new[] { stringValue, intValue });

// Parser and writer use System.IO.Pipelines for efficiency
```

#### 2. Connection Management
```csharp
using Valkey;
using Valkey.Configuration;

// Configure connection options
var options = new ValkeyOptions
{
    Endpoints = { new DnsEndPoint("localhost", 6379) },
    Password = "your-password",
    UseSsl = false,
    PreferResp3 = true,
    ConnectTimeout = 5000,
    KeepAlive = 60
};

// Or parse from connection string
var options = ValkeyOptions.Parse("localhost:6379,password=secret,ssl=true");

// Create and connect
await using var connection = await ValkeyConnection.ConnectAsync(
    options.Endpoints[0],
    options);
```

#### 3. Configuration Options

All available options:

```csharp
var options = new ValkeyOptions
{
    // Connection
    Endpoints = { new DnsEndPoint("localhost", 6379) },
    ConnectTimeout = 5000,
    CommandTimeout = 5000,
    KeepAlive = 60,

    // Security
    UseSsl = false,
    SslHost = "valkey.example.com",
    ClientCertificate = null,
    Password = "your-password",
    User = "your-username",

    // Protocol
    PreferResp3 = true,

    // Client
    ClientName = "MyApp",
    DefaultDatabase = 0,

    // Resilience
    AutoReconnect = true,
    MaxReconnectAttempts = -1, // infinite
    ReconnectBaseDelay = 1000,
    ReconnectMaxDelay = 30000,
    AbortOnConnectFail = true,

    // Performance
    SendBufferSize = 32768,
    ReceiveBufferSize = 32768,

    // Advanced
    AllowAdmin = false,
    IsCluster = false
};
```

## Design Principles

### 1. Zero-Allocation Performance

The library uses modern .NET patterns to minimize allocations:

```csharp
// Span<T> for stack-only operations
ReadOnlySpan<byte> command = "GET"u8;

// Memory<T> for async operations
ReadOnlyMemory<byte> key = Encoding.UTF8.GetBytes("mykey");

// ArrayPool for temporary buffers (planned)
byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### 2. System.IO.Pipelines

All I/O uses the high-performance Pipelines API:

```csharp
// Reading (inside parser)
var result = await pipeReader.ReadAsync(cancellationToken);
var buffer = result.Buffer;

// Writing (inside writer)
var span = pipeWriter.GetSpan(minimumSize);
// Write to span
pipeWriter.Advance(bytesWritten);
await pipeWriter.FlushAsync(cancellationToken);
```

### 3. Async/Await First

All I/O operations are asynchronous:

```csharp
// ValueTask for hot paths (common case is sync completion)
public async ValueTask<RespValue> ReadAsync(CancellationToken ct = default)
{
    // Implementation
}

// Task for operations that are always async
public async Task SendLoopAsync(CancellationToken ct)
{
    // Implementation
}
```

### 4. RESP3 Native

Full support for RESP3 protocol features:

```csharp
// RESP3 types
RespValue.SimpleString()   // +OK\r\n
RespValue.BulkString()     // $5\r\nhello\r\n
RespValue.Integer()        // :42\r\n
RespValue.Boolean()        // #t\r\n
RespValue.Double()         // ,3.14\r\n
RespValue.Array()          // *3\r\n...
RespValue.Map()            // %2\r\n...
RespValue.Set()            // ~5\r\n...
RespValue.Push()           // >4\r\n... (for pub/sub)
RespValue.Null()           // _\r\n
```

## Using the Library

### Basic Commands

```csharp
using Valkey;
using Valkey.Configuration;

// Connect to Valkey
var endpoint = new IPEndPoint(IPAddress.Loopback, 6379);
await using var connection = await ValkeyConnection.ConnectAsync(endpoint);
var db = connection.GetDatabase();

// String commands
await db.StringSetAsync("key", "value");
var value = await db.StringGetAsync("key");
Console.WriteLine(value); // Output: value

// Increment operations
await db.StringSetAsync("counter", "0");
await db.StringIncrementAsync("counter"); // Returns 1
await db.StringIncrementAsync("counter", 5); // Returns 6

// Hash commands
await db.HashSetAsync("user:1", "name", "Alice");
await db.HashSetAsync("user:1", "age", "30");
var name = await db.HashGetAsync("user:1", "name");
Console.WriteLine(name); // Output: Alice

// List commands
await db.ListLeftPushAsync("queue", "task1");
await db.ListLeftPushAsync("queue", "task2");
var task = await db.ListLeftPopAsync("queue");
Console.WriteLine(task); // Output: task2

// Set commands
await db.SetAddAsync("tags", "csharp");
await db.SetAddAsync("tags", "dotnet");
var exists = await db.SetContainsAsync("tags", "csharp");
Console.WriteLine(exists); // Output: True

// Sorted Set commands
await db.SortedSetAddAsync("leaderboard", "player1", 100);
await db.SortedSetAddAsync("leaderboard", "player2", 200);
var score = await db.SortedSetScoreAsync("leaderboard", "player1");
Console.WriteLine(score); // Output: 100

// Key operations
await db.KeyExpireAsync("session:123", TimeSpan.FromHours(1));
var exists = await db.KeyExistsAsync("session:123");
await db.KeyDeleteAsync("old-key");
```

### Transactions

Execute multiple commands atomically:

```csharp
using Valkey.Transactions;

// Create a transaction with fluent API
var transaction = db.CreateTransaction();

// Queue commands
transaction.StringSet("balance", "100");
transaction.StringIncrement("balance", 50);
transaction.StringDecrement("balance", 25);
transaction.HashSet("user:1", "lastTxn", DateTime.UtcNow.ToString());

// Execute atomically
var results = await transaction.ExecuteAsync();

// Process results
foreach (var result in results)
{
    Console.WriteLine($"Result: {result.Type}");
}

// Or use fluent syntax
var results = await db.CreateTransaction()
    .StringSet("key1", "value1")
    .StringSet("key2", "value2")
    .StringIncrement("counter")
    .ExecuteAsync();
```

### Pub/Sub

Publish messages to channels:

```csharp
// Publish a message
var subscriberCount = await db.PublishAsync("notifications", "Hello, World!");
Console.WriteLine($"Message sent to {subscriberCount} subscribers");

// Publish to multiple channels
await db.PublishAsync("channel1", "message1");
await db.PublishAsync("channel2", "message2");
```

### Concurrent Operations

The library is fully thread-safe and supports concurrent operations:

```csharp
// Execute multiple commands concurrently
var tasks = new List<Task<string?>>();
for (int i = 0; i < 100; i++)
{
    tasks.Add(db.StringGetAsync($"key:{i}"));
}
var results = await Task.WhenAll(tasks);

// Parallel writes
var writeTasks = Enumerable.Range(0, 100)
    .Select(i => db.StringSetAsync($"key:{i}", $"value:{i}"))
    .ToList();
await Task.WhenAll(writeTasks);
```

### Running Tests

```bash
# Run all tests (includes integration tests with Testcontainers)
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~TransactionIntegrationTests"

# Run with verbose output
dotnet test --verbosity normal
```

### Running Benchmarks

```bash
dotnet run --project tests/Valkey.Benchmarks -c Release
```

## Performance Characteristics

### Current Implementation

- **Zero allocations** in protocol parsing (uses pipelines)
- **Zero allocations** in protocol writing (uses Span<T>)
- **Minimal allocations** for RespValue objects (pooling planned)
- **Async I/O** throughout (no blocking)
- **Pipelining support** built into I/O layer

### Planned Optimizations

- ArrayPool for temporary buffers
- Object pooling for RespValue
- Auto-pipelining for batching commands
- Connection pooling
- Cluster-aware routing

## Contributing

This is an early-stage project. Key areas for contribution:

1. **Command API** - Implement Valkey commands
2. **Tests** - Unit and integration tests
3. **Benchmarks** - Performance comparisons
4. **Documentation** - Samples and guides
5. **Advanced Features** - Cluster, Sentinel, Pub/Sub

## Architecture Diagrams

### Connection Flow

```
User Code
    ↓
ValkeyConnection
    ↓
├── Socket (TCP)
├── NetworkStream
├── SslStream (optional)
└── Pipes
    ├── ReceiveLoop → PipeWriter → Resp3Parser
    └── SendLoop ← PipeReader ← Resp3Writer
```

### Protocol Stack

```
┌─────────────────────────────┐
│     Command API (Phase 2)   │
├─────────────────────────────┤
│   Connection Management     │
├─────────────────────────────┤
│   RESP3 Parser/Writer       │
├─────────────────────────────┤
│   System.IO.Pipelines       │
├─────────────────────────────┤
│   NetworkStream/SslStream   │
├─────────────────────────────┤
│   Socket (TCP)              │
└─────────────────────────────┘
```

## FAQ

### Q: Can I use this in production?
A: The core features are complete and tested. All basic commands, transactions, and pub/sub are working. However, advanced features like Lua scripting, streams, cluster support, and auto-pipelining are still in development.

### Q: How does this compare to StackExchange.Redis?
A:
- **Similarities**: Connection management patterns, async/await, similar API design
- **Differences**: RESP3 native, .NET 9 only, modern performance patterns, Valkey 9.0 focus, transaction fluent API
- **Status**: Core MVP feature-complete, benchmarks show competitive performance

### Q: What's the performance target?
A: Match or exceed StackExchange.Redis performance while supporting RESP3 features. Benchmarks show comparable or better performance for basic operations.

### Q: Will this support Redis?
A: Yes, Valkey is Redis-compatible. The library will work with Redis 7.x and later.

### Q: What features are complete?
A: All core commands (String, Hash, List, Set, Sorted Set, Key operations), Transactions (MULTI/EXEC), Pub/Sub (PUBLISH), comprehensive tests, and benchmarks.

## Resources

- [Valkey Documentation](https://valkey.io/)
- [RESP3 Specification](https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md)
- [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)
- [valkey-go](https://github.com/valkey-io/valkey-go)

## License

MIT License - See LICENSE file for details

---

**Status**: Phases 1-5 Complete ✅ | Phase 6 In Progress 🚧
**Version**: 0.3.0-alpha
**Last Updated**: 2025-11-16
