# Profile Guide

English | [日本語](profile_guide.md)

How to author, apply, and use **vehicle profiles** in OsmDotRoute.
For the basics ("what is a vehicle profile", "the list of built-in profiles and how to select one"),
see [Usage guide §6](usage_guide.en.md#6-specifying-a-profile). This guide covers what comes next:

1. How to author a new profile
2. Baking a `.odrg` with the new profile
3. Routing with the new profile
4. **Notes on using a new profile against an existing, un-baked `.odrg`** (most important)

> A profile defines "which roads are passable", "speed", and "reaction to difficulties" as JSON.
> There are 7 built-in profiles: `car` / `pedestrian` / `bicycle` / `truck` / `ambulance` / `fire_engine` / `disaster`.

---

## 0. Prerequisite: when is a profile evaluated?

Understanding when profile values affect routing makes the notes in §4 click.

```text
[extraction = bake]                       [runtime = route finding]
  Profiles/*.json (built-in) ─┐
  user-defined JSON ──────────┤→ pre-evaluate every edge × every profile
  extractor --profiles ───────┘    ↓
                              bake (speedKmh, flags) into the .odrg
                              ※ the OSM tag dictionary is NOT stored in the .odrg
                                                    ↓
                                  Router.Calculate(profile, ...)
                                    → resolve slot by profile name → read baked values directly (no re-evaluation)
```

The key point: **OSM tags are interpreted once at bake time, and only the result is kept in the `.odrg`.**
At runtime the engine just looks up the baked values by profile name; it does not re-evaluate the JSON
(only the difficulty reaction is read from JSON at runtime — see §4).

---

## 1. How to author a new profile

A profile is a single JSON file. The fastest path is to copy a built-in like `car.json` and edit it.

### 1.1 Full JSON schema

| Field | Required | Type | Meaning |
| --- | --- | --- | --- |
| `name` | ✓ | string | Profile name. **The key for routing and slot resolution.** Must be unique within a `.odrg` |
| `vehicleType` | | string | Vehicle class (`motor_vehicle`, etc.), used in access-hierarchy interpretation |
| `ignoreOneway` | | bool | Ignore one-way restrictions (`true` for pedestrians and emergency vehicles). Default `false` |
| `speedMultiplier` | | number | Multiplier applied to all speeds (e.g. `0.75` if actual avg ≒ legal × 0.75) |
| `accessTagKeys` | | string[] | access-related tag keys to evaluate. **Later entries take precedence** (e.g. `["access","vehicle","motor_vehicle","emergency"]`) |
| `highway` | ✓ | object | Per `highway=*`: `speedKmh` (speed) and `access` (`yes`/`no`) |
| `accessValueMap` | | object | Mapping from access tag values to `allow` / `deny` |
| `maxspeedTagKey` | | string | Tag key for speed limit (usually `maxspeed`) |
| `maxspeedUnitDefault` | | string | Default unit when maxspeed omits one (`kmh`) |
| `fallback` | | object | Default `{ speedKmh, access }` for highway types not in `highway` |
| `speedBounds` | | object | Lower/upper speed clamp `{ minKmh, maxKmh }` |
| `vehicleLimits` | | object | Blocks edges exceeding `maxWeightTon` / `maxHeightMeter` / `maxWidthMeter` |
| `difficulty` | | object | Per difficulty type: `{ speedFactor, canPass }`. **Evaluated at runtime** (§4) |
| `difficultyDefault` | | object | Default for difficulty types not listed in `difficulty` |

Entries of `highway` / `difficulty`:

| Key | Meaning |
| --- | --- |
| `speedKmh` | Base speed (km/h) for that type |
| `access` | `yes` (passable) / `no` (blocked) |
| `speedFactor` | Speed factor in a difficulty area (0.0–1.0; 0.5 = half speed) |
| `canPass` | Whether the difficulty can be traversed. `false` excludes the edge from routing |

### 1.2 Letting more roads through — avoiding hard-deny

Passability is decided at bake time by **AND-ing `highway[type].access` with the access-tag evaluation**.
To let an emergency vehicle onto roads a car cannot use (e.g. footways), you must satisfy both:

1. **Set `access: "yes"` on the `highway` side.**
   Open `footway` / `path` / `pedestrian` as `{ "speedKmh": 10, "access": "yes" }`.
   Setting this to `"no"` (a hard-deny) blocks the road no matter how permissive the access tags are.
2. **Tilt the access-tag evaluation toward allow.**
   Add `emergency` to `accessTagKeys` and map `emergency=yes/designated` to `allow`.
   Even if `accessValueMap` maps `private` to `deny`, a later-precedence `emergency` key that matches makes it passable.

> **Mind the vehicle width**: opening `footway` for a large vehicle with `vehicleLimits.maxWidthMeter`
> (e.g. a fire engine) can route it down physically impassable alleys. In that case set the
> `footway` `speedKmh` very low (e.g. 5) to **effectively avoid** it, or revert `access` to `"no"`.

### 1.3 vehicleLimits (size/weight limits)

With `vehicleLimits`, edges whose OSM `maxweight` / `maxheight` / `maxwidth` tags fall below the
vehicle specs are blocked at bake time. Built-in emergency/disaster values:

| Profile | maxWeightTon | maxHeightMeter | maxWidthMeter | Reference vehicle |
| --- | --- | --- | --- | --- |
| `ambulance` | 4.0 | 2.6 | 2.0 | High-grade ambulance |
| `fire_engine` | 8.0 | 2.9 | 2.1 | Water-tank fire pump truck |
| `disaster` | 20.0 | 3.8 | 2.5 | Same as `truck` (emergency vehicles, heavy machinery) |

### 1.4 difficulty (difficulty tolerance)

`difficulty` defines how an edge reacts when it falls inside a difficulty area registered via the
dynamic restriction API (`RestrictedAreaService.AddDifficultyArea`). **This is the only part read from
JSON at runtime** (see §4). Built-in difficulty types: `flooding` / `liquefaction` / `landslide` /
`construction` / `obstacle` / `congestion` / `snow` / `ice`.

- `canPass: false` → the edge is fully excluded from routing (e.g. all profiles set `landslide` to physically impassable)
- `speedFactor` → passable but slowed (smaller = more strongly avoided)

The emergency/disaster profiles set higher tolerance than `car` (smaller numbers = avoid more).

### 1.5 Design examples: the 3 built-in profiles

The design intent of the three profiles added in Phase 4 (full JSON in
`src/OsmDotRoute/Profiles/{ambulance,fire_engine,disaster}.json`):

| Aspect | `ambulance` | `fire_engine` | `disaster` |
| --- | --- | --- | --- |
| Base | `car`-like | `truck`-like | `truck`-like |
| `accessTagKeys` | `…,emergency` | `…,hgv,emergency` | `…,hgv,emergency` |
| `ignoreOneway` | **`true`** (reverse OK) | **`true`** (reverse OK) | `false` (restrictions are the upper layer's job) |
| footway/path | `access: yes` (10 km/h) | `access: yes` (5 km/h, crawl) | `access: no` |
| Size (vehicleLimits) | small 4.0t/2.6m/2.0m | large 8.0t/2.9m/2.1m | truck-equiv 20t/3.8m/2.5m |
| Difficulty tolerance | higher than car | more modest than ambulance (large) | **strongest** (flooding/obstacle, etc.) |
| `landslide` | `canPass: false` | `canPass: false` | `canPass: false` |

Background on the decisions:

- **Splitting ambulance/fire engine into separate profiles**: their sizes and reachable roads differ a lot
  (ambulances are small enough for realistic footway entry; fire engines are large).
- **`ignoreOneway`**: reflecting emergency-driving exemptions (Japanese road traffic law), ambulance/fire
  engine may go the wrong way on one-way streets. The disaster profile leaves it `false` because "which
  segments become emergency routes" is controlled dynamically by the upper layer via `RestrictedArea`.
- **`landslide` is `canPass: false` for all**: even emergency/disaster vehicles cannot physically cross a landslide.

---

## 2. Baking a `.odrg` with the new profile

**Key prerequisite**: profiles are baked at `.odrg` **extraction time**. Any profile you want to use at
runtime must be included in `--profiles` at extraction (the reason is in §4).

### 2.1 Baking built-in profiles

The 7 built-ins are specified by name:

```powershell
dotnet run --project src/OsmDotRoute.Extractor -- `
  extract `
  --input  D:\osm\chubu-latest.osm.pbf `
  --output D:\odrg\tokyo.odrg `
  --bbox   139.74,35.65,139.79,35.70 `
  --profiles car,ambulance,fire_engine,disaster
```

### 2.2 Baking a user-defined JSON (added in Phase 4)

`--profiles` accepts **a mix of built-in names and paths to external JSON files**.
Paths may be absolute or relative (any existing `.json` file is loaded as user-defined):

```powershell
dotnet run --project src/OsmDotRoute.Extractor -- `
  extract `
  --input  D:\osm\chubu-latest.osm.pbf `
  --output D:\odrg\tokyo.odrg `
  --bbox   139.74,35.65,139.79,35.70 `
  --profiles car,ambulance,.\profiles\my_delivery.json
```

- The external JSON's `name` field becomes the **slot name** in the `.odrg`. That same `name` is the
  runtime lookup key, so use a unique name that does not collide (reusing a built-in `name` invites overwrite/collision).
- If the JSON is invalid, the CLI exits with an error (reporting the load failure).
- After extraction, verify the baked names with `RouterDb.GetProfileNames()` (§4.3).

---

## 3. Routing with the new profile

Load the `.odrg` and call `Router.Calculate` with a baked profile.

### 3.1 Built-in profiles

Built-ins are obtained via static properties of `VehicleProfile`:

```csharp
using OsmDotRoute;

var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");
var router = new Router(routerDb);

var from = new GeoCoordinate(35.68040208522669, 139.769056008911); // (latitude, longitude)
var to   = new GeoCoordinate(35.659, 139.700);

// Built-in profile (VehicleProfile.Ambulance / FireEngine / Disaster / Car ...)
var route = router.Calculate(VehicleProfile.Ambulance, from, to);

Console.WriteLine(route is null
    ? "no route"
    : $"distance {route.TotalDistanceM:F0} m / duration {route.TotalDurationSec:F0} s");
```

| Property | Profile name (in `.odrg`) |
| --- | --- |
| `VehicleProfile.Car` | `car` |
| `VehicleProfile.Pedestrian` | `pedestrian` |
| `VehicleProfile.Bicycle` | `bicycle` |
| `VehicleProfile.Truck` | `truck` |
| `VehicleProfile.Ambulance` | `ambulance` |
| `VehicleProfile.FireEngine` | `fire_engine` |
| `VehicleProfile.Disaster` | `disaster` |

### 3.2 User-defined profiles

Load external JSON with `LoadFromJsonFile` etc. As long as **the loaded profile's `Name` matches a
name baked into the `.odrg`**, routing works:

```csharp
var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");
var router = new Router(routerDb);

// Load the same JSON that was baked at extraction (Name must match)
var custom = VehicleProfile.LoadFromJsonFile(@"D:\profiles\my_delivery.json");

var route = router.Calculate(custom, from, to);
```

> At runtime the profile JSON is actually used only for `difficulty` (difficulty tolerance) evaluation.
> Passability and speed come from the baked values (the JSON at bake time), so **editing the JSON alone
> is not enough to change routing results — you must re-bake** (§4).

---

## 4. Notes on using a new profile against an existing, un-baked `.odrg` (most important)

**Bottom line: to use a new profile, you must regenerate (re-bake) the `.odrg` with that profile included.**
You cannot "add" a profile to an existing `.odrg` after the fact. The reasons and behavior:

### 4.1 Why you cannot add it afterward

The `.odrg` **does not retain the OSM tag dictionary**. At bake time each edge's
`(speedKmh, flags{CanPass, Forward, Backward})` is pre-computed per profile, and only that result is stored.
Since the tags are gone, it is fundamentally impossible to apply a new profile at runtime and re-evaluate.

### 4.2 Behavior when routing with an un-baked profile (caution)

Calling `Router.Calculate` with an un-baked profile returns **`null` (no route), not an exception**.

- During snapping, the profile name has no slot, so the source/target snap returns `null`
  (`NativeRoadSnapper`'s `HasProfile` check → `Router.Calculate` returns `null` early).
- Therefore **you cannot tell "there is genuinely no route" from "the profile is un-baked" by the return value alone.**
  This is the biggest pitfall.
- Only when low-level graph APIs (`CanPass` / edge evaluation) are called directly is an
  `InvalidOperationException` thrown ("profile '…' does not exist in the .odrg's BAKED_PROFILE").
  The normal `Router.Calculate` path never reaches there and silently returns `null`.

### 4.3 Countermeasure: check the baked profile names up front

To distinguish the cause of a `null`, check the names baked into the `.odrg` before routing, via
`RouterDb.GetProfileNames()` (public API):

```csharp
var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");

IReadOnlyList<string> baked = routerDb.GetProfileNames();
// e.g. ["car", "ambulance", "fire_engine"]

if (!baked.Contains(profile.Name))
{
    Console.WriteLine(
        $"Profile '{profile.Name}' is not baked into this .odrg. " +
        $"Available: {string.Join(", ", baked)}. Re-extract with it included in --profiles.");
    return;
}

var route = router.Calculate(profile, from, to);
```

### 4.4 Re-bake procedure

1. Re-create the `.odrg` with the desired profile (built-in name / external JSON) in `--profiles` (§2).
2. Replace the existing `.odrg` with the new one.
3. Route at runtime using the same `name` (§3).

### 4.5 Light changes you can make without re-baking (tuning difficulty tolerance)

There is **one exception** to "re-bake required" in §4.1–4.4.
**Changing only the difficulty tolerance (`speedFactor` / `canPass` in the `difficulty` section) takes effect without a re-bake.**

The reason: only this value is **evaluated from the live profile JSON at runtime**.
The slowdown / passability of an edge that intersects a difficulty area (registered via
`RestrictedAreaService.AddDifficultyArea` etc.) is evaluated each time from the profile passed to
`Router.Calculate` (internally `EdgeWeightCalculator` calls the profile's difficulty evaluation).
Snapping, passability, and base speed, by contrast, come from the baked values (§4.1).

So you can keep the **existing `.odrg` as-is** and load a JSON with only the difficulty reaction tuned:

```csharp
var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");  // no regeneration
var router = new Router(routerDb);

// Reuse the ambulance already baked into the .odrg, swapping in a JSON with only difficulty tuned
// (keep name = "ambulance")
var tuned = VehicleProfile.LoadFromJsonFile(@"D:\profiles\ambulance_tuned.json");

var restrictions = new RestrictedAreaService();
restrictions.AddDifficultyArea(new MeshCode(53394611), DifficultyTypes.Flooding, tag: "flood");
var router2 = new Router(routerDb, restrictions);

var route = router2.Calculate(tuned, from, to);  // the tuned flooding speedFactor applies
```

#### Rules to observe for this usage

1. **Never change `name` (most important).**
   The runtime resolves the baked slot by the profile's `name`. Changing it means the slot is not found
   and snapping returns `null` (indistinguishable from "no route", §4.2). Edit only the difficulty values
   and keep `name` identical to bake time.

2. **Only `difficulty` / `difficultyDefault` changes take effect.**
   Editing any other field (`highway` `access` / `speedKmh`, `vehicleLimits`, `accessTagKeys`,
   `accessValueMap`, `ignoreOneway`, `speedMultiplier`, `speedBounds`, `fallback`) has **no effect at
   runtime** — the baked values are used. The behavior will not change even though the JSON looks edited,
   producing a **silent mismatch between the JSON and actual behavior**. To change those, a re-bake (§4.4) is required.

3. **Difficulty reactions apply only when a difficulty area is registered.**
   `speedFactor` / `canPass` apply only when an area of the matching difficulty type is registered in
   `RestrictedAreaService` and an edge intersects it. With no difficulty area, the base speed (baked) is used.

4. **Adding a new difficulty type (key) also works at runtime.**
   Add a new key to `difficulty` and register a matching difficulty area, and it reacts without a re-bake.
   Difficulty types not defined in the profile fall back to `difficultyDefault` (`speedFactor=1.0` / `canPass=true`).

> **Summary**: "fine-tune the reaction to a difficulty" = no re-bake (edit the JSON `difficulty`, reload under the same name).
> "change passable roads / speed / size limits / one-way reversal" = **re-bake required** (§4.4).

---

## Related documents

- [Usage guide](usage_guide.en.md) — practical guide to a first run (profile selection basics in §6)
- [Requirement definition](requirement_definition.md)
- [.odrg binary format spec](phase2_graph_format_spec.md)
</content>
