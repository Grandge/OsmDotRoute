# OsmDotRoute

.NET ネイティブの OSM 経路計算ライブラリ。動的通行制限（進入不可・移動困難）に対応した
Dijkstra ベースの経路探索を提供する。Itinero 1.x の後継として親プロジェクト
「災害廃棄物処理シミュレーション」から派生したスピンオフ。

## 特長

- **.NET 9 / pure C# 実装**、ホットパスは Itinero 由来の `RouterDb` グラフを利用
- **動的制約**: ポリゴン・JIS X0410 メッシュコード・国土数値情報 KSJ アプリケーションスキーマ GML（A31 等）入力
- **JSON 外部化されたプロファイル**: リビルドなしに通行可否・速度・難所反応を調整可能
- **8 種の組込み難所タイプ**: 冠水・液状化・土砂崩れ・工事中・障害物・交通集中・積雪・凍結
- **MIT License**

## 現在のフェーズ

**Phase 1 進行中**（ステップ 12 / 17）。

| Phase | 目標 | 状態 |
| --- | --- | --- |
| Phase 0 | 要件定義 | 完了（2026-05-18） |
| Phase 1 | 経路探索エンジン独自化（Itinero をデータ層として残す） | **進行中** |
| Phase 2 | 中間グラフフォーマット策定、ランタイム Itinero 依存削除 | 未着手 |
| Phase 3 | OSM PBF パーサー独自化、GitHub 個人アカウントで OSS 公開 | 未着手 |

## インストール

Phase 1 期間中は **ソース参照** での利用を想定しています（NuGet 公開は Phase 3 以降、REQ-PKG-001/004）。
親プロジェクトから次のように参照してください。

```xml
<ProjectReference Include="path/to/OsmDotRoute/src/OsmDotRoute/OsmDotRoute.csproj" />
<ProjectReference Include="path/to/OsmDotRoute/src/OsmDotRoute.Itinero/OsmDotRoute.Itinero.csproj" />
```

DI 統合を使う場合は加えて:

```xml
<ProjectReference Include="path/to/OsmDotRoute/src/OsmDotRoute.Extensions.DependencyInjection/OsmDotRoute.Extensions.DependencyInjection.csproj" />
```

## 最小利用サンプル

```csharp
using OsmDotRoute;
using OsmDotRoute.Itinero;

// 1. Itinero RouterDb から OsmDotRoute の RouterDb を構築
var routerDb = ItineroRouterDbLoader.LoadFromFile("default.routerdb");
var router = new Router(routerDb);

// 2. 東京駅 → 渋谷駅 を Car プロファイルで計算
var route = router.Calculate(
    VehicleProfile.Car,
    new GeoCoordinate(35.681, 139.767),
    new GeoCoordinate(35.658, 139.745));

if (route is null)
{
    Console.WriteLine("経路を計算できませんでした。");
    return;
}

Console.WriteLine($"距離 {route.TotalDistanceM:F0} m, 所要時間 {route.TotalDurationSec:F0} s");
```

## 動的制約の登録例

```csharp
var restrictions = new RestrictedAreaService();
var router = new Router(routerDb, restrictions);

// ポリゴンで進入不可エリア登録
var polygon = new GeoPolygon(new[]
{
    new GeoCoordinate(35.68, 139.76),
    new GeoCoordinate(35.68, 139.78),
    new GeoCoordinate(35.66, 139.78),
    new GeoCoordinate(35.66, 139.76),
});
var blockId = restrictions.AddBlockArea(polygon, tag: "incident-2026-05-19");

// メッシュコードで難所エリア登録（冠水）
restrictions.AddDifficultyArea(
    new MeshCode(53394611),
    DifficultyTypes.Flooding,
    tag: "typhoon-15");

// 国土数値情報 A31（洪水浸水想定区域）GML を一括登録（マップ範囲フィルタつき）
var bounds = new MapBounds(
    new GeoCoordinate(35.65, 139.74),
    new GeoCoordinate(35.70, 139.79));
restrictions.AddDifficultyAreaFromGmlFile(
    @"D:\ハザードデータ\A31-12_24_GML\A31-12_24.xml",
    DifficultyTypes.Flooding,
    mapBounds: bounds,
    tag: "ksj-a31");

// 一括削除
restrictions.RemoveByTag("typhoon-15");
```

## DI 統合

```csharp
using Microsoft.Extensions.DependencyInjection;
using OsmDotRoute.Extensions.DependencyInjection;

services.AddOsmDotRoute("default.routerdb");

// 取得側
var router = serviceProvider.GetRequiredService<Router>();
var restrictions = serviceProvider.GetRequiredService<RestrictedAreaService>();
```

`Router` / `RouterDb` / `RestrictedAreaService` がいずれも Singleton で登録されます。
`RestrictedAreaService` を共有することで、シミュレーション中に動的制約を変更すると
次の `Router.Calculate` 呼び出しから即時反映されます（REQ-RST-012）。

## バージョニング方針

0.x 期間中は **マイナー版アップでの破壊的 API 変更を許容** します（REQ-API-008）。
Phase 1 → Phase 2 移行時に独自グラフフォーマットと組み合わせて公開 API を整理する予定です。
セマンティックバージョニング厳密適用は 1.0 リリース以降（Phase 3 完了後）から。

## 親プロジェクトとの関係

最初の顧客は親プロジェクト「災害廃棄物処理シミュレーション」です。
ただし**汎用 OSM ルーティングライブラリ**として設計しており、災害ユースケースはその応用と位置付けています。
親プロジェクトのコード・データ・ドキュメントを本リポジトリに移動・コピーすることはしません
（依存方向は親 → 本ライブラリで一方向）。

## ライセンス

[MIT License](LICENSE) — Copyright (c) 2026 Grandge.

## ドキュメント

- [要件定義書](Documents/requirement_definition.md)
- [Phase 1 設計書](Documents/phase1_design.md)
- [Phase 1 実装計画書](Documents/phase1_implementation_plan.md)
