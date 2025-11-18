namespace Valkey.Abstractions;

/// <summary>
/// Represents a connection to a Valkey server.
/// </summary>
public interface IValkeyConnection : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the connection is currently connected.
    /// </summary>
    public bool IsConnected { get; }

    /// <summary>
    /// Gets the endpoint this connection is connected to.
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// Connects to the Valkey server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the Valkey server asynchronously.
    /// </summary>
    public ValueTask DisconnectAsync();
}
