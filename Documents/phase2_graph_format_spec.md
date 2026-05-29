# OsmDotRoute 独自バイナリグラフ形式 `.odrg` 仕様書

[English](phase2_graph_format_spec.en.md) | 日本語

**バージョン**: 0.2（ステップ 1 完了確定版）
**作成日**: 2026-05-20
**最終更新**: 2026-05-21
**ステータス**: v0.2 確定（Phase 2 ステップ 1 完了・ユーザー合意済）
**対象**: Phase 2 ステップ 1 成果物。OsmDotRoute 独自バイナリグラフ形式 `.odrg` のレイアウト・アルゴリズム・読書 API パターンを規定する
**関連ドキュメント**:

- [要件定義書](requirement_definition.md)（v2.3、REQ-MAP-003）
- [Phase 2 実装計画書](phase2_implementation_plan.md)（v0.2、§3.1 / §3.6）
- [Phase 2 設計書](phase2_design.md)（v0.2、§3）
- [Phase 1 設計書](phase1_design.md)（v0.21、§18 申し送り事項）

---

## 0. 本書の位置付けと読書ガイド

### 0.1 目的

本書は **OsmDotRoute 独自バイナリグラフ形式 `.odrg` の機械可読仕様**を提供する。`OsmDotRoute.Extractor`（書出側、Phase 2 ステップ 3）と `NativeRoadGraph` / `NativeRoadSnapper`（読込側、Phase 3）の両方が本仕様に従う。

### 0.2 設計原則（Phase 2 計画書 §3.1 / §3.6 より）

| #  | 原則                                                                                                                    | 由来                                                                                 |
| -- | ----------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| P1 | **動的制約のホットパスをデータ形式自体が直接支援する**                                                            | OsmDotRoute 最大の差別化要素（メモリ [[project-phase2-dynamic-restriction-design]]） |
| P2 | **Itinero RouterDb 構造に引きずられない自由設計**                                                                 | ユーザー判断 2026-05-20、メモリ [[project-phase2-scope-redefinition]]                |
| P3 | **`MemoryMappedFile` でゼロコピー読込**（`ReadOnlySpan<T>` / `ReadOnlyMemory<T>` で公開）                   | §5.4-6 計画書                                                                       |
| P4 | **エッジシェイプは連続バッファに配置**、Phase 3 で 77 MB/route アロケート根治（Phase 1 設計書 §18.4）            | §3.2 計画書                                                                         |
| P5 | **エッジ AABB / エッジ空間インデックス / エッジフラグを bake** し、ランタイムは O(1) 読み出しまたは O(log E) 検索 | §3.6 計画書                                                                         |
| P6 | **OSM タグから機械的に bake できる属性のみ採用**（航空写真・標高データ・動的更新要のものは不採用）                | §3.6 計画書                                                                         |
| P7 | **将来拡張に備えてセクションオフセットでルーズに連結**（バージョン跨ぎで未知セクションをスキップ可能）            | 一般原則                                                                             |
| P8 | **リトルエンディアン固定**、x64 Windows での処理性能を優先                                                        | プラットフォーム要件 REQ-NFR-006                                                     |

### 0.3 本書の更新ルール

ステップ 1 完了時に v0.1 → v0.2 へ昇格（ユーザー合意済、2026-05-21）。以降は実装で判明した制約・修正をその都度追記し、互換性破壊を伴う変更時のみメジャー昇格（v1.0）。

---

## 1. ファイル全体構造

### 1.1 ハイレベル構造

`.odrg` ファイルは以下のセクションを **オフセット参照型** で連結する：

```text
+------------------------------------------+ offset 0
| HEADER                                   | 固定長 256 バイト
+------------------------------------------+
| SECTION TABLE                            | 可変長
| (各セクションの (kind, offset, length))   |
+------------------------------------------+
| SECTION: Vertex Table                    |
+------------------------------------------+
| SECTION: Edge Table                      |
+------------------------------------------+
| SECTION: Edge Shape Buffer               |
+------------------------------------------+
| SECTION: Edge AABB Table                 |
+------------------------------------------+
| SECTION: Edge Flag Table                 |
+------------------------------------------+
| SECTION: Edge Spatial Index (R-tree)     |
+------------------------------------------+
| SECTION: Baked Profile Table             |
+------------------------------------------+
| SECTION: Reserved - Turn Restrictions    | （Phase 4+ で使用、Phase 2 は length=0）
+------------------------------------------+
| SECTION: Metadata                        | UTF-8 JSON 文字列
+------------------------------------------+ EOF
```

### 1.2 セクションテーブルの目的（P7）

未知のセクションを安全にスキップできるよう、各セクションは「種別 ID（uint16）+ オフセット（uint64）+ 長さ（uint64）」で表す。読込側は知らない種別はスキップする。

### 1.3 共通エンディアン

**リトルエンディアン**（little-endian）固定。整数・浮動小数のすべて。

---

## 2. ヘッダー詳細（HEADER, 固定 256 バイト）

| オフセット | サイズ | フィールド             | 型        | 説明                                                                        |
| ---------- | ------ | ---------------------- | --------- | --------------------------------------------------------------------------- |
| 0          | 8      | `magic`              | byte[8]   | ASCII `"ODRG\0\0\0\0"` （0x4F, 0x44, 0x52, 0x47, 0x00, 0x00, 0x00, 0x00） |
| 8          | 2      | `versionMajor`       | uint16    | フォーマットメジャー版（互換性破壊時に増やす）。初版 = 1                    |
| 10         | 2      | `versionMinor`       | uint16    | フォーマットマイナー版（後方互換な拡張）。初版 = 0、v0.3 = 1（`bboxRequested*` 追加） |
| 12         | 4      | `flags`              | uint32    | 予約フラグ（初版 = 0、bit0 = "compressed"（将来）、bit1〜31 = 予約）        |
| 16         | 8      | `vertexCount`        | uint64    | 頂点数                                                                      |
| 24         | 8      | `edgeCount`          | uint64    | エッジ数（**有向辺数**。双方向道路は 2 エッジ）                       |
| 32         | 8      | `bboxMinLon`         | double    | 全体バウンディングボックス 経度最小                                         |
| 40         | 8      | `bboxMinLat`         | double    | 全体バウンディングボックス 緯度最小                                         |
| 48         | 8      | `bboxMaxLon`         | double    | 全体バウンディングボックス 経度最大                                         |
| 56         | 8      | `bboxMaxLat`         | double    | 全体バウンディングボックス 緯度最大                                         |
| 64         | 4      | `profileCount`       | uint32    | bake 済みプロファイル数                                                     |
| 68         | 4      | `edgeFlagBytes`      | uint32    | エッジフラグの 1 エッジあたりバイト数（1 または 2）                         |
| 72         | 8      | `sectionTableOffset` | uint64    | セクションテーブル先頭オフセット（通常 256）                                |
| 80         | 4      | `sectionCount`       | uint32    | セクションテーブルのエントリ数                                              |
| 84         | 4      | `reservedA`          | uint32    | 予約（0 固定）                                                              |
| 88         | 8      | `bboxRequestedMinLon` | double   | **抽出要求時の bbox 経度最小**（v0.3+、CLI `--bbox` のユーザー入力。way 拡張前。VersionMinor=0 は 0.0） |
| 96         | 8      | `bboxRequestedMinLat` | double   | 同 緯度最小                                                                |
| 104        | 8      | `bboxRequestedMaxLon` | double   | 同 経度最大                                                                |
| 112        | 8      | `bboxRequestedMaxLat` | double   | 同 緯度最大                                                                |
| 120        | 136    | `reservedB`          | byte[136] | 予約（0 埋め）。将来のヘッダー拡張余地                                      |

総計 256 バイト。

**bbox（オフセット 32-63）と bboxRequested（88-119）の違い**:
- `bbox*` = 抽出結果の全頂点 AABB（way 拡張で要求 bbox を超える可能性）
- `bboxRequested*` = 抽出要求時の bbox（CLI `--bbox` の入力値）。VersionMinor=0 では未定義（ゼロ）、フォールバックは `bbox*` を使う

### 2.1 マジックナンバーの根拠

ASCII で `ODRG` = "OsmDotRoute Graph"。残り 4 バイトを `\0` で詰めて 8 バイトアライメント。

### 2.2 バージョニング規則

- `versionMajor` が異なる場合 → 読込側は **エラー**（互換性なし）
- `versionMinor` が読込側より大きい場合 → 読込側は **警告ログ + 続行**（後方互換、未知セクションはスキップ）
- `versionMinor` が読込側より小さい場合 → 読込側は **正常動作**（古いファイル）

**VersionMinor の履歴**:

| Minor | 追加内容 | 後方互換性 |
| --- | --- | --- |
| 0 | 初版 | — |
| 1 | `bboxRequested*`（オフセット 88-119）追加。要求 bbox を抽出後 bbox とは別に保持 | 旧コードはヘッダー末尾の予約領域として無視（安全）。新コードで Minor=0 を読む場合は `bboxRequested*` が未定義（ゼロ）として扱う |

---

## 3. セクションテーブル

ヘッダー直後、`sectionTableOffset` から `sectionCount` 個のエントリが並ぶ。各エントリは 24 バイト：

| オフセット | サイズ | フィールド   | 型     | 説明                               |
| ---------- | ------ | ------------ | ------ | ---------------------------------- |
| 0          | 2      | `kind`     | uint16 | セクション種別 ID（§3.1 参照）    |
| 2          | 2      | `reserved` | uint16 | 予約（0 固定）                     |
| 4          | 4      | `flags`    | uint32 | セクション固有フラグ（初版 = 0）   |
| 8          | 8      | `offset`   | uint64 | セクションのファイル先頭オフセット |
| 16         | 8      | `length`   | uint64 | セクション長（バイト）             |

### 3.1 セクション種別 ID 一覧

| ID             | 種別                   | 内容                                                     | Phase 2 必須            |
| -------------- | ---------------------- | -------------------------------------------------------- | ----------------------- |
| 0x0001         | Vertex Table           | 頂点配列                                                 | 必須                    |
| 0x0002         | Edge Table             | エッジ配列                                               | 必須                    |
| 0x0003         | Edge Shape Buffer      | エッジシェイプ連続バッファ                               | 必須                    |
| 0x0004         | Edge AABB Table        | エッジ AABB（double × 4）                               | 必須                    |
| 0x0005         | Edge Flag Table        | エッジフラグ（bitflag）                                  | 必須                    |
| 0x0006         | Edge Spatial Index     | STR パック静的 R-tree                                    | 必須                    |
| 0x0007         | Baked Profile Table    | プロファイル × エッジ →`(canPass, speedKmh, oneway)` | 必須                    |
| 0x0008         | Turn Restriction Table | ターン制限テーブル（Phase 4+）                           | 任意（length=0 で予約） |
| 0x0009         | Metadata               | UTF-8 JSON メタデータ                                    | 必須                    |
| 0x0100〜0xFFFF | 予約                   | 将来拡張                                                 | —                      |

未知 `kind` は読込側でスキップする（前方互換）。

---

## 4. セクション詳細

### 4.1 Vertex Table（kind = 0x0001）

頂点 ID（0 始まり、連番）の順に並ぶ固定長配列。

| オフセット | サイズ | フィールド | 型     | 説明          |
| ---------- | ------ | ---------- | ------ | ------------- |
| 0          | 8      | `lon`    | double | 経度（WGS84） |
| 8          | 8      | `lat`    | double | 緯度（WGS84） |

1 頂点 = 16 バイト。`vertexCount` × 16 バイト。

`ReadOnlySpan<Vertex>` で公開する内部構造：

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct Vertex
{
    public readonly double Lon;
    public readonly double Lat;
}
```

### 4.2 Edge Table（kind = 0x0002）

エッジ ID（0 始まり、連番）の順に並ぶ固定長配列。

| オフセット | サイズ | フィールド            | 型     | 説明                                                                            |
| ---------- | ------ | --------------------- | ------ | ------------------------------------------------------------------------------- |
| 0          | 4      | `fromVertexId`      | uint32 | 始端頂点 ID                                                                     |
| 4          | 4      | `toVertexId`        | uint32 | 終端頂点 ID                                                                     |
| 8          | 8      | `shapeOffset`       | uint64 | Edge Shape Buffer 内のシェイプ開始オフセット（バイト）                          |
| 16         | 4      | `shapeLength`       | uint32 | シェイプ点数（端点を含むかどうかは §4.3 参照）                                 |
| 20         | 4      | `bakedProfileIndex` | uint32 | Baked Profile Table 内のインデックス（プロファイル × エッジ の 2D 表へのキー） |

1 エッジ = 24 バイト。`edgeCount` × 24 バイト。

エッジ AABB とエッジフラグは別セクション（§4.4 / §4.5）に分離し、ホットパスで必要なものだけキャッシュラインに乗せる戦略。

### 4.3 Edge Shape Buffer（kind = 0x0003）

すべてのエッジのシェイプ点列を 1 本のバッファに連続配置。

各シェイプ点は `(double lon, double lat)` の 16 バイト。

`shapeLength` は端点（fromVertex / toVertex）を**含まない**中間点の数とする。`shapeLength = 0` は直線エッジ（端点 2 つのみ）を意味する。

#### 4.3.1 並び順（v0.2 確定）

シェイプ点は **エッジ ID 順** に配置する（2026-05-21 ユーザー判断）。

採用理由：

- 抽出ツール側の書出が単純（エッジを ID 順に処理 → そのままバッファ末尾に追記）
- 主ワークロード「エッジ ID 順スキャン」（プロファイル評価・ベンチマーク等）でキャッシュ局所性が効く
- Phase 3 R-tree クエリは結果としてランダム ID アクセスになるが、MMF ページキャッシュで吸収できる見込み

採用しなかった案：

- **Hilbert カーブ / R-tree 葉順での並べ替え**：Phase 3 ホットパス最適化として理論上は有効だが、抽出ツール実装が複雑化し、Phase 2 のスコープを超える。Phase 3 ステップ 3E のベンチで「ランダム ID アクセスがボトルネックになっている」と判定された場合に、Phase 3 末尾オプションとして再評価する

`NativeRoadGraph.GetShape(edgeId)` は次のいずれかのビューを返す：

- 中間点のみ：`ReadOnlySpan<GeoCoordinate>` 長さ `shapeLength`
- 端点込み：`GetFullShape(edgeId)` は端点を含む `shapeLength + 2` 点のビュー（実装側で `Vertex` をコピー注入する必要あり、ホットパスでは中間点版を使う）

### 4.4 Edge AABB Table（kind = 0x0004）

エッジ ID 順、固定長 32 バイト：

| オフセット | サイズ | フィールド | 型     |
| ---------- | ------ | ---------- | ------ |
| 0          | 8      | `minLon` | double |
| 8          | 8      | `minLat` | double |
| 16         | 8      | `maxLon` | double |
| 24         | 8      | `maxLat` | double |

`edgeCount` × 32 バイト。

`ReadOnlySpan<Aabb>` で公開：

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct Aabb
{
    public readonly double MinLon;
    public readonly double MinLat;
    public readonly double MaxLon;
    public readonly double MaxLat;
}
```

### 4.5 Edge Flag Table（kind = 0x0005）

エッジ ID 順、1 エッジあたり `edgeFlagBytes`（1 または 2 バイト、ヘッダーで指定）。

#### Phase 2 v0.2 で確定するビット割り当て

**2 バイト（uint16）** で確定（2026-05-21 ユーザー判断）。OSM タグから機械的に bake できる属性を広めに採用（§5.4-4 計画書、メモリ [[project-phase2-dynamic-restriction-design]]）。Bridge〜SchoolZone の 12 属性 + Oneway 2 bit = 14 bit 使用、残り 2 bit 予約。

| ビット | 名前                      | OSM タグ → bake ルール（v0.1 案）                                           |
| ------ | ------------------------- | ---------------------------------------------------------------------------- |
| 0      | `IsBridge`              | `bridge=yes` / `bridge=*` （`bridge=no` 除く）                         |
| 1      | `IsTunnel`              | `tunnel=yes` / `tunnel=*`                                                |
| 2      | `IsElevated`            | `layer >= 1` または `bridge=viaduct`                                     |
| 3      | `IsRoundabout`          | `junction=roundabout`                                                      |
| 4      | `IsToll`                | `toll=yes`                                                                 |
| 5      | `IsPrivateAccess`       | `access=private`                                                           |
| 6      | `IsServiceWay`          | `highway=service`                                                          |
| 7      | `IsTrack`               | `highway=track`（ダート路面、農道）                                        |
| 8      | `IsLivingStreet`        | `highway=living_street`（生活道路）                                        |
| 9      | `IsPedestrianSeparated` | `sidewalk=yes` / `sidewalk=both`（歩道分離あり）                         |
| 10     | `IsWinterClosed`        | `seasonal=winter` / `winter_road=no` 等（冬季閉鎖）                      |
| 11     | `IsSchoolZone`          | `hazard=school_zone` または近傍 `amenity=school`（通学路、初版は要検討） |
| 12     | `IsOnewayForward`       | `oneway=yes` （from → to 方向のみ通行可）                                 |
| 13     | `IsOnewayBackward`      | `oneway=-1` （to → from 方向のみ通行可）                                  |
| 14     | 予約                      | —                                                                           |
| 15     | 予約                      | —                                                                           |

`IsOnewayForward` と `IsOnewayBackward` のどちらも立っていなければ双方向。両方立つことはない。

#### 運用方針

§5.6-4 計画書のとおり「できるだけ多く採用、運用上不要と判断した時点で削る」方針。Phase 3 ステップ 3H で剪定判断を行う。

`SchoolZone` は OSM タグ単独 (`hazard=school_zone`) からの bake が難しい（実運用上ほぼ未使用、近傍 `amenity=school` 連携は複雑）。v0.2 では bit 11 を予約し、抽出ツール側では `IsSchoolZone = 0` 固定として出力する。Phase 3 で実用上不要と判断したら剪定、必要なら bake ルール（半径 N m 内に school 施設）をステップ 3 で確定する。

### 4.6 Edge Spatial Index（kind = 0x0006）— STR パック静的 R-tree

#### 4.6.1 STR (Sort-Tile-Recursive) アルゴリズム

ビルド手順（変換ツール側）：

1. 全エッジに対し AABB の中心点 (`(minLon+maxLon)/2`, `(minLat+maxLat)/2`) を計算
2. ノード分岐数 `M`（例：16）と総エッジ数 `N` から、葉ノード数 `L = ⌈N/M⌉`、ストリップ数 `S = ⌈√L⌉` を決定
3. エッジを経度（x）でソートし、`S` ストリップに分割
4. 各ストリップ内で緯度（y）でソートし、`M` ずつ葉ノードにまとめる
5. 葉ノードの AABB を「内包する全エッジ AABB の和」で計算
6. 葉ノードを子として再帰的に内部ノードを構築（同じ M, S アルゴリズム）
7. ルートまで集約

特徴：

- 静的データ向けに**最適配置**で一括ビルド可能
- ノード分岐数 M を固定すれば**高さ ⌈log_M(N)⌉**で済む
- ノード AABB が data-dependent に重ならない（オーバーラップ最小）

#### 4.6.2 R-tree シリアライズ

ノードを **配列化** し、子は配列インデックスで参照（ポインタを使わない、MMF フレンドリー）。

| オフセット | サイズ | フィールド              | 型          | 説明                       |
| ---------- | ------ | ----------------------- | ----------- | -------------------------- |
| 0          | 4      | `nodeCount`           | uint32      | 全ノード数（葉 + 内部）    |
| 4          | 4      | `rootIndex`           | uint32      | ルートノードのインデックス |
| 8          | 4      | `nodeBranchingFactor` | uint32      | 分岐数 M（v0.2 初期値 = 16、Phase 2 ステップ 3 で実測再評価） |
| 12         | 4      | `treeHeight`          | uint32      | ツリー高（参考情報）       |
| 16         | 〜     | ノード配列              | RTreeNode[] | 各ノードは 56 バイト固定   |

各ノードは：

| オフセット | サイズ | フィールド          | 型       | 説明                                                      |
| ---------- | ------ | ------------------- | -------- | --------------------------------------------------------- |
| 0          | 8      | `minLon`          | double   | ノード AABB                                               |
| 8          | 8      | `minLat`          | double   |                                                           |
| 16         | 8      | `maxLon`          | double   |                                                           |
| 24         | 8      | `maxLat`          | double   |                                                           |
| 32         | 4      | `firstChildIndex` | uint32   | 葉なら最初のエッジ ID、内部なら最初の子ノードインデックス |
| 36         | 4      | `childCount`      | uint32   | 子の数（葉なら含むエッジ数、内部なら子ノード数。M 以下）  |
| 40         | 4      | `flags`           | uint32   | bit0 =`IsLeaf`、bit1〜31 = 予約                         |
| 44         | 12     | `reserved`        | byte[12] | 0 埋め（将来拡張用）                                      |

1 ノード = 56 バイト。

#### 4.6.3 ランタイムクエリ（Phase 3 で実装）

「制約 polygon の AABB と交差するエッジ ID 集合」を取得するクエリ：

```text
Query(queryAabb):
  stack = [rootIndex]
  result = []
  while stack:
    nodeIdx = stack.Pop()
    node = nodes[nodeIdx]
    if not node.aabb intersects queryAabb: continue
    if node.IsLeaf:
      for i in 0..node.childCount-1:
        edgeId = node.firstChildIndex + i
        if edges[edgeId].aabb intersects queryAabb:
          result.Add(edgeId)
    else:
      for i in 0..node.childCount-1:
        stack.Push(node.firstChildIndex + i)
  return result
```

`Stack<int>` でループ実装（再帰なし）、`ReadOnlySpan<RTreeNode>` 上でビュー走査、ゼロアロケート。

### 4.7 Baked Profile Table（kind = 0x0007）

プロファイル × エッジ の 2D 表。プロファイルごとに各エッジの `(canPass, speedKmh, oneway)` を bake する。

#### 4.7.1 ヘッダー

| オフセット | サイズ | フィールド       | 型     | 説明                                               |
| ---------- | ------ | ---------------- | ------ | -------------------------------------------------- |
| 0          | 4      | `profileCount` | uint32 | プロファイル数（ヘッダー `profileCount` と同値） |
| 4          | 4      | `entrySize`    | uint32 | 1 エントリのサイズ（初版 = 8 バイト）              |

各プロファイル名は UTF-8 文字列のテーブルとして別途格納：

#### 4.7.2 プロファイル名表

`profileCount` 個のエントリ：

| オフセット | サイズ | フィールド     | 型     | 説明                       |
| ---------- | ------ | -------------- | ------ | -------------------------- |
| 0          | 4      | `nameOffset` | uint32 | 文字列バッファ内オフセット |
| 4          | 4      | `nameLength` | uint32 | UTF-8 バイト数             |

末尾に UTF-8 文字列バッファ。

#### 4.7.3 エントリ表（プロファイル × エッジ）

`profileCount × edgeCount` 個のエントリ（プロファイル単位でブロック化、各エッジ ID 順）：

| オフセット | サイズ | フィールド   | 型      | 説明                                                                       |
| ---------- | ------ | ------------ | ------- | -------------------------------------------------------------------------- |
| 0          | 4      | `speedKmh` | float   | 通行速度（km/h）。`canPass=false` なら 0                                 |
| 4          | 1      | `flags`    | byte    | bit0 =`CanPass`、bit1 = `Forward`、bit2 = `Backward`、bit3〜7 = 予約 |
| 5          | 3      | `reserved` | byte[3] | 0 埋め                                                                     |

1 エントリ = 8 バイト。Phase 1 の `ProfileEvaluator.Evaluate(IReadOnlyDictionary<string,string> osmTags)` を変換ツール側で呼んで bake。

#### 4.7.4 ランタイムアクセス

```csharp
// Edge Table の bakedProfileIndex を経由
var edgeRow = edges[edgeId];
var entry = bakedProfileTable[profileId, edgeRow.bakedProfileIndex];
// entry.speedKmh, entry.CanPass, entry.Forward, entry.Backward
```

#### 4.7.5 `bakedProfileIndex` の運用（v0.2 確定）

v0.2 では **`bakedProfileIndex == edgeId`** とする（2026-05-21 ユーザー判断、YAGNI）。

採用理由：

- 抽出ツール側の実装が単純（OSM タグ集合のハッシュテーブル不要）
- テーブルサイズは `profileCount × edgeCount × 8B`。津島市（57k エッジ × 2 プロファイル）で 0.9 MB、愛知県想定（数百万エッジ × 4 プロファイル）でも数十 MB 程度。MMF 経由でランタイム RAM 圧迫なし
- 同一 OSM タグ集合エッジの集約は **Phase 3 ベンチで `.odrg` ファイルサイズや IO 量がボトルネックと判明した場合のみ**、Phase 3 末尾オプションとして導入を再評価

採用しなかった案：

- **OSM タグ集合ハッシュでの集約**：テーブルサイズを 10〜30% 削減できる見込みだが、抽出ツールに `Dictionary<TagSet, uint>` 相当のハッシュテーブルが必要。Phase 2 のスコープを膨らませる割に効果が明確でない（Phase 1 ベンチでも IO がボトルネックではなかった）

### 4.8 Turn Restriction Table（kind = 0x0008、Phase 4+ 予約）

Phase 2 v0.1 では length = 0 で予約のみ。Phase 4+ で実装する際の予定レイアウト：

| オフセット | サイズ | フィールド           | 型                | 説明         |
| ---------- | ------ | -------------------- | ----------------- | ------------ |
| 0          | 4      | `restrictionCount` | uint32            | ターン制限数 |
| 4          | 〜     | エントリ配列         | TurnRestriction[] |              |

各エントリ案：`fromEdgeId(4) + viaVertexId(4) + toEdgeId(4) + restrictionType(1) + reserved(3)` = 16 バイト

セクションテーブルにエントリは登録するが `length = 0` とする。

### 4.9 Metadata（kind = 0x0009）

UTF-8 JSON 文字列。例：

```json
{
  "createdAt": "2026-05-21T10:00:00Z",
  "createdBy": "OsmDotRoute.Extractor 0.2.0",
  "sourcePbf": "tsushima.osm.pbf",
  "sourcePbfHash": "sha256:abcdef...",
  "profiles": ["car", "pedestrian"],
  "edgeFlagBits": {
    "0": "IsBridge",
    "1": "IsTunnel",
    "...": "..."
  },
  "rtreeBranchingFactor": 16,
  "phase1EquivalenceCheck": {
    "routerDb": "tsushima.routerdb",
    "vertexDelta": 0,
    "edgeDelta": 0
  }
}
```

JSON 内容は実装で発展。互換性破壊は禁止（後方互換に保つ）。

---

## 5. 書出（Extractor 側）アルゴリズム

### 5.1 全体パイプライン

```text
1. PBF 読込（OsmDotRoute.Pbf）
   → IEnumerable<OsmNode> / OsmWay / OsmRelation

2. 道路 way フィルタ
   → highway=* を取り、access=no / area=yes を除外（§5.6-17 計画書）

3. 頂点候補抽出
   → 全 way の参照ノード、交差点（次数 ≥ 3 のノード）、末端を頂点候補に
   → way の中間ノード（次数 2 で道路 way 専用）はシェイプ点に

4. 頂点 ID 採番
   → 頂点候補に 0 始まりの連番を割り当て

5. エッジ生成
   → 各 way を頂点間に分割
   → fromVertexId / toVertexId / shape（中間点列）/ OSM タグ集合 を確定
   → oneway を考慮（双方向道路は 2 エッジ）

6. エッジ AABB 計算
   → 端点 + シェイプ全点から minLon, minLat, maxLon, maxLat を求める

7. エッジフラグ抽出
   → §4.5 のビット割り当て表に従い、OSM タグ → 16 bit へ詰める

8. STR R-tree 構築
   → §4.6.1 のアルゴリズム

9. Baked Profile Table 構築
   → 各プロファイル × 各エッジで ProfileEvaluator.Evaluate
   → (canPass, speedKmh, oneway) を埋める

10. ファイル書出
    → ヘッダー（一旦 0 で埋める、最後に上書き）
    → セクションテーブル（一旦予約、最後に上書き）
    → 各セクション本体
    → 戻ってヘッダーとセクションテーブルを確定値で上書き
```

### 5.2 ストリーミングと一時バッファ

PBF は数 GB に達する可能性があるため、各フェーズはストリーミング想定。ただし「エッジ AABB」「STR R-tree ビルド」はメモリ内に全エッジ AABB が必要。津島市 PBF（数万エッジ）なら数 MB で済む見込み。

愛知県全体（数百万エッジ）でも 32 バイト × 数百万 = 数十 MB 程度。

### 5.3 Japan-wide PBF からの bbox 範囲抽出戦略

`japan-latest.osm.pbf`（2.3 GB、§8.3）を入力源として、**最大都道府県単位の bbox 範囲**を `.odrg` に抽出する場合の RAM ピーク予測と対策。地域別 PBF が県境跨ぎ道路ネットを切ってしまう問題を回避するため、Japan-wide PBF + `--bbox` がデフォルトワークフロー（§8.3）。

**前提**：出力 `.odrg` の最大サイズは都道府県単位（~1 GB、§8.2）。Japan-wide `.odrg` は作らない。

#### 5.3.1 `--bbox` は Japan-wide PBF 使用時の必須機能

CLI 必須オプション：

```text
osmdotroute-extractor extract \
  --input japan-latest.osm.pbf \
  --bbox 136.70,35.16,136.78,35.20 \
  --profiles car,pedestrian \
  --output tsushima.odrg
```

座標は **lon,lat 直接指定のみ**（v0.1.3 確定）：`--bbox minLon,minLat,maxLon,maxLat` の 4 値カンマ区切り。WGS84。

- メッシュコード指定 / 都道府県名プリセットは v0.1 では採用しない（Phase 3 以降で利用要望があれば追加）
- `--bbox` を **指定しない場合はエラーで中断**（v0.1.3 確定）。Phase 2 要件外の Japan-wide `.odrg` 誤生成を文字通り不可能にする。PBF HeaderBlock の自動 bbox 採用や警告継続もしない

#### 5.3.2 ナイーブ実装の問題（PBF 走査側）

OSM Japan は約 700M ノード。「ノード ID → 位置」辞書を**全 PBF 分**一括構築すると 700M × 24 B = **約 17 GB** で、16 GB RAM 機では不可能。

ただし出力範囲を bbox に絞れば、保持すべきノードはその範囲内のものだけになる。例：津島市の bbox なら数万〜数十万ノード、都道府県の bbox なら数百万ノード規模。

#### 5.3.3 採用戦略：2 パススキャン + bbox 早期フィルタ

PBF のブロック構造（PrimitiveBlock）を利用し、bbox 内のデータだけメモリ展開する：

##### パス 1：bbox 内のノード ID を特定（事前フィルタ）

1. PBF を先頭から走査、Node / DenseNodes ブロックを処理
2. 各ノードの (lon, lat) が `--bbox` 内かを判定
3. bbox 内のノード ID をビットセットに記録
4. bbox 内ノード数を N_in と呼ぶ（都道府県単位で N_in ≈ 数百万）

##### パス 2：道路 way フィルタと参照ノード追加（完全 way 単位採用、v0.1.3 確定）

1. PBF を再走査、Way ブロックを処理
2. 道路フィルタ（`highway=*` + `access` 除外）を通った way について：
   - way の参照ノードがビットセット（bbox 内）と **1 つでも重なれば、その way 全体を採用候補に**
   - way の参照ノード**全部**を「必要ノード集合」に追加（bbox 外でも、bbox 内 way の続きならシェイプとして必要）
3. これで bbox を**少しはみ出した** way の連続性を保てる（県境跨ぎ道路がシェイプとして完結する）

採用方針として「**完全 way 単位**」を v0.1.3 で確定（バッファ付き bbox や接続性ベース再帰は不採用）。理由：

- OSM の way は交差点・属性変わり目で自然分割されており、way 単独の長さは予測不能に大きくならない
- バッファ付き bbox はバッファ幅のチューニングが必要で、way 長を超えるとやはり連続性が切れる
- 接続性ベース再帰は最悪 Japan 全体を巻き込むリスクがあり、打ち切り深さの調整が複雑

##### パス 3：必要ノードの位置をメモリ展開

1. PBF を再走査、Node / DenseNodes ブロックを処理
2. 必要ノード集合に該当する ID のみ「ID → (lon, lat)」を `Dictionary<long, (double, double)>` に格納
3. 都道府県単位なら数百万ノード × 24 B ≈ 数十〜数百 MB

##### エッジ構築

1. 採用 way を走査、頂点正規化、エッジ・シェイプ生成
2. エッジ AABB 計算と並行して `List<EdgeRecord>` に蓄積
3. 都道府県単位で数百万エッジ × ~64 B ≈ 数百 MB

##### STR R-tree ビルド + 書出

1. エッジ AABB 中心の x ソート / ストリップ分割 / y ソート（インプレース）
2. 各セクションを順番にディスク書出（ストリーミング）

#### 5.3.4 RAM ピーク予測（都道府県単位を bbox に指定）

| フェーズ          | RAM ピーク（都道府県単位） |
| ----------------- | -------------------------- |
| パス 1 完了時     | ~100 MB（ビットセット）    |
| パス 2 完了時     | ~150 MB                    |
| パス 3 完了時     | ~500 MB（ノード辞書）      |
| エッジ構築中      | ~1 GB                      |
| R-tree ビルド中   | ~1.2 GB                    |

**16 GB 機での実行可能性**：余裕あり（ピーク 1.2 GB）。
**8 GB 機**：問題なく動作。

津島市単位（市レベル bbox）なら全フェーズ通じて 200 MB 以下に収まる見込み。

#### 5.3.5 抽出時間の概算

PBF を 3 パス走査するため、津島市単位でも Japan 全体 PBF を読み切る時間が支配的。津島市単位で数分、都道府県単位で 5〜15 分程度を見込む。ステップ 3 で実測する。

将来最適化案（v0.2+）：PBF の `BlobHeader` には bbox 情報がないため bbox 外ブロックの完全スキップは難しいが、`HeaderBlock` 経由のインデックス付き PBF（`required_features = "BoundingBox"` 等）対応で短縮可能。Phase 2 v0.1 では純粋に 3 パスシーケンシャル走査で実装する。

---

## 6. 読込（NativeRoadGraph、Phase 3）パターン

### 6.1 MMF オープン

```csharp
using var mmf = MemoryMappedFile.CreateFromFile(odrgPath, FileMode.Open);
using var accessor = mmf.CreateViewAccessor();

// ヘッダー読込
accessor.Read<Header>(0, out var header);

// マジック検証
if (header.MagicAsString != "ODRG\0\0\0\0") throw ...;

// バージョン検証
if (header.VersionMajor != 1) throw ...;
```

### 6.2 セクションテーブル読込

```csharp
var sectionTable = new SectionEntry[header.SectionCount];
accessor.ReadArray(header.SectionTableOffset, sectionTable, 0, sectionTable.Length);

// 種別ごとにオフセット解決
var vertexSection = sectionTable.First(s => s.Kind == 0x0001);
```

### 6.3 ゼロコピー Span ビュー（理想形）

```csharp
unsafe
{
    byte* basePtr = null;
    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);
    try
    {
        var vertices = new ReadOnlySpan<Vertex>(
            basePtr + vertexSection.Offset,
            (int)header.VertexCount);
        // vertices[i].Lon, vertices[i].Lat にアクセス。コピーなし
    }
    finally
    {
        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
    }
}
```

`unsafe` を局所化する `MemoryMappedSegment<T>` ラッパー型を導入予定（Phase 3）。

### 6.4 R-tree クエリ呼出

```csharp
// 制約 polygon の AABB を計算
var queryAabb = polygon.GetAabb();

// R-tree クエリ
var candidateEdgeIds = rtree.Query(queryAabb);  // Stack ベース、ゼロアロケート

// 各候補に polygon-shape 交差判定
foreach (var edgeId in candidateEdgeIds)
{
    var shape = graph.GetShape(edgeId);  // ReadOnlySpan<GeoCoordinate>
    if (polygon.Intersects(shape)) { ... }
}
```

---

## 7. 妥当性検証ルール（読込側で確認）

| 検証項目               | エラー条件                                                       |
| ---------------------- | ---------------------------------------------------------------- |
| マジック               | `magic != "ODRG\0\0\0\0"`                                      |
| メジャー版             | `versionMajor != 1`                                            |
| ヘッダーサイズ         | ファイルサイズ < 256                                             |
| バウンディングボックス | `minLon >= maxLon` または `minLat >= maxLat`                 |
| 頂点数整合             | Vertex Table の `length != vertexCount * 16`                   |
| エッジ数整合           | Edge Table の `length != edgeCount * 24`                       |
| AABB 数整合            | Edge AABB Table の `length != edgeCount * 32`                  |
| エッジフラグ整合       | Edge Flag Table の `length != edgeCount * edgeFlagBytes`       |
| シェイプオフセット     | `shapeOffset + shapeLength * 16` が Shape Buffer 内            |
| 頂点 ID 範囲           | `fromVertexId < vertexCount` かつ `toVertexId < vertexCount` |
| R-tree ルート          | `rootIndex < nodeCount`                                        |
| メタデータ             | UTF-8 として有効な JSON                                          |

検証は Phase 3 の `NativeRoadGraph` 読込時、または MapVerifier の `.odrg` 検査機能で実施。

---

## 8. ファイルサイズ概算（津島市基準）

Phase 1 RouterDb 津島市 = 約 14 MB（参考、Itinero RouterDb は CHContraction 等含む）。

`.odrg` 津島市の概算：

| セクション          | 1 エッジあたり                | 津島市（57k エッジ、Phase 1 ベンチ参考） |
| ------------------- | ----------------------------- | ---------------------------------------- |
| Vertex Table        | （頂点数 × 16）              | 約 40k 頂点 × 16 = 0.64 MB              |
| Edge Table          | 24 バイト                     | 57k × 24 = 1.4 MB                       |
| Edge AABB Table     | 32 バイト                     | 57k × 32 = 1.8 MB                       |
| Edge Flag Table     | 2 バイト                      | 57k × 2 = 0.1 MB                        |
| Edge Shape Buffer   | 平均 80 バイト（中間点 5 つ） | 57k × 80 = 4.6 MB                       |
| Edge Spatial Index  | 56 バイト × 約 4k ノード     | 0.2 MB                                   |
| Baked Profile Table | 2 プロファイル × 8 バイト    | 57k × 16 = 0.9 MB                       |
| Metadata            | 数 KB                         | 0.01 MB                                  |
| **合計概算**  | —                            | **約 9.6 MB**                      |

Phase 1 RouterDb（14 MB）の約 0.7 倍。MMF 経由なので RAM 使用量は逐次的。

### 8.1 愛知県全体（参考、Phase 3 都道府県単位ベンチ対象）

数百万エッジ規模と仮定して 100 倍：約 **1 GB**。Phase 3 ステップ 3G で実測する。

### 8.2 都道府県単位（Phase 3 ベンチ対象の最大サイズ）

数百万エッジ規模と仮定して 100 倍：約 **1 GB**。Phase 3 ステップ 3G で実測する。

**`.odrg` の最大想定サイズはこれ**。Japan-wide まで作る必要はない（地域跨ぎ道路ネットも、出力範囲を都道府県単位に絞れば bbox 内に収まる、§8.3 参照）。

### 8.3 Japan-wide PBF を入力源として使う理由

**参考 PBF**：`D:\workspace\災害廃棄物処理シミュレーション\Data\japan-latest.osm.pbf`（2.3 GB、Geofabrik 配布 OSM 日本全体）

`.odrg` は最大都道府県単位だが、**入力 PBF は Japan-wide を使う**。理由：

- 地域別配布 PBF（Geofabrik の都道府県別など）は **境界付近の道路ネットワークが切れている** 場合がある
- 例：愛知県 PBF 単体では、岐阜県・三重県との県境を跨ぐ国道・高速道路の way が途中で切断され、シミュレーションで使う道路ネットワークとして不完全
- Japan-wide PBF をソースに使い、`--bbox` で出力範囲を都道府県単位に絞って抽出することで、**県境を越える道路 way もシェイプが完結した状態で取り込める**

つまり「入力 = Japan-wide PBF」「出力 = 最大都道府県の `.odrg`（~1 GB）」がデフォルトワークフロー。

### 8.4 入力 PBF と出力 `.odrg` のサイズ比較

| 用途               | 入力 PBF                 | `--bbox` 指定 | 出力 `.odrg`       |
| ------------------ | ------------------------ | ------------- | ------------------ |
| 単一市（津島市）   | 地域 PBF or japan-latest | 市の bbox     | 約 9.6 MB          |
| 都道府県（愛知県） | japan-latest（推奨）     | 県の bbox     | 約 1 GB（推定）    |
| 県境跨ぎエリア     | japan-latest（必須）     | エリア bbox   | 数百 MB〜1 GB      |
| 日本全国           | （要件外）               | （要件外）    | （Phase 2 範囲外） |

抽出ツール側の RAM ピーク予測は §5.3.3 を参照（PBF を全走査する必要があるが、bbox で出力範囲を絞れば中間バッファは出力規模に比例）。

---

## 9. 公開 API（Phase 3 ランタイム実装時）

### 9.1 `NativeRoadGraph`（IRoadGraph 実装）

```csharp
internal sealed class NativeRoadGraph : IRoadGraph, IDisposable
{
    public static NativeRoadGraph Open(string odrgPath);

    public int VertexCount { get; }
    public int EdgeCount { get; }
    public Aabb Bounds { get; }

    public GeoCoordinate GetVertex(int vertexId);
    public ReadOnlySpan<GeoCoordinate> GetShape(int edgeId);
    public Aabb GetEdgeAabb(int edgeId);
    public EdgeFlags GetEdgeFlags(int edgeId);
    public (int fromVertexId, int toVertexId) GetEdgeEndpoints(int edgeId);
    public (bool canPass, float speedKmh, bool forward, bool backward) GetBakedProfile(int profileId, int edgeId);

    public IEnumerable<int> QuerySpatialIndex(Aabb queryAabb);  // R-tree クエリ
    public void Dispose();
}
```

### 9.2 `NativeRoadSnapper`（IRoadSnapper 実装）

```csharp
internal sealed class NativeRoadSnapper : IRoadSnapper
{
    public NativeRoadSnapper(NativeRoadGraph graph);
    public GeoCoordinate? Snap(GeoCoordinate point, float searchRadiusMeters);
}
```

実装：R-tree で検索半径の AABB クエリ → 候補エッジに対しシェイプ最短距離計算 → 最近接点を返す。

### 9.3 `RouterDb.LoadFromFile` の改変（Phase 3）

```csharp
public static RouterDb LoadFromFile(string path)
{
    if (path.EndsWith(".odrg", StringComparison.OrdinalIgnoreCase))
        return new RouterDb(NativeRoadGraph.Open(path));
  
    if (path.EndsWith(".routerdb", StringComparison.OrdinalIgnoreCase))
        throw new NotSupportedException(
            "Phase 2 以降、.routerdb 直接読込は廃止。OsmDotRoute.Extractor で .odrg に変換してください。" +
            "移行手順は README §x.x を参照。");
  
    throw new NotSupportedException($"未対応の拡張子: {path}");
}
```

---

## 10. オープン課題と次のアクション

### 10.1 ステップ 1 完了に向けて確認すべきこと

- [x] **エッジフラグ（§4.5）の bit 割り当て**：12 種採用案で確定（v0.2、2026-05-21、§4.5）
- [x] **R-tree 分岐数 M = 16**：v0.2 初期値 = 16、Phase 2 ステップ 3 で実測再評価（§4.6.2）
- [x] **Baked Profile Table の集約**：`bakedProfileIndex == edgeId` で確定（v0.2、2026-05-21、§4.7.5）
- [x] **Edge Shape Buffer の整列**：エッジ ID 順で確定（v0.2、2026-05-21、§4.3.1）
- [x] **`--bbox` オプションの座標系**：lon/lat 直接指定のみ（v0.1.3 確定、§5.3.1）
- [x] **bbox 外シェイプの扱い（§5.3.3 パス 2）**：完全 way 単位採用（v0.1.3 確定、§5.3.3）
- [x] **`--bbox` 未指定時の挙動**：エラーで中断（v0.1.3 確定、§5.3.1）

### 10.2 ステップ 2-3 以降で実測判断する事項

- R-tree 分岐数 M：Phase 2 ステップ 3 で 8 / 16 / 32 を実測比較、最適値を `nodeBranchingFactor` に確定
- エッジフラグ bit 11 (`SchoolZone`) の bake ルール（半径 N m 内 `amenity=school`）：Phase 3 で実用上必要なら確定、不要なら剪定
- Baked Profile Table の OSM タグ集合集約：Phase 3 ベンチで `.odrg` ファイルサイズ・IO がボトルネックと判明した場合のみ末尾オプションで再評価

### 10.3 Phase 3 で確定する事項

- §6 ゼロコピー Span ビューの実装パターン（`MemoryMappedSegment<T>` ラッパー型）
- §9 公開 API シグネチャの最終確定
- §7 妥当性検証の実装位置（`NativeRoadGraph.Open` 内 / 別 `OdrgValidator` クラス）
- Edge Shape Buffer の空間局所性並べ替え（必要と判定された場合の末尾オプション）

---

## 11. 改訂履歴

担当：Claude (Opus 4.7)

### v0.1 (draft) — 2026-05-20

初版骨子作成。

- ヘッダー / セクションテーブル / 9 セクションの全レイアウトを提示
- STR R-tree アルゴリズム（ビルド + シリアライズ + クエリ）
- 書出パイプライン / MMF 読込パターン / 妥当性検証
- 津島市サイズ概算（約 9.6 MB）/ Phase 3 公開 API 案
- エッジフラグは 16 bit のうち 12 ビット割り当て案
- R-tree 分岐数 M=16 初期値、`bakedProfileIndex` 集約は要検討で v0.2 持ち越し

### v0.1.1 (draft) — 2026-05-20

Japan-wide PBF (2.3 GB) 対応を反映（v0.1.2 で誤解訂正済）。

- §8.2 日本全国規模 / §5.3 Japan-wide 抽出戦略 / §10.1 課題 3 件を初版追加

### v0.1.2 (draft) — 2026-05-20

ユーザー訂正「Japan PBF は地域跨ぎ道路ネットのためのソース、出力 `.odrg` は依然として最大都道府県」を反映。

- §8.2 を「都道府県単位（~1 GB）」に書き直し、Japan-wide `.odrg`（7〜8 GB）概算を撤回
- §8.3「Japan-wide PBF を入力源として使う理由」新設（県境跨ぎ道路ネット問題）
- §8.4「入力 PBF と出力 `.odrg` のサイズ比較表」新設
- §5.3 を「Japan-wide PBF からの bbox 範囲抽出戦略」に書き直し
- `--bbox` を必須機能化（Japan-wide PBF 使用時の前提）
- 抽出戦略を 3 パススキャン（bbox 早期フィルタ含む）に変更
- RAM ピーク 6 GB → 1.2 GB に修正
- §10.1 オープン課題を bbox 周りに再整理

### v0.1.3 (draft) — 2026-05-20

§10.1 オープン課題 3 件をユーザー判断で確定。

- `--bbox` 座標系は **lon/lat 直接指定のみ**（メッシュコード・都道府県名プリセットは Phase 3+ 検討）
- bbox 外シェイプ way の扱いは **完全 way 単位採用**（バッファ付き bbox や接続性ベース再帰は不採用、§5.3.3 に採用理由を記述）
- `--bbox` 未指定時は **エラーで中断**（PBF HeaderBlock の自動採用や警告継続もしない、Phase 2 要件外の Japan-wide `.odrg` 誤生成を不可能にする）
- §5.3.1 / §5.3.3 に確定値を反映、§10.1 から該当 3 項目を完了マーク化

### v0.2 — 2026-05-21

ステップ 1 完了確定版。§10.1 残オープン課題 4 件をユーザー判断で確定。

- **エッジフラグ（§4.5）**：12 属性 + Oneway 2 bit = 14 bit 使用、残り 2 bit 予約で確定。`SchoolZone`（bit 11）は v0.2 では予約のみ、抽出ツール側は 0 固定出力。Phase 3 で実用上不要なら剪定、必要なら bake ルール（半径 N m 内 `amenity=school`）確定
- **Edge Shape Buffer の並び順（§4.3.1 新設）**：エッジ ID 順で確定。Hilbert カーブ / R-tree 葉順は Phase 3 ベンチで必要と判定された場合の末尾オプションとして §10.3 に申し送り
- **Baked Profile Table の `bakedProfileIndex`（§4.7.5 新設）**：`bakedProfileIndex == edgeId` で確定（YAGNI）。OSM タグ集合集約は Phase 3 で IO がボトルネックと判定された場合のみ再評価
- **R-tree 分岐数 M（§4.6.2）**：v0.2 初期値 = 16、Phase 2 ステップ 3 で実測再評価する旨を明記
- §10.1 を全件確定マーク化、§10.2「ステップ 2-3 以降で実測判断する事項」を新設、§10.3 に「Edge Shape Buffer の空間局所性並べ替え」を追加
- ステータスを「v0.2 確定（ユーザー合意済）」に変更、§0.3 更新ルールに「互換性破壊を伴う変更時のみメジャー昇格（v1.0）」を追記
