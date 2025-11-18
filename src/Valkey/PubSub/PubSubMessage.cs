namespace Valkey.PubSub;

/// <summary>
/// Represents a message received from a pub/sub channel.
/// </summary>
public readonly struct PubSubMessage
{
    /// <summary>
    /// Gets the type of the pub/sub message.
    /// </summary>
    public PubSubMessageType MessageType { get; }

    /// <summary>
    /// Gets the channel name that the message was received on.
    /// For pattern subscriptions, this is the actual channel name that matched the pattern.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// Gets the pattern that matched this message, if this is a pattern subscription.
    /// Null for regular channel subscriptions.
    /// </summary>
    public string? Pattern { get; }

    /// <summary>
    /// Gets the message payload. Null for subscription confirmation messages.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the number of channels currently subscribed to.
    /// Only populated for subscribe/unsubscribe confirmation messages.
    /// </summary>
    public long SubscriptionCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PubSubMessage"/> struct.
    /// </summary>
    public PubSubMessage(
        PubSubMessageType messageType,
        string channel,
        string? pattern = null,
        string? message = null,
        long subscriptionCount = 0)
    {
        MessageType = messageType;
        Channel = channel;
        Pattern = pattern;
        Message = message;
        SubscriptionCount = subscriptionCount;
    }

    /// <summary>
    /// Returns a string representation of the pub/sub message.
    /// </summary>
    public override string ToString()
    {
        return MessageType switch
        {
            PubSubMessageType.Message => $"Message on {Channel}: {Message}",
            PubSubMessageType.PatternMessage => $"Pattern message on {Channel} (pattern: {Pattern}): {Message}",
            PubSubMessageType.Subscribe => $"Subscribed to {Channel} ({SubscriptionCount} total)",
            PubSubMessageType.Unsubscribe => $"Unsubscribed from {Channel} ({SubscriptionCount} total)",
            PubSubMessageType.PatternSubscribe => $"Pattern subscribed to {Channel} ({SubscriptionCount} total)",
            PubSubMessageType.PatternUnsubscribe => $"Pattern unsubscribed from {Channel} ({SubscriptionCount} total)",
            _ => $"Unknown message type: {MessageType}"
        };
    }
}
