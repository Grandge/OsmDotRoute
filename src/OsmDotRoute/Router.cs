using OsmDotRoute.GeoJson;
using OsmDotRoute.Routing;

namespace OsmDotRoute;

/// <summary>
/// OsmDotRoute の公開ファサード（REQ-API-001）。
/// 経路計算・道路スナップ・道路ネットワーク GeoJSON 出力を提供する。
/// </summary>
public sealed class Router
{
    private readonly RouterDb _routerDb;
    private readonly RestrictedAreaService? _restrictions;

    /// <summary>
    /// Router を構築する。
    /// </summary>
    /// <param name="routerDb">経路計算用グラフ</param>
    /// <param name="restrictions">動的制約サービス（null の場合は制約なし）</param>
    public Router(RouterDb routerDb, RestrictedAreaService? restrictions = null)
    {
        ArgumentNullException.ThrowIfNull(routerDb);
        _routerDb = routerDb;
        _restrictions = restrictions;
        // Phase 3 ステップ 3B.3 (T9=A): restrictions に IRoadGraph を注入して
        // eager bake を有効化。同一 service の複数 Router 共有時は 2 回目以降 no-op、
        // 別 routerDb への流用は InvalidOperationException。
        restrictions?.AttachGraph(routerDb.Graph);
    }

    /// <summary>
    /// 2 点間の最短経路を計算する（REQ-RTE-001）。
    /// 経路未発見時・ネットワーク外座標時は <c>null</c> を返す（REQ-RTE-006, REQ-RTE-008）。
    /// </summary>
    /// <param name="profile">車両プロファイル</param>
    /// <param name="from">起点座標</param>
    /// <param name="to">終点座標</param>
    /// <returns>経路、または <c>null</c></returns>
    public Route? Calculate(VehicleProfile profile, GeoCoordinate from, GeoCoordinate to)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // ステップ 5b 現時点ではスナップ半径を 500m に固定。Phase 1 中に「動的スナップ半径」要件が出れば再考。
        const float SearchDistanceM = 500f;

        var sourceSnap = _routerDb.Snapper.Snap(profile.Name, from, SearchDistanceM);
        if (sourceSnap is null) return null;

        var targetSnap = _routerDb.Snapper.Snap(profile.Name, to, SearchDistanceM);
        if (targetSnap is null) return null;

        var calculator = new EdgeWeightCalculator(_routerDb.Graph, profile.Evaluator, _restrictions);
        var engine = new DijkstraEngine(_routerDb.Graph, calculator);
        var result = engine.Run(sourceSnap.Value, targetSnap.Value);
        if (result is null) return null;

        var builder = new RouteBuilder(_routerDb.Graph);
        return builder.Build(sourceSnap.Value, targetSnap.Value, result);
    }

    /// <summary>
    /// 任意座標を最寄り道路上にスナップする（REQ-RTE-002〜003）。
    /// 道路ネットワーク外の座標は <c>null</c> を返す（REQ-RTE-008）。
    /// </summary>
    /// <param name="profile">車両プロファイル</param>
    /// <param name="point">スナップ対象座標</param>
    /// <param name="searchDistanceM">検索半径（メートル、既定 500m）</param>
    /// <returns>スナップ後座標、または <c>null</c></returns>
    public GeoCoordinate? SnapToRoad(VehicleProfile profile, GeoCoordinate point, float searchDistanceM = 500f)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var result = _routerDb.Snapper.Snap(profile.Name, point, searchDistanceM);
        return result?.Location;
    }

    /// <summary>
    /// 道路ネットワーク全体を GeoJSON FeatureCollection（LineString 列）として出力する（REQ-RTE-004）。
    /// </summary>
    /// <returns>道路ネットワーク GeoJSON</returns>
    public RoadNetworkGeoJson GetRoadNetworkGeoJson()
        => new RoadNetworkGeoJson(GeoJsonWriter.WriteRoadNetwork(_routerDb.Graph));
}
