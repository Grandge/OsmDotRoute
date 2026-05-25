using OsmDotRoute.Itinero;
using OsmDotRoute.Tests.TestData;
using Xunit;

namespace OsmDotRoute.Tests;

/// <summary>
/// Phase 1 ステップ 3「Itinero アダプター」の検証テスト。
/// 親プロジェクトの default.routerdb（実データ、約 19MB）を使用してアダプター動作を確認する。
/// テストデータが配置されていない環境ではテストが失敗するため、CI 化時は別途対応する。
/// </summary>
public class ItineroAdapterTests
{
    [Fact]
    public void LoadFromFile_ParentDefault_LoadsAndExposesBasicStatistics()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);

        var stats = routerDb.GetStatistics();

        Assert.True(stats.VertexCount > 0, "頂点数が 0 より大きいこと");
        Assert.True(stats.EdgeCount > 0, "辺数が 0 より大きいこと");
        Assert.True(stats.SouthWest.Latitude <= stats.NorthEast.Latitude, "南西緯度 <= 北東緯度");
        Assert.True(stats.SouthWest.Longitude <= stats.NorthEast.Longitude, "南西経度 <= 北東経度");
    }

    [Fact]
    public void LoadFromFile_ParentDefault_BoundsAreInJapan()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);

        var stats = routerDb.GetStatistics();

        // 親プロは日本国内のメッシュデータ。緯度経度範囲が日本領域内であること（REQ-NFR-009）。
        Assert.InRange(stats.SouthWest.Latitude, 20.0, 46.0);
        Assert.InRange(stats.NorthEast.Latitude, 20.0, 46.0);
        Assert.InRange(stats.SouthWest.Longitude, 122.0, 154.0);
        Assert.InRange(stats.NorthEast.Longitude, 122.0, 154.0);
    }

    [Fact]
    public void GetEdgeEnumerator_ParentDefault_EnumeratesEdgesWithValidData()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var graph = routerDb.Graph;

        // エッジを持つ頂点を探索（孤立頂点を避ける）
        var enumerator = FindEnumeratorWithAtLeastOneEdge(graph, maxVerticesToScan: 1000);
        Assert.NotNull(enumerator);

        int verifiedEdges = 0;
        do
        {
            Assert.True(enumerator!.To < graph.VertexCount, "終点 ID がグラフ範囲内");
            Assert.True(enumerator.DistanceM > 0f, "距離が正値");
            verifiedEdges++;
        }
        while (enumerator.MoveNext() && verifiedEdges < 10);

        Assert.True(verifiedEdges > 0);
    }

    [Fact]
    public void GetEdgeOsmTagsForTest_ParentDefault_ContainsHighwayKey()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var graph = routerDb.Graph;

        var enumerator = FindEnumeratorWithAtLeastOneEdge(graph, maxVerticesToScan: 1000);
        Assert.NotNull(enumerator);

        var tags = graph.GetEdgeOsmTagsForTest(enumerator!.EdgeProfileIndex);
        Assert.NotEmpty(tags);
        // OSM 道路エッジには必ず highway タグが付く
        Assert.Contains("highway", tags.Keys);
    }

    [Fact]
    public void LoadFromFile_NonExistentPath_ThrowsFileNotFoundException()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".routerdb");
        Assert.False(File.Exists(fakePath), "テスト前提: パスは存在しない");

        Assert.Throws<FileNotFoundException>(
            () => ItineroRouterDbLoader.LoadFromFile(fakePath));
    }

    [Fact]
    public void LoadFromFile_NullOrEmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ItineroRouterDbLoader.LoadFromFile(""));
        Assert.Throws<ArgumentException>(() => ItineroRouterDbLoader.LoadFromFile("   "));
        Assert.Throws<ArgumentException>(() => ItineroRouterDbLoader.LoadFromFile(null!));
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

    private static OsmDotRoute.Routing.IRoadGraphEdgeEnumerator? FindEnumeratorWithAtLeastOneEdge(
        OsmDotRoute.Routing.IRoadGraph graph, int maxVerticesToScan)
    {
        var limit = Math.Min((uint)maxVerticesToScan, graph.VertexCount);
        for (uint v = 0; v < limit; v++)
        {
            var enumerator = graph.GetEdgeEnumerator(v);
            if (enumerator.MoveNext())
            {
                return enumerator;
            }
        }
        return null;
    }
}
