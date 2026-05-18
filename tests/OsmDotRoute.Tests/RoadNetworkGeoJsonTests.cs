using System.Text.Json;
using OsmDotRoute.Itinero;
using OsmDotRoute.Tests.TestData;
using Xunit;
using ItineroDb = global::Itinero.RouterDb;

namespace OsmDotRoute.Tests;

/// <summary>
/// Phase 1 ステップ 6「道路ネットワーク GeoJSON 出力」の検証テスト（REQ-RTE-004）。
/// 親プロジェクト default.routerdb を使用し、Itinero 直接列挙と features 数一致を確認する。
/// </summary>
public class RoadNetworkGeoJsonTests
{
    [Fact]
    public void GetRoadNetworkGeoJson_ProducesValidJsonRoundtrip()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        var geoJson = router.GetRoadNetworkGeoJson();
        Assert.NotNull(geoJson);
        Assert.False(string.IsNullOrWhiteSpace(geoJson.Json));

        // System.Text.Json でパースし、最低限のスキーマを検証
        using var doc = JsonDocument.Parse(geoJson.Json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("FeatureCollection", root.GetProperty("type").GetString());

        var features = root.GetProperty("features");
        Assert.Equal(JsonValueKind.Array, features.ValueKind);
        Assert.True(features.GetArrayLength() > 0, "features が 0 件");

        // 先頭 feature のスキーマ確認
        var first = features[0];
        Assert.Equal("Feature", first.GetProperty("type").GetString());
        var geometry = first.GetProperty("geometry");
        Assert.Equal("LineString", geometry.GetProperty("type").GetString());
        var coords = geometry.GetProperty("coordinates");
        Assert.True(coords.GetArrayLength() >= 2, "LineString は少なくとも 2 点必要");
        // 各座標は [lon, lat] の 2 要素
        var firstCoord = coords[0];
        Assert.Equal(2, firstCoord.GetArrayLength());
        var lon = firstCoord[0].GetDouble();
        var lat = firstCoord[1].GetDouble();
        // 日本領域内（REQ-NFR-009）
        Assert.InRange(lon, 122.0, 154.0);
        Assert.InRange(lat, 20.0, 46.0);

        Assert.True(first.TryGetProperty("properties", out var props));
        Assert.Equal(JsonValueKind.Object, props.ValueKind);
    }

    [Fact]
    public void GetRoadNetworkGeoJson_FeatureCount_MatchesItineroDirectEnumeration()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        var geoJson = router.GetRoadNetworkGeoJson();
        using var doc = JsonDocument.Parse(geoJson.Json);
        var ourFeatureCount = doc.RootElement.GetProperty("features").GetArrayLength();

        // Itinero RouterDb を直接読み、エッジ ID 重複排除で同じ要領で feature 数を数える
        using var stream = File.OpenRead(TestPaths.ParentDefaultRouterDb);
        var itineroDb = ItineroDb.Deserialize(stream);
        var network = itineroDb.Network;
        var processed = new HashSet<uint>();
        for (uint v = 0; v < network.VertexCount; v++)
        {
            var en = network.GetEdgeEnumerator(v);
            while (en.MoveNext())
            {
                processed.Add(en.Id);
            }
        }
        var expectedFeatureCount = processed.Count;

        Assert.Equal(expectedFeatureCount, ourFeatureCount);
    }

    [Fact]
    public void GetRoadNetworkGeoJson_FeatureCount_EqualsGraphEdgeCount()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);
        var stats = routerDb.GetStatistics();

        var geoJson = router.GetRoadNetworkGeoJson();
        using var doc = JsonDocument.Parse(geoJson.Json);
        var featureCount = doc.RootElement.GetProperty("features").GetArrayLength();

        // RouterDbStatistics.EdgeCount は重複排除済みの辺数。features 数と一致するはず。
        Assert.Equal(stats.EdgeCount, featureCount);
    }

    [Fact]
    public void GetRoadNetworkGeoJson_AllCoordinatesInJapanBounds()
    {
        EnsureTestData();
        var routerDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
        var router = new Router(routerDb);

        var geoJson = router.GetRoadNetworkGeoJson();
        using var doc = JsonDocument.Parse(geoJson.Json);
        var features = doc.RootElement.GetProperty("features");

        // 先頭 100 features を抜き取り検査
        var limit = Math.Min(100, features.GetArrayLength());
        for (int i = 0; i < limit; i++)
        {
            var coords = features[i].GetProperty("geometry").GetProperty("coordinates");
            foreach (var c in coords.EnumerateArray())
            {
                var lon = c[0].GetDouble();
                var lat = c[1].GetDouble();
                Assert.InRange(lon, 122.0, 154.0);
                Assert.InRange(lat, 20.0, 46.0);
            }
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
