# Phase 2 ステップ 5: Phase 2 検証・確定 計画書

**ステータス**: ドラフト v0.1（ユーザーレビュー待ち、2026-05-25）
**対応ステップ**: Phase 2 ステップ 5（[実装計画書 §6](phase2_implementation_plan.md#L290)）
**対応要件**: 直接の REQ なし（メタステップ）／間接的に REQ-MAP-008（PBF→`.odrg`）の最終受け入れ
**関連文書**:

- [Phase 2 実装計画書 §6 ステップ 5 / §8 リスク R3 R8](phase2_implementation_plan.md)
- [Phase 2 設計書 §7 検証と完了判定 / §8 Phase 3 申し送り](phase2_design.md#L1537)
- [Phase 2 グラフ形式仕様書](phase2_graph_format_spec.md)
- [Phase 1 ステップ 17 検証チェックリスト（参考フォーマット）](phase1_step17_verification_checklist.md)

---

## 1. 目的とゴール

**目的**: ステップ 3 で生成された `.odrg` ファイルが Phase 1 RouterDb と等価な道路ネットワークを表していることを検証し、Phase 2 を確定する。

**Phase 2 完了判定の根拠（[設計書 §7](phase2_design.md#L1537) より）**:

1. ✅ `OsmDotRoute.Extractor` が津島市 `.osm.pbf` から `.odrg` を生成できる（ステップ 3.9 で完了済）
2. ⏳ 出力 `.odrg` の頂点数・辺数が Phase 1 RouterDb と妥当な範囲で一致
3. ⏳ エッジ AABB / R-tree が整合（ランダム検査で OK）
4. ⏳ エッジフラグが OSM タグから正しく bake されている
5. ⏳ bake プロファイルが Phase 1 `ProfileEvaluator` の結果と一致

本ステップで残 4 件を満たし、v0.2.0 タグ判断に進む。

---

## 2. 前提と現状

### 2.1 既存資産

- `tsushima_extract.osm.pbf` (13 MB) → `tsushima.odrg` (3.7 MB) 抽出済（ステップ 3.9）
- 頂点 27,235 / エッジ 38,004（津島市 bbox `136.65,35.13,136.80,35.25` 内）
- 全 478 テスト pass（Phase 1: 153 + Pbf: 169 + Extractor: 156）

### 2.2 Phase 1 RouterDb の所在

**重要**: Phase 1 比較対象は親プロジェクトの `default.routerdb` を流用。

- パス: [`tests/OsmDotRoute.Tests/TestData/TestPaths.cs:14`](../tests/OsmDotRoute.Tests/TestData/TestPaths.cs#L14)
- 実体: `d:\workspace\災害廃棄物処理シミュレーション\App\DisasterWasteSim.Server\Data\Scenarios\default.routerdb`
- **bbox 不一致**: RouterDb は親プロ作成時の広めの範囲（愛知県西部周辺）、`.odrg` は津島市 bbox のみ。**直接比較ではなく `.odrg` bbox 内に絞った Phase 1 RouterDb 頂点/エッジ数と比較**する必要あり。

### 2.3 OdrgWriter 既存範囲

[`src/OsmDotRoute.Extractor/Pipeline/OdrgWriter.cs`](../src/OsmDotRoute.Extractor/Pipeline/OdrgWriter.cs):

- HEADER 256B / SECTION TABLE 9×24B / 9 セクション本体（仕様書 §1〜§4）
- リトルエンディアン固定、`BinaryWriter` ベース、単一パス書出

OdrgReader はこれと対称な eager-parse 実装を新規追加する。

---

## 3. スコープ

### 3.1 スコープ内

- `OdrgReader`（Extractor 内、managed eager-parse、`OdrgWriter` と対）
- `.odrg` 構造整合テスト（R-tree bijection、AABB 包含、エッジ端点解決、シェイプ連続性）
- Phase 1 RouterDb との統計比較テスト（bbox 絞込条件付き）
- bake プロファイル サンプリング比較テスト（OSM タグ → `ProfileEvaluator` の結果一致）
- MapVerifier Server + Web 拡張（`.odrg` ロード + RouterDb と重ね表示）
- 設計書 §7 §8 初版執筆
- v0.2.0 タグ付与判断

### 3.2 スコープ外

- ランタイム `.odrg` 読込実装（`NativeRoadGraph` / `NativeRoadSnapper`）→ **Phase 3**
- MMF + `ReadOnlySpan<>` での高速読込 → **Phase 3**（`OdrgReader` は managed eager-parse 限定）
- ステップ 4-opt（RouterDb→`.odrg` 変換ツール）→ **本ステップでは判断のみ。実施するなら別ステップ**
- 経路結果の比較（同じ起終点で経路が一致するか）→ **Phase 3** で `.odrg` ランタイムが動いてから実施

---

## 4. サブステップ詳細

### 4.1 サブステップ 5.1: OdrgReader 実装

**目的**: `.odrg` を managed eager-parse で読み込み、テストとオーバーレイ両方から使える API を提供。

**追加ファイル**:

- `src/OsmDotRoute.Extractor/Pipeline/OdrgReader.cs` — エントリポイント `OdrgReader.Read(string path)` / `OdrgReader.Read(Stream stream)`
- `src/OsmDotRoute.Extractor/Pipeline/OdrgReadResult.cs` — 読込結果コンテナ
  - `OdrgHeader Header`
  - `OdrgSectionTableEntry[] SectionTable`
  - `GeoCoordinate[] Vertices`
  - `EdgeRecordRead[] Edges`（FromVertexId / ToVertexId / ShapePointCount / ShapeOffset / BakedProfileIndex）
  - `GeoCoordinate[][] EdgeShapes`（エッジ ID 順、ジャグ配列でテストしやすく）
  - `Aabb[] EdgeAabbs`
  - `EdgeFlags[] EdgeFlags`
  - `RTreeRead RTree`（NodeCount / RootIndex / BranchingFactor / TreeHeight / Nodes）
  - `BakedProfileTableRead ProfileTable`（プロファイル名・エッジごとの `BakedProfileEntry`）
  - `byte[] TurnRestrictionRaw`（v0.2 は 0 byte）
  - `string MetadataJson`

- `tests/OsmDotRoute.Tests/Extractor/OdrgReaderTests.cs` — 単体テスト
  - 既存 `OdrgWriterTests` の出力を読み戻し、書出値と一致することを確認（往復テスト）
  - MagicBytes 不正なら例外
  - VersionMajor 不一致なら例外
  - SectionCount 不正なら例外
  - 各セクションのオフセット/サイズが HeaderSize/SectionTableEntrySize と整合
  - 空のエッジ／空のシェイプを許容

**完了条件 (DoD)**:

- `OdrgWriter` 出力 → `OdrgReader` で読込 → 全フィールド一致（往復テスト）
- `tsushima.odrg` を読込めて、頂点 27,235 / エッジ 38,004 と一致
- 単体テスト 15 件以上 pass
- 全体テスト 478 + 5.1 新規分 pass

**工数感**: 0.5 〜 1 日

---

### 4.2 サブステップ 5.2: `.odrg` 構造整合テスト

**目的**: 仕様書 §4 のセクション間整合を `tsushima.odrg` 実データで検査。

**追加ファイル**:

- `tests/OsmDotRoute.Tests/Extractor/OdrgIntegrityTests.cs`
  - **INT-1**: 全エッジの `FromVertexId` / `ToVertexId` < `vertexCount`
  - **INT-2**: 全エッジの `ShapeOffset` + `ShapePointCount` ≤ シェイプバッファ全長
  - **INT-3**: 全エッジの AABB が、そのエッジのシェイプ全点を包含する（仕様書 §4.4）
  - **INT-4**: 全エッジの AABB が `.odrg` ファイル全体の bbox 内に収まる
  - **INT-5**: R-tree 全葉ノードのエッジ ID が `{0,1,...,edgeCount-1}` の bijection（permutation）
  - **INT-6**: R-tree 全内部ノードの Bounds が子ノードの Bounds を包含
  - **INT-7**: R-tree root.Bounds が `.odrg` ファイル全体の bbox を包含
  - **INT-8**: R-tree TreeHeight × BranchingFactor で全エッジを葉に収納可能（容量整合）
  - **INT-9**: BakedProfileEntry の `SpeedKmh ≥ 0`、`Flags` の通行可否ビット (`Allowed`) を持つエッジは少なくとも car / pedestrian のどちらかで通行可
  - **INT-10**: `bakedProfileIndex == edgeId`（仕様書 §4.7.5、全エッジでサンプル検査ではなく全件検査）

**完了条件 (DoD)**:

- `tsushima.odrg` で INT-1〜INT-10 全 pass
- 各テストは合成データ（小規模 `.odrg`）でも個別 pass する（リグレッション保護）
- 設計書 §7 検証チェックリスト「エッジ AABB / R-tree が整合（ランダム検査）」を本サブで充足

**工数感**: 0.5 日

---

### 4.3 サブステップ 5.3: Phase 1 RouterDb 統計比較テスト

**目的**: `.odrg` の頂点数・辺数・経緯度範囲・bake プロファイルが Phase 1 RouterDb と妥当範囲で一致することを検査。

**追加ファイル**:

- `tests/OsmDotRoute.Tests/Extractor/OdrgVsRouterDbParityTests.cs`
  - **PAR-1**: `default.routerdb` を読込、`.odrg` の bbox `(136.65,35.13)〜(136.80,35.25)` 内に絞った RouterDb の頂点数と `.odrg` 頂点数を比較
    - **許容範囲**: `.odrg` 頂点数が RouterDb 絞込頂点数の **0.7〜1.3 倍**（±30%）に収まる
    - **不一致時の動作**: テストは失敗、原因分析（道路 way フィルタ差異 / 頂点正規化差異 / OSM 元データ更新差異）を計画書 §6 オープン課題に記録
  - **PAR-2**: 同じ bbox 絞込で辺数を比較（許容範囲 ±30%）
  - **PAR-3**: `.odrg` Header bbox が RouterDb 絞込範囲を概ね包含する
  - **PAR-4**: bake プロファイル サンプリング比較（car / pedestrian）
    - `.odrg` から無作為 100 エッジを抽出
    - 各エッジの `BakedProfileEntry`（SpeedKmh、Allowed フラグ）と、元 OSM タグから Phase 1 `ProfileEvaluator` で算出した値を比較
    - **完全一致** が必須（道路 way フィルタが両者で同じ OSM way を採用していれば、プロファイル評価ロジックは同一のため）
    - 不一致が出た場合はそのエッジを記録し、`ProfileBaker` の bug として扱う

- `tests/OsmDotRoute.Tests/Extractor/Helpers/RouterDbBboxFilter.cs`
  - RouterDb の頂点を bbox で絞り込むヘルパ（既存 `RouterDb` API でできなければ追加実装）

**完了条件 (DoD)**:

- `default.routerdb` が見つからない環境では Skip（`Skip.If(!File.Exists(...))`）
- PAR-1〜PAR-4 全 pass
- 設計書 §7 検証チェックリスト「頂点数・辺数が Phase 1 RouterDb と一致または妥当な範囲」「bake プロファイルが Phase 1 `ProfileEvaluator` の結果と一致」を本サブで充足

**工数感**: 0.5 〜 1 日（RouterDb 側の bbox 絞込 API 有無で変動）

---

### 4.4 サブステップ 5.4: MapVerifier 拡張（`.odrg` オーバーレイ）

**目的**: `.odrg` を MapVerifier で地図表示し、Phase 1 RouterDb と重ねて目視確認できるようにする。

#### 4.4.1 Server 側

**追加ファイル**:

- `samples/MapVerifier/MapVerifier.Server/Services/OdrgState.cs` — `OdrgReadResult` のホルダー（`RouterState` と並列に）
- `samples/MapVerifier/MapVerifier.Server/Endpoints/OdrgEndpoints.cs`
  - `POST /api/load-odrg` — リクエスト: `{ odrgPath: string }`、レスポンス: `{ vertexCount, edgeCount, bbox, profileNames[] }`
  - `GET /api/road-network-odrg` — レスポンス: GeoJSON FeatureCollection (LineString[] 形式、各 LineString が `.odrg` の 1 エッジ)

**変更ファイル**:

- `samples/MapVerifier/MapVerifier.Server/Program.cs` — `MapOdrgEndpoints()` 追加、`OdrgState` を DI 登録

**完了条件 (Server)**:

- `POST /api/load-odrg` で `tsushima.odrg` をロードし統計を返せる
- `GET /api/road-network-odrg` で 38,004 エッジ分の GeoJSON LineString を返せる
- 既存 `/api/load` / `/api/road-network` は変更なし、副作用無し

#### 4.4.2 Web 側

**変更ファイル**:

- `samples/MapVerifier/MapVerifier.Web/src/components/LoadPanel.tsx` — `.odrg` 用ファイルブラウザ起動ボタン追加（既存 `.routerdb` ボタンと並列）
- `samples/MapVerifier/MapVerifier.Web/src/api/` — `loadOdrg()` / `fetchOdrgRoadNetwork()` 追加
- `samples/MapVerifier/MapVerifier.Web/src/App.tsx`（または地図描画コンポーネント）— `.odrg` レイヤ追加、トグル ON/OFF と色分け

**色分け方針**:

- RouterDb (青系) / `.odrg` (赤系) / 両方有効時はそれぞれ半透明
- レイヤ ON/OFF トグルで重ね/単独切替

**完了条件 (Web)**:

- `tsushima.odrg` をロードして地図上に表示できる
- RouterDb と `.odrg` を同時に重ね表示できる
- レイヤトグルが動作する
- ズレが視覚的に確認できる

**工数感**: 1 〜 1.5 日（Server 0.5 日、Web 0.5〜1 日）

---

### 4.5 サブステップ 5.5: 締め（4-opt 判断 + 設計書執筆 + v0.2.0 判断）

**目的**: 5.1〜5.4 の検証結果を踏まえて Phase 2 を確定。

**作業内容**:

#### 4.5.1 ステップ 4-opt 判断

- 5.3 PAR-1〜PAR-4 が全 pass の場合 → **Phase 3 以降に延期**（YAGNI、計画書 §3.7「迷ったら作らない」）
- 不一致が多い場合 → 別ステップで RouterDb 変換ツール `OsmDotRoute.Converter` 実装を着手
- 判断根拠を設計書 §6（オプション変換ツール章）に追記

#### 4.5.2 設計書 §7 §8 初版執筆

- **§7 Phase 2 検証と完了判定** — 計画書 §1〜§4 の検証チェックリスト全件結果を記載、v0.2.0 タグ判断根拠
- **§8 Phase 2 制約事項と Phase 3 申し送り** — Phase 1 申し送り解消状況、Phase 2 で判明した制約、Phase 3 への申し送り 8 項目（3A〜3H）

#### 4.5.3 v0.2.0 タグ判断

- ユーザー承認後、Phase 2 確定コミットに `v0.2.0` タグを付与（CLAUDE.md「バージョン番号はユーザーが管理」）
- タグメッセージにステップ 1〜3 完了、ステップ 4-opt 判断結果、ステップ 5 検証完了を記載

#### 4.5.4 メモリ更新

- `project_phase_status.md` を「Phase 2 完了」状態に更新
- 検証時の特筆点があれば `project_phase2_*.md` に追記

**完了条件 (DoD)**:

- 設計書 §7 §8 初版コミット
- 4-opt 判断記録
- ユーザー承認 → v0.2.0 タグ付与
- メモリ更新

**工数感**: 0.5 日

---

## 5. ファイル変更サマリ

### 5.1 追加ファイル

| サブ | パス | 種別 |
|---|---|---|
| 5.1 | `src/OsmDotRoute.Extractor/Pipeline/OdrgReader.cs` | 実装 |
| 5.1 | `src/OsmDotRoute.Extractor/Pipeline/OdrgReadResult.cs` | 実装 |
| 5.1 | `tests/OsmDotRoute.Tests/Extractor/OdrgReaderTests.cs` | テスト |
| 5.2 | `tests/OsmDotRoute.Tests/Extractor/OdrgIntegrityTests.cs` | テスト |
| 5.3 | `tests/OsmDotRoute.Tests/Extractor/OdrgVsRouterDbParityTests.cs` | テスト |
| 5.3 | `tests/OsmDotRoute.Tests/Extractor/Helpers/RouterDbBboxFilter.cs` | テストヘルパ |
| 5.4 | `samples/MapVerifier/MapVerifier.Server/Services/OdrgState.cs` | 実装 |
| 5.4 | `samples/MapVerifier/MapVerifier.Server/Endpoints/OdrgEndpoints.cs` | 実装 |

### 5.2 変更ファイル

| サブ | パス | 内容 |
|---|---|---|
| 5.4 | `samples/MapVerifier/MapVerifier.Server/Program.cs` | OdrgEndpoints 登録、OdrgState DI 登録 |
| 5.4 | `samples/MapVerifier/MapVerifier.Web/src/components/LoadPanel.tsx` | `.odrg` ロード UI 追加 |
| 5.4 | `samples/MapVerifier/MapVerifier.Web/src/api/`（複数） | OdrgAPI 追加 |
| 5.4 | `samples/MapVerifier/MapVerifier.Web/src/App.tsx` 他 | レイヤ追加、トグル、色分け |
| 5.5 | `Documents/phase2_design.md` | §7 §8 初版執筆 |
| 5.5 | `Documents/phase2_implementation_plan.md` | §6 ステップ 5 完了マーク、§10 4-opt 判断追記 |

### 5.3 削除ファイル

なし。

---

## 6. 受け入れテスト対応表（設計書 §7 → サブステップ）

| 設計書 §7 検証項目 | 対応サブ | 対応テスト/手段 |
|---|---|---|
| Extractor が津島市 `.osm.pbf` から `.odrg` を生成できる | ✅ 3.9 完了 | 既存 `ExtractPipelineTests` |
| 頂点数・辺数が Phase 1 RouterDb と一致または妥当な範囲 | 5.3 | `OdrgVsRouterDbParityTests.PAR-1, PAR-2, PAR-3` |
| エッジ AABB / R-tree が整合（ランダム検査） | 5.2 | `OdrgIntegrityTests.INT-3〜INT-8` |
| エッジフラグが OSM タグから正しく bake されている | 5.2 + 5.4 | `OdrgIntegrityTests.INT-9` + MapVerifier 目視確認 |
| bake プロファイルが Phase 1 `ProfileEvaluator` の結果と一致 | 5.3 + 5.4 | `OdrgVsRouterDbParityTests.PAR-4` + MapVerifier 目視確認 |

---

## 7. オープン課題（実装中にユーザー確認が必要な事項）

| ID | 課題 | 確認タイミング |
|---|---|---|
| Q1 | RouterDb 側に bbox 絞込 API が無い場合、ヘルパ実装で対応してよいか | サブ 5.3 着手時 |
| Q2 | PAR-1/PAR-2 の許容範囲 ±30% は妥当か（より厳しく/緩く） | サブ 5.3 着手時に判断、不一致が出てから再判断も可 |
| Q3 | MapVerifier の `.odrg` 色分け（赤系）と既存 RouterDb 色（青系）の組合せで視認性に問題無いか | サブ 5.4 動作確認時 |
| Q4 | `.odrg` 単独表示モードを追加するか（RouterDb 無しでも見られる） | サブ 5.4 設計時 |

---

## 8. 工数感（粗見積もり）

| サブ | 内容 | 想定工数 |
|---|---|---|
| 5.1 | OdrgReader 実装 | 0.5 〜 1 日 |
| 5.2 | 構造整合テスト | 0.5 日 |
| 5.3 | RouterDb 突合テスト | 0.5 〜 1 日 |
| 5.4 | MapVerifier 拡張（Server + Web） | 1 〜 1.5 日 |
| 5.5 | 締め（4-opt 判断 + 設計書 + v0.2.0） | 0.5 日 |
| **合計** | | **3 〜 4.5 日** |

計画書 §7 では「ステップ 5 = 1〜2 日」想定だったが、MapVerifier 拡張と整合テスト分の上振れあり。

---

## 9. リスクと対処

| # | リスク | 影響 | 対処方針 |
|---|---|---|---|
| L1 | RouterDb と `.odrg` で頂点/辺数が大幅乖離 | サブ 5.3 不合格 → 4-opt 実施判断に振れる | 計画書 §8 R3 のとおり、許容範囲超過時は道路 way フィルタ・頂点正規化を再調査。原因が OSM データ更新差なら許容判断 |
| L2 | bake プロファイル サンプリング比較で不一致 | サブ 5.3 PAR-4 不合格 | `ProfileBaker` の bug 修正、再 bake、再比較。Phase 1 `ProfileEvaluator` の流用ロジックを再点検 |
| L3 | MapVerifier の `.odrg` 描画で 38k エッジの GeoJSON が重い | UI 体感低下 | サブ 5.4 で確認、必要に応じて単純化（線分簡略化、ズームレベル別 LOD） |
| L4 | `OdrgReader` が `OdrgWriter` の単純逆操作にならず実装複雑化 | サブ 5.1 工数超過 | フォーマットの非対称性（Shape Buffer の offset 計算等）を最小化、`OdrgFormat.cs` の定数で完結させる |
| L5 | Phase 3 NativeRoadGraph 設計が始まったときに `OdrgReader` の API が制約になる | Phase 3 影響 | `OdrgReader` は明示的に検証専用と位置づけ（`internal` 推奨）。Phase 3 設計は完全に独立 |

---

## 10. 次のアクション

- [ ] 本計画書 v0.1 のユーザーレビュー
- [ ] ユーザー合意後、サブステップ 5.1（OdrgReader 実装）着手
- [ ] サブ 5.1 完了でユーザー報告 → 承認 → サブ 5.2 着手
- [ ] 以下同様に 5.2 → 5.3 → 5.4 → 5.5 と進行

---

## 11. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 | 2026-05-25 | 初版。ユーザー合意済（サブ分割 5.1〜5.5、OdrgReader は Extractor 内 eager-parse、MapVerifier は RouterDb と重ね表示）を反映 | Claude (Opus 4.7) |
