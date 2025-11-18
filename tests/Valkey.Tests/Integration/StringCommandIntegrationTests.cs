using FluentAssertions;
using Valkey;
using Valkey.Configuration;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for string commands against a real Valkey/Redis server.
/// Tests only the currently implemented string methods.
/// </summary>
public class StringCommandIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task StringSet_AndGet_RoundTrip()
    {
        // Arrange
        var key = GetTestKey();
        var value = "Hello, Valkey!";

        // Act
        var setResult = await Database!.StringSetAsync(key, value);
        var getResult = await Database.StringGetAsync(key);

        // Assert
        setResult.Should().BeTrue();
        getResult.Should().Be(value);
    }

    [Fact]
    public async Task StringGet_NonExistentKey_ReturnsNull()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var result = await Database!.StringGetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task StringGetBytes_ReturnsCorrectBytes()
    {
        // Arrange
        var key = GetTestKey();
        var value = "Hello, Valkey!";
        await Database!.StringSetAsync(key, value);

        // Act
        var result = await Database.StringGetBytesAsync(key);

        // Assert
        result.Should().NotBeNull();
        System.Text.Encoding.UTF8.GetString(result!).Should().Be(value);
    }

    [Fact]
    public async Task StringIncrement_NewKey_ReturnsOne()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var result = await Database!.StringIncrementAsync(key);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task StringIncrement_ExistingKey_Increments()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "10");

        // Act
        var result = await Database.StringIncrementAsync(key);

        // Assert
        result.Should().Be(11);
    }

    [Fact]
    public async Task StringIncrementBy_IncrementsCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "10");

        // Act
        var result = await Database.StringIncrementAsync(key, 5);

        // Assert
        result.Should().Be(15);
    }

    [Fact]
    public async Task StringDecrement_DecrementsByOne()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "10");

        // Act
        var result = await Database.StringDecrementAsync(key);

        // Assert
        result.Should().Be(9);
    }

    [Fact]
    public async Task StringDecrementBy_DecrementsCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "20");

        // Act
        var result = await Database.StringDecrementAsync(key, 7);

        // Assert
        result.Should().Be(13);
    }

    [Fact]
    public async Task MultipleStringOperations_InSequence()
    {
        // Arrange
        var key = GetTestKey();

        // Act & Assert
        await Database!.StringSetAsync(key, "0");
        (await Database.StringIncrementAsync(key)).Should().Be(1);
        (await Database.StringIncrementAsync(key, 9)).Should().Be(10);
        (await Database.StringDecrementAsync(key)).Should().Be(9);
        (await Database.StringDecrementAsync(key, 4)).Should().Be(5);
        (await Database.StringGetAsync(key)).Should().Be("5");
    }

    [Fact]
    public async Task ConcurrentStringOperations_AllSucceed()
    {
        // Arrange
        var tasks = new List<Task<bool>>();
        var keys = new List<string>();

        // Act - Create 20 SET operations in parallel
        for (int i = 0; i < 20; i++)
        {
            var key = GetTestKey($"concurrent_{i}");
            keys.Add(key);
            tasks.Add(Database!.StringSetAsync(key, $"value_{i}").AsTask());
        }

        await Task.WhenAll(tasks);

        // Assert - All SETs succeeded
        tasks.Should().AllSatisfy(t => t.Result.Should().BeTrue());

        // Verify all values are correct
        for (int i = 0; i < 20; i++)
        {
            var value = await Database!.StringGetAsync(keys[i]);
            value.Should().Be($"value_{i}");
        }
    }

    [Fact]
    public async Task ConcurrentIncrements_AllSucceed()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "0");
        var tasks = new List<Task<long>>();

        // Act - Perform 10 increments concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Database.StringIncrementAsync(key).AsTask());
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Final value should be 10
        var finalValue = await Database.StringGetAsync(key);
        finalValue.Should().Be("10");

        // All results should be unique (1-10 in some order)
        results.Should().OnlyHaveUniqueItems();
        results.Should().AllSatisfy(r => r.Should().BeInRange(1, 10));
    }
}
