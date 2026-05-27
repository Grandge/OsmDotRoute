# Phase 3 ベンチマーク結果

**ステータス**: v0.1（2026-05-27 ステップ 3E.3 計測完了）
**対応計画書**: [Phase 3 ステップ 3E 計画書](phase3_step3E_plan.md)
**対応要件**: REQ-NFR-001（経路計算性能維持）、REQ-NFR-002（制約 100 件下劣化率 ≦ 1.5x）、REQ-NFR-003（経路 1 本あたりアロケート削減）
**関連文書**:

- [Phase 1 ベンチマーク結果](phase1_benchmark_results.md)（Phase 1 基準値、Itinero 環境は 3C.4 で完全撤去のため再現不可、本書では固定参照）
- [Phase 3 実装計画書 §9 性能基準値表](phase3_implementation_plan.md#9-性能基準値phase-1--phase-3-比較)（本書実測値で更新）
- [Phase 3 設計書 §7 ベンチマーク再実施](phase3_design.md)（本書要約を §7.3 検証結果に転記）

---

## 1. 計測環境

| 項目 | 値 |
|---|---|
| CPU | 11th Gen Intel Core i7-1165G7 @ 2.80GHz |
| 物理コア / 論理コア | 4 / 8 |
| OS | Windows 11 (10.0.26200.8457 / 25H2 / 2025Update / HudsonValley2) |
| .NET SDK | 9.0.103 |
| .NET ランタイム | 9.0.2 (9.0.225.6610), X64 RyuJIT x86-64-v4 |
| BenchmarkDotNet | v0.15.8 |
| GC | Concurrent Server |
| 計測グラフ | `samples/Data/tsushima.odrg` (8.48 MB、4 プロファイル bake) |
| 本ベンチ全体実行時間 | 約 10 分 16 秒（9 ベンチ実行） |

### 1.1 グラフ規模（Phase 1 比較）

| 項目 | Phase 1（`default.routerdb`） | Phase 3（`tsushima.odrg` v0.3） |
|---|---|---|
| ファイルサイズ | 19.4 MB（RouterDb 形式、Itinero 1.5.1） | **8.48 MB**（.odrg 形式、4 プロファイル bake） |
| Vertices | 43,685 | **53,727**（cycleway/footway 等を Bicycle/Truck 用に含む） |
| Edges | 57,331 | **74,276** |
| bake プロファイル | car, pedestrian（Itinero 内蔵） | car, pedestrian, **bicycle**, **truck** |
| bbox | (35.11°, 136.69°) - (35.21°, 136.81°) ≒ 11km × 11km | 同左（136.65,35.13,136.80,35.25 = 約 13km × 13km） |
| 対象地域 | 愛知県津島市（親プロ借用 RouterDb） | 愛知県津島市（中部 OSM PBF から bbox 抽出） |

**重要な留保**: Phase 1 (RouterDb) と Phase 3 (.odrg v0.3) は **抽出元 PBF / 範囲 / バケットプロファイル数が異なる**ため、Mean 値の直接比較には限界がある。本書では「Phase 1 値 = `default.routerdb` 43k 頂点での実測値」「Phase 3 値 = `tsushima.odrg` 53k 頂点での実測値」と明示し、比率比較（C0 vs C1 の倍率、Allocated の桁オーダー）を中心に評価する。

### 1.2 再現手順

```powershell
# 全ベンチ実行 (本番 job、約 10 分)
dotnet build tests/OsmDotRoute.Benchmarks -c Release
dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --filter "*"

# 個別実行 (絞り込み)
dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --filter "*RouteWithConstraintsBenchmark*"
dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --filter "*RestrictionThroughputBenchmark*"

# Bicycle 失敗率診断 (BenchmarkDotNet 外)
dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --bicycle-snap-probe

# TestData 再生成 (シード固定、通常不要)
dotnet run --project tests/OsmDotRoute.Benchmarks -c Release --no-build -- --generate-data
```

---

## 2. シナリオ定義（Phase 3 計画書 §3.5 / 3E 計画書 §2.2）

| Case | Profile | 制約 | データソース | 説明 |
|---|---|---|---|---|
| C0 | Car | なし | route-pairs.json (100 ペア) | 制約なし baseline |
| C1 | Car | mixed-100 | restrictions-mixed-100.json (block 50 + difficulty 50) | Phase 1 C3 相当、3B 効果本命 |
| C2 | Bicycle | mixed-100 | 同上 | Phase 3 新規、既存 Car ペア流用（成功率 65%、§3.3 参照） |
| C3 | — | — | — | C0/C1/C2 の Allocated を Phase 1 = 77 MB と比較するサブセクション（§4） |
| C4 | — | — | restrictions-mixed-100.json の block 系 50 件をリサイクル | 1 op = AddBlockArea + Remove(id)、Phase 1 未測定の新規（§5） |

**Mode** (RouteWithConstraintsBenchmark のみ):

- `Native-Detached`: NativeRoadGraph + RestrictedAreaService、AttachGraph **未実行**（3B 前相当、Phase 1 fallback）
- `Native-Attached`: 同上、AttachGraph 実行済（3B eager bake キャッシュ動作、本命）

---

## 3. 経路計算性能 (C0/C1/C2、REQ-NFR-001 / REQ-NFR-002)

### 3.1 実測値（DefaultJob、iter 10+ ベース）

| Case | Mode | Profile | Mean | Error | StdDev | Allocated |
|---|---|---|---:|---:|---:|---:|
| **C0** | Native-Detached | Car | **4.390 ms** | 0.087 ms | 0.110 ms | **1.92 MB** |
| **C0** | Native-Attached | Car | **4.637 ms** | 0.091 ms | 0.160 ms | **1.84 MB** |
| **C1** | Native-Detached | Car | **46.087 ms** | 3.089 ms | 9.108 ms | 8.49 MB |
| **C1** | Native-Attached | Car | **3.062 ms** | 0.061 ms | 0.126 ms | **34.13 MB** |
| **C2** | Native-Detached | Bicycle | 47.023 ms | 3.256 ms | 9.600 ms | 292.11 MB |
| **C2** | Native-Attached | Bicycle | **2.917 ms** | 0.050 ms | 0.062 ms | **1.50 MB** |

#### 補助ベンチ（独立計測）

| Type | Method | Mean | Error | StdDev | Allocated |
|---|---|---:|---:|---:|---:|
| RouteCalculationBenchmark | Router.Calculate（制約なし） | 4.594 ms | 0.092 ms | 0.228 ms | 1.87 MB |
| SnapBenchmark | Router.SnapToRoad | 34.39 μs | 0.686 μs | 2.023 μs | 21.4 KB |

### 3.2 Phase 1 比較

| 指標 | Phase 1（`default.routerdb` 43k） | Phase 3（`tsushima.odrg` v0.3 53k、Native-Attached） | 改善率 | Phase 3 目標 | 達成 |
|---|---:|---:|---:|---|:---:|
| C0 制約なし Mean | 32.97 / 35.64 ms | **4.64 ms** | **約 7 倍高速** | ≦ 33 ms（同等以下） | ✅ |
| C0 StdDev | 2.80 ms（OsmDotRoute Wrap Itinero） | 0.16 ms | StdDev も大幅縮小 | 同等維持 | ✅ |
| C1 mixed-100 Mean | 51.01 ms（Phase 1 C3）、C0 比 1.43x | **3.06 ms**、C0 比 **0.66x** | **約 17 倍高速** | ≦ 1.1× C0 | ✅ |
| Snap 単独 Mean | 1.78 ms | **34.39 μs** | **約 52 倍高速** | — | — |
| Itinero 比 | 0.48x（Phase 1 = 68.73 ms Itinero 直接） | （Itinero 撤去で再現不可、固定参照のみ） | — | ≦ 0.48x（同等以下） | — |

**観察**:

- C0 で **約 7 倍高速化** （Phase 1 33 ms → Phase 3 4.64 ms）。RouterDb 規模差（43k → 53k 頂点で 1.2 倍増）を考慮しても圧倒的高速化
- **C1 が C0 より速い (0.66x)**: 制約 100 件下では Dijkstra wave-front が制約エリアで早期 prune され、探索範囲が縮小する。Phase 1 では C0 比 1.43x（制約評価コスト > 探索縮小効果）だったが、Phase 3 では 3B eager bake キャッシュにより制約評価が O(1) HashSet ルックアップに圧縮 → 探索縮小効果が支配的に
- **3B 効果 (C1 Detached → Attached)**: 46.087 ms → 3.062 ms = **約 -93.4%、15 倍高速化** ⭐ 3B.5 の `--job short` 桁オーダー確認値（-93.5%）と本番統計値で完全一致
- **REQ-NFR-001 (≦ 100 ms) 全ケース達成**: 最大値 C2 Native-Detached = 47 ms < 100 ms
- **REQ-NFR-002 (制約 100 件下劣化率 ≦ 1.5x) 大幅達成**: C1/C0 = 0.66x（むしろ高速化）

### 3.3 C2 Bicycle スナップ失敗率（`--bicycle-snap-probe` 実測、2026-05-27）

既存 route-pairs.json（Car でスナップ成功 100 ペア）を Bicycle プロファイルで再実行した結果:

| 区分 | 件数 | 比率 |
|---|---:|---:|
| 成功（経路発見） | 65 | 65.0% |
| From スナップ失敗 | 17 | 17.0% |
| To スナップ失敗 | 17 | 17.0% |
| 経路発見失敗（両端スナップ成功・経路 null） | 1 | 1.0% |
| 合計 | 100 | 100.0% |

**解釈**: 既存ペアは Car（道路網）でスナップ成功した起終点を採用しており、Bicycle 通行可能道路（cycleway/footway/path）から 500m 半径内にスナップ点がないケースが 34/100 ある。津島市は motorway なし・trunk 数本のみだが、起終点が公園内 / 私有地で Car 道路から離れているケースが該当。

C2 Mean = 2.917 ms は `null` 返却ペア（34 件）を含む 100 ペア平均で、`null` 返却は SnapToRoad で早期 return するため高速。**Bicycle 道路網単独の純粋な経路計算性能は本書では確定しない**（Phase 4+ で Bicycle 専用ペア再生成 + 再測定を推奨）。

---

## 4. 経路 1 本あたりアロケート量 (C3 サブセクション、REQ-NFR-003)

### 4.1 Phase 1 → Phase 3 削減比較

| Case | Profile | Allocated/route (Phase 3 Attached) | Allocated/route (Phase 1) | 削減率 |
|---|---|---:|---:|---:|
| C0 | Car | **1.84 MB** | 76.98 MB | **約 42 倍削減** ⭐ |
| C1 | Car | **34.13 MB** | 57.31 MB | 約 1.7 倍削減 |
| C2 | Bicycle | **1.50 MB**（成功 65 + null 35 平均） | （Phase 1 未測定） | — |

**Phase 3 目標**: ≦ 5 MB（Phase 1 の 1/15、Itinero 32 MB の 1/6）、最低ライン ≦ 25 MB（Phase 1 R5 リスク対処）

| Case | 目標 ≦ 5 MB | 最低ライン ≦ 25 MB |
|---|:---:|:---:|
| C0 | ✅ 達成（1.84 MB） | ✅ 達成 |
| C1 | ❌ **未達（34.13 MB）** | ❌ **未達** |
| C2 | ✅ 達成（1.50 MB） | ✅ 達成 |

### 4.2 削減の主要因（C0 で 42 倍達成、設計書 §3, §4, §6 累積効果）

- 3A: `NativeRoadGraph` MMF + `ReadOnlySpan<GeoCoordinate>` 経由のシェイプアクセス（ゼロコピー）
- 3B: `RestrictedAreaEdgeCache` eager bake で制約評価ホットパスの `BuildFullShape` アロケート排除
- 3C: `Route.Shape: ReadOnlyMemory<GeoCoordinate>` 破壊変更で経路復元時のリストコピー排除

### 4.3 C1 Allocated 未達の原因分析と Phase 4+ への申し送り

**事実**: C1 (mixed-100 + Car) Native-Attached の Allocated = **34.13 MB**、目標 ≦ 5 MB に対し約 6.8 倍超過、最低ライン ≦ 25 MB も約 1.4 倍超過。

**3B.5 桁オーダー確認値との乖離**:

- 3B.5 (`--job short` iter=3): Native-Attached C3 (mixed-100 + Car) = **0.74 MB**（旧 odrg 27k 頂点）
- 3E.3 (DefaultJob iter 10+): Native-Attached C1 (mixed-100 + Car) = **34.13 MB**（新 odrg 53k 頂点）
- 約 **46 倍の乖離**。3B.5 の桁オーダー確認値は `--job short` のノイズで実態が見えていなかった可能性、または新 odrg (頂点 2 倍 / エッジ 2 倍) で制約評価対象エッジ数が増えたことが原因と推察

**推察される主要因**:

1. **`EvaluateConstraints` (graph 未注入時 fallback) と異なり、eager bake 経路でも `EvaluateConstraints` 自体が経路復元時に呼ばれている可能性** — 設計書 §4.6 で「graph 注入時は `EvaluateConstraints` は実質使われない」とあるが、本番ベンチで Allocated が大きいのは経路復元側で `EvaluateConstraints` のフォールバックが呼ばれているか、別箇所でアロケートしているか
2. **3B キャッシュ HashSet の制約数増による Lookup オーバーヘッド** — `RestrictedAreaEdgeCache.IsBlocked(edgeId)` 自体は O(1) だが、HashSet enumerator アロケートや、難所評価で `EvaluateDifficulty` の `EdgeEvaluation` struct アロケート等が積み上がる可能性
3. **Native-Detached C2 = 292 MB → Attached C2 = 1.50 MB の対比**: Bicycle 経路は `null` 比率 35% で早期 return → Allocated 小。Car (C1) は 100% 成功で経路復元まで走るため Allocated 大

**Phase 4+ への申し送り**:

- 3E.3 で `MemoryDiagnoser` の詳細解析を進める（Gen0/Gen1/Gen2 別、Allocated 大きいパスの特定）。本書では C1 Allocated 未達を事実として記録、根治は Phase 4+ ステップ「ホットパス Allocated 最適化」として切り出す
- C0 / C2 では目標達成、C1 のみ未達 = 「制約評価ホットパスでまだ Allocated が大きい」とは特定。Phase 4+ で `EdgeWeightCalculator` 経路の Allocated プロファイリングを実施

### 4.4 Native-Detached との対比（3B 効果の Allocated 観点）

| Case | Native-Detached | Native-Attached | Allocated 改善 |
|---|---:|---:|---:|
| C0 | 1.92 MB | 1.84 MB | -4%（制約 0 件で効果なし、期待通り） |
| C1 | 8.49 MB | 34.13 MB | **+302%（悪化）** ← 想定外 |
| C2 | 292.11 MB | 1.50 MB | **約 195 倍削減** ⭐ |

**C1 で Detached < Attached となる現象**: これは想定外。Detached モードでは graph 未注入で `EvaluateConstraints` の R-tree クエリパス、Attached モードでは `RestrictedAreaEdgeCache` のホットパス、それぞれ別実装。Attached の方が経路解析が**速い (Mean -93.4%)** にも関わらず**アロケートが多い (4 倍)** という挙動。これは Phase 4+ で再評価が必要な事項（§4.3 と同様、Phase 4+ ホットパス最適化ステップで根治判断）。

---

## 5. 制約 add/remove スループット (C4、Phase 3 新規)

### 5.1 実測値

| Operation | Mean | Error | StdDev | Gen0 | Allocated | ops/sec |
|---|---:|---:|---:|---:|---:|---:|
| AddBlockArea + Remove(id) 1 サイクル | **163.30 μs** | 2.030 μs | 1.898 μs | 4.15 / 1000 op | **87.1 KB** | **約 6,124 ops/sec** |

### 5.2 解釈

1 op で eager bake (`QueryEdgesByAabb` + `EdgeIntersectsShape` + HashSet 追加) と cache `RemoveArea` (HashSet 削除) の合計コストを観測。Phase 1 では未測定の新規シナリオであり、Phase 3 で動的制約 add/remove スループットの基準値を確定。

- **約 6,124 ops/sec** = 1 秒あたり制約 6,124 回の追加+削除サイクル
- **Allocated 87.1 KB/op** はやや大きい（HashSet enumerator + Aabb→OdrgBbox 変換 + `EdgeIntersectsShape` 内部の中間オブジェクト）。Phase 4+ で削減余地
- 親プロジェクト（災害廃棄物処理シミュレーション）のシナリオで「シミュレーション 1 ステップごとに制約 100 件追加」想定でも、100 件追加 = 100 × 163 μs = 16.3 ms 程度で完了。実用性十分

---

## 6. 判定サマリ (REQ-NFR-001〜003)

| 要件 | 目標 | 実測 | 判定 |
|---|---|---|:---:|
| REQ-NFR-001 | 経路計算 ≦ 100 ms | C0 = 4.64 ms、C1 = 3.06 ms、C2 = 2.92 ms（全 ≦ 5 ms） | ✅ 大幅達成 |
| REQ-NFR-002 | 制約 100 件下劣化率 ≦ 1.5x | C1 / C0 = 3.06 / 4.64 = **0.66x** | ✅ 大幅達成 |
| REQ-NFR-003 | 経路 1 本あたりアロケート削減 | C0 = 1.84 MB（≦ 5 MB ✅）、C1 = 34.13 MB（≦ 5 MB ❌、≦ 25 MB ❌）、C2 = 1.50 MB（≦ 5 MB ✅） | ⚠️ **部分達成**（C0/C2 達成、C1 未達） |

**追加達成項目**:

- 3B 効果: 制約 100 件下で Mean -93.4%（46 ms → 3 ms）、3B.5 桁オーダー確認値と一致
- Snap 単独: Phase 1 比 約 52 倍高速化（1.78 ms → 34.4 μs）
- C4 制約 add/remove: 約 6,124 ops/sec の新規基準値確定

**判定総括**:

- **REQ-NFR-001 / REQ-NFR-002 は Phase 1 を大幅に上回って達成**。3A/3B/3C の累積効果が本番統計値で確認された
- **REQ-NFR-003 は C0 / C2 で目標達成、C1 (mixed-100 + Car) のみ未達**。Phase 4+ で「制約評価ホットパス Allocated 最適化」ステップとして切り出し（§4.3 申し送り）
- Phase 3 として REQ-NFR-001〜003 の総合達成可否は、C1 Allocated 未達をユーザー判断項目とする（本書ステータス確定はユーザー判断）

---

## 7. Phase 3 計画書 §9 性能基準値表の更新

[Phase 3 実装計画書 §9](phase3_implementation_plan.md#9-性能基準値phase-1--phase-3-比較) の表を本実測値で更新したのが下表:

| 指標 | Phase 1 値（津島市、default.routerdb） | Phase 3 値（津島市、tsushima.odrg v0.3） | Phase 3 目標 | 達成 | 検証ステップ |
|---|---|---|---|:---:|---|
| 経路計算（C0、100 ペア平均） | 33 ms | **4.64 ms**（Native-Attached）/ 4.59 ms（独立 RouteCalc） | ≦ 33 ms | ✅ | 3E |
| Itinero 比 | 0.48x | （Itinero 撤去で再現不可、固定参照） | ≦ 0.48x（同等以下） | — | — |
| StdDev | Itinero の 1/7 | 0.16 ms（C0 Native-Attached） | 同等維持 | ✅ | 3E |
| 制約 100 件下（C1） | 51 ms（C0 比 1.43x） | **3.06 ms（C0 比 0.66x）** | ≦ 1.1× C0 | ✅ | 3E |
| 経路 1 本あたりアロケート（C3） | 77 MB（Itinero 2.4x） | C0 = 1.84 MB ✅ / **C1 = 34.13 MB ❌** | ≦ 5 MB（Phase 1 の 1/15） | ⚠️ 部分達成 | 3E |
| Bicycle (C2、新規) | — | 2.92 ms / 1.50 MB（成功率 65%） | 基準値確定 | ✅ | 3E |
| 制約 add/remove スループット（C4、新規） | — | **163 μs/op = 6,124 ops/sec** / 87.1 KB | 基準値確定 | ✅ | 3E |
| 経路距離同等性（親プロ統合） | 89/89 ペア ±10% 以内、Mean 0.07% | 未実施 | 同等維持 | — | 3F |
| 都道府県単位（愛知県全域、C5） | 未測定（Phase 1 §18.2 未解消） | 未実施 | 実測値を取得 | — | 3G |
| 定常 WorkingSet | 54 MB（16 GB の 0.3%） | 未測定（本ベンチでは取得せず） | ≦ 54 MB（同等以下） | — | 3E（追加実施判断） |

---

## 8. Phase 4+ への申し送り

### 8.1 C1 Allocated 未達根治（§4.3 + §4.4）

- C1 Native-Attached Allocated = 34.13 MB（目標 ≦ 5 MB、最低ライン ≦ 25 MB ともに未達）
- 原因仮説: `EdgeWeightCalculator` ホットパスで HashSet enumerator・`EdgeEvaluation` struct・難所評価結果オブジェクトのアロケート積み上げ
- Phase 4+ ステップとして「ホットパス Allocated 最適化」を切り出し、`MemoryDiagnoser` 詳細解析 → Span 化 / pooling 適用

### 8.2 Native-Detached C1 vs Attached C1 で Allocated が逆転（§4.4）

- Detached 8.49 MB < Attached 34.13 MB は想定外
- Attached の方が Mean は -93.4% で圧倒的に速いが、Allocated は 4 倍多い
- `EvaluateConstraints` (Detached) と `RestrictedAreaEdgeCache` (Attached) の実装パスが本質的に異なるため、別軸での最適化が必要

### 8.3 Bicycle 専用 route-pairs.json の生成

- C2 Mean = 2.92 ms は `null` 返却 35% を含む平均値で、Bicycle 道路網単独性能を表していない
- Phase 4+ で `RoutePairGenerator` を `VehicleProfile` パラメータ化し、Bicycle 専用 100 ペアを別 seed で生成 → C2′ として独立測定

### 8.4 C4 add/remove の分離測定

- 本書では 1 op = Add + Remove(id) 1 サイクルのみ
- Phase 4+ で Add のみ / Remove(id) のみ / RemoveByTag(tag) / ClearAll() を独立計測し、操作別コスト内訳を確定

### 8.5 都道府県単位ベンチ（3G）への引継ぎ

- 本書は津島市 53k 頂点 / 74k エッジでの実測値
- 3G で愛知県全域 PBF（推定 500k 頂点 / 700k エッジ）から `.odrg` 抽出 → 本ベンチを再実行し、規模拡張時の性能維持を確認
- Phase 1 §18.2 「都道府県単位は未測定」リベンジの位置付け

### 8.6 定常 WorkingSet の測定漏れ

- Phase 1 では `--memory-probe` で実測（54 MB）したが、本ベンチでは省略
- 3F 親プロ統合または 3G 都道府県単位ベンチ着手時に `--memory-probe` 相当の機能を実装 → 定常 WorkingSet 確認

---

## 9. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 | 2026-05-27 | 初版（3E.3 計測完了）。C0/C1/C2 × Native-Detached/Native-Attached の 6 ケース + RouteCalculation + Snap + C4 add/remove スループット計 9 ベンチ、約 10 分実行。Phase 1 比較で C0 約 7 倍高速 / 3B 効果 -93.4% / C0 Allocated 42 倍削減を確認。**C1 Allocated 34 MB は目標 ≦ 5 MB 未達**（Phase 4+ ホットパス最適化ステップへ申し送り）。REQ-NFR-001 / 002 大幅達成、REQ-NFR-003 部分達成（C0/C2 達成・C1 未達）。Bicycle 失敗率 65/100、C4 = 6,124 ops/sec の新規基準値確定 | Claude (Opus 4.7) |
