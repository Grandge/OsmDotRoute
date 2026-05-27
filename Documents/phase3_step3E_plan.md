# Phase 3 ステップ 3E: ベンチマーク再実施（津島市 C0〜C4）計画書

**ステータス**: ドラフト v0.1（着手前、2026-05-27）
**対応ステップ**: Phase 3 ステップ 3E（[Phase 3 実装計画書 §3.5 / §6 / §9](phase3_implementation_plan.md)）
**対応要件**: REQ-NFR-001（経路計算性能維持）、REQ-NFR-002（制約 100 件下の劣化率 ≦ 1.5x）、REQ-NFR-003（経路 1 本あたりアロケート削減）
**関連文書**:

- [Phase 3 実装計画書 §3.5 / §6 / §9](phase3_implementation_plan.md)（本ステップ位置付け・性能基準値表）
- [Phase 3 設計書 §7 ベンチマーク再実施（津島市）](phase3_design.md)（本ステップで肉付け対象、現状「未記述」プレースホルダ）
- [Phase 1 ベンチマーク結果](phase1_benchmark_results.md)（Phase 1 基準値、本ステップで Phase 3 値と並べて比較）
- [Phase 3 ステップ 3B 計画書 v0.7](phase3_step3B_plan.md)（3B 効果の `--job short` 桁オーダー確認済み）
- [Phase 3 ステップ 3C 計画書 v0.2](phase3_step3C_plan.md)（Itinero 完全撤去済、Phase 1 ベンチ環境は再現不可）
- [Phase 3 ステップ 3D 計画書 v0.2](phase3_step3D_plan.md)（Bicycle / Truck プロファイル同梱済）

---

## 1. 目的とゴール

**目的**: Phase 3 ステップ 3A（NativeRoadGraph）・3B（動的制約ホットパス eager bake）・3C（Itinero 完全撤去 + `Route.Shape` ReadOnlyMemory 化）・3D（Bicycle / Truck プロファイル）の累積効果を、Phase 1 ベンチマーク基準値（[`phase1_benchmark_results.md`](phase1_benchmark_results.md)）と並べて定量検証する。Phase 1 §18.3（制約 100 件下劣化率 1.43x）と §18.4（経路 1 本あたり 77 MB アロケート）の解消を本番統計値（BenchmarkDotNet 通常 job、iteration 10 以上）で確認し、要件 REQ-NFR-001〜003 の Phase 3 達成を文書化する。

**Done 判定**:

1. C0〜C4 の 5 シナリオが BenchmarkDotNet **通常 job**（iteration 10 以上、warmup 3〜5）で計測される
2. シナリオ定義（[Phase 3 計画書 §3.5](phase3_implementation_plan.md) + 本計画書 §2.2 ユーザー判断確定で明確化）:
   - **C0**: 津島市 `.odrg` + Car + 制約なし、100 ペア（既存 [RouteCalculationBenchmark](../tests/OsmDotRoute.Benchmarks/Benchmarks/RouteCalculationBenchmark.cs)）
   - **C1**: 津島市 `.odrg` + Car + **mixed-100**（block 50 + difficulty 50）、100 ペア（Phase 1 C3 相当、§2.2 Q1 = A 確定）
   - **C2**: 津島市 `.odrg` + **Bicycle** + mixed-100、100 ペア（§2.2 Q2 = A、既存 Car ペア流用 + 失敗率記録）
   - **C3**: C0〜C2 の `MemoryDiagnoser` 出力（経路 1 本あたり Allocated）を Phase 1 §18.4 = 77 MB と並べた **独立シナリオではなくサブセクション**
   - **C4**: `RestrictedAreaService` 単独、AttachGraph 後の **1 op = AddBlockArea + Remove(id) 1 サイクル**（§2.2 Q3 = A 確定、Phase 1 未測定の新規シナリオ）
3. 結果文書 [`Documents/phase3_benchmark_results.md`](phase3_benchmark_results.md) v0.1（新規）が Phase 1 比較表構成（Phase 1 値 / Phase 3 値 / 目標 / 達成）で生成される
4. Phase 3 計画書 [§9 性能基準値表](phase3_implementation_plan.md#9-性能基準値phase-1--phase-3-比較) に実測値を反映し、目標達成可否を明示する
5. 設計書 [`phase3_design.md` §7](phase3_design.md) が肉付けされる（§7.1〜§7.6 全 6 サブセクション、3A/3B/3C/3D 完了時と同方針）
6. **Phase 1 既存 526 件 + Phase 3 累計 146 件 = 672 件 pass を本ステップで維持**（ベンチマークプロジェクトのみの修正、本体テストは変更なし）
7. **本ステップで `tsushima.odrg` と `route-pairs.json` / `restrictions-*.json` の絶対パスは現状維持**（[BenchmarkAssets.cs](../tests/OsmDotRoute.Benchmarks/BenchmarkAssets.cs) の直書き、3C.4 で確定済の方針継続）

**REQ-NFR-001〜003 達成判定**:

- REQ-NFR-001（≤ 100 ms）: C0/C1/C2 全ペア平均が 100 ms 内
- REQ-NFR-002（制約 100 件下劣化率 ≤ 1.5x）: C1 / C0 ≤ 1.5x、目標 ≤ 1.1x（Phase 3 計画書 §9）
- REQ-NFR-003（経路 1 本あたりアロケート削減）: C3 サブセクションで Phase 1 = 77 MB から Phase 3 = ≦ 5 MB（Phase 1 の 1/15）目標、最低ライン ≦ 25 MB（Phase 1 R5 リスク対処）

---

## 2. 前提と現状

### 2.1 既存資産（3E 着手時点、2026-05-27）

- Phase 3 ステップ 3A 全体完了（commit `bf8a8a4`、NativeRoadGraph / NativeRoadSnapper、+69 件）
- Phase 3 ステップ 3B 全体完了（commit `cd9f435`、RestrictedAreaEdgeCache + AttachGraph、+29 件、3B 効果 `--job short` で C3 Mean -93.5% / Alloc -73.5% 確認済）
- Phase 3 ステップ 3D 全体完了（commit `1e4a628`、Bicycle / Truck 同梱、+60 件）
- Phase 3 ステップ 3C 全体完了（commit `c311c34`、Itinero 完全撤去 / `Route.Shape` ReadOnlyMemory 化 / DI 書換、-12 件、Itinero NuGet 依存ゼロ）
- 既存ベンチコード（3C.4 後、Itinero 撤去済の Native 系統のみ）:
  - [tests/OsmDotRoute.Benchmarks/Benchmarks/RouteCalculationBenchmark.cs](../tests/OsmDotRoute.Benchmarks/Benchmarks/RouteCalculationBenchmark.cs)（Native + Car + 100 ペア、制約なし）
  - [tests/OsmDotRoute.Benchmarks/Benchmarks/SnapBenchmark.cs](../tests/OsmDotRoute.Benchmarks/Benchmarks/SnapBenchmark.cs)（Snap 単独）
  - [tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs](../tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs)（`[Params]` で Mode (Native-Detached/Attached) × Case (C0/C3) 切替、C1/C2/C4 ビルダは内部に残置）
  - [tests/OsmDotRoute.Benchmarks/BenchmarkAssets.cs](../tests/OsmDotRoute.Benchmarks/BenchmarkAssets.cs)（`tsushima.odrg` 絶対パス直書き、`LoadNativeRouterDb()` 共通化）
  - [tests/OsmDotRoute.Benchmarks/Generators/TestDataInitializer.cs](../tests/OsmDotRoute.Benchmarks/Generators/TestDataInitializer.cs)（`--generate-data` でシード固定再生成）
- 既存 TestData（決定論的、シード固定）:
  - `route-pairs.json`（100 ペア、seed=20260520、Car スナップ成功条件）
  - `restrictions-mixed-100.json`（block 50 + difficulty (flood/debris/narrow/damaged/closed 各 10)、seed=20260521）
  - `restrictions-block-100.json`（block 100、seed=20260522）
- 3B 効果実測値（[設計書 §4.5.2](phase3_design.md)、`--job short` iter=3、本ステップで本番統計値に更新予定）:
  - Native-Attached C0: 3.030 ms / 1.13 MB
  - Native-Attached C3 (mixed-100): 1.177 ms / 0.74 MB
- 制約 API（[src/OsmDotRoute/Restrictions/RestrictedAreaService.cs](../src/OsmDotRoute/Restrictions/RestrictedAreaService.cs)）: `AddBlockArea(polygon, tag) → RestrictedAreaId` / `Remove(id)` / `RemoveByTag(tag)` / `ClearAll()` 完備、`AttachGraph(graph)` で eager bake キャッシュ初期化
- 親プロジェクト Itinero ベンチ環境は **3C.4 で完全撤去済**、本ステップでの再現は不可。Phase 1 数値は [`phase1_benchmark_results.md`](phase1_benchmark_results.md) を固定参照

### 2.2 ユーザー判断確定（本ステップ着手前、2026-05-27）

- **Q1 = (A) C1 制約パターン = mixed-100**（Phase 1 C3 相当、推奨）
  - block 50 + difficulty (flood/debris/narrow/damaged/closed 各 10) で散在的に制約が経路上に乗るパターン
  - 3B 効果（eager bake キャッシュ）のホットパス検証本命
  - Phase 1 で C0 比 1.43x = 51 ms と測定された本命ケース
  - Phase 3 計画書 §9 性能基準値表（「制約 100 件下（C1）51 ms（C0 比 1.43x）」）と整合
  - **block-only-100 は本ステップでは扱わない**（短絡効果の単独計測は Phase 1 で経路発見失敗の疑いがあり、Phase 3 でも津島市 11km × 11km での完全分断問題は残るため、3G 都道府県単位ベンチで再評価）
- **Q2 = (A) Bicycle 100 ペア = 既存 Car ペアを流用 + 失敗率を成果物に記録**（推奨）
  - 既存 route-pairs.json（Car でスナップ成功 100 ペア、seed=20260520）を Bicycle プロファイルでそのまま `Calculate` する
  - 起終点スナップ失敗（Bicycle で通行不可道路への最近傍）/ 経路発見失敗（Bicycle で連結性破綻）した分は `null` として混入し、Allocated は計上、Mean は影響を受ける
  - 津島市は motorway なし・trunk 数本のみのため失敗率は低い見込み（事前推測 < 5%）
  - 成功率は [phase3_benchmark_results.md](phase3_benchmark_results.md) §C2 で明示記録
  - Car と同一ペアなので **C1 と C2 で「Car vs Bicycle の速度・アロケート差」を直接比較可能** というメリットあり
  - 別 seed 生成案は採用しない（Car と Bicycle のペアが違うと直接比較不可、レポート構成が複雑化）
- **Q3 = (A) C4 単位 = 1 op = AddBlockArea + Remove(id) 1 サイクル**（推奨）
  - `RestrictedAreaService` に `AttachGraph` 済の状態で、polygon プール（既存 mixed-100 の block 系 50 件をリサイクル）から順次 `AddBlockArea(polygon)` → 返却 id で `Remove(id)` → 次の polygon
  - 1 サイクル = 1 op として ops/s と Allocated を計測
  - **eager bake コスト**（`QueryEdgesByAabb` + エッジ厳密判定 + HashSet 追加）と **cache RemoveArea コスト** の和を一括で見る最も素直な解釈
  - Phase 3 計画書 §3.5 「制約 add/remove スループット（`RestrictedAreaService` 単独、Phase 1 では未測定）」の素直な実装
  - Add のみ / Remove のみの分離測定は本ステップでは扱わない（必要なら別途 Phase 4+ で追加）
  - RemoveByTag / ClearAll の網羅も本ステップでは扱わない（3F 親プロ統合で実用性検証時に追加判断）
- **Q4 = (A) 4 サブ分割**（推奨）
  - 3E.1 既存 RouteWithConstraintsBenchmark を C0/C1/C2 ParamCase 化（mixed-100 + Car/Bicycle 切替 + MemoryDiagnoser 強化）
  - 3E.2 C4 制約 add/remove スループットベンチ新規実装
  - 3E.3 本番 job (iteration 10 以上) で全シナリオ実測 → [phase3_benchmark_results.md](phase3_benchmark_results.md) v0.1 生成
  - 3E.4 設計書 §7 (3E) 肉付け + 計画書 §9 性能基準値表に実測値反映 + 3E 完了総括

### 2.3 設計上の歯止め

- **公開 API 不変**: 本ステップはベンチマークプロジェクトのみの改修。`OsmDotRoute` コア / `OsmDotRoute.Extensions.DependencyInjection` / `OsmDotRoute.Pbf` / `OsmDotRoute.Extractor` は一切変更しない
- **既存テスト 672 件 pass を維持**: 本体に手を入れないため自然に維持。各サブステップで `dotnet test` を流して確認
- **TestData 再生成は実施しない**: 既存 `route-pairs.json` / `restrictions-mixed-100.json` / `restrictions-block-100.json` をシード固定の決定論的データとしてそのまま流用（3C.2 で `.odrg` ベース移行済）
- **Phase 1 環境再現は試みない**: 3C.4 で Itinero 完全撤去済、Phase 1 数値は [phase1_benchmark_results.md](phase1_benchmark_results.md) を固定参照（同一 RouterDb での再測定は不可能）
- **ベンチマーク 1 回あたりの実行時間上限**: iteration 10 以上にすると C1/C2 各シナリオで 30〜60 秒程度想定。全 5 シナリオ × 2 モード（Native-Detached / Native-Attached、3B 効果検証）= 10 ケース で 5〜10 分程度の見込み。3E.3 着手時に試走してパラメータ調整

---

## 3. アーキテクチャ概要

### 3.1 ベンチマーク全体構成（3E 完了時）

```text
tests/OsmDotRoute.Benchmarks/
├ BenchmarkAssets.cs                       (変更なし、tsushima.odrg + LoadNativeRouterDb)
├ Program.cs                                (変更なし、--generate-data + BenchmarkSwitcher)
├ TestData/                                 (変更なし、シード固定)
│  ├ route-pairs.json                       (100 ペア、Car スナップ成功)
│  ├ restrictions-mixed-100.json
│  └ restrictions-block-100.json
├ Generators/                               (変更なし)
├ Benchmarks/
│  ├ RouteCalculationBenchmark.cs           (変更なし、C0 単独計測の補助)
│  ├ SnapBenchmark.cs                       (変更なし、Snap 単独計測の補助)
│  ├ RouteWithConstraintsBenchmark.cs       (3E.1 で拡張: Case=C0/C1/C2 + Profile=Car/Bicycle)
│  └ ★ RestrictionThroughputBenchmark.cs  (3E.2 新規、C4)
```

### 3.2 RouteWithConstraintsBenchmark の拡張（3E.1）

**既存** (3C.4 後):

```csharp
[Params("Native-Detached", "Native-Attached")] public string Mode;
[Params("C0", "C3")] public string Case;   // C3 = mixed-100 + Car
```

**3E.1 後**:

```csharp
[Params("Native-Detached", "Native-Attached")] public string Mode;
[Params("C0", "C1", "C2")] public string Case;
// C0 = 制約なし + Car
// C1 = mixed-100 + Car  (旧 C3 にあたる、Phase 3 §3.5 で C1 にリナンバ)
// C2 = mixed-100 + Bicycle
```

**変更ポイント**:

- `Params` の `Case` 値を `"C0"`, `"C1"`, `"C2"` に置換（Phase 3 計画書 §3.5 のシナリオ命名に揃える）
- `Setup()` の `Case switch` で C1 = `BuildMixed(100)` + Car、C2 = `BuildMixed(100)` + Bicycle に分岐
- 既存の C3/C4 ビルダコード（`BuildMixed` / `BuildBlockOnly`）は内部に残置（再利用）
- `[MemoryDiagnoser]` 既設で Allocated は自動取得 → C3（経路 1 本あたりアロケート量）は独立シナリオではなく **C0/C1/C2 の Allocated 列を Phase 1 §18.4 = 77 MB と並べたサブセクション** として results.md に記載
- Bicycle ペア失敗率測定: 3E.1 着手時に診断ユーティリティを TestDataInitializer or 別途 1 回限りのコンソール出力で実装し、`results.md §C2` に Bicycle 100 ペア中の成功 / null 件数を記録（ベンチマーク本体には組み込まない、診断専用）

### 3.3 RestrictionThroughputBenchmark の新規実装（3E.2、C4）

```csharp
[MemoryDiagnoser]
public class RestrictionThroughputBenchmark
{
    private OsmDotRoute.RouterDb _routerDb = default!;
    private NativeRoadGraph _graph = default!;
    private RestrictedAreaService _service = default!;
    private GeoPolygon[] _polygonPool = default!;
    private int _index;

    [GlobalSetup]
    public void Setup()
    {
        (_routerDb, _graph) = BenchmarkAssets.LoadNativeRouterDb();
        _service = new RestrictedAreaService();
        // AttachGraph は Router 経由 or 直接 internal call
        // → AttachGraph は internal なので、Router(routerDb, service) コンストラクタ経由で自動 attach
        _ = new Router(_routerDb, _service);
        var file = TestDataInitializer.LoadMixedRestrictions();
        var polygons = RestrictionGenerator.ToPolygons(file)
            .Where(p => p.Entry.Type == "block")
            .Select(p => p.Polygon)
            .ToArray();
        _polygonPool = polygons;
        _index = 0;
    }

    [GlobalCleanup]
    public void Cleanup() => _graph.Dispose();

    [Benchmark(Description = "AddBlockArea + Remove(id) 1 サイクル")]
    public void AddRemoveCycle()
    {
        var polygon = _polygonPool[_index];
        _index = (_index + 1) % _polygonPool.Length;
        var id = _service.AddBlockArea(polygon);
        _service.Remove(id);
    }
}
```

**ポイント**:

- `_polygonPool` は mixed-100 の block 系 50 件をリサイクル（順繰り）
- `AttachGraph` は `Router` コンストラクタ内で `RestrictedAreaService.AttachGraph(_routerDb.Graph)` が自動呼出（[Router.cs](../src/OsmDotRoute/Router.cs) `autoAttachGraph: true` デフォルト動作）
- 1 ベンチ呼出 = 1 op = AddBlockArea + Remove(id) 1 サイクル（Q3 = A 確定通り）
- eager bake コスト（QueryEdgesByAabb + EdgeIntersectsShape + HashSet 追加 = O(候補エッジ数)）と cache RemoveArea コスト（HashSet 削除 = O(交差エッジ数)）の合計
- `[MemoryDiagnoser]` で 1 サイクルあたり Allocated を取得（HashSet 操作のみで小規模を期待）

### 3.4 phase3_benchmark_results.md v0.1 構成案（3E.3）

```markdown
# Phase 3 ベンチマーク結果

**ステータス**: v0.1 (2026-05-27 ステップ 3E.3 計測完了)
**対応計画書**: [Phase 3 ステップ 3E 計画書](phase3_step3E_plan.md)
**対応要件**: REQ-NFR-001〜003

## 1. 計測環境
（Windows 11、CPU、.NET ランタイム、BenchmarkDotNet、tsushima.odrg 規模）

## 2. シナリオ (Phase 3 計画書 §3.5)
（C0〜C4 の定義表）

## 3. 経路計算性能 (C0/C1/C2)

| Case | Mode | Profile | Mean | Error | StdDev | Allocated |
|---|---|---|---|---|---|---|
| C0 | Native-Detached | Car | … | … | … | … |
| C0 | Native-Attached | Car | … | … | … | … |
| C1 | Native-Detached | Car | … | … | … | … |
| C1 | Native-Attached | Car | … | … | … | … |
| C2 | Native-Detached | Bicycle | … | … | … | … |
| C2 | Native-Attached | Bicycle | … | … | … | … |

### 3.1 Phase 1 比較 (Phase 1 = default.routerdb 43k 頂点、Phase 3 = tsushima.odrg 27k 頂点、規模差注意)
（Phase 1 値 / Phase 3 値 / 目標 / 達成 の 4 列比較表）

### 3.2 C2 Bicycle スナップ失敗率
（既存 Car ペア 100 件中、Bicycle で Calculate 失敗した件数を記録）

## 4. 経路 1 本あたりアロケート量 (C3 サブセクション)

| Case | Profile | Allocated (Phase 3) | Allocated (Phase 1) | 削減率 |
|---|---|---|---|---|
| C0 | Car | … | 77 MB | … |
| C1 | Car | … | 57 MB | … |
| C2 | Bicycle | … | (Phase 1 未測定) | — |

## 5. 制約 add/remove スループット (C4、Phase 3 新規)

| Operation | Mean | StdDev | Allocated | ops/s |
|---|---|---|---|---|
| AddBlockArea + Remove(id) 1 cycle | … | … | … | … |

## 6. 判定サマリ
（REQ-NFR-001/002/003 の達成可否表）

## 7. Phase 3 計画書 §9 性能基準値表との照合
（計画書 §9 表の各行を実測値で埋めた更新版）

## 8. 改訂履歴
| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 | 2026-05-27 | 初版 (3E.3 計測完了) | Claude (Opus 4.7) |
```

---

## 4. サブステップ詳細

### 4.1 3E.1: RouteWithConstraintsBenchmark 拡張（C0/C1/C2 ParamCase 化 + Bicycle 切替）

**目的**: 既存 RouteWithConstraintsBenchmark の `[Params]` を Phase 3 計画書 §3.5 のシナリオ命名（C0/C1/C2）に揃え、Bicycle プロファイル切替を追加する。本体テストには触れず、ベンチプロジェクトのみ修正。

**作業内容**:

1. `RouteWithConstraintsBenchmark.Case` の `[Params]` を `"C0", "C1", "C2"` に変更
2. `Setup()` 内の `Case switch` を以下に置換:
   - C0: `null` (制約なし) + Car
   - C1: `BuildMixed(100)` + Car（既存 C3 のリネーム相当）
   - C2: `BuildMixed(100)` + Bicycle
3. `[Params("Car", "Bicycle")] public string Profile;` 案も検討するが、組み合わせ爆発（2 Mode × 3 Case × 2 Profile = 12 ケース）を避けるため、**Case の値に Profile を埋め込む**設計に統一（C2 = mixed-100 + Bicycle 固定）
4. C3/C4 旧ビルダ（`BuildBlockOnly` 等）は削除せず内部に残置（再利用可能性）
5. `dotnet build tests/OsmDotRoute.Benchmarks -c Release` で warning ゼロ確認
6. `dotnet test` で 672 件 pass 再確認（ベンチプロジェクトは Tests に含まれないので自然に維持）
7. Bicycle 失敗率診断ツール: `Program.cs` に `--bicycle-snap-probe` サブコマンドを 1 つ追加し、`route-pairs.json` 100 ペアを Bicycle で `Calculate` して `null` / 成功件数を出力（ベンチマーク本体には組み込まない、診断専用）

**Done 基準**:

- `dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --list flat` で C0/C1/C2 × Native-Detached/Native-Attached の 6 ケースが列挙される
- `dotnet test` 672 件 pass 維持
- `--bicycle-snap-probe` で Bicycle 100 ペアの成功率が出力される（結果は 3E.3 で results.md に記録）

**完了 commit メッセージ案**:

> feat: Phase 3 ステップ 3E.1 RouteWithConstraintsBenchmark Case を C0/C1/C2 化 + Bicycle 切替 + --bicycle-snap-probe 診断 (672 件 pass 維持、ベンチプロジェクト改修のみ)

### 4.2 3E.2: RestrictionThroughputBenchmark 新規実装（C4）

**目的**: 制約 add/remove スループットを `RestrictedAreaService` 単独で計測する新規ベンチクラスを追加する。1 op = AddBlockArea + Remove(id) 1 サイクル（Q3 = A 確定）。

**作業内容**:

1. `tests/OsmDotRoute.Benchmarks/Benchmarks/RestrictionThroughputBenchmark.cs` を新規作成（§3.3 のコード骨子を実装）
2. `_polygonPool` 構築: `RestrictionGenerator.ToPolygons(file).Where(Type == "block").Select(Polygon).ToArray()`（mixed-100 の block 系 50 件、polygon 構造は再利用可能なので新規 polygon 作成コストは含まれない）
3. AttachGraph は `Router(routerDb, service)` コンストラクタ経由で自動実行（既存 internal 規約利用、追加 API なし）
4. `[MemoryDiagnoser]` 有効化、ops/s と Allocated/op を取得
5. `dotnet build` で warning ゼロ確認
6. `dotnet test` 672 件 pass 維持
7. `--list flat` で `RestrictionThroughputBenchmark.AddRemoveCycle` が列挙されることを確認

**Done 基準**:

- 新規ベンチクラスが BenchmarkDotNet に列挙される
- `dotnet run … --filter "*RestrictionThroughputBenchmark*" --job short` で `--job short` 実行可能（実際の本番計測は 3E.3 で実施）
- `dotnet test` 672 件 pass 維持

**完了 commit メッセージ案**:

> feat: Phase 3 ステップ 3E.2 RestrictionThroughputBenchmark 新規実装 (C4: AddBlockArea + Remove(id) 1 サイクル op、MemoryDiagnoser 有効、672 件 pass 維持)

### 4.3 3E.3: 本番 job 全シナリオ実測 + phase3_benchmark_results.md v0.1 生成

**目的**: 通常 job（iteration 10 以上、warmup 3〜5）で C0〜C4 全シナリオを実測し、Phase 1 比較表を含む結果文書を生成する。

**作業内容**:

1. `dotnet build tests/OsmDotRoute.Benchmarks -c Release` でリリースビルド
2. `--bicycle-snap-probe` を 1 回実行し、Bicycle 失敗率を取得（コンソール出力）
3. 本番 job ベンチマーク実行:
   - `dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --filter "*RouteWithConstraintsBenchmark*"`（C0/C1/C2 × 2 Mode = 6 ケース）
   - `dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --filter "*RouteCalculationBenchmark*"`（C0 独立計測、補助）
   - `dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --filter "*SnapBenchmark*"`（Snap 単独、補助）
   - `dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --filter "*RestrictionThroughputBenchmark*"`（C4）
4. BenchmarkDotNet 出力（`BenchmarkDotNet.Artifacts/results/`）から実測値を抽出
5. `Documents/phase3_benchmark_results.md` v0.1 を §3.4 構成案で生成
6. **Phase 1 比較表は固定参照** ([phase1_benchmark_results.md](phase1_benchmark_results.md) の数値を引用、RouterDb 規模差を脚注で明示)
7. C2 Bicycle 失敗率を §C2 サブセクションに記載
8. `dotnet test` 672 件 pass 維持確認

**Done 基準**:

- `phase3_benchmark_results.md` v0.1 が生成される
- 全シナリオの Mean / Error / StdDev / Allocated が記載される
- Phase 1 比較表が完成し、目標達成可否が明示される
- BenchmarkDotNet 出力ファイル (`BenchmarkDotNet.Artifacts/`) は git ignore（既存 `.gitignore` を確認、未指定なら追加判断）
- `dotnet test` 672 件 pass 維持

**完了 commit メッセージ案**:

> feat: Phase 3 ステップ 3E.3 本番 job ベンチマーク全シナリオ実測 + phase3_benchmark_results.md v0.1 (REQ-NFR-001〜003 達成確認、Phase 1 比較表、Bicycle 失敗率 X/100、672 件 pass 維持)

### 4.4 3E.4: 設計書 §7 (3E) 肉付け + 計画書 §9 性能基準値表更新 + 3E 完了総括

**目的**: 設計書 [`phase3_design.md` §7](phase3_design.md) を 3A/3B/3C/3D 完了時と同じ 6 サブセクション構成で肉付けし、Phase 3 計画書 §9 性能基準値表に実測値を反映する。

**作業内容**:

1. `Documents/phase3_design.md` §7.1〜§7.6 を肉付け:
   - §7.1 意図: REQ-NFR-001〜003 達成確認、Phase 1 §18.3 / §18.4 解消、Phase 3 性能基準値の確定
   - §7.2 採用設計: ベンチマーク構成（C0〜C4、5 シナリオ、Native-Detached/Attached 2 モード）、TestData 流用方針、Phase 1 環境再現を試みない理由
   - §7.3 検証結果: phase3_benchmark_results.md v0.1 へのリンクと主要数値の要約（Mean / Allocated / Phase 1 比較・目標達成可否）
   - §7.4 設計判断の根拠: Q1〜Q4 ユーザー判断確定の理由
   - §7.5 トレードオフ・制約: RouterDb 規模差で Phase 1 直接比較不可・Bicycle 失敗率の混入による Mean ぶれ・C4 単一サイクル測定の限界・iteration 数増による実行時間
   - §7.6 実装メモ: 主要 commit、暗黙の前提（BenchmarkDotNet 子プロセスパス問題、AttachGraph 自動化、Bicycle ペア共有のメリット）、Phase 4+ 申し送り（Add/Remove 分離測定、RemoveByTag/ClearAll 測定、都道府県単位ベンチへの引継ぎ）
2. `Documents/phase3_implementation_plan.md` §9 性能基準値表を実測値で更新（既存表構造維持、各行に Phase 3 実測値を追記）
3. `Documents/phase3_step3E_plan.md` v0.2 に bump（§5 完了状況に 3E.1〜3E.4 実装結果を追記、改訂履歴に v0.1 → v0.2 行を追加）
4. メモリ [project_phase_status.md](C:/Users/ssys0/.claude/projects/d--workspace-DotRoute/memory/project_phase_status.md) を「Phase 3 ステップ 3E 全体完了」に更新
5. `dotnet test` 672 件 pass 最終確認

**Done 基準**:

- 設計書 §7 全 6 サブセクションが肉付けされる
- 計画書 §9 性能基準値表に Phase 3 実測値が反映される
- 計画書 v0.2 + メモリ更新が commit される
- `dotnet test` 672 件 pass 維持

**完了 commit メッセージ案**:

> docs: Phase 3 ステップ 3E.4 + 3E 完了 (設計書 §7 全 6 サブセクション肉付け + 計画書 v0.2 + §9 性能基準値表実測値反映 + REQ-NFR-001〜003 達成総括、672 件 pass 維持)

---

## 5. 完了状況

| サブステップ | 状態 | 完了 commit | 主要成果 |
| --- | --- | --- | --- |
| 3E.1 | 未着手 | — | RouteWithConstraintsBenchmark Case を C0/C1/C2 化 + Bicycle 切替 + --bicycle-snap-probe |
| 3E.2 | 未着手 | — | RestrictionThroughputBenchmark 新規（C4） |
| 3E.3 | 未着手 | — | 本番 job 全シナリオ実測 + phase3_benchmark_results.md v0.1 |
| 3E.4 | 未着手 | — | 設計書 §7 肉付け + 計画書 §9 表更新 + 3E 完了総括 |

---

## 6. リスクと対処

| # | リスク | 影響 | 対処方針 |
| --- | --- | --- | --- |
| 3E-R1 | Bicycle 100 ペアの失敗率が想定（< 5%）を大幅超過 | C2 Mean が Bicycle 経路特性ではなく `null` 比率の影響で歪む | 3E.1 `--bicycle-snap-probe` を先に実施。失敗率 > 20% の場合は Bicycle 専用 route-pairs-bicycle.json を別 seed で生成する判断を取る（計画書 v0.x bump、ユーザー判断 Q2 の見直し） |
| 3E-R2 | 本番 job (iteration 10+) で実行時間が想定（5〜10 分）を大幅超過 | 計測ラウンドが長引く | 3E.3 着手前に `--job short` で 1 シナリオ試走 → iteration / warmup パラメータを `[SimpleJob]` 属性で個別指定（C1/C2 のみ短縮等） |
| 3E-R3 | Phase 1 = 77 MB から Phase 3 = 5 MB 目標（1/15）が未達（Phase 3 計画書 §9 性能基準値） | REQ-NFR-003 計画書目標未達 | Phase 1 計画書 R5 リスク表「届かなければ Phase 1 の 1/3 = 25 MB を最低ライン」を踏襲。3B 効果実測値（0.74 MB）から推測すると目標達成見込みは高い |
| 3E-R4 | C2 Bicycle で Phase 3 計画書 §9 性能基準値表に対応行がない（C0/C1/C3 のみ） | 計画書 §9 表の更新方針が不明確 | 3E.4 で計画書 §9 表に Bicycle 行（C2）を新規追加する（既存 Car 行と並列）。表構造維持のため列追加ではなく行追加で対応 |
| 3E-R5 | BenchmarkDotNet 出力ディレクトリ (`BenchmarkDotNet.Artifacts/`) が git に混入 | コミット汚染 | 3E.3 着手前に `.gitignore` を確認。未指定なら `BenchmarkDotNet.Artifacts/` 行を追加 |
| 3E-R6 | C4 1 op = Add + Remove の Allocated が想定外に大きい（HashSet 操作だけのはずが、毎回 polygon コピー等が混入） | C4 数値の解釈が困難 | 3E.3 で Allocated 詳細を `MemoryDiagnoser` の Gen0/Gen1/Gen2 ヒート別に確認。想定外なら 3E.4 設計書 §7.6 で原因を記録（バグ修正は本ステップ外、3F 等で対処判断） |
| 3E-R7 | RouterDb 規模差（Phase 1 = 43k vs Phase 3 = 27k 頂点）で直接比較が公平でない | results.md の説得力低下 | results.md と設計書 §7.5 で規模差を脚注で明示。比率比較（C0 vs C1 の倍率、Allocated の桁オーダー）は規模差の影響を受けにくいため、相対値で目標達成を主張 |

---

## 7. テスト設計サマリ

本ステップは **ベンチマークプロジェクトのみの改修**のため、本体 testsuite に追加テストを書かない。`dotnet test` 672 件 pass を維持し続ける。

### 7.1 各サブステップでの pass 件数遷移

| サブステップ | 件数 | 増減 | 備考 |
| --- | --- | --- | --- |
| 3E.0（着手前） | 672 | — | 3C 完了時点 |
| 3E.1 | 672 | ±0 | ベンチプロジェクト改修のみ |
| 3E.2 | 672 | ±0 | ベンチプロジェクト改修のみ |
| 3E.3 | 672 | ±0 | ベンチプロジェクト改修のみ |
| 3E.4 | 672 | ±0 | ドキュメント更新のみ |

### 7.2 ベンチマーク自体の検証方法

- `dotnet build tests/OsmDotRoute.Benchmarks -c Release` で warning 0 件
- `--list flat` で全シナリオが列挙される
- 各サブステップ完了時に `dotnet test` を実行し本体 testsuite が崩れていないことを確認
- 3E.3 本番計測前に `--job short` で各シナリオが 1 回実行できることを確認（実行可能性チェック）

---

## 8. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
| --- | --- | --- | --- |
| 0.1 (draft) | 2026-05-27 | 初版。Phase 3 ステップ 3E（ベンチマーク再実施）の 4 サブ分割（3E.1 既存ベンチ拡張 / 3E.2 C4 新規 / 3E.3 本番計測 + results.md / 3E.4 設計書 §7 + 計画書 §9 + 完了総括）。ユーザー判断 Q1=mixed-100 / Q2=既存 Car ペア流用 + 失敗率記録 / Q3=1 op = Add + Remove(id) / Q4=4 サブ確定。Phase 1 比較は [phase1_benchmark_results.md](phase1_benchmark_results.md) を固定参照（3C.4 で Itinero 環境完全撤去のため再現不可）。RouterDb 規模差（Phase 1 = 43k vs Phase 3 = 27k 頂点）は脚注で明示。本ステップは本体テストに触れず 672 件 pass 維持。リスク R1〜R7 整理 | Claude (Opus 4.7) |
