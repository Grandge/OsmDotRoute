using BenchmarkDotNet.Attributes;
using OsmDotRoute.Itinero;

namespace OsmDotRoute.Benchmarks.Benchmarks;

/// <summary>
/// RouterDb 読み込み性能（REQ-NFR-003 のメモリ計測も兼ねる）。
/// `LoadFromFile` が <see cref="OsmDotRoute.RouterDb"/> までを構築する単一ベンチ。
/// Itinero 単独の Deserialize も比較として測定。
/// </summary>
[MemoryDiagnoser]
public class RouterDbLoadBenchmark
{
    [Benchmark(Baseline = true, Description = "Itinero RouterDb.Deserialize 単独")]
    public global::Itinero.RouterDb ItineroDeserialize()
    {
        using var stream = File.OpenRead(BenchmarkAssets.RouterDbPath);
        return global::Itinero.RouterDb.Deserialize(stream);
    }

    [Benchmark(Description = "OsmDotRoute.RouterDb (Itinero ラップ込み)")]
    public OsmDotRoute.RouterDb OsmDotRouteLoad()
    {
        return ItineroRouterDbLoader.LoadFromFile(BenchmarkAssets.RouterDbPath);
    }
}
