using Valkey.Abstractions.Geospatial;
using Valkey.Protocol;

namespace Valkey.Commands.Geospatial;

/// <summary>
/// Parses geospatial command responses.
/// </summary>
internal static class GeoResultParser
{
    /// <summary>
    /// Parses a coordinate value that could be a double or string.
    /// </summary>
    public static double ParseCoordinate(RespValue value)
    {
        if (value.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }

        if (value.TryGetString(out var stringValue))
        {
            return double.Parse(stringValue!);
        }

        throw new InvalidOperationException($"Unable to parse coordinate from {value.Type}");
    }

    /// <summary>
    /// Parses a GeoPosition from a RESP array of [longitude, latitude].
    /// </summary>
    public static GeoPosition ParsePosition(RespValue[] coords)
    {
        if (coords.Length < 2)
        {
            throw new InvalidOperationException("Expected at least 2 coordinates");
        }

        var longitude = ParseCoordinate(coords[0]);
        var latitude = ParseCoordinate(coords[1]);

        return new GeoPosition(longitude, latitude);
    }

    /// <summary>
    /// Parses an array of GeoPosition results where null entries are allowed.
    /// </summary>
    public static GeoPosition?[] ParsePositionArray(RespValue response)
    {
        var array = response.AsArray();
        var results = new GeoPosition?[array.Length];

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].IsNull)
            {
                results[i] = null;
            }
            else
            {
                var coords = array[i].AsArray();
                results[i] = ParsePosition(coords);
            }
        }

        return results;
    }

    /// <summary>
    /// Parses GeoRadius/GeoSearch results with optional distance, coordinates, and hash.
    /// </summary>
    public static GeoRadiusResult[] ParseGeoRadiusResults(
        RespValue response,
        bool withDistance,
        bool withCoordinates,
        bool withHash)
    {
        var array = response.AsArray();
        var results = new GeoRadiusResult[array.Length];

        for (int i = 0; i < array.Length; i++)
        {
            results[i] = ParseSingleGeoRadiusResult(
                array[i],
                withDistance,
                withCoordinates,
                withHash);
        }

        return results;
    }

    private static GeoRadiusResult ParseSingleGeoRadiusResult(
        RespValue item,
        bool withDistance,
        bool withCoordinates,
        bool withHash)
    {
        // Simple case: just member name
        if (!withDistance && !withCoordinates && !withHash)
        {
            return new GeoRadiusResult(item.AsString()!);
        }

        // Complex case: member is in an array with additional data
        var itemArray = item.AsArray();
        var member = itemArray[0].AsString()!;

        var resultData = new GeoRadiusResultData
        {
            Distance = null,
            Position = null,
            Hash = null
        };

        int idx = 1;

        if (withDistance)
        {
            resultData.Distance = ParseCoordinate(itemArray[idx++]);
        }

        if (withHash)
        {
            resultData.Hash = itemArray[idx++].AsInteger();
        }

        if (withCoordinates)
        {
            var coords = itemArray[idx].AsArray();
            resultData.Position = ParsePosition(coords);
        }

        return new GeoRadiusResult(member, resultData.Distance, resultData.Position, resultData.Hash);
    }

    private struct GeoRadiusResultData
    {
        public double? Distance;
        public GeoPosition? Position;
        public long? Hash;
    }
}
