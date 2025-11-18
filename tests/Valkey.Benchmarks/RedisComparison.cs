using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using StackExchange.Redis;
using System.Net;
using Valkey;
using Valkey.Configuration;

namespace Valkey.Benchmarks;

/// <summary>
/// Benchmarks comparing Valkey.NET vs StackExchange.Redis.
///
/// Run with: dotnet run -c Release --project tests/Valkey.Benchmarks
///
/// Note: Requires a Valkey/Redis server running on localhost:6379
/// Start with: docker run -p 6379:6379 valkey/valkey:8
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MarkdownExporter]
[HtmlExporter]
public class RedisComparison
{
    private ValkeyConnection? _valkeyConnection;
    private ValkeyDatabase? _valkeyDb;
    private ConnectionMultiplexer? _stackExchangeConnection;
    private IDatabase? _stackExchangeDb;

    // Use different keys for different data types
    private const string StringKey = "benchmark:string";
    private const string CounterKey = "benchmark:counter";
    private const string HashKey = "benchmark:hash";
    private const string ListKey = "benchmark:list";
    private const string SetKey = "benchmark:set";
    private const string SortedSetKey = "benchmark:zset";
    private const string StreamKey = "benchmark:stream";
    private const string PingKey = "benchmark:ping";

    [GlobalSetup]
    public async Task Setup()
    {
        // Setup Valkey.NET connection
        var endpoint = new IPEndPoint(IPAddress.Loopback, 6379);
        _valkeyConnection = await ValkeyConnection.ConnectAsync(
            endpoint,
            new ValkeyOptions
            {
                ConnectTimeout = 5000,
                CommandTimeout = 5000
            });
        _valkeyDb = _valkeyConnection.GetDatabase();

        // Setup StackExchange.Redis connection
        _stackExchangeConnection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        _stackExchangeDb = _stackExchangeConnection.GetDatabase();

        // Warm up and initialize keys
        await _valkeyDb.StringSetAsync(StringKey, "value");
        await _stackExchangeDb.StringSetAsync(StringKey, "value");

        await _valkeyDb.StringSetAsync(CounterKey, "0");
        await _stackExchangeDb.StringSetAsync(CounterKey, "0");

        await _valkeyDb.HashSetAsync(HashKey, "field1", "value1");
        await _stackExchangeDb.HashSetAsync(HashKey, "field1", "value1");

        await _valkeyDb.SetAddAsync(SetKey, "member");
        await _stackExchangeDb.SetAddAsync(SetKey, "member");

        await _valkeyDb.SortedSetAddAsync(SortedSetKey, "member", 100.0);
        await _stackExchangeDb.SortedSetAddAsync(SortedSetKey, "member", 100.0);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_valkeyConnection != null)
        {
            await _valkeyConnection.DisposeAsync();
        }

        if (_stackExchangeConnection != null)
        {
            await _stackExchangeConnection.DisposeAsync();
        }
    }

    #region String Operations

    [Benchmark(Description = "Valkey.NET: SET")]
    public async Task ValkeyStringSet()
    {
        await _valkeyDb!.StringSetAsync(StringKey, "value");
    }

    [Benchmark(Description = "StackExchange.Redis: SET")]
    public async Task StackExchangeStringSet()
    {
        await _stackExchangeDb!.StringSetAsync(StringKey, "value");
    }

    [Benchmark(Description = "Valkey.NET: GET")]
    public async Task<string?> ValkeyStringGet()
    {
        return await _valkeyDb!.StringGetAsync(StringKey);
    }

    [Benchmark(Description = "StackExchange.Redis: GET")]
    public async Task<RedisValue> StackExchangeStringGet()
    {
        return await _stackExchangeDb!.StringGetAsync(StringKey);
    }

    [Benchmark(Description = "Valkey.NET: INCR")]
    public async Task<long> ValkeyStringIncrement()
    {
        return await _valkeyDb!.StringIncrementAsync(CounterKey);
    }

    [Benchmark(Description = "StackExchange.Redis: INCR")]
    public async Task<long> StackExchangeStringIncrement()
    {
        return await _stackExchangeDb!.StringIncrementAsync(CounterKey);
    }

    #endregion

    #region Hash Operations

    [Benchmark(Description = "Valkey.NET: HSET")]
    public async Task ValkeyHashSet()
    {
        await _valkeyDb!.HashSetAsync(HashKey, "field1", "value1");
    }

    [Benchmark(Description = "StackExchange.Redis: HSET")]
    public async Task StackExchangeHashSet()
    {
        await _stackExchangeDb!.HashSetAsync(HashKey, "field1", "value1");
    }

    [Benchmark(Description = "Valkey.NET: HGET")]
    public async Task<string?> ValkeyHashGet()
    {
        return await _valkeyDb!.HashGetAsync(HashKey, "field1");
    }

    [Benchmark(Description = "StackExchange.Redis: HGET")]
    public async Task<RedisValue> StackExchangeHashGet()
    {
        return await _stackExchangeDb!.HashGetAsync(HashKey, "field1");
    }

    #endregion

    #region List Operations

    [Benchmark(Description = "Valkey.NET: LPUSH")]
    public async Task<long> ValkeyListPush()
    {
        return await _valkeyDb!.ListLeftPushAsync(ListKey, "value");
    }

    [Benchmark(Description = "StackExchange.Redis: LPUSH")]
    public async Task<long> StackExchangeListPush()
    {
        return await _stackExchangeDb!.ListLeftPushAsync(ListKey, "value");
    }

    [Benchmark(Description = "Valkey.NET: LPOP")]
    public async Task<string?> ValkeyListPop()
    {
        return await _valkeyDb!.ListLeftPopAsync(ListKey);
    }

    [Benchmark(Description = "StackExchange.Redis: LPOP")]
    public async Task<RedisValue> StackExchangeListPop()
    {
        return await _stackExchangeDb!.ListLeftPopAsync(ListKey);
    }

    #endregion

    #region Set Operations

    [Benchmark(Description = "Valkey.NET: SADD")]
    public async Task<bool> ValkeySetAdd()
    {
        return await _valkeyDb!.SetAddAsync(SetKey, "member");
    }

    [Benchmark(Description = "StackExchange.Redis: SADD")]
    public async Task<bool> StackExchangeSetAdd()
    {
        return await _stackExchangeDb!.SetAddAsync(SetKey, "member");
    }

    [Benchmark(Description = "Valkey.NET: SISMEMBER")]
    public async Task<bool> ValkeySetContains()
    {
        return await _valkeyDb!.SetContainsAsync(SetKey, "member");
    }

    [Benchmark(Description = "StackExchange.Redis: SISMEMBER")]
    public async Task<bool> StackExchangeSetContains()
    {
        return await _stackExchangeDb!.SetContainsAsync(SetKey, "member");
    }

    #endregion

    #region Sorted Set Operations

    [Benchmark(Description = "Valkey.NET: ZADD")]
    public async Task<bool> ValkeySortedSetAdd()
    {
        return await _valkeyDb!.SortedSetAddAsync(SortedSetKey, "member", 100.0);
    }

    [Benchmark(Description = "StackExchange.Redis: ZADD")]
    public async Task<bool> StackExchangeSortedSetAdd()
    {
        return await _stackExchangeDb!.SortedSetAddAsync(SortedSetKey, "member", 100.0);
    }

    [Benchmark(Description = "Valkey.NET: ZSCORE")]
    public async Task<double?> ValkeySortedSetScore()
    {
        return await _valkeyDb!.SortedSetScoreAsync(SortedSetKey, "member");
    }

    [Benchmark(Description = "StackExchange.Redis: ZSCORE")]
    public async Task<double?> StackExchangeSortedSetScore()
    {
        return await _stackExchangeDb!.SortedSetScoreAsync(SortedSetKey, "member");
    }

    #endregion

    #region Transactions

    [Benchmark(Description = "Valkey.NET: Transaction (3 commands)")]
    public async Task ValkeyTransaction()
    {
        var txn = _valkeyDb!.CreateTransaction();
        txn.StringSet("txn:key1", "value1");
        txn.StringIncrement("txn:counter");
        txn.HashSet("txn:hash", "field", "value");
        await txn.ExecuteAsync();
    }

    [Benchmark(Description = "StackExchange.Redis: Transaction (3 commands)")]
    public async Task StackExchangeTransaction()
    {
        var txn = _stackExchangeDb!.CreateTransaction();
        var t1 = txn.StringSetAsync("txn:key1", "value1");
        var t2 = txn.StringIncrementAsync("txn:counter");
        var t3 = txn.HashSetAsync("txn:hash", "field", "value");
        await txn.ExecuteAsync();
        await t1;
        await t2;
        await t3;
    }

    #endregion

    #region Lua Scripting

    private const string LuaScript = @"
        local current = redis.call('GET', KEYS[1])
        if not current or tonumber(current) < tonumber(ARGV[1]) then
            redis.call('SET', KEYS[1], ARGV[1])
            return 1
        end
        return 0
    ";

    private string? _stackExchangeScriptHash;

    [Benchmark(Description = "Valkey.NET: EVAL (Lua script)")]
    public async Task ValkeyEval()
    {
        await _valkeyDb!.ScriptEvaluateAsync(
            LuaScript,
            keys: new[] { "script:max" },
            args: new[] { "100" });
    }

    [Benchmark(Description = "StackExchange.Redis: EVAL (Lua script)")]
    public async Task StackExchangeEval()
    {
        await _stackExchangeDb!.ScriptEvaluateAsync(
            LuaScript,
            new RedisKey[] { "script:max" },
            new RedisValue[] { 100 });
    }

    [Benchmark(Description = "Valkey.NET: EVALSHA (cached script)")]
    public async Task ValkeyEvalSha()
    {
        if (_stackExchangeScriptHash == null)
        {
            _stackExchangeScriptHash = await _valkeyDb!.ScriptLoadAsync(LuaScript);
        }
        await _valkeyDb!.ScriptEvaluateShaAsync(
            _stackExchangeScriptHash,
            keys: new[] { "script:max" },
            args: new[] { "100" });
    }

    #endregion

    #region Streams

    [Benchmark(Description = "Valkey.NET: XADD (Stream)")]
    public async Task<string> ValkeyStreamAdd()
    {
        return await _valkeyDb!.StreamAddAsync(StreamKey, new Dictionary<string, string>
        {
            { "event", "test" },
            { "data", "benchmark" }
        });
    }

    [Benchmark(Description = "StackExchange.Redis: XADD (Stream)")]
    public async Task<RedisValue> StackExchangeStreamAdd()
    {
        return await _stackExchangeDb!.StreamAddAsync(StreamKey, new NameValueEntry[]
        {
            new NameValueEntry("event", "test"),
            new NameValueEntry("data", "benchmark")
        });
    }

    #endregion

    #region Ping

    [Benchmark(Description = "Valkey.NET: PING")]
    public async Task<string> ValkeyPing()
    {
        return await _valkeyDb!.PingAsync();
    }

    [Benchmark(Description = "StackExchange.Redis: PING")]
    public async Task<TimeSpan> StackExchangePing()
    {
        return await _stackExchangeDb!.PingAsync();
    }

    #endregion
}
