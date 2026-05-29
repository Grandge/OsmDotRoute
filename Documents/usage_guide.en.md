# OsmDotRoute Usage Guide

English | [日本語](usage_guide.md)

This guide summarizes the whole flow from OSM data to a computed route.
It is not an API reference; it is a practical guide that gets you to a **first working route**
as quickly as possible.

Target version: Phase 3 (`.odrg` runtime, System.* only, net9.0).

---

## 1. Overview

OsmDotRoute loads a **custom binary `.odrg` that you extract from OSM data beforehand**,
and computes Dijkstra routes while adding/removing dynamic travel restrictions
(no-entry / difficult-to-traverse areas) at runtime.

Usage breaks into three stages.

```text
[1] Get a PBF          [2] Extract .odrg (CLI)        [3] Find routes (library)
  from Geofabrik etc. →  osmdotroute-extractor      →   RouterDb.LoadFromOdrg(...)
  obtain *.osm.pbf       extract a sub-area              new Router(db).Calculate(...)
```

- **[1] and [2] are offline prep** (run once; keep the `.odrg` as an artifact).
- **[3] is the runtime processing.** Once you have a `.odrg`, you need neither the PBF nor the
  Extractor, and the only dependency is System.*.
- Profiles (car, pedestrian, etc.) are **baked into the `.odrg` at extraction time [2]** and
  referenced by the **same name at runtime [3]**.

| Stage | What you use | Output |
| --- | --- | --- |
| [1] PBF prep | browser / wget etc. | `*.osm.pbf` |
| [2] .odrg extraction | `osmdotroute-extractor` (CLI) | `*.odrg` |
| [3] Routing | `OsmDotRoute` (library reference) | `Route` |

---

## 2. Getting and setting up the library

OsmDotRoute is published on GitHub. Before the NuGet release, use it via **source reference**
(runtime depends on System.* only).

### Requirements

- .NET 9 SDK or later
- (only if you want to run the `.odrg` in a browser static site) Node.js 18 or later

### Getting the repository

`git clone` (easiest to keep up to date):

```powershell
git clone https://github.com/Grandge/OsmDotRoute.git
cd OsmDotRoute
```

Or grab a ZIP from the GitHub page via "Code" -> "Download ZIP" and extract it.
For a specific version, download a tagged archive from "Releases."

### Build and verify

```powershell
dotnet build
dotnet test    # optional; confirms all tests pass
```

### Referencing it from your project

Add a project reference to the source you obtained:

```xml
<ProjectReference Include="path/to/OsmDotRoute/src/OsmDotRoute/OsmDotRoute.csproj" />
```

To use DI integration (§7.2), also add:

```xml
<ProjectReference Include="path/to/OsmDotRoute/src/OsmDotRoute.Extensions.DependencyInjection/OsmDotRoute.Extensions.DependencyInjection.csproj" />
```

The `osmdotroute-extractor` tool that generates `.odrg` lives in the same repository under
`src/OsmDotRoute.Extractor` (usage in §4).

---

## 3. Preparing a PBF

The source data for routing is the OSM PBF format (`*.osm.pbf`). For Japan, the per-prefecture
downloads from [Geofabrik](https://download.geofabrik.de/asia/japan.html) are convenient.

1. Download the `*.osm.pbf` for your target area from the Geofabrik Japan page
   (e.g. `chubu-latest.osm.pbf`, `kanto-latest.osm.pbf`).
2. The files are large (hundreds of MB per region). Since extraction clips to the area you need
   via a bbox, it is fine to **download a broad regional PBF**.

> **License**: OSM data is ODbL. If you distribute or publish a `.odrg`, the attribution
> "© OpenStreetMap contributors" is required.

### Choosing a bbox (extraction area)

The extraction area is a WGS84 lat/lon rectangle (bounding box). **The order is lon, then lat**:

```text
minLon,minLat,maxLon,maxLat
e.g. around Tokyo Station: 139.74,35.65,139.79,35.70
```

> **Note**: the bbox is in `Lon,Lat` order (longitude first). However, the library's coordinate type
> `GeoCoordinate(Latitude, Longitude)` is **latitude first**. It is easy to mix up the argument order.

---

## 4. Creating an .odrg

Use the `extract` subcommand of `osmdotroute-extractor` to produce a `.odrg` from a PBF.

### Command syntax

```text
osmdotroute-extractor extract \
  --input  <file.osm.pbf>            # -i  input PBF (required)
  --output <file.odrg>               # -o  output .odrg (required)
  --bbox   minLon,minLat,maxLon,maxLat   # extraction area WGS84 (required)
  --profiles car,pedestrian          # -p  profiles to bake (default: car,pedestrian)
```

### Running from source

Before the NuGet release, run it from within the repository via `dotnet run`
(in PowerShell, chain with `;`, not `&&`):

```powershell
dotnet run --project src/OsmDotRoute.Extractor -- `
  extract `
  --input  D:\osm\chubu-latest.osm.pbf `
  --output D:\odrg\tokyo.odrg `
  --bbox   139.74,35.65,139.79,35.70 `
  --profiles car,pedestrian,bicycle,truck
```

It echoes the input, area, and profiles, then proceeds to extract and write:

```text
input    : D:\osm\chubu-latest.osm.pbf
output   : D:\odrg\tokyo.odrg
bbox     : 139.74,35.65,139.79,35.70
profiles : car,pedestrian,bicycle,truck

Extracting...
Extraction done: 12,345 vertices / 23,456 edges (3.2 s)
Writing...
Write done: 1,234,567 bytes (0.4 s)
Output file: D:\odrg\tokyo.odrg
```

### Notes on specifying profiles

- Only the profiles you pass to `--profiles` are baked into the `.odrg`.
  **Route finding in [3] can only use the names baked here**
  (e.g. you cannot use `VehicleProfile.Truck` on a `.odrg` baked with only `car,pedestrian`).
- Currently `--profiles` accepts only the 4 built-in names (`car` / `pedestrian` / `bicycle` / `truck`).
  Passing an unsupported name exits with an error.
- To check the baked profile names at runtime, use `RouterDb.GetProfileNames()`.

---

## 5. Finding routes

Load a `.odrg` and compute a route between two points with `Router`.

```csharp
using OsmDotRoute;

// 1. Load the .odrg (zero-copy via MMF + Span; no Itinero or PBF needed)
var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");

// 2. Build a Router
var router = new Router(routerDb);

// 3. Specify origin and destination (GeoCoordinate is latitude, longitude order)
var route = router.Calculate(
    VehicleProfile.Car,
    new GeoCoordinate(35.681, 139.767),   // Tokyo Station
    new GeoCoordinate(35.658, 139.745));  // Shibuya Station

// 4. Result (null when no route is found or a coordinate is out of range)
if (route is null)
{
    Console.WriteLine("No route could be calculated.");
    return;
}

Console.WriteLine($"distance {route.TotalDistanceM:F0} m, duration {route.TotalDurationSec:F0} s");

// Route geometry (zero-allocation ReadOnlyMemory)
foreach (var p in route.Shape.Span)
{
    Console.WriteLine($"  {p.Latitude:F6}, {p.Longitude:F6}");
}
```

Main members of `Route`:

| Member | Type | Meaning |
| --- | --- | --- |
| `TotalDistanceM` | `double` | Total distance (meters) |
| `TotalDurationSec` | `double` | Total duration (seconds, based on profile speeds) |
| `Shape` | `ReadOnlyMemory<GeoCoordinate>` | Vertices of the route geometry |

Helper APIs:

```csharp
// Snap an arbitrary coordinate to the nearest road (null if out of range)
GeoCoordinate? snapped = router.SnapToRoad(VehicleProfile.Car, new GeoCoordinate(35.68, 139.76));

// Vertex/edge counts and lat/lon bounds
RouterDbStatistics stats = routerDb.GetStatistics();

// Profile names baked into the .odrg
IReadOnlyList<string> names = routerDb.GetProfileNames();
```

### Adding dynamic restrictions

If you pass a `RestrictedAreaService` to `Router`, registered no-entry / difficulty areas are
**reflected from the next `Calculate` call** (no rebuild). This is OsmDotRoute's main purpose.

```csharp
var restrictions = new RestrictedAreaService();
var router = new Router(routerDb, restrictions);

// Register a no-entry area by polygon
var polygon = new GeoPolygon(new[]
{
    new GeoCoordinate(35.68, 139.76),
    new GeoCoordinate(35.68, 139.78),
    new GeoCoordinate(35.66, 139.78),
    new GeoCoordinate(35.66, 139.76),
});
restrictions.AddBlockArea(polygon, tag: "incident-1");

// Register a difficulty area (flooding) by mesh code (JIS X0410)
restrictions.AddDifficultyArea(new MeshCode(53394611), DifficultyTypes.Flooding, tag: "typhoon-15");

var route1 = router.Calculate(VehicleProfile.Car, from, to);  // with restrictions

// Remove by tag -> no restrictions from the next calculation
restrictions.RemoveByTag("typhoon-15");
var route2 = router.Calculate(VehicleProfile.Car, from, to);
```

You can also bulk-register from KSJ (e.g. A31 flood inundation zones) GML files:

```csharp
var bounds = new MapBounds(
    new GeoCoordinate(35.65, 139.74),
    new GeoCoordinate(35.70, 139.79));
restrictions.AddDifficultyAreaFromGmlFile(
    @"D:\hazard\A31-12_24_GML\A31-12_24.xml",
    DifficultyTypes.Flooding,
    mapBounds: bounds,   // features outside the map bounds are excluded
    tag: "ksj-a31");
```

Built-in difficulty types (`DifficultyTypes`): `Flooding` / `Liquefaction` /
`Landslide` / `Construction` / `Obstacle` / `Congestion` / `Snow` / `Ice`.

---

## 6. Specifying a profile

A profile defines "which roads are passable," "speed," and "reaction to difficulties."
Profiles are externalized as JSON and can be tuned without rebuilding.

### Built-in profiles

```csharp
VehicleProfile.Car         // car
VehicleProfile.Pedestrian  // pedestrian
VehicleProfile.Bicycle     // bicycle (avg 15 km/h, prefers cycleway/path, blocks motorway/trunk)
VehicleProfile.Truck       // 10t truck (Japanese road law: gross 20t / height 3.8m / width 2.5m)
```

> Route finding can only use profiles baked into that `.odrg` (see §4).
> For example, to use Truck you must include `--profiles ...,truck` at extraction time.

### User-defined profiles (JSON)

You can load a profile from your own JSON:

```csharp
VehicleProfile custom = VehicleProfile.LoadFromJsonFile(@"D:\profiles\delivery.json");
// or
VehicleProfile custom2 = VehicleProfile.LoadFromJsonString(jsonText);
```

> **Current limitation**: the runtime can load arbitrary profiles, but only the 4 built-in profiles
> can be baked into a `.odrg` (the extraction CLI has fixed built-in names). So to route with a
> custom profile, its `Name` must match one already baked into the `.odrg`.
> Support for user-defined profiles in the extraction tool is a Phase 4+ TODO.

### Profile JSON schema

The main fields, using `car.json` as an example (naming is camelCase):

```json
{
  "name": "car",
  "vehicleType": "motor_vehicle",
  "ignoreOneway": false,
  "speedMultiplier": 0.75,
  "accessTagKeys": ["access", "vehicle", "motor_vehicle"],
  "highway": {
    "motorway":    { "speedKmh": 120, "access": "yes" },
    "residential": { "speedKmh": 50,  "access": "yes" },
    "footway":     { "speedKmh": 5,   "access": "no" }
  },
  "accessValueMap": { "yes": "allow", "private": "deny" },
  "maxspeedTagKey": "maxspeed",
  "maxspeedUnitDefault": "kmh",
  "fallback": { "speedKmh": 10, "access": "no" },
  "speedBounds": { "minKmh": 30, "maxKmh": 200 },
  "difficulty": {
    "flooding":  { "speedFactor": 0.3, "canPass": true  },
    "landslide": { "speedFactor": 0.0, "canPass": false }
  },
  "difficultyDefault": { "speedFactor": 1.0, "canPass": true }
}
```

| Field | Meaning |
| --- | --- |
| `name` | Profile name (the lookup key at route time) |
| `ignoreOneway` | Whether to ignore one-way restrictions (true for pedestrians) |
| `speedMultiplier` | Multiplier applied to all speeds (e.g. `0.75` if actual avg ≒ legal speed × 0.75) |
| `accessTagKeys` | access-related tag keys to evaluate; later entries take precedence |
| `highway` | Per `highway=*` speed (`speedKmh`) and passability (`access`: `yes`/`no`) |
| `accessValueMap` | Mapping from access tag values to `allow`/`deny` |
| `fallback` | Default when the highway type is unknown |
| `speedBounds` | Lower/upper speed clamp |
| `difficulty` | Per difficulty type: `speedFactor` (speed factor) and `canPass` (passable or not) |
| `difficultyDefault` | Default for undefined difficulty types |
| `vehicleLimits` | (Truck) blocks edges exceeding `maxWeightTon` / `maxHeightMeter` / `maxWidthMeter` |

---

## 7. Code examples

### 7.1 Minimal end-to-end (console)

```csharp
using OsmDotRoute;

var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");
var router = new Router(routerDb);

var route = router.Calculate(
    VehicleProfile.Car,
    new GeoCoordinate(35.681, 139.767),
    new GeoCoordinate(35.658, 139.745));

Console.WriteLine(route is null
    ? "no route"
    : $"distance {route.TotalDistanceM:F0} m / duration {route.TotalDurationSec:F0} s / vertices {route.Shape.Length}");
```

### 7.2 DI integration (ASP.NET Core, etc.)

```csharp
using Microsoft.Extensions.DependencyInjection;
using OsmDotRoute.Extensions.DependencyInjection;

services.AddOsmDotRoute(@"D:\odrg\tokyo.odrg");
// Router / RouterDb / RestrictedAreaService are registered as singletons.
// Sharing the RestrictedAreaService makes dynamic restriction changes apply to the next calculation.

var router = serviceProvider.GetRequiredService<Router>();
var restrictions = serviceProvider.GetRequiredService<RestrictedAreaService>();
```

### 7.3 Re-Route with dynamic restrictions

```csharp
var restrictions = new RestrictedAreaService();
var router = new Router(routerDb, restrictions);

var from = new GeoCoordinate(35.681, 139.767);
var to   = new GeoCoordinate(35.658, 139.745);

var before = router.Calculate(VehicleProfile.Car, from, to);

// Register a flooded area by mesh -> re-routing yields an avoidance route
restrictions.AddDifficultyArea(new MeshCode(53394611), DifficultyTypes.Flooding, tag: "flood");
var after = router.Calculate(VehicleProfile.Car, from, to);

Console.WriteLine($"normal {before?.TotalDistanceM:F0} m -> flood-avoided {after?.TotalDistanceM:F0} m");
```

---

## Related documents

- [README](../README.en.md) — project overview and quick start
- [`.odrg` binary format specification](phase2_graph_format_spec.en.md)
- [Comparison & Selection Guide vs. Itinero](comparison_with_itinero.en.md)
