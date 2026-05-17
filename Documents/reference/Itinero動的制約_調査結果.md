# Itinero 1.x 動的制約機能 ソースコード調査結果

**作成日**: 2026-05-18
**調査対象**: Itinero develop ブランチ（`d:/workspace/Itinero_source_reference/`、commit `b8a004a8`）
**目的**: RouterDB を再ビルドせずに進入不可・移動困難領域を設定するための拡張ポイントを特定する

---

## サマリー

Itinero 1.x には **「特定エッジを動的に無効化する直接的なフック」は存在しない**ことが判明した。これは設計上の制約で、重み計算は OSM タグのプロファイル（ushort）と距離のみで行われ、エッジ ID や座標情報は重み計算関数に届かない構造になっている。

ただし、3つの間接的なアプローチで実現可能。**推奨は Approach A（独自 Dykstra 実装）**。

---

## 1. 拡張不可だった箇所

### 1.1 `Profile.FactorAndSpeed(IAttributeCollection attributes)`
[`src/Itinero/Profiles/Profile.cs:164`](file:///d:/workspace/Itinero_source_reference/src/Itinero/Profiles/Profile.cs)

```csharp
public virtual FactorAndSpeed FactorAndSpeed(IAttributeCollection attributes)
```

- `virtual` で**オーバーライド可能**だが、引数は OSM タグ集合のみ
- エッジ ID も座標も渡されないため、「このエッジが特定ポリゴン内か」を判定できない
- → **空間制約には使えない**

### 1.2 `DefaultWeightHandler.Calculate(ushort edgeProfile, float distance, out Factor factor)`
[`src/Itinero/Algorithms/Weights/DefaultWeightHandler.cs:88`](file:///d:/workspace/Itinero_source_reference/src/Itinero/Algorithms/Weights/DefaultWeightHandler.cs)

```csharp
public sealed override float Calculate(ushort edgeProfile, float distance, out Factor factor)
```

- `sealed` で**オーバーライド不可**
- `edgeProfile` は OSM タグ集合に対応するインデックス（ushort）であり、**個別エッジを識別できない**
- 同一タグを持つ全エッジに同じ `edgeProfile` が割り当てられる
- → **WeightHandler 経由の空間制約は不可能**

### 1.3 `Itinero.Algorithms.Default.Dykstra<T>`
[`src/Itinero/Algorithms/Default/Dykstra.cs`](file:///d:/workspace/Itinero_source_reference/src/Itinero/Algorithms/Default/Dykstra.cs)

- クラス自体は `public`（`sealed` ではない）→ 継承可能
- しかし主要フィールド（`_edgeEnumerator`、`_heap`、`_visits` 等）が `private` でカプセル化されており、サブクラス側からエッジ探索ループに介入することが困難
- 主要メソッド `Step()`、`DoRun()` は private/protected で公開度が低い

### 1.4 `Router.TryCalculateRaw`
[`src/Itinero/Router.cs:294`](file:///d:/workspace/Itinero_source_reference/src/Itinero/Router.cs)

- `sealed override` で**オーバーライド不可**
- Dykstra のインスタンス化が内部に埋め込まれているため、別アルゴリズムを差し込む余地がない

---

## 2. 拡張可能だった箇所

### 2.1 EdgeBased Dykstra の `_getRestriction` コールバック
[`src/Itinero/Algorithms/Default/EdgeBased/Dykstra.cs:196`](file:///d:/workspace/Itinero_source_reference/src/Itinero/Algorithms/Default/EdgeBased/Dykstra.cs)

```csharp
public Dykstra(Graph graph, WeightHandler<T> weightHandler, 
    Func<uint, IEnumerable<uint[]>> getRestriction, ...)
```

- 頂点 ID を受け取り、その頂点に紐付く制約列を返すコールバック
- `restriction.Length == 1` の場合「単純制約」として扱われ、その頂点を経由する経路探索が打ち切られる（`Step()` 内 L206-209）
- → **頂点単位での空間ブロックは可能**

**ただし条件あり**: このアルゴリズムは `RouterDb.HasComplexRestrictions(profile) == true` の場合のみ使用される（[`Router.cs:449`](file:///d:/workspace/Itinero_source_reference/src/Itinero/Router.cs)）。本プロジェクトの OSM データには複雑制限が含まれていないため、デフォルトでは標準 Dykstra が選ばれて当該フックは効かない。

### 2.2 公開 Graph API
- `RouterDb.Network` で `Graph` にアクセス可能
- `Graph.GetEdgeEnumerator(uint vertex)` でエッジ列挙
- `enumerator.Shape` でエッジ中間座標取得
- `network.GetVertex(uint)` で頂点座標取得
- これらを使えば**完全に独自のパス探索アルゴリズムを実装可能**

### 2.3 `IGetSimpleRestrictions` （現状非該当）
- 標準 Dykstra でも `Func<uint, uint>` 型のシンプル制約は受け取る（[`Dykstra.cs:47`](file:///d:/workspace/Itinero_source_reference/src/Itinero/Algorithms/Default/Dykstra.cs)）
- しかし**ソースの初期キューイング時のみ参照される**（L90-97）。探索ループ中には参照されない
- → ブロック領域内に出発・到着点がある場合の排除には使えるが、領域を通過する経路の排除には使えない

---

## 3. 実装アプローチ案

### Approach A: 独自 Dykstra 実装（推奨）

**概要**: Itinero の公開 Graph API のみを使い、空間制約対応の独自パス探索を実装する

**実装イメージ**:
```csharp
// DisasterWasteSim.Server.Routing.Experimental.RestrictedRouter
public class RestrictedRouter
{
    private readonly RouterDb _routerDb;
    private readonly RestrictedAreaService _restrictions;

    public Route? Calculate(Profile profile, float startLat, float startLon, ...)
    {
        // 1. Router.Resolve() で RouterPoint 取得（既存 API 利用可）
        // 2. 独自 Dijkstra 実行：
        //    - 各エッジで RestrictedAreaService.IsBlocked(shapePoints) を確認
        //    - ブロック時は無視、減速時は weight に倍率適用
        // 3. Itinero.Route を手動構築（既存 Route 出力フォーマットに準拠）
    }
}
```

**メリット**:
- Itinero 内部の制約に縛られず最も自由度が高い
- CH 非対応問題が一切発生しない（自分で書くため）
- ポリゴン判定は座標列で直接行えるため精度が高い
- 空間インデックス（R-tree など）を自由に統合できる

**デメリット**:
- Dijkstra を再実装する必要がある（ただし基本実装は数百行レベル）
- Itinero の `Route` 出力フォーマットを正しく再現する必要がある（リファレンス実装あり）
- One-hop ルーター・複雑な turn restrictions など Itinero の機能を失う

**工数感**: 1.5〜2 日

---

### Approach B: ダミー complex restriction + EdgeBased Dykstra 経路の強制利用

**概要**: RouterDb に 1つだけダミーの複雑制限を登録して `HasComplexRestrictions=true` にし、EdgeBased Dykstra 経路に強制的に切り替える。その上で `_getRestriction` コールバックを通じて空間ブロックを実現。

**メリット**:
- Itinero の最適化を流用できる
- 実装コード量が少ない

**デメリット**:
- 「ダミー制限を入れる」という非定型的な使い方で**Itinero のバージョンアップで動かなくなるリスク大**
- `_getRestriction` は頂点単位のため、エッジの一部だけがブロック領域に入っている場合の挙動が不明確
- Router.cs L451-454 を直接呼ぶには `Router` の内部 API へのアクセスが必要で、リフレクション等の汚いコードになる可能性

**工数感**: 1〜2 日（ただし不確実性が高い）

---

### Approach C: 動的エッジ属性注入

**概要**: RouterDb 読込後に、ブロック領域内のエッジに対して「blocked=true」のような独自属性を持つ新しい edge profile を割り当てる。カスタム Profile がこの属性を検出すると Factor.NoFactor を返す。

**メリット**:
- 既存の Itinero パイプラインに乗れる

**デメリット**:
- Edge profile の動的な追加・更新 API が公開されていない可能性が高い
- ブロック領域変更のたびに RouterDb 内部状態を書き換える必要がある（実質的に「RouterDb 改変なし」要件に反する）
- 実装複雑度が高い

**工数感**: 不確定（3 日以上）

---

## 4. 結論と次ステップの提案

### 推奨: Approach A（独自 Dykstra 実装）

理由：
1. **PoC として最も結果が読みやすい**：自分で書くため不確実性が低い
2. **Itinero の内部仕様変更の影響を受けない**：公開 API しか使わない
3. **空間制約のロジックがクリーンに分離できる**：将来の機能追加が容易
4. **CH 非対応問題が原理的に発生しない**

### 当初計画からの調整提案

[Documents/Itinero動的制約_PoC計画.md](Itinero動的制約_PoC計画.md) の Step 3 を以下に変更：

**変更前**: 「カスタム Profile / WeightHandler の実装」
**変更後**: 「独自 Dykstra ベースの `RestrictedRouter` 実装」

Step 1〜2 と Step 4〜5 は計画通り。

### 確認事項
- Approach A で進めて良いか
- 工数感（Step 1〜5 合計で **3〜4 日程度**）が許容範囲か
- 独自 Dykstra 実装で諦める Itinero 機能（複雑 turn restrictions、one-hop ルーター最適化、CH）について本プロジェクトでは不要と判断して問題ないか
  - 本プロジェクトは収集車・住民エージェントの移動シミュレーションが目的で、turn restriction や CH は使っていないため、**支障なし**と判断
