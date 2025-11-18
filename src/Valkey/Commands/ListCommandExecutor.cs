using System.Text;
using Valkey.Protocol;

namespace Valkey.Commands;

/// <summary>
/// Executor for List commands (LPUSH, RPUSH, LPOP, RPOP, LRANGE, LLEN, etc.).
/// </summary>
internal sealed class ListCommandExecutor : CommandExecutorBase
{
    internal ListCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Prepend one or more values to a list.
    /// </summary>
    internal async ValueTask<long> LeftPushAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (valueBuffer, valueLength) = CommandBuilder.EncodeValue(value);
        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(valueBuffer, valueLength);

            var response = await ExecuteAsync(
                CommandBytes.Lpush,
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, valueBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Prepend multiple values to a list.
    /// </summary>
    internal async ValueTask<long> LeftPushAsync(string key, string[] values, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (values == null || values.Length == 0)
        {
            throw new ArgumentException("Values cannot be null or empty", nameof(values));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var argCount = 1 + values.Length;
        var args = ArgumentArrayPool.Rent(argCount);

        var valueBuffers = new byte[values.Length][];
        var valueLengths = new int[values.Length];

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            for (int i = 0; i < values.Length; i++)
            {
                (valueBuffers[i], valueLengths[i]) = CommandBuilder.EncodeValue(values[i]);
                args[i + 1] = CommandBuilder.AsMemory(valueBuffers[i], valueLengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Lpush,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            foreach (var buffer in valueBuffers)
            {
                if (buffer != null)
                {
                    CommandBuilder.Return(buffer);
                }
            }
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Append one or more values to a list.
    /// </summary>
    internal async ValueTask<long> RightPushAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(value);

            var response = await ExecuteAsync(
                CommandBytes.Rpush,
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

    /// <summary>
    /// Append multiple values to a list.
    /// </summary>
    internal async ValueTask<long> RightPushAsync(string key, string[] values, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (values == null || values.Length == 0)
        {
            throw new ArgumentException("Values cannot be null or empty", nameof(values));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var argCount = 1 + values.Length;
        var args = ArgumentArrayPool.Rent(argCount);

        var valueBuffers = new byte[values.Length][];
        var valueLengths = new int[values.Length];

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            for (int i = 0; i < values.Length; i++)
            {
                (valueBuffers[i], valueLengths[i]) = CommandBuilder.EncodeValue(values[i]);
                args[i + 1] = CommandBuilder.AsMemory(valueBuffers[i], valueLengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Rpush,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            foreach (var buffer in valueBuffers)
            {
                if (buffer != null)
                {
                    CommandBuilder.Return(buffer);
                }
            }
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Remove and return the first element of a list.
    /// </summary>
    internal async ValueTask<string?> LeftPopAsync(string key, CancellationToken cancellationToken = default)
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
                "LPOP"u8.ToArray(),
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return null;
            }

            return response.AsString();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Remove and return the last element of a list.
    /// </summary>
    internal async ValueTask<string?> RightPopAsync(string key, CancellationToken cancellationToken = default)
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
                "RPOP"u8.ToArray(),
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return null;
            }

            return response.AsString();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get a range of elements from a list.
    /// </summary>
    internal async ValueTask<string[]> RangeAsync(string key, long start, long stop, CancellationToken cancellationToken = default)
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
                "LRANGE"u8.ToArray(),
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
    /// Get the length of a list.
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
                "LLEN"u8.ToArray(),
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
    /// Get an element from a list by its index.
    /// </summary>
    internal async ValueTask<string?> IndexAsync(string key, long index, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(index.ToString());

            var response = await ExecuteAsync(
                "LINDEX"u8.ToArray(),
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return null;
            }

            return response.AsString();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Set the value of an element in a list by its index.
    /// </summary>
    internal async ValueTask SetAsync(string key, long index, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(index.ToString());
            args[2] = Encoding.UTF8.GetBytes(value);

            await ExecuteAsync(
                "LSET"u8.ToArray(),
                args,
                3,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Remove elements from a list.
    /// </summary>
    internal async ValueTask<long> RemoveAsync(string key, long count, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(count.ToString());
            args[2] = Encoding.UTF8.GetBytes(value);

            var response = await ExecuteAsync(
                "LREM"u8.ToArray(),
                args,
                3,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Trim a list to the specified range.
    /// </summary>
    internal async ValueTask TrimAsync(string key, long start, long stop, CancellationToken cancellationToken = default)
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

            await ExecuteAsync(
                "LTRIM"u8.ToArray(),
                args,
                3,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Remove and return the first element of a list, blocking until one is available or timeout.
    /// </summary>
    internal async ValueTask<string?> BlockingLeftPopAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(((int)timeout.TotalSeconds).ToString());

            var response = await ExecuteAsync(
                "BLPOP"u8.ToArray(),
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return null;
            }

            // BLPOP returns [key, value] or null
            if (response.TryGetArray(out var array) && array.Length >= 2)
            {
                return array[1].AsString();
            }

            return null;
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Remove and return the last element of a list, blocking until one is available or timeout.
    /// </summary>
    internal async ValueTask<string?> BlockingRightPopAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(((int)timeout.TotalSeconds).ToString());

            var response = await ExecuteAsync(
                "BRPOP"u8.ToArray(),
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return null;
            }

            // BRPOP returns [key, value] or null
            if (response.TryGetArray(out var array) && array.Length >= 2)
            {
                return array[1].AsString();
            }

            return null;
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }
}
