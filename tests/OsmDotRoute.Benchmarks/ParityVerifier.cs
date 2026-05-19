using OsmDotRoute.Benchmarks.Generators;
using OsmDotRoute.Itinero;
using ItineroProfile = global::Itinero.Profiles.Profile;
using ItineroRouter = global::Itinero.Router;
using global::Itinero;

namespace OsmDotRoute.Benchmarks;

/// <summary>
/// 経路同等性検証（計画書 §5.1.1）:
/// 同じ route-pairs.json の 100 ペアに対して OsmDotRoute / Itinero 双方で経路計算し、
/// (a) 両方成功 / (b) OsmDotRoute-only / (c) Itinero-only / (d) 両方失敗 を分類、
/// 両方成功ペアでの距離差（±10% 基準）を集計する。
/// </summary>
internal static class ParityVerifier
{
    public static void Run()
    {
        Console.WriteLine("=== 経路同等性検証 ===");
        var pairs = TestDataInitializer.LoadRoutePairs().Pairs;
        Console.WriteLine($"対象ペア: {pairs.Count}");

        // OsmDotRoute 側初期化
        var osmDb = BenchmarkAssets.LoadOsmDotRouterDb();
        var osmRouter = new Router(osmDb);
        var osmProfile = VehicleProfile.Car;

        // Itinero 側初期化
        var itineroDb = BenchmarkAssets.LoadItineroRouterDb();
        var itineroRouter = new ItineroRouter(itineroDb);
        var itineroProfile = itineroDb.GetSupportedProfile("car")
            ?? throw new InvalidOperationException("Itinero RouterDb に 'car' プロファイルがありません。");

        int bothSucceeded = 0;
        int osmOnly = 0;
        int itineroOnly = 0;
        int bothFailed = 0;
        int within10Pct = 0;
        int over10Pct = 0;
        double maxDeviationPct = 0;
        var maxDeviationIndex = -1;
        var deviations = new List<double>();

        for (var i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];

            var osmRoute = osmRouter.Calculate(osmProfile, p.From, p.To);
            var itineroResult = itineroRouter.TryCalculate(
                itineroProfile,
                (float)p.FromLat, (float)p.FromLon,
                (float)p.ToLat, (float)p.ToLon);
            var itineroSuccess = !itineroResult.IsError;

            if (osmRoute is null && !itineroSuccess) { bothFailed++; continue; }
            if (osmRoute is null) { itineroOnly++; continue; }
            if (!itineroSuccess) { osmOnly++; continue; }

            bothSucceeded++;
            var osmDist = osmRoute.TotalDistanceM;
            var itineroDist = itineroResult.Value.TotalDistance;
            var baseDist = Math.Max(osmDist, itineroDist);
            var deviationPct = baseDist > 0
                ? Math.Abs(osmDist - itineroDist) / baseDist * 100
                : 0;
            deviations.Add(deviationPct);

            if (deviationPct <= 10.0) within10Pct++;
            else over10Pct++;

            if (deviationPct > maxDeviationPct)
            {
                maxDeviationPct = deviationPct;
                maxDeviationIndex = i;
            }
        }

        Console.WriteLine();
        Console.WriteLine("--- 経路発見成否の 4 区分 ---");
        Console.WriteLine($"(a) 両方成功         : {bothSucceeded} / {pairs.Count}");
        Console.WriteLine($"(b) OsmDotRoute-only : {osmOnly} / {pairs.Count}");
        Console.WriteLine($"(c) Itinero-only     : {itineroOnly} / {pairs.Count}");
        Console.WriteLine($"(d) 両方失敗         : {bothFailed} / {pairs.Count}");

        Console.WriteLine();
        Console.WriteLine("--- 距離同等性（両方成功ペアのみ） ---");
        if (bothSucceeded > 0)
        {
            var avg = deviations.Average();
            var p50 = Percentile(deviations, 0.5);
            var p95 = Percentile(deviations, 0.95);
            Console.WriteLine($"±10% 以内 : {within10Pct} / {bothSucceeded} ({100.0 * within10Pct / bothSucceeded:F1}%)");
            Console.WriteLine($"±10% 超過 : {over10Pct} / {bothSucceeded} ({100.0 * over10Pct / bothSucceeded:F1}%)");
            Console.WriteLine($"距離乖離 Mean = {avg:F2}%, Median = {p50:F2}%, P95 = {p95:F2}%, Max = {maxDeviationPct:F2}% (ペア index {maxDeviationIndex})");
        }
        else
        {
            Console.WriteLine("両方成功ペアなし — 距離同等性検証不可");
        }
    }

    private static double Percentile(List<double> sorted, double pct)
    {
        if (sorted.Count == 0) return 0;
        var copy = sorted.ToArray();
        Array.Sort(copy);
        var idx = Math.Max(0, Math.Min(copy.Length - 1, (int)Math.Round(pct * (copy.Length - 1))));
        return copy[idx];
    }
}
