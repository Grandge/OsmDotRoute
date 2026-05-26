using BenchmarkDotNet.Attributes;
using OsmDotRoute.Benchmarks.Generators;
using OsmDotRoute.Native;

namespace OsmDotRoute.Benchmarks.Benchmarks;

/// <summary>
/// 制約下の経路計算性能（REQ-NFR-002 + Phase 3 ステップ 3B 効果測定）。
/// </summary>
/// <remarks>
/// <para>
/// Phase 1 計画書 §3.4 の 5 ケース（C0〜C4）を <see cref="Case"/> で切替。
/// Phase 3 ステップ 3B.5-B (計画書 §4.5-B、ユーザー判断 T15/T16=(A)) で 3 モード分岐を追加:
/// </para>
/// <list type="bullet">
///   <item><c>"Itinero"</c>: 親プロ default.routerdb + <see cref="OsmDotRoute.Itinero.ItineroRoadGraph"/>
///         (Phase 1 動作、参考値、RouterDb 規模差あり)</item>
///   <item><c>"Native-Detached"</c>: 津島市 .odrg + <see cref="NativeRoadGraph"/> + AttachGraph **未実行**
///         (3B 前相当、graph 未注入の Phase 1 動作フォールバック)</item>
///   <item><c>"Native-Attached"</c>: 津島市 .odrg + <see cref="NativeRoadGraph"/> + AttachGraph 実行済
///         (3B キャッシュ動作、本命)</item>
/// </list>
/// <para>
/// 3B 効果 = <c>(Native-Detached - Native-Attached) / Native-Detached × 100</c>。
/// 同一 RouterDb (津島市 .odrg) + 同一 100 ペア で 3B キャッシュ有無のみが違う。
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class RouteWithConstraintsBenchmark
{
    private OsmDotRoute.RouterDb _routerDb = default!;
    private NativeRoadGraph? _nativeGraph;
    private Router _router = default!;
    private VehicleProfile _profile = default!;
    private RoutePair[] _pairs = default!;
    private int _index;

    /// <summary>
    /// Phase 3 ステップ 3B.5-B モード切替。Native-Detached/Native-Attached が 3B 効果計測の本命、
    /// Itinero は参考値（RouterDb 規模差あり、3E で詳細）。
    /// </summary>
    [Params("Itinero", "Native-Detached", "Native-Attached")]
    public string Mode { get; set; } = "Native-Attached";

    /// <summary>
    /// C0: 制約なし / C1: 混合 10 件 / C2: 混合 50 件 / C3: 混合 100 件 / C4: Block-only 100 件。
    /// 3B.5-B では C0 (baseline) と C3 (3B 効果本命) を実測対象とする。
    /// </summary>
    [Params("C0", "C3")]
    public string Case { get; set; } = "C0";

    [GlobalSetup]
    public void Setup()
    {
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

        switch (Mode)
        {
            case "Itinero":
                _routerDb = BenchmarkAssets.LoadOsmDotRouterDb();
                _router = new Router(_routerDb, restrictions);  // 既存パス (Itinero は IsGraphAttached=true でも全エッジ走査 fallback)
                break;
            case "Native-Detached":
                (_routerDb, _nativeGraph) = BenchmarkAssets.LoadNativeRouterDb();
                _router = new Router(_routerDb, restrictions, autoAttachGraph: false);  // 3B 前相当: AttachGraph スキップ → Phase 1 fallback
                break;
            case "Native-Attached":
                (_routerDb, _nativeGraph) = BenchmarkAssets.LoadNativeRouterDb();
                _router = new Router(_routerDb, restrictions);  // 3B キャッシュ動作 (autoAttachGraph=true default)
                break;
            default:
                throw new InvalidOperationException($"未知の Mode: {Mode}");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _nativeGraph?.Dispose();
        _nativeGraph = null;
    }

    [Benchmark(Description = "Router.Calculate (Mode + Case)")]
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
