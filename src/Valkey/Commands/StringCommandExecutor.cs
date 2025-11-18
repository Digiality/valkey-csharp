using Valkey.Protocol;

namespace Valkey.Commands;

/// <summary>
/// Executor for String commands (GET, SET, INCR, DECR, etc.).
/// </summary>
internal sealed class StringCommandExecutor : CommandExecutorBase
{
    internal StringCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Get the value of a key.
    /// </summary>
    internal async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
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
                CommandBytes.Get,
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
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get the value of a key as bytes.
    /// </summary>
    internal async ValueTask<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
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
                CommandBytes.Get,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return null;
            }

            // AsBytes() returns ReadOnlyMemory<byte> backed by a byte[] from the parser
            // Try to get the underlying array without additional copy
            var bytes = response.AsBytes();
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(bytes, out var segment))
            {
                // If the memory spans the entire array, return it directly
                if (segment.Offset == 0 && segment.Count == segment.Array!.Length)
                {
                    return segment.Array;
                }
            }

            // Fallback: copy is needed
            return bytes.ToArray();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Set the string value of a key.
    /// </summary>
    internal async ValueTask<bool> SetAsync(
        string key,
        string value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
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
        byte[]? expiryBuffer = null;
        int expiryLength = 0;
        ReadOnlyMemory<byte>[]? args = null;

        try
        {
            if (expiry.HasValue)
            {
                (expiryBuffer, expiryLength) = CommandBuilder.EncodeLong((int)expiry.Value.TotalSeconds);
                args = ArgumentArrayPool.Rent(4);
                args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
                args[1] = CommandBuilder.AsMemory(valueBuffer, valueLength);
                args[2] = CommandBytes.Ex;
                args[3] = CommandBuilder.AsMemory(expiryBuffer, expiryLength);

                var response = await ExecuteAsync(
                    CommandBytes.Set,
                    args,
                    4,
                    cancellationToken).ConfigureAwait(false);

                return response.TryGetString(out var result) && result == "OK";
            }
            else
            {
                args = ArgumentArrayPool.Rent(2);
                args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
                args[1] = CommandBuilder.AsMemory(valueBuffer, valueLength);

                var response = await ExecuteAsync(
                    CommandBytes.Set,
                    args,
                    2,
                    cancellationToken).ConfigureAwait(false);

                return response.TryGetString(out var result) && result == "OK";
            }
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, valueBuffer);
            if (expiryBuffer != null)
            {
                CommandBuilder.Return(expiryBuffer);
            }
            if (args != null)
            {
                ArgumentArrayPool.Return(args);
            }
        }
    }

    /// <summary>
    /// Set the bytes value of a key.
    /// </summary>
    internal async ValueTask<bool> SetAsync(
        string key,
        byte[] value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
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
        var argCount = expiry.HasValue ? 4 : 2;
        var args = ArgumentArrayPool.Rent(argCount);

        byte[]? expiryBuffer = null;
        int expiryLength = 0;

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = value;

            if (expiry.HasValue)
            {
                args[2] = CommandBytes.Ex;
                (expiryBuffer, expiryLength) = CommandBuilder.EncodeLong((int)expiry.Value.TotalSeconds);
                args[3] = CommandBuilder.AsMemory(expiryBuffer, expiryLength);
            }

            var response = await ExecuteAsync(
                CommandBytes.Set,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetString(out var result) && result == "OK";
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            if (expiryBuffer != null)
            {
                CommandBuilder.Return(expiryBuffer);
            }
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Increment the integer value of a key by one.
    /// </summary>
    internal async ValueTask<long> IncrementAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, nameof(key));

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            var response = await ExecuteAsync(
                CommandBytes.Incr,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            return SafeGetInteger(response, "INCR");
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Increment the integer value of a key by the given amount.
    /// </summary>
    internal async ValueTask<long> IncrementAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, nameof(key));

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (valueBuffer, valueLength) = CommandBuilder.EncodeLong(value);
        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(valueBuffer, valueLength);

            var response = await ExecuteAsync(
                CommandBytes.Incrby,
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return SafeGetInteger(response, "INCRBY");
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, valueBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Decrement the integer value of a key by one.
    /// </summary>
    internal async ValueTask<long> DecrementAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, nameof(key));

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            var response = await ExecuteAsync(
                CommandBytes.Decr,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            return SafeGetInteger(response, "DECR");
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Decrement the integer value of a key by the given amount.
    /// </summary>
    internal async ValueTask<long> DecrementAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, nameof(key));

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (valueBuffer, valueLength) = CommandBuilder.EncodeLong(value);
        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(valueBuffer, valueLength);

            var response = await ExecuteAsync(
                CommandBytes.Decrby,
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return SafeGetInteger(response, "DECRBY");
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, valueBuffer);
            ArgumentArrayPool.Return(args);
        }
    }
}
