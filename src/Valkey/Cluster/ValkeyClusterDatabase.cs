using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Valkey.Abstractions;
using Valkey.Abstractions.Geospatial;
using Valkey.Abstractions.Streams;
using Valkey.Configuration;
using Valkey.Protocol;

namespace Valkey.Cluster;

/// <summary>
/// Database implementation for cluster mode with automatic MOVED/ASK redirection handling.
/// </summary>
internal sealed partial class ValkeyClusterDatabase : IValkeyDatabase
{
    private readonly ValkeyCluster _cluster;
    private readonly ClusterConnectionPool _connectionPool;
    private readonly ClusterSlotMap _slotMap;
    private readonly ValkeyClusterOptions _options;

    // Regex patterns for parsing MOVED and ASK responses
    // Format: "MOVED 3999 127.0.0.1:6381" or "ASK 3999 127.0.0.1:6381"
    [GeneratedRegex(@"^MOVED (\d+) ([^:]+):(\d+)$", RegexOptions.Compiled)]
    private static partial Regex MovedPattern();

    [GeneratedRegex(@"^ASK (\d+) ([^:]+):(\d+)$", RegexOptions.Compiled)]
    private static partial Regex AskPattern();

    public ValkeyClusterDatabase(
        ValkeyCluster cluster,
        ClusterConnectionPool connectionPool,
        ClusterSlotMap slotMap,
        ValkeyClusterOptions options)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
        _slotMap = slotMap ?? throw new ArgumentNullException(nameof(slotMap));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Database number (always 0 for cluster mode).
    /// </summary>
    public int DatabaseNumber => 0;

    /// <summary>
    /// Executes a command with automatic MOVED/ASK redirection handling.
    /// </summary>
    private async ValueTask<RespValue> ExecuteWithRedirectionAsync(
        string key,
        ReadOnlyMemory<byte> command,
        ReadOnlyMemory<byte>[] args,
        CancellationToken cancellationToken)
    {
        var redirectCount = 0;
        var askRedirect = false;
        EndPoint? targetEndpoint = null;

        while (redirectCount < _options.MaxRedirects)
        {
            try
            {
                ValkeyConnection connection;

                if (askRedirect && targetEndpoint != null)
                {
                    // ASK redirection: send ASKING command first
                    connection = await _cluster.GetConnectionAsync(targetEndpoint, cancellationToken)
                        .ConfigureAwait(false);

                    await connection.ExecuteCommandAsync("ASKING"u8.ToArray(), Array.Empty<ReadOnlyMemory<byte>>(), 0, cancellationToken)
                        .ConfigureAwait(false);

                    askRedirect = false;
                }
                else if (targetEndpoint != null)
                {
                    // MOVED redirection: use the new target directly
                    connection = await _cluster.GetConnectionAsync(targetEndpoint, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    // First attempt: use slot map
                    connection = await _cluster.GetConnectionForKeyAsync(key, cancellationToken)
                        .ConfigureAwait(false);
                }

                var response = await connection.ExecuteCommandAsync(command, args, args.Length, cancellationToken)
                    .ConfigureAwait(false);

                // Check for MOVED/ASK error
                if (response.Type == RespType.SimpleError || response.Type == RespType.BulkError)
                {
                    var errorMessage = response.AsString();

                    // Handle MOVED redirection
                    if (_options.AutoHandleMovedRedirects)
                    {
                        var movedMatch = MovedPattern().Match(errorMessage);
                        if (movedMatch.Success)
                        {
                            var host = movedMatch.Groups[2].Value;
                            var port = int.Parse(movedMatch.Groups[3].Value);
                            targetEndpoint = new DnsEndPoint(host, port);

                            // MOVED indicates permanent slot migration - refresh topology in background
                            _ = Task.Run(() => _cluster.RefreshTopologyAsync(CancellationToken.None));

                            redirectCount++;
                            continue;
                        }
                    }

                    // Handle ASK redirection
                    if (_options.AutoHandleAskRedirects)
                    {
                        var askMatch = AskPattern().Match(errorMessage);
                        if (askMatch.Success)
                        {
                            var host = askMatch.Groups[2].Value;
                            var port = int.Parse(askMatch.Groups[3].Value);
                            targetEndpoint = new DnsEndPoint(host, port);
                            askRedirect = true;

                            redirectCount++;
                            continue;
                        }
                    }

                    // Not a redirection error, throw it
                    throw RespException.FromError(response);
                }

                return response;
            }
            catch (Exception ex) when (ex is not RespException)
            {
                // Network error or connection failure
                if (redirectCount < _options.MaxRedirects - 1)
                {
                    // Refresh topology and retry
                    await _cluster.RefreshTopologyAsync(cancellationToken).ConfigureAwait(false);
                    targetEndpoint = null;
                    redirectCount++;
                    continue;
                }

                throw;
            }
        }

        throw new ClusterException($"Exceeded maximum redirect count ({_options.MaxRedirects}) for key '{key}'");
    }

    // Helper to encode key
    private static ReadOnlyMemory<byte> EncodeKey(string key)
    {
        return System.Text.Encoding.UTF8.GetBytes(key).AsMemory();
    }

    // Helper to encode value
    private static ReadOnlyMemory<byte> EncodeValue(string value)
    {
        return System.Text.Encoding.UTF8.GetBytes(value).AsMemory();
    }

    // String commands
    public async ValueTask<string?> StringGetAsync(string key, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key) };
        var response = await ExecuteWithRedirectionAsync(key, "GET"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.IsNull ? null : response.AsString();
    }

    public async ValueTask<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var argsList = new List<ReadOnlyMemory<byte>>
        {
            EncodeKey(key),
            EncodeValue(value)
        };

        if (expiry.HasValue)
        {
            argsList.Add("EX"u8.ToArray().AsMemory());
            argsList.Add(EncodeValue(((long)expiry.Value.TotalSeconds).ToString()));
        }

        var response = await ExecuteWithRedirectionAsync(key, "SET"u8.ToArray(), argsList.ToArray(), cancellationToken)
            .ConfigureAwait(false);

        return response.AsString() == "OK";
    }

    public async ValueTask<long> StringIncrementAsync(string key, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key) };
        var response = await ExecuteWithRedirectionAsync(key, "INCR"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger();
    }

    public async ValueTask<long> StringIncrementAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key), EncodeValue(value.ToString()) };
        var response = await ExecuteWithRedirectionAsync(key, "INCRBY"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger();
    }

    public async ValueTask<long> StringDecrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
    {
        var args = value == 1
            ? new[] { EncodeKey(key) }
            : new[] { EncodeKey(key), EncodeValue(value.ToString()) };

        var command = value == 1 ? "DECR"u8.ToArray() : "DECRBY"u8.ToArray();
        var response = await ExecuteWithRedirectionAsync(key, command, args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger();
    }

    // Hash commands
    public async ValueTask<bool> HashSetAsync(string key, string field, string value, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key), EncodeValue(field), EncodeValue(value) };
        var response = await ExecuteWithRedirectionAsync(key, "HSET"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger() == 1;
    }

    public async ValueTask<string?> HashGetAsync(string key, string field, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key), EncodeValue(field) };
        var response = await ExecuteWithRedirectionAsync(key, "HGET"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.IsNull ? null : response.AsString();
    }

    // List commands
    public async ValueTask<long> ListLeftPushAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key), EncodeValue(value) };
        var response = await ExecuteWithRedirectionAsync(key, "LPUSH"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger();
    }

    public async ValueTask<string?> ListLeftPopAsync(string key, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key) };
        var response = await ExecuteWithRedirectionAsync(key, "LPOP"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.IsNull ? null : response.AsString();
    }

    // Set commands
    public async ValueTask<bool> SetAddAsync(string key, string member, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key), EncodeValue(member) };
        var response = await ExecuteWithRedirectionAsync(key, "SADD"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger() == 1;
    }

    public async ValueTask<bool> SetContainsAsync(string key, string member, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key), EncodeValue(member) };
        var response = await ExecuteWithRedirectionAsync(key, "SISMEMBER"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger() == 1;
    }

    // Sorted Set commands
    public async ValueTask<bool> SortedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key), EncodeValue(score.ToString()), EncodeValue(member) };
        var response = await ExecuteWithRedirectionAsync(key, "ZADD"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger() == 1;
    }

    public async ValueTask<double?> SortedSetScoreAsync(string key, string member, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key), EncodeValue(member) };
        var response = await ExecuteWithRedirectionAsync(key, "ZSCORE"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsNull)
        {
            return null;
        }

        // Handle RESP3 (double) and RESP2 (string) responses
        if (response.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }
        else
        {
            return double.Parse(response.AsString());
        }
    }

    // Key commands
    public async ValueTask<long> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key) };
        var response = await ExecuteWithRedirectionAsync(key, "DEL"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger();
    }

    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var args = new[] { EncodeKey(key) };
        var response = await ExecuteWithRedirectionAsync(key, "EXISTS"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger() == 1;
    }

    public async ValueTask<bool> ExpireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var args = new[]
        {
            EncodeKey(key),
            EncodeValue(((long)expiry.TotalSeconds).ToString())
        };

        var response = await ExecuteWithRedirectionAsync(key, "EXPIRE"u8.ToArray(), args, cancellationToken)
            .ConfigureAwait(false);

        return response.AsInteger() == 1;
    }

    // Utility commands
    public async ValueTask<string> PingAsync(CancellationToken cancellationToken = default)
    {
        // PING can go to any node
        var node = _slotMap.GetRandomMasterNode()
            ?? throw new NoNodeAvailableException("No nodes available in cluster");

        var connection = await _connectionPool.GetOrCreateConnectionAsync(node.EndPoint, cancellationToken)
            .ConfigureAwait(false);

        var response = await connection.ExecuteCommandAsync("PING"u8.ToArray(), Array.Empty<ReadOnlyMemory<byte>>(), 0, cancellationToken)
            .ConfigureAwait(false);

        return response.AsString();
    }

    // Pub/Sub - not supported in cluster mode
    public ValueTask<long> PublishAsync(string channel, string message, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Pub/Sub not supported in cluster mode");

    // Scripting commands
    public ValueTask<object?> ScriptEvaluateAsync(string script, string[]? keys = null, string[]? args = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Scripting commands not yet fully implemented in cluster mode");

    public ValueTask<string> ScriptLoadAsync(string script, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Scripting commands not yet fully implemented in cluster mode");

    public ValueTask<object?> ScriptEvaluateShaAsync(string sha1, string[]? keys = null, string[]? args = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Scripting commands not yet fully implemented in cluster mode");

    public ValueTask<bool[]> ScriptExistsAsync(string[] sha1Hashes, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Scripting commands not yet fully implemented in cluster mode");

    public ValueTask ScriptFlushAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Scripting commands not yet fully implemented in cluster mode");

    // Stream commands
    public ValueTask<string> StreamAddAsync(string key, Dictionary<string, string> fieldValues, string id = "*", long? maxLength = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    public ValueTask<StreamEntry[]> StreamReadAsync(string key, string startId = "0", long? count = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    public ValueTask<StreamEntry[]> StreamRangeAsync(string key, string start = "-", string end = "+", long? count = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    public ValueTask<long> StreamLengthAsync(string key, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    public ValueTask<long> StreamDeleteAsync(string key, string[] ids, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    public ValueTask<long> StreamTrimAsync(string key, long maxLength, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    public ValueTask StreamGroupCreateAsync(string key, string groupName, string startId = "$", CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    public ValueTask<StreamEntry[]> StreamReadGroupAsync(string key, string groupName, string consumerName, string startId = ">", long? count = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    public ValueTask<long> StreamAckAsync(string key, string groupName, string[] ids, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    public ValueTask StreamGroupDestroyAsync(string key, string groupName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Stream commands not yet fully implemented in cluster mode");

    // Geospatial commands
    public ValueTask<long> GeoAddAsync(string key, double longitude, double latitude, string member, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Geo commands not yet fully implemented in cluster mode");

    public ValueTask<double?> GeoDistanceAsync(string key, string member1, string member2, GeoUnit unit = GeoUnit.Meters, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Geo commands not yet fully implemented in cluster mode");

    public ValueTask<GeoPosition?[]> GeoPositionAsync(string key, string[] members, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Geo commands not yet fully implemented in cluster mode");

    public ValueTask<string?[]> GeoHashAsync(string key, string[] members, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Geo commands not yet fully implemented in cluster mode");

    public ValueTask<GeoRadiusResult[]> GeoRadiusAsync(string key, double longitude, double latitude, double radius, GeoUnit unit = GeoUnit.Meters, long? count = null, bool withDistance = false, bool withCoordinates = false, bool withHash = false, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Geo commands not yet fully implemented in cluster mode");

    public ValueTask<GeoRadiusResult[]> GeoRadiusByMemberAsync(string key, string member, double radius, GeoUnit unit = GeoUnit.Meters, long? count = null, bool withDistance = false, bool withCoordinates = false, bool withHash = false, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Geo commands not yet fully implemented in cluster mode");

    public ValueTask<GeoRadiusResult[]> GeoSearchByPolygonAsync(string key, GeoPosition[] polygon, long? count = null, bool withDistance = false, bool withCoordinates = false, bool withHash = false, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Geo commands not yet fully implemented in cluster mode");
}
