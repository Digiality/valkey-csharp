using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Valkey.Commands;
using Valkey.Configuration;
using Valkey.Protocol;
using Valkey.PubSub;

namespace Valkey;

/// <summary>
/// Represents a Valkey subscriber that can subscribe to pub/sub channels and patterns.
/// Uses a dedicated connection in pub/sub mode.
/// </summary>
public sealed class ValkeySubscriber : IValkeySubscriber
{
    private readonly ValkeyConnection _connection;
    private readonly Channel<PubSubMessage> _messageChannel;
    private readonly HashSet<string> _subscribedChannels = new();
    private readonly HashSet<string> _subscribedPatterns = new();
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private Task? _messageProcessingTask;
    private CancellationTokenSource? _processingCts;

    private ValkeySubscriber(ValkeyConnection connection)
    {
        _connection = connection;
        _messageChannel = Channel.CreateUnbounded<PubSubMessage>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });
    }

    /// <summary>
    /// Creates a new subscriber with a dedicated connection to the specified endpoint.
    /// </summary>
    public static async ValueTask<ValkeySubscriber> CreateAsync(
        EndPoint endpoint,
        ValkeyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var connectionOptions = options ?? new ValkeyOptions();
        var connection = await ValkeyConnection.ConnectAsync(endpoint, connectionOptions, cancellationToken).ConfigureAwait(false);
        var subscriber = new ValkeySubscriber(connection);
        subscriber.StartMessageProcessing();
        return subscriber;
    }

    /// <summary>
    /// Creates a new subscriber with a dedicated connection to localhost:6379.
    /// </summary>
    public static ValueTask<ValkeySubscriber> CreateAsync(CancellationToken cancellationToken = default)
    {
        return CreateAsync(new IPEndPoint(IPAddress.Loopback, 6379), new ValkeyOptions(), cancellationToken);
    }

    private void StartMessageProcessing()
    {
        _processingCts = new CancellationTokenSource();
        _messageProcessingTask = Task.Run(async () =>
        {
            try
            {
                await ProcessPushMessagesAsync(_processingCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when disposing
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Console.Error.WriteLine($"Push message processing error: {ex}");
            }
        });
    }

    private async Task ProcessPushMessagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Read push messages from the connection
                var pushMessage = await _connection.ReadPushMessageAsync(cancellationToken).ConfigureAwait(false);

                if (pushMessage == null)
                {
                    // Connection closed
                    break;
                }

                var parsedMessage = ParsePubSubMessage(pushMessage.Value);
                await _messageChannel.Writer.WriteAsync(parsedMessage, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing push message: {ex}");
            }
        }

        _messageChannel.Writer.Complete();
    }

    private static PubSubMessage ParsePubSubMessage(RespValue pushValue)
        => PubSubMessageParser.Parse(pushValue);

    /// <inheritdoc />
    public async IAsyncEnumerable<PubSubMessage> SubscribeAsync(
        string channel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await SendSubscribeCommandAsync(new[] { channel }, cancellationToken).ConfigureAwait(false);

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PubSubMessage> SubscribeAsync(
        string[] channels,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (channels == null || channels.Length == 0)
        {
            throw new ArgumentException("At least one channel must be specified", nameof(channels));
        }

        await SendSubscribeCommandAsync(channels, cancellationToken).ConfigureAwait(false);

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    private async ValueTask SendSubscribeCommandAsync(string[] channels, CancellationToken cancellationToken)
    {
        await _subscriptionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Build SUBSCRIBE command
            var args = new List<ReadOnlyMemory<byte>>();
            foreach (var channel in channels)
            {
                if (!_subscribedChannels.Contains(channel))
                {
                    args.Add(Encoding.UTF8.GetBytes(channel));
                    _subscribedChannels.Add(channel);
                }
            }

            if (args.Count > 0)
            {
                var argsArray = args.ToArray();
                await _connection.SendCommandAsync(CommandBytes.Subscribe, argsArray, argsArray.Length, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PubSubMessage> PatternSubscribeAsync(
        string pattern,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await SendPatternSubscribeCommandAsync(new[] { pattern }, cancellationToken).ConfigureAwait(false);

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PubSubMessage> PatternSubscribeAsync(
        string[] patterns,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (patterns == null || patterns.Length == 0)
        {
            throw new ArgumentException("At least one pattern must be specified", nameof(patterns));
        }

        await SendPatternSubscribeCommandAsync(patterns, cancellationToken).ConfigureAwait(false);

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    private async ValueTask SendPatternSubscribeCommandAsync(string[] patterns, CancellationToken cancellationToken)
    {
        await _subscriptionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Build PSUBSCRIBE command
            var args = new List<ReadOnlyMemory<byte>>();
            foreach (var pattern in patterns)
            {
                if (!_subscribedPatterns.Contains(pattern))
                {
                    args.Add(Encoding.UTF8.GetBytes(pattern));
                    _subscribedPatterns.Add(pattern);
                }
            }

            if (args.Count > 0)
            {
                var argsArray = args.ToArray();
                await _connection.SendCommandAsync(CommandBytes.Psubscribe, argsArray, argsArray.Length, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        await UnsubscribeAsync(new[] { channel }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask UnsubscribeAsync(string[] channels, CancellationToken cancellationToken = default)
    {
        if (channels == null || channels.Length == 0)
        {
            throw new ArgumentException("At least one channel must be specified", nameof(channels));
        }

        await _subscriptionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var args = new List<ReadOnlyMemory<byte>>();
            foreach (var channel in channels)
            {
                if (_subscribedChannels.Remove(channel))
                {
                    args.Add(Encoding.UTF8.GetBytes(channel));
                }
            }

            if (args.Count > 0)
            {
                var argsArray = args.ToArray();
                await _connection.SendCommandAsync(CommandBytes.Unsubscribe, argsArray, argsArray.Length, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask UnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        await _subscriptionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_subscribedChannels.Count > 0)
            {
                await _connection.SendCommandAsync(CommandBytes.Unsubscribe, Array.Empty<ReadOnlyMemory<byte>>(), 0, cancellationToken).ConfigureAwait(false);
                _subscribedChannels.Clear();
            }
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask PatternUnsubscribeAsync(string pattern, CancellationToken cancellationToken = default)
    {
        await PatternUnsubscribeAsync(new[] { pattern }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PatternUnsubscribeAsync(string[] patterns, CancellationToken cancellationToken = default)
    {
        if (patterns == null || patterns.Length == 0)
        {
            throw new ArgumentException("At least one pattern must be specified", nameof(patterns));
        }

        await _subscriptionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var args = new List<ReadOnlyMemory<byte>>();
            foreach (var pattern in patterns)
            {
                if (_subscribedPatterns.Remove(pattern))
                {
                    args.Add(Encoding.UTF8.GetBytes(pattern));
                }
            }

            if (args.Count > 0)
            {
                var argsArray = args.ToArray();
                await _connection.SendCommandAsync(CommandBytes.Punsubscribe, argsArray, argsArray.Length, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask PatternUnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        await _subscriptionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_subscribedPatterns.Count > 0)
            {
                await _connection.SendCommandAsync(CommandBytes.Punsubscribe, Array.Empty<ReadOnlyMemory<byte>>(), 0, cancellationToken).ConfigureAwait(false);
                _subscribedPatterns.Clear();
            }
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Cancel message processing
        _processingCts?.Cancel();

        // Wait for message processing to complete
        if (_messageProcessingTask != null)
        {
            try
            {
                await _messageProcessingTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore cancellation exceptions
            }
        }

        // Dispose resources
        _processingCts?.Dispose();
        _subscriptionLock.Dispose();

        // Close connection
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
