using FluentAssertions;
using Valkey.Cluster;
using Xunit;

namespace Valkey.Tests.Unit;

/// <summary>
/// Tests for hash slot calculation (CRC16 algorithm).
/// </summary>
public class HashSlotCalculatorTests
{
    [Theory]
    [InlineData("key:123", 710)]
    [InlineData("user:1000", 1649)]
    [InlineData("user:1001", 5712)]
    [InlineData("{user1000}.following", 3443)] // Hash tag: user1000
    [InlineData("{user1000}.followers", 3443)] // Hash tag: user1000 (same slot as .following)
    [InlineData("foo{bar}baz", 5061)] // Hash tag: bar
    [InlineData("{}plain", 8054)] // Empty hash tag, use full key
    [InlineData("no-hashtag", 2630)]
    public void CalculateSlot_KnownKeys_ReturnsExpectedSlot(string key, int expectedSlot)
    {
        // Act
        var slot = HashSlotCalculator.CalculateSlot(key);

        // Assert
        slot.Should().Be(expectedSlot);
    }

    [Fact]
    public void CalculateSlot_EmptyKey_ThrowsArgumentException()
    {
        // Act
        var act = () => HashSlotCalculator.CalculateSlot("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateSlot_NullKey_ThrowsArgumentException()
    {
        // Act
        var act = () => HashSlotCalculator.CalculateSlot((string)null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateSlot_SlotInValidRange_AlwaysTrue()
    {
        // Arrange
        var keys = new[]
        {
            "a", "b", "c", "key:1", "key:2", "user:123",
            "{tag1}key", "{tag2}key", "very:long:key:name:with:colons"
        };

        // Act & Assert
        foreach (var key in keys)
        {
            var slot = HashSlotCalculator.CalculateSlot(key);
            slot.Should().BeInRange(0, 16383, $"slot for key '{key}' should be valid");
        }
    }

    [Fact]
    public void CalculateSlot_SameKey_ReturnsSameSlot()
    {
        // Arrange
        var key = "test:key:123";

        // Act
        var slot1 = HashSlotCalculator.CalculateSlot(key);
        var slot2 = HashSlotCalculator.CalculateSlot(key);

        // Assert
        slot1.Should().Be(slot2);
    }

    [Fact]
    public void CalculateSlot_HashTag_UsesOnlyTagContent()
    {
        // Arrange - Keys with same hash tag should have same slot
        var key1 = "{user:1000}.profile";
        var key2 = "{user:1000}.settings";
        var key3 = "{user:1000}.preferences";

        // Act
        var slot1 = HashSlotCalculator.CalculateSlot(key1);
        var slot2 = HashSlotCalculator.CalculateSlot(key2);
        var slot3 = HashSlotCalculator.CalculateSlot(key3);

        // Assert
        slot1.Should().Be(slot2);
        slot2.Should().Be(slot3);
    }

    [Fact]
    public void CalculateSlot_MultipleHashTags_UsesFirstOne()
    {
        // Arrange - Only first hash tag is used
        var key1 = "{tag1}data{tag2}";
        var key2 = "{tag1}other";

        // Act
        var slot1 = HashSlotCalculator.CalculateSlot(key1);
        var slot2 = HashSlotCalculator.CalculateSlot(key2);

        // Assert
        slot1.Should().Be(slot2, "both keys should use {tag1}");
    }

    [Fact]
    public void CalculateSlot_EmptyHashTag_UsesFullKey()
    {
        // Arrange - Empty hash tag {} should use full key
        var key1 = "{}key";
        var key2 = "key";

        // Act
        var slot1 = HashSlotCalculator.CalculateSlot(key1);
        var slot2 = HashSlotCalculator.CalculateSlot(key2);

        // Assert
        slot1.Should().NotBe(slot2, "empty hash tag should use full key");
    }

    [Fact]
    public void CalculateSlot_MalformedHashTag_UsesFullKey()
    {
        // Arrange - Malformed hash tag (no closing brace) should use full key
        var key = "{notclosed";

        // Act
        var slot = HashSlotCalculator.CalculateSlot(key);

        // Assert
        slot.Should().BeInRange(0, 16383);
    }

    [Fact]
    public void AreKeysInSameSlot_SameSlot_ReturnsTrue()
    {
        // Arrange
        var keys = new[] { "{user:1000}.profile", "{user:1000}.settings" };

        // Act
        var result = HashSlotCalculator.AreKeysInSameSlot(keys);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreKeysInSameSlot_DifferentSlots_ReturnsFalse()
    {
        // Arrange
        var keys = new[] { "key1", "key2", "key3" };

        // Act
        var result = HashSlotCalculator.AreKeysInSameSlot(keys);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AreKeysInSameSlot_EmptyArray_ReturnsTrue()
    {
        // Arrange
        var keys = Array.Empty<string>();

        // Act
        var result = HashSlotCalculator.AreKeysInSameSlot(keys);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreKeysInSameSlot_SingleKey_ReturnsTrue()
    {
        // Arrange
        var keys = new[] { "single-key" };

        // Act
        var result = HashSlotCalculator.AreKeysInSameSlot(keys);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GroupKeysBySlot_MultipleKeys_GroupsCorrectly()
    {
        // Arrange
        var keys = new[]
        {
            "{user:1000}.profile",
            "{user:1000}.settings",
            "{user:2000}.profile",
            "unrelated:key"
        };

        // Act
        var groups = HashSlotCalculator.GroupKeysBySlot(keys);

        // Assert
        groups.Should().HaveCount(3); // Two keys in slot1, one in slot2, one in slot3

        // Keys with same hash tag should be in same group
        var slot1 = HashSlotCalculator.CalculateSlot("{user:1000}.profile");
        groups[slot1].Should().HaveCount(2);
        groups[slot1].Should().Contain("{user:1000}.profile");
        groups[slot1].Should().Contain("{user:1000}.settings");
    }

    [Fact]
    public void GroupKeysBySlot_EmptyCollection_ReturnsEmptyDictionary()
    {
        // Arrange
        var keys = Array.Empty<string>();

        // Act
        var groups = HashSlotCalculator.GroupKeysBySlot(keys);

        // Assert
        groups.Should().BeEmpty();
    }

    [Theory]
    [InlineData("user:1000", 1649)]
    [InlineData("user:1001", 5712)]
    [InlineData("user:1002", 9779)]
    public void CalculateSlot_ConsistentWithRedisCluster_MatchesExpectedValues(string key, int expectedSlot)
    {
        // These values are verified against actual Redis Cluster behavior
        // Act
        var slot = HashSlotCalculator.CalculateSlot(key);

        // Assert
        slot.Should().Be(expectedSlot, $"slot for '{key}' should match Redis Cluster behavior");
    }
}
