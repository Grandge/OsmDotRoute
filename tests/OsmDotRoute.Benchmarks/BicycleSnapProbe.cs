using OsmDotRoute.Benchmarks.Generators;

namespace OsmDotRoute.Benchmarks;

/// <summary>
/// 既存 route-pairs.json (Car スナップ成功 100 ペア) を Bicycle プロファイルで
/// Calculate し、成功 / Snap 失敗 / 経路発見失敗の件数を出力する診断ツール
/// （Phase 3 ステップ 3E.1、計画書 §4.1）。
/// </summary>
/// <remarks>
/// 結果は <c>phase3_benchmark_results.md §3.2 C2 Bicycle スナップ失敗率</c> に転記する。
/// ベンチマーク本体には組み込まれない (BenchmarkDotNet の対象外、コンソール出力のみ)。
/// </remarks>
internal static class BicycleSnapProbe
{
    public static void Run()
    {
        Console.WriteLine($"== Bicycle Snap Probe ==");
        Console.WriteLine($"津島市 .odrg: {BenchmarkAssets.TsushimaOdrgPath}");
        var (routerDb, graph) = BenchmarkAssets.LoadNativeRouterDb();
        try
        {
            var router = new Router(routerDb);
            var profile = VehicleProfile.Bicycle;
            var pairs = TestDataInitializer.LoadRoutePairs().Pairs;
            Console.WriteLine($"ペア数: {pairs.Count}");

            const float snapDistanceM = 500f;
            var success = 0;
            var fromSnapFail = 0;
            var toSnapFail = 0;
            var routeFail = 0;

            foreach (var pair in pairs)
            {
                var fromSnap = router.SnapToRoad(profile, pair.From, snapDistanceM);
                if (fromSnap is null)
                {
                    fromSnapFail++;
                    continue;
                }
                var toSnap = router.SnapToRoad(profile, pair.To, snapDistanceM);
                if (toSnap is null)
                {
                    toSnapFail++;
                    continue;
                }
                var route = router.Calculate(profile, pair.From, pair.To);
                if (route is null) routeFail++;
                else success++;
            }

            var total = pairs.Count;
            Console.WriteLine();
            Console.WriteLine($"成功:             {success,3} / {total} ({100.0 * success / total:F1}%)");
            Console.WriteLine($"From スナップ失敗: {fromSnapFail,3} / {total} ({100.0 * fromSnapFail / total:F1}%)");
            Console.WriteLine($"To スナップ失敗:   {toSnapFail,3} / {total} ({100.0 * toSnapFail / total:F1}%)");
            Console.WriteLine($"経路発見失敗:     {routeFail,3} / {total} ({100.0 * routeFail / total:F1}%)");
        }
        finally
        {
            graph.Dispose();
        }
    }
}
