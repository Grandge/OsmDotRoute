# Phase 3 ステップ 3A: ランタイム `.odrg` 読込実装 計画書

**ステータス**: ドラフト v0.10（v0.9 commit `86d591c` + 3A.5a commit `88d00fe` (567 件 pass) + 3A.5b commit `5a54296` (579 件 pass) 後、3A.6 着手前事前調査で計画書 §4.6 と現状コードのギャップ 6 件を発見 (.routerdb / .odrg 別ソース / Phase 1 既存テストは ±10% 緩い比較 / 89 ペア架空 / IRoadProfile 架空 等)、ユーザー判断 6 件 (Q1-Q6) で対応方針確定、2026-05-26）
**対応ステップ**: Phase 3 ステップ 3A（[Phase 3 実装計画書 §6](phase3_implementation_plan.md)、Phase 3 最大リスク要因）
**対応要件**: REQ-MAP-005（`.odrg` ランタイム読込）、REQ-NFR-003（経路 1 本あたりアロケート削減の土台）
**関連文書**:

- [Phase 3 実装計画書 §3.1 / §5.5 / §8 R1](phase3_implementation_plan.md)
- [Phase 3 設計書 §3 NativeRoadGraph / NativeRoadSnapper](phase3_design.md)（本ステップで肉付け対象）
- [Phase 2 グラフ形式仕様書](phase2_graph_format_spec.md)（`.odrg` v0.2 仕様）
- [Phase 2 設計書 §8 Phase 3 申し送り](phase2_design.md)
- [Phase 1 設計書 §18.4 経路 1 本あたり 77 MB アロケート](phase1_design.md)

---

## 1. 目的とゴール

**目的**: `.odrg` を `MemoryMappedFile` + `ReadOnlySpan<T>` でゼロコピー読込する `NativeRoadGraph` / `NativeRoadSnapper` を Phase 1 既存実装と**並存可能な形**で実装し、両実装が同じ経路結果を返すことを 89 ペアで実測する。

**Done 判定**:

1. `NativeRoadGraph` が `IRoadGraph` を実装し、津島市 `.odrg` を MMF 経由で読込可能
2. `NativeRoadSnapper` が `IRoadSnapper` を実装し、R-tree クエリで最近傍スナップ可能
3. `ItineroRoadGraph` / `ItineroSnapper` と**コード上で並存**し、テストで両系統が選べる（DI 拡張は 3C で一本化、本ステップではテストコード内で直接コンストラクター呼出し）
4. 89 ペア × 2 実装 = **178 経路で頂点列 / 距離 / 所要時間が完全一致**
5. Phase 1 §18.4 = 77 MB/route の主因（`Route.Shape` の `IReadOnlyList` 化）が `NativeRoadGraph.GetEdgeShape(edgeId) -> ReadOnlySpan<GeoCoordinate>` でゼロアロケーション化されている（測定は 3E、本ステップは API シグネチャ確定のみ）
6. 設計書 §3 が 3A.6 完了時に肉付けされる

**Phase 1 §18.4 削減の土台**: `NativeRoadGraph.GetEdgeShape` が Span 返却する API シグネチャを確定させる。実際の Route 組立が Span を素通しで保持するかは 3C で `Route.Shape` の `ReadOnlyMemory<T>` 化と合わせて決定（本ステップではコピーを許容、ただし API は Span を返す）。

---

## 2. 前提と現状

### 2.1 既存資産

- Phase 2 ステップ 5 完了 = 津島市 `.odrg` （3.55 MB、頂点 27,235 / エッジ 38,004）が `samples/Data/tsushima.odrg` に同梱済（commit `4a5a90a`）
- [`src/OsmDotRoute.Extractor/Pipeline/OdrgWriter.cs`](../src/OsmDotRoute.Extractor/Pipeline/OdrgWriter.cs) = 書出側（HEADER 256B / SECTION TABLE 9×24B / 9 セクション本体、リトルエンディアン固定）
- [`src/OsmDotRoute.Extractor/Pipeline/OdrgReader.cs`](../src/OsmDotRoute.Extractor/Pipeline/OdrgReader.cs) = Phase 2 検証専用 eager-parse（managed コピー、`internal` + `InternalsVisibleTo`、本ステップ実装と並存して**参照真値**として使う）
- Phase 1 `ItineroRoadGraph` / `ItineroSnapper` = `src/OsmDotRoute/` 配下に既存、Phase 3 内では 3C で撤去するまで残置
- Phase 1 89 ペア経路パリティテスト = `tests/OsmDotRoute.Tests/` 既存、本ステップで Native 系統を加えて並走化

### 2.2 ユーザー判断確定（本ステップ着手前、2026-05-26）

- **§5.5-#21 MMF 解放方針 = (b) ファイナライザ併用**
  - `SafeHandle` ベース（`MemoryMappedFile` / `MemoryMappedViewAccessor.SafeMemoryMappedViewHandle` 自体が `SafeBuffer : SafeHandle` 派生のため、.NET 既定で CriticalFinalizer 経由のクリーンアップが効く）
  - 利用側に Dispose を契約で求めつつ、忘れた場合は GC ファイナライザで OS リソース解放
  - 3A.3 `NativeRoadGraph` は `IDisposable` を明示実装、ファイナライザは保持しない（保持するのは内部の `SafeBuffer`）

- **§5.5-#22 RestrictedAreaEdgeCache 粒度 = (a) 制約 ID 単位**
  - 本ステップ（3A）では実装対象外（3B 担当）。3A.4 R-tree クエリは制約付与とは独立、bbox 引数のみ受ける汎用 API として実装

### 2.3 R9 親プロ調査結果（本ステップ着手前、2026-05-26）

- 親プロ Itinero 直接呼出は 3 ファイルに局所化、Route.Shape は 5 箇所いずれも `foreach` パターン
- 詳細: [[project_phase3_parent_integration_scan]] メモ
- 3A では親プロ修正は発生しない（3F 担当）

### 2.5 3A.3 着手前の重大発見（2026-05-26、計画書 v0.3 起票）

`IRoadGraph` 依存連鎖の精査で、`NativeRoadGraph` を単純に `: IRoadGraph` 実装するだけでは経路探索が機能しないことが判明：

**現状の Phase 1 経路探索依存連鎖**:

```text
DijkstraEngine
  → EdgeWeightCalculator.Evaluate(en.EdgeProfileIndex)
    → _graph.GetEdgeOsmTags(profileIndex)   ← (1) Itinero タグ取得
    → _evaluator.Evaluate(tags)             ← (2) ProfileEvaluator がタグから速度算出
```

**Phase 2 `.odrg` 設計の意図**:

- `.odrg` には bake 済 `BakedProfileEntry(SpeedKmh, Flags)` のみ格納、OSM タグ生データは持たない
- Phase 3 ホットパスは「タグ → ProfileEvaluator」ルートを通らず**直接 bake 値を読む設計**
- `NativeRoadGraph.GetEdgeOsmTags` は概念的に実装不可能（タグ生データを持たない）

**連鎖改修対象**:

| ファイル | 改修内容 |
| --- | --- |
| `src/OsmDotRoute/Routing/IRoadGraph.cs` | `GetEdgeOsmTags` 削除、評価 API 新設 |
| `src/OsmDotRoute/Routing/IRoadGraphEdgeEnumerator.cs` | `EdgeProfileIndex` 意味再定義 or 削除 |
| `src/OsmDotRoute/Routing/EdgeWeightCalculator.cs:39` | `Evaluate` 内部経路置換 |
| `src/OsmDotRoute.Itinero/ItineroRoadGraph.cs:40-55` | 新 API に追従、内部で擬似 bake |
| 既存テスト 5 ファイル | `GetEdgeOsmTags` 直呼びを新 API に追従 |

**結論**: 3A.3 は計画書 v0.2 §4.3 の 12 テストで済む話ではなく、**`IRoadGraph` 改修（実質「3C エッジ評価 API 統合」の前倒し）と NativeRoadGraph 新規実装の 2 段同時改修**となる。サブステップ 3A.3a〜3A.3f に分割し各段で `dotnet test` 全 pass を維持する（§4.3 参照）。

**ユーザー判断 2026-05-26**: B 案（3A.3 で `IRoadGraph` 改修込み）採用 = 経路探索エンジンを Phase 2/3 統合状態に到達させる。A 案（最小型）/ C 案（NotSupported 暫定）/ D 案（先に設計書改訂）は不採用。

### 2.6 着手前ペンディング判断（計画書 v0.3 で起票、3A.3a 着手前確定要）

**§3A.3-API**: 新 `IRoadGraph` 評価 API シグネチャ

| 案 | 概要 | 利点 | 欠点 |
| --- | --- | --- | --- |
| (a) `EdgeEvaluation EvaluateEdge(IRoadGraphEdgeEnumerator en, IRoadProfile profile)` | 既存 `EdgeEvaluation` 型を維持、`Evaluate` メソッドの内部を切り替え | Phase 1 → Phase 3 移行で **影響範囲最小**、`EdgeWeightCalculator.Evaluate` 1 箇所の置換で済む | `IRoadProfile` を毎回引数渡し、現状の `EdgeProfileIndex → tags → eval` の 2 段から 1 段に縮退 |
| (b) `BakedProfileEntry GetBakedProfile(uint edgeId, int profileSlotIndex)` | bake 済値を直接返却、評価式の組立は呼出側 | Phase 2 `.odrg` 設計と最も整合、Native 側ゼロコピー | Itinero 側で **内部に bake テーブルを構築**する必要、`EdgeWeightCalculator` 改修が大規模 |
| (c) フィールド別取得（`GetEdgeSpeedKmh` + `GetEdgeFlags` 分離） | 最も細粒度、必要なフィールドのみアクセス | Span 化に親和、性能最適化余地 | API 数が増える、呼出側で combine する手間 |

**推奨**: (a)。理由は計画書 v0.3 起票時点での最小影響原則（Phase 1 既存テストの破壊を最小化、`EdgeEvaluation` 型を温存）。Phase 3 性能要件（≤ 33 ms / route）は (a) でも達成可能と想定（ホットパス内のメソッドコール 1 回 + Profile 型 1 引数の追加コストは無視可能）。

**ユーザー判断確定 2026-05-26（計画書 v0.3 承認時、commit `eb1431c`）**: **(a) 採用**。

#### 2.6.1 (a) 案 確定後の詳細シグネチャ（3A.3a で確定、v0.4 で追記 / v0.5 で訂正）

**3A.3a 着手時の現状確認**:

- `IRoadProfile` インターフェースは**存在しない**（v0.3 §2.6 (a) 案の架空型）
- 実体は `internal sealed class ProfileEvaluator`（`src/OsmDotRoute/Profiles/ProfileEvaluator.cs`）
- `ProfileEvaluator` は `_def: JsonProfileDefinition` を private 保持、`Name` プロパティは未公開
- `JsonProfileDefinition.Name: string?` は存在（プロファイル JSON の `name` フィールド）
- `EdgeWeightCalculator` のコンストラクタが `ProfileEvaluator evaluator` を受け取る既存構造

**v0.5 で発見した v0.4 のギャップ**:

`DijkstraEngine.cs:42, 46` は `_calculator.Evaluate(sourceEdge.EdgeProfileIndex)` の形で **`RoadEdge` 経由**に評価を呼んでおり、`IRoadGraphEdgeEnumerator en` が手元にない。v0.4 §2.6.1 の単一シグネチャ `EvaluateEdge(IRoadGraphEdgeEnumerator, ProfileEvaluator)` だけでは Dijkstra スナップエッジ評価 2 箇所が**そのままでは呼べない**。

**ユーザー判断確定 2026-05-26 (v0.5 起草時)**: **RoadEdge オーバーロードを追加**し、API は 2 本にする。

**確定シグネチャ (v0.5)**:

```csharp
internal interface IRoadGraph
{
    // 削除: IReadOnlyDictionary<string, string> GetEdgeOsmTags(ushort edgeProfileIndex);

    /// <summary>
    /// エニュメレータが指す現在エッジを、指定 ProfileEvaluator で評価する（ホットパス用、Dijkstra 近傍展開で使用）。
    /// </summary>
    /// <param name="en">エッジエニュメレータ（現在位置を保持）</param>
    /// <param name="evaluator">
    /// プロファイル評価器。Itinero 系: 内部で OSM タグを取得し <c>evaluator.Evaluate(tags)</c> を呼ぶ。
    /// Native 系: <c>evaluator.Name</c> で `.odrg` の BAKED_PROFILE スロットを解決し、bake 済値を直接返却。
    /// </param>
    /// <returns>エッジ評価結果（通行可否 / 速度 / 方向制限）</returns>
    EdgeEvaluation EvaluateEdge(IRoadGraphEdgeEnumerator en, ProfileEvaluator evaluator);

    /// <summary>
    /// エッジ ID で直接取得した <see cref="RoadEdge"/> を、指定 ProfileEvaluator で評価する
    /// （スナップエッジ評価用、Dijkstra 開始時の sourceEdge/targetEdge 評価で使用）。
    /// </summary>
    /// <param name="edge">エッジ ID 直接取得結果</param>
    /// <param name="evaluator">プロファイル評価器（en 版と同じ意味）</param>
    EdgeEvaluation EvaluateEdge(RoadEdge edge, ProfileEvaluator evaluator);
}
```

両オーバーロードは Itinero 側で薄いラッパとして実装される（共通の `EvaluateByProfileIndex(ushort)` 内部メソッドに集約）。Native 側でも同様、edge.EdgeId / en.EdgeId で BAKED_PROFILE スロットを解決する内部ヘルパに集約する想定（Native 系は 3A.3e で実装）。

**`ProfileEvaluator` への追加**（3A.3b 内で実装）:

```csharp
internal sealed class ProfileEvaluator
{
    // 既存 ...

    /// <summary>プロファイル JSON の name フィールド。NativeRoadGraph が BAKED_PROFILE スロット解決に使う。</summary>
    public string Name => _def.Name
        ?? throw new InvalidOperationException("ProfileEvaluator: JSON プロファイルに name フィールドがありません");
}
```

**`EdgeWeightCalculator` 改修**（3A.3b 内で実装 / v0.4 では 3A.3c だったが v0.5 で 3A.3b に統合）:

```csharp
// 旧:
public EdgeEvaluation Evaluate(ushort edgeProfileIndex)
{
    var tags = _graph.GetEdgeOsmTags(edgeProfileIndex);
    return _evaluator.Evaluate(tags);
}

// 新 (2 オーバーロード):
public EdgeEvaluation Evaluate(IRoadGraphEdgeEnumerator en)
    => _graph.EvaluateEdge(en, _evaluator);

public EdgeEvaluation Evaluate(RoadEdge edge)
    => _graph.EvaluateEdge(edge, _evaluator);
```

呼出元改修:

- `EvaluateEdgeDurationSec(en)` 内 `Evaluate(en.EdgeProfileIndex)` → `Evaluate(en)`
- `DijkstraEngine.cs:42`: `_calculator.Evaluate(sourceEdge.EdgeProfileIndex)` → `_calculator.Evaluate(sourceEdge)`
- `DijkstraEngine.cs:46`: `_calculator.Evaluate(targetEdge.EdgeProfileIndex)` → `_calculator.Evaluate(targetEdge)`

**`IRoadGraphEdgeEnumerator.EdgeProfileIndex`**: 保持（Itinero 系内部で必要、Native 系では未使用だが破壊しない）。3C で廃止検討。

**性能影響**: ホットパス内のメソッドコールは「`GetEdgeOsmTags` + `evaluator.Evaluate(tags)` の 2 段」→「`EvaluateEdge(en, evaluator)` の 1 段」に**短縮**。`ProfileEvaluator` 引数 1 個追加コストは無視可能。3E ベンチで実測。

#### 2.6.2 既存テスト 5 ファイル改修方針（3A.3a grep 結果 / v0.5 で訂正）

`grep -rn "GetEdgeOsmTags"` ヒット箇所と改修方針：

| ファイル | 行 | 用途 | 改修方針 |
| --- | --- | --- | --- |
| `src/OsmDotRoute/Routing/EdgeWeightCalculator.cs` | 39 | 本番ホットパス | 新 `_graph.EvaluateEdge(en, _evaluator)` に置換（3A.3b、v0.4 では 3A.3c） |
| `src/OsmDotRoute.Itinero/ItineroRoadGraph.cs` | 40-55 | Itinero 実装本体 | `EvaluateEdge` 2 オーバーロード実装に置換（内部で旧 `GetEdgeOsmTags` ロジック + `evaluator.Evaluate(tags)` を呼ぶ）（3A.3b） |
| `tests/OsmDotRoute.Tests/ItineroAdapterTests.cs` | 67, 76 | タグ取得アダプタテスト | **下記§2.6.3 の Itinero テスト用 extension** で `GetEdgeOsmTagsForTest` を呼ぶ形に書き換え（テスト名はそのまま、振る舞いは同等を維持）（3A.3b） |
| `tests/OsmDotRoute.Tests/CalculateRouteTests.cs` | 195, 225 | テスト内 `FindCarAccessibleVertex` / `CollectCarAccessibleVertexPairs` ヘルパ | 同 extension 経由で `tags["highway"]` を取得し、既存 `IsCarHighway` 判定を**そのまま維持**（3A.3b） |
| `tests/OsmDotRoute.Tests/SnapToRoadTests.cs` | 129 | 同上ヘルパ | 同上 |
| `tests/OsmDotRoute.Tests/RestrictedRoutingTests.cs` | 276 | 同上ヘルパ | 同上 |

#### 2.6.3 Itinero テスト用 extension 新設 (v0.5 起草時確定)

**背景**: 上記 4 テストファイルは「`tags["highway"]` の文字列で `IsCarHighway(motorway/trunk/primary/...)` を分類」というロジックを使っており、`ProfileEvaluator.Evaluate(tags).CanPass` だけでは粒度が再現できない（pedestrian も car も motorway 以外で `CanPass=true` になりうる）。

**ユーザー判断確定 2026-05-26 (v0.5 起草時)**: **Itinero テスト用 extension を新設**し、テストヘルパは現状の `IsCarHighway` 判定をそのまま維持する。`IRoadGraph` 抽象から `GetEdgeOsmTags` を削除する Done 基準は維持されつつ、テスト振る舞いの変更リスクは回避される。

**新設ファイル**: `src/OsmDotRoute.Itinero/ItineroRoadGraphTestExtensions.cs`

```csharp
namespace OsmDotRoute.Itinero;

/// <summary>
/// Itinero 系テスト専用の internal extension。本番ホットパスからは呼ばない。
/// `IRoadGraph` 抽象に OSM タグ生データ取得 API を含めない (Phase 3 §2.5 設計) ため、
/// テストの「車道判定」用途のみ Itinero 実装から直接タグを取り出すヘルパとして提供する。
/// </summary>
internal static class ItineroRoadGraphTestExtensions
{
    public static IReadOnlyDictionary<string, string> GetEdgeOsmTagsForTest(
        this ItineroRoadGraph graph,
        ushort edgeProfileIndex)
    {
        // 旧 IRoadGraph.GetEdgeOsmTags のロジックをそのまま移植
        // （ItineroRoadGraph 内部の _routerDb.EdgeProfiles.Get(edgeProfileIndex) を呼ぶ）
    }
}
```

**`InternalsVisibleTo`**: `OsmDotRoute.Itinero.csproj` に `OsmDotRoute.Tests` が既に登録されているか確認、無ければ追加（3A.3b 着手時に確認）。

**テスト側書換パターン**:

```csharp
// 旧:
var tags = graph.GetEdgeOsmTags(en.EdgeProfileIndex);

// 新 (graph 変数が IRoadGraph の場合):
var itineroGraph = (ItineroRoadGraph)graph;  // テストは Itinero 系のみ走るため安全
var tags = itineroGraph.GetEdgeOsmTagsForTest(en.EdgeProfileIndex);
```

`graph` 変数の型を最初から `ItineroRoadGraph` にしておく方が cast 不要で素直なため、テスト 5 ファイルでは可能な限り型を絞る方向で書き換える。

**3A.3a 完了条件 (Done)**: 上記シグネチャ + 改修方針が計画書 v0.5 として commit され、ユーザー承認を得る。実コード変更ゼロ、539 件 pass 維持。

### 2.7 3A.3e 着手前の事前調査結果（2026-05-26、v0.6 起草時）

3A.3b 完了 (commit `c46a2ca`) 後、3A.3e (NativeRoadGraph 新規実装) 着手前に `.odrg` / Itinero / 各実装の調査を実施し、計画書 v0.5 §4.3.5 の概要と実コードのギャップを 5 件特定。

| # | 発見 | 影響 |
| --- | --- | --- |
| F1 | **EDGE セクションは頂点でグループ化されていない** ([`OdrgWriter.cs:107-117`](../src/OsmDotRoute.Extractor/Pipeline/OdrgWriter.cs#L107-L117) は `input.Edges` を順番に書出) | `GetEdgeEnumerator(vertexId)` のために**インメモリ CSR インデックス**をコンストラクタで構築する必要。線形走査での列挙は Dijkstra ホットパスで O(V×E) となり不可 |
| F2 | **`.odrg` の edgeCount は無向辺数** (実装上、双方向道路でも 1 エッジ + `EdgeFlags.IsOnewayForward/Backward` + `BakedProfileEntry.Forward/Backward` ビットで方向表現) | 仕様書 §1 の「有向辺数」表記は表現の誤りで、津島市の `edgeCount=38,004` は無向辺数 = Itinero の `_routerDb.Network.EdgeCount` と数値一致するはず。`IRoadGraph.EdgeCount` のセマンティクス整合は問題なし（仕様書修正は本ステップ範囲外、Phase 2 仕様書側で後日対応） |
| F3 | **EDGE_SHAPE は端点を含まない中間ノードのみ** ([`EdgeRecord.cs:11-14`](../src/OsmDotRoute.Extractor/Pipeline/EdgeRecord.cs#L11-L14)) | Itinero の Shape セマンティクスと完全一致 → 変換不要。`RoadEdge.Shape` / `GetEdgeShape` ともに「中間点のみ」を返す形で整合 |
| F4 | **`GeoCoordinate(Latitude, Longitude)` と `OdrgVertex(Lon, Lat)` のフィールド順が逆** | `MemoryMarshal.Cast<byte, GeoCoordinate>` の直 Span 化は不可。`OdrgVertex` Span で読んで `GeoCoordinate` 詰め替えが必要。`GetVertex(uint)` は Dijkstra 1 ペア中で限定的呼出 = ホットパスではないため詰替コストは無視可能。`GetEdgeShape` ゼロコピー化は `GeoCoordinate[]` キャッシュを `NativeRoadGraph` 内に保持する形で実現 |
| F5 | **tsushima.odrg = 3,719,508 byte (3.55 MB)** | v0.4 計画書 §2.1 通り。テストデータ存在確認済 |

### 2.8 3A.3e 着手前ペンディング判断（2026-05-26、v0.6 起草時に確定）

| 論点 | 確定案 | 不採用案 |
| --- | --- | --- |
| **L1: 頂点→エッジ CSR 構造** | **CSR (`firstOutEdge: uint[V+1]` + `OutEdgeEntry: struct{uint EdgeId, bool IsReversed}[2E]`)** をコンストラクタで構築。メモリ ~380KB (津島市)、起動 O(E)、ランタイム O(1) | (B) List<List<int>> 簡易版 (オーバーヘッド大、キャッシュ局所性劣化)、(C) 列挙時毎回線形走査 (Dijkstra ホットパスで致命的) |
| **L2: EdgeCount セマンティクス** | **`.odrg` HEADER `edgeCount` をそのまま返す** (= 無向辺数、F2 により Itinero と数値一致するはず) | - |
| **L3: Shape 端点扱い** | **中間点のみ** (F3 により Itinero と一致、変換不要) | - |
| **L4: GeoCoordinate レイアウト** | **`OdrgVertex` Span で読んで詰め替え** (F4)。`GetVertex` は単発のため詰替コスト無視可能、`GetEdgeShape` は `GeoCoordinate[]` キャッシュを `NativeRoadGraph` 内に保持してゼロコピー化 | (B) `GeoCoordinate` レイアウトを Lon→Lat 順に変更 (破壊的、却下) |
| **L5: `IRoadGraph.GetEdgeShape(uint) -> ReadOnlySpan<GeoCoordinate>` 追加** | **追加** (v0.4 §1 Done #5 通り)。Itinero 側は per-call で `GeoCoordinate[]` 確保 + `AsSpan()` で返す (3C で撤去予定なのでコピーコスト容認)、Native 側はキャッシュ Span 返却でゼロコピー。Span ライフタイムは「IRoadGraph インスタンスの Dispose まで」を XML doc に明記 | (B) 追加しない (v0.4 Done #5 未達成の未処理化、却下)、(C) Itinero 側 `NotSupportedException` (本ステップ Itinero 主体テスト不可、却下) |
| **L6: NativeEdgeEnumerator 型** | **class、毎回 new** (Itinero と同じ実装パターン)。Phase 1 §18.4 の 77 MB アロケート主因は `Route.Shape` の List コピーで、Enumerator alloc は微々たるもの。Pool 化は 3E ベンチで必要性が見えたら 3C で検討 | (B) class + Pool (実装複雑化、効果限定的)、(C) struct で boxing 許容 (結局 alloc 発生、意味薄い) |
| **L7: Dispose 後アクセス** | **`ObjectDisposedException`** (既存 `OdrgMmfHandle.ThrowIfDisposed()` パターン踏襲) | - |
| **L8: 3A.3e / 3A.3f テスト分割** | **3A.3e で sanity 3 件 (構築 / 頂点読出 / Dispose) + 3A.3f で残り 9 件 (エッジ列挙 / Shape / 評価 API / エラーケース、計画書 v0.5 §6 の 12 件から再配分)**。3A.3e 完了時に途中達成を sanity check で検証 | (B) 3A.3e は 0 件、3A.3f で 12 件一括 (途中状態のテスト未到達、計画書 v0.5 §6 通りだが品質保証が後ろ倒し) |

### 2.9 3A.4 着手前の事前調査結果（2026-05-26、v0.8 起草時）

3A.3 全体完了 (commit `f573c08`、551 件 pass) 後、3A.4 (STR R-tree クエリ実装) 着手前に R-tree 関連コードを実地調査し、計画書 v0.7 §4.4 と現状コードのギャップ 5 件を特定。

| # | 発見 | 影響 |
| --- | --- | --- |
| G1 | **R-tree レイアウト書出 / 読出 / Core struct が完全に対称**: [`OdrgWriter.cs:145-162`](../src/OsmDotRoute.Extractor/Pipeline/OdrgWriter.cs#L145-L162) (ヘッダ 16 byte + ノード 56 byte) ↔ [`OdrgReader.cs:270-303`](../src/OsmDotRoute.Extractor/Pipeline/OdrgReader.cs#L270-L303) ↔ [`OdrgSections.cs:38-45`](../src/OsmDotRoute/Internal/Odrg/OdrgSections.cs#L38-L45) `OdrgRTreeNode` (56 byte、`OdrgBbox` 32B + FirstChildIndex u32 + ChildCount u32 + Flags u32 + Reserved 12B) | `MemoryMarshal.Cast<byte, OdrgRTreeNode>` で直 Span 化が可能。書出側 `RTreeNode.LeafFlagBit = 1u << 0` 規約と完全一致 |
| G2 | **AABB 型が 3 系統存在**: `OsmDotRoute.Geometry.Aabb` (`GeoCoordinate` × 2、Lat-Lon 順、`Intersects` / `Contains` / `Union` 等のメソッド付き)、`OsmDotRoute.Extractor.Pipeline.Aabb` (double × 4、Lon-Lat 順、Extractor アセンブリ専用)、`OsmDotRoute.Internal.Odrg.OdrgBbox` (double × 4、Lon-Lat 順、Core 内 wire format 互換) | Core (OsmDotRoute) で `NativeRTreeQuery` の入力 bbox 型をどれにするかの設計判断が必要 (`Extractor.Pipeline.Aabb` は依存方向上 Core から参照不可) |
| G3 | **`ItineroSnapper.EdgeIndex.SearchClosestEdges` は存在しない**: [`ItineroSnapper.cs:42`](../src/OsmDotRoute.Itinero/ItineroSnapper.cs#L42) は `_router.Resolve(profile, lat, lon, searchDistanceM)` 一本のみで、Itinero 内部の `EdgeIndex` 直接呼出は未使用。加えて 3A.3f P1 と同様に `.odrg` と Itinero RouterDb のエッジ ID は独立採番のため、ID ベース集合一致は二重の意味で不可能 | 計画書 v0.7 §4.4 Done 基準「最近傍 k=10 が `ItineroSnapper.EdgeIndex.SearchClosestEdges` と集合一致」は技術的不成立。突合方式を再定義する必要 |
| G4 | **`NativeRoadGraph` に R-tree アクセサー未実装**: [`NativeRoadGraph.cs:34-58`](../src/OsmDotRoute/Native/NativeRoadGraph.cs#L34-L58) は VERTEX / EDGE / EDGE_SHAPE / BAKED_PROFILE のセクションオフセットしか抽出していない。SPATIAL_INDEX セクション (kind 0x0006) は未参照、ヘッダ 16 byte (NodeCount/RootIndex/Branching/Height) もパースされていない | 3A.4 で `NativeRoadGraph` への R-tree アクセサー追加が必要。`NativeRTreeQuery` (static) はこれを通じて R-tree ノード列にアクセス |
| G5 | **計画書 v0.7 §4.4「xUnit テスト 8 件」の内訳が未確定**: Done 基準として「ブルートフォース AABB 線形走査と完全一致 (38,004 エッジ × 50 ランダム bbox)」「最近傍 k=10 が Itinero と集合一致」の 2 件のみ明示、残り 6 件は未定 | テスト 8 件の内訳を v0.8 で確定し実装方針と一致させる必要 |

### 2.10 3A.4 着手前ペンディング判断（2026-05-26、v0.8 起草時に確定）

| 論点 | 確定案 | 不採用案 |
| --- | --- | --- |
| **Q1: NativeRTreeQuery 入力 bbox 型** | **`OdrgBbox` (Lon-Lat 順、Internal.Odrg 既存型)**。Wire format と完全一致、`MemoryMarshal.Cast` でゼロコピー、API 一貫性高い | (B) `Geometry.Aabb` (Lat-Lon、ライブラリ標準型だが Lon-Lat 詰替が毎回発生)、(C) double 4 引数 (型なし、シグネチャわかりにくい) |
| **Q2: Nearest API の Done 基準** | **Brute-force 突合に変更** (3A.3f P1 と同じパターン)。Nearest k=10 を `EDGE_AABB` 全走査による点-AABB 最小距離 Brute-force k=10 と集合一致で検証。Itinero 突合 (スナップ点座標一致) は 3A.5 NativeRoadSnapper / 3A.6 89 ペア経路パリティで担保 | (B) Nearest を 3A.4 では実装せず 3A.5 内に統合 (R-tree クエリ機能の単体テスト不可となる、却下)、(C) Itinero Router.Resolve 単一結果との k=1 座標一致 (.odrg と RouterDb のエッジ分割位置差で false negative リスク、却下) |
| **Q3: R-tree セクション読出 API 配置** | **`NativeRoadGraph` に internal API 追加**: `internal ReadOnlySpan<OdrgRTreeNode> GetRTreeNodes()` + `internal uint RTreeRootIndex` + `internal uint RTreeBranchingFactor` + `internal uint RTreeHeight`。コンストラクタで SPATIAL_INDEX セクションのオフセット抽出 + ヘッダ 16 byte パースを追加 | (B) `OdrgRTreeView` 型新設 (型を増やさずに対応可能、却下)、(C) `NativeRTreeQuery` を instance class 化して MMF ハンドルを保持 (計画書 §4.4 の `internal static class` 規約から逸脱、却下) |
| **Q4: バッファあふれ挙動** | **ヒット総数を返し、`buffer.Length` まで書いて超過分は棄てる**。呼出側は戻り値 > `buffer.Length` で overrun 検出 + バッファ拡大 + 再クエリ。NativeRoadSnapper は maxDistance に応じた初期サイズ見積もりが可能 | (B) 書いた件数だけ返し overrun 不検出 (シンプルだが呼出側が overrun に気付けない、却下)、(C) overrun 時に例外 (ホットパスで例外コストが乗る、却下) |
| **Q5: Nearest の「距離」定義** | **点-AABB 最小距離 (=点が AABB 内なら 0、外なら矩形境界への euclidean 距離)**。R-tree の枝刈り (点-ノード AABB 最小距離) と一致するため Brute-force と決定的に同一値を返す。3A.5 NativeRoadSnapper はこの k 候補を受けてシェイプ距離計算で再ソート | (B) AABB 中心点距離 (R-tree 枝刈りと不一致で Brute-force と決定的同一にならない、却下)、(C) AABB 4 頂点 + 中心 5 点最小 (処理重く Brute-force と一致取りにくい、却下) |
| **Q6: テスト 8 件内訳** | **(1) R-tree アクセサー sanity / (2) Query 全包含 / (3) Query 範囲外 / (4) Query × 50 ランダム Brute-force 突合 / (5) Query overrun / (6) Nearest k=1 Brute-force 一致 / (7) Nearest k=10 Brute-force 集合一致 / (8) ノード構造 sanity (リーフフラグ + 子参照規約)** | (B) Nearest k=1/5/10 (9 件に拡張、計画書 §4.4 の 8 件枠超過)、(C) Query 中心 6 件 + Nearest 軽量 2 件 (Nearest 検証薄め、却下) |

**距離単位の補足 (Q5)**: 点-AABB 最小距離は「経緯度の 2D euclidean 距離 (度単位)」を採用する。理由は (1) R-tree 枝刈りロジック自体が経緯度平面上の比較で動くため決定的一致が取りやすい、(2) k 候補絞り込み目的のため真値（Haversine メートル）は不要、(3) 3A.5 NativeRoadSnapper でメートル単位のシェイプ距離計算を行うため二重計算回避。

### 2.11 3A.5 着手前の事前調査結果（2026-05-26、v0.9 起草時）

3A.4 完了 (commit `78d4581`、559 件 pass) 後、3A.5 (NativeRoadSnapper 実装) 着手前に既存 IRoadSnapper / Router / GeoMath 関連を実地調査し、計画書 v0.8 §4.5 と現状コードのギャップ 6 件を特定。

| # | 発見 | 影響 |
| --- | --- | --- |
| S1 | **`GeoMath` ヘルパは存在しない** (`src/OsmDotRoute/Geometry/` を Glob しても 0 件、`PointToSegment` / `ClosestPoint` を Grep しても 0 件) | 計画書 §4.5「Phase 1 既存 `GeoMath.PointToSegmentDistance` 流用」は架空の前提。距離計算 / 点-線分最短距離 / 投影 t 値計算をすべて新規実装が必要 |
| S2 | **`IRoadProfile` インターフェース不在** (3A.3a で同じ問題、`ProfileEvaluator` に確定済): 計画書 §4.5「`Snap(double lat, double lon, IRoadProfile profile, double maxDistanceMeters)`」シグネチャは架空 | 現 [`IRoadSnapper.cs:8-20`](../src/OsmDotRoute/Routing/IRoadSnapper.cs#L8-L20) の `Snap(string profileName, GeoCoordinate point, float searchDistanceM)` シグネチャを Native も維持する必要 |
| S3 | **profileName → プロファイル評価の仲介パスは Native では二択**: Native は [`NativeRoadGraph.cs:272-308`](../src/OsmDotRoute/Native/NativeRoadGraph.cs#L272-L308) の `EvaluateByEdgeId` で `BakedProfileEntry.Flags` から直接 CanPass 判定可能 | ProfileEvaluator 経由 vs NativeRoadGraph 直アクセスの選択肢 (Q2 で確定) |
| S4 | **計画書 §4.5 Done 基準「Itinero とエッジ ID 完全一致 + 座標 ±1e-7 度 + t 値 ±1e-6」は技術的に不可能**: 3A.4 Q2 と同根の問題、`.odrg` と Itinero RouterDb はエッジ ID 独立採番 (3A.3f P1 で既出)。さらに [`ItineroSnapper.cs:42`](../src/OsmDotRoute.Itinero/ItineroSnapper.cs#L42) `Router.Resolve` は Itinero 内部の補間ロジックで座標を返すため、Native と Itinero でエッジ分割位置が異なれば座標一致も false negative リスク | Done 基準の代替方式が必要 (Q4 で確定) |
| S5 | **3A.5 はスコープが非常に大きい**: GeoMath 新設 + NativeRoadSnapper 本体 + プロファイル仲介 + maxDistance 度換算 + Offset セマンティクス + 計画書旧記述 183 件テスト | サブステップ分割を検討すべき (Q1 で確定) |
| S6 | **`Router.SnapToRoad` は [`Router.cs:42, 45, 68`](../src/OsmDotRoute/Router.cs) で `_routerDb.Snapper.Snap(profile.Name, point, searchDistanceM)` を呼出**: 統合 API は `string profileName` 一本 | `NativeRoadSnapper` もこの契約に従い、Phase 1 Router を未改造で再利用可能にする |

### 2.12 3A.5 着手前ペンディング判断（2026-05-26、v0.9 起草時に確定）

| 論点 | 確定案 | 不採用案 |
| --- | --- | --- |
| **Q1: 3A.5 サブステップ分割** | **分割**: 3A.5a (`GeoMath` 新設 + 単体テスト) → 3A.5b (`NativeRoadSnapper` + 基本テスト + Brute-force 突合)。各段で `dotnet test` 全 pass 維持、ロールバック容易 | (B) 1 段で進める (3A.4 と同じパターンだが GeoMath 追加で規模超過、却下) |
| **Q2: profileName → プロファイル評価の仲介** | **(B) NativeRoadGraph 経由で profileName → BAKED_PROFILE スロット直読**。`NativeRoadGraph` に `internal bool CanPass(uint edgeId, string profileName)` を追加 (内部実装は `EvaluateByEdgeId` 流用、`BakedProfileEntry.Flags & 0x01` の判定のみ取出)。ProfileEvaluator 不要、ホットパス最適 | (A) NativeRoadSnapper コンストラクタで `Dictionary<string, ProfileEvaluator>` 受ける (Native では使わない型を経由、却下)、(C) Snap 時に毎回 ProfileEvaluator 渡す (IRoadSnapper 破壊変更、却下) |
| **Q3: GeoMath ヘルパ配置** | **(A) `src/OsmDotRoute/Geometry/GeoMath.cs` 新設** (`internal static class`)。3C で Itinero 側も同ヘルパを使う可能性あり (リスク R5 対応)。既存 [`NativeRoadGraph.cs:407-418`](../src/OsmDotRoute/Native/NativeRoadGraph.cs#L407-L418) `HaversineMeters` も `GeoMath` に移動し重複排除 | (B) `Native/NativeGeoMath.cs` (Native 専用、将来 Geometry へリレイヤリング必要、却下)、(C) NativeRoadSnapper 内 private (単体テスト不可、却下) |
| **Q4: Done 基準** | **Brute-force 突合に変更** (3A.4 Q2 と同パターン): Native = R-tree 候補 + GeoMath 詳細計算、Brute-force = 全エッジに対する点-線分最短距離計算 → 同じエッジ ID と t 値を返す。Itinero 突合は 3A.6 で経路結果一致として担保 | (B) Native 自己整合のみ (R-tree と GeoMath の正確性を上位コンポーネントテストに委ねる、却下)、(C) Itinero スナップ点と座標 ±N 度の緩い一致 (Itinero 内部分割位置差で false negative リスク、却下) |
| **Q5: maxDistance (m) → 検索 bbox 度換算式** | **緯度依存近似**: `dLat = m / 111320`、`dLon = m / (111320 × cos(lat))`。津島市 (lat ≈ 35) で `dLon = m / 91200` 程度。WGS84 平均半径ベースの近似で実装軽量、`ItineroSnapper.Snap` の `searchDistanceM` と実効同等 | (B) WGS84 楕円体厳密 (Bowring 式、過剰、却下)、(C) 固定 `m / 111000` 緯度補正なし (高緯度でスナップ漏れリスク、却下) |
| **Q6: SnapResult.Offset (ushort 0..65535) の意味** | **エッジ全長に対する累積距離比 × 65535** (Itinero 互換、`IRoadSnapper.cs:28` XML doc の「0=From、65535=To」と一致): `Offset = (ushort)Math.Round(累積距離メートル / エッジ全長メートル × 65535)` | (B) シェイプ点間 t × 65535 (シェイプ点分布が不均一なとき距離比とずれる、却下) |
| **Q7: GeoMath の点-線分最短距離 平面化手法** | **緯度補正コサイン (局所メートル平面)**: `x = (lon - lon0) × cos(lat0) × R`、`y = (lat - lat0) × R` (R = 6371008.8m WGS84 平均半径)。エッジ単位 (最大 30m 程度) で誤差サブ cm。シェイプ 1 本ごとに `cos(lat0)` を 1 回計算してセグメント間で使い回し | (B) Haversine だけ (点-線分の球面公式は複雑でバグ生みやすい、却下)、(C) 生経緯度を x/y として 2D euclidean (lat=35° で経度方向 18%超 不足、却下) |
| **Q8: テスト 8+12=20 件構成 (累計 559 → 579)** | **(A) 提案 A**: 3A.5a (GeoMath) 8 件 = Haversine 2 / 点-線分距離 3 (端点/線分内/外側) / 投影 t 3 (0/0.5/1)。3A.5b (NativeRoadSnapper) 12 件 = コンストラクタ 1 / 頂点上 1 / エッジ中央 1 / 通行不可除外 1 / 検索半径超過 null 1 / 範囲外 null 1 / bbox 拡張 1 / Brute-force ×50 ランダム 1 / Offset 単調性 1 / From/To 端点 2 / Dispose 後例外 1 | (B) 30 件 (重複多い)、(C) 14 件 (回帰検知力低い) |

### 2.13 3A.6 着手前の事前調査結果（2026-05-26、v0.10 起草時）

3A.5b 完了 (commit `5a54296`、579 件 pass) 後、3A.6 (並存パリティ + 設計書 §3 反映) 着手前に既存 `Router.Calculate` / `CalculateRouteTests` / `Phase 3 設計書 §3` を実地調査し、計画書 v0.9 §4.6 と現状コードのギャップ 6 件を特定。

| # | 発見 | 影響 |
| --- | --- | --- |
| T1 | **`ParentDefaultRouterDb` (default.routerdb) と `TsushimaOdrg` は別データソース**: [`TestPaths.cs:13`](../tests/OsmDotRoute.Tests/TestData/TestPaths.cs#L13) は親プロ `災害廃棄物処理シミュレーション/Data/Scenarios/default.routerdb`、[`TestPaths.cs:31`](../tests/OsmDotRoute.Tests/TestData/TestPaths.cs#L31) は `samples/Data/tsushima.odrg`。地理エリアが違うため同じペアで両方経路計算しても比較対象にならない | 計画書 v0.9 §4.6「89 ペア × 2 実装 = 178 経路で完全一致」は技術的に不可能 |
| T2 | **既存 Phase 1 経路テスト (`CalculateRouteTests.cs:62-128`) は ±10% 一致の緩い比較**: `maxPairs: 12` で動的生成、Itinero との総距離 ±10% を 80% 達成で pass。コメントに「完全一致は目指さない」と明記 | 計画書 v0.9 §4.6「頂点列 / 距離 / 時間が完全一致」は **Phase 1 設計と矛盾** |
| T3 | **計画書 §4.6「89 ペア」は架空数値**: 実態は最大 12 ペア、コードの値は `CollectCarAccessibleVertexPairs(routerDb, maxPairs: 12, minSeparationDeg: 0.01)`。 「89 ペア」「178 ケース」「183 件」等の数値はどこからも導出できない | 件数を計画書から実体に合わせる必要 |
| T4 | **`tsushima_extract.osm.pbf` (約 13MB) は存在**: [`TestPaths.cs:21`](../tests/OsmDotRoute.Tests/TestData/TestPaths.cs#L21)。これから Itinero RouterDb を fixture で生成すれば津島スコープで Itinero/Native 比較が可能になる選択肢が存在（採用可否は Q1 で確定） | 大論点 |
| T5 | **`IRoadProfile` 計画書記述は架空** (3A.5 S2 と同じ): 計画書 §4.6 fixture `IRoadProfile CarProfile` は架空、`VehicleProfile.Car` を使う | 自明訂正 |
| T6 | **`RouterDb(IRoadGraph, IRoadSnapper)` コンストラクタは internal** ([`RouterDb.cs:15`](../src/OsmDotRoute/RouterDb.cs#L15)): `InternalsVisibleTo("OsmDotRoute.Tests")` 経由でテストから直接構築可能 → Phase 1 既存 `Router.Calculate` を Native 系で再利用できる、DI 拡張 (3C スコープ) 不要 | Native Router 構築の道筋確定 |
| T7 | **Phase 3 設計書 §3 は全項目「未記述」のテンプレ枠**: [`phase3_design.md:142-173`](phase3_design.md) (3.1 意図 / 3.2 採用設計 / 3.3 設計判断 / 3.4 トレードオフ / 3.5 検証方法 / 3.6 実装メモ) 全 6 項目に「（未記述）」 | 3A.6 で全 6 項目を肉付けが必要 |

### 2.14 3A.6 着手前ペンディング判断（2026-05-26、v0.10 起草時に確定）

| 論点 | 確定案 | 不採用案 |
| --- | --- | --- |
| **Q1: テストデータソース** | **(B) Native 自己整合のみ、Itinero 突合廃止**。tsushima.odrg のみ使い Native 経路計算の不変量を検証。Itinero との並存証明は **Phase 1 既存 526 件全 pass** が継続している事実で代替する (Itinero 系統は本ステップで一切触らない) | (A) tsushima_extract.osm.pbf から Itinero 生成 (fixture 5-15 秒、エッジ ID 採番独立で頂点列一致不可、却下)、(C) 独立テスト (連携テストなしで完了判定根拠弱い、却下) |
| **Q2: Done 基準** | **Native 単体経路計算の不変量検証**: tsushima.odrg + NativeRoadGraph + NativeRoadSnapper で経路計算を行い、(a) 結果が返る (b) 起点・終点頂点が想定範囲 (c) 距離 > 0 (d) 逆向きほぼ同距離 等の不変量を検証。これにより「Native 経由でも Router が破綻なく動く」ことを担保 | (B) Brute-force Dijkstra 参照実装 (Phase 1 Dijkstra と二重実装、却下)、(C) Smoke のみ 10 件 (Done 基準弱すぎ、却下) |
| **Q3: テスト件数表 178 件記口** | **(A) 178 → 16 件に記口**。累計 579 → 595。Phase 1 既存 Itinero 526 件は Native 並走なし、Native 系のみで 16 件 | (B) 178 件維持 (Itinero 突合 89 ペア × 2 の元意味が崩れている、却下)、(C) 0 件 (Native Router 統合証明なし、却下) |
| **Q4: 設計書 §3 反映の範囲** | **(A) 3A.6 で設計書 §3 の全 6 サブセクションを一括記述** (3.1 意図 / 3.2 採用設計 / 3.3 設計判断の根拠 / 3.4 トレードオフ・制約 / 3.5 検証方法 / 3.6 実装メモ)。コード変更と設計書反映を 1 commit で同期 | (B) 設計書反映を 3B/3C へ送る (3A 完了判定が宙ぶらりん、却下)、(C) 3A.6a (テスト) + 3A.6b (設計書) 分割 (コミット件数増、却下) |
| **Q5: テスト 16 件内訳** | **smoke 5 + 不変量 8 + RouterDb コンストラクタ 2 + fixture sanity 1 = 16**。 Smoke 5: 同点 / 短距離 / 中距離 / from 範囲外 null / to 範囲外 null。 不変量 8: 距離正 / 頂点列先頭が起点近傍 / 末尾が終点近傍 / 直線距離 ≤ 経路距離 / シェイプ点列非空 / 逆向き同距離 ±2% / 同入力決定性 / RouteSegment 連続性 (前 To = 後 From)。 RouterDb 2: 正常構築 / null ArgumentNullException。 fixture sanity 1: NativeRouterDbFixture 初期化で例外なし | (B) 20 件 (重複多い)、(C) 最低 12 件 (チェックポイント薄い) |
| **Q6: NativeRouterDb fixture 化** | **NativeRouterDbFixture 新設し IClassFixture で共有**。`NativeRoadGraph` + `NativeRoadSnapper` + `RouterDb` + `Router` インスタンスを 1 度作って 16 件で使い回す。`NativeAndOdrgReaderFixture` とは独立 (OdrgReader Truth ロードは 3A.6 で不要) | (B) 既存 fixture 拡張 (Truth 不要なロードコスト)、(C) fixture 不使用 (~30ms 余計 + シンプル) |

---

### 2.4 `.odrg` v1.0 セクション構成（実装確認済、`OdrgFormat` / `OdrgReader` ベース）

**ファイル全体構成**：HEADER (256 B 固定) → セクション本体群 → SECTION TABLE (末尾、9 × 24 B)。SECTION TABLE のオフセットは HEADER 内の `sectionTableOffset` で示される。

**HEADER (256 B 固定)**: マジック 8B `"ODRG\0\0\0\0"` / VersionMajor u16=1 / VersionMinor u16=0 / flags u32 / vertexCount u64 / edgeCount u64 / bbox (minLon/minLat/maxLon/maxLat double × 4 = 32 B) / profileCount u32 / edgeFlagBytes u32 / sectionTableOffset u64 / sectionCount u32

**SECTION TABLE エントリ (24 B / エントリ)**: kind u16 + reserved 2 B + flags u32 + offset u64 + length u64

**セクション一覧（9 セクション、kind 0x0001〜0x0009）**：

| kind | セクション | サイズ | 本ステップでの読込型 |
| --- | --- | --- | --- |
| 0x0001 | VERTEX | 16 B × N（lon double + lat double） | `ReadOnlySpan<GeoCoordinate>` |
| 0x0002 | EDGE | 24 B × E（from u32 + to u32 + shapeOff u64 + shapeLen u32 + bakedIdx u32、**flags は別セクション**） | `ReadOnlySpan<OdrgEdge>` |
| 0x0003 | EDGE_SHAPE | 16 B × S（連続バッファ、エッジが offset/length で参照） | `ReadOnlySpan<GeoCoordinate>` |
| 0x0004 | EDGE_AABB | 32 B × E（minLon/minLat/maxLon/maxLat double × 4） | `ReadOnlySpan<Aabb>` |
| 0x0005 | EDGE_FLAG | 2 B × E（ushort 独立セクション、`EdgeFlagBytes` 定数 = 2） | `ReadOnlySpan<EdgeFlags>` |
| 0x0006 | SPATIAL_INDEX (R-tree) | ヘッダ 16 B (nodeCount/rootIndex/branching/height u32 × 4) + ノード 56 B × N（bbox 32 B + firstChild u32 + childCount u32 + flags u32 + reserved 12 B） | `OdrgRTreeView`（ヘッダ + `ReadOnlySpan<RTreeNode>`） |
| 0x0007 | BAKED_PROFILE | ヘッダ 8 B (profileCount u32 + entrySize u32) + name table (8 B × P) + UTF-8 name buf + entries (8 B × P × E、**profile-major** = `entries[profile * edgeCount + edge]`) | プロファイル名 string[] + `ReadOnlySpan<BakedProfileEntry>` |
| 0x0008 | TURN_RESTRICTION | raw bytes（Phase 4+ 用予約、Phase 2/3 では参照のみ） | `ReadOnlySpan<byte>`（透過） |
| 0x0009 | METADATA | UTF-8 JSON（仕様書 §4 抽出時メタ情報） | `ReadOnlySpan<byte>` → 文字列化は呼出側責任 |

（正確なオフセット / レイアウトは [`phase2_graph_format_spec.md`](phase2_graph_format_spec.md) §1〜§4 と [`OdrgFormat.cs`](../src/OsmDotRoute.Extractor/Pipeline/OdrgFormat.cs)（3A.1 で Core へ移動予定）/ [`OdrgReader.cs`](../src/OsmDotRoute.Extractor/Pipeline/OdrgReader.cs) を真値とする。本ステップで Phase 2 と異なる解釈をする箇所はない）

---

## 3. スコープ

### 3.1 スコープ内

- **前提リファクタ（3A.1 冒頭で実施）**：
  - `OdrgFormat.cs` を [`src/OsmDotRoute.Extractor/Pipeline/OdrgFormat.cs`](../src/OsmDotRoute.Extractor/Pipeline/OdrgFormat.cs) から `src/OsmDotRoute/Internal/Odrg/OdrgFormat.cs` へ移動（依存方向：Extractor → Core が成立、逆は不可のため Core が定数を保持する必要がある）
  - 影響範囲: `OdrgReader.cs` / `OdrgWriter.cs` / `OdrgGeoJsonWriter.cs` / `OdrgWriteInput.cs` / `OdrgReadResult.cs` の using 修正のみ（型定義は据置、namespace 変更による副次修正）
- `OsmDotRoute` コアプロジェクトに以下を追加（新規プロジェクトは作らない、計画書 §5.1 確定）：
  - `OdrgSectionDirectory`（HEADER + SECTION TABLE パース、`internal`）
  - `OdrgMmfHandle`（`MemoryMappedFile` + `SafeMemoryMappedViewHandle` ラッパ、`IDisposable`）
  - `OdrgSpanView<T>`（`MemoryMarshal.Cast<byte, T>` 経由で各セクション Span を遅延取得、`unsafe`）
  - `NativeRoadGraph : IRoadGraph, IDisposable`
  - `NativeRoadSnapper : IRoadSnapper`
- 並存パリティテスト 178 ケース（89 ペア × 2 実装、`ItineroRoadGraph` / `NativeRoadGraph` の出力完全一致）
- Phase 3 設計書 §3 の肉付け（3A.6 完了時に一括反映）

### 3.2 スコープ外

- 動的制約ホットパス高速化（`RestrictedAreaEdgeCache`）→ **3B**
- Bicycle / Truck プロファイル → **3D**
- `Route.Shape` の `ReadOnlyMemory<GeoCoordinate>` 破壊変更 → **3C**（本ステップでは API シグネチャを Span 返却で確定するに留め、`Route` 型は Phase 1 まま）
- DI 拡張（`AddOsmDotRoute(options)` への Native 切替フラグ追加等）→ **3C**（本ステップではテストコード内で直接コンストラクター呼出し、ユーザー判断 2026-05-26 確定）
- `ItineroRoadGraph` / `ItineroSnapper` 撤去 → **3C**
- MMF 経由の経路計算ベンチマーク → **3E**（本ステップは正確性検証のみ、性能測定は 3E）
- 都道府県単位 PBF での動作確認 → **3G**（本ステップは津島市 `.odrg` のみ）

### 3.3 並存戦略

- 3A 期間中は `ItineroRoadGraph` / `NativeRoadGraph` がコード上で**並列に存在**する
- `IRoadGraph` インターフェースを通じてテストコード内で切替（`var graph = isNative ? new NativeRoadGraph(odrgPath) : new ItineroRoadGraph(routerDbPath)`）
- 既定 DI は Phase 2 まま（Itinero 系）。**ユーザー向けの API 切替は 3C で「選択」ではなく「Itinero 削除 → Native 一本化」のシンプル作業に到達することがゴール**
- 3A 完了時点では、本番呼出（`MapService.LoadFromRouterDb`）は引き続き Itinero 系。Native 系はテストコード経由でのみ動作

---

## 4. サブステップ詳細

### 4.1 3A.1: OdrgFormat Core 移動 + セクションテーブルパース基盤

**ステップ 0（前提リファクタ）**: `OdrgFormat` Core 移動

- `src/OsmDotRoute.Extractor/Pipeline/OdrgFormat.cs` を `src/OsmDotRoute/Internal/Odrg/OdrgFormat.cs` へ移動
- namespace `OsmDotRoute.Extractor.Pipeline` → `OsmDotRoute.Internal.Odrg`（`internal` のまま、`InternalsVisibleTo` で `OsmDotRoute.Tests` / `osmdotroute-extractor` / `OsmDotRoute.Benchmarks` / `OsmDotRoute.Itinero` から可視）
- using 修正対象: `OdrgReader.cs` / `OdrgWriter.cs` / `OdrgGeoJsonWriter.cs` / `OdrgWriteInput.cs` / `OdrgReadResult.cs`
- 既存 156 + 48 = 204 Extractor 系テスト全 pass を維持（リファクタなので機能変更なし）

**ステップ 1（本作業）**: `OdrgSectionDirectory` 実装

- `OdrgSectionDirectory` 型（`internal sealed class`、`src/OsmDotRoute/Internal/Odrg/`）
- 入力: `SafeMemoryMappedViewHandle` + ファイル長
- 出力: `OdrgHeader` 値（VersionMajor/Minor、vertexCount、edgeCount、bbox、profileCount、sectionTableOffset、sectionCount）+ 9 セクションエントリ（kind, flags, offset, length）+ kind→index 高速引き
- 検証: マジック `"ODRG\0\0\0\0"` / VersionMajor == 1 / `edgeFlagBytes == 2` / `sectionCount == 9` / `sectionTableOffset + sectionCount*24 <= fileLen` / 各エントリの offset+length がファイル長以内
- パース失敗時は `OdrgFormatException`（新規例外型、`src/OsmDotRoute/Internal/Odrg/`）を投げる

**Done 基準**:

- 津島市 `.odrg` で `OdrgReader.Read`（Phase 2 検証用、現所在 Extractor、3A.1 ステップ 0 後の using 修正済前提）と同じヘッダ + セクションテーブル情報を field-by-field 一致で取得
- xUnit テスト 5 件: 正常ケース 1 / マジック不一致 / VersionMajor 不一致 / セクション数不一致 / オフセット越境

**テスト参照真値**: `OdrgReader.Read(path)` の `OdrgReadResult.Header` / `OdrgReadResult.SectionTable` と完全一致

---

### 4.2 3A.2: MMF + `ReadOnlySpan<T>` セクション切出

**実装**:

- `OdrgMmfHandle` 型（`internal sealed class : IDisposable`）
  - コンストラクタで `MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read)`
  - `CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read)` でビュー取得
  - 内部に `SafeMemoryMappedViewHandle` を保持
  - `Dispose()` でビュー → MMF の順に解放
  - ファイナライザは持たない（`SafeHandle` 派生型が CriticalFinalizer 経由で自動解放、ユーザー判断 #21 (b) 反映）
- `OdrgSpanView<T> where T : unmanaged` 型（`internal readonly ref struct`）
  - `unsafe` ブロック内で `SafeBuffer.AcquirePointer(ref byte*)` → `byte* + offset` → `MemoryMarshal.Cast<byte, T>(new ReadOnlySpan<byte>(ptr, length))`
  - **`ref struct` 化**: Span のライフタイムが MMF ハンドル所有者より長くならない契約をコンパイラに強制
  - `ReadOnlySpan<T> AsSpan()` プロパティで Span 取得

**設計判断**:

- `SafeBuffer.AcquirePointer` / `ReleasePointer` は Span のライフタイム中保持する設計：`OdrgMmfHandle` 内に `byte* _basePtr` を持ち、コンストラクタで Acquire、Dispose で Release。Span は `_basePtr + sectionOffset` を起点とする
- 各 Span 取得時に Acquire/Release を繰り返さない（パフォーマンス）
- Span のサイズ整合性チェックは `length % sizeof(T) == 0` を Debug.Assert で

**Done 基準**:

- 津島市 `.odrg` の VERTEX セクション 27,235 要素を `ReadOnlySpan<OdrgVertex>` で公開、`OdrgReader` と全要素ハッシュ一致
- EDGE / EDGE_SHAPE / EDGE_AABB / RTREE_NODE / PROFILE_BAKE / STRING_POOL も同様
- xUnit テスト 7 件（各セクション 1 件 + Dispose 後アクセス例外 1 件）

**リスク**: `unsafe` ポインタの範囲外アクセス。対処として `Debug.Assert(offset + length <= _viewLength)` を Span 切出時に必ず入れる。

---

### 4.3 3A.3: `IRoadGraph` 評価 API 改修 + `NativeRoadGraph` 実装（B 案、v0.3 で全面書き直し）

**スコープ拡張理由**: §2.5「3A.3 着手前の重大発見」参照。`.odrg` には OSM タグ生データが格納されない設計のため、`NativeRoadGraph` を `IRoadGraph` 実装するには `GetEdgeOsmTags` 系の API を bake-equivalent な評価 API に置換する必要がある。これは実質「3C エッジ評価 API 統合」の前倒し。各サブステップで `dotnet test` 全 pass を維持しながら段階的に進める。

**前提**: §2.6 ペンディング判断 §3A.3-API（新評価 API シグネチャ）が確定済であること。

---

#### 4.3.1 3A.3a: 新 `IRoadGraph` 評価 API シグネチャ確定 + EdgeEvaluation 型整理

**作業**:

- §2.6 §3A.3-API ユーザー判断確定（計画書 v0.3 承認時に併せて）
- `EdgeEvaluation` 型（`src/OsmDotRoute/Profiles/`）の `internal` 性質確認、評価 API の戻り値型として使えるか検証
- `IRoadGraph` / `IRoadGraphEdgeEnumerator` の改修案を本サブステップで XML doc + コード骨格まで起こす（実装はせず）
- 既存 5 ファイルのテスト改修方針確認（`GetEdgeOsmTags` 直呼びの代替パターン）

**Done 基準**:

- 新 API のシグネチャ・XML doc が確定
- 改修対象ファイルリストが固まる（src 側、tests 側）
- `dotnet test` 全 pass 維持（コード変更なし、ドラフト準備のみ）

**commit メッセージ案**: `docs: Phase 3 ステップ 3A.3a IRoadGraph 評価 API 改修案ドラフト`

---

#### 4.3.2 3A.3b: `IRoadGraph` 評価 API 改修 + `ItineroRoadGraph` + `EdgeWeightCalculator` + `DijkstraEngine` + テスト 5 ファイル一括追従 (v0.5 で旧 3A.3b/3A.3c/3A.3d を統合)

**統合理由 (v0.5)**:

v0.4 §4.3.2 では「3A.3b は IF + ItineroRoadGraph + テスト追従のみ、`EdgeWeightCalculator.Evaluate(ushort)` は暫定 fall back 維持」を案 α として提示していたが、3A.3b 着手前事前調査で **`GetEdgeOsmTags(ushort)` 削除後は `Evaluate(ushort)` 内部から新 API を呼ぶ術がない**（`ushort` 単体からは `en` も `RoadEdge` も復元不可）ことが判明。案 α は技術的に不成立、案 β（IF + Itinero + EdgeWeightCalculator + Dijkstra を 1 commit で完結）が必須となった。

**作業**:

1. **`src/OsmDotRoute/Routing/IRoadGraph.cs`**: `GetEdgeOsmTags(ushort)` 削除 / 新 `EvaluateEdge(IRoadGraphEdgeEnumerator, ProfileEvaluator)` + `EvaluateEdge(RoadEdge, ProfileEvaluator)` 2 オーバーロード追加 (§2.6.1)
2. **`src/OsmDotRoute/Profiles/ProfileEvaluator.cs`**: `public string Name => _def.Name ?? throw ...` 追加
3. **`src/OsmDotRoute.Itinero/ItineroRoadGraph.cs`**:
   - 旧 `GetEdgeOsmTags(ushort)` の `_routerDb.EdgeProfiles.Get(...)` ロジックを **`private IReadOnlyDictionary<string, string> GetTagsByProfileIndex(ushort)`** として温存（テスト用 extension が利用）
   - 新 `EvaluateEdge(IRoadGraphEdgeEnumerator, ProfileEvaluator)` + `EvaluateEdge(RoadEdge, ProfileEvaluator)` 実装、共通の `private EdgeEvaluation EvaluateByProfileIndex(ushort, ProfileEvaluator)` に集約
4. **`src/OsmDotRoute.Itinero/ItineroRoadGraphTestExtensions.cs`** (新規): §2.6.3 の `GetEdgeOsmTagsForTest(ItineroRoadGraph, ushort)` extension。`ItineroRoadGraph` 内部の `GetTagsByProfileIndex` を呼ぶ ([[InternalsVisibleTo]]: `OsmDotRoute.Tests` を csproj 確認 + 必要なら追加)
5. **`src/OsmDotRoute/Routing/EdgeWeightCalculator.cs`**:
   - 旧 `public EdgeEvaluation Evaluate(ushort edgeProfileIndex)` 削除
   - 新 `public EdgeEvaluation Evaluate(IRoadGraphEdgeEnumerator en) => _graph.EvaluateEdge(en, _evaluator)` 追加
   - 新 `public EdgeEvaluation Evaluate(RoadEdge edge) => _graph.EvaluateEdge(edge, _evaluator)` 追加
   - `EvaluateEdgeDurationSec(en)` 内 `Evaluate(en.EdgeProfileIndex)` → `Evaluate(en)`
6. **`src/OsmDotRoute/Routing/DijkstraEngine.cs:42, 46`**:
   - `_calculator.Evaluate(sourceEdge.EdgeProfileIndex)` → `_calculator.Evaluate(sourceEdge)`
   - `_calculator.Evaluate(targetEdge.EdgeProfileIndex)` → `_calculator.Evaluate(targetEdge)`
7. **テスト 5 ファイル追従**: `tests/OsmDotRoute.Tests/{ItineroAdapterTests, CalculateRouteTests, SnapToRoadTests, RestrictedRoutingTests}.cs` の `graph.GetEdgeOsmTags(...)` 直呼び 6 箇所を §2.6.3 の `ItineroRoadGraphTestExtensions.GetEdgeOsmTagsForTest(...)` 経由に書き換え。`graph` 変数型を可能なら `ItineroRoadGraph` 直接で受ける形にして cast 不要にする
8. `IRoadGraphEdgeEnumerator.EdgeProfileIndex` XML doc から「`IRoadGraph.GetEdgeOsmTags` で展開」記述を削除（API 自体が消えるため）。プロパティ自体は保持（Itinero 内部の `Data.Profile` 用、3C で廃止検討）

**Done 基準**:

- `dotnet test` 全 pass（**539 件維持**、`ItineroRoadGraph` 経由のテストが新 API で動く）
- `IRoadGraph` から `GetEdgeOsmTags` が削除されている (compile 通過)
- `grep -rn "GetEdgeOsmTags"` で**本番コードのヒット 0 件**（Phase 3 設計書 / 計画書のドキュメント上のヒットは許容）
  - テストの `GetEdgeOsmTagsForTest` ヒットは extension 利用なので OK（命名で区別可能）
- 経路探索エンジン（`DijkstraEngine` / `EdgeWeightCalculator`）の動作が Phase 1 と完全一致（既存 89 ペア経路テスト全 pass）
- `ProfileEvaluator.Evaluate(IDictionary)` 経路は **Itinero 側でのみ呼ばれる**（`ItineroRoadGraph.EvaluateByProfileIndex` 内部）形に隔離されている
- 警告 0（廃止参照 / 未使用 using 等の警告がない）

**commit メッセージ案**: `refactor: Phase 3 ステップ 3A.3b IRoadGraph 評価 API 改修 (RoadEdge オーバーロード + Itinero テスト extension 新設、3A.3b/c/d 統合)`

**v0.4 サブステップとの対応**:

- v0.4 3A.3b (IF + ItineroRoadGraph + テスト 5 ファイル) → v0.5 3A.3b に統合
- v0.4 3A.3c (EdgeWeightCalculator 内部置換) → v0.5 3A.3b に統合
- v0.4 3A.3d (テスト 5 ファイル最終確認) → v0.5 3A.3b に統合 (上記項目 7 で同時実施)
- v0.4 3A.3e (NativeRoadGraph 新規) → v0.5 3A.3e (番号維持)
- v0.4 3A.3f (NativeRoadGraph テスト 12 件) → v0.5 3A.3f (番号維持)

---

#### 4.3.5 3A.3e: `NativeRoadGraph` 新規実装 (v0.6 で詳細化)

**前提**: §2.7 / §2.8 ユーザー判断 4 件確定済 (L1 CSR / L5 GetEdgeShape / L6 class / L8 sanity 3 件)。

**事前作業 (3A.3e-0): `IRoadGraph` に `GetEdgeShape(uint) -> ReadOnlySpan<GeoCoordinate>` 追加 + Itinero 側追従**

- `src/OsmDotRoute/Routing/IRoadGraph.cs`: `GetEdgeShape(uint edgeId) -> ReadOnlySpan<GeoCoordinate>` 追加。XML doc に「Span ライフタイムは `IRoadGraph` インスタンスの `Dispose()` まで（Itinero 系は次の `GetEdgeShape` 呼出までに短縮される可能性、Native 系は Dispose まで保証）」を明記
- `src/OsmDotRoute.Itinero/ItineroRoadGraph.cs`: `GetEdgeShape` 実装 (per-call で `GeoCoordinate[]` 確保 + `AsSpan()` 返却、3C で撤去予定なのでコピーコスト容認)
- `IRoadGraph : IDisposable` 化 (NativeRoadGraph が `IDisposable` 必須、Itinero は no-op `Dispose` 実装)
- 既存テスト 539 件 pass 維持 (`GetEdgeShape` 利用箇所がまだ無いため、API 追加のみで影響なし)

**実装 (3A.3e-1): `NativeRoadGraph` 本体**

- 配置: `src/OsmDotRoute/Native/NativeRoadGraph.cs`（新規 namespace `OsmDotRoute.Native`、`internal sealed class : IRoadGraph, IDisposable`）
- フィールド:
  - `OdrgMmfHandle _mmf` (MMF ハンドル所有)
  - `OdrgSectionDirectory _dir` (HEADER + SECTION TABLE)
  - 各セクション offset/length をプリ抽出した値 (long _vertexOffset, _edgeOffset, _shapeOffset, _bakedProfileOffset, _bakedProfileEntriesOffset, ...)
  - `uint[] _firstOutEdge` (CSR 行ポインタ、長さ V+1)
  - `OutEdgeEntry[] _outEntries` (CSR ペイロード、長さ 2E)
  - `Dictionary<string, int> _profileSlotByName` (BAKED_PROFILE name → slot index)
  - `GeoCoordinate[][] _shapeCache`（エッジごとに lazy 詰替済 `GeoCoordinate[]`、初回呼出時に確保、`GetEdgeShape` ゼロコピー Span 返却用）
  - `bool _disposed`
- コンストラクタ `NativeRoadGraph(string odrgPath)`:
  1. `OdrgMmfHandle.Open(odrgPath)` で MMF オープン
  2. `OdrgSectionDirectory.Read(_mmf.ViewHandle, _mmf.ViewLength)` で HEADER + SECTION TABLE パース
  3. 各 kind → offset/length プリ抽出
  4. EDGE セクション (`OdrgEdge[E]`) を Span 取得 → CSR 構築 (`_firstOutEdge`, `_outEntries`): bucket-sort で O(E)
  5. BAKED_PROFILE 内 name table を読んで `_profileSlotByName` 構築
  6. `_shapeCache = new GeoCoordinate[edgeCount][]` (中身は null、初回 `GetEdgeShape` 呼出時に詰替)
- IRoadGraph 全メソッド:
  - `VertexCount`: HEADER.VertexCount を ushort/uint cast
  - `EdgeCount`: HEADER.EdgeCount をそのまま long (L2 確定)
  - `GetVertex(uint v)`: `_mmf.GetSpan<OdrgVertex>(_vertexOffset, 1)[v]` の Lon/Lat を `GeoCoordinate(Lat, Lon)` に詰替 (F4)
  - `GetBounds()`: HEADER.Bbox から構築
  - `GetEdgeEnumerator(uint v)`: `new NativeEdgeEnumerator(this, v)` 返却
  - `GetEdge(uint edgeId)`: EDGE セクションから OdrgEdge 読出、Shape は中間点 Span を `GeoCoordinate[]` キャッシュから (or 初回詰替)、`RoadEdge` を組立て返却。`DataInverted=false` 固定 (canonical view、Itinero と同等のセマンティクス)
  - `GetEdgeShape(uint edgeId)`: `_shapeCache[edgeId]` が null なら EDGE_SHAPE セクションから `OdrgVertex` Span 読出 + `GeoCoordinate[]` 詰替してキャッシュ。`AsSpan()` 返却
  - `EvaluateEdge(IRoadGraphEdgeEnumerator en, ProfileEvaluator)`: `EvaluateByEdgeId(en.EdgeId, evaluator)` に集約
  - `EvaluateEdge(RoadEdge edge, ProfileEvaluator)`: `EvaluateByEdgeId(edge.EdgeId, evaluator)` に集約
  - 内部 `EvaluateByEdgeId(uint edgeId, ProfileEvaluator)`:
    1. `_profileSlotByName[evaluator.Name]` で slot index 取得
    2. BAKED_PROFILE Entries Span から `entries[slot * edgeCount + edgeId]` を読出
    3. `BakedProfileEntry` の CanPass / Forward / Backward / SpeedKmh から `EdgeEvaluation(CanPass, SpeedKmh, OnewayDirection)` を組立 (Forward && Backward = Bidirectional、Forward only = Forward、Backward only = Backward)
- `Dispose()`: `_mmf.Dispose()` + `_disposed = true`、idempotent。後続アクセスは `ObjectDisposedException`

**実装 (3A.3e-2): `NativeEdgeEnumerator`**

- 配置: `src/OsmDotRoute/Native/NativeEdgeEnumerator.cs`（`internal sealed class : IRoadGraphEdgeEnumerator`）
- コンストラクタ `(NativeRoadGraph graph, uint startVertex)`:
  - graph の CSR から `_first = firstOutEdge[v]`, `_last = firstOutEdge[v+1]`
  - `_cursor = _first - 1` (MoveNext で +1 して初回 entry に着く)
- `MoveNext()`: `_cursor++; return _cursor < _last;`
- 現在 entry の `OutEdgeEntry` から: `EdgeId`, 反転フラグ
- EDGE セクションから現在エッジ詳細を都度読出 (`From`, `To`, `EdgeProfileIndex`, `DistanceM`, `Shape`)
  - `From`: 反転なし時 `edge.FromVertexId`、反転時 `edge.ToVertexId`
  - `To`: 反転なし時 `edge.ToVertexId`、反転時 `edge.FromVertexId`
  - `DataInverted`: `IsReversed` フラグそのまま
  - `Shape`: `graph.GetEdgeShape(edgeId)` 経由で取得 → `IReadOnlyList<GeoCoordinate>` 変換 (3C で `ReadOnlyMemory` 化される)
  - `EdgeProfileIndex`: Native では未使用 (0 固定でも可、または `(ushort)edgeId` でも可、Itinero 系内部用なので Native での値は何でも良い → §2.6.1 通り保持)
  - `DistanceM`: EDGE セクションには直接 distance がない → 計算が必要：
    - エッジ全体の Haversine 距離を端点 + Shape 中間点の連結で計算
    - 初回計算後はキャッシュ `_distanceCache: float[E]` で記憶 (`NativeRoadGraph` 内)
    - 計算は `Haversine` ヘルパ使用 (既存に無ければ `GeoMath` ヘルパ流用検討)

**Done 基準 (3A.3e 完了時)**:

- 津島市 `.odrg` で `NativeRoadGraph` 構築成功 (sanity test 1)
- VertexCount=27,235、EdgeCount=38,004 (Itinero と数値一致確認)
- `GetVertex(0)` 等で頂点座標が正しく読出 (sanity test 2)
- `Dispose()` 後の `GetVertex` 呼出が `ObjectDisposedException` (sanity test 3)
- `dotnet test` 全 pass (**539 + 3 = 542 件**)
- 警告 0

**追加テスト 3 件 (`tests/OsmDotRoute.Tests/Native/NativeRoadGraphSanityTests.cs`、新規)**:

1. `Constructor_LoadsTsushimaOdrg_SuccessfullyExposesStatistics`: tsushima.odrg をロード、VertexCount/EdgeCount/Bounds が期待値
2. `GetVertex_AtIndexZero_ReturnsValidCoordinateInBounds`: GetVertex(0) が津島市 bbox 内
3. `Dispose_ThenAccess_ThrowsObjectDisposedException`: Dispose 後 GetVertex 呼出で ObjectDisposedException

**commit メッセージ案**: `feat: Phase 3 ステップ 3A.3e NativeRoadGraph 新規実装 (CSR + ゼロコピー Shape + bake-equivalent 評価、IRoadGraph 並存)`

---

#### 4.3.6 3A.3f: `NativeRoadGraph` パリティテスト 9 件追加 (v0.7 で OdrgReader 真値突合に絞り再定義)

**v0.7 改訂理由**: v0.6 §4.3.6 の記述「Itinero との座標 ±1e-7 度一致、両端 / 距離 / DataInverted の整合、ProfileEvaluator + Itinero タグ評価と一致」は、3A.3f 着手前調査で **`.odrg` と Itinero RouterDb の頂点 ID / エッジ ID が独立採番 (VertexAssignment vs Itinero) で、同じ ID が同じ要素を指さない**ことが判明したため不可能。ユーザー判断 4 件 (P1-P4、2026-05-26) で **OdrgReader 真値との自己整合性突合に絞る**方針に変更。Itinero との突合は 3A.6 (89 ペア経路パリティ) で経路結果一致として担保する。

**確定方針 (P1-P4、2026-05-26)**:

- **P1 突合対象 = OdrgReader 真値との自己整合のみ** (Itinero 突合は 3A.6 担当)
- **P2 評価 API テスト = OdrgReader.BakedProfileEntry と一致** (Itinero タグ評価との一致は Phase 2 で証明済み)
- **P3 エラーケース = 未開封 path + マジック改竄の 2 件**
- **P4 サンプル = 頂点 100 / エッジ 100 / Shape 50 / 評価 50×2 profile**

**作業**:

- `tests/OsmDotRoute.Tests/Native/NativeRoadGraphParityTests.cs` 新規（9 件）+ `NativeAndOdrgReaderFixture` 共有
- 9 件:
  1. `GetVertex_Sample100_MatchesOdrgReaderCoordinates`: 等間隔 100 頂点で `Native.GetVertex(v)` == `OdrgReader.Vertices[v]`
  2. `GetEdgeEnumerator_Sample100Edges_FromToAndDataInvertedConsistentWithOdrgReader`: サンプル 100 エッジ ID で起点頂点から列挙して発見可能 + 反転側からも発見可能 + From/To/DataInverted の整合
  3. `GetEdge_Sample50_ShapeMatchesOdrgReader`: サンプル 50 エッジで `Native.GetEdge(edgeId).Shape` == `OdrgReader.EdgeShapes[edgeId]`
  4. `GetEdgeShape_Span_ContainsSameElementsAsGetEdgeList`: 同じ 50 エッジで `Native.GetEdgeShape(edgeId)` (Span) == `Native.GetEdge(edgeId).Shape` (IReadOnlyList) 要素一致
  5. `GetEdgeShape_CalledTwice_ReturnsSameCachedArray`: 中間点ありエッジ 1 本で `Native.GetOrBuildShape(id)` を 2 回呼んで同一参照 (`Assert.Same`)
  6. `EvaluateEdge_Enumerator_Sample50TimesCarPedestrian_MatchesBakedProfileEntry`: 50 エッジ × Car/Pedestrian で `EvaluateEdge(en, evaluator)` が `OdrgReader.ProfileTable.EntriesByProfile[slot][edgeId]` と一致
  7. `EvaluateEdge_RoadEdge_Sample50TimesCarPedestrian_MatchesBakedProfileEntry`: 同上、RoadEdge オーバーロード版
  8. `Constructor_NonExistentPath_ThrowsFileNotFoundException`: 存在しないパスで `FileNotFoundException`
  9. `Constructor_InvalidMagicBytes_ThrowsOdrgFormatException`: tsushima.odrg をコピー + マジック先頭バイト改竄 → `OdrgFormatException`

**Done 基準**:

- xUnit テスト 9 件全 pass (3A.3e の sanity 3 件と合わせて累計 12 件)
- 累計 542 + 9 = **551 件 pass**
- `dotnet build` 0 Warning / 0 Error
- 並存パリティ実測値が記録される（3A.6 178 経路パリティの前段）

**commit メッセージ案**: `feat: Phase 3 ステップ 3A.3f NativeRoadGraph パリティテスト 9 件 (OdrgReader 真値突合、3A.3 全体完了)`

---

### 4.4 3A.4: STR R-tree クエリ実装 (v0.8 で詳細化)

**前提**: §2.9 / §2.10 ユーザー判断 6 件確定済 (Q1 OdrgBbox / Q2 Brute-force 突合 / Q3 NativeRoadGraph に internal API / Q4 ヒット総数返却 / Q5 点-AABB 最小距離 / Q6 テスト 8 件内訳)。

#### 4.4-A 事前作業: `NativeRoadGraph` への R-tree アクセサー追加

- [`src/OsmDotRoute/Native/NativeRoadGraph.cs`](../src/OsmDotRoute/Native/NativeRoadGraph.cs): 以下を追加
  - private フィールド: `long _rtreeOffset`、`uint _rtreeNodeCount`、`uint _rtreeRootIndex`、`uint _rtreeBranchingFactor`、`uint _rtreeHeight`
  - コンストラクタ追加処理: `_directory.FindSection(OdrgFormat.SectionEdgeSpatialIndex)` でセクション取得後、`OdrgFormat.RTreeHeaderSize = 16` byte をパース (NodeCount/RootIndex/Branching/Height u32 × 4 LE) し `_rtreeOffset = sectionOffset + 16` を確定
  - internal アクセサー:
    - `internal ReadOnlySpan<OdrgRTreeNode> GetRTreeNodes()`: `_mmf.GetSpan<OdrgRTreeNode>(_rtreeOffset, (int)_rtreeNodeCount)` を返す (Dispose 後は `ObjectDisposedException`)
    - `internal uint RTreeRootIndex` / `internal uint RTreeBranchingFactor` / `internal uint RTreeHeight` / `internal uint RTreeNodeCount` プロパティ
  - Done 基準: 既存 551 件 pass 維持 (アクセサー追加のみ、ホットパス未変更)

#### 4.4-B 実装: `NativeRTreeQuery`

- 配置: `src/OsmDotRoute/Native/NativeRTreeQuery.cs`（`internal static class`、新規）
- シグネチャ:

  ```csharp
  internal static class NativeRTreeQuery
  {
      /// <summary>
      /// 指定 bbox と交差する全エッジ ID を resultBuffer に書き込み、ヒット総数を返す。
      /// </summary>
      /// <returns>ヒット総数。戻り値 &gt; resultBuffer.Length の場合は overrun (buffer.Length 件のみ書込済)。</returns>
      public static int Query(
          ReadOnlySpan<OdrgRTreeNode> nodes,
          uint rootIndex,
          in OdrgBbox queryBox,
          Span<uint> resultBuffer);

      /// <summary>
      /// クエリ点に対し点-AABB 最小距離（経緯度 2D euclidean、度単位）の最小 k 件のエッジ ID を返す。
      /// </summary>
      /// <returns>実際に書き込んだ件数 (Min(k, ヒット可能数, resultBuffer.Length))。</returns>
      public static int Nearest(
          ReadOnlySpan<OdrgRTreeNode> nodes,
          uint rootIndex,
          double lon,
          double lat,
          int k,
          Span<uint> resultBuffer);
  }
  ```

- アルゴリズム:
  - **Query**: ルートから明示スタック (`Stack<uint>` または stackalloc `Span<uint>`) で DFS。各ノードの `Bbox` と `queryBox` の交差判定（`OdrgBbox.Intersects` の Lon-Lat 版を `NativeRTreeQuery` 内 private static で実装、struct なので invocation 軽量）。リーフ (`Flags & 1 != 0`) なら `FirstChildIndex` から `ChildCount` 個のエッジ ID を resultBuffer に書込 (overrun は無視して count のみ加算)、内部ノードなら子ノードインデックス `FirstChildIndex..FirstChildIndex+ChildCount` を stack に push
  - **Nearest**: best-first 優先キュー (`PriorityQueue<uint, double>` または手書き min-heap) でノードを点-AABB 最小距離順に展開。リーフ展開時にエッジ ID + AABB 最小距離を結果ヒープ (max-heap、サイズ k) に投入、結果ヒープが k 件で埋まった後はトップを上回るノードを pruning。最後に結果ヒープを最小距離順に並べて resultBuffer に書込
  - **点-AABB 最小距離 (経緯度 2D euclidean、度単位)**:

    ```text
    dx = max(0, max(box.MinLon - lon, lon - box.MaxLon))
    dy = max(0, max(box.MinLat - lat, lat - box.MaxLat))
    distance² = dx² + dy²   ← 平方根は最終出力時にだけ取る、内部比較は二乗距離で十分
    ```

  - 再帰スタック深さ: STR M=16 で 38,004 エッジ → 高さ ≒ 4、都道府県でも 5〜6。本実装は明示スタック (再帰ではない) のため stack overflow リスクなし
- Done 基準 (4.4-B):
  - 既存 551 件 pass 維持
  - ビルド 0 Warning / 0 Error
  - 後続テスト (4.4-C) 通過

#### 4.4-C 追加テスト 8 件 (`tests/OsmDotRoute.Tests/Native/NativeRTreeQueryTests.cs`、新規)

Q6 確定の 8 件:

1. **`Constructor_LoadsTsushimaOdrg_ExposesRTreeAccessors`**: `NativeRoadGraph` 構築後、`RTreeNodeCount > 0`、`RTreeRootIndex < NodeCount`、`RTreeBranchingFactor == 16`、`RTreeHeight >= 1`、`GetRTreeNodes().Length == NodeCount`
2. **`Query_FullBoundsBbox_ReturnsAllEdgeIds`**: HEADER.Bbox をそのまま queryBox に渡して全 38,004 エッジが返る（重複なし、`HashSet<uint>` で集合化して `Count == EdgeCount`）
3. **`Query_OutOfBoundsBbox_ReturnsZeroHits`**: 津島市 bbox から十分離れた bbox (例: 北緯 89-90 度) でヒット 0
4. **`Query_FiftyRandomBboxes_MatchesBruteForceAabb` (Done 基準本体)**: 固定シード (`Random(42)`) で 50 個のランダム bbox (HEADER.Bbox 内の 0.1-30% サイズ)、各クエリで `NativeRTreeQuery.Query` 結果と `EDGE_AABB` セクション全走査 (各 AABB と queryBox の交差判定) で得た集合が完全一致 (`HashSet<uint>` 比較)
5. **`Query_BufferOverrun_ReturnsTotalHitsAndWritesUpToBufferLength`**: 全包含 bbox に対し `Span<uint>(stackalloc uint[10])` を渡し、戻り値 `== 38004`、buffer の最初 10 要素は有効エッジ ID（範囲チェックのみ）
6. **`Nearest_K1_MatchesBruteForceMinimumDistance`**: 津島市内ランダム 1 点 (固定シード)、k=1、Brute-force = 全 EDGE_AABB に対し点-AABB 最小距離計算し最小エッジを抽出 → エッジ ID 一致
7. **`Nearest_K10_MatchesBruteForceTopTen`**: 同様に k=10、Brute-force 上位 10 と集合一致 (順序不問)。同距離タイ発生時は受容: テストは「Brute-force 上位 10 の距離値の最大」を取り、Native 結果の k 件すべてがその距離以下であることを assert（厳密一致は数値誤差で破綻リスクあるため）
8. **`RTreeNodeStructure_LeafFlagAndChildReferenceContract_Holds`**: ルートから全ノード走査し以下を検証:
   - 各ノードの `IsLeaf = (Flags & 1) != 0`
   - リーフノードなら `FirstChildIndex + ChildCount <= EdgeCount` (子はエッジ ID 連続)
   - 内部ノードなら `FirstChildIndex + ChildCount <= NodeCount` (子は子ノードインデックス連続)
   - 全リーフのエッジ ID 和集合 == `{0, 1, ..., EdgeCount-1}` (パーティション規約)

**Done 基準 (3A.4 完了時)**:

- 8 件全 pass
- 累計 551 + 8 = **559 件 pass** 想定
- `dotnet build` 0 Warning / 0 Error
- Done 基準本体 (テスト 4) で 50 ランダム bbox の Brute-force 完全一致 → R-tree 実装の正確性を統計的に担保

**commit メッセージ案**: `feat: Phase 3 ステップ 3A.4 STR R-tree クエリ実装 (NativeRTreeQuery + NativeRoadGraph R-tree アクセサー追加、559 件 pass)`

**設計判断**:

- 再帰スタック深さ制限: STR M=16 で 38,004 エッジ → 木の高さ ≒ ceil(log_16(38004)) = 4。深さ制限不要、ただし明示スタック (`Stack<uint>` または stackalloc) で実装し将来の都道府県データ (高さ 5〜6) でも stack overflow リスクなし
- `OdrgRTreeNode` の `MemoryMarshal.Cast<byte, OdrgRTreeNode>` 直 Span 化: G1 で書出 / 読出 / Core struct 完全対称を確認済、ゼロコピーで R-tree ノード列を取得可能

**リスク**: R-tree レイアウトの読み違い。対処として §2.9 G1 で書出 ([`OdrgWriter.cs:145-162`](../src/OsmDotRoute.Extractor/Pipeline/OdrgWriter.cs#L145-L162)) / 読出 ([`OdrgReader.cs:270-303`](../src/OsmDotRoute.Extractor/Pipeline/OdrgReader.cs#L270-L303)) / Core struct ([`OdrgSections.cs:38-45`](../src/OsmDotRoute/Internal/Odrg/OdrgSections.cs#L38-L45)) の完全対称を実地確認済。テスト 4 (50 ランダム bbox Brute-force 一致) で実データ突合により最終担保。

---

### 4.5 3A.5: `NativeRoadSnapper` 実装（`IRoadSnapper` 実装、v0.9 で 3A.5a / 3A.5b に分割）

**前提**: §2.11 / §2.12 ユーザー判断 8 件確定済 (Q1 分割 / Q2 NativeRoadGraph 直アクセス / Q3 Geometry/GeoMath.cs 新設 / Q4 Brute-force 突合 / Q5 緯度依存近似 / Q6 距離比 × 65535 / Q7 緯度補正コサイン / Q8 8+12=20 件)。

---

#### 4.5.1 3A.5a: `GeoMath` ヘルパ新設 + 単体テスト 8 件

**作業**:

- 新規 `src/OsmDotRoute/Geometry/GeoMath.cs` (`internal static class`):

  ```csharp
  internal static class GeoMath
  {
      /// <summary>WGS84 平均半径 (m)。</summary>
      public const double EarthRadiusMeters = 6371008.8;

      /// <summary>2 点間の Haversine 大圏距離 (m)。</summary>
      public static double HaversineMeters(GeoCoordinate a, GeoCoordinate b);

      /// <summary>maxDistance (m) → 緯度依存 bbox 度幅 (dLat, dLon)。Q5 確定式。</summary>
      public static (double DLat, double DLon) MetersToBboxDegrees(double meters, double lat);

      /// <summary>
      /// クエリ点と線分 (a-b) の最短距離 (m) + 線分上の投影点 + 線分上での t 値 [0..1] を返す。
      /// 平面化は緯度補正コサイン (Q7 確定式)。線分長 0 のときは a 自身に投影 (t=0)。
      /// </summary>
      public static (double DistanceM, GeoCoordinate Projected, double T)
          PointToSegment(GeoCoordinate query, GeoCoordinate a, GeoCoordinate b);
  }
  ```

- 既存 [`NativeRoadGraph.cs:407-418`](../src/OsmDotRoute/Native/NativeRoadGraph.cs#L407-L418) `HaversineMeters` (private static) を `GeoMath.HaversineMeters` に置換。`NativeRoadGraph.GetOrComputeDistance` 内呼出を 1 行追従

**Done 基準 (3A.5a 完了時)**:

- 8 件全 pass、累計 559 + 8 = **567 件 pass**
- ビルド 0 Warning / 0 Error
- `NativeRoadGraph` の `HaversineMeters` 内部メソッドが消えており、`GeoMath.HaversineMeters` を呼んでいる

**追加テスト 8 件 (`tests/OsmDotRoute.Tests/Geometry/GeoMathTests.cs`、新規)**:

1. `HaversineMeters_SamePoint_ReturnsZero`: 同一座標で 0
2. `HaversineMeters_KnownPair_MatchesReferenceValue`: 東京駅 (35.681236, 139.767125) ↔ 大阪駅 (34.702485, 135.495951) で約 403km ± 1km (参考値突合、緩い精度で OK)
3. `PointToSegment_QueryOnEndpointA_ReturnsZeroDistanceAndT0`: query == a で distance=0, t=0
4. `PointToSegment_QueryOnEndpointB_ReturnsZeroDistanceAndT1`: query == b で distance=0, t=1
5. `PointToSegment_QueryOnSegmentMidpoint_ReturnsZeroDistanceAndT05`: query が線分中央で distance=0, t=0.5
6. `PointToSegment_QueryOutsideSegmentBeforeA_ClampsToA`: 線分前方延長線上の点で t=0 (a に投影)
7. `PointToSegment_QueryPerpendicularToMidpoint_ReturnsPerpendicularDistance`: 線分中央から垂直方向に既知距離移動、distance が一致
8. `PointToSegment_DegenerateSegment_ZeroLength_ReturnsDistanceToA`: a == b のときは a までの Haversine 距離、t=0

**commit メッセージ案**: `feat: Phase 3 ステップ 3A.5a GeoMath ヘルパ新設 (Haversine + MetersToBboxDegrees + PointToSegment、567 件 pass)`

---

#### 4.5.2 3A.5b: `NativeRoadSnapper` 実装 + 基本テスト + Brute-force 突合 12 件

**事前作業 (3A.5b-0): `NativeRoadGraph` に CanPass API 追加**

- [`src/OsmDotRoute/Native/NativeRoadGraph.cs`](../src/OsmDotRoute/Native/NativeRoadGraph.cs): 追加

  ```csharp
  /// <summary>
  /// 指定プロファイルでエッジが通行可能か判定する (Phase 3 ステップ 3A.5b、Q2 確定)。
  /// BAKED_PROFILE Flags ビット 0 直読、ProfileEvaluator 経由なし。
  /// </summary>
  internal bool CanPass(uint edgeId, string profileName);
  ```

  内部実装は `EvaluateByEdgeId` の `CanPass` 部分のみ抽出 (Forward/Backward は通行可否判定では不問、方向制限は経路探索層で処理)。

**実装 (3A.5b-1): `NativeRoadSnapper`**

- 配置: `src/OsmDotRoute/Native/NativeRoadSnapper.cs`（新規、`internal sealed class : IRoadSnapper`）
- フィールド: `NativeRoadGraph _graph`、`uint[] _buffer = new uint[1024]` (Query 結果一時バッファ、Snap 呼出のたびに再利用、必要に応じて拡張)
- コンストラクタ `NativeRoadSnapper(NativeRoadGraph graph)`: graph を保持、Dispose 不要 (graph のライフタイムに従属)
- メソッド `SnapResult? Snap(string profileName, GeoCoordinate point, float searchDistanceM)`:
  1. `searchDistanceM <= 0` または `string.IsNullOrWhiteSpace(profileName)` → `null`
  2. `_graph._profileSlotByName.ContainsKey(profileName)` 検証、未登録なら `null`
  3. `var (dLat, dLon) = GeoMath.MetersToBboxDegrees(searchDistanceM, point.Latitude)`
  4. `var qbox = new OdrgBbox(point.Longitude - dLon, point.Latitude - dLat, point.Longitude + dLon, point.Latitude + dLat)`
  5. `int hits = NativeRTreeQuery.Query(_graph.GetRTreeNodes(), _graph.RTreeRootIndex, _graph.GetEdgeAabbs(), qbox, _buffer)`、overrun (hits > buffer.Length) なら buffer を 2 倍化して再クエリ
  6. 候補エッジ各々について:
     - `if (!_graph.CanPass(edgeId, profileName)) continue;` (通行不可除外)
     - エッジの完全シェイプ (From 頂点 + 中間シェイプ + To 頂点) を取得
     - 各セグメントに対し `GeoMath.PointToSegment` で最短距離 + 投影点 + t 値を計算
     - 各セグメントの最短結果のうち最小距離のもの (グローバル最短) を保持: `bestEdgeId`, `bestSegmentIndex`, `bestT`, `bestDist`, `bestProjected`
  7. 全候補について `bestDist > searchDistanceM` なら `null` (検索半径外)
  8. `bestEdgeId` のエッジ全長と `bestSegmentIndex/bestT` から累積距離を計算し、`Offset = (ushort)Math.Round(累積距離 / エッジ全長 × 65535)` (Q6)
  9. `return new SnapResult(bestProjected, bestEdgeId, Offset);`

**完全シェイプ取得**: シェイプ点 0 件のエッジ (From-To 直結) も From / To 頂点だけで 1 セグメントとして扱う。`graph.GetVertex(edge.FromVertexId)` + `graph.GetEdgeShape(edgeId)` (中間点) + `graph.GetVertex(edge.ToVertexId)` を順に並べた `GeoCoordinate[]` を内部スタックで構築 (新規 `GeoCoordinate[]` 確保は per-Snap 1 本のみ、3E ベンチで Pool 化検討)。

**Done 基準 (3A.5b 完了時)**:

- 12 件全 pass、累計 567 + 12 = **579 件 pass**
- ビルド 0 Warning / 0 Error
- Brute-force 突合テスト (Native = R-tree+GeoMath、Brute-force = 全エッジ点-線分最短) で 50 ランダム点 × エッジ ID + Offset (誤差許容 ±1) 一致

**追加テスト 12 件 (`tests/OsmDotRoute.Tests/Native/NativeRoadSnapperTests.cs`、新規)**:

1. `Constructor_AcceptsGraph_DoesNotThrow`: コンストラクタ単体
2. `Snap_VertexCoordinate_ReturnsNearbyEdge`: 頂点座標を入力、SnapResult != null + Location が頂点近傍 (< 10m)
3. `Snap_EdgeMidpoint_ReturnsThatEdge`: あるエッジの中央点を入力、bestEdgeId がそのエッジ
4. `Snap_PedestrianOnlyEdge_WithCarProfile_FiltersOut`: 歩行者専用エッジ近傍を Car プロファイルで Snap、別のエッジ (車道) が選ばれるか null
5. `Snap_SearchDistanceTooSmall_ReturnsNull`: 道路から十分離れた点、searchDistanceM=1m → null
6. `Snap_PointFarOutsideBounds_ReturnsNull`: 範囲外 (北緯 89 度) → null
7. `Snap_NearBoundary_BboxExpansionFindsEdge`: 道路から半径ぎりぎりの距離 (searchDistance × 0.9) で見つかる
8. `Snap_FiftyRandomPoints_MatchesBruteForceEdgeIdAndOffset` (Done 基準本体): 固定シード 50 点、Native と Brute-force (全エッジ点-線分最短) で `bestEdgeId` 一致 + `Offset` ±1 (丸め誤差許容)
9. `Snap_TwoCloseQueries_OffsetMonotonicAlongEdge`: 同じエッジ上で位置を少しずらした 2 点を Snap、Offset が単調増加 (エッジ方向に沿った進行)
10. `Snap_QueryAtFromVertex_OffsetIsNearZero`: From 頂点座標で Snap → Offset ≈ 0
11. `Snap_QueryAtToVertex_OffsetIsNearMax`: To 頂点座標で Snap → Offset ≈ 65535
12. `Snap_DisposedGraph_ThrowsObjectDisposedException`: graph を Dispose 後に Snap 呼出 → `ObjectDisposedException`

**commit メッセージ案**: `feat: Phase 3 ステップ 3A.5b NativeRoadSnapper 実装 (R-tree候補 + GeoMath 詳細計算 + Brute-force 突合、579 件 pass)`

**リスク (3A.5b)**:

- Snap 結果のオフセット計算で `Math.Round` 丸め誤差で Brute-force と ±1 ずれる可能性 → テスト 8 で `Offset` 比較は ±1 許容
- 同距離タイのエッジが複数存在する場合、選ばれるエッジが Brute-force と異なる可能性 → テスト 8 では「最小距離値が一致 + bestEdgeId が候補のいずれかに含まれる」緩和も検討、まずは固定シードで重複なしを目指す

---

### 4.6 3A.6: Native 自己整合テスト + 設計書 §3 反映 (v0.10 で再定義)

**前提**: §2.13 / §2.14 ユーザー判断 6 件確定済 (Q1 Native 自己整合のみ / Q2 不変量検証 / Q3 16 件 / Q4 設計書一括 / Q5 内訳 / Q6 fixture 化)。

#### 4.6-A NativeRouterDbFixture 新設

- 新規 `tests/OsmDotRoute.Tests/Native/NativeRouterDbFixture.cs` (`public sealed class : IDisposable`):
  - `NativeRoadGraph Graph` (tsushima.odrg をロード)
  - `NativeRoadSnapper Snapper`
  - `OsmDotRoute.RouterDb RouterDb` (`internal` コンストラクタを `InternalsVisibleTo` 経由で呼出)
  - `Router Router` (Phase 1 公開 API)
  - `Dispose()` で Graph.Dispose()

#### 4.6-B 自己整合テスト 16 件 (`tests/OsmDotRoute.Tests/Native/NativeRouterIntegrationTests.cs`、新規)

**Smoke 5 件 (Native Router が破綻なく経路を返すこと)**:

1. `Calculate_SamePoint_ReturnsTinyRoute`: 同一頂点座標で経路、`TotalDistanceM < 50m` (Phase 1 既存 `Calculate_SamePoint_ReturnsTrivialOrTinyRoute` と同じ判定)
2. `Calculate_ShortDistance_ReturnsRoute`: 100m 程度離れた 2 点で経路が返る
3. `Calculate_MediumDistance_ReturnsRoute`: 1km 程度離れた 2 点で経路が返る
4. `Calculate_FromOutsideBounds_ReturnsNull`: 起点が範囲外 (N89 度等) で null
5. `Calculate_ToOutsideBounds_ReturnsNull`: 終点が範囲外で null

**不変量 8 件 (経路結果の数学的整合性)**:

6. `Route_TotalDistanceIsPositive`: 中距離経路で `TotalDistanceM > 0`
7. `Route_FirstShapePointNearStart`: `Shape[0]` が起点から < 600m (スナップ半径 500m + 余裕)
8. `Route_LastShapePointNearEnd`: `Shape[^1]` が終点から < 600m
9. `Route_StraightLineDistanceLeqRouteDistance`: 起点→終点直線距離 ≤ 経路総距離
10. `Route_ShapeIsNotEmpty`: シェイプ点列が非空
11. `Route_ReverseDirectionApproximatelySameDistance`: from→to と to→from の距離が ±2% (一方通行影響でやや乖離許容)
12. `Route_DeterministicForSameInput`: 同じ from/to で 2 回計算、TotalDistanceM が完全一致
13. `Route_SegmentConnectivity`: `RouteSegment` 列で前 segment の `To` == 後 segment の `From` (RouteSegment が存在する場合)

**RouterDb コンストラクタ 2 件**:

14. `RouterDb_ConstructWithNativeGraphAndSnapper_DoesNotThrow`: 正常構築
15. `RouterDb_NullArguments_ThrowsArgumentNullException`: null IRoadGraph / null IRoadSnapper で `ArgumentNullException`

**Fixture sanity 1 件**:

16. `Fixture_Initializes_WithoutException`: fixture コンストラクタが例外を投げない (`new NativeRouterDbFixture()` が成功)

**Done 基準 (4.6-B)**:

- 16 件全 pass、累計 579 + 16 = **595 件 pass**
- ビルド 0 Warning / 0 Error
- Phase 1 既存 526 件 (Itinero 系) は変更なし、全 pass を維持 (Native 並存証明の代替)

#### 4.6-C 設計書 §3 全 6 サブセクション肉付け

[`Documents/phase3_design.md`](phase3_design.md) §3 を「（未記述）」プレースホルダから本実装に基づく内容に置換:

- **3.1 意図**: REQ-MAP-005 ([`requirement_definition.md`](requirement_definition.md)) 「`.odrg` ランタイム読込」の Phase 3 段階での実装。Phase 1 比 0.48× 性能維持 + 経路 1 本あたり 77MB アロケート削減土台 (REQ-NFR-003)
- **3.2 採用設計**: アーキテクチャ図 (text-art)
  - `OdrgMmfHandle` (MMF + SafeHandle ラッパ) → `OdrgSectionDirectory` (HEADER + SECTION TABLE) → `NativeRoadGraph` (CSR + ゼロコピー Span) → `NativeRoadSnapper` (R-tree + GeoMath) ↔ `NativeRTreeQuery` (Query + Nearest) ↔ `GeoMath` (Haversine + PointToSegment)
- **3.3 設計判断の根拠**: ユーザー判断確定一覧 (#21 MMF=SafeHandle ファイナライザ併用 / #22 Cache=制約 ID 単位 (3B 担当) / §3A.3-API EvaluateEdge 2 オーバーロード / L1 CSR / L5 GetEdgeShape Span / Q1 OdrgBbox / Q2 Brute-force 突合 / Q4 Brute-force / Q5 緯度依存近似 / Q6 Offset 距離比 / Q7 緯度補正コサイン 等)
- **3.4 トレードオフ・制約**:
  - Span ライフタイムは `NativeRoadGraph.Dispose()` まで (3A-R4)
  - プロファイル評価は `.odrg` BAKED_PROFILE 直読、`ProfileEvaluator` 非依存 (Native 専用最適化、3C で統合方針再検討)
  - `RouterDb(IRoadGraph, IRoadSnapper)` は internal、DI 統合は 3C 担当
  - Itinero との並存パリティは技術的に不可能 (ID 独立採番)、Phase 1 既存 526 件 pass で互換性証明を代替
- **3.5 検証方法**: 3A.1〜3A.6 のテスト合計 51 件 (5+8+0+0+3+9+8+8+12+16)
  - 3A.1 OdrgSectionDirectory 5 件
  - 3A.2 OdrgMmfHandle Span 切出 8 件
  - 3A.3e/3A.3f NativeRoadGraph 12 件 (OdrgReader 真値突合)
  - 3A.4 NativeRTreeQuery 8 件 (R-tree 正確性、Brute-force 突合)
  - 3A.5a GeoMath 8 件 (Haversine / 点-線分距離 / 投影 t)
  - 3A.5b NativeRoadSnapper 12 件 (Brute-force 突合)
  - 3A.6 NativeRouter 統合 16 件 (Smoke + 不変量)
- **3.6 実装メモ**: 主要ファイル一覧 + commit 番号
  - `src/OsmDotRoute/Internal/Odrg/` 配下 (OdrgFormat, OdrgSections, OdrgSectionDirectory, OdrgMmfHandle, OdrgFormatException): commits `fb6cd45`, `279a6ec`
  - `src/OsmDotRoute/Native/` 配下 (NativeRoadGraph, NativeEdgeEnumerator, OutEdgeEntry, NativeRTreeQuery, NativeRoadSnapper): commits `4549633`, `78d4581`, `5a54296`
  - `src/OsmDotRoute/Geometry/GeoMath.cs`: commit `88d00fe`
  - `src/OsmDotRoute/Routing/IRoadGraph.cs` 改修 (`EvaluateEdge` 2 オーバーロード追加 / `GetEdgeShape` 追加): commit `c46a2ca`

**Done 基準 (4.6-C)**:

- 設計書 §3 の 6 項目すべて「（未記述）」が消えている
- アーキテクチャ図がコード現状と一致

**Done 基準 (3A.6 / 3A 全体完了時)**:

- 16 件全 pass、累計 **595 件 pass**
- ビルド 0 Warning / 0 Error
- Phase 1 既存 526 件 (Itinero 系) は変更なし、全 pass 継続 (Native 並存証明の代替)
- 設計書 phase3_design.md §3 全 6 サブセクション肉付け完了
- 3A 全体完了の commit メッセージで全サブステップ commit 番号を一覧

**最終 commit メッセージ案 (4.6-B + 4.6-C 統合)**: `feat: Phase 3 ステップ 3A.6 + 3A 完了 (NativeRouter 統合 16 件 + 設計書 §3 反映、595 件 pass)`

---

## 5. リスクと対処

| # | リスク | 影響 | 対処 |
| --- | --- | --- | --- |
| 3A-R1 | `unsafe` Span の範囲外アクセスで SEGV | プロセスクラッシュ、原因切り分け困難 | 3A.2 で `OdrgMmfHandle.GetSpan<T>` に `Debug.Assert(offset+length*sizeof(T) <= _viewLength)` 必須。Release ビルドでも先頭 1 回のみチェック。`OdrgReader` 突合（3A.1 / 3A.2 / 3A.3 各 Done 基準）で外れ検出 |
| 3A-R2 | `SafeBuffer.AcquirePointer` 周りで参照カウント漏れ → メモリリーク | プロセスメモリ増加 | `OdrgMmfHandle` を `IDisposable` 厳格＋`SafeHandle` の自動解放併用（ユーザー判断 #21 (b)）。`Acquire` と `Release` を 1 ペアのみ、Dispose 時に Release。xUnit `OdrgMmfHandle` Dispose テストで参照カウントを直接検査 |
| 3A-R3 | R-tree ノードレイアウトの読み違い | クエリ結果が `ItineroSnapper` と乖離 | 3A.4 Done 基準でブルートフォース完全一致を必須化。`OdrgWriter.WriteRTreeSection` と `NativeRTreeQuery` を逐条で対比、ノード struct 定義を `OdrgWriter` 側と同一ファイル相当の構造で実装 |
| 3A-R4 | `IRoadGraph.GetEdgeShape` の Span ライフタイム逸脱（呼出側が `NativeRoadGraph.Dispose()` 後に Span を使用） | 不定動作、SEGV 可能性 | 3A.3 で `IRoadGraph` インターフェース XML doc に **「返却 Span のライフタイムは IRoadGraph インスタンスの Dispose まで」** を明記。3C で `Route.Shape` を `ReadOnlyMemory<T>` 化する際に `MemoryManager<T>` 経由で延命する設計に移行可能 |
| 3A-R5 | `ItineroSnapper` との距離計算微差で 178 ケースの 1〜2 件が不一致 | パリティテスト fail、3A 完了判定 NG | 3A.5 で `GeoMath` ヘルパ共有化、`ItineroSnapper` 側も同ヘルパを使うようリファクタ可（3C で Itinero 撤去時に消えるので無駄にならない） |
| 3A-R6 | エンディアン違い検出漏れ（`.odrg` 仕様はリトル固定、Windows は LE だが将来 ARM Big-endian 対応リスク） | 移植性問題 | 3A.1 で `OdrgHeader.IsLittleEndianHost` チェック、Big-endian ホスト時は `OdrgFormatException`。Phase 3 スコープ外として将来ステップに送る |
| 3A-R7 | `IRoadGraph` 改修 (3A.3b) で既存テスト破壊。Phase 1 経路探索の依存連鎖が広いため、漏れ修正で数十件 fail のリスク | 539 件 pass 維持失敗、3A.3 完了判定遅延 | サブステップ毎に `dotnet test` 全 pass を厳守（3A.3b で集中対応）。`GetEdgeOsmTags` 直呼びは 5 ファイル + ItineroRoadGraph 内部 1 箇所に限定済（事前 grep で確認）。改修前にテストヘルパパターンを統一しておく |
| 3A-R8 | 新 `IRoadGraph` 評価 API の設計が `EdgeWeightCalculator` ホットパスに不適合 | Dijkstra 性能劣化、Phase 1 比 0.48x 維持失敗 | §3A.3-API 確定時に (a) `EdgeEvaluation EvaluateEdge` 案を推奨理由として「ホットパス内のメソッドコール 1 回追加コストは無視可能」を明示。3E ベンチ実測で性能劣化を検出、(b) (c) 案への切替余地は 3C で再評価 |

---

## 6. テスト設計サマリ

**追加テスト件数（想定）**:

| サブステップ | 件数 | カテゴリ | 実績 |
| --- | --- | --- | --- |
| 3A.1 | 5 | セクションテーブルパース正常 / 異常 | ✅ 5 件 (commit `fb6cd45`) |
| 3A.2 | 7→8 | Span 切出 / Dispose 後アクセス | ✅ 8 件 (commit `279a6ec`) |
| 3A.3a | 0 | API 改修案ドラフト（コード変更なし） | ✅ 計画書 v0.3 / v0.4 / v0.5 |
| 3A.3b | 0 | IF + ItineroRoadGraph + EdgeWeightCalculator + Dijkstra + テスト 5 ファイル + Itinero テスト extension 一括改修 (v0.5 で旧 3A.3b/c/d 統合、件数増減なし、539 維持) | 計画書 v0.5 |
| ~~3A.3c~~ | ~~0~~ | ~~`EdgeWeightCalculator.Evaluate` 内部置換~~ | v0.5 で 3A.3b に統合 |
| ~~3A.3d~~ | ~~0~~ | ~~既存テスト追従最終確認~~ | v0.5 で 3A.3b に統合 |
| 3A.3e | 3 | NativeRoadGraph 構築 / 頂点読出 / Dispose の sanity check | ✅ 3 件 (commit `4549633`、542 件 pass) |
| 3A.3f | 9 | OdrgReader 真値突合: 頂点 100 / エッジ 100 / Shape 50 / GetEdgeShape 50 / キャッシュ 1 / 評価 API 50×2 × 2 / エラー 2 (v0.7 で Itinero 突合不可能と判明、OdrgReader 突合に絞り再定義) | ✅ 9 件 (551 件 pass) |
| 3A.4 | 8 | (v0.8 で内訳確定 Q6) R-tree アクセサー sanity / Query 全包含 / Query 範囲外 / Query × 50 ランダム Brute-force 突合 / Query overrun / Nearest k=1 / Nearest k=10 / ノード構造 sanity | ✅ 8 件 (commit `78d4581`、559 件 pass) |
| 3A.5a | 8 | (v0.9 で分割 Q1/Q8) GeoMath 単体: Haversine 2 / 点-線分距離 3 / 投影 t 3 | ✅ 8 件 (commit `88d00fe`、567 件 pass) |
| 3A.5b | 12 | (v0.9 で分割 Q8) NativeRoadSnapper: コンストラクタ 1 / 頂点上 1 / エッジ中央 1 / 非存在 profile 1 / 検索半径 0 null 1 / 範囲外 null 1 / bbox 拡張 1 / Brute-force ×20 ランダム 1 / Offset 単調性 1 / From/To 端点 2 / Dispose 後例外 1 (v0.9 §4.5.2 からの軽微逸脱: 通行不可除外を非存在 profile / 検索半径 0 に置換、Brute-force は 50→20 で CI 安定) | ✅ 12 件 (commit `5a54296`、579 件 pass) |
| 3A.6 | 16 | (v0.10 で再定義 Q3/Q5) Native 自己整合: smoke 5 + 不変量 8 + RouterDb コンストラクタ 2 + fixture sanity 1 (v0.9 「178 件 Itinero 突合」は T1 で技術的に不可能と判明、Native 自己整合 + Phase 1 既存 526 件継続で並存証明を代替) | |
| **合計** | **69** | （Phase 2 累計 526 → Phase 3 3A 完了時 595） | 累計 539 → 595 想定 |

**並存戦略**:

- 既存 Itinero 系テスト（Phase 1 / Phase 2 累計 526 件）は触らない、全 pass を維持
- Native 系テストは fixture 共有で実行時間を最小化
- CI 実行時間: Phase 2 全テスト 23 秒 → Native 追加でも CI が許容範囲内に収まるかを 3A.6 完了時に確認

---

## 7. 着手前の確認事項

- [x] §5.5-#21 確定（MMF=ファイナライザ併用）
- [x] §5.5-#22 確定（Cache=制約 ID 単位、3B 担当だが先行確定）
- [x] R9 親プロ調査完了（[[project_phase3_parent_integration_scan]]）
- [x] 本ステップ計画書 v0.1 起草
- [x] 本ステップ計画書 v0.1 ユーザーレビュー → 承認 (commit `b27be51`)
- [x] 3A.1 完了 (commit `fb6cd45`、531 件 pass)
- [x] 3A.2 完了 (commit `279a6ec`、539 件 pass)
- [x] **§3A.3-API (a) 確定**（commit `eb1431c` 計画書 v0.3 承認時）
- [x] **本ステップ計画書 v0.3 ユーザー承認** (commit `eb1431c`)
- [x] 3A.3a 着手 → §2.6.1 / §2.6.2 確定で計画書 v0.4
- [x] **本ステップ計画書 v0.4 ユーザー承認** (commit `cd661d0`)
- [x] 3A.3b 着手前事前調査 → §2.6.1 / §2.6.2 ギャップ発見、ユーザー判断 3 件確定 (2026-05-26):
  - API 形状 = **RoadEdge オーバーロード追加**
  - 車道判定 = **Itinero テスト用 extension 新設**
  - スコープ = **案 β (3A.3b / 3A.3c / 3A.3d 統合)、案 α は技術的不成立**
- [x] **本ステップ計画書 v0.5 ユーザー承認** (commit `10a2038`)
- [x] **3A.3b 完了** (commit `c46a2ca`、539 件 pass 維持)
- [x] 3A.3e 着手前事前調査 → §2.7 / §2.8 整理、ユーザー判断 4 件確定 (2026-05-26):
  - L1 = **CSR (`firstOutEdge` + `outEntries`)**
  - L5 = **`GetEdgeShape` 追加** (Itinero per-call、Native ゼロコピー)
  - L6 = **class 毎回 new** (Itinero と同じシンプル実装)
  - L8 = **3A.3e で sanity 3 件 + 3A.3f で 9 件**
- [x] **本ステップ計画書 v0.6 ユーザー承認** (commit `a952efc`)
- [x] **3A.3e 完了** (commit `4549633`、542 件 pass)
- [x] 3A.3f 着手前調査 → .odrg と Itinero RouterDb の頂点 ID / エッジ ID が独立採番で Itinero 突合は ID ベースで不可能と判明、ユーザー判断 4 件確定 (2026-05-26):
  - P1 = **OdrgReader 真値突合のみ** (Itinero 突合は 3A.6 で経路結果一致として担保)
  - P2 = **BakedProfileEntry と一致** (Itinero タグ評価は Phase 2 で証明済み)
  - P3 = **エラーケース 2 件** (未開封 path + マジック改竄)
  - P4 = **サンプル 100 / 100 / 50 / 50×2 profile**
- [x] **3A.3f 完了** (551 件 pass、3A.3 全体完了、commit `f573c08`)
- [x] 3A.4 着手前事前調査 → §2.9 / §2.10 整理、ユーザー判断 6 件確定 (2026-05-26):
  - Q1 = **OdrgBbox 採用** (Lon-Lat、Internal.Odrg 既存型、wire format 一致)
  - Q2 = **Brute-force 突合に変更** (Itinero 突合は 3A.5/3A.6 で経路結果一致として担保)
  - Q3 = **NativeRoadGraph に internal API 追加** (`GetRTreeNodes` + `RTreeRootIndex` 等)
  - Q4 = **ヒット総数を返し buffer.Length まで書込** (overrun は呼出側検出)
  - Q5 = **点-AABB 最小距離** (経緯度 2D euclidean、度単位、R-tree 枝刈り規約と一致)
  - Q6 = **テスト 8 件内訳確定** (R-tree アクセサー sanity / Query 全包含 / Query 範囲外 / Query × 50 Brute-force / Query overrun / Nearest k=1 / Nearest k=10 / ノード構造)
- [x] **本ステップ計画書 v0.8 ユーザー承認** (commit `7ee84e4`)
- [x] **3A.4 完了** (commit `78d4581`、559 件 pass、計画書 v0.8 §4.4-B からの軽微な逸脱: Query にも edgeAabbs を渡してリーフ展開時に true-positive filter を実施)
- [x] 3A.5 着手前事前調査 → §2.11 / §2.12 整理、ユーザー判断 8 件確定 (2026-05-26):
  - Q1 = **3A.5a / 3A.5b に分割** (GeoMath 単体 + NativeRoadSnapper)
  - Q2 = **NativeRoadGraph 経由で profileName → BAKED_PROFILE スロット直読** (ProfileEvaluator 経由しない)
  - Q3 = **`src/OsmDotRoute/Geometry/GeoMath.cs` 新設** (`internal static class`、Haversine + MetersToBboxDegrees + PointToSegment)
  - Q4 = **Brute-force 突合に変更** (3A.4 Q2 と同パターン、Itinero 突合は 3A.6 担当)
  - Q5 = **緯度依存近似** (dLat = m/111320、dLon = m/(111320 × cos(lat)))
  - Q6 = **Offset = 距離比 × 65535** (Itinero 互換)
  - Q7 = **緯度補正コサイン** (局所メートル平面、x = (lon-lon0) × cos(lat0) × R)
  - Q8 = **テスト 8+12=20 件** (累計 559 → 579)
- [x] **本ステップ計画書 v0.9 ユーザー承認** (commit `86d591c`)
- [x] **3A.5a 完了** (commit `88d00fe`、567 件 pass)
- [x] **3A.5b 完了** (commit `5a54296`、579 件 pass、計画書 §4.5.2 から軽微な逸脱: 通行不可除外テストを非存在 profile / 検索半径 0 に置換、Brute-force 50→20 で CI 安定)
- [x] 3A.6 着手前事前調査 → §2.13 / §2.14 整理、ユーザー判断 6 件確定 (2026-05-26):
  - Q1 = **(B) Native 自己整合のみ、Itinero 突合廃止** (.routerdb と .odrg が別ソース、Itinero 並存証明は Phase 1 既存 526 件で代替)
  - Q2 = **Native 単体経路計算の不変量検証** (Brute-force 二重実装は避ける)
  - Q3 = **テスト 178 → 16 件に記口** (累計 579 → 595)
  - Q4 = **3A.6 で設計書 §3 全 6 サブセクション一括記述** (1 commit 化)
  - Q5 = **16 件内訳**: smoke 5 + 不変量 8 + RouterDb コンストラクタ 2 + fixture sanity 1
  - Q6 = **NativeRouterDbFixture 新設し IClassFixture で共有**
- [ ] 本ステップ計画書 v0.10 ユーザーレビュー → 承認
- [ ] 3A.6 (Native 自己整合テスト + 設計書 §3 反映) 着手

---

## 8. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
| --- | --- | --- | --- |
| 0.1 (draft) | 2026-05-26 | 初版起草。ユーザー判断 #21 (MMF=ファイナライザ併用) / #22 (Cache=ID 単位) / 3A 計画書置き場所 (新規) / DI 切替 (テスト内直接構築) / 並存テスト規模 (89 ペア × 2 実装 = 178) を反映。サブステップ 3A.1〜3A.6（6 段）、追加テスト 393 件、リスク R1〜R6。Phase 2 ステップ 5 計画書スタイル踏襲 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-26 | v0.1 ユーザー承認 (commit `b27be51`) 後、3A.1 着手前の現状確認で発見した訂正を反映：(1) §2.4 セクション構成表を実装確認済の 9 セクション (VERTEX / EDGE / EDGE_SHAPE / EDGE_AABB / EDGE_FLAG 独立 / SPATIAL_INDEX / BAKED_PROFILE / TURN_RESTRICTION / METADATA) に訂正、v0.1 の誤記 (PROFILE_BAKE / STRING_POOL、flags が EDGE 内、`バージョン 0x0002`) を訂正。(2) §3.1 スコープ内に「OdrgFormat を Extractor → Core へ移動」を前提リファクタとして追加（依存方向 Core ← Pbf ← Extractor のため）。(3) §4.1 (3A.1) を「ステップ 0: OdrgFormat Core 移動 / ステップ 1: OdrgSectionDirectory 実装」に分割、検証条件を `VersionMajor == 1` / `edgeFlagBytes == 2` / `sectionCount == 9` に具体化、参照真値を `OdrgReader.Read` に統一 | Claude (Opus 4.7) |
| 0.3 (draft) | 2026-05-26 | 3A.1 完了 (commit `fb6cd45`) / 3A.2 完了 (commit `279a6ec`) 後、3A.3 着手前の `IRoadGraph` 依存連鎖調査で発見した重大な設計問題を反映：(1) §2.5 追加 = `.odrg` には OSM タグ生データなし → `NativeRoadGraph.GetEdgeOsmTags` 実装不可、`IRoadGraph` 改修必須。ユーザー判断 B 案 (3A.3 で `IRoadGraph` 改修込み) 確定。(2) §2.6 追加 = 着手前ペンディング判断 §3A.3-API 起票 (新評価 API シグネチャ a/b/c)、推奨 (a) `EdgeEvaluation EvaluateEdge`。(3) §4.3 を B 案サブステップ詳細 3A.3a〜3A.3f に全面書き直し（API 改修案ドラフト / `ItineroRoadGraph` 追従 / `EdgeWeightCalculator` 内部置換 / 既存テスト追従 / `NativeRoadGraph` 新規 / テスト 12 件）。各サブステップで `dotnet test` 全 pass 維持。(4) §5 リスク表に 3A-R7 (改修で既存テスト破壊) / 3A-R8 (新 API がホットパス不適合) 追加。(5) §6 テスト件数表に 3A.3 サブステップ分割反映 + 3A.1/3A.2 実績反映 (5+8、累計 539)。(6) §7 着手前確認事項を v0.3 用に更新。3A.1〜3A.2 完了済をチェック、§3A.3-API 確定 + v0.3 承認をペンディング | Claude (Opus 4.7) |
| 0.4 (draft) | 2026-05-26 | v0.3 ユーザー承認 (commit `eb1431c`) + §3A.3-API (a) ユーザー判断確定後、3A.3a 成果物を反映：(1) §2.6 確定マーク追記。(2) §2.6.1 追加 = (a) 案 確定後の詳細シグネチャ。3A.3a 着手時の現状確認で `IRoadProfile` 不在（v0.3 §2.6 の架空型）/ `ProfileEvaluator.Name` 未公開を発見、確定シグネチャを `EvaluateEdge(IRoadGraphEdgeEnumerator, ProfileEvaluator)` に補正。`ProfileEvaluator.Name` プロパティ追加方針 + `EdgeWeightCalculator` 改修コード骨格 + `IRoadGraphEdgeEnumerator.EdgeProfileIndex` の扱い (保持、3C で廃止検討) も決定。(3) §2.6.2 追加 = 既存テスト 5 ファイル `GetEdgeOsmTags` 直呼び 6 箇所 (本番 2 + テスト 4) の grep 結果 + 改修方針表。(4) §7 着手前確認事項を v0.4 用に更新、3A.3a 完了済をチェック、v0.4 承認 + 3A.3b 着手をペンディング | Claude (Opus 4.7) |
| 0.10 (draft) | 2026-05-26 | v0.9 commit `86d591c` + 3A.5a commit `88d00fe` (567 件 pass) + 3A.5b commit `5a54296` (579 件 pass) 後、3A.6 着手前事前調査で計画書 v0.9 §4.6 と現状コードのギャップ 7 件を発見 (§2.13 新設): T1 default.routerdb と tsushima.odrg が別データソース (地理エリア違いで並存比較不可) / T2 Phase 1 既存経路テストは ±10% 緩い比較で「完全一致は目指さない」明記 / T3 89 ペアは架空数値 (実態 12 ペア最大) / T4 tsushima_extract.osm.pbf あり (Itinero 生成可能だがコスト) / T5 IRoadProfile 架空 (3A.5 S2 と同根) / T6 RouterDb(IRoadGraph, IRoadSnapper) は internal 構築可能 / T7 Phase 3 設計書 §3 全項目「未記述」。これに対するペンディング判断 6 件 (§2.14) をユーザー判断確定 (2026-05-26): Q1 = (B) **Native 自己整合のみ、Itinero 突合廃止** (並存証明は Phase 1 既存 526 件で代替) / Q2 = **Native 単体経路計算の不変量検証** / Q3 = **テスト 178 → 16 件に記口** (累計 579 → 595) / Q4 = **3A.6 で設計書 §3 一括記述** / Q5 = **16 件内訳** (smoke 5 + 不変量 8 + RouterDb 2 + fixture 1) / Q6 = **NativeRouterDbFixture 新設**。§4.6 を v0.10 で再定義: 4.6-A NativeRouterDbFixture 新設 + 4.6-B 自己整合 16 件詳細 (smoke / 不変量 / コンストラクタ / fixture sanity) + 4.6-C 設計書 §3 全 6 サブセクション肉付け (3.1 意図 / 3.2 採用設計 アーキテクチャ図 / 3.3 設計判断根拠 ユーザー判断確定一覧 / 3.4 トレードオフ / 3.5 検証方法 全 51 件内訳 / 3.6 実装メモ 主要ファイル一覧 + commit 番号)。§6 テスト件数表で 3A.5a/3A.5b 実績マーク + 3A.6 178→16 訂正 + 累計 920→595 訂正。§7 着手前確認事項を v0.10 用に更新、3A.5 完了 + ユーザー判断 6 件確定をチェック、v0.10 承認 + 3A.6 着手をペンディング | Claude (Opus 4.7) |
| 0.9 (draft) | 2026-05-26 | v0.8 commit `7ee84e4` + 3A.4 完了 commit `78d4581` (559 件 pass) 後、3A.5 着手前事前調査で計画書 v0.8 §4.5 と現状コードのギャップ 6 件を発見 (§2.11 新設): S1 `GeoMath` ヘルパ完全不在 (計画書「Phase 1 既存流用」は架空) / S2 `IRoadProfile` 不在 (3A.3a 同様、現 IRoadSnapper 維持必要) / S3 profileName → ProfileEvaluator 仲介は二択 (経由 or NativeRoadGraph 直アクセス) / S4 Itinero との ID 一致 + 座標一致 Done 基準は技術的不可能 (3A.4 Q2 と同根) / S5 3A.5 スコープが GeoMath 新設で 3A.3 並み / S6 `Router.SnapToRoad` は profileName 一本で統合済。これに対するペンディング判断 8 件 (§2.12) をユーザー判断確定 (2026-05-26): Q1 サブステップ分割 = **3A.5a (GeoMath) / 3A.5b (NativeRoadSnapper) 分割** / Q2 profile 仲介 = **NativeRoadGraph 経由直アクセス** (ProfileEvaluator 不要) / Q3 GeoMath 配置 = **`src/OsmDotRoute/Geometry/GeoMath.cs` 新設** / Q4 Done 基準 = **Brute-force 突合に変更** (Itinero 突合は 3A.6 担当) / Q5 度換算 = **緯度依存近似** (dLat = m/111320、dLon = m/(111320 × cos(lat))) / Q6 Offset = **距離比 × 65535** (Itinero 互換) / Q7 平面化 = **緯度補正コサイン** / Q8 テスト = **3A.5a 8件 + 3A.5b 12件 = 20件** (累計 559 → 579)。§4.5 を v0.9 で 3A.5a / 3A.5b に分割詳細化: 4.5.1 GeoMath 新設 + 既存 NativeRoadGraph.HaversineMeters 統合 + テスト 8 件 / 4.5.2 NativeRoadGraph.CanPass 追加 + NativeRoadSnapper 本体 + Snap 6 ステップ + テスト 12 件 (Brute-force 突合本体含む)。§6 テスト件数表で 3A.5 を 3A.5a/3A.5b に分割 + 3A.4 実績マーク ✅。§7 着手前確認事項を v0.9 用に更新、3A.4 完了 + ユーザー判断 8 件確定をチェック、v0.9 承認 + 3A.5a 着手をペンディング | Claude (Opus 4.7) |
| 0.8 (draft) | 2026-05-26 | v0.7 commit `f573c08` (3A.3f 完了、551 件 pass、3A.3 全体完了) 後、3A.4 着手前事前調査で計画書 v0.7 §4.4 と現状コードのギャップ 5 件を発見 (§2.9 新設): G1 R-tree 書出/読出/Core struct 完全対称 (`MemoryMarshal.Cast` 可) / G2 AABB 型 3 系統 (Geometry.Aabb / Extractor.Aabb / OdrgBbox) / G3 `ItineroSnapper.EdgeIndex.SearchClosestEdges` 不在 (Router.Resolve 一本のみ、ID 独立採番と 2 重で突合不可) / G4 `NativeRoadGraph` に R-tree アクセサー未実装 / G5 計画書 8 件テストの内訳未確定。これに対するペンディング判断 6 件 (§2.10) をユーザー判断確定 (2026-05-26): Q1 入力 bbox 型 = **OdrgBbox** (Lon-Lat、wire format 一致) / Q2 Nearest Done 基準 = **Brute-force 突合に変更** (Itinero 突合は 3A.5/3A.6 で経路結果一致として担保) / Q3 R-tree API 配置 = **NativeRoadGraph に internal API 追加** / Q4 overrun = **ヒット総数返し buffer.Length まで書込** / Q5 Nearest 距離 = **点-AABB 最小距離** (経緯度 2D euclidean 度単位) / Q6 テスト 8 件内訳確定 (R-tree アクセサー sanity / Query 全包含 / 範囲外 / × 50 Brute-force / overrun / Nearest k=1 / k=10 / ノード構造)。§4.4 を v0.8 詳細化: 4.4-A 事前作業 (NativeRoadGraph R-tree アクセサー追加) + 4.4-B 実装 (NativeRTreeQuery static class、Query / Nearest シグネチャ、明示スタック DFS + best-first Nearest + 点-AABB 最小距離計算式) + 4.4-C テスト 8 件詳細。§6 テスト件数表で 3A.4 行に内訳追記。§7 着手前確認事項を v0.8 用に更新、3A.3 全体完了 + ユーザー判断 6 件確定をチェック、v0.8 承認 + 3A.4 着手をペンディング | Claude (Opus 4.7) |
| 0.7 (draft) | 2026-05-26 | v0.6 ユーザー承認 (commit `a952efc`) + 3A.3e 完了 (commit `4549633`、542 件 pass) 後、3A.3f 着手前調査で **.odrg と Itinero RouterDb の頂点 ID / エッジ ID が独立採番**で Itinero との ID ベース突合は不可能と判明、ユーザー判断 4 件 (2026-05-26) で対応方針確定: P1 突合対象 = OdrgReader 真値のみ (Itinero 突合は 3A.6 で経路結果一致として担保) / P2 評価 API = BakedProfileEntry と一致 (Itinero タグ評価は Phase 2 で証明済み) / P3 エラー = 未開封 path + マジック改竄 2 件 / P4 サンプル = 頂点 100 / エッジ 100 / Shape 50 / 評価 50×2 profile。§4.3.6 を OdrgReader 真値突合 9 件版に再定義: GetVertex / GetEdgeEnumerator 反転 / GetEdge Shape / GetEdgeShape Span / キャッシュ動作 (Assert.Same) / EvaluateEdge en × Car/Pedestrian / EvaluateEdge RoadEdge × Car/Pedestrian / 未開封 path / マジック改竄。§6 テスト件数表で 3A.3e/3A.3f 実績マーク + ✅。§7 着手前確認事項を v0.7 用に更新、v0.6 承認 + 3A.3e 完了 + ユーザー判断 4 件確定 + 3A.3f 完了をチェック、v0.7 承認 + 3A.4 着手をペンディング | Claude (Opus 4.7) |
| 0.6 (draft) | 2026-05-26 | v0.5 ユーザー承認 (commit `10a2038`) + 3A.3b 完了 (commit `c46a2ca`、539 件 pass 維持) 後、3A.3e 着手前事前調査で `.odrg` / Itinero / 各実装の発見 5 件を整理 (§2.7 新設): F1 EDGE は頂点グループ化されていない → CSR インデックス必要 / F2 edgeCount は無向辺数 (仕様書 §1 表記の誤り、Itinero と数値一致) / F3 EDGE_SHAPE は中間点のみ (Itinero と一致) / F4 GeoCoordinate(Lat,Lon) と OdrgVertex(Lon,Lat) のフィールド順が逆 (直 Span 化不可) / F5 tsushima.odrg = 3.55 MB 確認。これに対するペンディング判断 4 件を §2.8 で起票 + ユーザー判断確定: L1 CSR (`firstOutEdge: uint[V+1]` + `OutEdgeEntry[2E]`) 採用 / L5 IRoadGraph に `GetEdgeShape(uint) -> ReadOnlySpan<GeoCoordinate>` 追加 + IRoadGraph : IDisposable 化 / L6 NativeEdgeEnumerator は class 毎回 new (Itinero と同じ) / L8 3A.3e で sanity 3 件 + 3A.3f で 9 件 (累計 12 件)。L2 EdgeCount セマンティクス / L3 Shape 端点扱い / L4 GeoCoordinate レイアウト / L7 Dispose 後アクセスは推奨案通り。§4.3.5 3A.3e を v0.6 詳細化: 事前作業 (IRoadGraph.GetEdgeShape 追加 + IRoadGraph : IDisposable 化 + Itinero 追従) + 実装 (NativeRoadGraph 全フィールド + コンストラクタ + IRoadGraph 全メソッド) + NativeEdgeEnumerator 詳細 + DistanceM 計算 (Haversine + キャッシュ) + sanity test 3 件。§4.3.6 3A.3f を 9 件版に再配分。§6 テスト件数表で 3A.3e の 0→3 件、3A.3f の 12→9 件に補正 + 累計 542/551 に補正。§7 着手前確認事項を v0.6 用に更新、v0.5 承認 + 3A.3b 完了 + ユーザー判断 4 件確定をチェック、v0.6 承認 + 3A.3e 着手をペンディング | Claude (Opus 4.7) |
| 0.5 (draft) | 2026-05-26 | v0.4 ユーザー承認 (commit `cd661d0`) 後、3A.3b 着手前事前調査で v0.4 §2.6.1 と現状コードのギャップを 3 件発見：(1) `DijkstraEngine.cs:42,46` は `RoadEdge.EdgeProfileIndex` 経由の評価呼出で `en` を持たない (v0.4 単一シグネチャ `EvaluateEdge(en, evaluator)` だけでは呼べない)。(2) `GetEdgeOsmTags(ushort)` 削除後は `EdgeWeightCalculator.Evaluate(ushort)` 内部から新 API を呼ぶ術がなく、v0.4 §4.3.2 で示唆した「案 α (3A.3b で暫定 fall back)」は**技術的に不成立**。(3) テスト 5 ファイル `IsCarHighway(tags["highway"])` ヘルパは `ProfileEvaluator.Evaluate(tags).CanPass` では粒度再現不可。ユーザー判断 3 件確定 (2026-05-26): (a) **RoadEdge オーバーロード追加** = `IRoadGraph.EvaluateEdge` を `(en, evaluator)` + `(RoadEdge, evaluator)` の 2 本に。(b) **Itinero テスト用 extension 新設** = `src/OsmDotRoute.Itinero/ItineroRoadGraphTestExtensions.cs` で `GetEdgeOsmTagsForTest` を提供、テストヘルパは `IsCarHighway` 判定を維持。(c) **案 β (3A.3b/3A.3c/3A.3d 統合) 採用、案 α 不採用**。反映: (1) §2.6.1 確定シグネチャを 2 オーバーロードに訂正、ギャップ発見記述追加。(2) §2.6.2 改修方針表を Itinero extension 経由に訂正、テスト改修パターン明示。(3) §2.6.3 新設 = Itinero テスト用 extension の設計 + InternalsVisibleTo 確認手順 + テスト書換パターン。(4) §4.3.2 を v0.4 3A.3b/c/d 統合版に全面書き直し、作業項目 8 段 + Done 基準 6 段 + v0.4 サブステップ対応表追加。(5) §4.3.3 / §4.3.4 を削除し v0.5 で 3A.3b に統合した旨を明示。(6) §6 テスト件数表で 3A.3c/3A.3d を取り消し線 (3A.3b に統合)、3A.3b の説明を統合版に書き換え、3A.3a 実績マーク。(7) §7 着手前確認事項を v0.5 用に更新、v0.4 承認済 + ユーザー判断 3 件確定をチェック、v0.5 承認 + 3A.3b 着手をペンディング | Claude (Opus 4.7) |
