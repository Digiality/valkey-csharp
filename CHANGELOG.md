# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.6.0-alpha] - 2025-01-18

### Added

#### Core Protocol & Connection
- RESP3 protocol support with automatic RESP2 fallback for compatibility
- High-performance connection management using System.IO.Pipelines
- Three-pipe concurrent architecture (socket read, send, receive tasks) for optimal throughput
- Channel-based request/response correlation leveraging Redis FIFO guarantees
- Connection multiplexing for efficient resource utilization
- Automatic protocol negotiation via `HELLO 3` command

#### Command Coverage
- **String Operations**: GET, SET, MGET, MSET, INCR, DECR, INCRBY, DECRBY, APPEND, GETRANGE, SETRANGE, STRLEN, GETEX, SETEX, SETNX, GETSET
- **Hash Operations**: HGET, HSET, HMGET, HMSET, HGETALL, HDEL, HEXISTS, HKEYS, HVALS, HLEN, HINCRBY, HINCRBYFLOAT, HSETNX
- **List Operations**: LPUSH, RPUSH, LPOP, RPOP, LLEN, LRANGE, LINDEX, LSET, LREM, LTRIM, LINSERT, BLPOP, BRPOP, BRPOPLPUSH, RPOPLPUSH, LPOS, LMOVE, BLMOVE
- **Set Operations**: SADD, SREM, SMEMBERS, SISMEMBER, SCARD, SPOP, SRANDMEMBER, SINTER, SUNION, SDIFF, SINTERSTORE, SUNIONSTORE, SDIFFSTORE, SMOVE, SMISMEMBER
- **Sorted Set Operations**: ZADD, ZREM, ZSCORE, ZCARD, ZCOUNT, ZINCRBY, ZRANGE, ZREVRANGE, ZRANGEBYSCORE, ZREVRANGEBYSCORE, ZRANK, ZREVRANK, ZREMRANGEBYRANK, ZREMRANGEBYSCORE, ZPOPMIN, ZPOPMAX, BZPOPMIN, BZPOPMAX, ZINTER, ZUNION, ZDIFF, ZINTERSTORE, ZUNIONSTORE, ZDIFFSTORE, ZMSCORE, ZRANDMEMBER, ZRANGESTORE, ZLEXCOUNT, ZRANGEBYLEX, ZREVRANGEBYLEX, ZREMRANGEBYLEX
- **Key Operations**: DEL, EXISTS, EXPIRE, EXPIREAT, TTL, PTTL, PERSIST, RENAME, RENAMENX, TYPE, KEYS, SCAN, RANDOMKEY, DUMP, RESTORE, TOUCH, UNLINK, COPY, EXPIRETIME, PEXPIRETIME
- **Geospatial Operations**: GEOADD, GEODIST, GEOHASH, GEOPOS, GEORADIUS, GEORADIUSBYMEMBER, GEOSEARCH (with Valkey 9.0 polygon support), GEOSEARCHSTORE
- **Utility Operations**: PING, ECHO

#### Advanced Features
- **Pub/Sub**: Dedicated subscriber with SUBSCRIBE, PSUBSCRIBE, UNSUBSCRIBE, PUNSUBSCRIBE, PUBLISH support
- **Transactions**: MULTI/EXEC implementation with fluent API for local command batching
- **Lua Scripting**: EVAL, EVALSHA, SCRIPT LOAD, SCRIPT EXISTS, SCRIPT FLUSH support
- **Streams**: XADD, XREAD, XRANGE, XREVRANGE, XLEN, XDEL, XTRIM, XACK, XPENDING, XCLAIM, XAUTOCLAIM, XGROUP CREATE, XGROUP DESTROY, XGROUP SETID, XREADGROUP, consumer group support
- **Cluster Mode**: CLUSTER NODES, CLUSTER SLOTS, hash slot calculation, MOVED/ASK redirection handling

#### Performance & Design
- Zero-allocation design patterns in hot paths
- UTF8 string literals for compile-time command encoding
- Custom integer formatting to avoid allocations
- Buffer pooling infrastructure (CommandBuilder, ArgumentArrayPool)
- Aggressive inlining for performance-critical methods
- Direct PipeWriter usage avoiding StringBuilder allocations

#### Testing & Quality
- 305+ integration tests with Testcontainers (automatic Docker container management)
- Protocol-level parser tests for correctness
- Dual RESP3/RESP2 protocol testing
- Comprehensive benchmarks comparing with StackExchange.Redis
- FluentAssertions for readable test assertions

#### Developer Experience
- Command executor architecture with single responsibility (StringCommandExecutor, HashCommandExecutor, etc.)
- Comprehensive XML documentation on all public APIs
- Sample application demonstrating all features (BasicUsage)
- Support for dependency injection via IValkeyClient, IValkeyDatabase abstractions
- Cancellation token support throughout

### Known Limitations
- Sentinel support not yet implemented (planned for future release)
- Advanced cluster features (replica reads, read-from-replica preference) not yet implemented
- Client-side caching not yet implemented
- Auto-pipelining not yet implemented

### Dependencies
- .NET 9.0 (C# 13)
- System.IO.Pipelines 9.0.0
- System.Threading.Channels 9.0.0
- System.Diagnostics.DiagnosticSource 9.0.0
- Polly.Core 8.5.0 (resilience)

### Breaking Changes
- This is the first alpha release; no backwards compatibility guarantees until 1.0.0

### Security
- No known security vulnerabilities
- Built with secure defaults (TLS support ready but not yet implemented)

### Documentation
- Comprehensive README with getting started guide
- Architecture documentation in CLAUDE.md
- Detailed roadmap in ROADMAP.md
- Project status tracking in STATUS.md
- Go-live checklist in GO-LIVE.md

---

## Release Notes

This is the **first alpha release** of Valkey.NET. The library is feature-complete for single-node and cluster deployments with extensive command coverage.

**What's Ready**:
- ✅ Production-ready RESP3/RESP2 protocol implementation
- ✅ Complete coverage of core Redis/Valkey commands
- ✅ Cluster mode with redirection handling
- ✅ Pub/Sub, Transactions, Streams, Lua scripting
- ✅ High-performance zero-allocation design
- ✅ 305 tests passing with Testcontainers

**What's Coming**:
- Sentinel support for high availability
- Advanced cluster features (replica reads)
- Client-side caching
- TLS/SSL support
- Connection pooling optimizations

**Feedback Welcome**: This is an alpha release. Please report issues, suggest improvements, and contribute at https://github.com/Digiality/valkey-csharp

---

[Unreleased]: https://github.com/Digiality/valkey-csharp/compare/v0.6.0-alpha...HEAD
[0.6.0-alpha]: https://github.com/Digiality/valkey-csharp/releases/tag/v0.6.0-alpha
