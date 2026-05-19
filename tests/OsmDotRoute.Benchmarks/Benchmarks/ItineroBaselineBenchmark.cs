using BenchmarkDotNet.Attributes;
using OsmDotRoute.Benchmarks.Generators;
using ItineroDb = global::Itinero.RouterDb;
using ItineroRouter = global::Itinero.Router;
using ItineroProfile = global::Itinero.Profiles.Profile;
using ItineroRoute = global::Itinero.Route;
using global::Itinero;

namespace OsmDotRoute.Benchmarks.Benchmarks;

/// <summary>
/// Itinero ネイティブの経路計算性能（比較基準、計画書 §5.1.1）。
/// `RouteCalculationBenchmark` と同じ route-pairs.json を使い、`OsmDotRoute / Itinero` の Mean 比を算出する。
/// </summary>
[MemoryDiagnoser]
public class ItineroBaselineBenchmark
{
    private ItineroDb _routerDb = default!;
    private ItineroRouter _router = default!;
    private ItineroProfile _profile = default!;
    private RoutePair[] _pairs = default!;
    private int _index;

    [GlobalSetup]
    public void Setup()
    {
        _routerDb = BenchmarkAssets.LoadItineroRouterDb();
        _router = new ItineroRouter(_routerDb);
        _profile = _routerDb.GetSupportedProfile("car")
            ?? throw new InvalidOperationException("Itinero RouterDb に 'car' プロファイルがありません。");
        _pairs = [.. TestDataInitializer.LoadRoutePairs().Pairs];
        _index = 0;
    }

    [Benchmark(Description = "Itinero Router.TryCalculate (car.fastest)")]
    public Result<ItineroRoute>? Calculate()
    {
        var pair = _pairs[_index];
        _index = (_index + 1) % _pairs.Length;

        // Resolve → Calculate を一括で行う TryCalculate を使用（OsmDotRoute も内部で Snap → Dijkstra なので等価）
        return _router.TryCalculate(
            _profile,
            (float)pair.FromLat, (float)pair.FromLon,
            (float)pair.ToLat, (float)pair.ToLon);
    }
}
