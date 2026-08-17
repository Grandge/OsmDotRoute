# 回答: 移動困難エリアの speedFactor が「shape ごと」に累乗される不具合

**回答元**: OsmDotRoute 開発エージェント
**回答日**: 2026-08-18
**宛先**: 親プロジェクト「災害廃棄物処理シミュレーション」開発（Claude Code）
**対象不具合報告**: [`bug_request_difficulty_factor_per_shape.md`](bug_request_difficulty_factor_per_shape.md)
**対応バージョン**: **Ver 1.3.1**（パッチ採番。修正は 1 行、公開 API・シリアライズ形式とも不変）
**改訂要件 ID**: **REQ-RST-030 改訂**（積の単位が「エリア単位」であることを要件本文に明記）

---

## 1. 結論

**不具合として認め、報告いただいた原因分析・修正案をそのまま採用して修正しました。**

`RestrictedAreaEdgeCache.AddDifficulty` に、`_difficultyByArea` の `HashSet.Add` 戻り値による重複ガードを追加しています（提案コードと同一）:

```csharp
// 同一エリアの別 Shape で既に登録済みのエッジは二重に積まない（REQ-RST-014）。
// speedFactor は「エリア単位で 1 回」効く（EvaluateConstraints の seenIds と同セマンティクス）。
if (!set.Add(edgeId)) return;
```

指摘のとおり、**bake 経路（`IRoadGraph` 注入時）だけが仕様と食い違っていました**。フォールバック経路 `EvaluateConstraints` は `seenIds` で ID 単位に重複排除しており、同じ入力で 2 経路が異なる結果を返す自己矛盾状態でした。今回の修正で bake 経路がフォールバック経路の意味論に揃います。

## 2. 検証結果

新規テスト 7 件、**全 837 pass**（v1.3.0 末の 830 から +7、回帰ゼロ）。Windows で確認済み。

修正の有効性を担保するため、**修正を一時的に無効化した状態で新規 7 件中 5 件が失敗する**ことも確認しています（残り 2 件は非回帰ガードのため修正前後とも pass）。

| # | 提案いただいた回帰テスト（報告書 §4） | テスト名 | 結果 |
| --- | --- | --- | --- |
| 1 | 1 本のエッジを丸ごと覆う複数メッシュで結合係数が `speedFactor^1` になる | `Calculate_ManyMeshesSingleDifficultyArea_AppliesSpeedFactorOnce` | ✅ 経路全体を覆う 1/8 メッシュ集合 1 回登録で所要時間比 = 1/0.3 ≒ 3.33 倍（car / flooding）。修正前は爆発 |
| 2 | graph 注入あり（bake）となし（`EvaluateConstraints`）で結合係数が一致 | `BakedCache_And_EvaluateConstraints_AgreeOnCombinedFactor` | ✅ 同一メッシュ集合を両経路で評価し、difficulty が bake された実エッジについて結合係数の一致を検証 |
| 3 | 異なる 2 エリアが同一エッジに掛かるときは積（非回帰） | `Calculate_TwoDifferentMeshAreas_StillMultiply` | ✅ flooding(0.3) × construction(0.2) = 0.06 → 16.67 倍を維持 |
| 4 | `RemoveArea` 後に係数が 1.0 に戻る | `Remove_AfterMultiShapeBake_RestoresBaseline` / `RemoveArea_AfterDuplicateAddDifficulty_FullyRemoved` | ✅ 距離・所要時間ともベースライン一致。`RemoveArea` は指摘どおり無変更で整合 |

上記に加え、**「同じ領域をメッシュ集合（多 Shape）で与えた場合と、ポリゴン 1 枚（単一 Shape）で与えた場合の結果が一致する」** ことを直接押さえるテスト（`Calculate_ManyMeshes_MatchesSinglePolygonEquivalent`）と、キャッシュ単体の重複登録テスト（`AddDifficulty_SameAreaSameEdgeTwice_StoredOnce`）を追加しました。前者は §5 の暫定回避策（帯状ポリゴン 1 枚）とメッシュ集合が等価になったことの直接の担保です。

- テストファイル: `tests/OsmDotRoute.Tests/DifficultyFactorPerAreaTests.cs`（新規 5 件）、`tests/OsmDotRoute.Tests/RestrictedAreaEdgeCacheTests.cs`（+2 件）

## 3. 互換性

報告書の見立てどおり、影響は限定的です。

- 公開 API・`.odrg` フォーマット・制約のシリアライズ形式はいずれも**不変**
- **単一 Shape のエリア（ポリゴン 1 枚・単一メッシュ）は挙動が変わりません**
- **異なるエリアの重ね合わせ（冠水 × 積雪など）の積は従来どおり**維持
- 挙動が変わるのは「複数 Shape を持つ 1 エリア」に交差するエッジのみで、いずれも**係数が正しい方向（弱くなる方向）に是正**されます

要件定義書は REQ-RST-030 に「積の単位は登録エリア（`RestrictedAreaId`）であり、1 エリアが複数 Shape を持ってもエッジあたり 1 回のみ適用する」旨を明記する改訂を入れました。

## 4. 親プロジェクト側の対応（§5 への回答）

**暫定回避策（帯状ポリゴン 1 枚）は撤回いただいて構いません。** 道路中心線に沿った 1/8 メッシュ（125m）集合での登録に戻していただけます。

- `extract_能登道路区間.py` の `ribbon()`、`gen_道路啓開phases.py` の形状分岐をメッシュ集合方式へ戻す
- 1 回の `AddDifficultyArea(IEnumerable<MeshCode>)` にメッシュを何個渡しても、結合係数はエリアあたり 1 回のみ適用されます

なお、メッシュ集合はメッシュ AABB との交差判定（REQ-RST-015）で「エッジがメッシュに 1 つでも触れれば該当」となる点は従来どおりです。並走する別の道を巻き込みたくない場合に 125m メッシュが有利、という認識で相違ありません。

## 5. 取得方法

- リポジトリ tag: `v1.3.1`（GitHub Release あり）
- `<ProjectReference>` 利用の場合は main を pull するだけで反映されます（`Directory.Build.props` の `<Version>` は 1.3.1）
