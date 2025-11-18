using System.Text;
using Valkey.Protocol;
using Valkey.ResponseParsers;

namespace Valkey.Commands;

/// <summary>
/// Executor for Sorted Set commands (ZADD, ZREM, ZRANGE, ZSCORE, etc.).
/// </summary>
internal sealed class SortedSetCommandExecutor : CommandExecutorBase
{
    internal SortedSetCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Add a member with a score to a sorted set.
    /// </summary>
    internal async ValueTask<bool> AddAsync(string key, string member, double score, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (member == null)
        {
            throw new ArgumentNullException(nameof(member));
        }

        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(score.ToString("G17"));
            args[2] = Encoding.UTF8.GetBytes(member);

            var response = await ExecuteAsync(
                CommandBytes.Zadd,
                args,
                3,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var count) && count > 0;
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Add multiple members with scores to a sorted set.
    /// </summary>
    internal async ValueTask<long> AddAsync(string key, (string member, double score)[] items, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (items == null || items.Length == 0)
        {
            throw new ArgumentException("Items cannot be null or empty", nameof(items));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var argCount = 1 + (items.Length * 2);
        var args = ArgumentArrayPool.Rent(argCount);

        var scoreBuffers = new byte[items.Length][];
        var scoreLengths = new int[items.Length];
        var memberBuffers = new byte[items.Length][];
        var memberLengths = new int[items.Length];

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            for (int i = 0; i < items.Length; i++)
            {
                (scoreBuffers[i], scoreLengths[i]) = CommandBuilder.EncodeDouble(items[i].score);
                (memberBuffers[i], memberLengths[i]) = CommandBuilder.EncodeValue(items[i].member);

                args[1 + (i * 2)] = CommandBuilder.AsMemory(scoreBuffers[i], scoreLengths[i]);
                args[1 + (i * 2) + 1] = CommandBuilder.AsMemory(memberBuffers[i], memberLengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Zadd,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            for (int i = 0; i < items.Length; i++)
            {
                if (scoreBuffers[i] != null)
                {
                    CommandBuilder.Return(scoreBuffers[i], memberBuffers[i]);
                }
            }
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Remove a member from a sorted set.
    /// </summary>
    internal async ValueTask<bool> RemoveAsync(string key, string member, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (member == null)
        {
            throw new ArgumentNullException(nameof(member));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(member);

            var response = await ExecuteAsync(
                "ZREM"u8.ToArray(),
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var count) && count > 0;
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get the score of a member in a sorted set.
    /// </summary>
    internal async ValueTask<double?> ScoreAsync(string key, string member, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (member == null)
        {
            throw new ArgumentNullException(nameof(member));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(member);

            var response = await ExecuteAsync(
                "ZSCORE"u8.ToArray(),
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return null;
            }

            if (response.TryGetDouble(out var score))
            {
                return score;
            }

            // Try parsing as string (some servers return strings)
            if (response.TryGetString(out var scoreStr) && double.TryParse(scoreStr, out var parsedScore))
            {
                return parsedScore;
            }

            return null;
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get the rank of a member in a sorted set (0-based, ordered by score ascending).
    /// </summary>
    internal async ValueTask<long?> RankAsync(string key, string member, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (member == null)
        {
            throw new ArgumentNullException(nameof(member));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(member);

            var response = await ExecuteAsync(
                "ZRANK"u8.ToArray(),
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return null;
            }

            return response.AsInteger();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get a range of members in a sorted set by rank.
    /// </summary>
    internal async ValueTask<string[]> RangeByRankAsync(string key, long start, long stop, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(start.ToString());
            args[2] = Encoding.UTF8.GetBytes(stop.ToString());

            var response = await ExecuteAsync(
                CommandBytes.Zrange,
                args,
                3,
                cancellationToken).ConfigureAwait(false);

            var array = response.AsArray();
            return array.Select(v => v.AsString()).ToArray();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get a range of members with scores in a sorted set by rank.
    /// </summary>
    internal async ValueTask<(string member, double score)[]> RangeByRankWithScoresAsync(string key, long start, long stop, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var args = ArgumentArrayPool.Rent(4);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(start.ToString());
            args[2] = Encoding.UTF8.GetBytes(stop.ToString());
            args[3] = CommandBytes.Withscores;

            var response = await ExecuteAsync(
                CommandBytes.Zrange,
                args,
                4,
                cancellationToken).ConfigureAwait(false);

            return SortedSetWithScoresParser.Parse(response);
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get a range of members in a sorted set by score.
    /// </summary>
    internal async ValueTask<string[]> RangeByScoreAsync(string key, double min, double max, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(min.ToString("G17"));
            args[2] = Encoding.UTF8.GetBytes(max.ToString("G17"));

            var response = await ExecuteAsync(
                "ZRANGEBYSCORE"u8.ToArray(),
                args,
                3,
                cancellationToken).ConfigureAwait(false);

            var array = response.AsArray();
            return array.Select(v => v.AsString()).ToArray();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get the number of members in a sorted set.
    /// </summary>
    internal async ValueTask<long> LengthAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);

            var response = await ExecuteAsync(
                "ZCARD"u8.ToArray(),
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Increment the score of a member in a sorted set.
    /// </summary>
    internal async ValueTask<double> IncrementAsync(string key, string member, double value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (member == null)
        {
            throw new ArgumentNullException(nameof(member));
        }

        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(value.ToString("G17"));
            args[2] = Encoding.UTF8.GetBytes(member);

            var response = await ExecuteAsync(
                "ZINCRBY"u8.ToArray(),
                args,
                3,
                cancellationToken).ConfigureAwait(false);

            // Response is a Double in RESP3, or bulk string in RESP2
            if (response.TryGetDouble(out var doubleValue))
            {
                return doubleValue;
            }
            else if (response.TryGetString(out var scoreStr))
            {
                return double.Parse(scoreStr);
            }

            throw new InvalidOperationException($"Unexpected response type: {response.Type}");
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }
}
