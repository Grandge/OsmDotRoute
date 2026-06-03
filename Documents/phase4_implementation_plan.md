# Phase 4 実装計画書 — プロファイル追加

> 対象: REQ-PRF-005（救急/消防に分割）/ REQ-PRF-006 `disaster` ＋ユーザー定義プロファイル拡充
> 着手: 2026-06-03 / 設計根拠: [phase4_profile_research.md](phase4_profile_research.md)
> 本計画書は Phase 4 の **プロファイル追加** に限定する。「マルチプラットフォーム対応」は別計画書で扱う。

---

## 0. スコープ

### やること
- 同梱プロファイル **3種** の追加（2026-06-03 ユーザー決定で REQ-PRF-005 を救急/消防に分割）:
  - **救急車**（小型、REQ-PRF-005）
  - **消防車**（大型、REQ-PRF-005 から分割）
  - **災害用車両**（REQ-PRF-006 `disaster`）
- 上記を組込みプロファイルとして埋込リソース化し、Extractor CLI `--profiles` で bake 可能にする
- **Extractor CLI の外部 JSON プロファイルファイル対応**（Q4=実装。ユーザー定義プロファイルで odrg を焼けるように）
- ユーザー向け解説ドキュメント作成（プロファイルの作り方／odrg 焼成／探索／未 bake 時の留意点）
- Phase 4 設計書 `phase4_design.md` の起こし

### やらないこと（スコープ外）
- REQ-PRF-015（C# プラグイン拡張 API）/ REQ-PRF-016（Lua 互換層）— 「要望が出るまで未実装」継続
- マルチプラットフォーム対応（別計画書）
- 高速化（CH 等、計画削除済み）

### 確定した設計方針（2026-06-03 ユーザー決定）
| # | 論点 | 決定 |
|---|------|------|
| Q1 | emergency の車両寸法 | **救急車・消防車を別プロファイルに分割**。各々の実車諸元で vehicleLimits を設定 |
| Q2 | 通行範囲 | **歩道（footway/path）も限定速度で通行可** |
| Q3 | disaster の主軸 | **難所耐性中心**（vehicleLimits は truck 同等） |
| Q4 | CLI 外部プロファイル | **外部 JSON ファイル対応を実装** |

### 命名・ID 確定（2026-06-03 ユーザー決定）
- 救急車 = `ambulance` / 消防車 = `fire_engine` / 災害用 = `disaster`
- 要件 ID: **REQ-PRF-005 を救急車・消防車の両方に充てる**（ID 分割せず 1 要件 = 2 プロファイル）
- disaster の `landslide` は **canPass=false 維持**（土砂崩れは物理的に通行不可）

---

## 1. 現状把握（実装調査結果サマリ）

### 1.1 プロファイルのデータフロー

```
[抽出時 bake]
  Profiles/*.json (埋込) ──┐
  外部 JSON ファイル ──────┤→ VehicleProfile → ProfileEvaluator.Evaluate(tags)
                          │     ↓
  Extractor CLI --profiles ┘   ProfileBaker: 全エッジ×全プロファイルを事前評価
                                ↓
                              .odrg セクション 0x0007 (Baked Profile Table)
                                = プロファイル別 (speedKmh, flags{CanPass,Forward,Backward}) 8B/エッジ
                                ※ OSM タグ辞書は odrg に保存されない

[ランタイム探索]
  Router.Calculate(profile, from, to)
    → NativeRoadGraph.EvaluateByEdgeId(edgeId, evaluator)
      → _profileSlotByName[evaluator.Name] でスロット解決
        → 該当スロットの bake 済み値を直読（O(1)、再評価なし）
        → プロファイル名が未登録なら InvalidOperationException
```

### 1.2 重要な制約（ドキュメント留意点の根拠）

| # | 制約 | コード位置 |
|---|------|-----------|
| C1 | odrg は OSM タグを保持しない。bake 済み値のみ格納 | OdrgFormat.cs（タグセクション無し） |
| C2 | 未 bake プロファイルで探索 → `InvalidOperationException` | NativeRoadGraph.cs:457-461 |
| C3 | 未 bake プロファイルでスナップ → `null`（経路 null） | NativeRoadSnapper.cs:37 |
| C4 | 新プロファイル利用には odrg 再 bake が必須 | ProfileBaker.cs |
| C5 | difficulty は odrg にタイプ文字列を持ち、評価はランタイムで `EvaluateDifficulty()` | EdgeWeightCalculator.cs:134 |
| C6 | Extractor CLI は組込みプロファイル名のみ解決（外部ファイル不可）→ Step3 で解消 | Program.cs:121-129 `ResolveProfile` |

> C5 が示す通り、難所(difficulty)の評価規則だけはランタイムでプロファイル JSON から読まれる。ただしその前段のエッジ通行可否・速度（C1/C2）は bake 済み値依存のため、未 bake プロファイルは difficulty 評価に到達する前に例外となる。

### 1.3 組込みプロファイル追加で触る箇所

1. `src/OsmDotRoute/Profiles/X.json` を新規作成
2. `src/OsmDotRoute/OsmDotRoute.csproj` に `<EmbeddedResource Include="Profiles\X.json" />`
3. `src/OsmDotRoute/VehicleProfile.cs`: `Lazy<VehicleProfile>` + public static プロパティ追加
4. `src/OsmDotRoute.Extractor/Program.cs` `ResolveProfile`: 名前→プロファイルの分岐追加
5. テスト追加

---

## 2. プロファイル設計案

### 2.1 救急車（小型、REQ-PRF-005）

`car.json` ベース + 緊急走行特例 + 小型寸法。

| 項目 | 案 | 根拠（research §） |
|------|----|----|
| `vehicleType` | `motor_vehicle` | — |
| `ignoreOneway` | **`true`** | 一方通行逆走可（§2.1） |
| `accessTagKeys` | `["access", "vehicle", "motor_vehicle", "emergency"]` | emergency=yes をアクセスキー化（§1.1） |
| `accessValueMap` | car 同等 + `emergency:yes/designated → allow` | §1.1 |
| `highway` access | car 同等 + **footway/path/pedestrian を低速で access=yes**（Q2） | §1.1 + Q2 |
| `vehicleLimits` | 全高 2.6 / 全幅 2.0 / 重量 4.0t（高規格救急車 3.6t に余裕） | research §3 |
| `difficulty` | car より耐性高め。ただし `landslide` は canPass=false 維持（R2） | §2.1 |

### 2.2 消防車（大型、REQ-PRF-005 から分割）

`truck.json` ベース + 緊急走行特例 + 大型寸法。

| 項目 | 案 | 根拠 |
|------|----|----|
| `vehicleType` | `motor_vehicle` | — |
| `ignoreOneway` | **`true`** | §2.1 |
| `accessTagKeys` | `["access", "vehicle", "motor_vehicle", "emergency"]` | §1.1 |
| `highway` access | truck 同等 + **footway/path を超低速で access=yes**（Q2、車幅注意） | Q2 |
| `vehicleLimits` | 全高 2.9 / 全幅 2.1 / 重量 8.0t（水槽付消防車 ~8t） | research §3 |
| `difficulty` | 救急車と同等の耐性。`landslide` canPass=false（R2） | §2.1 |

### 2.3 災害用車両（REQ-PRF-006 `disaster`）

`truck.json` ベース + 難所耐性強化（Q3）。

| 項目 | 案 | 根拠 |
|------|----|----|
| `vehicleType` | `motor_vehicle` | 重機含む広い区分（§2.2） |
| `accessTagKeys` | `["access", "vehicle", "motor_vehicle", "emergency"]` | 緊急自動車包含（§2.2） |
| `vehicleLimits` | **truck 同等**（Q3=耐性中心、寸法緩和せず） | Q3 |
| `difficulty` | **`flooding`/`liquefaction`/`construction`/`obstacle` を speedFactor 高め。`landslide` は要判断（§3）** | §2.2 規制区間通行 |
| `ignoreOneway` | `false`（災害時の動的制御は上位レイヤー責務、R3） | R3 |

---

## 3. 命名・ID（確定）

| # | 項目 | 確定値 |
|---|------|--------|
| N1 | 救急車プロファイル名 | `ambulance` |
| N2 | 消防車プロファイル名 | `fire_engine` |
| N3 | 災害用車両プロファイル名 | `disaster` |
| N4 | 要件 ID | **REQ-PRF-005 を救急車・消防車の両方に充当**（ID 分割せず、1 要件 = 2 プロファイル） |
| N5 | disaster の `landslide` | **canPass=false 維持** |

---

## 4. 実装ステップ

> 各ステップ完了時にユーザー確認。`dotnet test` 全 pass 維持。バージョンはユーザー採番。

### Step 1: 救急車プロファイル（`ambulance`）
- `Profiles/ambulance.json` 作成
- csproj 埋込 + `VehicleProfile.Ambulance` + `ResolveProfile` 分岐
- 単体テスト: emergency=yes での access=private 通行、ignoreOneway 効果、footway 低速通行、vehicleLimits 発火、car との差分

### Step 2: 消防車プロファイル（`fire`）
- `Profiles/fire.json` 作成
- csproj 埋込 + `VehicleProfile.Fire` + `ResolveProfile` 分岐
- 単体テスト: 大型 vehicleLimits 発火、truck/ambulance との差分

### Step 3: 災害用車両プロファイル（`disaster`）
- `Profiles/disaster.json` 作成
- csproj 埋込 + `VehicleProfile.Disaster` + `ResolveProfile` 分岐
- 単体テスト: difficulty 耐性（flooding/liquefaction speedFactor）、truck との差分

### Step 4: Extractor CLI 外部プロファイル対応（Q4）
- `--profiles` でファイルパス（`.json`）を受理 → `VehicleProfile.LoadFromJsonFile`
- 組込み名とファイルパスの混在指定を許容（例: `--profiles car,./my_truck.json`）
- メタデータ JSON の `profiles` 配列にプロファイル名を記録
- 単体テスト: 外部プロファイルで bake → 探索成功

### Step 5: ユーザー向け解説ドキュメント（ユーザー要求）
`Documents/profile_guide.md`（日本語、公開 usage 系）を作成。章立て:
1. **新しいプロファイルの作り方**（JSON スキーマ、必須フィールド、ambulance/fire/disaster 実例）
2. **新プロファイルを適用した odrg の作成方法**（Extractor `--profiles`、組込み名/外部ファイル、再 bake）
3. **新プロファイルでのルート探索方法**（`VehicleProfile` ロード → `Router.Calculate`）
4. **未 bake の既存 odrg で新プロファイルを使う際の留意点**（C1〜C4：例外になる／odrg 再生成が必須／odrg に焼かれたプロファイル名の確認方法）

### Step 6: 設計書・要件反映
- `Documents/phase4_design.md` 起こし（§0 方針 + プロファイル章）
- requirement_definition.md: REQ-PRF-005 を「救急車(ambulance)・消防車(fire_engine)」に更新し `[x]` 化、REQ-PRF-006(disaster) を `[x]` 化（バージョンはユーザー採番）
- 改訂履歴更新

---

## 5. テスト方針

- 既存全テスト（693 pass 基準）維持
- 新規 3 プロファイルの ProfileEvaluator 単体テスト（tag → EdgeEvaluation）
- bake 統合テスト: 3 プロファイルを含む odrg を生成し探索成功を確認
- CLI 外部プロファイルの bake → 探索 統合テスト（Step4）
- 回帰: 既存 car/pedestrian/bicycle/truck の評価結果不変

## 6. リスク・留意

- **R2 緊急車両の landslide**: 救急車・消防車でも土砂崩れは物理的に通行不可。`landslide` は canPass=false 維持を推奨。disaster は N5 で判断。
- **R3 disaster と動的制約の責務**: disaster は「難所を通れる車両特性」に限定。災害規制区間の動的な指定（緊急交通路）は上位レイヤー（親プロの RestrictedArea 付け外し）の責務とし、本プロファイルでは ignoreOneway=false。
- **R4 footway 通行（Q2）と車幅**: 消防車（幅 2.1m）が footway を通行可にすると非現実的経路を生む恐れ。footway は超低速 + difficulty で実質回避させるか、vehicleLimits 物理判定との整合を Step2 で検証。
- **R5 CLI 外部プロファイルの名前衝突**: 外部ファイル名と組込み名の衝突、bake テーブルのプロファイル名一意性を Step4 で担保。

---

## 7. 改訂履歴

| Ver | 日付 | 変更 |
|-----|------|------|
| v0.1 | 2026-06-03 | 初版ドラフト |
| v0.2 | 2026-06-03 | Q1-Q4 確定反映。emergency を救急/消防に分割（3プロファイル）、footway 通行可、disaster 難所耐性中心、CLI 外部プロファイル対応を採用 |
