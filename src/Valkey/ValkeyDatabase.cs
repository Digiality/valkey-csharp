using System.Text;
using Valkey.Abstractions;
using Valkey.Abstractions.Streams;
using Valkey.Protocol;
using Valkey.Transactions;
using Valkey.Commands;
using Valkey.ResponseParsers;

namespace Valkey;

/// <summary>
/// Represents a Valkey database for executing commands.
/// </summary>
public sealed class ValkeyDatabase : IValkeyDatabase
{
    private readonly ValkeyConnection _connection;
    private readonly int _database;
    private readonly UtilityCommandExecutor _utilityCommands;
    private readonly KeyCommandExecutor _keyCommands;
    private readonly StringCommandExecutor _stringCommands;
    private readonly HashCommandExecutor _hashCommands;
    private readonly ListCommandExecutor _listCommands;
    private readonly SetCommandExecutor _setCommands;
    private readonly SortedSetCommandExecutor _sortedSetCommands;
    private readonly ScriptingCommandExecutor _scriptingCommands;
    private readonly StreamCommandExecutor _streamCommands;
    private readonly GeospatialCommandExecutor _geoCommands;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValkeyDatabase"/> class.
    /// </summary>
    internal ValkeyDatabase(ValkeyConnection connection, int database)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _database = database;
        _utilityCommands = new UtilityCommandExecutor(connection);
        _keyCommands = new KeyCommandExecutor(connection);
        _stringCommands = new StringCommandExecutor(connection);
        _hashCommands = new HashCommandExecutor(connection);
        _listCommands = new ListCommandExecutor(connection);
        _setCommands = new SetCommandExecutor(connection);
        _sortedSetCommands = new SortedSetCommandExecutor(connection);
        _scriptingCommands = new ScriptingCommandExecutor(connection);
        _streamCommands = new StreamCommandExecutor(connection);
        _geoCommands = new GeospatialCommandExecutor(connection);
    }

    /// <inheritdoc/>
    public int DatabaseNumber => _database;

    #region String Commands

    /// <summary>
    /// Get the value of a key.
    /// </summary>
    public ValueTask<string?> StringGetAsync(string key, CancellationToken cancellationToken = default)
        => _stringCommands.GetAsync(key, cancellationToken);

    /// <summary>
    /// Get the value of a key as bytes.
    /// </summary>
    public ValueTask<byte[]?> StringGetBytesAsync(string key, CancellationToken cancellationToken = default)
        => _stringCommands.GetBytesAsync(key, cancellationToken);

    /// <summary>
    /// Set the string value of a key.
    /// </summary>
    public ValueTask<bool> StringSetAsync(
        string key,
        string value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
        => _stringCommands.SetAsync(key, value, expiry, cancellationToken);

    /// <summary>
    /// Set the bytes value of a key.
    /// </summary>
    public ValueTask<bool> StringSetAsync(
        string key,
        byte[] value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
        => _stringCommands.SetAsync(key, value, expiry, cancellationToken);

    /// <summary>
    /// Delete a key.
    /// </summary>
    public ValueTask<bool> KeyDeleteAsync(string key, CancellationToken cancellationToken = default)
        => _keyCommands.DeleteAsync(key, cancellationToken);

    /// <summary>
    /// Delete multiple keys.
    /// </summary>
    public ValueTask<long> KeyDeleteAsync(string[] keys, CancellationToken cancellationToken = default)
        => _keyCommands.DeleteAsync(keys, cancellationToken);

    /// <summary>
    /// Check if a key exists.
    /// </summary>
    public ValueTask<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default)
        => _keyCommands.ExistsAsync(key, cancellationToken);

    /// <summary>
    /// Set a key's time to live in seconds.
    /// </summary>
    public ValueTask<bool> KeyExpireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        => _keyCommands.ExpireAsync(key, expiry, cancellationToken);

    /// <summary>
    /// Increment the integer value of a key by one.
    /// </summary>
    public ValueTask<long> StringIncrementAsync(string key, CancellationToken cancellationToken = default)
        => _stringCommands.IncrementAsync(key, cancellationToken);

    /// <summary>
    /// Increment the integer value of a key by the given amount.
    /// </summary>
    public ValueTask<long> StringIncrementAsync(string key, long value, CancellationToken cancellationToken = default)
        => _stringCommands.IncrementAsync(key, value, cancellationToken);

    /// <summary>
    /// Decrement the integer value of a key by one.
    /// </summary>
    public ValueTask<long> StringDecrementAsync(string key, CancellationToken cancellationToken = default)
        => _stringCommands.DecrementAsync(key, cancellationToken);

    /// <summary>
    /// Decrement the integer value of a key by the given amount.
    /// </summary>
    public ValueTask<long> StringDecrementAsync(string key, long value, CancellationToken cancellationToken = default)
        => _stringCommands.DecrementAsync(key, value, cancellationToken);

    #endregion

    #region Hash Commands

    /// <summary>
    /// Set the value of a hash field.
    /// </summary>
    public ValueTask<bool> HashSetAsync(string key, string field, string value, CancellationToken cancellationToken = default)
        => _hashCommands.HashSetAsync(key, field, value, cancellationToken);

    /// <summary>
    /// Set multiple hash fields to multiple values.
    /// </summary>
    public ValueTask<bool> HashSetAsync(string key, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
        => _hashCommands.HashSetAsync(key, fieldValues, cancellationToken);

    /// <summary>
    /// Get the value of a hash field.
    /// </summary>
    public ValueTask<string?> HashGetAsync(string key, string field, CancellationToken cancellationToken = default)
        => _hashCommands.HashGetAsync(key, field, cancellationToken);

    /// <summary>
    /// Get all fields and values in a hash.
    /// Returns an empty dictionary if the key does not exist.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary containing all field-value pairs.</returns>
    /// <exception cref="ArgumentException">Thrown when key is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the server returns a malformed response.</exception>
    public ValueTask<Dictionary<string, string>> HashGetAllAsync(string key, CancellationToken cancellationToken = default)
        => _hashCommands.HashGetAllAsync(key, cancellationToken);

    /// <summary>
    /// Delete one or more hash fields.
    /// </summary>
    public ValueTask<bool> HashDeleteAsync(string key, string field, CancellationToken cancellationToken = default)
        => _hashCommands.HashDeleteAsync(key, field, cancellationToken);

    /// <summary>
    /// Delete multiple hash fields.
    /// </summary>
    public ValueTask<long> HashDeleteAsync(string key, string[] fields, CancellationToken cancellationToken = default)
        => _hashCommands.HashDeleteAsync(key, fields, cancellationToken);

    /// <summary>
    /// Determine if a hash field exists.
    /// </summary>
    public ValueTask<bool> HashExistsAsync(string key, string field, CancellationToken cancellationToken = default)
        => _hashCommands.HashExistsAsync(key, field, cancellationToken);

    /// <summary>
    /// Get the number of fields in a hash.
    /// </summary>
    public ValueTask<long> HashLengthAsync(string key, CancellationToken cancellationToken = default)
        => _hashCommands.HashLengthAsync(key, cancellationToken);

    /// <summary>
    /// Get all field names in a hash.
    /// </summary>
    public ValueTask<string[]> HashKeysAsync(string key, CancellationToken cancellationToken = default)
        => _hashCommands.HashKeysAsync(key, cancellationToken);

    /// <summary>
    /// Get all values in a hash.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the server returns a malformed response.</exception>
    public ValueTask<string[]> HashValuesAsync(string key, CancellationToken cancellationToken = default)
        => _hashCommands.HashValuesAsync(key, cancellationToken);

    /// <summary>
    /// Increment the integer value of a hash field by the given number.
    /// </summary>
    public ValueTask<long> HashIncrementAsync(string key, string field, long value = 1, CancellationToken cancellationToken = default)
        => _hashCommands.HashIncrementAsync(key, field, value, cancellationToken);

    #endregion

    #region List Commands

    /// <summary>
    /// Prepend one or more values to a list.
    /// </summary>
    public ValueTask<long> ListLeftPushAsync(string key, string value, CancellationToken cancellationToken = default)
        => _listCommands.LeftPushAsync(key, value, cancellationToken);

    /// <summary>
    /// Prepend multiple values to a list.
    /// </summary>
    public ValueTask<long> ListLeftPushAsync(string key, string[] values, CancellationToken cancellationToken = default)
        => _listCommands.LeftPushAsync(key, values, cancellationToken);

    /// <summary>
    /// Append one or more values to a list.
    /// </summary>
    public ValueTask<long> ListRightPushAsync(string key, string value, CancellationToken cancellationToken = default)
        => _listCommands.RightPushAsync(key, value, cancellationToken);

    /// <summary>
    /// Append multiple values to a list.
    /// </summary>
    public ValueTask<long> ListRightPushAsync(string key, string[] values, CancellationToken cancellationToken = default)
        => _listCommands.RightPushAsync(key, values, cancellationToken);

    /// <summary>
    /// Remove and return the first element of a list.
    /// </summary>
    public ValueTask<string?> ListLeftPopAsync(string key, CancellationToken cancellationToken = default)
        => _listCommands.LeftPopAsync(key, cancellationToken);

    /// <summary>
    /// Remove and return the last element of a list.
    /// </summary>
    public ValueTask<string?> ListRightPopAsync(string key, CancellationToken cancellationToken = default)
        => _listCommands.RightPopAsync(key, cancellationToken);

    /// <summary>
    /// Get a range of elements from a list.
    /// </summary>
    public ValueTask<string[]> ListRangeAsync(string key, long start, long stop, CancellationToken cancellationToken = default)
        => _listCommands.RangeAsync(key, start, stop, cancellationToken);

    /// <summary>
    /// Get the length of a list.
    /// </summary>
    public ValueTask<long> ListLengthAsync(string key, CancellationToken cancellationToken = default)
        => _listCommands.LengthAsync(key, cancellationToken);

    /// <summary>
    /// Get an element from a list by its index.
    /// </summary>
    public ValueTask<string?> ListIndexAsync(string key, long index, CancellationToken cancellationToken = default)
        => _listCommands.IndexAsync(key, index, cancellationToken);

    /// <summary>
    /// Set the value of an element in a list by its index.
    /// </summary>
    public ValueTask ListSetAsync(string key, long index, string value, CancellationToken cancellationToken = default)
        => _listCommands.SetAsync(key, index, value, cancellationToken);

    /// <summary>
    /// Remove elements from a list.
    /// </summary>
    public ValueTask<long> ListRemoveAsync(string key, long count, string value, CancellationToken cancellationToken = default)
        => _listCommands.RemoveAsync(key, count, value, cancellationToken);

    /// <summary>
    /// Trim a list to the specified range.
    /// </summary>
    public ValueTask ListTrimAsync(string key, long start, long stop, CancellationToken cancellationToken = default)
        => _listCommands.TrimAsync(key, start, stop, cancellationToken);

    /// <summary>
    /// Remove and return the first element of a list, blocking until one is available or timeout.
    /// </summary>
    public ValueTask<string?> ListBlockingLeftPopAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)
        => _listCommands.BlockingLeftPopAsync(key, timeout, cancellationToken);

    /// <summary>
    /// Remove and return the last element of a list, blocking until one is available or timeout.
    /// </summary>
    public ValueTask<string?> ListBlockingRightPopAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)
        => _listCommands.BlockingRightPopAsync(key, timeout, cancellationToken);

    #endregion

    #region Set Commands

    /// <summary>
    /// Add one member to a set.
    /// </summary>
    public ValueTask<bool> SetAddAsync(string key, string member, CancellationToken cancellationToken = default)
        => _setCommands.AddAsync(key, member, cancellationToken);

    /// <summary>
    /// Add multiple members to a set.
    /// </summary>
    public ValueTask<long> SetAddAsync(string key, string[] members, CancellationToken cancellationToken = default)
        => _setCommands.AddAsync(key, members, cancellationToken);

    /// <summary>
    /// Remove a member from a set.
    /// </summary>
    public ValueTask<bool> SetRemoveAsync(string key, string member, CancellationToken cancellationToken = default)
        => _setCommands.RemoveAsync(key, member, cancellationToken);

    /// <summary>
    /// Get all members of a set.
    /// </summary>
    public ValueTask<string[]> SetMembersAsync(string key, CancellationToken cancellationToken = default)
        => _setCommands.MembersAsync(key, cancellationToken);

    /// <summary>
    /// Check if a member exists in a set.
    /// </summary>
    public ValueTask<bool> SetContainsAsync(string key, string member, CancellationToken cancellationToken = default)
        => _setCommands.ContainsAsync(key, member, cancellationToken);

    /// <summary>
    /// Get the number of members in a set.
    /// </summary>
    public ValueTask<long> SetLengthAsync(string key, CancellationToken cancellationToken = default)
        => _setCommands.LengthAsync(key, cancellationToken);

    /// <summary>
    /// Remove and return a random member from a set.
    /// </summary>
    public ValueTask<string?> SetPopAsync(string key, CancellationToken cancellationToken = default)
        => _setCommands.PopAsync(key, cancellationToken);

    /// <summary>
    /// Remove and return multiple random members from a set.
    /// </summary>
    public ValueTask<string[]> SetPopAsync(string key, long count, CancellationToken cancellationToken = default)
        => _setCommands.PopAsync(key, count, cancellationToken);

    /// <summary>
    /// Get the intersection of multiple sets.
    /// </summary>
    public ValueTask<string[]> SetIntersectAsync(string[] keys, CancellationToken cancellationToken = default)
        => _setCommands.IntersectAsync(keys, cancellationToken);

    /// <summary>
    /// Get the union of multiple sets.
    /// </summary>
    public ValueTask<string[]> SetUnionAsync(string[] keys, CancellationToken cancellationToken = default)
        => _setCommands.UnionAsync(keys, cancellationToken);

    /// <summary>
    /// Get the difference between the first set and successive sets.
    /// </summary>
    public ValueTask<string[]> SetDifferenceAsync(string[] keys, CancellationToken cancellationToken = default)
        => _setCommands.DifferenceAsync(keys, cancellationToken);

    #endregion

    #region Sorted Set Commands

    /// <summary>
    /// Add a member with a score to a sorted set.
    /// </summary>
    public ValueTask<bool> SortedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken = default)
        => _sortedSetCommands.AddAsync(key, member, score, cancellationToken);

    /// <summary>
    /// Add multiple members with scores to a sorted set.
    /// </summary>
    public ValueTask<long> SortedSetAddAsync(string key, (string member, double score)[] items, CancellationToken cancellationToken = default)
        => _sortedSetCommands.AddAsync(key, items, cancellationToken);

    /// <summary>
    /// Remove a member from a sorted set.
    /// </summary>
    public ValueTask<bool> SortedSetRemoveAsync(string key, string member, CancellationToken cancellationToken = default)
        => _sortedSetCommands.RemoveAsync(key, member, cancellationToken);

    /// <summary>
    /// Get the score of a member in a sorted set.
    /// </summary>
    public ValueTask<double?> SortedSetScoreAsync(string key, string member, CancellationToken cancellationToken = default)
        => _sortedSetCommands.ScoreAsync(key, member, cancellationToken);

    /// <summary>
    /// Get the rank of a member in a sorted set (0-based, ordered by score ascending).
    /// </summary>
    public ValueTask<long?> SortedSetRankAsync(string key, string member, CancellationToken cancellationToken = default)
        => _sortedSetCommands.RankAsync(key, member, cancellationToken);

    /// <summary>
    /// Get a range of members in a sorted set by rank.
    /// </summary>
    public ValueTask<string[]> SortedSetRangeByRankAsync(string key, long start, long stop, CancellationToken cancellationToken = default)
        => _sortedSetCommands.RangeByRankAsync(key, start, stop, cancellationToken);

    /// <summary>
    /// Get a range of members with scores in a sorted set by rank.
    /// </summary>
    public ValueTask<(string member, double score)[]> SortedSetRangeByRankWithScoresAsync(string key, long start, long stop, CancellationToken cancellationToken = default)
        => _sortedSetCommands.RangeByRankWithScoresAsync(key, start, stop, cancellationToken);

    /// <summary>
    /// Get a range of members in a sorted set by score.
    /// </summary>
    public ValueTask<string[]> SortedSetRangeByScoreAsync(string key, double min, double max, CancellationToken cancellationToken = default)
        => _sortedSetCommands.RangeByScoreAsync(key, min, max, cancellationToken);

    /// <summary>
    /// Get the number of members in a sorted set.
    /// </summary>
    public ValueTask<long> SortedSetLengthAsync(string key, CancellationToken cancellationToken = default)
        => _sortedSetCommands.LengthAsync(key, cancellationToken);

    /// <summary>
    /// Increment the score of a member in a sorted set.
    /// </summary>
    public ValueTask<double> SortedSetIncrementAsync(string key, string member, double value, CancellationToken cancellationToken = default)
        => _sortedSetCommands.IncrementAsync(key, member, value, cancellationToken);

    #endregion

    #region Utility Commands

    /// <summary>
    /// Ping the server.
    /// </summary>
    public ValueTask<string> PingAsync(CancellationToken cancellationToken = default)
        => _utilityCommands.PingAsync(cancellationToken);

    /// <summary>
    /// Echo the given string.
    /// </summary>
    public ValueTask<string> EchoAsync(string message, CancellationToken cancellationToken = default)
        => _utilityCommands.EchoAsync(message, cancellationToken);

    #endregion

    #region Transactions

    /// <summary>
    /// Creates a new transaction (MULTI/EXEC).
    /// Commands queued in the transaction are executed atomically.
    /// </summary>
    /// <returns>A transaction object to queue commands.</returns>
    public ITransaction CreateTransaction()
    {
        return new Transactions.ValkeyTransaction(_connection);
    }

    #endregion

    #region Pub/Sub

    /// <summary>
    /// Publishes a message to a channel.
    /// </summary>
    /// <param name="channel">The channel name to publish to.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of clients that received the message.</returns>
    public async ValueTask<long> PublishAsync(string channel, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(channel))
        {
            throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
        }

        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(channel);
            args[1] = Encoding.UTF8.GetBytes(message);

            var response = await _connection.ExecuteCommandAsync(
                CommandBytes.Publish,
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    #endregion

    #region Scripting

    /// <summary>
    /// Evaluates a Lua script on the server.
    /// </summary>
    /// <param name="script">The Lua script to execute.</param>
    /// <param name="keys">Array of keys that the script will access.</param>
    /// <param name="args">Array of additional arguments to pass to the script.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the script execution.</returns>
    async ValueTask<object?> IValkeyDatabase.ScriptEvaluateAsync(string script, string[]? keys, string[]? args, CancellationToken cancellationToken)
    {
        return await ScriptEvaluateAsync(script, keys, args, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Evaluates a Lua script on the server.
    /// </summary>
    /// <param name="script">The Lua script to execute.</param>
    /// <param name="keys">Array of keys that the script will access.</param>
    /// <param name="args">Array of additional arguments to pass to the script.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the script execution.</returns>
    public ValueTask<RespValue> ScriptEvaluateAsync(string script, string[]? keys = null, string[]? args = null, CancellationToken cancellationToken = default)
        => _scriptingCommands.ScriptEvaluateAsync(script, keys, args, cancellationToken);

    /// <summary>
    /// Loads a Lua script into the server's script cache.
    /// </summary>
    /// <param name="script">The Lua script to load.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The SHA1 hash of the script.</returns>
    public ValueTask<string> ScriptLoadAsync(string script, CancellationToken cancellationToken = default)
        => _scriptingCommands.ScriptLoadAsync(script, cancellationToken);

    /// <summary>
    /// Evaluates a previously loaded Lua script using its SHA1 hash.
    /// </summary>
    /// <param name="sha1">The SHA1 hash of the script to execute.</param>
    /// <param name="keys">Array of keys that the script will access.</param>
    /// <param name="args">Array of additional arguments to pass to the script.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the script execution.</returns>
    async ValueTask<object?> IValkeyDatabase.ScriptEvaluateShaAsync(string sha1, string[]? keys, string[]? args, CancellationToken cancellationToken)
    {
        return await ScriptEvaluateShaAsync(sha1, keys, args, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Evaluates a previously loaded Lua script using its SHA1 hash.
    /// </summary>
    /// <param name="sha1">The SHA1 hash of the script to execute.</param>
    /// <param name="keys">Array of keys that the script will access.</param>
    /// <param name="args">Array of additional arguments to pass to the script.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the script execution.</returns>
    public ValueTask<RespValue> ScriptEvaluateShaAsync(string sha1, string[]? keys = null, string[]? args = null, CancellationToken cancellationToken = default)
        => _scriptingCommands.ScriptEvaluateShaAsync(sha1, keys, args, cancellationToken);

    /// <summary>
    /// Checks if scripts exist in the script cache.
    /// </summary>
    /// <param name="sha1Hashes">Array of SHA1 hashes to check.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of booleans indicating if each script exists.</returns>
    public ValueTask<bool[]> ScriptExistsAsync(string[] sha1Hashes, CancellationToken cancellationToken = default)
        => _scriptingCommands.ScriptExistsAsync(sha1Hashes, cancellationToken);

    /// <summary>
    /// Removes all scripts from the script cache.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask ScriptFlushAsync(CancellationToken cancellationToken = default)
        => _scriptingCommands.ScriptFlushAsync(cancellationToken);

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
    public ValueTask<string> StreamAddAsync(string key, Dictionary<string, string> fieldValues, string id = "*", long? maxLength = null, CancellationToken cancellationToken = default)
        => _streamCommands.StreamAddAsync(key, fieldValues, id, maxLength, cancellationToken);

    /// <summary>
    /// Reads entries from one or more streams.
    /// </summary>
    /// <param name="key">The stream key to read from.</param>
    /// <param name="startId">The starting ID (exclusive). Use "0" for all entries or "$" for new entries.</param>
    /// <param name="count">Optional maximum number of entries to return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of stream entries.</returns>
    public ValueTask<StreamEntry[]> StreamReadAsync(string key, string startId = "0", long? count = null, CancellationToken cancellationToken = default)
        => _streamCommands.StreamReadAsync(key, startId, count, cancellationToken);

    /// <summary>
    /// Returns a range of entries from a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="start">Start ID (inclusive). Use "-" for minimum ID.</param>
    /// <param name="end">End ID (inclusive). Use "+" for maximum ID.</param>
    /// <param name="count">Optional maximum number of entries to return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of stream entries.</returns>
    public ValueTask<StreamEntry[]> StreamRangeAsync(string key, string start = "-", string end = "+", long? count = null, CancellationToken cancellationToken = default)
        => _streamCommands.StreamRangeAsync(key, start, end, count, cancellationToken);

    /// <summary>
    /// Gets the number of entries in a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of entries in the stream.</returns>
    public ValueTask<long> StreamLengthAsync(string key, CancellationToken cancellationToken = default)
        => _streamCommands.StreamLengthAsync(key, cancellationToken);

    /// <summary>
    /// Removes entries from a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="ids">Array of entry IDs to delete.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of entries deleted.</returns>
    public ValueTask<long> StreamDeleteAsync(string key, string[] ids, CancellationToken cancellationToken = default)
        => _streamCommands.StreamDeleteAsync(key, ids, cancellationToken);

    /// <summary>
    /// Trims a stream to a specified length.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="maxLength">The maximum length to trim to.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of entries removed.</returns>
    public ValueTask<long> StreamTrimAsync(string key, long maxLength, CancellationToken cancellationToken = default)
        => _streamCommands.StreamTrimAsync(key, maxLength, cancellationToken);

    /// <summary>
    /// Creates a consumer group for a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name.</param>
    /// <param name="startId">Starting ID for the group (use "0" for all entries or "$" for new entries).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask StreamGroupCreateAsync(string key, string groupName, string startId = "$", CancellationToken cancellationToken = default)
        => _streamCommands.StreamGroupCreateAsync(key, groupName, startId, cancellationToken);

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
    public ValueTask<StreamEntry[]> StreamReadGroupAsync(string key, string groupName, string consumerName, string startId = ">", long? count = null, CancellationToken cancellationToken = default)
        => _streamCommands.StreamReadGroupAsync(key, groupName, consumerName, startId, count, cancellationToken);

    /// <summary>
    /// Acknowledges one or more messages as processed in a consumer group.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name.</param>
    /// <param name="ids">Array of entry IDs to acknowledge.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of messages successfully acknowledged.</returns>
    public ValueTask<long> StreamAckAsync(string key, string groupName, string[] ids, CancellationToken cancellationToken = default)
        => _streamCommands.StreamAckAsync(key, groupName, ids, cancellationToken);

    /// <summary>
    /// Destroys a consumer group.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name to destroy.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask StreamGroupDestroyAsync(string key, string groupName, CancellationToken cancellationToken = default)
        => _streamCommands.StreamGroupDestroyAsync(key, groupName, cancellationToken);



    #endregion

    #region Geospatial Commands

    /// <summary>
    /// Adds one or more geospatial items (longitude, latitude, name) to the specified key.
    /// </summary>
    public ValueTask<long> GeoAddAsync(string key, double longitude, double latitude, string member, CancellationToken cancellationToken = default)
        => _geoCommands.GeoAddAsync(key, longitude, latitude, member, cancellationToken);

    /// <summary>
    /// Returns the distance between two members in the geospatial index.
    /// </summary>
    public ValueTask<double?> GeoDistanceAsync(string key, string member1, string member2, Abstractions.Geospatial.GeoUnit unit = Abstractions.Geospatial.GeoUnit.Meters, CancellationToken cancellationToken = default)
        => _geoCommands.GeoDistanceAsync(key, member1, member2, unit, cancellationToken);

    /// <summary>
    /// Returns the position (longitude, latitude) of one or more members.
    /// </summary>
    public ValueTask<Abstractions.Geospatial.GeoPosition?[]> GeoPositionAsync(string key, string[] members, CancellationToken cancellationToken = default)
        => _geoCommands.GeoPositionAsync(key, members, cancellationToken);

    /// <summary>
    /// Returns geohash strings representing the position of one or more members.
    /// </summary>
    public ValueTask<string?[]> GeoHashAsync(string key, string[] members, CancellationToken cancellationToken = default)
        => _geoCommands.GeoHashAsync(key, members, cancellationToken);

    /// <summary>
    /// Searches for members within a radius from a given longitude/latitude coordinate.
    /// </summary>
    public ValueTask<Abstractions.Geospatial.GeoRadiusResult[]> GeoRadiusAsync(string key, double longitude, double latitude, double radius, Abstractions.Geospatial.GeoUnit unit = Abstractions.Geospatial.GeoUnit.Meters, long? count = null, bool withDistance = false, bool withCoordinates = false, bool withHash = false, CancellationToken cancellationToken = default)
        => _geoCommands.GeoRadiusAsync(key, longitude, latitude, radius, unit, count, withDistance, withCoordinates, withHash, cancellationToken);

    /// <summary>
    /// Searches for members within a radius from an existing member.
    /// </summary>
    public ValueTask<Abstractions.Geospatial.GeoRadiusResult[]> GeoRadiusByMemberAsync(string key, string member, double radius, Abstractions.Geospatial.GeoUnit unit = Abstractions.Geospatial.GeoUnit.Meters, long? count = null, bool withDistance = false, bool withCoordinates = false, bool withHash = false, CancellationToken cancellationToken = default)
        => _geoCommands.GeoRadiusByMemberAsync(key, member, radius, unit, count, withDistance, withCoordinates, withHash, cancellationToken);

    /// <summary>
    /// Searches for members within a polygon-shaped area. (Valkey 9.0+)
    /// </summary>
    public ValueTask<Abstractions.Geospatial.GeoRadiusResult[]> GeoSearchByPolygonAsync(string key, Abstractions.Geospatial.GeoPosition[] polygon, long? count = null, bool withDistance = false, bool withCoordinates = false, bool withHash = false, CancellationToken cancellationToken = default)
        => _geoCommands.GeoSearchByPolygonAsync(key, polygon, count, withDistance, withCoordinates, withHash, cancellationToken);

    #endregion
}
