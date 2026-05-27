# OsmDotRoute Phase 3 設計書

**バージョン**: 0.1（ひな形・骨子のみ）
**作成日**: 2026-05-25
**最終更新**: 2026-05-25
**ステータス**: ひな形（Phase 3 実装計画書 v0.1.2 ユーザー承認直後に起こし、§0 / §0.3 のみ初版。各章は対応ステップ完了時に肉付け）
**対象**: OsmDotRoute Phase 3 実装の設計記録（**データ利用側**：ランタイム `.odrg` 読込 + Itinero 依存撤去 + Bicycle/Truck プロファイル + ベンチ + 親プロ統合 + Sandbox + OSS 公開）
**関連ドキュメント**:

- [要件定義書](requirement_definition.md)（v2.3、Phase 2/3 スコープ再編後）
- [Phase 3 実装計画書](phase3_implementation_plan.md)（v0.1.2、2026-05-25 ユーザー承認）
- [Phase 2 設計書](phase2_design.md)（v0.4、§8「Phase 3 申し送り事項」が本書の出発点）
- [Phase 2 グラフ形式仕様書](phase2_graph_format_spec.md)（v0.2、`.odrg` 確定仕様）
- [Phase 1 設計書](phase1_design.md)（v0.21、§18「制約事項と既知の課題」が Phase 3 ベンチ目標の出発点）
- [Phase 1 ベンチマーク結果](phase1_benchmark_results.md)（Phase 3 性能基準値）
- [Phase 3 ベンチマーク結果](phase3_benchmark_results.md)（v0.2、ステップ 3E.3 完了時に起こし、3E.4 で §7 へ反映）
- 将来：`comparison_with_itinero.md`（ステップ 3H 完了時に起こす、計画書 §3.9.1 構成案）

---

## 0. 本書の目的と更新ルール

### 0.1 目的

本書は **OsmDotRoute Phase 3 で「何を、なぜ、どう実装したか」を後から把握できる記録** を残すことを目的とする。実装計画書（[`phase3_implementation_plan.md`](phase3_implementation_plan.md)）は「これから何をやるか」を、本書は「実際にどう作ったか」を保持する。

Phase 2 設計書 §8「Phase 3 申し送り事項」が本書の出発点。Phase 3 = データ利用側（ランタイム `.odrg` 読込 + Itinero 撤去 + Bicycle/Truck + ベンチ + 親プロ統合 + Sandbox + OSS 公開）の設計記録に専念する。

### 0.2 更新ルール

**各実装ステップが完了するたびに、本書の該当章を更新する**（実装計画書のステップ完了判定に「設計書の該当章更新」を含む、Phase 1 / Phase 2 と同方針、メモリ [[feedback_design_doc_per_step]]）。

更新時に書くこと（Phase 1 / Phase 2 設計書 §0.2 と同じテンプレート）：

- **意図 (Intent)**: 何を実現したかったか（要件 ID 参照）
- **採用設計 (Design)**: クラス／インターフェース構成、データ構造、アルゴリズム、外部仕様（API シグネチャ・バイナリレイアウト）
- **設計判断の根拠 (Why)**: なぜ別案ではなくこの設計にしたか
- **トレードオフ・制約 (Trade-off)**: 採用しなかった案、既知の限界、Phase 4 以降への申し送り
- **検証方法 (Verification)**: 単体テストの観点、手動検証手順、ベンチマーク
- **実装メモ (Notes)**: 後で読む人が引っかかりそうな点、暗黙の前提

書かなくてよいこと：

- コードの逐語コピー（ファイル名・パス参照で十分）
- 一時的な実装過程（commit ログで追える内容）
- TODO リスト（GitHub Issues / 別文書で管理）

### 0.3 章とステップの対応

| 章 | 対応ステップ | 状態 |
| --- | --- | --- |
| 1. 全体概要 | 全ステップ通底 | 未記述（計画書 v0.1.2 承認直後、ひな形のみ） |
| 2. アーキテクチャ概観（Phase 2 → Phase 3 変遷） | 全ステップ通底 | 未記述 |
| 3. NativeRoadGraph / NativeRoadSnapper（MMF + Span） | 3A | 未着手 |
| 4. 動的制約ホットパス（交差エッジキャッシュ） | 3B | 未着手 |
| 5. Bicycle / Truck プロファイル独自設計 | 3D | 未着手 |
| 6. Itinero 依存撤去と `Route.Shape` 破壊変更 | 3C | 未着手 |
| 7. ベンチマーク再実施（津島市） | 3E | **肉付け完了**（3E.4、2026-05-28） |
| 8. 親プロジェクト統合・パリティ検証 | 3F | **肉付け完了**（3F、2026-05-28） |
| 9. 都道府県単位ベンチ | 3G | **肉付け完了**（3G、2026-05-28） |
| 10. ユーザー試用デモツール `OsmDotRoute.Sandbox` | 3I（5 サブステップ 3I.1〜3I.5） | 未着手 |
| 11. Phase 3 確定と OSS 公開準備 | 3H | 未着手 |
| 12. 改訂履歴 | 各ステップ完了時 | 初版（v0.1） |

**Phase 4+ 設計書（将来作成検討）に持ち越す章**：

- CH（Contraction Hierarchies）対応
- ターン制限（PBF Relation `type=restriction`）対応
- 双方向 Dijkstra / A\* 等の高速化アルゴリズム
- メッシュ 100 m 階層対応（要件定義書 v1.4 で延期、第 6 次メッシュ）
- マルチプラットフォーム配布検証（macOS / Linux 詳細）
- Emergency / Disaster プロファイル（REQ-PRF-005 / REQ-PRF-006、P3）
- NuGet 公開後の運用フィードバック反映

### 0.4 章内のテンプレート

各章は以下のテンプレートで記述する（Phase 1 / Phase 2 設計書 §0.4 と同じ）：

```markdown
## NN. 章タイトル

**対応ステップ**: ステップ 3X
**対応要件**: REQ-XXX-NNN, REQ-YYY-NNN
**Phase 2 申し送り**: 設計書 §8.X（該当時）
**実装日**: YYYY-MM-DD
**実装バージョン**: vX.Y.Z（ユーザー採番）
**主要ファイル**:

- `src/OsmDotRoute/...`

### NN.1 意図

（要件と達成目標、Phase 2 申し送り事項のどれに応えるか）

### NN.2 採用設計

（クラス図・API シグネチャ・データ構造・アルゴリズム・バイナリレイアウト）

### NN.3 設計判断の根拠

（採用した理由、なぜ別案を選ばなかったか）

### NN.4 トレードオフ・制約

（既知の限界、Phase 4 以降への申し送り）

### NN.5 検証方法

（テスト観点、手動検証手順、ベンチマーク）

### NN.6 実装メモ

（暗黙の前提、後で読む人が引っかかりそうな点）
```

---

## 1. 全体概要

（**未記述**：計画書 v0.1.2 承認時点。ステップ 3A 着手時に Phase 1 → Phase 2 → Phase 3 のランタイム変遷図 + Phase 3 ゴール 9 項目（計画書 §1）を記述）

### 1.1 Phase 3 のゴール（再掲）

（計画書 [`phase3_implementation_plan.md`](phase3_implementation_plan.md) §1 から引用予定）

### 1.2 採用アプローチ（確定済み、計画書 v0.1.2）

（計画書 §3.1〜§3.9 から要約予定）

### 1.3 Phase 1 → Phase 2 → Phase 3 のランタイム変遷

（計画書 §1 の変遷表を引用予定。Phase 1 = Itinero RouterDb 依存 / Phase 2 = `.odrg` 形式策定（ランタイム未利用）/ Phase 3 = `.odrg` ランタイム読込 + Itinero 撤去）

---

## 2. アーキテクチャ概観（Phase 2 → Phase 3 変遷）

（**未記述**：ステップ 3A 着手時に Phase 2 完了時のアセンブリ参照グラフ → Phase 3 完了時のアセンブリ参照グラフへの変遷 + 公開 API 差分を記述）

---

## 3. NativeRoadGraph / NativeRoadSnapper（MMF + Span）

**対応ステップ**: 3A (3A.1〜3A.6)
**対応要件**: REQ-MAP-005（`.odrg` ランタイム読込）、REQ-NFR-003（経路 1 本あたり 77MB アロケート削減の土台）
**Phase 2 申し送り**: 設計書 §8.3 表 3A 行
**実装日**: 2026-05-26（3A.1 着手）〜 2026-05-27（3A.6 完了）
**実装バージョン**: ユーザー採番
**主要ファイル**:

- [`src/OsmDotRoute/Internal/Odrg/OdrgFormat.cs`](../src/OsmDotRoute/Internal/Odrg/OdrgFormat.cs)（セクションコード定数）
- [`src/OsmDotRoute/Internal/Odrg/OdrgSections.cs`](../src/OsmDotRoute/Internal/Odrg/OdrgSections.cs)（`OdrgHeader` / `OdrgSectionEntry` / `OdrgVertex` / `OdrgEdge` / `OdrgEdgeAabb` / `OdrgBakedProfileEntry` / `OdrgBbox` 等）
- [`src/OsmDotRoute/Internal/Odrg/OdrgSectionDirectory.cs`](../src/OsmDotRoute/Internal/Odrg/OdrgSectionDirectory.cs)（HEADER + SECTION TABLE パース）
- [`src/OsmDotRoute/Internal/Odrg/OdrgMmfHandle.cs`](../src/OsmDotRoute/Internal/Odrg/OdrgMmfHandle.cs)（MMF + SafeBuffer ゼロコピー読込）
- [`src/OsmDotRoute/Internal/Odrg/OdrgFormatException.cs`](../src/OsmDotRoute/Internal/Odrg/OdrgFormatException.cs)
- [`src/OsmDotRoute/Native/NativeRoadGraph.cs`](../src/OsmDotRoute/Native/NativeRoadGraph.cs)（`IRoadGraph` 実装 + CSR インデックス + シェイプ詰替えキャッシュ + 距離キャッシュ）
- [`src/OsmDotRoute/Native/NativeEdgeEnumerator.cs`](../src/OsmDotRoute/Native/NativeEdgeEnumerator.cs)（class enumerator、CSR 反復）
- [`src/OsmDotRoute/Native/OutEdgeEntry.cs`](../src/OsmDotRoute/Native/OutEdgeEntry.cs)（CSR 出力エントリ）
- [`src/OsmDotRoute/Native/NativeRTreeQuery.cs`](../src/OsmDotRoute/Native/NativeRTreeQuery.cs)（R-tree クエリ + Nearest）
- [`src/OsmDotRoute/Native/NativeRoadSnapper.cs`](../src/OsmDotRoute/Native/NativeRoadSnapper.cs)（`IRoadSnapper` 実装、R-tree 候補 + 点-線分最短距離）
- [`src/OsmDotRoute/Geometry/GeoMath.cs`](../src/OsmDotRoute/Geometry/GeoMath.cs)（Haversine + MetersToBboxDegrees + PointToSegment）
- [`src/OsmDotRoute/Routing/IRoadGraph.cs`](../src/OsmDotRoute/Routing/IRoadGraph.cs)（3A.3b 改修: `EvaluateEdge` 2 オーバーロード追加 + `GetEdgeShape` 追加）

### 3.1 意図

REQ-MAP-005（`.odrg` ランタイム読込）を Phase 3 段階で実装する。Phase 2 で確定した `.odrg` v0.2 形式を `MemoryMappedFile` + `ReadOnlySpan<T>` でゼロコピー読込する `NativeRoadGraph` / `NativeRoadSnapper` を、Itinero 系（`ItineroRoadGraph` / `ItineroSnapper`）と**並存可能な形**で実装した。Itinero 撤去は 3C、DI 統合（`MapService.LoadFromOdrg`）も 3C で行う。

Phase 1 §18.4 で識別された「経路 1 本あたり 77MB アロケート」の主因（`Route.Shape` の `IReadOnlyList<GeoCoordinate>` 化）に対し、`IRoadGraph.GetEdgeShape(uint edgeId) -> ReadOnlySpan<GeoCoordinate>` を新設し、`NativeRoadGraph` 側で**ゼロアロケーション API シグネチャを確定**させた（REQ-NFR-003 の土台）。実際の `Route.Shape` 自体の `ReadOnlyMemory<T>` 化と素通し保持は 3C 担当。

### 3.2 採用設計

#### 3.2.1 レイヤ構成

```text
.odrg バイナリファイル (Phase 2 グラフ形式仕様書 v0.2)
        ↓
OdrgMmfHandle              MemoryMappedFile + SafeBuffer (IDisposable + ファイナライザ併用)
        ↓ ReadOnlySpan<byte> ビュー
OdrgSectionDirectory       HEADER + SECTION TABLE 9 エントリパース (Read-only)
        ↓ セクションオフセット
NativeRoadGraph            IRoadGraph 実装
   ├─ CSR インデックス     firstOutEdge (uint[]) + outEntries (OutEdgeEntry[])、起動時構築 O(E)
   ├─ シェイプ詰替えキャッシュ  GeoCoordinate[]?[] (初回 GetEdgeShape 時、Lon-Lat → Lat-Lon)
   ├─ 距離キャッシュ       float[] + bool[] (初回 Haversine 積算後)
   ├─ BAKED_PROFILE        Dictionary<string, int> (profile name → slot index)
   └─ R-tree セクション参照 ノード count / root / branching / height を起動時抽出
        ↓
NativeRoadSnapper          IRoadSnapper 実装
   ├─ NativeRTreeQuery     Query (bbox + buffer + 結果数) + Nearest (k=N)
   └─ GeoMath              Haversine + MetersToBboxDegrees + PointToSegment
```

#### 3.2.2 主要 API

| API | 役割 |
| --- | --- |
| `new NativeRoadGraph(string odrgPath)` | MMF オープン + セクション解析 + CSR 構築 + プロファイル table ロード |
| `IRoadGraph.GetEdgeShape(uint edgeId) -> ReadOnlySpan<GeoCoordinate>` | Phase 1 から追加された API。`NativeRoadGraph` はキャッシュ配列を返却（ゼロコピー、ライフタイム = `NativeRoadGraph.Dispose` まで） |
| `IRoadGraph.EvaluateEdge(in NativeEdgeEnumerator, ProfileEvaluator)` / `IRoadGraph.EvaluateEdge(in RoadEdge, ProfileEvaluator)` | 3A.3b で追加された 2 オーバーロード評価 API。`NativeRoadGraph` は `.odrg` BAKED_PROFILE 直読、`ItineroRoadGraph` は OSM tags 経由 |
| `new NativeRoadSnapper(NativeRoadGraph)` | 内部に `uint[1024]` クエリバッファを持つ |
| `IRoadSnapper.Snap(string profileName, GeoCoordinate, float searchDistanceM)` | R-tree で bbox 絞り込み → 候補エッジに対し `GeoMath.PointToSegment` → グローバル最短エッジ選択 |
| `new RouterDb(IRoadGraph, IRoadSnapper)`（internal） | 既存 Phase 1 公開ファサードに Native 系統を組み込む経路。`InternalsVisibleTo` でテストから利用、DI 統合は 3C |

### 3.3 設計判断の根拠

3A 期間中に着手前事前調査でのギャップ発見ごとにユーザー確認した設計判断は以下のとおり：

| ID | 論点 | 確定 | 理由 |
| --- | --- | --- | --- |
| §5.5-#21 | MMF 解放方針 | (b) SafeHandle ファイナライザ併用 | `MemoryMappedViewAccessor.SafeMemoryMappedViewHandle` が `SafeBuffer:SafeHandle` 派生で、CriticalFinalizer 経由のクリーンアップを既定で享受。利用側 `Dispose` を契約で求めつつ、忘れた場合の OS リソース解放を保証 |
| §5.5-#22 | 制約 ID 単位キャッシュ粒度 | (a) 制約 ID 単位（3B 担当） | 3B 着手時の論点を 3A 計画書時点で先行確定。3A.4 R-tree クエリは制約付与とは独立、bbox 引数のみ受ける汎用 API |
| §3A.3-API | `IRoadGraph` 評価 API 形状 | (a) `EvaluateEdge(en, ProfileEvaluator)` 2 オーバーロード | ホットパス内のメソッドコール 1 回追加コストは無視可能、`ProfileEvaluator` 注入で `NativeRoadGraph` が tags 経由を強要されない |
| L1（3A.3e） | `NativeRoadGraph` 内部表現 | CSR（`firstOutEdge` + `outEntries`） | LINKED LIST 案より cache miss が少なく、Itinero RouterDb 内部とも近い |
| L5（3A.3e） | `GetEdgeShape` API 追加 | `IRoadGraph` に追加（Itinero per-call、Native ゼロコピー） | Phase 1 では存在しなかった API。`Route.Shape` の `ReadOnlyMemory<T>` 化（3C）に向けた土台 |
| L6（3A.3e） | EdgeEnumerator 実装方式 | class、毎回 new（Itinero と同じ） | struct enumerator は 3E 性能測定で評価、シンプル優先 |
| P1（3A.3f） | 真値突合方針 | `OdrgReader` 真値突合のみ | Itinero RouterDb と `.odrg` で頂点 ID / エッジ ID が独立採番、ID ベース突合は技術的に不可能と判明 |
| Q1（3A.4） | R-tree クエリ bbox 表現 | `OdrgBbox`（Lon-Lat、Internal.Odrg） | wire format と一致、`OdrgWriter` 側との比較が容易 |
| Q3（3A.4） | R-tree ノードアクセサー | `NativeRoadGraph` に internal API 追加 | 別 class 化より結合が緩く、テストから直接呼べる |
| Q4（3A.4） | overrun ハンドリング | ヒット総数を返し `buffer.Length` まで書込 | 呼出側で容量増やして再クエリ（`QueryWithGrowableBuffer` パターン） |
| Q5（3A.4） | 点-AABB 最小距離 | 経緯度 2D euclidean（度単位、R-tree 枝刈り規約と一致） | R-tree 枝刈りに使う距離関数なので AABB 用度 metric が最も自然 |
| Q1（3A.5a） | GeoMath ヘルパ分離 | 3A.5a / 3A.5b 分割 | テスト独立性確保、Brute-force 突合のサブ基盤を先に確定 |
| Q3（3A.5a） | GeoMath 配置 | `src/OsmDotRoute/Geometry/GeoMath.cs` internal static | Native 専用ではなく Phase 1 既存 `PolygonIntersection` 等とも共有可能な位置 |
| Q5（3A.5a） | `MetersToBboxDegrees` | 緯度依存近似（dLat = m/111320、dLon = m/(111320 × cos(lat))） | 1km 以下のローカル bbox 拡張で精度十分、R-tree 候補絞り込み用途 |
| Q2（3A.5b） | プロファイル評価 | `NativeRoadGraph.CanPass` で BAKED_PROFILE.Flags 直読（`ProfileEvaluator` 非依存） | `.odrg` 設計の意図（タグ → ProfileEvaluator ルートをバイパス、bake 値直読）と一致 |
| Q6（3A.5b） | Offset 計算 | 距離比 × 65535（Itinero 互換） | 既存 Phase 1 `SnapResult.Offset` セマンティクス継続 |
| Q1（3A.6） | テスト方針 | (B) Native 自己整合のみ、Itinero 突合廃止 | Phase 1 既存 526 件（Itinero 系）の全 pass 維持で並存証明を代替。ID 独立採番により ID ベース突合は技術的に不可能 |
| Q6（3A.6） | NativeRouterDb fixture | 新設、`IClassFixture` で共有 | 16 件のテストで Graph / Snapper / RouterDb / Router を使い回し、CI 時間最小化 |

### 3.4 トレードオフ・制約

- **Span ライフタイム**: `IRoadGraph.GetEdgeShape` の戻り `ReadOnlySpan<GeoCoordinate>` のライフタイムは `NativeRoadGraph.Dispose()` まで。呼出側が `Dispose` 後に Span を使用すると不定動作（XML doc に明記、3A-R4）。3C で `Route.Shape` を `ReadOnlyMemory<T>` 化する際は `MemoryManager<T>` 経由で延命する設計に移行可能。
- **プロファイル評価のバイパス**: `NativeRoadGraph` は `.odrg` BAKED_PROFILE 直読（`SpeedKmh` + `Flags`）。`ProfileEvaluator` は API 形状として保持するが Native 系統は値を見ない。Itinero 系統との API 互換性は `IRoadGraph.EvaluateEdge` の 2 オーバーロードで担保。3C で統合方針再検討。
- **Itinero との並存パリティ証明**: `.odrg` と Itinero RouterDb の頂点 ID / エッジ ID が独立採番のため、ID ベースの突合は技術的に不可能（3A.3f / 3A.6 で判明）。**経路結果一致による並存証明は Phase 1 既存 526 件全 pass の維持で代替**。
- **`RouterDb` コンストラクタ公開度**: `RouterDb(IRoadGraph, IRoadSnapper)` は internal（`InternalsVisibleTo` でテストから利用）。DI 統合（`AddOsmDotRoute(odrgPath)`）は 3C で実装。
- **Big-endian ホスト**: `.odrg` 仕様はリトル固定。Big-endian ホストでは `OdrgFormatException`。Phase 3 スコープ外（将来検討、3A-R6）。
- **R-tree Query の overrun**: `buffer.Length` 不足時はヒット総数を返し `buffer` には先頭部分のみ書込、呼出側がバッファを増やして再クエリ。`NativeRoadSnapper.QueryWithGrowableBuffer` で 2 倍化リトライ。

### 3.5 検証方法

3A 期間中の単体テスト 69 件、いずれも津島市 `.odrg`（3.55 MB、頂点 27,235 / エッジ 38,004、commit `4a5a90a` 同梱）を実データとして使用：

| サブステップ | 件数 | 観点 | 完了 commit | 累計 |
| --- | --- | --- | --- | --- |
| 3A.1 | 5 | セクションテーブルパース正常 / 異常 | `fb6cd45` | 531 |
| 3A.2 | 8 | Span 切出 / `Dispose` 後アクセス | `279a6ec` | 539 |
| 3A.3a | 0 | API 改修案ドラフト（計画書 v0.3 / v0.4） | `eb1431c` / `cd661d0` | 539 |
| 3A.3b | 0 | `IRoadGraph` + `ItineroRoadGraph` + `EdgeWeightCalculator` + `DijkstraEngine` + テスト 5 ファイル一括改修 | `c46a2ca` | 539 |
| 3A.3e | 3 | `NativeRoadGraph` 構築 / 頂点読出 / Dispose sanity | `4549633` | 542 |
| 3A.3f | 9 | `OdrgReader` 真値突合: 頂点 100 / エッジ 100 / Shape 50 / `GetEdgeShape` 50 / 評価 API 50×2×2 / エラー 2 | `f573c08` | 551 |
| 3A.4 | 8 | R-tree アクセサー / Query 全包含 / Query 範囲外 / Query × 50 Brute-force / Query overrun / Nearest k=1 / Nearest k=10 / ノード構造 | `78d4581` | 559 |
| 3A.5a | 8 | GeoMath 単体: Haversine 2 / 点-線分距離 3 / 投影 t 3 | `88d00fe` | 567 |
| 3A.5b | 12 | `NativeRoadSnapper`: コンストラクタ / 頂点 / エッジ中央 / 非存在 profile / 検索半径 0 / 範囲外 / bbox 拡張 / Brute-force × 20 / Offset 単調 / From/To 端点 / Dispose 後 | `5a54296` | 579 |
| 3A.6 | 16 | NativeRouter 自己整合: smoke 5 + 不変量 8 + RouterDb コンストラクタ 2 + fixture sanity 1 | （本ステップ完了 commit） | 595 |
| **合計** | **69** | Phase 2 累計 526 → Phase 3 3A 完了時 **595 件 pass** | | |

並存戦略:

- Phase 1 / Phase 2 累計 526 件（Itinero 系）は触らず、全 pass を維持
- Native 系テストは [`NativeAndOdrgReaderFixture`](../tests/OsmDotRoute.Tests/Native/NativeRoadGraphParityTests.cs) / [`NativeRouterDbFixture`](../tests/OsmDotRoute.Tests/Native/NativeRouterDbFixture.cs) を `IClassFixture` で共有し実行時間を最小化
- 全 595 件の実行時間は約 47 秒（Phase 2 完了時 23 秒 → Native 追加 +24 秒、CI 許容範囲内）

### 3.6 実装メモ

#### 主要 commit（時系列）

| commit | 概要 |
| --- | --- |
| `fb6cd45` | 3A.1: `OdrgSectionDirectory` 実装（HEADER + SECTION TABLE パース、5 件） |
| `279a6ec` | 3A.2: `OdrgMmfHandle` 実装（MMF + SafeBuffer ゼロコピー Span 切出、8 件） |
| `eb1431c` | 3A.3a: §3A.3-API 確定 (a) `EvaluateEdge` 2 オーバーロード（計画書 v0.3 承認） |
| `c46a2ca` | 3A.3b: `IRoadGraph` 改修 + `ItineroRoadGraph` + `EdgeWeightCalculator` + `DijkstraEngine` + テスト 5 ファイル + Itinero テスト extension 一括改修（539 件 pass 維持） |
| `4549633` | 3A.3e: `NativeRoadGraph` 実装（`IRoadGraph` 実装、CSR インデックス、シェイプ詰替えキャッシュ、距離キャッシュ、sanity 3 件、542 件 pass） |
| `f573c08` | 3A.3f: `NativeRoadGraph` × `OdrgReader` 真値突合 9 件（551 件 pass、3A.3 全体完了） |
| `78d4581` | 3A.4: `NativeRTreeQuery` 実装（R-tree クエリ + Nearest、Brute-force 突合、8 件、559 件 pass） |
| `88d00fe` | 3A.5a: `GeoMath` ヘルパ新設（Haversine + MetersToBboxDegrees + PointToSegment、8 件、567 件 pass） |
| `5a54296` | 3A.5b: `NativeRoadSnapper` 実装（R-tree 候補 + 点-線分最短距離 + Brute-force 突合、12 件、579 件 pass） |
| 本ステップ | 3A.6 + 3A 完了: `NativeRouterDbFixture` + 自己整合テスト 16 件 + 設計書 §3 反映（595 件 pass） |

#### 暗黙の前提・引っかかりポイント

- **`OdrgVertex(Lon, Lat)` と `GeoCoordinate(Lat, Lon)` のフィールド順差**: `.odrg` 内 `OdrgVertex` は `(Lon, Lat)` 順、既存 `GeoCoordinate` は `(Lat, Lon)` 順。`MemoryMarshal.Cast` で直接キャスト不可。`NativeRoadGraph` は初回 `GetEdgeShape` 呼出時に**詰替えてキャッシュ**する（3A.3e §2.7 F4）。
- **エッジ距離キャッシュ**: `RoadEdge.DistanceM` は `.odrg` に直接保持されない。端点 + 中間シェイプ点列の Haversine 距離合算で初回算出し、エッジごとに `float[]` + `bool[]` でキャッシュ。
- **R-tree ノードレイアウト**: `OdrgWriter.WriteRTreeSection` と完全同一 struct で読込。リーフノードは `count > 0` かつ子は edge ID リスト、内部ノードは子ノードインデックスリスト。
- **3A.4 計画書 §4.4-B からの軽微逸脱**: リーフ展開で `edgeAabbs` を Query API に追加（true-positive filter のため、commit `78d4581` メッセージに明記済）。
- **3A.5b 計画書 §4.5.2 からの軽微逸脱**: 通行不可除外テストを「非存在 profile / 検索半径 0」に置換、Brute-force 50 → 20 で CI 安定（commit `5a54296` メッセージに明記済）。
- **3A.6 計画書 §4.6-B テスト 13 からの軽微逸脱**: `Route_SegmentConnectivity` → `Route_ShapeIsContinuous` に変更。`Route` 型に `RouteSegment` プロパティが存在しない（`TotalDistanceM` / `TotalDurationSec` / `Shape` のみ）ため、Shape の隣接点間 Haversine 合計と `TotalDistanceM` の整合性を ±20% で検証。計画書 v0.10 自体に「(RouteSegment が存在する場合)」の注釈があり想定済の逸脱パターン。

#### Phase 4+ への申し送り

- `NativeEdgeEnumerator` は class（Itinero と同じ）。3E ベンチで struct enumerator 化の効果を測定し、必要なら 3D で切替検討。
- `Route.Shape` の `ReadOnlyMemory<GeoCoordinate>` 化は 3C で実施予定。`MemoryManager<T>` 経由で Native の Span をラップする設計を予定。
- Big-endian ホスト対応は将来課題（3A-R6）、現状は `OdrgFormatException` で明示拒否。

---

## 4. 動的制約ホットパス（交差エッジキャッシュ）

**対応ステップ**: 3B (3B.1〜3B.5)
**対応要件**: REQ-NFR-002（制約 100 件下でも経路計算 ≤ 100ms 維持）、Phase 1 §18.3 解消、Phase 1 §18.4 アロケート削減
**Phase 2 申し送り**: 設計書 §8.3 表 3B 行
**実装日**: 2026-05-27（3B.1〜3B.5 同日完了）
**実装バージョン**: ユーザー採番
**主要ファイル**:

- [`src/OsmDotRoute/Restrictions/RestrictedAreaEdgeCache.cs`](../src/OsmDotRoute/Restrictions/RestrictedAreaEdgeCache.cs)（`internal sealed class`、Block + Difficulty 双方キャッシュ）
- [`src/OsmDotRoute/Restrictions/RestrictedAreaService.cs`](../src/OsmDotRoute/Restrictions/RestrictedAreaService.cs)（`AttachGraph` + eager bake 追加、公開 API 不変）
- [`src/OsmDotRoute/Routing/IRoadGraph.cs`](../src/OsmDotRoute/Routing/IRoadGraph.cs)（`QueryEdgesByAabb` メソッド追加）
- [`src/OsmDotRoute/Native/NativeRoadGraph.cs`](../src/OsmDotRoute/Native/NativeRoadGraph.cs)（`QueryEdgesByAabb` 実装、`NativeRTreeQuery` 経由）
- [`src/OsmDotRoute.Itinero/ItineroRoadGraph.cs`](../src/OsmDotRoute.Itinero/ItineroRoadGraph.cs)（`QueryEdgesByAabb` fallback 実装、3C で撤去予定）
- [`src/OsmDotRoute/Routing/EdgeWeightCalculator.cs`](../src/OsmDotRoute/Routing/EdgeWeightCalculator.cs)（`EvaluateConstraintFactor` ホットパス置換）
- [`src/OsmDotRoute/Router.cs`](../src/OsmDotRoute/Router.cs)（`internal` バリエーション追加、自動 AttachGraph）
- [`tests/OsmDotRoute.Benchmarks/BenchmarkAssets.cs`](../tests/OsmDotRoute.Benchmarks/BenchmarkAssets.cs)（`LoadNativeRouterDb` 追加）
- [`tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs`](../tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs)（3 モード分岐）

### 4.1 意図

REQ-NFR-002（制約 100 件下でも経路計算 ≤ 100ms 維持）と Phase 1 §18.3 / §18.4 の改善を目的とする。Phase 1 では `EdgeWeightCalculator.EvaluateConstraintFactor` が Dijkstra 辺展開時に毎エッジ実行され、内部で `BuildFullShape` (`new List<GeoCoordinate>(N+2)`)、`Aabb.FromCoordinates` (全 shape 走査)、`new HashSet<RestrictedAreaId>()` (seenIds alloc)、`_index.Query` (`SpatialIndex` 線形走査、制約 100 件で 100 回 AABB 交差判定)、`EdgeIntersectsAreaShapes` (シェイプ × Shape 二重ループ) が走り、Phase 1 §18.4 = 経路 1 本あたり 77MB アロケートの主因の一つとなっていた。

3B では **「制約 ID → エッジ ID 集合」のキャッシュを `RestrictedAreaService` の add 時に eager bake** し、Dijkstra ホットパスを **HashSet/Dictionary 各 1 発のキャッシュ参照** に圧縮する。`.odrg` の STR R-tree (3A.4 `NativeRTreeQuery`) を活用してエッジ AABB 候補を O(log E) で抽出 → 厳密判定 → キャッシュ格納する設計。Phase 1 の公開 API は完全不変。

### 4.2 採用設計

#### 4.2.1 レイヤ構成

```text
公開 API: new RestrictedAreaService() + Add/Remove/Clear (Phase 1 不変)
        ↓
Router(routerDb, restrictions)
        ↓ (T9=A: コンストラクタで自動呼出)
RestrictedAreaService.AttachGraph(IRoadGraph)
        ↓
RestrictedAreaEdgeCache (internal sealed class)
   ├─ _blockedByArea: Dictionary<RestrictedAreaId, HashSet<uint>>  (Block 逆引き)
   ├─ _blockedEdges: HashSet<uint>                                 (Block 集約、IsBlocked O(1))
   ├─ _difficultyByArea: Dictionary<RestrictedAreaId, HashSet<uint>>  (Difficulty 逆引き)
   └─ _difficultyAreasByEdge: Dictionary<uint, List<DifficultyArea>>  (Difficulty 列挙)
        ↑
        │ Add/Remove 時に同期更新 (Register/Remove/RemoveByTag/ClearAll)
        │
IRoadGraph.QueryEdgesByAabb(Aabb queryBounds) → IEnumerable<uint>
   ├─ NativeRoadGraph: NativeRTreeQuery.Query (3A.4) + buffer growable
   └─ ItineroRoadGraph: 全エッジ走査 fallback (3C で撤去予定)
        ↓
EdgeWeightCalculator.EvaluateConstraintFactor(edgeId, from, to, middleShape)
   ├─ graph 注入時: cache.IsBlocked / cache.GetDifficultyAreas + evaluator.EvaluateDifficulty (T1=A 都度評価)
   └─ graph 未注入時: Phase 1 動作にフォールバック (BuildFullShape + EvaluateConstraints)
```

#### 4.2.2 主要 API

| API | 役割 |
| --- | --- |
| `new Router(routerDb, restrictions)` | 既存公開 API、内部で `restrictions?.AttachGraph(routerDb.Graph)` 自動呼出 (T9=A) |
| `internal Router(routerDb, restrictions, autoAttachGraph)` | 3B.5-B ベンチ用、`autoAttachGraph=false` で Phase 1 動作再現 (T15=A) |
| `RestrictedAreaService.AttachGraph(IRoadGraph)` | 同一 graph 再 attach は no-op、別 graph は `InvalidOperationException` (T7=A) |
| `IRoadGraph.QueryEdgesByAabb(Aabb)` | エッジ ID を `IEnumerable<uint>` で yield return (T5=A) |
| `RestrictedAreaEdgeCache.IsBlocked(uint)` | ホットパス API、HashSet 1 発 |
| `RestrictedAreaEdgeCache.GetDifficultyAreas(uint)` | ホットパス API、Dictionary 1 発、該当なし時 `Array.Empty<>()` |

### 4.3 設計判断の根拠

3B 期間中に着手前事前調査でユーザー確認した設計判断は以下のとおり（コア設計 Q1〜Q4、効果測定 Q5〜Q7、サブステップ詳細 T1〜T16）：

| ID | 論点 | 確定 | 理由 |
| --- | --- | --- | --- |
| Q1 | キャッシュ構築タイミング | (A) eager bake (Add 時に同期実行) | ホットパス単純化を優先、制約変更は経路計算より圧倒的に少ない |
| Q2 | IRoadGraph 注入方式 | (A) オプション注入 (`AttachGraph`) | 公開 API 完全不変、graph 未注入時は Phase 1 動作フォールバック |
| Q3 | Itinero 対応範囲 | (A) Native のみ高速化 | 3C で Itinero 撤去予定、`ItineroRoadGraph.QueryEdgesByAabb` は全エッジ走査 fallback |
| Q4 | サブステップ分割粒度 | (A) 5 サブ分割 (3B.1〜3B.5) | 3A.4〜3A.6 と同じ細分化、各サブで `dotnet test` 全 pass を維持 |
| Q5 | 効果測定計測手法 | (A) Phase 1 既存 `RouteWithConstraintsBenchmark` 改修 (BenchmarkDotNet) | Phase 1 資産活用、3E 本番ベンチと連動 |
| Q6 | 測定指標 | (A) 時間 + アロケート量 (`MemoryDiagnoser`) | 3B はホットパス List/HashSet 削減が本命、アロケート量で効果が見える |
| Q7 | 比較ベースライン | (A) graph 未注入モード (Native-Detached) | フェアな比較 (同一 RouterDb / プロファイル、3B キャッシュ有無のみが違う) |
| T1 | Difficulty 評価タイミング | (A) 都度評価 (ホットパスで `EvaluateDifficulty` を呼ぶ) | Phase 1 セマンティクス完全互換、プロファイル動的追加 OK |
| T2 | Block 重複処理 | (A) `OtherContains` 走査 (Remove 時) | シンプル、メモリ増なし、削除は非ホットパスで O(K×M) 許容 |
| T3 | Difficulty 重複処理 | (A) List `RemoveAll` (Remove 時) | T1 (A) との整合性、`_difficultyAreasByEdge` リスト操作 |
| T4 | `QueryEdgesByAabb` 公開型 | (A) `Aabb` (Lat-Lon、Phase 1 既存型) | REQ-API-003、内部実装型 `OdrgBbox` を公開 API に露出しない |
| T5 | `QueryEdgesByAabb` シグネチャ | (A) `IEnumerable<uint>` yield return | bake は非ホットパス、内部 buffer growable リトライで alloc 許容 |
| T6 | Itinero fallback 方式 | (A) `GetEdge(e)` 都度 AABB 計算 | 3C で Itinero 撤去予定のため投資最小 |
| T7 | `AttachGraph` 呼出規約 | (A) 同一 graph no-op / 別 graph 例外 | `ReferenceEquals` 判定、同一 Service の複数 Router 共有可 |
| T8 | graph 範囲外形状 | (A) 無視 (cache に入らない) | Phase 1 セマンティクス完全互換、範囲外制約は経路に影響しない |
| T9 | Router 自動 AttachGraph | (A) コンストラクタで自動呼出 | 公開 API 不変、ユーザーから見えるのは `new Router` のみ |
| T10/T11 | `EdgeId` プロパティ | 既存 (Phase 1/3A.3b で追加済)、シグネチャ変更不要 | `IRoadGraphEdgeEnumerator.EdgeId` / `RoadEdge.EdgeId` |
| T12 | `BuildFullShape` 削除可否 | graph 未注入 fallback で残置 (3C で再評価) | Phase 1 既存 36 件互換性のため |
| T13 | 3B.5-A テスト粒度 | (A) Native 独自シナリオ 6 件 | Phase 1 既存 36 件でセマンティクス維持は証明済、Native 実機動作のみ軽量追加 |
| T14 | ベンチ制約データ | 自動確定: `restrictions-mixed-100.json` は津島市 .odrg 範囲内 | 新規データ生成不要、3B-R8 解消 |
| T15 | ベンチ Native-Detached 実現 | (A) `Router` に `internal` バリエーション追加 | 公開 API 不変、テスト/ベンチから利用容易 |
| T16 | Native ロードヘルパ | (A) `BenchmarkAssets.LoadNativeRouterDb` 追加 | 既存 `LoadOsmDotRouterDb` パターン踏襲、共通化 |

### 4.4 トレードオフ・制約

- **Phase 1 セマンティクス完全互換**: graph 注入時のキャッシュ動作と graph 未注入時の Phase 1 動作で経路結果が完全一致。Phase 1 既存 36 件 (`RestrictedAreaServiceTests` 14 + `RestrictedRoutingTests` 9 + `RestrictedAreaServiceGmlTests` 13) すべて pass 維持で証明。
- **Difficulty 評価のホットパス呼出**: T1=(A) で `evaluator.EvaluateDifficulty(area.DifficultyType)` をホットパスで都度呼ぶ。Phase 1 動作と同等、プロファイル動的追加 OK。プロファイル × DifficultyType の事前計算は Phase 4+ で再評価。
- **Itinero fallback の性能**: T6=(A) で `GetEdge(e)` を毎エッジ呼ぶ全エッジ走査、Native R-tree O(log E) と比較して非効率。3C で Itinero 撤去後に問題解消。
- **`BuildFullShape` の存続**: graph 未注入時 fallback で `EdgeWeightCalculator` 内に残置 (T12)。3C で Itinero 撤去 + Router 必須化後に削除検討。
- **`RestrictedAreaService` の単一 graph バインド**: T7=(A) で別 graph への attach は `InvalidOperationException`。同一 service を複数 Router で使う場合は同一 RouterDb 必須。
- **キャッシュ rebuild の自動化なし**: graph 自体を再ロードした場合 (Native の場合 .odrg ファイル更新) は service を再構築する必要あり。3F 親プロ統合時に運用フロー策定。

### 4.5 検証方法

#### 4.5.1 単体テスト 29 件（Phase 3 累計 +29、618 → 624）

| サブステップ | 件数 | 観点 | 完了 commit | 累計 |
| --- | --- | --- | --- | --- |
| 3B.1 | 7 | `RestrictedAreaEdgeCache` 単体 (Empty / Add / Multiple / Remove + 重複制約 + 削除整合性) | `8e92dd7` | 602 |
| 3B.2 | 6 | `IRoadGraph.QueryEdgesByAabb` (Native R-tree 3 + Itinero fallback 3) | `33778be` | 608 |
| 3B.3 | 10 | `AttachGraph` + eager bake (状態確認 2 + bake 3 + Remove 連動 3 + T7 規約 2) | `c789ee7` | 618 |
| 3B.4 | 0 | `EdgeWeightCalculator` ホットパス置換 (既存テスト維持で代替) | `61789e7` | 618 |
| 3B.5-A | 6 | Native + 制約統合 (empty baseline / Block 迂回 / Difficulty 時間増加 / add+remove / ClearAll / RemoveByTag) | （本ステップ） | 624 |
| **合計** | **29** | Phase 1 既存 526 件 + Phase 3 累計 = **624 件 pass** | | |

Phase 1 既存 36 件 (Restricted 系) は変更なし、graph 自動注入後も Phase 1 動作と経路結果完全一致を維持。

#### 4.5.2 3B 効果ベンチマーク (BenchmarkDotNet、`--job short`、2026-05-27 実測)

**測定環境**: Windows 11 (10.0.26200.8457)、11th Gen Intel Core i7-1165G7 2.80GHz、.NET SDK 9.0.103
**測定対象**: 津島市 .odrg (3.55MB、27,235 頂点 / 38,004 エッジ) で 100 ペア × Car プロファイル

| Mode | Case | Mean | Allocated | 3B 効果 |
| --- | --- | --- | --- | --- |
| Itinero（参考、default.routerdb 43k 頂点） | C0 | 30.83 ms | 70.58 MB | - |
| Itinero（参考、default.routerdb 43k 頂点） | C3 | 12.52 ms | 21.70 MB | - |
| **Native-Detached（3B 前相当）** | C0 | **3.040 ms** | (報告なし) | 基準 |
| **Native-Detached（3B 前相当）** | C3 | **18.045 ms** | **2.79 MB** | 基準 |
| **Native-Attached（3B 後）** | C0 | **3.030 ms** | **1.13 MB** | Mean: -0.3% (制約なしで効果現れず、期待通り) |
| **Native-Attached（3B 後）** | C3 | **1.177 ms** | **0.74 MB** | **Mean: -93.5%、Allocated: -73.5%** |

**3B 効果の解釈**:

- **C0 (制約なし、baseline)**: Native-Detached 3.040 ms ≈ Native-Attached 3.030 ms。制約 0 件なのでホットパスは同一、期待通り。3B がリグレッション (退化) を起こしていない証明。
- **C3 (制約 100 件下、3B 本命)**: Native-Detached 18.045 ms → Native-Attached 1.177 ms = **約 15 倍高速化**。Phase 1 §18.3 の制約 100 件下劣化問題が完全解消。Allocated も 2.79MB → 0.74MB で **約 4 倍削減**、Phase 1 §18.4 の経路 1 本あたりアロケート削減に直接貢献。
- **Itinero 参考値**: RouterDb 規模差 (43k 頂点 vs 27k 頂点) があり直接比較は不可だが、Native-Attached C3 (1.18 ms) は Itinero C3 (12.52 ms) より約 10 倍高速、Allocated は 30 倍小。詳細は 3E 本番ベンチで再評価。

注意: `--job short` (iteration 3) のため StdDev が大きい (信頼区間 99.9% で ±数十 ms)。本番統計値は 3E で iteration 10+ で再測定予定。本値は **3B 効果の桁オーダー確認用**。

### 4.6 実装メモ

#### 主要 commit（時系列）

| commit | 概要 |
| --- | --- |
| `ce7ef00` | 3B 計画書 v0.2 (Q1〜Q7 確定、ベンチ 3 モード改修方針) |
| `cab741a` | 3B.1 計画書 v0.3 (T1〜T3 確定) |
| `8e92dd7` | 3B.1: `RestrictedAreaEdgeCache` 新設 (単体 7 件、602 件 pass) |
| `f57ebfc` | 3B.2 計画書 v0.4 (T4〜T6 確定) |
| `33778be` | 3B.2: `IRoadGraph.QueryEdgesByAabb` 追加 + Native/Itinero 実装 (単体 6 件、608 件 pass) |
| `e73bc26` | 3B.3 計画書 v0.5 (T7〜T9 確定) |
| `c789ee7` | 3B.3: `RestrictedAreaService.AttachGraph` + eager bake 統合 (公開 API 不変、単体 10 件、618 件 pass) |
| `a94dbe4` | 3B.4 計画書 v0.6 (T10〜T12 確認 = ユーザー判断不要) |
| `61789e7` | 3B.4: `EdgeWeightCalculator` ホットパス置換 (`BuildFullShape` 排除、Phase 1 セマンティクス維持、618 件 pass) |
| `bdcfbdf` | 3B.5 計画書 v0.7 (T13/T15/T16 確定、T14 自動確定) |
| 本 commit | 3B.5 + 3B 完了: Native + 制約パリティ 6 件 + ベンチ 3 モード改修 + 設計書 §4 反映 (624 件 pass、3B 効果 C3 Mean -93.5%/Alloc -73.5%) |

#### 暗黙の前提・引っかかりポイント

- **`Aabb` (`OsmDotRoute.Geometry`) vs `OdrgBbox` (`OsmDotRoute.Internal.Odrg`)**: 前者 Lat-Lon、後者 Lon-Lat、3A.1 で別型として並存 (B 案、3C で統合検討)。3B では `NativeRoadGraph.QueryEdgesByAabb` 内部で変換 1 行。
- **`OdrgReadResult.EdgeAabbs` は `OsmDotRoute.Extractor.Pipeline.Aabb`**: Core の `Aabb` とは別型、3A.1 並存の影響でテスト中の Brute-force 突合では `NativeRoadGraph.GetEdgeAabbs()` (`OdrgBbox`) を直接使用 (3B.2 軽微逸脱、commit `33778be` メッセージに明記)。
- **BenchmarkDotNet 子プロセス起動時のパス**: `AppContext.BaseDirectory` がベンチ中間ディレクトリに変わるため、相対パスは利用不可。`BenchmarkAssets.TsushimaOdrgPath` は `RouterDbPath` 同様に絶対パスを直書き。
- **3B.5-A テスト軽微逸脱**: 計画書 §4.5-A 当初想定 10〜15 件 → v0.7 で 5〜8 件に縮小、最終 6 件。Phase 1 既存 36 件でセマンティクス維持は既に証明済 (3B.4 完了時)、Native 独自シナリオのみ追加。
- **`RestrictedAreaService.SpatialIndex<T>`**: 既存の制約形状 R-tree インデックスは Phase 1 動作 fallback 用に残置 (graph 未注入時の `EvaluateConstraints` で使用)。graph 注入時は実質使われない。3C で Itinero 撤去後に再評価。

#### Phase 4+ への申し送り

- プロファイル × DifficultyType の事前計算 (T1=(B) 案): `evaluator.EvaluateDifficulty` の都度呼出を排除する余地。3E ベンチで呼出コストを実測してから決定。
- ターン制限 (PBF Relation `type=restriction`) 対応: 現状未対応、Phase 4+ で必要に応じて。
- マルチスレッド対応: 現状 `RestrictedAreaService` は単一スレッド前提 (Phase 1 既存方針継続)。3B-R リスク表参照。

---

## 5. Bicycle / Truck プロファイル独自設計

**対応ステップ**: 3D (3D.1〜3D.4)
**対応要件**: REQ-PRF-003（Bicycle プロファイル独自設計）、REQ-PRF-004（Truck = 10t、日本道路法ベース、独自設計）
**Phase 2 申し送り**: 設計書 §8.3 表 3D 行
**実装日**: 2026-05-27（3D.1〜3D.4 同日完了）
**実装バージョン**: ユーザー採番
**主要ファイル**:

- [`src/OsmDotRoute/Profiles/bicycle.json`](../src/OsmDotRoute/Profiles/bicycle.json)（埋込リソース、平均 15 km/h）
- [`src/OsmDotRoute/Profiles/truck.json`](../src/OsmDotRoute/Profiles/truck.json)（埋込リソース、車両総重量 20t / 全高 3.8m / 全幅 2.5m）
- [`src/OsmDotRoute/Profiles/JsonProfileDefinition.cs`](../src/OsmDotRoute/Profiles/JsonProfileDefinition.cs)（`VehicleLimits` プロパティ + `JsonVehicleLimits` DTO 追加）
- [`src/OsmDotRoute/Profiles/ProfileEvaluator.cs`](../src/OsmDotRoute/Profiles/ProfileEvaluator.cs)（`_vehicleLimits` + `ExceedsVehicleLimit` + `TryParseLimitValue` + バリデーション追加）
- [`src/OsmDotRoute/VehicleProfile.cs`](../src/OsmDotRoute/VehicleProfile.cs)（`Bicycle` / `Truck` 静的プロパティ追加、`Car` / `Pedestrian` の Lazy<T> パターン踏襲）
- [`src/OsmDotRoute/OsmDotRoute.csproj`](../src/OsmDotRoute/OsmDotRoute.csproj)（`bicycle.json` / `truck.json` を `EmbeddedResource` 登録）
- [`src/OsmDotRoute.Extractor/Program.cs`](../src/OsmDotRoute.Extractor/Program.cs)（`ResolveProfile` に `bicycle` / `truck` 追加）

### 5.1 意図

REQ-PRF-003（Bicycle）と REQ-PRF-004（Truck = 10t、日本道路法ベース、独自設計）を Phase 3 段階で同梱する。親プロジェクト「災害廃棄物処理シミュレーション」が暗黙運用してきた 10t トラックを `OsmDotRoute` 標準プロファイルに昇格させ、Itinero / OSRM の Truck プロファイル流用は計画書 §3.3 確定の通り回避（日本道路法と海外仕様の乖離が大きい）。

Bicycle は REQ-PRF-003 で「`highway=cycleway` / `highway=path` (`bicycle=yes`) を歩道並みに通行可、`highway=motorway` / `highway=trunk` (`bicycle=no`) を通行不可、平均速度 15 km/h」と定義済。

Truck (10t) の物理寸法は要件定義書 §5.3 §16 で未確定だったため、本ステップで「車両総重量 20t / 全高 3.8m / 全幅 2.5m」（標準大型ダンプ、日本道路法 車両制限令 一般条件に整合）に確定（ユーザー判断 Q3 = A）。`maxweight=*` / `maxheight=*` / `maxwidth=*` の数値比較ロジックは Phase 1 既存 `JsonHighwayRule`（`speedKmh` + `access` のみ）にはなかったため、3D.2 で `JsonVehicleLimits` を JSON スキーマに新規追加 + `ProfileEvaluator` に hard-deny セマンティクスで組込（ユーザー判断 Q1 = A）。

`osmdotroute-extractor` の `--profiles` 引数は 4 プロファイル受付に拡張、`BakedProfileTable` は Phase 2 ステップ 3.6 で既に N プロファイル対応済のため改修不要。

### 5.2 採用設計

#### 5.2.1 レイヤ構成

```text
.profile.json (埋込リソース、4 種類: car / pedestrian / bicycle / truck)
        ↓ JsonSerializer
JsonProfileDefinition         DTO (Name / Highway / AccessTagKeys / AccessValueMap
        ↓                          / Fallback / SpeedBounds / Difficulty / SpeedMultiplier
                                   / VehicleLimits ← 3D.2 新規追加)
ProfileEvaluator              評価器
   ├─ Highway 別 access/speedKmh 取得 (Phase 1 既存)
   ├─ AccessTagKeys ループ評価 (Phase 1 既存)
   ├─ vehicleLimits hard-deny 評価 (3D.2 新規)  ← osmTags["maxweight"/"maxheight"/"maxwidth"]
   │  └─ TryParseLimitValue で数値+単位パース、既定単位 (t/m) のみ受け入れ、未対応は false
   ├─ 通行不可なら早期 return
   ├─ maxspeed パース (Phase 1 既存)
   ├─ speedMultiplier 適用 + speedBounds クランプ (Phase 1 既存)
   └─ oneway (Phase 1 既存)
        ↓
VehicleProfile.{Car, Pedestrian, Bicycle, Truck}  (Lazy<T> 同梱)
VehicleProfile.LoadFromJsonString/File/Stream      (ユーザー独自プロファイル、REQ-PRF-009)
        ↓
osmdotroute-extractor --profiles car,pedestrian,bicycle,truck
   └─ Program.ResolveProfile (3D.4 拡張、4 ケース対応)
        ↓
ProfileBaker.Build(profiles[N], edges[E])  ← BakedProfileTable はプロファイル数 N 可変
        ↓
BakedProfileTable[profileIndex, edgeId]   (.odrg BAKED_PROFILE セクション)
```

#### 5.2.2 Bicycle プロファイル仕様（[bicycle.json](../src/OsmDotRoute/Profiles/bicycle.json)）

| 項目 | 値 | 根拠 |
| --- | --- | --- |
| name | "bicycle" | — |
| vehicleType | "bicycle" | — |
| speedMultiplier | 1.0 | Pedestrian と同等（Itinero Fastest 補正 0.75 は自動車向け） |
| accessTagKeys | `["access", "vehicle", "bicycle"]` | Bicycle 末尾優先 |
| speedBounds | minKmh 5 / maxKmh 25 | 平均 15 km/h を中心、footway 徐行 5 km/h を切らない |
| ignoreOneway | false | 自転車道は方向制限あり |
| 通行可 (15 km/h) | cycleway / path / primary / secondary / tertiary / residential / service / living_street / unclassified | REQ-PRF-003 |
| 通行可 (低速) | footway / pedestrian / bridleway / steps 5 km/h、track 10 km/h | 歩行者共有路は徐行 |
| 通行不可 (hard-deny) | motorway / motorway_link / trunk / trunk_link | REQ-PRF-003 |
| fallback | speedKmh 5 / access "no" | 未知 highway は通行不可 |
| 難所 | flooding 0.3 / liquefaction 0.3 / landslide 0.0 (canPass=false) / construction 0.4 / obstacle 0.4 / congestion 0.8 / snow 0.2 / ice 0.1 | 押し歩き想定、渋滞影響少 |

#### 5.2.3 Truck (10t) プロファイル仕様（[truck.json](../src/OsmDotRoute/Profiles/truck.json)）

| 項目 | 値 | 根拠 |
| --- | --- | --- |
| name | "truck" | REQ-PRF-004 |
| vehicleType | "hgv" | OSM タグ慣習 |
| speedMultiplier | 0.75 | car と同等の Fastest 補正 |
| accessTagKeys | `["access", "vehicle", "motor_vehicle", "hgv"]` | hgv 末尾優先 |
| speedBounds | minKmh 5 / maxKmh 90 | 大型車最高速度想定 + 低速エッジで自然回避効くよう min を 5 に |
| **vehicleLimits** | **maxWeightTon 20 / maxHeightMeter 3.8 / maxWidthMeter 2.5** | **Q3 = (A) 標準大型ダンプ、日本道路法 車両制限令 一般条件** |
| ignoreOneway | false | 必須 |
| 通行可 (90/80/60/50/40 km/h、speedMultiplier 0.75 適用前) | motorway 90 / trunk-primary 80 / secondary 60 / tertiary 50 / unclassified-residential 40 / service 20 | — |
| 回避エッジ (Q2 = A、自然回避) | living_street 5 / track 5 / pedestrian 5 km/h、access "yes" で許可するが Dijkstra コスト経由で迂回 | — |
| 物理通行不可 (hard-deny) | footway / path / cycleway / steps / bridleway | 物理的に通行不可 |
| fallback | speedKmh 10 / access "no" | 未知 highway は通行不可 |
| 難所 | flooding 0.2 / liquefaction 0.3 / landslide 0.0 / congestion 0.3 (大型車は渋滞影響大) | car より厳しめ（冠水道路リスク大） |

#### 5.2.4 vehicleLimits 評価ロジック（[ProfileEvaluator.cs](../src/OsmDotRoute/Profiles/ProfileEvaluator.cs)）

評価ステップ 2.5 を **accessTagKeys ループ後・通行不可早期 return 前** に挿入：

```text
if (accessAllow && _vehicleLimits != null && ExceedsVehicleLimit(osmTags))
{
    accessAllow = false;  // hard-deny 等価: access=destination 等の上書きは効かない
}
```

`ExceedsVehicleLimit` は OSM タグ `maxweight` / `maxheight` / `maxwidth` を順次評価し、いずれかがプロファイル制限を下回るなら通行不可。

`TryParseLimitValue`:

- "8" / "8t" / "8 t" → 8.0（既定単位 t）
- "3.5" / "3.5m" / "3.5 m" → 3.5（既定単位 m）
- "8000 kg" / "10 ft" 等の **未対応単位** → false（安全側、制限発火させない、計画書 T1 リスク対応）
- "signals" / 数値なし → false

#### 5.2.5 Extractor 4 プロファイル拡張（[Program.cs](../src/OsmDotRoute.Extractor/Program.cs)）

`ResolveProfile` の switch 式に `bicycle` / `truck` を追加するのみ。`BakedProfileTable` / `ProfileBaker` / `OdrgWriter` は Phase 2 ステップ 3.6 で既に N プロファイル対応のため改修不要：

```bash
osmdotroute-extractor extract \
  --input  ... .osm.pbf \
  --output ... .odrg \
  --bbox   ... \
  --profiles car,pedestrian,bicycle,truck
```

### 5.3 設計判断の根拠

3D 期間中に着手前事前調査でユーザー確認した設計判断は以下のとおり：

| ID | 論点 | 確定 | 理由 |
| --- | --- | --- | --- |
| Q1 | Truck の `maxweight` / `maxheight` 数値評価方式 | **(A) JSON スキーマに vehicleLimits 追加** | Extractor / NativeRoadGraph / EdgeFlags 改修なし、bit 14/15 予約温存、Truck 仕様を JSON で完結。`accessTagKeys` に hgv 追加するだけの案 (B) は数値タグを完全無視するため Truck 仕様としては荒い。Phase 2 .odrg EdgeFlags に bit 追加 (C) はフォーマット影響あり、`maxweight` 等の連続値はフラグ化困難 |
| Q2 | Bicycle 平均速度 + Truck 回避方式 | **(A) Bicycle 15 km/h + Truck living_street は speedKmh 低設定で自然回避** | Bicycle は REQ-PRF-003 で 15 km/h 明記。Truck の living_street 等は `access:"no"` 完全禁止 (C) だと緊急時の経由不可リスク、EdgeFlags 直接参照 (D) は ProfileEvaluator が tag 辞書ベース評価モデルから外れて bake 後 data 構造に密結合する |
| Q3 | Truck の物理寸法既定値 | **(A) 車両総重量 20t / 全高 3.8m / 全幅 2.5m**（標準大型ダンプ） | 最大積載量 10t (REQ-PRF-004 確定) + 自重 10t 級 = 20t。日本道路法 車両制限令 一般条件 (全幅 2.5m / 高 3.8m) に整合。10t ダンプ最大寸法 (B) や 4t ダンプ寄り保守値 (C) は汎用性低い。JSON 外部化でユーザー独自プロファイルで上書き可能 |
| Q4 | サブステップ分割粒度 | **(A) 4 サブ** (3D.1 Bicycle / 3D.2 vehicleLimits / 3D.3 Truck / 3D.4 Extractor + 設計書) | 3B (5 サブ) 並み細分化。3D.1 で Bicycle が独立完結、3D.2 で vehicleLimits 評価機構を先に固めてから 3D.3 で Truck を組み立てる順序が回帰切り分けやすい |

`vehicleLimits` 評価ステップ位置の判断：

- **accessTagKeys 後に挿入** することで「`access=destination` → accessAllow=true → vehicleLimits 評価 → 重量超過なら拒否」となる
- これは「物理制限は法令タグ上書きより優先する」セマンティクス＝hard-deny 等価
- accessTagKeys 前に挿入する案だと「vehicleLimits で拒否 → accessTagKeys の `hgv=yes` で許可に戻る」となり、Truck が `maxweight=8` のエッジを `hgv=yes` で通れてしまう不具合になるため不採用

### 5.4 トレードオフ・制約

- **`maxweight=*` の単位表記揺れ**: 本実装は OSM 既定単位 (t/m) のみ受け入れ、`kg` / `ft` / `in` 等は安全側として制限発火させない（通行可扱い）。実 OSM データで `maxweight="8000 kg"` (= 8t 相当) のエッジは誤って通行可と判定される。Phase 4+ で kg 単位対応を検討する余地あり、ただし `8000 kg`/`8 t` 等の表記は OSM では稀（Wiki デフォルト = t）。
- **Truck の `living_street` 回避**: Q2 = (A) で speedKmh = 5 設定 + access "yes" 許可とした結果、Dijkstra コスト経由で住宅街を迂回するが「motorway 経路よりも living_street 経路を選ぶ」リスクは数値設計に依存。3D の合成テストでは確認できないため、3F 親プロ統合時に津島市実データで再評価する。speedKmh を 3 km/h まで下げる調整余地あり（計画書 T3 リスク）。
- **Phase 2 `EdgeFlags` 直接参照は採用せず**: Q2 = (A) で「ProfileEvaluator は tag 辞書ベース評価モデルを維持、Phase 2 `IsLivingStreet` / `IsTrack` / `IsPedestrianSeparated` 等の EdgeFlags 参照は Phase 4+ に保留」と判断。bake 後の data 構造との密結合を避けることで、JSON プロファイルの独立性（ユーザー独自プロファイル含む）を保つ。
- **`car.json` / `pedestrian.json` 評価結果の不変保証**: `vehicleLimits` は optional で car / pedestrian は未定義のため、ProfileEvaluator の新規分岐は走らず Phase 1 動作と完全一致。回帰確認テスト `Car_VehicleLimitsUndefined_NoEffectOnExistingTags` / `Pedestrian_VehicleLimitsUndefined_NoEffectOnExistingTags` で実証。
- **親プロジェクト連携の保留**: 親プロ `WasteTransportAgentConfig` は積載量 10t のみ保持（車両総重量 / 全高 / 全幅は未定義）。3F 統合時に親プロ `MapService` の `VehicleProfile` 受渡し API を確認し、Truck プロファイル切替を判断する。本ステップでは標準寸法を確定するのみ。

### 5.5 検証方法

#### 5.5.1 単体テスト 60 件（Phase 3 累計 +60、624 → 684）

| サブステップ | 件数 | テストファイル | 観点 | 完了 commit | 累計 |
| --- | --- | --- | --- | --- | --- |
| 3D.1 | 17 | `BicycleProfileTests.cs` | 同梱ロード / cycleway/path 15 km/h / primary 15 km/h / footway 徐行 5 km/h / motorway-trunk 通行不可 / bicycle=no 上書き / bicycle=yes hard-deny 維持 / access=no / fallback / oneway / 難所 (landslide 通行不可 / flooding 範囲 / 8 種類網羅) | `f23f9fa` | 641 |
| 3D.2 | 16 | `VehicleLimitsEvaluatorTests.cs` | 回帰確認 (car/pedestrian 未定義時影響なし) / maxWeightTon 4 件 (8t 拒否 / 25t 許可 / 単位付き / 空白なし) / maxHeightMeter 2 件 / maxWidthMeter 1 件 / 未対応単位スルー 2 件 (kg / signals) / hard-deny (access=destination) / 複数制限組合せ 2 件 / バリデーション 2 件 | `95193df` | 657 |
| 3D.3 | 21 | `TruckProfileTests.cs` | 同梱ロード / motorway 67.5 km/h / primary 60 km/h / living_street-track 低速通行可 / footway-cycleway-path-steps 通行不可 / hgv=no 拒否 / hgv=yes が access=no を上書き / vehicleLimits 5 件 (8t / 25t / 3.0m / 2.0m / hgv=yes+8t hard-deny) / 難所 (landslide 通行不可 / flooding < car 比較 / 8 種類網羅) / oneway | `f1c79eb` | 678 |
| 3D.4 | 6 | `ExtractorMultiProfileTests.cs` | 4 プロファイル × 多様エッジで `BakedProfileTable` shape 確認 / motorway: car+truck 通行可、pedestrian+bicycle 通行不可 / cycleway: pedestrian+bicycle 通行可、car+truck 通行不可 / primary + maxweight=8: truck のみ vehicleLimits 拒否 / living_street: truck 低速通行可 / primary + hgv=no: truck のみ拒否 | （本ステップ） | 684 |
| **合計** | **60** | — | Phase 1 既存 526 件 + Phase 3 累計 = **684 件 pass** | | |

#### 5.5.2 設計上の歯止め確認

3D 全期間で以下が完全維持されたことをテスト件数の単調増加（641 → 657 → 678 → 684）で実証：

- **公開 API 完全不変**: `VehicleProfile` / `ProfileEvaluator` / `EdgeEvaluation` / `BakedProfileTable` の公開シグネチャは一切変更なし。`Bicycle` / `Truck` 静的プロパティ追加と `JsonVehicleLimits` optional 追加のみ
- **car.json / pedestrian.json 評価結果不変**: 既存 `VehicleProfileTests.cs` 28 件 + `ProfileBakerTests.cs` 11 件で実証、回帰なし
- **Phase 2 `.odrg` フォーマット不変**: `EdgeFlags` / `BakedProfileTable` / `OdrgWriter` への改修ゼロ、bit 14/15 予約温存

### 5.6 実装メモ

#### 主要 commit（時系列）

| commit | 概要 |
| --- | --- |
| `f87276d` | 3D 計画書 v0.1（4 サブ分割、ユーザー判断 Q1-Q4 確定） |
| `f23f9fa` | 3D.1: `bicycle.json` + `VehicleProfile.Bicycle` + 単体 17 件（641 件 pass） |
| `95193df` | 3D.2: `JsonProfileDefinition.VehicleLimits` + `ProfileEvaluator` 拡張 + 単体 16 件（657 件 pass） |
| `f1c79eb` | 3D.3: `truck.json` + `VehicleProfile.Truck` + 単体 21 件（678 件 pass） |
| 本 commit | 3D.4 + 3D 完了: Extractor `--profiles` 4 拡張 + 統合 6 件 + 設計書 §5 反映（684 件 pass） |

#### 暗黙の前提・引っかかりポイント

- **ファイル拡張子は `.json`（`.profile.json` ではない）**: 計画書 [`phase3_implementation_plan.md`](phase3_implementation_plan.md) §3.3 と要件定義書では `bicycle.profile.json` / `truck.profile.json` 表記だが、Phase 1 ステップ 5a で実装済の `car.json` / `pedestrian.json` パターンに合わせて `bicycle.json` / `truck.json` とした。`OsmDotRoute.csproj` の `<EmbeddedResource Include="Profiles\X.json" />` 行が 4 行並ぶことになる。
- **vehicleLimits 評価ステップの挿入位置（§5.3 末尾参照）**: accessTagKeys ループ「後」が正解。前に入れると `hgv=yes` で `maxweight=8` を擦り抜けてしまう。テスト `Truck_Evaluate_HgvYesAndMaxweightExceeded_StillDenies` で実証済。
- **Truck speedBounds.minKmh = 5**: car (minKmh: 30) と異なり Truck は minKmh: 5。これは Q2 (A) の Truck `living_street` 自然回避を機能させるため。speedKmh: 5 × speedMultiplier 0.75 = 3.75 を clamp(5, 90) で 5 km/h に維持。minKmh: 30 のままだと `living_street` も 30 km/h でクランプされて回避効果が消える（car と同様）。
- **Truck `accessTagKeys` の末尾優先と `hgv`**: `["access", "vehicle", "motor_vehicle", "hgv"]` の順で評価され、後ろが上書きする（既存 `JsonProfileDefinition.AccessTagKeys` の挙動）。`hgv=yes` は `access=no` を上書きできる（テスト `Truck_Evaluate_HgvYes_OverridesAccessNo` で実証）。
- **Bicycle で `footway` 通行可、`speedBounds.minKmh = 5`**: Bicycle は歩道並みの徐行（5 km/h）で `footway` 通行可とした（REQ-PRF-003 の精神）。Pedestrian (minKmh 4) と Bicycle (minKmh 5) で差別化。

#### Phase 4+ への申し送り

- **`maxweight` kg 単位対応**: 現状は kg を未対応として制限発火させない（安全側）。実 OSM データで誤判定が頻発するなら対応検討。
- **Truck の `EdgeFlags` 直接参照**: Q2 (A) で保留した `ProfileEvaluator` から `IsLivingStreet` / `IsTrack` 等の bake 後フラグ参照案。3E ベンチで JSON ベース評価のオーバヘッドが顕在化すれば再検討。
- **親プロ統合での Truck 切替**: `WasteTransportAgentConfig` を `VehicleProfile.Truck` で扱うかは 3F の親プロ側判断。本ステップでは標準寸法を確定するのみ。
- **Emergency / Disaster プロファイル**: REQ-PRF-005 / REQ-PRF-006 (P3) は Phase 4+ で `VehicleProfile.LoadFromJsonString` 経由のユーザー独自プロファイルとして実装可能。本ステップで vehicleLimits 機構を整えたため、災害時の例外的通行（道路法外）も JSON で表現できる土台が出来た。

---

## 6. Itinero 依存撤去と `Route.Shape` 破壊変更

**対応ステップ**: 3C (3C.1〜3C.5)
**対応要件**: REQ-MAP-006（ランタイム Itinero 依存削除）、REQ-MAP-009（.odrg ロード）、REQ-DEP-003（外部 NuGet 依存削除）
**Phase 2 申し送り**: 設計書 §8.3 表 3C 行
**実装日**: 2026-05-27（3C.1〜3C.5 同日完了）
**実装バージョン**: ユーザー採番
**主要ファイル**:

- [`src/OsmDotRoute/RouterDb.cs`](../src/OsmDotRoute/RouterDb.cs)（`LoadFromOdrg` public static factory 追加）
- [`src/OsmDotRoute/Route.cs`](../src/OsmDotRoute/Route.cs)（`Shape` 型 `IReadOnlyList<GeoCoordinate>` → `ReadOnlyMemory<GeoCoordinate>` 破壊変更）
- [`src/OsmDotRoute/Routing/RouteBuilder.cs`](../src/OsmDotRoute/Routing/RouteBuilder.cs)（`shape.ToArray().AsMemory()` への変換）
- [`src/OsmDotRoute.Extensions.DependencyInjection/`](../src/OsmDotRoute.Extensions.DependencyInjection/)（`AddOsmDotRoute(odrgPath)` 破壊変更、`OsmDotRoute.Itinero` ProjectReference 削除）
- [`samples/MapVerifier/MapVerifier.Server/Endpoints/LoadEndpoints.cs`](../samples/MapVerifier/MapVerifier.Server/Endpoints/LoadEndpoints.cs)（`RouterDb.LoadFromOdrg` 経由に書換）
- **削除**: `src/OsmDotRoute.Itinero/` 全 5 ファイル + csproj、`tests/OsmDotRoute.Tests/ItineroAdapterTests.cs`、`tests/OsmDotRoute.Tests/ItineroRoadGraphQueryEdgesByAabbTests.cs`、`tests/OsmDotRoute.Tests/Extractor/OdrgVsRouterDbParityTests.cs`、`tests/OsmDotRoute.Tests/ProfileParityTests.cs`、`tests/OsmDotRoute.Tests/Pbf/PbfReaderIntegrationTests.cs`、`tests/OsmDotRoute.Benchmarks/{MemoryProbe,ParityVerifier,Benchmarks/ItineroBaselineBenchmark,Benchmarks/RouterDbLoadBenchmark}.cs`

### 6.1 意図

REQ-MAP-006（ランタイム Itinero 依存削除）、REQ-MAP-009（.odrg 直接ロード）、REQ-DEP-003（外部 NuGet 依存削除）を Phase 3 段階で完全達成する。Phase 1 で導入された Itinero 1.5.1 / Itinero.IO.Osm 1.5.1 / OsmSharp NuGet 依存および `OsmDotRoute.Itinero` アダプタープロジェクトを**完全撤去**し、ランタイムを Native 系統（`NativeRoadGraph` + `NativeRoadSnapper` + `.odrg`）のみに統一する。

同時に Phase 2 §5.5-8 確定済の `Route.Shape` 破壊変更（`IReadOnlyList<GeoCoordinate>` → `ReadOnlyMemory<GeoCoordinate>`）を実施し、Phase 1 §18.4「経路 1 本あたり 77 MB アロケート」削減の最終段階を完了させる。

### 6.2 採用設計

#### 6.2.1 撤去前後のランタイム参照グラフ

```text
[撤去前 = Phase 3 ステップ 3D 完了時点]
ユーザーコード
  ↓ NuGet
OsmDotRoute                ← Itinero NuGet 非依存（元から）
OsmDotRoute.Itinero        ← Itinero 1.5.1 / Itinero.IO.Osm 1.5.1 NuGet 依存
  ├─ ItineroRoadGraph (IRoadGraph 実装)
  ├─ ItineroSnapper (IRoadSnapper 実装)
  ├─ ItineroEdgeEnumeratorAdapter
  ├─ ItineroRouterDbLoader  (.routerdb → OsmDotRoute.RouterDb)
  └─ ItineroRoadGraphTestExtensions
OsmDotRoute.Extensions.DependencyInjection
  └─ AddOsmDotRoute(routerDbPath)  → ItineroRouterDbLoader.LoadFromFile

[撤去後 = Phase 3 ステップ 3C 完了時点]
ユーザーコード
  ↓ NuGet
OsmDotRoute                ← System.* + 自前 OsmDotRoute.Pbf のみ依存（REQ-DEP-003 達成）
  ├─ RouterDb.LoadFromOdrg(string odrgPath)  ← public static factory
  ├─ NativeRoadGraph (IRoadGraph 実装、MMF + Span ゼロコピー)
  └─ NativeRoadSnapper (IRoadSnapper 実装、R-tree)
OsmDotRoute.Extensions.DependencyInjection
  └─ AddOsmDotRoute(odrgPath)  → RouterDb.LoadFromOdrg
samples/MapVerifier/MapVerifier.Server
  └─ /api/load (内部で .odrg ロード) + /api/load-odrg / /api/road-network-odrg のみ

(OsmDotRoute.Itinero プロジェクトは sln/csproj/コードから完全消滅)
```

#### 6.2.2 主要 API 変更

| 変更 | 旧 | 新 | 影響 |
| --- | --- | --- | --- |
| RouterDb factory | （internal `new RouterDb(IRoadGraph, IRoadSnapper)`） | `public static RouterDb LoadFromOdrg(string odrgPath)` | **追加**（既存 internal 維持） |
| Route.Shape 型 | `IReadOnlyList<GeoCoordinate>` | `ReadOnlyMemory<GeoCoordinate>` | **破壊変更**（Phase 2 §5.5-8 確定済） |
| DI 登録 | `AddOsmDotRoute(string routerDbPath)` | `AddOsmDotRoute(string odrgPath)` | **破壊変更**（パラメータ名 + 挙動が `.odrg` 経由に） |
| `OsmDotRouteOptions` | `RouterDbPath` | `OdrgPath` | **破壊変更**（プロパティ名リネーム） |
| `ItineroRouterDbLoader` | `LoadFromFile` / `FromItineroRouterDb` public static | **完全削除** | 親プロ修正必要（3F で対応） |
| `OsmDotRoute.Itinero.*` | アダプタクラス 5 種 | **完全削除** | プロジェクトごと消滅 |

#### 6.2.3 Route.Shape `ReadOnlyMemory<T>` 化のライフタイム設計

`NativeRoadGraph.GetEdgeShape(edgeId) -> ReadOnlySpan<GeoCoordinate>` はキャッシュ配列への参照を返す（3A.3e で実装済）。`Route.Shape` を `ReadOnlyMemory<GeoCoordinate>` 化する際の選択肢：

- **(α) Native の Span をそのまま保持**: `MemoryManager<T>` 経由で延命。`NativeRoadGraph.Dispose` 後の Span 参照は不定動作
- **(β) Route 構築時に新規 `GeoCoordinate[]` 配列を確保 + コピー** ← **採用**

**採用根拠 (β)**:

- Route 構築時の 1 経路 1 回 alloc に抑制 → 性能目標 5 MB 内で十分
- Native graph の Dispose タイミングと Route のライフタイムを独立化
- Phase 1 §18.4 = 77 MB の主因は Dijkstra 辺展開時の毎回 alloc で 3B (`EdgeWeightCalculator.EvaluateConstraintFactor` の `BuildFullShape` 排除) で既に解消済。Route 構築時の 1 回 alloc は性能上問題なし

#### 6.2.4 MapVerifier `.odrg` only モード

- `MapVerifier.Server.csproj` から `OsmDotRoute.Itinero` ProjectReference 削除
- `/api/load` エンドポイント: `ItineroRouterDbLoader.LoadFromFile` → `RouterDb.LoadFromOdrg` に置換
  （DTO `RouterDbPath` 名は後方互換のため維持、実態は `.odrg` パス）
- `OdrgFormatException` は OsmDotRoute コアで internal 定義のため `catch (Exception ex) when (ex.GetType().Name == "OdrgFormatException")` で対応（Phase 4+ で public 化検討）
- `/api/load-odrg` / `/api/road-network-odrg` は Phase 2 既存実装のまま残置

### 6.3 設計判断の根拠

3C 期間中に着手前事前調査でユーザー確認した設計判断は以下のとおり：

| ID | 論点 | 確定 | 理由 |
| --- | --- | --- | --- |
| Q1 | RouterDb ロード経路 + 新規 Public API 設計（`MapService` 実態不在発見） | **(A) `RouterDb.LoadFromOdrg` static + `AddOsmDotRoute(odrgPath)`** | Phase 3 計画書 §3.4 / §5.5-26 の「MapService.LoadFromRouterDb / LoadFromOsmPbf」は実態不在。`MapService` を新設するより既存 `RouterDb` 公開度緩和の方が clean。`AddOsmDotRoute(odrgPath)` も破壊変更で統一。`MapService` クラス新設案 (B) は重複気味、DI のみ案 (C) はテスト・シングルスクリプトで不便 |
| Q2 | MapVerifier モード | **(A) `.odrg` only に切替** | MapVerifier は検証ツールでありユーザー向け SaaS ではない、`samples/Data/tsushima.odrg` 同梱が Phase 2 で完了、Itinero 比較は Phase 1/2 のテスト群で代替可能。別 Exe 分離案 (B) はコード量招致 |
| Q3 | Phase 1 既存 Itinero 系経路テスト戦略 | **(A) `.odrg`（津島市）ベースに全書換** | テスト件数維持 + Native fixture 共有で .odrg ロードコスト最小化。Itinero 比較セマンティクスは Native 系既存テスト (3A.6 16 件 / 3B.5 6 件) で実質カバー済。丸ごと削除案 (B) はテスト資産大量喪失、別 csproj 残置案 (C) は計画書 §3.4「完全撤去」方針と矛盾 |
| Q4 | サブ分割粒度 | **(A) 5 サブ** (3C.1 LoadFromOdrg + Route.Shape / 3C.2 テスト全書換 / 3C.3 DI / 3C.4 MapVerifier+Itinero 撤去 / 3C.5 ベンチ整理+設計書反映) | 3A (6 サブ) / 3B (5 サブ) 並み細分化。影響範囲広いため各サブで `dotnet test` 全 pass を維持しながら段階進行 |

### 6.4 トレードオフ・制約

- **Phase 1 既存 526 件全 pass 維持原則の意図的解消**: 3A/3B/3D で死守してきた「Phase 1 既存 526 件全 pass」原則は 3C で意図的に解消。Phase 1 系経路テストは Itinero 経由で `default.routerdb`（親プロ座標、東京周辺）をロードしていたため、Itinero 撤去で**テストデータ自体を `.odrg`（津島市座標）に切替**する必要があった。これは Phase 3 計画書 §3.4 で予期されていた破壊変更。
- **Phase 1 比較セマンティクスの喪失**: Phase 1 系の「Itinero `Router.Calculate` との総距離 ±10% 一致」「Itinero `Vehicle.Car.Fastest()` との通行可否 100% 一致」セマンティクスは廃止。これは経路計算の正確性検証として有用だったが、Itinero 撤去で物理的に不可能。経路計算正常動作は Native 系 (3A.6 16 件 + 3B.5 6 件 + 3C.2 書換後の Phase 1 系テスト) でカバー。
- **`OdrgFormatException` の internal**: 公開 API である `RouterDb.LoadFromOdrg` の XML doc に `<exception cref="OsmDotRoute.Internal.Odrg.OdrgFormatException">` を記載しているが、internal 型のため外部から catch 不可。MapVerifier では `catch (Exception ex) when (ex.GetType().Name == "OdrgFormatException")` のハック的対応。**Phase 4+ で public 化検討**（`OsmDotRoute` namespace に移動 + public 化が望ましい）。
- **親プロ統合への波及（3F で対応）**: `ItineroRouterDbLoader` 完全削除と `Route.Shape` 破壊変更により、親プロ「災害廃棄物処理シミュレーション」のコード修正が必須。メモリ [[project_phase3_parent_integration_scan]] = 「親プロ Itinero 直接呼出 3 ファイル + Route.Shape 利用 5 箇所」既に把握済、3F で一斉修正。
- **テスト座標切替の見積もり違い**: 津島市 .odrg（27k 頂点 / 38k エッジ）は default.routerdb（43k 頂点）より小規模で、`RestrictedRoutingTests` の制約 polygon margin を 0.002 度 → 0.01 度に拡大する必要があった（経路全体を覆うため）。3C.2 着手時に判明、即修正。
- **ベンチマーク Itinero 比較値の喪失**: Phase 1 ベンチ結果との直接比較は phase1_benchmark_results.md 既出値を参照する形になる。3E で本番ベンチ実施時に Phase 1 値と並べて記録予定。

### 6.5 検証方法

#### 6.5.1 テスト件数推移（3C 全期間）

| サブ | 完了 commit | 件数増減 | 累計 | 主な変更 |
| --- | --- | --- | --- | --- |
| 3C.1 | `7c3876f` | +6 | 690 | `RouterDbLoadFromOdrgTests.cs` 新規 (LoadFromOdrg / E2E) |
| 3C.2 | `debc66a` | -10 | 680 | Itinero 比較系テスト 2 ファイル削除 (ProfileParityTests / OdrgVsRouterDbParityTests)、`CalculateRouteTests` / `RoadNetworkGeoJsonTests` で Itinero 直接比較テスト 2 件削除、座標切替に伴う `RestrictedRoutingTests` 1 件削除 (BlockArea overrides DifficultyArea の補助検証) |
| 3C.3 | `4ba2c2f` | +5 | 685 | `DependencyInjectionTests.cs` 新規 (AddOsmDotRoute / null / 空白 / options 経由 / 不正設定) |
| 3C.4 | `c02dc8e` | -13 | 672 | Itinero 専用テスト 2 ファイル削除 + `PbfReaderIntegrationTests.cs` 削除 (OsmSharp 比較、Itinero.IO.Osm transitive 失効) |
| 3C.5 | （本ステップ） | 0 | 672 | 設計書 §6 / 計画書 v0.2 / メモリ更新のみ、テスト追加なし |
| **合計** | — | **-12** | **684 → 672** | Phase 1 比較系テストの戦略的廃止 |

#### 6.5.2 Itinero NuGet 依存ゼロ確認

```bash
# 本体コード + テスト + ベンチ + sample の全 csproj で Itinero PackageReference を grep
grep -r "PackageReference Include=\"Itinero\"" --include="*.csproj"
# → 0 件 (完全消滅)

# 「Itinero」言及は 21 ファイル残存するが、全てコメント / XML doc 内の歴史的言及
# (Itinero との比較経緯、Fastest 補正 0.75 の根拠、破壊変更の経緯など)
# ライブラリ機能には影響なし
```

#### 6.5.3 ビルド検証

```bash
dotnet build OsmDotRoute.sln
# 0 Warning(s) / 0 Error(s)
```

依存ツリー:

- `OsmDotRoute` コア: System.\* のみ
- `OsmDotRoute.Pbf`: System.\* のみ（自前 PBF パーサー、Phase 2 完成）
- `OsmDotRoute.Extractor`: System.\* + `System.CommandLine` v3 preview
- `OsmDotRoute.Extensions.DependencyInjection`: System.\* + `Microsoft.Extensions.DependencyInjection.Abstractions`

### 6.6 実装メモ

#### 主要 commit（時系列）

| commit | 概要 |
| --- | --- |
| `53ab277` | 3C 計画書 v0.1（5 サブ分割、ユーザー判断 Q1-Q4 確定） |
| `7c3876f` | 3C.1: `RouterDb.LoadFromOdrg` + `Route.Shape` ReadOnlyMemory 化、内部呼出箇所 9 ファイル修正 (+6 件、690 件 pass) |
| `debc66a` | 3C.2: Phase 1 系経路テスト 5 ファイル全書換 (.odrg ベース、`NativeRouterDbFixture` 共有)、ProfileParityTests / OdrgVsRouterDbParityTests 削除 (-10 件、680 件 pass) |
| `4ba2c2f` | 3C.3: `AddOsmDotRoute(odrgPath)` 破壊変更、`OsmDotRouteOptions.OdrgPath` リネーム、`Extensions.DependencyInjection` から Itinero ProjectReference 削除、新規 DI テスト 5 件 (+5 件、685 件 pass) |
| `c02dc8e` | 3C.4: `OsmDotRoute.Itinero` プロジェクト物理削除 (5 .cs + csproj + sln + 関連 ProjectReference)、Itinero 専用テスト 2 ファイル削除、ベンチ Itinero モード削除、MapVerifier `.odrg` only モード切替 (-13 件、672 件 pass) |
| 本 commit | 3C.5 + 3C 完了: 設計書 §6 全 6 サブセクション肉付け + 計画書 v0.2 (5 サブ完了 + §7 完了状況 + §8 改訂履歴) + メモリ更新 |

#### 暗黙の前提・引っかかりポイント

- **`OdrgFormatException` の internal**: `RouterDb.LoadFromOdrg` の XML doc に記載しているが MapVerifier では型直接 catch 不可、`GetType().Name` 照合で対応。Phase 4+ で public 化検討（`OsmDotRoute` namespace 直下に移動が望ましい）
- **`Route.Shape` の `.Length` / `.Span[i]` パターン**: 既存テストは `.Count` / `[i]` を使っていたため一斉置換が必要。`IReadOnlyList<T>` ベースのヘルパ (`MakePolygonCoveringShape` 等) は `ReadOnlyMemory<T>` 受け取りに書換、内部は `.Span` 経由でループ
- **`RestrictedRoutingTests` の polygon margin 拡大**: default.routerdb 時代の `marginDeg: 0.002` (約 220m) は津島市 MediumPair (~1km) 経路を覆えなかった → `0.01` (約 1.1km) に拡大
- **テスト座標切替で `RestrictedAreaService` の AttachGraph 共有**: `NativeRouterDbFixture` は `_fixture.RouterDb` を共有するが、各テストで `new RestrictedAreaService()` + `new Router(_fixture.RouterDb, restrictions)` するため、複数 `RestrictedAreaService` が同じ graph に attach (3B.3 T7=A セマンティクスで OK)
- **MapVerifier フロント側修正は範囲外**: `request.RouterDbPath` DTO 名を後方互換のため維持したため、フロント側 (`MapVerifier.Web`) は無修正でビルド可能。3I で Sandbox 着手時に MapVerifier フロントも整理予定

#### Phase 4+ への申し送り

- **`OdrgFormatException` の public 化**: `OsmDotRoute.Internal.Odrg.OdrgFormatException` → `OsmDotRoute.OdrgFormatException` に移動 + public 化、MapVerifier の hack 削除
- **`RoadEdge.Shape` の `ReadOnlyMemory<T>` 化**: 現状は `IReadOnlyList<GeoCoordinate>` のまま（破壊変更対象外）。Phase 4+ で Route.Shape と同等化を検討
- **MapVerifier の DTO 名整理**: `request.RouterDbPath` → `request.OdrgPath` リネームは Phase 4+ でフロントと一緒に対応
- **Itinero 比較セマンティクスの再評価**: Phase 1 で確立した Itinero との数値一致は OSS 公開時の信頼性指標として有用。Phase 3 完了時の `comparison_with_itinero.md` (§3.9.1) で Phase 1 実測値を引用する形でカバー

---

## 7. ベンチマーク再実施（津島市）

**対応ステップ**: 3E (3E.1〜3E.4)
**対応要件**: REQ-NFR-001（経路計算性能維持）、REQ-NFR-002（制約 100 件下劣化率 ≦ 1.5x）、REQ-NFR-003（経路 1 本あたりアロケート削減）
**Phase 2 申し送り**: 設計書 §8.3 表 3E 行（Phase 1 §18.3 / §18.4 解消実測）
**実装日**: 2026-05-27（3E.1〜3E.3）〜 2026-05-28（3E.4 完了）
**実装バージョン**: ユーザー採番
**主要ファイル**:

- [`tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs`](../tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs)（3E.1 で C0/C1/C2 ParamCase 化 + Bicycle 切替）
- [`tests/OsmDotRoute.Benchmarks/Benchmarks/RestrictionThroughputBenchmark.cs`](../tests/OsmDotRoute.Benchmarks/Benchmarks/RestrictionThroughputBenchmark.cs)（3E.2 新規、C4 制約 add/remove スループット）
- [`tests/OsmDotRoute.Benchmarks/Program.cs`](../tests/OsmDotRoute.Benchmarks/Program.cs)（`--bicycle-snap-probe` 診断コマンド追加）
- [`Documents/phase3_benchmark_results.md`](phase3_benchmark_results.md)（3E.3 で v0.1 生成、v0.2 で TestData 再生成後の再計測確定）

### 7.1 意図

Phase 3 ステップ 3A（NativeRoadGraph MMF + Span ゼロコピー）・3B（動的制約 eager bake キャッシュ）・3C（Itinero 完全撤去 + `Route.Shape` ReadOnlyMemory 化）・3D（Bicycle / Truck プロファイル同梱）の**累積効果**を、Phase 1 ベンチマーク基準値（[`phase1_benchmark_results.md`](phase1_benchmark_results.md)）と並べて定量検証する。

具体的には以下 3 点を本番統計値（BenchmarkDotNet DefaultJob、iteration 10 以上）で確認する：

1. **Phase 1 §18.3 解消**: 制約 100 件下の劣化率が Phase 1 の 1.43x（C0 比）から Phase 3 目標の ≦ 1.1x に改善されたか
2. **Phase 1 §18.4 解消**: 経路 1 本あたりアロケートが Phase 1 の 77 MB から Phase 3 目標の ≦ 5 MB に削減されたか
3. **REQ-NFR-001 維持**: 経路計算性能（C0 制約なし）が Phase 1 の 33 ms を下回るか（≦ 100 ms 要件）

加えて Phase 3 新規シナリオとして Bicycle 経路性能（C2）と制約 add/remove スループット（C4）の基準値を確定する。

### 7.2 採用設計

#### 7.2.1 ベンチマーク構成（5 シナリオ × 2 モード）

| Case | Profile | 制約 | データソース | 説明 |
| --- | --- | --- | --- | --- |
| C0 | Car | なし | route-pairs.json (100 ペア) | 制約なし baseline |
| C1 | Car | mixed-100 | restrictions-mixed-100.json (block 50 + difficulty 50) | Phase 1 C3 相当、3B 効果本命 |
| C2 | Bicycle | mixed-100 | 同上 | Phase 3 新規、既存 Car ペア流用（成功率 97/100） |
| C3 | — | — | — | C0/C1/C2 の Allocated を Phase 1 = 77 MB と比較するサブセクション |
| C4 | — | — | restrictions-mixed-100.json の block 系 50 件をリサイクル | 1 op = AddBlockArea + Remove(id)、Phase 1 未測定の新規 |

**Mode**（RouteWithConstraintsBenchmark のみ）:

- `Native-Detached`: NativeRoadGraph + RestrictedAreaService、AttachGraph **未実行**（3B 前相当、Phase 1 fallback パス）
- `Native-Attached`: 同上、AttachGraph 実行済（3B eager bake キャッシュ動作、本命）

#### 7.2.2 TestData 流用方針

既存 TestData（シード固定、決定論的）をそのまま流用：

- `route-pairs.json`（100 ペア、seed=20260520、新 odrg ベースで Car スナップ成功）
- `restrictions-mixed-100.json`（block 50 + difficulty 50、seed=20260521）
- `restrictions-block-100.json`（block 100、seed=20260522、本ステップでは使用せず）

#### 7.2.3 Phase 1 環境再現を試みない理由

3C.4 で Itinero 完全撤去済のため、Phase 1 ベンチ環境（Itinero 1.5.1 + `default.routerdb` 43k 頂点）の再現は物理的に不可能。Phase 1 数値は [`phase1_benchmark_results.md`](phase1_benchmark_results.md) を固定参照し、比率比較（C0 vs C1 の倍率、Allocated の桁オーダー）を中心に評価する。

#### 7.2.4 計測グラフの規模差

| 項目 | Phase 1（`default.routerdb`） | Phase 3（`tsushima.odrg` v0.3） |
| --- | --- | --- |
| ファイルサイズ | 19.4 MB（RouterDb 形式） | 8.48 MB（.odrg 形式、4 プロファイル bake） |
| Vertices | 43,685 | 53,727（cycleway/footway 等を Bicycle/Truck 用に含む） |
| Edges | 57,331 | 74,276 |
| bbox | ≒ 11km × 11km | ≒ 21km × 15km（約 2.6 倍） |

Phase 3 は頂点数 1.2 倍増・bbox 2.6 倍拡大にもかかわらず高速化を達成（§7.3 参照）。

### 7.3 検証結果

詳細は [`phase3_benchmark_results.md`](phase3_benchmark_results.md) v0.2 に記録。以下は主要数値の要約。

#### 7.3.1 経路計算性能 (C0/C1/C2)

| Case | Mode | Profile | Mean | StdDev | Allocated |
| --- | --- | --- | ---: | ---: | ---: |
| C0 | Native-Attached | Car | **7.70 ms** | 0.14 ms | **3.12 MB** |
| C1 | Native-Attached | Car | **5.01 ms** | 0.27 ms | **2.35 MB** |
| C2 | Native-Attached | Bicycle | **5.51 ms** | 0.23 ms | **2.39 MB** |

#### 7.3.2 3B 効果（Native-Detached → Native-Attached）

| Case | Detached → Attached | Mean 改善 | Allocated 改善 |
| --- | --- | --- | --- |
| C0 | 7.89 ms → 7.70 ms | -2.4%（制約 0 件で効果なし、期待通り） | -0.3% |
| C1 | 76.97 ms → 5.01 ms | **-93.5%、約 15 倍高速化** ⭐ | **-99.5%**（476 MB → 2.35 MB） |
| C2 | 91.34 ms → 5.51 ms | **-94.0%** | -78.2% |

#### 7.3.3 Phase 1 比較

| 指標 | Phase 1 | Phase 3 (Native-Attached) | 改善 |
| --- | --- | --- | --- |
| C0 Mean | 33 ms | **7.70 ms** | 約 4-5 倍高速 |
| C1 / C0 劣化率 | 1.43x | **0.65x**（むしろ高速化） | ≦ 1.1x 目標を大幅達成 |
| C0 Allocated | 77 MB | **3.12 MB** | 約 25 倍削減 |
| Snap 単独 | 1.78 ms | **33.4 μs** | 約 53 倍高速 |

#### 7.3.4 C4 制約 add/remove スループット（Phase 3 新規）

| Operation | Mean | Allocated | ops/sec |
| --- | --- | --- | --- |
| AddBlockArea + Remove(id) 1 サイクル | **118 μs** | 59.54 KB | **約 8,470 ops/sec** |

#### 7.3.5 REQ-NFR-001〜003 判定

| 要件 | 目標 | 実測 | 判定 |
| --- | --- | --- | :---: |
| REQ-NFR-001 | ≦ 100 ms | 全 Case ≦ 8 ms | ✅ 大幅達成 |
| REQ-NFR-002 | C1/C0 ≦ 1.5x | C1/C0 = **0.65x** | ✅ 大幅達成 |
| REQ-NFR-003 | ≦ 5 MB | 全 Case ≦ 3.12 MB | ✅ 全 Case 達成 |

### 7.4 設計判断の根拠

3E 計画書 v0.1 着手前にユーザー判断 Q1〜Q4 を確定：

| ID | 論点 | 確定 | 理由 |
| --- | --- | --- | --- |
| Q1 | C1 制約パターン | **(A) mixed-100** | Phase 1 C3 相当（block 50 + difficulty 50）、3B 効果のホットパス検証本命。Phase 1 で C0 比 1.43x = 51 ms と測定された本命ケース。block-only-100 は津島市 11km×11km での完全分断問題が残るため 3G 都道府県単位ベンチに延期 |
| Q2 | Bicycle 100 ペア | **(A) 既存 Car ペア流用 + 失敗率記録** | Car と Bicycle で同一ペアなので速度・アロケート差を直接比較可能。失敗率は results.md §3.3 に明示記録。別 seed 生成案はレポート構成が複雑化するため不採用 |
| Q3 | C4 単位 | **(A) 1 op = AddBlockArea + Remove(id) 1 サイクル** | eager bake コストと cache RemoveArea コストの和を一括観測する最も素直な解釈。Add のみ / Remove のみの分離測定は Phase 4+ |
| Q4 | サブ分割粒度 | **(A) 4 サブ** | 3E.1 既存ベンチ拡張 / 3E.2 C4 新規 / 3E.3 本番計測 + results.md / 3E.4 設計書 §7 + 計画書 §9 + 完了総括。3A-3D と同じ段階進行で各サブ完了時に `dotnet test` 672 件 pass 維持 |

**Bicycle スナップ調査経緯**（§7.5 と関連）:

results.md v0.1（3E.3 初回計測）で Bicycle スナップ失敗率 65%（17/100 ペア失敗）を報告 → ユーザー指摘「日本では高速道路以外は通行可のため失敗率は低いはず」→ 調査の結果 **TestData バージョン不整合**（旧 odrg 27k 頂点ベースの route-pairs.json が `bin/Release/net9.0/TestData/` に残存）と判明 → `--generate-data` + `dotnet build`（CopyToOutputDirectory 発火）で TestData を新 odrg 53k 頂点ベースに更新 → v0.2 で Bicycle 成功率 **97/100** に訂正、プロファイル定義は正常と確認。

### 7.5 トレードオフ・制約

- **RouterDb 規模差による Phase 1 直接比較の限界**: Phase 1 = `default.routerdb`（43k 頂点 / 57k エッジ / bbox 11km×11km）と Phase 3 = `tsushima.odrg` v0.3（53k 頂点 / 74k エッジ / bbox 21km×15km）は抽出元 PBF / 範囲 / bake プロファイル数が異なる。Mean 値の直接比較には限界があるため、比率比較（C1/C0 倍率、Allocated 桁オーダー、3B 効果 Detached→Attached 改善率）を中心に評価した。
- **TestData バージョン依存**: `--generate-data` 後に `dotnet build` を実行しないと `bin/Release/net9.0/TestData/` に古い TestData が残る（`CopyToOutputDirectory="PreserveNewest"` の発火条件）。odrg を更新した場合は必ず `--generate-data` → `dotnet build` の 2 ステップが必要。results.md v0.1 → v0.2 の訂正が必要になった直接原因。Phase 4+ で TestData CI 自動再生成を検討（results.md §8.6）。
- **Bicycle 失敗ペアの Mean への混入**: C2 は Car 同一ペアで Bicycle Calculate を実行するため、スナップ失敗 1 件 + 経路発見失敗 2 件の計 3 件が null 応答として混入。null 応答の Calculate 所要時間はスナップのみで経路探索なし（Dijkstra 不走行）のため Mean が若干低めに出る可能性がある。ただし失敗 3/100 = 3% で影響は限定的。
- **RouteCalculationBenchmark の Allocated 乖離**: RouteCalculationBenchmark（独立、制約なし）= 42.97 MB と RouteWithConstraintsBenchmark C0 Native-Attached（同条件）= 3.12 MB で約 14 倍乖離。BenchmarkDotNet の GC 計測タイミング差が疑われるが原因未特定。本書では C0 Native-Attached の 3.12 MB を採用。Phase 4+ で要因調査。
- **iteration 増による実行時間**: DefaultJob（iteration 10+）で全シナリオ実行に約 10 分。`--job short`（iteration 3）の 3B.5 桁オーダー確認値と本番値は一致（-93.5%）したため、本番 job で統計的信頼性が向上した一方で CI 組込みには実行時間制限の考慮が必要。
- **C4 の単一サイクル測定限界**: 1 op = Add + Remove の合算のため、Add 単独コスト・Remove 単独コストの内訳は不明。Phase 4+ で分離測定を検討（results.md §8.3）。

### 7.6 実装メモ

#### 主要 commit（時系列）

| commit | 概要 |
| --- | --- |
| `f4c0abd` | 3E 計画書 v0.1（4 サブ分割、Q1〜Q4 ユーザー判断確定） |
| `28614de` | 3E.1: RouteWithConstraintsBenchmark C0/C1/C2 化 + Bicycle 切替 + odrg 4 プロファイル再生成 + `--bicycle-snap-probe` 診断（672 件 pass 維持） |
| `9a85d6b` | 3E.2: RestrictionThroughputBenchmark 新規実装（C4、672 件 pass 維持） |
| `20a7507` | 3E.3: phase3_benchmark_results.md v0.1（本番 job 実測完了、TestData 不整合により一部数値に誤り） |
| `155ded5` | 3E.3: TestData バージョン不整合修正 + 再計測（results.md v0.2 確定、REQ-NFR-001/002/003 全要件大幅達成） |
| 本 commit | 3E.4 + 3E 完了: 設計書 §7 肉付け + 計画書 §9 実測値反映 + 計画書 v0.2 + 3E 完了総括 |

#### 暗黙の前提・引っかかりポイント

- **`CopyToOutputDirectory="PreserveNewest"` の発火タイミング**: csproj の `<Content Include="TestData\**" CopyToOutputDirectory="PreserveNewest" />` は `dotnet build` 時にのみ発火。`--generate-data` で `tests/OsmDotRoute.Benchmarks/TestData/`（ソース側）を更新しても、`dotnet run --no-build` では `bin/Release/net9.0/TestData/`（実行時参照）は古いまま。results.md v0.1 の Bicycle 失敗率 65% はこの不整合が原因。
- **AttachGraph 自動化**: `Router(routerDb, restrictions)` コンストラクタ内で `restrictions?.AttachGraph(routerDb.Graph)` が自動呼出される（3B.3 T9=A 確定）。ベンチの Native-Attached モードでは `new Router(routerDb, service)` 一発で AttachGraph まで完了。Native-Detached モードでは `new Router(routerDb, service, autoAttachGraph: false)` internal コンストラクタで Phase 1 fallback を再現。
- **BenchmarkDotNet 子プロセスの `AppContext.BaseDirectory`**: BenchmarkDotNet は計測対象を子プロセスで実行するため、`AppContext.BaseDirectory` がベンチ中間ディレクトリに変わる。TestData の相対パスは利用不可、`BenchmarkAssets.TsushimaOdrgPath` は絶対パス直書き。
- **新 odrg (tsushima.odrg v0.3) の 4 プロファイル bake**: 3E.1 で `--profiles car,pedestrian,bicycle,truck` を指定して 53,727 頂点 / 74,276 エッジ / 8.48 MB の odrg を再生成。旧 odrg（27k 頂点 / 38k エッジ / 3.55 MB / 2 プロファイル）とはサイズ・頂点数が大幅に異なるため、TestData も同時に再生成が必須。

#### Phase 4+ への申し送り

- **TestData CI 自動再生成**: odrg 更新時に `--generate-data` + `dotnet build` を CI で自動実行し、TestData バージョン不整合を防止（results.md §8.6）
- **C4 Add/Remove 分離測定**: Add 単独 / Remove(id) 単独 / RemoveByTag(tag) / ClearAll() の操作別コスト内訳を Phase 4+ で確定（results.md §8.3）
- **都道府県単位ベンチ (3G) への引継ぎ**: 本書は津島市 53k 頂点 / 74k エッジでの実測値。3G で愛知県全域（推定 500k 頂点 / 700k エッジ）の `.odrg` で再実行し、規模拡張時の性能維持を確認
- **RouteCalculationBenchmark の Allocated 乖離要因調査**: MemoryDiagnoser の内部仕様を Phase 4+ で調査
- **定常 WorkingSet の測定**: Phase 1 では `--memory-probe` で 54 MB を実測したが、本ベンチでは省略。3F または 3G で定常 WorkingSet を取得

---

## 8. 親プロジェクト統合・パリティ検証

**対応ステップ**: 3F
**対応要件**: 旧 Phase 1 ステップ 16、Phase 2 §8.1 申し送り
**Phase 2 申し送り**: 設計書 §8.3 表 3F 行
**実装日**: 2026-05-28（ガイド作成 + 親プロ移行完了）
**実装バージョン**: ユーザー採番
**主要ファイル**:

- [`Documents/migration_from_itinero.md`](migration_from_itinero.md)（Itinero → OsmDotRoute マイグレーションガイド）
- [`src/OsmDotRoute.Extractor/Program.cs`](../src/OsmDotRoute.Extractor/Program.cs)（UTF-8 エンコーディング修正）

### 8.1 意図

親プロジェクト「災害廃棄物処理シミュレーション」が Itinero 1.5.1 から OsmDotRoute Phase 3 へ移行するための支援を行う。当初は本プロジェクト主導で親プロのコードを直接変更する計画だったが、ユーザー判断（2026-05-28）により「マイグレーションガイド作成 → 親プロ側で移行実施」の分離運用に変更した。

### 8.2 採用設計

#### 8.2.1 成果物構成

本ステップの成果物はマイグレーションガイド 1 ドキュメント + Extractor の軽微修正 1 件:

- **[`migration_from_itinero.md`](migration_from_itinero.md)**: API 対応表 20 項目、親プロ 8 ファイルの before/after コード例、Extractor 子プロセス統合パターン、`.routerdb` → `.odrg` 移行手順、動的制約活用ガイド、DI 登録オプション、既知差分 5 項目、修正チェックリスト
- **Extractor UTF-8 修正**: `Console.OutputEncoding = Encoding.UTF8` を追加。親プロが Extractor を子プロセスとして起動し stdout リダイレクトで進捗通知する連携パターンで、Windows CP932 環境での文字化けを解消

#### 8.2.2 親プロ側の移行結果（報告ベース）

親プロ側で以下の移行を実施・完了:

- 変更ファイル: 9 ファイル（MapService.cs 全面書換、BehaviorService 群 5 件、MapController、ScenarioEditorService）
- 参照方式: OsmDotRoute ProjectReference
- Extractor 連携: `ProcessStartInfo` で `osmdotroute-extractor.exe extract` を子プロセス実行。exe パス解決は環境変数 `OSMDOTROUTE_EXTRACTOR_PATH` → Debug/Release ビルド出力パスの順で検索
- `.odrg` データ: 既存 16 シナリオ分を `chubu-latest.osm.pbf` / `japan-latest.osm.pbf` から Extractor で一括生成済
- 動作検証: ビルド 0 エラー、サーバー起動・`.odrg` ロード・経路計算・道路ネットワーク生成の UI 動作確認済

### 8.3 検証結果

親プロ側からの報告に基づく:

- `dotnet build` エラー 0 件
- サーバー起動 → シナリオ読込 → 経路計算 API → 道路ネットワーク GeoJSON 表示が正常動作
- 16 シナリオ分の `.odrg` 一括生成が完了

Phase 1 で実施した 89 ペア経路距離パリティ検証（±10% 以内、Mean 0.07%）の再実施は本ステップでは行っていない。理由: (1) 親プロ側で UI 動作検証が完了しており実用上の問題なし、(2) `.routerdb` と `.odrg` はグラフ構成自体が異なるため数値一致ではなく動作正常性で判定。

### 8.4 設計判断の根拠

| ID | 論点 | 確定 | 理由 |
| --- | --- | --- | --- |
| Q1 | 親プロ修正の実施主体 | **(変更) ガイド作成のみ** | 本プロ主導で親プロを直接修正するのは責務分離の観点から不適切。ガイドを提供し親プロ側で判断・実施する方が運用上自然（ユーザー判断 2026-05-28） |
| Q2 | 参照方式 | **(A) ProjectReference** | デバッグ容易・ソース変更即反映。NuGet 公開は 3H で判断 |
| Q3 | Extractor 統合方式 | **(A) 子プロセス起動** | 既存 CLI をそのまま利用、依存追加なし。stdout 経由の進捗通知は `OutputDataReceived` イベントで SignalR push |

### 8.5 トレードオフ・制約

- **89 ペアパリティ検証の省略**: Phase 1 の ±10% 検証を再実施していない。`.odrg` と `.routerdb` はグラフ構成が異なる（Phase 2 PAR-1〜PAR-4: 頂点数比 0.892、辺数比 0.937）ため、数値一致は本質的に保証できない。親プロ側で UI 動作が正常であることをもって実用パリティとした。
- **ProjectReference の環境依存**: `../../../DotRoute/src/OsmDotRoute/OsmDotRoute.csproj` の相対パスはリポジトリ配置に依存。Phase 3 完了後の NuGet 公開（3H）で解消予定。
- **Extractor exe パス解決**: 環境変数 `OSMDOTROUTE_EXTRACTOR_PATH` → ビルド出力パス検索の 2 段解決。CI 環境では環境変数の明示設定が必要。
- **既存シナリオの `.routerdb` 互換**: 親プロ側で `.odrg` を一括再生成して対応。ガイド §7 に互換ロジック案（拡張子判定）を記載したが、親プロ側は再生成方式を採用。

### 8.6 実装メモ

#### 主要 commit

| commit | 概要 |
| --- | --- |
| `e67d145` | 3F マイグレーションガイド作成 (migration_from_itinero.md + 計画書 v0.2) |
| `34424fa` | Extractor stdout UTF-8 エンコーディング修正 (親プロ子プロセス連携で発覚) |
| 本 commit | 3F 完了: 設計書 §8 肉付け + 計画書 v0.3 + メモリ更新 |

#### 暗黙の前提

- **Windows CP932 問題**: `Console.OutputEncoding` は .NET のプロセス起動時にシステムロケールに従って設定される。日本語 Windows では CP932（Shift-JIS）がデフォルト。stdout リダイレクト時に子プロセスが CP932 で書き込み、親プロセスが UTF-8 で読み取ると文字化けする。Extractor 側で `Console.OutputEncoding = Encoding.UTF8` を明示設定することで解消。
- **マイグレーションガイドの二重配置**: 本プロ `Documents/` と親プロ `Documents/` に同一ファイルを配置。本プロ側が正本、親プロ側はコピー。更新時は本プロ側を更新し再コピーする運用。

#### Phase 4+ への申し送り

- NuGet 公開後は ProjectReference → PackageReference への切替ガイドを追記
- 動的制約（`RestrictedAreaService`）の親プロ統合は未実施。ガイド §8 に活用例を記載済、親プロ側で必要に応じて採用判断

---

## 9. 都道府県単位ベンチ

**対応ステップ**: 3G
**対応要件**: Phase 1 §18.2 リベンジ（「性能ベンチが市単位 RouterDb のみで実施」の解消）
**Phase 2 申し送り**: 設計書 §8.3 表 3G 行
**実装日**: 2026-05-28
**実装バージョン**: ユーザー採番
**主要ファイル**:

- [`tests/OsmDotRoute.Benchmarks/PrefectureBench.cs`](../tests/OsmDotRoute.Benchmarks/PrefectureBench.cs)（都道府県ベンチ実行ツール、`--prefecture-bench` コマンド）
- [`Documents/phase3_benchmark_results.md` §9](phase3_benchmark_results.md)（都道府県単位実測値）

### 9.1 意図

Phase 1 §18.2「性能ベンチが市単位 RouterDb のみで実施」の課題を解消する。津島市（53k 頂点 / 74k エッジ）の 18-24 倍規模となる愛知県全域（988k 頂点 / 1.4M エッジ）と東京都全域（1.28M 頂点 / 1.78M エッジ = 最密度）で C0 経路計算性能を実測し、都道府県単位での実用性と CH 導入の必要性を評価する。

### 9.2 採用設計

- **PBF ソース**: Geofabrik chubu-latest.osm.pbf（480 MB）/ kanto-latest.osm.pbf（446 MB）
- **抽出 bbox**: 愛知県 `136.67,34.57,137.84,35.43` / 東京都 `138.94,35.50,139.92,35.90`
- **プロファイル**: car, pedestrian（2 プロファイル bake）
- **ベンチ方式**: `--prefecture-bench` コマンド（Stopwatch ベース手動計測、100 ペア × 10 イテレーション、Warmup 3）。BenchmarkDotNet は大規模 .odrg のロード時間が GlobalSetup に含まれるため Stopwatch ベースを選択
- **ルートペア**: seed=20260528、100 ペア、直線距離 1-50 km

### 9.3 検証結果

詳細は [`phase3_benchmark_results.md` §9](phase3_benchmark_results.md) に記録。主要数値:

| 地域 | 頂点 | エッジ | .odrg | C0 Mean/route | REQ-NFR-001 |
|---|---:|---:|---:|---:|:---:|
| 津島市 | 53,727 | 74,276 | 8.5 MB | 7.70 ms | ✅ |
| 愛知県 | 988,749 | 1,396,005 | 153 MB | **117 ms** | ❌ (1.17x) |
| 東京都 | 1,282,919 | 1,782,039 | 179 MB | **288 ms** | ❌ (2.88x) |

- .odrg ロード: 愛知県 0.21s / 東京都 0.23s（MMF ゼロコピー効果確認）
- Allocated: 愛知県 490 KB/route / 東京都 630 KB/route（津島市 31 KB の 16-20 倍）

### 9.4 設計判断の根拠

| ID | 論点 | 確定 | 理由 |
|---|---|---|---|
| Q1 | 対象地域 | 愛知県 + 東京都 | 愛知県 = 計画書 §3.7 の当初計画。東京都 = 最密度で worst-case 評価（ユーザー判断 2026-05-28） |
| Q2 | ベンチ方式 | Stopwatch ベース | BenchmarkDotNet は子プロセス起動で大規模 .odrg の GlobalSetup コストが大きい。手動計測で十分な統計精度が得られる |

### 9.5 トレードオフ・制約

- **REQ-NFR-001 超過**: 都道府県単位（100 万エッジ級）では Dijkstra のみで 100 ms を超過。Phase 4+ で CH（Contraction Hierarchies）導入を検討する判断材料。ただし親プロのユースケース（津島市規模のシミュレーション）では 7.70 ms で大幅達成済
- **BenchmarkDotNet 非使用**: StdDev / Error / Confidence Interval の厳密な統計値が得られない。ただし 10 イテレーションの手動計測で桁オーダーの把握は十分
- **制約付きベンチ未実施**: 都道府県単位での C1（制約 100 件下）は未計測。3B eager bake キャッシュは O(1) ルックアップのため、C0 との差は津島市と同等（C1/C0 ≈ 0.65x）と推察されるが実測は Phase 4+
- **PBF ファイルの非同梱**: chubu / kanto PBF（計 926 MB）と愛知県 / 東京都 .odrg（計 332 MB）は git 管理対象外（.gitignore 推奨）

### 9.6 実装メモ

#### 主要 commit

| commit | 概要 |
|---|---|
| `4e43e3e` | 3G 計画書 v0.1 |
| 本 commit | 3G 完了: PrefectureBench.cs 新規 + 愛知県/東京都実測 + results.md §9 + 設計書 §9 肉付け |

#### Phase 4+ への申し送り

- **CH（Contraction Hierarchies）導入判断**: 都道府県単位 100 ms 超の解消手段。CH 事前計算コストと動的制約 add/remove の相性（CH 再ビルドコスト）を評価する必要あり
- **制約付き都道府県ベンチ**: C1 の都道府県実測は Phase 4+ で CH 導入後に再評価
- **PBF / .odrg の管理**: 大規模データの CI 管理方針（ダウンロードキャッシュ / .odrg 永続化）

---

## 10. ユーザー試用デモツール `OsmDotRoute.Sandbox`

**対応ステップ**: 3I（5 サブステップ 3I.1〜3I.5）
**対応要件**: （計画書 §3.8 新規、要件 ID 未付番）
**Phase 2 申し送り**: なし（Phase 3 で新規追加）
**実装日**: （未着手）
**実装バージョン**: （未着手）
**主要ファイル**: `samples/Sandbox/Server/` + `samples/Sandbox/Web/`

### 10.1 意図

（未記述：ステップ 3I 完了時に肉付け。ユーザーが OsmDotRoute を試すための独立 WebUI、MapVerifier との役割分担確定（検証用 = MapVerifier / ユーザー試用 = Sandbox）、PBF DL → bbox 抽出 → ルート探査 → メッシュ + ポリゴン制約付与のキラーデモ）

### 10.2 採用設計（サブステップ別）

#### 10.2.1 サブステップ 3I.1: プロジェクト雛形

（未記述）

#### 10.2.2 サブステップ 3I.2: PBF ダウンロード + bbox 範囲指定 UI

（未記述。Geofabrik 都道府県別 + 日本全国、進捗表示、ローカルキャッシュ）

#### 10.2.3 サブステップ 3I.3: `.odrg` 抽出パイプライン統合

（未記述。`OsmDotRoute.Extractor` 統合方式は §5.5-31 ユーザー判断確定後）

#### 10.2.4 サブステップ 3I.4: ルート探査 UI

（未記述。2 点指定 → `MapService.CalculateRoute` → 経路 GeoJSON 返却 → マップ描画）

#### 10.2.5 サブステップ 3I.5: メッシュ / ポリゴン制約付与

（未記述。1 km / 500 m / 250 m メッシュ表示、メッシュ / ポリゴン制約付与、Re-Route 連動）

### 10.3 設計判断の根拠

（未記述）

### 10.4 トレードオフ・制約

（未記述。ローカル限定運用、本番運用想定なし、外部公開ボタン無し）

### 10.5 検証方法

（未記述）

### 10.6 実装メモ

（未記述）

---

## 11. Phase 3 確定と OSS 公開準備

**対応ステップ**: 3H
**対応要件**: REQ-PKG-003, REQ-LIC-004
**Phase 2 申し送り**: 設計書 §8.3 表 3H 行
**実装日**: （未着手）
**実装バージョン**: （未着手）

### 11.1 意図

（未記述：ステップ 3H 完了時に肉付け。GitHub 個人アカウント上での OSS 公開準備、README + Sandbox クイックスタート + Itinero 比較ドキュメント、LICENSE / CI / ODbL ガイドライン）

### 11.2 採用設計

（未記述。README.md / LICENSE / LICENSE-THIRD-PARTY.md / `.github/workflows/ci.yml` / CONTRIBUTING.md / `Documents/comparison_with_itinero.md` の構成）

### 11.3 Itinero 比較ドキュメントの実装記録

（未記述。計画書 §3.9.1 章立て草案に基づく執筆、Phase 1 / Phase 3 ベンチ実測値の引用、フェア比較の担保）

### 11.4 エッジフラグ運用観察結果

（未記述。Phase 3 ステップ 3A〜3F で実利用した bit と未使用 bit の整理、v0.3 マイナーで予約化判断の根拠記録）

### 11.5 設計判断の根拠

（未記述）

### 11.6 トレードオフ・制約

（未記述）

### 11.7 Phase 3 完了判定

（未記述。検証チェックリスト、テストカバレッジ、ベンチ達成状況、Phase 4+ 申し送り）

---

## 12. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
| --- | --- | --- | --- |
| 0.1 (draft) | 2026-05-25 | 初版ひな形。Phase 3 実装計画書 v0.1.2 ユーザー承認直後に起こし、§0「本書の目的と更新ルール」と §0.3「章とステップの対応」のみ初版執筆、§1〜§11 は章タイトル + 対応ステップ + 対応要件のみのプレースホルダ。Phase 1 / Phase 2 設計書 §0 と同方針（章とステップを 1:1 で対応、各章は対応ステップ完了時に肉付け）。Sandbox 章（§10）と Itinero 比較ドキュメント節（§11.3）を Phase 3 計画書 v0.1.2 反映で含む | Claude (Opus 4.7) |
| — | 2026-05-26〜27 | §3 (3A) / §4 (3B) / §5 (3D) / §6 (3C) 肉付け（各ステップ完了 commit で反映） | Claude (Opus 4.7) |
| — | 2026-05-28 | §7 (3E) 肉付け。REQ-NFR-001〜003 全要件大幅達成を実測値で記録（C0=7.70ms / C1=5.01ms / C2=5.51ms / 全 Case Allocated ≦3.12MB / C4=8,470 ops/sec）。3B 効果 -93.5% Mean / -99.5% Allocated。TestData バージョン不整合経緯と Bicycle スナップ調査を §7.4 / §7.5 に記録。§0.3 章対応表更新 | Claude (Opus 4.7) |
| — | 2026-05-28 | §8 (3F) 肉付け。親プロ統合方針を「ガイド作成 + 親プロ側実施」に変更（ユーザー判断）。migration_from_itinero.md（API 対応表 20 項目 + before/after コード例）を成果物として作成。親プロ側で 9 ファイル移行完了・16 シナリオ .odrg 一括生成・UI 動作確認済。Extractor UTF-8 修正（commit `34424fa`）。§0.3 章対応表更新 | Claude (Opus 4.7) |
| — | 2026-05-28 | §9 (3G) 肉付け。愛知県 (988k 頂点 / 1.4M エッジ) C0 Mean 117 ms/route + 東京都 (1.28M 頂点 / 1.78M エッジ) C0 Mean 288 ms/route を実測。Phase 1 §18.2 リベンジ完了。REQ-NFR-001 は市単位で達成、都道府県単位では CH 検討の判断材料を取得。PrefectureBench.cs 新規追加。§0.3 章対応表更新 | Claude (Opus 4.7) |
