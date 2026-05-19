using BenchmarkDotNet.Attributes;
using OsmDotRoute.Benchmarks.Generators;

namespace OsmDotRoute.Benchmarks.Benchmarks;

/// <summary>
/// 制約下の経路計算性能（REQ-NFR-002）。
/// 計画書 §3.4 の 5 ケース（C0〜C4）を <see cref="Case"/> パラメータで切替。
/// </summary>
[MemoryDiagnoser]
public class RouteWithConstraintsBenchmark
{
    private OsmDotRoute.RouterDb _routerDb = default!;
    private Router _router = default!;
    private VehicleProfile _profile = default!;
    private RoutePair[] _pairs = default!;
    private int _index;

    /// <summary>
    /// C0: 制約なし / C1: 混合 10 件 / C2: 混合 50 件 / C3: 混合 100 件 / C4: Block-only 100 件。
    /// </summary>
    [Params("C0", "C1", "C2", "C3", "C4")]
    public string Case { get; set; } = "C0";

    [GlobalSetup]
    public void Setup()
    {
        _routerDb = BenchmarkAssets.LoadOsmDotRouterDb();
        _profile = VehicleProfile.Car;
        _pairs = [.. TestDataInitializer.LoadRoutePairs().Pairs];
        _index = 0;

        RestrictedAreaService? restrictions = Case switch
        {
            "C0" => null,
            "C1" => BuildMixed(10),
            "C2" => BuildMixed(50),
            "C3" => BuildMixed(100),
            "C4" => BuildBlockOnly(100),
            _ => throw new InvalidOperationException($"未知の Case: {Case}"),
        };
        _router = new Router(_routerDb, restrictions);
    }

    [Benchmark(Description = "Router.Calculate (制約付き、Case による)")]
    public Route? Calculate()
    {
        var pair = _pairs[_index];
        _index = (_index + 1) % _pairs.Length;
        return _router.Calculate(_profile, pair.From, pair.To);
    }

    private static RestrictedAreaService BuildMixed(int count)
    {
        var file = TestDataInitializer.LoadMixedRestrictions();
        return Register(file, count);
    }

    private static RestrictedAreaService BuildBlockOnly(int count)
    {
        var file = TestDataInitializer.LoadBlockRestrictions();
        return Register(file, count);
    }

    private static RestrictedAreaService Register(RestrictionsFile file, int count)
    {
        var service = new RestrictedAreaService();
        var polygons = RestrictionGenerator.ToPolygons(file);
        for (var i = 0; i < count && i < polygons.Count; i++)
        {
            var (entry, polygon) = polygons[i];
            if (entry.Type == "block")
            {
                service.AddBlockArea(polygon);
            }
            else
            {
                service.AddDifficultyArea(polygon, entry.DifficultyType!);
            }
        }
        return service;
    }
}
