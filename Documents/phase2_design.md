# OsmDotRoute Phase 2 設計書

**バージョン**: 0.2.8（ひな形・ステップ 2.9 完了反映）
**作成日**: 2026-05-20（v0.1 として）
**最終更新**: 2026-05-21（v0.2.8、§4.8 ステップ 2.9 完了反映）
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

**ステータス**: ステップ 2.1〜2.9 完了。2.10 以降は未着手。

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

### 4.3 ステップ 2.4: HeaderBlock 解析（2026-05-21 実装）

#### 4.3.1 意図

OSM PBF ファイル先頭の OSMHeader Blob を解析し、(1) ファイル全体のバウンディングボックス、(2) `required_features` による読込可否判定、(3) 診断用メタデータ（`writingprogram` / `source`）を抽出する（REQ-MAP-007、計画書 §6 ステップ 2.4）。`required_features` は OsmDotRoute.Pbf がサポートしない機能（例：HistoricalInformation）が含まれていれば早期に弾く。

#### 4.3.2 採用設計

**型構成**（`OsmDotRoute.Pbf.Osm` 名前空間、全 internal）：

- `OsmBoundingBox` readonly record struct：WGS84 度単位 (double) の `(MinLon, MinLat, MaxLon, MaxLat)`。今後の bbox 表現（抽出 CLI `--bbox` オプション等）にも流用予定
- `OsmHeader` sealed record：`(BoundingBox?, RequiredFeatures, OptionalFeatures, WritingProgram?, Source?)`
- `OsmHeaderParser` static class：
  - `Parse(ReadOnlySpan<byte> headerBlockBytes) → OsmHeader`：純粋な protobuf 解析、副作用なし
  - `EnsureSupported(OsmHeader header)`：required_features 検証、未サポートで `NotSupportedException`
  - `SupportedRequiredFeatures` 公開セット：`{"OsmSchema-V0.6", "DenseNodes"}`（StringComparer.Ordinal）

**HeaderBlock フィールドマップ**（proto2 osmformat.proto）：

| Field | 型 | wire-type | OsmDotRoute での扱い |
|---|---|---|---|
| 1 bbox | HeaderBBox (msg) | LengthDelimited | `OsmBoundingBox` に変換（ナノ度 × 1e-9） |
| 4 required_features | repeated string | LengthDelimited | List に蓄積、`EnsureSupported` で検証 |
| 5 optional_features | repeated string | LengthDelimited | List に蓄積、情報のみ |
| 16 writingprogram | string | LengthDelimited | string |
| 17 source | string | LengthDelimited | string |
| 32-34 replication 関連 | int64/string | Varint/LengthDelimited | スキップ（Phase 4+ 必要なら追加） |

**HeaderBBox フィールド**（field 1-4、全て sint64 nanodegrees、protobuf 上は required）：

- left → MinLon、right → MaxLon、top → MaxLat、bottom → MinLat
- どれかが欠落すれば `InvalidDataException`

#### 4.3.3 設計判断の根拠

- **`Parse` と `EnsureSupported` を分離**：解析と検証を分離することで、診断ツール（MapVerifier 等）で「サポート外でも内容を見たい」ケースに対応可能。Phase 2 メインパスは Extractor が `Parse → EnsureSupported` を続けて呼ぶ
- **`SupportedRequiredFeatures` を `IReadOnlySet<string>`**：将来サポート機能を増やす場合に拡張点が明確。HashSet で O(1) 検索
- **`OsmBoundingBox` を `readonly record struct` で公開**：ゼロアロケート + 値セマンティクス。`OsmHeader.BoundingBox` は `Nullable<OsmBoundingBox>` で「bbox 未指定」を明示
- **採用しなかった案**：
  - **`record struct` ではなく `class`**：`Nullable<T>` で「未指定」を表現できないため別途 `bool HasBoundingBox` フラグが必要となり煩雑
  - **`StringComparer.OrdinalIgnoreCase`**：OSM PBF 仕様では feature 名は大文字小文字を区別する。`"DenseNodes"` と `"densenodes"` を別扱いするのが仕様準拠
  - **replication 情報を保持**：Phase 2 では未使用、Phase 4+ で必要なら追加（YAGNI）

#### 4.3.4 トレードオフ・制約

- **`SupportedRequiredFeatures` が固定値**：将来 PBF 仕様が拡張された場合に追従が必要。ただし OsmSchema-V0.6 は OSM の標準で長く変わらない見込み、DenseNodes は現代 PBF のデファクト標準
- **`OsmBoundingBox` のフィールド順が `(MinLon, MinLat, MaxLon, MaxLat)`**：HeaderBBox は `(left, right, top, bottom)` だが、本実装では「lon/lat × min/max」順に統一。GeoJSON や `.odrg` ヘッダーとも整合
- **`writingprogram` / `source` を string にデコード**：UTF-8 として有効である前提。invalid UTF-8 が来た場合 `Encoding.UTF8.GetString` が代替文字を入れるか例外を出すかは .NET 9 のデフォルト挙動依存（Phase 3 で必要なら DecoderFallback を明示）
- **`Parse` がヒープ確保（List + string）を行う**：HeaderBlock は 1 ファイルにつき 1 回しか解析されないため、ホットパスではない。ゼロアロケート化は不要

#### 4.3.5 検証方法

`tests/OsmDotRoute.Tests/Pbf/Osm/OsmHeaderParserTests.cs`（18 件、全 pass、2026-05-21）：

- **Parse 正常系**：空ペイロード / 最小 required_features / bbox ナノ度変換（津島市座標）/ 負座標 / writingprogram + source / optional_features / 複数 required の順序保持 / 未知 field (replication) スキップ / 全フィールド組合せ
- **Parse 異常系**：HeaderBBox required 欠落 / bbox wire-type 不一致 / required_features wire-type 不一致
- **EnsureSupported**：既知機能 OK / 未知 required は NotSupportedException / optional は無視 / null header / 空 required OK / 大文字小文字区別

Phase 1 既存 153 件 + ProtoReader 27 件 + Blob 25 件 + Osm.Header 18 件 = **223/223 全 pass**、回帰なし。

#### 4.3.6 実装メモ

- HeaderBBox の `sint64` フィールドは zigzag エンコードされた varint。`ProtoReader.ReadZigzag64()` で復号後、`* 1e-9` で度単位に変換
- `EnsureSupported` は LINQ を使わずに `foreach` ループ（小コレクション向けで `IEnumerator` 確保なし）
- テスト用ヘルパー（`BuildBoundingBox` / `WriteString` / `WriteLengthDelimited` / `ZigZagEncode`）はテストファイル内 private static で完結。ステップ 2.5 以降で 3+ 重複したら共有テストヘルパーへ切り出し検討（現状 2 重複以下）

### 4.4 ステップ 2.5: PrimitiveBlock + stringtable 解析（2026-05-21 実装）

#### 4.4.1 意図

OSM PBF の OSMData Blob 内に格納される PrimitiveBlock のうち、(1) StringTable（tag キー値のインデックステーブル）、(2) 座標スケール情報（granularity / lat_offset / lon_offset）、(3) date_granularity を解析する。PrimitiveGroup（Node / Way / Relation の実データ）はステップ 2.6-2.9 で実装するため、本ステップでは field 2 を意図的にスキップする（計画書 §6 ステップ 2.5）。

#### 4.4.2 採用設計

**型構成**（`OsmDotRoute.Pbf.Osm` 名前空間、全 internal）：

- `OsmStringTable` class：UTF-8 バイト列の配列を保持し、`GetBytes(index)` / `GetString(index)` で参照
- `PrimitiveBlock` class：StringTable + granularity + lat_offset + lon_offset + date_granularity
  - `ToLon(long encodedLon)` / `ToLat(long encodedLat)` 座標変換メソッド
- `PrimitiveBlockParser` static class：
  - `Parse(ReadOnlySpan<byte> blockBytes) → PrimitiveBlock`：envelope 解析、PrimitiveGroup は skip
  - `ParseStringTable(ReadOnlySpan<byte> bytes) → OsmStringTable`：StringTable サブメッセージ単体の解析（ステップ 2.6+ で再利用可能に public で公開）
  - `DefaultGranularity = 100` / `DefaultDateGranularity = 1000` 定数公開

**PrimitiveBlock フィールドマップ**（proto2 osmformat.proto）：

| Field | 型 | wire-type | OsmDotRoute での扱い |
|---|---|---|---|
| 1 stringtable | StringTable (required) | LengthDelimited | `OsmStringTable` に変換 |
| 2 primitivegroup | repeated PrimitiveGroup | LengthDelimited | スキップ（2.6+ で実装） |
| 17 granularity | int32 (default 100) | Varint | 値 ≤ 0 で `InvalidDataException` |
| 18 date_granularity | int32 (default 1000) | Varint | uint32 として読込（負値非対応、Phase 2 未使用） |
| 19 lat_offset | int64 (default 0) | Varint | `unchecked((long)varint64)` で sign-extended 復号 |
| 20 lon_offset | int64 (default 0) | Varint | 同上 |

**座標変換式（PBF 仕様）**：

```text
lon = 1e-9 × (LonOffset + Granularity × encodedLon)
lat = 1e-9 × (LatOffset + Granularity × encodedLat)
```

Granularity と Offset は nanodegree 単位。default Granularity=100 のとき、encoded 1 単位 = 100 nanodegree = 1e-7 度。

#### 4.4.3 設計判断の根拠

- **StringTable を `byte[][]` で保持**：実装最小、各エントリ独立。Phase 3 で「単一バッファ + オフセット配列」最適化を検討（メモリ局所性向上）
- **`ParseStringTable` を public 公開**：ステップ 2.5 の責務外だが、ステップ 2.6 以降で「PrimitiveBlock 全体を再パースせず StringTable サブメッセージだけ取り出す」用途を想定して早期公開（YAGNI 違反気味だが、3 行コードでテストもできるので許容）
- **`lat_offset` / `lon_offset` の符号復元**：protobuf の `int64` は varint エンコードで負値は 10 バイト sign-extended となる。`ReadVarint64()` で ulong として取り出し、`unchecked((long))` で復元。`ReadZigzag64()` ではない（sint64 とは異なる）点に注意
- **`granularity ≤ 0` を `InvalidDataException`**：仕様上 default 100 で正値前提、0 や負値は不正データ
- **PrimitiveGroup スキップを明示**：`ProtoReader.SkipField(LengthDelimited)` で安全にスキップ。ステップ 2.6 では `Parse` を拡張するか、`VisitGroups` 等の別 API を追加する（YAGNI、現時点では未定）
- **`PrimitiveBlock` を `sealed record` ではなく `class`**：座標変換メソッド（`ToLon` / `ToLat`）を持つため、record の値セマンティクスより通常クラスが自然
- **採用しなかった案**：
  - **StringTable のオフセット配列方式**：1 ブロック数千文字列で N+1 byte[] 確保するより 2 byte[] が GC 友好的だが、Phase 2 v0.1 ではシンプルさ優先（measure first）
  - **PrimitiveGroup を本ステップで部分解析**：スコープ越え、ステップ分割の目的を損なう
  - **`ToLon` / `ToLat` を struct extension method**：Phase 2 では Node / Way 解析時に `block.ToLon(x)` で呼ぶ予定、インスタンスメソッドが直感的

#### 4.4.4 トレードオフ・制約

- **StringTable インデックス 0 の慣習**：OSM PBF では index 0 を空文字列にする慣習（DenseNodes の tag リスト区切り用）。本実装はこれを enforce しないが、抽出ツール側で参照する際は気にする必要あり。設計書 §4.5（ステップ 2.7 予定）で再確認
- **granularity が int32 として読込**：負値（10 バイト sign-extended）は `ReadVarint32` で `InvalidDataException`。実用上問題なし
- **`OsmStringTable.GetString` が UTF-8 デコード毎に文字列確保**：tag アクセスがホットパスになる場合は問題。Phase 3 で「キャッシュ + interning」を検討
- **byte[][] による N+1 allocation**：津島市 PBF で 1 ブロックあたり数千文字列、Japan 全体で数百ブロック → 数百万 allocation 可能。GC 影響を Phase 3 ベンチで実測判断

#### 4.4.5 検証方法

`tests/OsmDotRoute.Tests/Pbf/Osm/PrimitiveBlockParserTests.cs`（19 件、全 pass、2026-05-21）：

- **Parse 正常系**：最小 StringTable + デフォルト値 / 全フィールド指定 / 負オフセット / PrimitiveGroup スキップ / 未知 field スキップ
- **Parse 異常系**：StringTable 欠落 / granularity = 0 / granularity 負 / wire-type 不一致 2 件
- **ParseStringTable**：空 / 順序保持 / GetBytes UTF-8 / 未知 field スキップ
- **PrimitiveBlock 座標変換**：デフォルト granularity / オフセットあり / 負座標
- **引数検証**：null StringTable / null items

Phase 1 既存 153 件 + ProtoReader 27 件 + Blob 25 件 + Osm.Header 18 件 + Osm.PrimitiveBlock 19 件 = **242/242 全 pass**、回帰なし。

#### 4.4.6 実装メモ

- `lat_offset` / `lon_offset` は **signed int64 varint** であり sint64（zigzag）ではない。テストヘルパー `WriteVarintSigned` で `unchecked((ulong)value)` 経由で書き込み、パーサーは逆変換で復元
- 「PrimitiveGroup スキップ時の wire-type 検証」を入れている：`EnsureWireType(tag, WireType.LengthDelimited, "PrimitiveBlock.primitivegroup")`。スキップする field でも形式チェックを行い、不正データを早期検知
- テスト「granularity 負値」は ProtoReader 層で `ReadVarint32 → uint32 範囲超 → InvalidDataException` の連鎖を検証。今後 Phase 2.7 (DenseNodes) で値域チェックが必要なケースが増えるため、層境界での例外型を統一しておく

### 4.5 ステップ 2.6: 単体 Node 解析（2026-05-21 実装）

#### 4.5.1 意図

OSM PBF の単体 Node メッセージ（`PrimitiveGroup.nodes` 内の repeated Node）を解析する。現代 PBF では DenseNodes (ステップ 2.7) が主流のためほぼ呼ばれないが、仕様完全性のために実装する（計画書 §6 ステップ 2.6）。

#### 4.5.2 採用設計

**型構成**（`OsmDotRoute.Pbf.Osm`、全 internal）：

- `OsmNode` sealed record：`(Id, Lon, Lat, TagKeys, TagValues)`
  - Lon / Lat は `PrimitiveBlock.ToLon/ToLat` で度単位に変換済
  - TagKeys / TagValues は `int[]`、`OsmStringTable` インデックス、同じ長さ
- `OsmNodeParser` static class：
  - `Parse(ReadOnlySpan<byte> nodeBytes, PrimitiveBlock block) → OsmNode`
  - packed uint32 デコードヘルパー `ReadPackedUint32` (private)

**Node フィールドマップ**（proto2 osmformat.proto）：

| Field | 型 | wire-type | 扱い |
|---|---|---|---|
| 1 id | sint64 (required) | Varint | `ReadZigzag64` |
| 2 keys | repeated uint32 (packed) | LengthDelimited | packed decode → `int[]` |
| 3 vals | repeated uint32 (packed) | LengthDelimited | packed decode → `int[]` |
| 4 info | Info (optional) | LengthDelimited | スキップ（Phase 2 で metadata 不要） |
| 8 lat | sint64 (required) | Varint | `ReadZigzag64` → `block.ToLat` |
| 9 lon | sint64 (required) | Varint | `ReadZigzag64` → `block.ToLon` |

**検証ルール**：

- id / lat / lon 欠落 → `InvalidDataException`
- `keys.Length != vals.Length` → `InvalidDataException`
- packed encoding 必須（非 packed = Varint wire-type）→ `InvalidDataException`
- 未知 field number / Info / その他 wire-type 違反は仕様準拠で処理

#### 4.5.3 設計判断の根拠

- **`OsmNode` を sealed record（class）で実装**：`int[]` フィールドを 2 つ持つため struct ではメモリ局所性のメリット薄。record の equality・ToString が無料で得られる
- **Info を意図的にスキップ**：Phase 2 ルーティング用途では version / timestamp / changeset / user は不要。Phase 3 以降で必要になれば追加（YAGNI）
- **`block` をパース時に渡す API**：座標変換を一段で完結。caller の忘れ防止。Phase 2.7 DenseNodes は状態を伴うため別 API
- **packed 専用、非 packed は拒否**：現代 OSM PBF（Geofabrik / Osmosis / osmconvert）は全て packed。非 packed 対応を入れると分岐が複雑化、YAGNI
- **`int[]` で保持（uint ではなく）**：StringTable インデックスは仕様上 uint32 だが実用上小さい値（< 64K）、`int` の方が C# 標準コレクション API と整合
- **採用しなかった案**：
  - **`OsmTagCollection` 型を別途設ける**：Phase 2 では `TagKeys` / `TagValues` 配列の直接アクセスで十分。Phase 3 で頻繁にタグ検索が必要になれば導入を再評価
  - **非 packed encoding 対応**：spec が "readers should accept both" と言うが、現代 PBF で見ない。実装複雑化を避ける
  - **`Info` を保持**：Phase 2 で使い道なし、保持コストだけかかる

#### 4.5.4 トレードオフ・制約

- **`int[]` ヒープ確保**：1 Node あたり最大 2 配列。タグ無しノードは `Array.Empty<int>()` で再利用、tagged ノードのみ確保
- **`block` 依存**：パーサーが PrimitiveBlock を必要とするため、PrimitiveBlock 解析が先に完了している前提（自然なシーケンス）
- **encoded lon/lat の double 変換が double 精度限界**：granularity=100 × encoded 1e9 = 1e11 の長整数演算後 1e-9 倍。double の 15-17 桁有効精度内に収まる
- **non-packed 入力での失敗が分かりにくいかもしれない**：エラーメッセージで「expected wire-type LengthDelimited but got Varint」と表示、適切な原因追跡が可能

#### 4.5.5 検証方法

`tests/OsmDotRoute.Tests/Pbf/Osm/OsmNodeParserTests.cs`（15 件、全 pass、2026-05-21）：

- **正常系**：最小 Node / タグ付き Node / 負 id (zigzag) / 負座標 / block オフセット適用 / Info スキップ / 未知 field スキップ / 空 keys/vals
- **異常系**：id 欠落 / lat 欠落 / lon 欠落 / keys/vals 長さ不一致 / 非 packed keys / id wire-type 不一致
- **引数検証**：null block

Phase 1 既存 153 件 + ProtoReader 27 件 + Blob 25 件 + Osm.Header 18 件 + Osm.PrimitiveBlock 19 件 + Osm.Node 15 件 = **257/257 全 pass**、回帰なし。

#### 4.5.6 実装メモ

- `id` は sint64 なので zigzag。`lat` / `lon` も sint64 zigzag。一方 `keys` / `vals` の中身は uint32（packed）varint で zigzag ではない（仕様）
- packed uint32 のデコードは「length-delimited バッファの中で varint を末尾まで連続読み」。空 packed (`length=0`) → `Array.Empty<int>()` 高速返却
- ヘルパー `ReadPackedUint32` は `List<int>` 経由。Phase 3 で性能ボトルネックなら `ArrayPool<int>` + 既知サイズの場合のスタックアロケート版に最適化検討
- テストヘルパー `BuildNode` / `PackUint32` / `WriteTag` / `WriteVarint` / `ZigZagEncode` はテストファイル内 private static で完結（5 ファイル目の重複となるが、共有ヘルパー抽出は 6+ ファイル時に判断）

### 4.6 ステップ 2.7: DenseNodes 解析（2026-05-21 実装）

#### 4.6.1 意図

OSM PBF の DenseNodes メッセージ（`PrimitiveGroup.dense`）を解析する。現代 PBF（Geofabrik / Osmosis / osmconvert）は全ノードを DenseNodes 形式で格納するため、本パーサーが PBF 抽出のメインパスとなる（計画書 §6 ステップ 2.7）。

#### 4.6.2 採用設計

**型構成**（`OsmDotRoute.Pbf.Osm`、全 internal）：

- `DenseNodesParser` static class：
  - `Parse(ReadOnlySpan<byte> denseNodesBytes, PrimitiveBlock block) → OsmNode[]`
  - 内部ヘルパー `DistributeTags`（keys_vals → 各 Node の TagKeys/TagValues 配列に分配）

**共有ユーティリティ**（`OsmDotRoute.Pbf.Protobuf`）：

- `PackedReader` static class：本ステップで新設し、ステップ 2.6 の `OsmNodeParser` も refactor 済
  - `ReadPackedUint32(ReadOnlySpan<byte>) → int[]`
  - `ReadPackedZigzag64(ReadOnlySpan<byte>) → long[]`

**DenseNodes フィールドマップ**（proto2 osmformat.proto）：

| Field | 型 | wire-type | 扱い |
|---|---|---|---|
| 1 id | repeated sint64 (packed, delta-coded) | LengthDelimited | `PackedReader.ReadPackedZigzag64` → in-place 累積 |
| 5 denseinfo | DenseInfo (optional) | LengthDelimited | スキップ（Phase 2 で metadata 不要） |
| 8 lat | repeated sint64 (packed, delta-coded) | LengthDelimited | 同上、累積後 `block.ToLat` |
| 9 lon | repeated sint64 (packed, delta-coded) | LengthDelimited | 同上、累積後 `block.ToLon` |
| 10 keys_vals | repeated int32 (packed, 0-separated) | LengthDelimited | `ReadPackedUint32` → `DistributeTags` |

**keys_vals フォーマット**（仕様）：

各ノードについて `(key1, val1, key2, val2, ..., 0)` を並べる。0 がそのノードのタグ列の終端マーカー。例：3 ノードで Node0=2 tags / Node1=1 tag / Node2=tagless の場合：

```text
[k1, v1, k2, v2, 0,  k3, v3, 0,  0]
```

全ノードが tagless なら配列自体を出さない省略形が許される（field 10 を出力しない）。明示的に [0, 0, 0] と書くことも可能（仕様準拠の両方をサポート）。

**検証ルール**：

- `id.Length`, `lat.Length`, `lon.Length` が一致しなければ `InvalidDataException`
- keys_vals 中で `(key, val)` ペアの値部が欠落（mid-tag truncation）→ `InvalidDataException`
- ノードあたり 0 区切りが欠落（"missing 0-separator for node index N"）→ `InvalidDataException`
- 全ノードを使い切った後に余剰要素（"trailing element(s)"）→ `InvalidDataException`

#### 4.6.3 設計判断の根拠

- **`OsmNode[]` を返す（IEnumerable ではなく）**：v0.1 シンプル優先。`ReadOnlySpan<byte>` 入力からの `IAsyncEnumerable<OsmNode>` 化はステップ 2.10 / Phase 3 で検討
- **delta デコードを in-place で実行**：別配列を確保せず ids/lats/lons をそのまま累積バッファとして再利用。中ブロック数千ノード規模でも GC 圧最小
- **`PackedReader` を新設**：DenseNodes / Node / Way / Relation 全てで packed 配列を扱う。3 つの場所に同じヘルパーを持つより共有が自然。OsmNodeParser もこの機会に refactor して整合性確保
- **`DistributeTags` でリスト再利用**：単一の `keysList` / `valsList` を `Clear` して再利用し、ノードごとの確保は `ToArray()` 時のみ。Phase 3 で `ArrayPool<int>` 化候補
- **tagless ノードに `Array.Empty<int>()` を使う**：共有インスタンス、ヒープ確保ゼロ
- **採用しなかった案**：
  - **`DenseNodes` という record 型を作る**：解析結果は単に `OsmNode[]` で十分。中間構造を作る理由なし
  - **`IAsyncEnumerable<OsmNode>` での streaming**：Phase 2 では PrimitiveBlock 全体を一括処理する想定、streaming のメリットが薄い
  - **`Span<long>` で delta デコードを表現**：内部実装の都合上 long[] を返す必要があるため、Span ベース API は構造化が複雑
  - **DenseInfo を保持**：version / timestamp / changeset / user は Phase 2 routing 用途で不要、YAGNI

#### 4.6.4 トレードオフ・制約

- **ノードあたり `int[]` 2 個（TagKeys / TagValues）**：1 ブロック ~8000 ノードで最大 16000 配列確保（tagged ノードのみ）。Phase 3 で `ArrayPool` or 共有バッファ + オフセット方式に最適化検討
- **入力配列が大きい場合の `List<long>` growth**：100K ノード規模では `List<long>.Add` の容量倍々増の累計コストが無視できなくなる可能性。Phase 3 で「サイズ事前計算 → 一括確保」に最適化検討
- **`keys_vals` の int[] 確保**：1 ブロック数千タグ規模、bigO で問題なし。値域 < uint32 だが int に格納
- **delta デコードの overflow**：絶対 id / encodedLat / encodedLon は long 範囲。OSM の実値は ~10^10 以下、累積 overflow なし

#### 4.6.5 検証方法

`tests/OsmDotRoute.Tests/Pbf/Osm/DenseNodesParserTests.cs`（17 件、全 pass、2026-05-21）：

- **正常系**：空 / 単一ノード / 複数ノード delta デコード / 負 id zigzag / 負座標 / タグ分配（3 ノード混合）/ 全 tagless 省略形 / 全 tagless 明示 [0,0,0] / DenseInfo スキップ / 未知 field スキップ / オフセット適用
- **異常系**：id/lat/lon 長さ不一致 / keys_vals truncated mid-tag / keys_vals 区切り欠落 / keys_vals trailing data / id wire-type 不一致
- **引数検証**：null block

Phase 1 既存 153 件 + Pbf 既存 104 件 + Osm.DenseNodes 17 件 = **274/274 全 pass**、回帰なし。

#### 4.6.6 実装メモ

- delta デコードのループは 3 配列を 1 ループで処理（id/lat/lon を同じインデックス変数で並走）→ キャッシュフレンドリー
- `DistributeTags` は `keysVals.Length == 0` の早期 return パスを持つ（全 tagless 省略形）
- `PackedReader.ReadPackedUint32` は値域 0..2^31-1 の uint32 として読む。`keys_vals` の値は実用上 < 65K（StringTable サイズ）、安全
- テストの `BuildDenseNodes` ヘルパーは `ToDeltas` で絶対値→delta、`PackZigzag` で zigzag varint 化を行う。テストとパーサーで形式が往復一致することを確認
- OsmNodeParser リファクタリングはチャンク小（`ReadPackedUint32` 6 行削除 + import 整理）、行動変化なし、テスト 257/257 で確認

### 4.7 ステップ 2.8: Way 解析（2026-05-21 実装）

#### 4.7.1 意図

OSM PBF の Way メッセージを解析する。Way は道路ネットワーク抽出の **中核データ**：Phase 2 抽出ツールは `highway=*` タグでフィルタし、`NodeRefs` を頂点列として使用する（計画書 §6 ステップ 2.8）。

#### 4.7.2 採用設計

**型構成**（`OsmDotRoute.Pbf.Osm`、全 internal）：

- `OsmWay` sealed record：`(Id, NodeRefs, TagKeys, TagValues)`
  - `NodeRefs` は delta デコード済の **絶対 OSM Node ID** 列（long[]）
  - `TagKeys` / `TagValues` は `OsmStringTable` インデックスの `int[]`
- `OsmWayParser` static class：
  - `Parse(ReadOnlySpan<byte> wayBytes) → OsmWay`
  - `PrimitiveBlock` 不要（座標変換なし、tag 解決は呼出側）

**Way フィールドマップ**（proto2 osmformat.proto）：

| Field | 型 | wire-type | 扱い |
|---|---|---|---|
| 1 id | int64 (required) | Varint | `unchecked((long)ReadVarint64())` — plain varint、zigzag ではない |
| 2 keys | repeated uint32 (packed) | LengthDelimited | `PackedReader.ReadPackedUint32` |
| 3 vals | repeated uint32 (packed) | LengthDelimited | 同上 |
| 4 info | Info (optional) | LengthDelimited | スキップ |
| 8 refs | repeated sint64 (packed, delta) | LengthDelimited | `ReadPackedZigzag64` → in-place 累積 |
| 9 lat | repeated sint64 (LocationsOnWays 拡張) | LengthDelimited | スキップ |
| 10 lon | repeated sint64 (LocationsOnWays 拡張) | LengthDelimited | スキップ |

**検証ルール**：

- `id` 欠落 → `InvalidDataException`
- `keys.Length != vals.Length` → `InvalidDataException`
- packed 専用（refs / keys / vals が `Varint` で来ると `InvalidDataException`）
- 未知 field number / Info / LocationsOnWays 拡張は黙ってスキップ

#### 4.7.3 設計判断の根拠

- **`block` を受け取らない API**：Way には座標変換対象（lon/lat）が無く、`tags` は StringTable インデックスのまま返して呼出側で解決させる。Node / DenseNodes と引数シグネチャが異なるのは意図的
- **`Way.id` は `int64` plain varint**：proto 定義が `int64`（`sint64` ではない）。実用上 OSM Way ID は正の連番（現在 ~12 億）、`unchecked` cast で long 互換
- **`refs` の delta デコードを in-place 実行**：DenseNodes と同じパターン。GC 圧最小
- **`lat`/`lon` (field 9/10) を意図的スキップ**：LocationsOnWays は Geofabrik PT などの拡張形式で、本 PBF 抽出フローでは別途 DenseNodes からノード位置を取得する。Phase 2 で対応する必要なし
- **採用しなかった案**：
  - **`Info` の version/timestamp を保持**：Phase 2 routing 用途で metadata 不要、YAGNI
  - **`refs` を `IReadOnlyList<long>`**：long[] の直接公開で十分。LINQ も使えるし、`Length` / インデクサで足りる
  - **`keys` / `vals` を `(int key, int val)[]`**：分離配列の方が PBF 仕様に近く、ペア化のオーバーヘッドなし
  - **non-packed 対応**：現代 PBF で見ない、実装複雑化を避ける（Node と同じ判断）

#### 4.7.4 トレードオフ・制約

- **`long[] NodeRefs` のヒープ確保**：1 way あたり平均 ~100 refs、津島市規模で数万 way × 100 = 数百万 long。8 バイト × 数百万 = 数十 MB の一時メモリ。Phase 3 で `ArrayPool<long>` 化検討
- **`int[]` Tag 配列**：tag 数は way あたり通常 < 20 個。問題なし
- **delta デコードの overflow**：Way refs の絶対値は OSM Node ID 範囲（< 10^10）、累積 overflow なし

#### 4.7.5 検証方法

`tests/OsmDotRoute.Tests/Pbf/Osm/OsmWayParserTests.cs`（16 件、全 pass、2026-05-21）：

- **正常系**：最小 Way / refs delta / tags / 全フィールド組合せ / 負 delta refs / 大 Way ID (10^10) / Info スキップ / LocationsOnWays スキップ / 未知 field / 空 refs / 空 keys-vals
- **異常系**：id 欠落 / keys-vals 長さ不一致 / id wire-type / refs wire-type / keys wire-type
- **引数検証**: なし（block 引数なし、null 引数なし）

Phase 1 既存 153 件 + Pbf 既存 121 件 + Osm.Way 16 件 = **290/290 全 pass**、回帰なし。

#### 4.7.6 実装メモ

- `Way.id` の `unchecked((long)reader.ReadVarint64())`：sign-extended 10-byte varint 復号。OSM の Way ID は正のため通常 ≤ 10 バイト、cast は no-op
- delta デコードのループは「`current += refs[i]; refs[i] = current`」の単純加算 → JIT が SIMD 化しやすい
- field 9/10（LocationsOnWays）は単純な `SkipField` で対応。フィールド番号は switch case の case 9: case 10: でフォールスルーパターン
- テストの `BuildWay` は Node テストと類似だが、id が int64 plain varint なので `unchecked((ulong)id)` 経由で書く。Way の zigzag ではない点に注意

### 4.8 ステップ 2.9: Relation 解析（2026-05-21 実装）

#### 4.8.1 意図

OSM PBF の Relation メッセージを解析する。Phase 2 抽出フローでは Relation を使わない（`type=restriction` はターン制限で Phase 4+ 延期、§5.6-18 計画書）が、仕様完全性のために実装する。Phase 4+ で多角形ルートやターン制限を扱う際の土台となる（計画書 §6 ステップ 2.9）。

#### 4.8.2 採用設計

**型構成**（`OsmDotRoute.Pbf.Osm`、全 internal）：

- `OsmMemberType` enum：`Node = 0` / `Way = 1` / `Relation = 2`（PBF MemberType と一対一対応）
- `OsmRelationMember` readonly record struct：`(MemberId, Type, RoleStringIndex)`
- `OsmRelation` sealed record：`(Id, Members, TagKeys, TagValues)`
- `OsmRelationParser` static class：`Parse(ReadOnlySpan<byte>) → OsmRelation`

**Relation フィールドマップ**（proto2 osmformat.proto）：

| Field | 型 | wire-type | 扱い |
|---|---|---|---|
| 1 id | int64 (required) | Varint | plain varint |
| 2 keys | repeated uint32 (packed) | LengthDelimited | StringTable インデックス |
| 3 vals | repeated uint32 (packed) | LengthDelimited | 同上 |
| 4 info | Info (optional) | LengthDelimited | スキップ |
| 8 roles_sid | repeated int32 (packed) | LengthDelimited | role 文字列の StringTable インデックス |
| 9 memids | repeated sint64 (packed, delta) | LengthDelimited | `ReadPackedZigzag64` → in-place 累積 |
| 10 types | repeated MemberType (packed varint) | LengthDelimited | `ReadPackedUint32`、0/1/2 検証 |

**3 つの member 並列配列**：`roles_sid` / `memids` / `types` は同じ長さで、インデックス i が i 番目メンバーを構成する。

**検証ルール**：

- `id` 欠落 → `InvalidDataException`
- `keys.Length != vals.Length` → `InvalidDataException`
- `roles_sid.Length / memids.Length / types.Length` が一致しなければ `InvalidDataException`
- MemberType が 0/1/2 以外 → `InvalidDataException`（未来の拡張時はパーサー更新が必要）
- packed 専用、wire-type 違反は `InvalidDataException`

#### 4.8.3 設計判断の根拠

- **`OsmRelationMember` を `readonly record struct`**：3 フィールド × 16 バイト程度、値セマンティクス自然。配列要素として GC 圧最小
- **`OsmMemberType` enum で raw int を型安全化**：パーサー段で 0/1/2 を厳密検証し、ランタイムで未定義値が出ない保証
- **3 並列配列の長さ一致を強制**：PBF 仕様では「3 つの配列は同じ長さ」が暗黙の前提。1 つでも欠ければ malformed data として早期失敗
- **`memids` の delta デコードを in-place 実行**：Way / DenseNodes と同じパターン、GC 圧最小
- **MemberType 未定義値を即時拒否**：将来 PBF 仕様拡張で 3+ が登場したらパーサー更新が必要。Phase 2 ではフェールセーフ動作（不正データを通さない）優先
- **採用しなかった案**：
  - **`OsmRelationMember` を class**：値型のメリット（配列内連続配置、コピーセマンティクス）を捨てる理由なし
  - **`Type` を生の int で保持**：呼出側で型変換が必要、API が腐る
  - **未定義 MemberType を `Unknown` 値として通す**：PBF データの整合性チェックを緩める。Phase 2 ではメンバー型を使わないが、Phase 4+ で多角形ルートを扱う際に問題化する可能性
  - **`Info` を保持**：Phase 4+ でも Relation の metadata は使わない見込み、YAGNI

#### 4.8.4 トレードオフ・制約

- **`OsmRelationMember[]` ヒープ確保**：Relation は OSM 全体で数百万件、典型的なメンバー数は数〜数百。Phase 4+ で性能評価
- **未定義 MemberType の厳密拒否**：将来の PBF 仕様で MemberType が拡張された場合、本パーサーは更新必須。fail-loud で気付ける利点
- **3 並列配列の同期 enforcement**：あるエンコーダーが 1 つだけ書き出すような場合、本パーサーは拒否する。仕様準拠で問題なし

#### 4.8.5 検証方法

`tests/OsmDotRoute.Tests/Pbf/Osm/OsmRelationParserTests.cs`（16 件、全 pass、2026-05-21）：

- **正常系**：最小 Relation / メンバー delta + 全 type / 全 MemberType / 負 delta / tags / 全フィールド組合せ / Info スキップ / 未知 field / 空 members / **`type=restriction` 形式の典型** (3 members: from way / via node / to way + 2 tags)
- **異常系**：id 欠落 / keys-vals 長さ不一致 / member 3 配列長さ不一致 / 未定義 MemberType=3 / id wire-type / memids wire-type

Phase 1 既存 153 件 + Pbf 既存 137 件 + Osm.Relation 16 件 = **306/306 全 pass**、回帰なし。

#### 4.8.6 実装メモ

- `types` フィールドの読込は `PackedReader.ReadPackedUint32`（MemberType enum は int32 varint、値域 0..2 で十分）
- MemberType の switch expression は `_ => throw` で網羅性チェック有効化。コンパイラ警告で未対応ケースを検知できる
- テスト `Parse_TurnRestrictionRelation_ParsedAsTagsAndMembers` は Phase 4+ のターン制限処理の入力形式が現時点で正しく解析できることを確認（実際の制限ロジックは Phase 4+ で実装）
- テストの `BuildRelation` は member 配列を絶対値で受け取り、内部で `ToDeltas` で delta 化する（Way / DenseNodes と同じパターン）

### 4.9 ステップ 2.10 以降の予定（未着手）

- 2.10 高レベル `PbfReader` API + 津島市 PBF で Itinero と Node / Way / Relation 数を突合する単体テスト
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
| 0.2.3 (draft) | 2026-05-21 | ステップ 2.4 完了反映。§4.3 に「HeaderBlock 解析」（`OsmBoundingBox` record struct / `OsmHeader` record / `OsmHeaderParser` static class、`SupportedRequiredFeatures = {OsmSchema-V0.6, DenseNodes}`）の意図・採用設計・判断根拠・トレードオフ・検証方法を記述。§4 ステータスを「2.1〜2.4 完了」に更新、旧 §4.3 を §4.4「2.5 以降の予定」へ繰下げ。採用しなかった案（class 化 / OrdinalIgnoreCase / replication 保持）の根拠も明記 | Claude (Opus 4.7) |
| 0.2.4 (draft) | 2026-05-21 | ステップ 2.5 完了反映。§4.4 に「PrimitiveBlock + stringtable 解析」（`OsmStringTable` / `PrimitiveBlock` / `PrimitiveBlockParser`、座標変換式 `lon = 1e-9 × (LonOffset + Granularity × encoded)`、PrimitiveGroup はスキップ）の意図・採用設計・判断根拠・トレードオフ・検証方法を記述。§4 ステータスを「2.1〜2.5 完了」に更新、旧 §4.4 を §4.5「2.6 以降の予定」へ繰下げ。採用しなかった案（オフセット配列方式 / PrimitiveGroup 部分解析 / struct extension method）の根拠も明記 | Claude (Opus 4.7) |
| 0.2.5 (draft) | 2026-05-21 | ステップ 2.6 完了反映。§4.5 に「単体 Node 解析」（`OsmNode` record / `OsmNodeParser` static、packed uint32 デコード、Info スキップ、座標は block.ToLon/ToLat 適用）の意図・採用設計・判断根拠・トレードオフ・検証方法を記述。§4 ステータスを「2.1〜2.6 完了」に更新、旧 §4.5 を §4.6「2.7 以降の予定」へ繰下げ。採用しなかった案（OsmTagCollection 型 / 非 packed 対応 / Info 保持）の根拠も明記 | Claude (Opus 4.7) |
| 0.2.6 (draft) | 2026-05-21 | ステップ 2.7 完了反映。§4.6 に「DenseNodes 解析」（`DenseNodesParser` static、delta-coded zigzag varint × 3 配列 + keys_vals 0 区切り、`PackedReader` 新設して `OsmNodeParser` も refactor）の意図・採用設計・判断根拠・トレードオフ・検証方法を記述。§4 ステータスを「2.1〜2.7 完了」に更新、旧 §4.6 を §4.7「2.8 以降の予定」へ繰下げ。採用しなかった案（DenseNodes record 型 / IAsyncEnumerable streaming / Span ベース delta / DenseInfo 保持）の根拠も明記 | Claude (Opus 4.7) |
| 0.2.7 (draft) | 2026-05-21 | ステップ 2.8 完了反映。§4.7 に「Way 解析」（`OsmWay` record / `OsmWayParser` static、id は int64 plain varint、refs は packed sint64 delta-coded zigzag、PrimitiveBlock 不要、LocationsOnWays 拡張スキップ）の意図・採用設計・判断根拠・トレードオフ・検証方法を記述。§4 ステータスを「2.1〜2.8 完了」に更新、旧 §4.7 を §4.8「2.9 以降の予定」へ繰下げ。採用しなかった案（Info 保持 / IReadOnlyList 化 / (key,val)[] ペア / non-packed 対応）の根拠も明記 | Claude (Opus 4.7) |
| 0.2.8 (draft) | 2026-05-21 | ステップ 2.9 完了反映。§4.8 に「Relation 解析」（`OsmMemberType` enum / `OsmRelationMember` record struct / `OsmRelation` record / `OsmRelationParser` static、3 並列配列 roles_sid/memids/types の同期検証、memids delta デコード、MemberType 0/1/2 厳密検証）の意図・採用設計・判断根拠・トレードオフ・検証方法を記述。§4 ステータスを「2.1〜2.9 完了」に更新、旧 §4.8 を §4.9「2.10 以降の予定」へ繰下げ。採用しなかった案（class 化 / 生 int 保持 / Unknown 値許容 / Info 保持）の根拠も明記 | Claude (Opus 4.7) |
