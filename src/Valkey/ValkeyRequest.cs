using System.Threading.Channels;
using Valkey.Protocol;

namespace Valkey;

/// <summary>
/// Represents a pending request to the Valkey server.
/// </summary>
internal sealed class ValkeyRequest
{
    private readonly TaskCompletionSource<RespValue> _tcs;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValkeyRequest"/> class.
    /// </summary>
    public ValkeyRequest(CancellationToken cancellationToken = default)
    {
        _tcs = new TaskCompletionSource<RespValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken = cancellationToken;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => _tcs.TrySetCanceled(cancellationToken));
        }
    }

    /// <summary>
    /// Gets the cancellation token for this request.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the task that will complete when the response is received.
    /// </summary>
    public Task<RespValue> Task => _tcs.Task;

    /// <summary>
    /// Completes the request with a successful response.
    /// </summary>
    public void SetResult(RespValue value) => _tcs.TrySetResult(value);

    /// <summary>
    /// Completes the request with an exception.
    /// </summary>
    public void SetException(Exception exception) => _tcs.TrySetException(exception);

    /// <summary>
    /// Completes the request as canceled.
    /// </summary>
    public void SetCanceled() => _tcs.TrySetCanceled(CancellationToken);
}

/// <summary>
/// Manages the queue of pending requests and correlates responses.
/// </summary>
internal sealed class RequestQueue : IAsyncDisposable
{
    private readonly Channel<ValkeyRequest> _pendingRequests;
    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestQueue"/> class.
    /// </summary>
    public RequestQueue()
    {
        _pendingRequests = Channel.CreateUnbounded<ValkeyRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Enqueues a request and returns a task that completes when the response is received.
    /// </summary>
    public async ValueTask<RespValue> EnqueueAsync(CancellationToken cancellationToken = default)
    {
        var request = new ValkeyRequest(cancellationToken);

        if (!await _pendingRequests.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Request queue is closed");
        }

        await _pendingRequests.Writer.WriteAsync(request, cancellationToken).ConfigureAwait(false);

        return await request.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Processes a response by completing the next pending request.
    /// </summary>
    public async ValueTask<bool> ProcessResponseAsync(RespValue response, CancellationToken cancellationToken = default)
    {
        if (!await _pendingRequests.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (_pendingRequests.Reader.TryRead(out var request))
        {
            // Check if response is an error
            if (response.Type == RespType.SimpleError || response.Type == RespType.BulkError)
            {
                request.SetException(RespException.FromError(response));
            }
            else
            {
                request.SetResult(response);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Fails all pending requests with an exception.
    /// </summary>
    public void FailAll(Exception exception)
    {
        _pendingRequests.Writer.Complete();

        while (_pendingRequests.Reader.TryRead(out var request))
        {
            request.SetException(exception);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _disposeCts.CancelAsync().ConfigureAwait(false);
        _pendingRequests.Writer.Complete();
        _disposeCts.Dispose();
    }
}
