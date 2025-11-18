using System.Net;
using Valkey;
using Valkey.Abstractions.Geospatial;
using Valkey.Configuration;

Console.WriteLine("Valkey.NET - Geospatial Operations Demo");
Console.WriteLine("=========================================\n");

// Configure connection options
var options = new ValkeyOptions
{
    Endpoints = { new DnsEndPoint("localhost", 6379) },
    ConnectTimeout = 5000,
    PreferResp3 = true
};

Console.WriteLine($"Connecting to {options.Endpoints[0]}...");

try
{
    // Connect to Valkey
    await using var connection = await ValkeyConnection.ConnectAsync(
        options.Endpoints[0],
        options);

    Console.WriteLine("✅ Connected successfully!\n");

    // Get a database instance
    var db = connection.GetDatabase();

    // ===== SETUP: POPULAR CITIES =====
    Console.WriteLine("=== Setting Up Geospatial Data ===\n");

    var cities = new[]
    {
        ("San Francisco", -122.4194, 37.7749),
        ("Los Angeles", -118.2437, 34.0522),
        ("New York", -74.0060, 40.7128),
        ("Chicago", -87.6298, 41.8781),
        ("Seattle", -122.3321, 47.6062),
        ("Austin", -97.7431, 30.2672),
        ("Boston", -71.0589, 42.3601),
        ("Denver", -104.9903, 39.7392),
        ("Miami", -80.1918, 25.7617),
        ("Portland", -122.6765, 45.5231),
        ("Las Vegas", -115.1398, 36.1699),
        ("Phoenix", -112.0740, 33.4484),
        ("London", -0.1278, 51.5074),
        ("Paris", 2.3522, 48.8566),
        ("Tokyo", 139.6917, 35.6762)
    };

    Console.WriteLine("Adding cities to 'cities' geospatial index...");
    foreach (var (city, lon, lat) in cities)
    {
        await db.GeoAddAsync("cities", lon, lat, city);
        Console.WriteLine($"  ✓ Added {city} ({lat}, {lon})");
    }

    Console.WriteLine($"\n✅ Added {cities.Length} cities to the geospatial index\n");

    // ===== EXAMPLE 1: GET POSITIONS =====
    Console.WriteLine("=== Example 1: Getting City Positions ===\n");

    var cityNames = new[] { "San Francisco", "New York", "London" };
    var positions = await db.GeoPositionAsync("cities", cityNames);

    for (int i = 0; i < cityNames.Length; i++)
    {
        var position = positions[i];
        if (position.HasValue)
        {
            Console.WriteLine($"{cityNames[i]}:");
            Console.WriteLine($"  Longitude: {position.Value.Longitude:F4}");
            Console.WriteLine($"  Latitude:  {position.Value.Latitude:F4}");
        }
    }

    // ===== EXAMPLE 2: CALCULATE DISTANCES =====
    Console.WriteLine("\n=== Example 2: Calculating Distances Between Cities ===\n");

    var cityPairs = new[]
    {
        ("San Francisco", "Los Angeles"),
        ("New York", "Boston"),
        ("London", "Paris"),
        ("Seattle", "Portland")
    };

    foreach (var (city1, city2) in cityPairs)
    {
        var distKm = await db.GeoDistanceAsync("cities", city1, city2, GeoUnit.Kilometers);
        var distMiles = await db.GeoDistanceAsync("cities", city1, city2, GeoUnit.Miles);

        Console.WriteLine($"{city1} ↔ {city2}:");
        Console.WriteLine($"  {distKm:F2} km");
        Console.WriteLine($"  {distMiles:F2} miles");
        Console.WriteLine();
    }

    // ===== EXAMPLE 3: GEOHASH =====
    Console.WriteLine("=== Example 3: Geohash Encoding ===\n");

    var hashCities = new[] { "San Francisco", "New York", "Tokyo" };
    var geohashes = await db.GeoHashAsync("cities", hashCities);

    for (int i = 0; i < hashCities.Length; i++)
    {
        Console.WriteLine($"{hashCities[i]}: {geohashes[i]}");
    }

    // ===== EXAMPLE 4: RADIUS SEARCH =====
    Console.WriteLine("\n=== Example 4: Radius Search ===\n");

    // Find all cities within 500km of San Francisco
    Console.WriteLine("Cities within 500km of San Francisco:");
    var sfPosition = positions[0]!.Value;
    var nearSF = await db.GeoRadiusAsync(
        "cities",
        sfPosition.Longitude,
        sfPosition.Latitude,
        500,
        GeoUnit.Kilometers,
        withDistance: true);

    foreach (var result in nearSF.OrderBy(r => r.Distance))
    {
        Console.WriteLine($"  {result.Member,-20} {result.Distance:F2} km");
    }

    // ===== EXAMPLE 5: RADIUS SEARCH BY MEMBER =====
    Console.WriteLine("\n=== Example 5: Radius Search by Member ===\n");

    // Find all US cities within 2000km of Chicago
    Console.WriteLine("Cities within 2000km of Chicago:");
    var nearChicago = await db.GeoRadiusByMemberAsync(
        "cities",
        "Chicago",
        2000,
        GeoUnit.Kilometers,
        withDistance: true,
        withCoordinates: true);

    foreach (var result in nearChicago.OrderBy(r => r.Distance))
    {
        var coords = result.Position.HasValue
            ? $"({result.Position.Value.Latitude:F2}, {result.Position.Value.Longitude:F2})"
            : "";
        Console.WriteLine($"  {result.Member,-20} {result.Distance:F2} km {coords}");
    }

    // ===== EXAMPLE 6: POLYGON SEARCH (Valkey 9.0+) =====
    Console.WriteLine("\n=== Example 6: Polygon Search (Valkey 9.0 Feature) ===\n");

    // Define a polygon covering the West Coast (approximate)
    var westCoastPolygon = new[]
    {
        new GeoPosition(-125.0, 49.0),  // Northwest (near Canadian border)
        new GeoPosition(-114.0, 49.0),  // Northeast
        new GeoPosition(-114.0, 32.0),  // Southeast (near Mexican border)
        new GeoPosition(-125.0, 32.0),  // Southwest
        new GeoPosition(-125.0, 49.0)   // Close the polygon
    };

    Console.WriteLine("West Coast polygon vertices:");
    foreach (var vertex in westCoastPolygon)
    {
        Console.WriteLine($"  ({vertex.Latitude:F2}, {vertex.Longitude:F2})");
    }

    Console.WriteLine("\nCities within West Coast polygon:");
    var westCoastCities = await db.GeoSearchByPolygonAsync(
        "cities",
        westCoastPolygon,
        withDistance: true,
        withCoordinates: true);

    foreach (var result in westCoastCities.OrderBy(r => r.Member))
    {
        var coords = result.Position.HasValue
            ? $"at ({result.Position.Value.Latitude:F2}, {result.Position.Value.Longitude:F2})"
            : "";
        Console.WriteLine($"  {result.Member,-20} {coords}");
    }

    // ===== EXAMPLE 7: FINDING NEAREST NEIGHBORS =====
    Console.WriteLine("\n=== Example 7: Finding Nearest Neighbors ===\n");

    // Find the 5 nearest cities to Austin
    Console.WriteLine("5 nearest cities to Austin:");
    var nearestToAustin = await db.GeoRadiusByMemberAsync(
        "cities",
        "Austin",
        5000,
        GeoUnit.Kilometers,
        count: 6, // Get 6 to include Austin itself
        withDistance: true);

    foreach (var result in nearestToAustin.OrderBy(r => r.Distance).Skip(1).Take(5))
    {
        Console.WriteLine($"  {result.Member,-20} {result.Distance:F2} km away");
    }

    // ===== EXAMPLE 8: RESTAURANT LOCATOR DEMO =====
    Console.WriteLine("\n=== Example 8: Restaurant Locator Demo ===\n");

    Console.WriteLine("Adding restaurants in San Francisco...");
    var restaurants = new[]
    {
        ("Ferry Building", -122.3937, 37.7955),
        ("Fisherman's Wharf", -122.4177, 37.8080),
        ("Mission District Tacos", -122.4194, 37.7599),
        ("Chinatown Restaurant", -122.4058, 37.7955),
        ("Golden Gate Park Cafe", -122.4862, 37.7694),
        ("North Beach Pizza", -122.4102, 37.8006),
        ("Castro Bistro", -122.4350, 37.7609)
    };

    foreach (var (name, lon, lat) in restaurants)
    {
        await db.GeoAddAsync("sf:restaurants", lon, lat, name);
        Console.WriteLine($"  ✓ {name}");
    }

    // User is at Union Square
    var userLon = -122.4074;
    var userLat = 37.7879;

    Console.WriteLine($"\nUser location: Union Square ({userLat:F4}, {userLon:F4})");
    Console.WriteLine("\nRestaurants within 2km:");

    var nearbyRestaurants = await db.GeoRadiusAsync(
        "sf:restaurants",
        userLon,
        userLat,
        2,
        GeoUnit.Kilometers,
        withDistance: true);

    foreach (var result in nearbyRestaurants.OrderBy(r => r.Distance))
    {
        var walkingMinutes = (int)(result.Distance!.Value * 12); // ~12 min per km walking
        Console.WriteLine($"  {result.Member,-30} {result.Distance:F2} km (~{walkingMinutes} min walk)");
    }

    // ===== EXAMPLE 9: REAL-TIME DELIVERY TRACKING =====
    Console.WriteLine("\n=== Example 9: Real-Time Delivery Tracking Demo ===\n");

    Console.WriteLine("Simulating delivery driver locations...");
    var drivers = new[]
    {
        ("Driver-101", -122.4100, 37.7900, "Available"),
        ("Driver-102", -122.4200, 37.7800, "On Delivery"),
        ("Driver-103", -122.4000, 37.7950, "Available"),
        ("Driver-104", -122.4300, 37.7700, "Available")
    };

    foreach (var (name, lon, lat, status) in drivers)
    {
        await db.GeoAddAsync("sf:drivers", lon, lat, name);
        await db.StringSetAsync($"driver:{name}:status", status);
        Console.WriteLine($"  ✓ {name} at ({lat:F4}, {lon:F4}) - {status}");
    }

    // New order comes in at a location
    var orderLon = -122.4050;
    var orderLat = 37.7920;
    Console.WriteLine($"\n📦 New order at ({orderLat:F4}, {orderLon:F4})");

    // Find nearest available drivers within 3km
    Console.WriteLine("\nFinding nearest available drivers...");
    var nearbyDrivers = await db.GeoRadiusAsync(
        "sf:drivers",
        orderLon,
        orderLat,
        3,
        GeoUnit.Kilometers,
        withDistance: true);

    Console.WriteLine("\nDrivers sorted by distance:");
    foreach (var result in nearbyDrivers.OrderBy(r => r.Distance))
    {
        var status = await db.StringGetAsync($"driver:{result.Member}:status");
        var etaMinutes = (int)(result.Distance!.Value * 3); // ~3 min per km driving
        Console.WriteLine($"  {result.Member,-15} {result.Distance:F2} km (ETA: {etaMinutes} min) - Status: {status}");
    }

    // ===== CLEANUP =====
    Console.WriteLine("\n=== Cleanup ===\n");
    await db.KeyDeleteAsync(new[] { "cities", "sf:restaurants", "sf:drivers" });
    foreach (var (name, _, _, _) in drivers)
    {
        await db.KeyDeleteAsync($"driver:{name}:status");
    }
    Console.WriteLine("✓ Cleaned up demo data");

    Console.WriteLine("\n✅ All geospatial demos completed successfully!");
    Console.WriteLine("\n📚 Key Features Demonstrated:");
    Console.WriteLine("   • GEOADD - Adding locations with coordinates");
    Console.WriteLine("   • GEOPOS - Retrieving coordinates");
    Console.WriteLine("   • GEODIST - Calculating distances between locations");
    Console.WriteLine("   • GEOHASH - Getting geohash representations");
    Console.WriteLine("   • GEORADIUS - Finding locations within a radius");
    Console.WriteLine("   • GEORADIUSBYMEMBER - Finding nearby locations from a member");
    Console.WriteLine("   • GEOSEARCH BYPOLYGON - Polygon-based queries (Valkey 9.0+)");
    Console.WriteLine("\n💡 Use Cases:");
    Console.WriteLine("   • Store locator / Nearest location finder");
    Console.WriteLine("   • Restaurant discovery & delivery");
    Console.WriteLine("   • Real-time fleet/driver tracking");
    Console.WriteLine("   • Geographic market analysis");
    Console.WriteLine("   • Location-based gaming");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Error: {ex.Message}");
    Console.WriteLine($"   {ex.GetType().Name}");

    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }

    Console.WriteLine("\n💡 Make sure Valkey 9.0+ is running on localhost:6379");
    Console.WriteLine("   You can start it with: docker run -d -p 6379:6379 valkey/valkey:9");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
