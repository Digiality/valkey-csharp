using System.Net;
using Valkey;
using Valkey.Configuration;

Console.WriteLine("Valkey.NET - Basic Usage Example");
Console.WriteLine("=================================\n");

// Configure connection options
var options = new ValkeyOptions
{
    Endpoints = { new DnsEndPoint("localhost", 6379) },
    // Uncomment if you need authentication:
    // Password = "your-password",
    // User = "your-username",
    ConnectTimeout = 5000,
    PreferResp3 = true
};

// Or use a connection string:
// var options = ValkeyOptions.Parse("localhost:6379,password=secret");

Console.WriteLine($"Connecting to {options.Endpoints[0]}...");

try
{
    // Connect to Valkey
    await using var connection = await ValkeyConnection.ConnectAsync(
        options.Endpoints[0],
        options);

    Console.WriteLine("✅ Connected successfully!\n");

    // Get a database instance
    var db = connection.GetDatabase();

    // ===== PING =====
    Console.WriteLine("--- Testing PING ---");
    var pong = await db.PingAsync();
    Console.WriteLine($"PING => {pong}\n");

    // ===== ECHO =====
    Console.WriteLine("--- Testing ECHO ---");
    var echo = await db.EchoAsync("Hello, Valkey!");
    Console.WriteLine($"ECHO 'Hello, Valkey!' => {echo}\n");

    // ===== STRING OPERATIONS =====
    Console.WriteLine("--- Testing String Operations ---");

    // SET a key
    var setResult = await db.StringSetAsync("mykey", "Hello from Valkey.NET!");
    Console.WriteLine($"SET mykey => {setResult}");

    // GET the key
    var getValue = await db.StringGetAsync("mykey");
    Console.WriteLine($"GET mykey => {getValue}");

    // SET with expiry
    await db.StringSetAsync("tempkey", "This expires in 60 seconds", TimeSpan.FromSeconds(60));
    Console.WriteLine("SET tempkey with 60s expiry");

    // Check if key exists
    var exists = await db.KeyExistsAsync("mykey");
    Console.WriteLine($"EXISTS mykey => {exists}");

    // ===== COUNTER OPERATIONS =====
    Console.WriteLine("\n--- Testing Counter Operations ---");

    // Initialize counter
    await db.StringSetAsync("counter", "0");
    Console.WriteLine("Initialized counter = 0");

    // Increment
    var val1 = await db.StringIncrementAsync("counter");
    Console.WriteLine($"INCR counter => {val1}");

    // Increment by 5
    var val2 = await db.StringIncrementAsync("counter", 5);
    Console.WriteLine($"INCRBY counter 5 => {val2}");

    // Decrement
    var val3 = await db.StringDecrementAsync("counter");
    Console.WriteLine($"DECR counter => {val3}");

    // Decrement by 2
    var val4 = await db.StringDecrementAsync("counter", 2);
    Console.WriteLine($"DECRBY counter 2 => {val4}");

    // ===== BINARY DATA =====
    Console.WriteLine("\n--- Testing Binary Data ---");

    // Store binary data
    byte[] binaryData = { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello" in bytes
    await db.StringSetAsync("binarykey", binaryData);
    Console.WriteLine("SET binarykey (binary data)");

    // Retrieve binary data
    var retrievedBytes = await db.StringGetBytesAsync("binarykey");
    Console.WriteLine($"GET binarykey => {BitConverter.ToString(retrievedBytes ?? Array.Empty<byte>())}");

    // ===== KEY OPERATIONS =====
    Console.WriteLine("\n--- Testing Key Operations ---");

    // Set expiry on existing key
    await db.KeyExpireAsync("mykey", TimeSpan.FromMinutes(5));
    Console.WriteLine("SET mykey expiry to 5 minutes");

    // Delete a key
    var deleted = await db.KeyDeleteAsync("tempkey");
    Console.WriteLine($"DEL tempkey => {deleted}");

    // Delete multiple keys
    await db.StringSetAsync("key1", "value1");
    await db.StringSetAsync("key2", "value2");
    await db.StringSetAsync("key3", "value3");
    var deletedCount = await db.KeyDeleteAsync(new[] { "key1", "key2", "key3" });
    Console.WriteLine($"DEL key1 key2 key3 => {deletedCount} keys deleted");

    // ===== PERFORMANCE TEST =====
    Console.WriteLine("\n--- Testing Performance (1000 operations) ---");

    var sw = System.Diagnostics.Stopwatch.StartNew();

    var tasks = new List<Task>();
    for (int i = 0; i < 1000; i++)
    {
        tasks.Add(db.StringSetAsync($"perftest:{i}", $"value{i}").AsTask());
    }

    await Task.WhenAll(tasks);
    sw.Stop();

    Console.WriteLine($"✅ SET 1000 keys in {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"   Average: {sw.ElapsedMilliseconds / 1000.0:F2}ms per operation");
    Console.WriteLine($"   Throughput: {1000.0 / sw.Elapsed.TotalSeconds:F0} ops/sec");

    // Clean up performance test keys
    var perfKeys = Enumerable.Range(0, 1000).Select(i => $"perftest:{i}").ToArray();
    await db.KeyDeleteAsync(perfKeys);
    Console.WriteLine("   Cleaned up performance test keys");

    // ===== HASH OPERATIONS =====
    Console.WriteLine("\n--- Testing Hash Operations ---");

    // Set hash fields
    await db.HashSetAsync("user:1000", "name", "Alice");
    await db.HashSetAsync("user:1000", "email", "alice@example.com");
    await db.HashSetAsync("user:1000", "age", "30");
    Console.WriteLine("HSET user:1000 name, email, age");

    // Get hash field
    var userName = await db.HashGetAsync("user:1000", "name");
    Console.WriteLine($"HGET user:1000 name => {userName}");

    // Get all hash fields
    var userInfo = await db.HashGetAllAsync("user:1000");
    Console.WriteLine($"HGETALL user:1000 => {userInfo.Count} fields");
    foreach (var kvp in userInfo)
    {
        Console.WriteLine($"   {kvp.Key}: {kvp.Value}");
    }

    // Hash exists
    var hasEmail = await db.HashExistsAsync("user:1000", "email");
    Console.WriteLine($"HEXISTS user:1000 email => {hasEmail}");

    // Hash length
    var fieldCount = await db.HashLengthAsync("user:1000");
    Console.WriteLine($"HLEN user:1000 => {fieldCount}");

    // ===== LIST OPERATIONS =====
    Console.WriteLine("\n--- Testing List Operations ---");

    // Push to list
    await db.ListRightPushAsync("queue", "task1");
    await db.ListRightPushAsync("queue", "task2");
    await db.ListRightPushAsync("queue", "task3");
    Console.WriteLine("RPUSH queue task1, task2, task3");

    // Get list length
    var queueLen = await db.ListLengthAsync("queue");
    Console.WriteLine($"LLEN queue => {queueLen}");

    // Get list range
    var queueTasks = await db.ListRangeAsync("queue", 0, -1);
    Console.WriteLine($"LRANGE queue 0 -1 => [{string.Join(", ", queueTasks)}]");

    // Pop from list
    var poppedTask = await db.ListLeftPopAsync("queue");
    Console.WriteLine($"LPOP queue => {poppedTask}");

    // ===== SET OPERATIONS =====
    Console.WriteLine("\n--- Testing Set Operations ---");

    // Add to set
    await db.SetAddAsync("tags", new[] { "c#", "valkey", "redis", "nosql" });
    Console.WriteLine("SADD tags c#, valkey, redis, nosql");

    // Get set members
    var allTags = await db.SetMembersAsync("tags");
    Console.WriteLine($"SMEMBERS tags => [{string.Join(", ", allTags)}]");

    // Set contains
    var hasTag = await db.SetContainsAsync("tags", "valkey");
    Console.WriteLine($"SISMEMBER tags valkey => {hasTag}");

    // Set length
    var tagCount = await db.SetLengthAsync("tags");
    Console.WriteLine($"SCARD tags => {tagCount}");

    // Set operations
    await db.SetAddAsync("langs:backend", new[] { "c#", "java", "go", "rust" });
    await db.SetAddAsync("langs:systems", new[] { "c", "c++", "rust", "go" });
    var intersection = await db.SetIntersectAsync(new[] { "langs:backend", "langs:systems" });
    Console.WriteLine($"SINTER langs:backend langs:systems => [{string.Join(", ", intersection)}]");

    // ===== SORTED SET OPERATIONS =====
    Console.WriteLine("\n--- Testing Sorted Set Operations ---");

    // Add to sorted set
    await db.SortedSetAddAsync("leaderboard", new[]
    {
        ("Alice", 100.0),
        ("Bob", 85.0),
        ("Charlie", 92.0),
        ("Diana", 95.0)
    });
    Console.WriteLine("ZADD leaderboard Alice:100, Bob:85, Charlie:92, Diana:95");

    // Get sorted set length
    var leaderboardLen = await db.SortedSetLengthAsync("leaderboard");
    Console.WriteLine($"ZCARD leaderboard => {leaderboardLen}");

    // Get range by rank
    var topPlayers = await db.SortedSetRangeByRankAsync("leaderboard", 0, 2);
    Console.WriteLine($"ZRANGE leaderboard 0 2 => [{string.Join(", ", topPlayers)}]");

    // Get range with scores
    var topWithScores = await db.SortedSetRangeByRankWithScoresAsync("leaderboard", 0, -1);
    Console.WriteLine("ZRANGE leaderboard 0 -1 WITHSCORES =>");
    foreach (var (member, score) in topWithScores)
    {
        Console.WriteLine($"   {member}: {score}");
    }

    // Get score
    var aliceScore = await db.SortedSetScoreAsync("leaderboard", "Alice");
    Console.WriteLine($"ZSCORE leaderboard Alice => {aliceScore}");

    // Get rank
    var aliceRank = await db.SortedSetRankAsync("leaderboard", "Alice");
    Console.WriteLine($"ZRANK leaderboard Alice => {aliceRank}");

    Console.WriteLine("\n✅ All tests completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Error: {ex.Message}");
    Console.WriteLine($"   {ex.GetType().Name}");

    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }

    Console.WriteLine("\n💡 Make sure Valkey/Redis is running on localhost:6379");
    Console.WriteLine("   You can start it with: docker run -d -p 6379:6379 valkey/valkey:9");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
