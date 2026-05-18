using global::Itinero;
using global::Itinero.Osm.Vehicles;
using OsmDotRoute.Itinero;
using OsmDotRoute.Tests.TestData;
using Xunit;

namespace OsmDotRoute.Tests;

/// <summary>
/// 同梱 car.json / pedestrian.json の評価結果を Itinero の Vehicle.Car/Pedestrian 相当と比較する
/// パリティテスト（実装計画書ステップ 5a 完了判定）。
///
/// 親プロジェクト default.routerdb の全 edge_profile を列挙し:
/// - 通行可否 100% 一致
/// - 速度 ±10% 以内（許容差）
///
/// 完全一致は目指さない（実装方針差で多少のブレあり）。
/// 重大な乖離があればテストが失敗し、JSON の調整指針となる。
/// </summary>
public class ProfileParityTests
{
    [Fact]
    public void CarProfile_Parity_AgainstItineroVehicleCar()
    {
        EnsureTestData();
        var osmDotRouteCar = VehicleProfile.Car;
        var itineroCar = Vehicle.Car.Fastest();

        // routerdb を生で読み直し、edge_profile を Itinero に直接問い合わせる
        using var stream = File.OpenRead(TestPaths.ParentDefaultRouterDb);
        var itineroDb = global::Itinero.RouterDb.Deserialize(stream);
        var osmDotRouteDb = ItineroRouterDbLoader.FromItineroRouterDb(itineroDb);

        int totalSamples = 0;
        int accessMismatch = 0;
        int speedMismatch = 0;
        var maxAbsoluteSpeedDiff = 0.0;
        var accessMismatchSamples = new List<string>();

        // edge_profile のインデックスは EdgeProfiles の Count 個（ushort）
        // 全件チェック。実環境では数百〜数千 profile 程度。
        var count = itineroDb.EdgeProfiles.Count;
        for (uint i = 0; i < count; i++)
        {
            var attrs = itineroDb.EdgeProfiles.Get(i);
            if (attrs == null) continue;

            // OSM タグ展開
            var tags = new Dictionary<string, string>(attrs.Count, StringComparer.Ordinal);
            foreach (var a in attrs)
            {
                tags[a.Key] = a.Value;
            }

            // 道路エッジ以外（highway タグ無し）はスキップ
            if (!tags.ContainsKey("highway")) continue;

            // OsmDotRoute 評価
            var ourEval = osmDotRouteCar.Evaluator.Evaluate(tags);

            // Itinero 評価
            var itineroFs = itineroCar.FactorAndSpeed(attrs);
            var itineroCanPass = itineroFs.Value > 0f;
            // Itinero SpeedFactor は 1/(speed_meters_per_second)。0 のときは無効。
            var itineroSpeedKmh = itineroFs.SpeedFactor > 0
                ? (1.0 / itineroFs.SpeedFactor) * 3.6
                : 0.0;

            totalSamples++;

            // 通行可否比較
            if (ourEval.CanPass != itineroCanPass)
            {
                accessMismatch++;
                if (accessMismatchSamples.Count < 5)
                {
                    var tagsStr = string.Join(",", tags.Select(kv => $"{kv.Key}={kv.Value}"));
                    accessMismatchSamples.Add($"  profile[{i}]: ours={ourEval.CanPass} itinero={itineroCanPass}, tags=[{tagsStr}]");
                }
                continue; // 通行不可なら速度比較は意味がない
            }

            // 通行可同士の場合のみ速度比較
            if (ourEval.CanPass && itineroSpeedKmh > 0)
            {
                var diff = Math.Abs(ourEval.SpeedKmh - itineroSpeedKmh);
                var ratio = diff / itineroSpeedKmh;
                if (ratio > 0.10)
                {
                    speedMismatch++;
                }
                if (diff > maxAbsoluteSpeedDiff)
                {
                    maxAbsoluteSpeedDiff = diff;
                }
            }
        }

        // 結果サマリ（テスト出力に残す）
        var summary =
            $"パリティ統計: samples={totalSamples}, " +
            $"accessMismatch={accessMismatch} ({(double)accessMismatch / totalSamples:P1}), " +
            $"speedMismatch>10%={speedMismatch} ({(double)speedMismatch / totalSamples:P1}), " +
            $"maxAbsoluteSpeedDiff={maxAbsoluteSpeedDiff:F1} km/h";
        if (accessMismatchSamples.Count > 0)
        {
            summary += "\n通行可否乖離サンプル:\n" + string.Join("\n", accessMismatchSamples);
        }

        // 完了判定: 通行可否 100% 一致、速度 ±10% 以内が 80% 以上
        Assert.True(totalSamples > 0, "サンプルが 0 件（routerdb 不正の可能性）");
        Assert.True(accessMismatch == 0,
            $"通行可否で乖離が発生: {summary}");
        var speedMismatchRate = (double)speedMismatch / totalSamples;
        Assert.True(speedMismatchRate <= 0.20,
            $"速度乖離 >10% が 20% を超過: {summary}");
    }

    [Fact]
    public void PedestrianProfile_Parity_AgainstItineroVehiclePedestrian()
    {
        EnsureTestData();
        var osmDotRoutePed = VehicleProfile.Pedestrian;
        var itineroPed = Vehicle.Pedestrian.Fastest();

        using var stream = File.OpenRead(TestPaths.ParentDefaultRouterDb);
        var itineroDb = global::Itinero.RouterDb.Deserialize(stream);

        int totalSamples = 0;
        int accessMismatch = 0;
        int speedMismatch = 0;

        var count = itineroDb.EdgeProfiles.Count;
        for (uint i = 0; i < count; i++)
        {
            var attrs = itineroDb.EdgeProfiles.Get(i);
            if (attrs == null) continue;

            var tags = new Dictionary<string, string>(attrs.Count, StringComparer.Ordinal);
            foreach (var a in attrs)
            {
                tags[a.Key] = a.Value;
            }

            if (!tags.ContainsKey("highway")) continue;

            var ourEval = osmDotRoutePed.Evaluator.Evaluate(tags);
            var itineroFs = itineroPed.FactorAndSpeed(attrs);
            var itineroCanPass = itineroFs.Value > 0f;
            var itineroSpeedKmh = itineroFs.SpeedFactor > 0
                ? (1.0 / itineroFs.SpeedFactor) * 3.6
                : 0.0;

            totalSamples++;
            if (ourEval.CanPass != itineroCanPass)
            {
                accessMismatch++;
                continue;
            }

            if (ourEval.CanPass && itineroSpeedKmh > 0)
            {
                var ratio = Math.Abs(ourEval.SpeedKmh - itineroSpeedKmh) / itineroSpeedKmh;
                if (ratio > 0.10) speedMismatch++;
            }
        }

        Assert.True(totalSamples > 0);
        // 歩行者は許容差を緩めに（pedestrian は元々 5km/h 一律のためほぼ一致するはず）
        Assert.True(accessMismatch == 0,
            $"歩行者通行可否で乖離: {accessMismatch}/{totalSamples}");
        Assert.True((double)speedMismatch / totalSamples <= 0.20,
            $"歩行者速度乖離 >10% が 20% を超過: {speedMismatch}/{totalSamples}");
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
