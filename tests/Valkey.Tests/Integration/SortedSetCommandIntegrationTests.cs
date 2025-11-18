using FluentAssertions;
using Valkey;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for sorted set commands against a real Valkey/Redis server.
/// </summary>
public class SortedSetCommandIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task SortedSetAdd_AddsMemberWithScore()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var added = await Database!.SortedSetAddAsync(key, "member1", 10.5);
        var score = await Database.SortedSetScoreAsync(key, "member1");

        // Assert
        added.Should().BeTrue();
        score.Should().Be(10.5);
    }

    [Fact]
    public async Task SortedSetAdd_UpdatesExistingMember()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SortedSetAddAsync(key, "member1", 10.0);

        // Act
        var added = await Database.SortedSetAddAsync(key, "member1", 20.0);
        var score = await Database.SortedSetScoreAsync(key, "member1");

        // Assert
        added.Should().BeFalse(); // Already existed
        score.Should().Be(20.0); // Score updated
    }

    [Fact]
    public async Task SortedSetAddMultiple_AddsAllMembers()
    {
        // Arrange
        var key = GetTestKey();
        var members = new[]
        {
            ("member1", 10.0),
            ("member2", 20.0),
            ("member3", 30.0)
        };

        // Act
        var addedCount = await Database!.SortedSetAddAsync(key, members);

        // Assert
        addedCount.Should().Be(3);
    }

    [Fact]
    public async Task SortedSetRangeByRank_ReturnsInScoreOrder()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SortedSetAddAsync(key, new[]
        {
            ("member3", 30.0),
            ("member1", 10.0),
            ("member2", 20.0)
        });

        // Act
        var range = await Database.SortedSetRangeByRankAsync(key, 0, -1);

        // Assert
        range.Should().Equal("member1", "member2", "member3");
    }

    [Fact]
    public async Task SortedSetRangeByRankWithScores_ReturnsWithScores()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SortedSetAddAsync(key, new[]
        {
            ("member1", 10.0),
            ("member2", 20.0),
            ("member3", 30.0)
        });

        // Act
        var range = await Database.SortedSetRangeByRankWithScoresAsync(key, 0, -1);

        // Assert
        range.Should().Equal(
            ("member1", 10.0),
            ("member2", 20.0),
            ("member3", 30.0)
        );
    }

    [Fact]
    public async Task SortedSetRangeByScore_ReturnsMatchingMembers()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SortedSetAddAsync(key, new[]
        {
            ("member1", 10.0),
            ("member2", 20.0),
            ("member3", 30.0),
            ("member4", 40.0)
        });

        // Act
        var range = await Database.SortedSetRangeByScoreAsync(key, 15.0, 35.0);

        // Assert
        range.Should().Equal("member2", "member3");
    }

    [Fact]
    public async Task SortedSetRank_ReturnsCorrectPosition()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SortedSetAddAsync(key, new[]
        {
            ("member1", 10.0),
            ("member2", 20.0),
            ("member3", 30.0)
        });

        // Act
        var rank = await Database.SortedSetRankAsync(key, "member2");

        // Assert
        rank.Should().Be(1); // 0-indexed, so second position
    }

    [Fact]
    public async Task SortedSetRank_NonExistentMember_ReturnsNull()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var rank = await Database!.SortedSetRankAsync(key, "nonexistent");

        // Assert
        rank.Should().BeNull();
    }

    [Fact]
    public async Task SortedSetRemove_RemovesMember()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SortedSetAddAsync(key, "member1", 10.0);

        // Act
        var removed = await Database.SortedSetRemoveAsync(key, "member1");
        var score = await Database.SortedSetScoreAsync(key, "member1");

        // Assert
        removed.Should().BeTrue();
        score.Should().BeNull();
    }

    [Fact]
    public async Task SortedSetLength_ReturnsCorrectCount()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SortedSetAddAsync(key, new[]
        {
            ("member1", 10.0),
            ("member2", 20.0),
            ("member3", 30.0)
        });

        // Act
        var length = await Database.SortedSetLengthAsync(key);

        // Assert
        length.Should().Be(3);
    }

    [Fact]
    public async Task SortedSetIncrement_IncrementsScore()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.SortedSetAddAsync(key, "member1", 10.0);

        // Act
        var newScore = await Database.SortedSetIncrementAsync(key, "member1", 5.5);

        // Assert
        newScore.Should().Be(15.5);
        (await Database.SortedSetScoreAsync(key, "member1")).Should().Be(15.5);
    }

    [Fact]
    public async Task SortedSetIncrement_NonExistentMember_CreatesWithScore()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var newScore = await Database!.SortedSetIncrementAsync(key, "member1", 5.5);

        // Assert
        newScore.Should().Be(5.5);
    }

    [Fact]
    public async Task ConcurrentSortedSetOperations_AllSucceed()
    {
        // Arrange
        var key = GetTestKey();
        var tasks = new List<Task<bool>>();

        // Act - Add 20 members concurrently
        for (int i = 0; i < 20; i++)
        {
            var member = $"member{i}";
            var score = i * 10.0;
            tasks.Add(Database!.SortedSetAddAsync(key, member, score).AsTask());
        }

        await Task.WhenAll(tasks);

        // Assert
        tasks.Should().AllSatisfy(t => t.Result.Should().BeTrue());
        var length = await Database!.SortedSetLengthAsync(key);
        length.Should().Be(20);
    }

    [Fact]
    public async Task SortedSet_MaintainsScoreOrdering()
    {
        // Arrange
        var key = GetTestKey();

        // Act - Add members in random order
        await Database!.SortedSetAddAsync(key, "charlie", 30.0);
        await Database.SortedSetAddAsync(key, "alice", 10.0);
        await Database.SortedSetAddAsync(key, "eve", 50.0);
        await Database.SortedSetAddAsync(key, "bob", 20.0);
        await Database.SortedSetAddAsync(key, "david", 40.0);

        var range = await Database.SortedSetRangeByRankAsync(key, 0, -1);

        // Assert - Should be ordered by score
        range.Should().Equal("alice", "bob", "charlie", "david", "eve");
    }

    [Fact]
    public async Task SortedSet_Leaderboard_Scenario()
    {
        // Arrange - Simulate a game leaderboard
        var key = GetTestKey();
        await Database!.SortedSetAddAsync(key, new[]
        {
            ("player1", 1000.0),
            ("player2", 1500.0),
            ("player3", 800.0),
            ("player4", 2000.0),
            ("player5", 1200.0)
        });

        // Act - Get top 3 players
        var top3 = await Database.SortedSetRangeByRankAsync(key, -3, -1);

        // Assert
        top3.Should().Equal("player5", "player2", "player4"); // Ordered by score ascending, last 3 are highest
    }
}
