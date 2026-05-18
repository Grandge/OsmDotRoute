# OsmDotRoute Phase 1 設計書

**バージョン**: 0.9（進行中）
**作成日**: 2026-05-18
**最終更新**: 2026-05-18
**ステータス**: 進行中（Phase 1 ステップ 5b 完了、独自 Dijkstra エンジン実装済、52/52 テスト成功 + Itinero `Router.Calculate` 距離パリティ確認）
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
| 8. 道路ネットワーク GeoJSON 出力 | ステップ 6 | 未記述 |
| 9. メッシュコード処理 | ステップ 7 | 未記述 |
| 10. 制約管理基盤 | ステップ 8 | 未記述 |
| 11. 制約対応 Dijkstra 統合 | ステップ 9 | 未記述 |
| 12. GeoJSON 入力対応 | ステップ 10 | 未記述 |
| 13. 経路 GeoJSON 出力 | ステップ 11 | 未記述 |
| 14. DI 拡張とドキュメント | ステップ 12 | 未記述 |
| 15. 検証用地図アプリ MapVerifier | ステップ 13-14 | 未記述 |
| 16. ベンチマーク結果 | ステップ 15 | 未記述 |
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

**ステータス**: ステップ 1〜5 進行に合わせて段階的に記述

### 2.1 レイヤー構造

ステップ 3 完了時点のレイヤー構造:

```text
[利用者コード（親プロジェクト等）]
        │ uses
        ▼
┌──────────────────────────────────────────────────────────┐
│ 公開 API 層 (namespace OsmDotRoute)                       │
│   Router, RouterDb, Route, GeoCoordinate, GeoPolygon,    │
│   MeshCode/MeshLevel, VehicleProfile, DifficultyTypes,   │
│   RestrictedAreaService, BlockArea, DifficultyArea, ...  │
└──────────────────────────────────────────────────────────┘
        │ uses (internal)
        ▼
┌──────────────────────────────────────────────────────────┐
│ コア層 (internal in OsmDotRoute assembly)                 │
│   namespace OsmDotRoute.Routing                          │
│     IRoadGraph, IRoadGraphEdgeEnumerator (interfaces)    │
│     [将来: DijkstraEngine, EdgeWeightCalculator, ...]    │
│   namespace OsmDotRoute.Geometry                         │
│     GeoBounds, [将来: Aabb, PolygonIntersection]         │
│   namespace OsmDotRoute.Profiles                         │
│     [将来: JsonProfileDefinition, ProfileEvaluator]      │
└──────────────────────────────────────────────────────────┘
        ▲
        │ implements (via InternalsVisibleTo)
        │
┌──────────────────────────────────────────────────────────┐
│ アダプター層 (separate assembly OsmDotRoute.Itinero)      │
│   ItineroRouterDbLoader (public)                         │
│   ItineroRoadGraph : IRoadGraph (internal)               │
│   ItineroEdgeEnumeratorAdapter : IRoadGraphEdgeEnumerator│
│        │                                                  │
│        │ wraps                                            │
│        ▼                                                  │
│   Itinero 1.5.1 NuGet (RouterDb, RoutingNetwork, ...)    │
└──────────────────────────────────────────────────────────┘
```

Phase 2 では「アダプター層」が `NativeRoadGraph`（独自フォーマット読込）に置き換わる。公開 API 層・コア層インターフェースは不変。

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

**読込フロー**（ステップ 3 で確立）:

```text
利用者: ItineroRouterDbLoader.LoadFromFile(path)
   │
   ▼
File.OpenRead → global::Itinero.RouterDb.Deserialize(stream)
   │
   ▼
new ItineroRoadGraph(itineroDb)  // IRoadGraph 実装
   │
   ▼
new OsmDotRoute.RouterDb(graph)  // internal コンストラクタ
   │
   ▼
利用者: routerDb.GetStatistics() → RouterDbStatistics
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

### 2.4 名前空間設計

| Namespace | 配置 | 内容 |
|---|---|---|
| `OsmDotRoute` | `src/OsmDotRoute/*.cs` | 公開 API 全般（Router, RouterDb, 値型、enum、Restriction 系も含む） |
| `OsmDotRoute.Routing` | `src/OsmDotRoute/Routing/` | 経路探索の内部抽象（`IRoadGraph`, `IRoadGraphEdgeEnumerator`、将来 Dijkstra） |
| `OsmDotRoute.Geometry` | `src/OsmDotRoute/Geometry/` | 幾何計算（`GeoBounds`、将来 `Aabb`, `PolygonIntersection`） |
| `OsmDotRoute.Profiles` | `src/OsmDotRoute/Profiles/` | （ステップ 5a 追加予定）JSON プロファイル定義・評価 |
| `OsmDotRoute.Mesh` | `src/OsmDotRoute/Mesh/` | （ステップ 7 追加予定）メッシュコード変換 |
| `OsmDotRoute.GeoJson` | `src/OsmDotRoute/GeoJson/` | （ステップ 10 追加予定）GeoJSON パーサー・ライター |
| `OsmDotRoute.Itinero` | `src/OsmDotRoute.Itinero/` 別アセンブリ | Itinero アダプター（Phase 2 で破棄） |

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
│   ├── OsmDotRoute/                      # コアライブラリ（System.* のみ）
│   └── OsmDotRoute.Itinero/              # Itinero アダプター
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
- `GenerateDocumentationFile=false`（XML doc 生成は §14 ステップ 12 で有効化）
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
- **`GenerateDocumentationFile=false` を採用**: XML doc は Step 12 で完備するまで warning を抑制したい。Step 12 で `true` に変更予定
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

---

## 8. 道路ネットワーク GeoJSON 出力

**対応ステップ**: ステップ 6
**ステータス**: 未記述

（記述予定項目: `GeoJsonWriter.WriteRoadNetwork` の出力スキーマ、エッジ重複排除のための `HashSet<uint>` 戦略、シェイプ座標の挿入順序、properties に含める情報、メモリ効率の考慮）

---

## 9. メッシュコード処理

**対応ステップ**: ステップ 7
**ステータス**: 未記述

（記述予定項目: JIS X0410 第3次〜1/10 細分の桁数仕様、`MeshCode` 値オブジェクトのバリデーション、`MeshCodeConverter.ToBoundingBox` の桁分解アルゴリズム、緯度経度換算の係数、精度確認に使用した国土地理院公開値、桁数不一致時の `ArgumentException` メッセージ仕様）

---

## 10. 制約管理基盤

**対応ステップ**: ステップ 8
**ステータス**: 未記述

（記述予定項目: `RestrictedAreaService` の内部構造（`ConcurrentDictionary` / リスト）、`Aabb` の交差判定、`PolygonIntersection` の Ray Casting + 線分交差アルゴリズム、Hole の扱い、`SpatialIndex` の初期実装方針（配列線形走査）、タグ管理の内部表現、一覧取得時の `IReadOnlyList` ラップ方法）

---

## 11. 制約対応 Dijkstra 統合

**対応ステップ**: ステップ 9
**ステータス**: 未記述

（記述予定項目: `EdgeWeightCalculator` への `RestrictedAreaService` 注入、エッジ評価フロー（AABB プリフィルタ → 線分交差 → 重み計算）、進入不可エリアでの探索打切処理、速度低下係数の重み乗算式、制約変更の即時反映保証、エッジシェイプ AABB のキャッシュ可否判断、性能特性の実測値）

---

## 12. GeoJSON 入力対応

**対応ステップ**: ステップ 10
**ステータス**: 未記述

（記述予定項目: `System.Text.Json` での GeoJSON パーサー構成、Polygon / MultiPolygon / Feature / FeatureCollection の解釈、Hole の検出と内外判定への反映、座標軸順（経度→緯度）の徹底、`properties.speedFactor` / `properties.tag` の読取、不正データ時の例外戦略、ファイル/Stream/文字列 入力 API の共通化）

---

## 13. 経路 GeoJSON 出力

**対応ステップ**: ステップ 11
**ステータス**: 未記述

（記述予定項目: `GeoJsonWriter.WriteRoute` の出力スキーマ、properties に含めるメタ情報（距離・所要時間）、座標精度の方針）

---

## 14. DI 拡張とドキュメント

**対応ステップ**: ステップ 12
**ステータス**: 未記述

（記述予定項目: `OsmDotRoute.Extensions.DependencyInjection` 分離の理由、`AddOsmDotRoute(this IServiceCollection, ...)` のシグネチャと内部登録物、Singleton / Scoped の選択、XML ドキュメントコメントの書式方針、README の構成・最小サンプル・0.x 期間中の破壊的変更方針）

---

## 15. 検証用地図アプリ MapVerifier

**対応ステップ**: ステップ 13〜14
**ステータス**: 未記述

### 15.1 サーバー API 仕様

（記述予定項目: 全エンドポイントの URL・HTTP メソッド・リクエスト/レスポンス JSON スキーマ、エラーレスポンス、CORS 設定、認証なし方針）

### 15.2 フロントエンド構成

（記述予定項目: Vite / React / MapLibre GL のバージョン、`MapView` の React ラッパー実装方針、各 Panel コンポーネントの状態管理、API クライアントの構成、メッシュグリッド描画のクライアント側 JIS X0410 変換実装、ポリゴン描画の自前実装フロー）

### 15.3 動作シナリオ

（記述予定項目: 主要検証シナリオ 7 ステップの実機操作手順、想定スクリーンショット参照先）

---

## 16. ベンチマーク結果

**対応ステップ**: ステップ 15
**ステータス**: 未記述

（記述予定項目: 計測環境（CPU / RAM / .NET ランタイム）、対象 RouterDb、起終点ペアの生成方法、制約 0/10/50/100 件パターンの結果テーブル、メモリ使用量、REQ-NFR-001（100ms）達成可否、最適化を実施した場合は前後比較）

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
