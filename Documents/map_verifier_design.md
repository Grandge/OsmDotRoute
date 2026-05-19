# MapVerifier 設計書

**バージョン**: 1.0.0（リリース、初版）
**作成日**: 2026-05-19
**最終更新**: 2026-05-19
**ステータス**: リリース（初版、Phase 1 ステップ 13〜14 で構築した検証用地図アプリの完成形）
**対象**: OsmDotRoute Phase 1 ステップ 13〜14 で構築する検証用地図アプリの設計記録
**関連ドキュメント**:

- [OsmDotRoute 要件定義書](requirement_definition.md)
- [Phase 1 設計書](phase1_design.md)（§15 は本ファイルへのポインタ）
- [Phase 1 実装計画書](phase1_implementation_plan.md)（ステップ 13〜14）

---

## 0. 本書の目的と更新ルール

### 0.1 目的

ブラウザ上で OsmDotRoute の動作を視覚的に確認できる検証ツール `MapVerifier` の
設計判断・採用構成・API 仕様・UI 仕様を記録する。実装後の挙動と要件の対応関係を辿れる
ようにし、Phase 2 以降で MapVerifier を継続発展させる場合の出発点とする。

### 0.2 ライブラリ本体との関係と独立バージョニング

`MapVerifier` は `samples/MapVerifier/` 配下に置く**検証用サンプルアプリ**で、
OsmDotRoute ライブラリ本体（`src/`）とは以下の点で独立に管理する。

| 観点 | OsmDotRoute（ライブラリ本体） | MapVerifier（検証用アプリ） |
| --- | --- | --- |
| バージョン体系 | 0.x 進行中、1.0.0 は Phase 3 OSS 公開時想定 | 独自 **SemVer**（本設計書時点で **1.0.0**） |
| バージョン管理ファイル | `Directory.Build.props` 等 | `MapVerifier.Server.csproj` の `<Version>` + `MapVerifier.Web/package.json` の `version` |
| 互換性ポリシー | 0.x はマイナー版アップで破壊的変更を許容（REQ-API-008） | MapVerifier 内部 API のみで完結。SemVer 厳密適用（破壊的変更 → major up） |
| 配布 | ソース参照／将来 NuGet | 配布せず、ローカル起動のみ（OSS 公開時もサンプルとして同梱） |
| 設計書 | `Documents/phase1_design.md` | **本ファイル** |
| 改訂履歴 | `phase1_design.md` §19 | 本ファイル §10 |

OsmDotRuote 0.x の破壊的変更が MapVerifier の動作に影響した場合、MapVerifier 側で
追従修正してマイナー版アップ（例: 1.1.0 → 1.2.0）として扱う。MapVerifier の挙動が
変わらず内部 OsmDotRoute 参照のみ更新した場合はパッチ版アップ（例: 1.0.0 → 1.0.1）。

### 0.3 更新ルール

- ステップ 13〜14 の実装完了時にユーザー合意の上で章を埋める
- 本ファイル冒頭の **バージョン** と §10 改訂履歴は変更ごとに更新する
- MapVerifier の公開挙動（API/UI）に変更が入る場合は SemVer に従い適切にバージョンを上げる
- バージョン採番はユーザーが判断する（Claude 単独で上げない）

---

## 1. スコープと検証ゴール

### 1.1 スコープ（やること）

- **OsmDotRoute Phase 1 公開 API の動作を視覚的に検証** する手段を提供する
- 都道府県単位の RouterDb 読込 → マップ表示 → スナップ → 経路計算 → 動的制約登録 → 経路再計算 を
  マウス操作 1 周で行えるようにする
- 国土数値情報 KSJ GML（A31 洪水等）の一括登録 UI も提供する（Phase 1 ステップ 10 機能の確認）
- メッシュコード階層（1km / 500m / 250m）の指定とグリッド表示

### 1.2 アウトオブスコープ（やらないこと）

- 認証・認可（個人ローカル起動前提）
- マルチユーザー対応（プロセス単一の `RestrictedAreaService` を Singleton 共有）
- リアルタイム双方向通信（SignalR 等）。`fetch` ベースの REST のみ
- グラフ表示・チャート機能（Recharts 等）
- シナリオ保存・読込・履歴
- 親プロジェクト「災害廃棄物処理シミュレーション」の Viewer 機能の再現（あくまで OsmDotRoute の検証用）
- 本番運用想定の堅牢性（エラーログ集約、メトリクス、ヘルスチェック等）
- ベンチマーク取得（性能計測は Phase 1 ステップ 15 の `OsmDotRoute.Benchmarks` で別途実施）

### 1.3 検証ゴール（受入条件）

ステップ 14 完了時点で、以下の手動シナリオが全て通ること:

1. RouterDb 読込 → 統計表示 → マップが当該経緯度範囲にフィット → 道路ネットワークがレイヤー描画される
2. 起終点をマップクリックで指定 → 経路計算 → 通常経路が LineString として描画される
3. 経路途中にマウスでポリゴンを描画し進入禁止登録 → 再計算で迂回経路になることを確認
4. ポリゴン削除 → 元の経路に戻ることを確認
5. メッシュ階層を切替（1km/500m/250m）→ 表示範囲のグリッドが描画される
6. 任意メッシュをクリックして難所登録（speedFactor 例 0.3）→ 経路が遅くなる／迂回することを確認
7. KSJ GML ファイル（例: A31 洪水浸水想定区域）を読込 → 表示範囲フィルタ付きで一括登録 → 経路が回避することを確認

---

## 2. アーキテクチャ概観

### 2.1 全体構成

```text
[Browser]
   │  fetch (JSON)
   ▼
[MapVerifier.Server]  (.NET 9 ASP.NET Core minimal API, localhost only)
   │  ProjectReference
   ├─ OsmDotRoute                         (コア)
   ├─ OsmDotRoute.Itinero                 (.routerdb 読込)
   └─ OsmDotRoute.Extensions.DependencyInjection
        └─ services.AddOsmDotRoute(routerDbPath) で Singleton 登録
```

```text
[MapVerifier.Web]  (Vite + React + MapLibre GL, dev server :5173)
   ├─ VersionBanner    ヘッダー固定: "MapVerifier vX.Y.Z (server: vA.B.C)"
   ├─ MapView          MapLibre GL の React ラッパー
   ├─ LoadPanel        RouterDb パス入力 → /api/load
   ├─ MapBoundsPanel   現在の表示範囲表示・手動 fitBounds
   ├─ RoutePanel       起終点指定 + プロファイル選択 + /api/route
   ├─ MeshGridPanel    メッシュ階層選択 + グリッド描画 + クリック登録
   ├─ PolygonEditorPanel  マウス描画モード / 座標テーブル指定モード
   ├─ GmlImportPanel   GML ファイルアップロード + 範囲フィルタ
   └─ RestrictionListPanel  /api/restrictions の表表示 + 個別削除 + 全クリア
```

**バージョン表示方針**: `VersionBanner` をヘッダーに常時固定表示し、フロント版（`MapVerifier.Web/src/version.ts`）と
サーバー版（`GET /api/version`）の両方を併記する。フロントとサーバーで版が食い違った場合に視覚的に気付ける
ようにするため。例: `MapVerifier v1.0.0 (server: v1.0.0)`、不一致時は警告色で表示。

### 2.2 サーバー・クライアント分離方針

- **サーバーは状態を持つ**: `RouterDb` と `RestrictedAreaService` を Singleton で保持。
  プロセス生存中に複数リクエストから共有される（=シミュレーション中の制約変更が即時反映、REQ-RST-012）
- **クライアントはステートレス**: サーバー状態をフェッチ・更新する薄い UI 層に徹する。
  描画用 GeoJSON はサーバーから取得した内容＋自前で生成（ポリゴン編集中のドラフト）の合成
- **JIS X0410 メッシュ変換はクライアント側にも実装**: 「現在表示範囲のメッシュ列を描画」は
  ラウンドトリップせずクライアント単独で算出。これによりズーム／パン中にサーバー負荷を上げない

### 2.3 ステップ 13 / 14 のスコープ分割

| 機能 | ステップ 13 | ステップ 14 |
| --- | --- | --- |
| サーバー: `/api/load`, `/api/stats`, `/api/road-network`, `/api/snap`, `/api/route` 基盤 | ✅ | （補強） |
| サーバー: `/api/restrictions/*`（polygon/mesh/gml/list/delete） | （列挙のみスケルトン） | ✅ |
| フロント: MapView / LoadPanel / MapBoundsPanel | ✅ | （継続使用） |
| フロント: 道路ネットワークレイヤー（オン／オフ） | ✅ | （継続使用） |
| フロント: RoutePanel / MeshGridPanel / PolygonEditorPanel / GmlImportPanel / RestrictionListPanel | — | ✅ |

ステップ 13 完了時点では「マップが出て道路ネットワークが描けて表示範囲を動かせる」までを保証する。
動的制約と経路計算 UI はステップ 14 で完成。

---

## 3. プロジェクト構成

### 3.1 配置

```text
samples/
└── MapVerifier/
    ├── MapVerifier.Server/
    │   ├── MapVerifier.Server.csproj
    │   ├── Program.cs                    # 最小 API ホスト
    │   ├── appsettings.json              # CORS 設定等
    │   ├── Endpoints/                    # 機能別エンドポイント
    │   │   ├── LoadEndpoints.cs
    │   │   ├── RouteEndpoints.cs
    │   │   ├── SnapEndpoints.cs
    │   │   └── RestrictionEndpoints.cs
    │   ├── Contracts/                    # リクエスト/レスポンス DTO
    │   │   ├── LoadRequest.cs
    │   │   ├── SnapRequest.cs / SnapResponse.cs
    │   │   ├── RouteRequest.cs / RouteResponse.cs
    │   │   ├── PolygonRestrictionRequest.cs
    │   │   ├── MeshRestrictionRequest.cs
    │   │   ├── GmlRestrictionRequest.cs
    │   │   └── RestrictionListItem.cs
    │   └── Services/                     # サーバー側ヘルパ
    │       ├── RouterState.cs            # ロード状態管理（Singleton）
    │       └── GeoJsonConverter.cs       # Route → GeoJSON LineString 変換
    └── MapVerifier.Web/
        ├── package.json                  # version: 1.0.0
        ├── vite.config.ts
        ├── tsconfig.json
        ├── index.html
        └── src/
            ├── main.tsx
            ├── App.tsx
            ├── api/                      # fetch ラッパー
            │   └── client.ts
            ├── components/
            │   ├── MapView.tsx
            │   ├── LoadPanel.tsx
            │   ├── MapBoundsPanel.tsx
            │   ├── RoutePanel.tsx
            │   ├── MeshGridPanel.tsx
            │   ├── PolygonEditorPanel.tsx
            │   ├── GmlImportPanel.tsx
            │   └── RestrictionListPanel.tsx
            ├── mesh/                     # JIS X0410 変換（クライアント側）
            │   └── meshGrid.ts
            └── version.ts                # 1.0.0
```

### 3.2 csproj / package.json バージョン設定

`MapVerifier.Server.csproj`:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <Version>1.0.0</Version>
  <AssemblyName>MapVerifier.Server</AssemblyName>
  <IsPackable>false</IsPackable>
  <GenerateDocumentationFile>false</GenerateDocumentationFile>
</PropertyGroup>
```

`MapVerifier.Web/package.json`:

```json
{
  "name": "map-verifier-web",
  "private": true,
  "version": "1.0.0",
  "type": "module"
}
```

両者を常に同じバージョンで揃える（サーバーとフロントは一体として配布される検証ツール）。

### 3.3 依存関係

**MapVerifier.Server**:

| 種別 | 名前 | バージョン | 用途 |
| --- | --- | --- | --- |
| ProjectRef | `OsmDotRoute` | （同リポ） | コア |
| ProjectRef | `OsmDotRoute.Itinero` | （同リポ） | `.routerdb` 読込 |
| ProjectRef | `OsmDotRoute.Extensions.DependencyInjection` | （同リポ） | `services.AddOsmDotRoute(...)` |

`AddOsmDotRoute` は遅延ファクトリ経由なのでアプリ起動時にはファイル I/O が走らない。
ただし MapVerifier では「起動時はパス未確定、`/api/load` で動的にロード」したいので、
`RouterState`（後述）を Singleton として別途持ち、`Router` インスタンスは
`AddOsmDotRoute` ではなく独自に管理する（§4.0 参照）。

**MapVerifier.Web**:

| 名前 | バージョン | 用途 |
| --- | --- | --- |
| `react` / `react-dom` | ^18 | UI |
| `maplibre-gl` | ^4 | マップ |
| `@types/react` / `@types/react-dom` | ^18 | 型 |
| `typescript` | ^5 | 言語 |
| `vite` / `@vitejs/plugin-react` | ^5 | ビルド |

---

## 4. サーバー API 仕様

### 4.0 状態管理と DI 方針

`MapVerifier.Server` は「起動時 RouterDb 未ロード、`/api/load` 後にロード済み」という
ライフサイクルを持つため、`OsmDotRoute.Extensions.DependencyInjection.AddOsmDotRoute(path)` を
そのまま使わず、以下の手動 Singleton 登録を行う:

```csharp
builder.Services.AddSingleton<RouterState>();          // ロード状態を保持
builder.Services.AddSingleton<RestrictedAreaService>();  // 制約サービス（常に存在）
```

`RouterState` は `RouterDb?` と `Router?` を内部に保持し、`/api/load` 時にロード／差し替えする。
未ロード状態でルーティング系エンドポイントが叩かれた場合は `409 Conflict` を返す（§4.7）。

### 4.1 共通仕様

- **ベース URL**: `http://localhost:<port>/api/`（既定 `http://localhost:5279`）
- **Content-Type**: 受信・送信とも `application/json; charset=utf-8`
- **CORS**: `MapVerifier.Web` 開発サーバー `http://localhost:5173` のみ許可（appsettings で変更可）
- **時間単位**: 秒（`durationSec`）、**距離単位**: メートル（`distanceM`）、**座標**: WGS84 度
- **エラーレスポンス**（HTTP ステータスは §4.13 参照）: `{ "error": "<short code>", "message": "<日本語説明>" }`

### 4.1.0 ファイル参照方式の変遷と最終形 (v1.3.0)

**v1.2.0 (撤回済)**: TargetFramework を `net9.0-windows` 化し、`POST /api/files/pick` で OS ネイティブ
`OpenFileDialog` を起動する方式を試みた。WinForms / WPF / PowerShell サブプロセス / WinExe 専用
ピッカー EXE と複数アプローチを試したが、ユーザー環境では一貫してダイアログが画面表示されなかった
（ASP.NET Core 子プロセスからの UI セッション接続に環境固有の制約があった模様）。

**v1.3.0 採用**: 親プロジェクト「災害廃棄物処理シミュレーション」の `UserSettingsDialog` 方式に倣い、
**Web 内モーダルで自前のファイルブラウザを実装**。サーバーは `GET /api/files/browse` で
ディレクトリ内容（ドライブ・親パス・サブフォルダ・パターン一致ファイル）を返すだけ。
OS ネイティブ UI に一切依存せず、ブラウザ ↔ サーバーの HTTP のみで完結するため確実に動作する。

これに伴い:

- TargetFramework を `net9.0-windows` → `net9.0` に戻し、Windows 固有依存を撤廃
- `<UseWindowsForms>` / `<UseWPF>` を削除
- `MapVerifier.FilePicker` 専用 EXE プロジェクトをソリューションから削除（フォルダは残置）
- `POST /api/files/pick` エンドポイントを廃止、`GET /api/files/browse` に置換

### 4.1.1 `GET /api/version` — サーバーバージョン取得

**Response 200**:

```json
{ "name": "MapVerifier.Server", "version": "1.0.0" }
```

- `version` は `MapVerifier.Server.csproj` の `<Version>` から `Assembly.GetName().Version` 経由で取得
- RouterDb ロード状態に依存しない（起動直後から応答可能）
- フロントは初回マウント時に取得し、`VersionBanner` に表示する

### 4.1.2 `GET /api/files/browse` — ローカルファイルブラウズ (v1.3.0)

**Query 引数**:

| 名前 | 必須 | 内容 |
| --- | --- | --- |
| `path` | 任意 | 走査するディレクトリの絶対パス。未指定時はユーザープロファイル (`%USERPROFILE%`) |
| `pattern` | 任意 | ファイル絞り込み (例: `*.routerdb` / `*.xml;*.gml`)。`;` 区切りで複数 |

**Response 200**:

```json
{
  "currentPath": "d:\\workspace\\DotRoute",
  "parentPath": "d:\\workspace",
  "directories": [{ "name": "Documents" }, { "name": "samples" }],
  "files": [{ "name": "README.md", "size": 5531 }],
  "drives": ["C:\\", "D:\\", "G:\\"]
}
```

- 隠し/システム属性は除外。アクセス拒否のサブフォルダは黙って除外
- ルートディレクトリ (例: `C:\`) では `parentPath` が `null`
- 個人ローカル実行前提のため認可は無し

### 4.2 `POST /api/load` — RouterDb 読込

**Request**:

```json
{ "routerDbPath": "C:/path/to/default.routerdb" }
```

**Response 200**:

```json
{
  "vertexCount": 1234567,
  "edgeCount": 2345678,
  "southWest": { "latitude": 35.500, "longitude": 139.500 },
  "northEast": { "latitude": 35.900, "longitude": 140.000 }
}
```

- ロード成功時、既存 `RouterDb` は破棄して差し替え（`RestrictedAreaService` はクリアせず継続）
- `ArgumentException` / `FileNotFoundException` は `400` で返す

### 4.3 `GET /api/stats` — 統計取得

`POST /api/load` のレスポンスと同形式を返す。未ロード時は `409`。

### 4.4 `GET /api/road-network` — 道路ネットワーク GeoJSON

`RoadNetworkGeoJson.Json` をそのまま `Content-Type: application/geo+json` で返す。
都道府県単位で数十 MB〜数百 MB になるため、レスポンスは gzip 圧縮を有効化する
（ASP.NET Core の `ResponseCompression` ミドルウェア）。

### 4.5 `POST /api/snap` — 道路スナップ

**Request**:

```json
{ "lat": 35.681, "lon": 139.767, "profile": "car", "searchDistanceM": 500 }
```

**Response 200**（成功時）:

```json
{ "snapped": { "latitude": 35.6812, "longitude": 139.7668 } }
```

ネットワーク外の場合は `{ "snapped": null }` を 200 で返す（REQ-RTE-008）。

### 4.6 `POST /api/route` — 経路計算

**Request**:

```json
{
  "fromLat": 35.681, "fromLon": 139.767,
  "toLat":   35.658, "toLon":   139.745,
  "profile": "car"
}
```

**Response 200**（成功時）:

```json
{
  "found": true,
  "distanceM": 4321.5,
  "durationSec": 612.3,
  "geometry": {
    "type": "LineString",
    "coordinates": [[139.767, 35.681], ..., [139.745, 35.658]]
  }
}
```

経路未発見・スナップ失敗時:

```json
{ "found": false, "distanceM": 0, "durationSec": 0, "geometry": null }
```

サーバー側で `Route.Shape: IReadOnlyList<GeoCoordinate>` を GeoJSON LineString に変換する
（REQ-FMT-004 廃止により、変換ロジックは MapVerifier 内部の `GeoJsonConverter` ヘルパに置く）。

### 4.7 `POST /api/restrictions/polygon` — ポリゴン制約登録

**Request**:

```json
{
  "kind": "block" | "difficulty",
  "difficultyType": "flooding",          // kind=difficulty のとき必須
  "outerBoundary": [
    { "latitude": 35.68, "longitude": 139.76 },
    { "latitude": 35.68, "longitude": 139.78 },
    { "latitude": 35.66, "longitude": 139.78 },
    { "latitude": 35.66, "longitude": 139.76 }
  ],
  "holes": [],                            // 省略可、空配列が既定
  "tag": "incident-2026-05-19"           // 省略可
}
```

**Response 200**: `{ "id": "<guid>" }`

### 4.8 `POST /api/restrictions/mesh` — メッシュコード制約登録

**Request**:

```json
{
  "kind": "block" | "difficulty",
  "difficultyType": "flooding",          // kind=difficulty のとき必須
  "meshCodes": [53394611, 533946112],    // 異なる階層の混在可
  "tag": "typhoon-15"                    // 省略可
}
```

**Response 200**: `{ "id": "<guid>" }`

### 4.9 `POST /api/restrictions/gml-file` — GML ファイル一括登録 (v1.2.0)

**Request** (`application/json`):

```json
{
  "filePath": "C:/path/to/A31-12_24.xml",
  "kind": "block" | "difficulty",
  "difficultyType": "flooding",
  "useMapBounds": true,
  "mapBoundsSouthWest": { "latitude": 35.11, "longitude": 136.69 },
  "mapBoundsNorthEast": { "latitude": 35.21, "longitude": 136.81 },
  "tag": "a31-2024"
}
```

- `filePath` はサーバー (= ローカル) のファイルパス。`POST /api/files/pick` で取得した値をそのまま渡す想定
- `useMapBounds=true` のときは `mapBoundsSouthWest` / `mapBoundsNorthEast` 必須 (REQ-RST-040)
- `useMapBounds=false` の場合はファイル内全フィーチャを採用

**Response 200**:

```json
{ "ids": ["<guid>", ...], "acceptedCount": 31 }
```

実機検証: A31 1.6 GB ファイルから現在のマップ範囲 (約 11×12 km) でフィルタすると 31 ポリゴンを 20 秒で取り込み完了。

v1.0.0 ドラフトでは `multipart/form-data` 案だったが、サーバーとブラウザが同一マシンで動く前提なので
ファイル転送せずパス渡しに変更。GB 級ファイルでもメモリ圧迫なし。

### 4.10 `GET /api/restrictions` — 一覧取得

**Response 200**:

```json
{
  "items": [
    {
      "id": "<guid>",
      "kind": "block" | "difficulty",
      "difficultyType": "flooding" | null,
      "shapeType": "polygon" | "mesh",
      "meshCount": 1,                       // shapeType=mesh のときのみ
      "outerBoundary": [...],               // shapeType=polygon のときのみ
      "tag": "..."
    }
  ]
}
```

- マップ描画用の GeoJSON は `GET /api/restrictions/geojson` で別途取得（メッシュ AABB → Polygon に展開してレスポンス）

### 4.11 `GET /api/restrictions/geojson` — マップ描画用 GeoJSON

`/api/restrictions` の各エントリを GeoJSON FeatureCollection に展開して返す。
メッシュコードはサーバー側で AABB → Polygon に変換（クライアント側の描画都合に合わせる）。
`Feature.properties` に `id`, `kind`, `difficultyType`, `tag` を含める。

### 4.12 `DELETE /api/restrictions/{id}` / `DELETE /api/restrictions`

| エンドポイント | 動作 | レスポンス |
| --- | --- | --- |
| `DELETE /api/restrictions/{id}` | 個別削除（存在しない ID も 204） | `204 No Content` |
| `DELETE /api/restrictions?tag=X` | タグ一括削除 | `204 No Content` |
| `DELETE /api/restrictions` | 全クリア | `204 No Content` |

### 4.13 HTTP ステータス対応表

| 状況 | ステータス |
| --- | --- |
| 成功（ボディ有） | `200 OK` |
| 成功（ボディ無） | `204 No Content` |
| バリデーション失敗（必須フィールド欠落、JSON parse 失敗等） | `400 Bad Request` |
| RouterDb 未ロード | `409 Conflict` |
| サーバー内部例外 | `500 Internal Server Error` |

---

## 5. フロントエンド構成

### 5.1 状態管理

- 小規模のため Redux 等は導入せず、`App.tsx` の `useState` + Context で十分とする
- グローバル状態:
  - `routerDbLoaded: boolean`
  - `stats: { vertexCount, edgeCount, southWest, northEast } | null`
  - `restrictions: GeoJSON.FeatureCollection`
  - `currentRoute: GeoJSON.LineString | null`
  - `mapBounds: { sw, ne } | null`
  - `interactionMode: "idle" | "pickFrom" | "pickTo" | "drawPolygon" | "pickMesh"`

### 5.2 MapView と MapLibre レイヤー設計

各機能は MapLibre ソース／レイヤーとして独立管理:

| ソース ID | レイヤー種別 | 内容 | 表示制御 |
| --- | --- | --- | --- |
| `road-network` | line | `/api/road-network` の LineString 列 | チェックボックスでオン/オフ |
| `restrictions` | fill + line | `/api/restrictions/geojson` | 常時表示 |
| `restriction-highlight` | fill | 一覧で選択中の 1 件をハイライト | 選択時のみ |
| `mesh-grid` | line + fill (半透明) | クライアント生成 | チェックボックスでオン/オフ |
| `polygon-draft` | line + circle (頂点) | 描画中ポリゴンのドラフト | drawPolygon モード時 |
| `route` | line（太線） | `/api/route` のレスポンス | 計算後表示 |
| `route-endpoints` | circle | 起点・終点マーカー | 設定後表示 |

MapView は親プロジェクトの `pages/EditorPage.tsx` / `pages/SimulationPage.tsx` の MapLibre 使用部
を参考に、独自の React ラッパーとして新規実装する（コピー貼り付けはしない）。

### 5.3 各 Panel の責務

| Panel | 状態管理 | API |
| --- | --- | --- |
| `VersionBanner` | サーバーバージョン（初回 fetch） | `GET /api/version` |
| `LoadPanel` | パス入力値、ファイル参照ボタン (v1.3.0: 自前 `FileBrowserDialog` 起動) | `POST /api/load`, `GET /api/road-network`, `GET /api/files/browse` |
| `GmlImportPanel` (v1.2.0+) | ファイル参照、kind、difficultyType、useMapBounds、tag | `POST /api/restrictions/gml-file`, `GET /api/files/browse` |
| `FileBrowserDialog` (v1.3.0) | カレントパス、選択ファイル、最近開いたパス (localStorage) | `GET /api/files/browse` |
| `MapBoundsPanel` | 現在の bounds 表示・編集 | （API 呼び出しなし、MapLibre の `fitBounds`） |
| `RoutePanel` | 起点・終点座標、プロファイル選択 | `POST /api/route`, `POST /api/snap`（任意） |
| `MeshGridPanel` | 階層選択、グリッド可視性 | `POST /api/restrictions/mesh` |
| `PolygonEditorPanel` | ドラフト頂点列、入力モード（マウス/座標表） | `POST /api/restrictions/polygon` |
| `GmlImportPanel` | ファイル、kind、difficultyType、tag | `POST /api/restrictions/gml` |
| `RestrictionListPanel` | 一覧、選択行 | `GET /api/restrictions`, `DELETE /api/restrictions/{id}`, `DELETE /api/restrictions` |

### 5.4 メッシュグリッドのサーバー側生成（v1.1.0 で方針変更）

v1.0.0 では「クライアント側で JIS X0410 変換を実装」する方針だったが、v1.1.0 で **サーバー側生成**
（`GET /api/mesh/grid`）に変更した。理由:

- ライブラリ本体に `MeshCode.EnumerateInBounds(MapBounds, MeshLevel)` および `MeshCode.ToBounds()` を
  公開 API として追加（lib v0.18）。サーバーはこれを呼び出すだけで二重実装不要
- 「描画ボタンを押したとき」だけ呼び出される 1 リクエスト/操作なので、ズーム／パン時の通信負荷
  懸念は実害がない
- 階層変更や範囲変更ごとに 1 度しか呼ばないため、ラウンドトリップは UX に影響しない

過大要求のガード: 範囲内メッシュ数が 10,000 を超える場合は `400 Bad Request` を返す。

### 5.5 ポリゴン描画 UI

MapLibre GL は標準でポリゴン描画 UI を持たないため自前実装する（`@mapbox/mapbox-gl-draw`
は追加せず、最小実装で済ませる）:

1. 「ポリゴン描画開始」ボタンで `interactionMode = "drawPolygon"`、`polygon-draft` ソースを空で初期化
2. マップクリックで頂点配列に push、`polygon-draft` ソースを更新（線 + 頂点丸）
3. 「描画完了」ボタンまたはダブルクリックで確定 → ダイアログで kind/difficultyType/tag を入力 →
   `POST /api/restrictions/polygon` → 成功後 `polygon-draft` クリア + `restrictions` 再取得
4. 「キャンセル」で `polygon-draft` クリア + `interactionMode = "idle"`

座標表モードは独立タブで提供。マウス描画モードと相互に変換可能（描画頂点をテーブルにロード／
テーブルから描画プレビュー）。

---

## 6. 動作シナリオ

### 6.1 基本検証シナリオ（ステップ 14 完了時の受入条件）

§1.3 の 7 シナリオを通すこと。詳細手順は実装完了時に本節へ追記（スクリーンショット等）。

### 6.2 起動手順

```bash
# ターミナル 1: サーバー
dotnet run --project samples/MapVerifier/MapVerifier.Server

# ターミナル 2: フロント
cd samples/MapVerifier/MapVerifier.Web
npm install
npm run dev

# ブラウザ
open http://localhost:5173
```

---

## 7. バージョニング方針

### 7.1 SemVer 適用ルール

| 変更内容 | 上げ方 |
| --- | --- |
| 公開 API 仕様（§4）の破壊的変更、UI フロー大幅変更 | Major（1.x → 2.0.0） |
| エンドポイント追加、UI 機能追加（既存破壊なし） | Minor（1.0.x → 1.1.0） |
| バグ修正、内部 OsmDotRoute 参照のみ更新、UI 調整（挙動不変） | Patch（1.0.0 → 1.0.1） |

### 7.2 OsmDotRoute 本体との連動

- OsmDotRoute 0.x の破壊的変更を取り込み MapVerifier の動作に影響が出た場合: MapVerifier 側で
  追従して **マイナー版上げ**（例: 1.1.0）
- OsmDotRoute 0.x の追加機能を MapVerifier が UI 露出した場合: **マイナー版上げ**
- OsmDotRoute の参照を更新しても MapVerifier の挙動が変わらない場合: **パッチ版上げ**

### 7.3 バージョン採番の責務

- バージョン番号の確定は **ユーザー判断**（Claude が独断で上げない）
- Claude は変更内容と推奨上げ方を提示し、承認を仰ぐ

---

## 8. 制約事項と既知の課題

- **大規模 RouterDb のロード時間**: 都道府県単位 RouterDb は `Itinero.RouterDb.Deserialize` で
  数秒〜数十秒かかる。`/api/load` は同期処理で実装し、フロント側でローディングインジケータを出す
  （非同期化は要望が出てから検討、REQ-RTE-005 に準ずる）
- **道路ネットワーク GeoJSON の転送量**: 都道府県単位で数十 MB〜数百 MB。gzip 圧縮で
  実用域には収まる見込みだが、ブラウザ描画も重いので「初期はオフ、ボタンで有効化」を既定とする
- **マルチユーザー非対応**: `RestrictedAreaService` Singleton 共有なので、複数タブ／複数 PC から
  同時にアクセスすると干渉する。ローカル個人検証ツール用途では問題なし
- **GML 大ファイル時のメモリ**: A31 1.6GB は `Stream` ベースで処理可能（既存実装）。ただし
  `multipart/form-data` の ASP.NET Core 既定上限（30MB）を上げる必要あり（`appsettings` で設定）

---

## 9. 検証方法

### 9.1 ステップ 13 完了判定

- [ ] `dotnet run --project samples/MapVerifier/MapVerifier.Server` で API 起動
- [ ] `npm run dev`（`MapVerifier.Web`）でフロント起動
- [ ] ブラウザで RouterDb 読込 → 統計表示 → マップが該当範囲にフィット
- [ ] 道路ネットワークがレイヤー描画される（オン／オフ切替可）
- [ ] 表示範囲を `MapBoundsPanel` の手動入力で変更できる

### 9.2 ステップ 14 完了判定

- [ ] §1.3 の 7 シナリオが全て動作
- [ ] マウス描画ポリゴンと座標指定ポリゴンが同等の制約効果を生む
- [ ] メッシュ 3 階層（1km/500m/250m）すべて表示・登録可能
- [ ] KSJ GML（A31 等）のアップロードと範囲フィルタが動作
- [ ] ユーザーレビュー OK

### 9.3 自動テスト方針（Phase 1 範囲）

- サーバー: エンドポイント単体の最小スモーク（`POST /api/load` で `400` / `200`、
  未ロード時の `409` 等）を 5〜10 件
- フロント: `meshGrid.ts` のみ Vitest で単体テスト（JIS X0410 変換の正しさ）。
  UI コンポーネントの E2E は Phase 1 では実施しない

---

## 10. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
| --- | --- | --- | --- |
| 1.0.0 (リリース) | 2026-05-19 | **初版リリース**。Phase 1 ステップ 13〜14 で構築した検証用地図アプリの完成形。複数の試行錯誤 (OS ネイティブダイアログ各種、専用 GUI プロセス) を経て、最終的に Web 内モーダル + HTTP API による自前ファイルブラウザに着地。本版で確定した機能セット:  **Server (`net9.0`, ASP.NET Core minimal API)**: `/api/version`、`/api/load`、`/api/stats`、`/api/road-network`、`/api/snap`、`/api/route`、`/api/mesh/grid` (1km/500m/250m 3 階層、過大要求 10k セル上限)、`/api/restrictions/polygon`、`/api/restrictions/mesh`、`/api/restrictions/gml-file` (lib `AddBlockAreaFromGmlFile`/`AddDifficultyAreaFromGmlFile` 呼出、A31 GB 級ファイルもパス渡しで対応)、`/api/restrictions`、`/api/restrictions/geojson`、`DELETE /api/restrictions/{id}`、`DELETE /api/restrictions[?tag=]`、`/api/files/browse?path=&pattern=`。`RouterDb` / `RestrictedAreaService` は Singleton、`RouterState` で動的差替。`RestrictedAreaService.ListAll()` の公開プロパティから直接 GeoJSON を生成 (GML インポート分も自動反映)。 **Web (Vite 5 + React 18 + MapLibre GL 4)**: `VersionBanner` (フロント版/サーバー版併記、不一致時警告)、`LoadPanel` (RouterDb 読込・統計・道路ネットワーク表示トグル)、`MapBoundsPanel` (現在範囲表示・手動 fit)、`RoutePanel` (起終点マップ指定・プロファイル選択・計算結果表示)、`MeshGridPanel` (階層選択・グリッド描画・メッシュクリックで属性付与)、`PolygonEditorPanel` (マウス描画・属性付与)、`GmlImportPanel` (ファイル参照・マップ範囲フィルタ)、`RestrictionListPanel` (一覧・個別/全削除)、`FileBrowserDialog` (ドライブ選択・ナビゲーション・件数表示・全ファイル表示トグル・パス直接入力・localStorage 記憶)。MapView に 6 種レイヤー (road-network/mesh-grid/restrictions/route/route-endpoints/polygon-draft)。 **Lib 連動**: `MeshCode.ToBounds()` + `MeshCode.EnumerateInBounds(MapBounds, MeshLevel)` を Lib v0.18 で公開 API 追加 (二重実装回避)、テスト 153/153。 **E2E 検証実績**: 実機 RouterDb 43k 頂点で load→stats→road-network→route 動作確認。ベースライン経路 3784m → ブロックポリゴン登録後 4968m 迂回確認。A31 1.6GB GML をマップ範囲フィルタで 31 ポリゴン 20 秒インポート確認。ユーザー承認済 (2026-05-19)。 **バージョニング方針**: 本版を初版基準とし、以後の機能追加で MINOR、修正で PATCH を上げる | Claude (Opus 4.7) |
