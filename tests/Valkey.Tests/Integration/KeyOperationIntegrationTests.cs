using FluentAssertions;
using Valkey;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for key operations against a real Valkey/Redis server.
/// </summary>
public class KeyOperationIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task KeyDelete_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "value");

        // Act
        var result = await Database.KeyDeleteAsync(key);
        var exists = await Database.KeyExistsAsync(key);

        // Assert
        result.Should().BeTrue();
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task KeyDelete_NonExistentKey_ReturnsFalse()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var result = await Database!.KeyDeleteAsync(key);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task KeyDeleteMultiple_DeletesAllKeys()
    {
        // Arrange
        var keys = new[] { GetTestKey("1"), GetTestKey("2"), GetTestKey("3") };
        foreach (var key in keys)
        {
            await Database!.StringSetAsync(key, "value");
        }

        // Act
        var deletedCount = await Database!.KeyDeleteAsync(keys);

        // Assert
        deletedCount.Should().Be(3);
        foreach (var key in keys)
        {
            (await Database!.KeyExistsAsync(key)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task KeyExists_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "value");

        // Act
        var result = await Database.KeyExistsAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task KeyExists_NonExistentKey_ReturnsFalse()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var result = await Database!.KeyExistsAsync(key);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task KeyExpire_SetsExpiration()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "value");

        // Act
        var result = await Database.KeyExpireAsync(key, TimeSpan.FromSeconds(10));

        // Assert
        result.Should().BeTrue();
        (await Database.KeyExistsAsync(key)).Should().BeTrue();
    }

    [Fact]
    public async Task KeyExpire_NonExistentKey_ReturnsFalse()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var result = await Database!.KeyExpireAsync(key, TimeSpan.FromSeconds(10));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task KeyExpire_ShortDuration_KeyDisappears()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "value");

        // Act
        await Database.KeyExpireAsync(key, TimeSpan.FromMilliseconds(100));
        await Task.Delay(200); // Wait for expiration
        var exists = await Database.KeyExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentKeyOperations_AllSucceed()
    {
        // Arrange
        var keys = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            keys.Add(GetTestKey($"concurrent_{i}"));
        }

        // Act - Create keys concurrently
        var setTasks = keys.Select(k => Database!.StringSetAsync(k, "value").AsTask()).ToList();
        await Task.WhenAll(setTasks);

        // Verify all exist
        var existsTasks = keys.Select(k => Database!.KeyExistsAsync(k).AsTask()).ToList();
        var existsResults = await Task.WhenAll(existsTasks);
        existsResults.Should().AllSatisfy(r => r.Should().BeTrue());

        // Delete concurrently
        var deleteTasks = keys.Select(k => Database!.KeyDeleteAsync(k).AsTask()).ToList();
        var deleteResults = await Task.WhenAll(deleteTasks);

        // Assert
        deleteResults.Should().AllSatisfy(r => r.Should().BeTrue());

        // Verify all deleted
        var existsAfterTasks = keys.Select(k => Database!.KeyExistsAsync(k).AsTask()).ToList();
        var existsAfterResults = await Task.WhenAll(existsAfterTasks);
        existsAfterResults.Should().AllSatisfy(r => r.Should().BeFalse());
    }
}
