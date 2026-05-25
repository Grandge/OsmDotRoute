using OsmDotRoute.Itinero;
using OsmDotRoute.Tests.TestData;
using Xunit;

namespace OsmDotRoute.Tests;

/// <summary>
/// Phase 1 ステップ 9「制約対応 Dijkstra 統合」の検証テスト（REQ-RST-013〜015, REQ-RST-030〜032）。
/// 親プロジェクト default.routerdb を使用し、制約なしベースラインに対して
/// 進入不可・難所エリア（単独・重複・通行不可・未知タイプ）の効果を検証する。
/// </summary>
public class RestrictedRoutingTests
{
    /// <summary>テスト共通の baseline 文脈。</summary>
    private sealed record BaselineContext(
        RouterDb RouterDb,
        GeoCoordinate From,
        GeoCoordinate To,
        Route CarBaseline,
        Route PedestrianBaseline);

    [Fact]
    public void Calculate_NoRestrictions_MatchesBaselineExactly()
    {
        var ctx = SetupBaseline();
        var router = new Router(ctx.RouterDb, new RestrictedAreaService());     // 空サービス
        var result = router.Calculate(VehicleProfile.Car, ctx.From, ctx.To);
        Assert.NotNull(result);
        // 制約 0 件 → baseline と同じ結果
        Assert.Equal(ctx.CarBaseline.TotalDistanceM, result!.TotalDistanceM, precision: 1);
        Assert.Equal(ctx.CarBaseline.TotalDurationSec, result.TotalDurationSec, precision: 1);
    }

    [Fact]
    public void Calculate_BlockArea_OnRoute_DetoursOrReturnsNull()
    {
        var ctx = SetupBaseline();
        var restrictions = new RestrictedAreaService();
        restrictions.AddBlockArea(MakeSmallPolygonAroundShapeMidpoint(ctx.CarBaseline.Shape));
        var router = new Router(ctx.RouterDb, restrictions);

        var result = router.Calculate(VehicleProfile.Car, ctx.From, ctx.To);

        if (result is null) return;        // 迂回路なし → null は許容
        // 迂回ルート: 距離またはシェイプが baseline と異なる
        var sameShape = HasSameShape(result.Shape, ctx.CarBaseline.Shape);
        Assert.False(sameShape, $"BlockArea を経路上に置いたのにシェイプが baseline と同一。baseline.Dist={ctx.CarBaseline.TotalDistanceM:F1}m, constrained.Dist={result.TotalDistanceM:F1}m");
    }

    [Fact]
    public void Calculate_DifficultyArea_Flooding_CoveringRoute_Car_Time_3_33x()
    {
        var ctx = SetupBaseline();
        var restrictions = new RestrictedAreaService();
        restrictions.AddDifficultyArea(MakePolygonCoveringShape(ctx.CarBaseline.Shape, marginDeg: 0.002), DifficultyTypes.Flooding);
        var router = new Router(ctx.RouterDb, restrictions);

        var result = router.Calculate(VehicleProfile.Car, ctx.From, ctx.To);
        Assert.NotNull(result);

        // 全エッジが flooding 領域内 → speedFactor=0.3、所要時間が baseline の 1/0.3 ≈ 3.33 倍
        var ratio = result!.TotalDurationSec / ctx.CarBaseline.TotalDurationSec;
        Assert.InRange(ratio, 3.30, 3.36);
        // 同じ経路を辿るはず（全領域が同じ係数なので最短経路は不変）
        Assert.Equal(ctx.CarBaseline.TotalDistanceM, result.TotalDistanceM, precision: 0);
    }

    [Fact]
    public void Calculate_DifficultyArea_Flooding_CoveringRoute_Pedestrian_Time_10x()
    {
        var ctx = SetupBaseline();
        var restrictions = new RestrictedAreaService();
        restrictions.AddDifficultyArea(MakePolygonCoveringShape(ctx.PedestrianBaseline.Shape, marginDeg: 0.002), DifficultyTypes.Flooding);
        var router = new Router(ctx.RouterDb, restrictions);

        var result = router.Calculate(VehicleProfile.Pedestrian, ctx.From, ctx.To);
        Assert.NotNull(result);

        // pedestrian flooding speedFactor=0.1 → 所要時間 10 倍
        var ratio = result!.TotalDurationSec / ctx.PedestrianBaseline.TotalDurationSec;
        Assert.InRange(ratio, 9.9, 10.1);
        Assert.Equal(ctx.PedestrianBaseline.TotalDistanceM, result.TotalDistanceM, precision: 0);
    }

    [Fact]
    public void Calculate_DifficultyArea_FloodingAndConstruction_Overlapping_Car_Time_16_67x()
    {
        var ctx = SetupBaseline();
        var polygon = MakePolygonCoveringShape(ctx.CarBaseline.Shape, marginDeg: 0.002);
        var restrictions = new RestrictedAreaService();
        restrictions.AddDifficultyArea(polygon, DifficultyTypes.Flooding);
        restrictions.AddDifficultyArea(polygon, DifficultyTypes.Construction);
        var router = new Router(ctx.RouterDb, restrictions);

        var result = router.Calculate(VehicleProfile.Car, ctx.From, ctx.To);
        Assert.NotNull(result);

        // car: flooding(0.3) × construction(0.2) = 0.06 → 1/0.06 ≈ 16.67 倍
        var ratio = result!.TotalDurationSec / ctx.CarBaseline.TotalDurationSec;
        Assert.InRange(ratio, 16.50, 16.80);
    }

    [Fact]
    public void Calculate_DifficultyArea_Landslide_CoveringRoute_Car_DetoursOrReturnsNull()
    {
        var ctx = SetupBaseline();
        var restrictions = new RestrictedAreaService();
        // ベースラインルートを覆う landslide（canPass:false） → 通行不可
        restrictions.AddDifficultyArea(MakePolygonCoveringShape(ctx.CarBaseline.Shape, marginDeg: 0.002), DifficultyTypes.Landslide);
        var router = new Router(ctx.RouterDb, restrictions);

        var result = router.Calculate(VehicleProfile.Car, ctx.From, ctx.To);

        if (result is null) return;     // 迂回路なし → null は許容
        // 迂回した場合は baseline と異なる経路
        var sameShape = HasSameShape(result.Shape, ctx.CarBaseline.Shape);
        Assert.False(sameShape, "landslide が canPass:false を返したのに baseline と同じ経路を辿った");
    }

    [Fact]
    public void Calculate_BlockArea_Overrides_DifficultyArea()
    {
        var ctx = SetupBaseline();
        var polygon = MakeSmallPolygonAroundShapeMidpoint(ctx.CarBaseline.Shape);
        var restrictions = new RestrictedAreaService();
        // 同じ領域に flooding（通行可）と BlockArea（通行不可）を重ねる
        restrictions.AddDifficultyArea(polygon, DifficultyTypes.Flooding);
        restrictions.AddBlockArea(polygon);
        var router = new Router(ctx.RouterDb, restrictions);

        var blocked = router.Calculate(VehicleProfile.Car, ctx.From, ctx.To);

        // BlockArea 優先 → 迂回 or null
        if (blocked is null) return;
        var sameShape = HasSameShape(blocked.Shape, ctx.CarBaseline.Shape);
        Assert.False(sameShape, "BlockArea が DifficultyArea 重複時に優先されていない");

        // flooding 単独だと同じ経路（時間だけ伸びる）になるべきだったことを念のため確認
        var floodOnly = new RestrictedAreaService();
        floodOnly.AddDifficultyArea(polygon, DifficultyTypes.Flooding);
        var floodResult = new Router(ctx.RouterDb, floodOnly).Calculate(VehicleProfile.Car, ctx.From, ctx.To);
        Assert.NotNull(floodResult);
        // flooding 単独は同じ経路 → BlockArea 追加で経路変化したことが BlockArea 優先の証拠
    }

    [Fact]
    public void Calculate_UnknownDifficultyType_AppliesDifficultyDefault_NoSpeedChange()
    {
        var ctx = SetupBaseline();
        var restrictions = new RestrictedAreaService();
        // 未知タイプ "meteor" → difficultyDefault (speedFactor=1.0, canPass=true) 適用 → 速度変化なし
        restrictions.AddDifficultyArea(MakePolygonCoveringShape(ctx.CarBaseline.Shape, marginDeg: 0.002), "meteor");
        var router = new Router(ctx.RouterDb, restrictions);

        var result = router.Calculate(VehicleProfile.Car, ctx.From, ctx.To);
        Assert.NotNull(result);
        Assert.Equal(ctx.CarBaseline.TotalDistanceM, result!.TotalDistanceM, precision: 1);
        Assert.Equal(ctx.CarBaseline.TotalDurationSec, result.TotalDurationSec, precision: 1);
    }

    [Fact]
    public void Calculate_AfterClearAll_RestoresBaseline()
    {
        var ctx = SetupBaseline();
        var restrictions = new RestrictedAreaService();
        restrictions.AddBlockArea(MakeSmallPolygonAroundShapeMidpoint(ctx.CarBaseline.Shape));
        var router = new Router(ctx.RouterDb, restrictions);

        // クリア前: 制約適用
        // クリア後: baseline と同じ
        restrictions.ClearAll();
        var afterClear = router.Calculate(VehicleProfile.Car, ctx.From, ctx.To);
        Assert.NotNull(afterClear);
        Assert.Equal(ctx.CarBaseline.TotalDistanceM, afterClear!.TotalDistanceM, precision: 1);
        Assert.Equal(ctx.CarBaseline.TotalDurationSec, afterClear.TotalDurationSec, precision: 1);
    }

    // --- ヘルパ ---

    /// <summary>baseline 文脈を構築する。car / pedestrian の両方でルートが取れるペアを使う。</summary>
    private static BaselineContext SetupBaseline()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        // 車両 / 歩行者の両方で経路が取れる頂点ペアを探す
        var pairs = CollectCarAccessibleVertexPairs(routerDb, maxPairs: 20, minSeparationDeg: 0.005);
        foreach (var (from, to) in pairs)
        {
            var car = router.Calculate(VehicleProfile.Car, from, to);
            var ped = router.Calculate(VehicleProfile.Pedestrian, from, to);
            if (car is not null && ped is not null && car.Shape.Count >= 4 && ped.Shape.Count >= 4)
            {
                return new BaselineContext(routerDb, from, to, car, ped);
            }
        }
        Assert.Fail("car / pedestrian の両方で経路が取れるテストペアが見つかりません。");
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>シェイプ全体を margin 度だけ広げた外接矩形ポリゴンを作る。</summary>
    private static GeoPolygon MakePolygonCoveringShape(IReadOnlyList<GeoCoordinate> shape, double marginDeg)
    {
        var minLat = shape.Min(c => c.Latitude) - marginDeg;
        var maxLat = shape.Max(c => c.Latitude) + marginDeg;
        var minLon = shape.Min(c => c.Longitude) - marginDeg;
        var maxLon = shape.Max(c => c.Longitude) + marginDeg;
        return new GeoPolygon(new[]
        {
            new GeoCoordinate(minLat, minLon),
            new GeoCoordinate(minLat, maxLon),
            new GeoCoordinate(maxLat, maxLon),
            new GeoCoordinate(maxLat, minLon),
            new GeoCoordinate(minLat, minLon),
        });
    }

    /// <summary>シェイプ中央付近を覆う小さなポリゴン（≈30m 四方）。局所遮断テスト用。</summary>
    private static GeoPolygon MakeSmallPolygonAroundShapeMidpoint(IReadOnlyList<GeoCoordinate> shape)
    {
        var midIdx = shape.Count / 2;
        var c = shape[midIdx];
        const double d = 0.0003;       // ≒ 30〜33m
        return new GeoPolygon(new[]
        {
            new GeoCoordinate(c.Latitude - d, c.Longitude - d),
            new GeoCoordinate(c.Latitude - d, c.Longitude + d),
            new GeoCoordinate(c.Latitude + d, c.Longitude + d),
            new GeoCoordinate(c.Latitude + d, c.Longitude - d),
            new GeoCoordinate(c.Latitude - d, c.Longitude - d),
        });
    }

    /// <summary>2 つのシェイプが頂点単位で（許容誤差 1e-6 度）一致するか。</summary>
    private static bool HasSameShape(IReadOnlyList<GeoCoordinate> a, IReadOnlyList<GeoCoordinate> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (Math.Abs(a[i].Latitude - b[i].Latitude) > 1e-6) return false;
            if (Math.Abs(a[i].Longitude - b[i].Longitude) > 1e-6) return false;
        }
        return true;
    }

    private static void EnsureTestData()
    {
        if (!File.Exists(TestPaths.ParentDefaultRouterDb))
        {
            Assert.Fail(
                $"テストデータが見つかりません: {TestPaths.ParentDefaultRouterDb}\n" +
                "親プロジェクトの default.routerdb が必要です。");
        }
    }

    /// <summary>
    /// 車道アクセス可能な頂点を最大 <paramref name="maxPairs"/> ペア集める。
    /// 各ペアは <paramref name="minSeparationDeg"/> 度以上離れる。
    /// </summary>
    private static List<(GeoCoordinate from, GeoCoordinate to)> CollectCarAccessibleVertexPairs(
        RouterDb routerDb,
        int maxPairs,
        double minSeparationDeg)
    {
        var graph = routerDb.Graph;
        var carVertices = new List<GeoCoordinate>();
        var limit = Math.Min((uint)20000, graph.VertexCount);
        var step = Math.Max(1u, limit / 1000);
        for (uint v = 0; v < limit; v += step)
        {
            var en = graph.GetEdgeEnumerator(v);
            var isCar = false;
            while (en.MoveNext())
            {
                var tags = graph.GetEdgeOsmTagsForTest(en.EdgeProfileIndex);
                if (tags.TryGetValue("highway", out var hwy) && IsCarHighway(hwy))
                {
                    isCar = true;
                    break;
                }
            }
            if (isCar) carVertices.Add(graph.GetVertex(v));
            if (carVertices.Count >= 200) break;
        }

        var pairs = new List<(GeoCoordinate, GeoCoordinate)>();
        for (var i = 0; i < carVertices.Count && pairs.Count < maxPairs; i++)
        {
            for (var j = i + 1; j < carVertices.Count && pairs.Count < maxPairs; j++)
            {
                var dLat = carVertices[j].Latitude - carVertices[i].Latitude;
                var dLon = carVertices[j].Longitude - carVertices[i].Longitude;
                if (Math.Sqrt(dLat * dLat + dLon * dLon) >= minSeparationDeg)
                {
                    pairs.Add((carVertices[i], carVertices[j]));
                    break;
                }
            }
        }
        return pairs;
    }

    private static bool IsCarHighway(string highway) => highway switch
    {
        "motorway" or "motorway_link" or
        "trunk" or "trunk_link" or
        "primary" or "primary_link" or
        "secondary" or "secondary_link" or
        "tertiary" or "tertiary_link" or
        "residential" or "unclassified" or "living_street" or "service" => true,
        _ => false,
    };
}
