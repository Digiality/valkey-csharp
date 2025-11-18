# Valkey.NET Performance Benchmarks

Performance comparison between Valkey.NET and StackExchange.Redis.

## Overview

This document contains benchmark results comparing Valkey.NET against StackExchange.Redis, the most popular .NET Redis client. The benchmarks measure both throughput (operations per second) and memory allocation efficiency.

## Benchmark Environment

- **Framework**: .NET 9.0
- **Server**: Valkey 8.0 (running in Docker)
- **Host**: localhost:6379
- **Tool**: BenchmarkDotNet
- **Configuration**: Release mode with optimizations enabled

## Methodology

All benchmarks:
- Run in Release mode with optimizations
- Use the same server instance for fair comparison
- Measure both execution time and memory allocations
- Include connection overhead in setup (excluded from measurements)
- Execute multiple iterations for statistical accuracy

## Running the Benchmarks

### Prerequisites

1. Start a Valkey/Redis server:
   ```bash
   docker run -p 6379:6379 valkey/valkey:8
   ```

2. Run benchmarks:
   ```bash
   dotnet run -c Release --project tests/Valkey.Benchmarks
   ```

### Output

Benchmarks generate:
- Console summary with timing and memory statistics
- Markdown report (`BenchmarkDotNet.Artifacts/results/*.md`)
- HTML report (`BenchmarkDotNet.Artifacts/results/*.html`)

## Benchmark Coverage

The benchmark suite includes **20+ benchmark methods** covering all major Valkey.NET features:

### 1. String Operations (6 benchmarks)
Tests basic string commands:
- **SET**: Set a string value
- **GET**: Retrieve a string value
- **INCR**: Increment a numeric value

**Why it matters**: String operations are the most common Redis operations, forming the foundation of most use cases.

### 2. Hash Operations (4 benchmarks)
Tests hash field operations:
- **HSET**: Set a hash field
- **HGET**: Get a hash field value

**Why it matters**: Hashes are frequently used for storing structured data like user profiles, session data, and objects.

### 3. List Operations (4 benchmarks)
Tests list operations:
- **LPUSH**: Push to list head
- **LPOP**: Pop from list head

**Why it matters**: Lists are essential for queues, activity streams, and message passing.

### 4. Set Operations (4 benchmarks)
Tests set membership:
- **SADD**: Add member to set
- **SISMEMBER**: Check membership

**Why it matters**: Sets are used for uniqueness constraints, tags, and relationship tracking.

### 5. Sorted Set Operations (4 benchmarks)
Tests scored set operations:
- **ZADD**: Add member with score
- **ZSCORE**: Get member score

**Why it matters**: Sorted sets power leaderboards, priority queues, and time-series data.

### 6. Transactions (2 benchmarks)
Tests MULTI/EXEC transactions:
- Execute 3 commands atomically (SET, INCR, HSET)

**Why it matters**: Transactions ensure atomicity for related operations and measure batch operation overhead.

### 7. Lua Scripting (3 benchmarks)
Tests server-side script execution:
- **EVAL**: Execute Lua script (conditional SET logic)
- **EVALSHA**: Execute cached script by SHA1

**Why it matters**: Lua scripts enable atomic complex operations, reduce network round-trips, and test server-side computation efficiency.

### 8. Streams (2 benchmarks)
Tests stream operations:
- **XADD**: Add entry to stream with 2 fields

**Why it matters**: Streams are used for event sourcing, message queues, and time-series data.

### 9. Connectivity (2 benchmarks)
Tests connection health:
- **PING**: Server connectivity check

**Why it matters**: Validates connection overhead and baseline latency.

## Expected Performance Characteristics

### Valkey.NET Advantages

1. **Zero-Allocation Design**
   - Uses `Span<T>` and `Memory<T>` throughout
   - ArrayPool for buffer management
   - Minimal GC pressure

2. **RESP3 Native**
   - Optimized for RESP3 protocol
   - Efficient type handling
   - Native support for new data types

3. **Modern .NET 9**
   - Latest runtime optimizations
   - System.IO.Pipelines for I/O
   - Improved async/await patterns

### StackExchange.Redis Advantages

1. **Battle-Tested**
   - Years of production optimization
   - Extensive real-world tuning
   - Mature codebase

2. **Implicit Pipelining**
   - Automatic command batching
   - Optimized multiplexer
   - Fire-and-forget support

## Interpreting Results

### Key Metrics

1. **Mean Time**: Average execution time per operation
   - Lower is better
   - Measured in microseconds (μs) or nanoseconds (ns)

2. **Allocated Memory**: Bytes allocated per operation
   - Lower is better
   - Zero allocation is ideal
   - Impacts GC pressure

3. **Throughput**: Operations per second
   - Higher is better
   - Calculated from mean time

### Performance Tiers

- **Excellent**: < 1 μs, 0 bytes allocated
- **Good**: 1-10 μs, < 100 bytes allocated
- **Acceptable**: 10-100 μs, < 1 KB allocated
- **Needs Improvement**: > 100 μs, > 1 KB allocated

## Sample Results Format

```
| Method                                    | Mean      | Error    | StdDev   | Allocated |
|------------------------------------------ |----------:|---------:|---------:|----------:|
| Valkey.NET: SET                          |  XX.XX μs | XX.XX μs | XX.XX μs |     XX B  |
| StackExchange.Redis: SET                 |  XX.XX μs | XX.XX μs | XX.XX μs |     XX B  |
| Valkey.NET: GET                          |  XX.XX μs | XX.XX μs | XX.XX μs |     XX B  |
| StackExchange.Redis: GET                 |  XX.XX μs | XX.XX μs | XX.XX μs |     XX B  |
```

## Performance Goals

### Primary Goals
1. **Competitive with StackExchange.Redis**: Match or exceed performance
2. **Zero or Near-Zero Allocations**: Minimize GC pressure
3. **Consistent Performance**: Low standard deviation

### Stretch Goals
1. **10-20% faster** than StackExchange.Redis for common operations
2. **50%+ fewer allocations** across all operations
3. **Sub-microsecond latency** for simple operations (SET, GET)

## Known Considerations

### Apples-to-Apples Comparison

While we strive for fair comparisons, note:

1. **Return Types**: 
   - Valkey.NET uses `string?` and native types
   - StackExchange.Redis uses `RedisValue` wrapper types
   - May affect allocation measurements

2. **RESP Protocol**:
   - Valkey.NET defaults to RESP3
   - StackExchange.Redis typically uses RESP2
   - Protocol differences may affect performance

3. **Design Philosophy**:
   - Valkey.NET: Modern, minimal, zero-allocation
   - StackExchange.Redis: Mature, feature-rich, battle-tested

### Factors Affecting Results

1. **Network Latency**: localhost minimizes but doesn't eliminate
2. **Server Load**: Ensure no other clients during benchmarking
3. **CPU State**: Disable power management for consistency
4. **Background Processes**: Close unnecessary applications
5. **Server Version**: Valkey 8 may behave differently than Redis 7

## Continuous Monitoring

Benchmarks should be run:
- Before each major release
- After performance optimizations
- When upgrading dependencies
- After protocol changes

Track results over time to:
- Detect performance regressions
- Validate optimizations
- Compare across .NET versions

## Contributing

To add new benchmarks:

1. Add methods to `tests/Valkey.Benchmarks/RedisComparison.cs`
2. Use `[Benchmark]` attribute
3. Include both Valkey.NET and StackExchange.Redis variants
4. Add descriptive `Description` parameter
5. Update this documentation

Example:
```csharp
[Benchmark(Description = "Valkey.NET: YOUR_COMMAND")]
public async Task ValkeyYourCommand()
{
    await _valkeyDb!.YourCommandAsync(...);
}

[Benchmark(Description = "StackExchange.Redis: YOUR_COMMAND")]
public async Task StackExchangeYourCommand()
{
    await _stackExchangeDb!.YourCommandAsync(...);
}
```

## Benchmark Results Archive

For historical benchmark results, see the `benchmarks/results/` directory (when created).

Each benchmark run should be tagged with:
- Date
- .NET version
- Valkey.NET version
- StackExchange.Redis version
- Valkey/Redis server version

## Further Reading

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [.NET Performance Tips](https://docs.microsoft.com/en-us/dotnet/core/performance/)
- [Valkey Performance Tuning](https://valkey.io/topics/benchmarks/)

## Benchmark Suite Statistics

- **Total Benchmark Methods**: 20+
- **Operation Categories**: 9 (String, Hash, List, Set, Sorted Set, Transactions, Lua, Streams, Connectivity)
- **Libraries Compared**: 2 (Valkey.NET vs StackExchange.Redis)
- **Infrastructure**: BenchmarkDotNet with MemoryDiagnoser
- **Build Status**: ✅ All tests passing

## Status

**Benchmark Suite**: ✅ Complete and ready for use

The benchmark suite covers all major Valkey.NET features with comprehensive performance comparison. It can be used to:
- Validate Valkey.NET performance claims
- Identify optimization opportunities
- Track performance across versions
- Compare with StackExchange.Redis objectively

**Completion Date**: 2025-11-16

## Disclaimer

Benchmarks are synthetic and may not reflect real-world performance. Always profile your specific use case with production-like data and load patterns.

Performance results will vary based on:
- Hardware configuration
- Network conditions
- Server tuning
- Data sizes
- Concurrency levels
- Use case patterns

Use these benchmarks as a **starting point** for performance evaluation, not as absolute measurements.
