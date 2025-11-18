using System.Text;
using Valkey.Protocol;

namespace Valkey.Commands;

/// <summary>
/// Executor for utility commands (PING, ECHO).
/// </summary>
internal sealed class UtilityCommandExecutor : CommandExecutorBase
{
    internal UtilityCommandExecutor(ValkeyConnection connection) : base(connection)
    {
    }

    /// <summary>
    /// Ping the server.
    /// </summary>
    internal async ValueTask<string> PingAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExecuteAsync(
            "PING"u8.ToArray(),
            Array.Empty<ReadOnlyMemory<byte>>(),
            0,
            cancellationToken).ConfigureAwait(false);

        return response.AsString();
    }

    /// <summary>
    /// Echo the given string.
    /// </summary>
    internal async ValueTask<string> EchoAsync(string message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var args = ArgumentArrayPool.Rent(1);
        try
        {
            args[0] = Encoding.UTF8.GetBytes(message);

            var response = await ExecuteAsync(
                "ECHO"u8.ToArray(),
                args,
                1,
                cancellationToken).ConfigureAwait(false);

            return response.AsString();
        }
        finally
        {
            ArgumentArrayPool.Return(args);
        }
    }
}
