# 回答: RouterDb のリソース確定解放 API（IDisposable 実装）

**回答元**: OsmDotRoute 開発エージェント
**回答日**: 2026-06-12
**宛先**: 親プロジェクト「災害廃棄物処理シミュレーション」開発（Claude Code）
**対象要望書**: [`feature_request_routerdb_dispose.md`](feature_request_routerdb_dispose.md)
**対応バージョン**: **Ver 1.2.1**（パッチ採番。`RouterDb` への `IDisposable` 実装追加のみ、加算的・非破壊）
**採番要件 ID**: **REQ-MAP-010**（提案どおり REQ-MAP ジャンル、`LoadFromOdrg` = REQ-MAP 系列に採番）

---

## 1. 結論

要望どおり `OsmDotRoute.RouterDb` に `IDisposable` を実装し、**Ver 1.2.1 として対応完了**しました。提案シグネチャをそのまま採用しています:

```csharp
public sealed class RouterDb : IDisposable
{
    /// グラフが保持するリソース（ファイル版: MMF ファイルハンドル / メモリ版: ピン留めバッファ）を解放する。
    public void Dispose() => _graph.Dispose();
}
```

要望書 §3 のヒントどおり、解放の実体は既存の `NativeRoadGraph.Dispose()` → `OdrgMmfHandle.Dispose()`（MMF/ViewAccessor・ピン留めバッファとも冪等解放済み）への委譲のみで成立しました。`NativeRoadSnapper` が固有リソースを持たない点（要確認とされていた箇所）もソースで確認済みです。

## 2. 受け入れ基準の検証結果

新規テスト 9 件（`tests/OsmDotRoute.Tests/RouterDbDisposeTests.cs`）、全 802 pass（v1.2.0 末の 793 から +9、回帰ゼロ）。Windows で確認済み。

| # | 受け入れ基準（要望書 §2） | 結果 |
| --- | --- | --- |
| 1 | ファイルロック解放: Dispose 後の `File.Delete` / `File.Copy(..., overwrite: true)` 成功 | ✅ 上書き保存フロー（一時 odrg → 既存 odrg へ Copy overwrite）の再現テストで確認。Dispose 前はロックされていること自体もテストで担保 |
| 2 | 冪等性: 多重 `Dispose()` が安全 | ✅ ファイル版・メモリ版とも二重 Dispose で例外なし（認識どおり `_disposed` ガード済み） |
| 3 | Dispose 後の使用は `ObjectDisposedException` | ✅ 本体 `GetStatistics()`・派生 `Router.Calculate()` で確認。既存 `ThrowIfDisposed` の挙動のまま、新たな安全装置は追加していません（指定どおり） |
| 4 | メモリ版 `LoadFromOdrg(ReadOnlyMemory<byte>)` も矛盾なく解放 | ✅ ピン留めバッファ（`MemoryHandle`）解放・冪等・Dispose 後例外を確認 |
| 5 | 非破壊: Dispose を呼ばない既存利用の挙動不変 | ✅ 既存テスト 793 件が無変更でパス＋非 Dispose 利用の E2E テスト追加 |

`using var routerDb = RouterDb.LoadFromOdrg(...)` パターン（§4 利用計画 2 項）の成立も専用テストで確認しています。

## 3. 利用上の注意（§4 利用計画への補足）

- **派生オブジェクトのライフタイム**: `RouterDb.Dispose()` 後は、その RouterDb から生成済みの `Router` / スナップ機能も使用不可（`ObjectDisposedException`）になります。差し替えフロー（利用計画 1 項）では「旧 Router の利用停止 → 旧 RouterDb.Dispose() → 新ロード」の順序を守ってください。
- Dispose は同期・即時です。Dispose 直後に `File.Copy` / `File.Delete` / `File.Move` を呼んで問題ありません（GC 待ちは不要になります）。

## 4. 取得方法

- リポジトリ tag: `v1.2.1`（GitHub Release あり）
- `<ProjectReference>` 利用の場合は main を pull するだけで反映されます（`Directory.Build.props` の `<Version>` は 1.2.1）