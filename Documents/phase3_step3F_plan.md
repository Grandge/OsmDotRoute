# Phase 3 ステップ 3F: 親プロジェクト統合・パリティ検証 計画書

**ステータス**: ドラフト v0.1（着手前、2026-05-28）
**対応ステップ**: Phase 3 ステップ 3F（[Phase 3 実装計画書 §3.6 / §6](phase3_implementation_plan.md)）
**対応要件**: 旧 Phase 1 ステップ 16、Phase 2 §8.1 申し送り
**関連文書**:

- [Phase 3 実装計画書 §3.6 / §6](phase3_implementation_plan.md)（本ステップ位置付け）
- [Phase 3 設計書 §8 親プロジェクト統合・パリティ検証](phase3_design.md)（本ステップで肉付け対象、現状「未記述」プレースホルダ）
- [親プロ統合スキャン結果（R9 メモリ）](../../../.claude/projects/d--workspace-DotRoute/memory/project_phase3_parent_integration_scan.md)（2026-05-26 実施）
- 親プロジェクト: `D:/workspace/災害廃棄物処理シミュレーション/`

---

## 1. 目的とゴール

**目的**: 親プロジェクト「災害廃棄物処理シミュレーション」の Itinero 1.5.1 依存を OsmDotRoute Phase 3（`.odrg` + NativeRoadGraph）に完全移行し、シミュレーション動作が破綻しないことを検証する。

**Done 判定**:

1. 親プロ `DisasterWasteSim.Server.csproj` から Itinero NuGet 参照が削除され、OsmDotRoute ProjectReference に置換される
2. `dotnet build` で親プロ全体がエラー 0 でビルド成功
3. 親プロ起動 → シナリオ読込 → 経路計算 → シミュレーション実行が正常動作
4. 設計書 [`phase3_design.md` §8](phase3_design.md) が肉付けされる
5. OsmDotRoute 側 `dotnet test` **672 件 pass** を維持

---

## 2. 前提と現状

### 2.1 親プロの Itinero 利用状況（2026-05-28 再確認、R9 メモリ一致）

| 区分 | ファイル | Itinero 利用内容 | 修正コスト |
| --- | --- | --- | --- |
| **MapService**（ラッパー本体） | `Services/MapService.cs` | `RouterDb.Deserialize` / `new Router` / `Resolve` / `Calculate` / `route.TotalDistance` / `route.Shape` / `enumerator.Shape` / `ResolveFailedException` | 中（全面書換） |
| **ScenarioEditorService**（RouterDb 生成） | `Services/ScenarioEditorService.cs` | `PBFOsmStreamSource` + `FilterBox` + `routerDb.LoadOsmData` + `routerDb.Serialize` | 大（Extractor 子プロセス統合） |
| **MapController** | `Controllers/MapController.cs` | `using IRoute = Itinero.Route;` + `route.Shape` / `route.TotalDistance` / `route.TotalTime` | 小 |
| **BehaviorService ×4** | `Services/*BehaviorService.cs` | `using IRoute = Itinero.Route;` + `route.Shape` （`foreach` パターン） | 小（各 1-2 行） |
| **csproj** | `DisasterWasteSim.Server.csproj` | `Itinero` 1.5.1 / `Itinero.IO.Osm` 1.5.1 PackageReference | 小（2 行削除 + 1 行追加） |

### 2.2 ユーザー判断確定（2026-05-28）

- **参照方式 = ProjectReference**: `DisasterWasteSim.Server.csproj` に `<ProjectReference Include="...\src\OsmDotRoute\OsmDotRoute.csproj" />` 追加。NuGet 公開は 3H で判断
- **Extractor 統合方式 = 子プロセス起動**: `osmdotroute-extractor.exe extract --input pbf --output odrg --bbox ... --profiles car,pedestrian` を `Process.Start` で実行。stdout パース → SignalR 進捗 push
- **サブ分割 = 計画書起草 → 合意 → 着手**

### 2.3 Itinero → OsmDotRoute API 対応表

| Itinero API | OsmDotRoute API | 差分 |
| --- | --- | --- |
| `RouterDb.Deserialize(stream)` | `RouterDb.LoadFromOdrg(string path)` | ファイルパス直接（stream 不可） |
| `new Router(routerDb)` | `new Router(routerDb, restrictions?)` | restrictions オプション追加 |
| `_router.Resolve(profile, lat, lon)` | `_router.SnapToRoad(profile, coord, distance)` | `GeoCoordinate` 型 + 検索半径 |
| `_router.Calculate(profile, start, end)` | `_router.Calculate(profile, from, to)` | `GeoCoordinate` 型に変更 |
| `route.TotalDistance` (float) | `route.TotalDistanceM` (double) | 型が float → double、プロパティ名変更 |
| `route.TotalTime` (float) | `route.TotalDurationSec` (double) | 型 + プロパティ名変更 |
| `route.Shape` (`IReadOnlyList<Coordinate>`) | `route.Shape` (`ReadOnlyMemory<GeoCoordinate>`) | `.Span` 介してアクセス、`Latitude`/`Longitude` は double |
| `routerDb.Network.VertexCount` | `routerDb.GetStatistics().VertexCount` | メソッド経由 |
| `routerDb.Network.EdgeCount` | `routerDb.GetStatistics().EdgeCount` | メソッド経由 |
| `routerDb.LoadOsmData(stream, profiles)` | （なし、Extractor 子プロセス） | 別パイプラインに置換 |
| `routerDb.Serialize(stream)` | （なし、.odrg は Extractor 生成済） | ファイル参照のみ |
| `Itinero.Exceptions.ResolveFailedException` | `SnapToRoad` が `null` を返却 | 例外 → null パターン |

### 2.4 設計上の歯止め

- **OsmDotRoute 本体コードの変更は最小限**。親プロ固有のニーズで公開 API を追加する場合は設計書 §8 に根拠を記録
- **親プロのコードを OsmDotRoute リポジトリにコピーしない**（CLAUDE.md ルール）
- **OsmDotRoute `dotnet test` 672 件 pass を維持**
- **親プロ既存テストが存在すれば全 pass を維持**（テストが存在しない場合は本ステップで追加しない）

---

## 3. サブステップ詳細

### 3F.1: csproj 変更 + MapService 全面書換

**目的**: 親プロの Itinero NuGet 参照を OsmDotRoute ProjectReference に差替え、`MapService.cs` を全面書換する。

**作業内容**:

1. `DisasterWasteSim.Server.csproj`:
   - `Itinero` 1.5.1 / `Itinero.IO.Osm` 1.5.1 PackageReference を削除
   - OsmDotRoute ProjectReference を追加（相対パス `../../../DotRoute/src/OsmDotRoute/OsmDotRoute.csproj`）
   - Trim 無効コメント（Itinero 由来）を更新
2. `MapService.cs` 全面書換:
   - `using Itinero;` 系を `using OsmDotRoute;` に置換
   - `LoadRouterDbFromFile`: `RouterDb.Deserialize(stream)` → `RouterDb.LoadFromOdrg(filePath)`（拡張子 `.routerdb` → `.odrg`）
   - `LoadOsmPbf`: 削除または Extractor 子プロセス呼出に置換（3F.3 で対応）
   - `CalculateRoute`: `_router.Resolve()` → `_router.SnapToRoad()`、`_router.Calculate()` 引数を `GeoCoordinate` 型に
   - `SnapToRoad`: `_router.Resolve()` + `LocationOnNetwork` → `_router.SnapToRoad()`
   - `GetRoadNetworkGeoJson`: `_routerDb.Network` 走査 → `_router.GetRoadNetworkGeoJson()` で置換
   - `ResolveFailedException` catch → `null` チェックに置換
   - 戻り型 `Itinero.Route?` → `OsmDotRoute.Route?`
3. `dotnet build` で親プロエラー 0 確認（BehaviorService は未修正のためコンパイルエラーが出る場合は 3F.2 で対応）

### 3F.2: BehaviorService + Controller の Route.Shape 移行

**目的**: MapController と 4 つの BehaviorService の `route.Shape` / `route.TotalDistance` / `route.TotalTime` アクセスを OsmDotRoute API に対応させる。

**作業内容**:

1. 全 5 ファイルの `using IRoute = Itinero.Route;` を `using OsmRoute = OsmDotRoute.Route;` に変更（型エイリアス維持、ASP.NET Core の `Route` と衝突回避）
2. `route.Shape` の `foreach` パターンを書換:
   ```csharp
   // Before (Itinero):
   foreach (var shape in route.Shape)
       path.Add(new Coordinate { Lat = shape.Latitude, Lon = shape.Longitude });
   
   // After (OsmDotRoute):
   foreach (var shape in route.Shape.Span)
       path.Add(new Coordinate { Lat = (float)shape.Latitude, Lon = (float)shape.Longitude });
   ```
3. `route.TotalDistance` → `route.TotalDistanceM`（double、必要に応じ float キャスト）
4. `route.TotalTime` → `route.TotalDurationSec`（使用箇所があれば）
5. `ResidentBehaviorService.cs` の `using IRoute` 削除（Shape 直接利用なしの場合）
6. `dotnet build` でエラー 0 確認

### 3F.3: ScenarioEditorService の Extractor 子プロセス統合

**目的**: `GenerateRouterDbAsync` の Itinero `LoadOsmData` + `Serialize` パイプラインを `osmdotroute-extractor.exe` 子プロセス呼出に置換する。

**作業内容**:

1. `GenerateRouterDbAsync` メソッド書換:
   - 出力パスを `{name}.routerdb` → `{name}.odrg` に変更
   - Itinero の `PBFOsmStreamSource` + `FilterBox` + `LoadOsmData` + `Serialize` を削除
   - `osmdotroute-extractor.exe extract --input {pbfPath} --output {odrgPath} --bbox {west},{south},{east},{north} --profiles car,pedestrian` を `Process.Start` で実行
   - stdout / stderr をリアルタイム読取 → `_hubContext.Clients.All.SendAsync("EditorProgressUpdate", ...)` で進捗 push
   - 非 0 終了コードで例外送出
   - 生成後の統計（VertexCount / EdgeCount）は `RouterDb.LoadFromOdrg(odrgPath).GetStatistics()` で取得
2. `CurrentMap.RouterDbFile` プロパティ名は温存（親プロ側の変更最小化のため、実態は `.odrg` パス）
3. `osmdotroute-extractor.exe` のパスを設定可能にする（環境変数 or 設定ファイル、見つからない場合は OsmDotRoute プロジェクトの build output を検索）
4. Itinero.IO.Osm 固有の `using` 文を削除
5. `dotnet build` + 手動起動確認

### 3F.4: 動作検証 + 設計書 §8 肉付け + 3F 完了

**目的**: 親プロを起動してシミュレーション動作を検証し、設計書 §8 に反映する。

**作業内容**:

1. 津島市 `.odrg`（`samples/Data/tsushima.odrg` を親プロにコピー or パス指定）で親プロ起動
2. シナリオ読込 → 経路計算 → マップ表示 → 制約付与 → Re-Route の一連フローを手動検証
3. `Documents/phase3_design.md` §8 肉付け（§8.1〜§8.6、3A-3E と同構成）
4. `Documents/phase3_step3F_plan.md` v0.2 bump
5. OsmDotRoute 側 `dotnet test` 672 件 pass 最終確認
6. メモリ `project_phase_status.md` 更新
7. commit + ユーザー報告

---

## 4. 完了状況

| サブステップ | 状態 | 完了 commit | 主要成果 |
| --- | --- | --- | --- |
| 3F.1 | 未着手 | — | csproj 変更 + MapService 全面書換 |
| 3F.2 | 未着手 | — | BehaviorService + Controller の Route.Shape 移行 |
| 3F.3 | 未着手 | — | ScenarioEditorService Extractor 子プロセス統合 |
| 3F.4 | 未着手 | — | 動作検証 + 設計書 §8 + 3F 完了 |

---

## 5. リスクと対処

| # | リスク | 影響 | 対処方針 |
| --- | --- | --- | --- |
| 3F-R1 | 親プロの既存シナリオが `.routerdb` パスを保持 | シナリオ読込時に `.odrg` が見つからない | `MapService.LoadRouterDbFromFile` で拡張子判定、`.routerdb` パスが来た場合は同名 `.odrg` を探す互換ロジック or 再生成案内 |
| 3F-R2 | `osmdotroute-extractor.exe` のパス解決 | 子プロセス起動失敗 | Extractor プロジェクトを `dotnet build` 後の `bin/` 出力から検索、設定で上書き可能に |
| 3F-R3 | Extractor の PBF bbox フィルタリング結果が Itinero `FilterBox` と異なる | エッジ数 / 頂点数の差異、一部経路が変わる | 自前 PBF パーサーの bbox フィルタ精度は Phase 2 で検証済（PAR-1〜PAR-4、±30% 以内）、厳密一致は不要 |
| 3F-R4 | `route.Shape` の `float` → `double` 型差異 | アニメーション座標の精度変化 | 親プロの `Coordinate` は float、`(float)shape.Latitude` キャストで実用上問題なし |
| 3F-R5 | 親プロに既存テストがない場合、回帰検出手段なし | 修正ミスの見落とし | `dotnet build` 成功 + 手動動作検証で代替。自動テスト追加は本ステップ外（親プロのテスト方針は親プロ側判断） |
| 3F-R6 | ProjectReference の相対パスがリポジトリ配置に依存 | 他環境でビルド不可 | 相対パス `../../../DotRoute/src/OsmDotRoute/OsmDotRoute.csproj` を採用、リポジトリ配置前提を README に記載 |

---

## 6. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
| --- | --- | --- | --- |
| 0.1 (draft) | 2026-05-28 | 初版。4 サブ分割。ユーザー判断: 参照方式=ProjectReference / Extractor 統合=子プロセス起動 / サブ分割=計画書起草。Itinero→OsmDotRoute API 対応表。親プロ 7 ファイル修正範囲確定（R9 メモリ再確認）。リスク R1〜R6 | Claude (Opus 4.7) |
