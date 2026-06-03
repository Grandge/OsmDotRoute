# macOS CI 自動ミラーの仕組み（運用メモ）

**目的**: 手元に Mac がないため、macOS（Apple Silicon / ARM64）でのテストを GitHub Actions で自動実行する。
そのために本体リポジトリのコードを専用の検証用リポジトリへ**自動コピー（ミラー）**し、そこで macOS テストを走らせる。

> このドキュメントは内部運用メモです。ミラー時に `Documents/` ごと除外されるため、検証リポジトリには公開されません。

---

## 1. なぜミラーするのか

`.odrg`（独自グラフ形式）は OS / CPU アーキに依存しやすいメモリマップ（MMF）経路を持つ。
Windows（x64）と Linux（WSL2, x64）では検証済みだが、**ARM64 + 16KB ページ**という macOS 固有の条件は実機でしか確認できない。

GitHub Actions の `macos-latest` ランナーは現在 Apple Silicon（ARM64）。
**public リポジトリなら macOS ランナーは無料**なので、これを使って自動検証する。

> 補足: 本体 `Grandge/OsmDotRoute` も現在は public なので、本来は本体 CI に macOS を足すだけでも無料で検証できる。
> 専用リポジトリにしている理由は「本体 CI をリーンに保つ」「検証専用として関心を分離する」ため（必須ではない設計判断）。

---

## 2. 登場するもの（全体像）

```
┌─────────────────────────────┐
│ 本体リポジトリ                │
│ github.com/Grandge/OsmDotRoute (private/public どちらでも)
│                             │
│  .github/workflows/mirror.yml   ← ① 同期の実行役（main push で起動）
│  .mirror/ci-macos.yml           ← ② 検証用ワークフローの「正本」
│  （secret: MIRROR_TOKEN）        ← ③ ミラー先へ push する鍵（PAT）
└──────────────┬──────────────┘
               │ ① が rsync で除外/注入して force-push（③ で認証）
               ▼
┌─────────────────────────────┐
│ 検証リポジトリ（public）       │
│ github.com/Grandge/OsmDotRoute-ci-macos
│                             │
│  .github/workflows/ci-macos.yml ← ② が注入されたもの。push で起動
│  （本体のコード一式。下記は除外）
└──────────────┬──────────────┘
               │ ci-macos.yml が macos-latest で dotnet test
               ▼
        ✅ macOS (ARM64) で 693/693 pass
```

### 各要素の役割

| # | もの | 場所 | 役割 |
|---|---|---|---|
| ① | `mirror.yml` | 本体 `.github/workflows/` | 本体 `main` が更新されるたびに起動し、ミラーを作って検証リポジトリへ送る |
| ② | `.mirror/ci-macos.yml` | 本体 `.mirror/` | 検証リポジトリで動かす macOS テストの定義（正本）。本体では**動かない**（後述） |
| ③ | secret `MIRROR_TOKEN` | 本体の Settings → Secrets | ①が検証リポジトリへ push するための認証トークン（PAT） |
| — | `ci-macos.yml` | 検証リポジトリ `.github/workflows/` | ①が②を注入したもの。push を受けて macOS テストを実行 |

---

## 3. 同期の流れ（main push したとき）

1. 本体 `main` に push（または merge）が入る。
2. 本体の `mirror.yml` が起動（`if: github.repository == 'Grandge/OsmDotRoute'` で本体限定）。
3. `mirror.yml` が本体のコードを一時フォルダへコピーしつつ、次を**除外**:
   - `Documents/`（親プロ情報を含む内部文書 → 公開しない）
   - `samples/Data/tokyo.odrg`（テストで使わない大容量サンプル → リーン化）
   - `.github/`（本体のワークフロー類はミラーに不要・暴発防止）
   - `.mirror/`（正本テンプレ自身）
   - ※ `samples/Data/tsushima.odrg` は**テスト必須フィクスチャなので残す**
4. `.mirror/ci-macos.yml` を、ミラー側の `.github/workflows/ci-macos.yml` として**注入**。
5. その一時フォルダを `MIRROR_TOKEN` で認証して検証リポジトリの `main` へ **force-push**（毎回まっさらな1コミットで上書き）。
6. 検証リポジトリが push を受け、`ci-macos.yml` が `macos-latest` で `dotnet test` を実行。
7. 結果（緑 / 赤）が検証リポジトリの Actions タブに出る。

---

## 4. なぜこういう作りなのか（疑問になりやすい点）

### Q. なぜ `.mirror/ci-macos.yml` という「正本」を別に置く？
本体の `.github/workflows/` に macOS 用ワークフローを直接置くと、本体（public）でも macOS テストが走ってしまう。
そこで本体では**ただのテキストファイル**（`.mirror/` 配下）として持ち、同期時にミラーへ「ワークフローとして」注入する。
こうすると本体では一切動かず、ミラーでだけ動く。

### Q. なぜ普通の `GITHUB_TOKEN` ではなく PAT（MIRROR_TOKEN）が要る？
- Actions が自動で持つ `GITHUB_TOKEN` は**自分のリポジトリ内**しか操作できない。別リポジトリ（検証用）へは push できない。
- さらに、`GITHUB_TOKEN` で push した変更は**相手側のワークフローを起動しない**仕様。PAT で push すると起動する。
- だから「別リポジトリへ push」かつ「相手の macOS テストを起動」の両方を満たすため PAT が必要。

### Q. force-push で履歴が消えないの？
検証リポジトリは「本体の最新スナップショットを毎回上書き」する使い捨てミラー。履歴は持たない設計（CI 用途なので問題なし）。
**検証リポジトリ側は手で編集しない**こと（次の同期で上書きされる）。

---

## 5. 有効化の条件

`mirror.yml` が動くのは、それが本体の **`main` ブランチに載ってから**。
merge 後の最初の main 更新から自動で回り始める。

> それまでの間は「手動ミラー」で検証済み（初回 push は手動実施し、macOS 693/693 pass を確認済み）。

---

## 6. PAT（MIRROR_TOKEN）の再発行手順 ★期限切れ時はここ

PAT には有効期限があり、切れるとミラー push が 403 で失敗する。その場合は新しい PAT を発行して `MIRROR_TOKEN` を入れ替える。

### 6.1 発行ページ
GitHub → 右上アイコン → **Settings** → 左下 **Developer settings** →
**Personal access tokens** → **Fine-grained tokens** → **Generate new token**
（直リンク: https://github.com/settings/personal-access-tokens ）

### 6.2 設定する項目（fine-grained PAT）

| 項目 | 設定値 |
|---|---|
| **Token name** | `osmdotroute-mirror-to-ci-macos`（用途が分かる名前。任意） |
| **Expiration** | 任意（切れたら本手順で再発行する前提。長め可） |
| **Resource owner** | `Grandge`（自分のアカウント） |
| **Repository access** | **Only select repositories** → **`OsmDotRoute-ci-macos`** だけを選ぶ |
| **Permissions → Repository permissions** | 下表の 2 つを設定（他は No access のまま） |

**Repository permissions（必須はこの2つ）**:

| 権限 | レベル | なぜ必要か |
|---|---|---|
| **Contents** | **Read and write** | ミラー先へコード/ファイルを push するため |
| **Workflows** | **Read and write** | push する内容に `.github/workflows/ci-macos.yml` が含まれるため。これが無いと「workflows 権限なしでは workflow ファイルを更新できない」と push が拒否される |
| Metadata | Read-only（自動） | 上記を選ぶと自動で付く。変更不要 |

> classic PAT を使う場合は scope **`repo`** + **`workflow`** の2つにチェック（ただし classic は全リポジトリに効くため、対象を絞れる fine-grained 推奨）。

### 6.3 発行したトークンを secret に登録

発行直後に表示されるトークン（`github_pat_...`）を控え、本体リポジトリの secret を上書きする:

```powershell
"<新しいPAT>" | & "D:\tools\gh\bin\gh.exe" secret set MIRROR_TOKEN --repo Grandge/OsmDotRoute --body -

# 登録確認（値は見えない・更新時刻が変われば OK）
& "D:\tools\gh\bin\gh.exe" secret list --repo Grandge/OsmDotRoute
```

> secret 名は **`MIRROR_TOKEN` 固定**（`mirror.yml` が参照）。名前を変えると動かない。
> トークンを一時ファイルに保存した場合は登録後すぐ削除すること。

### 6.4 動作確認

```powershell
# 本体の mirror.yml を手動起動（main に載っている場合）
& "D:\tools\gh\bin\gh.exe" workflow run mirror.yml --repo Grandge/OsmDotRoute
# → 検証リポジトリ側で ci-macos が緑になれば成功
& "D:\tools\gh\bin\gh.exe" run list --repo Grandge/OsmDotRoute-ci-macos --limit 3
```

---

## 7. 運用コマンド集（`gh` 使用、フルパス例）

```powershell
# 検証リポジトリの最近の実行を見る
& "D:\tools\gh\bin\gh.exe" run list --repo Grandge/OsmDotRoute-ci-macos --limit 5

# 直近の実行の詳細／失敗ログ
& "D:\tools\gh\bin\gh.exe" run view <run-id> --repo Grandge/OsmDotRoute-ci-macos --log-failed

# 本体の mirror.yml を手動起動（main に載った後のみ可）
& "D:\tools\gh\bin\gh.exe" workflow run mirror.yml --repo Grandge/OsmDotRoute
```

> 補足: `gh` は `D:\tools\gh\bin\gh.exe` に portable 配置。新しいターミナルなら PATH 反映済みで `gh` だけでも可。

---

## 8. よくあるトラブル

| 症状 | 原因 | 対処 |
|---|---|---|
| ミラーの push が 403 で失敗 | PAT 期限切れ / 権限不足 | **§6 の手順で PAT を再発行**し `MIRROR_TOKEN` を更新。権限は **Contents + Workflows 両方 write**、対象リポは `OsmDotRoute-ci-macos` |
| ミラーは更新されるが macOS テストが起動しない | `GITHUB_TOKEN` で push している等 | PAT（MIRROR_TOKEN）で push しているか確認（§4 の Q 参照） |
| `tsushima.odrg が見つかりません` で大量失敗 | 必須フィクスチャを除外した | `mirror.yml` の除外から `tsushima.odrg` を外す（現状は含める設定） |
| `mirror.yml` が走らない | まだ `main` に無い | main に merge する（§5） |

---

## 9. 関連

- 検証リポジトリ: https://github.com/Grandge/OsmDotRoute-ci-macos
- 計画・結果: [phase3_step3H_plan.md](phase3_step3H_plan.md) §5、[phase4_multiplatform_plan.md](phase4_multiplatform_plan.md)
- 設計記録: [phase3_design.md](phase3_design.md) §11.6
