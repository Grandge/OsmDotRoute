using OsmDotRoute.Routing;

namespace OsmDotRoute;

/// <summary>
/// 経路計算用のグラフデータ（REQ-MAP-001）。
/// Phase 1 では <c>OsmDotRoute.Itinero.ItineroRouterDbLoader.LoadFromFile</c> から内部的に生成される。
/// 公開 API に Itinero 型は露出させない（REQ-API-003）。
/// </summary>
public sealed class RouterDb
{
    private readonly IRoadGraph _graph;

    internal RouterDb(IRoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
    }

    /// <summary>
    /// 経路計算エンジン向けの内部グラフアクセサ。
    /// </summary>
    internal IRoadGraph Graph => _graph;

    /// <summary>
    /// 読み込み済みグラフから頂点数・辺数・経緯度範囲の統計を取得する（REQ-MAP-002）。
    /// </summary>
    public RouterDbStatistics GetStatistics()
    {
        var bounds = _graph.GetBounds();
        return new RouterDbStatistics(
            checked((int)_graph.VertexCount),
            checked((int)_graph.EdgeCount),
            bounds.SouthWest,
            bounds.NorthEast);
    }
    // 都道府県単位（数百万エッジ）でも int 範囲内 (~2.1B) に収まる前提。Phase 4+ で全国対応する場合は long に拡張。
}
