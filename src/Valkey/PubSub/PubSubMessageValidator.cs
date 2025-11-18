using Valkey.Protocol;

namespace Valkey.PubSub;

/// <summary>
/// Validates and identifies pub/sub messages.
/// </summary>
internal static class PubSubMessageValidator
{
    private static readonly HashSet<string> PubSubMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "subscribe",
        "unsubscribe",
        "psubscribe",
        "punsubscribe",
        "message",
        "pmessage"
    };

    /// <summary>
    /// Checks if the response is a pub/sub message (works for both RESP2 and RESP3).
    /// </summary>
    public static bool IsPubSubMessage(RespValue response)
    {
        // RESP3: Push type
        if (response.Type == RespType.Push)
        {
            return true;
        }

        // RESP2: Array with specific message types
        if (response.Type == RespType.Array)
        {
            if (!response.TryGetArray(out var array) || array.Length == 0)
            {
                return false;
            }

            if (!array[0].TryGetString(out var messageType))
            {
                return false;
            }

            return PubSubMessageTypes.Contains(messageType);
        }

        return false;
    }
}
