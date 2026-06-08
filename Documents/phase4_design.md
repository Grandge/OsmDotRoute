# OsmDotRoute Phase 4 設計書

**バージョン**: ユーザー採番（未採番、Ver 1.1.0 = 親プロFB 追補ぶん）
**作成日**: 2026-06-03
**最終更新**: 2026-06-09（親プロFB 追補 §3 追加）
**ステータス**: プロファイル追加（救急車 / 消防車 / 災害用車両 ＋ Extractor 外部 JSON プロファイル対応）完了。**マルチプラットフォーム対応も完了**（2026-06-03、macOS ARM64 / Linux x64 で 753 pass）。**親プロFB 追補（REQ-FMT-006 = Route.CumulativeDurationsSec）完了**（2026-06-09、Ver 1.1.0、全 761 pass）。マルチプラットフォーム対応の計画・設計記録は別書 [phase4_multiplatform_plan.md](phase4_multiplatform_plan.md) で扱う
**対象**: OsmDotRoute Phase 4 のうち**プロファイル追加**と**親プロFB 追補**の設計記録（REQ-PRF-005 = 救急車 `ambulance` / 消防車 `fire_engine`、REQ-PRF-006 = 災害用車両 `disaster`、＋ユーザー定義プロファイルの bake 経路拡張、REQ-FMT-006 = Route 区間別累積所要秒）
**関連ドキュメント**:

- [要件定義書](requirement_definition.md)（REQ-PRF-005 / REQ-PRF-006）
- [Phase 4 実装計画書（プロファイル追加）](phase4_implementation_plan.md)（v0.2、2026-06-03 確定）
- [Phase 4 プロファイル調査結果](phase4_profile_research.md)（設計根拠の Web 調査）
- [Phase 3 設計書](phase3_design.md)（§5「Bicycle / Truck プロファイル独自設計」が本章の出発点）
- [プロファイル活用ガイド](profile_guide.md) / [Profile Guide (EN)](profile_guide.en.md)（利用者向け解説、本章の外部仕様の利用手順）
- [使い方ガイド](usage_guide.md)（§4 抽出 / §6 プロファイル指定）

---

## 0. 本書の目的と更新ルール

### 0.1 目的

本書は **OsmDotRoute Phase 4 で「何を、なぜ、どう実装したか」を後から把握できる記録** を残す。実装計画書（[`phase4_implementation_plan.md`](phase4_implementation_plan.md)）が「これから何をやるか」を、本書が「実際にどう作ったか」を保持する。Phase 3 設計書 §5「Bicycle / Truck プロファイル独自設計」が本章の出発点。

Phase 4 のスコープは 2026-06-02 ユーザー決定で **(1) プロファイル追加**、**(2) マルチプラットフォーム対応** の 2 項目に限定されている。本書は (1) の設計記録に専念し、(2) は別計画書・別設計記録で扱う。

### 0.2 更新ルール

各実装ステップ完了時に本書の該当章を更新する（Phase 1〜3 と同方針、メモリ [[feedback_design_doc_per_step]]）。各章は Phase 3 設計書 §0.4 と同じテンプレート（意図 / 採用設計 / 設計判断の根拠 / トレードオフ・制約 / 検証方法 / 実装メモ）で記述する。工数・日数見積もりは記載しない（[[feedback_no_effort_estimates]]）。バージョン番号はユーザー採番。

### 0.3 章とステップの対応

| 章 | 対応ステップ | 状態 |
| --- | --- | --- |
| 1. Phase 4 概要（プロファイル追加） | 全ステップ通底 | 記述済 |
| 2. 救急車 / 消防車 / 災害用プロファイルと外部 JSON bake 対応 | Step 1〜4 | **肉付け完了**（2026-06-03） |
| 3. 親プロFB 追補: Route.CumulativeDurationsSec（REQ-FMT-006） | 単発 Step | **肉付け完了**（2026-06-09、Ver 1.1.0） |
| 4. 改訂履歴 | 各ステップ完了時 | 初版 |

> Step 5（利用者向け解説ドキュメント）/ Step 6（設計書・要件反映）は成果物が本書および [profile_guide.md](profile_guide.md) / [requirement_definition.md](requirement_definition.md) 自体であり、§2 にその位置付けを記す。

---

## 1. Phase 4 概要（プロファイル追加）

### 1.1 ゴール

親プロジェクト「災害廃棄物処理シミュレーション」の災害ユースケースを念頭に、Phase 3 までの 4 プロファイル（car / pedestrian / bicycle / truck）へ **緊急・災害系の 3 プロファイル**を追加し、**ユーザー定義プロファイルを `.odrg` に bake できる経路**を CLI に開く。

- REQ-PRF-005: 緊急車両。**救急車 `ambulance`（小型）と消防車 `fire_engine`（大型）を別プロファイルに分割**して提供（2026-06-03 ユーザー決定、ID は分割せず 1 要件 = 2 プロファイル）。
- REQ-PRF-006: 災害用車両 `disaster`。**難所耐性中心**（vehicleLimits は truck 同等）。
- ユーザー定義プロファイル拡充: Extractor CLI `--profiles` に外部 JSON ファイルパスを受理させ、REQ-PRF-009（任意プロファイル読込）を **bake 経路**へ拡張（Phase 3 までは bake 可能なのは組込み名のみだった）。

### 1.2 採用アプローチ（確定済み、計画書 v0.2）

Phase 3 §5 で確立した「JSON プロファイル + `ProfileEvaluator` + `BakedProfileTable`（N プロファイル可変）」基盤をそのまま活用し、**新規の評価機構は追加しない**。3 プロファイルは既存スキーマ（`accessTagKeys` / `highway` / `vehicleLimits` / `difficulty`）の値設計のみで表現する。Extractor の外部ファイル対応は `ResolveProfile` 相当の名前解決を [`ProfileResolver`](../src/OsmDotRoute.Extractor/ProfileResolver.cs) に切り出して拡張する。

---

## 2. 救急車 / 消防車 / 災害用プロファイルと外部 JSON bake 対応

**対応ステップ**: Step 1（ambulance）/ Step 2（fire_engine）/ Step 3（disaster）/ Step 4（Extractor 外部 JSON 対応）
**対応要件**: REQ-PRF-005（ambulance + fire_engine）、REQ-PRF-006（disaster）、REQ-PRF-009（外部プロファイルを bake 経路へ拡張）
**Phase 3 申し送り**: 設計書 §5（Bicycle / Truck プロファイル独自設計、`vehicleLimits` 機構）
**実装日**: 2026-06-03
**実装バージョン**: ユーザー採番
**主要ファイル**:

- [`src/OsmDotRoute/Profiles/ambulance.json`](../src/OsmDotRoute/Profiles/ambulance.json)（埋込、小型 4.0t / 2.6m / 2.0m、emergency / 歩道低速通行 / ignoreOneway）
- [`src/OsmDotRoute/Profiles/fire_engine.json`](../src/OsmDotRoute/Profiles/fire_engine.json)（埋込、大型 8.0t / 2.9m / 2.1m）
- [`src/OsmDotRoute/Profiles/disaster.json`](../src/OsmDotRoute/Profiles/disaster.json)（埋込、truck 同等寸法 + 難所耐性強化）
- [`src/OsmDotRoute/VehicleProfile.cs`](../src/OsmDotRoute/VehicleProfile.cs)（`Ambulance` / `FireEngine` / `Disaster` 静的プロパティ追加、Lazy<T> パターン踏襲）
- [`src/OsmDotRoute/OsmDotRoute.csproj`](../src/OsmDotRoute/OsmDotRoute.csproj)（3 JSON を `EmbeddedResource` 登録）
- [`src/OsmDotRoute.Extractor/ProfileResolver.cs`](../src/OsmDotRoute.Extractor/ProfileResolver.cs)（新規。組込み 7 名 + 外部 JSON ファイルパス解決）
- [`src/OsmDotRoute.Extractor/Program.cs`](../src/OsmDotRoute.Extractor/Program.cs)（`ProfileResolver.Resolve` 適用、プロファイル名一意性チェック、メタ JSON に実プロファイル名を記録）

### 2.1 意図

REQ-PRF-005 / REQ-PRF-006 を Phase 4 で同梱する。Phase 3 までの car / pedestrian / bicycle / truck では表現できなかった **緊急走行特例（一方通行逆走・歩道通行）と災害時の難所耐性**を、新規コードを足さず JSON の値設計だけで実現することを狙う。

加えて、Phase 3 §5.4 および [使い方ガイド](usage_guide.md) で「`.odrg` に bake できるのは組込み名のみ（外部プロファイル対応は Phase 4+ の TODO）」と申し送られていた制約を、Extractor の外部 JSON 対応（Step 4）で解消する。これにより REQ-PRF-009（任意 JSON プロファイル読込）がランタイムだけでなく抽出（bake）でも有効になり、ユーザー独自プロファイルで `.odrg` を焼けるようになる。

### 2.2 採用設計

#### 2.2.1 3 プロファイルの設計値

| 観点 | `ambulance`（救急車） | `fire_engine`（消防車） | `disaster`（災害用車両） |
| --- | --- | --- | --- |
| ベース | car 相当 | truck 相当 | truck 相当 |
| `vehicleType` | motor_vehicle | motor_vehicle | motor_vehicle |
| `accessTagKeys` | `access, vehicle, motor_vehicle, emergency` | `access, vehicle, motor_vehicle, hgv, emergency` | `access, vehicle, motor_vehicle, hgv, emergency` |
| `ignoreOneway` | **true**（逆走可） | **true**（逆走可） | **false**（災害規制は上位レイヤー責務） |
| footway / path / pedestrian | access **yes**（10 km/h） | access **yes**（5 km/h 徐行） | access **no** |
| `vehicleLimits` | 4.0t / 2.6m / 2.0m（高規格救急車） | 8.0t / 2.9m / 2.1m（水槽付消防ポンプ車） | 20.0t / 3.8m / 2.5m（truck 同等） |
| 難所耐性（小さいほど回避） | car より高め | ambulance より控えめ（大型ゆえ冠水・渋滞に弱い） | **最強化**（flooding 0.5 / obstacle 0.7 等） |
| `landslide` | canPass=false | canPass=false | canPass=false |
| `speedMultiplier` | 0.75 | 0.75 | 0.75 |

各プロファイルの全フィールドは JSON ファイルを参照（埋込リソース）。難所 8 種は全プロファイルで定義し、`landslide` は土砂崩れの物理的不可をもって全車 `canPass=false` を維持する。

#### 2.2.2 「歩道を通す」設計（emergency キー + hard-deny 回避）

救急車・消防車が footway / path / pedestrian を通れるようにするため、Phase 3 §5.2.4 の評価モデル（`highway[type].access` ∧ accessTagKeys 評価）に対し次の 2 点で許可側に倒す。新規の評価機構は不要で、JSON 値設計のみで成立する。

1. `highway` 側で footway / path / pedestrian を `access: "yes"`（hard-deny を外す）+ 低速 `speedKmh`（ambulance 10 / fire_engine 5）。
2. `accessTagKeys` 末尾に `emergency` を追加し、`emergency=yes/designated` を `accessValueMap` の `allow` で拾う（配列の後ろほど優先）。

`vehicleLimits` は Phase 3 の hard-deny 等価セマンティクス（accessTagKeys 評価後に挿入、`maxweight` 等の物理超過は法令タグ上書きより優先）をそのまま適用。消防車（幅 2.1m）の footway 通行は車幅との非現実性が懸念されるため `speedKmh=5` で実質回避させる方針とした（§2.4 トレードオフ参照）。

#### 2.2.3 Extractor 外部 JSON プロファイル対応（[ProfileResolver.cs](../src/OsmDotRoute.Extractor/ProfileResolver.cs)）

Phase 3 まで `Program.cs` の `ResolveProfile`（組込み名固定 switch）が担っていた解決を [`ProfileResolver`](../src/OsmDotRoute.Extractor/ProfileResolver.cs) に切り出し、次の解決順とした。

```text
ProfileResolver.Resolve(nameOrPath):
  1. 組込み名（car / pedestrian / bicycle / truck / ambulance / fire_engine / disaster）に一致 → 該当 VehicleProfile
  2. File.Exists(nameOrPath) → VehicleProfile.LoadFromJsonFile（ユーザー定義、REQ-PRF-009）
  3. いずれでもない → ArgumentException（組込み名一覧 + パス指定を案内）
```

- `--profiles car,ambulance,.\my.json` のように **組込み名と外部ファイルパスを混在指定可**。
- `.odrg` の BAKED_PROFILE スロットは name で解決されるため、[`Program.cs`](../src/OsmDotRoute.Extractor/Program.cs) で**プロファイル名の一意性チェック**を追加（重複名でエラー終了、計画書 R5）。
- メタデータ JSON には**ファイルパスではなく実プロファイル名（JSON の `name`）**を記録し、`.odrg` の BAKED_PROFILE 名テーブルおよびランタイムのスロット解決と一致させる。
- 外部 JSON が不正な場合は `InvalidProfileException` を `ArgumentException` に包んで CLI がエラー終了する。

### 2.3 設計判断の根拠

2026-06-03 にユーザー確認した設計判断（計画書 §0.3、Q1〜Q4）：

| ID | 論点 | 確定 | 理由 |
| --- | --- | --- | --- |
| Q1 | emergency の車両寸法 | **救急車・消防車を別プロファイルに分割** | 寸法と通行範囲の差が大きい（救急は小型で歩道侵入が現実的、消防は大型）。単一 emergency では中庸な値となり両車の特性を表現できない。ID は分割せず REQ-PRF-005 を両プロファイルに充当 |
| Q2 | 通行範囲 | **歩道（footway / path / pedestrian）も限定速度で通行可** | 緊急車両の現場接近を表現。hard-deny を外し emergency キーで許可、速度は低く抑えて自然回避とのバランスを取る |
| Q3 | disaster の主軸 | **難所耐性中心、vehicleLimits は truck 同等** | disaster の差別化は「難所を通れる車両特性」であり寸法緩和ではない。災害規制区間の動的指定は上位レイヤー（RestrictedArea 付け外し）の責務とし、プロファイルは耐性のみ担当 |
| Q4 | CLI 外部プロファイル | **外部 JSON ファイル対応を実装** | REQ-PRF-009 をランタイムから bake 経路へ拡張。ユーザー独自プロファイルで `.odrg` を焼けるようにし、Phase 3 §5.4 の申し送り TODO を解消 |

`ignoreOneway` の車種差：

- 救急車・消防車 = **true**。道路交通法の緊急自動車の特例（一方通行の逆行等）を踏まえる。
- 災害用 = **false**。「どの区間を緊急交通路にするか」は災害対応の動的判断であり、本ライブラリでは上位レイヤーが `RestrictedAreaService` で制御する設計。プロファイル側で恒常的に逆走可とするのは責務違反となるため false に固定（計画書 R3）。

`landslide` canPass=false の全車維持（計画書 N5 / R2）：緊急・災害車両であっても土砂崩れは物理的に通行不可。「規制を無視できる」ことと「物理的に通れない」ことを区別し、後者はプロファイルで一貫して `canPass=false` とする。

### 2.4 トレードオフ・制約

- **消防車の footway 通行と車幅の非現実性**（計画書 R4）：幅 2.1m の消防車が footway を access=yes で通行可とすると、物理的に通れない細街路を経路に含めうる。本実装は `speedKmh=5` の超低速設定で Dijkstra コスト経由の自然回避に委ねた。`vehicleLimits.maxWidthMeter` は OSM の `maxwidth` タグが付与されたエッジにしか効かないため、タグ欠損の細街路は防げない。実データでの挙動は親プロ統合・マルチプラットフォーム検証時に再評価する余地がある。
- **外部プロファイル名の衝突**（計画書 R5）：外部 JSON の `name` が組込み名や他の外部プロファイルと衝突すると BAKED_PROFILE スロットが一意に解決できない。`Program.cs` の一意性チェックで bake 時にエラー終了させて担保するが、利用者は一意な `name` を付ける必要がある（[profile_guide.md](profile_guide.md) §2.2 に明記）。
- **未 bake プロファイルのサイレント null**：`.odrg` に bake されていないプロファイルで `Router.Calculate` すると、スナップ段（`NativeRoadSnapper.HasProfile`）で `null` が返り「経路なし」と区別できない。これは Phase 3 からの既知挙動だが、新プロファイル追加で「古い `.odrg` に新プロファイルを当てる」誤用が起きやすくなるため、[profile_guide.md](profile_guide.md) §4 で最重要留意点として解説し、`RouterDb.GetProfileNames()` による事前確認を案内した。
- **difficulty のみランタイム評価**：通行可否・速度は bake 済み値依存だが、難所耐性（`difficulty`）の `speedFactor` / `canPass` はランタイムで JSON から読まれる。よって「既 bake プロファイルの難所反応だけ」は再 bake 不要で調整できる一方、通行可否・速度の変更には再 bake が必須。この非対称性をガイドに明記した。
- **新規評価機構なし**：3 プロファイルは既存スキーマの値設計のみで表現し、`ProfileEvaluator` / `BakedProfileTable` / `.odrg` フォーマットは不変。これにより Phase 1〜3 の既存プロファイル評価結果は完全不変（回帰ゼロ）。

### 2.5 検証方法

#### 2.5.1 単体・統合テスト（Phase 3 累計 693 → Phase 4 753 pass、+60）

| ステップ | テストファイル | 観点 |
| --- | --- | --- |
| Step 1 | [`AmbulanceProfileTests.cs`](../tests/OsmDotRoute.Tests/AmbulanceProfileTests.cs) | 同梱ロード / emergency=yes で access=private 通行 / ignoreOneway 効果 / footway 低速通行 / vehicleLimits 発火（4t 超過拒否）/ car との差分 / 難所耐性 / landslide 通行不可 |
| Step 2 | [`FireEngineProfileTests.cs`](../tests/OsmDotRoute.Tests/FireEngineProfileTests.cs) | 大型 vehicleLimits 発火（8t / 2.9m / 2.1m）/ footway 徐行通行 / truck・ambulance との差分 / 難所耐性が ambulance より控えめ / landslide 通行不可 |
| Step 3 | [`DisasterProfileTests.cs`](../tests/OsmDotRoute.Tests/DisasterProfileTests.cs) | difficulty 耐性（flooding / liquefaction / obstacle の speedFactor が truck より高い）/ truck 同等 vehicleLimits / ignoreOneway=false / footway access=no / landslide 通行不可 |
| Step 4 | [`Extractor/ProfileResolverTests.cs`](../tests/OsmDotRoute.Tests/Extractor/ProfileResolverTests.cs) | 組込み 7 名の解決 / 外部 JSON ファイルの解決 / 組込み名と外部パスの混在 / 不正 JSON のエラー / 未対応名かつ非存在パスのエラー |

`dotnet test tests/OsmDotRoute.Tests` で **753 件 pass / 0 fail / 0 skip**（2026-06-03 確認）。Phase 3 末の 693 件から回帰ゼロで +60。

#### 2.5.2 設計上の歯止め

- **公開 API 不変**：`VehicleProfile` への静的プロパティ 3 つ（`Ambulance` / `FireEngine` / `Disaster`）追加のみ。`ProfileEvaluator` / `BakedProfileTable` / `.odrg` フォーマットは変更なし。
- **既存プロファイル評価結果不変**：car / pedestrian / bicycle / truck の評価は新 JSON・新コードの影響を受けず、既存テストで回帰ゼロを実証。
- **Extractor の後方互換**：`--profiles` の組込み名指定は従来通り動作（`ProfileResolver` は組込み名を最優先で解決）。

### 2.6 実装メモ

- **Step 5（利用者向け解説）の成果物**：[profile_guide.md](profile_guide.md) / [profile_guide.en.md](profile_guide.en.md) を新規作成（プロファイルの作り方 / odrg 焼成 / 探索 / 未 bake 留意点の 4 章）。併せて [usage_guide.md](usage_guide.md) / [usage_guide.en.md](usage_guide.en.md) の Phase 3 時点で陳腐化した記述（組込み 4 種 → 7 種、外部ファイル bake 対応、§6「Phase 4+ TODO」の解消）を更新。公開文書のため内部覚書はリンクしない（[[feedback_public_doc_separation]]）。
- **Step 6（設計書・要件反映）の成果物**：本書および [requirement_definition.md](requirement_definition.md) の REQ-PRF-005 / REQ-PRF-006 完了マーク。
- **`fire_engine` の命名**：要件原文の「緊急車両」を救急/消防に分割した結果、消防車のプロファイル名は `fire`（短縮）ではなく `fire_engine` を採用（救急 `ambulance` と粒度を揃え、`fire` だと火災難所等と紛らわしいため）。
- **disaster の `name` と寸法**：disaster の `vehicleLimits` は truck と同値だが、難所 `difficulty` の値が異なるため別プロファイルとして成立する（耐性のみで差別化）。

---

## 3. 親プロFB 追補: Route.CumulativeDurationsSec（REQ-FMT-006）

**対応ステップ**: 単発 Step（親プロFB 追補、Phase 4 後追い）
**対応要件**: REQ-FMT-006（経路出力型 `Route` に Shape 点別累積所要秒を追加）
**起源**: 親プロジェクト「災害廃棄物処理シミュレーション」開発エージェントからの機能要望 [`feature_request_per_segment_durations.md`](feature_request_per_segment_durations.md)
**実装日**: 2026-06-09
**実装バージョン**: Ver 1.1.0（マイナー採番、新規プロパティ追加）
**主要ファイル**:

- [`src/OsmDotRoute/Route.cs`](../src/OsmDotRoute/Route.cs)（`CumulativeDurationsSec` プロパティ、4 引数コンストラクタ追加、3 引数互換コンストラクタは線形補間フォールバック）
- [`src/OsmDotRoute/Routing/DijkstraEngine.cs`](../src/OsmDotRoute/Routing/DijkstraEngine.cs)（`DijkstraResult.VertexCumulativeDurationsSec` を追加、復元時に `cost[v]` を頂点列順で格納）
- [`src/OsmDotRoute/Routing/RouteBuilder.cs`](../src/OsmDotRoute/Routing/RouteBuilder.cs)（Shape 構築と並行して累積秒列を構築。エッジ内中間シェイプ点は多角線距離按分で補間）
- [`tests/OsmDotRoute.Tests/CumulativeDurationsTests.cs`](../tests/OsmDotRoute.Tests/CumulativeDurationsTests.cs)（不変条件 6 種：整列・端点・単調・難所反映・SameEdge・互換コンストラクタ）

### 3.1 意図

親プロジェクトの**移動アニメーション**で、移動困難エリア（冠水 / 液状化 / 工事中等）を通過する際に**特定区間だけエージェントが目に見えて遅くなる**表現を実現したい。現状の `Route` は総距離・総所要時間・経路形状のみで区間別所要が公開されておらず、利用側でアニメーションを「時間 → 位置」で補間する手段がなかった（`Route.TotalDurationSec` 全体に均された均一速度になる）。

OsmDotRoute の Dijkstra 内部では各エッジに対し難所 `SpeedFactor` 反映済みの所要時間（`EvaluateEdgeDurationSec` / `EvaluateEdgePartialDurationSec`）を算出している。値はすでに存在するが、`Route` への集約段で区間内訳が失われていた。利用側で再構築するには `VehicleProfile.Evaluator.EvaluateDifficulty`（internal）に依存するため、ライブラリ側で **Shape と整列した累積秒列を公開** するのが最も自然と判断した。

### 3.2 採用設計

#### 3.2.1 公開 API: `Route.CumulativeDurationsSec`

```csharp
public ReadOnlyMemory<double> CumulativeDurationsSec { get; }
```

- `Shape` と 1:1 整列（`Length == Shape.Length`）。
- `[0] == 0.0`、`[^1] == TotalDurationSec`（厳密一致、同じ積算ロジック由来）。
- 単調非減少。
- 区間 i（`Shape[i] → Shape[i+1]`）の所要秒 = `CumulativeDurationsSec.Span[i+1] - CumulativeDurationsSec.Span[i]`（区間別 API は提供せず減算で導出）。
- 移動困難エリアの速度低下が区間所要に反映される（エッジ単位 SpeedFactor 由来）。
- 親側アニメーションは累積秒に対する二分探索で「経過時間 → Shape 位置」を求められる（`Shape` の累積距離補間と同じ要領）。

#### 3.2.2 構築ロジック（[RouteBuilder.cs](../src/OsmDotRoute/Routing/RouteBuilder.cs)）

`DijkstraResult` に `VertexCumulativeDurationsSec`（`VertexPath` と整列、要素は `cost[v]` = ソーススナップ点から各通過頂点までの累積秒）を追加し、`RouteBuilder` が次の規則で Shape と並行して累積秒列を組み立てる：

| Shape 構成要素 | 累積秒の出所 |
| --- | --- |
| 先頭: ソーススナップ点 | `0.0`（固定） |
| ソース側端点頂点（`vertexPath[0]`） | `VertexCumulativeDurationsSec[0]`（= `cost[vertexPath[0]]`） |
| 中間エッジの中間シェイプ点 | エッジ内多角線距離按分: `startTime + (cumPolylineDist / totalPolylineDist) × (endTime - startTime)` |
| 中間エッジの終端頂点（`vertexPath[i]`） | `VertexCumulativeDurationsSec[i]`（厳密一致） |
| 末尾: ターゲットスナップ点 | `TotalDurationSec`（固定、`bestCost` と一致） |
| SameEdge 直通 | `[0.0, TotalDurationSec]`（2 点） |

エッジ内速度（SpeedFactor 反映済）は一定なので、エッジ内中間点には多角線距離按分が正確（係数はエッジ単位なので按分しても整合）。これにより `[^1] == TotalDurationSec` の端点不変条件が浮動小数点誤差なしに成立する。

### 3.3 設計判断の根拠

| 論点 | 確定 | 理由 |
| --- | --- | --- |
| 累積形 vs 区間別形 | **累積のみ提供** | 親側アニメは「経過時間 → 位置」で累積列に二分探索を掛ける用途。減算で区間別は導出可能なため API 表面を絞った（親側要望書 §2 の推奨に沿う）。 |
| 端点厳密一致 | **`[^1] == TotalDurationSec` 厳密** | 利用側コードが `==` で端点比較できることを保証する。`TotalDurationSec` と累積列の最後の値が同じ `bestCost` 由来で、エッジ内按分も端点を保ったまま行うため浮動小数点誤差なしに成立する。 |
| エッジ内補間 | **多角線距離按分** | OsmDotRoute はエッジ単位で 1 つの所要を算出する（エッジ内速度は SpeedFactor 込みで一定）。よって距離按分が正確であり、別途速度モデルを持ち込む必要がない。退化線分（全座標一致）は `startTime` フォールバックで分岐。 |
| 互換コンストラクタ | **3 引数コンストラクタを温存（線形補間フォールバック）** | 既存利用コード（テスト等）は無改修で動作させる。線形補間はあくまでフォールバックであり、`RouteBuilder` 経由の本番経路では 4 引数コンストラクタが使われ正確な区間別累積秒が入る。 |
| 帰属 | **Phase 4 追補（親プロFB 枠）** | Phase 4 のスコープは元々「プロファイル追加 + マルチプラ対応」2 項目だが、第 1 顧客である親プロからの実需要を Phase 5 新設や Phase 4+ 未確定枠に押し込むより、Phase 4 内で「追補」として処理する方が責任所在が明確（ユーザー判断 2026-06-09）。 |

### 3.4 トレードオフ・制約

- **ソース / ターゲットスナップエッジの中間シェイプ非露出**: 現行 `RouteBuilder` はソース・ターゲット側のスナップ部分エッジについて中間シェイプを Shape に含めていない（Phase 1 設計の既知の単純化）。累積秒列も Shape と整列するため、スナップ部分エッジは「スナップ点 → 端点頂点」の 2 点のみで時間を割り振る。スナップ部分エッジの内部速度変化（同一エッジ内の難所判定はエッジ全体で 1 回なので速度は均一）は表現できないが、エッジ全体の SpeedFactor は反映済み。アニメーションの精度として実用上の問題は出ない見込み。
- **シェイプ多角線実長 vs `DistanceM`**: `Route.TotalDistanceM` はエッジ `DistanceM`（Phase 3 の Haversine 焼成値）の積算で、Shape の多角線実長と完全一致しない（既存制約、Phase 1 設計書記載）。累積秒の按分はエッジ内多角線実長で行うため、利用側で「累積秒の傾き × 距離」を計算しても `TotalDistanceM` とは微差が出うる。本機能は **時間軸補間専用** であり距離軸の整合性は保証しないので、利用側は時間ベースで補間する。
- **退化エッジの扱い**: エッジ内多角線距離が 0 となる退化ケース（全シェイプ点が同一座標）は、中間点に `startTime` を割り当てる。実データではほぼ発生しないが、`totalDist <= 0` 分岐で防衛。
- **API 表面の最小化**: 区間別所要 `SegmentDurationsSec`、エッジ ID 列、難所係数の露出などは提供しない。区間別所要は減算で導出可能、その他は利用側ユースケースに対し過剰な情報露出となるため。

### 3.5 検証方法

#### 3.5.1 不変条件テスト 6 件（[CumulativeDurationsTests.cs](../tests/OsmDotRoute.Tests/CumulativeDurationsTests.cs)）

| # | テスト | 検証する不変条件 |
| --- | --- | --- |
| 1 | `Cumulative_Length_MatchesShapeLength` | 整列: `CumulativeDurationsSec.Length == Shape.Length` |
| 2 | `Cumulative_Endpoints_AreExactlyZeroAndTotalDuration` | 端点: `[0] == 0.0`、`[^1] == TotalDurationSec`（厳密一致） |
| 3 | `Cumulative_IsMonotonicNonDecreasing` | 単調性: 全 i で `[i] <= [i+1]` |
| 4 | `Cumulative_DifficultyAreaCoveringRoute_TimingReflectedPerPoint` | 難所反映: 全エッジ flooding で被覆 → 各点累積秒が baseline の 1/0.3 ≈ 3.333 倍（許容 3.27〜3.40） |
| 5 | `Cumulative_SamePoint_TrivialRouteRespectsEndpoints` | SameEdge: 同一点起点終点でも端点不変条件が成立 |
| 6 | `Cumulative_LegacyConstructor_GeneratesLinearFallbackWithExactEndpoints` | 互換コンストラクタ: 3 引数経路でも端点厳密一致と単調性を維持 |

#### 3.5.2 全体回帰

`dotnet test tests/OsmDotRoute.Tests` で **761 件 pass / 0 fail / 0 skip**（2026-06-09 確認）。Phase 4 マルチプラ完了時の 753 件から +8（不変条件 6 + 別途追加分、回帰ゼロ）。

#### 3.5.3 設計上の歯止め

- **公開 API 後方互換**: `Route` の 3 引数コンストラクタを温存、新規プロパティ追加のみ。既存利用コード（親プロ Phase 1〜3 統合コード含む）は無改修で動作。
- **内部実装の追加のみ**: `DijkstraResult` は record で 1 フィールド追加、`RouteBuilder` の Shape 構築ロジックは並行配列を組むだけで Shape 出力自体は不変。Phase 1〜3 の経路探索結果は完全不変（テストで実証）。
- **エッジ単位 SpeedFactor の整合性**: `EvaluateEdgeDurationSec` / `EvaluateEdgePartialDurationSec` の既存実装をそのまま利用するため、Phase 1〜3 の制約評価セマンティクスが累積秒列にも自動で反映される（テスト 4 で実証）。

### 3.6 実装メモ

- **親プロ側の利用計画**（要望書 §4 参考、本要望のスコープ外）: 親側で `MapService.RouteResult` に累積所要を載せ、`AgentRouteChanged` ペイロードで前端へ送り、`SimulationPage.tsx` のアニメーションを **距離比例 → 時間比例（累積所要の二分探索）** に切り替える。親側交通負荷係数（25km/h 校正）は親側で `TotalDurationSec` に対して適用するため、本 API は素の累積秒を返すだけでよい。
- **要件 ID の採番**: 親プロ側提案の **REQ-FMT-006**（Format ジャンル）を採用。REQ-FMT-001〜003（総距離 / 総所要 / Shape）の延長線上の追加であり、ジャンルとして REQ-FMT が自然。
- **メタデータへの影響なし**: `.odrg` フォーマット・BAKED_PROFILE スロット・プロファイル評価機構には一切手を入れない。本機能はランタイム経路出力型の追加のみで成立する。

---

## 4. 改訂履歴

| Ver | 日付 | 変更 |
| --- | --- | --- |
| 親プロFB 追補 | 2026-06-09 | §3「Route.CumulativeDurationsSec（REQ-FMT-006）」を追加。親プロジェクト「災害廃棄物処理シミュレーション」からの区間別速度低下アニメーション要望に応えて Route に Shape 点別累積所要秒を追加。実装は `DijkstraResult.VertexCumulativeDurationsSec` + `RouteBuilder` でエッジ内多角線距離按分による補間。不変条件テスト 6 種で整列・端点・単調・難所反映・SameEdge・互換コンストラクタを実証、全 761 pass（回帰ゼロ）。Ver 1.1.0 マイナー採番 |
| 初版 | 2026-06-03 | Phase 4 プロファイル追加（Step 1〜4 完了）を §0〜§2 に起こし。救急車 `ambulance` / 消防車 `fire_engine` / 災害用 `disaster` の設計値・根拠・トレードオフ・検証（753 pass）、Extractor 外部 JSON プロファイル対応（`ProfileResolver`）を記録。Step 5（利用者ガイド）/ Step 6（要件反映）の位置付けを §2.6 に追記。バージョンはユーザー採番 |
</content>
