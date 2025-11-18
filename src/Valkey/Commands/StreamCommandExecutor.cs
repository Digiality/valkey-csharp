using System.Text;
using Valkey.Abstractions.Streams;
using Valkey.Protocol;
using Valkey.ResponseParsers;

namespace Valkey.Commands;

/// <summary>
/// Executor for Stream commands.
/// </summary>
internal sealed class StreamCommandExecutor : CommandExecutorBase
{
    internal StreamCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Appends a new entry to a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="fieldValues">Field-value pairs to add to the stream entry.</param>
    /// <param name="id">Optional entry ID (use "*" for auto-generation).</param>
    /// <param name="maxLength">Optional maximum stream length (trimming).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The ID of the added entry.</returns>
    internal async ValueTask<string> StreamAddAsync(string key, Dictionary<string, string> fieldValues, string id = "*", long? maxLength = null, CancellationToken cancellationToken = default)
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
        var (idBuffer, idLength) = CommandBuilder.EncodeValue(id);

        // Calculate argument count: key + [MAXLEN + maxLength] + id + (field + value) * count
        var argCount = 2 + (maxLength.HasValue ? 2 : 0) + (fieldValues.Count * 2);
        var args = ArgumentArrayPool.Rent(argCount);

        byte[]? maxLengthBuffer = null;
        int maxLengthLength = 0;

        // Allocate buffers for field-value pairs
        var fieldBuffers = new byte[fieldValues.Count][];
        var fieldLengths = new int[fieldValues.Count];
        var valueBuffers = new byte[fieldValues.Count][];
        var valueLengths = new int[fieldValues.Count];

        try
        {
            int argIndex = 0;
            args[argIndex++] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            // Add MAXLEN if specified
            if (maxLength.HasValue)
            {
                args[argIndex++] = CommandBytes.Maxlen;
                (maxLengthBuffer, maxLengthLength) = CommandBuilder.EncodeLong(maxLength.Value);
                args[argIndex++] = CommandBuilder.AsMemory(maxLengthBuffer, maxLengthLength);
            }

            // Add ID
            args[argIndex++] = CommandBuilder.AsMemory(idBuffer, idLength);

            // Add field-value pairs
            int pairIndex = 0;
            foreach (var kvp in fieldValues)
            {
                (fieldBuffers[pairIndex], fieldLengths[pairIndex]) = CommandBuilder.EncodeValue(kvp.Key);
                (valueBuffers[pairIndex], valueLengths[pairIndex]) = CommandBuilder.EncodeValue(kvp.Value);

                args[argIndex++] = CommandBuilder.AsMemory(fieldBuffers[pairIndex], fieldLengths[pairIndex]);
                args[argIndex++] = CommandBuilder.AsMemory(valueBuffers[pairIndex], valueLengths[pairIndex]);
                pairIndex++;
            }

            var response = await ExecuteAsync(
                CommandBytes.Xadd,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response.AsString();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, idBuffer);
            if (maxLengthBuffer != null)
            {
                CommandBuilder.Return(maxLengthBuffer);
            }

            // Return all field-value buffers
            for (int i = 0; i < fieldBuffers.Length; i++)
            {
                if (fieldBuffers[i] != null)
                {
                    CommandBuilder.Return(fieldBuffers[i], valueBuffers[i]);
                }
            }

            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Reads entries from one or more streams.
    /// </summary>
    /// <param name="key">The stream key to read from.</param>
    /// <param name="startId">The starting ID (exclusive). Use "0" for all entries or "$" for new entries.</param>
    /// <param name="count">Optional maximum number of entries to return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of stream entries.</returns>
    internal async ValueTask<StreamEntry[]> StreamReadAsync(string key, string startId = "0", long? count = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(startId))
        {
            throw new ArgumentException("Start ID cannot be null or empty", nameof(startId));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (startIdBuffer, startIdLength) = CommandBuilder.EncodeValue(startId);

        var argCount = count.HasValue ? 5 : 3;
        var args = ArgumentArrayPool.Rent(argCount);

        byte[]? countBuffer = null;
        int countLength = 0;

        try
        {
            int argIndex = 0;

            // Add COUNT if specified
            if (count.HasValue)
            {
                args[argIndex++] = CommandBytes.Count;
                (countBuffer, countLength) = CommandBuilder.EncodeLong(count.Value);
                args[argIndex++] = CommandBuilder.AsMemory(countBuffer, countLength);
            }

            // Add STREAMS keyword
            args[argIndex++] = CommandBytes.Streams;
            args[argIndex++] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[argIndex++] = CommandBuilder.AsMemory(startIdBuffer, startIdLength);

            var response = await ExecuteAsync(
                CommandBytes.Xread,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return Array.Empty<StreamEntry>();
            }

            return StreamParsers.ParseReadResponse(response);
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, startIdBuffer);
            if (countBuffer != null)
            {
                CommandBuilder.Return(countBuffer);
            }
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Returns a range of entries from a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="start">Start ID (inclusive). Use "-" for minimum ID.</param>
    /// <param name="end">End ID (inclusive). Use "+" for maximum ID.</param>
    /// <param name="count">Optional maximum number of entries to return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of stream entries.</returns>
    internal async ValueTask<StreamEntry[]> StreamRangeAsync(string key, string start = "-", string end = "+", long? count = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (startBuffer, startLength) = CommandBuilder.EncodeValue(start);
        var (endBuffer, endLength) = CommandBuilder.EncodeValue(end);

        var argCount = count.HasValue ? 5 : 3;
        var args = ArgumentArrayPool.Rent(argCount);

        byte[]? countBuffer = null;
        int countLength = 0;

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(startBuffer, startLength);
            args[2] = CommandBuilder.AsMemory(endBuffer, endLength);

            if (count.HasValue)
            {
                args[3] = CommandBytes.Count;
                (countBuffer, countLength) = CommandBuilder.EncodeLong(count.Value);
                args[4] = CommandBuilder.AsMemory(countBuffer, countLength);
            }

            var response = await ExecuteAsync(
                CommandBytes.Xrange,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return StreamParsers.ParseEntries(response);
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, startBuffer, endBuffer);
            if (countBuffer != null)
            {
                CommandBuilder.Return(countBuffer);
            }
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Gets the number of entries in a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of entries in the stream.</returns>
    internal async ValueTask<long> StreamLengthAsync(string key, CancellationToken cancellationToken = default)
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
                "XLEN"u8.ToArray(),
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
    /// Removes entries from a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="ids">Array of entry IDs to delete.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of entries deleted.</returns>
    internal async ValueTask<long> StreamDeleteAsync(string key, string[] ids, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (ids == null || ids.Length == 0)
        {
            throw new ArgumentException("IDs cannot be null or empty", nameof(ids));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var argCount = 1 + ids.Length;
        var args = ArgumentArrayPool.Rent(argCount);

        var idBuffers = new byte[ids.Length][];
        var idLengths = new int[ids.Length];

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);

            for (int i = 0; i < ids.Length; i++)
            {
                (idBuffers[i], idLengths[i]) = CommandBuilder.EncodeValue(ids[i]);
                args[i + 1] = CommandBuilder.AsMemory(idBuffers[i], idLengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Xdel,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            foreach (var buffer in idBuffers)
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
    /// Trims a stream to a specified length.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="maxLength">The maximum length to trim to.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of entries removed.</returns>
    internal async ValueTask<long> StreamTrimAsync(string key, long maxLength, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = CommandBytes.Maxlen;
            args[2] = Encoding.UTF8.GetBytes(maxLength.ToString());

            var response = await ExecuteAsync(
                CommandBytes.Xtrim,
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
    /// Creates a consumer group for a stream.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name.</param>
    /// <param name="startId">Starting ID for the group (use "0" for all entries or "$" for new entries).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async ValueTask StreamGroupCreateAsync(string key, string groupName, string startId = "$", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException("Group name cannot be null or empty", nameof(groupName));
        }

        var args = ArgumentArrayPool.Rent(5);
        try
        {
            args[0] = CommandBytes.Create;
            args[1] = Encoding.UTF8.GetBytes(key);
            args[2] = Encoding.UTF8.GetBytes(groupName);
            args[3] = Encoding.UTF8.GetBytes(startId);
            args[4] = CommandBytes.Mkstream;

            await ExecuteAsync(
                CommandBytes.Xgroup,
                args,
                5,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

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
    internal async ValueTask<StreamEntry[]> StreamReadGroupAsync(string key, string groupName, string consumerName, string startId = ">", long? count = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException("Group name cannot be null or empty", nameof(groupName));
        }

        if (string.IsNullOrEmpty(consumerName))
        {
            throw new ArgumentException("Consumer name cannot be null or empty", nameof(consumerName));
        }

        var (groupNameBuffer, groupNameLength) = CommandBuilder.EncodeValue(groupName);
        var (consumerNameBuffer, consumerNameLength) = CommandBuilder.EncodeValue(consumerName);
        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (startIdBuffer, startIdLength) = CommandBuilder.EncodeValue(startId);

        var argCount = count.HasValue ? 8 : 6;
        var args = ArgumentArrayPool.Rent(argCount);

        byte[]? countBuffer = null;
        int countLength = 0;

        try
        {
            int argIndex = 0;

            args[argIndex++] = CommandBytes.Group;
            args[argIndex++] = CommandBuilder.AsMemory(groupNameBuffer, groupNameLength);
            args[argIndex++] = CommandBuilder.AsMemory(consumerNameBuffer, consumerNameLength);

            if (count.HasValue)
            {
                args[argIndex++] = CommandBytes.Count;
                (countBuffer, countLength) = CommandBuilder.EncodeLong(count.Value);
                args[argIndex++] = CommandBuilder.AsMemory(countBuffer, countLength);
            }

            args[argIndex++] = CommandBytes.Streams;
            args[argIndex++] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[argIndex++] = CommandBuilder.AsMemory(startIdBuffer, startIdLength);

            var response = await ExecuteAsync(
                CommandBytes.Xreadgroup,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            if (response.IsNull)
            {
                return Array.Empty<StreamEntry>();
            }

            return StreamParsers.ParseReadResponse(response);
        }
        finally
        {
            CommandBuilder.Return(groupNameBuffer, consumerNameBuffer, keyBuffer, startIdBuffer);
            if (countBuffer != null)
            {
                CommandBuilder.Return(countBuffer);
            }
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Acknowledges one or more messages as processed in a consumer group.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name.</param>
    /// <param name="ids">Array of entry IDs to acknowledge.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The number of messages successfully acknowledged.</returns>
    internal async ValueTask<long> StreamAckAsync(string key, string groupName, string[] ids, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException("Group name cannot be null or empty", nameof(groupName));
        }

        if (ids == null || ids.Length == 0)
        {
            throw new ArgumentException("IDs cannot be null or empty", nameof(ids));
        }

        var (keyBuffer, keyLength) = CommandBuilder.EncodeKey(key);
        var (groupNameBuffer, groupNameLength) = CommandBuilder.EncodeValue(groupName);
        var argCount = 2 + ids.Length;
        var args = ArgumentArrayPool.Rent(argCount);

        var idBuffers = new byte[ids.Length][];
        var idLengths = new int[ids.Length];

        try
        {
            args[0] = CommandBuilder.AsMemory(keyBuffer, keyLength);
            args[1] = CommandBuilder.AsMemory(groupNameBuffer, groupNameLength);

            for (int i = 0; i < ids.Length; i++)
            {
                (idBuffers[i], idLengths[i]) = CommandBuilder.EncodeValue(ids[i]);
                args[i + 2] = CommandBuilder.AsMemory(idBuffers[i], idLengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Xack,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer, groupNameBuffer);
            foreach (var buffer in idBuffers)
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
    /// Destroys a consumer group.
    /// </summary>
    /// <param name="key">The stream key.</param>
    /// <param name="groupName">The consumer group name to destroy.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async ValueTask StreamGroupDestroyAsync(string key, string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException("Group name cannot be null or empty", nameof(groupName));
        }

        var args = ArgumentArrayPool.Rent(3);
        try
        {
            args[0] = CommandBytes.Destroy;
            args[1] = Encoding.UTF8.GetBytes(key);
            args[2] = Encoding.UTF8.GetBytes(groupName);

            await ExecuteAsync(
                CommandBytes.Xgroup,
                args,
                3,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }
}
