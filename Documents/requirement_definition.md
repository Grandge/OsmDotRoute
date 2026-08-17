# OsmDotRoute 要件定義書

**バージョン**: 2.3（確定）
**作成日**: 2026-05-18
**最終更新**: 2026-05-20
**ステータス**: 確定（Phase 1 完了 / v0.1.0 タグ付与済、commit `e5d90f2`。Phase 2/3 のスコープを再編：Phase 2 = `.odrg` 形式策定 + 独自 OSM PBF パーサー + PBF→`.odrg` 抽出に絞り、Phase 3 = ランタイム読込 + Itinero 依存削除 + Bicycle/Truck + ベンチ + 親プロジェクト統合へ移行。理由は §12 v2.3 改訂エントリ参照）

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

- **[Phase1]** — 経路探索エンジン独自化（Itinero をデータ層として残す）。完了 (v0.1.0, 2026-05-20)
- **[Phase2]** — **データ供給側の独自化**：独自バイナリグラフ形式 `.odrg` 策定 + 独自 OSM PBF パーサー + PBF → `.odrg` 抽出ツール。末尾オプションで Itinero RouterDb → `.odrg` 変換ツール（v2.3 で再編、§12 改訂履歴参照）
- **[Phase2-opt]** — Phase 2 末尾のオプションタスク。`.odrg` 設計完了後に「変換可能なら低優先度で作る」スタンス
- **[Phase3]** — **データ利用側の独自化**：ランタイム `.odrg` 読込 + ランタイム Itinero 依存完全削除 + Bicycle/Truck プロファイル + 性能ベンチマーク + 親プロジェクト統合・パリティ検証
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

- [x] [P1] [Phase1] **REQ-RTE-001**: 2点間（緯度経度）の最短経路を Dijkstra ベースで計算し、独自 `Route` 型で返すこと。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RTE-002**: 任意の緯度経度座標を最寄り道路上にスナップし、スナップ点の緯度経度を返すこと。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RTE-003**: スナップに使用する検索半径（メートル）を呼び出し側で指定可能とすること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RTE-004**: 道路ネットワーク全体を GeoJSON FeatureCollection（LineString 列）として出力できること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RTE-005**: 経路計算 API は同期版を基本提供すること。非同期 API は要望が出るまで提供しない。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RTE-006**: 経路が見つからなかった場合は `null` を返し、例外を投げないこと。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RTE-007**: 経路計算結果に総距離（メートル）・総所要時間（秒）・経路形状（緯度経度列）を含めること。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RTE-008**: 道路ネットワーク外の座標を起点／終点に指定した場合、`null` を返し例外を投げないこと。(Ver. 0.18)
- [ ] [P3] [Phase4+] **REQ-RTE-009**: 双方向 Dijkstra 等の高速化アルゴリズムを導入し、性能要件未達時の対策とすること。(Ver. -)
- [x] [P1] [Phase4] **REQ-RTE-010**: 指定プロファイルで 2 点間の経路を計算し、矩形範囲 R（北西端・南東端で指定可能）の境界とルートの交点、および交点から範囲外側の端点までのルート上距離を返す API を提供すること。範囲 R は読み込み済み地図の内側の任意の矩形でよい。判定結果は「両端が範囲内」「両端が範囲外」「起点 A が範囲外」「終点 B が範囲外」の 4 種を列挙値で返し、交点・距離は後 2 者のときのみ非 `null` とする。内外判定は利用者が指定した生の座標で行い、境界線上は範囲内扱いとする。(Ver. 1.3.0)
- [x] [P1] [Phase4] **REQ-RTE-011**: REQ-RTE-010 において、ルートが範囲境界を複数回またぐ場合は**範囲内側の端点に近い側の交点**を返すこと。返す距離・所要時間はその交点から範囲外側の端点までのルート上の値とし、途中で範囲内に戻る区間もこれに含めること。交点座標は Shape 線分と矩形辺の厳密な交点を線形補間で求め（最寄り Shape 頂点で代用しない）、距離は Shape 頂点列の Haversine 積算とすること（`Route.TotalDistanceM` による按分スケーリングは行わない）。(Ver. 1.3.0)
- [x] [P2] [Phase4] **REQ-RTE-012**: REQ-RTE-010 において、交点から範囲外側の端点までのルート上所要時間（秒）も併せて返すこと（`Route.CumulativeDurationsSec` を交点位置で線形補間）。また異常系は例外・`null` ではなく列挙値で報告すること: 経路未発見・スナップ失敗、および端点の内外判定とルート形状が矛盾する場合（両端が範囲内なのにルートが範囲外へ出る／両端が範囲外なのにルートが範囲内を通る／範囲内と判定した端点のスナップ先が範囲外）は「ルート探査エラー」、範囲 R の南北・東西逆転／面積ゼロ／非有限値／緯度経度定義域外は「パラメータ異常」とすること。(Ver. 1.3.0)
- [x] [P2] [Phase4] **REQ-RTE-013**: REQ-RTE-010 の判定を、計算済みの `Route` に対して経路再計算なしで実行できる純幾何 API も併せて提供すること。(Ver. 1.3.0)

### 5.2 動的制約管理 (REQ-RST)

#### 5.2.a 進入不可エリア

- [x] [P1] [Phase1] **REQ-RST-001**: 緯度経度ポリゴンによる進入不可エリアを登録できること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-002**: 地域メッシュコード（JIS X0410 第3次メッシュおよびその細分メッシュ、後述の REQ-RST-016 で対応階層を規定）による進入不可エリアを登録できること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-003**: 複数の地域メッシュコードを一括で進入不可エリアとして登録できること（異なる階層の混在を許容）。(Ver. 0.18)

#### 5.2.b 難所エリア（Difficulty Area）

「客観的事実（道路状況の種別）」を制約として登録し、「主観的反応（速度低下係数・通行可否）」は車両プロファイル側で規定する分離設計を採用する。

- [x] [P1] [Phase1] **REQ-RST-004**: 緯度経度ポリゴン + 難所タイプ（`string`、REQ-PRF-012 規定の組込み 8 種または REQ-PRF-013 のユーザー定義）による難所エリアを登録できること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-005**: 地域メッシュコード（REQ-RST-016 で規定する階層）+ 難所タイプによる難所エリアを登録できること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-006**: 複数の地域メッシュコードを一括で難所エリアとして登録できること（異なる階層の混在を許容）。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-007**: 難所タイプ文字列は空文字・`null` を拒否すること（引数例外）。組込み 8 種以外の任意キーは許容し、プロファイルが知らないキーには `difficultyDefault`（REQ-PRF-014）を適用すること。(Ver. 0.18)

#### 5.2.c 制約の削除・管理

- [x] [P1] [Phase1] **REQ-RST-008**: 登録された制約を一意の ID で個別削除できること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-009**: 全制約を一括クリアできること。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RST-010**: 制約登録時に任意のタグ文字列を付与でき、タグ単位で一括削除できること。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RST-011**: 登録済み制約の一覧を読み取り専用ビューで取得できること。(Ver. 0.18)

#### 5.2.d 反映タイミング・判定ロジック

- [x] [P1] [Phase1] **REQ-RST-012**: 制約の追加・削除・クリアは、次回の経路計算呼び出しから即時反映されること。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RST-013**: 空間制約の判定は、エッジのシェイプ列を用いた交差判定で行うこと。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RST-014**: 空間制約判定の事前フィルタとして外接矩形（AABB）による枝刈りを行うこと。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RST-015**: メッシュコード指定の場合、メッシュ矩形を AABB として直接使用すること（多角形ポリゴン交差判定をスキップ）。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-016**: メッシュコードの対応階層を以下の 4 階層とすること。(Ver. 0.18 で 3 階層、11 桁は 2026-06-11 改訂で「1/8 細分（象限方式）」として仕様確定・追加 = Ver. 1.2.0)
  - **第3次メッシュ** (約 1km 四方、8 桁、例 `53394547`)
  - **1/2 細分メッシュ** (約 500m 四方、9 桁、例 `533945471`)
  - **1/4 細分メッシュ** (約 250m 四方、10 桁、例 `5339454713`)
  - **1/8 細分メッシュ** (約 125m 四方=緯度 3.75 秒×経度 5.625 秒、11 桁、例 `53394547131`)。11 桁目は 1/4 細分をさらに 2×2 分割した象限番号 1〜4（南西=1、南東=2、北西=3、北東=4）で、9・10 桁目と同一の象限再帰。**「10分の1細分区画」（8 桁＋2 桁 = 10 桁・100m）は既存 1/4 細分と桁数衝突するため採用しない**（v1.4 で延期した「1/10 細分 = 100m / 11 桁」記載はこの仕様に読み替えて確定。親プロFB [`feature_request_mesh_level8_and_gml_attributes.md`](feature_request_mesh_level8_and_gml_attributes.md) 要望①）
- [x] [P1] [Phase1] **REQ-RST-017**: 入力されたメッシュコードの桁数から階層を自動判定し、対応する経緯度矩形領域に変換できること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-018**: 桁数が REQ-RST-016 の規定に該当しないメッシュコードは、引数例外で拒否すること。(Ver. 0.18)
- [ ] [P3] [Phase4+] **REQ-RST-019**: 第1次メッシュ（80km）・第2次メッシュ（10km）への対応拡張は要望が出た時点で個別判断する。(Ver. -)

#### 5.2.e 難所重複時の判定ルール

- [x] [P1] [Phase1] **REQ-RST-030**: 同一エッジに複数の難所エリアが交差する場合、各難所に対する速度低下係数の**積**を採用すること。(Ver. 0.18) **【v1.3.1 改訂】積の単位は「登録エリア（`RestrictedAreaId`）」であり、1 エリアが複数 Shape（メッシュ集合・分割ポリゴン）を持つ場合でも当該エリアの係数は**エッジあたり 1 回のみ**適用する。v1.3.0 までは bake 経路（`IRoadGraph` 注入時）だけがエッジの跨いだ Shape 数 N に対し係数を `speedFactor^N` として掛けており、フォールバック経路（`EvaluateConstraints`、ID 単位で重複排除）と結果が食い違っていた（親プロFB [`bug_request_difficulty_factor_per_shape.md`](bug_request_difficulty_factor_per_shape.md)）。
- [x] [P1] [Phase1] **REQ-RST-031**: いずれかの難所判定で `canPass: false`（通行不可）が返された場合、他の判定結果に関わらず通行不可とすること（短絡評価）。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-032**: 進入不可エリア（REQ-RST-001〜003）と難所エリア（REQ-RST-004〜006）の重複時も、進入不可が優先されること。(Ver. 0.18)

#### 5.2.f GML 入力対応（国土数値情報 KSJ アプリケーションスキーマ）

- [x] [P1] [Phase1] **REQ-RST-020**: 国土数値情報 KSJ アプリケーションスキーマ準拠 GML 3.2（`<ksj:Dataset>` ルート）を入力として進入不可エリア／難所エリアを登録できること。Phase 1 動作確認は A31「浸水想定区域」(`<ksj:ExpectedFloodArea>`) で実施するが、パーサーはフィーチャ要素名にハードコード依存せず、任意の KSJ プロダクトを受け入れること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-021**: 1 つの GML ファイル内の複数フィーチャを一括登録できること。各フィーチャごとに登録 ID を返すこと。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-022**: `<gml:Surface>` の `<gml:exterior>`（外周）と `<gml:interior>`（Hole）に対応し、外周内かつ Hole 外の領域のみを制約対象とすること。(Ver. 0.18)
- [ ] [P3] [Phase2+] **REQ-RST-023**: `<gml:MultiSurface>` で複数 Surface を 1 フィーチャに紐付ける構造への対応は Phase 2 以降に延期する（A31「浸水想定区域」サンプル `A31-12_24.xml`（1.6 GB）で `MultiSurface` 出現 0 件を確認、2026-05-19）。Phase 1 では検出時に `NotSupportedException` を投げる。(Ver. 1.5)
- [x] [P2] [Phase1] **REQ-RST-024**: GML ファイル（`.xml` / `.gml`）を直接読み込んで制約を一括登録できること。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RST-025**: GML 文字列（`string`）／`System.IO.Stream` からの制約一括登録 API を提供すること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-026**: GML 入力 API は進入不可エリア用と難所エリア用の 2 系統を提供すること。難所エリア用 API は引数で難所タイプ文字列を受け取り、ファイル内全フィーチャに同一の難所タイプを適用すること。フィーチャ要素名や属性値から難所タイプを自動判定する仕組みは設けない（利用者責任、複数の KSJ プロダクトを共通基盤で扱うため）。GML 内のフィーチャ属性（`<ksj:waterDepth>` 等）は本 API 系統では保持せず読み飛ばす（属性が必要な場合は REQ-RST-041 の `ParseFeatures*` を使う）。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-RST-027**: GML 入力 API は `tag` 引数で全フィーチャに同一タグ文字列を付与できること（REQ-RST-010 のタグ機構と連携、バッチ識別子用途）。フィーチャ別タグはサポートしない（KSJ 標準にタグ相当属性がないため）。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-RST-028**: GML の座標系を「緯度 経度」順（JGD2000、KSJ 規定）として扱うこと。他の座標系（WGS84 経度緯度順等）は本ライブラリでは扱わず、利用者側で事前変換すること。(Ver. 0.18)
- [ ] [P3] [Phase4+] **REQ-RST-029**: 汎用 GML 3.2（KSJ 拡張なし）／GeoJSON ／ Shapefile ／ TopoJSON 等の他形式対応は要望が出た時点で個別判断する。(Ver. 1.5)
- [x] [P1] [Phase1] **REQ-RST-040**: GML 入力 API はマップ範囲（緯度経度 AABB、`MapBounds` 値型）による フィーチャフィルタを optional 引数として受け付けること。指定時は、フィーチャの外周頂点が 1 つでもマップ範囲内（境界線上を含む）にあるフィーチャのみを採用し、0 個のものは登録せずスキップする（Hole は判定に使わない、シミュレーションの道路ネットワーク範囲外のフィーチャを除外する用途）。マップ範囲未指定 (`null`) 時は全フィーチャを採用する（互換動作）。(Ver. 0.18)
- [x] [P2] [Phase4] **REQ-RST-041**: KSJ GML をフィーチャ単位の「形状＋属性」ペアで取得できる公開パース API を提供すること。`GmlParser.ParseFeaturesString(string)` / `ParseFeaturesStream(Stream)` が `IReadOnlyList<GmlFeature>`（`GmlFeature` = `GeoPolygon Polygon` + `IReadOnlyDictionary<string, string> Attributes`）を返す。属性はフィーチャ要素直下の単純な子要素（子要素なし・xlink 参照なし・テキストあり）から抽出し、key = 名前空間 prefix を剥がしたローカル名（例 `A51_001`）、value = テキスト内容（型解釈・コードリスト解決は利用側責務）。属性が 1 つも無いフィーチャは空 Dictionary（例外にしない）。形状の解釈・フィーチャスキップ方針・例外は既存パーサ（REQ-RST-020〜028）と同一で、既存 `ParseString` / `ParseStream` / `AddBlockAreaFromGml*` / `AddDifficultyAreaFromGml*` の挙動は不変。用途は A51「雨水出水（内水）浸水想定区域」等、GeoJSON 未提供データセットの属性（浸水深ランク等）に基づく制約レベル振り分け（親プロFB [`feature_request_mesh_level8_and_gml_attributes.md`](feature_request_mesh_level8_and_gml_attributes.md) 要望②）。(Ver. 1.2.0)

### 5.3 車両プロファイル (REQ-PRF)

#### 5.3.a 同梱プロファイル

- [x] [P1] [Phase1] **REQ-PRF-001**: 同梱車両プロファイル `car`（普通自動車）を提供すること。実装は JSON 外部ファイル、内容は Itinero `Vehicle.Car` 相当の OSM タグ解釈を踏襲。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-PRF-002**: 同梱車両プロファイル `pedestrian`（歩行者）を提供すること。実装は JSON 外部ファイル、内容は Itinero `Vehicle.Pedestrian` 相当の OSM タグ解釈を踏襲。(Ver. 0.18)
- [ ] [P2] [Phase3] **REQ-PRF-003**: 同梱車両プロファイル `bicycle`（自転車）を提供すること。(Ver. -、v2.3 で Phase 2 → Phase 3 へ移動。Phase 2 はデータ供給側に集中させるため）
- [ ] [P2] [Phase3] **REQ-PRF-004**: 同梱車両プロファイル `truck`（10 t トラック）を提供すること。**独自設計**（Itinero / OSRM 流用ではなく日本道路法ベース、最大積載量 10 t・車両総重量 20 t 級・高さ/幅制限・`hgv=*` / `access=destination` 等を考慮）。(Ver. -、v2.3 で Phase 2 → Phase 3 へ移動、Truck=10 t を確定)
- [x] [P3] [Phase4] **REQ-PRF-005**: 同梱車両プロファイル（緊急車両：救急車・消防車相当）を提供すること。**救急車 `ambulance`（小型 4.0t / 2.6m / 2.0m）と消防車 `fire_engine`（大型 8.0t / 2.9m / 2.1m）を別プロファイルに分割**して提供（2026-06-03 ユーザー決定、ID は分割せず 1 要件 = 2 プロファイル）。緊急走行特例として `ignoreOneway=true`（逆走可）、`emergency` アクセスタグ評価、歩道（footway/path/pedestrian）も限定速度で通行可。`landslide` は物理的不可のため canPass=false 維持。Phase 4 で完了、設計は [phase4_design.md](phase4_design.md) §2 参照。(Ver. -、ユーザー採番)
- [x] [P3] [Phase4] **REQ-PRF-006**: 同梱車両プロファイル `disaster`（災害用車両）を提供すること。**難所耐性中心**（flooding/liquefaction/construction/obstacle の speedFactor を truck より高めに設定）。寸法は truck 同等（20t / 3.8m / 2.5m）で緩和せず、災害規制区間の動的指定は上位レイヤー（RestrictedArea 付け外し）の責務とするため `ignoreOneway=false`。`landslide` は canPass=false 維持。Phase 4 で完了、設計は [phase4_design.md](phase4_design.md) §2 参照。(Ver. -、ユーザー採番)

#### 5.3.b プロファイル外部ファイル化（リビルド不要の調整可能性）

- [x] [P1] [Phase1] **REQ-PRF-007**: 車両プロファイルは外部 JSON ファイル形式で定義可能とし、ライブラリのリビルドなしにパラメータ調整できること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-PRF-008**: 同梱プロファイル JSON はアセンブリ埋込リソースとして配置し、デフォルト動作で外部ファイル無しに利用可能とすること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-PRF-009**: ユーザーが独自の JSON プロファイルをファイルパスまたは文字列から読み込める API を提供すること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-PRF-010**: プロファイル JSON スキーマは少なくとも以下を含むこと: 名称 / 車両種別 / `highway` タグ別の通行可否と速度 / アクセスタグ評価ルール / `difficulty` セクション。(Ver. 0.18)

#### 5.3.c 難所タイプ対応（プロファイル × 難所のマトリクス）

- [x] [P1] [Phase1] **REQ-PRF-011**: プロファイルは難所タイプ毎の速度低下係数（0.0〜1.0）と通行可否（`canPass`）を保持すること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-PRF-012**: 組込み難所タイプを以下 8 種とすること（英語キー、日本語併記）。(Ver. 0.18)
  - `flooding`（冠水）
  - `liquefaction`（液状化）
  - `landslide`（土砂崩れ）
  - `construction`（工事中）
  - `obstacle`（障害物。瓦礫・落下物等を包含）
  - `congestion`（交通集中）
  - `snow`（積雪）
  - `ice`（凍結）
- [x] [P1] [Phase1] **REQ-PRF-013**: ユーザーが独自の難所タイプ（任意の文字列キー、英数字とアンダースコアのみ）をプロファイル JSON で追加可能とすること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-PRF-014**: プロファイル定義に存在しない難所タイプが指定された場合、`difficultyDefault`（規定: `speedFactor=1.0`, `canPass=true`）を適用すること。(Ver. 0.18) **【v1.1.1 改訂】難所タイプ照合は case-insensitive（Ordinal-IgnoreCase）。`"Flooding"` / `"FLOODING"` 等の表記揺れでも正準小文字キー（`DifficultyTypes` 定数）と一致する。プロファイル JSON 内に case 違いで重複するキー（`"flooding"` と `"Flooding"` の併存等）は `InvalidProfileException` で拒否する。** (Ver. 1.1.1、親プロFB 起源の不具合修正)
- [x] [P2] [Phase4] **REQ-PRF-017**: 利用者が難所タイプの定義有無を事前確認できる観測性 API を `VehicleProfile` 公開面に提供すること。具体的には `IReadOnlyCollection<string> KnownDifficultyTypes`（定義済キー一覧）と `bool HasDifficulty(string)`（case-insensitive 含有判定）の 2 つ。これは REQ-PRF-014 のサイレント・フォールバック挙動を利用者側で能動的に検知できるようにする観測性フックである（親プロFB 起源、`Documents/feature_request_per_segment_durations.md` 検証中に発覚した不具合への再発防止策）。(Ver. 1.1.1)

#### 5.3.d 将来拡張

- [ ] [P3] [Phase4+] **REQ-PRF-015**: JSON では表現できない動的ルール（時間帯依存・条件分岐等）を C# でプラグインする拡張 API を提供すること。要望が出るまで未実装。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-PRF-016**: Itinero Lua プロファイル互換層を別アセンブリで提供すること。要望が出るまで未実装。(Ver. -)

### 5.4 地図データ・グラフ (REQ-MAP)

- [x] [P1] [Phase1] **REQ-MAP-001**: Itinero RouterDb（`.routerdb`）ファイルを読み込めること。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-MAP-002**: 読み込み済みグラフから頂点数・辺数・経緯度範囲の統計情報を取得できること。(Ver. 0.18)
- [ ] [P1] [Phase2] **REQ-MAP-003**: 独自バイナリグラフ形式 `.odrg` を策定すること（仕様は別文書 `phase2_graph_format_spec.md` に記載）。動的制約のホットパスをデータ形式自体が支援する設計とする（エッジ AABB bake / STR パック静的 R-tree / エッジシェイプ連続バッファ / エッジ bitflag）。(Ver. -)
- [ ] [P3] [Phase2-opt] **REQ-MAP-004**: Itinero RouterDb → `.odrg` 一括変換ツール（CLI 等）を提供すること。**Phase 2 末尾のオプション**：v2.3 で「`.odrg` 設計を RouterDb 構造に引きずられないようにする」ため Phase 2 主軸から外した。`.odrg` 設計完了後に技術的負担が軽ければ作る、重ければ Phase 3 以降に延期。(Ver. -、v2.3 で P1 → P3、Phase2 → Phase2-opt へ降格)
- [ ] [P1] [Phase2] **REQ-MAP-007**: OSM PBF ファイルを直接読み込む独自 protobuf パーサーを提供すること。**System.\* のみで実装**（外部依存 protobuf-net 等は使わない）。サポート要素は OsmDotRoute 必要分（HeaderBlock / PrimitiveGroup の Way / Node / DenseNodes / Relation）に限定。(Ver. -、v2.3 で Phase 3 → Phase 2 へ前倒し)
- [ ] [P1] [Phase2] **REQ-MAP-008**: OSM PBF から `.odrg` を直接ビルドする CLI ツール `OsmDotRoute.Extractor` を提供すること。`extract --input *.osm.pbf --profiles car,pedestrian --output *.odrg` 形式。(Ver. -、v2.3 で Phase 3 → Phase 2 へ前倒し)
- [ ] [P1] [Phase3] **REQ-MAP-005**: 独自バイナリグラフ形式 `.odrg` のファイルをランタイムから読み込めること。`MemoryMappedFile` でビュー化、`ReadOnlySpan<T>` でゼロコピー公開。(Ver. -、v2.3 で Phase 2 → Phase 3 へ移動)
- [ ] [P1] [Phase3] **REQ-MAP-006**: ランタイム経路計算から Itinero アセンブリへの依存を排除すること。(Ver. -、v2.3 で Phase 2 → Phase 3 へ移動)
- [ ] [P1] [Phase3] **REQ-MAP-009**: ライブラリ全体（変換ツールを含む）から Itinero への一切の依存を排除すること。**v2.3 で Phase 2 が PBF 直接抽出主軸となったことにより、Phase 2 完了時点で実質達成見込み**（RouterDb 変換ツール REQ-MAP-004 を作らない場合）。(Ver. -)
- [x] [P1] [Phase4] **REQ-MAP-010**: `RouterDb` が保持するリソース（ファイル版: MMF ファイルハンドル / メモリ版: ピン留めバッファ）を利用側から確定的に解放できること（`IDisposable` 実装）。ファイル版 `LoadFromOdrg(string)` は `.odrg` を MemoryMappedFile で開いたまま保持するため、Dispose しない限り当該ファイルの上書き・削除ができない（親プロのシナリオ上書き保存が IOException で失敗する実バグの原因）。`Dispose()` は冪等。Dispose 後の本インスタンス・派生 `Router` / スナップ機能の使用は `ObjectDisposedException`（既存 `ThrowIfDisposed` の挙動）。Dispose を呼ばない既存利用コードの挙動は不変（加算的・非破壊）。親プロFB [`feature_request_routerdb_dispose.md`](feature_request_routerdb_dispose.md) 対応。(Ver. 1.2.1)

### 5.5 パブリック API 設計 (REQ-API)

- [x] [P1] [Phase1] **REQ-API-001**: エントリーポイントを `OsmDotRoute.Router` クラスとするファサードパターンを採用すること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-API-002**: 出力型は OsmDotRoute 独自の `Route` 型とし、`Itinero.Route` 型を公開 API に露出させないこと。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-API-003**: 親プロジェクト側から `using Itinero;` を完全に消去できる API 設計とすること（Itinero 名前空間を内部実装に隠蔽）。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-API-004**: 動的制約の管理を `OsmDotRoute.RestrictedAreaService` クラスで提供すること。(Ver. 0.18)
- [x] [P2] [Phase1] **REQ-API-005**: `Microsoft.Extensions.DependencyInjection` 互換の DI 登録拡張メソッド（`AddOsmDotRoute()` 等）を提供すること。(Ver. 0.17)
- [x] [P2] [Phase1] **REQ-API-006**: 公開型は XML ドキュメンテーションコメント（`<summary>` 等）を完備すること。(Ver. 0.17)
- [ ] [P2] [Phase4+] **REQ-API-007**: 1.0 リリース以降は SemVer に準拠したバージョニングを行うこと。(Ver. -)
- [x] [P3] [Phase1] **REQ-API-008**: 0.x 期間中の破壊的 API 変更はマイナー版アップで許容する旨を README に明記すること。(Ver. 0.17)

### 5.6 データフォーマット (REQ-FMT)

#### 5.6.a 経路出力型

- [x] [P1] [Phase1] **REQ-FMT-001**: 経路出力型 `Route` に総距離（メートル単位、`double`）を含めること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-FMT-002**: 経路出力型 `Route` に総所要時間（秒単位、`double`）を含めること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-FMT-003**: 経路出力型 `Route` に経路形状（`IReadOnlyList<GeoCoordinate>`）を含めること。(Ver. 0.18)
- [x] [P2] [Phase4] **REQ-FMT-006**: 経路出力型 `Route` に Shape 点別の累積所要時間（秒、`ReadOnlyMemory<double>`、プロパティ名 `CumulativeDurationsSec`）を含めること。`Shape` と 1:1 整列、`[0]==0`、`[^1]==TotalDurationSec`（厳密一致）、単調非減少、移動困難エリアの速度低下（エッジ単位 SpeedFactor 由来）が区間所要時間に反映されること。区間 i（`Shape[i]→Shape[i+1]`）の所要時間は隣接累積秒の差で導出可能（区間別 API は提供しない）。親プロジェクト「災害廃棄物処理シミュレーション」の区間別速度低下アニメーション要望を起源とする（`Documents/feature_request_per_segment_durations.md`）。(Ver. 1.1.0)

#### 5.6.b 形式変換ユーティリティ

- ~~**REQ-FMT-004**: 経路を GeoJSON LineString に変換するユーティリティを提供すること。~~ **【廃止・Ver. 1.7】** 親プロジェクトの実需要が不明確で、利用者側で `Route.Shape` から数行で GeoJSON 化可能なため YAGNI と判断。要望が出た時点で再度評価する（経緯は設計書 §13 参照）。
- [ ] [P3] [Phase4+] **REQ-FMT-005**: 経路を Encoded Polyline 形式に変換するユーティリティを提供すること（要望次第）。(Ver. -)

---

## 6. 非機能要件

### 6.1 性能・スケーラビリティ (REQ-NFR — 性能)

- [x] [P1] [Phase1] **REQ-NFR-001**: 都道府県単位（数百万エッジ）のグラフで 1 経路計算あたり 100ms 以内を目標とすること。(Ver. 0.20, 市単位（57k エッジ、津島市）にて 33ms（Itinero の 0.48x）で達成、ステップ 17 で MapVerifier 体感確認済 PF-1。都道府県単位 RouterDb での最終確認は Phase 3 完了後に実施へ送り、ベンチ結果は [phase1_benchmark_results.md](phase1_benchmark_results.md) 参照)
- [x] [P1] [Phase1] **REQ-NFR-002**: 制約 100 件登録時にも REQ-NFR-001 の性能目標を維持すること。(Ver. 0.20, C3 = 51ms（C0 比 1.43x）で達成、ステップ 17 で MapVerifier 体感確認済 PF-2/PF-3。都道府県単位再検証は Phase 3 完了後)
- [x] [P2] [Phase1] **REQ-NFR-003**: 都道府県単位 RouterDb 読み込み時に、システム搭載 RAM 16GB で動作可能とすること。(Ver. 0.20, 市単位 WorkingSet 54MB / ManagedHeap 23MB で達成、都道府県単位は約 100 倍規模を外挿しても 5〜6GB 程度の見込み、Phase 3 完了後に再確認)
- [ ] [P3] [Phase4+] **REQ-NFR-004**: Contraction Hierarchies（CH）対応により大規模グラフでの高速化を実現すること。(Ver. -)

### 6.2 対応プラットフォーム (REQ-NFR — プラットフォーム)

- [x] [P1] [Phase1] **REQ-NFR-005**: .NET 9 上で動作すること。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-NFR-006**: Windows 10/11 (x64) で動作すること。(Ver. 0.18)
- [x] [P3] [Phase4] **REQ-NFR-007**: Linux / macOS 対応。**Phase 4 でマルチプラットフォーム検証完了**（Linux x64 = WSL2 + CI ubuntu-latest、macOS ARM64 = GitHub Actions ミラー `OsmDotRoute-ci-macos`、いずれも 753 pass / 0 fail / 0 skip）。`.odrg` の MMF / `GetSpan<T>` 経路が ARM64・16KB ページ環境でも破綻しないことを実機確認。NuGet 配布自体は別途保留（REQ-PKG-004）。詳細は [phase4_multiplatform_plan.md](phase4_multiplatform_plan.md) 参照。(Ver. -、ユーザー採番)
- [ ] [P3] [Phase4+] **REQ-NFR-008**: .NET 8 LTS 等の旧バージョン互換対応は要望が出た時点で個別判断する（Phase 4 のマルチプラットフォーム対応は OS 横断のみで .NET バージョン横断は未着手）。(Ver. -)

### 6.3 対応地域・単位系 (REQ-NFR — 地域)

- [x] [P1] [Phase1] **REQ-NFR-009**: 対応地域は日本国内のみを前提とすること（OSM タグ解釈・座標範囲を日本領域に最適化してよい）。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-NFR-010**: 単位系はメートル法（メートル/秒/m/s）のみ対応とすること。(Ver. 0.18)
- [ ] [P3] [Phase4+] **REQ-NFR-011**: グローバル対応（OSM 全地域）は要望が出た時点で個別判断する。(Ver. -)

### 6.4 配布・公開戦略 (REQ-PKG)

- [x] [P1] [Phase1] **REQ-PKG-001**: Phase 1 では本プロジェクトをソースとして親プロジェクトから `<ProjectReference>` で参照可能とすること。(Ver. 0.17)
- [ ] [P2] [Phase3] **REQ-PKG-002**: Phase 3 完了までは非公開リポジトリで管理し、外部公開しないこと。(Ver. -、v2.3 で Phase 2 まで → Phase 3 完了まで に変更。Phase 2 はデータ供給側のみ完了する段階で単体実用にならないため、Phase 3 完了をもって OSS 公開判断とする)
- [ ] [P1] [Phase3] **REQ-PKG-003**: Phase 3 完了時点で GitHub 個人アカウント上で OSS 公開できる状態とすること（README/LICENSE/CI 整備済み）。(Ver. -)
- [ ] [P3] [Phase4+] **REQ-PKG-004**: NuGet.org への公開可否は Phase 3 完了後に別途判断する（当面公開しない）。(Ver. -)

### 6.5 ライセンス (REQ-LIC)

- [x] [P1] [Phase1] **REQ-LIC-001**: ライブラリ本体のライセンスを MIT License とすること。(Ver. 0.17)
- [x] [P1] [Phase1] **REQ-LIC-002**: Itinero（Apache 2.0）のソースコードを本プロジェクトにコピー・改変して取り込まないこと。(Ver. 0.18)
- [x] [P1] [Phase1] **REQ-LIC-003**: Itinero への依存は NuGet 経由のバイナリ参照のみで行うこと。(Ver. 0.18)
- [ ] [P2] [Phase3] **REQ-LIC-004**: OSM データ（ODbL）の利用ガイドラインを README で利用者に案内すること（本ライブラリ自体は OSM データを内包しない）。(Ver. -)

### 6.6 依存ライブラリ方針 (REQ-DEP)

- [x] [P1] [Phase1] **REQ-DEP-001**: Phase 1 ではランタイムが Itinero 1.5.1 系および System.* 標準ライブラリのみに依存すること。(Ver. 0.18)
- [ ] [P1] [Phase2] **REQ-DEP-002**: Phase 2 では `.odrg` 形式策定・独自 PBF パーサー・PBF→`.odrg` 抽出ツール（`OsmDotRoute.Extractor`）のすべてが **System.\* 標準ライブラリのみに依存すること**。protobuf-net 等の外部依存は使わず、PBF プロトコルバッファは独自実装する（v2.3 で「変換ツール内部の Itinero 利用は許容」条項を削除、Phase 2 スコープから RouterDb 変換ツールを外したため）。(Ver. -、v2.3 で条文書き換え)
- [ ] [P1] [Phase3] **REQ-DEP-003**: Phase 3 ではランタイム経路計算・スナップ・プロファイル評価・ベンチマークのすべてが **System.\* 標準ライブラリのみに依存すること**。`OsmDotRoute.Itinero` プロジェクトを撤去し、ライブラリ全体（REQ-MAP-004 オプション RouterDb 変換ツールを作らない場合）から Itinero への依存を排除する。(Ver. -、v2.3 で条文書き換え)

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

        // 範囲境界との交点判定（REQ-RTE-010〜013 で追加）。null は返さず、異常系も Kind で報告する。
        public BoundaryCrossingResult CalculateBoundaryCrossing(
            VehicleProfile profile, GeoCoordinate from, GeoCoordinate to,
            MapBounds bounds, float searchDistanceM = 500f);

        // 計算済み Route に対する純幾何版（REQ-RTE-013）。
        public static BoundaryCrossingResult FindBoundaryCrossing(
            Route route, GeoCoordinate from, GeoCoordinate to, MapBounds bounds);
    }

    // 範囲境界との交点判定結果（REQ-RTE-010〜012）
    public enum BoundaryCrossingKind
    {
        BothInside,        // 範囲内
        BothOutside,       // 範囲外
        PointAOutside,     // A（起点が範囲外）
        PointBOutside,     // B（終点が範囲外）
        RouteSearchError,  // ルート探査エラー
        InvalidParameter,  // パラメータ異常
    }

    public sealed record BoundaryCrossingResult(
        BoundaryCrossingKind Kind,
        GeoCoordinate? Crossing,              // PointAOutside / PointBOutside のときのみ非 null
        double? DistanceToOutsidePointM,      //  〃
        double? DurationToOutsidePointSec);   //  〃

    public readonly record struct MapBounds(GeoCoordinate SouthWest, GeoCoordinate NorthEast)
    {
        public bool Contains(GeoCoordinate coordinate);   // 境界線上を含む

        // 北西端・南東端からの生成（REQ-RTE-010 で追加）
        public static MapBounds FromNorthWestSouthEast(GeoCoordinate northWest, GeoCoordinate southEast);
    }

    public sealed class RouterDb : IDisposable    // IDisposable は REQ-MAP-010 で追加
    {
        // LoadFromFile は OsmDotRoute.Itinero アダプター側に配置（v1.3 で変更）。
        // アセンブリ依存方向（OsmDotRoute ← OsmDotRoute.Itinero）維持のため、
        // コア側からアダプターを直接呼べないため。
        public RouterDbStatistics GetStatistics();

        // グラフ保持リソース（MMF ハンドル / ピン留めバッファ）の確定解放（REQ-MAP-010）。
        // 冪等。Dispose 後の本体・派生 Router / スナップ使用は ObjectDisposedException。
        public void Dispose();
    }

    // OsmDotRoute.Itinero アダプタープロジェクト（NuGet 別アセンブリ）
    namespace OsmDotRoute.Itinero
    {
        public static class ItineroRouterDbLoader
        {
            public static OsmDotRoute.RouterDb LoadFromFile(string filePath);
            public static OsmDotRoute.RouterDb FromItineroRouterDb(global::Itinero.RouterDb itineroRouterDb);
        }
    }

    // 車両プロファイル（enum ではなく JSON で外部化されたクラス）
    public sealed class VehicleProfile
    {
        public string Name { get; }                 // "car", "pedestrian" 等

        // 同梱プロファイル（埋込リソース）
        public static VehicleProfile Car        { get; }   // Profiles/car.json
        public static VehicleProfile Pedestrian { get; }   // Profiles/pedestrian.json

        // ユーザー定義プロファイル読込
        public static VehicleProfile LoadFromJsonFile(string filePath);
        public static VehicleProfile LoadFromJsonString(string json);
        public static VehicleProfile LoadFromJsonStream(System.IO.Stream stream);
    }

    public sealed class RestrictedAreaService
    {
        // ポリゴン指定（進入不可・難所）
        public RestrictedAreaId AddBlockArea(GeoPolygon polygon, string? tag = null);
        public RestrictedAreaId AddDifficultyArea(GeoPolygon polygon, string difficultyType, string? tag = null);

        // 地域メッシュコード指定（JIS X0410 第3次〜1/8 細分、1km〜125m、8〜11 桁。REQ-RST-016）
        public RestrictedAreaId AddBlockArea(MeshCode meshCode, string? tag = null);
        public RestrictedAreaId AddBlockArea(IEnumerable<MeshCode> meshCodes, string? tag = null);
        public RestrictedAreaId AddDifficultyArea(MeshCode meshCode, string difficultyType, string? tag = null);
        public RestrictedAreaId AddDifficultyArea(IEnumerable<MeshCode> meshCodes, string difficultyType, string? tag = null);

        // GML 入力（国土数値情報 KSJ アプリケーションスキーマ準拠 GML 3.2）
        // 形状（外周＋Hole）のみ抽出。難所タイプは引数で全フィーチャに適用、フィーチャ属性は保持しない
        // （属性が必要な場合は GmlParser.ParseFeatures* を使い利用者側で振り分け、REQ-RST-041）
        // mapBounds 指定時は外周頂点が 1 つでも範囲内にあるフィーチャのみ採用（REQ-RST-040）
        public RestrictedAreaId[] AddBlockAreaFromGml(string gml, MapBounds? mapBounds = null, string? tag = null);
        public RestrictedAreaId[] AddBlockAreaFromGmlFile(string filePath, MapBounds? mapBounds = null, string? tag = null);
        public RestrictedAreaId[] AddBlockAreaFromGmlStream(Stream stream, MapBounds? mapBounds = null, string? tag = null);
        public RestrictedAreaId[] AddDifficultyAreaFromGml(string gml, string difficultyType, MapBounds? mapBounds = null, string? tag = null);
        public RestrictedAreaId[] AddDifficultyAreaFromGmlFile(string filePath, string difficultyType, MapBounds? mapBounds = null, string? tag = null);
        public RestrictedAreaId[] AddDifficultyAreaFromGmlStream(Stream stream, string difficultyType, MapBounds? mapBounds = null, string? tag = null);

        public void Remove(RestrictedAreaId id);
        public void RemoveByTag(string tag);
        public void ClearAll();
        public IReadOnlyList<RestrictedArea> ListAll();
    }

    // 組込み難所タイプの文字列定数（const string、ユーザー定義タイプとの混在可）
    public static class DifficultyTypes
    {
        public const string Flooding     = "flooding";      // 冠水
        public const string Liquefaction = "liquefaction";  // 液状化
        public const string Landslide    = "landslide";     // 土砂崩れ
        public const string Construction = "construction";  // 工事中
        public const string Obstacle     = "obstacle";      // 障害物
        public const string Congestion   = "congestion";    // 交通集中
        public const string Snow         = "snow";          // 積雪
        public const string Ice          = "ice";           // 凍結
    }

    // KSJ GML のフィーチャ単位パース（形状＋属性、REQ-RST-041。制約登録を伴わない読み取り専用 API）
    public static class GmlParser
    {
        public static IReadOnlyList<GeoPolygon> ParseString(string gml);          // 形状のみ（既存挙動）
        public static IReadOnlyList<GeoPolygon> ParseStream(Stream stream);
        public static IReadOnlyList<GmlFeature> ParseFeaturesString(string gml);  // 形状＋属性
        public static IReadOnlyList<GmlFeature> ParseFeaturesStream(Stream stream);
    }
    public sealed record GmlFeature(GeoPolygon Polygon, IReadOnlyDictionary<string, string> Attributes);

    public readonly record struct GeoCoordinate(double Latitude, double Longitude);
    public sealed class GeoPolygon { /* 緯度経度頂点列 */ }
    public readonly record struct MeshCode(long Value) { /* 8〜11 桁の数値。桁数で階層を自動判定（REQ-RST-016） */ }
    public enum MeshLevel { Mesh3rd /* 1km */, HalfMesh /* 500m */, QuarterMesh /* 250m */, EighthMesh /* 125m */ }
    public readonly record struct MapBounds(GeoCoordinate SouthWest, GeoCoordinate NorthEast) { /* GML 入力フィルタ等のマップ範囲、境界含む Contains 提供 */ }
    public sealed class Route {
        /* TotalDistanceM, TotalDurationSec, Shape: ReadOnlyMemory<GeoCoordinate>,
           CumulativeDurationsSec: ReadOnlyMemory<double> (REQ-FMT-006、Ver 1.1.0) */
    }
}
```

#### プロファイル JSON スキーマ概要（同梱 `car.json` 抜粋）

```jsonc
{
  "name": "car",
  "vehicleType": "motor_vehicle",
  "accessTagKeys": ["motor_vehicle", "vehicle", "access"],
  "highway": {
    "motorway":      { "speedKmh": 100, "access": "yes" },
    "primary":       { "speedKmh": 60 },
    "residential":   { "speedKmh": 30 },
    "footway":       { "access": "no" }
  },
  "accessValueMap": { "yes": "allow", "no": "deny", "private": "deny" },
  "fallback": { "speedKmh": 30, "access": "no" },
  "difficulty": {
    "flooding":     { "speedFactor": 0.3, "canPass": true  },
    "liquefaction": { "speedFactor": 0.5, "canPass": true  },
    "landslide":    { "speedFactor": 0.0, "canPass": false },
    "construction": { "speedFactor": 0.2, "canPass": true  },
    "obstacle":     { "speedFactor": 0.5, "canPass": true  },
    "congestion":   { "speedFactor": 0.4, "canPass": true  },
    "snow":         { "speedFactor": 0.4, "canPass": true  },
    "ice":          { "speedFactor": 0.3, "canPass": true  }
  },
  "difficultyDefault": { "speedFactor": 1.0, "canPass": true }
}
```

### 7.2 関連要件

- REQ-API-001〜REQ-API-008
- REQ-FMT-001〜REQ-FMT-005
- REQ-RTE-001〜REQ-RTE-008
- REQ-RST-001〜REQ-RST-041
- REQ-PRF-001〜REQ-PRF-016

---

## 8. データフォーマット詳細

### 8.1 グラフ入力フォーマット

| Phase | 入力データ（ランタイム） | データ供給経路 | 関連要件 |
|---|---|---|---|
| Phase 1 | Itinero RouterDb (`.routerdb`) | Itinero `.routerdb` を直接読込 | REQ-MAP-001 |
| Phase 2 | （ランタイム変化なし） | **OSM PBF → `.odrg` 独自抽出ツール** で `.odrg` を生成（ランタイムは Phase 3 で `.odrg` 読込開始） | REQ-MAP-003 / REQ-MAP-007 / REQ-MAP-008 |
| Phase 2-opt | — | （オプション）Itinero RouterDb → `.odrg` 変換ツールを設計完了後に検討 | REQ-MAP-004 |
| Phase 3 | OsmDotRoute 独自バイナリグラフ `.odrg` | `MemoryMappedFile` ビューで直接利用 | REQ-MAP-005 / REQ-MAP-006 / REQ-MAP-009 |

### 8.1.b 動的制約入力フォーマット

| 入力形式 | 内容 | 関連要件 |
|---|---|---|
| `GeoPolygon` メモリオブジェクト | 緯度経度頂点列 | REQ-RST-001, REQ-RST-004 |
| `MeshCode` メモリオブジェクト | JIS X0410 第3次〜1/8 細分（1km〜125m、8〜11 桁） | REQ-RST-002〜006, REQ-RST-016〜018 |
| GML 文字列 / ファイル / Stream | 国土数値情報 KSJ アプリケーションスキーマ準拠 GML 3.2（動作確認: A31「浸水想定区域」、制約登録 API は形状のみ抽出、難所タイプは API 引数で指定。フィーチャ属性が必要な場合は `GmlParser.ParseFeatures*` で形状＋属性を取得し利用者側で振り分け） | REQ-RST-020〜028, REQ-RST-041 |

#### GML 入力 API の難所タイプ・タグ指定方針

制約登録 API（`Add*FromGml*`）はフィーチャ属性（`<ksj:waterDepth>` 等）を保持しない。難所タイプとタグはともに利用者が API 引数で指定する（属性に基づく振り分けが必要な場合は `GmlParser.ParseFeatures*`＝REQ-RST-041 でフィーチャ別に取得し、利用者側で `AddBlockArea` / `AddDifficultyArea` に渡す）:

| 指定対象 | 指定方法 | 関連要件 |
|---|---|---|
| 難所タイプ | `AddDifficultyAreaFromGml*` の `difficultyType` 引数（全フィーチャに同一適用） | REQ-RST-026 |
| タグ | `AddBlockAreaFromGml*` / `AddDifficultyAreaFromGml*` の `tag` 引数（全フィーチャに同一適用） | REQ-RST-027 |

### 8.1.c プロファイル定義ファイル（JSON）

| 入力形式 | 内容 | 関連要件 |
|---|---|---|
| 埋込リソース | 同梱 `Profiles/car.json`, `Profiles/pedestrian.json` | REQ-PRF-008 |
| ファイルパス | ユーザー定義 JSON プロファイル | REQ-PRF-009 |
| 文字列 / Stream | ユーザー定義 JSON プロファイル（テストや動的生成用） | REQ-PRF-009 |

スキーマは §7.1 の `car.json` 抜粋を参照。

### 8.1.d 難所タイプ規定値

| キー | 日本語名 | 想定用途 |
|---|---|---|
| `flooding` | 冠水 | 河川氾濫・内水氾濫・津波後の冠水 |
| `liquefaction` | 液状化 | 地震による液状化現象 |
| `landslide` | 土砂崩れ | 崖崩れ・地すべり・落石（道路寸断） |
| `construction` | 工事中 | 道路工事・復旧工事による通行困難 |
| `obstacle` | 障害物 | 瓦礫・倒木・落下物・放置車両等 |
| `congestion` | 交通集中 | 避難集中・通常混雑による速度低下 |
| `snow` | 積雪 | 降雪後の未除雪区間 |
| `ice` | 凍結 | 路面凍結によるスリップリスク |

### 8.2 出力フォーマット

| 種別 | 内容 | 関連要件 |
|---|---|---|
| `OsmDotRoute.Route` 型 | 総距離・総所要時間・経路形状・Shape 点別累積所要秒（`CumulativeDurationsSec`、Ver 1.1.0〜） | REQ-FMT-001 〜 REQ-FMT-003、REQ-FMT-006 |
| ~~GeoJSON LineString~~ | ~~経路の地図表示用~~ | ~~REQ-FMT-004~~（**廃止・v1.7**、利用者側で `Route.Shape` から数行で変換可能なため YAGNI 判断、設計書 §13 参照） |
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

**スコープ**: REQ-RTE-001〜008, REQ-RST-001〜032, REQ-PRF-001〜002, REQ-PRF-007〜014, REQ-MAP-001〜002, REQ-API-001〜006, REQ-API-008, REQ-FMT-001〜004, REQ-NFR-001〜003, REQ-NFR-005〜006, REQ-NFR-009〜010, REQ-PKG-001, REQ-LIC-001〜003, REQ-DEP-001

**完了判定**:
- ~~親プロジェクトの `MapService.cs` から `using Itinero` を完全に消去できる~~ → **Phase 3 完了後に実証へ延期**（2026-05-20、親プロ `ScenarioEditorService.GenerateRouterDbAsync` が `Itinero.IO.Osm` PBF パーサーに依存しているため、Phase 1 段階での完全消去は技術的に困難。実装計画書 §8 ステップ 16 / 設計書 §17 参照）
- 既存の `CalculateRoute` / `SnapToRoad` / `GetRoadNetworkGeoJson` 相当機能が動作（OsmDotRoute 公開 API として実装済、MapVerifier で検証済）
- 動的制約の追加削除が次回経路計算で反映される（REQ-RST-012、実装済）
- ベンチマーク結果が REQ-NFR-001 を満たす（市単位で達成、都道府県単位は Phase 3 完了後に再ベンチ）

**公開アクション**: Phase 1 完了後、親プロジェクトに `<ProjectReference>` で組み込み（**Phase 3 完了まで延期**、2026-05-20）

### Phase 2: 中間グラフフォーマット

**目標**: ランタイムから Itinero 依存を削除。OsmDotRoute 単体で配布可能な状態に。

**スコープ**: REQ-PRF-003〜004, REQ-MAP-003〜006, REQ-PKG-002, REQ-DEP-002

**完了判定**:
- ランタイムから Itinero アセンブリ参照を削除できる（REQ-MAP-006, REQ-DEP-002）
- 独自フォーマットでも REQ-NFR-001 の性能要件を満たす

### Phase 3: OSM PBF パーサー独自化

**目標**: Itinero への完全独立。GitHub 公開可能な状態に。

**スコープ**: REQ-MAP-007〜009, REQ-PKG-003, REQ-LIC-004, REQ-DEP-003（REQ-PRF-005〜006 は Phase 4 に移動）

**完了判定**:
- ライブラリ全体から Itinero 依存が無い（REQ-MAP-009, REQ-DEP-003）
- GitHub 公開準備完了（LICENSE、README、CI 整備）

**公開アクション**: GitHub 個人アカウントで OSS 公開

### Phase 4（着手中、2026-06-03〜）

**スコープ（2026-06-02 ユーザー決定で 2 項目に限定、2026-06-09 親プロFB 追補で 1 項目追加）**:
- **プロファイル追加**: REQ-PRF-005〜006（emergency / disaster）＋ユーザー定義プロファイル拡充
- **マルチプラットフォーム対応**: REQ-NFR-007（Linux / macOS）の検証本格化【完了 2026-06-03、Windows/Linux/macOS で 753 pass】。REQ-NFR-008（.NET バージョン横断）は本スコープ外で Phase 4+ 継続
- **親プロFB 追補**: REQ-FMT-006（Route.CumulativeDurationsSec、Ver 1.1.0）/ REQ-PRF-014 改訂・REQ-PRF-017 追加（難所タイプ照合 case-insensitive 化＋観測性 API、Ver 1.1.1）/ REQ-RST-016 仕様確定・REQ-RST-041 追加（1/8 細分メッシュ 125m・11 桁対応＋GmlParser フィーチャ属性公開、Ver 1.2.0）/ REQ-MAP-010 追加（RouterDb の IDisposable 実装＝リソース確定解放、Ver 1.2.1、[`feature_request_routerdb_dispose.md`](feature_request_routerdb_dispose.md)）/ REQ-RTE-010〜013 追加（範囲境界との交点・交点からの距離／所要時間算出 API、Ver 1.3.0）/ REQ-RST-030 改訂（難所係数の積は「エリア単位」＝1 エリアが複数 Shape を持ってもエッジあたり 1 回、Ver 1.3.1、[`bug_request_difficulty_factor_per_shape.md`](bug_request_difficulty_factor_per_shape.md)）。親プロジェクト「災害廃棄物処理シミュレーション」からの区間別速度低下アニメーション要望（[`feature_request_per_segment_durations.md`](feature_request_per_segment_durations.md)）、その検証中に発覚した不具合報告、KSJ ハザードデータ取り込み計画の前提要望（[`feature_request_mesh_level8_and_gml_attributes.md`](feature_request_mesh_level8_and_gml_attributes.md)）を取り込み

### Phase 4 以降（将来検討）

- REQ-NFR-004 (CH 対応), REQ-NFR-011 (グローバル対応)
- REQ-RTE-009 (高速化アルゴリズム)
- REQ-RST-019 (第1次・第2次メッシュへの拡張。REQ-RST-016 の細分側拡張は 2026-06-11 改訂で 1/8 細分まで完了)
- REQ-PRF-015〜016 (プロファイル C# 拡張 API、Lua 互換層)
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
| **Profile（車両プロファイル）** | 車両種別ごとの通行可否・速度を OSM タグから決定する設定。本プロジェクトでは JSON 外部ファイルで定義（リビルド不要） |
| **FactorAndSpeed** | Itinero の `Profile` が返すエッジ毎の重み係数と速度のペア |
| **Edge / Vertex** | グラフの辺と頂点。OSM では辺=道路セグメント、頂点=交差点 |
| **Shape** | エッジの中間座標列。曲がった道路を表現するための補助点 |
| **RouterPoint** | 任意座標を道路ネットワーク上にスナップした結果点 |
| **ポリゴン** | 緯度経度頂点列で定義される多角形。外周＋Hole（穴）を持ちうる |
| **進入不可エリア (BlockArea)** | 経路探索でエッジが通過不可と扱われるポリゴン領域 |
| **難所エリア (DifficultyArea)** | 「客観的事実（道路状況種別）」を登録する制約。速度低下係数・通行可否はプロファイル側で規定する |
| **難所タイプ (Difficulty Type)** | 道路状況の客観的種別を示す文字列キー。組込み 8 種（flooding/liquefaction/landslide/construction/obstacle/congestion/snow/ice）と任意のユーザー定義キー |
| **動的制約** | ランタイム中に追加・削除・変更可能な通行制約 |
| **AABB** | Axis-Aligned Bounding Box。ポリゴン外接矩形。事前フィルタに使用 |
| **CH** | Contraction Hierarchies。経路計算高速化手法。Phase 4 以降で検討 |
| **GeoJSON** | 地理データを JSON で記述する標準フォーマット（RFC 7946）。座標系は WGS84 経度・緯度の順。本ライブラリでは**出力フォーマット**として使用（経路 LineString、道路ネットワーク FeatureCollection）。動的制約**入力**は GML（KSJ）を使う |
| **GML 3.2** | Geography Markup Language。OGC 標準の XML ベース地理データ記述形式（ISO 19136）。本ライブラリでは動的制約**入力**フォーマットとして KSJ プロファイルを採用 |
| **KSJ (国土数値情報)** | 国土交通省国土政策局が提供する地理情報データセット群。本ライブラリの動的制約入力は KSJ アプリケーションスキーマ準拠の GML を採用（A31「浸水想定区域」等） |
| **`<ksj:Dataset>`** | KSJ GML ファイルのルート要素。配下に `<gml:Curve>`、`<gml:Surface>`、フィーチャ要素（`<ksj:ExpectedFloodArea>` 等）が並ぶ |
| **`<gml:Surface>`** | GML 3.2 でポリゴン領域を表す要素。`<gml:exterior>`（外周）と `<gml:interior>`（Hole）を持つ |
| **`<gml:Curve>`** | GML 3.2 で曲線（リング）を表す要素。`<gml:posList>` に座標列を「緯度 経度」順で含む（KSJ 規定） |
| **`xlink:href` 参照** | GML で要素間の ID 参照に使う仕組み（例: `<gml:curveMember xlink:href="#c00001"/>`）。Surface ↔ Curve、フィーチャ ↔ Surface の関連付けに使用 |
| **メッシュ** | 地理空間を格子状に区切った領域 |
| **JIS X0410** | 「地域メッシュ統計のための地域区分」を規定した JIS 規格。第1次（80km）/ 第2次（10km）/ 第3次（1km）および細分メッシュを定義 |
| **地域メッシュコード** | JIS X0410 で各メッシュに割り当てられた数値コード。第3次（1km、8桁）/ 1/2 細分（500m、9桁）/ 1/4 細分（250m、10桁）/ 1/8 細分（125m、11桁、象限方式）の 4 階層に対応（REQ-RST-016） |
| **SemVer** | Semantic Versioning。`MAJOR.MINOR.PATCH` 形式の互換性保証付きバージョニング |

---

## 12. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| （採番待ち） | 2026-08-18 | **REQ-RST-030 改訂（難所 speedFactor の積は「エリア単位」、Ver 1.3.1、親プロFB 不具合修正）**。親プロジェクトから「複数 shape を持つ移動困難エリアを 1 個登録すると、エッジが跨いだ shape 数だけ `speedFactor` が累乗され、実測で ×604（0.4^7）／×315,135（0.4^14）に達し事実上の通行不能になる」不具合報告（[`bug_request_difficulty_factor_per_shape.md`](bug_request_difficulty_factor_per_shape.md)、P1）を受領。原因は `RestrictedAreaEdgeCache.AddDifficulty` が同一 `(areaId, edgeId)` を無条件に `_difficultyAreasByEdge` の `List` へ追加していたことで、bake 経路（`IRoadGraph` 注入時）のみがフォールバック経路（`EvaluateConstraints`、`seenIds` で ID 単位に重複排除）と食い違う自己矛盾状態だった。修正は `_difficultyByArea` の `HashSet.Add` 戻り値による重複判定 1 行のみ（公開 API・シリアライズ形式・単一 Shape エリアの挙動はいずれも不変、異なるエリア間の積は従来どおり維持）。回帰テスト 7 件追加（多メッシュ 1 エリアで係数 1 回／メッシュ集合とポリゴン 1 枚の一致／bake 経路とフォールバック経路の結合係数一致／異エリア重複時の積の非回帰／`Remove` 後の復帰／キャッシュ単体 2 件）、全 837 pass（v1.3.0 末の 830 から +7、回帰ゼロ）。修正前は追加 7 件中 5 件が失敗することを確認済。バージョン 1.3.1（パッチ採番、ユーザー指定） | Claude (Opus 5) |
| （採番待ち） | 2026-08-07 | **REQ-RTE-010〜013 追加（範囲境界との交点・距離算出 API、Ver 1.3.0）**。災害シミュレーションの「対象範囲の外にある搬出先まで、範囲境界からどれだけ走るか」を求める用途に対応。`Router.CalculateBoundaryCrossing(profile, from, to, bounds, searchDistanceM)`（探索込み）と `Router.FindBoundaryCrossing(route, from, to, bounds)`（計算済み Route への純幾何版、REQ-RTE-013）を追加、結果は `BoundaryCrossingResult`（`BoundaryCrossingKind` + 交点 + 距離 + 所要秒）で返す。仕様確定事項: ①範囲 R は北西端・南東端指定を `MapBounds.FromNorthWestSouthEast` で受け、地図内側の任意の矩形でよい ②多重交差時は**範囲内側の端点に近い側の交点**を採り、距離は途中で範囲内に戻る区間も含む ③内外判定は生の座標（境界線上は範囲内） ④交点は矩形辺との厳密な線形補間、距離は Shape 頂点列の Haversine 積算、所要秒は `CumulativeDurationsSec` の t 補間 ⑤異常系は例外・`null` ではなく `RouteSearchError` / `InvalidParameter` の列挙値で報告（端点判定とルート形状の矛盾も `RouteSearchError`）。新規テスト 28 件（純幾何 21 + 実データ 7）、全 830 pass（v1.2.1 末の 802 から +28、回帰ゼロ）。バージョン 1.3.0（マイナー採番、公開 API 追加のみ、ユーザー指定） | Claude (Opus 5) |
| （採番待ち） | 2026-06-12 | **REQ-MAP-010 追加（RouterDb の IDisposable 実装、親プロFB 対応）**。親プロジェクトの「既存シナリオの道路データ再生成 → 上書き保存」が、ファイル版 `LoadFromOdrg` の MMF ハンドル残留ロックにより必ず IOException で失敗する実バグ（P1）への対応要望（[`feature_request_routerdb_dispose.md`](feature_request_routerdb_dispose.md)）を受領。`RouterDb : IDisposable` を実装（`Dispose() => _graph.Dispose()` の委譲のみ、`OdrgMmfHandle.Dispose` が MMF/ViewAccessor・ピン留めバッファを冪等解放）。受け入れ基準 5 項目（①Dispose 後の File.Delete/Copy 成功 ②冪等性 ③Dispose 後使用は ObjectDisposedException ④メモリ版も解放 ⑤非破壊）を検証するテスト 9 件追加、全 802 pass（v1.2.0 末の 793 から +9、回帰ゼロ）。§7.1 API スケッチ更新。バージョン 1.2.1（パッチ採番、ユーザー指定） | Claude (Fable 5) |
| （採番待ち） | 2026-06-11 | **REQ-RST-016 仕様確定（1/8 細分メッシュ）+ REQ-RST-041 追加（GmlParser フィーチャ属性公開）（Ver 1.2.0、親プロFB 追補）**。親プロジェクトの KSJ ハザードデータ取り込み計画（A31a/A31b/A33/A51/A53 → 125m メッシュラスタライズ → `AddBlockArea/AddDifficultyArea` 登録）の前提要望（[`feature_request_mesh_level8_and_gml_attributes.md`](feature_request_mesh_level8_and_gml_attributes.md)）に対応。①REQ-RST-016: 11 桁目 = 象限 1〜4 の「1/8 細分（125m）」を正式仕様として確定（v1.4 で延期した「1/10 細分 = 100m」は既存 1/4 細分と桁数衝突するため象限方式に読み替え）。`MeshLevel.EighthMesh` 追加、`MeshCode.Level` / `ToBounds` / `EnumerateInBounds` / `RestrictedAreaService.AddBlockArea/AddDifficultyArea(IEnumerable<MeshCode>)` が 11 桁を 8〜10 桁と同等に処理（既存 API シグネチャ変更なし、`MeshCodeConverter` の細分処理を象限再帰ループに一般化）。②REQ-RST-041: `GmlParser` を公開化し `ParseFeaturesString/Stream` → `IReadOnlyList<GmlFeature>`（形状＋属性 Dictionary）を追加。A51（GML のみ提供）の浸水深ランク等に基づく制約レベル振り分けを利用者側で可能に。既存 `ParseString/ParseStream` / `Add*FromGml*` は挙動不変。全 793 pass（v1.1.1 末の 777 から +16、回帰ゼロ）。Sandbox のメッシュグリッド表示にも 125m 階層を追加（Server / WASM / Web UI）。バージョン 1.2.0（マイナー採番、公開 API 追加のみ） | Claude (Fable 5) |
| （採番待ち） | 2026-06-09 | **REQ-PRF-014 改訂 + REQ-PRF-017 追加（Ver 1.1.1、親プロFB 不具合修正）**。親プロジェクト（v1.1.0 アニメ目視検証中）から「難所タイプ照合が case-sensitive のため `"Flooding"` 等の表記揺れで速度低下がサイレントに無効化される」不具合報告を受領（[`debug_flooding_x10_for_animation_verification.md`](debug_flooding_x10_for_animation_verification.md) 経由の往復で発覚）。`ProfileEvaluator.EvaluateDifficulty` の照合を Ordinal-IgnoreCase 化（REQ-PRF-014 改訂）、case-only 重複キーを `InvalidProfileException` で拒否、観測性 API `VehicleProfile.KnownDifficultyTypes` / `HasDifficulty(string)` を新規追加（REQ-PRF-017）。`DifficultyTypes` / `RestrictedAreaService` / `Route.CumulativeDurationsSec` の XML doc に「サイレント・フォールバック」挙動を明記。全 777 pass（v1.1.0 末の 761 から +16、回帰ゼロ）。バージョン 1.1.1（パッチ採番） | Claude (Opus 4.7) |
| （採番待ち） | 2026-06-09 | **REQ-FMT-006 追加 / Phase 4 親プロFB 追補（Ver 1.1.0）**。親プロジェクト「災害廃棄物処理シミュレーション」からの区間別速度低下アニメーション要望（`Documents/feature_request_per_segment_durations.md`）に応えて `Route.CumulativeDurationsSec`（`ReadOnlyMemory<double>`、Shape 点別累積所要秒）を追加。実装は `DijkstraResult.VertexCumulativeDurationsSec` 追加 + `RouteBuilder` でエッジ内多角線距離按分による補間。Phase 4 スコープに「親プロFB 追補」枠を追加（§9 Phase 4 第 3 ブレット）、§7.1 API シグネチャ概要・§8.2 出力フォーマット表は §5.6.a を参照。全 761 pass（既存 753 + 不変条件テスト 6 + 別途追加分、回帰ゼロ）。バージョンはユーザー採番（1.1.0 マイナー） | Claude (Opus 4.7) |
| （採番待ち） | 2026-06-03 | **Phase 4 マルチプラットフォーム対応完了**。REQ-NFR-007（Linux / macOS）を完了マーク。3H で構築した macOS CI 自動ミラー基盤（`mirror.yml` / `.mirror/ci-macos.yml`）を Phase 4 成果を保ったまま main へ移植し、自動同期を有効化。**macOS ARM64（GitHub Actions ミラー `OsmDotRoute-ci-macos`）・Linux x64（WSL2 + CI）とも Phase 4 = 753 pass / 0 fail / 0 skip**、配布 3 本の pack も警告ゼロを確認。REQ-NFR-008（.NET バージョン横断）は本スコープ外で継続。詳細は [phase4_multiplatform_plan.md](phase4_multiplatform_plan.md)。バージョンはユーザー採番 | Claude (Opus 4.8) |
| （採番待ち） | 2026-06-03 | **Phase 4 プロファイル追加完了**。REQ-PRF-005 を救急車 `ambulance`（小型）+ 消防車 `fire_engine`（大型）の 2 プロファイルに分割して完了マーク（ID は分割せず両プロファイルに充当）、REQ-PRF-006 災害用 `disaster`（難所耐性中心）を完了マーク。Extractor CLI `--profiles` の外部 JSON プロファイル対応で REQ-PRF-009 を bake 経路へ拡張（`ProfileResolver`）。全 753 pass（Phase 3 末 693 → +60、回帰ゼロ）。設計は [phase4_design.md](phase4_design.md) §2、利用手順は [profile_guide.md](profile_guide.md) 参照。バージョンはユーザー採番 | Claude (Opus 4.8) |
| 0.1 (draft) | 2026-05-18 | 初版ドラフト作成 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-18 | 要件 ID（REQ-XXX-NNN）形式に再構成、ジャンル別整理、Phase/Ver 記法導入、地域メッシュコード対応反映 | Claude (Opus 4.7) |
| 1.0 (確定) | 2026-05-18 | 車両プロファイル Phase 分割確定、メッシュ階層 1km〜100m の 4 階層対応確定、ユーザー合意済み | Claude (Opus 4.7) |
| 1.1 (確定) | 2026-05-18 | 動的制約入力に GeoJSON（Polygon / MultiPolygon / FeatureCollection、Hole 対応、RFC 7946 準拠）を追加（REQ-RST-020〜029） | Claude (Opus 4.7) |
| 1.2 (確定) | 2026-05-18 | プロファイル外部 JSON ファイル化（REQ-PRF-007〜010、リビルド不要要件）、難所エリア導入（REQ-PRF-011〜014、REQ-RST-004〜007 を移動困難エリア → 難所エリアに変更、組込み 8 タイプ、ユーザー定義可、重複時は積・短絡）、API 変更（`AddSlowArea` 削除、`AddDifficultyArea` 追加、`VehicleProfile` enum → class）、GeoJSON プロパティ `speedFactor` → `difficulty` 変更（REQ-RST-026）、難所重複ルール追加（REQ-RST-030〜032）、§7.1 API シグネチャ更新、§8.1.c プロファイル定義ファイル節と §8.1.d 難所タイプ規定値表追加、用語集更新 | Claude (Opus 4.7) |
| 1.3 (確定) | 2026-05-18 | §7.1 API: `RouterDb.LoadFromFile` を削除し、`OsmDotRoute.Itinero.ItineroRouterDbLoader.LoadFromFile` / `FromItineroRouterDb` に移動。アセンブリ依存方向（コア ← アダプター）維持のため。Phase 1 ステップ 3 実装で確定 | Claude (Opus 4.7) |
| 1.4 (確定) | 2026-05-18 | REQ-RST-016 のメッシュ階層を 4 → 3 に縮小（1/10 細分 = 100m / 11 桁 を Phase 2 以降へ延期）。11 桁エンコーディング仕様が JIS X0410 cascade と整合しないため、親プロジェクト「災害廃棄物処理シミュレーション」と同範囲（8〜10 桁）に揃える。MeshLevel.TenthMesh を enum から削除。Phase 1 ステップ 7 実装で確定 | Claude (Opus 4.7) |
| 1.5 (確定) | 2026-05-19 | 動的制約入力フォーマットを GeoJSON → 国土数値情報 KSJ アプリケーションスキーマ準拠 GML 3.2 に変更。REQ-RST-020〜029 を全面書き換え、§5.2.f 見出しを「GML 入力対応」に改題、§7.1 API シグネチャを `AddFromGeoJson*` (3 メソッド) → `AddBlockAreaFromGml*`/`AddDifficultyAreaFromGml*` (6 メソッド) に置換、§8.1.b 入力フォーマット表更新（GeoJSON Properties 規定キー表を削除し、GML 入力 API の難所タイプ・タグ指定方針表に置換）。難所タイプはユーザー API 引数指定（フィーチャ要素名からの自動判定はしない、複数 KSJ プロダクト共通基盤のため）、ハザード属性は保持せず形状のみ抽出。`<gml:MultiSurface>` 対応は Phase 2 へ延期（A31 サンプル 1.6GB で出現 0 件を確認）。汎用 GML / GeoJSON / Shapefile / TopoJSON 等の他形式対応は REQ-RST-029 で「要望が出た時点で個別判断」に統合。Phase 1 ステップ 10 実装で確定予定 | Claude (Opus 4.7) |
| 1.6 (確定) | 2026-05-19 | GML 入力 API にマップ範囲フィルタを追加（REQ-RST-040、新規 P1）。`MapBounds` 公開値型を新設し、GML 入力 6 メソッドに optional `MapBounds? mapBounds = null` 引数（`difficultyType` の後・`tag` の前）を挿入。指定時はフィーチャ外周頂点が 1 つでも範囲内（境界線上含む）にあるフィーチャのみ採用、0 個はスキップ。未指定 (`null`) 時は全フィーチャ採用（互換）。シミュレーションのマップ範囲外フィーチャを自動除外するための機能で、利用者は `RouterDb.GetStatistics()` で得た範囲をそのまま渡せる。Phase 1 ステップ 10 実装で確定 | Claude (Opus 4.7) |
| 1.7 (確定) | 2026-05-19 | REQ-FMT-004「経路 → GeoJSON LineString 変換ユーティリティ」を**廃止**。親プロジェクトの実需要が不明確で、利用者側で `Route.Shape: IReadOnlyList<GeoCoordinate>` から数行で GeoJSON 化可能なため YAGNI 判断（ユーザー合意 2026-05-19）。Phase 1 ステップ 11 を廃止扱いとし、ステップ 12 以降に直接進む。§8.2 出力フォーマット表の該当行も廃止表記。要望が出た時点で再評価する（設計書 §13 に検討経緯を記録） | Claude (Opus 4.7) |
| 1.8 (確定) | 2026-05-19 | Phase 1 ステップ 12 完了反映：REQ-API-005（DI 拡張 `AddOsmDotRoute`）、REQ-API-006（XML doc 完備）、REQ-API-008（README に 0.x 破壊的変更方針明記）、REQ-PKG-001（ProjectReference 参照確立）、REQ-LIC-001（MIT License）を完了マーク。新規 csproj `OsmDotRoute.Extensions.DependencyInjection`（`Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0 のみ依存）を追加。`Directory.Build.props` の `GenerateDocumentationFile` を `true` に切替（テスト/ベンチマーク/サンプル csproj で個別に `false` 上書き）。`README.md` を Phase 0 → Phase 1 進行中の内容に全面書き換え（最小サンプル・DI 統合・動的制約登録例・Phase ロードマップ）。6 プロジェクト・147/147 テスト・0 警告維持。設計書 §14「DI 拡張とドキュメント」を実装済みに記述（§2.2/2.4/3.2/3.4 にも追記） | Claude (Opus 4.7) |
| 2.2 (確定) | 2026-05-20 | **Phase 1 ステップ 17 (ユーザー検証) 完了** (Ver. 0.21)。MapVerifier 1.0.0 で検証チェックリスト 32/32 をユーザーが全件 OK 確認 (`Documents/phase1_step17_verification_checklist.md`)。REQ-NFR-001〜003 を「条件付き完了」コメント付き [x] に確定マーク (市単位達成、都道府県単位は Phase 3 完了後に再検証)。Phase 1 機能要件＋性能要件全件達成、設計書 §18 「制約事項と既知の課題」も初版記述済。Phase 1 残作業は v0.1.0 タグ付与のユーザー判断のみ | Claude (Opus 4.7) |
| 2.3 (確定) | 2026-05-20 | **Phase 2/3 のスコープを再編**。ユーザー判断「Itinero RouterDb からの変換ツールを意識すると `.odrg` 構造が最適化できなくなる懸念、OSM PBF からの直接抽出を Phase 2 主軸に据える」を反映 (2026-05-20)。Phase 2 = 独自バイナリグラフ形式 `.odrg` 策定 + 独自 OSM PBF パーサー (System.\* 完結) + PBF→`.odrg` 抽出ツール `OsmDotRoute.Extractor` + (末尾オプション) RouterDb 変換ツール。Phase 3 = ランタイム `.odrg` 読込 + ランタイム Itinero 依存削除 + Bicycle/Truck プロファイル + ベンチマーク + 親プロジェクト統合・パリティ検証。Phase タグ付け替え: REQ-MAP-004 を P1[Phase2] → P3[Phase2-opt]、REQ-MAP-005/006 を Phase 2 → Phase 3、REQ-MAP-007/008 を Phase 3 → Phase 2、REQ-PRF-003/004 を Phase 2 → Phase 3、REQ-PKG-002 を Phase 2 まで → Phase 3 完了まで非公開、REQ-DEP-002/003 を再構成 (Phase 2 で System.\* 完結を確定、protobuf 独自実装、変換ツール内部 Itinero 許容条項を削除)。Truck=10t トラックを独自設計と確定 (REQ-PRF-004)。§2.2 実装フェーズ説明、§8.1 グラフ入力フォーマット表を新スコープに合わせて全面更新 | Claude (Opus 4.7) |
| 2.1 (確定) | 2026-05-20 | **Phase 1 ステップ 16 (親プロジェクト統合・パリティ検証) を Phase 3 完了まで延期** (Ver. 0.20)。事前調査で親プロ `ScenarioEditorService.GenerateRouterDbAsync` が `Itinero.IO.Osm` の PBF パーサーに依存していることが判明、Phase 1 段階で `using Itinero` 完全消去は技術的に困難なため。ユーザー判断「Phase 3 完了まで親プロジェクトを変更しない」（2026-05-20）。§11 Phase 1 完了判定の「親プロ `using Itinero` 消去」を Phase 3 完了後の実証へ延期と明記、関連項目を更新。Phase 1 残作業はステップ 17 (MapVerifier 手動検証・REQ-ID 完了マーク・v0.1.0 タグ) のみ | Claude (Opus 4.7) |
| 2.0 (確定) | 2026-05-20 | **Phase 1 ステップ 15 ベンチマーク完了** (Ver. 0.19)。REQ-NFR-001〜003 に「条件付き完了」コメントを追記（チェックは `[ ]` のまま、ステップ 17 で都道府県単位再検証後に [x] 確定）。市単位（津島市、57k エッジ）での計測結果: 経路計算 33ms / Itinero 比 0.48x / 制約 100 件下 51ms / WorkingSet 54MB。全判定基準を達成（経路距離同等性 89/89 ペアで ±10% 以内、OsmDotRoute-only 経路発見 8 件、Itinero-only 0 件）。詳細は [phase1_benchmark_results.md](phase1_benchmark_results.md) と設計書 §16 参照。残作業は REQ-NFR-001〜003 の都道府県単位最終確認（ステップ 17）と Phase 2+ 延期項目のみ | Claude (Opus 4.7) |
| 1.9 (確定) | 2026-05-19 | **Phase 1 機能要件全件を完了マーク** (Ver. 0.18)。MapVerifier 1.0.0 (初版リリース) を通じた end-to-end 検証で、ユーザーが「機能が動作しているのを確認」と承認したことを反映。完了マーク対象: REQ-RTE-001〜008 (経路探索・スナップ・道路ネットワーク GeoJSON 全 8 件)、REQ-RST-001〜018 (進入不可・難所・削除・一覧・即時反映・空間判定・メッシュ階層 全 18 件)、REQ-RST-020〜022 / 024〜028 (KSJ GML 入力 全 8 件)、REQ-RST-030〜032 (重複ルール 3 件)、REQ-RST-040 (マップ範囲フィルタ)、REQ-PRF-001〜002 (車両 2 プロファイル)、REQ-PRF-007〜014 (JSON プロファイル基盤 + 難所タイプ 8 件)、REQ-MAP-001〜002 (RouterDb 読込・統計)、REQ-API-001〜004 (Router/RouterDb/Route/RestrictedAreaService ファサード)、REQ-FMT-001〜003 (Route フィールド)、REQ-DEP-001 (Itinero+System.* のみ)、REQ-LIC-002〜003 (Apache コピー禁止・NuGet バイナリのみ)、REQ-NFR-005〜006 (.NET 9 + Windows x64)、REQ-NFR-009〜010 (日本国内+メートル法)。未完了として残るのは REQ-NFR-001〜003 (性能要件、Phase 1 ステップ 15 ベンチマーク待ち)、REQ-RST-019/023/029 等 Phase 2+ 延期項目、および将来 Phase 用 (REQ-RTE-009/PRF-003〜006/MAP-003〜009/API-007/FMT-005/PKG-002〜004/LIC-004/DEP-002〜003/NFR-004/007〜008/011)。検証手段の MapVerifier はサンプルアプリとして独自 SemVer 管理 (現 v1.0.0 初版リリース)、設計書 `map_verifier_design.md` 参照 | Claude (Opus 4.7) |

---

## 13. 次のアクション

- [x] ユーザーレビュー
- [x] 各要件 ID へのフィードバック・修正反映
- [x] **メッシュ階層対応範囲**の確定（REQ-RST-016、Phase 1 で 1km / 500m / 250m の 3 階層対応。100m は v1.4 で Phase 2 以降へ延期）
- [x] **親プロジェクト側の Phase 1 希望時期**: 気にしない方針で確定（Phase 1 の所要時間が短いと見込まれるため、独自スケジュールで進める）
- [x] ステータスを「ドラフト」から「確定」に変更
- [ ] Phase 1 開始前に `git init` の実施
- [ ] Phase 1 実装計画書（`phase1_implementation_plan.md`）の作成
