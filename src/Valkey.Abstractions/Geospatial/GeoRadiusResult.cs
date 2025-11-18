namespace Valkey.Abstractions.Geospatial;

/// <summary>
/// Represents a result from a geospatial radius or search query.
/// </summary>
public sealed class GeoRadiusResult
{
    /// <summary>
    /// Gets the member name.
    /// </summary>
    public string Member { get; }

    /// <summary>
    /// Gets the distance from the center point (if requested with WITHDIST).
    /// </summary>
    public double? Distance { get; }

    /// <summary>
    /// Gets the position coordinates (if requested with WITHCOORD).
    /// </summary>
    public GeoPosition? Position { get; }

    /// <summary>
    /// Gets the geohash value (if requested with WITHHASH).
    /// </summary>
    public long? Hash { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoRadiusResult"/> class.
    /// </summary>
    public GeoRadiusResult(string member, double? distance = null, GeoPosition? position = null, long? hash = null)
    {
        Member = member ?? throw new ArgumentNullException(nameof(member));
        Distance = distance;
        Position = position;
        Hash = hash;
    }

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string> { Member };
        if (Distance.HasValue)
        {
            parts.Add($"Distance: {Distance.Value}");
        }
        if (Position.HasValue)
        {
            parts.Add($"Position: {Position.Value}");
        }
        if (Hash.HasValue)
        {
            parts.Add($"Hash: {Hash.Value}");
        }
        return string.Join(", ", parts);
    }
}
