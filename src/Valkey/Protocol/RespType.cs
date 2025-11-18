namespace Valkey.Protocol;

/// <summary>
/// Represents the RESP3 protocol data types.
/// </summary>
public enum RespType : byte
{
    /// <summary>
    /// Simple string: +OK\r\n
    /// </summary>
    SimpleString = (byte)'+',

    /// <summary>
    /// Simple error: -ERR message\r\n
    /// </summary>
    SimpleError = (byte)'-',

    /// <summary>
    /// Integer: :1000\r\n
    /// </summary>
    Integer = (byte)':',

    /// <summary>
    /// Bulk string: $5\r\nhello\r\n
    /// </summary>
    BulkString = (byte)'$',

    /// <summary>
    /// Array: *3\r\n:1\r\n:2\r\n:3\r\n
    /// </summary>
    Array = (byte)'*',

    /// <summary>
    /// Null: _\r\n
    /// </summary>
    Null = (byte)'_',

    /// <summary>
    /// Boolean: #t\r\n or #f\r\n
    /// </summary>
    Boolean = (byte)'#',

    /// <summary>
    /// Double: ,1.23\r\n
    /// </summary>
    Double = (byte)',',

    /// <summary>
    /// Big number: (3492890328409238509324850943850943825024385\r\n
    /// </summary>
    BigNumber = (byte)'(',

    /// <summary>
    /// Bulk error: !21\r\nSYNTAX invalid syntax\r\n
    /// </summary>
    BulkError = (byte)'!',

    /// <summary>
    /// Verbatim string: =15\r\ntxt:Some string\r\n
    /// </summary>
    VerbatimString = (byte)'=',

    /// <summary>
    /// Map: %2\r\n+first\r\n:1\r\n+second\r\n:2\r\n
    /// </summary>
    Map = (byte)'%',

    /// <summary>
    /// Set: ~5\r\n+orange\r\n+apple\r\n...\r\n
    /// </summary>
    Set = (byte)'~',

    /// <summary>
    /// Push: >4\r\n+pubsub\r\n+message\r\n...\r\n
    /// </summary>
    Push = (byte)'>',
}
