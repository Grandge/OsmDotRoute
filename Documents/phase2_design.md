# OsmDotRoute Phase 2 設計書

**バージョン**: 0.3（ステップ 2 全完了反映）
**作成日**: 2026-05-20（v0.1 として）
**最終更新**: 2026-05-21（v0.3、§4.9 ステップ 2.10 完了 + §4.10 ステップ 2 完了状況サマリ追加）
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

**ステータス**: ステップ 2 全完了（2.1〜2.10）。`OsmDotRoute.Pbf` は System.\* 完結で OSM PBF を完全読込可能。次はステップ 3。

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

### 4.9 ステップ 2.10: 高レベル PbfReader API + 津島市 PBF 統合テスト（2026-05-21 実装）

#### 4.9.1 意図

これまでのステップ 2.1〜2.9 で構築した低レベル要素（`PbfBlobReader` / `OsmHeaderParser` / `PrimitiveBlockParser` / `OsmNodeParser` / `DenseNodesParser` / `OsmWayParser` / `OsmRelationParser`）を統合し、PBF ファイル 1 走査で Node / Way / Relation を供給する高レベル API を提供する。実装計画書 §6 ステップ 2 の **最終ステップ**で、Phase 2 ステップ 2 完了を意味する。

#### 4.9.2 採用設計

**API**（`OsmDotRoute.Pbf.PbfReader`、internal static class）：

```csharp
public static OsmHeader Read(
    Stream stream,
    Action<OsmNode, OsmStringTable>? onNode = null,
    Action<OsmWay, OsmStringTable>? onWay = null,
    Action<OsmRelation, OsmStringTable>? onRelation = null,
    bool leaveOpen = false);
```

- ストリームを 1 回走査して全要素をコールバックで供給
- 戻り値の `OsmHeader` は `EnsureSupported` 済（未サポート機能で例外）
- null コールバックは該当セクションをスキップ（マルチパス処理に対応）
- `OsmStringTable` をコールバックに渡すことで、Way / Node の tag インデックスを呼出側で解決可

**内部処理フロー**：

1. `PbfBlobReader.MoveNext()` で先頭 blob を取得 → OSMHeader でなければ `InvalidDataException`
2. `OsmHeaderParser.Parse` + `EnsureSupported`
3. 以降の blob を順次処理：
   - `PbfBlobType.Data` → `ProcessPrimitiveBlock`
   - `PbfBlobType.Header` 重複 → `InvalidDataException`
   - `PbfBlobType.Unknown` → スキップ（前方互換）
4. `ProcessPrimitiveBlock`：blob ペイロードを **2 パススキャン**
   - Pass 1: `PrimitiveBlockParser.Parse` でエンベロープ（StringTable + granularity + offsets）取得
   - Pass 2: 同じバイト列を `ProtoReader` で再走査し、PrimitiveGroup (field 2) を抽出
5. `ProcessPrimitiveGroup`：各フィールドを判定し、対応するパーサーに dispatch
   - field 1: Node[] → `OsmNodeParser.Parse` × N
   - field 2: DenseNodes → `DenseNodesParser.Parse` → 展開された `OsmNode[]` を 1 件ずつコールバック
   - field 3: Way[] → `OsmWayParser.Parse` × N
   - field 4: Relation[] → `OsmRelationParser.Parse` × N
   - field 5 (changesets) / その他 → スキップ

#### 4.9.3 設計判断の根拠

- **コールバックパターン（Visitor）**：`IEnumerable<OsmElement>` 方式と比較して、(1) 値型 OsmNode / OsmRelationMember のボックス化回避、(2) yield return 不要でリソース管理シンプル、(3) null callback でセクションスキップ可能、(4) マルチパス処理（pass1 = node, pass2 = way）が自然
- **2 パススキャンの容認**：PrimitiveBlockParser がエンベロープ解析時に PrimitiveGroup をスキップする設計のため、PbfReader 側で再走査が必要。バイト列はメモリ上にあるため再走査は varint デコードのみ（IO なし）、コスト軽微
- **`leaveOpen` パラメータ**：呼出側が複数回 `Read` を呼ぶマルチパス処理を想定（FileStream を 1 回開いて使い回すケースは現状想定なし、毎回 open/close）
- **`EnsureSupported` を自動適用**：未サポート PBF（HistoricalInformation 等）を早期に弾く。診断ツール用途で「サポート外でも内容を見たい」場合は、`OsmHeaderParser.Parse` + 個別パーサーを直接呼ぶことで bypass 可能
- **採用しなかった案**：
  - **`IEnumerable<OsmElement>` での streaming**：OsmElement の discriminated union を `record` で表現するとボックス化が発生。マルチパス処理（節 4.9.5）と相性が悪い
  - **`IPbfVisitor` interface**：delegate より overhead 高、null チェックも明示的でない。Phase 2 の internal API では delegate で十分
  - **`PbfReader` を class（インスタンスベース）**：状態を持つメリットなし、static で十分
  - **PrimitiveGroup 単一パススキャン**：PrimitiveBlockParser を再設計する必要があり、ステップ 2.5 のスコープ越境。再走査コストが軽微なため YAGNI

#### 4.9.4 トレードオフ・制約

- **2 パス再走査のオーバーヘッド**：1 PrimitiveBlock あたり最大 32 MB を 2 回走査。実測（津島市 13 MB PBF）で 13 秒以内完了、許容範囲
- **DenseNodes が `OsmNode[]` を一括確保**：1 block 最大 8000 ノード × OsmNode 容量。Phase 3 でストリーミング化検討（メモリ [[project-phase2-dynamic-restriction-design]]）
- **`Action<T1, T2>` delegate のオーバーヘッド**：呼び出しごとに delegate invoke。OSM 全要素 ~700M × delegate invoke は GC 影響なし、Phase 3 で必要なら function pointer 化検討
- **マルチパスでのストリーム再 open**：抽出ツールが 3 パススキャン（節 5.3.3 仕様書）を行う場合、FileStream を 3 回開く必要がある。FileStream はカーネルで効率化されており問題なし

#### 4.9.5 検証方法

**単体テスト** `tests/OsmDotRoute.Tests/Pbf/PbfReaderTests.cs`（12 件、全 pass、2026-05-21）：

- **制御フロー**：null stream / 空 stream / 先頭非 Header blob / Header 重複 / 未サポート required_feature
- **正常系**：Header のみ / Header + 空 data / Way 1 件入り / Relation 1 件入り / null コールバックでセクションスキップ / 複数 data blob / leaveOpen=true でストリーム継続

**統合テスト** `tests/OsmDotRoute.Tests/Pbf/PbfReaderIntegrationTests.cs`（4 件、全 pass、2026-05-21）：

- **Header bbox + required_features**：津島市 PBF (13 MB) の bbox が愛知県北西部 (lon 136.5-137.0 / lat 35.0-35.3) に収まる、required_features に "OsmSchema-V0.6" / "DenseNodes" を含む
- **要素数突合**：PbfReader と OsmSharp 6.2.0 (Itinero.IO.Osm 経由) でNode 1,646,875 件 / Way 数千件 / Relation 数百件が完全一致
- **座標一致**：先頭 1000 ノードの (lon, lat) が OsmSharp 出力と precision: 7 桁で完全一致
- **highway 抽出可能性**：`highway=*` タグを持つ way が 1000 件以上検出される（道路ネットワーク抽出の妥当性確認）

**統合テスト用データ**：`d:\workspace\災害廃棄物処理シミュレーション\App\DisasterWasteSim.Server\Data\tsushima_extract.osm.pbf`（親プロジェクト所有、13 MB）。`TestPaths.TsushimaExtractPbf` 経由で参照し、ファイル不在時は `Assert.Fail` で fail-loud（Phase 1 の `SnapToRoadTests` パターン踏襲）。

Phase 1 既存 153 件 + Pbf 既存 153 件 + PbfReader 12 件 + PbfReader 統合 4 件 = **322/322 全 pass**（13 秒、津島市 PBF 統合テストを含む）、回帰なし。

#### 4.9.6 実装メモ

- 統合テストで OsmSharp 6.2.0 を基準実装として使用。OsmSharp は Itinero.IO.Osm 1.5.1 の依存として既に NuGet キャッシュにあり、`OsmDotRoute.Tests` から transitive で参照可能
- DenseNodes の `byte[][]` StringTable 確保 × 大量ブロックでメモリピークが懸念だったが、津島市 PBF（StringTable 1 ブロックあたり ~10K 要素 × 数百ブロック）でも問題なく完走（13 秒、メモリピーク数百 MB 想定）
- 統合テストで `Read_TsushimaPbf_NodesInBoundingBox`（bbox + 5km マージン内に 99% ノードが収まるか）を当初予定したが、OSM PBF は way 完全性のため bbox 外ノードを含むのが標準動作（68% が bbox 内に収まる程度）。テスト方針を「OsmSharp との座標一致」に変更し、coord decoding 正確性をより直接的に検証
- 統合テスト 1 件あたり ~1 秒、計 4 件で約 4 秒の追加時間。CI / 開発フローで許容範囲

### 4.10 ステップ 2 完了状況のまとめ

ステップ 2.1〜2.10 全完了（2026-05-21）。`OsmDotRoute.Pbf` プロジェクトは System.\* 完結で OSM PBF を完全読込可能になり、Phase 2 計画書 §6 ステップ 2 の要件 REQ-MAP-007 / REQ-DEP-002 を満たす。

| サブステップ | 内容 | テスト |
|---|---|---|
| 2.1 | プロジェクト骨格 | — |
| 2.2 | ProtoReader (protobuf ワイヤ形式) | 27 件 |
| 2.3 | PbfBlobReader (Blob 層 + ZLib 解凍) | 25 件 |
| 2.4 | OsmHeaderParser | 18 件 |
| 2.5 | PrimitiveBlockParser + OsmStringTable | 19 件 |
| 2.6 | OsmNodeParser (単体 Node) | 15 件 |
| 2.7 | DenseNodesParser | 17 件 |
| 2.8 | OsmWayParser | 16 件 |
| 2.9 | OsmRelationParser | 16 件 |
| 2.10 | PbfReader (高レベル API) + 津島市統合 | 16 件 |
| **合計** | — | **169 件** (Phase 1: 153 + Pbf: 169 = 322 件全 pass) |

次はステップ 3 (PBF → `.odrg` 抽出ツール `OsmDotRoute.Extractor`) に進む。

---

## 5. PBF → `.odrg` 抽出ツール `OsmDotRoute.Extractor`

**対応ステップ**: ステップ 3（サブステップ 3.1〜3.9 に分割）
**対応要件**: REQ-MAP-008
**Phase 1 申し送り**: なし（Phase 2 で前倒し実装）

**ステータス**: サブステップ 3.1〜3.5 完了 / 3.6〜3.9 未着手

**ステップ 3 サブステップ分割**（2026-05-25 ユーザー合意）：

| サブ | 内容 | 検証 |
|---|---|---|
| 3.1 | プロジェクト骨格 + `System.CommandLine` v3 雛形 + `extract` サブコマンド | CLI パース 11 テスト |
| 3.2 | 道路 way フィルタ（`highway=*` 取込 + `access=no` / `area=yes` 除外、§5.6-17 計画書） | フィルタ判定単体テスト |
| 3.3 | 頂点正規化（Way 参照回数 ≥ 2 のノードを頂点、中間ノードをシェイプ） | 小規模 PBF で頂点数検証 |
| 3.4 | エッジ生成（`fromVertexId` / `toVertexId` / shape / `oneway` 抽出） | エッジ単体テスト |
| 3.5 | エッジ AABB 計算（`double` × 4） + エッジフラグ bake（14 bit、仕様書 §4.5） | bake 内容テスト |
| 3.6 | bake プロファイル表（`car` / `pedestrian` で `(canPass, speedKmh, oneway)`、Phase 1 `ProfileEvaluator` 流用） | プロファイル別 bake テスト |
| 3.7 | STR R-tree 構築（M=16、配列化シリアライズ、仕様書 §4.6） | 既知クエリ一致 |
| 3.8 | `.odrg` 書出（仕様書 §1〜§4 のレイアウト準拠、ヘッダー 256B → セクションテーブル → 各セクション本体） | 仕様書通りのバイナリ生成 |
| 3.9 | 津島市 PBF 統合テスト（Phase 1 RouterDb と頂点数 / 辺数 / 経緯度範囲突合） | パリティテスト |

### 5.1 サブステップ 3.1: CLI 雛形（2026-05-25 完了）

**意図**：抽出パイプライン本体（3.2 以降）の入口となる CLI ツリーを先に確定し、入力検証・ヘルプ表示・テスト可能性を担保。3.2 以降はこの CLI から `ExtractOptions` を受け取って実装する。

**採用設計**：

- **プロジェクト**: `src/OsmDotRoute.Extractor/`、`OutputType=Exe`、`AssemblyName=osmdotroute-extractor`、net9.0
- **依存**: `System.CommandLine` v3 preview（`3.0.0-preview.4.26230.115`）+ `OsmDotRoute` + `OsmDotRoute.Pbf`
- **公開度**: テスト側からは `InternalsVisibleTo("OsmDotRoute.Tests")` で internal API を直接検証
- **CLI 構造**:

  ```text
  osmdotroute-extractor extract
    --input  <file.osm.pbf>          (REQUIRED, -i)
    --output <file.odrg>             (REQUIRED, -o)
    --bbox   minLon,minLat,maxLon,maxLat  (REQUIRED、仕様書 §5.3.1)
   [--profiles car,pedestrian]       (-p、デフォルト car,pedestrian)
  ```

- **モジュール構成**:
  - [src/OsmDotRoute.Extractor/Program.cs](../src/OsmDotRoute.Extractor/Program.cs): エントリポイント。`RootCommand` 構築 → `Parse(args).Invoke()`
  - [src/OsmDotRoute.Extractor/Cli/ExtractCommand.cs](../src/OsmDotRoute.Extractor/Cli/ExtractCommand.cs): `extract` サブコマンド組立。System.CommandLine v3 の `CustomParser` プロパティで bbox を `Bbox` 型に変換
  - [src/OsmDotRoute.Extractor/Cli/ExtractOptions.cs](../src/OsmDotRoute.Extractor/Cli/ExtractOptions.cs): 確定済みパラメータの DTO（`record`）
  - [src/OsmDotRoute.Extractor/Cli/Bbox.cs](../src/OsmDotRoute.Extractor/Cli/Bbox.cs): `readonly record struct`。`Parse(string)` で 4 値カンマ区切り検証（経緯度範囲・min<max）
- **handler**: `Func<ExtractOptions, int> onExtract` を `Build()` に注入する設計。`Program.cs` 側で抽出処理を差し替え可能 + テスト側でキャプチャ可能

**判断根拠**：

- **System.CommandLine v3 採用**: v2 beta4 は 2022 年版で古い。v3 preview は新しいが API がほぼ確定済 (`CustomParser` プロパティ、`SetAction` 等) で、新規プロジェクトに v2 を選ぶ理由がない
- **`--bbox` を必須 + デフォルト省略**: 仕様書 §5.3.1 v0.1.3「`--bbox` 未指定時はエラーで中断、Phase 2 要件外の Japan-wide `.odrg` 誤生成を不可能にする」を CLI 層で機械的に保証
- **`Bbox` を `readonly record struct`**: ホットパスではないが、シリアライズ・ハッシュ・equality を `record` で自動生成、struct で値型として渡す
- **handler 注入パターン**: `ExtractCommand.Build(Func<ExtractOptions, int>)` でハンドラを外から差し込む形にすると、テスト時に副作用なくキャプチャできる（テスト 5 件で活用）
- **`-i` / `-o` / `-p` 短縮形**: 計画書 §3.4 のサンプル CLI と一致

**採用しなかった案**：

- **v2 beta API**: 2022 年で更新停滞、v3 がほぼ確定している今、新規プロジェクトには不適
- **`--bbox` を 4 つの個別オプション (`--bbox-min-lon` 等)**: 仕様書 §5.3.1 v0.1.3 のサンプル CLI と乖離、4 つも書かせるのは UX 悪
- **ヘルプを英語**: CLAUDE.md「全ての記述・対話は日本語」に従い、ヘルプ・エラーメッセージは日本語

**検証方法**：

- `tests/OsmDotRoute.Tests/Extractor/BboxTests.cs`（10 ケース）: `Bbox.Parse` の有効/無効値、範囲外、反転、フィールド数不一致
- `tests/OsmDotRoute.Tests/Extractor/ExtractCommandTests.cs`（5 ケース）: 全必須指定で handler 呼出、profiles カスタム、`--input` 欠落、`--bbox` 欠落、`--bbox` フォーマット不正 → 各々の `result.Errors` を検証
- スモークテスト: `dotnet run -- --help` / `-- extract --help` / `-- extract --input ... --output ... --bbox ...` を手動実行、すべて期待動作
- 全体テスト: 337/337 pass（Phase 1: 153 + Pbf: 169 + Extractor: 15）

### 5.2 サブステップ 3.2: 道路 way フィルタ（2026-05-25 完了）

**意図**：PBF を流すホットパスで「道路ネットワーク抽出対象の way か」を低コストに判定する。3.3 以降の頂点正規化・エッジ生成に渡す前段。

**採用設計**：

- [src/OsmDotRoute.Extractor/Pipeline/WayFilter.cs](../src/OsmDotRoute.Extractor/Pipeline/WayFilter.cs) を新設、`internal static class`
- API: `bool WayFilter.IsRoadWay(OsmWay way, OsmStringTable stringTable)`
- 判定ロジック（計画書 §5.6-17 に準拠）:
  - `highway` タグを持つ（値は問わない）
  - かつ `access=no` を持たない
  - かつ `area=yes` を持たない
- **ゼロアロケート実装**:
  - 比較キー (`highway` / `access` / `area`) と値 (`no` / `yes`) は `static ReadOnlySpan<byte> X => "..."u8;` の C# 11 UTF-8 リテラル
  - `OsmStringTable.GetBytes(int)` は `byte[]` の `ReadOnlySpan` ビューを返すためコピーなし
  - 比較は `ReadOnlySpan<byte>.SequenceEqual` で JIT 最適化
  - 文字列デコード・`Dictionary` を一切使わない

**判断根拠**：

- **「広めに採用」方針** (計画書 §5.6-17): `highway=*` は値を問わず採用（`proposed` / `construction` / `track` 等も含む）。プロファイル側 (3.6 / Phase 3) で絞る前提なので、ここで弾くと Bicycle / Truck プロファイル追加時に拾えなくなる
- **`access=private` は除外しない**: 計画書方針通り「フィルタは緩く」。`private` は車種・用途で扱いが変わるためプロファイル側判定に委譲
- **`area=yes` の除外**: 歩行者プラザ・駐車場等の閉合 polygon を線形 way として誤って扱わない
- **UTF-8 リテラル + SequenceEqual**: Japan-wide PBF で数千万 way を流す前提のホットパス。Phase 1 設計書 §18.4 の 77 MB アロケート反省を踏まえ、フィルタ判定では一切のヒープアロケートを発生させない

**採用しなかった案**：

- **`highway` 値ホワイトリスト方式**: `motorway` / `residential` / ... を列挙して非道路を弾く案。計画書 §5.6-17「広めに採用」と矛盾、また Bicycle / Truck プロファイル追加時にメンテナンスが必要になる
- **`Dictionary<string, string>` でタグ展開**: 可読性は上がるが、Way ごとに 数百バイト以上のアロケーションが発生し PBF 規模で致命的
- **`highway=construction` / `highway=proposed` を専用除外**: 「広めに採用」方針に反する。プロファイル側で `canPass=false` 化すれば bake で自然に弾ける

**検証方法**：

- `tests/OsmDotRoute.Tests/Extractor/WayFilterTests.cs` 19 ケース:
  - `highway` 各種値 6 種で採用 (Theory)
  - `highway` 欠落 / タグ全無し → 不採用
  - `access=no` / `area=yes` で除外（出現順を変えても両方で除外、`highway=service area=yes` も `highway=pedestrian area=no` も検証）
  - `access=yes / permissive / destination / private` は除外しない (Theory 4 ケース)
  - 実世界の典型タグ (`highway=residential, name=本町通り, maxspeed=40, oneway=yes, lit=yes`) で採用
  - 引数 null で `ArgumentNullException`
- 全体テスト: 356/356 pass

**副次的修正**: `OsmDotRoute.Pbf.csproj` の `InternalsVisibleTo` 対象を `OsmDotRoute.Extractor` → `osmdotroute-extractor` に修正（Extractor の `AssemblyName=osmdotroute-extractor` と整合）。

### 5.3 サブステップ 3.3: 頂点正規化（2026-05-25 完了）

**意図**：道路 way のノード参照履歴から、どの OSM Node を「頂点」(`.odrg` Vertex Table の要素) に昇格させるかを決める。エッジ生成 (3.4) はこの決定を使って way を頂点間のエッジに分割する。

**採用設計**：

- [src/OsmDotRoute.Extractor/Pipeline/VertexNormalizer.cs](../src/OsmDotRoute.Extractor/Pipeline/VertexNormalizer.cs) — `internal sealed class`、ステートフル builder
- [src/OsmDotRoute.Extractor/Pipeline/VertexAssignment.cs](../src/OsmDotRoute.Extractor/Pipeline/VertexAssignment.cs) — Build の結果、`OsmId → 0..N-1 連番頂点ID` の写像
- **判定規則**:
  - 道路 way の **始端ノード・終端ノード**は無条件で頂点
  - 2 本以上の道路 way から参照されているノード（**交差点**）は頂点
  - 同じ way 内に 2 回以上現れるノード（**自己交差**、figure-8 等）は頂点
  - それ以外（way の中間に 1 回だけ現れる）はシェイプ点
- **データ構造**: `Dictionary<long, NodeInfo>` で OSM Node ID → `{ Count, IsEndpoint }` を保持
  - `CollectionsMarshal.GetValueRefOrAddDefault` で参照経由更新、`TryGetValue` + `Add` の 2 lookup を回避
  - struct `NodeInfo` で 1 entry = 8 byte (int + bool padding)
- **頂点 ID 採番**: Build 時に「頂点候補のみ抽出 → OSM ID 昇順 sort → 0..N-1 で連番」。Dictionary の列挙順は実装依存のため決定論を担保するため sort
- **退化 way の無視**: `nodeRefs.Length < 2` の way は AddWay で無視（エッジを形成できない）

**判断根拠**：

- **`IsEndpoint` フラグの必要性**: count だけでは、単独 way の端点 (count=1) が頂点になれない。「端点 OR count ≥ 2」の OR 条件で正しく分類
- **`Dictionary<long, NodeInfo>` の選択**: Itinero `NodeIndex` の参考実装でも HashMap ベース。Sorted な配列 + binary search も検討したが、way 走査時のランダム挿入が多いため Dictionary が妥当
- **OSM ID 昇順での採番**: Dictionary の列挙順 (insertion order in .NET Core) に依存すると、PBF ブロック順や `CollectionsMarshal` リハッシュの影響でテストが不安定。N=40k〜100M でも sort コストは無視できる
- **closed loop (ラウンドアバウト) 処理**: A-B-C-D-A の way では A が count=2 (start + end) で頂点、B/C/D は中間 → シェイプ。Itinero の挙動と一致

**採用しなかった案**：

- **`HashSet<long>` 2 つ (seen once / seen twice)**: count = 2 までしか区別できず、`IsEndpoint` を別途持つ必要があり結局複雑化
- **Dictionary 列挙順での頂点 ID 採番**: 実装依存の順序に依存するためテストが不安定。決定論を犠牲にする利得が小さい
- **way 1 本ずつ即座に頂点 ID を採番（オンライン採番）**: 後出しで「count ≥ 2 だった」ノードを撤回 / 昇格できず、複数 way を経て初めて頂点判定できるため不向き

**検証方法**：

- [tests/OsmDotRoute.Tests/Extractor/VertexNormalizerTests.cs](../tests/OsmDotRoute.Tests/Extractor/VertexNormalizerTests.cs) — 14 ケース:
  - 単一 way 2 ノード → 2 頂点
  - 単一 way 3 ノード → 端点 2 頂点 + 中間 1 シェイプ
  - 2 way が中間ノードを共有 → 共有点が交差頂点に昇格
  - T 字路（一方の中間ノードに他方が端点で接続）→ 共有ノードが頂点
  - 閉ループ（A-B-C-D-A）→ A だけ頂点、B/C/D はシェイプ
  - figure-8（自己交差）→ 自己交差ノードが頂点
  - 空入力、退化 way（0 or 1 ノード）の無視
  - 頂点 ID 昇順採番の確認
  - `TryGetVertexId` で未登録 ID → false
  - 3×3 グリッド（6 way）→ 全 9 ノードが頂点
- 全体テスト: 370/370 pass

### 5.4 サブステップ 3.4: エッジ生成（2026-05-25 完了）

**意図**：道路 way を頂点境界で線形分割し、`.odrg` Edge Table の構造的レコード `EdgeRecord` を生成する。後段 (3.5 / 3.6 / 3.8) はこの構造的エッジ列を入力に動作する。

**採用設計**：

- [src/OsmDotRoute.Extractor/Pipeline/EdgeRecord.cs](../src/OsmDotRoute.Extractor/Pipeline/EdgeRecord.cs) — `internal sealed record`
  - `OsmWayId` (トレース用) / `FromVertexId` / `ToVertexId` / `ShapeNodeRefs (long[])` / `TagKeys / TagValues / StringTable`
- [src/OsmDotRoute.Extractor/Pipeline/EdgeGenerator.cs](../src/OsmDotRoute.Extractor/Pipeline/EdgeGenerator.cs) — `internal static class`
  - API: `List<EdgeRecord> SplitWay(OsmWay way, VertexAssignment vertexAssignment, OsmStringTable stringTable)`
- **アルゴリズム**:
  1. way の `NodeRefs` を線形走査し、`VertexAssignment.TryGetVertexId` で頂点境界を判定
  2. 頂点 i → 次の頂点 j を見つけ、その間 (i+1 .. j-1) を `ShapeNodeRefs` に格納
  3. エッジ 1 本生成、i = j で次のエッジ走査を継続
  4. way 末尾までスキャンしたら終了
- **エッジ向き**: way の元の向きをそのまま使う。oneway / oneway=-1 / junction=roundabout の解釈は **3.5 でフラグに bake** し、エッジ自体は 1 本のみ生成（仕様書 §4.5「IsOnewayForward/IsOnewayBackward が両方立たない=双方向」に従う）
- **shape**: 端点を**含まない**中間ノード OSM ID 列のみ（仕様書 §4.3 の `shapeLength` 定義に合わせる）。直線エッジは `Array.Empty<long>()` を返してアロケート節約
- **tags**: 由来 way の `TagKeys`/`TagValues` を**シェア**（コピーしない）。3.5/3.6 は同じ tag 集合を読むため重複保持は無駄
- **座標**: shape は OSM Node ID で保持。座標解決は PBF 3 パス目（仕様書 §5.3.3 パス 3）。3.4 時点では座標を持たない

**判断根拠**：

- **エッジ向きを way 方向で 1 本だけ生成**: 仕様書 §4.5 で oneway は bit フラグで表現と確定。エッジ本数を倍にする (双方向 = 2 エッジ) と Edge Table / R-tree / Profile Table が肥大化、ホットパスのキャッシュ局所性も悪化
- **shape を long[] で保持（座標展開しない）**: 3.4 では座標がまだロードされていない（3 パス PBF 走査の構造）。座標展開を 3.4 で行うと PBF 走査順序が崩れる
- **tag を共有 (Same ref)**: 単一 way が複数エッジに分割されても tag は同一。コピーは tag 数 × エッジ数 のメモリを浪費
- **`TryGetVertexId` ベースの走査**: 仕様上 way の始端・終端は VertexNormalizer で必ず頂点に昇格しているため、防御的に最初の頂点までスキップする処理を入れた（VertexNormalizer に投入されていない way が来た異常系でも crash しない）
- **`StringTable` をエッジに含める**: 同一 PrimitiveBlock 内の way 群しか扱わない場合は同じ StringTable で済むが、複数ブロック跨ぎのエッジ集約を考えるとエッジ毎に保持が必要。参照だけなのでメモリコストは無視できる

**採用しなかった案**：

- **双方向エッジを 2 本生成**: Edge Table が倍化、`bakedProfileIndex == edgeId` 設計と相性悪い、R-tree も倍化。仕様書 §4.5 のフラグ方式で十分
- **shape を `(double, double)[]` で持つ**: 3.4 時点で座標未ロード、PBF 走査順序が壊れる
- **タグを `Dictionary<string, string>` で展開**: 文字列化で大量アロケート、UTF-8 比較の高速化を捨てる。3.5/3.6 は UTF-8 SequenceEqual で読む方が速い
- **`IEnumerable` を yield return で返す**: 呼び出し側がほぼ確実に列挙するため、`List<EdgeRecord>` 確定の方が GC 圧少なく分かりやすい

**検証方法**：

- [tests/OsmDotRoute.Tests/Extractor/EdgeGeneratorTests.cs](../tests/OsmDotRoute.Tests/Extractor/EdgeGeneratorTests.cs) — 9 ケース:
  - 2 ノード単独 way → 1 エッジ shape=0
  - 中間ノードあり way → 1 エッジ shape=2
  - 中間頂点（他 way と交差）→ 2 エッジに分割
  - 閉ループ（ラウンドアバウト）→ 自己ループエッジ 1 本
  - 全ノード頂点 → エッジ shape 全部 0
  - 退化 way（0, 1 ノード）→ 0 エッジ
  - tag シェアの参照同一性確認（`Assert.Same`）
  - 田の字 6 way → 計 12 エッジ
  - null 引数で `ArgumentNullException`
- 全体テスト: 379/379 pass

### 5.5 サブステップ 3.5: エッジ AABB 計算 + エッジフラグ bake（2026-05-25 完了）

**意図**：仕様書 §4.4 Edge AABB Table と §4.5 Edge Flag Table のエントリを bake する。両者は独立しているため 1 サブステップで並行実装。

**採用設計**：

#### (A) エッジフラグ bake

- [src/OsmDotRoute.Extractor/Pipeline/EdgeFlags.cs](../src/OsmDotRoute.Extractor/Pipeline/EdgeFlags.cs) — `[Flags] internal enum EdgeFlags : ushort`、仕様書 §4.5 の 14 bit 割り当てを enum 化
- [src/OsmDotRoute.Extractor/Pipeline/EdgeFlagsBaker.cs](../src/OsmDotRoute.Extractor/Pipeline/EdgeFlagsBaker.cs) — `internal static class`、`Bake(EdgeRecord)` および `Bake(int[], int[], OsmStringTable)` の 2 シグネチャ
- **bake ルール（仕様書 §4.5 ビット表に準拠）**:
  - `IsBridge` (bit 0): `bridge=*`（`bridge=no` 除く）
  - `IsTunnel` (bit 1): `tunnel=*`（`tunnel=no` 除く）
  - `IsElevated` (bit 2): `layer >= 1` または `bridge=viaduct`
  - `IsRoundabout` (bit 3): `junction=roundabout`
  - `IsToll` (bit 4): `toll=yes`
  - `IsPrivateAccess` (bit 5): `access=private`
  - `IsServiceWay` / `IsTrack` / `IsLivingStreet` (bit 6,7,8): `highway` 値判定
  - `IsPedestrianSeparated` (bit 9): `sidewalk=yes` / `sidewalk=both`
  - `IsWinterClosed` (bit 10): `seasonal=winter` または `winter_road=no`
  - `IsSchoolZone` (bit 11): v0.2 では予約・常に 0（仕様書 §4.5 v0.2 注記）
  - `IsOnewayForward` (bit 12) / `IsOnewayBackward` (bit 13): `oneway=yes` / `oneway=-1`
- **junction=roundabout の暗黙 oneway**: 明示的 `oneway` 指定がない場合のみ `IsOnewayForward` を自動追加（OSM 慣習）。`oneway=no` / `oneway=-1` 明示時はそちらを優先
- **ホットパス最適化**: WayFilter と同じ UTF-8 リテラル + `SequenceEqual` パターン、`int.TryParse(ReadOnlySpan<byte>, ...)` を layer 解析に使用

#### (B) エッジ AABB 計算

- [src/OsmDotRoute.Extractor/Pipeline/Aabb.cs](../src/OsmDotRoute.Extractor/Pipeline/Aabb.cs) — `internal readonly record struct Aabb(MinLon, MinLat, MaxLon, MaxLat)`、仕様書 §4.4 のレイアウト準拠（4 double = 32 byte）
- [src/OsmDotRoute.Extractor/Pipeline/EdgeAabbCalculator.cs](../src/OsmDotRoute.Extractor/Pipeline/EdgeAabbCalculator.cs) — `internal static class`、API: `Compute(GeoCoordinate from, GeoCoordinate to, ReadOnlySpan<GeoCoordinate> shape) → Aabb`
- **入力**: 端点 2 つ + 中間シェイプ点列。3 つを統合して min/max を計算
- **ゼロアロケート**: `ReadOnlySpan<GeoCoordinate>` ベース、内部は `ref double` で min/max 更新

**判断根拠**：

- **`EdgeFlags` を独立した internal enum**: 公開すると `OsmDotRoute` ランタイム側で `EdgeFlags` を再現する破壊変更が起きやすい。Phase 3 で `NativeRoadGraph` が読み込む時に同じ enum を共有するか、別 enum で再 bake するかは Phase 3 で判断
- **新しい `Aabb` (4 double flat) の導入**: 既存 `OsmDotRoute.Geometry.Aabb` は `(GeoCoordinate SouthWest, NorthEast)` で名前は同じだが構造が違う。`.odrg` 仕様書 §4.4 の MMF レイアウト (Pack=1, 32 byte) と直接対応させたいため、Extractor 専用の 4 double 平坦型を新設。Extractor が `OsmDotRoute.Geometry.Aabb` を直接使うと layout 変更時に Extractor が壊れるカップリングが生じる
- **`Bake(EdgeRecord)` と `Bake(int[], int[], OsmStringTable)` の 2 シグネチャ**: ホットパスでは `EdgeRecord` のラップオーバーヘッド (record allocation) は無視できるが、テストで tag 列だけ与えて検証したい場合の利便性
- **junction=roundabout の暗黙 oneway 解釈**: OSM Wiki と Itinero 実装でも共通する慣習。明示指定があれば override するのが Itinero の挙動
- **`IsSchoolZone` を bit 確保のみで実装**: 仕様書 §4.5 v0.2 で「予約・抽出ツールは 0 固定出力」が確定済。Phase 3 で bake ルール（半径 N m 内 `amenity=school`）を確定するか剪定するかを判断する余地を残す

**採用しなかった案**：

- **`OsmDotRoute.Geometry.Aabb` 再利用**: 既存型はランタイム制約交差判定用で `Intersects` 等のメソッドが多い。`.odrg` I/O 用に layout 固定が必要なため別型を維持
- **`EdgeFlags` を `OsmDotRoute` 側に置く**: Extractor 専用と決まれば internal で十分。Phase 3 ランタイム側で必要になった時点で共有する/しないを判断
- **`Bake` で `Dictionary<string, string>` を構築**: 文字列化で数十バイト × 数千万 way のアロケーション、UTF-8 SequenceEqual の方が桁違いに速い

**検証方法**：

- [tests/OsmDotRoute.Tests/Extractor/EdgeFlagsBakerTests.cs](../tests/OsmDotRoute.Tests/Extractor/EdgeFlagsBakerTests.cs) — 37 ケース:
  - 各フラグ単独の bake（17 ケース InlineData、internal enum を ushort underlying で受けてキャスト）
  - bridge=viaduct → IsBridge + IsElevated 両立
  - tunnel 3 値（Theory）
  - layer 5 値（Theory、`int.TryParse` の境界）
  - junction=roundabout 単独 → 暗黙 oneway forward
  - junction=roundabout + oneway=no → 暗黙 oneway 抑止
  - junction=roundabout + oneway=-1 → backward 優先
  - 実世界の複合 tag（service + bridge + layer + oneway + name）→ 期待フラグセット
  - `hazard=school_zone` でも `IsSchoolZone` 立たず（v0.2 予約確認）
  - null 引数で `ArgumentNullException`（4 種）
- [tests/OsmDotRoute.Tests/Extractor/EdgeAabbCalculatorTests.cs](../tests/OsmDotRoute.Tests/Extractor/EdgeAabbCalculatorTests.cs) — 6 ケース:
  - 端点のみ（shape 空）→ 端点 bbox
  - 同一点 → 零面積 bbox
  - shape が端点を超えて出っ張る → shape 含む bbox
  - 負座標（南半球・西半球）
  - 端点の順序反転（from > to）→ 正しく min/max
  - 100 点シェイプ → true min/max
- 全体テスト: 422/422 pass

### 5.6 サブステップ 3.6 以降の予定

3.6 では Phase 1 の `ProfileEvaluator` を流用し、各エッジに対し `(canPass, speedKmh, oneway)` を 2 プロファイル (car / pedestrian) 分 bake する。仕様書 §4.7 Baked Profile Table のレイアウト（プロファイル × エッジ、`bakedProfileIndex == edgeId`）に従う。

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
| 0.3 (draft) | 2026-05-21 | **ステップ 2 全完了反映**。§4.9 に「高レベル PbfReader API + 津島市 PBF 統合テスト」（`PbfReader.Read(stream, onNode?, onWay?, onRelation?, leaveOpen?)`、2 パススキャン PrimitiveBlock 設計、null コールバックでセクションスキップ、津島市 PBF 1,646,875 ノードを OsmSharp と完全一致確認）の意図・採用設計・判断根拠・トレードオフ・検証方法を記述。§4.10「ステップ 2 完了状況サマリ」を新設（10 サブステップ + 169 テスト集計）。§4 ステータスを「2 全完了」に更新。採用しなかった案（IEnumerable / IPbfVisitor / インスタンス class / 単一パススキャン）の根拠も明記 | Claude (Opus 4.7) |
