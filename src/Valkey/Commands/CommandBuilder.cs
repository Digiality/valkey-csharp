using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Valkey.Commands;

/// <summary>
/// Helper for building commands with minimal allocations using ArrayPool.
/// </summary>
internal static class CommandBuilder
{
    private const int DefaultBufferSize = 256;

    /// <summary>
    /// Encodes a string key using ArrayPool to avoid allocation.
    /// Returns a rented buffer that must be returned to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (byte[] buffer, int length) EncodeKey(string key)
    {
        var byteCount = Encoding.UTF8.GetByteCount(key);
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        var actualLength = Encoding.UTF8.GetBytes(key, 0, key.Length, buffer, 0);
        return (buffer, actualLength);
    }

    /// <summary>
    /// Encodes a string value using ArrayPool to avoid allocation.
    /// Returns a rented buffer that must be returned to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (byte[] buffer, int length) EncodeValue(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        var actualLength = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
        return (buffer, actualLength);
    }

    /// <summary>
    /// Encodes a long value as UTF8 bytes using ArrayPool.
    /// Returns a rented buffer that must be returned to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (byte[] buffer, int length) EncodeLong(long value)
    {
        var str = value.ToString();
        return EncodeValue(str);
    }

    /// <summary>
    /// Encodes a double value as UTF8 bytes using ArrayPool.
    /// Returns a rented buffer that must be returned to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (byte[] buffer, int length) EncodeDouble(double value)
    {
        var str = value.ToString("G17"); // Use roundtrip format
        return EncodeValue(str);
    }

    /// <summary>
    /// Creates a ReadOnlyMemory from a rented buffer with the specified length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<byte> AsMemory(byte[] buffer, int length)
    {
        return new ReadOnlyMemory<byte>(buffer, 0, length);
    }

    /// <summary>
    /// Returns a rented buffer to the ArrayPool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(byte[] buffer)
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }

    /// <summary>
    /// Returns multiple rented buffers to the ArrayPool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(params byte[][] buffers)
    {
        foreach (var buffer in buffers)
        {
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
