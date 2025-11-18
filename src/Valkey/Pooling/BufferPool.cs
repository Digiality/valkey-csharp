using System.Buffers;

namespace Valkey.Pooling;

/// <summary>
/// Provides buffer pooling for reducing allocations.
/// </summary>
internal static class BufferPool
{
    /// <summary>
    /// Rents a byte array from the shared array pool.
    /// </summary>
    /// <param name="minimumLength">The minimum length of the array.</param>
    /// <returns>A rented byte array.</returns>
    public static byte[] RentBytes(int minimumLength)
    {
        return ArrayPool<byte>.Shared.Rent(minimumLength);
    }

    /// <summary>
    /// Returns a rented byte array to the shared pool.
    /// </summary>
    /// <param name="array">The array to return.</param>
    /// <param name="clearArray">Whether to clear the array before returning.</param>
    public static void ReturnBytes(byte[] array, bool clearArray = false)
    {
        if (array != null)
        {
            ArrayPool<byte>.Shared.Return(array, clearArray);
        }
    }

    /// <summary>
    /// Represents a rented buffer that will be automatically returned to the pool when disposed.
    /// </summary>
    public readonly struct RentedBuffer : IDisposable
    {
        private readonly byte[] _array;
        private readonly bool _clearOnReturn;

        /// <summary>
        /// Gets the rented array.
        /// </summary>
        public byte[] Array => _array;

        /// <summary>
        /// Gets a span over the rented array.
        /// </summary>
        /// <param name="length">The length of the span.</param>
        public Span<byte> AsSpan(int length) => _array.AsSpan(0, length);

        /// <summary>
        /// Initializes a new instance of the <see cref="RentedBuffer"/> struct.
        /// </summary>
        /// <param name="minimumLength">The minimum length required.</param>
        /// <param name="clearOnReturn">Whether to clear the buffer when returned.</param>
        public RentedBuffer(int minimumLength, bool clearOnReturn = false)
        {
            _array = RentBytes(minimumLength);
            _clearOnReturn = clearOnReturn;
        }

        /// <summary>
        /// Returns the buffer to the pool.
        /// </summary>
        public void Dispose()
        {
            ReturnBytes(_array, _clearOnReturn);
        }
    }

    /// <summary>
    /// Rents a buffer that will be automatically returned when disposed.
    /// </summary>
    /// <param name="minimumLength">The minimum length required.</param>
    /// <param name="clearOnReturn">Whether to clear the buffer when returned.</param>
    /// <returns>A rented buffer.</returns>
    public static RentedBuffer Rent(int minimumLength, bool clearOnReturn = false)
    {
        return new RentedBuffer(minimumLength, clearOnReturn);
    }
}
