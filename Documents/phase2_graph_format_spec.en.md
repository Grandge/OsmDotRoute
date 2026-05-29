# OsmDotRoute `.odrg` Custom Binary Graph Format Specification

English | [日本語](phase2_graph_format_spec.md)

**Version**: 0.2 (Step 1 completion finalized edition)
**Created**: 2026-05-20
**Last updated**: 2026-05-21
**Status**: v0.2 finalized (Phase 2 Step 1 complete, user-agreed)
**Scope**: Phase 2 Step 1 deliverable. Defines the layout, algorithms, and read/write API patterns of the OsmDotRoute custom binary graph format `.odrg`
**Related documents**:

- [Requirement definition](requirement_definition.md) (v2.3, REQ-MAP-003)
- [Phase 2 implementation plan](phase2_implementation_plan.md) (v0.2, §3.1 / §3.6)
- [Phase 2 design document](phase2_design.md) (v0.2, §3)
- [Phase 1 design document](phase1_design.md) (v0.21, §18 handover notes)

---

## 0. Purpose of this document and reading guide

### 0.1 Purpose

This document provides a **machine-readable specification of the OsmDotRoute custom binary graph format `.odrg`**. Both `OsmDotRoute.Extractor` (writer side, Phase 2 Step 3) and `NativeRoadGraph` / `NativeRoadSnapper` (reader side, Phase 3) conform to this specification.

### 0.2 Design principles (from Phase 2 plan §3.1 / §3.6)

| #  | Principle                                                                                                              | Origin                                                                              |
| -- | ---------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| P1 | **The data format itself directly supports the dynamic-restriction hot path**                                          | OsmDotRoute's biggest differentiator (internal note [[project-phase2-dynamic-restriction-design]]) |
| P2 | **Free design not constrained by the Itinero RouterDb structure**                                                      | User decision 2026-05-20, internal note [[project-phase2-scope-redefinition]]       |
| P3 | **Zero-copy reads via `MemoryMappedFile`** (exposed as `ReadOnlySpan<T>` / `ReadOnlyMemory<T>`)                         | plan §5.4-6                                                                          |
| P4 | **Edge shapes placed in a contiguous buffer**, eliminating the 77 MB/route allocation in Phase 3 (Phase 1 design §18.4) | plan §3.2                                                                            |
| P5 | **Bake edge AABBs / edge spatial index / edge flags** so the runtime can read O(1) or search O(log E)                  | plan §3.6                                                                            |
| P6 | **Adopt only attributes that can be mechanically baked from OSM tags** (aerial imagery, elevation data, and dynamically-updated items are excluded) | plan §3.6                                       |
| P7 | **Loosely chain via section offsets for future extensibility** (unknown sections can be skipped across versions)       | general principle                                                                    |
| P8 | **Fixed little-endian**, prioritizing processing performance on x64 Windows                                            | platform requirement REQ-NFR-006                                                     |

### 0.3 Update rules for this document

Promoted from v0.1 → v0.2 upon Step 1 completion (user-agreed, 2026-05-21). From here on, constraints and corrections discovered during implementation are appended as they arise; major promotion (v1.0) occurs only for changes that break compatibility.

---

## 1. Overall file structure

### 1.1 High-level structure

The `.odrg` file chains the following sections in an **offset-reference style**:

```text
+------------------------------------------+ offset 0
| HEADER                                   | fixed 256 bytes
+------------------------------------------+
| SECTION TABLE                            | variable length
| ((kind, offset, length) of each section) |
+------------------------------------------+
| SECTION: Vertex Table                    |
+------------------------------------------+
| SECTION: Edge Table                      |
+------------------------------------------+
| SECTION: Edge Shape Buffer               |
+------------------------------------------+
| SECTION: Edge AABB Table                 |
+------------------------------------------+
| SECTION: Edge Flag Table                 |
+------------------------------------------+
| SECTION: Edge Spatial Index (R-tree)     |
+------------------------------------------+
| SECTION: Baked Profile Table             |
+------------------------------------------+
| SECTION: Reserved - Turn Restrictions    | (used in Phase 4+, length=0 in Phase 2)
+------------------------------------------+
| SECTION: Metadata                        | UTF-8 JSON string
+------------------------------------------+ EOF
```

### 1.2 Purpose of the section table (P7)

So that unknown sections can be safely skipped, each section is expressed as "kind ID (uint16) + offset (uint64) + length (uint64)". The reader skips kinds it does not recognize.

### 1.3 Common endianness

**Little-endian** fixed. For all integers and floating-point values.

---

## 2. Header details (HEADER, fixed 256 bytes)

| Offset     | Size   | Field                  | Type      | Description                                                                 |
| ---------- | ------ | ---------------------- | --------- | --------------------------------------------------------------------------- |
| 0          | 8      | `magic`              | byte[8]   | ASCII `"ODRG\0\0\0\0"` (0x4F, 0x44, 0x52, 0x47, 0x00, 0x00, 0x00, 0x00) |
| 8          | 2      | `versionMajor`       | uint16    | Format major version (increased on compatibility break). Initial = 1       |
| 10         | 2      | `versionMinor`       | uint16    | Format minor version (backward-compatible extensions). Initial = 0, v0.3 = 1 (adds `bboxRequested*`) |
| 12         | 4      | `flags`              | uint32    | Reserved flags (initial = 0, bit0 = "compressed" (future), bit1–31 = reserved) |
| 16         | 8      | `vertexCount`        | uint64    | Number of vertices                                                          |
| 24         | 8      | `edgeCount`          | uint64    | Number of edges (**directed edge count**. A bidirectional road is 2 edges) |
| 32         | 8      | `bboxMinLon`         | double    | Overall bounding box minimum longitude                                      |
| 40         | 8      | `bboxMinLat`         | double    | Overall bounding box minimum latitude                                       |
| 48         | 8      | `bboxMaxLon`         | double    | Overall bounding box maximum longitude                                      |
| 56         | 8      | `bboxMaxLat`         | double    | Overall bounding box maximum latitude                                       |
| 64         | 4      | `profileCount`       | uint32    | Number of baked profiles                                                    |
| 68         | 4      | `edgeFlagBytes`      | uint32    | Bytes per edge for edge flags (1 or 2)                                      |
| 72         | 8      | `sectionTableOffset` | uint64    | Offset to the start of the section table (normally 256)                     |
| 80         | 4      | `sectionCount`       | uint32    | Number of entries in the section table                                      |
| 84         | 4      | `reservedA`          | uint32    | Reserved (fixed 0)                                                          |
| 88         | 8      | `bboxRequestedMinLon` | double   | **Minimum longitude of the bbox requested at extraction** (v0.3+, user input of CLI `--bbox`. Before way expansion. 0.0 for VersionMinor=0) |
| 96         | 8      | `bboxRequestedMinLat` | double   | Same, minimum latitude                                                     |
| 104        | 8      | `bboxRequestedMaxLon` | double   | Same, maximum longitude                                                    |
| 112        | 8      | `bboxRequestedMaxLat` | double   | Same, maximum latitude                                                     |
| 120        | 136    | `reservedB`          | byte[136] | Reserved (zero-filled). Room for future header extensions                  |

Total 256 bytes.

**Difference between bbox (offsets 32-63) and bboxRequested (88-119)**:
- `bbox*` = AABB of all vertices in the extraction result (may exceed the requested bbox due to way expansion)
- `bboxRequested*` = the bbox requested at extraction (the input value of CLI `--bbox`). Undefined (zero) for VersionMinor=0; the fallback is to use `bbox*`

### 2.1 Rationale for the magic number

In ASCII, `ODRG` = "OsmDotRoute Graph". The remaining 4 bytes are padded with `\0` for 8-byte alignment.

### 2.2 Versioning rules

- If `versionMajor` differs → the reader **errors** (incompatible)
- If `versionMinor` is greater than the reader's → the reader **logs a warning and continues** (backward-compatible, unknown sections are skipped)
- If `versionMinor` is less than the reader's → the reader **operates normally** (older file)

**VersionMinor history**:

| Minor | Addition | Backward compatibility |
| --- | --- | --- |
| 0 | Initial edition | — |
| 1 | Adds `bboxRequested*` (offsets 88-119). Retains the requested bbox separately from the post-extraction bbox | Old code ignores it as reserved space at the end of the header (safe). When new code reads Minor=0, `bboxRequested*` is treated as undefined (zero) |

---

## 3. Section table

Immediately after the header, `sectionCount` entries are laid out starting from `sectionTableOffset`. Each entry is 24 bytes:

| Offset     | Size   | Field        | Type   | Description                        |
| ---------- | ------ | ------------ | ------ | ---------------------------------- |
| 0          | 2      | `kind`     | uint16 | Section kind ID (see §3.1)         |
| 2          | 2      | `reserved` | uint16 | Reserved (fixed 0)                 |
| 4          | 4      | `flags`    | uint32 | Section-specific flags (initial = 0) |
| 8          | 8      | `offset`   | uint64 | Offset to the section in the file  |
| 16         | 8      | `length`   | uint64 | Section length (bytes)             |

### 3.1 List of section kind IDs

| ID             | Kind                   | Contents                                                 | Phase 2 required        |
| -------------- | ---------------------- | -------------------------------------------------------- | ----------------------- |
| 0x0001         | Vertex Table           | Vertex array                                             | Required                |
| 0x0002         | Edge Table             | Edge array                                               | Required                |
| 0x0003         | Edge Shape Buffer      | Contiguous edge shape buffer                             | Required                |
| 0x0004         | Edge AABB Table        | Edge AABBs (double × 4)                                  | Required                |
| 0x0005         | Edge Flag Table        | Edge flags (bitflag)                                     | Required                |
| 0x0006         | Edge Spatial Index     | STR-packed static R-tree                                 | Required                |
| 0x0007         | Baked Profile Table    | profile × edge →`(canPass, speedKmh, oneway)`            | Required                |
| 0x0008         | Turn Restriction Table | Turn restriction table (Phase 4+)                        | Optional (reserved with length=0) |
| 0x0009         | Metadata               | UTF-8 JSON metadata                                      | Required                |
| 0x0100–0xFFFF  | Reserved               | Future extensions                                        | —                      |

Unknown `kind` is skipped by the reader (forward compatibility).

---

## 4. Section details

### 4.1 Vertex Table (kind = 0x0001)

A fixed-length array ordered by vertex ID (0-based, sequential).

| Offset     | Size   | Field      | Type   | Description     |
| ---------- | ------ | ---------- | ------ | --------------- |
| 0          | 8      | `lon`    | double | Longitude (WGS84) |
| 8          | 8      | `lat`    | double | Latitude (WGS84) |

1 vertex = 16 bytes. `vertexCount` × 16 bytes.

Internal structure exposed as `ReadOnlySpan<Vertex>`:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct Vertex
{
    public readonly double Lon;
    public readonly double Lat;
}
```

### 4.2 Edge Table (kind = 0x0002)

A fixed-length array ordered by edge ID (0-based, sequential).

| Offset     | Size   | Field                 | Type   | Description                                                                     |
| ---------- | ------ | --------------------- | ------ | ------------------------------------------------------------------------------- |
| 0          | 4      | `fromVertexId`      | uint32 | Start vertex ID                                                                 |
| 4          | 4      | `toVertexId`        | uint32 | End vertex ID                                                                   |
| 8          | 8      | `shapeOffset`       | uint64 | Shape start offset within the Edge Shape Buffer (bytes)                         |
| 16         | 4      | `shapeLength`       | uint32 | Number of shape points (see §4.3 for whether endpoints are included)            |
| 20         | 4      | `bakedProfileIndex` | uint32 | Index into the Baked Profile Table (key into the 2D profile × edge table)       |

1 edge = 24 bytes. `edgeCount` × 24 bytes.

The edge AABB and edge flags are separated into distinct sections (§4.4 / §4.5), a strategy that places only what the hot path needs onto cache lines.

### 4.3 Edge Shape Buffer (kind = 0x0003)

The shape point sequences of all edges are placed contiguously in a single buffer.

Each shape point is 16 bytes of `(double lon, double lat)`.

`shapeLength` is the number of intermediate points, **excluding** the endpoints (fromVertex / toVertex). `shapeLength = 0` means a straight edge (only the 2 endpoints).

#### 4.3.1 Ordering (finalized in v0.2)

Shape points are placed in **edge ID order** (user decision 2026-05-21).

Reasons for adoption:

- The extraction tool's writing is simple (process edges in ID order → append directly to the end of the buffer)
- Cache locality works for the main workload "scan in edge ID order" (profile evaluation, benchmarks, etc.)
- Phase 3 R-tree queries result in random ID access, but this is expected to be absorbed by the MMF page cache

Alternatives not adopted:

- **Reordering by Hilbert curve / R-tree leaf order**: theoretically effective as a Phase 3 hot-path optimization, but it complicates the extraction tool implementation and exceeds Phase 2's scope. To be re-evaluated as a Phase 3 trailing option if the Phase 3 Step 3E benchmark determines that "random ID access is a bottleneck"

`NativeRoadGraph.GetShape(edgeId)` returns one of the following views:

- Intermediate points only: `ReadOnlySpan<GeoCoordinate>` of length `shapeLength`
- Endpoints included: `GetFullShape(edgeId)` is a view of `shapeLength + 2` points including endpoints (the implementation must copy and inject the `Vertex` values; the hot path uses the intermediate-points version)

### 4.4 Edge AABB Table (kind = 0x0004)

In edge ID order, fixed length 32 bytes:

| Offset     | Size   | Field      | Type   |
| ---------- | ------ | ---------- | ------ |
| 0          | 8      | `minLon` | double |
| 8          | 8      | `minLat` | double |
| 16         | 8      | `maxLon` | double |
| 24         | 8      | `maxLat` | double |

`edgeCount` × 32 bytes.

Exposed as `ReadOnlySpan<Aabb>`:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct Aabb
{
    public readonly double MinLon;
    public readonly double MinLat;
    public readonly double MaxLon;
    public readonly double MaxLat;
}
```

### 4.5 Edge Flag Table (kind = 0x0005)

In edge ID order, `edgeFlagBytes` per edge (1 or 2 bytes, specified in the header).

#### Bit assignment finalized in Phase 2 v0.2

Finalized as **2 bytes (uint16)** (user decision 2026-05-21). Attributes that can be mechanically baked from OSM tags are adopted broadly (plan §5.4-4, internal note [[project-phase2-dynamic-restriction-design]]). The 12 attributes Bridge–SchoolZone + Oneway 2 bits = 14 bits used, with the remaining 2 bits reserved.

| Bit    | Name                      | OSM tag → bake rule (v0.1 draft)                                            |
| ------ | ------------------------- | ---------------------------------------------------------------------------- |
| 0      | `IsBridge`              | `bridge=yes` / `bridge=*` (excluding `bridge=no`)                        |
| 1      | `IsTunnel`              | `tunnel=yes` / `tunnel=*`                                                |
| 2      | `IsElevated`            | `layer >= 1` or `bridge=viaduct`                                         |
| 3      | `IsRoundabout`          | `junction=roundabout`                                                      |
| 4      | `IsToll`                | `toll=yes`                                                                 |
| 5      | `IsPrivateAccess`       | `access=private`                                                           |
| 6      | `IsServiceWay`          | `highway=service`                                                          |
| 7      | `IsTrack`               | `highway=track` (dirt surface, farm road)                                  |
| 8      | `IsLivingStreet`        | `highway=living_street` (residential street)                              |
| 9      | `IsPedestrianSeparated` | `sidewalk=yes` / `sidewalk=both` (sidewalk separated)                    |
| 10     | `IsWinterClosed`        | `seasonal=winter` / `winter_road=no` etc. (winter closure)              |
| 11     | `IsSchoolZone`          | `hazard=school_zone` or nearby `amenity=school` (school route, TBD for initial edition) |
| 12     | `IsOnewayForward`       | `oneway=yes` (passable only in the from → to direction)                    |
| 13     | `IsOnewayBackward`      | `oneway=-1` (passable only in the to → from direction)                     |
| 14     | Reserved                  | —                                                                           |
| 15     | Reserved                  | —                                                                           |

If neither `IsOnewayForward` nor `IsOnewayBackward` is set, the edge is bidirectional. Both can never be set at once.

#### Operational policy

Following plan §5.6-4, the policy is "adopt as many as possible, and prune them once judged operationally unnecessary". The pruning decision is made in Phase 3 Step 3H.

`SchoolZone` is hard to bake from an OSM tag alone (`hazard=school_zone`) (rarely used in practice; coordinating with nearby `amenity=school` is complex). In v0.2, bit 11 is reserved, and the extraction tool outputs `IsSchoolZone = 0` fixed. If judged operationally unnecessary in Phase 3, it is pruned; if needed, the bake rule (a school facility within radius N m) is finalized in Step 3.

### 4.6 Edge Spatial Index (kind = 0x0006) — STR-packed static R-tree

#### 4.6.1 STR (Sort-Tile-Recursive) algorithm

Build procedure (conversion tool side):

1. For all edges, compute the AABB center point (`(minLon+maxLon)/2`, `(minLat+maxLat)/2`)
2. From the node branching factor `M` (e.g., 16) and the total edge count `N`, determine the leaf node count `L = ⌈N/M⌉` and strip count `S = ⌈√L⌉`
3. Sort edges by longitude (x) and split into `S` strips
4. Within each strip, sort by latitude (y) and group into leaf nodes `M` at a time
5. Compute each leaf node's AABB as "the union of all contained edge AABBs"
6. Recursively build internal nodes with leaf nodes as children (the same M, S algorithm)
7. Aggregate up to the root

Characteristics:

- Can be built in one pass with **optimal placement** for static data
- If the node branching factor M is fixed, the **height is ⌈log_M(N)⌉**
- Node AABBs do not overlap in a data-dependent way (minimal overlap)

#### 4.6.2 R-tree serialization

Nodes are **arrayed**, and children are referenced by array index (no pointers, MMF-friendly).

| Offset     | Size   | Field                   | Type        | Description                |
| ---------- | ------ | ----------------------- | ----------- | -------------------------- |
| 0          | 4      | `nodeCount`           | uint32      | Total node count (leaf + internal) |
| 4          | 4      | `rootIndex`           | uint32      | Index of the root node     |
| 8          | 4      | `nodeBranchingFactor` | uint32      | Branching factor M (v0.2 initial value = 16, re-evaluated by measurement in Phase 2 Step 3) |
| 12         | 4      | `treeHeight`          | uint32      | Tree height (reference info) |
| 16         | ~      | Node array              | RTreeNode[] | Each node is fixed at 56 bytes |

Each node is:

| Offset     | Size   | Field               | Type     | Description                                               |
| ---------- | ------ | ------------------- | -------- | --------------------------------------------------------- |
| 0          | 8      | `minLon`          | double   | Node AABB                                                 |
| 8          | 8      | `minLat`          | double   |                                                           |
| 16         | 8      | `maxLon`          | double   |                                                           |
| 24         | 8      | `maxLat`          | double   |                                                           |
| 32         | 4      | `firstChildIndex` | uint32   | If leaf, the first edge ID; if internal, the first child node index |
| 36         | 4      | `childCount`      | uint32   | Number of children (if leaf, the number of contained edges; if internal, the number of child nodes. ≤ M) |
| 40         | 4      | `flags`           | uint32   | bit0 =`IsLeaf`, bit1–31 = reserved                        |
| 44         | 12     | `reserved`        | byte[12] | Zero-filled (for future extension)                        |

1 node = 56 bytes.

#### 4.6.3 Runtime query (implemented in Phase 3)

Query that retrieves "the set of edge IDs intersecting the AABB of a restriction polygon":

```text
Query(queryAabb):
  stack = [rootIndex]
  result = []
  while stack:
    nodeIdx = stack.Pop()
    node = nodes[nodeIdx]
    if not node.aabb intersects queryAabb: continue
    if node.IsLeaf:
      for i in 0..node.childCount-1:
        edgeId = node.firstChildIndex + i
        if edges[edgeId].aabb intersects queryAabb:
          result.Add(edgeId)
    else:
      for i in 0..node.childCount-1:
        stack.Push(node.firstChildIndex + i)
  return result
```

Implemented as a loop using `Stack<int>` (no recursion), traversing views over `ReadOnlySpan<RTreeNode>`, zero-allocation.

### 4.7 Baked Profile Table (kind = 0x0007)

A 2D profile × edge table. For each profile, `(canPass, speedKmh, oneway)` for each edge is baked.

#### 4.7.1 Header

| Offset     | Size   | Field            | Type   | Description                                        |
| ---------- | ------ | ---------------- | ------ | -------------------------------------------------- |
| 0          | 4      | `profileCount` | uint32 | Number of profiles (same value as header `profileCount`) |
| 4          | 4      | `entrySize`    | uint32 | Size of one entry (initial = 8 bytes)              |

Each profile name is stored separately as a table of UTF-8 strings:

#### 4.7.2 Profile name table

`profileCount` entries:

| Offset     | Size   | Field          | Type   | Description                |
| ---------- | ------ | -------------- | ------ | -------------------------- |
| 0          | 4      | `nameOffset` | uint32 | Offset within the string buffer |
| 4          | 4      | `nameLength` | uint32 | UTF-8 byte count           |

A UTF-8 string buffer at the end.

#### 4.7.3 Entry table (profile × edge)

`profileCount × edgeCount` entries (blocked per profile, in edge ID order within each block):

| Offset     | Size   | Field        | Type    | Description                                                                |
| ---------- | ------ | ------------ | ------- | -------------------------------------------------------------------------- |
| 0          | 4      | `speedKmh` | float   | Travel speed (km/h). 0 if `canPass=false`                                |
| 4          | 1      | `flags`    | byte    | bit0 =`CanPass`, bit1 = `Forward`, bit2 = `Backward`, bit3–7 = reserved |
| 5          | 3      | `reserved` | byte[3] | Zero-filled                                                                |

1 entry = 8 bytes. Phase 1's `ProfileEvaluator.Evaluate(IReadOnlyDictionary<string,string> osmTags)` is called by the conversion tool to bake.

#### 4.7.4 Runtime access

```csharp
// Via the bakedProfileIndex of the Edge Table
var edgeRow = edges[edgeId];
var entry = bakedProfileTable[profileId, edgeRow.bakedProfileIndex];
// entry.speedKmh, entry.CanPass, entry.Forward, entry.Backward
```

#### 4.7.5 Operation of `bakedProfileIndex` (finalized in v0.2)

In v0.2, **`bakedProfileIndex == edgeId`** (user decision 2026-05-21, YAGNI).

Reasons for adoption:

- The extraction tool implementation is simple (no hash table for OSM tag sets needed)
- The table size is `profileCount × edgeCount × 8B`. For Tsushima City (57k edges × 2 profiles) it is 0.9 MB, and even for an Aichi Prefecture estimate (several million edges × 4 profiles) it is on the order of tens of MB. No runtime RAM pressure via MMF
- Aggregation of edges with identical OSM tag sets is re-evaluated for adoption as a Phase 3 trailing option **only if the Phase 3 benchmark reveals that the `.odrg` file size or IO volume is a bottleneck**

Alternative not adopted:

- **Aggregation by OSM tag set hash**: expected to reduce table size by 10–30%, but requires a hash table equivalent to `Dictionary<TagSet, uint>` in the extraction tool. It bloats Phase 2's scope while the effect is unclear (IO was not a bottleneck even in the Phase 1 benchmark)

### 4.8 Turn Restriction Table (kind = 0x0008, reserved for Phase 4+)

In Phase 2 v0.1, reserved only with length = 0. Planned layout for when it is implemented in Phase 4+:

| Offset     | Size   | Field                | Type              | Description  |
| ---------- | ------ | -------------------- | ----------------- | ------------ |
| 0          | 4      | `restrictionCount` | uint32            | Number of turn restrictions |
| 4          | ~      | Entry array          | TurnRestriction[] |              |

Each entry draft: `fromEdgeId(4) + viaVertexId(4) + toEdgeId(4) + restrictionType(1) + reserved(3)` = 16 bytes

The entry is registered in the section table but with `length = 0`.

### 4.9 Metadata (kind = 0x0009)

A UTF-8 JSON string. Example:

```json
{
  "createdAt": "2026-05-21T10:00:00Z",
  "createdBy": "OsmDotRoute.Extractor 0.2.0",
  "sourcePbf": "tsushima.osm.pbf",
  "sourcePbfHash": "sha256:abcdef...",
  "profiles": ["car", "pedestrian"],
  "edgeFlagBits": {
    "0": "IsBridge",
    "1": "IsTunnel",
    "...": "..."
  },
  "rtreeBranchingFactor": 16,
  "phase1EquivalenceCheck": {
    "routerDb": "tsushima.routerdb",
    "vertexDelta": 0,
    "edgeDelta": 0
  }
}
```

The JSON content evolves during implementation. Compatibility breaks are forbidden (it must be kept backward-compatible).

---

## 5. Writing (Extractor side) algorithm

### 5.1 Overall pipeline

```text
1. Read PBF (OsmDotRoute.Pbf)
   → IEnumerable<OsmNode> / OsmWay / OsmRelation

2. Road way filter
   → take highway=*, exclude access=no / area=yes (plan §5.6-17)

3. Extract vertex candidates
   → reference nodes of all ways, intersections (nodes with degree ≥ 3), and endpoints become vertex candidates
   → intermediate nodes of a way (degree 2, exclusive to road ways) become shape points

4. Assign vertex IDs
   → assign 0-based sequential numbers to vertex candidates

5. Generate edges
   → split each way between vertices
   → determine fromVertexId / toVertexId / shape (intermediate point sequence) / OSM tag set
   → consider oneway (a bidirectional road is 2 edges)

6. Compute edge AABBs
   → derive minLon, minLat, maxLon, maxLat from the endpoints + all shape points

7. Extract edge flags
   → following the bit assignment table in §4.5, pack OSM tags → 16 bits

8. Build STR R-tree
   → the algorithm in §4.6.1

9. Build Baked Profile Table
   → ProfileEvaluator.Evaluate for each profile × each edge
   → fill in (canPass, speedKmh, oneway)

10. Write file
    → header (fill with 0 first, overwrite at the end)
    → section table (reserve first, overwrite at the end)
    → each section body
    → go back and overwrite the header and section table with final values
```

### 5.2 Streaming and temporary buffers

Since a PBF can reach several GB, each phase assumes streaming. However, "edge AABBs" and "STR R-tree build" require all edge AABBs in memory. For the Tsushima City PBF (tens of thousands of edges), it is expected to fit in a few MB.

For all of Aichi Prefecture (several million edges), it is 32 bytes × several million = on the order of tens of MB.

### 5.3 Strategy for extracting a bbox range from a Japan-wide PBF

RAM peak prediction and countermeasures for extracting **up to a prefecture-scale bbox range** into `.odrg`, using `japan-latest.osm.pbf` (2.3 GB, §8.3) as the input source. To avoid the problem where region-specific PBFs cut the road network across prefectural borders, the Japan-wide PBF + `--bbox` is the default workflow (§8.3).

**Premise**: The maximum size of the output `.odrg` is prefecture-scale (~1 GB, §8.2). A Japan-wide `.odrg` is not created.

#### 5.3.1 `--bbox` is a mandatory feature when using a Japan-wide PBF

A mandatory CLI option:

```text
osmdotroute-extractor extract \
  --input japan-latest.osm.pbf \
  --bbox 136.70,35.16,136.78,35.20 \
  --profiles car,pedestrian \
  --output tsushima.odrg
```

Coordinates are **only specified directly as lon,lat** (finalized in v0.1.3): `--bbox minLon,minLat,maxLon,maxLat`, 4 comma-separated values. WGS84.

- Mesh code specification / prefecture name presets are not adopted in v0.1 (to be added in Phase 3 or later if there is demand)
- **If `--bbox` is not specified, abort with an error** (finalized in v0.1.3). This literally makes accidental generation of a Japan-wide `.odrg` outside Phase 2 requirements impossible. There is no automatic bbox adoption from the PBF HeaderBlock or continuation with a warning either

#### 5.3.2 Problem with a naive implementation (PBF scanning side)

OSM Japan has about 700M nodes. Building a "node ID → position" dictionary for the **entire PBF** at once is 700M × 24 B = **about 17 GB**, which is impossible on a 16 GB RAM machine.

However, if the output range is narrowed to a bbox, the nodes to retain become only those within that range. For example, the Tsushima City bbox is on the order of tens of thousands to hundreds of thousands of nodes, and a prefecture bbox is on the order of a few million nodes.

#### 5.3.3 Adopted strategy: 2-pass scan + bbox early filter

Using the PBF block structure (PrimitiveBlock), only the data within the bbox is expanded into memory:

##### Pass 1: Identify node IDs within the bbox (pre-filter)

1. Scan the PBF from the start, processing Node / DenseNodes blocks
2. Determine whether each node's (lon, lat) is within `--bbox`
3. Record node IDs within the bbox in a bitset
4. Call the number of nodes within the bbox N_in (per prefecture, N_in ≈ a few million)

##### Pass 2: Road way filter and reference node addition (full-way-unit adoption, finalized in v0.1.3)

1. Re-scan the PBF, processing Way blocks
2. For ways that passed the road filter (`highway=*` + `access` exclusion):
   - If even **one** of the way's reference nodes overlaps the bitset (within the bbox), make the **entire way** an adoption candidate
   - Add **all** of the way's reference nodes to the "required node set" (even those outside the bbox are needed as shape if they continue a way inside the bbox)
3. This preserves the continuity of ways that **slightly extend beyond** the bbox (roads crossing prefectural borders are completed as shapes)

The adopted policy "**full way unit**" is finalized in v0.1.3 (buffered bbox or connectivity-based recursion not adopted). Reasons:

- OSM ways are naturally split at intersections and attribute changes, so the length of a single way does not grow unpredictably large
- A buffered bbox requires tuning the buffer width, and continuity still breaks once the way length is exceeded
- Connectivity-based recursion risks dragging in all of Japan in the worst case, and adjusting the cutoff depth is complex

##### Pass 3: Expand the positions of required nodes into memory

1. Re-scan the PBF, processing Node / DenseNodes blocks
2. Store "ID → (lon, lat)" only for IDs in the required node set into a `Dictionary<long, (double, double)>`
3. Per prefecture, a few million nodes × 24 B ≈ tens to hundreds of MB

##### Edge construction

1. Scan adopted ways, normalize vertices, generate edges and shapes
2. Accumulate into `List<EdgeRecord>` in parallel with edge AABB computation
3. Per prefecture, several million edges × ~64 B ≈ hundreds of MB

##### STR R-tree build + writing

1. x sort / strip split / y sort of edge AABB centers (in-place)
2. Write each section to disk sequentially (streaming)

#### 5.3.4 RAM peak prediction (specifying a prefecture unit as the bbox)

| Phase             | RAM peak (per prefecture) |
| ----------------- | -------------------------- |
| End of pass 1     | ~100 MB (bitset)           |
| End of pass 2     | ~150 MB                    |
| End of pass 3     | ~500 MB (node dictionary)  |
| During edge build | ~1 GB                      |
| During R-tree build | ~1.2 GB                  |

**Feasibility on a 16 GB machine**: ample headroom (peak 1.2 GB).
**8 GB machine**: works without problems.

For a Tsushima City unit (city-level bbox), it is expected to stay under 200 MB throughout all phases.

#### 5.3.5 Rough estimate of extraction time

Because the PBF is scanned in 3 passes, even for a Tsushima City unit the dominant factor is the time to read through the entire Japan PBF. We expect a few minutes for a city unit and roughly 5–15 minutes for a prefecture unit. To be measured in Step 3.

Future optimization idea (v0.2+): Since the PBF `BlobHeader` has no bbox information, fully skipping out-of-bbox blocks is difficult, but it can be shortened by supporting indexed PBFs via `HeaderBlock` (such as `required_features = "BoundingBox"`). In Phase 2 v0.1 it is implemented as a purely 3-pass sequential scan.

---

## 6. Reading (NativeRoadGraph, Phase 3) patterns

### 6.1 Opening the MMF

```csharp
using var mmf = MemoryMappedFile.CreateFromFile(odrgPath, FileMode.Open);
using var accessor = mmf.CreateViewAccessor();

// Read the header
accessor.Read<Header>(0, out var header);

// Verify magic
if (header.MagicAsString != "ODRG\0\0\0\0") throw ...;

// Verify version
if (header.VersionMajor != 1) throw ...;
```

### 6.2 Reading the section table

```csharp
var sectionTable = new SectionEntry[header.SectionCount];
accessor.ReadArray(header.SectionTableOffset, sectionTable, 0, sectionTable.Length);

// Resolve offset by kind
var vertexSection = sectionTable.First(s => s.Kind == 0x0001);
```

### 6.3 Zero-copy Span view (ideal form)

```csharp
unsafe
{
    byte* basePtr = null;
    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);
    try
    {
        var vertices = new ReadOnlySpan<Vertex>(
            basePtr + vertexSection.Offset,
            (int)header.VertexCount);
        // Access vertices[i].Lon, vertices[i].Lat. No copy
    }
    finally
    {
        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
    }
}
```

A `MemoryMappedSegment<T>` wrapper type that localizes `unsafe` is planned (Phase 3).

### 6.4 Calling the R-tree query

```csharp
// Compute the AABB of the restriction polygon
var queryAabb = polygon.GetAabb();

// R-tree query
var candidateEdgeIds = rtree.Query(queryAabb);  // Stack-based, zero-allocation

// Polygon-shape intersection test for each candidate
foreach (var edgeId in candidateEdgeIds)
{
    var shape = graph.GetShape(edgeId);  // ReadOnlySpan<GeoCoordinate>
    if (polygon.Intersects(shape)) { ... }
}
```

---

## 7. Validity verification rules (checked on the reader side)

| Verification item      | Error condition                                                  |
| ---------------------- | ---------------------------------------------------------------- |
| Magic                  | `magic != "ODRG\0\0\0\0"`                                      |
| Major version          | `versionMajor != 1`                                            |
| Header size            | File size < 256                                                  |
| Bounding box           | `minLon >= maxLon` or `minLat >= maxLat`                     |
| Vertex count consistency | Vertex Table `length != vertexCount * 16`                     |
| Edge count consistency | Edge Table `length != edgeCount * 24`                          |
| AABB count consistency | Edge AABB Table `length != edgeCount * 32`                     |
| Edge flag consistency  | Edge Flag Table `length != edgeCount * edgeFlagBytes`          |
| Shape offset           | `shapeOffset + shapeLength * 16` is within the Shape Buffer    |
| Vertex ID range        | `fromVertexId < vertexCount` and `toVertexId < vertexCount`  |
| R-tree root            | `rootIndex < nodeCount`                                        |
| Metadata               | Valid JSON as UTF-8                                              |

Verification is performed when `NativeRoadGraph` reads in Phase 3, or by the MapVerifier `.odrg` inspection feature.

---

## 8. File size rough estimate (Tsushima City basis)

Phase 1 RouterDb for Tsushima City = about 14 MB (reference; the Itinero RouterDb includes CHContraction etc.).

Rough estimate of `.odrg` for Tsushima City:

| Section             | Per edge                       | Tsushima City (57k edges, Phase 1 benchmark reference) |
| ------------------- | ----------------------------- | ---------------------------------------- |
| Vertex Table        | (vertex count × 16)           | About 40k vertices × 16 = 0.64 MB        |
| Edge Table          | 24 bytes                      | 57k × 24 = 1.4 MB                        |
| Edge AABB Table     | 32 bytes                      | 57k × 32 = 1.8 MB                        |
| Edge Flag Table     | 2 bytes                       | 57k × 2 = 0.1 MB                         |
| Edge Shape Buffer   | average 80 bytes (5 intermediate points) | 57k × 80 = 4.6 MB             |
| Edge Spatial Index  | 56 bytes × about 4k nodes     | 0.2 MB                                   |
| Baked Profile Table | 2 profiles × 8 bytes          | 57k × 16 = 0.9 MB                        |
| Metadata            | a few KB                      | 0.01 MB                                  |
| **Total estimate**  | —                            | **about 9.6 MB**                         |

About 0.7× the Phase 1 RouterDb (14 MB). Since it goes via MMF, RAM usage is incremental.

### 8.1 All of Aichi Prefecture (reference, Phase 3 prefecture-unit benchmark target)

Assuming a scale of several million edges, 100×: about **1 GB**. To be measured in Phase 3 Step 3G.

### 8.2 Prefecture unit (the maximum size targeted by the Phase 3 benchmark)

Assuming a scale of several million edges, 100×: about **1 GB**. To be measured in Phase 3 Step 3G.

**This is the maximum assumed size of `.odrg`**. There is no need to build all the way up to Japan-wide (cross-region road networks also fit within the bbox if the output range is narrowed to a prefecture unit, see §8.3).

### 8.3 Reason for using a Japan-wide PBF as the input source

**Reference PBF**: `D:\workspace\災害廃棄物処理シミュレーション\Data\japan-latest.osm.pbf` (2.3 GB, the all-of-Japan OSM distributed by Geofabrik)

Although `.odrg` is at most prefecture-scale, **the input PBF uses Japan-wide**. Reasons:

- Region-distributed PBFs (such as Geofabrik's per-prefecture ones) may have the **road network near boundaries cut off**
- Example: with the Aichi Prefecture PBF alone, the ways of national and expressway roads crossing the borders with Gifu and Mie Prefectures are cut off partway, making it an incomplete road network for use in simulation
- By using the Japan-wide PBF as the source and narrowing the output range to a prefecture unit with `--bbox`, **road ways crossing prefectural borders can be imported with their shapes complete**

In other words, "input = Japan-wide PBF" and "output = at most a prefecture `.odrg` (~1 GB)" is the default workflow.

### 8.4 Size comparison of input PBF and output `.odrg`

| Use case            | Input PBF                | `--bbox` spec | Output `.odrg`     |
| ------------------ | ------------------------ | ------------- | ------------------ |
| Single city (Tsushima City) | Regional PBF or japan-latest | City bbox | About 9.6 MB     |
| Prefecture (Aichi Prefecture) | japan-latest (recommended) | Prefecture bbox | About 1 GB (estimated) |
| Cross-border area  | japan-latest (required)  | Area bbox     | hundreds of MB to 1 GB |
| All of Japan       | (outside requirements)   | (outside requirements) | (outside Phase 2 scope) |

For the RAM peak prediction on the extraction tool side, see §5.3.3 (the entire PBF must be scanned, but if the output range is narrowed by bbox, the intermediate buffers are proportional to the output scale).

---

## 9. Public API (at Phase 3 runtime implementation time)

### 9.1 `NativeRoadGraph` (IRoadGraph implementation)

```csharp
internal sealed class NativeRoadGraph : IRoadGraph, IDisposable
{
    public static NativeRoadGraph Open(string odrgPath);

    public int VertexCount { get; }
    public int EdgeCount { get; }
    public Aabb Bounds { get; }

    public GeoCoordinate GetVertex(int vertexId);
    public ReadOnlySpan<GeoCoordinate> GetShape(int edgeId);
    public Aabb GetEdgeAabb(int edgeId);
    public EdgeFlags GetEdgeFlags(int edgeId);
    public (int fromVertexId, int toVertexId) GetEdgeEndpoints(int edgeId);
    public (bool canPass, float speedKmh, bool forward, bool backward) GetBakedProfile(int profileId, int edgeId);

    public IEnumerable<int> QuerySpatialIndex(Aabb queryAabb);  // R-tree query
    public void Dispose();
}
```

### 9.2 `NativeRoadSnapper` (IRoadSnapper implementation)

```csharp
internal sealed class NativeRoadSnapper : IRoadSnapper
{
    public NativeRoadSnapper(NativeRoadGraph graph);
    public GeoCoordinate? Snap(GeoCoordinate point, float searchRadiusMeters);
}
```

Implementation: an AABB query for the search radius via the R-tree → compute the shortest shape distance for candidate edges → return the nearest point.

### 9.3 Modification of `RouterDb.LoadFromFile` (Phase 3)

```csharp
public static RouterDb LoadFromFile(string path)
{
    if (path.EndsWith(".odrg", StringComparison.OrdinalIgnoreCase))
        return new RouterDb(NativeRoadGraph.Open(path));
  
    if (path.EndsWith(".routerdb", StringComparison.OrdinalIgnoreCase))
        throw new NotSupportedException(
            "From Phase 2 onward, direct .routerdb reading is discontinued. Please convert to .odrg with OsmDotRoute.Extractor. " +
            "See README §x.x for the migration procedure.");
  
    throw new NotSupportedException($"Unsupported extension: {path}");
}
```

---

## 10. Open issues and next actions

### 10.1 Things to confirm toward Step 1 completion

- [x] **Bit assignment of edge flags (§4.5)**: finalized with the 12-kind adoption draft (v0.2, 2026-05-21, §4.5)
- [x] **R-tree branching factor M = 16**: v0.2 initial value = 16, re-evaluated by measurement in Phase 2 Step 3 (§4.6.2)
- [x] **Aggregation of the Baked Profile Table**: finalized as `bakedProfileIndex == edgeId` (v0.2, 2026-05-21, §4.7.5)
- [x] **Alignment of the Edge Shape Buffer**: finalized as edge ID order (v0.2, 2026-05-21, §4.3.1)
- [x] **Coordinate system of the `--bbox` option**: lon/lat direct specification only (finalized in v0.1.3, §5.3.1)
- [x] **Handling of out-of-bbox shapes (§5.3.3 pass 2)**: full-way-unit adoption (finalized in v0.1.3, §5.3.3)
- [x] **Behavior when `--bbox` is not specified**: abort with an error (finalized in v0.1.3, §5.3.1)

### 10.2 Items to decide by measurement in Step 2-3 and beyond

- R-tree branching factor M: compare 8 / 16 / 32 by measurement in Phase 2 Step 3, and finalize the optimal value in `nodeBranchingFactor`
- The bake rule for edge flag bit 11 (`SchoolZone`) (`amenity=school` within radius N m): finalize in Phase 3 if operationally needed, or prune if not
- OSM tag set aggregation of the Baked Profile Table: re-evaluate as a trailing option only if the Phase 3 benchmark reveals that the `.odrg` file size or IO is a bottleneck

### 10.3 Items to be finalized in Phase 3

- §6 implementation pattern of the zero-copy Span view (`MemoryMappedSegment<T>` wrapper type)
- §9 final finalization of the public API signatures
- §7 implementation location of validity verification (inside `NativeRoadGraph.Open` / a separate `OdrgValidator` class)
- Spatial-locality reordering of the Edge Shape Buffer (a trailing option if judged necessary)

---

## 11. Revision history

In charge: Claude (Opus 4.7)

### v0.1 (draft) — 2026-05-20

Created the initial skeleton.

- Presented the full layout of the header / section table / 9 sections
- STR R-tree algorithm (build + serialization + query)
- Writing pipeline / MMF reading pattern / validity verification
- Tsushima City size rough estimate (about 9.6 MB) / Phase 3 public API draft
- Edge flags as a draft assigning 12 of 16 bits
- R-tree branching factor M=16 initial value; `bakedProfileIndex` aggregation TBD, carried over to v0.2

### v0.1.1 (draft) — 2026-05-20

Reflected support for the Japan-wide PBF (2.3 GB) (misunderstanding corrected in v0.1.2).

- Initial addition of §8.2 all-of-Japan scale / §5.3 Japan-wide extraction strategy / 3 issues in §10.1

### v0.1.2 (draft) — 2026-05-20

Reflected the user correction "the Japan PBF is a source for cross-region road networks; the output `.odrg` is still at most a prefecture".

- Rewrote §8.2 to "prefecture unit (~1 GB)" and withdrew the Japan-wide `.odrg` (7–8 GB) estimate
- Newly created §8.3 "Reason for using a Japan-wide PBF as the input source" (the cross-border road network problem)
- Newly created §8.4 "Size comparison table of input PBF and output `.odrg`"
- Rewrote §5.3 to "Strategy for extracting a bbox range from a Japan-wide PBF"
- Made `--bbox` a mandatory feature (premise when using a Japan-wide PBF)
- Changed the extraction strategy to a 3-pass scan (including the bbox early filter)
- Corrected the RAM peak from 6 GB → 1.2 GB
- Reorganized the §10.1 open issues around the bbox

### v0.1.3 (draft) — 2026-05-20

Finalized 3 open issues in §10.1 by user decision.

- The `--bbox` coordinate system is **lon/lat direct specification only** (mesh codes and prefecture name presets considered for Phase 3+)
- The handling of out-of-bbox shape ways is **full-way-unit adoption** (buffered bbox and connectivity-based recursion not adopted; the adoption reasons are written in §5.3.3)
- When `--bbox` is not specified, **abort with an error** (no automatic adoption from the PBF HeaderBlock or continuation with a warning either; making accidental generation of a Japan-wide `.odrg` outside Phase 2 requirements impossible)
- Reflected the finalized values in §5.3.1 / §5.3.3, and marked the corresponding 3 items as complete in §10.1

### v0.2 — 2026-05-21

Step 1 completion finalized edition. Finalized the 4 remaining open issues in §10.1 by user decision.

- **Edge flags (§4.5)**: finalized with 12 attributes + Oneway 2 bits = 14 bits used and the remaining 2 bits reserved. `SchoolZone` (bit 11) is reserved only in v0.2, and the extraction tool outputs 0 fixed. Prune if operationally unnecessary in Phase 3, or finalize the bake rule (`amenity=school` within radius N m) if needed
- **Ordering of the Edge Shape Buffer (newly created §4.3.1)**: finalized as edge ID order. Hilbert curve / R-tree leaf order is handed over to §10.3 as a trailing option if judged necessary in the Phase 3 benchmark
- **The `bakedProfileIndex` of the Baked Profile Table (newly created §4.7.5)**: finalized as `bakedProfileIndex == edgeId` (YAGNI). OSM tag set aggregation is re-evaluated only if IO is judged to be a bottleneck in Phase 3
- **R-tree branching factor M (§4.6.2)**: stated that the v0.2 initial value = 16 and that it is re-evaluated by measurement in Phase 2 Step 3
- Marked all of §10.1 as finalized, newly created §10.2 "Items to decide by measurement in Step 2-3 and beyond", and added "Spatial-locality reordering of the Edge Shape Buffer" to §10.3
- Changed the status to "v0.2 finalized (user-agreed)", and added "major promotion (v1.0) only for changes that break compatibility" to the §0.3 update rules
