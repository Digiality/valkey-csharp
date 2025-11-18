namespace Valkey.Abstractions;

/// <summary>
/// Represents a Valkey database for executing commands.
/// </summary>
public interface IValkeyDatabase
{
    /// <summary>
    /// Gets the database number.
    /// </summary>
    public int DatabaseNumber { get; }

    #region String Commands

    /// <summary>
    /// Get the value of a key.
    /// </summary>
    public ValueTask<string?> StringGetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set the string value of a key.
    /// </summary>
    public ValueTask<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment the integer value of a key by one.
    /// </summary>
    public ValueTask<long> StringIncrementAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment the integer value of a key by the given amount.
    /// </summary>
    public ValueTask<long> StringIncrementAsync(string key, long value, CancellationToken cancellationToken = default);

    #endregion

    #region Hash Commands

    /// <summary>
    /// Set the value of a hash field.
    /// </summary>
    public ValueTask<bool> HashSetAsync(string key, string field, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the value of a hash field.
    /// </summary>
    public ValueTask<string?> HashGetAsync(string key, string field, CancellationToken cancellationToken = default);

    #endregion

    #region List Commands

    /// <summary>
    /// Prepend one or more values to a list.
    /// </summary>
    public ValueTask<long> ListLeftPushAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove and return the first element of a list.
    /// </summary>
    public ValueTask<string?> ListLeftPopAsync(string key, CancellationToken cancellationToken = default);

    #endregion

    #region Set Commands

    /// <summary>
    /// Add one member to a set.
    /// </summary>
    public ValueTask<bool> SetAddAsync(string key, string member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a member exists in a set.
    /// </summary>
    public ValueTask<bool> SetContainsAsync(string key, string member, CancellationToken cancellationToken = default);

    #endregion

    #region Sorted Set Commands

    /// <summary>
    /// Add a member with a score to a sorted set.
    /// </summary>
    public ValueTask<bool> SortedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the score of a member in a sorted set.
    /// </summary>
    public ValueTask<double?> SortedSetScoreAsync(string key, string member, CancellationToken cancellationToken = default);

    #endregion

    #region Utility Commands

    /// <summary>
    /// Ping the server.
    /// </summary>
    public ValueTask<string> PingAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Pub/Sub

    /// <summary>
    /// Publishes a message to a channel.
    /// </summary>
    /// <param name="channel">The channel name to publish to.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of clients that received the message.</returns>
    public ValueTask<long> PublishAsync(string channel, string message, CancellationToken cancellationToken = default);

    #endregion

    #region Scripting

    /// <summary>
    /// Evaluates a Lua script on the server.
    /// </summary>
    /// <param name="script">The Lua script to execute.</param>
    /// <param name="keys">Array of keys that the script will access.</param>
    /// <param name="args">Array of additional arguments to pass to the script.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the script execution as a dynamic value.</returns>
    public ValueTask<object?> ScriptEvaluateAsync(string script, string[]? keys = null, string[]? args = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a Lua script into the server's script cache.
    /// </summary>
    /// <param name="script">The Lua script to load.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The SHA1 hash of the script.</returns>
    public ValueTask<string> ScriptLoadAsync(string script, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a previously loaded Lua script using its SHA1 hash.
    /// </summary>
    /// <param name="sha1">The SHA1 hash of the script to execute.</param>
    /// <param name="keys">Array of keys that the script will access.</param>
    /// <param name="args">Array of additional arguments to pass to the script.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the script execution as a dynamic value.</returns>
    public ValueTask<object?> ScriptEvaluateShaAsync(string sha1, string[]? keys = null, string[]? args = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if scripts exist in the script cache.
    /// </summary>
    /// <param name="sha1Hashes">Array of SHA1 hashes to check.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of booleans indicating if each script exists.</returns>
    public ValueTask<bool[]> ScriptExistsAsync(string[] sha1Hashes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all scripts from the script cache.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask ScriptFlushAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Streams

    /// <summary>
    /// Appends a new entry to a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="fieldValues">Field-value pairs to add to the stream entry.</param>
    /// <param name="id">Optional entry ID (use "*" for auto-generation).</param>
    /// <param name="maxLength">Optional maximum stream length (trimming).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The ID of the added entry.</returns>
    public ValueTask<string> StreamAddAsync(string key, Dictionary<string, string> fieldValues, string id = "*", long? maxLength = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads entries from one or more streams.
    /// </summary>
    /// <param name="key">The stream key to read from.</param>
    /// <param name="startId">The starting ID (exclusive). Use "0" for all entries or "$" for new entries.</param>
    /// <param name="count">Optional maximum number of entries to return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of stream entries.</returns>
    public ValueTask<Streams.StreamEntry[]> StreamReadAsync(string key, string startId = "0", long? count = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a range of entries from a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="start">Start ID (inclusive). Use "-" for minimum ID.</param>
    /// <param name="end">End ID (inclusive). Use "+" for maximum ID.</param>
    /// <param name="count">Optional maximum number of entries to return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of stream entries.</returns>
    public ValueTask<Streams.StreamEntry[]> StreamRangeAsync(string key, string start = "-", string end = "+", long? count = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of entries in a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of entries in the stream.</returns>
    public ValueTask<long> StreamLengthAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes entries from a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="ids">Array of entry IDs to delete.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of entries deleted.</returns>
    public ValueTask<long> StreamDeleteAsync(string key, string[] ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trims a stream to a specified length.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="maxLength">The maximum length to trim to.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of entries removed.</returns>
    public ValueTask<long> StreamTrimAsync(string key, long maxLength, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a consumer group for a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name.</param>
    /// <param name="startId">Starting ID for the group (use "0" for all entries or "$" for new entries).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask StreamGroupCreateAsync(string key, string groupName, string startId = "$", CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads entries from a stream as part of a consumer group.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name.</param>
    /// <param name="consumerName">The consumer name.</param>
    /// <param name="startId">Starting ID (use ">" for new messages).</param>
    /// <param name="count">Optional maximum number of entries to return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of stream entries.</returns>
    public ValueTask<Streams.StreamEntry[]> StreamReadGroupAsync(string key, string groupName, string consumerName, string startId = ">", long? count = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges one or more messages as processed in a consumer group.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name.</param>
    /// <param name="ids">Array of entry IDs to acknowledge.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of messages successfully acknowledged.</returns>
    public ValueTask<long> StreamAckAsync(string key, string groupName, string[] ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Destroys a consumer group.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name to destroy.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask StreamGroupDestroyAsync(string key, string groupName, CancellationToken cancellationToken = default);

    #endregion

    #region Geospatial Commands

    /// <summary>
    /// Adds one or more geospatial items (longitude, latitude, name) to the specified key.
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="longitude">The longitude coordinate.</param>
    /// <param name="latitude">The latitude coordinate.</param>
    /// <param name="member">The member name.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of elements added to the sorted set (not including elements already existing).</returns>
    public ValueTask<long> GeoAddAsync(string key, double longitude, double latitude, string member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the distance between two members in the geospatial index.
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="member1">The first member.</param>
    /// <param name="member2">The second member.</param>
    /// <param name="unit">The unit for the returned distance (default: Meters).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The distance between the two members, or null if one or both members are missing.</returns>
    public ValueTask<double?> GeoDistanceAsync(string key, string member1, string member2, Geospatial.GeoUnit unit = Geospatial.GeoUnit.Meters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the position (longitude, latitude) of one or more members.
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="members">The member names.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of positions corresponding to each member (null for missing members).</returns>
    public ValueTask<Geospatial.GeoPosition?[]> GeoPositionAsync(string key, string[] members, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns geohash strings representing the position of one or more members.
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="members">The member names.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of geohash strings (null for missing members).</returns>
    public ValueTask<string?[]> GeoHashAsync(string key, string[] members, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for members within a radius from a given longitude/latitude coordinate.
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="longitude">The center longitude.</param>
    /// <param name="latitude">The center latitude.</param>
    /// <param name="radius">The search radius.</param>
    /// <param name="unit">The unit for the radius (default: Meters).</param>
    /// <param name="count">Optional limit on the number of results.</param>
    /// <param name="withDistance">Include distance in results.</param>
    /// <param name="withCoordinates">Include coordinates in results.</param>
    /// <param name="withHash">Include geohash in results.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of search results.</returns>
    public ValueTask<Geospatial.GeoRadiusResult[]> GeoRadiusAsync(string key, double longitude, double latitude, double radius, Geospatial.GeoUnit unit = Geospatial.GeoUnit.Meters, long? count = null, bool withDistance = false, bool withCoordinates = false, bool withHash = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for members within a radius from an existing member.
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="member">The center member.</param>
    /// <param name="radius">The search radius.</param>
    /// <param name="unit">The unit for the radius (default: Meters).</param>
    /// <param name="count">Optional limit on the number of results.</param>
    /// <param name="withDistance">Include distance in results.</param>
    /// <param name="withCoordinates">Include coordinates in results.</param>
    /// <param name="withHash">Include geohash in results.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of search results.</returns>
    public ValueTask<Geospatial.GeoRadiusResult[]> GeoRadiusByMemberAsync(string key, string member, double radius, Geospatial.GeoUnit unit = Geospatial.GeoUnit.Meters, long? count = null, bool withDistance = false, bool withCoordinates = false, bool withHash = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for members within a polygon-shaped area. (Valkey 9.0+)
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="polygon">Array of positions defining the polygon vertices (should be closed).</param>
    /// <param name="count">Optional limit on the number of results.</param>
    /// <param name="withDistance">Include distance from computed center in results.</param>
    /// <param name="withCoordinates">Include coordinates in results.</param>
    /// <param name="withHash">Include geohash in results.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of search results.</returns>
    public ValueTask<Geospatial.GeoRadiusResult[]> GeoSearchByPolygonAsync(string key, Geospatial.GeoPosition[] polygon, long? count = null, bool withDistance = false, bool withCoordinates = false, bool withHash = false, CancellationToken cancellationToken = default);

    #endregion
}
