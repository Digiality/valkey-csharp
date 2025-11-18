using System.Text;
using Valkey.Protocol;

namespace Valkey.Commands;

/// <summary>
/// Executor for scripting commands (EVAL, EVALSHA, SCRIPT LOAD, SCRIPT EXISTS, SCRIPT FLUSH).
/// </summary>
internal sealed class ScriptingCommandExecutor : CommandExecutorBase
{
    internal ScriptingCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Evaluates a Lua script on the server.
    /// </summary>
    /// <param name="script">The Lua script to execute.</param>
    /// <param name="keys">Array of keys that the script will access.</param>
    /// <param name="args">Array of additional arguments to pass to the script.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the script execution.</returns>
    internal async ValueTask<RespValue> ScriptEvaluateAsync(string script, string[]? keys = null, string[]? args = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(script))
        {
            throw new ArgumentException("Script cannot be null or empty", nameof(script));
        }

        var (scriptBuffer, scriptLength) = CommandBuilder.EncodeValue(script);
        var (numKeysBuffer, numKeysLength) = CommandBuilder.EncodeLong(keys?.Length ?? 0);

        var keyCount = keys?.Length ?? 0;
        var argCount = 2 + keyCount + (args?.Length ?? 0);
        var cmdArgs = ArgumentArrayPool.Rent(argCount);

        var keyBuffers = new byte[keyCount][];
        var keyLengths = new int[keyCount];
        var argBuffers = args != null ? new byte[args.Length][] : Array.Empty<byte[]>();
        var argLengths = args != null ? new int[args.Length] : Array.Empty<int>();

        try
        {
            int argIndex = 0;
            cmdArgs[argIndex++] = CommandBuilder.AsMemory(scriptBuffer, scriptLength);
            cmdArgs[argIndex++] = CommandBuilder.AsMemory(numKeysBuffer, numKeysLength);

            // Add keys
            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    (keyBuffers[i], keyLengths[i]) = CommandBuilder.EncodeValue(keys[i]);
                    cmdArgs[argIndex++] = CommandBuilder.AsMemory(keyBuffers[i], keyLengths[i]);
                }
            }

            // Add arguments
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    (argBuffers[i], argLengths[i]) = CommandBuilder.EncodeValue(args[i]);
                    cmdArgs[argIndex++] = CommandBuilder.AsMemory(argBuffers[i], argLengths[i]);
                }
            }

            var response = await ExecuteAsync(
                CommandBytes.Eval,
                cmdArgs,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response;
        }
        finally
        {
            CommandBuilder.Return(scriptBuffer, numKeysBuffer);
            foreach (var buffer in keyBuffers)
            {
                if (buffer != null)
                {
                    CommandBuilder.Return(buffer);
                }
            }
            foreach (var buffer in argBuffers)
            {
                if (buffer != null)
                {
                    CommandBuilder.Return(buffer);
                }
            }
            ArgumentArrayPool.Return(cmdArgs);
        }
    }

    /// <summary>
    /// Loads a Lua script into the server's script cache.
    /// </summary>
    /// <param name="script">The Lua script to load.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The SHA1 hash of the script.</returns>
    internal async ValueTask<string> ScriptLoadAsync(string script, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(script))
        {
            throw new ArgumentException("Script cannot be null or empty", nameof(script));
        }

        var args = ArgumentArrayPool.Rent(2);
        try
        {
            args[0] = CommandBytes.Load;
            args[1] = Encoding.UTF8.GetBytes(script);

            var response = await ExecuteAsync(
                CommandBytes.Script,
                args,
                2,
                cancellationToken).ConfigureAwait(false);

            return response.AsString();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }

    /// <summary>
    /// Evaluates a previously loaded Lua script using its SHA1 hash.
    /// </summary>
    /// <param name="sha1">The SHA1 hash of the script to execute.</param>
    /// <param name="keys">Array of keys that the script will access.</param>
    /// <param name="args">Array of additional arguments to pass to the script.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the script execution.</returns>
    internal async ValueTask<RespValue> ScriptEvaluateShaAsync(string sha1, string[]? keys = null, string[]? args = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sha1))
        {
            throw new ArgumentException("SHA1 hash cannot be null or empty", nameof(sha1));
        }

        var (sha1Buffer, sha1Length) = CommandBuilder.EncodeValue(sha1);
        var (numKeysBuffer, numKeysLength) = CommandBuilder.EncodeLong(keys?.Length ?? 0);

        var keyCount = keys?.Length ?? 0;
        var argCount = 2 + keyCount + (args?.Length ?? 0);
        var cmdArgs = ArgumentArrayPool.Rent(argCount);

        var keyBuffers = new byte[keyCount][];
        var keyLengths = new int[keyCount];
        var argBuffers = args != null ? new byte[args.Length][] : Array.Empty<byte[]>();
        var argLengths = args != null ? new int[args.Length] : Array.Empty<int>();

        try
        {
            int argIndex = 0;
            cmdArgs[argIndex++] = CommandBuilder.AsMemory(sha1Buffer, sha1Length);
            cmdArgs[argIndex++] = CommandBuilder.AsMemory(numKeysBuffer, numKeysLength);

            // Add keys
            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    (keyBuffers[i], keyLengths[i]) = CommandBuilder.EncodeValue(keys[i]);
                    cmdArgs[argIndex++] = CommandBuilder.AsMemory(keyBuffers[i], keyLengths[i]);
                }
            }

            // Add arguments
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    (argBuffers[i], argLengths[i]) = CommandBuilder.EncodeValue(args[i]);
                    cmdArgs[argIndex++] = CommandBuilder.AsMemory(argBuffers[i], argLengths[i]);
                }
            }

            var response = await ExecuteAsync(
                CommandBytes.Evalsha,
                cmdArgs,
                argCount,
                cancellationToken).ConfigureAwait(false);

            return response;
        }
        finally
        {
            CommandBuilder.Return(sha1Buffer, numKeysBuffer);
            foreach (var buffer in keyBuffers)
            {
                if (buffer != null)
                {
                    CommandBuilder.Return(buffer);
                }
            }
            foreach (var buffer in argBuffers)
            {
                if (buffer != null)
                {
                    CommandBuilder.Return(buffer);
                }
            }
            ArgumentArrayPool.Return(cmdArgs);
        }
    }

    /// <summary>
    /// Checks if scripts exist in the script cache.
    /// </summary>
    /// <param name="sha1Hashes">Array of SHA1 hashes to check.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of booleans indicating if each script exists.</returns>
    internal async ValueTask<bool[]> ScriptExistsAsync(string[] sha1Hashes, CancellationToken cancellationToken = default)
    {
        if (sha1Hashes == null || sha1Hashes.Length == 0)
        {
            throw new ArgumentException("SHA1 hashes cannot be null or empty", nameof(sha1Hashes));
        }

        var argCount = 1 + sha1Hashes.Length;
        var args = ArgumentArrayPool.Rent(argCount);

        var hashBuffers = new byte[sha1Hashes.Length][];
        var hashLengths = new int[sha1Hashes.Length];

        try
        {
            args[0] = CommandBytes.Exists_Script;

            for (int i = 0; i < sha1Hashes.Length; i++)
            {
                (hashBuffers[i], hashLengths[i]) = CommandBuilder.EncodeValue(sha1Hashes[i]);
                args[i + 1] = CommandBuilder.AsMemory(hashBuffers[i], hashLengths[i]);
            }

            var response = await ExecuteAsync(
                CommandBytes.Script,
                args,
                argCount,
                cancellationToken).ConfigureAwait(false);

            var array = response.AsArray();
            return array.Select(v => v.AsInteger() == 1).ToArray();
        }
        finally
        {
            foreach (var buffer in hashBuffers)
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
    /// Removes all scripts from the script cache.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async ValueTask ScriptFlushAsync(CancellationToken cancellationToken = default)
    {
        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = CommandBytes.Flush;

            await ExecuteAsync(
                CommandBytes.Script,
                args,
                1,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }
}
