using Valkey.Protocol;

namespace Valkey.Commands;

/// <summary>
/// Executor for generic key commands (DEL, EXISTS, EXPIRE).
/// </summary>
internal sealed class KeyCommandExecutor : CommandExecutorBase
{
    internal KeyCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Delete a key.
    /// </summary>
    internal async ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            var response = await ExecuteAsync(
                CommandBytes.Del,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var count) && count > 0;
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Delete multiple keys.
    /// </summary>
    internal async ValueTask<long> DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        if (keys == null || keys.Length == 0)
        {
            throw new ArgumentException("Keys cannot be null or empty", nameof(keys));
        }

        var buffers = new byte[keys.Length][];
        var lengths = new int[keys.Length];
        var args = ArgumentArrayPool.Rent(keys.Length);

        try
        {
            for (int i = 0; i < keys.Length; i++)
            {
                (buffers[i], lengths[i]) = CommandBuilder.EncodeKey(keys[i]);
            }

            for (int i = 0; i < keys.Length; i++)
            {
                args[i] = CommandBuilder.AsMemory(buffers[i], lengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Del,
                args,
                keys.Length,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            foreach (var buffer in buffers)
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
    /// Check if a key exists.
    /// </summary>
    internal async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            var response = await ExecuteAsync(
                CommandBytes.Exists,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var count) && count > 0;
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Set a key's time to live in seconds.
    /// </summary>
    internal async ValueTask<bool> ExpireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (expiryBuffer, expiryLength) = CommandBuilder.EncodeLong((int)expiry.TotalSeconds);
        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(expiryBuffer, expiryLength);

            var response = await ExecuteAsync(
                CommandBytes.Expire,
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var result) && result == 1;
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, expiryBuffer);
            ArgumentArrayPool.Return(args);
        }
    }
}
