using FluentAssertions;
using Valkey.Protocol;

namespace Valkey.Tests.Protocol;

/// <summary>
/// Tests for RespValue struct and its factory methods.
/// </summary>
public class RespValueTests
{
    #region SimpleString Tests

    [Fact]
    public void SimpleString_CreatesCorrectValue()
    {
        // Arrange & Act
        var data = "OK"u8.ToArray();
        var value = RespValue.SimpleString(data);

        // Assert
        value.Type.Should().Be(RespType.SimpleString);
        value.IsNull.Should().BeFalse();
        value.TryGetString(out var str).Should().BeTrue();
        str.Should().Be("OK");
    }

    [Fact]
    public void SimpleString_AsString_ReturnsValue()
    {
        // Arrange
        var data = "PONG"u8.ToArray();
        var value = RespValue.SimpleString(data);

        // Act
        var result = value.AsString();

        // Assert
        result.Should().Be("PONG");
    }

    #endregion

    #region BulkString Tests

    [Fact]
    public void BulkString_CreatesCorrectValue()
    {
        // Arrange
        var data = "Hello World"u8.ToArray();

        // Act
        var value = RespValue.BulkString(data);

        // Assert
        value.Type.Should().Be(RespType.BulkString);
        value.IsNull.Should().BeFalse();
        value.TryGetBytes(out var bytes).Should().BeTrue();
        bytes.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public void BulkString_EmptyMemory_CreatesValue()
    {
        // Act
        var value = RespValue.BulkString(ReadOnlyMemory<byte>.Empty);

        // Assert
        value.Type.Should().Be(RespType.BulkString);
        value.IsNull.Should().BeFalse();
        value.TryGetBytes(out var bytes).Should().BeTrue();
        bytes.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void BulkString_AsBytes_ReturnsValue()
    {
        // Arrange
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
        var value = RespValue.BulkString(data);

        // Act
        var result = value.AsBytes();

        // Assert
        result.ToArray().Should().BeEquivalentTo(data);
    }

    #endregion

    #region Integer Tests

    [Fact]
    public void Integer_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Integer(42);

        // Assert
        value.Type.Should().Be(RespType.Integer);
        value.IsNull.Should().BeFalse();
        value.TryGetInteger(out var num).Should().BeTrue();
        num.Should().Be(42);
    }

    [Fact]
    public void Integer_Negative_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Integer(-100);

        // Assert
        value.TryGetInteger(out var num).Should().BeTrue();
        num.Should().Be(-100);
    }

    [Fact]
    public void Integer_Zero_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Integer(0);

        // Assert
        value.TryGetInteger(out var num).Should().BeTrue();
        num.Should().Be(0);
    }

    [Fact]
    public void Integer_AsInteger_ReturnsValue()
    {
        // Arrange
        var value = RespValue.Integer(12345);

        // Act
        var result = value.AsInteger();

        // Assert
        result.Should().Be(12345);
    }

    #endregion

    #region Double Tests

    [Fact]
    public void Double_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Double(Math.PI);

        // Assert
        value.Type.Should().Be(RespType.Double);
        value.IsNull.Should().BeFalse();
        value.TryGetDouble(out var num).Should().BeTrue();
        num.Should().BeApproximately(Math.PI, 0.00001);
    }

    [Fact]
    public void Double_Negative_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Double(-Math.E);

        // Assert
        value.TryGetDouble(out var num).Should().BeTrue();
        num.Should().BeApproximately(-Math.E, 0.00001);
    }

    [Fact]
    public void Double_Infinity_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Double(double.PositiveInfinity);

        // Assert
        value.TryGetDouble(out var num).Should().BeTrue();
        num.Should().Be(double.PositiveInfinity);
    }

    #endregion

    #region Boolean Tests

    [Fact]
    public void Boolean_True_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Boolean(true);

        // Assert
        value.Type.Should().Be(RespType.Boolean);
        value.IsNull.Should().BeFalse();
        value.TryGetBoolean(out var result).Should().BeTrue();
        result.Should().BeTrue();
    }

    [Fact]
    public void Boolean_False_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Boolean(false);

        // Assert
        value.TryGetBoolean(out var result).Should().BeTrue();
        result.Should().BeFalse();
    }

    #endregion

    #region Array Tests

    [Fact]
    public void Array_CreatesCorrectValue()
    {
        // Arrange
        var items = new[]
        {
            RespValue.SimpleString("OK"u8.ToArray()),
            RespValue.Integer(42),
            RespValue.BulkString("test"u8.ToArray())
        };

        // Act
        var value = RespValue.Array(items);

        // Assert
        value.Type.Should().Be(RespType.Array);
        value.IsNull.Should().BeFalse();
        value.TryGetArray(out var array).Should().BeTrue();
        array!.Should().HaveCount(3);
        array![0].AsString().Should().Be("OK");
        array[1].AsInteger().Should().Be(42);
    }

    [Fact]
    public void Array_Empty_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Array(Array.Empty<RespValue>());

        // Assert
        value.TryGetArray(out var array).Should().BeTrue();
        array.Should().BeEmpty();
    }

    [Fact]
    public void Array_Empty_IsNotNull()
    {
        // Act
        var value = RespValue.Array(Array.Empty<RespValue>());

        // Assert
        value.Type.Should().Be(RespType.Array);
        value.IsNull.Should().BeFalse();
        value.TryGetArray(out var array).Should().BeTrue();
        array.Should().NotBeNull();
    }

    [Fact]
    public void Array_AsArray_ReturnsValue()
    {
        // Arrange
        var items = new[]
        {
            RespValue.Integer(1),
            RespValue.Integer(2),
            RespValue.Integer(3)
        };
        var value = RespValue.Array(items);

        // Act
        var result = value.AsArray();

        // Assert
        result.Should().HaveCount(3);
        result[0].AsInteger().Should().Be(1);
        result[1].AsInteger().Should().Be(2);
        result[2].AsInteger().Should().Be(3);
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_CreatesCorrectValue()
    {
        // Arrange
        var map = new Dictionary<RespValue, RespValue>
        {
            [RespValue.SimpleString("name")] = RespValue.BulkString("Alice"u8.ToArray()),
            [RespValue.SimpleString("age")] = RespValue.Integer(30)
        };

        // Act
        var value = RespValue.Map(map);

        // Assert
        value.Type.Should().Be(RespType.Map);
        value.IsNull.Should().BeFalse();
        value.TryGetMap(out var resultMap).Should().BeTrue();
        resultMap.Should().HaveCount(2);
    }

    [Fact]
    public void Map_Empty_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Map(new Dictionary<RespValue, RespValue>());

        // Assert
        value.TryGetMap(out var map).Should().BeTrue();
        map.Should().BeEmpty();
    }

    #endregion

    #region Set Tests

    [Fact]
    public void Set_CreatesCorrectValue()
    {
        // Arrange
        var set = new HashSet<RespValue>
        {
            RespValue.SimpleString("apple"u8.ToArray()),
            RespValue.SimpleString("banana"u8.ToArray()),
            RespValue.SimpleString("cherry"u8.ToArray())
        };

        // Act
        var value = RespValue.Set(set);

        // Assert
        value.Type.Should().Be(RespType.Set);
        value.IsNull.Should().BeFalse();
        value.TryGetSet(out var resultSet).Should().BeTrue();
        resultSet!.Should().HaveCount(3);
    }

    [Fact]
    public void Set_Empty_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.Set(new HashSet<RespValue>());

        // Assert
        value.TryGetSet(out var set).Should().BeTrue();
        set.Should().BeEmpty();
    }

    #endregion

    #region Error Tests

    [Fact]
    public void SimpleError_CreatesCorrectValue()
    {
        // Act
        var value = RespValue.SimpleError("ERR something went wrong"u8.ToArray());

        // Assert
        value.Type.Should().Be(RespType.SimpleError);
        value.IsNull.Should().BeFalse();
        value.TryGetString(out var error).Should().BeTrue();
        error.Should().Be("ERR something went wrong");
    }

    [Fact]
    public void BulkError_CreatesCorrectValue()
    {
        // Arrange
        var errorData = "WRONGTYPE Operation against a key holding the wrong kind of value"u8.ToArray();

        // Act
        var value = RespValue.BulkError(errorData);

        // Assert
        value.Type.Should().Be(RespType.BulkError);
        value.IsNull.Should().BeFalse();
    }

    #endregion

    #region Type Conversion Tests

    [Fact]
    public void AsString_WrongType_ThrowsException()
    {
        // Arrange
        var value = RespValue.Integer(42);

        // Act & Assert
        var act = () => value.AsString();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AsInteger_WrongType_ThrowsException()
    {
        // Arrange
        var value = RespValue.SimpleString("OK");

        // Act & Assert
        var act = () => value.AsInteger();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AsArray_WrongType_ThrowsException()
    {
        // Arrange
        var value = RespValue.Integer(42);

        // Act & Assert
        var act = () => value.AsArray();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGet_WrongType_ReturnsFalse()
    {
        // Arrange
        var value = RespValue.Integer(42);

        // Act & Assert
        value.TryGetString(out _).Should().BeFalse();
        value.TryGetArray(out _).Should().BeFalse();
        value.TryGetMap(out _).Should().BeFalse();
        value.TryGetSet(out _).Should().BeFalse();
    }

    #endregion

    #region Null Tests

    [Fact]
    public void Null_IsNull_ReturnsTrue()
    {
        // Act
        var value = RespValue.Null;

        // Assert
        value.Type.Should().Be(RespType.Null);
        value.IsNull.Should().BeTrue();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameInteger_ReturnsTrue()
    {
        // Arrange
        var value1 = RespValue.Integer(42);
        var value2 = RespValue.Integer(42);

        // Act & Assert
        value1.Equals(value2).Should().BeTrue();
        (value1 == value2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentInteger_ReturnsFalse()
    {
        // Arrange
        var value1 = RespValue.Integer(42);
        var value2 = RespValue.Integer(43);

        // Act & Assert
        value1.Equals(value2).Should().BeFalse();
        (value1 != value2).Should().BeTrue();
    }

    [Fact]
    public void Equals_SameByteArray_UsesReferenceEquality()
    {
        // Arrange
        var data = "OK"u8.ToArray();
        var value1 = RespValue.SimpleString(data);
        var value2 = RespValue.SimpleString(data); // Same reference

        // Act & Assert
        value1.Equals(value2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentByteArray_ReturnsFalse()
    {
        // Arrange
        var value1 = RespValue.SimpleString("OK"u8.ToArray());
        var value2 = RespValue.SimpleString("OK"u8.ToArray()); // Different array instance

        // Act & Assert
        value1.Equals(value2).Should().BeFalse(); // Reference equality, not value equality
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        // Arrange
        var value1 = RespValue.Integer(42);
        var value2 = RespValue.SimpleString("42");

        // Act & Assert
        value1.Equals(value2).Should().BeFalse();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_SimpleString_ReturnsValue()
    {
        // Arrange
        var value = RespValue.SimpleString("OK"u8.ToArray());

        // Act
        var result = value.ToString();

        // Assert
        result.Should().Be("OK");
    }

    [Fact]
    public void ToString_Integer_ReturnsFormattedString()
    {
        // Arrange
        var value = RespValue.Integer(42);

        // Act
        var result = value.ToString();

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public void ToString_Null_ReturnsFormattedString()
    {
        // Arrange
        var value = RespValue.Null;

        // Act
        var result = value.ToString();

        // Assert
        result.Should().Contain("null");
    }

    #endregion
}
