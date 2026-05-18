using OsmDotRoute.Geometry;

namespace OsmDotRoute.Routing;

/// <summary>
/// 経路計算エンジンが依存する道路グラフ抽象。
/// Phase 1: <c>ItineroRoadGraph</c>（Itinero RouterDb ラップ）、Phase 2 以降: 独自バイナリグラフ実装に差し替え可能。
/// </summary>
internal interface IRoadGraph
{
    /// <summary>頂点数</summary>
    uint VertexCount { get; }

    /// <summary>辺数（無向重複なし）。Itinero に合わせて <c>long</c>。</summary>
    long EdgeCount { get; }

    /// <summary>グラフ全体の経緯度範囲（AABB）。頂点列挙して計算するため初回呼び出しは O(V)。</summary>
    GeoBounds GetBounds();

    /// <summary>指定頂点 ID の緯度経度座標を取得する。</summary>
    GeoCoordinate GetVertex(uint vertexId);

    /// <summary>
    /// 指定頂点から出るエッジを列挙するエニュメレータを取得する。
    /// 列挙中は <see cref="IRoadGraphEdgeEnumerator.MoveNext"/> で次へ進める。
    /// </summary>
    IRoadGraphEdgeEnumerator GetEdgeEnumerator(uint vertexId);

    /// <summary>
    /// エッジプロファイル index に対応する OSM タグ集合を取得する（REQ-PRF-001/002 のプロファイル評価で参照）。
    /// </summary>
    /// <param name="edgeProfileIndex">エッジプロファイルインデックス（<see cref="IRoadGraphEdgeEnumerator.EdgeProfileIndex"/> から取得）</param>
    IReadOnlyDictionary<string, string> GetEdgeOsmTags(ushort edgeProfileIndex);

    /// <summary>
    /// 指定エッジ ID のエッジ情報（端点・距離・プロファイル index・シェイプ）を取得する。
    /// Phase 1 ではスナップ結果（<see cref="SnapResult.EdgeId"/>）から経路探索の起点・終点情報を取得するためと、
    /// 経路復元（<see cref="RouteBuilder"/>）でシェイプを取り出すために使用する。
    /// </summary>
    RoadEdge GetEdge(uint edgeId);
}
