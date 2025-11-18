using System.Buffers;
using System.Text;
using Valkey.Abstractions.Geospatial;

namespace Valkey.Commands.Geospatial;

/// <summary>
/// Helper for building geospatial command arguments with minimal allocations.
/// </summary>
internal static class GeoArgumentBuilder
{
    /// <summary>
    /// Encodes longitude and latitude values as UTF8 bytes using ArrayPool.
    /// </summary>
    public static (byte[] lonBuffer, int lonLength, byte[] latBuffer, int latLength) EncodeCoordinates(
        double longitude,
        double latitude)
    {
        var lonStr = longitude.ToString("G17");
        var latStr = latitude.ToString("G17");

        var lonByteCount = Encoding.UTF8.GetByteCount(lonStr);
        var latByteCount = Encoding.UTF8.GetByteCount(latStr);

        var lonBuffer = ArrayPool<byte>.Shared.Rent(lonByteCount);
        var latBuffer = ArrayPool<byte>.Shared.Rent(latByteCount);

        var lonLength = Encoding.UTF8.GetBytes(lonStr, 0, lonStr.Length, lonBuffer, 0);
        var latLength = Encoding.UTF8.GetBytes(latStr, 0, latStr.Length, latBuffer, 0);

        return (lonBuffer, lonLength, latBuffer, latLength);
    }

    /// <summary>
    /// Encodes a radius value as UTF8 bytes using ArrayPool.
    /// </summary>
    public static (byte[] buffer, int length) EncodeRadius(double radius)
    {
        var radiusStr = radius.ToString("G17");
        var byteCount = Encoding.UTF8.GetByteCount(radiusStr);
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        var length = Encoding.UTF8.GetBytes(radiusStr, 0, radiusStr.Length, buffer, 0);
        return (buffer, length);
    }

    /// <summary>
    /// Gets the command bytes for a geospatial unit.
    /// </summary>
    public static ReadOnlyMemory<byte> GetUnitBytes(GeoUnit unit)
    {
        return unit switch
        {
            GeoUnit.Meters => CommandBytes.M,
            GeoUnit.Kilometers => CommandBytes.Km,
            GeoUnit.Miles => CommandBytes.Mi,
            GeoUnit.Feet => CommandBytes.Ft,
            _ => CommandBytes.M
        };
    }

    /// <summary>
    /// Adds optional geospatial query arguments to the argument list.
    /// </summary>
    public static void AddOptionalArguments(
        List<ReadOnlyMemory<byte>> argList,
        bool withDistance,
        bool withCoordinates,
        bool withHash,
        long? count)
    {
        if (withDistance)
        {
            argList.Add(CommandBytes.Withdist);
        }
        if (withCoordinates)
        {
            argList.Add(CommandBytes.Withcoord);
        }
        if (withHash)
        {
            argList.Add(CommandBytes.Withhash);
        }
        if (count.HasValue)
        {
            argList.Add(CommandBytes.Count);
            argList.Add(Encoding.UTF8.GetBytes(count.Value.ToString()));
        }
    }
}
