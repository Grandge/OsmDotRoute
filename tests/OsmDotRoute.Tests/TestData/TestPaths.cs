namespace OsmDotRoute.Tests.TestData;

/// <summary>
/// テストで参照する外部データファイルのパス。
/// ファイルが存在しないテストはスキップ扱いとする（個人マシン固有のデータに依存しないため）。
/// </summary>
internal static class TestPaths
{
    /// <summary>
    /// 親プロジェクト「災害廃棄物処理シミュレーション」の default.routerdb。
    /// Phase 1 ステップ 3 のアダプター検証で利用。
    /// </summary>
    public const string ParentDefaultRouterDb =
        @"d:\workspace\災害廃棄物処理シミュレーション\App\DisasterWasteSim.Server\Data\Scenarios\default.routerdb";
}
