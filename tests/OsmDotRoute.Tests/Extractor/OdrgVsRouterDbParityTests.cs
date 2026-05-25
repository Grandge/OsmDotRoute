using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OsmDotRoute;
using OsmDotRoute.Extractor.Pipeline;
using OsmDotRoute.Itinero;
using OsmDotRoute.Profiles;
using OsmDotRoute.Tests.Extractor.Helpers;
using OsmDotRoute.Tests.TestData;
using Xunit.Abstractions;

namespace OsmDotRoute.Tests.Extractor;

/// <summary>
/// 津島市 PBF から ExtractPipeline + OdrgReader + Phase 1 RouterDb の 3 点を 1 回だけロードする fixture。
/// PAR-1〜PAR-4 は同一 fixture を共有してテストごとの再ロードを避ける。
/// </summary>
public sealed class TsushimaParityFixture
{
    internal OdrgReadResult Odrg { get; }
    internal ExtractPipelineResult Extract { get; }
    internal RouterDb RouterDb { get; }
    internal Aabb Bbox { get; }

    public TsushimaParityFixture()
    {
        if (!File.Exists(TestPaths.TsushimaExtractPbf))
            Assert.Fail($"テストデータが見つかりません: {TestPaths.TsushimaExtractPbf}");
        if (!File.Exists(TestPaths.ParentDefaultRouterDb))
            Assert.Fail($"親プロジェクトの RouterDb が見つかりません: {TestPaths.ParentDefaultRouterDb}");

        Bbox = new Aabb(MinLon: 136.65, MinLat: 35.13, MaxLon: 136.80, MaxLat: 35.25);

        var extractOpts = new ExtractPipelineOptions(
            InputPbf: TestPaths.TsushimaExtractPbf,
            Bbox: Bbox,
            Profiles: new[] { VehicleProfile.Car, VehicleProfile.Pedestrian });
        Extract = ExtractPipeline.Run(extractOpts);

        var writeInput = new OdrgWriteInput(
            Vertices: Extract.Vertices,
            Edges: Extract.Edges,
            EdgeAabbs: Extract.EdgeAabbs,
            EdgeFlags: Extract.EdgeFlags,
            RTree: Extract.RTree,
            ProfileTable: Extract.ProfileTable,
            NodeCoordLookup: Extract.NodeCoordLookup,
            Bbox: Extract.FileBbox,
            MetadataJson: "{}");
        using var ms = new MemoryStream();
        OdrgWriter.Write(ms, writeInput);
        ms.Position = 0;
        Odrg = OdrgReader.Read(ms);

        RouterDb = ItineroRouterDbLoader.LoadFromFile(TestPaths.ParentDefaultRouterDb);
    }
}

/// <summary>
/// サブステップ 5.3 — Phase 2 <c>.odrg</c> と Phase 1 RouterDb の統計突合テスト (PAR-1〜PAR-4)。
/// </summary>
public sealed class OdrgVsRouterDbParityTests : IClassFixture<TsushimaParityFixture>
{
    private const double Tolerance = 0.30;  // ±30% 許容 (計画書 §4.3 PAR-1/PAR-2)
    private const int Par4SampleSize = 100;

    private readonly TsushimaParityFixture _fixture;
    private readonly ITestOutputHelper _output;

    public OdrgVsRouterDbParityTests(TsushimaParityFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// 診断: bbox 絞込後の頂点・辺数と比率を出力 (デバッグ・記録用)。
    /// </summary>
    [Fact]
    public void DIAG_PrintFilteredStats()
    {
        var rd = RouterDbBboxFilter.Filter(_fixture.RouterDb, _fixture.Bbox);
        var od = RouterDbBboxFilter.FilterOdrg(_fixture.Odrg, _fixture.Bbox);

        _output.WriteLine($"bbox: {_fixture.Bbox}");
        _output.WriteLine($"RouterDb (絞込): vertices={rd.VertexCount}, edges={rd.EdgeCount}");
        _output.WriteLine($"   bounds: {rd.FilteredBounds}");
        _output.WriteLine($".odrg (絞込):    vertices={od.VertexCount}, edges={od.EdgeCount}");
        _output.WriteLine($"   bounds: {od.FilteredBounds}");
        _output.WriteLine($".odrg (全体):    vertices={_fixture.Odrg.Vertices.Length}, edges={_fixture.Odrg.Edges.Length}");
        _output.WriteLine($"   header bbox: {_fixture.Odrg.Header.Bbox}");
        _output.WriteLine($"頂点数比 odrg/routerDb: {(double)od.VertexCount / rd.VertexCount:F3}");
        _output.WriteLine($"辺数比   odrg/routerDb: {(double)od.EdgeCount / rd.EdgeCount:F3}");
    }

    // ===== PAR-1: bbox 絞込頂点数の比較 =====

    [Fact]
    public void PAR01_VertexCountsWithinTolerance()
    {
        var rd = RouterDbBboxFilter.Filter(_fixture.RouterDb, _fixture.Bbox);
        var od = RouterDbBboxFilter.FilterOdrg(_fixture.Odrg, _fixture.Bbox);

        Assert.True(rd.VertexCount > 0, $"RouterDb bbox 内頂点が 0、bbox 設定誤りの可能性");
        Assert.True(od.VertexCount > 0, $".odrg bbox 内頂点が 0、bbox 設定誤りの可能性");

        double ratio = (double)od.VertexCount / rd.VertexCount;
        Assert.True(
            ratio >= 1 - Tolerance && ratio <= 1 + Tolerance,
            $"頂点数比 {ratio:F3} が許容範囲 [{1 - Tolerance:F2},{1 + Tolerance:F2}] 外。" +
            $"odrg={od.VertexCount}, routerDb={rd.VertexCount}");
    }

    // ===== PAR-2: bbox 絞込辺数の比較 =====

    [Fact]
    public void PAR02_EdgeCountsWithinTolerance()
    {
        var rd = RouterDbBboxFilter.Filter(_fixture.RouterDb, _fixture.Bbox);
        var od = RouterDbBboxFilter.FilterOdrg(_fixture.Odrg, _fixture.Bbox);

        Assert.True(rd.EdgeCount > 0, $"RouterDb bbox 内辺が 0");
        Assert.True(od.EdgeCount > 0, $".odrg bbox 内辺が 0");

        double ratio = (double)od.EdgeCount / rd.EdgeCount;
        Assert.True(
            ratio >= 1 - Tolerance && ratio <= 1 + Tolerance,
            $"辺数比 {ratio:F3} が許容範囲 [{1 - Tolerance:F2},{1 + Tolerance:F2}] 外。" +
            $"odrg={od.EdgeCount}, routerDb={rd.EdgeCount}");
    }

    // ===== PAR-3: .odrg Header bbox が RouterDb 絞込範囲を概ね包含 =====

    [Fact]
    public void PAR03_OdrgHeaderBboxCoversFilteredRouterDbBounds()
    {
        var rd = RouterDbBboxFilter.Filter(_fixture.RouterDb, _fixture.Bbox);
        var odrgBbox = _fixture.Odrg.Header.Bbox;

        // .odrg Header bbox は実 vertex bounds (way 拡張で bbox 外も含む) なので、
        // 絞込 RouterDb の bounds (strict bbox 内) を必ず包含するはず。
        // 浮動小数の誤差を考慮して微小マージン。
        const double Margin = 1e-6;
        Assert.True(odrgBbox.MinLon <= rd.FilteredBounds.MinLon + Margin,
            $".odrg minLon {odrgBbox.MinLon} > RouterDb 絞込 minLon {rd.FilteredBounds.MinLon}");
        Assert.True(odrgBbox.MinLat <= rd.FilteredBounds.MinLat + Margin,
            $".odrg minLat {odrgBbox.MinLat} > RouterDb 絞込 minLat {rd.FilteredBounds.MinLat}");
        Assert.True(odrgBbox.MaxLon >= rd.FilteredBounds.MaxLon - Margin,
            $".odrg maxLon {odrgBbox.MaxLon} < RouterDb 絞込 maxLon {rd.FilteredBounds.MaxLon}");
        Assert.True(odrgBbox.MaxLat >= rd.FilteredBounds.MaxLat - Margin,
            $".odrg maxLat {odrgBbox.MaxLat} < RouterDb 絞込 maxLat {rd.FilteredBounds.MaxLat}");
    }

    // ===== PAR-4: bake プロファイルが Phase 1 ProfileEvaluator と完全一致 =====

    [Fact]
    public void PAR04_BakedProfileMatchesEvaluatorForSampledEdges()
    {
        var extract = _fixture.Extract;
        var odrg = _fixture.Odrg;

        // 決定論的サンプリング (seed 固定で再現性確保)
        var rng = new Random(42);  // 決定論性のため seed 固定
        int edgeCount = extract.Edges.Length;
        int sampleSize = Math.Min(Par4SampleSize, edgeCount);
        var sampledIds = new HashSet<int>(sampleSize);
        while (sampledIds.Count < sampleSize)
            sampledIds.Add(rng.Next(0, edgeCount));

        var profiles = new[] { VehicleProfile.Car, VehicleProfile.Pedestrian };
        foreach (int edgeId in sampledIds)
        {
            for (int p = 0; p < profiles.Length; p++)
            {
                var expected = ReevaluateFromTags(extract.Edges[edgeId], profiles[p]);
                var actual = odrg.ProfileTable.EntriesByProfile[p][edgeId];
                Assert.Equal(expected.SpeedKmh, actual.SpeedKmh);
                Assert.Equal(expected.Flags, actual.Flags);
            }
        }
    }

    /// <summary>
    /// <see cref="ProfileBaker"/> とは独立に、エッジの OSM タグから profile evaluator を呼び直し、
    /// 期待される <see cref="BakedProfileEntry"/> を再構築する。
    /// ProfileBaker の wiring バグ (タグ展開・Oneway→bit 変換・canPass→speed null) を検出する。
    /// </summary>
    private static BakedProfileEntry ReevaluateFromTags(EdgeRecord edge, VehicleProfile profile)
    {
        var tagDict = new Dictionary<string, string>(edge.TagKeys.Length, StringComparer.Ordinal);
        for (int i = 0; i < edge.TagKeys.Length; i++)
        {
            string key = Encoding.UTF8.GetString(edge.StringTable.GetBytes(edge.TagKeys[i]));
            string value = Encoding.UTF8.GetString(edge.StringTable.GetBytes(edge.TagValues[i]));
            tagDict[key] = value;  // 同キー後勝ち
        }
        EdgeEvaluation eval = profile.Evaluator.Evaluate(tagDict);

        bool forward, backward;
        switch (eval.Oneway)
        {
            case OnewayDirection.Forward: forward = true; backward = false; break;
            case OnewayDirection.Backward: forward = false; backward = true; break;
            default: forward = true; backward = true; break;
        }
        if (!eval.CanPass) { forward = false; backward = false; }

        return BakedProfileEntry.Create(eval.CanPass, eval.SpeedKmh, forward, backward);
    }

    // ===== 補助テスト: ヘルパ自体の妥当性 =====

    [Fact]
    public void RouterDbBboxFilter_TightInnerBbox_ReturnsLessThanFull()
    {
        // 1/4 程度に狭めた bbox でフィルタすると、頂点数は元の bbox より少なくなるはず
        var fullStats = RouterDbBboxFilter.Filter(_fixture.RouterDb, _fixture.Bbox);
        var narrow = new Aabb(MinLon: 136.71, MinLat: 35.17, MaxLon: 136.74, MaxLat: 35.19);
        var narrowStats = RouterDbBboxFilter.Filter(_fixture.RouterDb, narrow);

        Assert.True(narrowStats.VertexCount < fullStats.VertexCount,
            $"狭い bbox の頂点数 {narrowStats.VertexCount} が広い bbox の {fullStats.VertexCount} 以上");
        Assert.True(narrowStats.VertexCount > 0);
    }
}
