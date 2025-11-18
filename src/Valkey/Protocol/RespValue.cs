using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Valkey.Protocol;

/// <summary>
/// Represents a RESP3 protocol value with zero-allocation design using discriminated union pattern.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct RespValue : IEquatable<RespValue>
{
    private readonly RespType _type;
    private readonly long _integer;
    private readonly double _double;
    private readonly object? _object;

    private RespValue(RespType type, long integer = 0, double doubleValue = 0, object? obj = null)
    {
        _type = type;
        _integer = integer;
        _double = doubleValue;
        _object = obj;
    }

    /// <summary>
    /// Gets the type of this RESP value.
    /// </summary>
    public RespType Type => _type;

    /// <summary>
    /// Gets whether this value is null.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_object))]
    public bool IsNull => _type == RespType.Null;

    /// <summary>
    /// Creates a null RESP value.
    /// </summary>
    public static RespValue Null => new(RespType.Null);

    /// <summary>
    /// Creates a simple string value.
    /// </summary>
    public static RespValue SimpleString(ReadOnlyMemory<byte> value) =>
        new(RespType.SimpleString, obj: value);

    /// <summary>
    /// Creates a simple string value from a string.
    /// </summary>
    public static RespValue SimpleString(string value) =>
        new(RespType.SimpleString, obj: System.Text.Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Creates a simple error value.
    /// </summary>
    public static RespValue SimpleError(ReadOnlyMemory<byte> value) =>
        new(RespType.SimpleError, obj: value);

    /// <summary>
    /// Creates a simple error value from a string.
    /// </summary>
    public static RespValue SimpleError(string value) =>
        new(RespType.SimpleError, obj: System.Text.Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Creates an integer value.
    /// </summary>
    public static RespValue Integer(long value) =>
        new(RespType.Integer, integer: value);

    /// <summary>
    /// Creates a boolean value.
    /// </summary>
    public static RespValue Boolean(bool value) =>
        new(RespType.Boolean, integer: value ? 1 : 0);

    /// <summary>
    /// Creates a double value.
    /// </summary>
    public static RespValue Double(double value) =>
        new(RespType.Double, doubleValue: value);

    /// <summary>
    /// Creates a bulk string value.
    /// </summary>
    public static RespValue BulkString(ReadOnlyMemory<byte> value) =>
        new(RespType.BulkString, obj: value);

    /// <summary>
    /// Creates a bulk string value from a string.
    /// </summary>
    public static RespValue BulkString(string value) =>
        new(RespType.BulkString, obj: System.Text.Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Creates a bulk error value.
    /// </summary>
    public static RespValue BulkError(ReadOnlyMemory<byte> value) =>
        new(RespType.BulkError, obj: value);

    /// <summary>
    /// Creates an array value.
    /// </summary>
    public static RespValue Array(RespValue[] values) =>
        new(RespType.Array, obj: values);

    /// <summary>
    /// Creates a map value.
    /// </summary>
    public static RespValue Map(Dictionary<RespValue, RespValue> values) =>
        new(RespType.Map, obj: values);

    /// <summary>
    /// Creates a set value.
    /// </summary>
    public static RespValue Set(HashSet<RespValue> values) =>
        new(RespType.Set, obj: values);

    /// <summary>
    /// Creates a push value (for pub/sub and client-side caching).
    /// </summary>
    public static RespValue Push(RespValue[] values) =>
        new(RespType.Push, obj: values);

    /// <summary>
    /// Tries to get the value as a long integer.
    /// </summary>
    public bool TryGetInteger(out long value)
    {
        if (_type == RespType.Integer || _type == RespType.Boolean)
        {
            value = _integer;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Tries to get the value as a boolean.
    /// </summary>
    public bool TryGetBoolean(out bool value)
    {
        if (_type == RespType.Boolean)
        {
            value = _integer != 0;
            return true;
        }

        value = false;
        return false;
    }

    /// <summary>
    /// Tries to get the value as a double.
    /// </summary>
    public bool TryGetDouble(out double value)
    {
        if (_type == RespType.Double)
        {
            value = _double;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Tries to get the value as a byte array.
    /// </summary>
    public bool TryGetBytes(out ReadOnlyMemory<byte> value)
    {
        if (_object is ReadOnlyMemory<byte> bytes && this.IsBytesType())
        {
            value = bytes;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Tries to get the value as a string.
    /// </summary>
    public bool TryGetString([NotNullWhen(true)] out string? value)
    {
        if (TryGetBytes(out var bytes))
        {
            value = System.Text.Encoding.UTF8.GetString(bytes.Span);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Tries to get the value as an array.
    /// </summary>
    public bool TryGetArray([NotNullWhen(true)] out RespValue[]? value)
    {
        if (_object is RespValue[] array && (_type == RespType.Array || _type == RespType.Push))
        {
            value = array;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Tries to get the value as a map.
    /// </summary>
    public bool TryGetMap([NotNullWhen(true)] out Dictionary<RespValue, RespValue>? value)
    {
        if (_object is Dictionary<RespValue, RespValue> map && _type == RespType.Map)
        {
            value = map;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Tries to get the value as a set.
    /// </summary>
    public bool TryGetSet([NotNullWhen(true)] out HashSet<RespValue>? value)
    {
        if (_object is HashSet<RespValue> set && _type == RespType.Set)
        {
            value = set;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Gets the value as a long integer or throws.
    /// </summary>
    public long AsInteger()
    {
        if (TryGetInteger(out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Cannot convert {_type} to integer");
    }

    /// <summary>
    /// Gets the value as a string or throws.
    /// </summary>
    public string AsString()
    {
        if (TryGetString(out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Cannot convert {_type} to string");
    }

    /// <summary>
    /// Gets the value as a byte array or throws.
    /// </summary>
    public ReadOnlyMemory<byte> AsBytes()
    {
        if (TryGetBytes(out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Cannot convert {_type} to bytes");
    }

    /// <summary>
    /// Gets the value as an array or throws.
    /// </summary>
    public RespValue[] AsArray()
    {
        if (TryGetArray(out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Cannot convert {_type} to array");
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return _type switch
        {
            RespType.Null => "<null>",
            RespType.Integer => _integer.ToString(),
            RespType.Boolean => (_integer != 0).ToString(),
            RespType.Double => _double.ToString(),
            RespType.SimpleString or RespType.BulkString => TryGetString(out var s) ? s : "<invalid>",
            RespType.SimpleError or RespType.BulkError => TryGetString(out var e) ? $"Error: {e}" : "<invalid>",
            RespType.Array => TryGetArray(out var a) ? $"Array[{a.Length}]" : "<invalid>",
            RespType.Map => TryGetMap(out var m) ? $"Map[{m.Count}]" : "<invalid>",
            RespType.Set => TryGetSet(out var set) ? $"Set[{set.Count}]" : "<invalid>",
            RespType.Push => TryGetArray(out var p) ? $"Push[{p.Length}]" : "<invalid>",
            _ => $"<unknown:{_type}>"
        };
    }

    /// <inheritdoc/>
    public bool Equals(RespValue other)
    {
        if (_type != other._type)
        {
            return false;
        }

        return _type switch
        {
            RespType.Null => true,
            RespType.Integer or RespType.Boolean => _integer == other._integer,
            RespType.Double => _double == other._double,
            _ => ReferenceEquals(_object, other._object) ||
                 (_object?.Equals(other._object) == true)
        };
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RespValue other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_type);
        hash.Add(_integer);
        hash.Add(_double);
        hash.Add(_object);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(RespValue left, RespValue right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(RespValue left, RespValue right) => !left.Equals(right);
}
