using System.Runtime.CompilerServices;

namespace Valkey.Protocol;

/// <summary>
/// Extension methods for RespValue to simplify common operations.
/// </summary>
internal static class RespValueExtensions
{
    /// <summary>
    /// Checks if the response is an error (SimpleError or BulkError).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsError(this RespValue response)
        => response.Type == RespType.SimpleError || response.Type == RespType.BulkError;

    /// <summary>
    /// Throws a RespException if the response is an error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfError(this RespValue response)
    {
        if (response.IsError())
        {
            throw RespException.FromError(response);
        }
    }

    /// <summary>
    /// Checks if the type represents a string (SimpleString or BulkString).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStringType(this RespValue response)
        => response.Type == RespType.SimpleString || response.Type == RespType.BulkString;

    /// <summary>
    /// Checks if the type represents an error (SimpleError or BulkError).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsErrorType(this RespValue response)
        => response.Type == RespType.SimpleError || response.Type == RespType.BulkError;

    /// <summary>
    /// Checks if the type can be converted to bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBytesType(this RespValue response)
        => response.IsStringType() || response.IsErrorType();
}
