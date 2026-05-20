using System.IO;
using OsmDotRoute.Pbf;
using OsmDotRoute.Pbf.Osm;
using OsmDotRoute.Tests.TestData;

namespace OsmDotRoute.Tests.Pbf;

/// <summary>
/// PbfReader を実際の OSM PBF ファイル (親プロジェクトの津島市抽出) に対して実行する統合テスト。
/// OsmSharp を基準実装として、Node / Way / Relation 数の一致を確認する。
/// </summary>
public class PbfReaderIntegrationTests
{
    [Fact]
    public void Read_TsushimaPbf_HeaderHasExpectedBboxAndFeatures()
    {
        EnsureTestData();

        using var fs = File.OpenRead(TestPaths.TsushimaExtractPbf);
        OsmHeader header = PbfReader.Read(fs);

        // 津島市は愛知県北西部 (経度 136.6-136.8、緯度 35.1-35.25)
        Assert.NotNull(header.BoundingBox);
        OsmBoundingBox bbox = header.BoundingBox!.Value;
        Assert.InRange(bbox.MinLon, 136.5, 137.0);
        Assert.InRange(bbox.MaxLon, 136.5, 137.0);
        Assert.InRange(bbox.MinLat, 35.0, 35.3);
        Assert.InRange(bbox.MaxLat, 35.0, 35.3);

        // 現代 OSM PBF (osmconvert / Osmosis) は両 feature を出力
        Assert.Contains("OsmSchema-V0.6", header.RequiredFeatures);
        Assert.Contains("DenseNodes", header.RequiredFeatures);
    }

    [Fact]
    public void Read_TsushimaPbf_CountsMatchOsmSharp()
    {
        EnsureTestData();

        // (1) OsmDotRoute.Pbf でカウント
        int dotrouteNodes = 0, dotrouteWays = 0, dotrouteRelations = 0;
        using (var fs = File.OpenRead(TestPaths.TsushimaExtractPbf))
        {
            PbfReader.Read(fs,
                onNode: (_, _) => dotrouteNodes++,
                onWay: (_, _) => dotrouteWays++,
                onRelation: (_, _) => dotrouteRelations++);
        }

        // (2) OsmSharp (Itinero.IO.Osm の依存) を基準実装としてカウント
        int sharpNodes = 0, sharpWays = 0, sharpRelations = 0;
        using (var fs = File.OpenRead(TestPaths.TsushimaExtractPbf))
        {
            var source = new OsmSharp.Streams.PBFOsmStreamSource(fs);
            foreach (var geo in source)
            {
                switch (geo.Type)
                {
                    case OsmSharp.OsmGeoType.Node: sharpNodes++; break;
                    case OsmSharp.OsmGeoType.Way: sharpWays++; break;
                    case OsmSharp.OsmGeoType.Relation: sharpRelations++; break;
                }
            }
        }

        Assert.True(sharpNodes > 0, "Sanity: 津島市 PBF にノードが含まれるはず");
        Assert.True(sharpWays > 0, "Sanity: 津島市 PBF にウェイが含まれるはず");

        Assert.Equal(sharpNodes, dotrouteNodes);
        Assert.Equal(sharpWays, dotrouteWays);
        Assert.Equal(sharpRelations, dotrouteRelations);
    }

    [Fact]
    public void Read_TsushimaPbf_NodeCoordinatesMatchOsmSharp()
    {
        EnsureTestData();

        // 先頭 N 件のノードについて、PbfReader と OsmSharp の (lon, lat) が完全一致することを検証。
        // way 完全性のため bbox 外ノードも含まれるが、座標自体は両実装で一致するはず。
        const int SampleLimit = 1000;
        var ourCoords = new System.Collections.Generic.Dictionary<long, (double Lon, double Lat)>(SampleLimit);

        using (var fs = File.OpenRead(TestPaths.TsushimaExtractPbf))
        {
            PbfReader.Read(fs, onNode: (node, _) =>
            {
                if (ourCoords.Count < SampleLimit)
                {
                    ourCoords[node.Id] = (node.Lon, node.Lat);
                }
            });
        }
        Assert.Equal(SampleLimit, ourCoords.Count);

        int checkedNodes = 0;
        using (var fs = File.OpenRead(TestPaths.TsushimaExtractPbf))
        {
            var source = new OsmSharp.Streams.PBFOsmStreamSource(fs);
            foreach (var geo in source)
            {
                if (geo is OsmSharp.Node sharpNode &&
                    sharpNode.Id is long sharpId &&
                    sharpNode.Longitude is double sharpLon &&
                    sharpNode.Latitude is double sharpLat &&
                    ourCoords.TryGetValue(sharpId, out var our))
                {
                    // PBF granularity=100 (1e-7 度)、double 比較は精度 7 桁で十分
                    Assert.Equal(sharpLon, our.Lon, precision: 7);
                    Assert.Equal(sharpLat, our.Lat, precision: 7);
                    checkedNodes++;
                    if (checkedNodes >= SampleLimit) break;
                }
            }
        }
        Assert.True(checkedNodes >= SampleLimit / 2,
            $"OsmSharp と座標比較できたノードが {checkedNodes} 件のみ（500 件以上を期待）");
    }

    [Fact]
    public void Read_TsushimaPbf_HighwayWaysExtractable()
    {
        EnsureTestData();

        // highway=* タグを持つ way の数を数えて、道路が抽出可能であることを確認
        int totalWays = 0;
        int highwayWays = 0;

        using var fs = File.OpenRead(TestPaths.TsushimaExtractPbf);
        PbfReader.Read(fs, onWay: (way, stringTable) =>
        {
            totalWays++;
            for (int i = 0; i < way.TagKeys.Length; i++)
            {
                if (stringTable.GetString(way.TagKeys[i]) == "highway")
                {
                    highwayWays++;
                    break;
                }
            }
        });

        Assert.True(totalWays > 0);
        // 津島市 PBF (Phase 1 ベンチで 57k エッジ) は数千〜数万 way、その大半が道路
        Assert.True(highwayWays > 1000,
            $"highway=* の way 数 ({highwayWays}) が予想より少ない（数千〜数万を期待）");
    }

    private static void EnsureTestData()
    {
        if (!File.Exists(TestPaths.TsushimaExtractPbf))
        {
            Assert.Fail(
                $"テストデータが見つかりません: {TestPaths.TsushimaExtractPbf}\n" +
                "親プロジェクト「災害廃棄物処理シミュレーション」の tsushima_extract.osm.pbf が必要です。");
        }
    }
}
