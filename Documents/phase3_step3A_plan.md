# Phase 3 ステップ 3A: ランタイム `.odrg` 読込実装 計画書

**ステータス**: ドラフト v0.2（v0.1 ユーザー承認後、3A.1 着手前の現状確認で発見した訂正を反映、2026-05-26）
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

### 4.3 3A.3: `NativeRoadGraph` 実装（`IRoadGraph` 実装）

**実装**:

- `NativeRoadGraph : IRoadGraph, IDisposable`（`src/OsmDotRoute/Native/`、`public sealed class`）
- コンストラクタ: `NativeRoadGraph(string odrgPath)`
  - `OdrgMmfHandle` 内部生成
  - `OdrgSectionDirectory` でテーブル解決
  - 各セクション Span を field（`ReadOnlySpan<T>` は struct field にできないため、`OdrgMmfHandle` を保持して都度 Span 取得する形に変更 → 設計判断要）

**設計判断（重要）**:

`ReadOnlySpan<T>` は `ref struct` のため class の field にできない。Span を都度生成するための「セクション位置情報」を field に保持し、各 getter で Span を都度組み立てる：

```csharp
private readonly OdrgMmfHandle _handle;
private readonly int _vertexOffset, _vertexCount;
// ...

public ReadOnlySpan<OdrgVertex> Vertices => _handle.GetSpan<OdrgVertex>(_vertexOffset, _vertexCount);
public ReadOnlySpan<GeoCoordinate> GetEdgeShape(int edgeId)
{
    ref readonly var edge = ref _handle.GetSpan<OdrgEdge>(_edgeOffset, _edgeCount)[edgeId];
    return _handle.GetSpan<GeoCoordinate>(_shapeOffset + edge.ShapeOffset, edge.ShapeLength);
}
```

- `IRoadGraph` インターフェースを Phase 1 と互換維持（メソッド追加可、シグネチャ変更不可）
- `GetEdgeShape(edgeId) -> ReadOnlySpan<GeoCoordinate>` は `IRoadGraph` に**新規追加**するか、`NativeRoadGraph` 固有メソッドとして公開するかを本サブステップで判断（推奨: `IRoadGraph` に追加、`ItineroRoadGraph` 側はコピーで実装し 3C で削除予定）

**Done 基準**:

- 津島市 `.odrg` で `NativeRoadGraph` 構築成功
- 頂点列挙: 27,235 件、`ItineroRoadGraph` と座標 ±1e-7 度（≒1cm）以内で一致
- エッジ列挙: 38,004 件、両端頂点 ID / 距離 / プロファイルコストが完全一致
- `GetEdgeShape(edgeId)` で全 38,004 エッジのシェイプ列が `OdrgReader` のシェイプと完全一致
- `Dispose()` 後の Span アクセスは `ObjectDisposedException` を投げる
- xUnit テスト 12 件

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

---

## 6. テスト設計サマリ

**追加テスト件数（想定）**:

| サブステップ | 件数 | カテゴリ |
| --- | --- | --- |
| 3A.1 | 5 | セクションテーブルパース正常 / 異常 |
| 3A.2 | 7 | Span 切出 / Dispose 後アクセス |
| 3A.3 | 12 | `NativeRoadGraph` 頂点 / エッジ / シェイプ列挙 + Dispose |
| 3A.4 | 8 | R-tree クエリ正確性 / ブルートフォース突合 |
| 3A.5 | 183 | `NativeRoadSnapper` 178 + 解決失敗 5 |
| 3A.6 | 178 | 89 ペア × 2 実装 経路パリティ |
| **合計** | **393** | （Phase 2 累計 526 → Phase 3 3A 完了時 919） |

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
- [ ] 本ステップ計画書 v0.1 ユーザーレビュー → 承認
- [ ] 3A.1 着手

---

## 8. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
| --- | --- | --- | --- |
| 0.1 (draft) | 2026-05-26 | 初版起草。ユーザー判断 #21 (MMF=ファイナライザ併用) / #22 (Cache=ID 単位) / 3A 計画書置き場所 (新規) / DI 切替 (テスト内直接構築) / 並存テスト規模 (89 ペア × 2 実装 = 178) を反映。サブステップ 3A.1〜3A.6（6 段）、追加テスト 393 件、リスク R1〜R6。Phase 2 ステップ 5 計画書スタイル踏襲 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-26 | v0.1 ユーザー承認 (commit `b27be51`) 後、3A.1 着手前の現状確認で発見した訂正を反映：(1) §2.4 セクション構成表を実装確認済の 9 セクション (VERTEX / EDGE / EDGE_SHAPE / EDGE_AABB / EDGE_FLAG 独立 / SPATIAL_INDEX / BAKED_PROFILE / TURN_RESTRICTION / METADATA) に訂正、v0.1 の誤記 (PROFILE_BAKE / STRING_POOL、flags が EDGE 内、`バージョン 0x0002`) を訂正。(2) §3.1 スコープ内に「OdrgFormat を Extractor → Core へ移動」を前提リファクタとして追加（依存方向 Core ← Pbf ← Extractor のため）。(3) §4.1 (3A.1) を「ステップ 0: OdrgFormat Core 移動 / ステップ 1: OdrgSectionDirectory 実装」に分割、検証条件を `VersionMajor == 1` / `edgeFlagBytes == 2` / `sectionCount == 9` に具体化、参照真値を `OdrgReader.Read` に統一 | Claude (Opus 4.7) |
