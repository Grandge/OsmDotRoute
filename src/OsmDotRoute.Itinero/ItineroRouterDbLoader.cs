using ItineroDb = global::Itinero.RouterDb;

namespace OsmDotRoute.Itinero;

/// <summary>
/// Itinero RouterDb（<c>.routerdb</c>）ファイルから <see cref="OsmDotRoute.RouterDb"/> を生成するローダー（REQ-MAP-001）。
/// 親プロジェクトから <c>using Itinero;</c> を消去できるようにする（REQ-API-003）ため、
/// アセンブリ依存方向の制約上、ローダーは本アダプタープロジェクトに配置する。
/// </summary>
public static class ItineroRouterDbLoader
{
    /// <summary>
    /// Itinero RouterDb ファイルを読み込んで <see cref="OsmDotRoute.RouterDb"/> を返す。
    /// </summary>
    /// <param name="filePath">RouterDb ファイルパス</param>
    /// <returns>OsmDotRoute 用 <see cref="OsmDotRoute.RouterDb"/> インスタンス</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が <c>null</c> または空</exception>
    /// <exception cref="FileNotFoundException">ファイルが存在しない</exception>
    public static OsmDotRoute.RouterDb LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("ファイルパスを指定してください。", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("RouterDb ファイルが見つかりません。", filePath);
        }

        using var stream = File.OpenRead(filePath);
        var itineroRouterDb = ItineroDb.Deserialize(stream);
        return Build(itineroRouterDb);
    }

    /// <summary>
    /// 既に読み込み済みの Itinero RouterDb インスタンスから <see cref="OsmDotRoute.RouterDb"/> を生成する。
    /// 既存コード（親プロジェクト等）が独自に Itinero RouterDb を読み込むケース向け。
    /// </summary>
    /// <param name="itineroRouterDb">Itinero RouterDb インスタンス</param>
    /// <returns>OsmDotRoute 用 <see cref="OsmDotRoute.RouterDb"/> インスタンス</returns>
    /// <exception cref="ArgumentNullException"><paramref name="itineroRouterDb"/> が <c>null</c></exception>
    public static OsmDotRoute.RouterDb FromItineroRouterDb(ItineroDb itineroRouterDb)
    {
        ArgumentNullException.ThrowIfNull(itineroRouterDb);
        return Build(itineroRouterDb);
    }

    private static OsmDotRoute.RouterDb Build(ItineroDb itineroRouterDb)
    {
        var roadGraph = new ItineroRoadGraph(itineroRouterDb);
        var snapper = new ItineroSnapper(itineroRouterDb);
        return new OsmDotRoute.RouterDb(roadGraph, snapper);
    }
}
