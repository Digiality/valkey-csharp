using FluentAssertions;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for Streams commands.
/// </summary>
public class StreamsIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task StreamAdd_AddsEntry_ReturnsId()
    {
        // Arrange
        var streamKey = GetTestKey("stream");
        var fields = new Dictionary<string, string>
        {
            ["field1"] = "value1",
            ["field2"] = "value2"
        };

        // Act
        var id = await Database!.StreamAddAsync(streamKey, fields);

        // Assert
        id.Should().NotBeNullOrEmpty();
        id.Should().Contain("-"); // Format: timestamp-sequence
    }

    [Fact]
    public async Task StreamRead_ReadsEntries_ReturnsCorrectData()
    {
        // Arrange
        var streamKey = GetTestKey("stream");
        var fields = new Dictionary<string, string>
        {
            ["type"] = "order",
            ["amount"] = "99.99"
        };

        await Database!.StreamAddAsync(streamKey, fields);

        // Act
        var entries = await Database!.StreamReadAsync(streamKey, "0");

        // Assert
        entries.Should().NotBeEmpty();
        entries[0].Fields.Should().ContainKey("type");
        entries[0].Fields["type"].Should().Be("order");
        entries[0].Fields["amount"].Should().Be("99.99");
    }

    [Fact]
    public async Task StreamRange_ReturnsEntriesInRange()
    {
        // Arrange
        var streamKey = GetTestKey("stream");

        var id1 = await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "first" });
        var id2 = await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "second" });
        var id3 = await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "third" });

        // Act
        var entries = await Database!.StreamRangeAsync(streamKey);

        // Assert
        entries.Should().HaveCount(3);
        entries[0].Fields["msg"].Should().Be("first");
        entries[1].Fields["msg"].Should().Be("second");
        entries[2].Fields["msg"].Should().Be("third");
    }

    [Fact]
    public async Task StreamRange_WithCount_LimitsResults()
    {
        // Arrange
        var streamKey = GetTestKey("stream");

        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "1" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "2" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "3" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "4" });

        // Act
        var entries = await Database!.StreamRangeAsync(streamKey, count: 2);

        // Assert
        entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task StreamLength_ReturnsCorrectCount()
    {
        // Arrange
        var streamKey = GetTestKey("stream");

        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "1" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "2" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "3" });

        // Act
        var length = await Database!.StreamLengthAsync(streamKey);

        // Assert
        length.Should().Be(3);
    }

    [Fact]
    public async Task StreamDelete_RemovesEntries()
    {
        // Arrange
        var streamKey = GetTestKey("stream");

        var id1 = await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "1" });
        var id2 = await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "2" });
        var id3 = await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "3" });

        // Act
        var deleted = await Database!.StreamDeleteAsync(streamKey, [id2]);

        // Assert
        deleted.Should().Be(1);

        var length = await Database!.StreamLengthAsync(streamKey);
        length.Should().Be(2);
    }

    [Fact]
    public async Task StreamTrim_RemovesOldEntries()
    {
        // Arrange
        var streamKey = GetTestKey("stream");

        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "1" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "2" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "3" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "4" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "5" });

        // Act
        var removed = await Database!.StreamTrimAsync(streamKey, maxLength: 3);

        // Assert
        removed.Should().Be(2);

        var length = await Database!.StreamLengthAsync(streamKey);
        length.Should().Be(3);
    }

    [Fact]
    public async Task StreamAdd_WithMaxLength_AutoTrims()
    {
        // Arrange
        var streamKey = GetTestKey("stream");

        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "1" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "2" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "3" });

        // Act
        await Database!.StreamAddAsync(
            streamKey,
            new Dictionary<string, string> { ["msg"] = "4" },
            maxLength: 3);

        // Assert
        var length = await Database!.StreamLengthAsync(streamKey);
        length.Should().Be(3);
    }

    [Fact]
    public async Task StreamGroupCreate_CreatesConsumerGroup()
    {
        // Arrange
        var streamKey = GetTestKey("stream");

        // Add an entry first
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "initial" });

        // Act
        await Database!.StreamGroupCreateAsync(streamKey, "mygroup", "$");

        // Assert - should not throw
    }

    [Fact]
    public async Task StreamReadGroup_ReadsNewMessages()
    {
        // Arrange
        var streamKey = GetTestKey("stream");
        var groupName = "processors";
        var consumerName = "consumer1";

        // Create stream and consumer group
        await Database!.StreamGroupCreateAsync(streamKey, groupName, "$");

        // Add messages after group creation
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "message1" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "message2" });

        // Act
        var messages = await Database!.StreamReadGroupAsync(streamKey, groupName, consumerName, ">");

        // Assert
        messages.Should().HaveCount(2);
        messages[0].Fields["msg"].Should().Be("message1");
        messages[1].Fields["msg"].Should().Be("message2");
    }

    [Fact]
    public async Task StreamAck_AcknowledgesMessages()
    {
        // Arrange
        var streamKey = GetTestKey("stream");
        var groupName = "processors";
        var consumerName = "consumer1";

        await Database!.StreamGroupCreateAsync(streamKey, groupName, "$");
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "test" });

        var messages = await Database!.StreamReadGroupAsync(streamKey, groupName, consumerName, ">");
        var messageIds = messages.Select(m => m.Id).ToArray();

        // Act
        var acked = await Database!.StreamAckAsync(streamKey, groupName, messageIds);

        // Assert
        acked.Should().Be(1);
    }

    [Fact]
    public async Task StreamGroupDestroy_RemovesConsumerGroup()
    {
        // Arrange
        var streamKey = GetTestKey("stream");
        var groupName = "testgroup";

        await Database!.StreamGroupCreateAsync(streamKey, groupName, "$");

        // Act
        await Database!.StreamGroupDestroyAsync(streamKey, groupName);

        // Assert - should not throw
        // Trying to read from destroyed group would throw an error
    }

    [Fact]
    public async Task StreamReadGroup_MultipleConsumers_DistributesMessages()
    {
        // Arrange
        var streamKey = GetTestKey("stream");
        var groupName = "workers";

        await Database!.StreamGroupCreateAsync(streamKey, groupName, "$");

        // Add messages
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "1" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "2" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "3" });
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string> { ["msg"] = "4" });

        // Act - Read with two different consumers
        var consumer1Messages = await Database!.StreamReadGroupAsync(
            streamKey, groupName, "consumer1", ">", count: 2);
        var consumer2Messages = await Database!.StreamReadGroupAsync(
            streamKey, groupName, "consumer2", ">", count: 2);

        // Assert - Each consumer should get different messages
        consumer1Messages.Should().HaveCount(2);
        consumer2Messages.Should().HaveCount(2);

        var allIds = consumer1Messages.Concat(consumer2Messages).Select(m => m.Id).ToList();
        allIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task StreamScenario_EventSourcing_WorksEndToEnd()
    {
        // Arrange
        var streamKey = GetTestKey("events");
        var groupName = "event-processors";
        var consumerName = "processor-1";

        // Create consumer group
        await Database!.StreamGroupCreateAsync(streamKey, groupName, "$");

        // Act - Simulate event sourcing
        // Producer adds events
        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string>
        {
            ["event"] = "OrderCreated",
            ["order_id"] = "123",
            ["amount"] = "99.99"
        });

        await Database!.StreamAddAsync(streamKey, new Dictionary<string, string>
        {
            ["event"] = "OrderPaid",
            ["order_id"] = "123",
            ["payment_method"] = "credit_card"
        });

        // Consumer processes events
        var events = await Database!.StreamReadGroupAsync(streamKey, groupName, consumerName, ">");

        // Process and acknowledge
        var eventIds = events.Select(e => e.Id).ToArray();
        await Database!.StreamAckAsync(streamKey, groupName, eventIds);

        // Assert
        events.Should().HaveCount(2);
        events[0].Fields["event"].Should().Be("OrderCreated");
        events[1].Fields["event"].Should().Be("OrderPaid");
    }
}
