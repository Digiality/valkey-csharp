using System.Buffers;
using System.Text;
using Valkey.Commands;
using Valkey.Protocol;

namespace Valkey.Transactions;

/// <summary>
/// Implements a Valkey transaction (MULTI/EXEC).
/// Commands are queued locally and sent together when ExecuteAsync is called.
/// Uses deferred encoding to minimize allocations - commands are stored as strings
/// and only encoded when the transaction executes.
/// </summary>
public sealed class ValkeyTransaction : ITransaction
{
    private readonly ValkeyConnection _connection;

    // Store commands as strings to defer encoding until ExecuteAsync
    // This allows us to use pooled buffers only during the actual execution
    private readonly List<QueuedCommand> _queuedCommands = new();
    private bool _isExecuted;
    private bool _isDiscarded;

    internal ValkeyTransaction(ValkeyConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <summary>
    /// Represents a queued command with its arguments stored as strings.
    /// </summary>
    private readonly struct QueuedCommand
    {
        public readonly ReadOnlyMemory<byte> CommandBytes;
        public readonly string[] Args;

        public QueuedCommand(ReadOnlyMemory<byte> commandBytes, params string[] args)
        {
            CommandBytes = commandBytes;
            Args = args;
        }
    }

    private void EnsureNotExecuted()
    {
        if (_isExecuted)
        {
            throw new InvalidOperationException("Transaction has already been executed");
        }
        if (_isDiscarded)
        {
            throw new InvalidOperationException("Transaction has been discarded");
        }
    }

    // ========================================
    // Helper methods to reduce duplication
    // ========================================

    /// <summary>
    /// Queue a command with a single key argument.
    /// </summary>
    private ITransaction QueueKeyCommand(ReadOnlyMemory<byte> command, string key)
    {
        ValidateKey(key);
        EnsureNotExecuted();
        _queuedCommands.Add(new QueuedCommand(command, key));
        return this;
    }

    /// <summary>
    /// Queue a command with key and value arguments.
    /// </summary>
    private ITransaction QueueKeyValueCommand(ReadOnlyMemory<byte> command, string key, string value)
    {
        ValidateKey(key);
        ValidateValue(value);
        EnsureNotExecuted();
        _queuedCommands.Add(new QueuedCommand(command, key, value));
        return this;
    }

    /// <summary>
    /// Queue a command with key, field, and value arguments.
    /// </summary>
    private ITransaction QueueKeyFieldValueCommand(ReadOnlyMemory<byte> command, string key, string field, string value)
    {
        ValidateKey(key);
        ValidateField(field);
        ValidateValue(value);
        EnsureNotExecuted();
        _queuedCommands.Add(new QueuedCommand(command, key, field, value));
        return this;
    }

    /// <summary>
    /// Queue a command with key and field arguments.
    /// </summary>
    private ITransaction QueueKeyFieldCommand(ReadOnlyMemory<byte> command, string key, string field)
    {
        ValidateKey(key);
        ValidateField(field);
        EnsureNotExecuted();
        _queuedCommands.Add(new QueuedCommand(command, key, field));
        return this;
    }

    /// <summary>
    /// Queue a command with key and numeric value.
    /// </summary>
    private ITransaction QueueKeyNumericCommand(ReadOnlyMemory<byte> command, string key, long value)
    {
        ValidateKey(key);
        EnsureNotExecuted();
        _queuedCommands.Add(new QueuedCommand(command, key, value.ToString()));
        return this;
    }

    /// <summary>
    /// Queue a command with key, member, and score (for sorted sets).
    /// </summary>
    private ITransaction QueueSortedSetAddCommand(string key, string member, double score)
    {
        ValidateKey(key);
        ValidateMember(member);
        EnsureNotExecuted();
        // ZADD format: ZADD key score member
        _queuedCommands.Add(new QueuedCommand(CommandBytes.Zadd, key, score.ToString("G17"), member));
        return this;
    }

    // ========================================
    // Validation helpers
    // ========================================

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }
    }

    private static void ValidateValue(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
    }

    private static void ValidateField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }
    }

    private static void ValidateMember(string member)
    {
        if (member == null)
        {
            throw new ArgumentNullException(nameof(member));
        }
    }

    // ========================================
    // String Commands
    // ========================================

    /// <inheritdoc />
    public ITransaction StringSet(string key, string value, TimeSpan? expiry = null)
    {
        ValidateKey(key);
        ValidateValue(value);
        EnsureNotExecuted();

        if (expiry.HasValue)
        {
            var expirySeconds = ((int)expiry.Value.TotalSeconds).ToString();
            _queuedCommands.Add(new QueuedCommand(CommandBytes.Set, key, value, "EX", expirySeconds));
        }
        else
        {
            _queuedCommands.Add(new QueuedCommand(CommandBytes.Set, key, value));
        }

        return this;
    }

    /// <inheritdoc />
    public ITransaction StringGet(string key) => QueueKeyCommand(CommandBytes.Get, key);

    /// <inheritdoc />
    public ITransaction StringIncrement(string key) => QueueKeyCommand(CommandBytes.Incr, key);

    /// <inheritdoc />
    public ITransaction StringIncrement(string key, long value) => QueueKeyNumericCommand(CommandBytes.Incrby, key, value);

    /// <inheritdoc />
    public ITransaction StringDecrement(string key) => QueueKeyCommand(CommandBytes.Decr, key);

    /// <inheritdoc />
    public ITransaction StringDecrement(string key, long value) => QueueKeyNumericCommand(CommandBytes.Decrby, key, value);

    // ========================================
    // Key Commands
    // ========================================

    /// <inheritdoc />
    public ITransaction KeyDelete(string key) => QueueKeyCommand(CommandBytes.Del, key);

    /// <inheritdoc />
    public ITransaction KeyExists(string key) => QueueKeyCommand(CommandBytes.Exists, key);

    /// <inheritdoc />
    public ITransaction KeyExpire(string key, TimeSpan expiry)
    {
        ValidateKey(key);
        EnsureNotExecuted();
        var expirySeconds = ((int)expiry.TotalSeconds).ToString();
        _queuedCommands.Add(new QueuedCommand(CommandBytes.Expire, key, expirySeconds));
        return this;
    }

    // ========================================
    // Hash Commands
    // ========================================

    /// <inheritdoc />
    public ITransaction HashSet(string key, string field, string value) =>
        QueueKeyFieldValueCommand(CommandBytes.Hset, key, field, value);

    /// <inheritdoc />
    public ITransaction HashGet(string key, string field) =>
        QueueKeyFieldCommand(CommandBytes.Hget, key, field);

    /// <inheritdoc />
    public ITransaction HashDelete(string key, string field) =>
        QueueKeyFieldCommand(CommandBytes.Hdel, key, field);

    // ========================================
    // List Commands
    // ========================================

    /// <inheritdoc />
    public ITransaction ListLeftPush(string key, string value) =>
        QueueKeyValueCommand(CommandBytes.Lpush, key, value);

    /// <inheritdoc />
    public ITransaction ListRightPush(string key, string value) =>
        QueueKeyValueCommand(CommandBytes.Rpush, key, value);

    /// <inheritdoc />
    public ITransaction ListLeftPop(string key) => QueueKeyCommand(CommandBytes.Lpop, key);

    /// <inheritdoc />
    public ITransaction ListRightPop(string key) => QueueKeyCommand(CommandBytes.Rpop, key);

    // ========================================
    // Set Commands
    // ========================================

    /// <inheritdoc />
    public ITransaction SetAdd(string key, string member) =>
        QueueKeyValueCommand(CommandBytes.Sadd, key, member);

    /// <inheritdoc />
    public ITransaction SetRemove(string key, string member) =>
        QueueKeyValueCommand(CommandBytes.Srem, key, member);

    /// <inheritdoc />
    public ITransaction SetContains(string key, string member) =>
        QueueKeyValueCommand(CommandBytes.Sismember, key, member);

    // ========================================
    // Sorted Set Commands
    // ========================================

    /// <inheritdoc />
    public ITransaction SortedSetAdd(string key, string member, double score) =>
        QueueSortedSetAddCommand(key, member, score);

    /// <inheritdoc />
    public ITransaction SortedSetRemove(string key, string member) =>
        QueueKeyValueCommand(CommandBytes.Zrem, key, member);

    /// <inheritdoc />
    public ITransaction SortedSetScore(string key, string member) =>
        QueueKeyValueCommand(CommandBytes.Zscore, key, member);

    // ========================================
    // Transaction Execution
    // ========================================

    /// <inheritdoc />
    public async ValueTask<RespValue[]> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotExecuted();
        _isExecuted = true;

        if (_queuedCommands.Count == 0)
        {
            return Array.Empty<RespValue>();
        }

        // Send MULTI command
        await _connection.ExecuteCommandAsync(
            CommandBytes.Multi,
            Array.Empty<ReadOnlyMemory<byte>>(),
            0,
            cancellationToken).ConfigureAwait(false);

        // Send all queued commands using pooled buffers
        foreach (var queuedCommand in _queuedCommands)
        {
            await SendQueuedCommandAsync(queuedCommand, cancellationToken).ConfigureAwait(false);
        }

        // Send EXEC and get results
        var response = await _connection.ExecuteCommandAsync(
            CommandBytes.Exec,
            Array.Empty<ReadOnlyMemory<byte>>(),
            0,
            cancellationToken).ConfigureAwait(false);

        // EXEC returns an array of results, one for each command
        if (response.TryGetArray(out var results))
        {
            return results;
        }

        // If EXEC was aborted (e.g., WATCH key was modified), it returns null
        if (response.IsNull)
        {
            throw new InvalidOperationException("Transaction was aborted");
        }

        throw new InvalidOperationException($"Unexpected response from EXEC: {response.Type}");
    }

    /// <summary>
    /// Sends a single queued command using CommandBuilder and ArgumentArrayPool for zero-allocation encoding.
    /// </summary>
    private async ValueTask SendQueuedCommandAsync(QueuedCommand queuedCommand, CancellationToken cancellationToken)
    {
        var args = queuedCommand.Args;
        var argCount = args.Length;

        // Rent arrays for encoded arguments
        var encodedArgs = ArgumentArrayPool.Rent(argCount);
        var buffers = ArrayPool<byte[]>.Shared.Rent(argCount);
        var bufferCount = 0;

        try
        {
            // Encode all arguments using CommandBuilder
            for (int i = 0; i < argCount; i++)
            {
                var (buffer, length) = CommandBuilder.EncodeValue(args[i]);
                buffers[bufferCount++] = buffer;
                encodedArgs[i] = CommandBuilder.AsMemory(buffer, length);
            }

            // Execute the command
            await _connection.ExecuteCommandAsync(
                queuedCommand.CommandBytes.ToArray(),
                encodedArgs,
                argCount,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Return all pooled resources
            for (int i = 0; i < bufferCount; i++)
            {
                CommandBuilder.Return(buffers[i]);
            }
            ArrayPool<byte[]>.Shared.Return(buffers);
            ArgumentArrayPool.Return(encodedArgs);
        }
    }

    /// <inheritdoc />
    public async ValueTask DiscardAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotExecuted();
        _isDiscarded = true;

        // Send DISCARD command
        await _connection.ExecuteCommandAsync(
            CommandBytes.Discard,
            Array.Empty<ReadOnlyMemory<byte>>(),
            0,
            cancellationToken).ConfigureAwait(false);
    }
}
