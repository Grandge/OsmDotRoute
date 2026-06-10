# OsmDotRoute Phase 4 設計書

**バージョン**: ユーザー採番（未採番、Ver 1.1.0 = 親プロFB 追補ぶん、Ver 1.1.1 = 親プロFB 不具合修正ぶん）
**作成日**: 2026-06-03
**最終更新**: 2026-06-09（親プロFB 不具合修正 §4 追加）
**ステータス**: プロファイル追加（救急車 / 消防車 / 災害用車両 ＋ Extractor 外部 JSON プロファイル対応）完了。**マルチプラットフォーム対応も完了**（2026-06-03、macOS ARM64 / Linux x64 で 753 pass）。**親プロFB 追補（REQ-FMT-006 = Route.CumulativeDurationsSec）完了**（2026-06-09、Ver 1.1.0、全 761 pass）。**親プロFB 不具合修正（REQ-PRF-014 改訂 + REQ-PRF-017 = 難所タイプ case-insensitive 化＋観測性 API）完了**（2026-06-09、Ver 1.1.1、全 777 pass）。マルチプラットフォーム対応の計画・設計記録は別書 [phase4_multiplatform_plan.md](phase4_multiplatform_plan.md) で扱う
**対象**: OsmDotRoute Phase 4 のうち**プロファイル追加**と**親プロFB 追補**の設計記録（REQ-PRF-005 = 救急車 `ambulance` / 消防車 `fire_engine`、REQ-PRF-006 = 災害用車両 `disaster`、＋ユーザー定義プロファイルの bake 経路拡張、REQ-FMT-006 = Route 区間別累積所要秒、REQ-PRF-014 改訂 / REQ-PRF-017 = 難所タイプ照合 case-insensitive 化＋観測性 API）
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
| 4. 親プロFB 不具合修正: 難所タイプ照合 case-insensitive 化（REQ-PRF-014 改訂 / REQ-PRF-017 追加） | 単発 Step | **肉付け完了**（2026-06-09、Ver 1.1.1） |
| 5. 親プロFB 追補: 1/8 細分メッシュ（125m）＋ GmlParser フィーチャ属性公開（REQ-RST-016 仕様確定 / REQ-RST-041） | 単発 Step | **肉付け完了**（2026-06-11、Ver 1.2.0） |
| 6. 改訂履歴 | 各ステップ完了時 | 初版 |

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
- [`src/OsmDotRoute/VehicleProfile.cs`](../src/OsmDotRoute/VehicleProfile.cs)（`Ambulance` / `FireEngine` / `Disaster` 静的プロパティ追加、`Lazy<T>` パターン踏襲）
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

## 4. 親プロFB 不具合修正: 難所タイプ照合 case-insensitive 化（REQ-PRF-014 改訂 / REQ-PRF-017）

**対応ステップ**: 単発 Step（親プロFB 不具合修正、Phase 4 追補内）
**対応要件**: REQ-PRF-014（既存仕様の case-insensitive 化改訂）、REQ-PRF-017（新規・観測性 API 提供）
**起源**: 親プロジェクト開発エージェントからの不具合報告（v1.1.0 アニメ目視検証中に発覚）。`Documents/debug_flooding_x10_for_animation_verification.md` 経由の往復で「`flooding` を 0.03 に絞ってもアニメが減速しない → 親プロは `"Flooding"` PascalCase を渡しており case-sensitive 照合で `difficultyDefault` にサイレント・フォールバックしていた」と判明
**実装日**: 2026-06-09
**実装バージョン**: Ver 1.1.1（パッチ採番、後方互換バグ修正＋小規模 API 追加）
**主要ファイル**:

- [`src/OsmDotRoute/Profiles/ProfileEvaluator.cs`](../src/OsmDotRoute/Profiles/ProfileEvaluator.cs)（`_difficultyLookup` を `Ordinal-IgnoreCase` 比較器で構築、`EvaluateDifficulty` の照合経路を差し替え、内部 `KnownDifficultyTypes` / `HasDifficulty` 追加、`ValidateAndCompile` で case-only 重複キー検出）
- [`src/OsmDotRoute/VehicleProfile.cs`](../src/OsmDotRoute/VehicleProfile.cs)（公開 `IReadOnlyCollection<string> KnownDifficultyTypes` / `bool HasDifficulty(string)` を追加、`Evaluator` 委譲）
- [`src/OsmDotRoute/DifficultyTypes.cs`](../src/OsmDotRoute/DifficultyTypes.cs)（XML doc に「正準小文字推奨・case-insensitive 照合・未定義タイプは silent fallback」を明記）
- [`src/OsmDotRoute/Restrictions/RestrictedAreaService.cs`](../src/OsmDotRoute/Restrictions/RestrictedAreaService.cs)（クラス XML doc に同旨の注意を追加）
- [`src/OsmDotRoute/Route.cs`](../src/OsmDotRoute/Route.cs)（`CumulativeDurationsSec` の `<remarks>` にデバッグ tips を追加）
- [`tests/OsmDotRoute.Tests/DifficultyTypeCaseInsensitivityTests.cs`](../tests/OsmDotRoute.Tests/DifficultyTypeCaseInsensitivityTests.cs)（新規 16 テスト：Theory 4 × 2、HasDifficulty 各種、KnownDifficultyTypes、重複キー、E2E PascalCase 減速反映）

### 4.1 意図

親プロからの不具合報告の本質は「**設定ミスがサイレントに既定値へ落ちて、機能が動いていないことに気づけない**」という観測性の欠如にある。

直接の症状（PascalCase で照合外れ）は親プロ側で `ToLowerInvariant()` 適用済みだが、これは「親プロが気づいて回避した」だけであり、ライブラリ利用者の他の誰かも同じ罠に再び落ちる構造が残っている。本修正の狙いは：

1. **直接修正**: 表記揺れを ライブラリ側で吸収（照合を case-insensitive に）。利用者が正準キー以外を渡しても、十分に予測可能な範囲なら救済する。
2. **観測性向上**: 「使えるタイプかを事前確認できる」公開 API を追加し、サイレントな失敗を能動的に検知できるようにする。
3. **意図しない混入の拒否**: case 違いで衝突する JSON 定義は実装時に弾く（profile 作者のミス検知）。

### 4.2 採用設計

#### 4.2.1 照合の case-insensitive 化（REQ-PRF-014 改訂）

`ProfileEvaluator` のコンストラクタで、`def.Difficulty`（`Dictionary<string, JsonDifficultyRule>`、JSON デシリアライズ既定 = Ordinal）を `StringComparer.OrdinalIgnoreCase` 比較器付き Dictionary `_difficultyLookup` に **コピー** する。`EvaluateDifficulty` は `_difficultyLookup.TryGetValue` のみに依存（元の `_def.Difficulty` 参照を廃止）。

```csharp
_difficultyLookup = def.Difficulty is { } diff
    ? new Dictionary<string, JsonDifficultyRule>(diff, StringComparer.OrdinalIgnoreCase)
    : new Dictionary<string, JsonDifficultyRule>(StringComparer.OrdinalIgnoreCase);
```

性能インパクトは無視できる（Dictionary 構築 1 回、評価時は同じ O(1) ハッシュ参照、ハッシュ計算が大小無視になるが文字列長 ≤ 16 が典型なので差分は無関係）。

#### 4.2.2 case-only 重複キーの拒否

case-insensitive 化により、JSON 内に `"flooding"` と `"Flooding"` が併存すると一意性が崩れる（Dictionary ctor が例外を投げる、または最後の登録が優先される実装依存挙動になる）。これは明示的に **`InvalidProfileException` で拒否** する。

検出は `ValidateAndCompile` で `HashSet<string>(StringComparer.OrdinalIgnoreCase)` の `Add` 失敗を見るだけ。エラーメッセージで「正準小文字キーに統一してください」を案内する。

#### 4.2.3 観測性 API（REQ-PRF-017 新規）

公開 `VehicleProfile` に 2 つのプロパティ／メソッドを追加：

| API | シグネチャ | 用途 |
| --- | --- | --- |
| `KnownDifficultyTypes` | `IReadOnlyCollection<string>` | プロファイルが定義する全難所タイプキー（JSON 表記そのまま）の列挙 |
| `HasDifficulty(string)` | `bool` | 指定タイプが定義済みか（case-insensitive 含有判定） |

利用例（親プロ側起動時診断）:

```csharp
foreach (var registeredType in scenarioDifficultyTypes)
{
    if (!profile.HasDifficulty(registeredType))
        logger.LogWarning("Profile '{name}' does not define difficulty type '{type}'; "
                        + "speedFactor=1.0 fallback will apply (no slowdown).",
                          profile.Name, registeredType);
}
```

`ILogger` を本ライブラリには持ち込まず（依存を増やさない）、利用者の任意 logger に委ねる設計とした（提案 2 の「警告ログ自動出力」を採用せず、観測性 API という間接策に倒した理由）。

### 4.3 設計判断の根拠

| 論点 | 確定 | 理由 |
| --- | --- | --- |
| 照合方式 | **case-insensitive（OrdinalIgnoreCase）** | 利用者の自然な期待（`"Flooding"` でも効くだろう）に揃える。Culture-aware 比較は不要（難所タイプキーは ASCII 識別子限定、REQ-PRF-013）、Ordinal で十分かつ最速 |
| 後方互換 | **完全互換**（既存小文字キー利用者は無影響） | 既存テスト全 761 件が **未修正のまま** pass する状態を維持。新規テスト 16 件は v1.1.1 で初めて意味を持つ |
| 観測性手段 | **自前 logger 不採用、API 経由で利用者が検証** | コアに `ILogger<T>` を入れると Microsoft.Extensions.Logging.Abstractions 依存が増え、DI 統合プロジェクトと整合させる手間が出る。API 経由なら依存ゼロで観測性確保 |
| バージョン | **1.1.1 パッチ採番** | バグ修正＋ API 微増。SemVer 厳密適用なら API 追加はマイナーだが、v1.x 期間の小規模 API 追加はパッチ扱いで運用（README の「0.x 期間中の破壊的 API 変更はマイナー版アップで許容」と整合的） |
| 帰属 | **Phase 4 追補内・不具合修正** | v1.1.0 と同じ親プロFB 枠で受け止める。Phase 5 新設は別議論（Phase 4 のスコープを「親プロFB 全般」に解釈拡張） |

### 4.4 トレードオフ・制約

- **`KnownDifficultyTypes` の表記揺れ**: 戻り値の各要素は JSON 定義そのままの表記（小文字推奨だが、ユーザー定義プロファイルで PascalCase 等が入る可能性あり）。利用者が含有判定する際は `HasDifficulty(string)` 経由（case-insensitive）を推奨し、文字列の単純 `Contains` は避けるよう XML doc で案内。
- **`InvalidProfileException` の破壊性**: v1.1.0 まで「同一プロファイル JSON に `"flooding"` と `"Flooding"` を併記」が偶然動いていた（後勝ち）ケースがもしあれば、v1.1.1 で例外になる。標準同梱 7 プロファイル＋本プロジェクトの 9 テスト用 JSON は全て正準小文字なので影響ゼロ。リスクは利用者側 JSON のみ（実害があれば破壊変更扱い）。
- **「未定義タイプはサイレントに既定値」自体は維持**: REQ-PRF-014 の「定義に存在しない難所タイプ → `difficultyDefault`」は REQ-PRF-013（ユーザー定義タイプ）と表裏一体の仕様で、未知タイプを一律例外にはできない。観測性 API でこのトレードオフを利用者側で能動検知できる形に倒したのが本修正の本質。
- **CumulativeDurationsSec の XML doc に依存**: v1.1.0 の `Route.CumulativeDurationsSec` 利用者は、減速が見えない事象を見たとき本不具合を疑えるよう doc remarks に明記したが、doc を読まない利用者には届かない。`HasDifficulty` を起動時診断で呼ぶ習慣を [profile_guide.md](profile_guide.md) 等にも展開する余地がある（次回ガイド改訂時に反映）。

### 4.5 検証方法

#### 4.5.1 単体・統合テスト（v1.1.0 末 761 → +16 → 777 pass）

| グループ | テスト件数 | 観点 |
| --- | --- | --- |
| `EvaluateDifficulty_AnyCase_MatchesLowercaseProfileEntry` | 4（Theory） | `"flooding" / "Flooding" / "FLOODING" / "fLoOdInG"` のいずれも `car.json` の `"flooding"` エントリ（speedFactor=0.3）に一致 |
| `EvaluateDifficulty_TrulyUnknownType_StillFallsToDefault` | 1 | 未定義 `"meteor_strike"` は従来通り `difficultyDefault`（speedFactor=1.0）に落ちる（REQ-PRF-014 既存仕様維持） |
| `HasDifficulty_*` | 8（Theory 含む） | 正準小文字 / 表記揺れ 3 種 / 未知タイプ / null・空・空白の各ケース |
| `KnownDifficultyTypes_Car_ContainsAllBuiltinKeys` | 1 | `DifficultyTypes` 組込み 8 種が全て列挙される（`Count == 8`） |
| `LoadFromJsonString_CaseOnlyDuplicateKeys_ThrowsInvalidProfileException` | 1 | `"flooding"` と `"Flooding"` 併記 JSON が即時拒否される |
| `Calculate_DifficultyArea_PascalCase_SlowsDownSameAsLowercase` | 1 | **E2E 回帰固定**: PascalCase 指定で経路所要が小文字指定と一致、かつ baseline の 3 倍超に増加（= サイレント・フォールバックではない） |

`dotnet test tests/OsmDotRoute.Tests` で **777 pass / 0 fail / 0 skip**（2026-06-09 確認）。

#### 4.5.2 設計上の歯止め

- **既存 761 件の無改変 pass**: 既存テストは全て正準小文字キー（`DifficultyTypes.Flooding` 等の const 経由）を使うため、case-insensitive 化の影響を受けず未修正で pass。回帰がないことの最強実証。
- **公開 API 後方互換**: `Route` / `RestrictedAreaService` / `Router` の既存メソッドシグネチャに変更なし。`VehicleProfile` への追加は新規プロパティ＋メソッドのみ（既存メンバー不変）。
- **API 表面の最小化**: ロガー注入 / Action コールバック / 警告イベント等を見送り、`bool HasDifficulty(string)` ＋ `IReadOnlyCollection<string> KnownDifficultyTypes` の 2 つに絞った。利用者の検証ロジックは利用者責務とすることで、ライブラリのコアを薄く保つ。

### 4.6 実装メモ

- **親プロ側で既に回避済み**: 親プロは `ToLowerInvariant()` で正規化済みなので、本修正の有無に関わらず親プロは動く。本修正は「将来の利用者の事故防止＋親プロ側の正規化ロジック削除可能性」を狙う。親プロが正規化ロジックを削除するかは親プロ判断（残しても安全）。
- **アニメ目視用デバッグ JSON の継続価値**: `Documents/debug/car_debug_flooding_x10.json`（v1.1.0 検証用、git 未追跡）は今回の不具合修正と独立した「視覚的増幅（0.3 → 0.03）」目的の資産。バグは修正されたが、アニメ tuning 時に「明白な減速」を見たい場面では依然有用なため削除しない（利用者が任意で削除する）。
- **`debug_flooding_x10_for_animation_verification.md` の扱い**: 親プロ向けデバッグ手順書はユーザー（本プロジェクトオーナー）が削除予定（[[feedback]] 「相手が読んだ後に私が削除します」）。ここでは手を入れない。
- **学び**: 親プロ側エージェントの不具合報告 §6 が秀逸（「既定値へのサイレント・フォールバックは設定ミスを正常動作に偽装する」）。これは Phase 5 以降の同種設計判断（例: 未知プロファイル名・未知 access タグ値）でも参照する原則として留意する。

---

## 5. 親プロFB 追補: 1/8 細分メッシュ（125m）＋ GmlParser フィーチャ属性公開（REQ-RST-016 仕様確定 / REQ-RST-041）

**対応ステップ**: 単発 Step（親プロFB 追補、Phase 4 後追い）
**対応要件**: REQ-RST-016（11 桁 = 1/8 細分・125m・象限方式の仕様確定）/ REQ-RST-041（KSJ GML のフィーチャ単位「形状＋属性」公開 API）
**起源**: 親プロジェクト「災害廃棄物処理シミュレーション」開発エージェントからの機能要望 [`feature_request_mesh_level8_and_gml_attributes.md`](feature_request_mesh_level8_and_gml_attributes.md)（KSJ ハザードデータ取り込み計画 REQ-HAZ-013〜017 の前提）
**実装日**: 2026-06-11
**実装バージョン**: Ver 1.2.0（マイナー採番、公開 API 追加のみ）
**主要ファイル**:

- [`src/OsmDotRoute/MeshLevel.cs`](../src/OsmDotRoute/MeshLevel.cs)（`EighthMesh` 追加）
- [`src/OsmDotRoute/MeshCode.cs`](../src/OsmDotRoute/MeshCode.cs)（`Level` の 11 桁判定、範囲外上限を 12 桁に更新）
- [`src/OsmDotRoute/Mesh/MeshCodeConverter.cs`](../src/OsmDotRoute/Mesh/MeshCodeConverter.cs)（細分処理を象限再帰ループに一般化）
- [`src/OsmDotRoute/Gml/GmlFeature.cs`](../src/OsmDotRoute/Gml/GmlFeature.cs)（新規公開 record）
- [`src/OsmDotRoute/Gml/GmlParser.cs`](../src/OsmDotRoute/Gml/GmlParser.cs)（internal → public、`ParseFeaturesString/Stream` 追加）
- [`tests/OsmDotRoute.Tests/MeshCodeTests.cs`](../tests/OsmDotRoute.Tests/MeshCodeTests.cs) / [`GmlFeatureParsingTests.cs`](../tests/OsmDotRoute.Tests/GmlFeatureParsingTests.cs) / [`RestrictedAreaServiceAttachGraphTests.cs`](../tests/OsmDotRoute.Tests/RestrictedAreaServiceAttachGraphTests.cs)

### 5.1 意図

親プロジェクトは国土数値情報の公開ハザードデータ（洪水 A31a/A31b、土砂 A33、内水 A51、多段階浸水 A53）を**125m メッシュにラスタライズして移動制約エリアとして登録**する機能を計画している。250m（10 桁）では土砂災害警戒区域（幅数十 m の小区域多数）や浸水縁の形状が粗くなりすぎるため、1 階層下の 125m が必要（要望①）。また A51 のみ GeoJSON 未提供（GML のみ）で、浸水深ランク等の**フィーチャ属性に基づく制約レベル振り分け**（ランク2=移動困難 / ランク3以上=移動不能）には形状と属性のペアが必要だが、既存 `GmlParser` は形状のみを返していた（要望②）。

親側の活動効果判定・描画は `MeshCode.ToBounds()` 委譲で実装済みのため、ライブラリが 11 桁対応すれば親側ロジックは自動追従する。

### 5.2 採用設計

#### 5.2.1 要望①: 1/8 細分（象限方式）の採用

JIS X 0410 の 3 次メッシュより細かい区画には「分割地域メッシュ（象限方式、11 桁 = 1/8 = 125m）」と「10分の1細分区画（8 桁＋2 桁 = 10 桁 = 100m）」の 2 系統があるが、後者は**既存 1/4 細分（10 桁）と桁数衝突**し `MeshCode.Level` の桁数→階層一意判定が崩れるため、**11 桁目 = 象限 1〜4 の 1/8 細分（125m）を正式仕様として採用**（親要望書の提案どおり。v1.4 で延期した「1/10 細分 = 100m / 11 桁」記載はこの仕様への読み替えで確定）。

- `MeshLevel.EighthMesh` を追加し、`MeshCode.Level` に `10_000_000_000〜99_999_999_999` → `EighthMesh` を追加（範囲外例外の上限が 12 桁に移動）
- `MeshCodeConverter.ToBoundingBox` / `ToMeshCode` の細分処理は、9/10/11 桁で同型の象限再帰（SW=1, SE=2, NW=3, NE=4、ステップ幅半減）のため、**桁ごとの if ブロック羅列から細分桁ループ（`for digit = 8 .. code.Length-1`）に一般化**。1/8 メッシュは緯度 3.75 秒（≒115.7m）× 経度 5.625 秒
- `EnumerateInBounds` に `EighthMesh` のステップ幅（`/8`）を追加（南西→北東走査の既存契約・境界スナップ eps = 1e-7 度はそのまま。最小メッシュ幅 125m に対しても十分小さい）
- `RestrictedAreaService.AddBlockArea / AddDifficultyArea(IEnumerable<MeshCode>)` は `Shape.FromMesh` → `ToBoundingBox` 経由のため**変更不要**（要望書の見立てどおり）。AABB 直接使用（REQ-RST-015）のセマンティクスも不変

#### 5.2.2 要望②: GmlParser のフィーチャ属性公開

```csharp
public sealed record GmlFeature(
    GeoPolygon Polygon,
    IReadOnlyDictionary<string, string> Attributes);   // key=要素ローカル名（例 "A51_001"）、value=テキスト内容

public static class GmlParser   // internal → public
{
    public static IReadOnlyList<GeoPolygon> ParseString(string gml);          // 既存（形状のみ）
    public static IReadOnlyList<GeoPolygon> ParseStream(Stream stream);       // 既存（形状のみ）
    public static IReadOnlyList<GmlFeature> ParseFeaturesString(string gml);  // 新規（形状＋属性）
    public static IReadOnlyList<GmlFeature> ParseFeaturesStream(Stream stream);
}
```

- 内部は単一のパスを共有: コア `Parse` が `List<GmlFeature>` を構築し、`ParseString/ParseStream` は `Polygon` のみ射影。1 パス構造（Curve / Surface 辞書 → フィーチャ解決）は不変
- 旧 `FindSurfaceReferenceInFeature`（最初の `xlink:href="#..."` を返すのみ）を `ReadFeature` に置換し、**同一サブツリー走査で形状参照と属性を同時に取得**（サブツリーは 1 回しか読めないため）
- 属性の抽出規則: フィーチャ要素**直下**（Depth=1）の子要素のうち「子要素を持たない・xlink 参照でない・テキストがある」もの。key は名前空間 prefix を剥がしたローカル名、value はテキスト内容そのまま（型解釈・コードリスト解決は利用側責務）。同名要素は後勝ち。属性ゼロは共有の空 Dictionary（例外にしない）
- 大容量対応はリスト返却のまま（A51 は愛知県 0.6MB と小さい、要望書の合意どおりストリーミング yield 不要）

### 5.3 設計判断の根拠

| 論点 | 確定 | 理由 |
| --- | --- | --- |
| 11 桁の解釈 | **1/8 細分（象限方式）** | 既存 9・10 桁と同じ象限再帰で実装が 1 段深くなるだけ。桁数→階層の一意判定（`Level` の switch）が維持される。10分の1細分区画は既存 10 桁と判別不能。親ユースケース（ハザード形状保持）には 125m で十分（親側ユーザー確定済み） |
| 細分処理のループ化 | **桁ループに一般化** | 9/10/11 桁の if ブロック 3 連は完全同型のコピーになる。ループは除算が 2 のべき乗（浮動小数点で正確）のため数値挙動も既存と一致（既存 8〜10 桁テストが無変更でパスすることで実証） |
| 属性公開の形 | **`GmlParser` 公開化＋`ParseFeatures*` 追加** | 制約登録（`Add*FromGml*`）と分離した読み取り専用 API。属性→制約レベル振り分けは利用側責務（REQ-RST-026 の方針を維持）であり、ライブラリは「形状＋属性の素材」を返すに留める。親側で GML 形状パーサを二重実装する無駄を回避（要望書の趣旨） |
| 属性 = 直下の単純子要素のみ | **複合要素・空要素・xlink 参照は対象外** | KSJ の属性は フィーチャ直下の単純要素（`A51_001` 等）。複合要素まで再帰すると KSJ プロダクト毎のスキーマ知識が必要になり「フィーチャ要素名にハードコード依存しない」原則（REQ-RST-020）に反する |
| `Add*FromGml*` への属性引数追加 | **しない** | 全フィーチャ同一難所タイプの既存契約（REQ-RST-026）を維持。属性別振り分けは `ParseFeatures*` → 利用側ループ → `AddBlockArea/AddDifficultyArea` で組み立てる方が自由度が高い |

### 5.4 トレードオフ・制約

- **性能（要望書 §5 への回答）**: 数千〜数万件の 11 桁メッシュ一括登録は、既存の `Register` → shape ごとの `SpatialIndex.Add` + `BakeIntoCache`（AABB クエリ）構造のまま処理できる。メッシュ階層追加によるホットパスの分岐増はゼロ（`ToBoundingBox` は登録時に 1 回だけ呼ばれ、以降は bake 済みエッジ集合で判定）。125m メッシュは 250m 比で同面積あたり個数 4 倍になるが、shape あたりのコストは AABB 1 個で不変のため線形増にとどまる見込み。実測で問題が出た場合は隣接メッシュの矩形マージ（利用側 or ライブラリ側）を将来検討
- **`gml:MultiSurface` は引き続き非対応**（REQ-RST-023、検出時 `NotSupportedException`）。A51 実データでの出現有無は親側確認待ち（要望書 §4 でスコープ外と合意）
- **属性 value は生テキスト**: trim・型変換・コードリスト解決はしない。KSJ の属性値は単純トークンが基本で、解釈はデータセット知識を持つ利用側が行う
- **`GmlParser` 公開化に伴う API 表面拡大**: 形状のみの `ParseString/ParseStream` も公開になる。挙動は従来 internal 時代と同一で、`Add*FromGml*` 系の内部利用も不変のため互換リスクなし

### 5.5 検証方法

新規テスト 16 件（メッシュ 8 + GML 6 + 統合 2）、全 793 pass（v1.1.1 末の 777 から +16、回帰ゼロ）。親要望書の受け入れ基準との対応:

| 受け入れ基準（要望書） | テスト |
| --- | --- |
| ①-1 11 桁 `ToBounds()` が親 10 桁の対応象限の SW/NE と厳密一致（境界共有） | `ToBoundingBox_EighthMesh_SwQuadrant_SharesParentQuarterSouthwest` / `NeQuadrant_SharesParentQuarterNortheast`（precision 12） |
| ①-2 同一 3 次メッシュ内 64 個が隙間・重複なくタイリング | `ToBoundingBox_All64EighthMeshes_TileParentWithoutGapOrOverlap`（8×8 格子位置の全単射を検証） |
| ①-3 `EnumerateInBounds` が 8〜11 桁で整合した個数（1/8 は 1/4 の縦横 2 倍） | `EnumerateInBounds_AllFourLevels_CountsAreConsistent`（1/4/16/64）/ `EnumerateInBounds_EighthMesh_Of_1km_Yields64SubCells` |
| ①-4 11 桁 `AddBlockArea(meshCodes)` で交差エッジのみ遮断（REQ-RST-015 踏襲） | `AttachGraph_EighthMeshBlockArea_BakesIntersectingEdgesOnly`（津島市 .odrg 実グラフ、範囲外メッシュは遮断ゼロも確認）/ `AttachGraph_MixedQuarterAndEighthMeshes_RegisteredTogether`（10・11 桁混在） |
| ①-5 既存 8〜10 桁テストが無変更でパス | 既存 777 件回帰ゼロ（11 桁を「範囲外」と期待していた `Level_Throws_ForOutOfRangeDigits` の 2 ケースのみ 12 桁に差し替え＝仕様確定そのもの） |
| ②-1 フィーチャ数・`A51_*` 属性・外周/内環座標の取得 | `ParseFeaturesString_ReturnsShapeAndAttributesPerFeature`（A51 相当 GML、Hole 込み） |
| ②-2 既存 `ParseString/ParseStream` / `Add*FromGml` 系の挙動不変 | 既存 GML テスト全パス + `ParseString_ReturnsSamePolygonsAsParseFeatures` |
| ②-3 属性ゼロのフィーチャは空 Dictionary | `ParseFeaturesString_FeatureWithoutAttributes_ReturnsEmptyDictionary` |

加えて 11 桁目 0/5 の `ArgumentException`（既存細分桁と同等）、複合要素・空要素・xlink 参照要素が属性に混入しないこと、Stream/string 変種の同値性を検証。

### 5.6 実装メモ

- `Level_Throws_ForOutOfRangeDigits` の旧 InlineData（`10_000_000_000L` / `99_999_999_999L` = 11 桁を範囲外と期待）は本仕様確定により有効値となったため削除し、12 桁ケースを追加。これは「既存 8〜10 桁テストの無変更パス」（受け入れ基準①-5）には抵触しない（当該 2 ケースは 11 桁の挙動を固定するテストだったため）
- `MeshCode.cs` remarks の「11 桁（1/10 細分 = 100m）は仕様未確定」記載を削除し、1/8 象限方式＋10分の1細分区画を採用しない理由を明記
- 親側への返答ポイント: 性能上の留意点は §5.4 第 1 項（現行構造で問題なし、形状あたり AABB 1 個で線形）。`MultiSurface` は引き続き別途相談
- **Sandbox も 125m 対応**（ユーザー指示 2026-06-11）: メッシュグリッド表示の階層に `125m` を追加。Server（`MeshEndpoints.ParseLevel`）/ WASM（`RestrictionInterop.ParseLevel`）に `"125m" / "eighthmesh" / "11"` → `EighthMesh` を追加し、Web UI のドロップダウン・型・i18n（`mg.level125m`）を拡張。セル数上限 10,000 のガードは既存のまま（125m で広域を表示すると上限エラー → 階層を粗くする案内）

---

## 6. 改訂履歴

| Ver | 日付 | 変更 |
| --- | --- | --- |
| 親プロFB 追補 | 2026-06-11 | §5「1/8 細分メッシュ（125m）＋ GmlParser フィーチャ属性公開（REQ-RST-016 仕様確定 / REQ-RST-041）」を追加。`MeshLevel.EighthMesh`（11 桁・象限方式）、`MeshCodeConverter` の細分処理ループ一般化、`GmlParser` 公開化＋`ParseFeaturesString/Stream`（`GmlFeature` = 形状＋属性 Dictionary）、Sandbox メッシュグリッドの 125m 階層追加（Server / WASM / Web UI）。新規テスト 16 件、全 793 pass（回帰ゼロ）。Ver 1.2.0 マイナー採番 |
| 親プロFB 不具合修正 | 2026-06-09 | §4「難所タイプ照合 case-insensitive 化（REQ-PRF-014 改訂 / REQ-PRF-017）」を追加。親プロ側不具合報告（v1.1.0 アニメ目視検証中に発覚した `"Flooding"` PascalCase でのサイレント・フォールバック）を受けて、`ProfileEvaluator` の照合経路を `Ordinal-IgnoreCase` 化、case-only 重複キー検出、`VehicleProfile.KnownDifficultyTypes` / `HasDifficulty(string)` 観測性 API を追加。XML doc 4 箇所（`DifficultyTypes` / `RestrictedAreaService` / `Route.CumulativeDurationsSec` / `EvaluateDifficulty`）でサイレント・フォールバック挙動を明記。新規テスト 16 件、全 777 pass（回帰ゼロ）。Ver 1.1.1 パッチ採番 |
| 親プロFB 追補 | 2026-06-09 | §3「Route.CumulativeDurationsSec（REQ-FMT-006）」を追加。親プロジェクト「災害廃棄物処理シミュレーション」からの区間別速度低下アニメーション要望に応えて Route に Shape 点別累積所要秒を追加。実装は `DijkstraResult.VertexCumulativeDurationsSec` + `RouteBuilder` でエッジ内多角線距離按分による補間。不変条件テスト 6 種で整列・端点・単調・難所反映・SameEdge・互換コンストラクタを実証、全 761 pass（回帰ゼロ）。Ver 1.1.0 マイナー採番 |
| 初版 | 2026-06-03 | Phase 4 プロファイル追加（Step 1〜4 完了）を §0〜§2 に起こし。救急車 `ambulance` / 消防車 `fire_engine` / 災害用 `disaster` の設計値・根拠・トレードオフ・検証（753 pass）、Extractor 外部 JSON プロファイル対応（`ProfileResolver`）を記録。Step 5（利用者ガイド）/ Step 6（要件反映）の位置付けを §2.6 に追記。バージョンはユーザー採番 |
