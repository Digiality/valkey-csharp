using Valkey.PubSub;

namespace Valkey;

/// <summary>
/// Represents a Valkey subscriber that can subscribe to pub/sub channels and patterns.
/// A subscriber uses a dedicated connection that enters pub/sub mode and cannot execute regular commands.
/// </summary>
public interface IValkeySubscriber : IAsyncDisposable
{
    /// <summary>
    /// Subscribes to a single channel and returns an async stream of messages.
    /// </summary>
    /// <param name="channel">The channel name to subscribe to.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An async enumerable of pub/sub messages from the subscribed channel.</returns>
    public IAsyncEnumerable<PubSubMessage> SubscribeAsync(string channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to multiple channels and returns an async stream of messages from all subscribed channels.
    /// </summary>
    /// <param name="channels">The channel names to subscribe to.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An async enumerable of pub/sub messages from all subscribed channels.</returns>
    public IAsyncEnumerable<PubSubMessage> SubscribeAsync(string[] channels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to channels matching a pattern and returns an async stream of messages.
    /// Patterns use glob-style matching (*, ?, [...]).
    /// </summary>
    /// <param name="pattern">The pattern to match channel names against.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An async enumerable of pub/sub messages from channels matching the pattern.</returns>
    public IAsyncEnumerable<PubSubMessage> PatternSubscribeAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to channels matching multiple patterns and returns an async stream of messages.
    /// Patterns use glob-style matching (*, ?, [...]).
    /// </summary>
    /// <param name="patterns">The patterns to match channel names against.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An async enumerable of pub/sub messages from channels matching the patterns.</returns>
    public IAsyncEnumerable<PubSubMessage> PatternSubscribeAsync(string[] patterns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a single channel.
    /// </summary>
    /// <param name="channel">The channel name to unsubscribe from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask UnsubscribeAsync(string channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from multiple channels.
    /// </summary>
    /// <param name="channels">The channel names to unsubscribe from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask UnsubscribeAsync(string[] channels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from all currently subscribed channels.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask UnsubscribeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a single pattern subscription.
    /// </summary>
    /// <param name="pattern">The pattern to unsubscribe from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask PatternUnsubscribeAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from multiple pattern subscriptions.
    /// </summary>
    /// <param name="patterns">The patterns to unsubscribe from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask PatternUnsubscribeAsync(string[] patterns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from all currently subscribed patterns.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask PatternUnsubscribeAllAsync(CancellationToken cancellationToken = default);
}
