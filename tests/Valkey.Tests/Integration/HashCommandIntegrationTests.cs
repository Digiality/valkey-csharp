using FluentAssertions;
using Valkey;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for hash commands against a real Valkey/Redis server.
/// </summary>
public class HashCommandIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task HashSet_AndGet_RoundTrip()
    {
        // Arrange
        var key = GetTestKey();
        var field = "field1";
        var value = "value1";

        // Act
        var setResult = await Database!.HashSetAsync(key, field, value);
        var getResult = await Database.HashGetAsync(key, field);

        // Assert
        setResult.Should().BeTrue();
        getResult.Should().Be(value);
    }

    [Fact]
    public async Task HashGet_NonExistentField_ReturnsNull()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var result = await Database!.HashGetAsync(key, "nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task HashSetMultiple_SetsAllFields()
    {
        // Arrange
        var key = GetTestKey();
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };

        // Act
        await Database!.HashSetAsync(key, fields);

        // Assert
        foreach (var kvp in fields)
        {
            var value = await Database.HashGetAsync(key, kvp.Key);
            value.Should().Be(kvp.Value);
        }
    }

    [Fact]
    public async Task HashGetAll_ReturnsAllFields()
    {
        // Arrange
        var key = GetTestKey();
        var expected = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        };
        await Database!.HashSetAsync(key, expected);

        // Act
        var result = await Database.HashGetAllAsync(key);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task HashDelete_RemovesField()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.HashSetAsync(key, "field1", "value1");

        // Act
        var deleteResult = await Database.HashDeleteAsync(key, "field1");
        var existsResult = await Database.HashExistsAsync(key, "field1");

        // Assert
        deleteResult.Should().BeTrue();
        existsResult.Should().BeFalse();
    }

    [Fact]
    public async Task HashDeleteMultiple_RemovesAllFields()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.HashSetAsync(key, new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        });

        // Act
        var deletedCount = await Database.HashDeleteAsync(key, new[] { "field1", "field2" });

        // Assert
        deletedCount.Should().Be(2);
        (await Database.HashExistsAsync(key, "field1")).Should().BeFalse();
        (await Database.HashExistsAsync(key, "field2")).Should().BeFalse();
        (await Database.HashExistsAsync(key, "field3")).Should().BeTrue();
    }

    [Fact]
    public async Task HashExists_ExistingField_ReturnsTrue()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.HashSetAsync(key, "field1", "value1");

        // Act
        var result = await Database.HashExistsAsync(key, "field1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HashExists_NonExistentField_ReturnsFalse()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var result = await Database!.HashExistsAsync(key, "field1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HashLength_ReturnsCorrectCount()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.HashSetAsync(key, new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        });

        // Act
        var length = await Database.HashLengthAsync(key);

        // Assert
        length.Should().Be(3);
    }

    [Fact]
    public async Task HashKeys_ReturnsAllFieldNames()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.HashSetAsync(key, new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        });

        // Act
        var keys = await Database.HashKeysAsync(key);

        // Assert
        keys.Should().BeEquivalentTo(new[] { "field1", "field2" });
    }

    [Fact]
    public async Task HashValues_ReturnsAllFieldValues()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.HashSetAsync(key, new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        });

        // Act
        var values = await Database.HashValuesAsync(key);

        // Assert
        values.Should().BeEquivalentTo(new[] { "value1", "value2" });
    }

    [Fact]
    public async Task HashIncrement_IncrementsField()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.HashSetAsync(key, "counter", "10");

        // Act
        var result = await Database.HashIncrementAsync(key, "counter", 5);

        // Assert
        result.Should().Be(15);
        (await Database.HashGetAsync(key, "counter")).Should().Be("15");
    }

    [Fact]
    public async Task ConcurrentHashOperations_AllSucceed()
    {
        // Arrange
        var key = GetTestKey();
        var tasks = new List<Task<bool>>();

        // Act - Set 20 fields concurrently
        for (int i = 0; i < 20; i++)
        {
            var field = $"field{i}";
            var value = $"value{i}";
            tasks.Add(Database!.HashSetAsync(key, field, value).AsTask());
        }

        await Task.WhenAll(tasks);

        // Assert
        tasks.Should().AllSatisfy(t => t.Result.Should().BeTrue());
        var length = await Database!.HashLengthAsync(key);
        length.Should().Be(20);
    }
}
