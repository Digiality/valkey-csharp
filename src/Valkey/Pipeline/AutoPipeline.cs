using System.Threading.Channels;
using Valkey.Protocol;

namespace Valkey.Pipeline;

/// <summary>
/// Automatically batches commands within a time window to reduce round trips.
/// Inspired by valkey-go's auto-pipelining feature.
/// </summary>
internal sealed class AutoPipeline : IAsyncDisposable
{
    private readonly Channel<PipelineCommand> _commandChannel;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts;
    private readonly TimeSpan _batchWindow;
    private readonly int _maxBatchSize;
    private readonly Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>[], CancellationToken, ValueTask<RespValue>> _executeFunc;

    /// <summary>
    /// Represents a command in the pipeline.
    /// </summary>
    private sealed class PipelineCommand
    {
        public required ReadOnlyMemory<byte> Command { get; init; }
        public required ReadOnlyMemory<byte>[] Args { get; init; }
        public required TaskCompletionSource<RespValue> CompletionSource { get; init; }
        public required CancellationToken CancellationToken { get; init; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoPipeline"/> class.
    /// </summary>
    /// <param name="executeFunc">Function to execute commands.</param>
    /// <param name="batchWindow">Time window to collect commands (default: 100μs).</param>
    /// <param name="maxBatchSize">Maximum number of commands per batch (default: 100).</param>
    public AutoPipeline(
        Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>[], CancellationToken, ValueTask<RespValue>> executeFunc,
        TimeSpan? batchWindow = null,
        int maxBatchSize = 100)
    {
        _executeFunc = executeFunc ?? throw new ArgumentNullException(nameof(executeFunc));
        _batchWindow = batchWindow ?? TimeSpan.FromMicroseconds(100);
        _maxBatchSize = maxBatchSize;
        _commandChannel = Channel.CreateUnbounded<PipelineCommand>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _cts = new CancellationTokenSource();
        _processingTask = ProcessCommandsAsync(_cts.Token);
    }

    /// <summary>
    /// Queues a command for execution.
    /// </summary>
    public async ValueTask<RespValue> ExecuteAsync(
        ReadOnlyMemory<byte> command,
        ReadOnlyMemory<byte>[] args,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<RespValue>(TaskCreationOptions.RunContinuationsAsynchronously);

        var pipelineCommand = new PipelineCommand
        {
            Command = command,
            Args = args,
            CompletionSource = tcs,
            CancellationToken = cancellationToken
        };

        await _commandChannel.Writer.WriteAsync(pipelineCommand, cancellationToken).ConfigureAwait(false);

        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Processes commands in batches.
    /// </summary>
    private async Task ProcessCommandsAsync(CancellationToken cancellationToken)
    {
        var batch = new List<PipelineCommand>(_maxBatchSize);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                batch.Clear();

                // Read first command (blocking)
                if (!await _commandChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                if (!_commandChannel.Reader.TryRead(out var firstCommand))
                {
                    continue;
                }

                batch.Add(firstCommand);

                // Collect more commands within the batch window
                var deadline = DateTime.UtcNow + _batchWindow;
                while (batch.Count < _maxBatchSize && DateTime.UtcNow < deadline)
                {
                    if (_commandChannel.Reader.TryRead(out var command))
                    {
                        batch.Add(command);
                    }
                    else
                    {
                        // Small delay to allow commands to arrive
                        await Task.Delay(TimeSpan.FromMicroseconds(10), cancellationToken).ConfigureAwait(false);
                        break;
                    }
                }

                // Execute batch
                await ExecuteBatchAsync(batch).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down
        }
        catch (Exception ex)
        {
            // Log error (in production, use proper logging)
            Console.WriteLine($"Auto-pipeline processing error: {ex}");
        }
        finally
        {
            // Complete any remaining commands with cancellation
            while (_commandChannel.Reader.TryRead(out var command))
            {
                command.CompletionSource.TrySetCanceled();
            }
        }
    }

    /// <summary>
    /// Executes a batch of commands.
    /// </summary>
    private async Task ExecuteBatchAsync(List<PipelineCommand> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        // Execute all commands in the batch
        var tasks = new Task<RespValue>[batch.Count];
        for (int i = 0; i < batch.Count; i++)
        {
            var cmd = batch[i];
            tasks[i] = ExecuteSingleCommandAsync(cmd);
        }

        // Wait for all to complete
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a single command and completes its task.
    /// </summary>
    private async Task<RespValue> ExecuteSingleCommandAsync(PipelineCommand command)
    {
        try
        {
            var result = await _executeFunc(command.Command, command.Args, command.CancellationToken).ConfigureAwait(false);
            command.CompletionSource.TrySetResult(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            command.CompletionSource.TrySetCanceled(command.CancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            command.CompletionSource.TrySetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Disposes the pipeline.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _commandChannel.Writer.Complete();
        _cts.Cancel();

        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _cts.Dispose();
    }
}
