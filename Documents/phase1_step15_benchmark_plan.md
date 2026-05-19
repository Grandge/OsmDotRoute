# Phase 1 ステップ 15: ベンチマーク・性能検証 計画書

**ステータス**: ドラフト v0.1（ユーザーレビュー待ち、2026-05-20）
**対応ステップ**: Phase 1 ステップ 15
**対応要件**: REQ-NFR-001（100ms 経路計算）、REQ-NFR-002（制約 100 件下でも維持）、REQ-NFR-003（16GB RAM）
**関連文書**:

- [Phase 1 実装計画書 §8 ステップ 15](phase1_implementation_plan.md#L862)
- [Phase 1 設計書 §16 ベンチマーク結果](phase1_design.md#L2094)（記述先）
- [要件定義書 REQ-NFR-001〜003](requirement_definition.md#L259)

---

## 1. 目的

Phase 1 機能要件完了済の現状で、性能要件 REQ-NFR-001〜003 が達成可能かを定量計測し、以下のいずれかに着地させる:

- **A. 達成**: 設計書 §16 に結果を記録し、ステップ 16（親プロ統合）へ進む
- **B. 軽微未達**: 計画書 §7「未達時の対策」に沿ってローカル最適化を実施、再計測で達成
- **C. 大幅未達**: Phase 4 の CH（Contraction Hierarchies、REQ-NFR-004）送りをユーザーと合意、現状値を §16 と §18 に記録した上でステップ 16 へ

性能未達自体は Phase 1 完了を阻まない（CLAUDE.md ルール上、最終判断はユーザー）。本ステップは「現状値を明らかにして、対策方針を確定する」ことが本質。

**スコープ外（ユーザー判断 2026-05-20）**:

- DifficultyArea が経路上に乗ったときの **迂回による Dijkstra 探索領域拡大コスト** は自動ベンチで計測しない。ステップ 17（ユーザー検証）で MapVerifier を使った手動シナリオで体感確認する。本ステップでは「ランダム配置の混合制約」での平均挙動のみ記録する。

---

## 2. 計測環境

**ユーザーのローカルマシン一本で計測する**（ユーザー判断 2026-05-20）。CI でのベンチマーク自動化は Phase 1 では実施しない。

### 2.1 計測マシン仕様（記録項目）

ベンチマーク実行時に以下を計測スクリプトで自動採取し、`Documents/phase1_benchmark_results.md` 冒頭に記録する:

- CPU モデル名・物理コア数・論理コア数・ベースクロック
- 搭載 RAM 容量・利用可能 RAM 容量（実行直前時点）
- OS 名・ビルド番号
- .NET ランタイムバージョン（`RuntimeInformation.FrameworkDescription`）
- ストレージ種別（SSD / NVMe / HDD のいずれか、RouterDb 読込性能に影響）

### 2.2 計測前の安定化手順（再現可能性）

- 計測中は他の重い GUI アプリ（IDE のビルド・Unity・Chrome 多タブ等）を閉じる
- 電源プランを「高パフォーマンス」に固定
- ノート PC の場合は AC アダプタ接続
- BenchmarkDotNet 標準の Warmup / IterationCount で揺らぎを吸収（既定値で開始、ばらつき大なら個別調整）
- `Release` 構成・PDB 同梱・`<ServerGarbageCollection>true</ServerGarbageCollection>` を Benchmarks csproj に設定

### 2.3 再現手順

```powershell
# プロジェクトルートで実行
dotnet build -c Release
dotnet run -c Release --project tests/OsmDotRoute.Benchmarks -- --filter "*"
```

結果は `BenchmarkDotNet.Artifacts/` に出力される。サマリーを `Documents/phase1_benchmark_results.md` に貼り付ける運用とする。

---

## 3. 対象シナリオ

### 3.1 計測対象メソッド（公開 API ベース）

ベンチマーククラスごとに以下を対象とする。`Router.Calculate` が主軸、他は補助。

| クラス | メソッド | 計測内容 | 対応要件 |
|---|---|---|---|
| `RouterDbLoadBenchmark` | `ItineroRouterDbLoader.LoadFromFile` | RouterDb 読み込み時間・ピーク RAM | REQ-NFR-003 |
| `RouteCalculationBenchmark` | `Router.Calculate(car, from, to)`（制約なし） | 経路 1 本あたり ms（ベースライン） | REQ-NFR-001 |
| `ItineroBaselineBenchmark` | Itinero `Router.TryCalculate(Vehicle.Car.Fastest(), src, tgt)` | 同じ起終点ペアでの Itinero ネイティブ性能（比較基準） | REQ-NFR-001（相対比較） |
| `RouteWithConstraintsBenchmark` | `Router.Calculate` を 5 ケース下で計測（§3.4 参照） | 制約件数・種別の影響 | REQ-NFR-001, REQ-NFR-002 |
| `SnapBenchmark`（補助） | `Router.SnapToRoad` | スナップ単独の ms（経路計算内訳分解用） | — |

### 3.2 起終点ペア生成

決定論的に再現可能なペア集合を生成し、計測間でブレないようにする。

**生成方針**:

- RouterDb の道路ネットワークの bbox を取得
- `Random(seed: 20260520)` を使い、bbox 内の (緯度, 経度) ペアを生成
- 各候補について `Router.SnapToRoad(car, point, 500m)` が成功する点だけを採用
- 起点・終点の直線距離が 1〜30km の範囲のものだけを採用（極短・極長を除く、典型的災害シナリオの距離感）
- 採用ペアを 100 件で打ち切り
- 生成結果は JSON として `tests/OsmDotRoute.Benchmarks/TestData/route-pairs.json` にキャッシュ保存（初回生成時のみ計算、以後は読み込み）

**根拠**: 100 ペアで Mean / StdDev / P95 が安定すれば十分（BenchmarkDotNet が内部で 16 回程度反復計測するため、実質 100 × 16 = 1600 サンプル）。

### 3.3 制約データ生成

ミニマムセット方針（ユーザー判断 2026-05-20）に従い、以下 2 つの決定論セットを生成・コミットする:

| ファイル | 内容 | 用途 |
|---|---|---|
| `tests/OsmDotRoute.Benchmarks/TestData/restrictions-mixed-100.geojson` | BlockArea 50 件 + DifficultyArea 50 件（`flood`/`debris`/`narrow`/`damaged`/`closed` を 10 件ずつ）。`Random(seed: 20260521)` | 主計測（ベースライン混合パターン） |
| `tests/OsmDotRoute.Benchmarks/TestData/restrictions-block-100.geojson` | BlockArea 100 件のみ。`Random(seed: 20260522)` | 短絡効果チェック（DifficultyArea の `speedFactor` 計算がない場合の上限性能） |

**生成方針共通**:

- 同じ bbox 内、半径 200〜2000m の三角形〜五角形ポリゴン
- 計測時には先頭 `n` 件を `RestrictedAreaService` に登録
- 起終点ペアと制約の地理的重なり度合いは結果として大小ありうるが、決定論なので「同じ条件」での測定は再現可能

### 3.4 計測ケース（ミニマムセット = 5 ケース）

| ケース | 制約セット | n | 期待される挙動 |
|---|---|---|---|
| C0 | — | 0 | ベースライン（制約なし） |
| C1 | mixed-100 | 10 | `SpatialIndex` 検索コストが軽微 |
| C2 | mixed-100 | 50 | 中規模、典型ユースケース想定 |
| C3 | mixed-100 | 100 | REQ-NFR-002 の判定対象 |
| C4 | block-100 | 100 | BlockArea のみ → `PositiveInfinity` 短絡で C3 より速いはず（短絡効果の定量化） |

**設計判断（ミニマムセット採用根拠）**:

- 「種別 × 件数」の 2 軸を直交させると 12 ケースになり計測工数が膨らむため、混合 4 段階で件数感度を見て、Block-only 1 ケースで短絡効果の存在を確認する 5 ケース構成に絞った
- C3 が 100ms 達成し C4 がより速ければ「DifficultyArea の `speedFactor` 計算コスト < `SpatialIndex` 検索コスト」と推定でき、最適化優先順位の手がかりになる
- C3 が大幅未達のときは §7 の対策で内訳分解（プロファイラ / `dotnet-trace`）を実施

### 3.5 BenchmarkDotNet 設定

- `[MemoryDiagnoser]` を全クラスに付与（GC 世代別 alloc、ピーク管理メモリ）
- `[ThreadingDiagnoser]` は対象外（経路計算は同期 API）
- `[BenchmarkCategory]` で「LoadOnce」「PerRoute」を分け、`RouterDb` 読込は `GlobalSetup` で 1 回だけ
- `RouteWithConstraintsBenchmark` は `[Params]` で C0〜C4 をパラメータ化（ケース ID をキーに前述の制約セット＋件数を `GlobalSetup` でロード）
- iterations は既定値（Auto）から始め、StdDev が Mean の 10% を超えるなら `[IterationCount(20)]` を試す

---

## 4. テストデータ

| データ | 出所 | サイズ | 配置 |
|---|---|---|---|
| `default.routerdb` | 親プロ `d:\workspace\災害廃棄物処理シミュレーション\App\DisasterWasteSim.Server\Data\Scenarios\default.routerdb` | 19.4 MB（千葉県想定） | 既存 `TestPaths.cs` 経由で参照、コピーしない |
| `route-pairs.json` | 初回ベンチ実行時に生成 | 〜数 KB | `tests/OsmDotRoute.Benchmarks/TestData/` |
| `restrictions-mixed-100.geojson` | 初回ベンチ実行時に生成 | 〜数十 KB | `tests/OsmDotRoute.Benchmarks/TestData/` |
| `restrictions-block-100.geojson` | 初回ベンチ実行時に生成 | 〜数十 KB | `tests/OsmDotRoute.Benchmarks/TestData/` |

`route-pairs.json` と 2 つの GeoJSON は **生成スクリプトをコードに含めた上で、生成済みファイルもコミット** する（毎回再生成は不要、計測再現性のため）。

「`default.routerdb` は親プロ依存」という制約は要件上問題ない（CLAUDE.md ルールではコピー禁止だが参照は許可、Phase 2 で独自フォーマットに移行する想定）。

---

## 5. 計測指標と判定基準

### 5.1 経路計算（REQ-NFR-001）

| 指標 | 目標 | 計測元 |
|---|---|---|
| Mean | ≤ 100 ms | `RouteCalculationBenchmark.CalculateRoute` Mean |
| P95 | ≤ 200 ms（参考値） | BenchmarkDotNet `Percentiles` |
| Allocated | 上限なし、参考記録 | `MemoryDiagnoser` |

### 5.1.1 Itinero 比較（相対判定、等倍以上を期待）

| 指標 | 目標 | 計測元 |
|---|---|---|
| OsmDotRoute Mean / Itinero Mean | ≤ 1.0x（OsmDotRoute が Itinero と同等以上に速い） | `RouteCalculationBenchmark` / `ItineroBaselineBenchmark` の Mean 比 |
| Itinero Mean そのもの | 記録のみ（Itinero 自体が 100ms 内かを確認） | `ItineroBaselineBenchmark` Mean |
| 経路結果の同等性 | 両者成功ペアで距離が ±10% 以内 | 計測時に `Distance` を相互比較 |
| 経路発見率の差 | 記録のみ（§9 のペア分類参照） | OsmDotRoute-only / Itinero-only / 両方失敗の件数 |

**設計判断（比較基準を等倍にした根拠、ユーザー判断 2026-05-20）**:

- Itinero は Profile 評価に **Lua インタプリタ** を使うため、エッジを 1 本評価するたびに Lua スクリプトの実行コストが発生する
- 一方 OsmDotRoute は ステップ 5a で実装した JSON プロファイル + `ProfileEvaluator` により、**Lua を介さずネイティブ C# で評価** する（埋込 `car.json` / `pedestrian.json`）
- したがって原理的には Lua 解釈オーバーヘッドぶんだけ OsmDotRoute が速いはず — Itinero と同等（1.0x）に収まらないなら、OsmDotRoute 側に何らかの想定外コストがあると判断し、§7.1 の対策で内訳分解する
- 制約 0 件時の `RestrictedAreaService` の関数呼び出しは数 ns オーダー（`_entries.Count == 0` で即時 return）のため、Lua 解釈コストには太刀打ちできない想定
- Itinero Mean が 100ms を超えていれば、OsmDotRoute が 100ms 未達でも「Itinero では達成不能な水準だった」として REQ-NFR-001 の妥当性を再協議
- 経路結果の同等性は機能パリティの最終確認（ステップ 5a の Profile パリティ 0/52 mismatch を経路レベルでも追認）

### 5.2 制約下経路計算（REQ-NFR-002）

| 指標 | 目標 | 計測元 |
|---|---|---|
| Mean (C3 = mixed n=100) | ≤ 100 ms（C0 と同等オーダー） | `RouteWithConstraintsBenchmark` Mean@C3 |
| 劣化率 (C3 / C0) | ≤ 1.5x | 計算値 |
| 短絡効果 (C4 / C3) | C4 が C3 より速いまたは同等 | 計算値 |

劣化率 1.5x は本計画で初提案する内部目標。要件定義書には明示なし。ユーザー判断で緩和・厳格化可能。

### 5.3 RAM（REQ-NFR-003）

| 指標 | 目標 | 計測元 |
|---|---|---|
| ピーク管理メモリ | ≤ 4 GB（16GB マシンで他プロセス併用考慮） | `RouterDbLoadBenchmark` 完了直後の `GC.GetTotalMemory(true)` |
| プロセス Working Set | ≤ 8 GB | `Process.GetCurrentProcess().WorkingSet64` |

要件は「16GB RAM で動作可能」のみで上限値は明示なし。本計画では「OS + 他アプリ + .NET ランタイムで合計 8GB 程度使うと仮定し、本ライブラリは 8GB を上限とする」を内部目安に採用。

---

## 6. 出力物

### 6.1 コード

- `tests/OsmDotRoute.Benchmarks/Program.cs`: BenchmarkDotNet ランナー（既存の Hello World を置換）
- `tests/OsmDotRoute.Benchmarks/Benchmarks/RouterDbLoadBenchmark.cs`
- `tests/OsmDotRoute.Benchmarks/Benchmarks/RouteCalculationBenchmark.cs`
- `tests/OsmDotRoute.Benchmarks/Benchmarks/ItineroBaselineBenchmark.cs`（Itinero ネイティブ比較）
- `tests/OsmDotRoute.Benchmarks/Benchmarks/RouteWithConstraintsBenchmark.cs`（5 ケース）
- `tests/OsmDotRoute.Benchmarks/Benchmarks/SnapBenchmark.cs`
- `tests/OsmDotRoute.Benchmarks/Generators/RoutePairGenerator.cs`（決定論ペア生成）
- `tests/OsmDotRoute.Benchmarks/Generators/RestrictionGenerator.cs`（決定論制約生成、mixed/block の 2 セット）
- `tests/OsmDotRoute.Benchmarks/TestData/route-pairs.json`（生成キャッシュ、コミット対象）
- `tests/OsmDotRoute.Benchmarks/TestData/restrictions-mixed-100.geojson`（同上）
- `tests/OsmDotRoute.Benchmarks/TestData/restrictions-block-100.geojson`（同上）
- `tests/OsmDotRoute.Benchmarks/OsmDotRoute.Benchmarks.csproj`: `<ServerGarbageCollection>` 設定追加

### 6.2 ドキュメント

- `Documents/phase1_benchmark_results.md`（新規）: 計測環境、4 ベンチクラスの結果、判定、未達時の対策結果
- `Documents/phase1_design.md` §16「ベンチマーク結果」: 上記サマリーを設計書に転記
- 要件定義書 REQ-NFR-001〜003 の `[ ]` を判定結果に応じて `[x]` または「条件付き合格」コメント追加（ユーザー指示後）

---

## 7. 未達時の対策（A → C の優先順）

### 7.1 軽微未達（Mean 100〜200ms）の対策

1. **エッジシェイプ AABB のキャッシュ**: 現状は `RestrictedAreaService.EvaluateConstraints` 内で毎回 `Aabb.FromCoordinates(edgeShape)` を計算。同一エッジが Dijkstra 探索中に複数回触られる場合はキャッシュで改善余地あり
2. **プロファイル `FactorAndSpeed` キャッシュ**: `ProfileEvaluator.Evaluate` が同じタグ列に対し再評価しているなら、エッジ ID → 評価結果の辞書をプロファイル毎に作成
3. **`SpatialIndex` の AABB 配列構造変更**: 現状はリニアスキャン気味。R-tree 化（NetTopologySuite 等の参照は禁じる、自前実装が必要）

C3 と C4 の比較から **どちらが律速か** の手がかりを得る:

- C4（Block-only）が C3 より大幅に速い → DifficultyArea の評価コスト（`ProfileEvaluator.EvaluateDifficulty`）が律速 → 対策 2 が有効
- C4 と C3 が同等 → `SpatialIndex` 検索 + 厳密判定が律速 → 対策 1 / 3 が有効

### 7.2 大幅未達（Mean 200ms 超）の対策

4. **双方向 Dijkstra**（REQ-RTE-009、Phase 4 案件を前倒し）
5. **CH 対応**（REQ-NFR-004、Phase 4）— 前倒し却下、現状値を記録してステップ 16 へ

### 7.3 対策実施判断

- 7.1 の 1〜3 は本ステップ内で実施可（合計 1〜2 日の追加工数）
- 7.2 の 4 は本ステップで前倒しするとステップ 15 工数が倍以上になる、原則ユーザー承認後にスケジュール再調整
- 7.2 の 5 は Phase 4 で確定、本ステップでは実施しない

### 7.4 迂回探索拡大が原因の場合

ベースラインの混合シナリオで C3 が未達かつ C4 も未達のときは、`SpatialIndex` 検索コストではなく **Dijkstra 探索領域そのものが拡大している** 可能性がある。本ステップでは自動計測しないため、以下の手順で補足調査する:

- ステップ 17（ユーザー検証）で MapVerifier を使い、「経路直線上に DifficultyArea を意図的に配置」したシナリオを手動再現し、計算時間と探索ノード数感を体感確認
- 体感で大幅遅延が確認されたら設計書 §16 と §18（既知の課題）に記録、Phase 4 の双方向 Dijkstra で対処予定として申し送る

---

## 8. 実装手順（小ステップ分解）

| # | 作業 | 完了判定 | 想定工数 |
|---|---|---|---|
| 15-1 | Benchmarks csproj に `ServerGC` 設定追加、`MemoryDiagnoser` 等の attribute 配備 | ビルド成功 | 0.2 日 |
| 15-2 | `RoutePairGenerator` + `RestrictionGenerator` 実装、TestData 生成 → コミット | 生成 JSON/GeoJSON が決定論で再現（mixed/block の 2 セット） | 0.5 日 |
| 15-3 | `RouterDbLoadBenchmark` 実装・実行 | RAM ピーク取得、§16 に記録 | 0.3 日 |
| 15-4 | `RouteCalculationBenchmark` 実装・実行 | Mean / P95 取得（C0 相当） | 0.3 日 |
| 15-4b | `ItineroBaselineBenchmark` 実装・実行（同じ route-pairs.json 使用） | Itinero Mean 取得、経路結果の同等性確認（距離 ±10%） | 0.3 日 |
| 15-5 | `RouteWithConstraintsBenchmark` 実装・実行（C0〜C4 の 5 ケース） | 全ケース Mean 取得、劣化率算出 | 0.4 日 |
| 15-6 | `SnapBenchmark` 実装・実行（補助、内訳分解用） | Mean 取得 | 0.2 日 |
| 15-7 | 判定 → `phase1_benchmark_results.md` 作成 → 設計書 §16 更新 | ユーザー報告 | 0.3 日 |
| 15-8 | 未達時の対策実施（必要時のみ、7.1 範囲） | 改善後の再計測値を §16 に追記 | 0〜2 日 |
| 15-9 | 要件定義書の REQ-NFR-001〜003 を更新（ユーザー指示後） | チェック更新 | 0.1 日 |

**合計**: 達成パス 約 2.5 日、軽微未達対策込み 約 3〜4 日

---

## 9. リスク

| リスク | 影響 | 対応 |
|---|---|---|
| 親プロ `default.routerdb` が想定より小さい（千葉県でなく市単位） | REQ-NFR-001「都道府県単位」の判定が緩くなる | RouterDb 読込時に頂点数・エッジ数を `RouterDbStatistics` で確認、§16 に明記 |
| ベンチマークマシンが要件下限（16GB RAM）より高性能 | REQ-NFR-003 判定の実意が薄れる | マシンスペックを §16 冒頭に明記、相対的なメモリ使用量で判定 |
| 双方向 Dijkstra 前倒しが Phase 1 全体を 1 週間以上遅延させる | Phase 1 完了予定への影響 | 大幅未達時は前倒し却下を既定（§7.3）。ユーザーが前倒し希望時のみ実施 |
| BenchmarkDotNet が `net9.0` で Itinero 経由のロード時にエラー | 計測不能 | RouterDb 読込部のみ別プロセスで計測する代替案を準備（`Process.Start` で外部 `ConsoleDemo` を起動して時刻記録） |
| DifficultyArea が経路に乗ったときの探索拡大コストがベンチに現れない（ランダム配置のため） | 「典型ケースは OK、最悪ケースは未知」となる | §7.4 の手動シナリオで補足、結果を §16/§18 に記録 |
| Itinero 比較で OsmDotRoute が等倍を達成できない（> 1.0x） | Phase 1「Lua インタプリタ非依存」の優位性根拠が揺らぐ | プロファイラ（`dotnet-trace` / `PerfView`）で内訳分解、§7.1 の対策を優先実施。劣化要因が `RestrictedAreaService` 経由のオーバーヘッドだけなら「動的制約機能のコスト」として許容を検討（ユーザー判断、§16 に記録） |
| Itinero の `Router.TryCalculate` が一部ペアで経路発見せず、OsmDotRoute は発見する（または逆） | 比較対象が揃わない | 100 ペアを以下 3 区分に分類して §16 に件数記録: (a) 両方成功 — Mean 計算対象 / (b) OsmDotRoute-only 成功 / (c) Itinero-only 成功 / (d) 両方失敗。Mean 比は (a) のみで算出。(b)(c) は数のバランスを記録し、極端な偏りがあれば原因調査（スナップ半径・道路属性解釈の差異など） |

---

## 10. 完了判定（実装計画書 §8 ステップ 15 完了判定の再掲・具体化）

- [ ] BenchmarkDotNet 5 シナリオ（Load / RouteCalc / ItineroBaseline / WithConstraints / Snap）・計 5 ケース（C0〜C4）の実装完了
- [ ] Itinero 比較: OsmDotRoute / Itinero Mean 比（目標 ≤ 1.0x）、両方成功ペアでの経路距離同等性（±10%）、経路発見成否の 4 区分件数を `phase1_benchmark_results.md` に記録
- [ ] `Documents/phase1_benchmark_results.md` に計測環境＋結果一覧記録
- [ ] REQ-NFR-001 / REQ-NFR-002 / REQ-NFR-003 のいずれも「達成」または「ユーザー合意のもと条件付き合格／Phase 4 送り」が明記
- [ ] 設計書 §16「ベンチマーク結果」更新（実装計画書 §8 ステップ 15 完了判定 §3 該当）
- [ ] 未達時の対策実施有無に応じ §11「制約対応 Dijkstra 統合」・§10「制約管理基盤」も更新
- [ ] 迂回拡大シナリオの手動確認方針（§7.4）を §18 に申し送り
- [ ] ユーザー報告 → 承認 → ステップ 16 着手

---

## 11. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 (draft) | 2026-05-20 | 初版ドラフト作成（ステップ 15 計画詳細化）。ベンチケースはミニマムセット 5 ケース（混合 C0〜C3 + Block-only C4）、迂回拡大は MapVerifier 手動シナリオに分離 | Claude (Opus 4.7) |
| 0.2 (draft) | 2026-05-20 | Itinero ネイティブ性能との比較ベンチ（`ItineroBaselineBenchmark`）を追加。同じ起終点ペアで Itinero `Router.TryCalculate(Vehicle.Car.Fastest())` を計測、Mean 比 ≤ 2.0x と経路距離 ±10% を相対判定基準として導入。§3.1 / §5.1.1 / §6.1 / §8 / §9 / §10 更新 | Claude (Opus 4.7) |
| 0.3 (draft) | 2026-05-20 | Itinero 比較基準を 2.0x → **等倍 (1.0x)** に厳格化（ユーザー判断）。根拠を「Itinero は Lua インタプリタ依存、OsmDotRoute はネイティブ C# 評価で原理的に速いはず」と明記。経路発見成否は 4 区分（両方成功 / OsmDotRoute-only / Itinero-only / 両方失敗）でカウント記録、Mean 比は両方成功ペアのみで算出 | Claude (Opus 4.7) |
