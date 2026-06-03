# Phase 4 プロファイル追加 — Web 調査結果

> **位置づけ**: 内部調査メモ（Phase 4 計画書 `phase4_implementation_plan.md` の設計根拠）。
> 公開文書（README / usage_guide）からはリンクしない。OSS 公開時の棚卸し対象。
> 調査日: 2026-06-03 / 対象: REQ-PRF-005 `emergency` / REQ-PRF-006 `disaster`

---

## 0. 調査の目的

Phase 4 で追加する 2 プロファイルの設計根拠を Web から収集する。

- **REQ-PRF-005 `emergency`**: 同梱車両プロファイル「緊急車両（救急車・消防車相当）」
- **REQ-PRF-006 `disaster`**: 同梱車両プロファイル「災害用車両（特殊許可ルート・通行制限緩和）」

既存スキーマ（`src/OsmDotRoute/Profiles/*.json`）の機構を流用する前提で、設計に効く論点を抽出する。

---

## 1. OSM の emergency 関連タグ

### 1.1 `emergency=*`（アクセス用途クラスとしての解釈）

出典: [Key:emergency — OSM Wiki](https://wiki.openstreetmap.org/wiki/Key:emergency) / [Tag:emergency=yes — OSM Wiki](https://wiki.openstreetmap.org/wiki/Tag:emergency=yes)

- ルーティングで意味を持つ主値は **`emergency=yes`**。「当該 `highway=*` が緊急業務の車両にアクセス可能であること」を示す。明示的指定路には **`emergency=designated`**。
- `emergency=yes` の路は「**通行不能な障害物・物理的障害が無い**と合理的に仮定できる」（明示マッピングが無い限り）。
- **アクセス階層との関係**: `emergency=yes` は `access=*` 階層と同様の**車両用途クラスのアクセスキー**として機能する。`access=private` 等の制限付きの路でも `emergency=yes` を併記することで「緊急車両は支障なく通行できる」ことを明示する。
- **物理制約は上書きしない**: `emergency=yes` は**法的制限のみ**を上書きする趣旨。物理的制約（重量・高さ等）を超えることは意味しない。重量等の法的上限を安全に上書きする場合は、`maxweight:emergency=none` のような**条件付きタグ**を使う（`emergency=yes` 単独に依存しない）。

> **引用要点**: "a given `highway=*` is accessible to the vehicles of emergency services" / "assumed to lack any impassable barriers and physical obstacles"

### 1.2 バリア node の扱い

出典: [Key:emergency — OSM Wiki](https://wiki.openstreetmap.org/wiki/Key:emergency)

- emergency アクセスを持つ路上にバリア node がある場合、緊急業務はそのバリアを通過する手段を持つことを意味する（緊急車両用に下がる自動ボラード、緊急業務がパスキーを持つゲート/ボラード等）。

### 1.3 `service=emergency_access`

出典: [Tag:service=emergency_access — OSM Wiki](https://wiki.openstreetmap.org/wiki/Tag:service=emergency_access)

- 消防車のアクセス路は `highway=service` + `service=emergency_access` の組合せで記録される。

---

## 2. 日本の法的位置づけ

### 2.1 緊急自動車（道路交通法）— `emergency` の根拠

出典: [緊急車両 — Wikipedia](https://ja.wikipedia.org/wiki/緊急車両) / [道路交通法施行令 第13条（緊急自動車）— 警視庁 PDF](https://www.keishicho.metro.tokyo.lg.jp/tetsuzuki/kotsu/application/kinkyu.files/sekourei.pdf)

- 定義（道路交通法）: 「消防用自動車、救急用自動車その他の政令で定める自動車で、緊急用務のため運転中のもの」。施行令第13条で具体列挙（消防・救急・警察車両等）。
- **通行特例**（サイレン＋赤色灯火で運行中に適用）:
  - 一方通行の**逆走可**
  - 通行禁止・**進入禁止路の通行可**
  - **最高速度の緩和**（制限速度の適用除外）
  - 赤信号の通過
  - 路肩通行

> **経路探索への含意**: 一方通行・進入禁止・通行禁止という**法的制限**を無視できる。一方、橋の重量制限などの**物理制約は遵守**する（§1.1 と整合）。

### 2.2 緊急通行車両（災害対策基本法）— `disaster` の根拠

出典: [緊急車両 — Wikipedia](https://ja.wikipedia.org/wiki/緊急車両) / [災害時における緊急通行車両等の手続きについて — 警視庁](https://www.keishicho.metro.tokyo.lg.jp/tetsuzuki/kotsu/saigaisharyo.html)

- 定義（災害対策基本法）: 「道路交通法上の緊急自動車、その他災害応急対策の的確かつ円滑な実施のため**特に通行を確保する必要がある車両**」。緊急自動車より**広い区分**（電力・通信事業者車両、医療搬送、建設重機等を含む）。
- 大規模災害時は災害対策基本法に基づく**交通規制**が実施され、一般車両の通行が禁止・制限される。災害応急対策に従事する車両は、所定の手続きで**標章・確認証**を受けて規制区間を通行できる。
- **緊急交通路**: 災害時に指定される緊急交通路では、災害応急対策従事車両（緊急自動車＋標章掲示車両）**のみ通行可**、それ以外は通行禁止。

> **経路探索への含意**: `disaster` の本質は「**通常は通行制限された区間（災害規制・難所）を通行できる**」緩和特性。OsmDotRoute では難所エリア（`RestrictedArea` / プロファイル `difficulty`）への耐性が高い車両として表現するのが自然。

---

## 3. 車両諸元（vehicleLimits の根拠）

出典: [高規格救急車紹介 — 東京消防庁](https://www.tfd.metro.tokyo.lg.jp/fs/oume/about/ambu.html) / [水槽付消防車 MTX 仕様 — モリタ](https://www.morita119.jp/fire_engine/tank/003spec.html) / [消防車両の諸元 — 阿南市](https://www.city.anan.tokushima.jp/syoubou/syoubousyo/nishi/syaryo.html) ほか各消防本部公開値

| 車種 | 全長 | 全幅 | 全高 | 車両総重量 |
|------|------|------|------|-----------|
| 高規格救急車 | 5.65 m | 1.89 m | 2.51 m | 3.6 t |
| 水槽付消防ポンプ自動車 | 6.72 m | 1.89 m | 2.77 m | 7,990 kg |
| 消防ポンプ自動車 | 6.72 m | 1.89 m | 2.85 m | 6,435 kg |
| 積載車 | 5.20 m | 1.69 m | 2.89 m | 5,095 kg |
| 指揮車 | 5.38 m | 1.88 m | 2.41 m | 2,790 kg |

> **論点**: 「救急車・消防車相当」を 1 プロファイルで表すため、**包含（消防車基準＝全高 ~2.85m / 総重量 ~8t）か、救急車基準（小型）か**を計画書で決める。
> 比較: 既存 `truck.json` は `maxWeightTon: 20.0 / maxHeightMeter: 3.8 / maxWidthMeter: 2.5`。消防車は truck より小型のため、emergency の vehicleLimits は truck より小さい値が妥当。

---

## 4. 既存ルーティングエンジンの emergency プロファイル状況

出典: [foss_routing_engines_overview — gis-ops/tutorials](https://github.com/gis-ops/tutorials/blob/master/general/foss_routing_engines_overview.md) / [Work towards more configurable vehicle profiles — GraphHopper Forum](https://discuss.graphhopper.com/t/work-towards-more-configurable-vehicle-profiles/4708)

- **GraphHopper**: Contraction Hierarchies + 柔軟なプロファイル。"custom model"（エッジ重みを変更する JSON）で調整。
- **OSRM**: Lua ベースのプロファイル。デフォルトは car / bike / foot。Lua 編集で道路種別ペナルティを調整。
- **Valhalla**: 実行時 dynamic costing。全プロファイルが同一グラフを共有し、ペナルティ/コストで経路に影響。
- いずれも**専用の emergency プロファイルを標準同梱していない**（公開ドキュメント上）。emergency 対応は各自カスタムモデル / Lua / costing で実装する形。

> **含意**: OsmDotRoute が `emergency` / `disaster` を**同梱 JSON プロファイル**として提供するのは差別化要素になりうる（特に日本の災害ユースケース文脈）。

---

## 5. 既存スキーマへのマッピング（設計反映の方向性）

既存 `truck.json` の全フィールドを流用可能。新フィールドの追加は不要の見込み。

### 5.1 `emergency` 反映案

| スキーマ項目 | 案 | 根拠 |
|------------|----|----|
| `ignoreOneway` | **`true`** | 一方通行逆走可（§2.1） |
| `accessTagKeys` | `["access", "vehicle", "motor_vehicle", "emergency"]` | `emergency=yes` をアクセスキー化（§1.1） |
| `accessValueMap` | `emergency=yes/designated` → allow、`access=no/private` でも highway 既定で通行可寄り | 進入禁止・通行禁止の通行可（§2.1） |
| `highway` access | footway/path/steps の扱いが論点（物理的に通れるか） | 物理制約は遵守（§1.1） |
| `speedBounds.maxKmh` | car と同等以上 | 最高速度緩和（§2.1） |
| `vehicleLimits` | §3 の車両基準で決定（論点1） | 物理制約は遵守 |
| `difficulty` | car〜truck 中間〜耐性高め | 緊急走行は難所も通る要請 |

### 5.2 `disaster` 反映案

| スキーマ項目 | 案 | 根拠 |
|------------|----|----|
| `difficulty` | `landslide` / `flooding` / `liquefaction` を **canPass=true** 寄り・speedFactor 高め | 災害規制区間・難所の通行（§2.2） |
| `vehicleLimits` | truck 級以上 or 緩め（重機含む） | 緊急通行車両は重機含む（§2.2） |
| `accessTagKeys` | `emergency` キー併用も検討 | 緊急自動車を包含（§2.2） |
| `ignoreOneway` | 要検討（災害時の運用次第） | — |

---

## 6. 計画書で詰める設計論点

1. **emergency の車両寸法基準**: 消防車包含（全高 ~2.85m / 総重量 ~8t）か、救急車基準（小型 / 総重量 ~3.6t）か。
2. **emergency の `access=no` 通行範囲**: `emergency=yes` 明示路のみ許可か、全 highway を広く許可（footway/path も通すか）か。物理的妥当性とのバランス。
3. **disaster の表現主軸**: `difficulty` 耐性中心か、`vehicleLimits` 緩和も含めるか。emergency との差別化をどこで付けるか。
4. **動的制約（`RestrictedArea`）との関係**: disaster の「規制区間通行可」を、プロファイル `difficulty` で表すか、上位レイヤー（親プロの災害シナリオ）で `RestrictedArea` の付け外しで表すかの責務分担。

---

## 7. 出典一覧

### OSM
- [Key:emergency — OSM Wiki](https://wiki.openstreetmap.org/wiki/Key:emergency)
- [Tag:emergency=yes — OSM Wiki](https://wiki.openstreetmap.org/wiki/Tag:emergency=yes)
- [Tag:service=emergency_access — OSM Wiki](https://wiki.openstreetmap.org/wiki/Tag:service=emergency_access)
- [DE:Emergency Routing — OSM Wiki](https://wiki.openstreetmap.org/wiki/DE:Emergency_Routing)

### 日本の法令・行政
- [緊急車両 — Wikipedia](https://ja.wikipedia.org/wiki/緊急車両)
- [道路交通法施行令 第13条（緊急自動車）— 警視庁 PDF](https://www.keishicho.metro.tokyo.lg.jp/tetsuzuki/kotsu/application/kinkyu.files/sekourei.pdf)
- [災害時における緊急通行車両等の手続きについて — 警視庁](https://www.keishicho.metro.tokyo.lg.jp/tetsuzuki/kotsu/saigaisharyo.html)
- [緊急自動車専用路・緊急交通路 — 警視庁](https://www.keishicho.metro.tokyo.lg.jp/kurashi/saigai/shinsai_kisei/emergency.html)
- [緊急交通路と緊急通行車両等の取扱いについて — 奈良県警 PDF](https://www.police.pref.nara.jp/cmsfiles/contents/0000000/275/01_toriatsukai.pdf)

### 車両諸元
- [高規格救急車紹介 — 東京消防庁](https://www.tfd.metro.tokyo.lg.jp/fs/oume/about/ambu.html)
- [水槽付消防車 MTX 仕様 — 株式会社モリタ](https://www.morita119.jp/fire_engine/tank/003spec.html)
- [消防車両の諸元 — 阿南市](https://www.city.anan.tokushima.jp/syoubou/syoubousyo/nishi/syaryo.html)
- [いろいろな消防車 — 甲府地区広域行政事務組合消防本部](https://www.kfd.or.jp/?page_id=566)

### ルーティングエンジン比較
- [foss_routing_engines_overview — gis-ops/tutorials](https://github.com/gis-ops/tutorials/blob/master/general/foss_routing_engines_overview.md)
- [Work towards more configurable vehicle profiles — GraphHopper Forum](https://discuss.graphhopper.com/t/work-towards-more-configurable-vehicle-profiles/4708)
