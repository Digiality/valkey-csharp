using FluentAssertions;
using Valkey;

namespace Valkey.Tests.Integration;

/// <summary>
/// End-to-end scenario tests that combine multiple commands and data structures
/// to simulate real-world use cases.
/// </summary>
public class EndToEndScenarioTests : IntegrationTestBase
{
    [Fact]
    public async Task UserSession_Scenario()
    {
        // Scenario: User login session management with expiration
        var sessionKey = GetTestKey("session:user123");
        var userDataKey = GetTestKey("user:123");

        // Act - Create user session
        await Database!.StringSetAsync(sessionKey, "active");
        await Database.KeyExpireAsync(sessionKey, TimeSpan.FromSeconds(30));

        // Store user data in hash
        await Database.HashSetAsync(userDataKey, new Dictionary<string, string>
        {
            { "username", "john_doe" },
            { "email", "john@example.com" },
            { "last_login", DateTimeOffset.UtcNow.ToString("o") }
        });

        // Assert - Session exists
        (await Database.KeyExistsAsync(sessionKey)).Should().BeTrue();
        var username = await Database.HashGetAsync(userDataKey, "username");
        username.Should().Be("john_doe");

        // Cleanup
        await Database.KeyDeleteAsync(sessionKey);
        await Database.KeyDeleteAsync(userDataKey);
    }

    [Fact]
    public async Task ShoppingCart_Scenario()
    {
        // Scenario: E-commerce shopping cart using hash
        var cartKey = GetTestKey("cart:user456");

        // Act - Add items to cart
        await Database!.HashSetAsync(cartKey, "item:101", "2"); // 2 units of item 101
        await Database.HashSetAsync(cartKey, "item:102", "1"); // 1 unit of item 102
        await Database.HashSetAsync(cartKey, "item:103", "5"); // 5 units of item 103

        // Get cart contents
        var cart = await Database.HashGetAllAsync(cartKey);

        // Update quantity
        await Database.HashSetAsync(cartKey, "item:101", "3");

        // Remove item
        await Database.HashDeleteAsync(cartKey, "item:102");

        // Assert
        cart.Should().HaveCount(3);
        (await Database.HashGetAsync(cartKey, "item:101")).Should().Be("3");
        (await Database.HashExistsAsync(cartKey, "item:102")).Should().BeFalse();
        (await Database.HashLengthAsync(cartKey)).Should().Be(2);

        // Cleanup
        await Database.KeyDeleteAsync(cartKey);
    }

    [Fact]
    public async Task MessageQueue_Scenario()
    {
        // Scenario: Simple message queue using lists
        var queueKey = GetTestKey("queue:notifications");

        // Act - Enqueue messages
        await Database!.ListRightPushAsync(queueKey, "Message 1");
        await Database.ListRightPushAsync(queueKey, "Message 2");
        await Database.ListRightPushAsync(queueKey, "Message 3");

        // Dequeue messages (FIFO)
        var msg1 = await Database.ListLeftPopAsync(queueKey);
        var msg2 = await Database.ListLeftPopAsync(queueKey);

        // Check remaining
        var remaining = await Database.ListLengthAsync(queueKey);

        // Assert
        msg1.Should().Be("Message 1");
        msg2.Should().Be("Message 2");
        remaining.Should().Be(1);

        // Cleanup
        await Database.KeyDeleteAsync(queueKey);
    }

    [Fact]
    public async Task PageViewCounter_Scenario()
    {
        // Scenario: Page view counter using string increment
        var counterKey = GetTestKey("pageviews:article:789");

        // Act - Simulate page views
        await Database!.StringIncrementAsync(counterKey);
        await Database!.StringIncrementAsync(counterKey);
        await Database.StringIncrementAsync(counterKey, 3); // Batch views

        // Get total views
        var views = await Database.StringGetAsync(counterKey);

        // Assert
        views.Should().Be("5");

        // Cleanup
        await Database.KeyDeleteAsync(counterKey);
    }

    [Fact]
    public async Task TagSystem_Scenario()
    {
        // Scenario: Article tagging system using sets
        var articleTagsKey = GetTestKey("article:101:tags");
        var tagArticlesKey = GetTestKey("tag:csharp:articles");

        // Act - Add tags to article
        await Database!.SetAddAsync(articleTagsKey, new[] { "csharp", "dotnet", "async" });

        // Add article to tag index
        await Database.SetAddAsync(tagArticlesKey, "article:101");

        // Check if article has specific tag
        var hasCSharp = await Database.SetContainsAsync(articleTagsKey, "csharp");
        var hasJava = await Database.SetContainsAsync(articleTagsKey, "java");

        // Get all tags
        var allTags = await Database.SetMembersAsync(articleTagsKey);

        // Assert
        hasCSharp.Should().BeTrue();
        hasJava.Should().BeFalse();
        allTags.Should().BeEquivalentTo(new[] { "csharp", "dotnet", "async" });

        // Cleanup
        await Database.KeyDeleteAsync(articleTagsKey);
        await Database.KeyDeleteAsync(tagArticlesKey);
    }

    [Fact]
    public async Task Leaderboard_Scenario()
    {
        // Scenario: Game leaderboard using sorted set
        var leaderboardKey = GetTestKey("leaderboard:game1");

        // Act - Add player scores
        await Database!.SortedSetAddAsync(leaderboardKey, new[]
        {
            ("player1", 1500.0),
            ("player2", 2000.0),
            ("player3", 1200.0),
            ("player4", 1800.0),
            ("player5", 2200.0)
        });

        // Get top 3 players (highest scores)
        var top3 = await Database.SortedSetRangeByRankAsync(leaderboardKey, -3, -1);

        // Get player rank
        var player2Rank = await Database.SortedSetRankAsync(leaderboardKey, "player2");

        // Get player score
        var player2Score = await Database.SortedSetScoreAsync(leaderboardKey, "player2");

        // Assert
        top3.Should().Equal("player4", "player2", "player5"); // Ordered by score
        player2Rank.Should().Be(3); // 4th from bottom (0-indexed)
        player2Score.Should().Be(2000.0);

        // Cleanup
        await Database.KeyDeleteAsync(leaderboardKey);
    }

    [Fact]
    public async Task RateLimiting_Scenario()
    {
        // Scenario: Simple rate limiting using string with expiration
        var rateLimitKey = GetTestKey("ratelimit:user:123:api");

        // Act - Track API calls within time window
        var count1 = await Database!.StringIncrementAsync(rateLimitKey);
        if (count1 == 1)
        {
            await Database.KeyExpireAsync(rateLimitKey, TimeSpan.FromSeconds(60));
        }

        var count2 = await Database.StringIncrementAsync(rateLimitKey);
        var count3 = await Database.StringIncrementAsync(rateLimitKey);

        // Check if limit exceeded (example: max 5 calls per minute)
        var currentCount = long.Parse((await Database.StringGetAsync(rateLimitKey))!);
        var isAllowed = currentCount <= 5;

        // Assert
        count1.Should().Be(1);
        count2.Should().Be(2);
        count3.Should().Be(3);
        isAllowed.Should().BeTrue();

        // Cleanup
        await Database.KeyDeleteAsync(rateLimitKey);
    }

    [Fact]
    public async Task RecentItems_Scenario()
    {
        // Scenario: Recent items list (bounded) using list trim
        var recentKey = GetTestKey("recent:searches:user456");

        // Act - Add items, keeping only last 5
        for (int i = 1; i <= 10; i++)
        {
            await Database!.ListLeftPushAsync(recentKey, $"search_{i}");
            await Database.ListTrimAsync(recentKey, 0, 4); // Keep only 5 most recent
        }

        // Get recent items
        var recent = await Database!.ListRangeAsync(recentKey, 0, -1);

        // Assert
        recent.Should().HaveCount(5);
        recent.Should().Equal("search_10", "search_9", "search_8", "search_7", "search_6");

        // Cleanup
        await Database.KeyDeleteAsync(recentKey);
    }

    [Fact]
    public async Task CachePattern_Scenario()
    {
        // Scenario: Cache-aside pattern
        var cacheKey = GetTestKey("cache:product:123");

        // Act - Check cache
        var cached = await Database!.StringGetAsync(cacheKey);

        if (cached == null)
        {
            // Cache miss - simulate DB fetch and cache
            var dbValue = "Product Data from DB";
            await Database.StringSetAsync(cacheKey, dbValue);
            await Database.KeyExpireAsync(cacheKey, TimeSpan.FromMinutes(10));
            cached = dbValue;
        }

        // Second read - should hit cache
        var cached2 = await Database.StringGetAsync(cacheKey);

        // Assert
        cached.Should().Be("Product Data from DB");
        cached2.Should().Be(cached);
        (await Database.KeyExistsAsync(cacheKey)).Should().BeTrue();

        // Cleanup
        await Database.KeyDeleteAsync(cacheKey);
    }

    [Fact]
    public async Task MultiDataStructure_ComplexScenario()
    {
        // Scenario: Blog post with comments, tags, and views
        var postKey = GetTestKey("post:999");
        var commentsKey = GetTestKey("post:999:comments");
        var tagsKey = GetTestKey("post:999:tags");
        var viewsKey = GetTestKey("post:999:views");

        // Act - Create post metadata
        await Database!.HashSetAsync(postKey, new Dictionary<string, string>
        {
            { "title", "Getting Started with Valkey" },
            { "author", "Jane Doe" },
            { "published", "2025-01-15" }
        });

        // Add tags
        await Database.SetAddAsync(tagsKey, new[] { "valkey", "redis", "nosql", "database" });

        // Add comments (as a list)
        await Database.ListRightPushAsync(commentsKey, "Great article!");
        await Database.ListRightPushAsync(commentsKey, "Very helpful, thanks!");

        // Track views
        await Database.StringIncrementAsync(viewsKey, 42);

        // Read everything
        var postData = await Database.HashGetAllAsync(postKey);
        var tags = await Database.SetMembersAsync(tagsKey);
        var comments = await Database.ListRangeAsync(commentsKey, 0, -1);
        var views = await Database.StringGetAsync(viewsKey);

        // Assert
        postData["title"].Should().Be("Getting Started with Valkey");
        tags.Should().Contain("valkey", "redis");
        comments.Should().HaveCount(2);
        views.Should().Be("42");

        // Cleanup
        await Database.KeyDeleteAsync(new[] { postKey, commentsKey, tagsKey, viewsKey });
    }

    [Fact]
    public async Task ConcurrentUsers_Scenario()
    {
        // Scenario: Multiple concurrent users performing operations
        var tasks = new List<Task>();

        for (int userId = 1; userId <= 10; userId++)
        {
            var userTask = Task.Run(async () =>
            {
                var userKey = GetTestKey($"user:{userId}");
                var activityKey = GetTestKey($"activity:{userId}");

                // Each user creates their profile
                await Database!.HashSetAsync(userKey, new Dictionary<string, string>
                {
                    { "id", userId.ToString() },
                    { "name", $"User{userId}" },
                    { "status", "active" }
                });

                // Track activity
                await Database.ListRightPushAsync(activityKey, "login");
                await Database.ListRightPushAsync(activityKey, "view_page");
                await Database.ListRightPushAsync(activityKey, "logout");

                // Cleanup
                await Database.KeyDeleteAsync(userKey);
                await Database.KeyDeleteAsync(activityKey);
            });

            tasks.Add(userTask);
        }

        // Act & Assert - All tasks should complete without errors
        await Task.WhenAll(tasks);
        tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());
    }
}
