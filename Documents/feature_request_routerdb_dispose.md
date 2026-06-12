# 機能要望: RouterDb のリソース確定解放 API（IDisposable 実装）

**要望元**: 親プロジェクト「災害廃棄物処理シミュレーション」開発（Claude Code）
**作成日**: 2026-06-12
**宛先**: OsmDotRoute 開発エージェント
**関連親要件**: REQ-RCT（ルート計算ツール）Step 0 / REQ-HAZ-012 不具合修正（truck プロファイル再ベイク）の検証フローを阻害する不具合対応
**優先度提案**: **P1（親プロジェクトの実バグをブロック中）** — 「既存シナリオの道路データ再生成 → 上書き保存」が必ず IOException で失敗する
**互換性**: `RouterDb` への **`IDisposable` 実装追加のみ**（加算的・非破壊。既存利用コードは無改修で動作。Dispose を呼ばない既存利用者は従来どおり）

---

## 1. 背景・発生している実害

親プロジェクトのマップ＆シナリオエディタで、以下のフローが **`System.IO.IOException` で必ず失敗**する:

1. 既存シナリオを読み込み → `RouterDb.LoadFromOdrg("{id}.odrg")` が当該ファイルを **MemoryMappedFile（読み取り専用、`OdrgMmfHandle.Open`）でマップしたまま保持**
2. 道路ネットワーク再生成 → 新しい一時 odrg を生成し、親側は新しい `RouterDb` インスタンスへ差し替え。**旧インスタンスは参照を捨てるしかなく、MMF ハンドルは GC 任せ**で `{id}.odrg` のロックが残留
3. シナリオ上書き保存 → `File.Copy(一時ファイル → {id}.odrg, overwrite: true)` が手順2の残留ロックにより **IOException**

同様に、生成直後の統計取得用 `RouterDb.LoadFromOdrg(outputPath)`（親側のローカル変数）も解放手段がなく、一時ファイルの削除が静かに失敗してゴミファイルが残留する。

### 利用側で解決できない理由

- `RouterDb` は `IDisposable` を実装しておらず、リソースを保持する `Graph`（`IRoadGraph : IDisposable`）も **internal** のため、親アセンブリから確定的に解放する手段が存在しない。
- `GC.Collect()` + `WaitForPendingFinalizers()` による強制ファイナライズは可能だが、非確定的なワークアラウンドであり恒久対応にしたくない。

---

## 2. 要望内容（API 契約）

`OsmDotRoute.RouterDb`（`src/OsmDotRoute/RouterDb.cs`）に `IDisposable` を実装してほしい。

### 提案シグネチャ

```csharp
public sealed class RouterDb : IDisposable
{
    /// <summary>
    /// グラフが保持するリソース（ファイル版: MMF ファイルハンドル / メモリ版: ピン留めバッファ）を解放する。
    /// ファイル版 LoadFromOdrg は .odrg を MemoryMappedFile で開いたまま保持するため、
    /// Dispose するまで当該ファイルの上書き・削除はできない（シナリオ保存時のリネーム等で必要）。
    /// 多重呼び出しは安全（冪等）。Dispose 後の本インスタンスおよび本インスタンスから生成した
    /// Router / Snapper の使用は不可（ObjectDisposedException）。
    /// </summary>
    public void Dispose() => _graph.Dispose();
}
```

### 満たすべき不変条件（受け入れ基準）

1. **ファイルロック解放**: ファイル版 `LoadFromOdrg(string)` で生成した `RouterDb` を `Dispose()` した後、当該 `.odrg` に対する `File.Delete` / `File.Copy(..., overwrite: true)` / `File.Move` が成功すること（Windows で確認）
2. **冪等性**: `Dispose()` の多重呼び出しが安全であること（`NativeRoadGraph.Dispose` は既に `_disposed` ガード済みの認識）
3. **Dispose 後の使用**: 本インスタンス・派生 `Router` / スナップ機能の使用は `ObjectDisposedException`（既存の `ThrowIfDisposed` の挙動でよい。新たな安全装置の追加は不要）
4. **メモリ版**: `LoadFromOdrg(ReadOnlyMemory<byte>)`（WASM 向けピン留めバッファ版）でも矛盾なく解放されること
5. **非破壊**: Dispose を呼ばない既存利用コードの挙動が一切変わらないこと

---

## 3. 実装ヒント（親側調査での所見）

> 実装方針はそちらの裁量。以下は親側調査で確認した内部構造。

- `IRoadGraph` は既に `IDisposable` を継承（`src/OsmDotRoute/Routing/IRoadGraph.cs:14`、Phase 3 ステップ 3A.3e）。
- `NativeRoadGraph.Dispose()`（`src/OsmDotRoute/Native/NativeRoadGraph.cs:274`）は実装済み（`_disposed` ガード + `_mmf.Dispose()`）。`OdrgMmfHandle.Dispose` が MMF/ViewAccessor を解放する。
- したがって `RouterDb.Dispose() => _graph.Dispose();` の委譲のみで成立する見込み。`NativeRoadSnapper` はグラフ参照のみで固有リソースを持たない認識（要確認）。
- REQ-API-003（公開 API に内部実装型を露出させない）には抵触しない（`IDisposable` は BCL 型）。

---

## 4. 親プロジェクト側の利用計画（参考・本要望のスコープ外）

ライブラリ反映後、親側で以下を実施する:

1. `MapService.ClearRouterDb()` / `LoadRouterDbFromFile()` で旧 `RouterDb` を差し替え前に `Dispose()`（残留ロックの根絶）
2. 道路データ生成直後の統計取得を `using var routerDb = RouterDb.LoadFromOdrg(...)` 化
3. シナリオ保存時のリネーム処理を「MapService 解放 → コピー → 一時ファイル削除 → 新パスを再ロード」に変更

---

## 5. 要件 ID・記法の提案

OsmDotRoute 要件定義書の記法に合わせ、以下いずれかでの新規採番を提案（採番・確定はそちらの管理）:

- **REQ-MAP ジャンル**（`LoadFromOdrg` = REQ-MAP-009 の系列）: 「`RouterDb` が保持するリソース（MMF ハンドル / ピン留めバッファ）を利用側から確定的に解放できること（`IDisposable`）」
- もしくは **REQ-API ジャンル**相当
