using System.Buffers;
using System.Runtime.CompilerServices;

namespace Valkey.Commands;

/// <summary>
/// Provides pooling for ReadOnlyMemory&lt;byte&gt; arrays used in command arguments.
/// </summary>
internal static class ArgumentArrayPool
{
    private static readonly ArrayPool<ReadOnlyMemory<byte>> Pool = ArrayPool<ReadOnlyMemory<byte>>.Shared;

    /// <summary>
    /// Rents an array from the pool for command arguments.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<byte>[] Rent(int minimumLength)
    {
        return Pool.Rent(minimumLength);
    }

    /// <summary>
    /// Returns a rented array to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(ReadOnlyMemory<byte>[] array, bool clearArray = false)
    {
        if (array != null)
        {
            Pool.Return(array, clearArray);
        }
    }
}
