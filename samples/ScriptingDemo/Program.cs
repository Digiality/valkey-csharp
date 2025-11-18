using System.Net;
using Valkey;
using Valkey.Configuration;

// ============================================================================
// Valkey.NET - Lua Scripting Examples
// ============================================================================
// This demo shows how to use Lua scripting for server-side operations
// ============================================================================

Console.WriteLine("Valkey.NET - Lua Scripting Demo");
Console.WriteLine("================================\n");

// Connect to Valkey
var endpoint = new IPEndPoint(IPAddress.Loopback, 6379);
await using var connection = await ValkeyConnection.ConnectAsync(endpoint, ValkeyOptions.Default);
var db = connection.GetDatabase();

Console.WriteLine("✅ Connected to Valkey\n");

// ============================================================================
// Example 1: Basic Script Execution
// ============================================================================
Console.WriteLine("Example 1: Basic Script Execution");
Console.WriteLine("----------------------------------");

var simpleScript = "return 'Hello from Lua!'";
var result = await db.ScriptEvaluateAsync(simpleScript);
Console.WriteLine($"Result: {result.AsString()}");
Console.WriteLine();

// ============================================================================
// Example 2: Script with Keys and Arguments
// ============================================================================
Console.WriteLine("Example 2: Script with Keys and Arguments");
Console.WriteLine("------------------------------------------");

var setScript = "return redis.call('SET', KEYS[1], ARGV[1])";
var setResult = await db.ScriptEvaluateAsync(
    setScript,
    keys: ["demo:greeting"],
    args: ["Hello, Valkey!"]);

Console.WriteLine($"SET result: {setResult.AsString()}");

var getScript = "return redis.call('GET', KEYS[1])";
var getResult = await db.ScriptEvaluateAsync(
    getScript,
    keys: ["demo:greeting"]);

Console.WriteLine($"GET result: {getResult.AsString()}");
Console.WriteLine();

// ============================================================================
// Example 3: Atomic Increment with Initialization
// ============================================================================
Console.WriteLine("Example 3: Atomic Increment");
Console.WriteLine("----------------------------");

var atomicIncrScript = @"
    local current = redis.call('GET', KEYS[1])
    if not current then
        current = 0
    end
    local new_val = tonumber(current) + tonumber(ARGV[1])
    redis.call('SET', KEYS[1], new_val)
    return new_val
";

var counter1 = await db.ScriptEvaluateAsync(atomicIncrScript, keys: ["demo:counter"], args: ["5"]);
var counter2 = await db.ScriptEvaluateAsync(atomicIncrScript, keys: ["demo:counter"], args: ["3"]);
var counter3 = await db.ScriptEvaluateAsync(atomicIncrScript, keys: ["demo:counter"], args: ["2"]);

Console.WriteLine($"After +5: {counter1.AsInteger()}");
Console.WriteLine($"After +3: {counter2.AsInteger()}");
Console.WriteLine($"After +2: {counter3.AsInteger()}");
Console.WriteLine();

// ============================================================================
// Example 4: Conditional Update (Set if Greater)
// ============================================================================
Console.WriteLine("Example 4: Conditional Update");
Console.WriteLine("------------------------------");

var maxValueScript = @"
    local current = redis.call('GET', KEYS[1])
    if not current or tonumber(current) < tonumber(ARGV[1]) then
        redis.call('SET', KEYS[1], ARGV[1])
        return 1
    end
    return 0
";

var updated1 = await db.ScriptEvaluateAsync(maxValueScript, keys: ["demo:max"], args: ["100"]);
var updated2 = await db.ScriptEvaluateAsync(maxValueScript, keys: ["demo:max"], args: ["50"]);  // Won't update
var updated3 = await db.ScriptEvaluateAsync(maxValueScript, keys: ["demo:max"], args: ["200"]); // Will update

Console.WriteLine($"Set to 100: {(updated1.AsInteger() == 1 ? "✅ Updated" : "❌ Not updated")}");
Console.WriteLine($"Set to 50:  {(updated2.AsInteger() == 1 ? "✅ Updated" : "❌ Not updated")}");
Console.WriteLine($"Set to 200: {(updated3.AsInteger() == 1 ? "✅ Updated" : "❌ Not updated")}");

var finalValue = await db.StringGetAsync("demo:max");
Console.WriteLine($"Final value: {finalValue}");
Console.WriteLine();

// ============================================================================
// Example 5: Script Caching (SCRIPT LOAD + EVALSHA)
// ============================================================================
Console.WriteLine("Example 5: Script Caching");
Console.WriteLine("-------------------------");

var cachedScript = "return 'This script is cached!'";

// Load the script
var sha1 = await db.ScriptLoadAsync(cachedScript);
Console.WriteLine($"Script loaded with SHA1: {sha1}");

// Check if it exists
var exists = await db.ScriptExistsAsync([sha1]);
Console.WriteLine($"Script exists: {exists[0]}");

// Execute using SHA1
var cachedResult = await db.ScriptEvaluateShaAsync(sha1);
Console.WriteLine($"Result: {cachedResult.AsString()}");
Console.WriteLine();

// ============================================================================
// Example 6: Multi-Key Operations
// ============================================================================
Console.WriteLine("Example 6: Multi-Key Operations");
Console.WriteLine("--------------------------------");

// Set up some data
await db.StringSetAsync("demo:value1", "10");
await db.StringSetAsync("demo:value2", "20");
await db.StringSetAsync("demo:value3", "30");

var sumScript = @"
    local sum = 0
    for i = 1, #KEYS do
        local val = redis.call('GET', KEYS[i])
        if val then
            sum = sum + tonumber(val)
        end
    end
    return sum
";

var sum = await db.ScriptEvaluateAsync(
    sumScript,
    keys: ["demo:value1", "demo:value2", "demo:value3"]);

Console.WriteLine($"Sum of values: {sum.AsInteger()}");
Console.WriteLine();

// ============================================================================
// Example 7: Returning Complex Data (Arrays)
// ============================================================================
Console.WriteLine("Example 7: Returning Arrays");
Console.WriteLine("---------------------------");

var arrayScript = @"
    return {
        'name', ARGV[1],
        'age', tonumber(ARGV[2]),
        'active', ARGV[3] == 'true'
    }
";

var arrayResult = await db.ScriptEvaluateAsync(
    arrayScript,
    args: ["Alice", "30", "true"]);

var array = arrayResult.AsArray();
Console.WriteLine("User data:");
for (int i = 0; i < array.Length; i += 2)
{
    Console.WriteLine($"  {array[i].AsString()}: {array[i + 1]}");
}
Console.WriteLine();

// ============================================================================
// Example 8: Rate Limiting with Lua
// ============================================================================
Console.WriteLine("Example 8: Rate Limiting");
Console.WriteLine("------------------------");

var rateLimitScript = @"
    local key = KEYS[1]
    local limit = tonumber(ARGV[1])
    local window = tonumber(ARGV[2])

    local current = redis.call('INCR', key)

    if current == 1 then
        redis.call('EXPIRE', key, window)
    end

    if current > limit then
        return 0  -- Rate limit exceeded
    else
        return 1  -- Request allowed
    end
";

// Simulate rate limiting (5 requests per 10 seconds)
var rateLimitKey = "demo:ratelimit:user123";
for (int i = 1; i <= 7; i++)
{
    var allowed = await db.ScriptEvaluateAsync(
        rateLimitScript,
        keys: [rateLimitKey],
        args: ["5", "10"]); // 5 requests per 10 seconds

    var status = allowed.AsInteger() == 1 ? "✅ Allowed" : "❌ Rate limited";
    Console.WriteLine($"Request {i}: {status}");
}
Console.WriteLine();

// ============================================================================
// Example 9: Script Cache Management
// ============================================================================
Console.WriteLine("Example 9: Script Cache Management");
Console.WriteLine("-----------------------------------");

// Load multiple scripts
var script1 = await db.ScriptLoadAsync("return 'Script 1'");
var script2 = await db.ScriptLoadAsync("return 'Script 2'");

Console.WriteLine($"Loaded script 1: {script1[..10]}...");
Console.WriteLine($"Loaded script 2: {script2[..10]}...");

// Check existence
var existenceCheck = await db.ScriptExistsAsync([script1, script2, "0000000000000000000000000000000000000000"]);
Console.WriteLine($"Script 1 exists: {existenceCheck[0]}");
Console.WriteLine($"Script 2 exists: {existenceCheck[1]}");
Console.WriteLine($"Fake script exists: {existenceCheck[2]}");

// Flush cache
await db.ScriptFlushAsync();
Console.WriteLine("✅ Script cache flushed");

var afterFlush = await db.ScriptExistsAsync([script1, script2]);
Console.WriteLine($"After flush - Script 1 exists: {afterFlush[0]}");
Console.WriteLine($"After flush - Script 2 exists: {afterFlush[1]}");
Console.WriteLine();

// ============================================================================
// Cleanup
// ============================================================================
Console.WriteLine("Cleaning up demo keys...");
await db.KeyDeleteAsync([
    "demo:greeting", "demo:counter", "demo:max",
    "demo:value1", "demo:value2", "demo:value3",
    rateLimitKey
]);

Console.WriteLine("\n✅ Lua Scripting Demo Complete!");
