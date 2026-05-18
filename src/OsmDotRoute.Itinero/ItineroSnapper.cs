using global::Itinero; // Resolve / GetSupportedProfile などの拡張メソッドのため
using OsmDotRoute.Routing;
using ItineroDb = global::Itinero.RouterDb;
using ItineroRouter = global::Itinero.Router;
using ResolveFailedException = global::Itinero.Exceptions.ResolveFailedException;

namespace OsmDotRoute.Itinero;

/// <summary>
/// Itinero <see cref="ItineroRouter"/>.Resolve を呼び出して座標スナップを実現する <see cref="IRoadSnapper"/> 実装（Phase 1）。
/// Phase 2 で独自空間インデックス実装に置き換わる。
/// </summary>
internal sealed class ItineroSnapper : IRoadSnapper
{
    private readonly ItineroDb _routerDb;
    private readonly ItineroRouter _router;

    public ItineroSnapper(ItineroDb routerDb)
    {
        ArgumentNullException.ThrowIfNull(routerDb);
        _routerDb = routerDb;
        // Itinero Router は内部で空間インデックスをキャッシュするため、インスタンスを使い回す
        _router = new ItineroRouter(routerDb);
    }

    /// <inheritdoc/>
    public SnapResult? Snap(string profileName, GeoCoordinate point, float searchDistanceM)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return null;
        }

        var profile = _routerDb.GetSupportedProfile(profileName);
        if (profile == null)
        {
            return null;
        }

        try
        {
            var routerPoint = _router.Resolve(
                profile,
                (float)point.Latitude,
                (float)point.Longitude,
                searchDistanceM);

            if (routerPoint == null)
            {
                return null;
            }

            var loc = routerPoint.LocationOnNetwork(_routerDb);
            return new SnapResult(
                new GeoCoordinate(loc.Latitude, loc.Longitude),
                routerPoint.EdgeId,
                routerPoint.Offset);
        }
        catch (ResolveFailedException)
        {
            // 検索半径内に該当道路無し
            return null;
        }
    }
}
