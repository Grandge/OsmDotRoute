# OsmDotRoute 使い方ガイド

[English](usage_guide.en.md) | 日本語

OsmDotRoute で OSM データから経路を計算するまでの一連の手順をまとめる。
API リファレンスではなく、**最初に動かすまで**を最短で辿れる実用ガイドを目的とする。

> **インストール不要で試す**: コアエンジンを WebAssembly 化したライブデモを
> **[GitHub Pages](https://grandge.github.io/OsmDotRoute/)** で公開中。
> 経路探索・動的制約・Re-Route をそのままブラウザで体験できる。

対象バージョン: Phase 3（`.odrg` ランタイム、System.* のみ依存、net9.0）。

---

## 1. 概要

OsmDotRoute は、OSM データを**事前抽出した独自バイナリ `.odrg`** をランタイムで読み込み、
動的通行制限（進入不可・移動困難エリア）を実行中に追加／削除しながら Dijkstra 経路を計算するライブラリ。

利用の流れは大きく 3 段階に分かれる。

```text
[1] PBF 入手           [2] .odrg 抽出 (CLI)          [3] 経路探査 (ライブラリ)
  Geofabrik 等から   →   osmdotroute-extractor    →   RouterDb.LoadFromOdrg(...)
  *.osm.pbf を取得       extract で範囲を切り出す       new Router(db).Calculate(...)
```

- **[1] と [2] はオフラインの準備**（1 回だけ実行し、`.odrg` を成果物として保管）。
- **[3] がアプリ実行時の処理**。`.odrg` を読み込めば PBF も Extractor も不要で、依存は System.* のみ。
- プロファイル（car / pedestrian など）は **[2] の抽出時に `.odrg` へ bake** し、**[3] で同じ名前を指定**して使う。

| 段階 | 使うもの | 成果物 |
| --- | --- | --- |
| [1] PBF 準備 | ブラウザ / wget 等 | `*.osm.pbf` |
| [2] .odrg 抽出 | `osmdotroute-extractor` (CLI) | `*.odrg` |
| [3] 経路探査 | `OsmDotRoute` (ライブラリ参照) | `Route` |

---

## 2. ライブラリの入手とセットアップ

OsmDotRoute は GitHub で公開されている。NuGet 公開前は、リポジトリを取得して
**ソース参照**で利用する（ランタイム依存は System.* のみ）。

### 必要環境

- .NET 9 SDK 以降
- （`.odrg` をブラウザ静的サイトで動かす場合のみ）Node.js 18 以降

### リポジトリの取得

更新を追いやすい `git clone`:

```powershell
git clone https://github.com/Grandge/OsmDotRoute.git
cd OsmDotRoute
```

または GitHub のページで「Code」→「Download ZIP」から ZIP を取得して展開する。
特定バージョンを使いたい場合は「Releases」からタグ付きアーカイブを取得する。

### ビルドと動作確認

```powershell
dotnet build
dotnet test    # 任意。全テストが pass することを確認できる
```

### 自分のプロジェクトから参照する

取得したソースへプロジェクト参照を張る:

```xml
<ProjectReference Include="path/to/OsmDotRoute/src/OsmDotRoute/OsmDotRoute.csproj" />
```

DI 統合（§7.2）を使う場合は加えて:

```xml
<ProjectReference Include="path/to/OsmDotRoute/src/OsmDotRoute.Extensions.DependencyInjection/OsmDotRoute.Extensions.DependencyInjection.csproj" />
```

`.odrg` を生成する抽出ツール `osmdotroute-extractor` は同じリポジトリの
`src/OsmDotRoute.Extractor` にある（使い方は §4）。

---

## 3. PBF の準備方法

経路の元データは OSM の PBF 形式（`*.osm.pbf`）。日本国内なら [Geofabrik](https://download.geofabrik.de/asia/japan.html)
の都道府県別ダウンロードが扱いやすい。

1. Geofabrik の日本ページから対象地域の `*.osm.pbf` をダウンロードする
   （例: `chubu-latest.osm.pbf`、`kanto-latest.osm.pbf`）。
2. ファイルは大きい（地方単位で数百 MB〜）。抽出時に bbox で必要範囲だけ切り出すので、
   **PBF 自体は広めの地方単位**で取得しておけばよい。

> **ライセンス**: OSM データは ODbL。`.odrg` を配布・公開する場合は
> 「© OpenStreetMap contributors」の帰属表示が必要。

### bbox（抽出範囲）の決め方

抽出範囲は WGS84 経緯度の矩形（bounding box）で指定する。**順序は経度→緯度**:

```text
minLon,minLat,maxLon,maxLat
例) 東京駅周辺: 139.74,35.65,139.79,35.70
```

> **注意**: bbox は `Lon,Lat`（経度が先）の順。一方、ライブラリの座標型
> `GeoCoordinate(Latitude, Longitude)` は**緯度が先**。引数の順序を取り違えやすいので注意。

---

## 4. .odrg の作成方法

`osmdotroute-extractor` の `extract` サブコマンドで PBF から `.odrg` を生成する。

### コマンド書式

```text
osmdotroute-extractor extract \
  --input  <file.osm.pbf>            # -i  入力 PBF（必須）
  --output <file.odrg>               # -o  出力 .odrg（必須）
  --bbox   minLon,minLat,maxLon,maxLat   # 抽出範囲 WGS84（必須）
  --profiles car,pedestrian          # -p  bake するプロファイル（省略時 car,pedestrian）
```

### ソースから実行する場合

NuGet 配布前は、リポジトリ内から `dotnet run` で実行する（PowerShell では `;` で連結、`&&` は使わない）:

```powershell
dotnet run --project src/OsmDotRoute.Extractor -- `
  extract `
  --input  D:\osm\chubu-latest.osm.pbf `
  --output D:\odrg\tokyo.odrg `
  --bbox   139.74,35.65,139.79,35.70 `
  --profiles car,pedestrian,bicycle,truck
```

実行すると入力・範囲・プロファイルが表示され、抽出 → 書き出しが進む:

```text
input    : D:\osm\chubu-latest.osm.pbf
output   : D:\odrg\tokyo.odrg
bbox     : 139.74,35.65,139.79,35.70
profiles : car,pedestrian,bicycle,truck

抽出開始...
抽出完了: 頂点 12,345 件 / エッジ 23,456 件 (3.2 秒)
書出開始...
書出完了: 1,234,567 byte (0.4 秒)
出力ファイル: D:\odrg\tokyo.odrg
```

### プロファイル指定の注意

- `--profiles` で指定したプロファイルだけが `.odrg` に bake される。
  **[3] の経路探査では、ここで bake した名前のプロファイルしか使えない**
  （例: `car,pedestrian` だけ bake した `.odrg` で `VehicleProfile.Truck` は使えない）。
- 現状 `--profiles` が受け付けるのは組込み 4 種（`car` / `pedestrian` / `bicycle` / `truck`）のみ。
  未対応名を渡すとエラー終了する。
- ランタイムで bake 済みプロファイル名を確認するには `RouterDb.GetProfileNames()` を使う。

---

## 5. ルート探査の方法

`.odrg` をロードし、`Router` で 2 点間経路を計算する。

```csharp
using OsmDotRoute;

// 1. .odrg をロード（MMF + Span ゼロコピー。Itinero も PBF も不要）
var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");

// 2. Router を構築
var router = new Router(routerDb);

// 3. 起点・終点を指定して計算（GeoCoordinate は 緯度, 経度 の順）
var route = router.Calculate(
    VehicleProfile.Car,
    new GeoCoordinate(35.681, 139.767),   // 東京駅
    new GeoCoordinate(35.659, 139.700));  // 渋谷駅

// 4. 結果（経路未発見・範囲外座標のときは null）
if (route is null)
{
    Console.WriteLine("経路を計算できませんでした。");
    return;
}

Console.WriteLine($"距離 {route.TotalDistanceM:F0} m, 所要 {route.TotalDurationSec:F0} 秒");

// 経路形状（ゼロアロケーションの ReadOnlyMemory）
foreach (var p in route.Shape.Span)
{
    Console.WriteLine($"  {p.Latitude:F6}, {p.Longitude:F6}");
}
```

`Route` の主なメンバ:

| メンバ | 型 | 内容 |
| --- | --- | --- |
| `TotalDistanceM` | `double` | 総距離（メートル） |
| `TotalDurationSec` | `double` | 総所要時間（秒、プロファイル速度ベース） |
| `Shape` | `ReadOnlyMemory<GeoCoordinate>` | 経路形状の頂点列 |

補助 API:

```csharp
// 任意座標を最寄り道路にスナップ（範囲外は null）
GeoCoordinate? snapped = router.SnapToRoad(VehicleProfile.Car, new GeoCoordinate(35.68, 139.76));

// 頂点数・辺数・経緯度範囲
RouterDbStatistics stats = routerDb.GetStatistics();

// .odrg に bake 済みのプロファイル名
IReadOnlyList<string> names = routerDb.GetProfileNames();
```

### 動的制約を加える

`RestrictedAreaService` を `Router` に渡すと、登録した進入不可／難所エリアが
**次回の `Calculate` から即時反映**される（再ビルド不要）。これが OsmDotRoute の主目的。

```csharp
var restrictions = new RestrictedAreaService();
var router = new Router(routerDb, restrictions);

// ポリゴンで進入不可エリアを登録
var polygon = new GeoPolygon(new[]
{
    new GeoCoordinate(35.68, 139.76),
    new GeoCoordinate(35.68, 139.78),
    new GeoCoordinate(35.66, 139.78),
    new GeoCoordinate(35.66, 139.76),
});
restrictions.AddBlockArea(polygon, tag: "incident-1");

// メッシュコード（JIS X0410）で難所エリア（冠水）を登録
restrictions.AddDifficultyArea(new MeshCode(53394611), DifficultyTypes.Flooding, tag: "typhoon-15");

var route1 = router.Calculate(VehicleProfile.Car, from, to);  // 制約あり

// タグ単位で一括解除 → 次の計算から制約なし
restrictions.RemoveByTag("typhoon-15");
var route2 = router.Calculate(VehicleProfile.Car, from, to);
```

国土数値情報 KSJ（A31 洪水浸水想定区域など）の GML から一括登録もできる:

```csharp
var bounds = new MapBounds(
    new GeoCoordinate(35.65, 139.74),
    new GeoCoordinate(35.70, 139.79));
restrictions.AddDifficultyAreaFromGmlFile(
    @"D:\ハザードデータ\A31-12_24_GML\A31-12_24.xml",
    DifficultyTypes.Flooding,
    mapBounds: bounds,   // マップ範囲外のフィーチャは除外
    tag: "ksj-a31");
```

組込みの難所タイプ（`DifficultyTypes`）: `Flooding`（冠水）/ `Liquefaction`（液状化）/
`Landslide`（土砂崩れ）/ `Construction`（工事中）/ `Obstacle`（障害物）/
`Congestion`（交通集中）/ `Snow`（積雪）/ `Ice`（凍結）。

---

## 6. プロファイルの指定方法

プロファイルは「どの道路を通れるか」「速度」「難所への反応」を定義する。
JSON で外部化されており、リビルドなしに調整できる。

### 組込みプロファイル

```csharp
VehicleProfile.Car         // 自動車
VehicleProfile.Pedestrian  // 歩行者
VehicleProfile.Bicycle     // 自転車（平均 15km/h、cycleway/path 優先、motorway/trunk 不可）
VehicleProfile.Truck       // 10t トラック（日本道路法ベース。総重量 20t/全高 3.8m/全幅 2.5m）
```

> 経路探査で使えるのは、その `.odrg` に bake 済みのプロファイルだけ（§4 参照）。
> 例えば Truck を使うには抽出時に `--profiles ...,truck` を含める必要がある。

### ユーザー定義プロファイル（JSON）

独自の JSON からプロファイルを読み込める:

```csharp
VehicleProfile custom = VehicleProfile.LoadFromJsonFile(@"D:\profiles\delivery.json");
// あるいは
VehicleProfile custom2 = VehicleProfile.LoadFromJsonString(jsonText);
```

> **現状の制約**: ランタイムは任意プロファイルを読み込めるが、`.odrg` に bake できるのは
> 組込み 4 種のみ（抽出 CLI が組込み名固定）。そのため独自プロファイルで経路探査するには、
> その `Name` が `.odrg` に bake 済みのいずれかと一致している必要がある。
> 抽出ツールのユーザー定義プロファイル対応は Phase 4+ の TODO。

### プロファイル JSON スキーマ

`car.json` を例にした主なフィールド（命名は camelCase）:

```json
{
  "name": "car",
  "vehicleType": "motor_vehicle",
  "ignoreOneway": false,
  "speedMultiplier": 0.75,
  "accessTagKeys": ["access", "vehicle", "motor_vehicle"],
  "highway": {
    "motorway":    { "speedKmh": 120, "access": "yes" },
    "residential": { "speedKmh": 50,  "access": "yes" },
    "footway":     { "speedKmh": 5,   "access": "no" }
  },
  "accessValueMap": { "yes": "allow", "private": "deny" },
  "maxspeedTagKey": "maxspeed",
  "maxspeedUnitDefault": "kmh",
  "fallback": { "speedKmh": 10, "access": "no" },
  "speedBounds": { "minKmh": 30, "maxKmh": 200 },
  "difficulty": {
    "flooding":  { "speedFactor": 0.3, "canPass": true  },
    "landslide": { "speedFactor": 0.0, "canPass": false }
  },
  "difficultyDefault": { "speedFactor": 1.0, "canPass": true }
}
```

| フィールド | 内容 |
| --- | --- |
| `name` | プロファイル名（経路探査時の参照キー） |
| `ignoreOneway` | 一方通行を無視するか（歩行者で true） |
| `speedMultiplier` | 全速度に掛ける係数（実走平均 ≒ 法定速度 × 0.75 なら `0.75`） |
| `accessTagKeys` | 評価する access 系タグキー。配列の後ろほど優先 |
| `highway` | `highway=*` ごとの速度（`speedKmh`）と通行可否（`access`: `yes`/`no`） |
| `accessValueMap` | access タグ値 → `allow`/`deny` の対応 |
| `fallback` | highway 不明時の既定 |
| `speedBounds` | 速度の下限・上限クランプ |
| `difficulty` | 難所タイプごとの `speedFactor`（速度係数）と `canPass`（通行可否） |
| `difficultyDefault` | 未定義の難所タイプの既定値 |
| `vehicleLimits` | （Truck 用）`maxWeightTon` / `maxHeightMeter` / `maxWidthMeter` 超過エッジを通行不可化 |

---

## 7. 実装コード例

### 7.1 最小エンドツーエンド（コンソール）

```csharp
using OsmDotRoute;

var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");
var router = new Router(routerDb);

var route = router.Calculate(
    VehicleProfile.Car,
    new GeoCoordinate(35.681, 139.767),
    new GeoCoordinate(35.659, 139.700));

Console.WriteLine(route is null
    ? "経路なし"
    : $"距離 {route.TotalDistanceM:F0} m / 所要 {route.TotalDurationSec:F0} 秒 / 頂点 {route.Shape.Length}");
```

### 7.2 DI 統合（ASP.NET Core 等）

```csharp
using Microsoft.Extensions.DependencyInjection;
using OsmDotRoute.Extensions.DependencyInjection;

services.AddOsmDotRoute(@"D:\odrg\tokyo.odrg");
// Router / RouterDb / RestrictedAreaService が Singleton 登録される。
// RestrictedAreaService を共有することで、動的制約の変更が次回計算へ即時反映される。

var router = serviceProvider.GetRequiredService<Router>();
var restrictions = serviceProvider.GetRequiredService<RestrictedAreaService>();
```

### 7.3 動的制約つき Re-Route

```csharp
var restrictions = new RestrictedAreaService();
var router = new Router(routerDb, restrictions);

var from = new GeoCoordinate(35.681, 139.767);
var to   = new GeoCoordinate(35.659, 139.700);

var before = router.Calculate(VehicleProfile.Car, from, to);

// 浸水エリアをメッシュで登録 → 再計算すると回避経路になる
restrictions.AddDifficultyArea(new MeshCode(53394611), DifficultyTypes.Flooding, tag: "flood");
var after = router.Calculate(VehicleProfile.Car, from, to);

Console.WriteLine($"通常 {before?.TotalDistanceM:F0} m → 浸水回避 {after?.TotalDistanceM:F0} m");
```

---

## 関連ドキュメント

- [README](../README.md) — プロジェクト概要・クイックスタート
- [.odrg バイナリ形式仕様](phase2_graph_format_spec.md)
- [要件定義書](requirement_definition.md)
