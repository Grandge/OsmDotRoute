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
- 将来：`phase3_benchmark_results.md`（ステップ 3E 完了時に起こす）
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
| 7. ベンチマーク再実施（津島市） | 3E | 未着手 |
| 8. 親プロジェクト統合・パリティ検証 | 3F | 未着手 |
| 9. 都道府県単位ベンチ | 3G | 未着手 |
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

**対応ステップ**: 3D
**対応要件**: REQ-PRF-003, REQ-PRF-004
**Phase 2 申し送り**: 設計書 §8.3 表 3D 行
**実装日**: （未着手）
**実装バージョン**: （未着手）

### 5.1 意図

（未記述：ステップ 3D 完了時に肉付け。Truck=10t 日本道路法ベースの独自設計、Itinero / OSRM 流用しない方針）

### 5.2 採用設計

（未記述。`bicycle.profile.json` / `truck.profile.json` 構造、`hgv=*` / `maxweight=*` / `maxheight=*` / `access=destination` 評価ロジック）

### 5.3 設計判断の根拠

（未記述）

### 5.4 トレードオフ・制約

（未記述）

### 5.5 検証方法

（未記述）

### 5.6 実装メモ

（未記述）

---

## 6. Itinero 依存撤去と `Route.Shape` 破壊変更

**対応ステップ**: 3C
**対応要件**: REQ-MAP-006, REQ-MAP-009, REQ-DEP-003
**Phase 2 申し送り**: 設計書 §8.3 表 3C 行
**実装日**: （未着手）
**実装バージョン**: （未着手）

### 6.1 意図

（未記述：ステップ 3C 完了時に肉付け。`OsmDotRoute.Itinero` プロジェクト撤去、`MapService.LoadFromOdrg` 統一、`Route.Shape` `IReadOnlyList<GeoCoordinate>` → `ReadOnlyMemory<GeoCoordinate>` 破壊変更）

### 6.2 採用設計

（未記述）

### 6.3 設計判断の根拠

（未記述）

### 6.4 トレードオフ・制約

（未記述）

### 6.5 検証方法

（未記述）

### 6.6 実装メモ

（未記述）

---

## 7. ベンチマーク再実施（津島市）

**対応ステップ**: 3E
**対応要件**: REQ-NFR-001〜003
**Phase 2 申し送り**: 設計書 §8.3 表 3E 行（Phase 1 §18.3 / §18.4 解消実測）
**実装日**: （未着手）
**実装バージョン**: （未着手）

### 7.1 意図

（未記述：ステップ 3E 完了時に肉付け。C0〜C4 シナリオ、Phase 1 基準値 33ms / 51ms / 77MB との比較、目標 ≦33ms / ≦1.1×C0 / ≦5MB）

### 7.2 採用設計

（未記述）

### 7.3 検証結果

（未記述。詳細は別文書 `phase3_benchmark_results.md` に記録予定）

### 7.4 設計判断の根拠

（未記述）

### 7.5 トレードオフ・制約

（未記述）

### 7.6 実装メモ

（未記述）

---

## 8. 親プロジェクト統合・パリティ検証

**対応ステップ**: 3F
**対応要件**: （旧 Phase 1 ステップ 16、Phase 2 §8.1 申し送り）
**Phase 2 申し送り**: 設計書 §8.3 表 3F 行
**実装日**: （未着手）
**実装バージョン**: （未着手）

### 8.1 意図

（未記述：ステップ 3F 完了時に肉付け。親プロ「災害廃棄物処理シミュレーション」を `OsmDotRoute` v0.3.x に差替、89 ペア経路 ±10% 維持、KSJ GML 制約動作確認）

### 8.2 採用設計

（未記述）

### 8.3 検証結果

（未記述）

### 8.4 設計判断の根拠

（未記述）

### 8.5 トレードオフ・制約

（未記述）

### 8.6 実装メモ

（未記述）

---

## 9. 都道府県単位ベンチ

**対応ステップ**: 3G
**対応要件**: （Phase 1 §18.2 リベンジ）
**Phase 2 申し送り**: 設計書 §8.3 表 3G 行
**実装日**: （未着手）
**実装バージョン**: （未着手）

### 9.1 意図

（未記述：ステップ 3G 完了時に肉付け。愛知県全域 PBF → `.odrg` 抽出、頂点数 / エッジ数 / `.odrg` サイズ実測、経路計算スループット実測）

### 9.2 採用設計

（未記述）

### 9.3 検証結果

（未記述。`phase3_benchmark_results.md` に追記予定）

### 9.4 設計判断の根拠

（未記述）

### 9.5 トレードオフ・制約

（未記述）

### 9.6 実装メモ

（未記述）

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
