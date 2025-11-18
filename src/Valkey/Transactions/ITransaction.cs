using Valkey.Protocol;

namespace Valkey.Transactions;

/// <summary>
/// Represents a Valkey transaction (MULTI/EXEC).
/// Commands are queued and executed atomically when ExecuteAsync is called.
/// </summary>
public interface ITransaction
{
    /// <summary>
    /// Queue a SET command in the transaction.
    /// </summary>
    public ITransaction StringSet(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// Queue a GET command in the transaction.
    /// </summary>
    public ITransaction StringGet(string key);

    /// <summary>
    /// Queue an INCR command in the transaction.
    /// </summary>
    public ITransaction StringIncrement(string key);

    /// <summary>
    /// Queue an INCRBY command in the transaction.
    /// </summary>
    public ITransaction StringIncrement(string key, long value);

    /// <summary>
    /// Queue a DECR command in the transaction.
    /// </summary>
    public ITransaction StringDecrement(string key);

    /// <summary>
    /// Queue a DECRBY command in the transaction.
    /// </summary>
    public ITransaction StringDecrement(string key, long value);

    /// <summary>
    /// Queue a DEL command in the transaction.
    /// </summary>
    public ITransaction KeyDelete(string key);

    /// <summary>
    /// Queue an EXISTS command in the transaction.
    /// </summary>
    public ITransaction KeyExists(string key);

    /// <summary>
    /// Queue an EXPIRE command in the transaction.
    /// </summary>
    public ITransaction KeyExpire(string key, TimeSpan expiry);

    /// <summary>
    /// Queue an HSET command in the transaction.
    /// </summary>
    public ITransaction HashSet(string key, string field, string value);

    /// <summary>
    /// Queue an HGET command in the transaction.
    /// </summary>
    public ITransaction HashGet(string key, string field);

    /// <summary>
    /// Queue an HDEL command in the transaction.
    /// </summary>
    public ITransaction HashDelete(string key, string field);

    /// <summary>
    /// Queue an LPUSH command in the transaction.
    /// </summary>
    public ITransaction ListLeftPush(string key, string value);

    /// <summary>
    /// Queue an RPUSH command in the transaction.
    /// </summary>
    public ITransaction ListRightPush(string key, string value);

    /// <summary>
    /// Queue an LPOP command in the transaction.
    /// </summary>
    public ITransaction ListLeftPop(string key);

    /// <summary>
    /// Queue an RPOP command in the transaction.
    /// </summary>
    public ITransaction ListRightPop(string key);

    /// <summary>
    /// Queue an SADD command in the transaction.
    /// </summary>
    public ITransaction SetAdd(string key, string member);

    /// <summary>
    /// Queue an SREM command in the transaction.
    /// </summary>
    public ITransaction SetRemove(string key, string member);

    /// <summary>
    /// Queue an SISMEMBER command in the transaction.
    /// </summary>
    public ITransaction SetContains(string key, string member);

    /// <summary>
    /// Queue a ZADD command in the transaction.
    /// </summary>
    public ITransaction SortedSetAdd(string key, string member, double score);

    /// <summary>
    /// Queue a ZREM command in the transaction.
    /// </summary>
    public ITransaction SortedSetRemove(string key, string member);

    /// <summary>
    /// Queue a ZSCORE command in the transaction.
    /// </summary>
    public ITransaction SortedSetScore(string key, string member);

    /// <summary>
    /// Executes the transaction by sending EXEC command.
    /// Returns an array of results corresponding to each queued command.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Array of results from each command in the transaction.</returns>
    public ValueTask<RespValue[]> ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards the transaction by sending DISCARD command.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ValueTask DiscardAsync(CancellationToken cancellationToken = default);
}
