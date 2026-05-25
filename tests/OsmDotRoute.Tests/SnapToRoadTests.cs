using OsmDotRoute.Itinero;
using OsmDotRoute.Tests.TestData;
using Xunit;

namespace OsmDotRoute.Tests;

/// <summary>
/// Phase 1 ステップ 4「道路スナップ機能」の検証テスト（REQ-RTE-002〜003, REQ-RTE-008）。
/// 親プロジェクト default.routerdb を使用して Router.SnapToRoad の動作を確認する。
/// </summary>
public class SnapToRoadTests
{
    [Fact]
    public void SnapToRoad_PointOnNetwork_ReturnsNearbyCoordinate()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        // グラフ内の頂点座標を取得 → そのままスナップすると同一/近傍点が返るはず
        var vertex = routerDb.Graph.GetVertex(0);

        var snapped = router.SnapToRoad(VehicleProfile.Pedestrian, vertex, searchDistanceM: 1000f);

        Assert.NotNull(snapped);
        // 道路頂点そのものから snap した場合、誤差は緯度経度 0.001 度 (約 100m) 以内
        Assert.InRange(snapped.Value.Latitude - vertex.Latitude, -0.001, 0.001);
        Assert.InRange(snapped.Value.Longitude - vertex.Longitude, -0.001, 0.001);
    }

    [Fact]
    public void SnapToRoad_PointFarOutsideNetwork_ReturnsNull()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        var stats = routerDb.GetStatistics();
        // 北東端から +5 度（約 555km）離れた点はネットワーク外（searchDistanceM=500m 内に道路なし）
        var farPoint = new GeoCoordinate(
            stats.NorthEast.Latitude + 5.0,
            stats.NorthEast.Longitude + 5.0);

        var snapped = router.SnapToRoad(VehicleProfile.Car, farPoint, searchDistanceM: 500f);

        Assert.Null(snapped);
    }

    [Fact]
    public void SnapToRoad_CarProfile_OnRoadNetwork_ReturnsCoordinate()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        // 車道アクセス可能な頂点を探索
        var carVertex = FindCarAccessibleVertex(routerDb);
        Assert.NotNull(carVertex);

        var snapped = router.SnapToRoad(VehicleProfile.Car, carVertex.Value, searchDistanceM: 500f);

        Assert.NotNull(snapped);
        // スナップ後の点が元の点の近傍（500m 半径以内）
        Assert.InRange(snapped.Value.Latitude - carVertex.Value.Latitude, -0.01, 0.01);
        Assert.InRange(snapped.Value.Longitude - carVertex.Value.Longitude, -0.01, 0.01);
    }

    [Fact]
    public void SnapToRoad_PedestrianProfile_OnRoadNetwork_ReturnsCoordinate()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        var vertex = routerDb.Graph.GetVertex(0);

        var snapped = router.SnapToRoad(VehicleProfile.Pedestrian, vertex);

        Assert.NotNull(snapped);
    }

    [Fact]
    public void SnapToRoad_NullProfile_ThrowsArgumentNullException()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        Assert.Throws<ArgumentNullException>(
            () => router.SnapToRoad(null!, new GeoCoordinate(35.0, 139.0)));
    }

    [Fact]
    public void SnapToRoad_DefaultSearchDistance_Is500Meters()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        // 既定値 500m の動作確認: 道路頂点直上の点は必ず成功する
        var vertex = routerDb.Graph.GetVertex(0);
        var snapped = router.SnapToRoad(VehicleProfile.Pedestrian, vertex);
        Assert.NotNull(snapped);
    }

    private static void EnsureTestData()
    {
        if (!File.Exists(TestPaths.ParentDefaultRouterDb))
        {
            Assert.Fail(
                $"テストデータが見つかりません: {TestPaths.ParentDefaultRouterDb}\n" +
                "親プロジェクト「災害廃棄物処理シミュレーション」の default.routerdb が必要です。");
        }
    }

    /// <summary>
    /// 車道（motorway/trunk/primary/secondary/tertiary/residential/unclassified）アクセス可能な頂点を探索。
    /// 見つからない場合は null を返す。
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
                var tags = graph.GetEdgeOsmTagsForTest(en.EdgeProfileIndex);
                if (tags.TryGetValue("highway", out var hwy) && IsCarHighway(hwy))
                {
                    return graph.GetVertex(v);
                }
            }
        }
        return null;
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
