using global::Itinero;
using global::Itinero.Osm.Vehicles;
using OsmDotRoute.Itinero;
using OsmDotRoute.Tests.TestData;
using Xunit;

namespace OsmDotRoute.Tests;

/// <summary>
/// Phase 1 ステップ 5b「独自 Dijkstra（制約なし、JsonVehicleProfile 使用）」の検証テスト。
/// 親プロジェクト default.routerdb を使用し、Itinero <c>Router.Calculate</c> との総距離 ±10% 一致を確認する。
/// 完全一致は目指さない（プロファイル評価実装差で多少のブレあり）。
/// </summary>
public class CalculateRouteTests
{
    [Fact]
    public void Calculate_NullProfile_ThrowsArgumentNullException()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        Assert.Throws<ArgumentNullException>(
            () => router.Calculate(null!, new GeoCoordinate(35.0, 139.0), new GeoCoordinate(35.1, 139.1)));
    }

    [Fact]
    public void Calculate_FromOutsideNetwork_ReturnsNull()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        var stats = routerDb.GetStatistics();
        var farPoint = new GeoCoordinate(stats.NorthEast.Latitude + 5.0, stats.NorthEast.Longitude + 5.0);
        var inPoint = FindCarAccessibleVertex(routerDb);
        Assert.NotNull(inPoint);

        // 起点がネットワーク外
        Assert.Null(router.Calculate(VehicleProfile.Car, farPoint, inPoint.Value));
        // 終点がネットワーク外
        Assert.Null(router.Calculate(VehicleProfile.Car, inPoint.Value, farPoint));
    }

    [Fact]
    public void Calculate_SamePoint_ReturnsTrivialOrTinyRoute()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        var v = FindCarAccessibleVertex(routerDb);
        Assert.NotNull(v);

        var route = router.Calculate(VehicleProfile.Car, v.Value, v.Value);
        Assert.NotNull(route);
        // 同一点なら距離はごく小さい（スナップ誤差程度）
        Assert.InRange(route!.TotalDistanceM, 0.0, 50.0);
    }

    [Fact]
    public void Calculate_CarProfile_ItineroParity_TotalDistanceWithin10Percent()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        // Itinero 比較用に同じ routerdb を独自に読み込み
        using var stream = File.OpenRead(TestPaths.ParentDefaultRouterDb);
        var itineroDb = global::Itinero.RouterDb.Deserialize(stream);
        var itineroRouter = new global::Itinero.Router(itineroDb);
        var itineroCar = Vehicle.Car.Fastest();

        var pairs = CollectCarAccessibleVertexPairs(routerDb, maxPairs: 12, minSeparationDeg: 0.01);
        Assert.True(pairs.Count >= 5, $"テストペアが少なすぎる: {pairs.Count}（最低 5）");

        int compared = 0;
        int withinTolerance = 0;
        int bothNull = 0;
        int onlyOneNull = 0;
        var diagnostics = new List<string>();

        foreach (var (from, to) in pairs)
        {
            var ourRoute = router.Calculate(VehicleProfile.Car, from, to);

            var itineroResult = itineroRouter.TryCalculate(itineroCar, (float)from.Latitude, (float)from.Longitude,
                (float)to.Latitude, (float)to.Longitude);

            if (ourRoute is null && itineroResult.IsError)
            {
                bothNull++;
                continue;
            }
            if (ourRoute is null || itineroResult.IsError)
            {
                onlyOneNull++;
                diagnostics.Add($"片方のみ null: ours={(ourRoute is null ? "null" : "ok")} itinero={(itineroResult.IsError ? "err" : "ok")} from=({from.Latitude:F5},{from.Longitude:F5}) to=({to.Latitude:F5},{to.Longitude:F5})");
                continue;
            }

            compared++;
            var oursDist = ourRoute.TotalDistanceM;
            var itineroDist = itineroResult.Value.TotalDistance;
            var diff = Math.Abs(oursDist - itineroDist);
            var ratio = itineroDist > 0 ? diff / itineroDist : 0;
            if (ratio <= 0.10)
            {
                withinTolerance++;
            }
            else
            {
                diagnostics.Add($"距離乖離 >10%: ours={oursDist:F1}m itinero={itineroDist:F1}m diff={diff:F1}m ratio={ratio:P1}");
            }
        }

        var summary = $"比較対象 {compared} ペア中 ±10% 一致 {withinTolerance} ペア " +
                      $"(両方 null={bothNull}, 片方のみ null={onlyOneNull})";
        if (diagnostics.Count > 0)
        {
            summary += "\n" + string.Join("\n", diagnostics.Take(5));
        }

        // 完了判定: ±10% 一致が比較対象の 80% 以上、かつ片方のみ null が 20% 以下
        Assert.True(compared > 0, "Itinero と比較できたペアが 0 件");
        Assert.True(withinTolerance * 5 >= compared * 4, $"±10% 一致率が 80% 未満: {summary}");
        Assert.True(onlyOneNull * 5 <= pairs.Count, $"片方のみ null が 20% 超過: {summary}");
    }

    [Fact]
    public void Calculate_PedestrianProfile_ProducesValidRoute()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        var pairs = CollectCarAccessibleVertexPairs(routerDb, maxPairs: 3, minSeparationDeg: 0.005);
        Assert.True(pairs.Count >= 1);

        var route = router.Calculate(VehicleProfile.Pedestrian, pairs[0].from, pairs[0].to);
        Assert.NotNull(route);
        Assert.True(route!.TotalDistanceM > 0);
        Assert.True(route.TotalDurationSec > 0);
        Assert.True(route.Shape.Count >= 2);
    }

    [Fact]
    public void Calculate_RouteShape_StartsAtSnapFromAndEndsAtSnapTo()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        var pairs = CollectCarAccessibleVertexPairs(routerDb, maxPairs: 2, minSeparationDeg: 0.005);
        Assert.True(pairs.Count >= 1);

        var (from, to) = pairs[0];
        var route = router.Calculate(VehicleProfile.Car, from, to);
        Assert.NotNull(route);

        var snappedFrom = router.SnapToRoad(VehicleProfile.Car, from);
        var snappedTo = router.SnapToRoad(VehicleProfile.Car, to);
        Assert.NotNull(snappedFrom);
        Assert.NotNull(snappedTo);

        // シェイプ先頭はスナップ後の起点座標、末尾はスナップ後の終点座標
        Assert.Equal(snappedFrom!.Value.Latitude, route!.Shape[0].Latitude, precision: 5);
        Assert.Equal(snappedFrom.Value.Longitude, route.Shape[0].Longitude, precision: 5);
        Assert.Equal(snappedTo!.Value.Latitude, route.Shape[^1].Latitude, precision: 5);
        Assert.Equal(snappedTo.Value.Longitude, route.Shape[^1].Longitude, precision: 5);
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
    /// 車道アクセス可能な頂点を 1 つ探す（先頭から最大 5000 頂点走査）。
    /// </summary>
    private static GeoCoordinate? FindCarAccessibleVertex(OsmDotRoute.RouterDb routerDb)
    {
        var graph = routerDb.Graph;
        var limit = Math.Min((uint)5000, graph.VertexCount);
        for (uint v = 0; v < limit; v++)
        {
            var en = graph.GetEdgeEnumerator(v);
            while (en.MoveNext())
            {
                var tags = graph.GetEdgeOsmTags(en.EdgeProfileIndex);
                if (tags.TryGetValue("highway", out var hwy) && IsCarHighway(hwy))
                {
                    return graph.GetVertex(v);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 車道アクセス可能な頂点を最大 <paramref name="maxPairs"/> ペア集める。
    /// 各ペアは <paramref name="minSeparationDeg"/> 度以上離れた頂点を選ぶ（自明な短経路を避ける）。
    /// </summary>
    private static List<(GeoCoordinate from, GeoCoordinate to)> CollectCarAccessibleVertexPairs(
        OsmDotRoute.RouterDb routerDb,
        int maxPairs,
        double minSeparationDeg)
    {
        var graph = routerDb.Graph;
        var carVertices = new List<GeoCoordinate>();
        var limit = Math.Min((uint)20000, graph.VertexCount);
        // 頂点を一定間隔で抽出し、空間的に散らばらせる
        var step = Math.Max(1u, limit / 1000);
        for (uint v = 0; v < limit; v += step)
        {
            var en = graph.GetEdgeEnumerator(v);
            bool isCar = false;
            while (en.MoveNext())
            {
                var tags = graph.GetEdgeOsmTags(en.EdgeProfileIndex);
                if (tags.TryGetValue("highway", out var hwy) && IsCarHighway(hwy))
                {
                    isCar = true;
                    break;
                }
            }
            if (isCar) carVertices.Add(graph.GetVertex(v));
            if (carVertices.Count >= 200) break;
        }

        var pairs = new List<(GeoCoordinate from, GeoCoordinate to)>();
        for (int i = 0; i < carVertices.Count && pairs.Count < maxPairs; i++)
        {
            for (int j = i + 1; j < carVertices.Count && pairs.Count < maxPairs; j++)
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
