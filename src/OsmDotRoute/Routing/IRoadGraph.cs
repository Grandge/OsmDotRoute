using OsmDotRoute.Geometry;
using OsmDotRoute.Profiles;

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
    /// エニュメレータが指す現在エッジを、指定 <see cref="ProfileEvaluator"/> で評価する
    /// （ホットパス用、Dijkstra 近傍展開で使用）。
    /// Phase 1 → Phase 3 セマンティック移行 (Phase 3 ステップ 3A.3b)。
    /// </summary>
    /// <remarks>
    /// Itinero 系: 内部で OSM タグを取得し <c>evaluator.Evaluate(tags)</c> を呼ぶ。
    /// Native 系: <c>evaluator.Name</c> で <c>.odrg</c> の BAKED_PROFILE スロットを解決し、bake 済値を直接返却。
    /// </remarks>
    EdgeEvaluation EvaluateEdge(IRoadGraphEdgeEnumerator en, ProfileEvaluator evaluator);

    /// <summary>
    /// エッジ ID で直接取得した <see cref="RoadEdge"/> を、指定 <see cref="ProfileEvaluator"/> で評価する
    /// （スナップエッジ評価用、Dijkstra 開始時の sourceEdge/targetEdge 評価で使用）。
    /// </summary>
    EdgeEvaluation EvaluateEdge(RoadEdge edge, ProfileEvaluator evaluator);

    /// <summary>
    /// 指定エッジ ID のエッジ情報（端点・距離・プロファイル index・シェイプ）を取得する。
    /// Phase 1 ではスナップ結果（<see cref="SnapResult.EdgeId"/>）から経路探索の起点・終点情報を取得するためと、
    /// 経路復元（<see cref="RouteBuilder"/>）でシェイプを取り出すために使用する。
    /// </summary>
    RoadEdge GetEdge(uint edgeId);
}
