using FluentAssertions;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for Lua scripting commands.
/// </summary>
public class ScriptingIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task ScriptEvaluate_SimpleScript_ReturnsCorrectValue()
    {
        // Arrange
        var script = "return 42";

        // Act
        var result = await Database!.ScriptEvaluateAsync(script);

        // Assert
        result.AsInteger().Should().Be(42);
    }

    [Fact]
    public async Task ScriptEvaluate_WithKeys_AccessesCorrectKeys()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "hello");

        var script = "return redis.call('GET', KEYS[1])";

        // Act
        var result = await Database!.ScriptEvaluateAsync(
            script,
            keys: [key]);

        // Assert
        result.AsString().Should().Be("hello");
    }

    [Fact]
    public async Task ScriptEvaluate_WithArgs_UsesArguments()
    {
        // Arrange
        var key = GetTestKey();
        var script = "return redis.call('SET', KEYS[1], ARGV[1])";

        // Act
        var result = await Database!.ScriptEvaluateAsync(
            script,
            keys: [key],
            args: ["test_value"]);

        // Assert
        result.AsString().Should().Be("OK");

        var value = await Database!.StringGetAsync(key);
        value.Should().Be("test_value");
    }

    [Fact]
    public async Task ScriptEvaluate_ReturnsArray_ParsedCorrectly()
    {
        // Arrange
        var script = "return {1, 2, 3, 'hello'}";

        // Act
        var result = await Database!.ScriptEvaluateAsync(script);

        // Assert
        var array = result.AsArray();
        array.Should().HaveCount(4);
        array[0].AsInteger().Should().Be(1);
        array[1].AsInteger().Should().Be(2);
        array[2].AsInteger().Should().Be(3);
        array[3].AsString().Should().Be("hello");
    }

    [Fact]
    public async Task ScriptLoad_LoadsScript_ReturnsSHA1()
    {
        // Arrange
        var script = "return 'loaded'";

        // Act
        var sha1 = await Database!.ScriptLoadAsync(script);

        // Assert
        sha1.Should().NotBeNullOrEmpty();
        sha1.Should().HaveLength(40); // SHA1 hash is 40 hex characters
    }

    [Fact]
    public async Task ScriptEvaluateSha_WithLoadedScript_ExecutesCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        var script = "return redis.call('SET', KEYS[1], ARGV[1])";
        var sha1 = await Database!.ScriptLoadAsync(script);

        // Act
        var result = await Database!.ScriptEvaluateShaAsync(
            sha1,
            keys: [key],
            args: ["cached_value"]);

        // Assert
        result.AsString().Should().Be("OK");

        var value = await Database!.StringGetAsync(key);
        value.Should().Be("cached_value");
    }

    [Fact]
    public async Task ScriptExists_WithLoadedScript_ReturnsTrue()
    {
        // Arrange
        var script = "return 'test'";
        var sha1 = await Database!.ScriptLoadAsync(script);

        // Act
        var exists = await Database!.ScriptExistsAsync([sha1]);

        // Assert
        exists.Should().HaveCount(1);
        exists[0].Should().BeTrue();
    }

    [Fact]
    public async Task ScriptExists_WithNonExistentScript_ReturnsFalse()
    {
        // Arrange
        var fakeSha1 = "0000000000000000000000000000000000000000";

        // Act
        var exists = await Database!.ScriptExistsAsync([fakeSha1]);

        // Assert
        exists.Should().HaveCount(1);
        exists[0].Should().BeFalse();
    }

    [Fact]
    public async Task ScriptExists_WithMultipleScripts_ReturnsCorrectResults()
    {
        // Arrange
        var script1 = "return 1";
        var script2 = "return 2";
        var sha1_1 = await Database!.ScriptLoadAsync(script1);
        var sha1_2 = await Database!.ScriptLoadAsync(script2);
        var fakeSha1 = "0000000000000000000000000000000000000000";

        // Act
        var exists = await Database!.ScriptExistsAsync([sha1_1, fakeSha1, sha1_2]);

        // Assert
        exists.Should().HaveCount(3);
        exists[0].Should().BeTrue();
        exists[1].Should().BeFalse();
        exists[2].Should().BeTrue();
    }

    [Fact]
    public async Task ScriptFlush_ClearsScriptCache()
    {
        // Arrange
        var script = "return 'test'";
        var sha1 = await Database!.ScriptLoadAsync(script);

        // Verify script exists
        var existsBefore = await Database!.ScriptExistsAsync([sha1]);
        existsBefore[0].Should().BeTrue();

        // Act
        await Database!.ScriptFlushAsync();

        // Assert
        var existsAfter = await Database!.ScriptExistsAsync([sha1]);
        existsAfter[0].Should().BeFalse();
    }

    [Fact]
    public async Task ScriptEvaluate_AtomicIncrement_WorksCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        var script = @"
            local current = redis.call('GET', KEYS[1])
            if not current then
                current = 0
            end
            local new_val = tonumber(current) + tonumber(ARGV[1])
            redis.call('SET', KEYS[1], new_val)
            return new_val
        ";

        // Act
        var result1 = await Database!.ScriptEvaluateAsync(script, keys: [key], args: ["5"]);
        var result2 = await Database!.ScriptEvaluateAsync(script, keys: [key], args: ["3"]);
        var result3 = await Database!.ScriptEvaluateAsync(script, keys: [key], args: ["2"]);

        // Assert
        result1.AsInteger().Should().Be(5);
        result2.AsInteger().Should().Be(8);
        result3.AsInteger().Should().Be(10);
    }

    [Fact]
    public async Task ScriptEvaluate_ConditionalSet_OnlyUpdatesIfGreater()
    {
        // Arrange
        var key = GetTestKey();
        var script = @"
            local current = redis.call('GET', KEYS[1])
            if not current or tonumber(current) < tonumber(ARGV[1]) then
                redis.call('SET', KEYS[1], ARGV[1])
                return 1
            end
            return 0
        ";

        // Act
        var result1 = await Database!.ScriptEvaluateAsync(script, keys: [key], args: ["10"]);
        var result2 = await Database!.ScriptEvaluateAsync(script, keys: [key], args: ["5"]); // Should not update
        var result3 = await Database!.ScriptEvaluateAsync(script, keys: [key], args: ["20"]); // Should update

        // Assert
        result1.AsInteger().Should().Be(1); // Updated
        result2.AsInteger().Should().Be(0); // Not updated
        result3.AsInteger().Should().Be(1); // Updated

        var finalValue = await Database!.StringGetAsync(key);
        finalValue.Should().Be("20");
    }

    [Fact]
    public async Task ScriptEvaluate_MultiKeyOperation_WorksCorrectly()
    {
        // Arrange
        var key1 = GetTestKey("1");
        var key2 = GetTestKey("2");
        await Database!.StringSetAsync(key1, "10");
        await Database!.StringSetAsync(key2, "20");

        var script = @"
            local val1 = redis.call('GET', KEYS[1])
            local val2 = redis.call('GET', KEYS[2])
            return tonumber(val1) + tonumber(val2)
        ";

        // Act
        var result = await Database!.ScriptEvaluateAsync(
            script,
            keys: [key1, key2]);

        // Assert
        result.AsInteger().Should().Be(30);
    }
}
