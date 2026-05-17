# OsmDotRoute 要件定義書

**バージョン**: 1.1（確定）
**作成日**: 2026-05-18
**最終更新**: 2026-05-18
**ステータス**: 確定（Phase 0 完了）

---

## 1. 概要

`.NET ネイティブの OSM 経路計算ライブラリ` を開発する。最大の差別化要素は **シミュレーション中に動的に変更可能な通行制限**（進入不可エリア・移動困難エリア）。

親プロジェクト `災害廃棄物処理シミュレーション` で利用中の Itinero 1.5.1 が動的制約に対応していない・メンテナンス停止状態であることを背景に、独自ライブラリを段階的に開発する。

汎用 OSM ルーティングライブラリとして設計し、災害ユースケースはその応用として位置付ける。

---

## 2. 要件管理項目および記法ルール

本ドキュメントでは、各要件の優先度、実装フェーズ、進捗状況、実装バージョンを以下の記法で管理する。

### 2.1. 優先度 (Priority)

- **[P1]: 高 (Critical)** — Phase 1 で必須となるコア機能、公開 API の基盤。最優先で実装する
- **[P2]: 中 (Important)** — 性能・利便性・実用性に資する機能、Phase 2/3 のマイルストーン機能
- **[P3]: 低 (Optional)** — Nice-to-have、利用者要望が出た時点で着手判断
- **[P4:TBD]** — 優先度未確定、別途検討

### 2.2. 実装フェーズ (Phase)

- **[Phase1]** — 経路探索エンジン独自化（Itinero をデータ層として残す）
- **[Phase2]** — 中間グラフフォーマット策定、ランタイム Itinero 依存削除
- **[Phase3]** — OSM PBF パーサー独自化、Itinero への完全独立
- **[Phase4+]** — 将来検討（NuGet 公開、CH 対応、マルチプラットフォーム等）

### 2.3. 進捗管理 (Progress)

- **[ ]**: 未着手または実装中
- **[x]**: 実装および動作確認が完了し、最終検証待ちまたは完了

### 2.4. 実装バージョン (Implementation Version)

- **(Ver. -)**: 未実装
- **(Ver. x.x.x)**: 該当機能が初搭載されたバージョン番号を記述。バージョン番号はユーザーが管理する

### 2.5. 要件 ID 命名規則

`REQ-{ジャンル}-{連番3桁}` 形式。ジャンル一覧:

| ジャンル | 説明 |
|---|---|
| REQ-RTE | 経路探索コア機能 (Routing) |
| REQ-RST | 動的制約管理 (Restriction) |
| REQ-PRF | 車両プロファイル (Profile) |
| REQ-MAP | 地図データ・グラフ (Map) |
| REQ-API | パブリック API 設計 (API) |
| REQ-FMT | データフォーマット (Format) |
| REQ-NFR | 非機能要件 (Non-Functional Requirement) |
| REQ-PKG | 配布・公開戦略 (Package) |
| REQ-LIC | ライセンス (License) |
| REQ-DEP | 依存ライブラリ (Dependency) |

---

## 3. スコープ

### 3.1 やること

- OSM データに基づく 2 点間経路計算（Dijkstra ベース）
- 動的な進入不可・移動困難エリアのランタイム設定／変更
- 道路ネットワーク上への座標スナップ
- 道路ネットワークの GeoJSON 出力
- 複数車両プロファイル対応（段階的）

### 3.2 やらないこと

- リアルタイム交通情報（渋滞・所要時間予測）統合
- マルチモーダル経路計算（公共交通機関連携）
- ターンバイターン音声ナビゲーション
- 大規模分散経路計算（クラスタリング）
- 全世界対応（日本に限定）
- フロントエンド UI（ライブラリのため）

---

## 4. ターゲット利用シーン

### 4.1 第一の顧客（最優先）

**災害廃棄物処理シミュレーション**
- 災害発生後の道路寸断・冠水・通行止めを動的に反映した収集車・住民エージェント経路計算
- 都道府県単位の OSM データ規模
- 100ms 以内のレスポンスを 1 シミュレーションティック中に多数回呼び出す

### 4.2 想定される他のユースケース（汎用化）

- **工事・イベント時の通行制限シミュレーション**: 道路工事・大型イベントによる通行止めを反映した配送計画
- **物流・配送ルート最適化**: 時間帯による通行制限を考慮したラストワンマイル配送
- **観光・歩行者ルート提案**: 季節・天候による通行制限（積雪期通行止め等）を反映
- **ゲーム・シミュレーション AI 用**: NPC や AI エージェントの動的経路探索

---

## 5. 機能要件

### 5.1 経路探索コア (REQ-RTE)

- [ ] [P1] [Phase1] **REQ-RTE-001**: 2点間（緯度経度）の最短経路を Dijkstra ベースで計算し、独自 `Route` 型で返すこと。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RTE-002**: 任意の緯度経度座標を最寄り道路上にスナップし、スナップ点の緯度経度を返すこと。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RTE-003**: スナップに使用する検索半径（メートル）を呼び出し側で指定可能とすること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RTE-004**: 道路ネットワーク全体を GeoJSON FeatureCollection（LineString 列）として出力できること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RTE-005**: 経路計算 API は同期版を基本提供すること。非同期 API は要望が出るまで提供しない。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RTE-006**: 経路が見つからなかった場合は `null` を返し、例外を投げないこと。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RTE-007**: 経路計算結果に総距離（メートル）・総所要時間（秒）・経路形状（緯度経度列）を含めること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RTE-008**: 道路ネットワーク外の座標を起点／終点に指定した場合、`null` を返し例外を投げないこと。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-RTE-009**: 双方向 Dijkstra 等の高速化アルゴリズムを導入し、性能要件未達時の対策とすること。(Ver. -)

### 5.2 動的制約管理 (REQ-RST)

#### 5.2.a 進入不可エリア

- [ ] [P1] [Phase1] **REQ-RST-001**: 緯度経度ポリゴンによる進入不可エリアを登録できること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-002**: 地域メッシュコード（JIS X0410 第3次メッシュおよびその細分メッシュ、後述の REQ-RST-016 で対応階層を規定）による進入不可エリアを登録できること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-003**: 複数の地域メッシュコードを一括で進入不可エリアとして登録できること（異なる階層の混在を許容）。(Ver. -)

#### 5.2.b 移動困難エリア

- [ ] [P1] [Phase1] **REQ-RST-004**: 緯度経度ポリゴン + 速度低下係数による移動困難エリアを登録できること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-005**: 地域メッシュコード（REQ-RST-016 で規定する階層） + 速度低下係数による移動困難エリアを登録できること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-006**: 複数の地域メッシュコードを一括で移動困難エリアとして登録できること（異なる階層の混在を許容）。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-007**: 速度低下係数の有効範囲を `0.0`〜`1.0` とし、範囲外の値は引数例外で拒否すること（0.0=通行不可、1.0=通常速度）。(Ver. -)

#### 5.2.c 制約の削除・管理

- [ ] [P1] [Phase1] **REQ-RST-008**: 登録された制約を一意の ID で個別削除できること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-009**: 全制約を一括クリアできること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-010**: 制約登録時に任意のタグ文字列を付与でき、タグ単位で一括削除できること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-011**: 登録済み制約の一覧を読み取り専用ビューで取得できること。(Ver. -)

#### 5.2.d 反映タイミング・判定ロジック

- [ ] [P1] [Phase1] **REQ-RST-012**: 制約の追加・削除・クリアは、次回の経路計算呼び出しから即時反映されること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-013**: 空間制約の判定は、エッジのシェイプ列を用いた交差判定で行うこと。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-014**: 空間制約判定の事前フィルタとして外接矩形（AABB）による枝刈りを行うこと。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-015**: メッシュコード指定の場合、メッシュ矩形を AABB として直接使用すること（多角形ポリゴン交差判定をスキップ）。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-016**: メッシュコードの対応階層を以下の 4 階層とすること。(Ver. -)
  - **第3次メッシュ** (約 1km 四方、8 桁、例 `53394547`)
  - **1/2 細分メッシュ** (約 500m 四方、9 桁、例 `533945471`)
  - **1/4 細分メッシュ** (約 250m 四方、10 桁、例 `5339454713`)
  - **1/10 細分メッシュ** (約 100m 四方、11 桁、例 `53394547135`)
- [ ] [P1] [Phase1] **REQ-RST-017**: 入力されたメッシュコードの桁数から階層を自動判定し、対応する経緯度矩形領域に変換できること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-018**: 桁数が REQ-RST-016 の規定に該当しないメッシュコードは、引数例外で拒否すること。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-RST-019**: 第1次メッシュ（80km）・第2次メッシュ（10km）への対応拡張は要望が出た時点で個別判断する。(Ver. -)

#### 5.2.e GeoJSON 入力対応

- [ ] [P1] [Phase1] **REQ-RST-020**: GeoJSON `Polygon` Geometry オブジェクトを入力として進入不可エリア／移動困難エリアを登録できること（RFC 7946 準拠）。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-021**: GeoJSON `MultiPolygon` Geometry オブジェクトに対応すること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-022**: GeoJSON `Polygon` の Hole（2番目以降の内側境界配列）に対応し、外側境界内かつ Hole 外の領域のみを制約対象とすること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-RST-023**: GeoJSON `FeatureCollection` から複数の制約を一括登録できること。各 Feature ごとに登録 ID を返すこと。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-024**: GeoJSON ファイル（`.geojson` / `.json`）を直接読み込んで制約を一括登録できること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-025**: GeoJSON 文字列（`string`）からの制約一括登録 API を提供すること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-026**: GeoJSON Feature の `properties` から速度低下係数を読み取れること（規定キー `speedFactor`、`double` 値、範囲 0.0〜1.0）。`speedFactor` キーが存在しない／`null` の場合は進入不可エリアとして扱うこと。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-027**: GeoJSON Feature の `properties` の規定キー `tag` からタグ文字列を読み取れること（REQ-RST-010 のタグ機構との連携）。(Ver. -)
- [ ] [P2] [Phase1] **REQ-RST-028**: GeoJSON の座標系を WGS84（経度、緯度の順）として扱うこと（RFC 7946 準拠）。他の座標系は本ライブラリでは扱わず、利用者側で事前変換すること。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-RST-029**: TopoJSON 等の他形式対応は要望が出た時点で個別判断する。(Ver. -)

### 5.3 車両プロファイル (REQ-PRF)

- [ ] [P1] [Phase1] **REQ-PRF-001**: 車両プロファイル `Car` に対応すること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-PRF-002**: 車両プロファイル `Pedestrian` に対応すること。(Ver. -)
- [ ] [P2] [Phase2] **REQ-PRF-003**: 車両プロファイル `Bicycle` に対応すること。(Ver. -)
- [ ] [P2] [Phase2] **REQ-PRF-004**: 車両プロファイル `Truck` に対応すること。(Ver. -)
- [ ] [P3] [Phase3] **REQ-PRF-005**: 緊急車両プロファイル（救急車・消防車相当）に対応すること。(Ver. -)
- [ ] [P3] [Phase3] **REQ-PRF-006**: 災害用車両プロファイル（災害時の特殊許可ルート・通行制限緩和を考慮）に対応すること。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-PRF-007**: プロファイルをユーザーがコードで拡張可能な API を提供すること。(Ver. -)

### 5.4 地図データ・グラフ (REQ-MAP)

- [ ] [P1] [Phase1] **REQ-MAP-001**: Itinero RouterDb（`.routerdb`）ファイルを読み込めること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-MAP-002**: 読み込み済みグラフから頂点数・辺数・経緯度範囲の統計情報を取得できること。(Ver. -)
- [ ] [P1] [Phase2] **REQ-MAP-003**: 独自バイナリグラフ形式を策定すること（仕様は別文書 `phase2_graph_format_spec.md` に記載）。(Ver. -)
- [ ] [P1] [Phase2] **REQ-MAP-004**: Itinero RouterDb → 独自バイナリグラフへの一括変換ツール（CLI 等）を提供すること。(Ver. -)
- [ ] [P1] [Phase2] **REQ-MAP-005**: 独自バイナリグラフ形式のファイルを読み込めること。(Ver. -)
- [ ] [P1] [Phase2] **REQ-MAP-006**: ランタイム経路計算から Itinero アセンブリへの依存を排除すること。(Ver. -)
- [ ] [P1] [Phase3] **REQ-MAP-007**: OSM PBF ファイルを直接読み込む独自パーサーを提供すること。(Ver. -)
- [ ] [P1] [Phase3] **REQ-MAP-008**: OSM PBF から独自バイナリグラフを直接ビルドできること。(Ver. -)
- [ ] [P1] [Phase3] **REQ-MAP-009**: ライブラリ全体（変換ツールを含む）から Itinero への一切の依存を排除すること。(Ver. -)

### 5.5 パブリック API 設計 (REQ-API)

- [ ] [P1] [Phase1] **REQ-API-001**: エントリーポイントを `OsmDotRoute.Router` クラスとするファサードパターンを採用すること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-API-002**: 出力型は OsmDotRoute 独自の `Route` 型とし、`Itinero.Route` 型を公開 API に露出させないこと。(Ver. -)
- [ ] [P1] [Phase1] **REQ-API-003**: 親プロジェクト側から `using Itinero;` を完全に消去できる API 設計とすること（Itinero 名前空間を内部実装に隠蔽）。(Ver. -)
- [ ] [P1] [Phase1] **REQ-API-004**: 動的制約の管理を `OsmDotRoute.RestrictedAreaService` クラスで提供すること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-API-005**: `Microsoft.Extensions.DependencyInjection` 互換の DI 登録拡張メソッド（`AddOsmDotRoute()` 等）を提供すること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-API-006**: 公開型は XML ドキュメンテーションコメント（`<summary>` 等）を完備すること。(Ver. -)
- [ ] [P2] [Phase4+] **REQ-API-007**: 1.0 リリース以降は SemVer に準拠したバージョニングを行うこと。(Ver. -)
- [ ] [P3] [Phase1] **REQ-API-008**: 0.x 期間中の破壊的 API 変更はマイナー版アップで許容する旨を README に明記すること。(Ver. -)

### 5.6 データフォーマット (REQ-FMT)

#### 5.6.a 経路出力型

- [ ] [P1] [Phase1] **REQ-FMT-001**: 経路出力型 `Route` に総距離（メートル単位、`double`）を含めること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-FMT-002**: 経路出力型 `Route` に総所要時間（秒単位、`double`）を含めること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-FMT-003**: 経路出力型 `Route` に経路形状（`IReadOnlyList<GeoCoordinate>`）を含めること。(Ver. -)

#### 5.6.b 形式変換ユーティリティ

- [ ] [P2] [Phase1] **REQ-FMT-004**: 経路を GeoJSON LineString に変換するユーティリティを提供すること。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-FMT-005**: 経路を Encoded Polyline 形式に変換するユーティリティを提供すること（要望次第）。(Ver. -)

---

## 6. 非機能要件

### 6.1 性能・スケーラビリティ (REQ-NFR — 性能)

- [ ] [P1] [Phase1] **REQ-NFR-001**: 都道府県単位（数百万エッジ）のグラフで 1 経路計算あたり 100ms 以内を目標とすること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-NFR-002**: 制約 100 件登録時にも REQ-NFR-001 の性能目標を維持すること。(Ver. -)
- [ ] [P2] [Phase1] **REQ-NFR-003**: 都道府県単位 RouterDb 読み込み時に、システム搭載 RAM 16GB で動作可能とすること。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-NFR-004**: Contraction Hierarchies（CH）対応により大規模グラフでの高速化を実現すること。(Ver. -)

### 6.2 対応プラットフォーム (REQ-NFR — プラットフォーム)

- [ ] [P1] [Phase1] **REQ-NFR-005**: .NET 9 上で動作すること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-NFR-006**: Windows 10/11 (x64) で動作すること。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-NFR-007**: Linux / macOS 対応は要望が出た時点で個別判断する（当面非対応）。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-NFR-008**: .NET 8 LTS 等の旧バージョン互換対応は要望が出た時点で個別判断する。(Ver. -)

### 6.3 対応地域・単位系 (REQ-NFR — 地域)

- [ ] [P1] [Phase1] **REQ-NFR-009**: 対応地域は日本国内のみを前提とすること（OSM タグ解釈・座標範囲を日本領域に最適化してよい）。(Ver. -)
- [ ] [P1] [Phase1] **REQ-NFR-010**: 単位系はメートル法（メートル/秒/m/s）のみ対応とすること。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-NFR-011**: グローバル対応（OSM 全地域）は要望が出た時点で個別判断する。(Ver. -)

### 6.4 配布・公開戦略 (REQ-PKG)

- [ ] [P1] [Phase1] **REQ-PKG-001**: Phase 1 では本プロジェクトをソースとして親プロジェクトから `<ProjectReference>` で参照可能とすること。(Ver. -)
- [ ] [P2] [Phase2] **REQ-PKG-002**: Phase 2 までは非公開リポジトリで管理し、外部公開しないこと。(Ver. -)
- [ ] [P1] [Phase3] **REQ-PKG-003**: Phase 3 完了時点で GitHub 個人アカウント上で OSS 公開できる状態とすること（README/LICENSE/CI 整備済み）。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-PKG-004**: NuGet.org への公開可否は Phase 3 完了後に別途判断する（当面公開しない）。(Ver. -)

### 6.5 ライセンス (REQ-LIC)

- [ ] [P1] [Phase1] **REQ-LIC-001**: ライブラリ本体のライセンスを MIT License とすること。(Ver. -)
- [ ] [P1] [Phase1] **REQ-LIC-002**: Itinero（Apache 2.0）のソースコードを本プロジェクトにコピー・改変して取り込まないこと。(Ver. -)
- [ ] [P1] [Phase1] **REQ-LIC-003**: Itinero への依存は NuGet 経由のバイナリ参照のみで行うこと。(Ver. -)
- [ ] [P2] [Phase3] **REQ-LIC-004**: OSM データ（ODbL）の利用ガイドラインを README で利用者に案内すること（本ライブラリ自体は OSM データを内包しない）。(Ver. -)

### 6.6 依存ライブラリ方針 (REQ-DEP)

- [ ] [P1] [Phase1] **REQ-DEP-001**: Phase 1 ではランタイムが Itinero 1.5.1 系および System.* 標準ライブラリのみに依存すること。(Ver. -)
- [ ] [P1] [Phase2] **REQ-DEP-002**: Phase 2 ではランタイムから Itinero 依存を排除し、System.* 標準ライブラリのみに依存すること（変換ツール内部の Itinero 利用は許容）。(Ver. -)
- [ ] [P1] [Phase3] **REQ-DEP-003**: Phase 3 では変換ツールを含む全コンポーネントから Itinero 依存を排除すること（OSM PBF パース用に protobuf 関連を限定追加することを許容）。(Ver. -)

---

## 7. インターフェース設計指針

### 7.1 想定 API シグネチャ（Phase 1 ドラフト）

```csharp
namespace OsmDotRoute
{
    public sealed class Router
    {
        public Router(RouterDb routerDb, RestrictedAreaService? restrictions = null);

        public Route? Calculate(VehicleProfile profile, GeoCoordinate from, GeoCoordinate to);
        public GeoCoordinate? SnapToRoad(VehicleProfile profile, GeoCoordinate point, float searchDistanceM = 500f);
        public RoadNetworkGeoJson GetRoadNetworkGeoJson();
    }

    public sealed class RouterDb
    {
        public static RouterDb LoadFromFile(string filePath);
    }

    public sealed class RestrictedAreaService
    {
        // ポリゴン指定
        public RestrictedAreaId AddBlockArea(GeoPolygon polygon, string? tag = null);
        public RestrictedAreaId AddSlowArea(GeoPolygon polygon, float speedFactor, string? tag = null);

        // 地域メッシュコード指定（JIS X0410 第3次〜1/10 細分、1km〜100m、8〜11 桁）
        public RestrictedAreaId AddBlockArea(MeshCode meshCode, string? tag = null);
        public RestrictedAreaId AddBlockArea(IEnumerable<MeshCode> meshCodes, string? tag = null);
        public RestrictedAreaId AddSlowArea(MeshCode meshCode, float speedFactor, string? tag = null);
        public RestrictedAreaId AddSlowArea(IEnumerable<MeshCode> meshCodes, float speedFactor, string? tag = null);

        // GeoJSON 入力（Polygon / MultiPolygon / FeatureCollection、RFC 7946 準拠）
        // speedFactor は properties.speedFactor から読み取り、無ければ進入不可エリアとして扱う
        public RestrictedAreaId[] AddFromGeoJson(string geoJson, string? defaultTag = null);
        public RestrictedAreaId[] AddFromGeoJsonFile(string filePath, string? defaultTag = null);
        public RestrictedAreaId[] AddFromGeoJsonStream(Stream stream, string? defaultTag = null);

        public void Remove(RestrictedAreaId id);
        public void RemoveByTag(string tag);
        public void ClearAll();
        public IReadOnlyList<RestrictedArea> ListAll();
    }

    public enum VehicleProfile { Car, Pedestrian /* Phase 2: Bicycle, Truck / Phase 3: Emergency, Disaster */ }
    public readonly record struct GeoCoordinate(double Latitude, double Longitude);
    public sealed class GeoPolygon { /* 緯度経度頂点列 */ }
    public readonly record struct MeshCode(long Value) { /* 8〜11 桁の数値。桁数で階層を自動判定 */ }
    public enum MeshLevel { Mesh3rd /* 1km */, HalfMesh /* 500m */, QuarterMesh /* 250m */, TenthMesh /* 100m */ }
    public sealed class Route { /* TotalDistanceM, TotalDurationSec, Shape: IReadOnlyList<GeoCoordinate> */ }
}
```

### 7.2 関連要件

- REQ-API-001〜REQ-API-008
- REQ-FMT-001〜REQ-FMT-005
- REQ-RTE-001〜REQ-RTE-008
- REQ-RST-001〜REQ-RST-015

---

## 8. データフォーマット詳細

### 8.1 グラフ入力フォーマット

| Phase | 入力データ | 関連要件 |
|---|---|---|
| Phase 1 | Itinero RouterDb (`.routerdb`) | REQ-MAP-001 |
| Phase 2 | OsmDotRoute 独自バイナリグラフ | REQ-MAP-003 〜 REQ-MAP-005 |
| Phase 3 | OSM PBF（独自パーサー） | REQ-MAP-007 〜 REQ-MAP-008 |

### 8.1.b 動的制約入力フォーマット

| 入力形式 | 内容 | 関連要件 |
|---|---|---|
| `GeoPolygon` メモリオブジェクト | 緯度経度頂点列 | REQ-RST-001, REQ-RST-004 |
| `MeshCode` メモリオブジェクト | JIS X0410 第3次〜1/10 細分（1km〜100m） | REQ-RST-002〜006, REQ-RST-016〜018 |
| GeoJSON 文字列 / ファイル / Stream | RFC 7946 準拠の Polygon / MultiPolygon / FeatureCollection | REQ-RST-020〜028 |

#### GeoJSON Properties 規定キー

| キー | 型 | 用途 | 関連要件 |
|---|---|---|---|
| `speedFactor` | `double`（0.0〜1.0） | 移動困難エリアの速度低下係数。省略時は進入不可エリアとして扱う | REQ-RST-026 |
| `tag` | `string` | 制約タグ（タグ単位での一括削除に使用） | REQ-RST-027 |

### 8.2 出力フォーマット

| 種別 | 内容 | 関連要件 |
|---|---|---|
| `OsmDotRoute.Route` 型 | 総距離・総所要時間・経路形状 | REQ-FMT-001 〜 REQ-FMT-003 |
| GeoJSON LineString | 経路の地図表示用 | REQ-FMT-004 |
| GeoJSON FeatureCollection | 道路ネットワーク全体 | REQ-RTE-004 |

---

## 9. 段階的開発計画

### Phase 0: 要件定義（現在）

- 本要件定義書の確定
- 大まかな API デザイン方針の確定（本書 7.1）
- ライセンス・公開戦略の確定
- `git init`（Phase 1 着手前）

### Phase 1: 経路探索エンジン独自化

**目標**: 親プロジェクトから `using Itinero` を完全に消せる状態にする。動的制約対応の Dijkstra 経路計算を提供。

**スコープ**: REQ-RTE-001〜008, REQ-RST-001〜015, REQ-PRF-001〜002, REQ-MAP-001〜002, REQ-API-001〜006, REQ-API-008, REQ-FMT-001〜004, REQ-NFR-001〜003, REQ-NFR-005〜006, REQ-NFR-009〜010, REQ-PKG-001, REQ-LIC-001〜003, REQ-DEP-001

**完了判定**:
- 親プロジェクトの `MapService.cs` から `using Itinero` を完全に消去できる
- 既存の `CalculateRoute` / `SnapToRoad` / `GetRoadNetworkGeoJson` 相当機能が動作
- 動的制約の追加削除が次回経路計算で反映される（REQ-RST-012）
- ベンチマーク結果が REQ-NFR-001 を満たす

**公開アクション**: Phase 1 完了後、親プロジェクトに `<ProjectReference>` で組み込み

### Phase 2: 中間グラフフォーマット

**目標**: ランタイムから Itinero 依存を削除。OsmDotRoute 単体で配布可能な状態に。

**スコープ**: REQ-PRF-003〜004, REQ-MAP-003〜006, REQ-PKG-002, REQ-DEP-002

**完了判定**:
- ランタイムから Itinero アセンブリ参照を削除できる（REQ-MAP-006, REQ-DEP-002）
- 独自フォーマットでも REQ-NFR-001 の性能要件を満たす

### Phase 3: OSM PBF パーサー独自化

**目標**: Itinero への完全独立。GitHub 公開可能な状態に。

**スコープ**: REQ-PRF-005〜006, REQ-MAP-007〜009, REQ-PKG-003, REQ-LIC-004, REQ-DEP-003

**完了判定**:
- ライブラリ全体から Itinero 依存が無い（REQ-MAP-009, REQ-DEP-003）
- GitHub 公開準備完了（LICENSE、README、CI 整備）

**公開アクション**: GitHub 個人アカウントで OSS 公開

### Phase 4 以降（将来検討）

- REQ-NFR-004 (CH 対応), REQ-NFR-007〜008 (Linux/macOS、旧 .NET), REQ-NFR-011 (グローバル対応)
- REQ-RTE-009 (高速化アルゴリズム)
- REQ-RST-016 (メッシュ階層拡張)
- REQ-PRF-007 (プロファイル拡張 API)
- REQ-API-007 (SemVer)
- REQ-FMT-005 (Polyline)
- REQ-PKG-004 (NuGet 公開)

---

## 10. 制約とリスク

### 10.1 技術的リスク

| リスク | 影響 | 対応策 | 関連要件 |
|---|---|---|---|
| **性能 100ms 達成困難**（都道府県単位、CH 未使用） | 親プロジェクトのリアルタイム性に影響 | 空間インデックス（R-tree）導入、エッジキャッシュ、双方向探索などで段階的に最適化。最悪 Phase 4 で CH 対応 | REQ-NFR-001, REQ-RTE-009, REQ-NFR-004 |
| **OSM PBF パーサー実装の不確実性**（Phase 3） | Phase 3 工数が大幅に膨らむ可能性 | Phase 1/2 完了後、市場・技術環境を見て着手判断（CLAUDE.md 記載済み）。protobuf-net 等の既存ライブラリを最大限活用 | REQ-MAP-007 |
| **Itinero 1.x 公開 API の挙動仕様が不明瞭** | Phase 1 で予期せぬ仕様差が発覚 | Itinero ソース参照 `d:/workspace/Itinero_source_reference/` で都度確認 | REQ-MAP-001, REQ-DEP-001 |
| **動的制約による経路計算性能の劣化** | REQ-RST-012 と REQ-NFR-001/002 のトレードオフ | エッジ単位の AABB 事前フィルタ、空間インデックスで局所判定 | REQ-RST-013〜015 |

### 10.2 ライセンス・知的財産リスク

| リスク | 影響 | 対応策 | 関連要件 |
|---|---|---|---|
| **Itinero ソースの混入による Apache 2.0 違反** | MIT 公開不可、再ライセンスが必要 | コピペ完全禁止。実装は仕様書・参考資料を読んで自力で書く | REQ-LIC-001〜003 |
| **OSM データのライセンス（ODbL）違反** | データ提供の制約 | 本ライブラリ自体は OSM データを内包せず、利用者が用意 | REQ-LIC-004 |

### 10.3 スケジュールリスク

| リスク | 影響 | 対応策 |
|---|---|---|
| **ユーザー単独開発のため進行遅延** | Phase 完了時期が読みにくい | 各 Phase 開始時に詳細な実装計画書を作成し、見積もりを更新 |
| **親プロジェクトのスケジュールとの衝突** | Phase 1 のリリース時期に制約 | Phase 1 着手前に親プロジェクト側の希望時期を確認 |

---

## 11. 用語集

| 用語 | 説明 |
|---|---|
| **OSM** | OpenStreetMap。世界中のボランティアが編集する地理データプロジェクト |
| **OSM PBF** | OSM の地理データをエンコードしたバイナリ形式（Protocol Buffer ベース） |
| **Itinero** | .NET ベースの OSS 経路計算ライブラリ。1.x はメンテナンス停止状態 |
| **RouterDb** | Itinero がビルドしたグラフ表現のメモリ・ファイルフォーマット |
| **Dijkstra（Dykstra）** | グラフ最短経路探索アルゴリズム。Itinero では `Dykstra` という綴り |
| **Profile** | 車両種別ごとの通行可否・速度を OSM タグから決定する設定 |
| **FactorAndSpeed** | Itinero の `Profile` が返すエッジ毎の重み係数と速度のペア |
| **Edge / Vertex** | グラフの辺と頂点。OSM では辺=道路セグメント、頂点=交差点 |
| **Shape** | エッジの中間座標列。曲がった道路を表現するための補助点 |
| **RouterPoint** | 任意座標を道路ネットワーク上にスナップした結果点 |
| **ポリゴン** | 緯度経度頂点列で定義される多角形（GeoJSON Polygon 相当） |
| **進入不可エリア** | 経路探索でエッジが通過不可と扱われるポリゴン領域 |
| **移動困難エリア** | 速度低下係数（0.0〜1.0）が適用されるポリゴン領域 |
| **動的制約** | ランタイム中に追加・削除・変更可能な通行制約 |
| **AABB** | Axis-Aligned Bounding Box。ポリゴン外接矩形。事前フィルタに使用 |
| **CH** | Contraction Hierarchies。経路計算高速化手法。Phase 4 以降で検討 |
| **GeoJSON** | 地理データを JSON で記述する標準フォーマット（RFC 7946）。座標系は WGS84 経度・緯度の順 |
| **Polygon (GeoJSON)** | 1 つ以上の閉じた線リング配列で表現される面。第1配列が外側境界、第2配列以降が Hole（内側の穴） |
| **MultiPolygon (GeoJSON)** | 複数の Polygon を持つ Geometry。離散した複数領域を 1 つの Feature として表現 |
| **Hole (GeoJSON)** | Polygon の内側に切り抜かれる穴領域。外側境界内かつ Hole 外のみが有効領域 |
| **FeatureCollection (GeoJSON)** | 複数の Feature（Geometry + Properties）をまとめた最上位オブジェクト |
| **メッシュ** | 地理空間を格子状に区切った領域 |
| **JIS X0410** | 「地域メッシュ統計のための地域区分」を規定した JIS 規格。第1次（80km）/ 第2次（10km）/ 第3次（1km）および細分メッシュを定義 |
| **地域メッシュコード** | JIS X0410 で各メッシュに割り当てられた数値コード。本プロジェクトでは第3次（1km、8桁）/ 1/2 細分（500m、9桁）/ 1/4 細分（250m、10桁）/ 1/10 細分（100m、11桁）の 4 階層に対応 |
| **SemVer** | Semantic Versioning。`MAJOR.MINOR.PATCH` 形式の互換性保証付きバージョニング |

---

## 12. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 (draft) | 2026-05-18 | 初版ドラフト作成 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-18 | 要件 ID（REQ-XXX-NNN）形式に再構成、ジャンル別整理、Phase/Ver 記法導入、地域メッシュコード対応反映 | Claude (Opus 4.7) |
| 1.0 (確定) | 2026-05-18 | 車両プロファイル Phase 分割確定、メッシュ階層 1km〜100m の 4 階層対応確定、ユーザー合意済み | Claude (Opus 4.7) |
| 1.1 (確定) | 2026-05-18 | 動的制約入力に GeoJSON（Polygon / MultiPolygon / FeatureCollection、Hole 対応、RFC 7946 準拠）を追加（REQ-RST-020〜029） | Claude (Opus 4.7) |

---

## 13. 次のアクション

- [x] ユーザーレビュー
- [x] 各要件 ID へのフィードバック・修正反映
- [x] **車両プロファイル Phase 分割**の確定（REQ-PRF-003〜006、Bicycle/Truck を Phase 2、緊急車両/災害用車両を Phase 3 に確定）
- [x] **メッシュ階層対応範囲**の確定（REQ-RST-016、Phase 1 で 1km / 500m / 250m / 100m の 4 階層対応）
- [x] **親プロジェクト側の Phase 1 希望時期**: 気にしない方針で確定（Phase 1 の所要時間が短いと見込まれるため、独自スケジュールで進める）
- [x] ステータスを「ドラフト」から「確定」に変更
- [ ] Phase 1 開始前に `git init` の実施
- [ ] Phase 1 実装計画書（`phase1_implementation_plan.md`）の作成
