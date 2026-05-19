# OsmDotRoute Phase 1 実装計画書

**バージョン**: 1.1（確定）
**作成日**: 2026-05-18
**最終更新**: 2026-05-18
**ステータス**: 確定（ユーザー承認済み、Phase 1 進行中・ステップ 1 完了）
**対象フェーズ**: Phase 1（経路探索エンジン独自化）
**関連ドキュメント**:

- [要件定義書](requirement_definition.md)（v1.1 確定）
- [Phase 1 設計書](phase1_design.md)（v0.1 ひな形、**各ステップ完了時に該当章を更新する**）
- [Itinero 動的制約 調査結果](reference/Itinero動的制約_調査結果.md)（参考用コピー、編集禁止）
- [Itinero 動的制約 PoC 計画](reference/Itinero動的制約_PoC計画.md)（参考用コピー、編集禁止）

---

## 1. 概要

本書は OsmDotRoute Phase 1（経路探索エンジン独自化）の実装計画を定める。

**Phase 1 のゴール**:
1. 親プロジェクト `災害廃棄物処理シミュレーション` の `MapService.cs` から `using Itinero;` を完全に消去できる API を提供する
2. 動的制約（進入不可・移動困難エリア）を次回経路計算から反映できる Dijkstra ベース経路探索を実装する
3. 都道府県単位グラフで 1 経路 100ms 以内（REQ-NFR-001）の性能を達成する

**Phase 1 の方針**:
- グラフデータ層（OSM PBF パース、`RouterDb` 永続化）は Itinero 1.5.1 に任せる
- 経路探索ロジックは独自実装（Itinero ソースをコピーしない／Apache 2.0 違反回避）
- Itinero への依存はアダプター層に閉じ込め、公開 API には Itinero 型を一切露出させない

---

## 2. 前提条件

- [x] Phase 0（要件定義）完了（2026-05-18）
- [x] 要件定義書 v1.1 確定
- [x] `git init` 完了（ブランチ `main`、root commit `4b3806c`）
- [ ] 本実装計画書のユーザー合意
- [ ] 親プロジェクトの開発スケジュールとの調整（必要な場合のみ）

---

## 2.5 設計書の同時更新ルール

**Phase 1 実装中は、各ステップ完了時に設計書 [`phase1_design.md`](phase1_design.md) の対応章を必ず更新する**。これにより、会話セッションが切れたり日を跨いだりしても、後から実装内容を把握できる状態を保つ。

### 適用ルール

- ステップ着手時: 設計書の対応章を一読し、既存設計との整合性を確認
- ステップ完了時: 設計書の対応章に「意図 / 採用設計 / 設計判断の根拠 / トレードオフ / 検証方法 / 実装メモ」を記述（§0.4 のテンプレート準拠）
- ユーザー報告時: 設計書の更新済み章を一緒に確認できる状態にする
- 後続ステップで設計変更が発生した場合: 関係する過去章にも追記し、両章が矛盾しないようにする

### ステップ ↔ 設計書章 対応表

設計書 [`phase1_design.md`](phase1_design.md) §0.3 を参照。

---

## 3. 採用アプローチ

### 3.1 経路探索アプローチ

[`Itinero動的制約_調査結果.md`](reference/Itinero動的制約_調査結果.md) の結論に従い、**Approach A: 独自 Dykstra 実装** を採用する。

**根拠**:
- Itinero の `Profile.FactorAndSpeed()` / `WeightHandler.Calculate()` は引数にエッジ ID / 座標が渡されない設計のため、空間制約フックを後付けできない（調査結果 §1）
- Itinero 公開 Graph API（`RouterDb.Network` 系）は十分なエッジ列挙・座標取得機能を持つ（調査結果 §2.2）
- 独自実装にすることで Itinero のバージョンアップ・廃止リスクから経路探索ロジックを完全に切り離せる
- ライセンス（MIT 公開）の観点で Itinero ソース由来コードの混入リスクをゼロにできる

### 3.2 Itinero 依存の閉じ込め戦略

- 独自プロジェクト `OsmDotRoute.Itinero`（アダプターアセンブリ）に Itinero への参照を集約
- メイン `OsmDotRoute` プロジェクトは Itinero を直接参照しない（公開 API に Itinero 型を一切露出させない）
- Phase 2 で `OsmDotRoute.Itinero` を破棄し、独自グラフ形式に置き換えやすい構造とする

### 3.3 グラフ抽象化

経路探索本体は `IRoadGraph` インターフェース（仮称）に依存させ、実装を差し替え可能とする:

- **Phase 1**: `ItineroRoadGraph`（`OsmDotRoute.Itinero` 内、Itinero `RouterDb.Network` をラップ）
- **Phase 2**: `NativeRoadGraph`（独自バイナリ形式を直接読み込む）
- **Phase 3**: `NativeRoadGraph` を OSM PBF から直接ビルド

---

## 4. Phase 1 スコープ確認（要件対応表）

Phase 1 で実装する要件一覧（要件定義書から抽出）:

### 4.1 経路探索コア（REQ-RTE）

| ID | 概要 | 優先度 |
|---|---|---|
| REQ-RTE-001 | Dijkstra ベース最短経路、独自 `Route` 型返却 | P1 |
| REQ-RTE-002 | 任意座標の道路スナップ | P1 |
| REQ-RTE-003 | スナップ検索半径（メートル）指定 | P1 |
| REQ-RTE-004 | 道路ネットワーク GeoJSON 出力 | P1 |
| REQ-RTE-005 | 同期 API 基本提供 | P1 |
| REQ-RTE-006 | 経路未発見時は `null` 返却 | P2 |
| REQ-RTE-007 | 距離・所要時間・形状を結果に含める | P2 |
| REQ-RTE-008 | ネットワーク外座標で `null` 返却 | P2 |

### 4.2 動的制約管理（REQ-RST）

| ID | 概要 | 優先度 |
|---|---|---|
| REQ-RST-001〜003 | 進入不可エリア登録（ポリゴン／メッシュ／メッシュ一括） | P1 |
| REQ-RST-004〜006 | 難所エリア登録（ポリゴン／メッシュ／メッシュ一括、難所タイプ指定） | P1 |
| REQ-RST-007 | 難所タイプ文字列の引数検証 | P1 |
| REQ-RST-008〜009 | 個別削除／全クリア | P1 |
| REQ-RST-010〜011 | タグ管理・一覧取得 | P2 |
| REQ-RST-012 | 次回計算から即時反映 | P1 |
| REQ-RST-013〜015 | エッジシェイプ交差判定／AABB 事前フィルタ／メッシュ AABB 直接利用 | P2 |
| REQ-RST-016〜018 | メッシュ階層 4 種対応・桁数判定・引数例外 | P1 |
| REQ-RST-020〜023 | GeoJSON 入力（Polygon／MultiPolygon／Hole／FeatureCollection） | P1 |
| REQ-RST-024〜028 | GeoJSON ファイル／文字列／`difficulty` キー対応／WGS84 | P2 |
| REQ-RST-030〜032 | 難所重複時の積方式・短絡評価・進入不可優先 | P1 |

### 4.3 車両プロファイル（REQ-PRF）

| ID | 概要 | 優先度 |
|---|---|---|
| REQ-PRF-001 | 同梱プロファイル `car`（JSON 外部化） | P1 |
| REQ-PRF-002 | 同梱プロファイル `pedestrian`（JSON 外部化） | P1 |
| REQ-PRF-007 | 外部 JSON ファイル化（リビルド不要） | P1 |
| REQ-PRF-008 | アセンブリ埋込リソースで同梱 | P1 |
| REQ-PRF-009 | ユーザー独自プロファイル読込 API | P1 |
| REQ-PRF-010 | プロファイル JSON スキーマ規定 | P1 |
| REQ-PRF-011 | 難所タイプ毎の速度低下係数・通行可否を保持 | P1 |
| REQ-PRF-012 | 組込み 8 難所タイプ（冠水/液状化/土砂崩れ/工事中/障害物/交通集中/積雪/凍結） | P1 |
| REQ-PRF-013 | ユーザー定義難所タイプ追加可 | P1 |
| REQ-PRF-014 | 未知タイプには `difficultyDefault` 適用 | P1 |

### 4.4 地図データ・グラフ（REQ-MAP）

| ID | 概要 | 優先度 |
|---|---|---|
| REQ-MAP-001 | Itinero RouterDb（`.routerdb`）読み込み | P1 |
| REQ-MAP-002 | 頂点数・辺数・経緯度範囲の統計取得 | P2 |

### 4.5 公開 API 設計（REQ-API）

| ID | 概要 | 優先度 |
|---|---|---|
| REQ-API-001 | `OsmDotRoute.Router` ファサード | P1 |
| REQ-API-002 | 独自 `Route` 型（Itinero 型非露出） | P1 |
| REQ-API-003 | 親プロジェクトから `using Itinero;` 消去可能 | P1 |
| REQ-API-004 | `RestrictedAreaService` クラス | P1 |
| REQ-API-005 | DI 拡張メソッド `AddOsmDotRoute()` | P2 |
| REQ-API-006 | XML ドキュメンテーションコメント完備 | P2 |
| REQ-API-008 | README に 0.x 期間中の破壊的変更方針を明記 | P3 |

### 4.6 データフォーマット（REQ-FMT）

| ID | 概要 | 優先度 |
|---|---|---|
| REQ-FMT-001〜003 | `Route` 型（距離・所要時間・形状） | P1 |
| REQ-FMT-004 | 経路 → GeoJSON LineString 変換 | P2 |

### 4.7 非機能要件（REQ-NFR）

| ID | 概要 | 優先度 |
|---|---|---|
| REQ-NFR-001 | 都道府県単位 100ms 以内 | P1 |
| REQ-NFR-002 | 制約 100 件登録時も性能維持 | P1 |
| REQ-NFR-003 | 16GB RAM で動作 | P2 |
| REQ-NFR-005 | .NET 9 動作 | P1 |
| REQ-NFR-006 | Windows 10/11 x64 動作 | P1 |
| REQ-NFR-009 | 日本国内前提 | P1 |
| REQ-NFR-010 | メートル法のみ | P1 |

### 4.8 配布・ライセンス・依存（REQ-PKG / REQ-LIC / REQ-DEP）

| ID | 概要 | 優先度 |
|---|---|---|
| REQ-PKG-001 | `<ProjectReference>` で親プロジェクトから参照可能 | P1 |
| REQ-LIC-001 | MIT License | P1 |
| REQ-LIC-002〜003 | Itinero ソース非コピー、NuGet 経由のみ | P1 |
| REQ-DEP-001 | ランタイム依存は Itinero 1.5.1 + System.* のみ | P1 |

---

## 5. プロジェクト・ソリューション構成

### 5.1 ディレクトリ構造（案）

```
DotRoute/
├── OsmDotRoute.sln
├── LICENSE                                  # MIT
├── README.md
├── Documents/                               # 既存
├── src/
│   ├── OsmDotRoute/                         # メインライブラリ（Itinero 非依存）
│   │   ├── OsmDotRoute.csproj               # net9.0
│   │   ├── Router.cs                        # 公開ファサード
│   │   ├── RouterDb.cs                      # 公開（内部で Itinero RouterDb を保持）
│   │   ├── Route.cs                         # 公開型
│   │   ├── GeoCoordinate.cs                 # 公開型
│   │   ├── GeoPolygon.cs                    # 公開型
│   │   ├── MeshCode.cs                      # 公開型（JIS X0410）
│   │   ├── VehicleProfile.cs                # 公開クラス（JSON 読込・Car/Pedestrian static）
│   │   ├── DifficultyTypes.cs               # 公開 const string（組込み 8 タイプ）
│   │   ├── Profiles/
│   │   │   ├── car.json                     # 埋込リソース
│   │   │   ├── pedestrian.json              # 埋込リソース
│   │   │   ├── JsonProfileDefinition.cs     # internal DTO
│   │   │   └── ProfileEvaluator.cs          # internal、OSM タグ → (canPass, speed)
│   │   ├── Restrictions/
│   │   │   ├── RestrictedAreaService.cs
│   │   │   ├── RestrictedArea.cs
│   │   │   ├── RestrictedAreaId.cs
│   │   │   ├── BlockArea.cs (internal)
│   │   │   └── DifficultyArea.cs (internal)
│   │   ├── Routing/
│   │   │   ├── IRoadGraph.cs                # 内部抽象
│   │   │   ├── DijkstraEngine.cs            # 独自 Dijkstra
│   │   │   ├── EdgeWeightCalculator.cs
│   │   │   └── RouteBuilder.cs
│   │   ├── Geometry/
│   │   │   ├── Aabb.cs
│   │   │   ├── PolygonIntersection.cs
│   │   │   └── SpatialIndex.cs              # AABB 配列 or R-tree（実装ステップ次第）
│   │   ├── Mesh/
│   │   │   └── MeshCodeConverter.cs         # JIS X0410 ↔ 緯度経度
│   │   ├── GeoJson/
│   │   │   ├── GeoJsonParser.cs             # System.Text.Json ベース
│   │   │   └── GeoJsonWriter.cs
│   │   └── DependencyInjection/
│   │       └── ServiceCollectionExtensions.cs
│   └── OsmDotRoute.Itinero/                 # アダプター層（Phase 2 で破棄）
│       ├── OsmDotRoute.Itinero.csproj       # net9.0、Itinero 1.5.1 参照
│       ├── ItineroRouterDbLoader.cs
│       ├── ItineroRoadGraph.cs              # IRoadGraph 実装
│       └── ItineroSnapper.cs                # スナップ機能（既存 Router.Resolve 流用）
├── tests/
│   ├── OsmDotRoute.Tests/                   # 単体テスト
│   │   └── OsmDotRoute.Tests.csproj
│   └── OsmDotRoute.Benchmarks/              # 性能ベンチマーク
│       └── OsmDotRoute.Benchmarks.csproj
└── samples/                                  # 任意・最小サンプル
    ├── ConsoleDemo/                          # コンソール手動検証用
    └── MapVerifier/                          # 検証用地図アプリ（親プロ Viewer 流用簡素化）
        ├── MapVerifier.Server/               # ASP.NET Core 最小 API
        │   ├── MapVerifier.Server.csproj     # net9.0、OsmDotRoute 群参照
        │   └── Program.cs                    # /api/* エンドポイント定義
        └── MapVerifier.Web/                  # Vite + React + MapLibre GL
            ├── package.json
            ├── vite.config.ts
            └── src/
                ├── main.tsx
                ├── App.tsx
                ├── MapView.tsx               # MapLibre GL ラッパー
                ├── components/
                │   ├── LoadPanel.tsx         # RouterDb 読込・統計表示
                │   ├── MapBoundsPanel.tsx    # 表示範囲指定
                │   ├── MeshGridPanel.tsx     # メッシュ種別切替・グリッド描画
                │   ├── PolygonEditorPanel.tsx# マウス描画・座標入力
                │   ├── RoutePanel.tsx        # 起終点指定・経路計算
                │   └── RestrictionListPanel.tsx # 制約一覧・削除
                └── api/
                    └── osmDotRouteClient.ts  # /api/* HTTP クライアント
```

### 5.2 プロジェクト構成・依存関係

| プロジェクト | TargetFramework | 主要依存 | 役割 |
|---|---|---|---|
| `OsmDotRoute` | net9.0 | System.* のみ | 公開 API、経路探索本体、制約管理、メッシュ、GeoJSON |
| `OsmDotRoute.Itinero` | net9.0 | `OsmDotRoute`, Itinero 1.5.1 | RouterDb 読込・グラフアダプター・スナップ |
| `OsmDotRoute.Tests` | net9.0 | `OsmDotRoute`, `OsmDotRoute.Itinero`, xUnit | 単体テスト |
| `OsmDotRoute.Benchmarks` | net9.0 | `OsmDotRoute`, `OsmDotRoute.Itinero`, BenchmarkDotNet | 性能ベンチ |
| `ConsoleDemo` | net9.0 | `OsmDotRoute`, `OsmDotRoute.Itinero` | 手動動作確認用 |
| `MapVerifier.Server` | net9.0 | `OsmDotRoute`, `OsmDotRoute.Itinero`, ASP.NET Core | 検証用 Web API |
| `MapVerifier.Web` | - | React 18, Vite, MapLibre GL, TypeScript | 検証用フロントエンド |

**Note**:
- 親プロジェクト統合時には `OsmDotRoute` と `OsmDotRoute.Itinero` の両方を ProjectReference する想定（REQ-DEP-001）
- `samples/` 配下の依存（ASP.NET Core、MapLibre GL 等）はライブラリ本体の依存関係（REQ-DEP-001）に**含めない**。あくまで検証用ツールの依存
- `MapVerifier.Web` は親プロジェクト `App/DisasterWasteSim.Viewer/` の構成（React + Vite + MapLibre GL）を**流用しつつ大幅に簡素化**。SignalR・Recharts 等の不要依存は外す

### 5.3 公開 API 露出方針

- **公開する型**: `Router`, `RouterDb`, `Route`, `GeoCoordinate`, `GeoPolygon`, `MeshCode`, `MeshLevel`, `VehicleProfile`（クラス）, `DifficultyTypes`（const 集約）, `RestrictedAreaService`, `RestrictedArea`, `RestrictedAreaId`, `BlockArea`, `DifficultyArea`（DI 拡張メソッドは別途）
- **internal にする型**: `IRoadGraph`, `DijkstraEngine`, `EdgeWeightCalculator`, `RouteBuilder`, `Aabb`, `PolygonIntersection`, `SpatialIndex`, `MeshCodeConverter`, `GeoJsonParser`, `GeoJsonWriter`, `JsonProfileDefinition`, `ProfileEvaluator`
- **`OsmDotRoute.Itinero` の公開度**: `ItineroRouterDbLoader` のみ public、それ以外 internal
- **`InternalsVisibleTo`**: `OsmDotRoute.Itinero` → `OsmDotRoute` の internal にアクセス可とする（`IRoadGraph` 実装のため）

### 5.4 確定すべきユーザー判断事項

以下は本計画書のレビュー時にユーザー判断を仰ぐ:

- [ ] テストフレームワーク: xUnit / NUnit / MSTest（提案: **xUnit**）
- [ ] ベンチマークフレームワーク: BenchmarkDotNet（提案: **採用**）
- [ ] DI 拡張用パッケージ: `Microsoft.Extensions.DependencyInjection.Abstractions` を `OsmDotRoute` 本体に追加するか、別アセンブリ `OsmDotRoute.Extensions.DependencyInjection` に分離するか（提案: **別アセンブリに分離**、本体の System.* 限定方針を維持）
- [ ] サンプル `ConsoleDemo` プロジェクトの要否（提案: **作成**、手動検証で活用）
- [ ] 検証用地図アプリ `MapVerifier` の技術スタック（提案: **ASP.NET Core 最小 API + Vite + React 18 + MapLibre GL + TypeScript**、親プロジェクト Viewer の流用簡素化）
- [ ] `MapVerifier.Web` のポリゴン描画ツール（提案: **自前実装**で十分。MapLibre GL のクリックイベントで頂点追加、GeoJSON ソース更新。`terra-draw` / `maplibre-geoman-free` 等の追加依存は避ける）

---

## 6. モジュール構成

### 6.1 公開 API レイヤー（要件 REQ-API-001〜004, REQ-FMT-001〜003）

要件定義書 §7.1 のシグネチャを基準とする。Phase 1 では以下を実装:

- `Router(RouterDb, RestrictedAreaService?)`
- `Router.Calculate(VehicleProfile, GeoCoordinate, GeoCoordinate) → Route?`
- `Router.SnapToRoad(VehicleProfile, GeoCoordinate, float) → GeoCoordinate?`
- `Router.GetRoadNetworkGeoJson() → RoadNetworkGeoJson`
- `RouterDb.LoadFromFile(string) → RouterDb`（内部で `OsmDotRoute.Itinero.ItineroRouterDbLoader` 呼出）
- `RestrictedAreaService` の全公開メソッド（要件定義書 §7.1）

### 6.2 経路探索エンジン（要件 REQ-RTE-001〜008）

- **`IRoadGraph`**: グラフ抽象。頂点列挙・エッジ列挙・座標取得・シェイプ列取得・**エッジ OSM タグ取得** を提供
- **`DijkstraEngine`**: 単方向 Dijkstra（バイナリヒープ）
- **`EdgeWeightCalculator`**: エッジ重み = 距離 / 速度（`ProfileEvaluator` 経由）を基本とし、`RestrictedAreaService` を参照して制約を反映
- **`RouteBuilder`**: Dijkstra の親頂点配列から `Route` を構築（距離・所要時間・シェイプ集約）

### 6.2.b プロファイル評価（要件 REQ-PRF-001〜002, REQ-PRF-007〜014）

- **`VehicleProfile`**: 公開クラス。`Car` / `Pedestrian` 静的プロパティで埋込 JSON ロード。`LoadFromJsonFile` / `LoadFromJsonString` / `LoadFromJsonStream` でユーザー定義読込
- **`JsonProfileDefinition`**: `System.Text.Json` DTO。`highway` / `accessTagKeys` / `accessValueMap` / `fallback` / `difficulty` / `difficultyDefault` フィールド
- **`ProfileEvaluator`**: OSM タグ集合 → `(canPass: bool, speedKmh: float, oneway: OnewayDirection)` 評価。スキーマルールに従う
- **`DifficultyTypes`**: `public static class` に組込み 8 タイプを `const string` 集約（IDE 補完用）

### 6.3 動的制約管理（要件 REQ-RST-001〜015, REQ-RST-030〜032）

- **`RestrictedAreaService`**: `ConcurrentDictionary<RestrictedAreaId, RestrictedArea>` で制約を保持
- **`SpatialIndex`**: 登録された制約 AABB の配列（Phase 1 は配列線形走査で十分。100 件規模、AABB プリフィルタ前提）。性能要件未達なら R-tree 化を検討
- **`PolygonIntersection`**: エッジシェイプ線分と多角形の交差判定（AABB プリフィルタ後）
- **`DifficultyArea` 適用**:
  1. プロファイルの `difficulty[<type>]` を参照（未定義は `difficultyDefault`）
  2. `canPass: false` ならエッジ通行不可（短絡、REQ-RST-031）
  3. 複数の難所交差時は速度係数の積を取る（REQ-RST-030）
  4. 進入不可（BlockArea）と重複する場合は進入不可を優先（REQ-RST-032）

### 6.4 メッシュコード処理（要件 REQ-RST-016〜018）

- **`MeshCode`**: `long` 値の readonly record struct、桁数で階層を自動判定
- **`MeshCodeConverter`**: メッシュコード → 緯度経度 AABB 変換
  - 8 桁: 第3次（1km）
  - 9 桁: 1/2 細分（500m）
  - 10 桁: 1/4 細分（250m）
  - 11 桁: 1/10 細分（100m）
  - 桁数不一致: `ArgumentException`（REQ-RST-018）
- **参考実装**: 親プロジェクトの `Documents/標準地域メッシュ計算方法.md`（コピー禁止・参照のみ）

### 6.5 GeoJSON 入出力（要件 REQ-RST-020〜028, REQ-RTE-004, REQ-FMT-004）

- **`GeoJsonParser`**: `System.Text.Json` ベース
  - Polygon / MultiPolygon / FeatureCollection 対応
  - Hole（2 番目以降のリング）対応
  - `properties.speedFactor`（`double`、0.0〜1.0）読取
  - `properties.tag`（`string`）読取
  - WGS84（経度→緯度の順）（RFC 7946 準拠）
- **`GeoJsonWriter`**: `Route` → LineString、道路ネットワーク → FeatureCollection

### 6.6 DI 統合（要件 REQ-API-005）

- 別アセンブリ案: `OsmDotRoute.Extensions.DependencyInjection`
- `services.AddOsmDotRoute(routerDbPath)` で `RouterDb` / `Router` / `RestrictedAreaService` を Singleton 登録

---

## 7. 実装ステップ一覧

| # | ステップ | 主要要件 | 状態 |
|---|---|---|---|
| 1 | ソリューション・プロジェクト基盤構築 | REQ-NFR-005〜006, REQ-LIC-001, REQ-DEP-001 | 未着手 |
| 2 | 公開型のスケルトン定義 | REQ-API-001〜004, REQ-FMT-001〜003 | 未着手 |
| 3 | Itinero アダプター（RouterDb 読込・グラフ抽象実装・OSM タグ取得 API） | REQ-MAP-001〜002 | 未着手 |
| 4 | 道路スナップ機能 | REQ-RTE-002〜003, REQ-RTE-008 | 未着手 |
| 5a | JSON プロファイル基盤（スキーマ・`ProfileEvaluator`・`car.json`/`pedestrian.json`） | REQ-PRF-001〜002, REQ-PRF-007〜014 | 未着手 |
| 5b | 独自 Dijkstra（制約なし、JsonVehicleProfile 使用） | REQ-RTE-001, REQ-RTE-005〜007 | 未着手 |
| 6 | 道路ネットワーク GeoJSON 出力 | REQ-RTE-004 | 未着手 |
| 7 | メッシュコード変換実装 | REQ-RST-016〜018 | 未着手 |
| 8 | 制約管理基盤（AABB・多角形交差・サービスクラス・難所エリア） | REQ-RST-001〜011, REQ-RST-013〜015 | 未着手 |
| 9 | 制約対応 Dijkstra への統合（難所重複ルール込み） | REQ-RST-012, REQ-RST-030〜032 | 未着手 |
| 10 | GeoJSON 入力対応（`properties.difficulty` キー対応） | REQ-RST-020〜028 | 未着手 |
| 11 | 経路 → GeoJSON 出力ユーティリティ | REQ-FMT-004 | 未着手 |
| 12 | DI 拡張・XML ドキュメンテーション・README 整備 | REQ-API-005〜006, REQ-API-008, REQ-LIC-001 | 未着手 |
| 13 | 検証用地図アプリ - サーバー API + 地図基盤・範囲指定 | REQ-API-001〜004, REQ-RTE-001〜004（検証手段） | 未着手 |
| 14 | 検証用地図アプリ - メッシュ表示・ポリゴン作成・経路 UI | REQ-RST-001〜028, REQ-RTE-001〜002（検証手段） | 未着手 |
| 15 | ベンチマーク・性能検証 | REQ-NFR-001〜003 | 未着手 |
| 16 | 親プロジェクト統合・パリティ検証 | REQ-API-003, REQ-PKG-001 | 未着手 |
| 17 | ユーザー検証・Phase 1 確定 | — | 未着手 |

各ステップ完了時に **ユーザー報告 → 承認 → 次ステップ着手** のサイクルを厳守する（CLAUDE.md ルール）。

---

## 8. ステップ詳細

### ステップ 1: ソリューション・プロジェクト基盤構築

**目的**: ビルド可能な空のソリューションと各プロジェクトを準備する。

**作業**:
- `dotnet new sln -n OsmDotRoute`
- 5.1 のディレクトリ構造に従い空プロジェクトを生成（`dotnet new classlib`, `dotnet new xunit`, `dotnet new console`）
- ソリューションにプロジェクト追加（`dotnet sln add ...`）
- `OsmDotRoute.Itinero.csproj` に Itinero 1.5.1 / Itinero.IO.Osm 1.5.1 NuGet 参照を追加
- `Directory.Build.props` で共通設定（`LangVersion`, `Nullable enable`, `TreatWarningsAsErrors=true` 等）
- ルートに `LICENSE`（MIT）を配置
- `.editorconfig` 作成（必要に応じ）

**完了判定**:
- `dotnet build OsmDotRoute.sln` 成功（0 エラー、警告は許容）
- 設計書 [`phase1_design.md`](phase1_design.md) §3「プロジェクト構成」を更新

---

### ステップ 2: 公開型のスケルトン定義

**目的**: 公開 API のシグネチャを確定し、コンパイル可能な状態にする（実装本体は `NotImplementedException` で OK）。

**作業**:
- `GeoCoordinate`, `GeoPolygon`, `MeshCode`, `MeshLevel`, `VehicleProfile`, `Route`, `RoadNetworkGeoJson`, `RestrictedAreaId`, `RestrictedArea`, `BlockArea`, `SlowArea` を定義
- `Router`, `RouterDb`, `RestrictedAreaService` の公開メソッドシグネチャ（要件定義書 §7.1）
- XML コメントの雛形を主要型に付与（本格的な記述はステップ 12）

**完了判定**:
- ビルド成功
- 公開 API シグネチャが要件定義書 §7.1 と一致（ユーザーレビュー）
- 設計書 §4「公開型カタログ」を更新（必要に応じ §2「アーキテクチャ概観」§2.4 名前空間設計も）

---

### ステップ 3: Itinero アダプター（RouterDb 読込・グラフ抽象実装）

**目的**: 親プロジェクトの `MapService.LoadRouterDbFromFile()` 相当を再現し、内部 `IRoadGraph` で抽象化する。

**作業**:
- `IRoadGraph` インターフェース定義（頂点数、エッジ列挙、頂点座標、エッジシェイプ、**エッジ OSM タグ取得 `GetEdgeOsmTags(edgeProfileIndex) → IReadOnlyDictionary<string,string>`**）
- `OsmDotRoute.Itinero.ItineroRoadGraph`: `Itinero.RouterDb.Network` をラップし、`RouterDb.EdgeProfiles.Get(index)` から OSM タグを取り出す
- `OsmDotRoute.Itinero.ItineroRouterDbLoader.LoadFromFile(string) → IRoadGraph`
- `OsmDotRoute.RouterDb.LoadFromFile(string)` から `ItineroRouterDbLoader` を呼ぶ（リフレクション不要・公開メソッド経由）
- 統計取得 API（REQ-MAP-002）: `RouterDb.GetStatistics() → RouterDbStatistics` 実装
- **Itinero `Profile.FactorAndSpeed` への直接依存は廃止**（プロファイル評価は Phase 1 から OsmDotRoute 側で実装、ステップ 5a）

**完了判定**:
- 単体テスト: 既存の `.routerdb` ファイルを読み込み、頂点数・辺数・経緯度範囲が正しく取得できる
- 親プロジェクトの `MapService.cs` で読み込んだ統計値と一致
- 設計書 §5「Itinero アダプター」を更新（§2「アーキテクチャ概観」のレイヤー構造・依存図も初版作成）

---

### ステップ 4: 道路スナップ機能

**目的**: 任意座標を道路ネットワーク上にスナップする機能を実装。

**作業**:
- `OsmDotRoute.Itinero.ItineroSnapper`: Itinero `Router.Resolve` を呼んでスナップ点を取得 → `GeoCoordinate` に変換
- `Router.SnapToRoad(profile, point, searchDistanceM)` で呼出
- ネットワーク外座標時の `null` 返却（REQ-RTE-008 の前段）

**完了判定**:
- 単体テスト: 道路上の点 → 近傍点に解決、道路外の点 → `null` 返却
- 親プロジェクトの `SnapToRoad` と同じ座標が返ることを 10 点で確認
- 設計書 §6「道路スナップ」を更新

---

### ステップ 5a: JSON プロファイル基盤

**目的**: 「ビルドなしでパラメータ調整可能」要件（REQ-PRF-007）を満たす JSON プロファイル基盤を構築。Phase 2/3 以降も使い続ける中核機構。

**作業**:

- **JSON スキーマ定義**:
  - `JsonProfileDefinition` DTO（`System.Text.Json` 用）
  - フィールド: `name`, `vehicleType`, `accessTagKeys`, `highway` (Dictionary), `accessValueMap`, `maxspeedTagKey`, `fallback`, `speedBounds`, `difficulty` (Dictionary), `difficultyDefault`
  - JSON Schema（`profile-schema-v1.json`）を `Documents/schemas/` に配置
- **公開型 `VehicleProfile`** (enum → クラス):
  - `Car`, `Pedestrian` static プロパティ（埋込リソース `Profiles/car.json`, `Profiles/pedestrian.json` から遅延ロード）
  - `LoadFromJsonFile` / `LoadFromJsonString` / `LoadFromJsonStream`
  - 内部に `JsonProfileDefinition` と `ProfileEvaluator` を保持
- **公開型 `DifficultyTypes`**: `public static class`、組込み 8 `const string`
- **`ProfileEvaluator`** (internal):
  - `Evaluate(IReadOnlyDictionary<string,string> osmTags) → (canPass, speedKmh, oneway)`
  - ルール評価順: アクセスタグ → highway 別ルール → maxspeed → fallback
  - `EvaluateDifficulty(string type) → (speedFactor, canPass)`: 未定義は `difficultyDefault`
- **同梱 JSON プロファイル**:
  - `Profiles/car.json`: Itinero `Vehicle.Car` 相当（参考: `car.lua` 仕様、ソースコピー禁止）
  - `Profiles/pedestrian.json`: Itinero `Vehicle.Pedestrian` 相当
  - 両ファイルとも `difficulty` セクション 8 種完備（提案値は要件定義書 §7.1 参照、ユーザー調整可）
- **JSON 検証**: 不正スキーマ・範囲外値・必須キー欠落で適切な例外（`InvalidProfileException`）

**完了判定**:

- 単体テスト: 同梱 `car.json` 読込で `Evaluate` が代表的 OSM タグセット（motorway / residential / footway 等）で期待通り
- 単体テスト: 親プロジェクト RouterDb の主要 `edge_profile` を列挙し、Itinero `Vehicle.Car.Fastest().FactorAndSpeed` と比較 → 通行可否 100% 一致、速度 ±10% 以内
- 単体テスト: ユーザー JSON ロード（ファイル/文字列/Stream）が動作、不正 JSON で `InvalidProfileException`
- 単体テスト: `EvaluateDifficulty("flooding")` / `EvaluateDifficulty("unknown_type")` が正しく動作
- 設計書 §7「独自 Dijkstra エンジン」の前段として「Profile 戦略・JSON スキーマ」小節を §2 もしくは新章 §7-Pre に記述

---

### ステップ 5b: 独自 Dijkstra（制約なし、JsonVehicleProfile 使用）

**目的**: 制約なしの基本経路探索を完成させ、親プロジェクトの `CalculateRoute` と等価な経路を返せるようにする。

**作業**:

- `EdgeWeightCalculator`: エッジ重み = `distance / speed`、speed は `ProfileEvaluator.Evaluate(edgeOsmTags).speedKmh` から取得
- `DijkstraEngine`: バイナリヒープによる単方向 Dijkstra
  - 起点・終点は `SnapToRoad` 経由で頂点に解決（仮想頂点扱い）
  - 親頂点配列で経路復元
- `RouteBuilder`: 頂点列 → エッジ列 → シェイプ統合 → `Route` 構築
- `Router.Calculate(profile, from, to)` から呼出
- 経路未発見・ネットワーク外時に `null` 返却（REQ-RTE-006, REQ-RTE-008）
- `VehicleProfile.Car` / `VehicleProfile.Pedestrian` 対応

**完了判定**:
- 単体テスト: 数十点ペアで親プロジェクト Itinero `Router.Calculate` 結果と総距離が ±10% 以内で一致
- ネットワーク外・経路不存在時に例外を投げず `null` 返却
- 設計書 §7「独自 Dijkstra エンジン」を更新（§2「アーキテクチャ概観」§2.3 データフローも記述）

**Note**: 親プロジェクトとの完全一致は目指さない（プロファイル評価実装差で多少のブレあり）。経路全体の通過道路セットがほぼ一致 + 距離 ±10% で OK。

---

### ステップ 6: 道路ネットワーク GeoJSON 出力

**目的**: 道路ネットワーク全エッジを GeoJSON FeatureCollection で出力。

**作業**:
- `GeoJsonWriter.WriteRoadNetwork(IRoadGraph) → string`（または `RoadNetworkGeoJson` オブジェクト）
- `Router.GetRoadNetworkGeoJson()` から呼出
- エッジ重複排除（親プロジェクト `MapService.GetRoadNetworkGeoJson()` 同様、`HashSet<uint>` で edge ID 管理）

**完了判定**:
- 単体テスト: 既知の小規模 RouterDb で出力した FeatureCollection の features 数が親プロジェクトと一致
- GeoJSON 構文妥当性（`System.Text.Json` でラウンドトリップ可能）
- 設計書 §8「道路ネットワーク GeoJSON 出力」を更新

---

### ステップ 7: メッシュコード変換実装

**目的**: JIS X0410 第3次〜1/10 細分メッシュの 4 階層対応。

**作業**:
- `MeshCode(long value)` コンストラクタで桁数バリデーション（8/9/10/11 桁以外は `ArgumentException`、REQ-RST-018）
- `MeshLevel` 自動判定プロパティ
- `MeshCodeConverter.ToBoundingBox(MeshCode) → Aabb`: 緯度経度矩形を返す
- 参考資料: 親プロジェクト `Documents/標準地域メッシュ計算方法.md`（読むだけ・コピー禁止）

**完了判定**:
- 単体テスト: 既知メッシュ（例: 東京駅 `53394547`）の境界座標を国土地理院公開値と照合
- 4 階層すべてで境界精度がメートル誤差レベル
- 設計書 §9「メッシュコード処理」を更新

---

### ステップ 8: 制約管理基盤（AABB・多角形交差・サービスクラス・難所エリア）

**目的**: 制約の登録・削除・タグ管理・空間判定基盤を完成。難所エリアを第一級サポート。

**作業**:
- `Aabb`: 緯度経度矩形、交差判定・点包含判定
- `PolygonIntersection`: 線分 vs 多角形交差判定（ray casting + 線分交差）、Hole 対応
- `SpatialIndex`: 登録 AABB の配列線形走査（Phase 1 は単純実装、性能要件未達時に R-tree 化）
- `BlockArea` (internal): 進入不可エリア (ポリゴン or メッシュ AABB)
- `DifficultyArea` (internal): 難所エリア (ポリゴン or メッシュ AABB) + 難所タイプ文字列
- `RestrictedAreaService`:
  - 進入不可エリアのポリゴン／メッシュ／メッシュ一括登録（REQ-RST-001〜003）
  - 難所エリアのポリゴン／メッシュ／メッシュ一括登録（REQ-RST-004〜006）
  - 難所タイプ文字列の引数検証（空文字・null 拒否、REQ-RST-007）
  - 個別削除・全クリア・タグ一括削除（REQ-RST-008〜010）
  - 一覧取得（REQ-RST-011、`IReadOnlyList<RestrictedArea>` 返却）
- メッシュ AABB は `MeshCodeConverter` の結果を直接使用（REQ-RST-015）

**完了判定**:
- 単体テスト: 進入不可・難所エリア登録・削除・タグ削除・全クリアが期待通り動作
- 単体テスト: AABB プリフィルタが正しい候補を返す
- 単体テスト: Hole 込み多角形での内外判定
- 単体テスト: 難所タイプ空文字・null で `ArgumentException`
- 単体テスト: ユーザー定義難所タイプ（例: `"snow_heavy"`）が登録可能
- 設計書 §10「制約管理基盤」を更新

---

### ステップ 9: 制約対応 Dijkstra への統合（難所重複ルール込み）

**目的**: Dijkstra のエッジ評価で進入不可・難所制約を参照し、プロファイル経由で速度低下を反映する。複数難所重複時のルール（積・短絡）を実装。

**作業**:
- `EdgeWeightCalculator` に `RestrictedAreaService` と `VehicleProfile` を注入
- エッジ評価時:
  1. エッジシェイプの AABB と全制約 AABB の交差をプリフィルタ（REQ-RST-014）
  2. 候補制約に対しエッジシェイプとの線分交差判定（REQ-RST-013）
  3. **進入不可（BlockArea）が交差** → 重み無限大（探索打切、REQ-RST-032）
  4. **交差した全難所エリア（DifficultyArea）**を列挙:
     - 各難所タイプを `VehicleProfile.EvaluateDifficulty(type)` で評価
     - いずれかが `canPass: false` → 重み無限大（短絡評価、REQ-RST-031）
     - 全 `speedFactor` の積を計算（REQ-RST-030）
     - 通常エッジ重み × (1 / combinedSpeedFactor) を採用
- 制約変更後の次回 `Calculate` から即時反映（キャッシュ無しが基本、REQ-RST-012）

**完了判定**:
- 単体テスト: BlockArea 設定で迂回経路が返る
- 単体テスト: DifficultyArea(`"flooding"`) で car の所要時間が `1 / 0.3 ≈ 3.33` 倍に
- 単体テスト: 同じ DifficultyArea で pedestrian の所要時間が `1 / 0.1 = 10` 倍に（プロファイル別反応）
- 単体テスト: 2 つの DifficultyArea（`"flooding"` + `"construction"`）重複時に速度係数が積（0.3 × 0.2 = 0.06）
- 単体テスト: DifficultyArea(`"landslide"`) で経路が迂回（canPass: false 短絡）
- 単体テスト: BlockArea と DifficultyArea が重複する場合、BlockArea 優先で迂回
- 単体テスト: 未知の難所タイプ（例: `"meteor"`）に対して `difficultyDefault` 適用（速度変化なし）
- 単体テスト: 制約クリア後は元の経路に戻る
- 設計書 §11「制約対応 Dijkstra 統合」を更新（§7「独自 Dijkstra エンジン」に統合点も追記、難所重複ルールの計算式を明記）

---

### ステップ 10: GML 入力対応（KSJ アプリケーションスキーマ、形状のみ抽出）

**目的**: 国土数値情報 KSJ アプリケーションスキーマ準拠 GML 3.2 で動的制約（進入不可・難所）を一括登録できるようにする。Phase 1 動作確認は A31「浸水想定区域」(`<ksj:ExpectedFloodArea>`) で行うが、パーサーはフィーチャ要素名にハードコード依存せず、任意の KSJ プロダクトを受け入れる（拡張性、REQ-RST-020）。難所タイプとタグは API 引数でユーザーが指定する（REQ-RST-026/027）。

**作業**:
- `GmlParser`（`System.Xml.XmlReader` ベース、internal、`OsmDotRoute.Gml` 名前空間）:
  - `<ksj:Dataset>` 直下要素を順次走査
  - `<gml:Curve gml:id="...">` の `<gml:posList>` を「緯度 経度」順で読取、`GeoCoordinate` 列に変換（REQ-RST-028）
  - `<gml:Surface gml:id="...">` の `<gml:exterior>` / `<gml:interior>` から外周・Hole を構築、`<gml:curveMember xlink:href="#cXXX">` で Curve を参照解決（REQ-RST-022）
  - 任意のフィーチャ要素（要素名にハードコード依存しない）の `<*:bounds xlink:href="#aXXX">` から Surface を解決し `GeoPolygon` を構築（REQ-RST-020/021）
  - `<gml:MultiSurface>` は Phase 1 非対応、検出時は `NotSupportedException` を投げる（REQ-RST-023、Phase 2 で対応予定。A31 サンプル `A31-12_24.xml` 1.6GB で出現 0 件を確認、2026-05-19）
  - フィーチャ属性子要素（`<ksj:waterDepth>` 等）は読み飛ばす（Phase 1 では保持しない、REQ-RST-026）
- `RestrictedAreaService` に 6 メソッド追加（REQ-RST-024/025）:
  - `AddBlockAreaFromGml(string gml, string? tag = null)`
  - `AddBlockAreaFromGmlFile(string filePath, string? tag = null)`
  - `AddBlockAreaFromGmlStream(Stream stream, string? tag = null)`
  - `AddDifficultyAreaFromGml(string gml, string difficultyType, string? tag = null)`
  - `AddDifficultyAreaFromGmlFile(string filePath, string difficultyType, string? tag = null)`
  - `AddDifficultyAreaFromGmlStream(Stream stream, string difficultyType, string? tag = null)`
- すべてのフィーチャに同一 `tag` を適用、`AddDifficultyArea*` は全フィーチャに同一 `difficultyType` を適用（REQ-RST-026/027）
- 各フィーチャごとに `RestrictedAreaId` を配列で返却（REQ-RST-021）
- ステップ 8 で `NotImplementedException` で残した `AddFromGeoJson(string)` / `AddFromGeoJsonFile(string)` / `AddFromGeoJsonStream(Stream)` の 3 メソッドを削除（v0.x、破壊変更可）

**完了判定**:
- 単体テスト: 最小 KSJ GML（1 Curve + 1 Surface + 1 フィーチャ）を `AddBlockAreaFromGml*` で正しく読込
- 単体テスト: Hole 込み Surface を `AddDifficultyAreaFromGml*` で読込、難所タイプが付与される
- 単体テスト: 複数フィーチャ（≥ 3）を 1 ファイル内で一括登録、戻り値配列の長さを検証（REQ-RST-021）
- 単体テスト: `tag` 引数で全フィーチャに同一タグ付与、`RemoveByTag` で一括削除可能（REQ-RST-027 連携）
- 単体テスト: `<gml:MultiSurface>` 検出時に `NotSupportedException`（REQ-RST-023）
- 単体テスト: 不正 GML（XML パースエラー / 必須要素欠落 / xlink 参照解決失敗）で適切な例外
- 単体テスト: フィーチャ要素名に依存しないこと（架空の要素名 `<test:DummyArea>` も同様に読める、拡張性検証）
- 単体テスト: ユーザー定義難所タイプ（例: `"snow_heavy"`）も `difficultyType` 引数で指定可能
- 統合テスト: 実データ `D:/ハザードデータ/A31-12_24_GML/A31-12_24.xml`（1.6GB）の先頭部分を `AddDifficultyAreaFromGmlStream(..., DifficultyTypes.Flooding)` で読込、フィーチャ数 ≥ 1 とエラーなしを検証（フル読込は性能ベンチで別途確認、ステップ 15）
- 設計書 §12「GML 入力対応」を本記述化

---

### ステップ 11: 経路 → GeoJSON 出力ユーティリティ ~~（廃止、2026-05-19 / v1.7）~~

**ステータス**: **廃止**。要件 REQ-FMT-004 を v1.7 で廃止（ユーザー合意 2026-05-19）。

**廃止理由**: 親プロジェクトでの実需要が確認できず、利用者側で `Route.Shape` から数行で GeoJSON 化可能なため YAGNI 判断。詳細は設計書 §13「経路 GeoJSON 出力（廃止）」参照。

**次の作業**: ステップ 11 をスキップしてステップ 12（DI 拡張・XML ドキュメンテーション・README 整備）に直接進む。

---

### ステップ 12: DI 拡張・XML ドキュメンテーション・README 整備（**完了 2026-05-19**）

**目的**: 公開 API のドキュメント整備・DI 統合・配布準備。

**実施内容**:

- `OsmDotRoute.Extensions.DependencyInjection` プロジェクト新設（`Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0 のみに依存、`OsmDotRoute` / `OsmDotRoute.Itinero` への ProjectReference）
- `AddOsmDotRoute(IServiceCollection, string routerDbPath)` 実装 + `AddOsmDotRoute(IServiceCollection, Action<OsmDotRouteOptions>)` オーバーロード（`Router` / `RouterDb` / `RestrictedAreaService` を Singleton 登録）
- 公開 20 型に `<summary>` / `<param>` / `<returns>` / `<exception>` 完備
- `Directory.Build.props` の `GenerateDocumentationFile` を `false` → `true` に切替（テスト/ベンチマーク/サンプル csproj で個別に `false` 上書き）
- ルート `README.md` を Phase 0 → Phase 1 進行中の内容に全面書き換え（最小サンプル、動的制約登録例、DI 統合、0.x 破壊的変更方針 REQ-API-008、Phase ロードマップ）
- `LICENSE` は既存 MIT のまま据置（ステップ 1 で配置済）
- `.sln` に DI 拡張プロジェクト登録、`src` ソリューションフォルダ配下に配置

**完了判定**:

- ✅ `dotnet build OsmDotRoute.sln`: 6 プロジェクト全て **0 警告 0 エラー**（XML doc 生成警告含む）
- ✅ `dotnet test`: 147/147 成功維持（新規テスト追加なし）
- ✅ 設計書 §14「DI 拡張とドキュメント」を実装済みに記述（§2.2/2.4/3.2/3.4 にも追記反映）
- ✅ 要件定義書: REQ-API-005 / REQ-API-006 / REQ-API-008 / REQ-PKG-001 / REQ-LIC-001 を `[x]` 完了マーク、v1.8 に改訂

---

### ステップ 13: 検証用地図アプリ - サーバー API + 地図基盤・範囲指定（**完了 2026-05-19**）

**実施内容**:

- `samples/MapVerifier/MapVerifier.Server/` を新規作成（minimal API、CORS、ResponseCompression、`<Version>1.0.0</Version>`）
- 実装エンドポイント（ステップ 13 スコープ）: `GET /api/version`、`POST /api/load`、`GET /api/stats`、`GET /api/road-network`、`POST /api/snap`、`POST /api/route`
- `RouterState` Singleton で RouterDb / Router ホルダーを管理（`AddOsmDotRoute` は使わず、起動時未ロード前提に手動登録）
- `samples/MapVerifier/MapVerifier.Web/` を新規作成（Vite 5 + React 18 + TypeScript 5 + MapLibre GL 4、`version: 1.0.0`）
- 実装 UI: `VersionBanner`（フロント版+サーバー版併記、不一致時警告）、`LoadPanel`（パス入力→読込→統計表示+道路ネットワーク表示トグル）、`MapView`（MapLibre GL React ラッパー、OSM raster、`fitBounds`/`setRoadNetwork` API 公開）、`MapBoundsPanel`（現在範囲表示+手動 fit）
- 設計書を別ドキュメント `Documents/map_verifier_design.md` に分離（OsmDotRoute 本体とは独立 SemVer）

**完了判定**:

- ✅ `dotnet run --project samples/MapVerifier/MapVerifier.Server`: 起動成功、実機 RouterDb 読込・統計・road-network (10.3MB)・経路計算が動作（curl スモーク検証）
- ✅ `npm run build`: tsc + vite build 成功（0 エラー）
- ✅ `npm run dev`: dev サーバー起動、`/api/*` proxy 経由で `/api/version` が `{name:"MapVerifier.Server",version:"1.0.0"}` を返す
- ✅ ライブラリ本体 147/147 テスト維持
- ✅ MapVerifier 設計書 v1.0.0 リリース

**ブラウザ動作確認**: dev サーバー＋API サーバー起動後、ブラウザで `http://localhost:5173` を開き、RouterDb パスを入力 →「読込」→統計表示・マップが該当範囲にフィット →「道路ネットワークを表示」で青線描画されることを目視確認する（手動）

---

### ステップ 13 計画（参考、原文）：検証用地図アプリ - サーバー API + 地図基盤・範囲指定

**目的**: ブラウザ上で OsmDotRoute の動作を視覚的に確認できる検証ツール基盤を構築する。親プロジェクト `App/DisasterWasteSim.Viewer/` の構成（React + Vite + MapLibre GL）を流用しつつ大幅に簡素化する。

**作業（サーバー側 `MapVerifier.Server`）**:

- `dotnet new web -n MapVerifier.Server` で最小 API プロジェクト作成
- `OsmDotRoute` / `OsmDotRoute.Itinero` への ProjectReference
- CORS を `MapVerifier.Web` 開発サーバー（既定 `http://localhost:5173`）に開放
- エンドポイント実装:
  - `POST /api/load` — `{ routerDbPath }` で RouterDb 読込
  - `GET  /api/stats` — 頂点数・辺数・経緯度範囲（REQ-MAP-002）
  - `GET  /api/road-network` — 道路ネットワーク GeoJSON（REQ-RTE-004）
  - `POST /api/snap` — `{ lat, lon, profile, searchDistanceM }` → スナップ座標
  - `POST /api/route` — `{ fromLat, fromLon, toLat, toLon, profile }` → 経路 GeoJSON LineString + 距離・所要時間
  - `POST /api/restrictions/polygon` — ポリゴン制約登録（block / slow）
  - `POST /api/restrictions/mesh` — メッシュコード制約登録（block / slow、複数可）
  - `POST /api/restrictions/geojson` — GeoJSON 文字列で一括登録
  - `GET  /api/restrictions` — 一覧（GeoJSON FeatureCollection）
  - `DELETE /api/restrictions/{id}` — 個別削除
  - `DELETE /api/restrictions` — 全クリア

**作業（フロント側 `MapVerifier.Web`）**:

- `npm create vite@latest MapVerifier.Web -- --template react-ts`
- 依存追加: `maplibre-gl`、`@types/maplibre-gl`
- 親プロジェクト `App/DisasterWasteSim.Viewer/src/` から流用する**最小要素**:
  - `MapView.tsx` 相当（MapLibre GL の React ラッパー）
  - 既定スタイル: OSM raster タイル（`https://tile.openstreetmap.org/{z}/{x}/{y}.png`）または親プロ流用の MapLibre スタイル
- 流用しない（簡素化のため除外）: SignalR、Recharts、エディタ複雑機能、シナリオ管理、認証
- 実装 UI（このステップ範囲）:
  - **`LoadPanel`**: RouterDb ファイルパス入力 → 読込ボタン → 統計（頂点数・辺数）表示
  - **`MapView`**: MapLibre GL でマップ表示、初期表示は読込時の経緯度範囲に自動 fit
  - **`MapBoundsPanel`**: 現在の表示範囲（南西・北東経緯度）を表示。手動入力で表示範囲を変更（`map.fitBounds`）
  - 道路ネットワーク描画: `/api/road-network` の GeoJSON をレイヤーとして表示（オン／オフ切替）

**完了判定**:

- `dotnet run --project samples/MapVerifier/MapVerifier.Server` で API 起動
- `npm run dev`（`MapVerifier.Web`）でフロント起動
- ブラウザで RouterDb 読込 → 統計表示 → マップが該当範囲にフィット → 道路ネットワークが描画される
- 表示範囲を手動入力で変更できる
- 設計書 §15.1「サーバー API 仕様」§15.2「フロントエンド構成」（基盤部分）を更新

---

### ステップ 14: 検証用地図アプリ - メッシュ表示・ポリゴン作成・経路 UI（**完了 2026-05-19、MapVerifier 1.0.0 初版リリースで確定**）

**実施内容**:

- **Lib v0.18 (153/153 tests)**: `MeshCode.ToBounds()` + `MeshCode.EnumerateInBounds(MapBounds, MeshLevel)` を公開 API として追加。サーバー側でメッシュグリッド GeoJSON 生成に利用 (二重実装回避)。テスト 6 件追加
- **MapVerifier 1.0.0 (初版リリース)**: Phase 1 ステップ 13〜14 で構築した検証用地図アプリの完成形。複数の試行錯誤 (OS ネイティブダイアログ: WinForms / WPF / PowerShell サブプロセス / WinExe 専用 EXE — ユーザー環境で全て画面非表示) を経て、最終的に **Web 内モーダル + HTTP API による自前ファイルブラウザ方式** に着地 (親プロジェクト `UserSettingsDialog` 方式に倣う、OS UI 依存ゼロ)。詳細仕様は `Documents/map_verifier_design.md` v1.0.0 を参照
- **本版で確定した機能群** (ユーザー承認済 2026-05-19):
  - Server: `/api/version`、`/api/load`、`/api/stats`、`/api/road-network`、`/api/snap`、`/api/route`、`/api/mesh/grid` (1km/500m/250m)、`/api/restrictions/{polygon,mesh,gml-file}`、`/api/restrictions[/{id}|/geojson|?tag=]`、`/api/files/browse`
  - Web: `VersionBanner` / `LoadPanel` / `MapBoundsPanel` / `RoutePanel` / `MeshGridPanel` / `PolygonEditorPanel` / `GmlImportPanel` / `RestrictionListPanel` / `FileBrowserDialog` + MapView 6 レイヤー
- **E2E 検証実績**: 実機 RouterDb 43k 頂点で動作確認、ベースライン経路 3784m → ブロックポリゴン登録後 4968m 迂回確認、A31 1.6GB GML をマップ範囲フィルタで 31 ポリゴン 20 秒インポート確認
- **MapVerifier 1.1.0 (MINOR up)**:
  - Server エンドポイント追加: `GET /api/mesh/grid?swLat&swLon&neLat&neLon&level=1km|500m|250m` (過大要求 10k セル上限ガード)、`POST /api/restrictions/polygon`、`POST /api/restrictions/mesh`、`GET /api/restrictions`、`GET /api/restrictions/geojson`、`DELETE /api/restrictions/{id}`、`DELETE /api/restrictions[?tag=]`
  - Server 内部: 描画用メタデータを `RestrictionMetadataStore` Singleton で保持 (RestrictedAreaService は形状を内部表現で持つため、UI 描画用に別途キャッシュ)
  - Web 新規コンポーネント: `MeshGridPanel` (階層選択+描画+メッシュクリックで属性付与)、`PolygonEditorPanel` (マウス描画+属性付与)、`RoutePanel` (起終点マップ指定+プロファイル+計算)、`RestrictionListPanel` (一覧+個別/全削除)
  - MapView 拡張: mesh-grid / restrictions / route / route-endpoints / polygon-draft の 5 レイヤー追加、クリックハンドラで feature の `meshCode` プロパティを App 側に通知
  - App.tsx でモード管理 (`pickMode` for route / `drawing` for polygon / mesh-click for restriction)、制約変更時の自動再描画

**完了判定**:

- ✅ `dotnet build` / `npm run build`: 0 エラー
- ✅ `dotnet test OsmDotRoute.sln`: 153/153 成功（mesh 公開 API テスト 6 件追加）
- ✅ E2E スモーク: ベースライン経路 3784m → ブロックポリゴン登録後 4968m への迂回を curl で確認 (制約反映 OK)
- ✅ `GET /api/mesh/grid`: 1km / 500m / 250m すべての階層で GeoJSON FeatureCollection を返却
- ✅ MapVerifier 設計書 v1.1.0 リリース、§5.4 を「サーバー側生成」に方針変更

**ブラウザ動作確認手順**:

1. `./start-map-verifier.ps1` で起動
2. ヘッダーが `MapVerifier v1.1.0 (server: v1.1.0)` を表示
3. RouterDb パス入力 → 「読込」→ マップが自動でデータ範囲にフィット、統計表示
4. 「現在の表示範囲のメッシュを描画」(1km) → メッシュ格子がマップに描画される
5. 任意メッシュをクリック → 「選択中メッシュ」表示 → 種別 (block/difficulty) + 難所タイプ + タグ → 「登録」
6. 「マウスで描画開始」→ 数頂点クリック → 種別/タイプ/タグ → 「登録」
7. 「起点をマップで指定」→ 道路上クリック → 同様に終点 → 「経路計算」 → 緑線で経路描画、距離・所要時間表示
8. 登録済み制約一覧から個別削除 → マップから当該領域が消える
9. 制約のあるエリアを通る経路と、無いときの経路で結果が異なることを確認

---

### ステップ 14 計画（参考、原文）：検証用地図アプリ - メッシュ表示・ポリゴン作成・経路 UI

**目的**: 動的制約と経路計算をマウス操作で直感的に検証できる UI を完成させる。

**作業（フロント側 `MapVerifier.Web` 拡張）**:

- **`MeshGridPanel`** - メッシュ種別指定:
  - 階層選択ドロップダウン: 第3次（1km） / 1/2 細分（500m） / 1/4 細分（250m） / 1/10 細分（100m）（REQ-RST-016）
  - 「現在の表示範囲のメッシュグリッドを描画」ボタン
  - フロント側で表示範囲の経緯度から該当メッシュコード列を算出（クライアント側で JIS X0410 変換ロジックを実装、サーバー側 `MeshCodeConverter` と独立）
  - メッシュ矩形を GeoJSON FeatureCollection としてマップに描画（半透明枠線）
  - メッシュ矩形クリックで「進入禁止」「移動困難（速度低下係数 0.5 等）」選択ダイアログ → `/api/restrictions/mesh` に POST
  - メッシュコードを GeoJSON properties に含めて識別可能に

- **`PolygonEditorPanel`** - マウス描画モード:
  - 「ポリゴン描画開始」ボタン → 描画モードに入る
  - マップクリックで頂点追加（クリックごとに線が伸びる、暫定 GeoJSON ソース更新）
  - 「描画完了」ボタンまたはダブルクリックで多角形確定
  - 確定時に「進入禁止」「移動困難（速度低下係数指定）」「タグ（任意）」の入力ダイアログ
  - `/api/restrictions/polygon` に POST、成功後に登録済み制約レイヤーへ追加

- **`PolygonEditorPanel`** - 座標指定モード:
  - 緯度経度ペアのテーブル入力（行追加・削除・並び替え）
  - 種別・係数・タグ指定
  - 「登録」ボタンで `/api/restrictions/polygon` に POST
  - マウス描画モードと相互に変換可能（描画した頂点をテーブルにロード／テーブルから描画プレビュー）

- **`RoutePanel`** - 経路計算 UI:
  - 「起点をマップで指定」「終点をマップで指定」ボタン（次のマップクリックで該当地点が設定される）
  - 起点・終点を緯度経度直接入力でも指定可能
  - プロファイル選択（Car / Pedestrian）
  - 「経路計算」ボタン → `/api/route` を呼び出し → 結果 LineString をマップに描画 + 総距離・所要時間表示
  - 経路結果のクリア機能

- **`RestrictionListPanel`** - 登録済み制約一覧:
  - `/api/restrictions` から取得した一覧をテーブル表示（ID・種別・係数・タグ・形状種別）
  - 行クリックでマップ上で該当形状をハイライト
  - 個別削除ボタン → `DELETE /api/restrictions/{id}`
  - 「全クリア」ボタン → `DELETE /api/restrictions`

**作業（サーバー側 `MapVerifier.Server` 補強）**:

- 必要に応じてレスポンス型・エラーハンドリングを補強
- 経路計算結果に GeoJSON LineString + メタ情報（距離・所要時間）を含むよう調整

**動作シナリオ（ユーザー検証手順例）**:

1. RouterDb 読込 → 道路ネットワーク表示
2. 起終点をマップクリックで指定 → 経路計算 → 通常経路を確認
3. 経路途中にマウスでポリゴン描画 → 進入禁止登録
4. 再度経路計算 → 迂回経路になることを確認
5. ポリゴン削除 → 元の経路に戻ることを確認
6. メッシュ種別を 100m に切替 → グリッド描画 → 任意メッシュをクリックで移動困難（係数 0.3）登録
7. 経路計算 → 該当メッシュを避けるか遅くなることを確認

**完了判定**:

- 上記シナリオが全て動作
- マウス描画したポリゴンと座標指定ポリゴンが同等の制約効果を生む
- メッシュ 4 階層すべて表示・制約登録可能
- ユーザーレビュー OK
- 設計書 §15.2「フロントエンド構成」§15.3「動作シナリオ」を更新

---

### ステップ 15: ベンチマーク・性能検証

**目的**: REQ-NFR-001（100ms）と REQ-NFR-002（制約 100 件）を達成可能か確認、未達なら最適化方針を策定。

**作業**:
- `OsmDotRoute.Benchmarks` プロジェクトに BenchmarkDotNet シナリオ追加:
  - 都道府県単位 RouterDb（親プロジェクトで使用中のものを借用）
  - 1000 ペアの起終点（ランダム生成、ネットワーク内に限定）
  - 制約 0 / 10 / 50 / 100 件パターン
  - メモリ使用量計測（`MemoryDiagnoser`）
- 結果を `Documents/phase1_benchmark_results.md` にまとめる
- 100ms 未達なら以下を順次検討:
  1. AABB 配列を R-tree 化
  2. エッジシェイプ AABB のキャッシュ
  3. プロファイル FactorAndSpeed のキャッシュ
  4. 双方向 Dijkstra（REQ-RTE-009）— Phase 4 案件だが Phase 1 で必要なら前倒し検討

**完了判定**:
- REQ-NFR-001（100ms）達成、または未達ならユーザー合意のもとで対策実施／Phase 4 送り判断
- REQ-NFR-003（16GB RAM）達成
- ベンチマーク結果文書化
- 設計書 §16「ベンチマーク結果」を更新（最適化を実施した場合は §11「制約対応 Dijkstra 統合」・§10「制約管理基盤」にも反映）

---

### ステップ 16: 親プロジェクト統合・パリティ検証

**目的**: 親プロジェクトの `MapService.cs` を OsmDotRoute に置き換え、`using Itinero;` を消去できる状態を実証する。

**作業**:
- 親プロジェクト `DisasterWasteSim.Server.csproj` に `<ProjectReference>` で OsmDotRoute プロジェクト群を参照
- 親プロジェクト側で `MapService.cs` の試験的書き換え用ブランチを作成（**親プロジェクト本体はマージしない、検証ブランチのみ**）
- `using Itinero;` / `using Itinero.IO.Osm;` / `using Itinero.Osm.Vehicles;` を削除
- 既存メソッド `LoadRouterDbFromFile` / `CalculateRoute` / `SnapToRoad` / `GetRoadNetworkGeoJson` を OsmDotRoute API で再実装
- 親プロジェクトのビルド・既存テスト通過確認

**完了判定**:
- 親プロジェクト検証ブランチで `using Itinero` が完全消去できビルド成功
- 既存テストが通過
- 親プロジェクト動作確認: シナリオ実行で経路計算結果が大きく変わらない
- 設計書 §17「親プロジェクト統合」を更新

**Note**: 親プロジェクト側の正式マージ可否はユーザー判断。本ステップは「Phase 1 が要件を満たすことの実証」が目的。

---

### ステップ 17: ユーザー検証・Phase 1 確定

**目的**: ユーザー手動検証 → Phase 1 完了確定 → 要件定義書のステータス更新。

**作業**:
- ユーザーが手動で動作確認（`MapVerifier` 検証用地図アプリ、サンプル `ConsoleDemo`、または親プロジェクト検証ブランチ）
- 各 REQ-ID の進捗を要件定義書で `[x]` に更新、`(Ver. -)` を該当バージョンに更新（バージョン番号はユーザー採番）
- `Phase 進行状況` メモリ更新
- Git tag 付与（ユーザー判断、例: `v0.1.0`）

**完了判定**:
- ユーザー OK
- 要件定義書 Phase 1 要件が全 `[x]`
- 設計書全章が記述済み（特に §18「制約事項と既知の課題」に Phase 2 申し送り事項を集約）
- Phase 1 完了報告

---

## 9. 検証戦略

### 9.1 単体テスト方針

- 各ステップで `OsmDotRoute.Tests` に xUnit テストを追加
- カバレッジ目標は設定しない（実用上の確実性を優先、過剰なテストを避ける）
- 各要件 ID に対し最低 1 テスト
- テストデータ: 親プロジェクトで使用中の `.routerdb` を `tests/TestData/` にコピー（ライセンス確認後）

### 9.2 統合テスト方針

- `MapVerifier` 検証用地図アプリでの操作検証（ステップ 13〜14）と、親プロジェクト検証ブランチでの実動作確認（ステップ 16）の二段構えで統合テストとする
- 自動化は Phase 1 では不要（ステップ 17 のユーザー手動検証で代替）
- `MapVerifier` は Phase 2 以降も継続活用想定（独自グラフ形式の検証ツールとして再利用）

### 9.3 設計記録方針

- 実装の根拠・トレードオフは設計書 [`phase1_design.md`](phase1_design.md) に蓄積
- 各ステップ完了時に該当章を更新（§2.5 のルール）
- セッションを跨いでも設計意図が辿れる状態を維持

### 9.3 性能テスト方針

- ステップ 13 でベンチマーク
- ベンチマーク結果は `Documents/phase1_benchmark_results.md` に文書化

---

## 10. リスクと対応

### 10.1 技術的リスク

| リスク | 影響 | 対応 | 関連要件 |
|---|---|---|---|
| 独自 Dijkstra が Itinero と挙動差で迂回経路を出す | 親プロジェクトの既存ルートに非互換が出る | ステップ 5 で 数十ペアの比較テスト、許容範囲はユーザーと合意 | REQ-RTE-001 |
| 100ms 性能未達（特に制約 100 件時） | REQ-NFR-001/002 未達 | ステップ 13 で計測、未達なら R-tree / キャッシュ / 双方向 Dijkstra で対策 | REQ-NFR-001, REQ-NFR-002 |
| Itinero `RouterDb` 読込時のメモリピーク超過 | REQ-NFR-003 未達 | ステップ 13 で計測、未達なら遅延読込を検討 | REQ-NFR-003 |
| エッジシェイプ AABB の事前計算なしで判定コスト膨大 | 100ms 未達 | ステップ 9 で必要なら起動時に全エッジ AABB をキャッシュ | REQ-RST-013〜014 |
| メッシュコード境界計算の丸め誤差 | 制約適用範囲がメッシュ仕様とずれる | 国土地理院公開値で検証、`decimal` または高精度演算を検討 | REQ-RST-016〜017 |
| GeoJSON パースの仕様逸脱（座標軸順・Hole 解釈） | 利用者の GeoJSON が想定外動作 | RFC 7946 公式サンプルで網羅テスト | REQ-RST-020〜028 |

### 10.2 ライセンスリスク

| リスク | 影響 | 対応 | 関連要件 |
|---|---|---|---|
| Itinero ソース由来コードの混入 | MIT 公開不可・再ライセンス | 実装は仕様書と公開 API ドキュメントのみ参照、`Itinero_source_reference/` のソースをコピペしない（理解の参考に読むのは可） | REQ-LIC-002 |
| 親プロジェクトのコード混入 | 著作権・依存方向逆転 | 親プロジェクトのコードは参考にしない方針（API シグネチャ表面以外） | — |

### 10.3 進行リスク

| リスク | 影響 | 対応 |
|---|---|---|
| ステップ間のユーザー承認遅延 | Phase 1 完了時期が延びる | 各ステップ完了時に簡潔な報告で承認コストを下げる |
| ステップの想定外膨張 | 工数見積もり破綻 | 1 ステップで 3 セッション以上に膨らんだ場合は分割提案 |
| Itinero NuGet パッケージの取得不能（廃止リスク） | ビルド不能 | 早期に `OsmDotRoute.Itinero` プロジェクトをビルドし、必要なら Itinero NuGet パッケージをローカルキャッシュ |
| `MapVerifier` フロントの工数膨張（CSS / UX 深追い） | 検証ツールの肥大化、Phase 1 完了遅延 | UI は実用最低限（飾り付け不要）、検証シナリオが回ることを最優先。流用元の親プロ Viewer の機能を持ち込まない |
| MapLibre GL のタイル取得元（OSM 公式タイル）の利用ポリシー | 検証用途を超えた利用で OSM 規約違反 | 検証ツール用途であることを README に明記、本番利用は別タイルプロバイダを案内 |

---

## 11. 想定工数感

各ステップの工数感（参考、ユーザー単独開発・他作業並行ベース）:

| # | ステップ | 工数感 |
|---|---|---|
| 1 | ソリューション基盤 | 0.5 日 |
| 2 | 公開型スケルトン | 0.5 日 |
| 3 | Itinero アダプター | 1 日 |
| 4 | スナップ機能 | 0.5 日 |
| 5a | JSON プロファイル基盤 | 1.5〜2 日 |
| 5b | 独自 Dijkstra（制約なし） | 2 日 |
| 6 | 道路ネットワーク GeoJSON | 0.5 日 |
| 7 | メッシュコード変換 | 1 日 |
| 8 | 制約管理基盤（難所エリア込み） | 1.8 日 |
| 9 | 制約対応 Dijkstra 統合（重複ルール込み） | 1.2 日 |
| 10 | GeoJSON 入力対応（`difficulty` キー） | 1 日 |
| 11 | 経路 → GeoJSON 出力 | 0.5 日 |
| 12 | DI 拡張・XML doc・README | 1 日 |
| 13 | 検証用地図アプリ - サーバー API + 地図基盤 | 1.5〜2 日 |
| 14 | 検証用地図アプリ - メッシュ・ポリゴン・経路 UI | 2〜3 日 |
| 15 | ベンチマーク・性能検証 | 1〜3 日（最適化要否次第） |
| 16 | 親プロジェクト統合検証 | 1 日 |
| 17 | ユーザー検証・確定 | 0.5 日 |

**合計**: 約 **20〜23 日**（最適化深掘りなしの場合）

---

## 12. Phase 1 完了判定

- [ ] 要件定義書 Phase 1 該当要件（REQ-RTE-001〜008, REQ-RST-001〜015, REQ-RST-016〜028, REQ-PRF-001〜002, REQ-MAP-001〜002, REQ-API-001〜006, REQ-API-008, REQ-FMT-001〜004, REQ-NFR-001〜003, REQ-NFR-005〜006, REQ-NFR-009〜010, REQ-PKG-001, REQ-LIC-001〜003, REQ-DEP-001）が全 `[x]`
- [ ] 親プロジェクト検証ブランチで `using Itinero` 完全消去ビルド成功
- [ ] ベンチマーク REQ-NFR-001 達成（または未達の場合のユーザー合意ある対応方針確定）
- [ ] README / LICENSE / XML ドキュメント整備完了
- [ ] ユーザー OK

---

## 13. Phase 2 への引き継ぎ事項

Phase 1 完了時点で以下を Phase 2 計画書に引き継ぐ:

- 独自バイナリグラフ形式の仕様（REQ-MAP-003、別文書 `phase2_graph_format_spec.md`）
- Itinero RouterDb → 独自形式変換ツール仕様（REQ-MAP-004）
- ランタイム Itinero 依存削除手順（`OsmDotRoute.Itinero` プロジェクト削除）
- 車両プロファイル `Bicycle` / `Truck` 追加（REQ-PRF-003〜004）
- Phase 1 ベンチマーク基準値（Phase 2 で同等以上の性能維持確認用）

---

## 14. 次のアクション

- [ ] 本実装計画書のユーザーレビュー
- [ ] 5.4「確定すべきユーザー判断事項」への回答
  - テストフレームワーク（提案: xUnit）
  - ベンチマークフレームワーク（提案: BenchmarkDotNet）
  - DI 拡張アセンブリの分離可否（提案: 分離）
  - サンプル `ConsoleDemo` 要否（提案: 作成）
  - `MapVerifier` 技術スタック（提案: ASP.NET Core 最小 API + Vite + React + MapLibre GL）
  - `MapVerifier.Web` のポリゴン描画ツール（提案: 自前実装）
- [ ] ユーザー合意後、ステップ 1 着手

---

## 15. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 (draft) | 2026-05-18 | 初版ドラフト作成 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-18 | 検証用地図アプリ `MapVerifier`（地図範囲指定・メッシュ種別指定・マウスポリゴン作成・座標指定）を追加。ステップ 13〜14 に分割、既存ステップ 13〜15 を 15〜17 へ繰り下げ。工数感を約 17〜20 日に更新 | Claude (Opus 4.7) |
| 0.3 (draft) | 2026-05-18 | 設計書 [`phase1_design.md`](phase1_design.md) を新設（ひな形）。§2.5 に「各ステップ完了時に設計書該当章を更新」ルール追加、全ステップの完了判定に「設計書 §NN を更新」を組込。会話セッションを跨いでも実装意図を把握できる体制とした | Claude (Opus 4.7) |
| 1.0 (確定) | 2026-05-18 | ユーザー承認、Phase 1 着手、ステップ 1（ソリューション・プロジェクト基盤）完了 | Claude (Opus 4.7) |
| 1.1 (確定) | 2026-05-18 | プロファイル外部 JSON 化（リビルド不要要件）と難所エリア導入を反映。ステップ 5 を 5a（JSON プロファイル基盤）+ 5b（独自 Dijkstra）に分割。ステップ 3/8/9/10 を難所対応に更新。工数 17〜20 日 → 20〜23 日。要件定義書 v1.2 と整合 | Claude (Opus 4.7) |
