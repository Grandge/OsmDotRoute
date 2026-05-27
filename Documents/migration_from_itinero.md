# Itinero → OsmDotRoute マイグレーションガイド

**対象**: 災害廃棄物処理シミュレーション（`DisasterWasteSim.Server`）
**移行元**: Itinero 1.5.1 + Itinero.IO.Osm 1.5.1
**移行先**: OsmDotRoute Phase 3（`.odrg` 形式、NativeRoadGraph、動的制約対応）
**作成日**: 2026-05-28
**作成元**: OsmDotRoute Phase 3 ステップ 3F

---

## 1. 概要

OsmDotRoute は Itinero の単純置換ではなく、**動的制約に特化した OSM ルーティングライブラリ**として独自設計されている。主な違い:

- **データ形式**: Itinero `.routerdb` → OsmDotRoute `.odrg`（MemoryMappedFile ゼロコピー読込）
- **ランタイム依存**: Itinero NuGet 群 → **System.\* 標準ライブラリのみ**
- **動的制約**: Itinero にはない `RestrictedAreaService`（シミュレーション中に制約 add/remove → 次回経路計算で即時反映）
- **プロファイル**: car / pedestrian に加え **bicycle / truck（10t、日本道路法ベース）** を同梱
- **性能**: Phase 1 比 C0 = 4-5 倍高速（7.7 ms）、制約 100 件下 C1/C0 = 0.65x（むしろ高速化）、Allocated 25 倍削減（3.12 MB）

---

## 2. 前提条件

- OsmDotRoute リポジトリが `D:\workspace\DotRoute\` に clone されている
- `dotnet build` で OsmDotRoute がビルド成功する
- 津島市 `.odrg` が `D:\workspace\DotRoute\samples\Data\tsushima.odrg` に存在する

---

## 3. プロジェクト設定

### 3.1 NuGet 参照の削除

`DisasterWasteSim.Server.csproj` から Itinero 関連の PackageReference を削除:

```xml
<!-- 削除 -->
<PackageReference Include="Itinero" Version="1.5.1" />
<PackageReference Include="Itinero.IO.Osm" Version="1.5.1" />
```

### 3.2 OsmDotRoute ProjectReference の追加

```xml
<!-- 追加（相対パスは実際の配置に合わせて調整） -->
<ProjectReference Include="..\..\..\DotRoute\src\OsmDotRoute\OsmDotRoute.csproj" />
```

sln にも OsmDotRoute プロジェクトを追加:

```powershell
dotnet sln add ..\..\..\DotRoute\src\OsmDotRoute\OsmDotRoute.csproj
```

### 3.3 Trim 設定コメントの更新

```xml
<!-- 旧: Trim無効（Itinero/ONNX 非対応） -->
<!-- 新: Trim無効（ONNX 非対応） -->
```

---

## 4. API 対応表

| # | Itinero API | OsmDotRoute API | 備考 |
|---|---|---|---|
| 1 | `using Itinero;` | `using OsmDotRoute;` | |
| 2 | `using Itinero.Profiles;` | `using OsmDotRoute;` | `VehicleProfile` は OsmDotRoute 名前空間 |
| 3 | `using Itinero.IO.Osm;` | （削除） | PBF 読込は Extractor 子プロセスで実施 |
| 4 | `using IRoute = Itinero.Route;` | `using OsmRoute = OsmDotRoute.Route;` | ASP.NET Core `Route` との衝突回避 |
| 5 | `RouterDb.Deserialize(stream)` | `RouterDb.LoadFromOdrg(string path)` | stream → ファイルパス直接 |
| 6 | `new Router(routerDb)` | `new Router(routerDb)` | 第 2 引数に `RestrictedAreaService?` を渡せる |
| 7 | `_router.Resolve(profile, lat, lon)` | `_router.SnapToRoad(profile, coord, distance)` | §5.2 参照 |
| 8 | `_router.Calculate(profile, start, end)` | `_router.Calculate(profile, from, to)` | §5.1 参照 |
| 9 | `route.TotalDistance` (float, m) | `route.TotalDistanceM` (double, m) | 型が float → double |
| 10 | `route.TotalTime` (float, s) | `route.TotalDurationSec` (double, s) | 型が float → double |
| 11 | `route.Shape` (`IReadOnlyList<Coordinate>`) | `route.Shape` (`ReadOnlyMemory<GeoCoordinate>`) | §5.3 参照 |
| 12 | `shape.Latitude` (float) | `shape.Latitude` (double) | GeoCoordinate は double |
| 13 | `shape.Longitude` (float) | `shape.Longitude` (double) | GeoCoordinate は double |
| 14 | `routerDb.Network.VertexCount` | `routerDb.GetStatistics().VertexCount` | メソッド経由 |
| 15 | `routerDb.Network.EdgeCount` | `routerDb.GetStatistics().EdgeCount` | メソッド経由 |
| 16 | `routerDb.LoadOsmData(stream, profiles)` | Extractor 子プロセス | §6 参照 |
| 17 | `routerDb.Serialize(stream)` | （不要、`.odrg` は Extractor が生成） | |
| 18 | `Itinero.Exceptions.ResolveFailedException` | `SnapToRoad` / `Calculate` が `null` を返却 | 例外 → null パターン |
| 19 | `routerDb.GetSupportedProfile(name)` | `VehicleProfile.Car` 等の静的プロパティ | §5.1 参照 |
| 20 | `Vehicle.Car` / `Vehicle.Pedestrian` | `VehicleProfile.Car` / `VehicleProfile.Pedestrian` | |

---

## 5. ファイル別 before/after コード例

### 5.1 MapService.cs — 経路計算

```csharp
// ===== BEFORE (Itinero) =====
using Itinero;
using Itinero.Osm.Vehicles;
using Itinero.Profiles;

private RouterDb? _routerDb;
private Router? _router;

public void LoadRouterDbFromFile(string filePath)
{
    using var stream = File.OpenRead(filePath);
    _routerDb = RouterDb.Deserialize(stream);
    _router = new Router(_routerDb);
}

public Itinero.Route? CalculateRoute(float startLat, float startLon,
    float endLat, float endLon, string profileName = "car")
{
    if (_routerDb == null || _router == null) return null;
    var profile = _routerDb.GetSupportedProfile(profileName);
    try
    {
        var startPoint = _router.Resolve(profile, startLat, startLon);
        var endPoint = _router.Resolve(profile, endLat, endLon);
        return _router.Calculate(profile, startPoint, endPoint);
    }
    catch (Itinero.Exceptions.ResolveFailedException)
    {
        return null;
    }
}
```

```csharp
// ===== AFTER (OsmDotRoute) =====
using OsmDotRoute;

private RouterDb? _routerDb;
private Router? _router;

public void LoadFromOdrg(string odrgPath)
{
    _routerDb = RouterDb.LoadFromOdrg(odrgPath);
    _router = new Router(_routerDb);
}

public OsmDotRoute.Route? CalculateRoute(float startLat, float startLon,
    float endLat, float endLon, string profileName = "car")
{
    if (_router == null) return null;
    var profile = ResolveProfile(profileName);
    var from = new GeoCoordinate(startLat, startLon);
    var to = new GeoCoordinate(endLat, endLon);
    return _router.Calculate(profile, from, to);
    // Calculate 内部でスナップも自動実行される。
    // スナップ失敗・経路発見失敗時は null が返る。
}

private static VehicleProfile ResolveProfile(string name) => name switch
{
    "car" => VehicleProfile.Car,
    "pedestrian" => VehicleProfile.Pedestrian,
    "bicycle" => VehicleProfile.Bicycle,
    "truck" => VehicleProfile.Truck,
    _ => VehicleProfile.Car
};
```

### 5.2 MapService.cs — SnapToRoad

```csharp
// ===== BEFORE (Itinero) =====
public (float Lat, float Lon)? SnapToRoad(float lat, float lon,
    string profileName = "car", float searchDistanceM = 500f)
{
    if (_routerDb == null || _router == null) return null;
    var profile = _routerDb.GetSupportedProfile(profileName);
    try
    {
        var point = _router.Resolve(profile, lat, lon, searchDistanceM);
        var location = point.LocationOnNetwork(_routerDb);
        return ((float)location.Latitude, (float)location.Longitude);
    }
    catch (Itinero.Exceptions.ResolveFailedException)
    {
        return null;
    }
}
```

```csharp
// ===== AFTER (OsmDotRoute) =====
public (float Lat, float Lon)? SnapToRoad(float lat, float lon,
    string profileName = "car", float searchDistanceM = 500f)
{
    if (_router == null) return null;
    var profile = ResolveProfile(profileName);
    var coord = new GeoCoordinate(lat, lon);
    var snapped = _router.SnapToRoad(profile, coord, searchDistanceM);
    if (snapped == null) return null;
    return ((float)snapped.Value.Latitude, (float)snapped.Value.Longitude);
}
```

### 5.3 MapController.cs / BehaviorService — route.Shape 走査

全ファイルで同じパターンで書き換える:

```csharp
// ===== BEFORE (Itinero) =====
using IRoute = Itinero.Route;

foreach (var shape in route.Shape)
{
    path.Add(new Coordinate { Lat = shape.Latitude, Lon = shape.Longitude });
}
```

```csharp
// ===== AFTER (OsmDotRoute) =====
using OsmRoute = OsmDotRoute.Route;

foreach (var shape in route.Shape.Span)
{
    path.Add(new Coordinate { Lat = (float)shape.Latitude, Lon = (float)shape.Longitude });
}
```

**該当ファイル一覧**（`route.Shape` の `foreach` パターン）:

| ファイル | 箇所 |
|---|---|
| `Controllers/MapController.cs` | 1 箇所（GeoJSON 化） |
| `Services/SurveyAgentBehaviorService.cs` | 1 箇所（パス生成） |
| `Services/DemolitionCrewBehaviorService.cs` | 1 箇所（パス生成） |
| `Services/WasteTransportAgentBehaviorService.cs` | 2 箇所（パス生成） |

### 5.4 MapController.cs — TotalDistance / TotalTime

```csharp
// ===== BEFORE (Itinero) =====
TotalDistance = route.TotalDistance,
TotalTime = route.TotalTime,
```

```csharp
// ===== AFTER (OsmDotRoute) =====
TotalDistance = (float)route.TotalDistanceM,
TotalTime = (float)route.TotalDurationSec,
```

### 5.5 MapService.cs — GetRoadNetworkGeoJson

```csharp
// ===== BEFORE (Itinero) =====
// routerDb.Network.GetEdgeEnumerator() を走査して GeoJSON を手動構築
var enumerator = _routerDb.Network.GetEdgeEnumerator();
// ... 複数十行の手動 GeoJSON 構築 ...
```

```csharp
// ===== AFTER (OsmDotRoute) =====
public string GetRoadNetworkGeoJson()
{
    if (_router == null) return "{}";
    var result = _router.GetRoadNetworkGeoJson();
    return result.Json;
}
```

### 5.6 MapService.cs — 統計情報

```csharp
// ===== BEFORE (Itinero) =====
nodeCount = (long)_routerDb.Network.VertexCount,
edgeCount = (long)_routerDb.Network.EdgeCount,
```

```csharp
// ===== AFTER (OsmDotRoute) =====
var stats = _routerDb.GetStatistics();
nodeCount = (long)stats.VertexCount,
edgeCount = (long)stats.EdgeCount,
```

### 5.7 ResidentBehaviorService.cs

`using IRoute = Itinero.Route;` が存在するが `route.Shape` の直接利用はなし。`route.TotalDistance` のみ使用:

```csharp
// ===== BEFORE =====
travelTime = (float)(route.TotalDistance / 416.6);

// ===== AFTER =====
travelTime = (float)(route.TotalDistanceM / 416.6);
```

---

## 6. ScenarioEditorService — PBF → .odrg 生成

### 6.1 概要

Itinero の `LoadOsmData` + `Serialize` を `osmdotroute-extractor.exe` の子プロセス呼出に置換する。

### 6.2 before/after

```csharp
// ===== BEFORE (Itinero) =====
using Itinero;
using Itinero.IO.Osm;
using Itinero.Osm.Vehicles;
using OsmSharp.Streams;

var outputPath = Path.Combine(UserScenariosDir, $"{name}.routerdb");

await Task.Run(() =>
{
    using var stream = File.OpenRead(pbfPath);
    var source = new PBFOsmStreamSource(stream);
    var filtered = source.FilterBox(
        CurrentMap.NorthWestLon, CurrentMap.NorthWestLat,
        CurrentMap.SouthEastLon, CurrentMap.SouthEastLat);
    routerDb = new RouterDb();
    routerDb.LoadOsmData(filtered, Vehicle.Car, Vehicle.Pedestrian);
}, cancellationToken);

await Task.Run(() =>
{
    var tempPath = outputPath + ".tmp";
    using (var output = File.Create(tempPath))
        routerDb.Serialize(output);
    File.Move(tempPath, outputPath, overwrite: true);
}, cancellationToken);

CurrentMap.RouterDbFile = outputPath;
```

```csharp
// ===== AFTER (OsmDotRoute) =====
using System.Diagnostics;
using OsmDotRoute;

var outputPath = Path.Combine(UserScenariosDir, $"{name}.odrg");

// Extractor exe のパス（ビルド成果物から検索、または設定で指定）
var extractorExe = FindExtractorExe();

// bbox = west,south,east,north
var bbox = $"{CurrentMap.NorthWestLon},{CurrentMap.SouthEastLat}," +
           $"{CurrentMap.SouthEastLon},{CurrentMap.NorthWestLat}";

var psi = new ProcessStartInfo
{
    FileName = extractorExe,
    Arguments = $"extract --input \"{pbfPath}\" --output \"{outputPath}\" " +
                $"--bbox {bbox} --profiles car,pedestrian",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true
};

using var process = new Process { StartInfo = psi };
process.OutputDataReceived += (_, e) =>
{
    if (e.Data != null)
        _hubContext.Clients.All.SendAsync("EditorProgressUpdate", new
        {
            stage = "extracting",
            message = e.Data
        });
};
process.Start();
process.BeginOutputReadLine();
await process.WaitForExitAsync(cancellationToken);

if (process.ExitCode != 0)
{
    var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
    throw new InvalidOperationException($"Extractor failed (exit {process.ExitCode}): {stderr}");
}

// 生成後の統計取得
var routerDb = RouterDb.LoadFromOdrg(outputPath);
var stats = routerDb.GetStatistics();

CurrentMap.RouterDbFile = outputPath;
```

### 6.3 Extractor exe のパス解決

`osmdotroute-extractor.exe` は OsmDotRoute ソリューションの `src/OsmDotRoute.Extractor` プロジェクトをビルドすると生成される:

```
DotRoute/src/OsmDotRoute.Extractor/bin/Release/net9.0/osmdotroute-extractor.exe
```

推奨: 環境変数 `OSMDOTROUTE_EXTRACTOR_PATH` で上書き可能にし、未設定時は既知のビルド出力パスを検索。

### 6.4 CLI リファレンス

```
osmdotroute-extractor extract
  --input <path>        入力 OSM PBF ファイル
  --output <path>       出力 .odrg ファイル
  --bbox <w,s,e,n>      抽出範囲 (経度,緯度,経度,緯度)
  --profiles <list>     カンマ区切りプロファイル名 (car,pedestrian,bicycle,truck)
```

---

## 7. 既存シナリオの .routerdb → .odrg 移行

### 7.1 手順

既存シナリオの `.routerdb` ファイルは Itinero 形式のため OsmDotRoute では読めない。以下の手順で再生成する:

1. 元の PBF ファイルが手元にある場合:
   ```powershell
   osmdotroute-extractor extract `
     --input path/to/area.osm.pbf `
     --output path/to/scenario.odrg `
     --bbox <west>,<south>,<east>,<north> `
     --profiles car,pedestrian
   ```

2. PBF がない場合: ScenarioEditor の「道路データ生成」ボタンで再生成（§6 の AFTER コードが動作すれば自動的に `.odrg` が生成される）

### 7.2 シナリオ JSON の RouterDbFile パス

シナリオ JSON が `"RouterDbFile": "path/to/xxx.routerdb"` を保持している場合、以下の対応が考えられる:

- **(A) MapService で拡張子判定**: `.routerdb` パスが来たら同ディレクトリの `xxx.odrg` を探す互換ロジック
- **(B) シナリオ JSON を手動更新**: 拡張子を `.odrg` に変更
- **(C) 初回ロード時に再生成**: `.odrg` が見つからない場合に PBF から自動再生成（PBF パスが必要）

推奨は **(A)** の互換ロジック。MapService のロード時に:

```csharp
public void LoadFromFile(string filePath)
{
    var odrgPath = filePath;
    if (filePath.EndsWith(".routerdb", StringComparison.OrdinalIgnoreCase))
    {
        odrgPath = Path.ChangeExtension(filePath, ".odrg");
        if (!File.Exists(odrgPath))
            throw new FileNotFoundException(
                $".odrg ファイルが見つかりません。Extractor で再生成してください: {odrgPath}");
    }
    _routerDb = RouterDb.LoadFromOdrg(odrgPath);
    _router = new Router(_routerDb);
}
```

---

## 8. 動的制約の活用（オプション）

OsmDotRoute は Itinero にない**動的制約機能**を持つ。シミュレーション中に進入不可/移動困難エリアを追加・削除でき、次回の `Router.Calculate` で即時反映される:

```csharp
// 制約サービスを Router と連携
var restrictions = new RestrictedAreaService();
var router = new Router(routerDb, restrictions);

// 浸水エリアを進入不可として追加
var polygon = new GeoPolygon(new[]
{
    new GeoCoordinate(35.15, 136.72),
    new GeoCoordinate(35.15, 136.74),
    new GeoCoordinate(35.17, 136.74),
    new GeoCoordinate(35.17, 136.72),
});
var areaId = restrictions.AddBlockArea(polygon, tag: "flood_zone_1");

// この後の Calculate は浸水エリアを迂回する
var route = router.Calculate(VehicleProfile.Car, from, to);

// 浸水解除
restrictions.Remove(areaId);
```

**メッシュコードによる制約**:

```csharp
// 250m メッシュで制約
var meshCode = new MeshCode(5339461111);
restrictions.AddBlockArea(meshCode, tag: "mesh_block");

// 複数メッシュ一括
var meshCodes = new[] { new MeshCode(5339461111), new MeshCode(5339461112) };
restrictions.AddDifficultyArea(meshCodes, DifficultyTypes.Flooding, tag: "flood");
```

**KSJ GML ファイルからの制約読込**:

```csharp
// 国土数値情報 GML ファイルから浸水想定区域を一括追加
var ids = restrictions.AddBlockAreaFromGmlFile(
    "path/to/A31-12_24.xml",
    mapBounds: new MapBounds(southWest, northEast),
    tag: "ksj_flood");
```

---

## 9. DI 登録（オプション）

`OsmDotRoute.Extensions.DependencyInjection` を使う場合:

```csharp
// csproj に追加
// <ProjectReference Include="...\DotRoute\src\OsmDotRoute.Extensions.DependencyInjection\..." />

// Program.cs
builder.Services.AddOsmDotRoute("path/to/area.odrg");

// DI で注入される型: RouterDb, Router, RestrictedAreaService (全て Singleton)
```

ただし親プロは `MapService` が独自ラッパーとして機能しているため、DI 登録を使わず `MapService` 内部で直接 `RouterDb.LoadFromOdrg` + `new Router` する方が影響範囲が小さい。

---

## 10. 既知の差分と注意事項

### 10.1 座標精度

| 項目 | Itinero | OsmDotRoute |
|---|---|---|
| GeoCoordinate 型 | `float` (32bit) | `double` (64bit) |
| 精度 | ≒ 1m | ≒ 0.01mm |

親プロの `Coordinate` 構造体が `float Lat, Lon` を使用しているため、OsmDotRoute の `double` 値を `(float)` キャストする必要がある。実用上の精度差はない。

### 10.2 グラフ規模差

同じ PBF / bbox でも Itinero と OsmDotRoute でエッジ数 / 頂点数が異なる:

- Itinero は内部で道路リンク分割を行うため頂点数が多くなる傾向
- OsmDotRoute は OSM way の交差点のみを頂点とするため頂点数が少ない傾向
- Phase 2 検証 (PAR-1〜PAR-4): 頂点数比 0.892、辺数比 0.937（±30% 以内）

**経路結果は完全一致しない**が、同一 OD ペアで経路長は概ね同等（Phase 1 検証: 89/89 ペア ±10% 以内、Mean 0.07%）。

### 10.3 プロファイル差異

| プロファイル | Itinero | OsmDotRoute |
|---|---|---|
| Car | Itinero 内蔵（Lua） | `car.json`（JSON 外部化、同等スペック） |
| Pedestrian | Itinero 内蔵（Lua） | `pedestrian.json`（JSON 外部化、同等スペック） |
| Bicycle | なし（使用時） | `bicycle.json`（15 km/h、高速道路通行不可） |
| Truck | なし（使用時） | `truck.json`（10t、日本道路法ベース、車両制限評価あり） |

### 10.4 Route.Shape のライフタイム

`Route.Shape` は `ReadOnlyMemory<GeoCoordinate>` 型。`Route` オブジェクトが GC されるまで有効。`RouterDb.Dispose()` とは独立のライフタイム（Route 構築時にコピー済）。

### 10.5 性能参考値（津島市 53k 頂点 / 74k エッジ）

| 指標 | 値 |
|---|---|
| C0（制約なし、Car、100 ペア平均） | 7.70 ms |
| C1（制約 100 件、Car） | 5.01 ms |
| C0 Allocated | 3.12 MB |
| Snap 単独 | 33.4 μs |
| 制約 add/remove | 118 μs/op（8,470 ops/sec） |

---

## 11. 修正対象ファイル チェックリスト

| # | ファイル | 修正内容 | 参照 |
|---|---|---|---|
| 1 | `DisasterWasteSim.Server.csproj` | Itinero NuGet 削除 + OsmDotRoute ProjectReference 追加 | §3 |
| 2 | `Services/MapService.cs` | 全面書換（LoadFromOdrg / Calculate / SnapToRoad / GeoJSON） | §5.1, §5.2, §5.5, §5.6 |
| 3 | `Controllers/MapController.cs` | `using` エイリアス + `route.Shape.Span` + プロパティ名 | §5.3, §5.4 |
| 4 | `Services/ResidentBehaviorService.cs` | `using` エイリアス + `TotalDistanceM` | §5.7 |
| 5 | `Services/SurveyAgentBehaviorService.cs` | `using` エイリアス + `route.Shape.Span` | §5.3 |
| 6 | `Services/DemolitionCrewBehaviorService.cs` | `using` エイリアス + `route.Shape.Span` | §5.3 |
| 7 | `Services/WasteTransportAgentBehaviorService.cs` | `using` エイリアス + `route.Shape.Span` (2 箇所) | §5.3 |
| 8 | `Services/ScenarioEditorService.cs` | GenerateRouterDbAsync → Extractor 子プロセス | §6 |

---

## 12. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 | 2026-05-28 | 初版。API 対応表 20 項目 + 親プロ 8 ファイルの before/after コード例 + Extractor 子プロセスパターン + .routerdb→.odrg 移行手順 + 動的制約活用ガイド + DI 登録オプション + 既知差分 5 項目 | Claude (Opus 4.7) |
