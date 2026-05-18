namespace OsmDotRoute.Routing;

/// <summary>
/// Dijkstra 結果から公開型 <see cref="Route"/> を組み立てる。
/// </summary>
/// <remarks>
/// Phase 1 のシェイプ統合方針:
/// <list type="bullet">
///   <item>ソース部分: <c>[ソーススナップ点, ソース端点頂点]</c>（中間シェイプは未補間）</item>
///   <item>中間エッジ: <c>[..中間シェイプ.., 到達側頂点]</c>。エッジ進行方向に合わせシェイプを反転</item>
///   <item>ターゲット部分: <c>[..中間シェイプ.., ターゲットスナップ点]</c>（同様に未補間）</item>
/// </list>
/// 総距離は <see cref="DijkstraResult.TotalDistanceM"/>（エッジ DistanceM の積算）をそのまま採用し、
/// シェイプ多角線の実長との誤差は許容する（Phase 1 完了判定で ±10% 以内）。
/// </remarks>
internal sealed class RouteBuilder
{
    private readonly IRoadGraph _graph;

    public RouteBuilder(IRoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
    }

    public Route Build(SnapResult sourceSnap, SnapResult targetSnap, DijkstraResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var shape = new List<GeoCoordinate>
        {
            sourceSnap.Location,
        };

        if (result.SameEdge)
        {
            shape.Add(targetSnap.Location);
            return new Route(result.TotalDistanceM, result.TotalDurationSec, shape);
        }

        // VertexPath: [sourceEndpoint, v1, v2, ..., targetEndpoint]
        // EdgePath:   [sourceSnap.EdgeId, e_to_v1, e_to_v2, ..., e_to_targetEndpoint]
        var vertexPath = result.VertexPath;
        var edgePath = result.EdgePath;

        if (vertexPath.Count == 0)
        {
            // 想定外（SameEdge=false なら少なくとも 1 頂点はある）。スナップ点のみで返す。
            shape.Add(targetSnap.Location);
            return new Route(result.TotalDistanceM, result.TotalDurationSec, shape);
        }

        // ソース端点
        shape.Add(_graph.GetVertex(vertexPath[0]));

        // 中間エッジ（index 1 以降の EdgePath を辿る）
        for (int i = 1; i < vertexPath.Count; i++)
        {
            var fromVertex = vertexPath[i - 1];
            var toVertex = vertexPath[i];
            var edgeId = edgePath[i]; // i=1 は中間遷移の最初のエッジ
            var edge = _graph.GetEdge(edgeId);

            // 進行方向（fromVertex → toVertex）がストレージ順（edge.From → edge.To）と一致するか
            var traversedInStorageOrder = edge.From == fromVertex && edge.To == toVertex;
            if (edge.Shape.Count > 0)
            {
                if (traversedInStorageOrder)
                {
                    for (int s = 0; s < edge.Shape.Count; s++)
                    {
                        shape.Add(edge.Shape[s]);
                    }
                }
                else
                {
                    for (int s = edge.Shape.Count - 1; s >= 0; s--)
                    {
                        shape.Add(edge.Shape[s]);
                    }
                }
            }
            shape.Add(_graph.GetVertex(toVertex));
        }

        shape.Add(targetSnap.Location);

        return new Route(result.TotalDistanceM, result.TotalDurationSec, shape);
    }
}
