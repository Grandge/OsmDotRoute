# Itinero 動的制約機能 PoC 実装計画

**作成日**: 2026-05-18
**目的**: RouterDB を再ビルドせずに「進入不可領域」「移動困難領域」をシミュレーション中に動的に設定可能にする
**方針**: 本プロジェクト内で PoC（ラッパー方式）→ フォーク改造が必要と判明した段階で別リポジトリへ切り出し

---

## 背景

- 災害廃棄物処理シミュレーションでは、災害発生後の道路寸断・冠水・通行止めをモデル化したい
- 現状の `MapService` は静的な RouterDB を読み込んで `_router.Calculate()` するのみで、動的な通行制約をかける手段がない
- RouterDB を作り直す案は時間がかかりすぎる（OSM PBF からのビルドに分単位）ため却下

## 現状確認結果

- **Itinero バージョン**: 1.5.1（`App/DisasterWasteSim.Server/DisasterWasteSim.Server.csproj`）
- **Itinero 使用箇所**: `MapService.cs` のみ（`CalculateRoute()` / `SnapToRoad()` / `GetRoadNetworkGeoJson()`）
- **Contraction Hierarchies**: **未使用**（`LoadOsmData` 後に `Contract()` を呼んでいない）→ 動的重み変更が反映可能
- **使用 Profile**: `Vehicle.Car` / `Vehicle.Pedestrian`

## ゴール

1. シミュレーション稼働中に「進入不可ポリゴン」「移動困難ポリゴン（速度低下係数付き）」を REST API で追加・削除可能にする
2. 既存の `CalculateRoute()` が制約を考慮した経路を返すようにする
3. 経路探索性能の許容範囲（仮: 1経路あたり 100ms 以内）を確認する
4. フォーク改造が不要な範囲で実現できるかを判定する

## 非ゴール（PoC 範囲外）

- フロントエンド（React 側）の UI 整備
- Contraction Hierarchies 対応
- 永続化（再起動後の制約復元）
- 既存シナリオへの組み込み・本番運用化

---

## 実装計画（5ステップ）

### Step 1: Itinero 拡張ポイントの調査（コード変更なし・調査のみ）

**目的**: `Vehicle.Car` の `Profile` クラスの拡張可能性を確認し、PoC のアプローチを最終決定する

**作業**:
- Itinero 1.5.1 のソース（[itinero/routing](https://github.com/itinero/routing)）を参照し、以下を確認:
  - `Itinero.Profiles.Profile` のコンストラクタ・継承可能性
  - `FactorAndSpeed` 計算のフック点（`FactorAndSpeed Get(IAttributeCollection attributes)` 系）
  - `Router.Calculate` がエッジ重みをどう取得しているか
  - エッジに紐付く座標情報の取得方法（エッジ ID → 座標列）
- ローカルにソースを `git clone` してリファレンス用に保持（改造はしない）

**成果物**: `Documents/Itinero動的制約_調査結果.md`（拡張ポイントとアプローチ選定）

**完了判定**: PoC のアプローチが (A) ラッパー Profile、(B) Profile 継承、(C) フォーク改造、のいずれになるか確定

---

### Step 2: 制約管理クラスの実装（独立ユニット）

**名前空間**: `DisasterWasteSim.Server.Routing.Experimental`

**作業**:
- `RestrictedAreaService.cs` 新規作成
  - 進入不可ポリゴン・移動困難ポリゴン（速度係数付き）を List で保持
  - `AddBlockArea(GeoPolygon)` / `AddSlowArea(GeoPolygon, float speedFactor)` / `ClearAll()` 等
  - Point-in-Polygon 判定（ray casting）
  - 性能のため AABB（外接矩形）による事前フィルタ
- `GeoPolygon.cs`: 緯度経度の頂点列を持つ単純な値オブジェクト
- DI 登録（`Program.cs`）

**完了判定**: ユニットテスト相当（コンソールアプリ or 一時的なテストエンドポイント）でポリゴン内外判定が正しく動くこと

---

### Step 3: カスタム Profile / WeightHandler の実装

**作業**: Step 1 の判定に基づき以下のいずれかを実装

**案 (A) ラッパー Profile**:
- 既存 `Vehicle.Car.Fastest()` 等の Profile をラップする `RestrictedProfile` を作成
- エッジ評価時に `RestrictedAreaService` に問い合わせ、ポリゴン内なら Factor を ∞ または ×（速度係数の逆数）にする
- エッジの代表座標は「両端の中点」または「シェイプの中点」で判定（性能と精度のトレードオフ）

**案 (B) Profile 継承**:
- `Profile` を継承して独自クラスを作成
- 案 (A) より深い拡張だがコードは類似

**実装場所**: `App/DisasterWasteSim.Server/Routing/Experimental/RestrictedProfile.cs`

**完了判定**: 任意の2点間でテスト経路計算を行い、ブロックエリアを設定すると経路が迂回されることを確認

---

### Step 4: MapService への統合

**作業**:
- `MapService.cs` に `CalculateRouteWithRestrictions(...)` メソッドを**追加**（既存 `CalculateRoute` は触らない）
- 新メソッドは `RestrictedProfile` を使う
- REST エンドポイント追加:
  - `POST /api/routing/restrictions/block` — ポリゴン追加
  - `POST /api/routing/restrictions/slow` — 速度低下エリア追加
  - `DELETE /api/routing/restrictions` — 全クリア
  - `GET /api/routing/test-route?fromLat=&fromLon=&toLat=&toLon=` — 制約適用済みの経路を返す（GeoJSON）

**完了判定**: ブラウザから REST 経由で制約を設定し、`/api/routing/test-route` で迂回経路が返ることを確認

---

### Step 5: 性能評価とアプローチ判定

**作業**:
- 制約 0個 / 10個 / 100個での経路計算時間を計測（1000回平均）
- メモリ使用量の変化を確認
- 性能が要件（仮: 100ms 以内）を満たすかを判定
- 満たさない場合の対策案を列挙（空間インデックス導入、エッジキャッシュ等）

**完了判定**: `Documents/Itinero動的制約_PoC評価レポート.md` を作成し、以下のいずれかの結論を出す:
- **(α) ラッパー方式で十分** → 本番機能化のための実装計画を別途立てる
- **(β) ラッパー方式は機能するが性能不足** → 最適化計画 or フォーク改造の検討へ
- **(γ) ラッパー方式では実現困難** → 別リポジトリで Itinero フォーク改造プロジェクトを開始

---

## ステップ間の確認ポイント

CLAUDE.md の方針に従い、**各ステップ完了時に必ずユーザーに報告し、次へ進む指示を待つ**。
特に Step 1 の調査結果はアプローチを左右するため、ユーザーと方針合意を取ってから Step 2 へ進む。

## リスクと対応

| リスク | 対応 |
|--------|------|
| Itinero 1.x のソースが古く、想定したフック点が存在しない | Step 1 で確認。なければ別アプローチ（フォーク前提）に切り替え |
| エッジ毎のポリゴン判定で性能が壊滅的に悪化 | Step 5 で計測。空間インデックス（R-tree 等）で対応 |
| `Router.Calculate` が内部で重みをキャッシュしていて動的変更が効かない | Step 1 のソース確認で判明させる |
| PoC コードが本体コードに混入し切り出し困難に | 名前空間を `Routing.Experimental` に統一して隔離 |

## 想定工数感（参考）

- Step 1: 半日〜1日（ソースリーディング）
- Step 2: 半日（独立した単純なクラス群）
- Step 3: 1〜2日（Itinero の内部理解次第）
- Step 4: 半日
- Step 5: 半日

合計: **3〜5日程度**（Step 1 の結果次第で大きく振れる）
