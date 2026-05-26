# Phase 3 ステップ 3A: ランタイム `.odrg` 読込実装 計画書

**ステータス**: ドラフト v0.7（v0.6 ユーザー承認 commit `a952efc` + 3A.3e 完了 commit `4549633` 後、3A.3f 着手前調査で `.odrg` と Itinero RouterDb の頂点 ID / エッジ ID 体系不一致を発見、Itinero 突合は ID ベースで不可能と判明、ユーザー判断 4 件 (P1-P4) で OdrgReader 真値突合に絞り 9 件のテストを再定義、3A.3f 完了、2026-05-26）
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

### 4.4 3A.4: STR R-tree クエリ実装

**実装**:

- `NativeRTreeQuery` 型（`internal static class`）
- `int Query(ReadOnlySpan<RTreeNode> tree, in EdgeAabb queryBox, Span<int> resultBuffer)` シグネチャ
  - tree のルートから DFS、子ノードの bbox が queryBox と交差するもののみ再帰
  - リーフ到達時、エッジ ID を resultBuffer に詰める
  - 戻り値はヒット数（resultBuffer に書き込んだ要素数）
- バッファあふれ時の挙動: ヒット数だけ返し、`resultBuffer.Length` を超えるエッジは捨てる（呼出側は再試行）。または `Length` を返して別 API で再クエリを促す
- 最近傍検索 `int Nearest(ReadOnlySpan<RTreeNode> tree, double lat, double lon, int k, Span<int> resultBuffer)` も同様

**Done 基準**:

- 津島市 `.odrg` の R-tree（仕様書 §4.7 STR パック M=16）に対し、任意の bbox クエリでヒットエッジ ID 集合が **ブルートフォース AABB 線形走査と完全一致**（38,004 エッジ × 50 個のランダム bbox クエリ）
- 最近傍 k=10 クエリで、`ItineroSnapper` 内部の `EdgeIndex.SearchClosestEdges` と同じエッジ ID 集合を返す（順序は問わない、集合一致）
- xUnit テスト 8 件

**設計判断**:

- 再帰スタック深さ制限: STR M=16 で 38,004 エッジ → 木の高さ ≒ ceil(log_16(38004)) = 4。深さ制限不要
- 都道府県単位（数百万エッジ）でも高さ ≒ 5〜6、深さ制限不要（3G で確認）

**リスク**: R-tree レイアウトの読み違い。対処として Phase 2 `OdrgWriter.WriteRTreeSection` の書出ロジックを逐条で対比し、ノード構造体を完全互換にする。

---

### 4.5 3A.5: `NativeRoadSnapper` 実装（`IRoadSnapper` 実装）

**実装**:

- `NativeRoadSnapper : IRoadSnapper`（`src/OsmDotRoute/Native/`、`public sealed class`）
- コンストラクタ: `NativeRoadSnapper(NativeRoadGraph graph)`（graph 経由で MMF ハンドル / R-tree 参照、独自 MMF は持たない）
- メソッド: `SnapResult Snap(double lat, double lon, IRoadProfile profile, double maxDistanceMeters)`、内部処理は以下の順序：
    1. 緯度経度から検索 bbox を生成（maxDistanceMeters → 度換算）
    2. `NativeRTreeQuery.Query` で候補エッジ ID 集合取得
    3. 各候補エッジのシェイプを `graph.GetEdgeShape(edgeId)` で取得
    4. シェイプ上の各セグメントへの垂線最短距離を計算（Phase 1 既存 `GeoMath.PointToSegmentDistance` 流用）
    5. profile 評価で通行可能なエッジのみフィルタ
    6. 最短のエッジ ID + シェイプ内位置 t 値 + スナップ点座標を `SnapResult` で返却

**Done 基準**:

- 津島市 89 ペア × 2 端点 = 178 スナップで、`ItineroSnapper` と
  - エッジ ID 完全一致
  - スナップ点座標 ±1e-7 度（≒1cm）以内
  - シェイプ内 t 値 ±1e-6 以内
- 解決失敗ケース（maxDistance 内に車道なし）でも `ItineroSnapper` と同じ判定
- xUnit テスト 178 + 解決失敗 5 = 183 件

**リスク**: `ItineroSnapper` 内部の距離計算が二乗近似 / 球面近似で `NativeRoadSnapper` の正確計算と微差が出る可能性。対処として **`ItineroSnapper` の距離計算ロジックを Phase 1 と同じ式で `NativeRoadSnapper` に移植**（コピーではなく、`GeoMath` ヘルパを共有）。

---

### 4.6 3A.6: 並存パリティテスト + 設計書 §3 反映

**実装**:

- `tests/OsmDotRoute.Tests/NativeRoadGraphParityTests.cs`（新規）
- xUnit `[Theory]` + `[MemberData(nameof(Pair89Cases))]` で 89 ペアを `IRoadGraph` 別に流す
- fixture: `IClassFixture<NativeAndItineroGraphFixture>` で `.odrg` と `.routerdb` を同時ロード、テストクラス共有
- 各ペアで以下を完全一致 assert:
  - 経路頂点列（int[]、Itinero / Native で同一頂点 ID 列）
  - 経路総距離（double、±1e-6 m）
  - 経路総所要時間（double、±1e-6 秒）

**fixture 構造**:

```csharp
public sealed class NativeAndItineroGraphFixture : IDisposable
{
    public ItineroRoadGraph ItineroGraph { get; }
    public NativeRoadGraph NativeGraph { get; }
    public ItineroSnapper ItineroSnapper { get; }
    public NativeRoadSnapper NativeSnapper { get; }
    public IRoadProfile CarProfile { get; }
    // ...
}
```

**Done 基準**:

- 89 ペア × 2 実装 = 178 テスト全 pass
- 1 件でも頂点列 / 距離 / 時間が不一致なら fail（許容差は浮動小数演算の数値誤差レベルのみ）
- 解決失敗が両実装で同じケース集合になる
- xUnit テスト 178 件（fail 0）

**設計書 §3 反映内容**:

- アーキテクチャ図（`OdrgMmfHandle` / `OdrgSectionDirectory` / `OdrgSpanView` / `NativeRoadGraph` / `NativeRoadSnapper` / `NativeRTreeQuery` の関係）
- `IRoadGraph.GetEdgeShape` の API 仕様（`ReadOnlySpan<GeoCoordinate>`、Span ライフタイムは `NativeRoadGraph.Dispose()` 呼出までと明記）
- MMF 解放方針（ユーザー判断 #21 (b) 反映、`SafeHandle` 系の CriticalFinalizer 動作説明）
- 並存パリティテスト 178 ケースの根拠と運用（3C で `ItineroRoadGraph` 撤去まで CI で常時実行）

**最終 commit メッセージ案**: `feat: Phase 3 ステップ 3A 完了 (NativeRoadGraph + NativeRoadSnapper 並存パリティ)`

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
| 3A.4 | 8 | R-tree クエリ正確性 / ブルートフォース突合 | |
| 3A.5 | 183 | `NativeRoadSnapper` 178 + 解決失敗 5 | |
| 3A.6 | 178 | 89 ペア × 2 実装 経路パリティ | |
| **合計** | **394** | （Phase 2 累計 526 → Phase 3 3A 完了時 920） | 累計 539 → 920 想定 |

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
- [x] **3A.3f 完了** (551 件 pass、3A.3 全体完了)
- [ ] 本ステップ計画書 v0.7 ユーザーレビュー → 承認
- [ ] 3A.4 (STR R-tree クエリ) 着手

---

## 8. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
| --- | --- | --- | --- |
| 0.1 (draft) | 2026-05-26 | 初版起草。ユーザー判断 #21 (MMF=ファイナライザ併用) / #22 (Cache=ID 単位) / 3A 計画書置き場所 (新規) / DI 切替 (テスト内直接構築) / 並存テスト規模 (89 ペア × 2 実装 = 178) を反映。サブステップ 3A.1〜3A.6（6 段）、追加テスト 393 件、リスク R1〜R6。Phase 2 ステップ 5 計画書スタイル踏襲 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-26 | v0.1 ユーザー承認 (commit `b27be51`) 後、3A.1 着手前の現状確認で発見した訂正を反映：(1) §2.4 セクション構成表を実装確認済の 9 セクション (VERTEX / EDGE / EDGE_SHAPE / EDGE_AABB / EDGE_FLAG 独立 / SPATIAL_INDEX / BAKED_PROFILE / TURN_RESTRICTION / METADATA) に訂正、v0.1 の誤記 (PROFILE_BAKE / STRING_POOL、flags が EDGE 内、`バージョン 0x0002`) を訂正。(2) §3.1 スコープ内に「OdrgFormat を Extractor → Core へ移動」を前提リファクタとして追加（依存方向 Core ← Pbf ← Extractor のため）。(3) §4.1 (3A.1) を「ステップ 0: OdrgFormat Core 移動 / ステップ 1: OdrgSectionDirectory 実装」に分割、検証条件を `VersionMajor == 1` / `edgeFlagBytes == 2` / `sectionCount == 9` に具体化、参照真値を `OdrgReader.Read` に統一 | Claude (Opus 4.7) |
| 0.3 (draft) | 2026-05-26 | 3A.1 完了 (commit `fb6cd45`) / 3A.2 完了 (commit `279a6ec`) 後、3A.3 着手前の `IRoadGraph` 依存連鎖調査で発見した重大な設計問題を反映：(1) §2.5 追加 = `.odrg` には OSM タグ生データなし → `NativeRoadGraph.GetEdgeOsmTags` 実装不可、`IRoadGraph` 改修必須。ユーザー判断 B 案 (3A.3 で `IRoadGraph` 改修込み) 確定。(2) §2.6 追加 = 着手前ペンディング判断 §3A.3-API 起票 (新評価 API シグネチャ a/b/c)、推奨 (a) `EdgeEvaluation EvaluateEdge`。(3) §4.3 を B 案サブステップ詳細 3A.3a〜3A.3f に全面書き直し（API 改修案ドラフト / `ItineroRoadGraph` 追従 / `EdgeWeightCalculator` 内部置換 / 既存テスト追従 / `NativeRoadGraph` 新規 / テスト 12 件）。各サブステップで `dotnet test` 全 pass 維持。(4) §5 リスク表に 3A-R7 (改修で既存テスト破壊) / 3A-R8 (新 API がホットパス不適合) 追加。(5) §6 テスト件数表に 3A.3 サブステップ分割反映 + 3A.1/3A.2 実績反映 (5+8、累計 539)。(6) §7 着手前確認事項を v0.3 用に更新。3A.1〜3A.2 完了済をチェック、§3A.3-API 確定 + v0.3 承認をペンディング | Claude (Opus 4.7) |
| 0.4 (draft) | 2026-05-26 | v0.3 ユーザー承認 (commit `eb1431c`) + §3A.3-API (a) ユーザー判断確定後、3A.3a 成果物を反映：(1) §2.6 確定マーク追記。(2) §2.6.1 追加 = (a) 案 確定後の詳細シグネチャ。3A.3a 着手時の現状確認で `IRoadProfile` 不在（v0.3 §2.6 の架空型）/ `ProfileEvaluator.Name` 未公開を発見、確定シグネチャを `EvaluateEdge(IRoadGraphEdgeEnumerator, ProfileEvaluator)` に補正。`ProfileEvaluator.Name` プロパティ追加方針 + `EdgeWeightCalculator` 改修コード骨格 + `IRoadGraphEdgeEnumerator.EdgeProfileIndex` の扱い (保持、3C で廃止検討) も決定。(3) §2.6.2 追加 = 既存テスト 5 ファイル `GetEdgeOsmTags` 直呼び 6 箇所 (本番 2 + テスト 4) の grep 結果 + 改修方針表。(4) §7 着手前確認事項を v0.4 用に更新、3A.3a 完了済をチェック、v0.4 承認 + 3A.3b 着手をペンディング | Claude (Opus 4.7) |
| 0.7 (draft) | 2026-05-26 | v0.6 ユーザー承認 (commit `a952efc`) + 3A.3e 完了 (commit `4549633`、542 件 pass) 後、3A.3f 着手前調査で **.odrg と Itinero RouterDb の頂点 ID / エッジ ID が独立採番**で Itinero との ID ベース突合は不可能と判明、ユーザー判断 4 件 (2026-05-26) で対応方針確定: P1 突合対象 = OdrgReader 真値のみ (Itinero 突合は 3A.6 で経路結果一致として担保) / P2 評価 API = BakedProfileEntry と一致 (Itinero タグ評価は Phase 2 で証明済み) / P3 エラー = 未開封 path + マジック改竄 2 件 / P4 サンプル = 頂点 100 / エッジ 100 / Shape 50 / 評価 50×2 profile。§4.3.6 を OdrgReader 真値突合 9 件版に再定義: GetVertex / GetEdgeEnumerator 反転 / GetEdge Shape / GetEdgeShape Span / キャッシュ動作 (Assert.Same) / EvaluateEdge en × Car/Pedestrian / EvaluateEdge RoadEdge × Car/Pedestrian / 未開封 path / マジック改竄。§6 テスト件数表で 3A.3e/3A.3f 実績マーク + ✅。§7 着手前確認事項を v0.7 用に更新、v0.6 承認 + 3A.3e 完了 + ユーザー判断 4 件確定 + 3A.3f 完了をチェック、v0.7 承認 + 3A.4 着手をペンディング | Claude (Opus 4.7) |
| 0.6 (draft) | 2026-05-26 | v0.5 ユーザー承認 (commit `10a2038`) + 3A.3b 完了 (commit `c46a2ca`、539 件 pass 維持) 後、3A.3e 着手前事前調査で `.odrg` / Itinero / 各実装の発見 5 件を整理 (§2.7 新設): F1 EDGE は頂点グループ化されていない → CSR インデックス必要 / F2 edgeCount は無向辺数 (仕様書 §1 表記の誤り、Itinero と数値一致) / F3 EDGE_SHAPE は中間点のみ (Itinero と一致) / F4 GeoCoordinate(Lat,Lon) と OdrgVertex(Lon,Lat) のフィールド順が逆 (直 Span 化不可) / F5 tsushima.odrg = 3.55 MB 確認。これに対するペンディング判断 4 件を §2.8 で起票 + ユーザー判断確定: L1 CSR (`firstOutEdge: uint[V+1]` + `OutEdgeEntry[2E]`) 採用 / L5 IRoadGraph に `GetEdgeShape(uint) -> ReadOnlySpan<GeoCoordinate>` 追加 + IRoadGraph : IDisposable 化 / L6 NativeEdgeEnumerator は class 毎回 new (Itinero と同じ) / L8 3A.3e で sanity 3 件 + 3A.3f で 9 件 (累計 12 件)。L2 EdgeCount セマンティクス / L3 Shape 端点扱い / L4 GeoCoordinate レイアウト / L7 Dispose 後アクセスは推奨案通り。§4.3.5 3A.3e を v0.6 詳細化: 事前作業 (IRoadGraph.GetEdgeShape 追加 + IRoadGraph : IDisposable 化 + Itinero 追従) + 実装 (NativeRoadGraph 全フィールド + コンストラクタ + IRoadGraph 全メソッド) + NativeEdgeEnumerator 詳細 + DistanceM 計算 (Haversine + キャッシュ) + sanity test 3 件。§4.3.6 3A.3f を 9 件版に再配分。§6 テスト件数表で 3A.3e の 0→3 件、3A.3f の 12→9 件に補正 + 累計 542/551 に補正。§7 着手前確認事項を v0.6 用に更新、v0.5 承認 + 3A.3b 完了 + ユーザー判断 4 件確定をチェック、v0.6 承認 + 3A.3e 着手をペンディング | Claude (Opus 4.7) |
| 0.5 (draft) | 2026-05-26 | v0.4 ユーザー承認 (commit `cd661d0`) 後、3A.3b 着手前事前調査で v0.4 §2.6.1 と現状コードのギャップを 3 件発見：(1) `DijkstraEngine.cs:42,46` は `RoadEdge.EdgeProfileIndex` 経由の評価呼出で `en` を持たない (v0.4 単一シグネチャ `EvaluateEdge(en, evaluator)` だけでは呼べない)。(2) `GetEdgeOsmTags(ushort)` 削除後は `EdgeWeightCalculator.Evaluate(ushort)` 内部から新 API を呼ぶ術がなく、v0.4 §4.3.2 で示唆した「案 α (3A.3b で暫定 fall back)」は**技術的に不成立**。(3) テスト 5 ファイル `IsCarHighway(tags["highway"])` ヘルパは `ProfileEvaluator.Evaluate(tags).CanPass` では粒度再現不可。ユーザー判断 3 件確定 (2026-05-26): (a) **RoadEdge オーバーロード追加** = `IRoadGraph.EvaluateEdge` を `(en, evaluator)` + `(RoadEdge, evaluator)` の 2 本に。(b) **Itinero テスト用 extension 新設** = `src/OsmDotRoute.Itinero/ItineroRoadGraphTestExtensions.cs` で `GetEdgeOsmTagsForTest` を提供、テストヘルパは `IsCarHighway` 判定を維持。(c) **案 β (3A.3b/3A.3c/3A.3d 統合) 採用、案 α 不採用**。反映: (1) §2.6.1 確定シグネチャを 2 オーバーロードに訂正、ギャップ発見記述追加。(2) §2.6.2 改修方針表を Itinero extension 経由に訂正、テスト改修パターン明示。(3) §2.6.3 新設 = Itinero テスト用 extension の設計 + InternalsVisibleTo 確認手順 + テスト書換パターン。(4) §4.3.2 を v0.4 3A.3b/c/d 統合版に全面書き直し、作業項目 8 段 + Done 基準 6 段 + v0.4 サブステップ対応表追加。(5) §4.3.3 / §4.3.4 を削除し v0.5 で 3A.3b に統合した旨を明示。(6) §6 テスト件数表で 3A.3c/3A.3d を取り消し線 (3A.3b に統合)、3A.3b の説明を統合版に書き換え、3A.3a 実績マーク。(7) §7 着手前確認事項を v0.5 用に更新、v0.4 承認済 + ユーザー判断 3 件確定をチェック、v0.5 承認 + 3A.3b 着手をペンディング | Claude (Opus 4.7) |
