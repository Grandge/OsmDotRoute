# OsmDotRoute Phase 1 設計書

**バージョン**: 0.4（進行中）
**作成日**: 2026-05-18
**最終更新**: 2026-05-18
**ステータス**: 進行中（Phase 1 ステップ 1 完了、JSON プロファイル基盤＋難所エリア設計方針確定）
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
| 4. 公開型カタログ | ステップ 2 | 未記述 |
| 5. Itinero アダプター | ステップ 3 | 未記述 |
| 6. 道路スナップ | ステップ 4 | 未記述 |
| 7a. JSON プロファイル基盤 | ステップ 5a | 未記述 |
| 7b. 独自 Dijkstra エンジン | ステップ 5b | 未記述 |
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

（記述予定: 公開 API 層 / コア層 / 抽象化層 / アダプター層 の関係図）

### 2.2 プロジェクト依存関係

（記述予定: OsmDotRoute / OsmDotRoute.Itinero / OsmDotRoute.Extensions.DependencyInjection / samples の依存矢印）

### 2.3 主要データフロー

（記述予定: RouterDb ロード → IRoadGraph → Router.Calculate → DijkstraEngine → RouteBuilder → Route）

### 2.4 名前空間設計

（記述予定: `OsmDotRoute` / `OsmDotRoute.Restrictions` / `OsmDotRoute.Routing` / `OsmDotRoute.Geometry` / `OsmDotRoute.Mesh` / `OsmDotRoute.GeoJson` / `OsmDotRoute.Profiles` の責務分担）

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
**ステータス**: 未記述

（記述予定項目: `Router`, `RouterDb`, `Route`, `GeoCoordinate`, `GeoPolygon`, `MeshCode`, `MeshLevel`, `VehicleProfile`, `RestrictedAreaService`, `RestrictedArea`, `RestrictedAreaId`, `BlockArea`, `SlowArea`, `RoadNetworkGeoJson`, `RouterDbStatistics` 各型のシグネチャ・責務・代表的な利用例）

---

## 5. Itinero アダプター

**対応ステップ**: ステップ 3
**ステータス**: 未記述

（記述予定項目: `IRoadGraph` インターフェース定義、`ItineroRoadGraph` の `RouterDb.Network` ラップ方針、`ItineroRouterDbLoader.LoadFromFile` の責務分担、頂点・エッジ・シェイプ・タグ取得 API の対応関係、統計取得 `RouterDbStatistics` の算出ロジック）

---

## 6. 道路スナップ

**対応ステップ**: ステップ 4
**ステータス**: 未記述

（記述予定項目: `ItineroSnapper` の `Router.Resolve` 利用、検索半径のデフォルト値、ネットワーク外座標時の `null` 返却フロー、`ResolveFailedException` のハンドリング）

---

## 7a. JSON プロファイル基盤

**対応ステップ**: ステップ 5a
**ステータス**: 未記述

（記述予定項目: JSON スキーマ確定版（`profile-schema-v1.json` リンク）、`VehicleProfile` クラスのライフサイクル（Lazy 読込・キャッシュ）、`JsonProfileDefinition` DTO 構造、`ProfileEvaluator.Evaluate` の評価順序（accessTagKeys → highway → maxspeed → fallback）、`EvaluateDifficulty` の動作、同梱 `car.json` / `pedestrian.json` の各セクション設定値とその根拠、Itinero `Vehicle.Car` とのパリティ検証結果（許容差・実測値）、`InvalidProfileException` の発生条件、`DifficultyTypes` const string の利用方法）

## 7b. 独自 Dijkstra エンジン

**対応ステップ**: ステップ 5b
**ステータス**: 未記述

（記述予定項目: アルゴリズム概要、バイナリヒープ実装の選択理由、起点・終点を仮想頂点として扱うかエッジ上の補間点として扱うかの方針、`EdgeWeightCalculator` のエッジ重み = `distance / speed` 公式、`ProfileEvaluator` 経由での speed 取得、`RouteBuilder` の親頂点配列からの経路復元、親プロ Itinero との挙動差の許容範囲）

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
