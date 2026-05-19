using ItineroDb = global::Itinero.RouterDb;

namespace OsmDotRoute.Benchmarks;

/// <summary>
/// ベンチマーク全体で共有する RouterDb / テストデータのパス・ロード手順。
/// </summary>
internal static class BenchmarkAssets
{
    /// <summary>
    /// 親プロジェクト「災害廃棄物処理シミュレーション」の <c>default.routerdb</c>。
    /// Phase 1 ステップ 15 ベンチマーク計画書 §4 で借用するテストデータ。
    /// </summary>
    public const string RouterDbPath =
        @"d:\workspace\災害廃棄物処理シミュレーション\App\DisasterWasteSim.Server\Data\Scenarios\default.routerdb";

    /// <summary>ベンチマーク用 TestData ディレクトリ（カレント基準）。</summary>
    public static string TestDataDirectory =>
        Path.Combine(AppContext.BaseDirectory, "TestData");

    /// <summary>route-pairs.json のフルパス。</summary>
    public static string RoutePairsPath => Path.Combine(TestDataDirectory, "route-pairs.json");

    /// <summary>restrictions-mixed-100.json のフルパス。</summary>
    public static string MixedRestrictionsPath => Path.Combine(TestDataDirectory, "restrictions-mixed-100.json");

    /// <summary>restrictions-block-100.json のフルパス。</summary>
    public static string BlockRestrictionsPath => Path.Combine(TestDataDirectory, "restrictions-block-100.json");

    /// <summary>
    /// Itinero RouterDb を 1 回だけ Deserialize し、参照を保持する。
    /// OsmDotRoute / Itinero 両方のベンチで同じインスタンスを使い回せるようにする。
    /// </summary>
    private static ItineroDb? _itineroDb;
    private static readonly object _lock = new();

    public static ItineroDb LoadItineroRouterDb()
    {
        lock (_lock)
        {
            if (_itineroDb is not null) return _itineroDb;
            if (!File.Exists(RouterDbPath))
            {
                throw new FileNotFoundException(
                    $"ベンチマーク用 RouterDb が見つかりません: {RouterDbPath}\n" +
                    "親プロジェクト 災害廃棄物処理シミュレーション の default.routerdb を配置するか、パスを修正してください。",
                    RouterDbPath);
            }
            using var stream = File.OpenRead(RouterDbPath);
            _itineroDb = ItineroDb.Deserialize(stream);
            return _itineroDb;
        }
    }

    /// <summary>OsmDotRoute 用 RouterDb を生成（Itinero RouterDb 共有、別インスタンス）。</summary>
    public static OsmDotRoute.RouterDb LoadOsmDotRouterDb()
    {
        var itinero = LoadItineroRouterDb();
        return OsmDotRoute.Itinero.ItineroRouterDbLoader.FromItineroRouterDb(itinero);
    }
}
