using Valkey.Abstractions.Streams;
using Valkey.Protocol;

namespace Valkey.ResponseParsers;

/// <summary>
/// Parses stream-related RESP responses, handling both RESP2 and RESP3 formats.
/// </summary>
internal static class StreamParsers
{
    /// <summary>
    /// Parses stream entries from XRANGE/XREVRANGE responses.
    /// </summary>
    /// <param name="response">The RESP response containing stream entries.</param>
    /// <returns>An array of stream entries.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the response format is invalid.</exception>
    /// <remarks>
    /// Expected format: [[id, [field, value, ...]], [id, [field, value, ...]], ...]
    /// Each entry is an array with 2 elements: entry ID and field-value pairs.
    /// </remarks>
    public static StreamEntry[] ParseEntries(RespValue response)
    {
        if (!response.TryGetArray(out var array))
        {
            throw new InvalidOperationException($"Stream entries: Expected array response, got {response.Type}");
        }

        var entries = new List<StreamEntry>(array.Length);

        foreach (var entryValue in array)
        {
            if (!entryValue.TryGetArray(out var entryArray))
            {
                throw new InvalidOperationException(
                    $"Stream entry: Expected array for entry, got {entryValue.Type}");
            }

            if (entryArray.Length < 2)
            {
                throw new InvalidOperationException(
                    $"Stream entry: Expected at least 2 elements [id, fields], got {entryArray.Length}");
            }

            if (!entryArray[0].TryGetString(out var id))
            {
                throw new InvalidOperationException(
                    $"Stream entry: Expected string ID, got {entryArray[0].Type}");
            }

            var fields = ParseFields(entryArray[1]);
            entries.Add(new StreamEntry(id!, fields));
        }

        return entries.ToArray();
    }

    /// <summary>
    /// Parses stream entries from XREAD/XREADGROUP responses.
    /// </summary>
    /// <param name="response">The RESP response from XREAD/XREADGROUP.</param>
    /// <returns>An array of stream entries.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the response format is invalid.</exception>
    /// <remarks>
    /// RESP3 format: Map {stream_name => [[id, [field, value, ...]], ...]}
    /// RESP2 format: Array [[stream_name, [[id, [field, value, ...]], ...]]]
    /// </remarks>
    public static StreamEntry[] ParseReadResponse(RespValue response)
    {
        // RESP3 may return a Map instead of Array
        if (response.TryGetMap(out var map))
        {
            // Map format: {stream_name => entries_array}
            // Note: XREAD can return multiple streams, but we only take the first one
            // (typical usage is single stream per XREAD call)
            foreach (var kvp in map)
            {
                return ParseEntries(kvp.Value);
            }
        }
        else if (response.TryGetArray(out var array))
        {
            // Array format: [[stream_name, entries_array]]
            if (array.Length == 0)
            {
                return Array.Empty<StreamEntry>();
            }

            if (!array[0].TryGetArray(out var streamData))
            {
                throw new InvalidOperationException(
                    $"XREAD response: Expected array for stream data, got {array[0].Type}");
            }

            if (streamData.Length < 2)
            {
                throw new InvalidOperationException(
                    $"XREAD response: Expected [stream_name, entries], got {streamData.Length} elements");
            }

            return ParseEntries(streamData[1]);
        }

        return Array.Empty<StreamEntry>();
    }

    /// <summary>
    /// Parses field-value pairs from a stream entry.
    /// </summary>
    /// <param name="fieldsValue">The RESP value containing field-value pairs.</param>
    /// <returns>A dictionary of field-value pairs.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the response format is invalid.</exception>
    /// <remarks>
    /// RESP2 format: Flat array [field1, value1, field2, value2, ...]
    /// RESP3 format: Map {field1 => value1, field2 => value2, ...}
    /// </remarks>
    public static Dictionary<string, string> ParseFields(RespValue fieldsValue)
    {
        var fields = new Dictionary<string, string>();

        if (fieldsValue.TryGetArray(out var array))
        {
            // RESP2: Flat array [field, value, field, value, ...]
            if (array.Length % 2 != 0)
            {
                throw new InvalidOperationException(
                    $"Stream fields: Expected even-length array for field-value pairs, got {array.Length} elements");
            }

            for (int i = 0; i < array.Length; i += 2)
            {
                if (!array[i].TryGetString(out var field))
                {
                    throw new InvalidOperationException(
                        $"Stream fields: Expected string field at index {i}, got {array[i].Type}");
                }

                if (!array[i + 1].TryGetString(out var value))
                {
                    throw new InvalidOperationException(
                        $"Stream fields: Expected string value at index {i + 1}, got {array[i + 1].Type}");
                }

                fields[field!] = value!;
            }
        }
        else if (fieldsValue.TryGetMap(out var map))
        {
            // RESP3: Map format
            foreach (var kvp in map)
            {
                if (!kvp.Key.TryGetString(out var field))
                {
                    throw new InvalidOperationException(
                        $"Stream fields: Expected string field in map key, got {kvp.Key.Type}");
                }

                if (!kvp.Value.TryGetString(out var value))
                {
                    throw new InvalidOperationException(
                        $"Stream fields: Expected string value in map value, got {kvp.Value.Type}");
                }

                fields[field!] = value!;
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Stream fields: Expected array or map, got {fieldsValue.Type}");
        }

        return fields;
    }
}
