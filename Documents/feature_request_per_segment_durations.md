# 機能要望: 経路の区間別（Shape点別）累積所要時間の公開

**要望元**: 親プロジェクト「災害廃棄物処理シミュレーション」開発（Claude Code）
**作成日**: 2026-06-09
**宛先**: OsmDotRoute 開発エージェント
**関連親要件**: REQ-HAZ-011（移動時間ベース化）/ REQ-HAZ-012（VehicleProfile 対応）
**優先度提案**: P2（親プロジェクトの災害移動制約エリア機能の仕上げに必要）
**互換性**: 既存 `Route` への**プロパティ追加のみ**（加算的・非破壊。既存利用コードは無改修で動作）

---

## 1. 背景・ユースケース

親プロジェクトでは、移動困難エリア（`AddDifficultyArea`、冠水/液状化/土砂崩れ等）を通過するエージェントの**移動アニメーションを区間ごとの速度で描画**したい。具体的には「冠水ポリゴンに入った区間だけ、エージェントが目に見えて遅くなる」表現を実現したい。

現在の親側アニメーションは、`Route.Shape` と総移動時間を使い、**時間の経過に対して移動距離を線形補間**している（= 全区間が均一速度）。このため、移動困難による速度低下は `TotalDurationSec` 全体に均され、特定区間だけ遅くなる挙動が再現できない。

区間ごとの速度低下を可視化するには、**各 Shape 区間の所要時間**が必要。これは OsmDotRoute の Dijkstra が内部で算出している（エッジ単位の `EvaluateEdgeDurationSec` に難所 SpeedFactor が反映済み）が、現在は `Route.TotalDurationSec` に集約されて区間内訳が失われている。

### 利用側で再構築できない理由
- 区間別の速度低下を算出するには難所 `SpeedFactor`（プロファイル依存。car 冠水=0.3 等）が必要だが、`VehicleProfile.Evaluator` / `ProfileEvaluator.EvaluateDifficulty` は **internal** で親アセンブリから参照不可。
- `Route` は `TotalDistanceM` / `TotalDurationSec` / `Shape` のみで、区間内訳を持たない。
- → ライブラリ側で値を公開してもらうのが最も正確かつ自然（利用側での近似計算を避けられる）。

---

## 2. 要望内容（API 契約）

`OsmDotRoute.Route`（`src/OsmDotRoute/Route.cs`）に、`Shape` と整列した **Shape 点別の累積所要時間（秒）** を追加してほしい。

### 提案プロパティ
```csharp
/// <summary>
/// Shape 各点における起点からの累積所要時間（秒）。Shape と 1:1 で整列する。
/// CumulativeDurationsSec.Length == Shape.Length。
/// [0] == 0、[Length-1] == TotalDurationSec（誤差なく一致）。単調非減少。
/// 区間 i（Shape[i]→Shape[i+1]）の所要時間 = CumulativeDurationsSec.Span[i+1] - CumulativeDurationsSec.Span[i]。
/// 移動困難エリア（AddDifficultyArea）の速度低下が区間所要時間に反映される（エッジ単位の SpeedFactor 由来）。
/// </summary>
public ReadOnlyMemory<double> CumulativeDurationsSec { get; }
```

> 累積（cumulative）形を推奨する理由: 利用側アニメーションが「経過時間 → 位置」を求める際、累積配列に対する二分探索でそのまま補間でき、区間配列より扱いやすい（親側 `Shape` も累積距離で同様に補間している）。区間別配列が実装上自然なら、`Shape.Length - 1` 要素の区間別所要 `SegmentDurationsSec` でも可。どちらか一方で良い。

### 満たすべき不変条件（受け入れ基準）
1. **整列**: `CumulativeDurationsSec.Length == Shape.Length`
2. **端点**: `[0] == 0.0`、`[^1] == TotalDurationSec`（`TotalDurationSec` と完全一致。両者を同じ積算ロジックから導出すること）
3. **単調性**: 単調非減少（`[i] <= [i+1]`）
4. **困難反映**: 移動困難エリアを横断する経路では、当該エリア内の区間で累積の傾き（秒/m）が増大する（= 区間速度が低下）。`Impassable` 横断は経路に乗らないため対象外
5. **同一エッジ直通**（`SameEdge`）・**スナップ部分通過**（起点/終点のエッジ途中区間）でも矛盾なく算出されること

---

## 3. 実装ヒント（内部構造の所見）

> 実装方針は OsmDotRoute 開発エージェントの裁量。以下は親側調査での所見。

- `DijkstraEngine.Run`（`src/OsmDotRoute/Routing/DijkstraEngine.cs`）は頂点別の累積コスト `cost[]`（= 起点からの所要秒）を保持している。経路復元時、各通過頂点の累積所要が取り出せる。
- 起点側スナップ部分・終点側スナップ部分の所要は `EvaluateEdgePartialDurationSec`、中間エッジは `EvaluateEdgeDurationSec` で既に算出済み（いずれも難所 SpeedFactor 反映済み）。
- `RouteBuilder.Build`（`src/OsmDotRoute/Routing/RouteBuilder.cs`）は `Shape` を「起点スナップ点 → 各頂点 + エッジ中間シェイプ → 終点スナップ点」の順で構築している。**累積所要も同じ走査で並行構築**すれば `Shape` と整列する。
- **エッジ内の中間シェイプ点**: OsmDotRoute はエッジ単位で 1 つの所要を算出する（エッジ内速度は一定）。よってエッジ内の中間シェイプ点には、そのエッジの所要を**エッジ内距離按分**で割り付ければ正確（難所係数はエッジ単位なので按分しても整合）。
- `DijkstraResult`（同ファイル）に累積所要列または区間所要列を追加し、`RouteBuilder` 経由で `Route` へ渡す構成が素直。

---

## 4. 親プロジェクト側の利用計画（参考・本要望のスコープ外）

ライブラリ反映後、親側で以下を行う（本要望の完了後に親側で別途実施）:
1. `MapService.RouteResult` に区間別累積所要を載せ、各エージェント行動サービスがフロントへ送る `AgentRouteChanged` ペイロードに追加
2. フロント（`SimulationPage.tsx`）のアニメーションを**距離比例 → 時間比例（累積所要の二分探索）**へ変更し、移動困難区間でエージェントが減速する描画を実現
3. 親側の交通負荷係数（25km/h 校正）は親側で総時間に対して適用するため、本 API は**素の `TotalDurationSec` 整合の累積秒**を返すだけでよい（係数適用は利用側責務）

---

## 5. 要件 ID・記法の提案

OsmDotRoute 要件定義書の記法に合わせ、以下いずれかでの新規採番を提案（採番・確定はそちらの管理）:
- **REQ-FMT-006**（Format ジャンル）: 「経路出力型 `Route` に Shape 点別の累積所要時間（秒、`ReadOnlyMemory<double>`）を含めること」
- もしくは **REQ-RTE-010**（Routing ジャンル）相当

`REQ-FMT-001〜003`（総距離・総所要・Shape）の延長線上の追加であり、ジャンルとしては REQ-FMT が自然と考える。

---

## 6. 補足: 検討したが採用しなかった代替案

- **代替A: `VehicleProfile.GetDifficultySpeedFactor(string difficultyType)` を public 化**し、利用側が区間中点の難所判定（`RestrictedAreaService.ListAll` + 自前ポリゴン内外判定）で区間別重みを近似算出する案。→ 利用側計算が**近似**（道路種別速度差を無視、中点判定）になり、`TotalDurationSec` と一致しない。ライブラリが正確値を出せるなら本案が上位。
- **代替B: 現状維持（均一速度アニメ）**。→ ユーザー検証で「移動困難区間で遅くなって見えない」と指摘済みのため不採用。

以上。ご検討よろしくお願いします。区間別所要が取得できれば、移動困難エリアの速度低下を正確にアニメーションへ反映できます。
