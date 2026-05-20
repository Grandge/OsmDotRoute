# OsmDotRoute Phase 2 実装計画書

**バージョン**: 0.2（ドラフト・骨子）
**作成日**: 2026-05-18（v0.1 として）
**最終更新**: 2026-05-20（v0.2、スコープ再編）
**ステータス**: ドラフト v0.2（骨子のみ・ユーザーレビュー前。Phase 2 スコープを「データ供給側」に絞り、Phase 3 を「データ利用側」に再編した方針確定後の版）
**対象フェーズ**: Phase 2（**データ供給側**：独自バイナリグラフ形式 `.odrg` 策定 + 独自 OSM PBF パーサー + PBF→`.odrg` 抽出ツール）
**関連ドキュメント**:

- [要件定義書](requirement_definition.md)（v2.3、Phase 2/3 スコープ再編後）
- [Phase 2 設計書](phase2_design.md)（v0.2、本計画書と並行作成・各章は対応ステップ完了時に肉付け）
- [Phase 1 実装計画書](phase1_implementation_plan.md)（v1.2 確定、§13「Phase 2 への引き継ぎ事項」参照）
- [Phase 1 設計書](phase1_design.md)（v0.21、§18「制約事項と既知の課題」参照）
- [Phase 1 ベンチマーク結果](phase1_benchmark_results.md)（Phase 3 で性能維持確認時の基準値）
- 別文書：`phase2_graph_format_spec.md`（独自バイナリグラフ形式 `.odrg` 仕様、ステップ 1 で起こす）

---

## 1. 概要

本書は OsmDotRoute Phase 2（**データ供給側**：独自バイナリグラフ形式 `.odrg` 策定と独自 OSM PBF パーサー）の実装計画を定める。

**Phase 2 のゴール**（v0.2 でスコープ再編）：

1. 独自バイナリグラフ形式 `.odrg` を策定する（REQ-MAP-003、別文書 `phase2_graph_format_spec.md`）
2. **OSM PBF を直接読み込む独自 protobuf パーサーを実装する**（REQ-MAP-007、System.\* のみで実装）
3. **OSM PBF → `.odrg` 直接抽出ツール `OsmDotRoute.Extractor` を提供する**（REQ-MAP-008）
4. **OsmDotRoute 最大の差別化要素である動的制約のホットパスをデータ形式自体が直接支援する構造とする**（後述 §3.6）
5. （末尾オプション）Itinero RouterDb → `.odrg` 変換ツール（REQ-MAP-004）：`.odrg` 設計完了後に技術的負担が軽ければ作る、重ければ Phase 3 以降に延期

**Phase 2 の方針**：

- **Itinero RouterDb の構造に引きずられない `.odrg` 設計**（v0.2 で最も重要な方針転換）。Phase 1 で内製した経路探索エンジンが要求するエッジモデルから直接 `.odrg` を設計し、Itinero RouterDb の `edge_profile` 概念にとらわれない
- ランタイム経路計算 API は Phase 1 のまま据え置く（`.odrg` を実際に読み込むのは Phase 3）。Phase 2 完了時点では `OsmDotRoute.Extractor` の出力 `.odrg` を MapVerifier 等で直接検査することで形式の正しさを担保
- Phase 2 は **System.\* のみ**で完結する（REQ-DEP-002、protobuf-net 等の外部依存を使わず独自 protobuf パーサーを実装）
- 非公開リポジトリ運用を維持する（REQ-PKG-002、Phase 3 完了まで非公開に変更済）
- ランタイム Itinero 依存削除・Bicycle/Truck プロファイル・ベンチマーク・親プロジェクト統合は Phase 3 へ移動（要件定義書 v2.3）

**Phase 2/3 のスコープ分担**（v0.2 で確定）：

| 区分 | Phase 2 = データ供給側 | Phase 3 = データ利用側 |
|---|---|---|
| `.odrg` 形式 | 策定（REQ-MAP-003） | 読込（REQ-MAP-005） |
| OSM PBF | 独自パーサー実装（REQ-MAP-007） | （成果物利用） |
| PBF→`.odrg` 抽出 | CLI ツール提供（REQ-MAP-008） | （成果物利用） |
| RouterDb→`.odrg` 変換 | 末尾オプション（REQ-MAP-004） | （Phase 3 へ延期可） |
| ランタイム実装 | 変更なし（Phase 1 のまま） | `NativeRoadGraph` / `NativeRoadSnapper`（REQ-MAP-005） |
| Itinero 依存削除 | （対象外） | ランタイムから排除（REQ-MAP-006） |
| Bicycle/Truck Profile | （対象外） | 独自設計・Truck=10t（REQ-PRF-003〜004） |
| 性能ベンチ | （対象外、Phase 1 基準は Phase 3 で再測定） | 実施（REQ-NFR-001〜003 維持） |
| 親プロジェクト統合 | （対象外） | 実施（旧 Phase 1 ステップ 16） |

---

## 2. 前提条件

- [x] Phase 1 完了（v0.1.0 タグ付与済、commit `e5d90f2`、2026-05-20）
- [x] Phase 1 設計書 §18「制約事項と既知の課題」が確定
- [x] 要件定義書 v2.3 で Phase 2/3 スコープ再編済（2026-05-20）
- [x] §5.4 ユーザー判断事項 10 件 + Phase 2/3 スコープ再編 4 件確定済
- [ ] 本実装計画書 v0.2 のユーザーレビュー
- [ ] `phase2_graph_format_spec.md` 起こし判断（ステップ 1 着手時）

---

## 2.5 設計書の同時更新ルール（Phase 1 と同方針）

**Phase 2 実装中も、各ステップ完了時に設計書 [`phase2_design.md`](phase2_design.md) の対応章を必ず更新する**。Phase 1 のルール（[`phase1_implementation_plan.md`](phase1_implementation_plan.md) §2.5、メモリ [[feedback_design_doc_per_step]]）を踏襲する。

---

## 3. 採用アプローチ

### 3.1 独自バイナリグラフ形式 `.odrg`

- ファイル拡張子は **`.odrg`** で確定（§5.4-1）。マジックナンバー・エンディアン・バージョニング規約はステップ 1 で確定
- アクセス方式は **`MemoryMappedFile`** で確定（§5.4-6）。ランタイムはセクション先頭オフセットから `ReadOnlySpan<T>` / `ReadOnlyMemory<T>` ビューを切り出す
- **Itinero RouterDb 構造に引きずられない自由設計**を v0.2 で確定（最重要方針）。OsmDotRoute コアの `IRoadGraph` 抽象が要求するデータのみから直接設計
- 構成セクション（暫定）：
  - **ヘッダー**（マジック / バージョン / 全体バウンディングボックス / 頂点数 / エッジ数 / 各セクションオフセット）
  - **頂点表**（緯度経度の配列、`ReadOnlySpan<Vertex>` で公開）
  - **エッジ表**（fromVertexId / toVertexId / shapeOffset / shapeLength / **edgeAabbIndex** / bakedProfileIndex / **edgeFlags**）
  - **エッジシェイプ連続バッファ**（全エッジのシェイプ点列を 1 本の大きなバッファに連続配置、各エッジへ `ReadOnlySpan<GeoCoordinate>` ビューを公開してゼロアロケーション化、§3.6）
  - **エッジ AABB 表**（エッジ毎の minLon/minLat/maxLon/maxLat を **double×4** で bake（§5.4-3）、制約 AABB プリフィルタを O(1) 配列読み出し化、§3.6）
  - **エッジ空間インデックス**（**R-tree（STR パック静的版）**で確定（§5.4-2）。制約追加時に影響エッジ集合を O(log N) で取得、§3.6）
  - **エッジフラグ**（**1〜2 バイトの bitflag**（§5.4-4、できるだけ多く採用方針）。橋 / トンネル / 高架 / 一方通行向き / 歩道分離 / 有料道路 / ラウンドアバウト / 閉鎖区間 / 冬季閉鎖 / ダート路面 / 私道 / 通学路 等を候補とし、ステップ 1 で配置確定。難所評価・プロファイル意味判定で利用、§3.6）
  - **bake 済みプロファイル表**（プロファイル × highway × OSM タグ集約 → `(canPass, speedKmh, oneway)`、ランタイム O(1) ルックアップ）
  - **メタデータ**（生成日時 / 元 PBF ハッシュ / 使用プロファイル一覧 / Phase 1 RouterDb 比のサイズ実測値）

### 3.2 Span/Memory ベース API への刷新（Phase 3 で実利用）

Phase 1 設計書 §18.4（経路 1 本あたり 77 MB アロケート、Itinero の 2.4 倍）の根治を見据え、`.odrg` 形式は Span/Memory ベース API で読めるよう設計する。**主要動機は動的制約のホットパスにおけるゼロアロケーション化**（§3.6）。

ただし API の実適用は Phase 3 で `NativeRoadGraph` を実装する時。Phase 2 では「形式設計が API を縛らない」ことを確認するに留める。

- エッジシェイプ連続バッファ + `ReadOnlySpan<GeoCoordinate>` ビュー前提のレイアウト
- エッジ AABB は配列インデックス参照で `ReadOnlySpan<Aabb>` 化可能なレイアウト
- R-tree ノードも配列化、`ReadOnlySpan<RTreeNode>` で参照可能

### 3.3 独自 OSM PBF パーサー（REQ-MAP-007）

- 新規プロジェクト：`src/OsmDotRoute.Pbf`（net9.0、internal もしくは public 要検討）
- **System.\* のみで実装**（REQ-DEP-002、protobuf-net 等の外部依存禁止）
- 対応する PBF 要素を OsmDotRoute 必要分に限定：
  - HeaderBlock（バウンディングボックス・required_features 等）
  - PrimitiveBlock / PrimitiveGroup
  - Node（座標）
  - DenseNodes（座標差分圧縮）
  - Way（ノード参照・タグ）
  - Relation（type=restriction の限定対応：通行禁止規則）
  - 文字列テーブル（stringtable）
- zlib 解凍は `System.IO.Compression.ZLibStream`（.NET 6+）を使用
- protobuf ワイヤ形式の独自実装（varint / zigzag / sint64 等）
- 動作確認は親プロジェクトの愛知県津島市 OSM PBF サンプルから

### 3.4 PBF → `.odrg` 抽出ツール `OsmDotRoute.Extractor`（REQ-MAP-008）

- 新規プロジェクト：`src/OsmDotRoute.Extractor`（net9.0、`OutputType=Exe`）
- 依存：`OsmDotRoute`（コア）、`OsmDotRoute.Pbf`（独自 PBF パーサー）、**`System.CommandLine`**（§5.4-7）
- CLI ユースケース：

  ```text
  osmdotroute-extractor extract \
    --input tsushima.osm.pbf \
    --profiles car,pedestrian \
    --output tsushima.odrg
  ```

- 内部処理パイプライン：
  1. PBF 読込（`OsmDotRoute.Pbf`）→ Node / Way / Relation 列挙
  2. 道路 way フィルタ（`highway=*` 等）
  3. 頂点正規化（交差点・端点を頂点に、中間ノードはシェイプに）
  4. エッジ生成（fromVertexId / toVertexId / shape / tags）
  5. エッジ AABB 計算
  6. エッジフラグ抽出（`bridge=yes` → 橋ビット 等）
  7. プロファイル bake（プロファイル × エッジ → `(canPass, speedKmh, oneway)`）
  8. STR R-tree 構築
  9. `.odrg` 書出

### 3.5 Phase 3 への申し送り設計

Phase 2 完了時点ではランタイムは引き続き Phase 1 の Itinero RouterDb 経路で動く。`.odrg` 形式の実利用は Phase 3 で行う：

- `NativeRoadGraph` / `NativeRoadSnapper` 実装（Phase 3 ステップ）
- ランタイム Itinero 依存削除（`OsmDotRoute.Itinero` プロジェクト撤去、Phase 3 ステップ）
- 「制約 ID → 交差エッジ ID 集合」キャッシュ機構（Phase 3 ステップ）
- Bicycle / Truck プロファイル独自設計（Phase 3 ステップ）
- ベンチマーク・性能維持確認（Phase 3 ステップ）
- 親プロジェクト統合・パリティ検証（Phase 3 ステップ）

これらは Phase 2 で**設計の余地を残す**（API・データレイアウトが Phase 3 実装を妨げないことを確認）に留める。

### 3.6 動的制約を最大限に活かすデータ設計

OsmDotRoute が Itinero に対して持つ唯一の差別化要素は、**シミュレーション実行中に進入不可エリア・難所エリアを追加／削除でき、次回経路計算から即時反映できる**こと（REQ-RST-001〜015, REQ-RST-030〜032, REQ-RST-012）。Phase 2 のバイナリ形式はこの強みを最大化することを最優先設計目標とする（メモリ [[project-phase2-dynamic-restriction-design]]）。

**Phase 1 のホットパスと課題**：

経路探索中、Dijkstra が辺を展開するたびに `EdgeWeightCalculator` が以下を実行：

1. エッジシェイプを取得（Phase 1 では `IReadOnlyList<GeoCoordinate>` で **コピー発生** → 77 MB/route の主因、設計書 §18.4）
2. 全制約に対し AABB プリフィルタ → 多角形交差判定（Phase 1 では制約側 `SpatialIndex` 線形走査、設計書 §16.3）
3. 通行可否・速度係数を確定して重みを返す

制約数 N、エッジ数 E、シェイプ点数 S のとき走査コスト O(N × E × S)。Phase 1 は制約数 ≦ 100 を前提に許容したが、本来エッジ側に空間インデックスがあれば「制約毎に交差候補エッジを O(log E) で絞る」逆方向アプローチが取れる。

**Phase 2 の設計指針**：

| 課題 | データ形式での対応 | 期待効果（Phase 3 で実測） |
|---|---|---|
| シェイプコピーで 77 MB/route アロケート | エッジシェイプを連続バッファに配置、`ReadOnlySpan<GeoCoordinate>` ビューで公開（§3.1, §3.2） | ホットパスのアロケートをゼロに、GC 圧削減 |
| エッジ AABB を毎回シェイプから再計算 | エッジ AABB を変換時に bake（double×4、§5.4-3）、配列インデックスで即取得（§3.1） | プリフィルタが O(1) 配列読み出し、ブランチも単純 |
| 制約追加時に全エッジを再評価 | **R-tree（STR パック静的版）**でエッジ空間インデックスを bake（§5.4-2、§3.1）、制約追加時に交差候補エッジ集合を O(log E) で取得 | 制約 add/remove 時のコスト O(交差候補) で完結、`RestrictedAreaService` 側に「制約 ID → 影響エッジ ID 集合」キャッシュを構築可能 |
| 「橋は冠水しない」等の意味判定 | 橋 / トンネル / 高架 / 一方通行向き / 歩道分離 / 有料道路 / ラウンドアバウト / 冬季閉鎖 / ダート路面 等を **1〜2 バイト bitflag** で広めに bake（§5.4-4、§3.1） | 難所プロファイル評価で 1 分岐で意味判定、プロファイル側で「冠水フラグ × 橋フラグ → canPass=true」のような表現を可能に |
| 動的制約 100 件投入時の劣化（REQ-NFR-002） | 上記をすべて組み合わせ、ホットパスのコピー・アロケート・線形走査を撲滅 | Phase 1 NFR-002（制約 100 件で性能維持）の達成余裕を増やし、Phase 3 都道府県単位ベンチに耐える土台を作る |

**ランタイム側に持たせる仕組み**（Phase 3 で実装、ファイルにはレイアウト想定のみ）：

- `RestrictedAreaService.AddBlockArea(polygon)` 時、エッジ空間インデックスで polygon と交差するエッジ ID 集合を 1 回だけ計算しキャッシュ
- Dijkstra の辺展開時はキャッシュを参照するのみ（HashSet 一発でルックアップ）
- 制約削除時はキャッシュから当該エントリを drop
- Phase 1 の `RestrictedAreaService` 公開 API は変更しない（内部実装の高速化に留める）

**設計上の歯止め**：

- 上記はあくまで「形式が支援する」レベル。**プロファイル拡張点（例：橋への動的属性付与）を勝手に増やさない**（YAGNI）
- エッジフラグは **OSM タグから機械的に bake できる属性のみ**採用（§5.4-4「できるだけ多く採用、運用上不要と判断した時点で削る」方針）。航空写真・標高データ・動的更新が必要な属性は Phase 2 でも導入しない
- 「制約 ID → エッジ ID 集合」キャッシュの API は internal にとどめ、`RestrictedAreaService` 公開シグネチャは Phase 1 のままを死守する

### 3.7 Itinero RouterDb → `.odrg` 変換ツール（末尾オプション、REQ-MAP-004）

- **Phase 2 末尾の追加オプションタスク**（§5.4 Phase 再編 Q4）
- `.odrg` 設計確定後（ステップ 1 完了時点）に、Itinero `RouterDb.Network` から `.odrg` を生成する変換ロジックが技術的に軽いかを評価
- 軽ければ作る：`src/OsmDotRoute.Converter`（Itinero 1.5.1 参照、`OsmDotRoute.Pbf` 不要）として実装
- 重ければ作らない：Phase 3 以降の保留タスクに移管。Phase 1 の MapService.cs ベース成果物との互換確認は Phase 3 で `OsmDotRoute.Extractor` 出力との突合で代替
- **設計上の影響**：このツールが「ない可能性」を前提に、`.odrg` 形式は RouterDb 構造に引きずられない設計とする

---

## 4. Phase 2 スコープ確認（要件対応表）

| ID | 概要 | 優先度 | 関連ステップ |
|---|---|---|---|
| REQ-MAP-003 | `.odrg` 形式策定（別文書 `phase2_graph_format_spec.md`） | P1 | 1 |
| REQ-MAP-007 | 独自 OSM PBF パーサー（System.\* 完結） | P1 | 2 |
| REQ-MAP-008 | PBF → `.odrg` 抽出ツール `OsmDotRoute.Extractor` | P1 | 3 |
| REQ-MAP-004 | RouterDb → `.odrg` 変換ツール（末尾オプション） | P3 | 4-opt |
| REQ-PKG-002 | Phase 3 完了まで非公開リポジトリ維持 | P2 | 全体（運用） |
| REQ-DEP-002 | Phase 2 すべて System.\* のみで完結（protobuf-net 等の外部依存禁止） | P1 | 2-3 |

**スコープ外（Phase 3 へ移動）**：

- REQ-MAP-005 / REQ-MAP-006: `.odrg` ランタイム読込、ランタイム Itinero 依存削除
- REQ-PRF-003 / REQ-PRF-004: Bicycle / Truck プロファイル（Truck=10t 独自設計）
- REQ-NFR-001〜003 性能維持確認
- 親プロジェクト統合・パリティ検証（旧 Phase 1 ステップ 16）
- 都道府県単位ベンチ完全実施
- メッシュ 100 m 階層対応（要件定義書 v1.4、Phase 2 以降に延期済み）

---

## 5. プロジェクト構成変更

### 5.1 新規プロジェクト

| プロジェクト | 配置 | 役割 | 依存 |
|---|---|---|---|
| `OsmDotRoute.Pbf` | `src/OsmDotRoute.Pbf/` | 独自 OSM PBF パーサー（System.\* 完結） | `OsmDotRoute`（共有型のみ）、System.\* |
| `OsmDotRoute.Extractor` | `src/OsmDotRoute.Extractor/` | PBF → `.odrg` 抽出 CLI | `OsmDotRoute`, `OsmDotRoute.Pbf`, `System.CommandLine` |
| `OsmDotRoute.Converter`（末尾オプション） | `src/OsmDotRoute.Converter/` | RouterDb → `.odrg` 変換 CLI | `OsmDotRoute`, `OsmDotRoute.Itinero`, Itinero 1.5.1, `System.CommandLine` |

### 5.2 Phase 2 完了時のアセンブリ参照グラフ

```text
OsmDotRoute                      (ランタイム、Itinero 依存維持 Phase 3 で除去)
  ↑
OsmDotRoute.Itinero              (Phase 1 のまま、Phase 3 で撤去予定)
  ↑
OsmDotRoute.Pbf                  (新規、System.* 完結)
  ↑
OsmDotRoute.Extractor            (新規、CLI ツール)

[末尾オプションの場合のみ]
OsmDotRoute.Converter            (RouterDb→.odrg、Itinero 1.5.1 参照)
```

### 5.3 Phase 1 既存プロジェクトへの影響

- `OsmDotRoute`（コア）：ランタイム実装は Phase 2 では変更なし。`.odrg` 読込型の追加は Phase 3
- `OsmDotRoute.Itinero`：Phase 1 のまま据え置き（Phase 3 で撤去）
- `OsmDotRoute.Extensions.DependencyInjection`：変更なし
- `MapVerifier`：`.odrg` ファイルを直接検査する機能を追加検討（Phase 2 完了判定で形式の正しさを確認するため、ステップ 5 で要件化判断）

### 5.4 Phase 2/3 スコープ再編 確定事項（2026-05-20 ユーザー判断）

| # | 項目 | 決定 |
|---|---|---|
| 11 | Phase 2/3 スコープ再編 | **Phase 2 = `.odrg` 形式策定 + PBF パーサー + PBF→`.odrg` 抽出に絞る**。ランタイム読込・Itinero 依存削除・Bicycle/Truck・ベンチ・親プロ統合は Phase 3 へ |
| 12 | OSM PBF パーサー実装 | **独自 protobuf 実装、System.\* 完結**（REQ-DEP-002） |
| 13 | 要件定義書改訂タイミング | **v0.2 計画書と同時に v2.3 へ改訂**（実施済み 2026-05-20） |
| 14 | RouterDb 変換ツール | **Phase 2 末尾のオプションタスク**（REQ-MAP-004 を P3[Phase2-opt] へ降格） |

### 5.5 確定済みユーザー判断事項（v0.1.2 で確定済み・継承）

| # | 項目 | 決定 |
|---|---|---|
| 1 | ファイル拡張子 | `.odrg` |
| 2 | エッジ空間インデックス方式 | R-tree（STR パック静的版） |
| 3 | エッジ AABB の精度 | double |
| 4 | エッジフラグの組込み範囲 | できるだけ多く採用、運用上不要と判断した時点で削る |
| 5 | 「制約 ID → 交差エッジ ID 集合」キャッシュの API 位置付け | Phase 3 のランタイム読込実装時に確定（v0.2 で Phase 3 へ移動） |
| 6 | ファイルアクセス方式 | `MemoryMappedFile` |
| 7 | 変換ツール CLI パーサ | `System.CommandLine` |
| 8 | `Route.Shape` API 移行 | `IReadOnlyList` → `ReadOnlyMemory<GeoCoordinate>` 破壊変更可（Phase 3 で実施） |
| 9 | Phase 3 ベンチ対象都市 | 愛知県津島市（Phase 1 と同一） |
| 10 | Bicycle / Truck プロファイル | 独自設計、Truck=10t トラック（Phase 3 で実装） |

### 5.6 v0.2 で確定したユーザー判断事項（2026-05-20 確定）

| # | 項目 | 決定 | 補足 |
|---|---|---|---|
| 15 | `OsmDotRoute.Pbf` の公開度 | **internal**（`OsmDotRoute.Extractor` 専用、`InternalsVisibleTo` 利用） | 外部 API 不変約束の責任を負わず、Phase 3 以降のリファクタリングに自由度。外部公開要望が出たら public 化は後で可能（YAGNI） |
| 16 | PBF パーサーの ZLib 解凍実装 | **ステップ 2 で実測比較**（`System.IO.Compression.ZLibStream` を第一候補、性能未達なら独自実装を検討） | `ZLibStream` は .NET 6+ で標準提供、PBF 仕様の zlib ブロックに使える見込み |
| 17 | 道路 way フィルタの基準 | **`highway=*` 全部を取り込み、`access=no` / `area=yes` 等の access タグで除外**。プロファイル判定は bake 時に実施 | フィルタは広めに取り、プロファイル側で絞る設計。Phase 3 で Bicycle/Truck を bake 対象に追加する際に元の way 集合が不足しないようにする |
| 18 | PBF Relation `type=restriction`（ターン制限・通行禁止）の対応 | **Phase 4+ に延期** | Phase 1 も未対応（Itinero 設定依存、親プロジェクトでは未使用）。Phase 2 では Phase 1 と同じ結果を出すことを優先。`.odrg` 形式に「ターン制限テーブル」セクションのオフセットだけ予約し、将来拡張可能とする |
| 19 | `.odrg` フォーマット検証手段 | **MapVerifier 拡張** | Phase 1 ステップ 17 の 32/32 検証体制を踏襲。`.odrg` を読んで頂点・エッジ・シェイプを地図上にオーバーレイ、Phase 1 RouterDb と重ね表示してズレを目視確認 |

---

## 6. 実装ステップ一覧

| # | ステップ | 主要要件 | 状態 |
|---|---|---|---|
| 1 | 独自バイナリグラフ形式 `.odrg` 仕様策定（`phase2_graph_format_spec.md` 起こし、Span/Memory ベース API 設計、**STR パック静的 R-tree のビルド/シリアライズアルゴリズム設計**、エッジ AABB（double×4）/ シェイプ連続バッファ / エッジフラグ（1〜2 バイト bitflag、§3.6 候補一覧）の配置確定、MMF レイアウト確定） | REQ-MAP-003 | **完了**（2026-05-21、仕様書 v0.2 確定、設計書 §3 v0.2.1 反映、commit 099969a で骨子・本セッションで残オープン課題 4 件 確定） |
| 2 | 独自 OSM PBF パーサー `OsmDotRoute.Pbf` 実装（protobuf ワイヤ形式 / varint / DenseNodes / Way / Relation / stringtable / ZLib 解凍、System.\* 完結） | REQ-MAP-007, REQ-DEP-002 | **着手中** — 2.1 プロジェクト骨格 ✅ / 2.2 protobuf ワイヤ形式 ProtoReader ✅ (27/27 テスト) / 2.3 Blob 構造 + ZLib 解凍 ✅ (25/25 テスト) / 2.4 HeaderBlock 解析 ✅ (18/18 テスト) / 2.5 PrimitiveBlock + stringtable ✅ (19/19 テスト) / 2.6 単体 Node 解析 ✅ (15/15 テスト、`OsmNode` + `OsmNodeParser`、packed uint32 + Info スキップ) / 2.7〜2.10 未着手 |
| 3 | PBF → `.odrg` 抽出ツール `OsmDotRoute.Extractor` CLI 実装（`System.CommandLine` ベース、PBF 読込 → 道路 way フィルタ → 頂点正規化 → エッジ生成 → AABB 計算 → STR R-tree 構築 → エッジフラグ bake → bake プロファイル → `.odrg` 書出） | REQ-MAP-008 | 未着手 |
| 4-opt | （末尾オプション）Itinero RouterDb → `.odrg` 変換ツール `OsmDotRoute.Converter`。ステップ 1 完了時点で技術的負担を評価し、軽ければ実装、重ければ Phase 3 以降に延期 | REQ-MAP-004 | 未着手・実施判断保留 |
| 5 | Phase 2 検証・確定（`OsmDotRoute.Extractor` 出力 `.odrg` の形式正当性検査、Phase 1 RouterDb と同等の頂点数・辺数・経緯度範囲が出ることを確認、設計書 §10 で Phase 3 申し送り整理、v0.2.0 タグ判断） | — | 未着手 |

**Phase 3 で実施するステップ**（参考、別途 Phase 3 計画書を起こす）：

- 3A. ランタイム `.odrg` 読込実装（`NativeRoadGraph` / `NativeRoadSnapper` / MMF ビュー / R-tree クエリ）
- 3B. 動的制約ホットパス高速化（「制約 ID → 交差エッジ ID 集合」キャッシュ機構）
- 3C. ランタイム Itinero 依存削除（`OsmDotRoute.Itinero` 撤去、`Route.Shape` 破壊変更）
- 3D. Bicycle / Truck プロファイル独自設計（Truck=10t、日本道路法ベース）
- 3E. ベンチマーク再実施（津島市、Phase 1 基準値との比較、制約 100 件時劣化率改善確認）
- 3F. 親プロジェクト統合・パリティ検証（旧 Phase 1 ステップ 16）
- 3G. 都道府県単位ベンチ（Phase 1 §18.2 リベンジ）
- 3H. ユーザー検証・Phase 3 確定（OSS 公開準備、運用上不要なエッジフラグ剪定判断）

各ステップ完了時に **ユーザー報告 → 承認 → 次ステップ着手** のサイクルを厳守（CLAUDE.md ルール、Phase 1 と同様）。

---

## 7. 想定工数感（粗見積もり）

| ステップ | 想定工数 | 主リスク |
|---|---|---|
| 1. `.odrg` 仕様策定 | 3〜5 日 | フォーマット設計の試行錯誤、STR R-tree シリアライズ設計、エッジフラグ配置確定 |
| 2. 独自 PBF パーサー | 4〜6 日 | protobuf ワイヤ形式の独自実装、DenseNodes 差分圧縮、テストデータ準備 |
| 3. `OsmDotRoute.Extractor` | 3〜4 日 | 道路 way フィルタ精度、頂点正規化の Phase 1 等価性確認、bake 漏れ |
| 4-opt. RouterDb 変換ツール | 0〜3 日 | （実施判断次第。実施しなければ 0 日） |
| 5. Phase 2 検証・確定 | 1〜2 日 | `.odrg` 出力検査手段の確立 |
| **合計** | **11〜20 日** | （末尾オプションを含む） |

Phase 1（20〜23 日想定）の半分強。Phase 3 がより大きくなる見込み（旧 Phase 2 のランタイム実装 + Bicycle/Truck + ベンチ + 親プロ統合）。

---

## 8. リスクと対処方針

| # | リスク | 影響 | 対処方針 |
|---|---|---|---|
| R1 | `.odrg` 設計の試行錯誤で仕様が長期化 | 全ステップ遅延 | ステップ 1 を v0.1（最小仕様）と v0.2（最適化）の 2 段階で確定。最小仕様で全ステップを通し、最適化は別ラウンドで |
| R2 | 独自 PBF パーサー実装の難易度予測ミス | ステップ 2 遅延 | OSM Wiki の PBF Format 仕様を熟読、参考実装（osmium-tool / OsmSharp）を**コピーせず**読み解く。limit を「OsmDotRoute 必要分のみ」に絞る（HeaderBlock / PrimitiveGroup の限定要素） |
| R3 | `.odrg` の道路ネットワークが Phase 1 RouterDb と等価でない（経路差異） | Phase 3 で「Phase 1 と同じ経路が出ない」状態となる | ステップ 3 完了時に、Phase 1 RouterDb (`tsushima.routerdb`) と Phase 2 `.odrg` (`tsushima.odrg`) で頂点数・辺数・経緯度範囲・代表的な経路結果が一致することを単体テスト化。ズレた場合は道路 way フィルタ・頂点正規化を調整 |
| R4 | 動的制約 §3.6 設計を盛り込みすぎて仕様肥大化・ステップ 1 が長期化 | Phase 2 全体遅延、YAGNI 違反 | §3.6 末尾「設計上の歯止め」を尊重。エッジフラグは「できるだけ多く採用、運用上不要と判断した時点で削る」方針、キャッシュ機構は internal 限定、公開 API は Phase 1 のまま死守。ステップ 1 で「v0.1 最小仕様」と「v0.2 最適化」を分け、v0.1 で §3.6 全項目を載せたうえで全ステップを通す（R1 の対処と連動） |
| R5 | エッジ空間インデックスを bake した結果、ファイルサイズが Phase 1 RouterDb を大幅超過 | 配布・読込時間 | ステップ 1 で R-tree のサイズ概算をシミュレートし、Phase 1 RouterDb の 1.5 倍以内を目標。超過時はインデックス粒度を粗くする選択肢を仕様策定時に確保 |
| R6 | `OsmDotRoute.Extractor` の Phase 1 ベンチ津島市 PBF からの抽出時間が長すぎる | 利用者の手間 | ステップ 3 で抽出時間を実測。許容上限は「Itinero の `Itinero.IO.Osm.LoadOsmDataAsync()` と同等以内」（親プロジェクトでの Phase 1 RouterDb 生成時間）とする |
| R7 | RouterDb 変換ツール（オプション）の実装判断ミスで Phase 2 完了が遅延 | 全体遅延 | ステップ 1 完了時点で「軽い／重い」を判定し、迷ったら作らない方針（YAGNI、Phase 3 以降に延期）。実施判断の根拠を設計書 §4 に明記 |
| R8 | Phase 2 完了時点では実際にランタイムが動かないため、`.odrg` の正しさ検証が難しい | Phase 2 完了判定の根拠不足 | ステップ 5 で `.odrg` 検査機能（MapVerifier 拡張 / 専用 CLI / 単体テスト）のいずれかを実装し、Phase 1 RouterDb との等価性を担保。詳細は §5.6 で確定判断 |

---

## 9. 性能基準値（Phase 3 で検証）

Phase 2 完了時点ではランタイムが `.odrg` を使わないため、性能ベンチは Phase 3 で実施。Phase 1 基準値（[`phase1_benchmark_results.md`](phase1_benchmark_results.md)、津島市）を Phase 3 で同等以上維持することが目標。

| 指標 | Phase 1 値（津島市） | Phase 3 目標 |
|---|---|---|
| Mean（制約 0 件） | 33 ms | ≦ 33 ms（同等以上） |
| OsmDotRoute / Itinero 比 | 0.48x | ≦ 0.50x 維持 |
| StdDev | Itinero の 1/7 | 同等以上の安定性 |
| WorkingSet | 54 MB | ≦ 54 MB |
| **経路 1 本あたりアロケート** | **77 MB**（設計書 §18.4） | **大幅削減**（Span/Memory 化目標） |
| **制約 100 件投入時の劣化率（REQ-NFR-002）** | C4 ベンチで Block 100 件短絡効果が単独計測できず（設計書 §18.3） | §3.6 のエッジ空間インデックス + 交差エッジキャッシュにより、**制約 0 件時との Mean 比を Phase 1 より明確に小さく**できることを実測 |
| **`AddBlockArea` / `AddDifficultyArea` 単発コスト** | 計測対象外 | エッジ空間インデックスで O(交差候補) に抑え、100 件追加合計で実用時間（暫定 < 100 ms / 市単位）以内 |

都道府県単位ベンチは Phase 3 ステップ 3G で実施（Phase 1 設計書 §18.2 リベンジ）。

---

## 10. 次のアクション

- [x] 本計画書 v0.2 のユーザーレビュー（2026-05-20、commit 099969a）
- [x] §5.6「v0.2 で新たに確定すべきユーザー判断事項」5 件の暫定回答収集（2026-05-20 確定）
- [x] ユーザー合意後、設計書 [`phase2_design.md`](phase2_design.md) v0.2 起こしに反映（2026-05-20、commit 099969a）
- [x] ステップ 1 着手 → `phase2_graph_format_spec.md` v0.2 確定（2026-05-21、本セッションで残オープン課題 4 件 確定）
- [x] ステップ 2.3 完了（2026-05-21、`PbfBlobReader` + 25 テスト、`PbfBlobType` enum、`ZLibStream` 解凍、ArrayPool バッファリング）
- [x] ステップ 2.4 完了（2026-05-21、`OsmHeader` + `OsmBoundingBox` + `OsmHeaderParser` + 18 テスト、`SupportedRequiredFeatures = {OsmSchema-V0.6, DenseNodes}`、HeaderBBox ナノ度→度変換）
- [x] ステップ 2.5 完了（2026-05-21、`PrimitiveBlock` + `OsmStringTable` + `PrimitiveBlockParser` + 19 テスト、座標変換 `lon = 1e-9 × (LonOffset + Granularity × encoded)`、PrimitiveGroup はスキップ）
- [x] ステップ 2.6 完了（2026-05-21、`OsmNode` + `OsmNodeParser` + 15 テスト、packed uint32 デコード、Info スキップ、座標は block.ToLon/ToLat 適用）
- [ ] ステップ 2.7 着手（DenseNodes 解析：差分圧縮デコード、現代 OSM PBF のメイン）

---

## 11. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 (draft) | 2026-05-20 | 初版骨子作成。実装順序・変換ツール csproj 配置・Phase 3 境界線・Span/Memory 化スコープをユーザー判断で確定したうえで起草 | Claude (Opus 4.7) |
| 0.1.1 (draft) | 2026-05-20 | ユーザー指摘「OsmDotRoute 最大の差別化要素である動的制約を最大限に活かすデータ実装に」を反映。§1 ゴール 5・方針追記、§3.1 形式構成にエッジ AABB / エッジ空間インデックス / シェイプ連続バッファ / エッジフラグを追加、§3.2 にホットパスゼロアロケート動機を明記、§3.6「動的制約を最大限に活かすデータ設計」を新設、§5.4 判断事項 4 件追加、§6 ステップ 1〜3・6 を動的制約特化に拡張、§8 リスク R8〜R9 追加、§9 性能目標に制約 100 件時劣化率・`Add*Area` 単発コスト追加 | Claude (Opus 4.7) |
| 0.1.2 (draft) | 2026-05-20 | §5.4 判断事項 10 件すべてユーザー確定（拡張子 `.odrg` / インデックス R-tree STR パック静的版 / AABB double / フラグできるだけ多く採用 / キャッシュ API はステップ 3 確定 / MMF 採用 / `System.CommandLine` / `Route.Shape` 破壊変更可 / ベンチ津島市 / プロファイル独自設計・Truck 10 t）。§3.1・§3.3・§3.5・§3.6・§6 ステップ表を確定値で書き換え。Phase 1 ベンチ対象都市の誤記（松山市→津島市）を修正 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-20 | **Phase 2/3 スコープを再編**（ユーザー判断 2026-05-20）。Phase 2 = `.odrg` 形式策定 + 独自 OSM PBF パーサー + PBF→`.odrg` 抽出ツール + (末尾オプション) RouterDb 変換ツール、Phase 3 = ランタイム読込 + Itinero 依存削除 + Bicycle/Truck + ベンチ + 親プロ統合。理由：Itinero RouterDb 変換ツールを意識した `.odrg` 設計は構造を最適化阻害するため、PBF からの直接抽出を Phase 2 主軸に据える。要件定義書を v2.3 へ同時改訂（REQ-MAP-004 を P3[Phase2-opt]、REQ-MAP-005/006 を Phase 3、REQ-MAP-007/008 を Phase 2、REQ-PRF-003/004 を Phase 3、REQ-PKG-002 を Phase 3 完了まで非公開、REQ-DEP-002/003 再構成）。§1 ゴール再構築、§3.3 独自 PBF パーサー節新設、§3.4 PBF→`.odrg` 抽出ツール節新設、§3.5 Phase 3 申し送り設計節新設、§3.7 RouterDb 変換ツール末尾オプション節新設、§5 プロジェクト構成変更（`OsmDotRoute.Pbf` / `OsmDotRoute.Extractor` 新規）、§5.6 新たな確定判断事項 5 件、§6 ステップ表を 7 段 → 5 段に短縮、§7 工数 14〜22 日 → 11〜20 日、§8 リスク R1〜R8 を新スコープ向けに再編、§9 性能基準は Phase 3 で検証する旨を明記 | Claude (Opus 4.7) |
| 0.2.1 (draft) | 2026-05-21 | ステップ 1 完了反映。仕様書 [`phase2_graph_format_spec.md`](phase2_graph_format_spec.md) v0.2 確定（残オープン課題 4 件をユーザー判断で確定：エッジフラグ 12 bit 案 / Edge Shape Buffer エッジ ID 順 / `bakedProfileIndex == edgeId` / R-tree M=16 初期値）。§6 ステップ表のステップ 1 を完了マーク化、§10 次のアクションをチェック更新（ステップ 2.3 着手を新規追加） | Claude (Opus 4.7) |
| 0.2.2 (draft) | 2026-05-21 | ステップ 2.3 完了反映。§6 ステップ表のステップ 2 を「2.1〜2.3 完了 / 2.4〜2.10 未着手」に更新。§10 次のアクションを「2.4 着手」へ繰下げ。設計書 §4.2 に `PbfBlobReader` の意図・採用設計・判断根拠・検証方法（25/25 テスト）を記録（[`phase2_design.md`](phase2_design.md) v0.2.2） | Claude (Opus 4.7) |
| 0.2.3 (draft) | 2026-05-21 | ステップ 2.4 完了反映。§6 ステップ表のステップ 2 を「2.1〜2.4 完了 / 2.5〜2.10 未着手」に更新。§10 次のアクションを「2.5 着手」へ繰下げ。設計書 §4.3 に `OsmHeaderParser` の意図・採用設計・判断根拠・検証方法（18/18 テスト、SupportedRequiredFeatures = OsmSchema-V0.6/DenseNodes）を記録（[`phase2_design.md`](phase2_design.md) v0.2.3） | Claude (Opus 4.7) |
| 0.2.4 (draft) | 2026-05-21 | ステップ 2.5 完了反映。§6 ステップ表のステップ 2 を「2.1〜2.5 完了 / 2.6〜2.10 未着手」に更新。§10 次のアクションを「2.6 着手」へ繰下げ。設計書 §4.4 に `PrimitiveBlock` + `OsmStringTable` + `PrimitiveBlockParser` の意図・採用設計・判断根拠・検証方法（19/19 テスト、座標変換式、PrimitiveGroup スキップ）を記録（[`phase2_design.md`](phase2_design.md) v0.2.4） | Claude (Opus 4.7) |
| 0.2.5 (draft) | 2026-05-21 | ステップ 2.6 完了反映。§6 ステップ表のステップ 2 を「2.1〜2.6 完了 / 2.7〜2.10 未着手」に更新。§10 次のアクションを「2.7 着手」へ繰下げ。設計書 §4.5 に `OsmNode` + `OsmNodeParser` の意図・採用設計・判断根拠・検証方法（15/15 テスト、packed uint32 + Info スキップ + 座標変換）を記録（[`phase2_design.md`](phase2_design.md) v0.2.5） | Claude (Opus 4.7) |
