using Valkey.Protocol;

namespace Valkey.Commands;

/// <summary>
/// Base class for command executors with shared utilities and common functionality.
/// </summary>
internal abstract class CommandExecutorBase
{
    protected readonly ValkeyConnection Connection;

    protected CommandExecutorBase(ValkeyConnection connection)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <summary>
    /// Executes a command and returns the response.
    /// </summary>
    protected ValueTask<RespValue> ExecuteAsync(
        ReadOnlyMemory<byte> command,
        ReadOnlyMemory<byte>[] args,
        int argCount,
        CancellationToken cancellationToken = default)
    {
        return Connection.ExecuteCommandAsync(command, args, argCount, cancellationToken);
    }

    /// <summary>
    /// Validates that a key is not null or empty.
    /// </summary>
    protected static void ValidateKey(string key, string paramName = "key")
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", paramName);
        }
    }

    /// <summary>
    /// Validates that an array has an even length (for paired data like field-value pairs).
    /// </summary>
    protected static void ValidateEvenArrayLength(RespValue[] array, string context)
    {
        if (array.Length % 2 != 0)
        {
            throw new InvalidOperationException($"{context}: Expected even-length array for field-value pairs, got {array.Length} elements");
        }
    }

    /// <summary>
    /// Safely converts a RESP array to a string array with validation.
    /// </summary>
    protected static string[] SafeConvertArrayToStrings(RespValue response, string context)
    {
        if (!response.TryGetArray(out var array))
        {
            throw new InvalidOperationException($"{context}: Expected array response, got {response.Type}");
        }

        var result = new string[array.Length];
        for (int i = 0; i < array.Length; i++)
        {
            if (!array[i].TryGetString(out var str))
            {
                throw new InvalidOperationException($"{context}: Expected string element at index {i}, got {array[i].Type}");
            }
            result[i] = str!;
        }
        return result;
    }

    /// <summary>
    /// Safely gets an integer from a RESP response with validation.
    /// </summary>
    protected static long SafeGetInteger(RespValue response, string context)
    {
        if (!response.TryGetInteger(out var value))
        {
            throw new InvalidOperationException($"{context}: Expected integer response, got {response.Type}");
        }
        return value;
    }

    /// <summary>
    /// Safely gets a string from a RESP response with validation.
    /// </summary>
    protected static string SafeGetString(RespValue response, string context)
    {
        if (!response.TryGetString(out var value))
        {
            throw new InvalidOperationException($"{context}: Expected string response, got {response.Type}");
        }
        return value!;
    }
}
