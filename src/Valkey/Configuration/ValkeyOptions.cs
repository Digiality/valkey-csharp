using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Valkey.Configuration;

/// <summary>
/// Configuration options for Valkey connections.
/// </summary>
public sealed class ValkeyOptions
{
    /// <summary>
    /// Gets or sets the endpoints to connect to.
    /// </summary>
    public List<EndPoint> Endpoints { get; set; } = new();

    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the command timeout in milliseconds.
    /// </summary>
    public int CommandTimeout { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the keep-alive interval in seconds. 0 disables keep-alive.
    /// </summary>
    public int KeepAlive { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether to use SSL/TLS.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets the SSL host for certificate validation.
    /// </summary>
    public string? SslHost { get; set; }

    /// <summary>
    /// Gets or sets the client certificate for mutual TLS.
    /// </summary>
    public X509Certificate? ClientCertificate { get; set; }

    /// <summary>
    /// Gets or sets the server certificate validation callback.
    /// </summary>
    public RemoteCertificateValidationCallback? CertificateValidationCallback { get; set; }

    /// <summary>
    /// Gets or sets the password for authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the username for ACL authentication.
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Gets or sets the client name.
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Gets or sets the default database to select on connect.
    /// </summary>
    public int DefaultDatabase { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to abort connect if no servers are available.
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow admin commands (dangerous commands).
    /// </summary>
    public bool AllowAdmin { get; set; }

    /// <summary>
    /// Gets or sets the reconnect retry policy.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of reconnection attempts. -1 for infinite.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = -1;

    /// <summary>
    /// Gets or sets the base delay for exponential backoff on reconnect (milliseconds).
    /// </summary>
    public int ReconnectBaseDelay { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum delay for exponential backoff on reconnect (milliseconds).
    /// </summary>
    public int ReconnectMaxDelay { get; set; } = 30000;

    /// <summary>
    /// Gets or sets whether to prefer RESP3 protocol.
    /// </summary>
    public bool PreferResp3 { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this is a cluster connection.
    /// </summary>
    public bool IsCluster { get; set; }

    /// <summary>
    /// Gets or sets the send buffer size in bytes.
    /// </summary>
    public int SendBufferSize { get; set; } = 32768;

    /// <summary>
    /// Gets or sets the receive buffer size in bytes.
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 32768;

    /// <summary>
    /// Creates default options for a localhost connection.
    /// </summary>
    public static ValkeyOptions Default => new()
    {
        Endpoints = { new DnsEndPoint("localhost", 6379) }
    };

    /// <summary>
    /// Creates options from a connection string.
    /// </summary>
    public static ValkeyOptions Parse(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        var options = new ValkeyOptions();

        // Parse connection string (simplified version)
        // Format: host:port,password=xxx,ssl=true,user=xxx
        var parts = connectionString.Split(',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            if (trimmed.Contains('='))
            {
                var kvp = trimmed.Split('=', 2);
                var key = kvp[0].Trim().ToLowerInvariant();
                var value = kvp[1].Trim();

                switch (key)
                {
                    case "password":
                        options.Password = value;
                        break;
                    case "user":
                        options.User = value;
                        break;
                    case "ssl":
                        options.UseSsl = bool.Parse(value);
                        break;
                    case "sslhost":
                        options.SslHost = value;
                        break;
                    case "name":
                    case "clientname":
                        options.ClientName = value;
                        break;
                    case "defaultdatabase":
                    case "database":
                    case "db":
                        options.DefaultDatabase = int.Parse(value);
                        break;
                    case "connecttimeout":
                        options.ConnectTimeout = int.Parse(value);
                        break;
                    case "keepalive":
                        options.KeepAlive = int.Parse(value);
                        break;
                    case "abortconnect":
                        options.AbortOnConnectFail = bool.Parse(value);
                        break;
                    case "allowadmin":
                        options.AllowAdmin = bool.Parse(value);
                        break;
                }
            }
            else
            {
                // Endpoint
                var endpoint = ParseEndpoint(trimmed);
                if (endpoint != null)
                {
                    options.Endpoints.Add(endpoint);
                }
            }
        }

        if (options.Endpoints.Count == 0)
        {
            throw new ArgumentException("No endpoints specified in connection string", nameof(connectionString));
        }

        return options;
    }

    private static EndPoint? ParseEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var parts = endpoint.Split(':');
        var host = parts[0];
        var port = parts.Length > 1 ? int.Parse(parts[1]) : 6379;

        return new DnsEndPoint(host, port);
    }
}
