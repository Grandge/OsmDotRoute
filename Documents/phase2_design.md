# OsmDotRoute Phase 2 設計書

**バージョン**: 0.2.2（ひな形・ステップ 2.3 完了反映）
**作成日**: 2026-05-20（v0.1 として）
**最終更新**: 2026-05-21（v0.2.2、§4.2 ステップ 2.3 完了反映）
**ステータス**: ひな形（Phase 2 実装計画書 v0.2 と並行作成、各章は対応ステップ完了時に肉付け）
**対象**: OsmDotRoute Phase 2 実装の設計記録（**データ供給側**：`.odrg` 策定 + 独自 PBF パーサー + PBF→`.odrg` 抽出）
**関連ドキュメント**:

- [要件定義書](requirement_definition.md)（v2.3、Phase 2/3 スコープ再編後）
- [Phase 2 実装計画書](phase2_implementation_plan.md)（v0.2 ドラフト）
- [Phase 1 設計書](phase1_design.md)（v0.21、Phase 1 確定済み、§18「Phase 2 申し送り事項」が本書の出発点）
- [Phase 1 ベンチマーク結果](phase1_benchmark_results.md)（Phase 3 で性能維持確認時の基準値）
- 別文書：`phase2_graph_format_spec.md`（独自バイナリグラフ形式 `.odrg` 仕様、ステップ 1 で起こす）

---

## 0. 本書の目的と更新ルール

### 0.1 目的

本書は **OsmDotRoute Phase 2 で「何を、なぜ、どう実装したか」を後から把握できる記録** を残すことを目的とする。実装計画書（[`phase2_implementation_plan.md`](phase2_implementation_plan.md)）は「これから何をやるか」を、本書は「実際にどう作ったか」を保持する。

Phase 1 設計書 §18「Phase 2 申し送り事項」が本書の出発点であるが、**v0.2 で Phase 2 スコープを再編した結果、Phase 1 申し送り事項の大半は Phase 3 へ持ち越し**となった。本書は Phase 2 = データ供給側（`.odrg` 形式策定 + 独自 PBF パーサー + PBF→`.odrg` 抽出）の設計記録に専念する。

### 0.2 更新ルール

**各実装ステップが完了するたびに、本書の該当章を更新する**（実装計画書のステップ完了判定に「設計書の該当章更新」を含む）。

更新時に書くこと（Phase 1 設計書 §0.2 と同じテンプレート）：

- **意図 (Intent)**: 何を実現したかったか（要件 ID 参照）
- **採用設計 (Design)**: クラス／インターフェース構成、データ構造、アルゴリズム、外部仕様（API シグネチャ・バイナリレイアウト）
- **設計判断の根拠 (Why)**: なぜ別案ではなくこの設計にしたか
- **トレードオフ・制約 (Trade-off)**: 採用しなかった案、既知の限界、Phase 3 以降への申し送り
- **検証方法 (Verification)**: 単体テストの観点、手動検証手順、ベンチマーク
- **実装メモ (Notes)**: 後で読む人が引っかかりそうな点、暗黙の前提

書かなくてよいこと：

- コードの逐語コピー（ファイル名・パス参照で十分）
- 一時的な実装過程（commit ログで追える内容）
- TODO リスト（GitHub Issues / 別文書で管理）

### 0.3 章とステップの対応

| 章 | 対応ステップ | 状態 |
|---|---|---|
| 1. 全体概要 | 全ステップ通底 | 初版（2026-05-20、v0.2 ひな形） |
| 2. アーキテクチャ概観（Phase 1 → Phase 2 変遷） | 全ステップ通底 | 未記述 |
| 3. 独自バイナリグラフ形式 `.odrg` | ステップ 1 | 仕様書 v0.2 確定（[`phase2_graph_format_spec.md`](phase2_graph_format_spec.md)、2026-05-21）。本書 §3 に採用設計・判断根拠・トレードオフを記述済 |
| 4. 独自 OSM PBF パーサー `OsmDotRoute.Pbf` | ステップ 2 | ステップ 2.1〜2.2 完了（プロジェクト骨格 + protobuf ワイヤ形式 ProtoReader、§4.1 に記録）。2.3 以降未着手 |
| 5. PBF → `.odrg` 抽出ツール `OsmDotRoute.Extractor` | ステップ 3 | 未記述 |
| 6. (オプション) Itinero RouterDb → `.odrg` 変換ツール | ステップ 4-opt | 未記述・実施判断保留 |
| 7. Phase 2 検証と完了判定 | ステップ 5 | 未記述 |
| 8. Phase 2 制約事項と Phase 3 申し送り | ステップ 5 | 未記述 |

**Phase 3 設計書（別途作成予定）に持ち越す章**：

- ランタイム独自形式読込（`NativeRoadGraph` / `NativeRoadSnapper`）
- 動的制約ホットパス高速化（交差エッジキャッシュ）
- ランタイム Itinero 依存削除
- Bicycle / Truck プロファイル（独自設計、Truck=10t）
- Phase 3 ベンチマーク結果
- 親プロジェクト統合・パリティ検証

### 0.4 章内のテンプレート

各章は以下のテンプレートで記述する（Phase 1 設計書 §0.4 と同じ）：

```markdown
## NN. 章タイトル

**対応ステップ**: ステップ NN
**対応要件**: REQ-XXX-NNN, REQ-YYY-NNN
**Phase 1 申し送り**: 設計書 §18.X（該当時）
**実装日**: YYYY-MM-DD
**実装バージョン**: vX.Y.Z（ユーザー採番）
**主要ファイル**:
- `src/OsmDotRoute/...`

### NN.1 意図
（要件と達成目標、Phase 1 申し送り事項のどれに応えるか）

### NN.2 採用設計
（クラス図・API シグネチャ・データ構造・アルゴリズム・バイナリレイアウト）

### NN.3 設計判断の根拠
（採用した理由、なぜ別案を選ばなかったか）

### NN.4 トレードオフ・制約
（既知の限界、Phase 3 以降への申し送り）

### NN.5 検証方法
（テスト観点、手動検証手順、ベンチマーク）

### NN.6 実装メモ
（暗黙の前提、後で読む人が引っかかりそうな点）
```

---

## 1. 全体概要

### 1.1 Phase 2 のゴール（再掲）

[`phase2_implementation_plan.md`](phase2_implementation_plan.md) §1 より：

1. 独自バイナリグラフ形式 `.odrg` を策定（REQ-MAP-003）
2. 独自 OSM PBF パーサーを実装、System.\* のみで完結（REQ-MAP-007, REQ-DEP-002）
3. PBF → `.odrg` 直接抽出ツール `OsmDotRoute.Extractor` を提供（REQ-MAP-008）
4. **OsmDotRoute 最大の差別化要素である動的制約のホットパスをデータ形式自体が直接支援する**（§3.6 計画書、メモリ [[project-phase2-dynamic-restriction-design]]）
5. （末尾オプション）Itinero RouterDb → `.odrg` 変換ツール（REQ-MAP-004）

### 1.2 採用アプローチ（確定済み、計画書 v0.2）

- **バイナリ形式**: `.odrg` 拡張子、MemoryMappedFile アクセス、リトルエンディアン、バージョン番号埋込、**Itinero RouterDb 構造に引きずられない自由設計**
- **エッジ空間インデックス**: STR パック静的 R-tree（密度ムラ自動適応、O(log N) クエリ）
- **エッジ AABB**: double × 4 で bake（精度優先、ファイルサイズ増は MMF で吸収）
- **エッジフラグ**: 1〜2 バイト bitflag、橋・トンネル・高架・一方通行向き・歩道分離・有料・ラウンドアバウト・閉鎖・冬季閉鎖・ダート路面・私道・通学路 等を広めに採用、Phase 3 で運用上不要と判断したものは剪定
- **エッジシェイプ**: 連続バッファ + `ReadOnlySpan<GeoCoordinate>` ビュー（Phase 1 設計書 §18.4 の 77 MB/route を Phase 3 で根治する土台）
- **独自 PBF パーサー**: `src/OsmDotRoute.Pbf`（net9.0、System.\* 完結、`System.IO.Compression.ZLibStream` 利用、protobuf ワイヤ形式独自実装）
- **抽出ツール**: `src/OsmDotRoute.Extractor`（net9.0 Exe、`System.CommandLine`、PBF→`.odrg`）
- **公開 API**: Phase 1 のまま据え置き（ランタイム改修は Phase 3 で実施）
- **末尾オプション**: Itinero RouterDb → `.odrg` 変換ツールはステップ 1 完了時に技術的負担を評価して実施判断

詳細は実装計画書 §3 参照。

### 1.3 Phase 1 → Phase 2 → Phase 3 の変遷（要約）

| 観点 | Phase 1 | Phase 2 | Phase 3 |
|---|---|---|---|
| ランタイム入力 | Itinero `.routerdb` | Phase 1 のまま据え置き | OsmDotRoute 独自 `.odrg` |
| グラフ実装 | `ItineroRoadGraph` | Phase 1 のまま | `NativeRoadGraph`（新規） |
| スナップ実装 | `ItineroSnapper` | Phase 1 のまま | `NativeRoadSnapper`（新規） |
| OSM PBF パース | Itinero `Itinero.IO.Osm` | OsmDotRoute 独自（新規）, System.\* | （成果物利用） |
| PBF→`.odrg` 抽出 | （ない） | `OsmDotRoute.Extractor` CLI（新規） | （成果物利用） |
| ランタイム依存 | Itinero 1.5.1 + System.\* | Phase 1 のまま | System.\* のみ |
| エッジ空間索引 | なし | `.odrg` 内 STR パック R-tree（bake） | R-tree クエリで利用 |
| エッジ AABB | エッジシェイプから毎回計算 | `.odrg` 内に bake（double × 4） | 配列インデックス参照 |
| エッジフラグ | なし | `.odrg` 内に 1〜2 バイト bitflag bake | プロファイル評価で参照 |
| シェイプ取得 | `IReadOnlyList<GeoCoordinate>`（コピー、77 MB/route） | `.odrg` 内連続バッファ | `ReadOnlySpan<GeoCoordinate>`（ゼロコピー） |
| 制約 → エッジ集合 | Dijkstra 中に毎回 AABB プリフィルタ + 多角形交差 | （ランタイム変化なし） | `Add*Area` 時に 1 回計算、HashSet キャッシュ |
| 同梱プロファイル | Car / Pedestrian | Phase 1 のまま | + Bicycle / Truck（10 t、独自設計） |
| `Route.Shape` | `IReadOnlyList<GeoCoordinate>` | Phase 1 のまま | `ReadOnlyMemory<GeoCoordinate>` |
| MapVerifier | 継続活用 | `.odrg` 検査機能追加検討（ステップ 5） | `.odrg` ランタイム読込の体感確認 |

---

## 2. アーキテクチャ概観（Phase 1 → Phase 2 変遷）

**ステータス**: 未記述（ステップ 3 完了時に初版、Phase 1 設計書 §2 と差分対比形式で記述）

**初版時に書くこと**：

- レイヤー構造図（公開 API 層 / コア層 / 独自 PBF パーサー層 / 抽出ツール層）
- アセンブリ依存グラフ（Phase 2 新規プロジェクト `OsmDotRoute.Pbf` / `OsmDotRoute.Extractor` 追加後）
- 抽出経路：`*.osm.pbf` → `OsmDotRoute.Pbf`（HeaderBlock / PrimitiveGroup 解析）→ 道路 way フィルタ → 頂点正規化 → エッジ生成 → AABB 計算 → STR R-tree 構築 → エッジフラグ bake → bake プロファイル → `.odrg` 書出
- ランタイム経路（Phase 2 では変更なし）：Phase 1 と同じ `RouterDb` → `ItineroRoadGraph` → Dijkstra
- Phase 3 で起動する経路（参考）：`.odrg` → `MemoryMappedFile` → `NativeRoadGraph` → R-tree 索引 → エッジ展開

---

## 3. 独自バイナリグラフ形式 `.odrg`

**対応ステップ**: ステップ 1
**対応要件**: REQ-MAP-003
**Phase 1 申し送り**: 設計書 §18.5（`ItineroSnapper` / `ItineroRoadGraph` 置換の前提）
**実装日**: 2026-05-21（v0.2 確定）
**実装バージョン**: 仕様書 v0.2
**主要ファイル**:

- [`phase2_graph_format_spec.md`](phase2_graph_format_spec.md)（仕様書本体）

本章は仕様書（[`phase2_graph_format_spec.md`](phase2_graph_format_spec.md) v0.2）の要約 + 採用判断記録。バイト単位レイアウトの正は仕様書側にあり、本章は「なぜそうしたか」の記録に専念する。

### 3.1 意図

- **REQ-MAP-003**：MMF アクセス前提のバイナリグラフ形式を新規策定し、Phase 3 で Itinero `.routerdb` 依存を完全排除する土台を作る
- **Phase 1 設計書 §18.4（77 MB/route アロケート）の根治土台**：エッジシェイプを連続バッファ + `ReadOnlySpan<GeoCoordinate>` ビューで公開し、コピーレス取り出しを Phase 3 で実現する
- **動的制約ホットパスをデータ形式自体が直接支援**（OsmDotRoute 最大の差別化要素、メモリ [[project-phase2-dynamic-restriction-design]]）：エッジ AABB / STR R-tree / エッジフラグを抽出時に bake し、ランタイムは O(log E) で制約交差エッジを取得
- **Itinero RouterDb 構造に引きずられない自由設計**（ユーザー判断 2026-05-20、メモリ [[project-phase2-scope-redefinition]]）：Phase 2 スコープ再編に伴い、PBF からの直接抽出を主軸に置きフォーマット側を最適化阻害から解放

### 3.2 採用設計（仕様書要約）

#### 3.2.1 ファイル全体構造

- マジック `"ODRG\0\0\0\0"`、固定 256 バイトヘッダー、可変長セクションテーブル、9 セクションをオフセット参照型で連結（仕様書 §1.1 / §2）
- リトルエンディアン固定、x64 Windows での処理性能を優先（REQ-NFR-006）
- セクションテーブル方式で将来セクションを安全にスキップ可能（前方互換性、仕様書 §1.2 / §3）

#### 3.2.2 9 セクション構成

| セクション ID | 種別 | レイアウト | Phase 2 必須 |
|---|---|---|---|
| 0x0001 | Vertex Table | `(double lon, double lat) × vertexCount` = 16 B/頂点 | 必須 |
| 0x0002 | Edge Table | `(fromVid, toVid, shapeOff, shapeLen, bakedIdx)` = 24 B/エッジ | 必須 |
| 0x0003 | Edge Shape Buffer | 中間点列 `(double lon, double lat)` 連続配置、**エッジ ID 順**（v0.2 確定） | 必須 |
| 0x0004 | Edge AABB Table | `(minLon, minLat, maxLon, maxLat)` = 32 B/エッジ、double 4 個（v0.1.2 確定） | 必須 |
| 0x0005 | Edge Flag Table | uint16（2 B/エッジ）bitflag、14 bit 使用 + 2 bit 予約（v0.2 確定） | 必須 |
| 0x0006 | Edge Spatial Index | STR パック静的 R-tree、56 B/ノード、M=16 初期値（v0.2 暫定） | 必須 |
| 0x0007 | Baked Profile Table | `(speedKmh, flags)` = 8 B/(プロファイル × エッジ)、`bakedProfileIndex == edgeId`（v0.2 確定） | 必須 |
| 0x0008 | Turn Restriction Table | Phase 4+ 予約（v0.2 では length=0） | 予約 |
| 0x0009 | Metadata | UTF-8 JSON | 必須 |

#### 3.2.3 エッジフラグ bit 割り当て（v0.2 確定）

仕様書 §4.5 で 16 bit のうち 14 bit を割当、2 bit 予約：

- bit 0〜10：Bridge / Tunnel / Elevated / Roundabout / Toll / PrivateAccess / ServiceWay / Track / LivingStreet / PedestrianSeparated / WinterClosed（11 属性、OSM タグから機械的に bake 可）
- bit 11：SchoolZone 予約（v0.2 では 0 固定、Phase 3 で bake ルール確定 or 剪定）
- bit 12〜13：Oneway 向き（Forward / Backward、両方立てない）
- bit 14〜15：予約

#### 3.2.4 STR パック静的 R-tree

仕様書 §4.6 でビルド・シリアライズ・クエリを規定：

- ビルド：エッジ AABB 中心の x ソート → ストリップ分割 → 各ストリップ内 y ソート → M 個ずつ葉ノード化 → 再帰的に内部ノード構築
- シリアライズ：配列化、子は配列インデックス参照（ポインタなし、MMF フレンドリー）、1 ノード = 56 B 固定
- クエリ：`Stack<int>` ベース反復実装、`ReadOnlySpan<RTreeNode>` 上でビュー走査、ゼロアロケート

#### 3.2.5 抽出パイプライン

仕様書 §5 で 10 ステップを規定。特に Japan-wide PBF (2.3 GB) 入力時の RAM ピーク 1.2 GB（都道府県単位 bbox、3 パス走査 + bbox 早期フィルタ、§5.3）。

### 3.3 設計判断の根拠

#### 3.3.1 エッジ AABB を double × 4 で bake

- 採用：double × 4（32 B/エッジ）
- 不採用：float × 4（16 B/エッジ）
- 理由：精度劣化が地理座標系で顕在化（経度 0.0001 度 ≈ 10 m が float の有効桁ぎりぎり）。MMF 経由なのでファイルサイズ増加は RAM を圧迫しない。Phase 1 設計書 §18.3 で動的制約交差判定の精度問題が予見されており、ここで精度確保

#### 3.3.2 STR パック静的 R-tree を採用

- 採用：STR (Sort-Tile-Recursive) packed static R-tree
- 不採用：
  - **Uniform grid**：道路密度ムラに非適応（都市部で大量空クエリ、山間部で大セル）
  - **kd-tree**：点インデックス向き、AABB クエリ（範囲交差）には不向き
  - **動的 R-tree（R\*-tree 等）**：挿入削除が前提、`.odrg` は読み取り専用なので不要、ビルド時間も冗長
- 理由：静的データ向け最適配置・高さ ⌈log_M(N)⌉ 保証・オーバーラップ最小化・MMF フレンドリーな配列レイアウト

#### 3.3.3 エッジシェイプ連続バッファ + エッジ ID 順（v0.2 確定）

- 採用：エッジ ID 順に連続配置
- 不採用：
  - **Hilbert カーブ / R-tree 葉順での並べ替え**：Phase 3 ホットパス最適化として理論上有効だが、抽出ツール実装が複雑化（座標 → カーブインデックス計算、エッジ再番号、シェイプバッファ再構築）
- 理由：抽出ツールが単純（エッジを ID 順に処理 → そのままバッファ末尾に追記）、主ワークロード「エッジ ID 順スキャン」でキャッシュ局所性が効く、Phase 3 R-tree クエリのランダム ID アクセスは MMF ページキャッシュで吸収できる見込み

#### 3.3.4 Baked Profile Table を `bakedProfileIndex == edgeId` で確定（v0.2、YAGNI）

- 採用：エッジ ID をそのままインデックスに使用
- 不採用：OSM タグ集合ハッシュでの集約
- 理由：抽出ツール実装が単純、テーブルサイズが許容範囲（津島市 0.9 MB、愛知県想定数十 MB）、MMF 経由でランタイム RAM 圧迫なし。Phase 1 ベンチで IO はボトルネックではなかった

#### 3.3.5 セクションテーブル方式（前方互換）

- 採用：`(kind, offset, length)` で連結、未知 kind はスキップ
- 不採用：固定オフセットレイアウト
- 理由：Phase 4+ で Turn Restriction Table を追加する場合に、旧仕様で書かれた `.odrg` を新仕様読込側で安全に処理できる

### 3.4 トレードオフ・制約

#### 3.4.1 既知の限界

- **`.odrg` の最大想定サイズは都道府県単位（〜1 GB）**：Japan-wide `.odrg` は作らない方針。地域跨ぎ道路ネットは Japan-wide PBF + `--bbox` で取り込む（仕様書 §8.3）
- **`SchoolZone`（bit 11）は v0.2 では 0 固定**：OSM タグから機械的に bake しにくく、Phase 3 で実用性判断
- **R-tree 分岐数 M=16 は仮置き**：Phase 2 ステップ 3 で 8 / 16 / 32 を実測比較し最適値確定
- **シェイプ並び順がエッジ ID 順**：空間的に隣接するエッジが MMF 上で離れる可能性、Phase 3 ベンチで問題が出たら末尾オプションで再評価

#### 3.4.2 Phase 3 以降への申し送り

- ゼロコピー Span ビュー実装パターン（`MemoryMappedSegment<T>` ラッパー型）
- 公開 API シグネチャの最終確定（`NativeRoadGraph` / `NativeRoadSnapper`、仕様書 §9）
- 妥当性検証の実装位置（`NativeRoadGraph.Open` 内 / 別 `OdrgValidator` クラス、仕様書 §7）
- Edge Shape Buffer の空間局所性並べ替え（必要と判定された場合の末尾オプション）
- Baked Profile Table の OSM タグ集合集約（IO ボトルネック発覚時のみ）
- エッジフラグ bit 11 (`SchoolZone`) の bake ルール確定 or 剪定

### 3.5 検証方法

- **形式正当性**：抽出ツール（ステップ 3）が生成した `.odrg` を仕様書 §7「妥当性検証ルール」で検査（マジック / バージョン / セクション長整合 / 頂点 ID 範囲 / R-tree ルート整合 / JSON メタデータ）
- **Phase 1 RouterDb との等価性**：津島市 PBF から抽出した `.odrg` の頂点数・辺数・経緯度範囲が Phase 1 RouterDb (`tsushima.routerdb`) と一致または妥当な範囲（ステップ 3 完了時に単体テスト化、計画書 §6 / §8 R3）
- **STR R-tree クエリ正当性**：ランダム生成 AABB クエリで「全エッジを線形走査した結果」と「R-tree クエリ結果」が一致（ステップ 3 単体テスト）
- **MapVerifier 拡張**：`.odrg` 検査機能で頂点・エッジ・シェイプを地図上にオーバーレイ、Phase 1 RouterDb と重ね表示してズレを目視確認（ステップ 5、計画書 §5.6-19）

### 3.6 実装メモ

- セクションテーブルエントリは 24 B 固定（`kind(2) + reserved(2) + flags(4) + offset(8) + length(8)`）、9 セクション × 24 B = 216 B
- ヘッダー 256 B + セクションテーブル 216 B = 472 B が固定オーバーヘッド（津島市 9.6 MB に対して無視できる）
- メタデータ JSON には `sourcePbfHash` (SHA-256) を含めることで、同一 PBF からの再抽出を判定できる（インクリメンタル抽出は v0.2 では未対応）
- 抽出ツール側で `bakedProfileIndex == edgeId` を強制する。仕様書 §4.7.5 の選択は v0.2 で確定したが、Phase 3 で集約モードを追加した場合に備えて Edge Table の `bakedProfileIndex` フィールド自体は uint32 で残してある
- エッジフラグ `IsOnewayForward` と `IsOnewayBackward` が両方立つことは禁止。抽出ツール側で assertion を入れる

---

## 4. 独自 OSM PBF パーサー `OsmDotRoute.Pbf`

**対応ステップ**: ステップ 2
**対応要件**: REQ-MAP-007, REQ-DEP-002（System.\* 完結）
**Phase 1 申し送り**: なし（Phase 2 で前倒し実装）

**ステータス**: ステップ 2.1〜2.3 完了。2.4 以降は未着手。

### 4.1 ステップ 2.1〜2.2: プロジェクト骨格と protobuf ワイヤ形式（2026-05-20 実装）

#### 4.1.1 意図

OSM PBF パース基盤の最下層として、protobuf ワイヤ形式デコーダーを System.\* のみで実装する（REQ-DEP-002、計画書 §3.3、§5.6-15）。`OsmDotRoute.Pbf` プロジェクトを新設し、`InternalsVisibleTo` で `OsmDotRoute.Extractor`（未作成）と `OsmDotRoute.Tests` から内部型を参照可能にする。

#### 4.1.2 採用設計

**プロジェクト構成**：

- `src/OsmDotRoute.Pbf/OsmDotRoute.Pbf.csproj`：net9.0、ルート名前空間 `OsmDotRoute.Pbf`、`InternalsVisibleTo`：`OsmDotRoute.Extractor` / `OsmDotRoute.Tests`
- 外部依存ゼロ（System.\* のみ、`System.Buffers.Binary` / `System.IO.Compression` を利用予定）
- 全型 internal（外部 API 約束なし、Phase 3 以降のリファクタリング自由度確保）

**型構成**（`OsmDotRoute.Pbf.Protobuf` 名前空間）：

- `WireType` enum：Varint(0) / Fixed64(1) / LengthDelimited(2) / StartGroup(3) / EndGroup(4) / Fixed32(5)
- `ProtoTag` readonly record struct：`(int FieldNumber, WireType WireType)`、`IsEnd` センチネル（FieldNumber == 0）
- `ProtoReader` ref struct：`ReadOnlySpan<byte>` 上のカーソル、ゼロアロケート
  - 読込：`ReadVarint64` / `ReadVarint32` / `ReadZigzag32` / `ReadZigzag64` / `ReadFixed32` / `ReadFixed64` / `ReadLengthDelimited`（コピーなしスライス返却）
  - 制御：`ReadTag` / `SkipField` / `Position` / `HasMore` / `Remaining`
  - 例外：EOF / varint 10 バイト超 / uint32 オーバーフロー / field_number 0 / length-delimited サイズ超過 → 全て `InvalidDataException`、group wire-type は `NotSupportedException`

#### 4.1.3 設計判断の根拠

- **`ref struct` の選択**：`ReadOnlySpan<byte>` を保持しゼロアロケートを実現するため、ref struct 必須。Phase 2 計画書 §3.2「Span/Memory ベース API」と整合
- **`ReadLengthDelimited` がスライスを返す**：embedded message を内部走査する場合に、`new ProtoReader(slice)` で入れ子に再帰でき、コピーが発生しない（PBF の PrimitiveBlock → PrimitiveGroup → DenseNodes など多階層構造で有効）
- **field_number == 0 を `IsEnd` センチネルにし、同時に不正タグとしても拒否**：バッファ末尾と不正データを区別できる（`ReadTag()` で `HasMore` が false なら IsEnd、`HasMore` で 0 タグが現れたら例外）
- **採用しなかった案**：
  - protobuf-net 依存：REQ-DEP-002 違反、Phase 2 主軸の独自実装方針と矛盾
  - `MemoryMappedFile` 直接渡し：PBF は zlib 圧縮ブロック内の protobuf なので、解凍後の `byte[]`/`Span` を渡す形が自然。MMF 直走査は zlib との相性が悪い

#### 4.1.4 トレードオフ・制約

- **ref struct の利用制約**：xUnit `Assert.Throws<T>(() => ...)` のラムダにキャプチャできない（CS8175）。テストでは `try/catch + bool フラグ` パターンで対応（`ProtoReaderTests` 4 箇所）
- **group wire-type は非対応**：protobuf 2 で廃止された機能、OSM PBF でも使われない。`NotSupportedException` を投げる
- **`ulong` を `long` zigzag デコードで最上位ビット**：`(long)(n >> 1) ^ -((long)n & 1L)` で C# のオーバーフロー警告を避ける書き方

#### 4.1.5 検証方法

`tests/OsmDotRoute.Tests/Pbf/ProtoReaderTests.cs`（27 件、全 pass、2026-05-20）：

- varint：単バイト / 複数バイト / `ulong.MaxValue`（10 バイト）/ EOF 例外 / uint32 オーバーフロー例外
- zigzag：0/1/-1/2/-2/64/-64（32bit・64bit）
- tag：通常 / EOF で `IsEnd` / field_number=0 で例外
- fixed32 / fixed64：リトルエンディアン
- length-delimited：スライス参照 / バッファ超過時例外
- SkipField：各 WireType / Group で `NotSupportedException`
- 複合並び：tag → varint → tag → length-delimited が連続して正しく読める

Phase 1 既存 153 件と合わせ **180/180 全 pass**、回帰なし。

#### 4.1.6 実装メモ

- `BinaryPrimitives.ReadUInt32LittleEndian` / `ReadUInt64LittleEndian` を fixed32/64 に利用（System.* 標準、エンディアン明示）
- `ReadTag()` は EOF 時 `default(ProtoTag)` を返す（`FieldNumber=0` 扱い、`IsEnd=true`）。呼出側は `while (true) { var t = r.ReadTag(); if (t.IsEnd) break; ... }` のパターンで使う
- ZLib 解凍は本ステップ未実装（ステップ 2.3 で `System.IO.Compression.ZLibStream` 利用予定、計画書 §5.6-16）

### 4.2 ステップ 2.3: PBF Blob 構造と ZLib 解凍（2026-05-21 実装）

#### 4.2.1 意図

OSM PBF のフレーミング層を実装する：4 バイト BE プレフィックス → `BlobHeader` protobuf → `Blob` protobuf のシーケンスを解析し、`Blob` 内の raw または zlib_data を取り出して解凍後ペイロードを呼出側へ供給する（REQ-MAP-007、REQ-DEP-002、計画書 §3.3 / §5.6-16）。次層（HeaderBlock / PrimitiveBlock）はステップ 2.4 以降で本層の出力を `ProtoReader` で再パースする形になる。

#### 4.2.2 採用設計

**型構成**（`OsmDotRoute.Pbf.Blob` 名前空間、全 internal）：

- `PbfBlobType` enum：`Unknown` / `Header` ("OSMHeader") / `Data` ("OSMData")
- `PbfBlobReader` class（`IDisposable`）：
  - `PbfBlobReader(Stream stream, bool leaveOpen = false)`
  - `bool MoveNext()`：4 B BE サイズ → BlobHeader → Blob → 解凍を 1 ブロック実行、EOF で false
  - `PbfBlobType CurrentType { get; }`：直前 Blob の種別
  - `ReadOnlySpan<byte> CurrentPayload { get; }`：解凍後ペイロード、次の `MoveNext` / `Dispose` まで有効

**仕様準拠の固定値**：

- `MaxBlobHeaderSize = 64 KB`、`MaxBlobSize = 32 MB`（PBF 仕様の上限値）
- BlobHeader フィールド：field 1 (type, string) / field 2 (indexdata, bytes, スキップ) / field 3 (datasize, varint) — field 1 と 3 を required 扱い、欠落時 `InvalidDataException`
- Blob フィールド：field 1 (raw) / field 2 (raw_size) / field 3 (zlib_data) / field 4 (lzma_data) / field 5 (deprecated bzip2) / field 6 (lz4_data) / field 7 (zstd_data)
- 未対応圧縮（lzma/bzip2/lz4/zstd）検出時 `NotSupportedException`、raw も zlib も無い場合 `InvalidDataException`
- zlib 解凍は `System.IO.Compression.ZLibStream`（.NET 6+ 標準、§5.6-16 計画書）

**バッファ戦略**：

- BlobHeader / Blob protobuf バイトは `ArrayPool<byte>.Shared.Rent` でレンタル → `try/finally` で Return
- 解凍後ペイロードもプール、`MoveNext` 呼出毎に前回分を Return
- zlib 入力は `MemoryStream(blobBuffer, zlibOffset, zlibLength, writable: false)` で blob バッファ上を直接ストリーム化（中間コピー回避）

#### 4.2.3 設計判断の根拠

- **`Stream` 受け取り API**：PBF ファイルは GB 級、全読み込みは NG。`FileStream` 直接渡しで逐次走査、`MemoryStream` でテストに対応
- **enumerator パターン（MoveNext + Current）**：`IEnumerator<T>` を実装しない理由は、ペイロードが `ReadOnlySpan<byte>` でありヒープ昇格しないため。`yield return` で `byte[]` を返す API より無駄が無い。Phase 3 ホットパスとも整合（メモリ [[project-phase2-dynamic-restriction-design]]）
- **payload のリサイクル仕様**：`MoveNext` 毎に前回 payload を ArrayPool に返却する明示契約。Phase 3 で OSM PBF をストリーミング処理する際、GC 圧を最小化する
- **zlib_data 位置の算出**：`ProtoReader.ReadLengthDelimited()` はスライスを返すが、`Position` プロパティと `slice.Length` から開始オフセットを `position - length` で復元できる。これにより `MemoryStream(buffer, offset, count)` で中間バッファ無しに `ZLibStream` を構築できる
- **採用しなかった案**：
  - `System.IO.Compression.DeflateStream`：zlib ヘッダー（0x78 0x9C 等の RFC 1950 プレフィックス）を読まないため不適合。OSM PBF の zlib_data は RFC 1950 形式、`ZLibStream` が正解
  - `IAsyncEnumerable<...>`：Phase 2 では同期ストリーム処理で十分。非同期化は Phase 3 以降の要件次第で判断
  - 解凍バッファを呼出側に bring-your-own させる API：上位コード（PrimitiveBlock パース）の煩雑化、ArrayPool を内部で抱える方が GC 影響範囲を限定できる

#### 4.2.4 トレードオフ・制約

- **`raw_size` 必須化**：仕様上 zlib_data があっても raw_size はオプションだが、本実装では事前バッファ確保のため必須化（未指定なら `InvalidDataException`）。実用上の OSM PBF は全て raw_size を含む
- **`indexdata` フィールド非対応**：PBF 仕様にあるが OSM コミュニティで未使用、本実装ではスキップのみ
- **未知 field_number のスキップ**：将来の PBF 拡張（zstd や独自フィールド）でも壊れない設計。ただし field 4-7 については圧縮種別として認識した上で NotSupportedException を投げる
- **`raw_size` を必ず ArrayPool 確保サイズに使う**：悪意ある PBF が `raw_size = 32 MB` と宣言して 1 KB のデータしか提供しない場合、無駄に 32 MB レンタルされる。Phase 2 では信頼できる入力（Geofabrik 配布等）が前提、Phase 3 で必要なら段階的バッファに変更可

#### 4.2.5 検証方法

`tests/OsmDotRoute.Tests/Pbf/Blob/PbfBlobReaderTests.cs`（25 件、全 pass、2026-05-21）：

- **正常系**：empty stream / single raw header / single zlib data / multi-blob 順次走査 / unknown blob type / payload 再利用
- **異常系（フレーミング層）**：truncated size prefix / truncated BlobHeader / truncated Blob body / size > 64 KB / size = 0 / size 負値
- **異常系（BlobHeader）**：type 欠落 / datasize 欠落
- **異常系（Blob）**：zlib_data あり raw_size 無し / raw_size < 実サイズ / raw_size > 実サイズ / すべての payload フィールド欠落
- **未対応圧縮**：lzma / lz4 / zstd → `NotSupportedException`
- **ライフサイクル**：Dispose 後 Current アクセスで `ObjectDisposedException` / 二重 Dispose 許容 / null stream / write-only stream

Phase 1 既存 153 件 + ProtoReader 27 件 + Blob 25 件 = **205/205 全 pass**、回帰なし。

#### 4.2.6 実装メモ

- テストはヘルパーで PBF バイト列を合成（`BuildBlobHeader` / `BuildBlobRaw` / `BuildBlobZlib` / `AssembleBlock`、`ZLibStream` で圧縮）。`ProtoWriter` 相当を別ファイルとして起こさず、テスト内 private static に閉じ込めて YAGNI
- `Buffer.BlockCopy` を raw payload コピーに使用（バイト配列間の高速メモコピー）
- `_sizePrefixBuffer = new byte[4]` を再利用しない選択肢もあったが、サイズプレフィックス読込毎に 4 B 確保するのは無駄。インスタンスごとに 1 個保持
- xUnit `Assert.Throws<T>(() => ...)` は ref struct を扱わない通常のラムダなのでそのまま使える（`PbfBlobReader` は class なので問題なし、`ProtoReader` のような ref struct とは異なる）

### 4.3 ステップ 2.4 以降の予定（未着手）

- 2.4 HeaderBlock 解析（bbox / required_features / writingprogram）
- 2.5 PrimitiveBlock + stringtable 解析（granularity / lat_offset / lon_offset / 文字列テーブル）
- 2.6 Node 解析（単体ノード、PBF では稀）
- 2.7 DenseNodes 解析（差分圧縮デコード、現代 OSM PBF のメイン）
- 2.8 Way 解析（refs 差分圧縮、tags キー値インデックス）
- 2.9 Relation 解析（members、tags。`type=restriction` は Phase 4+ 延期、§5.6-18）
- 2.10 高レベル `PbfReader` API + 津島市 PBF で Itinero と Node / Way / Relation 数を突合する単体テスト
- 採用しなかった案：protobuf-net 依存、`OsmSharp` 等の既存 .NET ライブラリ依存（REQ-DEP-002 違反）
- 動作確認：津島市 `*.osm.pbf`（親プロジェクトサンプル）で Node / Way / Relation 数を Itinero `Itinero.IO.Osm.LoadOsmDataAsync()` と突合

---

## 5. PBF → `.odrg` 抽出ツール `OsmDotRoute.Extractor`

**対応ステップ**: ステップ 3
**対応要件**: REQ-MAP-008
**Phase 1 申し送り**: なし（Phase 2 で前倒し実装）

**ステータス**: 未記述

**初版時に書くこと**：

- プロジェクト構成（`src/OsmDotRoute.Extractor/`、net9.0 Exe、`System.CommandLine` 依存）
- CLI コマンド体系（`extract` サブコマンド、入出力オプション、プロファイル指定、進捗表示）
- 内部処理パイプライン：
  1. PBF 読込（`OsmDotRoute.Pbf.PbfReader`）→ Node / Way / Relation 列挙
  2. 道路 way フィルタ（**`highway=*` 全部を取り込み、`access=no` / `area=yes` 等の access タグで除外**、§5.6-17 計画書で確定。フィルタは広めに取り、プロファイル判定は bake 時に実施）
  3. 頂点正規化（交差点・道路端点を頂点に、中間ノードはシェイプに）
  4. エッジ生成（fromVertexId / toVertexId / shape / OSM タグ集合）
  5. エッジ AABB 計算（double × 4）
  6. エッジフラグ抽出ルール表（`bridge=yes` → 橋ビット、`tunnel=yes` → トンネルビット、`oneway=*` → 一方通行向き、`maxspeed` → 速度フラグ、...）
  7. プロファイル bake（プロファイル × エッジ で `(canPass, speedKmh, oneway)` 表を構築、Phase 1 `ProfileEvaluator` を流用）
  8. STR R-tree 構築（変換時、ノード配列にシリアライズ）
  9. `.odrg` 書出（MMF レイアウトに従ったバイナリ）
- 津島市 `.osm.pbf`（PBF サイズ / 抽出時間 / `.odrg` 出力サイズ）の実測表
- Phase 1 RouterDb（津島市）との比較：頂点数 / 辺数 / 経緯度範囲 / 代表的経路結果の等価性確認
- 採用しなかった案：Itinero `RouterDb` を経由した変換（Phase 2 主軸から外した理由、§3.7 計画書）

---

## 6. (オプション) Itinero RouterDb → `.odrg` 変換ツール `OsmDotRoute.Converter`

**対応ステップ**: ステップ 4-opt
**対応要件**: REQ-MAP-004（P3[Phase2-opt]）
**Phase 1 申し送り**: なし

**ステータス**: 未記述・実施判断保留（ステップ 1 完了時点で技術的負担を評価）

**初版時に書くこと（実施した場合）**：

- 実施判断の根拠（ステップ 1 完了時点で「軽い／重い」をどう判定したか）
- プロジェクト構成（`src/OsmDotRoute.Converter/`、net9.0 Exe、Itinero 1.5.1 参照、`System.CommandLine`）
- 変換ロジック：`Itinero.RouterDb.Network` → 頂点列挙 → エッジ列挙 → AABB 計算 → STR R-tree 構築 → エッジフラグ bake（Itinero `edge_profile` から OSM タグを取り出す経路）→ プロファイル bake → `.odrg` 書出
- Phase 1 RouterDb (`tsushima.routerdb`) → `.odrg` 変換結果と、Phase 2 PBF→`.odrg` 抽出結果との等価性確認
- 採用しなかった案：Phase 2 主軸への組込み（`.odrg` 設計を縛るリスク、ユーザー判断 2026-05-20）

**実施しない判断をした場合**：

- 実施しない判断の根拠
- Phase 3 以降への申し送り（必要になれば Phase 3 末尾オプションとして再評価）

---

## 7. Phase 2 検証と完了判定

**対応ステップ**: ステップ 5
**対応要件**: なし（メタ章）
**Phase 1 申し送り**: なし

**ステータス**: 未記述

**初版時に書くこと**：

- `.odrg` 検査手段は **MapVerifier 拡張**で確定（§5.6-19 計画書）。Phase 1 ステップ 17 の 32/32 検証体制を踏襲し、`.odrg` の頂点・エッジ・シェイプを地図上にオーバーレイ、Phase 1 RouterDb と重ね表示してズレを目視確認
- 検証チェックリスト（Phase 1 ステップ 17 と同形式）：
  - `OsmDotRoute.Extractor` が津島市 `.osm.pbf` から `.odrg` を生成できる
  - 出力 `.odrg` の頂点数・辺数が Phase 1 RouterDb（津島市）と一致または妥当な範囲
  - エッジ AABB / R-tree が整合（ランダム検査）
  - エッジフラグが OSM タグから正しく bake されている
  - bake プロファイルが Phase 1 `ProfileEvaluator` の結果と一致
- v0.2.0 タグ判断
- Phase 2 完了判定（実装計画書 §6 ステップ表）

---

## 8. Phase 2 制約事項と Phase 3 申し送り

**対応ステップ**: ステップ 5（メタ章）
**対応要件**: なし（メタ章）
**Phase 1 申し送り**: Phase 1 設計書 §18 全体（Phase 2 完了時にどれが解消できたか確認）

**ステータス**: 未記述（Phase 2 完了確定時に初版、Phase 1 設計書 §18 と同形式）

**初版時に書くこと**：

- **Phase 1 申し送り事項の Phase 2 完了時点での解消状況**
  - §18.1 親プロジェクト統合 → Phase 3（継続延期、Phase 1 計画書 §13 のとおり）
  - §18.2 都道府県単位ベンチ → Phase 3
  - §18.3 制約 100 件短絡効果 → Phase 3 で `.odrg` ランタイム読込実装後に解消予定
  - §18.4 77 MB アロケート → Phase 3 で Span/Memory 化と同時に解消予定（Phase 2 では設計の土台）
  - §18.5 `ItineroSnapper` / `ItineroRoadGraph` 置換 → Phase 3 へ持ち越し
  - §18.6 カバレッジ目標 → Phase 3 で API が変わるタイミングで導入判断

- **Phase 2 で新たに判明した制約・課題**
  - 独自 PBF パーサー実装で発見した制約（Relation 対応範囲・特殊エンコーディング等）
  - `.odrg` ファイルサイズの実測結果（Phase 1 RouterDb 比）
  - 抽出ツール所要時間の実測結果

- **Phase 3 への申し送り事項**（実装計画書 §6 Phase 3 で実施するステップ参照）
  - 3A. ランタイム `.odrg` 読込実装（`NativeRoadGraph` / `NativeRoadSnapper`）
  - 3B. 動的制約ホットパス高速化（「制約 ID → 交差エッジ ID 集合」キャッシュ機構）
  - 3C. ランタイム Itinero 依存削除（`OsmDotRoute.Itinero` 撤去、`Route.Shape` 破壊変更）
  - 3D. Bicycle / Truck プロファイル独自設計（Truck=10t、日本道路法ベース）
  - 3E. ベンチマーク再実施（津島市、Phase 1 基準値との比較）
  - 3F. 親プロジェクト統合・パリティ検証（旧 Phase 1 ステップ 16）
  - 3G. 都道府県単位ベンチ（Phase 1 §18.2 リベンジ）
  - 3H. ユーザー検証・Phase 3 確定（OSS 公開準備、運用上不要なエッジフラグ剪定判断）

- RouterDb 変換ツール（REQ-MAP-004）の最終位置付け（実施したか・Phase 3 以降に延期したか）

---

## 9. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 (draft) | 2026-05-20 | 初版ひな形作成。Phase 2 実装計画書 v0.1.2 と並行作成、各章は対応ステップ完了時に肉付け。Phase 1 設計書 §18「Phase 2 申し送り事項」を出発点として章立てを構成 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-20 | **Phase 2/3 スコープ再編に伴い章立てを全面再構成**。ランタイム読込・動的制約キャッシュ機構・Itinero 依存削除・Bicycle/Truck・ベンチ・親プロ統合 を Phase 3 設計書（別途）へ申し送りに格上げ、§0.3 章対応表で明示。新章として §4 独自 OSM PBF パーサー、§5 PBF→`.odrg` 抽出ツール、§6 (オプション) RouterDb→`.odrg` 変換ツール、§7 Phase 2 検証と完了判定 を追加。§1.3 Phase 1→2→3 変遷表を 3 段に拡張、Phase 2 ではランタイム動作変化なしを明示 | Claude (Opus 4.7) |
| 0.2.1 (draft) | 2026-05-21 | ステップ 1 完了に伴い §3 を初版記述。仕様書 v0.2 の要約 + 採用判断記録（§3.1〜§3.6 全埋め、エッジ AABB double / STR R-tree / シェイプエッジ ID 順 / `bakedProfileIndex == edgeId` / セクションテーブル方式 の根拠と不採用案を記述）。§0.3 章対応表のステップ 1 行を「v0.2 確定」に更新 | Claude (Opus 4.7) |
| 0.2.2 (draft) | 2026-05-21 | ステップ 2.3 完了反映。§4.2 に「PBF Blob 構造と ZLib 解凍」（`PbfBlobType` enum / `PbfBlobReader` クラス）の意図・採用設計・判断根拠・トレードオフ・検証方法を記述。§4 ステータスを「2.1〜2.3 完了」に更新、旧 §4.2 を §4.3「2.4 以降の予定」へ繰下げ。採用しなかった案（DeflateStream / IAsyncEnumerable / bring-your-own buffer）の根拠も明記 | Claude (Opus 4.7) |
