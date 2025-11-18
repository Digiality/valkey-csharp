using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;

namespace Valkey.Protocol;

/// <summary>
/// High-performance RESP3 protocol parser using System.IO.Pipelines for zero-allocation parsing.
/// </summary>
public sealed class Resp3Parser
{
    private readonly PipeReader _reader;

    /// <summary>
    /// Initializes a new instance of the <see cref="Resp3Parser"/> class.
    /// </summary>
    /// <param name="reader">The pipe reader to read from.</param>
    public Resp3Parser(PipeReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>
    /// Reads the next RESP value asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed RESP value.</returns>
    public async ValueTask<RespValue> ReadAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;

            try
            {
                if (TryParse(ref buffer, out var value))
                {
                    _reader.AdvanceTo(buffer.Start);
                    return value;
                }

                // Tell the PipeReader how much we consumed and examined
                _reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    throw new RespProtocolException("Incomplete RESP value in stream");
                }
            }
            catch
            {
                _reader.AdvanceTo(buffer.Start);
                throw;
            }
        }
    }

    /// <summary>
    /// Tries to parse a RESP value from the buffer.
    /// </summary>
    private bool TryParse(ref ReadOnlySequence<byte> buffer, out RespValue value)
    {
        if (buffer.IsEmpty)
        {
            value = default;
            return false;
        }

        var reader = new SequenceReader<byte>(buffer);

        // Read the type marker
        if (!reader.TryRead(out var typeByte))
        {
            value = default;
            return false;
        }

        var type = (RespType)typeByte;
        var startPosition = buffer.Start;

        var result = type switch
        {
            RespType.SimpleString => TryParseSimpleString(ref reader, out value),
            RespType.SimpleError => TryParseSimpleError(ref reader, out value),
            RespType.Integer => TryParseInteger(ref reader, out value),
            RespType.BulkString => TryParseBulkString(ref reader, out value),
            RespType.Array => TryParseArray(ref reader, out value),
            RespType.Null => TryParseNull(ref reader, out value),
            RespType.Boolean => TryParseBoolean(ref reader, out value),
            RespType.Double => TryParseDouble(ref reader, out value),
            RespType.BulkError => TryParseBulkError(ref reader, out value),
            RespType.Map => TryParseMap(ref reader, out value),
            RespType.Set => TryParseSet(ref reader, out value),
            RespType.Push => TryParsePush(ref reader, out value),
            _ => throw new RespProtocolException($"Unknown RESP type: {(char)typeByte}")
        };

        if (result)
        {
            buffer = buffer.Slice(reader.Position);
        }

        return result;
    }

    private bool TryParseSimpleString(ref SequenceReader<byte> reader, out RespValue value)
    {
        if (!TryReadLine(ref reader, out var line))
        {
            value = default;
            return false;
        }

        value = RespValue.SimpleString(line.ToArray());
        return true;
    }

    private bool TryParseSimpleError(ref SequenceReader<byte> reader, out RespValue value)
    {
        if (!TryReadLine(ref reader, out var line))
        {
            value = default;
            return false;
        }

        value = RespValue.SimpleError(line.ToArray());
        return true;
    }

    private bool TryParseInteger(ref SequenceReader<byte> reader, out RespValue value)
    {
        if (!TryReadLine(ref reader, out var line))
        {
            value = default;
            return false;
        }

        var lineArray = line.ToArray();
        if (!TryParseLong(lineArray, out var intValue))
        {
            throw new RespProtocolException($"Invalid integer format: {Encoding.UTF8.GetString(lineArray)}");
        }

        value = RespValue.Integer(intValue);
        return true;
    }

    private bool TryParseBoolean(ref SequenceReader<byte> reader, out RespValue value)
    {
        if (!TryReadLine(ref reader, out var line))
        {
            value = default;
            return false;
        }

        if (line.Length != 1)
        {
            throw new RespProtocolException("Invalid boolean format");
        }

        var boolByte = line.First.Span[0];
        var boolValue = boolByte switch
        {
            (byte)'t' => true,
            (byte)'f' => false,
            _ => throw new RespProtocolException($"Invalid boolean value: {(char)boolByte}")
        };

        value = RespValue.Boolean(boolValue);
        return true;
    }

    private bool TryParseDouble(ref SequenceReader<byte> reader, out RespValue value)
    {
        if (!TryReadLine(ref reader, out var line))
        {
            value = default;
            return false;
        }

        var lineArray = line.ToArray();
        var str = Encoding.UTF8.GetString(lineArray);

        // Handle special values
        if (str == "inf")
        {
            value = RespValue.Double(double.PositiveInfinity);
            return true;
        }

        if (str == "-inf")
        {
            value = RespValue.Double(double.NegativeInfinity);
            return true;
        }

        if (!double.TryParse(str, out var doubleValue))
        {
            throw new RespProtocolException($"Invalid double format: {str}");
        }

        value = RespValue.Double(doubleValue);
        return true;
    }

    private bool TryParseNull(ref SequenceReader<byte> reader, out RespValue value)
    {
        if (!TryReadLine(ref reader, out _))
        {
            value = default;
            return false;
        }

        value = RespValue.Null;
        return true;
    }

    private bool TryParseBulkString(ref SequenceReader<byte> reader, out RespValue value)
    {
        return TryParseBulk(ref reader, RespType.BulkString, out value);
    }

    private bool TryParseBulkError(ref SequenceReader<byte> reader, out RespValue value)
    {
        return TryParseBulk(ref reader, RespType.BulkError, out value);
    }

    private bool TryParseBulk(ref SequenceReader<byte> reader, RespType type, out RespValue value)
    {
        if (!TryReadLine(ref reader, out var lengthLine))
        {
            value = default;
            return false;
        }

        var lengthArray = lengthLine.ToArray();
        if (!TryParseLong(lengthArray, out var length))
        {
            throw new RespProtocolException($"Invalid bulk string length: {Encoding.UTF8.GetString(lengthArray)}");
        }

        // Handle null bulk string
        if (length == -1)
        {
            value = RespValue.Null;
            return true;
        }

        if (length < 0)
        {
            throw new RespProtocolException($"Invalid bulk string length: {length}");
        }

        if (length > RespConstants.MaxBulkSize)
        {
            throw new RespProtocolException($"Bulk string too large: {length} bytes");
        }

        // Read the bulk data
        if (reader.Remaining < length + 2) // +2 for CRLF
        {
            value = default;
            return false;
        }

        var data = reader.Sequence.Slice(reader.Position, length);
        var dataArray = data.ToArray();

        reader.Advance(length);

        // Read and verify CRLF
        if (!TryReadCrlf(ref reader))
        {
            throw new RespProtocolException("Expected CRLF after bulk string data");
        }

        value = type == RespType.BulkString
            ? RespValue.BulkString(dataArray)
            : RespValue.BulkError(dataArray);

        return true;
    }

    private bool TryParseArray(ref SequenceReader<byte> reader, out RespValue value)
    {
        return TryParseAggregate(ref reader, RespType.Array, out value);
    }

    private bool TryParsePush(ref SequenceReader<byte> reader, out RespValue value)
    {
        return TryParseAggregate(ref reader, RespType.Push, out value);
    }

    private bool TryParseSet(ref SequenceReader<byte> reader, out RespValue value)
    {
        return TryParseAggregate(ref reader, RespType.Set, out value);
    }

    private bool TryParseAggregate(ref SequenceReader<byte> reader, RespType type, out RespValue value)
    {
        if (!TryReadLine(ref reader, out var lengthLine))
        {
            value = default;
            return false;
        }

        var lengthArray = lengthLine.ToArray();
        if (!TryParseLong(lengthArray, out var count))
        {
            throw new RespProtocolException($"Invalid {type} count: {Encoding.UTF8.GetString(lengthArray)}");
        }

        // Handle null array
        if (count == -1)
        {
            value = RespValue.Null;
            return true;
        }

        if (count < 0)
        {
            throw new RespProtocolException($"Invalid {type} count: {count}");
        }

        if (count > RespConstants.MaxCollectionSize)
        {
            throw new RespProtocolException($"Collection too large: {count} elements");
        }

        var elements = new RespValue[count];
        var originalSequence = reader.Sequence;
        var originalPosition = reader.Position;

        for (var i = 0; i < count; i++)
        {
            var elementBuffer = reader.Sequence.Slice(reader.Position);

            if (!TryParse(ref elementBuffer, out var element))
            {
                // Not enough data, rollback
                value = default;
                return false;
            }

            elements[i] = element;
            reader.Advance(reader.Sequence.Slice(reader.Position, elementBuffer.Start).Length);
        }

        value = type switch
        {
            RespType.Array => RespValue.Array(elements),
            RespType.Push => RespValue.Push(elements),
            RespType.Set => RespValue.Set(new HashSet<RespValue>(elements)),
            _ => throw new InvalidOperationException($"Unsupported aggregate type: {type}")
        };

        return true;
    }

    private bool TryParseMap(ref SequenceReader<byte> reader, out RespValue value)
    {
        if (!TryReadLine(ref reader, out var lengthLine))
        {
            value = default;
            return false;
        }

        var lengthArray = lengthLine.ToArray();
        if (!TryParseLong(lengthArray, out var count))
        {
            throw new RespProtocolException($"Invalid map count: {Encoding.UTF8.GetString(lengthArray)}");
        }

        if (count == -1)
        {
            value = RespValue.Null;
            return true;
        }

        if (count < 0)
        {
            throw new RespProtocolException($"Invalid map count: {count}");
        }

        if (count > RespConstants.MaxCollectionSize)
        {
            throw new RespProtocolException($"Map too large: {count} entries");
        }

        var map = new Dictionary<RespValue, RespValue>((int)count);

        for (var i = 0; i < count; i++)
        {
            // Parse key
            var keyBuffer = reader.Sequence.Slice(reader.Position);
            if (!TryParse(ref keyBuffer, out var key))
            {
                value = default;
                return false;
            }

            reader.Advance(reader.Sequence.Slice(reader.Position, keyBuffer.Start).Length);

            // Parse value
            var valueBuffer = reader.Sequence.Slice(reader.Position);
            if (!TryParse(ref valueBuffer, out var mapValue))
            {
                value = default;
                return false;
            }

            reader.Advance(reader.Sequence.Slice(reader.Position, valueBuffer.Start).Length);

            map[key] = mapValue;
        }

        value = RespValue.Map(map);
        return true;
    }

    /// <summary>
    /// Tries to read a line (up to CRLF) from the buffer.
    /// </summary>
    private bool TryReadLine(ref SequenceReader<byte> reader, out ReadOnlySequence<byte> line)
    {
        if (reader.TryReadTo(out line, RespConstants.LF))
        {
            // Remove CR if present
            if (line.Length > 0 && line.ToArray()[line.Length - 1] == RespConstants.CR)
            {
                line = line.Slice(0, line.Length - 1);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to read and verify CRLF.
    /// </summary>
    private bool TryReadCrlf(ref SequenceReader<byte> reader)
    {
        if (reader.Remaining < 2)
        {
            return false;
        }

        if (!reader.TryRead(out var cr) || cr != RespConstants.CR)
        {
            return false;
        }

        if (!reader.TryRead(out var lf) || lf != RespConstants.LF)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tries to parse a long integer from a span.
    /// </summary>
    private static bool TryParseLong(ReadOnlySpan<byte> span, out long value)
    {
        value = 0;

        if (span.IsEmpty)
        {
            return false;
        }

        var negative = false;
        var index = 0;

        if (span[0] == (byte)'-')
        {
            negative = true;
            index = 1;
        }
        else if (span[0] == (byte)'+')
        {
            index = 1;
        }

        if (index >= span.Length)
        {
            return false;
        }

        for (; index < span.Length; index++)
        {
            var digit = span[index] - (byte)'0';

            if (digit is < 0 or > 9)
            {
                return false;
            }

            value = value * 10 + digit;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }
}
