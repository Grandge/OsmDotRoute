# OsmDotRoute vs. Itinero — Comparison & Selection Guide

English | [日本語](comparison_with_itinero.md)

OsmDotRoute is not a drop-in replacement for [Itinero](https://github.com/itinero/routing); it is a
**.NET routing library specialized for dynamic restrictions**.
This document provides as fair a comparison as possible to help you decide whether OsmDotRoute or
Itinero fits your use case.

> **Intended audience**: those considering a .NET / OSM routing solution, and existing Itinero users.
>
> **Source of performance numbers**: measured values from OsmDotRoute's bundled benchmarks
> (`tests/OsmDotRoute.Benchmarks`). The measurement environment is Intel Core i7-1165G7 / Windows 11 /
> .NET 9. Targets are Tsushima City, Aichi (city scale) and Aichi / Tokyo prefectures (prefecture scale)
> OSM data. Itinero's numbers were measured on the same machine with the same RouterDb.
> **No estimated values are used.**

---

## 1. Summary (TL;DR)

- **Static routing / global scale / proven track record / Isochrone & matrix → Itinero**
- **Travel restrictions changing at runtime / large-volume route finding (multi-agent) / Japan-local / System.* only → OsmDotRoute**

OsmDotRoute's core is "**adding/removing no-entry and difficult-to-traverse areas during a running
simulation and reflecting them in the next route calculation without a rebuild.**"
Itinero can also *express* such areas via edge tagging and profile factor/speed evaluation, but that
evaluation is derived from static edge attributes and cached per profile (`ProfileFactorAndSpeedCache`).
So switching target areas frequently at runtime is not the focus of Itinero's design. OsmDotRoute puts
this "runtime add/remove on demand" on its hot path.
Conversely, Itinero has features OsmDotRoute lacks, such as CH (Contraction Hierarchies) and
many-to-many computation.

---

## 2. Difference in design philosophy

| | Itinero | OsmDotRoute |
| --- | --- | --- |
| Positioning | General-purpose OSM routing | Simulation-oriented routing specialized for dynamic restrictions |
| Profiles | Broadly extensible via Lua / Native | JSON-externalized (tunable without rebuild) |
| Restrictions | Expressed via tags + profiles (frequent runtime changes are not its strength) | Add/remove areas at runtime, reflected in the next calculation |
| Scope | Worldwide OSM, diverse algorithms | Japan-local focus, concentrated on Dijkstra |

Itinero has the breadth to serve "any OSM routing in the world."
OsmDotRoute narrows its breadth and concentrates its design resources on the **dynamic-restriction hot path**.

---

## 3. Difference in data structures

| Item | Itinero RouterDb (`.routerdb`) | OsmDotRoute (`.odrg`) |
| --- | --- | --- |
| Edge model | Aggregates OSM tags into an `edge_profile` (edges reference a shared profile) | Bakes profile evaluation results per edge |
| Spatial index | Vertex-based | **Bakes an edge STR R-tree (M=16)**; dynamic-restriction intersection is O(log E) |
| Edge AABB | Computed from the shape on demand | **Bakes double × 4**; O(1) retrieval by array index |
| Edge attributes | Via profile / restriction | **14-bit flags** (bridge, tunnel, elevated, toll, private, one-way, etc. baked) |
| Edge geometry | Copies tend to occur | **Contiguous buffer + `ReadOnlySpan<GeoCoordinate>`** (zero-allocation) |
| File access | Reads the whole file | **`MemoryMappedFile` + `ReadOnlySpan<T>`** (zero-copy, minimal resident memory) |
| Input | OSM PBF / intermediate RouterDb, etc. | **Direct OSM PBF extraction only** (to the runtime via a `.odrg`) |
| Turn restrictions | Supported | Not supported (format reserved only, Phase 4+) |

OsmDotRoute's `.odrg` is designed as a "**data foundation for evaluating dynamic restrictions fast.**"
By pre-baking the edge R-tree and AABBs, it finds intersections between polygon/mesh restrictions and
edges in O(log E), and caches the result as "restriction ID → set of intersecting edges."
During Dijkstra edge expansion, a single HashSet lookup (O(1)) suffices.

---

## 4. Difference in algorithms

| | Itinero | OsmDotRoute (current) |
| --- | --- | --- |
| Search | Dijkstra / A\* / **CH** / bidirectional / many-to-many / Isochrone / matrix | **Dijkstra only** |

OsmDotRoute currently offers Dijkstra only. A\* / bidirectional / CH are future work (Phase 4+).

The key point here is that **"dynamic add/remove" and "CH precomputation" are a poor match.**
CH speeds things up by precomputation, but recomputation cost arises every time a restriction changes,
so for hot-reload-style use cases CH can become a liability instead.
Instead of having CH, OsmDotRoute adopts a design that does not degrade even under 100 restrictions
(see below).

---

## 5. Difference in performance characteristics (based on Tsushima City measurements)

> **Important caveat for comparison**:
> Itinero's numbers are values measured at OsmDotRoute's Phase 1 (same machine, **same RouterDb**,
> 43,685 vertices / 57,331 edges). The Itinero side uses **ordinary search without CH** (`car.fastest`);
> enabling CH could make Itinero faster. Meanwhile, OsmDotRoute's current (`.odrg`) numbers are from a
> different dataset (53,727 vertices / 74,276 edges, with a wider extraction area), so read them as
> **ratios / orders of magnitude**, not as a direct Mean comparison with Itinero.

### 5.1 Route calculation (no restrictions, Tsushima City, 100 pairs)

| Metric | Itinero (same RouterDb, no CH) | OsmDotRoute (same RouterDb, Phase 1) | OsmDotRoute (`.odrg`, current) |
| --- | ---: | ---: | ---: |
| Mean | 68.73 ms | 32.97 ms (**0.48x** of Itinero) | 7.70 ms |
| StdDev | 20.98 ms | 2.80 ms (**~1/7** of Itinero) | 0.14 ms |
| Allocated per route | 32.02 MB | 76.98 MB | **3.12 MB** |

- In a **Dijkstra-implementation comparison on the same RouterDb**, OsmDotRoute finishes in
  **0.48x the time** of Itinero. Without the Lua interpreter's execution jitter, its **StdDev is ~1/7**, i.e. more stable.
- **Allocation was initially higher than Itinero** (77 MB vs 32 MB), but with `.odrg` + `ReadOnlySpan`
  it dropped to **3.12 MB** (about 1/10 of Itinero's 32 MB). A former weakness reversed by the redesign.
- Route equivalence: across the 89 pairs where both returned a route, the average distance deviation was
  0.07%, all within ±10%. Moreover, OsmDotRoute found routes for 8 pairs Itinero could not
  (its snapping/search tolerance is wider).

### 5.2 Under 100 dynamic restrictions (OsmDotRoute only)

| | Itinero | OsmDotRoute (`.odrg`, current) |
| --- | --- | --- |
| Route calc under 100 restrictions | Direct comparison under the same conditions is not possible (see below) | **5.01 ms** (**0.65x** of no-restriction) |
| Restriction add/remove throughput | — | **~8,470 ops/sec** (118 μs per cycle) |

OsmDotRoute actually **gets faster (0.65x)** even under 100 restrictions. This is because the search
range of Dijkstra is pruned early around restriction areas, and restriction evaluation is compressed to
an O(1) lookup.

Itinero can also express passability/speed via edge tagging and profile settings, but that evaluation is
based on static edge attributes and cached per profile, so there is no standard way to add/remove the
same 100 areas at runtime and reflect them instantly. Therefore a direct comparison under the same
conditions as this benchmark is not possible, and we present the OsmDotRoute value alone here.

### 5.3 Loading and scale

| Metric | Itinero | OsmDotRoute (`.odrg`) |
| --- | --- | --- |
| Graph loading | RouterDb deserialize 115.4 ms (Tsushima scale) | MMF zero-copy. Even 153–179 MB prefecture data loads in **~0.2 s** |
| Prefecture-scale routing | (not measured) | Aichi 117 ms / Tokyo 288 ms (city scale is under 8 ms) |

At prefecture scale, Dijkstra-only exceeds 100 ms (Tokyo 288 ms).
This is an area where Itinero, with CH, has the advantage for large/high-frequency queries, and
OsmDotRoute treats it as material for deciding whether to introduce CH in Phase 4+.

### 5.4 Implications for large-volume / multi-agent use

OsmDotRoute is primarily intended for scenarios with large volumes of route finding, such as
**multi-agent simulations where many agents frequently recompute their own routes.**
In such scenarios, per-query speed, small jitter, and low allocation are what matter
(7.70 ms / StdDev 0.14 ms / 3.12 MB in Tsushima City).

Itinero's Lua profiles are strong in **high generality** — you can write tag-evaluation logic freely as a
script and flexibly support diverse rules worldwide. In exchange for that flexibility, script evaluation
(mitigated by the factor cache `ProfileFactorAndSpeedCache`, but still) tends to incur more runtime
overhead and jitter than native evaluation. In the same-data comparison at Phase 1, this appeared as a
~7x StdDev difference (OsmDotRoute 2.80 ms vs Itinero 20.98 ms).

OsmDotRoute narrows profile expressiveness to JSON + native evaluation, trading some generality for
**stable speed under large query volumes**. Which one suits you depends on the balance between the
flexibility and the throughput you need.

---

## 6. Supported profiles

| | Itinero | OsmDotRoute |
| --- | --- | --- |
| Built-in | Car / Pedestrian / Bicycle and many more (Lua) | Car / Pedestrian / **Bicycle** / **Truck (10t, based on Japanese road law)** |
| Extension | Lua / Native plugins | JSON-externalized (`VehicleProfile.LoadFromJsonFile`) |
| Regional fit | Mostly based on overseas conventions | Focused on Japan (Truck evaluates gross weight 20t / height 3.8m / width 2.5m) |

OsmDotRoute's Bicycle makes 99.5% of all edges passable (Car is 93.6%), consistent with Japanese cycling
rules (passable except on motorways and motor-vehicle-only roads).

---

## 7. Dependencies and runtime requirements

| | Itinero | OsmDotRoute |
| --- | --- | --- |
| Runtime dependencies | Itinero NuGet packages (multiple assemblies) | **System.* standard libraries only** (zero external NuGet, net9.0) |
| Distribution | Comes with multiple dependencies | Small. Security updates are covered just by following the .NET runtime |

In environments where external NuGet is restricted or you want to reduce the security-audit burden,
the System.*-only OsmDotRoute has the advantage.

---

## 8. License

| | Itinero | OsmDotRoute |
| --- | --- | --- |
| License | MIT | MIT |

Both are MIT, and combined use is fine.
Note that OSM data itself is under ODbL, so distribution requires the attribution
"© OpenStreetMap contributors."

---

## 9. Which to choose — a use-case guide

### Use cases that suit Itinero

- Fast, high-frequency routing over static OSM data (large queries where CH shines)
- Handling OSM from around the world with unified profiles
- Needing Isochrone / matrix / many-to-many computation
- Valuing years of operational track record
- A development setup that authors profiles in Lua / Native

### Use cases that suit OsmDotRoute

- **Disaster simulation** (dynamically add/remove flooding, earthquakes, road closures, reflected without recomputation)
- **Multi-agent simulation** (large query volume where many agents frequently recompute their own routes; per-query speed, stability, and low allocation matter)
- **Autonomous-driving / logistics simulation** (dynamically reflect time-based regulations, accident closures, construction zones)
- **Domestic Japanese truck delivery planning** (10t class, evaluating `hgv` / `maxweight` / `maxheight` / `maxwidth`)
- **Processing with frequent runtime restriction changes** (add/remove throughput matters)
- **Environments requiring System.* completeness** (external NuGet restrictions, reduced audit burden)
- **Wanting to keep resident memory low** (MMF zero-copy, switching among multiple `.odrg`s)

### Either is fine / evaluate carefully

- Simple point-to-point routing without dynamic restrictions → both work; Itinero leads on track record/feature coverage, OsmDotRoute on System.* completeness
- Ultra-large / high-frequency at prefecture scale and beyond → Itinero (CH) is advantageous; OsmDotRoute is in the >100 ms zone with Dijkstra only
- Bidirectional Dijkstra / CH / turn restrictions required → currently Itinero

---

## 10. Use cases OsmDotRoute is not suited for (stated honestly)

- **Centered on many-to-many matrix computation** → Itinero / OSRM have more complete features
- **Handling overseas OSM data** → the Truck / Bicycle profiles are based on Japanese road law and may not fit overseas. Car / Pedestrian are general-purpose but overseas track record is unverified
- **Large/high-frequency queries that assume CH and the like** → currently Dijkstra only
- **Requiring a long production track record** → OsmDotRoute is new; Itinero has years of track record

---

## 11. Future plans (Phase 4+)

Many of the points where OsmDotRoute currently falls short are candidates for future resolution.

- Speedup algorithms such as CH (Contraction Hierarchies) / bidirectional Dijkstra
- Turn restrictions (PBF Relation `type=restriction`)
- User-defined profile support in the extraction tool
- NuGet publishing, multi-platform (macOS / Linux) verification

---

## 12. Acknowledgments

In its early development (Phase 1), OsmDotRoute borrowed the graph structure from Itinero's
`RouterDb.Network` to validate the routing engine. It then migrated to the custom graph format `.odrg`
in Phase 2 / Phase 3 and fully removed the Itinero dependency from the runtime.
We pay our respects to Itinero — an excellent prior implementation — and to its author and community
(we only referenced it and did not copy its source code).
