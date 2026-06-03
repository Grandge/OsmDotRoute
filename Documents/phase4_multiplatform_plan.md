# Phase 4 実装計画書：マルチプラットフォーム対応

**バージョン**: ユーザー採番（未採番）
**作成日**: 2026-06-03
**ステータス**: 計画ドラフト（ユーザー合意待ち）
**対象**: Phase 4 スコープ第 2 項「マルチプラットフォーム対応」（第 1 項「プロファイル追加」は完了・push 済み）
**関連ドキュメント**:

- [Phase 4 設計書](phase4_design.md)（§0.1：本項は別計画書で扱うと明記）
- [Phase 3 ステップ 3H 計画書](phase3_step3H_plan.md)（macOS CI ミラー・Linux 検証の前段。`feature/phase3-3h-packaging` にアーカイブ）
- [macOS CI 自動ミラーの仕組み](macos_ci_mirror.md)（運用メモ。3H ブランチ在。本計画で main へ移植）
- [要件定義書](requirement_definition.md)（CI / プラットフォーム範囲の該当 REQ）

---

## 0. 背景と現状（2026-06-03 調査）

マルチプラットフォーム検証の基盤は **Phase 3 ステップ 3H で構築・検証済み**だが、NuGet 公開保留に伴い `feature/phase3-3h-packaging` にアーカイブされ **main 未マージ**である。3H ブランチは Phase 4 より前のため、**丸ごとマージすると Phase 4 のプロファイル追加成果（753 pass）を削除してしまう**。よって本対応は「3H の検証基盤を Phase 4 成果を保ったまま main へ移植し、Phase 4 コードを各プラットフォームで再検証する」ことが実体となる。

### 0.1 現状サマリ

| 項目 | 状態 |
| --- | --- |
| macOS CI ミラー基盤（`mirror.yml` / `.mirror/ci-macos.yml` / `macos_ci_mirror.md`） | 3H ブランチに構築・検証済み。**main 未反映** |
| macOS 検証リポジトリ `Grandge/OsmDotRoute-ci-macos`（public） | 稼働中。最後の検証は **Phase 4 前スナップショット（693 pass）**。Phase 4 コードは macOS ARM64 で**未検証**。初回自動ミラーで Phase 4 スナップショットに**置換（空にして再投入）**される |
| `MIRROR_TOKEN` secret（本体リポジトリ） | 登録済み（2026-06-02）・`ci-macos` 向けスコープ済み。即運用可 |
| 自動ミラー（`mirror.yml` の main push トリガ） | main に未存在のため**未起動**。本計画で有効化する |
| Linux 検証 | main の既存 CI（ubuntu-latest）が毎 push で検証中。WSL2 ローカル実データ検証は **Phase 4 前（693 pass）**のまま |
| 3H の NuGet メタデータ（RepositoryUrl / サブパッケージ README 等） | NuGet 公開無期限保留中。**本計画のスコープ外**（main へ前倒ししない） |

### 0.2 検証の主眼（3H から継承）

OS / アーキ依存リスクは `.odrg` の MMF / `byte*` 直アクセス経路（`OdrgMmfHandle.cs` の `AcquirePointer` + `PointerOffset` 補正、`GetSpan<T>` の unmanaged 構造体 zero-copy 解釈）に集中する。検証はここを各プラットフォームで通すことが目的。

- macOS：Apple Silicon (ARM64) + 16KB ページ前提が破綻しないこと（実機検証は GitHub Actions `macos-latest` 一択）。
- Linux：パス大小文字区別・埋込リソース読込・x64 での `.odrg` 決定性。

---

## 1. 確定済みの判断（2026-06-03 ユーザー決定）

| 項目 | 決定 |
| --- | --- |
| 完了条件 | **Linux は WSL2 で、macOS は GitHub Actions（ミラー）で Phase 4 コードを検証**する |
| macOS 検証リポジトリ | 既存 **`Grandge/OsmDotRoute-ci-macos`** を継続使用。初回ミラーで旧 693 スナップショットを Phase 4 スナップショットに置換 |
| 自動ミラー | `mirror.yml` を main に載せ、**main push 毎の自動同期＆ARM64 テストを有効化する** |
| NuGet メタデータ（3H の B 系統） | 公開保留のため本計画では main へ前倒ししない（スコープ外） |
| バージョン採番 | ユーザー管理（本計画では採番しない） |

---

## 2. ステップ計画

> 各ステップ完了時に停止してユーザー確認（CLAUDE.md ルール）。実施結果は本書 §3 に追記する（3H 計画書と同方針で、本計画書が plan と results を兼ねる）。

### Step M1：macOS ミラー基盤を main へ移植

3H ブランチから検証基盤ファイルのみを取得して main に置く（ブランチマージはせず、Phase 4 成果を保持）。

- `.github/workflows/mirror.yml`（本体 main push → ci-macos へ一方向ミラー。`if: github.repository == 'Grandge/OsmDotRoute'` で本体限定）
- `.mirror/ci-macos.yml`（検証リポジトリで動く macOS テスト定義の正本。本体では稼働しない）
- `Documents/macos_ci_mirror.md`（運用メモ：仕組み・PAT 再発行手順・トラブルシュート）

取得方法は `git show feature/phase3-3h-packaging:<path>` で内容を取り出し main に書き出す。3 ファイルとも Phase 4 固有の記述を含まず、リポジトリ全体をミラーして同一テストプロジェクト（`tests/OsmDotRoute.Tests`）を走らせる作りのため、**移植だけで Phase 4 プロファイルも自動的に検証対象**になる。

確認事項：

- ミラー除外範囲は 3H のまま（`Documents/`・`samples/Data/tokyo.odrg`・`.github/`・`.mirror/` を除外、`samples/Data/tsushima.odrg` は必須フィクスチャのため同梱）。Phase 4 で `Documents/` に増えた公開ドキュメント（profile_guide 等）はミラー検証に不要のため除外維持で問題ない。
- `MIRROR_TOKEN` は登録済み・権限（Contents + Workflows write）も 3H で検証済み。

### Step M2：自動ミラーで Phase 4 を macOS ARM64 検証

- Step M1 を main へ push した時点で `mirror.yml` が起動し ci-macos へ同期（force-push で旧 693 スナップショットを置換）。
- ci-macos の `ci-macos.yml`（`macos-latest`）が `dotnet test tests/OsmDotRoute.Tests -c Release` を実行。
- **Phase 4 の 753 pass（新プロファイル 3 種・ProfileResolver 含む）が macOS ARM64 で 0 fail / 0 skip を達成**することを run ログで確認。
- 必要なら `gh workflow run mirror.yml` で手動起動して即確認。

### Step M3：Linux WSL2 で Phase 4 を再検証

3H §4 の手順を Phase 4 コードで再実行する。

1. WSL2（Ubuntu）の .NET 9 SDK で `dotnet test tests/OsmDotRoute.Tests -c Release` → **753 pass / 0 fail / 0 skip**（`/mnt/d` 経由で Windows 実データ可視のためデータ依存テストも通る）。
2. 配布 3 本（OsmDotRoute / Pbf / DI）の `dotnet pack` が Linux で警告ゼロ生成。
3. （任意）Extractor を Linux で 1 回通し、Windows 生成 `.odrg` とグラフ本体を SHA256 比較（3H で x64 決定性確認済み・Phase 4 で評価機構不変のため再現性は維持見込み）。

### Step M4：ドキュメント・要件・メモリ反映

- 本計画書 §3 に各ステップの実施結果（pass 数・run-id・所見）を追記。
- [phase4_design.md](phase4_design.md)：マルチプラットフォーム対応の設計記録章を追加（§0.3 の表に行追加）。
- [requirement_definition.md](requirement_definition.md)：CI / プラットフォーム範囲の該当 REQ に Phase 4 マルチプラットフォーム検証完了を反映。
- メモリ更新（[[project_phase_status]] に Phase 4 完了、[[project_nuget_crossplatform_plan]] の自動ミラー有効化を反映）。

---

## 3. 実施結果

（各ステップ完了時に追記）

---

## 4. スコープ外（明示）

- **NuGet 実公開**：無期限保留（メモリ project_nuget_publish_hold）。本計画は検証のみ。
- **3H の NuGet メタデータ（RepositoryUrl / PackageReadmeFile / サブパッケージ README）の main 前倒し**：公開判断とセットのため本計画では扱わない。
- **ARM Linux / Windows ARM**：3H 同様、ARM 検証は macOS（Apple Silicon）に委ね、Linux は x64（WSL2）に限定する。

---

## 5. 改訂履歴

| Ver | 日付 | 変更 |
| --- | --- | --- |
| ドラフト | 2026-06-03 | 初版。3H 検証基盤の main 移植＋Phase 4 コードの macOS(Actions)/Linux(WSL2) 再検証を 4 ステップで計画。macOS 検証先は既存 `OsmDotRoute-ci-macos` を継続使用（初回ミラーで旧スナップショット置換）。ユーザー決定（完了条件・自動ミラー有効化・NuGet スコープ外）を §1 に記録。バージョンはユーザー採番 |
