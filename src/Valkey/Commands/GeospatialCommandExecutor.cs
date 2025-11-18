using System.Text;
using Valkey.Abstractions.Geospatial;
using Valkey.Commands.Geospatial;
using Valkey.Protocol;

namespace Valkey.Commands;

/// <summary>
/// Executes geospatial commands against Valkey/Redis.
/// </summary>
internal sealed class GeospatialCommandExecutor : CommandExecutorBase
{
    internal GeospatialCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Adds one or more geospatial items (longitude, latitude, name) to the specified key.
    /// </summary>
    internal async ValueTask<long> GeoAddAsync(
        string key,
        double longitude,
        double latitude,
        string member,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ValidateMember(member);

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (memberBuffer, memberLength) = CommandBuilder.EncodeValue(member);
        var (lonBuffer, lonLength, latBuffer, latLength) = GeoArgumentBuilder.EncodeCoordinates(longitude, latitude);
        var args = ArgumentArrayPool.Rent(4);

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(lonBuffer, lonLength);
            args[2] = CommandBuilder.AsMemory(latBuffer, latLength);
            args[3] = CommandBuilder.AsMemory(memberBuffer, memberLength);

            var response = await ExecuteAsync(
                CommandBytes.Geoadd,
                args,
                4,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, memberBuffer, lonBuffer, latBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Returns the distance between two members in the geospatial index.
    /// </summary>
    internal async ValueTask<double?> GeoDistanceAsync(
        string key,
        string member1,
        string member2,
        GeoUnit unit = GeoUnit.Meters,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ValidateMember(member1, nameof(member1));
        ValidateMember(member2, nameof(member2));

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (member1Buffer, member1Length) = CommandBuilder.EncodeValue(member1);
        var (member2Buffer, member2Length) = CommandBuilder.EncodeValue(member2);
        var args = ArgumentArrayPool.Rent(4);

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(member1Buffer, member1Length);
            args[2] = CommandBuilder.AsMemory(member2Buffer, member2Length);
            args[3] = GeoArgumentBuilder.GetUnitBytes(unit);

            var response = await ExecuteAsync(
                CommandBytes.Geodist,
                args,
                4,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return null;
            }

            return GeoResultParser.ParseCoordinate(response);
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, member1Buffer, member2Buffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Returns the position (longitude, latitude) of one or more members.
    /// </summary>
    internal async ValueTask<GeoPosition?[]> GeoPositionAsync(
        string key,
        string[] members,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ValidateMembersArray(members);

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var argCount = 1 + members.Length;
        var args = ArgumentArrayPool.Rent(argCount);
        var memberBuffers = new byte[members.Length][];
        var memberLengths = new int[members.Length];

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            for (int i = 0; i < members.Length; i++)
            {
                (memberBuffers[i], memberLengths[i]) = CommandBuilder.EncodeValue(members[i]);
                args[i + 1] = CommandBuilder.AsMemory(memberBuffers[i], memberLengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Geopos,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return GeoResultParser.ParsePositionArray(response);
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ReturnMemberBuffers(memberBuffers);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Returns geohash strings representing the position of one or more members.
    /// </summary>
    internal async ValueTask<string?[]> GeoHashAsync(
        string key,
        string[] members,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ValidateMembersArray(members);

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var argCount = 1 + members.Length;
        var args = ArgumentArrayPool.Rent(argCount);
        var memberBuffers = new byte[members.Length][];
        var memberLengths = new int[members.Length];

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            for (int i = 0; i < members.Length; i++)
            {
                (memberBuffers[i], memberLengths[i]) = CommandBuilder.EncodeValue(members[i]);
                args[i + 1] = CommandBuilder.AsMemory(memberBuffers[i], memberLengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Geohash,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            var array = response.AsArray();
            var results = new string?[array.Length];

            for (int i = 0; i < array.Length; i++)
            {
                results[i] = array[i].IsNull ? null : array[i].AsString();
            }

            return results;
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ReturnMemberBuffers(memberBuffers);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Searches for members within a radius from a given longitude/latitude coordinate.
    /// </summary>
    internal async ValueTask<GeoRadiusResult[]> GeoRadiusAsync(
        string key,
        double longitude,
        double latitude,
        double radius,
        GeoUnit unit = GeoUnit.Meters,
        long? count = null,
        bool withDistance = false,
        bool withCoordinates = false,
        bool withHash = false,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (lonBuffer, lonLength, latBuffer, latLength) = GeoArgumentBuilder.EncodeCoordinates(longitude, latitude);
        var (radiusBuffer, radiusLength) = GeoArgumentBuilder.EncodeRadius(radius);

        var argList = new List<ReadOnlyMemory<byte>>
        {
            CommandBuilder.AsMemory(keyBuffer, keyLength),
            CommandBuilder.AsMemory(lonBuffer, lonLength),
            CommandBuilder.AsMemory(latBuffer, latLength),
            CommandBuilder.AsMemory(radiusBuffer, radiusLength),
            GeoArgumentBuilder.GetUnitBytes(unit)
        };

        GeoArgumentBuilder.AddOptionalArguments(argList, withDistance, withCoordinates, withHash, count);

        var args = ArgumentArrayPool.Rent(argList.Count);

        try
        {
            CopyArgsToArray(argList, args);

            var response = await ExecuteAsync(
                CommandBytes.Georadius,
                args,
                argList.Count,
                cancellationToken).ConfigureAwait(false);

            return GeoResultParser.ParseGeoRadiusResults(response, withDistance, withCoordinates, withHash);
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, lonBuffer, latBuffer, radiusBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Searches for members within a radius from an existing member.
    /// </summary>
    internal async ValueTask<GeoRadiusResult[]> GeoRadiusByMemberAsync(
        string key,
        string member,
        double radius,
        GeoUnit unit = GeoUnit.Meters,
        long? count = null,
        bool withDistance = false,
        bool withCoordinates = false,
        bool withHash = false,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ValidateMember(member);

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (memberBuffer, memberLength) = CommandBuilder.EncodeValue(member);
        var (radiusBuffer, radiusLength) = GeoArgumentBuilder.EncodeRadius(radius);

        var argList = new List<ReadOnlyMemory<byte>>
        {
            CommandBuilder.AsMemory(keyBuffer, keyLength),
            CommandBuilder.AsMemory(memberBuffer, memberLength),
            CommandBuilder.AsMemory(radiusBuffer, radiusLength),
            GeoArgumentBuilder.GetUnitBytes(unit)
        };

        GeoArgumentBuilder.AddOptionalArguments(argList, withDistance, withCoordinates, withHash, count);

        var args = ArgumentArrayPool.Rent(argList.Count);

        try
        {
            CopyArgsToArray(argList, args);

            var response = await ExecuteAsync(
                CommandBytes.Georadiusbymember,
                args,
                argList.Count,
                cancellationToken).ConfigureAwait(false);

            return GeoResultParser.ParseGeoRadiusResults(response, withDistance, withCoordinates, withHash);
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, memberBuffer, radiusBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Searches for members within a polygon-shaped area. (Valkey 9.0+)
    /// </summary>
    internal async ValueTask<GeoRadiusResult[]> GeoSearchByPolygonAsync(
        string key,
        GeoPosition[] polygon,
        long? count = null,
        bool withDistance = false,
        bool withCoordinates = false,
        bool withHash = false,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ValidatePolygon(polygon);

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (numVerticesBuffer, numVerticesLength) = CommandBuilder.EncodeLong(polygon.Length);

        var argList = new List<ReadOnlyMemory<byte>>
        {
            CommandBuilder.AsMemory(keyBuffer, keyLength),
            CommandBytes.Bypolygon,
            CommandBuilder.AsMemory(numVerticesBuffer, numVerticesLength)
        };

        var coordBuffers = new List<byte[]>();

        try
        {
            // Add all polygon vertices
            foreach (var vertex in polygon)
            {
                var (lonBuffer, lonLength, latBuffer, latLength) = GeoArgumentBuilder.EncodeCoordinates(
                    vertex.Longitude,
                    vertex.Latitude);

                coordBuffers.Add(lonBuffer);
                coordBuffers.Add(latBuffer);

                argList.Add(CommandBuilder.AsMemory(lonBuffer, lonLength));
                argList.Add(CommandBuilder.AsMemory(latBuffer, latLength));
            }

            GeoArgumentBuilder.AddOptionalArguments(argList, withDistance, withCoordinates, withHash, count);

            var args = ArgumentArrayPool.Rent(argList.Count);

            try
            {
                CopyArgsToArray(argList, args);

                var response = await ExecuteAsync(
                    CommandBytes.Geosearch,
                    args,
                    argList.Count,
                    cancellationToken).ConfigureAwait(false);

                return GeoResultParser.ParseGeoRadiusResults(response, withDistance, withCoordinates, withHash);
            }
            finally
            {
                ArgumentArrayPool.Return(args);
            }
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, numVerticesBuffer);
            foreach (var buffer in coordBuffers)
            {
                CommandBuilder.Return(buffer);
            }
        }
    }

    // ========================================
    // Private Helper Methods
    // ========================================

    private static void ValidateMember(string member, string? paramName = null)
    {
        if (string.IsNullOrEmpty(member))
        {
            throw new ArgumentException("Member cannot be null or empty", paramName ?? nameof(member));
        }
    }

    private static void ValidateMembersArray(string[] members)
    {
        if (members == null || members.Length == 0)
        {
            throw new ArgumentException("Members array cannot be null or empty", nameof(members));
        }
    }

    private static void ValidatePolygon(GeoPosition[] polygon)
    {
        if (polygon == null || polygon.Length < 3)
        {
            throw new ArgumentException("Polygon must have at least 3 vertices", nameof(polygon));
        }
    }

    private static void ReturnMemberBuffers(byte[][] memberBuffers)
    {
        foreach (var buffer in memberBuffers)
        {
            if (buffer != null)
            {
                CommandBuilder.Return(buffer);
            }
        }
    }

    private static void CopyArgsToArray(List<ReadOnlyMemory<byte>> source, ReadOnlyMemory<byte>[] destination)
    {
        for (int i = 0; i < source.Count; i++)
        {
            destination[i] = source[i];
        }
    }
}
