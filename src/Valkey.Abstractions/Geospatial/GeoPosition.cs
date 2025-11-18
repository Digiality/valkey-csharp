namespace Valkey.Abstractions.Geospatial;

/// <summary>
/// Represents a geographical position with longitude and latitude.
/// </summary>
public readonly struct GeoPosition
{
    /// <summary>
    /// Gets the longitude coordinate.
    /// </summary>
    public double Longitude { get; }

    /// <summary>
    /// Gets the latitude coordinate.
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoPosition"/> struct.
    /// </summary>
    /// <param name="longitude">The longitude coordinate (-180 to 180).</param>
    /// <param name="latitude">The latitude coordinate (-85.05112878 to 85.05112878).</param>
    public GeoPosition(double longitude, double latitude)
    {
        Longitude = longitude;
        Latitude = latitude;
    }

    /// <summary>
    /// Returns a string representation of the position.
    /// </summary>
    public override string ToString() => $"({Longitude}, {Latitude})";
}
