using System.Text;
using Valkey.Protocol;

namespace Valkey.Commands;

/// <summary>
/// Executor for Set commands (SADD, SREM, SMEMBERS, SINTER, SUNION, etc.).
/// </summary>
internal sealed class SetCommandExecutor : CommandExecutorBase
{
    internal SetCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Add one member to a set.
    /// </summary>
    internal async ValueTask<bool> AddAsync(string key, string member, CancellationToken cancellationToken = default)
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
                CommandBytes.Sadd,
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
    /// Add multiple members to a set.
    /// </summary>
    internal async ValueTask<long> AddAsync(string key, string[] members, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (members == null || members.Length == 0)
        {
            throw new ArgumentException("Members cannot be null or empty", nameof(members));
        }

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
                CommandBytes.Sadd,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response.AsInteger();
        }
        finally
        {
            CommandBuilder.Return(keyBuffer);
            foreach (var buffer in memberBuffers)
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
    /// Remove a member from a set.
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
                "SREM"u8.ToArray(),
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
    /// Get all members of a set.
    /// </summary>
    internal async ValueTask<string[]> MembersAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, nameof(key));

        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);

            var response = await ExecuteAsync(
                CommandBytes.Smembers,
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            // RESP3 might return a Set type, fallback to Array
            if (response.TryGetSet(out var set))
            {
                var result = new string[set.Count];
                int i = 0;
                foreach (var v in set)
                {
                    result[i++] = SafeGetString(v, "SMEMBERS set element");
                }
                return result;
            }

            return SafeConvertArrayToStrings(response, "SMEMBERS");
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Check if a member exists in a set.
    /// </summary>
    internal async ValueTask<bool> ContainsAsync(string key, string member, CancellationToken cancellationToken = default)
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
                "SISMEMBER"u8.ToArray(),
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return response.TryGetInteger(out var result) && result == 1;
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get the number of members in a set.
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
                "SCARD"u8.ToArray(),
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
    /// Remove and return a random member from a set.
    /// </summary>
    internal async ValueTask<string?> PopAsync(string key, CancellationToken cancellationToken = default)
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
                "SPOP"u8.ToArray(),
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
    /// Remove and return multiple random members from a set.
    /// </summary>
    internal async ValueTask<string[]> PopAsync(string key, long count, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(key);
            args[1] = Encoding.UTF8.GetBytes(count.ToString());

            var response = await ExecuteAsync(
                "SPOP"u8.ToArray(),
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            // SPOP with count returns a Set in RESP3, or Array in RESP2
            if (response.TryGetSet(out var set))
            {
                return set.Select(v => v.AsString()).ToArray();
            }
            else if (response.TryGetArray(out var array))
            {
                return array.Select(v => v.AsString()).ToArray();
            }

            throw new InvalidOperationException($"Unexpected response type: {response.Type}");
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Get the intersection of multiple sets.
    /// </summary>
    internal async ValueTask<string[]> IntersectAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        if (keys == null || keys.Length == 0)
        {
            throw new ArgumentException("Keys cannot be null or empty", nameof(keys));
        }

        var args = keys.Select(k => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(k)).ToArray();

        var response = await ExecuteAsync(
            CommandBytes.Sinter,
            args,
            args.Length,
            cancellationToken).ConfigureAwait(false);

        // RESP3 might return a Set type
        if (response.TryGetSet(out var set))
        {
            var result = new string[set.Count];
            int i = 0;
            foreach (var v in set)
            {
                result[i++] = SafeGetString(v, "SINTER set element");
            }
            return result;
        }

        return SafeConvertArrayToStrings(response, "SINTER");
    }

    /// <summary>
    /// Get the union of multiple sets.
    /// </summary>
    internal async ValueTask<string[]> UnionAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        if (keys == null || keys.Length == 0)
        {
            throw new ArgumentException("Keys cannot be null or empty", nameof(keys));
        }

        var args = keys.Select(k => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(k)).ToArray();

        var response = await ExecuteAsync(
            CommandBytes.Sunion,
            args,
            args.Length,
            cancellationToken).ConfigureAwait(false);

        // RESP3 might return a Set type
        if (response.TryGetSet(out var set))
        {
            var result = new string[set.Count];
            int i = 0;
            foreach (var v in set)
            {
                result[i++] = SafeGetString(v, "SUNION set element");
            }
            return result;
        }

        return SafeConvertArrayToStrings(response, "SUNION");
    }

    /// <summary>
    /// Get the difference between the first set and successive sets.
    /// </summary>
    internal async ValueTask<string[]> DifferenceAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        if (keys == null || keys.Length == 0)
        {
            throw new ArgumentException("Keys cannot be null or empty", nameof(keys));
        }

        var args = keys.Select(k => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(k)).ToArray();

        var response = await ExecuteAsync(
            CommandBytes.Sdiff,
            args,
            args.Length,
            cancellationToken).ConfigureAwait(false);

        // RESP3 might return a Set type
        if (response.TryGetSet(out var set))
        {
            var result = new string[set.Count];
            int i = 0;
            foreach (var v in set)
            {
                result[i++] = SafeGetString(v, "SDIFF set element");
            }
            return result;
        }

        return SafeConvertArrayToStrings(response, "SDIFF");
    }
}
