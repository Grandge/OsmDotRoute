namespace OsmDotRoute;

/// <summary>
/// 経路計算用のグラフデータ（REQ-MAP-001）。
/// Phase 1 では内部に Itinero RouterDb を保持するが、公開 API に Itinero 型は露出させない（REQ-API-003）。
/// </summary>
public sealed class RouterDb
{
    // Step 3 で内部実装（IRoadGraph 保持）に置き換える。
    private RouterDb()
    {
    }

    /// <summary>
    /// Itinero RouterDb（<c>.routerdb</c>）ファイルを読み込む（REQ-MAP-001）。
    /// </summary>
    /// <param name="filePath">RouterDb ファイルパス</param>
    public static RouterDb LoadFromFile(string filePath)
        => throw new NotImplementedException("Step 3 で実装予定");

    /// <summary>
    /// 読み込み済みグラフから頂点数・辺数・経緯度範囲の統計を取得する（REQ-MAP-002）。
    /// </summary>
    public RouterDbStatistics GetStatistics()
        => throw new NotImplementedException("Step 3 で実装予定");
}
