# OsmDotRoute Phase 4 設計書

**バージョン**: ユーザー採番（未採番）
**作成日**: 2026-06-03
**最終更新**: 2026-06-03
**ステータス**: プロファイル追加（救急車 / 消防車 / 災害用車両 ＋ Extractor 外部 JSON プロファイル対応）完了。**マルチプラットフォーム対応も完了**（2026-06-03、macOS ARM64 / Linux x64 で 753 pass）。マルチプラットフォーム対応の計画・設計記録は別書 [phase4_multiplatform_plan.md](phase4_multiplatform_plan.md) で扱う
**対象**: OsmDotRoute Phase 4 のうち**プロファイル追加**の設計記録（REQ-PRF-005 = 救急車 `ambulance` / 消防車 `fire_engine`、REQ-PRF-006 = 災害用車両 `disaster`、＋ユーザー定義プロファイルの bake 経路拡張）
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
| 3. 改訂履歴 | 各ステップ完了時 | 初版 |

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

## 3. 改訂履歴

| Ver | 日付 | 変更 |
| --- | --- | --- |
| 初版 | 2026-06-03 | Phase 4 プロファイル追加（Step 1〜4 完了）を §0〜§2 に起こし。救急車 `ambulance` / 消防車 `fire_engine` / 災害用 `disaster` の設計値・根拠・トレードオフ・検証（753 pass）、Extractor 外部 JSON プロファイル対応（`ProfileResolver`）を記録。Step 5（利用者ガイド）/ Step 6（要件反映）の位置付けを §2.6 に追記。バージョンはユーザー採番 |
</content>
