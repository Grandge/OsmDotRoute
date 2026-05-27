# Phase 3 ステップ 3C: ランタイム Itinero 依存削除 計画書

**ステータス**: ドラフト v0.2（v0.1 + 3C.1〜3C.5 全完了 + 設計書 §6 全肉付け、2026-05-27）
**対応ステップ**: Phase 3 ステップ 3C（[Phase 3 実装計画書 §3.4 / §6](phase3_implementation_plan.md)）
**対応要件**: REQ-MAP-006（ランタイム Itinero 依存削除）、REQ-MAP-009（.odrg ロード）、REQ-DEP-003（外部 NuGet 依存削除）
**関連文書**:

- [Phase 3 実装計画書 §3.4 / §5.5-25 / §5.5-26 / §6 R8](phase3_implementation_plan.md)
- [Phase 3 設計書 §6 Itinero 依存撤去と Route.Shape 破壊変更](phase3_design.md)（本ステップで肉付け対象、現状「未記述」プレースホルダ）
- [Phase 3 ステップ 3A 計画書](phase3_step3A_plan.md)（Native 系統 = 撤去後の唯一ルート）
- [Phase 3 ステップ 3B 計画書](phase3_step3B_plan.md)（RestrictedAreaEdgeCache、Native 系統で動作確認済）
- [Phase 3 ステップ 3D 計画書](phase3_step3D_plan.md)（直前ステップ、4 プロファイル動作確認済）
- Phase 2 §5.5-8 確定: `Route.Shape` 破壊変更（`IReadOnlyList<GeoCoordinate>` → `ReadOnlyMemory<GeoCoordinate>`）

---

## 1. 目的とゴール

**目的**: Phase 1 で導入された Itinero 1.5.1 / Itinero.IO.Osm 1.5.1 NuGet 依存および `OsmDotRoute.Itinero` アダプタープロジェクトを**完全撤去**し、ランタイムを Native 系統（`NativeRoadGraph` / `NativeRoadSnapper` + `.odrg`）のみに統一する。同時に Phase 2 §5.5-8 確定済の `Route.Shape` 破壊変更（`IReadOnlyList<GeoCoordinate>` → `ReadOnlyMemory<GeoCoordinate>`）を実施し、Phase 1 §18.4「経路 1 本あたり 77 MB アロケート」削減の最終段階を完了させる。

**Done 判定**:

1. `OsmDotRoute.RouterDb.LoadFromOdrg(string odrgPath)` public static factory 新設、`NativeRoadGraph` + `NativeRoadSnapper` 経由で `RouterDb` を生成
2. `OsmDotRoute.Route.Shape` の型が `IReadOnlyList<GeoCoordinate>` → `ReadOnlyMemory<GeoCoordinate>` に破壊変更
3. `OsmDotRoute.Extensions.DependencyInjection.AddOsmDotRoute(string odrgPath)` 破壊変更（旧 `routerDbPath` から `odrgPath` へ）、内部で `RouterDb.LoadFromOdrg` を呼出
4. `OsmDotRoute.Itinero` プロジェクト**完全削除**（5 ファイル + csproj + sln エントリ + Itinero 1.5.1 / Itinero.IO.Osm 1.5.1 NuGet 参照）
5. Phase 1 既存 Itinero 系経路テスト（`CalculateRouteTests` / `RestrictedRoutingTests` / `SnapToRoadTests` / `OdrgVsRouterDbParityTests` 等）を `.odrg`（津島市）ベースに**全書換**
6. Itinero 専用テスト（`ItineroAdapterTests` / `ItineroRoadGraphQueryEdgesByAabbTests`、計 2 ファイル）削除
7. `MapVerifier.Server` の `OsmDotRoute.Itinero` ProjectReference 削除、RouterDb 比較表示エンドポイント削除、`.odrg` レイヤーのみ残す
8. ベンチマーク `BenchmarkAssets.LoadOsmDotRouterDb` 等の Itinero 経路削除（Native のみに統一）
9. ライブラリ全体で Itinero NuGet 参照ゼロ確認（`grep -r "Itinero"` で本体コード + テスト + ベンチ + sample の全てから消える、文書は除く）
10. `dotnet test` 全 pass 維持（テスト件数は書換による微増減を許容、3C 完了時の実測値を 3C.5 で確定）
11. 設計書 `phase3_design.md` §6 が 3C.5 完了時に肉付け

**Phase 1 既存 526 件全 pass 維持原則の解消**: 3A/3B/3D で死守してきた「Phase 1 既存 526 件全 pass」原則は、3C で意図的に解消する。Phase 1 系経路テストは Itinero 経由で `default.routerdb`（親プロ座標、東京周辺）をロードしていたため、Itinero 撤去で **テストデータ自体を `.odrg`（津島市座標）に切替**する必要がある。これは Phase 3 計画書 §3.4 で予期されていた破壊変更。3C 完了後の新規ベースラインで Phase 3 残ステップ（3E/3F/3G/3I/3H）に進む。

---

## 2. 前提と現状

### 2.1 既存資産（3C 着手時点）

- Phase 3 ステップ 3D 完了（commit `1e4a628`、684 件 pass、計画書 v0.2）
- Native 系統が 3A/3B/3D で完成済：
  - [`NativeRoadGraph`](../src/OsmDotRoute/Native/NativeRoadGraph.cs)（`IRoadGraph` 実装、MMF + Span ゼロコピー、3A.3e）
  - [`NativeRoadSnapper`](../src/OsmDotRoute/Native/NativeRoadSnapper.cs)（`IRoadSnapper` 実装、R-tree クエリ、3A.5b）
  - [`NativeRTreeQuery`](../src/OsmDotRoute/Native/NativeRTreeQuery.cs)（3A.4）
  - 動的制約ホットパス: [`RestrictedAreaEdgeCache`](../src/OsmDotRoute/Restrictions/RestrictedAreaEdgeCache.cs) + `AttachGraph` + eager bake（3B）
  - 4 プロファイル（car / pedestrian / bicycle / truck、3D）
- 既存 [`RouterDb`](../src/OsmDotRoute/RouterDb.cs) コンストラクタ `(IRoadGraph, IRoadSnapper)` は internal（`InternalsVisibleTo` でテストから利用）
- 既存 [`Route`](../src/OsmDotRoute/Route.cs)：`Shape` は `IReadOnlyList<GeoCoordinate>`
- 既存 [`Router`](../src/OsmDotRoute/Router.cs)：Itinero 非依存（`IRoadGraph` / `IRoadSnapper` 経由）
- Itinero NuGet 依存は [`OsmDotRoute.Itinero.csproj`](../src/OsmDotRoute.Itinero/OsmDotRoute.Itinero.csproj) のみ（Core は元から非依存）
- 同梱 `.odrg`: [`samples/Data/tsushima.odrg`](../samples/Data/tsushima.odrg)（3.55 MB、頂点 27,235 / エッジ 38,004、Phase 2 ステップ 5.4 で同梱）

### 2.2 撤去対象（事前調査確定）

| カテゴリ | 場所 | 撤去操作 |
| --- | --- | --- |
| Itinero アダプタプロジェクト | `src/OsmDotRoute.Itinero/` 全 5 ファイル + csproj | 完全削除 |
| Itinero NuGet | `Itinero 1.5.1` / `Itinero.IO.Osm 1.5.1`（`OsmDotRoute.Itinero.csproj` 内） | プロジェクトごと削除 |
| sln エントリ | `OsmDotRoute.sln` の `{7D97B7CD-...}` 行 + Build 構成行 | 削除 |
| Core InternalsVisibleTo | `OsmDotRoute.csproj` の `<InternalsVisibleTo Include="OsmDotRoute.Itinero" />` | 削除 |
| DI 登録 | `ServiceCollectionExtensions.cs` の `ItineroRouterDbLoader.LoadFromFile` 呼出 | `RouterDb.LoadFromOdrg` 呼出に書換 |
| MapVerifier 依存 | `MapVerifier.Server.csproj` の `OsmDotRoute.Itinero` ProjectReference + RouterDb 関連エンドポイント | 削除（`.odrg` レイヤーのみ残置） |
| Itinero 専用テスト | `ItineroAdapterTests.cs` / `ItineroRoadGraphQueryEdgesByAabbTests.cs`（計 2 ファイル） | 削除 |
| Phase 1 系経路テスト | `CalculateRouteTests` / `RestrictedRoutingTests` / `SnapToRoadTests` / `OdrgVsRouterDbParityTests` / `ItineroAdapterTests` 等が `ItineroRouterDbLoader.LoadFromFile(ParentDefaultRouterDb)` 経由 | **`.odrg`（津島市）ベースに全書換** |
| ベンチ Itinero 経路 | `BenchmarkAssets.LoadOsmDotRouterDb` / `RouteWithConstraintsBenchmark` の Itinero モード | Native のみに統一 |
| テスト csproj | `tests/OsmDotRoute.Tests/OsmDotRoute.Tests.csproj` の `OsmDotRoute.Itinero` ProjectReference | 削除 |

### 2.3 ユーザー判断確定（本ステップ着手前、2026-05-27）

- **Q1 = (A) `RouterDb.LoadFromOdrg` static + `AddOsmDotRoute(odrgPath)`**
  - `RouterDb` に public static factory `LoadFromOdrg(string odrgPath)` 追加
  - 内部で `NativeRoadGraph(odrgPath)` + `NativeRoadSnapper(graph)` を生成して `RouterDb` を返す
  - `AddOsmDotRoute(routerDbPath)` → `AddOsmDotRoute(odrgPath)` 破壊変更
  - 既存 `RouterDb(IRoadGraph, IRoadSnapper)` internal は維持（テスト・将来の拡張用）
  - 計画書文言「`MapService`」は実態不在のため新規作成せず、`RouterDb` 直接 factory で対応
- **Q2 = (A) MapVerifier `.odrg` only に切替**
  - `MapVerifier.Server.csproj` から `OsmDotRoute.Itinero` ProjectReference 削除
  - RouterDb 関連エンドポイント (`/api/load-routerdb` / `/api/road-network`) 削除
  - `.odrg` レイヤー (`/api/load-odrg` / `/api/road-network-odrg`) のみ残す
  - Phase 2 ステップ 5.4 で同梱した `samples/Data/tsushima.odrg` がデフォルトロード対象
- **Q3 = (A) Itinero 系経路テストを `.odrg`（津島市）ベースに全書換**
  - `ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb)` → `RouterDb.LoadFromOdrg(TestPaths.TsushimaOdrg)` に一括置換
  - テスト座標も `default.routerdb`（東京周辺）→ 津島市座標に変換
  - アサーション値（距離・所要時間など）は新ベースライン基準で再計算
  - Itinero 比較セマンティクスは失うが、Native 系統テスト（3A.6 16 件 / 3B.5 6 件）で実質カバー済
- **Q4 = (A) 5 サブ分割**
  - 3C.1 `RouterDb.LoadFromOdrg` + `Route.Shape` 破壊変更 + 内部呼出箇所修正
  - 3C.2 テスト全書換（Phase 1 系を `.odrg` ベースに）
  - 3C.3 DI 書換 `AddOsmDotRoute(odrgPath)`
  - 3C.4 `MapVerifier` モード切替 + `OsmDotRoute.Itinero` 完全撤去（sln / csproj / Itinero 専用テスト 2 ファイル削除）
  - 3C.5 ベンチ整理 + 設計書 §6 反映

### 2.4 設計上の歯止め

- **`Router` / `RouterDb` / `RoadEdge` / `IRoadGraph` / `IRoadSnapper` の公開シグネチャは可能な限り維持**: `RouterDb.LoadFromOdrg` 新設と `Route.Shape` 型変更（**意図された破壊変更**）が今回の唯一の公開 API 改修
- **`Route.Shape` 破壊変更の影響を最小化**: 内部実装で `ReadOnlyMemory<GeoCoordinate>` を持ち、テスト・利用側は `.Span` 経由で読出（イテレーション用途は `foreach (var coord in shape.Span)`、配列化は `.ToArray()`）
- **テスト座標切替の波及最小化**: テスト共通ヘルパ（`TestPaths.cs` 等）を使い、座標選定は fixture で集約。テスト個別の座標ハードコードは最小化
- **MapVerifier の `.odrg` レイヤーは Phase 2 で同梱済**: 新規実装は不要、Itinero 経路の削除のみ

---

## 3. アーキテクチャ概要

### 3.1 撤去前後のランタイム参照グラフ

```text
[撤去前 = Phase 3 ステップ 3D 完了時点]
親プロ / ユーザーコード
  ↓ NuGet
OsmDotRoute                ← Itinero NuGet 非依存（元から）
OsmDotRoute.Itinero        ← Itinero 1.5.1 / Itinero.IO.Osm 1.5.1 NuGet 依存
  ├─ ItineroRoadGraph (IRoadGraph 実装)
  ├─ ItineroSnapper (IRoadSnapper 実装)
  ├─ ItineroEdgeEnumeratorAdapter
  ├─ ItineroRouterDbLoader  (.routerdb → OsmDotRoute.RouterDb)
  └─ ItineroRoadGraphTestExtensions (テスト用)
OsmDotRoute.Extensions.DependencyInjection
  └─ AddOsmDotRoute(routerDbPath)  → ItineroRouterDbLoader.LoadFromFile
samples/MapVerifier/MapVerifier.Server
  ├─ ProjectReference OsmDotRoute.Itinero
  ├─ /api/load-routerdb / /api/road-network  (RouterDb モード)
  └─ /api/load-odrg / /api/road-network-odrg  (.odrg モード、Phase 2 で追加)

[撤去後 = Phase 3 ステップ 3C 完了時点]
親プロ / ユーザーコード
  ↓ NuGet
OsmDotRoute                ← System.* + 自前 OsmDotRoute.Pbf のみ依存（REQ-DEP-003 達成）
  ├─ RouterDb.LoadFromOdrg(string odrgPath)  ← 新規 public static factory
  ├─ NativeRoadGraph (IRoadGraph 実装、MMF + Span ゼロコピー)
  └─ NativeRoadSnapper (IRoadSnapper 実装、R-tree)
OsmDotRoute.Extensions.DependencyInjection
  └─ AddOsmDotRoute(odrgPath)  → RouterDb.LoadFromOdrg
samples/MapVerifier/MapVerifier.Server
  └─ /api/load-odrg / /api/road-network-odrg のみ（.odrg only モード）

(OsmDotRoute.Itinero プロジェクトは sln/csproj/コードから完全消滅)
```

### 3.2 主要 API 変更

| 変更 | 旧 | 新 | 影響 |
| --- | --- | --- | --- |
| RouterDb factory | （internal `new RouterDb(IRoadGraph, IRoadSnapper)`） | `public static RouterDb LoadFromOdrg(string odrgPath)` | **追加**（既存 internal は維持） |
| Route.Shape 型 | `IReadOnlyList<GeoCoordinate>` | `ReadOnlyMemory<GeoCoordinate>` | **破壊変更**（Phase 2 §5.5-8 確定済） |
| DI 登録 | `AddOsmDotRoute(string routerDbPath)` | `AddOsmDotRoute(string odrgPath)` | **破壊変更**（パラメータ名と挙動が `.odrg` 経由に） |
| ItineroRouterDbLoader | `LoadFromFile` / `FromItineroRouterDb` public static | **完全削除** | 親プロ修正必要（3F で対応） |
| Itinero アダプタ全クラス | `OsmDotRoute.Itinero.*` | **完全削除** | プロジェクトごと消滅 |

### 3.3 Route.Shape `ReadOnlyMemory<T>` 化のライフタイム設計

`NativeRoadGraph.GetEdgeShape(edgeId) -> ReadOnlySpan<GeoCoordinate>` はキャッシュ配列（`GeoCoordinate[]`）への参照を返す（3A.3e で実装済）。`Route.Shape` を `ReadOnlyMemory<GeoCoordinate>` 化する際の選択肢：

- **(α) Native の Span をそのまま保持**: `MemoryManager<T>` 経由で延命。`NativeRoadGraph.Dispose` 後の Span 参照は不定動作
- **(β) Route 構築時に新規 `GeoCoordinate[]` 配列を確保 + コピー**: ライフタイム独立、ただしアロケート発生

**採用方針**: **(β) Route 構築時に新規配列を確保**。`RouteBuilder` で経路を構築する際にエッジシェイプを 1 本の配列に詰める処理は元から存在（`new List<GeoCoordinate>` → `.ToArray()`）。これを `GeoCoordinate[]` + `.AsMemory()` 化する。Phase 1 §18.4 で言及された 77 MB は **Dijkstra 辺展開時の毎回 alloc** が主因で、これは 3B で既に解消済（`EdgeWeightCalculator.EvaluateConstraintFactor` の `BuildFullShape` 排除）。Route 構築時の 1 回 alloc は経路 1 本に対して 1 度しか発生せず、Phase 3 性能目標 5 MB 以内に収まる見込み。

### 3.4 テストデータ切替（津島市 .odrg ベース）

Phase 1 系経路テストは現状 `TestPaths.ParentDefaultRouterDb`（東京周辺、43k 頂点）を使用。これを `TestPaths.TsushimaOdrg`（津島市、27k 頂点）に切替：

- 座標選定: 既存 Native 系テスト（`NativeRouterDbFixture`、3A.6）で使用済の `ShortPair` / `MediumPair` パターンを流用
- アサーション値: 距離・所要時間は新ベースライン基準で再計算（数値ハードコードを最小化）
- `TestPaths.ParentDefaultRouterDb` 定数は 3C.4 で削除

---

## 4. サブステップ詳細

### 4.1 サブステップ 3C.1: `RouterDb.LoadFromOdrg` + `Route.Shape` 破壊変更

#### 4.1.1 事前調査結果

- [`RouterDb.cs`](../src/OsmDotRoute/RouterDb.cs)（既存）: `IRoadGraph` / `IRoadSnapper` を保持する internal 型、コンストラクタも internal
- [`Route.cs`](../src/OsmDotRoute/Route.cs)（既存）: `Shape` プロパティが `IReadOnlyList<GeoCoordinate>`
- [`RouteBuilder.cs`](../src/OsmDotRoute/Routing/RouteBuilder.cs)（既存）: `Route` 構築時に `List<GeoCoordinate>` → `IReadOnlyList<GeoCoordinate>` 化（実装詳細は 3C.1 着手時に確認）
- 内部呼出箇所: `Router.Calculate` で `RouteBuilder.Build` 呼出、`GeoJsonConverter` / `GeoJsonWriter` で `Route.Shape` を反復、テスト・ベンチで `Route.Shape` 反復

#### 4.1.2 採用設計

- `RouterDb.cs` に `public static RouterDb LoadFromOdrg(string odrgPath)` 追加:
  ```csharp
  public static RouterDb LoadFromOdrg(string odrgPath)
  {
      if (string.IsNullOrWhiteSpace(odrgPath))
          throw new ArgumentException("ファイルパスを指定してください。", nameof(odrgPath));
      if (!File.Exists(odrgPath))
          throw new FileNotFoundException(".odrg ファイルが見つかりません。", odrgPath);

      var graph = new NativeRoadGraph(odrgPath);
      var snapper = new NativeRoadSnapper(graph);
      return new RouterDb(graph, snapper);  // 既存 internal コンストラクタ
  }
  ```
- `Route.cs` の `Shape` 型を `ReadOnlyMemory<GeoCoordinate>` に変更（破壊変更）:
  ```csharp
  public Route(double totalDistanceM, double totalDurationSec, ReadOnlyMemory<GeoCoordinate> shape)
  {
      TotalDistanceM = totalDistanceM;
      TotalDurationSec = totalDurationSec;
      Shape = shape;
  }
  public ReadOnlyMemory<GeoCoordinate> Shape { get; }
  ```
- `RouteBuilder.Build` 内で配列を `.AsMemory()` 化
- 内部呼出箇所（`GeoJsonConverter` / `GeoJsonWriter` 等）を `Shape.Span` 経由に書換

#### 4.1.3 Done 基準

- `RouterDb.LoadFromOdrg("samples/Data/tsushima.odrg")` が成功して `RouterDb` を返す
- 新規テスト `RouterDbLoadFromOdrgTests.cs` 約 5 件（正常 / null パス / 存在しないパス / 不正フォーマット / 内部 graph 動作確認）
- `Route.Shape` 型変更によるコンパイルエラーが全箇所修正完了
- `dotnet test` 全 pass（3C.2 でテスト全書換するため一時的に失敗増えるが、本サブで `Route.Shape` 関連のみは pass）

### 4.2 サブステップ 3C.2: テスト全書換（Phase 1 系を `.odrg` ベースに）

#### 4.2.1 事前調査結果

- Phase 1 系経路テスト（Itinero RouterDb 依存）の主要ファイル: `CalculateRouteTests` / `RestrictedRoutingTests` / `SnapToRoadTests` / `OdrgVsRouterDbParityTests` / `RestrictedAreaServiceGmlTests` / `ItineroAdapterTests`（最後は 3C.4 で削除）
- 全テスト件数 684 件のうち、Itinero RouterDb 依存テストは概算 100+ 件
- テスト座標は `default.routerdb`（東京周辺）と `tsushima.odrg`（津島市、35°N 136°E）で完全に異なる
- Native 系テストは既に `NativeRouterDbFixture`（3A.6）で `samples/Data/tsushima.odrg` を共有ロード

#### 4.2.2 採用設計

- **共通 fixture 拡張**: 既存 `NativeRouterDbFixture` を Phase 1 系テストでも使えるよう公開度を緩和（internal → 必要に応じて Tests 内で共有）
- **書換方針**:
  - `ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb)` → `RouterDb.LoadFromOdrg(TestPaths.TsushimaOdrg)`
  - 座標を東京 → 津島市座標に変換（既存 fixture の `ShortPair` / `MediumPair` を流用）
  - アサーション値（距離・所要時間）は実測値で更新
  - `Route.Shape` 反復は `.Span` 経由に書換（3C.1 で破壊変更済）
- **Itinero 比較セマンティクスを諦める**: Phase 1 系テストの「Itinero `Router.Calculate` との総距離 ±10% 一致」検証は廃止。Native 系テスト（3A.6）でカバー
- **削除候補テスト**: 純粋に Itinero 比較目的のテスト（`ItineroAdapterTests` 等）は本サブでは触らず 3C.4 で一括削除

#### 4.2.3 Done 基準

- 全 Phase 1 系経路テストが `.odrg` ベースで pass
- `ItineroRouterDbLoader` 呼出が Itinero 専用テスト（3C.4 で削除予定）以外から消える
- `dotnet test` 全 pass（Itinero 専用テストは未削除のため残存、ベンチも未整理）

### 4.3 サブステップ 3C.3: DI 書換 `AddOsmDotRoute(odrgPath)`

#### 4.3.1 事前調査結果

- [`ServiceCollectionExtensions.cs`](../src/OsmDotRoute.Extensions.DependencyInjection/ServiceCollectionExtensions.cs): `AddOsmDotRoute(this IServiceCollection, string routerDbPath)` + `AddOsmDotRoute(this IServiceCollection, Action<OsmDotRouteOptions>)` の 2 オーバーロード
- 既存実装: `ItineroRouterDbLoader.LoadFromFile(options.RouterDbPath!)` 呼出
- [`OsmDotRouteOptions.cs`](../src/OsmDotRoute.Extensions.DependencyInjection/OsmDotRouteOptions.cs): `RouterDbPath` プロパティを持つ
- DI テスト: 既存有無を 3C.3 着手時に確認

#### 4.3.2 採用設計

- `OsmDotRouteOptions.RouterDbPath` → `OdrgPath` リネーム（破壊変更）
- `AddOsmDotRoute(this IServiceCollection, string odrgPath)` シグネチャ（パラメータ名変更、挙動は `.odrg` 経由）
- 内部呼出: `ItineroRouterDbLoader.LoadFromFile(...)` → `RouterDb.LoadFromOdrg(...)`
- `OsmDotRoute.Extensions.DependencyInjection.csproj` から `OsmDotRoute.Itinero` ProjectReference 削除（既に削除されている場合あり）

#### 4.3.3 Done 基準

- DI 経由で `AddOsmDotRoute("path/to/tsushima.odrg")` → `Router` Singleton 取得 → 経路計算成功
- 新規 DI テスト 2-3 件（正常登録 / null チェック / 不正パスでの例外）
- `dotnet test` 全 pass

### 4.4 サブステップ 3C.4: MapVerifier モード切替 + `OsmDotRoute.Itinero` 完全撤去

#### 4.4.1 事前調査結果

- [`MapVerifier.Server.csproj`](../samples/MapVerifier/MapVerifier.Server/MapVerifier.Server.csproj): `OsmDotRoute.Itinero` ProjectReference あり
- MapVerifier の `/api/load-routerdb` / `/api/road-network` エンドポイント: `ItineroRouterDbLoader` 経由で RouterDb をロード
- MapVerifier の `/api/load-odrg` / `/api/road-network-odrg` エンドポイント: Phase 2 ステップ 5.4 で追加済、`.odrg` レイヤーを返却
- Itinero 専用テスト: `ItineroAdapterTests.cs` / `ItineroRoadGraphQueryEdgesByAabbTests.cs` 計 2 ファイル
- sln: `OsmDotRoute.Itinero` プロジェクト登録あり（`{7D97B7CD-...}`）

#### 4.4.2 採用設計

- **MapVerifier 改修**:
  - `MapVerifier.Server.csproj` から `OsmDotRoute.Itinero` ProjectReference 削除
  - RouterDb 関連エンドポイント (`/api/load-routerdb` / `/api/road-network`) 削除
  - 関連サービス (`RouterState` 等) のうち RouterDb 専用部分削除
  - `.odrg` レイヤーは Phase 2 既存実装のまま残置
  - README / フロント側の RouterDb 言及を `.odrg` に統一（フロント側は最低限の修正）
- **`OsmDotRoute.Itinero` 完全削除**:
  - `src/OsmDotRoute.Itinero/` ディレクトリ全削除（5 ファイル + csproj + obj/bin/）
  - `OsmDotRoute.sln` から `OsmDotRoute.Itinero` プロジェクトエントリ + Build 構成行削除
  - `OsmDotRoute.csproj` の `<InternalsVisibleTo Include="OsmDotRoute.Itinero" />` 削除
  - `tests/OsmDotRoute.Tests/OsmDotRoute.Tests.csproj` の `OsmDotRoute.Itinero` ProjectReference 削除
- **Itinero 専用テスト削除**:
  - `tests/OsmDotRoute.Tests/ItineroAdapterTests.cs` 削除
  - `tests/OsmDotRoute.Tests/ItineroRoadGraphQueryEdgesByAabbTests.cs` 削除
- **`grep -r "Itinero"` 確認**: 本体コード + テスト + ベンチ + sample から「Itinero」言及がゼロになることをコミット前に確認（文書 / 計画書は除外、Comment / commit メッセージは許容）

#### 4.4.3 Done 基準

- `OsmDotRoute.Itinero` プロジェクトが物理的に削除され、`dotnet build OsmDotRoute.sln` がエラーなく成功
- `Itinero` / `Itinero.IO.Osm` NuGet 依存がプロジェクト全体からゼロ（`grep -r "PackageReference Include=\"Itinero\"" --include="*.csproj"` でヒットなし）
- MapVerifier が `.odrg` ロード + 表示できる（手動確認、起動 → `tsushima.odrg` 読込 → 道路ネットワーク表示）
- `dotnet test` 全 pass

### 4.5 サブステップ 3C.5: ベンチ整理 + 設計書 §6 反映

#### 4.5.1 事前調査結果

- `BenchmarkAssets.LoadOsmDotRouterDb` は Itinero `default.routerdb` をロード（3B.5 時点）
- `BenchmarkAssets.LoadNativeRouterDb` は `.odrg` をロード（3B.5 で新設）
- `RouteWithConstraintsBenchmark` は Itinero / Native-Detached / Native-Attached の 3 モード（3B.5 で改修）
- 設計書 [`phase3_design.md`](phase3_design.md) §6 は未記述プレースホルダ

#### 4.5.2 採用設計

- **ベンチ整理**:
  - `BenchmarkAssets.LoadOsmDotRouterDb` 削除（Itinero 経路）
  - `RouteWithConstraintsBenchmark` から Itinero モード削除、Native-Detached / Native-Attached の 2 モードのみに整理
  - 3B 効果実測値は設計書 §4.5.2 に既に記録済、ベンチ削除でもデータは保持
- **設計書 §6 全 6 サブセクション肉付け** (3A.6 / 3B.5 / 3D.4 パターン踏襲):
  - §6.1 意図 (REQ-MAP-006/009/REQ-DEP-003、Phase 1 §18.4 解消の最終段階)
  - §6.2 採用設計 (撤去前後ランタイム参照グラフ + API 変更表 + Route.Shape ライフタイム設計)
  - §6.3 設計判断の根拠 (ユーザー判断 Q1〜Q4)
  - §6.4 トレードオフ・制約 (Phase 1 既存テスト消失 / Itinero 比較セマンティクス喪失 / 親プロ 3F 対応必須)
  - §6.5 検証方法 (テスト件数推移表)
  - §6.6 実装メモ (主要 commit + 引っかかりポイント + Phase 4+ 申し送り)

#### 4.5.3 Done 基準

- ベンチ整理完了、`OsmDotRoute.Benchmarks` プロジェクトから Itinero 言及消失
- 設計書 §6 が記述充足
- `dotnet test` 全 pass、テスト件数を 3C 完了時の実測値で確定

---

## 5. リスクと対処

| # | リスク | 影響 | 対処方針 |
| --- | --- | --- | --- |
| C1 | `Route.Shape` 破壊変更で内部呼出箇所の修正漏れによりコンパイルエラー残存 | ビルド失敗、テスト不能 | 3C.1 着手時に `grep -r "\.Shape\b"` で全箇所列挙、書換チェックリスト化。`IEnumerable<GeoCoordinate>` 化要求箇所は `.ToArray()` 一時利用 |
| C2 | テスト全書換で Phase 1 セマンティクス（距離精度・スナップ距離）の検証が消失 | リグレッション検出力低下 | 3C.2 着手時に Native 系既存テスト（3A.6 / 3B.5）でカバー範囲を再確認、不足分を追加。Phase 1 比較は Phase 1 ベンチマーク結果 [`phase1_benchmark_results.md`](phase1_benchmark_results.md) に既に記録済 |
| C3 | テスト座標切替で経路結果アサーション値の見積もり違い | テスト不安定化 | 3C.2 着手時に Native 系 `NativeRouterDbFixture` の `ShortPair` / `MediumPair` 距離を実測ベースで再利用、ハードコード値は最小化 |
| C4 | MapVerifier の Itinero 撤去でフロント側コードが RouterDb モード呼出を残しビルドエラー | MapVerifier 起動不能 | 3C.4 着手時に MapVerifier フロント（Web）の `/api/load-routerdb` 呼出箇所をスキャン、`.odrg` 経路に置換 or 削除 |
| C5 | 親プロが `ItineroRouterDbLoader` を直接利用している場合、撤去で親プロビルド不能 | 3F 着手時に親プロ修正範囲拡大 | 3C 範囲外。メモリ [[project_phase3_parent_integration_scan]] で「親プロ Itinero 直接呼出 3 ファイル」既に把握済、3F で一斉修正 |
| C6 | Itinero NuGet 削除後の `dotnet restore` でキャッシュされた Itinero アセンブリ参照がリンガリング | ビルド一時失敗 | 3C.4 完了後に `dotnet clean` + `dotnet restore` で確認、必要なら `obj/bin/` 削除 |
| C7 | `ReadOnlyMemory<T>` 化で外部呼出（親プロ）の `foreach` パターンが壊れる | 親プロビルド失敗（3F 影響） | メモリ [[project_phase3_parent_integration_scan]] = 「`foreach` パターンは `.Span` 介して低影響」と既に判明、3F 着手時に親プロを修正 |

---

## 6. テスト設計サマリ

| サブ | テストファイル | 主要観点 | 想定件数 |
| --- | --- | --- | --- |
| 3C.1 | `RouterDbLoadFromOdrgTests.cs`（新規）/ Route.Shape 関連は既存修正 | 正常ロード / null パス / 存在しないパス / 不正フォーマット / RouterDb 内部 graph 動作確認 | 約 5 件 |
| 3C.2 | 既存 Phase 1 系テスト全書換（`CalculateRouteTests` / `RestrictedRoutingTests` / `SnapToRoadTests` / `OdrgVsRouterDbParityTests` / `RestrictedAreaServiceGmlTests` 等） | `.odrg`（津島市）ベースで Phase 1 系経路テストが pass | 件数変化少（書換が中心、`.odrg` 適合しないテストは削除） |
| 3C.3 | DI テスト（新規 or 既存拡張） | `AddOsmDotRoute(odrgPath)` 正常登録 / null パス / Options 経由設定 | 約 3 件 |
| 3C.4 | Itinero 専用 2 ファイル削除 | テスト件数 -約 N 件（`ItineroAdapterTests` / `ItineroRoadGraphQueryEdgesByAabbTests`） | 削除のみ |
| 3C.5 | ベンチ整理（テストではない）、設計書 §6 反映 | テスト件数を 3C 完了時の実測値で確定 | — |

**累計目標**: 3C 完了時 **約 600〜700 件 pass**（書換 + 削除で微増減、実装時に実測値で確定）

---

## 7. 完了状況

- ✅ Phase 3 ステップ 3D 完了（commit `1e4a628`、684 件 pass、計画書 v0.2）
- ✅ Itinero 依存範囲精査済（§2.2）
- ✅ `MapService` 不在を確認、`RouterDb.LoadFromOdrg` 新設方針確定
- ✅ Phase 1 系経路テストの Itinero 依存度判明（推定 100+ 件、`.odrg` 全書換方針）
- ✅ ユーザー判断 Q1〜Q4 確定（§2.3、2026-05-27）
- ✅ 計画書 v0.1 のユーザー承認 → commit `53ab277`
- ✅ **3C.1 完了** (commit `7c3876f`、+6 件、690 件 pass): RouterDb.LoadFromOdrg + Route.Shape ReadOnlyMemory 化 + 内部呼出箇所 9 ファイル修正
- ✅ **3C.2 完了** (commit `debc66a`、-10 件、680 件 pass): Phase 1 系経路テスト 5 ファイル `.odrg` ベースに全書換、ProfileParityTests / OdrgVsRouterDbParityTests 削除
- ✅ **3C.3 完了** (commit `4ba2c2f`、+5 件、685 件 pass): AddOsmDotRoute(odrgPath) 破壊変更、OsmDotRouteOptions.OdrgPath リネーム、Extensions.DependencyInjection から Itinero 削除、新規 DI テスト 5 件、.gitignore に .claude/ 追加
- ✅ **3C.4 完了** (commit `c02dc8e`、-13 件、672 件 pass): OsmDotRoute.Itinero プロジェクト物理削除 (5 .cs + csproj + sln + 関連 ProjectReference)、Itinero 専用テスト 2 ファイル削除、ベンチ Itinero モード削除、MapVerifier `.odrg` only モード切替、PbfReaderIntegrationTests 削除 (OsmSharp 失効)
- ✅ **3C.5 + 3C 全体完了** (本 commit、0 件、672 件 pass): 設計書 §6 全 6 サブセクション肉付け + 計画書 v0.2 bump + メモリ更新

**3C 達成度**:

- REQ-MAP-006 (ランタイム Itinero 依存削除) / REQ-MAP-009 (.odrg ロード) / REQ-DEP-003 (外部 NuGet 依存削除) **完全達成**
- Itinero 1.5.1 / Itinero.IO.Osm 1.5.1 / OsmSharp NuGet 依存ゼロ (`grep PackageReference Include="Itinero"` 0 件)
- 公開 API 破壊変更 3 件 (RouterDb.LoadFromOdrg 新設 / Route.Shape ReadOnlyMemory / AddOsmDotRoute(odrgPath))、全て計画書 Q1 (A) 確定済
- テスト件数 684 → 672 (-12)、内訳: 新規 +11 件 (LoadFromOdrg 6 + DI 5) / 削除 -23 件 (Itinero/OsmSharp 比較系の戦略的廃止)
- 次ステップ: 3E ベンチマーク再実施 (C0〜C4、Phase 1 基準値との比較、本番統計値で 3B 効果再測定)

---

## 8. 改訂履歴

| 版 | 日付 | 内容 |
| --- | --- | --- |
| v0.1 | 2026-05-27 | 初版起草。3D 完了後の着手前事前調査（Itinero 依存範囲精査 + MapVerifier 構成 + Phase 1 系経路テスト依存度確認 + `MapService` 不在発見）+ ユーザー判断 Q1〜Q4（RouterDb.LoadFromOdrg / MapVerifier .odrg only / テスト全書換 / 5 サブ分割）反映。サブステップ 3C.1〜3C.5 詳細記述、リスク C1〜C7、テスト設計サマリ、設計書 §6 反映方針を確定。 |
| v0.2 | 2026-05-27 | 3C.1〜3C.5 全完了 + 設計書 §6 全肉付け。3C.1 LoadFromOdrg + Route.Shape (+6、commit `7c3876f`) / 3C.2 テスト全書換 (-10、commit `debc66a`) / 3C.3 DI 書換 + .gitignore (+5、commit `4ba2c2f`) / 3C.4 Itinero 完全撤去 + MapVerifier 切替 (-13、commit `c02dc8e`) / 3C.5 設計書 §6 + メモリ更新 (本 commit)。累計 684 → 672 件、Itinero NuGet 依存ゼロ達成。§7 着手前確認 → 完了状況に書換、各サブステップの commit ハッシュ + テスト件数を記録。 |
