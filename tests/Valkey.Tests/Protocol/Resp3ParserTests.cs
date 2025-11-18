using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using FluentAssertions;
using Valkey.Protocol;

namespace Valkey.Tests.Protocol;

/// <summary>
/// Tests for Resp3Parser protocol parsing.
/// </summary>
public class Resp3ParserTests
{
    private static async Task<RespValue> ParseAsync(string data)
    {
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(data));
        pipe.Writer.Complete();

        var parser = new Resp3Parser(pipe.Reader);
        return await parser.ReadAsync();
    }

    private static async Task<RespValue> ParseAsync(byte[] data)
    {
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(data);
        pipe.Writer.Complete();

        var parser = new Resp3Parser(pipe.Reader);
        return await parser.ReadAsync();
    }

    #region SimpleString Tests

    [Fact]
    public async Task Parse_SimpleString_OK_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync("+OK\r\n");

        // Assert
        result.Type.Should().Be(RespType.SimpleString);
        result.AsString().Should().Be("OK");
    }

    [Fact]
    public async Task Parse_SimpleString_PONG_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync("+PONG\r\n");

        // Assert
        result.Type.Should().Be(RespType.SimpleString);
        result.AsString().Should().Be("PONG");
    }

    [Fact]
    public async Task Parse_SimpleString_Empty_ReturnsEmpty()
    {
        // Arrange & Act
        var result = await ParseAsync("+\r\n");

        // Assert
        result.Type.Should().Be(RespType.SimpleString);
        result.AsString().Should().Be("");
    }

    [Fact]
    public async Task Parse_SimpleString_WithSpaces_PreservesSpaces()
    {
        // Arrange & Act
        var result = await ParseAsync("+Hello World\r\n");

        // Assert
        result.AsString().Should().Be("Hello World");
    }

    #endregion

    #region SimpleError Tests

    [Fact]
    public async Task Parse_SimpleError_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync("-ERR unknown command\r\n");

        // Assert
        result.Type.Should().Be(RespType.SimpleError);
        result.AsString().Should().Be("ERR unknown command");
    }

    [Fact]
    public async Task Parse_SimpleError_WRONGTYPE_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync("-WRONGTYPE Operation against a key holding the wrong kind of value\r\n");

        // Assert
        result.Type.Should().Be(RespType.SimpleError);
        result.AsString().Should().Contain("WRONGTYPE");
    }

    #endregion

    #region Integer Tests

    [Fact]
    public async Task Parse_Integer_Zero_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync(":0\r\n");

        // Assert
        result.Type.Should().Be(RespType.Integer);
        result.AsInteger().Should().Be(0);
    }

    [Fact]
    public async Task Parse_Integer_Positive_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync(":42\r\n");

        // Assert
        result.Type.Should().Be(RespType.Integer);
        result.AsInteger().Should().Be(42);
    }

    [Fact]
    public async Task Parse_Integer_Negative_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync(":-100\r\n");

        // Assert
        result.Type.Should().Be(RespType.Integer);
        result.AsInteger().Should().Be(-100);
    }

    [Fact]
    public async Task Parse_Integer_Large_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync(":9223372036854775807\r\n"); // Max long

        // Assert
        result.Type.Should().Be(RespType.Integer);
        result.AsInteger().Should().Be(long.MaxValue);
    }

    #endregion

    #region BulkString Tests

    [Fact]
    public async Task Parse_BulkString_Simple_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync("$5\r\nHello\r\n");

        // Assert
        result.Type.Should().Be(RespType.BulkString);
        result.AsString().Should().Be("Hello");
    }

    [Fact]
    public async Task Parse_BulkString_Empty_ReturnsEmpty()
    {
        // Arrange & Act
        var result = await ParseAsync("$0\r\n\r\n");

        // Assert
        result.Type.Should().Be(RespType.BulkString);
        result.AsString().Should().Be("");
    }

    [Fact]
    public async Task Parse_BulkString_Null_ReturnsNull()
    {
        // Arrange & Act
        var result = await ParseAsync("$-1\r\n");

        // Assert
        result.Type.Should().Be(RespType.Null);
        result.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task Parse_BulkString_WithNewlines_PreservesContent()
    {
        // Arrange
        // "Hello\r\nWorld" = H e l l o \r \n W o r l d = 12 bytes
        var data = "$12\r\nHello\r\nWorld\r\n";

        // Act
        var result = await ParseAsync(data);

        // Assert
        result.AsString().Should().Be("Hello\r\nWorld");
    }

    [Fact]
    public async Task Parse_BulkString_Binary_PreservesBinaryData()
    {
        // Arrange
        var binaryData = new byte[] { 0x00, 0x01, 0x02, 0xFF };
        var data = Encoding.UTF8.GetBytes($"${binaryData.Length}\r\n")
            .Concat(binaryData)
            .Concat(Encoding.UTF8.GetBytes("\r\n"))
            .ToArray();

        // Act
        var result = await ParseAsync(data);

        // Assert
        result.Type.Should().Be(RespType.BulkString);
        result.AsBytes().ToArray().Should().Equal(binaryData);
    }

    #endregion

    #region Array Tests

    [Fact]
    public async Task Parse_Array_Empty_ReturnsEmpty()
    {
        // Arrange & Act
        var result = await ParseAsync("*0\r\n");

        // Assert
        result.Type.Should().Be(RespType.Array);
        result.AsArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Parse_Array_Null_ReturnsNull()
    {
        // Arrange & Act
        var result = await ParseAsync("*-1\r\n");

        // Assert
        result.Type.Should().Be(RespType.Null);
        result.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task Parse_Array_MixedTypes_ReturnsCorrectValues()
    {
        // Arrange & Act
        var result = await ParseAsync("*3\r\n+OK\r\n:42\r\n$5\r\nHello\r\n");

        // Assert
        result.Type.Should().Be(RespType.Array);
        var array = result.AsArray();
        array.Should().HaveCount(3);
        array[0].AsString().Should().Be("OK");
        array[1].AsInteger().Should().Be(42);
        array[2].AsString().Should().Be("Hello");
    }

    [Fact]
    public async Task Parse_Array_Nested_ReturnsCorrectStructure()
    {
        // Arrange & Act
        var result = await ParseAsync("*2\r\n*2\r\n:1\r\n:2\r\n*2\r\n:3\r\n:4\r\n");

        // Assert
        result.Type.Should().Be(RespType.Array);
        var array = result.AsArray();
        array.Should().HaveCount(2);

        array[0].AsArray()[0].AsInteger().Should().Be(1);
        array[0].AsArray()[1].AsInteger().Should().Be(2);
        array[1].AsArray()[0].AsInteger().Should().Be(3);
        array[1].AsArray()[1].AsInteger().Should().Be(4);
    }

    [Fact]
    public async Task Parse_Array_WithNullElement_HandlesNull()
    {
        // Arrange & Act
        var result = await ParseAsync("*3\r\n+OK\r\n$-1\r\n:42\r\n");

        // Assert
        var array = result.AsArray();
        array.Should().HaveCount(3);
        array[0].AsString().Should().Be("OK");
        array[1].IsNull.Should().BeTrue();
        array[2].AsInteger().Should().Be(42);
    }

    #endregion

    #region Null Tests

    [Fact]
    public async Task Parse_Null_ReturnsNullValue()
    {
        // Arrange & Act
        var result = await ParseAsync("_\r\n");

        // Assert
        result.Type.Should().Be(RespType.Null);
        result.IsNull.Should().BeTrue();
    }

    #endregion

    #region Boolean Tests

    [Fact]
    public async Task Parse_Boolean_True_ReturnsTrue()
    {
        // Arrange & Act
        var result = await ParseAsync("#t\r\n");

        // Assert
        result.Type.Should().Be(RespType.Boolean);
        result.TryGetBoolean(out var value).Should().BeTrue();
        value.Should().BeTrue();
    }

    [Fact]
    public async Task Parse_Boolean_False_ReturnsFalse()
    {
        // Arrange & Act
        var result = await ParseAsync("#f\r\n");

        // Assert
        result.Type.Should().Be(RespType.Boolean);
        result.TryGetBoolean(out var value).Should().BeTrue();
        value.Should().BeFalse();
    }

    #endregion

    #region Double Tests

    [Fact]
    public async Task Parse_Double_Positive_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync(",3.14159\r\n");

        // Assert
        result.Type.Should().Be(RespType.Double);
        result.TryGetDouble(out var value).Should().BeTrue();
        value.Should().BeApproximately(Math.PI, 0.00001);
    }

    [Fact]
    public async Task Parse_Double_Negative_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync(",-2.71828\r\n");

        // Assert
        result.TryGetDouble(out var value).Should().BeTrue();
        value.Should().BeApproximately(-Math.E, 0.00001);
    }

    [Fact]
    public async Task Parse_Double_Infinity_ReturnsInfinity()
    {
        // Arrange & Act
        var result = await ParseAsync(",inf\r\n");

        // Assert
        result.TryGetDouble(out var value).Should().BeTrue();
        value.Should().Be(double.PositiveInfinity);
    }

    [Fact]
    public async Task Parse_Double_NegativeInfinity_ReturnsNegativeInfinity()
    {
        // Arrange & Act
        var result = await ParseAsync(",-inf\r\n");

        // Assert
        result.TryGetDouble(out var value).Should().BeTrue();
        value.Should().Be(double.NegativeInfinity);
    }

    #endregion

    #region BulkError Tests

    [Fact]
    public async Task Parse_BulkError_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync("!21\r\nSYNTAX invalid syntax\r\n");

        // Assert
        result.Type.Should().Be(RespType.BulkError);
        result.AsString().Should().Be("SYNTAX invalid syntax");
    }

    [Fact]
    public async Task Parse_BulkError_Null_ReturnsNull()
    {
        // Arrange & Act
        var result = await ParseAsync("!-1\r\n");

        // Assert
        result.Type.Should().Be(RespType.Null);
        result.IsNull.Should().BeTrue();
    }

    #endregion

    #region Map Tests

    [Fact]
    public async Task Parse_Map_Empty_ReturnsEmpty()
    {
        // Arrange & Act
        var result = await ParseAsync("%0\r\n");

        // Assert
        result.Type.Should().Be(RespType.Map);
        result.TryGetMap(out var map).Should().BeTrue();
        map!.Should().BeEmpty();
    }

    [Fact]
    public async Task Parse_Map_SingleEntry_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync("%1\r\n+key\r\n+value\r\n");

        // Assert
        result.Type.Should().Be(RespType.Map);
        result.TryGetMap(out var map).Should().BeTrue();
        map!.Should().HaveCount(1);

        var key = map!.Keys.First();
        key.AsString().Should().Be("key");
        map[key].AsString().Should().Be("value");
    }

    [Fact]
    public async Task Parse_Map_MultipleEntries_ReturnsCorrectValues()
    {
        // Arrange & Act
        var result = await ParseAsync("%2\r\n+name\r\n$5\r\nAlice\r\n+age\r\n:30\r\n");

        // Assert
        result.TryGetMap(out var map).Should().BeTrue();
        map!.Should().HaveCount(2);
    }

    #endregion

    #region Set Tests

    [Fact]
    public async Task Parse_Set_Empty_ReturnsEmpty()
    {
        // Arrange & Act
        var result = await ParseAsync("~0\r\n");

        // Assert
        result.Type.Should().Be(RespType.Set);
        result.TryGetSet(out var set).Should().BeTrue();
        set!.Should().BeEmpty();
    }

    [Fact]
    public async Task Parse_Set_MultipleElements_ReturnsCorrectValues()
    {
        // Arrange & Act
        var result = await ParseAsync("~3\r\n+apple\r\n+banana\r\n+cherry\r\n");

        // Assert
        result.Type.Should().Be(RespType.Set);
        result.TryGetSet(out var set).Should().BeTrue();
        set!.Should().HaveCount(3);
    }

    #endregion

    #region Push Tests

    [Fact]
    public async Task Parse_Push_ReturnsCorrectValue()
    {
        // Arrange & Act
        var result = await ParseAsync(">2\r\n+message\r\n$5\r\nHello\r\n");

        // Assert
        result.Type.Should().Be(RespType.Push);
        result.TryGetArray(out var array).Should().BeTrue();
        array!.Should().HaveCount(2);
        array![0].AsString().Should().Be("message");
        array[1].AsString().Should().Be("Hello");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Parse_UnknownType_ThrowsException()
    {
        // Arrange & Act & Assert
        var act = async () => await ParseAsync("?unknown\r\n");
        await act.Should().ThrowAsync<RespProtocolException>()
            .WithMessage("*Unknown RESP type*");
    }

    [Fact]
    public async Task Parse_InvalidInteger_ThrowsException()
    {
        // Arrange & Act & Assert
        var act = async () => await ParseAsync(":notanumber\r\n");
        await act.Should().ThrowAsync<RespProtocolException>();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Parse_LargeArray_HandlesCorrectly()
    {
        // Arrange
        var sb = new StringBuilder();
        sb.Append("*100\r\n");
        for (int i = 0; i < 100; i++)
        {
            sb.Append($":{i}\r\n");
        }

        // Act
        var result = await ParseAsync(sb.ToString());

        // Assert
        result.AsArray().Should().HaveCount(100);
        result.AsArray()[99].AsInteger().Should().Be(99);
    }

    [Fact]
    public async Task Parse_DeeplyNestedArray_HandlesCorrectly()
    {
        // Arrange - Create [[[[42]]]]
        var data = "*1\r\n*1\r\n*1\r\n*1\r\n:42\r\n";

        // Act
        var result = await ParseAsync(data);

        // Assert
        var innerMost = result.AsArray()[0]
            .AsArray()[0]
            .AsArray()[0]
            .AsArray()[0];
        innerMost.AsInteger().Should().Be(42);
    }

    [Fact]
    public async Task Parse_EmptyBulkString_DistinguishesFromNull()
    {
        // Arrange & Act
        var emptyResult = await ParseAsync("$0\r\n\r\n");
        var nullResult = await ParseAsync("$-1\r\n");

        // Assert
        emptyResult.IsNull.Should().BeFalse();
        emptyResult.AsString().Should().Be("");

        nullResult.IsNull.Should().BeTrue();
    }

    #endregion

    #region Multiple Values

    [Fact]
    public async Task Parse_MultipleValues_ReadsInSequence()
    {
        // Arrange
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("+OK\r\n:42\r\n$5\r\nHello\r\n"));
        pipe.Writer.Complete();

        var parser = new Resp3Parser(pipe.Reader);

        // Act
        var value1 = await parser.ReadAsync();
        var value2 = await parser.ReadAsync();
        var value3 = await parser.ReadAsync();

        // Assert
        value1.AsString().Should().Be("OK");
        value2.AsInteger().Should().Be(42);
        value3.AsString().Should().Be("Hello");
    }

    #endregion
}
