# Phase 3 ステップ 3D: Bicycle / Truck プロファイル独自設計 計画書

**ステータス**: ドラフト v0.1（着手前事前調査 + ユーザー判断 Q1〜Q4 確定、2026-05-27）
**対応ステップ**: Phase 3 ステップ 3D（[Phase 3 実装計画書 §3.3 / §6](phase3_implementation_plan.md)）
**対応要件**: REQ-PRF-003（Bicycle プロファイル独自設計）、REQ-PRF-004（Truck = 10t、日本道路法ベース、独自設計）
**関連文書**:

- [Phase 3 実装計画書 §3.3 / §5.5-23, §5.5-24 / §6 / §8 R3](phase3_implementation_plan.md)
- [Phase 3 設計書 §5 Bicycle / Truck プロファイル独自設計](phase3_design.md)（本ステップで肉付け対象、現状「未記述」プレースホルダ）
- [Phase 3 ステップ 3A 計画書](phase3_step3A_plan.md)（Native 系統合先）
- [Phase 3 ステップ 3B 計画書](phase3_step3B_plan.md)（直前ステップ、完了済）
- 要件定義書 §5.3 §16（Truck 物理寸法は本ステップで確定、未確定範囲を解消）

---

## 1. 目的とゴール

**目的**: 親プロジェクト（災害廃棄物処理シミュレーション）が暗黙的に運用してきた「10t トラック」シナリオを `OsmDotRoute` ライブラリ側の標準プロファイルとして昇格させ、加えて Bicycle プロファイルを REQ-PRF-003 準拠で同梱する。Itinero / OSRM の Truck プロファイルを流用せず、日本道路法ベースの独自設計とすることで「日本国内 OSM ルーティング」用途の差別化を完成させる。

**Done 判定**:

1. `Profiles/bicycle.json` / `Profiles/truck.json` が新規追加され、`OsmDotRoute.csproj` の `EmbeddedResource` に登録される
2. `VehicleProfile.Bicycle` / `VehicleProfile.Truck` 静的プロパティが追加され、`Car` / `Pedestrian` と同じ Lazy ロード機構で利用可能
3. `JsonProfileDefinition` に `vehicleLimits` セクションが追加され、`maxWeightTon` / `maxHeightMeter` / `maxWidthMeter` の 3 数値（全て optional）を受ける
4. `ProfileEvaluator` が `osmTags["maxweight"]` / `maxheight` / `maxwidth` を数値 parse し、プロファイル制限を超過するエッジを `CanPass=false` で返す
5. `osmdotroute-extractor` の `--profiles` 引数で `car,pedestrian,bicycle,truck` の 4 列 bake が可能
6. **公開 API は不変死守**（`VehicleProfile.LoadFromJsonString` の挙動、`Evaluator.Evaluate` のシグネチャ、`car.json` / `pedestrian.json` の評価結果が一切変化しないこと）
7. Phase 1 既存 526 件 + Phase 3 累計 98 件 = **624 件 pass を全サブステップで維持**、3D 完了時は **+α 件**（Bicycle / vehicleLimits / Truck / Extractor の各サブで増分）
8. **Bicycle / Truck で経路長が pedestrian / car と異なる**ことを単体テスト化（津島市データではなく、合成タグ集合の評価結果ベースで検証）
9. 設計書 `phase3_design.md` §5 が 3D.4 完了時に肉付けされる

**REQ-PRF-003 / REQ-PRF-004 達成**: 本ステップ完了時点で要件定義書 §5.3 §16 の未確定範囲（Truck 物理寸法）が解消され、Phase 3 計画書 R3 リスク（Truck 仕様の親プロすり合わせ）も「本ステップで確定 → 3F 親プロ統合時に運用方針確認」へ繰り下げ可能になる。

---

## 2. 前提と現状

### 2.1 既存資産（3D 着手時点）

- Phase 3 ステップ 3B 全体完了（commit `cd9f435`、624 件 pass、計画書 v0.7）
- Phase 1 ステップ 5a で `VehicleProfile` の JSON 化が既に完了済：[src/OsmDotRoute/VehicleProfile.cs](../src/OsmDotRoute/VehicleProfile.cs)
- 同梱 JSON: [src/OsmDotRoute/Profiles/car.json](../src/OsmDotRoute/Profiles/car.json) / [pedestrian.json](../src/OsmDotRoute/Profiles/pedestrian.json)（埋込リソース、Lazy<T> でキャッシュ）
- DTO: [src/OsmDotRoute/Profiles/JsonProfileDefinition.cs](../src/OsmDotRoute/Profiles/JsonProfileDefinition.cs)（`Name` / `Highway` / `AccessTagKeys` / `AccessValueMap` / `Fallback` / `SpeedBounds` / `Difficulty` / `SpeedMultiplier` / etc.）
- 評価器: [src/OsmDotRoute/Profiles/ProfileEvaluator.cs](../src/OsmDotRoute/Profiles/ProfileEvaluator.cs)（OSM タグ辞書 → `EdgeEvaluation`、`maxspeed` parse / unit 変換含む）
- Extractor: [src/OsmDotRoute.Extractor/](../src/OsmDotRoute.Extractor/)（`ResolveProfile` は `car` / `pedestrian` のみ、`BakedProfileTable` は既に N プロファイル対応）
- Bake: [src/OsmDotRoute.Extractor/Pipeline/ProfileBaker.cs](../src/OsmDotRoute.Extractor/Pipeline/ProfileBaker.cs)（`IReadOnlyList<VehicleProfile>` を受けて bake、配列レイアウト 4 列拡張は内部実装ゼロ改修）
- Phase 1 既存テスト: [tests/OsmDotRoute.Tests/VehicleProfileTests.cs](../tests/OsmDotRoute.Tests/VehicleProfileTests.cs) 28 件 / [ProfileBakerTests.cs](../tests/OsmDotRoute.Tests/Extractor/ProfileBakerTests.cs) 11 件
- 親プロ調査（R3 リスク対策）: `WasteTransportAgentConfig.cs` は積載量 10t（REQ-PRF-004 と一致）/ 積み下ろし能力 / 廃棄物カテゴリのみ保持。**車両総重量 / 全高 / 全幅 などの OSM タグ関連寸法は親プロ側未定義**のため、本ステップで Truck 標準寸法を確定して問題ない

### 2.2 ユーザー判断確定（本ステップ着手前、2026-05-27）

- **Q1 = (A) JSON スキーマに `vehicleLimits` 追加**
  - `JsonProfileDefinition` に `vehicleLimits: { maxWeightTon, maxHeightMeter, maxWidthMeter }` を追加（全て optional）
  - `ProfileEvaluator` が `osmTags["maxweight"]` / `maxheight` / `maxwidth` を `double` に parse、単位サフィックス（"8 t" / "3.5 m"）対応
  - プロファイル制限を超過するエッジを `CanPass=false` で返す
  - Extractor / NativeRoadGraph / EdgeFlags への影響なし、bit 14/15 予約温存
- **Q2 = (A) Bicycle 15 km/h + Truck の `living_street` 等は `speedKmh` 低設定で回避**
  - Bicycle 平均速度 15 km/h（REQ-PRF-003、§5.5-23 確定）
  - Truck の `living_street` / `track` / `pedestrian` / `service` は `speedKmh=5〜20`、`access: "yes"` で許可するが Dijkstra コスト経由で自然に避ける
  - Phase 2 `EdgeFlags` (`IsLivingStreet` 等) の `ProfileEvaluator` 直接参照は **Phase 4+ に保留**（tag 辞書ベース評価モデルを維持）
- **Q3 = (A) Truck 物理寸法 = 車両総重量 20t / 全高 3.8m / 全幅 2.5m**（標準大型ダンプ）
  - 最大積載量 10t（REQ-PRF-004 で確定）+ 自重 10t 級 = 20t を採用
  - 日本道路法 車両制限令 一般条件（全幅 2.5m / 高 3.8m）に整合
  - JSON で外部化されるためユーザー独自プロファイルで上書き可能
- **Q4 = (A) 4 サブ分割**
  - 3D.1 Bicycle JSON + 静的プロパティ + 単体テスト
  - 3D.2 ProfileEvaluator vehicleLimits 拡張 + 単体テスト
  - 3D.3 Truck JSON + 静的プロパティ + 単体テスト
  - 3D.4 Extractor `--profiles` 4 プロファイル拡張 + 統合テスト + 設計書 §5 反映

### 2.3 設計上の歯止め

- **公開 API 不変**: `VehicleProfile` / `ProfileEvaluator` / `EdgeEvaluation` / `BakedProfileTable` の公開シグネチャは一切変更しない。`vehicleLimits` は新規 optional フィールドなので既存 JSON ロードに影響なし
- **`car.json` / `pedestrian.json` 評価結果不変**: 両者は `vehicleLimits` を持たないため、ProfileEvaluator 拡張による分岐は走らない。Phase 1 ステップ 5a で固めた回帰テスト全 28 件が pass 維持
- **Phase 2 .odrg フォーマット不変**: EdgeFlags の bit 拡張なし、`BakedProfileTable` は既にプロファイル数可変、`OdrgWriter` 改修なし

---

## 3. アーキテクチャ概要

### 3.1 vehicleLimits 評価ロジック

```text
ProfileEvaluator.Evaluate(osmTags):
  1. highway 別ルール参照 (Phase 1 既存)
  2. accessTagKeys 評価 (Phase 1 既存)
  3. maxspeed パース (Phase 1 既存)
  4. ★ vehicleLimits 評価 (3D.2 新規):
       if (_vehicleLimits is not null):
         if (TryParseTagDouble(osmTags["maxweight"], "t", out var w) && w < _vehicleLimits.MaxWeightTon)
           accessAllow = false; isHardDeny = true;
         (maxheight / maxwidth 同様)
  5. speedMultiplier / 速度クランプ / oneway (Phase 1 既存)
```

**配置**: `ProfileEvaluator` 内 private メソッド `TryParseTagDouble(string raw, string defaultUnit, out double value)` を追加。`TryParseMaxspeed` と同形態（数値部 + 単位サフィックス）。

**hard-deny セマンティクス**: `vehicleLimits` 超過は **highway 別 access=no と同等の hard-deny** として扱い、`access=destination` 等のタグ上書きを許可しない。これは物理制限が法令に関係なく適用される現実に合わせる。

### 3.2 Bicycle プロファイル設計概要

| 項目 | 値 | 出典 |
| --- | --- | --- |
| name | "bicycle" | — |
| vehicleType | "bicycle" | — |
| speedMultiplier | 1.0 | Pedestrian と同等（Itinero Fastest 補正 0.75 は自動車向け） |
| accessTagKeys | `["access", "vehicle", "bicycle"]` | Bicycle 優先 |
| speedBounds | minKmh: 5, maxKmh: 25 | 平均 15 km/h を中心に |
| ignoreOneway | false | 自転車道は方向制限あり |
| 主な通行可エッジ | cycleway, path, footway, residential, service, secondary, tertiary 等（15 km/h） | REQ-PRF-003 |
| 主な通行不可エッジ | motorway, motorway_link, trunk, trunk_link | REQ-PRF-003 |
| 難所プロファイル | flooding 0.5 / landslide 0.0 (canPass=false) / snow 0.3 / ice 0.2 など | car / pedestrian を参考に Bicycle 向け調整 |

### 3.3 Truck プロファイル設計概要

| 項目 | 値 | 出典 |
| --- | --- | --- |
| name | "truck" | REQ-PRF-004 |
| vehicleType | "hgv" | OSM タグ慣習 |
| speedMultiplier | 0.75 | car と同等の Fastest 補正 |
| accessTagKeys | `["access", "vehicle", "motor_vehicle", "hgv"]` | hgv 末尾優先 |
| speedBounds | minKmh: 20, maxKmh: 90 | 大型車最高速度 80 km/h を考慮 |
| ignoreOneway | false | 必須 |
| vehicleLimits | maxWeightTon: 20, maxHeightMeter: 3.8, maxWidthMeter: 2.5 | Q3 確定（標準大型ダンプ） |
| 主な通行可エッジ | motorway〜tertiary（speedKmh は car と同等、ただし上限 90） | — |
| Truck 回避エッジ | living_street: 5 km/h / track: 5 km/h / service: 20 km/h / pedestrian: 5 km/h（all access=yes、Dijkstra で自然回避） | Q2 確定 |
| 通行不可エッジ | footway, path, cycleway, steps, bridleway | 物理的に通行不可 |
| 難所プロファイル | flooding 0.2 / landslide 0.0 / liquefaction 0.3（Truck は浸水・液状化に弱い） | car より厳しめ |

### 3.4 Extractor 改修概要

- `Program.cs:ResolveProfile` に `bicycle` / `truck` ケース追加
- `--profiles` 引数の defaultValue は `["car", "pedestrian"]` のまま（後方互換）。`--profiles car,pedestrian,bicycle,truck` で 4 列 bake 可能
- `BakedProfileTable` / `ProfileBaker` / `OdrgWriter` の改修は**一切不要**（既に N プロファイル対応）

---

## 4. サブステップ詳細

### 4.1 サブステップ 3D.1: Bicycle プロファイル

#### 4.1.1 事前調査結果

- `car.json` / `pedestrian.json` の構造完全把握済（§2.1）
- `VehicleProfile.Bicycle` 静的プロパティ追加は `Car` / `Pedestrian` の Lazy<T> パターンをそのまま踏襲可能
- `OsmDotRoute.csproj` の `EmbeddedResource` 登録パターンは car/pedestrian で確立済（要確認）
- `bicycle` プロファイルは `vehicleLimits` を必要としないため、3D.2 の前に独立して着手可能

#### 4.1.2 採用設計

- 新規ファイル `src/OsmDotRoute/Profiles/bicycle.json`（§3.2 の値で構成）
- `VehicleProfile.cs` に静的プロパティ `Bicycle` 追加、`BicycleLazy` を `LoadEmbedded("bicycle.json")` で初期化
- `OsmDotRoute.csproj` に `<EmbeddedResource Include="Profiles\bicycle.json" />` 追加（既存パターン参照）
- 新規テストファイル `tests/OsmDotRoute.Tests/Profiles/BicycleProfileTests.cs`（または `VehicleProfileTests.cs` 内に追加、3D.3 とまとめる場合は別ファイル推奨）

#### 4.1.3 Done 基準

- `VehicleProfile.Bicycle.Name == "bicycle"`
- `Bicycle.Evaluator.Evaluate(("highway", "cycleway")).CanPass == true && SpeedKmh == 15`
- `Bicycle.Evaluator.Evaluate(("highway", "motorway")).CanPass == false`
- `Bicycle.Evaluator.Evaluate(("highway", "footway")).CanPass == true`（歩道並み通行）
- `Bicycle.Evaluator.Evaluate(("highway", "primary"), ("bicycle", "no")).CanPass == false`
- `Bicycle.Evaluator.EvaluateDifficulty(DifficultyTypes.Flooding)` が 0〜1 範囲で正常返却
- `dotnet test` 全 pass、テスト件数 624 + N 件（N は新規テスト件数、目安 8 件程度）

---

### 4.2 サブステップ 3D.2: ProfileEvaluator vehicleLimits 拡張

#### 4.2.1 事前調査結果

- `ProfileEvaluator.Evaluate` の現状ステップ順は「highway 別 → access → 通行不可早期 return → maxspeed → speedMultiplier → clamp → oneway」（[ProfileEvaluator.cs:62-128](../src/OsmDotRoute/Profiles/ProfileEvaluator.cs#L62-L128)）
- `TryParseMaxspeed` 関数（[ProfileEvaluator.cs:164-196](../src/OsmDotRoute/Profiles/ProfileEvaluator.cs#L164-L196)）が数値 + 単位サフィックスのパース手本になる
- `JsonProfileDefinition` は `vehicleLimits` プロパティを持たないため null チェックで後方互換性確保
- OSM タグの `maxweight` / `maxheight` / `maxwidth` の単位は単位省略時 "t" / "m" / "m" がデフォルト（OSM Wiki）
- `car.json` / `pedestrian.json` は `vehicleLimits` を持たないため、新規 ProfileEvaluator ロジックは Truck (3D.3) で初めて活性化

#### 4.2.2 採用設計

- `JsonProfileDefinition` に追加:
  ```csharp
  [JsonPropertyName("vehicleLimits")]
  public JsonVehicleLimits? VehicleLimits { get; set; }
  ```
- 新規 DTO `JsonVehicleLimits`:
  ```csharp
  internal sealed class JsonVehicleLimits
  {
      [JsonPropertyName("maxWeightTon")]   public double? MaxWeightTon { get; set; }
      [JsonPropertyName("maxHeightMeter")] public double? MaxHeightMeter { get; set; }
      [JsonPropertyName("maxWidthMeter")]  public double? MaxWidthMeter { get; set; }
  }
  ```
- `ProfileEvaluator` に private フィールド `_vehicleLimits`（参照保持）+ private メソッド `EvaluateVehicleLimits(osmTags, out bool hardDeny)` を追加
- 評価順は §3.1 の通り「access タグ上書き → maxspeed」の間に `vehicleLimits` 評価を挿入（あるいは access タグ評価の直後）。**hard-deny として後段の上書きを許可しない**
- 単位パース: `TryParseLimitValue(string raw, out double value)`（"8" / "8 t" / "3.5 m" / "2.5" 等を受け、単位部は無視して数値のみ抽出。`maxspeed` のような unit 変換は不要 = OSM の重さ/高さ/幅は単一単位前提）
- `ValidateAndCompile` で `vehicleLimits` 数値の妥当性検証（負数禁止等）

#### 4.2.3 Done 基準

- 新規テスト（`VehicleLimitsEvaluatorTests.cs` 等）:
  - `vehicleLimits` 未定義のプロファイル（既存 car/pedestrian）で評価結果が一切変化しない（回帰確認）
  - `maxWeightTon: 20` 設定下、`maxweight=8` → CanPass=false
  - `maxWeightTon: 20` 設定下、`maxweight=25` → CanPass=true
  - `maxHeightMeter: 3.8` 設定下、`maxheight=3.5` → CanPass=false
  - `maxHeightMeter: 3.8` 設定下、`maxheight="4.0 m"` → CanPass=true
  - `maxweight` が `"signals"` / 不正値 → CanPass 判定は他要因のみで決定（vehicleLimits は影響なし）
  - hard-deny セマンティクス: `access=destination` で hard-deny 解除されない
  - 不正 `vehicleLimits`（負数）で `InvalidProfileException` 発生
- 既存 `VehicleProfileTests.cs` 28 件 pass 維持（`vehicleLimits` 未定義の場合は完全に no-op）
- `dotnet test` 全 pass

---

### 4.3 サブステップ 3D.3: Truck プロファイル

#### 4.3.1 事前調査結果

- 3D.2 の `vehicleLimits` 評価が稼働済 → Truck で利用可能
- `accessTagKeys: ["access", "vehicle", "motor_vehicle", "hgv"]` で `hgv=no` のエッジを通行不可化（既存 `accessValueMap` 仕組みで対応）
- Truck の `living_street` 回避は `speedKmh=5` で実装（Q2 (A) 確定、Dijkstra で自然回避）
- Itinero / OSRM の Truck プロファイルは流用しない（REQ-PRF-004、§3.3）

#### 4.3.2 採用設計

- 新規ファイル `src/OsmDotRoute/Profiles/truck.json`（§3.3 の値で構成）
- `VehicleProfile.cs` に静的プロパティ `Truck` 追加、`TruckLazy` を `LoadEmbedded("truck.json")` で初期化
- `OsmDotRoute.csproj` に `<EmbeddedResource Include="Profiles\truck.json" />` 追加
- 新規テストファイル `tests/OsmDotRoute.Tests/Profiles/TruckProfileTests.cs`

#### 4.3.3 Done 基準

- `VehicleProfile.Truck.Name == "truck"`
- `Truck.Evaluator.Evaluate(("highway", "motorway")).CanPass == true`
- `Truck.Evaluator.Evaluate(("highway", "footway")).CanPass == false`
- `Truck.Evaluator.Evaluate(("highway", "living_street")).CanPass == true && SpeedKmh <= 10`（回避用低速）
- `Truck.Evaluator.Evaluate(("highway", "primary"), ("hgv", "no")).CanPass == false`
- `Truck.Evaluator.Evaluate(("highway", "primary"), ("maxweight", "8")).CanPass == false`（vehicleLimits 20t > 8t）
- `Truck.Evaluator.Evaluate(("highway", "primary"), ("maxheight", "3.0 m")).CanPass == false`（vehicleLimits 3.8m > 3.0m）
- `Truck.Evaluator.Evaluate(("highway", "primary"), ("maxwidth", "2.0")).CanPass == false`（vehicleLimits 2.5m > 2.0m）
- `Truck.Evaluator.EvaluateDifficulty(DifficultyTypes.Flooding).SpeedFactor < 0.5`（Truck は浸水に弱い）
- `dotnet test` 全 pass、テスト件数 +N（目安 10 件程度）

---

### 4.4 サブステップ 3D.4: Extractor + 統合テスト + 設計書反映

#### 4.4.1 事前調査結果

- `Program.cs:ResolveProfile` ([src/OsmDotRoute.Extractor/Program.cs:116-122](../src/OsmDotRoute.Extractor/Program.cs#L116-L122)) は switch 式で `car` / `pedestrian` のみ対応
- `BakedProfileTable` / `ProfileBaker` / `OdrgWriter` は既に N プロファイル対応済（[BakedProfileTable.cs](../src/OsmDotRoute.Extractor/Pipeline/BakedProfileTable.cs)）
- 既存 `ProfileBakerTests.cs` で `Build` を 2 プロファイル × 2 エッジで検証済 → 4 プロファイル × N エッジに拡張

#### 4.4.2 採用設計

- `Program.cs:ResolveProfile` に追加:
  ```csharp
  "bicycle" => VehicleProfile.Bicycle,
  "truck" => VehicleProfile.Truck,
  ```
- エラーメッセージ更新: "'car' / 'pedestrian' / 'bicycle' / 'truck' のみ対応"
- 新規テスト（`ExtractorMultiProfileTests.cs` または既存 `ProfileBakerTests.cs` 拡張）:
  - 4 プロファイル × 合成エッジ集合で `BakedProfileTable` 構築 → 各セル評価結果検証
  - 例: motorway エッジ × {car: true, pedestrian: false, bicycle: false, truck: true}
  - 例: cycleway エッジ × {car: false, pedestrian: true, bicycle: true, truck: false}
  - 例: maxweight=8 タグ付き primary エッジ × {car: true, pedestrian: true, bicycle: true, truck: false}
- 設計書 [`phase3_design.md`](phase3_design.md) §5 を肉付け:
  - §5.1 Bicycle プロファイル設計（§3.2 表 + 設計思想）
  - §5.2 Truck プロファイル設計（§3.3 表 + 日本道路法ベース根拠）
  - §5.3 vehicleLimits 評価ロジック（§3.1 図 + ProfileEvaluator 拡張箇所）
  - §5.4 Extractor `--profiles` 4 プロファイル拡張
  - §5.5 検証方法（3D.1〜3D.4 で導入したテスト件数）

#### 4.4.3 Done 基準

- `osmdotroute-extractor extract --input ... --output ... --bbox ... --profiles car,pedestrian,bicycle,truck` がエラーなく `.odrg` を生成（実 PBF での動作確認は 3F 統合時に実施、3D ではコマンド受付確認のみ）
- 統合テスト: 合成エッジ集合で 4 プロファイル bake 結果が Truck / Bicycle で car / pedestrian と異なる経路特性を示す（具体的なセル値検証）
- `dotnet test` 全 pass、テスト件数 +N（目安 5 件程度）
- `phase3_design.md` §5 が記述充足、3D サブステップ毎の変更内容が反映

---

## 5. リスクと対処

| # | リスク | 影響 | 対処方針 |
| --- | --- | --- | --- |
| T1 | OSM タグ `maxweight=*` の表記揺れ（"8" / "8 t" / "8.0t" / "8000 kg"）で誤判定 | Truck で通行可能なエッジを誤って不可と判定、または逆 | 3D.2 で表記揺れ網羅テスト追加。kg 単位（"8000 kg"）は今回スコープ外として README に明記、3D.2 テストで失敗表記を確認しつつ「未対応値は vehicleLimits 制限を発火しない（=通行可）」セマンティクスを確定 |
| T2 | Bicycle プロファイルの highway 別速度（cycleway 15 km/h / residential 15 km/h など）が pedestrian と差別化されず、経路探索結果が同一になる | REQ-PRF-003 未達、3D.4 統合テストで失敗 | 3D.1 着手時に Pedestrian (4 km/h) との `speedKmh` 差別化を明示。`primary` / `secondary` などの主要道路を Bicycle は 15 km/h で通行可（Pedestrian も 4 km/h で通行可だが速度差で Dijkstra 結果差別化） |
| T3 | Truck で `living_street` を `speedKmh=5` で許可した結果、Truck が `motorway` 経路よりも `living_street` 経路を選ぶ（Dijkstra 重みは時間ベース） | Truck が住宅街を経由する不適切な経路、親プロ実用性問題 | 3D.3 で `living_street` < `track` < `service` < 通常道路 の速度階層を確認、合成テストで `motorway → primary → living_street` の段階的経路コスト試算。回避が不十分なら `speedKmh` を 3 km/h まで下げる調整余地確保 |
| T4 | Truck の `vehicleLimits` 評価が `access=destination` 等のソフト許可タグでオーバーライドされてしまう | 物理制限が法的タグで上書きされる仕様矛盾 | 3D.2 で hard-deny 実装 + テスト網羅。`vehicleLimits` 超過は `highway: access=no` と同等の hard-deny として扱う（既存 ProfileEvaluator の `isHardDeny` 機構を流用） |
| T5 | 親プロ統合（3F）時に Truck プロファイル切替の `MapService` API が不足 | 親プロ側で Truck 利用できず 3F 遅延 | 3D 範囲外。3F 着手時に親プロ `MapService` の `VehicleProfile` 受渡し API を確認。`OsmDotRoute.MapService` 側の API は既に `VehicleProfile` パラメータを受ける設計のため、親プロ呼出側変更のみで対応可能（要確認） |

---

## 6. テスト設計サマリ

| サブ | テストファイル | 主要観点 | 想定件数 |
| --- | --- | --- | --- |
| 3D.1 | `BicycleProfileTests.cs`（新規） | Bicycle 同梱ロード / cycleway 通行可 / motorway 通行不可 / footway 通行可 / bicycle=no 上書き / 難所評価 | 約 8 件 |
| 3D.2 | `VehicleLimitsEvaluatorTests.cs`（新規） | vehicleLimits 未定義回帰 / maxweight 超過 / maxheight 超過 / maxwidth 超過 / 単位サフィックス / hard-deny / 不正値スルー / バリデーション | 約 8 件 |
| 3D.3 | `TruckProfileTests.cs`（新規） | Truck 同梱ロード / motorway 通行可 / footway 通行不可 / living_street 低速 / hgv=no / maxweight 違反 / maxheight 違反 / maxwidth 違反 / 難所評価 | 約 10 件 |
| 3D.4 | `ExtractorMultiProfileTests.cs`（新規または既存拡張） | 4 プロファイル bake / 各セル評価値 / Truck × maxweight タグエッジ | 約 5 件 |
| 既存 | `VehicleProfileTests.cs` / `ProfileBakerTests.cs` | Phase 1 同等動作維持（vehicleLimits 未定義時の no-op） | 既存 28 + 11 件 pass 維持 |

**累計目標**: 3D 完了時 **624 + 約 31 = 約 655 件 pass**（実装時に微調整）

---

## 7. 着手前確認

- ✅ Phase 3 ステップ 3B 完了（commit `cd9f435`、624 件 pass）
- ✅ `dotnet test` 624 件 pass 再確認済（2026-05-27、本計画書起草時）
- ✅ 既存 `VehicleProfile` / `ProfileEvaluator` / Extractor 構造把握済
- ✅ 親プロ（災害廃棄物処理シミュレーション）の Truck シナリオ調査済（積載量 10t のみ、物理寸法未定義）
- ✅ ユーザー判断 Q1〜Q4 確定（§2.2、2026-05-27）
- ⏳ 本計画書 v0.1 のユーザー承認 → 承認後 commit → 3D.1 着手

**ユーザー承認後の進め方**: 3A / 3B と同様、各サブステップ着手前に必要に応じて事前調査と判断を実施し、計画書を v0.2 / v0.3 と更新する。サブステップ完了毎に commit し、3D.4 完了時に最終 commit で設計書 §5 を肉付けする。

---

## 8. 改訂履歴

| 版 | 日付 | 内容 |
| --- | --- | --- |
| v0.1 | 2026-05-27 | 初版起草。3B 完了後の着手前事前調査（既存 VehicleProfile/ProfileEvaluator/Extractor 構造調査 + 親プロ Truck シナリオ調査）+ ユーザー判断 Q1〜Q4（vehicleLimits / Bicycle 15km/h+Truck 速度回避 / Truck 寸法 20t-3.8m-2.5m / 4 サブ分割）反映。サブステップ 3D.1〜3D.4 詳細記述、リスク T1〜T5、テスト設計サマリ、設計書 §5 反映方針を確定。 |
