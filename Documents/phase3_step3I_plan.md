# Phase 3 ステップ 3I: Sandbox ユーザー試用デモツール 計画書

**ステータス**: v0.2（3I 全完了、2026-05-28）
**対応ステップ**: Phase 3 ステップ 3I（[Phase 3 実装計画書 §3.8 / §6](phase3_implementation_plan.md)）
**対応要件**: REQ-INT-001〜003（API 設計）、REQ-LIC-004（ODbL 表記）
**関連文書**:

- [Phase 3 設計書 §10](phase3_design.md)（本ステップで肉付け対象）
- [Phase 3 実装計画書 §3.8](phase3_implementation_plan.md)（Sandbox 概要・機能一覧）
- [Phase 3 実装計画書 §5.5-30〜34](phase3_implementation_plan.md)（ユーザー判断確定済み）
- MapVerifier 実装（`samples/MapVerifier/`）— 構成踏襲元

---

## 1. 目的とゴール

**目的**: OSS 公開時の「Try it」キラーデモとして、OsmDotRoute の動的制約ルーティングを 1 ツールで体験できる Web UI を提供する。PBF ダウンロード → bbox 抽出 → 経路探索 → メッシュ/ポリゴン制約付与 → Re-Route の一連フローを実演可能にする。

**配置**:

| ディレクトリ | 内容 |
|---|---|
| `samples/Sandbox/Server` | ASP.NET Core minimal API（.NET 9.0） |
| `samples/Sandbox/Web` | React + TypeScript + Vite + MapLibre GL |

**Done 判定**:

1. `dotnet run --project samples/Sandbox/Server` + `npm run dev`（Web）で起動 → ブラウザで地図表示
2. Geofabrik 都道府県一覧から PBF ダウンロード → 進捗表示 → ローカルキャッシュ
3. マップ上の矩形描画で bbox 指定 → プロファイル選択 → `.odrg` 抽出（進捗表示）
4. 2 点指定 → 経路計算 → マップ上に経路描画 + 距離/所要時間表示
5. メッシュグリッド表示（1 km / 500 m / 250 m 切替）→ メッシュクリックで Block/Difficulty 付与
6. ポリゴン描画 → 難所タイプ選択 → Block/Difficulty 付与
7. Re-Route で制約回避経路を確認
8. 設計書 `phase3_design.md` §10 が肉付けされる
9. OsmDotRoute `dotnet test` **672 件 pass** を維持

---

## 2. §5.5 ユーザー判断（全件確定済み）

| # | 項目 | 確定案 | 根拠 |
|---|---|---|---|
| 30 | PBF ダウンロード元 | **(a) Geofabrik 固定** | 安定実績、R11 対処（User-Agent + If-Modified-Since） |
| 31 | Extractor 統合方式 | **(a) 同プロセス内呼出** | プロジェクト参照で `ExtractPipeline.Run()` 直接呼出。`Task.Run` + `IProgress<T>` で非同期化、SSE で UI に進捗 push |
| 32 | キャッシュ場所 | **(a) `%LOCALAPPDATA%/OsmDotRoute.Sandbox/cache`** | Windows 標準パス。PBF と `.odrg` を分離管理 |
| 33 | 制約永続化 | **(a) セッション中のみ** | デモ用途に十分。リロードで消失 |
| 34 | メッシュ表示パフォ | **(c) 両方** | ズームレベル閾値で自動非表示 + ユーザー手動 ON/OFF |

---

## 3. 技術構成

### 3.1 計画書記載との差異

計画書 §3.8 では「静的 HTML / JS + Leaflet」と記載されているが、MapVerifier の実態が **React + TypeScript + Vite + MapLibre GL** であり、コンポーネント再利用（MapView、MeshGridPanel、PolygonEditorPanel 等）による開発効率を優先して **React + MapLibre GL 構成**を採用する（ユーザー確認済み 2026-05-28）。

### 3.2 MapVerifier からの再利用方針

**コピーして改変する**（MapVerifier を壊さず独立進化）。

| MapVerifier 資産 | Sandbox での扱い |
|---|---|
| `MapView.tsx` | **流用**（レイヤー構成はそのまま、bbox 矩形描画機能を追加） |
| `MeshGridPanel.tsx` | **流用**（API パスを差替） |
| `PolygonEditorPanel.tsx` | **流用**（API パスを差替） |
| `RestrictionListPanel.tsx` | **流用** |
| `client.ts` | **流用**（Sandbox 固有エンドポイント追加） |
| `App.tsx` | **参考にして新規**（パネル構成が異なる：DL + Extract パネル追加、Load パネル不要） |
| `styles.ts` | **流用** |
| Server `RouterState.cs` | **参考にして新規**（`SandboxState.cs`：RouterDb + Router + 抽出状態を統合管理） |
| Server `MeshEndpoints.cs` | **流用**（Utf8JsonWriter パターン） |
| Server `RestrictionEndpoints.cs` | **流用** |
| Server `SnapAndRouteEndpoints.cs` | **流用** |

**新規実装**:

| コンポーネント | 内容 |
|---|---|
| `DownloadPanel.tsx` | Geofabrik 都道府県プルダウン + DL ボタン + 進捗バー |
| `ExtractPanel.tsx` | bbox 表示 + プロファイル選択 + Extract ボタン + 進捗 |
| `BboxDrawControl` | MapLibre GL 上の矩形描画（MapView に統合） |
| Server `DownloadEndpoints.cs` | PBF DL 開始 / 進捗 SSE / キャッシュ確認 |
| Server `ExtractEndpoints.cs` | Extractor 同プロセス呼出 / 進捗 SSE |
| Server `GeofabrikService.cs` | 都道府県 URL 一覧 / HttpClient + If-Modified-Since |
| Server `CacheService.cs` | PBF / .odrg キャッシュ管理 |

### 3.3 サーバー構成

```
Sandbox.Server (ASP.NET Core minimal API, .NET 9.0)
├── Program.cs              # DI + エンドポイント登録 + Kestrel 127.0.0.1 バインド
├── Services/
│   ├── SandboxState.cs     # RouterDb / Router / 抽出状態の統合管理
│   ├── GeofabrikService.cs # 都道府県 PBF URL 一覧 + HttpClient DL
│   ├── CacheService.cs     # %LOCALAPPDATA% キャッシュ管理
│   └── GeoJsonConverter.cs # Route → GeoJSON（MapVerifier 流用）
├── Endpoints/
│   ├── VersionEndpoints.cs
│   ├── DownloadEndpoints.cs  # POST /api/download, GET /api/download/progress (SSE)
│   ├── ExtractEndpoints.cs   # POST /api/extract, GET /api/extract/progress (SSE)
│   ├── GraphEndpoints.cs     # GET /api/graph/stats, GET /api/road-network
│   ├── RouteEndpoints.cs     # POST /api/snap, POST /api/route
│   ├── MeshEndpoints.cs      # GET /api/mesh/grid
│   └── RestrictionEndpoints.cs # CRUD /api/restrictions
└── Contracts/
    └── Dtos.cs
```

### 3.4 Web 構成

```
Sandbox.Web (React + TypeScript + Vite)
├── index.html
├── vite.config.ts          # Port 5174, /api → http://127.0.0.1:5280 プロキシ
├── package.json
├── tsconfig.json
└── src/
    ├── main.tsx
    ├── App.tsx              # パネル統合（DL → Extract → Route → Mesh/Polygon）
    ├── api/
    │   └── client.ts        # fetch + SSE クライアント
    └── components/
        ├── MapView.tsx       # MapLibre GL（bbox 矩形描画機能追加）
        ├── DownloadPanel.tsx  # PBF DL UI
        ├── ExtractPanel.tsx   # 抽出 UI
        ├── RoutePanel.tsx     # 経路計算 UI
        ├── MeshGridPanel.tsx  # メッシュ表示 + 制約
        ├── PolygonEditorPanel.tsx # ポリゴン描画 + 制約
        ├── RestrictionListPanel.tsx # 制約一覧
        └── styles.ts
```

### 3.5 ポート割り当て

| ツール | Server | Web (Vite) |
|---|---|---|
| MapVerifier | 5279 | 5173 |
| Sandbox | 5280 | 5174 |

---

## 4. サブステップ

### 3I.1: プロジェクト雛形

**ゴール**: `dotnet run` + `npm run dev` で起動 → ブラウザでブランクマップ（日本中心）が表示される。

1. `samples/Sandbox/Server/` に ASP.NET Core minimal API プロジェクト作成
   - csproj: .NET 9.0、プロジェクト参照 `OsmDotRoute` + `OsmDotRoute.Extractor`
   - Program.cs: CORS / ResponseCompression / Kestrel `127.0.0.1:5280` バインド
   - `GET /api/version` エンドポイントのみ
2. `samples/Sandbox/Web/` に Vite + React + TypeScript プロジェクト作成
   - `npm create vite@latest` → React + TypeScript テンプレート
   - `maplibre-gl` インストール
   - vite.config.ts: port 5174、`/api` → `http://127.0.0.1:5280` プロキシ
3. MapVerifier から `MapView.tsx`、`styles.ts`、`client.ts` をコピーして最小改変
4. sln にプロジェクト追加
5. 起動確認（ブランクマップ表示）

### 3I.2: PBF ダウンロード + bbox 範囲指定 UI

**ゴール**: 都道府県選択 → PBF DL（進捗バー）→ マップ上で矩形描画 → bbox 確定。

**サーバー側**:
1. `GeofabrikService.cs`: 47 都道府県 + 日本全国の URL 定義（`download.geofabrik.de/asia/japan/` 配下）
   - `GET /api/regions` → 地域一覧 JSON
2. `CacheService.cs`: `%LOCALAPPDATA%/OsmDotRoute.Sandbox/cache/` に PBF / .odrg 保存
   - 既存キャッシュ確認 / ファイルサイズ取得
3. `DownloadEndpoints.cs`:
   - `POST /api/download` → PBF DL 開始（`HttpClient` + `IProgress<T>`）
   - `GET /api/download/progress` → SSE で進捗 push（バイト数 / 総バイト数）
   - `GET /api/download/status` → キャッシュ済み PBF 一覧
4. User-Agent ヘッダ: `OsmDotRoute.Sandbox/0.1 (https://github.com/xxx/OsmDotRoute)`
5. `If-Modified-Since` でキャッシュ有効性確認

**Web 側**:
1. `DownloadPanel.tsx`: 都道府県プルダウン + DL ボタン + 進捗バー + キャッシュ状態表示
2. `MapView.tsx` に bbox 矩形描画機能追加:
   - 描画モード ON → マップ上で 2 点ドラッグ → 矩形表示 → bbox（west,south,east,north）確定
   - 確定した矩形はレイヤーとして残す（青破線）

### 3I.3: .odrg 抽出パイプライン統合

**ゴール**: bbox + プロファイル指定 → `.odrg` 生成（進捗表示）→ グラフロード → 道路ネットワーク表示。

**サーバー側**:
1. `ExtractEndpoints.cs`:
   - `POST /api/extract` → `Task.Run` で `ExtractPipeline.Run()` 呼出
     - 入力: PBF パス（キャッシュ済み）、bbox、profiles
     - 出力: `.odrg` パス（キャッシュディレクトリ）
   - `GET /api/extract/progress` → SSE で進捗 push（Pass 1/2/3 + 処理件数）
   - `ExtractPipeline` に `IProgress<T>` を渡す（既存 API にない場合は追加検討）
2. 抽出完了後、自動で `RouterDb.LoadFromOdrg()` → `SandboxState` に登録
3. `GraphEndpoints.cs`:
   - `GET /api/graph/stats` → 頂点数 / エッジ数 / bbox
   - `GET /api/road-network` → GeoJSON（MapVerifier の RoadNetworkEndpoints 流用）

**Web 側**:
1. `ExtractPanel.tsx`:
   - bbox 表示（3I.2 で確定した値）
   - プロファイル選択（car / pedestrian / bicycle / truck チェックボックス、デフォルト car + pedestrian）
   - Extract ボタン + 進捗バー
   - 完了後: マップに道路ネットワーク表示 + 統計パネル

**ExtractPipeline への IProgress 追加**:
- `ExtractPipeline.Run()` の現在のシグネチャを確認し、`IProgress<ExtractProgress>` パラメータを追加可能か判断
- 不可能な場合: 子プロセス方式にフォールバック（§5.5-31 の候補 (b)）ではなく、Pipeline 内部にコールバック機構を追加

### 3I.4: ルート探索 UI

**ゴール**: 2 点指定 → 経路計算 → マップ上に経路描画 + 距離/所要時間表示。

**サーバー側**:
1. `RouteEndpoints.cs`（MapVerifier の SnapAndRouteEndpoints 流用）:
   - `POST /api/snap` → 座標スナップ
   - `POST /api/route` → 経路計算（profile 指定）→ GeoJSON 返却
2. `GeoJsonConverter.cs`（MapVerifier 流用）

**Web 側**:
1. `RoutePanel.tsx`（MapVerifier 流用、改変最小）:
   - プロファイル選択ドロップダウン
   - From / To ピックモード → マップクリックで設定
   - Route ボタン → 経路描画（緑線）
   - 距離 / 所要時間 / エッジ数の表示
   - Re-Route ボタン（制約変更後の再計算）

### 3I.5: メッシュ + ポリゴン制約

**ゴール**: メッシュ / ポリゴンで動的制約を付与 → Re-Route で制約回避経路を確認。

**サーバー側**:
1. `MeshEndpoints.cs`（MapVerifier 流用）:
   - `GET /api/mesh/grid` → メッシュグリッド GeoJSON
2. `RestrictionEndpoints.cs`（MapVerifier 流用）:
   - `POST /api/restrictions/polygon` → ポリゴン制約登録
   - `POST /api/restrictions/mesh` → メッシュ制約登録
   - `GET /api/restrictions` → 制約一覧
   - `GET /api/restrictions/geojson` → 制約 GeoJSON
   - `DELETE /api/restrictions/{id}` → 個別削除
   - `DELETE /api/restrictions` → 全削除

**Web 側**:
1. `MeshGridPanel.tsx`（MapVerifier 流用）:
   - メッシュ表示 ON/OFF トグル（手動 ON/OFF: §5.5-34 確定）
   - メッシュレベル切替（1 km / 500 m / 250 m）
   - ズームレベル閾値で自動非表示（§5.5-34 確定）:
     - zoom < 12 → 1 km メッシュのみ許可
     - zoom < 14 → 500 m メッシュのみ許可
     - zoom < 16 → 250 m メッシュのみ許可
   - メッシュクリック → Block / Difficulty 選択 → 難所タイプ選択（組込み 8 種）
2. `PolygonEditorPanel.tsx`（MapVerifier 流用）:
   - ポリゴン描画ツール（MapVerifier の既存実装）
   - 難所タイプセレクタ（Block / Difficulty + 8 種）
3. `RestrictionListPanel.tsx`（MapVerifier 流用）:
   - 登録済み制約一覧
   - 個別削除 / 一括クリア

---

## 5. Geofabrik 都道府県 URL 一覧

Geofabrik Japan extracts のパス規則: `https://download.geofabrik.de/asia/japan/{region}-latest.osm.pbf`

| カテゴリ | region 名 | 備考 |
|---|---|---|
| 日本全国 | `japan` | 約 1.8 GB |
| 北海道 | `hokkaido` | |
| 東北 | `tohoku` | 青森・岩手・宮城・秋田・山形・福島 |
| 関東 | `kanto` | 茨城・栃木・群馬・埼玉・千葉・東京・神奈川 |
| 中部 | `chubu` | 新潟・富山・石川・福井・山梨・長野・岐阜・静岡・愛知 |
| 近畿 | `kansai` | 三重・滋賀・京都・大阪・兵庫・奈良・和歌山 |
| 中国 | `chugoku` | 鳥取・島根・岡山・広島・山口 |
| 四国 | `shikoku` | 徳島・香川・愛媛・高知 |
| 九州 | `kyushu` | 福岡・佐賀・長崎・熊本・大分・宮崎・鹿児島・沖縄 |

**注**: Geofabrik の日本 extracts は**地方ブロック単位**（都道府県単位ではない）。UI 表示は「地方ブロック」プルダウンに変更し、Extractor の bbox で都道府県相当の範囲を切り出す運用とする。

---

## 6. セキュリティ・運用方針

| 項目 | 方針 |
|---|---|
| ネットワーク | Kestrel `127.0.0.1` バインド固定。`0.0.0.0` 不可。外部公開機能なし |
| CORS | `http://localhost:5174` のみ許可 |
| 認証 | なし（localhost 前提） |
| ODbL 表記 | 地図タイル帰属 + PBF DL 時に Geofabrik / OSM 帰属表示 |
| README | 「localhost only、本番運用不可」を冒頭に明記 |
| User-Agent | PBF DL 時に `OsmDotRoute.Sandbox/{version}` 明示 |
| キャッシュ上限 | 設けない（ユーザーが手動管理）。キャッシュ一覧 + 個別削除 UI を提供 |

---

## 7. スコープ外（別ステップへ分離）

| 項目 | 分離先 | 理由 |
|---|---|---|
| GitHub Pages デモ（Blazor WASM） | **ステップ 3J**（実装計画書 §3.9） | Sandbox のサブセット機能を WASM 化してブラウザ即体験可能にする。PBF DL / Extractor は CORS / 処理量の制約で不可。3I のローカル版完成後に着手し、WASM 前提の設計複雑化を回避 |

---

## 8. リスク

| # | リスク | 対処 |
|---|---|---|
| 3I-R1 | ExtractPipeline に IProgress が渡せない（既存 API に引数がない） | 3I.3 着手時にシグネチャ確認。必要なら ExtractPipeline にオーバーロード追加。最悪はコンソール出力キャプチャ |
| 3I-R2 | Geofabrik のレート制限 | User-Agent 明示 + If-Modified-Since + キャッシュ優先。DL 前に確認ダイアログ |
| 3I-R3 | 大規模 PBF（日本全国 1.8 GB）の DL / 抽出が長時間 | 進捗 SSE で UX 維持。地方ブロック推奨をデフォルトに |
| 3I-R4 | MapVerifier コンポーネント流用時の差分が大きい | 流用元は固定（現在の commit）、Sandbox 側で独立改変 |
| 3I-R5 | 同プロセス Extractor で OOM（大規模 PBF） | 都道府県 bbox で切り出すため PBF 全量はロードしない。Pass 1/2/3 ストリーミング処理で軽減済み |

---

## 9. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 | 2026-05-28 | 初版。§5.5-30〜34 全件ユーザー確定、React + MapLibre GL 構成確定、5 サブステップ分割、Geofabrik 地方ブロック単位に修正 | Claude (Opus 4.7) |
| 0.2 | 2026-05-28 | **3I 全完了**（commit bbe3af1 / 793050e / bc3c38f、676 件 pass）。実装中の追加対応: 保存先永続化 / bbox 頂点ドラッグ / 既存 PBF・odrg ブラウズ / プロファイル反映 / .odrg v0.3 RequestedBbox 拡張 + マイグレーション。ズーム閾値自動非表示は未実装（手動 ON/OFF + サーバー 10,000 セル上限で代替、Phase 4+）。GML は Sandbox 非対応。設計書 §10 肉付け完了 | Claude (Opus 4.7) |
