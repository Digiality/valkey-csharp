using FluentAssertions;
using Valkey;
using Valkey.Configuration;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for ValkeyConnection against a real Valkey/Redis server.
/// </summary>
public class ConnectionIntegrationTests : IntegrationTestBase
{
    [Fact]
    public void Connection_IsEstablished_Successfully()
    {
        // Assert
        Connection.Should().NotBeNull();
        Connection!.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void GetDatabase_ReturnsValidDatabase()
    {
        // Act
        var database = Connection!.GetDatabase();

        // Assert
        database.Should().NotBeNull();
        database.DatabaseNumber.Should().Be(0);
    }

    [Fact]
    public void GetDatabase_WithDatabaseNumber_ReturnsCorrectDatabase()
    {
        // Act
        var database = Connection!.GetDatabase(1);

        // Assert
        database.Should().NotBeNull();
        database.DatabaseNumber.Should().Be(1);
    }

    [Fact]
    public async Task Ping_ReturnsPong()
    {
        // Act
        var result = await Database!.PingAsync();

        // Assert
        result.Should().Be("PONG");
    }

    [Fact]
    public async Task Echo_ReturnsMessage()
    {
        // Arrange
        var message = "Hello, Valkey!";

        // Act
        var result = await Database!.EchoAsync(message);

        // Assert
        result.Should().Be(message);
    }

    [Fact]
    public async Task MultipleSimultaneousCommands_ExecuteCorrectly()
    {
        // Arrange
        var tasks = new List<Task<bool>>();

        // Act - Execute 10 SET commands in parallel
        for (int i = 0; i < 10; i++)
        {
            var key = GetTestKey($"key{i}");
            var value = $"value{i}";
            tasks.Add(Database!.StringSetAsync(key, value).AsTask());
        }

        await Task.WhenAll(tasks);

        // Assert - All commands should have succeeded
        tasks.Should().AllSatisfy(t => t.Result.Should().BeTrue());
    }
}
