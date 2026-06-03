# プロファイル活用ガイド

[English](profile_guide.en.md) | 日本語

OsmDotRoute の**車両プロファイル**を自作・適用・利用するまでの手順をまとめる。
「車両プロファイルとは何か」「組込みプロファイルの一覧と指定方法」の基本は
[使い方ガイド §6](usage_guide.md#6-プロファイルの指定方法) を参照。本ガイドはその先の、

1. 新しいプロファイルの作り方
2. 新プロファイルを適用した `.odrg` の作成
3. 新プロファイルでのルート探索
4. **未 bake の既存 `.odrg` で新プロファイルを使う際の留意点**（最重要）

を扱う。

> プロファイルは「どの道路を通れるか」「速度」「難所への反応」を JSON で定義する。
> 組込みプロファイルは `car` / `pedestrian` / `bicycle` / `truck` / `ambulance` / `fire_engine` / `disaster` の 7 種。

---

## 0. 前提：プロファイルはいつ評価されるか

プロファイルの値が経路計算に効くタイミングを理解しておくと、後述の留意点（§4）が腑に落ちる。

```text
[抽出時 = bake]                          [ランタイム = 探索時]
  Profiles/*.json（組込み）─┐
  ユーザー定義 JSON ───────┤→ 各エッジ × 各プロファイルを事前評価
  extractor --profiles ────┘    ↓
                              .odrg に (speedKmh, flags) を焼き込む
                              ※ OSM タグ辞書は .odrg に保存されない
                                                    ↓
                                  Router.Calculate(profile, ...)
                                    → プロファイル名でスロット解決 → 焼込値を直読（再評価なし）
```

ポイントは **「OSM タグの解釈は bake 時に 1 回だけ行われ、結果だけが `.odrg` に残る」** こと。
ランタイムは `.odrg` に焼かれた値をプロファイル名で引くだけで、JSON を再評価しない
（難所 `difficulty` の反応のみランタイムで JSON から読む。詳細は §4）。

---

## 1. 新しいプロファイルの作り方

プロファイルは 1 つの JSON ファイル。`car.json` 等の組込みプロファイルをひな型にして書き換えるのが早い。

### 1.1 JSON スキーマ全フィールド

| フィールド | 必須 | 型 | 内容 |
| --- | --- | --- | --- |
| `name` | ✓ | string | プロファイル名。**経路探索時およびスロット解決のキー**。`.odrg` 内で一意であること |
| `vehicleType` | | string | 車両区分（`motor_vehicle` 等）。アクセス階層の解釈に使う |
| `ignoreOneway` | | bool | 一方通行を無視するか（歩行者・緊急車両で `true`）。既定 `false` |
| `speedMultiplier` | | number | 全速度に掛ける係数（実走平均 ≒ 法定速度 × 0.75 なら `0.75`） |
| `accessTagKeys` | | string[] | 評価する access 系タグキー。**配列の後ろほど優先**（例: `["access","vehicle","motor_vehicle","emergency"]`） |
| `highway` | ✓ | object | `highway=*` ごとの `speedKmh`（速度）と `access`（`yes`/`no`） |
| `accessValueMap` | | object | access タグ値 → `allow` / `deny` の対応表 |
| `maxspeedTagKey` | | string | 速度制限を読むタグキー（通常 `maxspeed`） |
| `maxspeedUnitDefault` | | string | maxspeed の単位省略時の既定（`kmh`） |
| `fallback` | | object | `highway` に定義の無いタイプの既定 `{ speedKmh, access }` |
| `speedBounds` | | object | 速度の下限・上限クランプ `{ minKmh, maxKmh }` |
| `vehicleLimits` | | object | `maxWeightTon` / `maxHeightMeter` / `maxWidthMeter` を超過するエッジを通行不可化 |
| `difficulty` | | object | 難所タイプごとの `{ speedFactor, canPass }`。**ランタイムで評価される**（§4） |
| `difficultyDefault` | | object | `difficulty` に未定義の難所タイプの既定値 |

`highway` / `difficulty` の各エントリ:

| キー | 内容 |
| --- | --- |
| `speedKmh` | そのタイプの基準速度（km/h） |
| `access` | `yes`（通行可）/ `no`（通行不可） |
| `speedFactor` | 難所での速度係数（0.0〜1.0。0.5 なら半速） |
| `canPass` | 難所を通行できるか。`false` なら経路から除外 |

### 1.2 「通れる道」を増やすコツ — hard-deny の回避

通行可否は **bake 時に `highway[type].access` と access タグ評価の AND** で決まる。
「車では入れないが緊急車両なら入りたい」道（歩道など）を通すには、次の 2 つを両方満たす必要がある。

1. **`highway` 側で `access: "yes"` にする**
   `footway` / `path` / `pedestrian` を `{ "speedKmh": 10, "access": "yes" }` のように開放する。
   ここを `"no"`（= hard-deny）にすると、いくら access タグを許可しても通れない。
2. **access タグ評価を許可側に倒す**
   `accessTagKeys` に `emergency` を加え、`emergency=yes/designated` を `allow` として解釈させる。
   `accessValueMap` で `private` 等を `deny` にしていても、後優先の `emergency` キーが拾えば通行可になる
   （配列の後ろほど優先）。

> **車幅との整合に注意**: `vehicleLimits.maxWidthMeter` を設定した大型車（消防車など）で
> `footway` を開放すると、物理的に通れない細街路を経路に含めうる。
> その場合は `footway` の `speedKmh` を極端に低く（例 5）して**実質的に回避**させるか、
> 通したくないなら `access: "no"` に戻す。

### 1.3 vehicleLimits（寸法・重量制限）

`vehicleLimits` を設定すると、OSM の `maxweight` / `maxheight` / `maxwidth` タグが
車両諸元を下回るエッジが bake 時に通行不可化される。組込み緊急/災害プロファイルの値:

| プロファイル | maxWeightTon | maxHeightMeter | maxWidthMeter | 想定実車 |
| --- | --- | --- | --- | --- |
| `ambulance` | 4.0 | 2.6 | 2.0 | 高規格救急車 |
| `fire_engine` | 8.0 | 2.9 | 2.1 | 水槽付消防ポンプ車 |
| `disaster` | 20.0 | 3.8 | 2.5 | `truck` 同等（緊急通行車両・重機を含む） |

### 1.4 difficulty（難所耐性）

`difficulty` は動的制約（`RestrictedAreaService.AddDifficultyArea`）で登録された難所エリアに
エッジが入ったときの反応を定義する。**この値だけはランタイムで JSON から読まれる**（§4 参照）。
組込みの難所タイプは `flooding` / `liquefaction` / `landslide` / `construction` /
`obstacle` / `congestion` / `snow` / `ice`。

- `canPass: false` → そのエッジは経路から完全に除外（例: 全プロファイルで `landslide`＝土砂崩れは物理的に通行不可）
- `speedFactor` → 通れるが減速（小さいほど避けられやすい）

緊急/災害プロファイルは car より耐性を高めに設定している（小さい数値ほど避ける）。

### 1.5 組込み 3 プロファイルの設計例

実例として Phase 4 で追加した 3 つの設計意図を示す（フル JSON は
`src/OsmDotRoute/Profiles/{ambulance,fire_engine,disaster}.json`）。

| 観点 | `ambulance`（救急車） | `fire_engine`（消防車） | `disaster`（災害用車両） |
| --- | --- | --- | --- |
| ベース | `car` 相当 | `truck` 相当 | `truck` 相当 |
| `accessTagKeys` | `…,emergency` | `…,hgv,emergency` | `…,hgv,emergency` |
| `ignoreOneway` | **`true`**（逆走可） | **`true`**（逆走可） | `false`（規制は上位レイヤー責務） |
| footway/path | `access: yes`（10km/h） | `access: yes`（5km/h、徐行） | `access: no` |
| 寸法（vehicleLimits） | 小型 4.0t/2.6m/2.0m | 大型 8.0t/2.9m/2.1m | truck 同等 20t/3.8m/2.5m |
| 難所耐性 | car より高 | ambulance より控えめ（大型） | **最強化**（flooding/obstacle 等） |
| `landslide` | `canPass: false` | `canPass: false` | `canPass: false` |

設計判断の背景:

- **救急/消防を別プロファイルに分割**: 寸法と通行範囲が大きく異なるため（救急は小型で歩道侵入が現実的、消防は大型）。
- **`ignoreOneway`**: 緊急走行特例（道路交通法）を踏まえ救急/消防は逆走可。
  災害用は「どの区間を緊急交通路にするか」を動的制約（`RestrictedArea`）で上位レイヤーが制御する設計のため `false`。
- **`landslide` は全車 `canPass: false`**: 緊急/災害車両でも土砂崩れは物理的に通れない。

---

## 2. 新プロファイルを適用した `.odrg` の作成

**重要前提**: プロファイルは `.odrg` の**抽出（bake）時に焼き込まれる**。
ランタイムで使いたいプロファイルは、必ず抽出時に `--profiles` へ含めること（理由は §4）。

### 2.1 組込みプロファイルを bake する

組込み 7 種は名前で指定できる:

```powershell
dotnet run --project src/OsmDotRoute.Extractor -- `
  extract `
  --input  D:\osm\chubu-latest.osm.pbf `
  --output D:\odrg\tokyo.odrg `
  --bbox   139.74,35.65,139.79,35.70 `
  --profiles car,ambulance,fire_engine,disaster
```

### 2.2 ユーザー定義 JSON を bake する（Phase 4 で対応）

`--profiles` には**組込み名と外部 JSON ファイルパスを混在指定できる**。
パスは絶対・相対どちらでも可（`.json` ファイルが存在すればユーザー定義として読み込む）:

```powershell
dotnet run --project src/OsmDotRoute.Extractor -- `
  extract `
  --input  D:\osm\chubu-latest.osm.pbf `
  --output D:\odrg\tokyo.odrg `
  --bbox   139.74,35.65,139.79,35.70 `
  --profiles car,ambulance,.\profiles\my_delivery.json
```

- 外部 JSON の `name` フィールドが `.odrg` 内の**スロット名**になる。
  ランタイムでの参照キーもこの `name` なので、衝突しない一意な名前を付ける
  （組込み名と同じ `name` を付けると上書き／衝突の元になる）。
- JSON が不正な場合は CLI がエラー終了する（読込失敗の旨を表示）。
- 抽出後、`RouterDb.GetProfileNames()` で `.odrg` に焼かれた名前一覧を確認できる（§4.3）。

---

## 3. 新プロファイルでのルート探索

`.odrg` をロードし、bake 済みのプロファイルを指定して `Router.Calculate` を呼ぶ。

### 3.1 組込みプロファイル

組込みは `VehicleProfile` の静的プロパティで取得する:

```csharp
using OsmDotRoute;

var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");
var router = new Router(routerDb);

var from = new GeoCoordinate(35.68040208522669, 139.769056008911); // 緯度, 経度の順
var to   = new GeoCoordinate(35.659, 139.700);

// 組込みプロファイル（VehicleProfile.Ambulance / FireEngine / Disaster / Car ...）
var route = router.Calculate(VehicleProfile.Ambulance, from, to);

Console.WriteLine(route is null
    ? "経路なし"
    : $"距離 {route.TotalDistanceM:F0} m / 所要 {route.TotalDurationSec:F0} 秒");
```

| プロパティ | プロファイル名（`.odrg` 内） |
| --- | --- |
| `VehicleProfile.Car` | `car` |
| `VehicleProfile.Pedestrian` | `pedestrian` |
| `VehicleProfile.Bicycle` | `bicycle` |
| `VehicleProfile.Truck` | `truck` |
| `VehicleProfile.Ambulance` | `ambulance` |
| `VehicleProfile.FireEngine` | `fire_engine` |
| `VehicleProfile.Disaster` | `disaster` |

### 3.2 ユーザー定義プロファイル

外部 JSON は `LoadFromJsonFile` 等で読み込む。
**読み込んだプロファイルの `Name` が `.odrg` に bake 済みの名前と一致**していれば探索できる:

```csharp
var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");
var router = new Router(routerDb);

// 抽出時に bake したのと同じ JSON を読み込む（Name が一致する必要がある）
var custom = VehicleProfile.LoadFromJsonFile(@"D:\profiles\my_delivery.json");

var route = router.Calculate(custom, from, to);
```

> ランタイム側のプロファイル JSON は、`difficulty`（難所耐性）の評価にのみ実際に使われる。
> 通行可否・速度は `.odrg` の焼込値（= bake 時の JSON）で決まるため、**探索結果を変えたい場合は
> JSON を編集しただけでは不十分で、再 bake が必要**（§4）。

---

## 4. 未 bake の既存 `.odrg` で新プロファイルを使う際の留意点（最重要）

**結論: 新しいプロファイルを使うには、そのプロファイルを含めて `.odrg` を再生成（再 bake）する必要がある。**
既存の `.odrg` にプロファイルを「後付け」することはできない。理由と挙動を以下にまとめる。

### 4.1 なぜ後付けできないのか

`.odrg` は **OSM タグ辞書を保持しない**。プロファイルは bake 時に各エッジの
`(speedKmh, flags{CanPass, Forward, Backward})` へ事前計算され、その結果だけが `.odrg` に格納される。
タグが残っていないため、ランタイムで新しいプロファイルを当てて再評価することは原理的にできない。

### 4.2 未 bake プロファイルで探索したときの挙動（要注意）

未 bake のプロファイルで `Router.Calculate` を呼ぶと、**例外ではなく `null`（経路なし）が返る**。

- スナップ段階でプロファイル名のスロットが見つからず、起点/終点のスナップが `null` になる
  （`NativeRoadSnapper` の `HasProfile` チェック → `Router.Calculate` が早期に `null` を返す）。
- このため **「本当に経路が無い」のか「プロファイルが未 bake」なのか戻り値だけでは区別できない**。
  これが最大の落とし穴。
- 低レベルのグラフ API（`CanPass` / エッジ評価）を直接呼んだ場合のみ
  `InvalidOperationException`（"プロファイル '…' は .odrg の BAKED_PROFILE に存在しません。"）が送出される。
  通常の `Router.Calculate` 経路ではここに到達せず、サイレントに `null` になる。

### 4.3 対策: 事前に bake 済みプロファイル名を確認する

`null` の原因切り分けのため、探索前に `RouterDb.GetProfileNames()` で
`.odrg` に焼かれているプロファイル名を確認する（public API）:

```csharp
var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");

IReadOnlyList<string> baked = routerDb.GetProfileNames();
// 例: ["car", "ambulance", "fire_engine"]

if (!baked.Contains(profile.Name))
{
    Console.WriteLine(
        $"プロファイル '{profile.Name}' はこの .odrg に bake されていません。" +
        $"利用可能: {string.Join(", ", baked)}。--profiles に含めて再抽出してください。");
    return;
}

var route = router.Calculate(profile, from, to);
```

### 4.4 再 bake の手順

1. 使いたいプロファイル（組込み名 / 外部 JSON）を `--profiles` に含めて `.odrg` を作り直す（§2）。
2. 既存の `.odrg` を新しいものに差し替える。
3. ランタイムで同じ `name` を指定して探索する（§3）。

### 4.5 再 bake せずに使える軽度の変更（難所耐性の調整）

§4.1〜4.4 の「再 bake 必須」には**例外が 1 つ**ある。
**難所耐性（`difficulty` セクションの `speedFactor` / `canPass`）の変更だけは、再 bake せずに反映できる。**

理由は、この値だけが**ランタイムで生のプロファイル JSON から評価される**ため。
難所エリア（`RestrictedAreaService.AddDifficultyArea` 等で登録）と交差したエッジの減速・通行可否は、
探索時に `Router.Calculate` へ渡したプロファイルから都度評価される
（内部的には `EdgeWeightCalculator` が `profile` の難所評価を呼ぶ）。
一方、スナップ・通行可否・基準速度は `.odrg` の焼込値で決まる（§4.1）。

つまり **既存の `.odrg` はそのまま**にして、難所への反応だけ調整した JSON をランタイムで読み込めば反映される:

```csharp
var routerDb = RouterDb.LoadFromOdrg(@"D:\odrg\tokyo.odrg");  // 再生成不要
var router = new Router(routerDb);

// .odrg に bake 済みの ambulance を、難所反応だけ調整した JSON で差し替えて使う
// （name は "ambulance" のまま据え置くこと）
var tuned = VehicleProfile.LoadFromJsonFile(@"D:\profiles\ambulance_tuned.json");

var restrictions = new RestrictedAreaService();
restrictions.AddDifficultyArea(new MeshCode(53394611), DifficultyTypes.Flooding, tag: "flood");
var router2 = new Router(routerDb, restrictions);

var route = router2.Calculate(tuned, from, to);  // 調整後の flooding speedFactor が効く
```

#### この使い方の注意点（厳守）

1. **`name` を変えてはいけない（最重要）**
   ランタイムはプロファイルの `name` で `.odrg` の bake 済みスロットを引く。
   `name` を変えるとスロットが見つからず、スナップ段で `null`（= 経路なしと区別不可、§4.2）になる。
   難所値だけ編集し、`name` は bake 時と同一に保つこと。

2. **変更が反映されるのは `difficulty` / `difficultyDefault` だけ**
   それ以外のフィールド（`highway` の `access` / `speedKmh`、`vehicleLimits`、`accessTagKeys`、
   `accessValueMap`、`ignoreOneway`、`speedMultiplier`、`speedBounds`、`fallback`）を編集しても
   **ランタイムには一切反映されない**（bake 済み値が使われる）。
   JSON 上は変えたつもりでも挙動が変わらず、**「JSON と実挙動が食い違うサイレントな不整合」**になる。
   これらを変えたい場合は再 bake（§4.4）が必須。

3. **難所反応が効くのは難所エリアが登録されている時だけ**
   `speedFactor` / `canPass` は、対応する難所タイプのエリアが `RestrictedAreaService` に登録され、
   かつエッジがそれと交差したときにのみ適用される。難所エリアが無ければ基準速度（bake 値）のまま。

4. **新しい難所タイプ（キー）の追加もランタイムで効く**
   `difficulty` に新規キーを足し、対応する難所エリアを登録すれば、再 bake なしで反応する。
   プロファイルに未定義の難所タイプは `difficultyDefault`（既定 `speedFactor=1.0` / `canPass=true`）が適用される。

> **まとめ**: 「難所への効きを微調整したい」＝ 再 bake 不要（JSON の `difficulty` 編集＋同名で再読込）。
> 「通れる道・速度・寸法制限・逆走可否を変えたい」＝ **再 bake 必須**（§4.4）。

---

## 関連ドキュメント

- [使い方ガイド](usage_guide.md) — 最初に動かすまでの実用ガイド（プロファイル指定の基本は §6）
- [要件定義書](requirement_definition.md)
- [.odrg バイナリ形式仕様](phase2_graph_format_spec.md)
</content>
</invoke>
