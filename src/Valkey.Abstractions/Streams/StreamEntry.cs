namespace Valkey.Abstractions.Streams;

/// <summary>
/// Represents a single entry in a stream.
/// </summary>
public readonly struct StreamEntry
{
    /// <summary>
    /// Gets the unique ID of the stream entry.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the field-value pairs of the stream entry.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamEntry"/> struct.
    /// </summary>
    /// <param name="id">The entry ID.</param>
    /// <param name="fields">The field-value pairs.</param>
    public StreamEntry(string id, Dictionary<string, string> fields)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Fields = fields ?? throw new ArgumentNullException(nameof(fields));
    }
}
