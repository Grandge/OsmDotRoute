# 不具合報告: 移動困難エリアの speedFactor が「エリアごと」ではなく「shape ごと」に累乗される

**報告元**: 親プロジェクト「災害廃棄物処理シミュレーション」開発（Claude Code）
**作成日**: 2026-08-16
**宛先**: OsmDotRoute 開発エージェント
**該当**: `RestrictedAreaService.BakeIntoCache` / `RestrictedAreaEdgeCache.AddDifficulty`
**関連要件**: REQ-RST-013〜015 / REQ-RST-030〜032（結合 speedFactor の定義）
**優先度提案**: **P1** — 複数 shape を持つ移動困難エリアが**事実上の通行不能**になる。回避策はあるが不自然な形状を強いられる
**互換性**: 1行の重複チェック追加のみ。**既に正しく動いている単一 shape のエリアには一切影響しない**

---

## 1. 現象

**複数の shape を持つ移動困難エリアを1つ登録すると、エッジが跨いだ shape の数だけ
`speedFactor` が掛け合わされる。**

親プロジェクトの実測値（`truck` プロファイル、`obstacle` = `speedFactor 0.4`、同一区間・同一経路）:

| エリアの与え方 | shape 数 | 期待 | 実測 |
|---|---:|---:|---:|
| ポリゴン1枚（帯状） | 1 | ×1.2〜2.5 | **×1.20** ✅ |
| ポリゴン70枚（道路に沿った正方形の連なり） | 70 | 同上 | **×604**（≒ 0.4^7） ❌ |
| `AddDifficultyArea(IEnumerable<MeshCode>)` 1回・1/8メッシュ336個 | 336 | 同上 | **×315,135**（≒ 0.4^14） ❌ |

3行目が特に問題で、**API 呼び出しは1回・`RestrictedAreaId` も1個**なのに係数が14回掛かっている。
`construction`（0.4 ではなく 0.2）では ×543,428 まで悪化した。

いずれも**経路そのものは変わらない**（距離が完全に一致）。所要時間だけが爆発するので、
「速度低下」のつもりの設定が実質 `canPass=false` と同じ挙動になる。

---

## 2. 原因

### 2-1. bake 側が shape ごとに `AddDifficulty` を呼ぶ

`src/OsmDotRoute/Restrictions/RestrictedAreaService.cs`

```csharp
private void BakeIntoCache(RestrictedAreaId id, AreaEntry entry)
{
    foreach (var shape in entry.Shapes)                       // ← エリア1個でも shape は N 個
    {
        foreach (var edgeId in _graph!.QueryEdgesByAabb(shape.Bounds))
        {
            if (!EdgeIntersectsShape(_graph, edgeId, shape)) continue;
            ...
            else if (entry.Area is DifficultyArea diff)
            {
                _cache!.AddDifficulty(id, diff, edgeId);      // ← 同じ (id, edgeId) が N 回来る
            }
        }
    }
}
```

### 2-2. キャッシュ側が重複を弾かない

`src/OsmDotRoute/Restrictions/RestrictedAreaEdgeCache.cs`

```csharp
public void AddDifficulty(RestrictedAreaId areaId, DifficultyArea area, uint edgeId)
{
    ArgumentNullException.ThrowIfNull(area);
    if (!_difficultyByArea.TryGetValue(areaId, out var set)) { ... }
    set.Add(edgeId);                       // ← HashSet なのでここは重複しない

    if (!_difficultyAreasByEdge.TryGetValue(edgeId, out var list)) { ... }
    list.Add(area);                        // ★ List なので同じ area が N 個入る
}
```

`_difficultyByArea` は `HashSet<uint>` なので重複しないが、
**`_difficultyAreasByEdge` は `List<DifficultyArea>`** で無条件 `Add` している。

### 2-3. ホットパスがリストを素直に掛け合わせる

`src/OsmDotRoute/Routing/EdgeWeightCalculator.cs`

```csharp
var areas = cache.GetDifficultyAreas(edgeId);
double combined = 1.0;
foreach (var area in areas)          // ← 同じ area が N 個入っているので N 回掛かる
{
    var ev = _evaluator.EvaluateDifficulty(area.DifficultyType);
    if (!ev.CanPass) return double.PositiveInfinity;
    combined *= ev.SpeedFactor;
    ...
}
```

---

## 3. 契約違反であることの根拠（自己矛盾）

**graph 未注入時のフォールバック経路は、同じ状況で正しく1回しか掛けない。**

`RestrictedAreaService.EvaluateConstraints`:

```csharp
/// <returns>
/// 結合 speedFactor（全該当 <see cref="DifficultyArea"/> の <c>speedFactor</c> の積）。
/// </returns>
...
foreach (var sr in _index.Query(edgeAabb))
{
    if (!seenIds.Add(sr.Id)) continue;   // ★ ID 単位で重複排除している
    ...
    combined *= ev.SpeedFactor;
}
```

* XML ドキュメントは「**全該当 DifficultyArea の** speedFactor の積」＝**エリア単位**と明記している。
* `EvaluateConstraints` は `seenIds` で ID を重複排除し、コメントも
  「ID 単位の厳密判定: 当該 ID の全 Shape を見て、いずれかと交差すれば『ヒット』」と書いてある。

したがって **bake 経路（graph 注入時）だけが仕様と食い違っている**。
同じ入力で2つの経路が異なる結果を返すこと自体が不具合の証拠と考える。

---

## 4. 提案する修正

`RestrictedAreaEdgeCache.AddDifficulty` で、`_difficultyByArea` の `HashSet.Add` の戻り値を
そのまま重複判定に使うのが最小変更で済む。

```csharp
public void AddDifficulty(RestrictedAreaId areaId, DifficultyArea area, uint edgeId)
{
    ArgumentNullException.ThrowIfNull(area);
    if (!_difficultyByArea.TryGetValue(areaId, out var set))
    {
        set = new HashSet<uint>();
        _difficultyByArea[areaId] = set;
    }
    // ★ 同一エリアの別 shape で既に登録済みのエッジは二重に積まない。
    //   speedFactor は「エリア単位で1回」効く（EvaluateConstraints と同セマンティクス）。
    if (!set.Add(edgeId)) return;

    if (!_difficultyAreasByEdge.TryGetValue(edgeId, out var list))
    {
        list = new List<DifficultyArea>();
        _difficultyAreasByEdge[edgeId] = list;
    }
    list.Add(area);
}
```

* `RemoveArea` は `list.RemoveAll(a => a.Id.Equals(areaId))` なので、**修正後もそのままで整合**する
  （重複が無くなるだけ）。
* **異なるエリアが重なった場合の積は従来どおり**維持される（冠水×積雪などの重ね合わせは仕様どおり）。
* 単一 shape のエリア（ポリゴン1枚・単一メッシュ）は挙動が変わらない。

### 提案する回帰テスト

1. `AddDifficultyArea(meshCodes)` に**1本のエッジを丸ごと覆う複数メッシュ**を渡し、
   結合係数が `speedFactor^1` になること（現状は `^N`）。
2. 同じ入力に対し、**graph 注入あり（bake 経路）となし（`EvaluateConstraints` 経路）で
   結合係数が一致**すること。← 今回の食い違いを直接押さえる
3. **異なる**2エリア（例: `flooding` と `snow`）が同一エッジに掛かるときは積になること（非回帰）。
4. `RemoveArea` 後に係数が 1.0 に戻ること（重複除去で消し漏れが起きないこと）。

---

## 5. 親プロジェクト側の暫定回避策（修正後に戻したい）

移動困難エリアを **「帯状のポリゴン1枚」** で与えている（`shape` を1個に抑える）。
本来は道路中心線に沿った 1/8メッシュ（125m）集合で与えたい——そのほうが道路に密着し、
並走する別の道を巻き込まないため。**修正後はメッシュ集合へ戻す予定。**

該当箇所: `Documents/廃棄物量推定/extract_能登道路区間.py` の `ribbon()`、
`Documents/廃棄物量推定/gen_道路啓開phases.py` の形状分岐。
