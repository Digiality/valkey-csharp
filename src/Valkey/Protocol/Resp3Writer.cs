using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace Valkey.Protocol;

/// <summary>
/// High-performance RESP3 protocol writer with zero-allocation design.
/// </summary>
public sealed class Resp3Writer
{
    private readonly PipeWriter _writer;

    /// <summary>
    /// Initializes a new instance of the <see cref="Resp3Writer"/> class.
    /// </summary>
    /// <param name="writer">The pipe writer to write to.</param>
    public Resp3Writer(PipeWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>
    /// Writes a RESP value and flushes the writer.
    /// </summary>
    public async ValueTask WriteAsync(RespValue value, CancellationToken cancellationToken = default)
    {
        Write(value);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a RESP value to the buffer without flushing.
    /// </summary>
    public void Write(RespValue value)
    {
        switch (value.Type)
        {
            case RespType.SimpleString:
                WriteSimpleString(value.AsBytes().Span);
                break;

            case RespType.SimpleError:
                WriteSimpleError(value.AsBytes().Span);
                break;

            case RespType.Integer:
                WriteInteger(value.AsInteger());
                break;

            case RespType.BulkString:
                if (value.IsNull)
                {
                    WriteNullBulkString();
                }
                else
                {
                    WriteBulkString(value.AsBytes().Span);
                }
                break;

            case RespType.Array:
                if (value.IsNull)
                {
                    WriteNullArray();
                }
                else
                {
                    WriteArray(value.AsArray());
                }
                break;

            case RespType.Null:
                WriteNull();
                break;

            case RespType.Boolean:
                value.TryGetBoolean(out var boolValue);
                WriteBoolean(boolValue);
                break;

            case RespType.Double:
                value.TryGetDouble(out var doubleValue);
                WriteDouble(doubleValue);
                break;

            default:
                throw new NotSupportedException($"Writing {value.Type} is not yet implemented");
        }
    }

    /// <summary>
    /// Writes a command (helper for connection handshake).
    /// </summary>
    public void WriteCommand(ReadOnlyMemory<byte> command, ReadOnlyMemory<byte>[] args, int argCount)
    {
        var totalArgs = 1 + argCount;
        WriteArrayHeader(totalArgs);
        WriteBulkString(command.Span);

        for (int i = 0; i < argCount; i++)
        {
            WriteBulkString(args[i].Span);
        }
    }

    /// <summary>
    /// Writes a command with variable arguments (params version for convenience).
    /// </summary>
    public void WriteCommand(byte[] command, params byte[][] args)
    {
        var totalArgs = 1 + args.Length;
        WriteArrayHeader(totalArgs);
        WriteBulkString(command);

        foreach (var arg in args)
        {
            WriteBulkString(arg);
        }
    }

    /// <summary>
    /// Writes a command and flushes.
    /// </summary>
    public async ValueTask WriteCommandAsync(
        ReadOnlyMemory<byte> command,
        ReadOnlyMemory<byte>[] args,
        int argCount,
        CancellationToken cancellationToken = default)
    {
        WriteCommand(command, args, argCount);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a simple string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSimpleString(ReadOnlySpan<byte> value)
    {
        var span = _writer.GetSpan(value.Length + 3); // +type +CRLF
        span[0] = (byte)RespType.SimpleString;
        value.CopyTo(span[1..]);
        RespConstants.Crlf.CopyTo(span[(1 + value.Length)..]);
        _writer.Advance(value.Length + 3);
    }

    /// <summary>
    /// Writes a simple error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSimpleError(ReadOnlySpan<byte> value)
    {
        var span = _writer.GetSpan(value.Length + 3);
        span[0] = (byte)RespType.SimpleError;
        value.CopyTo(span[1..]);
        RespConstants.Crlf.CopyTo(span[(1 + value.Length)..]);
        _writer.Advance(value.Length + 3);
    }

    /// <summary>
    /// Writes an integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInteger(long value)
    {
        var span = _writer.GetSpan(24); // Enough for ':' + long.MinValue + CRLF
        span[0] = (byte)RespType.Integer;

        var written = FormatInteger(value, span[1..]);

        RespConstants.Crlf.CopyTo(span[(1 + written)..]);
        _writer.Advance(1 + written + 2);
    }

    /// <summary>
    /// Writes a boolean.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBoolean(bool value)
    {
        var span = _writer.GetSpan(4); // #t\r\n or #f\r\n
        span[0] = (byte)RespType.Boolean;
        span[1] = value ? (byte)'t' : (byte)'f';
        RespConstants.Crlf.CopyTo(span[2..]);
        _writer.Advance(4);
    }

    /// <summary>
    /// Writes a double.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[32];
        buffer[0] = (byte)RespType.Double;

        int written;
        if (double.IsPositiveInfinity(value))
        {
            "inf"u8.CopyTo(buffer[1..]);
            written = 3;
        }
        else if (double.IsNegativeInfinity(value))
        {
            "-inf"u8.CopyTo(buffer[1..]);
            written = 4;
        }
        else
        {
            var str = value.ToString("G17");
            written = Encoding.UTF8.GetBytes(str, buffer[1..]);
        }

        RespConstants.Crlf.CopyTo(buffer[(1 + written)..]);

        var span = _writer.GetSpan(1 + written + 2);
        buffer[..(1 + written + 2)].CopyTo(span);
        _writer.Advance(1 + written + 2);
    }

    /// <summary>
    /// Writes a null value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNull()
    {
        var span = _writer.GetSpan(3); // _\r\n
        span[0] = (byte)RespType.Null;
        RespConstants.Crlf.CopyTo(span[1..]);
        _writer.Advance(3);
    }

    /// <summary>
    /// Writes a bulk string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBulkString(ReadOnlySpan<byte> value)
    {
        // Write $<length>\r\n
        var headerSpan = _writer.GetSpan(32);
        headerSpan[0] = (byte)RespType.BulkString;
        var lengthWritten = FormatInteger(value.Length, headerSpan[1..]);
        RespConstants.Crlf.CopyTo(headerSpan[(1 + lengthWritten)..]);
        _writer.Advance(1 + lengthWritten + 2);

        // Write data\r\n
        var dataSpan = _writer.GetSpan(value.Length + 2);
        value.CopyTo(dataSpan);
        RespConstants.Crlf.CopyTo(dataSpan[value.Length..]);
        _writer.Advance(value.Length + 2);
    }

    /// <summary>
    /// Writes a bulk string from a string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBulkString(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);

        // Write $<length>\r\n
        var headerSpan = _writer.GetSpan(32);
        headerSpan[0] = (byte)RespType.BulkString;
        var lengthWritten = FormatInteger(byteCount, headerSpan[1..]);
        RespConstants.Crlf.CopyTo(headerSpan[(1 + lengthWritten)..]);
        _writer.Advance(1 + lengthWritten + 2);

        // Write data\r\n
        var dataSpan = _writer.GetSpan(byteCount + 2);
        Encoding.UTF8.GetBytes(value, dataSpan);
        RespConstants.Crlf.CopyTo(dataSpan[byteCount..]);
        _writer.Advance(byteCount + 2);
    }

    /// <summary>
    /// Writes a null bulk string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNullBulkString()
    {
        var span = _writer.GetSpan(5); // $-1\r\n
        RespConstants.NullBulkString.CopyTo(span);
        _writer.Advance(5);
    }

    /// <summary>
    /// Writes an array header.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteArrayHeader(int count)
    {
        var span = _writer.GetSpan(24);
        span[0] = (byte)RespType.Array;
        var written = FormatInteger(count, span[1..]);
        RespConstants.Crlf.CopyTo(span[(1 + written)..]);
        _writer.Advance(1 + written + 2);
    }

    /// <summary>
    /// Writes a null array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNullArray()
    {
        var span = _writer.GetSpan(5); // *-1\r\n
        RespConstants.NullArray.CopyTo(span);
        _writer.Advance(5);
    }

    /// <summary>
    /// Writes an array.
    /// </summary>
    public void WriteArray(ReadOnlySpan<RespValue> values)
    {
        WriteArrayHeader(values.Length);

        foreach (var value in values)
        {
            Write(value);
        }
    }

    /// <summary>
    /// Flushes the writer.
    /// </summary>
    public ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        return _writer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Formats an integer to a span and returns the number of bytes written.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FormatInteger(long value, Span<byte> destination)
    {
        var isNegative = value < 0;
        var absValue = isNegative ? (ulong)(-value) : (ulong)value;

        // Count digits
        var digitCount = CountDigits(absValue);
        var totalLength = digitCount + (isNegative ? 1 : 0);

        if (destination.Length < totalLength)
        {
            throw new ArgumentException("Destination too small", nameof(destination));
        }

        // Write from right to left
        var index = totalLength - 1;
        do
        {
            destination[index--] = (byte)('0' + (absValue % 10));
            absValue /= 10;
        } while (absValue > 0);

        if (isNegative)
        {
            destination[0] = (byte)'-';
        }

        return totalLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits(ulong value)
    {
        if (value == 0)
        {
            return 1;
        }

        var count = 0;
        while (value > 0)
        {
            count++;
            value /= 10;
        }

        return count;
    }
}
