using OsmDotRoute.Geometry;
using OsmDotRoute.Itinero;
using OsmDotRoute.Tests.TestData;
using Xunit;

namespace OsmDotRoute.Tests;

/// <summary>
/// Phase 3 ステップ 3B.2 — <see cref="ItineroRoadGraph.QueryEdgesByAabb"/> fallback 実装の検証 3 件（計画書 §4.2.3）。
/// </summary>
/// <remarks>
/// ユーザー判断 T6=(A) 都度計算 (GetEdge(e) でシェイプ + 端点から AABB を計算) を検証する。
/// 親プロ default.routerdb 依存のため <c>EnsureTestData</c> でファイル存在を確認する Phase 1 既存パターン踏襲。
/// </remarks>
public class ItineroRoadGraphQueryEdgesByAabbTests
{
    [Fact]
    public void QueryEdgesByAabb_FullBounds_ReturnsAllEdges()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var graph = routerDb.Graph;
        var bounds = graph.GetBounds();
        var aabb = new Aabb(bounds.SouthWest, bounds.NorthEast);

        // 全 bounds でエッジ取得 (Itinero は大規模 routerdb のため、最初の 1000 件で十分)
        var result = graph.QueryEdgesByAabb(aabb).Take(1000).ToList();
        Assert.True(result.Count == 1000, $"全範囲クエリで {result.Count} 件、>= 1000 期待");
    }

    [Fact]
    public void QueryEdgesByAabb_OutOfBounds_ReturnsEmpty()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var graph = routerDb.Graph;
        // 日本領域 (default.routerdb は N20-46/E122-154) から離れた範囲
        var far = new Aabb(
            new GeoCoordinate(89.0, 0.0),
            new GeoCoordinate(89.1, 0.1));

        var result = graph.QueryEdgesByAabb(far).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void QueryEdgesByAabb_SmallBox_SelfConsistencyWithReturnedEdges()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var graph = routerDb.Graph;
        var bounds = graph.GetBounds();

        // bounds 中央の小さな矩形 (≈ 1km)
        double cLat = (bounds.SouthWest.Latitude + bounds.NorthEast.Latitude) / 2;
        double cLon = (bounds.SouthWest.Longitude + bounds.NorthEast.Longitude) / 2;
        var smallBox = new Aabb(
            new GeoCoordinate(cLat - 0.005, cLon - 0.005),
            new GeoCoordinate(cLat + 0.005, cLon + 0.005));

        // 結果は最大 100 件取得 (ItineroRoadGraph 全エッジ走査の自己整合検証)
        var result = graph.QueryEdgesByAabb(smallBox).Take(100).ToList();
        Assert.True(result.Count > 0, "中央の小矩形でエッジが 1 件以上見つかること");

        // 結果の各エッジが本当に smallBox と交差するかを再計算して確認 (T6 自己整合)
        foreach (var edgeId in result)
        {
            var edge = graph.GetEdge(edgeId);
            var from = graph.GetVertex(edge.From);
            var to = graph.GetVertex(edge.To);

            double minLat = Math.Min(from.Latitude, to.Latitude);
            double maxLat = Math.Max(from.Latitude, to.Latitude);
            double minLon = Math.Min(from.Longitude, to.Longitude);
            double maxLon = Math.Max(from.Longitude, to.Longitude);

            foreach (var c in edge.Shape)
            {
                if (c.Latitude < minLat) minLat = c.Latitude;
                if (c.Latitude > maxLat) maxLat = c.Latitude;
                if (c.Longitude < minLon) minLon = c.Longitude;
                if (c.Longitude > maxLon) maxLon = c.Longitude;
            }

            bool intersects = !(maxLat < smallBox.MinLatitude
                             || minLat > smallBox.MaxLatitude
                             || maxLon < smallBox.MinLongitude
                             || minLon > smallBox.MaxLongitude);
            Assert.True(intersects, $"edgeId={edgeId} の AABB が smallBox と交差しない (自己整合 fail)");
        }
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
}
