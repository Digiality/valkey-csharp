# Geospatial Operations Demo

This sample demonstrates Valkey.NET's comprehensive geospatial capabilities for location-based applications.

## Features Demonstrated

### Core Geospatial Commands

1. **GEOADD** - Adding locations with longitude/latitude coordinates
2. **GEOPOS** - Retrieving coordinates of stored locations
3. **GEODIST** - Calculating distances between locations (meters, km, miles, feet)
4. **GEOHASH** - Getting geohash string representations
5. **GEORADIUS** - Finding locations within a radius of coordinates
6. **GEORADIUSBYMEMBER** - Finding locations within a radius of a stored member
7. **GEOSEARCH BYPOLYGON** - Polygon-based spatial queries (Valkey 9.0+)

### Real-World Examples

#### 1. City Database
- Store and query major world cities
- Calculate distances between cities
- Find cities within a region

#### 2. Restaurant Locator
- Store restaurant locations
- Find restaurants near user's location
- Calculate walking time estimates

#### 3. Delivery Tracking
- Track real-time driver locations
- Find nearest available drivers
- Calculate ETAs for deliveries

#### 4. Polygon Search (Valkey 9.0+)
- Define custom geographic regions
- Find all locations within a polygon
- Example: West Coast cities search

## Prerequisites

- .NET 9.0 or later
- Valkey 9.0 or later running on `localhost:6379`

## Running the Demo

### Start Valkey 9.0

```bash
docker run -d -p 6379:6379 valkey/valkey:9
```

### Run the Sample

```bash
cd samples/GeospatialDemo
dotnet run
```

## Key Concepts

### Coordinates
- Longitude: -180 to 180 (West to East)
- Latitude: -85.05112878 to 85.05112878 (South to North)
- Format: (longitude, latitude) - Note the order!

### Distance Units
- **Meters** (m) - Default
- **Kilometers** (km)
- **Miles** (mi)
- **Feet** (ft)

### Geohash
A hierarchical spatial data structure that subdivides space into buckets of grid shape. Geohashes are useful for:
- Proximity searches
- Data distribution
- Spatial indexing

### Polygon Search (Valkey 9.0+)
The BYPOLYGON feature allows querying locations within custom-shaped areas:
- Requires at least 3 vertices
- Polygon must be closed (first vertex = last vertex)
- More flexible than radius or bounding box searches

## Use Cases

### Store Locator
Find nearest retail locations to customers:
```csharp
var nearbyStores = await db.GeoRadiusAsync(
    "stores",
    userLon, userLat,
    10, // 10km radius
    GeoUnit.Kilometers,
    withDistance: true);
```

### Ride Sharing / Delivery
Match customers with nearest available drivers:
```csharp
var nearbyDrivers = await db.GeoRadiusByMemberAsync(
    "drivers",
    "customer-location",
    5,
    GeoUnit.Kilometers,
    count: 5, // Top 5 nearest
    withDistance: true);
```

### Real Estate / Market Analysis
Find all properties within a neighborhood polygon:
```csharp
var polygon = new[]
{
    new GeoPosition(-122.5, 37.8),
    new GeoPosition(-122.4, 37.8),
    new GeoPosition(-122.4, 37.7),
    new GeoPosition(-122.5, 37.7),
    new GeoPosition(-122.5, 37.8)
};

var properties = await db.GeoSearchByPolygonAsync(
    "properties",
    polygon,
    withCoordinates: true);
```

### Location-Based Gaming
Track player positions and find nearby players:
```csharp
// Add player location
await db.GeoAddAsync("players", playerLon, playerLat, playerId);

// Find nearby players
var nearby = await db.GeoRadiusByMemberAsync(
    "players",
    playerId,
    100, // 100 meters
    GeoUnit.Meters,
    withDistance: true);
```

## Performance Tips

1. **Batch Operations**: Use arrays when adding multiple locations
2. **Limit Results**: Use the `count` parameter to limit radius searches
3. **Choose Appropriate Units**: Use km for city-scale, meters for local searches
4. **Polygon Complexity**: Simpler polygons (fewer vertices) perform better
5. **Expiration**: Set TTL on temporary locations (e.g., active drivers)

## Output Example

```
=== Example 2: Calculating Distances Between Cities ===

San Francisco ↔ Los Angeles:
  559.12 km
  347.42 miles

New York ↔ Boston:
  306.52 km
  190.47 miles

=== Example 4: Radius Search ===

Cities within 500km of San Francisco:
  San Francisco        0.00 km
  Oakland              13.52 km
  San Jose             67.23 km
  Sacramento           121.45 km

=== Example 9: Real-Time Delivery Tracking Demo ===

📦 New order at (37.7920, -122.4050)

Drivers sorted by distance:
  Driver-103      0.31 km (ETA: 1 min) - Status: Available
  Driver-101      0.35 km (ETA: 1 min) - Status: Available
  Driver-102      1.82 km (ETA: 5 min) - Status: On Delivery
```

## Learn More

- [Valkey Geospatial Documentation](https://valkey.io/topics/geospatial/)
- [Valkey.NET Documentation](../../README.md)
- [GEOSEARCH Command Reference](https://valkey.io/commands/geosearch/)
