using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Valkey.Abstractions;
using Valkey.Commands;
using Valkey.Configuration;
using Valkey.Protocol;
using Valkey.PubSub;

namespace Valkey;

/// <summary>
/// Represents a single connection to a Valkey server using high-performance async I/O.
/// </summary>
public sealed class ValkeyConnection : IValkeyConnection
{
    private readonly ValkeyOptions _options;
    private readonly EndPoint _endpoint;
    private Socket? _socket;
    private Stream? _stream;
    private Pipe? _receivePipe;
    private Pipe? _sendPipe;
    private Resp3Parser? _parser;
    private Resp3Writer? _writer;
    private Task? _receiveTask;
    private Task? _sendTask;
    private Task? _socketReadTask;
    private RequestQueue? _requestQueue;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile bool _isConnected;
    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ValkeyConnection"/> class.
    /// </summary>
    /// <param name="endpoint">The endpoint to connect to.</param>
    /// <param name="options">Connection options.</param>
    public ValkeyConnection(EndPoint endpoint, ValkeyOptions options)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public bool IsConnected => _isConnected && _socket?.Connected == true;

    /// <inheritdoc/>
    public string Endpoint => _endpoint.ToString() ?? string.Empty;

    /// <summary>
    /// Creates and connects a new Valkey connection.
    /// </summary>
    public static async ValueTask<ValkeyConnection> ConnectAsync(
        EndPoint endpoint,
        ValkeyOptions options,
        CancellationToken cancellationToken = default)
    {
        var connection = new ValkeyConnection(endpoint, options);
        await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    /// <inheritdoc/>
    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isConnected)
            {
                return;
            }

            // Create and connect socket
            _socket = await CreateAndConnectSocketAsync(cancellationToken).ConfigureAwait(false);

            // Create stream (with optional SSL)
            _stream = await CreateStreamAsync(_socket, cancellationToken).ConfigureAwait(false);

            // Initialize pipes, parser, writer, and request queue
            InitializePipesAndProtocol();

            // Start background tasks
            _socketReadTask = StartSocketReadAsync(_disposeCts.Token);
            _sendTask = SendLoopAsync(_disposeCts.Token);

            // Perform handshake (HELLO/AUTH/SELECT)
            await HandshakeAsync(cancellationToken).ConfigureAwait(false);

            // Start receive response processing loop
            _receiveTask = ReceiveLoopAsync(_disposeCts.Token);

            _isConnected = true;
        }
        catch
        {
            CleanupOnFailure();
            throw;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Creates and connects a socket with configured options and timeout.
    /// </summary>
    private async Task<Socket> CreateAndConnectSocketAsync(CancellationToken cancellationToken)
    {
        var addressFamily = _endpoint is DnsEndPoint
            ? AddressFamily.InterNetwork
            : (_endpoint as IPEndPoint)?.AddressFamily ?? AddressFamily.InterNetwork;

        var socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true, // Disable Nagle's algorithm for low latency
            SendBufferSize = _options.SendBufferSize,
            ReceiveBufferSize = _options.ReceiveBufferSize
        };

        try
        {
            // Set keep-alive if configured
            if (_options.KeepAlive > 0)
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            }

            // Connect with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.ConnectTimeout);

            try
            {
                await socket.ConnectAsync(_endpoint, cts.Token).ConfigureAwait(false);
                return socket;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Connection to {_endpoint} timed out after {_options.ConnectTimeout}ms");
            }
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a network stream with optional SSL/TLS encryption.
    /// </summary>
    private async Task<Stream> CreateStreamAsync(Socket socket, CancellationToken cancellationToken)
    {
        var networkStream = new NetworkStream(socket, ownsSocket: false);

        if (!_options.UseSsl)
        {
            return networkStream;
        }

        try
        {
            var sslStream = new SslStream(
                networkStream,
                leaveInnerStreamOpen: false,
                _options.CertificateValidationCallback);

            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = _options.SslHost ?? (_endpoint is DnsEndPoint dns ? dns.Host : "valkey"),
                ClientCertificates = _options.ClientCertificate != null
                    ? new X509CertificateCollection { _options.ClientCertificate }
                    : null,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.ConnectTimeout);

            await sslStream.AuthenticateAsClientAsync(sslOptions, cts.Token).ConfigureAwait(false);
            return sslStream;
        }
        catch
        {
            networkStream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Initializes pipes, parser, writer, and request queue for protocol handling.
    /// </summary>
    private void InitializePipesAndProtocol()
    {
        // Create pipes for async I/O
        _receivePipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: _options.ReceiveBufferSize,
            resumeWriterThreshold: _options.ReceiveBufferSize / 2,
            minimumSegmentSize: 4096));

        _sendPipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: _options.SendBufferSize,
            resumeWriterThreshold: _options.SendBufferSize / 2,
            minimumSegmentSize: 4096));

        // Create parser and writer
        _parser = new Resp3Parser(_receivePipe.Reader);
        _writer = new Resp3Writer(_sendPipe.Writer);

        // Create request queue
        _requestQueue = new RequestQueue();
    }

    /// <summary>
    /// Cleans up resources on connection failure.
    /// </summary>
    private void CleanupOnFailure()
    {
        _stream?.Dispose();
        _socket?.Dispose();
        _stream = null;
        _socket = null;
        _receivePipe = null;
        _sendPipe = null;
        _parser = null;
        _writer = null;
        _requestQueue = null;
    }

    /// <summary>
    /// Performs initial handshake with the server (HELLO, AUTH, SELECT).
    /// </summary>
    private async ValueTask HandshakeAsync(CancellationToken cancellationToken)
    {
        if (_writer == null || _parser == null)
        {
            throw new InvalidOperationException("Connection not initialized");
        }

        // Switch to RESP3 if preferred
        if (_options.PreferResp3)
        {
            await TryResp3HandshakeAsync(cancellationToken).ConfigureAwait(false);
        }

        // Set client name if specified
        if (!string.IsNullOrEmpty(_options.ClientName))
        {
            await SetClientNameAsync(cancellationToken).ConfigureAwait(false);
        }

        // Select database if not default
        if (_options.DefaultDatabase != 0)
        {
            await SelectDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Attempts RESP3 handshake with HELLO command, falls back to RESP2 AUTH if needed.
    /// </summary>
    private async ValueTask TryResp3HandshakeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SendHelloCommandAsync(cancellationToken).ConfigureAwait(false);
            var response = await _parser!.ReadAsync(cancellationToken).ConfigureAwait(false);

            response.ThrowIfError();
        }
        catch (RespException)
        {
            // RESP3 not supported, fall back to RESP2 AUTH
            await FallbackToResp2AuthAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends HELLO command with optional authentication.
    /// </summary>
    private async ValueTask SendHelloCommandAsync(CancellationToken cancellationToken)
    {
        byte[]? userBuffer = null;
        byte[]? passwordBuffer = null;
        ReadOnlyMemory<byte>[]? args = null;

        try
        {
            if (!string.IsNullOrEmpty(_options.User) && !string.IsNullOrEmpty(_options.Password))
            {
                // HELLO 3 AUTH <user> <password>
                var (userBuf, userLen) = CommandBuilder.EncodeValue(_options.User);
                var (passBuf, passLen) = CommandBuilder.EncodeValue(_options.Password);
                userBuffer = userBuf;
                passwordBuffer = passBuf;

                args = ArgumentArrayPool.Rent(4);
                args[0] = CommandBytes.Resp3Version;
                args[1] = CommandBytes.Auth;
                args[2] = CommandBuilder.AsMemory(userBuffer, userLen);
                args[3] = CommandBuilder.AsMemory(passwordBuffer, passLen);

                _writer!.WriteCommand(CommandBytes.Hello, args, 4);
            }
            else if (!string.IsNullOrEmpty(_options.Password))
            {
                // HELLO 3 AUTH default <password>
                var (passBuf, passLen) = CommandBuilder.EncodeValue(_options.Password);
                passwordBuffer = passBuf;

                args = ArgumentArrayPool.Rent(4);
                args[0] = CommandBytes.Resp3Version;
                args[1] = CommandBytes.Auth;
                args[2] = CommandBytes.DefaultUser;
                args[3] = CommandBuilder.AsMemory(passwordBuffer, passLen);

                _writer!.WriteCommand(CommandBytes.Hello, args, 4);
            }
            else
            {
                // HELLO 3
                args = ArgumentArrayPool.Rent(1);
                args[0] = CommandBytes.Resp3Version;

                _writer!.WriteCommand(CommandBytes.Hello, args, 1);
            }

            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (userBuffer != null)
            {
                CommandBuilder.Return(userBuffer);
            }
            if (passwordBuffer != null)
            {
                CommandBuilder.Return(passwordBuffer);
            }
            if (args != null)
            {
                ArgumentArrayPool.Return(args);
            }
        }
    }

    /// <summary>
    /// Falls back to RESP2 authentication using AUTH command.
    /// </summary>
    private async ValueTask FallbackToResp2AuthAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.Password))
        {
            return; // No authentication needed
        }

        byte[]? userBuffer = null;
        byte[]? passwordBuffer = null;
        ReadOnlyMemory<byte>[]? args = null;

        try
        {
            if (!string.IsNullOrEmpty(_options.User))
            {
                // AUTH <user> <password>
                var (userBuf, userLen) = CommandBuilder.EncodeValue(_options.User);
                var (passBuf, passLen) = CommandBuilder.EncodeValue(_options.Password);
                userBuffer = userBuf;
                passwordBuffer = passBuf;

                args = ArgumentArrayPool.Rent(2);
                args[0] = CommandBuilder.AsMemory(userBuffer, userLen);
                args[1] = CommandBuilder.AsMemory(passwordBuffer, passLen);

                _writer!.WriteCommand(CommandBytes.Auth, args, 2);
            }
            else
            {
                // AUTH <password>
                var (passBuf, passLen) = CommandBuilder.EncodeValue(_options.Password);
                passwordBuffer = passBuf;

                args = ArgumentArrayPool.Rent(1);
                args[0] = CommandBuilder.AsMemory(passwordBuffer, passLen);

                _writer!.WriteCommand(CommandBytes.Auth, args, 1);
            }

            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            var authResponse = await _parser!.ReadAsync(cancellationToken).ConfigureAwait(false);
            authResponse.ThrowIfError();
        }
        finally
        {
            if (userBuffer != null)
            {
                CommandBuilder.Return(userBuffer);
            }
            if (passwordBuffer != null)
            {
                CommandBuilder.Return(passwordBuffer);
            }
            if (args != null)
            {
                ArgumentArrayPool.Return(args);
            }
        }
    }

    /// <summary>
    /// Sets the client name using CLIENT SETNAME command.
    /// </summary>
    private async ValueTask SetClientNameAsync(CancellationToken cancellationToken)
    {
        byte[]? nameBuffer = null;
        ReadOnlyMemory<byte>[]? args = null;

        try
        {
            var (nameBuf, nameLen) = CommandBuilder.EncodeValue(_options.ClientName!);
            nameBuffer = nameBuf;

            args = ArgumentArrayPool.Rent(2);
            args[0] = CommandBytes.Setname;
            args[1] = CommandBuilder.AsMemory(nameBuffer, nameLen);

            _writer!.WriteCommand(CommandBytes.Client, args, 2);

            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            var nameResponse = await _parser!.ReadAsync(cancellationToken).ConfigureAwait(false);
            // Non-fatal error, ignore if it fails
        }
        finally
        {
            if (nameBuffer != null)
            {
                CommandBuilder.Return(nameBuffer);
            }
            if (args != null)
            {
                ArgumentArrayPool.Return(args);
            }
        }
    }

    /// <summary>
    /// Selects the database using SELECT command.
    /// </summary>
    private async ValueTask SelectDatabaseAsync(CancellationToken cancellationToken)
    {
        byte[]? dbBuffer = null;
        ReadOnlyMemory<byte>[]? args = null;

        try
        {
            var (dbBuf, dbLen) = CommandBuilder.EncodeValue(_options.DefaultDatabase.ToString());
            dbBuffer = dbBuf;

            args = ArgumentArrayPool.Rent(1);
            args[0] = CommandBuilder.AsMemory(dbBuffer, dbLen);

            _writer!.WriteCommand(CommandBytes.Select, args, 1);

            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            var selectResponse = await _parser!.ReadAsync(cancellationToken).ConfigureAwait(false);
            selectResponse.ThrowIfError();
        }
        finally
        {
            if (dbBuffer != null)
            {
                CommandBuilder.Return(dbBuffer);
            }
            if (args != null)
            {
                ArgumentArrayPool.Return(args);
            }
        }
    }

    /// <summary>
    /// Starts the background task that reads from socket and writes to the receive pipe.
    /// </summary>
    private Task StartSocketReadAsync(CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _stream != null)
                {
                    var memory = _receivePipe!.Writer.GetMemory(_options.ReceiveBufferSize);
                    var bytesRead = await _stream.ReadAsync(memory, cancellationToken).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    _receivePipe.Writer.Advance(bytesRead);
                    var result = await _receivePipe.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                await _receivePipe!.Writer.CompleteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _receivePipe!.Writer.CompleteAsync(ex).ConfigureAwait(false);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Receives data from the parser and processes responses.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Parse responses and correlate with requests
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _parser!.ReadAsync(cancellationToken).ConfigureAwait(false);

                    if (!await _requestQueue!.ProcessResponseAsync(response, cancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _requestQueue?.FailAll(ex);
        }
    }

    /// <summary>
    /// Reads data from the pipe and sends to the socket.
    /// </summary>
    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream != null)
            {
                var result = await _sendPipe!.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;

                try
                {
                    foreach (var segment in buffer)
                    {
                        await _stream.WriteAsync(segment, cancellationToken).ConfigureAwait(false);
                    }

                    await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                    _sendPipe.Reader.AdvanceTo(buffer.End);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                catch
                {
                    _sendPipe.Reader.AdvanceTo(buffer.Start);
                    throw;
                }
            }

            await _sendPipe!.Reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _sendPipe!.Reader.CompleteAsync(ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the RESP3 writer for this connection.
    /// </summary>
    internal Resp3Writer Writer => _writer ?? throw new InvalidOperationException("Connection not established");

    /// <summary>
    /// Gets the RESP3 parser for this connection.
    /// </summary>
    internal Resp3Parser Parser => _parser ?? throw new InvalidOperationException("Connection not established");

    /// <summary>
    /// Gets a database instance for executing commands.
    /// </summary>
    /// <param name="database">The database number (default is 0).</param>
    /// <returns>A database instance.</returns>
    public ValkeyDatabase GetDatabase(int database = 0)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("Connection is not established");
        }

        return new ValkeyDatabase(this, database);
    }

    /// <summary>
    /// Executes a command and returns the response.
    /// </summary>
    internal async ValueTask<RespValue> ExecuteCommandAsync(
        ReadOnlyMemory<byte> command,
        ReadOnlyMemory<byte>[] args,
        int argCount,
        CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("Connection is not established");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Enqueue the request
            var responseTask = _requestQueue!.EnqueueAsync(cancellationToken);

            // Write the command - only use the specified number of args
            _writer!.WriteCommand(command, args, argCount);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Wait for the response
            return await responseTask.ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisconnectAsync()
    {
        if (!_isConnected)
        {
            return;
        }

        _isConnected = false;

        // Complete pipes
        if (_sendPipe != null)
        {
            await _sendPipe.Writer.CompleteAsync().ConfigureAwait(false);
        }

        // Wait for tasks to complete
        if (_sendTask != null)
        {
            await _sendTask.ConfigureAwait(false);
        }

        if (_receiveTask != null)
        {
            await _receiveTask.ConfigureAwait(false);
        }

        if (_socketReadTask != null)
        {
            await _socketReadTask.ConfigureAwait(false);
        }

        // Close stream and socket
        _stream?.Dispose();
        _socket?.Dispose();

        _stream = null;
        _socket = null;
    }

    #region Pub/Sub Support

    /// <summary>
    /// Sends a command directly to the server without going through the request queue.
    /// Used by the subscriber for SUBSCRIBE/UNSUBSCRIBE commands.
    /// </summary>
    internal async ValueTask SendCommandAsync(byte[] command, ReadOnlyMemory<byte>[] args, int argCount, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("Not connected");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Write command using the writer - convert byte[] to ReadOnlyMemory<byte>
            _writer!.WriteCommand(new ReadOnlyMemory<byte>(command), args, argCount);
            await _sendPipe!.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Reads a push message from the server.
    /// Used by the subscriber to receive pub/sub messages.
    /// </summary>
    internal async ValueTask<RespValue?> ReadPushMessageAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            return null;
        }

        try
        {
            var response = await _parser!.ReadAsync(cancellationToken).ConfigureAwait(false);

            // Check if it's a pub/sub message (handles both RESP2 and RESP3)
            if (PubSubMessageValidator.IsPubSubMessage(response))
            {
                return response;
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _disposeCts.CancelAsync().ConfigureAwait(false);
        await DisconnectAsync().ConfigureAwait(false);

        if (_requestQueue != null)
        {
            await _requestQueue.DisposeAsync().ConfigureAwait(false);
        }

        _connectLock.Dispose();
        _writeLock.Dispose();
        _disposeCts.Dispose();
    }
}
