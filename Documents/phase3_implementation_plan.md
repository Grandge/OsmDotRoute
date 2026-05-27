# OsmDotRoute Phase 3 実装計画書

**バージョン**: 0.1（ドラフト・骨子）
**作成日**: 2026-05-25
**最終更新**: 2026-05-25
**ステータス**: ドラフト v0.1（骨子のみ・ユーザーレビュー前。Phase 2 v0.2.0 完了直後 = 2026-05-25 起草）
**対象フェーズ**: Phase 3（**データ利用側**：ランタイム `.odrg` 読込 + ランタイム Itinero 依存完全削除 + Bicycle/Truck プロファイル + 性能ベンチマーク + 親プロジェクト統合・パリティ検証 + OSS 公開準備）
**関連ドキュメント**:

- [要件定義書](requirement_definition.md)（v2.3、Phase 2/3 スコープ再編後）
- [Phase 3 設計書](phase3_design.md)（v0.1 で§0 / §0.3 のみ起こし、各章は対応ステップ完了時に肉付け）
- [Phase 2 実装計画書](phase2_implementation_plan.md)（v0.4 確定、§6 表「Phase 3 で実施するステップ」参照）
- [Phase 2 設計書](phase2_design.md)（v0.4、§8「Phase 2 制約事項と Phase 3 申し送り」参照）
- [Phase 2 グラフ形式仕様書](phase2_graph_format_spec.md)（v0.2、`.odrg` 確定仕様）
- [Phase 1 設計書](phase1_design.md)（v0.21、§18「制約事項と既知の課題」参照）
- [Phase 1 ベンチマーク結果](phase1_benchmark_results.md)（Phase 3 ベンチ基準値）

---

## 1. 概要

本書は OsmDotRoute Phase 3（**データ利用側**：`.odrg` ランタイム読込とライブラリ全体からの Itinero 依存撤去）の実装計画を定める。

**Phase 3 のゴール**：

1. ランタイムから `.odrg` を `MemoryMappedFile` + `ReadOnlySpan<T>` でゼロコピー読込する（REQ-MAP-005）
2. 動的制約のホットパスを `.odrg` のエッジ AABB / STR R-tree / エッジフラグで高速化し、Phase 1 §18.3「制約 100 件短絡効果」を実測可能にする
3. ライブラリ全体（ランタイム + 抽出ツール）から Itinero 依存を排除する（REQ-MAP-006 / REQ-MAP-009 / REQ-DEP-003、`OsmDotRoute.Itinero` プロジェクト撤去）
4. Bicycle / Truck（10 t、日本道路法ベース）プロファイルを独自設計で同梱する（REQ-PRF-003 / REQ-PRF-004）
5. 津島市ベンチで Phase 1 基準値（33 ms / 0.48x / 制約 100 件下 51 ms / 経路 77 MB アロケート）を同等以上維持・改善する（Phase 1 §18.4 根治）
6. 親プロジェクト「災害廃棄物処理シミュレーション」を `OsmDotRoute` v0.3.x へ統合し、Phase 1 と同一の経路結果が出ることを検証する（旧 Phase 1 ステップ 16、Phase 2 §8.1 申し送り）
7. 都道府県単位 PBF でベンチを通し、Phase 1 §18.2 リベンジ（Phase 1 では未測定）
8. **ユーザー試用デモツール `OsmDotRoute.Sandbox` を新設**し、PBF ダウンロード → bbox 範囲指定 → `.odrg` 抽出 → ルート探査 → メッシュ／ポリゴン制約付与の一連フローを WebUI で提供（OSS 公開時のキラーデモ）
9. OSS 公開準備（GitHub README / LICENSE / CI 整備、ODbL ガイドライン、Itinero 比較ドキュメント）を完了し、Phase 3 確定（REQ-PKG-003）

**Phase 3 の方針**：

- **Phase 2 で整地済の `.odrg` データ土台をフル活用**（エッジ AABB / STR R-tree / シェイプ連続バッファ / エッジフラグ 14 bit、Phase 2 設計書 §8.3 表参照）。Phase 3 で「読む側」の実装に集中
- **公開 API は Phase 1 を基本踏襲、ただし `Route.Shape` のみ破壊変更**（`IReadOnlyList<GeoCoordinate>` → `ReadOnlyMemory<GeoCoordinate>`、§5.5-8 で v0.2 確定済、Phase 1 §18.4 根治の必須条件）
- **ランタイムは System.\* のみ**で完結（REQ-DEP-003）。`OsmDotRoute.Itinero` を撤去し、Itinero 1.5.1 NuGet 依存を削除
- **非公開リポジトリ運用を Phase 3 完了まで維持**（REQ-PKG-002）。Phase 3 完了時に OSS 公開判断
- ベンチマークは Phase 1 と同じ津島市を主軸、都道府県単位は副次（Phase 1 §18.2 リベンジ位置付け）

**Phase 1 → Phase 2 → Phase 3 のランタイム変遷**：

| 区分 | Phase 1（v0.1.0） | Phase 2（v0.2.0） | Phase 3（v0.3.0 想定） |
| --- | --- | --- | --- |
| `RouterDb` / `.odrg` | RouterDb（Itinero） | RouterDb + `.odrg` 共存（検証） | `.odrg` のみ |
| `IRoadGraph` 実装 | `ItineroRoadGraph` | `ItineroRoadGraph` | **`NativeRoadGraph`**（MMF + Span） |
| `IRoadSnapper` 実装 | `ItineroSnapper` | `ItineroSnapper` | **`NativeRoadSnapper`**（R-tree クエリ） |
| `Route.Shape` 型 | `IReadOnlyList<GeoCoordinate>` | `IReadOnlyList<GeoCoordinate>` | **`ReadOnlyMemory<GeoCoordinate>`** |
| Itinero 依存 | 必須 | 必須（ランタイム） | **完全撤去** |
| `OsmDotRoute.Itinero` | 提供 | 提供 | **撤去** |
| ランタイム System.\* 完結 | 不可 | 不可 | **可**（REQ-DEP-003） |
| 同梱プロファイル | car / pedestrian | car / pedestrian | car / pedestrian / **bicycle** / **truck** |
| 動的制約ホットパス | Phase 1 線形走査 + AABB プリフィルタ | （変更なし） | **R-tree + 「制約 ID → 交差エッジ ID 集合」キャッシュ** |

---

## 2. 前提条件

- [x] Phase 1 完了（v0.1.0 タグ付与済、commit `e5d90f2`、2026-05-20）
- [x] Phase 2 完了（v0.2.0 タグ付与済、commit `59d6ff5`、2026-05-25）
- [x] Phase 2 設計書 §8「Phase 3 申し送り事項」確定（3A〜3H ステップ表）
- [x] `.odrg` 仕様 v0.2 確定（[`phase2_graph_format_spec.md`](phase2_graph_format_spec.md)）
- [x] 要件定義書 v2.3 で Phase 2/3 スコープ再編済（2026-05-20）
- [x] Phase 2 v0.2.0 タグ付与済（`v0.2.0`、commit `59d6ff5`）
- [x] MapVerifier `.odrg` オーバーレイ表示と `samples/Data/tsushima.odrg` 同梱済（commit `4a5a90a`、Phase 3 ランタイム実装の I/O 比較土台）
- [ ] 本実装計画書 v0.1 のユーザーレビュー
- [ ] [`phase3_design.md`](phase3_design.md) ひな形起こし（§0 / §0.3 のみ、Phase 2 設計書 §0 と同方針）

---

## 2.5 設計書の同時更新ルール（Phase 1 / Phase 2 と同方針）

**Phase 3 実装中も、各ステップ完了時に設計書 [`phase3_design.md`](phase3_design.md) の対応章を必ず更新する**。Phase 1 / Phase 2 のルール（[`phase1_implementation_plan.md`](phase1_implementation_plan.md) §2.5、[`phase2_implementation_plan.md`](phase2_implementation_plan.md) §2.5、メモリ [[feedback_design_doc_per_step]]）を踏襲する。

`phase3_design.md` の構成は Phase 2 設計書 §0.3 に倣い、章とステップを対応表で結ぶ：

| 章 | 対応ステップ |
| --- | --- |
| §1 全体概要 | 計画書承認時（§0 / §0.3 と同時起こし） |
| §2 アーキテクチャ概観（Phase 2 → Phase 3 変遷） | 計画書承認時 |
| §3 NativeRoadGraph / NativeRoadSnapper（MMF + Span） | 3A |
| §4 動的制約ホットパス（交差エッジキャッシュ） | 3B |
| §5 Bicycle / Truck プロファイル独自設計 | 3D |
| §6 Itinero 依存撤去と `Route.Shape` 破壊変更 | 3C |
| §7 ベンチマーク再実施（津島市） | 3E |
| §8 親プロジェクト統合・パリティ検証 | 3F |
| §9 都道府県単位ベンチ | 3G |
| §10 ユーザー試用デモツール `OsmDotRoute.Sandbox` | 3I |
| §11 Phase 3 確定と OSS 公開準備 | 3H |
| §12 改訂履歴 | 各ステップ完了時 |

---

## 3. 採用アプローチ

### 3.1 NativeRoadGraph / NativeRoadSnapper（REQ-MAP-005）

- 新規型：`OsmDotRoute.NativeRoadGraph`（`IRoadGraph` 実装）、`OsmDotRoute.NativeRoadSnapper`（`IRoadSnapper` 実装）。`OsmDotRoute` コアプロジェクト直下に追加（新規プロジェクトは作らない、§5.1）
- **MMF（`MemoryMappedFile.CreateFromFile`）+ `MemoryMappedViewAccessor` でビュー化、`unsafe` ポインタ経由で `ReadOnlySpan<T>` を切り出す**（REQ-MAP-005 確定方式、Phase 2 仕様書 §4 セクションテーブル方式）
- セクション別ビュー：頂点表 → `ReadOnlySpan<Vertex>`、エッジ表 → `ReadOnlySpan<Edge>`、エッジシェイプ → `ReadOnlySpan<GeoCoordinate>`、AABB → `ReadOnlySpan<EdgeAabb>`、STR R-tree → `ReadOnlySpan<RTreeNode>`
- `NativeRoadGraph.GetEdgeShape(edgeId) -> ReadOnlySpan<GeoCoordinate>` で**ゼロアロケーション**（Phase 1 §18.4 = 77 MB/route の主因根治）
- `NativeRoadSnapper` は STR R-tree クエリで最近傍候補エッジを取得 → 各候補シェイプへの最短距離計算

### 3.2 動的制約ホットパス（Phase 1 §18.3 解消）

- 内部キャッシュ `internal sealed class RestrictedAreaEdgeCache`：制約 ID → 交差エッジ ID 集合（`HashSet<int>`）
- `RestrictedAreaService.AddBlockArea(polygon)` / `AddDifficultyArea(polygon, type)` 時、STR R-tree で polygon と交差候補エッジを O(log E) で取得 → エッジ AABB + シェイプ多角形交差判定で確定 → キャッシュに格納（1 回計算で固定化）
- Dijkstra 辺展開時は `RestrictedAreaEdgeCache.IsBlocked(edgeId)` ルックアップのみ（HashSet 1 発、O(1)）
- 制約削除時はキャッシュから当該エントリを drop
- **公開 API は Phase 1 のまま死守**（内部実装の高速化に留める、Phase 2 §3.6 設計上の歯止め継承）
- 期待効果：制約 100 件下 51 ms → Phase 1 C0 (35 ms) との劣化率 1.43x を 1.1x 以下に圧縮、REQ-NFR-002 達成余裕拡大

### 3.3 Bicycle / Truck プロファイル独自設計（REQ-PRF-003 / REQ-PRF-004）

- **プロファイル定義 JSON は Phase 1 で外部化済**（メモリ [[project-profile-difficulty-design]]）。Phase 3 では `bicycle.profile.json` / `truck.profile.json` を新規追加
- **Bicycle**：`highway=cycleway` / `highway=path` (`bicycle=yes`) を歩道並みに通行可、`highway=motorway` / `highway=trunk` (`bicycle=no`) を通行不可、平均速度 15 km/h
- **Truck (10 t)**：日本道路法ベース（最大積載量 10 t、車両総重量 20 t 級）。`hgv=*` / `maxweight=*` / `maxheight=*` / `access=destination` 評価、`highway=living_street` 等を回避、Phase 2 エッジフラグ `IsTrack` / `IsLivingStreet` / `IsPedestrianSeparated` を活用
- **Itinero / OSRM の Truck プロファイルは流用しない**（要件定義書 REQ-PRF-004、日本道路法と海外仕様の乖離が大きいため）
- Phase 2 抽出ツール `osmdotroute-extractor` が `--profiles car,pedestrian,bicycle,truck` を受けて bake プロファイル表を 4 列で生成するよう拡張（ステップ 3D 内で対応）
- 動作確認は津島市 PBF で 4 プロファイル全てが経路を返すこと、Bicycle / Truck で経路長が車道経由（pedestrian 経路と異なる）になることを単体テスト化

### 3.4 Itinero 依存撤去（REQ-MAP-006 / REQ-MAP-009 / REQ-DEP-003）

- **`OsmDotRoute.Itinero` プロジェクトを完全撤去**（`ItineroRoadGraph` / `ItineroSnapper` 削除、`OsmDotRoute.sln` から削除）
- **`OsmDotRoute` コア**：Itinero NuGet 参照を削除。`MapService.LoadFromRouterDb()` / `LoadFromOsmPbf()` API を削除し `LoadFromOdrg(path)` のみに統一（**破壊変更**）
- **`OsmDotRoute.Extensions.DependencyInjection`**：DI 登録を `NativeRoadGraph` / `NativeRoadSnapper` 用に書換
- **`OsmDotRoute.Extractor`**：Phase 2 は Itinero 不参照のため影響なし
- **`MapVerifier`**：RouterDb 比較表示の `OsmDotRoute.Itinero` 依存を撤去するか、`MapVerifier.Server` を Itinero ベタ依存に切替するか判断（ステップ 3C 内、§5.6 で確定）
- **`Route.Shape` 破壊変更**：`IReadOnlyList<GeoCoordinate>` → `ReadOnlyMemory<GeoCoordinate>`（Phase 2 §5.5-8 確定済）。親プロジェクト統合（ステップ 3F）で呼び出し側を一斉修正

### 3.5 ベンチマーク再実施（津島市、REQ-NFR-001〜003 維持確認）

- 既存 `tests/OsmDotRoute.Benchmarks/` プロジェクトを流用（Phase 1 ベンチを `--filter` で `Phase3*` シナリオに切り替え可能とする）
- **基準ケース C0**（Phase 1 の C0 と同条件）：津島市 `.odrg`、car プロファイル、89 ペアの経路計算、制約なし
- **C1**：制約 100 件下（Phase 1 C4 と同条件、AABB 100 件分散配置）
- **C2**：制約 100 件下 + Bicycle プロファイル（Phase 3 新規）
- **C3**：経路 1 本あたりアロケート量（`MemoryDiagnoser`、Phase 1 §18.4 = 77 MB の比較）
- **C4**：制約 add/remove スループット（`RestrictedAreaService` 単独、Phase 1 では未測定）
- 目標：C0 ≦ 33 ms（Phase 1 同等）、C1 ≦ 1.1× C0（Phase 1 1.43× から改善）、C3 ≦ 5 MB（Phase 1 77 MB から 1/15）
- 結果は [`phase3_benchmark_results.md`](phase3_benchmark_results.md)（新規）に Phase 1 比較表で記録

### 3.6 親プロジェクト統合・パリティ検証（旧 Phase 1 ステップ 16）

- 親プロジェクト `災害廃棄物処理シミュレーション` を `OsmDotRoute` v0.3.x へ NuGet 参照差替（現状は Itinero 1.5.1 直接利用＋自前ラッパ）
- `Route.Shape` 破壊変更（§3.4）に伴う呼び出し側修正を親プロで実施
- 89 ペアの経路計算結果が Phase 1 RouterDb 経路と ±10% 以内（Phase 1 §18.4 = 89/89 ペア達成と同等以上、Mean 0.07% 維持）
- メッシュコード制約・GML 制約（KSJ A31 浸水想定区域、メモリ [[reference_hazard_sample]]）の動作確認
- 親プロでの動作確認結果は親プロ側ドキュメントに記載、本書には PR / commit リンクのみ残す

### 3.7 都道府県単位ベンチ（Phase 1 §18.2 リベンジ）

- 愛知県全域 PBF（OSM の `Geofabrik` aichi.osm.pbf 等）から `.odrg` 抽出
- 都道府県単位の頂点数 / エッジ数 / `.odrg` サイズを実測
- 89 ペアと同じスケールで経路計算を流し、Phase 1 §18.2 で未測定だった「都道府県単位ベンチ」を実施
- 仕様書 §8 「都道府県単位は数百万エッジ想定、STR R-tree 容量 INT-8 で 100k エッジ検証済」の延長線で実用性確認

### 3.8 ユーザー試用デモツール `OsmDotRoute.Sandbox`（新設）

**位置付け**：MapVerifier は Phase 2 / Phase 3 の検証データ可視化（INT-/PAR- テスト、設計書 §6 §7 突合）に特化し残置。本ツールは**ユーザーが OsmDotRoute を試すための独立 WebUI**として新規追加（§5.5-32 ユーザー判断確定）。OSS 公開時の「Try it」キラーデモを担う。

**配置**：`samples/Sandbox/Server`（ASP.NET Core minimal API）+ `samples/Sandbox/Web`（静的 HTML / JS + Leaflet）。MapVerifier と同構成を踏襲し学習コスト最小化。

**機能（親プロ「Map&シナリオツール」由来 + MapVerifier 由来の融合）**：

| 区分 | 機能 | 出典 |
| --- | --- | --- |
| データ取得 | OSM PBF ダウンロード（都道府県別 + 日本全国） | 親プロ |
| データ取得 | 進捗表示・ローカルキャッシュ管理 | 親プロ |
| 範囲指定 | マップ上の矩形描画で 2 点 bbox を指定 | 親プロ |
| 抽出 | bbox + プロファイル選択で `.odrg` を生成（`OsmDotRoute.Extractor` を内部呼出） | 親プロ |
| 経路探査 | 2 点指定 → ルート計算 → マップ上に経路描画 | MapVerifier |
| 経路探査 | 距離 / 所要時間 / 経由エッジ数の表示 | MapVerifier |
| メッシュ | 地域メッシュ表示（**1 km / 500 m / 250 m** = 第 3 次・第 4 次・第 5 次メッシュ、Phase 1 REQ-RST-016 3 階層） | MapVerifier 由来 / 拡張 |
| メッシュ | メッシュをクリックして移動不可 / 移動困難属性を付与 | 新規 |
| ポリゴン | マップ上でポリゴン描画 | 新規 |
| ポリゴン | ポリゴンに移動不可 / 移動困難属性を付与（難所タイプは組込み 8 種から選択） | 新規 |
| 制約管理 | 登録済み制約一覧・個別削除・一括クリア | MapVerifier |

**ユースケースシナリオ（OSS 公開時の README 動画想定）**：

1. ユーザーが Sandbox を起動 → 「日本全国 / 愛知県 / 東京都 …」プルダウンから都道府県を選択 → PBF ダウンロード（進捗表示）
2. マップ上で愛知県津島市付近を矩形選択 → 「Extract」クリック → `.odrg` 生成（数秒）
3. 出発地・目的地を 2 点指定 → 「Route」クリック → 経路が青線で表示、距離 / 所要時間表示
4. マップ上に 250 m メッシュをグリッド表示 → 浸水想定区域に該当するメッシュをクリック → 「Block」属性付与
5. 「Re-Route」クリック → 経路が浸水メッシュを回避して再計算（**動的制約のキラー機能を 1 クリックで実演**）
6. ポリゴン描画ツールで難所エリア（例：地震時通行困難）を描画 → 難所タイプ「surface_damage」付与 → Re-Route で速度低下を反映した経路

**サブステップ 3I.1〜3I.5**：

- **3I.1** プロジェクト雛形（`OsmDotRoute.Sandbox.Server` + `OsmDotRoute.Sandbox.Web`、MapVerifier 構成踏襲、起動 → ブランクマップ表示）
- **3I.2** PBF ダウンロード + bbox 範囲指定 UI（Geofabrik 都道府県一覧、進捗表示、ローカルキャッシュ、矩形選択 → bbox 確定）
- **3I.3** `.odrg` 抽出パイプライン統合（bbox + プロファイル → `OsmDotRoute.Extractor` 内部呼出 → `.odrg` 生成 → サーバ常駐 → マップに頂点 / エッジ表示）
- **3I.4** ルート探査 UI（2 点指定 → `MapService.CalculateRoute` 呼出 → 経路 GeoJSON 返却 → マップ描画 + メトリクス表示）
- **3I.5** メッシュ / ポリゴン制約付与（メッシュグリッド表示 1 km/500 m/250 m 切替、メッシュクリック → Block/Difficulty 付与、ポリゴン描画ツール、難所タイプセレクタ、Re-Route 連動、制約一覧パネル）

**設計上の歯止め**：

- **Sandbox 自体は外部 NuGet 依存を最小化**（System.\* + Leaflet OSS のみ、Bootstrap 等の重い UI フレームワークは避ける）
- **ASP.NET Core サーバはローカル限定運用**（CORS 全開放、認証なし、外部公開ボタン無し）。README に「localhost only」を明記
- **本番運用想定はしない**（試用・デモ専用、設計書 §10.x で運用方針を明記）
- **PBF ダウンロード元は Geofabrik 固定**（§5.5-30 ユーザー判断、ODbL 表記必須）

### 3.9 OSS 公開準備（REQ-PKG-003 / REQ-LIC-004）

- GitHub 個人アカウント上で公開可能な状態を整備：
  - `README.md`：プロジェクト概要 / インストール手順 / クイックスタート / アーキテクチャ図 / Phase 1〜3 経緯 / ライセンス / **§3.9.1 Itinero 比較ドキュメントへのリンク**
  - `LICENSE`：MIT 想定（要件定義書 §6 REQ-LIC-001 確定済方針があれば踏襲、未確定なら本ステップで確定）
  - `LICENSE-THIRD-PARTY.md`：System.\* 標準ライブラリのみ依存のため、OSM データ（ODbL）の利用案内のみ記載（REQ-LIC-004）
  - `.github/workflows/ci.yml`：dotnet test + dotnet pack の最低限 CI（Windows / Linux 両方）
  - `CONTRIBUTING.md`：ビルド方法・テスト方法
  - **`Documents/comparison_with_itinero.md`（新規）**：Itinero との設計思想・データ構造・性能特性の違いと用途別の向き不向きを詳細に解説（§3.9.1 構成案、README からリンク必須）
- 公開判断は本ステップ完了 → ユーザー判断（REQ-PKG-002 の解除を Phase 3 確定で実施）
- **エッジフラグ 14 bit の運用上不要な bit の剪定判断**もここで実施（Phase 2 §5.5-4「できるだけ多く採用、運用上不要と判断した時点で削る」方針、Phase 3 ステップ 3A〜3F で使われなかった bit を v0.3 リリースノートに記載のうえ予約化）

#### 3.9.1 Itinero 比較ドキュメントの構成案（`Documents/comparison_with_itinero.md`）

OsmDotRoute は Itinero の単純代替ではなく**動的制約に特化したルーティングライブラリ**である。利用者が「自分のユースケースに OsmDotRoute / Itinero どちらが向くか」を判断できる、フェアな比較ドキュメントを Phase 3 完了時点で執筆する。

**想定読者**: OSS 公開後に OsmDotRoute を評価する .NET / OSM ルーティングユーザー（Itinero 既存ユーザー / 新規ルーティング導入検討者）。

**章立て（草案）**:

1. **要約（TL;DR）** — 「速度重視 / 静的経路 / 世界規模 → Itinero、動的制約 / シミュレーション / 日本ローカル → OsmDotRoute」を 3 行で
2. **設計思想の違い**
   - Itinero: **汎用 OSM ルーティング**（Profile を Lua / Native 拡張で広く対応、CH 含む高速化アルゴリズム、世界中の OSM 用途に対応）
   - OsmDotRoute: **動的制約特化**（シミュレーション実行中に進入不可・難所エリアを add/remove して**次回経路計算から即時反映**、再ビルド不要）
3. **データベース構造の違い**

   | 項目 | Itinero RouterDb (`.routerdb`) | OsmDotRoute (`.odrg`) |
   | --- | --- | --- |
   | エッジモデル | `edge_profile` で OSM タグを集約（共通 profile ID をエッジが参照） | エッジ毎にプロファイル評価結果を bake（`bakedProfileIndex == edgeId` 規約） |
   | 空間インデックス | エッジ単位なし（頂点ベース） | **エッジ STR R-tree（M=16）** を bake、動的制約交差判定が O(log E) |
   | エッジ AABB | （bake なし、シェイプから都度計算） | **double × 4 を bake**、配列インデックスで O(1) 取得 |
   | エッジフラグ | profile / restriction 経由 | **14 bit bitflag**（橋・トンネル・高架・有料・私道・一方通行等を bake、難所評価で 1 分岐判定） |
   | エッジシェイプ | `IReadOnlyList<Coordinate>` 風（コピー発生） | **連続バッファ + `ReadOnlySpan<GeoCoordinate>`**（ゼロアロケーション、Phase 1 比 77 MB → 5 MB 目標） |
   | アクセス方式 | ファイル全読み | **`MemoryMappedFile` + `ReadOnlySpan<T>`**（ゼロコピー、起動時メモリ常駐最小化） |
   | 入力 | OSM PBF / ITN / 中間 RouterDb | **OSM PBF 直接抽出のみ**（`.odrg` ファイル経由でランタイムへ） |
   | ターン制限 | 対応 | **未対応**（Phase 4+、Phase 2 で形式予約のみ） |

4. **アルゴリズムの違い**
   - Itinero: Dijkstra / A\* / Contraction Hierarchies（CH）/ Many-to-many / Isochrone / マトリクス計算 / 双方向探索
   - OsmDotRoute: **Dijkstra のみ**（Phase 3 時点）。CH / A\* / 双方向は要件定義書 REQ-RTE-009 で「性能要件未達時の対策」として Phase 4+ 延期
   - **「動的制約 add/remove → CH 再ビルド」の組み合わせは Itinero でも困難**：CH の事前計算コストが動的制約変更ごとに発生するため、ホットリロード型用途では CH が逆効果。OsmDotRoute は CH なしで動的制約 100 件下の劣化率 1.1× 以下を目指す設計（§9 性能基準値表参照）
5. **性能特性の違い（津島市実測ベース、Phase 3 §9 / `phase3_benchmark_results.md` の数値を埋め込む）**
   - 経路計算（C0）: Itinero ≒ X ms、OsmDotRoute = 33 ms（Phase 1 実測 0.48×）
   - 制約 100 件下劣化率: Itinero **比較不可**（Itinero は動的制約を持たない、`RouterDb` 再ビルド前提）、OsmDotRoute = 1.43× （Phase 1）→ 1.1× 以下（Phase 3 目標）
   - 経路 1 本あたりアロケート: Itinero ≒ X MB、OsmDotRoute = 77 MB（Phase 1）→ 5 MB 以下（Phase 3 目標）
   - StdDev: OsmDotRoute は Itinero の 1/7（Phase 1 実測、ジッタ少なさが特徴）
   - **「Itinero は動的制約用途を想定していない」**ことを明示し、不公平比較を避ける（CH を有効化した Itinero との 1:1 性能比較は Itinero に有利、CH を切った Itinero との比較は OsmDotRoute に有利、どちらも単独では誤解を招くため両ケース併記）
6. **対応プロファイル**
   - Itinero: Car / Pedestrian / Bicycle / 多数（Lua プラグイン）、海外仕様ベース
   - OsmDotRoute: Car / Pedestrian / **Bicycle / Truck（10 t、日本道路法ベース、独自設計）**、`*.profile.json` 外部化、難所プロファイル（メモリ [[project-profile-difficulty-design]]）
7. **依存とランタイム要件**
   - Itinero: Itinero NuGet 群（複数アセンブリ）、.NET Standard 系、追加で Reminiscence・GeoAPI 等の依存
   - OsmDotRoute: **System.\* 標準ライブラリのみ**（net9.0、外部 NuGet ゼロ、REQ-DEP-003）。配布物が小さい / セキュリティ更新が .NET 本体追従のみで完結
8. **ライセンス**
   - Itinero: MIT
   - OsmDotRoute: MIT 想定（§5.5-27 で確定、Itinero と組合せ利用も問題なし）
9. **「Itinero / OsmDotRoute どちらを選ぶか」用途別ガイド**
   - **Itinero に向く用途**:
     - 静的な OSM データに対する高速経路計算（CH 必須レベルの大規模・高頻度クエリ）
     - 世界各地の OSM データを統一プロファイルで扱う（multilingual / global）
     - Isochrone / マトリクス / 多対多経路計算が必要
     - 安定稼働実績重視（Itinero は数年運用実績）
     - Lua / Native でプロファイルを自作する開発体制
   - **OsmDotRoute に向く用途**:
     - **災害シミュレーション**（浸水・地震・道路閉鎖を動的に追加 / 削除、再計算なしで反映）
     - **自動運転 / 物流シミュレーション**（時間帯規制・事故閉鎖・工事区間を動的反映）
     - **日本国内 Truck 配送計画**（10 t 規模、日本道路法ベース、`hgv=*` / `maxweight=*` 評価）
     - **シミュレーション実行中の制約変化が頻繁**（add/remove スループット重視）
     - **System.\* 完結が必要な環境**（外部 NuGet 制限、セキュリティ監査負担軽減）
     - **メモリ常駐サイズを抑えたい**（MMF ゼロコピー読込、複数 `.odrg` の使い分け）
   - **どちらでもよい用途 / 慎重に評価すべき用途**:
     - 動的制約なしの単純な 2 点間経路 → どちらも可、Itinero が実績・機能網羅で優位、OsmDotRoute が System.\* 完結で優位
     - 都道府県単位以上の超大規模 → Phase 3 ステップ 3G で OsmDotRoute 実測値が出るまで判断保留
     - CH / 双方向 Dijkstra が必須 → 現状 Itinero（OsmDotRoute は Phase 4+ で検討）
     - ターン制限が必須 → 現状 Itinero（OsmDotRoute は Phase 4+ で検討）
10. **OsmDotRoute が向かない用途（正直に書く）**
    - **多対多マトリクス計算が中心** → Itinero / OSRM の方が機能が揃っている
    - **海外の OSM データを扱う** → OsmDotRoute の Truck / Bicycle プロファイルは日本道路法ベースのため、海外で適合しない可能性。Car / Pedestrian は汎用設計だが海外実績未確認
    - **CH 等の高速化前提の大規模クエリ** → Phase 3 では Dijkstra のみ
    - **稼働実績必須（プロダクション複数年実績）** → OsmDotRoute は新興、Itinero は数年実績
11. **将来計画（Phase 4+）**
    - CH 対応、双方向 Dijkstra、ターン制限、NuGet 公開、マルチプラットフォーム検証等
    - 「現時点で OsmDotRoute が劣る項目は将来解消候補」と明示し、Phase 4+ ロードマップへリンク
12. **謝辞**
    - Phase 1 ではグラフ構造を Itinero `RouterDb.Network` から借用し動作確認を行った経緯への謝意（コピー禁止条項を守った参照のみ、要件定義書 CLAUDE.md ルール準拠）

**執筆方針**:

- **Itinero を否定しない、フェアな比較**を貫く。OsmDotRoute は「Itinero の置き換え」ではなく「動的制約ユースケースに特化した別物」というポジショニング
- 性能数値は Phase 3 ステップ 3E（津島市 C0〜C4）/ 3G（都道府県単位）の実測値を必ず引用、推測値は使わない
- Itinero 側の数値は Phase 1 ベンチ（[`phase1_benchmark_results.md`](phase1_benchmark_results.md)）で実測済の Itinero 1.5.1 数値を流用、Phase 3 で再測定しない（時間節約）
- 「劣る点を隠さない」（CH 未対応、ターン制限未対応、稼働実績、世界規模適合性は正直に列挙）
- README.md からは「OsmDotRoute と Itinero の違い、向き不向きの詳細はこちら」のキャッチで 1 行リンク

### 3.9 エッジフラグ運用観察と剪定方針

Phase 2 で確定した 14 bit のうち、Phase 3 で実際に利用するのは：

| bit | 名前 | Phase 3 利用箇所 |
| --- | --- | --- |
| 0 | IsBridge | 難所評価（冠水時に通行可、メモリ [[project-profile-difficulty-design]]） |
| 1 | IsTunnel | 難所評価（冠水時に通行可） |
| 2 | IsElevated | 難所評価（地震時の通行制限想定） |
| 3 | IsRoundabout | 経路コスト（Phase 3 で利用判断、現状は形式予約） |
| 4 | IsToll | プロファイル評価（pedestrian / bicycle で通行不可化） |
| 5 | IsPrivateAccess | プロファイル評価（`access=private` 系の除外） |
| 6 | IsServiceWay | プロファイル評価（`highway=service` の速度低下） |
| 7 | IsTrack | Truck プロファイル評価（`highway=track` 通行不可） |
| 8 | IsLivingStreet | Truck プロファイル評価（生活道路を回避） |
| 9 | IsPedestrianSeparated | Bicycle / Truck プロファイル評価 |
| 10 | IsWinterClosed | Phase 3 では利用しない（Phase 4+ 季節制約用、予約） |
| 11 | IsSchoolZone | Phase 3 では予約 0 固定（取得実装は運用判断後） |
| 12 | OnewayForward | 全プロファイル必須 |
| 13 | OnewayBackward | 全プロファイル必須 |

ステップ 3H 完了時に「実際にプロファイル / 制約評価で参照された bit」と「予約のまま終わった bit」を分離し、`.odrg` v0.3 で予約化（再利用候補）。**剪定は v0.3 マイナーリリースで実施、Phase 3 v0.3.0 では削らない**（破壊変更を最小化）。

---

## 4. Phase 3 スコープ確認（要件対応表）

| ID | 概要 | 優先度 | 関連ステップ |
| --- | --- | --- | --- |
| REQ-MAP-005 | `.odrg` ランタイム読込（`NativeRoadGraph` / MMF + Span） | P1 | 3A |
| REQ-MAP-006 | ランタイム Itinero 依存排除 | P1 | 3C |
| REQ-MAP-009 | ライブラリ全体から Itinero 依存排除 | P1 | 3C |
| REQ-DEP-003 | Phase 3 ランタイム System.\* 完結 | P1 | 3C |
| REQ-PRF-003 | Bicycle プロファイル独自設計 | P2 | 3D |
| REQ-PRF-004 | Truck (10 t) プロファイル独自設計 | P2 | 3D |
| REQ-NFR-001 | 経路計算性能維持（Phase 1 = 33 ms、Itinero 比 0.48x） | P1 | 3E |
| REQ-NFR-002 | 制約 100 件下の劣化率 ≦ 1.5x | P1 | 3E |
| REQ-NFR-003 | 経路 1 本あたりアロケート削減（Phase 1 = 77 MB） | P2 | 3E |
| REQ-PKG-002 | Phase 3 完了まで非公開リポジトリ維持（運用） | P2 | 全体 |
| REQ-PKG-003 | OSS 公開準備（README / LICENSE / CI 整備） | P1 | 3H |
| REQ-LIC-004 | OSM データ（ODbL）の利用ガイドライン整備 | P2 | 3H |

**スコープ外（Phase 4+ へ移動）**：

- REQ-PRF-005 / REQ-PRF-006: Emergency / Disaster プロファイル（要望が出た時点で着手判断、P3）
- REQ-RTE-009: 双方向 Dijkstra 等の高速化アルゴリズム導入（性能要件未達時の対策、P3）
- ターン制限（PBF Relation `type=restriction`）対応（Phase 2 §8.2.1 で延期確定）
- メッシュ 100 m 階層対応（要件定義書 v1.4 で延期）
- CH（Contraction Hierarchies）対応
- マルチプラットフォーム配布（macOS / Linux 詳細検証）

---

## 5. プロジェクト構成変更

### 5.1 新規プロジェクト / 撤去プロジェクト

| プロジェクト | 配置 | 操作 | 備考 |
| --- | --- | --- | --- |
| `OsmDotRoute`（コア） | `src/OsmDotRoute/` | **拡張** | `NativeRoadGraph` / `NativeRoadSnapper` / `RestrictedAreaEdgeCache` 追加、Itinero 依存削除、`Route.Shape` 型変更 |
| `OsmDotRoute.Itinero` | `src/OsmDotRoute.Itinero/` | **撤去** | プロジェクト削除、`OsmDotRoute.sln` から外す。git 履歴には残す |
| `OsmDotRoute.Extensions.DependencyInjection` | `src/OsmDotRoute.Extensions.DependencyInjection/` | **更新** | DI 登録を `NativeRoadGraph` 用に書換 |
| `OsmDotRoute.Pbf` | `src/OsmDotRoute.Pbf/` | **変更なし** | Phase 2 で完成 |
| `OsmDotRoute.Extractor` | `src/OsmDotRoute.Extractor/` | **更新** | `--profiles` に `bicycle` / `truck` 追加、bake プロファイル表 4 列出力 |
| `tests/OsmDotRoute.Tests` | `tests/OsmDotRoute.Tests/` | **拡張** | Native 系 / Bicycle / Truck / 親プロ統合パリティテスト追加 |
| `tests/OsmDotRoute.Benchmarks` | `tests/OsmDotRoute.Benchmarks/` | **拡張** | C0〜C4 シナリオ追加、Phase 1 ベンチを `--filter` で切替可能化 |
| `samples/MapVerifier` | `samples/MapVerifier/` | **更新** | RouterDb 比較表示モードを撤去 or `.odrg` only に切替（§5.6 で判断）。**検証用ツールとして残置、ユーザー試用は Sandbox へ役割分担** |
| `samples/Sandbox/Server` | `samples/Sandbox/Server/` | **新規** | ユーザー試用デモ：ASP.NET Core minimal API、PBF DL / bbox 抽出 / 経路探査 / 制約付与 を REST 公開（ローカル限定運用） |
| `samples/Sandbox/Web` | `samples/Sandbox/Web/` | **新規** | ユーザー試用デモ Web UI：静的 HTML + Leaflet、メッシュ表示 / ポリゴン描画 / 制約付与パネル |

### 5.2 Phase 3 完了時のアセンブリ参照グラフ

```text
OsmDotRoute                      (NativeRoadGraph / NativeRoadSnapper、System.* のみ依存)
  ↑
OsmDotRoute.Extensions.DependencyInjection
  ↑
OsmDotRoute.Pbf                  (Phase 2 から変更なし)
  ↑
OsmDotRoute.Extractor            (Phase 2 から軽微更新)

[撤去]
OsmDotRoute.Itinero              ← 削除
```

ランタイム経路：`OsmDotRoute` → System.\* のみ。Itinero 1.5.1 への NuGet 参照ゼロ。

### 5.3 Phase 1 / Phase 2 既存プロジェクトへの影響

- `OsmDotRoute`：`MapService` の `LoadFromRouterDb` / `LoadFromOsmPbf` を **削除**、`LoadFromOdrg(path)` に統一。`Route.Shape` を `IReadOnlyList<GeoCoordinate>` → `ReadOnlyMemory<GeoCoordinate>` に変更（**破壊変更、§5.5-8 確定**）
- `OsmDotRoute.Extensions.DependencyInjection`：`AddOsmDotRoute(options)` 内部実装を `NativeRoadGraph` 切替
- `MapVerifier`：RouterDb / `.odrg` 二重表示を**廃止**し `.odrg` only に統一する案を有力候補とする（Itinero 撤去の象徴的事例として）。最終判断は §5.6 で
- `samples/Data/tsushima.routerdb`：撤去判断（Phase 3 で利用しないため。git 履歴には残す）

### 5.4 Phase 2 から継承する確定事項

| # | 項目 | 決定 | 出典 |
| --- | --- | --- | --- |
| 1 | ファイル拡張子 | `.odrg` | Phase 2 計画書 §5.5-1 |
| 2 | エッジ空間インデックス方式 | R-tree（STR パック静的版、M=16） | Phase 2 計画書 §5.5-2 |
| 3 | エッジ AABB の精度 | double × 4 | Phase 2 計画書 §5.5-3 |
| 4 | エッジフラグの組込み範囲 | 14 bit（運用上不要は v0.3 で剪定） | Phase 2 計画書 §5.5-4 |
| 5 | ファイルアクセス方式 | `MemoryMappedFile` + `ReadOnlySpan<T>` | Phase 2 計画書 §5.5-6 |
| 6 | `Route.Shape` API | `IReadOnlyList` → `ReadOnlyMemory<GeoCoordinate>` 破壊変更 | Phase 2 計画書 §5.5-8 |
| 7 | Phase 3 ベンチ対象都市 | 愛知県津島市（Phase 1 と同一） | Phase 2 計画書 §5.5-9 |
| 8 | Bicycle / Truck プロファイル | 独自設計、Truck = 10 t | Phase 2 計画書 §5.5-10 |
| 9 | エッジフラグ Phase 3 剪定方針 | 不要 bit は v0.3 マイナーで予約化、v0.3.0 では削らない | Phase 2 §8.3 |

### 5.5 Phase 3 で確定が必要なユーザー判断事項

| # | 項目 | 候補 | 確定タイミング |
| --- | --- | --- | --- |
| 21 | `NativeRoadGraph` の MMF 解放方針 | (a) `IDisposable` 厳格、(b) ファイナライザ併用 | ステップ 3A 着手時 |
| 22 | `RestrictedAreaEdgeCache` の格納粒度 | (a) 制約 ID 単位、(b) 制約タグ単位（バルク削除最適化、REQ-RST-010） | ステップ 3B 着手時 |
| 23 | Bicycle プロファイル平均速度 | (a) 15 km/h（一般、要件定義書 5.3 §16 で未確定）、(b) 20 km/h（クロスバイク想定） | ステップ 3D 着手時 |
| 24 | Truck プロファイルの幅員制限 | (a) `maxwidth=*` のみ評価、(b) `maxwidth=*` + `highway=residential` 回避 | ステップ 3D 着手時 |
| 25 | Itinero 撤去後の `MapVerifier` モード | (a) `.odrg` only に切替、(b) RouterDb 比較表示を別 Exe に分離 | ステップ 3C 着手時 |
| 26 | `MapService.LoadFromOsmPbf` の扱い | (a) 完全削除、(b) `OsmDotRoute.Extractor` を内部呼出する後方互換 API を残す | ステップ 3C 着手時 |
| 27 | OSS 公開時のライセンス | (a) MIT、(b) Apache 2.0、(c) BSD-3-Clause | ステップ 3H 着手時 |
| 28 | CI プラットフォーム | (a) Windows のみ、(b) Windows + Linux、(c) Windows + Linux + macOS | ステップ 3H 着手時 |
| 29 | NuGet 公開のタイミング | (a) v0.3.0 と同時、(b) Phase 4 以降に判断 | ステップ 3H 完了時 |
| 30 | Sandbox の PBF ダウンロード元 | (a) Geofabrik 固定（推奨、安定実績）、(b) Geofabrik + OSM 公式 mirror 切替可、(c) 任意 URL 入力可 | ステップ 3I 着手時 |
| 31 | Sandbox の `OsmDotRoute.Extractor` 統合方式 | (a) NuGet / プロジェクト参照で同プロセス内呼出、(b) `osmdotroute-extractor.exe` を子プロセス起動 | ステップ 3I.3 着手時 |
| 32 | Sandbox の PBF / `.odrg` キャッシュ場所 | (a) `%LOCALAPPDATA%/OsmDotRoute.Sandbox/cache`、(b) 起動時カレント直下 `./cache`、(c) 設定ファイルで指定可 | ステップ 3I.2 着手時 |
| 33 | Sandbox の制約永続化 | (a) セッション中のみ（リロードで消失）、(b) JSON エクスポート / インポート、(c) 親プロのシナリオ JSON 形式と互換 | ステップ 3I.5 着手時 |
| 34 | Sandbox のメッシュ表示パフォーマンス対策 | (a) ズームレベル閾値で自動非表示、(b) ユーザー手動 ON/OFF、(c) 両方 | ステップ 3I.5 着手時 |

### 5.6 Phase 3 着手前に判断保留する項目

- **MapVerifier の RouterDb 比較モード**：Itinero 撤去後の MapVerifier 動作は §5.5-25 で確定。Phase 2 で実装した `samples/Data/tsushima.odrg` 同梱 + `.odrg` レイヤー表示は維持
- **`OsmDotRoute.Itinero` プロジェクト削除のタイミング**：ステップ 3A〜3D で `NativeRoadGraph` / `NativeRoadSnapper` が動いてから削除（事前削除すると回帰検出ができない）
- **エッジフラグ剪定**：v0.3.0 では削らない方針確定（§5.4-9）。`.odrg` v0.3 で予約化判断
- **Sandbox の親プロ「Map&シナリオツール」仕様参照**：ステップ 3I 着手前に親プロ実装を一読し、UX 設計の参考とする（コード移植はしない、要件定義書 §3 「親プロのコードを移動・コピーしない」遵守）

---

## 6. 実装ステップ一覧

| # | ステップ | 主要要件 | 状態 |
| --- | --- | --- | --- |
| 3A | ランタイム `.odrg` 読込実装（`NativeRoadGraph` / `NativeRoadSnapper` / MMF ビュー / セクションテーブル → `ReadOnlySpan<T>` 公開 / R-tree クエリ実装。`OsmDotRoute` コアに追加、`IRoadGraph` / `IRoadSnapper` 実装。Itinero 既存実装と**並存**でテスト pass） | REQ-MAP-005 | 未着手 |
| 3B | 動的制約ホットパス高速化（`RestrictedAreaEdgeCache` 実装、`RestrictedAreaService.AddBlockArea` / `AddDifficultyArea` で R-tree クエリ → エッジ ID 集合確定 → キャッシュ格納、Dijkstra 辺展開を HashSet ルックアップ化、Phase 1 §18.3 解消を実測） | （Phase 1 §18.3） | 未着手 |
| 3D | Bicycle / Truck プロファイル独自設計（`bicycle.profile.json` / `truck.profile.json` 新規、`osmdotroute-extractor` `--profiles` 拡張、津島市で 4 プロファイル経路返却確認） | REQ-PRF-003, REQ-PRF-004 | 未着手 |
| 3C | ランタイム Itinero 依存削除（`OsmDotRoute.Itinero` 撤去、`MapService.LoadFromOdrg` 統一、`Route.Shape` 破壊変更、`MapVerifier` モード切替、DI 登録書換、ライブラリ全体 Itinero NuGet ゼロ確認） | REQ-MAP-006, REQ-MAP-009, REQ-DEP-003 | 未着手 |
| 3E | ベンチマーク再実施（津島市 C0〜C4、Phase 1 基準値との比較表、`MemoryDiagnoser` で 77 MB 削減確認、結果を [`phase3_benchmark_results.md`](phase3_benchmark_results.md) に記録） | REQ-NFR-001〜003 | 未着手 |
| 3F | 親プロジェクト統合・パリティ検証（旧 Phase 1 ステップ 16、`OsmDotRoute` v0.3.x へ差替、`Route.Shape` 破壊変更対応、89 ペア経路結果 ±10% 以内、KSJ GML 制約動作確認） | — | 未着手 |
| 3G | 都道府県単位ベンチ（Phase 1 §18.2 リベンジ、愛知県全域 PBF から `.odrg` 抽出、経路計算スループット実測、`.odrg` サイズ実測） | — | 未着手 |
| 3I | **ユーザー試用デモツール `OsmDotRoute.Sandbox` 新設**（5 サブステップ 3I.1〜3I.5：プロジェクト雛形 → PBF DL + bbox → `.odrg` 抽出統合 → ルート探査 UI → メッシュ / ポリゴン制約付与。`samples/Sandbox/Server` + `samples/Sandbox/Web` 新設、MapVerifier 構成踏襲、ローカル限定運用） | （§3.8 新規） | 未着手 |
| 3H | ユーザー検証・Phase 3 確定（OSS 公開準備：README / LICENSE / CI 整備、ODbL ガイドライン、**`Documents/comparison_with_itinero.md` 執筆 + README リンク（§3.9.1 構成案）**、**Sandbox 起動方法を README にクイックスタートとして掲載**、エッジフラグ運用観察結果記録、Phase 3 v0.3.0 タグ判断、REQ-PKG-002 解除判断） | REQ-PKG-003, REQ-LIC-004 | 未着手 |

各ステップ完了時に **ユーザー報告 → 承認 → 次ステップ着手** のサイクルを厳守（CLAUDE.md ルール、Phase 1 / Phase 2 と同様）。

**ステップ順序の根拠**：

- 3A → 3B：3B が 3A の R-tree クエリ実装に依存
- 3B → 3D：3D のプロファイル評価で 3B のキャッシュ機構が活用される（Truck で制約多数のシナリオを想定）。ただし依存関係は弱いため 3A 完了直後でも着手可
- 3D → 3C：**Itinero 撤去前に新プロファイル動作確認**が完了している方が、撤去ステップで回帰原因の切り分けが容易（テスト失敗が Itinero 撤去由来か新プロファイル由来かを区別する）
- 3C → 3E：ベンチは Itinero 撤去後のクリーン状態で実施（Phase 1 ベンチとの公平比較）
- 3E → 3F：親プロ統合前にライブラリ性能を確定（親プロ統合で性能未達が発覚した場合に対応難）
- 3F → 3G：親プロ統合で実用性確認後、都道府県単位ベンチで負荷上限確認
- 3G → 3I：Sandbox は 3A〜3G の成果物（NativeRoadGraph / 新プロファイル / Itinero 撤去後 API / Extractor）を一通り利用するため、コア機能とベンチが揃った後に着手
- 3I → 3H：OSS 公開準備（3H）の README で Sandbox 起動方法 / スクリーンショット / GIF を載せるため、Sandbox 完成後に着手

---

## 7. 想定工数感（粗見積もり）

| ステップ | 想定工数 | 主リスク |
| --- | --- | --- |
| 3A. NativeRoadGraph / NativeRoadSnapper | 4〜6 日 | MMF + `unsafe` Span ポインタの初実装、R-tree クエリ実装の正確性、Itinero 既存実装と並存テスト |
| 3B. 動的制約ホットパス | 2〜3 日 | `RestrictedAreaEdgeCache` の TaG ベース無効化の実装漏れ、HashSet 多重排他のスレッドセーフ性 |
| 3D. Bicycle / Truck プロファイル | 3〜4 日 | Truck 日本道路法ベース仕様策定、OSM タグ `hgv=*` / `maxweight=*` 等の実データ網羅性確認 |
| 3C. Itinero 依存撤去 | 2〜3 日 | `MapVerifier` モード切替判断、`Route.Shape` 破壊変更の伝播範囲、`samples/Data/tsushima.routerdb` 廃止判断 |
| 3E. ベンチマーク再実施 | 2〜3 日 | `MemoryDiagnoser` ノイズ、Phase 1 ベンチと同条件再現、Phase 1 §18.4 数値（77 MB）の再現性 |
| 3F. 親プロジェクト統合・パリティ検証 | 3〜5 日 | 親プロ側修正範囲（`Route.Shape` 破壊変更の波及）、89 ペア結果の Phase 1 一致確認、KSJ GML 制約の動作確認 |
| 3G. 都道府県単位ベンチ | 1〜2 日 | 愛知県 PBF 取得・抽出時間、メモリ使用量上限到達リスク |
| 3I. Sandbox 新設 | 6〜8 日 | 5 サブステップ 3I.1（雛形 1 日）/ 3I.2（PBF DL + bbox 1〜2 日）/ 3I.3（Extractor 統合 1 日）/ 3I.4（ルート探査 UI 1 日）/ 3I.5（メッシュ + ポリゴン制約 2〜3 日）。Leaflet ポリゴン描画ライブラリ選定 / Geofabrik レート制限 / PBF サイズ（数十 MB〜数 GB）対応 |
| 3H. OSS 公開準備 | 3〜5 日 | ライセンス選定（§5.5-27）、CI プラットフォーム範囲（§5.5-28）、README 執筆量、**Itinero 比較ドキュメント執筆（§3.9.1、Phase 3 ベンチ実測値を引用するため 3E 完了が前提）**、Sandbox GIF / スクリーンショット作成、NuGet 公開判断 |
| **合計** | **26〜39 日** | （Phase 3 全体、Phase 2 の 11〜20 日より大幅増。Sandbox 追加で +6〜+8 日、Itinero 比較ドキュメント追加で 3H +1〜+2 日。Phase 1 の 20〜23 日比 1.3〜1.7 倍） |

ステップ間の依存は §6 末尾参照。Phase 2 で「土台が整地済」のため、3A の MMF 実装が最大のリスク要因。

---

## 8. リスクと対処方針

| # | リスク | 影響 | 対処方針 |
| --- | --- | --- | --- |
| R1 | `NativeRoadGraph` の MMF + `unsafe` Span 実装で SEGV / 範囲外アクセス | ランタイムクラッシュ、原因切り分けが難しい | 3A で `OdrgReader`（Phase 2 検証用 eager-parse）との突合テストを必須化。`MemoryMappedViewAccessor` の `SafeBuffer` API を優先利用、`unsafe` ポインタは hotspot のみ。`#nullable enable` + `Span<T>.Length` チェック厳守 |
| R2 | `RestrictedAreaEdgeCache` のキャッシュ不整合（制約 add/remove 時の漏れ） | 経路結果が誤る、Phase 1 動作と乖離 | 3B で「キャッシュ rebuild モード」を internal で実装し、Phase 1 動作との突合テスト（89 ペア × 制約 100 件 × 4 プロファイル = 35,600 ケース）を CI に組込 |
| R3 | Truck プロファイル仕様の日本道路法解釈ミス | 親プロ統合（3F）で実用性問題 | 3D 着手時に親プロチームと仕様すり合わせ。最大積載量 10 t は確定（要件定義書 REQ-PRF-004）だが、車両総重量 20 t 級・高さ/幅制限は要件定義書 §5.3 §16 で未確定のため、3D 着手時に確認 |
| R4 | `Route.Shape` 破壊変更（`IReadOnlyList` → `ReadOnlyMemory`）が親プロ修正範囲を過小評価 | 3F で修正コスト超過 | 3F 着手前に親プロのコード調査（`Route.Shape` 利用箇所をリスト化、修正コスト見積もり）。重ければ 3F で2段階修正（互換アダプタを一時的に追加 → 親プロ修正完了後に削除） |
| R5 | Phase 1 §18.4 = 77 MB アロケート削減が想定未達（5 MB 目標未達） | REQ-NFR-003 未達 | 3E で MemoryDiagnoser 詳細解析、ホット box 化（経路ノード列・ヒープ）を 1 つずつ Span 化。目標 5 MB に届かなければ「Phase 1 の 1/3 = 25 MB」を最低ラインとする |
| R6 | 都道府県単位ベンチ（3G）で性能基準値超過 | REQ-NFR-001 未達 | 3G 着手前に Phase 2 仕様書 §8 「都道府県は数百万エッジ想定」の R-tree 容量試算を再確認。超過時は CH（Contraction Hierarchies）検討（Phase 4+ ステップ） |
| R7 | OSS 公開時のライセンス選定で時間消費 | 3H 遅延 | §5.5-27 で MIT を推奨デフォルト（要件定義書 §6 REQ-LIC-001 未確定なら本ステップで MIT 確定提案）。Apache 2.0 の場合は LICENSE-3RD-PARTY 注意事項が増える点を比較 |
| R8 | `OsmDotRoute.Itinero` 撤去で `MapVerifier` が動かなくなる | 検証手段喪失 | 3C 着手前に MapVerifier モード切替（§5.5-25）を確定。`.odrg` only モードで MapVerifier を改修済とした後に Itinero 撤去 |
| R9 | 親プロ統合（3F）で発覚する公開 API 不足（Phase 1 で隠蔽していた Itinero 直接利用箇所） | スコープ拡大、Phase 3 遅延 | 3F 着手前に親プロのコード調査時に Itinero 直接利用箇所をリスト化。不足 API は本書 §5.5 ユーザー判断事項に追加して確定 |
| R10 | Itinero 比較ドキュメント（§3.9.1）が主観的・不公平な比較となり OSS 公開後にコミュニティから批判される | プロジェクト評価毀損、信頼性低下 | 3H 着手時に「執筆方針」（§3.9.1 末尾）厳守：Itinero 数値は Phase 1 実測値のみ引用、CH 有効 / 無効両ケース併記、OsmDotRoute が劣る点（CH 未対応・ターン制限未対応・稼働実績・海外適合性）を正直に列挙、Itinero 謝辞を §12 に明記。ドラフト完成後にユーザー査読を必須化 |
| R11 | Sandbox の PBF ダウンロード機能で Geofabrik にレート制限・帯域圧迫を発生させる | OSM コミュニティでの評判悪化、Geofabrik 利用ブロック | 3I.2 で User-Agent ヘッダ（`OsmDotRoute.Sandbox/x.y.z`）明示、HTTP If-Modified-Since 利用でローカルキャッシュ優先、デフォルトダウンロード前に確認ダイアログ。README に「PBF ダウンロードは Geofabrik の利用規約に従う」明記（REQ-LIC-004） |
| R12 | Sandbox の Web UI が外部公開ポートでリッスンし、CORS 全開放 / 認証なしで踏み台化 | セキュリティリスク、利用者の被害 | 3I.1 で `Kestrel` を `127.0.0.1` バインド固定、`launchSettings.json` に `0.0.0.0` を含めない、README 冒頭で「localhost only、本番運用不可」を太字明記。設定で外部公開を有効化する機能は実装しない |
| R13 | Sandbox の Extractor 統合（§5.5-31）で同プロセス内実行を選んだ場合、長時間 PBF 抽出が UI スレッドをブロック | UX 低下、進捗表示不能 | 3I.3 着手前に §5.5-31 確定。同プロセス（NuGet 参照）なら `Task.Run` + `IProgress<T>` で別スレッド化、子プロセス起動なら `Process.OutputDataReceived` で進捗 stream 化。どちらでも WebSocket / SSE で UI に push |
| R14 | Sandbox のメッシュ表示（1 km / 500 m / 250 m）が都道府県単位の範囲で描画コスト過大 | フリーズ、UX 低下 | 3I.5 着手前に §5.5-34 確定。ズームレベル閾値（例：zoom < 12 で 1 km、zoom < 14 で 500 m、zoom < 16 で 250 m 強制非表示）+ ユーザー手動 ON/OFF の両方サポート |

---

## 9. 性能基準値（Phase 1 → Phase 3 比較）

Phase 1 ベンチマーク結果（津島市、[`phase1_benchmark_results.md`](phase1_benchmark_results.md)）を基準値とし、Phase 3 では同等以上の維持を目標とする。

| 指標 | Phase 1 値（津島市、`default.routerdb`） | Phase 3 値（津島市、`tsushima.odrg` v0.3） | Phase 3 目標 | 達成 | 検証ステップ |
| --- | --- | --- | --- | :---: | --- |
| 経路計算（C0、100 ペア平均） | 33 ms | **7.70 ms**（Native-Attached） | ≦ 33 ms | ✅ | 3E |
| Itinero 比 | 0.48x | （Itinero 撤去で再現不可、固定参照） | ≦ 0.48x（同等以下） | — | — |
| StdDev | Itinero の 1/7 | 0.14 ms（C0 Native-Attached） | 同等維持 | ✅ | 3E |
| 制約 100 件下（C1） | 51 ms（C0 比 1.43x） | **5.01 ms（C0 比 0.65x）** | ≦ 1.1× C0 | ✅ | 3E |
| 経路 1 本あたりアロケート（C3） | 77 MB（Itinero 2.4x） | **C0 = 3.12 MB / C1 = 2.35 MB / C2 = 2.39 MB** | ≦ 5 MB（Phase 1 の 1/15） | ✅ | 3E |
| Bicycle (C2、新規) | — | 5.51 ms / 2.39 MB（成功率 97/100） | 基準値確定 | ✅ | 3E |
| 制約 add/remove スループット（C4、新規） | — | **118 μs/op = 8,470 ops/sec** / 59.54 KB | 基準値確定 | ✅ | 3E |
| 経路距離同等性（親プロ統合） | 89/89 ペア ±10% 以内、Mean 0.07% | 未実施 | 同等維持 | — | 3F |
| 都道府県単位（愛知県全域、C5） | 未測定（Phase 1 §18.2 未解消） | 未実施 | 実測値を取得 | — | 3G |
| 定常 WorkingSet | 54 MB（16 GB の 0.3%） | 未測定（本ベンチでは取得せず） | ≦ 54 MB（同等以下） | — | 3E（追加実施判断） |

---

## 10. 次のアクション

1. 本実装計画書 v0.1 のユーザーレビュー
2. ユーザー承認後：
   - [`phase3_design.md`](phase3_design.md) ひな形を起こす（§0 / §0.3 のみ、§2.5 章対応表を反映）
   - メモリ `project_phase_status.md` を「Phase 3 起動、計画書 v0.1 ユーザー承認待ち」に更新
   - 計画書 + 設計書 ひな形を 1 commit にまとめる（`docs: Phase 3 計画書 v0.1 + 設計書ひな形`）
3. ステップ 3A 着手前：
   - §5.5-21 / §5.5-22 ユーザー判断確定
   - 親プロのコード調査（Itinero 直接利用箇所リスト化、R9 対処）
4. ステップ 3I 着手前：
   - §5.5-30〜34 ユーザー判断確定
   - 親プロ「Map&シナリオツール」を一読し UX 設計の参考とする（コピー不可、§5.6）
   - Leaflet 系ポリゴン描画プラグイン候補リスト化（`Leaflet.draw` 等、ライセンス確認含む）

---

## 11. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
| --- | --- | --- | --- |
| 0.1 (draft) | 2026-05-25 | 初版骨子。Phase 2 v0.2.0 タグ付与直後に起草。スコープ 3A〜3H、順序 3A→3B→3D→3C→3E→3F→3G→3H、Phase 2 設計書 §8 申し送り表を出発点。要件対応表 REQ-MAP-005/006/009 / REQ-DEP-003 / REQ-PRF-003/004 / REQ-NFR-001〜003 / REQ-PKG-003 / REQ-LIC-004。プロジェクト構成変更で `OsmDotRoute.Itinero` 撤去、`Route.Shape` 破壊変更、Itinero 1.5.1 NuGet 参照ゼロ目標を明示。性能基準値表で Phase 1 33ms / 制約 100件 51ms / 経路 77MB アロケートに対し ≦33ms / ≦1.1×C0 / ≦5MB を目標化 | Claude (Opus 4.7) |
| 0.1.1 (draft) | 2026-05-25 | **ユーザー指示反映**：§3.8 OSS 公開準備に `Documents/comparison_with_itinero.md`（Itinero との設計思想・データ構造・性能特性・用途別向き不向き解説）の執筆タスクを追加、README.md からのリンクを必須化。§3.8.1 新規サブセクションで章立て草案（12 章）と執筆方針を記述（フェア比較・実測値引用・劣る点正直列挙・Itinero 謝辞）。§6 ステップ表 3H 行を更新、§7 工数表 3H を 2〜3 日 → 3〜5 日、合計 19〜29 → 20〜31 日。§8 リスク表に R10「比較ドキュメント不公平リスク」追加（執筆方針厳守 + ユーザー査読必須）。**※ v0.1.2 で §3.8 → §3.9 にリナンバ済（§3.8.1 → §3.9.1）** | Claude (Opus 4.7) |
| 0.1.2 (draft) | 2026-05-25 | **ユーザー指示反映**：ユーザー試用デモツール `OsmDotRoute.Sandbox` を Phase 3 ステップ 3I として新設（5 サブステップ：3I.1 雛形 / 3I.2 PBF DL+bbox / 3I.3 Extractor 統合 / 3I.4 ルート探査 / 3I.5 メッシュ+ポリゴン制約）。§3.8 章を新規挿入（旧 §3.8 → §3.9 OSS 公開準備にリナンバ、§3.8.1 → §3.9.1）。MapVerifier は検証用として残置・Sandbox は独立新設の役割分担確定。§5.1 プロジェクト構成に `samples/Sandbox/Server` + `samples/Sandbox/Web` 追加。§5.5 ユーザー判断 #30〜#34 追加（Geofabrik 固定 / Extractor 統合方式 / キャッシュ場所 / 制約永続化 / メッシュ表示パフォ）。§6 ステップ表 3I 追加・順序 3G→3I→3H、§7 工数表 3I 6〜8 日追加で合計 26〜39 日。§8 リスク R11〜R14 追加（Geofabrik レート制限 / Sandbox セキュリティ / Extractor 統合 / メッシュ描画コスト）。§10 次のアクションに 3I 着手前判断項目追記 | Claude (Opus 4.7) |
