namespace Valkey.PubSub;

/// <summary>
/// Represents the type of pub/sub message received.
/// </summary>
public enum PubSubMessageType
{
    /// <summary>
    /// A message received on a subscribed channel.
    /// </summary>
    Message,

    /// <summary>
    /// A message received on a pattern-subscribed channel.
    /// </summary>
    PatternMessage,

    /// <summary>
    /// Confirmation of a channel subscription.
    /// </summary>
    Subscribe,

    /// <summary>
    /// Confirmation of a channel unsubscription.
    /// </summary>
    Unsubscribe,

    /// <summary>
    /// Confirmation of a pattern subscription.
    /// </summary>
    PatternSubscribe,

    /// <summary>
    /// Confirmation of a pattern unsubscription.
    /// </summary>
    PatternUnsubscribe
}
