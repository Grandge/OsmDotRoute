# OsmDotRoute

English | [日本語](README.md)

A .NET-native OSM routing library. It provides Dijkstra-based path finding with
**dynamic travel restrictions** (no-entry / difficult-to-traverse areas).
A successor to Itinero 1.x, spun off from the parent project
"Disaster Waste Processing Simulation."

Its defining strength: it loads a pre-extracted custom binary `.odrg` at runtime and lets you
**add/remove travel restrictions during a running simulation and re-route instantly**.

> For the differences between OsmDotRoute and Itinero and which one fits your use case, see the
> [Comparison & Selection Guide](Documents/comparison_with_itinero.en.md).

## Features

- **.NET 9 / pure C#, runtime dependencies are System.* only** (zero external NuGet packages)
- **Custom `.odrg` graph format**: zero-copy reads via `MemoryMappedFile` + `ReadOnlySpan<T>`
- **Dynamic restrictions**: polygon, JIS X0410 mesh code, and KSJ GML (e.g. A31) input; reflected in the next calculation without a rebuild
- **JSON-externalized profiles**: tune access/speed/difficulty reactions without rebuilding
- **4 built-in profiles**: car / pedestrian / bicycle / truck (10t, based on Japanese road law)
- **8 built-in difficulty types**: flooding, liquefaction, landslide, construction, obstacle, congestion, snow, ice
- **MIT License**

## Quick start

```csharp
using OsmDotRoute;

// 1. Load a pre-extracted .odrg (no PBF or Itinero needed)
var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");
var router = new Router(routerDb);

// 2. Tokyo Station -> Shibuya Station with the Car profile (GeoCoordinate is latitude, longitude order)
var route = router.Calculate(
    VehicleProfile.Car,
    new GeoCoordinate(35.681, 139.767),
    new GeoCoordinate(35.658, 139.745));

Console.WriteLine(route is null
    ? "No route could be calculated."
    : $"distance {route.TotalDistanceM:F0} m, duration {route.TotalDurationSec:F0} s");
```

For everything from building a `.odrg` (preparing a PBF and extracting) to profiles and dynamic
restrictions, see the **[Usage Guide](Documents/usage_guide.en.md)**.

## Dynamic restriction example

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
restrictions.AddBlockArea(polygon, tag: "incident-2026-05-19");

// Register a difficulty area (flooding) by mesh code
restrictions.AddDifficultyArea(
    new MeshCode(53394611),
    DifficultyTypes.Flooding,
    tag: "typhoon-15");

// Bulk-register KSJ A31 (flood inundation zones) from GML (filtered by map bounds)
var bounds = new MapBounds(
    new GeoCoordinate(35.65, 139.74),
    new GeoCoordinate(35.70, 139.79));
restrictions.AddDifficultyAreaFromGmlFile(
    @"D:\hazard\A31-12_24_GML\A31-12_24.xml",
    DifficultyTypes.Flooding,
    mapBounds: bounds,
    tag: "ksj-a31");

// Bulk remove by tag -> reflected in the next calculation
restrictions.RemoveByTag("typhoon-15");
```

## DI integration

```csharp
using Microsoft.Extensions.DependencyInjection;
using OsmDotRoute.Extensions.DependencyInjection;

services.AddOsmDotRoute(@"D:\odrg\tokyo.odrg");

// Consumer side
var router = serviceProvider.GetRequiredService<Router>();
var restrictions = serviceProvider.GetRequiredService<RestrictedAreaService>();
```

`Router` / `RouterDb` / `RestrictedAreaService` are all registered as singletons.
By sharing the `RestrictedAreaService`, any dynamic restriction change made during a simulation
is reflected from the next `Router.Calculate` call (REQ-RST-012).

## Try-it demo (Sandbox)

A bundled demo (`samples/Sandbox`, local-only) lets you try the full flow in a browser UI:
PBF download -> bbox extraction -> routing -> mesh/polygon restriction -> Re-Route.

A static-site build that runs the core engine compiled to WebAssembly entirely in the browser
(no install) is also available:

```powershell
cd samples/Sandbox/Web ; npm run build:wasm
```

## Installation

NuGet packages are not yet published, so use it via **source reference** for now
(runtime depends on System.* only). Add a project reference:

```xml
<ProjectReference Include="path/to/OsmDotRoute/src/OsmDotRoute/OsmDotRoute.csproj" />
```

To use DI integration, also add:

```xml
<ProjectReference Include="path/to/OsmDotRoute/src/OsmDotRoute.Extensions.DependencyInjection/OsmDotRoute.Extensions.DependencyInjection.csproj" />
```

For the `osmdotroute-extractor` tool that generates `.odrg`, see
[Usage Guide §4](Documents/usage_guide.en.md#4-creating-an-odrg).

## Current phase

| Phase | Goal | Status |
| --- | --- | --- |
| Phase 0 | Requirements definition | Done (2026-05-18) |
| Phase 1 | Custom routing engine (Itinero kept as data layer) | Done |
| Phase 2 | Custom intermediate graph format `.odrg` | Done |
| Phase 3 | `.odrg` runtime, full Itinero removal, bicycle/truck, benchmarks, parent integration, demo, OSS release prep | **In progress (OSS release prep)** |

The Itinero dependency has been removed from the runtime (System.* only).

## Versioning policy

During the 0.x line, **breaking API changes are allowed on minor version bumps** (REQ-API-008).
Strict semantic versioning applies from the 1.0 release onward.

## Relationship with the parent project

The first customer is the parent project "Disaster Waste Processing Simulation."
However, OsmDotRoute is designed as a **general-purpose OSM routing library**, with the disaster
use case positioned as one application. We do not move or copy the parent project's code, data, or
documents into this repository (the dependency direction is one-way: parent -> this library).

## Contributing

Bug reports and pull requests are welcome. For build/test/PR instructions, see
[CONTRIBUTING.md](CONTRIBUTING.md) (Japanese).

## License

[MIT License](LICENSE) — Copyright (c) 2026 Grandge.
For third-party components and their licenses, see [LICENSE-THIRD-PARTY.md](LICENSE-THIRD-PARTY.md).

The OSM data itself is under ODbL. When distributing or publishing a `.odrg`, the attribution
"© OpenStreetMap contributors" is required.

## Documentation

- [Usage Guide](Documents/usage_guide.en.md) — PBF prep -> `.odrg` extraction -> routing -> profiles -> code examples
- [Comparison & Selection Guide vs. Itinero](Documents/comparison_with_itinero.en.md) — design philosophy, data structures, performance, and fit by use case
- [`.odrg` binary format specification](Documents/phase2_graph_format_spec.en.md)

> Detailed design and requirement documents (Phase 1–3 design, requirements definition) are
> currently available in Japanese only.
