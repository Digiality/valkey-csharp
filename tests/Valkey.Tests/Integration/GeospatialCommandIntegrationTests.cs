using FluentAssertions;
using Valkey;
using Valkey.Abstractions.Geospatial;

namespace Valkey.Tests.Integration;

/// <summary>
/// Integration tests for geospatial commands against a real Valkey/Redis server.
/// </summary>
public class GeospatialCommandIntegrationTests : IntegrationTestBase
{
    // Test data: Famous cities with their coordinates
    private const double SanFranciscoLon = -122.4194;
    private const double SanFranciscoLat = 37.7749;

    private const double LosAngelesLon = -118.2437;
    private const double LosAngelesLat = 34.0522;

    private const double NewYorkLon = -74.0060;
    private const double NewYorkLat = 40.7128;

    private const double LondonLon = -0.1278;
    private const double LondonLat = 51.5074;

    [Fact]
    public async Task GeoAdd_AddsLocation()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var result = await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");

        // Assert
        result.Should().Be(1); // 1 element added
    }

    [Fact]
    public async Task GeoAdd_DuplicateMember_ReturnsZero()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");

        // Act - Add same member again
        var result = await Database.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");

        // Assert
        result.Should().Be(0); // 0 new elements added
    }

    [Fact]
    public async Task GeoPosition_ReturnsCorrectCoordinates()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");

        // Act
        var positions = await Database.GeoPositionAsync(key, new[] { "San Francisco" });

        // Assert
        positions.Should().HaveCount(1);
        positions[0].Should().NotBeNull();
        positions[0]!.Value.Longitude.Should().BeApproximately(SanFranciscoLon, 0.01);
        positions[0]!.Value.Latitude.Should().BeApproximately(SanFranciscoLat, 0.01);
    }

    [Fact]
    public async Task GeoPosition_NonExistentMember_ReturnsNull()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var positions = await Database!.GeoPositionAsync(key, new[] { "NonExistent" });

        // Assert
        positions.Should().HaveCount(1);
        positions[0].Should().BeNull();
    }

    [Fact]
    public async Task GeoPosition_MultipleMembers_ReturnsAllPositions()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");
        await Database.GeoAddAsync(key, NewYorkLon, NewYorkLat, "New York");

        // Act
        var positions = await Database.GeoPositionAsync(key, new[] { "San Francisco", "Los Angeles", "New York" });

        // Assert
        positions.Should().HaveCount(3);
        positions.Should().AllSatisfy(p => p.Should().NotBeNull());
    }

    [Fact]
    public async Task GeoDistance_CalculatesCorrectDistance_Meters()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");

        // Act
        var distance = await Database.GeoDistanceAsync(key, "San Francisco", "Los Angeles", GeoUnit.Meters);

        // Assert
        distance.Should().NotBeNull();
        // Distance between SF and LA is approximately 559 km = 559,000 meters
        distance!.Value.Should().BeInRange(550000, 570000);
    }

    [Fact]
    public async Task GeoDistance_CalculatesCorrectDistance_Kilometers()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");

        // Act
        var distance = await Database.GeoDistanceAsync(key, "San Francisco", "Los Angeles", GeoUnit.Kilometers);

        // Assert
        distance.Should().NotBeNull();
        // Distance between SF and LA is approximately 559 km
        distance!.Value.Should().BeInRange(550, 570);
    }

    [Fact]
    public async Task GeoDistance_CalculatesCorrectDistance_Miles()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");

        // Act
        var distance = await Database.GeoDistanceAsync(key, "San Francisco", "Los Angeles", GeoUnit.Miles);

        // Assert
        distance.Should().NotBeNull();
        // Distance between SF and LA is approximately 347 miles
        distance!.Value.Should().BeInRange(340, 360);
    }

    [Fact]
    public async Task GeoDistance_NonExistentMember_ReturnsNull()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");

        // Act
        var distance = await Database.GeoDistanceAsync(key, "San Francisco", "NonExistent");

        // Assert
        distance.Should().BeNull();
    }

    [Fact]
    public async Task GeoHash_ReturnsGeohashStrings()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");

        // Act
        var hashes = await Database.GeoHashAsync(key, new[] { "San Francisco", "Los Angeles" });

        // Assert
        hashes.Should().HaveCount(2);
        hashes[0].Should().NotBeNullOrEmpty();
        hashes[1].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GeoHash_NonExistentMember_ReturnsNull()
    {
        // Arrange
        var key = GetTestKey();

        // Act
        var hashes = await Database!.GeoHashAsync(key, new[] { "NonExistent" });

        // Assert
        hashes.Should().HaveCount(1);
        hashes[0].Should().BeNull();
    }

    [Fact]
    public async Task GeoRadius_FindsLocationsWithinRadius()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");
        await Database.GeoAddAsync(key, NewYorkLon, NewYorkLat, "New York");

        // Act - Search within 600km of San Francisco
        var results = await Database.GeoRadiusAsync(key, SanFranciscoLon, SanFranciscoLat, 600, GeoUnit.Kilometers);

        // Assert
        results.Should().HaveCount(2); // SF and LA, but not NY
        results.Select(r => r.Member).Should().Contain("San Francisco");
        results.Select(r => r.Member).Should().Contain("Los Angeles");
        results.Select(r => r.Member).Should().NotContain("New York");
    }

    [Fact]
    public async Task GeoRadius_WithDistance_ReturnsDistances()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");

        // Act
        var results = await Database.GeoRadiusAsync(key, SanFranciscoLon, SanFranciscoLat, 600, GeoUnit.Kilometers, withDistance: true);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r => r.Distance.Should().NotBeNull());
    }

    [Fact]
    public async Task GeoRadius_WithCoordinates_ReturnsPositions()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");

        // Act
        var results = await Database.GeoRadiusAsync(key, SanFranciscoLon, SanFranciscoLat, 600, GeoUnit.Kilometers, withCoordinates: true);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r => r.Position.Should().NotBeNull());
    }

    [Fact]
    public async Task GeoRadius_WithCount_LimitsResults()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");
        await Database.GeoAddAsync(key, NewYorkLon, NewYorkLat, "New York");

        // Act - Search all US, but limit to 2 results
        var results = await Database.GeoRadiusAsync(key, SanFranciscoLon, SanFranciscoLat, 5000, GeoUnit.Kilometers, count: 2);

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GeoRadiusByMember_FindsLocationsWithinRadius()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");
        await Database.GeoAddAsync(key, NewYorkLon, NewYorkLat, "New York");

        // Act - Search within 600km of San Francisco
        var results = await Database.GeoRadiusByMemberAsync(key, "San Francisco", 600, GeoUnit.Kilometers);

        // Assert
        results.Should().HaveCount(2); // SF and LA, but not NY
        results.Select(r => r.Member).Should().Contain("San Francisco");
        results.Select(r => r.Member).Should().Contain("Los Angeles");
    }

    [Fact]
    public async Task GeoRadiusByMember_WithDistance_ReturnsDistances()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");

        // Act
        var results = await Database.GeoRadiusByMemberAsync(key, "San Francisco", 600, GeoUnit.Kilometers, withDistance: true);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r => r.Distance.Should().NotBeNull());

        // San Francisco should have distance 0 from itself
        var sfResult = results.First(r => r.Member == "San Francisco");
        sfResult.Distance!.Value.Should().BeApproximately(0, 0.1);
    }

    [Fact]
    public async Task GeoSearchByPolygon_FindsLocationsWithinPolygon()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");
        await Database.GeoAddAsync(key, NewYorkLon, NewYorkLat, "New York");
        await Database.GeoAddAsync(key, LondonLon, LondonLat, "London");

        // Define a polygon covering California (approximate)
        var polygon = new[]
        {
            new GeoPosition(-124.0, 42.0),  // Northwest corner
            new GeoPosition(-114.0, 42.0),  // Northeast corner
            new GeoPosition(-114.0, 32.0),  // Southeast corner
            new GeoPosition(-124.0, 32.0),  // Southwest corner
            new GeoPosition(-124.0, 42.0)   // Close the polygon
        };

        // Act
        var results = await Database.GeoSearchByPolygonAsync(key, polygon);

        // Assert
        results.Should().HaveCount(2); // SF and LA are in California
        results.Select(r => r.Member).Should().Contain("San Francisco");
        results.Select(r => r.Member).Should().Contain("Los Angeles");
        results.Select(r => r.Member).Should().NotContain("New York");
        results.Select(r => r.Member).Should().NotContain("London");
    }

    [Fact]
    public async Task GeoSearchByPolygon_WithDistance_ReturnsDistances()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");

        var polygon = new[]
        {
            new GeoPosition(-124.0, 42.0),
            new GeoPosition(-114.0, 42.0),
            new GeoPosition(-114.0, 32.0),
            new GeoPosition(-124.0, 32.0),
            new GeoPosition(-124.0, 42.0)
        };

        // Act
        var results = await Database.GeoSearchByPolygonAsync(key, polygon, withDistance: true);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r => r.Distance.Should().NotBeNull());
    }

    [Fact]
    public async Task GeoSearchByPolygon_WithCoordinates_ReturnsPositions()
    {
        // Arrange
        var key = GetTestKey();
        await Database!.GeoAddAsync(key, SanFranciscoLon, SanFranciscoLat, "San Francisco");
        await Database.GeoAddAsync(key, LosAngelesLon, LosAngelesLat, "Los Angeles");

        var polygon = new[]
        {
            new GeoPosition(-124.0, 42.0),
            new GeoPosition(-114.0, 42.0),
            new GeoPosition(-114.0, 32.0),
            new GeoPosition(-124.0, 32.0),
            new GeoPosition(-124.0, 42.0)
        };

        // Act
        var results = await Database.GeoSearchByPolygonAsync(key, polygon, withCoordinates: true);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r => r.Position.Should().NotBeNull());
    }

    [Fact]
    public async Task GeoSearchByPolygon_EmptyPolygon_ThrowsException()
    {
        // Arrange
        var key = GetTestKey();
        var polygon = Array.Empty<GeoPosition>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Database!.GeoSearchByPolygonAsync(key, polygon)
        );
    }

    [Fact]
    public async Task GeoSearchByPolygon_TooFewVertices_ThrowsException()
    {
        // Arrange
        var key = GetTestKey();
        var polygon = new[]
        {
            new GeoPosition(-124.0, 42.0),
            new GeoPosition(-114.0, 42.0)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Database!.GeoSearchByPolygonAsync(key, polygon)
        );
    }

    [Fact]
    public async Task ConcurrentGeoOperations_AllSucceed()
    {
        // Arrange
        var key = GetTestKey();
        var cities = new[]
        {
            ("City1", -122.0, 37.0),
            ("City2", -122.1, 37.1),
            ("City3", -122.2, 37.2),
            ("City4", -122.3, 37.3),
            ("City5", -122.4, 37.4)
        };

        // Act - Add cities concurrently
        var tasks = cities.Select(city =>
            Database!.GeoAddAsync(key, city.Item2, city.Item3, city.Item1).AsTask()
        );

        await Task.WhenAll(tasks);

        // Assert - All cities should be added
        var positions = await Database!.GeoPositionAsync(key, cities.Select(c => c.Item1).ToArray());
        positions.Should().HaveCount(5);
        positions.Should().AllSatisfy(p => p.Should().NotBeNull());
    }
}
