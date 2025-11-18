namespace Valkey.Protocol;

/// <summary>
/// Exception thrown when a RESP protocol error occurs.
/// </summary>
public class RespException : Exception
{
    /// <summary>
    /// Gets the error type if available.
    /// </summary>
    public string? ErrorType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RespException"/> class.
    /// </summary>
    public RespException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RespException"/> class with a message.
    /// </summary>
    public RespException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RespException"/> class with a message and inner exception.
    /// </summary>
    public RespException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RespException"/> class with error type and message.
    /// </summary>
    public RespException(string errorType, string message)
        : base($"{errorType}: {message}")
    {
        ErrorType = errorType;
    }

    /// <summary>
    /// Creates a RespException from a RESP error value.
    /// </summary>
    internal static RespException FromError(RespValue error)
    {
        if (error.TryGetString(out var message))
        {
            // Try to parse error type from format: "TYPE message"
            var spaceIndex = message.IndexOf(' ');
            if (spaceIndex > 0)
            {
                var errorType = message[..spaceIndex];
                var errorMessage = message[(spaceIndex + 1)..];
                return new RespException(errorType, errorMessage);
            }

            return new RespException(message);
        }

        return new RespException("Unknown error");
    }
}

/// <summary>
/// Exception thrown when a RESP protocol parsing error occurs.
/// </summary>
public class RespProtocolException : RespException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RespProtocolException"/> class.
    /// </summary>
    public RespProtocolException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RespProtocolException"/> class with an inner exception.
    /// </summary>
    public RespProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
