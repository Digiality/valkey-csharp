using FluentAssertions;
using Valkey;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for set commands against a real Valkey/Redis server.
/// </summary>
public class SetCommandIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task SetAdd_AddsMembers()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var added1 = await Database!.SetAddAsync(key, "member1");
        var added2 = await Database.SetAddAsync(key, "member2");
        var addedDuplicate = await Database.SetAddAsync(key, "member1");

        // Assert
        added1.Should().BeTrue();
        added2.Should().BeTrue();
        addedDuplicate.Should().BeFalse();
    }

    [Fact]
    public async Task SetAddMultiple_AddsAllMembers()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var addedCount = await Database!.SetAddAsync(key, new[] { "member1", "member2", "member3" });

        // Assert
        addedCount.Should().Be(3);
    }

    [Fact]
    public async Task SetMembers_ReturnsAllMembers()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SetAddAsync(key, new[] { "member1", "member2", "member3" });

        // Act
        var members = await Database.SetMembersAsync(key);

        // Assert
        members.Should().BeEquivalentTo(new[] { "member1", "member2", "member3" });
    }

    [Fact]
    public async Task SetContains_ChecksMembership()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SetAddAsync(key, "member1");

        // Act
        var contains1 = await Database.SetContainsAsync(key, "member1");
        var contains2 = await Database.SetContainsAsync(key, "member2");

        // Assert
        contains1.Should().BeTrue();
        contains2.Should().BeFalse();
    }

    [Fact]
    public async Task SetRemove_RemovesMember()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SetAddAsync(key, "member1");

        // Act
        var removed = await Database.SetRemoveAsync(key, "member1");
        var contains = await Database.SetContainsAsync(key, "member1");

        // Assert
        removed.Should().BeTrue();
        contains.Should().BeFalse();
    }

    [Fact]
    public async Task SetRemoveMultiple_RemovesAllMembers()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SetAddAsync(key, new[] { "member1", "member2", "member3" });

        // Act
        var removed1 = await Database.SetRemoveAsync(key, "member1");
        var removed2 = await Database.SetRemoveAsync(key, "member2");

        // Assert
        removed1.Should().BeTrue();
        removed2.Should().BeTrue();
        (await Database.SetContainsAsync(key, "member1")).Should().BeFalse();
        (await Database.SetContainsAsync(key, "member2")).Should().BeFalse();
        (await Database.SetContainsAsync(key, "member3")).Should().BeTrue();
    }

    [Fact]
    public async Task SetLength_ReturnsCorrectCount()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SetAddAsync(key, new[] { "member1", "member2", "member3" });

        // Act
        var length = await Database.SetLengthAsync(key);

        // Assert
        length.Should().Be(3);
    }

    [Fact]
    public async Task SetPop_RemovesAndReturnsRandomMember()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SetAddAsync(key, new[] { "member1", "member2", "member3" });

        // Act
        var popped = await Database.SetPopAsync(key);
        var length = await Database.SetLengthAsync(key);

        // Assert
        popped.Should().NotBeNull();
        popped.Should().BeOneOf("member1", "member2", "member3");
        length.Should().Be(2);
    }

    [Fact]
    public async Task SetPopMultiple_RemovesAndReturnsMembers()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SetAddAsync(key, new[] { "member1", "member2", "member3" });

        // Act
        var popped = await Database.SetPopAsync(key, 2);
        var length = await Database.SetLengthAsync(key);

        // Assert
        popped.Should().HaveCount(2);
        popped.Should().BeSubsetOf(new[] { "member1", "member2", "member3" });
        length.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentSetOperations_AllSucceed()
    {
        // Arrange
        var key = GetTestKey();
        var tasks = new List<Task<bool>>();

        // Act - Add 20 members concurrently
        for (int i = 0; i < 20; i++)
        {
            var member = $"member{i}";
            tasks.Add(Database!.SetAddAsync(key, member).AsTask());
        }

        await Task.WhenAll(tasks);

        // Assert
        tasks.Should().AllSatisfy(t => t.Result.Should().BeTrue());
        var length = await Database!.SetLengthAsync(key);
        length.Should().Be(20);
    }

    [Fact]
    public async Task SetOperations_MaintainUniqueness()
    {
        // Arrange
        var key = GetTestKey();

        // Act - Add same member multiple times
        await Database!.SetAddAsync(key, "member1");
        await Database.SetAddAsync(key, "member1");
        await Database.SetAddAsync(key, "member1");
        var length = await Database.SetLengthAsync(key);

        // Assert
        length.Should().Be(1);
    }
}
