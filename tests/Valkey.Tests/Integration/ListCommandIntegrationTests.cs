using FluentAssertions;
using Valkey;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for list commands against a real Valkey/Redis server.
/// </summary>
public class ListCommandIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task ListPush_AndPop_RoundTrip()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        await Database!.ListLeftPushAsync(key, "value1");
        await Database.ListLeftPushAsync(key, "value2");
        var pop1 = await Database.ListLeftPopAsync(key);
        var pop2 = await Database.ListLeftPopAsync(key);

        // Assert
        pop1.Should().Be("value2");
        pop2.Should().Be("value1");
    }

    [Fact]
    public async Task ListRightPush_AndRightPop_RoundTrip()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        await Database!.ListRightPushAsync(key, "value1");
        await Database.ListRightPushAsync(key, "value2");
        var pop1 = await Database.ListRightPopAsync(key);
        var pop2 = await Database.ListRightPopAsync(key);

        // Assert
        pop1.Should().Be("value2");
        pop2.Should().Be("value1");
    }

    [Fact]
    public async Task ListLength_ReturnsCorrectCount()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        await Database!.ListRightPushAsync(key, "value1");
        await Database.ListRightPushAsync(key, "value2");
        await Database.ListRightPushAsync(key, "value3");
        var length = await Database.ListLengthAsync(key);

        // Assert
        length.Should().Be(3);
    }

    [Fact]
    public async Task ListRange_ReturnsCorrectElements()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.ListRightPushAsync(key, "value1");
        await Database.ListRightPushAsync(key, "value2");
        await Database.ListRightPushAsync(key, "value3");

        // Act
        var range = await Database.ListRangeAsync(key, 0, -1);

        // Assert
        range.Should().Equal("value1", "value2", "value3");
    }

    [Fact]
    public async Task ListRange_PartialRange_ReturnsSubset()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.ListRightPushAsync(key, "value1");
        await Database!.ListRightPushAsync(key, "value2");
        await Database!.ListRightPushAsync(key, "value3");

        // Act
        var range = await Database.ListRangeAsync(key, 1, 2);

        // Assert
        range.Should().Equal("value2", "value3");
    }

    [Fact]
    public async Task ListIndex_ReturnsCorrectElement()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.ListRightPushAsync(key, "value1");
        await Database.ListRightPushAsync(key, "value2");
        await Database.ListRightPushAsync(key, "value3");

        // Act
        var value = await Database.ListIndexAsync(key, 1);

        // Assert
        value.Should().Be("value2");
    }

    [Fact]
    public async Task ListSet_UpdatesElement()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.ListRightPushAsync(key, "value1");
        await Database.ListRightPushAsync(key, "value2");

        // Act
        await Database.ListSetAsync(key, 1, "updated");
        var value = await Database.ListIndexAsync(key, 1);

        // Assert
        value.Should().Be("updated");
    }

    [Fact]
    public async Task ListTrim_KeepsOnlySpecifiedRange()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.ListRightPushAsync(key, "value1");
        await Database.ListRightPushAsync(key, "value2");
        await Database.ListRightPushAsync(key, "value3");
        await Database.ListRightPushAsync(key, "value4");

        // Act
        await Database.ListTrimAsync(key, 1, 2);
        var range = await Database.ListRangeAsync(key, 0, -1);

        // Assert
        range.Should().Equal("value2", "value3");
    }

    [Fact]
    public async Task ListPop_EmptyList_ReturnsNull()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var result = await Database!.ListLeftPopAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentListOperations_AllSucceed()
    {
        // Arrange
        var key = GetTestKey();
        var tasks = new List<Task<long>>();

        // Act - Push 20 values concurrently
        for (int i = 0; i < 20; i++)
        {
            var value = $"value{i}";
            tasks.Add(Database!.ListRightPushAsync(key, value).AsTask());
        }

        await Task.WhenAll(tasks);

        // Assert
        var length = await Database!.ListLengthAsync(key);
        length.Should().Be(20);
    }

    [Fact]
    public async Task ListAsQueue_FIFO_Behavior()
    {
        // Arrange
        var key = GetTestKey();

        // Act - Push to right, pop from left (FIFO)
        await Database!.ListRightPushAsync(key, "first");
        await Database.ListRightPushAsync(key, "second");
        await Database.ListRightPushAsync(key, "third");

        var pop1 = await Database.ListLeftPopAsync(key);
        var pop2 = await Database.ListLeftPopAsync(key);
        var pop3 = await Database.ListLeftPopAsync(key);

        // Assert
        pop1.Should().Be("first");
        pop2.Should().Be("second");
        pop3.Should().Be("third");
    }

    [Fact]
    public async Task ListAsStack_LIFO_Behavior()
    {
        // Arrange
        var key = GetTestKey();

        // Act - Push and pop from same side (LIFO)
        await Database!.ListRightPushAsync(key, "first");
        await Database.ListRightPushAsync(key, "second");
        await Database.ListRightPushAsync(key, "third");

        var pop1 = await Database.ListRightPopAsync(key);
        var pop2 = await Database.ListRightPopAsync(key);
        var pop3 = await Database.ListRightPopAsync(key);

        // Assert
        pop1.Should().Be("third");
        pop2.Should().Be("second");
        pop3.Should().Be("first");
    }
}
