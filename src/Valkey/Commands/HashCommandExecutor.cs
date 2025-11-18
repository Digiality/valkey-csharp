using Valkey.Protocol;

namespace Valkey.Commands;

/// <summary>
/// Executes Hash commands for Valkey/Redis.
/// </summary>
internal sealed class HashCommandExecutor : CommandExecutorBase
{
    internal HashCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Set the value of a hash field.
    /// </summary>
    internal async ValueTask<bool> HashSetAsync(string key, string field, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (fieldBuffer, fieldLength) = CommandBuilder.EncodeValue(field);
        var (valueBuffer, valueLength) = CommandBuilder.EncodeValue(value);
        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(fieldBuffer, fieldLength);
            args[2] = CommandBuilder.AsMemory(valueBuffer, valueLength);

            var response = await ExecuteAsync(
                CommandBytes.Hset,
                args,
                3,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var result) && result >= 0;
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, fieldBuffer, valueBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get the value of a hash field.
    /// </summary>
    internal async ValueTask<string?> HashGetAsync(string key, string field, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (fieldBuffer, fieldLength) = CommandBuilder.EncodeValue(field);
        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(fieldBuffer, fieldLength);

            var response = await ExecuteAsync(
                CommandBytes.Hget,
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
            CommandBuilder.Return(keyBuffer, fieldBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get all fields and values in a hash.
    /// Returns an empty dictionary if the key does not exist.
    /// </summary>
    /// <param name="key">The key of the hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary containing all field-value pairs.</returns>
    /// <exception cref="ArgumentException">Thrown when key is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the server returns a malformed response.</exception>
    internal async ValueTask<Dictionary<string, string>> HashGetAllAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, nameof(key));

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            var response = await ExecuteAsync(
                CommandBytes.Hgetall,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            var result = new Dictionary<string, string>();

            // HGETALL returns a flat array: [field1, value1, field2, value2, ...]
            if (response.TryGetArray(out var array))
            {
                // Validate even-length array for field-value pairs
                ValidateEvenArrayLength(array, "HGETALL");

                for (int i = 0; i < array.Length; i += 2)
                {
                    if (!array[i].TryGetString(out var field))
                    {
                        throw new InvalidOperationException($"HGETALL: Expected string field at index {i}, got {array[i].Type}");
                    }
                    if (!array[i + 1].TryGetString(out var value))
                    {
                        throw new InvalidOperationException($"HGETALL: Expected string value at index {i + 1}, got {array[i + 1].Type}");
                    }
                    result[field!] = value!;
                }
            }
            else if (response.TryGetMap(out var map))
            {
                // RESP3 may return a Map type
                foreach (var kvp in map)
                {
                    var field = SafeGetString(kvp.Key, "HGETALL map key");
                    var value = SafeGetString(kvp.Value, "HGETALL map value");
                    result[field] = value;
                }
            }
            else if (!response.IsNull)
            {
                throw new InvalidOperationException($"HGETALL: Expected array or map response, got {response.Type}");
            }

            return result;
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Delete one or more hash fields.
    /// </summary>
    internal async ValueTask<bool> HashDeleteAsync(string key, string field, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (fieldBuffer, fieldLength) = CommandBuilder.EncodeValue(field);
        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(fieldBuffer, fieldLength);

            var response = await ExecuteAsync(
                CommandBytes.Hdel,
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var count) && count > 0;
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, fieldBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Delete multiple hash fields.
    /// </summary>
    internal async ValueTask<long> HashDeleteAsync(string key, string[] fields, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (fields == null || fields.Length == 0)
        {
            throw new ArgumentException("Fields cannot be null or empty", nameof(fields));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var fieldBuffers = new byte[fields.Length][];
        var fieldLengths = new int[fields.Length];
        var args = ArgumentArrayPool.Rent(fields.Length + 1);

        try
        {
            for (int i = 0; i < fields.Length; i++)
            {
                (fieldBuffers[i], fieldLengths[i]) = CommandBuilder.EncodeValue(fields[i]);
            }

            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            for (int i = 0; i < fields.Length; i++)
            {
                args[i + 1] = CommandBuilder.AsMemory(fieldBuffers[i], fieldLengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Hdel,
                args,
                fields.Length + 1,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            foreach (var buffer in fieldBuffers)
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
    /// Determine if a hash field exists.
    /// </summary>
    internal async ValueTask<bool> HashExistsAsync(string key, string field, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (fieldBuffer, fieldLength) = CommandBuilder.EncodeValue(field);
        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(fieldBuffer, fieldLength);

            var response = await ExecuteAsync(
                CommandBytes.Hexists,
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var result) && result == 1;
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, fieldBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get the number of fields in a hash.
    /// </summary>
    internal async ValueTask<long> HashLengthAsync(string key, CancellationToken cancellationToken = default)
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
                CommandBytes.Hlen,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get all field names in a hash.
    /// </summary>
    internal async ValueTask<string[]> HashKeysAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, nameof(key));

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            var response = await ExecuteAsync(
                CommandBytes.Hkeys,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            return SafeConvertArrayToStrings(response, "HKEYS");
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get all values in a hash.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the server returns a malformed response.</exception>
    internal async ValueTask<string[]> HashValuesAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, nameof(key));

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            var response = await ExecuteAsync(
                CommandBytes.Hvals,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            return SafeConvertArrayToStrings(response, "HVALS");
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Increment the integer value of a hash field by the given number.
    /// </summary>
    internal async ValueTask<long> HashIncrementAsync(string key, string field, long value = 1, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, nameof(key));

        if (string.IsNullOrEmpty(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (fieldBuffer, fieldLength) = CommandBuilder.EncodeValue(field);
        var (valueBuffer, valueLength) = CommandBuilder.EncodeLong(value);
        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(fieldBuffer, fieldLength);
            args[2] = CommandBuilder.AsMemory(valueBuffer, valueLength);

            var response = await ExecuteAsync(
                CommandBytes.Hincrby,
                args,
                3,
                cancellationToken).ConfigureAwait(false);

            return SafeGetInteger(response, "HINCRBY");
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, fieldBuffer, valueBuffer);
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Set multiple hash fields to multiple values.
    /// </summary>
    internal async ValueTask<bool> HashSetAsync(string key, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (fieldValues == null || fieldValues.Count == 0)
        {
            throw new ArgumentException("Field values cannot be null or empty", nameof(fieldValues));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var buffers = new byte[fieldValues.Count * 2][];
        var lengths = new int[fieldValues.Count * 2];
        var args = ArgumentArrayPool.Rent(fieldValues.Count * 2 + 1);

        try
        {
            int idx = 0;
            foreach (var kvp in fieldValues)
            {
                (buffers[idx], lengths[idx]) = CommandBuilder.EncodeValue(kvp.Key);
                idx++;
                (buffers[idx], lengths[idx]) = CommandBuilder.EncodeValue(kvp.Value);
                idx++;
            }

            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            for (int i = 0; i < buffers.Length; i++)
            {
                args[i + 1] = CommandBuilder.AsMemory(buffers[i], lengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Hset,
                args,
                fieldValues.Count * 2 + 1,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var result) && result >= 0;
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
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
}
