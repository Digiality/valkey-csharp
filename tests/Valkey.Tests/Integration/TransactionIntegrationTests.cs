using FluentAssertions;
using Valkey;
using Valkey.Configuration;
using Valkey.Protocol;
using Valkey.Transactions;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for transaction (MULTI/EXEC) commands against a real Valkey/Redis server.
/// </summary>
public class TransactionIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task Transaction_StringSet_ExecutesSuccessfully()
    {
        // Arrange
        var key = GetTestKey();
        var value = "test_value";
        var transaction = Database!.CreateTransaction();

        // Act
        transaction.StringSet(key, value);
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Type.Should().Be(RespType.SimpleString);

        // Verify the value was set
        var actualValue = await Database.StringGetAsync(key);
        actualValue.Should().Be(value);
    }

    [Fact]
    public async Task Transaction_MultipleCommands_AllExecuted()
    {
        // Arrange
        var key1 = GetTestKey("key1");
        var key2 = GetTestKey("key2");
        var transaction = Database!.CreateTransaction();

        // Act
        transaction.StringSet(key1, "value1");
        transaction.StringSet(key2, "value2");
        transaction.StringIncrement(key1); // This will fail but the transaction should still execute
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(3);

        // Verify the values
        var value1 = await Database.StringGetAsync(key1);
        var value2 = await Database.StringGetAsync(key2);
        value1.Should().NotBeNull(); // value1 is set but increment may have error
        value2.Should().Be("value2");
    }

    [Fact]
    public async Task Transaction_StringIncrementAndGet_WorksCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "0");
        var transaction = Database.CreateTransaction();

        // Act
        transaction.StringIncrement(key);
        transaction.StringIncrement(key);
        transaction.StringIncrement(key);
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].TryGetInteger(out var result1).Should().BeTrue();
        result1.Should().Be(1);
        results[1].TryGetInteger(out var result2).Should().BeTrue();
        result2.Should().Be(2);
        results[2].TryGetInteger(out var result3).Should().BeTrue();
        result3.Should().Be(3);

        // Verify final value
        var finalValue = await Database.StringGetAsync(key);
        finalValue.Should().Be("3");
    }

    [Fact]
    public async Task Transaction_HashOperations_ExecuteAtomically()
    {
        // Arrange
        var key = GetTestKey();
        var transaction = Database!.CreateTransaction();

        // Act
        transaction.HashSet(key, "field1", "value1");
        transaction.HashSet(key, "field2", "value2");
        transaction.HashGet(key, "field1");
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].TryGetInteger(out var hset1Result).Should().BeTrue();
        hset1Result.Should().Be(1); // First HSET returns 1 (new field)
        results[1].TryGetInteger(out var hset2Result).Should().BeTrue();
        hset2Result.Should().Be(1); // Second HSET returns 1 (new field)

        // Verify hash values
        var actualValue1 = await Database.HashGetAsync(key, "field1");
        var actualValue2 = await Database.HashGetAsync(key, "field2");
        actualValue1.Should().Be("value1");
        actualValue2.Should().Be("value2");
    }

    [Fact]
    public async Task Transaction_ListOperations_ExecuteInOrder()
    {
        // Arrange
        var key = GetTestKey();
        var transaction = Database!.CreateTransaction();

        // Act
        transaction.ListLeftPush(key, "first");
        transaction.ListLeftPush(key, "second");
        transaction.ListLeftPush(key, "third");
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].TryGetInteger(out var push1).Should().BeTrue();
        push1.Should().Be(1);
        results[1].TryGetInteger(out var push2).Should().BeTrue();
        push2.Should().Be(2);
        results[2].TryGetInteger(out var push3).Should().BeTrue();
        push3.Should().Be(3);
    }

    [Fact]
    public async Task Transaction_SetOperations_WorkCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        var transaction = Database!.CreateTransaction();

        // Act
        transaction.SetAdd(key, "member1");
        transaction.SetAdd(key, "member2");
        transaction.SetContains(key, "member1");
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].TryGetInteger(out var add1).Should().BeTrue();
        add1.Should().Be(1); // New member added
        results[1].TryGetInteger(out var add2).Should().BeTrue();
        add2.Should().Be(1); // New member added
    }

    [Fact]
    public async Task Transaction_SortedSetOperations_WorkCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        var transaction = Database!.CreateTransaction();

        // Act
        transaction.SortedSetAdd(key, "member1", 1.0);
        transaction.SortedSetAdd(key, "member2", 2.5);
        transaction.SortedSetScore(key, "member1");
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].TryGetInteger(out var add1).Should().BeTrue();
        add1.Should().Be(1); // New member added
        results[1].TryGetInteger(out var add2).Should().BeTrue();
        add2.Should().Be(1); // New member added
    }

    [Fact]
    public async Task Transaction_KeyOperations_WorkCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "value");
        var transaction = Database.CreateTransaction();

        // Act
        transaction.KeyExists(key);
        transaction.KeyExpire(key, TimeSpan.FromSeconds(60));
        transaction.KeyDelete(key);
        transaction.KeyExists(key);
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(4);
        results[0].TryGetInteger(out var exists1).Should().BeTrue();
        exists1.Should().Be(1); // Key exists
        results[1].TryGetInteger(out var expire).Should().BeTrue();
        expire.Should().Be(1); // Expire succeeded
        results[2].TryGetInteger(out var delete).Should().BeTrue();
        delete.Should().Be(1); // Delete succeeded
        results[3].TryGetInteger(out var exists2).Should().BeTrue();
        exists2.Should().Be(0); // Key no longer exists
    }

    [Fact]
    public async Task Transaction_EmptyTransaction_ReturnsEmptyArray()
    {
        // Arrange
        var transaction = Database!.CreateTransaction();

        // Act
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Transaction_CannotExecuteTwice_ThrowsException()
    {
        // Arrange
        var transaction = Database!.CreateTransaction();
        transaction.StringSet(GetTestKey(), "value");
        await transaction.ExecuteAsync();

        // Act & Assert
        var act = async () => await transaction.ExecuteAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction has already been executed");
    }

    [Fact]
    public async Task Transaction_CannotQueueAfterExecute_ThrowsException()
    {
        // Arrange
        var transaction = Database!.CreateTransaction();
        transaction.StringSet(GetTestKey(), "value");
        await transaction.ExecuteAsync();

        // Act & Assert
        var act = () => transaction.StringSet(GetTestKey(), "another_value");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transaction has already been executed");
    }

    [Fact]
    public async Task Transaction_FluentAPI_ChainsCorrectly()
    {
        // Arrange
        var key1 = GetTestKey("key1");
        var key2 = GetTestKey("key2");
        var key3 = GetTestKey("key3");

        // Act
        var results = await Database!.CreateTransaction()
            .StringSet(key1, "value1")
            .StringSet(key2, "value2")
            .StringSet(key3, "value3")
            .ExecuteAsync();

        // Assert
        results.Should().HaveCount(3);

        // Verify all values
        (await Database.StringGetAsync(key1)).Should().Be("value1");
        (await Database.StringGetAsync(key2)).Should().Be("value2");
        (await Database.StringGetAsync(key3)).Should().Be("value3");
    }

    [Fact]
    public async Task Transaction_MixedDataTypes_AllExecuted()
    {
        // Arrange
        var stringKey = GetTestKey("string");
        var hashKey = GetTestKey("hash");
        var listKey = GetTestKey("list");
        var setKey = GetTestKey("set");
        var zsetKey = GetTestKey("zset");

        var transaction = Database!.CreateTransaction();

        // Act
        transaction.StringSet(stringKey, "stringValue");
        transaction.HashSet(hashKey, "field", "hashValue");
        transaction.ListLeftPush(listKey, "listValue");
        transaction.SetAdd(setKey, "setValue");
        transaction.SortedSetAdd(zsetKey, "zsetMember", 1.0);
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(5);

        // Verify all values
        (await Database.StringGetAsync(stringKey)).Should().Be("stringValue");
        (await Database.HashGetAsync(hashKey, "field")).Should().Be("hashValue");
        (await Database.ListLeftPopAsync(listKey)).Should().Be("listValue");
        (await Database.SetContainsAsync(setKey, "setValue")).Should().BeTrue();
        (await Database.SortedSetScoreAsync(zsetKey, "zsetMember")).Should().Be(1.0);
    }

    [Fact]
    public async Task Transaction_StringSetWithExpiry_WorksCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        var transaction = Database!.CreateTransaction();

        // Act
        transaction.StringSet(key, "value", TimeSpan.FromSeconds(60));
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Type.Should().Be(RespType.SimpleString);

        // Verify the value was set
        var actualValue = await Database.StringGetAsync(key);
        actualValue.Should().Be("value");
    }

    [Fact]
    public async Task Transaction_WithGetCommand_ReturnsQueuedResponse()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "test_value");
        var transaction = Database.CreateTransaction();

        // Act
        transaction.StringGet(key);
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].TryGetString(out var value).Should().BeTrue();
        value.Should().Be("test_value");
    }

    [Fact]
    public async Task Transaction_ComplexScenario_AllOperationsAtomic()
    {
        // Arrange
        var counterKey = GetTestKey("counter");
        var userKey = GetTestKey("user");
        await Database!.StringSetAsync(counterKey, "0");

        var transaction = Database.CreateTransaction();

        // Act - Simulate a complex atomic operation
        transaction.StringIncrement(counterKey); // Increment counter
        transaction.HashSet(userKey, "id", "123");
        transaction.HashSet(userKey, "name", "Alice");
        transaction.HashSet(userKey, "score", "100");
        transaction.StringGet(counterKey); // Get new counter value

        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(5);

        // Counter was incremented
        results[0].TryGetInteger(out var newCounter).Should().BeTrue();
        newCounter.Should().Be(1);

        // Verify hash was set
        var userId = await Database.HashGetAsync(userKey, "id");
        var userName = await Database.HashGetAsync(userKey, "name");
        var userScore = await Database.HashGetAsync(userKey, "score");

        userId.Should().Be("123");
        userName.Should().Be("Alice");
        userScore.Should().Be("100");

        // Verify counter value was returned
        results[4].TryGetString(out var counterValue).Should().BeTrue();
        counterValue.Should().Be("1");
    }

    [Fact]
    public async Task Transaction_StringIncrementBy_WorksCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "10");
        var transaction = Database.CreateTransaction();

        // Act
        transaction.StringIncrement(key, 5);
        transaction.StringIncrement(key, 3);
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(2);
        results[0].TryGetInteger(out var result1).Should().BeTrue();
        result1.Should().Be(15);
        results[1].TryGetInteger(out var result2).Should().BeTrue();
        result2.Should().Be(18);

        // Verify final value
        var finalValue = await Database.StringGetAsync(key);
        finalValue.Should().Be("18");
    }

    [Fact]
    public async Task Transaction_StringDecrementBy_WorksCorrectly()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.StringSetAsync(key, "20");
        var transaction = Database.CreateTransaction();

        // Act
        transaction.StringDecrement(key, 3);
        transaction.StringDecrement(key, 7);
        var results = await transaction.ExecuteAsync();

        // Assert
        results.Should().HaveCount(2);
        results[0].TryGetInteger(out var result1).Should().BeTrue();
        result1.Should().Be(17);
        results[1].TryGetInteger(out var result2).Should().BeTrue();
        result2.Should().Be(10);

        // Verify final value
        var finalValue = await Database.StringGetAsync(key);
        finalValue.Should().Be("10");
    }
}
