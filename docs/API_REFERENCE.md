# Valkey.NET API Reference

Complete API reference for Valkey.NET library.

## Table of Contents

- [Connection Management](#connection-management)
  - [ValkeyConnection](#valkeyconnection)
  - [ValkeyMultiplexer](#valkeymultiplexer)
- [Database Operations](#database-operations)
- [String Commands](#string-commands)
- [Hash Commands](#hash-commands)
- [List Commands](#list-commands)
- [Set Commands](#set-commands)
- [Sorted Set Commands](#sorted-set-commands)
- [Key Commands](#key-commands)
- [Transactions](#transactions)
- [Lua Scripting](#lua-scripting)
- [Streams](#streams)
- [Pub/Sub](#pubsub)
  - [PublishAsync](#publishasync)
  - [ValkeySubscriber](#valkeysubscriber)
  - [PubSubMessage](#pubsubmessage)
  - [Pub/Sub Patterns](#pubsub-patterns)
  - [Pub/Sub Best Practices](#pubsub-best-practices)
- [Geospatial Commands](#geospatial-commands)
- [Cluster Support](#cluster-support)
  - [IValkeyCluster Interface](#ivalkeycluster-interface)
  - [ValkeyClusterOptions](#valkeyclusteroptions)
  - [ClusterNode](#clusternode)
  - [Cluster Behavior](#cluster-behavior)
- [Configuration](#configuration)
  - [ValkeyOptions](#valkeyoptions)
- [Protocol Types](#protocol-types)
  - [RespValue](#respvalue)

## Connection Management

### ValkeyConnection

Main connection class for single database connections.

```csharp
public class ValkeyConnection : IAsyncDisposable
```

#### Methods

##### ConnectAsync
Creates and opens a connection to a Valkey server.

```csharp
public static async ValueTask<ValkeyConnection> ConnectAsync(
    EndPoint endpoint,
    ValkeyOptions? options = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `endpoint`: The server endpoint (IPEndPoint or DnsEndPoint)
- `options`: Optional connection configuration
- `cancellationToken`: Cancellation token

**Returns:** Connected ValkeyConnection instance

**Example:**
```csharp
var endpoint = new DnsEndPoint("localhost", 6379);
await using var connection = await ValkeyConnection.ConnectAsync(endpoint);
```

##### GetDatabase
Gets a database instance for executing commands.

```csharp
public ValkeyDatabase GetDatabase(int db = 0)
```

**Parameters:**
- `db`: Database number (0-15)

**Returns:** ValkeyDatabase instance

**Example:**
```csharp
var db = connection.GetDatabase(0);
```

### ValkeyMultiplexer

Connection multiplexer for sharing connections across multiple databases.

```csharp
public sealed class ValkeyMultiplexer : IAsyncDisposable
```

#### Methods

##### ConnectAsync
Creates a multiplexer from a connection string.

```csharp
public static async ValueTask<ValkeyMultiplexer> ConnectAsync(
    string configuration,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `configuration`: Connection string (e.g., "localhost:6379")
- `cancellationToken`: Cancellation token

**Returns:** Connected ValkeyMultiplexer instance

**Example:**
```csharp
await using var multiplexer = await ValkeyMultiplexer.ConnectAsync("localhost:6379");
var db0 = multiplexer.GetDatabase(0);
var db1 = multiplexer.GetDatabase(1);
```

##### GetDatabase
Gets a database instance (cached per database number).

```csharp
public IValkeyDatabase GetDatabase(int db = 0)
```

##### PingAsync
Tests the connection.

```csharp
public async ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
```

## Database Operations

### IValkeyDatabase

Main interface for executing Valkey commands.

```csharp
public interface IValkeyDatabase
```

## String Commands

### StringGetAsync
Gets the value of a key.

```csharp
ValueTask<string?> StringGetAsync(
    string key,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
var value = await db.StringGetAsync("mykey");
```

### StringSetAsync
Sets the value of a key.

```csharp
ValueTask<bool> StringSetAsync(
    string key,
    string value,
    TimeSpan? expiry = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `key`: The key to set
- `value`: The value to set
- `expiry`: Optional expiration time
- `cancellationToken`: Cancellation token

**Example:**
```csharp
await db.StringSetAsync("mykey", "myvalue");
await db.StringSetAsync("session:123", "data", TimeSpan.FromMinutes(30));
```

### StringIncrementAsync
Increments the numeric value of a key.

```csharp
ValueTask<long> StringIncrementAsync(
    string key,
    long value = 1,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
var newValue = await db.StringIncrementAsync("counter");
var newValue2 = await db.StringIncrementAsync("counter", 10);
```

### StringDecrementAsync
Decrements the numeric value of a key.

```csharp
ValueTask<long> StringDecrementAsync(
    string key,
    long value = 1,
    CancellationToken cancellationToken = default)
```

### StringAppendAsync
Appends a value to a key.

```csharp
ValueTask<long> StringAppendAsync(
    string key,
    string value,
    CancellationToken cancellationToken = default)
```

### StringLengthAsync
Gets the length of the string value of a key.

```csharp
ValueTask<long> StringLengthAsync(
    string key,
    CancellationToken cancellationToken = default)
```

## Hash Commands

### HashSetAsync (Single Field)
Sets a field in a hash.

```csharp
ValueTask<bool> HashSetAsync(
    string key,
    string field,
    string value,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
await db.HashSetAsync("user:1", "name", "Alice");
```

### HashSetAsync (Multiple Fields)
Sets multiple fields in a hash.

```csharp
ValueTask HashSetAsync(
    string key,
    Dictionary<string, string> fieldValues,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
await db.HashSetAsync("user:1", new Dictionary<string, string>
{
    { "name", "Bob" },
    { "email", "bob@example.com" },
    { "age", "30" }
});
```

### HashGetAsync
Gets the value of a hash field.

```csharp
ValueTask<string?> HashGetAsync(
    string key,
    string field,
    CancellationToken cancellationToken = default)
```

### HashGetAllAsync
Gets all fields and values in a hash.

```csharp
ValueTask<Dictionary<string, string>> HashGetAllAsync(
    string key,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
var user = await db.HashGetAllAsync("user:1");
Console.WriteLine($"Name: {user["name"]}, Email: {user["email"]}");
```

### HashDeleteAsync
Deletes one or more hash fields.

```csharp
ValueTask<long> HashDeleteAsync(
    string key,
    string[] fields,
    CancellationToken cancellationToken = default)
```

### HashExistsAsync
Checks if a hash field exists.

```csharp
ValueTask<bool> HashExistsAsync(
    string key,
    string field,
    CancellationToken cancellationToken = default)
```

### HashLengthAsync
Gets the number of fields in a hash.

```csharp
ValueTask<long> HashLengthAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### HashKeysAsync
Gets all field names in a hash.

```csharp
ValueTask<string[]> HashKeysAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### HashValuesAsync
Gets all values in a hash.

```csharp
ValueTask<string[]> HashValuesAsync(
    string key,
    CancellationToken cancellationToken = default)
```

## List Commands

### ListLeftPushAsync
Prepends a value to a list.

```csharp
ValueTask<long> ListLeftPushAsync(
    string key,
    string value,
    CancellationToken cancellationToken = default)
```

### ListRightPushAsync
Appends a value to a list.

```csharp
ValueTask<long> ListRightPushAsync(
    string key,
    string value,
    CancellationToken cancellationToken = default)
```

### ListLeftPopAsync
Removes and returns the first element of a list.

```csharp
ValueTask<string?> ListLeftPopAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### ListRightPopAsync
Removes and returns the last element of a list.

```csharp
ValueTask<string?> ListRightPopAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### ListLengthAsync
Gets the length of a list.

```csharp
ValueTask<long> ListLengthAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### ListRangeAsync
Gets a range of elements from a list.

```csharp
ValueTask<string[]> ListRangeAsync(
    string key,
    long start,
    long stop,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
// Get all elements
var all = await db.ListRangeAsync("mylist", 0, -1);

// Get first 10
var first10 = await db.ListRangeAsync("mylist", 0, 9);
```

### ListLeftPushAsync (Multiple Values)
Prepends multiple values to a list.

```csharp
ValueTask<long> ListLeftPushAsync(
    string key,
    string[] values,
    CancellationToken cancellationToken = default)
```

**Returns:** The length of the list after the push operation

**Example:**
```csharp
await db.ListLeftPushAsync("queue", new[] { "item3", "item2", "item1" });
// List order will be: item1, item2, item3, <existing items>
```

### ListRightPushAsync (Multiple Values)
Appends multiple values to a list.

```csharp
ValueTask<long> ListRightPushAsync(
    string key,
    string[] values,
    CancellationToken cancellationToken = default)
```

**Returns:** The length of the list after the push operation

**Example:**
```csharp
await db.ListRightPushAsync("log", new[] { "entry1", "entry2", "entry3" });
```

### ListIndexAsync
Gets the element at the specified index in a list.

```csharp
ValueTask<string?> ListIndexAsync(
    string key,
    long index,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `index`: Zero-based index (use negative values to count from the end: -1 is last element)

**Returns:** The element at index, or null if index is out of range

**Example:**
```csharp
var first = await db.ListIndexAsync("mylist", 0);   // First element
var last = await db.ListIndexAsync("mylist", -1);   // Last element
var third = await db.ListIndexAsync("mylist", 2);   // Third element
```

### ListSetAsync
Sets the value of an element in a list by its index.

```csharp
ValueTask ListSetAsync(
    string key,
    long index,
    string value,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `index`: Zero-based index (negative values count from the end)
- `value`: New value to set

**Example:**
```csharp
await db.ListSetAsync("mylist", 0, "updated_first");  // Update first element
await db.ListSetAsync("mylist", -1, "updated_last");  // Update last element
```

### ListRemoveAsync
Removes elements from a list matching a value.

```csharp
ValueTask<long> ListRemoveAsync(
    string key,
    long count,
    string value,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `count`: 
  - `count > 0`: Remove elements from head to tail
  - `count < 0`: Remove elements from tail to head
  - `count = 0`: Remove all elements matching value

**Returns:** Number of elements removed

**Example:**
```csharp
// Remove first 2 occurrences of "x" from head
await db.ListRemoveAsync("mylist", 2, "x");

// Remove all occurrences of "y"
await db.ListRemoveAsync("mylist", 0, "y");

// Remove last occurrence of "z"
await db.ListRemoveAsync("mylist", -1, "z");
```

### ListTrimAsync
Trims a list to the specified range.

```csharp
ValueTask ListTrimAsync(
    string key,
    long start,
    long stop,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `start`: Start index (inclusive)
- `stop`: Stop index (inclusive, use -1 for end of list)

**Example:**
```csharp
// Keep only first 100 elements
await db.ListTrimAsync("mylist", 0, 99);

// Keep only last 50 elements
await db.ListTrimAsync("mylist", -50, -1);
```

### ListBlockingLeftPopAsync
Removes and returns the first element of a list, blocking until an element is available or timeout expires.

```csharp
ValueTask<string?> ListBlockingLeftPopAsync(
    string key,
    TimeSpan timeout,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `timeout`: Maximum time to wait for an element (use TimeSpan.Zero for infinite wait)

**Returns:** The first element, or null if timeout expired

**Example:**
```csharp
// Wait up to 5 seconds for an element
var item = await db.ListBlockingLeftPopAsync("queue", TimeSpan.FromSeconds(5));
if (item != null)
{
    await ProcessItem(item);
}

// Wait indefinitely
var nextItem = await db.ListBlockingLeftPopAsync("queue", TimeSpan.Zero);
```

### ListBlockingRightPopAsync
Removes and returns the last element of a list, blocking until an element is available or timeout expires.

```csharp
ValueTask<string?> ListBlockingRightPopAsync(
    string key,
    TimeSpan timeout,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `timeout`: Maximum time to wait for an element (use TimeSpan.Zero for infinite wait)

**Returns:** The last element, or null if timeout expired

**Example:**
```csharp
// Wait up to 10 seconds for an element
var item = await db.ListBlockingRightPopAsync("stack", TimeSpan.FromSeconds(10));
```

## Set Commands

### SetAddAsync
Adds a member to a set.

```csharp
ValueTask<long> SetAddAsync(
    string key,
    string member,
    CancellationToken cancellationToken = default)
```

### SetRemoveAsync
Removes a member from a set.

```csharp
ValueTask<long> SetRemoveAsync(
    string key,
    string member,
    CancellationToken cancellationToken = default)
```

### SetContainsAsync
Checks if a member is in a set.

```csharp
ValueTask<bool> SetContainsAsync(
    string key,
    string member,
    CancellationToken cancellationToken = default)
```

### SetMembersAsync
Gets all members of a set.

```csharp
ValueTask<string[]> SetMembersAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### SetLengthAsync
Gets the number of members in a set.

```csharp
ValueTask<long> SetLengthAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### SetAddAsync (Multiple Members)
Adds multiple members to a set.

```csharp
ValueTask<long> SetAddAsync(
    string key,
    string[] members,
    CancellationToken cancellationToken = default)
```

**Returns:** Number of members that were added (excludes existing members)

**Example:**
```csharp
var added = await db.SetAddAsync("tags", new[] { "csharp", "redis", "valkey" });
Console.WriteLine($"Added {added} new tags");
```

### SetPopAsync
Removes and returns a random member from a set.

```csharp
ValueTask<string?> SetPopAsync(
    string key,
    CancellationToken cancellationToken = default)
```

**Returns:** A random member, or null if the set is empty

**Example:**
```csharp
var randomItem = await db.SetPopAsync("lottery_entries");
```

### SetPopAsync (Multiple Members)
Removes and returns multiple random members from a set.

```csharp
ValueTask<string[]> SetPopAsync(
    string key,
    long count,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `count`: Number of random members to pop

**Returns:** Array of random members (may be fewer than count if set has fewer elements)

**Example:**
```csharp
// Draw 3 random lottery winners
var winners = await db.SetPopAsync("lottery_entries", 3);
```

### SetIntersectAsync
Returns the intersection of multiple sets.

```csharp
ValueTask<string[]> SetIntersectAsync(
    string[] keys,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `keys`: Array of set keys to intersect

**Returns:** Members that exist in all specified sets

**Example:**
```csharp
// Find users who have all three tags
var users = await db.SetIntersectAsync(new[] { "tag:csharp", "tag:redis", "tag:expert" });
```

### SetUnionAsync
Returns the union of multiple sets.

```csharp
ValueTask<string[]> SetUnionAsync(
    string[] keys,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `keys`: Array of set keys to union

**Returns:** All unique members across all specified sets

**Example:**
```csharp
// Find all users with any of these tags
var allUsers = await db.SetUnionAsync(new[] { "tag:csharp", "tag:fsharp", "tag:dotnet" });
```

### SetDifferenceAsync
Returns the difference between the first set and all successive sets.

```csharp
ValueTask<string[]> SetDifferenceAsync(
    string[] keys,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `keys`: Array of set keys (first set minus all others)

**Returns:** Members in the first set that don't exist in any other set

**Example:**
```csharp
// Find users with C# tag but NOT beginner or intermediate tags
var experts = await db.SetDifferenceAsync(new[] { "tag:csharp", "tag:beginner", "tag:intermediate" });
```

## Sorted Set Commands

### SortedSetAddAsync
Adds a member with a score to a sorted set.

```csharp
ValueTask<long> SortedSetAddAsync(
    string key,
    string member,
    double score,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
await db.SortedSetAddAsync("leaderboard", "player1", 100);
await db.SortedSetAddAsync("leaderboard", "player2", 200);
```

### SortedSetRemoveAsync
Removes a member from a sorted set.

```csharp
ValueTask<long> SortedSetRemoveAsync(
    string key,
    string member,
    CancellationToken cancellationToken = default)
```

### SortedSetScoreAsync
Gets the score of a member.

```csharp
ValueTask<double?> SortedSetScoreAsync(
    string key,
    string member,
    CancellationToken cancellationToken = default)
```

### SortedSetRangeByRankAsync
Gets members by rank (index).

```csharp
ValueTask<string[]> SortedSetRangeByRankAsync(
    string key,
    long start,
    long stop,
    bool desc = false,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
// Top 10 players
var top10 = await db.SortedSetRangeByRankAsync("leaderboard", 0, 9, desc: true);
```

### SortedSetRangeByRankWithScoresAsync
Gets members with scores by rank.

```csharp
ValueTask<(string member, double score)[]> SortedSetRangeByRankWithScoresAsync(
    string key,
    long start,
    long stop,
    bool desc = false,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
var top = await db.SortedSetRangeByRankWithScoresAsync("leaderboard", 0, 9, desc: true);
foreach (var (member, score) in top)
{
    Console.WriteLine($"{member}: {score}");
}
```

### SortedSetLengthAsync
Gets the number of members in a sorted set.

```csharp
ValueTask<long> SortedSetLengthAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### SortedSetAddAsync (Multiple Members)
Adds multiple members with scores to a sorted set.

```csharp
ValueTask<long> SortedSetAddAsync(
    string key,
    (string member, double score)[] items,
    CancellationToken cancellationToken = default)
```

**Returns:** Number of members that were added (excludes members whose scores were updated)

**Example:**
```csharp
var leaderboard = new[]
{
    ("player1", 1000.0),
    ("player2", 850.0),
    ("player3", 920.0)
};
await db.SortedSetAddAsync("leaderboard", leaderboard);
```

### SortedSetRankAsync
Gets the rank (index) of a member in a sorted set.

```csharp
ValueTask<long?> SortedSetRankAsync(
    string key,
    string member,
    CancellationToken cancellationToken = default)
```

**Returns:** The rank (0-based index) of the member, or null if member doesn't exist

**Example:**
```csharp
var rank = await db.SortedSetRankAsync("leaderboard", "player1");
if (rank.HasValue)
{
    Console.WriteLine($"Player rank: #{rank.Value + 1}");  // +1 for 1-based display
}
```

### SortedSetRangeByScoreAsync
Gets members within a score range.

```csharp
ValueTask<string[]> SortedSetRangeByScoreAsync(
    string key,
    double min,
    double max,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `min`: Minimum score (inclusive)
- `max`: Maximum score (inclusive)

**Returns:** Members with scores in the specified range

**Example:**
```csharp
// Get players with scores between 500 and 1000
var midTierPlayers = await db.SortedSetRangeByScoreAsync("leaderboard", 500, 1000);
```

### SortedSetIncrementAsync
Increments the score of a member in a sorted set.

```csharp
ValueTask<double> SortedSetIncrementAsync(
    string key,
    string member,
    double value,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `value`: Amount to increment (use negative value to decrement)

**Returns:** The new score after increment

**Example:**
```csharp
// Award 50 points
var newScore = await db.SortedSetIncrementAsync("leaderboard", "player1", 50);
Console.WriteLine($"New score: {newScore}");

// Deduct 25 points
var penaltyScore = await db.SortedSetIncrementAsync("leaderboard", "player2", -25);
```

## Key Commands

### KeyDeleteAsync
Deletes a key.

```csharp
ValueTask<long> KeyDeleteAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### KeyExistsAsync
Checks if a key exists.

```csharp
ValueTask<bool> KeyExistsAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### KeyExpireAsync
Sets a timeout on a key.

```csharp
ValueTask<bool> KeyExpireAsync(
    string key,
    TimeSpan expiry,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
await db.KeyExpireAsync("session:123", TimeSpan.FromMinutes(30));
```

### KeyTimeToLiveAsync
Gets the remaining time to live of a key.

```csharp
ValueTask<TimeSpan?> KeyTimeToLiveAsync(
    string key,
    CancellationToken cancellationToken = default)
```

## Transactions

### ValkeyTransaction

Represents a MULTI/EXEC transaction.

```csharp
public class ValkeyTransaction
```

#### Methods

##### CreateTransaction
Creates a new transaction.

```csharp
ValkeyTransaction CreateTransaction()
```

**Example:**
```csharp
var txn = db.CreateTransaction();
```

##### StringSet, HashSet, etc.
Queue commands in the transaction (same names as database methods, but without Async suffix).

**Example:**
```csharp
txn.StringSet("key1", "value1");
txn.StringIncrement("counter");
txn.HashSet("user:1", "name", "Alice");
```

##### ExecuteAsync
Executes the transaction.

```csharp
ValueTask<RespValue[]> ExecuteAsync(CancellationToken cancellationToken = default)
```

**Returns:** Array of command results

**Example:**
```csharp
var txn = db.CreateTransaction();
txn.StringSet("counter", "0");
txn.StringIncrement("counter");
txn.StringIncrement("counter");
var results = await txn.ExecuteAsync();

// results[0] = OK (from SET)
// results[1] = 1 (from first INCR)
// results[2] = 2 (from second INCR)
var finalValue = results[2].AsInteger();
```

## Lua Scripting

### ScriptEvaluateAsync
Executes a Lua script.

```csharp
ValueTask<object?> ScriptEvaluateAsync(
    string script,
    string[]? keys = null,
    string[]? args = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `script`: The Lua script to execute
- `keys`: Array of key names (accessible as KEYS in Lua)
- `args`: Array of arguments (accessible as ARGV in Lua)

**Example:**
```csharp
var script = @"
    local current = redis.call('GET', KEYS[1])
    if not current or tonumber(current) < tonumber(ARGV[1]) then
        redis.call('SET', KEYS[1], ARGV[1])
        return 1
    end
    return 0
";

var result = await db.ScriptEvaluateAsync(
    script,
    keys: new[] { "max_value" },
    args: new[] { "100" }
);
```

### ScriptLoadAsync
Loads a script into the server cache.

```csharp
ValueTask<string> ScriptLoadAsync(
    string script,
    CancellationToken cancellationToken = default)
```

**Returns:** SHA1 hash of the script

**Example:**
```csharp
var sha1 = await db.ScriptLoadAsync(script);
```

### ScriptEvaluateShaAsync
Executes a cached script by SHA1 hash.

```csharp
ValueTask<object?> ScriptEvaluateShaAsync(
    string sha1,
    string[]? keys = null,
    string[]? args = null,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
var result = await db.ScriptEvaluateShaAsync(
    sha1,
    keys: new[] { "key1" },
    args: new[] { "arg1" }
);
```

### ScriptExistsAsync
Checks if scripts exist in the cache.

```csharp
ValueTask<bool[]> ScriptExistsAsync(
    string[] sha1Hashes,
    CancellationToken cancellationToken = default)
```

### ScriptFlushAsync
Removes all scripts from the cache.

```csharp
ValueTask ScriptFlushAsync(CancellationToken cancellationToken = default)
```

## Streams

### StreamEntry

Represents a stream entry.

```csharp
public readonly struct StreamEntry
{
    public string Id { get; init; }
    public Dictionary<string, string> Fields { get; init; }
}
```

### StreamAddAsync
Adds an entry to a stream.

```csharp
ValueTask<string> StreamAddAsync(
    string key,
    Dictionary<string, string> fieldValues,
    string id = "*",
    long? maxLength = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `key`: Stream key
- `fieldValues`: Field-value pairs
- `id`: Entry ID ("*" for auto-generated)
- `maxLength`: Optional max stream length (for trimming)

**Returns:** The entry ID

**Example:**
```csharp
var id = await db.StreamAddAsync("events", new Dictionary<string, string>
{
    { "event_type", "user_login" },
    { "user_id", "123" },
    { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() }
});
```

### StreamReadAsync
Reads entries from a stream.

```csharp
ValueTask<StreamEntry[]> StreamReadAsync(
    string key,
    string startId = "0",
    long? count = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `key`: Stream key
- `startId`: Starting ID ("0" for all, ">" for new)
- `count`: Optional max entries to return

**Example:**
```csharp
// Read all entries
var entries = await db.StreamReadAsync("events", "0");

// Read up to 10 entries
var entries = await db.StreamReadAsync("events", "0", count: 10);
```

### StreamRangeAsync
Reads a range of entries.

```csharp
ValueTask<StreamEntry[]> StreamRangeAsync(
    string key,
    string startId,
    string endId,
    long? count = null,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
// Read all entries
var entries = await db.StreamRangeAsync("events", "-", "+");
```

### StreamLengthAsync
Gets the length of a stream.

```csharp
ValueTask<long> StreamLengthAsync(
    string key,
    CancellationToken cancellationToken = default)
```

### StreamDeleteAsync
Deletes entries from a stream.

```csharp
ValueTask<long> StreamDeleteAsync(
    string key,
    string[] ids,
    CancellationToken cancellationToken = default)
```

### StreamTrimAsync
Trims a stream to a maximum length.

```csharp
ValueTask<long> StreamTrimAsync(
    string key,
    long maxLength,
    CancellationToken cancellationToken = default)
```

**Returns:** Number of entries deleted

### StreamGroupCreateAsync
Creates a consumer group.

```csharp
ValueTask StreamGroupCreateAsync(
    string key,
    string groupName,
    string startId = "$",
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `key`: Stream key
- `groupName`: Consumer group name
- `startId`: Starting position ("0" for all, "$" for new messages)

**Example:**
```csharp
await db.StreamGroupCreateAsync("events", "processors", "$");
```

### StreamReadGroupAsync
Reads entries as part of a consumer group.

```csharp
ValueTask<StreamEntry[]> StreamReadGroupAsync(
    string key,
    string groupName,
    string consumerName,
    string startId = ">",
    long? count = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `key`: Stream key
- `groupName`: Consumer group name
- `consumerName`: Consumer name
- `startId`: Starting ID (">" for new messages)
- `count`: Optional max entries to return

**Example:**
```csharp
var messages = await db.StreamReadGroupAsync(
    "events",
    "processors",
    "worker1",
    ">",
    count: 10
);

foreach (var msg in messages)
{
    // Process message
    await ProcessMessage(msg);
    
    // Acknowledge
    await db.StreamAckAsync("events", "processors", new[] { msg.Id });
}
```

### StreamAckAsync
Acknowledges processed messages.

```csharp
ValueTask<long> StreamAckAsync(
    string key,
    string groupName,
    string[] ids,
    CancellationToken cancellationToken = default)
```

**Returns:** Number of messages acknowledged

### StreamGroupDestroyAsync
Destroys a consumer group.

```csharp
ValueTask StreamGroupDestroyAsync(
    string key,
    string groupName,
    CancellationToken cancellationToken = default)
```

## Pub/Sub

Valkey.NET provides a complete Pub/Sub implementation with support for channel subscriptions, pattern subscriptions, and message streaming using `IAsyncEnumerable<T>`.

### PublishAsync
Publishes a message to a channel.

```csharp
ValueTask<long> PublishAsync(
    string channel,
    string message,
    CancellationToken cancellationToken = default)
```

**Returns:** Number of subscribers that received the message

**Example:**
```csharp
var subscribers = await db.PublishAsync("notifications", "Hello, World!");
Console.WriteLine($"Message delivered to {subscribers} subscribers");
```

### ValkeySubscriber

The `ValkeySubscriber` class provides dedicated Pub/Sub functionality with a separate connection (required by Redis/Valkey protocol).

#### Creating a Subscriber

```csharp
public static async ValueTask<ValkeySubscriber> CreateAsync(
    ValkeyOptions options,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
var options = new ValkeyOptions
{
    Endpoints = { new DnsEndPoint("localhost", 6379) }
};

await using var subscriber = await ValkeySubscriber.CreateAsync(options);
```

#### SubscribeAsync

Subscribes to one or more channels and returns an async enumerable stream of messages.

```csharp
IAsyncEnumerable<PubSubMessage> SubscribeAsync(
    params string[] channels)
```

**Parameters:**
- `channels`: One or more channel names to subscribe to

**Returns:** Async enumerable stream of `PubSubMessage` objects

**Example:**
```csharp
await using var subscriber = await ValkeySubscriber.CreateAsync(options);

// Subscribe to multiple channels
await foreach (var message in subscriber.SubscribeAsync("news", "updates"))
{
    Console.WriteLine($"[{message.Channel}] {message.Message}");
    
    if (message.Message == "STOP")
        break;
}
```

#### PSubscribeAsync

Subscribes to channel patterns using glob-style pattern matching.

```csharp
IAsyncEnumerable<PubSubMessage> PSubscribeAsync(
    params string[] patterns)
```

**Parameters:**
- `patterns`: One or more glob-style patterns (e.g., `news.*`, `user:*:notifications`)

**Returns:** Async enumerable stream of `PubSubMessage` objects

**Example:**
```csharp
// Subscribe to all channels matching pattern
await foreach (var message in subscriber.PSubscribeAsync("news.*", "alerts.*"))
{
    Console.WriteLine($"[{message.Channel}] (pattern: {message.Pattern}) {message.Message}");
}
```

#### UnsubscribeAsync

Unsubscribes from specific channels or all channels.

```csharp
ValueTask UnsubscribeAsync(params string[] channels)
```

**Parameters:**
- `channels`: Channels to unsubscribe from, or empty to unsubscribe from all

**Example:**
```csharp
// Unsubscribe from specific channels
await subscriber.UnsubscribeAsync("news", "updates");

// Unsubscribe from all channels
await subscriber.UnsubscribeAsync();
```

#### PUnsubscribeAsync

Unsubscribes from pattern subscriptions.

```csharp
ValueTask PUnsubscribeAsync(params string[] patterns)
```

**Parameters:**
- `patterns`: Patterns to unsubscribe from, or empty to unsubscribe from all patterns

**Example:**
```csharp
// Unsubscribe from specific patterns
await subscriber.PUnsubscribeAsync("news.*");

// Unsubscribe from all patterns
await subscriber.PUnsubscribeAsync();
```

### PubSubMessage

Represents a message received from a Pub/Sub subscription.

```csharp
public class PubSubMessage
{
    public string Channel { get; set; }
    public string? Pattern { get; set; }
    public string Message { get; set; }
}
```

**Properties:**
- `Channel`: The channel the message was published to
- `Pattern`: The pattern that matched (only for pattern subscriptions, otherwise null)
- `Message`: The message content

### Pub/Sub Patterns

**Single Channel Subscription:**
```csharp
await using var subscriber = await ValkeySubscriber.CreateAsync(options);

await foreach (var msg in subscriber.SubscribeAsync("notifications"))
{
    await ProcessNotification(msg.Message);
}
```

**Multiple Channel Subscription:**
```csharp
var channels = new[] { "channel1", "channel2", "channel3" };
await foreach (var msg in subscriber.SubscribeAsync(channels))
{
    Console.WriteLine($"Received on {msg.Channel}: {msg.Message}");
}
```

**Pattern Subscription with Wildcard:**
```csharp
// Subscribe to all user notification channels
await foreach (var msg in subscriber.PSubscribeAsync("user:*:notifications"))
{
    // msg.Channel might be "user:1000:notifications", "user:2000:notifications", etc.
    // msg.Pattern will be "user:*:notifications"
    var userId = msg.Channel.Split(':')[1];
    Console.WriteLine($"Notification for user {userId}: {msg.Message}");
}
```

**Combining Regular and Pattern Subscriptions:**
```csharp
var regularSub = subscriber.SubscribeAsync("important");
var patternSub = subscriber.PSubscribeAsync("log.*");

// Process both streams concurrently
var tasks = new[]
{
    Task.Run(async () =>
    {
        await foreach (var msg in regularSub)
            Console.WriteLine($"[IMPORTANT] {msg.Message}");
    }),
    Task.Run(async () =>
    {
        await foreach (var msg in patternSub)
            Console.WriteLine($"[LOG:{msg.Channel}] {msg.Message}");
    })
};

await Task.WhenAll(tasks);
```

**Cancellation Support:**
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

try
{
    await foreach (var msg in subscriber.SubscribeAsync("events")
        .WithCancellation(cts.Token))
    {
        await ProcessEvent(msg.Message);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Subscription cancelled after timeout");
}
```

**Publishing Messages:**
```csharp
// Publisher side (using ValkeyDatabase)
await using var connection = await ValkeyConnection.ConnectAsync(options);
var db = connection.GetDatabase();

// Publish to channel subscribers
var count = await db.PublishAsync("notifications", "System maintenance in 5 minutes");
Console.WriteLine($"Message sent to {count} subscribers");

// Publish to pattern subscribers
await db.PublishAsync("user:1000:notifications", "You have a new message");
```

### Pub/Sub Best Practices

1. **Dedicated Connection**: Always use `ValkeySubscriber` for subscriptions (not `ValkeyDatabase`)
2. **Resource Cleanup**: Always dispose subscribers with `await using` or `DisposeAsync()`
3. **Cancellation**: Use `CancellationToken` for graceful shutdown
4. **Pattern Performance**: Minimize pattern subscriptions when possible (exact channels are faster)
5. **Message Ordering**: Messages within a channel are ordered, but no ordering guarantee across channels
6. **Fire-and-Forget**: Pub/Sub has no message persistence - subscribers only receive messages published while connected
7. **Backpressure**: If processing is slow, use `Channel<T>` or similar to buffer messages:

```csharp
var messageChannel = Channel.CreateBounded<string>(100);

// Consumer
_ = Task.Run(async () =>
{
    await foreach (var msg in subscriber.SubscribeAsync("events"))
    {
        await messageChannel.Writer.WriteAsync(msg.Message);
    }
});

// Processor (can be slower without blocking subscriber)
await foreach (var message in messageChannel.Reader.ReadAllAsync())
{
    await SlowProcessing(message);
}
```

## Geospatial Commands

Geospatial commands allow you to store and query geographic coordinates using Redis/Valkey's geospatial indexes.

### GeoAddAsync
Adds one or more geospatial items to a sorted set representing a geospatial index.

```csharp
ValueTask<long> GeoAddAsync(
    string key,
    double longitude,
    double latitude,
    string member,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `key`: The geospatial index key
- `longitude`: Longitude coordinate (-180 to 180)
- `latitude`: Latitude coordinate (-85.05112878 to 85.05112878)
- `member`: The member name to associate with this location

**Returns:** Number of elements added (0 if the element already existed and was updated)

**Example:**
```csharp
// Add store locations
await db.GeoAddAsync("stores", -122.4194, 37.7749, "sf_store");
await db.GeoAddAsync("stores", -122.2728, 37.8044, "oakland_store");
await db.GeoAddAsync("stores", -118.2437, 34.0522, "la_store");
```

### GeoDistanceAsync
Returns the distance between two members in a geospatial index.

```csharp
ValueTask<double?> GeoDistanceAsync(
    string key,
    string member1,
    string member2,
    GeoUnit unit = GeoUnit.Meters,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `unit`: Distance unit (Meters, Kilometers, Miles, Feet)

**Returns:** Distance between members, or null if either member is missing

**Example:**
```csharp
using Valkey.Abstractions.Geospatial;

var distance = await db.GeoDistanceAsync("stores", "sf_store", "oakland_store", GeoUnit.Miles);
Console.WriteLine($"Distance: {distance:F2} miles"); // Distance: 12.45 miles
```

### GeoPositionAsync
Returns the longitude and latitude of one or more members.

```csharp
ValueTask<GeoPosition?[]> GeoPositionAsync(
    string key,
    string[] members,
    CancellationToken cancellationToken = default)
```

**Returns:** Array of positions (null for missing members)

**Example:**
```csharp
var positions = await db.GeoPositionAsync("stores", new[] { "sf_store", "la_store" });
foreach (var pos in positions)
{
    if (pos.HasValue)
        Console.WriteLine($"Location: {pos.Value.Longitude}, {pos.Value.Latitude}");
}
```

### GeoHashAsync
Returns geohash strings representing the positions of members.

```csharp
ValueTask<string?[]> GeoHashAsync(
    string key,
    string[] members,
    CancellationToken cancellationToken = default)
```

**Returns:** Array of geohash strings (null for missing members)

**Example:**
```csharp
var hashes = await db.GeoHashAsync("stores", new[] { "sf_store", "la_store" });
// Returns: ["9q8yyk8yuv0", "9q5ctr7h3v0"]
```

### GeoRadiusAsync
Searches for members within a radius from given coordinates.

```csharp
ValueTask<GeoRadiusResult[]> GeoRadiusAsync(
    string key,
    double longitude,
    double latitude,
    double radius,
    GeoUnit unit = GeoUnit.Meters,
    long? count = null,
    bool withDistance = false,
    bool withCoordinates = false,
    bool withHash = false,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `count`: Limit number of results
- `withDistance`: Include distance from center in results
- `withCoordinates`: Include coordinates in results
- `withHash`: Include geohash in results

**Returns:** Array of matching members with optional metadata

**Example:**
```csharp
// Find all stores within 20 miles of a location
var results = await db.GeoRadiusAsync(
    "stores",
    -122.4, 37.8,
    20,
    GeoUnit.Miles,
    withDistance: true,
    withCoordinates: true);

foreach (var result in results)
{
    Console.WriteLine($"{result.Member}: {result.Distance:F2} miles");
    if (result.Position.HasValue)
        Console.WriteLine($"  Coordinates: {result.Position.Value.Longitude}, {result.Position.Value.Latitude}");
}
```

### GeoRadiusByMemberAsync
Searches for members within a radius from an existing member.

```csharp
ValueTask<GeoRadiusResult[]> GeoRadiusByMemberAsync(
    string key,
    string member,
    double radius,
    GeoUnit unit = GeoUnit.Meters,
    long? count = null,
    bool withDistance = false,
    bool withCoordinates = false,
    bool withHash = false,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
// Find stores within 50 miles of sf_store
var nearby = await db.GeoRadiusByMemberAsync(
    "stores",
    "sf_store",
    50,
    GeoUnit.Miles,
    withDistance: true);

foreach (var store in nearby)
{
    Console.WriteLine($"{store.Member}: {store.Distance:F2} miles away");
}
```

### GeoSearchByPolygonAsync
Searches for members within a polygon-shaped area. **Valkey 9.0+ feature**.

```csharp
ValueTask<GeoRadiusResult[]> GeoSearchByPolygonAsync(
    string key,
    GeoPosition[] polygon,
    long? count = null,
    bool withDistance = false,
    bool withCoordinates = false,
    bool withHash = false,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `polygon`: Array of positions defining polygon vertices (should form a closed polygon)

**Returns:** Array of members within the polygon area

**Example:**
```csharp
// Define a polygon area (e.g., city boundaries)
var polygon = new[]
{
    new GeoPosition { Longitude = -122.5, Latitude = 37.7 },
    new GeoPosition { Longitude = -122.3, Latitude = 37.7 },
    new GeoPosition { Longitude = -122.3, Latitude = 37.9 },
    new GeoPosition { Longitude = -122.5, Latitude = 37.9 },
    new GeoPosition { Longitude = -122.5, Latitude = 37.7 } // Close the polygon
};

var storesInArea = await db.GeoSearchByPolygonAsync(
    "stores",
    polygon,
    withCoordinates: true);

Console.WriteLine($"Found {storesInArea.Length} stores in the defined area");
```

### Geospatial Type Definitions

#### GeoUnit Enum
```csharp
public enum GeoUnit
{
    Meters,      // m
    Kilometers,  // km
    Miles,       // mi
    Feet         // ft
}
```

#### GeoPosition Struct
```csharp
public struct GeoPosition
{
    public double Longitude { get; set; }  // -180 to 180
    public double Latitude { get; set; }   // -85.05112878 to 85.05112878
}
```

#### GeoRadiusResult Class
```csharp
public class GeoRadiusResult
{
    public string Member { get; set; }              // Member name
    public double? Distance { get; set; }           // Distance from center (if withDistance)
    public GeoPosition? Position { get; set; }      // Coordinates (if withCoordinates)
    public string? Hash { get; set; }               // Geohash (if withHash)
}
```

### Geospatial Best Practices

1. **Coordinate validation**: Ensure longitude is -180 to 180 and latitude is -85.05 to 85.05
2. **Use appropriate units**: Choose the unit that matches your application's needs
3. **Limit results**: Use `count` parameter for large datasets to prevent performance issues
4. **Polygon complexity**: Keep polygons simple for better performance with GeoSearchByPolygonAsync
5. **Indexing**: Geospatial indexes are implemented as sorted sets, so all sorted set commands apply

## Cluster Support

Valkey.NET provides comprehensive support for Valkey/Redis cluster deployments with automatic topology discovery, hash slot routing, and MOVED/ASK redirection handling.

### IValkeyCluster Interface

The cluster client interface for managing connections to a Valkey cluster.

```csharp
public interface IValkeyCluster : IAsyncDisposable
{
    IValkeyDatabase GetDatabase(int database = 0);
    ValueTask RefreshTopologyAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<ClusterNode> GetNodes();
    ClusterNode? GetNodeForKey(string key);
    ClusterNode? GetNodeForSlot(int slot);
    IReadOnlyDictionary<string, ValkeyConnection> GetActiveConnections();
}
```

### ValkeyCluster.ConnectAsync

Creates and connects to a Valkey cluster.

```csharp
public static async ValueTask<IValkeyCluster> ConnectAsync(
    ValkeyClusterOptions options,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `options`: Cluster configuration options
- `cancellationToken`: Optional cancellation token

**Returns:** Connected cluster client

**Example:**
```csharp
var options = new ValkeyClusterOptions
{
    SeedEndpoints =
    {
        new DnsEndPoint("cluster-node1", 6379),
        new DnsEndPoint("cluster-node2", 6379),
        new DnsEndPoint("cluster-node3", 6379)
    },
    Password = "cluster-secret"
};

await using var cluster = await ValkeyCluster.ConnectAsync(options);
var db = cluster.GetDatabase();
await db.StringSetAsync("key", "value");
```

### GetDatabase

Gets a database instance for executing commands.

```csharp
IValkeyDatabase GetDatabase(int database = 0)
```

**Parameters:**
- `database`: Database number (default: 0)

**Returns:** Database instance

**Note:** In cluster mode, only database 0 is typically available.

**Example:**
```csharp
var db = cluster.GetDatabase();
await db.StringSetAsync("user:1000", "Alice");
```

### RefreshTopologyAsync

Manually refreshes the cluster topology by querying CLUSTER NODES.

```csharp
ValueTask RefreshTopologyAsync(CancellationToken cancellationToken = default)
```

**Example:**
```csharp
// Force topology refresh after cluster reconfiguration
await cluster.RefreshTopologyAsync();
```

**Note:** Topology is automatically refreshed when MOVED/ASK redirects are received.

### GetNodes

Gets the current cluster topology.

```csharp
IReadOnlyList<ClusterNode> GetNodes()
```

**Returns:** List of cluster nodes with their metadata

**Example:**
```csharp
var nodes = cluster.GetNodes();
foreach (var node in nodes)
{
    Console.WriteLine($"Node {node.Id}: {node.EndPoint} - {node.Flags}");
    Console.WriteLine($"  Slots: {string.Join(", ", node.SlotRanges.Select(r => $"{r.Start}-{r.End}"))}");
}
```

### GetNodeForKey

Determines which cluster node handles a specific key.

```csharp
ClusterNode? GetNodeForKey(string key)
```

**Parameters:**
- `key`: The key to lookup

**Returns:** The cluster node responsible for the key, or null if topology is unknown

**Example:**
```csharp
var node = cluster.GetNodeForKey("user:1000");
if (node != null)
{
    Console.WriteLine($"Key 'user:1000' is on node {node.EndPoint}");
}
```

### GetNodeForSlot

Determines which cluster node handles a specific hash slot.

```csharp
ClusterNode? GetNodeForSlot(int slot)
```

**Parameters:**
- `slot`: Hash slot number (0-16383)

**Returns:** The cluster node responsible for the slot, or null if topology is unknown

**Example:**
```csharp
int slot = ClusterHashSlot.Calculate("user:1000");
var node = cluster.GetNodeForSlot(slot);
Console.WriteLine($"Slot {slot} is handled by {node?.EndPoint}");
```

### GetActiveConnections

Gets all currently active connections to cluster nodes.

```csharp
IReadOnlyDictionary<string, ValkeyConnection> GetActiveConnections()
```

**Returns:** Dictionary mapping node IDs to active connections

**Example:**
```csharp
var connections = cluster.GetActiveConnections();
Console.WriteLine($"Active connections: {connections.Count}");
foreach (var (nodeId, connection) in connections)
{
    Console.WriteLine($"  Node {nodeId}: Connected");
}
```

### ValkeyClusterOptions

Configuration options for cluster connections.

```csharp
public class ValkeyClusterOptions
{
    public List<EndPoint> SeedEndpoints { get; set; } = new();
    public string? Password { get; set; }
    public string? User { get; set; }
    public bool UseSsl { get; set; }
    public int ConnectTimeout { get; set; } = 5000;
    public bool PreferResp3 { get; set; } = true;
}
```

**Properties:**
- `SeedEndpoints`: Initial cluster nodes to connect to (only one needs to be reachable)
- `Password`: Optional password for authentication
- `User`: Optional username for ACL authentication
- `UseSsl`: Enable SSL/TLS for all connections
- `ConnectTimeout`: Connection timeout in milliseconds
- `PreferResp3`: Use RESP3 protocol when supported (default: true)

**Example:**
```csharp
var options = new ValkeyClusterOptions
{
    SeedEndpoints =
    {
        new DnsEndPoint("node1.cluster.example.com", 6379),
        new DnsEndPoint("node2.cluster.example.com", 6379)
    },
    Password = "secret",
    UseSsl = true,
    ConnectTimeout = 10000
};
```

### ClusterNode

Represents a node in the cluster topology.

```csharp
public class ClusterNode
{
    public string Id { get; set; }
    public EndPoint EndPoint { get; set; }
    public ClusterNodeFlags Flags { get; set; }
    public string? PrimaryId { get; set; }
    public List<(int Start, int End)> SlotRanges { get; set; }
}
```

**Properties:**
- `Id`: Unique node identifier (40-char hex string)
- `EndPoint`: Network endpoint (IP:port)
- `Flags`: Node role and status flags
- `PrimaryId`: Primary node ID (for replicas only)
- `SlotRanges`: Hash slot ranges assigned to this node

### ClusterNodeFlags

Flags indicating node role and status.

```csharp
[Flags]
public enum ClusterNodeFlags
{
    None = 0,
    Master = 1,
    Replica = 2,
    Myself = 4,
    PFail = 8,      // Possibly failing
    Fail = 16,      // Definitively failing
    Handshake = 32,
    NoAddr = 64,
    NoFlags = 128
}
```

### Cluster Behavior

**Hash Slot Routing:**
- Keys are automatically routed to the correct node based on CRC16 hash slot calculation
- Multi-key operations require all keys to be in the same hash slot
- Use hash tags `{...}` to ensure keys map to the same slot: `user:{1000}:profile`, `user:{1000}:settings`

**Automatic Redirection:**
- MOVED redirects trigger automatic topology refresh and retry
- ASK redirects are handled transparently with ASKING command
- No manual intervention needed for cluster reconfigurations

**Connection Management:**
- Connections are established lazily to nodes as needed
- Connection pooling per node for optimal performance
- Automatic reconnection on connection failures

**Multi-Key Operations:**
```csharp
// ❌ May fail if keys are on different nodes
await db.DeleteAsync(new[] { "user:1", "user:2" });

// ✅ Use hash tags to ensure same slot
await db.DeleteAsync(new[] { "user:{1}:profile", "user:{1}:settings" });

// ✅ Or use single-key operations
await db.DeleteAsync("user:1");
await db.DeleteAsync("user:2");
```

**Error Handling:**
```csharp
try
{
    await db.StringSetAsync("key", "value");
}
catch (ValkeyException ex) when (ex.Message.Contains("CLUSTERDOWN"))
{
    // Cluster is unavailable
    await Task.Delay(1000);
    await cluster.RefreshTopologyAsync();
}
```

**Best Practices:**
1. **Seed nodes**: Provide multiple seed endpoints for redundancy
2. **Hash tags**: Use hash tags for related keys that need multi-key operations
3. **Topology awareness**: Use GetNodeForKey() for diagnostic purposes
4. **Connection cleanup**: Always dispose cluster with `await using` or `DisposeAsync()`
5. **Monitoring**: Use GetActiveConnections() to monitor cluster connectivity

## Configuration

### ValkeyOptions

Configuration options for Valkey connections.

```csharp
public class ValkeyOptions
{
    public List<EndPoint> Endpoints { get; set; }
    public string? Password { get; set; }
    public string? User { get; set; }
    public bool UseSsl { get; set; }
    public int ConnectTimeout { get; set; } = 5000;
    public bool PreferResp3 { get; set; } = true;
}
```

**Properties:**
- `Endpoints`: List of server endpoints
- `Password`: Optional password for authentication
- `User`: Optional username for ACL authentication
- `UseSsl`: Enable SSL/TLS
- `ConnectTimeout`: Connection timeout in milliseconds
- `PreferResp3`: Use RESP3 protocol (default: true)

**Example:**
```csharp
var options = new ValkeyOptions
{
    Endpoints = { new DnsEndPoint("localhost", 6379) },
    Password = "secret",
    UseSsl = true,
    ConnectTimeout = 10000,
    PreferResp3 = true
};
```

## Protocol Types

### RespValue

Represents a RESP3 protocol value.

```csharp
public readonly struct RespValue
```

#### Methods

##### AsString
Converts to string.

```csharp
public string AsString()
```

##### AsInteger
Converts to long.

```csharp
public long AsInteger()
```

##### AsDouble
Converts to double.

```csharp
public double AsDouble()
```

##### AsBoolean
Converts to boolean.

```csharp
public bool AsBoolean()
```

##### TryGetArray
Tries to get as array.

```csharp
public bool TryGetArray(out RespValue[] array)
```

##### TryGetMap
Tries to get as map.

```csharp
public bool TryGetMap(out Dictionary<RespValue, RespValue> map)
```

##### IsNull
Checks if the value is null.

```csharp
public bool IsNull { get; }
```

**Example:**
```csharp
var results = await transaction.ExecuteAsync();
var stringResult = results[0].AsString();
var intResult = results[1].AsInteger();
var boolResult = results[2].AsBoolean();
```

## Error Handling

All async methods can throw:
- `ValkeyException`: General Valkey errors
- `TimeoutException`: Operation timeout
- `SocketException`: Network errors
- `ArgumentException`: Invalid arguments

**Example:**
```csharp
try
{
    await db.StringSetAsync("key", "value");
}
catch (ValkeyException ex)
{
    Console.WriteLine($"Valkey error: {ex.Message}");
}
catch (TimeoutException)
{
    Console.WriteLine("Operation timed out");
}
```

## Best Practices

1. **Use `await using` for connections**: Ensures proper cleanup
   ```csharp
   await using var connection = await ValkeyConnection.ConnectAsync(endpoint);
   ```

2. **Prefer ValkeyMultiplexer for multi-database scenarios**:
   ```csharp
   await using var multiplexer = await ValkeyMultiplexer.ConnectAsync("localhost:6379");
   ```

3. **Use transactions for atomic operations**:
   ```csharp
   var txn = db.CreateTransaction();
   txn.StringSet("key1", "value1");
   txn.StringIncrement("counter");
   await txn.ExecuteAsync();
   ```

4. **Cache Lua scripts with SCRIPT LOAD**:
   ```csharp
   var sha1 = await db.ScriptLoadAsync(script);
   // Later: use EVALSHA
   await db.ScriptEvaluateShaAsync(sha1, keys, args);
   ```

5. **Use consumer groups for reliable stream processing**:
   ```csharp
   await db.StreamGroupCreateAsync("events", "workers", "$");
   var messages = await db.StreamReadGroupAsync("events", "workers", "worker1", ">");
   // Process and acknowledge
   await db.StreamAckAsync("events", "workers", messageIds);
   ```

6. **Handle cancellation properly**:
   ```csharp
   var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
   await db.StringGetAsync("key", cts.Token);
   ```

## See Also

- [Getting Started Guide](GETTING_STARTED.md)
- [Migration Guide](MIGRATION_FROM_STACKEXCHANGE_REDIS.md)
- [Status & Roadmap](STATUS.md)
