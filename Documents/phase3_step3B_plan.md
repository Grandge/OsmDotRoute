# Phase 3 ステップ 3B: 動的制約ホットパス高速化 計画書

**ステータス**: ドラフト v0.2（v0.1 + 3B 効果測定方針 Q5〜Q7 確定、2026-05-27）
**対応ステップ**: Phase 3 ステップ 3B（[Phase 3 実装計画書 §6](phase3_implementation_plan.md)、Phase 1 §18.3 解消）
**対応要件**: REQ-NFR-002（制約 100 件下でも経路計算 ≤ 100ms 維持）、REQ-NFR-003（経路 1 本あたりアロケート削減）
**関連文書**:

- [Phase 3 実装計画書 §3.2 / §6](phase3_implementation_plan.md)
- [Phase 3 設計書 §4 動的制約ホットパス](phase3_design.md)（本ステップで肉付け対象、現状「未記述」プレースホルダ）
- [Phase 3 ステップ 3A 計画書](phase3_step3A_plan.md)（3A.4 NativeRTreeQuery を本ステップで利用）
- [Phase 1 設計書 §18.3 制約 100 件短絡効果](phase1_design.md)
- [Phase 1 設計書 §18.4 経路 1 本あたり 77 MB アロケート](phase1_design.md)

---

## 1. 目的とゴール

**目的**: Dijkstra 辺展開時の動的制約評価ホットパスを、**Phase 1 の「線形走査 + シェイプ多角形交差判定 + List/HashSet 毎回 new」**から**「HashSet 1 発のキャッシュ参照」**に圧縮し、REQ-NFR-002 達成余裕を拡大すると同時に Phase 1 §18.4 のアロケート削減土台を提供する。

**Done 判定**:

1. `internal sealed class RestrictedAreaEdgeCache` が新設され、制約 ID → 通行不可エッジ集合（`HashSet<uint>`）+ 制約 ID → 難所エッジ→speedFactor マップ（`Dictionary<uint, double>`）を保持する
2. `IRoadGraph.QueryEdgesByAabb(Aabb queryBounds) -> IEnumerable<uint>` が新設され、`NativeRoadGraph` は `NativeRTreeQuery` (3A.4) で実装、`ItineroRoadGraph` は全エッジ走査 fallback で実装
3. `RestrictedAreaService.AttachGraph(IRoadGraph)` が internal で新設され、`Router` コンストラクタから 1 度だけ呼び出される
4. graph 注入後の `AddBlockArea` / `AddDifficultyArea` 時に eager bake が走り、エッジ ID 集合をキャッシュに格納
5. `EdgeWeightCalculator.EvaluateConstraintFactor` がキャッシュ参照のみに置換され、`BuildFullShape` の `List<GeoCoordinate>` 毎回 new が **不要化**（Phase 1 §18.4 アロケート削減の直接貢献）
6. **公開 API は Phase 1 のまま死守**（`new RestrictedAreaService()` / `AddBlockArea(polygon, tag)` 等のシグネチャ不変）
7. Phase 1 既存 526 件 + Phase 3 3A 累計 595 件すべて pass 維持、graph 注入時と未注入時の双方で制約評価結果が完全一致
8. Native + 制約サービスの統合テスト（経路結果一致 / Block 回避 / Difficulty 速度低下）が新規追加され全 pass
9. **3B 効果測定レポート**: `RouteWithConstraintsBenchmark` を Native 対応 3 モード分岐に改修し、津島市 .odrg を共通基盤として「Itinero / Native-Detached（3B 前相当）/ Native-Attached（3B 後）」の C0/C3 を実測。Native-Detached vs Native-Attached の Mean / StdDev / Allocated 削減効果を**設計書 §4.5 検証方法表に数値で埋め込む**
10. 設計書 §4 が 3B.5 完了時に肉付けされる

**Phase 1 §18.4 削減への寄与**: `EdgeWeightCalculator.BuildFullShape` で毎エッジ展開ごとに発生していた `new List<GeoCoordinate>(N+2)` および `RestrictedAreaService.EvaluateConstraints` 内の `new HashSet<RestrictedAreaId>()` がホットパスから消える。**実数値削減は 3B.5 ベンチで一次確認、3E で本番ベンチに引き継ぎ**。

---

## 2. 前提と現状

### 2.1 既存資産

- Phase 3 ステップ 3A 全体完了（commit `a09805f`、595 件 pass、計画書 v0.10）
- 3A.4 [`NativeRTreeQuery`](../src/OsmDotRoute/Native/NativeRTreeQuery.cs) = bbox クエリ実装済（commit `78d4581`、buffer ベース API、overrun は呼出側で再クエリ）
- 3A.4 `NativeRoadGraph` に R-tree アクセサー追加済（`GetRTreeNodes` / `RTreeRootIndex` / `GetEdgeAabbs` 等）
- Phase 1 [`RestrictedAreaService`](../src/OsmDotRoute/Restrictions/RestrictedAreaService.cs) = 制約管理サービス（公開 API は Phase 3 で**不変死守**）
- Phase 1 [`SpatialIndex<T>`](../src/OsmDotRoute/Geometry/SpatialIndex.cs) = 線形走査の単純実装（Phase 1 §10 注記「性能未達時に R-tree へ差し替える前提」、Phase 1 完了時点では未達でなかったため未差替）
- Phase 1 [`EdgeWeightCalculator`](../src/OsmDotRoute/Routing/EdgeWeightCalculator.cs) = エッジ重み計算器、`EvaluateConstraintFactor` が動的制約ホットパス
- Phase 1 既存 `RestrictedRoutingTests` / `RestrictedAreaServiceTests` 等（テスト件数は実装時に確認）

### 2.2 現状のホットパス連鎖（Phase 1 / Phase 3 3A 完了時点）

```text
DijkstraEngine.Run() 1 ステップ内のエッジ展開:
  EdgeWeightCalculator.EvaluateEdgeDurationSec(en)
    Evaluate(en)                                       ← 3A.3b で graph.EvaluateEdge へ集約済
    EvaluateConstraintFactor(en.From, en.To, en.Shape)
      if (_restrictions is null) return 1.0;
      BuildFullShape(from, to, middle)                  ← ★ alloc: new List<GeoCoordinate>(N+2)
      _restrictions.EvaluateConstraints(shape, eval)
        if (_entries.Count == 0) return 1.0;
        Aabb.FromCoordinates(shape)                     ← Span 全走査
        new HashSet<RestrictedAreaId>()                 ← ★ alloc: seenIds
        foreach sr in _index.Query(edgeAabb)            ← ★ SpatialIndex 線形走査 (制約 100 件で 100 回 AABB)
          if (!seenIds.Add(sr.Id)) continue
          EdgeIntersectsAreaShapes(shape, _entries[sr.Id].Shapes)  ← ★ シェイプ N × Shape M 二重ループ
            for i in shape:
              foreach s in shapes:
                Bounds.IntersectsSegment(p1, p2)
                if Polygon: PolygonIntersection.IntersectsSegment(s.Polygon, p1, p2)
```

★ = 3B で削除/置換対象。Phase 1 §18.4 = 経路 1 本あたり 77 MB アロケートの主因の一つ。

### 2.3 改善後のホットパス連鎖（3B 完了時）

```text
RestrictedAreaService.AddBlockArea(polygon, tag):                 ← bake は add 時に 1 度だけ
  Register(id, area, shapes)
  if (_graph != null):                                            ← Q2 (A) AttachGraph 済
    foreach (s in shapes):
      foreach edgeId in _graph.QueryEdgesByAabb(s.Bounds):
        edgeShape = _graph.GetEdgeShape(edgeId) + 端点
        if EdgeIntersectsAreaShapes(edgeShape, [s]):
          _cache.AddBlocked(id, edgeId)                           ← HashSet<uint>

EdgeWeightCalculator.EvaluateConstraintFactor(en.From, en.To, en.Shape):
  if (_restrictions is null) return 1.0;
  edgeId = ???                                                    ← ★ サブ論点 (en から edgeId をどう得るか、3B.4 着手時)
  if (_restrictions.Cache.IsBlocked(edgeId))                      ← HashSet 1 発
    return PositiveInfinity;
  return _restrictions.Cache.GetDifficultyFactor(edgeId);         ← Dictionary 1 発 (なければ 1.0)
```

### 2.4 ユーザー判断確定（本ステップ着手前、2026-05-27）

- **Q1 = (A) eager bake**: `AddBlockArea` / `AddDifficultyArea` 時に同期的に R-tree クエリ + 交差判定を一括実行。ホットパスは純粋に HashSet 参照のみ
- **Q2 = (A) オプション注入**: `RestrictedAreaService` に `internal AttachGraph(IRoadGraph)` 追加、`Router` コンストラクタで 1 回呼ぶ。graph 未注入時は Phase 1 動作にフォールバック（公開 API 既存形状 `new RestrictedAreaService()` 維持）
- **Q3 = (A) Native のみ**: `ItineroRoadGraph.QueryEdgesByAabb` は全エッジ走査 fallback。Phase 1 既存 Itinero 系テストは Phase 1 パフォーマンスのまま維持、3B 高速化投資を Native に集中（Itinero は 3C で撤去予定）
- **Q4 = (A) 5 サブ分割**: 3B.1〜3B.5 で各サブステップごとに `dotnet test` 全 pass を維持。3A.4〜3A.6 と同じ細分化方針
- **Q5 = (A) Phase 1 既存ベンチ改修**: [`tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs`](../tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs) に Native 対応 3 モード分岐を追加（BenchmarkDotNet）、Phase 1 ベンチ資産を活用、3E 本番ベンチとも連動
- **Q6 = (A) 時間 + アロケート量**: `[MemoryDiagnoser]` を活用、経路 1 本あたり時間と Allocated を実測
- **Q7 = (A) graph 未注入モード**: 「導入前」= Native-Detached（graph 注入なし、Phase 1 動作）、「導入後」= Native-Attached（graph 注入あり、3B キャッシュ動作）。同一 RouterDb (津島市 .odrg) + 同一 89 ペア + 同一プロファイル で 3B オプション有効化だけが違う状態を比較

### 2.5 §5.5-#22 確定（2026-05-26、3A 計画書 v0.1 時点で先行確定済）

- 格納粒度 = **(a) 制約 ID 単位**（`HashSet<uint>` シンプル実装）。タグ単位バルク削除は ID リスト走査で実用上問題なし

---

## 3. アーキテクチャ概要

### 3.1 RestrictedAreaEdgeCache 設計

```text
RestrictedAreaEdgeCache  (internal sealed class)
   ├─ _blockedByArea: Dictionary<RestrictedAreaId, HashSet<uint>>     ← Block 制約 ID → エッジ ID 集合
   ├─ _difficultyByArea: Dictionary<RestrictedAreaId, Dictionary<uint, double>>  ← Difficulty 制約 ID → エッジ ID → speedFactor
   ├─ _blockedEdges: HashSet<uint>                                    ← Block 集約 (IsBlocked O(1))
   ├─ _difficultyByEdge: Dictionary<uint, List<double>>?              ← エッジ ID → 該当する全 Difficulty の speedFactor リスト
   │
   ├─ AddBlocked(areaId, edgeId)                                      ← Add 時に呼ぶ
   ├─ AddDifficulty(areaId, edgeId, factor)
   ├─ RemoveArea(areaId)                                              ← Remove 時に呼ぶ (逆引きで O(K))
   ├─ Clear()
   │
   ├─ IsBlocked(edgeId) -> bool                                       ← ホットパス API
   └─ GetCombinedDifficultyFactor(edgeId) -> double                   ← ホットパス API、該当なし時 1.0
```

### 3.2 IRoadGraph.QueryEdgesByAabb の追加

```csharp
internal interface IRoadGraph : IDisposable
{
    // 既存 (Phase 1 / 3A.3b で追加)
    GeoCoordinate GetVertex(uint vertexId);
    IRoadGraphEdgeEnumerator GetEdgeEnumerator(uint vertexId);
    RoadEdge GetEdge(uint edgeId);
    EdgeEvaluation EvaluateEdge(IRoadGraphEdgeEnumerator en, ProfileEvaluator evaluator);
    EdgeEvaluation EvaluateEdge(RoadEdge edge, ProfileEvaluator evaluator);
    ReadOnlySpan<GeoCoordinate> GetEdgeShape(uint edgeId);
    GeoBounds GetBounds();

    // 3B で追加
    /// <summary>
    /// 指定 AABB と交差するエッジ ID を列挙する（3B 動的制約 eager bake 用）。
    /// </summary>
    /// <remarks>
    /// NativeRoadGraph: NativeRTreeQuery (3A.4) で R-tree クエリ、O(log E)。
    /// ItineroRoadGraph: 全エッジ走査 fallback（Q3 確定、Itinero は 3C で撤去予定）。
    /// </remarks>
    IEnumerable<uint> QueryEdgesByAabb(Aabb queryBounds);
}
```

### 3.3 RestrictedAreaService 統合フロー

```text
new RestrictedAreaService()                                          ← 既存 API 不変
  _entries = empty
  _index = empty SpatialIndex
  _graph = null
  _cache = null

new Router(routerDb, restrictions)                                   ← Router 内部 (3B.3)
  if (restrictions != null):
    restrictions.AttachGraph(routerDb.Graph)                          ← internal
      _graph = graph
      _cache = new RestrictedAreaEdgeCache()
      foreach (id, entry) in _entries:                                ← 既存制約を再評価して bake
        BakeIntoCache(id, entry)

restrictions.AddBlockArea(polygon, tag)                              ← 既存 API 不変
  Register(id, area, shapes)
  if (_cache != null):                                                ← graph 注入済
    BakeIntoCache(id, _entries[id])

restrictions.RemoveByTag(tag)                                        ← 既存 API 不変
  ids = _entries.Where(...).Select(...).ToList()
  foreach id in ids:
    _entries.Remove(id)
    _index.RemoveAll(...)
    if (_cache != null): _cache.RemoveArea(id)
```

### 3.4 ホットパス置換 (EdgeWeightCalculator)

```csharp
private double EvaluateConstraintFactor(uint from, uint to, IReadOnlyList<GeoCoordinate> middleShape, uint edgeId)
{
    if (_restrictions is null) return 1.0;

    // graph 注入済の場合はキャッシュ参照のみ
    if (_restrictions.IsGraphAttached)
    {
        if (_restrictions.Cache.IsBlocked(edgeId)) return double.PositiveInfinity;
        return _restrictions.Cache.GetCombinedDifficultyFactor(edgeId);
    }

    // graph 未注入時は Phase 1 動作にフォールバック
    var shape = BuildFullShape(from, to, middleShape);
    return _restrictions.EvaluateConstraints(shape, _evaluator);
}
```

ホットパスへの edgeId 渡しは `IRoadGraphEdgeEnumerator.CurrentEdgeId` プロパティ追加で実現（既存実装に追加、3B.4 着手前に詳細確認）。

---

## 4. サブステップ詳細

### 4.1 3B.1: `RestrictedAreaEdgeCache` 新設 + 単体テスト

**Done 基準**:

- `src/OsmDotRoute/Restrictions/RestrictedAreaEdgeCache.cs` 新規（`internal sealed class`、frontmatter §3.1 設計）
- 単体テスト（仮 5〜8 件、着手前に最終確定）:
  - 空状態で `IsBlocked` が false / `GetCombinedDifficultyFactor` が 1.0
  - `AddBlocked` 後の `IsBlocked` が true
  - 複数 Difficulty の積算結果が正しい
  - `RemoveArea` 後の `IsBlocked` / 係数解除
  - `Clear` で全エントリ消える
- 累計テスト 595 + 5〜8 = 600〜603 件 pass、Phase 1 既存 526 件 + Phase 3 3A 累計テスト維持
- ビルド 0 Warning / 0 Error

**着手前事前調査の論点候補** (3B.1 着手時に確認):
- T1. Difficulty 速度低下係数の積算規約: 現状 `EvaluateConstraints` は `combined *= ev.SpeedFactor` で積算。キャッシュ側は同じ規約でよいか
- T2. 同一 edgeId が複数 Block 制約に該当する場合の格納方法: `_blockedEdges` HashSet では重複しないが、`_blockedByArea` 逆引きで全制約 ID を保持する必要があるか
- T3. 削除パフォーマンス: `RemoveArea` で `_blockedEdges.ExceptWith(_blockedByArea[id])` するため、当該エッジが他の Block 制約にも該当している場合の整合性

**commit メッセージ案**: `feat: Phase 3 ステップ 3B.1 RestrictedAreaEdgeCache 新設 (単体 N 件、累計 NNN 件 pass)`

### 4.2 3B.2: `IRoadGraph.QueryEdgesByAabb` API 追加 + Native/Itinero 実装

**Done 基準**:

- `IRoadGraph` に `IEnumerable<uint> QueryEdgesByAabb(Aabb queryBounds)` 追加
- `NativeRoadGraph` 実装: 内部で 3A.4 `NativeRTreeQuery.Query` を呼出し、`OdrgBbox` ⇄ `Aabb` 変換、buffer overrun は内部 `QueryWithGrowableBuffer` パターンで処理
- `ItineroRoadGraph` 実装: 全エッジ走査 fallback（`for (uint e = 0; e < EdgeCount; e++) if (...) yield return e;`）
- 単体テスト（仮 4〜6 件、着手前に最終確定）:
  - NativeRoadGraph: ランダム N 個の AABB クエリで R-tree 結果 = Brute-force 結果 完全一致（3A.4 と同じ突合パターン、ただし Aabb 型での突合）
  - ItineroRoadGraph: 全エッジ走査の正確性
- 累計テスト + 4〜6 件、Phase 1 既存 526 件は変更なし

**着手前事前調査の論点候補** (3B.2 着手時に確認):
- T4. `Aabb` (Lat-Lon) と `OdrgBbox` (Lon-Lat) の変換方向: 3A.4 R-tree クエリは `OdrgBbox` を受けるため、`Aabb → OdrgBbox` 変換が必要
- T5. `IEnumerable<uint>` の yield return vs `ReadOnlyMemory<uint>` などの効率的な API: 3B では eager bake の add 時のみ呼ばれるためホットパスではない、yield return で OK か

**commit メッセージ案**: `feat: Phase 3 ステップ 3B.2 IRoadGraph.QueryEdgesByAabb 追加 + Native/Itinero 実装 (単体 N 件、累計 NNN 件 pass)`

### 4.3 3B.3: `RestrictedAreaService.AttachGraph` + eager bake 統合

**Done 基準**:

- `RestrictedAreaService` に `internal AttachGraph(IRoadGraph)` メソッド追加
- 内部に `_graph: IRoadGraph?` + `_cache: RestrictedAreaEdgeCache?` フィールド追加
- `AttachGraph` 内部で既存 `_entries` を全て再評価してキャッシュに反映する `BakeIntoCache(id, entry)` private メソッド
- `AddBlockArea` / `AddDifficultyArea` 時、`_cache != null` なら `BakeIntoCache` 呼出
- `RemoveByTag` / `Remove(id)` / `Clear` 時、`_cache != null` なら対応する Cache 操作呼出
- 公開 API シグネチャ完全不変（既存 Phase 1 テスト互換性死守）
- 単体テスト（仮 5〜8 件、着手前に最終確定）:
  - graph 未注入時の `EvaluateConstraints` が Phase 1 動作（既存テスト維持で代替）
  - graph 注入後、既存制約が自動的にキャッシュに反映される
  - 注入後の add/remove がキャッシュに反映される
  - `Clear` でキャッシュも消える
  - Phase 1 既存 `RestrictedRoutingTests` / `RestrictedAreaServiceTests` 全 pass 維持
- 累計テスト + 5〜8 件

**着手前事前調査の論点候補** (3B.3 着手時に確認):
- T6. `AttachGraph` 呼出回数の規約: 1 回のみ? それとも複数回上書き OK?
- T7. graph と _entries の整合性: graph が `.odrg` ベース、`_entries` の制約形状が graph 範囲外 (例: 海外座標) の場合の挙動
- T8. `RestrictedAreaServiceTests` の既存テスト件数と影響範囲（着手時に grep）

**commit メッセージ案**: `feat: Phase 3 ステップ 3B.3 RestrictedAreaService.AttachGraph + eager bake 統合 (公開 API 不変、累計 NNN 件 pass)`

### 4.4 3B.4: `EdgeWeightCalculator` ホットパス置換

**Done 基準**:

- `IRoadGraphEdgeEnumerator` に `uint CurrentEdgeId` プロパティ追加（または既存 `EdgeId` プロパティが存在する場合はそれを使う、着手前確認）
- `EdgeWeightCalculator.EvaluateConstraintFactor` シグネチャ更新: `(uint from, uint to, IReadOnlyList<GeoCoordinate> middleShape)` → `(uint edgeId, uint from, uint to, IReadOnlyList<GeoCoordinate> middleShape)`
- graph 注入済時のホットパスは `cache.IsBlocked(edgeId)` + `cache.GetCombinedDifficultyFactor(edgeId)` の 2 回参照のみ
- graph 未注入時は Phase 1 動作にフォールバック（`BuildFullShape` + `EvaluateConstraints`）
- `EvaluateEdgeDurationSec` / `EvaluateEdgePartialDurationSec` の呼出側修正
- `RestrictedAreaService` に `internal bool IsGraphAttached` + `internal RestrictedAreaEdgeCache Cache` プロパティ追加（テスト/EdgeWeightCalculator から見える）
- Phase 1 既存テスト全 pass 維持（graph 注入のあり/なしで結果完全一致）

**着手前事前調査の論点候補** (3B.4 着手時に確認):
- T9. `IRoadGraphEdgeEnumerator` に `EdgeId` プロパティが既に存在するか確認 (Itinero 側で取得方法に差異あり)
- T10. `EvaluateEdgePartialDurationSec` (スナップエッジ評価) でも `edgeId` を渡せるか
- T11. `BuildFullShape` の削除可否: ホットパスから完全に消せるか、テスト/fallback 経路で残すか

**commit メッセージ案**: `feat: Phase 3 ステップ 3B.4 EdgeWeightCalculator ホットパス置換 (BuildFullShape ホットパス除去、累計 NNN 件 pass)`

### 4.5 3B.5: Native + 制約パリティテスト + ベンチ改修 + 設計書 §4 反映

**3 サブパート構成**:

#### 4.5-A Native + RestrictedAreaService 統合テスト

- 統合テスト（仮 10〜15 件、着手前に最終確定、`NativeRouterDbFixture` 拡張または新規 fixture）:
  - Block ポリゴンが経路と交差 → 経路距離増加 + Block 通過なし
  - Difficulty ポリゴンが経路と交差 → 経路距離増加 (時間ベースで他経路選択)
  - 複数 Block の組合せ
  - 制約 add → 経路再計算で反映、制約 remove → 経路再計算で復元
  - graph 注入時と未注入時で経路結果完全一致 (パリティ検証)

#### 4.5-B `RouteWithConstraintsBenchmark` 改修 + 3B 効果実測（Q5/Q6/Q7 確定）

- [`tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs`](../tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs) を 3 モード分岐に改修:
  ```csharp
  [Params("Itinero", "Native-Detached", "Native-Attached")]
  public string Mode { get; set; } = "Native-Attached";
  ```
  - `"Itinero"`: 既存 Phase 1 動作（default.routerdb + `ItineroRoadGraph` + restrictions.AttachGraph 呼ばず）
  - `"Native-Detached"`: 津島市 .odrg + `NativeRoadGraph` + `restrictions.AttachGraph` を**呼ばない**（3B 前の動作、フォールバック経路）
  - `"Native-Attached"`: 津島市 .odrg + `NativeRoadGraph` + `restrictions.AttachGraph` 呼出（3B キャッシュ動作）
- 実測対象: **C0**（制約なし、ベースライン）と **C3**（制約 100 件混合、3B 本命）の 2 ケース × 3 モード = 6 シナリオ
- BenchmarkDotNet `[MemoryDiagnoser]` で Mean / StdDev / Allocated 取得
- 実行コマンド: `dotnet run -c Release --project tests/OsmDotRoute.Benchmarks -- --filter "*RouteWithConstraints*" --job short`
- 結果を **設計書 [`phase3_design.md`](phase3_design.md) §4.5 検証方法** に比較表として埋め込む:
  ```text
  | Case | Mode             | Mean    | StdDev  | Allocated | 3B 効果 |
  | C0   | Itinero          | (実測)  | (実測)  | (実測)    | 参考    |
  | C0   | Native-Detached  | (実測)  | (実測)  | (実測)    | 基準    |
  | C0   | Native-Attached  | (実測)  | (実測)  | (実測)    | 効果%   |
  | C3   | Itinero          | (実測)  | (実測)  | (実測)    | 参考    |
  | C3   | Native-Detached  | (実測)  | (実測)  | (実測)    | 基準    |
  | C3   | Native-Attached  | (実測)  | (実測)  | (実測)    | 効果%   |
  ```
- 結果の **3B 効果%** = `(Native-Detached - Native-Attached) / Native-Detached × 100`、時間とアロケート量それぞれに計算
- Itinero 列は参考値（RouterDb 規模差 / R-tree 有無の混合要因あり、3B 単独効果ではない、3E 本番ベンチで詳細）

#### 4.5-C 設計書 §4 全 6 サブセクション肉付け

- 設計書 [`phase3_design.md`](phase3_design.md) §4 を「未記述」プレースホルダから本実装に基づく内容に置換（3A.6 §3 反映パターン踏襲）:
  - 4.1 意図 / 4.2 採用設計 (アーキテクチャ図) / 4.3 設計判断の根拠 (ユーザー判断 Q1〜Q7 確定一覧、計 7 件) / 4.4 トレードオフ・制約 / **4.5 検証方法 (3B.1〜3B.5 のテスト合計件数 + 4.5-B ベンチ結果表)** / 4.6 実装メモ (主要ファイル + commit 番号)

**Done 基準 (3B 全体完了)**:

- 4.5-A 統合テスト全 pass + 4.5-B ベンチ実行結果記録 + 4.5-C 設計書反映完了
- 累計テスト 595 + 3B 累計（着手時に内訳確定）= 目標 620〜640 件 pass
- ビルド 0 Warning / 0 Error
- Phase 1 既存 526 件 (Itinero 系) + Phase 3 3A 累計テスト 全 pass 維持
- ベンチ結果として「**Native-Detached → Native-Attached** の C3 Mean 削減 % と Allocated 削減 %」が定量的に提示される

**着手前事前調査の論点候補** (3B.5 着手時に確認):
- T12. パリティテスト方針: Native 経路 vs Itinero 経路の突合は ID 独立採番により不可能（3A.6 と同じパターン）、Native 内での graph 注入あり/なしの突合で代替
- T13. 制約形状サンプル: 津島市 .odrg の範囲内でテスト用の Block/Difficulty ポリゴンを準備（既存 RestrictedRoutingTests のパターン流用）
- T14. ベンチデータ準備: 既存 `restrictions-mixed-100.json` の制約形状が津島市 .odrg 範囲内に収まっているかの確認（範囲外なら 3B キャッシュ bake で 0 件ヒットとなり効果測定が成立しない）。範囲外の場合は津島市 bbox 内で seed 固定の新規制約データを生成
- T15. Itinero モード切替: `RouteWithConstraintsBenchmark.Setup` 内で `Mode` パラメータ分岐、`Itinero` モードは既存ロジック（default.routerdb）、`Native-*` モードは津島市 .odrg、と Setup の二重化

**最終 commit メッセージ案 (4.5-A + 4.5-B 改修 + 4.5-C 統合)**: `feat: Phase 3 ステップ 3B.5 + 3B 完了 (Native + 制約パリティ + ベンチ 3 モード改修 + 設計書 §4 反映、累計 NNN 件 pass、3B 効果 C3: Mean -XX%/Alloc -XX%)`

**ベンチ実行と結果記録の運用**:
- ベンチコード改修は 3B.5 commit に含める
- ベンチ実行は Claude がローカル PowerShell (`dotnet run -c Release`) で実行
- 実測値を設計書 §4.5 に記録（同 commit、3A.6 §3 反映と同じ運用）
- 3E 本番ベンチで C0〜C4 全件 + 都道府県単位を実施、本ベンチ結果は 3E が上書き予定

---

## 5. リスクと対処

| # | リスク | 影響 | 対処 |
| --- | --- | --- | --- |
| 3B-R1 | キャッシュと `_entries` の不整合（add/remove 時の漏れ） | 経路結果が誤る、Phase 1 動作と乖離 | 3B.3 で「graph 注入時の add/remove は必ず Cache 操作も同時に呼ぶ」を契約化。3B.5 でパリティテスト (graph 注入あり/なし完全一致) を必須化 |
| 3B-R2 | `IRoadGraphEdgeEnumerator.EdgeId` が既存実装になく取得方法に差異 | 3B.4 シグネチャ変更で広範囲影響 | 3B.4 着手前に `IRoadGraphEdgeEnumerator` 実装を全数 grep し、必要なら 3A.3b のような IF 改修 + 全実装一括対応 |
| 3B-R3 | `RemoveArea` 時の整合性: 当該エッジが他の Block 制約にも該当している場合、HashSet から外すと他制約の効果も消える | 制約 remove で意図せず経路が変化 | 3B.1 単体テスト T2/T3 で確認、`_blockedByArea` 逆引きを保持し remove 時は「他制約に存在しないエッジのみ」を `_blockedEdges` から除外 |
| 3B-R4 | ItineroRoadGraph の `QueryEdgesByAabb` 全エッジ走査が極端に遅い | Itinero ベースの既存テストが遅延 | 3B.2 着手時に Phase 1 既存テスト件数 + 実行時間を実測、許容範囲を超えるなら ItineroRoadGraph 側にもエッジ AABB キャッシュを追加検討 |
| 3B-R5 | graph 注入時の bake コスト（既存 _entries 100 件の再評価）が `AttachGraph` レイテンシを増やす | Router コンストラクタが遅くなる | 3B.3 着手時に bake 実行時間を実測、許容外なら lazy 化を Q1 再検討 |
| 3B-R6 | `BuildFullShape` を完全削除すると fallback 経路 (graph 未注入時) が動かない | Phase 1 テスト fail | 3B.4 では fallback 経路を残す、ホットパスからのみ除去。3C 完了時 (Itinero 撤去) に完全削除を再検討 |
| 3B-R7 | エッジ ID 重複参照のメモリ消費: 制約 100 件 × エッジ平均 50 件 = 5,000 件の HashSet 格納 | メモリ増 | 3B.1 で `HashSet<uint>` (4 byte × 5,000 ≈ 20 KB + オーバヘッド) を実測、許容範囲内（54 MB 定常 WorkingSet 比 0.04%） |
| 3B-R8 | 既存 `restrictions-mixed-100.json` 制約データが**津島市 .odrg 範囲外**でベンチ効果が 0% に見える（範囲外なら bake で 0 件ヒット → ホットパスでもキャッシュ参照のみで HashSet 空、Phase 1 動作との差が現れない） | ベンチ結果が誤読される | 3B.5 着手前 T14 で範囲確認、外なら津島市 bbox 内で seed 固定の新規制約データを生成し `samples/Data/restrictions-tsushima-100.json` 等として同梱 |
| 3B-R9 | ベンチ Mode 分岐で Setup が二重化し、初回ロードコスト混入で実測が歪む | 効果数値の信頼性低下 | 3B.5 着手前 T15 で BenchmarkDotNet の `[GlobalSetup]` を Mode 別に分岐、warm-up 実行で計測対象外のロードコストを除外 |

---

## 6. テスト設計サマリ

**追加テスト件数（想定、着手前事前調査でサブステップごとに最終確定）**:

| サブステップ | 件数 (仮) | カテゴリ |
| --- | --- | --- |
| 3B.1 | 5〜8 | RestrictedAreaEdgeCache 単体 (add/remove/clear/積算/パリティ) |
| 3B.2 | 4〜6 | IRoadGraph.QueryEdgesByAabb (Native R-tree 突合 + Itinero fallback) |
| 3B.3 | 5〜8 | RestrictedAreaService.AttachGraph + eager bake + 既存 API 維持 |
| 3B.4 | 0 | EdgeWeightCalculator ホットパス置換 (既存テスト維持で代替) |
| 3B.5-A | 10〜15 | Native + 制約統合テスト (Block 回避 / Difficulty / add+remove / graph 注入あり/なしパリティ) |
| 3B.5-B | 0 | RouteWithConstraintsBenchmark 3 モード改修 + 実測（ユニットテストとしてはカウントしない、ベンチ結果は設計書に記録） |
| 3B.5-C | 0 | 設計書 §4 全 6 サブセクション肉付け（ベンチ結果埋め込み含む） |
| **合計** | **24〜37** | （Phase 3 3A 完了時 595 → 3B 完了時 約 620〜632、加えてベンチ実測値が設計書 §4.5 に定量記録される） |

**並存戦略**:

- Phase 1 / Phase 2 累計 526 件 (Itinero 系) は触らない、全 pass を維持
- Phase 3 3A 累計テストは触らない、全 pass を維持
- 3B 新規テストは fixture を活用（3A.6 `NativeRouterDbFixture` 拡張または新規 `NativeRouterWithRestrictionsFixture`）
- CI 実行時間: 595 件 58s → 3B 追加で許容範囲内に収まるかを 3B.5 完了時に確認

---

## 7. 着手前の確認事項

- [x] §5.5-#22 確定（Cache 粒度 = 制約 ID 単位、2026-05-26）
- [x] 3A 全体完了（commit `a09805f`、595 件 pass）= 3B の依存条件成立
- [x] 着手前事前調査 = ホットパス連鎖 + SpatialIndex 線形走査 + Phase 1 §18.3/§18.4 確認完了
- [x] ユーザー判断 Q1〜Q4 確定（2026-05-27）:
  - Q1 = (A) eager bake
  - Q2 = (A) オプション注入 (AttachGraph)
  - Q3 = (A) Native のみ (Itinero は fallback)
  - Q4 = (A) 5 サブ分割 (3B.1〜3B.5)
- [x] **3B 効果測定方針 Q5〜Q7 確定（2026-05-27）**:
  - Q5 = (A) Phase 1 既存 `RouteWithConstraintsBenchmark` 改修 (BenchmarkDotNet)
  - Q6 = (A) 時間 + アロケート量 (MemoryDiagnoser)
  - Q7 = (A) graph 未注入モード (Native-Detached) を「導入前」ベースライン
- [ ] **本ステップ計画書 v0.2 ユーザー承認**（次の確認ポイント）
- [ ] 3B.1 着手前事前調査 → ユーザー判断（必要なら T1〜T3 等）→ 計画書 v0.2
- [ ] 3B.1 完了 → ユーザー確認 → commit
- [ ] 3B.2 着手前事前調査 → ユーザー判断（必要なら T4〜T5）→ 計画書 v0.3
- [ ] 3B.2 完了 → ユーザー確認 → commit
- [ ] 3B.3 着手前事前調査 → ユーザー判断（必要なら T6〜T8）→ 計画書 v0.4
- [ ] 3B.3 完了 → ユーザー確認 → commit
- [ ] 3B.4 着手前事前調査 → ユーザー判断（必要なら T9〜T11）→ 計画書 v0.5
- [ ] 3B.4 完了 → ユーザー確認 → commit
- [ ] 3B.5 着手前事前調査 → ユーザー判断（必要なら T12〜T13）→ 計画書 v0.6
- [ ] 3B.5 完了 → 3B 全体完了 commit → 設計書 §4 反映完了

---

## 8. 改訂履歴

| 版 | 日付 | 内容 |
| --- | --- | --- |
| v0.1 | 2026-05-27 | 初版起草（着手前事前調査 + ユーザー判断 Q1〜Q4 確定、3A 全体完了直後） |
| v0.2 | 2026-05-27 | 3B 効果測定方針 Q5〜Q7 確定追記。§1 Done 判定 9 追加、§2.4 Q5〜Q7 追記、§4.5 を A/B/C 3 サブパート構成に拡張（A 統合テスト / B `RouteWithConstraintsBenchmark` 3 モード改修 + ベンチ実測 / C 設計書 §4 反映）、§5 リスク 3B-R8 / 3B-R9 追加、§6 テスト件数表更新、§7 着手前確認に Q5〜Q7 確定追加 |
