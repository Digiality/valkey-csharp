using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Valkey;
using Valkey.Abstractions;
using Valkey.Configuration;

namespace Valkey.Tests.Integration;

/// <summary>
/// Base class for integration tests that require a running Redis/Valkey server.
/// Uses Testcontainers to spin up a real server instance.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private IContainer? _container;
    protected ValkeyConnection? Connection { get; private set; }
    protected ValkeyDatabase? Database { get; private set; }

    /// <summary>
    /// Gets the Redis/Valkey image to use for testing.
    /// Default is Valkey 9, but can be overridden for specific test scenarios.
    /// </summary>
    protected virtual string ContainerImage => "valkey/valkey:9";

    /// <summary>
    /// Gets the container port for Redis/Valkey.
    /// </summary>
    protected virtual int ContainerPort => 6379;

    /// <summary>
    /// Initializes the test container and establishes a connection.
    /// Called automatically by xUnit before each test.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Build and start the container
        _container = new ContainerBuilder()
            .WithImage(ContainerImage)
            .WithPortBinding(ContainerPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(ContainerPort))
            .Build();

        await _container.StartAsync();

        // Get the mapped port
        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(ContainerPort);

        // Connect to the server
        // Use IPAddress.Loopback instead of hostname to avoid DNS resolution issues on macOS
        var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, port);
        Connection = await ValkeyConnection.ConnectAsync(
            endpoint,
            new ValkeyOptions
            {
                ConnectTimeout = 5000, // 5 seconds in milliseconds
                CommandTimeout = 5000  // 5 seconds in milliseconds
            });

        Database = Connection.GetDatabase();
    }

    /// <summary>
    /// Cleans up the test container and connection.
    /// Called automatically by xUnit after each test.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (Connection != null)
        {
            await Connection.DisposeAsync();
        }

        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Helper method to generate a unique test key to avoid conflicts between tests.
    /// </summary>
    protected string GetTestKey(string suffix = "")
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        return string.IsNullOrEmpty(suffix) ? $"test:{guid}" : $"test:{guid}:{suffix}";
    }
}
