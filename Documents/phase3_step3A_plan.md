# Phase 3 ステップ 3A: ランタイム `.odrg` 読込実装 計画書

**ステータス**: ドラフト v0.4（v0.3 ユーザー承認 commit `eb1431c` 後、§3A.3-API ユーザー判断 (a) 確定を反映 + 3A.3a 着手時の現状確認結果（`IRoadProfile` 不在 / `ProfileEvaluator.Name` 未公開）を反映、2026-05-26）
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

#### 2.6.1 (a) 案 確定後の詳細シグネチャ（3A.3a で確定、v0.4 で追記）

**3A.3a 着手時の現状確認**:

- `IRoadProfile` インターフェースは**存在しない**（v0.3 §2.6 (a) 案の架空型）
- 実体は `internal sealed class ProfileEvaluator`（`src/OsmDotRoute/Profiles/ProfileEvaluator.cs`）
- `ProfileEvaluator` は `_def: JsonProfileDefinition` を private 保持、`Name` プロパティは未公開
- `JsonProfileDefinition.Name: string?` は存在（プロファイル JSON の `name` フィールド）
- `EdgeWeightCalculator` のコンストラクタが `ProfileEvaluator evaluator` を受け取る既存構造

**確定シグネチャ**:

```csharp
internal interface IRoadGraph
{
    // 削除: IReadOnlyDictionary<string, string> GetEdgeOsmTags(ushort edgeProfileIndex);

    /// <summary>
    /// 現在エニュメレータが指すエッジを、指定 ProfileEvaluator で評価する。
    /// Phase 1 → Phase 3 セマンティック移行 (Phase 3 ステップ 3A.3b で導入)。
    /// </summary>
    /// <param name="en">エッジエニュメレータ（現在位置を保持）</param>
    /// <param name="evaluator">
    /// プロファイル評価器。Itinero 系: 内部で OSM タグを取得し <c>evaluator.Evaluate(tags)</c> を呼ぶ。
    /// Native 系: <c>evaluator.Name</c> で `.odrg` の BAKED_PROFILE スロットを解決し、bake 済値を直接返却。
    /// </param>
    /// <returns>エッジ評価結果（通行可否 / 速度 / 方向制限）</returns>
    EdgeEvaluation EvaluateEdge(IRoadGraphEdgeEnumerator en, ProfileEvaluator evaluator);
}
```

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

**`EdgeWeightCalculator` 改修**（3A.3c 内で実装）:

```csharp
// 旧:
public EdgeEvaluation Evaluate(ushort edgeProfileIndex)
{
    var tags = _graph.GetEdgeOsmTags(edgeProfileIndex);
    return _evaluator.Evaluate(tags);
}

// 新:
public EdgeEvaluation Evaluate(IRoadGraphEdgeEnumerator en)
    => _graph.EvaluateEdge(en, _evaluator);
```

呼出元 `EvaluateEdgeDurationSec(en)` は `Evaluate(en.EdgeProfileIndex)` → `Evaluate(en)` に変更。`DijkstraEngine.cs:42, 46` の sourceEdge / targetEdge 評価呼出も同様（`en` 相当を渡す）。

**`IRoadGraphEdgeEnumerator.EdgeProfileIndex`**: 保持（Itinero 系内部で必要、Native 系では未使用だが破壊しない）。3C で廃止検討。

**性能影響**: ホットパス内のメソッドコールは「`GetEdgeOsmTags` + `evaluator.Evaluate(tags)` の 2 段」→「`EvaluateEdge(en, evaluator)` の 1 段」に**短縮**。`ProfileEvaluator` 引数 1 個追加コストは無視可能。3E ベンチで実測。

#### 2.6.2 既存テスト 5 ファイル改修方針（3A.3a grep 結果）

`grep -rn "GetEdgeOsmTags"` ヒット箇所と改修方針：

| ファイル | 行 | 用途 | 改修方針 |
| --- | --- | --- | --- |
| `src/OsmDotRoute/Routing/EdgeWeightCalculator.cs` | 39 | 本番ホットパス | 新 `_graph.EvaluateEdge(en, _evaluator)` に置換（3A.3c） |
| `src/OsmDotRoute.Itinero/ItineroRoadGraph.cs` | 40-55 | Itinero 実装本体 | `EvaluateEdge` 実装に置換（内部で旧 `GetEdgeOsmTags` ロジック + `evaluator.Evaluate(tags)` を呼ぶ）（3A.3b） |
| `tests/OsmDotRoute.Tests/ItineroAdapterTests.cs` | 67, 76 | タグ取得アダプタテスト | テストの意図が「Itinero タグが正しく取れる」なので、新 API では `EvaluateEdge` 結果が ProfileEvaluator + Itinero タグ評価と一致することを assert する形にリネーム + 書き換え（3A.3b） |
| `tests/OsmDotRoute.Tests/CalculateRouteTests.cs` | 195, 225 | テスト内ヘルパで `tags` を取得 | 当該ヘルパ部分を新 API 経由に置換（3A.3b 或いは 3A.3d で取りこぼし回収） |
| `tests/OsmDotRoute.Tests/SnapToRoadTests.cs` | 129 | 同上 | 同上 |
| `tests/OsmDotRoute.Tests/RestrictedRoutingTests.cs` | 276 | 同上 | 同上 |

**3A.3a 完了条件 (Done)**: 上記シグネチャ + 改修方針が計画書 v0.4 として commit され、ユーザー承認を得る。実コード変更ゼロ、539 件 pass 維持。

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

#### 4.3.2 3A.3b: `ItineroRoadGraph` を新 API に追従改修

**作業**:

- `src/OsmDotRoute.Itinero/ItineroRoadGraph.cs` の新 API 実装（既存 `GetEdgeOsmTags` ロジック + ProfileEvaluator を内部で呼び bake-equivalent な値を返す）
- `IRoadGraph` インターフェース改修（`GetEdgeOsmTags` 削除 / 新 API 追加）
- `IRoadGraphEdgeEnumerator` の `EdgeProfileIndex` の扱い確定
- `tests/OsmDotRoute.Tests/ItineroAdapterTests.cs` の `GetEdgeOsmTags_ParentDefault_ContainsHighwayKey` 含む直呼び 5 ファイルの追従修正

**Done 基準**:

- `dotnet test` 全 pass（539 件維持、`ItineroRoadGraph` 経由のテストが新 API で動く）
- `ItineroRoadGraph.GetEdgeOsmTags` が削除されている（compile error が出ないこと）
- 経路探索エンジン（`DijkstraEngine` / `EdgeWeightCalculator`）の動作が Phase 1 と完全一致（既存 89 ペア経路テスト全 pass）

**commit メッセージ案**: `refactor: Phase 3 ステップ 3A.3b IRoadGraph 評価 API 改修 + ItineroRoadGraph 追従`

---

#### 4.3.3 3A.3c: `EdgeWeightCalculator.Evaluate` 内部置換

**作業**:

- `src/OsmDotRoute/Routing/EdgeWeightCalculator.cs:37-41` の `Evaluate(ushort edgeProfileIndex)` を新 API ベースに書き換え
- `EvaluateEdgeDurationSec(IRoadGraphEdgeEnumerator en)` 内の `Evaluate(en.EdgeProfileIndex)` 呼出を新 API シグネチャに合わせて更新
- `DijkstraEngine.cs:42, 46` の sourceEdge / targetEdge 評価呼出も整合

**Done 基準**:

- `dotnet test` 全 pass（539 件維持）
- Dijkstra ホットパスが新 API 経由で動く
- `ProfileEvaluator.Evaluate(IDictionary)` 経路は **Itinero 側でのみ呼ばれる**（`ItineroRoadGraph` 内部）形に隔離

**commit メッセージ案**: `refactor: Phase 3 ステップ 3A.3c EdgeWeightCalculator.Evaluate を新 IRoadGraph 評価 API 経由に置換`

---

#### 4.3.4 3A.3d: 既存テスト 5 ファイル追従最終確認

**作業**:

- 3A.3b で大半は対応済の想定。本サブステップでは取りこぼし回収
- `tests/OsmDotRoute.Tests/{CalculateRouteTests, SnapToRoadTests, RestrictedRoutingTests, ItineroAdapterTests, ...}.cs` のテストヘルパや fixture が新 API を前提とした記述になっているか最終確認
- 削除された `GetEdgeOsmTags` を参照するテストが残っていれば修正
- 廃止された XML doc（`IRoadGraphEdgeEnumerator.EdgeProfileIndex` の説明文等）の整理

**Done 基準**:

- `dotnet test` 全 pass（539 件維持）
- 警告 0（CS0618 廃止参照等の警告がない）
- `grep -rn "GetEdgeOsmTags"` でヒット 0 件

**commit メッセージ案**: `chore: Phase 3 ステップ 3A.3d 旧 IRoadGraph 評価 API 参照の最終クリーンアップ`

---

#### 4.3.5 3A.3e: `NativeRoadGraph` 新規実装

**作業**:

- `src/OsmDotRoute/Native/NativeRoadGraph.cs` 新規（`public sealed class : IRoadGraph, IDisposable`）
- コンストラクタ: `NativeRoadGraph(string odrgPath)` → 内部で `OdrgMmfHandle.Open` + `OdrgSectionDirectory.Read`
- セクション位置情報を field 保持（`ReadOnlySpan<T>` は class field にできないため、各 getter で都度 Span 取得）
- `IRoadGraph` 全メソッド実装:
  - `VertexCount` / `EdgeCount` / `GetVertex` / `GetBounds`: HEADER + VERTEX セクション参照
  - `GetEdgeEnumerator(uint vertexId)`: VERTEX → EDGE 走査用 `NativeEdgeEnumerator` を新規実装（`IRoadGraphEdgeEnumerator` 実装、struct or class は性能要件で判断）
  - `GetEdge(uint edgeId)`: EDGE + EDGE_SHAPE セクションから `RoadEdge` 組立（`Shape` は当面 `List<GeoCoordinate>` コピー、3C で `ReadOnlyMemory<T>` 化）
  - 新評価 API（§3A.3-API 確定済）: BAKED_PROFILE セクションから直接取得
- `Dispose()`: 内部 `OdrgMmfHandle.Dispose()`

**Done 基準**:

- 津島市 `.odrg` で `NativeRoadGraph` 構築成功
- 頂点列挙: 27,235 件、`ItineroRoadGraph` と座標 ±1e-7 度（≒1cm）以内で一致
- エッジ列挙: 38,004 件、両端頂点 ID / 距離 / プロファイルコストが完全一致
- `Dispose()` 後のアクセスは `ObjectDisposedException` を投げる
- `dotnet test` 全 pass（539 件維持、NativeRoadGraph 単体テストは 4.3.6 で追加）

**commit メッセージ案**: `feat: Phase 3 ステップ 3A.3e NativeRoadGraph 新規実装 (IRoadGraph 実装、ItineroRoadGraph と並存)`

---

#### 4.3.6 3A.3f: `NativeRoadGraph` テスト 12 件追加 + 並存パリティ確認

**作業**:

- `tests/OsmDotRoute.Tests/Native/NativeRoadGraphTests.cs` 新規（12 件）
  - 頂点列挙パリティ（`ItineroRoadGraph` との座標一致）
  - エッジ列挙パリティ（両端 / 距離 / プロファイルコスト）
  - `GetEdge(edgeId)` シェイプ一致（`OdrgReader` 真値との完全一致）
  - 新評価 API 結果が ProfileEvaluator + Itinero タグ評価と一致
  - `Dispose()` 後例外、未開封 path、不正 `.odrg` 等のエラーケース
- `IClassFixture<NativeAndItineroGraphFixture>` で `.odrg` + `.routerdb` を同時ロード

**Done 基準**:

- xUnit テスト 12 件全 pass
- 累計 539 + 12 = **551 件 pass**
- 並存パリティ実測値が記録される（3A.6 178 経路パリティの前段）

**commit メッセージ案**: `feat: Phase 3 ステップ 3A.3 完了 (NativeRoadGraph + IRoadGraph 評価 API 改修)`

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
| 3A.3a | 0 | API 改修案ドラフト（コード変更なし） | 計画書 v0.3 |
| 3A.3b | 0 | `ItineroRoadGraph` 改修 + 既存テスト追従 (件数増減なし、539 維持) | 計画書 v0.3 |
| 3A.3c | 0 | `EdgeWeightCalculator.Evaluate` 内部置換 (件数増減なし) | 計画書 v0.3 |
| 3A.3d | 0 | 既存テスト追従最終確認 (件数増減なし) | 計画書 v0.3 |
| 3A.3e | 0 | `NativeRoadGraph` 新規実装 (テストは 3A.3f) | 計画書 v0.3 |
| 3A.3f | 12 | `NativeRoadGraph` 頂点 / エッジ / シェイプ / 新評価 API + Dispose | 計画書 v0.3 |
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
- [ ] 本ステップ計画書 v0.4 ユーザーレビュー → 承認
- [ ] 3A.3b 着手

---

## 8. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
| --- | --- | --- | --- |
| 0.1 (draft) | 2026-05-26 | 初版起草。ユーザー判断 #21 (MMF=ファイナライザ併用) / #22 (Cache=ID 単位) / 3A 計画書置き場所 (新規) / DI 切替 (テスト内直接構築) / 並存テスト規模 (89 ペア × 2 実装 = 178) を反映。サブステップ 3A.1〜3A.6（6 段）、追加テスト 393 件、リスク R1〜R6。Phase 2 ステップ 5 計画書スタイル踏襲 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-26 | v0.1 ユーザー承認 (commit `b27be51`) 後、3A.1 着手前の現状確認で発見した訂正を反映：(1) §2.4 セクション構成表を実装確認済の 9 セクション (VERTEX / EDGE / EDGE_SHAPE / EDGE_AABB / EDGE_FLAG 独立 / SPATIAL_INDEX / BAKED_PROFILE / TURN_RESTRICTION / METADATA) に訂正、v0.1 の誤記 (PROFILE_BAKE / STRING_POOL、flags が EDGE 内、`バージョン 0x0002`) を訂正。(2) §3.1 スコープ内に「OdrgFormat を Extractor → Core へ移動」を前提リファクタとして追加（依存方向 Core ← Pbf ← Extractor のため）。(3) §4.1 (3A.1) を「ステップ 0: OdrgFormat Core 移動 / ステップ 1: OdrgSectionDirectory 実装」に分割、検証条件を `VersionMajor == 1` / `edgeFlagBytes == 2` / `sectionCount == 9` に具体化、参照真値を `OdrgReader.Read` に統一 | Claude (Opus 4.7) |
| 0.3 (draft) | 2026-05-26 | 3A.1 完了 (commit `fb6cd45`) / 3A.2 完了 (commit `279a6ec`) 後、3A.3 着手前の `IRoadGraph` 依存連鎖調査で発見した重大な設計問題を反映：(1) §2.5 追加 = `.odrg` には OSM タグ生データなし → `NativeRoadGraph.GetEdgeOsmTags` 実装不可、`IRoadGraph` 改修必須。ユーザー判断 B 案 (3A.3 で `IRoadGraph` 改修込み) 確定。(2) §2.6 追加 = 着手前ペンディング判断 §3A.3-API 起票 (新評価 API シグネチャ a/b/c)、推奨 (a) `EdgeEvaluation EvaluateEdge`。(3) §4.3 を B 案サブステップ詳細 3A.3a〜3A.3f に全面書き直し（API 改修案ドラフト / `ItineroRoadGraph` 追従 / `EdgeWeightCalculator` 内部置換 / 既存テスト追従 / `NativeRoadGraph` 新規 / テスト 12 件）。各サブステップで `dotnet test` 全 pass 維持。(4) §5 リスク表に 3A-R7 (改修で既存テスト破壊) / 3A-R8 (新 API がホットパス不適合) 追加。(5) §6 テスト件数表に 3A.3 サブステップ分割反映 + 3A.1/3A.2 実績反映 (5+8、累計 539)。(6) §7 着手前確認事項を v0.3 用に更新。3A.1〜3A.2 完了済をチェック、§3A.3-API 確定 + v0.3 承認をペンディング | Claude (Opus 4.7) |
| 0.4 (draft) | 2026-05-26 | v0.3 ユーザー承認 (commit `eb1431c`) + §3A.3-API (a) ユーザー判断確定後、3A.3a 成果物を反映：(1) §2.6 確定マーク追記。(2) §2.6.1 追加 = (a) 案 確定後の詳細シグネチャ。3A.3a 着手時の現状確認で `IRoadProfile` 不在（v0.3 §2.6 の架空型）/ `ProfileEvaluator.Name` 未公開を発見、確定シグネチャを `EvaluateEdge(IRoadGraphEdgeEnumerator, ProfileEvaluator)` に補正。`ProfileEvaluator.Name` プロパティ追加方針 + `EdgeWeightCalculator` 改修コード骨格 + `IRoadGraphEdgeEnumerator.EdgeProfileIndex` の扱い (保持、3C で廃止検討) も決定。(3) §2.6.2 追加 = 既存テスト 5 ファイル `GetEdgeOsmTags` 直呼び 6 箇所 (本番 2 + テスト 4) の grep 結果 + 改修方針表。(4) §7 着手前確認事項を v0.4 用に更新、3A.3a 完了済をチェック、v0.4 承認 + 3A.3b 着手をペンディング | Claude (Opus 4.7) |
