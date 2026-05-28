# Phase 3 ステップ 3J: GitHub Pages デモ（React 流用 + コア WASM 化）計画書

**ステータス**: v0.1（ドラフト・ユーザーレビュー前、2026-05-29 起草）
**対応ステップ**: Phase 3 ステップ 3J（[Phase 3 実装計画書 §3.9 / §6](phase3_implementation_plan.md)）
**対応要件**: REQ-PKG-003（OSS 公開準備の一環）、REQ-LIC-004（ODbL 表記）、REQ-MAP-005（`.odrg` ランタイム読込のブラウザ拡張）
**関連文書**:

- [Phase 3 設計書 §10.5](phase3_design.md)（本ステップで肉付け対象。プレースホルダ題は「Blazor WASM」だが本計画で **React 流用 + コア WASM 化** に確定・改題する）
- [Phase 3 実装計画書 §3.9](phase3_implementation_plan.md)（GitHub Pages デモ概要。Blazor 前提の記述は本計画で更新）
- [Phase 3 ステップ 3I 計画書](phase3_step3I_plan.md)（流用元の Sandbox React UI / Server API）
- 現行 Sandbox 実装（`samples/Sandbox/Web`、`samples/Sandbox/Server`）

---

## 0. 起草経緯と原計画からの修正

実装計画書 §3.9 は「Sandbox の Web コンポーネント（MapView / RoutePanel / MeshGridPanel / PolygonEditorPanel）を **Blazor interop で流用**」と記述している。しかし 3I で実装した Sandbox の Web は **React + TypeScript + MapLibre GL** であり、Blazor コンポーネントではない。したがって原計画の前提は成立しない。

本計画では、2026-05-29 のユーザー確認で確定した以下 3 方針に基づき、原計画 §3.9 を修正してステップを定義する。

| # | 判断項目 | 確定 | 却下案 |
|---|---|---|---|
| J-A | UI 方式 | **React 流用**（既存 React UI を再利用、WASM 化はコアエンジンのみ） | Blazor 全面書換 |
| J-B | コア改修の是非 | **in-memory ソースをコアに追加**（`.odrg` を `byte[]` から読めるよう抽象化） | samples 内に WASM 専用リーダを閉じる |
| J-C | gh-pages 実公開タイミング | **3H と同時**（3J ではビルド + CI 整備まで、実公開は Phase 3 確定で REQ-PKG-002 解除と同時） | 3J で即公開 |

---

## 1. 目的とゴール

**目的**: OSS 公開時に「インストール不要で即体験」できるブラウザデモを GitHub Pages 用静的成果物として用意する。Sandbox（3I）のサブセット機能を、**C# ルーティングエンジン（OsmDotRoute コア）を WebAssembly 化**してブラウザ内で実行し、UI は 3I の React 資産を流用する。

**スコープ（§3.9 の可否表に準拠）**:

| 機能 | 3J | 理由 |
|---|:---:|---|
| 事前ビルド `.odrg` の経路計算 | ○ | コアを WASM 化、`.odrg` を静的アセット同梱 |
| メッシュ / ポリゴン制約 + Re-Route | ○ | 制約管理もクライアント側で完結 |
| 道路ネットワーク / メッシュグリッド表示 | ○ | コアの GeoJSON 出力を WASM 経由で取得 |
| PBF ダウンロード | × | Geofabrik は CORS 非対応、ブラウザ直接取得不可 |
| Extractor（PBF → `.odrg`） | × | WASM での大規模 PBF パースは非現実的 |

**Done 判定**:

1. `samples/Sandbox.Wasm/` を `dotnet publish` し、`samples/Sandbox/Web` を WASM モードでビルドした**静的成果物のみ**（ローカル HTTP サーバ不要）でブラウザが起動し、地図が表示される
2. 同梱の事前ビルド `.odrg`（津島市等）をプルダウンから選択 → ブラウザ内ロード → 道路ネットワーク表示
3. 2 点指定 → ブラウザ内経路計算 → 経路描画 + 距離 / 所要時間表示（HTTP リクエストゼロを DevTools で確認）
4. メッシュグリッド表示（1 km / 500 m / 250 m）→ メッシュクリックで Block / Difficulty 付与
5. ポリゴン描画 → 難所タイプ選択 → Block / Difficulty 付与
6. Re-Route で制約回避経路を確認（**動的制約のキラー機能がブラウザ内で完結**）
7. GitHub Actions ワークフローが静的成果物 artifact を生成する（gh-pages への push は無効化し 3H で有効化）
8. 設計書 `phase3_design.md` §10.5 が肉付けされ、題が「React 流用 + コア WASM 化」に改題される
9. OsmDotRoute `dotnet test` **676 件 pass** を維持（コア改修の回帰なし）+ in-memory ロード突合テストを追加

---

## 2. ユーザー判断事項

### 2.1 確定済み（2026-05-29、§0 表参照）

J-A（React 流用）/ J-B（in-memory コア改修）/ J-C（公開は 3H 同時）。

### 2.2 着手時に確定（推奨案を提示、各サブステップ着手時にユーザー確認）

| # | 項目 | 推奨案 | 代替案 | 確定タイミング |
|---|---|---|---|---|
| J-1 | WASM ランタイム方式 | **(a) IL インタプリタ**（ビルド単純、まずは動作優先） | (b) WASM AOT（高速だが `wasm-tools` AOT 必須・ビルド重い）。性能不足が判明したら移行 | 3J.1 |
| J-2 | in-memory ロード公開 API | **(a) `RouterDb.LoadFromOdrg(ReadOnlyMemory<byte>)` オーバーロード追加**（既存ファイル版を残し追加のみ、非破壊） | (b) 別名 `LoadFromOdrgBytes` | 3J.2 |
| J-3 | 同梱事前ビルド `.odrg` | **(a) 津島市 + 小規模 1〜2 プリセット**（DL サイズ優先） | (b) 津島市のみ / (c) 都道府県級も含む（サイズ大） | 3J.3 |
| J-4 | React の WASM / HTTP 切替方式 | **(a) Vite ビルドモード分離**（dev = 既存 Sandbox Server へ HTTP、`build:wasm` = WASM クライアント） | (b) ランタイム自動検出 | 3J.5 |
| J-5 | WASM 成果物のホスト形態 | **(a) React 成果物に WASM を同梱（単一の静的サイト）** | (b) WASM を別パスに分離 | 3J.5 |

---

## 3. 技術構成

### 3.1 全体像

```text
[ブラウザ（GitHub Pages 静的ホスティング）]
  React UI（3I 流用）  ──→  api/wasmClient.ts  ──[JS interop]──┐
   MapView / RoutePanel / MeshGridPanel / PolygonEditorPanel    │
                                                                ▼
                              Sandbox.Wasm（.NET WASM、JSExport ブリッジ）
                                                                │ プロジェクト参照
                                                                ▼
                              OsmDotRoute コア（NativeRoadGraph + Router + RestrictedAreaService）
                                                                │ in-memory ソース
                                                                ▼
                              事前ビルド .odrg（wwwroot 静的アセット、fetch → byte[]）
```

- **UI は 3I の React をそのまま流用**。違いは「サーバ REST 呼出」を「WASM JS interop 呼出」に差し替える `api` 層のみ。
- **WASM 化するのは OsmDotRoute コアエンジン**。`Sandbox.Wasm` は薄いブリッジ（`[JSExport]` メソッド群）で、既存 `client.ts` の DTO 形状（`RouteResponse` / `StatsResponse` / `RestrictionItem` 等）に一致する JSON 文字列を返す。
- **PBF DL / Extract は WASM では実装しない**。該当パネルは WASM モードで非表示、代わりに「事前ビルド `.odrg` プルダウン」を表示。

### 3.2 採用技術スタック

| 層 | 技術 | 備考 |
|---|---|---|
| WASM プロジェクト | `Microsoft.NET.Sdk.WebAssembly`（**WebAssembly Browser App**、Blazor ではない） | `[JSExport]` / `[JSImport]` で JS ⇔ C# 相互運用。`wasm-tools` ワークロード前提 |
| ルーティングエンジン | `OsmDotRoute` コア（net9.0、外部 NuGet ゼロ） | プロジェクト参照。`unsafe` / `Span` は WASM でも動作 |
| UI | React + TypeScript + Vite + MapLibre GL（3I 流用） | 追加 npm 依存は増やさない |
| 同梱データ | 事前ビルド `.odrg`（静的アセット） | `fetch` → `ArrayBuffer` → `byte[]` |
| デプロイ | GitHub Actions（`dotnet publish` + `npm run build` → 静的成果物） | gh-pages push は 3H まで無効化（J-C） |

### 3.3 コア改修（in-memory `.odrg` ソース、J-B）

**課題**: `NativeRoadGraph` は `OdrgMmfHandle`（`MemoryMappedFile.CreateFromFile` + `byte*`）に具象結合している。ブラウザ WASM には実ファイルシステムの MMF が無いため、このままでは `.odrg` を読めない。

**方針**: `OdrgMmfHandle` が提供する読込 API（`GetSpan<T>` / `GetRawSpan` / `ViewLength`、およびセクション辞書読込）を抽象化し、**MMF 版（既存・ファイル）** と **in-memory 版（pinned `byte[]`）** の 2 実装を持たせる。`NativeRoadGraph` は抽象経由でアクセスする。

- 抽象（internal）: `.odrg` バイト列に対する zero-copy span プロバイダ。`GetSpan<T>(offset, count)` / `GetRawSpan(offset, length)` / `ViewLength`。
- in-memory 実装: `GCHandle.Alloc(bytes, Pinned)`（または `Memory<byte>.Pin()`）でピン留めし `byte*` を取得。`unsafe` ポインタ経路は既存と同一ロジックを再利用。
- **結合点の解消**: `OdrgSectionDirectory.Read(SafeMemoryMappedViewHandle, long)` が MMF ハンドルに依存している。バイト span から読めるオーバーロードを追加し、両ソースから呼べるようにする。
- 公開 API: `RouterDb.LoadFromOdrg(ReadOnlyMemory<byte>)` オーバーロードを追加（J-2(a)、ファイル版は維持・**非破壊**）。
- **歯止め**: 既存 676 件 pass を維持。MMF ロード結果と in-memory ロード結果が**同一**（頂点 / エッジ / 経路 / R-tree クエリ）であることを突合テストで保証（R2 対処）。

### 3.4 WASM ブリッジ（`Sandbox.Wasm`、JSExport 面）

既存 `client.ts` の 3J サブセット相当を、JSON 文字列を返す `[JSExport]` メソッドとして実装する（DTO 形状は既存と一致させ React 側変更を最小化）。

| 既存 REST（client.ts） | WASM ブリッジ | 備考 |
|---|---|---|
| `loadOdrg` / `fetchGraphStats` | `LoadOdrgBytes(byte[]) → StatsResponse(JSON)` | fetch したバイト列を渡す |
| `fetchRoadNetwork` | `GetRoadNetworkGeoJson() → GeoJSON(JSON)` | `Router.GetRoadNetworkGeoJson()` |
| `calculateRoute` | `Route(reqJson) → RouteResponse(JSON)` | `Router.Calculate` |
| `snapToRoad` | `Snap(reqJson) → SnapResponse(JSON)` | `Router.SnapToRoad` |
| `fetchMeshGrid` | `MeshGrid(reqJson) → GeoJSON(JSON)` | `MeshCodeConverter` ベース |
| `registerPolygonRestriction` / `registerMeshRestriction` | `AddRestriction(reqJson) → {id}` | `RestrictedAreaService` |
| `listRestrictions` / `fetchRestrictionsGeoJson` | `ListRestrictions()` / `RestrictionsGeoJson()` | |
| `deleteRestriction` / `clearAllRestrictions` | `DeleteRestriction(id)` / `ClearRestrictions()` | |

**スコープ外（WASM 非実装）**: `downloadPbf` / `extractOdrg` / `fetchRegions` / `fetchCacheStatus` / `browseDirectory`。

### 3.5 React 統合（api 層差替、J-4 / J-5）

- `samples/Sandbox/Web/src/api/wasmClient.ts` を新規追加（`client.ts` と**同一インターフェース**を実装、内部で WASM JSExport を呼ぶ）。
- Vite ビルドモードで切替（J-4(a)）: `dev`（既存）= Sandbox Server へ HTTP / `build:wasm` = `wasmClient`。
- WASM モードでは DownloadPanel / ExtractPanel を非表示にし、「事前ビルド `.odrg` プルダウン」を表示（Load 起点を切替）。
- 事前ビルド `.odrg` を `wwwroot`（または Vite public）に同梱し、選択時に `fetch` → `byte[]` → `LoadOdrgBytes`。

### 3.6 ビルド / デプロイ（J-C）

- `.github/workflows/` に WASM publish + React build → 静的成果物生成ジョブを追加。
- **gh-pages への push ステップは 3J では無効化**（コメントアウトまたは `if: false` ガード）。artifact 生成・検証までを 3J の Done とし、実公開は 3H で REQ-PKG-002 解除と同時に有効化。
- 非公開リポジトリのままでも CI で静的成果物が出力されることを確認（公開はしない）。

---

## 4. サブステップ分割

各サブステップ完了時に **ユーザー報告 → 承認 → 次へ**（CLAUDE.md ルール）。

| # | 内容 | Done 判定 |
|---|---|---|
| **3J.1** | WASM プロジェクト雛形（`samples/Sandbox.Wasm`、WebAssembly Browser App テンプレ、コア参照、最小 `[JSExport]`） | `dotnet publish` 成果物をブラウザで開き、JS から `[JSExport]`（例: version）を呼べる。J-1 確定 |
| **3J.2** | コア in-memory `.odrg` ソース（§3.3。抽象化 + in-memory 実装 + `LoadFromOdrg(ReadOnlyMemory<byte>)` + 突合テスト） | 676 件 pass 維持 + MMF/in-memory 同一結果テスト追加。J-2 確定 |
| **3J.3** | WASM ルーティングブリッジ（§3.4 の load / stats / road-network / route / snap）+ 事前ビルド `.odrg` 同梱 | ブラウザ内でロード → 経路計算 → GeoJSON 返却。J-3 確定 |
| **3J.4** | WASM 制約 + メッシュブリッジ（§3.4 の restriction 系 + mesh grid、Re-Route 連動） | ブラウザ内で制約付与 → Re-Route 回避を確認 |
| **3J.5** | React 統合（`wasmClient.ts`、Vite モード分離、パネル切替、事前ビルド `.odrg` プルダウン） | 静的成果物のみで一連フロー完結（HTTP ゼロを DevTools 確認）。J-4 / J-5 確定 |
| **3J.6** | GitHub Actions ワークフロー整備（静的成果物 artifact 生成、gh-pages push は無効化） | CI が artifact 生成。設計書 §10.5 肉付け + 改題 |

---

## 5. リスクと対処

| # | リスク | 影響 | 対処 |
|---|---|---|---|
| RJ1 | `NativeRoadGraph` / `OdrgMmfHandle` の `unsafe` ポインタが WASM で範囲外アクセス | クラッシュ・原因切り分け困難 | 3J.2 で in-memory 実装を `OdrgReader`（eager-parse）/ MMF 版と突合。`Span.Length` チェック維持。pin 解放を `IDisposable` で厳格管理 |
| RJ2 | in-memory ソース追加でコア既存動作が回帰 | 676 件失敗、Itinero 撤去後の確定土台が崩れる | 抽象化は**追加のみ・既存 MMF 経路は不変**。in-memory/MMF 同一結果テストを CI 化。公開 API はオーバーロード追加で非破壊（J-2a） |
| RJ3 | .NET WASM ランタイムの DL サイズが大きく初回ロードが重い | UX 低下 | J-1 で IL インタプリタ採用しつつトリミング。重ければ 3J.1 で AOT 比較。README に初回ロードサイズを明記 |
| RJ4 | WASM 単一スレッド制約（`Task.Run` 並列不可） | 経路計算中に UI フリーズ | コアの経路計算は同期完結で問題なし。重い場合はメッシュ生成上限を設け、必要なら Web Worker 化を 3J.5 で検討 |
| RJ5 | gh-pages 公開が非公開リポジトリ方針（REQ-PKG-002）と衝突 | コンプラ違反 | J-C 確定どおり 3J では push 無効化、実公開は 3H。CI は artifact 生成のみ |
| RJ6 | 事前ビルド `.odrg` のサイズが大きく静的サイトが肥大 | DL 遅延 | J-3 で津島市 + 小規模に限定。都道府県級は同梱しない |
| RJ7 | `EmbeddedResource` プロファイル JSON が WASM で読めない | プロファイル評価不能 | 3J.1 で manifest resource stream 読込を WASM 上で確認（標準的に動作するが早期検証） |

---

## 6. 設計書更新方針（§2.5 ルール準拠）

- 各サブステップ完了時に `phase3_design.md` §10.5 を肉付け。
- §10.5 の題を「GitHub Pages デモ（Blazor WASM）」→「**GitHub Pages デモ（React 流用 + コア WASM 化）**」に改題（章対応表 L62 も更新）。
- コア改修（§3.3）はコアの設計章にも波及するため、in-memory ソース抽象は §3（NativeRoadGraph）の補足として追記。

---

## 7. スコープ外（3J では扱わない）

- PBF ダウンロード / Extractor の WASM 化（§3.9 可否表で × 確定）
- gh-pages への実公開（3H、REQ-PKG-002 解除と同時）
- WASM AOT 最適化の本格導入（J-1 で性能不足が判明した場合のみ。既定は IL インタプリタ）
- 制約の永続化 / エクスポート（Sandbox 同様セッション中のみ）
- Web Worker 化（RJ4 で必要性が出た場合に限り検討）

---

## 8. 改訂履歴

| 版 | 日付 | 内容 | 担当 |
|---|---|---|---|
| 0.1 (draft) | 2026-05-29 | 初版起草。原計画 §3.9 の Blazor 前提を修正し、ユーザー確認済み 3 方針（React 流用 / in-memory コア改修 / 公開は 3H 同時）で再定義。サブステップ 3J.1〜3J.6、着手時ユーザー判断 J-1〜J-5、リスク RJ1〜RJ7、設計書 §10.5 改題方針を記載 | Claude (Opus 4.7) |
