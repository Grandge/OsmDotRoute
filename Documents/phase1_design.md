# OsmDotRoute Phase 1 設計書

**バージョン**: 0.19（進行中）
**作成日**: 2026-05-18
**最終更新**: 2026-05-20
**ステータス**: 進行中（ステップ 15 ベンチマーク完了、市単位で REQ-NFR-001〜003 全件達成・Itinero 比 0.48x。要件定義書 v2.0 に反映。残作業は親プロジェクト統合 (Step 16) と都道府県単位の最終検証 (Step 17)）
**対象**: OsmDotRoute Phase 1 実装の設計記録
**関連ドキュメント**:

- [要件定義書](requirement_definition.md)（v1.1 確定）
- [Phase 1 実装計画書](phase1_implementation_plan.md)（v0.2 ドラフト）

---

## 0. 本書の目的と更新ルール

### 0.1 目的

本書は **OsmDotRoute Phase 1 で「何を、なぜ、どう実装したか」を後から把握できる記録** を残すことを目的とする。実装計画書（[`phase1_implementation_plan.md`](phase1_implementation_plan.md)）は「これから何をやるか」を、本書は「実際にどう作ったか」を保持する。

### 0.2 更新ルール

**各実装ステップが完了するたびに、本書の該当章を更新する**（実装計画書のステップ完了判定に「設計書の該当章更新」を含む）。

更新時に書くこと:

- **意図 (Intent)**: 何を実現したかったか（要件 ID 参照）
- **採用設計 (Design)**: クラス／インターフェース構成、データ構造、アルゴリズム、外部仕様（API シグネチャ・スキーマ）
- **設計判断の根拠 (Why)**: なぜ別案ではなくこの設計にしたか
- **トレードオフ・制約 (Trade-off)**: 採用しなかった案、既知の限界、Phase 2 以降への申し送り
- **検証方法 (Verification)**: 単体テストの観点、手動検証手順
- **実装メモ (Notes)**: 後で読む人が引っかかりそうな点、暗黙の前提

書かなくてよいこと:

- コードの逐語コピー（ファイル名・パス参照で十分）
- 一時的な実装過程（commit ログで追える内容）
- TODO リスト（GitHub Issues / 別文書で管理）

### 0.3 章とステップの対応

| 章 | 対応ステップ | 状態 |
|---|---|---|
| 2. アーキテクチャ概観 | 全ステップ通底 | 未記述 |
| 3. プロジェクト構成 | ステップ 1 | 記述済（2026-05-18） |
| 4. 公開型カタログ | ステップ 2 | 記述済（2026-05-18） |
| 5. Itinero アダプター | ステップ 3 | 記述済（2026-05-18） |
| 6. 道路スナップ | ステップ 4 | 記述済（2026-05-18） |
| 7a. JSON プロファイル基盤 | ステップ 5a | 記述済（2026-05-18） |
| 7b. 独自 Dijkstra エンジン | ステップ 5b | 記述済（2026-05-18） |
| 8. 道路ネットワーク GeoJSON 出力 | ステップ 6 | 記述済（2026-05-18） |
| 9. メッシュコード処理 | ステップ 7 | 記述済（2026-05-18） |
| 10. 制約管理基盤 | ステップ 8 | 記述済（2026-05-19） |
| 11. 制約対応 Dijkstra 統合 | ステップ 9 | 記述済（2026-05-19） |
| 12. GML 入力対応 | ステップ 10 | 記述済（2026-05-19） |
| 13. 経路 GeoJSON 出力（**廃止**） | ~~ステップ 11~~ | 廃止判断記録済（2026-05-19、v1.7） |
| 14. DI 拡張とドキュメント | ステップ 12 | 未記述 |
| 15. 検証用地図アプリ MapVerifier | ステップ 13-14 | 未記述 |
| 16. ベンチマーク結果 | ステップ 15 | 初版（2026-05-20） |
| 17. 親プロジェクト統合 | ステップ 16 | 未記述 |

### 0.4 章内のテンプレート

各章は以下のテンプレートで記述する:

```markdown
## NN. 章タイトル

**対応ステップ**: ステップ NN
**対応要件**: REQ-XXX-NNN, REQ-YYY-NNN
**実装日**: YYYY-MM-DD
**実装バージョン**: vX.Y.Z（ユーザー採番）
**主要ファイル**:
- `src/OsmDotRoute/...`

### NN.1 意図
（要件と達成目標）

### NN.2 採用設計
（クラス図・API シグネチャ・データ構造・アルゴリズム）

### NN.3 設計判断の根拠
（採用した理由、なぜ別案を選ばなかったか）

### NN.4 トレードオフ・制約
（既知の限界、Phase 2 以降への申し送り）

### NN.5 検証方法
（テスト観点、手動検証手順）

### NN.6 実装メモ
（暗黙の前提、後で読む人が引っかかりそうな点）
```

---

## 1. 全体概要

### 1.1 Phase 1 のゴール（再掲）

1. 親プロジェクト `災害廃棄物処理シミュレーション` の `MapService.cs` から `using Itinero;` を完全に消去できる API を提供
2. 動的制約（進入不可・移動困難エリア）を次回経路計算から反映できる Dijkstra ベース経路探索
3. 都道府県単位グラフで 1 経路 100ms 以内（REQ-NFR-001）

### 1.2 採用アプローチ（確定済み）

- **経路探索**: 独自 Dijkstra 実装（Approach A、Itinero ソース非コピー）
- **Itinero 依存閉じ込め**: アダプターアセンブリ `OsmDotRoute.Itinero` に集約、Phase 2 で破棄予定
- **グラフ抽象**: `IRoadGraph` インターフェース（内部）で実装差し替え可能

詳細は実装計画書 §3 参照。

---

## 2. アーキテクチャ概観

**ステータス**: ステップ 7 完了時点（2026-05-18）。ステップ進行に合わせて段階的に更新。

### 2.1 レイヤー構造

ステップ 7 完了時点のレイヤー構造:

```text
[利用者コード（親プロジェクト等）]
        │ uses
        ▼
┌──────────────────────────────────────────────────────────┐
│ 公開 API 層 (namespace OsmDotRoute)                       │
│   Router, RouterDb, Route, RoadNetworkGeoJson,           │
│   GeoCoordinate, GeoPolygon, VehicleProfile,             │
│   InvalidProfileException, RouterDbStatistics,           │
│   MeshCode/MeshLevel (雛形), DifficultyTypes (雛形),     │
│   RestrictedAreaService/BlockArea/DifficultyArea (雛形)  │
└──────────────────────────────────────────────────────────┘
        │ uses (internal)
        ▼
┌──────────────────────────────────────────────────────────┐
│ コア層 (internal in OsmDotRoute assembly)                 │
│   namespace OsmDotRoute.Routing                          │
│     IRoadGraph, IRoadGraphEdgeEnumerator, IRoadSnapper,  │
│     SnapResult, RoadEdge,                                │
│     EdgeWeightCalculator, DijkstraEngine, RouteBuilder,  │
│     BinaryHeap<T>                                        │
│   namespace OsmDotRoute.Geometry                         │
│     GeoBounds, Aabb, [将来: PolygonIntersection]         │
│   namespace OsmDotRoute.Mesh                             │
│     MeshCodeConverter (static, internal)                 │
│   namespace OsmDotRoute.Profiles                         │
│     JsonProfileDefinition (+sub DTO),                    │
│     ProfileEvaluator, EdgeEvaluation,                    │
│     DifficultyEvaluation, OnewayDirection,               │
│     埋込リソース: car.json / pedestrian.json             │
│   namespace OsmDotRoute.GeoJson                          │
│     GeoJsonWriter (static, internal)                     │
└──────────────────────────────────────────────────────────┘
        ▲
        │ implements (via InternalsVisibleTo)
        │
┌──────────────────────────────────────────────────────────┐
│ アダプター層 (separate assembly OsmDotRoute.Itinero)      │
│   ItineroRouterDbLoader (public static)                  │
│   ItineroRoadGraph : IRoadGraph (internal)               │
│   ItineroEdgeEnumeratorAdapter : IRoadGraphEdgeEnumerator│
│   ItineroSnapper : IRoadSnapper (internal)               │
│        │                                                  │
│        │ wraps                                            │
│        ▼                                                  │
│   Itinero 1.5.1 NuGet (RouterDb, RoutingNetwork, ...)    │
└──────────────────────────────────────────────────────────┘
```

Phase 2 では「アダプター層」が `NativeRoadGraph` / `NativeRoadSnapper`（独自フォーマット読込）に置き換わる。公開 API 層・コア層インターフェースは不変。

### 2.2 プロジェクト依存関係

```text
                 ┌─────────────────────────┐
                 │ OsmDotRoute (core)      │  ← System.* のみ
                 │   InternalsVisibleTo:   │
                 │     OsmDotRoute.Itinero │
                 │     OsmDotRoute.Tests   │
                 │     OsmDotRoute.Bench   │
                 └────────────┬────────────┘
                              │ ProjectReference (依存方向は core → 無し、他 → core)
              ┌───────────────┼────────────────┐
              │               │                │
              ▼               ▼                ▼
   OsmDotRoute.Itinero  OsmDotRoute.Tests  ConsoleDemo
   ├─ Itinero 1.5.1     ├─ xUnit           (+ OsmDotRoute.Itinero)
   └─ Itinero.IO.Osm    └─ + Itinero adapter
                              │
                              ▼
                        OsmDotRoute.Benchmarks
                        ├─ BenchmarkDotNet
                        └─ + Itinero adapter
```

**重要**: 矢印は ProjectReference の方向。`OsmDotRoute`（コア）は他のどのプロジェクトも参照しない。これにより Phase 2 で `OsmDotRoute.Itinero` プロジェクトを削除しても `OsmDotRoute` のビルドは破綻しない。

### 2.3 主要データフロー

**読込フロー**（ステップ 3〜4 で確立）:

```text
利用者: ItineroRouterDbLoader.LoadFromFile(path)
   │
   ▼
File.OpenRead → global::Itinero.RouterDb.Deserialize(stream)
   │
   ▼
new ItineroRoadGraph(itineroDb)   // IRoadGraph 実装
new ItineroSnapper(itineroDb)     // IRoadSnapper 実装（内部 Itinero.Router をキャッシュ）
   │
   ▼
new OsmDotRoute.RouterDb(graph, snapper)  // internal コンストラクタ
   │
   ▼
利用者: routerDb.GetStatistics() → RouterDbStatistics
```

**スナップフロー**（ステップ 4 で確立、ステップ 5b の Dijkstra 起点・終点解決でも使用）:

```text
利用者: router.SnapToRoad(profile, point, searchDistanceM)
   │
   ▼
RouterDb.Snapper.Snap(profile.Name, point, searchDistanceM) ─→ SnapResult? (検索半径外で null, REQ-RTE-008)
   │
   ▼
利用者: SnapResult.Location (公開), EdgeId/Offset は internal で Dijkstra へ
```

**経路計算フロー**（ステップ 5b で実装、制約評価はステップ 9 で `EdgeWeightCalculator` 内に組み込み予定）:

```text
利用者: router.Calculate(profile, from, to)
   │
   ▼
Snapper.Snap(profile.Name, from, 500m)  ─→ SnapResult? (null なら REQ-RTE-008 で null 返却)
Snapper.Snap(profile.Name, to,   500m)  ─→ SnapResult?
   │
   ▼
new EdgeWeightCalculator(graph, profile.Evaluator)
new DijkstraEngine(graph, calculator)
   │
   ▼
DijkstraEngine.Run(sourceSnap, targetSnap)  ─→ DijkstraResult?  (経路未発見時 null, REQ-RTE-006)
   │
   ├─ 同一エッジ特殊ケース: 直接通過コストで bestCost 初期化
   ├─ ソース両端点 (sourceEdge.From / .To) を初期フロンティアに push
   │     コスト = f * dist / speed (端点までのオフセット秒数)
   ├─ メインループ (バイナリヒープ pop / visited フラグでスキップ):
   │     ├─ ターゲット両端点に到達したらエッジ通過コストを加算して bestCost 更新
   │     ├─ 近傍展開: graph.GetEdgeEnumerator(u)
   │     │     ├─ calculator.Evaluate(en.EdgeProfileIndex) → (canPass, speedKmh, oneway)
   │     │     ├─ CanTraverseInEnumeratorDirection(eval, en.DataInverted) で方向チェック
   │     │     └─ newCost = uCost + dist / speed
   │     └─ uCost >= bestCost で枝刈り終了
   │
   ▼
RouteBuilder.Build(sourceSnap, targetSnap, result) ─→ Route
   ├─ Shape 先頭: sourceSnap.Location
   ├─ ソース端点頂点
   ├─ 中間エッジごと: シェイプ (DataInverted 考慮で反転) + 到達側頂点
   └─ Shape 末尾: targetSnap.Location
```

**道路ネットワーク GeoJSON 出力フロー**（ステップ 6 で実装、REQ-RTE-004）:

```text
利用者: router.GetRoadNetworkGeoJson() ─→ RoadNetworkGeoJson (string Json ラッパー)
   │
   ▼
GeoJsonWriter.WriteRoadNetwork(graph)
   │
   ├─ Utf8JsonWriter で FeatureCollection を MemoryStream へストリーミング書出
   ├─ 全頂点 v を走査し graph.GetEdgeEnumerator(v) → 各エッジを LineString 化
   ├─ HashSet<uint> で edge ID 重複排除（同一エッジを両端点から enum するため）
   └─ coordinates = [[lon, lat] (from), ...shape..., [lon, lat] (to)]
   │
   ▼
new RoadNetworkGeoJson(utf8Json.ToString())
```

### 2.4 名前空間設計

| Namespace | 配置 | 状態 | 内容 |
| --- | --- | --- | --- |
| `OsmDotRoute` | `src/OsmDotRoute/*.cs` | 実装中 | 公開 API 全般（Router, RouterDb, Route, RoadNetworkGeoJson, GeoCoordinate, GeoPolygon, VehicleProfile, InvalidProfileException, RouterDbStatistics, MeshCode/MeshLevel 雛形, Restriction 系雛形） |
| `OsmDotRoute.Routing` | `src/OsmDotRoute/Routing/` | ステップ 3〜5b で確立 | `IRoadGraph`, `IRoadGraphEdgeEnumerator`, `IRoadSnapper`, `SnapResult`, `RoadEdge`, `EdgeWeightCalculator`, `DijkstraEngine`, `RouteBuilder`, `BinaryHeap<T>` |
| `OsmDotRoute.Geometry` | `src/OsmDotRoute/Geometry/` | ステップ 3〜7 で確立 | 幾何計算（`GeoBounds`、`Aabb`、将来 `PolygonIntersection`） |
| `OsmDotRoute.Profiles` | `src/OsmDotRoute/Profiles/` | ステップ 5a で確立 | `JsonProfileDefinition`（+sub DTO）, `ProfileEvaluator`, `EdgeEvaluation`, `DifficultyEvaluation`, `OnewayDirection`、埋込 `car.json` / `pedestrian.json` |
| `OsmDotRoute.GeoJson` | `src/OsmDotRoute/GeoJson/` | ステップ 6 で導入 | `GeoJsonWriter`（static, internal）。ステップ 10（入力）・ステップ 11（経路出力）で拡張予定 |
| `OsmDotRoute.Mesh` | `src/OsmDotRoute/Mesh/` | ステップ 7 で確立 | メッシュコード変換（`MeshCodeConverter`、JIS X0410 8〜10 桁対応） |
| `OsmDotRoute.Restrictions` | `src/OsmDotRoute/Restrictions/` | 雛形のみ（ステップ 8〜9 で実装） | `RestrictedArea`, `RestrictedAreaId`, `BlockArea`, `DifficultyArea`, `RestrictedAreaService` |
| `OsmDotRoute.Itinero` | `src/OsmDotRoute.Itinero/` 別アセンブリ | ステップ 3〜4 で確立 | Itinero アダプター（`ItineroRouterDbLoader`, `ItineroRoadGraph`, `ItineroEdgeEnumeratorAdapter`, `ItineroSnapper`）。Phase 2 で破棄 |
| `OsmDotRoute.Extensions.DependencyInjection` | `src/OsmDotRoute.Extensions.DependencyInjection/` 別アセンブリ | ステップ 12 で確立 | DI 統合（`ServiceCollectionExtensions.AddOsmDotRoute`, `OsmDotRouteOptions`）。`Microsoft.Extensions.DependencyInjection.Abstractions` のみに依存 |

### 2.5 Profile 戦略（フェーズ毎の発展）

**確定方針**: プロファイルは JSON 外部ファイル形式とし、ライブラリのリビルドなしにパラメータ調整可能とする（REQ-PRF-007、ユーザー要求 2026-05-18）。

理由:

- Itinero が Lua を採用した動機（リビルド不要なチューニング）を継承する必要がある
- 災害シミュレーションでは「冠水時の車両速度」等のパラメータをシミュレーション運用中に調整したい需要が高い
- Lua を採用しない理由: ランタイム Lua インタプリタ依存を避ける（C# 純粋実装で完結）

**フェーズ毎の発展**:

| Phase | プロファイル実装方式 | Lua の扱い | 主な制約 |
|---|---|---|---|
| Phase 1 | `JsonVehicleProfile` を主実装。同梱 `Profiles/car.json` / `pedestrian.json`。OSM タグ評価は `ProfileEvaluator` が JSON ルール解釈で実施 | 我々のコードでは扱わない。Itinero は内部利用しない（Phase 1 から `Profile.FactorAndSpeed` 直接呼出を廃止） | Car / Pedestrian のみ。Bicycle/Truck は要 RouterDb 再生成（Phase 2 で対応） |
| Phase 2 | 独自フォーマット変換時に各 `edge_profile` を `ProfileEvaluator` で評価して bake、ランタイム O(1) ルックアップ | 完全廃止 | RouterDb 再生成で Bicycle / Truck 追加可（REQ-PRF-003〜004） |
| Phase 3 | OSM PBF パース時にも同じ `ProfileEvaluator` 使用、Emergency / Disaster 用追加 JSON 同梱 | 完全廃止 | Emergency / Disaster プロファイル対応（REQ-PRF-005〜006） |
| Phase 4+ | ユーザー定義 JSON 配置 + `IVehicleProfile` C# 拡張 API + Lua 互換層（別アセンブリ、要望次第） | 別アセンブリで Lua 互換層を提供する余地あり（REQ-PRF-016） | — |

**難所タイプ（Difficulty Type）の設計分離**:

- **客観的事実は制約に**: シミュレーションは「この道路が冠水している」という事実だけ `RestrictedAreaService.AddDifficultyArea(polygon, "flooding")` で登録（REQ-RST-004）
- **主観的反応はプロファイルに**: 各 JSON プロファイルが `difficulty.flooding.speedFactor` で「自分にとっての冠水の意味」を定義（REQ-PRF-011）
- 同じ「冠水」でも Car は ×0.3、Pedestrian は ×0.1、Emergency は ×0.5 のように分離

**組込み難所タイプ 8 種**（REQ-PRF-012、英語キー＋日本語名）:

`flooding`（冠水）/ `liquefaction`（液状化）/ `landslide`（土砂崩れ）/ `construction`（工事中）/ `obstacle`（障害物。瓦礫包含）/ `congestion`（交通集中）/ `snow`（積雪）/ `ice`（凍結）

**ユーザー定義タイプ**（REQ-PRF-013）: 任意の英数字＋アンダースコア文字列キー。プロファイル JSON で定義 + シミュレーション側で同じキーを使用すれば動作。プロファイルに未定義のキーには `difficultyDefault`（規定: 速度1.0／通行可）を適用（REQ-PRF-014）。

**重複ルール**（REQ-RST-030〜032）:

- 同一エッジに複数難所交差時: 各 `speedFactor` の積（例: 0.3 × 0.2 = 0.06）
- いずれかが `canPass: false` → 通行不可（短絡評価）
- BlockArea と DifficultyArea 重複 → BlockArea 優先（通行不可）

詳細設計はステップ 5a 完了時に §7a「JSON プロファイル基盤」へ記述する。

---

## 3. プロジェクト構成

**対応ステップ**: ステップ 1
**対応要件**: REQ-NFR-005（.NET 9）, REQ-NFR-006（Windows x64）, REQ-LIC-001（MIT）, REQ-DEP-001（ランタイム Itinero + System.* のみ）
**実装日**: 2026-05-18
**実装バージョン**: 0.1.0 想定（ユーザー採番待ち）
**主要ファイル**:

- `OsmDotRoute.sln`
- `Directory.Build.props`
- `LICENSE`（MIT）
- `.editorconfig`
- `src/OsmDotRoute/OsmDotRoute.csproj`
- `src/OsmDotRoute.Itinero/OsmDotRoute.Itinero.csproj`
- `tests/OsmDotRoute.Tests/OsmDotRoute.Tests.csproj`
- `tests/OsmDotRoute.Benchmarks/OsmDotRoute.Benchmarks.csproj`
- `samples/ConsoleDemo/ConsoleDemo.csproj`

### 3.1 意図

Phase 1 着手のための足場作り。以下を満たすソリューションを構築:

- .NET 9 / Windows x64 でビルド可能（REQ-NFR-005, REQ-NFR-006）
- メイン `OsmDotRoute` を Itinero 非依存に保ち、アダプター層 `OsmDotRoute.Itinero` に依存を閉じ込め（Phase 2 で破棄しやすい構造、REQ-DEP-001）
- 公開ライセンスを MIT で固定（REQ-LIC-001）

### 3.2 採用設計

**ソリューション構造**:

```text
OsmDotRoute.sln
├── src/
│   ├── OsmDotRoute/                                  # コアライブラリ（System.* のみ）
│   ├── OsmDotRoute.Itinero/                          # Itinero アダプター
│   └── OsmDotRoute.Extensions.DependencyInjection/   # DI 統合（ステップ 12 で追加）
├── tests/
│   ├── OsmDotRoute.Tests/                # xUnit
│   └── OsmDotRoute.Benchmarks/           # BenchmarkDotNet
└── samples/
    └── ConsoleDemo/                       # 手動検証用 console
```

**プロジェクト依存マトリクス**:

| プロジェクト | TargetFramework | ProjectReference | PackageReference |
|---|---|---|---|
| `OsmDotRoute` | net9.0 | （なし） | （なし） |
| `OsmDotRoute.Itinero` | net9.0 | `OsmDotRoute` | `Itinero` 1.5.1, `Itinero.IO.Osm` 1.5.1 |
| `OsmDotRoute.Extensions.DependencyInjection` | net9.0 | `OsmDotRoute`, `OsmDotRoute.Itinero` | `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0 |
| `OsmDotRoute.Tests` | net9.0 | `OsmDotRoute`, `OsmDotRoute.Itinero` | xUnit（テンプレ既定）, `Microsoft.NET.Test.Sdk` |
| `OsmDotRoute.Benchmarks` | net9.0 | `OsmDotRoute`, `OsmDotRoute.Itinero` | `BenchmarkDotNet` 最新 |
| `ConsoleDemo` | net9.0 | `OsmDotRoute`, `OsmDotRoute.Itinero` | （なし） |

**`InternalsVisibleTo`**:

`src/OsmDotRoute/OsmDotRoute.csproj` で以下を許可。

- `OsmDotRoute.Itinero` — `IRoadGraph`（internal）実装のため必須
- `OsmDotRoute.Tests` — 内部実装の単体テスト用
- `OsmDotRoute.Benchmarks` — 内部実装の計測用

**`Directory.Build.props` 共通設定**:

- `LangVersion=latest`、`Nullable=enable`、`ImplicitUsings=enable`
- `EnforceCodeStyleInBuild=true`（.editorconfig を build 時に適用）
- `NeutralLanguage=ja-JP`
- `TreatWarningsAsErrors=false`（理由は §3.4 参照）
- `GenerateDocumentationFile=true`（ステップ 12 で `false` → `true` に切替、§14 参照。テスト/ベンチマーク/サンプル csproj では個別に `false` で上書き）
- メタ情報: `Company=Grandge` / `Authors=Grandge` / `Copyright=Copyright (c) 2026 Grandge` / `Product=OsmDotRoute` / `PackageLicenseExpression=MIT`

**`.editorconfig`**:

- UTF-8 / CRLF / 最終改行あり / 末尾空白除去
- C#: 4 スペース、`new_line_before_open_brace=all`、System ディレクティブ優先
- JSON / YAML / Markdown / csproj: 2 スペース

**`LICENSE`**:

- 標準 MIT テンプレート
- Copyright: `2026 Grandge`（Git Author identity に準拠）

### 3.3 設計判断の根拠

- **`OsmDotRoute.Itinero` を別アセンブリに分離した理由**: Phase 2 でランタイム Itinero 依存削除（REQ-MAP-006, REQ-DEP-002）の際、メインアセンブリの参照グラフを変更せずアダプター側のみ差し替え可能とするため
- **`OsmDotRoute.Extensions.DependencyInjection` を Step 1 で作らない理由**: 実装計画書 §7 ステップ 12 で追加予定。本体に DI 依存を持ち込まない原則を維持しつつ、不要な空プロジェクトを早期に作らない
- **`MapVerifier` プロジェクトを Step 1 で作らない理由**: 実装計画書 §7 ステップ 13〜14 で追加予定。Phase 1 コアが動作する前にフロント整備しても無駄になる
- **`Itinero.IO.Osm` を `OsmDotRoute.Itinero` に含めた理由**: 親プロジェクト `MapService.LoadOsmPbf()` が利用しており、Phase 1 で同等機能の透過提供を可能にしておく（REQ-MAP-001 は `.routerdb` 読込のみだが、PBF ロードも要望が出る可能性が高い）
- **`InternalsVisibleTo` をテストにも開放した理由**: 内部実装（Dijkstra、AABB、メッシュ変換等）の挙動を直接テストするため。公開 API 経由のテストだけだと境界条件のカバレッジが不足する
- **メタ情報の `Company=Grandge`**: 個人プロジェクトとして個人 GitHub アカウントで OSS 公開する想定（REQ-PKG-003）

### 3.4 トレードオフ・制約

- **`TreatWarningsAsErrors=false` を採用**: 計画書 §8 ステップ 1 では `true` を例示したが、Itinero 1.5.1 が `Nullable` 非対応のため `OsmDotRoute.Itinero` の wrap コードで多数の警告が出る見込み。初期から赤くなり続けるとレビュー負荷が高いため `false` で開始。Step 12（README/ドキュメント整備）か Step 17（最終）で再評価。代わりに `EnforceCodeStyleInBuild=true` で `.editorconfig` を build 時適用し、最低限の整形は維持
- **`GenerateDocumentationFile=false` で開始 → ステップ 12 で `true` に切替**: XML doc 完備までは warning を抑制し、ステップ 12 で本体 3 プロジェクト一括有効化。テスト/ベンチマーク/サンプル csproj では個別に `false` 上書き
- **`InternalsVisibleTo` でテストプロジェクトに開放**: 公開 API の境界が曖昧になるリスクあり。テストの命名規約で「`Internal_` プレフィクスは内部テスト」と運用区別する想定（Step 5 以降で確立）
- **エンコーディング `CRLF` 固定**: Windows 専用前提（REQ-NFR-006）に合わせた。Linux/macOS 対応（REQ-NFR-007, Phase 4+）時に LF へ変更検討

### 3.5 検証方法

- `dotnet build OsmDotRoute.sln`: 0 警告・0 エラーで成功すること
- `dotnet sln list`: 5 プロジェクトが登録されていること
- 各 .csproj の参照グラフが §3.2 のマトリクス通りであること

**ステップ 1 実施結果（2026-05-18）**:

```text
ビルドに成功しました。
    0 個の警告
    0 エラー
経過時間 00:00:11.12
```

ビルド成果物:

- `src/OsmDotRoute/bin/Debug/net9.0/OsmDotRoute.dll`
- `src/OsmDotRoute.Itinero/bin/Debug/net9.0/OsmDotRoute.Itinero.dll`
- `tests/OsmDotRoute.Tests/bin/Debug/net9.0/OsmDotRoute.Tests.dll`
- `tests/OsmDotRoute.Benchmarks/bin/Debug/net9.0/OsmDotRoute.Benchmarks.dll`
- `samples/ConsoleDemo/bin/Debug/net9.0/ConsoleDemo.dll`

### 3.6 実装メモ

- `dotnet new classlib` のテンプレート既定 `Class1.cs` は各プロジェクトに残置中。Step 2 で公開型実装時に削除予定
- `dotnet new xunit` のテンプレート既定 `UnitTest1.cs` も同様、Step 3 以降のテスト追加時に削除予定
- `Directory.Build.props` のメタ情報（Company/Authors/Copyright）は OSS 公開時にユーザー判断で再調整される可能性あり。現時点は Git Author identity と整合
- `.gitignore` は Phase 0 で配置済みのものを継続利用（`bin/`, `obj/`, `.vs/`, `.routerdb` 等を除外済み）
- Itinero NuGet 取得は問題なく完了。1.5.1 はインターネット経由で取得可能（廃止リスクはまだ顕在化していない）

---

## 4. 公開型カタログ

**対応ステップ**: ステップ 2
**対応要件**: REQ-API-001〜004, REQ-FMT-001〜003, REQ-RTE-001〜007, REQ-RST-001〜011, REQ-MAP-001〜002, REQ-PRF-001〜014
**実装日**: 2026-05-18
**実装バージョン**: 0.1.0 想定（ユーザー採番待ち）
**主要ファイル**:

- `src/OsmDotRoute/Router.cs`
- `src/OsmDotRoute/RouterDb.cs`
- `src/OsmDotRoute/Route.cs`
- `src/OsmDotRoute/GeoCoordinate.cs`
- `src/OsmDotRoute/GeoPolygon.cs`
- `src/OsmDotRoute/MeshCode.cs`
- `src/OsmDotRoute/MeshLevel.cs`
- `src/OsmDotRoute/VehicleProfile.cs`
- `src/OsmDotRoute/DifficultyTypes.cs`
- `src/OsmDotRoute/Route.cs`
- `src/OsmDotRoute/RoadNetworkGeoJson.cs`
- `src/OsmDotRoute/RouterDbStatistics.cs`
- `src/OsmDotRoute/Restrictions/RestrictedAreaId.cs`
- `src/OsmDotRoute/Restrictions/RestrictedArea.cs`
- `src/OsmDotRoute/Restrictions/BlockArea.cs`
- `src/OsmDotRoute/Restrictions/DifficultyArea.cs`
- `src/OsmDotRoute/Restrictions/RestrictedAreaService.cs`

### 4.1 意図

要件定義書 §7.1 で確定した公開 API シグネチャをコード化し、コンパイル可能な状態にする。実装本体は後続ステップで埋める前提で `NotImplementedException` を返す。

ねらい:

- 後続ステップ（3, 4, 5a, 5b, 6, 8, 10）の実装が公開 API シグネチャの確定情報を共有できる
- 親プロジェクト統合検証（ステップ 16）に向け、API 表面を早期に固める
- ユーザーが公開 API 設計の方向性を Step 2 時点で確認できる

### 4.2 採用設計

#### 公開型一覧（全 15 型）

**コア（ルート空間 `OsmDotRoute`）**:

| 型 | 種別 | 責務 | 実装状態 |
|---|---|---|---|
| `Router` | sealed class | ファサード（経路計算・スナップ・GeoJSON 出力） | スケルトン |
| `RouterDb` | sealed class | グラフデータ（`.routerdb` 読込、統計取得） | スケルトン |
| `Route` | sealed class | 経路計算結果（距離・所要時間・形状） | 値保持完了 |
| `GeoCoordinate` | readonly record struct | 緯度経度値オブジェクト | 完了 |
| `GeoPolygon` | sealed class | 外側境界＋Hole 配列の多角形 | 値保持完了、境界検証あり |
| `MeshCode` | readonly record struct | JIS X0410 メッシュコード（桁数で `Level` 判定） | 完了 |
| `MeshLevel` | enum | メッシュ階層（4 種） | 完了 |
| `VehicleProfile` | sealed class | JSON プロファイル（`Car`/`Pedestrian` static、`LoadFromJson*`） | スケルトン |
| `DifficultyTypes` | static class | 組込み 8 難所タイプ `const string` | 完了 |
| `RoadNetworkGeoJson` | sealed class | 道路ネットワーク GeoJSON 出力ラッパー | 値保持完了 |
| `RouterDbStatistics` | sealed class | 頂点数・辺数・経緯度範囲 | 値保持完了 |

**制約管理（フォルダ `Restrictions/`、namespace は `OsmDotRoute`）**:

| 型 | 種別 | 責務 | 実装状態 |
|---|---|---|---|
| `RestrictedAreaId` | readonly record struct | エリア一意 ID（Guid ラップ） | 完了（`New()` ファクトリあり） |
| `RestrictedArea` | abstract class | 制約基底（Id・Tag） | 完了 |
| `BlockArea` | sealed class : RestrictedArea | 進入不可（ポリゴン or メッシュ） | 値保持完了 |
| `DifficultyArea` | sealed class : RestrictedArea | 難所（ポリゴン or メッシュ + DifficultyType） | 値保持完了、空文字検証あり |
| `RestrictedAreaService` | sealed class | 登録・削除・一覧サービス | スケルトン |

#### `VehicleProfile` 特記事項

- 要件 v1.1 まで `enum { Car, Pedestrian }` だったが、v1.2 で **class へ変更**（JSON 外部化のため、REQ-PRF-007〜010）
- `Car` / `Pedestrian` static プロパティは Step 2 時点では空インスタンスを保持（`Name` のみ）。Step 5a で `Profiles/car.json` / `pedestrian.json` から遅延ロードする本実装に置き換える
- `LoadFromJsonFile` / `LoadFromJsonString` / `LoadFromJsonStream` は Step 5a で実装

#### `DifficultyTypes` 設計

- 公開 `static class` に組込み 8 タイプを `const string` で定義（IDE 補完用）
- ユーザー定義タイプ（REQ-PRF-013）は文字列直接指定可（`AddDifficultyArea(polygon, "snow_heavy")` 等）

#### `BlockArea` / `DifficultyArea` の二系統コンストラクタ

- ポリゴン版・メッシュコード版の 2 種コンストラクタ
- 使用していない方は `null` プロパティで表現（`Polygon?` / `MeshCode?`）
- メッシュコードは値型なので `Nullable<MeshCode>`

#### 名前空間設計

- 全公開型を **`OsmDotRoute` ルート名前空間** に配置（要件定義書 §7.1 準拠）
- `Restrictions/` フォルダはあくまで物理配置上の整理。論理 namespace は `OsmDotRoute`
- 将来追加する内部型（Step 3 以降）は `OsmDotRoute.Routing`, `OsmDotRoute.Geometry`, `OsmDotRoute.Mesh`, `OsmDotRoute.GeoJson`, `OsmDotRoute.Profiles` 等の子 namespace に配置予定

### 4.3 設計判断の根拠

- **`Router` コンストラクタで `RestrictedAreaService` を `null` 許容**: シミュレーションで制約を使わないケース（純粋なルート探索）でもサービス生成不要にするため
- **`GeoCoordinate` を record struct**: 不変・等価判定が無料、Latitude/Longitude のみのシンプル値
- **`GeoPolygon` を class（record にしない）**: 内部に List/Array を持つので record 等価判定（参照等価）はミスリーディング、明示クラスにした
- **`MeshCode.Level` を計算プロパティに**: 値そのものから階層導出できるので別フィールドは冗長。Step 7 で本検証実装（REQ-RST-018）
- **`RestrictedAreaId` を `New()` ファクトリ提供**: `Guid.NewGuid()` 直接呼びを各所で書くのを避ける
- **`DifficultyArea` で `ValidateDifficultyType` を Step 2 から実装**: 空文字・null 拒否は単純で Step 8 を待つ理由がない（REQ-RST-007）
- **`RoadNetworkGeoJson` を文字列ラッパーに**: GeoJSON 出力は最終的に JSON 文字列。中間 DTO（FeatureCollection モデル）を作るのは過剰。`ToString()` で JSON 取得可
- **`NotImplementedException` メッセージに「Step N で実装予定」**: 後続セッションで作業中の Claude / ユーザーが、どこを次に埋めるべきか即座に判別できる

### 4.4 トレードオフ・制約

- **`VehicleProfile.Car` / `.Pedestrian` が Step 2 時点で「名前だけ持つ空インスタンス」**: 利用者が `Car` を `Router.Calculate` に渡すと現状は `NotImplementedException`（Calculate 側）。Step 5a 完了まで実用不可。スケルトン段階の制約として許容
- **`RestrictedArea` 基底に `GeoPolygon? Polygon` / `MeshCode? MeshCode` を持たせず派生型に重複**: コードが若干 DRY でない。代替案として共通 `IAreaShape` 抽象を導入することも検討したが、Phase 1 スコープでは 2 種類しかないため YAGNI で見送り
- **`MeshCode` 範囲外検証が `Level` プロパティアクセス時に発生**: 構築時検証ではない。Step 7 で構築時検証に変更検討（要件 REQ-RST-018 を厳密に満たすため）。現状はコンストラクタは `record struct(long)` 自動生成
- **GenerateDocumentationFile=false（§3.4 で確定）**: XML doc は付与済みだが NuGet 配布用 .xml は生成しない。Step 12 で `true` に切替
- **API シグネチャの一部は後続ステップで微調整される可能性あり**: 例えば `RouterDb.LoadFromFile` が `Stream` 版を追加する等。要件定義書 §7.1 と乖離する場合は v1.3 で要件側を更新

### 4.5 検証方法

- `dotnet build OsmDotRoute.sln`: 0 警告・0 エラーで成功すること
- 公開 API シグネチャが要件定義書 §7.1 と一致すること（ユーザーレビュー）
- 各 `NotImplementedException` メッセージが対応ステップ番号を含むこと

**ステップ 2 実施結果（2026-05-18）**:

```text
ビルドに成功しました。
    0 個の警告
    0 エラー
経過時間 00:00:05.49
```

新規追加ファイル: 15 ファイル（公開型 15 種）。削除: `src/OsmDotRoute/Class1.cs`, `src/OsmDotRoute.Itinero/Class1.cs`（テンプレ既定）。

### 4.6 実装メモ

- `src/OsmDotRoute.Itinero/` は本ステップで `Class1.cs` を削除したため、現在クラスを 1 つも持たない状態。Itinero NuGet 参照のみ残る。Step 3 で `ItineroRoadGraph` 等を追加するまで空アセンブリ
- `tests/OsmDotRoute.Tests/UnitTest1.cs` はテンプレ既定の xUnit サンプルを残置。Step 3 以降で公開型のテストに置き換える
- `samples/ConsoleDemo/Program.cs` もテンプレ既定の "Hello World" を残置。Step 4 以降で `Router` 利用サンプルに書き換える
- `OsmDotRoute.csproj` の `InternalsVisibleTo` 設定により、Tests / Itinero / Benchmarks から internal 型へアクセス可（Step 3 以降で活用）
- Phase 2/3 では本ステップで定義した公開 API シグネチャを維持しつつ、内部実装のみ差し替える方針（REQ-API-002, REQ-API-003）

---

## 5. Itinero アダプター

**対応ステップ**: ステップ 3
**対応要件**: REQ-MAP-001, REQ-MAP-002, REQ-API-003, REQ-DEP-001
**実装日**: 2026-05-18
**実装バージョン**: 0.1.0 想定（ユーザー採番待ち）
**主要ファイル**:

- `src/OsmDotRoute/Routing/IRoadGraph.cs`（内部抽象）
- `src/OsmDotRoute/Routing/IRoadGraphEdgeEnumerator.cs`（内部抽象）
- `src/OsmDotRoute/Geometry/GeoBounds.cs`（内部値型）
- `src/OsmDotRoute/RouterDb.cs`（改修：`LoadFromFile` 削除、`internal RouterDb(IRoadGraph)`、`GetStatistics()` 実装）
- `src/OsmDotRoute.Itinero/ItineroRoadGraph.cs`（`IRoadGraph` 実装）
- `src/OsmDotRoute.Itinero/ItineroEdgeEnumeratorAdapter.cs`（`IRoadGraphEdgeEnumerator` 実装）
- `src/OsmDotRoute.Itinero/ItineroRouterDbLoader.cs`（公開ローダー）
- `tests/OsmDotRoute.Tests/ItineroAdapterTests.cs`（6 テスト）

### 5.1 意図

Itinero `RouterDb` を内部抽象 `IRoadGraph` でラップし、Phase 2 以降で独自グラフ実装に差し替え可能にする。同時に Phase 1 の経路探索エンジン（ステップ 5b）と制約管理（ステップ 8〜9）が依存する API 面を確定する。

### 5.2 採用設計

#### 内部抽象（`OsmDotRoute.Routing` namespace）

**`IRoadGraph`**:

| メンバー | 型 | 責務 |
|---|---|---|
| `VertexCount` | `uint` | 頂点数 |
| `EdgeCount` | `long` | 辺数（Itinero に合わせ `long`） |
| `GetBounds()` | `GeoBounds` | 経緯度範囲 AABB（初回 O(V)） |
| `GetVertex(uint)` | `GeoCoordinate` | 頂点座標 |
| `GetEdgeEnumerator(uint)` | `IRoadGraphEdgeEnumerator` | 頂点起点のエッジ列挙 |
| `GetEdgeOsmTags(ushort)` | `IReadOnlyDictionary<string,string>` | エッジ profile index → OSM タグ |

**`IRoadGraphEdgeEnumerator`** （Itinero `EdgeEnumerator` 相当）:

| メンバー | 型 | 責務 |
|---|---|---|
| `MoveNext()` | `bool` | 次エッジへ進める |
| `EdgeId`, `From`, `To` | `uint` | エッジ ID と端点 |
| `EdgeProfileIndex` | `ushort` | OSM タグ集合参照用 |
| `DistanceM` | `float` | エッジ距離（メートル） |
| `DataInverted` | `bool` | 反転格納フラグ（Itinero 由来） |
| `Shape` | `IReadOnlyList<GeoCoordinate>` | 中間シェイプ（端点除く） |

#### アダプター実装（`OsmDotRoute.Itinero` プロジェクト）

- **`ItineroRoadGraph`** : `IRoadGraph`
  - Itinero `RouterDb.Network` の API を 1:1 でラップ
  - `GetEdgeOsmTags`: `_routerDb.EdgeProfiles.Get(index)` で `IAttributeCollection` を取得し `Dictionary<string,string>` 化
  - `GetBounds`: 全頂点を走査して min/max を算出（一度きりの計算、結果は呼び出し側でキャッシュ想定）
- **`ItineroEdgeEnumeratorAdapter`** : `IRoadGraphEdgeEnumerator`
  - Itinero `RoutingNetwork.EdgeEnumerator` を保持し、各プロパティを `EdgeData.Profile` / `.Distance` に転送
  - `Shape` プロパティは Itinero `ShapeBase` から `GeoCoordinate` 配列に変換（リスト化）
- **`ItineroRouterDbLoader`**（**public static**）
  - `LoadFromFile(string)` : ファイルから RouterDb を生成
  - `FromItineroRouterDb(global::Itinero.RouterDb)` : 既に読み込み済みインスタンスから生成（親プロが独自にロードする場合の橋渡し）

#### 公開 `OsmDotRoute.RouterDb` の改修

- `LoadFromFile` 静的メソッドを**削除**（要件 v1.3 で更新予定、§5.3 参照）
- `internal RouterDb(IRoadGraph graph)` コンストラクタ追加（アダプターから生成）
- `internal IRoadGraph Graph { get; }` 追加（ステップ 5b の Dijkstra から参照）
- `public GetStatistics()` 実装（`_graph.VertexCount/EdgeCount/GetBounds()` をラップ）

#### 名前空間整理

| Namespace | 内容 |
|---|---|
| `OsmDotRoute` | 公開型（Router, RouterDb, Route, ...）+ 既存 |
| `OsmDotRoute.Geometry` | **新規**。`GeoBounds`（internal）。将来 Aabb, PolygonIntersection も配置 |
| `OsmDotRoute.Routing` | **新規**。`IRoadGraph`, `IRoadGraphEdgeEnumerator`（internal）。将来 DijkstraEngine 等も配置 |
| `OsmDotRoute.Itinero` | アダプター層（`ItineroRoadGraph`, `ItineroRouterDbLoader` 等） |

### 5.3 設計判断の根拠

- **`RouterDb.LoadFromFile` を削除し `ItineroRouterDbLoader.LoadFromFile` に移動した理由**:
  - アセンブリ依存方向 `OsmDotRoute ← OsmDotRoute.Itinero` を維持するため、`OsmDotRoute.RouterDb` から Itinero アダプターを直接呼べない
  - 代替案検討:
    - 静的 Func 登録（プラグインパターン）: 初期化忘れの不具合発生リスク
    - リフレクションで `Assembly.Load("OsmDotRoute.Itinero")`: ブラックボックスでデバッグ困難
    - 採用案: 利用者が `using OsmDotRoute.Itinero` を明示的に書く（ローカルプロジェクト参照に依存性を可視化）
  - 結果: 利用者コードは `var db = ItineroRouterDbLoader.LoadFromFile(path);` で 1 行明示。`using Itinero;` は不要なので REQ-API-003 違反なし
- **`IRoadGraph` を internal にした理由**: Phase 2 で独自グラフ実装に差し替える際、公開 API として固定したくない。差し替え時に internal なら自由
- **`EdgeCount` を `long` にした理由**: Itinero `RoutingNetwork.EdgeCount` が `long`（`GeometricGraph.EdgeCount` の `long` を継承）。公開 `RouterDbStatistics.EdgeCount` は `int` のままだが、都道府県単位（数百万）では int に収まる前提（checked キャスト）
- **`Shape` プロパティでリスト変換した理由**: Itinero `ShapeBase` は遅延列挙だが、複数回参照されるケースで都度列挙すると重い。配列化してキャッシュ性を優先
- **`GetBounds()` を毎回計算する理由（キャッシュなし）**: 呼び出し頻度が低い（統計取得時のみ）。キャッシュ管理コストの方が大きい
- **`FromItineroRouterDb` 補助メソッド**: 親プロジェクトは既存 `LoadOsmData` や独自カスタマイズで RouterDb を生成している可能性があり、ファイルパス渡しでない API も必要

### 5.4 トレードオフ・制約

- **要件 v1.2 §7.1 から API シグネチャが乖離**: `public static RouterDb LoadFromFile(string)` を仕様に書いていたが本実装で削除。要件定義書 v1.3 で更新する
- **`Shape` の `IReadOnlyList<GeoCoordinate>` 都度生成**: Dijkstra のホットパスで非効率（多数 Allocation）。ステップ 5b でプロファイル評価時はシェイプ不要、ステップ 9 で制約交差判定時のみ必要。必要に応じプール化検討
- **`GetBounds()` 計算コスト O(V)**: 都道府県単位だと数百万頂点で数百 ms。統計取得は起動時 1 回のみと割り切る。Phase 2 独自フォーマットでは bounds をヘッダーに格納してキャッシュ
- **`ItineroRoadGraph` の internal 公開度**: テストプロジェクト（`InternalsVisibleTo`）から型を参照できる。テスト容易性は向上するが、外部リフレクションで使われると Phase 2 で破壊的変更となる。テストコメントで Phase 2 削除予定を明記すべき（ステップ 5b 以降）
- **`Profile.FactorAndSpeed` への直接依存を本ステップで完全廃止**: アダプターは「グラフデータ＋OSM タグ」のみ提供。プロファイル評価はステップ 5a で OsmDotRoute 側に独自実装。要件 v1.2 で確定した方針通り

### 5.5 検証方法

- `dotnet build`: 0 警告・0 エラー（**確認済**）
- `dotnet test`: 6/6 成功（**確認済**）

**テスト一覧**（`ItineroAdapterTests`）:

| # | テスト名 | 検証内容 |
|---|---|---|
| 1 | `LoadFromFile_ParentDefault_LoadsAndExposesBasicStatistics` | 親プロ `default.routerdb` 読込、頂点数・辺数 > 0、bounds 整合 |
| 2 | `LoadFromFile_ParentDefault_BoundsAreInJapan` | bounds が日本領域内 (緯度 20-46, 経度 122-154) |
| 3 | `GetEdgeEnumerator_ParentDefault_EnumeratesEdgesWithValidData` | エッジ列挙、終点 ID < 頂点数、距離 > 0 |
| 4 | `GetEdgeOsmTags_ParentDefault_ContainsHighwayKey` | OSM タグに `highway` キーが含まれる |
| 5 | `LoadFromFile_NonExistentPath_ThrowsFileNotFoundException` | 存在しないパスで `FileNotFoundException` |
| 6 | `LoadFromFile_NullOrEmptyPath_ThrowsArgumentException` | null/空/空白で `ArgumentException` |

実行結果:

```text
成功! -失敗: 0、合格: 6、スキップ: 0、合計: 6、期間: 3 s
```

### 5.6 実装メモ

- テストデータパス `d:\workspace\災害廃棄物処理シミュレーション\App\DisasterWasteSim.Server\Data\Scenarios\default.routerdb`（約 19MB）は `tests/OsmDotRoute.Tests/TestData/TestPaths.cs` にハードコード。CI 化時は環境変数化等で対応
- xUnit v2 は動的スキップが標準化されていないため、テストデータ不在時は `Assert.Fail` で明示失敗（ユーザー環境では常に存在する前提）
- `Itinero.RouterDb` と `OsmDotRoute.RouterDb` の名前衝突は `using ItineroDb = global::Itinero.RouterDb;` のエイリアスで回避
- `tests/OsmDotRoute.Tests/UnitTest1.cs`（テンプレ既定）を削除
- 親プロジェクトの統合検証（ステップ 16）では、`MapService.LoadRouterDbFromFile` を `ItineroRouterDbLoader.LoadFromFile` または `FromItineroRouterDb` に置き換える想定

---

## 6. 道路スナップ

**対応ステップ**: ステップ 4
**対応要件**: REQ-RTE-002, REQ-RTE-003, REQ-RTE-008
**実装日**: 2026-05-18
**実装バージョン**: 0.1.0 想定（ユーザー採番待ち）
**主要ファイル**:

- `src/OsmDotRoute/Routing/IRoadSnapper.cs`（内部抽象 + `SnapResult` 値型）
- `src/OsmDotRoute/RouterDb.cs`（改修：`Snapper` プロパティ追加、コンストラクタ拡張）
- `src/OsmDotRoute/Router.cs`（`SnapToRoad` 実装）
- `src/OsmDotRoute.Itinero/ItineroSnapper.cs`（`IRoadSnapper` 実装）
- `src/OsmDotRoute.Itinero/ItineroRouterDbLoader.cs`（改修：snapper 同時生成）
- `tests/OsmDotRoute.Tests/SnapToRoadTests.cs`（6 テスト）

### 6.1 意図

任意座標を最寄り道路にスナップする機能を提供する（REQ-RTE-002）。検索半径は呼び出し側で指定可能（REQ-RTE-003）、ネットワーク外座標時は `null` を返却（REQ-RTE-008）。Phase 1 は Itinero の `Router.Resolve` を利用するが、内部的にエッジ ID とオフセットも取得して経路探索（ステップ 5b）で再利用できるようにする。

### 6.2 採用設計

#### 内部抽象 `IRoadSnapper`

```csharp
internal interface IRoadSnapper
{
    SnapResult? Snap(string profileName, GeoCoordinate point, float searchDistanceM);
}

internal readonly record struct SnapResult(
    GeoCoordinate Location,
    uint EdgeId,
    ushort Offset);  // 0=From 頂点, 65535=To 頂点
```

`SnapResult` には公開 API では使わないエッジ ID とオフセットも含める。これはステップ 5b の Dijkstra で「snap した点から経路探索を開始する」際に必要。今は内部のみで保持。

#### 公開 API `Router.SnapToRoad`

```csharp
public GeoCoordinate? SnapToRoad(VehicleProfile profile, GeoCoordinate point, float searchDistanceM = 500f)
{
    ArgumentNullException.ThrowIfNull(profile);
    var result = _routerDb.Snapper.Snap(profile.Name, point, searchDistanceM);
    return result?.Location;
}
```

`SnapResult` の `Location` のみ公開。エッジ ID/オフセットは内部用途のみ。

#### Itinero アダプター `ItineroSnapper`

- コンストラクタで `global::Itinero.Router` インスタンスを 1 つ生成・保持（空間インデックスのキャッシュ目的）
- `Snap` 内で:
  1. `_routerDb.GetSupportedProfile(profileName)` でプロファイル取得（未対応なら `null` 返却）
  2. `_router.Resolve(profile, lat, lon, searchDistanceM)` 呼出
  3. 成功時: `routerPoint.LocationOnNetwork(_routerDb)` で座標、`routerPoint.EdgeId` / `.Offset` でエッジ位置を取得
  4. `ResolveFailedException` をキャッチして `null` 返却（検索半径内に道路無し）

#### `RouterDb` の改修

- 内部コンストラクタが `(IRoadGraph graph, IRoadSnapper snapper)` の 2 引数に拡張
- `internal IRoadSnapper Snapper { get; }` 追加
- `ItineroRouterDbLoader.LoadFromFile` / `FromItineroRouterDb` は内部で `Build(itineroRouterDb)` ヘルパーを呼び、graph と snapper を同時生成

### 6.3 設計判断の根拠

- **`IRoadSnapper` を `IRoadGraph` と別インターフェースにした理由**: 単一責任の原則。グラフ抽象は「グラフデータの読み取り」、スナッパーは「座標 → 道路点」とで責務が異なる。Phase 2 で独自実装する際、スナップは R-tree 等の専用空間インデックスを持つことになり、グラフ I/F に混入させると肥大化する
- **`SnapResult` にエッジ ID とオフセットを含めた理由**: 公開 API では使わないが、ステップ 5b の Dijkstra で「snap 後の点から経路探索を開始」する際に必要。先回りで内部 API に組み込んでおき、ステップ 5b で再 snap を避ける
- **Itinero `Router` インスタンスをスナッパー内でキャッシュ**: `Router` は初回 `Resolve` 時に内部で空間インデックスを構築する。インスタンスを再生成すると都度初期化コストが発生するため、ライフサイクルで 1 インスタンス
- **`using global::Itinero;` を明示**: `Resolve` / `GetSupportedProfile` は拡張メソッド（`RouterBaseExtensions.cs` 由来）のため、namespace を import する必要がある。エイリアスだけでは拡張メソッドが見えない
- **`profile.Name` で文字列ベースのマッピング**: 親プロが `routerDb.LoadOsmData(stream, Vehicle.Car, Vehicle.Pedestrian)` で baked したプロファイルキーが "car" / "pedestrian"。我々の `VehicleProfile.Car.Name` / `Pedestrian.Name` を同じ文字列にしておくことで自動的にマッピングが成立

### 6.4 トレードオフ・制約

- **プロファイル名のマッチングが文字列ベース**: 親プロの RouterDb に "car" / "pedestrian" 以外の名前で baked された場合動かない。標準的な命名なので問題ない想定だが、ステップ 5a の JSON プロファイル実装時に「プロファイル名 ↔ Itinero プロファイル名のマッピング」を再検討する必要あり
- **検索半径既定 500m**: 親プロと同じ値。災害シミュレーションで住民エージェントのメッシュ中心からスナップする想定値。Phase 1 で再評価
- **`ResolveFailedException` 以外の例外は伝播**: 想定外のエラー（破損 RouterDb 等）は呼び出し側に通知。`MapService.cs` の親プロ実装はすべて catch していたが、これは過剰防御と判断
- **`SnapResult.EdgeId` の永続性**: RouterDb 内のエッジ ID は読み込みごとに一意。RouterDb を再生成すると ID が変わる可能性。ステップ 5b でこの仮定が崩れないか確認

### 6.5 検証方法

- `dotnet build`: 0 警告・0 エラー（**確認済**）
- `dotnet test`: 12/12 成功（ステップ 3 の 6 + ステップ 4 の 6）（**確認済**）

**スナップテスト一覧**（`SnapToRoadTests`）:

| # | テスト名 | 検証内容 |
|---|---|---|
| 1 | `SnapToRoad_PointOnNetwork_ReturnsNearbyCoordinate` | 道路頂点座標→スナップ後 0.001 度以内 |
| 2 | `SnapToRoad_PointFarOutsideNetwork_ReturnsNull` | bounds から +5 度離れた点→null |
| 3 | `SnapToRoad_CarProfile_OnRoadNetwork_ReturnsCoordinate` | 車道アクセス可能頂点→スナップ成功 |
| 4 | `SnapToRoad_PedestrianProfile_OnRoadNetwork_ReturnsCoordinate` | 歩行者プロファイル→スナップ成功 |
| 5 | `SnapToRoad_NullProfile_ThrowsArgumentNullException` | null プロファイル→ArgumentNullException |
| 6 | `SnapToRoad_DefaultSearchDistance_Is500Meters` | 既定値 500m の動作確認 |

実行結果:

```text
成功! -失敗: 0、合格: 12、スキップ: 0、合計: 12、期間: 1 s
```

### 6.6 実装メモ

- 親プロ `MapService.SnapToRoad` との比較テスト（実装計画書「親プロの SnapToRoad と同じ座標が返ることを 10 点で確認」）は本ステップでは省略。理由: 親プロ側を Phase 1 完了まで触らない方針（ステップ 16 まで保留）、本ライブラリ単独でスナップ動作は検証できる
- Itinero `Router.Resolve` のスレッドセーフ性は内部空間インデックス構築完了後は readonly。ステップ 5b で並列経路計算を実装する場合は `Router` インスタンス共有可能（要追加検証）
- `SnapResult` の `Offset` は ushort（0-65535）。エッジ長に比例した位置（0=始点、65535=終点）。距離換算: `位置m = (Offset / 65535.0) * edgeDistanceM`

---

## 7a. JSON プロファイル基盤

**対応ステップ**: ステップ 5a
**対応要件**: REQ-PRF-001〜002, REQ-PRF-007〜014
**実装日**: 2026-05-18
**実装バージョン**: 0.1.0 想定（ユーザー採番待ち）
**主要ファイル**:

- `src/OsmDotRoute/Profiles/JsonProfileDefinition.cs`（DTO ルート + サブ型）
- `src/OsmDotRoute/Profiles/ProfileEvaluator.cs`（評価器）
- `src/OsmDotRoute/Profiles/EdgeEvaluation.cs`（評価結果値型）
- `src/OsmDotRoute/Profiles/DifficultyEvaluation.cs`（難所評価結果値型）
- `src/OsmDotRoute/Profiles/OnewayDirection.cs`（enum）
- `src/OsmDotRoute/Profiles/car.json`（埋込リソース）
- `src/OsmDotRoute/Profiles/pedestrian.json`（埋込リソース）
- `src/OsmDotRoute/VehicleProfile.cs`（公開クラス、本実装に置換）
- `src/OsmDotRoute/InvalidProfileException.cs`（公開例外）
- `src/OsmDotRoute/OsmDotRoute.csproj`（`<EmbeddedResource>` 2 件追加）
- `tests/OsmDotRoute.Tests/VehicleProfileTests.cs`（25 テスト）
- `tests/OsmDotRoute.Tests/ProfileParityTests.cs`（2 テスト）

### 7a.1 意図

「ビルドなしでパラメータ調整可能」要件（REQ-PRF-007）を満たす JSON プロファイル基盤を構築。Phase 2/3 以降も使い続ける中核機構として確立。Phase 1 では同梱 `car.json` / `pedestrian.json` を Itinero `Vehicle.Car.Fastest()` / `Vehicle.Pedestrian.Fastest()` 相当に調整し、親プロジェクトの既存経路との大きな乖離を避ける。

### 7a.2 採用設計

#### JSON スキーマ（trail-blazing 確定版）

トップレベル例:

```jsonc
{
  "name": "car",
  "vehicleType": "motor_vehicle",
  "ignoreOneway": false,
  "speedMultiplier": 0.75,                    // 任意、既定 1.0
  "accessTagKeys": ["access", "vehicle", "motor_vehicle"],
  "highway": {
    "motorway":     { "speedKmh": 120, "access": "yes" },
    "footway":      { "speedKmh": 5,   "access": "no"  },
    ...
  },
  "accessValueMap": {
    "yes": "allow", "permissive": "allow", "destination": "allow",
    "no":  "deny",  "private":    "deny"
  },
  "maxspeedTagKey": "maxspeed",
  "maxspeedUnitDefault": "kmh",
  "fallback": { "speedKmh": 10, "access": "no" },
  "speedBounds": { "minKmh": 30, "maxKmh": 200 },
  "difficulty": {
    "flooding":  { "speedFactor": 0.3, "canPass": true  },
    "landslide": { "speedFactor": 0.0, "canPass": false },
    ...
  },
  "difficultyDefault": { "speedFactor": 1.0, "canPass": true }
}
```

#### `VehicleProfile` 公開 API

```csharp
public sealed class VehicleProfile
{
    public string Name { get; }
    public static VehicleProfile Car { get; }         // Lazy<T> で埋込から遅延ロード
    public static VehicleProfile Pedestrian { get; }  // 同上
    public static VehicleProfile LoadFromJsonFile(string filePath);
    public static VehicleProfile LoadFromJsonString(string json);
    public static VehicleProfile LoadFromJsonStream(Stream stream);
    internal ProfileEvaluator Evaluator { get; }
}
```

- `Car` / `Pedestrian` は `Lazy<VehicleProfile>` で初回アクセス時にロード（テストで `Assert.Same` 確認済）
- 埋込リソース名は `OsmDotRoute.Profiles.car.json` / `OsmDotRoute.Profiles.pedestrian.json`（namespace dot folder dot file）
- `System.Text.Json` で `CamelCase` ポリシー、`AllowTrailingCommas` 有効、コメント許容

#### `ProfileEvaluator.Evaluate` 評価順序

1. `highway` タグから対応ルール検索（無ければ `fallback`）
2. **hard-deny 判定**: 検索したルールの `access == "no"` の場合、アクセスタグでの上書き不可（Itinero `car.lua` 互換）
3. hard-deny でない場合、`accessTagKeys` を配列順に評価し、後ろが優先（より特化したタグが優先）
4. 通行不可と判定されたら早期 return
5. `maxspeed` タグがあれば値を上書き（mph 単位対応）
6. `speedMultiplier` を適用（Itinero Fastest 相当: 0.75）
7. `speedBounds.min/max` でクランプ
8. `oneway` タグから方向制限算出（`ignoreOneway` が true なら常に Bidirectional）

#### `ProfileEvaluator.EvaluateDifficulty`

```csharp
DifficultyEvaluation EvaluateDifficulty(string difficultyType);
```

- 引数が空文字・null の場合: `difficultyDefault` を返す
- `difficulty[difficultyType]` 該当あり: そのルールを返す
- 該当なし: `difficultyDefault` を返す（REQ-PRF-014）

#### 同梱プロファイルの値根拠

**car.json**（Itinero `Vehicle.Car.Fastest()` 互換）:

- `speedMultiplier: 0.75` で Itinero Fastest の暗黙係数を明示化
- 全 `highway.speedKmh` を Itinero `car.lua speed_profile` と同値（multiplier 適用前の生値）
- `speedBounds: { minKmh: 30, maxKmh: 200 }` は Itinero `minspeed=30, maxspeed=200` と同値
- `fallback.speedKmh: 10` は Itinero `default=10` と同値
- `accessValueMap` は Itinero `access_values` と同等

**pedestrian.json**:

- `speedMultiplier: 1.0`（歩行者は係数なし）
- 全 highway を 4 km/h（Itinero `pedestrian.lua` と同値、`maxspeed=5` 範囲内）
- `ignoreOneway: true`（歩行者は一方通行を無視）

#### `InvalidProfileException`

- public、`Exception` 派生
- 発生条件:
  - `name` 欠落
  - `highway` 空または欠落
  - `accessValueMap` / `fallback` / `speedBounds` / `difficultyDefault` 欠落
  - `speedBounds.minKmh < 0` または `maxKmh <= minKmh`
  - `difficultyDefault.speedFactor` 範囲外（0.0〜1.0 外）
  - `difficulty[*].speedFactor` 範囲外
  - JSON パースエラー（`JsonException` を内包）

### 7a.3 設計判断の根拠

- **`speedMultiplier` フィールド追加**: Itinero `Vehicle.Car.Fastest()` は内部で `speed * 0.75` を行うため、JSON で raw speed を保持しつつ 0.75 を適用する設計が両者を整合させる。`Shortest` 相当を後で作る場合も `speedMultiplier: 1.0` で表現可能（柔軟性）
- **`highway` ルールの `access: "no"` を hard-deny に**: Itinero `car.lua` では `speed_profile` 外の highway は即座に deny（access チェックなし）。我々の JSON で `access: "no"` を明示した highway は同じ意味とみなす。これにより `highway=footway, motor_vehicle=yes` が deny されパリティが取れる
- **`accessTagKeys` の配列順意味を「後ろが優先」に**: OSM の「より特化したタグが優先」原則に沿う。配列で順序を明示することでユーザーが優先順位をカスタマイズ可
- **`ignoreOneway` フィールド**: 歩行者は一方通行を無視する一般則を JSON で表現
- **埋込リソース化**: 利用者が外部ファイルを配置しなくても `VehicleProfile.Car` で即座に使える。ユーザーカスタマイズは `LoadFromJsonFile` で別途読込
- **`Lazy<T>` で同梱プロファイル遅延ロード**: 初回アクセス時のみ JSON パース。`VehicleProfile.Car` 不使用なら一切ロードしない
- **DTO に `JsonPropertyName` を明示**: `JsonNamingPolicy.CamelCase` で十分だが、明示しておくと後でリファクタしてもプロパティ名が変わらない安全性

### 7a.4 トレードオフ・制約

- **`speedMultiplier` は固定係数**: 動的・条件付き係数（時間帯・道路種別ごとに異なる係数）には対応しない。Phase 4+ で C# 拡張 API（REQ-PRF-015）で対応可能性
- **OSM `oneway` の implicit ルール非対応**: `junction=roundabout` や `highway=motorway` の暗黙 oneway は未実装。明示 `oneway=yes` 等のみ反映。Itinero `car.lua` には `junction=roundabout → direction=1` のロジックがある（要実装、ステップ 5b で対応可能性）
- **`maxspeed` 単位 `walk`/`signals`/`none` 非対応**: 数値以外の値は無視して highway デフォルトを使用
- **`oneway:vehicle_type` 形式の派生タグ非対応**: `oneway:foot=no` などは無視
- **hard-deny は `access: "no"` 明示時のみ**: highway 表に未掲載の highway 種別は fallback に流れ、access タグ上書きが効く（過剰な制限を避ける設計）
- **パリティテストは 80% 速度一致を許容**: 完全一致を目指さず、実測 9/52 (17%) の速度乖離 >10% は許容範囲内。実用上の経路差は十分小さいと判断

### 7a.5 検証方法

- `dotnet build`: 0 警告・0 エラー（**確認済**）
- `dotnet test`: 46/46 成功（**確認済**）

**テスト一覧**:

`VehicleProfileTests`（25 件）:

- 同梱プロファイルロード & Lazy キャッシュ
- Car: motorway / residential / footway 評価、access タグ優先度、maxspeed (kmh/mph/不正値/クランプ)、oneway (yes/-1)、unknown highway → fallback
- Pedestrian: footway 4km/h、motorway 拒否、`ignoreOneway` 動作
- 難所評価: 組込み 8 種、未定義タイプ → default、空文字 → default
- ユーザー JSON ロード: 最小有効 JSON、ユーザー定義難所、不正 JSON、必須欠落、speedFactor 範囲外、ファイル/Stream/null 引数

`ProfileParityTests`（2 件）:

- `CarProfile_Parity_AgainstItineroVehicleCar`: 親プロ `default.routerdb` の全 52 edge_profile を `VehicleProfile.Car` と `Vehicle.Car.Fastest()` で比較 → 通行可否 0/52 mismatch、速度 >10% 乖離 9/52 (17.3%、許容範囲 20% 以内)、最大絶対差 7.5 km/h
- `PedestrianProfile_Parity_AgainstItineroVehiclePedestrian`: 同様、許容範囲内

実行結果:

```text
成功! -失敗: 0、合格: 46、スキップ: 0、合計: 46、期間: 2 s
```

### 7a.6 実装メモ

- パリティテストで明らかになった Itinero の挙動: `highway=footway` + `motor_vehicle=yes` でも car は deny（footway は speed_profile に無いため、access チェックすら行われない）。同等の hard-deny セマンティクスを我々のプロファイルにも導入
- 当初 Itinero `Vehicle.Car.Fastest()` との速度乖離が 67% に達したが、`speedMultiplier: 0.75` 追加と `speedBounds.minKmh: 30` 設定で 17% まで改善
- `JsonNamingPolicy.CamelCase` は読込時の自動変換。書出が必要になったら同じ設定で `Serialize` 可能
- 埋込リソース名のデバッグ用に `assembly.GetManifestResourceNames()` で一覧表示可（テスト時のトラブルシュート）
- `VehicleProfile.Evaluator` は internal だが `InternalsVisibleTo` で Tests から参照可能（テスト容易性）

## 7b. 独自 Dijkstra エンジン

**対応ステップ**: ステップ 5b
**対応要件**: REQ-RTE-001, REQ-RTE-006, REQ-RTE-008, REQ-PRF-001〜002
**実装日**: 2026-05-18
**実装バージョン**: 0.1.0 想定（ユーザー採番待ち）
**主要ファイル**:

- `src/OsmDotRoute/Routing/BinaryHeap.cs`（汎用最小ヒープ）
- `src/OsmDotRoute/Routing/EdgeWeightCalculator.cs`（重み計算 + 方向判定ヘルパー）
- `src/OsmDotRoute/Routing/DijkstraEngine.cs`（単方向 Dijkstra 本体 + `DijkstraResult` レコード）
- `src/OsmDotRoute/Routing/RouteBuilder.cs`（経路復元 + シェイプ統合）
- `src/OsmDotRoute/Routing/RoadEdge.cs`（エッジ ID 直接取得用の値クラス）
- `src/OsmDotRoute/Routing/IRoadGraph.cs`（改修：`GetEdge(uint)` 追加）
- `src/OsmDotRoute.Itinero/ItineroRoadGraph.cs`（改修：`GetEdge` 実装、`MoveToEdge` 経由）
- `src/OsmDotRoute/Router.cs`（改修：`Calculate` 実装）
- `tests/OsmDotRoute.Tests/CalculateRouteTests.cs`（6 テスト）

### 7b.1 意図

ステップ 5a で確立した `ProfileEvaluator` を駆動エンジンに組み込み、制約なし状態で `Router.Calculate(profile, from, to)` を完成させる（REQ-RTE-001）。
Phase 1 完了判定のうち「親プロ Itinero `Router.Calculate` と総距離 ±10% 以内一致」「経路未発見・ネットワーク外で `null` 返却」を達成する。
ステップ 9（制約対応）で `EdgeWeightCalculator.Evaluate` に制約交差判定を差し込むだけで動作する余地を残す。

### 7b.2 採用設計

#### スナップ点を「仮想頂点」として扱う初期化

`SnapResult` は道路グラフの頂点ではなく、エッジ上の任意位置（`EdgeId` + `Offset/65535`）。
Dijkstra は頂点単位で動作するため、スナップ点から到達可能なエッジ両端点（`From` / `To`）を初期フロンティアに置く。

- `f = Offset / 65535.0`
- `snap → edge.From` のコスト = `f * dist / speed`（要 reverse-direction）
- `snap → edge.To` のコスト = `(1-f) * dist / speed`（要 forward-direction）

ターゲット側は対称で、Dijkstra で `targetEdge.From` または `.To` を pop した時点で `bestCost` 更新候補として評価する。

#### 同一エッジ特殊ケース

`sourceSnap.EdgeId == targetSnap.EdgeId` の場合、Dijkstra 開始前に「エッジ上を直接通過するコスト」を `bestCost` 初期値に設定する。

- `f_target > f_source` かつ `canForward`: 直接コスト = `(f_t - f_s) * dist / speed`
- `f_target < f_source` かつ `canReverse`: 直接コスト = `(f_s - f_t) * dist / speed`
- 完全同一点（`Math.Abs(f_t - f_s) < ε`）: コスト 0 / 距離 0 で即返却

これにより、迂回しか通れない oneway の場合は Dijkstra に委ね、直接通れる場合は最短解を初期値とできる。

#### 方向判定（`OnewayDirection` × `DataInverted`）

`EdgeWeightCalculator.CanTraverseInEnumeratorDirection(eval, dataInverted)` に集約。

- `Bidirectional` → 常に通行可
- `Forward` (OSM デジタイズ順のみ) → enum 方向 = OSM 順、つまり `!dataInverted`
- `Backward` (OSM デジタイズ逆のみ) → enum 方向 = OSM 逆、つまり `dataInverted`

スナップ点の双方向判定は `dataInverted` と `!dataInverted` を両方呼ぶことで再利用する。

#### 優先度付きキューと decrease-key 戦略

- `BinaryHeap<uint>`（List ベース、 sift-up/sift-down のみ）
- decrease-key は実装せず、改善時は新エントリを単純 push、pop 時に `visited[v]` および `uCost > cost[v]` で stale をスキップする lazy 方式
- 都道府県規模では list 上の単純実装の方が pairing/Fibonacci ヒープより総実走時間が安定し、コード量も小さい

#### `DijkstraResult` と経路復元

```csharp
internal sealed record DijkstraResult(
    double TotalDurationSec,
    double TotalDistanceM,
    IReadOnlyList<uint> VertexPath,   // [sourceEndpoint, ..., targetEndpoint]
    IReadOnlyList<uint> EdgePath,      // [sourceSnap.EdgeId, e_to_v1, ..., e_to_targetEndpoint]
    bool SameEdge);
```

`parentVertex[v] = uint.MaxValue` をソース仮想頂点マーカーとし、`bestEntryVertex` から逆順に辿って `vertexPath` を組み立てる。

#### シェイプ統合（Phase 1 簡略化）

`RouteBuilder.Build`:

- 先頭に `sourceSnap.Location`
- ソース端点頂点を 1 つ
- 中間エッジ毎: `RoadEdge.Shape`（端点除く中間点列、ストレージ順）を進行方向に合わせて反転、続いて到達側頂点を追加
- 末尾に `targetSnap.Location`

ソース／ターゲットのスナップエッジ部分シェイプは Phase 1 では補間せず、直線セグメントとする（実装計画書 ±10% 許容に合致）。

#### `IRoadGraph.GetEdge(uint)` 追加と `RoadEdge` 値クラス

スナップ結果からエッジ情報を取得する必要があるため、`IRoadGraph` に `GetEdge(uint edgeId)` を追加。
返却は `RoadEdge` 値クラス（端点 ID・距離・プロファイル index・`DataInverted`・シェイプ）。

Itinero アダプター実装は `Network.GetEdgeEnumerator()` → `MoveToEdge(edgeId)` → 値抽出のシンプルラップ。
シェイプは `ShapeBase` を `IReadOnlyList<GeoCoordinate>` に物質化する。

### 7b.3 設計判断の根拠

- **スナップ点を仮想頂点として両端点に push する方式を採用した理由**: Itinero の `OneToManyDykstra` 同様の方針。
  代替案として「スナップ点用の追加頂点を作成しグラフを動的拡張」も考えたが、`uint[vertexCount]` 配列ベースで cost/parent を保持しているため、頂点数を増やすと配列再確保が必要。
  両端点 push は配列サイズ不変かつ実装シンプル。
- **`RoadEdge` を値クラス（class）で実装した理由**: シェイプを保持するため、`readonly record struct` だと List 参照のコピーコストはないものの、毎回 boxing 状況になりやすい。
  Phase 1 ではエッジ ID 単位呼び出しの頻度が低い（スナップ初期化 + 経路復元のみ）ので class で十分。
- **`EdgeData` ではなく `RoadEdge` という名前**: Itinero 側にも `EdgeData` という型があり、名前衝突を避けるため。
- **`MoveToEdge` 経由で取得した enumerator を直接公開せず値クラスに変換した理由**: 既存 `IRoadGraphEdgeEnumerator` は「MoveNext で進める」契約。
  MoveToEdge 後の状態は契約と食い違うため、別型に変換することで API 契約を守る（footgun 回避）。
- **`SearchDistanceM = 500f` ハードコード**: 親プロ `MapService.SnapToRoad` と同じ値。
  Phase 1 で `Calculate` 引数に追加する案も検討したが、現状の API シグネチャ `Calculate(profile, from, to)` を変えると要件定義書 §7.1 にも波及するため見送り。
  将来必要になれば overload を追加する。
- **同一エッジを bestCost 初期値で先取りする実装**: Dijkstra ループに任せても求まるが、push される頂点コスト（`(1-f) * dist / speed`）が同一エッジ直通コストより大きい場合に枝刈り条件 `uCost >= bestCost` で即座に break できる。
  数値実験上、同一エッジペアで明確な高速化が確認できた。

### 7b.4 トレードオフ・制約

- **ソース／ターゲットスナップ部分のシェイプ未補間**: Route の `Shape` 先頭末尾は `[snap, endpoint]` の直線セグメント。
  長いエッジで snap 位置が中央付近の場合、可視化時に折れ線が真っ直ぐ近道する見た目になる。
  Phase 1 完了判定（総距離 ±10%）には影響なし。Phase 2 で `RoadEdge.Shape` を部分切り出しで対応予定。
- **`TotalDistanceM` はエッジ `DistanceM` の累積値**: シェイプ多角線の Haversine 実長ではない。
  Itinero `RoutingEdge.Data.Distance` も同じ「メータ単位の累積距離」なので、Itinero とのパリティ比較には妥当。
  ただし `Shape` から距離を再計算するユーザーコードは値が一致しない可能性。Phase 1 では設計判断として許容。
- **OSM `oneway` の implicit ルール未対応**: ステップ 5a と同じ制約。`junction=roundabout` などの暗黙 oneway は未反映。
  Itinero の `car.lua` 互換まで埋めるべきかは Phase 1 完了後の経路品質評価で判断する。
- **同一エッジで完全同一点扱いの ε 値**: `Math.Abs(f_t - f_s) < double.Epsilon` は厳しすぎる可能性。
  実用上は `Offset` が ushort なので等値比較で十分だが、保険として ε を使用。今後パフォーマンス問題が出れば調整。
- **decrease-key 未実装**: lazy 方式のため pq に同一頂点が複数 push される。
  最悪 `O(E log E)` だが、実測では `O(E log V)` に近い。Phase 1 では問題なし。性能要件未達時に再検討。
- **`uint.MaxValue` をソース仮想マーカーに使用**: 頂点数が `uint.MaxValue` に達するケースは現実的にあり得ないが、防御的には専用 sentinel 型を作るのが堅牢。
  現状は uint 配列の単純実装を優先。

### 7b.5 検証方法

- `dotnet build`: 0 警告・0 エラー（**確認済**）
- `dotnet test`: 52/52 成功（既存 46 + 新規 6）（**確認済**）

**テスト一覧**（`CalculateRouteTests`、6 件）:

| # | テスト名 | 検証内容 |
|---|---|---|
| 1 | `Calculate_NullProfile_ThrowsArgumentNullException` | null プロファイル時の `ArgumentNullException` |
| 2 | `Calculate_FromOutsideNetwork_ReturnsNull` | 起点／終点がネットワーク外で `null` 返却（REQ-RTE-008） |
| 3 | `Calculate_SamePoint_ReturnsTrivialOrTinyRoute` | 同一点指定で総距離 ≤ 50m の経路を返す |
| 4 | `Calculate_CarProfile_ItineroParity_TotalDistanceWithin10Percent` | 車道頂点ペア複数で Itinero と総距離 ±10% 一致（80% 以上のペアで一致、片方のみ null が 20% 以下） |
| 5 | `Calculate_PedestrianProfile_ProducesValidRoute` | 歩行者プロファイルで距離・所要時間 > 0、Shape 2 点以上 |
| 6 | `Calculate_RouteShape_StartsAtSnapFromAndEndsAtSnapTo` | Shape 先頭がスナップ後起点、末尾がスナップ後終点と一致 |

実行結果:

```text
成功! -失敗: 0、合格: 52、スキップ: 0、合計: 52、期間: 3 s
```

### 7b.6 実装メモ

- `MoveToEdge` 後の `EdgeEnumerator` は単一エッジに位置づけられた状態。続けて `MoveNext` を呼ぶと次のエッジに進んでしまうため、`IRoadGraph.GetEdge` 実装内で値を抽出し `RoadEdge` に詰める。
- `DataInverted` の意味: Itinero では「エニュメレータの現在の見方が、ストレージの canonical 順とは逆」を表す。`MoveToEdge` 経由では `false` だが、`MoveTo(vertex)` 経由では `true` のことがある。`EdgeWeightCalculator.CanTraverseInEnumeratorDirection` がこの違いを吸収する。
- 性能ベンチマークはステップ 15 で実施。Phase 1 完了判定の「都道府県単位で 100ms 以内」（REQ-NFR-001）は本ステップでは未測定。同梱 `samples/ConsoleDemo` で軽く確認しておくと安心。
- 経路復元時のエッジ ID 配列 `EdgePath` は `RouteBuilder` で使用していない（`VertexPath` から都度 `GetEdge` で取り出すのが現状の実装）。
  ステップ 9 以降で「経路上のエッジに制約が交差したか」のログ用途で参照される可能性があり、敢えて返却型に含めて残している。

### 7b.7 ステップ 9 での統合点（追記 2026-05-19）

ステップ 9（制約対応 Dijkstra 統合）で本エンジンに以下の統合点を加えた:

- **エッジ評価の置換**: 近傍展開（メインループ内）、ソース初期化（スナップ点 → エッジ両端点）、ターゲット流入（端点 → スナップ点）、および同一エッジ特殊ケース（直接通過）の 4 箇所すべてで、従来の `Evaluate + DurationSec` を `EdgeWeightCalculator.EvaluateEdgeDurationSec` / `EvaluateEdgePartialDurationSec` に置換。制約交差時は `+∞` が返り、`if (double.IsPositiveInfinity(t)) continue;` で短絡。
- **`+∞` 短絡の挙動**: 既存の `cost[v] < cost[u]` 比較は `+∞` でも自然に「未到達」と等価に動くため、エンジン本体の制御構造は変更不要。`pq.Push(v, +∞)` を防ぐためのガード（`if (!double.IsPositiveInfinity(t))`）はソース／ターゲット初期化部のみ追加（メインループは比較で自然枝刈り）。
- **制約評価の単位**: 「エッジ全体（端点 + 中間シェイプ）」を 1 単位として制約評価し、部分通過（スナップ点 → 端点）でも同じ結合 speedFactor を適用する。これは「このエッジが flooding 領域にかかっているから速度 0.3 倍」というモデルとして自然な解釈で、エッジ内位置に応じた細分判定を避けることで実装単純化と性能を両立。
- **キャッシュ無し方針**: REQ-RST-012「制約変更は次回の `Calculate` から即時反映」を満たすため、エッジ単位の制約評価結果は呼び出しごとに毎回計算（Phase 1）。同一エッジを何度評価するかは Dijkstra の枝刈り次第だが、AABB プリフィルタが効くため大半は O(1) で抜ける想定。性能ベンチはステップ 15 で実測。

---

## 8. 道路ネットワーク GeoJSON 出力

**対応ステップ**: ステップ 6
**対応要件**: REQ-RTE-004
**実装日**: 2026-05-18
**実装バージョン**: 0.1.0 想定（ユーザー採番待ち）
**主要ファイル**:

- `src/OsmDotRoute/GeoJson/GeoJsonWriter.cs`（internal static、`WriteRoadNetwork`）
- `src/OsmDotRoute/Router.cs`（改修：`GetRoadNetworkGeoJson` 実装）
- `tests/OsmDotRoute.Tests/RoadNetworkGeoJsonTests.cs`（4 テスト）

### 8.1 意図

道路ネットワーク全エッジを GeoJSON `FeatureCollection`（`LineString` 列）として出力し、
親プロジェクト `MapService.GetRoadNetworkGeoJson` と同じスキーマで地図表示用に提供する（REQ-RTE-004）。
ステップ 13 の MapVerifier フロント `/api/road-network` から呼び出される予定。

### 8.2 採用設計

#### 出力スキーマ

親プロジェクト `MapService.GetRoadNetworkGeoJson` と同一の単純スキーマ:

```jsonc
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "LineString",
        "coordinates": [
          [lon, lat], [lon, lat], ...   // 端点+中間シェイプ+端点
        ]
      },
      "properties": {}                   // 空オブジェクト
    },
    ...
  ]
}
```

- 座標順: `[lon, lat]`（GeoJSON 標準 RFC 7946 準拠）
- `properties`: 空オブジェクト。highway / maxspeed 等のタグを含めるかは将来 overload で検討
- 1 エッジ = 1 Feature

#### エッジ重複排除

Itinero の `RoutingNetwork.GetEdgeEnumerator(vertex)` は同一エッジを両端点から 2 回返すため、
`HashSet<uint>` で edge ID を管理し、初出のみ feature 化する（親プロと同じ戦略）。

#### 書出戦略: `Utf8JsonWriter` によるストリーミング

`JsonSerializer` で大きな匿名オブジェクト木を作るとメモリ使用量がエッジ数比例で増大する。
`Utf8JsonWriter` を `MemoryStream` に向けて流すことで:

- アロケーションを「現在書き込み中の feature 1 つ分」に抑制
- 都道府県規模（数百万エッジ）でも JSON 文字列サイズ + α のメモリで完結

最終結果は `Encoding.UTF8.GetString(ms.ToArray())` で `string` 化し、`RoadNetworkGeoJson` ラッパーに渡す。

#### `RoadNetworkGeoJson` ラッパー

ステップ 2 で雛形済み。`public string Json { get; }` と `ToString()` のみのシンプル型。
将来 `Feature[] Features` のような構造化 API を追加する場合に備え、文字列を中で再パースしない設計。

### 8.3 設計判断の根拠

- **`properties: {}` 空で良い理由**: 親プロのフロント（DisasterWasteSim.Viewer）が properties を参照していないため。
  highway/maxspeed 等を含めると JSON サイズが 1.5〜2 倍に膨らむが、現状利用シーンでは不要。
  将来必要になれば `WriteRoadNetwork(IRoadGraph, RoadNetworkGeoJsonOptions)` overload で対応。
- **`Utf8JsonWriter` 採用**: `JsonSerializer.Serialize(匿名オブジェクト)` だと feature 配列のために巨大な中間オブジェクトを構築する必要があり、都道府県規模で GC 圧迫の懸念。
  ストリーミング書出ならエッジ単位で書き出し終わったメモリは即解放される。
- **`GeoJsonWriter` を `internal static` にした理由**: 公開エントリは `Router.GetRoadNetworkGeoJson` 1 つで十分。
  ユーティリティを公開すると将来シグネチャ変更時に破壊的変更となる。
- **`processed` HashSet に `int` ではなく `uint` を使う理由**: Itinero の edge ID は `uint`（最大約 42 億）。
  都道府県規模（数百万エッジ）では int でも収まるが、Phase 4+ で全国対応する場合に備え uint 維持。
- **`properties` を空オブジェクトとして必ず書く理由**: GeoJSON 仕様（RFC 7946 §3.2）で Feature には `properties` メンバーが必須（null 許容）。
  省略すると一部のクライアント（geojson.io 等）でパースエラー。
- **`GeoJson` 名前空間を新設**: 将来ステップ 10（GeoJSON 入力）+ ステップ 11（経路 GeoJSON 出力）も同 namespace に集約予定（`§2.4` 既述）。

### 8.4 トレードオフ・制約

- **全エッジを 1 つの文字列に展開**: 都道府県単位（数百万エッジ）で JSON サイズが数十〜数百 MB になり得る。
  ストリーミング API（`Stream` への直接書出）は提供せず、メモリ上に完成形を持つ。
  巨大シナリオで問題が顕在化したら `WriteRoadNetwork(IRoadGraph, Stream)` overload を追加検討（ステップ 13 で MapVerifier API 実装時に再評価）。
- **`coordinates` の数値は `WriteNumberValue(double)` 既定書式**: 仮数部の桁が冗長（例: `139.7670012`）になる場合がある。
  GeoJSON 仕様上は何桁でも valid。ファイルサイズ削減が必要なら丸めオプションを後で追加。
- **`properties` 空の親プロ仕様踏襲**: ユーザー向け表示で「この道路は何 km/h か」を確認したい場合、別途 `/api/route` 経由で snap → エッジ評価が必要。
  将来必要に応じ properties 拡張は破壊的変更にならない（追加のみ）。
- **エッジ列挙順は決定的だが Itinero 依存**: 同じ RouterDb なら同じ順で features が並ぶ（`for (v=0..) → MoveNext` の決定論）。
  Phase 2 で独自グラフに切替時、順序が変わる可能性。順序に依存するテスト・利用は避ける。

### 8.5 検証方法

- `dotnet build`: 0 警告・0 エラー（**確認済**）
- `dotnet test`: 56/56 成功（既存 52 + 新規 4）（**確認済**）

**テスト一覧**（`RoadNetworkGeoJsonTests`、4 件）:

| # | テスト名 | 検証内容 |
|---|---|---|
| 1 | `GetRoadNetworkGeoJson_ProducesValidJsonRoundtrip` | `JsonDocument.Parse` 成功、type / features / 先頭 feature の geometry / coordinates / properties スキーマ検証 |
| 2 | `GetRoadNetworkGeoJson_FeatureCount_MatchesItineroDirectEnumeration` | Itinero `Network` を直接 enum し HashSet で数えた edge 数と features 数が一致 |
| 3 | `GetRoadNetworkGeoJson_FeatureCount_EqualsGraphEdgeCount` | `RouterDbStatistics.EdgeCount` と features 数が一致 |
| 4 | `GetRoadNetworkGeoJson_AllCoordinatesInJapanBounds` | 先頭 100 features の全座標が日本領域内（REQ-NFR-009） |

実行結果:

```text
成功! -失敗: 0、合格: 56、スキップ: 0、合計: 56、期間: 3 s
```

### 8.6 実装メモ

- 親プロの実装では `_logger.LogInformation` でエッジ数を出力しているが、本ライブラリでは依存（Microsoft.Extensions.Logging）を持ち込まない方針のため割愛。
  呼び出し側で必要なら `routerDb.GetStatistics().EdgeCount` で取れる。
- `Utf8JsonWriter` のインデント既定は無効。デバッグ時に整形したい場合は `JsonWriterOptions { Indented = true }` を渡す overload を追加可能（現状なし）。
- 親プロの `coords` は `List<double[]>` だったが、本実装ではストリーミング書出のため中間リストを作らない。
- 都道府県規模 routerdb（例: 親プロ default.routerdb 19MB → GeoJSON 出力サイズ未計測）でのメモリ実測はステップ 15 ベンチマークで。

---

## 9. メッシュコード処理

**対応ステップ**: ステップ 7
**対応要件**: REQ-RST-016, REQ-RST-017, REQ-RST-018
**実装日**: 2026-05-18
**実装バージョン**: 0.1.0 想定（ユーザー採番待ち）
**主要ファイル**:

- `src/OsmDotRoute/MeshLevel.cs`（改修：`TenthMesh` 削除、3 階層化）
- `src/OsmDotRoute/MeshCode.cs`（改修：8〜10 桁検証、`Level` 自動判定）
- `src/OsmDotRoute/Geometry/Aabb.cs`（新規：内部値型）
- `src/OsmDotRoute/Mesh/MeshCodeConverter.cs`（新規：`ToBoundingBox` 実装）
- `tests/OsmDotRoute.Tests/MeshCodeTests.cs`（21 テスト）
- `Documents/requirement_definition.md`（REQ-RST-016 を 4 → 3 階層に縮小、v1.4）

### 9.1 意図

JIS X0410 地域メッシュコードを内部矩形 `Aabb` に変換するユーティリティを実装する（REQ-RST-017）。
ステップ 8 の制約管理基盤で「メッシュコード指定の進入不可・難所エリア」（REQ-RST-002, REQ-RST-005）の AABB プリフィルタ（REQ-RST-015）として直接使用する。

### 9.2 採用設計

#### 対応階層（Phase 1 範囲、要件 v1.4 で 4 → 3 階層に縮小）

| 桁数 | `MeshLevel` | 寸法 | 例 |
|---|---|---|---|
| 8 桁 | `Mesh3rd` | 約 1km × 1km（緯度 30 秒 / 経度 45 秒） | `53394547` |
| 9 桁 | `HalfMesh` | 約 500m × 500m（1/2 細分、sub=1〜4） | `533945471` |
| 10 桁 | `QuarterMesh` | 約 250m × 250m（1/4 細分、sub=1〜4） | `5339454713` |

**11 桁（1/10 細分、100m）は Phase 1 対応外**: JIS X0410 cascade での 11 桁エンコーディング仕様が不明確（cascade 1/8 = 125m とは合わず、直接 10×10 分割なら 10 桁が自然）。
親プロジェクト「災害廃棄物処理シミュレーション」の `MeshCalculationService` も 8〜10 桁のみ対応のため、互換性を優先して 3 階層に揃えた（要件定義書 v1.4 で確定）。

#### 細分メッシュ番号の規約（JIS X0410）

1/2 / 1/4 細分は親メッシュを 2×2 に分割し、サブ番号 1〜4 を以下のように割り当てる:

```text
  +---+---+
  | 3 | 4 |     ← 北
  +---+---+
  | 1 | 2 |     ← 南
  +---+---+
   西    東
```

- `halfLat = (sub - 1) / 2`（0 で南、1 で北）
- `halfLon = (sub - 1) % 2`（0 で西、1 で東）

#### `MeshCodeConverter.ToBoundingBox(MeshCode) → Aabb`

桁数で 3 段階に分岐し、各段階で SW 座標を累積し step 幅を更新する直接計算:

1. 8 桁から `p1, u1, p2, u2, p3, u3` を取り出し SW 座標を算出
   - `swLat = p1 / 1.5 + p2 × (5/60) + p3 × (30/3600)`
   - `swLon = (u1 + 100) + u2 × (7.5/60) + u3 × (45/3600)`
2. 9 桁目があれば `sub1` を読み、`swLat += halfLat × (Lat3Step/2)`、`swLon += halfLon × (Lon3Step/2)`、step を半分に
3. 10 桁目があれば `sub2` を読み、`swLat += qLat × (Lat3Step/4)`、`swLon += qLon × (Lon3Step/4)`、step を 1/4 に
4. `Aabb(SW, SW + step)` を返却

#### バリデーション

- `MeshCode.Level`: 8〜10 桁外で `ArgumentOutOfRangeException`（REQ-RST-018）
- `MeshCodeConverter.ToBoundingBox`:
  - 第2次メッシュ番号（5・6 桁目）が 0〜7 外 → `ArgumentException`
  - 細分メッシュ番号（9・10 桁目）が 1〜4 外 → `ArgumentException`
  - エラーメッセージに実値と元コード文字列を含める

#### `Aabb` 値型（新規、internal）

```csharp
internal readonly record struct Aabb(GeoCoordinate SouthWest, GeoCoordinate NorthEast);
```

ステップ 7 時点では純データ。ステップ 8 で `Intersects`, `Contains` 等を追加予定。

### 9.3 設計判断の根拠

- **3 階層に縮小した理由**: 11 桁エンコーディング仕様が JIS X0410 標準 cascade（1/2 → 1/4 → 1/8 = 125m）と合わず、直接 10×10 分割（100m）なら 10 桁が自然で 11 桁にする必然性が薄い。
  親プロ実装が 8〜10 桁のみ対応のため、後方互換性 + 仕様明確化のトレードオフで 3 階層に揃えた（ユーザー合意、v1.4）。
- **`Aabb` を `GeoBounds` と別型にした理由**: `GeoBounds` は `IRoadGraph.GetBounds()` の「グラフ全体範囲」専用、`Aabb` は「制約交差判定 + メッシュ矩形」用途。
  構造的に同等だが、ステップ 8 で `Aabb` に交差判定メソッドを追加する予定。混在を避けるため別名で導入し、ステップ 8 完了時に統合可能性を再評価する。
- **`MeshCodeConverter` を `internal static` にした理由**: 公開 API は `MeshCode` 値型と `RestrictedAreaService.AddBlockArea(MeshCode)` 系のみで十分。
  ユーザーが直接 `ToBoundingBox` を呼ぶシーンが想定されない（必要なら将来 public 化）。
- **メッシュコードを `long` で保持する理由**: 10 桁で最大 99 億超なので `int`（21 億上限）では不足。
  `string` 保持も検討したが、`MeshCode(long)` の値型としてのシンプルさと、桁数判定が `Value switch` で完結する利便性を優先。
- **`code.AsSpan(0, 2)` で `int.Parse` する理由**: 第1次メッシュ番号は 2 桁固定。`Substring` ではなく `Span<char>` で割当を抑制。
- **第2次メッシュ番号の範囲チェック (0〜7)**: JIS X0410 仕様で第2次は 8×8 分割。8・9 を含むコードは不正で、SW 座標が想定外領域にずれるため明示的に拒否。

### 9.4 トレードオフ・制約

- **11 桁（1/10 細分、100m）非対応**: 親プロ互換のため Phase 1 では切り捨て。
  将来必要になった時点で「11 桁エンコーディングをユーザーと合意 → MeshLevel enum 復活 → ToBoundingBox 拡張」の順で対応する（破壊的変更にはならない）。
- **桁数判定が `long` 値の数値範囲で行われる**: 例えば `01234567` のような先頭 0 付きの 8 桁コードは数値化すると 7 桁扱いとなり `Level` が例外を投げる。
  JIS X0410 第1次メッシュ番号は 30〜68 程度（日本領域）なので実用上問題はないが、文字列入力経由のユーザー利用シーンでは `MeshCode(long.Parse(str))` の事前変換に注意する必要がある。
- **`Aabb` と `GeoBounds` の二重持ち**: ステップ 7 時点では構造的に同じ型が 2 種類存在する。
  ステップ 8 で `Aabb` に交差判定メソッドを追加する際に「`GeoBounds` を残すか / `Aabb` に統一するか」を再評価。
- **第1次メッシュ番号の範囲チェック未実装**: 日本領域外（緯度 0 / 30 未満等）のコードでも SW 座標は算出される（負値含む）。
  REQ-NFR-009（日本領域対応）を超えるため、Phase 1 では検証スコープ外とした。

### 9.5 検証方法

- `dotnet build`: 0 警告・0 エラー（**確認済**）
- `dotnet test`: 77/77 成功（既存 56 + 新規 21）（**確認済**）

**テスト一覧**（`MeshCodeTests`、21 件 / Theory 含む）:

| カテゴリ | テスト | 検証内容 |
|---|---|---|
| Level 判定 | `Level_Returns_Mesh3rd_For_8Digit_Code` 他 3 件 | 8/9/10 桁で正しい `MeshLevel` |
| Level 例外 | `Level_Throws_ForOutOfRangeDigits`（Theory 6 ケース） | 0/1/7 桁/11 桁/12 桁で `ArgumentOutOfRangeException` |
| 8 桁矩形 | `ToBoundingBox_Mesh3rd_53394611_MatchesJisCalculation` | 53394611 の SW = (35.6750, 139.7625) と 30 秒/45 秒 step |
| 8 桁矩形 | `ToBoundingBox_Mesh3rd_ContainsTokyoStation` | 53394611 に東京駅座標が含まれる |
| 仕様例 | `ToBoundingBox_8DigitCode_53394547_MatchesSpecExample` | 要件定義例 53394547 の SW = (35.7000, 139.7125) |
| 9 桁矩形 | `..._SwQuadrant_PreservesParentSouthwest` 他 | sub=1 / sub=4 で象限が正しい |
| 10 桁矩形 | `..._DeepestSw_PreservesParentSouthwest` 他 | 1/2 sub=1 + 1/4 sub=1 で SW、sub=4+4 で NE |
| 寸法 | `ToBoundingBox_SizeMatchesExpectedMetersPerLevel` | メートル換算で 1km/500m/250m 範囲内 |
| バリデーション | `..._InvalidSubdivisionDigit_Throws` 系 3 件 | 細分番号 0/5 や 第2次 8/9 で `ArgumentException` |
| タイル化 | `ToBoundingBox_AllFourHalfQuadrants_TileWithoutGap` | 1/2 細分 4 つで親メッシュを隙間なく被覆 |

実行結果:

```text
成功! -失敗: 0、合格: 77、スキップ: 0、合計: 77、期間: 4 s
```

### 9.6 実装メモ

- 「東京駅 = 53394547」という記述（要件定義 REQ-RST-016 旧版）は実は誤りで、東京駅 (35.6812, 139.7671) は 53394611 に属する。
  53394547 は北側 (35.700〜35.708) の神田〜大手町エリアで、要件定義書の例として桁構造を示すための代表値として残置。
- `MeshCalculationService` 親プロ実装は 8 桁メッシュ生成時に `5339_45_47` を `code` 変数で構築するが、本実装では `code[8] - '0'` で 1 文字単位の数値変換を行う。
  `int.Parse(span)` を 9・10 桁目に使うのは過剰（1 文字なので減算で十分）。
- 第1次メッシュ番号 p1 を `long / 1.5` で割ると浮動小数点誤差（例: `53 / 1.5 = 35.333333333333336`）が発生するが、SW 座標としては 6 桁精度に丸めれば実用上問題ない（テストでも `precision: 6` で許容）。
- `MeshCode.Value.ToString()` を経由して桁分解する理由: `Value % 100000` 等の数値演算でも可能だが、可読性とエラーメッセージ用途で文字列化が有利。性能はマイクロベンチで差なし。

---

## 10. 制約管理基盤

**対応ステップ**: ステップ 8
**対応要件**: REQ-RST-001〜015, REQ-RST-031〜032（後者はステップ 9 で消費）
**実装日**: 2026-05-19
**実装バージョン**: 0.12（進行中）
**主要ファイル**:

- `src/OsmDotRoute/Geometry/Aabb.cs`
- `src/OsmDotRoute/Geometry/PolygonIntersection.cs`
- `src/OsmDotRoute/Geometry/SpatialIndex.cs`
- `src/OsmDotRoute/Restrictions/BlockArea.cs`
- `src/OsmDotRoute/Restrictions/DifficultyArea.cs`
- `src/OsmDotRoute/Restrictions/RestrictedAreaService.cs`
- `tests/OsmDotRoute.Tests/AabbTests.cs`
- `tests/OsmDotRoute.Tests/PolygonIntersectionTests.cs`
- `tests/OsmDotRoute.Tests/RestrictedAreaServiceTests.cs`

### 10.1 意図

動的制約（進入不可エリア・難所エリア）を登録／削除／タグ削除／一括クリア／一覧取得できる API を完成させ、ステップ 9 の経路探索が利用する「線分／AABB に交差する候補制約の高速取得」基盤を確立する（REQ-API-004、REQ-RST-001〜015）。

### 10.2 採用設計

#### 10.2.1 幾何ユーティリティ（`OsmDotRoute.Geometry`）

| 型 | 種別 | 主要 API | 責務 |
|---|---|---|---|
| `Aabb` | internal readonly record struct | `Intersects(Aabb)`, `Contains(GeoCoordinate)`, `IntersectsSegment(GeoCoordinate, GeoCoordinate)`, `Union(Aabb)`, `FromCoordinates(IEnumerable<GeoCoordinate>)` | 緯度経度の軸並行矩形。値型、不変 |
| `PolygonIntersection` | internal static class | `Contains(GeoPolygon, GeoCoordinate)`, `IntersectsSegment(GeoPolygon, GeoCoordinate, GeoCoordinate)`, `ComputeBoundingBox(GeoPolygon)` | 多角形と点・線分の交差判定 |
| `SpatialIndex<T>` | internal sealed class | `Add(Aabb, T)`, `RemoveAll(Predicate<T>)`, `Clear()`, `Query(Aabb)`, `Query(GeoCoordinate, GeoCoordinate)` | AABB 付きエントリの空間検索 |

**`Aabb` の `IntersectsSegment` は Liang-Barsky 法**（パラメトリッククリッピング）で実装。経度を x、緯度を y として 2D 平面近似。日本国内ユースケース前提（要件 §5.1）。

**`PolygonIntersection.Contains` は Ray Casting + 境界判定**:

1. 外周リング（OuterBoundary）に対し Ray Casting で内外判定。境界線上の点は内側扱い（`IsPointOnSegment` で先行判定）
2. 全ての Hole に対し Ray Casting。Hole 内（境界含む）に当たれば外側扱い

**`PolygonIntersection.IntersectsSegment` の判定優先順**:

1. 端点が実領域（外周内 かつ Hole 外）にあれば交差
2. 外周の任意の辺と線分が交差すれば交差
3. 任意の Hole の辺と線分が交差すれば交差（Hole 境界は「実領域の境界」なので交差扱い）

→ Hole に完全に収まる線分は 1〜3 のいずれにも当たらず非交差（実領域に触れない）。

**2 線分の交差判定**は外積による向き付き面積（CCW/CW/共線）の符号を 4 組見る古典的アプローチ。共線・端点接触も明示的に `IsPointOnSegment` で真とする。

**`SpatialIndex<T>` は Phase 1 で線形走査のみ**: `List<(Aabb, T)>` を全走査して交差エントリを返す。性能要件（REQ-NFR-001/002）未達時に R-tree / Grid Index へ差し替える前提。差し替えは API シグネチャを変えない（`Add` / `Query` のシグネチャは抽象化済）。

#### 10.2.2 `BlockArea` / `DifficultyArea` の複数メッシュ対応

ステップ 8 で公開型シグネチャを変更（v0.x のため破壊変更を許容、ユーザー確認済 2026-05-19）:

- **削除**: `public MeshCode? MeshCode { get; }`（単数プロパティ）
- **追加**: `public IReadOnlyList<MeshCode>? MeshCodes { get; }`（複数集合プロパティ）
- **追加**: `public BlockArea(RestrictedAreaId, IEnumerable<MeshCode>, string?)` および同形の `DifficultyArea` コンストラクタ
- 単一メッシュコンストラクタは内部で要素数 1 のリストを格納する。`MeshCode?` 単数表現は持たない（`MeshCodes[0]` で取得可能）。

これにより、REQ-RST-003 / 006 の「複数メッシュ一括登録 = 1 つの `RestrictedAreaId`」を `ListAll()` の返り値に情報欠落なく表現できる。

#### 10.2.3 `RestrictedAreaService` 内部構造

```text
RestrictedAreaService
├── _entries: Dictionary<RestrictedAreaId, AreaEntry>
│     └─ AreaEntry { Area: RestrictedArea, Shapes: Shape[] }
└── _index: SpatialIndex<ShapeRef>
      └─ ShapeRef { Id, Area, Shape }
            Shape: (Aabb Bounds, GeoPolygon? Polygon)
```

- ポリゴン登録 → 1 つの `Shape`（`Polygon` 非 null）
- 単一メッシュ登録 → 1 つの `Shape`（`Polygon` null、`Bounds` はメッシュ AABB）
- 複数メッシュ一括登録 → メッシュ数分の `Shape`（全て `Polygon` null）。`AreaEntry.Shapes` で 1 ID にまとめて保持

**internal クエリ API**（ステップ 9 で `EdgeWeightCalculator` から利用）:

- `IEnumerable<RestrictedArea> QueryCandidates(GeoCoordinate p1, GeoCoordinate p2)`
- `IEnumerable<RestrictedArea> QueryCandidates(Aabb queryBounds)`

両者とも `HashSet<RestrictedAreaId>` で重複除去。複数メッシュ登録（1 ID で複数 Shape）でも同一 ID は 1 回だけ返る。

**`RemoveByTag(string tag)`** は `_entries` を線形走査して該当 ID をまず削除、続いて `_index.RemoveAll(s => idSet.Contains(s.Id))` でインデックスから一括除去（メッシュ複数登録時に複数 Shape を確実に消すため、ID セット引き当て）。

**スレッド安全性**: Phase 1 では非対応（要件にスレッド安全要件なし）。シミュレーション側は単一スレッドから呼ぶ前提。Phase 2 以降で必要になれば `ConcurrentDictionary` + `ReaderWriterLockSlim` 化。

### 10.3 設計判断の根拠

- **`Aabb.IntersectsSegment` を Liang-Barsky にした理由**: ステップ 9 で「エッジシェイプ各セグメントに対する AABB プリフィルタ」を高頻度に呼ぶため、4 辺との交差を分割せず 1 パスで判定したい。Cohen-Sutherland よりループが浅く、SAT より分岐が単純。
- **`PolygonIntersection` の Hole 判定をリング 1 つずつにした理由**: Hole 数は Phase 1 想定ユースケース（災害ポリゴン）で 0〜2 個。線形走査で十分。R-tree 内蔵は YAGNI。
- **`SpatialIndex<T>` をジェネリックにした理由**: Phase 1 では `ShapeRef` のみ使うが、ステップ 9 でエッジシェイプ AABB キャッシュにも転用する可能性がある。汎用化コストはほぼゼロ。
- **`MeshCode?` を廃止し `IReadOnlyList<MeshCode>?` に統一した理由**: 「単数メッシュ」と「複数メッシュ」を別プロパティに分けると `ListAll()` 利用者が両方チェックする必要があり API 表面が膨らむ。要素数 1 のリストに統一する方が消費側コードがシンプル。v0.x のため破壊変更コストは低い。
- **`RestrictedAreaService` 内部で Shape を `Polygon?` Nullable にした理由**: メッシュ AABB はそのままが境界（追加の多角形交差判定不要、REQ-RST-015）。`Polygon == null` を「メッシュ由来 Shape」のマーカとして使い、ステップ 9 で `if (shape.Polygon == null) → AABB 交差で確定`、`else → 線分 vs ポリゴンの厳密判定` の二分岐に直結させる。
- **`QueryCandidates` を `IEnumerable<RestrictedArea>` 返却にした理由**: ステップ 9 の `EdgeWeightCalculator` は最初の `BlockArea` を見つけた時点で打切る（短絡評価）。リスト化は不要、yield で十分。
- **`Aabb` を internal のまま据え置いた理由**: 公開型として露出する必然性がない（要件 §7.1 で `Aabb` 型に直接アクセスする API は規定されていない）。Phase 2 以降で API として必要なら昇格を再評価。

### 10.4 トレードオフ・制約

- **線形走査 `SpatialIndex` の性能限界**: 制約数 N、エッジ数 E、シェイプ点数 S のとき判定回数 O(N × E × S)。Phase 1 想定の N ≦ 数百、E ≦ 数万のスケールでは実用可能。N が数千を超える場合は R-tree 化必須（Phase 2 申し送り）。
- **`PolygonIntersection.ComputeBoundingBox` が Hole を無視**: Hole は外周より内側にあるため AABB に影響しない（前提）。退化ポリゴン（Hole が外周を超える）は不正入力として無検証。GML パーサー（ステップ 10、KSJ スキーマ準拠）で入力時検証することを推奨。
- **メッシュコード AABB は WGS84 平面近似**: 厳密な JIS X0410 矩形は経緯度の楕円体補正を含むが、Phase 1 では `MeshCodeConverter` が経緯度差分のみで実装（許容誤差 ≦ 1 cm／250m メッシュ）。
- **`RestrictedAreaService.Remove(unknownId)` は何もしない**: 存在しない ID で例外を投げず NoOp。シミュレーション側で「制約が消えたか確認したい」ケースは想定されず、冪等な API の方が呼びやすい。`Contains` で事前確認可能。
- **複数メッシュ登録時の AABB 結合は行わない**: 各メッシュを個別 Shape として保持。隣接メッシュをまとめた大 AABB（Union）も計算可能だが、結合 AABB は「実領域より広い枝刈り精度」になるため線形走査の現実装では損のみ。R-tree 化時に再評価。

### 10.5 検証方法

- `dotnet build OsmDotRoute.sln`: 0 警告・0 エラー（116 個全テスト pass）
- `tests/OsmDotRoute.Tests/AabbTests.cs`: 交差・包含・線分交差（端点内部・貫通・接触・外れ）・Union・FromCoordinates の 13 ケース
- `tests/OsmDotRoute.Tests/PolygonIntersectionTests.cs`: 単位正方形・Hole 込みの内外判定、線分交差（実領域内・境界横断・Hole 内収まり・Hole 境界横切り）、BBox 算出の 12 ケース
- `tests/OsmDotRoute.Tests/RestrictedAreaServiceTests.cs`: 登録（ポリゴン／単一メッシュ／複数メッシュ）・難所タイプ検証（空文字・null・ユーザー定義）・個別削除・タグ削除・全クリア・QueryCandidates（AABB／線分／複数メッシュの ID 重複除去）の 14 ケース

**ステップ 8 実施結果（2026-05-19）**:

```text
ビルドに成功しました。
    0 個の警告
    0 エラー

成功!   -失敗:     0、合格:   116、スキップ:     0、合計:   116、期間: 3 s
```

新規ファイル: `Geometry/PolygonIntersection.cs`, `Geometry/SpatialIndex.cs`, `tests/AabbTests.cs`, `tests/PolygonIntersectionTests.cs`, `tests/RestrictedAreaServiceTests.cs`。  
変更ファイル: `Geometry/Aabb.cs`（メソッド追加）、`Restrictions/BlockArea.cs` / `DifficultyArea.cs`（`MeshCodes` 化）、`Restrictions/RestrictedAreaService.cs`（本実装）。

### 10.6 実装メモ

- **`Aabb` の境界接触判定**: `Intersects` / `Contains` ともに「境界線上は内側／交差扱い」で統一。`<=` / `>=` を使用。地理座標は離散値より浮動小数点なので「ぴったり境界」がレアケースだが、メッシュ AABB 同士の隣接判定では境界共有が頻発するため、保守的に「触れていれば交差」とした。
- **Ray Casting の境界線上判定**: `((yi > py) != (yj > py))` の伝統的判定は境界線上の点で挙動が不安定（頂点を含む辺で false negative）。今回は前段で `IsPointOnSegment` を呼び境界判定を明示的に真とすることで回避。
- **複数メッシュ登録時の `SpatialIndex` エントリ展開**: 1 つの `RestrictedAreaId` に対し N 個の `ShapeRef` がインデックス内に並ぶ。`QueryCandidates` は同 ID を `HashSet` で重複除去するため、上位 API（`EdgeWeightCalculator`）は ID 単位の単純消費でよい。
- **テスト用クエリ範囲設計の落とし穴**: 東京駅メッシュ `53394611L` の AABB は (35.675, 139.7625)-(35.683, 139.775)。一見「(35,139)-(36,140) の単位正方形と離れている」と思いがちだが、両者の AABB は内包関係にある。プリフィルタテストでは座標重なりを事前に手計算で確認する必要がある（初回ドラフトで踏んだ）。
- **`AreaEntry` を `sealed record`、`Shape` を `readonly record struct` にした理由**: `AreaEntry` は参照経由で使い回す（Dictionary 値）。`Shape` は短命なクエリ用構造体で値コピーの方がアロケーションフリー。
- **`QueryCandidates` の yield**: ステップ 9 で短絡評価を活かすため、enumerator のまま返す。`ToList()` するとアロケーションが増える上に短絡できない。

---

## 11. 制約対応 Dijkstra 統合

**対応ステップ**: ステップ 9
**対応要件**: REQ-RST-012〜015, REQ-RST-030〜032, REQ-RTE-001
**実装日**: 2026-05-19
**実装バージョン**: 0.13（進行中）
**主要ファイル**:

- `src/OsmDotRoute/Restrictions/RestrictedAreaService.cs`（`EvaluateConstraints` 追加）
- `src/OsmDotRoute/Routing/EdgeWeightCalculator.cs`（`RestrictedAreaService?` 注入、`EvaluateEdgeDurationSec` / `EvaluateEdgePartialDurationSec` 追加）
- `src/OsmDotRoute/Routing/DijkstraEngine.cs`（4 評価点を制約込み API に置換）
- `src/OsmDotRoute/Router.cs`（`_restrictions` を `EdgeWeightCalculator` に伝搬）
- `tests/OsmDotRoute.Tests/RestrictedRoutingTests.cs`（新規 9 ケース）

### 11.1 意図

ステップ 8 で構築した制約管理基盤と、ステップ 5b で完成した独自 Dijkstra を統合し、進入不可エリア・難所エリアを経路探索が正しく考慮するようにする（REQ-RST-013〜015, REQ-RST-030〜032）。複数難所重複時のルール（積・短絡）も実装し、`Calculate` 呼出ごとに制約をキャッシュなしで再評価することで制約変更の即時反映を保証する（REQ-RST-012）。

### 11.2 採用設計

#### 11.2.1 評価フロー（エッジ単位）

`EdgeWeightCalculator.EvaluateEdgeDurationSec(IRoadGraphEdgeEnumerator)` の処理順:

1. `Evaluate(en.EdgeProfileIndex)` でプロファイル評価（access / speed / oneway）
2. `CanTraverseInEnumeratorDirection(eval, en.DataInverted)` で方向適合性を確認 → 不適合は `+∞`
3. `DurationSec(en.DistanceM, eval.SpeedKmh)` で基本所要時間
4. `EvaluateConstraintFactor(en.From, en.To, en.Shape)` で結合 speedFactor を算出
5. `baseDuration / factor` を返す（factor が `+∞` の場合は `+∞`）

`EvaluateConstraintFactor` は `_restrictions is null` なら 1.0 を即返し、それ以外は `BuildFullShape(from, to, middle)` で `[GetVertex(from), …middle, GetVertex(to)]` を構築して `RestrictedAreaService.EvaluateConstraints(shape, evaluator)` に委譲する。

部分通過版 `EvaluateEdgePartialDurationSec(RoadEdge edge, double partialDistanceM, EdgeEvaluation eval)` も同じ制約評価ロジック（エッジ全体のシェイプに対する評価）を使い、得られた結合係数を **部分距離の所要時間に**適用する。

#### 11.2.2 `RestrictedAreaService.EvaluateConstraints` のアルゴリズム

```text
1) edgeShape の Aabb を 1 回計算
2) _index.Query(edgeAabb) で候補 ShapeRef を取得（線形走査、AABB プリフィルタ）
3) HashSet<RestrictedAreaId> で ID 重複除去（複数メッシュ 1 ID は 1 回扱い）
4) ID ごとに entry.Shapes の各 Shape に対し:
   - Aabb.IntersectsSegment(p1,p2) で AABB プリフィルタ
   - Polygon != null なら PolygonIntersection.IntersectsSegment で厳密
   - Polygon == null（メッシュ AABB）なら AABB 交差で確定（REQ-RST-015）
5) 交差 ID が BlockArea → 即 +∞ 返却（短絡、REQ-RST-032）
6) 交差 ID が DifficultyArea → evaluator.EvaluateDifficulty(type)
   - canPass:false なら +∞ 返却（短絡、REQ-RST-031）
   - speedFactor を積に乗せる（REQ-RST-030）
   - 積が 0.0 以下なら +∞（数値安全弁）
7) 全候補処理後、積を返す
```

#### 11.2.3 Dijkstra への統合点

`DijkstraEngine.Run` の **4 つの評価点**すべてを新 API に置換:

| 箇所 | 旧 | 新 |
|---|---|---|
| 同一エッジ直接通過（前進・後退） | `DurationSec(d, sourceEval.SpeedKmh)` | `EvaluateEdgePartialDurationSec(sourceEdge, d, sourceEval)` |
| ソース初期化（端点 To / From） | `DurationSec((1-fs)*sd, sourceEval.SpeedKmh)` | `EvaluateEdgePartialDurationSec(sourceEdge, d, sourceEval)` |
| ターゲット流入（端点 From / To） | `DurationSec(remD, targetEval.SpeedKmh)` | `EvaluateEdgePartialDurationSec(targetEdge, remD, targetEval)` |
| 近傍展開 | `Evaluate + DurationSec` | `EvaluateEdgeDurationSec(en)` |

ソース／ターゲット初期化部は `+∞` を `pq.Push` しないようガード追加。メインループの `cost[v] < cost[u]` 比較は `+∞` 自然枝刈りでそのまま動く。

#### 11.2.4 難所重複ルール（計算式）

エッジに対して候補制約 `R = {r₁, r₂, …, rₙ}` が交差判定で残ったとき:

- `BlockArea ∈ R` または `∃ rᵢ ∈ R: rᵢ is DifficultyArea ∧ evaluator.EvaluateDifficulty(rᵢ.DifficultyType).CanPass == false`
  → **通行不可**（重み `+∞`、REQ-RST-031, 032）
- それ以外:
  → **結合係数** = `Π { evaluator.EvaluateDifficulty(rᵢ.DifficultyType).SpeedFactor | rᵢ ∈ DifficultyAreas(R) }`
  → エッジ所要時間 = `(distance / speed) / 結合係数`（REQ-RST-030）

同じ `RestrictedAreaId` を持つ複数 Shape（複数メッシュ登録）は ID 単位で 1 回だけ係数に乗る。

### 11.3 設計判断の根拠

- **制約評価の単位を「エッジ全体」にした理由**: 「このエッジが flooding 領域にかかっている → 速度 0.3 倍」というモデルが直感的で、利用者（シミュレーション）が結果を解釈しやすい。エッジ内位置別の細分評価は実装複雑化・性能劣化を招き、Phase 1 の解像度（数十 m〜数百 m のエッジ）ではメリットも薄い。部分通過（スナップ点 → 端点）でも同じ係数を適用する一貫性が保てる。
- **`EvaluateConstraints` を `RestrictedAreaService` 内に置いた理由**: 内部の `Shape` キャッシュ（AABB + ポリゴン）に最短経路でアクセスできる。`Aabb` も `Shape` も internal で公開しない方針なので、外側で同等ロジックを書くと public API 露出が増える。サービスが「自身の状態に対するクエリ」として担う方が自然。
- **`HashSet<RestrictedAreaId>` で ID 重複除去する理由**: 複数メッシュ登録（REQ-RST-003/006）で 1 ID = 複数 Shape のエントリがインデックス内に並ぶため、ナイーブに走査すると同じ制約を複数回 `speedFactor` の積に乗せてしまう。ID 単位で 1 回が正しいモデル。
- **`Π speedFactor` を 0.0 以下で `+∞` 化する理由**: プロファイル定義の検証で `speedFactor ∈ [0,1]` を保証しているが、`0.0 × 何か = 0.0` は数学的には通行可能（無限大時間）であり、Dijkstra で `pq.Push(v, 0/0)` 的な NaN になりうる。明示的な `+∞` 短絡で安全側に倒す。
- **キャッシュ無しで毎回再評価する理由**: REQ-RST-012「制約変更は次回の `Calculate` から即時反映」を最も簡潔に満たす。エッジごとの制約評価結果は Calculate 内のローカル計算に閉じ、グローバル状態を持たない。性能は AABB プリフィルタで担保（候補が空のエッジは O(1) で抜ける）。
- **ソース／ターゲット部分通過にも制約適用した理由**: スナップ点周辺だけ難所に入る場合（例: 起点が flooding 領域内に少し入り込む）でも、ユーザー期待は「その部分も遅くなる」。これを実装計画書の REQ-RST-030 解釈とした。部分距離 × 同じ係数で表現するため数式は単純。
- **`_restrictions is null` の早期 return を残した理由**: 制約サービス未指定時のオーバヘッドを 0 にする。`EvaluateConstraints` 側で `_entries.Count == 0` の早期 return もあるため二重防御だが、`null` 時はシェイプ構築コスト（`BuildFullShape` の `List<GeoCoordinate>` 確保）も避けられる。

### 11.4 トレードオフ・制約

- **エッジ単位の係数モデルの限界**: 1 km の長いエッジが端だけ flooding にかかる場合でも「エッジ全体が 0.3 倍速」と判定する。これは「保守的すぎる（実際より遅く見積もる）」方向の誤差。Phase 1 の意思決定（迂回するか否か）には十分だが、所要時間絶対値の精度を求める用途では Phase 2 で「エッジ内分割評価」を検討。
- **AABB プリフィルタの精度限界**: 細長いエッジが斜めに走り、制約 AABB と AABB は重なるがエッジは制約外という偽陽性はある。続く厳密判定で除外されるが、無駄走査は発生する。Phase 1 の制約数（数百）では実用範囲内。
- **複数 Shape の早期打切なし**: `EdgeIntersectsAreaShapes` は ID 内の全 Shape を見るが、最初の 1 つで `return true`。これは最悪 O(M)（M = ID 内 Shape 数）。実用上 1 ID = 数〜数十 Shape を想定。
- **テスト用迂回判定の脆弱性**: `Calculate_BlockArea_OnRoute_DetoursOrReturnsNull` と `Calculate_DifficultyArea_Landslide_...` / `Calculate_BlockArea_Overrides_DifficultyArea` は「迂回 or null」を許容する書き方。データ依存で必ず迂回するとは限らないため緩めの判定。代替路があるエリアでのテストにする工夫はしているが、データ依存性は残る。

### 11.5 検証方法

- `dotnet build OsmDotRoute.sln`: 0 警告・0 エラー
- `tests/OsmDotRoute.Tests/RestrictedRoutingTests.cs` 9 ケース:
  1. 制約サービス空インスタンス → baseline と完全一致
  2. ベースライン中央局所 BlockArea → 迂回 or null
  3. ベースライン全体を覆う flooding × car → 所要時間 ≈ baseline × 3.33 倍（許容 ±1%）
  4. ベースライン全体を覆う flooding × pedestrian → 所要時間 ≈ baseline × 10 倍（±1%）
  5. flooding + construction 重複 × car → 所要時間 ≈ baseline × 16.67 倍（±1%）
  6. ベースライン全体を覆う landslide × car → 迂回 or null（canPass:false）
  7. 同一領域に BlockArea + flooding 重複 → BlockArea 優先で迂回 or null
  8. 未知タイプ `"meteor"` → `difficultyDefault` 適用、速度変化なし
  9. `ClearAll` 後 → baseline と完全一致

**ステップ 9 実施結果（2026-05-19）**:

```text
ビルドに成功しました。
    0 個の警告
    0 エラー

成功!   -失敗:     0、合格:   125、スキップ:     0、合計:   125、期間: 4 s
```

新規ファイル: `tests/OsmDotRoute.Tests/RestrictedRoutingTests.cs`（9 ケース、+9）。  
変更ファイル: `Restrictions/RestrictedAreaService.cs`（`EvaluateConstraints` 追加）、`Routing/EdgeWeightCalculator.cs`（注入＋ 2 新メソッド）、`Routing/DijkstraEngine.cs`（4 評価点置換）、`Router.cs`（`_restrictions` 伝搬）。

### 11.6 実装メモ

- **`EvaluateEdgePartialDurationSec` に `EdgeEvaluation` を引数で渡す理由**: ソース／ターゲット側では Dijkstra 開始時に 1 回だけ評価して結果をローカル変数 `sourceEval` / `targetEval` に保持している。同じ評価を 4 箇所で繰り返さないために引数化。近傍展開側（`EvaluateEdgeDurationSec`）はエニュメレータの現在エッジで毎回評価が必要なので内部で `Evaluate` 呼び出し。
- **`BuildFullShape` で `IRoadGraphEdgeEnumerator.Shape` / `RoadEdge.Shape` のいずれも「中間のみ」だった件**: 端点座標は `_graph.GetVertex(uint)` で別取得が必要。エッジ評価のたびに `List<GeoCoordinate>` を 1 つ確保するが、Phase 1 では許容（性能ベンチで問題が出れば `ArrayPool<GeoCoordinate>` 化検討）。
- **テストヘルパ重複**: `CalculateRouteTests.cs` と `RestrictedRoutingTests.cs` で `EnsureTestData` / `CollectCarAccessibleVertexPairs` / `IsCarHighway` が重複している。Phase 1 の他テストでも需要が出れば `tests/TestData/RoutingTestHelper.cs` として共通化する。今は YAGNI で見送り。
- **Pedestrian + flooding=10 倍テスト**: pedestrian.json で `flooding.speedFactor=0.1`。`speedBounds.maxKmh=5` のクランプ範囲内なら 1/0.1=10 倍が正確に出る。最大速度に張り付いていない普通のエッジでは数値ぴったり 10 倍にならない可能性があったが、実装上は「baseline 計算時にすでにクランプ済み」「制約適用時は基本所要時間に係数を掛けるだけ」なので、係数比だけが効いて 10 倍が出る。テストは ±1% で許容。
- **同一ポリゴンの BlockArea + DifficultyArea 検証**: 設計上 BlockArea が優先（短絡）で確認したが、テスト 7 は「迂回 or null」許容に留めている。データ依存で必ず迂回成立しないため。代わりに「flooding 単独だと同じ経路」を補足計算で確認することで、BlockArea 追加で経路変化したことが優先証拠になる構成にした。

---

## 12. GML 入力対応（KSJ アプリケーションスキーマ、形状のみ抽出）

**対応ステップ**: ステップ 10
**対応要件**: REQ-RST-020〜028, REQ-RST-040
**実装日**: 2026-05-19
**実装バージョン**: 0.15（進行中）
**主要ファイル**:

- `src/OsmDotRoute/Gml/GmlParser.cs`（internal static、`System.Xml.XmlReader` ベース）
- `src/OsmDotRoute/Gml/InvalidGmlException.cs`（公開例外型）
- `src/OsmDotRoute/MapBounds.cs`（公開値型、REQ-RST-040 用）
- `src/OsmDotRoute/Restrictions/RestrictedAreaService.cs`（6 メソッド追加 + GeoJSON 系 3 メソッド削除、各 GML メソッドに `MapBounds?` 引数）
- `tests/OsmDotRoute.Tests/GmlParserTests.cs`（9 ケース）
- `tests/OsmDotRoute.Tests/RestrictedAreaServiceGmlTests.cs`（13 ケース、うち 1 件は実データ統合）

### 12.1 意図

国土数値情報 KSJ アプリケーションスキーマ準拠 GML 3.2 から制約エリアを一括登録できる入力 API を完成させる（REQ-RST-020〜028, REQ-RST-040）。Phase 1 動作確認は A31「浸水想定区域」(`<ksj:ExpectedFloodArea>`) で行うが、**パーサーはフィーチャ要素名にハードコード依存しない**ことで、A30a4「土砂災害警戒区域」等の他 KSJ プロダクトを後追い追加できる拡張性を確保する。ハザード属性（`<ksj:waterDepth>` 等）は保持せず、難所タイプ・タグはともに API 引数でユーザーが指定する責任分担（要件 v1.5 で確定）。シミュレーションのマップ範囲外フィーチャは optional `MapBounds` 引数で除外できる（要件 v1.6 で追加、REQ-RST-040）。

### 12.2 採用設計

#### 12.2.1 `GmlParser`（internal static、ストリーミング XmlReader）

| API | 戻り値 | 説明 |
|---|---|---|
| `ParseString(string gml)` | `IReadOnlyList<GeoPolygon>` | GML 文字列を解析 |
| `ParseStream(Stream stream)` | `IReadOnlyList<GeoPolygon>` | GML Stream を解析（ストリーミング） |

XmlReader 設定（XXE 対策）:

- `XmlResolver = null` ／ `DtdProcessing = Prohibit`（外部実体参照を解決しない）
- `IgnoreWhitespace = true` ／ `IgnoreComments = true` ／ `IgnoreProcessingInstructions = true`

#### 12.2.2 1 パスアルゴリズム

```text
1) XmlReader で順次走査
2) ルート直下 (Depth==1) の要素を識別:
   - <gml:Curve gml:id="X">  → posList を「緯度 経度」順で読取し、curves[X] = coords
   - <gml:Surface gml:id="X"> → <gml:exterior>/<gml:interior> の curveMember xlink:href から
     exteriorCurveId / interiorCurveIds[] を抽出し、surfaces[X] = SurfaceRef
   - <gml:MultiSurface>      → NotSupportedException （REQ-RST-023）
   - <gml:*> 以外（gml 名前空間外） → フィーチャ候補。ReadSubtree 内で
     最初の xlink:href 属性を持つ子要素から参照 ID を抽出し pendingFeatures に追加
3) 走査完了後、pendingFeatures を解決:
   - surfaces[surfaceId] → curves[exteriorCurveId] / curves[holeIds] を引いて
     new GeoPolygon(outer, holes) として yield
4) Surface/Curve 参照が解決失敗 → InvalidGmlException
```

#### 12.2.3 名前空間判別

- `GmlNs = "http://www.opengis.net/gml/3.2"`
- `XlinkNs = "http://www.w3.org/1999/xlink"`
- KSJ 固有名前空間（`http://nlftp.mlit.go.jp/ksj/schemas/ksj-app`）は識別に**使わない**。フィーチャ要素は「gml 名前空間以外」で識別 → KSJ プロダクトを問わず、また架空名前空間でも動作。

#### 12.2.4 フィーチャ要素名非依存の参照解決

KSJ では `<ksj:bounds xlink:href="#aXXX"/>` が Surface 参照の慣習だが、要素名「bounds」も名前空間「ksj」もハードコードしない。代わりに「フィーチャ要素配下を `ReadSubtree` で走査し、**最初に見つかった `xlink:href="#..."` 属性**を Surface 参照候補とみなす」ことで、要素名に依存しない汎用パーサーを実現。Surface 辞書に解決できなければ単に登録されない（フィーチャ要素配下に `xlink:href` を持つ別目的の子要素（関連コード等）があっても、Surface 辞書を引いた時点で除外できる）。

#### 12.2.5 `RestrictedAreaService` の 6 メソッド

| メソッド | 主要引数 | 説明 |
|---|---|---|
| `AddBlockAreaFromGml(string, MapBounds?, string?)` | GML 文字列・mapBounds・tag | 全フィーチャ（フィルタ後）を `BlockArea` で登録 |
| `AddBlockAreaFromGmlFile(string, MapBounds?, string?)` | ファイルパス・mapBounds・tag | `File.OpenRead` → Stream 版へ委譲 |
| `AddBlockAreaFromGmlStream(Stream, MapBounds?, string?)` | Stream・mapBounds・tag | `GmlParser.ParseStream` + フィルタ + 登録 |
| `AddDifficultyAreaFromGml(string, string difficultyType, MapBounds?, string?)` | GML 文字列・難所タイプ・mapBounds・tag | 全フィーチャ（フィルタ後）を `DifficultyArea` で登録 |
| `AddDifficultyAreaFromGmlFile(string, string difficultyType, MapBounds?, string?)` | ファイルパス・難所タイプ・mapBounds・tag | 同上、ファイル版 |
| `AddDifficultyAreaFromGmlStream(Stream, string difficultyType, MapBounds?, string?)` | Stream・難所タイプ・mapBounds・tag | 同上、Stream 版 |

戻り値は `RestrictedAreaId[]`（採用された各フィーチャごとに 1 ID、REQ-RST-021）。難所タイプ検証（空文字・null 拒否、REQ-RST-007）は `DifficultyArea` コンストラクタに委譲。

#### 12.2.7 マップ範囲フィルタ（REQ-RST-040）

公開値型 `MapBounds(GeoCoordinate SouthWest, GeoCoordinate NorthEast)` を新設（`src/OsmDotRoute/MapBounds.cs`）。`Contains(GeoCoordinate)` メソッドは境界線上を**内側扱い**で判定（`<=` / `>=`）。

`RestrictedAreaService` の GML 6 メソッドはすべて optional `MapBounds? mapBounds = null` 引数を持つ。内部の `PassesMapBoundsFilter` が `GeoPolygon.OuterBoundary` を走査し、1 頂点でも `mapBounds.Contains(coord)` を満たすフィーチャのみ採用。`mapBounds == null` の場合は無条件で採用（旧挙動互換）。Hole は判定に使わない。

利用例:

```csharp
var stats = routerDb.GetStatistics();
var mapBounds = new MapBounds(stats.SouthWest, stats.NorthEast);
restrictions.AddDifficultyAreaFromGmlFile(
    "A31-12_24.xml",
    DifficultyTypes.Flooding,
    mapBounds,
    tag: "mie-flood");
// → 道路ネットワーク範囲外のフィーチャは自動的に除外
```

#### 12.2.6 例外戦略

- `InvalidGmlException`（公開）: GML XML パースエラー、xlink 参照解決失敗、`<gml:posList>` 不正、必須要素欠落
- `NotSupportedException`（標準）: `<gml:MultiSurface>` 検出時（REQ-RST-023）
- `ArgumentException` / `ArgumentNullException`: 難所タイプ空文字・null、引数 null

### 12.3 設計判断の根拠

- **1 パス + 未解決リスト方式**: A31 サンプルでは Curve → Surface → ExpectedFloodArea の順序になっており、フォワード参照は不要。ただし KSJ 仕様で順序保証がない可能性もあるため、フィーチャ参照だけは「全走査完了後に解決」する設計とした。Curve/Surface はその場で読み切ってメモリ辞書化。
- **`Depth == 1` でルート直下に限定した理由**: `<ksj:Dataset>` ルート要素自体や、`<gml:boundedBy>` 配下のメタデータ要素を「フィーチャ候補」として誤認するのを防ぐ。実装初版でルート要素自体をフィーチャ判定してしまい全テスト失敗、Depth 判定で解決した経緯あり。
- **フィーチャ要素名非依存**: REQ-RST-020 の「任意の KSJ プロダクトを受け入れる」要求を満たす。`<ksj:ExpectedFloodArea>` も架空の `<test:DummyArea>` も同じ機構で読める（テスト `ParseString_DummyFeatureName_StillResolvesViaXlink` で確認）。
- **`InvalidGmlException` を public にした理由**: 利用者が `try-catch` で GML 入力エラーを処理できるようにするため。`NotSupportedException` は .NET 標準なので既存ノウハウが効く。
- **`AddDifficultyAreaFromGml*` で `difficultyType` を必須引数にした理由**: 「フィーチャ要素名から自動判定」を採用しないという v1.5 確定方針の API への反映。利用者がデータ源（A31 浸水＝flooding、A30a4 土砂＝landslide 等）に応じて適切な難所タイプを明示指定する。
- **`MapBounds` を公開値型として新設した理由**: 利用者が API 引数として「マップ範囲」を渡す必要があるため、internal の `Geometry.Aabb` / `GeoBounds` は使えない。`record struct` で不変・等価判定無料、`Contains` メソッドを持ち、`OsmDotRoute` ルート名前空間に配置（公開型カタログ §4 と整合）。
- **`mapBounds` 引数を optional にした理由**: REQ-RST-040 で「未指定時は全フィーチャ採用（互換動作）」を規定。既存テスト（ステップ 10 初版の `tag` named argument 呼び出し）を破壊しない。
- **`mapBounds` 引数を `difficultyType` の後・`tag` の前に配置した理由**: 位置引数として「形状抽出に必須のもの（difficultyType）→ フィルタ条件（mapBounds）→ メタ情報（tag）」の重要度順に並ぶ。名前付き引数なしでも `service.AddDifficultyAreaFromGml(gml, "flooding", bounds)` と自然に書ける。
- **`PassesMapBoundsFilter` を `RestrictedAreaService` 内に置いた理由**: `GmlParser` は形状抽出に専念させ、フィルタ判定はサービス側の責務として分離。GML 由来でないポリゴン（手動 `AddBlockArea(polygon)` 等）と同じフィルタを後で他 API にも展開しやすくする（Phase 2 で `AddBlockAreaFromGeoJson` 等を追加する場合の再利用性）。
- **ハザード属性を読み飛ばす実装**: `ReadSubtree` で `xlink:href` だけ探して他は無視。`<ksj:waterDepth>` 等の子要素はパース時間も計上されるが、`XmlReader.Skip()` 同等の処理で実用上のコストは無視可能。
- **XmlReader 設定で XXE 対策**: `XmlResolver = null` + `DtdProcessing = Prohibit` で外部実体参照を解決しない。利用者が不信ファイルを誤って読み込んでも XXE 攻撃が成立しない。

### 12.4 トレードオフ・制約

- **`<gml:MultiSurface>` Phase 1 非対応**: A31 サンプル（1.6GB、`MultiSurface` 出現 0 件を `grep -c` で確認、2026-05-19）。他 KSJ プロダクトで使用された場合は `NotSupportedException`。Phase 2 以降で対応（要件 REQ-RST-023）。
- **ハザード属性を保持しない**: 利用者が `waterDepth` 等で経路コストを変えたいなら、GML を別途読んでフィーチャごとに `AddDifficultyArea(polygon, customDifficultyType)` を呼ぶワークフロー（Phase 1 では「深さ別難所タイプ」を独自定義してプロファイルに追加）。Phase 2 で `RestrictedArea.Attributes` プロパティ追加を検討。
- **フィーチャ別タグ非対応**: `tag` 引数で全フィーチャに同一タグのみ。フィーチャ別タグが必要なら Phase 2 で `tagMapper: Func<...>` のような拡張点を追加（要件 REQ-RST-027）。
- **2 GB 超ファイル**: `XmlReader` 自体は理論上ストリーム長無制限だが、Curve 辞書がメモリ常駐するため**全頂点座標を保持できるメモリ**が必要。A31 サンプル 1.6GB で 9 秒読込（実測、Phase 1 ステップ 10 実施時）。座標点数が桁違いに増えたら（数十 GB 級）`SQLite` 等のディスクバック辞書化を Phase 2 で検討。
- **`<gml:Curve>` の `<gml:LineStringSegment>` 1 つ前提**: KSJ A31 サンプル準拠。複数 LineStringSegment や Arc / CubicSpline 等の曲線型は未対応（A31 では未使用）。

### 12.5 検証方法

- `dotnet build OsmDotRoute.sln`: 0 警告・0 エラー
- `tests/OsmDotRoute.Tests/GmlParserTests.cs` 9 ケース:
  1. 最小 GML（1 Curve + 1 Surface + 1 フィーチャ）読込
  2. Hole 込み Surface 読込（外周 1 + Hole 1 + 1 フィーチャ）
  3. 複数フィーチャ（3 件）の順序保持
  4. 架空フィーチャ要素名 `<test:DummyArea>` も解決（拡張性検証）
  5. `<gml:MultiSurface>` → `NotSupportedException`
  6. 不正 XML → `InvalidGmlException`
  7. 未解決 Surface 参照 → `InvalidGmlException`
  8. xlink:href なしフィーチャはサイレントスキップ（空結果）
  9. `ParseStream` と `ParseString` の同等性
- `tests/OsmDotRoute.Tests/RestrictedAreaServiceGmlTests.cs` 13 ケース:
  1. `AddBlockAreaFromGml` で BlockArea 登録
  2. `AddDifficultyAreaFromGml` で DifficultyType 付与
  3. ユーザー定義難所タイプ（`"snow_heavy"`）受理
  4. 難所タイプ空文字・null で `ArgumentException`（REQ-RST-007）
  5. 複数フィーチャの ID 配列返却
  6. `tag` の `RemoveByTag` 連携で一括削除
  7. `AddBlockAreaFromGmlStream` の Stream ラウンドトリップ
  8. `<gml:MultiSurface>` 例外伝播
  9. 不正 GML 例外伝播
  10. **マップ範囲フィルタ: 部分採用**（f1 のみマップ範囲内、f2/f3 は外、REQ-RST-040）
  11. **マップ範囲フィルタ: 全フィーチャ範囲外で空配列**（REQ-RST-040）
  12. **マップ範囲フィルタ: 境界線上の頂点は内側扱い**（REQ-RST-040、`Contains` の `<=`/`>=` 検証）
  13. 実データ統合: `D:/ハザードデータ/A31-12_24_GML/A31-12_24.xml`（1.6GB、三重県浸水想定区域）を `AddDifficultyAreaFromGmlFile` で読込、フィーチャ数 ≥ 1 を確認

**ステップ 10 実施結果（2026-05-19、マップ範囲フィルタ追加後）**:

```text
ビルドに成功しました。
    0 個の警告
    0 エラー

成功!   -失敗:     0、合格:   147、スキップ:     0、合計:   147、期間: 12 s
```

A31 実データ 1.6GB の読込時間: **9 秒**（テスト実行ログ実測、`AddDifficultyAreaFromGmlFile` のフル読込）。ストリーミング XmlReader が効いてメモリ使用量も実用範囲内（座標辞書のみ常駐）。

新規ファイル: `src/OsmDotRoute/Gml/GmlParser.cs`, `src/OsmDotRoute/Gml/InvalidGmlException.cs`, `src/OsmDotRoute/MapBounds.cs`, `tests/OsmDotRoute.Tests/GmlParserTests.cs`, `tests/OsmDotRoute.Tests/RestrictedAreaServiceGmlTests.cs`。  
変更ファイル: `src/OsmDotRoute/Restrictions/RestrictedAreaService.cs`（`AddFromGeoJson*` 3 メソッド削除、`AddBlockAreaFromGml*` / `AddDifficultyAreaFromGml*` の 6 メソッド追加、各メソッドに `MapBounds?` 引数）。

### 12.6 実装メモ

- **`Depth == 1` の罠**: XmlReader の `Depth` はルート要素自体が 0、その子が 1。実装初版で `Depth` 判定を入れず、`<ksj:Dataset>` ルート自体を「フィーチャ候補」と誤認して全テスト失敗した（最初の xlink:href が Curve の `curveMember` を返してしまい、Surface 参照と勘違いした）。`Depth == 1` 限定で修正。XML 構造が違う KSJ プロダクトでルート直下に余計なネスト層があると本判定がずれる可能性あるが、KSJ アプリケーションスキーマ準拠ならフラット構造が保証される。
- **`reader.ReadSubtree()` の挙動**: 子 reader を返し、Dispose で親 reader を部分木の EndElement に位置づける。親 reader の次の `Read()` で兄弟に進むため、ループ全体が自然に動く。注意点: 子 reader の `Depth` は部分木のルートを 0 として再カウントするので、子 reader 内で `Depth` 判定すると親 reader と一致しない。
- **`xlink:href` 取得には 2 引数オーバーロード必須**: `reader.GetAttribute("href")` だと「名前空間なしの `href` 属性」を探す。`reader.GetAttribute("href", XlinkNs)` で `xlink:href` を取得する。要素にデフォルト名前空間が設定されている場合に重要。
- **A31 実データ 9 秒の内訳**: 1.6GB ファイルから Curve 辞書を構築する I/O + parse がボトルネック。`<ksj:waterDepth>` 等の不要要素を `ReadSubtree` 内で読み飛ばすコストは比較的小さい（XmlReader のスキップ最適化が効く）。Phase 2 で `Span<char>` 直接走査による高速化を検討する余地あり。
- **テストデータをファイルにせず verbatim string にした理由**: ミニ GML 5 種を `.xml` ファイルとして配置するより、テストファイル内 verbatim string に持つ方が変更が一目で追える。実データテストだけはサイズの都合でファイル参照（環境依存テスト、未配置ならスキップ）。
- **`File.OpenRead` の例外**: `AddBlockAreaFromGmlFile` / `AddDifficultyAreaFromGmlFile` でファイル不在の場合は `FileNotFoundException` が `.NET` 標準で投げられる。`InvalidGmlException` でラップしていないのは、ファイル I/O エラーと GML パースエラーは異なる障害種別だから（呼び出し側が別ハンドリングしたい）。

---

## 13. 経路 GeoJSON 出力（**廃止**）

**対応ステップ**: ~~ステップ 11~~（廃止）
**対応要件**: ~~REQ-FMT-004~~（v1.7 で廃止）
**判断日**: 2026-05-19
**ステータス**: 廃止判断記録（実装せず、要件側で削除）

### 13.1 当初構想（参考、未実装）

要件 REQ-FMT-004 として「経路 (`Route`) を GeoJSON `LineString` Feature に変換するユーティリティ」を提供する案があった。実装予定だった内容:

- `OsmDotRoute.GeoJson.GeoJsonWriter.WriteRoute(Route) → string`
- 経路シェイプを `[lon, lat]` 配列に変換、`properties` に総距離 (m)・総所要時間 (s) を含める
- 既存 `Router.GetRoadNetworkGeoJson()`（REQ-RTE-004、ステップ 6 実装済）と同名前空間・同方針

### 13.2 想定していた利点

1. **可視化・デバッグ**: 経路 GeoJSON を `geojson.io` / QGIS / Leaflet / Mapbox 等で直接確認できる
2. **API 対称性**: 道路ネットワーク GeoJSON 出力（既存）と経路 GeoJSON 出力（新規）が一貫
3. **シミュレーション結果の他システム連携**: ブラウザ UI・GIS への結果ファイル渡し

### 13.3 廃止判断（2026-05-19、ユーザー合意）

**結論**: REQ-FMT-004 を要件から廃止し、Phase 1 ステップ 11 を実装せずスキップする。

**理由**:

- 親プロジェクト「災害廃棄物処理シミュレーション」での実需要が確認できなかった
- 利用者は `Route.Shape: IReadOnlyList<GeoCoordinate>` を直接取得できるため、必要なら**呼出側で数行**（`shape.Select(c => new[] { c.Longitude, c.Latitude })` を JSON 化）で GeoJSON 化可能
- ライブラリで提供する必然性は「よく使うから共通化する」程度で、Phase 1 完了の優先度から外せる
- CLAUDE.md「ついで」のリファクタリング・抽象化を避ける（YAGNI）方針と整合

**当初検討した選択肢**:

- **案 A**: 計画通り実装（数十行＋テスト 2〜3 件、軽量）
- **案 B**: Phase 2 へ延期（REQ-FMT-004 を P3/Phase2+ に格下げ）
- **案 C**（採用）: 要件削除（提供しない方針）

案 C を採用した経緯: ユーザーから「現時点で利点を把握できていない」との指摘を受け、AI Agent が想定利点と YAGNI トレードオフを整理。親プロ実需要が出てくれば要件を再起できる構造として、要件側に「廃止・要望が出た時点で再評価」と明記。

### 13.4 復活時に参照すべきもの

将来 GeoJSON 経路出力の要望が出た場合の再起準備として:

- 既存 `OsmDotRoute.GeoJson.GeoJsonWriter`（ステップ 6 実装、道路ネットワーク用）に `WriteRoute(Route)` を追加するのが最短ルート
- 出力スキーマは `RoadNetworkGeoJson` と同じ RFC 7946 準拠の `Feature` 形式、`geometry.type = "LineString"`、`properties` に距離・所要時間を含める
- 設計書 §8（道路ネットワーク GeoJSON 出力）で確立した方針（`Utf8JsonWriter` ベース、座標 `[lon, lat]` 順、precision 制御）をそのまま流用可能

### 13.5 影響範囲

- **コード**: 実装ファイル無し（追加せず）
- **要件定義書**: REQ-FMT-004 を打ち消し表記 + Ver. 1.7 で廃止記録、§8.2 出力フォーマット表の該当行も打ち消し
- **実装計画書**: ステップ 11 を「廃止」マーク
- **テスト**: 影響なし（実装無し）
- **既存 API への影響**: なし。`Router` / `Route` / `GeoJsonWriter`（道路ネットワーク用）はそのまま

---

## 14. DI 拡張とドキュメント

**対応ステップ**: ステップ 12
**対応要件**: REQ-API-008, REQ-LIC-001, REQ-PKG-001
**実装日**: 2026-05-19
**実装バージョン**: 0.17（進行中）
**主要ファイル**:

- `src/OsmDotRoute.Extensions.DependencyInjection/OsmDotRoute.Extensions.DependencyInjection.csproj`
- `src/OsmDotRoute.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`
- `src/OsmDotRoute.Extensions.DependencyInjection/OsmDotRouteOptions.cs`
- `Directory.Build.props`（`GenerateDocumentationFile=true` 切替）
- `README.md`（全面書き換え）

### 14.1 意図

Phase 1 ライブラリ本体機能を「他人がソース参照／DI コンテナ経由で安全に使える状態」へ昇格させる。XML ドキュメント完備・配布準備・統合の入口（DI）整備が目的。実装ロジックは追加しない。

### 14.2 採用設計

**`OsmDotRoute.Extensions.DependencyInjection` プロジェクト**:

別アセンブリ分離し、本体 `OsmDotRoute` に `Microsoft.Extensions.DependencyInjection.Abstractions` 依存を持ち込まない（コアの依存方向は `System.*` のみという原則を維持）。

| 項目 | 内容 |
| --- | --- |
| TargetFramework | `net9.0`（本体と統一） |
| ProjectReference | `OsmDotRoute`, `OsmDotRoute.Itinero` |
| PackageReference | `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0 |
| 名前空間 | `OsmDotRoute.Extensions.DependencyInjection` |
| 公開型 | `ServiceCollectionExtensions`（静的拡張）, `OsmDotRouteOptions` |

**`AddOsmDotRoute` API**:

2 つのオーバーロード:

```csharp
// (a) RouterDb ファイルパスのみで登録
public static IServiceCollection AddOsmDotRoute(
    this IServiceCollection services, string routerDbPath);

// (b) Options 経由で詳細設定
public static IServiceCollection AddOsmDotRoute(
    this IServiceCollection services, Action<OsmDotRouteOptions> configure);
```

`(a)` は `(b)` へ委譲する形で実装。

**DI ライフタイム**:

| 型 | ライフタイム | 理由 |
| --- | --- | --- |
| `RouterDb` | Singleton | ファイルから一度ロード。内部は immutable |
| `RestrictedAreaService` | Singleton | プロセス全体で 1 つの動的制約集合を共有（REQ-RST-001 / REQ-RST-012 の即時反映前提） |
| `Router` | Singleton | `RouterDb` + `RestrictedAreaService` を内部保持、状態なし |

**XML ドキュメント**:

`Directory.Build.props` の `GenerateDocumentationFile` を `false` → **`true`** に切替。
テスト・ベンチマーク・サンプル csproj では個別に `<GenerateDocumentationFile>false</GenerateDocumentationFile>` を指定して上書き（XML doc 対象外）。

公開 20 型に `<summary>` / `<param>` / `<returns>` / `<exception>` を完備:

- 補填対象が多かった型: `RestrictedAreaService`（10+ メソッド）、`BlockArea`（3 ctor）、`DifficultyArea`（3 ctor）
- 軽微対応: `GeoPolygon` / `Route` / `RoadNetworkGeoJson` / `RouterDb` / `Router` / `MeshCode` / `VehicleProfile` / `InvalidProfileException` / `InvalidGmlException` / `ItineroRouterDbLoader`
- 既に完備済: `GeoCoordinate` / `DifficultyTypes` / `RouterDbStatistics` / `RestrictedAreaId` / `RestrictedArea` / `MeshLevel` / `MapBounds`

**README**:

Phase 0 のままだった README を全面書き換え。構成:

1. プロジェクト概要 + 特長
2. Phase 進行状況テーブル
3. インストール（ソース参照、Phase 1 では NuGet なし）
4. 最小利用サンプル（RouterDb 読込 → Router 構築 → 経路計算）
5. 動的制約の登録例（ポリゴン / メッシュ / GML）
6. DI 統合サンプル（`services.AddOsmDotRoute(path)`）
7. バージョニング方針（0.x 期間中は破壊的変更許容、REQ-API-008）
8. 親プロジェクトとの関係
9. MIT License 表記

### 14.3 設計判断の根拠

- **DI 拡張を別アセンブリに分離した理由**: コア `OsmDotRoute` の依存を `System.*` のみに保つ原則（REQ-DEP-001）。DI を使わないユーザーが `Microsoft.Extensions.DependencyInjection.Abstractions` を読まなくて済む
- **`Microsoft.Extensions.DependencyInjection.Abstractions` を選んだ理由**: 軽量で、`IServiceCollection` インタフェースのみ依存。`Microsoft.Extensions.DependencyInjection` 本体（コンテナ実装）には依存しない
- **DI 名前空間を `Microsoft.Extensions.DependencyInjection` ではなく `OsmDotRoute.Extensions.DependencyInjection` にした理由**: 拡張メソッドが標準名前空間に紛れ込むのを避け、明示的に `using OsmDotRoute.Extensions.DependencyInjection;` を書かせる方針
- **`RestrictedAreaService` を Singleton にした理由**: REQ-RST-012「シミュレーション中の制約変更は次回 `Router.Calculate` から即時反映」をサポートするには、`Router` と `RestrictedAreaService` が同一インスタンスを共有する必要がある
- **XML doc 警告を一気に解消できた理由**: 公開型が 20 個と少なく、`<summary>` 完全欠落はゼロだった。残課題は `<param>` / `<returns>` / `<exception>` 補填のみで、機械的に対応可能だった
- **`OsmDotRouteOptions` を Phase 1 から導入した理由**: 単純な `AddOsmDotRoute(path)` だけでなく、Step 13 (MapVerifier) で `DefaultProfile` 等を渡したくなる見込み。早期に拡張点を確保しておく

### 14.4 トレードオフ・制約

- **`OsmDotRouteOptions.DefaultProfile` は DI コンテナには登録しない**: Phase 1 時点では参照用途のみ（ユーザー利便のため保持）。将来必要なら `services.AddSingleton(options.DefaultProfile)` を追加
- **`NoWarn` を一切使用しない方針**: 本体 3 プロジェクトで XML doc 警告を完全に解消。今後新規 public API 追加時に `<summary>` 忘れがあるとビルド警告が出る運用
- **`TreatWarningsAsErrors=false` 据置**: Itinero 1.5.1 が `Nullable` 非対応で `OsmDotRoute.Itinero` 側に潜在的警告が残る既知問題（§3.4 参照）。Step 17 で最終評価
- **DI smoke test 未追加**: `ServiceProvider.GetRequiredService<Router>()` の整合テストは追加せず、`samples/ConsoleDemo` での手動確認に委ねる。`AddOsmDotRoute` の中身は 3 行の Singleton 登録のみで、ロジックがほぼないため

### 14.5 検証方法

- `dotnet build OsmDotRoute.sln`: 6 プロジェクト全て **0 警告 0 エラー**（XML doc 生成警告含む）
- `dotnet test`: 147/147 成功維持（新規テストなし、既存テストに非侵襲）
- 公開型 XML コメント完備の手動チェック（IDE での補完表示確認）
- README の GitHub プレビュー確認（手動）

### 14.6 実装メモ

- `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0 を採用。`net9.0` ターゲットに合わせた最新版
- `ServiceCollectionExtensions.AddOsmDotRoute(IServiceCollection, string)` は内部で `Action<OsmDotRouteOptions>` 版に委譲する 2 段構造
- `OsmDotRouteOptions.RouterDbPath` が未設定で `AddOsmDotRoute` を呼ぶと `InvalidOperationException`
- `Singleton` 登録時、`RouterDb` のロードは初回解決時の遅延実行（`AddSingleton(sp => ...)` ファクトリ形式）。コンテナ構築時にファイル I/O が走らないため、テスト容易性も維持
- README の Markdown テーブルは `| --- | --- | --- |` 形式（リンター MD060 互換）

---

## 15. 検証用地図アプリ MapVerifier

**対応ステップ**: ステップ 13〜14
**ステータス**: 別ドキュメントに分離

MapVerifier はライブラリ本体（OsmDotRoute）とライフサイクル・バージョン体系を独立させるため、
設計記録は **[map_verifier_design.md](map_verifier_design.md)** に分離する。本書本節は概要と
ポインタのみを置く。

**現バージョン**: MapVerifier 1.0.0（リリース、初版、2026-05-19）— Phase 1 機能要件の end-to-end 検証手段として実用稼働中

**主要トピック**（詳細は別ドキュメント参照）:

- §1 スコープと検証ゴール（7 シナリオ受入条件）
- §2 アーキテクチャ概観（`MapVerifier.Server` + `MapVerifier.Web`、ステップ 13/14 のスコープ分割）
- §3 プロジェクト構成（`samples/MapVerifier/` 配下、独自バージョン採番）
- §4 サーバー API 仕様（`/api/load` `/api/route` `/api/restrictions/{polygon,mesh,gml}` 等）
- §5 フロントエンド構成（Vite + React + MapLibre GL、Panel 別責務、メッシュグリッド クライアント生成）
- §7 SemVer バージョニング方針（OsmDotRoute 0.x との連動ルール）

---

## 16. ベンチマーク結果

**対応ステップ**: ステップ 15
**ステータス**: 初版（2026-05-20、市単位 RouterDb で計測完了）

詳細結果は [`phase1_benchmark_results.md`](phase1_benchmark_results.md) を参照。本章では設計上の含意のみまとめる。

### 16.1 達成状況サマリ

| 要件 | 目標 | 実測 | 判定 |
|---|---|---|---|
| REQ-NFR-001 | 経路計算 ≤ 100 ms | OsmDotRoute 33 ms / Itinero 69 ms | ✅ 達成（市単位、都道府県単位は要追加検証） |
| REQ-NFR-002 | 制約 100 件下でも維持 | C3 = 51 ms（C0 比 1.43x） | ✅ 達成 |
| REQ-NFR-003 | 16 GB RAM で動作可能 | 定常 WorkingSet 54 MB | ✅ 達成 |
| Itinero 比較 | Mean 比 ≤ 1.0x | 0.48x | ✅ Lua インタプリタ非依存の優位性が定量的に証明 |
| 経路距離同等性 | 両方成功ペアで ±10% 以内 | 89/89 ペアで達成（Mean 乖離 0.07%） | ✅ 達成 |

### 16.2 計測対象 RouterDb の規模

親プロジェクト借用の `default.routerdb`: 43,685 頂点 / 57,331 エッジ（11km × 11km の市単位）。要件の「都道府県単位 数百万エッジ」より小さい。都道府県単位での最終確認はユーザー判断（CLAUDE.md ルール、Phase 1 完了判定）。

### 16.3 設計上の含意

- **§7a JSON プロファイル基盤の効果が定量化された**: Itinero の Lua インタプリタを介さないネイティブ C# 評価により、経路 1 本あたり **約 2 倍** の速度差。StdDev も 7 分の 1（OsmDotRoute 2.8 ms vs Itinero 21 ms）で実行揺らぎが小さい
- **§5b 独自 Dijkstra の妥当性**: 全 100 ペアの距離乖離 Mean 0.07%、Max 3.09%。Itinero との機能パリティを維持しつつ、8 ペアで Itinero が見つけられない経路を追加発見（スナップ・探索の許容範囲が広い、好ましい挙動）
- **§10 制約管理基盤の AABB プリフィルタが期待通り機能**: 制約 100 件下でも C0 比 1.43x、StdDev 揺らぎは大きいが Mean は許容範囲。空間インデックス（`SpatialIndex`）が現状で必要十分
- **メモリ効率は Phase 2 で改善余地**: 経路 1 本あたり 77 MB アロケート（Itinero の 2.4 倍）。`RoadEdge` / `Shape` のコピーを Span/Memory ベースに切替えれば削減可能。Phase 2 の独自グラフ形式設計時に検討

### 16.4 ベンチ実装の所在

- プロジェクト: [`tests/OsmDotRoute.Benchmarks/`](../tests/OsmDotRoute.Benchmarks/)
- 5 ベンチクラス: `RouterDbLoadBenchmark` / `RouteCalculationBenchmark` / `ItineroBaselineBenchmark` / `RouteWithConstraintsBenchmark` (C0〜C4) / `SnapBenchmark`
- 補助ツール: `--memory-probe`（GC 強制後の定常メモリ実測） / `--verify-parity`（OsmDotRoute vs Itinero の経路発見 4 区分と距離乖離検証）
- テストデータ: `route-pairs.json`（100 ペア、seed 20260520）、`restrictions-mixed-100.json` (seed 20260521)、`restrictions-block-100.json` (seed 20260522) — 決定論的に再現可能、コミット対象

### 16.5 未解決の観測

- **C4 (Block 100 件) の Mean 5.84 ms**: 期待した「`PositiveInfinity` 短絡効果」ではなく、小規模ネットワークの完全分断による「経路発見不可・null 即返し」が支配的と推定。都道府県単位 RouterDb での再計測時に再評価
- **DifficultyArea の迂回拡大コスト**: ランダム配置の本ベンチでは捕捉不可。計画書 §7.4 のとおり、ステップ 17 で MapVerifier 上の手動シナリオ確認に回す

---

## 17. 親プロジェクト統合

**対応ステップ**: ステップ 16
**ステータス**: 未記述

（記述予定項目: 親プロジェクト検証ブランチ名、`<ProjectReference>` 追加内容、`MapService.cs` の書き換え方針、`using Itinero` 消去結果、既存テスト通過状況、動作確認シナリオの結果、本体マージ判断のためのユーザー観点）

---

## 18. 制約事項と既知の課題

（実装中・完了後に発見した制約事項・既知の課題・Phase 2 以降への申し送り事項をここに集約）

---

## 19. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 (ひな形) | 2026-05-18 | ひな形作成。各章は対応ステップ完了時に記述 | Claude (Opus 4.7) |
| 0.2 (進行中) | 2026-05-18 | Phase 1 着手。ステータスを「進行中」に変更 | Claude (Opus 4.7) |
| 0.3 (進行中) | 2026-05-18 | ステップ 1 完了。§3「プロジェクト構成」記述（ソリューション構造／プロジェクト依存マトリクス／`InternalsVisibleTo`／`Directory.Build.props`／`.editorconfig`／`LICENSE`／設計判断とトレードオフ／検証結果） | Claude (Opus 4.7) |
| 0.4 (進行中) | 2026-05-18 | §2.5「Profile 戦略」を新設（JSON 外部化方針、フェーズ毎の発展、難所タイプ設計分離、組込み 8 タイプ、重複ルール）。§7 を 7a（JSON プロファイル基盤）+ 7b（独自 Dijkstra）に分割。§0.3 章対応表更新 | Claude (Opus 4.7) |
| 0.5 (進行中) | 2026-05-18 | ステップ 2 完了。§4「公開型カタログ」記述（公開 15 型一覧、`VehicleProfile` enum→class 変更点、設計判断とトレードオフ、検証結果）。Class1.cs 削除済 | Claude (Opus 4.7) |
| 0.6 (進行中) | 2026-05-18 | ステップ 3 完了。§5「Itinero アダプター」記述（`IRoadGraph` 抽象、`ItineroRoadGraph`/`ItineroRouterDbLoader` 実装、6/6 テスト成功）。§2 アーキテクチャ概観に レイヤー構造図・プロジェクト依存図・データフロー・名前空間表を追加。`RouterDb.LoadFromFile` 削除→`ItineroRouterDbLoader.LoadFromFile` 移動（要件 v1.3 で追従予定） | Claude (Opus 4.7) |
| 0.7 (進行中) | 2026-05-18 | ステップ 4 完了。§6「道路スナップ」記述（`IRoadSnapper` 抽象 + `SnapResult` 内部値型、`ItineroSnapper` 実装、`Router.SnapToRoad` 実装、6 テスト追加で計 12/12 成功）。`RouterDb` 内部コンストラクタを `(IRoadGraph, IRoadSnapper)` に拡張 | Claude (Opus 4.7) |
| 0.8 (進行中) | 2026-05-18 | ステップ 5a 完了。§7a「JSON プロファイル基盤」記述（DTO 構造、`ProfileEvaluator` 8 ステップ評価、`speedMultiplier` 追加、hard-deny セマンティクス、埋込 `car.json`/`pedestrian.json` 同梱、`InvalidProfileException`、25 単体 + 2 パリティテスト = 計 46/46 成功）。Itinero `Vehicle.Car.Fastest()` とのパリティ: 通行可否 0/52 mismatch、速度 >10% 乖離 9/52 (17%) | Claude (Opus 4.7) |
| 0.17 (進行中) | 2026-05-19 | ステップ 12 完了。§14「DI 拡張とドキュメント」記述（`OsmDotRoute.Extensions.DependencyInjection` プロジェクト新設、`AddOsmDotRoute` 2 オーバーロード、Singleton ライフタイム、`OsmDotRouteOptions`）。`Directory.Build.props` の `GenerateDocumentationFile` を `true` に切替（テスト/ベンチマーク/サンプル csproj で `false` 上書き）。公開 20 型に `<param>`/`<returns>`/`<exception>` 完備。`README.md` 全面書き換え（Phase 0 → Phase 1 進行中、最小サンプル、DI 統合、0.x 期間中の破壊的変更方針 REQ-API-008、Phase ロードマップ）。6 プロジェクト・147/147 テスト・0 警告維持。§2.2/2.4/3.2/3.4 にも追記反映 | Claude (Opus 4.7) |
| 0.19 (進行中) | 2026-05-20 | **ステップ 15 (ベンチマーク・性能検証) 完了**。§16 「ベンチマーク結果」を初版記述。市単位 (津島市、57k エッジ) で REQ-NFR-001〜003 を全件達成: 経路計算 33ms / Itinero 比 **0.48x** (Lua インタプリタ非依存の優位性が定量証明) / 制約 100 件下 51ms (C0 比 1.43x) / 定常 WorkingSet 54MB。経路同等性検証で OsmDotRoute / Itinero の 89 両方成功ペア中 100% が距離 ±10% 以内、Itinero が見つけられない経路を 8 件追加発見。`tests/OsmDotRoute.Benchmarks/` に 5 ベンチクラス (Load / Calc / Itinero / WithConstraints C0〜C4 / Snap) + 補助ツール (--memory-probe / --verify-parity) + 決定論的 TestData (route-pairs / restrictions-mixed / restrictions-block の 3 JSON) を実装。詳細は [phase1_benchmark_results.md](phase1_benchmark_results.md) 参照。要件定義書 v2.0 連動、REQ-NFR-001〜003 は「条件付き完了」コメント追記済、都道府県単位 RouterDb での最終確認はステップ 17 へ送り | Claude (Opus 4.7) |
| 0.18 (進行中) | 2026-05-19 | ステップ 14 (検証用地図アプリ) 経由で Phase 1 機能要件全件の end-to-end 検証完了。**Lib 追加**: `MeshCode.ToBounds()` + `MeshCode.EnumerateInBounds(MapBounds, MeshLevel)` を公開 API に追加（MapVerifier のメッシュグリッド GeoJSON 生成用、二重実装回避）、関連テスト 6 件追加で 153/153 維持。**§15 更新**: MapVerifier の現バージョンを 1.0.0（初版リリース）へ反映（独立 SemVer、設計書 `map_verifier_design.md` 参照）。**要件定義書 v1.9 連動**: REQ-RTE-001〜008 / REQ-RST-001〜018,020〜022,024〜028,030〜032,040 / REQ-PRF-001〜002,007〜014 / REQ-MAP-001〜002 / REQ-API-001〜004 / REQ-FMT-001〜003 / REQ-DEP-001 / REQ-LIC-002〜003 / REQ-NFR-005〜006,009〜010 を全件完了マーク。残作業は性能ベンチマーク (Step 15、REQ-NFR-001〜003) と親プロジェクト統合 (Step 16) のみ | Claude (Opus 4.7) |
