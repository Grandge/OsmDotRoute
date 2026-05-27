# Phase 3 ステップ 3G: 都道府県単位ベンチ 計画書

**ステータス**: ドラフト v0.1（着手前、2026-05-28）
**対応ステップ**: Phase 3 ステップ 3G（[Phase 3 実装計画書 §3.7 / §6](phase3_implementation_plan.md)）
**対応要件**: Phase 1 §18.2 リベンジ（「性能ベンチが市単位 RouterDb のみで実施」の解消）
**関連文書**:

- [Phase 1 設計書 §18.2](phase1_design.md)（都道府県単位未測定の課題記載）
- [Phase 3 ベンチマーク結果](phase3_benchmark_results.md)（v0.2、津島市 53k 頂点の基準値）
- [Phase 3 設計書 §9](phase3_design.md)（本ステップで肉付け対象）

---

## 1. 目的とゴール

**目的**: Phase 1 §18.2「性能ベンチが市単位 RouterDb のみで実施」の課題を解消し、都道府県単位（数十万〜数百万エッジ）での経路計算性能を実測する。

**対象地域**:

| 地域 | PBF ソース | 抽出 bbox (west,south,east,north) | 想定規模 |
|---|---|---|---|
| 愛知県全域 | chubu-latest.osm.pbf | `136.67,34.57,137.84,35.43` | 中規模（数十万エッジ） |
| 東京都全域 | kanto-latest.osm.pbf | `138.94,35.50,139.92,35.90` | 大規模・最密度（数百万エッジ） |

**Done 判定**:

1. 愛知県 + 東京都の `.odrg` が生成され、グラフ統計（頂点数 / エッジ数 / ファイルサイズ / 抽出時間）が記録される
2. 各 `.odrg` で 100 ペアの C0（制約なし）ベンチマークが BenchmarkDotNet で実測される
3. 結果が `phase3_benchmark_results.md` に追記される（§9 相当、都道府県単位セクション）
4. 設計書 `phase3_design.md` §9 が肉付けされる
5. OsmDotRoute `dotnet test` **672 件 pass** を維持

---

## 2. サブステップ

### 3G.1: PBF ダウンロード + .odrg 抽出 + 統計取得

1. Geofabrik から PBF ダウンロード:
   - `https://download.geofabrik.de/asia/japan/chubu-latest.osm.pbf`
   - `https://download.geofabrik.de/asia/japan/kanto-latest.osm.pbf`
2. Extractor で愛知県 `.odrg` 抽出
3. Extractor で東京都 `.odrg` 抽出
4. 各 `.odrg` のグラフ統計を取得（`RouterDb.LoadFromOdrg` → `GetStatistics()`）
5. 抽出時間を計測

### 3G.2: ベンチマーク実行 + 結果文書化

1. 各 `.odrg` で route-pairs.json を生成（`--generate-data` 相当、100 ペア）
2. C0（制約なし、Car）ベンチマークを BenchmarkDotNet DefaultJob で実測
3. `phase3_benchmark_results.md` に都道府県単位セクションを追記
4. Phase 3 計画書 §9 性能基準値表の「都道府県単位（C5）」行を更新

### 3G.3: 設計書 §9 肉付け + 3G 完了

1. 設計書 `phase3_design.md` §9 肉付け
2. 計画書 v0.2 bump
3. メモリ更新
4. `dotnet test` 672 件 pass 確認 → commit

---

## 3. リスク

| # | リスク | 対処 |
|---|---|---|
| 3G-R1 | 東京都 `.odrg` が巨大（数 GB）でメモリ不足 | MMF ゼロコピーのため理論上 OK、16 GB RAM で実測確認 |
| 3G-R2 | PBF ダウンロードに時間がかかる | chubu ≒ 200 MB、kanto ≒ 300 MB、合計 ≒ 500 MB |
| 3G-R3 | 抽出時間が長大 | 市単位 1.6 秒 → 都道府県は数十秒〜数分を見込み |
| 3G-R4 | C0 Mean が 100 ms を超過 | REQ-NFR-001 未達の場合は Phase 4+ で CH 検討の判断材料に |

---

## 4. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 | 2026-05-28 | 初版。愛知県 + 東京都の 2 地域。3 サブ分割 | Claude (Opus 4.7) |
