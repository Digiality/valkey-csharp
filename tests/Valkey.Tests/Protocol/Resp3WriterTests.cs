using FluentAssertions;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Valkey.Protocol;

namespace Valkey.Tests.Protocol;

/// <summary>
/// Tests for Resp3Writer and its protocol serialization methods.
/// </summary>
public class Resp3WriterTests
{
    #region Helper Methods

    /// <summary>
    /// Helper method to write data and capture the output.
    /// </summary>
    private static async Task<byte[]> WriteAndCaptureAsync(Action<Resp3Writer> writeAction)
    {
        var pipe = new Pipe();
        var writer = new Resp3Writer(pipe.Writer);

        writeAction(writer);
        await writer.FlushAsync();
        pipe.Writer.Complete();

        var result = await ReadAllAsync(pipe.Reader);
        return result;
    }

    /// <summary>
    /// Helper method to read all data from a PipeReader.
    /// </summary>
    private static async Task<byte[]> ReadAllAsync(PipeReader reader)
    {
        var result = await reader.ReadAsync();
        var buffer = result.Buffer;
        var data = ToByteArray(buffer);
        reader.AdvanceTo(buffer.End);
        reader.Complete();
        return data;
    }

    /// <summary>
    /// Helper method to convert ReadOnlySequence to byte array.
    /// </summary>
    private static byte[] ToByteArray(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
        {
            return sequence.FirstSpan.ToArray();
        }

        var result = new byte[sequence.Length];
        sequence.CopyTo(result);
        return result;
    }

    #endregion

    #region SimpleString Tests

    [Fact]
    public async Task WriteSimpleString_ValidString_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteSimpleString("OK"u8));

        // Assert
        output.Should().Equal("+OK\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteSimpleString_EmptyString_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteSimpleString(""u8));

        // Assert
        output.Should().Equal("+\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteSimpleString_LongString_ProducesCorrectOutput()
    {
        // Arrange
        var longString = "The quick brown fox jumps over the lazy dog";

        // Act
        var output = await WriteAndCaptureAsync(w => w.WriteSimpleString(Encoding.UTF8.GetBytes(longString)));

        // Assert
        var expected = $"+{longString}\r\n";
        output.Should().Equal(Encoding.UTF8.GetBytes(expected));
    }

    #endregion

    #region SimpleError Tests

    [Fact]
    public async Task WriteSimpleError_ValidError_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteSimpleError("ERR something went wrong"u8));

        // Assert
        output.Should().Equal("-ERR something went wrong\r\n"u8.ToArray());
    }

    #endregion

    #region Integer Tests

    [Fact]
    public async Task WriteInteger_PositiveInteger_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteInteger(42));

        // Assert
        output.Should().Equal(":42\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteInteger_NegativeInteger_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteInteger(-100));

        // Assert
        output.Should().Equal(":-100\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteInteger_Zero_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteInteger(0));

        // Assert
        output.Should().Equal(":0\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteInteger_LongMaxValue_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteInteger(long.MaxValue));

        // Assert
        output.Should().Equal(":9223372036854775807\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteInteger_LongMinValue_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteInteger(long.MinValue));

        // Assert
        output.Should().Equal(":-9223372036854775808\r\n"u8.ToArray());
    }

    #endregion

    #region Boolean Tests

    [Fact]
    public async Task WriteBoolean_True_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteBoolean(true));

        // Assert
        output.Should().Equal("#t\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteBoolean_False_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteBoolean(false));

        // Assert
        output.Should().Equal("#f\r\n"u8.ToArray());
    }

    #endregion

    #region Double Tests

    [Fact]
    public async Task WriteDouble_PositiveDouble_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteDouble(Math.PI));

        // Assert
        var outputStr = Encoding.UTF8.GetString(output);
        outputStr.Should().StartWith(",");
        outputStr.Should().EndWith("\r\n");
        // G17 format preserves full precision - actual value will be more precise
        var expected = Math.PI.ToString("G17");
        outputStr.Should().Be($",{expected}\r\n");
    }

    [Fact]
    public async Task WriteDouble_NegativeDouble_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteDouble(-Math.E));

        // Assert
        var outputStr = Encoding.UTF8.GetString(output);
        outputStr.Should().StartWith(",");
        outputStr.Should().EndWith("\r\n");
        outputStr.Should().Contain("-2.71828");
    }

    [Fact]
    public async Task WriteDouble_PositiveInfinity_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteDouble(double.PositiveInfinity));

        // Assert
        output.Should().Equal(",inf\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteDouble_NegativeInfinity_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteDouble(double.NegativeInfinity));

        // Assert
        output.Should().Equal(",-inf\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteDouble_Zero_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteDouble(0.0));

        // Assert
        output.Should().Equal(",0\r\n"u8.ToArray());
    }

    #endregion

    #region Null Tests

    [Fact]
    public async Task WriteNull_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteNull());

        // Assert
        output.Should().Equal("_\r\n"u8.ToArray());
    }

    #endregion

    #region BulkString Tests

    [Fact]
    public async Task WriteBulkString_ValidBytes_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteBulkString("Hello"u8));

        // Assert
        output.Should().Equal("$5\r\nHello\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteBulkString_ValidString_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteBulkString("World"));

        // Assert
        output.Should().Equal("$5\r\nWorld\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteBulkString_EmptyBytes_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteBulkString(""u8));

        // Assert
        output.Should().Equal("$0\r\n\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteBulkString_BinaryData_ProducesCorrectOutput()
    {
        // Arrange
        var binaryData = new byte[] { 0x00, 0x01, 0xFF, 0xFE };

        // Act
        var output = await WriteAndCaptureAsync(w => w.WriteBulkString(binaryData));

        // Assert
        var expected = new byte[] { (byte)'$', (byte)'4', (byte)'\r', (byte)'\n', 0x00, 0x01, 0xFF, 0xFE, (byte)'\r', (byte)'\n' };
        output.Should().Equal(expected);
    }

    [Fact]
    public async Task WriteBulkString_WithCRLF_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteBulkString("Hello\r\nWorld"u8));

        // Assert
        output.Should().Equal("$12\r\nHello\r\nWorld\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteNullBulkString_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteNullBulkString());

        // Assert
        output.Should().Equal("$-1\r\n"u8.ToArray());
    }

    #endregion

    #region Array Tests

    [Fact]
    public async Task WriteArrayHeader_ValidCount_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteArrayHeader(3));

        // Assert
        output.Should().Equal("*3\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteArrayHeader_Zero_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteArrayHeader(0));

        // Assert
        output.Should().Equal("*0\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteNullArray_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteNullArray());

        // Assert
        output.Should().Equal("*-1\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteArray_SimpleStrings_ProducesCorrectOutput()
    {
        // Arrange
        var values = new[]
        {
            RespValue.SimpleString("OK"u8.ToArray()),
            RespValue.SimpleString("PONG"u8.ToArray())
        };

        // Act
        var output = await WriteAndCaptureAsync(w => w.WriteArray(values));

        // Assert
        output.Should().Equal("*2\r\n+OK\r\n+PONG\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteArray_MixedTypes_ProducesCorrectOutput()
    {
        // Arrange
        var values = new[]
        {
            RespValue.SimpleString("OK"u8.ToArray()),
            RespValue.Integer(42),
            RespValue.BulkString("test"u8.ToArray())
        };

        // Act
        var output = await WriteAndCaptureAsync(w => w.WriteArray(values));

        // Assert
        output.Should().Equal("*3\r\n+OK\r\n:42\r\n$4\r\ntest\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteArray_Empty_ProducesCorrectOutput()
    {
        // Arrange
        var values = Array.Empty<RespValue>();

        // Act
        var output = await WriteAndCaptureAsync(w => w.WriteArray(values));

        // Assert
        output.Should().Equal("*0\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteArray_NestedArrays_ProducesCorrectOutput()
    {
        // Arrange
        var innerArray1 = RespValue.Array(new[] { RespValue.Integer(1), RespValue.Integer(2) });
        var innerArray2 = RespValue.Array(new[] { RespValue.Integer(3), RespValue.Integer(4) });
        var values = new[] { innerArray1, innerArray2 };

        // Act
        var output = await WriteAndCaptureAsync(w => w.WriteArray(values));

        // Assert
        output.Should().Equal("*2\r\n*2\r\n:1\r\n:2\r\n*2\r\n:3\r\n:4\r\n"u8.ToArray());
    }

    #endregion

    #region Command Tests

    [Fact]
    public async Task WriteCommand_SimpleCommand_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteCommand("PING"u8.ToArray()));

        // Assert
        output.Should().Equal("*1\r\n$4\r\nPING\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteCommand_WithSingleArg_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w => w.WriteCommand("GET"u8.ToArray(), "key"u8.ToArray()));

        // Assert
        output.Should().Equal("*2\r\n$3\r\nGET\r\n$3\r\nkey\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteCommand_WithMultipleArgs_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w =>
            w.WriteCommand("SET"u8.ToArray(), "key"u8.ToArray(), "value"u8.ToArray()));

        // Assert
        output.Should().Equal("*3\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n"u8.ToArray());
    }

    #endregion

    #region Write RespValue Tests

    [Fact]
    public async Task Write_SimpleString_ProducesCorrectOutput()
    {
        // Arrange
        var value = RespValue.SimpleString("OK"u8.ToArray());

        // Act
        var output = await WriteAndCaptureAsync(w => w.Write(value));

        // Assert
        output.Should().Equal("+OK\r\n"u8.ToArray());
    }

    [Fact]
    public async Task Write_SimpleError_ProducesCorrectOutput()
    {
        // Arrange
        var value = RespValue.SimpleError("ERR test"u8.ToArray());

        // Act
        var output = await WriteAndCaptureAsync(w => w.Write(value));

        // Assert
        output.Should().Equal("-ERR test\r\n"u8.ToArray());
    }

    [Fact]
    public async Task Write_Integer_ProducesCorrectOutput()
    {
        // Arrange
        var value = RespValue.Integer(42);

        // Act
        var output = await WriteAndCaptureAsync(w => w.Write(value));

        // Assert
        output.Should().Equal(":42\r\n"u8.ToArray());
    }

    [Fact]
    public async Task Write_BulkString_ProducesCorrectOutput()
    {
        // Arrange
        var value = RespValue.BulkString("Hello"u8.ToArray());

        // Act
        var output = await WriteAndCaptureAsync(w => w.Write(value));

        // Assert
        output.Should().Equal("$5\r\nHello\r\n"u8.ToArray());
    }

    [Fact]
    public async Task Write_Array_ProducesCorrectOutput()
    {
        // Arrange
        var value = RespValue.Array(new[]
        {
            RespValue.SimpleString("OK"u8.ToArray()),
            RespValue.Integer(42)
        });

        // Act
        var output = await WriteAndCaptureAsync(w => w.Write(value));

        // Assert
        output.Should().Equal("*2\r\n+OK\r\n:42\r\n"u8.ToArray());
    }

    [Fact]
    public async Task Write_Null_ProducesCorrectOutput()
    {
        // Arrange
        var value = RespValue.Null;

        // Act
        var output = await WriteAndCaptureAsync(w => w.Write(value));

        // Assert
        output.Should().Equal("_\r\n"u8.ToArray());
    }

    [Fact]
    public async Task Write_Boolean_ProducesCorrectOutput()
    {
        // Arrange
        var value = RespValue.Boolean(true);

        // Act
        var output = await WriteAndCaptureAsync(w => w.Write(value));

        // Assert
        output.Should().Equal("#t\r\n"u8.ToArray());
    }

    [Fact]
    public async Task Write_Double_ProducesCorrectOutput()
    {
        // Arrange
        var value = RespValue.Double(Math.PI);

        // Act
        var output = await WriteAndCaptureAsync(w => w.Write(value));

        // Assert
        var outputStr = Encoding.UTF8.GetString(output);
        outputStr.Should().StartWith(",");
        outputStr.Should().EndWith("\r\n");
        outputStr.Should().Contain("3.14");
    }

    #endregion

    #region Multiple Writes Tests

    [Fact]
    public async Task MultipleWrites_Sequential_ProducesCorrectOutput()
    {
        // Arrange & Act
        var output = await WriteAndCaptureAsync(w =>
        {
            w.WriteSimpleString("OK"u8);
            w.WriteInteger(42);
            w.WriteBulkString("test"u8);
        });

        // Assert
        output.Should().Equal("+OK\r\n:42\r\n$4\r\ntest\r\n"u8.ToArray());
    }

    #endregion

    #region WriteAsync Tests

    [Fact]
    public async Task WriteAsync_RespValue_FlushesAutomatically()
    {
        // Arrange
        var pipe = new Pipe();
        var writer = new Resp3Writer(pipe.Writer);
        var value = RespValue.SimpleString("OK"u8.ToArray());

        // Act
        await writer.WriteAsync(value);
        pipe.Writer.Complete();

        // Assert
        var output = await ReadAllAsync(pipe.Reader);
        output.Should().Equal("+OK\r\n"u8.ToArray());
    }

    [Fact]
    public async Task WriteCommandAsync_FlushesAutomatically()
    {
        // Arrange
        var pipe = new Pipe();
        var writer = new Resp3Writer(pipe.Writer);

        // Act
        await writer.WriteCommandAsync("GET"u8.ToArray(), new ReadOnlyMemory<byte>[] { "key"u8.ToArray() }, 1);
        pipe.Writer.Complete();

        // Assert
        var output = await ReadAllAsync(pipe.Reader);
        output.Should().Equal("*2\r\n$3\r\nGET\r\n$3\r\nkey\r\n"u8.ToArray());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task WriteBulkString_LargeString_ProducesCorrectOutput()
    {
        // Arrange
        var largeString = new string('A', 1000);

        // Act
        var output = await WriteAndCaptureAsync(w => w.WriteBulkString(largeString));

        // Assert
        output.Should().StartWith("$1000\r\n"u8.ToArray());
        output.Should().EndWith("\r\n"u8.ToArray());
        output.Length.Should().Be(1000 + 9); // $1000\r\n (7 bytes) + data (1000) + \r\n (2)
    }

    [Fact]
    public async Task WriteArray_LargeArray_ProducesCorrectOutput()
    {
        // Arrange
        var values = Enumerable.Range(0, 100).Select(i => RespValue.Integer(i)).ToArray();

        // Act
        var output = await WriteAndCaptureAsync(w => w.WriteArray(values));

        // Assert
        var outputStr = Encoding.UTF8.GetString(output);
        outputStr.Should().StartWith("*100\r\n");
        outputStr.Should().Contain(":0\r\n");
        outputStr.Should().Contain(":99\r\n");
    }

    #endregion
}
