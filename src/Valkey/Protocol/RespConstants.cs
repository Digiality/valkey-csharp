namespace Valkey.Protocol;

/// <summary>
/// Constants used in the RESP3 protocol.
/// </summary>
internal static class RespConstants
{
    /// <summary>
    /// CRLF line terminator.
    /// </summary>
    public static ReadOnlySpan<byte> Crlf => "\r\n"u8;

    /// <summary>
    /// Carriage return byte.
    /// </summary>
    public const byte CR = (byte)'\r';

    /// <summary>
    /// Line feed byte.
    /// </summary>
    public const byte LF = (byte)'\n';

    /// <summary>
    /// Boolean true value.
    /// </summary>
    public static ReadOnlySpan<byte> True => "t"u8;

    /// <summary>
    /// Boolean false value.
    /// </summary>
    public static ReadOnlySpan<byte> False => "f"u8;

    /// <summary>
    /// Null bulk string prefix (RESP2).
    /// </summary>
    public static ReadOnlySpan<byte> NullBulkString => "$-1\r\n"u8;

    /// <summary>
    /// Null array prefix (RESP2).
    /// </summary>
    public static ReadOnlySpan<byte> NullArray => "*-1\r\n"u8;

    /// <summary>
    /// OK response.
    /// </summary>
    public static ReadOnlySpan<byte> Ok => "+OK\r\n"u8;

    /// <summary>
    /// PONG response.
    /// </summary>
    public static ReadOnlySpan<byte> Pong => "+PONG\r\n"u8;

    /// <summary>
    /// Maximum size for inline protocol parsing (prevent DoS).
    /// </summary>
    public const int MaxInlineSize = 64 * 1024; // 64 KB

    /// <summary>
    /// Maximum bulk string size (configurable, default 512MB).
    /// </summary>
    public const int MaxBulkSize = 512 * 1024 * 1024; // 512 MB

    /// <summary>
    /// Maximum array/collection elements (prevent DoS).
    /// </summary>
    public const int MaxCollectionSize = 1_000_000;
}
