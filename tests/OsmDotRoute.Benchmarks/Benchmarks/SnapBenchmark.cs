using BenchmarkDotNet.Attributes;
using OsmDotRoute.Benchmarks.Generators;

namespace OsmDotRoute.Benchmarks.Benchmarks;

/// <summary>
/// 道路スナップ単独の性能（経路計算の内訳分解用、計画書 §3.1 補助）。
/// 同じ route-pairs.json の From 座標を順繰りに使う（100 サンプル）。
/// </summary>
[MemoryDiagnoser]
public class SnapBenchmark
{
    private OsmDotRoute.RouterDb _routerDb = default!;
    private Router _router = default!;
    private VehicleProfile _profile = default!;
    private RoutePair[] _pairs = default!;
    private int _index;

    [GlobalSetup]
    public void Setup()
    {
        _routerDb = BenchmarkAssets.LoadOsmDotRouterDb();
        _router = new Router(_routerDb);
        _profile = VehicleProfile.Car;
        _pairs = [.. TestDataInitializer.LoadRoutePairs().Pairs];
        _index = 0;
    }

    [Benchmark(Description = "Router.SnapToRoad")]
    public GeoCoordinate? Snap()
    {
        var pair = _pairs[_index];
        _index = (_index + 1) % _pairs.Length;
        return _router.SnapToRoad(_profile, pair.From, 500f);
    }
}
