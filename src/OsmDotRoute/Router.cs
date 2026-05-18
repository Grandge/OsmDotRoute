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
        => throw new NotImplementedException("Step 5b で実装予定");

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
    public RoadNetworkGeoJson GetRoadNetworkGeoJson()
        => throw new NotImplementedException("Step 6 で実装予定");
}
