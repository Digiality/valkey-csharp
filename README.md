# Valkey.NET

[![CI](https://github.com/Digiality/valkey-csharp/actions/workflows/ci.yml/badge.svg)](https://github.com/Digiality/valkey-csharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Valkey.NET.svg)](https://www.nuget.org/packages/Valkey.NET/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Valkey.NET.svg)](https://www.nuget.org/packages/Valkey.NET/)
[![codecov](https://codecov.io/gh/Digiality/valkey-csharp/branch/main/graph/badge.svg)](https://codecov.io/gh/Digiality/valkey-csharp)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

A high-performance, modern Valkey client library for .NET 9+, built from the ground up with RESP3 protocol support and zero-allocation design patterns.

**Status**: ✅ Production ready for single-node and cluster deployments

## Features

- ✅ **RESP3 Protocol Native**: Full RESP3 support with RESP2 fallback
- ✅ **Zero-Allocation Design**: `Span<T>`, `Memory<T>`, and `ArrayPool` throughout
- ✅ **Modern .NET 9**: Latest framework features and performance
- ✅ **System.IO.Pipelines**: High-performance I/O with zero-copy operations
- ✅ **Complete Command Coverage**: String, Hash, List, Set, Sorted Set, Key operations
- ✅ **Advanced Features**: Pub/Sub, Transactions, Lua Scripting, Streams, Geospatial
- ✅ **Cluster Support**: MOVED/ASK redirection, topology management, connection pooling
- ✅ **Connection Multiplexing**: Efficient connection sharing across databases
- ✅ **Fully Async**: `ValueTask`-based API with proper cancellation support

## Quick Start

```csharp
using Valkey;

// Connect to Valkey
var endpoint = new IPEndPoint(IPAddress.Loopback, 6379);
await using var connection = await ValkeyConnection.ConnectAsync(endpoint);
var db = connection.GetDatabase();

// Basic operations
await db.StringSetAsync("key", "value");
var value = await db.StringGetAsync("key"); // "value"

// Hash operations
await db.HashSetAsync("user:1", "name", "Alice");
var name = await db.HashGetAsync("user:1", "name"); // "Alice"

// Transactions
var tx = db.CreateTransaction();
tx.StringSet("counter", "0");
tx.StringIncrement("counter");
await tx.ExecuteAsync();

// Pub/Sub
await db.PublishAsync("notifications", "Hello!");

// Streams
await db.StreamAddAsync("events", new Dictionary<string, string>
{
    { "type", "user_login" },
    { "user_id", "12345" }
});
```

For more examples, see the [Getting Started Guide](docs/GETTING_STARTED.md).

## Documentation

- 📘 [Getting Started Guide](docs/GETTING_STARTED.md) - Setup, usage, and examples
- 📖 [API Reference](docs/API_REFERENCE.md) - Complete API documentation
- 📊 [Status & Roadmap](docs/STATUS.md) - Current progress and future plans
- ⚡ [Benchmarks](docs/BENCHMARKS.md) - Performance comparison with StackExchange.Redis
- 🚀 [Go-Live Checklist](docs/GO-LIVE.md) - Production readiness guide

## Sample Applications

Explore complete working examples in the `samples/` directory:

- **BasicUsage** - Core commands and patterns
- **ScriptingDemo** - Lua scripting with EVAL/EVALSHA
- **StreamsDemo** - Streams and consumer groups
- **GeospatialDemo** - Geospatial commands with Valkey 9.0 features
- **ClusterDemo** - Cluster support foundation

```bash
dotnet run --project samples/BasicUsage
dotnet run --project samples/ScriptingDemo
```

## Requirements

- .NET 9.0 or later
- Valkey 8.0+ or Redis 7.x+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package Valkey.NET
```

Or via Package Manager Console:

```powershell
Install-Package Valkey.NET
```

For dependency injection scenarios, you can also install the abstractions package:

```bash
dotnet add package Valkey.NET.Abstractions
```

## Building & Testing

```bash
# Build
dotnet restore
dotnet build

# Run tests (uses Testcontainers - no manual Docker setup needed)
dotnet test

# Run benchmarks
dotnet run --project tests/Valkey.Benchmarks -c Release
```

All 305 integration tests use Testcontainers to automatically manage Valkey containers.

## Architecture Highlights

This library combines best practices from:
- **StackExchange.Redis**: Connection management patterns
- **valkey-go**: Zero-allocation and auto-pipelining techniques  
- **.NET 9**: Latest runtime optimizations

### Design Principles

1. **RESP3 First**: Built for RESP3 from the ground up
2. **Zero-Allocation**: Extensive use of `Span<T>`, `Memory<T>`, `ArrayPool`
3. **Async-Native**: All I/O operations use `ValueTask`
4. **Modern API**: Clean, idiomatic C# optimized for Valkey 9.0

## Performance

Valkey.NET is designed for high performance with:
- Zero-allocation command building
- System.IO.Pipelines for efficient I/O
- Optimized RESP3 parser and writer
- Auto-pipelining infrastructure
- Buffer pooling with ArrayPool

See [BENCHMARKS.md](docs/BENCHMARKS.md) for detailed performance comparison with StackExchange.Redis.

## Contributing

Contributions welcome! The project follows standard .NET development practices:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes with clear commits
4. Ensure all tests pass (`dotnet test`)
5. Submit a pull request

See the codebase for coding standards and patterns.

## License

Apache 2.0

## Acknowledgments

- **Valkey**: Open-source fork of Redis
- **StackExchange.Redis**: Inspiration for API design
- **valkey-go**: Performance optimization patterns
