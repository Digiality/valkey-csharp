using Valkey.Protocol;

namespace Valkey.PubSub;

/// <summary>
/// Parses pub/sub messages from RESP responses.
/// </summary>
internal static class PubSubMessageParser
{
    private static readonly Dictionary<string, Func<RespValue[], PubSubMessage>> Parsers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["message"] = ParseMessage,
        ["pmessage"] = ParsePatternMessage,
        ["subscribe"] = ParseSubscribe,
        ["unsubscribe"] = ParseUnsubscribe,
        ["psubscribe"] = ParsePatternSubscribe,
        ["punsubscribe"] = ParsePatternUnsubscribe
    };

    /// <summary>
    /// Parses a pub/sub message from a RESP push or array value.
    /// </summary>
    public static PubSubMessage Parse(RespValue pushValue)
    {
        var array = pushValue.AsArray();

        if (array.Length == 0)
        {
            throw new InvalidOperationException("Empty push message array");
        }

        var messageType = array[0].AsString();

        if (!Parsers.TryGetValue(messageType, out var parser))
        {
            throw new InvalidOperationException($"Unknown pub/sub message type: {messageType}");
        }

        return parser(array);
    }

    private static PubSubMessage ParseMessage(RespValue[] array)
    {
        if (array.Length < 3)
        {
            throw new InvalidOperationException("Invalid message format");
        }

        return new PubSubMessage(
            PubSubMessageType.Message,
            channel: array[1].AsString(),
            message: array[2].AsString());
    }

    private static PubSubMessage ParsePatternMessage(RespValue[] array)
    {
        if (array.Length < 4)
        {
            throw new InvalidOperationException("Invalid pmessage format");
        }

        return new PubSubMessage(
            PubSubMessageType.PatternMessage,
            channel: array[2].AsString(),
            pattern: array[1].AsString(),
            message: array[3].AsString());
    }

    private static PubSubMessage ParseSubscribe(RespValue[] array)
    {
        if (array.Length < 3)
        {
            throw new InvalidOperationException("Invalid subscribe format");
        }

        return new PubSubMessage(
            PubSubMessageType.Subscribe,
            channel: array[1].AsString(),
            subscriptionCount: array[2].AsInteger());
    }

    private static PubSubMessage ParseUnsubscribe(RespValue[] array)
    {
        if (array.Length < 3)
        {
            throw new InvalidOperationException("Invalid unsubscribe format");
        }

        return new PubSubMessage(
            PubSubMessageType.Unsubscribe,
            channel: array[1].AsString(),
            subscriptionCount: array[2].AsInteger());
    }

    private static PubSubMessage ParsePatternSubscribe(RespValue[] array)
    {
        if (array.Length < 3)
        {
            throw new InvalidOperationException("Invalid psubscribe format");
        }

        return new PubSubMessage(
            PubSubMessageType.PatternSubscribe,
            channel: array[1].AsString(),
            subscriptionCount: array[2].AsInteger());
    }

    private static PubSubMessage ParsePatternUnsubscribe(RespValue[] array)
    {
        if (array.Length < 3)
        {
            throw new InvalidOperationException("Invalid punsubscribe format");
        }

        return new PubSubMessage(
            PubSubMessageType.PatternUnsubscribe,
            channel: array[1].AsString(),
            subscriptionCount: array[2].AsInteger());
    }
}
